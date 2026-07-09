---
name: reference_bms_judgement_parity
description: "BMS judge-mode parity (P1-C) real state — not \"all missing\"; contract test gates window changes; which numbers are audit-backed vs placeholder"
metadata: 
  node_type: memory
  type: reference
  originSessionId: b849838f-c921-4312-bd80-805af5a40256
---

BMS 判定 parity（P1-C）的真实状态，纠正 STATUS/CONSTRAINTS 早先「全缺」的失真叙述（2026-06-14 第 1 刀确认）。

**架构**：`Scoring/BmsJudgementSystem`（抽象基类）已预留全部钩子——`GetEarlyWindow/GetLateWindow`（支持非对称）、`ScratchWindows`、`LongNoteReleaseWindows`、`GetExcessivePoorEarly/LateWindow`、`CanTriggerExcessivePoor`。四套系统：`OsuOdJudgementSystem` / `IidxJudgementSystem` / `Lr2JudgementSystem` / `BeatorajaJudgementSystem`，经 `BmsJudgeMode.CreateJudgementSystem()` 选择，挂在 `BmsTimingWindows`（带 `IsScratch` 属性）。消费端：`DrawableBmsHitObject.ResultForPlayerInput`（note 命中，恒 `isLongNoteRelease=false`）+ `BmsLane.shouldTriggerEmptyPoor`（空 poor：若候选 `SupportsExcessivePoor` 则用 `CanTriggerExcessivePoor(offset)`，否则看有无未判的未来候选）。FAST/SLOW 由 offset 符号在反馈层定，不在窗口里。

**已 sourced（勿乱改）**：IIDX `16.67/33.33/116.67/250`（rank-invariant，`SetDifficulty` 是 no-op）；LR2 四档 `8/24/40`·`15/30/60`·`18/40/100`·`21/60/120`（尾 200，VeryEasy 共用 Easy），excessive poor `1000/0`＝仅 note 前；beatoraja `25/50/75/100/125` 整数截断缩放（=EASY 100% 的 25/50/75/100/125%）+ early/late 非对称 BAD，来源 beatoraja `JudgeProperty.SEVENKEYS`（exch-bms2/beatoraja）。

**G3 真实 bug 已修（第 2 刀 2026-06-14）**：beatoraja BAD 非对称**方向曾写反**。权威 SEVENKEYS：note BAD `{…,-280000,220000}`µs ＝**负=早/正=晚 → 早 280 比晚 220 宽**（scratch 290/230、LN release 280/220）。OMS 原为早 220/晚 280（晚宽），已改回。坑：`createProfile(perfect,great,good,badEarly,badLate)` 的 Windows[4]=badLate（晚界）→ `PoorWindow` 供 `CanStillBeHitByPlayer` 做**晚**自动 miss；`WindowFor(Meh)`=max(早,晚)=早界（更宽），故显示上会出现 Bad(早210) > Poor(晚165) 的"怪象"，是对的。属性显示 `BAD hit window -早/+晚` 本就分读 GetEarly/GetLateWindow，修后自动正确。

**G4 收口为 documented heuristic（非 placeholder）**：IIDX 闭源、无权威 empty/excessive POOR 单值（审计仅述「note 前或后均可」），故 IIDX `500/150` + IIDX CN release 沿用 note 窗口**保留为标注清楚的 OMS 启发式，不宣称 parity**。

**硬约束（P1-C CONSTRAINTS #14–#17）**：任何窗口数值/非对称/scratch·release 扩窗/excessive-poor 改动**必须先改** `osu.Game.Rulesets.Bms.Tests/BmsJudgementSystemParityTest.cs`（29 case，先改测试让 parity diff 显式）。跨家族边界统一为 `<= window + BoundaryEpsilon`（含 beatoraja 自有非对称分支）。约束 #1：不允许只改窗口不改训练反馈表达。

**分刀**：①契约测试+边界统一（已收口 2026-06-14）→ ②溯源修复 G3(beatoraja 方向写反)+G4 收口为 heuristic（已收口 2026-06-14，BMS 916/916、parity 29/29）→ **③语境已变（2026-06-15）：原计划把 BAD-early/late、empty-poor vs note-poor 接进 gameplay judge display——但承载它的常驻速度反馈卡已被产品决定整体删除（见下），gameplay 反馈面不复存在；属性显示面已自动满足，counts 改由全局 `JudgementCounterDisplay` 承担** → ④全量回归+文档。改窗口前必先改 `BmsJudgementSystemParityTest`（约束 #14）。相关：[[reference_mania_autoplay_holdnote]]、[[reference_build_and_test]]。

**2026-06-21 LN/CN/HCN「松开重按接回」语义更正（P1-E，用户确认 CN 旧行为是 bug）**：正确语义＝`LN`=头判+长条；`CN`=头判+长条+尾判，中途松开即**永久 miss、不可接回**；`HCN`=头判+长条（持续 gauge）+尾判，中途松开**可重按恢复**。旧代码把「可接回 / late body press」错误门控在 `RequiresTailJudgement()`（CN+HCN 都真）→ CN 也能接回（且 `TestCnEarlyReleaseCanRepressAndResolveTail` 固化了错误）。修复＝新增 `BmsLongNoteModeExtensions.AllowsRegrabAfterRelease()`（`==HCN`），`DrawableBmsHoldNote.CanApplyLateBodyPress` + `OnReleased` 的「保持 tail 打开等待接回」分支改门控于它；LN/CN 走「立即 `resolveTail`（早释放=Miss）」。**勿退回 `RequiresTailJudgement()` 门控**（那是被修的 bug）。`RequiresTailJudgement()` 仅=「尾判计分」(CN+HCN)、`RequiresBodyGaugeTicks()` 仅=「body 进 gauge」(HCN)，三者勿混。连带：长条 body 三态视觉（`DrawableBmsHoldNote.BodyState`，归 P1-A，见 [[reference_bms_default_skin_geometry]]）纯派生自 `isHolding`，故「仅 HCN 恢复」自动成立。测试：`TestCnEarlyReleaseResolvesTailAsMissWithoutRegrab` / `TestCnLatePressDoesNotStartHoldAfterHeadMiss` / `TestHcnAllowsLatePressThroughTailMissBoundaryButCnAndLnNever` + `TestBodyStateFollowsHcnHoldLifecycleWithRecovery`。BMS 936/936。

**2026-06-15 gameplay 反馈面整体移除 + COMBO BREAK 计数修复**：按产品决定删除常驻速度反馈卡 `DefaultBmsSpeedFeedbackDisplay` 及其专属子系统（`BmsGameplayFeedbackState` / `BmsJudgementCounts` / `BmsJudgementTimingFeedback` / `BmsExScore*Info` / `BmsTimingOffsetSparkline` + `DrawableBmsRuleset` 的 `GameplayFeedbackState` / `LatestJudgementFeedback` / `RecentJudgementFeedbacks` / `ExScorePacemakerInfo` 暴露面与 pacemaker/timing 管线）——游戏内不再有 FAST/SLOW 逐 note 计时、EX pacemaker、judge display、judgement summary、常驻 GN（GN 仅留 toast/pre-start）。判定**计数**改由上游全局 `JudgementCounterDisplay`（右侧 7 计数器）承担。**LANDMINE（已修）**：BMS `GetHitResultsForDisplay()` 列 `HitResult.ComboBreak` 为第 7 计数器，但 ComboBreak 是 `BmsScoreProcessor.ApplyScoreChange` 旁路自增的**派生统计**、从不作为真实 `JudgementResult.Type` 经 `NewJudgement` 流过，而上游 `JudgementCountController` 旧逻辑按 `judgement.Type` 自增 → COMBO BREAK 计数游玩中**恒 0**（仅 replay-frame reset 经 statistics 同步）。修复＝`JudgementCountController` 改为每次判定从 `ScoreProcessor.Statistics` **全量同步**各计数器（统计在 `NewJudgement` 前已更新，对 mania 等真实 type 行为不变）。同期修了编辑器 Activator 坑，见 [[reference_bms_skin_editor]]；默认几何调整见 [[reference_bms_default_skin_geometry]]。
