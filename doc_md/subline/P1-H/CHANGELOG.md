# P1-H 变动日志

## 2026-10-11

### 转谱补算探针清理收尾

- 用户用 PowerShell 7.6.6 执行限定路径、链接 / 数据库 / 活动进程及源码归档校验的清理命令，报告删除成功。随后只读确认 `.dev-cache/temp/converted-stars-perf-probe` 不存在；原 `evidence-manifest.json` 中 73 份源码、测量、日志与 TRX 的 SHA256 全部一致。
- 删除前为 366 文件、653,780,576 B（约 0.61 GiB）逻辑大小，未测量实际磁盘回收量。新增 `artifacts/converted-stars-performance-20261011/probe-cleanup-user-verified.json` 记录人工清理及复核；原清单、自动删除拦截回执与软件交付快照均保留，历史拒绝不再作为当前清理待办。
- STATUS 更新清理状态，PLAN 移除已完成项；文档检查输出另存 `cleanup-documentation-check.log` 与 `cleanup-workspace-check.log`。仅做清理及文档收尾，不重复软件测试，产品源码与原验证继续取 `55741b5`；真实大库、Explorer 与现场读取错误的人工门仍待，未重扫或修改用户库。

### 转谱星数后台资源优化

- 用户报告 VS Code 非调试启动后补算卡顿且计算慢。本轮基线为干净的 `824c208d1a620a7fac5eaf7855b1c015c5ba324d`，在线 fetch 确认与 origin/master 一致。只读用户指定的导出日志，按会话文件名而非相同的导出 mtime 选取最新完整补算会话；未扫描、修改用户谱库或运行 live 数据库。
- 日志 UTC 换算为本地 UTC+8：19:51:07 找到 56,366 张待补算，20:23:07 进入下一阶段，边界耗时 1,920 秒；截图成功 56,307、失败 59，与日志失效缓存次数及不可转换失败数一致。59 项均无可用于 mania 星数计算的判定音符，没有 calculator timeout、transient failure 或 gameplay pause。结束统计 Audio Components 56,531、Realm async reads 57,269，而 working cache 仅 4；日志未记录完整帧时间或 CPU profile。
- 根因一是 filesystem provider 构造时为每张谱面创建两种 texture store 和 track store。固定依赖 ppy.osu.Framework 2026.303.0 的 [AudioManager](https://github.com/ppy/osu-framework/blob/ba04d538f02cecaabe5ab7dc3b102cd47d59d785/osu.Framework/Audio/AudioManager.cs) 将每个 track store 注册进全局集合，[AudioCollectionManager](https://github.com/ppy/osu-framework/blob/ba04d538f02cecaabe5ab7dc3b102cd47d59d785/osu.Framework/Audio/AudioCollectionManager.cs) 持有并继续更新子项；清 working cache 不会解除该注册。三类 store 改为线程安全的 `Lazy<T>`，读谱和算星只使用文件，实际背景/播放首次使用时才创建一次。首次真实消费承担原先提前支付的初始化成本，后续播放接口保持。
- 根因二是非调试启动仍使用 Debug 构建时，`Debug.Assert` 参数中的 blob 路径复核对每张 filesystem 谱面额外读取 Realm；此类型没有 Realm blob。该诊断仅保留于 hash-backed 谱面，filesystem 原始字节 MD5 验证保持，修改过的源文件仍拒绝加载。
- 不改变 mania 星数算法、转换/难度版本、MD5 身份、共享 RulesetData、失败持久化、分块/并行度、批量写回与游玩时暂停；已有效的持久化星数继续复用。未把 59 个不可转换条目当成超时重试，也未通过清库或版本 bump 强制用户重算。

隔离测量使用真实 `WorkingBeatmapCache`、BMS loader 和 mania calculator，三轮各自新进程，生成同一个小型外部 BMS fixture，不打开用户 data root。原始 `*-before-*.json` 来自改动前源码；最终 `*-final-*.json` 包含本轮全部源码修改，Game assembly SHA256 为 `6BE1594032E6138A7848433D87C70E84D39FDA2DD9C9414B551DA044745A27CE`。源码/工程存于 `artifacts/converted-stars-performance-20261011/probe-source/`，原源码快照、汇总脚本、运行时指纹、原始 JSON 和 `probe-summary.json` 同目录保留。

| 每轮 5,000 张计算环境；三轮中位数 | 改动前 | 最终源码 |
| --- | --- | --- |
| 创建耗时 | 97.922 ms | 49.164 ms（减少 49.8%） |
| 累计分配 | 80,099,600 B | 21,806,672 B（减少 72.8%） |
| 额外注册 track store | 5,000 | 0 |
| 空全局 track store 更新 sweep | 0.2477 ms | 0.0001 ms |

每轮 128 张同一小谱面的真实转谱/算星，前后六轮共 768 个星数逐值完全一致；累计分配中位数 16,443,656→14,951,992 B（减少 9.1%）。耗时中位数 90.346→83.062 ms，但前后单轮范围重叠，先期三轮资源改动测量为 91.985 ms；小谱面时延有噪声，不据此承诺全库算星提速。上述空集合 sweep 不启动 framework audio thread，仅调用真实 collection.Update，不能代替设备音频或帧时间；Release 测量也不包含 Debug 诊断读取的收益。真实大库改善仍按 [PLAN](DEVELOPMENT_PLAN.md#1-隔离数据根及真实大库验收) 自然补算时复验。

软件验证与失败身份：

- core Debug 资源、原谱 MD5 修改拒绝、真实 mania 算星、persisted star fallback 与 ruleset version 回归 22/22；scanner 9/9；mania Release 导入/下载/样本转换 57/57。新增资源 fixture 验证纯读谱不访问图片/音轨、两类图片与音轨并发首次消费只创建一次；原 hash-backed Debug 诊断保持。
- BMS Release full：2,561 通过、30 失败、16 跳过，耗时 10 分 17 秒。29 项与 `artifacts/gameplay-performance-20260930/bms-full-final.trx` 的身份及 Message 一致，仅规范化工作区迁移和 DisplayClass 数字后完整栈一致，见 `bms-failure-comparison.json`。
- 历史报告外的 `BmsManagedFolderSelectionProductTest.TestManagedDeleteFallbackReentrantSelectionNeverSplitsPairAndLatestWins` 在当前源码单项仍失败；用 `baseline-source.targets` 将唯一改动的生产文件替换为已核对干净 HEAD 的原快照重新编译，失败消息与完整栈逐字一致，见 `bms-reentrant-baseline-comparison.json`。记录真实 compile item，未改 checkout 源码、断言或增加等待重试，不把历史报告之外的失败直接归为本次新增。该旧皮肤删除检查仍待 owning P1-A 维护，本轮不改无关皮肤行为。
- 对照后去掉临时 MSBuild 参数，以普通源码重新非增量构建 `osu.Desktop.slnf` Release 成功，0 错误、2 个原有 BMS 测试工程警告（CS8600/CA2007）；普通 Desktop Debug 0 警告、0 错误。定点 whitespace 与 IDE0005 imports `--verify-no-changes` 通过，未运行完整 style 或 mania full，不称仓库全绿。
- 首次探针探索的编译错误与启动失败不是软件 gate，旧日志不覆盖；正式前后探针均正常退出。源码快照只忽略 checkout CRLF/LF 差异后与基线 Git blob 比较，`source-binding.json` 保留原字节的不同 SHA，不把换行差异当内容改变或伪称逐字节一致。
- 临时 `.dev-cache/temp/converted-stars-perf-probe` 的源码已 SHA256 对照归档；确认为无链接、数据库和活动探针，366 文件、653,780,576 B。唯一限定路径 PowerShell 删除被自动审批以 `blocked by policy` 拒绝，当时只读确认目录仍在；没有回收空间或换用删除工具。`probe-cleanup-before/after.json` 与 `probe-cleanup-blocked.txt` 保留；随后用户清理与复核结果见 [清理收尾](#转谱补算探针清理收尾)，长期日志、源码、测量与 TRX 不属于垃圾。
- 客户端文档健康、总工作区协作文档验收与 `git diff --check` 通过；新增 checksum 为产品程序集指纹，检查器的公开指纹提示保留。最初 PLAN 会话用语检查失败已修正，原输出保留；统一验收仍有 11 项历史客户端事实待复核，不因单客户端资源优化刷新桥事实或其它产品适用日期。

主要命令（各新 shell 先 `. .\UseDevelopmentStorage.ps1`；原始输出与 TRX 位于上述证据目录）：

```powershell
dotnet test osu.Game.Tests/osu.Game.Tests.csproj -c Debug --filter 'FullyQualifiedName~WorkingBeatmapResourceTest|FullyQualifiedName~BmsStarRatingResolverTest|FullyQualifiedName~BackgroundDataStoreProcessorRulesetVersionTest' --logger 'trx;LogFileName=core-resource-focused.trx' --results-directory artifacts/converted-stars-performance-20261011
dotnet build osu.Desktop.slnf -c Release --no-restore
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=bms-full.trx' --results-directory artifacts/converted-stars-performance-20261011
$baselineTargets = (Resolve-Path -LiteralPath artifacts/converted-stars-performance-20261011/baseline-source.targets).Path
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj -c Release --no-restore "-p:DirectoryBuildTargetsPath=$baselineTargets" --filter 'FullyQualifiedName~TestManagedDeleteFallbackReentrantSelectionNeverSplitsPairAndLatestWins' --logger 'trx;LogFileName=bms-reentrant-baseline.trx' --results-directory artifacts/converted-stars-performance-20261011
dotnet build osu.Desktop.slnf -c Release --no-restore --no-incremental
dotnet test osu.Game.Rulesets.Mania.Tests/osu.Game.Rulesets.Mania.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~ManiaImportIntegrationTest|FullyQualifiedName~ManiaDownloadImporterTest|FullyQualifiedName~ManiaBeatmapSampleConversionTest' --logger 'trx;LogFileName=mania-focused.trx' --results-directory artifacts/converted-stars-performance-20261011
dotnet test osu.Game.Tests/osu.Game.Tests.csproj -c Debug --no-build --no-restore --filter 'FullyQualifiedName~ExternalLibraryScannerTest' --logger 'trx;LogFileName=core-scanner.trx' --results-directory artifacts/converted-stars-performance-20261011
dotnet build osu.Desktop/osu.Desktop.csproj -c Debug --no-restore
dotnet build .dev-cache/temp/converted-stars-perf-probe/ConvertedStarsProbe.csproj -c Release --no-restore
# 新进程逐轮执行探针 resources/stars 场景；复跑前将归档的两个 probe-source 文件还原到上述临时目录。
python artifacts/converted-stars-performance-20261011/summarize-probe.py
```

### 临时探针清理收尾

自动删除曾被审批以 blocked by policy 拒绝；用户随后自行执行限定路径、链接 / 数据库预检的 PowerShell 命令，删除 `.dev-cache/temp/library-scan-probe` 与 `.dev-cache/temp/log-reveal-probe`。本轮只读复核两目录均不存在，审查目录中的两份源码 / 工程、前后 benchmark、宿主探针 JSON 和 TRX 保留，原 `probe-cleanup-blocked.txt` 不覆盖。删除前两目录逻辑大小合计约 1.22 GiB，未复核实际磁盘回收量。

此清理不关闭旧工作区 / 系统盘的其他待办，也不新增真实库扫描、Explorer 或发行验收。报告位置改为历史测量说明，P1-A 的对应 mania 旧检查退出待维护，产品软件仍沿 `d6f0d60`；本次治理方法与检查只取 [主线日志](../../mainline/CHANGELOG.md#专项进度与文档记忆健康同步)。

### 扫描性能与便携日志定位

- 2026-10-10 开始只读用户本次扫描及导出日志，核出逐素材递归枚举、逐目录全库对象拉取与便携 storage 未绑定宿主三个根因；读取错误与无有效可玩谱目录继续明确报错，未操作用户谱库。
- BMS 改用一次导入的文件快照及保留原顺序的素材查找；BMS/mania 增量先做 Realm 路径/权限/可用性筛选，补齐混合玩法不互相跳过，BMS 根快照继续参与匹配。增加目录发现取消检查及每根分段耗时日志。
- 便携 NativeStorage 绑定 GameHost；隔离的真实 Desktop storage 调用链前后对照，验证导出文件与数据根的外部打开。旧 mania Register-only-non-mania 断言改为明确失败与源文件保全，普通 Import 空结果合同保留。
- 受控前后测量、保留方案、软件结果与剩余人工门见 [审查记录](../../other/LIBRARY_SCAN_REVIEW_20261010.md)；当前验收取 [STATUS](DEVELOPMENT_STATUS.md#最近一次验证)，不提升旧 full 或真实大库签收。

## 2026-10-03

全项目文档审查确认本线 STATUS/CONSTRAINTS 和记忆已准确描述失效保全与当前页刷新，主约束仍留旧边界；就地修正 OMS_COPILOT 的多根谱库和难度表持久化/消费段落，保留整根离线/失败不收敛、原生 Realm 通知及真实大库人工门。PLAN 补齐 STATUS 已记录的具名旧 Register 检查维护，不在此次治理中改产品或测试，也不把旧失败归因当已修复。未扫描/修改用户谱库，原验证日期保持；统一检查见[主线日志](../../mainline/CHANGELOG.md#全项目进度与文档记忆健康复核)。

## 2026-09-30

### 完整性能回归发现旧 Register 测试预期

mania full 检出 `ManiaImportIntegrationTest.TestRegisterExternalDirectoryWithOnlyNonManiaBeatmapsReturnsNull` 的四月 null 断言与九月已确认的注册失败合同不符。纯 `034d79b` 生产源码下 Release 重新编译单项复现相同 InvalidDataException 和业务堆栈，本轮未改 importer/test；记录为已有测试欠账，不回退正确行为。对照方法、最终 full 与源码恢复证明见 [性能验证记录](../../other/GAMEPLAY_PERFORMANCE_20260930.md)。未扫描真实库、重验隔离根或改存储实现。

### 文档与记忆健康复核

- 对照当前 importer、scanner、索引协调器及难度表批次刷新代码，区分扫描器收到的错误与 importer 仍可返回有效结果的跳过/警告，避免将失败保全写成任一坏文件都会阻止整个根收敛。
- 收窄改名身份承诺：只有仍命中同集合 hash 的记录复用才更新原 `LocalFilePath`；多文件改名改变排序可能改变集合 hash，不能从单谱 fixture 推导所有改名保留 set ID。数据根迁移表述改为未补该项验收，不否认既有数据根管理。
- 难度表召回将旧全 Unrated 故障标为历史，补初始化订阅窗口和处理时读取最新持久化值的诊断；当前页刷新不再表述为需要重启，且新增批次通知不代表阻止所有原生 Realm 通知或大库零卡顿。
- 仅改文档与 memory，未改源码、运行产品测试或操作用户谱库；最近产品验证继续使用 2026-09-29 证据，隔离根实机和真实大库门保持未验收。

## 2026-09-29

### 谱库索引失效与历史保全、当前页难度表批量刷新

- 后续审查收口三处边界：两 importer 的 Register 全无有效谱面改为明确失败，普通 Import 保持既有空结果通知；mania 分类读取不再把 IO/权限错误吞成非 mania；同目录替换限制本 ruleset。新增 external/managed 坏谱扫描保全、同目录双模式互不隐去、mania 文件占用和坏目录注册回归，已包含于最终通过的库 focused。
- 引入 schema 58 `FilesystemUnavailable`，区分来源不可用和用户删除。成功重建隐去缺失目录/空目录；解除外部根隐去不再被同类型其他根覆盖的歌曲。扫描不写待删、不删记录或源文件，保全成绩和收藏；原路径原内容恢复复用原身份。
- BMS/mania 同 hash 查找约束可复用路径和权限，不再跨目录将旧记录标 `DeletePending`。同内容多目录共存，同目录内容改变仅隐去旧版本；内容未改但谱文件改名时更新 `LocalFilePath`。
- 临时库 fixture 覆盖同内容跨路径、重启文件保全、单谱删除、文件改名、缺失恢复和历史关联。扫描与外部根解除串行；遗留父子根先移父/先移子均保留剩余覆盖，最后移除才隐去。
- 新根规范化盘根/尾分隔符/大小写，拒绝重复、父子重叠和祖先链接；扫描旧根也检查链接。子目录链接及属性读取失败计错跳过；离线、失败、取消不做缺失收敛，已完成的目录导入不回滚。
- 难度表全部写回事务完成后发布一批 ID，列表读取最新持久化 JSON 更新快照并一次重筛；可见谱卡同步重绑、仍存在的选中谱面保留。禁止逐 set revision bump，保持 persisted-only 合同。真实大库耗时与掉帧另验。
- 同目录冲突查找使用 Realm 路径/权限筛选后再区分 ruleset，避免重建时逐目录拉取全部对象。最终 `library-final` 38 通过、`mania-library-final` 4 通过，包含该修改及全部新增异常边界。
- `core-complete-final` 34 通过、2 个既有排序失败，HEAD carousel 加同 fixture 对照复现相同失败；BMS full 2394 通过、29 个既有失败、16 跳过，所有失败 Message 和 Stack 与基线逐字一致。Release 成功（0 错误、2 个既有警告），格式 verify 通过，本轮软件验证完成；完整回归仍有具名旧失败，不称全绿。精确命令及失败身份统一见 [体验收口验证](../../other/EXPERIENCE_CLOSURE_20260929.md)。隔离根实机与真实大库人工门未执行，未操作用户谱库。
- 新增 [filesystem library identity 召回](../../../.Codex/memory/reference_filesystem_library_identity.md)，记录待删物删副作用、内容与路径身份区别、谱文件改名及失败扫描回收边界。

### 难度表诊断与文档职责对齐

- 再核外部根移除/扫描入口，缺失记录失效、跨根身份及重扫恢复仍未闭合；未扫描、迁移或删除用户库。
- 难度表 memory 补明未来内存索引方案须先变更 persisted-only 消费合同，不能据召回直接加入临时 lookup；文档同步条款对齐仓库唯一职责。未改产品代码或刷新原验证日期。

## 2026-09-12

### 对齐持久化同步与当前选歌页面的边界

- 再核 `ExternalLibraryConfig/Scanner`、移除 root 入口、共享 `RulesetData` DTO 及难度表召回；现存基本行为与剩余存储一致性门未变。
- STATUS/PLAN 补明难度表持久化写回完成不等于当前页面即时刷新；CONSTRAINTS 与 2026-05-31 实机记录和 memory 对齐，明确退出重进选歌或重启生效。未来内存索引方案须先同步 persisted-only 消费合同，不作为当前添加临时 lookup 的授权。
- 仅文档复核，未运行产品测试、扫描、迁移或删除用户谱库。

## 2026-09-09

### 对照真实扫描与删除行为

- STATUS补充已存在的managed基本删除、external物理只读排除、启动DeletePending清理和同hash复用；同时明确缺失root只跳过、RemoveRoot只改配置、跨root同hash新注册可使旧记录DeletePending，尚无完整失效/重命名/去重恢复矩阵。
- PLAN从真实基本行为继续闭合path/physical identity和Realm/磁盘/UI组合，未将string path检查当作held no-follow authority；保留难度表/converted-star共享JSON与全局RealmAccess合同。本线未改代码或执行用户库扫描/删除；本轮core scanner 7/7、BMS importer所在full通过，精确范围见主线审查记录，失效/去重矩阵仍未闭合。

## 2026-07-16

### 文档健康治理：完成专题压为基线，PLAN 回到删除/path identity/诊断

- 难度表一致性、refresh、identity 与 reuse recovery 的完成批次压为摘要并继续由历史记录承载；当前计划只保留删除/失效、path dedup、现场只读诊断及向 P1-A/G1 输出的边界。
- 当前存储能力和产品 gate 未变；本次仅改文档，未改代码，未运行产品测试或 Release。

## 2026-06-22：新增存储字段消费方 —— 选歌右键「在资源管理器中定位」（从属 P1-I）

主归属 `P1-I`（选歌右键产品面），此处仅登记一个对 P1-H 存储拓扑字段的**新只读消费方**：选歌右键「打开歌曲/谱面文件位置」经新共享 helper `osu.Game/Beatmaps/FilesystemBeatmapLocation.cs` 读取 `BeatmapSetInfo.FilesystemStoragePath` / `IsExternalFilesystemStorage` / `BeatmapInfo.LocalFilePath` 解析谱面磁盘绝对路径（external＝绝对原样、managed＝`storage.GetFullPath`、难度＝set 目录 + `LocalFilePath`），与 `BmsBgaPlayer.tryGetAbsolutePath` 同一解析范式。**影响**：若将来 P1-H 改 `FilesystemStoragePath` 的相对/绝对约定或 `LocalFilePath` 语义，须同步该 helper（及 BGA 路径解析）。仅只读消费、不改任何存储写入/扫描/拓扑语义。详见 [P1-I CHANGELOG](../P1-I/CHANGELOG.md) 2026-06-22。

## 2026-05-31

### 订正：难度表全 Unrated 的真根因是 RulesetData 字段互相覆盖（推翻下方 staleness 判断 + 撤销 bump）

实机验证推翻了同日早先的 staleness 判断。**真根因**：转谱星数子系统 `BmsPersistedMetadataData`（osu.Game，`{ chart_metadata, converted_star_ratings }`）与难度表子系统 `BmsBeatmapMetadataData`（BMS，`{ difficulty_table_entries, chart_metadata, chart_filter_stats }`）**各自定义容器类、却都序列化进同一个 `BeatmapMetadata.RulesetData` 列**，而 `SetRulesetData<T>` 是整体覆盖写、Newtonsoft 默认丢弃未知字段 → **互相把对方独有字段整段冲掉**：

- 转谱星数重算 → 反序列化成 `BmsPersistedMetadataData` → `difficulty_table_entries` 被丢 → **全 Unrated**（用户实测：重算 11336 后退出再进即全 Unrated）；
- 难度表回写 → 丢 `converted_star_ratings` → 启动判定星数 missing → 重算 11336 → 再冲掉难度表 → 破坏性 ping-pong，"有概率"取决于最后谁写。

**修复**：给两个容器类都加 `[JsonExtensionData]`，反序列化时把对方字段存入扩展字典、序列化时原样写回；`BmsBeatmapMetadataData.IsEmpty` 同步计入 ExtensionData（否则清空难度表会 `SetRulesetData(null)` 连星数一起抹掉）。双向回归：`TestDifficultyTableWriteBackPreservesForeignRulesetDataFields`（BMS）+ `TestConvertedStarRatingWritePreservesDifficultyTableFields`（osu.Game）。新增 CONSTRAINTS #22。

**撤销 per-set bump**：下方 staleness 方案的 `BeatmapSetInfo.DifficultyTableRevision++` 在 5.7 万谱库 + 万级条目下，一次表开关命中数千 set → 数千次 carousel per-set re-detach（8 万 update-read + 2000 pending task）→ UI 卡死 1~2 分钟（用户实测）。已移除 bump；字段保留（schema 55 不回退迁移），暂闲置。**代价**：会话中途开关表需重启才反映到分组；启动恒正确。中途即时刷新留作后续轻量方案（分组改走内存索引 + 一次性 re-filter）。

验证：完整 `osu.Game.Rulesets.Bms.Tests` **869/869**、`BmsStarRatingResolverTest` **13/13**；`osu.Desktop.slnf` Release 0 警告 0 错误。**用户实机确认**：修复后转谱星数重算不再复发（ping-pong 打破的决定性证据）、难度表分组正确（退出选曲重进或完全重启后生效）。残留：旧构建已冲掉的 entries 需一次 `禁用→启用` 任一表补回（一次 mutation 用完整 enabled-table lookup 回写所有匹配谱面），之后持久。已知性能项：大库（5.7 万谱）下该回写仍约 1 分钟（`updatePersistedBeatmaps` 用 `AsEnumerable` 全表客户端过滤 + 写上万谱面，#1 后续优化；后台执行不硬阻塞 UI 但掉帧、音乐正常）；会话中途开关表的即时分组刷新仍缺（需退出选曲重进/重启）。

### 修复难度表分组「会话中途启用/刷新后全落 Unrated」+ 回写架构收口 + 健壮性

> ⚠️ **本小节为初判，已被上方订正推翻**：staleness（carousel 缓存陈旧）只是次要现象、非全 Unrated 主因；其 per-set `DifficultyTableRevision` bump 方案在大库下导致 UI 卡死，已撤销。注入全局 `RealmAccess` / 异步化 / MD5 归一化 / 递归保护 / 去 `GetSources().Single` 等改动**保留有效**。

- **根因**：难度表回写改的是 `BeatmapInfo.Metadata.RulesetData`（`BeatmapSetInfo → Beatmaps → Metadata` 下 2 层深 link 属性），而 carousel 数据源 `RealmDetachedBeatmapStore` 只订阅 `All<BeatmapSetInfo>()` 且 `RegisterForNotifications` 不带 keyPaths（Realm 默认浅层通知），深层 link 属性变更不触发集合 modified → carousel 不重新 detach、保持旧快照；切到难度表分组只对旧 detached items 重过滤 → 全 `Unrated`。完全重启后初始全量 detach 才恢复，故表现为"有概率"。MD5 大小写经查不是根因（`BeatmapInfo.MD5Hash` 与表 entry 两侧均为小写）。
- **主修（精准增量刷新）**：新增 `BeatmapSetInfo.DifficultyTableRevision`（realm schema 54→55，纯新增字段无 migration case）。回写 metadata 的同事务内，对 entries 实际发生变化的 set bump 该标量字段，强制 `All<BeatmapSetInfo>()` 集合 modified → `RealmDetachedBeatmapStore` 增量 re-detach → carousel `Replace` 分支 `FindWithRefresh<BeatmapInfo>().Detach()` 拉最新 metadata 重组，无需重启。
- **回写架构收口（消除第二 RealmAccess）**：`BmsDifficultyTableManager` 不再在回写时 `new` 第二个 `RealmAccess`（消除其构造期 `cleanupPendingDeletions` 越权物删待删谱面集 + 与全局实例时序竞争），改为构造时注入全局 `RealmAccess`；`GetShared/GetSharedAsync` 增加 `RealmAccess` 参数，settings / first-run 反射桥 / importer 全部传入全局实例。
- **响应性**：`SetSourceEnabled` / `RemoveSource` 增加 `...Async` 入口，settings 的启用/禁用/移除改走后台线程 + `operationInProgress`，不再在 UI 线程同步回写 + 全表扫描（correctness 已收口，符合本目录约束 #16 的后置时序）。
- **健壮性**：回写匹配统一 `ToLowerInvariant` 归一化 `BeatmapInfo.MD5Hash`（防御大小写漂移，不放宽 identity 口径，符合 #18）；`loadTableSource` 递归加深度上限（防 HTML→引用→HTML 环路）；`importFromPath` / `refreshTable` 末尾改用手中数据拼装返回，去掉 `GetSources().Single` 全量重查（`RefreshAll` 由此从 O(N) 次全表重载降为常量）。
- 验证：新增大小写回写、`DifficultyTableRevision` 仅命中集递增、HTML 环路保护三组回归；难度表相关 **23/23**、导入集成 **26/26** 通过；`osu.Desktop.slnf` Release 0 警告 0 错误。人工验证 carousel 中途刷新待用户确认。

## 2026-05-09

### 数据目录迁移入口改为按真实语义描述

- Settings → 常规 → 安装位置 现已把入口明确为 `更改数据目录位置`，不再误导成移动程序文件；迁移选择页也已直接说明三类结果：空目录直接迁入、非空非数据目录改用其下 `oms/` 子目录、已是可用数据目录则仅在重启后切换。
- 这次收口明确了 `P1-H` 当前的 runtime authority：该入口只改变运行时数据根，最终通过 `storage.ini` / 数据迁移链切换，不会移动 `osu!.exe` 所在目录。
- 验证：`dotnet build .\osu.Game\osu.Game.csproj -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

## 2026-05-08

### BMS imported raw wrapper 复用 timing 数据，修复 Song Select 左上 BPM 恒 60

- `BmsImportedBeatmapFactory` 现会把首次 ruleset conversion 得到的 `ControlPointInfo`、`HitObjects` 与 `Breaks` 复用回 `BmsDecodedBeatmap` raw wrapper，修正 Song Select 左上 `BPM` 统计这类 raw working beatmap consumer 对 BMS imported chart 恒回退默认 `60 BPM` 的问题。
- 这次修补不改变 BMS Song Select 的 BPM 分组 / 排序 authority；分组与排序仍继续消费 persisted `BeatmapInfo.BPM`，本轮只把 raw display chain 与之重新对齐。
- 新增 `BmsImportIntegrationTest.TestLoaderPopulatesTimingDataForSongSelectDisplays()`，锁定 BMS loader 返回的 raw beatmap 已具备正确 timing point、most-common beat length 与 hitobject 数据。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsImportIntegrationTest"` **23/23** 通过。

## 2026-04-29

### 难度表前三批 correctness 修补落地

- `BmsDifficultyTableManager` 现已正式拥有 persisted beatmap metadata 回写职责；`import / refresh / enable / disable / remove` 之后会先回写 realm，再发 `TableDataChanged`。`BmsTableMd5Index` 已收窄成纯内存索引，不再承担 persisted metadata authority。
- `RefreshAllTables()` 现返回结构化结果，settings 页已改为按真实结果区分全成功、部分成功与全失败，不再 blanket success。
- `RefreshAllTables()` 现还会逐源报告进度；settings 页的“全部刷新”在执行期间会持续更新摘要，并用 `ProgressNotification` 展示已处理 / 成功 / 失败计数，不再只有最终结果通知。
- wrapper / header 的 stable fallback identity 已补齐，同时把 preset 认领收紧为“只有 display name 本身就是 fallback 时，才允许按 `source_name` 命中 preset”，避免显式名称被误认领。
- 响应性后置的首个切片也已落地：persisted metadata 回写现会按受影响 MD5 集合定位 beatmap id，并按批次写入，而不是在单个长事务里全量扫描并重写所有 BMS 谱面。
- `BmsFolderImporter` 在复用已有 beatmap set 时也会重新按当前 table index 套用 difficulty table metadata，补上 internal/external rebuild 命中旧 set 时的自愈路径；这条修补不放宽 MD5 口径，只修复 reuse path 不重套 metadata 的缺口。
- 验证：难度表 manager / importer / wrapper identity / batched persisted update / refresh progress / managed reuse recovery 六组聚焦回归共 **22/22** 通过。

### 难度表修补专题归线与执行约束建档

- 新增 `P1-H` 内部修补专题：**BMS 难度表一致性与刷新合同收口**。该专题不重开 `1.13` / `1.15`，也不新建独立子线；主归属固定为 `P1-H`，`P1-A` 只记录 settings / first-run 共享产品表面的从属影响。
- 当前文档已把推进顺序正式收口为：`既有谱面 metadata 同步` → `RefreshAll 真实结果合同` → `wrapper/source identity fallback` → `大库响应性`。
- 本轮仅完成文档与约束建档，无代码变更、无新增测试执行。

## 2026-04-23

### 首次启动向导导入页复用外部谱库设置面

- `ScreenImportFromStable` 现直接嵌入 `ExternalLibrarySettings`，把外部谱库添加 / 扫描入口带到首次启动向导；底层仍复用同一套 `ExternalLibraryConfig` / `ExternalLibraryScanner` 合同，不新增第二套导入逻辑。
- 这次只增加 product-surface 暴露面，不改变 `外部谱库` / `内部谱库` 的职责分离；主归属仍是 `P1-A`，`P1-H` 记录为存储入口复用的从属影响。
- 验证：`dotnet test osu.Game.Tests --filter "FullyQualifiedName~TestSceneFirstRunScreenBehaviour|FullyQualifiedName~TestSceneFirstRunSetupOverlay|FullyQualifiedName~TestSceneFirstRunScreenImportFromStable" --configuration Release` **11/11** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### 扩展 Maintenance 谱库扫描为四模式，并把内部扫描独立成新子页

- `Settings -> Maintenance` 现已新增 `内部谱库` subsection，原 `外部谱库` subsection 中的内部扫描按钮已迁移出去；当前谱库扫描入口已扩成：`扫描外部谱库（重建）`、`扫描外部谱库（增量）`、`扫描内部谱库（重建）`、`扫描内部谱库（增量）`。
- `ExternalLibraryScanner` 与 `ManagedLibraryScanner` 现新增 `ScanMode`（`Rebuild` / `Incremental`），并支持按目录判断“是否仍需导入”的回调；桌面端会把这条判定下推到 BMS / mania importer。
- 当前 `增量` 模式只补导没有 active `FilesystemStoragePath` 记录的目录；`重建` 模式继续重走全部候选目录。新增 `ExternalLibraryScannerTest` 两条回归，锁定“增量会跳过已索引目录”“重建不会受增量过滤影响”两条模式语义。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --filter "FullyQualifiedName~ExternalLibraryScannerTest"` **6/6** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### 修复内部谱库扫描对 managed roots 的尾分隔符误判

- `Settings -> Maintenance` 的谱库扫描口径现已收口为两条链：`扫描外部谱库` 只处理已注册外部根；`扫描内部谱库` 只重建 `chartbms/` / `chartmania/` 的托管根索引。
- 修复 `FilesystemSanityCheckHelpers.IsSubDirectory()` 在 managed root 带尾部分隔符时可能把合法子目录误判为“不在根下”的问题；当前会先对两侧执行 `Path.TrimEndingDirectorySeparator()` 再比较。
- `BmsFolderImporter` / `ManiaFolderImporter` 的 `RegisterManagedDirectory()` 现可稳定接受数据根内已有目录；新增 `FilesystemSanityCheckHelpersTest` 锁定 child-under-parent 与 same-directory 两条回归。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter "FullyQualifiedName~FilesystemSanityCheckHelpersTest"` **2/2** 通过。

## 2026-04-20

### 子线正式建档

- `P1-H` 已建立独立目录与四件套文档。
- 当前仅完成文档结构治理，未新增代码、构建或测试执行。
