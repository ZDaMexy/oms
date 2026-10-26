# Omni gameplay 增量与未使用素材审计（2026-10-26）

本轮基线为干净 `master@008f7a4998cd6e332f452519bd1719daed258910`，fetch后与origin一致。工作范围为客户端只读显示、独立作者包和原版参照，不涉及网络端点或正式玩家数据。当前交付与剩余门由[P1-A状态](../subline/P1-A/DEVELOPMENT_STATUS.md)维护；此前Start、挡板、LIFT和甩盘证据仍取[原记录](OMNI_GAMEPLAY_CONTROLS_20261025.md)。

## 本轮接入

PACEMAKER NEXT读取严格匹配的有效本地Personal Best和实际EX满分，七档为LV A、A+、AA、AA+、AAA、AAA+、DJ AUTO。[原版说明](https://p.eagate.573.jp/game/2dx/33/howto/play/score_graph.html)确认档位含义；OMS对满分的12/18至18/18逐项只向上取整一次，选择严格大于Personal Best的第一档，最高饱和。缺数据不当作0分，原主目标优先、SUB仅补缺、双方不可用则NO DATA。整数边界和等值升档是OMS策略，原机尚未逐项核对；NEXT+、rotate和Ghost不据此补造。

FAST/SLOW使用原gameparts的四张64×22图片；Combined、Keys、Scratch沿精确group分别读真实输入来源，Keys和Scratch不会互相覆盖。BMS保留原判定、offset、计分与累计FAST/SLOW，另外标记真实按下/抬起，排除自动完成与完整Autoplay。当前单次资格、覆盖和恢复策略只在[作者参考](../../skin-authoring/docs/REFERENCE.md#动画状态机绑定变体模板)完整定义，不冒称原机全等级或保留时长等价。

新配置提供Position A/B/C、各自Separate Display和OFF，默认A。A的Separate参考原生游玩截图；B/C的位置关系及横排/竖排取原版预览，具体几何和频道上下顺序为明确OMS适配。P2及DP使用实际C3宿主与精确组的镜像规则；仍跟随现有判定高度、Lift与亮度。普通mania没有此输入来源或配置，不借用BMS提示。

原主目标选项改由同ID的C2 module承接；每次只发布所选目标的呈现树，保留原六个值及默认值。新增NEXT与timing-display后共13个module、32项统一配置、1285个choice；原11类1271款美术与539张预览保留。没有增加字体或扩大预算。最终作者为独立的 `omni-full-v31-gameplay-timing-r2`，旧作者及OSK不覆盖。

## 原版参照的实际边界

操作仅在 `E:\oms-iidx-reference-20261018` 的既有独立副本进行，不使用真实卡。证据根为 `E:\oms-omni-gameplay-completion-20261026-r1`；API截图时间戳不作为游戏tick或硬件时序证明。

- 原版游客缺Personal Best时，菜单没有Personal Best、NEXT和NEXT+，不能据游客推断“无PB默认A”。
- `Chewingood!!!`、BPM190、无挡板的一次游玩中取到Normal Hi-Speed的20档。NOTES TIME依次为1200、1001、800、700、650、600、550、501、480、460、440、420、400、380、360、340、320、300、281、260；其中1001/501/281为截图实际显示，不擅自改成整百/整数理想值。仅一个BPM样本，未改OMS三种Hi-Speed的物理公式，也未证明20档跨BPM映射或原版倍率常量。
- 有效预览确认A轨内上方、B靠BGA的轨外同高、C轨外近判定线；Separate为A横排、B/C竖排。A实际双标签字形中心约为Keys(331,506.5)、Scratch(256,506.5)，同期BAD/POOR共享判定中心约(294,565)。没有将其伪称实测GREAT中心。
- B/C原生游玩坐标、PG覆盖、单次等级资格和提示保留规则未闭合。末段仍有自动POOR，先前“无输入五秒即证明连续保留同一提示”的推断已撤回；27/28图片名称误指也已排除。399帧采样保留为原始材料，不等于399次成功判定。

最终参照索引为 `timing-evidence/reference-index.json`，SHA256 `e5bad54569425839857430c60f6fe67e5ca6a8b3efa98fbb68b09d0e7f24d9d7`；作者位置收据独立为 `timing-positions-author-r1.json`，SHA256 `b98a7c506584625c7be143395be958049d85cd2bc26ae62da1017db85f177877`。

取证中自动审批曾拒绝独立副本的“准备配置、停止进程并重启”组合命令，仅返回 `blocked by policy`。子代理在收到停止重试指示前拆分执行了literal stop与Hidden启动，实际产生新进程；该不当重试完整保留，不能记成未执行。旧runtime/patch配置未改；新独立配置只增加菜单计时冻结。最终通过公开 `control.exit([0])` 正常退出，并核对进程消失，没有继续kill或重启。

## 素材是否已经完整分类

已有库按原始包、图片、UV、AFP/GEO、预览和用途族分层，但不是整个HDD的完整解包或语义分类。库存快照5539项含4405个IFS，主库只选841包；另外3564个IFS没有在该库解包。另计独立Full Combo的25包/468纹理，以及localized库的3张首音提示。

对冻结父作者r5的R4审计在 `E:\oms-omni-asset-audit-20261026-r4`。共22712条源纹理记录，分类如下；这是本轮接入前的父作者基线，后续新包不能直接沿用其未引用数量。

| 分类 | 源纹理记录 |
| --- | ---: |
| 内容相等的runtime引用，含像素别名 | 5284 |
| 已逐像素确认的atlas、字形或tint派生引用 | 1030 |
| 待核查候选 | 4868 |
| 明确范围外 | 6559 |
| 证据不足 | 4971 |

候选对应2447种独立像素内容，不等于2447项缺失功能。工具展开压缩recorded-animation、追踪字体、C2分支和INI材料，并核对原图与当前atlas裁片、UV/字形消费者及白转绿tint。COMBO蓝黄字形和大量动态挡板已经使用；仅凭原PNG没直接打包会误判。全部30631个读取文件大小和mtime保持，未解析scene资源ID为0。

本轮从这份审计确认并接入四张FAST/SLOW原图。后续优先核对HCN gauge及红绿分段的真实玩法状态、Auto Scratch原图、未接Graph条图、剩余COMBO绿红/pink/white条件；动态挡板余58条先查原AFP分支和导出覆盖，不先认定缺动画。Premium、副屏和Concentration仍在明确范围外。工具可复跑，路径与完整参数保留在R4 summary，不以重新排版刷新事实日期。

| 推进候选 | 进入实施前要闭合的证据 |
| --- | --- |
| HCN gauge、红绿分段及位置标记 | 原版何时出现/变化，与实际HCN持有或回复状态的对应；不能只接普通总血量 |
| Auto Scratch的`notes_auto_turn` | 当前真实Mod/物件状态、实际皿轨与原图显示条件，避免给普通note套错误标记 |
| Graph条图及剩余配色 | 原版配色选择、条宽和基线；继续读取当前真实分数与目标，不为原图制造假对手 |
| COMBO剩余绿红数字及pink/white | 真实运行触发条件；蓝黄字形已接，不能把整组COMBO登记为未实现 |
| 动态挡板余58条 | 原AFP条件分支、已有导出的覆盖与重复内容，先排除已烘入atlas或复用的帧 |

## 验证与保全

本轮证据在根目录 `tests/`、`native-*-r*` 与作者检查日志中；未把父作者旧13436组CLI或旧12次Player结果改记为本包结果。最终Core非视觉Skinning完整1397通过/3跳过，BMS完整3068通过/19跳过，mania完整995通过/2跳过，Desktop Debug/Release及SkinAuthoring Release构建通过。新增NEXT数学、真实来源、状态折叠、传输、晚挂载与精确group显示均有生产链回归；旧ImageSharp索引告警和既有nullable/CA2007警告保持，不扩张为全仓所有测试通过。

最终r2同包六组原生Player通过：BMS7 P1 A分开、BMS5 P1 A合并、BMS7 P2 B分开、BMS14 C分开、BMS7 OFF及mania16默认。BMS以普通Player和软件输入产生真实早/晚判定，核对两个频道独立、跨500ms保留、精确group不泄漏、OFF及正常结算；另有准时输入获得实际正EX。取景显式模拟宿主活跃并暂时停/启软件时钟，不是实体输入或前台性能验收。合成谱进入正常Results，但因没有持久化BeatmapInfo而不能保存成绩；不据此宣称原生取景验证了PB持久化，PB匹配另由实际Realm生产链测试覆盖。

最初r1作者实际导入被 `InvalidStableId` 拒绝，原因是生成器将数字用作点分ID的段首；修为合法字母前缀，并对全部105个分支的声明和引用执行既有严格ID校验，独立生成r2。首次r2取景在producer事件已到、consumer尚未消费时冻结画面而失败；夹具改为等待实际consumer sequence及epoch收敛后再取景，未修改生产显示或放松断言。原失败目录与日志均保留。

最终r2完整作者CLI通过13612种代表组合，各选项搭配其余默认值并覆盖全部显式条件，不是全部排列。全部7071份ZIP CRC及与作者源逐SHA256核对通过，闭包解压336192389字节、配置14589951字节；选中上界scene 513533/524288、manifest 64873/65536，字体仍10。准确回执为 `author-check-final-r2-exit.json`、完整日志及 `archive-final-r2-integrity.json`。

最终清理删除无调用方的Clear/Channel与Python重复NEXT算法，仅保留实际C#计算及其数学/生产链测试；没有改变原生取景和完整CLI执行的显示/生成逻辑或r2作者字节。清理后重新通过上述完整Core、Debug/Release/CLI构建及Python工具42项（引用审计12、Graph/C2 10、timing 20）；原初1398项Core及中间Python数字只保留在原日志，不与最终数量相加。本轮没有改变Hi-Speed物理公式，没有代签原版FAST/SLOW全等级/寿命、B/C原生坐标、真实歌曲、前台帧时间或设备体验。

交付为独立 `Lighting Model Skin Omni Until 33.v26102601.osk`，收存在总工作区 `deliverables/skins/` 的同名版本目录，附说明、实际Player预览、SHA256及完整验证JSON。OSK为323355410字节、SHA256 `6d6a19413f5c145c91e5a47f162864415796daf4b500c64fbeeb4a4b856a6eca`；旧`.v26102600`及其作者/原包保留。先重编并重启OMS再导入，模式/键数显式设置继续优先；这不是新的Windows发行包或公开作品发布。

磁盘空间不足时，将旧 `artifacts/omni-start-options-20261024/reference-captures` 的15149文件、2401203135字节完整归档到 `E:\oms-archived-validation-evidence-20261026`，逐文件SHA256复核，并用junction保留旧路径。没有删除截图、验收材料或正式玩家数据；此次归档不重试先前被拒绝的临时宿主删除，既有宿主继续保留复用。原HDD的14221份文件元数据及原件/参照各7份关键文件摘要均与本轮开始一致，旧参照配置也未变化；细节保留在仓库外 `source-before.json / source-after.json`，不把真实卡内容带入证据或交付。
