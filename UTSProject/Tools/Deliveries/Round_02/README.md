# Round_02

八个模型已迁移并完成渲染，退出码为 0。结果汇总见 `reports/completion.json`；迁移、渲染及图像检查见 `reports/`。

正式场景、材质、模型配置和渲染结果的位置未改变。详见 [统一分轮说明](../README.md)。

容器内重新渲染：`bash Tools/Deliveries/render_round.sh Round_02`。

服务器宿主机重新渲染：`bash UTSProject/Tools/Deliveries/Round_02/scripts/render_on_server.sh`（在 UnityProjects 目录执行）。

`scripts/job_wrapper.sh` 可作为后台任务入口，并将退出码写入 `logs/job.exit`。`archives/initial_migration.sh` 仅保存首次迁移历史，不作为重复执行入口。

目录整理前后对正式 Assets、configs 和已有渲染输出进行了完整性核对；结果见 `reports/organization_verification.json`。

## 简短模型名

porsche, crystal, suv, bunny, shop, mario, quesillo, witch。场景、FBX、GLB、模型目录、渲染配置和结果目录已统一更名；旧名对照见 `reports/rename_mapping.json`。

## 简短模型命名

当前模型名：`porsche`、`crystal`、`suv`、`bunny`、`shop`、`mario`、`quesillo`、`witch`。场景、FBX/GLB 文件、模型资源目录、模型配置目录和正式输出目录均使用该命名。旧名称对照见 `reports/rename_mapping.json`。

改名后已用正常 GPU 渲染入口重渲染 16 张参考图，全部与原图逐字节一致；2016 张既有正式图片保留并重新通过校验。验收结果见 `reports/rename_verification.json`。单模型可在 GPU 容器的 UTSProject 下运行 `bash render.sh porsche`；整轮命令不变。历史归档保留原始名称用于追溯。

GLB 输入目录按 Round_01 格式同步保留 `<model>.glb`、`view0.png`、`view1.png`、`cameras.json`。整轮渲染成功后，统一入口会自动同步参考图与相机文件；完整环绕和几何遮罩仍在 comparison/unity/val。核对结果见 `reports/glb_reference_validation.json`。
