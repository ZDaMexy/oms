# OMS 当前开发状态

> 最后更新：2026-09-09（文档健康度治理；产品验证日期与人工签收不变）
> 本页只保留全局状态与风险。执行顺序见[当前计划](DEVELOPMENT_PLAN.md)，专项事实从[子线路由](../subline/README.md)进入，历史见[CHANGELOG](CHANGELOG.md)。

## 一句话状态

OMS处于Phase 1.x后段。Skin V1为 **`5/7 closed，C6 active`**；C6 sandbox/最终整包reload与C7 canonical双包/Authoring Kit未交付，`V-001`～`V-004`签收 **0/4**，Skin V1与release均未完成。能力与剩余门见[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)。

## 产品与仓库基线

- Windows-only，保留osu!mania与第一类BMS，Osu/Taiko/Catch已删除；离线优先，Phase 3前OMS私有服务与默认endpoint为空。用户主动添加公共BMS难度表URL仅是既有窄例外。
- BMS直读`chartbms/`，mania直读`chartmania/`；支持portable `data/`与自定义数据根。主要工程为`osu.Desktop.slnf`、`osu.Game.Rulesets.Bms`及`oms.Input`。
- 当前协作分支为`master`。皮肤恢复/数据门`SV1-0`已关闭；迁移归档和四个无authority orphan blob继续保全，不能由scanner认领或清理。恢复事实见[恢复审计](../other/SKIN_SYSTEM_RECOVERY_20260710.md)及[数据/实机报告](../other/SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)。

## 当前执行门与全局风险

| 顺序 | 当前事实与下一道门 | 归属 |
| --- | --- | --- |
| 1 | C1～C5作为冻结输入，继续C6脚本隔离与最终整包reload；不重开campaign | [P1-A](../subline/P1-A/DEVELOPMENT_PLAN.md) |
| 2 | C7双包、Authoring Kit与自动release。程序化OmsSkin在canonical parity、完整性、原子恢复与实机门前继续保留 | [P1-A](../subline/P1-A/DEVELOPMENT_PLAN.md) |
| 3 | 输入软件基线可用；analog scratch跨设备、校准与真实HID尚未闭合 | [P1-B](../subline/P1-B/DEVELOPMENT_STATUS.md)、[P1-D](../subline/P1-D/DEVELOPMENT_STATUS.md) |
| 4 | 真实LN/CN/HCN、音频/特殊谱、BGA、选歌大库与发行组合仍需验收；P1-L仍逐viewport创建player，单content/decoder未完成 | [子线路由](../subline/README.md) |
| 5 | V-001～V-004及候选发行包人工签收；2026-07-14恢复验收不能代替新增视觉与最终包验证 | [集中清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)、[P1-G](../subline/P1-G/DEVELOPMENT_STATUS.md) |

P1-I仍是三行双端筛选原型，须落实既定单轨上限段并补shared fixture/大库门；P1-J的C3末端lane前置已完成，剩余转谱LN、dense profile和听感验收。发行覆盖须保持原便携模式，包内说明欠账归P1-F。各项具体风险只在owning子线维护。

皮肤安全与失败回退详见[P1-A技术约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)：当前并无live gameplay reload或watcher，external永久只读；C5完成不等于C6/C7门关闭。异常期归档只能定点取证。自动测试也不能代替跨ruleset真实选择链、视觉、硬件或特殊Gimmick证明。

## 最近一次验证

**2026-09-09**：Release构建与BMS全量通过；mania full/core Skin的失败名称和故障与既有基线对应。额外选歌/谱库子集仍有shared TestSearch的fixture依赖缺失。精确结果、命令及远端TLS限制见[本轮审查证据](../other/PROJECT_PROGRESS_AUDIT_20260909.md#本轮实际验证)。未跑全core、publish或实机；[2026-09-03 C5完整闭门](../other/SKIN_SYSTEM_C5_SCENE_EVENT_COMPLETION_HANDOFF_20260903.md)未重签。

## 文档治理验证

2026-09-09实际进度审查已覆盖P1-A～M、主约束、派生说明与memory，见[审查报告](../other/PROJECT_PROGRESS_AUDIT_20260909.md)。本次仅集中合同与压缩重复入口，未改产品代码、测试日期或人工结论；文档检查结果由[主线CHANGELOG](CHANGELOG.md)记录。
