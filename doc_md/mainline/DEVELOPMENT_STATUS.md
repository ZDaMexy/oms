# OMS 当前开发状态

> 最后更新：2026-09-11（原 C7 内修复交付后的 BMS 预览入口问题；人工签收与公开发行仍待验）
> 本页只保留全局状态与风险。执行顺序见[当前计划](DEVELOPMENT_PLAN.md)，专项事实从[子线路由](../subline/README.md)进入，历史见[CHANGELOG](CHANGELOG.md)。

## 一句话状态

OMS处于Phase 1.x后段。Skin 原七阶段的非人工结果已完成（**`7/7 closed`**）：双包、完整制作体验、正式保底、安装恢复与集中体验包已交付；`V-001`～`V-004`签收仍 **0/4**，`V-005`未签收，Skin V1与release均未完成。能力、实际证据与剩余人工门见 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)。

## 产品与仓库基线

- Windows-only，保留osu!mania与第一类BMS，Osu/Taiko/Catch已删除；离线优先，Phase 3前OMS私有服务与默认endpoint为空。用户主动添加公共BMS难度表URL仅是既有窄例外。
- BMS直读`chartbms/`，mania直读`chartmania/`；支持portable `data/`与自定义数据根。主要工程为`osu.Desktop.slnf`、`osu.Game.Rulesets.Bms`及`oms.Input`。
- 当前协作分支为`master`。皮肤恢复/数据门`SV1-0`已关闭；迁移归档和四个无authority orphan blob继续保全，不能由scanner认领或清理。恢复事实见[恢复审计](../other/SKIN_SYSTEM_RECOVERY_20260710.md)及[数据/实机报告](../other/SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)。

## 当前执行门与全局风险

| 顺序 | 当前事实与下一道门 | 归属 |
| --- | --- | --- |
| 1 | 原C7交付后的BMS预览问题已修复；完成针对实际入口的复验，以修复包继续双包、原V项目及真实设备/长时体验签收 | [P1-A](../subline/P1-A/DEVELOPMENT_PLAN.md) |
| 2 | canonical普通简洁包接管已实现；旧OmsSkin只保留历史/人工对照，物理删除仍待实机门 | [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md) |
| 3 | 输入软件基线可用；analog scratch跨设备、校准与真实HID尚未闭合 | [P1-B](../subline/P1-B/DEVELOPMENT_STATUS.md)、[P1-D](../subline/P1-D/DEVELOPMENT_STATUS.md) |
| 4 | 真实LN/CN/HCN、音频/特殊谱、BGA、选歌大库与发行组合仍需验收；P1-L仍逐viewport创建player，单content/decoder未完成 | [子线路由](../subline/README.md) |
| 5 | V-001～V-005及候选发行包人工签收；2026-07-14恢复验收不能代替新增视觉与最终包验证 | [集中清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)、[P1-G](../subline/P1-G/DEVELOPMENT_STATUS.md) |

P1-I仍是三行双端筛选原型，须落实既定单轨上限段并补shared fixture/大库门；P1-J的C3末端lane前置已完成，剩余转谱LN、dense profile和听感验收。发行覆盖须保持原便携模式，非便携真实设备与公开发行组合验收归P1-F/P1-G。各项具体风险只在owning子线维护。

C7 最终完整发行物已通过独立便携/自定义位置、工作副本恢复及覆盖后启动；此前旧候选误入已有保存根的事故已事后保全，但缺少该根事前快照，不能追溯宣称无损，详见 [P1-F 状态](../subline/P1-F/DEVELOPMENT_STATUS.md)。

皮肤安全与失败回退详见[P1-A技术约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)：当前并无live gameplay reload或watcher，external永久只读；授权撤销不扩大Reload准入，C6完成不等于C7或人工门关闭。异常期归档只能定点取证。局部自动测试不能代替完整真实选择链，自动证据也不能替代视觉、硬件或特殊Gimmick证明。

## 最近一次验证

**2026-09-11 BMS预览反馈**：按用户日志修复实际进入画面时的设置读取，并补齐真实普通进入、自动演示、重试与退出；此前测试宿主提前注入设置的遗漏及修复后发行证据见 [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)与 [C7入口修复](../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md#交付后-bms-预览入口修复)。仍属原C7，不重新计数或代签人工项目。

**2026-09-11 C7（反馈前）**：最终双包/第三方完整使用、三来源真实备份保护、作者独立制作、规定自动复验、Release publish、真实安装四轮及集中包组装完成；既有失败逐项精确核对与独立复审见 [C7证据](../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md)。没有运行全core或代签人工观感/设备，也没有改变其它子线的既有失败与验收结论。
