# P1-F 变动日志

## 2026-05-09

### 发行包新增中英双语 `how to update.txt`

- `build-release.ps1` 现会在发行根目录生成 `how to update.txt`，并随 `oms_YYYYMMDD(.zip)` 一起打包。
- 该文件同时提供中文与英文的手动覆盖更新步骤，以及当前离线更新链最关键的注意事项：先退出程序、保留 `portable.ini`、保留便携模式下的 `data/`，以及在自定义数据根场景保留 `storage.ini`。
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