# P1-L 开发计划：BMS 演出/Gimmick 谱视觉复刻

> 最后更新：2026-05-29
> 全局计划见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。完整可行性与架构分析见 [../../other/BMS_GIMMICK_CHART_RENDERING.md](../../other/BMS_GIMMICK_CHART_RENDERING.md)（本子线由该分析升级而来）。
> **红线（最高优先级）：任何阶段都不得改坏 OMS 正常游玩链路（mania 风格前进式滚动判定）。演出渲染只能作为可隔离、可关闭的旁路。**

## 子线定位

- 目标：在 OMS 内尽可能忠实复刻 DEAD SOUL [Revive] 这类**炫技/观赏（演出）谱**的视觉效果（定格动画式的凭空出现/消失、悬停、反向/双向滚动、LN 跳回等）。
- authority：演出**视觉渲染**（对象/小节线/特效的屏上定位）与其所需的解析消费。判定/计分/gauge 继续归现有链路（P1-C/Scoring），本线不改判定语义。
- 不拥有：通用前进式滚动游玩链路（保持现状）；解析模型本体（归 P1-K，本线只消费）；运行时音频/性能基线（与 P1-J 协同）。

## 机理结论（详见可行性文档）

DEAD SOUL [Revive] 的所有效果是**定格动画（stop-motion）**：132 万 BPM 让滚动瞬间到位、measure-length 摆位置、STOP 定帧、大量地雷/音符作逐帧"像素"，全谱**无负值**。osu! 前进式滚动 + `TimingControlPoint.BeatLength` 钳制 `[6,60000]` + `RelativeScaleBeatLengths` 归一会把极端反差**压扁**，且 STOP 当前不真正视觉冻结、地雷此前根本不渲染——故现模型无法忠实复刻，需 beatoraja 风格的**逐对象位置积分旁路**。

## 分阶段计划

### Phase 0 — 解析完备性核对（前置，多数已在 P1-K 具备）
- 确认地雷（`MineEvents`）、measure-length、STOP、扩展/内联 BPM 已无损解码。结论：已具备（见 P1-K）。

### Phase 1 — 地雷视觉呈现（✅ 已落地，2026-05-29）
- 在**现有前进式滚动**下把地雷渲染为可视、非判定对象，零滚动模型改动、零正常链路风险。
- 落地文件：
  - [../../osu.Game.Rulesets.Bms/Objects/BmsMine.cs](../../osu.Game.Rulesets.Bms/Objects/BmsMine.cs)（`HitObject`，`IgnoreJudgement` + 空 hit window，仅携 `LaneIndex`）
  - [../../osu.Game.Rulesets.Bms/UI/DrawableBmsMine.cs](../../osu.Game.Rulesets.Bms/UI/DrawableBmsMine.cs)（仿小节线 drawable，非 `DrawableBmsHitObject`，Phase 1 用非皮肤简单圆形）
  - [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs)（注册地雷时间键 + `buildMines`：`channel-0xC0` 映射回 lane、按时间排序）
  - [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs)（`Mines` 列表，**不进 `HitObjects`**）
  - [../../osu.Game.Rulesets.Bms/UI/BmsPlayfield.cs](../../osu.Game.Rulesets.Bms/UI/BmsPlayfield.cs)（`addMines`，仿 `addMeasureBarLines` 直接加到对应 lane）
- 隔离保证：地雷不进 `beatmap.HitObjects`；`DrawableBmsMine` 非 `DrawableBmsHitObject`，被 `OfType<DrawableBmsHitObject>` 天然排除（empty-poor / 键音时间线不受影响）；`IgnoreJudgement` + `DisplayResult=false`（不计分/不显示判定）。
- 后续小项（非阻塞）：地雷皮肤化（接入 `BmsLaneSkinElements`）、触雷伤害语义（接 gauge，跨 P1-C/Scoring，需单独评估）、极端谱地雷数量的对象池/性能（与 P1-J 协同）。

### Phase 2 — BMS 专用滚动位置积分旁路（核心，Step A–D ✅ 已落地 2026-05-29 / 门控默认 Auto / 逐帧人工视觉验收待办）
- 目标：忠实复刻"瞬移摆位 + 真 STOP 冻结 + 任意定高"，让 DEAD SOUL 的定格动画成立。
- **落地状态**：Step A（`BmsScrollProfile` + converter 并行积分 `D(t)`）、Step B（`BmsStopMotionScrollAlgorithm` + `BmsScrollingInfo` 重缓存 + `GimmickScrollMode` 门控）、Step C（标定 + 设置面板下拉 + 端到端冻结测试）、Step D（`IsStopMotionGimmick` 自动检测）均已落地。**门控默认 `Auto`**（特效/变速谱开箱即用，正常谱不命中检测、零改动；`Off` 回退），Normal 模式零标定即忠实；详见 [CHANGELOG.md](CHANGELOG.md) / [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)。DEAD SOUL 逐帧人工视觉验收（Phase 4）待办。
- 设计草案（已按此落地）：
  1. **BMS 滚动模型**：把 BPM（不封顶、绝对值/带符号）、STOP（真冻结）、measure-length、`#SCROLL`/`#SPEED` 归一成一个分段 `scrollVelocity(t)`，预计算累计距离 `D(t)`（分段，二分 O(log n)）。
  2. **逐对象定位**：对象屏上位置 = `D(objectTime) - D(currentTime)`（可负=在判定线另一侧/反向）；STOP 段 `velocity=0` → 真冻结；瞬移段 velocity 极大 → snap。
  3. **专用视觉容器**：用 BMS 自有容器/定位逻辑承载对象与小节线定位，**绕开** osu! `ScrollingHitObjectContainer` 与 `TimingControlPoint` 的 `[6,60000]` 钳制（不动共享核心类型）。
  4. **门控**：仅在"演出渲染模式"（按谱面特征自动判定 gimmick，或显式开关）启用；非 gimmick 谱一律走现有前进式滚动路径，零改动。
  5. **判定不变**：判定/计分继续由现有按时间的链路负责；本旁路只接管视觉定位。
- 验收：DEAD SOUL [Revive] 逐帧对照 beatoraja。

### Phase 3 — 反向/双向与自定义 LN
- 负向滚动、双向/正反并存小节线、LN 头"跳回"（自定义 LN 视觉或短 LN 重画）；覆盖使用负 BPM/`#SCROLL` 的其它 gimmick 谱。

### Phase 4 — 人工验收
- DEAD SOUL 等真谱与 beatoraja/LR2 逐帧对照（P1-E/P1-G 风格）。

## 验证顺序（每阶段强制）
1. 先 focused 单测（如 Phase 1 的 converter 地雷构建 + 不泄漏判定路径）。
2. BMS 全套 + `osu.Desktop.slnf` Release 门槛。
3. **每阶段都必须证明正常游玩链路无回归**；任一阶段不达标不得推进下一阶段。
