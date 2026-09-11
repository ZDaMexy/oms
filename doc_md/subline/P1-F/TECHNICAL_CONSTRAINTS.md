# P1-F 技术约束：发行后置与离线发布验收

1. 在 Phase 3 前不得借发行验收之名重新打开在线更新、默认 endpoint 或终端联网入口。
2. 便携发布、覆盖更新与离线首发口径必须与 `../../other/RELEASE.md` 保持一致。
3. 任何改变发行方式、覆盖更新结论或公开 release gate 的改动，都必须同步更新本目录四件套与 `../../mainline/`、`../../other/RELEASE.md`。
4. 当前正式发行压缩包命名以 `build-release.ps1 -> release-repo/oms_YYYYMMDD(.zip)` 为准；不要继续把现状写成泛化的 `OMS-Portable.zip`。
5. 覆盖更新保持用户原有portable选择和数据：便携模式保留 `portable.ini` 与 `data/`；非便携模式覆盖带marker的新包后、首次启动前仍保持exe旁没有 `portable.ini`。`storage.ini`由 `OsuStorage` 的基础数据根读取，便携模式位于 `data/`，非便携位于host默认数据根；必须保留，不假定在exe同级或已重定向的目标根。当前布局也不是“严格只有一个exe”。
6. 若后续改变内部 `game.Version` 口径，发行线变更不得破坏 changelog 跳转或配置迁移对非上游 `版本-流` 字符串的兼容性。
7. 当前工作区的 `build-release.ps1` 运行稳定性不应依赖 Python 终端自动激活；`.venv` 不属于 OMS 正式发行链，工作区需保持 `python.terminal.activateEnvironment = false`，避免新 PowerShell 终端在前台 `dotnet publish` 期间被自动激活命令打断。
8. 随包 `Update-OMS.ps1` 只操作通过 `release-files.json` 完整性验证的新包文件；新旧目录不得相同或互相包含，拒绝 reparse/路径冲突与运行中覆盖。目标原 `portable.ini` 的存在状态及字节、`data/`、基础目录 `storage.ini` 和外部数据根均保持；普通程序文件通过同卷 replace 备份，canonical 按下一条原样移动。中断保留 receipt/old/new 与修复指引，不承诺跨文件事务或猜测清理。
9. 正式保底原件的内容完整性由内嵌校验与只读打开约束；两款原件均受发行文件清单校验。ReadOnly 属性只作操作保护，不是完整性 authority。ZIP 显式携带 DOS 只读属性，发行验收使用实际能保留属性的 Windows 标准解包链，记录来源/副本属性而不在启动前补标；不得把 .NET/Expand-Archive 的属性丢失混称为完整保留。游戏不为此写原件属性。更新工具只在本次私有暂存中给新 canonical 副本设 ReadOnly；旧原件不改内容或属性，以 no-replace move 移入本次 old，再将新件 no-replace move 到目标，保护已有及准备期间新增硬链接的外部作者原件。两次移动间中断可暂缺安装原件，用同一完整新包重试并保留旧现场，不称单次原子替换；工作副本的原子恢复合同不变。
10. 发行物必须携带可独立执行的作者工具全部运行文件、双包源文件、模板/说明、普通 `.osk`、集中验收入口与更新工具。作者工具必须发布到本次唯一新目录后复制整份输出，不复用旧 bin 或猜测删除未知内容。不得用开发 SDK 可用替代无 SDK 作者体验。作者工具已独立验证的 self-contained single-file 方式不随游戏改为多文件而改变，不能混同两者的发布约束。
11. 原 V-001/V-005 输入固定于 `skin-c7-acceptance/legacy/`，原字节、原清单和摘要同时保留。发行与验收组装在写入输出前验证固定文件，不从未提交的 `artifacts/` 猜取或重新生成原验收输入；组装还须确认安装原件与作者套件中的同名成品一致。
12. 游戏采用 `PublishSingleFile=false` 的完整自包含多文件 ZIP，直接运行根目录 `osu!.exe`，保留实际发现需要的规则集 DLL 和全部运行文件。完整自解压会把 `AppContext.BaseDirectory` 移到 TEMP，不能再作为 portable/canonical 路径合同；只去掉 `IncludeAllContentForSelfExtract` 也不能满足 `RulesetStore` 的物理 DLL 发现。游戏发布方式改变必须经过真实发行副本复验，不能沿用旧 single-file 窗口/进程 smoke。
13. Desktop 建立宿主时将 `OsuGameDesktop.IsPortableMode` 传入 `HostOptions.PortableInstallation`。便携用户库由程序旁 `data/` 及其 `storage.ini` 选择，框架缓存位于程序旁 `cache/`；缓存不等于用户库，也不随该指针重定向。发行验收必须记录实际 `client.realm`、本轮日志、工作副本与缓存位置，不能以 marker 存在或进程存活推定隔离成立。非便携真实运行在独立账户/虚拟机验证，不能让候选包接触当前账户已有库。
14. 发生误入已有数据根时保全实际数据、指针及运行证据，不记录公开账户路径。事后逐字节备份不构成事前快照；未经事前后对应证据不得宣称无损或回滚成功，不猜测删除、降级数据库或恢复未知旧状态。本轮 Realm schema 57 迁移必须按这一边界记录。
