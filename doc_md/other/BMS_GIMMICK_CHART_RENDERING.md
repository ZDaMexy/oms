# BMS 演出/Gimmick 谱渲染：可行性与架构规划

> 最后更新：2026-05-29
> 本文档是**可行性与架构分析**（背景/机理/方案的权威来源）。已升级为正式子线 **[P1-L](../subline/P1-L/)**：执行计划/状态/约束/变更以 P1-L 四件套为准，本文只保留分析与架构论证。**Phase 1（地雷视觉）已落地；Phase 2（方案 B — BMS 专用位置积分旁路）Step A–C 已落地**（门控默认 OFF、Normal 模式忠实、可显式开启；逐帧人工验收与自动检测待办，见 [P1-L CHANGELOG](../subline/P1-L/CHANGELOG.md)）。下方第 5 节方案 B 即本次落地路径，第 3-4 节的 squash 结论已被实测证实（`GetMostCommonBeatLength` 实测为 6）。
> **红线（贯穿全文）：任何实现都不得改坏 OMS 现有的正常游玩链路（mania 风格前进式滚动判定）。** 演出渲染只能作为**可隔离、可关闭**的旁路存在。

## 0. 文档定位

- 目的：评估"在 OMS 内尽可能完美复刻 DEAD SOUL [Revive] 这类炫技/观赏谱视觉演出"的可行性，给出架构选项与分阶段规划。
- 触发：对 `D:\...\soundsouler_deadsoul_Revive.bms` 的实谱解剖 + osu! 滚动引擎硬约束核查 + 社区资料检索。
- 边界：本文聚焦**视觉演出渲染**。解析侧（已能解码 BPM/STOP/measure-length/mine 等）现状见 [../subline/P1-K/](../subline/P1-K/)；运行时性能/音频见 [../subline/P1-J/](../subline/P1-J/)；判定语义见 [../subline/P1-C/](../subline/P1-C/)。

## 1. 案例解剖：DEAD SOUL [Revive]

实谱统计（7K，`#PLAYLEVEL 77`，基准 `#BPM 132`，约 933 小节）：

| 元素 | 量 | 含义 |
| --- | --- | --- |
| BGM（ch `01`） | 2721 | 键音叠层（音乐主体），大量**同位多层** |
| 可玩音符（11-19） | 394 | 真正可判定的音符很少 |
| **地雷（Dx/Ex）** | 1758 | **远超可玩音符——是演出的主要"像素"** |
| 小节长度（ch `02`） | 884 | 几乎逐小节改长度（摆放位置用） |
| STOP（ch `09`，`#STOPxx`） | 390 | 巨值（`#STOP01 360000`…），定"帧"用 |
| 扩展 BPM（ch `08`，`#BPMxx`） | 64 changes | `#BPMxx` 定义**全部 = 1320000**（132 万） |
| 内联 BPM（ch `03`，十六进制） | 若干 | 如 `42`=66、`84`=132 |
| 负值（`-N`） | **0** | 全谱无任何负 BPM/STOP/SCROLL |
| `#SCROLL`/SC、`#RANDOM`/`#SWITCH`、长条 | 0 / 0 / 极少 | 无 scroll 通道、无随机分支 |

**核心机理 = 定格动画（stop-motion）**，而非连续负向滚动：

1. **132 万 BPM 让滚动"瞬间到位"**（`getBeatLength≈0.045ms`，note 不连续移动而是直接 snap 到位置）。
2. **measure-length（02）摆放每个对象的"高度"**（在瞬移前提下，对象屏上位置≈累积的小节长度）。
3. **STOP 把每一"帧"定住**若干 ms（巨 STOP × 132 万 BPM = 受控 ms 停顿）。
4. **地雷 + 真音符作为逐帧"像素"**：1758 个地雷摆出图形/动画；真音符在"判定那一帧"出现成为可击对象。
5. 所有"反向/上升/悬停/凭空出现消失"都是**离散帧之间重新摆位的视觉错觉**，不是连续反向速度——所以全谱无需任何负值。

社区佐证：Dead Soul（BOFU2017，Sound Souler）标签含 **"Gimmick-Stop"**；osu!mania 移植版被描述为 "BPM 0→∞ (base 132)"——与"超高 BPM + STOP 定格"一致。参考：[BMS SEARCH 条目](https://bmssearch.net/)、[manbow BOFU2017](https://manbow.nothing.sh/)。

## 2. 用户观察的 10 个效果 → BMS 机理映射

| # | 观察到的效果 | BMS 机理（本谱，全 stop-motion 无负值） |
| --- | --- | --- |
| 1 | 变速 | 内联/扩展 BPM + measure-length |
| 2 | note 凭空出现，可切普通/地雷样式，可悬停，可判定，判定瞬间变普通 note | 逐帧不同对象：**地雷**作"预览/悬停"像素，真音符在判定帧出现；STOP 定住该帧 |
| 3 | LongNote 凭空出现 | 逐帧出现的（通常短）长条对象 |
| 4 | LN 头落到判定线后瞬闪回上方重落（"筷子点桌"） | 逐帧把（短）LN 在不同高度重画；STOP 定帧造成"跳回"错觉 |
| 5 | 小节线反向滚动 | 逐帧把小节线摆在递增高度（离散错觉，非连续反向） |
| 6 | note 反向滚动（上升） | 同上——逐帧把对象摆得越来越高 |
| 7 | 同时存在悬停/下落/反向 note | 同一帧内不同对象处于不同 measure-length 决定的高度 |
| 8 | note 可在悬停时被判定 | 判定按**时间**而非屏上位置；STOP 定帧期间对象的判定时刻到达即判 |
| 9 | note 凭空消失（或悬停时被判定） | 对象在某帧后时间已过/被判定 → 下一帧消失 |
| 10 | 同时存在正/反向小节线 | 同一帧内多条小节线被摆在不同高度 |

> 结论：**这张谱没有任何"连续反向滚动"**；它是离散定格帧序列。这对可行性是好消息（不强依赖负速度），但对 OMS 现有渲染仍是坏消息（见下）。

## 3. OMS 现状与硬约束（已核查代码）

- BMS 走 [`DrawableBmsRuleset : DrawableScrollingRuleset`](../../osu.Game.Rulesets.Bms/UI/DrawableBmsRuleset.cs)，仅设 `RelativeScaleBeatLengths => true`，用 osu! **默认前进式滚动算法**（`SequentialScrollAlgorithm`），无自定义/反向。
- 滚动速度公式（[`MultiplierControlPoint`](../../osu.Game/Rulesets/Timing/MultiplierControlPoint.cs)）：
  `Multiplier = Velocity × EffectPoint.ScrollSpeed × BaseBeatLength / TimingPoint.BeatLength`。
  → **BeatLength 越小滚动越快**；`BaseBeatLength` = `GetMostCommonBeatLength()`（按时长加权的最常见 beatLength）。
- **`TimingControlPoint.BeatLength` 被框架硬钳在 `[6, 60000]`**（=BPM `[1, 10000]`，见 [`TimingControlPoint.cs`](../../osu.Game/Beatmaps/ControlPoints/TimingControlPoint.cs)）。132 万 BPM → 钳成 1 万 BPM。
- 长条渲染为**单个 head→tail 拉伸对象**（无法在自身时长内"跳变/反复"）。
- **地雷当前不渲染**：[`BmsBeatmapConverter`](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs) 只消费 `ObjectEvents`/`LongNoteEvents`；`MineEvents` 解码后无 drawable、不进 playfield（见 [P1-K STATUS backlog](../subline/P1-K/DEVELOPMENT_STATUS.md)）。
- 小节线由 [`BmsPlayfield.addMeasureBarLines`](../../osu.Game.Rulesets.Bms/UI/BmsPlayfield.cs) 从 `MeasureStartTimes` 前向生成。

### 由此推出的"squash"问题（关键）

DEAD SOUL 的演出依赖 **132 万 BPM（瞬移）与基准 132 BPM 的极端反差**。但在 OMS 中：

1. 132 万 BPM 被钳成 1 万 BPM（BeatLength 6）；
2. 由于全谱大量 timing point 都钳到 BeatLength 6，且 STOP 段也写成 BeatLength 6，`GetMostCommonBeatLength()`（`BaseBeatLength`）很可能也接近 6；
3. `Multiplier ≈ BaseBeatLength / BeatLength ≈ 6/6 ≈ 1` → **极端反差被双重压扁成近似匀速**。

→ **"瞬移摆位 + 定格"的视觉对比在 OMS 现模型下基本丢失。** 此外 STOP 当前写成 `BeatLength=6`（最小），在上式下是**高倍速而非冻结**——时序（判定/对象时间）由 K3-A 的时间间隙保证是对的，但**视觉冻结不保真**（注意：本结论需用真谱实跑验证；现有测试只断言 BeatLength 值为 6，未覆盖视觉冻结）。

## 4. 可行性结论（逐效果）

| 效果 | osu! 现模型 | 说明 |
| --- | --- | --- |
| 1 变速 | 🟡 部分 | 中等幅度 soflan 可；极端 BPM 被钳 + relative-scale 压扁 |
| 2/3 凭空出现（含地雷样式） | 🔴 阻塞 | **地雷未渲染** + 帧定位失真；需新增可视对象与定位 |
| 4 LN 跳回 | 🔴 阻塞 | 单拉伸 LN 无法跳变；需自定义 LN 视觉或短 LN 重画 + 帧定位 |
| 5/6/7/10 反向/上升/双向 | 🔴 阻塞 | 即便是"离散帧错觉"，也要求把对象**按帧任意定高并在 STOP 内冻结**；现模型 squash + STOP 不冻结 → 做不到。真正连续反向（别的谱用负 BPM/`#SCROLL`）osu! 前进式更不可能 |
| 8/9 悬停判定/消失 | 🟡 部分 | 判定按时间，原则上可；但"悬停"的视觉冻结依赖 STOP 冻结保真，当前不达标 |

**总结论**：osu! 的前进式、单调时间轴、beatLength 钳制、单拉伸 LN 的滚动模型，**无法忠实复刻这类 stop-motion 演出谱**。要"尽可能完美复刻"，需要一条 **beatoraja 风格的专用 BMS 视觉渲染路径**：每个对象的屏上位置 = 对滚动函数的**逐对象积分**，支持不封顶 BPM（真瞬移）、STOP 真冻结、measure-length 任意定高、地雷可视、必要时负向滚动与自定义 LN。

## 5. 架构选项

- **方案 A：在 osu! 滚动框架内打补丁**（自定义 `IScrollAlgorithm` + 解钳 + STOP 改为高 beatLength 冻结 + 地雷 drawable）。
  - 优点：改动局部，复用现有 playfield/lane/判定。
  - 缺点：受单调前进/单拉伸 LN 根本限制；负向/双向/跳回仍做不到；解钳触碰**共享核心类型** `TimingControlPoint`（高风险，可能影响 mania）。**只能逼近，无法忠实，且风险溢出到正常链路。**
- **方案 B（推荐）：BMS 专用视觉渲染旁路**（beatoraja 风格逐对象位置积分），与 osu! `ScrollingHitObjectContainer` 解耦，仅在"演出渲染模式"下接管 BMS playfield 的对象定位与小节线；判定仍复用现有按时间的判定（保持正常游玩语义）。
  - 优点：可忠实复刻（瞬移/冻结/任意定高/反向/地雷/自定义 LN）；**完全旁路，正常前进式滚动与判定链路零改动**。
  - 缺点：工作量大；需自建 BMS 位置积分、地雷与可视对象模型、性能优化（本谱级别：~1300+ control point、数千对象、390 帧）。

## 6. 隔离原则（红线落地）

1. 正常游玩链路（mania 风格前进式滚动 + 判定）必须保持现状、可独立运行、回归不变。
2. 演出渲染只能是**可检测/可开关的旁路**：建议按"谱面特征自动判定 gimmick 模式"或显式开关进入；非 gimmick 谱一律走原路径。
3. 判定/计分/gauge 继续由现有按时间的链路负责；演出层只接管**视觉定位**，不得改判定语义。
4. 不得为演出去解钳/改写共享核心类型 `TimingControlPoint`；BMS 专属语义留在 BMS 侧的位置积分器内。
5. 每一阶段独立可落地、可回退，均需 focused 回归 + Release 门槛，且不得回归正常链路。

## 7. 分阶段规划（草案，待立项）

- **Phase 0 — 解析完备性核对**：确认地雷、measure-length、STOP、扩展/内联 BPM 已被无损解码进中间模型（多数已具备，见 P1-K）；补齐缺口（如地雷 typed 模型/可视语义）。**纯解码，无渲染改动。**
- **Phase 1 — 地雷与可视对象呈现**：把 `MineEvents` 落成可视（非判定）对象，进入现有 playfield；先在**正常前进式滚动**下显示地雷（最小、独立、可回退）。这一步即可显著提升观感，且不触碰滚动模型。
- **Phase 2 — BMS 专用位置积分旁路（方案 B 核心）**：实现逐对象位置积分器，支持不封顶 BPM（真瞬移）、STOP 真冻结、measure-length 任意定高；以"演出模式"接管 BMS 对象/小节线定位；判定仍走原链路。用 DEAD SOUL 做主验收。
- **Phase 3 — 反向/双向与自定义 LN**：支持负向滚动、双向小节线、LN 跳回（自定义 LN 视觉）；覆盖更广的 gimmick 谱（含使用负 BPM/`#SCROLL` 的谱）。
- **Phase 4 — 人工验收**：DEAD SOUL 等真谱逐帧对照 beatoraja/LR2（归 P1-E/P1-G 风格的人工验收）。

## 8. 风险与红线

- **解钳风险**：`TimingControlPoint` 是 osu.Game 共享类型，mania 也用；任何放宽必须 BMS 局部化，禁止全局改。方案 B 通过旁路自有积分器**绕开**该钳制，是规避此风险的关键理由。
- **性能**：本谱 ~1300+ control point、~4900 可视对象（含地雷）、390 帧 STOP；位置积分与对象池需性能预算（归 P1-J 协同）。
- **正常链路回归**：演出旁路若与正常路径耦合，风险极高；必须物理隔离 + 全程回归正常 play。
- **判定一致性**：演出视觉与按时间判定可能"看起来不对齐"（悬停判定等），需明确这是 BMS 既有语义、并与 P1-C 对齐文案/反馈。

## 9. 验证策略

- 真谱集：DEAD SOUL [Revive] + 若干使用负 BPM/`#SCROLL`/双向小节线的 gimmick 谱（覆盖 stop-motion 与连续反向两类）。
- 自动化：位置积分器的纯函数单测（给定 BPM/STOP/measure-length 序列 → 期望对象屏上位置/可见区间）；地雷可视对象的 focused 测试。
- 人工：逐帧截图与 beatoraja/LR2 对照（Phase 4）。
- 全程：BMS 全套 + Release 门槛；**每阶段都必须证明正常游玩链路无回归。**

## 10. 联动与升级

1. 本文是 `other/` 分析材料；方向获批后升级为正式 subline（暂定 `P1-L`，四件套），并回写 [mainline](../mainline/) 的全局优先级。
2. 与现有线的关系：Phase 0/1 的解码与地雷与 [P1-K](../subline/P1-K/) 相关（地雷已在其 backlog）；Phase 2/3 的运行时性能与 [P1-J](../subline/P1-J/) 协同；Phase 4 验收与 [P1-E](../subline/P1-E/)/[P1-G](../subline/P1-G/) 一致。
3. 与外部参考：机理对照 [BMS_FORMAT_REFERENCE.md](BMS_FORMAT_REFERENCE.md)（STOP/BPM/measure-length/mine 通道语义）。
