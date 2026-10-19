# 游玩性能与判定深审（2026-10-19）

## 范围与基线

本轮从干净的 `master@1d96e1b7` 开始，已成功 fetch；该提交包含同日较早的[游玩链路审查](GAMEPLAY_REVIEW_20261019.md)，其历史验证不冒充本轮结果。本轮主归属 [P1-J](../subline/P1-J/DEVELOPMENT_STATUS.md)，输入归 P1-B，判定/结果归 P1-C，长按归 P1-E，BGA 准备归 P1-L。

检查范围是实际输入交付、音乐时钟与 catch-up、普通音符/长按、计分和血量撤销、自动演奏结算、回放导出/读取，以及稳定游玩时的控制器、回放和血条分配与 BGA 图片读取。未更换音乐时钟、判定家族数表、TOTAL v7 或成绩分桶合同。

原 HDD 未读取、启动或写入；玩家正式数据未用于探针。实机使用既有隔离谱面副本与私下独立的 Omni 成品，该包不内置到 OMS，也未重打包。未生成发行包、publish 或安装副本；独立测试宿主及其输出位于开发盘的 disposable 目录。

## 已复现的判定与结果问题

| 触发 | 修前实际影响 | 本轮处理及行为证据 |
| --- | --- | --- |
| 原生键盘在 Update 卡顿期间收到两次完整 tap | 原始事件丢失接收时间，catch-up 的第一个子步消耗整批，判定与录制时间一起偏移 | 原生线程只记录单调时间；既有 offset/rate 后时钟发布配对样本，Update 冻结批次并让原有稳定时钟停在事件时刻。真实 loaded 场景的 1040/1080 ms 两次 tap、1/1.5 倍速均保留独立 Perfect、RawTime、零时差和录制时刻 |
| 暂停、seek/reset、offset/source 变化、回放接管 | 旧待发输入可能进入新时间线或新输入所有者 | 沿现有时钟/原生 reset epoch 失效；未来事件等下一样本，迟到事件不倒改已提交的判定，同时间保留 receipt 顺序。补同一外层帧之间 replay attach/detach 的失效证明；原生回调不读取 Drawable/音乐时钟 |
| mania 长按在同一物理输入批次 down→up，中间没有 DHO.Update | 松键读取滞后的显示 bindable，真实 hold 仍为 true、缺失 ComboBreak；再接住后尾部可错误获 Perfect | OnReleased 读取已有 Result 的真实 hold 状态。真实 Player 的同批/跨帧对照同时检查断条、即时连击、重抓、不重抓、尾判和最终统计；不改窗口或断条后的尾判上限 |
| mania 普通音符与长按相邻 | Ignore 长按父对象被排除出 note lock，旧对象抢走后一个对象的输入 | 判定是否参与锁定时考虑计分后代；force-miss 仍只处理自身计分对象，保留 Ignore 父对象完成 body/tail 的生命周期。真实 ReplayPlayer 严格核对 Miss、Perfect 和 10 ms 时差 |
| 多轨道结果回退，包括同刻和弦 | 每列单独撤销自己的栈，顺序不等于全局应用顺序的逆序，HealthProcessor 恢复了错误快照 | 根 Playfield 复用原 entry 栈记录全局应用顺序，嵌套结果只转发；不新造第二套 health/history。BMS/mania 真实手动回放检查血量、分数、EX、统计和连击恢复 |
| BMS 全自动 LN/CN/HCN 进入结算 | drawable 为自动输入设置 AutoPlay，重建血量时误当辅助排除；实况 100%，结果重建约 20% 并错误 Failed | 回血分母取真实 CountsForScore。ASCR/ANOT 继续排除，正常 AT 保留计分；实际 ReplayPlayer 证明实况、重建及 ScoreInfo 的最终 gauge/Perfect 灯一致，TOTAL 身份保持 |
| 失败成绩导出再读取 `.osr` | 可选 replay 元数据缺少 Passed，旧 F 可被恢复成默认 true | 增加可选 `passed`；旧包仅依据已存 F 恢复失败，无 rank 证据保留旧默认。已存 F 重新导出也不把 Realm 中 ignored bool 的默认值写成通过；NF 或统计不用于猜测是否失败 |

`ScoreInfo.Passed` 本身是 Realm ignored 字段，本地成绩卡主要读取已存 Rank。本轮最后一项修复涉及回放元数据及其实际读取者，不能描述为“本地所有失败成绩曾显示通过”；没有改正常新局的 OMSIR 提交合同或 Realm schema。

## 真实输入到完整成绩的额外证明

现有mania稳定性测试已通过真实ReplayPlayer与编码往返覆盖六类判定，本轮补齐混合谱的完整成绩核对；BMS另补四种judge family的混合手动回放。用例由固定谱面和固定输入组成，不直接调用DHO.OnPressed、不注入ApplyResult，也不从被测窗口/计分函数反算预期。

- mania固定OD5，六音符分别在0/-30/-60/-100/-130/-150ms按下，实际Perfect/Great/Good/Ok/Meh/Miss各一次。最终accuracy为`955/1830`，最高combo5、最终0，标准分176180；运行中与完成后的ScoreInfo统计、准确率和分数一致。该原生mania合同使用Perfect305、Great300等现行权重，不套用其它播放器的320权重。
- BMS固定五音符，OD/Beatoraja/LR2/IIDX各跑一次，0/+30/-60/+125ms按下及第五不按，真实PG/GR/GD/BAD/Miss各一次；在第四音符前400ms空按产生独立Empty Poor，此刻combo3/EX3/accuracy0.5保持。结束EX3/MAX10、accuracy0.3、最高combo3/最终0、Empty Poor1、ComboBreak1和五个完成点均与完成ScoreInfo一致。

这是对当前实现合同的行为证明，不能把IIDX启发式提升为闭源精确parity。两份新增fixture在全量结束后单独加入，未改全量已验证的45份生产/既有测试源码；专项成功后不重复无新风险的全套回归，两个范围分开登记。

## 已测出的热路径开销

下表均由真实生产组件执行，预热后使用同线程 allocation counter；包含实际输入 Apply、按键边沿、血条和原生 Vortice COM wrapper，原生 fixture 用 vtable stub 验证 Poll/GetDeviceState/Release。零值只覆盖表中稳定区间，不能推导整局、所有输入、绘制线程或驱动都零分配。

| 稳定场景 | 修前分配 | 修后分配 | 同时保留的行为 |
| --- | ---: | ---: | --- |
| DirectInput 空闲 6000 次轮询 | 3,312,000 B | 0 B | 6000 次 Poll 和 GetDeviceState |
| 已连接设备通用空闲轮询 6000 次 | 240,000 B | 0 B | 每帧仍轮询设备 |
| 轴正反切换，65,536 个真实动作边沿 | 6,946,816 B | 0 B | 反向先松后按、完整边沿 |
| BMS replay/AT 与 mania replay，空/持有各 65,536 次 | 各 4,718,592 B | 各 0 B | 实际 Apply、reset 后重放按下、换帧时正确松开 |
| 未变化的 SP/DP 血条各 65,536 次更新 | 2,097,152 / 4,194,304 B | 各 0 B | 数值改变和文化切换仍使用原 `P0` 格式，双盘共用同一字符串 |

- DirectInput 改为复用固定 JoystickState 和 change buffer，避免包装器每次新建 state/数组；HasPendingChanges 不再额外取得第二份原生快照。断开当帧释放动作，设备资源独立、幂等释放，修复 disconnected flag 导致 Dispose 跳过的泄漏。
- 缺席设备修前可在 6000 个 gameplay 更新里枚举 6000 次；修后该短窗口只发生加载期的首次发现。运行中采用唯一后台任务和 500 ms 缺席退避，Update 只提交已完成结果，既有设备仍每帧轮询；重复候选和退休设备在同一后台任务释放。
- 真正闭锁的 provider 证明发现未结束时两次 gameplay 更新仍返回、已连接控制器仍触发动作；另测任务中的新通知、退出 join 和未提交候选恰好释放一次。没有把“更少枚举”冒充“枚举已经不阻塞”。同 ID 设备在发现期间断开，重新连接可能等待下一次最多 500 ms 的发现。
- replay 只缓存当前 frame 的不可变输入状态；换帧创建新状态，reset 清 LastReplayState 后仍重新应用。没有删录制心跳、真实输入边沿、统计帧或 spectator 记录。
- 短 microbenchmark 的 CPU 耗时受 JIT/调度影响，部分原生短样本并未变快；本轮明确证明的是去分配、去同步发现阻塞和行为保留，不宣布所有路径 CPU 普遍加速。

## BGA 图片准备

修前晚出现的图片第一次切换时在 Update 执行 TextureStore.Get，同步文件读取及解码；固定版本的[框架加载器](https://raw.githubusercontent.com/ppy/osu-framework/ba04d538f02cecaabe5ab7dc3b102cd47d59d785/osu.Framework/Graphics/Textures/TextureLoaderStore.cs)和真实线程计数共同确认该路径。修后利用既有 Drawable 后台加载，按层准备实际可达图片，重名资源只准备一次；同层同刻被后一个 source 替换的图片不加载，关闭的 POOR 不加载，缺失/不可解码结果也保留为空，切换时不重试磁盘。

真实异步加载 fixture 的晚图在开始前已有一次读取，Default/Undisplayed 两组的 gameplay Update 读取均为零；四窗、显示更换、POOR、视频位置、seek、隐藏/零窗及退出释放继续由既有会话测试验收。TextureStore 仍是资源 owner，未建立第二套图片缓存寿命。代价是把将来会显示的图片解码时间及驻留提前到歌曲加载阶段；不能将此推导为任意大图谱的内存无上限，也不能把 CPU 解码移出 Update 等同于 GPU upload/视频初始化成本全部消失。

## 实机采样方法

首组实机固定同一隔离 Xecus、同一最终 Omni、同一 60 秒自动演奏宿主、1000 Hz Update / 240 Hz Draw 请求。预分配采样数组，先写内存，退出后导出 CSV；采样实际 Update/Draw wall interval、Update thread allocation 和前台状态，不在每帧写日志。源文件修前/修后摘要相同，生产 DLL 清单分别记录。

修前第三次有效运行只有约 36 秒后进入前台，因此比较规则在修后运行前已固定：38–58 秒、active=true，并排除原 fixture 每 5 秒 JSON/截图检查点的 -250 至 +2000 ms。完整 CSV、后台样本与剔除规则全部保留；不把后台 60 Hz 限制、截图落盘或探针退出 hash 的停顿算成产品性能改善。

采样包括 limiter/调度等待，不是 CPU 纯工作时间、GPU 时间或显示器 scanout；该机器的 Draw 实际节奏须依据 CSV，不能用请求值宣称实绘 240 FPS。

| 固定前台比较区间 | 修前 `native-before-3` | 修后 `native-after-1` |
| --- | ---: | ---: |
| Update 样本数 | 10,998 | 10,299 |
| Update p50 / p99 / 最大间隔 | 0.9759 / 1.7068 / 10.4014 ms | 0.9539 / 2.1776 / 18.1221 ms |
| Update 每观测秒分配 | 1,128,835 B | 1,019,511 B |
| Draw 样本数 | 1,320 | 1,319 |
| Draw 平均 / p99 / 最大间隔 | 8.3334 / 9.1472 / 11.7925 ms | 8.3400 / 9.3631 / 22.2275 ms |

该区间分配率约减少9.7%，但长尾未改善，修后仍有长帧；这组证据**不支持FPS或流畅度提升结论**。采样中实际Draw约120Hz。整段GC计数也因修前前台历史不同而不直接作前后比较。原始CSV、离线分析脚本、选择规则与`native-comparison.json`保留，不能因结果不理想而更换区间。

进一步核对发现，普通ExactVisualTestGame/TestOsuGame缺少实际OsuGameDesktop缓存的`IHighPerformanceSessionManager`。因此上表是同一测试宿主的对照，不能直接代表正常桌面客户端的GC条件。随后增加仅属于探针的DesktopPerformanceScene，缓存原生产管理器，让真实PlayerLoader自行进入会话；没有改客户端GC策略或由探针强制切换LatencyMode。

补测`native-desktop-full-1`的`desktop-session.json`确认真实管理器已激活、`LowLatency`、`ForcedByProbe=false`。Xecus在约131.58秒完整结束，EX5428、最高combo2714，运行异常0，保留原始BGA单实例和全部2878条timeline。末段截图确认完整连击演出与100%血条；配置交互不属于这次自动演奏的验收范围。

但是补测3–58秒的3292个Update/Draw样本全部`active=false`，仍受后台约60Hz限制，故没有有效前台帧时间比较。该段记录了704.689ms累计GC暂停、297/109/2次各代收集，并有142ms和83ms间隔与gen2收集相邻；后台、测试开销和GC因果尚未拆开，不能据此宣称正常桌面同样卡顿，也不能据此签收零卡顿。它只补足生产会话下整曲运行与结果证明；**正常前台设备上的帧时间/GC验收仍未完成**，不以更换GC模式或屏蔽长帧日志掩盖此缺口。

## 正常客户端反馈后的定因取证

本节是上述修复之后的新证据，源码基线为干净的`master@c2f08c6`，已成功fetch。用户明确授权读取本次正常VS Code日志；探针使用独立Love & Justice谱面副本、同一私下Omni成品和真实PlayerLoader/ReplayPlayer，没有读取正式Realm或操作原IIDX HDD。新增证据在`artifacts/gameplay-cause-20261019/`，不与前述历史测试混算。

正常客户端日志确认Xecus与三张7K压力谱均进入结算、正常退出；曲中长帧为Xecus的434/487ms和38,973 notes压力谱的811ms，均同帧Gen2。另两张压力谱没有曲中40ms以上帧，但45,715 notes的2秒更新窗口最低77次/秒、最大27ms，不能以STALL阈值宣称无抖动。这些是Update日志，不是Draw/present测量；原日志没有GC Reason，不能追认其三次GC的具体触发者。

后续用45,715 notes实谱做两次60秒、一次20秒的隔离取证，真实桌面高性能管理器由PlayerLoader进入。采样期间没有截图或周期性JSON/CSV写入，保留现有诊断日志；主动把探针后台限帧提高到1000Hz以观察工作负载，但所有样本仍为`active=false`，所以这些结果只用于CPU/GC/内存定因与同宿主候选比较，不能代签正常前台显示流畅度。影片BGA仍回退静态图，不覆盖视频播放压力。

### 已确认的原因与影响

| 原因 | 本轮直接证据 | 能下的结论 |
| --- | --- | --- |
| 系统低可用物理内存引发阻塞全代回收 | `dense-before`曲中三次GCStart均为`InducedLowMemory`、`NonConcurrentGC`、同一`.NET Finalizer Thread`，持续193.5/215.2/183.3ms，并对应长帧 | 已确认这次隔离复现的直接暂停来源；不能倒推用户原日志的GC Reason，也不能仅据此把所有内存压力归给OMS |
| Omni编码素材重复驻留 | `dense-memory/owned-memory.json`在真实Player加载后读取所有者的元数据：capsule约400.8MiB、catalog约399.1MiB，后者6669个数组与capsule共享数为0；进程私有约1.88GiB、托管约1.14GiB，当时系统物理内存负载96%、可用约606MiB | 同一资源在两个长期所有者中各留一份是确定的额外内存成本；只读共享可消除约399MiB重复驻留，实际物理内存与暂停收益须修后复测 |
| 每次键音播放仍新建底层通道及调整绑定 | 50秒区间的采样分配在AudioThread约146.6MiB，其中`BindAdjustments`约102.1MiB；两者包含关系，不能相加。栈从`PoolableSkinnableSample.Play → DrawableSample.GetChannel → Sample.CreateChannel → SampleChannelBass`进入音频线程绑定 | 之前`PlayPathAllocatedBytes`只计Update线程入口，约10%的比例不能代表键音整体成本；现有same-sample优化省了drawable重建，未省底层channel/adjustments |
| 池化皮肤组件复用扫描全场景 | `resetSpecialisedStateMachineScope`每次apply/free都查全体runtime nodes，即使没有状态机；CPU trace确有该方法，但远少于滚动/Drawable更新等主要栈 | 是确定的可移除重复工作，不能把它夸大为全部密集谱掉速主因 |

GC映射依据与运行时精确匹配的[.NET 8.0.32枚举](https://github.com/dotnet/runtime/blob/v8.0.32/src/coreclr/gc/gc.h#L59-L77)、[Finalizer低内存事件处理](https://github.com/dotnet/runtime/blob/v8.0.32/src/coreclr/vm/finalizerthread.cpp#L188-L203)和[Windows物理内存通知定义](https://learn.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-creatememoryresourcenotification)。这条Reason 9路径与ImageSharp记账的`InducedNotForced`不同。系统全局压力还受其它进程影响，本轮没有结束其它进程来制造优化结果。

### 已否决的单改GC候选

当前`LowLatency`的约256KiB年轻代预算与高频小回收吻合，但改为`SustainedLowLatency`并非已验证方案。两次CPU/GC trace在预先固定的曲内5–55秒比较：

| Update wall interval | 原策略 | 单改GC候选 |
| --- | ---: | ---: |
| p50 / p99 | 5.850 / 12.950ms | 5.515 / 11.993ms |
| 最大间隔 | 222.446ms | 216.953ms |
| ≥40ms次数 | 2 | 28 |
| Update线程分配率 | 约8.50MB/s | 约8.50MB/s |

候选虽减少小回收次数并略改善中位数，却增加长帧，故已撤回；没有把GC策略改动留在产品源码。两个区间的EventPipe采样配置相同，所有帧和失败/候选记录均保留；仅一次对照不足以证明一般性能，但已足以拒绝按“GC次数少了”宣布优化成功。

### 具体方案与验收顺序

1. 先由P1-A收敛capsule/catalog的编码资源所有权：目录应消费同一不可变revision的只读资源，保留预算、完整替代项校验、取消/失败释放、旧revision租约与对外可修改副本隔离。不能改成游玩Update临时读原目录，不能删除未选中的custom选项来省内存。先证明驻留/峰值减少，再复测系统可用物理内存和全代暂停。
2. P1-J处理底层播放通道与adjustments重复创建。精确Framework当前的[SampleChannelBass](https://github.com/ppy/osu-framework/blob/ba04d538f02cecaabe5ab7dc3b102cd47d59d785/osu.Framework/Audio/Sample/SampleChannelBass.cs)中Stop是暂停、Play会续播，已自然结束的通道又拒绝重播，因此不能直接缓存后Stop/Play。需要窄范围、受测的从头Restart/复用能力，守住per-WAV cut、同文件不同槽、多声部、设备变化、pause/seek和revision退休；不得预造整谱通道或在生产反射原生句柄。
3. 皮肤scope复用仅处理当前visual已知的objectId与本地节点；无状态机时跳过复位扫描。维持free/reapply对象隔离、snapshot重建、故障退休与非IIDX皮肤行为。
4. 内存与分配治理后再重测GC策略，固定同谱、同包、同区间，分别保留轻量GC取证和无重型采样的前台Update/Draw长尾。音频同帧、判定/结果与实际设备听感分别验收。

本节完成原因调查和候选否决，未声称上述方案已实施。产品源码与私下皮肤没有变化，未为这次诊断重跑已通过且无源码变化的完整判定测试。新增宿主Release构建成功，三次真实Player部分曲目运行无scene fault；均未播放到整曲结束，不代替原用户四首结算证据。自定义EventSource标记没有进入trace，离线分析明确使用首个heartbeat与trace起始时刻关联区间；首次直接转换异常由工具生成ETLX后重读解决，原始失败日志保留。

## 验证证据

本轮工件根：`artifacts/gameplay-performance-judgement-20261019/`。

| 证据 | 有效结果与范围 |
| --- | --- |
| 首组修前 | `desktop-before-r2.log`成功；`bms-before.trx`16项中14个预期红例，`mania-before.trx`11项中4个预期红例；控制组通过。对应native时间、跨轨撤销、AT结算、HID开销、note lock和失败元数据 |
| 第二组修前 | `performance-second-before.trx`13项中11个预期红例：真实闭锁同步发现、replay稳定Apply、SP/DP血条分配及晚图Update读取；当时首组生产修复已存在，这组目标实现仍未改 |
| 同批松键修前 | `mania-batched-before.trx`两同批用例真实失败，跨帧对照与5个note lock用例通过；重抓分支明确取得错误Perfect尾/连击，非超时或初始化失败 |
| 聚焦修后 | `bms-after-r1.trx`56通过；`performance-after-r1.trx`49通过，3个夹具观察失败经纠正后`performance-after-r2.trx`19通过；`mania-batched-after.trx`11通过。旧BGA观察改为开局base+POOR均准备，血条值由真实HealthProcessor更新；另将原生资源断言放在owner drain后，均未放宽产品语义 |
| BMS完整 | `bms-full-final.trx`2845项：2799通过、28失败、18跳过。相对上一轮最终`bms-full-after-fixture-cleanup.trx`新增47个通过身份，无删除；28失败名称、错误消息与业务方法栈一致，仅规范化仓库路径、源码行号与编译器closure序号，`bms-full-comparison.json`无新增/变化失败 |
| mania完整 | `mania-full-final.trx`960项：958通过、0失败、2跳过；相对上一轮943项新增17个通过身份、无删除，见`mania-full-comparison.json` |
| 公共链路 | `core-gameplay-final.trx`255通过，0失败/跳过；包括新clock样本的4组正负offset/倍率、seek/pause/resume/reset/source与offset epoch，以及既有FrameStability/framed replay、加载、动画和编辑器寿命。命令筛选保留在`core-final-filter.txt` |
| 混合判定完整链路 | `bms-tap-pipeline.trx`5通过、`mania-tap-pipeline.trx`2通过（均含构造），真实手动回放验证上节独立oracle；这是全量之后追加的专项，不伪写为已重新执行包含这7项的全量 |
| 原生同宿主对照 | `native-before-3`与`native-after-1`退出0，后者60秒score2544/combo1272、异常0；固定前台区间分配减少但p99/最大帧未改善，缺桌面高性能服务的边界见上节 |
| 原生生产会话整曲 | `native-desktop-full-1`退出0，真实PlayerLoader激活LowLatency、整曲EX5428/combo2714、异常0；该次采样全部后台，不签收前台流畅度 |
| 当前构建/格式 | Desktop Release、Debug和Core Release成功；补充fixture后Desktop Release再次成功。最终Desktop/Core与补充fixture的format verify均退出0、报告`[]`；对应`desktop-*-final.log`、`core-release-final.log`、`desktop-release-pipeline.log`及`format-*-verify.*` |

完整检查期间45份相关源码摘要未漂移，见`full-check-source-control.json`。补充两份混合判定fixture单独验证；后续只改文档和工作区定点清理脚本。最终文档/工作区/暂存差异检查另记收尾证据，不将其当作新运行的玩法测试。

构建保留既有MessagePack 3.1.8的GHSA-qhrr-8q5h-9q3h、SignalR.MessagePack 9.0.17的GHSA-86xp-94pg-8r6r，以及测试CS8600/CA2007警告；本轮没有升级依赖或屏蔽告警。

初次构建的tuple nullable、测试namespace和无timeout等待分析错误均修正后重编，失败日志保留，不当作产品红例；首次原生宿主误用Core exact-runner身份未进入游戏，第二次只有后台60Hz样本，二者均不作前台比较。有效修前第三次与修后宿主源摘要相同，实际DLL清单单独留档。

原生宿主原版三份源码和追加桌面会话后的四份源码分别保存在`reproduction/native-probe-plain-after/`和`reproduction/native-probe-final/`，修前版本另存`native-probe-before/`。用户可使用总工作区deliverables内的`Clear-Gameplay-Performance-Probe.v261019.ps1`定点清理；它检查进程、绝对路径、reparse和四份源码摘要后才移除唯一disposable宿主。脚本不列原HDD、谱面副本、玩家数据、成品、共享依赖缓存或验收证据；未代用户执行删除。

收尾确认47份C#源码中，原全量范围45份仍与编译/测试时摘要一致，两份补充fixture已单独编译和验证。最终Omni摘要仍与开工值一致，原生宿主进程已结束。清理脚本语法和`-WhatIf`成功，预览631份文件约789MB，目标仍存在；客户端文档检查、总工作区`verify-workspace.mjs`及差异空白检查通过。检查只验证其声明的结构和来源范围，不刷新其它仓库的产品验收。旧隐私示例/公开制品指纹警告保持，首轮PLAN会话措辞失败已修正，日志保留。

## 保留边界

- 真实键盘、HID、设备延迟和听感仍需设备验收。单调戳是窗口回调接收时间，不是硬件物理触点时间；框架键盘回退、原生 BMS 输入与 mania 普通框架输入的能力不能混写。
- IIDX 闭源细节仍是当前 documented heuristic；本轮不宣称与实机每种窗口完全等同。BMS 六类判定、scratch/release/empty-poor、LN/CN/HCN、辅助及 TOTAL 继续按现有合同验证。
- Empty Poor 不增 EX/accuracy、不断当前 combo，但影响 gauge 和 FC/PERFECT；GOOD 保 combo/演出与 FULL COMBO 灯资格是不同合同，不改成同一条件。
- 地雷当前只有视觉/Ignore 路径，本轮未实现地雷伤害判定，不能将其列为已经有效的玩法判定。
- 尚未签收所有真实谱、极端密度、所有设备/DPI、视频解码器和系统调度的零卡顿；短时原生观测与组件零分配不能证明这些结论。
