# P1-C 开发进度：判定语义、绿色数字与反馈闭环

> 最后更新：2026-04-22
> 主线全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。本文件只记录 `P1-C` 的真实进展。

## 当前阶段

- **阶段定位**：C1（权威绿色数字常驻反馈）首轮实现已完成；C2（`Sudden / Hidden / Lift` 联动收口）已从 strict Classic 扩到 tri-mode；C3（判定语义与训练反馈收口）已推进到第七刀，当前已完成“最近判定 + FAST/SLOW”入同一 feedback container、瞬时 judge display 生命周期、compact visual timing-offset sparkline、fixed AAA EX pacemaker 差值、compact live judgement summary、live `DJ LEVEL + EX 原始分子/分母 + %`，以及基于现有 counts 派生、且已带紧凑原因标签的 live `PERFECT / FC / FC LOST` 状态线；与此同时，tri-mode Hi-Speed surface 与 pre-start hold operator window 也已首轮接通。
- **代码状态**：常驻 speed feedback HUD 已落地，并开始承担 gameplay feedback；`BmsScrollSpeedMetrics` / `BmsSpeedMetricsToast` / `Normal / Floating / Classic Hi-Speed` 调速链均已接到统一 feedback 表达，HUD 现在可区分 `NONE` / `ONLY` / 多 target 可切换状态、cycle 序号，单击 `UI_LaneCoverFocus`（或鼠标中键）会在已启用项之间循环切换持久目标（`Sudden → Hidden → Lift → ...`），同时会显示最近一次判定与 `FAST/SLOW` timing 文案；最近判定反馈已改为瞬时 judge display 语义，会在短时间后自动消隐，重复同类判定也会刷新显示窗口；同一张 feedback card 现已接入 compact visual timing-offset sparkline、fixed AAA 目标的 EX pacemaker 文案、两行 compact live judgement summary、live `DJ LEVEL + EX 原始分子/分母 + %`，以及一条基于现有 judgement counts 派生、可进一步显示 `GR` 或最轻 break bucket 的 live `PERFECT / FC / FC LOST` 状态线。当前 tri-mode surface 中，`Normal` 为默认 settings surface，`Floating` 为 initial-BPM anchored runtime surface，`Classic` 继续锁定 `(100000 / 13) / HS` 与官方 sample；进入 gameplay 后则新增 5 秒 delayed-start 窗口，按住 `UI_PreStartHold` 会阻塞开谱并显示 pre-start overlay，期间可按键位奇数列增速、偶数列减速，且 `UI_LaneCoverFocus` / 滚轮 / 中键仍可继续调节 `Sudden / Hidden / Lift` 与 target cycle。`UI_PreStartHold` 与 `UI_LaneCoverFocus` 已拆为独立动作（PreStartHold = 按住阻塞开谱，LaneCoverFocus = 单击循环目标），pre-start 期间 playfield 通过 `SoftUnpause()` 正常渲染。对应 `TestSceneBmsSoloPlayerPreStart` 现已额外锁住“提前松开仍等待 delayed-start”与“click-to-cycle 在 Sudden → Hidden → Lift 之间完整循环”两条分支。display 默认继续通过 `BmsGameplayFeedbackState` 消费 speed metrics / target-state / latest judgement / judgement counts / live EX progress / pacemaker / timing visual range 这批 scalar state，recent history 仍独立流转。与此同时，BMS mod 选中状态与 non-default settings 现已通过 `PersistedModState` 作为 ruleset-local snapshot 持久化；`Sudden` / `Hidden` / `Lift` 的 gameplay wheel 调整则由 `RememberGameplayChanges` 控制是否跨 gameplay clone 回写到当前 selected mod 并延续到回场后状态。考虑 `RulesetConfigCache` 的 startup 顺序后，宿主现会在 cache ready 后 replay 当前 ruleset，因此完全冷启动第一次进入 BMS 也会恢复 selected mods / remembered settings，且不再误报 ruleset failure。
- **文档状态**：`P1-C` 的计划、状态、变动日志、技术约束已与 C3 第七刀和 aggregate scalar state contract 第四刀同步。
- **外部参考状态**：已完成 [iidx.org](https://iidx.org/) 五篇参考文章（green number / hi-speed / floating / in-game controls / IIDX-LR2-beatoraja differences）的审计，GN 公式 / WN 语义 / LIFT 独立性 / soflan 行为均已与当前代码对照确认，结论已写入 `TECHNICAL_CONSTRAINTS.md`。

## 已确认事实

- 当前 `GN / WN` 已在 `BmsScrollSpeedMetrics`、HUD、toast 与 pre-start overlay 中存在。
- GN 公式 `Round(VisibleLaneTime × 0.6)` 与 IIDX 原始公式完全一致（已验证）。
- WhiteNumber = SuddenUnits，不受 LIFT 影响（与 IIDX 一致）。
- Lift 通过几何链（`ScrollLengthRatio`）间接影响 GN（正确行为）。
- 当前常驻 `DefaultBmsSpeedFeedbackDisplay` 已接入 HUD，显示 `GN + 可见毫秒 + 模式缩写和值 + 当前目标`。
- 当前 tri-mode Hi-Speed surface 已完成首轮接线：`Normal`、`Floating`、`Classic` 都可在 settings 与 runtime 间切换，其中 `Classic` 仍锁定 `HS 10 + WN 350 => GN 300`，`Floating` 目前只做到 initial-BPM anchored surface；这仍不等价于完整 FHS。
- BMS mod 选中状态与 remembered settings 现通过 `BmsRulesetConfigManager.PersistedModState` 作为 ruleset-local snapshot 持久化；重启与 ruleset 切换恢复只作用于 BMS，不外溢到 mania。
- 冷启动首轮若发生在 `RulesetConfigCache` ready 前，`OsuGameBase` 现在会延后 replay 当前 ruleset 到 cache ready 后再做 restore；这条时序合同同时修复了 startup ruleset false-failure 与 BMS mod 冷启动漏恢复。
- `Playfield Scale` 已从 settings / runtime config 移除并固定为 `1.0`；数值型 `Playfield Horizontal Offset` 也已退出，改为四态 `Playfield Style`（`1P（居左）` / `2P（居右）` / `居中（左皿）` / `居中（右皿）`）这一不改变 `VisibleLaneTime` / `GreenNumber` 语义的 single-play presentation surface，其中 `1P / 2P` 为“侧停靠但保留固定屏侧间距”。只有 `Sudden / Hidden / Lift` 可以合法影响当前可见时间语义；9K 固定居中，14K 固定双侧布局。
- `DrawableBmsRuleset` 现已暴露 `HiSpeedMode` 与 `SelectedHiSpeed`，pre-start overlay 与 HUD 可共享同一组 runtime 选择状态。
- `BmsInputManager` 现已具备 variant-aware odd/even lane key 调速映射，供 pre-start hold 窗口复用。
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
- 当前 results 页已完成 `DJ LEVEL` / `EX-SCORE` 第一轮收口，但仍不足以形成完整训练反馈闭环。
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
| tri-mode Hi-Speed surface + pre-start hold | 已完成 | settings/runtime/player hook 首轮已接通 |
| `Sudden / Hidden / Lift` HUD 联动（C2） | 已完成 | 已补 target 数量状态、`NONE` / `ONLY` 语义、click-to-cycle 循环与临时覆写文案 |
| `FAST/SLOW` / judge display / pacemaker 家族化（C3） | 进行中 | 已补最近判定 + `FAST/SLOW`、瞬时 judge display 生命周期、compact visual timing-offset、fixed AAA EX pacemaker、compact judgement summary、live `DJ LEVEL + EX 原始分子/分母 + %` 与带紧凑原因标签的 live `PERFECT / FC / FC LOST` 状态线；剩余更丰富 judge display 与后续 pacemaker 扩展 |
| BRJ / LR2 parity 收口 | 未开始 | 当前仍缺 early/late 非对称与 excessive poor 等关键细节 |

## 当前风险

- **术语冒进风险**：如果把当前 GN HUD 直接包装成完整 `FHS`，会与 IIDX 参考约束冲突。
- **接口越权风险**：如果 `P1-C` 绕开 `P1-A` 直接修改旧版 HUD 接口，会破坏现有 skin provider。
- **反馈分裂风险**：如果继续把速度反馈、判定反馈、pacemaker 分散到多条 ad-hoc overlay 链，后续维护成本会继续放大。
- **Soflan 误导风险**：如果在当前 tri-mode surface 下显示 soflan GN 范围，会暗示完整 FHS 已实现。
- **时序风险**：`BmsSoloPlayer` 的 delayed start 已接上，但更严格的 gameplay-start event sequencing 仍需后续 integration coverage 与语义审计。

## 下一检查点

1. 继续推进 C3，把更完整 judge display 与后续 pacemaker 扩展继续收到同一 feedback family。
2. 继续为 tri-mode / pre-start hold 补 dedicated integration coverage；当前已锁住提前松开 delayed-start、persistent target cycle 与 BMS mod 冷启动恢复三条关键分支，下一步重点是更完整的 visual / input-event path 与 delayed-start 时序合同。
3. 推进 C4，把 `GameplayFeedbackDisplay` 的作者文档与 aggregate state contract 继续补齐。

## 验证记录

- 2026-04-22：补齐 BMS mod 冷启动恢复路径；`OsuGameBase` 现会在 `RulesetConfigCache` ready 后 replay 当前 ruleset，避免 startup 首轮丢失 selected mods / remembered settings，并消除 `BMS` / `osu!mania` startup issue 通知。`dotnet build .\osu.Desktop\osu.Desktop.csproj -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过；`dotnet run --project .\osu.Desktop\osu.Desktop.csproj -c Release` 进入 MainMenu 且最新 runtime log 干净；`BmsStartupModPersistenceIntegrationTest`、`BmsModStatePersistenceTest` 合计 **4/4** 通过；手测确认冷启动 / 运行中关开 / 切 mania 往返的 BMS mod 记忆均正确。

- 2026-04-21：完成 BMS mod 记忆与 gameplay adjustment 回写合同。`BmsModStatePersistence` 现会持久化 selected mod 顺序与 non-default settings，`DrawableBmsRuleset` 在 `RememberGameplayChanges` 开启时把 `Sudden` / `Hidden` / `Lift` 的局内滚轮调整同步回当前 selected mod，`SoloSongSelect.revertMods()` 回场前再合并回开局快照。定向 `BmsRulesetConfigurationTest`、`BmsModStatePersistenceTest`、`BmsRulesetModTest` 合计 **56/56** 通过；独立输出目录 `Release` 构建通过。
- 2026-04-20：完成 pre-start 稳定性修复与 `UI_LaneCoverFocus` / `UI_PreStartHold` 语义拆分。修复 clock failure（`Reset(startClock:false)` + `SoftUnpause()`）、playfield 不渲染、LaneCoverFocus 无法切换到 Lift。拆分 `UI_PreStartHold`（hold gate）与 `UI_LaneCoverFocus`（click-to-cycle），新增 `BmsAction.PreStartHold` + 映射 + 本地化。`TestSceneBmsSoloPlayerPreStart` **6/6** 通过；`BmsRulesetModTest` **40/40** 通过；`Build osu! (Release)` 通过。
- 2026-04-20：扩展 `TestSceneBmsSoloPlayerPreStart`，新增"提前松开 hold 仍等待 delayed-start""click-to-cycle 在 Sudden → Hidden → Lift 之间完整循环"两条 start-sequence 回归，并补上 odd/even lane hi-speed 双向断言；定向 `TestSceneBmsSoloPlayerPreStart` **5/5** 通过。
- 2026-04-20：完成 tri-mode Hi-Speed surface 与 pre-start hold 调速窗口首轮接线；`BmsHiSpeedMode`、`BmsHiSpeedRuntimeCalculator`、mode dropdown + current-mode slider、`BmsSoloPlayer` delayed start、`BmsPreStartHiSpeedOverlay`、variant-aware odd/even lane key 调速映射与 paused pre-start `Sudden / Hidden / Lift` 调整链均已落地。定向 `BmsScrollSpeedMetricsTest`、`BmsRulesetConfigurationTest`、`BmsGameplayFeedbackStateTest`、`TestSceneBmsSpeedFeedbackDisplay`、`BmsDrawableRulesetTest` 合计 97/97 通过；`Build osu! (Release)` 通过。
- 2026-04-20：完成 C1 首轮代码实现；定向 `BmsSkinTransformerTest`、`BmsScrollSpeedMetricsTest`、`TestSceneBmsUserSkinFallbackSemantics`、`TestSceneBmsSpeedFeedbackDisplay` 合计 113/113 通过；`Build osu! (Debug)` 通过。
- 2026-04-20：推进 C2 首轮语义收口；定向 `BmsRulesetModTest`、`BmsSkinTransformerTest`、`BmsScrollSpeedMetricsTest`、`TestSceneBmsUserSkinFallbackSemantics`、`TestSceneBmsSpeedFeedbackDisplay` 合计 149/149 通过；`Build osu! (Debug)` 通过。
- 2026-04-20：推进 C2 第二刀；定向 `BmsRulesetModTest`、`BmsSkinTransformerTest`、`BmsScrollSpeedMetricsTest`、`TestSceneBmsUserSkinFallbackSemantics`、`TestSceneBmsSpeedFeedbackDisplay` 合计 150/150 通过；`Build osu! (Debug)` 通过。
- 2026-04-20：推进 C2 第三刀；恢复 `UI_LaneCoverFocus` 的 Hidden 临时覆写，并把 cycle 明确收口到鼠标中键；定向 `BmsRulesetModTest`、`BmsSkinTransformerTest`、`BmsScrollSpeedMetricsTest`、`TestSceneBmsUserSkinFallbackSemantics`、`TestSceneBmsSpeedFeedbackDisplay` 合计 151/151 通过；`Build osu! (Debug)` 通过。
- 2026-04-20：推进 C2 第四刀；新增 `IsAdjustmentTargetTemporarilyOverridden` 状态并让 HUD 在临时覆写时显示 `HOLD`，完成 lane cover focus 与 HUD 文案的代码侧收口；定向 `BmsRulesetModTest`、`BmsSkinTransformerTest`、`BmsScrollSpeedMetricsTest`、`TestSceneBmsUserSkinFallbackSemantics`、`TestSceneBmsSpeedFeedbackDisplay` 合计 152/152 通过；`Build osu! (Debug)` 通过。
- 2026-04-20：推进 C3 首刀；`DrawableBmsRuleset` 新增最近判定 feedback 状态，`DefaultBmsSpeedFeedbackDisplay` 开始在同一 feedback container 中显示最近判定与 `FAST/SLOW` 文案，并对 `EPOOR` 这类无真实 timing 语义的结果省略 timing 后缀；定向 `BmsRulesetModTest`、`BmsSkinTransformerTest`、`BmsScrollSpeedMetricsTest`、`TestSceneBmsUserSkinFallbackSemantics`、`TestSceneBmsSpeedFeedbackDisplay` 合计 155/155 通过；`Build osu! (Debug)` 通过。
- 2026-04-20：推进 C3 第二刀；最近判定 feedback 现按瞬时 judge display 语义自动消隐，重复同类判定会刷新生命周期；`TestSceneBmsSpeedFeedbackDisplay` 已补“过期消隐”和“同值刷新续时”回归，并改用 `display.Time.Current` 对齐组件时钟；定向 `BmsRulesetModTest`、`BmsSkinTransformerTest`、`BmsScrollSpeedMetricsTest`、`TestSceneBmsUserSkinFallbackSemantics`、`TestSceneBmsSpeedFeedbackDisplay` 合计 157/157 通过；`Build osu! (Debug)` 通过。
- 2026-04-20：推进 C3 第三刀；`DrawableBmsRuleset` 新增 recent timing history 与 visual range 状态，`DefaultBmsSpeedFeedbackDisplay` 现会在同一张 feedback card 中显示 compact visual timing-offset sparkline，并只吸收有 timing 语义的 recent basic judgement；`BmsRulesetModTest` 与 `TestSceneBmsSpeedFeedbackDisplay` 已补 runtime / visual 回归，锁定 recent history 过滤与 sparkline 渲染；定向 `BmsRulesetModTest`、`BmsSkinTransformerTest`、`BmsScrollSpeedMetricsTest`、`TestSceneBmsUserSkinFallbackSemantics`、`TestSceneBmsSpeedFeedbackDisplay` 合计 158/158 通过；`Build osu! (Debug)` 通过。
- 2026-04-20：推进 C3 第四刀；`DrawableBmsRuleset` 新增 `ExScorePacemakerInfo`，`DefaultBmsSpeedFeedbackDisplay` 现会在同一张 feedback card 中显示 fixed AAA 目标的 EX pacemaker 差值，且差值按当前已判对象节奏推进；新增 `BmsExScorePacemakerInfoTest` 与 `TestSceneBmsSpeedFeedbackDisplay` 回归，锁定 pacemaker 计算与文案/配色；定向 `BmsExScorePacemakerInfoTest`、`TestSceneBmsSpeedFeedbackDisplay`、`BmsRulesetModTest` 合计 52/52 通过；`Build osu! (Debug)` 通过。
- 2026-04-20：推进 state contract 首刀；新增 `BmsGameplayFeedbackState`，`DefaultBmsSpeedFeedbackDisplay` 已改为优先消费 aggregate scalar snapshot，避免继续直接拼接多组 ruleset scalar bindable；定向 `BmsRulesetModTest`、`TestSceneBmsSpeedFeedbackDisplay`、`BmsSkinTransformerTest`、`BmsGameplayFeedbackLayoutTest`、`TestSceneBmsJudgementDisplayPosition` 合计 154/154 通过；`Build osu! (Debug)` 通过。
- 2026-04-20：推进 state contract 第二刀；`TimingFeedbackVisualRange` 已并入 `BmsGameplayFeedbackState`，`DefaultBmsSpeedFeedbackDisplay` 现已收口为消费 aggregate snapshot + recent history list；新增 `BmsGameplayFeedbackStateTest` 并扩展 `BmsRulesetModTest`、`TestSceneBmsSpeedFeedbackDisplay`、`BmsSkinTransformerTest`，定向合计 153/153 通过；`Build osu! (Debug)` 通过。
- 2026-04-20：推进 C3 第五刀；新增 `BmsJudgementCounts`，并让 `DefaultBmsSpeedFeedbackDisplay` 在同一张 feedback card 中显示两行 compact live judgement summary；与此同时 `BmsGameplayFeedbackState` 已把 judgement counts 也并入 aggregate snapshot。新增 `BmsJudgementCountsTest`，并扩展 `BmsGameplayFeedbackStateTest`、`BmsRulesetModTest`、`TestSceneBmsSpeedFeedbackDisplay`，定向合计 59/59 通过；`Build osu! (Debug)` 通过。
- 2026-04-20：推进 C3 第六刀；新增 `BmsExScoreProgressInfo`，并让 `DefaultBmsSpeedFeedbackDisplay` 在同一张 feedback card 中显示 live `DJ LEVEL + EX %`；与此同时 `BmsGameplayFeedbackState` 已把 live EX progress 也并入 aggregate snapshot。新增 `BmsExScoreProgressInfoTest`，并扩展 `BmsGameplayFeedbackStateTest`、`BmsRulesetModTest`、`TestSceneBmsSpeedFeedbackDisplay`；轻量 ruleset mirror 断言同步改为 null-aware 镜像语义以避开 `CreateDrawableRulesetWith()` 的初始化时序噪音，定向合计 66/66 通过；`Build osu! (Debug)` 通过。
- 2026-04-20：推进 C3 第七刀；`BmsJudgementCounts` 新增 `CanStillPerfect / CanStillFullCombo`，随后又补入 `LeastSevereFullComboBreakResult / LeastSevereFullComboBreakCount` 派生语义，`DefaultBmsSpeedFeedbackDisplay` 现会在同一张 feedback card 中显示带紧凑原因标签的 live `PERFECT / FC / FC LOST` 状态线，并复用既有 results accent，而不为这条训练状态继续扩新的 snapshot 字段；扩展 `BmsJudgementCountsTest` 与 `TestSceneBmsSpeedFeedbackDisplay` 回归，定向合计 69/69 通过；`Build osu! (Debug)` 通过。
- 2026-04-20：完成 strict Classic Hi-Speed + frozen geometry 收口；`DrawableBmsRuleset` 现改用 `(100000 / 13) / HS` base time，`BmsScrollSpeedMetricsTest` 新增官方 sample `HS 10 + WN 350 => GN 300` 回归，`BmsPlayfield` 不再在运行时消费 layout override，`BmsSettingsSubsection` 也已移除 geometry sliders。定向 `BmsScrollSpeedMetricsTest`、`BmsRulesetConfigurationTest`、`TestSceneBmsSpeedFeedbackDisplay`、`TestSceneBmsPlayfieldLayoutConfig`、`BmsLaneLayoutTest`、`BmsDrawableRulesetTest` 合计 91/91 通过；`Build osu! (Release)` 通过。
