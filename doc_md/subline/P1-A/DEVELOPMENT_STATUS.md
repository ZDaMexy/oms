# P1-A 当前状态：Skin V1、产品面与 release gate

> 最后更新：2026-09-09（文档健康度治理；既有产品验证与签收不变）
> 全局见[主线状态](../../mainline/DEVELOPMENT_STATUS.md)，后续门见[PLAN](DEVELOPMENT_PLAN.md)，实现合同见[TECHNICAL_CONSTRAINTS](TECHNICAL_CONSTRAINTS.md)，历史见[CHANGELOG](CHANGELOG.md)。

## 一句话状态

Skin V1为 **`5/7 closed，C6 active`**：C1～C5已闭合，C6 sandbox/最终整包reload与C7 canonical双包/Authoring Kit未交付。程序化`OmsSkin`仍是迁移链底，`V-001`～`V-004`签收 **0/4**；`SV1-1`、`SV1-2`整体、Skin V1与release未完成。campaign非等权，不换算线性百分比。

## 当前产品能力与剩余门

| 产品面 | 当前可用结果 | 必须保持的边界 |
| --- | --- | --- |
| 恢复与数据 | SV1-0自动、schema 56数据及用户恢复实机门通过 | 迁移归档与无authority orphan blob保全，不做全局cleanup |
| C1作者工作区/archive | external注册/选择/重启/Open/Managed Copy/Unregister；managed Open/Rename/Delete；ordinary .osk有界导入/rollback receipt | external永久只读；held proof、exact-set、single-v3 journal/recovery及共享blob所有权 |
| C2 current revision | Settings唯一Reload current skin覆盖ordinary Realm .osk、managed、external；后台prepare与update-thread原子提交 | gameplay/preview在source prepare前拒绝；current mutation先fallback+detach，legacy editor/update-import关闭 |
| C3 keymode/layout | P1-K提供唯一keymode/lane/timeline；BMS/mania/core/HUD/BGA viewport消费同一immutable layout | stable LaneId/GroupId与显式index，不在consumer重建几何 |
| C4作者合同/material | 28项public catalog、exact skin.ini唯一shared codec、Provide/Inherit/Suppress与实际material consumer | critical不可Suppress；不新增beatmap-local public作者格式，legacy direct visual兼容保留 |
| C5 scene/animation/event | versioned manifest/prepared scene、frame/tween/state/binding/template、只读Snapshot/Reset及预算/池化进入真实host | BMS 28项有route（9K适用26项）；mania 23 Supported，Mine/Turntable/Laser/BGA viewport/BGA frame五项NotApplicable |
| C6、C7 | 均未实现，当前执行C6 | 完整非人工结果、权限/VM、整包reload、canonical/Authoring Kit与退出条件只见[PLAN](DEVELOPMENT_PLAN.md) |
| 集中视觉/实机 | V-001～V-004签收0/4 | 已导入.osk短键与LN head/body/tail及最终包签收见[集中清单](../../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md) |

C2～C5共用exact package+layout+material+scene publication；C6新增consumer继续加入同一生命周期。完整consumer inventory、borrow/lease、失败保A、detach/retire和脱敏诊断只在[技术约束](TECHNICAL_CONSTRAINTS.md)维护；campaign如何闭合只在[PLAN](DEVELOPMENT_PLAN.md)维护。

## 最近一次验证

**2026-09-09已记录的项目复验**：

| 测试面 | 结果 |
| --- | --- |
| Windows Release | 0 error / 11 warnings：既有NU1902九次、CS8600/CA2007各一次 |
| BMS full | 1721/1721，无跳过或hang artifact |
| mania full | 860/864，四项既有HoldNote frame-count失败 |
| core ~Skin | 1218/1224，四项既有archive fixture与两项默认皮肤假设失败 |

命令、TRX范围、逐项名称/消息和远端时效见[9月9日项目审查](../../other/PROJECT_PROGRESS_AUDIT_20260909.md#本轮实际验证)。本轮文档治理没有重跑产品测试、全core、publish或实机。完整C5闭门仍为[2026-09-03交接](../../other/SKIN_SYSTEM_C5_SCENE_EVENT_COMPLETION_HANDOFF_20260903.md)，C5该次focused/并发/slot矩阵、精确失败与独立终审记录不被9月9日项目复验替代，不重签campaign。

## 当前风险与未完成项

- `BmsBeatmapDecoderOptions.KeymodeOverride`只是host/importer seam；普通loader传null，无用户纠正UI。证据不足的sparse .bms/.bml仍安全拒绝；可用性缺口归P1-K，不重开C3。
- scanner只在启动后对账一次；新增managed direct child须重启发现，已登记current内容只能手动Reload。没有watcher或live gameplay replacement。
- C1 held-root/journal不是filesystem transaction；foreign addition/replacement可导致冻结。current mutation的具体失败阶段与external零写入合同继续生效。
- 程序化OmsSkin在canonical parity、完整性、原子恢复与实机gate前不得删除；C5 slot完成不能提前核算C6/C7或人工签收，NotApplicable也不能写成普遍unsupported。
- BGA内容/timeline/seek/gimmick仍归P1-L；在线服务、sample pool、判定、binding不属C5。既有core六项/mania四项失败须按[精确失败合同](TECHNICAL_CONSTRAINTS.md#测试与发布约束)比较，不能按数量掩盖回归。

## 文档治理验证

2026-09-09生产链与测试源码审查见[项目审查记录](../../other/PROJECT_PROGRESS_AUDIT_20260909.md)。本次仅压缩入口、归位C6/C7补充条件及合并重复合同，未改runtime、campaign或视觉/release门；文档检查由本次主线CHANGELOG统一记录。
