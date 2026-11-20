# 分轮交付资料

每轮使用一个目录：`Round_02/`、`Round_03/`、`Round_04/`。不在 UnityProjects 或 Unity 工程根目录散放迁移脚本、日志和压缩包。

- `models.txt`：该轮模型名，供统一渲染入口读取。
- `scripts/`：该轮迁移、校验和服务器辅助脚本。
- `reports/`：清单、校验与完成记录。
- `logs/`：运行日志、退出码。
- `previews/`：审阅缩略图。
- `archives/`：原始上传包、历史脚本、抽样包及源 GLB 备份。

正式 Unity 资源仍位于 `Assets/Scenes/Round_XX/` 和 `Assets/Models/Round_XX/`；模型渲染配置仍使用 `configs/val_models/<model>/`。正式输出保持 README 中的 `shaderNPR/comparison/unity/val/<model>/` 格式。

在 GPU 容器的 UTSProject 目录中运行：

```bash
bash Tools/Deliveries/render_round.sh Round_02
```

仅列出模型，不渲染：

```bash
bash Tools/Deliveries/render_round.sh Round_02 --list
```

后续轮次创建同名目录和 `models.txt` 即可复用此入口。生成的 archives、logs、previews 不进入 Git；脚本、模型列表和小型验收报告可跟踪。
