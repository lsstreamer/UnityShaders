# Toon RP 独立工程

本目录是从 [Delt06/toon-rp](https://github.com/Delt06/toon-rp) 导入的独立 Unity 工程，保留仓库自带的 Toon RP 包、13 个示例场景、模型和材质。上游工程声明使用 Unity 2022.3.12f1 / Core RP 14.0.9；本工程沿用现有 `6000.3.24f1` 编辑器，不更换 Unity 编译器。

## Unity 6 适配

- Unity 6 自动将 Core RP 和 Shader Graph 解析为 17.3.0。
- `ToonRenderPipelineAsset` 使用 Unity 6 的泛型 `RenderPipelineAsset<ToonRenderPipeline>`，避免运行时无法识别管线类型。
- `LightType.Area` 更新为 Unity 6 的 `LightType.Rectangle`。
- 场景辅助组件使用 `GraphicsSettings.defaultRenderPipeline`。
- 工程保持 Linear 色彩空间；Toon RP 不支持 Gamma。

## 打开与示例

用 Unity Hub 选择本目录并打开 `6000.3.24f1`。可从以下场景开始：

`Assets/Samples/CubeWorld/CubeWorld.unity`

仓库中的示例依赖均已放在工程内，首次打开后等待 Package Manager 和 Shader 编译完成即可。上游资源和代码许可见 `LICENSE.upstream.md` 与包目录内的 `LICENSE.md`。

## 验证结果

已用现有 GPU 容器中的 Unity `6000.3.24f1`、Vulkan 和 GPU 7 导入工程并渲染 `CubeWorld`。管线实例成功创建，主灯参数正常，材质正确出图；验证用 PNG 已在检查后删除。

## Aika 角色对比

`Comparison/` 中保留 Aika 正面单视角预览。角色沿用项目原材质和 Shader；相机、灯光和阴影采样按预览需求调整，运行结束后恢复原配置，不保存到场景和管线资产。

- `aika_front_basecolor.png`：保留 Aika 原材质的纹理、颜色和 Alpha 设置，临时替换为 Toon RP Unlit。
- `aika_front_project_lit.png`：项目原材质搭配调整后的前侧上方主灯和弱补光，保留明显的 Toon 明暗分界。
- `aika_front_comparison.png`：左右并排对照，左为 BaseColor，右为灯光渲染。
- `settings.json`：记录场景、管线资产、正交相机尺寸和位置、启用灯光、材质 Shader、实际使用的管线配置及采样参数。

两张原图使用相同的正交相机，角色正对相机，尺寸为 1024×1024；内部以 2048×2048 渲染后缩小，并使用 8x MSAA。主灯旋转为 `(30, 135, 0)`，阴影图集提高到 4096，深度为 24 bit；去掉样例的条纹阴影图案，保留软阴影采样和明暗分界。原示例使用 1024 图集分为四级联且未启用 MSAA，条纹图案也会让阴影边缘显得破碎。

批处理入口为 `Assets/Editor/RenderAikaComparison.cs`。只生成正面两张图和 `settings.json`，并排图由下方 FFmpeg 命令生成。

验证命令（容器内）：

```bash
cd /workspace/UnityProjects/ToonRPProject
export LD_LIBRARY_PATH=/workspace/Unity/runtime-ubuntu24.04/usr/lib/x86_64-linux-gnu${LD_LIBRARY_PATH:+:$LD_LIBRARY_PATH}
export VK_DRIVER_FILES=/etc/vulkan/icd.d/nvidia_icd.json
xvfb-run -a -s '-screen 0 1024x768x24 -nolisten tcp' \
  /workspace/Unity/6000.3.24f1/Editor/Unity \
  -batchmode -quit -force-vulkan -force-device-index 7 \
  -projectPath /workspace/UnityProjects/ToonRPProject \
  -executeMethod RenderAikaComparison.Run
ffmpeg -y -loglevel error \
  -i Comparison/aika_front_basecolor.png \
  -i Comparison/aika_front_project_lit.png \
  -filter_complex hstack=inputs=2 -frames:v 1 \
  Comparison/aika_front_comparison.png
```
