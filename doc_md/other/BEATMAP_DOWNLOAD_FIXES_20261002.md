# 游戏内下载审查修复与验证（2026-10-02）

此记录承接[全量审查](BEATMAP_DOWNLOAD_REVIEW_20261002.md)的六项确认问题。修复授权覆盖实现、验证、文档与记忆同步及当前分支提交；推送仍须另行确认。当前能力以[P1-A 状态](../subline/P1-A/DEVELOPMENT_STATUS.md)为准，稳定合同见[下载约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#第三方-bms-浏览下载)。

## 开工前方案与退出条件

基线 `491c822938f309eca6a31896e6d964a3b3bb24e0`，工作区干净；成功 fetch 后 master 相对 origin/master 领先8项、无落后。此基线含审查文档，生产实现仍是10月1日下载增改。修复按以下闭环执行，不扩大官网/私有服务、玩法或皮肤产品面。

| 审查问题 | 具体修复 | 必须证明的玩家结果 |
| --- | --- | --- |
| D1 不支持归档使任务停在入库中 | 仅在归档提取输入边界把不支持方法转换为明确坏包失败；保留 manager 的程序错误暴露 | 真实 importer 和 manager 收尾后失败，同包可重试、下一合法包可完成、退出释放正常 |
| D2 路径层数放大内存 | 在拆分和累计前缀之前限制相对路径512字符、深度32；保留 Windows 路径安全检查 | 超长/过深包失败且不发布；合法子目录和旧谱库保持 |
| D3 谱文本解析没有独立预算 | 单 classic 谱文本32MiB、合计128MiB；声明与实际读取双重限制，ZIP/RAR/7z提取共用预算；资源仍保留单项2GiB/展开8GiB | 超预算下载在 folder importer 之前失败；普通资源及正常谱面不受谱文本限制 |
| D4 ZIP 资源损坏仍成功入库 | 流式核对实际大小及 CRC，目标谱 MD5 继续独立校验 | 谱MD5正确但音频/图片损坏的包在发布前失败；正常ZIP与数据描述符可入库 |
| D5 目录失败无法用搜索动作恢复 | 保存目录读取失败状态，搜索图标/Enter显式重读失败目录；目录与歌曲各自提示 | 歌曲成功仍保留目录故障；服务恢复后可继续表→等级筛选，无自动循环请求 |
| D6 已处理失败重复英文诊断 | 目录、歌曲、等级及任务可恢复失败只记录完整 Network 日志，由已有中文页面/通知承接 | 玩家只收到对应本地化反馈，完整异常仍可诊断；其它程序错误继续暴露 |

文件责任分离：归档实现及其专项测试、浏览页及其界面测试各自独立维护；主执行者负责任务反馈、真实集成、统一审阅、构建/测试、文档和提交。所有共享构建/测试串行执行，相关源文件在验证期间冻结。每个新验证 shell 先执行 `. .\UseDevelopmentStorage.ps1`；临时内容放 F 盘 `.dev-cache/temp/`，长期证据放 `artifacts/beatmap-download-fixes-20261002/`。

验收顺序：先将审查中的真实坏包/鼠标/键盘失败转换为维护中的回归夹具，检查无发布、旧库保全及暂存清理；再检查两源/Sayobot查询、任务、封面、浏览、BMS/mania导入与指定难度导航，最后运行 Desktop Release、文档检查与差异检查。七项导航旧失败须按原审查的逐项身份、消息和业务堆栈对照，不据失败数量判断，也不记为已修复。来源实网 TLS、真实大包/设备/听感和皮肤/发行人工门保留。

## 已完成的实现与边界

六项均沿上述方案修复：坏包在提取边界成为可重试失败，路径和classic谱文本在进入全文解析前有界，ZIP资源核对实际大小/CRC；目录与歌曲提示分离，搜索/Enter仅重读失败目录或等级。正常浏览/后台任务、原MD5和持久化Guid、旧库及已提交目录的部分发布合同保持。目录、查询、等级和任务已处理失败记录完整Network/默认Verbose日志，中文页面/通知承接玩家反馈；清理失败和程序错误保持原暴露。

落实D1/D4时用实际小型坏包确认了同一收尾问题的更多触发方式，没有扩大manager的可恢复异常集合。固定SharpCompress0.39.0使用的异常并不统一属于ArchiveException，转换只包围归档提取，内部异常类型核对库程序集，通用异常另匹配确切声明类和方法：

| 真实输入 | 原异常 / 确切库来源 | 修复前证据 |
| --- | --- | --- |
| ZIP不支持method77 | NotSupportedException | 原审查真实manager；维护中的同包重试/退出回归 |
| DEFLATE保留BTYPE=3 | ZlibException | `deflate-boundary-before.trx` |
| ZIP LZMA非法properties | 内部LZMA.InvalidParamException | `codec-boundaries-before.trx` |
| ZIP Zstd无效frame | ZstdSharp.ZstdException | 同上 |
| 7z LZMA2非法chunk控制字节 | 内部LZMA.DataErrorException | 同上 |
| stored RAR声明展开长度多1字节 | InvalidOperationException / RarStream.Read | 同上 |
| ZIP Bzip2 block CRC翻转 | InvalidOperationException / CBZip2InputStream.Cadvise | 同上 |
| XZ合法SHA256头而当前库不支持该check | NotImplementedException / XZStream.AssertBlockCheckTypeIsSupported | `codec-extra-boundaries-before.trx` |
| ZIP LZMA空properties | IndexOutOfRangeException / LzmaStream构造器 | 同上 |
| 7z StartHeaderCRC单字节翻转 | InvalidOperationException / SevenZip.ArchiveReader.ReadDatabase | `sevenzip-header-before.trx` |

上述输入由入库测试确定生成；未依赖网络、第三方包或外部打包工具。完整红身份保留，后续绿结果不覆盖它们。另用真实manager+importer证明method77和坏DEFLATE均收尾为Failed、无发布、暂存已清理，同包重试能完成并复用原身份，下一合法包可完成，保留失败任务退出也正常；故障注入的程序InvalidOperation仍使Completion/Dispose明确抛出。

RAR/7z实际提取失败时必须在入流using作用域内先reader.Cancel再抛出，之后才能Dispose：SharpCompress的未读完EntryStream释放会跳读剩余数据。独立计数实验先读1字节，普通释放额外消费72字节，取消后释放额外0字节；源码、原失败位置观察探针、有效计数结果均见 `archive-fixtures/`。它证明库释放语义，不是生产解压量或峰值内存测量。正常storedRAR/Copy7z夹具另经既有7-Zip独立核验格式；ZIP非零目录数据按PKWARE §4.3.8拒绝，正常空目录及data descriptor保持。

独立SharpCompress探针restore出现NU1902，产品工程已存在针对受影响WriteToDirectory API的精确豁免；本下载沿stream读取并自行约束路径。未借此升级依赖、全局屏蔽审计或增加解压API。这项探针警告与生产构建警告分开记录。

## 验证结果

源码冻结、格式化后从当前改动重新编译；所有共享验证串行执行，没有用旧产物或锁冲突充当通过结果。长期证据根为 `artifacts/beatmap-download-fixes-20261002/`，最终源码清单为 `final-source-freeze.json`。

| 有效门 | 配置与结果 | 证据 |
| --- | --- | --- |
| BMS/616/Ginger资料、Sayobot、两后台任务、共享封面、两浏览页 | Debug下载相关336通过；含真实失败任务退出/重试、程序错误负例、目录恢复与完整日志/单一反馈 | `core-final.trx` / `.log`、`core-class-results.json` |
| 原难度组选歌 | 同一次Debug联合运行10通过/7既有失败，逐项身份/消息/业务堆栈一致 | `navigation-final-comparison.json` |
| BMS下载入库及原目录/读取边界 | Release124通过：下载入库78、普通导入38、文件位置6、sanity2 | `bms-imports-final.trx` / `.log` |
| mania下载及原目录/读取边界 | Release55通过/1既有失败：下载49全部通过，其余原目录/库检查6通过/1失败 | `mania-imports-final.trx` / `.log`、`mania-final-comparison.json` |
| Desktop | Release构建成功，最终命令0错误/0警告 | `desktop-release-final.log` |
| 格式化 | Game相关5个CS文件及BMS相关2个CS文件完成，最终report空；补测试引用后该文件再格式化并重新编译 | `format-core-final/`、`format-bms-final/`、`format-core-compile-fix/` |

页面验证使用真实游戏的headless场景、鼠标/键盘输入及组件布局。目录503先于歌曲成功，警告仍可见，鼠标420宽/Enter800宽恢复到指定表等级；成功目录不随关键词重复读取，目录恢复不清掉独立搜索失败，隐藏/切源迟到回应不回写。英文警告真实组件宽度不越内容区；中文提示和通知由游戏当前LocalisationManager对可见组件解析，日志按完整LogEntry正向检查。它不冒充本次新桌面截图或实际中文字形验收。

主要命令如下；每个新shell均先执行存储入口。restore还分别覆盖三个测试工程；最终test没有使用`--no-build`。

```powershell
. .\UseDevelopmentStorage.ps1
dotnet restore osu.Desktop.slnf
dotnet restore osu.Game.Tests/osu.Game.Tests.csproj
dotnet restore osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj
dotnet restore osu.Game.Rulesets.Mania.Tests/osu.Game.Rulesets.Mania.Tests.csproj
dotnet format osu.Game.Tests/osu.Game.Tests.csproj --no-restore --include osu.Game/Online/Bms/BmsDownloadManager.cs osu.Game/Overlays/BmsDownloadOverlay.cs osu.Game/Localisation/BmsDownloadStrings.cs osu.Game.Tests/Online/BmsDownloadManagerTest.cs osu.Game.Tests/Visual/Overlays/TestSceneBmsDownload.cs --report artifacts/beatmap-download-fixes-20261002/format-core-final
dotnet format osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore --include osu.Game.Rulesets.Bms/Beatmaps/BmsDownloadImporter.cs osu.Game.Rulesets.Bms.Tests/Beatmaps/BmsDownloadImporterTest.cs --report artifacts/beatmap-download-fixes-20261002/format-bms-final
dotnet test osu.Game.Tests/osu.Game.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~BmsDownloadClientTest|FullyQualifiedName~BmsDownloadManagerTest|FullyQualifiedName~ManiaDownloadManagerTest|FullyQualifiedName~SayobotClientTest|FullyQualifiedName~BmsCoverResourceStoreTest|FullyQualifiedName~TestSceneBmsDownload|FullyQualifiedName~TestSceneManiaDownload|FullyQualifiedName~TestSceneBeatmapCarouselDifficultyGrouping" --logger "trx;LogFileName=core-final.trx" --results-directory artifacts/beatmap-download-fixes-20261002
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~BmsDownloadImporterTest|FullyQualifiedName~BmsImportIntegrationTest|FullyQualifiedName~FilesystemBeatmapLocationTest|FullyQualifiedName~FilesystemSanityCheckHelpersTest" --logger "trx;LogFileName=bms-imports-final.trx" --results-directory artifacts/beatmap-download-fixes-20261002
dotnet test osu.Game.Rulesets.Mania.Tests/osu.Game.Rulesets.Mania.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~ManiaDownloadImporterTest|FullyQualifiedName~ManiaImportIntegrationTest|FullyQualifiedName~ManiaFilesystemLibraryTest" --logger "trx;LogFileName=mania-imports-final.trx" --results-directory artifacts/beatmap-download-fixes-20261002
dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m
powershell.exe -NoProfile -ExecutionPolicy Bypass -File artifacts/beatmap-download-fixes-20261002/compare-regressions.ps1 -Scope All
```

先前有效BMS测试编译仍提示已存在的CS8600（storyboard fixture）及CA2007（statistics fixture），本次未修改对应文件。首轮core编译因新增真实manager测试缺少Rulesets引用而失败，未运行测试；补引用、再次格式化后重编译，最终353项联合结果才是有效门。该失败保留 `core-compile-before.log`，不称既有基线。前面真实坏包红结果与独立探针警告均各自保留。

## 既有失败与未签收门

七项导航失败在原审查中已经从改动前detached `24f621e`重新restore/build复现；此次再次对照其原始 `navigation-baseline.trx`，而非只比失败数量。完整身份为：

- `TestOpenCloseGroupWithNoSelectionKeyboard`
- `TestKeyboardGroupToggleCollapse_SelectionNotContained`
- `TestGroupSelectionOnHeaderKeyboard`
- `TestCarouselRemembersSelection`
- `TestBasicFiltering`
- `TestKeyboardGroupToggleCollapse_SelectionContained`
- `TestGroupSelectionOnHeaderMouse`

mania原目录失败为 `TestRegisterExternalDirectoryWithOnlyNonManiaBeatmapsReturnsNull`，消息为“The mania directory contains no valid mania charts.”，与10月1日原始结果的业务堆栈一致。当前P1-H合同明确对无有效mania目录失败；不为旧null预期把它伪装成成功。原夹具维护与七项导航检查仍是具名欠账，不称相关联合回归或整个仓库全绿。比较脚本只统一换行、去掉源码路径/行号，保留消息与业务方法；实际结果分别见两个final-comparison.json。

未重新访问来源下载节点、下载实网包、开桌面实机或重打发行包。Sayobot真实下载TLS阻塞、大包/慢网/代理、完整歌曲与听感、窗口/DPI/设备/长时及Skin/发行人工门继续保留；此次小型有界坏包不替代这些门，也未执行GiB内存耗尽压力。

## 文档、记忆与收尾

同步P1-A状态、剩余计划、下载合同和日志，向mainline回写修复门结果；三语README说明目录手动恢复与坏包失败。原审查报告只追加后续入口，保留原六项发现及红证据。独有异常类型/抛出来源、central/local头替换、reader释放继续读取、文本预算及本地化测试误读收进对应memory，索引随之维护。未机械刷新其它子线或旧人工日期。

`powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\CheckDocumentation.ps1`通过：192个Markdown、1903个相对链接、256个本地Markdown锚点、121个memory wiki链；原公开制品/参考指纹及通用路径提示保留，未为消除提示改写历史。`git diff --check`通过，STATUS/PLAN行数和字符预算、memory单行预算均保持；源码冻结清单与验证后文件一致。检查日志为 `documentation-check.log`，差异结果为 `diff-check.log`。

两个本次自建暂存目录 `.dev-cache/temp/download-fix-bms-fixtures/` 和 `.dev-cache/temp/beatmap-download-fixes-20261002/` 已定点检查；探针源码及工具生成参考包与artifacts保存证据哈希一致。清理命令被自动安全审查在执行前拒绝，仅返回“blocked by policy”，没有执行递归删除。两个目录继续保留，`cleanup.json`明确记录清理未完成；没有新建或操作其它工作副本，也未清空依赖缓存、用户内容或长期证据。

收尾在当前master提交，未经用户确认不推送。实网、设备与发行人工门不因文档或提交完成而关闭。
