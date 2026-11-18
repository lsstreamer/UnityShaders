# Unity Toon / NPR 完整用例调研

更新日期：2026-11-17。

## 当前需求与统计口径

- 用于论文实验；新建独立 Unity 工程，管线不限，优先较新版本。
- 必须有现成完整用例：模型、Shader、配置好的材质，以及配套灯光、相机或场景。
- **已放宽为允许基于任意 Shader 库（包括 UTS）；免费或付费均可，预算 500 元以内。**
- 价格按约 `1 USD ≈ 7.2 元` 估算，未计税；促销与汇率会变化，以结账总价为准。
- 付费资源数量来自作者公开说明；免费项目数量来自仓库文件树。尚未在 Unity 中导入、编译或渲染验证。
- 场景文件可能包含功能测试；FBX 可能包含动画、碰撞网格和模型部件，不能直接等同于独立模型数量。

## 付费候选对比

| 项目 / 商店链接 | 正常价 / 调研时促销价 | 最新版本 / 日期 | 管线与版本 | 完整用例证据 | 建议 |
| --- | --- | --- | --- | --- | --- |
| [Flat Kit](https://assetstore.unity.com/packages/vfx/shaders/flat-kit-toon-shading-and-water-143368) | `$39.90`（约 287 元）/ `$19.95` | `4.9.15` / 2026-11-04 | Built-in、URP；商店标注支持 Unity 6 | 作者介绍：11 个 Demo 场景、100+ 模型、20+ 材质预设 | **综合首选** |
| [Quibli](https://assetstore.unity.com/packages/vfx/quibli-anime-shaders-and-tools-203178) | `$59.90`（约 431 元）/ `$29.95` | `2.6.6` / 2026-11-04 | URP；商店标注支持 Unity 6 | 作者介绍：7 个场景、50+ 模型、60+ 纹理、200+ 材质、20+ 预设 | **环境、植物和动画风格首选** |
| [RealToon](https://assetstore.unity.com/packages/vfx/shaders/realtoon-pro-anime-toon-shader-65518) | `$35`（约 252 元）/ `$17.50` | `5.0.16` / 2026-09-17 | Built-in、URP、HDRP；商店标注支持 Unity 6 | 有公开角色灯光与游戏场景 Demo；包内模型、场景数量未核实 | 角色和跨管线方向候选 |
| [Toony Colors Pro 2](https://assetstore.unity.com/packages/vfx/shaders/toony-colors-pro-2-8105) | `$75`（约 540 元）/ `$37.50` | `2.9.22` / 2026-07-24 | Built-in、URP；官方说明支持 Unity 6 / URP 17 | 有 Shader Generator Demo 场景和 WebGL 展示；模型总数未核实 | 正价超预算，促销时考虑 |
| [The Toon Shader](https://assetstore.unity.com/packages/vfx/shaders/the-toon-shader-269238) | `$49.99`（约 360 元）/ `$15` | `1.4.3`；发布日期待补核 | Built-in、URP；商店兼容表列 Unity 2022.3 | 有官方文档和效果展示；完整模型、场景数量未核实 | 次选，完整用例证据不足 |

### Flat Kit：多类别场景最值得优先看

- 内容涉及环境、室内、汽车、树木、道具、地形和水体；有 Toon/Cel、描边、雾、天空、光束和像素化效果。
- Demo 文档列出 Valley、Wanderer、Mugs、Fruit Vase、Tree Island、Room、Retro Cars、Racing Car、Desert、Normal Map Tree、Ocean Water、Pool、Water Vessels 等场景或场景组。
- 官网的“11 个 Demo”与文档列出的场景组口径不同，实际 `.unity` 文件数需导入后确认；100+ 模型也是作者标称，尚未去重统计。
- 链接：[官方介绍与数量](https://dustyroom.com/flat-kit/) · [Demo 场景详情](https://flatkit.dustyroom.com/demo-scenes/) · [官方文档](https://flatkit.dustyroom.com/)。

### Quibli：现成环境和材质配套明确

- 7 类 Demo：Nature、City、Kitchenware、Plants、Mugs、Sample、Clouds；文档明确说明包含使用到的模型和材质。
- 具备植物、草、云、天空、光束、渐变着色、后处理，以及植物和曲线生成工具；主要面向环境和道具，并非多角色展示包。
- 仅支持 URP。正常价接近预算上限，需计入税费；部分示例与 Flat Kit 共用内容，两包数量不能直接相加。
- 链接：[官方介绍与数量](https://dustyroom.com/quibli/) · [Demo 场景详情](https://quibli.dustyroom.com/demo-scenes/) · [官方文档](https://quibli.dustyroom.com/)。

### RealToon / Toony Colors Pro 2：先核实包内资源

- **RealToon**：角色效果和管线覆盖有吸引力；公开可运行 Demo 不能直接证明全部模型、灯光和源场景随购买包提供。
  链接：[作者项目页（含两个 Demo 入口）](https://mjqstudioworks.weebly.com/projects.html) · [角色灯光 Web Demo](https://realtoondemo1.netlify.app/)。
- **Toony Colors Pro 2**：Shader Generator、Ramp、描边、水体等工具完整，教程明确提到 `Demo TCP2 ShaderGenerator2.unity`；适合自定义效果，但不是已确认的大型模型库。
  链接：[官方主页与 WebGL 展示](https://www.jeanmoreno.com/unity/toonycolorspro/) · [主文档](https://jeanmoreno.com/unity/toonycolorspro/doc/) · [示例教程](https://jeanmoreno.com/unity/toonycolorspro/doc/tutorials)。
- **The Toon Shader**：偏漫画、排线、网点和 Cel 风格；完整用例数量待补核。
  链接：[作者官网与文档入口](https://www.shadercrew.com/#toon)。

## 免费候选（本轮更新）

以下代码许可与第三方模型许可分开计算；“免费”不代表所有模型都能随论文源码再分发。数量来自仓库文件树，FBX 可能包含动画、碰撞网格和模型部件，不能直接等同于独立模型数。

| 项目 / 链接 | 声明环境 | 示例统计 | 判断 |
| --- | --- | --- | --- |
| [Delt06/toon-rp](https://github.com/Delt06/toon-rp) · [Samples](https://github.com/Delt06/toon-rp/tree/master/Assets/Samples) | 自定义 SRP，Unity 2022.3.12f1 / Core RP 14.0.9 | 13 场景、36 FBX、94 材质 | MIT；Cyberpunk、Space、CubeWorld、PixelFPS 等，仍是免费综合首选；项目标注早期阶段 |
| [Delt06/urp-toon-shader](https://github.com/Delt06/urp-toon-shader) · [Scenes](https://github.com/Delt06/urp-toon-shader/tree/master/Assets/Scenes) | Unity 2021.3 / URP 12.1.6 | 16 场景、56 FBX、65 材质 | MIT；角色、森林、城市示例丰富，但已不再积极维护 |
| [BlurToonURP](https://github.com/blurfeng/blur-toon-urp) | README 声明 Unity 2022.3 / URP 14 | 1 场景、5 FBX、68 材质 | MIT；Nicole 角色、Idle 动画和环境网格，适合单角色实验 |
| [ChiliMilk/URP_Toon](https://github.com/ChiliMilk/URP_Toon) | URP 12.1.6 | 3 场景、2 FBX、12 材质 | MIT；UnityChan、脸部 SDF、头发阴影，规模较小 |
| [LiliumToonGraph](https://github.com/you-ri/LiliumToonGraph) | README 声明 Unity 2020.2 / URP 10.3.1 | 3 场景、2 FBX、29 材质、8 Shader Graph | MIT；最近推送 2026-11-03，有 Toon、Hair、Rendering 测试场景，但依赖内部 API、版本旧 |
| [HonkaiStarRailToonShader](https://github.com/Gaolingx/HonkaiStarRailToonShader) | Unity 2022.3 / URP | 1 Fu Xuan Prefab、2 FBX、7 材质、6 角色 Shader | 4.5.0，最近推送 2025-05-28；代码 MIT，另依赖 [StarRailNPRShader（GPL-3.0）](https://github.com/stalomeow/StarRailNPRShader)，没有 `.unity` 场景 |
| [GenshinCelShaderURP](https://github.com/Gaolingx/GenshinCelShaderURP) | URP Forward | 1 Nilou FBX、5 材质、1 主 Shader、5 HLSL | 最近推送 2025-03-29；MIT 代码，带模型和贴图但没有场景，示例模型版权归 miHoYo 且 README 标注“切勿商用” |
| [unity-isometric-pixel-pipeline](https://github.com/bababuyyy/unity-isometric-pixel-pipeline) | Unity 6 / URP 17.3.0 | 1 测试场景 + 1 恢复场景、1 FBX、2 OBJ、11 材质、5 Shader | MIT；最近推送 2026-05-16，含 toon lighting、GPU grass、描边、昼夜系统，但模型数量少、偏像素等距风格 |

### 免费但不适合作为完整场景首选

- [PoiyomiToonShader](https://github.com/poiyomi/PoiyomiToonShader)：仓库代码 MIT，最近推送 2026-11-05；245 个材质、24 个 Shader、2 FBX，但没有 `.unity` 场景，适合研究 Shader 参数和预设。第三方资源许可需分别检查。
- [StarRailNPRShader](https://github.com/stalomeow/StarRailNPRShader)：GPL-3.0，角色 Shader 代码较完整，但没有模型或场景；通常作为 HonkaiStarRailToonShader 依赖使用。
- [URP Toon Lit Shader Example](https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample)：MIT，最近仍有更新，但只有教程 Shader/HLSL，没有模型和 Unity 场景。
- [URPShadersCollection](https://github.com/TinyPlay/URPShadersCollection)：MIT，1 场景、31 FBX、17 材质，主要是一个狐狸模型及动画，不能按 31 个独立模型计算。
- [JTRP](https://github.com/JasonMa0012/JTRP)：含角色、材质和 `JTRP_Samples.unitypackage`，但版本偏旧，样例包内容及模型许可仍需核查。
- [lilToon](https://github.com/lilxyzw/lilToon)：功能成熟，但主仓库缺少完整模型场景示例。

## 选型结论与未验证项

1. **要模型、场景和材质数量**：首选 [Delt06/toon-rp](https://github.com/Delt06/toon-rp)；可接受更旧 URP 时再看 [urp-toon-shader](https://github.com/Delt06/urp-toon-shader)。
2. **要较新的角色 Shader 和现成角色资源**：选 [HonkaiStarRailToonShader](https://github.com/Gaolingx/HonkaiStarRailToonShader)，但需要自己搭建场景、灯光，并确认米哈游模型仅用于论文展示是否合规。
3. **要 Unity 6 技术基线**：选 [unity-isometric-pixel-pipeline](https://github.com/bababuyyy/unity-isometric-pixel-pipeline)；有工程与测试场景，但模型样例不丰富。
4. **要 Shader Graph 场景参考**：选 [LiliumToonGraph](https://github.com/you-ri/LiliumToonGraph)，接受 Unity 2020/URP 10 的兼容性风险。
5. **要原神角色效果**：可试 [GenshinCelShaderURP](https://github.com/Gaolingx/GenshinCelShaderURP)，但不要把其示例模型当作可自由再分发资产。

尚未执行 Unity 导入、编译或渲染验证。最终选定后仍需检查实际独立模型数、场景灯光是否已配置、目标 Unity 版本兼容性，以及论文配图和原始资源再分发许可；付费包遵循商品页许可，提供 Shader 源码不等于开源授权。
