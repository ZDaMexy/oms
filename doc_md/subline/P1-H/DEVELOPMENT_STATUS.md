# P1-H 当前状态：存储拓扑

> 最后更新：2026-09-09（production/测试源审查；未改存储实现）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。

## 当前阶段

文件系统谱库与数据根主链存在；已有基本删除与hash复用，但**缺失源失效、解除根注册后的记录处理、跨root/path identity与重扫一致性尚未闭合**。P1-H 可为皮肤 G1 提供路径经验，不提供可直接复制的删除 authority。

## 已落地能力

- BMS `chartbms/`、mania `chartmania/` 文件系统直读。
- `portable.ini → data/` 与 `storage.ini` 自定义数据根；安装位置迁移只移动运行时数据，不移动程序。
- external/managed library 分离，Settings 中各自提供重建/增量扫描。
- `ExternalLibraryConfig/Scanner` 管理注册外部根；`ManagedLibraryScanner` 管理当前数据根下内部谱库。
- managed 子目录 trailing-separator 归一化已修复；首次启动导入页复用同一外部谱库入口。
- 难度表 manager-owned metadata sync、真实 refresh 结果、wrapper/source identity fallback、分批写回和 reuse recovery 主链已收口。
- converted star 与难度表共享 `RulesetData` 时通过 `[JsonExtensionData]` 保留彼此未知字段，避免互相覆盖。
- raw wrapper 复用 timing/hitobject/break 数据，Song Select BPM 不再回退 60。

## 已实现的删除/重扫边界

- [ExternalLibraryScanner](../../../osu.Game/Beatmaps/ExternalLibraryScanner.cs) 的重建重走候选目录，增量经 importer 的active path谓词跳过已索引目录；缺失root只报告错误/跳过，没有现存record失效回收阶段。[ManagedLibraryScanner](../../../osu.Game/Beatmaps/ManagedLibraryScanner.cs) 复用同一遍历器。
- [ExternalLibrarySettings](../../../osu.Game/Overlays/Settings/Sections/Maintenance/ExternalLibrarySettings.cs) 的移除root只修改 `library-roots.json`，不会同步解除已导入Realm记录。external源物理只读保护已存在，不能把“移除root”写成完整卸载该谱库。
- [BeatmapManager](../../../osu.Game/Beatmaps/BeatmapManager.cs) 的内部删除入口排除external/protected记录，删除managed记录后清理目录；[RealmAccess](../../../osu.Game/Database/RealmAccess.cs) 启动清理DeletePending记录时也排除external目录。它们不等于缺失/重命名/rebuild的完整恢复协议。
- BMS/mania folder importer已有大小写不敏感path比较与同hash复用；reuse先取同hash记录再校验path/authority，不可复用时新注册会将已有同hash记录置DeletePending。当前不是跨root同内容多份记录的统一physical identity策略。
- `ExternalLibraryConfig.AddRoot`目前只有FullPath和大小写不敏感相等去重；尾分隔符、父子root重叠和reparse物理别名尚无统一准入合同，scanner遍历也不能当作held no-follow capture。

## 当前边界

- `重建` 重走候选目录，`增量` 只补不存在 active filesystem record 的目录。
- external 用户目录只读；managed 目录才允许由 OMS 管理。
- 谱面 authority 与皮肤 authority 不可混用。G1 必须单独定义扫描、删除、重命名和 external root 合同。

## 最近一次验证

2026-09-09复验core scanner **7/7**；BMS importer在本轮BMS full范围内通过。精确范围见[审查报告](../../other/PROJECT_PROGRESS_AUDIT_20260909.md)，历史scanner/难度表/raw-wrapper证据见 [CHANGELOG](CHANGELOG.md)。未对用户库执行扫描或删除。现存 [ExternalLibraryScannerTest](../../../osu.Game.Tests/Beatmaps/ExternalLibraryScannerTest.cs) 覆盖递归/root传递及incremental/rebuild；[BmsImportIntegrationTest](../../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs) 覆盖external只读、managed相对路径、metadata reuse与external目录不被删除。这些不是未完成失效/去重/恢复矩阵的证明。

## 下一检查点

1. 以已有删除/复用行为为基线，闭合缺失、重命名、移除root、同hash跨root及重扫后的Realm/磁盘/UI矩阵。
2. 定义规范化 path identity、大小写和重复 root 处理。
3. 为现场 MD5/难度表不匹配提供只读诊断，不在 UI 卡顿路径做全量重算。
4. 向 P1-A/G1 只输出可复用路径原则，不输出可直接复制的 importer 实现。

## 文档治理验证

2026-09-09区分已实现基本行为与尚未闭合的存储策略；未改代码、未迁移/扫描/删除用户谱库。全仓实际运行结果由本次主线审查记录汇总。
