#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ]; then
  echo "usage: scripts/query-docs-api.sh <name-or-entity-id>" >&2
  exit 2
fi

query=$1
matches=$(find output -path '*/api/*.html' -type f -printf '%P\n' | grep -i -- "$query" || true)

if [ -z "$matches" ]; then
  echo "No generated API page matches '$query'. Run 'dotnet livedocs build --warn-as-error' first." >&2
  exit 1
fi

printf '%s\n' "$matches"
