#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

while IFS= read -r source_page; do
  page_name="$(basename "$source_page")"
  if [[ ! "$page_name" =~ ^[0-9][0-9]- ]]; then
    echo "Documentation page lacks a numeric ordering prefix: ${source_page#"$root_dir/"}" >&2
    exit 1
  fi
done < <(find "$root_dir"/docs/[0-9][0-9]-* -mindepth 1 -type f -name '*.md' ! -name '_index.md' | sort)

while IFS= read -r source_folder; do
  folder_name="$(basename "$source_folder")"
  if [[ ! "$folder_name" =~ ^[0-9][0-9]- ]]; then
    echo "Nested documentation folder lacks a numeric ordering prefix: ${source_folder#"$root_dir/"}" >&2
    exit 1
  fi
done < <(find "$root_dir"/docs/[0-9][0-9]-* -mindepth 1 -type d | sort)

if rg -n '^weight:' "$root_dir/docs" --glob '*.md'; then
  echo "Documentation ordering must use numeric file and folder prefixes, not frontmatter weights." >&2
  exit 1
fi
