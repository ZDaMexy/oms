# 外部谱库扫描与日志定位审查（2026-10-10～11）

所属工作线：[P1-H 当前状态](../subline/P1-H/DEVELOPMENT_STATUS.md)；现行边界取 [P1-H 约束](../subline/P1-H/TECHNICAL_CONSTRAINTS.md)。本记录保存本轮证据，不代替实时状态。

## 范围与现场证据

从维护设置的 external/managed 重建、增量及首次启动入口，沿 scanner 的目录发现、分类、导入、Realm 复用与缺失收敛审查；同时追踪常规设置导出日志的完成通知。

修改前基线为 `15926a8448053f1cc7a4f209aae5c92b76e76a26`，工作区干净，已成功 fetch。只修改客户端，未改网站、接口、默认在线策略或发行流程。

用户本次截图显示索引 3539、跳过 0、错误 14。本机对应日志确认程序使用便携数据根，导出后 `ProgressCompletionNotification` 收到多次点击；导出压缩包实际存在。扫描错误为 13 个“目录无有效可玩谱面”和 1 个读取谱文件时的 Windows 系统资源不足异常。数据库日志还有 32738 条解析警告、3334 条无效谱文件跳过记录；首末相关消息相隔约 42 分钟，但旧日志没有完整扫描计时，不能把这段间隔作为精确扫描总耗时。

本轮只读了已有日志及该读取失败文件的大小/属性，没有重扫、修复、迁移或删除用户谱库。13 个目录的作者用途未逐个核定，不将其统一判成可删除的模板，也不把资源不足异常归因为已经确认的内存耗尽。

## 已实施的优化与修复

### BMS 素材查找

`DirectoryArchiveReader.Filenames` 每次访问都会递归枚举真实目录。原 importer 在每张谱的 STAGEFILE、BACKBMP、BANNER、各 BMP 条目和预览音频解析中反复访问它，再线性检查全部文件。BGA 引用多、素材多时，枚举及字符串分配随“谱面 × 引用 × 素材数”放大。

现在一次目录导入只捕获一份文件名快照，用忽略大小写的字典定位候选。字典记录原枚举顺序；多个扩展名都存在时仍选原来最先遇到的文件，不改成优先某个扩展名。快照只活在当前导入中，下次导入重新读取；不增加跨扫描缓存或持久化新字段。收益代价是一份与目录文件数成正比的临时字典，测量中的总分配明显减少。

谱面排序、原始字节 MD5、集合 hash、重复谱跳过、解析警告、可玩性判定、完整转换与原始素材文件均保持现有处理。

### 增量判断

原 BMS/mania 的 active 检查先取所有同权限可用 set 到托管列表，再在其中查一个路径；每个候选目录重复执行。现在先由 Realm 按规范路径、权限及可用性过滤，只把匹配路径的候选交给托管层；BMS 继续比较注册根快照。

同时修正同路径只有另一玩法记录时错误跳过的问题：可用的 mania 索引不能代替 BMS，反向也相同。大小写、尾分隔符由既有准入/注册规范化处理；历史不可用或待删记录不满足 active 判断。没有数据库 schema 升级、全库内存快照或并行 Realm 写入。

### 取消与日志

目录发现阶段每处理一个目录检查取消令牌，取消继续抛出，不记成普通遍历错误。每根新增开始与完成记录，包含扫描模式、目录/导入/跳过/错误数量、发现与处理耗时；处理计时截止逐目录循环结束，不包含后续缺失索引收敛。

### 导出通知定位

便携模式构造 `NativeStorage` 时漏传 `GameHost`。其子 storage 也缺少宿主，完成通知调用 `PresentFileExternally` 会直接返回 false，虽然按钮已收到点击。现在在数据根创建处绑定宿主，使日志导出、打开数据目录及其他 storage 子目录的外部打开都经过正常桌面入口。依据取当前依赖的 [osu-framework 2026.303.0 NativeStorage 实现](https://raw.githubusercontent.com/ppy/osu-framework/2026.303.0/osu.Framework/Platform/NativeStorage.cs)。

隔离探针调用真实 Desktop 的 `CreateStorage`，经 `OsuStorage.GetExportStorage()` 和相同的 `PresentFileExternally("compressed-logs.zip")` 路径，以捕获宿主接收的路径代替打开 Explorer。修改前导出与根目录外部打开均为 false，修改后须同时返回 true，且接收路径分别等于真实导出文件与数据根。这个探针验证调用链，不替代用户在 Windows 中看到选中文件的人工验收。

## 受控性能比较

Release，使用相同探针与隔离临时库，各场景运行四轮，首轮保留但不纳入后三轮中位数。分配由 `GC.GetTotalAllocatedBytes(true)` 差值获取，是进程累计临时分配，不是峰值常驻内存。未同时运行其他构建、测试或 formatter。

- BGA 重建：12 个歌曲目录，每目录 4 张不同谱、512 个素材、每谱 96 条 BMP 引用；每轮均导入 12、跳过 0、错误 0。
- 增量判断：上述 12 条实际索引加 4000 条不同路径记录，1000 次已存在路径判断及 1000 次新路径判断；前后均已存在路径导入 0 次、新路径导入 1000 次。

| 场景 | 修改前中位耗时 | 修改后中位耗时 | 耗时比 | 修改前临时分配 | 修改后临时分配 |
| --- | ---: | ---: | ---: | ---: | ---: |
| BGA 重建 | 1946.59 ms | 205.29 ms | 9.48× | 1,280,905,752 B | 19,495,512 B |
| 增量路径判断 | 17051.82 ms | 4597.29 ms | 3.71× | 4,969,734,952 B | 115,486,472 B |

分配分别降低 98.48% 和 97.68%。该 fixture 的谱面内容、素材布局、磁盘和缓存状态均不能代表真实大库，不能据此承诺 3539 个目录总耗时按同样倍数缩短。

原始 JSON、输出、TRX、前后宿主探针和精确代码保存在忽略目录 `artifacts/library-scan-review-20261010/`。开发探针位于 `.dev-cache/temp/`，依赖与所有输出留在开发盘；没有生成 publish、发行 ZIP 或额外客户端安装副本。

## 审查取舍

| 环节/方案 | 本轮结论 |
| --- | --- |
| 重复素材枚举、全库对象拉取 | 有直接源码与前后测量证据，已优化。 |
| 目录遍历、BMS 扩展名与 mania Mode 分类 | 仍完整遍历并遵守链接/读取失败边界；mania 的模式分类需读取文件，未跳过该判断。 |
| BMS 解码及可玩转换 | 负责可玩性、时长、物量和筛选 metadata；没有充分等价证据，不改成只读头部。 |
| 仅以修改时间或大小跳过重建 | 会漏掉相同时间/大小的内容变更及素材更新，不引入。 |
| 并行导入/合并全库事务 | 会扩大磁盘竞争、Realm 通知、取消和历史保全问题；现有串行逐目录提交保持。 |
| 日志与进度更新 | 现场解析警告有诊断价值，通知文字和进度经 scheduler 更新；UI 成本未独立测量，本轮不静音警告或更改所有进度消费者。 |
| 目录快照跨扫描保留、新增数据库索引/schema | 当前窄优化已有实测收益；不在缺少迁移与一致性收益证据时扩张。 |
| 无有效谱面与系统读取错误 | 继续明确失败，完整无错误且未取消的根才能收敛缺失索引；不掩错换取速度。 |

## 验证与剩余人工门

自动范围覆盖 BMS/mania 的导入与下载、scanner、大小写路径、根归属、混合玩法、不可用/待删记录、素材扩展匹配与下次导入更新，及既有缺失/恢复、成绩/收藏身份保全。新增取消重建回归确认取消不收敛缺失目录。旧 mania Register-only-non-mania 检查改为符合现行明确失败合同，普通 Import 空结果通知独立保留；结果总览取 [P1-H 验证](../subline/P1-H/DEVELOPMENT_STATUS.md#最近一次验证)。

每个新 shell 均先执行 `. .\UseDevelopmentStorage.ps1`。缺测试工程 assets 的首次 `--no-restore` 没有发现/执行测试，退出 0 不作为门；正常 restore 后以下均要求实际执行摘要与 TRX：

```powershell
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~BmsImportIntegrationTest|FullyQualifiedName~BmsDownloadImporterTest' --logger 'trx;LogFileName=bms-importers-delivery.trx' --results-directory artifacts/library-scan-review-20261010
dotnet test osu.Game.Rulesets.Mania.Tests/osu.Game.Rulesets.Mania.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ManiaFilesystemLibraryTest|FullyQualifiedName~ManiaImportIntegrationTest|FullyQualifiedName~ManiaDownloadImporterTest' --logger 'trx;LogFileName=mania-importers-delivery.trx' --results-directory artifacts/library-scan-review-20261010
dotnet test osu.Game.Tests/osu.Game.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~ExternalLibraryScannerTest|FullyQualifiedName~ManagedLibraryScannerTest' --logger 'trx;LogFileName=library-scanners-delivery.trx' --results-directory artifacts/library-scan-review-20261010
dotnet build osu.Desktop/osu.Desktop.csproj -c Release --no-restore
dotnet build osu.Desktop/osu.Desktop.csproj -c Debug --no-restore
```

定点 whitespace/imports 及本轮新增的 IDE0008 检查独立验收，不称完整 style 全绿：全文件 style 检查还报告修改前已存在的私有 `ScanRoot`、`initLock`、旧测试常量命名、旧 `var`/自动属性/using 等问题。本轮没有屏蔽诊断或批量改无关实现。BMS 测试工程编译仍显示未触及的 `TestSceneFilesystemBackedStoryboardFallback` CS8600 与 `BmsRulesetStatisticsTest` CA2007，产品桌面构建单独记录。

尚未对用户实际库复跑扫描，未验收真实扫描掉帧/输入延迟，也未确认 Windows Explorer 的实际窗口与文件选中。下一次从当前源码非调试启动后可先复验导出日志点击，再用新增每根计时比较增量和重建；不把本轮自动验证提升为这些人工门已经通过。
