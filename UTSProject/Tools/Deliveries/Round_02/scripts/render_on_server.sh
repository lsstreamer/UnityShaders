#!/usr/bin/env bash
set -euo pipefail
docker exec --user 1010:1010 --env HOME=/tmp/shader-npr-home-1010 --env USER=sirui sirui-NPRShader bash -lc 'cd /workspace/UnityProjects/UTSProject && bash Tools/Deliveries/render_round.sh Round_02'
