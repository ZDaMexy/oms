---
name: reference_bms_bga_chain
description: BMS BGA 游戏会话、作者窗口、共享画布与转码缓存诊断地雷
metadata:
  node_type: memory
  type: reference
---

# BMS BGA 链路召回

权威当前态：[P1-L STATUS](../../doc_md/subline/P1-L/DEVELOPMENT_STATUS.md)；历史：[P1-L CHANGELOG](../../doc_md/subline/P1-L/CHANGELOG.md)。

## 当前链路

- decoder 产出 base/poor/layer/layer2 事件与 visual definitions；converter 写 `BmsBeatmap.BgaTimeline/PoorBgaMode`，不进入 `HitObjects`。
- `DrawableBmsRuleset`在Overlays持有独立`BmsBgaPlaybackSession`；有时间线时唯一`BmsBgaPlayer`按frame-stable gameplay time播图片/视频，无时间线时会话只提供静态背景，不创建player。pause/seek/retry随游戏时钟。
- BMS 资源经 `WorkingBeatmap.GetStream`/文件路径直读 `chartbms/`，不走 hash store。
- `BmsBgaPanel`消费同一immutable layout/viewports与material/scene、只读状态事件。无`BgaViewports`声明才使用5/7/9K单角、14K四角或极窄下方兼容布局；作者可按Keymode声明最多16个安全区域矩形及fit/fill/stretch，none显式关闭。`BgaInformationHeight>0`时独立保留四类信息区域，不因零窗消失。converted-mania BGA不在当前范围。
- 所有窗口含单窗都是共享合成surface的只读view；皮肤显示/布局重建只换views与装饰，不重建或释放会话player/视频，不丢失POOR。退出游玩根才释放会话；隐藏或零窗停止合成重绘但继续时间与状态。皮肤不能取得timeline/player/可写clock。独立兼容预览的临时会话不能混作正式游玩路径。
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
9. `BufferedContainerView`不自动继承内容aspect；默认1:1配Fit会压成方形。当前必须明确按时间线4:3合成画布（静态无时间线按自然比例）适配fit/fill/stretch，不能让各窗口重写谱面叠层坐标。验证实际比例与像素，不能只数player。
10. 重建先摘旧frames再退役scene/views；`Clear(disposeChildren: false)`只脱树，需释放旧显示对象，但不能释放游戏持有的player/视频。皮肤失效、4窗换1窗、none再恢复都须保留内容身份；退出游玩根才验证媒体释放。
11. 转码 runner 失败后，`failedDestinations` 若先于 finally 的 tmp 清理发布，Resolve 会返回 Unavailable 而目录仍暂有半成品。失败状态应在清理尝试后、移除 in-progress 前发布；不要给“无残留”测试加 sleep 规避竞态。2026-09-30 性能完整回归实际复现过该旧顺序窗口。
12. 作者窗口合法不等于当前屏幕能容纳：碰撞时整组0窗，不改数量/位置，不回默认，runtime.log搜索`bms.layout.bga-viewports-unavailable`；CLI只检查格式/范围/预算，设置没有专属错误面板。零窗的BGA装饰省略仍须完整验证资源/所有者，不允许坏声明借缺几何绕过验证，也不让mania接受BGA。
13. 离屏共享画布不能沿用默认裁剪与缓存子树行为：view只绘制已有buffer，source须在view之前绘制；`SynchronisedDrawQuad=false`让各窗独立定位，离屏source须保留绘制但隐藏时整树不画，cached framebuffer子节点仍更新时间。headless通过不证明离屏画布产生了像素。
14. BGA专用实例预算必须按当前每个窗口投影计数：Global源重复到各窗，显式index只属于一窗；不能按第一小窗的effect面积乘总数，也不能把全部显式窗口节点再乘总窗数。binding/状态赋值的source fanout同样按真实clone算。作者允许不同尺寸与最多16窗，不再固定四份池。

## 进度与并发

- 多源预热、当前 player 与快速重进产生的另一实例 join 同一 `Lazy<Task>`；共享 runtime player 不能代替跨实例转码去重。
- ffmpeg stderr 的 `Duration/time=` 转成平均进度，经 core `GameplayLoadProgress` 桥到 loading scanline；无进度用 indeterminate 动画。
- shared transcode task 解决转码去重，共享 player/surface 解决 runtime 镜像重复加载与解码，两者证据不可混写。极端 BGA/地雷及新增 framebuffer 的 GPU/帧时成本继续先 profile。

## 红线与遗留

- BGA 不回流判定/计分；正常滚动链必须可一键回退。
- overlay 黑透/ARGB 仍是近似；图序列、POOR、seek、老式视频和 14K 布局仍需逐谱人工验证。
- 2026-09-30较早性能切片的素材单次读取、暂停/seek/POOR和四窗像素证据见[性能记录](../../doc_md/other/GAMEPLAY_PERFORMANCE_20260930.md)；独立会话、作者窗口和手册例子的验证见[作者能力记录](../../doc_md/other/BGA_SKIN_AUTHORING_20260930.md)。旧显示重建释放player是历史行为，不能照搬为新断言。软件与逐谱人工门分开登记，不从资源计数反推保真或帧时收益。
