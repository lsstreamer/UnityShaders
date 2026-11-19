# URP Toon 独立工程

2026-11-19 已清理与 artist 输出无关的示例场景、闲置素材和首轮预览场景。当前保留 9 个角色的 `Assets/ComparisonTools/Scenes/Round2/`、生成这些预览所需的 6 个原场景，以及原材质、动画、贴图和导出工具。下文关于完整上游场景和首轮预览场景的描述属于清理前记录；已有 Comparison 图片与参数文件仍保留。详见 [素材清理记录](../ASSET-CLEANUP.md)。

使用现有 **Unity 6000.3.24f1 / URP 17.3.0**，无需安装其他 Unity 版本。工程设置来自该编辑器自带的 Universal 3D 模板，模型、贴图、材质、Shader 和 16 个原示例场景来自 [Delt06/urp-toon-shader](https://github.com/Delt06/urp-toon-shader)。

上游固定提交：`6981f7dc931b53a0bd797725f4dac30b824041d7`。上游声明环境为 Unity 2021.3 / URP 12.1.6；原说明和许可证分别保存在 `README.upstream.md`、`LICENSE.upstream.md`，第三方模型继续遵循各自许可。

## 第二轮：Arisa 换姿势，其余切换两段明暗

当前默认输出为 `Comparison/Round2/`，汇总大图是 `Comparison/Round2/all_characters_comparison.png`，快速预览为同目录的 `all_characters_preview.jpg`。包含 9 个主示例角色，排除首轮右下角的白色参考机器人，保留 Mike 机甲。

第二轮大图为 **6240×3384**（三列三行），包含 18 张 1024×1024 原图。已在现有 Unity `6000.3.24f1` / URP `17.3.0` 下完成实际渲染，检查全部原图并目视确认新姿态和大图。

- Arisa 改用工程自带男性 Idle 的 Humanoid 重定向，取 1.2 秒，替换原先交叉双腿的女性 Idle；保留全部原材质参数。
- 其他 8 个角色只关闭 `_RampTriple` / `_RAMP_TRIPLE`，改用两段漫反射；原阈值、平滑宽度、阴影颜色、高光、法线、毛发、姿态、灯光和相机不作额外调整。Crazy Cat 本来就是两段，保持其设置。
- 使用单独保存的材质副本：`Assets/ComparisonTools/Materials/Round2/`。原资源材质不覆盖；预览场景保存到 `Assets/ComparisonTools/Scenes/Round2/`。
- 每组左边为原贴图和颜色的 Base Color，右边 Arisa 标记 `Original Toon Shader`，其他角色标记 `Two-band Toon Shader`。为兼容已有文件名，右图仍名为 `original_shader.png`，其实际配置见 `settings.json` 的 `twoBand` 和 `materials` 字段。

首轮结果继续保存在 `Comparison/`，两轮可以直接对照。

与首轮核对：除 Arisa 外，其他 8 个角色的动画、采样时刻、相机、灯光、环境设置均相同，Base Color PNG 逐字节一致；两段材质除三段开关外的实际属性均与原材质一致。64 个原资源 `.mat` 文件的 SHA-256 全部保持不变。

## 首轮全部角色的对比大图

| 模型 | 原示例场景 | 输出目录 |
| --- | --- | --- |
| Arisa 动漫角色（15 份原材质） | `Assets/Scenes/SceneArisa.unity` | `Comparison/Arisa/` |
| Warrior 战士（1 份原材质） | `Assets/Scenes/Warrior.unity` | `Comparison/Warrior/` |
| Mike 机甲 | `Assets/Scenes/SampleScene.unity` | `Comparison/Mike/` |
| Crazy Cat | `Assets/Scenes/Fur.unity` | `Comparison/CrazyCat/` |
| Supercyan 男性 | `Assets/Scenes/Forest.unity` | `Comparison/SupercyanMale/` |
| 城市女性 | `Assets/Scenes/TinyToonyCityDemo.unity` | `Comparison/CityFemale/` |
| 城市男性 A | 同上 | `Comparison/CityMaleA/` |
| 城市男性 B | 同上 | `Comparison/CityMaleB/` |
| 城市警察 | 同上 | `Comparison/CityPolice/` |
| 跳舞参考机器人 | `Assets/Graphics/Animations/Robot Hip Hop Dance.fbx`，使用 SampleScene 灯光 | `Comparison/ReferenceRobot/` |

每个目录包含：

- `basecolor.png`：1024×1024，无光照的原始底色。
- `original_shader.png`：1024×1024，使用模型原本绑定的 DELTation Toon Shader 和材质参数。
- `comparison.png`：带标签的左右对比，左为 Base Color，右为原 Toon Shader。
- `settings.json`：Unity/URP 版本、原始提交、材质路径与完整参数、贴图、相机、灯光和渲染配置。

`Comparison/all_characters_comparison.png` 是全部 10 个角色的大图（4168×5624，两列五行），每个角色左边为 Base Color、右边为原 Shader。`Comparison/all_characters_preview.jpg` 是便于快速浏览的缩小版。`Comparison/two_models_comparison.png` 保留前两个角色的汇总入口，内容同步更新。`Comparison/image_validation.json` 保存图像检查结果。

按不同角色模型去重，包含 9 个原示例角色，以及动画 FBX 内额外附带的参考机器人；动画片段、重复场景实例、头发测试网格和道具不计作额外角色。参考机器人保留导入后的 URP Lit 材质，图中单独标注，其余角色使用原 Toon 或毛发 Shader。完整模型/动画清单见 `Comparison/character_inventory.json`。

每组使用完全相同的模型姿态、正交相机和构图，内部 2048×2048、8x MSAA 后缩小到 1024×1024。沿用各模型原场景的灯光方向、颜色和强度。为便于看清单个模型，重新取景，使用浅灰背景，隐藏非模型物体，关闭雾、Volume 和后处理。

有兼容动画时采样项目自带动画并冻结：Mike 使用自身 Idle，Crazy Cat 使用自身 Normal Stance，人形角色使用工程内男女 Idle 的 Humanoid 重定向，参考机器人使用自身 Dance。没有兼容动画时保留原姿态（包括 T-pose），不手动调整骨骼。各角色实际使用的动画来源、名称和时刻记录在 `settings.json` 的 `pose` 字段中，采样控制器保存在 `Assets/ComparisonTools/Poses/`。

Base Color 临时使用专门的 Unlit Shader：计算原材质的 `_BaseMap × _BaseColor`，保留 UV 缩放/偏移、启用的 RGB 顶点色、透明混合、Alpha Clip、剔除和深度写入。它不包含明暗、阴影、高光、边缘光、自发光、毛发外扩或后处理。原 Shader 成图直接使用原材质，不统一覆盖明暗阈值或颜色；脚本检查材质实际属性和有效关键词在两次渲染之间保持不变。URP Lit 使用 Unity 原生的材质关键词初始化，避免将导入过程中的默认关键词更新误判为参数改动。

## 重新渲染第二轮

从宿主机进入现有 GPU 容器（普通用户）：

```bash
bash /home/sirui/workspace/sh/run.sh
```

在容器内运行：

```bash
cd /workspace/UnityProjects/URPToonProject
bash render_comparisons.sh
```

默认使用 GPU 0，输出到 `Comparison/Round2/`。可通过 `URPTOON_GPU=7` 指定设备、`URPTOON_OUTPUT=/绝对路径` 指定输出位置。完整 Unity 日志保存在 `Logs/comparison.log`，启动时显示路径。并排图生成使用容器中已有的 Python/Pillow。

编辑器中也可以执行 `Tools > URP Toon > Render all character comparisons` 生成单张原图和参数记录；随后执行 `python3 compose_comparisons.py Comparison/Round2` 生成并排图。

默认重新渲染全部角色。需要重渲染个别角色时，可使用 `URPTOON_NAMES=Mike,CityPolice bash render_comparisons.sh`；其余角色复用已有结果后重新拼图。

可直接打开 `Assets/ComparisonTools/Scenes/Round2/Arisa.unity` 等场景查看第二轮设置；首轮预览保存在其父目录。原始场景仍保留在 `Assets/Scenes/`。

## 导出 artist 渲染与 Base Color GLB

第二轮全部 9 个角色已经导出到工作区内的 `shaderNPR/comparison/unity/artist/<角色名>/` 和 `shaderNPR/glb/artist/<角色名>/`，目录名为 `Arisa`、`Warrior`、`Mike`、`CrazyCat`、`SupercyanMale`、`CityFemale`、`CityMaleA`、`CityMaleB`、`CityPolice`。

- `comparison` 目录通过 `OrbitRenderMatch` 生成 `ref/view0.png`、`ref/view1.png`、`ref/cameras.json` 和 `geometry/optimization_views/` 中的几何遮罩。`bounding_sphere/frames/` 和 `camera_only/frames/` 各有 120 帧正交环绕图，从 `view0` 起步。前者让场景灯光和相机同步环绕，后者固定灯光。两套渲染共用 `geometry/orbit/` 中的 8 张几何遮罩，根目录的 `cameras.json` 记录逐帧相机和主灯方向。`settings.json` 保存第二轮配置。
- `glb` 目录每个角色包含 `<角色名>.glb`、`view0.png`、`view1.png`、`cameras.json`。两张 view 是原 Toon Shader 的 ref，和 comparison 中逐字节一致；GLB 是同姿态的静态 Base Color 网格，嵌入底色贴图，保留颜色、UV 变换、透明/裁切和双面设置。毛发保留基础网格和底色，不重复导出 Shader 的外扩毛发层。
- 两台相机均为正交。view0 沿用第二轮认可的相机，view1 绕模型中心水平旋转 180°，保持俯仰与正交尺寸。ref 为透明背景、1024×1024，内部 2 倍超采样、8x MSAA；角色姿态、材质、场景灯光均读取第二轮快照。
- 坐标映射沿用 `(-x,+y,+z)` 与现有归一化规则；GLB 同级 JSON 保留 Python renderer 相机矩阵，移除仅在 comparison 目录中有效的 geometry 相对路径。

在现有 GPU 容器内重跑：

```bash
cd /workspace/UnityProjects/URPToonProject
bash export_artist_refs.sh
python3 verify_artist_refs.py
```

通过 `URPTOON_NAMES=Arisa,Warrior` 选择部分角色；`URPTOON_REFS`、`URPTOON_GLBS` 可指定输出根目录。日志为 `Logs/artist_refs.log`。验证脚本检查全部 9 个 GLB、18 张 view、相机与贴图，并用 shaderNPR 的真实 GLB/相机读取器和 GPU GeometryRenderer 重投影几何；本轮全部通过，18 视角几何 IoU 为 0.99898–0.99970，64 个原材质文件哈希未变。报告和预览在 `Comparison/ArtistRefs/validation.json`、`Comparison/ArtistRefs/refs_overview.jpg`。

导出使用从现有 UTSProject 复制的 UniGLTF `0.131.4`（嵌入式 `Packages/com.vrmc.gltf`，许可证随包保留），并复用 ToonRPProject 的 `OrbitRenderMatch.cs` / `GeometryMask.shader`。继续使用 Unity `6000.3.24f1` / URP `17.3.0`。

## Unity 6 适配范围

- 嵌入式 Toon Shader 包的 URP 依赖更新为 `17.3.0`。
- 原 Shader 的 RenderPipeline 标签改为 URP 17 使用的 `UniversalPipeline`。
- 对比使用独立的 URP Forward Renderer，关闭 Depth Priming，不使用旧第三方描边 Renderer Feature；主光阴影图集为 4096、单级联。
- `Assets/ComparisonTools/` 保存对比工具、Base Color Shader、渲染配置和预览场景。

原始功能测试场景仍需分别检查；上游的 UnityFx 第三方描边依赖尚未迁移，不能据此认为全部旧场景功能已兼容 Unity 6。

## 首轮验证

2026-11-19，使用已激活的普通用户，在现有 GPU 容器内以 Unity `6000.3.24f1`、URP `17.3.0`、Vulkan 和 GPU 0（RTX 5090）完成全部 10 个角色、20 张原图的真实渲染。所用 Shader 通过编译检查，图像通过尺寸、非空和粉色错误检查；取景按照实际骨骼姿态和蒙皮顶点计算，避免 FBX 导入缩放和旧包围盒导致裁切。

每组图片使用相同相机和姿态；脚本逐项比较 Shader 实际属性、贴图、UV 变换、有效关键词和渲染队列，确认原材质参数在对比过程中未被修改。本次开始时对全部 64 个原 `.mat` 文件记录 SHA-256，完成后核对均未变化，记录见 `Comparison/material_source_hashes.json`。前次已确认 Arisa 和 Warrior 的 16 份原材质与固定上游提交逐字节一致。

姿态方面：Arisa、Warrior 和 4 个城市人物使用已有男女 Idle；Mike 使用自身 Idle；Crazy Cat 使用自身 Normal Stance；参考机器人使用自身 Dance。Supercyan 男性没有有效的 Humanoid Avatar，保留原示例的静态姿态，不人工重设骨骼或强行重定向。
