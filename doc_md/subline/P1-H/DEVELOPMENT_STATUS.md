# P1-H 当前状态：存储拓扑

> 最后更新：2026-07-16（文档健康治理；功能状态未改变）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。

## 当前阶段

文件系统谱库与数据根主链已经落地；当前剩余是删除/失效、path identity 去重和重扫策略。P1-H 可为皮肤 G1 提供路径经验，但不得直接复用谱面扫描的删除 authority。

## 已落地能力

- BMS `chartbms/`、mania `chartmania/` 文件系统直读。
- `portable.ini → data/` 与 `storage.ini` 自定义数据根；安装位置迁移只移动运行时数据，不移动程序。
- external/managed library 分离，Settings 中各自提供重建/增量扫描。
- `ExternalLibraryConfig/Scanner` 管理注册外部根；`ManagedLibraryScanner` 管理当前数据根下内部谱库。
- managed 子目录 trailing-separator 归一化已修复；首次启动导入页复用同一外部谱库入口。
- 难度表 manager-owned metadata sync、真实 refresh 结果、wrapper/source identity fallback、分批写回和 reuse recovery 主链已收口。
- converted star 与难度表共享 `RulesetData` 时通过 `[JsonExtensionData]` 保留彼此未知字段，避免互相覆盖。
- raw wrapper 复用 timing/hitobject/break 数据，Song Select BPM 不再回退 60。

## 当前边界

- `重建` 重走候选目录，`增量` 只补不存在 active filesystem record 的目录。
- external 用户目录只读；managed 目录才允许由 OMS 管理。
- 谱面 authority 与皮肤 authority 不可混用。G1 必须单独定义扫描、删除、重命名和 external root 合同。

## 当前验证

- 全局最新产品验证统一见 [mainline STATUS 的“最近一次验证”](../../mainline/DEVELOPMENT_STATUS.md#最近一次验证)；2026-07-16 仅治理文档，未运行产品测试或 Release。
- scanner/难度表/raw-wrapper 的本线历史 focused/full 数字和命令只查 [CHANGELOG.md](CHANGELOG.md)，不冒充当前全局 gate。

## 下一检查点

1. 定义删除/失效与重扫后的 Realm/目录行为。
2. 定义规范化 path identity、大小写和重复 root 处理。
3. 为现场 MD5/难度表不匹配提供只读诊断，不在 UI 卡顿路径做全量重算。
4. 向 P1-A/G1 只输出可复用路径原则，不输出可直接复制的 importer 实现。
