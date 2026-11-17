#!/usr/bin/env bash
set -euo pipefail

project=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)
workspace=$(cd -- "$project/../.." && pwd)
source "$project/configs/render_config.sh"
if [[ -n ${SHADERNPR_UTS_CONFIG:-} ]]; then
  source "$SHADERNPR_UTS_CONFIG"
fi
gpu=${SHADERNPR_UTS_GPU:-$gpu}
frame_count=${SHADERNPR_UTS_FRAMES:-$frame_count}
output_root=${SHADERNPR_UTS_OUTPUT_ROOT:-$output_root}

die() { printf '%s\n' "$*" >&2; exit 1; }

if [[ ${1:-} == -h || ${1:-} == --help ]]; then
  printf 'Usage: %s [model ... | --all]\nDefault models: %s\nShared settings: %s/configs/render_config.sh\nModel settings: %s/<model>/{camera,light,shader}.sh\n' "$0" "${default_models[*]}" "$project" "$model_settings_dir"
  printf 'Project scene settings: %s/<model>/scene.sh\n' "$model_settings_dir"
  printf 'Sample settings: %s/<model>/{scene,camera,light}.sh\n' "$sample_settings_dir"
  exit 0
fi
(( EUID != 0 )) || die 'Run as the ordinary user that activated Unity.'
[[ $gpu =~ ^[0-9]+$ ]] || die "Invalid Vulkan device index: $gpu"
[[ $frame_count =~ ^[1-9][0-9]*$ ]] || die "Invalid orbit frame count: $frame_count"

[[ -x $unity_editor ]] || die "Unity executable not found: $unity_editor. Check configs/render_config.sh and run inside the GPU container."
tty=0
[[ -t 1 ]] && tty=1

# Read each line as it arrives. mawk can buffer pipe input even with fflush().
show_progress() {
  local line='' progress phase='' next_phase active=0
  while IFS= read -r line || [[ -n $line ]]; do
    case "$line" in
      '[RenderProgress] '*)
        progress=${line#'[RenderProgress] '}
        if (( tty )); then
          next_phase=${progress%% *}
          if (( active )) && [[ $next_phase != "$phase" ]]; then printf '\n'; fi
          phase=$next_phase
          printf '\r%s' "$progress"
          active=1
        else
          printf '%s\n' "$progress"
        fi
        ;;
      '[UtsRender]'*|'[OrbitRenderMatch] Completed:'*)
        if (( active )); then printf '\n'; active=0; fi
        printf '%s\n' "$line"
        ;;
    esac
  done
  if (( active )); then printf '\n'; fi
}

models=("$@")
if (( $# == 0 )); then
  models=("${default_models[@]}")
elif [[ $1 == --all && $# == 1 ]]; then
  shopt -s nullglob
  models=()
  for folder in "$model_settings_dir"/*/ "$sample_settings_dir"/*/; do
    folder=${folder%/}
    name=${folder##*/}
    models+=("$name")
  done
  (( ${#models[@]} )) || die "No settings in $model_settings_dir or $sample_settings_dir"
fi
(( ${#models[@]} )) || die 'Set default_models or pass one or more model names.'

for model in "${models[@]}"; do
  [[ $model =~ ^[[:alnum:]_][[:alnum:]_-]*$ ]] || die "Invalid model name: $model"
  if [[ -d $sample_settings_dir/$model ]]; then
    [[ ! -d $model_settings_dir/$model ]] || die "$model has both sample and GLB settings"
    [[ -r $sample_settings_dir/$model/scene.sh ]] || die "$model: missing sample scene.sh"
  elif [[ ! -f $model_settings_dir/$model/scene.sh ]]; then
    input="$model_root/$model/$model.glb"
    [[ -r $input ]] || die "Missing input: $input"
  fi
done

exec 9>"$project/.render.lock"
flock -n 9 || die "Another UTS render is using $project"
logs=$(mktemp -d "${TMPDIR:-/tmp}/shadernpr-uts.XXXXXXXX")
export XDG_RUNTIME_DIR="$logs"
export LD_LIBRARY_PATH="$unity_lib${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}"
export VK_DRIVER_FILES="$vulkan_icd"
printf 'Unity logs (in container): %s\n' "$logs"
render_args=(
  -matchFrameCount "$frame_count" -matchResolution "$resolution" -matchMsaa "$msaa"
  -matchGeometrySamples "$geometry_samples" -matchBackground "$background"
)

for index in "${!models[@]}"; do (
  model=${models[index]}
  log="$logs/$model.log"
  defaults="$project/configs/defaults"
  model_settings="$model_settings_dir/$model"
  if [[ -d $sample_settings_dir/$model ]]; then
    model_settings="$sample_settings_dir/$model"
  fi
  render_refs_only=false
  material_overrides=()
  for section in camera light shader; do
    source "$defaults/$section.sh"
    if [[ -f $model_settings/$section.sh ]]; then
      source "$model_settings/$section.sh"
    fi
  done
  model_render_args=()
  case "$render_refs_only" in
    true) model_render_args+=(-matchRefsOnly) ;;
    false) ;;
    *) die "$model: render_refs_only must be true or false." ;;
  esac
  if [[ $model_settings == "$sample_settings_dir/$model" ]]; then
    source "$model_settings/scene.sh"
    input_args=(
      -utsSampleScene "$sample_scene" -matchModel "$sample_model" -matchLight "$scene_light"
    )
    if [[ -n ${sample_dependencies:-} ]]; then input_args+=(-utsSampleDependencies "$sample_dependencies"); fi
    if [[ -n ${sample_hide:-} ]]; then input_args+=(-utsSampleHide "$sample_hide"); fi
    if [[ -n ${sample_pose_clip:-} ]]; then
      input_args+=(-utsSamplePose "$sample_pose_clip" -utsSamplePoseTime "${sample_pose_time:-0}")
    fi
    if [[ -n ${scene_additional_lights:-} ]]; then
      input_args+=(-utsAdditionalLights "$scene_additional_lights")
    fi
  elif [[ -f $model_settings/scene.sh ]]; then
    source "$model_settings/scene.sh"
    [[ $unity_scene == Assets/*.unity && -r $project/$unity_scene ]] || die "$model: missing project scene $unity_scene"
    input_args=(
      -utsProjectScene "$unity_scene" -matchModel "$scene_model" -matchLight "$scene_light"
      -matchRefCameras "$scene_ref_cameras"
    )
    if [[ -n ${scene_shadow_distance:-} ]]; then
      input_args+=(-utsSceneShadowDistance "$scene_shadow_distance")
    fi
  else
    input_args=(
      -matchGlb "$model_root/$model/$model.glb"
      -utsLightPitch "$light_pitch" -utsLightYaw "$light_yaw" -utsLightIntensity "$light_intensity" -utsLightColor "$light_color"
      -utsBaseStep "$toon_base_step" -utsShadeStep "$toon_shade_step"
      -utsBaseFeather "$toon_base_feather" -utsShadeFeather "$toon_shade_feather" -utsOutlineWidth "$outline_width"
    )
  fi
  printf '\n[%d/%d] %s | GPU %s | %s\n' "$((index + 1))" "${#models[@]}" "$model" "$gpu" "$output_root/$model"
  if [[ -d $model_settings ]]; then
    printf 'Model settings: %s\n' "$model_settings"
  else
    printf 'Model settings: %s (defaults)\n' "$defaults"
  fi
  if [[ $render_refs_only == true ]]; then printf 'Render mode: refs only\n'; fi
  printf 'Starting Unity and loading the project...\n'
  if xvfb-run -a -s '-screen 0 1024x768x24 -nolisten tcp' \
    "$unity_editor" -batchmode -quit -force-vulkan -force-device-index "$gpu" \
    -projectPath "$project" -executeMethod UtsRender.Run \
    "${input_args[@]}" \
    -matchAxes '(-x,+y,+z)' "${render_args[@]}" \
    -matchRefAzimuths "$reference_azimuths" -matchRefElevation "$reference_elevation" -matchFrameFill "$frame_fill" \
    -matchOrbitDirection "$orbit_direction" \
    "${model_render_args[@]}" "${material_overrides[@]}" \
    -matchOutput "$output_root/$model" -logFile - 2>&1 | tee "$log" | show_progress; then
    exit 0
  else
    rc=$?
    printf 'Unity failed for %s (exit %s). Log: %s\n' "$model" "$rc" "$log" >&2
    tail -n 35 "$log" >&2 || true
    exit "$rc"
  fi
); done
