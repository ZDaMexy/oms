# P1-E 技术约束：gameplay 与长条真实谱面验校

> 最后更新：2026-07-16（文档健康治理；稳定合同未改变）
> 当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，更正史与验证见 [CHANGELOG.md](CHANGELOG.md)。

1. `P1-E` 只负责真实谱面验校与 gameplay 边界收口，不得借题提前带入 BSS / MSS 或其他 Phase 2 键模式扩张。
2. 任何长条语义结论都必须同时同步到计划、状态与验证记录，不能只留在测试命令里。
3. 若验校结论影响判定、计分、HUD 或反馈表达，必须回写对应子线与 `../../mainline/` 文档。
4. 长条语义必须保持三轴分离：
   - LN 中途松开即终结，不可接回。
   - CN 需要计分尾判，但中途松开即 tail Miss 并终结，不可接回。
   - HCN 需要持续 gauge body 与计分尾判，并且是唯一允许在 tail miss 窗口内重按恢复的模式。
5. `AllowsRegrabAfterRelease()` 只对 HCN 为真，`RequiresTailJudgement()` 只表达 CN/HCN 尾判计分，`RequiresBodyGaugeTicks()` 只表达 HCN body gauge；三者不得互相替代。`DrawableBmsHoldNote.CanApplyLateBodyPress` 与 `OnReleased` 的保持打开分支必须由前者门控，不得恢复曾让 CN 可接回的 `RequiresTailJudgement()` 门控。
6. 长条 body 三态视觉归 P1-A，当前由 `isHolding` 派生；任何 regrab/release 语义改动都必须同步验证 `Broken → Holding` 只可能出现在 HCN。
