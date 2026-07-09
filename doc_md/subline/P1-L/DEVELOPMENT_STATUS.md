# P1-L 当前状态：BMS Gimmick 与 BGA 视觉

> 最后更新：2026-07-10（同步 Skin V1 的 BGA ownership/layout 边界；功能状态未改变）
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
- 当前默认 BGA 浮窗按 playfield side 镜像；14K 为四角四 player。该项是现状而非 Skin V1 最终合同：P1-A 已要求改为 engine-owned 单一 content authority + descriptor viewport。converted-mania BGA 不在当前范围。

## 已知限制

- DEAD SOUL 等 Gimmick 谱尚未与 beatoraja 做逐帧对照。
- overlay 黑透与 `#ARGB` 仍是近似合成。
- 14K 四 player/潜在四视频解码器既未 profile，也与 Skin V1 单一 BGA content authority 冲突；设置提示仍写“14K→中缝”，与当前四角实现不一致。
- Floating/Classic 绝对刻度、负向/反向滚动未实现。

## 当前验证

- 2026-07-10 BMS 全量 **1005/1005**，覆盖滚动、地雷、BGA/cache 现有测试。
- 历史 focused 数字与逐刀实现只查 [CHANGELOG.md](CHANGELOG.md)。

## 下一检查点

1. 与 P1-A SV1-3 冻结 `BmsGameplayLayoutSnapshot`、单一 BGA content surface 和 viewport policy；在新合同落地前保留当前行为，不继续扩写四 player 路线。
2. 用代表谱人工验证图序列、POOR、seek、老式视频转码和 14K 布局。
3. DEAD SOUL 逐帧对照，记录 Auto 检测、freeze/snap 与正常谱回退。
4. 极端谱先 profile 再决定对象池/解码器优化；与 P1-J 协同。
5. 反向/负向滚动保持后置，不以破坏正常链路换取支持。
