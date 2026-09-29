# P1-L 当前状态：BMS Gimmick 与 BGA 视觉

> 最后核对：2026-09-30（BGA 多视图共享播放与合成；桌面、完整回归及逐谱人工门分开记录）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，机理分析见 [BMS_GIMMICK_CHART_RENDERING.md](../../other/BMS_GIMMICK_CHART_RENDERING.md)。

## 当前阶段

- 地雷视觉已落地。
- BMS 专用滚动位置积分旁路 A–C 已落地，Auto 检测存在；正常链路保持隔离。
- 已接入 P1-C 的 BMS 显示偏移：普通/STOP 映射、小节线/地雷淡出随显示时间，长条嵌套不重复偏移；原判定/音频时间保持，额外预加载不扩大空 POOR/辅助列消音。验证与人工未签收边界统一见 [P1-C](../P1-C/DEVELOPMENT_STATUS.md)。
- BGA 图序列/视频/POOR/转码缓存/预加载主链已落地。
- C3 的 immutable layout/viewports 与 C5 的 BGA material/scene、只读状态事件已接入真实宿主。
- 默认 BGA 显示已由一个 `BmsBgaPlayer` 负责时间线、素材加载与合成；常规 14K 四角共享一个 framebuffer 和三个只读 view，单 viewport 保持直接绘制。暂停、seek、POOR 与状态事件使用同一内容源。
- 选中皮肤已经可以通过 `BgaWidth/BgaHeight/BgaVerticalPosition/BgaInformationHeight` 声明 BGA 宽高、位置和信息区预留；静线采用大型 BGA 与上下演奏信息区，唯一 solver 在 safe bounds 内求解并避免遮挡轨道。尺寸不再只能由游戏固定；公开布局合同归 [P1-A](../P1-A/DEVELOPMENT_STATUS.md)。
- 未闭合：逐谱视觉、设备与尺寸验收、反向/负向滚动、极端谱性能与部分保真细节；本轮软件 gate 见验证记录。

## 当前有效合同

- Gimmick 渲染只允许 BMS 侧隔离旁路；不改 `TimingControlPoint` 钳制和共享 `ScrollingHitObjectContainer`。
- 判定/计分继续使用 `HitObject.StartTime`；滚动旁路只改变位置映射。
- `BmsScrollProfile` 对 BPM/STOP/measure-length/scroll 积分；STOP 段冻结，极端 BPM 产生 snap。
- `BmsGimmickScrollMode` 提供 Off/On/Auto；未命中检测的正常谱使用常规滚动算法；固定/自动显示偏移与 Gimmick 开关独立。
- BGA timeline 不进入 `HitObjects`；运行时 `BmsBgaPlayer` 合成 base/layer/layer2，资源直读 `chartbms/`。
- 老式视频只在 opt-in 外部 ffmpeg 可用时转码；缓存写入必须唯一 temp、去重且失败不留半成品。
- BGA 浮窗消费与 playfield/gauge 共用的 `BmsGameplayLayoutSnapshot.BgaViewports`；无声明时保留兼容默认，常规 14K 四角共享同一 player，极窄空间下由 solver 缩短轨道并改为下方单 viewport。选中包的尺寸/位置声明和上下信息区也由同一 snapshot 管理，不继承安装皮肤的大 BGA 强加给未声明的旧包。descriptor、material/scene 与事件继续按 viewport 发布，皮肤装饰不取得 player、timeline 或可写 clock；布局重建先摘除旧视图，再释放旧内容资源。converted-mania BGA 不在当前范围。

## 已知限制

- DEAD SOUL 等 Gimmick 谱尚未与 beatoraja 做逐帧对照。
- overlay 黑透与 `#ARGB` 仍是近似合成。
- 已消除按 viewport 重复创建 player/视频资源，四窗普通图/POOR 桌面像素验证通过；合成 framebuffer 的 GPU 成本与代表谱帧时仍需实测，不能由资源计数或合成图推导所有谱面保真达标。
- Floating/Classic 绝对刻度、负向/反向滚动未实现。

## 最近一次验证

- 2026-09-30：真实图片/视频各读取一次、四角共享一个 player、暂停/seek/POOR 同步与旧 player 释放，以及桌面四窗普通图/POOR 像素验证通过。本轮还修正转码失败状态早于临时文件清理的竞态。完整 gate、命令与证据统一见 [本轮性能验证](../../other/GAMEPLAY_PERFORMANCE_20260930.md)，未新增逐谱或设备人工签收。
- 2026-09-29 显示偏移专项与回归记录统一见 [P1-C](../P1-C/DEVELOPMENT_STATUS.md#最近一次验证)；未新增逐谱或 BGA 人工签收。
- 全局最新产品验证统一见 [mainline STATUS 的“最近一次验证”](../../mainline/DEVELOPMENT_STATUS.md#最近一次验证)，不把历史结果当作新增人工签收。
- 滚动、地雷、BGA/cache 的本线历史 focused/full 数字与逐刀实现只查 [CHANGELOG.md](CHANGELOG.md)，不冒充当前全局 gate。

## 下一检查点

1. 用代表设备与窗口尺寸检查共享内容比例及 lane 遮挡；自动像素证明不代签所有 DPI 和真实谱视觉。
2. 用代表谱人工验证图序列、POOR、seek、老式视频转码和 14K 布局。
3. DEAD SOUL 逐帧对照，记录 Auto 检测、freeze/snap 与正常谱回退。
4. 极端谱先 profile 再决定对象池/解码器优化；与 P1-J 协同。
5. 反向/负向滚动保持后置，不以破坏正常链路换取支持。

## 文档治理验证

2026-09-30 对照默认 BGA 显示与已通过的无窗口用例，移除当前态的逐 viewport player 和单 content 未完成断言；设置提示已与四角/窄屏下方布局对齐。显示偏移合同保持 P1-C 边界；桌面、完整回归与逐谱门以各自证据为准，历史审查见 [CHANGELOG](CHANGELOG.md)。
