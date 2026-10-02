# 游戏内谱面下载增改全量审查

> 2026-10-02；审查基线 master@f0f52a2，范围 `24f621e..f0f52a2`。开工工作区干净，fetch 成功，相对刷新后的 origin/master 领先7、落后0。归属 [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md)，沿用 P1-H/P1-K 导入合同。

## 结论与范围

确认六项需修复问题：三项 P1、三项 P2，均在新增 BMS 下载路径。正常来源筛选、后台下载、原难度打开与 Sayobot 软件路径继续有有效证据，但 BMS 不支持包的收尾、解析内存与路径预算必须先收口，不能据正常小包通过签收完整坏包边界。本轮只审查并记录问题，没有修改生产源码、既有测试或产品合同，没有推送。

审查四次下载提交 `6f61139`、`4e2e3c0`、`1cedd56`、`f0f52a2` 的全部增改：Ginger Rush/616、来源→表→等级、原浏览视觉复用、Sayobot 原生 mania。覆盖来源/模型、任务/通知、两导入器及现有目录/Realm链、共享封面/卡片、两个页面/入口、MainMenu/SongSelect/carousel、本地化、专项测试及状态/说明；67个文件。来源、导入、界面只读并行审查，主执行者负责导航及串行验证。

## 已确认问题

### D1 · P1 · 不支持的 BMS 归档会永久停在入库中

[BmsDownloadImporter](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDownloadImporter.cs) 第76–78行只转换 ArchiveException/ExtractionException/EndOfStreamException。远端 ZIP 使用不支持的 method 77 时，SharpCompress 在第191行抛 NotSupportedException，越过 [BmsDownloadManager](../../osu.Game/Online/Bms/BmsDownloadManager.cs) 第200行的可恢复失败边界。

真实 manager + 真实 importer + 本地 HTTP 包夹具确认：Completion fault，状态保持 Importing；取消无效，同包重试继续返回原任务。下一个合法包可完成，故不是整个队列死锁；manager.Dispose 第287行再次抛同一异常，随后会跳过 OsuGame 中其余析构步骤。应在明确的归档输入边界把“不支持”转换成坏包失败，保持程序错误暴露，不能用扩大 manager catch 代替。

### D2 · P1 · BMS 路径预留可被短输入放大为大量内存

[BmsDownloadImporter](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDownloadImporter.cs) 第261行的 reservePath 在没有总路径长度或层数限制时，为每个分段保存累计前缀（281行）。空间随层数平方增长，条目50,000限制不约束一个条目展开出的隐式目录；检查发生在真正创建路径之前。

安全探针调用生产 reservePath：4,104字符、2,048层的路径获准，保留2,049个前缀，累计4,198,408字符，分配8,986,216字节。ZIP的两字节文件名长度允许约64KiB元数据；按该算法约32K层意味着约2GiB UTF-16前缀内容，可能耗尽客户端内存。后者是代码与格式上限的推导，未执行极限压力。应在拆分及累计字符串分配之前限制路径长度、层数。

### D3 · P1 · BMS 谱文本沿用资源的 GiB 预算后全文解析

[BmsDownloadImporter](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDownloadImporter.cs) 第31行及第185–192行对谱文本和普通资源统一允许2GiB；第91行交给 [BmsFolderImporter](../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs) 后，文件完整进入 MemoryStream（320–322），[BmsBeatmapDecoder](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs) 再复制、解码全文并保存各行，集合内容还累积在 hashableBeatmaps（359–360）。缺少单谱及合计谱文本解析预算。

安全探针把仅一个对象的有效谱附加大量可忽略文本：包8,360字节，展开谱8MiB，仍成功入库，导入期间累计分配76,237,248字节。这是累计分配，不能误写成峰值驻留；GiB级输入造成多份全文内存的风险由代码证明，未尝试耗尽机器内存。应在新下载边界区分谱文本和普通资源预算，并约束全部待解析谱文本；不把普通本地目录合同改成隐式兼容分支。

### D4 · P2 · ZIP 资源损坏仍作为成功下载发布

[BmsDownloadImporter](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDownloadImporter.cs) 第191–192行直接读取条目，第247–256行只限制实际字节及计算谱MD5，没有核对条目声明大小/CRC；当前 SharpCompress 路径不会代为验证 ZIP CRC。

夹具保持目标谱MD5正确，只翻转 stored 资源一字节且不更新CRC，仍成功发布一个集合，复制出的资源内容已改变。所选谱MD5不能证明音频/图片完整，损坏资源会以“已入库”呈现，游玩可能无声或坏图。应在归档输入边界校验实际条目内容，损坏包须在发布之前明确失败；验证不能只围绕目标谱的MD5。

### D5 · P2 · 表目录失败后搜索/回车无法恢复三级筛选

[BmsDownloadOverlay](../../osu.Game/Overlays/BmsDownloadOverlay.cs) 第133–138行的 Header.Retry 只读选中表等级和歌曲。首次目录请求失败时没有选中表，目录不会重读；第275行歌曲搜索成功还会清掉目录失败提示。服务恢复后玩家仍只有“所有难度表”，只能关闭重开或切源恢复。

真实界面夹具先让目录503、让歌曲查询成功，再恢复目录，分别点击搜索图标与按Enter。两次歌曲查询均发出，但目录请求计数保持1，目录仍不可选。应让手动重试覆盖失败的目录，并保持目录与歌曲状态各自准确；不自动循环请求。

### D6 · P2 · 已处理的 BMS 来源失败重复弹英文诊断

[BmsDownloadOverlay](../../osu.Game/Overlays/BmsDownloadOverlay.cs) 第206、287、334行及 [BmsDownloadManager](../../osu.Game/Online/Bms/BmsDownloadManager.cs) 第202行仍调用 Logger.Error。失败已由中文页面/通知处理，但 [OsuGame](../../osu.Game/OsuGame.cs) 第1984行转发器又生成英文系统通知；发行版还会附带自动报告提示并消耗诊断提示额度。Sayobot 同类路径已避免此问题。

真实界面503查询夹具确认重试按钮可见后仍存在对应英文 SimpleErrorNotification。应沿 Sayobot 已确认方式在 Network 日志保留完整异常，由本地化页面/任务通知处理预期失败，保持其它程序错误的既有转发。

## 验证与失败身份

所有新shell先执行 `. .\UseDevelopmentStorage.ps1`，共享构建/测试串行。主工作区源码始终为 f0f52a2；对照工作副本为 F盘临时 detached `24f621e`，从该源码重新restore/build，不使用旧产物。临时源在 `.dev-cache/temp/download-review-20261002/`，长期证据在 `artifacts/download-review-20261002/`。

| 验证 | 结果 | 证据 |
| --- | --- | --- |
| 当前 core 下载与导航，Debug | 共338项：下载321通过；difficulty grouping 10通过、7失败 | `download-regression.trx` / `.log` |
| 七项导航失败基线对照，Debug | 改动前重新编译后同七项失败；逐项消息、业务堆栈一致 | `navigation-baseline.trx` / `.log`、`navigation-baseline-comparison.json` |
| 新 UI 复现，Debug | 三项按期望恢复/单提示断言失败：鼠标目录重试、Enter目录重试、重复诊断 | `ui-review.trx` / `.log` |
| BMS/mania 归档安全探针，Release | 六项观察：不支持格式、资源CRC、谱文本、真实manager传播、路径放大与已排除的目录兼容候选 | `import/import-review-probe.json`、ZIP/OSZ素材、`import-review-final.log` |
| 既有 BMS/mania 下载导入专项，Release | BMS43通过、mania49通过 | `bms-import-regression.trx`、`mania-import-regression.trx` |

下载321项包含 BMS client108/manager30、Sayobot client54/manager54、封面35、BMS页面24/mania页面16。core命令过滤上述七个套件及 `TestSceneBeatmapCarouselDifficultyGrouping`；`--no-build --no-restore`使用本轮 UI probe 已成功编译的当前 Debug core tests。新 UI probe 独立工程只运行三个新增方法，不重跑继承的全部用例；归档探针使用 `dotnet run --project .dev-cache/temp/download-review-20261002/import/ImportReviewProbe.csproj -c Release -- artifacts/download-review-20261002/import`。

导入命令为 `dotnet test <对应ruleset tests工程> -c Release --no-restore --filter 'FullyQualifiedName~BmsDownloadImporterTest'` 或 `ManiaDownloadImporterTest`，均先restore并由test编译当前工程；所有test附TRX及本轮artifacts结果目录。BMS tests仍有未改文件的CS8600/CA2007两个警告；临时探针有单次JSON选项CA1869警告，不能写成产品新增编译失败。

七项既有失败为 TestOpenCloseGroupWithNoSelectionKeyboard、TestKeyboardGroupToggleCollapse_SelectionNotContained、TestGroupSelectionOnHeaderKeyboard、TestCarouselRemembersSelection、TestBasicFiltering、TestKeyboardGroupToggleCollapse_SelectionContained、TestGroupSelectionOnHeaderMouse。对照只去掉源码路径/行号差异，未只按失败数量判定；它们不列入本次增改finding，也未声称欠账已修复。

## 排除项与审查边界

mania 的2字节空DEFLATE目录候选已排除：虽然.NET可读，[PKWARE APPNOTE](https://pkware.cachefly.net/webdocs/casestudies/APPNOTE.TXT) §4.3.8要求目录不包含文件数据，不能据该容忍行为提出无证据的兼容修复。既有程序错误、取消/部分发布及不覆盖玩家资产继续原合同。

完成仅通知、Player期间拒绝跳转、最终GUID/ruleset可用重查、正常精确选歌及共享封面慢读/退出未发现本次新增可确认问题。先前封面WaitSafely及字形Sprite误判已修，不重复报告。Sayobot实网TLS失败仍沿[10月1日原记录](MANIA_SAYOBOT_DOWNLOAD_20261001.md)，本轮未尝试新的真实包下载，也未关闭TLS或更换来源。

本轮没有跑规则集全套、重新签收Desktop Release、真实大包/设备/听感或皮肤/发行人工门。已有专项通过不覆盖新增复现缺口；审查完成不等于六项修复完成。后续修复退出条件归[P1-A计划](../subline/P1-A/DEVELOPMENT_PLAN.md#下载增改审查的修复门2026-10-02)。

复现源和运行方式另存本轮artifacts的 `probe-source/`，不只留临时目录；导航对照证据已保存，确认闲置且无用户数据的F盘临时工作副本已移除，见 `baseline-worktree.json` / `review-cleanup.json`。主工作区原缓存及其它任务文件未清理。

CheckDocumentation与git diff --check通过，日志为 `documentation.log` / `diff-check.log`。第一次文档检查仅发现PLAN中的会话措辞两项，删除后复查通过，原日志保留为 `documentation-first-failed.log`；没有调整检查脚本或掩盖产品失败。当前master只提交审查文档与记忆，不推送。
