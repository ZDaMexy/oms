# P1-L 当前计划：BMS Gimmick 与 BGA 视觉

> 最后更新：2026-09-30（游戏会话与作者窗口；后续代表设备与真实谱验收）
> 当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，稳定滚动/BGA 合同见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)，机理背景见 [BMS_GIMMICK_CHART_RENDERING.md](../../other/BMS_GIMMICK_CHART_RENDERING.md)，已完成阶段按日期查 [CHANGELOG.md](CHANGELOG.md)。

## 子线职责

P1-L 拥有 BMS Gimmick 的视觉位置旁路、地雷呈现与 BGA decode/timeline/seek/POOR/content authority；不修改判定/计分时间 truth，不拥有 P1-A 的最终 skin layout/viewport。

## 已完成基线

| 阶段 | 当前结果 |
| --- | --- |
| 解析前置 | mine/measure/STOP/BPM/scroll/BGA typed input 由 P1-K 提供 |
| 地雷视觉 | 已落，保持非判定、随可表示 lane permutation 移动 |
| 滚动旁路 | BMS-only position integration 与 Off/On/Auto 已落，正常链路隔离 |
| BGA 主链 | 图序列/视频/POOR、seek、ffmpeg opt-in 转码已落 |
| 多视图共享内容 | 游戏根持有独立播放会话，所有窗口使用共享合成surface的只读view；显示/换窗不重建player，退出根释放 |
| Skin V1 接线 | C3 layout/viewports、C5 material/scene与只读状态事件已落；作者可声明最多16窗及fit/fill/stretch/none，信息区独立；无声明保留旧布局 |
| 转码体验 | 预热等待上限、会话缓存、ultrafast 与扫描线进度已落 |

完成实现、事故诊断和旧测试数字不在 PLAN 重述，统一查 [CHANGELOG](CHANGELOG.md)。

## 当前执行顺序

### 1. 作者窗口与代表设备验收

游戏持有播放会话与作者窗口的实现及软件证据见[作者能力验证](../../other/BGA_SKIN_AUTHORING_20260930.md)。后续代表设备覆盖宽高比、DPI、三种适配、单窗/多窗与BGA不遮lane；关闭显示/零窗后恢复内容应跟随当前游戏时刻，独立信息区保持。

验收记录屏幕比例、样式、窗口声明与实际图像；不能用资源计数替代实绘，较早的[性能证据](../../other/GAMEPLAY_PERFORMANCE_20260930.md)保持其代码身份。
### 2. 代表谱逐帧/逐功能人工验收

1. DEAD SOUL 等 Gimmick 谱与 beatoraja/LR2 对照 freeze、snap、Auto 检测和 Off 回退。
2. 代表图序列、POOR、seek、老式视频转码与重进缓存。
3. 验证无声明时的14K兼容四角布局、作者窗口及none、独立信息区、共享内容比例、C5 BGA frame/viewport scene与状态事件及seek/POOR一致。实际冲突在runtime.log查`bms.layout.bga-viewports-unavailable`，没有专属设置错误面板。
4. 结果交 P1-G 汇总；自动链不能替代逐谱视觉结论。

验收：每个样本记录谱面、模式、预期/实际、截图或日志及 owning 子线，不以“能播放”替代保真判断。

### 3. 反向/负向滚动与自定义 LN 后置

1. 先保留 signed BPM/scroll source truth，不能为当前单调位置算法丢掉方向信息。
2. 新模型须同时解释负向、方向切换、小节线与 LN 头/身位置，不得只为单谱打补丁。
3. 继续使用 BMS 隔离旁路，不修改 shared `TimingControlPoint` 钳制或 `ScrollingHitObjectContainer`。

验收：代表负向/双向谱与正常谱均有位置、判定正交和 Off 回退证明；无完整模型时不启动。

### 4. 极端内容只按 profile 优化

1. 海量 `#BMP`、大视频、14K 多 viewport 或 dense Gimmick 必须先记录 decoder、texture、GC、update/draw 占比；共享 player 的资源计数与真实 GPU/帧时收益分开举证。
2. 优化不能破坏单 content authority、转码原子发布、等待上限或正常链路隔离。
3. 音频/keysound 卡顿归 P1-J，不在本线用 BGA 改动掩盖。

## 验证顺序

1. owning focused：scroll profile/algorithm、mine、BGA timeline/session/player/cache/transcode，配合P1-A验证作者布局、场景边界和完整例子。
2. BMS full + `osu.Desktop.slnf` Release。
3. 代表谱人工视觉；任一阶段必须证明正常非 Gimmick gameplay 零回归。

## 明确不做

- 不向 skin 交付自建 player、raw timeline 或可写 clock。
- 不在 P1-L 修改 judgement/score/replay 时间结果。
- 不把 converted-mania BGA、任意外部视频工具链或硬件编码器扩张混入当前门。
