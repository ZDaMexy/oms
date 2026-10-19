# P1-E 技术约束：gameplay 与长条真实谱面验校

> 最后核对：2026-10-19（补齐回放倒带与HCN漏头接回的状态边界）
> 当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，更正史与验证见 [CHANGELOG.md](CHANGELOG.md)。

1. `P1-E` 只负责真实谱面验校与 gameplay 边界收口，不得借题提前带入 BSS / MSS 或其他 Phase 2 键模式扩张。
2. 长条语义结论必须同步本线实际受影响的状态、计划、约束和验证记录，不能只留在测试命令里，也不机械刷新无变化文件。
3. 若验校结论影响判定、计分、HUD 或反馈表达，回写对应子线；仅影响全局优先级、release gate 或硬约束时向 mainline 回写摘要与链接。同步规则以 [AGENTS](../../../AGENTS.md#权威与文档) 为准。
4. 长条语义必须保持三轴分离：
   - LN 中途松开即终结，不可接回。
   - CN 需要计分尾判，但中途松开即 tail Miss 并终结，不可接回。
   - HCN 需要持续 gauge body 与计分尾判，并且是唯一允许在 tail miss 窗口内重按恢复的模式。
5. `AllowsRegrabAfterRelease()` 只对 HCN 为真，`RequiresTailJudgement()` 只表达 CN/HCN 尾判计分，`RequiresBodyGaugeTicks()` 只表达 HCN body gauge；三者不得互相替代。`DrawableBmsHoldNote.CanApplyLateBodyPress` 与 `OnReleased` 的保持打开分支必须由前者门控，不得恢复曾让 CN 可接回的 `RequiresTailJudgement()` 门控。
6. 长条 body 三态视觉归 P1-A，当前由 `isHolding` 派生；任何 regrab/release 语义改动都必须同步验证 `Broken → Holding` 只可能出现在 HCN。
7. 倒带输入不创建新判定；恢复 holding 只读取现有 entry head/tail、实际 replay pressed state 和对象级自动辅助，不另记第二份历史。首个正向帧须再次对齐；已终结的 LN/CN 不因仍按键而复活，自动辅助无实体动作时仍保留自动 holding。
8. HCN 漏头后合法接回时，后续 body gauge 依实际 holding 恢复；head 保留 Miss，松开仍扣血，再接回才恢复。不得重新加上 head-hit 门控，让 Holding 外观与持续 gauge 相矛盾。
