#!/usr/bin/env bash

# Packs the Fable-compatible projects into a local feed, checks each nupkg is a Fable library for JavaScript, then
# compiles tests/Axial.Fable.PackageTests (which references those packages, not the projects) to JavaScript with Fable
# and runs it on Node. scripts/run-fable-tests.sh covers behaviour through project references; this covers what a
# Fable user gets from NuGet.

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
test_dir="$root_dir/tests/Axial.Fable.PackageTests"
work_dir="$root_dir/artifacts/fable-package-tests"
version=$(sed -n 's:.*<AxialPackageVersion>\(.*\)</AxialPackageVersion>.*:\1:p' "$test_dir/Axial.Fable.PackageTests.fsproj")
projects=(Axial Axial.Console Axial.FileSystem Axial.Process Axial.Configuration Axial.Hosting.Node Axial.Hosting.Browser Axial.Telemetry.JavaScript)

# The packages folder keeps third-party packages between runs; Axial packages are always the ones packed below.
rm -rf "$work_dir/feed" "$work_dir/out" "$work_dir"/packages/axial*

# A separate artifacts path keeps the test version out of the solution's Release outputs. Guardrails already ran in
# the solution build.
for project in "${projects[@]}"; do
    dotnet pack "$root_dir/src/$project/$project.fsproj" -c Release --nologo -v quiet -o "$work_dir/feed" \
        -p:Version="$version" -p:ArtifactsPath="$work_dir/build" -p:AxialGuardrailsEnabled=false
done

python3 - "$work_dir/feed" "$version" "${projects[@]}" <<'PY'
import re, sys, zipfile
feed, version, projects = sys.argv[1], sys.argv[2], sys.argv[3:]
failures = []
for project in projects:
    with zipfile.ZipFile(f"{feed}/{project}.{version}.nupkg") as package:
        names = package.namelist()
        nuspec = package.read(f"{project}.nuspec").decode("utf-8")
    tags = re.search(r"<tags>(.*?)</tags>", nuspec).group(1).split()
    sources = [name for name in names if name.startswith("fable/") and name.endswith(".fs")]
    if f"fable/{project}.fsproj" not in names: failures.append(f"{project}: no fable/{project}.fsproj")
    if not sources: failures.append(f"{project}: no F# sources under fable/")
    for tag in ("fable", "fable-library", "fable-javascript"):
        if tag not in tags: failures.append(f"{project}: missing tag {tag}")
    print(f"{project}: {len(sources)} fable sources; tags {' '.join(tags)}")
if failures:
    sys.exit("Fable package check failed:\n" + "\n".join(failures))
PY

dotnet fable "$test_dir/Axial.Fable.PackageTests.fsproj" --lang javascript --outDir "$work_dir/out"
printf '%s\n' '{ "type": "module" }' > "$work_dir/out/package.json"
node "$work_dir/out/Program.js"
