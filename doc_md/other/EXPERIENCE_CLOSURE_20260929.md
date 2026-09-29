# 谱库、声音与选歌体验闭环：2026-09-29 验证记录

## 范围与基线

用户授权先审查现状、详细规划并推进完成。本轮由曲库保全开始，依次处理重扫与当前页难度表刷新、长伴奏暂停续播和手动转谱长条、既定单轨上限筛选。开始时 `master@f94c1b6` 工作区干净；`git fetch origin` 成功，领先跟踪分支 8、落后 0。本轮不推送，不操作用户真实谱库，不重开已暂停的皮肤打磨。

当前能力与剩余门归 [P1-H](../subline/P1-H/DEVELOPMENT_STATUS.md)、[P1-I](../subline/P1-I/DEVELOPMENT_STATUS.md)、[P1-J](../subline/P1-J/DEVELOPMENT_STATUS.md)；此页保存跨线自动验证，不能代替实机签收。

## 执行环境

- 每个新的编译、测试、格式与检查 shell 均先 `. .\UseDevelopmentStorage.ps1`；缓存和临时数据位于 `F:\oms\.dev-cache`，证据位于 `artifacts/experience-closure-20260929/`。
- 首次使用当前缓存分别执行 `dotnet restore osu.Desktop.slnf --verbosity minimal` 与 `dotnet restore osu.Game.Tests/osu.Game.Tests.csproj --verbosity minimal`，均成功。后续 `--no-restore` 基于这批 assets。
- 所有 build/test/formatter 串行；编译期间暂停源文件写入。测试默认为 Debug，Release 构建另列；没有用旧二进制跳过本轮源码编译。
- 格式处理使用仓库相对路径，包含已修改与新建 C# 文件。

## 有效证据与调试过程

| 验证 | 结果与工件 |
| --- | --- |
| BMS focused r3 | 59 通过；`bms-focused-r3.trx`。覆盖 importer/历史保全、难度表批次、模式往返与 native 音频位置。其后曲库异常输入补修须看最终结果 |
| core focused | 19 通过；`core-final.trx`。scanner、公共搜索、真实 Realm 快照、可见表等级更新及选中保留、批次不额外替换集合、转谱难度表分组 |
| mania relevant | 421 通过、4 既有失败；`mania-final.trx`，逐项对照见下。其后 importer 补修另跑 focused |
| BMS importer 最终补修 | 38 通过；`library-final.trx`。全坏谱错误报告与失败保全、同目录双模式不互隐均通过 |
| mania importer 最终补修 | 4 通过；`mania-library-final.trx`。同内容多来源、全坏谱明确失败、真实文件占用时不收敛及释放后恢复 |
| BMS 完整回归 | 2394 通过、29 既有失败、16 跳过；`bms-full-final.trx`。所有失败 Message/StackTrace 与上一轮完整基线逐字一致 |
| 最终筛选控件 | 10 通过；`filter-final.trx`。文案修正后重新编译，包含默认、拖拽、数值和模式往返 |
| 最终 core 全部相关 | 34 通过、2 既有排序失败；`core-complete-final.trx`。本轮新增刷新/保全和公共搜索均通过 |

执行命令（每条均在已加载存储入口的 shell 中，日志同名 `.log`）：

```powershell
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsImportIntegrationTest|FullyQualifiedName~BmsDifficultyTableManagerTest|FullyQualifiedName~TestRulesetRoundTripKeepsFiltersSeparate|FullyQualifiedName~BmsKeysoundPositionResume" --logger "trx;LogFileName=bms-focused-r3.trx" --results-directory artifacts/experience-closure-20260929
dotnet test osu.Game.Tests/osu.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~ExternalLibraryScannerTest|FullyQualifiedName~TestSceneBeatmapFilterControl|FullyQualifiedName~TestSceneRealmDetachedBeatmapMetadata|FullyQualifiedName~TestRulesetMetadataRefreshesVisibleTableLabelAndKeepsSelection|FullyQualifiedName~TestRulesetMetadataBatch|FullyQualifiedName~BmsConvertedDifficultyTableGroupingTest" --logger "trx;LogFileName=core-final.trx" --results-directory artifacts/experience-closure-20260929
dotnet test osu.Game.Rulesets.Mania.Tests/osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~ManiaFilesystemLibraryTest|FullyQualifiedName~Hold|FullyQualifiedName~Replay|FullyQualifiedName~Keysound|FullyQualifiedName~BeatmapConverter" --logger "trx;LogFileName=mania-final.trx" --results-directory artifacts/experience-closure-20260929
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsImportIntegrationTest" --logger "trx;LogFileName=library-final.trx" --results-directory artifacts/experience-closure-20260929
```

Native 音频测试生成真实长 WAV，检查 BASS 位置在暂停期间完全不变、继续沿用原通道、恢复 1x/1.5x 速率，以及暂停中 seek 清除旧声部。它证明后端行为，不代签设备听感。首次 fixture 的 `SampleInfo` 命名冲突和提前 TearDown 已修复；模式往返最初只看子控件 `IsPresent`，后改为验证祖先可见性；这些中间失败不作为最终结果。

Realm fixture 写入 JSON 时观察到原生集合通知，因此不能声称所有元数据写入都不产生集合变化。最终用例先等待原生写通知，再证明新增批次通知读取最新已提交值、只发一次更新且自身不增加集合替换。可见谱卡测试使用真实 BMS ruleset，证明等级从 ★1 改为 ★2，同时保留选中 ID。

### 既有失败核对

mania 的 `TestHoldNoteChord`、`TestHoldNoteStair`、`TestHoldNoteWithReleasePress`、`TestSingleHoldNote` 与 `TestResults/auto-keysound-20260929/auto-keysound-mania-relevant.trx` 中同名失败的 Message 和 StackTrace 逐字一致；对照存于 `mania-failure-comparison.json`。不把整组称为全绿。

BMS 完整结果与 `TestResults/auto-keysound-20260929/auto-keysound-bms-full.trx` 逐项核对：29 项失败名称均唯一匹配，Message 与 StackTrace 均逐字一致，名单保存于 `bms-failure-comparison.json`。既有皮肤设置测试未在本轮改修；16 个需要外部候选/备份根或人工捕获条件的跳过项没有被当作通过。完整回归后仅修正筛选浮层提示文字（限定“已有构成统计”），随后重编译并复验筛选和 core，未重跑无行为变化的完整套件。

core 扩展回归中，两项 `TestSortingStability` 失败。定点将 `BeatmapCarousel.cs` 和 `TestSceneBeatmapCarouselUpdateHandling.cs` 临时替换为 HEAD 版本，运行同一筛选后仍为 1 通过、2 失败；错误仍在“Order didn't change”断言。`finally` 恢复本轮文件后继续验证。工件 `core-head-carousel-baseline.trx`/`.log`。这是改动前 carousel 与 fixture 的局部对照，其他工作区源码保持本轮状态，不宣称全仓 HEAD 对照。

## 最终验证

最终完整回归与追加定点命令：

```powershell
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore --logger "trx;LogFileName=bms-full-final.trx" --results-directory artifacts/experience-closure-20260929
dotnet test osu.Game.Rulesets.Mania.Tests/osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~ManiaFilesystemLibraryTest" --logger "trx;LogFileName=mania-library-final.trx" --results-directory artifacts/experience-closure-20260929
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneBmsFilterControl" --logger "trx;LogFileName=filter-final.trx" --results-directory artifacts/experience-closure-20260929
dotnet test osu.Game.Tests/osu.Game.Tests.csproj --no-restore --filter "FullyQualifiedName~ExternalLibraryScannerTest|FullyQualifiedName~TestSceneBeatmapFilterControl|FullyQualifiedName~TestSceneRealmDetachedBeatmapMetadata|FullyQualifiedName~TestSceneBeatmapCarouselUpdateHandling|FullyQualifiedName~BmsConvertedDifficultyTableGroupingTest" --logger "trx;LogFileName=core-complete-final.trx" --results-directory artifacts/experience-closure-20260929
dotnet build osu.Desktop.slnf --no-restore -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m
```

Release 构建成功，0 错误，2 个既有警告：`TestSceneFilesystemBackedStoryboardFallback.cs:151` 的 CS8600、`BmsRulesetStatisticsTest.cs:555` 的 CA2007。日志 `release-final.log`。这不是 publish、安装包或设备签收。

`dotnet format whitespace` 对本轮相对路径变更集（含未跟踪新文件）分别通过 `osu.Desktop.slnf` 与 `osu.Game.Tests/osu.Game.Tests.csproj` 的 `--no-restore --include ... --verify-no-changes`，日志 `format-verify.log`；最终校验未改源码。`powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\CheckDocumentation.ps1` 与 `git diff --check` 通过，文档日志为 `documentation-final.log`；绝对路径/指纹提示为审阅警告，不是失败。暂存后再次核对 diff。

## 人工验收仍保留

1. 备份或隔离数据根中核对缺失、改名、恢复、解除注册、整根离线，确认磁盘文件、旧成绩和收藏；本轮自动测试使用临时 fixture，没有扫描用户真实库。
2. 当前选歌页切换难度表时核对真实大库的响应、分组、标记和选中歌曲；没有五万级库的实测性能结论。
3. 单轨筛选的窄窗口、拖拽手感、零宽编辑、数值反馈及无解提示理解度；自动鼠标/数值交互证明不代替玩家认可。
4. 原生 BMS 与 BMS→mania 分别试听手动/自动长条、同槽重触发、密集键音、长伴奏反复暂停及变速。seek/retry 清除旧声，不承诺补回过去已开始的长样本。

这些项目由 [P1-G](../subline/P1-G/DEVELOPMENT_PLAN.md) 汇总；未新增 Skin V1、真实硬件或公开发行签收。
