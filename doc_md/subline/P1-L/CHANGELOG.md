# P1-L 变更日志：BMS 演出/Gimmick 谱视觉复刻

> 本文件记录 `P1-L` 相关的验证通过变更，按时间倒序排列。
> 当前进度见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，执行规划见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

---

## 2026-05-29

### Phase 2 Step D：演出谱自动检测（`Auto` 可用，默认仍 OFF）

让门控 `Auto` 仅对自动识别为演出谱的谱启用旁路。检测为 `BmsScrollProfile` 的纯函数，基于两条保守信号（实测 DEAD SOUL vs 正常谱区分度极大）：

- [BmsScrollProfile](../../osu.Game.Rulesets.Bms/Beatmaps/BmsScrollProfile.cs) 新增纯指标 `MaxSlope`（最快段相对 base 的倍率：base≈1、STOP=0、132 万 BPM snap≈10000）与 `FrozenFraction`（STOP 冻结时长占比），及 `IsStopMotionGimmick = MaxSlope ≥ 50 || FrozenFraction ≥ 0.05`。正常/中等 soflan（< ~10×、~0% 冻结）稳不触发；DEAD SOUL（10000×、43%）必触发。
- [BmsPlayfield.updateGimmickScroll](../../osu.Game.Rulesets.Bms/UI/BmsPlayfield.cs)：`Auto` → `profile.IsStopMotionGimmick` 才 engage；`On` 恒 engage、`Off` 恒不。
- **默认改为 `Auto`**（用户拍板）：特效/变速谱开箱即用，正常谱因保守检测不命中而走常规路径、零改动；`Off` 仍是回退开关。设置面板文案改为发行向（caption「特效谱滚动（实验性）」+「若出现异常调回 Off」）。**红线**：默认 Auto 下「非 gimmick 谱零改动」依赖检测无误报（已保守）+ `Off` 兜底。
- 验证：新增 `BmsScrollProfileTest` 检测项（snap/freeze-only → true，normal/moderate-soflan → false）+ converter 端真实检测（extreme-BPM 谱 true、单 BPM 谱 false）；默认改 Auto 后 BMS 全套 **860/860**（gameplay TestScene 默认 Auto、简单测试谱不命中检测 → 行为不变）；Release 0 错误、生产代码 0 新增警告。

### 修复：最右键轨道地雷不显示（Phase 1 既有 off-by-one，Phase 2 暴露）

人工验证 DEAD SOUL 时发现 7K **最右键轨道（lane 7）地雷不渲染**。根因：`BmsBeatmapConverter.buildMines` 用 `BmsRuleset.GetKeyCount`（**键数**=7）做 `laneIndex >= bound` 丢弃上界，但 scratch 占 lane 0 使最右键映射到 lane index 7（`mapLaneIndex(7K,0x19)=7`），`7>=7` 被误丢；音符路径无此检查故不受影响。5K(lane5)/14K(lane14,15) 同类受害。

- 新增权威 [BmsRuleset.GetLaneCount](../../osu.Game.Rulesets.Bms/BmsRuleset.cs)`(keymode)`（键 + scratch：5K=6/7K=8/9K=9/14K=16），`buildMines` 改用之；越界（如单打谱里的 P2 通道）仍丢弃，不 mis-map。
- DRY：[BmsLaneLayout.getExpectedLaneCount](../../osu.Game.Rulesets.Bms/UI/BmsLaneLayout.cs) 委托给 `BmsRuleset.GetLaneCount`，消除两份重复的 keymode→轨道数映射（单一真源，防再犯）。
- 回归：新增 `TestBuildsMineOnRightmostKeyLane`（7K channel D9 → 地雷落在 lane 7 不被丢）；BMS 全套 **855/855**；Release 0 错误、生产代码 0 新增警告。

### Phase 2（Step A–C）：BMS 专用滚动位置积分旁路落地（门控默认 OFF）

落地 beatoraja 风格的逐对象位置积分旁路，让 DEAD SOUL [Revive] 这类定格动画演出谱的「瞬移 snap / STOP 真冻结 / measure-length 任意定高」成立。**绕开而非改写**共享核心：不动 `TimingControlPoint` 的 `[6,60000]` 钳制、不动 `ScrollingHitObjectContainer`，注入全在 BMS 侧；**判定/计分继续走 `HitObject.StartTime` 时间链路，语义不变**；**门控默认 OFF，对所有谱（含演出谱）渲染零变化**。

**Step A — 位置积分数据模型（零渲染改动）**
- 新增 [../../../osu.Game.Rulesets.Bms/Beatmaps/BmsScrollProfile.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsScrollProfile.cs)：纯函数分段线性 `D(t)`（`DistanceAt`/`PositionDelta`/`TimeAtDistance`，二分 + 端点外推），无 framework 依赖、可独立单测。
- [BmsBeatmapConverter.buildEventTimeline](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs) 在既有时间游走里**并行积分** D 的 knots（用原始未钳制 BPM/STOP/measure-length/scroll；STOP 段 `dD=0` 真冻结、132 万 BPM 段斜率暴涨 = snap），经 `TimelineBuildResult` 挂到 [BmsBeatmap.ScrollProfile](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs)（不进 `HitObjects`）。距离按非冻结时长最常见 BPM 缩放成 base-BPM ms（`computeBaseBpm`）。

**Step B — stop-motion 算法 + IScrollingInfo 重缓存（门控 OFF）**
- 新增 [BmsStopMotionScrollAlgorithm](../../osu.Game.Rulesets.Bms/UI/Scrolling/BmsStopMotionScrollAlgorithm.cs)`: IScrollAlgorithm`（包 `BmsScrollProfile`，5 个接口方法以 D/D⁻¹ 实现，与 `ConstantScrollAlgorithm` 同形而以距离替代时间）。
- 新增 [BmsScrollingInfo](../../osu.Game.Rulesets.Bms/UI/Scrolling/BmsScrollingInfo.cs)`: IScrollingInfo`：包裹基类，Direction/TimeRange 透传，Algorithm 默认 `GetBoundCopy` 逐实例跟随基类，仅 `EngageStopMotion`/`Disengage` 切换。
- [BmsPlayfield.CreateChildDependencies](../../osu.Game.Rulesets.Bms/UI/BmsPlayfield.cs) 把 `BmsScrollingInfo` `CacheAs<IScrollingInfo>` 给子节点（lanes + 容器）——**零核心文件改动的注入点**；解析不到基类 info 时退回基类行为（防御）。
- 新增门控 [BmsGimmickScrollMode](../../osu.Game.Rulesets.Bms/Configuration/BmsGimmickScrollMode.cs)`{ Off, On, Auto }` + `BmsRulesetSetting.GimmickScrollMode` 默认 `Off`；`BmsPlayfield.updateGimmickScroll` 按门控 engage/disengage。

**Step C — 标定 + 显式 ON 入口 + 端到端冻结验证**
- **标定关键发现（实测 DEAD SOUL）**：因 STOP 冻结占 43.1%（141s 中 60.8s，stop-freeze 点 beatLength=6）+ 132 万 BPM 钳到 6，`GetMostCommonBeatLength` 返回 **6（BPM 10000）而非 132**；正常链路因此把 132 段压成 multiplier≈0.013（squash 实锤）。但**默认 Normal hi-speed 模式下 `timeRange` 与 `GetMostCommonBeatLength` 无关**（modeScale=1，见 `BmsHiSpeedRuntimeCalculator`），而 profile `baseBeatLength=454.5`（原始 132）使 base 段 `D≈t` → 旁路 base 段 PositionAt 与正常 132 谱同速：**Normal 模式零标定即忠实**。Floating/Classic 的 modeScale 用了 6，绝对刻度偏差留 Phase 4 标定。
- 设置面板 [BmsSettingsSubsection](../../osu.Game.Rulesets.Bms/BmsSettingsSubsection.cs) 新增「演出谱滚动（实验性）」下拉（Off/On/Auto），供显式开启与人工验证。
- 端到端测试：真实 converter 产出的 profile 喂进算法，断言转换链路在 STOP 区间真冻结。

**验证**：新增 `BmsScrollProfileTest`(11)、`BmsStopMotionScrollAlgorithmTest`(5)、`BmsScrollingInfoTest`(4) + 扩 `BmsBeatmapConverterTest`（profile 冻结/退化/snap/端到端 4 项）；`dotnet test osu.Game.Rulesets.Bms.Tests` **854/854**；`dotnet build osu.Desktop.slnf -p:Configuration=Release` **0 错误**、生产代码 0 新增警告。**正常链路无回归**：默认 OFF 时 `BmsScrollingInfo` 逐实例跟随基类（单测锁定）+ 全部 Player 系 gameplay TestScene（真实加载 DI 重缓存路径）全绿。

**待办（非本次）**：① DEAD SOUL 逐帧对照 beatoraja 的**人工视觉验收**（交接，归 Phase 4）；② Step D 自动检测（`Auto` 当前等同 `Off`）；③ Floating/Classic 绝对刻度标定；④ 负向/反向滚动（Phase 3）。

### 子线建线 + Phase 1：地雷视觉呈现落地

由 [../../other/BMS_GIMMICK_CHART_RENDERING.md](../../other/BMS_GIMMICK_CHART_RENDERING.md) 可行性分析升级为正式子线 `P1-L`，并落地 Phase 1（地雷渲染），目标是为后续忠实复刻 DEAD SOUL [Revive] 这类演出谱打地基。**本轮零滚动模型改动、零判定/计分改动、零正常游玩链路风险。**

**地雷渲染（视觉-only，仿小节线、完全隔离）**

此前地雷（channel D/E）解码进 `MineEvents` 但**从不渲染**（演出谱的主要"像素"直接缺失）。本轮把地雷渲染为可视、非判定对象：

- 新增 [../../osu.Game.Rulesets.Bms/Objects/BmsMine.cs](../../osu.Game.Rulesets.Bms/Objects/BmsMine.cs)：`HitObject` + `IgnoreJudgement` + 空 hit window，仅携 `LaneIndex`。
- 新增 [../../osu.Game.Rulesets.Bms/UI/DrawableBmsMine.cs](../../osu.Game.Rulesets.Bms/UI/DrawableBmsMine.cs)：仿 `DrawableBmsBarLine` 的 drawable（**非 `DrawableBmsHitObject`**），`HandleUserInput=false`、`DisplayResult=false`、`CheckForResult` 走 ignore-judgement；Phase 1 用非皮肤简单圆形（皮肤化后置）。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs)：把 `MineEvents` 的 `(measure,fraction)` 注册进 `eventTimes`；新增 `buildMines` 按 `channel-0xC0` 把 D1-D9/E1-E9 映射回可见通道 11-19/21-29 → `mapLaneIndex` → lane 范围校验 → 按时间排序，写入 `BmsBeatmap.Mines`。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs)：新增 `Mines` 列表，**刻意不进 `HitObjects`**。
- [../../osu.Game.Rulesets.Bms/UI/BmsPlayfield.cs](../../osu.Game.Rulesets.Bms/UI/BmsPlayfield.cs)：新增 `addMines`，仿 `addMeasureBarLines` 在构造期把 `DrawableBmsMine` 直接加到对应 lane。

**隔离保证（红线落地）**：地雷不进 `beatmap.HitObjects` → 不影响 `TotalObjectCount`/统计/计分/judged-note；`DrawableBmsMine` 非 `DrawableBmsHitObject` → 被 `OfType<DrawableBmsHitObject>`（empty-poor / 候选音符扫描 / 键音时间线）天然排除；`IgnoreJudgement` + `DisplayResult=false` → 不计分、不弹判定。完全复用小节线已验证安全的模式。

**验证**：新增 focused `TestBuildsMinePlacementsWithoutLeakingIntoJudgedObjects`（锁地雷 lane=1/time=2500 + `HitObjects` 无 `BmsMine` + `TotalObjectCount` 不含地雷）；`BmsBeatmapConverterTest` **16/16**；`dotnet test osu.Game.Rulesets.Bms.Tests` **831/831**；`dotnet build osu.Desktop.slnf -p:Configuration=Release` **0 错误**、生产代码 0 新增警告。

**已知限制**：Phase 1 仅让地雷可见；在现有前进式滚动下 DEAD SOUL 的"瞬移定格"仍被 squash，故该谱尚未忠实复刻（预期内，需 Phase 2 专用滚动旁路）。后续非阻塞项：地雷皮肤化、触雷伤害语义、极端谱地雷性能。
