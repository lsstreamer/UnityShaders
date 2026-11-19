#!/usr/bin/env bash
set -euo pipefail
project="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
workspace="$(cd -- "$project/../.." && pwd)"
editor="${URPTOON_EDITOR:-$workspace/Unity/6000.3.24f1/Editor/Unity}"
runtime_lib="$workspace/Unity/runtime-ubuntu24.04/usr/lib/x86_64-linux-gnu"
output="${URPTOON_OUTPUT:-$project/Comparison/Round2}"
stage="$(mktemp -d /tmp/urp-toon-render.XXXXXXXX)"
trap 'rm -rf -- "$stage"' EXIT
mkdir -p "$stage/runtime"
mkdir -p "$project/Logs"
chmod 700 "$stage/runtime"
export XDG_RUNTIME_DIR="$stage/runtime"
export LD_LIBRARY_PATH="$runtime_lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
export VK_DRIVER_FILES="${VK_DRIVER_FILES:-/etc/vulkan/icd.d/nvidia_icd.json}"
log="$project/Logs/comparison.log"
printf 'Project: %s\nOutput: %s\nLog: %s\n' "$project" "$output" "$log"
render_args=()
if [[ -n "${URPTOON_NAMES:-}" ]]; then
  render_args+=(-comparisonNames "$URPTOON_NAMES")
fi
xvfb-run -a -s '-screen 0 1024x768x24 -nolisten tcp' "$editor" \
  -batchmode -quit -force-vulkan -force-device-index "${URPTOON_GPU:-0}" \
  -projectPath "$project" -executeMethod RenderToonComparisons.Run \
  -comparisonOutput "$output" "${render_args[@]}" -logFile "$log"
python3 "$project/compose_comparisons.py" "$output"
