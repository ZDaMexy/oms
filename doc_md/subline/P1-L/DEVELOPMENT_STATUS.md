# P1-L 当前状态：BMS Gimmick 与 BGA 视觉

> 最后核对：2026-10-25（接续10月24日BMS补验；未新增原生或逐谱验证）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，机理分析见 [BMS_GIMMICK_CHART_RENDERING.md](../../other/BMS_GIMMICK_CHART_RENDERING.md)。

## 当前阶段

- 进程内视频已从框架附带的FFmpeg 4.3.3四库替换为4.4.8加固定上游维护补丁，保留原主ABI、启用格式与Windows x64范围；不会依靠更新外部`ffmpeg.exe`或修改NuGet缓存完成修复。源码、签名、许可及构建配方随依赖保全，构建输出只带新win-x64四件套；身份、重链历史和复跑边界见[本轮记录](CHANGELOG.md#2026-10-22进程内ffmpeg原生依赖升级)与[依赖入口](../../../native/ffmpeg/README.md)。整仓其它风险仍归[P1-A](../P1-A/DEVELOPMENT_STATUS.md#当前风险与未完成项)。

- 地雷视觉已落地。
- BMS 专用滚动位置积分旁路 A–C 已落地，Auto 检测存在；正常链路保持隔离。
- 已接入 P1-C 的 BMS 显示偏移：普通/STOP 映射、小节线/地雷淡出随显示时间，长条嵌套不重复偏移；原判定/音频时间保持，额外预加载不扩大空 POOR/辅助列消音。验证与人工未签收边界统一见 [P1-C](../P1-C/DEVELOPMENT_STATUS.md)。
- BGA 图序列/视频/POOR/转码缓存/预加载主链已落地。
- 可达图片已随真实后台加载准备，同层同刻替代项和关闭的POOR不加载，缺失结果也保留为空；Update切图只选已准备纹理。使用原TextureStore寿命，不把CPU解码移出Update扩大成GPU上传、视频初始化或任意大图谱的内存成本已消失。
- BGA相对引用在转换边界校验，坏的可选引用不拒绝可玩谱面；`.m1v/.m2v`进入现有视频链。真实seek/reset清旧POOR与各层未来重试时刻，不再重播旧Miss或阻塞pending视频；普通暂停、共享会话与静态回退保持。
- C3 的 immutable layout/viewports 与 C5 的 BGA material/scene、只读状态事件已接入真实宿主。
- `DrawableBmsRuleset`持有独立`BmsBgaPlaybackSession`，有时间线时由唯一player负责加载、播放与合成；无时间线时会话提供静态背景。所有窗口，包括单窗，都使用只读view。皮肤显示/布局重建不更换player或视频，不重置POOR；退出游玩根才释放会话。关闭显示或零窗停止合成重绘，时钟与内容状态继续推进。
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

2026-10-24完整BMS补验已关闭10月22日内存中止的回归缺口，具名范围见[P1-A记录](../P1-A/CHANGELOG.md#2026-10-24omni持续背景与原版参照边界)。这不是最终原生配方的从零重编，也不新增逐谱、GPU帧时、设备/DPI或Windows发行包签收。

2026-10-22原生专项通过四库版本、框架所需导出、仅`pipe`输入协议及35项托管/原生布局核对；实际框架加载路径/摘要与ABI **2/2**、共享BGA **10/10**及同日布局 **4/4** 通过。Desktop Release、全新公钥导入/有效签名及篡改拒绝均通过，旧归档九项失败已闭合；证据见[同日记录](CHANGELOG.md#2026-10-22进程内ffmpeg原生依赖升级)。静态运行库重链接不证明最终配方干净重编或逐位复现，此门仍待验。

2026-10-19后续深审：晚出现图片与可选POOR在play前准备、重复/不可达资源及Update零读取有真实异步加载证明；四窗/替换/POOR/视频/seek/退出回归和同包实机取[集中记录](../../other/GAMEPLAY_PERFORMANCE_JUDGEMENT_20261019.md#验证证据)。加载与驻留提前的代价保留，不宣称所有视频或GPU帧时已优化。

2026-10-19游玩审查已覆盖坏路径/合法子目录、elementary MPEG、真实Seek/Reset旧POOR以及pending视频回退重试；修前失败、完整回归与优化后同包Xecus实绘取[集中记录](../../other/GAMEPLAY_REVIEW_20261019.md#验证证据)。不代签其它谱、设备、解码器或DPI。

2026-10-19真实Xecus首次view绘制崩溃已复现并修复；首帧与会话七项回归通过，同包默认Omni在独立Direct3D11宿主完整AP约131.55秒、2714最高连，原BGA保持播放且零scene runtime fault。原因、原始失败、探针纠正与完整回归入口见[本轮日志](CHANGELOG.md#2026-10-19真实xecus首帧崩溃修复)。未使用玩家存档或写入原HDD；不代签其它谱、设备与DPI。

2026-09-30后续会话/作者窗口：已验证显示更换、换窗、POOR、视频/暂停位置、隐藏/零窗与退出释放；桌面像素验证Fit/Fill/Stretch，5/16作者外框及投影预算通过。当前软件结果和完整BMS/mania回归的既有失败逐项对照见[作者能力记录](../../other/BGA_SKIN_AUTHORING_20260930.md)，不称全套全绿，未代签逐谱/设备门。

同日较早的[性能验证](../../other/GAMEPLAY_PERFORMANCE_20260930.md)保存四窗共享、单次图片/视频读取与转码失败清理竞态的证据。其“第一窗持有内容”及“重建释放旧player”只描述当时实现，现行生命周期由后续独立游戏会话取代，不能据此推导当前单窗FPS或全部谱面的GPU收益。滚动、地雷、cache和显示偏移的历史结果按日期查[CHANGELOG](CHANGELOG.md)。

## 下一检查点

1. 用代表设备与窗口尺寸检查三种适配及lane遮挡；已完成的会话/作者窗口与手册例子软件验证不代签所有DPI和真实谱视觉。
2. 用代表谱人工验证图序列、POOR、seek、老式视频转码和 14K 布局。
3. DEAD SOUL 逐帧对照，记录 Auto 检测、freeze/snap 与正常谱回退。
4. 极端谱先 profile 再决定对象池/解码器优化；与 P1-J 协同。
5. 反向/负向滚动保持后置，不以破坏正常链路换取支持。

## 文档治理验证

2026-10-25按原TRX接续已闭合BMS补验，删除计划中的重复补跑，原生从零重编、发行与人工门保持；仅文档复核，没有新产品测试。统一检查取[主线治理记录](../../mainline/CHANGELOG.md#2026-10-25专项进度与文档记忆复核)。外部转码器/进程内库的历史勘误见[10月22日记录](../../mainline/CHANGELOG.md#2026-10-22安全修复后的文档与记忆一致性复核)，早期会话/布局勘误见[原日志](CHANGELOG.md#2026-10-01)。
