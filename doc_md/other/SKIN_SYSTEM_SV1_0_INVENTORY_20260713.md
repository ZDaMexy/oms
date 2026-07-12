# Skin V1 `SV1-0` 数据安全门报告（2026-07-13）

> 本报告只记录恢复后 schema 56 的只读取证与 stop/go 结论。当前执行 authority 仍是 [P1-A PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md) 与 [P1-A CONSTRAINTS](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)；本文不授权任何生产数据修复。

## 结论

`SV1-0` 自动 focused gate 与恢复基线一致，folder-backed 路径面没有异常；但生产 Realm 中有两条 `SkinInfo.InstantiationInfo` 仍引用恢复树已不存在的异常期类型 `BmsOmsReferenceSkin`，其中一条是 managed hash-backed 记录。该记录若被选择，当前 `SkinInfo.CreateInstance()` 会进入历史 `TrianglesSkin` 兼容 fallback，而不会还原为该包应使用的 `BmsLegacySkin`。因此数据安全门判定为 **STOP**：本窗口不进入 `SV1-1`，不启动生产客户端，不自动迁移、删除或重写任何记录。

## 取证方法与零写入证据

1. 确认 OMS/osu 进程数为 0。
2. 从 release 配置解析自定义生产数据 authority；报告不记录用户绝对路径。
3. 在未通过 Realm API 打开生产文件的前提下，记录 `client.realm` 的 length、mtime、SHA-256，并复制到系统临时目录。
4. 确认副本的 length、mtime、SHA-256 与生产文件完全一致。
5. 仅以 Realm SDK 的 dynamic + read-only 模式打开副本；没有使用会迁移/写入的 `RealmAccess`。
6. 清点结束后再次记录生产文件元数据与哈希，并再次确认 OMS/osu 进程数为 0。

| 证据 | 取证前 | 取证后 | 判读 |
| --- | --- | --- | --- |
| length | `108,003,328` bytes | `108,003,328` bytes | 一致 |
| mtime UTC | `2026-07-04T20:44:36.3218348Z` | `2026-07-04T20:44:36.3218348Z` | 一致 |
| SHA-256 | `FB9E4BF7F106D0B0898B3104041380DE42009BBB1558B5204C8EC141AE5AFB40` | 同左 | 一致 |
| OMS/osu 进程 | `0` | `0` | 无客户端写入源 |

生产 Realm、`chartskin/` 与用户皮肤目录均未被写入、清理、迁移、删除、重命名或自动修复。

## schema 56 清点

副本中的 `Skin` schema 确认包含 `FilesystemStoragePath` 与 `IsExternalFilesystemStorage`。

| 项目 | 结果 |
| --- | --- |
| `SkinInfo` 总数 | `3` |
| `FilesystemStoragePath` 非空 | `0` |
| `IsExternalFilesystemStorage == true` | `0` |
| folder-backed 记录 | `0` |
| managed/external 路径重复、冲突或越界 | `0` |
| `DeletePending` | `0` |
| `chartskin/` | 存在但为空 |
| 当前选择 | OMS 固定内置 ID；protected built-in authority |
| 其它记录 | 两条 managed hash-backed `.osk`；路径字段均为空，`Files` 数分别为 `6` 与 `4` |

为避免泄露用户皮肤名称、文件名、绝对路径与内容哈希，本报告只保留 authority、记录类别和数量。

## 异常期遗留与影响

- 当前 selected/protected OMS 记录的名称、creator 与 `InstantiationInfo` 仍是异常期 reference-default 值。当前 `SkinManager` 正常启动会按固定 OMS ID 将它重写回 `OmsSkin`，但本次取证没有启动客户端或触发该写入。
- 一条 managed hash-backed 记录也引用已不存在的 `BmsOmsReferenceSkin`。启动时的 protected-record 修正不会处理它。
- `Type.GetType()` 无法解析该类型时，当前 `SkinInfo.CreateInstance()` 会返回 `TrianglesSkin`。这既不能证明包内 mania/BMS 素材被正确读取，也与 OMS 最终产品不得依赖上游默认视觉的方向冲突。
- 该问题不是 folder authority 冲突，但属于异常期数据遗留和明确迁移决策项；在用户决定前不得把数据 gate 或实机 gate 标记为通过。

## 迁移选项（均需用户明确选择后另开切片）

1. **保全后重导入（建议）**：先再次备份 Realm 与关联 hash-backed 文件；把该 managed 记录导出到隔离位置，验证 `skin.ini`/素材，再经当前 `.osk` importer 新建 `BmsLegacySkin` 记录。新记录通过选择、mania/BMS 与 partial fallback 验收后，才决定是否移除旧记录。该路线不原地猜改类型，回退最清晰。
2. **仅保留并继续阻塞**：保持生产数据原样，不选择该记录；`SV1-1` 与实机 gate 继续等待，直到迁移工具和验收方案获批。
3. **保全后移除**：若用户确认该 managed 记录只是异常期生成的无用副本，先导出/备份，再通过显式、定点操作删除；不得由 scanner 或启动清理批量完成。

无论选择哪条路线，protected OMS 固定记录的恢复也应作为同一迁移演练的可见步骤记录，不能把一次普通启动造成的静默重写当作已完成的数据迁移证明。

## 自动验证与未完成 gate

| 检查 | 结果 | 判读 |
| --- | --- | --- |
| BMS parser/legacy/reference/render focused | **43/43** | 通过 |
| BMS transformer + user fallback | **104/104** | 通过 |
| mania `TestSceneOmsBuiltInSkin` | **84/84** | 通过 |
| mania OMS 默认资源专项 | **1/1** | 未被 BMS reference 覆盖 |
| core skin focused | **57/62** | 5 项与恢复审计同名：1 项 Argon 旧期待、4 项已删 ruleset 的 beatmap archive 依赖；无新失败 |

所有命令均保留告警：每次 9 条 MessagePack 3.1.3 `NU1902`；BMS 命令另见既有 `CS8600` 与 `CA2007`。未使用 `NoWarn`。

实机清单仍全部待用户反馈：无外部皮肤、当前 `.osk` 用户皮肤、partial fallback、BMS 5K/7K/9K/14K、14K S1/S2 双皿素材，以及 mania 默认资源隔离。因为启动生产客户端会重写 protected OMS 记录，本次 STOP 后没有执行 GUI 验收。

## 明确未实施

- 没有新增三态 gameplay slot 合同或 fixture；`SV1-1` 未开始。
- 没有接入 `SkinManager`，没有修改 nullable `ISkin` ABI、provider hierarchy 或 `Drawable.Empty()` 语义。
- 没有实施 G1、layout DTO/solver、shared ini codec、scene/event/script、`oms-simple/oms-complex` 视觉或 `OmsSkin` 删除。
