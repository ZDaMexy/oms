# P1-H 开发进度：存储拓扑支撑线

> 最后更新：2026-04-23

## 当前阶段

- `P1-H` 已完成子线建档。
- 当前仓库已具备 `chartbms/`、`chartmania/`、`portable.ini -> data/` 与四模式谱库扫描基线；`ExternalLibraryConfig` / `ExternalLibraryScanner` 负责已注册外部根的 `重建 / 增量`，`ManagedLibraryScanner` 负责当前数据根下 `chartbms/` / `chartmania/` 的 `重建 / 增量`。
- `OsuGameDesktop` 已把 BMS / mania 两类根目录注册进 managed library scanner；当前内部托管谱库补扫路径已修复尾部分隔符误判，合法的 managed 子目录不会再因为 `IsSubDirectory()` 比较口径不一致而被拒绝。
- `Settings -> Maintenance` 现已从原先“外部谱库 subsection 混放外部/内部扫描”改为 `外部谱库` / `内部谱库` 双 subsection；内部两种扫描语义已完成层级隔离。
- `ExternalLibrarySettings` 现也被首次启动向导导入页直接复用，作为 OMS onboarding 的外部谱库导流入口；该入口不新增任何独立扫描逻辑，仍共享 `P1-H` 的外部谱库 contract。
- 当前剩余重点仍是删除 / 失效语义、path identity dedup 与重扫策略。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 子线建档 | 已完成 | 四件套已建立 |
| 基础存储拓扑 | 已完成 | `chartbms/` / `chartmania/` / `storage.ini` / `portable.ini` 主链已落地 |
| 外部谱库管理 | 已完成 | `ExternalLibraryConfig` / `ExternalLibraryScanner` + `外部谱库` subsection 已接通，现支持 `重建 / 增量`，并已复用于首次启动向导导入页 |
| 内部谱库重扫 | 已完成 | `ManagedLibraryScanner` + `内部谱库` subsection 已接通，现支持 `重建 / 增量`；尾分隔符误判已修复 |
| 删除 / 失效语义 | 未开始 | 待收口 |
| path identity dedup / 重扫策略 | 未开始 | 待收口 |

## 验证记录

- 2026-04-23：扩展谱库扫描拓扑为 `外部/内部 × 重建/增量` 四模式，并把内部两种扫描迁移到新的 `内部谱库` subsection。`增量` 模式只补导当前没有 active `FilesystemStoragePath` 记录的目录；`重建` 模式继续重走全部候选目录。`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --filter "FullyQualifiedName~ExternalLibraryScannerTest"` **6/6** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。
- 2026-04-23：修复内部托管谱库重扫对 managed roots 的尾分隔符误判；`FilesystemSanityCheckHelpers.IsSubDirectory()` 现先归一化尾部分隔符，再比较父子目录链。`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter "FullyQualifiedName~FilesystemSanityCheckHelpersTest"` **2/2** 通过。
- 2026-04-23：首次启动向导导入页复用 `ExternalLibrarySettings` 作为 secondary product-surface entry；底层仍共享同一套外部谱库注册与扫描 contract，没有新增独立 storage 逻辑。`dotnet test osu.Game.Tests --filter "FullyQualifiedName~TestSceneFirstRunScreenBehaviour|FullyQualifiedName~TestSceneFirstRunSetupOverlay|FullyQualifiedName~TestSceneFirstRunScreenImportFromStable" --configuration Release` **11/11** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。