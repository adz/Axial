#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
fixture_dir="$root_dir/tests/compile-fail/schema-ce"

dotnet build "$root_dir/src/Axial.Schema/Axial.Schema.fsproj" --nologo -v quiet

check_failure() {
  local fixture="$1"
  local expected="$2"
  local output

  if output="$(dotnet fsi --exec "$fixture_dir/$fixture" 2>&1)"; then
    echo "$fixture compiled, but it must be rejected." >&2
    exit 1
  fi

  if ! grep -Fq "$expected" <<<"$output"; then
    echo "$fixture did not report the expected compiler diagnostic:" >&2
    echo "  $expected" >&2
    echo "$output" >&2
    exit 1
  fi
}

check_failure raw-field-without-refine.fsx 'A field block must finish with the getter type'
check_failure missing-refinement.fsx 'static member Refinement'
check_failure constraint-after-refine.fsx "No overloads match for method 'Constrain'"
check_failure validation-at-wrong-stage.fsx "No overloads match for method 'Validate'"
check_failure constructor-mismatch.fsx "The type 'int' does not match the type 'string'"
check_failure ambiguous-refinement.fsx 'Duplicate method'

echo "Schema CE invalid transitions and ambiguous refinements produce compile-time errors."
