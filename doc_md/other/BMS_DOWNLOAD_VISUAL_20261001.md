# BMS 下载页复用原浏览设计：规划与验收

> 日期：2026-10-01。归属 P1-A，接续[两源下载](BMS_DOWNLOAD_20261001.md)及[三级筛选](BMS_DOWNLOAD_FILTERS_20261001.md)。
> 当前事实见 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)，剩余体验门见 [PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md#用户指定第三方-bms-浏览下载闭环2026-10-01)，稳定边界见[第三方下载合同](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#第三方-bms-浏览下载)。

## 玩家结果与范围

本轮按用户授权，将现有 BMS 下载页尽量复用仓库保留的 osu!lazer 谱面浏览设计。搜索框在前，下载源使用原筛选页签，难度表与表内等级继续下拉联动。歌曲采用紧凑横向卡片，难度摘要悬停或点击展开按钮可选择具体谱面；图标的完整提示说明下载、取消、重试、打开及外部看谱，细进度条显示下载/入库。封面缺失时显示普通底色与谱面图标。

两源及原后台下载、原包直入 chartbms、取消/重试、精确选歌继续使用既有实现。来源未提供的官网星数、收藏和独立在线试听没有进入页面；作者标级和表内等级保持分开。没有修改规则集、归档/导入、安全预算、通知持有或皮肤素材，不把下载 UI 改动记为静线、设备或发行签收。

开工工作区干净，master@4e2e3c0；fetch成功，相对更新后的origin/master领先五个提交、落后零。这是开工在线核对，非收尾远端状态。当前分支提交，未获push授权。

## 实施前记录的闭环方案

以下方案在修改产品代码前写入 P1-A PLAN，交付时从活动计划移入本记录：

1. 搜索区沿用原字号、100px标签列、间距和深色背景；两源用原筛选行，长表名及任意原等级保留下拉。整行设置式搜索按钮改为紧凑搜索操作及回车重试，保留自动搜索、下级清空、表失败提示与旧回应隔离。
2. 卡片复用原345×80比例、BeatmapCardContent的展开/边框/阴影、原图标按钮与进度条。显示真实曲名、作者、当前难度、键数和作者标级；有界封面缺失时使用普通图标，不伪造官网资料或在线身份。
3. 展开区采用原最大200px滚动容器，难度行可点击且明确标出当前选择。图标承接下载、取消、重试、入库后精确打开和外部看谱，缺包/不支持仍禁用。分页合并保持所选原MD5和来源内真实包任务。
4. 结果沿用反向绘制顺序、10px间距和220px展开底余量，避免跨行/末卡列表被裁切或吞点击。空结果、表/来源失败及追加失败可读并可手动恢复，追加中已有卡片继续可操作。
5. 串行运行formatter、core浏览及来源/任务/封面focused，原卡片focused用于复用组件检查，Desktop Release和独立桌面探针须编译成功。桌面核对中文、多包、长标题、窄布局与末卡展开，再用两源真实小包分别完成三级筛选→下载→关页后台继续→入库→稳定后精确打开。失败逐项归因，禁止旧产物no-build或把未改玩法全套算新增验收。
6. 同步状态、计划、稳定合同、历史、使用说明和新地雷，检查文档与diff，在master提交。探针与产物位于F盘，长期证据保留artifacts；确认闲置后只清理可再生成内容，不触碰用户谱库、作者包和恢复资料。

完成标准是新外观下上述整个玩家路径可用、原等级/指定难度/后台任务语义保持，并有有效自动结果和真实桌面证据。真实大包、全窗口/DPI、设备听感、长时与最终发行组合仍保留原人工门。

## 组件复用与实现边界

| 区域 | 实际复用 | BMS 适配 |
| --- | --- | --- |
| 页面 | OnlineOverlay、OverlayHeader/Title、原配色与背景层 | 两源无需官网登录 |
| 搜索/筛选 | BasicSearchTextBox、BeatmapSearchFilterRow/FilterTabItem、OsuDropdown | 来源页签、表/等级标签与回车提交；任意原等级不变成星数 |
| 主卡片 | BeatmapCard原宽、Normal原高、BeatmapCardContent | 左侧安全缩略图，右侧真实资料及任务动作 |
| 难度展开 | ExpandedContentScrollContainer、原悬浮边框/缩放/阴影 | 原MD5行选择、当前选择底色，保留hover层 |
| 下载/更多 | BeatmapCardIconButton、BeatmapCardDownloadProgressBar、ShowMoreButton | BMS任务状态映射、取消/重试/打开、分页失败后手动恢复 |

完整原卡片、缩略图和难度行还含官网身份、星级、官网纹理/试听与下载控制器，本轮没有伪造APIBeatmapSet以套用它们。显示与来源/任务分开在BmsDownloadHeader、BmsDownloadCard中，overlay继续持有查询和既有服务；没有新增通用下载接口或未来配置层。

## 有效软件验证

所有新shell均先执行UseDevelopmentStorage.ps1；formatter、build、test由主执行者串行调度，检查时生产源文件冻结。当前改动未触及规则或导入生产代码，不重跑其全套或替代既有人工门。

| 检查 | 结果与证据 |
| --- | --- |
| core来源、任务、封面及浏览focused，Debug | 193通过、0失败；其中来源/任务/封面169、浏览24；download-visual-focused.trx |
| 浏览行为覆盖 | 真鼠标难度选择、下载/取消/失败重试/精确打开、关闭后台继续、两源三级筛选、图标与回车读表重试、缺包/不支持禁用、追加页操作、420/800宽长标题及跨行/末卡展开 |
| 原卡片focused首轮 | 12通过、2失败；download-visual-focused-first-run.trx。第三个首轮失败为新增回车重试，修复SearchTextBox默认不提交后已通过 |
| 原卡片失败基线 | 原HEAD下载源码/测试单独编译复现TestPlayButtonByTouchInput与TestThumbnailPreview；original-card-head-baseline.trx及original-card-failure-comparison.json证明身份、原消息与堆栈均一致。保全后逐字节恢复本轮文件，再重新编译当前Debug |
| 当前Debug恢复编译 | core-current-debug.log：0错误、0warning；没有把临时HEAD产物用于当前no-build |
| Desktop Release | desktop-release.log：0错误，两个既有BMS测试warning（TestSceneFilesystemBackedStoryboardFallback:151的CS8600、BmsRulesetStatisticsTest:555的CA2007），未修改或屏蔽 |
| 桌面探针Release | render-probe-build-final.log：0错误、0warning；输出和host storage均在隔离F盘目录 |
| 实际窗口fixture | RenderDownloadScene完成；fixture-runtime.log及fixture、fixture-multi-row、fixture-narrow.png覆盖中文、长标题、跨行/末卡点击及取消/失败重试后台完成 |
| 两源真实鼠标闭环 | render-probe/live-results.json：两源ExitCode=0，实际页签/菜单/动作点击后原MD5/GUID均稳定命中，直接chartbms |

主要命令与当前配置：

```powershell
. .\UseDevelopmentStorage.ps1
dotnet format whitespace osu.Game.Tests/osu.Game.Tests.csproj --no-restore --include osu.Game/Overlays/BmsDownloadOverlay.cs osu.Game/Overlays/BmsDownloadHeader.cs osu.Game/Overlays/BmsDownloadCard.cs osu.Game/Localisation/BmsDownloadStrings.cs osu.Game.Tests/Visual/Overlays/TestSceneBmsDownload.cs --verbosity minimal
dotnet test osu.Game.Tests/osu.Game.Tests.csproj --filter 'FullyQualifiedName~TestSceneBmsDownload|FullyQualifiedName~BmsDownloadClientTest|FullyQualifiedName~BmsDownloadManagerTest|FullyQualifiedName~BmsCoverResourceStoreTest' --logger 'trx;LogFileName=download-visual-focused.trx' --results-directory artifacts/bms-download-visual-20261001
dotnet build osu.Game.Tests/osu.Game.Tests.csproj -c Debug -v minimal
dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m
dotnet build .dev-cache/temp/bms-download-visual-render-probe/BmsDownloadFilterRenderProbe.csproj -c Release -v minimal
```

两项原卡片试听fixture失败仍保留，不能称整个原卡片套件或core full全绿；本轮没有重引入已删除玩法或改写官网试听预期。

## 桌面观察与收尾

每源使用独立隔离数据根，实际窗口点击来源页签、Satellite表菜单和sl11等级菜单，再点击下载图标、关页等后台入库、重新打开并点击完成图标。等待选歌过滤和推荐延迟稳定后，同时核对carousel/全局原GUID和原MD5。不是直接调用下载方法冒充UI点击，也不以首次选歌尚未稳定的瞬时状态判定成功。

| 公开小包 | Ginger Rush | 616 / Alvorna |
| --- | --- | --- |
| 实际压缩字节 | 237,383 | 56,925 |
| 入库谱面 | 18 | 18 |
| 筛选后的卡片 | 仅目标原谱 | 仅目标原谱 |
| 等级及稳定选歌 | sl11、原MD5/GUID均命中 | sl11、原MD5/GUID均命中 |
| 库路径 | 直接chartbms | 直接chartbms |

截图分别保存两站browser、levels、completed、song-select及toolbar；fixture图另覆盖无图普通卡、长中英文标题、多卡/单列末卡选择和下载。图中TestBrowser侧栏、输入控制提示属于隔离诊断宿主，产品下载页不包含这些元素。自动/桌面证据不代替所有窗口/DPI、代理慢网、大包、完整歌曲及设备听感。

首次Ginger鼠标探针已确实登记Queued任务，但同一step在模拟输入消费前立即读取GetTask，捕获旧null导致探针断言失败。日志、截图和进程结果保存在render-probe/first-attempt。只将探针改为等待实际任务登记，未修改产品任务时序；随后两源完成。完整表探针沿此前做法仅对独立进程设置OSU_TESTS_NO_TIMEOUT=1，保留120秒游戏watchdog和3分钟外层截止，产品及focused期限没有改变。

本轮证据全部归artifacts/bms-download-visual-20261001，旧下载/筛选证据没有覆盖。独立终审未发现阻断交付的问题；CheckDocumentation.ps1通过，检查188份Markdown、1,842条相对链接、241处本地Markdown锚点及121条memory wiki链接，git diff --check通过。两项检查的日志保存在同一证据目录。

临时HEAD对照后，七份产品/测试源码逐字节恢复的SHA核对已保存为source-byte-restoration.json；桌面探针的四份源码及SHA存入render-probe/source。探针进程均已退出，F盘探针目录保留用于复现，本轮没有新建checkout。自动审批策略拦截了临时基线备份目录.dev-cache/temp/bms-download-visual-baseline的递归清理，工具只返回“blocked by policy”，未提供更具体原因；该清理未完成，目录保留。

以上闭合本轮下载页视觉复用范围，不扩大原皮肤、长期与发行签收。
