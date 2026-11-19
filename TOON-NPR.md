# Unity Toon / NPR 完整用例调研

候选调研更新于 2026-11-17。本地工程清单核对于 2026-11-18。

## 当前工程中的外部包和素材

工作区有两个独立 Unity 工程，均使用 Unity `6000.3.24f1`。下表以本地 `Packages/manifest.json`、`Packages/packages-lock.json`、嵌入包的 `package.json` 和 `Assets/` 为准，包含 Git 忽略的本地下载资源。编辑器自带且工程未引用的包、Unity 内置模块和临时渲染目录略去。[UTSProject 包锁定文件](UTSProject/Packages/packages-lock.json) · [ToonRPProject 包锁定文件](ToonRPProject/Packages/packages-lock.json)

### Unity Package Manager 包

| 包 | 工程及安装方式 | 用途、来源与许可 |
| --- | --- | --- |
| Unity Toon Shader `com.unity.toonshader` `0.16.1-preview` | `UTSProject`，Unity 注册表 | Unity 官方 Toon Shader，代码采用 Unity Companion License。缓存内有 Built-in、URP、HDRP 示例，包括项目渲染用到的 UnityChan CelLook。示例角色许可见包内说明。[官方文档](https://docs.unity3d.com/Packages/com.unity.toonshader@0.16/manual/index.html) |
| Unity glTFast `com.unity.cloud.gltfast` `6.21.0` | `UTSProject`，Unity 注册表 | 导入 GLB/glTF，Apache-2.0。[上游](https://github.com/Unity-Technologies/com.unity.cloud.gltfast) |
| Toon RP `com.deltation.toon-rp` `0.9.0` | `ToonRPProject`，嵌入包 | Vladislav Kantaev（Delt06）的自定义 SRP，MIT。本地工程已适配 Unity 6，保留 `Assets/Samples/` 下 13 个上游场景及其模型和材质。[上游](https://github.com/Delt06/toon-rp) · [适配说明](ToonRPProject/README.md) |
| UniGLTF `com.vrmc.gltf` `0.131.4` | 两个工程，嵌入包 | UniVRM 项目的 glTF 基础库，含 UniUnlit，MIT。`UTSProject` 保留 glTFast 作为默认 GLB/glTF 导入器。[上游版本](https://github.com/vrm-c/UniVRM/tree/v0.131.4) |
| UniVRM `com.vrmc.univrm` `0.131.4` | 两个工程，嵌入包 | VRM Consortium 的 VRM 角色支持，含 `VRM/MToon` Shader，代码和 MToon 均为 MIT。[上游版本](https://github.com/vrm-c/UniVRM/tree/v0.131.4) |
| URP `17.3.0`、Core RP `17.3.0`、Shader Graph `17.3.0` | `UTSProject` 含三者，`ToonRPProject` 含 Core RP 和 Shader Graph | Unity 官方渲染依赖。`UTSProject` 默认跑 Built-in，Chop Chop 预览用 URP。`ToonRPProject` 用自定义 Toon RP。[UTSProject 说明](UTSProject/README.md) · [ToonRPProject 说明](ToonRPProject/README.md) |

两个工程还解析了 Unity 的 Burst、Collections、Mathematics、Searcher、uGUI、Test Framework 和 Performance Testing 等通用依赖。其中 Unity 分发的第三方库包括 Mono.Cecil `1.11.6`（`com.unity.nuget.mono-cecil`）和 NUnit `2.0.5`（`com.unity.ext.nunit` 的包版本）。`UTSProject` 中的 UniVRM 与 UniGLTF 目录已被 Git 忽略。`ToonRPProject` 整个工程目前在工作区，尚未被 Git 跟踪。[UTSProject 忽略规则](UTSProject/.gitignore) · [UTSProject 包锁定文件](UTSProject/Packages/packages-lock.json) · [ToonRPProject 包锁定文件](ToonRPProject/Packages/packages-lock.json)

### UTSProject 中直接导入的资源

| 资源 | 本地内容 | 来源与许可 |
| --- | --- | --- |
| SD Kohaku-chanz `1.1.0` | `Assets/UTSOfficialCharacters/UnityChan/SD_Kohaku_chanz/`，含夏制服 Misaki 与 `Misaki_sum.unity` | Unity Technologies Japan，Unity-Chan License 2.02。[官方下载](https://unity3d.jp/unity-chan_contents/releaseNote.php?id=SDKohakuchanz&lang=en) |
| UnityChan Urban Street Style | `Assets/UTSOfficialCharacters/UrbanStreetStyle/`，含角色、8 个场景、UnityChan SpringBone 和 Unity-Chan Toon Shader `2.0.7` | Born Digital 教程书配套下载，版权声明为 Urakami、Maejima，随包附 Unity-Chan License 2.0。[配套书资源页](https://www.borndigital.co.jp/book/15564.html) · [UTS2 上游](https://github.com/unity3d-jp/UnityChanToonShaderVer2_Project) |
| VRoid Preset A–Z 的原始 A/B/C 模型 | `Assets/VRoidSamples/`，含 `AvatarSample_A`、`AvatarSample_B`、`AvatarSample_C` 三个 VRM 及对应预览场景，沿用 MToon 材质 | pixiv Inc. / VRoid Project。模型适用 VRoid 官方条款，下载镜像和哈希记录在本地 `SOURCE.json`。[VRoid 官方条款](https://vroid.pixiv.help/hc/en-us/articles/4402394424089-VRoidPreset-A-Z) |
| Chop Chop（Unity Open Project #1） | `Assets/ChopChop/`，含 9 个角色预览场景，以及配套模型、贴图、材质和 Toon Shader Graph | 导入角色预览所需的上游资源，固定提交 `608eac98df29cd97821a6115cd52dfb9027345b1`，随包附 Apache-2.0。[上游版本](https://github.com/UnityTechnologies/open-project-1/tree/608eac98df29cd97821a6115cd52dfb9027345b1) |

上表四组资源均已存在于本机，对应目录由 `.gitignore` 忽略。工程也包含 `Assets/Models/CafeHouse/` 和 `Assets/Models/Round_01/` 下的模型及配套场景，当前工程资料中未核实其是否来自公开发布包，也未核实上游许可。[UTSProject 忽略规则](UTSProject/.gitignore) · [工程模型目录](UTSProject/Assets/Models) · [工程场景目录](UTSProject/Assets/Scenes)

### ToonRPProject 示例中随附的第三方素材

| 示例 | 素材及来源 |
| --- | --- |
| `Main` | Quaternius 的 [Animated Mech Pack](https://quaternius.com/packs/animatedmech.html)（George，本地附 CC0 1.0）和 [Ultimate Stylized Nature Pack](https://quaternius.com/packs/ultimatestylizednature.html)，以及 Unity Asset Store 的 [Aika: Sailor Uniform](https://assetstore.unity.com/packages/3d/characters/aika-sailor-uniform-222398) 和 [Stone](https://assetstore.unity.com/packages/3d/environments/landscapes/stone-62333)。 |
| `CubeWorld`、`Cyberpunk`、`PixelFPS`、`Space` | 分别使用 Quaternius 的 [Cube World Kit](https://quaternius.com/packs/cubeworldkit.html)、[Cyberpunk Game Kit](https://quaternius.com/packs/cyberpunkgamekit.html)、[Toon Shooter Game Kit](https://quaternius.com/packs/toonshootergamekit.html) 和 [Ultimate Space Kit](https://quaternius.com/packs/ultimatespacekit.html)（本地附 CC0 1.0）。 |
| MatCap 贴图 | 上游素材清单还列出 [nidorx/matcaps](https://github.com/nidorx/matcaps)。 |

Toon RP 的 MIT 许可见包内 `LICENSE.md`。示例素材的授权以各自随附文件或发布页为准，Aika 和 Stone 的具体再分发条款尚未核实。[Toon RP 上游素材清单](ToonRPProject/README.upstream.md#used-assets) · [Toon RP 包许可](ToonRPProject/Packages/com.deltation.toon-rp/LICENSE.md)

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
