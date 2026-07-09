# P1-L 当前状态：BMS Gimmick 与 BGA 视觉

> 最后更新：2026-07-10（文档降噪复核；功能状态未改变）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，机理分析见 [BMS_GIMMICK_CHART_RENDERING.md](../../other/BMS_GIMMICK_CHART_RENDERING.md)。

## 当前阶段

- 地雷视觉已落地。
- BMS 专用滚动位置积分旁路 A–C 已落地，Auto 检测存在；正常链路保持隔离。
- BGA 图序列/视频/POOR/转码缓存/预加载主链已落地。
- 未闭合：逐谱视觉、反向/负向滚动、极端谱性能与部分保真细节。

## 当前有效合同

- Gimmick 渲染只允许 BMS 侧隔离旁路；不改 `TimingControlPoint` 钳制和共享 `ScrollingHitObjectContainer`。
- 判定/计分继续使用 `HitObject.StartTime`；滚动旁路只改变位置映射。
- `BmsScrollProfile` 对 BPM/STOP/measure-length/scroll 积分；STOP 段冻结，极端 BPM 产生 snap。
- `BmsGimmickScrollMode` 提供 Off/On/Auto；未命中检测的正常谱走常规路径。
- BGA timeline 不进入 `HitObjects`；运行时 `BmsBgaPlayer` 合成 base/layer/layer2，资源直读 `chartbms/`。
- 老式视频只在 opt-in 外部 ffmpeg 可用时转码；缓存写入必须唯一 temp、去重且失败不留半成品。
- 默认 BGA 浮窗按 playfield side 镜像；14K 为四角布局。converted-mania BGA 不在当前范围。

## 已知限制

- DEAD SOUL 等 Gimmick 谱尚未与 beatoraja 做逐帧对照。
- overlay 黑透与 `#ARGB` 仍是近似合成。
- 14K 四视频解码器与 5645 地雷/6522 knots 等极端负载尚未做系统 profile。
- Floating/Classic 绝对刻度、负向/反向滚动未实现。

## 当前验证

- 2026-07-10 BMS 全量 **1005/1005**，覆盖滚动、地雷、BGA/cache 现有测试。
- 历史 focused 数字与逐刀实现只查 [CHANGELOG.md](CHANGELOG.md)。

## 下一检查点

1. 用代表谱人工验证图序列、POOR、seek、老式视频转码和 14K 布局。
2. DEAD SOUL 逐帧对照，记录 Auto 检测、freeze/snap 与正常谱回退。
3. 极端谱先 profile 再决定对象池/解码器优化；与 P1-J 协同。
4. 反向/负向滚动保持后置，不以破坏正常链路换取支持。
