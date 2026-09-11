# P1-F 开发进度：发行后置与离线发布验收

> 最后更新：2026-09-11（完整多文件包、保存位置、恢复及覆盖后正常启动已实证；人工发行门未签收）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，当前执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

## 当前阶段

- [build-release.ps1](../../../build-release.ps1) 产出完整自包含多文件 ZIP（`PublishSingleFile=false`）。本轮实际包为 `release-repo/oms_20260911_4.zip`，完整解压后直接运行 `osu!.exe`，无需另装 .NET；玩法 DLL 和安装原件留在真实安装目录。
- 该包已通过 Windows Shell 实际解压与完整清单校验，两款 canonical 原件实际保留 ReadOnly/Archive，检查未补写属性。首次便携、自定义保存位置、损坏工作副本恢复、同一完整包覆盖后重启均已完成加载、八秒稳定运行和正常退出；没有以强制结束代替正常退出。
- 便携用户库位于程序旁 `data/` 或其中 `storage.ini` 指定的自定义根；`HostOptions.PortableInstallation` 与现有便携判断一致，使框架缓存留在程序旁 `cache/`。四轮结束后，分别从首次便携根与最终覆盖后的共享自定义根新复制数据库，用只读 SDK 确认 `bms`、`mania` 均为 `Available=true`；这不是四份独立时点数据库快照。
- 随包 `Update-OMS.ps1` 校验完整新包后保留原 portable 模式、基础目录 `storage.ini` 与用户数据，备份被替换程序；本次真实覆盖的是同一版本完整包，不冒充未执行的跨版本安装升级。已保留坏工作副本、更新前文件及中断修复说明；此前只读硬链接、两次移动间冲突和中断重试的隔离验证仍有效。
- 本轮作者工具、完整双包源文件、普通导入包、模板和验收工具均随真实发行物交付。最终可直接运行的集中验收目录为 `release-repo/oms-skin-c7-acceptance-20260911-final`；已用该真实包在 PS5、PATH 不含 Git/SDK 的进程中完成组装，来源字节及属性不变。随包作者程序与独立完成制作演练的版本摘要一致，见 [WORKSHOP](../../../skin-authoring/docs/WORKSHOP.md)。
- 原 V-001～V-004 仍为 0/4、V-005 未签收，C7 的画面、声音、真实设备与长期体验仍需人工确认。自动安装与恢复通过不代表整个 Skin V1 或发行版本完成；程序化 `OmsSkin` 仅保留历史对照和待签收移除边界，不再作为 canonical 损坏时的替代外观。
- 游戏内在线更新继续关闭，不恢复安装器、联网 endpoint 或增量更新承诺。非便携实际启动仍须在独立账户或虚拟机验收，不使用当前账户既有保存根试运行。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 完整发行与便携保存基线 | 自动验证通过 | 当前真实 ZIP、实际保存/缓存位置、两玩法可用及正常退出均有证据 |
| 只读安装原件、恢复与覆盖 | 自动验证通过 | 坏工作副本保全后恢复；真实同包覆盖保持用户库、指针和原模式；跨版本升级未借此宣称通过 |
| 离开开发环境的制作与验收组装 | 已验证 | 最终作者程序独立制作演练、PS5 无 Git/SDK 的真实包组装均已完成 |
| 在线更新关闭基线 | 已完成 | 离线完整包与随包工具覆盖，不进入 Velopack 自更新链 |
| 公开发行物人工验收 | 待签收 | 依赖 P1-A/P1-G 的画面、设备及长期体验，不宣称 Skin V1 或 release 完成 |

## 最近一次验证

最终 ZIP SHA256 为 `bcf6aa8da7700f822db6613734dfc4af20d4bf57c5ff6d29f25cf5c4b2ce9ac8`，发行清单 SHA256 为 `d7ccb5bc66abedef7bf4f25ffca93b9b25b3605ddc976b89d991669a07899d57`。`release-final4-extracted-extraction.json`、`release-startup-final4/results.json` 与 `acceptance-final4-assembly.json` 固定本次来源；四轮实际运行时间为 UTC 09:08:41～09:10:58，均 `NormalExit=true`、退出码 0、无强制终止，原发行来源保持不变。两处共享测试根的只读玩法检查见 `release-final4-rulesets-portable.log` 与 `release-final4-rulesets-custom.log`。完整开发证据与自动检查边界集中在 [C7 验证记录](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md)。

本次启动前后，账户 bootstrap、事故保存根与原 G1 根的全部文件字节和属性均保持相同，未直接打开这些原数据库。该结果只证明本次受保护运行没有改变它们，不能追溯证明首次事故没有影响。

## 首次事故与证据边界

旧完整自解压发行曾把 `AppContext.BaseDirectory` 移到临时解压目录，未读到安装旁的 `portable.ini`，误入当前账户既有自定义保存根并执行 Realm schema 57 迁移。事后数据和指针已完整逐字节保全；没有该根的事前快照，不能宣称无损、恢复原状或可回滚。事后第二副本只读可读性检查不证明迁移前后内容相同，不能用旧 corrupt 库猜测覆盖或自动删除，公开文档不披露账户路径。

2026-05-09 的打包、fresh extract 和八秒 smoke 历史保留，但窗口/进程观察未证明实际保存根，不能作为便携隔离依据。本轮多文件包的明确保存根与正常退出证据替代其当前结论，不抹去事故。

## 文档治理验证

当前发行说明、保存位置、缓存、同包覆盖及人工边界已同步到 [RELEASE](../../other/RELEASE.md)；后续候选包或相关实现改变后，按 [PLAN](DEVELOPMENT_PLAN.md) 重新验证。本次最终文档检查与提交由主线统一记录。
