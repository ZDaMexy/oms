# P1-H 变动日志

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
