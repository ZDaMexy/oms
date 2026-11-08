# beatoraja IIDX 双作品适配记录

本页记录2026-11-07开始的独立宿主工作；截至11月8日，Standard的22来源增量与Lightning首版均已安装、保全并交付ZIP，当前两者各为`v26110800`。不提升OMS旧包、来源原软件或发行验收。调度取[P1-A维护计划](../subline/P1-A/IIDX_SKIN_MAINTENANCE.md)，状态取[STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md#iidx-作品与平台维护状态)。

21原版增量已按用户要求暂停，尚未安装或交付新版本；冻结来源、被替代候选、重新建立玩家基线及后续验收取[唯一续接记录](STANDARD_MODEL_SKIN_20261105.md#2026-11-08-暂停与续接)。

## 授权、名称与来源

用户本轮明确要求调研并制作Standard与Lightning的beatoraja皮肤，更新其实际播放器，保全玩家数据，推进可验证产物。基础名称仍为`Standard Model Skin Omni Until 26`与`Lightning Model Skin Omni Until 33`。Lightning在OMS的停止自动推进不变；本轮授权是新的beatoraja消费者，不能据此恢复OMS旧缺口队列。

首版两族各自只读消费冻结作者：Standard使用2026-11-07的`author-r8`；Lightning使用2026-10-29交付对应的`omni-full-v52-current-hispeed-padding-r1`，不是较早的`delivery`目录。同名素材不跨族替换，尺寸、框体和软件机能不强求相等。Standard来源范围沿[来源现状](STANDARD_MODEL_SKIN_20261105.md#本地来源现状)，Lightning沿[作品记录](OMNI_GAMEPLAY_COMPLETION_20261026.md)。

首版原始素材留在`E:/OMS-Development/sources/`各作品原位，新的原生试产在`OMS-Beatoraja-20261107/<family>/`区分；后续临时构建、逐帧烘焙在本仓`.dev-cache/temp/`，真实运行证据在`artifacts/beatoraja-capture/`。只有候选闭合后才复制版本化交付，历史OMS包不迁移或覆盖。精确私有清单、素材、玩家信息和截图不进Git。优先CLI/API与程序内真实framebuffer；本轮用户仅允许必要时以computer use解决操作阻塞，目前未使用。

首版完整动画队列与最终原生构建包按[开发磁盘约束](../../AGENTS.md#开发磁盘约束)的本轮例外写`D:/OMS-Development/sources/OMS-Beatoraja-Bakes-20261107/`，保留空间门和独立输出验证；原E盘作者与已完成F盘证据不迁移，不为此复制HDD或玩家安装。交付只在F盘保存版本化ZIP、说明与校验，避免再存一份解包目录。

## 官方0.8.9更新与数据保全

官方版本取[0.8.9发布](https://github.com/exch-bms2/beatoraja/releases/tag/0.8.9)及[官方分发页](https://www.mocha-repository.info/download.php)。研究源码固定标签提交`83574048ba489082eb46df7fe9806b464bf1f526`；执行验证使用实际官方发行JAR。源码标签与发行包的默认skin路径有差异，不能假定两者完全同一打包布局。

已将用户的`D:/beatoraja0.8.8-jre-win64`原目录同卷改名为`D:/beatoraja`，目录文件ID一致，没有复制完整安装或曲库。使用官方差分包的10个程序/IR/native文件；原启动器、JavaFX 21运行时、曲库、player、现有skin、表与其它工具保留。旧minir-600移至`ir_backup/`并仅调整Endless Dream启动器使其继续引用旧插件；新beatoraja引用minir-700。未改用户当前选择的skin，未启动真实玩家环境执行数据库升级。

升级前22项关键文件已逐个备份并核SHA256。路径迁移只改3个JSON中的7个值及`songdata.db`的一条根目录路径；8个数据库13张表逐表核对类型/行内容与schema，其它数据保持一致，原59974条谱面相对路径与成绩不改。3个开始菜单快捷方式已修复。失败回滚在合成目录验证后才执行实际迁移；备份和完整回执位于`E:/OMS-Development/evidence/beatoraja-upgrade-20261107/`，其中`upgrade-complete.json`是更新完成入口。下载SHA为本地完整性证据，不能冒充发布者签名。

后续只读复核的22项当前文件及22份原备份、8个DB、11个有效谱库根和10个官方文件均通过，未出现额外DB或journal。新皮肤安装前另建立现有skin的11449文件基线，用于本轮安装后逐字节比较；没有升级前全量skin摘要，不能倒推该范围。回执为`artifacts/beatoraja-capture/player-preservation-check-20261107.json`。

## 宿主研究与实现边界

原生格式取官方[JsonSkin](https://github.com/exch-bms2/beatoraja/blob/0.8.9/src/bms/player/beatoraja/skin/json/JsonSkin.java)、[Lua状态API](https://github.com/exch-bms2/beatoraja/blob/0.8.9/docs/lua-main-state-api.md)与同标签loader/render源码。

- `.luaskin`的header和body分别执行；读取原生option并只加载本次所选模式/主题的资源。作者catalog的variant使用第一个匹配分支，DP不携带SP专用layout选项；不得把同名animation ID当跨manifest资源身份。
- 当前适配器提供独立5KEYS、7KEYS与14KEYS入口，5KEYS沿冻结作者实际六路布局与材料另验；P2使用靠近皿的五键位置，保留原框体美术。官方按精确SkinType选择JSON/Lua皮肤，将7/14加入5/10菜单的兼容逻辑只属于LR2分支；没有10KEYS作者声明或本包入口。
- notes、LN/CN/HCN、判定、Combo、Gauge、BGA与失败/FC时机由beatoraja实际状态驱动。SP2仍是第一玩家；DP才使用第二组timer。新长条字段明确映射，不沿用易混淆的旧active/damage命名。
- 两族普通note和长条首尾高度不同；原生共享lane高度，导出用同族透明顶补、底对齐保留原像素。IIDX没有mine素材，BMS补显眼的红白危险条并登记为宿主新增，不谎称街机原图。
- Gauge原作是整条图与裁切，不能猜成50格。Graph采用beatoraja当前Target与实际EX/PB；无法区分无PB与真实零分时保留原生零值，不伪造镜像成绩。宿主Fast/Slow统计与OMS语义不同，分别记录。
- 原生Judge始终保留每stage的实际状态所有权，外观OFF不删除owner。Lightning的A/B/C使用原FAST/SLOW图和实际region的提前/延后资格，显示500ms；源没有毫秒数字美术，键/皿独立历史也不能由525/526/527三个Judge region伪造。Course Gauge暂取同族HARD/EX-HARD外观并保留真实值；原1000+ Combo宽度适配缺少公开的命中时course-combo，不能用未实现的104或最大Combo顶替。
- 原转盘按beatoraja实际scratch offset运动，含宿主自动转动和输入加速；静态几何按各自来源。Standard 25极小仿射偏斜在原纹理像素中心反向采样，其它分代不套用该修正；整数destination带来的小数几何差异另记，不称GPU像素全等价。
- Standard中的26 ARC SCORE SPADA（iidx21）的28个键灯另有真实固定仿射quad，按各自源顶点在目标像素中心反采样并保留越界部分。读数也沿该复刻框体实际473高/top11作者轨道，不能误套26软件表的484跨度，也不能将此身份误认作21原程序的source21-gmframe0。两族白/绿读数使用公开Sudden/Lift/Hidden和duration；Standard固定clip下的Lift/Hidden位置有公开千分位量化，Lightning沿原生offset；不造Normal/Floating/Classic模式映射。
- Standard的原Gauge增长和Graph越线影片由只读CustomTimer观察实际Gauge/EX变化。正PB/Target严格超过，A/AA/AAA取ceil阈值且至少达到；初始快照、重试、回退及阈值变化只设基线。这里记录render观察时刻，不等同OMS事件时间；零PB不能伪造一次历史越线。Lightning冻结作者没有这两类挂载，不跨族补入。
- Lightning原血条边界的双clip、P2两次180°和独立透明度脉冲分别适配。官方SkinObject将零面积clip视作无裁切，故极低正Gauge必须按实际signed毫秒截断阻止elapsed-zero露整图。HD/FULLHD原生CPU探针测得边缘误差不超过0.5源像素，脉冲alpha不超过1字节差；这不是GPU逐像素等价。
- 字体按原glyph矩形和advance转换；原生字号、裁切及采样差异逐项登记。bitmap字体强制线性过滤、原生truncate与OMS ellipsis等不宣称像素等价。
- AFP记录动画包含UV网格、camera、mask及colourMatrix，不能当普通PNG帧直接读。专用GPU宿主调用现有生产解码/evaluator/drawable/shader，按原层序保留normal与additive段，并在黑/白/彩底验证复合。该宿主实际后端是Veldrid/D3D11；beatoraja的OpenGL显示仍需独立取景。
- 烘焙atlas保留源帧索引、fps、loopStart、heldLoop/release及空终止帧，裁边/精确去重/分块不降帧、不缩图。选中结果逐个核来源文件摘要；事件类不接受partial静止采样，框体固定clock使用源声明的准确帧。PNG压缩大小不代表显存。
- 动画挡板使用单texture规则grid及只读TimerProperty选格，保留原生Slider/HiddenCover/LiftCover的移动和裁切；Graph Area共享同族像素。组合成本按该载体另算，不能沿用ImageSet或旧静态裁片排序。部分Lift+Hidden组合会把边缘读数放入原静态Judge区域并被短暂遮挡；保留宿主真实边缘和原判定层级，不宣称同白数时位置与OMS相同。

实现入口为[素材工具](../../tools/IidxSkinAssets/README.md)、[GPU烘焙工具](../../tools/IidxSkinBake/README.md)和[原生取景工具](../../tools/BeatorajaSkinCapture/README.md)。没有移植OMS运行时，也不改beatoraja判定、计分或用户的客户端JAR。

## 22 来源 Standard 增量交付

2026-11-08 Standard已交付并安装`v26110800`。源作者为D盘22来源专用父目录的`author-r5`，生成与适配源码冻结于`9fd03c8e`；新原生包为`beatoraja-packages-r4/standard`。新增PENDUAL PRESENT/FUTURE和三款22 CUSTOM，与旧十二款合计17主题；完整来源与非连续代数边界取[Standard记录](STANDARD_MODEL_SKIN_20261105.md#2026-11-08-22-来源接入)。Lightning及旧Standard保持原包、原验收日期，未借新增主题刷新结论。

完整包11863文件、3874 PNG、2,319,662,722字节；17主题×5布局共85，官方发现只识别3个Lua入口，258次CPU加载、7947片段通过。1443条完整源动画资格全部闭合，无待烘焙项；最终构建严格复用已核完整帧，不把静态抽样当完整电影。CPU与冻结清单取专用父目录`beatoraja-work-r3/native-build-r4/`。

同一最终包完成22场官方原生Replay：新五主题各SP1/SP2/DP、5KEYS两侧、HCN真实松开550ms后的active→damage→active恢复、自然HARD失败、DP FAST/SLOW及旧26/23回归。1646张PNG完整性与正常dispose门通过，直接审看72张不同原图，未确认尚待修复的视觉缺陷。原22失败影片已实际播放并退场，随后进入宿主默认MusicResult；未制作IIDX选曲/Results。记录在`artifacts/beatoraja-capture/standard22-r1/runs/native-r1/completed.json`与`delivery-r1/visual-review.json`，保持单族`globalComplete=false`；不把代码中的范围完成标记推广到所有主题配置、物理输入、真实整曲/视频BGA、持续性能或真实玩家存分/IR。

本包保留明确适配：c22的原CN图/电影供宿主HCN状态使用，不伪称原22具备HCN；BPM范围底板恒显，未复刻原版恒定BPM时隐藏及DP父容器入场。`l22-058/068`原文字和`l22-087`浅色图降低Lift白读数对比，仍保留原图、颜色与锚点；原DP MAXCOMBO标签与Graph边缘交叠已有原版参照。命中边界的HUD/Graph可能暂时不同步，已在后续稳定样本收敛，不能把单帧当原子计分快照。完整Start面板和其它宿主差异见[实现边界](#宿主研究与实现边界)。

实际安装到`D:/beatoraja/skin/Standard Model Skin Omni Until 26.v26110800`，未改变当前选择。源包、暂存和安装结果三次完整清单相同；另以本次新基线核对38项受保护文件、10个DB、10个官方文件和46499个原有skin文件及1673个目录，字节、路径保持，唯一新增为本版11863文件。未读取SQL行或改玩家数据，不能用旧升级前22文件/8DB基线代签用户期间的活动。安装回执在`delivery-r1/installation.v26110800.json`，保全在`artifacts/standard-model-22-20261108/player-preservation-r1/after-install-r1.json`。

版本化ZIP与外部说明、校验和验证记录位于总工作区`deliverables/skins/beatoraja/Standard Model Skin Omni Until 26.v26110800/`，全部11863原生成员逐项核长度及SHA，保留VL Gothic许可和`.json.txt`侧车；私有原始素材、生成包、运行证据和玩家信息不入Git。外部README另附本次实绘发现，冻结的包内README及安装字节不改。

## 本轮同包验收

以下保留两族首版及其合并验收历史；Standard当前增量取[新交付](#22-来源-standard-增量交付)，Lightning仍沿本节首版结果。

两族独立交付：Standard为`v26110700`，Lightning为`v26110800`。Standard来源解析与导出工具冻结于`36c90ab`，Lightning冻结于`ded2df3c`，各自精确源码快照保存在构建证据中；后续安装器优化和文档整理不更换包身份。构建源分别位于D盘约定父目录的`packages-r1/standard`与`packages-r2/lightning`，不是早期组件切片。

Standard全部12款Frame、5KEYS/7KEYS两侧与14KEYS DP已生成；实际菜单保留各族来源支持的组件和13项局部控制，没有临时缺烘焙项。官方发现只识别三个Lua入口，全部6707片段、230次装配及60个Frame×布局解析通过。文件清单、组件数量与构建原日志保全于`artifacts/beatoraja-capture/final-build-r1/standard/`。构建监测脚本曾把UTC时间解析成本地时间，旧内存记录只有启动采样，不能宣称整次峰值；限制说明与修正前后脚本均保留。

同一完整Standard包的21场官方Replay覆盖12主题P1、P2/DP、5KEYS两侧、HCN断持恢复、Timing与Judge OFF、FAILED、控制端点及最大默认DP开场/FC负载；1645张PNG与身份、状态、原生读数和正常退出均通过。人工审看21张联系图的282个缩略图，另独立打开31张不同原图（两位审看者合计32次），未确认缺图、持续遮挡或FC/失败退场残留。完整性不等于逐张目视；原生Replay也不代签物理输入、完整歌曲、持续帧率、视频BGA或真实玩家存分/IR。

Standard证据在`artifacts/beatoraja-capture/final-matrix-r1/`的`standard-plan.json`、`standard-integrity.json`与`standard-visual-review.json`；它们仍限定单族、保留`globalComplete=false`。11月8日合并矩阵逐项核对Standard包及21场定义未变，才复用其原证据，未重跑或刷新其日期。联系图索引保留生成时“待审看”状态，实际审看以独立回执为准。

Standard实际安装目录为`D:/beatoraja/skin/Standard Model Skin Omni Until 26.v26110700`，原选择不变。源包、安装暂存和安装结果三次全清单核对一致；安装后22项关键文件、8个DB、10个官方文件及原11449个skin文件通过保全检查。安装证据为`artifacts/beatoraja-capture/final-install-r1/standard.v26110700.json`，后保全回执为同父证据区的`player-preservation-check-20261107-105736673390.json`。版本化ZIP位于`deliverables/skins/beatoraja/`的同名版本目录，9997个成员逐个核长度与摘要，附使用说明、许可、校验和独立Standard验证记录；这份历史交付保持原字节及单族范围。

Lightning完整包包含15款Frame及15项局部控制，官方三入口发现、11978片段、305次装配和75个Frame×布局通过，未有临时缺烘焙项。实际发现加header约852ms，只是扫描探针，不是完整设置页打开时间。两族132个FC身份、330个开场身份以及Lightning全部21支动画挡板程序（20选择、42个Cover/Lift身份）的完整来源门闭合；新grid资格与原像素、帧及选择保持一致。构建和25个适配器原字节保全于`artifacts/beatoraja-capture/final-build-r2/lightning/`，不重新生成已交付Standard。

完整Lightning导出约100.7分钟，Python的OS PeakWS为1.821GiB，Java为477.4MiB；10秒采样树高水位约909.5MiB，最低采样可用物理内存约812.4MiB。采样不能覆盖所有瞬间，不能用此前只读索引资格的274.52MiB代称完整构建峰值。最终包25053文件、13049 PNG，约6.854GiB；安装侧车254.62MiB，故安装器改为短命Python读取名称/族别后退出，再做原三次全清单验证。Python仍完整解析JSON，不宣称流式或内存上限；5项身份读取、实际大侧车、12项安装边界及独立审查取`install-metadata-r1/`。

同一完整Lightning包的23场官方Replay覆盖15主题P1、P2/DP、5KEYS两侧、Hard HCN断持恢复、提前/延后Timing、Judge OFF、百分比目标与控制端点、自然FAILED及较大动画负载。1340张PNG的身份、状态、读数及正常退出门通过；实际审看23张联系图的289个缩略帧，另打开45张不同原图，没有确认未接受的视觉缺陷。短时命中画面中HUD/Graph/判定统计曾有不同步读数，后续稳态收敛；它们读取宿主实时值，不能把截图当作原子计分快照。失败用例证明宿主FAILED流程，冻结Lightning作者未声明`gameplay.fail`，不冒称原Stage Failed影片。

最终合并`matrix.json`、`integrity.json`和`visual-review.json`位于`final-matrix-r1/`；完整性与目视回执为双族`globalComplete=true`，共44场、2985张PNG完整性、44联系图/571缩略帧/76张不同原图（77次打开）。Lightning单族回执为`lightning-visual-review.json`，独立身份审查为`final-evidence-review.json`。这些“完整”只针对声明的矩阵；不表示全部配置排列、物理输入、持续性能、真实歌曲/视频BGA或真实玩家保存/IR已验。

Lightning安装为`D:/beatoraja/skin/Lightning Model Skin Omni Until 33.v26110800`；源、暂存、安装三次逐文件核验一致，原选择不改。安装后保全同样通过22项关键文件、8个DB、10个官方文件及原11449个skin文件；回执为`final-install-r2/lightning.v26110800.json`与`player-preservation-check-20261107-182643223393.json`（文件名沿UTC，实际本地日期11月8日）。ZIP为6652403960字节，25053个原生包成员加1份同级运行说明，全部25054成员逐项核长度和SHA；使用说明、许可、校验及验证记录齐全。执行脚本、三次清单和证据引用保全于`final-install-r2/lightning-proof/`。

安装用三份临时验包清单已逐字节保全，但随后删除原临时副本的PowerShell命令被自动审批以`blocked by policy`拒绝，未执行或换方式重试；约16.90MiB临时文件仍保留。具体目标与只读复核取`final-install-r2/install-temp-cleanup.json`，不能将其记为已回收，不影响已完成的安装与交付。

### Lightning资源用量与验收边界

固定BPL tradz DP/fc22，对全部20款动画挡板及基线做官方CPU实装，同款同时用于Lane Cover/Lift Art/Graph Area；21个组合无重复路径source ID。最大为l312，625 PNG、逻辑RGBA8共6756511704字节（约6.292GiB）。四个原生载体共用314张grid图，不重复加载同路径；这是包含其它包内图片的逻辑成本，不含字体、BGA、驱动等，不等于实际显存峰值。21组由同一个`-Xmx1G` Java进程依次执行，各组重建Globals/accessor/Skin，不能称逐组新JVM。

同一固定其它条件的1540种三选择集合投影，最大l312+l335+l336为949 PNG、11469041016字节（约10.681GiB）；没有实际混选装配或GPU验收。原生实装与集合投影分别保存在`cover-grid-r1/footprint-r1/full-lightning-dp-r1/`和`full-lightning-dp-set-analysis-r1/`，不能用旧l341或静态载体的排序代替。

最终Lightning 23场的一秒GPU采样共821行，本机RTX 4060 Laptop GPU（报告8188MiB）总适配器已用高水位7602MiB，包含桌面和其它应用。较大负载场明确绑定bpl-tradz DP/fc22及l312三处同选，并有实绘/退场审看；观测取`final-matrix-r1/gpu-memory-r1/`，不是整44场采样、单皮肤占用、瞬态绝对峰值或FPS结论。运行说明随ZIP在原生目录之外单列，提醒不同动画混选会增加资源、同选可以共享；低显存设备、全部混选和长时体验未签收。

## 开发阶段验证（历史）

下列记录按各候选当时的覆盖与缺口保留，不能把早期未验项或小包通过直接套用为当前状态；当前同包范围取上节。

Lightning新挡板载体的早期代表候选使用`l341`，当时最终全菜单与实绘待验。原tile128素材重排为167页，实际解码约1806.51MiB；36493格RGBA、268个透明填充、481帧与41047个有序run逐项一致，严格Lift别名不新增资产。生成/核验的Windows已观测内存高水位188.61MiB，不称瞬态绝对峰值或GPU占用。官方CPU选格/显示门、包回归及真实DP开场路由通过；来源资格在D盘专用父目录的`l341-cover-grid-qualified-r1/`。

该Lightning候选修正BPL tradz DP按各自舞台选择轨道开场；同包SP1/SP2/DP三场官方Replay、183张PNG完整性通过，DP含完整大FC22及三类挡板/Graph。已目视3张联系图的42个缩略图和5张不同原图，未确认grid选格/裁切或开场分组缺陷。SP2的Lift=.1、Hidden=.15读数与活跃Judge交叠已另核源层级和宿主offset，作为上方已知限制保留。证据为`artifacts/beatoraja-capture/cover-grid-native-r1/`的`integrity.json`、`visual-review.json`与`readout-overlap-review.json`；不代签其余动画挡板、最终完整包、持续帧率或物理输入。

实际安装的官方JAR/natives/JRE已在独立QA工作目录通过SP1正常REPLAY、SP2自然失败、DP HCN及SP2 CN AUTOPLAY；配置、合成谱、Replay、player和DB全在QA目录。工具核对实际skin路径/选项，捕捉fallback，要求自然状态与正常dispose，阻止HTTP/HTTPS及真实IR。AUTOPLAY只证明自然返回选曲，正常Result由原生Replay获得；不注入成绩、Gauge或判定计时器。

两套默认SP1/SP2/DP已使用r10开发包真实加载并自然结算。七个用例包括Standard SP2失败、双方SP1 HCN断持后恢复、SP2 CN/LN与DP HCN；全部满足实际身份、配置、判定状态、退出和诊断门。HCN恢复由真实Replay松开550ms后重新按下，原生active→damage→active成立；三类挡板组合实际使用0.3/0.1/0.05。证据在`artifacts/beatoraja-capture/standard-r10/`与`lightning-r10/`，不是最终全组件包验收。

逐图审看确认SP2逻辑键与视觉位置、DP两侧、Lift下爆炸和Standard判定位置，以及原Stage Failed演出。审看同时发现Standard低于满血时百位图没有退出，90被叠成190；导出器已按源父容器的Gauge opacity track修正，在`standard-package-r1/`的HCN恢复实绘复验正确显示90。r10当时未烘焙的P2/DP FC美术不因该轮原生FC timer通过而获签，后续完整片取下方同包验证。

补充真实提前/延后50ms Replay：官方default DP确认每个Judge region都出现两方向后，Lightning当前SP1组件切片完成A、C加Judgement OFF及上移位置两场，均有原生正负50ms、非满EX的FC与MusicResult；已目视原FAST/SLOW图及OFF后提示仍有状态所有权。证据在`timing-selftest/`和`lightning-timing-r2/`。Standard的原提示实际位于frame stage.foreground，不因缺独立Timing模块而视为没有来源；其720个来源组合和Lightning315个组合已静态核对，Standard同包实绘取下方r4记录。

原生包按同族共享资源和独立组件Lua片段组合，选择项保留稳定ID；只有在所有纳入主题/模式中成立的选项才列入菜单。缺烘焙或不支持的选择留在收据，不改成其它素材。Notes/Charge Notes用同族最大首尾高度透明顶补，控制组合只枚举组件内部实际相关参数，不展开所有模块的笛卡尔积。Graph背景、Graph Area美术和前景按原层序拼接，六个模式/家族的拼接结果已与整体配方逐项一致。首个全主题开发包中，Standard 3941片段/92次代表装配、Lightning 10424片段/140次代表装配通过官方LuaJ/JsonSkin和引用闭包；它们属于旧工具快照，不能代签后续增加的动画与读数。包资产与早期11个实际场景另经`beatoraja_verify.py`核摘要、尺寸及完成门。

`integrated-r4`为每族两个真实Frame、三种模式的小包：Standard的26 ARC SCORE SPADA（iidx21）与Rootage共194片，Lightning 27/33共460片，构建前后工具SHA一致；全部片段及20次装配通过原生静态检查。八个官方播放器用例全部正常dispose：Lightning SP1/SP2/DP真实使用l344或完整双clip l345的动画挡板、Lift和Graph Area，三模式FC24全段取景；Standard SP1 A提示、DP C加Judge OFF、SP2 CN、该ARC SCORE SPADA主题DP/Full HD以及SP2失败均自然结算。Standard真实Gauge增长、A/AA/AAA及Target越线均有起点和原片取景；无PB场景没有造出MYBEST事件。

上述七个非失败用例均经公开输入接口在曲中按住START并松开，原生270门实际开/关，目视白/绿数与真实边缘；FC追加取景至4000ms，未据timer本身补签缺片。官方default的同工具自测另通过Normal Gauge、读数开关和自然Result。九个通过用例的全部PNG、入口/包身份与退出门由`integrated-r4/capture-integrity.json`复核。首次把START放到曲终尾段的工具试跑触发宿主正常提前结束，未通过读数门；该失败证据保留，已改为曲中输入，未改宿主规则。

继续逐项核配方时确认Standard的`effect.long-note`原持续光曾被收集但未消费；此前HCN恢复仅证明头身尾和原生active/damage状态，不能扩称持续光已经实现。20个消费身份/10支原影片已补齐烘焙并合入独立pass；144个真实来源组合和官方CPU状态探针通过。原transition只观察进入/离开Holding，不把未持有的HCN DAMAGE结束误作释放；保留原break循环与退出尾，逐物件同时存在的尾部及render间多次变化保持宿主边界。Lightning 7920个现有Charge/布局/控制组合未声明此slot，所有已查Frame的key-flash显式Suppress。另修复Lightning 27/28/29最低BPM的28px字形/25px advance：保留原图，以原生负space表达重叠和末字外伸。

`integrated-r6`同包四局又通过Standard 26 SP1 HCN断持恢复、SP2 CN，26 ARC SCORE SPADA（iidx21）DP Full HD HCN恢复及Lightning 27 SP2；全部正常结算，PNG/身份门通过，六张选图已目视原持续/红break/恢复光及原最低BPM。回执和逐图说明在`artifacts/beatoraja-capture/integrated-r6/`。高清密集截图会造成下一render采样延迟，所记50/200ms是请求点，真实age另载，不冒充精确帧时序。小节线、更多局部控制和亮度修正发生于该包之后，须由下一候选另验。

继续核对发现原亮度只在部分carrier生效、Lightning原小节线未被采用。现已分别接Standard lane、Lightning column/lane objects；408对真实notes/charge设置直接核29696个输出部件像素，原尺寸、alpha、透明padding保持；Judge的内部label/Combo destination分别消费亮度。小节线取Lightning本族原PNG与BMS deck marker，Standard保持明确宿主新增；81真实frame-mode经官方CPU检查108个group引用及2592次几何/颜色，取整和原arrival fade差异留在收据。b04光柱的input.pressed绑定还曾被误判为未映射，已接真实KEYON开关并透传到recorded-animation；12个实际分支经官方LuaJ检查258048次可见门、258048次选帧及1728次整帧集合，松开、按住、释放和再次按下均符合本族原17帧片。Graph、Gauge/Judge显示、爆炸大小等源控制已生成controls-r8候选，未以CPU检查代签实绘。

Lightning默认FC24做过实际官方JAR的两种无损分块比较。固定联合框方案为467个imageset组、约1041.6MiB解码；16像素对齐方案为6448组、733.42MiB。前者Play/FC后的render调用median为0.544/0.685ms、p95为0.776/0.941ms；后者对应3.684/5.176ms、4.389/5.988ms，故选择前者。实测为本机HD、合成HCN Replay、限120FPS；含宿主等待，不是纯CPU、实际FPS或手感。两次取景在`artifacts/beatoraja-capture/lightning-r7/`与`lightning-r8-union/`，完整FC原始未分块约1478.22MiB；未据此宣称全部组件或低显存设备通过。

`controls-r8`保留同工具SHA的两族候选，官方CPU通过Standard 364片/92次装配与Lightning 3098片/136次装配。六场实绘覆盖Graph关闭、Gauge B/C、Realtime Judge关闭、Judge Panel分支、亮度/爆炸大小端点、b04光柱长度、Judge关闭下FAST/SLOW及CN/HCN；Standard和Lightning各有Full HD DP。全部自然结果、PNG与退出门通过，七张选图已目视，证据在`artifacts/beatoraja-capture/controls-r8/`。Standard DP Timing此前三次失败均保留；精确同谱、同Replay字节、同选项复测在合并同render取景并加快无损PNG后达到88 Combo、0 BP。队列满时GL线程编码跨过短按是工具干扰，未改原输入或放宽结算门；62个共享画面的尺寸和SHA另核。

完整目录的控制组合估算暴露了构建体积问题：Lightning约52万候选，其中Judge位置/亮度与爆炸大小/亮度占七成。现从实际来源路径提取Judge、爆炸、光柱和Timing的独立参数，只把已逐轴核对的原生字段做运行时赋值，源分支与所有选项保留；未获资格的路径仍精确枚举。4320个真实组合、226920次原生字段及回调比较通过；候选记录收敛至115830，缓存只留片段路径和诊断。cap height扫描也只消去已证明无几何影响的RGB轴。偏移值/AUTO接真实number12与event_index75，45个源分支的符号、可见门及AUTO原图经官方CPU检查；尚待新同包实绘。

后续参数工厂只在实际选中贡献、消费几何、控制范围和默认值相同时跨Frame复用，Timing另保留Judge材料所有权；未选中的catalog分支不再强迫重复生成，真实`option.frame`依赖仍入键。7065条真实来源资格路径、30/33三模式942条对照和2106个边界组合通过；其中原有54个不可用Separate Timing组合继续阻断。其余2052个组合经官方LuaJ/JsonSkin核220268次回调，重复加载没有共享可变表。四类所需原始组合计算由104490降至13932，菜单与候选覆盖不减；不是GPU或完整包验收。原`.dev-cache/temp/beatoraja-parameter-cache-r1/`的证明、日志和复跑脚本已按摘要保留到`artifacts/beatoraja-capture/research-graph-cache-r1/parameter-cache/`。

两族准备期已按原Frame时长接公开READY/PLAY/Failed时钟，Ready仅使用最后1500ms，不延长源等待；CustomTimer每render缓存一次，准备期在首次PLAY冻结，presentation继续随实际PLAY，失败后冻结。Practice/加载/取消与缺少先前READY的晚观察明确拒绝旧epoch，不假造起点。80个官方CPU时钟场景通过。Lightning原Judge Panel位移和原0/1透明度轨道已接入，45个源分支在HD/FULLHD检查；原生整毫秒采样引入的移动误差单独登记，不能以稳态坐标相同宣称动态像素全等。

开场quad按原step帧和unit-Q四角使用生产框架的TL–BR两三角纹理映射，保留116个轻微非仿射四边形，不近似成平行四边形；目标PNG按原画布像素中心采样，整数边缘覆盖仍有宿主差异。独立有理数UV测试覆盖非仿射、反射、退化与共享对角线。两族默认SP1的六支完整录制已通过原渲染和atlas逆向核验；`entrance-default-r2`两场官方JAR均44 Combo、88 EX、0 BP，自然Result和退出。136张PNG完整性、18个准备期检查点与8张原图目视通过，Ready确实只在各自最后1500ms出现，首次PLAY在源3000/5000ms后冻结准备时钟。证据为`artifacts/beatoraja-capture/entrance-default-r2/`。其余主题完整开场继续排队，不把可用源误称缺素材。

后续查漏修复两项原来未报错的静态替代：两族歌曲进度游标接实际宿主rate6，81来源/108游标经整合核对没有静态残留或重复绘制，原PNG/RGBA保留；宿主playtime包含完成余量，不能称与OMS首末物件区间完全同义。Lightning轨道原明暗曲线接公开140节拍时钟，保留原四个alpha点，不自行积分BPM；该宿主在STOP中继续脉冲、每小节重置，失败冻结已观测相位，与OMS连续音乐拍数的区别明示。两项官方CPU通过；`entrance-default-r3`再以Standard默认SP1正常Replay与Lightning默认SP1 Timing完成自然FC/Result，143张PNG完整性和6张原图目视通过，确认两族游标实际移动及Lightning轨道明暗变化。证据在`artifacts/beatoraja-capture/entrance-default-r3/`，没有代签所有主题/配置或精确节拍相位。这两个Gameplay作者目录没有选曲/Results场景合同，不能据此声称还原这些页面，也不反推原HDD没有对应素材。

对Graph SUB的初步“第二比较柱”判断经祖先门复核撤回：原PB分支在PB不可用时才以SUB替换同一列，NO DATA也属于最大值不可用的后备显示。宿主公开值不能区分无PB与合法零分，故维持真实宿主主target与已记存在性边界，不开放假想副柱。此次更正来自源可见门和相同锚点，未改源或发布错误选项。

Graph Target Artwork进一步按实际叶子角色补齐Standard custom/NEXT/NEXT+和Lightning percentage，数值、差分、柱与名称统一读取宿主Target，百分比只显示number135的整数部分；没有开放原作百分比控制或NEXT算法。Standard NEXT/NEXT+在此合同下与AAA同美术，只保留来源选值身份；Lightning NEXT仍因PB存在性门未开放。153条实际来源分支的原图/RGBA、原柱高及最大值字体门通过，官方LuaJ检查57618次数值、67716次门和1620次HD/FULLHD柱高，动态柱误差不超过0.000207源像素。181项适配器focused通过；Frame×Play Side静态探针已补缺失的组合，现有小包14布局通过，完整包仍待验。证明、实际配方和原日志已按摘要保留到`artifacts/beatoraja-capture/research-graph-cache-r1/`的`graph/`和`frame-side/`，均不代签新增样式的GPU表现。

冻结作者的5KEYS已按实际声明补入独立type1、`play5.luaskin`及P1/P2选择；谱面模式与显示侧分别入缓存，不借7KEYS header或前六列截取。27个Frame的135条五/七键两侧及DP来源路由、300个实际Notes/Charge选择对照和187项适配器focused通过。四个默认5KEYS候选各25片，经官方LuaJ加载与完整性检查；17项合成谱/Replay/非法组合检查保持六路输入和SP2第一玩家身份。两族完整默认P2开场另按原帧烘焙，不用P1取景替代。

`five-key-r1`实际完成Standard P1 HCN断持恢复、P2 CN，Lightning P1 HCN与P2 Full HD提前/延后50ms四局，均自然MusicResult并正常退出；独立官方default5自测也通过。五局343张PNG、入口/包/源码身份、真实6路1stage与共享framebuffer完整性通过，9张原图目视确认靠皿布局、挡板读数、持续光恢复和FAST/SLOW。证据为`artifacts/beatoraja-capture/five-key-r1/capture-integrity-and-visual-review.json`，CPU原日志与回执保留在同目录`static/`。这是同一工具快照的默认主题资格，最终全菜单135布局与同包实绘仍待完成；不宣称10KEYS或物理输入验收。

安装前复核官方`SkinConfiguration.scanSkins`发现其递归收集所有`.json`，`JSONSkinLoader`会完整读取审计侧车后才因缺少skin type忽略它。新包因此改用`package-receipt.json.txt`；原生绘制与Lua不变，历史旧名只作唯一文件的只读验收，同时存在两份时拒绝，正式安装只接新名。官方JAR实际扫描已复现旧候选多读一份收据，三入口合成目录改名后只发现5/7/14三个入口；31项生成/完整性focused及12项PowerShell边界检查通过。新名合成包的实际复制、三次完整性及4份原文件保全也通过；证据在`artifacts/beatoraja-capture/package-sidecar-r1/`。最终完整包还须通过独立发现与header门，扫描耗时不等于打开设置页的整体体验。

安装签收还要求目标精确属于实际`D:/beatoraja/skin/<版本名>`，保全必须在安装结束后开始；排除的新目录须真实存在，且不是初始皮肤清单中的目录。后回执固定初始清单SHA及原tree摘要，14项隔离合成检查通过，旧11449文件基线与新校验合同相符。新侧车名下的私有脚本复验和精确快照在`artifacts/beatoraja-capture/release-safety-r2/`；这些准备检查不代表真实播放器已经安装或打包。

安装准备阶段已完成各主题稳态、Ready/已有Fail、全部普通光柱/爆炸、Lightning四档光柱长度、默认FC三模式与Standard全Gauge/Graph反馈的完整或声明固定clock烘焙；当时其它FC、开场及动画挡板仍在队列中。此处仅保留当时范围，后续完成情况取上方同包验收。真实物理输入、长期手感、视频BGA、真实玩家保存/IR、所有配置排列与选曲/Results皮肤不由这些烘焙证明代签。
