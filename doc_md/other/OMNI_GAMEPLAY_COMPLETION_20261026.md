# Omni gameplay 增量与未使用素材审计（2026-10-26）

10月26日首轮基线为干净 `master@008f7a4998cd6e332f452519bd1719daed258910`，fetch后与origin一致。工作范围为客户端只读显示、独立作者包和原版参照，不涉及网络端点或正式玩家数据。当前交付与剩余门由[P1-A状态](../subline/P1-A/DEVELOPMENT_STATUS.md)维护；此前Start、挡板、LIFT和甩盘证据仍取[原记录](OMNI_GAMEPLAY_CONTROLS_20261025.md)。

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
