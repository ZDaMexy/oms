# P1-H 当前计划：存储拓扑

> 最后更新：2026-07-16
> 当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，稳定路径/扫描合同见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)，已完成难度表与扫描治理按日期查 [CHANGELOG.md](CHANGELOG.md)。

## 子线职责

P1-H 维护 `chartbms/`、`chartmania/`、portable/custom data root、managed/external 谱库及其 Realm/path authority。它为导入、发行和 P1-A/G1 提供路径经验，但不输出可直接复制的皮肤 scanner/delete 实现。

## 已完成基线

| 面 | 当前结果 |
| --- | --- |
| 文件系统谱库 | BMS/mania 直读目录与 managed/external scanner 已落 |
| 数据根 | `portable.ini → data/` 与 `storage.ini` custom root 已落 |
| 扫描入口 | external/managed 各自支持重建与增量，职责分离 |
| 难度表一致性 | manager-owned metadata sync、真实 refresh 结果、identity/fallback、批量写回与 reuse recovery 已收口 |
| persisted coexistence | 难度表、converted star 等共享 `RulesetData` 时保留未知 JSON 字段 |
| raw wrapper | timing/hitobject/break authority 可复用，Song Select BPM 不回退默认值 |

完成批次、修复过程和旧测试数字不在 PLAN 重述，统一查 [CHANGELOG](CHANGELOG.md)。

## 当前执行顺序

### 1. 删除、失效与重扫语义

1. 明确源目录消失、单谱删除、set 变空、重命名和重新出现时 Realm record、目录与选择状态的行为。
2. external root 永远只读；“删除”只能解除注册或标记失效，不能删用户目录。
3. managed 删除必须通过 owning scanner/command、resolved-root containment 与冲突检查；不得让增量扫描顺手清理未知记录。
4. `重建` 重走全部候选目录；`增量` 只补不存在 active filesystem record 的目录，不得混写成隐式 repair-all。

验收：备份数据根上覆盖存在→缺失→重命名→恢复→重扫矩阵，确认 Realm、磁盘和 UI 结果一致且可恢复。

### 2. path identity 与重复 root

1. 冻结规范化路径、大小写、尾分隔符、相对/绝对和 managed/external root 的 identity 规则。
2. 同一物理目录被重复注册、父子 root 重叠、portable/custom root 切换时必须给出确定结果，不能重复导入或跨 authority 接管。
3. reparse-point/symlink 风险需要显式判定；在合同冻结前 fail-closed，不做模糊字符串前缀判断。
4. 改 `FilesystemStoragePath`/`LocalFilePath` 约定时同步所有只读 consumer，包括资源管理器定位与 BGA 路径解析。

验收：Windows 大小写、分隔符、同目录别名、父子 root、重启和数据根迁移矩阵有 focused coverage。

### 3. 现场只读诊断

1. 对 difficulty-table/MD5/source identity 不匹配输出脱敏、可定位的只读诊断。
2. 先区分 persisted 字段覆盖、原始字节 MD5、carousel 中途未刷新与真实 scanner 缺口。
3. 不在 UI 卡顿路径逐谱加载 working beatmap、全库重算或写 Realm。

验收：诊断可解释“为何未匹配/未刷新”，不泄露用户绝对路径、不改变库状态，且能明确下一 owning 子线。

## 向 P1-A/G1 输出的边界

- 可复用：managed/external authority 分离、native path containment、scanner 只维护自身记录、批量 Realm 写回和诊断模式。
- 不可复制：谱面 scanner 的删除/失效规则、unknown-record cleanup 或把外部目录当 managed 的写权限。
- G1 必须另行冻结 package identity、skin selection、rename/delete 与 atomic reload。

## 明确不做

- 不恢复远端同步、backend 镜像或定时联网刷新。
- 不把难度表来源管理泛化为跨 ruleset 平台。
- 不以大库性能优化替代 correctness；性能改动必须先有现场 profile。
