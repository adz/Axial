#!/usr/bin/env bash
set -euo pipefail
root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
mkdir -p "$tmp/scripts" "$tmp/.livedocs" "$tmp/bin"
cp "$root/scripts/verify.sh" "$tmp/scripts/"
[[ ! -f "$root/scripts/restore-livedocs-projects.py" ]] || cp "$root/scripts/restore-livedocs-projects.py" "$tmp/scripts/"
printf '{"projects":["core.fsproj"],"docsSets":[{"projects":["core.fsproj","aspnet.fsproj"]}]}' > "$tmp/.livedocs/config.json"
cat > "$tmp/bin/dotnet" <<'SH'
#!/usr/bin/env bash
set -euo pipefail
if [[ $1 == restore ]]; then
  [[ ${FAIL_RESTORE:-false} != true ]] || { echo 'restore failed' >&2; exit 17; }
  echo "$2" >> restored
elif [[ $1 == livedocs ]]; then
  touch extracted
  [[ $(sort restored) == $'aspnet.fsproj\ncore.fsproj' ]]
else
  exit 18
fi
SH
chmod +x "$tmp/bin/dotnet"
git -C "$tmp" init -q
git -C "$tmp" -c user.name=Test -c user.email=test@example.invalid commit --allow-empty -qm fixture
export PATH="$tmp/bin:$PATH"
bash "$tmp/scripts/verify.sh" livedocs > "$tmp/success.log" 2>&1 || {
  echo 'livedocs must restore every configured project once before extraction' >&2
  exit 1
}
rm "$tmp/extracted" "$tmp/restored"
if FAIL_RESTORE=true bash "$tmp/scripts/verify.sh" livedocs > "$tmp/failure.log" 2>&1; then
  echo 'restore failure must fail the livedocs stage' >&2
  exit 1
fi
[[ ! -f "$tmp/extracted" ]] || { echo 'extraction ran after restore failure' >&2; exit 1; }
grep -q 'restore failed' "$tmp/artifacts/verify/livedocs.log"
echo 'livedocs restore regression tests passed'
