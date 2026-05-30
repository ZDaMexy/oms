# P1-C 开发进度：判定语义、绿色数字与反馈闭环

> 最后更新：2026-05-25
> 主线全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。本文件只记录 `P1-C` 的真实进展。

## 当前阶段

- **阶段定位**：C1（权威绿色数字常驻反馈）首轮实现已完成；C2（`Sudden / Hidden / Lift` 联动收口）已从 strict Classic 扩到 tri-mode；C2.5（pre-start 1 号普通轨纯视觉流速预览）首轮实现已完成；C3（判定语义与训练反馈收口）的当前范围也已阶段性收口：除既有“最近判定 + FAST/SLOW”、瞬时 judge display 生命周期、compact visual timing-offset sparkline、fixed AAA EX pacemaker 差值、compact live judgement summary、live `DJ LEVEL + EX 原始分子/分母 + %` 与带紧凑原因标签的 live `PERFECT / FC / FC LOST` 状态线外，`CreateStatisticsForScore()` 的 results summary / gauge history consumer proof 也已补齐。与此同时，tri-mode Hi-Speed surface 与 `阻止谱面开始/ingame start` operator window 不仅已接通，也已补上 owner overlay / real-player binding / input-bridge focused coverage；剩余 richer judge display、更多 pacemaker 来源、BRJ/LR2 parity 与更宽 delayed-start integration 统一后置为 backlog，不再作为当前进行中项。
- **代码状态**：常驻 speed feedback HUD 已落地，并开始承担 gameplay feedback；`BmsScrollSpeedMetrics` / `BmsSpeedMetricsToast` / `Normal / Floating / Classic Hi-Speed` 调速链均已接到统一 feedback 表达，HUD 现在可区分 `NONE` / `ONLY` / 多 target 可切换状态、cycle 序号，单击 `UI_LaneCoverFocus`（或鼠标中键）会在已启用项之间循环切换持久目标（`Sudden → Hidden → Lift → ...`），同时会显示最近一次判定与 `FAST/SLOW` timing 文案；最近判定反馈已改为瞬时 judge display 语义，会在短时间后自动消隐，重复同类判定也会刷新显示窗口；同一张 feedback card 现已接入 compact visual timing-offset sparkline、fixed AAA 目标的 EX pacemaker 文案、两行 compact live judgement summary、live `DJ LEVEL + EX 原始分子/分母 + %`，以及一条基于现有 judgement counts 派生、可进一步显示 `GR` 或最轻 break bucket 的 live `PERFECT / FC / FC LOST` 状态线。当前 tri-mode surface 中，`Normal` 为默认 settings surface，`Floating` 为 initial-BPM anchored runtime surface，`Classic` 继续锁定 `(100000 / 13) / HS` 与官方 sample；进入 gameplay 后现已统一为“前 5 秒 delayed-start 阻塞 + 全程调速修饰键”这一运行时合同：前 5 秒按住 `UI_PreStartHold` 时会阻塞开谱并显示 pre-start overlay，期间可按键位奇数列增速、偶数列减速，且 `UI_LaneCoverFocus` / 滚轮 / 中键仍可继续调节 `Sudden / Hidden / Lift` 与 target cycle；若 delayed-start 已在 hold 期间耗尽，则松开 hold 时必须重新给满一段 fresh delay，而不是立即开谱；正式 gameplay 开始后按住同一键仍会继续受理 odd/even lane 调速，并持续刷新居中的 `BMS speed` toast。hold 修饰键按住期间，新的 lane action 不再转发进 gameplay `KeyBindingContainer`，而只属于调整链；`UI_PreStartHold` 与 `UI_LaneCoverFocus` 仍保持独立动作（PreStartHold = 阻止开谱/调速修饰键，LaneCoverFocus = 单击循环目标）。同一条链上，pre-start 1 号普通轨纯视觉流速预览第一版现已落地：`BmsSoloPlayer` 会把 pending / hold / pause state gate 到 `DrawableBmsRuleset`，后者再把 skinnable fake note 挂到第一非 scratch 轨的独立 preview 容器；该 preview 复用 `BmsNoteSkinLookup` 与 `BmsScrollSpeedMetrics`，但不进入 judgement / score / keysound / replay 链，且正式 gameplay 中不会再次出现。对应覆盖现已分成 owner-level `TestSceneBmsPreStartHiSpeedOverlay`、pre-start focused slice 与输入桥 `OmsInputRouterTest` 三层：前者锁住 tri-mode 文案 formatting 与“仅在 overlay 可见时响应 odd/even lane 调速”的组件合同，后两者分别锁住 **24/24** 的 delayed-start / hold modifier / mode-value binding / preview gate / release-after-elapsed fresh-delay 语义，以及 **9/9** 的 lane action 转发抑制。display 默认继续通过 `BmsGameplayFeedbackState` 消费 speed metrics / target-state / latest judgement / judgement counts / live EX progress / pacemaker / timing visual range 这批 scalar state，recent history 仍独立流转。与此同时，BMS mod 选中状态与 non-default settings 现已通过 `PersistedModState` 作为 ruleset-local snapshot 持久化；`Sudden` / `Hidden` / `Lift` 的 gameplay wheel 调整则由 `RememberGameplayChanges` 控制是否跨 gameplay clone 回写到当前 selected mod 并延续到回场后状态。考虑 `RulesetConfigCache` 的 startup 顺序后，宿主现会在 cache ready 后 replay 当前 ruleset，因此完全冷启动第一次进入 BMS 也会恢复 selected mods / remembered settings，且不再误报 ruleset failure。
- **文档状态**：`P1-C` 的计划、状态、变动日志、技术约束已与 C3 当前范围、aggregate scalar state contract 第四刀、2026-05-08 的 pre-start 扩面、2026-05-16 的 pre-start 视觉预览首轮实现，以及 2026-05-25 的 results summary / gauge history consumer proof 同步。
- **外部参考状态**：已完成 [iidx.org](https://iidx.org/) 五篇参考文章（green number / hi-speed / floating / in-game controls / IIDX-LR2-beatoraja differences）的审计，GN 公式 / WN 语义 / LIFT 独立性 / soflan 行为均已与当前代码对照确认，结论已写入 `TECHNICAL_CONSTRAINTS.md`。

## 已确认事实

- 当前 `GN / WN` 已在 `BmsScrollSpeedMetrics`、HUD、toast 与 pre-start overlay 中存在。
- GN 公式 `Round(VisibleLaneTime × 0.6)` 与 IIDX 原始公式完全一致（已验证）。
- WhiteNumber = SuddenUnits，不受 LIFT 影响（与 IIDX 一致）。
- Lift 通过几何链（`ScrollLengthRatio`）间接影响 GN（正确行为）。
- 当前常驻 `DefaultBmsSpeedFeedbackDisplay` 已接入 HUD，显示 `GN + 可见毫秒 + 模式缩写和值 + 当前目标`。
- 当前 tri-mode Hi-Speed surface 已完成首轮接线：`Normal`、`Floating`、`Classic` 都可在 settings 与 runtime 间切换，其中 `Classic` 仍锁定 `HS 10 + WN 350 => GN 300`，`Floating` 目前只做到 initial-BPM anchored surface；这仍不等价于完整 FHS。
- settings 当前还会在当前模式数值后显示“不启用 `Sudden / Hidden / Lift` 的基础下落时间（ms）”；`GreenNumber` 本身仍只在 HUD / toast / pre-start overlay 这条 runtime feedback 链中查看。
- BMS mod 选中状态与 remembered settings 现通过 `BmsRulesetConfigManager.PersistedModState` 作为 ruleset-local snapshot 持久化；重启与 ruleset 切换恢复只作用于 BMS，不外溢到 mania。
- 冷启动首轮若发生在 `RulesetConfigCache` ready 前，`OsuGameBase` 现在会延后 replay 当前 ruleset 到 cache ready 后再做 restore；这条时序合同同时修复了 startup ruleset false-failure 与 BMS mod 冷启动漏恢复。
- `Playfield Scale` 已从 settings / runtime config 移除并固定为 `1.0`；数值型 `Playfield Horizontal Offset` 也已退出，改为四态 `Playfield Style`（`1P（居左）` / `2P（居右）` / `居中（左皿）` / `居中（右皿）`）这一不改变 `VisibleLaneTime` / `GreenNumber` 语义的 single-play presentation surface，其中 `1P / 2P` 为“侧停靠但保留固定屏侧间距”。只有 `Sudden / Hidden / Lift` 可以合法影响当前可见时间语义；9K 固定居中，14K 固定双侧布局。
- `DrawableBmsRuleset` 现已暴露 `HiSpeedMode` 与 `SelectedHiSpeed`，pre-start overlay 与 HUD 可共享同一组 runtime 选择状态。
- 当前 `DrawableBmsRuleset`、`BmsPlayfield`、`BmsHitObjectArea` 与 `BmsScrollSpeedMetrics` 已提供足够的 scroll/time authority，可承接 pre-start 纯视觉流速预览。
- 5K / 7K / 14K 的 raw `laneIndex = 0` 当前是 scratch，因此产品语义上的“1 号轨道”必须解析为第一非 scratch 普通轨；9K 才可直接落第一条 lane。
- `DrawableBmsHitObject`、`BmsLane.OnPressed()`、lane keysound 与 judgement / replay / autoplay authority 不适合承接 preview；若误走这条链，会直接违背“只做视觉反馈”的需求边界。
- `BmsInputManager` 现已具备 variant-aware odd/even lane key 调速映射与 hold 期间 lane-action gameplay 转发抑制，供 `UI_PreStartHold` 这条全程调速修饰链复用。
- 若 delayed-start 已在 pre-start hold 期间耗尽，release 分支当前会重新调度一整段 fresh delay；press-side reset 语义仍保留给 quick re-press。
- `BmsScrollSpeedMetrics` 已具备 `IEquatable<>` 值语义，可安全挂到 Bindable。
- `DrawableBmsRuleset` 已暴露 `SpeedMetrics` 与 `ActiveAdjustmentTarget` 响应式状态。
- `DrawableBmsRuleset` 现会在 `RememberGameplayChanges = true` 时把 `Sudden` / `Hidden` / `Lift` 的局内调整同步回当前全局 selected mod；`SoloSongSelect.revertMods()` 回场前再把这些设置合并回开局快照，避免 gameplay clone 边界吞掉 lane cover / lift 改动。
- `DrawableBmsRuleset` 已暴露 `EnabledAdjustmentTargetCount`，HUD 可显式区分无可调目标、单目标锁定与多目标循环。
- `DrawableBmsRuleset` 已暴露 `ActiveAdjustmentTargetIndex`，HUD 可在多 target 场景显示当前 target 的循环位置（如 `2/3`）。
- `DrawableBmsRuleset` 已暴露 `IsAdjustmentTargetTemporarilyOverridden`，HUD 可显式区分临时覆写态（如从 `AdjustLaneCover(preferBottom)` 产生的临时底层覆写）。
- `DrawableBmsRuleset` 已暴露 `LatestJudgementFeedback`，HUD 可在同一 feedback container 中显示最近一次判定与 `FAST/SLOW` timing 文案。
- `DrawableBmsRuleset` 已暴露 `RecentJudgementFeedbacks` 与 `TimingFeedbackVisualRange`，HUD 可在同一 feedback container 中显示 compact visual timing-offset sparkline。
- `DrawableBmsRuleset` 已暴露 `ExScorePacemakerInfo`，HUD 可在同一 feedback container 中显示 fixed AAA 目标的 EX pacemaker 差值。
- `DrawableBmsRuleset` 已暴露 `GameplayFeedbackState`，把当前 speed metrics、target-state、latest judgement、judgement counts、live EX progress、pacemaker 与 timing visual range 这批 scalar feedback 收口为单个 snapshot bindable。
- `BmsGameplayAdjustmentTarget` 已提取为公共枚举。
- `BmsJudgementTimingFeedback` 已把 `JudgementResult` 快照为轻量值对象，`EPOOR` 等无真实 timing 语义的结果不会附带 `FAST/SLOW` 后缀。
- `BmsJudgementCounts` 已把 live score statistics 快照为轻量值对象，当前 compact judge summary 以 `PGR / GR / GD` 与 `BD / PR / EP` 两行文案显示。
- `BmsJudgementCounts` 现已额外暴露 `CanStillPerfect / CanStillFullCombo` 与 `LeastSevereFullComboBreakResult / LeastSevereFullComboBreakCount` 派生语义，当前 feedback card 可在不扩 snapshot 的前提下显示带紧凑原因标签的 live `PERFECT / FC / FC LOST` 状态线。
- `BmsExScoreProgressInfo` 已把当前 `EX-SCORE / MAX EX-SCORE` 快照为轻量值对象，当前 live progress 以 `DJ LEVEL + EX 原始分子/分母 + %` 文案显示。
- `BmsExScorePacemakerInfo` 已把 fixed AAA 目标的 runtime pacemaker 状态快照为轻量值对象，当前 pacemaker 差值按 `JudgedHits` 对应的目标节奏计算，避免开局就显示整局目标缺口。
- 最近判定 feedback 现为瞬时态：短时自动消隐，且重复的同类判定会刷新显示生命周期而不是沿用旧的过期时钟。
- visual timing-offset 现按 recent timing history 呈现，只吸收有 timing 语义的 basic judgement，不把 `EPOOR` / `ComboBreak` 这类无 timing 语义的结果塞进 sparkline。
- 旧版 `IBmsHudLayoutDisplay` 兼容策略已在 transformer 层收口：新接口直接摆位，旧接口自动 overlay 包装。
- 当前 results 页已完成 `DJ LEVEL` / `EX-SCORE` 第一轮收口；结果侧 `BmsClearLampProcessor` 现也会在 clear check 通过后才授予 `PERFECT` / `FULL COMBO`，并在重建 final gauge / gauge history 时消费 caller 提供的已带 mods playable beatmap，不再在 helper 内重复应用 beatmap mods，因此 `HCN` body-tick fail 与 `A-SCR` / `A-NOT` assist 场景已不再偏离 gameplay authority；[../../osu.Game.Rulesets.Bms.Tests/BmsRulesetStatisticsTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsRulesetStatisticsTest.cs) 现也已用 plain focused proof 同时锁住 `CreateStatisticsForScore()` 的 results summary consumer 与 gauge history consumer：summary 侧会消费 selected mode / EX-SCORE / DJ LEVEL / computed clear lamp state，而 gauge history 侧会消费完整的 auto-shift timeline state，不再只停留在 panel type proof。当前 `P1-C` 的已落地反馈家族与结果侧 consumer proof 已阶段性收口；剩余 richer judge display / parity / delayed-start dedicated integration 统一后置为 backlog。
- 当前 `BEATORAJA` / `LR2` judge mode 已接通基础窗口与分桶，但 parity 仍未收口。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 子线归线到 `P1-C` | 已完成 | 主线文档已挂接 |
| 绿色数字与 speed feedback 审计 | 已完成 | 当前模型与风险点已明确 |
| 子线级计划 / 状态 / 约束文档 | 已完成 | 文档位于当前目录 |
| C1 外部参考验证（IIDX GN/WN/LIFT/FHS） | 已完成 | 公式 / 语义已确认一致 |
| C1 详细实现方案 | 已完成 | 7 步实施 + 文件清单已写入 PLAN |
| C1.1 提取 `BmsGameplayAdjustmentTarget` 公共枚举 | 已完成 | 已移出 `DrawableBmsRuleset` 私有作用域 |
| C1.2 响应式 `SpeedMetrics` Bindable | 已完成 | `DrawableBmsRuleset` 已暴露 Bindable，`BmsScrollSpeedMetrics` 已补 `IEquatable<>` |
| C1.3 `BmsSkinComponents.SpeedFeedback` 注册 | 已完成 | 枚举 + transformer case 已接通 |
| C1.4 `IBmsSpeedFeedbackDisplay` 接口与默认实现 | 已完成 | 默认显示已落地 |
| C1.5 HUD 布局集成 | 已完成 | 通过兼容扩展接口 + legacy overlay wrapper 收口 |
| C1.6 Toast 语义退位 | 已完成 | toast 保留为操作确认层，不再是唯一权威反馈 |
| C1.7 测试 | 已完成 | 单元 + 视觉 + 皮肤 fallback 已验证 |
| tri-mode Hi-Speed surface + `阻止谱面开始/ingame start` | 已完成 | settings/runtime/player hook 与 ingame hold modifier 语义已接通 |
| `Sudden / Hidden / Lift` HUD 联动（C2） | 已完成 | 已补 target 数量状态、`NONE` / `ONLY` 语义、click-to-cycle 循环与临时覆写文案 |
| pre-start 1 号普通轨纯视觉流速预览（C2.5） | 已完成首轮实现 | 纯视觉 lane preview path、第一非 scratch 轨选择、pause freeze 与“正式 gameplay 不再出现 preview”已由 `TestSceneBmsSoloPlayerPreStart` **24/24** 锁定 |
| `FAST/SLOW` / judge display / pacemaker 家族化（C3） | 已阶段性收口 | 当前范围内的 feedback family 与 results-side consumer proof 已闭合；更丰富 judge display 与后续 pacemaker 来源后置为 backlog |
| BRJ / LR2 parity 收口 | 后置 backlog | 当前仍缺 early/late 非对称与 excessive poor 等关键细节，但不再作为本轮进行中事项 |

## 当前风险

- **术语冒进风险**：如果把当前 GN HUD 直接包装成完整 `FHS`，会与 IIDX 参考约束冲突。
- **接口越权风险**：如果 `P1-C` 绕开 `P1-A` 直接修改旧版 HUD 接口，会破坏现有 skin provider。
- **反馈分裂风险**：如果继续把速度反馈、判定反馈、pacemaker 分散到多条 ad-hoc overlay 链，后续维护成本会继续放大。
- **Soflan 误导风险**：如果在当前 tri-mode surface 下显示 soflan GN 范围，会暗示完整 FHS 已实现。
- **preview 越权风险**：如果 pre-start 流速预览误走 `DrawableBmsHitObject` / lane 判定 / keysound 链，就会直接破坏“只做视觉反馈”的前提，并把 preview 变成 gameplay authority 的脏副本。
- **时序风险**：`BmsSoloPlayer` 的 delayed start 已接上，但更严格的 gameplay-start event sequencing 仍需后续 integration coverage 与语义审计。

## 下一检查点

1. 当前不再为 C2.5 继续开新实现；只维持 gate、第一非 scratch 轨选择与“无判定 / 无键音 / 无 replay side effect”三条硬约束。
2. 当前不再为 C3 继续开新刀；现有 compact feedback family 与 results-side consumer proof 作为本阶段收口基线维持。
3. tri-mode / `阻止谱面开始/ingame start` 的更宽 visual / input-event path 与 delayed-start dedicated integration 改列 backlog，若未来重新开启，必须继续沿既有 overlay / input-bridge authority 推进。
4. `GameplayFeedbackDisplay` 的作者文档与 aggregate state contract 扩面改列 backlog，不再作为本页当前进行中事项。

## 历史变动与验证

- 当前仍影响判读的验证结论已在“当前阶段”和“进度矩阵”中汇总；按日期展开的功能切片、回归命令与构建记录见 [CHANGELOG.md](CHANGELOG.md)。
