#!/usr/bin/env bash
set -euo pipefail
docker exec --user 1010:1010 --env HOME=/tmp/shader-npr-home-1010 --env USER=sirui sirui-NPRShader bash -lc '
set -euo pipefail
project=/workspace/UnityProjects/UTSProject
workspace=/workspace
cd "$project"
source configs/render_config.sh
export LD_LIBRARY_PATH="$unity_lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
export VK_DRIVER_FILES="$vulkan_icd"
export XDG_RUNTIME_DIR=/tmp/round02-rename-1010
mkdir -p "$XDG_RUNTIME_DIR"
chmod 700 "$XDG_RUNTIME_DIR"
exec 8>.render.lock
flock -n 8
xvfb-run -a -s "-screen 0 1024x768x24 -nolisten tcp" "$unity_editor" -batchmode -quit -force-vulkan -force-device-index "$gpu" -projectPath "$project" -executeMethod Round02Rename.Run -logFile "$project/Tools/Deliveries/Round_02/logs/rename.log"
'
