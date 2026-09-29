# P1-L 当前状态：BMS Gimmick 与 BGA 视觉

> 最后核对：2026-09-30（游戏持有 BGA 会话与作者窗口已实现；专项与桌面验证已登记，逐谱人工门保持）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，机理分析见 [BMS_GIMMICK_CHART_RENDERING.md](../../other/BMS_GIMMICK_CHART_RENDERING.md)。

## 当前阶段

- 地雷视觉已落地。
- BMS 专用滚动位置积分旁路 A–C 已落地，Auto 检测存在；正常链路保持隔离。
- 已接入 P1-C 的 BMS 显示偏移：普通/STOP 映射、小节线/地雷淡出随显示时间，长条嵌套不重复偏移；原判定/音频时间保持，额外预加载不扩大空 POOR/辅助列消音。验证与人工未签收边界统一见 [P1-C](../P1-C/DEVELOPMENT_STATUS.md)。
- BGA 图序列/视频/POOR/转码缓存/预加载主链已落地。
- C3 的 immutable layout/viewports 与 C5 的 BGA material/scene、只读状态事件已接入真实宿主。
- `DrawableBmsRuleset`持有独立`BmsBgaPlaybackSession`，一个player负责时间线、加载与合成；所有窗口，包括单窗，都使用只读view。皮肤显示/布局重建不更换player或视频，不重置POOR；退出游玩根才释放会话。关闭显示或零窗停止合成重绘，时钟与内容状态继续推进。
- 选中皮肤可以通过`BgaViewports`声明最多16个窗口的数量、位置、比例与fit/fill/stretch，none关闭窗口；无声明保留`BgaWidth/BgaHeight/BgaVerticalPosition`和14K旧默认布局。`BgaInformationHeight`独立保留信息区。唯一solver在safe bounds内校验；实际冲突整组0窗并写runtime.log，公开布局与完整作者手册归[P1-A](../P1-A/DEVELOPMENT_STATUS.md)。
- 未闭合：逐谱视觉、设备与尺寸验收、反向/负向滚动、极端谱性能与部分保真细节继续保留。

## 当前有效合同

- Gimmick 渲染只允许 BMS 侧隔离旁路；不改 `TimingControlPoint` 钳制和共享 `ScrollingHitObjectContainer`。
- 判定/计分继续使用 `HitObject.StartTime`；滚动旁路只改变位置映射。
- `BmsScrollProfile` 对 BPM/STOP/measure-length/scroll 积分；STOP 段冻结，极端 BPM 产生 snap。
- `BmsGimmickScrollMode` 提供 Off/On/Auto；未命中检测的正常谱使用常规滚动算法；固定/自动显示偏移与 Gimmick 开关独立。
- BGA timeline 不进入 `HitObjects`；运行时 `BmsBgaPlayer` 合成 base/layer/layer2，资源直读 `chartbms/`。
- 老式视频只在 opt-in 外部 ffmpeg 可用时转码；缓存写入必须唯一 temp、去重且失败不留半成品。
- BGA浮窗消费与playfield/gauge共用的`BmsGameplayLayoutSnapshot.BgaViewports`。无声明的兼容默认才使用常规14K四角或窄屏下方布局；作者声明按原顺序发布，冲突整组不显示，不搬位或替换成默认。信息区独立求解，不继承安装皮肤的大BGA强加给旧包。descriptor、material/scene与事件继续按viewport发布；显示重建先摘旧frames，再退役装饰与views，保留游戏持有的player/视频。皮肤不取得timeline、player或可写clock；converted-mania BGA不在当前范围。
- 谱面时间线先按固定4:3画布合成，逐窗fit保留完整画面、fill中心裁切、stretch拉伸；无时间线的静态背景按自然比例适配。单窗也不再直接持有player，合成buffer按最大投影视图决定尺寸。

## 已知限制

- DEAD SOUL 等 Gimmick 谱尚未与 beatoraja 做逐帧对照。
- overlay 黑透与 `#ARGB` 仍是近似合成。
- 已消除按viewport重复创建player/视频资源；此前四窗证据保留历史身份，本轮独立会话和三种比例窗口已完成专项及桌面像素验证。合成framebuffer的GPU成本与代表谱帧时仍须实测，不能由资源计数或合成图推导所有谱面保真达标。
- Floating/Classic 绝对刻度、负向/反向滚动未实现。

## 最近一次验证

- 2026-09-30后续会话/作者窗口：已验证显示更换、换窗、POOR、视频/暂停位置、隐藏/零窗与退出释放；桌面像素验证Fit/Fill/Stretch，5/16作者外框及投影预算通过。当前软件结果和既有失败逐项对照见[作者能力记录](../../other/BGA_SKIN_AUTHORING_20260930.md)，较早性能结果保持独立身份，未代签逐谱/设备门。
- 2026-09-30：真实图片/视频各读取一次、四角共享一个 player、暂停/seek/POOR 同步与旧 player 释放，以及桌面四窗普通图/POOR 像素验证通过。本轮还修正转码失败状态早于临时文件清理的竞态。完整 gate、命令与证据统一见 [本轮性能验证](../../other/GAMEPLAY_PERFORMANCE_20260930.md)，未新增逐谱或设备人工签收。
- 2026-09-29 显示偏移专项与回归记录统一见 [P1-C](../P1-C/DEVELOPMENT_STATUS.md#最近一次验证)；未新增逐谱或 BGA 人工签收。
- 全局最新产品验证统一见 [mainline STATUS 的“最近一次验证”](../../mainline/DEVELOPMENT_STATUS.md#最近一次验证)，不把历史结果当作新增人工签收。
- 滚动、地雷、BGA/cache 的本线历史 focused/full 数字与逐刀实现只查 [CHANGELOG.md](CHANGELOG.md)，不冒充当前全局 gate。

## 下一检查点

1. 用代表设备与窗口尺寸检查三种适配及lane遮挡；已完成的会话/作者窗口与手册例子软件验证不代签所有DPI和真实谱视觉。
2. 用代表谱人工验证图序列、POOR、seek、老式视频转码和 14K 布局。
3. DEAD SOUL 逐帧对照，记录 Auto 检测、freeze/snap 与正常谱回退。
4. 极端谱先 profile 再决定对象池/解码器优化；与 P1-J 协同。
5. 反向/负向滚动保持后置，不以破坏正常链路换取支持。

## 文档治理验证

2026-09-30对照游戏会话、默认显示与作者窗口源码，当前态不再保留“单窗直出/三个镜像”及布局重建释放player的旧描述。显示偏移合同保持P1-C边界；专项与桌面结果见作者能力记录，完整回归与逐谱门以各自证据为准，历史实现见[CHANGELOG](CHANGELOG.md)。
