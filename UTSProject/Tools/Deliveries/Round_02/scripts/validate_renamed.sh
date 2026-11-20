#!/usr/bin/env bash
set +e
docker exec --user 1010:1010 --env HOME=/tmp/shader-npr-home-1010 --env USER=sirui --env SHADERNPR_UTS_CONFIG=/workspace/UnityProjects/UTSProject/Tools/Deliveries/Round_02/scripts/rename_refs_config.sh sirui-NPRShader bash -lc 'cd /workspace/UnityProjects/UTSProject && bash Tools/Deliveries/render_round.sh Round_02'
code=$?
printf '%s\n' "$code" > /home/sirui/workspace/UnityProjects/UTSProject/Tools/Deliveries/Round_02/logs/rename_render.exit
exit "$code"
