# OMS 当前开发状态

> 最后更新：2026-09-09（Skin C6非人工闭门，C7保留后续；人工签收不变）
> 本页只保留全局状态与风险。执行顺序见[当前计划](DEVELOPMENT_PLAN.md)，专项事实从[子线路由](../subline/README.md)进入，历史见[CHANGELOG](CHANGELOG.md)。

## 一句话状态

OMS处于Phase 1.x后段。Skin V1为 **`6/7 closed，C7 active`**；C6可选脚本、最终整包reload与G1自动门已闭合，C7 canonical双包/完整Authoring Kit及接管保留后续。`V-001`～`V-004`签收 **0/4**，新增`V-005`未签收，Skin V1与release均未完成。能力、验证与剩余门见[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)。

## 产品与仓库基线

- Windows-only，保留osu!mania与第一类BMS，Osu/Taiko/Catch已删除；离线优先，Phase 3前OMS私有服务与默认endpoint为空。用户主动添加公共BMS难度表URL仅是既有窄例外。
- BMS直读`chartbms/`，mania直读`chartmania/`；支持portable `data/`与自定义数据根。主要工程为`osu.Desktop.slnf`、`osu.Game.Rulesets.Bms`及`oms.Input`。
- 当前协作分支为`master`。皮肤恢复/数据门`SV1-0`已关闭；迁移归档和四个无authority orphan blob继续保全，不能由scanner认领或清理。恢复事实见[恢复审计](../other/SKIN_SYSTEM_RECOVERY_20260710.md)及[数据/实机报告](../other/SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)。

## 当前执行门与全局风险

| 顺序 | 当前事实与下一道门 | 归属 |
| --- | --- | --- |
| 1 | C1～C6已闭合，不重开campaign；下一门为C7双包、完整Authoring Kit与自动release | [P1-A](../subline/P1-A/DEVELOPMENT_PLAN.md) |
| 2 | 程序化OmsSkin在canonical parity、完整性、原子恢复与实机门前继续保留，本次未提前接管 | [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md) |
| 3 | 输入软件基线可用；analog scratch跨设备、校准与真实HID尚未闭合 | [P1-B](../subline/P1-B/DEVELOPMENT_STATUS.md)、[P1-D](../subline/P1-D/DEVELOPMENT_STATUS.md) |
| 4 | 真实LN/CN/HCN、音频/特殊谱、BGA、选歌大库与发行组合仍需验收；P1-L仍逐viewport创建player，单content/decoder未完成 | [子线路由](../subline/README.md) |
| 5 | V-001～V-005及候选发行包人工签收；2026-07-14恢复验收不能代替新增视觉与最终包验证 | [集中清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)、[P1-G](../subline/P1-G/DEVELOPMENT_STATUS.md) |

P1-I仍是三行双端筛选原型，须落实既定单轨上限段并补shared fixture/大库门；P1-J的C3末端lane前置已完成，剩余转谱LN、dense profile和听感验收。发行覆盖须保持原便携模式，包内说明欠账归P1-F。各项具体风险只在owning子线维护。

皮肤安全与失败回退详见[P1-A技术约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)：当前并无live gameplay reload或watcher，external永久只读；授权撤销不扩大Reload准入，C6完成不等于C7或人工门关闭。异常期归档只能定点取证。局部自动测试不能代替完整真实选择链，自动证据也不能替代视觉、硬件或特殊Gimmick证明。

## 最近一次验证

**2026-09-09 C6**：规定focused/relevant、BMS full、三源备份根G1与Release通过；mania full/core Skin十项既有失败逐名、分类和精确消息全等。独立复审与完整结果见[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)及[C6证据](../other/SKIN_SYSTEM_C6_VALIDATION_20260909.md)。未跑全core、publish或实机。P1-I既有shared TestSearch fixture依赖缺失仍见[此前项目审查](../other/PROJECT_PROGRESS_AUDIT_20260909.md#本轮实际验证)，没有借C6改变其它子线验收结论。
