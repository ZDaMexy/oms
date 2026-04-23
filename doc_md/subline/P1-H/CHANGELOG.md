# P1-H 变动日志

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