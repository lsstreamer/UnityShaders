#!/usr/bin/env bash
set -euo pipefail
here=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
project=$(cd -- "$here/../.." && pwd)
round=${1:?Usage: bash Tools/Deliveries/render_round.sh Round_02}
[[ $round =~ ^Round_[0-9]{2,}$ ]] || { echo "Invalid round: $round" >&2; exit 2; }
list="$here/$round/models.txt"
[[ -f $list ]] || { echo "Missing model list: $list" >&2; exit 2; }
models=()
while IFS= read -r model || [[ -n $model ]]; do
  [[ -z $model || $model == \#* ]] && continue
  [[ $model =~ ^[[:alnum:]_][[:alnum:]_-]*$ ]] || { echo "Invalid model: $model" >&2; exit 2; }
  models+=("$model")
done < "$list"
(( ${#models[@]} )) || { echo 'Empty model list' >&2; exit 2; }
if [[ ${2:-} == --list ]]; then printf '%s\n' "${models[@]}"; exit 0; fi
cd "$project"
bash render.sh "${models[@]}"
sync_script="$here/$round/scripts/sync_input_references.py"
if [[ -f $sync_script ]]; then python3 "$sync_script"; fi
