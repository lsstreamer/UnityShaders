# UTSProject 渲染使用说明

本工程用 Unity 6000.3.24f1、Built-in 渲染管线和 Vulkan 从 GLB 生成参考图、两套环绕、几何遮罩及相机 JSON。批处理会在临时场景中导入 GLB，根据模型边界自动摆放相机，并使用模型配置中的灯光和材质。

## 启动渲染

通过 SSH 登录服务器宿主机，用已激活 Unity 的普通用户进入 GPU 容器。

```bash
bash /home/sirui/workspace/sh/run.sh
```

在容器内启动 `house`。

```bash
cd /workspace/UnityProjects/UTSProject
bash render.sh house
```

| 命令 | 用途 |
| --- | --- |
| `bash render.sh` | 渲染公共配置中的 `default_models`，当前是 `house`。 |
| `bash render.sh house` | 渲染指定模型。可传入多个模型名，按顺序执行。 |
| `bash render.sh --all` | 渲染 `configs/models/` 中有配置目录的模型。 |
| `bash render.sh --help` | 查看启动用法和配置位置。 |

[render.sh](render.sh) 负责加载配置并启动 Unity。Unity 的批处理入口是 `UtsRender.Run`，负责导入模型、设置材质、相机和灯光。`OrbitRenderMatch` 负责渲染和导出。[UtsRender.cs](Assets/Editor/UtsRender.cs) · [OrbitRenderMatch.cs](Assets/Editor/OrbitRenderMatch.cs)

## 输入和输出

| 项目 | 容器内默认路径 |
| --- | --- |
| GLB 根目录 | `/workspace/shaderNPR/glb/val_data` |
| `house` 的 GLB | `/workspace/shaderNPR/glb/val_data/house/house.glb` |
| 渲染输出根目录 | `/workspace/shaderNPR/comparison/unity/UTS` |
| `house` 的渲染结果 | `/workspace/shaderNPR/comparison/unity/UTS/house` |
| 完整日志 | 启动时显示的 `/tmp/shadernpr-uts.XXXXXXXX/house.log` |

输入按 `<model_root>/<模型名>/<模型名>.glb` 放置，批处理以 GLB 文件为单位导入。导出模型时把需要的贴图嵌入 GLB。完整渲染完成后，模型输出目录包含以下文件。

```text
house/
├── ref/
│   ├── view0.png                       # 第一台参考相机的成图
│   ├── view1.png                       # 第二台参考相机的成图
│   └── cameras.json                    # 各 ref 的姿态和投影参数
├── bounding_sphere/frames/             # 相机和灯光同步环绕
├── camera_only/frames/                 # 相机环绕，灯光固定
├── geometry/
│   ├── orbit/                          # 两套环绕共用的几何遮罩及 geometry.json
│   └── optimization_views/             # 每个参考视角的几何遮罩及 geometry.json
├── cameras.json                        # 两套环绕的逐帧相机、灯光方向和采样记录
└── unity_render.json                   # 本次运行的分辨率、帧数和完成状态
```

终端的 `Refs`、`Frames` 和 `Masks` 分别显示参考图、环绕成图和环绕遮罩的进度。运行结束时，`Completed` 会汇总实际保存的数量。

## 公共渲染设置

在公共配置里设置所有模型共用的输入、输出、设备和成图参数。[render_config.sh](configs/render_config.sh)

| 配置 | 当前值或用途 |
| --- | --- |
| `model_root`、`output_root` | GLB 输入根目录和渲染输出根目录。 |
| `default_models`、`model_settings_dir` | 默认模型列表和模型配置目录。 |
| `gpu` | Vulkan 设备编号，当前是 `7`。 |
| `frame_count` | 每套环绕的帧数，默认 `120`。完整运行共生成 `240` 张环绕成图。 |
| `resolution` | 正方形 PNG 的边长，默认 `1024`。 |
| `msaa` | 默认 `8`，可选 `1`、`2`、`4`、`8`。`1` 表示关闭。 |
| `geometry_samples` | 两套环绕共用的几何采样数，默认 `8`。`0` 表示每帧都生成遮罩。每张 ref 都有自己的几何遮罩。 |
| `background` | 默认 `transparent`。需要纯色背景时填写带引号的 `'#RRGGBB'`。 |
| `unity_editor`、`unity_lib`、`vulkan_icd` | 容器环境的 Unity 可执行文件、运行库目录和 Vulkan 驱动配置。 |

临时跑一个短环绕时，可以把结果写到容器的 `/tmp`。

```bash
SHADERNPR_UTS_FRAMES=8 SHADERNPR_UTS_OUTPUT_ROOT=/tmp/uts-preview bash render.sh house
```

也可以用 `SHADERNPR_UTS_GPU` 临时指定 GPU，或用 `SHADERNPR_UTS_CONFIG=/绝对路径/override.sh` 加载一份覆盖公共设置的 Bash 文件。GPU、帧数和输出路径的环境变量覆盖优先于配置文件。

## 每个模型的设置

把模型专用配置放在 `configs/models/<模型名>/`，目录名与 GLB 文件名对应。按类别编辑 `camera.sh`、`light.sh` 和 `shader.sh`。启动时先加载该类别的默认值，再应用模型文件中的设置。可以只填写要调整的参数。[模型默认值](configs/defaults) · [house 配置](configs/models/house)

### 相机和参考图

在相机配置中调整参考视角、取景和环绕方向。[camera.sh](configs/models/house/camera.sh)

```bash
render_refs_only=false
reference_azimuths='30,210'
reference_elevation=15
frame_fill=0.9
orbit_direction=increasing
```

- `reference_azimuths` 按逗号分隔多个方位角，角度从 Unity 的 `+Z` 向 `+X` 旋转。列表顺序对应 `view0.png`、`view1.png` 等。第一个角度也决定两套环绕的起点。
- `reference_elevation` 是所有参考相机共用的仰角，单位是度，范围为 `(-90, 90)`。
- `frame_fill` 控制正交相机整圈的取景占框比例，范围为 `(0, 1]`。值越大，模型在画面里占比越大。
- `orbit_direction` 可设为 `increasing` 或 `decreasing`，对应导出的渲染坐标系中方位角递增或递减。
- 试调参考图构图或材质时，设 `render_refs_only=true` 后用同一条启动命令。参考图和相机参数写入 `ref/`，几何遮罩写入 `geometry/optimization_views/`。改回 `false` 后可运行完整环绕。

### 灯光

在灯光配置中调整主方向光。`light_pitch` 和 `light_yaw` 分别设置方向光的 X、Y 轴旋转角度，单位为度。[light.sh](configs/models/house/light.sh)

```bash
light_pitch=40
light_yaw=-30
light_intensity=1
light_color='#FFFFFF'
```

参考图和 `camera_only` 使用固定的灯光方向。`bounding_sphere` 使用同一盏灯，按相机的环绕角度同步旋转。

### UTS Shader 和分块材质

默认材质使用 Unity Toon Shader，并保留 GLB 的底色贴图和色调。在模型的 Shader 配置里调整默认 UTS 材质。[shader.sh](configs/models/house/shader.sh)

| 配置 | 作用 |
| --- | --- |
| `toon_base_step`、`toon_shade_step` | 三档明暗的两条分界，范围为 `[0, 1]`。 |
| `toon_base_feather`、`toon_shade_feather` | 两条分界的过渡宽度，范围为 `[0, 1]`。 |
| `outline_width` | UTS 描边宽度，设为 `0` 可关闭描边。 |

模型的不同节点或子网格可以使用独立材质。将准备好的 `.mat` 及其依赖的 Shader、贴图和对应的 `.meta` 文件放到工程的 `Assets/`，再在模型的 `shader.sh` 中按 **Renderer 路径、材质槽编号、材质资产路径** 绑定。自定义 Shader 需兼容 Built-in 渲染管线和 Vulkan。

例如，将屋顶材质放到 `Assets/Models/house/Materials/Roof.mat` 后，可以给 `house` 屋顶的一个材质槽增加这条绑定。

```bash
material_overrides+=(
  -utsSlotMaterial '01_Roof/Cube.1152' 0 'Assets/Models/house/Materials/Roof.mat'
)
```

Renderer 路径相对于导入的模型根节点，用 `/` 连接各级节点名。材质槽从 `0` 开始编号。同一模型可以添加多条绑定，每个槽使用指定 `.mat` 的 Shader 和参数。成功绑定后，启动日志会列出 Renderer 路径、槽位和 Shader 名称。
