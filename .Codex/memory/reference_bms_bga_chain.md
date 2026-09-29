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
- `BmsBgaPanel` 消费同一 immutable layout/viewports 与 material/scene、只读状态事件；常规兼容布局为 5/7/9K 单角、14K 四角，极窄空间由 solver 缩短轨道并改为下方单 viewport。静线已通过选定包的 BGA 尺寸、位置与信息带参数提供较大窗口，不能把兼容小窗写成当前成品固定大小。converted-mania BGA 不在当前范围。
- 默认显示只创建一个 `BmsBgaPlayer`；多 viewport 使用一个 `BufferedContainer` 和其余只读 views 共享合成画面，单 viewport 直接绘制。暂停/seek/POOR 和各 viewport 的内容状态共享同一 player；皮肤 scene 仅装饰画面与状态，不拥有 timeline/player/clock。
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
9. `BufferedContainerView` 不自动继承内容 aspect；默认 `FillAspectRatio=1` 时加 `FillMode.Fit` 会把矩形镜像压成方形。当前等尺寸 view 应匹配 viewport，letterbox 留在原 player 内容内；验证要包含实际比例与桌面像素。
10. 重建必须先把旧 frames 全部脱树，再退役 scene 装饰并释放旧 frames/player/views；`Clear(disposeChildren: false)` 只脱树，不能代替释放旧视频资源。
11. 转码 runner 失败后，`failedDestinations` 若先于 finally 的 tmp 清理发布，Resolve 会返回 Unavailable 而目录仍暂有半成品。失败状态应在清理尝试后、移除 in-progress 前发布；不要给“无残留”测试加 sleep 规避竞态。本轮完整回归实际复现过该旧顺序窗口。

## 进度与并发

- 多源预热、当前 player 与快速重进产生的另一实例 join 同一 `Lazy<Task>`；共享 runtime player 不能代替跨实例转码去重。
- ffmpeg stderr 的 `Duration/time=` 转成平均进度，经 core `GameplayLoadProgress` 桥到 loading scanline；无进度用 indeterminate 动画。
- shared transcode task 解决转码去重，共享 player/surface 解决 runtime 镜像重复加载与解码，两者证据不可混写。极端 BGA/地雷及新增 framebuffer 的 GPU/帧时成本继续先 profile。

## 红线与遗留

- BGA 不回流判定/计分；正常滚动链必须可一键回退。
- overlay 黑透/ARGB 仍是近似；图序列、POOR、seek、老式视频和 14K 布局仍需逐谱人工验证。
- 2026-09-30 已通过真实图片/视频各读取一次、共享 player 的暂停/seek/POOR、重建释放旧 player，以及四窗普通图/POOR 桌面像素证明；完整 gate 与人工逐谱验收分开登记，不从资源计数反推保真或帧时结果。统一证据见 [本轮性能验证](../../doc_md/other/GAMEPLAY_PERFORMANCE_20260930.md)。
