---
name: reference_skin_managed_folder_mutation_foundation
description: managed mutation 的 NTFS handoff、首写 journal、恢复歧义与 current delete 失败边界
metadata:
  node_type: memory
  type: reference
---

# Managed chartskin mutation 地雷

完整资格、v1/v2/v3 schema、phase 图与恢复矩阵唯一来源是 [P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md) 的 G1 存储章节；完成态读 [STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)，实现证据按操作搜 [CHANGELOG](../../doc_md/subline/P1-A/CHANGELOG.md)。本页保留查故障时容易漏掉的前提，不复制整份合同。

## 线性化与权限不要混淆

- scanner 从 native discovery 到 Realm reconcile 都持同一 coordinator；selection 最终重读/发布、mutation、recovery 也在此边界内。发布时取本 Realm 的 fresh Live record，不用调用方旧对象。
- startup 与 staged-import 有 typed completion，可让 configured selection 等待后 fresh retry；generic mutation 没有。generic epoch 跨越必须拒绝，不能把所有 contention 都改成重试；manual 请求与 configured startup 路径分开。
- ordinary 短 lease 只同线程嵌套；mutation reservation 不可重入，但可跨线程 dispose。holder ownership 必须与 admission 同锁可见，不能出现 semaphore 已占用而 holder 尚未发布的空窗。
- manager 构造先 recovery；启动 worker 在同一 typed sequence 中再次幂等 recovery 后立刻 scanner。shutdown 在 Realm 释放前 cancel + join startup、selection/reload、mutation 和 materializer/work/retire，queued completion 须 reap 或晚到 no-op。
- scanner/service owner 只是 Realm 记录归属，不是文件写权限。foreign/null owner、root/path/record 冲突不允许“修好 owner 后继续”；目标资格按 ID fresh 重读，文件能力来自 held no-follow session。
- 合法非重叠 external 不再全局阻断 managed mutation。v3 必须持有 exact declaration set + physical non-overlap proof 到 final collision；旧 v2 按“当时无 service-owned external”的冻结合同恢复，不能静默补 v3 字段。

## 首个物理步骤与 durable intent

- target 是同 held root 下经过 NFC/Windows/case-insensitive collision 检查的 name slot；创建前没有 physical identity。staging/managed authority root 必须既存、同卷且 held，不能临时创建替代。
- ManagedCopy 只接 external record ID 与用户明确 target child；manager 生成 operation/staging。fresh capture 同时给 exact capsule 和含空目录的 logical manifest：文件 bytes 只读 capsule，目录只按 paired manifest 重建，不重开 external path。
- 创建 provisional root/写首字节已经是 filesystem mutation，发生在旧 StagedImport Prepared 之前。因此要用 single canonical v3 combined intent 在首写前 durable 绑定 capsule、manifest、external proof、root 与槽位；不能另起 copy journal，或按 operation-like 名称/年龄清 staging。
- Prepared 持久化后要 exact reload 才拿 durable receipt；它绑定 session/store/intent，publication plan 本身不是 Realm 写权限。one-shot publisher 只在 durable FilesystemApplied、target recapture 与最终 Realm 冲突复核后接管 exact record。
- Rename 只改工作目录与同 record 的 FilesystemStoragePath，不改 skin.ini Name、Realm Name/Creator、内容或 revision。Import 不自动选择，不取消无关 pending selection；Rename 会使旧 selection generation 失效。不要把两个操作的副作用套用。

## NTFS 实证：descendant handles 不能跨目录 move

- 非空目录的 descendant handles 即使允许 FILE_SHARE_DELETE，`NtSetInformationFile(FileRenameInformationEx)` flags 0 或诊断 POSIX 0x2 仍会返回 STATUS_ACCESS_DENIED (0xC0000022)。
- 正确 handoff：最终完整 preflight + 取消检查 → 只释放 descendant handles → 保留 source directory 与两侧 authority root → held-parent-relative、同卷 no-replace move → CancellationToken.None 完整 no-follow target recapture → 核对 source identity、capsule revision 与 exact tree。
- release→move→recapture 有窄窗，不是 oplock/TxF/filesystem transaction，也不是字节级原子快照。任何可观察 drift 都保留 journal 并冻结，不能写成 handles 跨 move 全程排他。
- staged source 的 DELETE access 会合法推进父目录项 metadata：source node 自身 pinned metadata 仍须 exact，provisional parent inventory 比 name/identity/kind；target package root 仅放行明确 rename-related time 变化。不能顺便放宽 descendant metadata、内容或其它 gate。
- move attempted 后不再观察 caller cancellation；否则可能把已发生物理变化伪装成“取消且无副作用”。

## Journal / recovery 排查

- canonical 文件是 `skin-managed-mutation-journal.json`；严格 schema/UTF-8/重复字段/校验和/大小预算与 version-kind-phase 闭集由合同规定。Copying/ProvisionalReady 只属于 ManagedCopy，不能复用到 Rename/Delete；旧 version 不能靠 optional 字段升级。
- write-through + Flush(true) + exact reload 是 receipt 前提。canonical 是目录/reparse、锁定、ACL/IO 或未知 journal-like sibling 都不是 Missing；只可清理被精确证明未发布的 orphan temp。
- 首个物理步骤前若 authority 漂移、canonical receipt 仍 exact，可按合同 RolledBack + compare-delete；写入结果/receipt 不确定就冻结，不伪装安全 abort。
- terminal 也不是立即可删：先复核 held native/external authority 与 exact Realm，再 compare-delete，随后确认为 Missing 才解冻。读异常、foreign/invalid、歧义后突然 Missing 均不能算成功。
- recovery 逐 phase write + exact reload；“物理看起来终态”不允许跳过 durable phase。无 handler、未知/invalid/IO 不能安全定位时冻结整个 managed namespace。
- UI 只可显示脱敏 kind/phase/status/reason 与可证明安全的 Retry；不提供删 journal、选择恢复方向或强制解冻。path、record/operation ID、identity、hash/native 异常正文不进日志。

### 最容易被误清理的恢复状态

| 操作 | 可恢复依据 | 保留并冻结的情况 |
| --- | --- | --- |
| Rename | exact SourceOnly/TargetOnly + 同 record 的 source/target path 决定回滚或前滚；只改该 record path | Both/Neither/identity mismatch、Realm path 在两者之外或资格漂移 |
| Staged import | TargetExact + Realm absent 可 one-shot publish；SourceExact + TargetAbsent 可删 exact planned record 并清 provisional | foreign/conflicting record、root/槽位 identity drift、Both 或证据不足；不得删 managed target/外部源 |
| provisional 已部分清理 | durable root/source identity 与节点 proof 只允许继续清 exact OMS owned subset；Neither 还须证明原 external 来源保留 | 不能因 capsule 不再可完整重捕就扫整个 staging；非空不完整 ManagedCopy 保持 Ambiguous |
| Managed Delete | source/tombstone/manifest/Realm fingerprint/fallback disposition 与 phase 一致才推进；Realm 仅在 durable FilesystemApplied 后 compare-remove | raw disposition 却 TargetOnly/Neither、foreign node、缺证据；保留 journal 与 record |

详细矩阵只以合同为准，表格不是替代恢复算法。

## Delete 特有窄窗

- current managed Delete 先 held capture 证明 target/content=current，再通过同一 publication transaction 切 verified fallback 并等 ConsumersDetached；这之前无 journal/物理步骤，失败恢复 exact A。之后才 durable Prepared + ProtectedPairCommitted；首个 physical 后不确定失败保持 fallback，交 recovery，不承诺恢复 A。
- 确认框只持 detached record ID/immutable label。操作开始 fresh 判 current/noncurrent/split，不能沿用开框快照或先选中目标；NotRequired 只适用于三元 current authority coherent 且均非目标。
- source→operation-derived tombstone detach 是首个外部步骤。move 后 verification handles 无 DELETE 权；验证、释放后，再从 held root 捕获 DELETE + share READ 的 delete-exclusive tree。
- same-session 重新捕获要求 exact manifest，窄窗移出节点在零 disposition 时拒绝；只有 fresh recovery 的 partial survivor 可按 durable 子集处理，不能混用。
- directory handle 不封 namespace。final preflight 后新增/replacement 不进入 held delete list；可能删完部分 owned 节点后 root 删除失败，此时保留 journal/Realm 并冻结。foreign、sibling、managed root、caller path 永不删除。
- ProtectedPairCommitted 的恢复终态仍须 exact protected fallback Realm record；NotRequired 不要求。重启恢复不能宣称重验已不存在的旧 runtime detach。
- 分开等 fallback publication、old detach、physical/Realm completion；一个总 timeout 会把 fixture cleanup 误导回旧同步 Delete。callback/shutdown 恰一方 claim/reap，先完成 TCS 再发可能重入的 SourceChanged。

current external Unregister 始终 pure-Realm/source 零 I/O，ordinary .osk Delete 是 soft-delete；两者不套用 managed physical-delete 矩阵。fixed-source staged import 仍非独立用户能力，其公共底层被 ManagedCopy 复用。

关联：[[reference_skin_atomic_reload_detach]]、[[reference_skin_managed_folder_scanner]]、[[reference_skin_managed_folder_selection]]、[[reference_skin_external_workspace_managed_copy]]。
