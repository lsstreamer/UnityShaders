#!/usr/bin/env bash
set +e
here=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
bash "$here/render_on_server.sh"
code=$?
printf '%s\n' "$code" > "$here/../logs/job.exit"
exit "$code"
