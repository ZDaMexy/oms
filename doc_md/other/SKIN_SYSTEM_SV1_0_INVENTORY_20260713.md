# Skin V1 `SV1-0` 数据安全门报告（2026-07-13；2026-07-14 闭门）

> 本报告记录恢复后 schema 56 的只读取证、经用户授权的定点迁移与最终实机闭门。当前执行 authority 仍是 [P1-A PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md) 与 [P1-A CONSTRAINTS](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)；本文不授权重复迁移或其它生产数据修复。

## 结论

`SV1-0` 自动 focused gate 与恢复基线一致，folder-backed 路径面没有异常；初次清点发现两条 `SkinInfo.InstantiationInfo` 仍引用恢复树已不存在的异常期类型 `BmsOmsReferenceSkin`，其中一条是该异常期内置 reference-default 生成的 managed mutable copy。初始数据安全门因此 **STOP**。用户随后确认该副本没有保留价值并授权定点处置；备份、副本演练和生产单事务迁移已完成，数据 blocker 解除。2026-07-14 用户又自行确认完整实机清单全部正常，因此自动、数据、实机三门均已通过，`SV1-0` 正式闭门。

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

本节所述初次清点阶段中，生产 Realm、`chartskin/` 与用户皮肤目录均未被写入、清理、迁移、删除、重命名或自动修复。后续经用户授权的定点写入另见“迁移处置”。

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

## 迁移处置（用户授权后已执行）

内容复核确认异常 managed 记录只有自动生成的 `skin.ini`/`skininfo.json` 与通用 HUD/playfield JSON，没有图片、音频、`[Bms]` 配置或其它 gameplay 素材。用户确认其异常期表现没有价值，选择“保全后定点移除”，不重导入、不迁移旧 HUD。

1. 在游戏关闭时建立独立恢复归档，包含迁移前 Realm、lock/management、`game.ini`、`storage.ini` 与四个关联 blob；Realm 和四个 blob 均逐项核对 SHA-256。
2. 在第二代 Realm 副本上以 Realm SDK dynamic 模式演练；预检精确 GUID、旧类型、名称/作者/model hash、4 个 file usages、非 protected、非 folder/external 与总记录数。
3. 单一事务删除异常 managed GUID，并把 OMS 固定 GUID 的 `Name/Creator/InstantiationInfo/Protected` 精确修正为当前 `OmsSkin.CreateInfo()`；不调用 `RealmAccess`、scanner、`RealmFileStore.Cleanup()` 或任何 GUI。
4. 副本演练通过后在生产 Realm 执行同一事务，再以 dynamic read-only 独立重开验证。

| 迁移证据 | 结果 |
| --- | --- |
| 迁移前备份 SHA-256 | `FB9E4BF7F106D0B0898B3104041380DE42009BBB1558B5204C8EC141AE5AFB40` |
| 生产迁移后 SHA-256 | `3761AFDAE7F7F18352DD1932880D560B32DFBACD6411BCE7F2BD70CD647BFBD8` |
| Realm length | 前后均为 `108,003,328` bytes |
| `SkinInfo` | `3 → 2` |
| 异常 managed GUID | 已不存在 |
| OMS 固定记录 | `OMS 默认皮肤` / `OMS 开发组` / `osu.Game.Skinning.OmsSkin, osu.Game` / protected |
| post-migration read-only reopen | `VERIFY_OK_NO_WRITE`，前后哈希一致 |
| OMS/osu 进程 | 全程 `0` |

Realm 本次事务提交后 mtime 仍保持旧值，因此不能把 mtime 单独作为写入/未写入证明；以 SHA-256 和动态 schema 状态联合判定。异常记录的 embedded file usages 已随 parent 删除，但四个物理 blob 与独立 `RealmFile` 行本次刻意保留，不做全局 orphan cleanup；它们无运行时 authority，恢复归档也保存了同内容副本。

## 自动验证与实机闭门

| 检查 | 结果 | 判读 |
| --- | --- | --- |
| BMS parser/legacy/reference/render focused | **43/43** | 通过 |
| BMS transformer + user fallback | **104/104** | 通过 |
| mania `TestSceneOmsBuiltInSkin` | **84/84** | 通过 |
| mania OMS 默认资源专项 | **1/1** | 未被 BMS reference 覆盖 |
| core skin focused | **57/62** | 5 项与恢复审计同名：1 项 Argon 旧期待、4 项已删 ruleset 的 beatmap archive 依赖；无新失败 |

所有命令均保留告警：每次 9 条 MessagePack 3.1.3 `NU1902`；BMS 命令另见既有 `CS8600` 与 `CA2007`。未使用 `NoWarn`。

2026-07-14 用户自行反馈上述实机清单全部正常；Agent 全程未操控 GUI。结合本报告的自动与数据证据，`SV1-0` 已通过。后续 `SV1-1` 首切验证见 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)。

## 明确未实施

- 本报告所述数据处置本身没有新增三态合同；实机闭门后另一个 `SV1-1` 首切才新增平行合同与 fixture。
- 仍没有接入 `SkinManager`，没有修改 nullable `ISkin` ABI、生产 provider hierarchy 或 `Drawable.Empty()` 语义。
- 没有实施 G1、layout DTO/solver、shared ini codec、scene/event/script、`oms-simple/oms-complex` 视觉或 `OmsSkin` 删除。
