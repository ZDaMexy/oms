# P1-C 变动日志

> 本文件只记录 `P1-C` 子线已确认、已验证或已完成挂接的变更摘要。

## 2026-04-20

### C2 补回归：pre-start hold start-sequence integration coverage 扩面

- `TestSceneBmsSoloPlayerPreStart` 现额外锁定 `BmsSoloPlayer` 的两个 paused pre-start 分支：提前松开 `UI_LaneCoverFocus` 时必须继续等待 delayed-start 到时，以及 hold 期间 persistent target cycle 不得破坏临时 `Hidden` 覆写与松开后的 target 恢复。
- 同一 scene 也补上奇偶列 hi-speed 双向调节回归，确保 odd/even lane mapping 在正式 pre-start overlay 输入桥上两边都有效。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~TestSceneBmsSoloPlayerPreStart"` **5/5** 通过。

### C2 扩面：tri-mode Hi-Speed surface 与 pre-start hold 调速窗口落地

- `BmsHiSpeedMode`、`BmsHiSpeedRuntimeCalculator`、mode dropdown + current-mode slider 已接通；`BmsScrollSpeedMetrics` / HUD / toast 现会按 `Normal / Floating / Classic` 三模式发布 mode-aware 反馈。
- `Floating` 首轮已具备 initial-BPM anchored runtime surface，并可在 pre-start hold 窗口中与 `Sudden / Hidden / Lift` 联动调节；但仍不宣称完整 mid-song re-float parity 或 soflan GN range。
- `BmsSoloPlayer` 与 `BmsPreStartHiSpeedOverlay` 已把 5 秒 delayed-start、`UI_LaneCoverFocus` hold gate、奇偶列按键调速与 paused pre-start lane-cover 调整链接入正式 gameplay 流程。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~BmsRulesetConfigurationTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~BmsDrawableRulesetTest"` **97/97** 通过；`Build osu! (Release)` 通过。

### C1 strict 收口：Classic Hi-Speed 数值映射与 geometry profile 一并锁定

- `DrawableBmsRuleset` 已把 Classic Hi-Speed 的 base time 从上游 mania 的 `11485 / HS` 改为官方 sample 对齐的 `(100000 / 13) / HS`。
- `BmsScrollSpeedMetricsTest` 现新增 `HS 10 + WN 350 => GN 300` 回归，锁定 strict Classic surface 的核心 sample。
- `BmsPlayfield` 不再在运行时消费 playfield / receptor / bar-line 的 layout override，`BmsSettingsSubsection` 也已移除 geometry sliders，当前 geometry profile 已冻结为 strict surface。
- 当前公开 `Classic Hi-Speed` 范围仍保持 `1.0 - 20.0`，但这次已不只是范围收口，而是把 strict Classic surface 一并锁定。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~BmsRulesetConfigurationTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~TestSceneBmsPlayfieldLayoutConfig|FullyQualifiedName~BmsLaneLayoutTest|FullyQualifiedName~BmsDrawableRulesetTest"` **91/91** 通过；`Build osu! (Release)` 通过。

### C3 第七刀：live `PERFECT / FC / FC LOST` 资格线入同一 feedback card

- `BmsJudgementCounts` 新增 `CanStillPerfect / CanStillFullCombo`，随后又补入 `LeastSevereFullComboBreakResult / LeastSevereFullComboBreakCount` 派生语义，允许直接从现有 live counts 推导当前 run 是否仍保 `PERFECT` / `FULL COMBO`，以及 `FC LOST` 的紧凑原因标签。
- `DefaultBmsSpeedFeedbackDisplay` 现会在同一张 feedback card 中显示带原因标签的 live 状态线，例如 `LIVE FC | GR 1` 与 `FC LOST | GD 1`，并复用 results palette 的 `PERFECT` / `FULL COMBO` accent，而不是再扩新的 runtime state contract。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsExScoreProgressInfoTest|FullyQualifiedName~BmsExScorePacemakerInfoTest|FullyQualifiedName~BmsJudgementCountsTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **69/69** 通过；`Build osu! (Debug)` 通过。

### C3 第六刀：live `DJ LEVEL + EX %` 入同一 feedback card

- 新增 `BmsExScoreProgressInfo`，把当前 `EX-SCORE / MAX EX-SCORE` 快照为轻量值对象，并沿 `GameplayFeedbackState` 进入同一条 feedback state 流。
- `DefaultBmsSpeedFeedbackDisplay` 现会在同一张 feedback card 中显示 live `DJ LEVEL + EX 原始分子/分母 + %`，把即时 EX 进度也收进既有训练反馈容器，而不是再开一条独立 overlay。
- `BmsGameplayFeedbackState` 现也已包含 live EX progress，继续把 gameplay feedback 的 scalar 输入向单个 snapshot 收口，而 recent history 仍独立流转。
- 验证：后续沿同一 feedback family 的聚焦回归已升至 `dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsExScoreProgressInfoTest|FullyQualifiedName~BmsExScorePacemakerInfoTest|FullyQualifiedName~BmsJudgementCountsTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **69/69** 通过；`Build osu! (Debug)` 通过。

### C3 第五刀：compact judgement summary 入同一 feedback card

- 新增 `BmsJudgementCounts`，把 live score statistics 快照为轻量值对象，并沿 `GameplayFeedbackState` 进入同一条 feedback state 流。
- `DefaultBmsSpeedFeedbackDisplay` 现会在同一张 feedback card 中显示两行 compact live judgement summary：`PGR / GR / GD` 与 `BD / PR / EP`。
- `BmsGameplayFeedbackState` 现也已包含 judgement counts，继续把 gameplay feedback 的 scalar 输入向单个 snapshot 收口，而 recent history 仍独立流转。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsJudgementCountsTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **59/59** 通过；`Build osu! (Debug)` 通过。

### state contract 第二刀：timing visual range 并入 aggregate snapshot

- `BmsGameplayFeedbackState` 现已额外包含 `TimingFeedbackVisualRange`，让 compact timing sparkline 的 scalar 输入也收进同一条 snapshot contract。
- `DefaultBmsSpeedFeedbackDisplay` 现已收口为消费 `GameplayFeedbackState` 加 `RecentJudgementFeedbacks`，不再额外直接绑定 `TimingFeedbackVisualRange` scalar。
- 新增 `BmsGameplayFeedbackStateTest`，并扩展 `BmsRulesetModTest`、`TestSceneBmsSpeedFeedbackDisplay`、`BmsSkinTransformerTest`，锁定 snapshot 值语义、ruleset 镜像与 sparkline/expiry 行为。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~BmsSkinTransformerTest"` **153/153** 通过；`Build osu! (Debug)` 通过。

### state contract 首刀：feedback card 改吃 aggregate scalar snapshot

- 新增 `BmsGameplayFeedbackState`，把 speed metrics、target-state、最近判定与 fixed AAA pacemaker 这批 scalar gameplay feedback 收口为单个 snapshot。
- `DrawableBmsRuleset` 现额外暴露 `GameplayFeedbackState`；`DefaultBmsSpeedFeedbackDisplay` 已改为优先消费该 aggregate state，而不是继续分别绑定多组 ruleset scalar bindable。
- recent timing history 与 visual range 仍保持独立状态流，避免把列表态与瞬时标量语义硬塞进同一个 snapshot。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsGameplayFeedbackLayoutTest|FullyQualifiedName~TestSceneBmsJudgementDisplayPosition"` **154/154** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### C3 第四刀：fixed AAA EX pacemaker 入同一 feedback card

- `DrawableBmsRuleset` 新增 `ExScorePacemakerInfo`，把 fixed AAA 目标的 runtime EX pacemaker 状态暴露给 HUD。
- `DefaultBmsSpeedFeedbackDisplay` 现会在同一张 feedback card 中显示 `PAC AAA +/-n` 文案，并按当前已判对象的目标节奏推进差值，而不是从开局起显示整局最终目标缺口。
- 新增 `BmsExScorePacemakerInfoTest`，`TestSceneBmsSpeedFeedbackDisplay` 已补 pacemaker 文案与配色回归。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsExScorePacemakerInfoTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~BmsRulesetModTest"` **52/52** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### C3 第三刀：compact visual timing-offset 入同一 feedback card

- `DrawableBmsRuleset` 新增 `RecentJudgementFeedbacks` 与 `TimingFeedbackVisualRange`，把 recent timing history 与当前局 visual range 暴露给 HUD。
- `DefaultBmsSpeedFeedbackDisplay` 现会在同一张 feedback card 中显示 compact visual timing-offset sparkline，并按 recent history 呈现最近若干次有 timing 语义的偏移。
- `EPOOR` / `ComboBreak` 这类无 timing 语义的结果不会进入 sparkline；recent judgement 文本与 sparkline 现统一消费同一条 ruleset 状态流。
- `BmsRulesetModTest` 与 `TestSceneBmsSpeedFeedbackDisplay` 已补 runtime / visual 回归，锁定 recent history 过滤与 sparkline 渲染。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **158/158** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### C3 第二刀：最近判定 feedback 改为瞬时 judge display

- `DefaultBmsSpeedFeedbackDisplay` 里的最近判定 feedback 不再永久停留，而是按短时 judge display 语义自动消隐。
- 相同判定与相同 `FAST/SLOW` 偏移再次出现时，显示窗口会被刷新，而不是沿用旧的过期时钟。
- `TestSceneBmsSpeedFeedbackDisplay` 已补“过期消隐”和“同值刷新续时”回归，并改用 `display.Time.Current` 对齐组件自己的时钟。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **157/157** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### C3 首刀：最近判定与 `FAST/SLOW` 入 feedback container

- `DrawableBmsRuleset` 新增 `LatestJudgementFeedback` bindable，并通过 `BmsJudgementTimingFeedback` 将 `JudgementResult` 快照为轻量 HUD 状态。
- `DefaultBmsSpeedFeedbackDisplay` 现会在同一 feedback container 中显示最近一次判定与 `FAST/SLOW` timing 文案，例如 `PGREAT | FAST 3.2ms`。
- `EPOOR` 这类无真实 timing 语义的结果会只显示判定名，不再硬附 `FAST/SLOW` 后缀。
- `BmsRulesetModTest` 与 `TestSceneBmsSpeedFeedbackDisplay` 已补 runtime / visual 回归，锁定最近判定、`FAST/SLOW` 与 `EPOOR` 的显示语义。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **155/155** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### C2 第四刀：HUD 显式区分临时覆写 `HOLD` 状态

- `DrawableBmsRuleset` 新增 `IsAdjustmentTargetTemporarilyOverridden` bindable，用于把“当前显示 target 是否是临时覆写”暴露给 HUD。
- `DefaultBmsSpeedFeedbackDisplay` 在按住 `UI_LaneCoverFocus` 导致的临时覆写场景下，现会显示 `HID HOLD` 这类显式文案，而不是继续沿用普通 cycle 文案。
- `BmsRulesetModTest` 与 `TestSceneBmsSpeedFeedbackDisplay` 已补运行时与视觉回归，锁定临时覆写开关与 `HOLD` 文案替换语义。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **152/152** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### C2 第三刀：恢复 `UI_LaneCoverFocus` 的 Hidden 临时覆写

- `DrawableBmsRuleset` 已恢复 `UI_LaneCoverFocus` 的按住型语义：按住时滚轮临时转向 `Hidden`（若可用），松开后回到持久 target。
- target cycle 入口已明确收口到鼠标中键点击，不再借用 `LaneCoverFocusPressed` 信号本身。
- `BmsRulesetModTest` 新增回归，锁定“临时覆写不会改写持久 target，松开后会回退到持久 target”的运行时语义。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **151/151** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### C2 第二刀：多 target cycle 序号入 HUD

- `DrawableBmsRuleset` 新增 `ActiveAdjustmentTargetIndex` bindable，把当前 target 在可切换列表中的位置暴露给 HUD。
- `DefaultBmsSpeedFeedbackDisplay` 在多 target 状态下不再只显示当前 target 简写，而是显示显式 cycle 序号，例如 `SUD 1/3`、`HID 2/3`、`LIFT 3/3`。
- `BmsRulesetModTest` 现锁定无 target 为 `-1`、单 target 为 `0`、多 target cycle 为 `0 -> 1 -> 2 -> 0` 的运行时语义；`TestSceneBmsSpeedFeedbackDisplay` 已补 `1/3` 与 `2/3` 视觉回归。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **150/150** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### C2 首轮收口：speed feedback target-state 语义细化

- `DrawableBmsRuleset` 新增 `EnabledAdjustmentTargetCount` bindable，用于把 runtime 中“当前有多少可调目标可用”暴露给 HUD。
- `DefaultBmsSpeedFeedbackDisplay` 现在会区分三类状态：无 target 时显示 `NONE`、只有一个可调目标时显示 `{TARGET} ONLY`、多个 target 可切换时显示当前激活 target。
- `BmsRulesetModTest` 新增无 target / 单 target / 多 target 三类状态断言；`TestSceneBmsSpeedFeedbackDisplay` 新增 `ONLY` 与 `NONE` 文案回归。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **149/149** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### C1 权威绿色数字：首轮实现与视觉回归验证

- 新增 `BmsGameplayAdjustmentTarget` 公共枚举，并将 `SUD / HID / LIFT` 文案格式化逻辑移到公共扩展，供 HUD 与 toast 共享。
- `DrawableBmsRuleset` 新增 `SpeedMetrics` 与 `ActiveAdjustmentTarget` bindable；`BmsScrollSpeedMetrics` 补齐 `IEquatable<>` 值语义，speed feedback 可直接响应 runtime 调速链。
- 新增 `BmsSkinComponents.SpeedFeedback`、`IBmsSpeedFeedbackDisplay` 与 `DefaultBmsSpeedFeedbackDisplay`，常驻 HUD 现显示 `GN + 可见毫秒 + HS + 当前目标`，当可见区域被完全遮挡时显示 `GN ---` 警告态。
- `BmsSkinTransformer` 现会在 `MainHUDComponents` 中统一组装 `SpeedFeedback`；新 HUD layout 可通过 `IBmsHudLayoutDisplayWithGameplayFeedback` 自定义摆位，旧 HUD layout 则自动包一层 overlay 容器，保持兼容。
- 新增 `TestSceneBmsSpeedFeedbackDisplay`，并扩展 `BmsSkinTransformerTest` / `BmsScrollSpeedMetricsTest` / `TestSceneBmsUserSkinFallbackSemantics`，覆盖值语义、fallback、legacy HUD 兼容与 speed feedback 文案/警告态。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **113/113** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### C1 权威绿色数字：外部参考审计与详细实现规划

- 完成 [iidx.org](https://iidx.org/) 五篇外部参考文章审计：`green number / white number / LIFT`（差异对照）、`Classic Hi-Speed`、`Floating Hi-Speed`、`In-game Controls`（hi-speed options）、`External Resources`。
- 确认当前 `BmsScrollSpeedMetrics.GreenNumber` 公式 `Round(VisibleLaneTime × 0.6)` 与 IIDX 原始公式（`10 × note 可见帧数 @60fps`）完全一致。
- 确认 `WhiteNumber = SuddenUnits` 语义与 IIDX 一致（不受 LIFT 影响）；Lift 通过 `ScrollLengthRatio` 几何链间接影响 GN（正确行为）。
- 确认 soflan（曲内 BPM 变化）下只显示基准 GN 是 Classic Hi-Speed 的合理行为；soflan GN 范围显示留待 FHS。
- C1 详细实现方案已写入 `DEVELOPMENT_PLAN.md`，包含 7 步实施路径（C1.1 提取公共枚举 → C1.2 响应式 Bindable → C1.3 皮肤组件注册 → C1.4 接口与默认实现 → C1.5 HUD 布局集成 → C1.6 Toast 语义退位 → C1.7 测试）与完整文件变动清单。
- `TECHNICAL_CONSTRAINTS.md` 新增"GN 公式约束"段落，锁定 5 条硬约束。
- 本轮仅文档与规划变更，未新增构建或测试执行。

### 子线正式建档

- `P1-C` 已作为正式子线入口建立在 `doc_md/subline/P1-C/`，主承接绿色数字、速度反馈、判定语义与训练反馈闭环。
- 本子线现固定维护 `DEVELOPMENT_PLAN.md`、`DEVELOPMENT_STATUS.md`、`CHANGELOG.md`、`TECHNICAL_CONSTRAINTS.md`，并与 `P1-A` 保持交叉联动。
- 当前仅完成文档重构与联动挂接，未新增构建或测试执行。