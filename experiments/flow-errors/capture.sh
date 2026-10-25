#!/usr/bin/env bash
set -euo pipefail
root=$(cd "$(dirname "$0")/../.." && pwd)
cd "$root/experiments/flow-errors/snippets"
variant=${1:-baseline}
dotnet=${DOTNET:-dotnet}
out="$root/experiments/flow-errors/golden/$variant"
mkdir -p "$out"
for source in missing-clock missing-service wrong-error non-flow mixed-envs envwith-missing-member forgot-return; do
  if "$dotnet" fsi --nologo --exec "$source.fsx" >"$out/$source.txt" 2>&1; then
    echo "ERROR: $source unexpectedly compiled" >&2
    exit 1
  fi
  python3 - "$root" "$out/$source.txt" <<'PY'
import pathlib, sys
root, output = sys.argv[1:]
path = pathlib.Path(output)
path.write_text(path.read_text().replace(root, "<ROOT>"))
PY
done
successful_scripts=(for-loops)
[[ "$variant" == "witness" ]] && successful_scripts+=(user-source)
for successful in "${successful_scripts[@]}"; do
  "$dotnet" fsi --nologo --exec "$successful.fsx" >"$out/$successful.txt" 2>&1
  python3 - "$root" "$out/$successful.txt" <<'PY'
import pathlib, sys
root, output = sys.argv[1:]
path = pathlib.Path(output)
path.write_text(path.read_text().replace(root, "<ROOT>"))
PY
done
