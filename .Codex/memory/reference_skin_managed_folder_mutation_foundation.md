---
name: reference_skin_managed_folder_mutation_foundation
description: chartskin mutation foundation、directory-only rename、durable recovery与NTFS handoff边界
metadata:
  node_type: memory
  type: reference
---

# managed chartskin mutation foundation 地雷

> 快速召回 `SV1-2` 受管目录mutation的公共安全地基。实时能力与下一操作门只看 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md) / [PLAN](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md)；前置链见[[reference_skin_managed_folder_scanner]]与[[reference_skin_managed_folder_selection]]。

## 共享线性化

- scanner、selection、mutation与recovery共用一个`SkinManagedFolderOperationCoordinator`。普通短临界区允许同线程嵌套，以便启动worker用一个外层lease连续执行recovery→scanner；mutation reservation是detached、不可重入的独占持有，跨线程dispose后才释放。
- scanner从native discovery开始一直持有共享边界到Realm reconcile提交结束，不能再把snapshot与Realm commit之间暴露给mutation。managed selection的最终authoritative Realm重读、冻结检查与`CurrentSkinInfo`/`CurrentSkin`发布也在同一边界内；提交时使用本Realm重新取得的`Live<SkinInfo>`，不发布调用方传入的陈旧live对象。
- `SkinManager`构造期先恢复再允许配置选择；`OsuGame`启动worker随后在同一个外层lease内再次幂等恢复并立即scanner，二者之间没有进程内插入点。update-thread selection遇到占用时fail-closed而不阻塞；普通后台Realm请求可等待边界并在之后按既有latest-wins合同提交。

## mutation资格不是写能力

- 既有rename/delete候选每次按ID从Realm刷新重读：必须是唯一合法`chartskin/<direct-child>`、`Files`空、非external/protected/fixed-ID/DeletePending、exact scanner owner、allowlisted实例类型且revision非空；任何external filesystem声明尚无resolved identity，因此当前保守阻断全部managed mutation。
- Windows mutation session从物理本地卷逐段no-follow固定data root与held `chartskin` root。既有source只从该根捕获direct-child identity并持有带DELETE权限、拒绝外部write/delete的handle；target只是一枚同held root绑定、经NFC/Windows命名与case-insensitive collision/absence验证的name slot，绝不能预造physical identity。
- staged source不接受调用方path/token，只能来自data root下固定`skin-mutation-staging/{operationId:N}`，staging root、source与managed root必须同volume并全程持有/复验identity与exact canonical name。未来copy/move语义仍须由staged-import操作切片决定。
- staged import当前只产生immutable publication plan：planned record ID固定为operation ID，并绑定target slot、managed-root identity与version。它**不是Realm写权限**，scanner不得消费；真正one-shot publisher只能在后续staged-import切片达到durable `FilesystemApplied`、固定最终target identity并做最终Realm冲突复核后签发。
- held session在生成或持久化Prepared journal前都会重新验证native inventory/authority links及Realm资格；owner/hash/DeletePending/target collision等post-open漂移一律拒绝。rename已有唯一专用physical move与同记录path-only Realm消费者；staged import/delete仍没有写primitive或Realm新记录publisher。

## directory-only rename真实行为

- 产品语义已冻结：只把一个`chartskin/<direct-child>`工作目录移动到同一held managed root下的空target name slot，并更新同一authoritative Realm record的`FilesystemStoragePath`。目录名是工作区存储身份；`skin.ini [General] Name`、Realm `Name`/`Creator`、包内容、revision/hash和scanner owner都是作者内容/既有身份，rename不得修改。
- 生产链直接消费既有`OpenRename`、held root/source、target slot、shared coordinator、canonical journal和exact durable receipt。首个物理可见步骤前必须durable `Prepared`，之后闭合`FilesystemApplied → RealmApplied → Committed`；可在物理步骤前安全取消时走`RolledBack`并exact-delete terminal journal。
- 成功rename不替换当前skin pair：active immutable capsule继续服务既有consumer；selection generation推进并取消旧路径pending preparation，未来重新选择从新path重新capture。scanner snapshot→Realm commit、selection final commit、rename/recovery共用同一coordinator，`OsuGame.Dispose`在Realm释放前cancel+join rename worker。

## NTFS descendant-handle边界

- 真实NTFS诊断证明：即使descendant handles均允许`FILE_SHARE_DELETE`，非空目录在这些handles仍打开时用`NtSetInformationFile(FileRenameInformationEx)`执行flags `0`或POSIX诊断flag `0x2`均返回`STATUS_ACCESS_DENIED (0xC0000022)`。
- 因此最终完整held-tree preflight和caller取消检查后只释放descendant handles，exact managed-root与source-directory identity继续持有；随后不再观察caller cancellation，立即做held-root-relative no-replace move。move后用`CancellationToken.None`从target no-follow重捕完整树，核对source identity、reparse/hardlink/duplicate/busy-writer、逐节点metadata和inventory并重新持有，才允许Realm path-only publication。
- 这是可恢复的同目录identity路径rename，不是oplock/TxF/filesystem transaction，也不保证release→move→recapture窄窗口的字节级排他。不得误写成descendant handles跨move持有或原子内容快照；任何可观察差异都保留journal并冻结。

## durable journal与恢复

- canonical journal使用稳定文件名`skin-managed-mutation-journal.json`与payload version；严格UTF-8、固定schema/类型、重复字段拒绝、SHA-256校验和、128 KiB上限。新intent只能从Prepared开始，phase按显式图单调推进；terminal不可重写，A intent不能覆盖B intent。
- 写入使用同目录临时文件、write-through、`Flush(true)`与Windows `MoveFileEx(REPLACE_EXISTING|WRITE_THROUGH)`。精确孤儿temp可判定为未发布并清理；canonical目录/reparse、锁定/ACL/IO或未知journal-like sibling不得伪装成Missing。
- mutation session只能经绑定的canonical store持久化，落盘后必须精确reload才返回durable receipt。receipt绑定session、store和exact Prepared journal，消费前后都复验；未解决session dispose或持久化结果不确定会粘性冻结关联路径。无外部mutation时可把Prepared写成RolledBack、精确删除并确认Missing后abort。
- rename已有production recovery handler；其它有效但无operation handler的nonterminal journal仍属歧义，保留journal并精确冻结source/target，scanner对这些路径连negative cleanup都禁止。invalid/unknown/IO无法安全导出路径时冻结整个managed namespace。恢复handler的inspection/action必须回报与journal相同的held managed-root identity，否则保持歧义。
- terminal journal只在compare-delete后再次确认Missing才解除冻结；仍见同一terminal则幂等重试。一次歧义后journal突然Missing不能被当成成功；必须保持冻结到新启动/可证明恢复路径。

## rename恢复矩阵

- `SourceOnly + Realm source`：已回滚；`SourceOnly + Realm target`：只把同记录path回滚到source。
- `TargetOnly + Realm source`：只把同记录path前滚到target；`TargetOnly + Realm target`：已提交。
- `Both`、`Neither`、`IdentityMismatch`、Realm在source/target外、record/root资格或identity不可信：一律歧义，保留journal、冻结source/target并继续禁止scanner negative cleanup。恢复不得发布新record或改展示/内容元数据。

## current delete fallback

- 实际物理delete仍不存在。当前foundation只在update thread、同一mutation reservation与exact durable receipt下确认fallback pair。
- 迁移期唯一允许的fallback是Realm中受保护、非DeletePending、无folder/files声明且实例信息匹配的程序化`OmsSkin`，并要求`CurrentSkinInfo`和`CurrentSkin`最终同时指向exact OMS record/type。canonical接管后这条policy才可替换为只读`oms-simple.osk`。
- `NotRequired`只允许两半ID一致且都不是删除目标；任何split-brain不得放行。fallback无效、selection disabled、提交异常、pair未确认、authority漂移或receipt失效都拒绝未来delete，并在没有发生外部mutation时abort Prepared journal。

## 不可误推

- rename internal production纵切已经实现，但没有UI，也不表示G1、`SV1-2`、Skin V1或reload已交付。staged import与delete仍须分别冻结语义，并通过physical write、final identity、Realm publication、crash-point、幂等恢复、取消、selection竞态与真实Windows gate后才能开放UI。
- rename不联动展示名/`skin.ini`；staged import仍不决定copy/move或冲突策略，delete confirmation仍不执行任何Realm/磁盘删除。
- journal、identity、relative path、operation/record ID与native异常都可能敏感；安全`ToString()`/日志只能输出类型、phase、kind、status或计数。

## 产品可达性与下一纵切

- coordinator、recovery-before-scanner、scanner冻结/negative-cleanup保护、selection最终authoritative重读及directory-only rename都已由production消费；rename仍是internal surface，不是玩家可见删改能力。
- 下一纵切为staged import：必须直接复用现有staged source、target slot、coordinator、journal/recovery及publication plan，先冻结来源所有权、copy/move、冲突与one-shot record publication语义。delete、external、reload与所有UI继续冻结。
- operation/recovery状态只能脱敏输出；若继续增加没有当前或紧随纵切production消费者的抽象，应视为过度工程风险。
