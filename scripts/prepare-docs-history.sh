#!/usr/bin/env bash

set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
manifest="$root_dir/.livedocs/history-manifest.json"
history_root="${1:-$root_dir/.livedocs/build-history}"
local_manifest="$history_root/manifest.json"
models_dir="$history_root/models"
sources_dir="$history_root/sources"

mkdir -p "$models_dir" "$sources_dir"
jq -n '{schemaVersion: 1, currentVersion: "", entries: []}' > "$local_manifest"

entry_total="$(jq '.entries | length' "$manifest")"
for ((index = 0; index < entry_total; index++)); do
  version="$(jq -r ".entries[$index].version" "$manifest")"
  tag="$(jq -r ".entries[$index].tag" "$manifest")"
  model_asset="$(jq -r ".entries[$index].modelAsset" "$manifest")"
  checksum_asset="$(jq -r ".entries[$index].checksumAsset" "$manifest")"
  version_models="$models_dir/$version"
  version_source="$sources_dir/$version"

  mkdir -p "$version_models" "$version_source"
  gh release download "$tag" --pattern "$model_asset" --pattern "$checksum_asset" --dir "$version_models"
  (cd "$version_models" && sha256sum --check "$checksum_asset")
  gh release verify-asset "$tag" "$version_models/$model_asset"

  git archive "$tag" | tar -x -C "$version_source"

  model_sha256="$(sha256sum "$version_models/$model_asset" | cut -d' ' -f1)"
  next_manifest="$local_manifest.next"
  jq \
    --arg version "$version" \
    --arg model_path "models/$version/$model_asset" \
    --arg model_sha256 "$model_sha256" \
    --arg docs_path "sources/$version/docs" \
    '.entries += [{version: $version, modelPath: $model_path, modelSha256: $model_sha256, docsPath: $docs_path}]' \
    "$local_manifest" > "$next_manifest"
  mv "$next_manifest" "$local_manifest"
done

current_version="${GITHUB_REF_NAME#v}"
jq --arg current_version "$current_version" '.currentVersion = $current_version' "$local_manifest" > "$local_manifest.next"
mv "$local_manifest.next" "$local_manifest"

echo "$local_manifest"
