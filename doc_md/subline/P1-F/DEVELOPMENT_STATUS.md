# P1-F 开发进度：发行后置与离线发布验收

> 最后更新：2026-05-09

## 当前阶段

- `P1-F` 已完成子线建档。
- `OsuGameDesktop.CreateStorage()` 已按 `portable.ini -> data/` 接通便携数据根；当前正式打包入口已明确为 `build-release.ps1 -> release-repo/oms_YYYYMMDD(.zip)`，发行根目录保持 `osu!.exe` + `portable.ini` + `lazer.ico` + `beatmap.ico` + 中英双语 `how to update.txt`。`build-release.ps1` 当前还已固定保留 `IncludeAllContentForSelfExtract=true`，fresh extract 的 single-file 便携发行物冷启动与 8 秒 smoke 已复核通过。
- 手工覆盖更新路径已重新审计：用户数据仍与程序文件分离，游戏内在线更新继续保持禁用；当前主要操作风险是运行中覆盖文件，以及误删 `portable.ini` / `storage.ini` 导致数据根切换。
- 工作区级 `.vscode/settings.json` 现已关闭 `python.terminal.activateEnvironment`，避免 VS Code 直接点 Run 执行 `build-release.ps1` 时，新终端中的 `.venv` 自动激活打断前台 `dotnet publish`。
- 游戏内在线更新仍保持禁用，当前重点不是恢复安装器/在线更新，而是继续收口公开发行物产品面与最终 release gate。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 子线建档 | 已完成 | 四件套已建立 |
| 便携发布基线 | 已完成 | `build-release.ps1 -> oms_YYYYMMDD(.zip)`、`portable.ini -> data/`、single-file 完整自解压与覆盖更新路径结论已对齐 |
| 在线更新关闭基线 | 已完成 | 当前仍维持离线优先，不恢复在线更新；手工覆盖后不会进入 Velopack 自更新链 |
| 公开发行物产品面验收 | 进行中 | 依赖 `P1-A` 的默认皮肤与 release gate 收尾 |
| 发布口径同步 | 进行中 | 需持续联动 `../../other/RELEASE.md` |

## 当前验证基线

- `build-release.ps1` 当前可稳定通过 PowerShell 语法解析并实际产出 `release-repo/oms_YYYYMMDD(.zip)`；最近一次实机打包已确认 `publish/` 与 zip 根目录都包含 `osu!.exe`、`portable.ini`、图标资源与中英双语 `how to update.txt`。
- `IncludeAllContentForSelfExtract=true` 已锁定 fresh extract 的 single-file 冷启动与 8 秒 smoke 基线；手工覆盖更新继续遵循“退出程序 -> 解压覆盖 -> 再启动”，并保留 `portable.ini`、便携模式下的 `data/` 与任何自定义数据根使用的 `storage.ini`。
- 未来内部 OMS 版号切换所需的最小兼容护栏已补齐：`ChangelogOverlay.ShowBuild(string)` 与 `OsuConfigManager.Migrate()` 已兼容不带上游 `-stream` 后缀的 `oms_YYYYMMDD` 版号。
- 当前 `osu.Game` Release 构建可通过；按日期展开的发行链修补与验证记录见 [CHANGELOG.md](CHANGELOG.md)。
