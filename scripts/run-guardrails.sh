#!/usr/bin/env bash
# Runs the Axial.Guardrails analyzers against Axial.slnx projects, minus
# scripts/guardrails-exclude.txt.
#
#   - src/Axial* and examples/Axial.Examples get all checks: AXG001 effect boundary, AXG002
#     suppression integrity, AXG003 raise/failwith inside flow { }, AXG004 shared xUnit fixtures.
#   - tests/Axial* projects get only AXG004. AXG001-003 are about Axial's own service-adapter
#     code; test code legitimately touches Thread.Sleep, System.IO.File, DateTimeOffset, and raw
#     exceptions for setup and assertions, so running those checks there would be noise, not
#     signal. AXG004 (shared fixtures) is the one check that only has anything to find in tests.
#
# Severity:
#   By default findings fail the run (exit non-zero). Set AXIAL_GUARDRAILS_SEVERITY=warning
#   to report findings without failing, e.g. while a package is being migrated onto explicit
#   services. Any other value (or unset) means "error".
#
# Opt-out:
#   List a project's .fsproj path (one per line) in scripts/guardrails-exclude.txt to skip it
#   entirely. Prefer a narrow `// axial-allow-effect: <category>` comment at the call site —
#   the exclude list is for a project that can't be checked at all, not a way to silence a
#   finding you haven't looked at.
#
# See docs/15-notes/03-guardrails.md.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

analyzers_path="artifacts/bin/Axial.Guardrails/debug"
exclude_file="scripts/guardrails-exclude.txt"
src_codes=(AXG001 AXG002 AXG003 AXG004)
test_codes=(AXG004)

severity="${AXIAL_GUARDRAILS_SEVERITY:-error}"
if [ "$severity" = "warning" ]; then
  severity_flag="--treat-as-warning"
else
  severity_flag="--treat-as-error"
fi

echo "Building Axial.Guardrails..."
dotnet build src/Axial.Guardrails/Axial.Guardrails.fsproj --nologo -v quiet

mapfile -t excluded < <(grep -v '^\s*#' "$exclude_file" 2>/dev/null | grep -v '^\s*$' || true)

is_excluded() {
  local proj="$1"
  for ex in "${excluded[@]}"; do
    [ "$proj" = "$ex" ] && return 0
  done
  return 1
}

src_projects=()
while IFS= read -r proj; do
  [ "$proj" = "src/Axial.Guardrails/Axial.Guardrails.fsproj" ] && continue
  is_excluded "$proj" || src_projects+=("$proj")
done < <(grep -oP 'Path="\K[^"]+\.fsproj' Axial.slnx | grep '^src/')

src_projects+=("examples/Axial.Examples/Axial.Examples.fsproj")

test_projects=()
while IFS= read -r proj; do
  is_excluded "$proj" || test_projects+=("$proj")
done < <(grep -oP 'Path="\K[^"]+\.fsproj' Axial.slnx | grep '^tests/')

echo "Checking ${#src_projects[@]} src project(s) (severity: $severity)..."

dotnet tool run fsharp-analyzers \
  --project "${src_projects[@]}" \
  --analyzers-path "$analyzers_path" \
  --code-root "$repo_root" \
  "$severity_flag" "${src_codes[@]}" \
  --output-format github \
  -v n

echo "Checking ${#test_projects[@]} test project(s) for shared fixtures (severity: $severity)..."

# --treat-as-error/--treat-as-warning only overrides the severity of the named codes; it does not
# restrict which analyzers run. --include-analyzers (by analyzer name, not diagnostic code) is
# what actually keeps AXG001-003 from running against test projects at all.
dotnet tool run fsharp-analyzers \
  --project "${test_projects[@]}" \
  --analyzers-path "$analyzers_path" \
  --code-root "$repo_root" \
  --include-analyzers Fixture \
  "$severity_flag" "${test_codes[@]}" \
  --output-format github \
  -v n
