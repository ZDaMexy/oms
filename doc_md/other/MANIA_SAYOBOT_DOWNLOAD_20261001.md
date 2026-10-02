# Sayobot mania 游戏内下载：闭环规划与验收

> 2026-10-01；产品面归P1-A，沿用P1-H的mania目录导入与数据保全合同。开工master@1cedd56工作区干净；fetch成功，相对更新后origin/master领先6、落后0。此为开工在线核对，不代表收尾远端状态。
> 本页保留当时的方案和证据；当前能力见[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)，剩余动作见[PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md#sayobot-mania-实网成功验收)，稳定合同见[Sayobot下载约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#sayobot-mania-浏览下载)。

## 本轮授权与目标

用户在官网/Sayobot取证后明确授权详细规划并推进完成。玩家从既有下载入口进入，选择BMS或osu!mania；BMS继续Ginger Rush/616三级筛选，mania从Sayobot查找、挑选具体原生难度、后台下载、自动加入chartmania并打开该难度。继续采用原搜索筛选、紧凑卡片、难度展开、图标操作与细进度条。

这是新增的公共镜像下载窄例外，不开放OMS私有服务、官网账号/成绩/排行榜、默认endpoint、其它玩法或自动更新。官方download接口标lazer专用，普通第三方OAuth不可调用，本轮不伪装官网客户端。相关事实见[官方文档](https://osu.ppy.sh/docs/)。Sayobot公开API文档由api.sayobot.cn根页面链接；软件须注明User-Agent及发布地址Referer，同一地图不多线程下载，按此实施。

## 实施前闭环计划

1. **取证与输入合同**：核对Sayobot列表、详情、筛选、分页、下载、封面与公开使用规则，保存实际小包样本和必要字段。固定公共源，无用户自定义endpoint。列表只请求mode=8；状态按站方class位，键数cs范围，星级stars范围。分页原样传递endid，0终止；status=-1列表为空；列表期间已下架的集合跳过并保留游标，直接编号查询缺失明确失败。详情只保留mode=3且同一难度满足键数/星级，防止混合包集合级筛选假阳性。JSON有界、最多4并发详情，整页成功后发布，不自动重试。
2. **后台任务与来源边界**：独立Sayobot任务持有真实sid和请求bid；同包活动任务去重，单流有界下载，拒绝未知主机、协议、端口和重定向。明确支持实际tc1.sayobot.cn:25225 HTTPS跳转，不照搬BMS默认端口合同。临时文件在数据根mania-downloads独占目录；完成/取消/失败清理该任务暂存，关浏览页不取消任务，关闭游戏取消并释放。通知与卡片反映排队、下载、入库、取消、失败、完成，不自动打开打断游玩。
3. **原包与入库**：.osz按实际ZIP核对，有界展开且拒绝Windows路径逃逸、ADS、设备名、链接、重复/冲突项、加密与截断。API未提供原MD5，发布前按原BeatmapSetID/BeatmapID及Mode3证明所选谱有效，计算实际MD5；只将目标所在目录通过现有ManiaFolderImporter直入chartmania。复用旧身份、保留玩家文件与历史；坏包/缺失目标不得先发布其它难度后伪报失败。不开通通用hash-backed files导入，不将BMS转成.osz。
4. **统一玩家入口与视觉**：沿用顶部原下载槽位、主菜单和快捷键；页面BMS/mania分区切换，各自保留搜索/筛选状态。mania固定Sayobot，关键词、键数、星级范围及状态可选；紧凑卡片显示真实标题/作者/难度/键数/镜像星级，展开选择有当前标记，封面缺失用普通图标。首次进入该分区才联网。切换/关闭取消旧列表请求与迟到回应，已有下载继续，分页中旧卡片仍可操作。搜索图标与Enter支持重试；完成卡片/通知打开原bid对应本地Guid，选歌稳定后仍保留目标。
5. **验证顺序与验收标准**：由主执行者串行调度formatter/build/test，源码冻结后验证。先客户端、任务、导入focused与core浏览真实鼠标交互，覆盖错误/超时/取消/重试、旧回应、混合模式、分页、窄布局、重复及原库保全；再原BMS下载相关、mania导入相关及Desktop Release。新失败逐项修复，旧失败只按当前HEAD重新编译的身份/消息/堆栈对照归因，不把全套写成全绿。独立审查后以隔离F盘数据根完成真实小包“搜索/筛选→选择原难度→下载→关闭后台继续→入库→准确打开”，核对原sid/bid、实际MD5、chartmania目录和混合包只索引mania，留存真实截图/日志。
6. **交付收尾**：同步P1-A状态/计划/合同/历史，新增公共源硬约束向mainline和AGENTS回写一句摘要/链接；存储行为改变才更新P1-H合同。同步多语使用说明和新踩坑记忆，不刷新未运行的皮肤/设备/发行验收日期。运行CheckDocumentation.ps1、git diff --check，独立终审后在当前master提交，不新建分支/PR，不推送。探针、缓存均放F盘；长期证据保存artifacts/mania-sayobot-download-20261001，核对本轮闲置目录，只清理已确认可再生成且不含用户数据的内容。

## 文件职责与并行约定

来源执行者负责Sayobot客户端/模型及其focused；导入执行者只负责新mania下载导入器、接口和导入focused；任务执行者只负责后台任务/专项；视觉执行者只负责新浏览fixture；探针执行者只写本轮F盘临时探针。主执行者负责共享卡片、界面/游戏注册、统一修复与验证、权威文档和提交。共享build/test/formatter仅由主执行者串行执行，检查期间相关源码冻结，独立终审只读。

## 验收结果

镜像接入实现与软件门已通过；真实窗口素材验证了下载/入库/稳定打开。实站搜索、原编号/联合筛选和原难度鼠标选择成功，但实际谱包未下载完成：当前自动节点安全连接断开。不能把素材闭环、早先HEAD成功或路由修复写成真实小包成功；实网成功入库仍保留为验收门。

## 来源取证与实现结果

站方[公开API根页面](https://api.sayobot.cn/)链接[调用文档](https://www.showdoc.cc/SoulDee?page_id=3969517351482508)与[软件/下载规则](https://www.showdoc.cc/SoulDee?page_id=3969242108165986)。匿名取证保存于`artifacts/mania-sayobot-download-20261001/source-contract.json`，含站方规则摘要、UTC/HEAD服务端日期、真实分页/状态/分类形状、三份小包资料与HEAD大小、精确封面/下载跳转authority；该阶段只有HEAD大小，未伪报GET包摘要。正式真实GET与入库由后续桌面闭环独立记录。

统一卡片提取原BMS页已验证的显示组件；两种玩法共用原搜索行、标题区、卡片/难度展开、图标、封面预算和细进度反馈，各自保留来源流程。使用SayobotClient、游戏持有的ManiaDownloadManager及ruleset内ManiaDownloadImporter，不增加core对mania/BMS ruleset的依赖。源sid/bid保留在任务，发布前完整解码校验目标，目录直入chartmania；普通目录导入、集合OnlineID=-1、历史保全合同未改。

只读审查发现玩家输入0/超Int32数字会从async void逸出参数异常，已在文本边界改为明确读取失败，补自动搜索恢复验证；其它内部参数违约继续暴露。首次新客户端专项有一条断言仍期待旧异常类型，修改后重编译该专项全绿。新导入/视觉fixture的Decoder别名、Visibility命名空间编译错误已修正。后续没有用宽泛catch/fallback掩盖这些错误。

实际窗口还发现失败会同时弹英文系统诊断与中文重试提示。预期失败改在Network/Verbose记录完整异常，保留原中文页面/通知；仅改target为Network仍触发转发，Debug级别又不能在默认日志留证，已据真实结果修正而非改全局日志规则。补六项界面断言；其中一项首轮把其它旧测试迟到的难度计算通知算成当前查询错误，改为只检查本来源重复诊断后全部通过，未隐藏其它错误。

真实混合谱包浏览还暴露了慢速封面下切换筛选的异常，与节点TLS故障不同。当前框架TextureStore.GetAsync在同URL并发取图时走WaitSafely，底层线程池调用被明确拒绝；可控慢图在修复前复现同一消息/堆栈。新增两页面共用的封面owner合并正在读取的图片，每卡只取消自己的等待；旧卡不释放共享纹理，退出时取消实际读取、等待取图撤出后释放loader。底层TextureLoaderStore把源取消转为null，owner退出后再核对并保持取消语义。未catch程序异常、换成域名通配或更改图片预算。

## 有效软件验证

每条新shell先执行`. .\UseDevelopmentStorage.ps1`；所有输出在F盘，临时探针在`.dev-cache/temp/`，证据在`artifacts/mania-sayobot-download-20261001/`。以下为本轮有效结果，不以未运行的full替代人工门：

| 门 | 配置与结果 | 证据 |
| --- | --- | --- |
| Sayobot客户端 | Debug，54通过；原游标、同难度筛选、协议/大小、缺失及可恢复数字输入 | `source-boundary-final.trx`；首轮联合TRX中该项旧异常预期已修复 |
| 后台任务与封面 | Debug，初始任务48、实网路由修正后54/封面35通过；终态/重试/取消/退出、tc1/tc2正反例/标识、原BMS封面边界 | `source-manager-browser.trx` / `mirror-routing-notifications-final.trx`对应套件；后者当时6项UI通知失败随后已修复 |
| 实际包字节预算 | Debug，Explicit单项1通过；无Content-Length真实读取到2GiB边界失败、未调用导入并清理独占暂存 | `download-actual-budget.trx` |
| 原BMS与新mania浏览 | Debug，初始BMS24/mania14通过，通知相关6通过；共享封面修复后联合75通过（BMS24、mania16、封面35），准确匹配封面纹理后两专项再通过。覆盖真实鼠标分区/重试/分页/展开/后台完成/精确打开与游玩拒绝跳转 | `source-manager-browser.trx` / `player-failure-notifications-final.trx` / `expected-failure-diagnostics-final.trx` / `cover-browser-regression-final.trx` / `slow-cover-texture-final.trx` |
| Mania原包导入 | Debug，49通过；路径/头/CRC/大小/格式、原sid/bid/Mode、混合/重复与旧库保护 | `mania-download-import.trx` |
| BMS来源/任务与选歌导航 | Debug，138通过，`--no-build`使用本轮当前Core Tests已编译产物 | `bms-navigation-regression.trx` |
| 既有目录与BMS原包导入 | mania Debug混合/managed两项通过；BMS Release原包专项43通过，使用本轮Release产物 | `mania-storage-regression.trx` / `bms-import-regression.trx` |
| 主工程及Desktop | Debug core零警告/错误；最后源码Desktop.slnf Release零错误，两个未改BMS测试文件的CS8600/CA2007警告保留 | `core-debug-first.log` / `desktop-release-final.log` |

命令为`dotnet format whitespace osu.Desktop.slnf --include <本轮C#文件> --no-restore --verbosity quiet`后重编译；`dotnet test osu.Game.Tests/osu.Game.Tests.csproj --filter 'FullyQualifiedName~SayobotClientTest|FullyQualifiedName~ManiaDownloadManagerTest|FullyQualifiedName~BmsCoverResourceStoreTest|FullyQualifiedName~TestSceneManiaDownload|FullyQualifiedName~TestSceneBmsDownload'`；失败后只重编译/重跑SayobotClientTest，其它绿色用例未无因重复。实际预算用`--no-build --filter 'FullyQualifiedName=osu.Game.Tests.Online.ManiaDownloadManagerTest.TestActualBodyBudgetIsEnforcedWithoutAContentLength'`单独触发Explicit。

封面故障用`--filter 'FullyQualifiedName~TestFilteringDuringASlowCoverKeepsTheNewCardUsable'`先复现未修版WaitSafely（`slow-cover-before-fix.trx`退出1）；第一次合并读取后75项中的74通过，退出专项暴露框架吞取消，修复后两专项通过，再跑联合75通过。截图终审发现封面断言误将文字字形Sprite当图片，改为精确匹配填满封面区域的Sprite，两专项重编译再通过（`slow-cover-texture-final.trx`）。没有以旧错误断言或图片占位冒充真实取图；最后源码联合命令只选两个浏览场景和封面资源store，完整记录于`cover-browser-regression-final.log`，格式收尾见`format-final.log`。

导入命令`dotnet test osu.Game.Rulesets.Mania.Tests/osu.Game.Rulesets.Mania.Tests.csproj --filter 'FullyQualifiedName~ManiaDownloadImporterTest'`；旧目录只选`TestRegisterExternalDirectoryIgnoresNonManiaBeatmapsInMixedFolder`与`TestRegisterManagedDirectoryPreservesRelativeManagedPath`，未改Register空候选合同或重跑已有过时ReturnsNull断言。BMS原包用`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj -c Release --no-build --filter 'FullyQualifiedName~BmsDownloadImporterTest'`。Release为`dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m`；所有测试附`--logger 'trx;LogFileName=<对应文件>' --results-directory artifacts/mania-sayobot-download-20261001`，完整输出留同名log。

未运行mania/BMS全套、旧官网卡片试听或工具栏全套，不把历史已核实的旧失败写成本轮重新签收；本轮不修改判定、皮肤、音频或全库扫描authority。

## 桌面验证与收尾

F盘隔离portable原生窗口探针由`ExactVisualTestGame`启动，本地化为中文，以真实鼠标执行；TestBrowser目录、步骤/控制条及“正在覆盖输入”属于诊断宿主，不是产品UI。探针工程在`.dev-cache/temp/mania-sayobot-download-render-probe/`，长期证据在`artifacts/mania-sayobot-download-20261001/render-probe/`。

最后素材窗口完成慢图重建与owner退出、取消→失败重试→单一中文提示→后台入库→稳定精确打开；420/800宽长标题、跨行及末卡展开可点，`fixture-run.json`退出0。真实站点从原工具栏槽位打开、切mania页签，以原sid查询、4K/★1–2筛选并鼠标选择原bid，截图留`Sayobot-2440353-{menu-banner,browser}.png`；这不是实际包成功证据。探针重新编译成功，其三个CA1869警告仅为证据JSON使用临时序列化选项，不属于产品Desktop构建。

实网第一尝试路由切换为tc2:25225被原单节点白名单拒绝（`Sayobot-2440353-first-rejection.*`）。无body的HEAD及GET证实站点302 Location，追加精确tc2和正反例，未改为域名通配。复验可关页后台继续，却在TLS握手收到unexpected EOF，任务明确Failed并清理暂存；完整异常见当前`Sayobot-2440353-runtime.log`，run.json退出1。Python对tc1/tc2头请求也超时且无代理。公开CDN参数及DE/USA/unicom/cmcc/Telecom五条线路全部仍指向同一个tc2，不能宣称换参就可用。公开线路来源和有界HEAD结果保存于`real-download-redirect-current.json`、`mirror-head-reachability.json`、`cdn-route-evidence.json`与`extra-route-evidence.json`。

混合实际集合2506607同样完成查询并筛出四个原生mania难度，4K/★1–2联合筛选和原bid选择正确；下载仍遇同节点TLS断开（`Sayobot-2506607-runtime.log` / run.json退出1）。该日志中的封面并发异常另行复现并修复，不能归为镜像网络故障。

修复后以独立catalogue模式复验同一混合集合：原四个mania难度、联合筛后两个难度、所选原bid正确，实际封面加载完成且没有WaitSafely/未处理异常；`Sayobot-2506607-catalogue-run.json`退出0，真实封面见`-catalogue-cover-ready.png`。该模式明确未尝试谱包下载，不替代上述退出1的live证据。第一次封面检查匹配到字形Sprite而提前通过，结果已单独保留为`-catalogue-first-unreliable-*`，不作为封面成功证据；截图终审修正精确匹配并重新执行。

真实探针仅该独立进程设置`OSU_TESTS_NO_TIMEOUT=1`移除10秒单步阈值，保留120秒整体watchdog及180秒外部截止，不改产品30分钟下载期限。没有关闭TLS校验、改HTTP、绕到官网/其它镜像或自动换源。真实包GET没有成功，未声称校验其原MD5、加入库或完成选歌；混合实际小包未成功门继续保留，素材中的混合/原身份测试单独证明软件行为。

最后Release及相关回归已通过。最终CheckDocumentation及git diff --check通过（`documentation-final.log`），收简重复状态后没有新增预算告警；保留的路径/公开制品指纹提示属于未改文档。主执行者统一审查和当前master提交，未推送。原生探针四个源码文件及复现方式已保存到`artifacts/mania-sayobot-download-20261001/probe-source/`，Release输出和隔离素材库保留供实网成功门补验。闲置obj的定点清理被自动审批策略拒绝，仅返回blocked by policy，没有详细理由；没有执行删除，记录于`probe-cleanup.json`。长期截图、日志、TRX、原目录与恢复证据全部保全。

## 保留边界

官网下载权限仍不可用，本轮交付Sayobot公共镜像；仅原生mania，不转其它玩法，不开放OMS账号或私有服务。无视频包仍可包含无视频Storyboard/图片资源，本轮不承诺视频/独立在线试听、批量整表、续传或跨重启恢复。

小包及软件门不能签收更广DPI/窗口、慢网/代理/长时、大包/真实设备/完整歌曲听感；两个源的原BMS剩余门、Skin V1/release人工门继续保留，未刷新历史ZIP安装验收。当前master收尾提交，未获push确认，不推送。
