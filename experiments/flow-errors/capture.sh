#!/usr/bin/env bash
set -euo pipefail
root=$(cd "$(dirname "$0")/../.." && pwd)
cd "$root/experiments/flow-errors/snippets"
variant=${1:-baseline}
dotnet=${DOTNET:-dotnet}
out="$root/experiments/flow-errors/golden/$variant"
mkdir -p "$out"
normalize() {
  python3 - "$root" "$1" <<'PY'
import pathlib, sys
root, output = sys.argv[1:]
path = pathlib.Path(output)
# The shared build-lock wrapper reports queueing, not an F# compiler diagnostic.
text = ''.join(line for line in path.read_text().splitlines(keepends=True) if not line.startswith('[dotnet-lock]'))
path.write_text(text.replace(root, '<ROOT>'))
PY
}
# Clock access is runtime-scoped now; a missing clock interface is no longer a compiler error.
for source in missing-service wrong-error non-flow mixed-envs envwith-missing-member forgot-return underconstrained-async; do
  if "$dotnet" fsi --nologo --exec "$source.fsx" >"$out/$source.txt" 2>&1; then
    echo "ERROR: $source unexpectedly compiled" >&2
    exit 1
  fi
  normalize "$out/$source.txt"
done
successful_scripts=(for-loops)
[[ "$variant" == "witness" ]] && successful_scripts+=(user-source)
for successful in "${successful_scripts[@]}"; do
  "$dotnet" fsi --nologo --exec "$successful.fsx" >"$out/$successful.txt" 2>&1
  normalize "$out/$successful.txt"
done
