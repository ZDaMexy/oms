# P1-A 变动日志

> 本文件只记录 `P1-A` 子线已确认、已验证或已完成挂接的变更摘要。

## 2026-04-23

### onboarding surface 跟进：首次启动向导收口为 OMS 六步流程

- `FirstRunSetupOverlay` 现已固定为六步：欢迎、UI 缩放、获取谱面、导入、难度表设置、按键绑定；这次变更维持主归属 `P1-A`，不为 onboarding / settings-entry surface 新开子线。
- 获取谱面页现改为 mania / BMS 外部站点导流与内部谱库补扫提示；导入页直接复用 `ExternalLibrarySettings`；难度表页通过反射调用 `BmsDifficultyTableManager` 导入 zris 镜像预设；最后一步复用全局、mania 与 BMS keybinding subsection。
- 欢迎页、获取谱面页与导入页的可见文案现已切到 OMS-owned localisation namespace + `.resx`，解决简中继续命中上游翻译的问题；手动重新打开向导并进入旧“游戏表现”页导致的 blank panel / unhandled error 也已一并修复。
- 验证：`dotnet test osu.Game.Tests --filter "FullyQualifiedName~TestSceneFirstRunScreenBehaviour|FullyQualifiedName~TestSceneFirstRunSetupOverlay|FullyQualifiedName~TestSceneFirstRunScreenImportFromStable" --configuration Release` **11/11** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

## 2026-04-22

### gameplay mod surface 修复：冷启动恢复与 startup cache 时序补全

- `OsuGameBase` 现不再把 startup 早期 `RulesetConfigCache` 未 ready 的 path 当作 ruleset failure；BMS mod memory 会先允许无 config 的首轮 apply，并在 cache ready 后 replay 当前 ruleset，补做 `PersistedModState` 恢复。
- 该修复同时消除了启动期误报的 `BMS` / `osu!mania` ruleset issue 通知，以及完全冷启动第一次进入 BMS 时 selected mod 与 remembered settings 丢失的问题。
- 新增 `BmsStartupModPersistenceIntegrationTest`，用“两段式 host 冷启动”回归锁定 BMS 冷启动恢复路径：先 seed `PersistedModState`，再用第二个同名 host 启动 `OsuGameBase`，断言 `BmsModSudden` 选中状态与配置成功恢复。
- 验证：`dotnet build .\osu.Desktop\osu.Desktop.csproj -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过；`dotnet run --project .\osu.Desktop\osu.Desktop.csproj -c Release` 进入 MainMenu 且最新 runtime log 不再出现 startup ruleset 错误；`BmsStartupModPersistenceIntegrationTest` + `BmsModStatePersistenceTest` 合计 **4/4** 通过；手测确认冷启动 / 运行中关开 / 切 mania 往返的 BMS mod 记忆均正确。

## 2026-04-21

### gameplay mod surface 跟进：BMS mod 选项与配置持久化

- `OsuGameBase` 现通过 ruleset-level mod persistence hook 在 BMS ruleset 切入 `BmsModStatePersistence`；当前选中 mod 顺序与 remembered settings 会写入 `BmsRulesetSetting.PersistedModState`，完全重启或切到其他 ruleset 再切回 BMS 时恢复，且不影响 mania。
- `ModSelectOverlay` 不再对实现 `IPreserveSettingsWhenDisabled` 的 configurable BMS mod 在 deselect 时 reset 默认值；`Auto Scratch` / `Auto Note` / `Random` / `Gauge Auto Shift` / `Judge Rank` / `Sudden` / `Hidden` / `Lift` 现在关闭再开启仍保留最后配置。
- `Sudden` / `Hidden` / `Lift` 现新增 `Remember gameplay changes` 开关，默认开启；局内滚轮调整可选择回写到持久化配置，而不是只停留在 gameplay clone 内。
- 验证：定向 `BmsRulesetConfigurationTest`、`BmsModStatePersistenceTest`、`BmsRulesetModTest` 合计 **56/56** 通过；独立输出目录 `Release` 构建通过。

### gameplay surface 跟进：`Playfield Style` 替换数值型 horizontal offset

- `BmsSettingsSubsection` 已移除数值型 `游玩区域水平偏移`，`BmsRulesetConfigManager` 改为声明四态 `Playfield Style`：`1P（居左）`、`2P（居右）`、`居中（左皿）`、`居中（右皿）`。
- 当前基础实现只作用于 single-play 5K / 7K：`1P（居左）` 与 `2P（居右）` 都会侧停靠但保留固定屏侧间距，scratch 视觉分别在左 / 右；两种 `居中` 都保持 playfield 居中，仅改变 scratch 视觉是在左还是右。9K 固定居中，14K 固定双侧布局。这不是完整 `1P/2P flip`，不会翻 bindings，也不会提前承诺 side-aware skin/HUD/BGA 合同。
- 验证：定向 `BmsRulesetConfigurationTest`、`BmsPlayfieldAdjustmentContainerTest`、`BmsLaneLayoutTest`、`TestSceneBmsPlayfieldLayoutConfig`、`BmsDrawableRulesetTest`、`BmsScrollSpeedMetricsTest` 合计 **92/92** 通过；`Build osu! (Release)` 通过。

### gameplay speed setting 跟进：移除 `Playfield Scale` 残余 surface

- `BmsSettingsSubsection` 已移除 `游玩区域缩放`，`BmsRulesetConfigManager` 也不再声明 `PlayfieldScale`；BMS settings surface 不再提供会破坏皮肤编排的整体缩放入口。
- `BmsPlayfieldAdjustmentContainer` 现明确固定为 identity transform；这样 settings / runtime 不会再通过用户缩放或数值横向偏移扭曲 strict visual-speed surface。
- 验证：后续同日回归已扩大到 `BmsLaneLayoutTest`，合计 **90/90** 通过；`Build osu! (Release)` 通过。

## 2026-04-20

### gameplay speed setting 跟进：pre-start hold integration coverage 扩面

- `TestSceneBmsSoloPlayerPreStart` 现额外锁定两类 `BmsSoloPlayer` 预开谱时序语义：提前松开 `UI_LaneCoverFocus` 时 gameplay 仍必须继续等待 delayed-start 到时，以及 hold 期间 persistent target cycle 不得破坏临时 `Hidden` 覆写与松开后的 target 恢复。
- 同一 scene 也补上奇偶列调速双向回归，确认 paused pre-start overlay 下 odd-key 增速与 even-key 减速都能走通正式输入桥。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~TestSceneBmsSoloPlayerPreStart"` **5/5** 通过。

### gameplay speed setting 跟进：tri-mode Hi-Speed surface 与 pre-start hold operator surface 落地

- `BmsHiSpeedMode`、`BmsHiSpeedRuntimeCalculator`、mode dropdown + current-mode slider 已接通；settings 现可在 `Normal / Floating / Classic Hi-Speed` 三种模式间切换，并只显示当前模式数值。
- `DrawableBmsRuleset` 现按模式发布 runtime metrics / HUD detail / toast；`Classic` 继续锁定 `HS 10 + WN 350 => GN 300`，`Floating` 首轮为 initial-BPM anchored surface，但仍不宣称完整 `FHS`。
- `BmsSoloPlayer` 与 `BmsPreStartHiSpeedOverlay` 已把 5 秒 delayed start、`UI_LaneCoverFocus` hold gate、奇偶键调速与 paused pre-start lane-cover 调整链接入正式 gameplay 流程；`SoloSongSelect` 则改为反射创建 `BmsSoloPlayer`，避免跨项目编译期依赖。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~BmsRulesetConfigurationTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~BmsDrawableRulesetTest"` **97/97** 通过；`Build osu! (Release)` 通过。

### gameplay speed setting 跟进：strict Classic Hi-Speed + frozen geometry surface 落地

- `DrawableBmsRuleset` 已把 Classic Hi-Speed 的 base time 从上游 mania 的 `11485 / HS` 改为官方 sample 对齐的 `(100000 / 13) / HS`，并由 `BmsScrollSpeedMetricsTest` 锁定 `HS 10 + WN 350 => GN 300`。
- `BmsPlayfield` 不再在运行时消费 playfield / receptor / bar-line 的 layout override，`BmsSettingsSubsection` 也已移除会扰动 strict profile 的 geometry sliders；内部 `BmsPlayfieldLayoutProfile` abstraction 仍保留给 ruleset / skin 侧使用。
- 当前公开 `Classic Hi-Speed` 范围仍保持 `1.0 - 20.0`，但这次已不只是范围收口，而是把 strict Classic surface 一并锁定。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~BmsRulesetConfigurationTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~TestSceneBmsPlayfieldLayoutConfig|FullyQualifiedName~BmsLaneLayoutTest|FullyQualifiedName~BmsDrawableRulesetTest"` **91/91** 通过；`Build osu! (Release)` 通过。

### gameplay feedback display 跟进：live `PERFECT / FC / FC LOST` 资格线复用现有 snapshot

- `BmsJudgementCounts` 新增 `CanStillPerfect / CanStillFullCombo`，随后又补入 `LeastSevereFullComboBreakResult / LeastSevereFullComboBreakCount` 派生语义，`DefaultBmsSpeedFeedbackDisplay` 现可在不扩 `BmsGameplayFeedbackState` 的前提下显示带紧凑原因标签的 live `PERFECT / FC / FC LOST` 状态线。
- 本次变更确认一部分 richer judge display 语义可以保留在 display 侧派生，而 recent timing history 与 aggregate snapshot 的分层不变。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsExScoreProgressInfoTest|FullyQualifiedName~BmsExScorePacemakerInfoTest|FullyQualifiedName~BmsJudgementCountsTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **69/69** 通过；`Build osu! (Debug)` 通过。

### aggregate gameplay feedback state contract 第四刀：live EX progress 并入 snapshot

- 新增 `BmsExScoreProgressInfo`，把当前 `EX-SCORE / MAX EX-SCORE` 快照为轻量值对象，并并入 `BmsGameplayFeedbackState`。
- `DefaultBmsSpeedFeedbackDisplay` 现继续沿同一 aggregate snapshot contract 显示 live `DJ LEVEL + EX 原始分子/分母 + %`，而 recent timing history 仍保持独立列表态。
- 新增 `BmsExScoreProgressInfoTest`，并扩展 `BmsGameplayFeedbackStateTest`、`BmsRulesetModTest`、`TestSceneBmsSpeedFeedbackDisplay`，锁定 EX 进度值语义、snapshot 镜像与文案显示。
- 验证：后续沿同一 feedback family 的聚焦回归已升至 `dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsExScoreProgressInfoTest|FullyQualifiedName~BmsExScorePacemakerInfoTest|FullyQualifiedName~BmsJudgementCountsTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **69/69** 通过；`Build osu! (Debug)` 通过。

### aggregate gameplay feedback state contract 第三刀：compact judgement counts 并入 snapshot

- 新增 `BmsJudgementCounts`，把 live score statistics 快照为轻量值对象，并并入 `BmsGameplayFeedbackState`。
- `DefaultBmsSpeedFeedbackDisplay` 现继续沿同一 aggregate snapshot contract 显示两行 compact live judgement summary，而 recent timing history 仍保持独立列表态。
- 新增 `BmsJudgementCountsTest`，并扩展 `BmsGameplayFeedbackStateTest`、`BmsRulesetModTest`、`TestSceneBmsSpeedFeedbackDisplay`，锁定 counts 映射、snapshot 值语义、初始镜像与文案显示。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsJudgementCountsTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **59/59** 通过；`Build osu! (Debug)` 通过。

### aggregate gameplay feedback state contract 第二刀：timing visual range 并入 snapshot

- `BmsGameplayFeedbackState` 现已额外包含 `TimingFeedbackVisualRange`，把 timing sparkline 的最后一个 scalar 输入也并入 aggregate snapshot。
- `DefaultBmsSpeedFeedbackDisplay` 现已收口为消费 `GameplayFeedbackState` 加 `RecentJudgementFeedbacks` 列表，不再直接额外绑定 `TimingFeedbackVisualRange` scalar。
- 新增 `BmsGameplayFeedbackStateTest` 并扩展 `BmsRulesetModTest`、`TestSceneBmsSpeedFeedbackDisplay`、`BmsSkinTransformerTest`，锁定 snapshot 值语义、ruleset 镜像和 sparkline/expiry 行为不回退。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~BmsSkinTransformerTest"` **153/153** 通过；`Build osu! (Debug)` 通过。

### aggregate gameplay feedback state contract 首刀落地

- 新增 `BmsGameplayFeedbackState`，把 speed metrics、target-state、最近判定与 fixed AAA pacemaker 这批 scalar feedback 收口为单个 BMS-owned snapshot。
- `DrawableBmsRuleset` 现额外暴露 `GameplayFeedbackState` bindable；`DefaultBmsSpeedFeedbackDisplay` 已改为优先消费该 aggregate state，而不是继续直接绑定多组 ruleset scalar bindable。
- recent timing history 与 visual range 暂时仍保持独立状态流，不把列表态硬塞进同一个 snapshot。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsGameplayFeedbackLayoutTest|FullyQualifiedName~TestSceneBmsJudgementDisplayPosition"` **154/154** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### shared judgement / feedback position contract 首轮落地

- 新增 `BmsGameplayFeedbackLayout`，把默认 gameplay feedback 摆位与 judgement 基线收口到同一条 BMS-owned 位置合同。
- `DrawableBmsJudgement` 不再持有独立的 `140px` judgement 偏移常量，`DefaultBmsHudLayoutDisplay.ApplyGameplayFeedbackDefaults()` 也已统一改为消费 shared contract。
- 新增 `BmsGameplayFeedbackLayoutTest`，并扩展 `TestSceneBmsJudgementDisplayPosition`，锁定 shared contract 的默认摆位与 direction-aware judgement 基线。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsGameplayFeedbackLayoutTest|FullyQualifiedName~TestSceneBmsJudgementDisplayPosition|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **117/117** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### 子线正式建档

- `P1-A` 已从旧的自由命名专题目录中拆出，成为 `doc_md/subline/P1-A/` 的正式子线入口。
- 本子线现固定维护 `DEVELOPMENT_PLAN.md`、`DEVELOPMENT_STATUS.md`、`CHANGELOG.md`、`TECHNICAL_CONSTRAINTS.md`，并与 `P1-C` 保持交叉联动。
- 当前仅完成文档重构与联动挂接，未新增构建或测试执行。
