# Standard Model Skin Omni Until 26：独立作品记录

2026-11-05。使用用户提供的 `D:\LDJ-2019090200`，已私下交付初版 **Standard Model Skin Omni Until 26.v26110500**；原任务尚未完成，继续按Standard框体与26软件自身证据补齐实际遗漏。初版可玩不代表制作闭环，也不以Lightning的功能清单定义完成范围。当前状态由 [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)引用本记录，工具入口为 [IidxSkinAssets](../../tools/IidxSkinAssets/README.md)。

## 身份与来源

作品命名为 **Standard Model Skin Omni Until 26**，默认 Rootage。它与 **Lightning Model Skin Omni Until 33** 的素材库、作者目录、配置、名称和 OSK 分开；后者只更正现行名称，历史 Lighting 成品不改名、不覆盖，原停止推进边界保留。新作品不读取旧作者包，也不以 27～33 图像补齐缺口。

该 HDD 实际包含十种 Game Frame：03 / 08 / 12 / 14 / 16 / 17 / 19 / 21 / 25 / 26。分别为 3rd style、8th style、HAPPY SKY、GOLD、EMPRESS、SIRIUS、Lincle、SPADA、CANNON BALLERS、Rootage。最早可用的样式是 03；这是 26 HDD 内保留的款式，不能表述为从初代到 26 连续齐全，也不能据此断言原发行 HDD 的每项美术完全一致。缺失框体待用户后续提供更早 HDD。

原图按 XML/UV 解包，AFP/GEO 按真实父子变换、相机、遮罩和循环指令解析；不描摹、重画或从截图替代原始部件。来源选取 283 个小型 IFS，合计约 197.53 MiB；`1/common.ifs` 严格解码失败且不被成品引用，合格可复跑选择文件保留其余 282 个包、4373 张 UV 图。未复制完整游戏。当前共用部件取该 HDD 的 `0` 源族，不能把另一族的失败静默当作成功。

私有来源、PNG 摘要、展开指令及游戏截图分别保存在本机 `E:\OMS-Development\sources\OMS-Standard-Model-Until26-20261105` 与 `E:\OMS-Development\evidence\standard-model-20261105`。源码仓只维护提取算法、公开合同、无原图的测试和来源名称；原图及其私有摘要清单不入 Git。成品仍按 [IIDX 素材作品约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#iidx-素材作品约束)独立私下使用，不内置或公开分发。

## 当前补齐实现：候选待验

以下是对 26 原软件、原素材与 v00 的独立差异审计所得，不是 Lightning 功能对照。新的作者工具和客户端来源正在整合，尚未完成新候选的 CLI、真实 Player、同包审图与交付验收；不能用下文 v00 的历史数字代签，也没有在此宣布闭环或新的最终版本。

| 26 普通演奏部件 | 当前实现与来源 | 明确的 OMS 适配 |
| --- | --- | --- |
| Start 调速窗口、当前绿数与范围 | `standard_reading.py` 使用 `0/gameparts` 的 Normal/Floating 原窗、原滑块锚点，以及 26 的 DF 平成 Gothic W7 字体；绿数范围取实际最大端到最小端 | 原 Normal 只有十行；OMS 20 档分两页显示真实档位，Classic 明确另标。保持 OMS 速度公式，不冒充原 26 数学；窗口使用原稳态画面 |
| Sudden / Hidden / Lift 边缘读数 | `standard_cover_readout.py` 沿原固定软件锚点显示白字、当前绿数和范围，保留原字号、补零与居中；实际 Start、各自 active 和已应用 Lift 控制显隐、位置 | 原白字是原像素换算的 0～999；这里显示 OMS 实际 0～1000，满值可四位。原 482px 裁切保留，Lift 反向滚动另作实际几何映射；不受 Start 面板偏好开关影响 |
| Normal 速度预览与首排提示 | 唯一原生预览投影到原小窗；原 BPM 条的 38×27 图、30px 支点和强度变化读取同一预览的实际 phase。首排原图沿 active Sudden 顶边，完整图在 482px 处裁切 | 预览采用 OMS 两音符与同一暂停/激活时钟，未复刻原十六音符、每四音符线和十六帧开合。首排采用实际全局第一排与原生 200ms 退场，未冒充原课程难度资格和脉冲时钟 |
| GET READY、普通失败演出 | 三份原 90 帧 READY 由准备时钟驱动；`standard_failure.py` 导出共享 `miss` 的完整 180 帧，绑定真实 `gameplay.fail` | 失败后使用独立 3000ms 视觉退场，曲时和计分停止，再交回原生失败界面；保留完整素材不代表原程序全部 180 帧都可见 |
| 实时 JUDGE、FAST/SLOW、判定位置 | `standard_feedback.py` 使用各框体真实 JUDGE 钩子与原字体；原 A/B/C/OFF 位置、normal/up100/up200/up300/off 与实际 Lift 共用。BMS PR 汇总真实 miss 与空 POOR | mania 六档统计单独映射；不以缺一项统计为由冒充原 PR，也不移动 CN 固定判定线 |
| Graph 目标与差分 | `standard_graph.py` / `standard_graph_targets.py` 使用原 Graph 艺术与原目标调用证据，增加 AAA/AA/A/CUSTOM/NEXT/NEXT+ 和差分，读取实际得分、理论满分、已有 PB | BMS 目标计算和原图组合属于 OMS 计分映射；没有实际对手或 ghost 数据时不补造数据。v00 的固定 MAXIMUM 红柱只属下文历史 |
| Gauge A/B/C、增加提示、轨道亮度 | 原调用方证实 A 全显、B 保留百分比与暗底、C 暗底及有资格的原横线；原 `gauge_up` 取每款框体的三个源电影。轨底按原层序拆分，六级 RGB 亮度只作用 `lane_1p/2p` | 增加提示读取实际 Gauge 正增；达到实际 Clear 条件则循环，初始已 Clear 而无增长历史时取真实挂载起点。B/C 不禁用该光效；没有捏造初始增长，也不改变血量规则 |
| EXP 七档、HCN 显示、长条持续效果、ASCR 显示 | `standard_components.py` 按 26 自身的七档范围与 HCN 外观选项组织；CN/HCN 光效改由真实长条对象在判定线驱动。ASCR OFF 只隐藏实际辅助 Scratch 对象的普通音符及长条头/体/尾 | 不更改自动辅助资格、输入或计分；HCN 采用 CN 外观不改变 HCN 状态。FC 的原宿主层序另有来源证据，当前候选仍需实际 FC17 画面确认 |

原部件和研究出口仍只来自 26：`standard_presentation_options.py` 导出有原层序的轨底片段，`standard_gauge_effect.py` 导出三十份原 Gauge 电影，`standard_failure.py` 导出普通失败片段；整合器从同一私有库核对来源。公开脚本不含原美术或私有摘要。字体使用原 TTC 字形，在 Windows 内存 GDI 中生成固定字格；这不等于原游戏 GPU 字库采样逐像素相同。

判定位置按原完整 PNG 的顶边 `342-offset-lift`，不能当作中心。原数字使用 37px advance，55px 原图的尾部光晕相互重叠；不裁图来凑间距。OMS 保留完整实际 COMBO，超过轨道时才按完整字形外伸等比收拢，并整体居中；原程序五槽上限及宿主 1～3px 取整偏移不冒称完全等价。实时 JUDGE 的 `FA:%4d/SL:%4d` 原单行固定字格本来伸出底板，按原坐标保留；这与误把整块 JUDGE 挂入底部信息槽造成的裁切不同。

26 官方显示区域组合为 SUDDEN+、HIDDEN+、SUD+&HID+、LIFT、LIFT&SUD+，没有 Hidden+Lift。取景分别验证 LIFT&SUD+ 与 SUD+&HID+；OMS 额外允许的 Hidden+Lift 同开会让相近底边的读数相叠，不移动读数去冒充实际边界，也不将该组合记为原26能力。组合来源取[26 官方选项说明](https://p.eagate.573.jp/game/2dx/26/howto/play/option.html)。

来源审计将条目分为已有、26 确有而 v00 未接、仅有资源或外部数据边界、26 不适用；每项分别记录当前实现路径与适配。普通失败、Gauge、ASCR、读谱预览及挡板读数的实际调用方已经核明，不能再笼统记为“资源存在，待查”。原 HD Start 参照和只读调用分析属于本轮证据；attract 的 HCN/Gauge 教学画面不签实际长条交互。最终实机证据仍由新候选独立取得。

资源清单不是整款原游戏重做清单。Qpro 账户装扮、e-amusement 对手/ghost 和摄像头在 26 确实存在，但没有对应的 OMS 实际数据；当前作品不伪造角色、对手或视频流。`5key_auto` 是 HDD 中保留的图，十框体钩子、软件字面引用与[26 Assist 清单](https://p.eagate.573.jp/game/2dx/26/howto/play/option.html)未提供可用的 26 普通演奏调用证据，不能把真实 BMS 5K 称作该辅助模式。Beginner 教学属于[STEP UP 模式流程](https://p.eagate.573.jp/game/2dx/26/howto/mode/game_mode.html)。独立 LIFT CUSTOM、光柱长度、音符厚度由[31 官方新功能页](https://p.eagate.573.jp/game/2dx/31/howto/new/05.html)明确列为新增，不能用后代功能倒推 26 缺项；v00 的独立 LIFT 艺术选择因此是 OMS 作者扩展。

当前私有证据集中在 `completion/source-coverage-audit.json` 和对应的 `native-*.json`，其分类以 v00/r12 为历史基线，后续实现另记。此来源审计不签新包的预算、可见效果、失败时钟或最终交付；这些项目完成前，Standard 任务仍在推进。

## v00 已交付范围：历史

本节只描述 `.v26110500` / r12，后续候选以上节为准。历史数量、图像和限制保持原身份，不因新增实现而改写成新候选的验收结果。

Game Frame 10 款与配套部件共 11 类、609 款选择，275 张小预览。配套范围为 notes 12、charge-notes 1、judgement 14、explosion 20、key-beam 11、lane-cover 167、lift-art 167、turntable 21、graph-area 168、full-combo 18。LIFT 仅对已有原生能力的 BMS 开放；不为 mania 伪造 Lift。

单侧提供 1P / 2P，双侧使用 DP；作者布局覆盖 BMS 5/7/14K 与 mania 5/6/7/8/14/16K。5K 等少轨配置保留原生留空位，不能把每一款都称为原机键数。选曲与 Results 保留 OMS 原界面。

框体保留同一 1280×720 来源画布，入场与持续段按各自原指令拆分；26 的两类入场分别在 120 / 180 帧进入循环，不能套一个统一时长。普通键的原键帽由框体提供，按下光使用同 HDD 的真实素材及各代原投影，保留 21 的微小角度与 25 右侧镜像。它们读取真实输入，准备期间隐藏。组件序号 00 是作者起始选择，未冒充各代原 CUSTOM 默认值。

单人 Graph 开启时使用原 `center_frame_sp`，关闭时分别使用 `center_frame_wide_1p/2p`，宽布局的 SCORE/MAXCOMBO 也取各自真实锚点。Graph 1P 中心右移 36px 与[官方 SP 图](https://p.eagate.573.jp/game/2dx/26/howto/epass/score_graph.html)对齐；2P 左移 36px 是明确的镜像适配。DP 按原 `qpro_bg` 锚点拆出左侧上下开口；下窗同步显示同一 BGA，不伪造 Qpro。公共 BGA 布局保留底部 8% HUD，所以下窗底部裁至 662px；原完整开口和裁切后窗口分别记入私有收据。Graph 关闭时保留 DP 双左窗口属于 OMS 映射。

分数、最高 COMBO、BPM、Hi-Speed、Gauge、进度和 Graph 读取 OMS 实际状态。Graph 面板、数字和柱图使用 `0/graph` 原 23 张 PNG；蓝柱是当前分数，绿柱是已存在的个人最佳，红柱明确标为 MAXIMUM，不虚构对手或 Pacemaker。缺少数据时隐藏对应柱和数字，个人最佳为空则显示 NO DATA；AAA/AA/A 只绑定 BMS 的实际门槛。七位 OMS 分数在原六位 160px 区域内缩小字号；100% 按原独立百位位置合成，避免越过原框边界。Gauge 的 Clear 门槛、Clear 状态和 HAZARD 标识属于明确的 OMS 适配，不能改变玩法规则。

以下内容仍属于作者适配或未签收范围：BGA 开口内的 fit/下沿裁切、DP Graph 右侧复用、Graph 三柱位置及 MAXIMUM 对照、判定行在原生槽中的定位与超宽等比缩放、PG 三色 100ms 换相、OMS 音符命中偏移、非原机键数、七位分数、原 effect 区域内的 OMS Mod 控件和部分文字。原图与几何保真不等于原机完整时序、内部数学、实体手感或正常工作区长期体验验收。

判定标签与 COMBO 按各套原字形宽度排成居中一行，仅当超过 292px 原舞台宽时整体等比缩小，不裁字、不省略数字、不改 PNG。1～7 位按实际位数取比例；受公开轨迹时间范围限制，8～10 位采用可容纳十位整数的保守比例。无 COMBO 时使用独立的居中标签，避免隐藏数字仍占横排宽度。

已提取原 CN/HCN 重复光效研究材料；当前客户端缺少固定在判定线、按长条对象状态计时的专属挂载位置，因此暂未把这些循环假接到普通按键或判定事件。现有 CN/HCN 使用真实源头/体/尾及状态材质。原 `turn_shutter_light` 经指令确认是入场闪光，已保留在原框体动画中，不能因其名字将它改成 Scratch 按下光。

Turntable 中心图沿原投影中心，读取真实 `input.motion-revolutions-1/2`，无运动数据时保持静止；单人 2P 仍取逻辑组 0，DP 右侧才取组 1。全部 40 个原投影都是等比正交变换，原 quad 不变；25 单人 2P 的负行列式对应反向旋转。该映射不使用恒转定时器，不代表原机转速、原程序响应或实体轴已验收。

mania 当前未提供 `scroll.hi-speed` 时数值隐藏，原框体内的 HI-SPEED 标签仍保留；不拿未校准的速度参数冒充原倍率。无 BMS BGA 的玩法显示 OMS 背景，未知难度和等级使用真实文本或留空。

FC17 的 DP 源电影内，COMBO 大字本来宽于轨道，瞬间运动还会继续越界；Graph 开启时会遮住右侧部分字形。当前保留原电影和 Graph 的数据层优先级，未擅自重缩或补一个会裁掉更多字的舞台遮罩。这是可见限制，原游戏宿主的额外变换/裁切尚未确认；其它 FC 选择与默认 FC00 不由这一项推断。

## 验证与原版参照

以下是 v00/r12 的历史验收，不覆盖上节新增的客户端字段、长条挂载、Start/反馈、Gauge 或失败演出。新候选需独立填写验证与交付记录。

离线复核十框体共 100 个源布局与 160 个运动/持续片段；输出无未知指令警告，逐层几何重投影最大误差约 1.14×10⁻¹³。普通键 60907 次源帧矩阵检查未发现该入场片段中键帽位置变化。部件审查逐项核对 2708 个初始文件、1212 次原图复制和 560 个字形像素区域；PG/FC 修订与 Turntable 运动增量另与原部件合并核对。以上均为来源与软件证据，不是游戏实绘验收。

首次实际 Player 暴露出全屏 `decoration` 被 Stage/Group/Lane 继承，BMS 7K 因而重复加载十一份信息层，触发文字预算。作者工具在 Stage 明确抑制继承，只保留 Global 一份；没有放宽客户端预算。验证工具 [StandardSkinCapture](../../tools/StandardSkinCapture/README.md)通过普通导入、模式选择、真实 ReplayPlayer、实际成绩与自然 Results 记录精确包身份和 framebuffer，禁止以静态拼图替代真实演奏。

r11 的29局软件路径通过后，逐图复核发现长 COMBO 的横排行超宽，不能把这些回执当成最终外观验收。真实压力谱用1024个原生mania音符/长条累积到1040 COMBO；10100ms的1024读数在r11越界，r12修后两侧完整居中收拢。正式r12只改14份判定场景，其余文件及全部原图保持；下方最终矩阵重新使用正式同一包，不复用旧包或探针截图。

原版测查直接启动 HDD 本体，使用独立启动配置及空卡游客流程，只备份少量 NVRAM 文件。最初的本机游客服务缺少 `local2` 服务注册，另缺一个无状态到期接口；修复后已进入游客选曲与 1P/2P 实际演奏，宽布局取得 API 图像。游客服务协议沿固定版本的 [IIDX 基类](https://github.com/DragonMinded/bemaniutils/blob/1e66fcd780afb3088ce8e78c88615a143db492ee/bemani/backend/iidx/base.py)与 [Rootage 实现](https://github.com/DragonMinded/bemaniutils/blob/1e66fcd780afb3088ce8e78c88615a143db492ee/bemani/backend/iidx/rootage.py)核对，只在本机运行、不保存卡片/成绩、不转发请求。各次退出以实际 API 监听子进程为准；最后 r7 与游客服务均已停止，恢复后 17 个受保护文件与启动前相同。禁止 computer use；没有创建 HDD 副本、使用真实卡片或上传玩家数据。

正式作者检查通过 2158 个不同代表组合，覆盖各选项及明确条件分支，不代表全部排列。3621 个归档成员与作者闭包逐字节一致，1942 张 PNG、CRC、成员路径和私有收据排除检查通过。48 项 focused 测试及取景宿主 Release 构建通过；客户端运行时代码基线为 `2a1f9a2a`，本次只增加制作/验证工具，未修改玩法引擎。既有五条 ImageSharp 构建告警保留，未重复执行未变的全量产品测试。

最终同包完成 30 局真实 ReplayPlayer：15 个基础玩法/键数/侧别组合、9 个其它框体组合、5 个 Graph 关闭组合，另加一局 mania16 DP 四位数压力谱。全部经过普通导入、原生计分、自然 Full Combo 与普通 Results；压力谱最终为 1,000,000 分、1040 COMBO。180 张实绘 PNG 已检查完整性，其中 44 张逐图审看；不是逐180图视觉签收。BMS 测试谱通过原生 BGA 读取棋盘图片并核对实际纹理/窗口，预览中的棋盘属于测试谱，不是皮肤素材。25 DP 满血时的窄百位“1”另以原图 alpha 与实绘白色笔画复核完整；FC17 的遮挡仍按上文限制保留。

最终包原样复制为 `.v26110500.osk`，与七张原实景、README、VALIDATION 和 SHA256 共11个文件，收存于本机 `deliverables/skins/Standard Model Skin Omni Until 26.v26110500`。详细身份与审图记录保存在私有 evidence 的 `player/final-r12-acceptance.json`、三份视觉报告及 `delivery-r12-receipt.json`。源码制作阶段为 `efd0f70c` / `35fb5b31`，交付需使用已验证基线或后续兼容 OMS；旧窗口先重启再导入。正常工作区长期体验、实体设备、原机完全等价与公开发行未签收；既有 Lightning、静线及原产品门不因此提升。

## 存储与收尾边界

旧字体缓存 11.10 GiB 的清理与五个过期小型探针目录删除均被自动批准审查拒绝，未换方法删除；已有证据及恢复备份保留。手动维护脚本只列出经核对的旧字体缓存，存于本机 deliverables/maintenance；未执行即不能声称已释放空间。临时游客规划文件的一次写入也被拒绝，未重试；这不构成游戏来源或成品证据。

本次收尾仅清除已确认可再生成的单个新 Python 字节码缓存；作者源、最终包、失败候选、截图、回执与恢复备份均保留。旧清理受阻项仍未删除，不以此次小缓存清除冒称已完成大规模回收。
