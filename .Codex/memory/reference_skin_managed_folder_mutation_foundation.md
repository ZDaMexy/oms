---
name: reference_skin_managed_folder_mutation_foundation
description: chartskin rename/staged import/delete/ManagedCopy、v3 exact-set recovery与NTFS handoff边界
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

- 既有rename/delete候选每次按ID从Realm刷新重读：必须是唯一合法`chartskin/<direct-child>`、`Files`空、非external/protected/fixed-ID/DeletePending、exact scanner owner、allowlisted实例类型且revision非空。current v3 admission会在同一coordinator lease下捕获有界exact external declaration set及全部held physical proofs；合法非重叠external不再全局阻断，foreign/null owner、集合超限、overlap或proof/generation drift仍fail-closed。
- Windows mutation session从物理本地卷逐段no-follow固定data root与held `chartskin` root。既有source只从该根捕获direct-child identity并持有带DELETE权限、拒绝外部write/delete的handle；target只是一枚同held root绑定、经NFC/Windows命名与case-insensitive collision/absence验证的name slot，绝不能预造physical identity。
- fixed staged source不接受调用方path/token，只能来自data root下`skin-mutation-staging/{operationId:N}`。current manager-owned Import Managed Copy只接external record ID和用户明确target child，由fresh external capture成对产出exact capsule与含显式empty directory的bounded immutable logical-tree manifest；文件bytes只从capsule读取，destination handles按manifest no-follow/no-replace重建，绝不重开external path。
- **创建provisional root/写入首个byte发生在既有StagedImport Prepared之前**，因此current writer使用single canonical v3 combined intent：首写前绑定external exact record fingerprint、capsule revision、held staging root、operation-derived source/target与logical manifest，phase覆盖Copying/ProvisionalReady及既有move/publish。partial copy只清理exact durable owned subset，foreign/drift冻结；禁止第二journal、目录年龄/operation-like name或启动清空staging猜测回收。staging root与managed root都是既存held authority root，不能由import临时创建或替换；两者与source必须同volume并全程复验identity和canonical name。
- staged import的immutable publication plan固定`ID = operationId`并绑定target slot、managed-root identity与version。plan**不是Realm写权限**，ordinary startup scanner不得消费；production one-shot publisher只在durable `FilesystemApplied`、exact target recapture/fingerprint和最终Realm ID/path/owner冲突复核后执行。
- C1收窄“存在external就全局阻断”后，v3 Rename/StagedImport/Delete/ManagedCopy必须在首个物理步骤前durable保存external-registry generation/集合digest与non-overlap disposition，并在fresh recovery重取当前service-owned集合与held physical proof；external register/unregister与proof→final collision共享同一线性化边界。pre-C1 v2按production尚无service-owned external的历史合同冻结读取，不能静默补写；v3缺disposition/digest须Invalid/freeze。disposition不新增external绝对path/raw external record ID/raw physical identity；canonical recovery必需的manager operation ID/managed fingerprint仍可按schema持久化，UI/诊断一律脱敏。
- held session在生成或持久化Prepared journal前都会重新验证native inventory/authority links、external exact set及Realm资格；owner/hash/DeletePending/target collision等post-open漂移一律拒绝。rename、staged import、managed delete与ManagedCopy各有唯一专用physical/Realm消费者；Folder Skin Workspace现通过record-ID manager surface提供Open/Rename/Delete/Import Managed Copy真实caller，旧通用folder mutation仍冻结。

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

- canonical journal使用稳定文件名`skin-managed-mutation-journal.json`；严格UTF-8、固定schema/类型、重复字段拒绝、SHA-256校验和、1 MiB上限。准入按`(version, kind, phase)`闭集验证：v1/v2只接受其历史Rename/StagedImport/Delete phase图；即使在v3，`Copying`/`ProvisionalReady`也只允许ManagedCopy，Rename/StagedImport/Delete一律Invalid/freeze。current v3新intent只能从Prepared开始，Rename/StagedImport/Delete沿既有phase图，ManagedCopy显式加入Copying/ProvisionalReady后再进入FilesystemApplied/RealmApplied/Committed；terminal不可重写，A intent不能覆盖B intent，delete Prepared只允许fallback disposition单调补写。v3同时绑定external generation/digest/non-overlap disposition，缺失或不匹配即Invalid/freeze。v1/v2按各自frozen schema dispatch与恢复，绝不注入v3 optional字段或创建新旧version intent。
- 写入使用同目录临时文件、write-through、`Flush(true)`与Windows `MoveFileEx(REPLACE_EXISTING|WRITE_THROUGH)`。精确孤儿temp可判定为未发布并清理；canonical目录/reparse、锁定/ACL/IO或未知journal-like sibling不得伪装成Missing。
- mutation session只能经绑定的canonical store持久化，落盘后必须精确reload才返回durable receipt。receipt绑定session、store和exact Prepared journal，消费前后都复验；未解决session dispose或持久化结果不确定会粘性冻结关联路径。首个物理步骤前logical/native authority漂移若canonical receipt仍exact，可把Prepared compare-write为RolledBack、精确删除并确认Missing；receipt或写入结果已漂移时不能假装安全abort，须冻结。
- recovery按journal kind路由到rename、staged-import、managed-delete或ManagedCopy production handler，并复用同一coordinator/store/receipt。v3 recovery从取得mutation lease开始持有native与external snapshot到terminal compare-delete，每个Realm事务复核exact declaration set；forward即使已见终态证据也逐一write + exact reload缺失phase。ManagedCopy Copying只有exact empty durable root可rollback，完整capsule/manifest/content exact才forward，非空不完整保持Ambiguous。有效但无handler的nonterminal journal继续冻结；invalid/unknown/IO不能安全导出路径时冻结整个managed namespace。
- current production writer/schema为v3。Delete Prepared固定operation-derived `.oms-delete-{operationId:N}` tombstone、existing-record fingerprint、source-node manifest与fallback disposition；ManagedCopy增加exact external binding与durable logical manifest。v2 reader语义永久冻结，不追加optional字段；证据完整的v2 Rename/StagedImport/Delete继续按旧合同恢复，legacy-v1/旧v2缺证据仍strict Invalid且不猜测迁移。
- terminal journal必须先在held native/external authority及exact Realm declaration仍成立时执行compare-delete，删除后只允许再次读到`Missing`才解除冻结；仍见同一terminal则幂等重试，foreign/invalid/IO均保持冻结。不能先把journal删掉再补authority/Realm验证，也不能把“删后读取异常”解释为Missing或成功；一次歧义后journal突然Missing同样不能被当成成功。

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

- current managed delete先取得并全程持有mutation reservation与held exact source/content authority，再在C2 publication transaction中fresh重取current selection/owner/revision/generation并证明exact一致；随后在update thread发布protected fallback revision并等待旧`ConsumersDetached`。participant/source/split/fallback失败或该边界前取消都不得创建journal、移动/删除目录，且须保留或恢复exact A并释放reservation/session。
- 迁移期fallback必须与`OmsSkin.CreateInfo()`逐字段一致：exact ID/Name/Creator/InstantiationInfo、空Hash、protected、非DeletePending、无folder/files/external/owner，并要求`CurrentSkinInfo`、`CurrentSkin`和`CurrentRevision`在同一barrier指向exact OMS authority。C7 canonical接管后才替换为只读`oms-simple.osk`。
- fallback+detach成功后才在既有reservation/session内创建C1 Prepared journal并durable保存`ProtectedPairCommitted`；首个physical步骤前继续fresh复核fallback current revision与held target authority。此后由journal/tombstone/Realm/recovery收口，首个physical步骤后的uncertain outcome只保证保持fallback和durable intent，不承诺恢复A。
- current delete测试与诊断必须把fallback publication、old consumers detach、physical/Realm completion分成三个独立等待门；一个覆盖全流程的总timeout会掩盖究竟是participant未detach还是C1 mutation未完成，也容易让cleanup在live provider仍挂载时误用legacy同步Delete。
- `NotRequired`只允许current三元authority coherent且都不是删除目标；任何split不得放行。fallback无效、selection disabled、revision/receipt漂移或提交异常均拒绝或冻结；recovery不得凭physical terminal猜成成功。

## managed delete真实行为与恢复

- 既有current settings按钮与Folder Skin Workspace managed row都只把committed record ID交给fresh-authoritative`CanDelete(Guid)`和manager-owned`DeleteSkinAsync(Guid)`；共享确认框只持detached ID与immutable label，不构造/选择noncurrent `Skin`，也不成为第二authority。确认后的operation fresh决定coherent noncurrent=`NotRequired`、current=C2 fallback transaction、split=拒绝，不能沿用dialog打开时的pair快照。ordinary current `.osk`也先fallback publication+old detach，再做Realm soft-delete；Realm失败恢复exact旧pair/revision、record/blob。旧通用folder `CanModify/Delete`、protected/fixed、external、foreign/null owner、非法path继续fail-closed。
- Prepared绑定held managed-root/source、operation-derived tombstone、exact existing-record fingerprint与bounded exact source-node manifest，再单调持久化fallback disposition。完整树以显式迭代walker受capsule depth/entry/path及pending-handle预算约束。final no-follow tree/authority/identity复核及caller取消检查后，首个外部步骤只能是held-root-relative source→tombstone no-replace detach；之后不再观察caller cancellation。rename后的verification handles没有DELETE权，须验证后释放，再从held root以fresh no-follow delete-exclusive handles（持有DELETE、只共享READ）重捕；same-session live tree仍须与manifest精确相等，release→exclusive重捕窄窗内完成的node移出因此在0次disposition时拒绝，只有fresh recovery session的partial survivor可接受durable子集。
- exclusive tree取得后再把已持有root/child移到sibling或authority外由sharing violation阻断，但目录handle不封namespace。preflight前可见的foreign addition/replacement及reparse、hardlink、duplicate/metadata/inventory drift、source replacement或同级collision仍在0次disposition时拒绝。final preflight后竞态新增/replacement绝不进入held delete list或被删除，可能在manifest节点部分清理后令root删除失败；此时保留FilesystemApplied journal与Realm record并冻结。始终不得触及foreign、managed root、sibling或caller path。
- Realm只在durable FilesystemApplied后compare-remove journal绑定的exact record。recovery按source/tombstone/manifest/Realm fingerprint/disposition逐phase前滚或安全回滚；raw disposition却出现TargetOnly/Neither、Both、identity mismatch、foreign/conflicting record、缺证据或歧义都冻结。`ProtectedPairCommitted`在`FilesystemApplied/RealmApplied + source absent + tombstone absent + Realm absent`时仍须有exact protected fallback Realm record；`NotRequired`不要求该record。重启恢复不声称能重验detach前的旧runtime pair。
- fallback completion经update scheduler时由callback或shutdown恰一方claim/reap；先完成worker等待的TCS，再发布可能重入的`SourceChanged`。late callback no-op，update thread不等待，Realm释放前必须cancel/join delete、reload、materializer/work fence及其它全部worker。outer成功诊断清理须由request generation守卫，不能覆盖observer重入后的较新拒绝reason。

## 不可误推

- rename、fixed-source staged import、managed delete与full ManagedCopy由Folder Skin Workspace/manager surface组成已关闭的C1产品链；C2 current revision/mutation也已签发，但仍不表示G1最终整包门、`SV1-2`整体、Skin V1或release已交付。所有旧通用rename/import/delete入口继续冻结。
- rename不联动展示名/`skin.ini`；staged import只move受控provisional副本、不会修改包字节或自动选择；managed delete只适用于eligible managed direct-child并会物理删除，不得类推为external删除、任意path cleanup或通用Realm hard-delete。
- journal、identity、relative path、operation/record ID与native异常都可能敏感；安全`ToString()`/日志只能输出类型、phase、kind、status或计数。

## 产品可达性与下一纵切

- coordinator、recovery-before-scanner、scanner冻结/negative-cleanup保护和selection最终authoritative重读已由启动发现/选择、Workspace Rename/Delete/ManagedCopy及recovery链消费；external register/unregister与全部managed mutation共享exact-set线性化。
- `OsuGame.Dispose`必须在Realm释放前统一cancel + synchronous join startup scanner、rename、staged-import、managed delete、selection/reload capture/retry、materializer/work fence与retire worker；queued selection/reload/delete completion必须在shutdown被reap或晚到no-op，不得新建脱离该边界的后台链。
- operation/recovery状态只能脱敏输出；若继续增加没有当前或紧随纵切production消费者的抽象，应视为过度工程风险。每一新切片都要明确它连接的真实caller/host/renderer，不能用production项目中的internal类型数量代替产品进度。
- 当前go/no-go：C1已关闭external Workspace、exact-set mutation与full ManagedCopy；C2 current external/managed/ordinary mutation和atomic reload/detach也已签发，C3 package+layout pair不改变这些物理边界，燃尽为`3/7 closed，C4 active`。旧通用`Delete/CanModify`及thin/arbitrary-path stager继续冻结；不能把held-authority/capsule+manifest/single-v3纵切退化成普通递归copy。
