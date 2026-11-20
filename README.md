# UnityProjects

2026-11-19 已按 `shaderNPR/comparison/unity` 的参考图生成依赖清理闲置素材；当前保留三个渲染工程及所需资源，示例场景不再完整。范围和验证记录见 [素材清理记录](ASSET-CLEANUP.md)。

## 环境

- Unity Editor：`6000.3.24f1`
- UTS / Built-in 与 Chop Chop 工程：`UTSProject/`
- Toon RP / Aika 工程：`ToonRPProject/`
- URP Toon / 9 个角色工程：`URPToonProject/`
- 渲染脚本和配置：`UTSProject/render.sh`、`UTSProject/configs/`

## 获取项目

```bash
git clone <仓库地址> UnityProjects
cd UnityProjects/UTSProject
```

用 Unity Hub 安装并打开 `6000.3.24f1`，选择 `UTSProject/`。首次打开时，Unity 会按照 `Packages/manifest.json` 和 `Packages/packages-lock.json` 自动下载 Unity Toon Shader、glTFast 等包；`Library/`、`Temp/`、`Logs/` 和 `UserSettings/` 是本地生成目录，不需要从仓库下载。

`ToonRPProject/` 是 `Delt06/toon-rp` 的独立工程。它原本声明 Unity 2022.3 / Core RP 14.0.9；已在不更换现有 `6000.3.24f1` 编辑器的前提下完成 Unity 6 适配，清理后保留 Aika 使用的 Main 场景及其依赖。详细打开方式及验证结果见 [ToonRPProject/README.md](ToonRPProject/README.md)。

## 素材包、依赖与下载地址

以下清单核对于 2026-11-19，以清理后保留的三个工程、包清单和素材自带说明为依据。第三方素材随各自的模型、材质、贴图、动画一起记录；仅剩空目录或许可证的已删除示例包不列为当前素材。部分完整场景保留了背景、道具等依赖，因此这些包也列在下表。

### 渲染、Shader 和模型导入导出包

| 包及本地版本 | 所在工程与用途 | 下载 / 安装来源 |
| --- | --- | --- |
| Unity Toon Shader `com.unity.toonshader` `0.16.1-preview` | UTSProject；官方 Toon Shader，含 Built-in 的 UnityChan CelLook / Illust 示例 | Package Manager 按包名安装；[官方仓库](https://github.com/Unity-Technologies/com.unity.toonshader) · [0.16 文档](https://docs.unity3d.com/Packages/com.unity.toonshader@0.16/manual/index.html) |
| Toon RP `com.deltation.toon-rp` `0.9.0` | ToonRPProject；嵌入式自定义渲染管线，已作本地 Unity 6 适配 | [源码与示例](https://github.com/Delt06/toon-rp)；UPM Git 地址：`https://github.com/Delt06/toon-rp.git?path=Packages/com.deltation.toon-rp` |
| URP Toon Shader `com.deltation.toon-shader` `0.4.8` | URPToonProject；嵌入式角色 Toon / Fur Shader，已作本地 URP 17 适配 | [固定上游版本](https://github.com/Delt06/urp-toon-shader/tree/6981f7dc931b53a0bd797725f4dac30b824041d7) · [完整工程 ZIP](https://github.com/Delt06/urp-toon-shader/archive/6981f7dc931b53a0bd797725f4dac30b824041d7.zip) |
| Unity glTFast `com.unity.cloud.gltfast` `6.21.0` | UTSProject；GLB / glTF 导入 | Package Manager 按包名安装；[官方仓库](https://github.com/Unity-Technologies/com.unity.cloud.gltfast) |
| UniGLTF `com.vrmc.gltf` `0.131.4` | 三个工程；嵌入式 GLB 导出和 glTF 基础库，含 UniUnlit | [UniVRM v0.131.4 发布页](https://github.com/vrm-c/UniVRM/releases/tag/v0.131.4) · [对应源码](https://github.com/vrm-c/UniVRM/tree/v0.131.4/Packages)；使用其中 `com.vrmc.gltf` |
| UniVRM `com.vrmc.univrm` `0.131.4` | UTSProject、ToonRPProject；已安装的 VRM / MToon 支持 | [同版本发布页](https://github.com/vrm-c/UniVRM/releases/tag/v0.131.4)；使用其中 `com.vrmc.univrm`，保留 UniGLTF 依赖 |
| lilToon `jp.lilxyzw.liltoon` `2.3.4` | UTSProject；本地保留的嵌入式 Toon Shader 包 | [2.3.4 ZIP](https://github.com/lilxyzw/lilToon/releases/download/2.3.4/jp.lilxyzw.liltoon-2.3.4.zip) · [官方文档](https://lilxyzw.github.io/lilToon/) |
| URP / Core RP / Shader Graph `17.3.0` | UTSProject、URPToonProject 使用 URP；ToonRPProject 使用 Core RP / Shader Graph | Package Manager 按各工程清单解析；[Unity Graphics 源码](https://github.com/Unity-Technologies/Graphics) |
| Unity-Chan Toon Shader 2 / SpringBone | `UnityChanTPK`、Urban Street Style 随附的 Shader、角色骨骼组件；USS 随附 UTS2 `2.0.7` | 随下表角色包获取；[UTS2 上游](https://github.com/unity3d-jp/UnityChanToonShaderVer2_Project) · [SpringBone 上游](https://github.com/unity3d-jp/UnityChanSpringBone) |

其他 Unity 模块和传递依赖由包管理器处理，完整包名、版本及来源以各工程的清单和锁定文件为准：[UTS manifest](UTSProject/Packages/manifest.json) / [lock](UTSProject/Packages/packages-lock.json)、[ToonRP manifest](ToonRPProject/Packages/manifest.json) / [lock](ToonRPProject/Packages/packages-lock.json)、[URP Toon manifest](URPToonProject/Packages/manifest.json) / [lock](URPToonProject/Packages/packages-lock.json)。嵌入包的版本以各自 `package.json` 为准。上游下载内容不包含本工程的本地适配和自建预览场景，复现时应保留本仓库中的适配文件。

### Unity 官方角色与 Chop Chop

| 素材包 | 本地位置与对应输出 | 下载地址与来源说明 |
| --- | --- | --- |
| SD Kohaku-chanz `1.1.0` | `UTSProject/Assets/UTSOfficialCharacters/UnityChan/SD_Kohaku_chanz/`；`misaki_school_summer` | [官方发布 / 下载页](https://unity3d.jp/unity-chan_contents/releaseNote.php?id=SDKohakuchanz&lang=en)；Unity Technologies Japan，UCL 2.02；本地记录见 `UTSOfficialCharacters/SOURCE.txt` |
| Unity-Chan 3D Model Data `1.4.0`（Classic） | `UTSProject/Assets/UTSOfficialCharacters/ClassicUnityChan/`；`unitychan_classic` | [Unity-Chan 官方下载入口](https://unity-chan.com/download/)；选择 Unity-Chan 3D 模型，当前本地 README 标明 `1.4.0`、2023-02-17；UCL 2.02。未保存该版本独立压缩包的直链 |
| UnityChan Urban Street Style | `UTSProject/Assets/UTSOfficialCharacters/UrbanStreetStyle/`；`unitychan_uss_arrange`、`unitychan_uss_cellb`、`unitychan_uss_haircolor`、`unitychan_uss_pop` | [Born Digital 配套书下载页](https://www.borndigital.co.jp/book/15564.html)，书名《実践！ユニティちゃんトゥーンシェーダー2.0 スーパー使いこなし術》；本地记录见该目录 `SOURCE.txt`，Urakami / Maejima，随附 UCL 2.0 |
| UnityChanTPK 六角色素材 | `UTSProject/Assets/UnityChanTPK/`；`01_kohaku_A`、`01_kohaku_B`、`02_yuko`、`03_misaki`、`04_yuji`、`05_akaza` | [Unity-Chan 官方下载入口](https://unity-chan.com/download/) · [UTS2 工程与样例来源入口](https://github.com/unity3d-jp/UnityChanToonShaderVer2_Project)；本地随附 UCL 2.0。原始 TPK 压缩包名称、版本及精确下载直链未保存在项目中，以上为查找入口 |
| Unity Toon Shader 的 UnityChan 示例 | UTS 包的 `Samples~/Built-In_RP/`；`unitychan_cel_look`、`unitychan_illust` | 随上表 `com.unity.toonshader` 包下载；[官方仓库](https://github.com/Unity-Technologies/com.unity.toonshader)。脚本按需暂存官方场景及其 `UnityChan` 依赖 |
| Chop Chop（Unity Open Project #1） | `UTSProject/Assets/ChopChop/`；BardHare、LegendaryChef、PhoenixChick、PigChef、PlantCritter、RockCritter、Townsfolk_F、Townsfolk_M；另保留 SlimeCritter 预览 | [固定源码版本](https://github.com/UnityTechnologies/open-project-1/tree/608eac98df29cd97821a6115cd52dfb9027345b1) · [完整 ZIP](https://github.com/UnityTechnologies/open-project-1/archive/608eac98df29cd97821a6115cd52dfb9027345b1.zip)；Apache-2.0；本地 `ChopChop/SOURCE.md` 记录了模型、Shader Graph、材质和自建预览场景的处理方式 |

外部角色资源导入后的目录布局如下。`UTSOfficialCharacters`、ChopChop 等目录在 `.gitignore` 中忽略，克隆本仓库不会自动下载这些素材。

```text
UTSProject/Assets/
├── UTSOfficialCharacters/
│   ├── UnityChan/SD_Kohaku_chanz/
│   ├── ClassicUnityChan/
│   └── UrbanStreetStyle/
├── UnityChanTPK/
└── ChopChop/
```

SD Kohaku-chanz 原包的 `UnityChan/` 放到 `UTSOfficialCharacters/` 内；Classic 和 USS 分别放入对应子目录。保留原包的 `SOURCE`、README、许可证和 `.meta`。打开 Unity 等待导入完成；角色样例与导出流程还依赖本仓库已有脚本和场景。CelLook / Illust 使用 UTS 包内样例，不需要另下载 SD Kohaku-chanz。

### Toon RP Main 与 URP Toon 场景随附素材

这些资源随 Delt06 上游工程导入。下载上游完整工程可取得其收录版本；原作者发布页用于查找独立素材包。Toon RP 对应目录为 `ToonRPProject/Assets/Samples/Main/Art/`，URP Toon 对应目录为 `URPToonProject/Assets/Graphics/_External Assets/`。

| 素材包 / 作者 | 本地使用位置与用途 | 原始发布 / 下载地址 |
| --- | --- | --- |
| Aika: Sailor Uniform | Toon RP `Sailor Uniform/`；`aika_sailor_uniform` 主角 | [Unity Asset Store](https://assetstore.unity.com/packages/3d/characters/aika-sailor-uniform-222398) |
| Animated Mech Pack / Quaternius | Toon RP 的 George 与 URP Toon 的 Mike；`Animated Mech Pack/`、`Animated Mech Pack - March 2021/` | [作者下载页](https://quaternius.com/packs/animatedmech.html)；本地随附 CC0 1.0 |
| Ultimate Stylized Nature Pack / Quaternius | Toon RP Main 场景的自然环境依赖 | [作者下载页](https://quaternius.com/packs/ultimatestylizednature.html) |
| Stone / Vsify | Toon RP Main 的 `Stone/`；URP Toon 同名目录已无模型素材 | [Unity Asset Store](https://assetstore.unity.com/packages/3d/environments/landscapes/stone-62333) |
| MatCap 贴图库 / nidorx | Toon RP 上游素材清单列出的 MatCap 来源；本地保留 Stone 的 `Gold_matcap.png` 和 Aika 的 `HiLight_Matcap.png`，未逐张核实它们与图库的对应关系 | [原始图库与下载入口](https://github.com/nidorx/matcaps) |
| Anime Character: Arisa / Dai Yong Xiang | URP Toon `ArisaNew/`；Arisa 模型及 15 份材质 | [Unity Asset Store](https://assetstore.unity.com/packages/3d/characters/anime-character-arisa-free-remakev2-contain-vrm-164251) |
| RPG Character Pack / Quaternius | URP Toon `RPG Characters - Nov 2020/`；Warrior | [作者下载页](https://quaternius.com/packs/rpgcharacters.html)；本地随附 CC0 1.0 |
| Crazy Cats Toon Pack / Snowconesolid Assets（Samer Khatib） | URP Toon `Snowconesolid Assets/Crazy Cats Toon Pack/`；CrazyCat 和姿态动画 | [工程中收录的素材目录](https://github.com/Delt06/urp-toon-shader/tree/6981f7dc931b53a0bd797725f4dac30b824041d7/Assets/Graphics/_External%20Assets/Snowconesolid%20Assets)；可从上表 URP Toon 完整 ZIP 获取。本地 ReadMe 标注作者与包名，未提供独立商店链接 |
| Character Pack: Free Sample / Supercyan | URP Toon `Supercyan Character Pack Free Sample/`；SupercyanMale | [Unity Asset Store](https://assetstore.unity.com/packages/3d/characters/humanoids/character-pack-free-sample-79870) |
| Toony Tiny City Demo / Marcelo Barrio | URP Toon `Tiny_Toony_City_Demo/`；CityFemale、CityMaleA、CityMaleB、CityPolice 以及男女 Idle 动画和场景依赖 | [Unity Asset Store](https://assetstore.unity.com/packages/3d/environments/urban/toony-tiny-city-demo-176087) |
| Environment Pack: Free Forest Sample / Supercyan | URP Toon `Supercyan Free Forest Sample/`；保留 Forest 原场景的环境依赖 | [Unity Asset Store](https://assetstore.unity.com/packages/3d/vegetation/environment-pack-free-forest-sample-168396) |
| The Free Medieval and War Props / Inguz Media | URP Toon `Inguz Media Studio/`；保留原场景中的道具依赖 | [Unity Asset Store](https://assetstore.unity.com/packages/3d/props/the-free-medieval-and-war-props-174433) |

上游完整工程：[Toon RP 源码与素材](https://github.com/Delt06/toon-rp) · [URP Toon 固定版本 ZIP](https://github.com/Delt06/urp-toon-shader/archive/6981f7dc931b53a0bd797725f4dac30b824041d7.zip)。Shader 代码的 MIT 许可与素材自身许可分别适用；各素材的授权以原包说明和发布页为准。

以上地址从本地来源文件、上游 README 和包元数据整理；本次未联网验证链接当前是否可下载。已删除的 VRoid Samples、CubeWorld、Cyberpunk、PixelFPS、Space 等示例不再列入当前使用素材清单。

## 渲染

在 GPU 容器中进入项目目录后运行：

```bash
bash render.sh house
```

完整命令和配置说明见 [UTSProject/README.md](UTSProject/README.md)。

## 分轮交付资料

Round_02 及后续轮次的脚本、日志和验收资料统一收在 `UTSProject/Tools/Deliveries/Round_XX/`（相对 UnityProjects）；使用说明见 [分轮交付目录](UTSProject/Tools/Deliveries/README.md)。正式渲染结果的位置不变。
