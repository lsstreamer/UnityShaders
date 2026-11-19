# UnityProjects

## 环境

- Unity Editor：`6000.3.24f1`
- 项目目录：`UTSProject/`
- Toon RP 候选工程：`ToonRPProject/`
- 渲染脚本和配置：`UTSProject/render.sh`、`UTSProject/configs/`

## 获取项目

```bash
git clone <仓库地址> UnityProjects
cd UnityProjects/UTSProject
```

用 Unity Hub 安装并打开 `6000.3.24f1`，选择 `UTSProject/`。首次打开时，Unity 会按照 `Packages/manifest.json` 和 `Packages/packages-lock.json` 自动下载 Unity Toon Shader、glTFast 等包；`Library/`、`Temp/`、`Logs/` 和 `UserSettings/` 是本地生成目录，不需要从仓库下载。

`ToonRPProject/` 是调研文档中第一个免费候选 `Delt06/toon-rp` 的独立工程。它原本声明 Unity 2022.3 / Core RP 14.0.9；已在不更换现有 `6000.3.24f1` 编辑器的前提下完成 Unity 6 适配，并保留上游 13 个示例场景、模型和材质。详细打开方式及验证结果见 [ToonRPProject/README.md](ToonRPProject/README.md)。

两个工程的外部包、素材来源和候选选型见 [TOON-NPR.md](TOON-NPR.md)。

## 下载外部角色包

`misaki_school_summer` 示例依赖 Unity Technologies Japan 发布的 **SD Kohaku-chanz** 角色包。请从官方页面下载：

<https://unity3d.jp/unity-chan_contents/releaseNote.php?id=SDKohakuchanz&lang=en>

解压后，将 `UTSOfficialCharacters` 目录放到下面的位置（目录名和大小写保持不变）：

```text
UnityProjects/
└── UTSProject/
    └── Assets/
        └── UTSOfficialCharacters/
```

打开 Unity 后等待导入完成。该目录及其 `.meta` 文件属于外部下载内容，已在 `UTSProject/.gitignore` 中忽略；请不要把它们提交到仓库。下载包中的 `SOURCE.txt` 和许可证文件应与角色资源一起保留。资源版权和使用条件以 Unity-Chan License 2.02 及官方发布页为准。

如果只运行 GLB 模型（例如 `house`），可以不下载角色包；运行 `unitychan_cel_look` 或 `misaki_school_summer` 前必须先完成上述下载。

## 渲染

在 GPU 容器中进入项目目录后运行：

```bash
bash render.sh house
```

完整命令和配置说明见 [UTSProject/README.md](UTSProject/README.md)。
