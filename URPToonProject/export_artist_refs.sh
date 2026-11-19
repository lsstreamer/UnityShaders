#!/usr/bin/env bash
set -euo pipefail
project="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
workspace="$(cd -- "$project/../.." && pwd)"
editor="${URPTOON_EDITOR:-$workspace/Unity/6000.3.24f1/Editor/Unity}"
runtime_lib="$workspace/Unity/runtime-ubuntu24.04/usr/lib/x86_64-linux-gnu"
stage="$(mktemp -d /tmp/urp-toon-artist.XXXXXXXX)"
trap 'rm -rf -- "$stage"' EXIT
mkdir -p "$stage/runtime" "$project/Logs"
chmod 700 "$stage/runtime"
export XDG_RUNTIME_DIR="$stage/runtime"
export LD_LIBRARY_PATH="$runtime_lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
export VK_DRIVER_FILES="${VK_DRIVER_FILES:-/etc/vulkan/icd.d/nvidia_icd.json}"
args=()
if [[ -n "${URPTOON_NAMES:-}" ]]; then args+=(-artistNames "$URPTOON_NAMES"); fi
xvfb-run -a -s '-screen 0 1024x768x24 -nolisten tcp' "$editor" \
  -batchmode -quit -force-vulkan -force-device-index "${URPTOON_GPU:-0}" \
  -projectPath "$project" -executeMethod ExportToonArtistReferences.Run \
  -artistRefs "${URPTOON_REFS:-$workspace/shaderNPR/comparison/unity/artist}" \
  -artistGlbs "${URPTOON_GLBS:-$workspace/shaderNPR/glb/artist}" \
  "${args[@]}" -logFile "$project/Logs/artist_refs.log"
