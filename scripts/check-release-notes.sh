#!/usr/bin/env bash
set -euo pipefail

version="${1:-$(cat NEXT_VERSION)}"
notes="dev-docs/releases/$version.md"

if [ ! -f "$notes" ]; then
  echo "Missing release notes: $notes" >&2
  echo "NEXT_VERSION declares $version; add its release notes before this can ship." >&2
  exit 1
fi
