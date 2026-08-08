#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
version="${1:?Usage: $0 <version> [model-path] [checksum-path]}"
model_path="${2:-}"
checksum_path="${3:-}"
manifest="$root_dir/.livedocs/history-manifest.json"

jq -e '.schemaVersion == 1' "$manifest" >/dev/null

entry_count="$(jq --arg version "$version" '[.entries[] | select(.version == $version)] | length' "$manifest")"
if [ "$entry_count" -ne 1 ]; then
  echo "History manifest must contain exactly one entry for $version." >&2
  exit 1
fi

jq -e --arg version "$version" '
  .entries[]
  | select(.version == $version)
  | .tag == ("v" + $version)
    and .modelSchemaVersion == 1
    and (.modelAsset | length > 0)
    and (.checksumAsset | length > 0)
' "$manifest" >/dev/null

if [ -n "$model_path" ] || [ -n "$checksum_path" ]; then
  test -f "$model_path"
  test -f "$checksum_path"
  jq -e --arg version "$version" '.SchemaVersion == 1 and .Package.Version == $version' "$model_path" >/dev/null
  (cd "$(dirname "$model_path")" && sha256sum --check "$(basename "$checksum_path")")
fi
