# P1-F 技术约束：发行后置与离线发布验收

1. 在 Phase 3 前不得借发行验收之名重新打开在线更新、默认 endpoint 或终端联网入口。
2. 便携发布、覆盖更新与离线首发口径必须与 `../../other/RELEASE.md` 保持一致。
3. 任何改变发行方式、覆盖更新结论或公开 release gate 的改动，都必须同步更新本目录四件套与 `../../mainline/`、`../../other/RELEASE.md`。
4. 当前正式发行压缩包命名以 `build-release.ps1 -> release-repo/oms_YYYYMMDD(.zip)` 为准；不要继续把现状写成泛化的 `OMS-Portable.zip`。
5. 覆盖更新保持用户原有portable选择和数据：便携模式保留 `portable.ini` 与 `data/`；非便携模式覆盖带marker的新包后、首次启动前仍保持exe旁没有 `portable.ini`。`storage.ini`由 `OsuStorage` 的基础数据根读取，便携模式位于 `data/`，非便携位于host默认数据根；必须保留，不假定在exe同级或已重定向的目标根。当前布局也不是“严格只有一个exe”。
6. 若后续改变内部 `game.Version` 口径，发行线变更不得破坏 changelog 跳转或配置迁移对非上游 `版本-流` 字符串的兼容性。
7. 当前工作区的 `build-release.ps1` 运行稳定性不应依赖 Python 终端自动激活；`.venv` 不属于 OMS 正式发行链，工作区需保持 `python.terminal.activateEnvironment = false`，避免新 PowerShell 终端在前台 `dotnet publish` 期间被自动激活命令打断。
8. 随包 `Update-OMS.ps1` 只操作通过 `release-files.json` 完整性验证的新包文件；新旧目录不得相同或互相包含，拒绝 reparse/路径冲突与运行中覆盖。目标原 `portable.ini` 的存在状态及字节、`data/`、基础目录 `storage.ini` 和外部数据根均保持；每个原程序文件通过同卷 replace 备份，中断保留 receipt/old/new 与修复指引，不承诺跨文件事务或猜测清理。
9. 双 canonical 文件的内容完整性由内嵌校验与只读打开约束；文件 ReadOnly 属性只作操作保护，不是完整性 authority。发行和验收启动设置该属性；更新工具只可对清单已验证的两条 canonical 安装路径暂时解除旧 ReadOnly 并在替换后恢复，永远不触及作者外部目录。ZIP 解压是否保留属性不能替代运行时校验。
10. 发行物必须携带可独立执行的作者工具全部运行文件、双包源文件、模板/说明、普通 `.osk`、集中验收入口与更新工具。不得用开发 SDK 可用替代无 SDK 作者体验；这些旁路文件不得被 single-file 清理规则误删。
