# P1-A 当前状态：Skin V1、产品面与 release gate

> 核对：2026-11-28（静线纠正经典素材来源）
> 全局见[主线状态](../../mainline/DEVELOPMENT_STATUS.md)，后续门见[PLAN](DEVELOPMENT_PLAN.md)，稳定合同见[TECHNICAL_CONSTRAINTS](TECHNICAL_CONSTRAINTS.md)，历史见[CHANGELOG](CHANGELOG.md)。

## 一句话状态

本地导入、外链与依赖修复已入树，旧在线保持关闭。BMS将Sudden/Hidden/Lift与Hi-Speed统一到“读谱设置”，局内调整按单一开关成组记忆。构建、回归与依赖取[最近一次验证](#最近一次验证)。

静线为唯一内置、首次默认与保底；complex已退役。公开内置恢复前版，IIDX 素材适配见[本机候选](CHANGELOG.md#2026-11-28-更正静线经典素材方向)。BMS/mania各有模式默认和键数覆盖，旧全局配置首次迁移；游玩按最终键数应用。原V-001～V-005、设备和长期体验未签收，Skin V1/release未完成。C1～C7证据不重计，OmsSkin物理删除仍待原实机门。

## IIDX 作品与平台维护状态

[维护计划](IIDX_SKIN_MAINTENANCE.md)。

| 作品 | 宿主 | 推进状态与证据 |
| --- | --- | --- |
| Lightning Model Skin Omni Until 33 | OMS | 自动推进停止，完整形态未完成；最新[.v26102902](CHANGELOG.md#2026-10-29omniv52私下交付)，历史Lighting文件名保留；缺口取[作品记录](../../other/OMNI_GAMEPLAY_COMPLETION_20261026.md) |
| Standard Model Skin Omni Until 26 | OMS | v26111000已交付，19主题；同包33场与原图审查完成，见[20来源交付](../../other/STANDARD_MODEL_SKIN_20261105.md#20-来源-oms-交付) |
| Lightning Until33 | beatoraja | `v26112500` 已交付[无损重包](../../other/BEATORAJA_IIDX_SKINS_20261107.md#2026-11-25-无损重包)。实装记录为 `v26110800` |
| Standard Until26 | beatoraja | 19 主题。`v26112500` 已交付[无损重包](../../other/BEATORAJA_IIDX_SKINS_20261107.md#2026-11-25-无损重包)。实装记录为 `v26111500`，见[20 来源交付](../../other/BEATORAJA_IIDX_SKINS_20261107.md#20-来源-standard-增量) |

安装与缺口取[适配记录](../../other/BEATORAJA_IIDX_SKINS_20261107.md)。OMS选曲/Results沿[原界面决定](CHANGELOG.md#2026-10-28omni两页拟真硬门)。

## 当前产品能力与剩余门

谱面列表按[展示合同](TECHNICAL_CONSTRAINTS.md#谱面列表展示与来源能力)先选模式、所属来源，保留BMS上次来源。2026-10-12软件/隔离宽度证据取[P3-IR](../P3-IR/DEVELOPMENT_STATUS.md#最近一次验证)，正常窗口lazer体验待签收；下载/精确打开及旧实网证据沿原日期，见[CHANGELOG](CHANGELOG.md)。

| 产品面 | 当前可用结果 | 必须保持的边界与未完成项 |
| --- | --- | --- |
| 客户端品牌 | 主菜单、选歌/加载及节拍残影、普通/节日启动、窗口标题与应用图标共用圆润 OMS 字标；启动/退出男声已移除，退出短淡出 | 同源稿见[品牌资源](../../../assets/branding/README.md)；正常窗口、用户主题/曲库与 DPI 待观察，不代签 gameplay 或发行 |
| 公共 BMS 浏览下载 | BMS模式→所属来源→表→等级/关键词，匹配谱面、后台下载、通知、自动入库和精确选歌；追加时已有卡片仍能下载 | 首次打开才联网，原包直读 chartbms；无独立在线试听、整表下载或续传/跨重启恢复。窗口/DPI/网络、大包、完整歌曲与听感待观察，旧在线及发行门不变 |
| Sayobot mania 镜像 | 同一列表按 osu!mania→Sayobot 进入，按原编号/关键词及键数/星级/状态筛选；后台任务、安全解包、只索引mania与原bid精确打开已有软件证据 | 2026-11-26 真实纯 mania 与混合小包已成功入库；失败路径可中文重试。首次进入才联网、无需账号；旧上游在线关闭，OMSIR不改变下载路径。无视频/独立试听/续传，网络、大包与发行门保留 |
| 导入、选择与管理 | 普通.osk、固定chartskin目录；单一卡片按BMS/mania→模式默认或键数设置，未指定键数继承默认；可用的重命名、导出、删除与编辑直接显示并指向卡片皮肤 | 浏览不切模式/谱面/current；失效键数指定依次取模式默认/静线，原ID保留。旧external只读；手动刷新发现目录，编辑需兼容当前谱面 |
| 皮肤配置项 | 作者下拉/开关/数值按皮肤、模式和键数保存并逐层继承；预览整行/箭头可浏览、搜索分页，点图才保存。美术预览按包提供，无预览保留文字 | 下次游玩/重开预览冻结同一选项/INI/布局/美术publication，无进行中换包。图卡可见才解码，每页最多12款，换页/收起释放旧卡；普通包原几何保持 |
| 更新与失败保护 | 设置“刷新皮肤”重新发现固定目录并重载当前整包，失败保留此前外观；开局布局未准备完成时提示重试并回原页，回收未挂资源 | 游玩/预览期间不可 reload；无 watcher 或游玩中替换，不将半载规则集当作成功 |
| 唯一内置与恢复 | 正常开发启动、build/publish 从作者源自动同步 simple；安装原件验证后恢复工作副本，旧内置 complex 选择迁回 simple | canonical 原件故障须提示修复安装并阻止游玩/预览，不恢复程序化主题；不按同名清除用户包 |
| 静线游玩外观 | BMS 青蓝控制台、分段血槽与透明 BGA 外框，mania 紫色控制台和顶部血量。两玩法的音符、键帽和判定牌都使用原创绘制 | 原创资源和实绘取最近记录。完整歌曲、设备与长时体验仍待验收 |
| 实时演奏信息 | 实时判定与断连统计、EX SCORE/HiSpeed、MIN/当前/MAX BPM；曲名、作者标级与独立的表名/表内等级 | 读取规则真实状态；表归类不替代作者难度，皮肤不改变判定/计分、时间或难度表导入 |
| 皮肤布局与公共页面 | 作者声明stage/lane位置、留空槽和键区，不创建输入；BMS最多16个`BgaViewports`，逐窗fit/fill/stretch或none。通用选曲/Results接口共用真实数据及原操作，默认原界面 | 显式field不被旧BGA自适应挪动；冲突整组零窗并诊断，9K/PMS不套黑白比。BGA归[P1-L](../P1-L/DEVELOPMENT_STATUS.md)；Omni当前两页方案取消、保留OMS原界面 |
| 作者制作与可选演出 | [完整套件](../../../skin-authoring/README.md)含制作、元素文件/尺寸/动画、INI/scene/script字段与预算、刷新打包；示例区分完整包与练习片段，手册/图片随套件发行 | 示例工具检查/打包和真实挂载已验证；历史实机图与素材示意分开。原编辑器可改既有组件并存独立副本，无完整scene/script可视化编辑器；不据手册代签视觉或旧三包演练 |
| Lightning 与 BPL 第三方作品 | 默认33，27～33与八支BPL共15框体；1271款美术/539图，13模块/32项配置。BMS5/7/14、mania5/6/7/8/14/16与两侧/DP保持；当前行为和交付身份取上方作品记录 | 原机数学/变速谱、实体手感、转盘/Graph/外框时序、FAST/SLOW、PG、lane345、CUSTOM、默认/滤镜及DP末端仍有未验项，HCN不由教学代签。26属独立Standard，Until34不在范围；继续独立私下分享 |
| 安装与发行 | [11 月 26 日候选](../P1-F/CHANGELOG.md#2026-11-26-windows-候选及六月升级)覆盖资源管理器解压、四轮启动、六月公开包升级及作者套件实际演练 | 独立账户非便携、真实设备与长期体验仍待，公开发行待签收 |

装载与脚本合同见[技术约束](TECHNICAL_CONSTRAINTS.md)。

## 最近一次验证

2026-11-26 真实 Ginger Rush、616、Sayobot 纯 mania／混合小包已入库并核对文件 MD5，具名取[发行候选](../P1-F/CHANGELOG.md#2026-11-26-windows-候选及六月升级)。实站界面首次精确打开、实际大包及整曲仍待。

11 月 25 日，两款 beatoraja [无损重包](../../other/BEATORAJA_IIDX_SKINS_20261107.md#2026-11-25-无损重包)通过逐图、ZIP 回读和隔离回放。次日确认 MinIR 取表导致黑屏，恢复与 OMS-IR 接续见[启动现场](../../other/BEATORAJA_IIDX_SKINS_20261107.md#2026-11-26-minir-启动黑屏)。

Lightning的10月29日候选、产品回归和Release保持[原记录](CHANGELOG.md#2026-10-29omni候选核验收尾与停止推进)；[.v26102902交付](CHANGELOG.md#2026-10-29omniv52私下交付)与候选同字节，Standard不刷新其验证日期。

原版参照的精度、有限运动与退出/保全边界沿[10月29日原记录](CHANGELOG.md#2026-10-29原版demo自然运动标尺)；不代签新候选或完整原机行为。

依赖及旧索引告警取[10 月 22 日记录](CHANGELOG.md#2026-10-22客户端依赖安全修复接入)，BMS 补验取[10 月 24 日记录](CHANGELOG.md#2026-10-24omni持续背景与原版参照边界)。正常窗口、前台性能、长期内存和原机时序待验。原生重编取[P1-L](../P1-L/DEVELOPMENT_STATUS.md)。

10 月旧下载记录见[日志](CHANGELOG.md)，谱库见[扫描审查](../../other/LIBRARY_SCAN_REVIEW_20261010.md)。

## 当前风险与未完成项

- 读谱职责分离取[评估](../../other/OMNI_GAMEPLAY_COMPLETION_20261026.md#2026-10-27mod游戏设置与皮肤职责分离评估)。新计算的归一化不代签原机数学/时序；字体来源已核、曲名/作者绑定未定。HCN、素材与设备缺口沿[作品计划](DEVELOPMENT_PLAN.md#iidx-历代皮肤作品)。

- 原生媒体干净重编结果与剩余门取[P1-L](../P1-L/DEVELOPMENT_STATUS.md)，正常窗口/设备和发行门保持。测试、作者工具与取景宿主须匹配当前构建，沿[开发约束](../../../AGENTS.md#开发磁盘约束)；旧日志/TRX只作历史证据。

- 真实 Sayobot 样本已过，界面首次可玩及不同网络和大包按[当前计划](DEVELOPMENT_PLAN.md#sayobot-mania-实网成功验收)接续。

- 两站没有已确认的独立音乐试听服务；第一版试听沿入库后的本地选歌。远端含 bmson、24K/48K 或不被当前 classic 解码器支持的谱面，不因下载成功伪报可玩；部分包内谱面被原 importer 拒绝时保留成功入库事实。真实大包、网络慢/代理、常用窗口与完整歌曲听感仍需观察，整表/续传/跨重启任务不在当前能力内。

- V-001～V-004未逐项签收、V-005未签收；用户对局部改进的认可不补填未观察矩阵。V-005 原三源操作中的外部注册及工作区行级入口已退出当前设置，不能把旧步骤当作新用户可执行流程；现行可达范围与未执行边界见[集中清单 V-005](../../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md#v-005c6-可选脚本与双规则集-momentum-候选)。
- OmsSkin只保留历史证据与人工对照，源码物理移除仍待原实机门；安装故障不得重新启用它。
- G1 held-root/journal不是filesystem transaction，foreign addition/replacement可导致冻结；未知旧记录及无完整恢复证据的intent不猜测迁移。旧保存根事故只有事后保全，不追溯宣称无损。
- BmsBeatmapDecoderOptions.KeymodeOverride只是host/importer seam，普通loader无用户纠正UI；证据不足的sparse .bms/.bml仍拒绝，该缺口归P1-K。
- BGA内容/seek/decoder归P1-L；本轮会话与作者窗口改动不关闭逐谱视觉门。输入与设备归P1-B/D，真实LN/音频和发行验收仍见对应子线。
