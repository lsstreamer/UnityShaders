# 素材清理记录（2026-11-19）

本次依据 `/home/sirui/workspace/shaderNPR/comparison/unity` 的现有输出、模型配置、批处理脚本和 Unity 资源 GUID 依赖进行清理。三个工程均参与参考图生成，全部保留。

| 工程 | 删除资源数 | 含配套 .meta 的文件数 | 删除大小 |
| --- | ---: | ---: | ---: |
| ToonRPProject | 175 | 350 | 25.85 MiB |
| URPToonProject | 123 | 246 | 36.58 MiB |
| UTSProject | 422 | 844 | 120.41 MiB |
| 合计 | 720 | 1,440 | 182.84 MiB |

## 清理内容

- ToonRP：删除 Main 以外的 12 个示例场景，以及无保留资源引用的模型、材质、贴图、光照数据和动画。保留 Aika 的 Main 场景、递归依赖和 Comparison 中动态读取的衣服贴图。
- URP Toon：删除 10 个无关原示例场景、10 个首轮预览场景，以及未引用的石头、测试模型、城市动画、贴图与烘焙光照文件。保留 9 个角色的 Round2 场景、6 个生成用原场景、采样用男女 Idle 动画和相关依赖。
- UTS：删除无引用的角色语音、BoxUnityChan 等闲置资源、额外贴图和闲置设置。保留 val 场景与模型、ChopChop 场景及动态加载的 PreviewSettings、官方角色场景、完整 SD 角色目录和 UnityChanTPK 六角色资源。

所有删除均配套删除同名 `.meta`。代码、Shader、Resources、StreamingAssets、许可文件、Packages 和 ProjectSettings 保留。模型的导入器依赖、Shader Graph GUID 和可能通过文件名关联的同目录贴图纳入保留检查。对缺少持久化调用配置的角色包采用保守保留。

`Comparison/` 保留：其中部分 JSON 是 artist 导出脚本的输入，Aika 的衣服贴图也通过文件路径动态读取。`Library/` 是导入和编译缓存，本次专注素材清理，未清除这些缓存。原 README 中的完整上游示例和首轮预览场景说明属于清理前记录。

## 验证及限制

- 删除前计算场景、材质、模型导入器及项目设置的资源 GUID 依赖闭包，删除列表不得包含任何保留文件引用的资源。
- 删除前后对 3,263 个保留的 Assets 文件做 SHA-256 核对，内容一致。
- 对 comparison/unity 下 10,560 个输出文件核对大小和修改时间，均未变化。
- 删除后检查全部剩余 Assets 文件，确认没有引用本次已删除资源的 GUID；保留入口文件均存在。
- 本次未启动 Unity 编辑器或执行 GPU 重新渲染；验证结论限于静态依赖与文件完整性。

## 临时恢复文件

删除前已逐文件校验并归档全部待删文件，包括 Git 忽略的外部下载素材：

- 完整清单、大小和 SHA-256：`/tmp/unity-assets-cleanup/manifest.json`
- 恢复备份：`/tmp/unity-assets-cleanup/removed-assets.tar.gz`（约 93.21 MiB）

恢复全部已删素材可运行：

```bash
tar -xzf /tmp/unity-assets-cleanup/removed-assets.tar.gz -C /home/sirui/workspace/UnityProjects
```

备份位于项目目录之外，仍占用临时目录空间；上述 182.84 MiB 为项目内删除文件的合计大小。`/tmp` 可能被系统清理，需要长期保存时请另行迁移。恢复素材后，README 顶部的清理说明也应相应调整。
