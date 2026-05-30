# P1-L 开发进度：BMS 演出/Gimmick 谱视觉复刻

> 最后更新：2026-05-29
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。完整分析见 [../../other/BMS_GIMMICK_CHART_RENDERING.md](../../other/BMS_GIMMICK_CHART_RENDERING.md)。

## 当前阶段

- **阶段定位**：本子线由 `other/BMS_GIMMICK_CHART_RENDERING.md` 可行性分析升级而来。**Phase 1（地雷视觉呈现）已落地**；**Phase 2（BMS 专用滚动位置积分旁路）Step A–C 已落地**（门控默认 OFF、Normal 模式忠实、可显式开启），其内的 Step D（自动检测）与 DEAD SOUL 逐帧人工视觉验收（Phase 4）未完成；Phase 3（负向/反向）未开工。
- **红线状态**：Phase 2 落地**绕开而非改写**共享核心——不动 `TimingControlPoint` 钳制、不动 `ScrollingHitObjectContainer`，注入全在 BMS 侧（`BmsPlayfield.CreateChildDependencies` 重缓存 `IScrollingInfo`）；判定/计分继续走 `HitObject.StartTime` 时间链路、语义不变；门控默认 OFF，对所有谱渲染零变化、可一键回退。

## 已确认事实

- 机理：DEAD SOUL [Revive] 是**定格动画**演出谱（132 万 BPM 瞬移 + measure-length 摆位 + STOP 定帧 + 大量地雷作像素，全谱无负值）。osu! 前进式滚动 + `BeatLength` 钳制 `[6,60000]` + `RelativeScaleBeatLengths` 会压扁极端反差，故现模型无法忠实复刻（详见可行性文档第 3-4 节）。
- [../../osu.Game.Rulesets.Bms/Objects/BmsMine.cs](../../osu.Game.Rulesets.Bms/Objects/BmsMine.cs)：地雷为 `HitObject` + `IgnoreJudgement` + 空 hit window，仅携 `LaneIndex`。
- [../../osu.Game.Rulesets.Bms/UI/DrawableBmsMine.cs](../../osu.Game.Rulesets.Bms/UI/DrawableBmsMine.cs)：仿小节线 drawable（非 `DrawableBmsHitObject`），Phase 1 用非皮肤简单圆形；`DisplayResult=false`、`HandleUserInput=false`、`CheckForResult` 走 ignore-judgement。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs)：注册地雷时间键到 `eventTimes`，`buildMines` 按 `channel-0xC0` 映射回 lane、范围校验、按时间排序，写入 `BmsBeatmap.Mines`。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs)：`Mines` 列表**不进 `HitObjects`**。
- [../../osu.Game.Rulesets.Bms/UI/BmsPlayfield.cs](../../osu.Game.Rulesets.Bms/UI/BmsPlayfield.cs)：`addMines` 仿 `addMeasureBarLines` 直接把 `DrawableBmsMine` 加到对应 lane。
- 隔离验证：地雷不进 `HitObjects`（`TotalObjectCount`/统计/计分不受影响）；`DrawableBmsMine` 非 `DrawableBmsHitObject`（empty-poor / 键音时间线不受影响）。

## Phase 2 已确认事实（Step A–C）

- **位置积分模型**：[BmsScrollProfile](../../osu.Game.Rulesets.Bms/Beatmaps/BmsScrollProfile.cs) 是分段线性 `D(t)`，由 `BmsBeatmapConverter.buildEventTimeline` 在既有时间游走里**并行积分**（原始未钳制 BPM/STOP/measure-length/scroll；STOP 段 `dD=0` 真冻结、132 万 BPM 段斜率暴涨 = snap），挂 [BmsBeatmap.ScrollProfile](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs)，不进 `HitObjects`。距离单位 = base-BPM ms（base = 非冻结时长最常见 BPM）。
- **注入机制（零核心改动）**：[BmsScrollingInfo](../../osu.Game.Rulesets.Bms/UI/Scrolling/BmsScrollingInfo.cs) 包裹基类 `IScrollingInfo`（Direction/TimeRange 透传，Algorithm 默认逐实例跟随基类），由 [BmsPlayfield.CreateChildDependencies](../../osu.Game.Rulesets.Bms/UI/BmsPlayfield.cs) 重缓存给 lanes；门控 ON 时 `EngageStopMotion([BmsStopMotionScrollAlgorithm](../../osu.Game.Rulesets.Bms/UI/Scrolling/BmsStopMotionScrollAlgorithm.cs))`。
- **门控**：[BmsGimmickScrollMode](../../osu.Game.Rulesets.Bms/Configuration/BmsGimmickScrollMode.cs)`{ Off(默认), On, Auto }`，设置面板「演出谱滚动（实验性）」下拉可切。`On` 恒启用；`Auto`（**默认**，Step D 已实现）仅对 `BmsScrollProfile.IsStopMotionGimmick`（`MaxSlope ≥ 50 || FrozenFraction ≥ 0.05`，保守区分特效/变速谱与正常/中等 soflan）命中的谱启用；`Off` 恒不启用、为回退开关。默认 Auto 下「非 gimmick 谱零改动」依赖检测无误报（已保守）+ `Off` 兜底。
- **标定结论（实测 DEAD SOUL [Revive]）**：STOP 冻结占 **43.1%**、snap 斜率 **10000×**、地雷 5645、knots 6522；`GetMostCommonBeatLength` 实测 **6（BPM 10000）**（被 STOP-freeze/钳制点拉低）——正常链路对 132 段 squash 实锤。**但默认 Normal hi-speed 模式 `timeRange` 与 `GetMostCommonBeatLength` 无关**（modeScale=1），profile base=454.5（132）使 `D≈t`，故 **Normal 模式零标定即忠实**；Floating/Classic 绝对刻度偏差归 Phase 4。

## 当前验证基线

- focused：`BmsScrollProfileTest` 11、`BmsStopMotionScrollAlgorithmTest` 5、`BmsScrollingInfoTest` 4，`BmsBeatmapConverterTest` 含 profile 冻结/退化/snap/端到端冻结共 23（原 16 + 新 7）。
- 更宽：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal` **854/854**；`dotnet build osu.Desktop.slnf -p:Configuration=Release` **0 错误**、生产代码 0 新增警告。
- **正常链路无回归证明**：默认 OFF 时 `BmsScrollingInfo.Algorithm` 逐实例跟随基类（`BmsScrollingInfoTest` 锁定）；全部 Player 系 gameplay TestScene（真实加载 DrawableBmsRuleset→BmsPlayfield→lanes，实跑 DI 重缓存路径）全绿。

## 已知限制 / 下一步

- **DEAD SOUL 逐帧人工视觉验收未做**（交接给人工 / Phase 4）：自动化已证明转换链路产出正确的 freeze/snap 且 Normal 模式 base 段忠实，且用户实跑反馈观感已对路；但「与 beatoraja 逐帧对照」仍需人工。验证方式：设置 → BMS →「演出谱滚动」选 On（或 Auto），进 DEAD SOUL 实跑。
- **默认已改 `Auto`**（用户拍板）：特效/变速谱开箱即用，正常谱不命中检测、走常规路径；`Off` 为回退开关。
- **Floating/Classic 模式绝对刻度标定**、**负向/反向滚动（Phase 3）** 未做。
- 非阻塞后续：地雷皮肤化、触雷伤害语义（跨 P1-C/Scoring）、极端谱（5645 地雷/6522 knots）地雷与对象池性能（P1-J 协同）。
