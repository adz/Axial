#!/usr/bin/env bash
# Runs the Axial.Guardrails analyzers (AXG001 effect boundary, AXG002 suppression integrity)
# against every src/Axial* project in Axial.slnx, plus examples/Axial.Examples, minus
# scripts/guardrails-exclude.txt.
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
codes=(AXG001 AXG002)

severity="${AXIAL_GUARDRAILS_SEVERITY:-error}"
if [ "$severity" = "warning" ]; then
  severity_flag="--treat-as-warning"
else
  severity_flag="--treat-as-error"
fi

echo "Building Axial.Guardrails..."
dotnet build src/Axial.Guardrails/Axial.Guardrails.fsproj --nologo -v quiet

mapfile -t excluded < <(grep -v '^\s*#' "$exclude_file" 2>/dev/null | grep -v '^\s*$' || true)

projects=()
while IFS= read -r proj; do
  skip=false
  for ex in "${excluded[@]}"; do
    [ "$proj" = "$ex" ] && skip=true && break
  done
  [ "$proj" = "src/Axial.Guardrails/Axial.Guardrails.fsproj" ] && skip=true
  $skip || projects+=("$proj")
done < <(grep -oP 'Path="\K[^"]+\.fsproj' Axial.slnx | grep '^src/')

projects+=("examples/Axial.Examples/Axial.Examples.fsproj")

echo "Checking ${#projects[@]} project(s) (severity: $severity)..."

dotnet tool run fsharp-analyzers \
  --project "${projects[@]}" \
  --analyzers-path "$analyzers_path" \
  --code-root "$repo_root" \
  "$severity_flag" "${codes[@]}" \
  --output-format github \
  -v n
