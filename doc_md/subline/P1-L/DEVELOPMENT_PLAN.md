# P1-L 当前计划：BMS Gimmick 与 BGA 视觉

> 最后更新：2026-09-30（共享 BGA 内容已实现；继续代表设备与真实谱验收）
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
| 多视图共享内容 | 默认显示一个 player；多 viewport 共享合成 surface，单 viewport 直接绘制；真实素材与生命周期无窗口回归已过 |
| Skin V1 接线 | C3 共用 immutable layout/viewports、C5 material/scene 与只读 BGA 状态事件已落；选中包可声明 BGA 尺寸/位置及信息区预留 |
| 转码体验 | 预热等待上限、会话缓存、ultrafast 与扫描线进度已落 |

完成实现、事故诊断和旧测试数字不在 PLAN 重述，统一查 [CHANGELOG](CHANGELOG.md)。

## 当前执行顺序

### 1. 共享 BGA 内容的真实设备与尺寸验收

默认显示已收敛为一个 `BmsBgaPlayer`，常规 14K 用一个 `BufferedContainer` 和三个只读 view 复用合成结果，单 viewport 保持直接绘制。C3 snapshot、C5 material/scene 与状态事件继续使用现有接口；「显示 BGA」提示已修正为四角及窄屏下方布局。

真实图片/视频各读取一次、暂停/seek/POOR 同步、旧 player 释放及四窗普通图/POOR 桌面实绘已通过。完整回归与证据统一见 [性能验证记录](../../other/GAMEPLAY_PERFORMANCE_20260930.md)。剩余工作是代表设备与真实谱的尺寸/保真验收。

验收：桌面像素与 viewport 比例一致；代表设备覆盖宽高比、DPI 与 BGA 不遮 lane，不能用无窗口资源计数替代实绘结论。

### 2. 代表谱逐帧/逐功能人工验收

1. DEAD SOUL 等 Gimmick 谱与 beatoraja/LR2 对照 freeze、snap、Auto 检测和 Off 回退。
2. 代表图序列、POOR、seek、老式视频转码与重进缓存。
3. 验证当前 descriptor 驱动的默认 14K 四角布局、共享内容比例、C5 BGA frame/viewport scene 与状态事件及 seek/POOR 一致。
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

1. owning focused：scroll profile/algorithm、mine、BGA timeline/player/cache/transcode。
2. BMS full + `osu.Desktop.slnf` Release。
3. 代表谱人工视觉；任一阶段必须证明正常非 Gimmick gameplay 零回归。

## 明确不做

- 不向 skin 交付自建 player、raw timeline 或可写 clock。
- 不在 P1-L 修改 judgement/score/replay 时间结果。
- 不把 converted-mania BGA、任意外部视频工具链或硬件编码器扩张混入当前门。
