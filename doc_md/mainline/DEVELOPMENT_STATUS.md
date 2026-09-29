# OMS 当前开发状态

> 最后核对：2026-09-30（文档与记忆专项复核；产品验证仍为 2026-09-29，皮肤专项仍为 2026-09-14）
> 本页只保留全局状态与风险。执行顺序见[当前计划](DEVELOPMENT_PLAN.md)，专项事实从[子线路由](../subline/README.md)进入，历史见[CHANGELOG](CHANGELOG.md)。

## 一句话状态

OMS处于Phase 1.x后段。complex 已退役；用户认可静线最近调整并暂停外观打磨，已按授权收简设置，移除外部注册工作区，固定皮肤目录配打开/刷新，并恢复原组件布局编辑器；保存独立副本后按正常流程应用。BMS 与 osu!mania 可分别保存皮肤并在切换时应用，旧全局配置会在首次启动复制到两个模式。静线是唯一内置、首次默认与正式保底；星轨不再是内置选项、启动依赖或构建对象，历史作者文件仅保留参考。静线与剩余设备/长期体验仍未签收，Skin V1 与 release 未完成。详见 [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)。

## 产品与仓库基线

静线已完成当前控制区、分段血槽、轨宽及演奏信息迭代：白黑/皿轨独立比例，BGA布局由皮肤声明，作者标级与表名/表内等级独立显示。正常开发启动、build/publish只同步simple源。当前暂停继续打磨，既有成果、最新验证和未完成门统一见[P1-A状态](../subline/P1-A/DEVELOPMENT_STATUS.md)；历史双内置和各轮截图不作为新的签收。

- Windows-only，保留osu!mania与第一类BMS，Osu/Taiko/Catch已删除；离线优先，Phase 3前OMS私有服务与默认endpoint为空。用户主动添加公共BMS难度表URL仅是既有窄例外。
- BMS直读`chartbms/`，mania直读`chartmania/`；支持portable `data/`与自定义数据根。主要工程为`osu.Desktop.slnf`、`osu.Game.Rulesets.Bms`及`oms.Input`。
- 当前协作分支为`master`。皮肤恢复/数据门`SV1-0`已关闭；迁移归档和四个无authority orphan blob继续保全，不能由scanner认领或清理。恢复事实见[恢复审计](../other/SKIN_SYSTEM_RECOVERY_20260710.md)及[数据/实机报告](../other/SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)。
- 2026-09-30 专项复核开始时工作区干净，HEAD 为 `8c22fdf`；`git fetch origin` 成功，领先 `origin/master` 9、落后 0。本地产品实现已提交，尚未推送；此基线不代表远端已含这些改进。

## 当前执行门与全局风险

| 顺序 | 当前事实与下一道门 | 归属 |
| --- | --- | --- |
| 1 | 静线当前迭代已留存实绘与回归证据，外观打磨按用户决定暂停；按模式选择、固定目录刷新与原组件编辑恢复已完成限定切片，不恢复complex或自行推进视觉下一阶段 | [P1-A](../subline/P1-A/DEVELOPMENT_PLAN.md) |
| 2 | canonical普通简洁包接管已实现；旧OmsSkin只保留历史/人工对照，物理删除仍待实机门 | [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md) |
| 3 | 输入软件基线可用；analog scratch跨设备、校准与真实HID尚未闭合 | [P1-B](../subline/P1-B/DEVELOPMENT_STATUS.md)、[P1-D](../subline/P1-D/DEVELOPMENT_STATUS.md) |
| 4 | 真实LN/CN/HCN、音频/特殊谱、BGA、选歌大库与发行组合仍需验收；P1-L仍逐viewport创建player，单content/decoder未完成 | [子线路由](../subline/README.md) |
| 5 | V-001～V-005及候选发行包人工签收；2026-07-14恢复验收不能代替新增视觉与最终包验证 | [集中清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)、[P1-G](../subline/P1-G/DEVELOPMENT_STATUS.md) |

谱库缺失恢复与历史保全、难度表当前页刷新、长伴奏暂停保位、手动转谱长条发声和单轨上限筛选已实现；软件验证与未完成的真实大库、交互和听感门分别见 [P1-H](../subline/P1-H/DEVELOPMENT_STATUS.md)、[P1-I](../subline/P1-I/DEVELOPMENT_STATUS.md)、[P1-J](../subline/P1-J/DEVELOPMENT_STATUS.md)。发行覆盖须保持原便携模式，非便携真实设备与公开发行组合验收归P1-F/P1-G。

此前 C7 完整包已完成实际安装恢复及从上一修复版跨版本覆盖，原保存根前后字节和属性一致；旧候选误入已有保存根的事故仍只有事后保全、没有该根事前快照，不能追溯宣称无损。发行和数据边界见 [P1-F 状态](../subline/P1-F/DEVELOPMENT_STATUS.md)。

皮肤安全与失败回退详见[P1-A技术约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)：当前并无live gameplay reload或watcher，external永久只读；授权撤销不扩大Reload准入，C6完成不等于C7或人工门关闭。异常期归档只能定点取证。局部自动测试不能代替完整真实选择链，自动证据也不能替代视觉、硬件或特殊Gimmick证明。

## 最近一次验证

2026-09-29：谱库、声音与单轨筛选的软件验证已完成，Release 编译通过；完整回归中的既有失败已逐项对照，不能称全套全绿。精确证据与人工边界见[验证记录](../other/EXPERIENCE_CLOSURE_20260929.md)。真实大库、操作手感、设备听感与发行签收仍未完成。

2026-09-29：BMS 与 BMS→mania 分别新增默认关闭的自动键音，按谱面时刻播放，仍由真实操作决定成绩；软件证据、完整回归旧失败与真实听感待验收边界见 [P1-J 状态](../subline/P1-J/DEVELOPMENT_STATUS.md#最近一次验证)。

2026-09-29：按用户指定优先级完成「自动调整偏移」互斥 style；旧 lazer 自动调整保留，beatoraja style 在 BMS 演奏时调整显示偏移。软件验证、完整回归的旧失败与真实设备待验收边界集中见 [P1-C 状态](../subline/P1-C/DEVELOPMENT_STATUS.md#最近一次验证)，不扩大发行签收。

2026-09-22：BMS TOTAL 已区分作者声明与各家族缺省，并为新旧成绩选择对应算法；专项验证与完整回归的具名失败归因见 [TOTAL 报告](../other/BMS_TOTAL_RULES_AUDIT_20260922.md)。该结果不更新皮肤、真实设备或发行签收。

2026-09-14的设置/编辑切片已验证固定目录刷新、原编辑控件、独立副本保存与模式偏好隔离；未刷新视觉、设备或 release 人工门。详见 [P1-A 状态](../subline/P1-A/DEVELOPMENT_STATUS.md#最近一次验证)。

皮肤代码仍以 2026-09-14 的 `701893f`（原编辑器与固定目录）及 `36eb79c`（按模式选择）为最近修改。2026-09-13 的 `234ce1f` 是最近一次静线外观修改；该轮实绘、自动结果与较早 core/mania 证据见[轨道验证记录](../other/SKIN_SIMPLE_LANE_PROPORTIONS_20260913.md)。此前发行ZIP和安装证据未随后续产品修改重新验收。

## 文档治理验证

2026-09-29 开发存储治理：新增进程级 `UseDevelopmentStorage.ps1`，按 [AGENTS 开发磁盘约束](../../AGENTS.md#开发磁盘约束)将后续命令的临时文件、NuGet 与 .NET CLI/解包缓存放在非系统盘 checkout 的 `.dev-cache`。真实无依赖探针 restore/build/run、NuGet 路径、PowerShell 5.1 和系统盘拒绝 fixture 已验证；未运行产品回归。旧 C 盘产物清理被工具策略拦截、用户尚未手动执行，不能记为释放；Codex 全局数据未迁移，仍可能增长。

2026-09-30：对照已提交源码、留存 TRX、失败对照及 Release 日志，修正难度表当前页刷新、暂停保位与手动长条在合同和记忆中的旧描述；精简重复进度，补清旧发行物不覆盖后续功能与 schema 58 的边界。仅文档与记忆治理，未重跑产品测试或新增实机签收；检查结果见[同步记录](CHANGELOG.md#2026-09-30)。
