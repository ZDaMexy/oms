# P1-A 当前状态：Skin V1、产品面与 release gate

> 最后更新：2026-09-09（C6 非人工产品、最终整包 reload 与 G1 自动门闭合）
> 全局见[主线状态](../../mainline/DEVELOPMENT_STATUS.md)，后续门见[PLAN](DEVELOPMENT_PLAN.md)，实现合同见[TECHNICAL_CONSTRAINTS](TECHNICAL_CONSTRAINTS.md)，历史见[CHANGELOG](CHANGELOG.md)。

## 一句话状态

Skin V1为 **`6/7 closed，C7 active`**：原 C6 已完成可选脚本产品链、VM/作者工具、授权与安全隔离，并关闭最终整包 reload 及 G1 自动门；C7 canonical 双包/完整 Authoring Kit 与接管保留后续。程序化`OmsSkin`仍是迁移链底，`V-001`～`V-004`签收 **0/4**，新增 `V-005` 未签收；`SV1-1`、Skin V1与release未完成。campaign非等权，不换算线性百分比。

## 当前产品能力与剩余门

| 产品面 | 当前可用结果 | 必须保持的边界 |
| --- | --- | --- |
| 恢复与数据 | SV1-0自动、schema 56数据及用户恢复实机门通过 | 迁移归档与无authority orphan blob保全，不做全局cleanup |
| C1作者工作区/archive | external注册/选择/重启/Open/Managed Copy/Unregister；managed Open/Rename/Delete；ordinary .osk有界导入/rollback receipt | external永久只读；held proof、exact-set、single-v3 journal/recovery及共享blob所有权 |
| C2 current revision | Settings唯一Reload current skin覆盖ordinary Realm .osk、managed、external；后台prepare与update-thread原子提交 | gameplay/preview在source prepare前拒绝；current mutation先fallback+detach，legacy editor/update-import关闭 |
| C3 keymode/layout | P1-K提供唯一keymode/lane/timeline；BMS/mania/core/HUD/BGA viewport消费同一immutable layout | stable LaneId/GroupId与显式index，不在consumer重建几何 |
| C4作者合同/material | 28项public catalog、exact skin.ini唯一shared codec、Provide/Inherit/Suppress与实际material consumer | critical不可Suppress；不新增beatmap-local public作者格式，legacy direct visual兼容保留 |
| C5 scene/animation/event | versioned manifest/prepared scene、frame/tween/state/binding/template、只读Snapshot/Reset及预算/池化进入真实host | BMS 28项有route（9K适用26项）；mania 23 Supported，Mine/Turntable/Laser/BGA viewport/BGA frame五项NotApplicable |
| C6脚本与最终整包 | 无需DLL的source/bytecode V1、同源CLI compiler/verifier；Settings授权/拒绝/撤销，双ruleset真实host、预算/熔断/确定性/profiler；三源最终publication/G1自动门通过 | 可选表现，基础note/key/judgement无需授权；external只读、live Reload仍拒绝。作者产物见[Momentum候选](../../other/skin-c6-candidate/README.md)，完整证据见[C6报告](../../other/SKIN_SYSTEM_C6_VALIDATION_20260909.md) |
| C7 canonical/发行 | 下一campaign，本次未实施 | 双包/完整Authoring Kit、canonical parity/完整性/原子恢复与fallback接管仍按[PLAN](DEVELOPMENT_PLAN.md) |
| 集中视觉/实机 | V-001～V-004签收0/4，V-005新增待验收 | 已导入.osk短键、LN head/body/tail、双ruleset脚本候选及最终包见[集中清单](../../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md) |

C2～C6共用exact package+layout+material+scene publication，script及编译工作已加入同一owner/participant/lease/detach/retire。无gameplay host的菜单也先验证ini/manifest/scene/script/素材；授权撤销独立使旧host失权，不扩大Reload准入。完整合同只在[技术约束](TECHNICAL_CONSTRAINTS.md)维护。

## 最近一次验证

**2026-09-09 C6最终实际验证**（Release当前源码，共享构建/测试串行）：

| 测试面 | 结果 |
| --- | --- |
| core GameplaySkin focused | 486/486 |
| BMS / mania relevant | 414/414及复审新增atomic-save 2/2；mania 69/69 |
| 三源备份根 G1 | source/实际CLI bytecode × 三源 6/6；用户原根与baseline保持一致 |
| BMS full | 1776/1776，无跳过或hang artifact |
| mania full | 860/864，四项既有HoldNote frame-count失败 |
| core ~Skin / FileStoreTests | 1275/1281，六项既有archive/default-skin失败；存储11/11 |
| Windows Release | 0 error / 18条NU1902输出，九条既有MessagePack告警在restore/build重复；BMS重新编译仍仅有既有CS8600/CA2007 |

十项既有失败的名称、错误分类及精确消息与[项目审查](../../other/PROJECT_PROGRESS_AUDIT_20260909.md)实际TRX逐项全等。命令、红转绿、source/consumer矩阵、性能环境、独立终审和检查结论集中于[C6报告](../../other/SKIN_SYSTEM_C6_VALIDATION_20260909.md)。未跑全core、publish、低端实机或GPU/字体视觉门；C5既有完成证据仍见[2026-09-03交接](../../other/SKIN_SYSTEM_C5_SCENE_EVENT_COMPLETION_HANDOFF_20260903.md)，不重签C1～C5。

## 当前风险与未完成项

- `BmsBeatmapDecoderOptions.KeymodeOverride`只是host/importer seam；普通loader传null，无用户纠正UI。证据不足的sparse .bms/.bml仍安全拒绝；可用性缺口归P1-K，不重开C3。
- scanner只在启动后对账一次；新增managed direct child须重启发现，已登记current内容只能手动Reload。没有watcher或live gameplay replacement。
- C1 held-root/journal不是filesystem transaction；foreign addition/replacement可导致冻结。current mutation的具体失败阶段与external零写入合同继续生效。
- 程序化OmsSkin在canonical parity、完整性、原子恢复与实机gate前不得删除；C6完成不能提前核算C7或人工签收，NotApplicable也不能写成普遍unsupported。
- BGA内容/timeline/seek/gimmick仍归P1-L；在线服务、sample pool、判定、binding不属C5。既有core六项/mania四项失败须按[精确失败合同](TECHNICAL_CONSTRAINTS.md#测试与发布约束)比较，不能按数量掩盖回归。
