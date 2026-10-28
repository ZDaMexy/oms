# Omni gameplay 增量与未使用素材审计（2026-10-26）

10月26日首轮基线为干净 `master@008f7a4998cd6e332f452519bd1719daed258910`，fetch后与origin一致。工作范围为客户端只读显示、独立作者包和原版参照，不涉及网络端点或正式玩家数据。当前交付与剩余门由[P1-A状态](../subline/P1-A/DEVELOPMENT_STATUS.md)维护；此前Start、挡板、LIFT和甩盘证据仍取[原记录](OMNI_GAMEPLAY_CONTROLS_20261025.md)。

2026-10-27存储归整后，下文历史`E:\...`证据路径按[归并表](../mainline/CHANGELOG.md#2026-10-27oms外置开发目录归并)读取：素材/独立参照在`E:\OMS-Development\sources/`，其余九个证据目录在`evidence/`，原目录名和内部相对路径保持。原命令、来源日期与验证身份不改写。

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

## 2026-10-27素材候选闭合与游玩增量

接续基线为干净且与已fetch的origin一致的`master@87b4bc827aa820fc97c0d1f9349c273571d33f7b`。新证据独立保存在`E:\oms-omni-gameplay-states-20261026-r1`，不把上方v31或父候选的结果自动改记为新包。原版只启动既有E盘独立副本，未使用真实卡；本次通过官方`control.exit([0])`正常退出，原件14221份文件元数据及原件/副本关键文件摘要保持。

AUTO SCRATCH接入原`notes_auto_turn`与长皿AUTO头/身/尾，依据精确真实物件的辅助角色换图，完整Autoplay仍走普通图。`notes_hcn01_long`的AUTO端点原高24、普通端点原高27，分别保留原尺寸；joint不充当body，未扩张BSS/MSS规则。实际Player另发现池在Mod应用时仍持未辅助占位物件而丢弃Hidden/Tint设置，现保留角色配置并在真实OnApply/复用时重采，普通物件恢复原Alpha/Colour。字段和恢复语义只在[作者参考](../../skin-authoring/docs/REFERENCE.md#动画状态机绑定变体模板)维护。

Graph使用原blue/green/red分别显示Current、真实Personal Best与Target；Personal Best独立于主目标，缺记录不制造0分，Personal Best only不再重复绘制PB目标柱。主/SUB/NO DATA资格保持。三柱使用90×690原PNG，显示高度689、边框94×692，固定于既有Graph区域；这些横向位置及1px缩放明确属于OMS作者适配，P2沿原-1502平移、DP保持右侧。未据文件名加入yellow/team/ROG或600帧滚动。

58条挡板候选闭合为16条无GEO消费者、10条虽有GEO但未被AFP放置/导出，以及32条lane345后半cat帧。后一项原root边框128@60、同一cat child256@60，旧导出按root截掉cat33..64；新导出保持两个独立循环，并逐tick核对cat连续512tick、border128tick的原图、UV与几何。独立cat movie128@30已在嵌入时翻倍，不再二次倍速。为守原64MiB编码字节门，仅一张派生atlas使用lossless/exact WebP，整页含透明RGB逐字节一致，原PNG保留。原机游客CUSTOM仍不可用，不将来源连续性或OMS播放宣称为原机parent-loop验收。

HCN gauge及红绿段只在已查来源中定位到固定教学组合`hcn_sp/dp`、`x_hcn/x_gauge`，不是可直接消费的实时正常游玩状态证据；本轮不为这些演示素材新增Gauge API，也不据此断言原机所有HCN显示已等价。该候选归为教学来源，后续若有正常游玩的具体缺失再以实际状态立项，不能按同名文件制造新功能。

COMBO新原机无输入demo中，799–804图保持同一COMBO64/EX0128，实际显示pink label+red digits、blue+blue、white label（画面绿色）+green；原PNG匹配与EX局部像素一致，证明同一次PGREAT内部换色。已有正常输入图确认GOOD1仍是黄色，BAD/POOR不带0；[官方用语集](https://p.eagate.573.jp/game/bpl/bpl2021/about/term/index.html)也将PGREAT描述为彩色闪烁，但不提供33的相位表。新作者使用三套完整原RGBA配对，蓝→绿→粉各100ms、300ms循环是明确OMS作者节奏；沿原`judgement.hit`相对起点，不冒称原机精确周期或起相。yellow保留Great/Good等原规则，不当PGREAT第四相。

BMS零连击改为单独居中判定；普通mania的零数规则保持。label-only独立单cell居中，消除隐藏数字仍占横排宽度的问题。P1/P2/DP共用同一模板并保留真实布局锚点，各material/实际Group仍有独立判定起点和状态；`combo.active`与`layout.presentation`的受限y变体不改计分或输入。四套COMBO字形使实际组合需要12套字体，声明上限相应为12，其余字形、图片、场景预算保持；并以Frame独占且未被INI消费的PNG短路径收敛manifest，不放宽64KiB/512KiB门。原图字节、Frame场景、全部INI、款式选值及539预览保全。

最终冻结作者为`omni-full-v35-combo-states-r3`，OSK为331565626字节，SHA256 `6599e157bddd0b9765b17b71627d5c45255406da5792500c08e8cf9b3e0141ef`。完整当前CLI通过13612组代表组合及各显式分支，并非全部排列；日志为`tests/author-check-final-v35-r3.log`与同名exit回执。7070份ZIP条目CRC和逐SHA256作者闭包匹配，展开344881235字节，JSON/INI配置14969846字节，编码录制程序4855157字节单独记账；以`archive-final-v35-r3-integrity-r2.json`为准，前一份回执误把录制程序也计入配置，仅保留为诊断。选中上界manifest65530/65536、scene523788/524288、12套字体，原11类1271款及539预览保持。父v31～v34的OSK和来源摘要均未变。

最终生产源码完整回归：Core非视觉Skinning1434通过/3跳过、BMS3079通过/19跳过、mania995通过/2跳过；Desktop Debug/Release与当前作者CLI构建通过。Python工具98项通过（AUTO25、Graph15、动态挡板15、COMBO17、原controls26），最终23份C#增量的format verify通过。完整回归后仅修改原生取景夹具并重新编译BMS Release，未改生产源码；准确命令、TRX与构建身份保存在本轮`tests/`，最后取景运行时身份为`current-binaries-v35-r4.json`。

同一个最终OSK九组正常Player全部exit0：`native-ascr-visible-hcn-r6 / hidden-cn-r6 / untinted-ln-r6`核原图、材质、实际辅助状态及Hidden/Tint；`native-combo-p1-r4 / p2-label-only-r4 / dp-r4`核真实输入命中后的blue/green/pink和Miss零连击，DP两侧独立起点；`native-final-p2-pb-only-r3 / dp-next-fallback-r3 / mania16-r3`复查两侧、Graph缺PB/主SUB资格、双挡板/LIFT与mania隔离。COMBO按自然游戏时钟停帧，没有seek、重置或直接写入分数；barline的IgnoreHit不计作实际计分物件命中。取景显式模拟宿主活跃及软件输入，合成谱/隔离结果不代签用户正常窗口、PB持久化或实体控制器，真实PB生产链与数学边界另由现有测试覆盖。

中间作者r1的slot重复、COMBO缺step easing/opacity carrier和label-only占位已在后续独立候选修正，失败作者及日志保留；取景夹具最初误把离散envelope时间当作连续consumer时间，并将barline IgnoreHit计入命中数，现已修正且不重写旧失败回执。它们不混计为最终同包通过。原版参照正常退出，`source-after.json`确认原HDD14221份文件元数据及关键摘要保全。

本轮交付`Lighting Model Skin Omni Until 33.v26102700.osk`，由上述冻结OSK直接复制并核对摘要，连同说明、验证JSON、校验及最终同包预览收存在总工作区`deliverables/skins/`同名版本目录；旧交付不覆盖。须先重编/重启OMS再导入，原模式/键数显式设置保持。已识别候选的实施/排除已闭合；原版精确PG周期、lane345 parent-loop、FAST/SLOW资格及完整变速/设备体验仍保留具名缺口，本轮没有生成Windows发行包或公开发布素材作品。

## 2026-10-27Omni与BMSMod交叉评估

按用户要求评估当前32项Omni配置与BMS实际提供的23个Mod，基线为干净且已fetch一致的`9a6d1459`，使用上述最终v35-r3及相同OSK摘要；没有修改玩法、作者目录或生成新皮肤包。作者侧实际扫描1557份scene及41份INI，定义、适用范围和消费者见本地忽略证据`artifacts/omni-mod-config-audit-20261027/author-options.json`，15框体×三布局的精确节点/默认图/分支收据为同目录`gauge-45-layouts.json`。这是源码与最终包评估，不是新增原机或正常前台实测。

确认三项需要后续处理的设计/显示问题：

| 问题 | 最终事实与玩家影响 | 建议处理方向 |
| --- | --- | --- |
| 普通血条固定80%视觉分界 | 最终普通PNG有50格，第40格起红；全部Frame未消费`gauge.clear-threshold`。IIDX/LR2规则的A-EASY门槛为60%，BRG 5K Normal/Easy为75%、Assist为50%，真实已Clear仍可能未到原图红区 | 保留原美术，同时明确表达真实Clear门槛/状态；不能让固定配色代替当前规则 |
| 非33框体HAZARD落回普通图 | 仅iidx33三布局有hazard资源分支，27～32及八支BPL的42布局在普通底、亮填充和暗区均回退normal；真实HAZARD规则仍生效 | 补齐可靠的HAZARD视觉映射；原代素材不足时明确标示当前模式，不无声使用普通图 |
| PB/NEXT按纯外观与记忆偏好分组 | `GameplaySkinEventRuntimeHost.LivePresentation.cs`按全部APIMod设置精确匹配本地成绩；SUD/HID透明度、ASCR/ANOT着色以及LIFT的RememberGameplayChanges单独变化，都会使旧Mod签名不匹配，PB与NEXT可能读不到原成绩 | 依据实际计分/判定影响区分比较资格与外观/记忆偏好；不得简单忽略所有Mod或跨规则合并成绩 |

第三项由独立诊断程序使用真实Mod、APIMod序列化与生产相同的DeepEquals谓词验证五组设置；结果为五组均不匹配。没有伪造真实用户成绩丢失或在正常窗口演奏复现；现有Realm生产链另确认有效成绩加载及不兼容成绩拒绝。探针首次仅因自身ProjectReference多一级路径而编译失败，修正后的r2成功，旧日志保留，不计为产品失败。

另有一处明确外观职责重叠：ASCR/ANOT的Tint与原音符/HCN配色、皮肤亮度逐层相乘，因此开启Mod着色后不会保持原图原色；这是当前正确叠加，没有互抢同一设置，也不是刚修复过的池化Hidden/Tint失效。后续可明确“原皮肤配色/叠加着色”的入口语义。挡板图案与SUD/HID启用、LIFT艺术与真实抬升、CN/HCN款式与长条机制、判定高度/字形与Judge Mode/Rank、P1/P2停靠与Mirror/Random均有独立消费者，未发现皮肤改写玩法状态。选框体不自动选择IIDX Judge/Gauge；BMS可选Mod中没有Reverse、ConstantSpeed或Hi-Speed，Hi-Speed仍归规则集配置与Start控制。

判定显示OFF只关文字/COMBO，独立FAST/SLOW仍由自己的选项控制；透明挡板不等于禁用SUD/HID。这些是入口含义与依赖提示问题，不能冒称Mod覆盖故障。GAS真实类型变更会持续发布，问题在作者缺少相应图形分支/门槛表达，不是规则状态被冻结。

本次定向复跑全部通过：PB加载22项，ASCR/挡板透明度/LIFT显示11项，Gauge/GAS65项；与同基线当前Release产物一致，命令、TRX、探针源快照及日志留在`artifacts/omni-mod-assessment-20261027`。98项结果只证明所述原生链路和现行语义，未代签42布局HAZARD或非80%门槛的修复；上述三项仍未实施。

## 2026-10-27Mod问题修正与用户判决

修正接续干净且已fetch一致的`master@52b0fe5bb67486522116d076b4b0c28db17e57cf`，独立证据位于`E:\oms-omni-mod-solutions-20261027-r1`。用户分别选择：PB/NEXT不按Sudden/Hidden/Lift有无、位置或透明度分开；ASCR/ANOT保留默认叠加着色和原保存值并说明关闭后使用皮肤原色；旧框体保留原图，以明确HAZARD/生存状态标识区分，不借用33蓝条。上节为修正前评估，不将其“未修复”状态当作新候选事实。

BMS新增规则集拥有的本地PB比较副本：剥离SUD/HID/LIFT已知显示/记忆字段，只有没有未知设置时才去除该Mod空壳；ASCR/ANOT只忽略Tint开关及颜色。辅助有无、辅助可见性、Judge/Gauge、CN/HCN、MR/RD与seed及未知数据保持严格区别，已声明默认值才与省略值等价，不解析文本猜数值。真实默认元数据只捕获一次并冻结，逐条候选不新建全部Mod或反射。Core仅接收可选规则集回调，无BMS acronym；无回调仍完整匹配，回调违约不静默换回另一策略。原玩家/谱面/玩法身份、版本、规则统计、失败/删除及有效成绩门保持，原ModsJson/APIMods、回放和传分不改。真实Realm回归实际保存BmsScoreProcessor结果，核旧记录、PB/NEXT同源、未知拒绝及关闭Realm后的reset/晚挂载，不手填假成绩。

原代HAZARD来源检查另确认共用`playm_gauge_event2`与Hard/ExHard同几何，但已查AFP/GEO没有足够关联证明其HAZARD语义，且不是33 HAZARD的同像素别名；它没有被采用。冻结新作者为`omni-full-v36-gauge-state-r1`：45布局按真实门槛画细线，P2只反向线坐标，CLEAR/NOT CLEAR文字仍正向；Hard/ExHard/Hazard不画0%线，HAZARD / SURVIVAL读取实时type。原图的80%配色保留为美术，真实资格由新线/状态表达。新子树只对BMS显示，没有新配置/bitmap字体，mania保持原表现。

为守原预算，仅压缩没有引用的Frame program/case/keyframe身份，避开所有其它模块的声明ID；节点、resource/font/material/INI身份保持。另46个不被C4/INI引用的私有媒体路径采用完整SHA核对的短别名。3505份旧PNG、41份INI、1511份非Frame scene与root scene逐字节保全；45个Frame移除新增子树并反向映射后完整恢复原JSON，1285选值、539预览及其bytes保持。独立证明为`artifacts/omni-mod-config-audit-20261027/gauge-state-r1-preservation.json`。新OSK331581856字节、SHA256 `f1251bdaa01e7d0ffa63ebb0790ded170d21e58afbe0a0405bf79675dd29195a`；父包仍为上节6599e157摘要。闭包7071文件/344991393字节，配置15079936字节；选中上界manifest65051/65536、scene523741/524288、12字体，配置仍32项/13模块。

初次夹具使用了不存在的Host.RuntimeNodes属性，改为既有lightingNodes读取后重编；最初Core focused只发现旧22项，因Desktop.slnf不含Core测试工程，显式编译后实际43项全部通过，并再次编译BMS/mania消费者，避免Core helper新签名与旧引用混用。原生r1先因漏启CAPTURE_COLUMN导致原RGB合同拒绝，修正运行参数后r2六组BMS实际Player全部通过；这些是夹具/执行配置错误，旧失败日志保留，不作为玩法或作者修复证据。

最终生产源码完整Core非视觉Skinning1455通过/3跳过、BMS3104通过/19跳过、mania995通过/2跳过，Desktop Debug/Release与当前作者CLI构建通过，最终formatter verify无改动。新PB focused为43项真实Realm/原链路及25项领域比较，Python Gauge工具6项通过；数字不与旧修正轮次相加。命令、TRX和构建身份取本轮`tests/`，最新原生程序集封存为`current-binaries-v36-r2.json`。

同一f1251bda最终包八组实际Player通过：`native-iidx33-aeasy60-p1-r2`、`native-iidx27-normal75-p2-r2`、`native-iidx28-aeasy50-p1-r2`、`native-iidx27-hazard-p2-r2`、`native-bpl-hazard-dp-r2`、`native-iidx33-normal80-dp-r2`及`native-mania-7k-r1 / 16k-r1`。六组BMS各五个自然时钟样本核实际规则、门槛、坐标、正向文字及clear-state，HAZARD标识实时读取type且不画0%线；另两组mania复核隔离。全部40张PNG与30份Gauge严格读数回执保留，汇总为`native-summary-r1.json`；取景明确使用隔离正常Player和真实Autoplay结果，无生命/分数注入或seek，不代签用户正常窗口、真实歌曲或实体设备。

最终当前作者CLI检查exit0，13612种代表组合及各显式条件分支全部通过，并非全部排列；原日志为`tests/author-check-v36-r1.log`及同名exit回执。实现已提交`0b9f3c9695d6fd294595ddc2b3cacb7d337b6fb0`，最终文档与交付另外收口；完整验证后没有改变作者源或OSK字节。新交付`Lighting Model Skin Omni Until 33.v26102701.osk`为上述f1251bda冻结包的直接复制，连同六张最终实景预览、说明、SHA256及含实际源码提交身份的验证JSON收存在总工作区`deliverables/skins/`同名版本目录，旧版不覆盖。须重编/重启OMS后导入；原成绩、回放、传分与原HDD均未改动，没有生成Windows发行包或公开发布素材作品。

## 2026-10-27Mod游戏设置与皮肤职责分离评估

本次接续干净且已fetch一致的`master@1336d257f6f468ff8ee5c97fd7fc551f3d26798d`，核对现有BMS Mod、游戏设置、Omni最终32项配置及真实消费者。以下为源码与冻结作者的静态评估和建议，尚未移动入口、迁移配置或更改玩法；没有新运行构建、回归、原机或用户窗口验收，当前交付仍为`.v26102701`。上节三项用户判决保持，不能把本节建议当作已采纳合同。

划分建议以数据职责为准：Mod表达当局谱面、判定、计分、辅助与挑战条件；游戏设置保存玩家操作、读谱、显示偏好及记忆策略；皮肤声明美术、排版、可支持的展示和作品默认。Mod也可被长期记忆；改变视觉的挑战亦可能属于Mod，不能只按是否改变分数倍率划分。多个页面或Start快捷操作可控制同一状态，不能因此复制持久化源。

| 当前内容与实际落点 | 建议职责与保持项 |
| --- | --- |
| ASCR/ANOT的自动演奏、Visible/Hidden与Tint均在Mod的SettingSource | 自动演奏和辅助可见性仍按既定成绩条件区分；Tint开关/颜色适合独立的玩家显示偏好，默认true和旧保存值保持。皮肤提供AUTO图，读取真实辅助角色，不决定辅助是否生效 |
| SUD/HID的值、透明度及Remember、LIFT的值和Remember位于Mod；Hi-Speed在规则集配置 | 读谱几何与Hi-Speed/目标可见时间须由同一个游戏侧状态和完整记忆策略管理。皮肤只绘制真实边界/白绿字与图案。是否保留Mod快捷入口属于产品取舍；现有Mod类型和旧记录读取不得直接删除 |
| Graph主目标、SUB目标及FAST/SLOW开关/频道等选项跟随皮肤保存 | 目标选择及反馈内容适合作为按玩法保存的玩家偏好；Graph外框/条图、FAST/SLOW字形与A/B/C实际位置继续归作品。皮肤未声明展示能力时明确边界，不能制造假目标或自动新增通用浮窗 |
| 判定美术/高度、音符亮度、特效尺寸、P1/P2和转盘表现等位于皮肤配置 | 保留作品原坐标、适用键数和支持布局。公共“显示什么”与皮肤局部“画在哪里/多大”分开；跨皮肤默认与每皮肤校准的覆盖关系须明确，不直接全局套用原图像素或原版标尺 |
| Judge/Gauge家族、Gauge类型、CN/HCN、Mirror/Random等位于Mod | 继续保留当局规则和成绩身份；可以整理为规则预设/分组入口，选IIDX/BPL框体不能暗中切换Judge或Gauge。血条美术只消费真实type/threshold/state |
| Hi-Speed、自动键音、BGA开关、输入与音频偏移属于游戏设置 | 不随皮肤重置。BGA窗口/外框与键区美术归作品；音频偏移、显示偏移和判定文字高度不可混为一个“判定调整” |

Omni32项的评估分组为20项保留皮肤、7项玩家偏好候选、5项先拆语义与适配。20项涵盖11类美术、note/charge尺寸、HCN着色、爆炸尺寸/光柱长度、开场三开关和柱底亮度，保留实际原素材/局部校准。7项为`graph-visible / graph-target / graph-sub-target / pacemaker-percent / judge-panel-visible / arrival-cue / lift-readout`，候选保存范围是规则集默认及可选键数覆盖，不能声称已经迁移。5项为`layout / timing-display / judgement-display / judgement-position / lane-objects-brightness`，需先定义公共语义与作品适配的边界。

三个具体混合点已有消费者证据：`lighting_timing_display.py`将开关、合并/键皿分离频道和A/B/C原布局位置合为一项；`lighting_combo_states.py`的`judgement-display=off`仅关判定标签/COMBO，不关独立FAST/SLOW；`lighting_judgement_position.py`的-2..10是作品标尺，不能直接当跨皮肤像素。32项没有独立“判定大小”，爆炸尺寸不能代称它。`lighting_start_options.py:8`现标签确为“轨道物件亮度”，绑定范围包括音符、长条、光柱、爆炸、判定及小节线，FAST/SLOW沿同亮度投影；这是现有宽范围语义，不是已经叫成“音符亮度”的文案错误，迁移为窄范围值前须明确取舍。`layout`应映射皮肤支持的展示角色，而不是把作者p1/p2 ID无条件套用到另一包。

当前保存范围确有差异：`BmsModStatePersistence`保存选中acronym及各Mod的APIMod设置；Player先DeepClone出当局Mods，挡板通过`rememberGameplayAdjustment()`有条件回写原选中Mod。`RulesetConfigCache`目前按ruleset缓存、配置默认variant=0，不能把尚未实现的按键数读谱配置说成当前能力。`SkinOptionPreferences`按skinId/ruleset/keycount保存，键数层覆盖模式默认；`SkinOptionsSettings`和布局准备冻结下次游玩/预览的配置。因此现在换皮肤会换Graph/反馈偏好，游戏内速度调整却立即修改游戏配置，单纯挪控件不能统一这些生命周期。

另发现一条需要运行回归的具体保存风险：`DrawableBmsRuleset.cs`的311–314行把Hi-Speed双向绑定共享配置；`AdjustFromScratch()`的1306–1314行先调runtime挡板，再把Floating补偿写入该配置；`rememberGameplayAdjustment()`的1485–1494行在Remember=false时拒绝回写选中Mod。于是同一操作可能保留新Hi-Speed而在新局恢复旧挡板，目标可见时间不再成对恢复。例：相同BPM/几何下Floating2.50、SUD250且Remember=false，80个relative counts折为20单位，挡板局内变270而选中值仍250，补偿Hi-Speed约2.43被保存；下一局组合不再是原来的配方。数值是现行公式的推导，不是本次运行测量。SUD+HID记忆开关不一致也有部分几何/完整速度组合风险，不能只给SUD单项打补丁；应先明确成组记忆语义，再补新局/重启/混合开关/模式切换回归。

两个边界需保持：BMS Hidden是下遮挡，mania Hidden是随combo变化的淡出挑战，mania Cover/Constant Speed也各有原生Mod语义，不能跨玩法按同名批量迁移。Floating当前基准时长仍取mostCommon/initial anchor，当前BPM参与实时读数和转盘重锚；不能因旧提示不完整就声称已自动随当前BPM恒定绿字。`GimmickScrollMode`是BMS渲染算法选择，不是皮肤动画，也不能仅因“视觉”字样与mania Constant Speed归为同一设置。

建议实施顺序为先闭合读谱联动保存，再分离纯显示偏好，最后整理入口和作品局部配置。迁移须保留玩家显式值、旧Mod/回放读取及原成绩，区分恢复玩家偏好与恢复作者样式。当前PB投影的已知忽略字段从现行SettingSource元数据构造，移走Tint声明时必须显式保留历史字段的查询处理，否则旧Tint会变成未知字段而重新拆分PB；不得改写历史ModsJson。是否完全移除SUD/HID/LIFT的Mod入口、合并三个Remember开关、按键数保存读谱配方、回放采用历史Tint还是当前偏好、公共偏好如何覆盖皮肤默认尚属待判决设计，不在本次评估中执行。

## 2026-10-27BMS读谱设置与统一记忆实施

用户采纳上述两项：Sudden/Hidden/Lift迁出可选Mod菜单，三个Remember合并为“记住游玩中的读谱调整”。接续干净且已fetch一致的`master@2c8bb223d328374ba255448d0b147d8ed9715618`，证据独立保存在`E:\oms-bms-reading-settings-20261027-r1`。其它显示偏好、Tint、Graph、每键数读谱保存与作品配置尚未迁移；旧交付`.v26102701`及作者/OSK字节不变，不生成新皮肤包或Windows发行物。

入口为BMS游戏设置内的“读谱设置”，集中三种Hi-Speed、Sudden/Hidden的组合/白字/不透明度、Lift及独立挡板显示开关。`LaneCoversEnabled`记录当前显示，`SuddenEnabled/HiddenEnabled`保留所选组合，双击关闭后仍可恢复Hidden或双挡板；Lift不随双击改变。中文提示引用实际“游玩：挡板／调速”绑定，不要求玩家寻找不存在的EFFECT绑定行。

运行时使用脱离配置的本局值，私有原生挡板适配器不进入Mods。每个顶层动作完成全部速度/几何联动后才按统一Remember保存13字段；关闭时本局改变不会单独留下补偿Hi-Speed。设置页显式编辑单向更新当前局，保存回灌有同根抑制，退出解绑；不透明度更新不重置Floating暂存目标。统一配置仍是BMS规则集级，未新增按键数配置。准确行为以[P1-A合同](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#既有产品面硬约束未被-skin-v1-取代)为准。

首次构造在读取旧PersistedModState后迁移已选组合和保留参数；任一明确旧Remember=false则统一false，已有新键或完成标记优先，坏旧JSON/值明确记录并按原可恢复语义处理。Mod持久化绑定过滤旧预设带入的三项，仅调整新选择，不改旧预设或成绩。三种旧类型保留为System，CreateAllMods/APIMod解码及已知PB字段继续可用，实际判定、计分、Gauge和PB策略不变。

新录制在`reading_settings`保存不可变的初始配方，录制结束不让后续配置覆盖开局值；没有新增局内读谱变化轨迹。历史无快照时仅由旧Mod决定挡板/Lift，不把当前个人新挡板套入旧回放；播放只读，不回写个人配置。GeneratedAutoplay继续按当前局处理。原TOTAL_RULES_VERSION=7与旧结果/Mod数据保持。只读核对当前实际OMSIR适配器`OmsIrSubmissionContext.createBmsResult()`仍白名单提取原10个最终规则/计分字段，不提交reading_settings；后端DTO严格额外字段拒绝未改。旧待交body/UUID不重写，本地PB比较不代替IR受理条件；未连接真实账号或验收当前服务上传。

初次编译修正了IBindable静态接口不能直接BindTo、测试raw插值括号、受保护Drawable.IsDisposed及ThreadSafety setter不可写。首轮focused因夹具未释放其自建compatibility owner、迁移测试在非update线程构造Realm配置、旧ScoreInfo未设BMS身份及过紧的固定Floating误差断言失败并主动停止；保留原日志/停止回执。现分别使用真实update步骤、显式释放fixture-owned owner、真实Ruleset身份及由0.01速度精度推导的误差界，未放宽生产线程或生命周期合同。另有源码审查发现的opacity清目标问题已修并补专用回归。

第一轮定向173项通过后，完整BMS回归3159通过/1失败/19跳过，唯一失败为opacity编辑未同步到当前局。检查实际框架IL确认GetBindable已经返回bound copy，再取一次GetBoundCopy留下弱引用中间层，GC后回调链断开。补强制回收步骤后两个相关用例修前均失败；去除第二次复制、直接持有首个副本后均通过。没有用轮询/重绑或放宽断言掩盖寿命问题，修前/后TRX为`reading-watch-before-r1 / after-r1`。Desktop Release/Debug及最终format verify通过；原生首次取景回执缺少ExitCode不计为通过，修正测试进程句柄保存后以独立r2重跑，不改旧回执。初始和中间失败不得混记为下方最终通过，软件/隔离结果不代签用户正常窗口、真实歌曲或实体设备。

最终BMS完整回归3160通过/19跳过/0失败，日志及TRX为`tests/bms-full-final-r2`，12分57秒；最终源码Desktop Release/Debug分别取`tests/build-desktop-release-r2 / debug-r2`，格式验证为`format-verify-final-r1`。同一原OSK三组正常Player全部exit0：`native-p1-both-lift-r2 / p2-hidden-r2 / dp-lift-r2`，各13张PNG，共39张，含每组8个控制阶段实景/读数及5张自然时钟画面。校验原生/作者同源的Start状态、模式、甩盘、双击组合、Lift与白绿字，实际selected/player/renderer Mods均无三项旧挡板；汇总为`native-summary-r2.json`，程序集身份为`current-binaries-reading-r2.json`。已人工检查P1恢复双挡板、P2下挡板和DP读数/面板图，均维持原包布局。原测试宿主活动状态显式模拟，输入为软件动作，不代签真实前台或控制器。

上述结果完成本次两项软件实施和同包复核；只改BMS专属链路，没有重跑或重记旧Core/mania全量、原作者CLI/美术保全结论，不生成新包。正常用户窗口、完整真实歌曲及实体设备仍沿原人工门，进一步显示偏好迁移继续属于待判决设计。

## 2026-10-27Omni持续完善读谱回放与结果页

本goal从干净且已fetch一致的`master@a7071d6c48532cb9753b086ed41a375464aa2921`继续，作品名`Lighting Model Skin Omni Until 33`和`.vyymmddxx.osk`策略保持；源与证据收在统一父目录内的`evidence/omni-completion-20261027-r1`。软件整合与v39作者检查已过，当前私下交付仍为`.v26102701`；新OSK仅为同包原生验收候选，尚未替换正式取用版本，未生成Windows发行物。

BMS新增本局13字段读谱变化时间线，按真实游玩时刻记录成组结果，同刻归并末值，回放与seek/reset恢复且不写个人偏好；旧初始快照和旧Mod读取保持。移除早于三种Hi-Speed设计的290～7692ms截断，读数由实际TimeRange回推；满Lift正分母与零读谱区分开。SCROLL/STOP读数、长STOP下的参考基准和Floating重锚已沿实际选中的原生算法补修，短STOP硬回退、Off既有算法及零/负SCROLL边界保持，不据此宣称Off/SCROLL全语义或完整变速谱已实机对齐。元数据不改变规则版本、旧ModsJson、累计统计或OMSIR白名单body。

首轮原版独立副本留下249张实际EXTRA STAGE/Chewingood!!!/BEGINNER Lv.1图，后来的DEMONSTRATION明确排除。采样循环无游玩输入、held为空，但启动前有一次P1 1操作，不能称为物理输入全无或精确命中校准。SLOW可与自然漏音POOR同显，POOR闪烁不代表SLOW结束；14条轨迹与slot3曲中片段相合，另8条未闭合，不据153notes/1263ms候选安排确定输入。Position A合并提示的P1横向约16px差异仍是候选，不推广到P2。该次副本已官方exit退出并核对原件元数据及七项关键摘要；不适用日志参数的首轮启动失败仍保留。

普通可操作键/皿音符自然超时新增独立SLOW反馈，不伪造grade、offset或HasPlayerTiming，累计FAST/SLOW仍不计Miss；自动成功、强制Miss、长条尾/tick与完整Autoplay不扩大资格。Core只承接规则集明确的显示事实，group/lane、epoch/reset和完整快照保持。

父v36经可逆精简形成`omni-full-v37-compact-r1`：只缩短私有typed scene ID及未被INI/preview/C4引用的物理路径，资源、字体、材质、选项身份和动画采样保持。投影scene523741→513629、manifest65051→57531；7071源文件映射到6597唯一目标，474条路径减少来自相同字节配置目标合并，非删除美术或功能。14项工具回归通过，旧作者/OSK及反向证明保全。

33主屏六包独立复制核SHA后选择性解出2690纹理，raw/XML/UV/AFP/GEO保持，未用副屏result素材。`omni-full-v38-results-r1`使用16张原图、39元素、21项真实文本来源；声明7,258字节，图片4,528,712字节/9,593,352像素，父v37的6597项gameplay闭包逐字节保持。可选`skin-pages.json`与图像走同一revision/capsule和公开CLI，默认原结果页、显式切换，共用实际成绩和原操作。Clear/Failed只取规则集确认的`score.cleared`，BMS未知值中性显示；`score.passed`不冒充Clear，EX标签及判定名称由玩法提供，DJ/Gauge沿真实native-statistics。版式、静态层序与乘色是OMS首版适配，未签原版AFP布局/动画。现用Python重包入口已纳入页面闭包，不能漏装JSON/图。

结果页早期失败抓到每次prepare过早解绑、页面未沿同一retained capsule读取，以及Realm只有`skin-pages.json`时绕过author prepare；均修生产根因，保留严格reader断言。测试另修真实read reservation释放顺序、framework取消返回cleanup continuation语义，以及BMS夹具混淆RawTime/TimeOffset和BGM事件的假设。`core-focused-r6`154通过、`bms-focused-r2/r3`各183通过属于当时构建，失败日志保留，不代签后续修正。

`python-api-package-results-r3`57项通过，其中ReferenceApi31项覆盖分块/连续收发与异常。ARC4改为每连接一个C实现对象、双向连续共享流，旧generator保留给既有退出工具；吞吐不冒充原机帧时。五组实际CLI图片夹具确认page与gameplay独立预算：page1M可与gameplay64M共存；共享/INI及未知旧PNG仍计原预算，page17M拒绝，未放宽旧包上限。

整合作者为`omni-full-v39-song-select-r1`：五张已核主屏原图装饰真实原生选曲控件，默认原界面、页脚切换；不用MUSIC/ARTIST等`*_usr`占位冒充元数据，不重造列表、动作或成绩。父v38生成前后6614项闭包摘要一致；只追加页声明与五张PNG，两页共21资源、9,775,852像素、4,564,295字节，声明7,815字节，gameplay/INI/预览/C4与Results语义保持。合同接受Results/SongSelect任一或两页，共守原总预算；活跃非空选曲外观显式阻塞C2，先清可见及池化行引用再释放owner，不借空布局receipt放行。

软件分项`core-pages-combined-r9`176通过、`bms-reading-combined-r8`269通过及`python-combined-r4`62通过。转谱夹具曾只证mania/7、未锁定BMS源，并修改了被overlay替换的旧Random实例；现经真实PresentBeatmap、核对source/ID并操作原面板，4K目标未放宽。Results坏gameplay脚本回归保留精确错误提示、原页与操作。这些分项不冒充完整Core或用户窗口验收。

本轮r13完整回归使用`combined-r12`构建：BMS共3272项，3253通过/19跳过/0失败；mania共997项，995通过/2跳过/0失败，分别见`tests/bms-full-combined-r13`与`tests/mania-full-combined-r13`日志、TRX和exit回执。r12的Release Desktop/Core/BMS/mania/作者及Debug Desktop已编译；其后r14改动不能由r12结果代签。

Core broad r1的27个失败已与同一`a707`只读baseline逐身份对照。旧夹具引用已删除的Osu/Taiko/Catch，或依赖旧在线按钮、共享metadata假设，已改用真实mania/BMS输入；另修共用Carousel展开组时覆盖collapsed set子难度可见性的实际问题。r12受影响76项为75通过/1失败；最后一项违背BMS默认root下Carousel无当前选项、全局保留mania原曲的合同，未修改production迎合旧自动fallback。夹具现精确断言默认root，再显式选BMS并检验mania的GroupMode.None合法跨set fallback。

新SongSelect page host另补真实兼容前提：当前曲不满足`AllowGameplayWithRuleset(..., true)`时使用mode-default，不从保留的mania7误取BMS7；真实BMS→mania7及Key4仍按实际转换键数解析。`combined-r14` Core Release与Desktop Release/Debug均通过；`core-select-pages-results-r14`为292通过/1失败，新scope回归通过，失败仍为旧TestRulesetChange。

按冻结源码SHA还原行号后，确认r12的78行与r14的79行都指第一次mixed set的mania→BMS，先前口头定位为mania-only不准确；没有由此证明生产竞态。BMS根分组不因同set有另一张BMS难度而例外。夹具改为两次切BMS均强断言root，再显式选mixed BMS→mania的真实None分组检验同set推荐；bmsOnly→mania仍检验跨set。开头等待实际CarouselItemsPresented，而非仅后台筛选任务完成。`core-release-combined-r15`通过，`core-select-pages-results-r15`最终293通过/0失败/0跳过，覆盖全SongSelect及Results/页面；未将此子集称为完整Core全量。

`author-v38-pack-r1`因v39整合候选取代，在六类检查后主动停止，未创建archive；`-superseded.json`与原退出码保留，不是完整通过或作者错误。`author-v39-pack-r1`最终exit0，公开CLI全部13,612种代表组合与条件分支通过（非全部排列），普通导入器路径检查后生成`assets/omni-v39-checked-r1.osk`，335,375,466字节，SHA256 `3a297cde6a84afbe4e13ba7d47a8ccc3be39fb241611739ee0eee5d007615977`。独立`omni-v39-archive-proof-r1.json`再核6621个ZIP实体条目全CRC、无大小写重名与路径越界、全部作者文件逐字节一致；其中6619为包payload，另两项为generation/audit元数据。

原生Results初次r1在启动时因私有Program从OsuGameTestScene所在production程序集找ExactVisualTestGame而TypeLoad失败，改为明确载入当前osu.Game.Tests；r2普通谱面注册成功但Player拒绝空谱，原因是私有TestOsuGame漏接Desktop的BmsBeatmapLoader，BMS文件落到通用decoder。三份私有夹具现按真实Desktop接同一loader，并在进Player前异步核原WorkingBeatmap解码类型、元数据、音符/事件且保存JSON；没有改production、延长超时或构造替代WorkingBeatmap。旧退出回执保留，Results r3正在运行，尚无本轮最终包PNG通过结论。

三份私有宿主当前重编分别为Results r5、SongSelect r3、Reading r2。Reading首次prepare明确拒绝旧依赖图：当前Mania.Tests的六条SharpGen依赖边与旧template不同，并非文件丢失。新宿主实际只引用Core.Tests与production owner，现按当前Core.Tests与generated host构成228项闭包，逐项核实际资产与源/目标SHA；被排除的两个ruleset Tests图及差异留receipt，不混合冲突版本。`native-reading-prepare-r2`通过，仅准备643.2MiB程序资产，无旧data复制或game执行，不是原生验收。

原版r3现已运行独立副本，实际场景为190 BPM、NORMAL Lv.4、CLASSIC OFF与Display OFF：1～18档稳定可读，19档淡出但可读、20档黑屏，不能签收完整20档；130 BPM尚未补采。正式HDD本goal始终未运行或改写；r3副本仍运行，正常退出及最终保全核对待做。先前离线would-plan不算真实输入或档位资格。

Results r3以真实Generated Autoplay自然完成、两次真实按钮切换、三张PNG及host/OS exit0通过行为断言，但人工审图发现作者将MISS COUNT原图误认作MAX COMBO：实际128确为Max Combo，数据未算错，画面不签收。新v40撤去该图，公共结果数据提供准确的`score.accuracy.label`与`score.max-combo.label`，分别显示ACCURACY/MAX COMBO；不从ComboBreak猜原版MISS COUNT。CLEAR/FAILED改用已核主屏英文原图，原kana纹理上的框形濁点本就存在于source raw裁边与sprite，并非字体缺字。v40保留6597项gameplay逐字节及SongSelect声明原value字节、五张图；两页共20资源、4,653,273字节、9,837,048像素，JSON7943字节。新作者与所有差异/15图原始RGBA裁边证明取`assets/v40-results-labels-r1`，标准`author-v40-pack-r1`仍在检查，未签新OSK。

标签修正后Core Release、Desktop Release/Debug与作者CLI重新编译通过，`core-select-pages-results-r16`293通过；Python当前整目录discover `python-combined-r5`363通过，包含替换Results保留其它页和拒绝误删共享资源的回归。v39选曲原生r1因夹具仅更新global WorkingBeatmap而未显式打开具体BMS谱，在默认root无谱卡处超时；已补真实PresentBeatmap入口与初始状态记录，未改BMS root合同。Reading v39 SP首轮真实解码、按住Start画面及读数通过，在释放Start后因夹具无条件要求绿字可见而失败；保存帧的时钟仅晚1.8ms、数值仍正确，已改核可见性等于真实Adjusting，回放不制造Start，其他时间/数值断言保持，待重跑。

剩余门为v40作者完整检查/OSK、最终包选曲与Results原生画面、多BPM/SCROLL与Off下STOP实测及真实回放、原版精确提示/动画与真实歌曲/设备。未解包IFS不计已分类，26及更早不借资源标签补造；更多显示偏好的保存范围沿已采纳合同，不静默重定义。

## 2026-10-28Classic11局内选择与原图面板准备

本节只记录本轮Classic增量；当前状态、档表和后续门分别取[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)、[稳定合同](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#既有产品面硬约束未被-skin-v1-取代)与[PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md#iidx-历代皮肤作品)，完整命令/失败身份取[当次日志](../subline/P1-A/CHANGELOG.md#2026-10-28classic11局内选择与原图面板准备)。

Classic局内/READY已按十一档执行方向相邻选择，旧offgrid无请求方向邻档时不反向钳制；一次多步只发布并记忆最终成组状态。Normal/Floating、设置滑条、Classic合法存档/Replay范围0.5～10、precision/refloat、13字段和calculation0保持。原数字整条90×394、专用cursor150×70、37px档间及原Classic NOTES TIME/GN anchor由现有作者能力直接接入；底板/竖排CLASSIC原图保留，不再用字体拼数字或Normal cursor冒充。

r28行为与构建通过，随后只改十一档/模式返回说明，r29最终编译通过；已测Classic逻辑和测试字节保持。Python r13已过，真实r12的私有receipt schema误读原失败保留、源PNG哈希/尺寸/nearest检查未减；r14已成功生成`omni-full-v46-classic-original-r1`（6619物理/6617 runtime、新增2PNG），父作者、非Classic、539预览和15 Frame保全，证据取`assets/v46-classic-original-r1`。

首个r1候选因真实DP曲名/作者与面板重叠拒交，结构通过未代签视觉。r2恢复33 DP原锚点，354px有界省略和现字体明确为OMS适配；原图十一档、正式包、真实操作/OSR和独立DP长文字于`.v26102802`交付，来源f6997e96。后续`.v26102900`补其它Frame原Header、27双侧残字与BPL DP宽度；当前范围及身份取[P1-A状态](../subline/P1-A/DEVELOPMENT_STATUS.md)和[最新交付日志](../subline/P1-A/CHANGELOG.md#2026-10-29omni原header与双侧修正交付)，旧证据不重计。原版r5 NORMAL20档已齐，r6/r7未补成DP/FHS资格；不把当前面板、静态读数或截取ROI当完整原数学/时序、原字体或设备验收。33 DP及原版校准沿PLAN，选曲/结算扩展保持取消。
