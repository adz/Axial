#!/usr/bin/env bash
set -euo pipefail

fail() { echo "Guardrails v2 migration test: $*" >&2; exit 1; }

for removed in scripts/test-guardrails-baseline-growth.sh scripts/check-guardrails-baseline-growth.sh; do
    [[ ! -e "$removed" ]] || fail "$removed must be deleted; the v2 lock owns baseline growth"
done

if grep -qE 'test-guardrails-baseline-growth|check-guardrails-baseline-growth' scripts/verify.sh; then
    fail "verify.sh still invokes legacy baseline-growth scripts"
fi

if grep -R -nE 'AxialGuardrailsUseLegacyPolicy|AxialGuardrailsSeverity|AxialGuardrailsGenerateBaseline|GetEnvironmentVariable\(.?(AxialGuardrailsEnabled|AXIAL_GUARDRAILS_ENABLED|AxialGuardrailsSeverity|AXIAL_GUARDRAILS_SEVERITY)|CI=true' \
    Directory.Build.props Directory.Build.targets src/Axial.Guardrails/build src/Axial.Guardrails/*.fs >/dev/null; then
    fail "legacy policy, severity, baseline-generation, or environment switches remain"
fi

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
