---
name: reference_bms_bga_chain
description: BMS BGA 主链、转码缓存安全合同与诊断地雷
metadata:
  node_type: memory
  type: reference
---

# BMS BGA 链路召回

权威当前态：[P1-L STATUS](../../doc_md/subline/P1-L/DEVELOPMENT_STATUS.md)；历史：[P1-L CHANGELOG](../../doc_md/subline/P1-L/CHANGELOG.md)。

## 当前链路

- decoder 产出 base/poor/layer/layer2 事件与 visual definitions；converter 写 `BmsBeatmap.BgaTimeline/PoorBgaMode`，不进入 `HitObjects`。
- `BmsBgaPlayer` 按 frame-stable gameplay time 播图片/视频；挂 `DrawableRuleset.Overlays`，pause/seek/retry 随游戏时钟。
- BMS 资源经 `WorkingBeatmap.GetStream`/文件路径直读 `chartbms/`，不走 hash store。
- `BmsBgaPanel` 消费 C3 的同一 immutable layout/viewports 与 C5 material/scene、只读状态事件；默认 5/7/9K 单角、14K 四角。converted-mania 不在当前范围。
- 老式视频经 opt-in 外部 ffmpeg 转 mp4；无 ffmpeg/失败/超时均回退静态，不阻断游玩。

## 必须记住的地雷

1. 新 BGA event 必须注册进 converter `eventTimes`，否则 measure/fraction 无绝对时间，timeline 全 miss。
2. 预加载器要直接挂在 player 等待的 Overlays 子树；放入异步 `SkinnableDrawable` 不能阻塞 player push。
3. ffmpeg 写 temp 必须 **GUID 唯一 + static 跨实例 in-progress 去重 + 原子 move**。固定 temp 并发写会生成损坏 mp4，而 `File.Exists` 会永久端出坏缓存。
4. 转码参数变更必须 bump cache version；旧缓存存在不代表可解码。
5. `.tmp` 输出要显式 `-f mp4`；libx264 使用兼容参数与 `-preset ultrafast`。转码失败先保留 stderr，再猜编码器。
6. 判断缓存坏文件：先用产出它的同一 ffmpeg 解码；不要先归咎框架硬解。
7. `Video.FramesProcessed` 可做零帧 watchdog；宽限后 dispose，静态优先，避免黑屏和日志刷屏。
8. 会话缓存启动期只清一次；清理必须发生在任何转码任务前。

## 进度与并发

- 多源预热 join 同一 `Lazy<Task>`，14K 四播放器不能重复转同一文件。
- ffmpeg stderr 的 `Duration/time=` 转成平均进度，经 core `GameplayLoadProgress` 桥到 loading scanline；无进度用 indeterminate 动画。
- `DefaultBmsBgaPanelDisplay.createFrame()` 仍按 viewport 新建 `BmsBgaPlayer`；同一视频可能被默认 14K 四 player 分别解码。shared transcode task 去重不代表 runtime 已有单 content/decoder 会话，迁移仍在 P1-L PLAN；极端 BGA/地雷性能先 profile。

## 红线与遗留

- BGA 不回流判定/计分；正常滚动链必须可一键回退。
- overlay 黑透/ARGB 仍是近似；图序列、POOR、seek、老式视频和 14K 布局仍需逐谱人工验证。
- C3 descriptor/C5 事件已接通，不重开布局前置；单 content 会话未完成，不能从只读事件或测试通过反推已经共享解码器。现有四 player 测试表达当前行为，迁移时须配套新的多视图 seek/POOR proof。
