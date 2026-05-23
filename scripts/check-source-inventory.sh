#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$root_dir"

tmp_dir="$(mktemp -d)"
trap 'rm -rf "$tmp_dir"' EXIT

find src tests -name '*.fsproj' -print | sort > "$tmp_dir/projects.actual"
grep -o 'Path="[^"]*\.fsproj"' FsFlow.slnx \
  | sed 's/^Path="//; s/"$//' \
  | grep -E '^(src|tests)/' \
  | sort > "$tmp_dir/projects.expected"

find src tests -name '*.fs' -print | sort > "$tmp_dir/sources.actual"

> "$tmp_dir/sources.expected"
while IFS= read -r project; do
  project_dir="$(dirname "$project")"
  grep -o '<Compile Include="[^"]*\.fs"' "$project" \
    | sed 's/^<Compile Include="//; s/"$//' \
    | while IFS= read -r include_path; do
        if [[ "$include_path" == ..* ]]; then
          realpath --relative-to="$root_dir" "$project_dir/$include_path"
        else
          printf '%s\n' "$project_dir/$include_path"
        fi
      done
done < "$tmp_dir/projects.actual" | sort -u > "$tmp_dir/sources.expected"

if ! diff -u "$tmp_dir/projects.expected" "$tmp_dir/projects.actual"; then
  echo "Source project inventory mismatch: update FsFlow.slnx or remove stale src/tests project files." >&2
  exit 1
fi

if ! diff -u "$tmp_dir/sources.actual" "$tmp_dir/sources.expected"; then
  echo "Source file inventory mismatch: every src/tests .fs file must be explicitly compiled by a src/tests project." >&2
  exit 1
fi

echo "Source inventory covers src/tests .fs and .fsproj files."
