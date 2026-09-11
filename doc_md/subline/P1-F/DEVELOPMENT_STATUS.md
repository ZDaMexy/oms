# P1-F 开发进度：发行后置与离线发布验收

> 最后更新：2026-09-12（启动提示修复包、正常退出及真实跨版本更新已实证；人工发行门未签收）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，当前执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

## 当前阶段

- [build-release.ps1](../../../build-release.ps1) 产出完整自包含多文件 ZIP（`PublishSingleFile=false`）。当前启动提示修复包为 `release-repo/oms_20260911_startup-fix-final.zip`，完整解压后直接运行 `osu!.exe`，无需另装 .NET；玩法 DLL 和安装原件留在真实安装目录。先前 `_4`、`preview-fix` 包及其结果保留为历史，本轮未改变部署入口或发行合同。
- 该包已通过 Windows Shell 实际解压与完整清单校验，两款 canonical 原件实际保留 ReadOnly/Archive，检查未补写属性。首次便携、自定义保存位置、损坏工作副本恢复、同一完整包覆盖后重启均已完成加载、八秒稳定运行和正常退出；没有以强制结束代替正常退出。
- 便携用户库位于程序旁 `data/` 或其中 `storage.ini` 指定的自定义根；`HostOptions.PortableInstallation` 与现有便携判断一致，使框架缓存留在程序旁 `cache/`。四轮结束后，分别从首次便携根与最终覆盖后的共享自定义根新复制数据库，用只读 SDK 确认 `bms`、`mania` 均为 `Available=true`；这不是四份独立时点数据库快照。
- 随包 `Update-OMS.ps1` 校验完整新包后保留原 portable 模式、基础目录 `storage.ini` 与用户数据，备份被替换程序。除四轮中的同包覆盖，还从旧 `preview-fix` 安装的独立副本完成真实跨版本更新：更新工具未改变用户文件、数据库或便携标记，两款旧只读原件留在备份中；旧工作副本在覆盖后仍原样保留，由游戏首次启动自动换成新版简洁款，并正常退出。随后第三处测试根的额外只读副本确认两种玩法均可用。此前只读硬链接、两次移动间冲突和中断重试的隔离验证仍有效。
- 本轮作者工具、完整双包源文件、普通导入包、模板和验收工具均随真实发行物交付。当前可直接运行的集中验收目录为 `release-repo/oms-skin-c7-acceptance-20260911-startup-fix-final`；已用该真实包在 PS5、PATH 不含 Git/SDK 的进程中完成组装，来源字节及属性不变。随包作者程序 SHA256 为 `be91924141e44c8b4422290430ba8a14998a2c345c1cb2daa242b1e22c8ef2fa`，与本轮公开制作流程的实际演练版本一致；制作入口见 [WORKSHOP](../../../skin-authoring/docs/WORKSHOP.md)。
- 原 V-001～V-004 仍为 0/4、V-005 未签收，C7 的画面、声音、真实设备与长期体验仍需人工确认。自动安装与恢复通过不代表整个 Skin V1 或发行版本完成；程序化 `OmsSkin` 仅保留历史对照和待签收移除边界，不再作为 canonical 损坏时的替代外观。
- 游戏内在线更新继续关闭，不恢复安装器、联网 endpoint 或增量更新承诺。非便携实际启动仍须在独立账户或虚拟机验收，不使用当前账户既有保存根试运行。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 完整发行与便携保存基线 | 自动验证通过 | 当前真实 ZIP、实际保存/缓存位置、两玩法可用及正常退出均有证据 |
| 只读安装原件、恢复与覆盖 | 自动验证通过 | 坏工作副本保全后恢复；同包覆盖保留自定义保存指针，跨版本另证便携用户文件与原模式保持，旧只读原件留在备份 |
| 离开开发环境的制作与验收组装 | 已验证 | 最终作者程序独立制作演练、PS5 无 Git/SDK 的真实包组装均已完成 |
| 在线更新关闭基线 | 已完成 | 离线完整包与随包工具覆盖，不进入 Velopack 自更新链 |
| 公开发行物人工验收 | 待签收 | 依赖 P1-A/P1-G 的画面、设备及长期体验，不宣称 Skin V1 或 release 完成 |

## 最近一次验证

当前 ZIP 为 344,240,108 B，SHA256 `76f1e7d91581a8c4aad5f3f0da2e64a3e47930b6259ec9fdc3f8be9105035574`；发行清单 SHA256 为 `5bd05386bdb687774a6a8b295b00581ef3a27b343aaabd8300011649af4b8ac0`。`artifacts/skin-startup-warning-20260911/final-delivery/delivery.json`、同目录的 `release-extracted-extraction.json`、`acceptance-assembly.json` 固定本次来源与实际组装。四轮证据为 `artifacts/skin-c7-evidence/release-startup-startup-fix-final/results.json`（UTC 2026-09-11 16:22:49～16:25:13），均正常退出、退出码 0、无强制终止；两处共享测试根的只读玩法结果见当前证据目录的 `rulesets-portable.log`、`rulesets-custom.log`。

跨版本证据为 `artifacts/skin-c7-evidence/release-startup-preview-upgrade-startup-fix-final/results.json`（UTC 16:25:24～16:26:35）。其中玩法检查的 Pending 字段是该记录形成时的另步待查；随后已在 `artifacts/skin-startup-warning-20260911/final-delivery/rulesets-upgrade.log` 及其指向的独立只读副本记录中完成，两玩法均可用，未改写旧证据。更新工具前后用户库字节相同；实际游戏启动允许更新自己的测试库，不把它说成整个游玩期间数据库不变。完整开发证据与自动检查边界集中在 [C7 验证记录](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md)。

本次受保护运行前后，旧测试安装、旧发行来源、新发行来源，以及账户 bootstrap、事故保存根与原 G1 根的全部文件字节和属性均保持相同，未直接打开原数据库。该结果不能追溯证明首次事故没有影响。

## 首次事故与证据边界

旧完整自解压发行曾把 `AppContext.BaseDirectory` 移到临时解压目录，未读到安装旁的 `portable.ini`，误入当前账户既有自定义保存根并执行 Realm schema 57 迁移。事后数据和指针已完整逐字节保全；没有该根的事前快照，不能宣称无损、恢复原状或可回滚。事后第二副本只读可读性检查不证明迁移前后内容相同，不能用旧 corrupt 库猜测覆盖或自动删除，公开文档不披露账户路径。

2026-05-09 的打包、fresh extract 和八秒 smoke 历史保留，但窗口/进程观察未证明实际保存根，不能作为便携隔离依据。本轮多文件包的明确保存根与正常退出证据替代其当前结论，不抹去事故。

## 文档治理验证

当前发行说明、保存位置、缓存、同包覆盖、真实跨版本更新及人工边界已同步到 [RELEASE](../../other/RELEASE.md)；后续候选包或相关实现改变后，按 [PLAN](DEVELOPMENT_PLAN.md) 重新验证。本次最终制品独立复核已通过，文档及提交记录统一见 C7 报告。
