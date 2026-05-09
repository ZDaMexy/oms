# P1-F 变动日志

## 2026-05-09

### single-file 发行物补齐完整自解压并复核冷启动

- `build-release.ps1` 现已在 `PublishSingleFile=true` 之外显式保留 `IncludeAllContentForSelfExtract=true`，避免 fresh extract 的便携发行物首次运行只创建 `data/` 后就无窗退出。
- 这次修正不改变当前离线便携发布策略：正式发行物仍是 `oms_YYYYMMDD(.zip)`、`portable.ini -> data/` 与手工覆盖更新，只是把 single-file 冷启动合同补回到可发布状态。
- 验证：重新执行 ` .\build-release.ps1 ` 后，新解压的便携 zip 冷启动通过；`.\SmokeTestDesktop.ps1 -Configuration Release -WaitSeconds 8` 通过。

## 2026-05-09

### 工作区关闭 Python 终端自动激活以稳定发行脚本运行

- 工作区级 `.vscode/settings.json` 现已加入 `python.terminal.activateEnvironment = false`，避免 VS Code 直接点 Run 执行 `build-release.ps1` 时，新 PowerShell 终端又被 `.venv` 自动激活命令打断。
- 这次修正不改变 OMS 正式发行链：仓库当前没有 Python 源文件、`pyproject.toml`、`requirements.txt` 或 Python 任务；根目录 `.venv/` 仅为本地工作区环境，不属于 OMS 正式构建 / 测试 / 发行链。
- 验证：工作区 `.vscode/settings.json` 已更新且无错误；仓库级 `.vscode/settings.json` / `.vscode/tasks.json` 未发现项目级 Python 依赖配置。

## 2026-05-09

### 发行包新增中英双语 `how to update.txt`

- `build-release.ps1` 现会在发行根目录生成 `how to update.txt`，并随 `oms_YYYYMMDD(.zip)` 一起打包。
- 该文件同时提供中文与英文的手动覆盖更新步骤，并以更精炼的终端用户口径强调：先退出程序、覆盖整个压缩包内容，以及在便携模式下保留 `portable.ini` 与 `data/`。
- `../../other/RELEASE.md` 与本子线状态文档已同步到“发行根目录额外包含一份中英双语手动更新说明”的口径。
- 验证：PowerShell 语法解析通过；实际执行 ` .\build-release.ps1 ` 成功生成 `release-repo/oms_20260509_2.zip`，且 `publish/` 与 zip 根目录均已确认包含 `how to update.txt`。

## 2026-05-09

### 离线覆盖更新审计与发行命名口径同步

- `P1-F` 当前正式打包入口已明确为 `build-release.ps1`，输出 `release-repo/oms_YYYYMMDD(.zip)`；发行根目录继续保留 `osu!.exe` + `portable.ini` + `lazer.ico` + `beatmap.ico`，不再把当前 release 误写成“严格只有一个 exe”。
- 手工覆盖更新链已重新审计：`portable.ini -> data/` 与 `storage.ini` 自定义数据根路径继续成立，覆盖后不会进入 Velopack / 安装器自更新；当前实际风险主要是程序未退出时覆盖文件，或误删 `portable.ini` / `storage.ini` 造成数据根切换。
- 同轮已把 `../../other/RELEASE.md`、`../../mainline/DEVELOPMENT_STATUS.md`、`../../mainline/CHANGELOG.md` 与根 `README.md` 同步到当前发行物命名、覆盖更新步骤和注意事项口径。
- 验证：`dotnet build .\osu.Game\osu.Game.csproj -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

## 2026-04-20

### 子线正式建档

- `P1-F` 已建立独立目录与四件套文档。
- 当前仅完成文档结构治理，未新增代码、构建或测试执行。