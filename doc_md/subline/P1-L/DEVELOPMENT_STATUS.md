# P1-L 当前状态：BMS Gimmick 与 BGA 视觉

> 最后更新：2026-09-09（代码与跨线证据同步；产品验证未刷新）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，机理分析见 [BMS_GIMMICK_CHART_RENDERING.md](../../other/BMS_GIMMICK_CHART_RENDERING.md)。

## 当前阶段

- 地雷视觉已落地。
- BMS 专用滚动位置积分旁路 A–C 已落地，Auto 检测存在；正常链路保持隔离。
- BGA 图序列/视频/POOR/转码缓存/预加载主链已落地。
- C3 的 immutable layout/viewports 与 C5 的 BGA material/scene、只读状态事件已接入真实宿主。
- 未闭合：BGA 单一 content/decoder 会话迁移、逐谱视觉、反向/负向滚动、极端谱性能与部分保真细节。

## 当前有效合同

- Gimmick 渲染只允许 BMS 侧隔离旁路；不改 `TimingControlPoint` 钳制和共享 `ScrollingHitObjectContainer`。
- 判定/计分继续使用 `HitObject.StartTime`；滚动旁路只改变位置映射。
- `BmsScrollProfile` 对 BPM/STOP/measure-length/scroll 积分；STOP 段冻结，极端 BPM 产生 snap。
- `BmsGimmickScrollMode` 提供 Off/On/Auto；未命中检测的正常谱走常规路径。
- BGA timeline 不进入 `HitObjects`；运行时 `BmsBgaPlayer` 合成 base/layer/layer2，资源直读 `chartbms/`。
- 老式视频只在 opt-in 外部 ffmpeg 可用时转码；缓存写入必须唯一 temp、去重且失败不留半成品。
- BGA 浮窗已消费与 playfield/gauge 共用的 `BmsGameplayLayoutSnapshot.BgaViewports`；默认 14K 仍为四角四 player。descriptor、material/scene 与事件接线已经完成，单一 engine-owned content/decoder 会话尚未完成，两者不得混写。converted-mania BGA 不在当前范围。

## 已知限制

- DEAD SOUL 等 Gimmick 谱尚未与 beatoraja 做逐帧对照。
- overlay 黑透与 `#ARGB` 仍是近似合成。
- `DefaultBmsBgaPanelDisplay.createFrame()` 仍逐 viewport 创建 `BmsBgaPlayer`；14K 四 player/潜在四组视频解码资源没有共享 content 会话，也尚无代表场景 profile。现有 layout/timing-epoch 测试明确断言四 player，不能为了满足文档目标直接删除它们。
- BMS 设置「显示 BGA」提示仍写 `14K→中缝`，与默认四角 viewport 不一致；这是待修文案，不是当前布局行为。
- Floating/Classic 绝对刻度、负向/反向滚动未实现。

## 最近一次验证

- 全局最新产品验证统一见 [mainline STATUS 的“最近一次验证”](../../mainline/DEVELOPMENT_STATUS.md#最近一次验证)；2026-07-16 仅治理文档，未运行产品测试或 Release。
- 滚动、地雷、BGA/cache 的本线历史 focused/full 数字与逐刀实现只查 [CHANGELOG.md](CHANGELOG.md)，不冒充当前全局 gate。

## 下一检查点

1. 复用已闭合的 C3 layout/viewports 与 C5 publication/event 接口，完成 engine-owned 单一 BGA content 会话；变更当前四 player 测试时同时证明 seek/POOR/clock 与多视图一致性，不重建第二套布局 authority。
2. 用代表谱人工验证图序列、POOR、seek、老式视频转码和 14K 布局。
3. DEAD SOUL 逐帧对照，记录 Auto 检测、freeze/snap 与正常谱回退。
4. 极端谱先 profile 再决定对象池/解码器优化；与 P1-J 协同。
5. 反向/负向滚动保持后置，不以破坏正常链路换取支持。

## 文档治理验证

2026-09-09：对照 [BmsBgaPanel](../../../osu.Game.Rulesets.Bms/UI/BmsBgaPanel.cs)、[DrawableBmsRuleset](../../../osu.Game.Rulesets.Bms/UI/DrawableBmsRuleset.cs)及 [layout 测试源码](../../../osu.Game.Rulesets.Bms.Tests/TestSceneBmsBgaPanelLayout.cs)，区分已交付 descriptor/scene/event 和未交付单 content 会话。scroll/转码主链及人工门保持；本节仅记录源码审查；逐谱视觉验收仍开放，全局实测见主线最新验证。
