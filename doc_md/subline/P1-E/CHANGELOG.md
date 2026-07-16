# P1-E 变动日志

## 2026-07-16

### 文档健康治理：长条当前态与稳定门控分层

- STATUS 只保留 LN/CN/HCN 当前语义和真实谱 gate；CONSTRAINTS 将原单条长叙事拆成模式语义、三类 predicate authority 与视觉联动三层，2026-06-21 的 bug 过程继续留在下方历史。
- 本次仅改文档，未改代码，未运行产品测试或 Release。

## 2026-06-21

### 更正 CN 长条语义：中途松开即终结、不可重按接回（仅 HCN 可恢复）

用户在长条 body 视觉改造（P1-A）评审中指出并确认：**CN 当前「松开后可重新按住接回」是开发/文档错误**，正确语义应为——`LN`＝头判 + 长条；`CN`＝头判 + 长条 + 尾判，中途松开即**永久 miss、不可接回**；`HCN`＝头判 + 长条（带持续 gauge 行为）+ 尾判，中途松开后**可重新击打恢复**。此前 `DrawableBmsHoldNote` 把「可接回」错误地门控在 `RequiresTailJudgement()`（CN+HCN 都为真），导致 CN 也能接回（且有测试 `TestCnEarlyReleaseCanRepressAndResolveTail` 固化了该错误行为）。

- **根因 + 修复**：「可接回 / late body press」语义本应只属于 HCN（其 body 被持续判定、接回才有意义）。新增 `BmsLongNoteModeExtensions.AllowsRegrabAfterRelease()`（`== HCN`），把两处门控由 `RequiresTailJudgement()` 改为 `AllowsRegrabAfterRelease()`：① `CanApplyLateBodyPress`（head-miss late-grab / 释放后重按）；② `OnReleased` 的「非命中早释放保持 tail 打开等待接回」分支——现仅 HCN 保留该分支，LN/CN 走「立即 `resolveTail`（早释放＝Miss）」路径，与 LN 一致、不可接回。`RequiresTailJudgement()`（仍 CN+HCN）继续只负责「尾判是否计分」语义，未改；`RequiresBodyGaugeTicks()`（HCN）未改。
- **视觉联动**：body 三态（P1-A）纯派生自 `isHolding`，故本更正后 CN 中途松开→`resolveTail(Miss)`→finalise→body Broken 后随父淡出（与 LN 同），仅 HCN 保留「Broken→重按→Holding」恢复，自动满足用户「仅 HCN 恢复」。
- 测试更正：`TestCnEarlyReleaseCanRepressAndResolveTail` → `TestCnEarlyReleaseResolvesTailAsMissWithoutRegrab`（CN 早释放即 tail Miss + AllJudged + 重按不复活 + body Broken）；`TestCnLatePressStartsHoldAfterHeadMiss` → `TestCnLatePressDoesNotStartHoldAfterHeadMiss`（CN head-miss 后 late press 不起 hold、note 全 miss）；`TestTailJudgedModesAllowLatePressThroughTailMissBoundary`（曾 `[CN][HCN]`）→ `TestHcnAllowsLatePressThroughTailMissBoundaryButCnAndLnNever`（HCN 边界 + CN/LN 恒不可接回）。HCN 既有用例（`TestHcnLateBodyPressStartsHoldAfterHeadMiss`、`TestHoldingThroughTailAutoResolvesHold(CN/HCN/LN)` 等）保持绿。验证：BMS 全套 **936/936**、`osu.Game.Rulesets.Bms.Tests` 0 错。

## 2026-04-20

### 子线正式建档

- `P1-E` 已建立独立目录与四件套文档。
- 当前仅完成文档结构治理，未新增代码、构建或测试执行。
