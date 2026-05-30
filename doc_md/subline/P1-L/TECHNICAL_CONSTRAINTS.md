# P1-L 技术约束：BMS 演出/Gimmick 谱视觉复刻

> 最后更新：2026-05-29
> 本文件记录 `P1-L` 的硬约束。若实现与本文冲突，先修正其一再继续开发。完整背景见 [../../other/BMS_GIMMICK_CHART_RENDERING.md](../../other/BMS_GIMMICK_CHART_RENDERING.md)。

## 红线（最高优先级，贯穿全线）

1. **不得改坏正常游玩链路**：mania 风格前进式滚动 + 判定/计分/gauge 必须保持现状、可独立运行、回归不变。
2. 演出渲染只能是**可检测/可开关的旁路**：建议按谱面特征自动判定 gimmick 模式或显式开关进入；非 gimmick 谱一律走现有路径。
3. **判定语义不变**：判定/计分/gauge 继续由现有按时间的链路负责；本线只接管**视觉定位**，不得改判定语义。
4. **不得为演出改写共享核心类型**（如 `TimingControlPoint` 的 `[6,60000]` beatLength 钳制）；BMS 专属滚动语义必须留在 BMS 侧旁路内。
5. 每阶段独立可落地、可回退；均需 focused 回归 + Release 门槛 + 正常链路无回归证明。

## 归线约束

1. 本线 authority 是**演出视觉渲染**（对象/小节线/特效定位）及其解析消费；不拥有判定/计分（P1-C/Scoring）、解析模型本体（P1-K，只消费）、运行时音频基线（P1-J，协同）。
2. 解析侧若需新增 typed 模型（如地雷伤害语义），归 P1-K 落地后由本线消费，不在本线另起第二套解析。

## Phase 1（地雷视觉）约束 —— 已落地，须长期保持

1. 地雷是**视觉-only、非判定、非计分**对象：必须用 `IgnoreJudgement` + 空 hit window，`DisplayResult=false`。
2. 地雷**不得进入 `beatmap.HitObjects`**：必须像小节线一样由 `BmsPlayfield` 直接加到对应 lane（`BmsBeatmap.Mines` 承载），从而不进入计分/统计/`TotalObjectCount`/judged-note 路径。
3. `DrawableBmsMine` **不得**继承 `DrawableBmsHitObject`：必须保持为 `DrawableHitObject<BmsMine>`，以便被 `OfType<DrawableBmsHitObject>`（empty-poor / 候选音符扫描）天然排除。
4. 地雷 channel → lane 映射固定为 `channel - 0xC0`（D1-D9→11-19、E1-E9→21-29），再经 `mapLaneIndex` + lane 范围校验；越界或非可玩通道的地雷丢弃，不得 mis-map 到错误 lane。**范围上界必须用轨道数 `BmsRuleset.GetLaneCount`（键 + scratch），不得用键数 `GetKeyCount`**——否则 scratch 占 lane 0 会使最右键轨道（如 7K lane 7）地雷被误丢（2026-05-29 修复）。
5. 地雷时间必须复用 converter 的 `eventTimes`（与音符同一时间轴），不得另算一套 timing。

## Phase 2（演出旁路）约束 —— Step A–C 已落地，须长期保持

1. 逐对象位置积分旁路**绕开**而非**改写** osu! 的 `ScrollingHitObjectContainer` 与 `TimingControlPoint` 钳制：实现为 BMS 专用 `BmsStopMotionScrollAlgorithm : IScrollAlgorithm`，经 `BmsPlayfield.CreateChildDependencies` 重缓存的 `BmsScrollingInfo` 注入；**零核心文件改动**。新增/改动绝不可回退为修改 `TimingControlPoint` 钳制或 `ScrollingHitObjectContainer`。
2. 旁路启用必须门控（`BmsGimmickScrollMode`，默认 `Auto`）；`Off`/未命中检测时 `BmsScrollingInfo.Algorithm` 必须**逐实例跟随基类算法**，与当前前进式滚动逐像素一致（`BmsScrollingInfoTest` 锁定，不得弱化）。默认 `Auto` 下「非 gimmick 谱零改动」依赖 `IsStopMotionGimmick` **无误报**（阈值须保守：`MaxSlope ≥ 50 || FrozenFraction ≥ 0.05`，正常/中等 soflan 远低于此）；放宽阈值前须重新评估正常链路回归，且 `Off` 必须始终是可用的硬回退。
3. 判定/计分继续走 `HitObject.StartTime` 时间链路；旁路只接管**视觉定位**，position 不得回流判定。`BmsScrollProfile` 不得进入 `beatmap.HitObjects`。
4. `BmsScrollProfile` 必须用**原始未钳制** BPM/STOP/measure-length/scroll 构建（复用 `buildEventTimeline` 游走），不得改用钳制后的 `ControlPointInfo`；STOP 段距离零增长（真冻结）、负向滚动留待 Phase 3（当前 `D` 单调非减，`TimeAtDistance` 取最早达成时间）。
5. **base 刻度 = 非冻结时长最常见 BPM**（`computeBaseBpm`，DEAD SOUL=132）。注意 `GetMostCommonBeatLength` 对演出谱会被 STOP-freeze/钳制点拉成 6；旁路在默认 Normal hi-speed 模式下因 `timeRange` 与之无关而忠实，**不得**为对齐而改用 6 做 base（那会复现 squash）。Floating/Classic 绝对刻度标定归 Phase 4。
6. 极端谱（DEAD SOUL：5645 地雷、6522 knots、~1300 control point、390 STOP 帧）必须有对象池/生命周期预算（与 P1-J 协同），不得无界实例化导致正常链路卡顿。

## 测试与发布约束

1. 每阶段至少补 focused 回归：Phase 1 已锁 converter 地雷构建 + "不泄漏判定路径"（`HitObjects` 无 `BmsMine`、`TotalObjectCount` 不含地雷）。
2. Release（`osu.Desktop.slnf`）构建 0 错误、生产代码 0 新增警告，是每阶段门槛。
3. 任一阶段改动若触及 P1-C 判定、P1-K 解析合同或 P1-J runtime hot-path，必须先停下拆分归线，再继续。
