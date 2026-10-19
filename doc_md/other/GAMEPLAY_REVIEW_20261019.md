# 2026-10-19 游玩链路审查与优化

本轮按用户授权审查 BMS、BMS→mania 与原生 mania 的加载、输入、判定、回放、声音、BGA、皮肤动画和退出保存。修改前基线为 `master@965fa21ae0b2fae2e4aefa44e3ee55952e9dd32e`，工作区干净；远端查询成功。本文保存本轮证据，当前状态和剩余人工门仍由各 owning 子线维护。

全部探针使用开发缓存中的独立宿主与临时数据；原 IIDX HDD 未启动、未写入，正式玩家目录未作实验。Omni 仍为独立私下分享作品，本轮未将其内置、公开分发或修改交付包。

## 审查范围与不变量

| 链路 | 审查及本轮落点 |
| --- | --- |
| 解码、转换与 Mod | 核对 playable 初始化、nested judgement、lane/音频时间线；补 BGA 引用边界及视频分类，保留源 WAV slot 与 post-mod lane authority |
| 输入与时间 | 原生键盘消息转交 gameplay Update，核对双来源去重、组合键、失焦/暂停、回放接管及设备共享动作；主时钟、判定窗口和声音时刻不改 |
| 判定、血量与回放 | 修复完成长条后倒带的 holding 还原，以及 HCN 漏头后接回的 body gauge；LN/CN 仍不能重抓，HCN 头 Miss 不改写 |
| 声音与 BGA | invisible-only 样本在开局前准备，转谱提前/空击走同一 shared store，停止后立即复用通道；seek 清旧 POOR 与视频重试时刻 |
| 显示与皮肤 | recorded 动画复用准备期已验证的不可变几何，实际 draw node 保留冻结的坐标变换；修复编辑器退出与异步皮肤加载的集合绑定互锁 |
| 退出、结算与保存 | 无 legacy token 的退出不复制无用回放；本地游玩终态不等待旧网络提交。Player 的实际导入、UUID/hash 和保存后 OMSIR 队列保持原链路 |

没有重写判定模型、谱面 identity、成绩版本、音频后端、BGA 会话或皮肤 publication authority。正常 mania 的普通样本路径保持；移除玩法的旧 fixture 不以重新引入 osu/taiko/fruits 来修复。

## 已确认问题与修复

1. **键盘事件跨线程判定。** 原 Windows WndProc 位于 InputThread，却同步修改键盘 HashSet、动作引用计数和已加载 Drawable；真实 loaded 回归在旧代码失败。现在只入有序队列，在 gameplay input 的 Update 消费。reset 丢弃旧 epoch 的待发按键，只释放键盘已应用状态；关闭后晚到 callback 不再接纳。旧 sink 的解除不会摘掉新 sink。
2. **两路键盘事件与组合键。** 仅靠 pressed 集合不能去重两批完整 tap；原生来源可交付时，绑定键统一由该来源负责，失败或无法完整覆盖当前绑定组时保留普通框架输入。虚拟 Ctrl/Shift/Alt/Super 按左右实体键的 OR 状态判断，精确左/右绑定继续独立。字母/标点按固定框架的物理位置映射，小键盘保留 NumLock 关闭时的 Clear/导航身份。暂停/Break 的 ownership 与原生实际交付范围一致。
3. **长条回退丢失状态。** 完成后回退到 body，head 结果仍命中而 transient holding 已清零，原 LN/CN/HCN 再到尾部会 Miss。现由既有 replay pressed state、entry head/tail 与对象级自动辅助还原；倒退输入不产生新判定。未另建 hold 历史。
4. **HCN 漏头接回仍扣血。** 外观已 Holding，但 body tick 仍要求 head hit，导致持续误扣。现按实际 holding 判定 body；头仍 Miss，松开继续扣血，再接回才恢复。真实 replay、tick、gauge 和再次倒带均覆盖。
5. **声音责任旁路。** 原生 invisible-only 资源未进入预热；转谱 Note/Hold 的提前和空击仍走普通样本，绕过同 WAV slot 截音与暂停。现分别补齐实际 lane 时间线预热和 owner-aware shared-store 反馈；BGM/皿 sample-only 与长条尾继续静音，普通 mania 保留原路径。StopAllPlayback 回收 idle 通道，使同一 Update 的下一次 Play 可立即复用。
6. **BGA 的输入与 seek 状态。** 拒绝父目录跳转、绝对路径等可选引用，坏 BGA 不拒绝可玩谱面；补 `.m1v/.m2v` 分类。实际 GameplayClockContainer seek/reset 清理旧 POOR 和未来 retry timestamp，重新遇到该时刻不会重播旧 miss，也不会阻止 pending 视频再次尝试。
7. **动画重复计算与分配。** 准备期原本已经为安全预算计算全部层级、camera、mask 和裁切，却丢掉结果；每个 clone 每帧又计算一遍。现在一次保留扁平、私有的 float 几何与颜色值，所有 clone 共享。draw node 捕获 frame/piece/DrawSize/DrawInfo，绘制不再读活动 Source 或保存逐 piece 的历史最大数组。整包剩余三角预算在冻结下一帧前检查，公开准入额度保持。
8. **离线退出复制长回放。** 无 token 时原路径仍复制整段 Score 并排 worker，最终只打印跳过提交。现在先结束该局本地状态，再直接跳过无用提交；有实际 token 的分支保留快照。完整成绩的导入与 OMSIR 不依赖 legacy token。

编辑器集合互锁已用两次实际挂起线程栈定位，并在旧实现的绑定锁闸门回归中确认修前失败。只读观察改为直接订阅/退订，保留真正的双向选择；已排队及已捕获的迟到通知不能复活蓝图。不能将其来源写成普通游玩必然卡死，也不能把中止的测试算作通过。

## 受控测量

### 真实默认动画

使用同一份私有最终包的默认爆炸、三组 key beam 和三组 FC，共 7 个 program；分别测试 1/16/64 clone、同步/错开播放、是否调用真实 DrawNode.ApplyState，共 84 组。Release，预热 32 帧、测量 120 帧、60 Hz。测试用小纹理排除 PNG 解码和 GPU 上传，不加载游戏 HDD。

| 指标 | 优化前 | 优化后 |
| --- | --- | --- |
| 84 组稳态组件临时分配 | 随 clone 和动画复杂度增加 | 每组均为 0 B |
| 64 clone 爆炸，含 ApplyState | 约 55.0～64.5 MB / 120 帧 | 0 B |
| 64 clone 单组光柱，含 ApplyState | 约 58.25 MB / 120 帧 | 0 B |
| 64 clone FC，含 ApplyState | 单侧约 795～869 MB，双侧约 1.56～1.69 GB / 120 帧 | 0 B |
| 默认 7 个 program 的共享冻结 payload | 无此保留项 | 共 2,200,212 B，约 2.10 MiB |

这是组件采样/ApplyState 的分配测量，不是整局 FPS。屏幕变换由 ApplyState 转到实际提交顶点时执行，基准不包含 Blit/GPU，不能据此宣称相同比例的渲染耗时改善。新数据以保留内存换取稳态分配消除；现有每包 2m frame-triangle 预算下，几何/颜色 payload 上界约 424 MB，另有帧及数组头开销。此边界不含解码临时数据、纹理、其它对象或并存 publication，不能表述为全客户端内存上限。

### 键盘与退出

- 固定 Ctrl 组合键的 65,536 次 transition：旧 handler 分配 4,194,304 B，新 handler 预热后 0 B；不将此数值扩大为整个 Windows 消息或所有输入零分配。
- 真实 Player 中保留 131,072 个 replay frame 引用后退出：mania 从 1,177,064 B 降为 124,304 B；BMS 从 1,149,448 B 降为 96,368 B。计量范围为当前线程退出调用，原回放仍保留。该 fixture 的 TestPlayer 有既存 EndPlaying workaround，因此这项测量不能单独证明真实重试时序或 OMSIR 保存。

## 验证证据

长期证据位于 `artifacts/gameplay-review-20261019/`：修改前失败、TRX、构建日志、分配 JSON、线程现场和独立宿主源文件分别留存。无效探针导致的超时/编译失败与真实旧行为失败分开登记；最终结果以本节完成后的有效运行及 owning CHANGELOG 为准。

| 已完成验证 | 结果与证据 |
| --- | --- |
| Core 游玩/clock/loader、recorded 动画与编辑器寿命 | `core-gameplay-after-final.trx`：239通过，零失败/跳过，包含真实加载后的变换冻结与绑定锁/迟到通知 |
| 原有55个皮肤相关fixture | `core-skin-related-final.trx`：1167通过，零失败/跳过；相对原1159项增加8项，没有删除原身份，比较见`core-related-comparison.json` |
| 原生键盘 loaded 场景 | `bms-raw-threading-final.trx`：13通过；双路完整tap、混合chord两种顺序、左右Ctrl、首个Update、reset、其它设备引用和回退 |
| 完整mania | `mania-full-final.trx`：941通过、零失败、2跳过，共943项；四个旧AutoGeneration夹具修正后通过，全部用例集合未增删，见`mania-full-comparison.json` |
| 完整BMS | `bms-full-after-fixture-cleanup.trx`：2752通过、28失败、18跳过，共2798项；剩余28项均为旧Workspace getter，逐项对照修改前Release，见下文 |
| 当前构建与格式 | Desktop Release/Debug及Core Release成功；Desktop/Core最终`dotnet format --verify-no-changes`均成功、报告为空；保留既有依赖及测试警告，未生成发行包 |
| 独立 Windows 键盘宿主 | `native-keyboard-running/`：实际监听、成对Z/W输入、真实Perfect、SDL回声不重复、短inactive边沿清旧队列、Break普通键盘回退；动作全部在Update线程。实体设备未签收 |
| 优化后同包 Xecus 全曲 | `native-xecus-full-final/`：Direct3D11，约131.75秒完成，最高连2714、分数5428、2878个BGA事件，始终一个原BGA player、零scene runtime fault、包身份保持；首帧/中段/FC截图已检查 |

该全曲取景没有操作设置（receipt明确`settingsInteractionCovered=false`），不是这轮设置界面交互验收。宿主限帧、截图读回和结束时整包摘要计算均影响帧时；日志中完成后的约666ms停顿与探针同步计算408MB输入包摘要同段，不用于推导产品结算性能。

维护了两组旧夹具：HUD布局矩阵改为保留的BMS/mania及真实layout owner；mania AutoGeneration在生成前初始化nested对象、校正expected/actual顺序，未改生产autoplay或恢复已移除玩法。原生键盘夹具显式提供并逐用例重置测试Realm的绑定，避免保存的Ctrl组合污染后续默认键测试。

旧键盘handler的两项同步调用夹具已由真实加载后的线程、reset与其它设备引用用例替代；不能继续用“调用原生入口后立即已有判定”作为正确行为。完整BMS身份比较保留这两项退出及新增loaded场景的对应关系。

完整BMS首轮为2749通过、31失败、18跳过，共2798项（`bms-full-final.trx`）。其中28项与此前Debug完整运行的身份、消息及最先进入的应用栈逐字相同；Release省略部分测试运行器栈帧，不能声称完整原始堆栈一致。额外三项分别涉及删除重入、改名等待扫描和导入后扫描；当前及修改前源码的独立三项运行均通过。

另将开工HEAD完整导出到开发缓存，保留6196份源码输入清单及归档摘要，复用F盘依赖但独立编译Release。相同751项管理夹具为708通过、28失败、15跳过；28项与当前结果的身份、消息、全部有序应用方法符号一致，完整栈仍分别保留。比较取`bms-managed-release-control.json`，不以Debug/Release数量直接替代对照。

已确认的夹具风险是方法间共用目录/Realm，失败又会跳过后置视觉清理。首轮改名已物理移动而Realm未更新，后续初始化抹掉journal，紧接着的导入扫描便可看见额外未登记目录。因此`Added=1`不能单独证明重复导入；这条串扰解释不能代替对最初删除/改名失败的归因。生产目录保护及恢复合同未因测试失败放宽。

为定位初始拒绝，再用外部startup hook只观察原生目录异常，当前完整重跑为2748通过、32失败、18跳过，全部用例集合未增删，失败身份发生变化。删除目标通过；改名的取消等待、后置Realm确认和后续扫描失败。第四个报在HCN纹理用例上的异常，完整栈却只来自前一个改名测试30秒捕获闸门，并未进入HCN判定或绘制。

随后在隔离HEAD的原`InventoryChanged`条件已经成立后追加字段取证，保留同一拒绝；其完整BMS为2693通过、31失败、18跳过，共2742项。也重现改名拒绝及同一个旧线程断言污染HCN标签。某个其它目录只有LastWriteTime/ChangeTime从创建时值增加约0.6941ms，身份、属性等其余记录值一致；精确原始字段只留`baseline-native-sibling-drift.log`。两次改名失败阶段不同，不能把它们写成完整状态/堆栈一致。隔离副本的原始/诊断源码和DLL标记留档，之后6196份源码逐一复核均已还原。

微软说明NTFS枚举读取的目录项影子信息可能落后于文件本体，打开再关闭相应对象可促成更新，见[官方说明](https://devblogs.microsoft.com/oldnewthing/20111226-00/?p=8813)。它与本次字段实证吻合，但不能单凭这个差值排除其它写入。夹具现为造好的候选目录完成一次真实no-follow打开/关闭，保留所有元数据校验。导入扫描改为同时证明本次path和ID各唯一，不以整份共享目录的`Added=0`代替重复检查。

仅补失败收尾时，七项独立检查为6通过、1取消等待失败，HCN迟到污染已不出现；再补目录信息发布后七项全部通过。两份结果分别为`bms-managed-after-cleanup-focused.trx`与`bms-managed-metadata-focused.trx`，不丢掉中间失败。初轮删除的具体原生拒绝点没有被捕获，不将其或此前宽Core的三项删除直接归成同一个根因。

随后完整BMS收敛到2752通过、28失败、18跳过。与开工HEAD的相同751项Release夹具逐项比较，28个失败的身份、消息和有序应用方法符号全部相同，均停在旧`FullSkinSettingsCallerHost.Workspace.Single()`；比较见`bms-managed-release-control-final.json`。相对上轮完整2742项，新增58项、退出两项旧同步键盘测试，没有新增失败，见`bms-full-comparison-final.json`。原始栈仍因构建配置/路径及行号不同而分别保留。

全量结束后复核框架收尾顺序：固定版本的视觉runner在NUnit单例`TearDown`之后才运行当前视觉步骤，因此该回调只能回收前例；失败又可能跳过`TearDownSteps`。现在下一例在Update确认旧worker已join后才替换journal，另由本夹具`OneTimeTearDown`在基类关闭host前调度最终Update收尾，覆盖仅运行一例且失败的情形。真实holder逐个同步Dispose后再关闭manager，异常跨线程传播且等待有界；不改通用runner或生产生命周期。

最终仅增加上述fixture结束回调后，重新编译双配置、七项正常场景全部通过（`bms-managed-lifetime-final.trx`）；再仅运行一个已知Workspace失败作为负向控制，2.28秒正常结束，保留原`Single()`异常，没有新增OneTimeTearDown失败或收尾超时（`bms-managed-lifetime-negative-control.trx`）。完整BMS数字来自此前全量，末次回调变更没有重跑整套；生产源码与已完成的全曲/Core/mania验证保持相同。

一次替换旧Blueprint源码的before运行复用了较新的DLL，因此不算有效旧实现通过；显式更新源mtime、核对实际DLL包含旧`targetComponents`字段后，`core-blueprint-lifetime-before-rebuilt.trx`在预期锁依赖处失败。其余修前探针的初始化/时钟/Realm问题和一次Windows拒绝前台激活（未发输入）均保留原始日志，不混作产品失败或签收。

最终Desktop Release保留四项既有警告：MessagePack 3.1.8（GHSA-qhrr-8q5h-9q3h）、SignalR MessagePack 9.0.17（GHSA-86xp-94pg-8r6r），以及BMS测试中的CS8600/CA2007；未屏蔽警告或借本轮升级依赖。Desktop/Core完整格式检查及末次夹具格式复核通过；文档健康、总工作区导航/事实登记检查和diff空白检查通过，未修改其它仓库的既有工作。

## 保留边界

- 真实键盘、HID/轴、设备听感、窗口/DPI、特殊谱及长时门继续由 P1-B/P1-E/P1-G/P1-J/P1-L 维护。原生 SendInput 或 loaded fake 不能代签实体设备。
- replay 无状态变化时的录制/输入分配、缺失 HID 设备时的发现频率、密集候选扫描仍是源码观察到的后续测量点。本轮未对它们做同场景测量，不声称这些热点已优化，也不先修改 replay 边界或热插拔语义。
- 完整回归必须逐项比较旧失败的 identity、message 和业务堆栈，不能只按数量签收；本轮不提升公开发行或 Skin V1 的人工验收状态。

独立宿主与修改前源码/编译副本、tar合计约1.70GiB，最终探针源、源码输入清单和长期证据均已留档。定点清理脚本`Clear-Gameplay-Review-Probes.v261019.ps1`只作AST语法检查，交由用户手动执行；校验精确绝对路径、reparse、活动进程及源码保全，不清共享依赖、谱面副本、玩家数据、最终皮肤或证据。本轮仅本地提交，未推送、打包或部署。
