#!/usr/bin/env bash
set -euo pipefail

fail() { echo "Guardrails v2 migration test: $*" >&2; exit 1; }

for removed in scripts/test-guardrails-baseline-growth.sh scripts/check-guardrails-baseline-growth.sh; do
    [[ ! -e "$removed" ]] || fail "$removed must be deleted; the v2 lock owns baseline growth"
done

if grep -qE 'test-guardrails-baseline-growth|check-guardrails-baseline-growth' scripts/verify.sh; then
    fail "verify.sh still invokes legacy baseline-growth scripts"
fi

# The removed severity setting may be mentioned by its diagnostic-only target, but must not affect policy.
python3 - <<'PY' || fail "legacy policy, severity, baseline-generation, or environment switches remain"
from pathlib import Path
import re
import xml.etree.ElementTree as ET

paths = [Path("Directory.Build.props"), Path("Directory.Build.targets")]
paths += list(Path("src/Axial.Guardrails/build").rglob("*"))
paths += list(Path("src/Axial.Guardrails").glob("*.fs"))
pattern = re.compile(r'AxialGuardrailsUseLegacyPolicy|AxialGuardrailsSeverity|AxialGuardrailsGenerateBaseline|GetEnvironmentVariable\(.?(AxialGuardrailsEnabled|AXIAL_GUARDRAILS_ENABLED|AxialGuardrailsSeverity|AXIAL_GUARDRAILS_SEVERITY)|CI=true')
for path in paths:
    if not path.is_file():
        continue
    text = path.read_text()
    if path.name == "Axial.Guardrails.targets":
        xml = ET.fromstring(text)
        for target in list(xml):
            if target.tag == "Target" and target.get("Name") == "AxialGuardrailsWarnRemovedSeverity":
                assert [node.tag for node in target] == ["Warning", "PropertyGroup"]
                assert target[0].get("Code") == "AXIALGR001"
                assert [(node.tag, node.text) for node in target[1]] == [("_AxialGuardrailsRemovedSeverityWarned", "true")]
                xml.remove(target)
        text = ET.tostring(xml, encoding="unicode")
    if path.name == "GuardrailConfig.fs":
        # The once-per-process diagnostic reads the removed environment spelling only to warn; it does not
        # feed policy or severity. Exclude that function from the legacy-switch scan.
        text = re.sub(r'let private removedSeverityText =.*?\n\nlet load', '\n\nlet load', text, flags=re.S)
    if pattern.search(text):
        raise SystemExit(f"legacy policy switch in {path}")
PY

[[ -f Axial.Guardrails.toml && -f Axial.Guardrails.lock ]] || fail "checked-in policy and lock are required"

tool="artifacts/bin/Axial.Tool/release/Axial.Tool.dll"
[[ -f "$tool" ]] || tool="artifacts/bin/Axial.Tool/debug/Axial.Tool.dll"
[[ -f "$tool" ]] || fail "Axial.Tool must be built before this test"
dotnet --roll-forward Major "$tool" check >/dev/null || fail "axial check rejected the checked-in policy"

backup=$(mktemp)
cp Axial.Guardrails.toml "$backup"
trap 'cp "$backup" Axial.Guardrails.toml; rm -f "$backup"' EXIT
printf '\n# deliberate policy drift\n' >> Axial.Guardrails.toml
output=$(dotnet --roll-forward Major "$tool" check 2>&1) || fail "axial check rejected deliberate policy drift"
grep -q 'WARNING: Policy changed since the last review:' <<<"$output" || fail "axial check did not warn about deliberate policy drift"
grep -q 'axial policy review' <<<"$output" || fail "policy drift warning did not point to review"
