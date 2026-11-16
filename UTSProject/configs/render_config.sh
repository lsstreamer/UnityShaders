# 公共渲染配置。启动命令是 bash render.sh [模型名 ... | --all]。
# 路径可用容器内的绝对路径，$workspace 和 $project 由启动脚本设置。

# 输入按 <model_root>/<模型名>/<模型名>.glb 存放。
default_models=(house)
model_root="$workspace/shaderNPR/glb/val_data"
output_root="$workspace/shaderNPR/comparison/unity/UTS"
model_settings_dir="$project/configs/models"
gpu=7
unity_editor="$workspace/Unity/6000.3.24f1/Editor/Unity"
unity_lib="$workspace/Unity/runtime-ubuntu24.04/usr/lib/x86_64-linux-gnu"
vulkan_icd=/etc/vulkan/icd.d/nvidia_icd.json

frame_count=120                # 每套环绕的帧数
resolution=1024                # 正方形 PNG 的边长，像素
msaa=8                         # 1 为关闭，可选 1、2、4、8
geometry_samples=8             # 两圈共用的遮罩采样数，0 为全部，ref 始终生成遮罩
background=transparent         # transparent 或带引号的 '#RRGGBB'
