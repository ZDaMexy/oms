# P1-H 开发进度：存储拓扑支撑线

> 最后更新：2026-05-09

## 当前阶段

- `P1-H` 已完成子线建档。
- 当前仓库已具备 `chartbms/`、`chartmania/`、`portable.ini -> data/` 与四模式谱库扫描基线；`ExternalLibraryConfig` / `ExternalLibraryScanner` 负责已注册外部根的 `重建 / 增量`，`ManagedLibraryScanner` 负责当前数据根下 `chartbms/` / `chartmania/` 的 `重建 / 增量`。
- `OsuGameDesktop` 已把 BMS / mania 两类根目录注册进 managed library scanner；当前内部托管谱库补扫路径已修复尾部分隔符误判，合法的 managed 子目录不会再因为 `IsSubDirectory()` 比较口径不一致而被拒绝。
- `Settings -> Maintenance` 现已从原先“外部谱库 subsection 混放外部/内部扫描”改为 `外部谱库` / `内部谱库` 双 subsection；内部两种扫描语义已完成层级隔离。
- `ExternalLibrarySettings` 现也被首次启动向导导入页直接复用，作为 OMS onboarding 的外部谱库导流入口；该入口不新增任何独立扫描逻辑，仍共享 `P1-H` 的外部谱库 contract。
- Settings → 常规 → 安装位置 当前已把入口明确为 `更改数据目录位置`；它只切换/迁移运行时数据根，不移动程序文件。空目录会直接迁入当前数据内容，非空非数据目录会改用其下 `oms/` 子目录，已是可用数据目录则只写 `storage.ini` 并在重启后切换。
- 当前另有一条已正式归线的 `P1-H` 修补专题：**BMS 难度表一致性与刷新合同**。其中前三批 correctness 修补已经落地：manager-owned metadata sync、`RefreshAll` 真实结果合同、以及 wrapper/source identity fallback 均已接通并经聚焦回归验证；与此同时，响应性后置与 reuse 自愈也已继续推进：persisted metadata 回写已改为按受影响 MD5 集合分批写入，`RefreshAll` 已补齐逐源进度合同与 settings 页持续反馈，旧 beatmap set 在 rebuild / reuse 命中时也会重新套用当前难度表 metadata。当前这轮工程修补已可收尾；若后续仍有 `Unrated` 反馈，优先进入现场 MD5 差异诊断，不再视为 consumer-side 分组缺口。
- 当前还补了一条小型但正式归线的 raw-wrapper 显示合同修补：`BmsImportedBeatmapFactory` 现会把首次转换得到的 `ControlPointInfo` / `HitObjects` / `Breaks` 复用回 raw wrapper，使 Song Select 左上 BPM 这类直接读取 `WorkingBeatmap.Beatmap` 的 raw consumer 不再回退到默认 `60 BPM`；BPM 分组 / 排序仍继续使用 persisted `BeatmapInfo.BPM`，本轮不改变其 authority。
- 当前剩余重点已扩展为：删除 / 失效语义、path identity dedup / 重扫策略，以及难度表的后置后台任务化 / 取消策略与现场 MD5 诊断工具化。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 子线建档 | 已完成 | 四件套已建立 |
| 基础存储拓扑 | 已完成 | `chartbms/` / `chartmania/` / `storage.ini` / `portable.ini` 主链已落地 |
| 外部谱库管理 | 已完成 | `ExternalLibraryConfig` / `ExternalLibraryScanner` + `外部谱库` subsection 已接通，现支持 `重建 / 增量`，并已复用于首次启动向导导入页 |
| 内部谱库重扫 | 已完成 | `ManagedLibraryScanner` + `内部谱库` subsection 已接通，现支持 `重建 / 增量`；尾分隔符误判已修复 |
| 难度表一致性 / 刷新合同 | 已完成（当前修补） | 主链 correctness、反馈与 reuse recovery 已收口；剩余仅为后台任务化 / 取消策略后置与 MD5 现场诊断工具化 |
| 删除 / 失效语义 | 未开始 | 待收口 |
| path identity dedup / 重扫策略 | 未开始 | 待收口 |

## 当前验证基线

- 难度表 manager / importer / wrapper identity / reuse recovery 聚焦回归当前累计 **22/22** 通过。
- raw-wrapper timing display 合同当前由 `BmsImportIntegrationTest` **23/23** 锁定；外部 / 内部谱库扫描与路径归一化当前由 `ExternalLibraryScannerTest` **6/6** 与 `FilesystemSanityCheckHelpersTest` **2/2** 锁定。
- 桌面端与 `osu.Game` Release 构建当前可通过；按日期展开的数据根迁移、难度表修补与谱库扫描验证记录见 [CHANGELOG.md](CHANGELOG.md)。
