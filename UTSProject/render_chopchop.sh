#!/usr/bin/env bash
set -euo pipefail
project="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
workspace="$(cd -- "$project/../.." && pwd)"
source "$project/configs/render_config.sh"
stage="$(mktemp -d /tmp/chopchop-render.XXXXXXXX)"
mkdir -p "$stage/runtime"
chmod 700 "$stage/runtime"
export XDG_RUNTIME_DIR="$stage/runtime"
export LD_LIBRARY_PATH="$unity_lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
export VK_DRIVER_FILES="$vulkan_icd"
output="${CHOPCHOP_OUTPUT:-$project/Comparison/ChopChop}"
render_method=ChopChopPreview.Run
render_args=()
if [[ "${CHOPCHOP_REFS_ONLY:-0}" == 1 || "${CHOPCHOP_GROUND_TRUTH:-0}" == 1 ]]; then
  render_method=ChopChopPreview.RunReferences
  render_args+=(
    -chopLightIntensity "${CHOPCHOP_LIGHT_INTENSITY:-1.6}"
    -chopAmbientIntensity "${CHOPCHOP_AMBIENT_INTENSITY:-0.2}"
  )
  if [[ "${CHOPCHOP_REFS_ONLY:-0}" == 1 ]]; then
    render_args+=(-matchRefsOnly)
  else
    render_args+=(-matchFrameCount "${CHOPCHOP_FRAME_COUNT:-120}")
  fi
  if [[ -n "${CHOPCHOP_NAMES:-}" ]]; then
    render_args+=(-chopNames "$CHOPCHOP_NAMES")
  fi
fi
printf 'Chop Chop output: %s\nLog: %s\n' "$output" "$stage/render.log"
xvfb-run -a -s '-screen 0 1024x768x24 -nolisten tcp' "$unity_editor" \
  -batchmode -quit -force-vulkan -force-device-index "${CHOPCHOP_GPU:-$gpu}" \
  -projectPath "$project" -executeMethod "$render_method" \
  -chopOutput "$output" "${render_args[@]}" -logFile "$stage/render.log"
