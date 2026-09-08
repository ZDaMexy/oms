# P1-F 技术约束：发行后置与离线发布验收

1. 在 Phase 3 前不得借发行验收之名重新打开在线更新、默认 endpoint 或终端联网入口。
2. 便携发布、覆盖更新与离线首发口径必须与 `../../other/RELEASE.md` 保持一致。
3. 任何改变发行方式、覆盖更新结论或公开 release gate 的改动，都必须同步更新本目录四件套与 `../../mainline/`、`../../other/RELEASE.md`。
4. 当前正式发行压缩包命名以 `build-release.ps1 -> release-repo/oms_YYYYMMDD(.zip)` 为准；不要继续把现状写成泛化的 `OMS-Portable.zip`。
5. 覆盖更新保持用户原有portable选择和数据：便携模式保留 `portable.ini` 与 `data/`；非便携模式覆盖带marker的新包后、首次启动前仍保持exe旁没有 `portable.ini`。`storage.ini`由 `OsuStorage` 的基础数据根读取，便携模式位于 `data/`，非便携位于host默认数据根；必须保留，不假定在exe同级或已重定向的目标根。当前布局也不是“严格只有一个exe”。
6. 若后续改变内部 `game.Version` 口径，发行线变更不得破坏 changelog 跳转或配置迁移对非上游 `版本-流` 字符串的兼容性。
7. 当前工作区的 `build-release.ps1` 运行稳定性不应依赖 Python 终端自动激活；`.venv` 不属于 OMS 正式发行链，工作区需保持 `python.terminal.activateEnvironment = false`，避免新 PowerShell 终端在前台 `dotnet publish` 期间被自动激活命令打断。
