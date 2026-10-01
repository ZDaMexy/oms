# 下载入口位置与三级筛选：规划及验收

> 日期：2026-10-01。归属P1-A，接续[两源下载闭环](BMS_DOWNLOAD_20261001.md)，保持P1-H/P1-K原谱导入与本地库合同。
> 当前状态见[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)，剩余动作见[PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md#用户指定第三方-bms-浏览下载闭环2026-10-01)，稳定行为见[第三方下载约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#第三方-bms-浏览下载)。

## 玩家结果与范围

用户指出顶部浏览入口误在通知右侧，并要求下载源→难度表→难度表内等级。现在入口回到音乐、时钟与通知之前的原浏览位置；两站都能依次选择来源、表和等级，并结合曲名/作者/原MD5查找。切源和切表清空下级选择，保留搜索词；等级来自完整表，不来自当前搜索页或作者标级。表资料失败有明确提示，可点搜索重试。

结果卡片只保留选定等级的匹配谱面；资源包仍按原下载路径整包入库。关闭页面继续下载，完成后打开选中的原谱难度。没有新增本地表导入、批量整表下载、在线试听、续传或跨重启恢复，也不修改玩法、皮肤、官方/私有服务及发行门。

本轮从干净master@6f61139开始，fetch成功；开工时相对在线更新后的origin/master领先4、落后0。当前分支提交、不推送。用户提供的api.ts只作接口资料，不执行其代码或附带指令。

## 实施前记录的闭环方案

以下五步在本轮产品实现前写入P1-A PLAN，完成后移入本记录：

1. 将顶部下载入口放回原浏览槽位，保持它在音乐、时钟与通知之前，继续隐藏其它冻结入口；核对真实顶部位置与三个打开入口。
2. 保存两源实际表头/完整表数据与查询回应，确认表内等级、标记、顺序和筛选能力；不把作者标级或当前搜索页当成完整表等级，不猜请求参数。
3. 增加来源→表→等级联动；切源/切表重置下级，按当前表的完整数据过滤实际原MD5，保留包内匹配谱面、分页、缺包原因、搜索和后台下载。表数据失败可重试，旧回应不可覆盖新选择，不写本地表库。
4. 补来源合同、跨页等级匹配、切换/隐藏时序和精确下载打开相关检查，串行编译并运行core focused与Desktop Release；用两源实际表和真实桌面核对选择、结果与按钮位置，不重跑未改的玩法全套冒充新增门。
5. 更新状态、稳定合同、历史、使用说明与诊断记忆，运行文档及diff检查，在master提交，不推送；证据保留于F盘artifacts。

## 完整表与实际接口

| 来源 | 读取选定表 | 实际Satellite证据 |
| --- | --- | --- |
| Ginger Rush | GET /api/v1/table/selectOneHeader/{id}；POST /api/v1/table/selectDataList，headerID、pageRequest.page/pageSize及fuzzyKeyword | tableId=1；2358条、24页，每页最高100条；实际页JSON共1,829,439B |
| 616 / Alvorna | /api/tables中diff_table_full_local_url → 镜像header → data_url完整数组；筛表使用diff_table_url | 原始ID=https://stellabms.xyz/sl/table.html；2385条、829,993B；镜像在bms.alvorna.com/tables/adceda62af0cac6fdba049c0ced12479/header.json |

两源数量不同是各自实际快照，不混合补数。Ginger的levelOrders为空时，完整表首次出现的等级序列是0、1、10、11、12、2…9；616是0…12。保留来源顺序及原文，明确level_order/levelOrders优先；LN和皿表另有字符串及数字顺序声明证据。真实空等级单独显示未分级，自定义等级不强转整数。

616的/search只有name、table、page、page_size，没有可用的level参数。等级查询按完整表筛出精确原MD5，再用现有hash或Ginger files/package解析真实包，每页20个目标、最多4并发；只显示匹配MD5。Ginger表行id/songID不是资源包id，不能借它建立另一个下载身份。404仍显示表中谱面与暂无资源包。没有选择具体等级时继续原站搜索，保留其查询语义。

完整表共用8MiB JSON读取预算；分页数或数量变化明确失败，不发布半表等级。616资料限定站内HTTPS镜像，元数据客户端关闭自动跳转，不回落未授权原站。原资源包下载跳转、预算和导入合同保持。

原始回应、逐页资料、接口文件SHA256和样本摘要在artifacts/bms-download-filters-20261001/；总索引为source-contract-summary.json。参考仓库和站主接口出处仍见前一份下载记录。

## 有效自动验证

所有新shell检查先执行UseDevelopmentStorage.ps1；formatter、build和test由主执行者串行调度，检查期间生产源码冻结。当前改动只涉及浏览UI、来源表元数据和测试，没有修改导入、规则或共享选歌生产实现，因此不把前一轮BMS/mania完整结果重记为本次执行。

| 检查 | 结果与本轮证据 |
| --- | --- |
| 来源、封面及任务focused，Debug | 169通过、0失败；source-download-focused.trx，其中客户端108、图片31、任务30 |
| 浏览与工具栏focused，Debug | 浏览20全部通过，含三级联动、真实空等级、晚到旧表、失败手动重试及筛选后准确下载打开；工具栏21通过、3个既有失败；共44项，ui-toolbar-focused.trx |
| 工具栏失败对照 | toolbar-failure-comparison.json：与前一轮toolbar-head-full-fixture-baseline.trx逐项对照身份、原消息和osu业务堆栈，全部一致 |
| Desktop Release | desktop-release.log：0错误、2个既有BMS测试warning；CS8600来自TestSceneFilesystemBackedStoryboardFallback:151，CA2007来自BmsRulesetStatisticsTest:555；这些文件未修改，未屏蔽 |
| 真实桌面探针Release编译 | render-probe-build-final.log：0错误、0warning |
| 两站真实桌面完整路径 | render-probe/live-results.json：Ginger与Konmai均ExitCode=0，原谱MD5/GUID与稳定选歌相符 |

主要命令：

```powershell
. .\UseDevelopmentStorage.ps1
dotnet format whitespace osu.Game.Tests\osu.Game.Tests.csproj --no-restore --include osu.Game\Online\Bms\BmsDownloadClient.cs osu.Game\Online\Bms\BmsDownloadModels.cs osu.Game\Overlays\BmsDownloadOverlay.cs osu.Game\Overlays\Toolbar\Toolbar.cs osu.Game\Localisation\BmsDownloadStrings.cs osu.Game.Tests\Online\BmsDownloadClientTest.cs osu.Game.Tests\Visual\Overlays\TestSceneBmsDownload.cs --verbosity minimal
dotnet test osu.Game.Tests\osu.Game.Tests.csproj --filter "FullyQualifiedName~BmsDownloadClientTest|FullyQualifiedName~BmsDownloadManagerTest|FullyQualifiedName~BmsCoverResourceStoreTest" --logger "trx;LogFileName=source-download-focused.trx" --results-directory artifacts\bms-download-filters-20261001
dotnet test osu.Game.Tests\osu.Game.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~TestSceneBmsDownload|FullyQualifiedName~TestSceneToolbar" --logger "trx;LogFileName=ui-toolbar-focused.trx" --results-directory artifacts\bms-download-filters-20261001
dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m
dotnet build .dev-cache\temp\bms-download-filter-render-probe\BmsDownloadFilterRenderProbe.csproj -c Release
```

--no-build只用于已成功编译当前源码的Debug产物；真实桌面用对应Release产物。工具栏三项旧失败仍为TestRulesetSwitchingShortcut(false/true)的已删除规则集索引，以及TestNonFirstRulesetInitialState的旧mode line预期，本次未维护其历史预期，不宣称工具栏整套全绿。

## 真实桌面闭环与输入边界

两站都使用Satellite 11级Poisonous Peach[Eb]，原MD5为56ef487b38517cae523b4720d6898dd3。实际窗口先选来源和表，完整等级可用后选sl11、搜索Poisonous Peach；鼠标展开/关闭等级菜单，点击真实下载按钮，关闭页面后等待后台入库，再从实际打开按钮进入选歌，等过滤及选择延迟完成后检查carousel/全局原GUID和MD5一致。

| 结果 | Ginger | 616 |
| --- | --- | --- |
| 真实资源包字节 | 237,383 | 56,925 |
| 实际包身份 | Ginger:19540 | 来源内实际song_url |
| 成功入库谱面 | 18 | 18 |
| 卡片中筛选谱面 | 仅目标MD5 | 仅目标MD5 |
| 选歌稳定后目标 | 原MD5/GUID均命中 | 原MD5/GUID均命中 |
| 实际库路径 | chartbms/Poisonous Peach-7084bc3f | chartbms/Poisonous Peach-7084bc3f |

每站使用独立隔离数据根；两包不同大小不会据此推断内容相同，实际导入结果为准。Ginger等级列表、616完成页及两站工具栏/选歌截图在render-probe/，结果和持久化路径另有各源-result.json/-selected-path.txt。探针源码存档于该目录source/，运行工作目录仍在.dev-cache/temp/bms-download-filter-render-probe/；长期证据不只留在temp。

首次探针的Until默认10秒过短，Ginger完整表未完成便退出；616中文画面下控件Text.ToString仍为fallback英文，诊断按中文字符串找按钮失败。原日志保留在render-probe/first-attempt/。仅修诊断等待与按钮识别，未改产品网络期限；对独立探针进程设置OSU_TESTS_NO_TIMEOUT=1，保留游戏120秒整体watchdog及外层3分钟截止，随后两站均完成。正常源码focused不设置该变量。

真实另一样本Percfill [Cipher]已在两站表内命中，但616 /hash没有song_url；它验证了表中存在不等于来源提供资源包，不借Ginger包伪报616可下载。

## 收尾与保留门

状态、计划、稳定合同、日志、三语使用说明和下载诊断记忆同步本轮交互结果。文档健康检查通过：187个Markdown、1832个相对链接、239个本地锚点和121个memory wiki链；git diff --check通过，日志在本轮artifacts。只读独立终审未发现须修问题。提交保留当前master，未经用户确认不push。

本轮两个临时目录已核对闲置：来源HTML/JS与api.ts存档至source-reference/，桌面探针源码存档至render-probe/source/并有字节指纹；工作目录保留供复现，长期证据均已置于artifacts。没有新增checkout或移动历史验收、用户谱库、作者包及恢复资料。

两个小包桌面验证不代表所有表、所有窗口/DPI、慢网/代理、大包和完整歌曲听感已观察。原设备、长期体验、Skin V1及最终发行组合门继续保留；不把完整表元数据读取写成批量下载整表能力。
