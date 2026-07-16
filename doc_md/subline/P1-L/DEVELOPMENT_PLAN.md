# P1-L 当前计划：BMS Gimmick 与 BGA 视觉

> 最后更新：2026-07-16
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
| 转码体验 | 预热等待上限、会话缓存、ultrafast 与扫描线进度已落 |

完成实现、事故诊断和旧测试数字不在 PLAN 重述，统一查 [CHANGELOG](CHANGELOG.md)。

## 当前执行顺序

### 1. Skin V1 BGA 内容/视图解耦

依赖：P1-A `SV1-3` 冻结 `BmsGameplayLayoutSnapshot` 与 viewport policy；在新合同落地前保留当前 fallback，不继续扩张四 player 路线。

1. timeline、texture/video decode、playback clock、seek/retry 与 POOR 切换收敛为一个 engine-owned content session。
2. skin-facing API 只暴露只读 content handle/proxy、状态事件与 layout snapshot 的 named viewport；不交付 raw timeline、resource store 或可写 clock。
3. 多个 mirror viewport 共享同一 content/decoder authority，不因 14K 或 skin layout 重复创建 player。
4. 5K/7K P1/P2/CenterP1/CenterP2、9K BMS/PMS、14K DP 全部消费 P1-A 的最终 rect。

验收：focused tests 锁单 content source、seek/POOR 同步和多 viewport 不重复解码；人工覆盖宽高比、DPI 与 BGA 不遮 lane。

### 2. 代表谱逐帧/逐功能人工验收

1. DEAD SOUL 等 Gimmick 谱与 beatoraja/LR2 对照 freeze、snap、Auto 检测和 Off 回退。
2. 代表图序列、POOR、seek、老式视频转码与重进缓存。
3. 当前 14K 四角布局只验证恢复基线，同时记录与目标单 content/mirror viewport 的差异。
4. 结果交 P1-G 汇总；自动链不能替代逐谱视觉结论。

验收：每个样本记录谱面、模式、预期/实际、截图或日志及 owning 子线，不以“能播放”替代保真判断。

### 3. 反向/负向滚动与自定义 LN 后置

1. 先保留 signed BPM/scroll source truth，不能为当前单调位置算法丢掉方向信息。
2. 新模型须同时解释负向、方向切换、小节线与 LN 头/身位置，不得只为单谱打补丁。
3. 继续使用 BMS 隔离旁路，不修改 shared `TimingControlPoint` 钳制或 `ScrollingHitObjectContainer`。

验收：代表负向/双向谱与正常谱均有位置、判定正交和 Off 回退证明；无完整模型时不启动。

### 4. 极端内容只按 profile 优化

1. 海量 `#BMP`、大视频、14K 当前多 player 或 dense Gimmick 必须先记录 decoder、texture、GC、update/draw 占比。
2. 优化不能破坏单 content authority、转码原子发布、等待上限或正常链路隔离。
3. 音频/keysound 卡顿归 P1-J，不在本线用 BGA 改动掩盖。

## 验证顺序

1. owning focused：scroll profile/algorithm、mine、BGA timeline/player/cache/transcode。
2. BMS full + `osu.Desktop.slnf` Release。
3. 代表谱人工视觉；任一阶段必须证明正常非 Gimmick gameplay 零回归。

## 明确不做

- 不把 skin 自建 player、raw timeline 或当前 14K 四 player 冻结为 V1 API。
- 不在 P1-L 修改 judgement/score/replay 时间结果。
- 不把 converted-mania BGA、任意外部视频工具链或硬件编码器扩张混入当前门。
