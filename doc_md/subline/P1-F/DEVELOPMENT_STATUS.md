# P1-F 开发进度：发行后置与离线发布验收

> 最后核对：2026-09-30（补齐新功能与数据库升级的发行边界；发行启动验证仍为 2026-09-12）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，当前执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

## 当前阶段

用户已放弃 complex，并接受静线当前调整、暂停继续打磨。静线为唯一内置、默认与保底；星轨不再是启动依赖或构建对象。历史作者文件保留参考，旧内置星轨选择迁回静线，普通用户导入皮肤不清除；不再要求星轨视觉签收。退役迁移与单内置发行检查已通过，具体结果见下文。

此前双内置构建更新、已保存星轨的冷启动修复候选 `oms_20260912_2.zip`，以及旧 C7 安装/覆盖测试均保留历史身份，见[构建更新记录](../../other/SKIN_BUILTIN_BUILD_20260912.md)与[旧 C7 验证](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md)。它们不证明当前仅静线候选已经发行验收。

离线完整自包含多文件 ZIP、便携/自定义根、保留用户数据的覆盖更新合同不变；非便携实际启动仍须独立账户或虚拟机，不接触当前账户原保存根。静线画面、设备及长期体验仍未签收。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 完整发行与便携保存基线 | 自动验证通过 | 此前真实 ZIP、实际保存/缓存位置、两玩法可用及正常退出均有证据 |
| 只读安装原件、恢复与覆盖 | 自动验证通过 | 坏工作副本保全后恢复；同包覆盖保留自定义保存指针，跨版本另证便携用户文件与原模式保持，旧只读原件留在备份 |
| 离开开发环境的制作与验收组装 | 已验证 | 最终作者程序独立制作演练、PS5 无 Git/SDK 的真实包组装均已完成 |
| 在线更新关闭基线 | 已完成 | 离线完整包与随包工具覆盖，不进入 Velopack 自更新链 |
| 公开发行物人工验收 | 待签收 | 依赖 P1-A/P1-G 的画面、设备及长期体验，不宣称 Skin V1 或 release 完成 |

## 最近一次验证

最近一次发行启动验证为 2026-09-12，以下候选与隔离启动结果固定为形成时的快照。其后的 9 月 13 日静线外观、9 月 14 日皮肤设置/编辑与按模式选择、9 月 22 日 TOTAL 及历史成绩兼容、9 月 29 日自动偏移/自动键音、谱库 schema 58 与失效恢复、暂停续播及单轨筛选均未由该 ZIP 的旧结果验收。对应产品验证见 [P1-A STATUS](../P1-A/DEVELOPMENT_STATUS.md)、[P1-C STATUS](../P1-C/DEVELOPMENT_STATUS.md) 与[体验验证记录](../../other/EXPERIENCE_CLOSURE_20260929.md)，不能替代新候选的打包、启动或人工发行矩阵。

验证候选为 release-repo/oms_20260912_4.zip。正常 Release publish/打包、单内置构建 fixture、独立作者套件制作与错误拒绝、集中验收目录组装通过。独立副本在缺少 complex 原件、预先保存旧星轨配置的情况下，首次启动/custom root/损坏副本恢复/同包覆盖四轮正常关闭，配置已保存为 simple；证据 artifacts/simple-only-startup-20260912/results.json。该候选修正了作者脚本 UTF-8 BOM，386 个游戏运行文件逐字节匹配该四轮启动来源，作者检查脚本匹配实际通过的独立套件副本（artifacts/simple-only-final-publication.log）；未再次宣称 ZIP 解包或已有个人库迁移签收。集中目录组装记录 artifacts/simple-only-acceptance.log；制品保持生成时快照，之后仅将组装结束提示改为静线已内置、作者包按需导入，不改变组装或游戏行为。单内置迁移与自动回归结论见 [P1-A 同期记录](../P1-A/CHANGELOG.md#放弃星轨并恢复唯一内置静线)。以下为更早的发行证据，不计为本轮通过。

此前 C7 ZIP 为 344,240,108 B，SHA256 `76f1e7d91581a8c4aad5f3f0da2e64a3e47930b6259ec9fdc3f8be9105035574`；发行清单 SHA256 为 `5bd05386bdb687774a6a8b295b00581ef3a27b343aaabd8300011649af4b8ac0`。`artifacts/skin-startup-warning-20260911/final-delivery/delivery.json`、同目录的 `release-extracted-extraction.json`、`acceptance-assembly.json` 固定本次来源与实际组装。四轮证据为 `artifacts/skin-c7-evidence/release-startup-startup-fix-final/results.json`（UTC 2026-09-11 16:22:49～16:25:13），均正常退出、退出码 0、无强制终止；两处共享测试根的只读玩法结果见当前证据目录的 `rulesets-portable.log`、`rulesets-custom.log`。

跨版本证据为 `artifacts/skin-c7-evidence/release-startup-preview-upgrade-startup-fix-final/results.json`（UTC 16:25:24～16:26:35）。其中玩法检查的 Pending 字段是该记录形成时的另步待查；随后已在 `artifacts/skin-startup-warning-20260911/final-delivery/rulesets-upgrade.log` 及其指向的独立只读副本记录中完成，两玩法均可用，未改写旧证据。更新工具前后用户库字节相同；实际游戏启动允许更新自己的测试库，不把它说成整个游玩期间数据库不变。完整开发证据与自动检查边界集中在 [C7 验证记录](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md)。

该次 C7 受保护运行前后，旧测试安装、旧发行来源、新发行来源，以及账户 bootstrap、事故保存根与原 G1 根的全部文件字节和属性均保持相同，未直接打开原数据库。该结果不能追溯证明首次事故没有影响。

## 首次事故与证据边界

旧完整自解压发行曾把 `AppContext.BaseDirectory` 移到临时解压目录，未读到安装旁的 `portable.ini`，误入当前账户既有自定义保存根并执行 Realm schema 57 迁移。事后数据和指针已完整逐字节保全；没有该根的事前快照，不能宣称无损、恢复原状或可回滚。事后第二副本只读可读性检查不证明迁移前后内容相同，不能用旧 corrupt 库猜测覆盖或自动删除，公开文档不披露账户路径。

2026-05-09 的打包、fresh extract 和八秒 smoke 历史保留，但窗口/进程观察未证明实际保存根，不能作为便携隔离依据。后续多文件包的明确保存根与正常退出证据替代该 smoke 的隔离结论，不抹去事故。

## 文档治理验证

2026-09-30：对照当前 schema 与 9 月 29 日软件验证，补齐旧候选尚未覆盖的玩法、谱库、暂停续播和筛选范围；计划增加实际候选隔离升级与历史保全验收。未重打发行物、运行游戏或重验 9 月 12 日启动结果；schema 57 事故、公开制品摘要和人工边界保持，历史见 [CHANGELOG](CHANGELOG.md)。后续候选按 [PLAN](DEVELOPMENT_PLAN.md) 与 [RELEASE](../../other/RELEASE.md) 复验。
