# OMS 当前开发状态

> 最后核对：2026-09-29（文档与记忆专项审查；TOTAL 产品验证仍为 2026-09-22，皮肤专项仍为 2026-09-14）
> 本页只保留全局状态与风险。执行顺序见[当前计划](DEVELOPMENT_PLAN.md)，专项事实从[子线路由](../subline/README.md)进入，历史见[CHANGELOG](CHANGELOG.md)。

## 一句话状态

OMS处于Phase 1.x后段。complex 已退役；用户认可静线最近调整并暂停外观打磨，已按授权收简设置，移除外部注册工作区，固定皮肤目录配打开/刷新，并恢复原组件布局编辑器；保存独立副本后按正常流程应用。BMS 与 osu!mania 可分别保存皮肤并在切换时应用，旧全局配置会在首次启动复制到两个模式。静线是唯一内置、首次默认与正式保底；星轨不再是内置选项、启动依赖或构建对象，历史作者文件仅保留参考。静线与剩余设备/长期体验仍未签收，Skin V1 与 release 未完成。详见 [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)。

## 产品与仓库基线

静线已完成当前控制区、分段血槽、轨宽及演奏信息迭代：白黑/皿轨独立比例，BGA布局由皮肤声明，作者标级与表名/表内等级独立显示。正常开发启动、build/publish只同步simple源。当前暂停继续打磨，既有成果、最新验证和未完成门统一见[P1-A状态](../subline/P1-A/DEVELOPMENT_STATUS.md)；历史双内置和各轮截图不作为新的签收。

- Windows-only，保留osu!mania与第一类BMS，Osu/Taiko/Catch已删除；离线优先，Phase 3前OMS私有服务与默认endpoint为空。用户主动添加公共BMS难度表URL仅是既有窄例外。
- BMS直读`chartbms/`，mania直读`chartmania/`；支持portable `data/`与自定义数据根。主要工程为`osu.Desktop.slnf`、`osu.Game.Rulesets.Bms`及`oms.Input`。
- 当前协作分支为`master`。皮肤恢复/数据门`SV1-0`已关闭；迁移归档和四个无authority orphan blob继续保全，不能由scanner认领或清理。恢复事实见[恢复审计](../other/SKIN_SYSTEM_RECOVERY_20260710.md)及[数据/实机报告](../other/SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)。
- 2026-09-29 审查开始时工作区干净，HEAD 为文档提交 `0cbbbb4`；本次 `git fetch origin` 成功，领先 `origin/master` 2、落后 0。最近产品修改仍为 `17a341c`，未出现新的产品交付或人工签收。

## 当前执行门与全局风险

| 顺序 | 当前事实与下一道门 | 归属 |
| --- | --- | --- |
| 1 | 静线当前迭代已留存实绘与回归证据，外观打磨按用户决定暂停；按模式选择、固定目录刷新与原组件编辑恢复已完成限定切片，不恢复complex或自行推进视觉下一阶段 | [P1-A](../subline/P1-A/DEVELOPMENT_PLAN.md) |
| 2 | canonical普通简洁包接管已实现；旧OmsSkin只保留历史/人工对照，物理删除仍待实机门 | [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md) |
| 3 | 输入软件基线可用；analog scratch跨设备、校准与真实HID尚未闭合 | [P1-B](../subline/P1-B/DEVELOPMENT_STATUS.md)、[P1-D](../subline/P1-D/DEVELOPMENT_STATUS.md) |
| 4 | 真实LN/CN/HCN、音频/特殊谱、BGA、选歌大库与发行组合仍需验收；P1-L仍逐viewport创建player，单content/decoder未完成 | [子线路由](../subline/README.md) |
| 5 | V-001～V-005及候选发行包人工签收；2026-07-14恢复验收不能代替新增视觉与最终包验证 | [集中清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)、[P1-G](../subline/P1-G/DEVELOPMENT_STATUS.md) |

P1-I仍是三行双端筛选原型，须落实既定单轨上限段并补shared fixture/大库门；P1-J的C3末端lane前置已完成，剩余转谱LN、dense profile和听感验收。发行覆盖须保持原便携模式，非便携真实设备与公开发行组合验收归P1-F/P1-G。各项具体风险只在owning子线维护。

此前 C7 完整包已完成实际安装恢复及从上一修复版跨版本覆盖，原保存根前后字节和属性一致；旧候选误入已有保存根的事故仍只有事后保全、没有该根事前快照，不能追溯宣称无损。发行和数据边界见 [P1-F 状态](../subline/P1-F/DEVELOPMENT_STATUS.md)。

皮肤安全与失败回退详见[P1-A技术约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)：当前并无live gameplay reload或watcher，external永久只读；授权撤销不扩大Reload准入，C6完成不等于C7或人工门关闭。异常期归档只能定点取证。局部自动测试不能代替完整真实选择链，自动证据也不能替代视觉、硬件或特殊Gimmick证明。

## 最近一次验证

2026-09-22：BMS TOTAL 已区分作者声明与各家族缺省，并为新旧成绩选择对应算法；专项验证与完整回归的具名失败归因见 [TOTAL 报告](../other/BMS_TOTAL_RULES_AUDIT_20260922.md)。该结果不更新皮肤、真实设备或发行签收。

2026-09-14的设置/编辑切片已验证固定目录刷新、原编辑控件、独立副本保存与模式偏好隔离；未刷新视觉、设备或 release 人工门。详见 [P1-A 状态](../subline/P1-A/DEVELOPMENT_STATUS.md#最近一次验证)。

最新产品修改为 2026-09-22 的 `17a341c`（TOTAL 与历史成绩兼容）。当日 TOTAL 专项与 Release 通过，BMS full 的旧皮肤测试失败已在修改前版本逐项复现，后续维护归 [P1-A](../subline/P1-A/DEVELOPMENT_PLAN.md)。不把“既有失败已归因”写成完整回归全绿。

皮肤代码仍以 2026-09-14 的 `701893f`（原编辑器与固定目录）及 `36eb79c`（按模式选择）为最近修改。2026-09-13 的 `234ce1f` 是最近一次静线外观修改；该轮实绘、自动结果与较早 core/mania 证据见[轨道验证记录](../other/SKIN_SIMPLE_LANE_PROPORTIONS_20260913.md)。此前发行ZIP和安装证据未随后续产品修改重新验收。

## 文档治理验证

2026-09-29 对照当前代码、留存 TRX、P1-A～M 与 memory 回链审查进度和一致性。修正多语首页、作者/发行/验收入口及子线同步规则的旧口径，精简重复记忆；旧候选包、自动结果与人工未签收继续分开。未改产品代码、未重跑产品测试或 Release，产品与实机日期不变。范围、证据与健康检查见[同步记录](CHANGELOG.md#2026-09-29)。
