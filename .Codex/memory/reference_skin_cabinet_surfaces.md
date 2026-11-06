# 机台底板、跨拓扑 scene 与原版素材诊断

当前进度与验收只读 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)，图像及对应迭代见[原图索引](../../doc_md/other/references/simple-1p-20260912/README.md)。以下保留截图暴露的诊断因果，不把历次检查合并成当前验收。

原版Start设置须看实际普通/详细标题和当前选中栏，不能把初学者开曲确认窗、帮助页或按键后仍在SongSelect的截图算菜单。官网列五类可调亮度物体，不等于五个独立设置；实机carousel是一个共享Lane Objects Brightness选项。枚举要转到首项再次出现，数值记录初始值、边界与步长；切语言的按键须在招募页稳定出现后投递。冻结菜单倒计时的测试配置独立留证，不能代签自然开场时间。

新增游玩读数不能把`width-px/height-px`放到container或普通sprite上；它们只准用于horizontal直接sprite叶子。白绿字按完整Stage跟随真实边缘，字号/标签使用既有横排自然尺寸，不随挡板高度或opacity缩小。`lift.offset`才是已应用判定线几何，`lift.value`只是单位；上下挡板在完整CoverContainer里线性取各自单位，不能误以为LIFT会缩短其父矩形。作者结构/Python检查不能代替实际codec与Player，诊断来源取[游玩复核](../../doc_md/other/OMNI_GAMEPLAY_CONTROLS_20261025.md)。

C4的Stage Provide会继续匹配Group/Lane；`instances.material`精确匹配resource不等于只匹配一份原Stage。把原Stage Suppress替成读数Provide后，若不保留后代截断，7K一舞台会展开Stage+Group+8 lanes共10份，14K共20份；visible门不减准备储备。仅需舞台的自定义装饰应在相应Group显式Suppress，保持原后代不继承的结果。实际C3拒绝025须看runtime预留分项，不能按root声明数、CLI Select或单舞台猜DP；原例textGlyphs9073/14321，根因与修正见上述游玩复核，不靠增加8192上限通过。

RGB亮度不能写到容器的ContentDrawable.Colour后再期望作者色还在：container/mask的Content与Transform可能同一对象。独立倍率复用既有RootDrawable，作者颜色与显隐保持在内层。普通与specialised构造均须初始化elapsed轨迹默认值，否则无Start的专用池节点会缺恢复表；回归保留旧rotation轨迹以区分新属性问题，语法取[作者参考](../../skin-authoring/docs/REFERENCE.md#节点目标和属性)。

显隐与透明度合同见[作者参考](../../skin-authoring/docs/REFERENCE.md#节点目标和属性)。只断言OFF场景的root.visible=false会漏掉后续judgement.hit把同root.opacity写回1；应检查静态OFF祖先是否还被binding/track/variant/state赋值，并看真实命中后的图。独立canvas关门、root保留生命周期可解决该冲突，不能靠初始值或RGB倍率代签。

判定根的`y`按其宿主高度取相对值，不能按1080直接除；同一原字形在SD/HD既有分支中可使用不同换算，不必复制字体或增加Frame分支。RootDrawable是未移动的外层RGB载体，实际位移在TransformDrawable及原子行，取景须同时记两者和子孙屏幕几何。原版菜单缩图的位移不能代签真实游玩；四个锚点与剩余插值边界取[Start复核](../../doc_md/other/OMNI_START_OPTIONS_20261024.md)。

已发布作者包的资源路径可能是内容摘要，不能按解包前的PNG文件名后缀定位同一图。从已有只读数据绑定、分支所有权、唯一原几何和对应texture声明识别实际消费者，再保留原resource身份及父本字节；合成测试须覆盖这种路径，不仅使用易读的源文件名。具体失败取[Start复核](../../doc_md/other/OMNI_START_OPTIONS_20261024.md)。

最终冻结作者的CN/Judge本地ID可能已压缩，旧`note_geometry`按长ID重建会拒绝。仅修LIFT目录时沿[专用目录工具](../../tools/IidxSkinAssets/README.md)新建兄弟作者，不重跑旧几何变换或放宽断言；逐文件核对仅目录变化，缺省回退与已保存有效ID分开验证。Graph的合法blank、LIFT每款艺术内的黑色衬底都不能当作LIFT纯黑选项一并删除。

Group素材匹配需同时检查codec作用域准入、ResolveMaterialTemplateEntries的独立Kind.Group及准备目标映射；少一处会解码拒绝、零实例或错误目标，不能改成Stage逃过问题。选中来源revision必须取C4文档Identity.ContentRevision；C2 whole-package identity可能是`record:...`，既不是合法opaque token，也不等于选中C4声明。以真实Skin/INI/encoded scene重过准备链，stage/lane作对照，根因与实际范围取[Start复核](../../doc_md/other/OMNI_START_OPTIONS_20261024.md)。

挡板透明度若只检查保存值或旧display.Alpha，会漏掉与display并列的C5场景宿主。产品回归须沿真实作者/canonical两侧最终Sprite.DrawColourInfo检查乘积，并往返0/部分/满值；在现有共同宿主乘原生opacity，不能覆盖specialised visual的lease Alpha或作者内部Alpha。合同取[P1-A](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#声明式作者面)。

轨底独立亮度不能对整张baked框体调暗。先按原`lane_1p/lane_2p`网格与祖先重绘、重合成对照，保留`lane_base`白边/封底；同时改C4 Provide和C5资源才能退出旧整屏纹理解码。28原draw stream含base/column交错，按连续run拆分，不能按类别重排；保留UV、nearest采样、mask、Alpha、hold和sentinel。源RGBA相等仍须实际Player在默认值与最低值核边界、非column ROI，不能用离线图代签GPU，方法取[Start复核](../../doc_md/other/OMNI_START_OPTIONS_20261024.md)。

原生ModDisplay的旧自动避让只看可序列化旧HUD组件，不含C5作者根；Graph文字状态正确也可能被右上图标遮挡。用选中作者布局的可选`modsRect`投影同一个ModDisplay，不另画副本；HUD与scene canvas坐标可不同，四角转到HUD局部再等比fit，保持未声明包原位置。C5的IsLoaded不等于逐帧预算内节点已建完，实际材质断言须等同一runtime.IsSceneReady后再核完整替换和fault，不能删掉暂未就绪的槽来凑覆盖。

使用歌曲相对`timing.elapsed`的演出，起点应取`GameplayClockContainer.StartTime`；`GameplayStartTime`是可跳过前奏上界，长前奏时不是实际进入起点。显式准备演出读取Player父准备快照，不能接回停住的歌曲时钟，见[准备与首排](reference_gameplay_skin_event_envelope.md#准备时钟与首排提示)。无真实起点的预览不把elapsed=0当作开场首帧；有效→未知Reset恢复作者原属性并清除旧quad。独立time来源须单列脏状态，不让普通成绩事件多重算beat绑定。原路径证据取[接通记录](../../doc_md/other/LIGHTING_DETAILS_20261021.md)。

拆持续组件时先把完整源图重绘到与旧baked背景RGBA完全相等，再从原层次排除组件重绘；不要擦除PNG或把33的组合规则直接套BPL。BPL高清框体的旧作者背景仍走safe-BGA开口组合，DP还含固定符号层；相似画布不代表相同合成闭包。源300帧在299回跳180表示重复180～299，不是让整个300帧重播，也不能把45度采样尾段当成整圈。持续组件须脱离1000ms入场overlay、沿准备到游玩的派生时间继续；新节点`properties.z`直接加到osu Depth，较低值在前，`+1`会被同级不透明背景盖住。源/codec通过而实机缺图时先核实际draw depth与遮挡，不能因开场JSON只筛准备clock就断言新clock没激活。当前公共合同取[作者参考](../../skin-authoring/docs/REFERENCE.md#记录动画)，实际验证取P1-A状态。

原版参照须核最终日志而非只看CLI：XML已有非空选项会覆盖CLI，布尔值文本`0`仍可因非空被启用；已有服务器URL会使内置离线EA选项失效。游客Start选项需持续按住；副屏机台“設定”不等于CUSTOM。Spice截图新时间戳和不同JPEG只证明捕获样本不同，不能当作每个Present、游戏tick或判定原点；缩小倍数在D3D完整读回后应用，采图仍会扰动帧预算。PG相位或转盘速度对毫秒/BPM拟合冲突时，保留竞争解释，不据三组颜色或文件名发布统一周期。具体源证据与未匹配二进制边界取[参照复核](../../doc_md/other/OMNI_OPENINGS_20261021.md#2026-10-24持续组件与原版参照复核)。

Hi-Speed采样先核实际选曲、难度、BPM、CLASSIC/Display Area状态及高亮档位；helper文件名不证明它真的进入该曲或该档。淡出可读只支持字面数值，不能充作稳定控制样本；黑屏不从相邻档或既有表补值。跨BPM的近似nominal或拟合K仍不是内部公式，命名为“official”的旧单元测试也不能补足原版证据；资格与legacy fixture边界只取[P1-A](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#既有产品面硬约束未被-skin-v1-取代)。

原生取景ENV的`OMS_AUTHOR_SKIN_OPTIONS`由夹具读为`Dictionary<string,string>`，布尔/数值也须用规范字符串；JSON字面量false或1.5会在进入Player前失败，不是产品选项拒绝。BMS旧取景的15/24px断言只覆盖默认00画布，不能拿它验证n11或其它CN尺寸；该分支的CUSTOM取景保留实际画面、冻结选项、计分/结算与错误检查，量化尺寸另沿选中源验证，不把跳过不适用断言写作尺寸通过。

取景宿主重建后的deps覆盖、空ENV及缓存清理后的源码接续统一见[构建与测试](reference_build_and_test.md#测试宿主与产物)，不把宿主失效当作皮肤故障。

实际材质覆盖取证需要真实native实例：mania合成谱没有TimingControlPoint时BarLineGenerator直接不产小节线，不能删去小节线断言；爆炸存活短，固定PNG取景会漏掉，按同一活跃lease经过完整更新后取一次证据。所有预览接入后Frame是带OptionId的图片选项，旧FormDropdown查找失效；Settings保存/重建/复位断言仍须走真实控件。

异步音符替换的竞态夹具不能只等host/Drawable.IsLoaded：构造期的保护fallback也满足它，初次正式来源还可能在后续发布。拍oldVisual前须等PendingLoadTask清空，以已发布画面为基线，再保留旧结果不得发布的断言；选包等待应显式核拒绝原因，不能把LiveGameplay保护或准备拒绝笼统记成超时。

`Program.Bindings`还包含未实例化模板，原生面板断言不能遍历它再在只枚举`PreparedScene.Roots`的节点中盲目Single。先按已准备根树的声明ID限定绑定，再要求Graph/JUDGE两类均存在，最后保留运行时唯一性和实际alpha断言；不要按查找成功与否过滤目标，否则会掩盖真正缺失的root。非空Graph/LIFT美术混选可触发这个取景边界。

新增C5输入展示事件不能自动成为C6回调：旧消费入口按payload family与enum数值通发，会让没有新声明的脚本额外收到轴运动。须先完成引擎状态/授权/时钟折叠，再在作者回调边界过滤内部展示事件，并用真实输入→C5累计、旧key down/up→原C6回调对照；仅不增加snapshot字段不足以证明脚本ABI不变。

组合作者包的预算按最终选中贡献核对。隐藏其它框体的转盘仍会执行轨迹，宜在加载期选定当前框体；作者布局也只保留对应单双侧声明。原PNG可用短内容标识而不重绘，关键帧可缩短ID而不丢采样，但每个dot segment仍须字母开头，数字摘要与数组序号都要前缀。BPL catalog的`bpl-team`与原节点`bpl.team`是不同身份，不应改写原素材marker路径。

血条闪烁前沿不能仅按gauge.type换三张固定dot：NORMAL过80变红、不同Frame的EX HARD/Hazard配色由原fill定义。复制当前完整fill及同一mode资源分支，保持完整UV并只裁末端窄条；P2沿原180度补偿。同图alpha叠在自身上不会闪烁，窄条须作中性明暗调制，不能凭opacity轨迹存在认定画面有变化。v15成品前沿实际8px（调用方覆盖helper默认6px），迁移须核原包，勿把默认参数当旧实物。FC的fcombo24角色原图明示33及果汁主题，可确定主题身份，但不能据此宣称已实测原机菜单默认ID；保留fc00通用款及全部旧选值。

IIDX游标核源：原PNG画布不等同于GEO显示尺寸，SD投影和DP横缩分别沿实际源几何；DP命名游标在dp_frame_1p、S/E标记在center_frame_dp_1p，不能只查center movie或按同名实例去重。容器表示行程、子图相对y绑定timing.progress时，子图高度须除以行程高度，保持实际游标尺寸；不能用负scale-x实现镜像（公开codec只接受非负缩放），左右原RGBA确实对称时可共用原图。稳定ID的每个dot segment须以小写字母开始，数组序号使用o0而不是裸0；Python结构检查不能代替真实SkinAuthoring codec检查。完整当前验证仍取P1-A STATUS。

材料Diagnostics为空不证明C3无布局警告：BGA窗口诊断另合入PersistenceSafeDiagnostics/Batch，成功commit后才异步发布全局通知，提示可能跨下一玩法延续。定位须对照原始日志时间、已提交snapshot与同输入旧包，不能凭截图归给当前玩法或窄窗口。显式作者stage命中会覆盖按窗口比例算的轨道宽度，但其BGA窗仍须避开固定HUD保留带；任一窗口失败按合同整组省略，不以放宽通用solver掩盖作者几何错误。

公开instances.material可按同一选定包已解析Global/Stage/Group/Lane Provide slot/resource挂载模板，解决BMS/mania及单双舞台的有限复用；不是任意条件或跨包scene继承。血槽用独立外框、暗格与clip.reveal-x亮格，GaugeHeight选定包默认0.036，静线0.07。parseInstances的target/material须二选一，不能同时列入validateFields required。真实DrawNode会拒绝未Masking的outline/shadow容器，单纯headless属性检查不能证明绘制成功；outline是矩形边框，不能当字形描边使用。

material模板无slot子节点须在owner表面定位，不能仅覆盖Stage target后仍用整个playfield矩形，否则血槽会盖满音轨。显式target旧模板不改坐标语义。clip外包transform后必须将子节点挂到content mask，否则裁切只改变空容器；实际内置BMS/mania产品检查与图11共同取证。编辑含非ASCII字面量的测试文件须保持UTF-8，曾误改é为茅导致规范化路径冲突fixture失真，恢复原值后通过，不应放宽生产校验。

图12素材迭代：整体底板过亮可用普通Global `stage.background` sprite的colour乘色，省略resource时使用该玩法已经准备的公开素材；BMS原图保留，mania透明图仍透明。不为单张1P参考硬编码会穿帮的左右控制台底座。该次键面/切角外壳修改止于离线作者配方，以真实作者包的双玩法/多模式多窗口矩阵和desktop像素验证，不把此前full日志重新计为新执行。

用户图07显示控制键和BGA周围仍穿透歌曲背景。根因不是stage.png透明：原stage.background只有Stage，其矩形是轨道group union，键区和外侧不在其中。当前扩展Global/Stage独立声明；Global=SafeBounds，在Background层depth2，Stage仍depth1。BGA实际内容在它们前方。不得用全屏decoration（Overlay）盖住音符，也不扩大所有Stage矩形。

普通scene的Stage/Group/Lane目标需要精确id+index；显式target实例无条件准备，用variant隐藏也仍解析目标。同包覆盖BMS/mania和单双舞台时，不得无条件引用不存在的deck-2或BMS目标，有限复用使用上述material实例。分段血条不能只换分段PNG：原纯texture缩放整个宽度，会使格距随血量变化。固定暗槽+mask裁切满宽亮图中，RelativeChildSize补偿mask宽度，0值避免除零并保留底槽。视觉测试必须挂载父容器，检查实际DrawWidth而非只看相对Width。

转盘原FillMode.Fit受scratch音轨宽约束，换高质量原图也不会变大。公开ScratchKeyWidth只扩独立KeyAreaHeight内的视觉矩形，solver向普通键外侧求解并避开safe/BGA，native消费者读取同snapshot。不得为放大转盘改ScratchLaneWidth或输入lane；旧包缺字段保持倍率1。精确拼接的键面和50格血槽由离线PNG配方提供，游戏仍只消费包内文件。

血槽现有暗/亮两层，素材归属测试须严格核对两层均使用已准备纹理，其它语义槽仍一层；不能沿用Sprite.Single，也不能用First掩盖意外多图。full出现安装准入错误时不能凭前一测试失败就认定污染，应保留首次输出并独立及组合复验；临时测试runtime日志会自动清理。

Global背景仍禁止Suppress；用透明PNG表达透明。旧声明未提供Global时继续Inherit到canonical，不能偷偷另加缺省规则。NonVisual未加载Container的Parent可为空，验证装配应看层Children；真实像素另用隔离desktop GameHost.TakeScreenshotAsync。

simple新仪表各有独立边框，布局测试须逐字段检查对应面板，不能把所有字段都限定在第一块面板内。固定字大小必须覆盖640×480信息带，放大数字时检查标签/值/底框三者不相交。

complex退役后历史ordinary import测试从SkinAuthoringSamples取归档，不再从Skins/Canonical取不存在的内置原件；不为测试重新发布complex。


信息区取数：MIN/MAX必须用BMS原生TimingProfile实际segments，排除STOP兼容控制点、SCROLL及未引用BPM定义。统计读取真实ScoreProcessor.Statistics，CB在Combo通知后才更新，须逐帧同步；EventStream聚合器重附着须保留SongInformation。Global材质模板只实例一次以免14K重复；作者信息区通过layout-surface继承，缺失不能猜1P坐标。

完整信息区暴露旧字体预算错误：OsuSpriteText由共享FontStore取得字形，并非每个标签新分配2048图集。保留每glyph保守面积总和与原64MP/256MB上限，统一页对齐一次。OsuSpriteText隐藏的Truncate setter会抛异常；专用省略文本须像既有TruncatingSpriteText一样显式转SpriteText再设置，并在实际产品路径检查RuntimeFaults，不能只等ready超时。长曲名用作者声明text-overflow=ellipsis，不改绑定原文。

2026-09-13难度表取数：BMS转换器会新建BeatmapMetadata，游玩谱上的difficulty_table_entries可能为空，不能据此宣称原谱未被收录。演奏只读归类须从PlayerLoader屏幕lease缓存的具体Bindable<WorkingBeatmap>获取原谱资料并在初始化时快照；IBindable接口缓存仍可能指向全局对象，不能混用。真实PlayerLoader的BMS及转谱mania用例应覆盖持久源有表、转换产物无表、皮肤快照仍有表；作者#DIFFICULTY/#PLAYLEVEL独立保留，表归类不得从星级或作者LEVEL拼造。

2026-09-13轨宽：IIDX黑键不能用BMS默认NoteColourCyan判断（7K key4可能Yellow，5K调色也不同），应按每deck规范键序2/4/6；P2只改显示位置，14K第二deck必须重新从白键起算。SkinRecipe布局及legacy键素材也须同步这个起点。看起来scratch note太窄时，先区分exact lane.Rect、独立ScratchLaneSpacing、PNG alpha bbox；当前drawable链本身Width=1，透明边和皿旁空隙才是本次实证，不应靠超lane拉伸改判定/时间区。不同宽度轨道的分隔线和音符透明边需补偿投影厚度。

验证注意：Desktop.slnf也构建测试工程，不能假定它只编译产品而允许agent同步写测试；冻结相关源码后串行运行最终gate。BmsHoldNote.EndTime=StartTime+Duration，测试平移谱面只改StartTime，再改EndTime会延长LN；普通note与LN头尾不一定同帧存在，应在各自真实生存期验证源Sprite。共享作者配方改动后需同步Aurora Study练习包的生成结果，不能只重打内置simple便宣称整个作者套件可重复。

2026-10-18组合包诊断：目录导入的entry预算包含中间目录，OSK闭包文件数合格不代表作者目录可导入；大量小配置改用稳定散列文件名的平面目录，并从引用闭包构建干净发布目录，来源staging保留。根INI的Global target仍须带完整ruleset/keymode/stage-mode条件，不能以scene/catalog合法推定INI合法。分支manifest.scene仍是固定合同头，真实子场景路径由catalog contribution.scene提供；平面化不能把物理路径写入头而触发006。带正确IHDR但损坏zlib的PNG会抛ImageFormatException；只抓UnknownImageFormatException/InvalidImageContentException会漏掉这类边界失败，应转为明确素材错误。

记录动画的预算须按裁切后实际三角形和Sprite池计数；源三角形数不能覆盖祖先mask/视锥裁切带来的增长。准备阶段逐帧计算最大池和总应用数，运行时复用池。专用visual退役中rollback已按完整节点成本扣减，余项只能减未跟踪成本，不能再按RuntimeNodes.Count补减一次。长爆炸的native pool寿命读选定clip时长，不能被旧200ms回收截断。

按键动画相位取生产事件快照的真实press/release origin；宿主临时字典会在late attach、边沿背压和epoch切换时漂移。按住状态但未知press origin保持稳定held帧，不以attach/seek时间伪造起点；真实release即使press未知仍可播放out。重试/seek清origin但保留真实按住状态，不能为了动画强制输入归零。编辑器没有GameplayClockContainer.OnSeek，generic reset已pending时可能绕过回退识别，deferred期间也可能收到中间时刻边沿；最终完整snapshot提交前还须按最终时间剔除已落在未来的旧起点。同一时间域内的有效过去起点继续保留，不放宽EventStream的未来输入拒绝。

组合INI的C4身份：原package SourceRevision的parsed SHA用于原文件前后守卫，不能同时当作选中组合的Public Document来源SHA。BMS已用合并声明准备正确纹理，但CreateMaterialSet仍标原SHA时，会出现Provide/selected=true、无诊断，却全部note模板routed=[]并继续显示默认fallback。公开声明的Provide/Suppress/diagnostic须标合并document真实SHA，legacy authority保持原SHA；不通过移除ScenePreparer强归属检查解决。实机须检查实际native pooled note的SpecialisedScene和源像素高度，菜单或普通纹理可显示不能证明选项已生效。

Global装饰标记会沿材质作用域继承；框体信息通过Global decoration挂载时，每个Stage须明确Suppress，阻断Stage/Group/Lane重复实例。拆旧FC时若只删Stage的旧Provide而没有写回Suppress，hud.text即使已完全隐藏，仍会看到标题和仪表在轨道内按Stage比例缩放重绘；先查精确材质ResourceName与target，不据叠字猜测共享HUD失效。新的lane装饰有更具体声明时可正常覆盖Stage Suppress，不应改引擎继承规则或抑制整个原生HUD。

退出期HUD级联：一个Headless host首次abort会让后续数百fixture转抛同一缓存异常，必须先按TRX时间定位第一个实际执行步骤。SkinnableContainer的terminal工作关闭先于HUDOverlay真正Dispose，content已撤销而旧load完成位仍true时，另一owner迟到的OnComponentsLoaded会驱动HUD adapter错误搬移旧source；Remove的“unsupported by current skin”此时是生命周期失效，不是作者权限问题。ComponentsLoaded须同时读取原terminal flag及Dispose状态；不吞Remove异常、不清旧visual冒充detach、不延迟测试退出。确定性回归持有真实第二owner的callback，先关闭第一owner再投递，并保留正常两owner路由与精确释放对照。

Headless的每例存储与失败收尾统一见[测试宿主地雷](reference_build_and_test.md#测试宿主与产物)；不能仅由测试顺序推断残留journal导致下一项失败，须保留具体拒绝原因和中间通过用例。

- 独立大包同时受两道不同门约束：C2捕获结构64MiB，OSK导入全部配置仍16MiB，后者不能按前者放宽。相同几何可由不同选项清单声明自身纹理并共用scene；相同INI按完整字节去重。scene经codec将整数编码成double可能长出`.0`，不能只加原文件长度；缩短声明ID并给归一化留预算，保留全部原图/采样。详见[本轮记录](../../doc_md/other/LIGHTING_DETAILS_20261021.md)。

- scene点分stable ID的每一段都须以字母开头，段内不使用下划线；`td.0.keys`、`td.p0.0`或原资源名直接拼入ID，即使无冲突、ZIP闭包正确，实际导入仍会以InvalidStableId拒绝。摘要段加`h`前缀，序号用`n0`等合法段；源文件名与公开ID应分别保留。生成器应复用严格ID校验，覆盖全部C2条件分支的声明和引用，不能只测默认树或放宽公共codec。诊断来源见[增量记录](../../doc_md/other/OMNI_GAMEPLAY_COMPLETION_20261026.md#验证与保全)与[Standard来源管线](../../doc_md/other/STANDARD_MODEL_SKIN_20261105.md)。
- 未引用排查先展开`.json.br`并追到atlas/字形/UV消费者；原图文件名未出现不是未使用证据。原白图经tint变绿必须逐RGBA核对及保存来源收据，不能仅靠视觉相似；剩余候选也不能由文件名直接升级为HCN/COMBO等玩法状态。范围与工具入口见[素材审计](../../doc_md/other/OMNI_GAMEPLAY_COMPLETION_20261026.md#素材是否已经完整分类)。
- 用原图空区遮HDD专用快捷键前须核alpha；judge plate空区alpha204会留下0/9/3残影，应取已核实opaque像素而非继续叠半透明遮罩。AUTO灰字可能烘在整张plate内，只隐藏橙灯仍会让unknown看成OFF；先遮原灰字，再在实际known门内恢复原灰slice/橙灯，毫秒读数独立。原PNG字节保全不等于显示语义正确，准入取[P1-A合同](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#iidx-素材作品约束)。
- 原生Mod提示消失要比较HUD.LoadComplete、Player.OnEntering、IsSceneReady与实际start四个时点；只在Entering重给2秒仍会被C5/5秒准备耗完。可见期应落在`startGameplayImmediately`的真实`Reset(startClock:true)`后，不能只搬modsRect、加假Mod图或强开HUD父级；Replay guard保持。几何和可见生命周期分别核对，合同归[P1-A](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#皮肤配置项与显式舞台)。
- 嵌套AFP不能只按root总帧数截导出。lane345的root边框128@60、同一cat child256@60；独立cat movie128@30已在嵌入时翻倍，不能再翻一次。原root结束不证明child也结束；先核source renderer行为和逐tick原PNG/UV，再将作者明确选择的两循环分别保留，不重复旧128成品假装补帧。原机parent-loop仍需原机CUSTOM，来源连续性与OMS播放各自留证。PGREAT同级蓝/绿/粉数字也不是简单tint：alpha可不同，须用原图配对；采集timestamp不是renderclock，不能据稀疏截图宣布精确原机周期。

## Classic原图面板与私有receipt边界

Classic数字整条可用既有`original_sprite`直接画`tx_num1_4`；`gc.font`重拼即使字值相同也不等于源像素。Classic专用cursor是150×70，Normal是130×70，不能因角色相近共用。标签PNG正确不证明GEO正确：Classic NOTES TIME与绿字须沿各侧/DP原anchor，已正确的底板/竖排标签无需重做；输入/存档合同取[P1-A](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#既有产品面硬约束未被-skin-v1-取代)。

私有来源receipt未提供`size`时，先核其真实schema及独立PNG/GEO来源，不把读取器假设失败说成源图坏；修读取器后仍须保留源hash、真实尺寸、nearest和路径边界，不能退成文件名存在检查。当前原图和验证证据只回链[P1-A日志](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-28classic11局内选择与原图面板准备)，不在memory复制进度或源图摘要表。

原机`usr_l_music`/artist的重复占位字矩形不证明真实字体、长文字裁剪或缩放；DP2P占位甚至能出屏。只按已核AFP锚点落位，宿主fit策略另取真实DP图；同名机身texture可左右各画一次，须按left export的layer_path定位边界。style33特判也须覆盖DP，不能让原锚点被SP写死矩形绕过。视觉回归应按SongTitle/SongArtist真实绑定抓quad，对实际可见label/GN/面板核相交，结构CLI与原图字节相等不能代签无重叠。

同一个scroll.mode来源可同时控制三种整面板和NOTES TIME等子门，子门可能没有true case；面板取景须按实际同owner的三兄弟识别，不能对所有该来源variant直接Single(true)。保留三mode等价、实际alpha及Start可见性断言。

原生PlayerLoader尚不进入游玩时先区分LoadTask/ownership与ReadyForGameplay。后台Ready、已解码和无scope拒绝不等于已允许push；加载页的hover/idle/focus条件仍有效。隔离夹具按正常鼠标路径离开预览pane，再用原predicate等待；不得写ready/idle/clock或仅加timeout宣称修好。

PowerShell7读取私有API输入锁时，`ConvertFrom-Json`可自动把ISO deadline转成DateTime，CLI插值随后变成本地格式，令Python在API连接前拒绝。用`-DateKind String`保留原ISO字符串，不能重写锁或延期掩盖解析失败；实际失败与保全边界见[P1-A日志](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-29原版游客导航r8窄门)。

原版Welcome与第二阶段选曲教学都有六点，但不是同一组六页；loop数量也不是逻辑页数，两个timer分支可占同一第4点。按实际白点、AFP/GEO/文字和host关联辨页，独立Start終了素材不能证明首组末页有该footer，正文白键决定曲目更不能当安全skip。旧来源取[R8审查](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-29原版游客导航r8窄门)；[R11](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-29原版教学退出与classic入口)才实证首组第六点/printed Start終了及其后无遮挡MusicSelect，不追溯补签每页控制。大图往返可能耗尽guest或整首歌，先用缩图辨页、保留原图核数字；开曲确认后需立即hold Start，专用CS-style延迟须单列，不能用菜单冻结代替歌曲开场暂停或签自然时序。

Time Freeze先核实际`-patchcfgpath`和最终日志，不能只看默认配置。10月29日Lightning参照的R9已有Standard/Menu ON；R10补Premium后两项ON，但20稳定只观测于Mode Select教学覆盖层，不能外推所有timer或补丁因果。R12结束帮助后显示00再进入Tutorial，尚不能区分超时/Start传递；该轮游客提示仍倒计时，既定host deadline与guest导航限制保持原历史范围。CS-style Song Start Delay需单列，并在实际白键确认开曲后同一Root调用立即hold Start，延后一次截图可能已失去整首歌。持有后才可在同一开场有限连续采速度档位，每条仍检查deadline/owned，实际数字逐原图核读，不推自然时序。资格取[R12](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-29原版classic与floating190读数)，不把这些编号或补丁结论套到24/26来源。

原版显示小数不等于内部精度，API轴位移也不是实体编码器单位。只读选定标量先核精确PID/启动身份、模块路径及DLL字节，仅VM_READ；地址/字节源留私下。R13证同显示1.75的两内部值会落到不同Classic档，实际最近档中点取低；相同API手势亦可能落在不同整数计数边界。完整数字只取[R13](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-29原版独立bpm与内部速度核对)，R12的190表不补签未读过的速率字段。字段类型/数值不证明完整语义；不能固定K外推。OMS modeScale、内部精度、相邻选档与旧回放0需联动实际滚动，不能只修显示。

自然运动的release→有限连续取景须放在同一Root调用，中间挂图分析可能已耗尽整首歌。每图按实际stage资格，文件名active或残留chart标量不能证明gameplay；R13的040～046实际Results，033是guard拒绝且无图，后续034/035的预期名字也不是分支证据。均保留失败原件，不放宽限制。PE叶函数可能无exception-directory项；明确的byte span只签所读指令，不伪称函数边界或静默猜入口。

原版自然运动可优先用无输入Demo，把一次有界标量采样与有限小段密集取景覆盖轮播，省去guest/教学导航；首张已是Logo也不重启。按同一音符、实际BPM/raw及保存时间两侧标量核位移，保留tick向量累加与绘制投影的float32顺序，不用猜定K或把绿字推算当实读。保存UTC不等于呈现帧相位，字段与tick频率吻合也不证明实体FPS；资格见[R15](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-29原版demo自然运动标尺)。

连续批次前先核owned状态，重复hold会被拒绝，预期文件名不会因此生成。handle、进程和端口消失而exit回执缺失时，退出代码/原因只能记未知；可离线另核原元数据/关键摘要，不能造exit0或冒签依赖最终输入读取的正常gate。R14限制及失败原件取[独立保全核对](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-29原版r14失败取景与独立保全核对)。

取景旁车不要直接序列化osuTK `Vector2`：其`PerpendicularRight`等计算属性可递归，投影成X/Y标量即可，不改被观察对象。`ChildrenOfType<T>()`可包含根/content自身，额外Prepend后须先按同一drawable身份Distinct，再保留真正的Single断言，不能改First掩盖多计数器。普通Results等待实际SelectedScore、展开面板和原计数器完成后再取景，不以已push或固定延时代表稳定；居中fallback须分别检查TransformDrawable盒子和ContentDrawable字形。失败与修正见[33DP日志](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-29omni33dp原header布局修正)。

难度Mod修改playable `Beatmap.Difficulty`，不一定改独立克隆的`BeatmapInfo.Difficulty`。核JD生效应同时读取真实Mod设置、playable难度及实际音符消费的HitWindows；只看原metadata会把正确生效误判为失败，不得改metadata迎合断言。来源见上述[33DP日志](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-29omni33dp原header布局修正)。

`BindableDouble`默认持久化字符串会舍入内部精度。需要round-trip时应覆写虚方法`ToString(string?, IFormatProvider?)`，不是不存在的单参数重载；自定义子类还必须覆写`CreateInstance()`返回同一运行时子类，否则framework的`GetBoundCopy()`会在载入配置时拒绝。显示两位小数与保存精度分开，尤其不能让保存后的值跨过Classic选档中点；合同取[P1-A读谱](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#既有产品面硬约束未被-skin-v1-取代)。

原字体先看SFNT签名，不按扩展名筛掉.bin：已核源有一个三face TTC采用.bin。原模块字族名用CP932，UTF-8/UTF-16或字体文件名搜索会漏；字族/路径出现、指针邻域、初始化与实际曲名/作者绑定是不同证据。字体字段还须核对所属资源，视频列表的artist字段不是gameplay文字槽。独立进程GDI/SFNT表比对可校准face识别，不能代签HDD实际绘制绑定或字体已应用；不得据此先选“相近字体”，边界取[P1-A收尾记录](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-29omni候选核验收尾与停止推进)。

## Standard 来源与能力诊断

同一 HDD 内的旧代主题不等于该旧代原版来源：Rootage 的 `gmframe03/08/12/14/16/17/19/21` 属于 ARC SCORE 旧作复刻主题，不能由最小编号03推出覆盖3～26，也不能用主题款数替代原版代数。组成库的`s23/s24`与`gmframe23`又是[明确作者别名](../../tools/IidxSkinAssets/standard_source_catalog.py)，须沿`sourceComposition`、`origin`与保留的输入清单回到原路径，不能把组成库路径当作原HDD目录或已取得23程序的证据。当前来源分类只取 [Standard记录](../../doc_md/other/STANDARD_MODEL_SKIN_20261105.md#身份与来源)。

相同PNG不证明同一动画：24的`fcombo00`与26共像素，但AFP/GEO不同，须保留独立来源；不能按PNG摘要去掉旧FC。原文件逐字节保全、atlas解码像素一致、GEO/AFP调用资格各是不同证明。依据取[24来源接入](../../doc_md/other/STANDARD_MODEL_SKIN_20261105.md#2026-11-07-24-来源接入)，不在记忆复制素材数量或成品版本。

早期来源缺少专用Normal预览美术，不等于OMS没有原生预览。23/24分支不声明后来的小窗或首排cue；`HasAuthoredNormalSpeedPreview=false`时，Start前按原生资格显示轨内预览。诊断同时核选中的来源、真实publication、缺少对应作者节点及实际音符纹理/位置；不能把26的断言直接套到旧源，也不能用“旧源缺图”为其它来源跳过检查。真实取景条件见[Reading用例](../../tools/StandardSkinCapture/TestSceneStandardSkinCapture.Reading.cs)。同理，23沿24调用方的中心平移/Graph标尺只是保留美术适配，不证明23原软件相同；来源边界仍取上方作品记录。

已选包和成品引用闭包不能证明整份HDD素材穷尽；还须查全目录、包内电影索引、原加载链与未选来源。26演奏主题表没有gmframe25，但其残留框体仍可作为作品来源；同理，当前程序不调用不能直接排除另一族的失败演出。`1/common`曾因一个非法文本名称整包解码失败，原名称字节的MD5却能精确定位纹理；无损恢复时保留bytes与存储标识，像素相同只能登记alias，不能猜名重写。`0/1`也不能凭目录数字当分辨率重复，原标题、实际像素、电影和调用资格应分别查。具体缺口与恢复材料取[Standard查漏](../../doc_md/other/STANDARD_MODEL_SKIN_20261105.md#素材查漏与未采用范围)。

26 原版单人画面不可只枚举 `center_frame_sp`：同源还存在 `center_frame_wide_1p/2p`，Graph 开关使用不同中心电影与数字锚点。源电影名不等于已观察到的游戏分支；先比较实际开口、SCORE/MAXCOMBO 位置和原动态实例，再决定选择与宿主平移。`apertures()`若已合成平移后的全局画布，写 BgaViewports 时不得再次加偏移。逐代来源与实际资格取 [Standard 26 记录](../../doc_md/other/STANDARD_MODEL_SKIN_20261105.md)，不能借 Lightning 的宿主偏移填空。

26 游客能进入标题但选模式回标题时，须分清基础 e-amusement 服务与游戏专用协议；只有 services/pcbtracker/facility 正常不证明 IIDX 配置已返回。Copula 以后还依赖服务发现的 `local2`，漏项会使游戏专用请求根本不发出。对未知请求只记录已校验的模块/方法，不打印账号、请求体或完整启动参数；空日志也不能证明无错误。该诊断不授予使用真实卡或存档的权限。

spice2x 启动器可能重新启动管理员子进程；仅等待启动器 PID 退出会错误宣称游戏已经停止。取实际 API 监听 PID，并在正常 control.exit 后确认该子进程停止，再备份运行后差异和恢复小文件。对 Suppress 材料记录使用 `TryGetMaterial`，不能无条件读取 `Material`；实际渲染已准备成功时，诊断器自身取空载荷也会中止取景，不能归为作者包故障。

BMS 作者 BGA 还受公共 solver 的全宽底部 8% HUD 保留区约束，即使那里不与轨道相交。DP 原下窗到 670px 并不意味着可以原样声明；越界会使整组窗口 unavailable。先保存完整原开口，再把 720px 画布的实际窗口裁至 662px 内，并用谱面真实加载的图片验证 viewport/content-state/纹理及 framebuffer；默认背景不能证明 BGA 成功。DP 上下窗应按真实 qpro hook 分区测量，不能全栏只取最大的透明矩形。

原判定标签加 COMBO 的横排要按实际位数核总宽；计分/自然 Results 成功不能证明三位、四位字形仍在轨内。隐藏数字的 alpha 不会自动消除横排占位，无 COMBO 须独立居中。按数值驱动缩放还须遵守 C4 轨迹时间上限，超范围不能伪称精确分段。看似缺数字时先核原图 alpha bbox 与实心笔画，再映射 framebuffer；透明 padding 的 quad 越界不等于笔画被遮挡。实际资格取 [Standard 验证](../../doc_md/other/STANDARD_MODEL_SKIN_20261105.md#验证与原版参照)。

作者数字 atlas 的区域属于 glyph.`rect`，不是 resource.`region`；同一规范化路径不能换多个资源ID重复声明。资源只保留公开字段，原数字图集按一个资源共享，字体/字号复用而非每个读数另占字库。Python结构自洽不等于实际codec通过。`y-px`也不是公开节点属性；原像素位移需按实际父槽高度换成`y`，C3判定带、Global画布和482px clip不能混用分母。来源与复跑入口见[Standard记录](../../doc_md/other/STANDARD_MODEL_SKIN_20261105.md)及[工具说明](../../tools/IidxSkinAssets/README.md)。

Global `hud.text` 的默认载体是原生信息区域；material-selected模板的子树继承该窄区域，把1280×720坐标直接放入会压到底部。框体JUDGE可用完整画布的Global decoration信息owner，但decoration所在Overlay层低于判定的HudForeground，内部z不能跨层遮住POOR。全屏失败片应沿现有direct scene树，以Global hud.text拥有前景，显式Global子节点保留完整目标矩形；不使用强制继承窄载体的material-selected实例，也不把失败藏在JUDGE偏好下面。须分别核对原坐标、实际prepared rect和运行层级，见[Standard记录](../../doc_md/other/STANDARD_MODEL_SKIN_20261105.md)。

真实AT入口采用ReplayPlayer；取景工具自行拼出BmsSoloPlayer加Replay会制造不存在的输入归属。回放独占时，两条输入队列持续清理真实输入，可能出现Start仍held而AdjustmentHeld已复位；一次按键或反复写皮肤状态不能证明普通游玩可用。读谱交互应沿普通SoloPlayer和原生输入API取证，保留输入计划、真实投递时间与计分，见[取景工具](../../tools/StandardSkinCapture/README.md)。

把测试游戏铺满framebuffer时，要保留其ManualInputManager父树；仅移走Game后向旧InputManager按Esc，会把键送进空树，不能据此认定产品退场失效。实际Esc由HoldForMenuButton进入PerformExitWithConfirmation，独立失败尾声须在该既有分支结束并打开FailOverlay，不改QuickExit或ScreenStack退出语义。

原caller的坐标须连同sprite原点核对：26判定的y=342是完整PNG顶边，并非中心；横排容器还须显式设成74px原高，不能只改anchor而继承三倍判定带。数字advance37与55px完整光晕图不同，BAD/POOR原本无数字；原图尺寸不能替代排版表。FA/SL的font5固定8px字格和hook+13/y+150本来超出原底板，不能擅自收拢。Hidden+Lift不在26官方显示区域组合中；分别验LIFT&SUD+、SUD+&HID+，不把Hidden读数移离实际边界。证据与适配边界见[Standard记录](../../doc_md/other/STANDARD_MODEL_SKIN_20261105.md)。

素材名与调用语义分开核：26的`miss`是实际舞台失败电影，`failed_`另属boss流程；`turn_shutter_light`是入场闪光，不据名字绑定Scratch pressed。`gauge_up`取各框体源电影，重复条件是达到实际Clear门槛且片段结束，不是统一100%；Gauge B/C隐藏fill/百分比不等于隐藏该光。初始化Clear循环与真实增长历史是两件事，不能造一个初始增长事件填空。原图帧数也不证明原宿主会完整显示所有尾帧。对应边界见[Standard记录](../../doc_md/other/STANDARD_MODEL_SKIN_20261105.md)。

26读谱原型里的数字钩子会由程序隐藏并用原字体重画，不能让原型数字与活读数叠在一起。原白字999刻度、OMS实际1000单位、绿数当前/最大/最小端须区分；相同当前绿数不能重建BPM范围。原绿数对齐要追到字体renderer，原矩形不足以判断left/centre/right。配置的Sudden/Hidden值在关闭后仍保留，读数必须另看active；Lift位置取已应用offset。首排原图跟activeSudden顶边而不是Lift，原482px clip与484px轨高分别保留。见[Standard读谱来源](../../doc_md/other/STANDARD_MODEL_SKIN_20261105.md)。

26 Normal预览的BPM条纹理高27px但调用方支点是30px，按图片底边缩放会错位。原预览计数器只在该分支运行，不能拿曲目beat代签其节奏；采用OMS唯一预览时钟时须明确周期适配，保留其实际暂停和激活状态。原16帧窗、16音符模式、首排脉冲资格与当前实现要分别记，不能仅因素材齐全就写完整原机时序。旧`5key_auto`图和历史成绩字符串也不证明26存在对应可选模式，更不等于BMS5K拓扑。来源界定只取[Standard原软件审计](../../doc_md/other/STANDARD_MODEL_SKIN_20261105.md)，不用后代作品清单定义遗漏。

原生Preview的active/phase不能证明音符实际可见。BmsAsyncNoteDrawable拿到专属scene后会隐藏fallback，但未进入SetPooledUsageActive就不会OnApply；Preview不是普通音符池，须由自己的可见生命周期成对启用/释放，异步加载前激活也要保留。复核两条真实纹理、窗口内位置、隐藏/复显与释放，不能只签父Alpha。取景证据须单张核对，不将多图输出截断或缩略预览误显黑色BGA当作游戏缺图；疑点应以原分辨率图片、原PNG像素及未遮挡区域比较确认，再决定是否改实现。

BGA原图可能同时包含中央透明窗和边缘装饰孔，alpha==0的全局bbox不能当作视频开口；应核中央完全透明矩形与原四角投影，保留其余孔的原像素。`anime_cover`命名存在也不代表每种布局实际有钩子；26 SP/宽框与DP分别核完整时序，25宽2P须保留负X镜像。Camera/曲目年代的原调用资格与OMS手选外框适配分开登记，见[Standard接入](../../doc_md/other/STANDARD_MODEL_SKIN_20261105.md#查漏后的接入与适配)。

静态框体与运动片段若只是同一原电影的不同采样，不必占两个program；复用前须核实际draw及全部祖先、mask、camera、颜色和层序，不能只比较纹理或局部矩阵。常量clock也占numericSources预算；NEXT等整数表达式精简须覆盖门槛前/等于/后、奇数半档和大整数边界，不能把“某个场景少了程序”当成整组合预算通过。PB实景取证则须让普通Player保存第一局再由第二局原生查询，转换后的临时beatmap wrapper ID不是working map或保存成绩的身份。复跑入口见[取景工具](../../tools/StandardSkinCapture/README.md)。

跨来源可能让直接 sprite 与录制电影引用同一张原 PNG，但不同资源声明共用路径仍会被作者 CLI 拒绝。直接图与录制图应保留独立命名空间，不能改像素或把两个 owner 合并来绕过重复路径检查。作者 ID 压缩须包含真实 `stateMachines[].states[].set[].target`，只改 `assignments` 会漏掉现行 schema；保留诊断语义 ID，并以逆向重写核对全部其它字段，再跑正式 CLI 与同包 Player。来源接入取[Standard 记录](../../doc_md/other/STANDARD_MODEL_SKIN_20261105.md#2026-11-07-24-来源接入)。
