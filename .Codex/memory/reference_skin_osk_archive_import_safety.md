---
name: reference_skin_osk_archive_import_safety
description: ordinary .osk导入在archive枚举/读取/hash/Files.Add前的恶意归档门、流式预算与失败零残留地雷
metadata:
  node_type: memory
  type: reference
---

# Ordinary `.osk` archive import safety 地雷

> 实时范围、数值预算与完成状态只看[P1-A PLAN](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md)、[STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)和[CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)。本页记录C1开工前已确认的代码风险，不能当作已实现能力。

## 当前无界读取点

- `RealmArchiveModelImporter.Import()`先由`ImportTask.GetReader()`构造`ZipArchiveReader`，随后`CreateModel`、batch `computeHashFast()`、`getShortenedFilenames()`与`Files.Add()`都会消费archive；只在`SkinImporter.Populate()`或runtime纹理加载处检查已经太晚。
- `ZipArchiveReader.GetStream()`在declared `entry.Size > 0`时把`long`强转为`int`后一次分配并`ReadExactly`；size为0时为规避SharpCompress历史bug使用`ReadAllRemainingBytesToArray()`，对unknown/data-descriptor/欺骗header没有流式硬上限。
- `RealmArchiveModelImporter`会先枚举/缩短公共前缀、fast-hash内容，再逐entry写`RealmFileStore`，最后才进入模型Realm事务。恶意name冲突或中途失败若只在后段发现，可能已经消耗大量内存/IO或留下本次file-store副作用；必须用产品级零残留测试证明收口，不能从transaction rollback推断。

## C1硬门

- ordinary `.osk`使用两层skin-scoped preflight：在`ImportTask.GetReader`/`ZipArchive.Open`前只限制compressed source length并把非seekable stream写入bounded spool；bounded archive open后立即有界枚举central-directory metadata，完成entry/name/declared-size gate后才允许`Filenames`向后续暴露。任何`GetStream`、fast hash、`Files.Add`、model construction或`SkinInfo` publication都在完整准入之后；实际copy/hash流再次硬计数并观察cancellation，不信任declared size。
- name gate同时检查raw与common-prefix-shortened结果：slash/NFC/Windows case-fold重复、file-directory conflict、空名、traversal、ADS/device/trailing-dot-space及路径/层级上限；encrypted、overflow、truncated/CRC、symlink/special entry和zero/unknown/data-descriptor必须bounded accept或typed reject。
- `skininfo.json`中的untrusted `InstantiationInfo`必须经过closed compatibility allowlist或稳定legacy fallback，禁止任意CLR type activation；archive ingress预算与runtime texture/decode预算是两层不同合同，不得互相冒充。
- 拒绝、取消或失败不产生`SkinInfo`、Realm file reference、仅由本次创建的orphan blob/temp，也不能误删共享content-addressed blob；原`.osk`保留，只有成功继续既有`ShouldDeleteArchive`语义。Shift-JIS、公共顶层目录、`Skin.InI`及历史有效包必须回归。
- 优先只改Skin importer路径；若为获得entry metadata必须修改共享`ZipArchiveReader`、`ImportTask`或`RealmArchiveModelImporter`，同步跑beatmap与其它archive importer宽回归。普通`.osk`继续是hash-backed Realm package，不得转为external/managed或接入managed-copy stager。

## 测试地雷

- 不能只生成declared-size正常的小zip；必须覆盖size为0却有内容、unknown/data-descriptor、超高ratio、声明/实际不一致、并发取消、fast-hash阶段失败、N次`Files.Add`后失败及共享blob已存在的场景。
- 自动断言除typed reason外还要检查：原archive存在、Realm模型/引用集合未增、file store没有本次孤儿、import queue继续可用、后续合法包可成功导入；异常与诊断不得输出用户路径或entry原始敏感文本。
