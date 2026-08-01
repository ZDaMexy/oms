---
name: reference_skin_managed_folder_mutation_foundation
description: chartskin mutation foundation、directory-only rename、fixed-source staged import、durable recovery与NTFS handoff边界
metadata:
  node_type: memory
  type: reference
---

# managed chartskin mutation foundation 地雷

> 快速召回 `SV1-2` 受管目录mutation的公共安全地基。实时能力与下一操作门只看 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md) / [PLAN](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md)；前置链见[[reference_skin_managed_folder_scanner]]与[[reference_skin_managed_folder_selection]]。

## 共享线性化

- scanner、selection、mutation与recovery共用一个`SkinManagedFolderOperationCoordinator`。holder分为普通短临界区、generic mutation reservation、staged-import reservation与startup sequence：普通短临界区及startup内的短临界区只允许同线程嵌套；两类mutation reservation不可重入且独占，session可跨线程dispose；startup/staged holder各自携带selection可等待的typed completion，generic holder永不携带。阻塞participant用可取消monitor wait，update-thread selection只做non-blocking admission；ownership取得/发布必须在同一锁内，不能留`SemaphoreSlim`已取得而holder尚不可见的空窗。
- scanner从native discovery开始一直持有共享边界到Realm reconcile提交结束，不能再把snapshot与Realm commit之间暴露给mutation。managed selection的最终authoritative Realm重读、冻结检查与`CurrentSkinInfo`/`CurrentSkin`发布也在同一边界内；提交时使用本Realm重新取得的`Live<SkinInfo>`，不发布调用方传入的陈旧live对象。
- `SkinManager`构造期先恢复再允许配置选择；`OsuGame`启动worker随后在同一个typed startup sequence外层lease内再次幂等恢复并立即scanner，二者之间没有mutation插入点。已经开始的configured managed preparation可等待该exact startup completion后fresh retry；新的manual managed请求仍遇占用即fail-closed。preparation的startup/generic mutation observation必须贯穿整个retry chain，generic epoch跨越即拒绝，并在retry short lease内再次复核；普通后台Realm请求可按既有latest-wins合同提交。

## mutation资格不是写能力

- 既有rename/delete候选每次按ID从Realm刷新重读：必须是唯一合法`chartskin/<direct-child>`、`Files`空、非external/protected/fixed-ID/DeletePending、exact scanner owner、allowlisted实例类型且revision非空；任何external filesystem声明尚无resolved identity，因此当前保守阻断全部managed mutation。
- Windows mutation session从物理本地卷逐段no-follow固定data root与held `chartskin` root。既有source只从该根捕获direct-child identity并持有带DELETE权限、拒绝外部write/delete的handle；target只是一枚同held root绑定、经NFC/Windows命名与case-insensitive collision/absence验证的name slot，绝不能预造physical identity。
- staged source不接受调用方path/token，只能来自data root下固定`skin-mutation-staging/{operationId:N}`；合同要求future upstream stager预先复制、保留外部原来源，并把副本交给OMS作为本operation独占持有的可丢弃provisional。当前仓库没有该production stager；未来必须独立闭合source authority、no-follow、budget、cancellation、cleanup与脱敏诊断，普通递归copy或任意caller path不能直接进入mutation。staging root与managed root都是既存held authority root，不能由import临时创建或替换；两者与source必须同volume并全程复验identity和canonical name。
- staged import的immutable publication plan固定`ID = operationId`并绑定target slot、managed-root identity与version。plan**不是Realm写权限**，ordinary startup scanner不得消费；production one-shot publisher只在durable `FilesystemApplied`、exact target recapture/fingerprint和最终Realm ID/path/owner冲突复核后执行。
- held session在生成或持久化Prepared journal前都会重新验证native inventory/authority links及Realm资格；owner/hash/DeletePending/target collision等post-open漂移一律拒绝。rename与staged import现各有唯一专用physical move及Realm消费者；managed delete仍没有写primitive或Realm record删除。

## directory-only rename真实行为

- 产品语义已冻结：只把一个`chartskin/<direct-child>`工作目录移动到同一held managed root下的空target name slot，并更新同一authoritative Realm record的`FilesystemStoragePath`。目录名是工作区存储身份；`skin.ini [General] Name`、Realm `Name`/`Creator`、包内容、revision/hash和scanner owner都是作者内容/既有身份，rename不得修改。
- 生产链直接消费既有`OpenRename`、held root/source、target slot、shared coordinator、canonical journal和exact durable receipt。首个物理可见步骤前必须durable `Prepared`，之后闭合`FilesystemApplied → RealmApplied → Committed`；可在物理步骤前安全取消时走`RolledBack`并exact-delete terminal journal。
- 成功rename不替换当前skin pair：active immutable capsule继续服务既有consumer；全局selection generation推进并取消当时的pending preparation，旧generation不得发布，未来重新选择从新path重新capture。scanner snapshot→Realm commit、selection final commit、rename/recovery共用同一coordinator，`OsuGame.Dispose`在Realm释放前cancel+join rename worker。

## fixed-source staged import真实行为

- staged source handle必须以`DELETE | SYNCHRONIZE`打开并允许share delete，且先从held staging root做完整no-follow package capture；根`skin.ini`、closed实例类型、capsule revision、reparse、hardlink/duplicate identity、busy writer及完整inventory全部通过后，才可进入physical phase。target只能是可信internal caller显式给出的held managed-root direct-child name-slot，NFC/Windows/case-insensitive physical与Realm ID/path冲突都在首个物理步骤前拒绝，不覆盖、merge或自动suffix。
- 首个move或provisional cleanup前必须durable `Prepared`并exact reload receipt；Prepared须绑定uppercase capsule content revision与lowercase完整physical-tree fingerprint。该树指纹覆盖capsule revision、空目录、每个节点identity/kind/length/creation/attributes/reparse/link/delete和exact ordinal层级边界，仅package root排除rename会推进的last-write/change time。Windows primitive以held staging parent/source和held managed root执行同卷identity-preserving no-replace move；move attempted后不再观察caller cancellation。target以`CancellationToken.None`完整no-follow重捕，source→target identity、content revision与physical-tree fingerprint必须和Prepared exact，之后才可推进`FilesystemApplied`。
- one-shot publisher只发布一条exact managed record：ID为operation ID，path为exact target path，`Name`/`Creator`/hash来自最终capture，实例类型为closed allowlist，`Files`空、非external/protected、`DeletePending=false`；只有完整通过scanner同等级注册/capsule/Realm冲突门后才可交接exact scanner owner，禁止直接写token或让scanner竞争发布。
- import不自动选择、不替换active immutable capsule，也不复用rename的全局pending取消；无关pending selection在最终authoritative复核仍成立时继续。成功仍闭合`FilesystemApplied → RealmApplied → Committed`并compare-delete确认Missing。

## NTFS descendant-handle边界

- 真实NTFS诊断证明：即使descendant handles均允许`FILE_SHARE_DELETE`，非空目录在这些handles仍打开时用`NtSetInformationFile(FileRenameInformationEx)`执行flags `0`或POSIX诊断flag `0x2`均返回`STATUS_ACCESS_DENIED (0xC0000022)`。
- 因此最终完整held-tree preflight和caller取消检查后只释放descendant handles，exact source directory与两侧authority-root identity继续持有；随后不再观察caller cancellation，立即做held-parent-relative no-replace move。move后用`CancellationToken.None`从target no-follow重捕完整树，核对source identity、descendant exact metadata、目录项name/identity/kind inventory，并重跑reparse/hardlink/duplicate/busy-writer gate后重新持有，才允许Realm publication。
- staged provisional source需要DELETE access；真实NTFS表明该访问可以合法推进父目录项相关metadata。因此held source node自身的pinned metadata仍必须exact，provisional parent inventory只比较name/identity/kind，不得把该合法时间推进误判为foreign drift。移动后target package root只在明确的recapture合同内允许rename-related root time前进；不能据此放宽descendant metadata、capsule revision或其它安全gate。
- 这些是可恢复的同卷identity-preserving目录项move，不是oplock/TxF/filesystem transaction，也不保证release→move→recapture窄窗口的字节级排他。不得误写成descendant handles跨move持有或原子内容快照；任何可观察差异都保留journal并冻结。

## durable journal与恢复

- canonical journal使用稳定文件名`skin-managed-mutation-journal.json`与payload version；严格UTF-8、固定schema/类型、重复字段拒绝、SHA-256校验和、128 KiB上限。新intent只能从Prepared开始，phase按显式图单调推进；terminal不可重写，A intent不能覆盖B intent。current staged forward只允许`Prepared → FilesystemApplied → RealmApplied → Committed`，不能直跳terminal；RolledBack的target identity/publication fingerprint必须none/none或exact+valid成对。fixed skin ID不得作为staged operation/record ID，payload重算checksum也仍是invalid。legacy v1仍可按旧schema重写terminal并删除，重写不得混入v2字段而导致下一次strict load失败。
- 写入使用同目录临时文件、write-through、`Flush(true)`与Windows `MoveFileEx(REPLACE_EXISTING|WRITE_THROUGH)`。精确孤儿temp可判定为未发布并清理；canonical目录/reparse、锁定/ACL/IO或未知journal-like sibling不得伪装成Missing。
- mutation session只能经绑定的canonical store持久化，落盘后必须精确reload才返回durable receipt。receipt绑定session、store和exact Prepared journal，消费前后都复验；未解决session dispose或持久化结果不确定会粘性冻结关联路径。无外部mutation时可把Prepared写成RolledBack、精确删除并确认Missing后abort。
- recovery按journal kind路由到rename或staged-import production handler，并复用同一coordinator/store/receipt；managed delete仍unsupported/frozen。current forward恢复即使已看见终态证据，也必须逐一write + exact reload缺失的`FilesystemApplied → RealmApplied → Committed`，每阶段重新inspection；publisher/action绝不早于durable FilesystemApplied，phase fault只保留最后durable journal，fresh restart继续且不重复publication。有效但无handler的nonterminal journal继续保留并精确冻结source/target，scanner对这些路径连negative cleanup都禁止；invalid/unknown/IO无法安全导出路径时冻结整个managed namespace。handler inspection/action必须回报与journal相同的held authority-root identity，否则保持歧义。
- terminal journal只在compare-delete后再次确认Missing才解除冻结；仍见同一terminal则幂等重试。一次歧义后journal突然Missing不能被当成成功；必须保持冻结到新启动/可证明恢复路径。

## rename恢复矩阵

- `SourceOnly + Realm source`：已回滚；`SourceOnly + Realm target`：只把同记录path回滚到source。
- `TargetOnly + Realm source`：只把同记录path前滚到target；`TargetOnly + Realm target`：已提交。
- `Both`、`Neither`、`IdentityMismatch`、Realm在source/target外、record/root资格或identity不可信：一律歧义，保留journal、冻结source/target并继续禁止scanner negative cleanup。恢复不得发布新record或改展示/内容元数据。

## staged-import恢复矩阵

- `TargetExact + Realm absent`：前滚one-shot publication；`TargetExact + exact planned record`：已提交。
- `SourceExact + TargetAbsent + Realm absent/exact planned`：只删除exact planned record（若存在）、清理exact provisional source并回滚。每个可判定inspection返回前重新枚举source/target双槽；cleanup中途崩溃时可凭durable staging-root/source identity与树证明幂等续删同一provisional root，不得因capsule已无法完整重捕而触及外部原来源、任何managed target或foreign node。
- `Neither + Realm absent/exact planned`：只有journal operation仍能证明source是可丢弃OMS provisional副本且外部原来源已保留时，才可按同一exact-record边界回滚；否则保持歧义。
- `Both`、staging/managed root或source/target identity mismatch、同ID/同path冲突、foreign record、字段漂移或其它conflicting Realm state：保留journal，冻结target与相关operation状态。不得删除foreign record、不明physical node或managed target。

## current delete fallback

- 实际物理delete仍不存在。当前foundation只在update thread、同一mutation reservation与exact durable receipt下确认fallback pair。
- 迁移期唯一允许的fallback是Realm中受保护、非DeletePending、无folder/files声明且实例信息匹配的程序化`OmsSkin`，并要求`CurrentSkinInfo`和`CurrentSkin`最终同时指向exact OMS record/type。canonical接管后这条policy才可替换为只读`oms-simple.osk`。
- `NotRequired`只允许两半ID一致且都不是删除目标；任何split-brain不得放行。fallback无效、selection disabled、提交异常、pair未确认、authority漂移或receipt失效都拒绝未来delete，并在没有发生外部mutation时abort Prepared journal。

## 不可误推

- rename与fixed-source staged import internal production纵切已经实现，但没有非测试caller、stager或UI，也不表示G1、`SV1-2`、Skin V1或reload已交付。managed delete、external与atomic reload/detach仍须分别闭合，所有通用rename/import/delete入口继续冻结。
- rename不联动展示名/`skin.ini`；staged import只move受控provisional副本、不会修改包字节或自动选择；delete confirmation仍不执行任何Realm/磁盘删除。
- journal、identity、relative path、operation/record ID与native异常都可能敏感；安全`ToString()`/日志只能输出类型、phase、kind、status或计数。

## 产品可达性与下一纵切

- coordinator、recovery-before-scanner、scanner冻结/negative-cleanup保护和selection最终authoritative重读已由玩家可达的启动发现/选择链消费；directory-only rename及fixed-source staged import只在production assembly内部被operation/recovery组装，没有应用caller，不是玩家可见删改能力。
- `OsuGame.Dispose`必须在Realm释放前统一cancel + synchronous join startup scanner、rename、staged-import与selection retry worker；queued selection completion必须在shutdown被reap或晚到no-op，不得新建脱离该边界的后台链。
- operation/recovery状态只能脱敏输出；若继续增加没有当前或紧随纵切production消费者的抽象，应视为过度工程风险。每一新切片都要明确它连接的真实caller/host/renderer，不能用production项目中的internal类型数量代替产品进度。
- 当前go/no-go：managed delete有settings delete dialog/caller雏形及protected fallback foundation，下一切conditional GO，但必须新建独立`CanDelete`/async caller并同切闭合held-root物理删除、Realm收敛、journal recovery、selection/shutdown与隐私；不得解冻旧通用`Delete`直连。thin staged-import stager/caller当前NO-GO，external source→fixed provisional的可信authority/no-follow复制、预算、取消、清理、诊断与真实caller未一起冻结前，不能把任意path或普通递归copy包装成“thin”产品入口。
