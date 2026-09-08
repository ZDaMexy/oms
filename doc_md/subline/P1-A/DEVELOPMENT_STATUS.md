# P1-A 当前状态：Skin V1、产品面与 release gate

> 最后更新：2026-09-08（文档治理；产品验证仍为2026-09-03）
> 全局状态见[主线状态](../../mainline/DEVELOPMENT_STATUS.md)，后续动作见[当前计划](DEVELOPMENT_PLAN.md)，实现与安全合同见[技术约束](TECHNICAL_CONSTRAINTS.md)，历史见[CHANGELOG](CHANGELOG.md)。

## 一句话状态

Skin V1 当前为 **`5/7 closed，C6 active`**：C1作者工作区/archive、C2三源current revision、C3 P1-K前置与唯一layout、C4 public catalog/shared codec/三态material、C5 scene/animation/read-only event与全部适用public slot production均已闭合。该计数不是线性百分比；C6 sandbox/最终整包reload、C7 canonical双包/Authoring Kit尚未交付，程序化`OmsSkin`仍是迁移链底。`SV1-1`、`SV1-2`整体、Skin V1与release未完成。

## 当前产品能力与剩余门

| 产品面 | 当前可用结果 | 后续边界 |
| --- | --- | --- |
| 恢复与数据 | `SV1-0`自动、schema 56数据与用户恢复实机gate均通过 | 保全迁移归档与无authority orphan blob，不做全局cleanup |
| C1作者工作区/archive | external只读注册/选择/重启/Open/Managed Copy/Unregister；managed Open/Rename/Delete；ordinary `.osk`有界导入与rollback receipt | 保持held capture、exact-set、single-v3 journal/recovery及共享blob所有权合同 |
| C2 current revision | Settings唯一`Reload current skin`覆盖ordinary Realm `.osk`、managed、external；新revision在后台准备，同一update-thread barrier提交 | live gameplay/preview在source prepare前拒绝；current mutation先fallback+detach，无watcher或legacy update/editor旁路 |
| C3 keymode/layout | P1-K parser/converter提供唯一keymode/lane/timeline；BMS solver、mania adapter与core/HUD/BGA viewport消费同一immutable layout | 保持stable LaneId/GroupId、显式index与全部keymode/style/stage矩阵 |
| C4作者合同/material | 28项public catalog、exact `skin.ini`唯一shared codec、显式`Provide/Inherit/Suppress`与BMS/mania实际material consumer | critical不可Suppress；public beatmap-local作者格式排除，既有只读legacy direct visual compatibility保留 |
| C5 scene/animation/event | exact package的versioned manifest/scene、frame/tween/state/binding/template、只读event Snapshot/Reset及预算/池化已进入真实BMS/mania host | BMS 28项有route（9K适用26项）；mania 23 Supported，Mine/Turntable/Laser/BGA viewport/BGA frame五项为版本化NotApplicable |
| C6 sandbox与最终整包reload | **active，未实现** | VM/toolchain、授权持久化/撤销、quota/determinism/fuse/profiler；ini/manifest/scene/script/素材同一revision闭门 |
| C7 canonical发行 | **未实现** | 双包、Authoring Kit/validator、只读完整性/原子恢复、程序化产品视觉退出与自动release |
| 集中视觉与实机 | `V-001`～`V-004`签收 **0/4** | 已导入`.osk`的普通短键及LN head/body/tail统一签收，最终包/真实设备/谱面与发行复核 |

C2～C5共同发布一个exact package+layout+material+scene immutable publication。prepared resource borrow、participant/work/operation lease、动态attach/detach、late attach、失败保exact A与最后detach后exactly-once retire已闭合；C6新增consumer必须加入同一协议。诊断只在成功commit后输出去重、排序的脱敏摘要，日志故障不改变commit。完整合同与production/compatibility seam边界只在[技术约束](TECHNICAL_CONSTRAINTS.md)维护。

每个campaign由真实caller/consumer、失败回退、宽测试、文档、独立终审与有意义提交闭合；审计、DTO、fixture、单一consumer或提交数不推进编号。C5交付和完整矩阵见[C5完成交接](../../other/SKIN_SYSTEM_C5_SCENE_EVENT_COMPLETION_HANDOFF_20260903.md)。

## 最近一次验证

产品证据日期：**2026-09-03，C5闭门**；本次文档治理未重跑runtime或改写该结论。

- core `FullyQualifiedName~GameplaySkin` **429/429**；mania `~GameplaySkin` **69/69**；BMS `~GameplaySkin` **146/146**。新增scene codec/runtime/event、publication、全部slot host与BMS/mania production矩阵均从真实SkinManager current revision进入实际consumer，不使用测试publisher注入最终snapshot。
- BMS full（`--blame-hang --blame-hang-timeout 5m`）**1721/1721**，无hang artifact；BMS current-revision production **215/215**。BMS C5 all-keymode/双deck、custom-fallback/opaque-shell/partial-stage、hit-explosion与timing-epoch矩阵均通过；9K中Turntable/Laser按catalog applicability不计入适用格。
- mania full **860/864**，四项`TestHoldNoteWithReleasePress`、`TestHoldNoteChord`、`TestSingleHoldNote`、`TestHoldNoteStair`与冻结的既有frame-count基线逐字符一致；mania C5 real production matrix覆盖single/dual、lane-cover、hit-explosion及28项逐项capability decision（23 Supported、5 NotApplicable）。
- core `~Skin` **1218/1224**，六项失败精确保持冻结基线：`TestRetrieveAndLegacyExportJapaneseFilename`、`TestRetrieveAndNonLegacyExportJapaneseFilename`、`TestBackgroundCyclingOnDefaultSkin(True)`、`TestRetrievalWithConflictingFilenames`、`TestSampleUpdatedBeforePlaybackWhenNotPresent`、`TestRetrieveOggAudio`；错误分类/消息与既有基线一致，无新增失败。core `~Skins` **746/750**同样只含既有四项archive失败。
- P1-K decoder/converter/timing/keysound **126/126**、converted mania **24/24**、真实BMS keysound lifecycle **17/17**、shared store/DrawableRuleset **68/68**。Windows Release **0 error / 20 emitted known warnings**：既有MessagePack `NU1902` 18次及BMS tests既有`CS8600`、`CA2007`各1次；未用NoWarn隐藏。
- `CheckDocumentation.ps1`、`git diff --check`及owning-project targeted formatter `--verify-no-changes`在2026-09-03代码/文档闭门后复验；四类独立终审均GO，blocker/major **0/0**。完整交接见[C5 scene/event完成交接](../../other/SKIN_SYSTEM_C5_SCENE_EVENT_COMPLETION_HANDOFF_20260903.md)。

## 当前风险与未完成项

- `BmsBeatmapDecoderOptions.KeymodeOverride`只是host/importer correction seam；普通`ICustomBeatmapLoader`仍传`null`，没有终端用户纠正UI。证据不足的sparse`.bms/.bml`继续fail-closed，这是P1-K产品可用性缺口，不重开C3安全门，也不得描述成已交付用户功能。
- C5已闭合全部advertised slot的production host/capability决策；后续不得把C6 sandbox、最终整包reload、C7双包或人工视觉签收提前算入C5，也不得把mania的五个版本化NotApplicable写成普遍unsupported。
- scanner仍只在启动后对账一次，不是watcher；新增managed direct child需重启发现，已登记current managed/external只由Settings manual Reload进入新revision。
- C1 held-root/journal/recovery不是filesystem transaction；foreign addition/replacement可导致fail-closed冻结。current mutation仍须先fallback+detach，external永久source零写入。
- core Skin六项与mania full四项既有fixture失败必须按同名精确基线比较，不能隐藏新回归。
- BGA内容/timeline/seek/gimmick仍归P1-L；在线服务、sample pool、判定、binding与无关ruleset不属于P1-A C5。
- 程序化`OmsSkin`在C7 canonical包通过parity、完整性、原子恢复与实机gate前不得删除。

## 文档治理验证

2026-09-08仅整理现行合同、状态与计划：C5稳定约束合并到所属章节，STATUS保留唯一产品验证，PLAN只保留未完成动作和冻结输入。按当前decoder、event/schema与runtime profile定点核对语义；未改runtime，未运行BMS/mania/core测试或Release。文档检查结果由本次主线治理记录统一汇总；不推进campaign或视觉/release gate。
