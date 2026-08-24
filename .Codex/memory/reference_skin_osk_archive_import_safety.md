---
name: reference_skin_osk_archive_import_safety
description: ordinary .osk导入在archive枚举/读取/hash/Files.Add前的恶意归档门、流式预算与失败零残留地雷
metadata:
  node_type: memory
  type: reference
---

# Ordinary `.osk` archive import safety 地雷

> 实时范围、数值预算与完成状态只看[P1-A PLAN](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md)、[STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)和[CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)。本页的skin-scoped安全链已随C1关闭；C2 current revision完成边界见[[reference_skin_atomic_reload_detach]]。

## pre-C1失败链（禁止重新引入）

- `RealmArchiveModelImporter.Import()`先由`ImportTask.GetReader()`构造`ZipArchiveReader`，随后`CreateModel`、batch `computeHashFast()`、`getShortenedFilenames()`与`Files.Add()`都会消费archive；只在`SkinImporter.Populate()`或runtime纹理加载处检查已经太晚。
- `ZipArchiveReader.GetStream()`在declared `entry.Size > 0`时把`long`强转为`int`后一次分配并`ReadExactly`；size为0时为规避SharpCompress历史bug使用`ReadAllRemainingBytesToArray()`，对unknown/data-descriptor/欺骗header没有流式硬上限。
- `RealmArchiveModelImporter`会先枚举/缩短公共前缀、fast-hash内容，再逐entry写`RealmFileStore`，最后才进入模型Realm事务。恶意name冲突或中途失败若只在后段发现，可能已经消耗大量内存/IO或留下本次file-store副作用；必须用产品级零残留测试证明收口，不能从transaction rollback推断。

## 当前实现合同

- ordinary `.osk`使用两层skin-scoped preflight：在`ImportTask.GetReader`/`ZipArchive.Open`前只限制compressed source length并把非seekable stream写入bounded spool；随后由自身受compressed-input、central-directory bytes、entry count与内存预算约束的metadata parser/open立即枚举central-directory，不能先让通用reader无界物化metadata再后置检查。完整entry/name/type/declared-size gate后才允许`Filenames`向后续暴露；任何`GetStream`、fast hash、`Files.Add`、model construction或`SkinInfo` publication都在准入之后，实际copy/hash流再次硬计数并观察cancellation，不信任declared size。
- name gate同时检查raw与common-prefix-shortened结果：slash/NFC/Windows case-fold重复、file-directory conflict、空名、traversal、ADS/device/trailing-dot-space及路径/层级上限；encrypted、overflow、truncated/CRC、symlink/special entry和zero/unknown/data-descriptor必须bounded accept或typed reject。
- `skininfo.json`中的untrusted `InstantiationInfo`必须经过closed compatibility allowlist或稳定legacy fallback，禁止任意CLR type activation；archive ingress预算与runtime texture/decode预算是两层不同合同，不得互相冒充。
- 拒绝、取消或失败不产生`SkinInfo`、Realm file reference、仅由本次创建的orphan blob/temp，也不能误删共享content-addressed blob；原`.osk`保留，只有成功继续既有`ShouldDeleteArchive`语义。cancellation须穿过source spool、entry copy/CRC、metadata rewrite、hash、`Files.Add`与model population，并在最终Realm publication事务前再次检查。Shift-JIS、公共顶层目录、`Skin.InI`及历史有效包必须回归。
- 当前通过`RealmArchiveModelImporter`的protected virtual reader/scope hook仅让`SkinImporter`选择专用reader、关闭fast precheck并启用exact receipt；beatmap/score等默认仍走原reader。共享基类与`RealmFileStore`的默认路径回归已纳入C1退出门。普通`.osk`继续是hash-backed Realm package，不得转为external/managed或接入managed-copy stager。
- bounded ingress/receipt只负责导入安全，不授予reload或mutation authority。ordinary current `.osk`的same-ID Reload使用fresh Realm declaration set与逐blob hash准备新revision；已发布owner不受Realm record的file-declaration path、external或DeletePending projection漂移污染，late renderer仍消费active immutable capsule；fresh reload/mutation重读到path改变造成的declaration mismatch时拒绝，不得误称registry file drift。current Delete先发布protected fallback并等待old detach，再做Realm soft-delete，Realm失败恢复exact旧pair/revision、record与blob。两者都不得放宽importer、update-import或direct current file mutation。

## 2026-08-13 C1关闭合同

- `Skinning/IO/SkinArchiveReader`在任何`ZipArchive`消费前限制path/seekable/nonseekable raw输入；nonseekable使用bounded、可取消、delete-on-close spool，自行解析并冻结EOCD/CEN/local metadata，再只允许Store/Deflate普通文件进入后续reader。
- entry/name/type/压缩与展开预算、CRC、actual bytes、aggregate ratio及cancellation由frozen metadata和实际流双重检查；`skininfo.json`只解析最小DTO并按closed compatibility map canonicalize，未知CLR类型稳定回落，不调用`Type.GetType(raw)`。
- opt-in `RealmFileStore` receipt按`RealmAccess + storage identity + hash`建立进程内participant group；并发same-hash scopes必须共同参与，只有全部participants/adds结束、没有成功或unscoped writer且仍持有rollback资产时才可finalize。任一成功participant、已有usage/backlink或finalization generation被fresh add取代都保留共享资产；每个hash的rollback异常隔离，不能阻止其它hash收口，失败group可由后续同hash scope安全重试。
- Realm record与physical blob的baseline必须独立记账：baseline record存在但blob缺失时只删除本group写出的blob；baseline blob存在但record缺失时只删除本group创建且仍零引用的record；二者都不存在时才分别按各自ownership删除，二者都存在时都保留。不得以“同hash”或单一created flag把两侧捆绑回滚，也不调用全局`Cleanup()`。
- rollback lock order须保持`Realm transaction → import group lock`；scope参与清单在锁内摘取后于锁外逐group finalization，cleanup只能在无active scope/add时进入。storage删除在Realm阶段完成且generation仍exact后执行；任何新add/finalizer竞态都由active计数、generation与group identity复验阻止误删。
- C1已闭合专用reader、receipt、shared importer回归与Release门；这些验证不允许删掉上述恶意archive、取消、same-hash并发与asymmetric baseline地雷。

## 测试地雷

- 不能只生成declared-size正常的小zip；必须覆盖size为0却有内容、unknown/data-descriptor、超高ratio、声明/实际不一致、并发取消、fast-hash阶段失败、N次`Files.Add`后失败、same-hash多participant及record/blob两种asymmetric baseline。
- 自动断言除typed reason外还要检查：原archive存在、Realm模型/引用集合未增、file store没有本次孤儿、import queue继续可用、后续合法包可成功导入；异常与诊断不得输出用户路径或entry原始敏感文本。
