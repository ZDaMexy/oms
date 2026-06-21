# P1-E 技术约束：gameplay 与长条真实谱面验校

1. `P1-E` 只负责真实谱面验校与 gameplay 边界收口，不得借题提前带入 BSS / MSS 或其他 Phase 2 键模式扩张。
2. 任何长条语义结论都必须同时同步到计划、状态与验证记录，不能只留在测试命令里。
3. 若验校结论影响判定、计分、HUD 或反馈表达，必须回写对应子线与 `../../mainline/` 文档。
4. **长条「松开后重按接回」只属于 HCN**：`LN` 中途松开即 tail 终结、`CN` 中途松开即 tail Miss 终结，二者都**不可接回**；仅 `HCN`（body 被持续判定）允许早释放后保持 tail 打开、在 tail miss 窗口内重按恢复。该门控的唯一真源是 `BmsLongNoteModeExtensions.AllowsRegrabAfterRelease()`（`== HCN`），`DrawableBmsHoldNote.CanApplyLateBodyPress` 与 `OnReleased` 的「保持打开等待接回」分支都必须门控于它——**不得退回用 `RequiresTailJudgement()`（CN+HCN 都真）门控**，那会让 CN 错误地可接回（2026-06-21 已更正的历史 bug）。`RequiresTailJudgement()` 仅表达「尾判是否计分」（CN+HCN），`RequiresBodyGaugeTicks()` 仅表达「body 是否进 gauge」（HCN），三者语义不得混用。长条 body 三态视觉（`DrawableBmsHoldNote.BodyState`，归 `P1-A`）纯派生自 `isHolding`，故任何对接回门控的改动都会连带改变 body「Broken→恢复」表现，须一并核对。