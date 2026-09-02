# P1-A 变动日志

> 本文件只记录 `P1-A` 子线已确认、已验证或已完成挂接的变更摘要。

## 2026-09-02

### C4 shared codec / public catalog / resolved material闭门

- core冻结`oms-gameplay-skin-catalog.v1`：Common v1与唯一BMS v1 extension共28项stable ID，完整声明scope、value/resource type、Required/Recommended/Optional、inherit、Suppress资格、ruleset/keymode/stage/lane-role适用性与稳定diagnostic；author catalog、runtime capability和Suppress eligibility分层。`GameplaySkinDocumentCodec`成为唯一tokenizer，每个package的exact`skin.ini` bytes只capture/hash/tokenize一次，public与legacy adapter共享同一防御性immutable document并保留Absent/DeclaredEmpty/Invalid/Valid/Suppress、duplicate/escaping/case/comment/unknown version/field及canonical round-trip合同。
- `Provide/Inherit/Suppress`改为显式模型，唯一resolver按exact ruleset/keymode/stage/scope specificity及legacy beatmap direct compatibility→selected public→selected legacy→ruleset resource→canonical/protected→programmatic authority输出一个immutable material set；Required/Recommended非法Suppress与invalid/empty不再冒充Absent或跨来源拼件。diagnostic只在successful commit后异步去重输出稳定脱敏摘要，路径、作者值、display name、record ID/hash与exception正文均不进入持久文本。
- BMS与mania真实consumer已迁入同一package+layout+material publication：ordinary Realm`.osk`、managed与registered external三源public声明经真实`SkinManager` current revision、ruleset prepare驱动actual BMS Note/LN与mania Note/Hold/KeyVisual；renderer只读prepared material，不在commit后重开资源或重跑resolver。BMS legacy候选production顺序固定为5K `[Bms]→Keys6→Keys5`、7K `→Keys8→Keys7`、9K `→Keys9`且不重复、14K `→Keys16→同一Keys8 bucket的两个stage-local deck→Keys14`；9K raw`0..8`/canonical`1..9`只走`bms-gameplay-skin-nine-key-index.v1`，Mirror/Random继续只改变mod后LaneId。
- C2+C3 revision协议扩展为exact triple：全部codec/catalog/resource/material失败止于background prepare，update thread只交换prepared immutable引用；prepare前后与commit复核participant generation、current selection、exact source/content/package/layout及contract version。fresh attach、commit前detach、late attach、跨revision holder、latest-wins/reentrant/cancel/scheduler fault/shutdown与最后lease detach exactly-once retire继续成立，失败保留exact A画面。BMS static与固定60FPS作者合同、P1-K末端lane keysound及C3 geometry/aspect/DPI/safe-area均保持。
- 最终Major生命周期审计补齐BMS exact preparation所有权：`BmsLegacySkin`按exact layout generation维护waiter/borrower计数，`BmsGameplayResolvedNoteMaterialPreparer`只把完成的Note/LN revision以幂等borrow交给`GameplaySkinLayoutPublication`。publication验证/prepare失败、取消、dispatcher拒绝或commit admission失败均exactly-once释放provisional borrow；成功commit原子转交`GameplaySkinLayoutRevisionOwner`，并由`RulesetSkinProvidingContainer`在renderer子树完成detach/dispose后释放。`BmsLegacySkin.Dispose`会取消、join并标记generation退役，但只在waiter/borrower同时归零后清理prepared revision，不能使active画面引用已释放texture store。
- 最终并发终审又关闭了prepared carrier返回到caller ownership之间的取消窄窗：shared owner提供cancellation-aware `PreparePublication`，carrier一旦取得fresh work lease与publication retirement，任何随后可见的取消都先Dispose再逸出；BMS与mania production caller都使用该入口，并在`using`内、`TryCommit`前再次复核host token。core确定性fixture在carrier创建后取消并证明retirement/work lease各exactly-once释放，BMS fixture进一步证明真实managed Note/LN preparation/borrow归零且current revision不变；取消后的mania也不再可能提交exact material。
- beatmap-local public authoring终态为C4排除：仓库没有安全sidecar、producer/importer、`WorkingBeatmap` public document/revision authoring ownership或C1/C2 lifecycle，因此public source/candidate不可达；真实BMS importer→manager→`WorkingBeatmap.Skin`仍惰性返回同一只读`LegacyBeatmapSkin`实例，只保留更高优先级direct visual compatibility，其public section不进入author resolver。作者替代流程是ordinary`.osk`、managed`chartskin/<包>/`或registered external包。已删除被catalog/exact material取代的lane-colour、legacy mania bucket及BMS bucket colour/geometry/declaration factory；BMS lane-resource/configuration candidate与9K mapping接入production，event cursor与通用capability negotiator分别留给C5/C6且不计C4。
- 最终验证：core focused **141/141**、core Skin **1110/1116**且六项失败名称/消息逐字符匹配精确既有基线；mania C4 relevant **172/172**、Skin **193/193**、full **838/842**且full四项HoldNote失败同样逐字符匹配既有基线；BMS C4 relevant/current-revision/managed-candidate **315/315、197/197、115/115**，BMS Skin/full **726/726、1687/1687**且无hang artifact；P1-K/projection/真实发声 **102/102、24/24、14/14 + 2/2**。formatter后重新build的core/BMS/mania C4 production focused仍为 **141/141、315/315、172/172**。Release **0 error / 20 emitted known warnings**，全部为既有MessagePack advisory重复输出与既有BMS test `CS8600`/`CA2007`；六工程97个C#文件的默认targeted formatter、文档门与diff检查通过。四类独立终审均GO，blocker/major **0/0**。
- C4燃尽只推进为 **`4/7 closed，C5 active`**，不换算线性百分比。C5只处理declarative scene/animation/event与剩余optional slot production；C6 sandbox与最终整包reload、C7 canonical双包/Authoring Kit/程序化`OmsSkin`退出、P1-L BGA内容/timeline/seek及在线服务均未完成。完整边界见[C4完成交接](../../other/SKIN_SYSTEM_C4_CODEC_MATERIAL_COMPLETION_HANDOFF_20260831.md)。

## 2026-08-30

### C3 P1-K前置与唯一gameplay layout闭门

- P1-K Skin前置已从真实decode source闭合：decoder/parser与converter是keymode、lane count和keysound timeline的唯一truth；explicit override优先级、`.pms/.bme` extension与P2/high-channel冲突、sparse 5K/7K/9K证据不足均走明确authority或fail-closed，并只输出稳定脱敏source/evidence/reason token。`BmsBeatmapConverter.buildLaneKeysoundTimelines()`及相邻mine/armed路径统一以`GetLaneCount()`为上界，覆盖5K/7K最右键、9K全lane、14K右deck末键与Scratch2的visible、LN head/tail armed、invisible及mine/armed timeline；layout/skin/runtime不再从最高出现channel、layout宽度、enum位置或总lane数重读/猜测keymode。真实player与autoplay均经同一`BmsKeysoundStore`发声，Mirror/S-RANDOM只改变mod后对象目标LaneId，不改变固定playfield topology或另造keysound store；未改sample pool、判定或binding。
- 最终产品价值审计收紧override表述：`BmsBeatmapDecoderOptions.KeymodeOverride`是供authoritative host/importer调用的production correction seam，普通`ICustomBeatmapLoader`仍传`null`，当前没有终端用户纠正UI；因此模糊sparse `.bms/.bml`会稳定拒绝，不能把seam冒充已交付用户功能。该缺口登记回P1-K后续产品可用性，不影响C3“parser唯一truth、无证据不猜”的安全闭门。审计同时删除了零caller的`BmsGameplayLayoutProvider.PrepareExact()` convenience wrapper；真实产品入口继续唯一使用`TryPrepareExact()`。
- C3交付唯一ruleset-neutral immutable `GameplaySkinLayoutContext`、neutral snapshot/publication与revision owner，BMS仅有一个solver，mania仅有一个adapter。publication绑定exact native context/keymode、topology、presentation style、safe bounds/aspect/DPI、package/current/content/topology/layout revision并防御性复制；neutral snapshot与ruleset typed adapter是同一publication中的单一引用。stable GroupId/LaneId跨style、视觉重排、geometry和topology-preserving revision不变；logical/visual、global/group-local index显式携带。矩阵覆盖5K/7K P1/P2/CenterP1/CenterP2、9K BMS/PMS、14K双deck/S1/S2/centre gap及mania真实single/dual stage vector，stage-local special-key语义保持。
- 全部production consumer已迁到owner current publication的同一exact snapshot：BMS playfield/stage/group/lane、Note/LN、barline、hit/judgement line/target、lane cover、pre-start preview、BGA最终viewport、gauge/combo/HUD，以及mania playfield/stage/column/note/hold/hit target/judgement与core ruleset/provider。既有profile/default profile/fixed rect/local offset、drawable尺寸二次求解和caller-injected compatibility carrier均已删除、证明为非layout数据或在exact production root稳定fail-closed；isolated visual compatibility必须显式标记owner。BGA只统一最终viewport/rect，其内容、timeline、seek、POOR/gimmick播放继续归P1-L。
- geometry逐字段验证finite、正值、合法range、安全screen bounds与non-overlap；单字段非法只应用确定程序化fallback并产生稳定脱敏diagnostic，始终一次构造完整snapshot，禁止NaN/Infinity/负尺寸、部分新旧pair或consumer自建geometry。14K双field/scratch/centre gap、BGA/HUD及常见/极窄/极宽aspect、DPI和safe-area均进入真实renderer矩阵。
- C2协议扩展为package+layout同一publication：全部可失败geometry解析/求解/资源准备止于background prepare，update thread只做prepared immutable单引用交换；participant generation、current selection、exact source/content/layout revision在prepare前后及commit同锁复核。prepare中attach强制fresh barrier，commit前detach安全移出，late attach只取committed revision/lease；latest-wins、reentrant、cancel、scheduler fault、shutdown、callback claim/join、跨revision holder与最后lease detach exactly-once retire均以CAS终态和owner fence收口，任一失败保留exact A package+layout pair。三源same-ID A→B、动态attach/detach、failure保A与current external/managed mutation失败原子性继续通过；live gameplay/preview仍在source prepare前拒绝，不新增watcher或live reload。
- 最终owner审计以新增真实owner红测闭合cached descendant可直接二次发布的窗口：shared `GameplaySkinLayoutRevisionOwner`在同一锁内对已有exact publication实行one-shot guard，旧production路径先红、修正后green；mania stage vector/topology/environment移入fresh work lease与participant-generation后的solve callback，BMS managed入口也在读取config/skin/求解前拒绝compatibility token。detached compatibility仍只服务显式solver/visual fixture，exact production branch继续以current snapshot与package token reference-equal fail-closed。
- 最终自动证据：P1-K decode/converter **176/176**；mania projection **24/24**；BMS真实keysound **14/14**；converted mania shared store **2/2**；BMS relevant **316/316**；mania C3 **27/27**；core final-audit **48/48**（此前focused **56/56**）；终审硬化后mania **51/51**、BMS **37/37**；product concurrency **17/17**；storyboard **7/7**。core Skin **1164/1170**，六项精确既有失败仍为四项removed-Osu `TestSceneBeatmapSkinResources`、default background cycling与Argon sample；mania Skin **209/209**；mania full **854/858**，精确既有四项均为`AutoGeneration` fixture；BMS Skin **802/802**；BMS full **1763/1763**且无hang。最终Release **0 error / 9 warnings**；P1-K authority、唯一geometry、全consumer/reachable bypass、revision participant/owner及并发终审为blocker/major/moderate/minor **0/0/0/0**。
- targeted formatter曾把一个带`[Cached]`的显式field改写为field-attributed auto-property，而当前DI source generator不支持该形态；已恢复显式backing field，并重新build、重跑focused/full与Release。该事件是已修复的formatter/source-generator适配问题，不是产品runtime问题。
- C3因此闭合，燃尽推进为 **`3/7 closed，C4 active`**。C4只推进shared codec/public catalog/`Provide/Inherit/Suppress` resolver、mania parity及beatmap-local作者格式终态；scene/animation/event、剩余optional slot、sandbox/script VM、canonical双包与Authoring Kit继续分别留在C5～C7，最终ini/manifest/scene/script/素材整包门只在C6关闭。不得提前删除程序化`OmsSkin`、开放watcher/live gameplay reload或扩张P1-L的BGA内容authority。

## 2026-08-24

### C2 current revision publication / reload / detach 闭门

- 真实Folder Skin Workspace、legacy author-preview与Settings host比较后，唯一触发冻结为Settings → Skin的`Reload current skin`：覆盖ordinary Realm`.osk`、managed、external三种current source；Workspace无第二Reload，same-value selection继续no-op，scanner继续只做启动reconcile且不实现watcher。`RulesetSkinProvidingContainer`/`PlayerLoader`将gameplay与preview标成`LiveGameplayHost`，manager在任何source prepare前确定拒绝并给出退出后重试反馈，绝不先改变active pair。
- `SkinCurrentRevision`、background prepare、update-thread reversible barrier、participant registry、manager/participant/work/operation lease、detach fence与retire queue已接入真实Settings→manager→BMS/mania/core/shell owner链。完整inventory区分coherent visual consumer、跨fade/sample/materializer/callback的lease-only holder及已证明不持current owner的wrapper/UI/独立authority；dynamic attach/detach、late attach、latest-wins、reentrant/cancel/scheduler/shutdown与最后lease detach exactly-once retire进入统一合同。base source invalidation在event栈内同步取消旧work，再按generation调度fresh rebuild；participant shutdown先进入terminal并取消pending/Ready admission，再由真实owner hook回收work。BMS/Skinnable在work admission gate内推进generation并exact claim pending owner/CTS，prepare install与finish publish比较captured generation，shutdown/dispose沿同一gate claim，因此不保留CTS已dispose异常窄窗。成功诊断清理按request generation守卫，outer成功不能覆盖observer推进generation后的新拒绝reason。
- current external Unregister、current managed Delete与ordinary current`.osk` Delete均先发布protected fallback并等待old detach；external随后fresh compare exact service-owner/record/current revision并pure-Realm remove，source零I/O；managed随后才进入C1 journal/physical boundary，首个physical后的uncertain failure由durable recovery保持fallback而不承诺恢复A；ordinary随后Realm soft-delete，失败恢复旧pair/revision、record/blob。legacy Skin Editor、external-edit、update-import和direct current mutation旁路已连UI/backend稳定禁用或纳入统一admission。
- 最终宽验证为core focused **204/204**、`FullyQualifiedName~PendingAsyncDrawableOwnership` visual/host **11/11**、core canonical `~Skin` **1137/1143**、mania `~Skin` **182/182**、BMS `~Skin` **796/796（8m53s）**与BMS full **1670/1670（10m09s）**；full使用`--blame-hang 5m`且明确全数完成、未生成hang sequence。core六项失败与既有精确基线完全相同：四项`TestSceneBeatmapSkinResources`依赖removed-Osu fixture、`TestBackgroundCyclingOnDefaultSkin(True)`及Argon `TestSampleUpdatedBeforePlaybackWhenNotPresent`。另有完整真实C2产品路径集 **314/314**、final drift + half-loaded + Ready sentinel **6/6**。Release含restore首跑 **0 error / 20 known warnings（41.88s）**，formatter后`--no-restore`复验 **0 error / 11 known warnings（36.58s）**；分别为18/9次MessagePack `NU1902`输出加BMS tests既有`CS8600`/`CA2007`。core、core-tests、BMS、BMS-tests、mania-tests owning-project targeted formatter均exit 0，仅保留`IDE1006`不可自动修复提示。participant/holder、reachable bypass、concurrency/owner及tests/product-contract四项独立终审均为blocker/major/moderate **0/0/0**。
- 最终文档门为`CheckDocumentation.ps1` **137 Markdown / 1071 relative links / 80 memory wiki links**通过，仅有两份PLAN的预期数字比值提醒；`git diff --check -- doc_md .Codex/memory`通过。C2因此闭合；权威燃尽推进为 **`2/7 closed，C3 active`**。C2完成事实与最终验证由[当前状态](DEVELOPMENT_STATUS.md)及本条历史保存，participant/holder/bypass inventory与冻结合同见[技术约束](TECHNICAL_CONSTRAINTS.md)，C3工作门由[当前计划](DEVELOPMENT_PLAN.md)维护。

## 2026-08-13

### C1作者文件工作区与archive安全闭门

- Folder Skin Workspace、external strict read-only registration/capture/select/configured restart/Open/pure-Realm noncurrent Unregister、managed Open/Rename/Delete、manager-owned full ManagedCopy与dynamic redacted journal support已形成真实settings→manager→BMS/mania renderer链。external selection不在慢capture期持有coordinator，但持有managed authority/full registry snapshot/target package session到final；final callback fresh复核generation/generic epoch/current pair、target/full-set declaration与physical proof，再compare transaction并发布fresh Name/Creator/Hash。latest distinct request可越过被阻塞的旧capture，旧请求不发布；shutdown取消/join释放全部handles。
- journal/recovery收紧为封闭`(version, kind, phase)`白名单，ManagedCopy的Copying/ProvisionalReady不能被legacy kind/version接受。terminal journal仅在exact Realm/held authority下compare-delete，删除后fresh inspection只确认Missing，消除Missing+freeze旁路。Rename/StagedImport/Delete/ManagedCopy的exact external declaration/proof持有至final Realm线性化，v1/v2 schema保持strict frozen。
- ordinary `.osk` 使用bounded `SkinArchiveReader`：自解析EOCD/central directory、封闭InstantiationInfo、实际CRC/length/ratio/aggregate与cancellation完成后才允许model publication。`RealmArchiveModelImporter`/`SkinImporter`将transactional import scope的token贯穿hash/copy/metadata/Add/Replace/final commit。`RealmFileStore` 按exact same-hash participant group并发，多组rollback故障隔离且generation-safe可重试；Realm record与blob baseline独立判定ownership，覆盖双向非对称baseline而不删共享asset。
- 最终独立终审另闭合两处真实生命周期/漂移窗口：Workspace records与journal support只读worker现由manager统一跟踪，shutdown封门后cancel并同步join，UI关闭只取消自身刷新；managed Open在初始与held-capture后的final Realm视图都重新证明normalized path唯一，旧row遇同路径重复声明不调用host。
- 最终验证：Debug build **0 error**/仅9个既有MessagePack `NU1902`；core C1 focused **490/490**，archive/receipt **84/84**，BMS产品组合 **118/118**，mania Skin **182/182**，BMS full **1586/1586**。core Skin **679/683**，4项均为已移除Osu ruleset mode 0 fixture基线；Release **0 error**/仅9个既有MessagePack `NU1902`。external与receipt最终独立复审均为blocker/major/moderate **0/0/0**。C1因此关闭首项并转入C2；G1/`SV1-2`/Skin V1/release与`V-001`～`V-004`仍未完成。

### C1产品价值与后续边界终验

- 只读产品复核从真实settings/import caller追到manager、selection、BMS/mania consumer与用户结果，确认C1主要交付没有无caller或无consumer冒充进度：作者可真实注册、选择、复制、打开、重命名、删除、解除注册并查看恢复支持，ordinary `.osk` 仍从拖入导入进入选择与renderer。Windows authority、journal/recovery和receipt的体量用于防止external源写入、错目标删改、partial copy与共享blob误删；它们是昂贵但直接保护玩家数据的安全价值，不是视觉完成度。
- 仓库中C1前已有的internal fixed-staging import surface仍没有独立非测试caller；StagedImport operation/handler不被ManagedCopy直接调用，但共同的fixed-slot authority/native move+inspection及journal/coordinator/recovery框架已复用，故不计作额外用户功能，也不能把全部共同底层判成死代码。进度口径固定为`1/7`硬campaign通过但不换算14%；数据/导入安全与作者工作区已过门，current revision、layout、shared codec/三态、scene/event、sandbox、canonical发行与视觉/release仍按C2～C7逐门闭合。
- 后续C2工作门当时已明确真实Reload触发与live gameplay允许/defer/reject决定、完整production holder inventory、prepare/commit线性化、same-record-ID三源红测、`ExternalEditOverlay`即时dispose旁路、current external失败原子性及真实caller→renderer→owner终审，禁止用watcher、DTO、单consumer或路线审计假完成。C2闭合后的稳定合同见[技术约束](TECHNICAL_CONSTRAINTS.md)。

## 2026-08-12

### C1意外中断后的最小checkpoint收口

- C1未提交工作树已形成真实Folder Skin Workspace：external行仅暴露Open Folder / Import Managed Copy / Unregister，exact scanner-owned managed行暴露Open Folder / Rename Folder / Delete，ordinary Realm `.osk`不进入列表；row/dialog只保留committed record ID与immutable label，manager在操作时fresh重读authority。current与noncurrent managed row delete、split/record/owner/path/hash/DeletePending/external-set漂移、取消/重入/shutdown及ordinary `.osk` soft-delete回归均已有真实settings/manager测试。
- external注册使用resolver-issued Windows请求与held no-follow physical ancestry/session，在预算内原子产出immutable capsule、logical manifest与physical proof；service-owned Realm记录进入显式dropdown/configured restart，但random/next/previous排除。noncurrent Unregister为pure-Realm compare-remove，source missing/drift仍可清理记录，current/split与unresolved journal稳定拒绝，源目录identity/inventory/bytes保持不变。
- v3 journal新增external registry generation/digest/disposition及ManagedCopy的Prepared/Copying/ProvisionalReady；pre-C1 v1/v2保持严格version dispatch和旧phase recovery。Rename/StagedImport/Delete/ManagedCopy均在held exact-set下准入并在final Realm线性化点复核，旧“任一external全局阻断”已原子收窄；legacy recovery无held authority时仍按旧empty-set fail-closed。support inspector只读，只有唯一安全forward/rollback才提供manager-owned retry，UI只显示稳定脱敏状态。
- manager-owned ManagedCopy只接external record ID与用户明确direct-child，operation ID/staging由manager生成；首个provisional root/byte前已durable exact reload single canonical v3 intent。文件bytes只来自同次capsule，目录/空目录来自paired manifest，destination用no-follow/no-replace handles重建；首写前取消可exact rollback，首写后由journal/recovery收口，foreign replacement/partial evidence保留journal并冻结。完整产品旅程已贯通注册→copy（不自动选择）→BMS/mania渲染→restart→Open→Rename→restart render→external Unregister→managed Delete，external physical proof/inventory/bytes前后相同。
- ordinary `.osk`改为skin-scoped ingress：在`ImportTask.GetReader`/archive open前做raw length与bounded spool，自解析EOCD/central-directory并冻结name/type/size metadata后才允许通用内容读取；actual stream持续计数、CRC/ratio/aggregate/cancellation复核，untrusted `InstantiationInfo`只走closed compatibility mapping。opt-in RealmFileStore exact receipt在fault/cancel时compare-remove本次新增零引用record/blob，不跑全局Cleanup且保留共享hash blob；成功仍保持hash-backed Realm与success-only source delete。
- 因对话意外中断，用户要求停止原目标并最小收口。本次仅修正Journey2测试按唯一`chartskin/<target>`定位managed row，避免共享fixture已有同类记录导致`Single(kind)`假红。验证：`osu.Game` Debug build 0 error/9个已知MessagePack `NU1902`；core archive/journal/recovery/exact-set smoke **152/152**；BMS/Workspace/两条产品旅程相关smoke **34/34**。P1-A四件套、mainline、作者手册、中断交接及memory同步后，`CheckDocumentation.ps1`通过（135个Markdown、1064个相对链接、74个memory wiki链，仅PLAN数字比值复核提醒），`git diff --check`无内容错误。尚未跑core/mania/BMS相关full、Release、targeted formatter、独立产品/安全/并发终审，也尚未提交，因此燃尽保持 **`0/7 closed，C1 active`**，不得生成C2或宣告G1/SV1-2/Skin V1/release完成。该历史缺口由本条保存，最终闭合结果见2026-08-13记录。

## 2026-08-09

### 协作粒度纠偏与七个持久campaign燃尽

- 用户指出`SV1-0`～`SV1-7`已经被远多于八轮协作拆解，阶段编号与实际推进粒度不再匹配。复核确认`SV1-*`本意是能力依赖而非会话次数，但此前handoff prompt确实允许审计、单个foundation或一条窄纵切消耗整轮，导致产品完成度与协作轮数脱节。
- 执行总览改为`C1`～`C7`新对话prompt硬预算：`C1`作者文件工作区/G1 UX与archive安全，`C2`当前consumer revision reload/detach，`C3`P1-K+唯一layout，`C4`shared codec/catalog/resolver/mania compatibility，`C5`scene/event及剩余slot production，`C6`sandbox并关闭ini/manifest/scene/script最终reload门，`C7`canonical双包/Authoring Kit/自动release。当前为`0/7 closed`；完整范围、退出门和阶段映射只看PLAN。reload触发/允许场景、beatmap-local V1范围与VM选型若仍需产品确认，必须在各自campaign同一对话取得决定并立即实现，不能预写未经授权的结论或单独消耗编号。
- 一个campaign是一段持久对话而非一个turn/commit，允许多轮、compaction、多个有意义提交和多组测试。audit、NO-GO、路线冻结、红测、DTO/foundation、单个caller/consumer或文档不能推进编号；未过退出门就留在同一对话，若需用户产品决策也在原对话等待。`C7`结束时只准保留人工视觉、真实设备与长时间体验签收，不能把已知代码/测试/工具/release任务继续外溢。
- 规划变更后的第二次全层级审查清理了mainline、P1-A、dated handoff、派生README与memory中残留的external-only路由：external是`C1`首个技术子门而非campaign终态；同一`C1`必须继续full managed-copy stager/import、managed rename与既有delete、journal支持UX、普通`.osk` archive安全。只有`C1`整体退出后才能进入`C2`当前consumer revision协议；`C3`～`C6`新增consumer逐次加入，最终整包reload/G1自动门只在`C6`关闭。service-owner record只作Realm provenance/admission；fresh held proof授权capture，source文件bytes只来自paired exact capsule，destination handles只授权按logical manifest写入。
- C1实施可行性复审在开工前补闭了五类隐蔽空窗：external register/unregister与全部managed mutation共享有界exact registry generation及durable collision disposition；configured external延续`551a` typed startup/generic epoch合同；noncurrent unregister只做pure-Realm exact service-owner compare-remove，source缺失/漂移也不触盘；managed-copy由fresh capture成对产出exact capsule与含empty directory的logical manifest，首个destination write前已有single canonical v3 combined intent，覆盖copy/ProvisionalReady及既有move/publish而不创建交权空窗；ordinary`.osk`先做pre-open compressed bound/spool，再由自身受预算的metadata parser/open且在任何archive内容消费前完成central-directory/name/type gate，实际流继续硬计数并证明失败零Realm/file-store残留。pre-C1 v2 schema永久冻结读取，C7只迁移证据完整的supported intent；新增archive safety memory。这些仍是C1准入合同，不是已交付runtime。
- 最后一轮产品面复核正式把scanner-owned managed行级Delete纳入C1 Folder Skin Workspace：current-only入口会迫使作者先选择一个只想删除的包，而专用后端本来就以record ID支持noncurrent `NotRequired`。新row只复用fresh `CanDelete`、既有确认语义、manager-owned `DeleteSkinAsync`和同一v3 journal/recovery，不解冻通用delete、不创建第二authority，也不要求构造/选择noncurrent `Skin`；确认框打开后的current/record/owner/path/external generation漂移必须由operation在线性化点fresh重判，current才提交exact fallback，noncurrent保持active pair，split拒绝。该项同样只是C1合同同步，runtime尚未实现。
- 本次只重排产品执行预算并同步文档/memory，没有runtime或测试文件变化，故未重跑focused/full、formatter或Release；最近代码验证继续沿用2026-08-02基线。`CheckDocumentation.ps1`通过（133个Markdown、1046个相对链接、72个memory wiki链），仅有mainline PLAN数字比值的既有非失败提醒；`git diff --check`通过。三路独立范围/可行性终审补齐optional-slot阶段、产品决定授权、跨代reload consumer、ordinary`.osk` early gate、journal v2→v3、empty-directory manifest、VM作者ABI与canonical旧journal迁移；最后对managed row Delete、dialog后fresh disposition及有界archive metadata parser再独立复核后，最终blocker/major/moderate为 **0/0/0**。

### 产品价值、最终差距与后续大纵切收口

- 三路只读审计按真实caller→manager/backend→renderer→用户结果重新分类此前Skin投入。玩家当前真实可达的是：普通`.osk`导入/选择后BMS Note与LN head/body/tail进入renderer；合法managed目录手工放入后重启发现并从settings dropdown选择；eligible managed目录可从settings确认框安全物理删除。immutable capsule直接保证active package revision不被磁盘并发变化混入，`551a`协调直接保护configured managed skin的启动恢复，shared coordinator/journal/recovery已被真实delete消费并保护玩家文件，因此这些复杂安全工作不是无意义抽象。
- directory-only rename与fixed-source staged import的专属operation/recovery仍没有非测试caller、external→provisional stager或UI，只能算潜在后端；现有topology/config/event/capability fixture没有production host/renderer/authoring consumer的部分同样不得按类型数、提交数或测试数计作产品进度。reload审计选择NO-GO且没有继续造foundation，是对投入失衡的正确止损；今后最低切片定义固定为真实caller、全部相关consumer、用户旅程、失败回退、owner安全归属/释放边界与自动/人工验收同切存在，只有声称释放或替换旧owner时才必须同时闭合detach/retirement。
- 按最终release-ready玩家能力只能概括为**约三成**，工程/安全地基约半数且显著高于玩家完成度；二者不是gate，也没有线性剩余工期含义。`SV1-0`完成；`SV1-1`只有Note/LN四组件且视觉0/4；`SV1-2`只有managed发现/选择/delete玩家可达，external/reload未交付；`SV1-3`～`SV1-7`目标产品均未形成，canonical双包不存在且程序化`OmsSkin`仍是链底。最近代码验证仍沿用2026-08-02基线，本审计不新增runtime、视觉签收或release结论。
- 下一高价值候选冻结为external只读作者工作区的完整产品纵切：settings目录选择及独立registrations行级管理→Windows resolved-identity/no-follow完整capture→版本化service-owner Realm注册→dropdown选择/配置重启→同一capsule的BMS Note/LN与legacy mania note/hold最小renderer artifact→打开源目录→只解除注册且绝不物理修改源。管理行持有record ID并提供Open Folder/Unregister，不复用只绑定current的Delete按钮/dialog。owner token只授权Realm记录，不能替代source capability；要收窄当前对全部managed mutation的临时全局阻断，必须让rename、staged-import、managed delete等每个真实admission持有并复验所有相关external root/ancestry proof到final collision linearization point，否则external纵切NO-GO并保留全局阻断。首切只允许noncurrent unregister；任一current half指向目标或pair split都禁用/稳定拒绝，用户须先显式选择并提交其他skin，unregister不dispose旧`Skin`/capsule也不预建reload barrier。无真实settings caller、immutable instance、具体renderer artifact与unregister同切闭合时不得先交付foundation。其后才冻结reload触发方式/允许场景/consumer协议并重新过门；thin stager、逐件optional slot C#、layout/shared codec、scene/script与canonical包仍按PLAN依赖，不因“多推进”越序。
- 本节只同步产品核算、路线和交接，没有runtime或测试文件变化，因此没有重复运行focused/full、targeted formatter或Release，最近代码验证仍沿用2026-08-02基线。`CheckDocumentation.ps1`通过（132个Markdown、1037个相对链接、72个memory wiki链），仅有mainline PLAN数字比值的既有非失败提醒；`git diff --check`通过。独立终审在收紧noncurrent unregister与owner边界后为blocker/major/moderate **0/0/0**。

### `SV1-2` current managed atomic reload/detach 产品 GO/NO-GO

- 严格按现行PLAN的external registration/capture→atomic reload/detach顺序，三路独立只读审计追踪settings/config/hotkey、selection commit、capsule owner及真实renderer/consumer。仓库没有reload current managed revision的production caller、UI、watcher或manager API：settings只提交selection，same-value请求不启动准备，startup scanner只做一次reconcile，filesystem-backed skin继续被editor、update import与external edit拒绝。普通Realm skin的`ExternalEditOverlay`虽会new instance后立即dispose old，但既不接受managed source，也没有consumer barrier，不能成为越序复用入口。
- 现有`SkinManager`只在manager内提交`CurrentSkinInfo`/`CurrentSkin` pair并广播`SourceChanged`，不存在package revision publication对象、consumer registry/ack、detach receipt或旧instance retire queue。真实渲染链无法在同一边界发布：`BmsPlayfield`于loader一次读取并缓存geometry且不监听`SourceChanged`；BMS Note/LN gameplay与pre-start preview分别使用独立`BmsAsyncNoteDrawable` host；`SkinReloadableDrawable`、core/mania drawables及菜单背景混合同步、scheduler、next-update和过渡持有旧`Skin`。因此逐组件SourceChanged、per-host异步A→B和selection pair都不能描述为整包原子reload。
- managed exact capsule/store由active `Skin`拥有；dispose会释放texture/sample/fallback store/capsule，BMS实例还会取消package note preparation。成功selection没有等待全部consumer后退役旧owner的协议，现有产品测试只能手工dispose superseded managed skin。即时dispose会破坏仍挂载consumer，不dispose则无法证明生命周期闭合；既有测试也不覆盖same-ID revision gate、全host publication、失败保留exact旧pair/owner、detach后dispose-once或reload latest-wins/reentrant/cancel/shutdown。
- 产品结论为**NO-GO，本轮停止且不增加reload foundation**。重新开门前必须先由产品冻结真实触发方式、live gameplay等允许场景及全部consumer participation/publication/detach/retirement协议；否则“先建红测”本身就会发明缺失的caller和barrier，违反同切闭合要求。本轮没有runtime、测试或release gate变化，因此没有运行focused/full、targeted formatter或Release；2026-08-02代码基线与managed delete、scanner/selection/mutation协调及`551a`全部强制回归保持不变。`CheckDocumentation.ps1`通过（131个Markdown、1030个相对链接、67个memory wiki链），仅有mainline PLAN数字比值的既有非失败提醒；`git diff --check`通过。该reload审计本身未改变当时mainline顺序，同日后续产品价值与优先级收口见上节。

## 2026-08-02

### `SV1-2` managed chartskin delete 独立产品纵切

- 只读追踪冻结最短玩家链为既有`SkinSection.DeleteSkinButton → SkinDeleteDialog`，没有新增UI设计。按钮资格改用独立fresh-authoritative `CanDelete`，确认框只把detached record ID交给manager-owned `DeleteSkinAsync`，update thread不等待worker。普通Realm `.osk`继续既有soft delete + SetDefault语义；只有eligible scanner-owned `chartskin/<direct-child>`进入专用物理delete。protected/fixed、external、foreign/null owner、非法folder、mixed files、DeletePending、空revision或非allowlist factory全部保持既有行为或fail-closed，旧通用folder `CanModify/Delete`没有解冻。
- delete operation直接消费既有authority/coordinator/canonical journal：首个物理步骤前先以`Prepared(null)` durable绑定held managed-root/source identity、由operation ID唯一派生的`.oms-delete-{operationId:N}` tombstone、exact authoritative existing-record fingerprint及有界、排序、版本化的exact source-node manifest；fallback证明再以同phase monotonic write固化`NotRequired`或`ProtectedPairCommitted`。Windows primitive用显式迭代walker和depth/entry/path/pending-handle预算完成held-root no-follow完整树复核，再执行source→tombstone relative no-replace detach；之后不再观察caller cancellation。verification handles释放后以持有DELETE且只共享READ的fresh no-follow delete-exclusive handles重捕；same-session live完整树必须仍等于manifest，release窄窗内完成的node移出在0次disposition时拒绝，只有fresh recovery的crash survivor可为其子集，exclusive tree取得后的已持有root/child relocation由sharing violation阻断。directory handle不封namespace：preflight前可见的foreign addition/replacement、reparse、hardlink、duplicate identity、metadata/inventory漂移、source replacement或同级碰撞在0次disposition时拒绝；之后竞态新增/replacement不进入held list且绝不删除，可能在exact manifest节点部分清理后令root失败，此时保留FilesystemApplied journal、Realm record并冻结。Realm只在durable FilesystemApplied后以journal fingerprint compare-remove同一record；成功严格持久化`Prepared(null) → Prepared(disposition) → FilesystemApplied → RealmApplied → Committed`并compare-delete terminal journal。
- current目标在任何physical detach前必须先真实提交与`OmsSkin.CreateInfo()`逐字段一致的protected Realm record和exact `OmsSkin`实例pair；两半ID/type、Name/Creator/InstantiationInfo、空Hash、空owner/path/files、nonexternal/nonpending均必须coherent。`ProtectedPairCommitted`在detach边界、Realm hard-remove与recovery继续要求exact protected fallback Realm record，但不冒充重验detach前runtime pair；`NotRequired`仅允许两半ID一致且都非目标，noncurrent live/recovery不创建或要求OMS record。split pair、invalid fallback、selection disabled、authority/receipt漂移或恢复歧义都在物理步骤前拒绝并在exact receipt仍可证明时安全RolledBack，否则保留最后durable journal冻结；物理步骤后若缺exact durable phase/disposition/fallback证据，不得只凭“source/tombstone/record都不在”猜成成功。
- recovery按source/tombstone、exact Realm fingerprint、source-node manifest、durable fallback disposition与phase覆盖Prepared/FilesystemApplied/RealmApplied/terminal crash/restart矩阵：可证明未detach时安全回滚；Prepared只在已确认disposition、TargetOnly且Realm exact时先清理并写FilesystemApplied，Prepared+Neither即使disposition已确认也保持歧义；FilesystemApplied/RealmApplied再按各自exact Realm/fallback证据从TargetOnly或Neither逐phase前滚，partial cleanup只续删manifest子集。raw disposition却出现物理进展、Both、identity mismatch、foreign/conflicting record、fallback无效或证据缺失均保持歧义冻结。首个物理步骤前（包括disposition已经exact落盘后）取消可abort，之后由journal/recovery决定结果。configured selection仍只等待typed startup/staged-import completion；generic delete epoch fail-closed，fresh Realm/path/owner/freeze/capture/factory、latest-wins/reentrant保持。selection在resolver触盘前新增non-blocking coordinator admission，避免generic contention先检查路径。fallback publication先完成worker TCS再发`SourceChanged`，关闭event重入shutdown的join环；queued fallback由scheduler callback或shutdown恰一方claim/reap，late callback no-op，Realm释放前joinstartup、rename、staged-import、delete与selection全部真实worker。
- 自动验证：core managed mutation+contract broad **281/281**（含真实Windows delete native **11/11**）、managed selection产品类 **62/62**、mania skin **182/182**、BMS full **1530/1530**；core skin broad **911/917**的六项失败与既有基线相同（四项removed Osu archive fixture、两项default-skin旧假设）。mania full **827/831**的四项`TestSceneAutoGeneration` replay frame既有失败且与本切无文件交集；额外core full执行仍受已移除ruleset/fixture的既有广泛失败阻断，本切相关子集无新增失败。Release **0 error / 20 known warnings**，仍为MessagePack `NU1902`及BMS tests既有`CS8600`/`CA2007`。独立安全审查补齐exact fallback、live post-detach drift、`SourceChanged`重入shutdown、delete-exclusive relocation、release-gap exact manifest及namespace-race foreign preservation后，最终blocker/major/moderate为 **0/0/0**。
- journal payload version保持v2，大小预算因最多8193个定长manifest节点由128 KiB提高为1 MiB。pre-product foundation阶段从无production delete caller；legacy-v1或旧v2 Delete payload若缺derived tombstone、exact existing-record fingerprint、source-node manifest，或physical phase缺durable fallback disposition，均不能安全迁移，strict load会按Invalid全局冻结且不猜测path/record/tree；v2 Rename/StagedImport与legacy-v1非Delete terminal处理不变。本切未实现thin stager、任意path import、external、reload/detach、scene/script或canonical包，未启动GUI或新增视觉签收；`V-001`～`V-004`仍为0/4，G1、`SV1-2`、Skin V1与release均未完成。

## 2026-08-01

### 产品价值与跨会话收口

- 以`551a64af3bc2958db4baa57421b73fee61f259ac`复核真实入口、renderer、settings caller与无caller后端：本轮竞态闭合直接保护玩家已配置managed skin的启动恢复链，新增typed coordinator/retry均有production consumer；但它只提高已有能力可靠性，没有增加可见功能，最终可发布Skin V1排期量化仍约25%～30%。rename/staged import与topology/event/capability种子不得计作玩家功能，也不得继续横向扩张无同切consumer的foundation。
- 本轮冻结下一产品判断：managed delete仅在独立`CanDelete`、真实async settings caller、物理/Realm/journal/recovery/current fallback能同切闭合时conditional GO；thin staged-import stager/caller维持NO-GO。该收口只同步文档与memory，不修改runtime、release gate或视觉状态，也不重复产品测试/Release；`CheckDocumentation.ps1`通过（128个Markdown、1013个相对链接、58个memory wiki链），仅保留mainline PLAN数字比值的既有非失败提醒，`git diff --check`通过。

### `SV1-2` configured managed selection ↔ startup scanner 独立端到端切片

- 先以真实`SkinManager` native capture/factory、shared coordinator与真实scanner建立确定性headless产品交错：configured candidate首轮capture被阻塞，startup sequence内scanner遍历慢/多包snapshot并在Realm reconcile前受控暂停，随后让capture completion撞入最终发布边界。修复前该测试超时且始终保留旧`OmsSkin` pair，证明大目录/慢capture风险真实存在；另锁定scanner在capture完成前已退出、在首轮factory中退出两种completed ordering，避免只修active-holder时序。
- startup recovery→scanner现使用typed `StartupSequence`外层lease，staged import使用独立typed reservation，rename/delete/其它真实mutation保持generic reservation。coordinator ownership发布与non-blocking selection admission在同一锁内线性化；阻塞participant改为可取消的monitor wait，没有旧`SemaphoreSlim`取得后尚未发布owner的空窗。final boundary只为exact active startup/staged-import holder返回其completion；generic short scope或mutation reservation仍返回无retry authority。
- 每次managed preparation捕获startup sequence epoch与generic mutation epoch。Realm mismatch发生在scanner已完成后时，crossed startup epoch可触发fresh retry；generic mutation在首次mismatch前、factory后direct typed boundary前、已注册waiter但deferred callback未执行前、或后续typed chain中任一时刻跨越，都会在排队前或持有retry short lease后被复核并fail-closed。observation贯穿contention waiter、scheduler callback与chained retry，关闭check→fresh preparation的TOCTOU；staged import不计入generic epoch，并用exact holder completion保留“失败后立刻完成”的既有语义，不再依赖全局running/epoch猜测。
- retry worker只在后台等待typed completion，update thread始终non-blocking；回到update scheduler后先要求shutdown/disabled、generation、旧`CurrentSkinInfo`/`CurrentSkin` pair仍一致，再从Realm按ID重取记录，重新解析path/owner/freeze/allowlist并重新native capture/factory。latest accepted request、reentrant请求、记录删除、owner/path/freeze/factory变化都会胜出。startup期间新的manual managed选择仍即时`ManagedFolderOperationInProgress`且不取消既有configured preparation；普通Realm `.osk`保持同步路径，不触发managed capture。
- selection lifecycle新增capture-scheduling task、contention waiter与queued completion的统一ownership。shutdown先封门、推进generation并cancel capture/retry CTS，再原子claim并回收已完成capsule/CTS，最后join worker；已被宿主丢弃的queued callback之后只会no-op。completion scheduler fault即使撞上generic lease也只做non-blocking稳定化，不会让shutdown反向等待mutation或泄漏owner/unobserved task。
- 自动验证：coordinator **11/11**、startup scanner lifecycle **2/2**、完整managed selection产品类 **52/52**、core managed **275/275**、mania skin **182/182**、BMS full **1520/1520**。core skin broad **863/869**，六项失败与既有基线完全一致：四项`TestSceneBeatmapSkinResources`依赖已移除Osu ruleset archive fixture，以及`TestBackgroundCyclingOnDefaultSkin(True)`、`TestSampleUpdatedBeforePlaybackWhenNotPresent`两项既有native-default视觉/资源假设。production/core-test/BMS-test三工程targeted formatter verify、`CheckDocumentation.ps1`与`git diff --check`通过；`osu.Desktop.slnf` Release **0 error / 20 emitted known warnings**，仍为9条MessagePack `NU1902`在restore/build重复及BMS tests既有`CS8600`/`CA2007`。三路独立终审最终blocker/major/moderate **0/0/0**。
- 本切没有实现UI、production stager、managed delete、external、reload/detach、scene/script或canonical包，也未启动GUI/操控桌面或新增视觉签收。按最短玩家价值链复核：managed delete已有settings delete dialog/caller雏形、专用authority与protected fallback foundation，下一切**conditional GO**，但必须同切闭合独立`CanDelete`/async caller、物理+Realm删除、journal recovery、current fallback、取消/shutdown与隐私；thin staged-import stager/caller仍缺external→fixed provisional可信source/no-follow复制、预算、取消、清理、诊断和真实caller，当前**NO-GO**，不得继续增加无consumer foundation。

## 2026-07-31

### 产品可达性、开发价值与跨会话交接审计

- 以已推送的`c53f1e08d88a023a56267bbeb5802d6cc9bfc080`为runtime基线，反查真实入口、非测试caller、selection/rendering链与最终Skin V1门。已导入`.osk`的BMS Note/LN四组件真实进入`BmsRuleset → BmsSkinTransformer → BmsManagedPackageNoteProvider → BmsAsyncNoteDrawable`；合法managed目录经startup scanner注册后可进入现有dropdown并由guarded exact-capsule selection发布。这些是玩家可达能力，不是fixture幻象。
- `RenameManagedFolderAsync`与`ImportManagedFolderAsync`全仓没有非测试caller；fixed staged import也没有把外部来源安全复制到`skin-mutation-staging/{operationId:N}`的production stager或UI。operation、journal、recovery和shutdown已在production程序集装配并解决真实数据安全/崩溃一致性问题，但本身没有新增玩家可触发功能；今后不得把internal method或production项目代码量写成产品E2E。
- 新发现Major启动竞态：configured managed selection在`OsuGame.load()`先异步capture，startup scanner随后持有shared coordinator做完整discovery/reconcile；若completion撞上scanner，当前非staged-import的final-boundary争用会直接`ManagedFolderOperationInProgress`且不重试。已有测试分别覆盖无争用configured selection与scanner lifecycle，没有覆盖二者交错。下一刀改为先确定性复现并修复；不得阻塞update thread或放宽真实mutation争用，scanner后仍须重做generation/Realm/path/owner/freeze复核。
- 最终Skin V1仍处于“安全地基显著推进、玩家可见能力只有窄纵切、作者runtime尚未成形”的阶段：`SV1-0`完成；`SV1-1`仅Note/LN四组件自动门且`V-001`～`V-004`为0/4；`SV1-2`缺caller/stager/UI、delete、external和atomic reload/detach；`SV1-3`～`SV1-7`没有产品实现，canonical双包与Authoring Kit未落，程序化`OmsSkin`仍是链底。若为排期强制量化，release-ready玩家产品仅约25%～30%，该区间不是gate。
- 同步mainline/P1-A STATUS/PLAN/CONSTRAINTS、路由、other索引及managed selection/mutation/authoring memory；新增[产品进度审计](../../other/SKIN_SYSTEM_PROGRESS_AUDIT_20260731.md)作为dated证据。后续shared slot/topology/config/event/capability/candidate合同只有在同切或紧随切片存在production consumer时才继续扩张。竞态闭合后，对thin staged-import product caller/stager与managed delete做产品go/no-go，而不是默认再堆一层无入口后端。
- 本次未修改runtime、未重跑产品测试或Release，也未启动GUI/操控桌面、未新增视觉签收；`c53f1e0`的验证数字保持在2026-07-29原始条目，不倒写历史。`CheckDocumentation.ps1`通过（127个Markdown、998个相对链接、57个memory wiki链），仅保留mainline PLAN数字比值的既有非失败提醒；`git diff --check`通过。

## 2026-07-29

### `SV1-2` managed chartskin staged import 独立端到端切片

- 产品语义冻结为internal production staged import：只接受upstream stager预先复制到固定`skin-mutation-staging/{operationId:N}`、外部原来源仍保留且由OMS为本operation持有的provisional副本；managed root是既存authority root。target direct-child由可信internal caller提供，按NFC/Windows/direct-child规则在首个物理步骤前拒绝大小写、physical及Realm ID/path冲突；不覆盖、merge或自动suffix，也不开放任意path/external registration。
- 操作直接消费既有`SkinManagedFolderMutationAuthority.OpenStagedImport`、held staging root/source、held managed root、target name-slot、`SkinManagedFolderNewRecordPublicationPlan`、shared coordinator、canonical journal store与exact durable receipt。source先完成held no-follow full package capture并闭合根`skin.ini`、closed实例类型、capsule revision、reparse、hardlink/duplicate、busy writer和完整inventory；capture同时生成覆盖capsule revision、空目录、每个节点identity/kind/length/creation/attributes/reparse/link/delete及exact ordinal层级边界的确定性完整physical-tree fingerprint。首个move或provisional cleanup前，Prepared必须持久绑定uppercase content revision与lowercase tree fingerprint并exact reload。
- Windows production primitive以held staging parent/source与held managed root执行同卷identity-preserving no-replace move，不退回absolute path、字符串前缀、普通`Directory`/`File` API或live `Storage`授权。真实NTFS仍要求final preflight后释放仅必要descendant handles，继续持有两侧parent/source identity，不再观察caller cancellation，立即move并从target no-follow完整重捕；每次可判定inspection返回前重新枚举source/target双槽，target identity、content revision、完整physical-tree fingerprint与全部安全gate必须和Prepared exact。
- 成功链严格闭合`Prepared → FilesystemApplied → RealmApplied → Committed`，terminal journal compare-delete后确认Missing。one-shot Realm publisher只在durable `FilesystemApplied`、最终target capture及最终Realm冲突复核后发布一条record：`ID = operationId`、path为exact target managed path、`Name`/`Creator`/hash来自最终capsule、实例类型为closed allowlist、`Files`为空、非external/protected、`DeletePending=false`；完整通过scanner同等级注册门后才交接exact scanner owner。publication plan不是Realm writer，ordinary startup scanner不消费plan，也不得竞争发布第二条record；任何fixed skin ID均不得成为staged operation/record ID，持久化后重算checksum也按invalid journal冻结处理。
- staged-import recovery作为现有`SkinManagedFolderMutationRecovery`的同一按kind handler复用coordinator/journal：target exact时对Realm absent前滚发布、exact planned视为提交；source exact且target absent时只删除exact planned record（若存在）、清理exact provisional并回滚；neither仅在journal仍能证明provisional可丢弃且外部原来源已保留时同样回滚。current journal恢复不得根据终态证据跳过durable phase：必须逐一写入并exact reload `FilesystemApplied → RealmApplied → Committed`，每次重检双槽/Realm，publisher绝不早于durable `FilesystemApplied`；任一phase checkpoint失败保留最后已落盘阶段，fresh restart继续而不重复Realm publication。partial self-cleanup只凭durable staging-root/source identity与树证明续删同一provisional root。both、root/source/target identity mismatch、physical-tree/content drift、foreign/conflicting Realm、同ID/同path冲突或字段漂移均保留journal并冻结；不得删除managed target、foreign record或不明node。
- import不自动选择、不替换current active immutable capsule，也不复用rename的全局pending取消；无关pending selection在authoritative复核仍成立时继续。scanner snapshot→commit、重复record抑制、negative cleanup冻结与重启恢复继续服从shared coordinator；startup、rename与staged-import worker在Realm释放前统一cancel + synchronous join。operation/recovery状态只输出kind/phase/status/count。
- 自动验证：authority/journal/rename/staged-import/coordinator/scanner/capture及真实Windows native focused **265/265**；BMS完整selection产品类 **36/36**；core skin broad **856/862**，6项均为既有基线：4项`TestSceneBeatmapSkinResources` removed Osu ruleset archive fixture，以及`TestBackgroundCyclingOnDefaultSkin(True)`、`TestSampleUpdatedBeforePlaybackWhenNotPresent`两项native-default视觉/资源假设；mania skin **182/182**、BMS full **1504/1504**。production/core-test/BMS-test三工程targeted formatter verify通过；`osu.Desktop.slnf` Release **0 error / 20 emitted known warnings**，为9条MessagePack `NU1902`在restore/build重复及BMS tests既有`CS8600`/`CA2007`。`CheckDocumentation.ps1`通过（126个Markdown、983个相对链接、57个memory wiki链），仅保留mainline PLAN数字比值的既有非失败提醒；`git diff --check`通过。两轮独立终审修复恢复相位与fixed-ID journal问题后，剩余blocker/major/moderate **0/0/0**。本轮未启动GUI或操控桌面，没有新增视觉验收；`V-001`～`V-004`仍为0/4，managed/external/重启/切换/import/delete等最终视觉与实机gate仍未完成。UI、managed delete、external、reload与atomic detach继续冻结；下一独立纵切为managed delete。

### `SV1-2` managed chartskin directory-only rename 独立端到端切片

- 产品语义冻结为“目录身份与作者展示身份分离”：rename只把一个`chartskin/<direct-child>`工作目录从source slot移动到target slot，并更新同一authoritative Realm record的`FilesystemStoragePath`。根`skin.ini [General] Name`、Realm `Name`/`Creator`、包字节、revision/hash和scanner owner均不修改；没有发布新record，也没有接入旧通用rename或UI。
- 操作直接消费既有`SkinManagedFolderMutationAuthority.OpenRename`、held managed root/source、target name slot、shared coordinator、canonical journal store与exact durable receipt。首个物理可见步骤前必须durable Prepared；成功链严格闭合`Prepared → FilesystemApplied → RealmApplied → Committed`并compare-delete确认Missing，物理步骤前取消可写`RolledBack`后exact cleanup，物理步骤一旦尝试则由journal/recovery而非caller cancellation接管不确定结果。
- Windows production primitive使用source DELETE handle与held managed-root handle执行relative no-replace `FileRenameInformationEx`，不从absolute path、字符串前缀或live `Storage`授权。完整gate覆盖大小写/NFC collision、reparse、hardlink、重复physical identity、busy writer、target race、source→target identity continuity，以及允许renamed root时间推进但要求descendant exact metadata与目录项name/identity/kind完整inventory的最终复验；target被竞争创建时不覆盖。
- 真实NTFS取证确认：descendant handles即使允许`FILE_SHARE_DELETE`，flags `0`与诊断用POSIX `0x2`仍使非空目录rename返回`STATUS_ACCESS_DENIED (0xC0000022)`。实现因此在最后一次完整held-tree preflight与caller取消检查后释放仅descendant handles，继续持有exact root/source identity，不再观察caller取消，立即move，再以`CancellationToken.None`从target no-follow重捕并重新持有完整树。该release→move→recapture窄窗口不是filesystem transaction、全树排他或字节内容快照；任何可观察漂移都保留journal并冻结。
- rename production recovery按physical slot和Realm path四格幂等收敛：`SourceOnly/source`已回滚、`SourceOnly/target`回滚Realm、`TargetOnly/source`前滚Realm、`TargetOnly/target`已提交。`Both/Neither/IdentityMismatch`、record/root identity或Realm path不可信均保持歧义、保留journal、冻结source/target并继续禁止scanner negative cleanup；恢复只改同一record path，不改展示/内容元数据。
- 成功rename不销毁active immutable capsule；全局selection generation推进并取消当时的pending preparation，旧generation不得发布，未来重新选择从新path capture。scanner snapshot→Realm commit与rename共用coordinator；重启歧义同时阻止selection和negative cleanup；shutdown在Realm释放前cancel并同步join worker。operation/recovery只暴露脱敏status，不记录path、record/operation ID、identity或native异常正文。
- 自动验证：rename authority/native/journal/recovery/scanner focused **195/195**；BMS rename lifecycle **5/5**、完整selection产品类 **29/29**；core skin broad **783/789**，其中4项为removed Osu archive fixture，另2项既有native-default视觉/资源假设在隔离复跑仍失败且不触及本切；mania skin **182/182**、BMS skin **624/624**、BMS full **1497/1497**。production/core-test/BMS-test三工程targeted format verify通过；`osu.Desktop.slnf` Release **0 error / 20 emitted known warnings**，均为9条MessagePack `NU1902`在restore/build重复及BMS tests既有`CS8600`/`CA2007`。`CheckDocumentation.ps1`通过（126个Markdown、982个相对链接、56个memory wiki链），`git diff --check`通过；独立终审blocker/major/minor **0/0/0**。
- 本轮未启动GUI或操控桌面，`V-001`～`V-004`仍为0/4，rename没有新增视觉签收项。UI、staged import、实际delete、external与atomic reload/detach继续冻结；本切不等于G1、`SV1-2`、Skin V1或release完成。

### rename提交后权威文档与memory一致性审计

- 以已提交production行为反查P1-A权威合同，补正两处容易误导后续纵切的精度：rename target physical identity必须与held source exact一致；staged import只在创建/移动后固定自己的target identity。rename最终复验允许目录根自身的rename-related change timestamps推进，但descendant exact metadata、目录项name/identity/kind inventory及reparse/hardlink/duplicate/busy-writer gate均不得放宽。
- 同步当前作者说明与capsule、filesystem preflight、scanner、selection、recovery、mutation foundation跨会话memory：directory-only rename已是internal production operation；active immutable capsule可存活而未来recapture使用新路径；startup scanner与rename worker均须在Realm释放前依次cancel + join。既有dated CHANGELOG条目与旧schema原始结论保持原样；recovery memory的滚动锚点更新至2026-07-29，没有把后来的实现倒写进历史结论。
- 本次只修正文档与memory，不改变runtime、产品能力、全局优先级或release gate，故不重复产品测试和Release build，也不回写mainline。`CheckDocumentation.ps1`通过（126个Markdown、982个相对链接、56个memory wiki链），仅保留mainline PLAN数字比值的既有非失败提醒；`git diff --check`通过。staged import仍是下一独立纵切；delete、rename UI、external与reload继续冻结。

## 2026-07-27

### `SV1-2` managed chartskin mutation authority/recovery foundation

- 代码取证确认旧scanner的`scanGate`只串行单实例，native discovery返回后held `chartskin` authority已释放，selection使用自身提交边界，普通delete命中current只异步调度fallback后继续流程。新增共享`SkinManagedFolderOperationCoordinator`：短lease允许同线程嵌套，detached mutation reservation不可重入且可跨线程交还；scanner从discovery到Realm reconcile返回、selection最终authoritative重读/pair发布、mutation和recovery全部通过该边界线性化。`SkinManager`构造期先恢复，`OsuGame`worker再用同一外层lease连续幂等执行recovery→scanner。
- 专用mutation authority按ID刷新重读既有record并复核唯一合法direct-child、空`Files`、非external/protected/fixed/DeletePending、exact scanner owner、allowlisted类型与非空revision；任一external filesystem声明在resolved identity实现前保守阻断managed mutation。Windows session从物理卷逐段no-follow持有data root/`chartskin`/source identity；source handle拒绝外部write/delete，target只签发held root绑定、NFC/Windows规范和case-insensitive absence/collision验证过的name slot，不预造identity。修正staged source held DELETE handle与authority-link复验的share/access配对，根链share未放宽。
- staged source不接受调用方path/token，只能从固定`skin-mutation-staging/{operationId:N}`捕获同卷staging-root/source held identity。staged新记录的planned ID固定为operation ID，并以immutable plan绑定target path、managed-root identity与version；plan不是Realm writer且scanner不能消费，真正one-shot publisher留到staged-import独立切片的durable`FilesystemApplied`+final target identity之后。
- 新canonical version-1 journal采用稳定文件名、严格UTF-8/固定schema与token type、duplicate拒绝、SHA-256、128 KiB预算、同目录write-through temporary+`Flush(true)`及Windows原子write-through replace。intent只能从Prepared开始，显式phase图单调推进，terminal不可重写且A不能覆盖B；精确孤儿temp可清理，canonical目录/reparse、locked/ACL/IO与未知journal-like sibling均fail-closed。session落盘后必须exact reload才返回绑定session/store/journal的durable receipt，未终结dispose或不确定持久化结果粘性冻结关联路径。
- 启动recovery对已配置operation handler的可判定状态可幂等forward/rollback；本轮未开放handler，因此有效nonterminal保留journal并精确冻结source/target，invalid/unsupported/IO全局冻结。terminal只在compare-delete并确认Missing后解冻；先前歧义后突然Missing仍保持冻结。scanner的valid add/update/revive与negative cleanup、managed selection及新mutation均服从冻结。
- delete foundation只在update thread、held mutation reservation与exact Prepared receipt下确认受保护fallback pair；迁移期精确要求程序化`OmsSkin`的protected Realm record/type与最终`CurrentSkinInfo`/`CurrentSkin` pair。独立审查发现原`NotRequired`只看info半边会在可达split-brain下误放行，现收紧为两半ID一致且都非目标；split pair、fallback/selection/receipt/authority失败均拒绝并在未发生外部mutation时abort Prepared。该路径没有删除Realm record或目录。
- 新增/扩充authority资格和post-open owner/hash/DeletePending/target drift、coordinator重入/跨线程、journal store/recovery故障矩阵、scanner冻结/线性化、Windows staged authority及BMS真实选择/fallback pair合同。focused合并 **107/107**，BMS production selection/fallback **24/24**；扩大回归core skin **337/341**，4项均为既有removed Osu archive fixture且Argon旧失败本轮通过；mania skin **182/182**、BMS full **1492/1492**。最终独立审查blocker/major **0/0**；未启动GUI或操控桌面，`V-001`～`V-004`仍0/4。
- production/core-test/BMS-test三工程targeted formatter及verify均exit 0；`osu.Desktop.slnf` Release **0 error / 18 emitted known warnings**，均为MessagePack 3.1.3既有`NU1902`在restore/build重复输出，未用`NoWarn`隐藏。`CheckDocumentation.ps1`通过（126个Markdown、980个相对链接、51个memory wiki链），仅保留mainline PLAN数字比值的既有非失败提醒；`git diff --check`通过。
- foundation没有任何create/move/rename/delete primitive、operation-specific recovery handler、Realm新记录publisher或UI。rename目录名/展示名语义、staged import copy/move与冲突、真实delete、external和atomic reload/detach仍按PLAN独立过门，不得把本切写成G1、`SV1-2`或产品交付完成。

### 本轮协作收口：foundation产品可达性与价值复核

- 只读取证确认本轮并非孤立测试抽象：共享coordinator、启动recovery顺序、scanner冻结/negative-cleanup保护与selection最终authoritative重读都已接入production；它们立即约束现有受管目录发现与选择，直接收口恢复审计中曾出现的snapshot→Realm竞态和陈旧selection发布风险。
- 三个`OpenRename/OpenStagedImport/OpenDelete`入口、native write primitive及operation-specific recovery handler则刻意没有production caller；因此本轮没有新增玩家可见rename/import/delete能力。当前production遇到有效nonterminal journal只会保留并冻结，这是尚未接操作handler时的安全状态，不是可恢复操作已经交付。
- 价值判断为“必要安全底座、尚待纵切兑现”：既有记录资格、held source/target、durable receipt、journal phase、启动冻结及current delete fallback pair可被后三个操作直接复用；若后续继续横向扩foundation而不交付首条真实rename，就会开始形成沉没成本。下一对话必须先冻结rename产品语义并直接消费现有authority/coordinator/journal/recovery，不得另建平行链。
- 后续已知硬门包括：rename是否联动`skin.ini`展示名可能决定journal v1是否够用；真实操作须提供可判定crash state的幂等handler；external落地时须把“任一external声明全局阻断”收窄为resolved-identity局部冲突；canonical接管时须把delete fallback从程序化`OmsSkin`切到只读`oms-simple.osk`；开放操作前还须补脱敏恢复状态。该复核不改变当前执行顺序或gate结论。
- 本次收口只同步当前风险、路由与跨会话记忆，没有修改runtime，因此不重复运行产品测试或Release；`CheckDocumentation.ps1`通过（126个Markdown、980个相对链接、56个memory wiki链），仅保留mainline PLAN数字比值的既有非失败提醒，`git diff --check`通过。

## 2026-07-26

### 当前合同、产品语言与跨会话记忆复核

- 代码只读取证确认`bd40966`仍为产品基线：schema 57 owner/scanner、native capture、exact-capsule factory与guarded selection已经形成合法受管目录的窄生产链；external production registration/capture、专用mutation、durable recovery journal、整包publication barrier与全consumer detach仍不存在。旧folder mutation入口被冻结，不等于已有安全删改。
- 取证确认scanner的`scanGate`只串行单实例，discovery snapshot返回前held-root authority已经释放，启动链也没有journal recovery前置阶段。未来mutation因此必须先实现共享authority/线性化、启动“先恢复后scanner”、跨filesystem/Realm durable journal与歧义状态fail-closed；已有记录的exact owner和Realm记录只提供资格。既有受管source与受管target authority必须从held `chartskin` root下的no-follow physical identity即时签发；staged source则必须来自另行批准并固定identity的authority，新记录发布authority仍须冻结。
- PLAN把已完成的Note/LN和G1前五个切片压回STATUS/CHANGELOG，只展开未完成顺序：先foundation且不开放UI/真实写入，再按rename、staged import、delete独立端到端过门，随后external与atomic reload/detach。rename的目录名/展示名语义及import来源/copy-move/冲突语义仍须先冻结，不能由实现猜测。
- 既有普通delete命中current时只异步调度`DefaultOmsSkin`后继续删除，不能证明selection pair已经提交。硬约束现要求受管目录current delete等待当时已验证protected fallback真实提交；迁移期为程序化`OmsSkin`，canonical接管后为`oms-simple.osk`。确认式路径未实现前保持冻结，失败或恢复状态不明时拒绝物理删除。
- 制作者说明与视觉清单统一区分已导入`.osk`和`chartskin`受管目录；scanner只发现/注册、不自动选择。清单移除旧测试数字与漂移构建锚点，视觉结论仍为`V-001`～`V-004`待统一签收。memory删除重复当前态/逐刀史，补scanner frontmatter、authority交叉路由及mutation协调地雷；不提前创建尚未冻结的mutation实现memory。
- 本轮只改文档与memory，未修改runtime/生产数据，未运行产品测试或Release，未启动GUI。`CheckDocumentation.ps1`通过（125个Markdown、978个相对链接、48个memory wiki链），`git diff --check`通过；不改变`bd40966`记录的任何自动/人工gate。

## 2026-07-17

### `SV1-2` 第五刀：schema 57 exact-owner managed启动自动发现/reconcile

- Realm schema从56升至57，`SkinInfo`新增nullable opaque `FilesystemStorageAuthorityOwner`；migration明确零backfill，旧/null记录保持unknown且scanner永不claim。roundtrip与真实动态schema56升级测试锁住token大小写/opaque原样持久化、旧字段保留和owner=null。folder selection与`Skin` detached snapshot同时携带owner，capture/factory期间owner变化会被authoritative复核拒绝，但owner本身不授予filesystem capability。
- 新managed scanner只维护exact token `oms.skin.managed-folder.scanner.v1`。完整snapshot分离全部合法direct-child `ObservedPaths`与通过capture/metadata的`ValidDiscoveries`：valid可add/update/revive；只有唯一、结构合法、exact-own且未observed的记录可soft-delete。null/foreign/duplicate/mixed Realm files/external/protected/fixed-ID/普通`.osk`均不claim不改；observed file/reparse/坏包保护既有记录。所有reconcile在一个Realm事务内，非法snapshot、根缺失/不可读、异常、取消或不完整scan零negative；apply及commit前取消使整笔事务回滚，scanner从不删除磁盘文件。
- Windows source从physical volume handle逐段固定data root与同一个held `chartskin` handle，baseline枚举、候选相对no-follow capture和最终完整inventory/authority-link复验共享该authority。每个valid包复用既有immutable capsule gate，根`skin.ini`按大小写不敏感定位并以strict decoding、1MiB metadata上限、256字符/控制字符限制提取Name/Author；case collision或坏metadata只保留observed。真实NTFS刚写目录的LastWrite/Change时间可能延迟落稳，因此仅对明确identity/inventory race做最多3次、每次25ms、可取消的**完整session**重试，失败轮不发布partial snapshot。source/result/异常安全字符串不展开路径、名字或revision。
- `OsuGame.LoadComplete`末尾由线程池执行一次scan；既有Realm通知链自动刷新Skin dropdown，不自动切换当前皮肤。`Dispose`最先cancel并同步join scanner，再由base释放Realm；固定日志不记录异常正文。headless lifecycle以500ms闭锁证明worker非update thread、恰运行一次、Dispose确实等待，并在worker finally访问Realm成功后才返回。
- focused结果：schema/scanner **12/12**，native fake+真实Windows **55/55**，startup/Dispose lifecycle **2/2**，BMS production selection **15/15**。真实native smoke在source dispose后成功rename/delete包目录，证明handle释放；最终独立安全审查blocker/major **0/0**。全程未启动GUI、开窗或操控桌面，`V-001`～`V-004`仍0/4。
- 扩大回归为core相关 **222/222**、mania skin **182/182**、BMS full **1483/1483**；改动文件三工程format verify均exit 0，`osu.Desktop.slnf` Release **0 error / 20 emitted known warnings**（9条MessagePack `NU1902`在restore/build重复为18条，加BMS tests既有`CS8600`/`CA2007`）。
- 当前产品语义是“合法`chartskin/<direct-child>`在重启后自动进入选择面”，不是watcher或热重载；启动后原位变化、新revision publication、全consumer detach、专用managed import/rename/delete与external registration/capture仍未实现。因此G1、`SV1-2`、Skin V1与产品交付均未完成，下一刀转入专用managed mutation。

### `SV1-2` 第四刀：production managed folder exact-capsule factory 与 guarded selection

- 生产`SkinManager`现只对Realm中authoritative `IsManaged`且由resolver认定合法的`chartskin/<direct-child>`记录启动后台native no-follow capture；成功capsule通过明确exact-store marker转入owning revision store，再由closed ordinal allowlist精确实例化`BmsLegacySkin`。folder要求根`skin.ini`和公开exact-capsule四参数构造入口，不添加live `RealmBackedResourceStore`，也不再经`SkinInfo.CreateInstance()`落入历史`TrianglesSkin` fallback；普通`.osk`、`OmsSkin`与mania路径保持既有行为。
- 选择链在capture完成后与factory完成后双重复核authoritative记录，并以generation/current-selection及prepared target对象identity作为提交门；只有全部仍匹配才在update thread一次发布`CurrentSkinInfo`/`CurrentSkin` pair。invalid/unregistered/unmanaged/external、hardlink、capture/factory failure、记录或目录竞态、stale generation、reentrant request与completion scheduler fault均保留旧pair、稳定reason和已提交配置，并清理provisional capsule/store；任务异常被显式观察且不记录敏感原生路径。
- 新增guarded selection bindable并封闭binding graph：普通`Bindable`、generic Dropdown与lease不能双向写入committed值；settings改用本地展示bindable调用request surface，再从guarded committed副本单向同步。disabled/same-value不启动准备，reentrant请求不能通过内部commit路径绕过precommit gate。active BMS普通短键与长条head/body/tail全部从同一immutable capsule revision读取，capture后的磁盘变化不影响当前实例。
- folder旧mutation面在UI、manager与importer层冻结；delete/undelete、文件add/delete/replace、update-import、external edit及base/interface dispatch都会在真正Realm事务内按ID重新取得authoritative记录后判定，同ID伪造或陈旧shadow object不能授权。该冻结不是managed mutation实现；后续仍需独立no-follow journal/rollback服务。
- 产品路径focused **14/14**，覆盖真实native capture→selection、immutable Note/Head/Body/Tail、无capture的非法/未注册记录、配置选择、hardlink/capture/factory/竞态/reentrant/scheduler失败清理、并发Realm请求原子supersede、off-thread managed无副作用拒绝，以及base/interface与同IDspoof mutation冻结；最终独立审查为 blocker/major **0/0**。本切未启动GUI或操控桌面，`V-001`～`V-004`仍集中待验收。
- 扩回归首轮发现普通Realm `.osk`被过宽的update-thread门误伤；收窄后终审又发现后台Realm请求可插入managed最终检查与commit之间。最终将thread gate移到任何generation/cancel之前，并用同一commit lock串行所有请求的generation bump/Realm commit与managed最终检查/commit，异步reason也只允许同generation写回。最终core importer/selection/capsule **101/101**、BMS相关 **210/210**、settings/startup **3/3**、mania `OmsSkin` **84/84**、BMS full **1482/1482**；改动文件四工程format verify均exit 0，`osu.Desktop.slnf` Release **0 error / 20 emitted known warnings**（9条MessagePack `NU1902`在restore/build重复为18条，加BMS tests既有`CS8600`/`CA2007`）。
- 当前只闭合“已注册合法managed记录可安全选择”的窄链，尚无schema 57 scanner owner、自动发现/导入、专用managed rename/delete/import、external registration/capture、整包atomic reload或全consumer detach barrier；因此G1、`SV1-2`、Skin V1与产品交付均未完成。下一刀进入scanner ownership，异常期归档仍只可定点取证。

### `SV1-2` 第三刀：managed Windows fixed-handle/handle-relative no-follow capture

- `SkinFilesystemStorageResolver` 只为合法 managed `chartskin/<direct-child>` 发出 opaque capture request，request 构造受 private issuer 约束但不是 security/filesystem/mutation capability；Realm `.osk`、external 与 invalid declaration均无 request。新增平台安全入口和 typed result/rejection，安全字符串不展开 data root、package path、file ID或原生异常。
- Windows producer用 `QueryDosDevice` 将 drive收窄为 exact `\Device\HarddiskVolume<uint>`，直接打开 NT volume后只通过 parent handle相对枚举/打开 data root segments、`chartskin`、package及全部子项；使用 `OBJ_DONT_REPARSE`、`FILE_OPEN_REPARSE_POINT`、`FileIdExtdDirectoryInformation`、volume serial/file ID、link count和metadata锁定物理身份。SUBST、mapped/remote drive、shadow/device alias、reparse/junction、未由resolver展开成长名的8.3/alternate alias、hardlink、重复identity、unsupported entry和busy writer均fail-closed。
- 所有目录/文件 handle在读取前固定并保持到 pure capsule构造与最终复验完成；枚举在扩充managed集合前执行取消/entry budget，文件流按最多1 MiB分段读取。返回前再次校验每个 pinned node、每级 authority link、全部 directory inventory及package root，并在发布 capsule前先释放全部handle；取消、typed failure、意外异常或dispose异常均继续回收handle与provisional capsule。该保证刻意窄于filesystem transaction：只保证发布bytes来自held identity，且final validation前观察到的变化会拒绝。
- capture focused **47/47**（36项deterministic fake + 11项真实Windows，0 skipped），与preflight/capsule合并 **167/167**。真实门覆盖当前x64进程native ABI layout、嵌套/空包、native entry budget、busy writer、hardlink、package/nested junction、resolver对现存8.3路径的长名规范化、capture期间反向写入与文件rename被share mode阻止，以及新增child后final inventory typed reject；fake另锁request发出后package在首次open前消失的`PackageUnavailable`与零读取/清理。未执行真实SUBST命令级集成；strict target classifier与fake alias合同证明其fail-closed分支。
- core skin **224/229**，5项失败仍为1项Argon默认皮肤旧期待与4项已删除Osu ruleset archive fixture；mania skin **182/182**、BMS skin **583/583**。`osu.Desktop.slnf --no-restore` Release Rebuild **0 error / 11 known warnings**，保留9条MessagePack 3.1.3 `NU1902`和BMS tests既有`CS8600`/`CA2007`。生产/测试targeted format verify通过；文档健康检查通过 **123个Markdown / 969个相对链接 / 31个memory wiki链**，仅保留mainline plan数字比值的既有非失败提醒，`git diff --check`通过。
- 测试只访问deterministic fake及系统临时目录；junction定点创建/清理，生产Realm、`chartskin/`、用户目录、网络与GUI零访问。本节落地时全仓除tests外无`SkinManager`/production managed folder factory/选择消费方；其后第四刀已接`InstantiationInfo`/选择，但external source、scanner owner、mutation与atomic reload仍未完成。

### `SV1-2` 第二刀：ruleset-neutral immutable package revision capsule 合同

- shared core 新增纯 post-capture `SkinPackageRevisionCapsule`：输入是 capture producer 提供的稳定逻辑 file/directory entries，不含 filesystem path、Storage 或 authority；本节当时没有 producer，其后已有第三刀managed native producer与第四刀production exact-capsule factory/选择消费方。它不是 directory inventory/capture service，不证明 containment、no-follow、physical identity、TOCTOU 或读取期间稳定性。
- factory 将 `\` 统一为 `/` 并做 Unicode NFC，按 Windows case-insensitive 语义拒绝 duplicate、绝对/穿越/ADS/歧义设备名、file/directory 层级冲突和 entry/file/depth/name/raw-byte 预算；精确读取 declared length，以版本 domain + 规范 UTF-8 名 + 长度 + per-file SHA-256 生成确定性 whole-package content revision。空目录计入 entry/depth budget，但不影响 content revision。
- capsule 独占 defensive byte backing，metadata collection 真只读，resource view 非 owning；`Get`/`GetStream` 返回副本，已有 stream 不受 capsule 退役影响。预期 source failure 返回 typed reason，取消传播；当前 buffer、此前已接管 buffer 与 capsule 退役均有清零证明，失败/取消不返回半成品。resolver 同时只复用等价的 Windows segment validator，未扩大 preflight authority。
- focused **66/66**，preflight + capsule 合并 **120/120**；core `osu.Game.Tests.Skins` **177/182**，5 项失败仍是恢复基线同名的 1 项 Argon 旧期待和 4 项已删除 Osu ruleset archive fixture，无新增失败；mania `FullyQualifiedName~Skin` **182/182**、BMS `FullyQualifiedName~Skin` **583/583**。`osu.Desktop.slnf --no-restore` Release Rebuild **0 error / 11 known warnings**，保留 9 条 MessagePack 3.1.3 `NU1902` 与 BMS tests 既有 `CS8600`/`CA2007`。
- 独立代码、安全和测试终审在补合成 parent 即时预算、合法短读、全结构/预算零 source open、当前/历史 backing 清理、默认预算固定和 NFC lookup 后为 blocker/major **0/0**。剩余 minor 只涉及部分测试对 `IList`/buffer identity 的实现耦合与未穷举同一异常分支，不阻塞 pure capsule 合同。
- 生产与测试改动文件的 targeted format verify 通过；文档健康检查通过 **122 个 Markdown / 967 个相对链接 / 26 个 memory wiki 链**，仅有 mainline plan 数字比值的既有非失败提醒；`git diff --check` 通过。
- deterministic 测试全为纯内存 fake；生产 Realm、`chartskin/`、用户目录、网络与 GUI 零访问。下一刀仍须实现 Windows fixed-handle、handle-relative no-follow capture，闭合 8.3/SUBST/junction/hardlink alias、entry/final identity 与读取/枚举竞态；之后才允许 exact-capsule folder factory、选择、scanner、mutation 和 atomic reload 接入。`SV1-2`、G1、Skin V1 与产品交付均未因此完成。

### `SV1-2` 第一刀：folder authority/path lexical preflight 合同

- shared core 新增无生产消费者的 `SkinFilesystemStorageResolver`，把 schema 56 的字段组合闭合为 Realm `.osk`、`chartskin/<direct-child>` managed folder、只读 drive-letter-qualified Windows external folder或 typed invalid。该语法不证明物理本地盘，mapped drive/SUBST/final identity留给后续 gate。Realm/内置无 folder 记录不触碰 Storage；folder 强制 `Files.Count == 0`，protected/fixed-ID/DeletePending 拒绝，normalised absolute/relative path不进入安全字符串。
- managed 路径只接受 direct child，拒绝 root/sibling-prefix/nested/traversal/ADS/尾点尾空格/Windows 设备名；external 拒绝 relative、UNC/device、盘符根，并拒绝与 managed `chartskin` namespace exact/ancestor/descendant 重叠。现存路径逐段做 data root/managed root/package/external ancestor reparse preflight，缺失、非目录、权限/IO、过长路径均返回稳定 reason。
- 该类型的 XML/公开属性已明确收窄：这是检查当时的 lexical/reparse preflight，不是 canonical/final physical identity、mutation token、包内容/`InstantiationInfo`/选择资格或 TOCTOU 安全打开。8.3/SUBST/alias、真实 junction 集成、包内条目、no-follow inventory 与 immutable revision capsule留给下一刀；接 `SkinManager`/`NativeStorage`/scanner/mutation 前不得直接使用 normalised path 执行 I/O。
- focused **54/54**；core skin aggregate **111/116**，5 项失败仍是恢复基线同名的 1 项 Argon 旧期待和 4 项已删除 Osu ruleset archive fixture，无新增失败；mania `FullyQualifiedName~Skin` **182/182**、BMS `FullyQualifiedName~Skin` **583/583**。Release Rebuild **0 error / 11 known warnings**，保留 9 条 MessagePack 3.1.3 `NU1902` 与 BMS tests 既有 `CS8600`/`CA2007`；targeted whitespace/style 与 CRLF、`git diff --check` 均通过。
- 三路独立终审在补 external/managed namespace 冲突、移除 mutation 授权误称、收窄 canonical/no-follow 措辞、补 external bytes+mtime+`SkinInfo` 零 mutation和显式 typed reason 后为 blocker/major **0/0**。测试只使用隔离临时目录与注入式 attributes probe；代码没有 production call site，未访问生产 Realm、`chartskin/`、用户皮肤目录或网络，未启动 GUI。`SV1-2`、G1 与 Skin V1 均未因此完成。

### `SV1-1` managed `.osk` BMS critical 长条身与安全宽度纵切

- managed source-bound provider 已扩到 critical、不可 `Suppress` 的 `LongNoteBody`，只消费 decoder-time accepted native `[Bms] NoteImage{lane}L` / `NoteImageSL` / `NoteImageS2L`；静态图与连续 `name-0`、`name-1`…编号帧均沿用 60 FPS、containment、解码前后预算、后台 preparation、取消与 stale result 处置。ordinary note、head、body、tail 四组件至此闭合自动、合同、安全与回退 gate。
- 新增唯一共享 `LongNoteBodyWidth` scalar resolver：默认 `0.5775`，只接受 finite 且 `0 < width <= 1`；absent、non-finite、小于等于零和大于一均以稳定 typed reason 拒绝并逐字段回落默认。有效 body + 非法 width 继续发布同组件默认宽，只有 body 资源整体失败才 `Inherit`；selected 坏 body 不能由低层裸同名文件或裸 width 拼件，低层自己的完整 body 组件仍可接管。
- body texture/frames 与 resolved width 现绑定同一个 exact parsed `skin.ini` 内容身份和 package revision进入 prepared material并一起发布，renderer 不再反查 aggregate width。managed/default body 共用真实 Idle/Holding/Broken 视觉宿主，保持 active `0.8`、broken `0.32` 与 80ms tint/fade；异步首次挂载立即投影 hold 当前态，HCN regrab 继续只投影 gameplay authority。未改 `DrawableBmsHoldNote` gameplay state、body 拉伸/裁剪或 LN/CN/HCN 规则。
- 验证：产品 fixture **94/94** 连续三轮全绿，合并态 focused **326/326**，BMS full **1456/1456**，`osu.Desktop.slnf` Release **0 error / 11 known warnings**；保留 9 条 MessagePack 3.1.3 `NU1902` 与 BMS tests 既有 `CS8600`/`CA2007`。改动文件 targeted format verify 通过，文件内既存 `IDE1006` 命名债未冒充本切问题；独立终审 blocker/major **0/0**。
- 新能力登记为集中视觉项 `V-004`，与 `V-001`～`V-003` 一样仍待用户统一签收；按用户“不操控电脑、集中反馈”的协作方式，本切未启动 GUI。当前只能写成“`SV1-1` 首个 Note/LN 产品纵切自动闭环”，不能写成 `SV1-1` 完成、产品交付或 release gate 通过。
- 工程依赖允许下一门转入 `SV1-2`。当前成功 preparation cache 不感知同一 `BmsLegacySkin` 实例内的原地 source revision 变化；它不会混合或发布过期 material，而会安全保留旧视觉/回落并要求实例重建，须在 `SV1-2` 作为原子 reload 风险处理。剩余 optional slot 不再沿私有逐件 C# provider/display 扩张，后续由 shared scene/runtime 接管。

## 2026-07-16

### `SV1-1` managed `.osk` BMS 长条尾静态图/编号帧动画纵切

- source-bound provider 从 ordinary note/head 扩到 optional `LongNoteTail`，只消费 decoder-time accepted native `[Bms] NoteImage{lane}T` / `NoteImageST` / `NoteImageS2T`。static 与连续 `name-0`、`name-1`…编号帧固定 60 FPS；descriptor、materializer、exact package revision、containment、大小写冲突、raw/image/pixel/frame/texture/component/package 预算、后台 preparation、取消与 stale result disposal 全部复用既有合同，未改预算数字或 importer gate。
- 真实 `DrawableBmsHoldNote → DrawableBmsHoldNoteTail` 已改走 `BmsAsyncNoteDrawable`，body 保持原路径。未声明、空值、缺失、损坏、断帧、越界、authority 冲突或超预算均诊断后 `Inherit`；producer 不返回 `Suppress`，程序化透明 tail 也不解释为 `Suppress`。protected `OmsSkin` tail 强制 `Alpha=0` 且不查询 aggregate 同名纹理；selected 坏 tail 不会借低层裸同名文件补件，但低层自己的完整 accepted declaration/resource 可作为完整组件接管。beatmap-local 直接 drawable 仍优先，head/body、LN/CN/HCN、判定/计分/滚动/裁剪、22.5px tail host、layout、G1 与 event runtime 均未改。
- 产品 fixture 从 **39** 扩到 **60** 个 case并连续跑三轮全绿：覆盖 static tail、真实完整 hold 的动画推进/循环、7K normal/scratch、14K `S2T`、A→B 2→3 帧与旧视觉保留、注入式 beatmap provider 顺序、坏 tail 与有效 note/head 隔离、protected 透明链底、低层裸同名防串/完整组件接管、authority/大小写/资源失败、代表性帧预算、逐组件隔离及后台取消/过期结果。注入式 `ISkin` 只证明 provider order，不是公开 beatmap-local 作者格式。
- 验证：合并态 BMS skin/runtime focused **271/271**，BMS full **1401/1401**，`osu.Desktop.slnf --no-restore` Release **0 error / 11 known warnings**；保留 9 条 MessagePack 3.1.3 `NU1902` 与 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。独立终审 blocker/major **0/0**；文档健康检查为 **120 个 Markdown / 964 个相对链接 / 22 个 memory wiki 链**，`git diff --check` 通过。本切未改 shared `osu.Game` ABI、mania compatibility 或 fallback authority，故未另跑 core/mania 产品测试；Release 已编译相关工程。
- 新能力登记为集中视觉项 `V-003`，与 `V-001`/`V-002` 一样只能称“实现/自动 gate 通过，视觉待验收”，不得计作产品交付、`SV1-1` 完成或 release gate 通过。按用户“不操控电脑、集中反馈”的协作方式，本切未启动 GUI。下一刀经依赖审计冻结为 critical `LongNoteBody`：先建立可复用标量几何合法域（`LongNoteBodyWidth` 默认 `0.5775`，只接受 finite 且 `0 < width <= 1`），让 body 素材与 width 同 revision 发布，并复用现有 Idle/Holding/Broken 状态宿主；不提前扩成 `SV1-3` layout snapshot。

### `SV1-1` managed `.osk` BMS 长条头静态图/编号帧动画纵切

- selected managed `.osk` 的 source-bound note provider 从 ordinary note 扩到 critical `LongNoteHead`，只消费 decoder-time accepted native `[Bms] NoteImage{lane}H` / `NoteImageSH` / `NoteImageS2H`。slot key 与 descriptor 现包含 element，materializer 在同一 immutable package revision 内枚举 ordinary/head canonical lanes；连续 `name-0`、`name-1`…仍固定 60 FPS，并复用现有 raw/image/pixel/frame/texture/component/package 预算、containment、大小写冲突和 decode 前后验证，未改预算数字或 importer gate。
- 真实 `DrawableBmsHoldNote → DrawableBmsHoldNoteHead` 已改走 `BmsAsyncNoteDrawable`；初次加载保持可见 protected head，A→B 准备期间保留旧视觉，只发布当前 generation/revision 的完整结果。selected 声明为空、缺失、损坏、断帧、越界、authority 冲突或超预算时回落到可见默认头；protected fallback 不再反查 aggregate 同名纹理，避免低层裸文件补齐 selected 坏声明，但仍保留独立 colour/palette rescue。body/tail 继续原 `SkinnableDrawable` 路径，LN/CN/HCN、22.5px head host、长条身宽/状态/裁剪、layout、G1、manifest、event runtime 与作者 `Suppress` 均未改。
- 产品 fixture 从 **28** 扩到 **39** 个 case：新增静态 head、真实完整 hold 的动画推进/循环、7K scratch、14K `S2H`、Note/head A→B 2→3 帧、坏 head 与有效 ordinary note 隔离、beatmap-local provider-order、低层同名防串、authority/filename conflict、Note/head async 换源及 body/tail 不拦截。beatmap-local 仍是注入式 provider fixture，不是 `WorkingBeatmap` 作者格式。
- 验证：合并态 BMS skin/runtime focused **248/248**，BMS full **1378/1378**；`osu.Desktop.slnf` Release **0 error / 11 known warnings**，即 9 条 MessagePack 3.1.3 `NU1902` 与 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。本切没有改 shared `osu.Game` skin ABI、mania compatibility 或 fallback authority，故未另跑 core/mania 产品测试；Release 已编译 core、mania/BMS 与两个 test project。
- 稳定性暂态如实保留：产品 fixture 前两轮 **39/39**；第三轮发现真实 hold 仅放在 `+1s` 后会进入判定/停更，导致已到 frame 1 后等不到 frame 0。把测试观察对象移到 `+60s` 后，精确用例 **1/1** 与完整 fixture **39/39** 再过，没有放宽等待；这是 fixture 时窗修正，不是产品加载失败。
- 新可见能力登记为集中视觉项 `V-002`，与 `V-001` 一样只能称“实现/自动 gate 通过，视觉待验收”，不得计作产品交付、`SV1-1` 完成或 release gate 通过。按用户“不操控电脑、集中反馈”的协作方式，本切未启动 GUI；下一刀经只读依赖审计冻结为 optional `LongNoteTail` 静态图/编号帧动画，只做 `Provide/Inherit`，透明链底不得冒充作者 `Suppress`。

### 普通短键动画 gate 的隔离自动 runner 与 staging 安全闭环

- 新增 `RunBmsNoteAnimationVisualGate.ps1` 和 exact scene runner：只打开指定 visual test，真实 `SkinManager` 导入内存 good/broken 包，经 `RulesetSkinProvidingContainer → BmsAsyncNoteDrawable` 自动循环 3 轮；场景等待 60 帧/默认回落完全加载，成功停留 3 秒。runner 仅 `--exact-test` 进入严格模式，普通 TestBrowser 参数保持兼容；加载/步骤/watchdog 失败为 1，成功为 0，提前关闭为 3。
- exact host/data storage 均由内部固定前缀 + GUID 生成；host 路径必须是规范 AppData 直系子目录，清理逐层非递归且遇任意 reparse/junction 即拒绝，不会跟随外部目标。场景在普通 desktop TestBrowser 缺少 exact marker 时会在导入前 fail-closed，headless NUnit 保持可跑。手工 `import-staging` 只覆盖两个精确已知副本，保留无关文件，并拒绝 staging 根/目标 reparse、文件/目录冲突；确定性原件和 SHA 清单不被消费。
- 自动验证：产品/生成/staging/scene/runner safety 合并 focused **53/53**，root generator **1/1**，两个 staged/source SHA-256 相同；非法/缺值 exact CLI 均 exit 1，新增 AppData host 残留 0。Release 首轮发现 linked runner 生成代码引入 16 条 `CS0436`，随后将 exact 类型限制为 executable test project 条件编译并保留 `osu.Game` 原 legacy runner API，最终恢复 **0 error / 20 warnings**；只保留既有 `NU1902`、`CS8600`、`CA2007`。
- 本切只改测试工具、fixture、脚本、编译边界与文档，不改皮肤生产 runtime、fallback authority、Realm、用户皮肤或网络。按用户“不操控电脑”的要求，最终代码未重新开窗；自动 scene 与先前代理观察均不能替代 `V-001` 用户视觉签收。

### 视觉验收调度改为集中签收，下一切片冻结为 BMS 长条头

- 用户决定不再用逐组件 GUI/实机签收串行阻塞开发：切片通过自动、合同、安全与回退 gate 后即可按依赖继续，视觉项统一进入[集中清单](../../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)。待签收只能记为“实现／自动 gate 通过，视觉待验收”，不得计作产品交付、`SV1` 阶段完成或 release gate 通过；只有视觉结论实际影响下一实现时才暂停。
- 首项 `V-001` 仍覆盖 managed `.osk` BMS 普通短键编号帧动画、选择切换与 selected 坏包回落，2026-07-14 静态恢复结论不能复用。下一最小安全切片冻结为 critical `LongNoteHead` 的静态图与连续编号帧动画；只复用精确 package revision、预算、异步发布和逐组件回落，不扩 body/tail、`Suppress`、layout、G1 或 event runtime。

### BMS 普通短键编号帧动画的确定性手工门素材

- 新增根目录 `GenerateBmsNoteAnimationManualGate.ps1` 薄包装和测试侧 generator，按固定像素、PNG 参数、ZIP entry 顺序/时间戳/压缩方式生成 good/broken `.osk`、静音 7K `.bme` 与 SHA-256 清单；不读取 SimpleTou、用户皮肤、生产 Realm、`chartskin/` 或网络。
- good 包为 lane 1 提供 60 张同尺寸编号帧；broken 包声明同一 slot 但只有 frame 1，稳定触发 selected 单槽回落。真实 package 产品链新增两项后 `BmsManagedPackageNoteProductTest` **28/28**，双生成逐文件确定性/ZIP/PNG/7K chart smoke **1/1**；根脚本实际生成成功。只保留既有 MessagePack `NU1902` 与 BMS tests `CS8600`/`CA2007`。
- 手工说明见[专页](../../other/SKIN_BMS_NOTE_ANIMATION_MANUAL_GATE.md)。它只验 managed 编号帧、选择切换与 selected 坏包回落，不改变用户实机待确认结论。原 26/26 的 beatmap-local 用例是私有 provider-contract fixture，不是 `WorkingBeatmap` / 真实 `chartbms/` 集成，禁止用本素材把 beatmap-local 或完整 Skin V1 写成实机通过。

### 文档健康治理完成：当前态、未来顺序、稳定合同与历史重新归位

- 将 `DEVELOPMENT_STATUS.md` 从逐切验证账压缩为当前能力、gate、唯一最新产品验证、本次文档治理、仍影响决策的风险和下一门；历史验证仍由本文件按日期保存。
- 将 `DEVELOPMENT_PLAN.md` 收敛为治理完成后的活动门、`SV1-1` 未完纵切及 `SV1-2`～`SV1-7` 的依赖/验收；移除已完成 `SV1-0` 细史、重复测试数字和第九至二十切历史快照。
- `README.md` 降为兼容路由；`TECHNICAL_CONSTRAINTS.md` 保留稳定 Skin V1 合同，并把 GN/WN、Floating、预开谱 delay 与 mod 记忆的仍有效合同收成短条，避免它们只埋在旧实现史里。
- **产品事实与 gate 未变**：新增可见功能仍为 1，首个编号帧动画自动 gate 已过、用户实机待确认，Skin V1 整体仍不可用。此次仅改文档，未改代码，未运行产品测试或 Release。
- **文档验证**：Windows PowerShell 5.1 与 PowerShell 7 均运行 `CheckDocumentation.ps1` 通过（118 个 Markdown、946 个相对链接、22 个 memory wiki 链），`git diff --check` 通过；P1-J/P1-L 两处历史诊断路径已脱敏且未改变诊断含义。

### 当前事实、作者文档与 memory 同步；移交下一轮健康治理

- **产品事实未变**：`SV1-0` 自动/数据/2026-07-14 实机三门全过；首个玩家可见普通短键编号帧纵切自动 gate 已过，Skin V1 新增可见功能仍为 **1**，新动画实机仍待确认；`SV1-1` 未完成、`SV1-2` 仅 early carrier、`SV1-3`～`SV1-7` 未实现，Skin V1 整体不可用。
- **执行暂停点**：实现停在 `d1ea483`。下一新对话只做文档与 memory 健康治理；治理不改代码、产品合同或 gate，也不算产品功能。治理完成并重新冻结执行门后，才先闭合新动画实机 gate，再由产品决定下一组件。
- **事实纠偏**：把“当前选中的 managed `.osk`”统一改成条件式能力——用户选中已导入 managed 包时该能力生效；schema 56 清点结束时的实际当前选择仍是 protected OMS。测试矩阵按改动 authority 分层，补记首个产品纵切因未改 shared `osu.Game`、mania compatibility 或 fallback authority 而未重跑 core/mania。
- **派生文档与隐私**：`SKINNING.md` 补齐 ordinary-note 的真实范围、固定 60 FPS、`Keymodes` 仅提示、`Provide/Inherit` 窄生产例外及当前 `OmsSkin`／最终 `oms-simple` 边界；恢复文档与 memory 移除本机绝对路径，只保留脱敏 authority/归档描述。
- **边界**：本次没有代码、runtime、生产 Realm、`chartskin/`、用户皮肤目录、网络或其它用户数据访问/写入；没有运行产品测试或 Release build，完整保留 2026-07-15 的 26/283/1333、Release 0 error / 20 warnings 与既有告警记录。
- **文档验证**：hidden-aware 全仓 Markdown 检查为 **119 个文件 / 937 个相对链接 / 0 断链**；`git diff --check` 通过，新增行隐私扫描 **0 命中**。未使用 `NoWarn`，也没有把文档同步写成 runtime 自动 gate。

## 2026-07-15

### `SV1-1` 首个玩家可见纵切：managed `.osk` BMS 普通短键 numbered-frame animation

- **玩家功能**：当用户选中已导入的 managed `.osk` 时，它可用 osu 社区式 `name-0`、`name-1`…编号帧驱动 BMS 普通短键动画；既有静态 `NoteImage` 属恢复基线，本次只把编号帧动画计作新增可见功能。
- **可玩回落**：单个短键素材缺失、损坏、越权或超预算时只让该 slot 沿既有选择链继续回落，不会让短键消失；有效 beatmap-local 视觉仍高于 selected package，跨 package 同名素材不得拼接。
- **需求对应**：为满足已声明的资源隔离、安全预算与 update thread 不做文件 IO/解码要求，素材声明和帧序列绑定到精确 package revision，并在后台准备完成后发布；换肤期间保留现有视觉，过期结果不会覆盖新选择。
- **验证**：产品自动验收 **26/26**、相关 focused **283/283**、BMS full **1333/1333**、`osu.Desktop.slnf` Release **0 error / 20 warnings**，独立终审 blocker/major **0/0**；Markdown **119 文件 / 934 相对链接 / 0 断链**。保留 9 条 MessagePack 3.1.3 `NU1902` 在 restore/build 重复显示及 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。
- **格式证据**：最终 whitespace 首轮 verify 精确报告三个改动文件的局部混合行尾；工程 formatter 只统一行尾后，source/test 两组 verify 均 exit 0。每次 formatter 仍显示既有的泛化 workspace-load warning，未将它隐去或写成静默无告警。
- **数据与实机**：测试只使用隔离 headless 临时存储；生产 Realm、`chartskin/`、用户皮肤目录及网络零访问、零写入。`SV1-0` 静态恢复实机清单已通过，但本次新增编号帧动画仍须用户单独实机确认。
- **边界**：未交付 LN、mania compatibility、完整三态/布局、安全 G1、scene/event/script、`oms-simple.osk`/`oms-complex.osk` 或整包原子重载；程序化 `OmsSkin` 仍是迁移链底，Skin V1 整体仍不可用。本任务在该功能提交后停止。

### `SV1-1` 第二十个合同切片：native `[Bms]` exact geometry decoder-time accepted provenance

- native BMS configuration/decoder 为当前十二项 geometry lookup 建立 exact、case-sensitive closed catalog 与 decoder-time accepted-value sidecar：`PlayfieldWidth/Height`、normal/scratch lane width/spacing、hit-target height/bar/line/glow、bar-line height 与 long-note body width；`HitTargetVerticalOffset` 明确排除。accepted value 完全沿用既有 invariant `float.TryParse(NumberStyles.Float)`，因此正负号、小数、指数、`-0`、`NaN`、正负 `Infinity`、overflow→Infinity 与 underflow→带符号零都保留为未验证 source fact；malformed 不声明且不擦除先前成功值，valid duplicate 与既有 bucket/merge 时序不变。
- 新增 BMS-internal、source-specific、immutable geometry snapshot/factory。factory 只读 private sidecar，public mutable `Geometry` 的手工 forge 或 decode 后 overwrite/remove/clear/late-add 均不能伪造、擦除或改变 provenance。既有 `geometry_keys`、parser 与 production compatibility view 保持；逗号 composite raw key 即使经 `Enum.TryParse` 折叠到已定义 lookup，也只写 public view而不进入 exact sidecar。
- focused geometry **49/49**、decoder **8/8**、BMS skin focused **381/381**、BMS full **1237/1237**；`osu.Desktop.slnf` Release Rebuild **0 error / 20 warnings**。独立源码终审 **0 blocker / 0 major / 0 minor**，`git diff --check` 通过。formatter/verify 通过，仅保留泛化 workspace-load warning；最终没有新增告警，既有 MessagePack `NU1902` 与 BMS tests 告警均未隐藏或误报为本切回归。
- 本切不是 finite/positive/range/cross-field/screen-space validation，不是 layout descriptor/solver、neutral config/author manifest/wire ABI，也没有 production lookup/fallback/renderer/`SkinManager`/reload wiring。未访问或写入生产 Realm、`chartskin/`、用户皮肤目录或网络；产品新可见功能仍为 **0**。
- 路线整体仍为：`SV1-0` 已完成；`SV1-1` 完成第二十个合同/fixture 地基但整体仍进行中；`SV1-2` 只有 early carrier；`SV1-3`～`SV1-7` 未实施，Skin V1 仍不可用。下一步改按可实机演示、可验收的纵向切片推进，不再把 source-provenance 横向扩面本身当作产品进度。

### `SV1-1` 第十九个合同切片：native `[Bms]` exact colour decoder-time accepted provenance

- native BMS configuration/decoder 为当前生产 colour lookup 已消费的二十二个 exact、case-sensitive source key 建立 closed catalog 与 decoder-time accepted-value sidecar：四项 note colour、五项 lane/background/divider colour、六项 normal/scratch hit-target colour、major/minor barline、三项 lane-cover，以及 playfield backdrop/baseplate。accepted value 完全沿用既有 RGB/RGBA byte parser：三分量补 alpha 255，四分量保留 alpha（含 0），正号、leading zero 与 `-0` 等既有 `NumberStyles.Integer` 行为不在本切重解释；malformed 不声明且不擦除先前成功值，valid duplicate、重复 bucket、pending-before-`Keymode` 与同一 decoder repeated `Parse` 继续既有 merge/last-accepted 语义。
- 新增 internal、source-specific、immutable 的 `BmsGameplaySkinBucketColourSnapshot`/factory；缺目标 keymode bucket 为 outer `Absent`，显式空 bucket 为 outer `Declared` 且二十二项全 `Absent`。factory 只读 private sidecar，不从 public mutable `Colours` 反推，因此手工 forge 与 decode 后/factory 前或 snapshot 后的 overwrite/remove/clear/late-add 都不能伪造、擦除或改变 provenance。snapshot 只提供 native keymode 与 closed field declaration query，安全字符串不展开 colour/source key；它不是 neutral config、slot、manifest 或 wire ABI。
- 保留一项重要 compatibility quirk：当前区分大小写的 `Enum.TryParse` 可将某些逗号 composite key 折叠为已定义 lookup；decoder 仍把这类成功颜色写入 public `Colours`，因此 production compatibility 行为不变，但 exact classifier 拒绝把 composite 提升为 accepted declaration。fixture 同时证明 exact declaration 后的 composite 可以覆盖 public view，却不能改变 private provenance；本切没有清洗任意其它 enum/string lookup，也没有改 `BmsLegacySkin` production query。
- 验证暂态如实记录：首次 colour focused 为 **27/28**，唯一失败是 fixture 对 LINQ iterator 误用 `Has.Count`，并非 source/production 缺陷；修正为物化/枚举断言并扩展 byte parser、repeated `Parse`、composite 与 mutation 矩阵后为 **31/31**。BMS skin focused **332/332**，BMS full **1188/1188**；`osu.Desktop.slnf` Release Rebuild **0 error / 20 warnings**，仍仅 9 条 MessagePack 3.1.3 `NU1902` 在 restore/build 重复显示与 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。targeted formatter/verify 通过，只保留泛化 workspace-load warning；独立终审为 **0 blocker / 0 major / 0 minor**，`git diff --check` 通过。
- 本切没有访问或写入生产 Realm、`chartskin/`、用户皮肤目录及网络；没有新增主题化颜色、默认视觉或私有 renderer 路线，也没有实现 package/materializer、shared codec、geometry validation/full layout descriptor、结构化 malformed diagnostics、production revision/lookup/renderer/`SkinManager` wiring、真实 fallback/reload、G1、scene/script、`oms-simple`/`oms-complex` 或程序化 `OmsSkin` authority 切换。
- 路线整体仍为：`SV1-0` 已完成；`SV1-1` 完成第十九个合同/fixture 地基但整体仍进行中；`SV1-2` 只有 early carrier；`SV1-3`～`SV1-7` 未实施，Skin V1 仍不可用。后续仍须按证据继续 native geometry/resource provenance、shared validation/config codec、full layout、package authority/materialization 与生产接线，不能把本切 accepted declaration 写成可用 Skin V1。

### `SV1-1` 第十八个合同切片：legacy mania `NoteBodyStyle` decoder-time accepted provenance

- shared legacy mania configuration/decoder 为 exact、case-sensitive `NoteBodyStyle` 增加 decoder-time accepted-value sidecar；decoder 仍使用既有 `Enum.TryParse<LegacyNoteBodyStyle>(string, out ...)`，没有加入 `Enum.IsDefined`、canonical numeric 或 flags 限制。四个命名值保留现行为；undefined numeric（fixture 覆盖 `1`、`99`、`-1`）、非 canonical numeric（`+2`、`02`）和逗号 composite 只要被现 parser 接受，就原样保存 parsed enum value。大小写变体、`Repeat`、未知名称和空值不声明，也不覆盖此前成功值；valid duplicate 继续 last accepted。pending-before-`Keys`、换 section/EOF 丢弃、malformed `Keys` 沿用旧 current bucket 与 duplicate `Keys` 写入 discarded bucket 的既有行为均由 fixture 固定，没有借本切修 parser。
- 新增 public、sealed、fixed-property 的 `LegacyManiaGameplaySkinBucketNoteBodyStyleSnapshot` 与 factory。缺少目标 `Keys:` bucket 为 outer `Absent`；显式 bucket 无有效字段为 outer `Declared` + inner `Absent`。factory 只读 decoder-time `AcceptedNoteBodyStyle`，不从公开可变 `NoteBodyStyle` 字段反推，因此手工 forge，以及 decode 后/factory 前的 erase、alter 和 snapshot 后 mutation 都不能伪造、擦除或改变 provenance。`LegacySkin` 按全局 Version `< 2.5` 选择 `Stretch`、否则选择 `RepeatBottom` 的 effective default 明确不算 declaration；snapshot 安全字符串只返回类型名。
- 验证：新增 focused **26/26**；shared legacy config aggregate（`GameplaySkinConfigurationDeclaration`、`LegacyManiaGameplaySkinBucket*`、`LaneColourSnapshot`、`LaneResourceAcceptedProvenance`、`LegacyManiaSkinDecoder`）**126/126**；mania `FullyQualifiedName~Skin` **182/182**；BMS `FullyQualifiedName~Skin` **301/301**；BMS full **1157/1157**。mania full **827/831** 仍仅恢复基线同名 4 个 `TestSceneAutoGeneration` HoldNote frame-count 期待失败；core skin focused **57/62** 仍仅 1 项 Argon 旧期待与 4 项已删除 ruleset archive。`osu.Desktop.slnf` Release Rebuild **0 error / 20 warnings**；保留 9 条 MessagePack 3.1.3 `NU1902` 在 restore/build 重复显示，以及 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。targeted formatter 与 `--verify-no-changes` 均 exit 0，只保留泛化 workspace-load warning。文档终审初次为 **0 blocker / 0 major / 1 minor**，指出 STATUS 误沿用第十六切 BMS full **1146/1146** 作为“上一切”；修正为第十七切 **1157/1157** 后复核为 **0 blocker / 0 major / 0 minor**。首次合并链接/隐私 checker 在进入 PowerShell 前因 JavaScript raw template 中的 `${path}` 被宿主插值而失败，属于无仓库影响的无效证据；移除该宿主插值后，hidden-aware、root-safe Markdown checker 为 **121 个文件 / 936 个相对链接 / 0 断链**，工作区绝对用户路径扫描为 **0 命中**，working-tree `git diff --check` 通过。
- 本切只闭合 legacy mania `NoteBodyStyle` 的 decoder→factory mutable-field provenance window，不验证 enum 是否属于未来 V1 作者允许集，也不新增任何 native BMS geometry/colour/resource field。没有改 tokenizer、`LegacySkin`/mania/BMS consumer、renderer、`SkinManager`、nullable `ISkin` ABI、candidate/fallback、程序化 `OmsSkin` authority 或 package/reload；没有访问或写入生产 Realm、`chartskin/`、用户皮肤目录及网络。
- 未实施资源 containment/存在/解码/预算/animation sequence validation/materialization、geometry 合法域/full layout descriptor、arbitrary colour/resource schema、shared codec、production revision/event/wire、G1、scene/script、`oms-simple`/`oms-complex` 视觉与 canonical fallback 切换。`SV1-1` 仍未整体完成，Skin V1 仍不可用；后续先分组封闭 native BMS canonical fields，再按依赖进入资源 materializer/动画与 geometry validation，不能把任意 compatibility dictionary 升级成作者 ABI。

### `SV1-1` 第十七个合同切片：mania/BMS lane-resource decoder-time accepted provenance 双侧闭合

- shared legacy mania configuration/decoder 为 note、LN head/body/tail、key up/down 六类逐 lane 资源增加 per-field/per-source-column accepted-string sidecar；只接受区分大小写的 `NoteImage{n}`、`NoteImage{n}H/L/T`、`KeyImage{n}`、`KeyImage{n}D`，其中 `{n}` 必须是 `0..Keys-1` 的 canonical ASCII decimal。native `[Bms]` configuration/decoder 同样只把现有 per-lane regex 中精确对应六类字段的 prefix+suffix 组合写入 sidecar，并原样保留 `\d+`、`S`、`S2` raw token，不做 numeric normalisation。显式空资源名仍为 `Declared("")`，valid duplicate 与重复 `[Bms]` bucket merge 继续 last accepted；unknown/non-canonical/composite field、legacy 越界/非 canonical index、BMS 非法 token 与 null resource 在任一 compatibility/sidecar 写入前拒绝。
- `LegacyManiaGameplaySkinLaneResourceSnapshotFactory` 与 `BmsGameplaySkinLaneResourceSnapshotFactory` 已改为只读各自 decoder-time sidecar，不再从 public mutable `ImageLookups` 反推 declaration；decode 后、factory 前对两侧 compatibility dictionary 内容的 add/overwrite/remove/clear、legacy dictionary 的整表 reassign，以及手工构造 configuration 后直接注入 exact-looking key，都不能伪造、擦除或改变 snapshot。9K 仍按当前 production-compatible raw `0..8` 投影到 stable K1..K9，raw `9`、`01` 或 Unicode decimal token 即使被 `[Bms]` decoder 接受也不会被静默归一化或投影；14K 继续固定 `S`、global numeric `1..14`、`S2`，没有改变 stable lane topology 或 BMS native authority。
- 兼容双写保持不变：两侧 exact key 仍同步写入原 `ImageLookups` 供现有 production lookup 消费；legacy 其它 broad/prefix-only image key、BMS `NoteImage*`/`KeyImage*` 错配 suffix、`LaneBackgroundImage*`、`LaneDividerImage*` 与 tokenless image key 仍依旧有 dictionary 行为，但不会进入六字段 closed sidecar。未改 tokenizer、pending-before-keymode、duplicate bucket merge、production `LegacySkin`/`BmsLegacySkin` lookup、candidate precedence、stable lane mapping 或 nullable `ISkin` ABI。
- 权威顺序验证为：legacy shared 既有 12 + 新增 9 共 **21/21**；mania relevant factory **6/6**；BMS 既有 candidate 29 + 新增 provenance 11 共 **40/40**；BMS full **1157/1157**。mania full **827/831** 的 4 项仍为恢复基线同名 `TestSceneAutoGeneration` HoldNote frame-count 期待；core canonical `FullyQualifiedName~osu.Game.Tests.Skins` 最终 **57/62**，仍仅 1 项 Argon 旧期待与 4 项已删除 ruleset archive。`osu.Desktop.slnf` Release Rebuild **0 error / 20 warnings**；保留 9 条 MessagePack 3.1.3 `NU1902` 在 restore/build 重复显示及 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。本切未重跑 legacy config aggregate 或 shared gameplay aggregate，不沿用旧数字冒充本切验证。
- 暂态如实保留：首轮源编译出现新增 `CS0128`/`CS0165`，调整局部变量命名/definite-assignment 后消失；随后 nullable 编译新增 `CS8604`，收紧 successful classifier 的 non-null out contract 后消失。BMS fixture 首轮 **10/11** 只因测试把 null token 正确抛出的 `ArgumentNullException` 按精确 `ArgumentException` 断言，拆分期待后为 **11/11**，不是生产缺陷。一次并行 focused 因多个进程争用同一输出命中 `CS2012` 文件锁，属于无效证据；改为顺序执行后上述 focused 全过。core 曾用不一致窄过滤器得到 **27/32**、**52/60** 探测结果，最终以与恢复基线同口径的 canonical filter 重跑为 **57/62**，窄探测不构成新增回归。前十一个 targeted formatter/verify invocation 均 exit 0；终审修正 BMS XML 后的首次 whitespace verify 精确报告 2 处 `ENDOFLINE`，规范化后最终 whitespace/style verify 均通过。累计十六个 invocation 均保留一条泛化 workspace-load warning，其中只有该次 EOL verify 非零。独立终审初次为 **0 blocker / 0 major / 2 minor**，两项均已修正并复核为 **0 blocker / 0 major / 0 minor**。hidden-aware、root-safe Markdown checker 为 **121 个文件 / 936 个相对链接 / 0 断链**，working-tree `git diff --check` 通过。
- 本切仍只闭合 process-local declaration provenance：没有做 containment、文件存在/解码、动画帧、纹理预算、materialization、slot validation、完整 neutral config 或 shared codec 重写，也没有接入 renderer、`SkinManager`、真实 package/fallback/reload、`oms-simple`/`oms-complex` 视觉或切换程序化 `OmsSkin` authority。全程没有访问或写入生产 Realm、`chartskin/`、用户皮肤目录及网络；第十七切仍只是合同/fixture 地基，Skin V1 不可用。下一切不预设实现，先只读审计剩余 `NoteBodyStyle`、resource validation/materialization 与其它 mutable compatibility 入口，再据证据选择最小切片。

### `SV1-1` 第十六个合同切片：legacy mania bucket-global resource-name accepted provenance

- shared legacy mania configuration/decoder 为一个实际 `[Mania] Keys:` bucket 内十三个 exact、case-sensitive、non-column source key 建立 decoder-time accepted-string sidecar：`LightingN`、`LightingL`、`StageLeft`、`StageRight`、`StageBottom`、`StageLight`、`StageHint`、`Hit0`、`Hit50`、`Hit100`、`Hit200`、`Hit300`、`Hit300g`。source semantic mapping 为 `LightingN → ExplosionResource`、`LightingL → HoldNoteLightResource`、`StageLeft/StageRight/StageBottom → LeftStageResource/RightStageResource/BottomStageResource`、`StageLight → KeyFlashResource`、`StageHint → HitTargetResource`、`Hit0/Hit50/Hit100/Hit200/Hit300/Hit300g → MissJudgementResource/MehJudgementResource/OkJudgementResource/GoodJudgementResource/GreatJudgementResource/PerfectJudgementResource`；这些只是 source-specific compatibility 语义，不是 neutral slot/manifest ID。其它 broad-prefix `Hit*`/`Stage*`/`Lighting*` 仍按原兼容规则留在 `ImageLookups`，不会进入 closed sidecar。
- 新增 public、sealed、fixed-property 的 source-bucket snapshot/factory，不提供 raw string-key query 或 dictionary。缺 bucket 为 outer `Absent`；显式空 bucket 为 outer `Declared` 且十三字段全 `Absent`；显式 `Key:` 空值为 `Declared("")`，valid duplicate 取 last accepted。保存值是 `SplitKeyVal` trim 后、尚未 `CleanFilename` 或验证的字符串；内部冒号与引号保持兼容值。exact key 继续同步写入既有 production compatibility `ImageLookups`，而 factory 只读 sidecar；decode 后 dictionary 的 add/replace/remove/reassign/clear 与手工注入都不能伪造、擦除或改变 accepted provenance。
- focused **15/15**；连同 scalar/array/global-colour/per-column-colour/declaration/legacy decoder/snapshot 为 **98/98**，shared neutral gameplay 总集 **250/250**，BMS full **1146/1146**。mania full **827/831** 的 4 项仍为恢复基线同名 `TestSceneAutoGeneration` HoldNote frame-count 期待；core skin **57/62** 仍为 1 项 Argon 旧期待与 4 项已删除 ruleset archive。`osu.Desktop.slnf` Release Rebuild **0 error / 20 warnings**；保留 9 条 MessagePack 3.1.3 `NU1902` 在 restore/build 重复显示及 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。targeted formatter/verify exit 0，仍如实保留泛化 workspace-load warning；独立审查为 0 blocker / 0 major。首次 Markdown checker 只按文件目录解析仓库根 authored memory link，假报 14 个 missing，属于无效证据；补回 root-contained 仓库根 fallback 后重跑为 **121 个文件 / 936 个相对链接 / 0 断链**。
- 本切只冻结 source-specific、process-local accepted string declaration；它未做 containment、文件存在/解码、动画帧、纹理预算、materialization 或 slot validation，declaration 不等于 `Provide`，也不是 topology、neutral resource catalog、manifest/wire ABI 或 candidate plan。没有把新 snapshot 接入 `LegacySkin` production consumer、renderer、`SkinManager`、nullable `ISkin`、真实 fallback/reload 或 package/wire ABI，没有访问或写入生产 Realm、`chartskin/`、用户皮肤目录或网络；程序化 `OmsSkin` 与 fallback authority 均未改变，Skin V1 仍不可用。
- 下一切须关闭六类逐 lane resource 的两个 decoder→factory provenance 窗口：legacy mania factory 当前读取可替换的 public `LegacyManiaSkinConfiguration.ImageLookups`，native `[Bms]` factory 也读取 public mutable `BmsSkinConfiguration.ImageLookups`。若按更小切片分别处理，在两侧都改为只读 decoder-time accepted sidecar 前，不得宣称 lane-resource durable decoder provenance 已闭合；不得把这项加固与真实文件 validation/materialization、production candidate resolution 或 `SkinManager`/renderer/fallback 接线合并。

### `SV1-1` 第十五个合同切片：legacy mania per-column colour provenance 与 stable-lane projection

- shared legacy mania configuration/decoder 为 exact case-sensitive `Colour{n}` / `ColourLight{n}` 增加 decoder-time accepted declaration/value sidecar。`{n}` 只接受 `1..Keys` 的严格 1-based ASCII decimal token；`01`、`+1`、大小写变化、`0`、越界、后缀及其它 `Colour*` 均不进入 sidecar。只有既有颜色解析成功后才捕获当时的 parser `Color4`；valid duplicate 为 last accepted，malformed 不创建或覆盖 declaration。sidecar/accessor 均防御性复制，之后对公开 `CustomColours` 的 add/overwrite/remove/clear 不能伪造或擦除 provenance。
- 新增 closed process-local 的 lane-colour field catalog（`LaneBackground` / `LaneLight`）、exact-topology-bound immutable snapshot 与 source-column projection。snapshot 按 logical lane/field 确定性排序；partial mapping 与多个 target lane 共享同一 source column 的 many-to-one mapping 合法，重复 target、topology 外 target 与 source 越界 fail-closed。mania 使用 `GlobalLogicalIndex`；BMS full 使用 `GlobalVisualIndex`，14K deck 使用 `GroupLocalVisualIndex` 且两个 deck 共享 source index，key-only 按 non-scratch visual order。三个 BMS projection 当前彼此独立且仅供 fixture；未来若并入同一 candidate plan，必须共享同一个 exact topology reference。
- focused 最终为 shared **18/18**、mania **5/5**、BMS **14/14**，既有 BMS candidate mapping refactor fixture **29/29**；scalar/array/global/per-column/declaration/legacy decoder/snapshot aggregate **83/83**，shared gameplay **250/250**，BMS full **1146/1146**。BMS focused 首轮唯一失败来自 fixture 错把目标色构造成 `Color4(float, ...)`，改用与 decoder 值一致的 byte constructor 后通过，不是生产实现回归。mania full **827/831** 的 4 项仍是恢复基线同名 `TestSceneAutoGeneration` HoldNote frame-count 期待；core skin **57/62** 仍为 1 项 Argon 旧期待与 4 项已删除 ruleset archive。
- `osu.Desktop.slnf` Release Rebuild **0 error / 20 warnings**；仅保留 9 条 MessagePack 3.1.3 `NU1902` 在 restore/build 重复与 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。六个 owning project 的 targeted `dotnet format --verify-no-changes` 均 exit 0、无格式差异，但每次都保留泛化“加载工作区时遇到警告，诊断级别可查看”，没有将其隐去或写成静默全绿。首次 Markdown checker 调用在进入 PowerShell 前因 raw JavaScript template 中的 `${file}` 仍触发宿主插值而失败，属于无仓库影响、无效证据；移除 `${...}` 后按 hidden-aware、root-safe、fail-closed 规则重跑为 **119 个文件 / 936 个相对链接 / 0 断链**。
- 独立 source 审查为 0 blocker / 0 major / 0 minor；contract 审查为 0 blocker / 0 major，提出的 direct `IList` 只读性与 BMS `LaneLight` 两项测试遗漏均已补齐。另一个未来风险不是当前失败：若三个独立 BMS projection 后续合并进同一 candidate plan，必须复用同一个 exact topology reference；该约束已写入技术约束。
- 本切没有接入 production lookup、candidate plan、renderer、`SkinManager`、nullable `ISkin`、真实 fallback/reload 或 package/wire ABI，也没有访问或写入生产 Realm、`chartskin/`、用户皮肤目录或网络。下一切先为 exact 13 项 global resources（`LightingN`、`LightingL`、`StageLeft`、`StageRight`、`StageBottom`、`StageLight`、`StageHint`、`Hit0`、`Hit50`、`Hit100`、`Hit200`、`Hit300`、`Hit300g`）建立 decoder-time sidecar，再硬化六类 lane resource 的 `ImageLookups` provenance。第十五切仍只是合同/fixture 地基，Skin V1 不可用。

### `SV1-1` 第十四个合同切片：topology publication、revision 与 native-context continuity

- shared 新增 public、sealed、immutable 的 `GameplaySkinLaneTopologyPublication` 与 `GameplaySkinLaneTopologyRevisionOwner<TNativeContext>`。一个 process-local owner 的首个成功 publication 为 revision 0；后续先执行 exact native-context comparator、既有 neutral topology transition validation 与 checked increment，全部成功才替换 `Current`。native mismatch、comparer 异常、neutral rejection、`long.MaxValue` overflow 与 invalid input 均不推进状态。公开安全字符串不展开 context/topology；该 revision 明确不是 package revision、event `layoutRevision`、serialization/wire ABI、thread-safe owner 或 security boundary。
- BMS internal wrapper/owner 以 exact `BmsKeymode` 维护 native continuity，`AppliedStyle` 仅为可变 presentation metadata。5K/7K 覆盖 P1/P2、Center/CenterRightScratch 的 visual reorder，以及 P1→Center/P2→CenterRightScratch 这种 topology 不变的 style-only change；9K fixture 先证明 neutral validator 会接受 BMS/PMS 同 shape，再由 native gate 拒绝且后续合法 BMS publication 仍为 revision 1；14K canonical 双 deck 独立重建可发布。没有把 `BmsKeymode` 放进 shared 合同。
- mania factory 先复制 ordered stage-column vector，再由 projection 自行创建 canonical topology，不再接受调用方传入的任意同 cardinality topology；旧 `Create()` 仍返回相同 neutral snapshot。internal owner 以 exact ordered vector 维护 continuity：同 shape 独立重建递增，4→5 与 `[4,5]→[5,4]` 从 `nativeContext` gate 原子拒绝，beatmap 后续 mutation 不漂移。ruleset wrappers 未接 playfield、renderer、event producer 或 `SkinManager`。
- focused：shared owner **8/8**、owner+transition **20/20**、shared gameplay **243/243**；BMS publication **14/14**、topology aggregate **34/34**、full **1132/1132**；mania publication **7/7**、topology aggregate **16/16**、full **822/826**。mania 4 项失败仍是恢复基线同名 `TestSceneAutoGeneration` HoldNote frame-count 期待；core skin **57/62** 仍是 1 项 Argon 旧期待与 4 项已删除 ruleset archive。`osu.Desktop.slnf` Release Rebuild **0 error / 20 warnings**；仅保留 9 条 MessagePack 3.1.3 `NU1902` 在 restore/build 重复与 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。六个 owning-project formatter/analysis verify exit 0（仅 workspace-load 概括 warning）；Markdown **119 文件 / 936 相对链接 / 0 断链**。
- 三轮独立只读审查均为 0 blocker / 0 major。审查提出的 Mania exception source/canonical construction、BMS style-only 与 9K reject 后 revision fixture 已补齐；规则集 wrapper 的强原子性措辞收窄为 comparator/validation/overflow 等正常拒绝路径，不把灾难性对象分配失败误写成跨 assembly 事务保证。
- 只读审计另确认旧 lane-resource factory 在调用时读取公开可变 `LegacyManiaSkinConfiguration.ImageLookups`：snapshot 创建后稳定，但 decode 后、factory 前的 mutation 仍可伪造/擦除 declaration。下一切先为 exact `Colour{n}`/`ColourLight{n}` 建 decoder-time accepted sidecar；global resources 后续同样不得从 mutable dictionary 反推。未访问生产 Realm、`chartskin/`、用户皮肤目录或网络。
- 未实现完整 `GameplaySkinLayoutContext`、bounds/geometry/action/source、production `layoutRevision`/event/wire producer、thread-safe attachment、per-column/扩展 colour、global resources/`NoteBodyStyle`、真实文件 validation/materialization/shared codec、生产 adapter、G1、scene/script、`oms-simple`/`oms-complex` 视觉或 fallback authority 切换。第十四切仍只是合同/fixture 地基，Skin V1 不可用。

## 2026-07-14

### `SV1-1` 第十三个合同切片：legacy mania known-global-colour accepted-declaration snapshot

- shared legacy mania configuration/decoder 为现有 production lookup 已消费的四个 exact global colour——`ColourColumnLine`、`ColourJudgementLine`、`ColourBreak`、`ColourBarline`——增加 decoder-time accepted declaration/value sidecar。既有 `HandleColours(..., allowAlpha: true)` 完整成功后才捕获当时 `Color4`；RGB 保留 alpha 255，RGBA 与 alpha 0 原样保存，valid duplicate 为 last accepted，malformed 不创建或覆盖 sidecar。没有改 shared parser 签名、tokenizer、pending-line 时序、compatibility dictionary 或 production lookup。
- 新增 public、sealed、process-local 的 source-specific exact-bucket snapshot/factory。缺 bucket 为 outer `Absent`，显式空 bucket 为 outer `Declared` 且四字段全 `Absent`；factory 不从公开可变的 `CustomColours` 反推，manual/dictionary clear/overwrite 与 snapshot 后 mutation 均不能伪造或漂移 provenance。fixed-property surface 不暴露 raw key、string lookup 或 dictionary，`ToString()` 只返回类型名。
- 本切明确排除 `Colour{n}`、`ColourLight{n}` 及其它以 exact 大写 `Colour` 前缀开头的非四项 key；这些有效键仍按原 decoder 进入 compatibility dictionary，但不冻结为 public ABI，lowercase `colour*` 仍被旧 decoder 忽略。snapshot 保存 renderer compatibility 前的 parser value，不做 doubled alpha、zero-alpha 修正、默认回落、额外 range/视觉验证、stable lane 映射或 neutral colour taxonomy。malformed colour 留在 `flushPendingLines()` 队首并阻断同 section 后续的既有坏行为未修复，也未提升为 V1 合同。
- focused **15/15**；连同 primitive scalar/indexed-array/legacy decoder/declaration 为 **65/65**。shared gameplay **235/235**、mania relevant **120/120**、BMS relevant **108/108**；core skin **57/62** 的 1 项 Argon 旧期待与 4 项已删除 ruleset archive 失败仍和恢复审计同名。额外 mania 全量 **815/819**，4 项仍是恢复基线同组的 `TestSceneAutoGeneration` HoldNote frame-count 期待失败，与本切文件无交集。`osu.Desktop.slnf` Release Rebuild **0 error / 20 warnings**；仅保留 9 条 MessagePack 3.1.3 `NU1902` 在 restore/build 重复与 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。owning-project formatter exit 0（仅 workspace-load 概括 warning），独立审查 0 blocker / 0 major / 0 minor。首个 Markdown checker 因未含 hidden 文件且根文件 parent 为空而只报 82/858，并伴随 `Join-Path` 错误，判为无效证据；修正为 hidden-aware、root-safe、fail-closed 后权威结果为 **118 个文件 / 932 个相对链接 / 0 断链**。
- 未实现 per-column colour、完整 neutral/global config、global resources、`NoteBodyStyle`、shared codec/structured malformed diagnostics、真实文件 validation/materialization、`SkinManager`/nullable `ISkin`/renderer/production adapter、`oms-simple`/`oms-complex` 视觉或 fallback authority 切换；未访问生产 Realm、`chartskin/`、用户皮肤目录或网络。第十三切仍只是合同/fixture 地基，Skin V1 不可用。

### `SV1-1` 第十二个合同切片：legacy mania indexed-array accepted-declaration snapshot

- shared legacy mania configuration/decoder 为 `ColumnLineWidth`、`ColumnSpacing`、`ColumnWidth`、`LightingNWidth → ExplosionWidth`、`LightingLWidth → HoldNoteLightWidth` 增加 private per-index accepted-value sidecar。五个 power-of-two single-value field 通过 exact switch 选择 native/sidecar 数组；field 和 index 在写入任一 view 前完成校验，unknown/composite/越界 fail-closed。decoder 改为 field-driven scale rule：line width 不缩放，其余四组沿用 `×1.6`，未改变 `Split(',')`、`TryParse` 或现有 compatibility 值。
- 新增 public、sealed、process-local 的 source-specific array snapshot 与 exact-bucket factory。cardinality 固定为 `Keys+1 / Keys-1 / Keys / Keys / Keys`；每个 index 独立 `Absent/Declared`。短数组 synthetic tail 不提升为声明；空/invalid/trailing-empty item 仍按旧规则接受为 `Declared(0)`；超长尾在容量边界忽略；重复短行逐 index last accepted 且保留未覆盖的先前尾部。sidecar accessor 与 snapshot 各做 defensive copy，公开面只暴露 `Array.AsReadOnly`，decode 后 native mutation、accessor 副本 mutation 与 snapshot 后 mutation 均不漂移 provenance。
- focused 初版 **19/19**；独立实现审查为 0 blocker / 0 major / 2 minor，随后补 `int.MaxValue` cardinality overflow fail-closed 与 accessor backing-copy fixture，并把“atomic”措辞收窄为异常前完成 field/index validation，最终 **20/20**。连同 primitive scalar/legacy decoder/declaration 合同为 **50/50**；fixtures 覆盖 missing/empty/exact/duplicate bucket、五组缩放/显式零、短/超长、空/invalid/whitespace/trailing item、partial overlay、Keys 前 pending、1K zero-spacing、non-finite、manual/native mutation、错误输入/cardinality、只读公开面与安全字符串。
- 扩大验证为 shared gameplay **235/235**、mania relevant **120/120**、BMS relevant **108/108**；core skin **57/62** 的 1 项 Argon 旧期待与 4 项已删除 ruleset archive 失败仍和恢复审计同名，无新失败。首次 code-changing Release 与最终强制 Rebuild 均为 **0 error / 20 warnings**；中间一次增量 Release 因 BMS test projects 未重编译而为 **0 error / 18 warnings**，未重报既有 `CS8600`/`CA2007`。完整构建只保留 9 条 MessagePack 3.1.3 `NU1902`（restore/build 重复为 18）与上述 BMS tests 两条既有告警，未使用 `NoWarn`。多轮 owning-project formatter/verify 均 exit 0，仅显示已知 workspace-load 概括 warning；Markdown **118 文件 / 932 相对链接 / 0 断链**，diff/隐私检查通过。
- snapshot 只保存 decoder converted compatibility value，不把 `ColumnLineWidth` boundary、`ColumnSpacing` gap 强行映射到 stable lane，也不提前派生左右 spacing 或 explosion/light scale。未覆盖颜色/global resources/`NoteBodyStyle`、finite/range/layout validation、完整 neutral config/shared codec、真实文件 validation/materialization 或 production adapter；未改 malformed pending/`Keys` 时序，未接 `SkinManager`、nullable `ISkin`、renderer、真实 `.osk`/`oms-simple`/`OmsSkin` authority，也未访问生产 Realm、`chartskin/`、用户皮肤目录或网络。第十二切仍只是合同/fixture 地基，Skin V1 不可用。

### `SV1-1` 第十一个合同切片：legacy mania primitive scalar accepted-declaration snapshot

- shared legacy mania configuration/decoder 为 `WidthForNoteHeightScale`、`HitPosition`、`LightPosition`、`ComboPosition`、`ScorePosition`、`BarlineHeight`、`JudgementLine`、`KeysUnderNotes`、`LightFramePerSecond` 九个 primitive scalar 增加 internal accepted-value sidecar。只在既有 parse、转换、clamp/scale、bool/FPS 规范化成功赋值后捕获 `Declared(value)`；枚举标识是 single-value discriminant，未知/组合值 fail-closed。sidecar 是 process-local decoder provenance，不是 security/authority boundary。
- 新增 public、sealed、process-local 的 source-specific bucket snapshot 与 factory。factory 只扫描实际 decoder output 的 exact `Keys:` bucket，不经过会合成默认 configuration 的 production `LegacySkin` lookup；缺 bucket 为外层 `Absent`，显式空 bucket 为外层 `Declared` 且九字段全 `Absent`，显式默认保持 `Declared`。accepted value 在 decoder 时已复制，decode 后先改 native public 字段再调用 factory、或 snapshot 后再改，均不能漂移；手工构造/直接 native mutation 也不能伪造 presence。
- focused 从初版 presence-only **15/15**，在独立预审指出“factory 读取 mutable native 调用时值”后改为 presence+accepted-value sidecar，并补 mutation fixture；随后又补 duplicate `Keys` discarded bucket 不污染 accepted bucket和 unknown/composite sidecar field fail-closed，最终 **18/18**。连同既有 legacy decoder/declaration 为 **30/30**；fixtures 还覆盖 missing/empty/exact bucket、显式 defaults、既有转换/bool/FPS 规则、scalar-before-Keys、malformed numeric、`NaN`/`Infinity` declared-but-unvalidated、重复 scalar last accepted、伪 duplicate/null decoder output、公开面与安全字符串。独立终审为 0 blocker / 0 major / 0 minor。
- 扩大验证为 shared gameplay **235/235**、mania relevant **120/120**、BMS relevant **108/108**；core skin **57/62** 的 1 项 Argon 旧期待与 4 项已删除 ruleset archive 失败仍和恢复审计同名，无新失败。最终 `osu.Desktop.slnf` Release **0 error / 20 warnings**；只保留 9 条 MessagePack 3.1.3 `NU1902`（restore/build 重复为 18）与 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。多轮 owning-project formatter/verify 均 exit 0，仅显示已知 workspace-load 概括 warning；Markdown **118 文件 / 932 相对链接 / 0 断链**，diff 检查通过。
- 未覆盖五组数组/per-index mask、颜色、global resources、`NoteBodyStyle`、finite/range validation、完整 neutral config、shared codec、真实文件 validation/materialization 或生产 adapter。`flushPendingLines()` 坏行保留、malformed/duplicate `Keys` 的既有 parser 时序没有顺手改变，另列 shared codec/malformed diagnostics 决议；未接 `SkinManager`、nullable `ISkin`、renderer、真实 `.osk`/`oms-simple` 或 `OmsSkin` authority 切换，也未访问生产 Realm、`chartskin/`、用户皮肤目录或网络。第十一切仍只是合同/fixture 地基，Skin V1 不可用。

### `SV1-1` 第十个合同切片：neutral topology-preserving transition validator

- shared `osu.Game.Skinning.Gameplay` 新增 public static、process-local 的 `GameplaySkinLaneTopologyTransitionValidator`。它只接收两个已构造的 immutable neutral snapshot：前后 GroupId/LaneId 集合、group logical index、lane group membership/role/global 与 group-local logical index 必须稳定；group side、group visual index 与 lane global/group-local visual index/order明确允许改变。无效 current 以 `ArgumentException` fail-closed；stable ID 可进入诊断，因为既有合同已禁止其中包含用户、包、资源名或路径。
- validator 不携带也不推断 keymode/style/action/source/geometry/revision。BMS fixture 固定 5K/7K P1↔P2 的 side/visual reorder 可通过、5K→7K 的 neutral shape 改变会拒绝；9K BMS/PMS 因 neutral shape 相同会通过，测试明确声明 native keymode continuity 仍由外层 projection/context 负责。mania fixture 固定同 stage 独立重建通过、4K→5K 拒绝。shared fixture 另覆盖 null、独立等价重建、group/lane count 与 ID set、group logical order、lane membership/role/logical order，以及公开面不携带状态。
- 最终 focused transition **12/12**、shared gameplay aggregate **235/235**、mania relevant **120/120**、BMS relevant **108/108**；core skin **57/62** 的 1 项 Argon 旧期待与 4 项已删除 ruleset archive 失败和恢复审计同名，无新失败。`osu.Desktop.slnf` Release **0 error / 20 warnings**，只保留 9 条 MessagePack 3.1.3 `NU1902`（restore/build 重复为 18）与 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。第九切 BMS full **1117/1117** 本切未重跑；Markdown **118 文件 / 932 相对链接 / 0 断链**。
- 首轮 source 编译新增 2 条 nullable `CS8602`，补 exact lookup null guard 后消失；owning-project formatter 首次按仓库规则拒绝 LF/mixed line endings，定点规范化后四个工程 verify 均为 exit 0，只保留无明细 workspace-load 概括 warning。独立审查为 0 blocker / 0 major / 1 minor，唯一测试名把 neutral-shape 拒绝写成过宽的 native-topology 拒绝，已收窄。审查者一次并行启动三个 Debug build 因共享 `osu.Game/obj` 命中 `CS2012` 文件锁，改为串行后 12/12、20/20、9/9；主验证始终串行。BMS relevant 首个过滤器通过 90 项但漏列既有 default-note 组，补齐后权威结果为 108/108。
- 未接 native context/revision producer、`GameplaySkinLayoutContext`、geometry、event、renderer、`SkinManager`、nullable `ISkin`、真实 `.osk`、`oms-simple` 或生产 adapter；未改变程序化 `OmsSkin` 与 fallback authority，也未访问或写入生产 Realm、`chartskin/`、用户皮肤目录或网络。第十切仍只是合同/fixture 地基，`SV1-1` 继续进行；`SV1-2` 仍只有 ctor/schema carrier，`SV1-3`～`SV1-7` 未进入生产实施，Skin V1 不可用。

### `SV1-1` 第九个合同切片：六字段逐项 resolution 与 revision-scoped component owner

- BMS 新增 internal lane-resource lookup context、source-aware declaration reference 与 selected-package candidate provider factory。factory 只按既有 plan 顺序发出 canonical marker 前的 providers，不装载或伪造 `oms-simple`；caller 仍显式组合 beatmap-local → selected candidates → ruleset resources → fake canonical。lookup fail-closed 校验 exact immutable topology、canonical field 与对应 semantic descriptor，provider 名只使用稳定非敏感 authority；resource name 不进入 context/reference/diagnostic 的安全字符串或 JSON。
- 缺 bucket/字段直接 `Inherit` 且不调用 owner；显式声明只有经 owner 构造、持有并完成基础验证后才可能 `Provide`。显式空名、缺文件、构造失败、null、额外 validator 拒绝/异常均由既有 shared `GameplaySkinSlotResolver` 产生结构化诊断并逐字段继续，取消异常传播，ini candidate 永不制造 `Suppress`。fixture 覆盖六字段 BMS→Keys8、5K/7K scratch 跳过 key-only、9K Keys9 不重复、14K full→deck 与六字段 deck→Keys14、双 scratch 跳过 Keys14、同名资源按 source 分流、critical/optional、beatmap `Provide`/optional `Suppress`、ruleset/canonical precedence 及 canonical 自身失败。
- 独立审查发现首版裸 materializer 在 outer validator reject/throw 后无人持有已构造 component，会对未来 Drawable/IDisposable 形成生命周期泄漏。最终改为 revision-scoped `IBmsGameplaySkinLaneResourceComponentOwner<T>`：winner/rejected 都只借自同一 owner，resolver/provider 不单独 dispose；失败 reload 只释放新 provisional owner，成功替换先 detach superseded consumer 再释放旧 owner，teardown 同理。fixture 固定 retain、validator false/throw、幂等 dispose、dispose 后拒绝 materialize、成功 revision swap 与失败 provisional/active 隔离。
- focused 从初版 **24/24** 扩至最终 **55/55**，BMS 全量 **1117/1117**；`osu.Desktop.slnf` Release **0 error / 20 warnings**。初次 source build 新增的 XML cref `CS1574`、owner fixture 首编译新增的 `CS8603`/`CS8600`/`CS8602`/`CA1513`，以及一次 targeted style `IDE0001` 均在最终门前修正；最终 owning-project whitespace/style verify 全过，只显示无明细的 workspace-load 概括 warning。最终构建只保留 9 条 MessagePack 3.1.3 `NU1902`（Release restore/build 重复为 18）与 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。本切未改 shared/mania 源码，因此 shared/core/mania focused 均沿用上一切已记录基线而未重跑；Release 已重新编译 shared、mania、BMS 与 desktop。首个内联 link checker 因 JavaScript host 吞掉 PowerShell regex 反斜杠而假报 11 条垃圾目标；改用 raw string 后权威结果为 **118 个 Markdown / 932 个相对链接 / 0 断链**，不能把脚本错误记成文档回归。
- 未接真实 `.osk` 文件 containment/存在性/解码、纹理预算、production materializer、Drawable parenting/thread affinity、`SkinManager`、nullable `ISkin`、renderer、真实 `oms-simple` 或 atomic reload；未改变程序化 `OmsSkin` 和 production fallback authority，也未访问或写入生产 Realm、`chartskin/`、用户皮肤目录或网络。第九切仍只是合同/fixture 地基，`SV1-1` 继续进行，`SV1-2` 仍只有 ctor/schema carrier，Skin V1 不可用。

### `SV1-1` 第八个合同切片：lane-resource neutral snapshot 与 BMS→mania compatibility plan

- shared `osu.Game.Skinning.Gameplay` 新增 closed process-local 六字段 catalog：note、LN head/body/tail、key up/down，并关联既有 semantic slot。声明携带 stable lane ID/field/未验证 resource name，显式空字符串保持 `Declared`；immutable snapshot 绑定 exact lane topology、防御性复制、按 logical lane/catalog 确定性排序，拒绝 null、非 catalog field、topology 外 lane 与重复 lane-field，缺项查询返回 `Absent`。安全 `ToString()` 不展开资源名；这不是完整 neutral config、manifest/serialization ABI 或 slot `Provide`。
- public `LegacyManiaGameplaySkinLaneResourceSnapshotFactory` 只因 mania/BMS 跨程序集共享实际 `LegacyManiaSkinDecoder` field 语义而作为 CLR bridge；它不是 plugin/package/manifest/script API，也绝不经过会为缺失 `Keys:` 合成默认 configuration 的 `LegacySkin` lookup。internal mania adapter 将 native topology 的 global logical column 投影到 snapshot；internal BMS adapter 只读取实际 `BmsSkinDecoder` bucket，scratch 使用 `S/S2`。
- internal BMS candidate plan 保留整个 bucket/field 候选链而不提前选首值或验证资源：5K `[Bms]→Keys6→Keys5→marker`，7K `[Bms]→Keys8→Keys7→marker`，9K BMS/PMS `[Bms]→Keys9→marker` 且不重复相同 key-only bucket，14K `[Bms]→Keys16→同一 Keys8 bucket 按两个 engine-owned deck 分别投影→Keys14 普通键→marker`。P2/CenterRightScratch full bucket 按 global visual index，14K deck bucket 按 group-local visual index；stable lane ID/action 不变。Keys8 先于 Keys14，以优先保留 scratch/deck-local presentation；末端只是 `Absent` canonical marker，未伪造或装载 `oms-simple`。
- 对照当前 `BmsLegacySkin.resolveImageKey()` 确认一项文档债：未版本化 5K/7K/14K 因 scratch 位于 logical index 0 而普通键 raw token 从 1 开始，但无 scratch 的 9K BMS/PMS 当前实际为 `0..8`。本切片按当前生产事实建立 fixture；V1 canonical 作者目标 `1..9` 必须另做显式格式版本、迁移和冲突诊断，不能将两套在 `1..8` 上重叠的 token 静默双 alias。
- 新增 focused shared **12/12**、mania adapter **6/6**、BMS candidate **29/29**，合计 **47/47**；扩回归 shared gameplay **223/223**、provider authority **6/6**、mania relevant **119/119** + legacy decoder **7/7**、BMS relevant **107/107** + transformer/fallback **104/104**。core skin 仍为恢复基线同名既有 **57/62**（1 项 Argon 旧期待、4 项已删除 ruleset archive），强制 `osu.Desktop.slnf` Release Rebuild **0 error / 20 warnings**。
- 首次 source build 暴露一条新增 nullable `CS8602`，补 exact group lookup null guard 后消失；首次 solution-level formatter verify 对 LF 新文件报告 `ENDOFLINE` 并提示一处 `IDE0032`，改为 auto-property、按 owning project 规范化后 targeted verify 通过；后续注释调整再次留下 5 行混合行尾，也由 owning-project formatter 捕获并规范化，最终六个工程 whitespace/style verify 全过。保留 9 条 MessagePack `NU1902`（Release restore/build 重复为 18）与 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`；一次 5 秒命令工具超时被中断后以完整 27 秒 Release 重建重新取证，不能把工具超时记作工程失败。Markdown **118 文件 / 932 相对链接 / 0 断链**。
- 未接 `SkinManager`、nullable `ISkin`、renderer、真实 fallback package 或任何具体视觉；未改变程序化 `OmsSkin`、provider authority 与 production lookup。未访问或写入生产 Realm、`chartskin/`、用户皮肤目录或网络。第八切只是六字段 config/fallback 候选地基，Skin V1 仍不可用。

### `SV1-1` 第七个合同切片：capability negotiation 与永不授权 authority

- shared `osu.Game.Skinning.Gameplay` 新增 opaque `GameplaySkinCapabilityId`、internal-created immutable request、结构化 denial 与 immutable negotiation snapshot。ID 使用 lowercase ASCII dot-segment/ordinal 强值语义且不得包含用户、包、资源或路径；request 防御性复制、拒绝 duplicate 并按 ordinal 排序。当前 CLR carrier/diagnostic/JSON 只是 process-local decision/隐私 fixture，不是 manifest、持久化或 script ABI；future parser 的 ID length/request count/package budget 尚未定义。
- internal pure negotiator 显式分离 package request、engine closed allowlist definition、host feature availability 与当前 skin authorization snapshot。判定优先级为 hard deny → unknown → host unavailable → per-skin authorization missing → grant；unknown 即使混入 support/grant 也不动态注册，unrequested support/grant 不产生权限，host feature 缺失优先于 stale authorization。`NoAdditionalAuthorization` 仅预留给低风险 baseline，真实 optional package ability 仍须 per-skin authorization。
- hard-deny 表固定 28 个明确 authority token，并对这些 root 的 descendant、gameplay terminal mutation action，以及 Realm/config/network/arbitrary-filesystem/reflection/process/thread/native family 做第二层 fail-closed 分类。fake definition + available feature + authorization 也不能覆盖。独立审查发现首版 classifier 扫描任意 action segment 会误杀 `gameplay.lifecycle.reset.read`/`event.seek.read` 等只读事件；最终改为“明确 deny root descendant + terminal action”，补 reset/pause/create/trigger/seek/score-update read fixtures。该 classifier 是 closed allowlist 后的第二屏障，不是任意同义词穷举；声明式 geometry 与 package-scoped resource read 分别不等于 gameplay mutation 和 arbitrary filesystem。
- negotiation aggregate 拒绝 hard-denied grant、同 ID grant+deny、duplicate denial 与 hard-deny code/ID 不一致；公开结果只含 ID/diagnostic/count query，不含 `Drawable`/`HitObject`/judgement/score/gauge/Realm/bindable/clock、delegate、service 或 authority handle。authorization 撤销与 feature 移除只能通过重新协商得到新 snapshot；本切片没有 package identity/授权存储/UI、required/optional、layer activation/fallback/version、真实 host-call gate、scene/script runtime、VM 或 sandbox。
- focused 首轮 **59/60**，唯一失败是测试误把 Newtonsoft enum JSON 期待成字符串，实际仍为安全数值输出；修正为不冻结数值 ABI 后通过。审计收紧后的首次编译又因 targeted formatter 把实际使用的 `System.Reflection` 误判 unused 并删除而出现 4 个 `CS0103 BindingFlags`；改用全限定名后最终 capability **91/91**、shared gameplay aggregate **211/211**。并发测试曾出现一次 `MSB3026` DLL 锁重试后成功，串行最终门未复现。
- 最终扩回归：provider authority **6/6**、mania relevant **113/113** + legacy decoder **7/7**、BMS relevant **78/78** + transformer/fallback **104/104**；core skin 仍为同名既有 **57/62**，强制 `osu.Desktop.slnf` Release Rebuild **0 error / 20 warnings**。保留 9 条 MessagePack `NU1902`（Release restore/build 重复为 18）与 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`；Markdown 117 文件 / 926 相对链接 / 0 断链。
- 没有真实 production capability allowlist entry，也未接 `SkinManager`、manifest、scene/script、Realm、Storage 或 host service；未访问/写入生产 Realm、`chartskin/`、用户皮肤目录或网络，未改变 nullable `ISkin`、程序化 `OmsSkin`、fallback authority 或任何具体视觉。第七切只是权限决策地基，Skin V1 仍不可用。

### `SV1-1` 第六个合同切片：gameplay event envelope 与 canonical stream ordering

- shared `osu.Game.Skinning.Gameplay` 新增非 generic、只读 `GameplaySkinEventEnvelope`、V1 常量、`Snapshot/Reset/Edge` delivery kind 与 engine-owned `GameplaySkinEventPayload` hierarchy。envelope 固定 `apiVersion/epoch/sequence/gameplayTime/layoutRevision`，只允许内部 dispatcher 边界构造；payload 基类不允许第三方 package 派生，后续 concrete family 必须由 shared engine 定义。`gameplayTime` 使用 gameplay clock 毫秒域，允许 finite 负 lead-in，拒绝 NaN/Infinity；正数 future version 可表示以供 fail-closed 拒绝，当前 cursor 只支持 V1。
- internal `GameplaySkinEventStreamCursor` 只校验 capability/family filtering 前的完整 canonical stream：新 consumer 可从任意非负 mid-session epoch/sequence 的完整 Snapshot high-water attach；之后 epoch 与同 epoch sequence 均严格连续，同 epoch time 非递减且同时间由 sequence 排序。Reset 只能在下一 epoch 的 sequence 0 原子重锚，time 可前跳/后跳；layout revision 全 attachment 不回退，Snapshot/Reset 可保持或推进，Edge 只能引用当前 revision。拒绝不推进状态、不排序/补洞/修复，sequence/epoch 边界禁止 wrap。
- 独立审查在首轮 21/21 后发现 cursor 自称“complete, unfiltered”却允许 epoch 跳号，fixture 还把 4→7/3→5 当成合法；最终改为严格 `previous+1`，补 epoch gap 与 `long.MaxValue` 防回绕。另把未知 cursor version 改为 fail-closed，只让 envelope header 表示 future positive version；最终 event focused **23/23**、shared gameplay aggregate **120/120**。
- public-surface fixture 固定 envelope 非 generic、无 public constructor/factory/setter，payload ctor 仅 engine-visible，并拒绝 `Drawable`/`HitObject`/`Bindable`/ruleset-native 属性类型。该反射门只覆盖 envelope/base hierarchy；每个后续 concrete payload 仍必须单独验证 sealed/immutable/defensive-copy/property graph。
- 本切片没有接入 `GameplayClockContainer.OnSeek`、`DrawableRuleset`、`SkinReloadableDrawable.SourceChanged`、`SkinManager` 或生产 dispatch。现有 `OnSeek` 无 reason/time 且 `Reset()` 同样经过 `Seek()`，`JudgementResult` 会在 revert 回调后 reset，`HitEvent` 仍携带 `HitObject`，现成 reload callback 又会调度/合并，均不能直接冒充稳定 producer。当前 fake payload 只证明 envelope/category/order，不能证明完整 Snapshot/Reset、真实 reload/seek/retry delivery 或 event runtime；Skin V1 仍不可用。
- 最终验证：event **23/23**、shared gameplay aggregate **120/120**、provider authority **6/6**、mania relevant **108/108** + legacy decoder **7/7**、BMS relevant **78/78** + transformer/fallback **104/104**；core skin 仍为同名既有 **57/62**，强制 `osu.Desktop.slnf` Release Rebuild **0 error / 20 warnings**。targeted formatter/analysis 0 文件待改，workspace-load 概括告警仍对应 MessagePack advisory；Markdown 116 文件 / 923 相对链接 / 0 断链，working/untracked diff check 通过。保留 9 条 MessagePack `NU1902`（Release restore/build 重复为 18）与 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。
- 未访问或写入生产 Realm、`chartskin/`、用户皮肤目录或网络；未改变 nullable `ISkin`、程序化 `OmsSkin`、fallback authority、layout solver、ini codec、scene/script 或具体 `oms-simple/oms-complex` 视觉。

### `SV1-1` 第五个合同切片：configuration bucket explicit presence

- shared `osu.Game.Skinning.Gameplay` 新增 `GameplaySkinConfigurationDeclaration<T>`：`default`/`Absent` 不携带值，`Declared(T)` 保留显式 `false`、`0` 与空字符串；`Value` 在 absent 时 fail-closed，`TryGetValue()` 不把缺失折叠成 `default(T)`，安全 `ToString()` 只输出状态。它是 process-local declaration provenance，不是 slot `Provide/Inherit/Suppress`、validation result、manifest 或 serialisation ABI。
- internal mania/BMS factory 只从实际 `LegacyManiaSkinDecoder` / `BmsSkinDecoder.Configurations` 输出查找 exact bucket，并返回 `int`/`BmsKeymode` immutable key marker；不经过会合成缺失 mania bucket 的 `LegacySkin` production lookup，也不把 mutable `LegacyManiaSkinConfiguration` / `BmsSkinConfiguration` 作为 neutral payload。null、同 target duplicate、unsupported gameplay key count/keymode fail-closed；其它 target 的普通 bucket 不改变 exact lookup。
- fixtures 固定 missing `Keys:`/`Keymode` 为 `Absent`、显式空 bucket 为 `Declared`、`[General]` metadata 不创建 bucket、BMS 显式 zero/空 image 不折叠、malformed field 不抹除已存在 bucket、9K BMS/PMS 分离。审查一度按 `AvailableVariants` 把 11/19 误判为非法 equal-dual variant，首轮收紧还被 0K fixture 立即抓到下界遗漏；二次对照 topology 的 mixed dual authority（两 stage 各 1–10 列）后撤回该过窄判断，最终以 total-column 1–20 为准并恢复 13/13。
- 最终验证：shared declaration **5/5**、mania bucket **13/13**、BMS bucket **9/9**、shared gameplay contract 合并 **97/97**、legacy mania decoder **7/7**、provider authority **6/6**、mania relevant **113/113**、BMS relevant **71/71** 与 transformer/fallback **104/104**。core skin 仍为同名既有 **57/62**；强制 `osu.Desktop.slnf` Release Rebuild **0 error / 20 warnings**。
- 告警保持为 9 项 MessagePack `NU1902`（restore/build 重复为 18）和 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。首轮 whitespace verify 因审计补丁在两个 CRLF 文件留下 13 行 LF 而报告 `ENDOFLINE`；solution-level verify 随后又漏检未跟踪 core test，owning `osu.Game.Tests.csproj` 复核才报告其 89 行 `ENDOFLINE`。逐项目规范化后最终六文件 targeted verify 通过，workspace-load 概括告警仍对应已知 advisory。Markdown 相对链接 115 个文件 / 920 个链接 / 0 断链，working tree 与 staged diff 检查通过。
- 本切片没有改 decoder/tokenizer、`LegacySkin` lookup、compatibility mapping、`SkinManager`、nullable `ISkin`、程序化 `OmsSkin` authority、生产 Realm、`chartskin/` 或用户皮肤目录。field-level presence/diagnostics、neutral config snapshot/shared codec、event/capability、生产 adapter 与真实 `oms-simple` 仍未实施；Skin V1 不可用。

### `SV1-1` 第四个合同切片：neutral lane topology snapshot 与 internal ruleset projections

- 在 shared `osu.Game.Skinning.Gameplay` 新增 immutable `GameplaySkinLaneTopologyEntry` / `Group` / `Snapshot`。entry 显式承载 global/group-local 的 logical/visual 四类零基 index；group/snapshot 提供防御性复制后的只读排序视图和强类型 ID lookup。创建时 fail-closed 拒绝 null/empty、负 index、重复 group/lane ID、membership metadata 冲突、非 permutation、local/global order 不一致与 logical/visual group 非连续块；cross-revision 稳定仍是 producer 合同，不把单 snapshot 校验器冒充 transition validator。
- shared snapshot 故意排除 keymode/style、action/source channel、geometry/bounds、revision/native context，既不是完整 `GameplaySkinLayoutContext`，也不是 author manifest/event/JSON ABI。ruleset-specific factory/projection 均为 internal 且目前只有测试引用，没有接入生产 selection/render chain。
- `BmsLaneLayout.Lane` 只读暴露现有 solver 已计算的 `VisualIndex`，`Lanes` 仍保持 logical `LaneIndex` 存储，`RelativeStart`/几何和渲染行为不变。BMS projection 固定 5K/7K 四 style、9K BMS/PMS 与 14K DP 的 stable token、side、role、global/local order；14K 完整序列为 `S1,K1..K14,S2`、两个 8-lane group。它按 keymode 逐 lane 校验 canonical `(LaneIndex, Action, IsScratch)`，拒绝只满足 lane count、却注入额外 S2 或 9K 假 scratch 的 malformed layout。
- mania projection 防御性复制 stage authority，只接受 1–2 stage、每 stage 1–10 keys；single 为 Neutral，dual stage 0/1 为 Primary/Secondary。global index 使用 stage column count 前缀和，group-local index 使用 stage-local ordinal，当前 visual=logical；odd-stage centre 映为 `SpecialKey`，绝不赋予 scratch 语义。fixture 固定单 stage、双 5+5、mixed 4+5、null/empty/>2/>10 等边界。
- 独立审计在最终验证前补出三处测试盲点：canonical count 不等于 canonical composition；14K 必须锁完整 `Scratch + 14×Key + Scratch` role；连续块测试必须包含两组多 lane 的真实交错。修正后最终审计 blocker=0；功能 focused 首轮与审计修正后均无测试失败。
- 最终验证：shared slot+identity+topology **92/92**，provider authority **6/6**，BMS lane layout+projection **26/26**、parser/legacy/reference/render **43/43**、transformer/fallback **104/104**，mania topology/special/OMS **95/95**、默认资源专项 **1/1**。core skin 仍为既有 **57/62**（1 项 Argon 旧期待、4 项已删除 osu ruleset 的 archive fixture）；强制 `osu.Desktop.slnf` Release Rebuild **0 error / 20 warnings**。
- 静态收尾时 targeted formatter 把 fixture 所需的 `System.Collections.Generic` 误报为 `IDE0005`；按告警移除后 BMS fixture 编译以两处 `CS0246 HashSet<>` 失败。fixture 改用已引用 LINQ 的 `ToHashSet()`，whitespace 规范化后 BMS projection 19/19、最终 targeted verify exit 0。该次失败只发生在测试清理中，未触及生产实现。
- 最终保留告警为 9 项 MessagePack `NU1902`（restore/build 重复显示）和 BMS 测试工程既有 `CS8600`/`CA2007`，未使用 `NoWarn`。`dotnet format` workspace-load 概括告警仍对应同一组 advisory，source targeted verify 另重报 `BmsLaneLayout` 两个既有 array declaration 的 `IDE0008`；Markdown 相对链接 114 个文件 / 916 个链接 / 0 断链，最终 diff 检查通过。
- 没有访问或写入生产 Realm、`chartskin/`、用户皮肤目录或网络；没有改 `SkinManager`、nullable `ISkin`、三态生产接线、程序化 `OmsSkin` authority、shared ini、scene/event/script、layout solver 或具体 `oms-simple/oms-complex` 视觉。第四切仍只是合同地基，Skin V1 不可用。

### `SV1-1` 第三个合同切片：neutral lane identity primitives

- 在 shared `osu.Game.Skinning.Gameplay` 新增强类型 `GameplaySkinLaneGroupId` / `GameplaySkinLaneId`、`GameplaySkinLaneGroupIdentity` / `GameplaySkinLaneIdentity`，以及 `GameplaySkinLaneSide`（`Neutral/Primary/Secondary`）和 `GameplaySkinLaneRole`（`Key/SpecialKey/Scratch`）。公开 `Create()` 统一校验小写 ASCII 点分 opaque ID，避免依赖 BMS-only `InternalsVisibleTo`；ID 使用 ordinal 值相等，hash 只限进程内。
- producer 合同固定：ID 必须是非敏感 topology token，不得嵌入用户、包、资源名或路径；同一 ID 不得分配给两个不同 semantic group/lane，同一语义实体跨不改变 topology 的 revision 重建时必须复用 ID。stable ID 跨 style、视觉重排、geometry、skin reload 与 topology-preserving layout revision 保持，group membership/role 不漂移。跨这类 revision 关联只比较 `Id`；完整 identity equality 还包含当前 neutral metadata，`Group.Side` 可随 P1/P2 presentation 改变。
- `Side` 是逻辑 player/deck presentation side，不是屏幕 Left/Right 或 input binding authority；`SpecialKey` 明确是 key input，绝不获得 scratch gameplay 语义。值对象 `ToString()` 只输出受上述非敏感 producer 合同约束的 stable ID，未冻结 JSON/event/manifest ABI。
- 本切片没有加入 logical/visual/global/group-local index、keymode/style、action/source channel、rect/bounds、真实 mania/BMS token catalog、`GameplaySkinLayoutContext` aggregate 或 adapter。后续 mapping fixture 必须记住：`BmsLaneLayout.Lanes` 按 logical index 存储，不能把枚举位置当 visual index；mania special column 按 stage-local center 判定，双 5+5 的全局 special 是 2/7。
- identity focused 首次编译被 analyzer `RS0030` 拒绝，因为 fixture 直接调用禁用的 `object.Equals`；改用 `EqualityComparer<object>.Default` 后 identity 26/26、slot+identity 合并 73/73。provider authority 6/6、BMS 43/43 与 104/104、mania 84/84 与专项 1/1；core skin 57/62 仍为同名既有 5 项，强制 `osu.Desktop.slnf` Release Rebuild 0 error / 20 warnings。
- 最终 XML 合同补强后的首次 whitespace verify 报告 9 处 `ENDOFLINE`（补丁行使用 LF、项目要求 CRLF）；仓库格式器规范化后连续 verify 通过，identity 再跑 26/26。
- 首个自写 Markdown 检查 pass 将 14 个 `.Codex/memory` 既有仓库根相对链接按文件目录误解析为断链；改为文件相对优先、仓库根回退后复跑 114 个 Markdown / 915 个相对链接 / 0 断链，`git diff --check` 通过。
- 告警仍为 MessagePack 9 项 `NU1902`（restore/build 重复）及 BMS 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。`dotnet format` 的泛化 workspace-load warning 经 diagnostic verbosity 确认为同一组 9 项 MessagePack advisory，verify 仍为 exit 0。没有改 `SkinManager`、nullable `ISkin`、BMS/mania 生产 layout、`OmsSkin` authority、Realm 或用户皮肤；Skin V1 仍不可用。

### `SV1-1` 第二个合同切片：ruleset-neutral semantic slot taxonomy

- 新增不可变 `GameplaySkinSlotDescriptor`、`GameplaySkinSlotCatalog` 与 `GameplaySkinSlotLookup<TContext>`：26 个内部语义 family 固定为 7 critical / 19 optional，小写 ASCII 点分 ID 采用 ordinal 精确查询；未知或畸形 ID fail-closed 为 `TryGet=false`，不动态注册。ID 当前只用于内部 taxonomy/诊断，不是作者 manifest ABI，也不携带 lane/keymode/side/result/layout。
- critical 最小层为 lane surface、judgement line、note、LN head/body、mine 与 active lane-cover fill；LN tail、key/effect/HUD/barline/stage/backdrop/cover decoration/turntable/laser/BGA viewport+frame/decoration 均 optional。lane cover fill 只挂在引擎强制 geometry/clip host 内，BGA viewport 只呈现引擎只读 content surface。
- 新 resolver overload 把 descriptor 与 ruleset context 一并交给 provider，requirement 只能来自 descriptor；旧 raw overload保持兼容，但 catalog descriptor/lookup 若试图错配 requirement 会拒绝。diagnostic 保留上一切片构造/解构 ABI并新增 `SlotId`，JSON 与安全 `ToString()` 排除 process-local `Slot`/`Exception`；provider name 仍必须由 provider 保证为非敏感 authority。
- focused 首跑 43/44，唯一失败是新测试把 lazy `Distinct()` 当成有 `Count` 属性的集合；改为显式 `Count()` 后 catalog 34/34、旧 resolver 13/13、合并 47/47。一次并行启动 BMS/mania 测试因共享 `oms.Input/obj` 文件锁报 `CS2012`，顺序复跑后 provider authority 6/6、BMS 43/43 与 104/104、mania 84/84 和专项 1/1；core skin 57/62 仍是同名既有 5 项，Release 0 error / 20 warnings。
- 告警仍为 MessagePack 9 项 `NU1902`（restore/build 重复）及 BMS 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。没有改 `SkinManager`、nullable `ISkin`、legacy lookup/transformer、`OmsSkin` authority、生产 Realm 或用户皮肤；layout/config/event/capability/manifest、真实 `oms-simple` 与生产 suppress 均未实施。

### `SV1-0` 实机闭门，`SV1-1` 三态 gameplay slot 合同首切

- 用户亲自确认实机清单全部正常：无外部皮肤、当前 `.osk` 用户皮肤、partial fallback、BMS 5K/7K/9K/14K、14K S1/S2 双皿素材，以及 mania 默认资源未被 BMS reference 覆盖；Agent 没有操控 GUI。结合前一日自动与 schema 56 数据门，`SV1-0` 已完成。
- 在 `osu.Game.Skinning.Gameplay` 新增平行 `SkinSlotResult<T>`、`IGameplaySkinSlotProvider`、`GameplaySkinSlotResolver`、requirement/resolution 与结构化 diagnostic。默认 result=`Inherit`、默认 requirement=`Critical`；有效 `Provide` 与 optional `Suppress` 截断，critical `Suppress`、provider/构造失败、坏 `Provide` 和 validator 异常均诊断后继续 fallback；取消异常继续传播。
- resolver 严格保留调用方顺序且不 dispose 候选值。focused fixtures 覆盖三态、critical/optional、坏/异常 `Provide`、逐组件 fallback、全链 `Inherit`、fake `oms-simple` 末端、`Drawable.Empty()` 仍是普通 `Provide`，以及 beatmap-local `Provide` 不被后层 `Suppress` 穿透。
- 实 provider fixture 固定 `BeatmapSkinProvidingContainer` / `RulesetSkinProvidingContainer` 的顺序为 beatmap-local → selected → ruleset resources → protected built-in。guard 首跑 3/6：两项旧用例和新增用例都因测试夹具从全局 bindable 取得 mania ruleset、再强转通用 `Beatmap` 而失败；测试夹具改用其声明的 `CreateRuleset()` 后最终 6/6，生产容器未改。
- 最终验证：新合同 13/13、provider guard 6/6、BMS skin 43/43、BMS transformer/fallback 104/104、mania OMS 84/84、默认资源专项 1/1；core skin 57/62 的 5 项仍是既有 Argon/已删 ruleset 失配，无新增失败；`osu.Desktop.slnf` Release 首次与最终完整编译均为 0 error / 20 warnings。告警保留为 9 项 MessagePack `NU1902`（restore/build 重复显示为 18）及既有 BMS `CS8600`/`CA2007`；中间增量复核曾因 BMS tests 未重编而只重报 18 项，未使用 `NoWarn`。
- 本切片没有接入 `SkinManager`、没有改变 nullable `ISkin` ABI、没有删除/切换程序化 `OmsSkin` authority。G1、全量 layout DTO/solver、shared ini codec、scene/event/script、`oms-simple/oms-complex` 视觉与真实 canonical fallback 均未实施；不能据此称 Skin V1 可用。

## 2026-07-13

### `SV1-0` schema 56 只读取证触发 STOP，未进入三态合同实现

- 在 OMS/osu 进程关闭时先记录生产 `client.realm` 的 length/mtime/SHA-256，再复制到系统临时目录；仅用 Realm SDK dynamic + read-only 打开副本，未用 `RealmAccess` 打开生产文件。取证前后生产文件三项证据完全一致，`chartskin/` 与用户皮肤目录零写入。
- 共 3 条 `SkinInfo`：`FilesystemStoragePath` 非空 0、external 0、folder-backed 0、DeletePending 0、路径重复/冲突/越界 0；当前选择是 protected OMS 固定 ID，另两条为 managed hash-backed `.osk`。
- 当前 protected 记录与一条 managed 记录仍引用恢复树已删除的 `BmsOmsReferenceSkin`。前者会被普通启动重写，后者不会，并会落入 `SkinInfo.CreateInstance()` 的历史 `TrianglesSkin` fallback。按 stop/go 判为数据迁移 blocker，未启动生产客户端、未做实机 gate、未实施 `SV1-1`。
- 自动 gate：BMS skin focused **43/43**、BMS transformer/fallback **104/104**、mania OMS **84/84**、默认资源专项 **1/1**；core skin **57/62** 的 5 项与恢复审计同名，无新失败。每次保留 9 条 MessagePack `NU1902`，BMS 另有既有 `CS8600`/`CA2007`。
- 脱敏证据、实机待办和三种迁移选项见 [`SV1-0` 数据安全门报告](../../other/SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)。本切片只改文档/memory，不改运行时代码。

### `SV1-0` 异常 managed copy 完成备份、演练和生产定点处置

- 内容复核确认异常记录是 `BmsOmsReferenceSkin` 经 `EnsureMutableSkin()` 生成的 mutable copy，仅含自动 metadata 与 HUD/playfield JSON，没有 gameplay 素材。用户确认其无保留价值并授权执行，且要求不操控桌面。
- 备份迁移前 Realm/配置/四个关联 blob 并逐项校验；在第二代副本上预检精确 GUID、旧类型、model hash、4 file usages、authority 与总记录数，单事务演练通过后才应用生产。
- 生产事务将 `SkinInfo` 从 3 条减为 2 条，移除异常 managed GUID，并把 OMS fixed-ID 的四个字段修正为当前 `OmsSkin.CreateInfo()`；post-migration dynamic read-only reopen 返回 `VERIFY_OK_NO_WRITE`。未启动客户端、未运行 scanner/全局 cleanup，四个无 authority blob 暂留且已保全。
- Realm length 前后保持一致、脱敏指纹发生变化，而 mtime 没有变化；新增地雷是 Realm 写入证据不能只看 mtime，精确取证值只留仓库外恢复归档。临时 dynamic-only 工具构建 0 error / 1 条预期空 schema Fody warning。
- 数据 blocker 已解除；实机 gate 仍待用户反馈，`SV1-1` 未开始。

## 2026-07-10

### 产品决议修订：`oms-simple` 文件 fallback、`oms-complex` 上限包与 osu 社区式作者生态

- 用户否决最终程序化 rescue：主题化色块/辉光/默认节点会破坏产品规整性。当前 `OmsSkin` 只作为迁移脚手架保留到文件包 parity/完整性/原子恢复/实机 gate；V1 release 前退出产品渲染链，引擎仅保留通用 renderer、layout/event bridge、对象池与 gameplay truth。
- 两个验收包正式命名为 `oms-simple.osk` 与 `oms-complex.osk`，均在一个普通 `.osk` 内同时提供 mania/BMS，并与第三方完全同权。`oms-simple` 只保留可玩核心且成为只读 canonical 逐组件 fallback；`oms-complex` 覆盖完整公开 slot/event，证明 IIDX 级表达上限。
- canonical `oms-simple` 由发行物只读携带、校验并可原子恢复；其自身失败属于安装完整性故障，必须进入明确修复流程，禁止再生成隐藏程序化视觉。
- 制作者生态对齐 osu 社区：`.osk` 分发、根 `skin.ini`、mania 共同素材/动画命名、普通目录编辑与拖入导入；`[Bms]`/scene/script 作为版本化 ruleset 扩展且不要求 DLL。Skin Authoring Kit 定义为两包可编辑源、注释模板、字段/事件/layout/预算规范、validator/diagnostics 与打包说明，不是另一套 SDK/runtime。
- 本轮仅同步 PLAN/STATUS/CONSTRAINTS、架构审计、主线和制作者手册；无运行时代码或测试变化。

### Skin V1：完成 mania/BMS 现状审查，按“极简到 IIDX Showcase”重冻首版架构与路线

- **mania 上限结论**：普通 `.osk/[Mania]` 已有成熟素材、配置、帧序列和逐组件 fallback，但 key press、column light、LN hold、hit explosion、judgement/combo 等交互仍由固定 C# 驱动；`ISkin` 返回任意 `Drawable` 只是受信任编译期扩展，不是普通作者运行时。故 BMS 不能靠复制 `ManiaLegacySkinTransformer` 达到目标。
- **共享/分离决议**：共享 package/resource/ini codec、显式 presence、scene/template/animation、只读 event ABI、三态、诊断/reload/sandbox；mania 保留 stage/column/legacy 坐标 adapter，BMS 保留 scratch/P1-P2/DP/cover/gauge/BGA/gimmick adapter。采用 adapter-first 迁移，不在第一刀重写成熟 mania decoder。
- **作者上限**：引擎只拥有 gameplay truth、playfield/BGA layout、滚动/LN 裁剪、对象池、内容时钟和安全边界；外部 package 拥有具体 scene、动画和事件响应。V1 必须同时用公共 API 通过 Minimal（仅可玩核心）与 Showcase（IIDX 级表现），可选 judgement/combo/gauge/HUD/装饰允许显式 `Suppress`。
- **布局审计**：现有 5K/7K 已有 P1/P2/两种居中与左右皿、9K 居中、14K 双 deck/双皿/centre gap；但 HUD 会另建默认 geometry、BGA 使用固定 rect，CenterP2 对侧 BGA 不正确，14K 当前四个独立 BGA player 不是最终合同，geometry 也缺 finite/range 校验。路线新增唯一 `BmsGameplayLayoutSnapshot` 与单一 BGA content authority。
- **相邻缺陷**：确认 `buildLaneKeysoundTimelines()` 误用 key count 作 lane 上界，会丢 5K/7K 最右键及 14K K14/S2；sparse 7K/9K 的 keymode 启发式也可能低估。代码修复分别进入 P1-K/P1-J，P1-A 布局矩阵负责 smoke gate。
- **文档与验证**：新增 [Skin V1 架构审计](../../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)，重写 P1-A PLAN/STATUS/CONSTRAINTS 与创作者手册，并同步 mainline/P1-J/K/L。此切片无运行时代码改动；复跑 BMS parser/legacy/reference/render focused **43/43**、mania `TestSceneOmsBuiltInSkin` **84/84**，仅见既有 MessagePack 3.1.3 `NU1902` 告警。

### 皮肤系统恢复到可信基线，保全异常期历史并撤回未经验证的生产链

- **取证与选择**：以 2026-06-30 00:05（北京时间）为协作分界。严格分界前最后正式提交为 `b53b798`；采用 `2b27c09` 的树作为恢复基线，仅因为其 schema 56 patch 已存在于分界前 WIP `a4c3346`，同时避免对已打开过的用户 Realm 降 schema。没有移动/改写旧分支历史，而是用本次正常提交承载恢复结果。
- **完整保全**：恢复前 `9e37087`、dirty stash `4bde4c3`、不可达对象均固定在 `refs/archive/pre-recovery-20260710/*`；完整 bundle 与 production/release-test/appdata 运行时备份保存在 workspace 外恢复归档。归档可定点取证，不允许整包恢复。
- **保留**：F1 独立 `[Bms]` parser、`BmsLegacySkin`、`.osk` 导入路由、静态件颜色/纹理/几何、reference ini 自校验、程序化 `OmsSkin` 最终兜底；G1 只保留 folder ctor + `SkinInfo` 两字段/schema 56 载体。
- **撤回**：G1 `SkinManager` 生产分支/导入扫描/删改/热重载，F2 动态件、Lua、mania fallback adapter 与 reference-default 替换。原因包括外部路径 storage authority 错误、递归删除目标风险、启动扫描清理用户记录、错误 fallback 期待和 mania 默认资源回归。
- **独立修正**：`BmsLegacySkin` 复制流后重置 position 再交 base parser，恢复 `[General]/[Colours]/[Mania]` 解析；per-lane decoder 支持 `S2`，14K 第二皿 lane 映射到 P2 素材。测试覆盖 General/Mania 共存和 P1/P2 双皿选择。
- **验证**：H1/H2 focused **15/15**；BMS 全量 **1005/1005**；mania 默认 OMS 资源 **1/1**；Release **0 error / 20 warnings**。mania 全量 **787/791**（4 项既有 HoldNote auto-frame 期待失败），core skin focused **57/62**（1 项 Argon 旧期待 + 4 项已删 ruleset beatmap archive 依赖）。实机视觉仍待用户验收。
- **治理**：新增 [恢复审计](../../other/SKIN_SYSTEM_RECOVERY_20260710.md)，同步 mainline/P1-A/SKINNING/RELEASE/README；把 pre-cutoff Claude 记忆迁入 `.Codex/memory` 并建立恢复优先声明，`.claude/` 作为 legacy workspace 忽略。

## 2026-06-29

### `F1` ③刀（续）：纹理铺开 — lane 背景 / 分隔贴图 + composite 化 + 抽共享 `BmsSkinnableVisual`

承接 note 件全家纹理化，把 lane 背景 / 分隔从直接 `Box` 升级为支持「贴图优先 / 颜色回退」的 composite，并把单填充元素的解析逻辑收敛成一个可复用 helper（避免后续 hit target / cover / backdrop 各抄一份 `GetTexture` 链）：

- **新增** `BmsSkinnableVisual.Resolve(skin, imageLookup, colourLookup, keymode, defaultColour, out hasTexture, laneIndex, isScratch)`（`osu.Game.Rulesets.Bms/UI/`）：单填充元素的「贴图→`Sprite`·文件皮肤主导不吃程序化色 / 无贴图→`Box`·颜色 override 或默认」解析。`DefaultBmsNoteDisplayBase.CreateVisual` 改为薄委托（删本地 `GetTexture` 重复 + 多余 `Sprites` using）。helper 注释明确：只服务单填充件；多元素 composite（hit target / cover）自行读纹理分支。
- **`DefaultBmsLaneBackgroundDisplay` / `DefaultBmsLaneDividerDisplay` `Box→CompositeDrawable`**：构造期挂默认 palette `Box`（未 load 也有观感）；BDL 经 helper 解析。贴图＝per-lane `LaneBackgroundImage{lane}` / `LaneDividerImage{lane}`（lane = 数字或 `S`）；颜色回退沿用既有分组（背景 even/odd/scratch、分隔 scratch/非 scratch）。divider 新增 `LaneIndex`（transformer 传 `lookup.LaneIndex`，原仅 `IsScratch`），故贴图可按车道分桶；几何（CentreRight/Width=1/Y 轴）保持不变。
- **解析链补全**：`BmsSkinDecoder` 的 `per_lane_image` 正则加 `LaneBackgroundImage|LaneDividerImage` 前缀（按 lane token 存原值）；`BmsLegacySkin.resolveImageKey` 加两条 per-lane case。两者均落 ruleset 内、**零碰核心**。
- **分层澄清（沿用 note 件结论）**：放了 lane 贴图即由文件皮肤主导、**不吃**程序化颜色；颜色 config 只服务「不画贴图的轻量皮肤」+ 程序化 fallback 参数化（CONSTRAINTS 第 6 条）。
- **测试**：`BmsDefaultNoteSkinConfigTest` 加 lane bg / divider 纹理用例（有贴图→`Sprite`、颜色断言移内层 `ChildrenOfType<Box>`、divider 构造补 lane index）；`BmsLegacySkinTest` + `BmsSkinDecoderTest` 加 per-lane lane bg / divider 键解析用例（image 计数 3→5）；`BmsSkinTransformerTest` colour helper 注释更新（lane 件现亦 composite）。**BMS 全套 990/990 绿**（+4），无回归。
- **下一步**（仍属 `F1` ③刀）：hit target / cover 纹理 + 颜色（composite 多元素，自读纹理分支）→ barline（transformer Box 提成独立件 + 颜色）→ stage 框架 / `KeyImage`（无现成件，新增）→ backdrop/baseplate 颜色 → 几何（`BmsPlayfieldLayoutProfile.CreateDefault` 读 config）→ reference skin.ini。

### `F1` ③刀（续）：hit target / bar line / lane cover 颜色 + 纹理铺开

接着把三类带交互/多元素的件接上 ini 配置（颜色 override + 可选贴图），均落 ruleset 内、零碰核心：

- **hit target（`DefaultBmsHitTargetDisplay`，composite 6 颜色 lookup + 贴图）**：新增 `keymode` 字段（构造方 `BmsHitTarget` lambda + transformer 均传 `lookup.Keymode`）。BDL 读 bar/line/glow 三色 × scratch/非 scratch override（缺则 palette）。**贴图**＝`HitTargetImage` 存在则隐藏程序化 bar/line、加 `Sprite`（`Depth=1` 留在 press/focus 覆盖层之下）；glow 颜色提成字段 + `applyGlow()`，使后续 `ApplyLayoutProfile`（半径变化）不冲掉 ini glow 色。`BarHeight`/`LineHeight` 等几何内省属性不变（bar/line 仍在树内、仅 `Alpha=0`），`TestSceneBmsHitTargetState` 不破。
- **bar line（提成 `DefaultBmsBarLineDisplay`，颜色-only）**：transformer 内联 `Box` + `DrawableBmsBarLine` 内联默认 `Box` 统一替换为独立 `Box` 子类件，BDL 读 `MajorBarLineColour`/`MinorBarLineColour`（+keymode）override 或 palette。无 `BarLineImage` 槽（高度仍由父 bar line 的 layout profile 拥有），故颜色-only。`BmsSkinTransformerTest` bar line 类型断言 `Box`→`DefaultBmsBarLineDisplay`。
- **lane cover（`DefaultBmsLaneCoverDisplay`，颜色 3 lookup + 贴图 + keymode）**：display 自身经 `[Resolved(CanBeNull)] GameplayState` 解析 keymode（同 `BmsBgaPanel` 模式·**不改 mod/lookup 签名**）。`load()` 改 `load(ISkinSource)`，先解析 `LaneCoverFillColour`/`LaneCoverShadeColour`/`LaneCoverFocusColour`（缺则 palette）再建子树（cover 子树本就在 BDL 建、一遍到位）。**贴图**＝Sudden 读 `LaneCoverTopImage` / Hidden 读 `LaneCoverBottomImage`，存在则 base 用 `Sprite`、跳过程序化 shade 渐变（focus wash/edge 仍叠在上）。focus wash 保持 palette `FocusWash`（无独立 lookup），`LaneCoverFocusColour` 只 override focus edge。
- **测试**：`BmsDefaultNoteSkinConfigTest` 加 hit target 颜色/贴图、bar line 颜色、lane cover 颜色/贴图 5 用例（composite 件断言「存在某 `Box` 颜色==ini 值」/ 贴图断言 `ChildrenOfType<Sprite>`）。**BMS 全套 995/995 绿**（990→992 hit target→993 bar line→995 cover），无回归。
- **下一步**（仍属 `F1`）：stage 框架 / `KeyImage`（无现成件，新增组件 + lookup 接线）→ backdrop/baseplate 颜色（`PlayfieldBackdropColour`/`PlayfieldBaseplateColour`）→ 几何（`BmsPlayfieldLayoutProfile.CreateDefault` 读 config·static 无 skin·需调用方传）→ reference skin.ini 验收。

### `F1` ③刀（续）：playfield backdrop / baseplate 颜色 + backdrop 贴图（收口现存 shell 件）

把最后两个现存 shell 件接上 ini，至此**所有现存渲染件**（note 家族 / lane bg / divider / hit target / bar line / lane cover / backdrop / baseplate）均已配置化；剩余仅净新增件（stage 框架 / `KeyImage`）+ 几何 + reference skin。

- **backdrop（`DefaultBmsPlayfieldBackdropDisplay`）**：加 `keymode`（transformer `createDefaultPlayfieldComponent` 传 `lookup.Keymode`）。优先级＝**skin `PlayfieldBackdropImage`（文件皮肤主导·平铺 Sprite·跳过谱面背景模糊）> 谱面背景模糊（既有默认）> 无背景时 `PlayfieldBackdropColour`/palette 平涂**。`load()`→`load(ISkinSource)`。
- **baseplate（`DefaultBmsPlayfieldBaseplateDisplay`，Box 子类·颜色-only）**：加 `keymode`，BDL 读 `PlayfieldBaseplateColour` override 或 palette。
- **测试**：`BmsDefaultNoteSkinConfigTest` 加 baseplate 颜色 + backdrop 贴图 2 用例（backdrop 贴图断言「有 `Sprite` 且无 `BufferedContainer`」以区分 skin-texture 路径 vs 谱面模糊路径）。**BMS 全套 997/997 绿**（+2），core Release gate（`osu.Desktop.slnf`）通过。
- **本会话累计**：F1 ③刀颜色+纹理铺开五刀（lane bg/divider → hit target → bar line → lane cover → backdrop/baseplate），986→997。

### `F1` ③刀（续）：几何经皮肤驱动（profile 11 键 + LN body 宽）

把第三条数据轴（几何）接上 ini——异于颜色/纹理（渲染件 BDL 直读），几何须在有 skin 访问的 `BmsPlayfield` 层把 override 喂进 `BmsPlayfieldLayoutProfile.CreateDefault`：

- **`BmsPlayfield.applySkinGeometry`**：`load` 加 `[Resolved(CanBeNull)] ISkinSource`，按 keymode 读 11 个几何键（`PlayfieldWidth/Height`、`Normal/ScratchLaneWidth`、`Normal/ScratchLaneSpacing`、`HitTarget{Height/BarHeight/LineHeight/GlowRadius}`、`BarLineHeight`），用 `CreateDefault` 可选 override 重建 profile 并 `LaneLayout = CreateFor(beatmap, profile)`。**`HitTargetVerticalOffset` 故意不可皮肤化**（须锁 0 守 `scrollLengthRatio≡1` 的 GN/判定时序不变量，enum 本就排除）。**`if (!anyOverride) return` 守护**：无几何键时保留默认 profile 对象，**非皮肤（及非 OMS）游玩字节一致**。替换后的 profile 经 load 后即触发的 playfield-style 绑定流到已建 lanes（复用既有 `applyPlayfieldStyle` 机制，不新开重排路径）。
- **`LongNoteBodyWidth`**：唯一不在 profile 的几何量（LN body 件的 `Width`），在 `DefaultBmsLongNoteBodyDisplay.ApplyVisual` 读（默认 `0.5775` 提常量）。
- **测试**：`TestSceneBmsPlayfieldLayoutConfig` 加 `TestSkinGeometryOverridesStrictProfile`（包 `SkinProvidingContainer` 注入几何皮肤·断言 `LayoutProfile` 取皮肤值·未设键留默认·`HitTargetVerticalOffset` 锁 0；**区别于既有「ruleset config 滑块被忽略」用例**——后者仍绿，证非皮肤路径未变）+ `BmsDefaultNoteSkinConfigTest` 的 LN body 宽用例。
- **验证**：BMS 全套 **998/999**——唯一失败 `TestLegacyTranscodeFailureBecomesUnavailableAndLeavesNoPartialFile` 是 **预存的 BGA 视频缓存 temp 清理 race**（状态转 Unavailable 与删 `.tmp` 竞争·环境/负载相关），经 `git stash` 回到干净树重建后**同样失败**，证**与本改动零因果**（改动文件零涉 BGA/cache）；core Release gate（`osu.Desktop.slnf`）通过。
- **至此颜色 / 纹理 / 几何三轴均已皮肤化**；剩余仅净新增件（stage 框架 / `KeyImage`）+ reference skin.ini。

### `F1` reference skin.ini（验收 capstone + 创作者模板）

把"复现程序化默认观感"的参考皮肤落为**自校验的验收门 + 创作者模板**，覆盖已皮肤化的全颜色/纹理/几何面：

- **创作者模板** [doc_md/other/oms-bms-reference-skin/skin.ini](../../other/oms-bms-reference-skin/skin.ini)：7K 全键 + 分节注释 + 作者须知（`//` 注释、键全可选·缺省 fail-open 回默认、颜色 R,G,B[,A] 0–255、几何 px vs 相对、纹理键留空＝默认纯程序化观感、放图即文件皮肤主导该件）。其它 keymode 同键、仅 lane-count 派生的 `PlayfieldWidth` 默认不同。
- **验收门** `BmsReferenceSkinTest`（2 用例）：把参考 ini 经 `BmsLegacySkin` 解析，**逐键断言等于真实 palette / profile 常量**（几何对 `BmsPlayfieldLayoutProfile.CreateDefault(7K,8)`、颜色对 `BmsDefaultPlayfieldPalette.*`）。**自校验**＝模板里任何值写错、或某默认漂移，都会在此失败；并顺带证全键集端到端 round-trip（decoder→skin→config）。模板 ini 是该测试的镜像（注释版·decoder 剥 `//`），二者锁同步。
- **验证**：BMS 全套 **1001/1001**（含 reference 2 用例·全绿）。注：`TestLegacyTranscodeFailureBecomesUnavailableAndLeavesNoPartialFile`（预存 BGA 缓存 temp 清理 race）为**间歇 flaky**——本轮通过、几何刀那轮失败；已经 `git stash` 干净树复现证实与本工作零因果（属 P1-L·后台 chip 跟踪）。core Release gate 绿。
- **F1 状态**：ini 数据层 → 配置源 → 颜色/纹理/几何三轴 → reference 验收**均已落**；**剩余仅净新增件 stage 框架 / `KeyImage`**（当前 playfield 无边框/无 key 区·属新增视觉元素 + 定位决策；`KeyImage` 尤其与"无物理按键区"的现设计冲突，宜独立评估）。

### 皮肤后续路线勘察 + 立项 `G` 系列（存储/分发轨）+ 扩写 `F2`（纯文档·未开工）

应用户 2026-06-29 三问（皮肤可视文件夹管理 / 默认是否落成文件 / 能否还原 LR2·beatoraja·IIDX），勘察代码得三条确凿结论并写进 [DEVELOPMENT_PLAN](DEVELOPMENT_PLAN.md)：

- **① 皮肤存储**：现状皮肤走核心 `SkinImporter : RealmArchiveModelImporter<SkinInfo>`（`.osk` → realm **hash-backed `files/`**·哈希文件名·不可读/不可手管），**无 chartbms 式可视文件夹**；chartbms 可视文件夹模型（`BmsFolderImporter`·`chartbms/<名>-<hash8>/`·realm 仅索引路径）只为谱面建了。F1 gate（06-27）曾定"复用 SkinManager·不走 chartbms 旁路"——用户要求**重审**。→ 立项 **`G1` 皮肤可视文件夹存储**（仿 `BmsFolderImporter`·revisit F1 hash 决议·待拍板）。
- **② 默认皮肤**：确认 BMS 默认 **100% 程序化**（palette/profile via `DefaultBms*Display`·`OmsSkin` 内嵌 `Skins/Oms` 无 BMS skin.ini）；reference skin.ini 仅文档模板·未接成运行时默认。→ 立项 **`G2` 文件型默认皮肤**（可选·小·须保留程序化兜底）。
- **③ IIDX/LR2 还原度**：**部分**——F1 让现有静态件可换色/图/几何；但 **turntable / keyflash / hit explosion / bomb / LN hold light / ghost-TD 全仓零渲染**（grep 证·盘面亦无 turntable 区/键区），属 `F2`**未开工**，是"还原 IIDX"的真正大头。→ **扩写 `F2`**（结构前置 + 组件清单 + 分期 + P1-L 协作 + 红线）。
- **G 系列与 F 正交**：F1–F3＝"什么可被皮肤控制"，G＝"皮肤文件如何存放/管理"。优先级待用户拍板（见 PLAN「当前优先顺序」第 9 条）。**本轮纯文档·无代码改动。**

### `G1` 实现架构勘探落账 + 刀①（folder-backed 皮肤直读建块）

用户看路线图后确认"按推荐来：G1 打头"。勘察核心皮肤实例化/资源链得**可动工的 G1 实现架构**（进 [PLAN](DEVELOPMENT_PLAN.md) G1「实现架构」7 条 + 6 刀序），并落第一刀（ruleset-only·低风险）：

- **★关键架构发现：文件夹直读零改核心资源链**。`Skin` 基类把 `fallbackStore` 并入资源 `store`（skin.ini 经 `store.GetStream`、纹理经 `TextureStore`），realm `Files`（`RealmBackedResourceStore`：`SkinInfo.Files→hash`）先查、空则回落 `fallbackStore`；`OmsSkin` 正是用内嵌 store 当 fallbackStore。**故 folder 皮肤 = fallbackStore 换 `StorageBackedResourceStore(chartskin/<名>)`**（同 SkinManager 既有 `userFiles`），不进 hash、不碰 `Skin`/资源核心。
- **实例化定方案 D4**：`SkinManager.GetSkin` 对 `SkinInfo.FilesystemStoragePath` 非空的皮肤走分支，**反射调 `BmsLegacySkin(SkinInfo, IStorageResourceProvider, IResourceStore<byte[]> fallbackStore)` ctor**（核心不编译依赖 ruleset·同本会话 SkinImporter 路由范式）。
- **刀①（已落）**：`BmsLegacySkin` 加该 public folder ctor（委托既有 protected ctor·`[UsedImplicitly]` 供反射）；测试 `TestFolderBackedSkinReadsIniDirectlyFromDisk`——真实临时目录写 `skin.ini` → `StorageBackedResourceStore` 直读 → 断言 `[Bms]` 几何/颜色值解析自磁盘（空 realm Files 回落 fallbackStore 已证）。**BMS 全套 1002/1002 + core Release gate 绿**（纯 ruleset·零核心改动）。
- **下一刀 = ②realm 迁移**：`SkinInfo` 加 `string? FilesystemStoragePath` + `RealmAccess.schema_version` bump（核心 realm·加性 nullable·须跑现有 skin 读写测试 + Release gate）。

### `G1` 刀②（realm 迁移：`SkinInfo` 承载 folder-backed 字段 + schema bump）

把 folder-backed 皮肤所需的 realm 载体加进核心 `SkinInfo`，使皮肤实例化（刀③·D4 分支）能识别"文件夹皮肤"并定位其磁盘路径。**唯一一刀触及核心 realm schema**，故按"加性 nullable / 无数据迁移 / 跑全验证面"谨慎落地：

- **`SkinInfo` 镜像 `BeatmapSetInfo`**：加 `string? FilesystemStoragePath`（非空＝皮肤由可视文件夹 `chartskin/<名>/` 支撑、`Files` 留空、skin.ini + 纹理直读该路径）+ `bool IsExternalFilesystemStorage`（用户管理的外部只读目录，OMS 永不改名/删除/写入）。带 XML 注释指明镜像来源与 G1 填充点。
- **`RealmAccess.schema_version` 55→56**（注释：`Add SkinInfo.FilesystemStoragePath and SkinInfo.IsExternalFilesystemStorage … mirroring BeatmapSetInfo`）。**纯加性 nullable/scalar 字段无需 migration case**——`applyMigrationsForVersion` 的 `switch` 不加 `case 56`（与 v50–55 连续 6 个加性版本同模式：realm 自动加列填默认 null/false，迁移回调对该版本仅记日志）。
- **本刀同时加 `IsExternalFilesystemStorage`（PLAN 标"可选"）的决策**：刀④（导入/扫描器·managed/external 两态）确定需要该 flag；与其将来再 bump 一次 schema，不如此刻一次性加（beatmap 侧 `FilesystemStoragePath` v13 / `IsExternalFilesystemStorage` v54 分两次 bump，正是外部库特性后到所致的反例）。**未加** `ExternalLibraryRootPath`——皮肤"外部库根 vs 条目路径"嵌套语义尚未设计（皮肤比谱库简单·一文件夹一皮肤），属投机字段，留到刀④真需要时再定（届时再 bump）。本刀字段默认 null/false、**本刀不在任何生产路径写入**（填充在刀④），故无 copy/clone/serialise 逻辑需同步。
- **验证**：`osu.Desktop.slnf` Release 构建 0 错误；**BMS 全套 1002/1002**（整套在 schema 56 下创建/操作 realm）；核心 `osu.Game.Tests.Skins` 过滤 **57 通过 / 5 失败**——5 失败（`TestExportThenImportDefaultSkin` + `TestSceneBeatmapSkinResources` 4 项）经 `git stash` 干净树重跑**逐一同名重现**（"No valid beatmap files found in the beatmap archive"·`BeatmapImporter` 解析 osu 模式谱面归档·OMS 删 Osu/Taiko/Catch 后的预存失败），**与本 schema 改动零因果**；`.osk` 导入 / `OmsSkin` / SkinProvidingContainer 等不依赖 osu beatmap 的皮肤测试均在 57 通过内。
- **下一刀 = ③ `SkinManager.GetSkin` folder 分支（D4）**：对 `FilesystemStoragePath` 非空的皮肤反射调 `BmsLegacySkin` 的 public folder ctor（刀①已备）、传 `StorageBackedResourceStore(chartskin/<path>)`；守卫测试钉死反射字符串；非 folder/非 BMS 零变化。

## 2026-06-27

### BMS 素材 + ini 皮肤创作生态立项 + `F0` 契约冻结（纯文档，未开工实现）

把当前"临时应付"的纯代码型 BMS 皮肤升级成像 mania 那样「放文件夹 + `skin.ini` 即换皮」的产品，正式立项为 `P1-A` 下的 `F` 系列。本轮为大型**规划**，不含代码改动。

- **背景定性**：当前 BMS 皮肤唯一 authoring 入口是写 C# `ISkin.GetDrawableComponent()`（只有开发者能做），且默认皮肤是 **100% 程序化、零位图素材**（`BmsTemporarySkinPalette` 静态色板 + `BmsPlayfieldLayoutProfile` 几何）。缺的是 mania 已有的「素材 + ini 数据层」（osu legacy skin：`LegacyManiaSkinConfiguration` / `Decoder` / `ConfigurationLookup` / `ManiaLegacySkinTransformer` 按文件名约定取图）——BMS 侧这四件全无。
- **检索校准**：联网检索 osu!mania（skin.ini `[Mania]` 段 + 素材命名）、beatoraja（Lua 规范 + 元素族）、LR2（CSV `#SRC_/#DST_` 对象枚举）三套游玩界面皮肤规范，确认①"osu-ini 标准本身是静态素材型、不含 timer/op/keyframe 动态系统"②上一版标"缺少"的件（keyflash/bomb/turntable/ghost…）在生态里都是一等公民、是真缺口 ③许多件无 osu-ini 先例 → BMS ini 须 `[Mania]` 静态段 + `[Bms]` 扩展段，不照搬 `[Mania]`。
- **锁定决议（用户已拍板）**：游玩界面 only（lazer 已弃非游玩皮肤）/ 自有 `[Mania]`-对齐 + `[Bms]` 扩展段·按 keymode 分桶 / 程序化兜底（不可删）+ 参考素材皮肤·**不烤 PNG**（纯色→ini 键、辉光引擎绘制 ini 参数化）/ 加载期 fail-open + 诊断、编辑期更严、必备三档 / 手改 ini + 热重载 + 复用 lazer 布局编辑器（**决议 X**：BMS 专属 HUD gauge/combo/clear lamp 保持代码编排不升格为可拖摆 `ISerialisableDrawable`）/ 新 `BmsAssetSkin` 作为被 `BmsSkinTransformer` 包裹的 `ISkin`，**零改现有 lookup 契约**。
- **关于"代码渲染对象生成文件"的结论**：不建"代码对象 → PNG"导出器（纯色块烤图＝冗余 + 丢可缩放 + 辉光烤死）；默认皮肤文件化＝人工编写复现观感的 reference `skin.ini`（绝大部分是 ini 数值、位图极少），与 osu! 一致（Argon 程序化默认从不导出文件）。
- **权威层立账（本轮交付）**：契约冻结进 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)「皮肤创作生态（素材 + ini）约束」1–10（权威源）；分期 `F0–F3` 进 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)；状态进 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)；mainline `DEVELOPMENT_STATUS` / `DEVELOPMENT_PLAN` / `OMS_COPILOT` 已回写本立项。
- **`SKINNING.md` 降级重锚**：[../../other/SKINNING.md](../../other/SKINNING.md) 早先被直接写成"本文即 P0 契约"——属层级颠倒（`other/` 是参考材料、不替代主线计划/约束）。本轮纠正：`SKINNING.md` 改为面向制作者的**派生视图**，权威源指向本子线 CONSTRAINTS / PLAN。
- **`F0` 范围**：纯文档冻结，**无代码改动、无测试运行**。`F1`（`BmsAssetSkin` 加载器 / 校验器 / 热重载 + 参考皮肤，覆盖①类静态件）起未开工，排在主交付线之后。

### `F1` 实现架构勘探落账 + 修正立项期「兜底需重构」设想（纯文档，未开工实现）

应「评估皮肤开发面 + 开发准备」勘探 BMS 皮肤注入链与 fallback 链路，得到可动工的 `F1` 实现架构，并**纠正一处立项期错判**。无代码改动。

- **纠错：fail-open 已天然成立，撤回「兜底需重构」**：`SkinManager.AllSources`（`CurrentSkin` 后恒 yield `DefaultOmsSkin`）+ `RulesetSkinProvidingContainer`（`DefaultOmsSkin` 作链底 fallback）保证素材皮肤缺件经链式查找回退到链底 OmsSkin 的程序化兜底。`BmsSkinTransformer.providesBuiltInFallbacks = skin is OmsSkin` 是「仅链底默认皮肤兜底」的正确分层设计，F1 不得改。
- **核心 `LegacySkin` 硬编码 mania 段**：mania ini 解析（`LegacyManiaSkinDecoder` / `maniaConfigurations` / `GetConfig` 分支）嵌在 `osu.Game/LegacySkin` 是上游历史包袱；BMS 作为 ruleset 不得照搬侵入核心，`BmsSkinDecoder` / config / lookup 须落 `osu.Game.Rulesets.Bms`。
- **F1 实现架构确定项**：纹理走被包裹 skin `GetTexture`（不侵入）；结构化配置 ruleset 内独立解析；素材化＝改造现有 `DefaultBms*Display` 读 config（不另起 Asset* 全家桶、不烤 PNG）；落地顺序 = ini 三件套 → 配置源 → 单条 lookup 最小闭环 → 铺开①类件 + 校验器 + 热重载 → reference skin.ini 验收。
- **头号 gate（未决，动工前需拍板）**：BMS 结构化配置如何从 SkinManager 选中的皮肤读到——(A) 统一 OMS 皮肤实例化类型 /(B) 借资源句柄重解析 /(C) 独立 `skin.bms.ini`，推荐 (A)。
- **皮肤来源**：用户已定**复用 lazer SkinManager 皮肤体系**（皮肤作 SkinInfo 进列表、设置切换），不走 chartbms 旁路。
- 权威落账：[DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) F1「实现架构」+ [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)「皮肤创作生态」约束 11–12 + [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md) 同步。

### `F1` 动工：gate 拍板（方案 A）+ ini 数据层解析三件套落地 + 据代码更正 SKINNING.md

承接 `F1` 实现架构：用户拍板**头号 gate = 方案 A**（统一 OMS 皮肤实例化类型，导入皮肤解析 mania + BMS 段）+ 复用 SkinManager 皮肤来源；并确立 **schema 由代码实现确立、`SKINNING.md` 据代码派生**（纠正早先「拿 `[规划]` 草案当 schema 规范」的参考方向）。据此开写第一刀的纯解析可验证闭环（**纯新增、零碰现有代码**）。

- **新增** `osu.Game.Rulesets.Bms/Skinning/`：`BmsSkinConfigurationLookups`（逻辑键枚举，①类静态件）、`BmsSkinConfigurationLookup`（keymode + lane/scratch 查询载体）、`BmsSkinConfiguration`（per-keymode 数据：Geometry / Colours / ImageLookups）、`BmsSkinDecoder`（**独立 ini 解析器**，不继承核心 `LegacyDecoder`——其 `Section` enum 无 `Bms`——故 BMS 段解析完全不侵入 osu.Game；`[General] Keymodes` + per-`[Bms]` `Keymode:` 分桶、`//` 注释、fail-open 跳过未知键/畸形值）。
- **键集据代码实测**（非草案）：几何映射 `BmsPlayfieldLayoutProfile.CreateDefault`（`PlayfieldWidth/Height`、`Normal·ScratchLaneWidth` 1/1.5、`HitTarget{Height16/Bar12/Line3/Glow6}`、`BarLineHeight2`、`LongNoteBodyWidth0.5775`；`HitTargetVerticalOffset` 锁 0 不开放）；颜色＝IIDX 键色组（`NoteColourWhite/Cyan/Yellow/Scratch`）+ lane/divider/hit-target/barline/cover 色（对应 `BmsDefaultPlayfieldPalette`）；纹理走 per-lane `NoteImage{lane}[H/L/T]`/`KeyImage{lane}[D]` + 全局槽。
- **测试**：新增 `BmsSkinDecoderTest`（8 用例：keymode 分桶、几何/颜色/纹理解析、未知键忽略、pending-flush、畸形值容错）。**BMS 全套 969/969 绿**（Debug），无回归。
- **据代码更正 [SKINNING.md](../../other/SKINNING.md)**（修草案失真，相关节 `[规划]→[部分]`）：§3 keymode 删 `10K`（代码 `BmsKeymode` 无）+ 注释 `;`→`//` + 音符色非逐道；§4 几何表以 `BmsPlayfieldLayoutProfile` 真实键/默认值替换 mania 式 `HitPosition`/逐列 `ColumnWidth`；§5.4 颜色逐道 `ColourColumn{lane}`→IIDX 键色组；附录 C 字段约定校正；权威说明改为「代码是真实依据、本文据代码派生」。
- **下一步**（仍属 `F1`）：②刀方案 A 配置源接入（OMS skin 子类解析 BMS 段 + 导入实例化 + `GetBmsSkinConfig` 扩展）→ ③刀改造 `DefaultBms*Display` 读 config → reference skin.ini 验收。

### `F1` ②刀（机制层）：BmsLegacySkin 配置源 + GetBmsSkinConfig 扩展

按方案 A 落地「让 skin 应答 BMS 段配置」的机制层（**ruleset 内、零碰核心**）：

- 新增 `BmsLegacySkin : LegacySkin`（`osu.Game.Rulesets.Bms/Skinning/`）：override `ParseConfigurationStream`（**copy-first 快照 stream** → `base` 解析 general/mania 段 → `BmsSkinDecoder` 解析 BMS 段，对 base 是否消费流免疫）+ override `GetConfig<BmsSkinConfigurationLookup>`（几何 `float` / 颜色 `Color4` / per-lane+全局纹理路径，`typeof(TValue)` 守卫保 `SkinUtils.As` 硬 cast 安全）。**mania 段解析（核心 LegacySkin）完整保留**。补 protected fallbackStore 构造（对标 OmsSkin 内置 ini 路径）。
- 新增 `BmsSkinConfigExtensions.GetBmsSkinConfig<T>`（薄包装 → `GetConfig<BmsSkinConfigurationLookup,T>`，对标 mania `GetManiaSkinConfig`；缺 override 返回 null → caller fallback 程序化默认，对 fallback 链任何皮肤安全）。
- 测试 `BmsLegacySkinTest`（8 用例：几何/颜色/per-lane+scratch+全局纹理解析、缺键/错 keymode/**错类型返回 null 不抛**、**mania 段共存仍解析**）。BMS 全套 977/977 绿。
- **关键架构边界**：`BmsLegacySkin` 必须落 ruleset 程序集（core 不能引用 ruleset），靠反射 `InstantiationInfo` 接入——这正是②刀下半要做的、且触及 core `SkinManager` 导入实例化的敏感改动。
- **下一步**（②刀下半，触及 core，稳妥单独做）：让导入皮肤实例化为 `BmsLegacySkin` 使用户皮肤 BMS 段真正生效 → ③刀改造 `DefaultBms*Display` 读 config。

### `F1` ②刀（接入层）：导入皮肤路由到 BmsLegacySkin（方案 A，改 SkinImporter）

用户拍板「改 SkinImporter 写入点」，让导入的 plain `LegacySkin` 升级为 `BmsLegacySkin`，使用户皮肤的 `[Bms]` 段端到端生效。**最小 core 改动 + fallback 保护**：

- `SkinImporter`：新增 `resolveInstantiationInfo`，把 plain `LegacySkin`（且仅它）的 `InstantiationInfo` 写成 `BmsLegacySkin`（反射 `Type.GetType` 字符串、不编译依赖 ruleset）；import（:124）与 Save（:246）两个写入点统一走它。**fallback**：BMS 程序集不在场时解析为 null、不升级——非 OMS 环境（osu.Game.Tests 不加载 BMS）行为完全不变。
- `SkinnableSprite.isUserSkin` 连带点：升级皮肤变 `LegacySkin` 子类后落出精确 typeof allowlist。按 **FullName 精确匹配** `BmsLegacySkin`——审查发现 `is LegacySkin` 会误纳 `LegacyBeatmapSkin` 等子类、破坏 allowlist 排除语义，故弃用宽匹配。
- 守卫测试钉死 SkinImporter（assembly-qualified）+ SkinnableSprite（full name）两处 core 硬编码字符串解析到 `BmsLegacySkin`，防 rename 静默失效。
- 验证：BMS 全套 **978/978** + core Release gate（`osu.Desktop.slnf`）**通过（0 错误）**。**fallback 设计使 core 回归零风险**（两处改动对无 BMS 环境透明：`SkinImporter` 走 null 分支、`SkinnableSprite` FullName 永不匹配）。已知边界：core 其他「假设 plain LegacySkin」点（若有）靠 `BmsLegacySkin` 继承 `LegacySkin` 行为兼容兜底，建议实机验证导入皮肤换肤。
- **下一步 ③刀**：改造 `DefaultBms*Display` 读 `GetBmsSkinConfig`，让 ini 配置真正驱动渲染（端到端换肤）。

### `F1` ③刀（起步）：note 颜色端到端读 config —— ini→渲染链路打通

③刀首件 + **F1 端到端验证**：改 `DefaultBmsNoteDisplay` 经 `[BackgroundDependencyLoader] load(ISkinSource)` 读 `GetBmsSkinConfig<Color4>(GetNoteColourLookup(...))` ?? palette 默认。`BmsDefaultPlayfieldPalette.GetNoteColourLookup` 把 lane → IIDX 键色组 lookup（复用 `getNoteColourGroup`）。

- **证明 F1 整条链路通**：ini `[Bms] NoteColourWhite` → `BmsSkinDecoder` → `BmsLegacySkin` → `GetBmsSkinConfig` → `DefaultBmsNoteDisplay.Colour`。headless `BmsDefaultNoteSkinConfigTest`（override 用 ini 色 / 缺省回 palette）绿。
- **模式**：构造设 palette 默认，BDL 有 config 则覆盖——对 fallback 链中任何皮肤安全（缺 override 自动回退）。
- **下一步**：按此模式铺开其余①类件颜色（LN head/body/tail、lane bg/divider、hit target、barline、cover），再处理几何（`BmsPlayfieldLayoutProfile.CreateDefault` 读 config，模式不同）与纹理（Box→可选 Sprite，体量更大）。

### `F1` ③刀（续）：颜色件铺开 — LN head/body + lane bg/divider 读 config

按 note 模式铺开①类颜色件（构造设 palette 默认 + BDL 有 config 覆盖、对 fallback 链安全）：

- `DefaultBmsLongNoteHeadDisplay`（NoteColour 组）、`DefaultBmsLongNoteBodyDisplay`（active=NoteColour、broken=`GreyOutBroken(active)` 派生、三态不变）、`DefaultBmsLaneBackgroundDisplay`（+keymode 字段，LaneBackground even/odd/scratch）、`DefaultBmsLaneDividerDisplay`（+keymode 字段、transformer 传 `lookup.Keymode`，LaneDivider/Scratch）。
- palette 加 `GetLaneBackgroundLookup` + `GreyOutBroken`。
- 端到端测试扩到 7 用例（各件 ini 色 + note fallback）；BMS 全套 **985/985**，无回归（默认件 BDL 注入 `ISkinSource` 不破坏现有 load）。
- **剩余**：颜色 hit target（composite 多元素）/ barline（transformer Box→件）/ cover（+keymode）；之后几何（`CreateDefault` 读 config）/ 纹理（Box→Sprite）/ reference skin.ini。

### `F1` ③刀（续）：纹理 — note 件全家 Box→CompositeDrawable，贴图优先 / 颜色回退

打通素材皮肤的核心（换贴图），并落实**贴图 vs 颜色的 fallback 分层**（回应「文件皮肤为何还吃代码颜色」的质疑）：有 `NoteImage` 贴图 → 显示 Sprite、文件皮肤主导、**不吃程序化颜色**；无贴图 → 程序化 Box + `NoteColour` override 或 palette。

- `DefaultBmsNoteDisplayBase` 由 `Box` 重构为 `CompositeDrawable`：`CreateVisual` 解析「texture?Sprite:Box(colour)」，BDL `ApplyVisual` 挂内层 visual。4 件适配：note/head 走 base；LN body override（三态 active/broken 作用于内层 visual，贴图时 white tint + grey broken）；tail 仅在有贴图时显示（默认 tail-less）。各件构造挂默认 palette Box（未 load 也有正确观感、且避开构造期虚调用 CA2214）。
- 连带适配 `BmsSkinTransformerTest`（非 headless、11 处 `assertSingleColour`）：note 件颜色现移到内层 box，helper 改读 `ChildrenOfType<Box>`。
- 端到端 `BmsDefaultNoteSkinConfigTest` 加纹理用例（有贴图→Sprite、无贴图→Box fallback）；BMS 全套 **986/986**。
- **颜色定位澄清（产品）**：颜色 config 只服务「不画贴图的轻量皮肤」+ 程序化 fallback 参数化（CONSTRAINTS 第 6 条），放了贴图即不吃；auto-scratch/note 的颜色区分属 gameplay、不在皮肤主线。
- **剩余纹理**：lane / stage / hit target / cover 的贴图 + `KeyImage`；以及几何 / reference skin。

## 2026-06-21

### 默认皮肤长条 body 改造：增宽 10% + 同 head 色 + 三态游玩视觉（unactivated/activated/missed）

按用户要求把 BMS 默认皮肤的长条 body 从「静态暗条」改成「随游玩状态变化的同色亮条」。仅动默认皮肤 body 视觉与状态绑定，不碰 head/tail（tail 仍 `Alpha=0`）、判定/计分/滚动/键音/chartbms 直读。

- **增宽 10%**：`DefaultBmsLongNoteBodyDisplay.Width 0.525 → 0.5775`（相对车道宽，×1.1）。
- **body 颜色与 head 一致**：颜色源从 `GetLongNoteBody`（在 head 色上 darken 0.72 的暗条）改为 `GetLongNoteHead`（= 完整 note 色）；透明度保留 `0.8`（用户选项：body 仍比实心端帽略透、层次更清晰）。
- **三态游玩视觉（皮肤无关 + 默认映射）**：新增公开枚举 `BmsLongNoteBodyState { Idle, Holding, Broken }`（= 未激活/激活/miss）。父 `DrawableBmsHoldNote` 暴露 `IBindable<BmsLongNoteBodyState> BodyState`，每帧 `Update()` 从 `isHolding` + head/tail 判定**纯派生**（`isHolding`→Holding；head 未判→Idle；head·tail 均 IsHit 的成功收尾→Holding；其余＝头判 miss/中途断开→Broken）。默认 body（`DefaultBmsLongNoteBodyDisplay`）经 `[Resolved] DrawableHitObject`→cast `DrawableBmsHoldNote` 绑定该 bindable（与 mania `DefaultBodyPiece`/`ArgonHoldBodyPiece` 同范式，`DrawableHitObject` 带 `[Cached]`，body 在 `SkinnableDrawable` 内可解析父对象），按状态 80ms 切视觉：**unactivated 与 activated 完全一致**（head 色 + 0.8）、**missed 变淡＝去色变灰 + 降透明度**（新增 `BmsDefaultPlayfieldPalette.GetLongNoteBodyBroken`：朝亮度 0.85 去色 + dim 0.45，alpha 0.32）。非游玩上下文（皮肤回退查询，无父 hold note）解析为 null → 维持 Idle 默认观感，安全。
- **HCN 恢复天然成立**：Broken→重新击打→`isHolding` 重新 true→派生回 Holding，无需特例（与 P1-E 同步把「松开重按接回」收窄为 HCN-only，故 LN/CN 中途松开后 body 保持 Broken 直到淡出，仅 HCN 可恢复）。
- 测试同步：`BmsSkinTransformerTest` 两条 LN body 回归（width `0.5775`、色＝head 色 `YellowKeyNote`/`ScratchNote`、alpha `0.8`）；`BmsDrawableRulesetTest` 新增 `TestBodyStateFollowsHcnHoldLifecycleWithRecovery`（Idle→Holding→Broken→Holding）+ 在 CN 用例断言 Broken。验证：BMS 全套 **936/936**、`osu.Game.Rulesets.Bms.Tests` 0 错。**人工实机视觉验收待用户确认**。详见 [P1-E CHANGELOG](../P1-E/CHANGELOG.md) 2026-06-21（CN 机制更正）与记忆 `reference_bms_default_skin_geometry`。

## 2026-06-20

### playfield 顶边贴屏幕边缘 + combo 移到 playfield 中心并去背景色块（用户实机三连改之一二）

- **playfield 顶边回到屏幕边缘**：上一版用 `PLAYFIELD_VERTICAL_OFFSET=0.06` 把整条 play 立柱下移，导致顶边离开屏幕顶、留出 header 空带，不符合 green-number「音符从屏幕最顶出现、整屏可见场 = 顶边→判定线」语义。本次**删除 `PLAYFIELD_VERTICAL_OFFSET`**，playfield 恢复纯顶部锚定（`Y=0`）；为保持判定线/gauge 仍停在原低位（~0.92 屏高），把 `DEFAULT_PLAYFIELD_HEIGHT 0.86→0.92`（顶边贴边 + 场更高、音符从顶出现）。判定时序不变量不变（`HitTargetVerticalOffset=0` → `scrollLengthRatio≡1` → `TimeRange`/GN 与场高无关，仅像素扫过距离变）。`gauge_top = DEFAULT_PLAYFIELD_HEIGHT + 0.002`（不再含 offset 项）。同步 `BmsLaneLayoutTest` / `TestSceneBmsPlayfieldLayoutConfig`（0.86→0.92）、`TestSceneBmsHudGaugePlacement`（去掉 offset 项）。
- **combo 移到 playfield 中心 + 去背景色块**：`DefaultBmsHudLayoutDisplay` 新增 `applyComboPlacement()`，把 `BmsComboCounter` 放到 **playfield 宽/高中线交点**（水平＝按 `PlayfieldStyle` 的 P1 左 / P2 右 / 居中得 playfield 横向中心、复用与 gauge 同一套 `PlayfieldWidth` + inset；垂直＝`PlayfieldHeight/2`），`Anchor=TopLeft, Origin=Centre, RelativePositionAxes=Both`，随 PlayfieldStyle live 重定位；无 GameplayState/config 宿主降级屏幕中心。`BmsComboCounter` 去掉 `TextComponent` 里的 `body` 色块容器（background 渐变 / glow / accentStrip / 圆角边框），只留居中的 `COMBO` 标签 + 数字（pulse/flash 改作用在数字上、带 Shadow）。
- 回归：`TestSceneBmsHudGaugePlacement.TestComboCentredOnPlayfield`（combo Origin=Centre、相对定位、居中 X=0.5、Y=PlayfieldHeight/2）。验证：BMS 全套 **930/930**、`osu.Desktop.slnf` Release **0 错误**。**人工实机视觉验收待用户确认**。

### 修复：BMS gauge 被通用"血条显示"开关误隐藏（用户实机"gaugebar 没了"）

去掉默认 combo / leaderboard 后用户报「gaugebar 没了」。一轮日志驱动诊断（先排除 strip：真实 `BmsGaugeBar` 在 strip 后布局里仍可见 → 不是 strip）后定位真因：**`BmsGaugeBar : HealthDisplay`，而 `HealthDisplay.LoadComplete` 把自身绑到 `[Resolved] HUDOverlay.ShowHealthBar`，`ShowHealthBar==false` 时 `this.FadeTo(0)` 把 gauge 淡到透明**。某处（NoFail 等通用"隐藏血条"开关 / 设置）把 `ShowHealthBar` 设 false → BMS groove gauge 被一起隐藏（而 combo 不受影响，故"combo 在、gauge 没"）。诊断盲区：之前的 gauge 摆位测试没有真实 `HUDOverlay`（`hudOverlay` 解析为 null → `showHealthBar` 恒 true → gauge 恒可见），掩盖了该路径。

- **修复**：`BmsGaugeBar` 解析 `[Resolved(CanBeNull)] HUDOverlay`，在 `LoadComplete` 里订阅 `ShowHealthBar` 变化并 `FinishTransforms()+Alpha=1` 重申满显——BMS groove gauge 是核心游玩信息，**免疫** 通用血条开关，始终显示。该订阅在 base 之后注册（bound-copy 订阅者先于 own 订阅者触发），故稳定压过 base 的淡出；HUD 整体 `ShowHud` 淡入仍经父级生效、不受影响。
- 回归：`TestSceneBmsSoloPlayerPreStart.TestGaugeBarStaysVisibleWhenHealthBarHidden`（真实 Player+HUDOverlay，置 `ShowHealthBar=false` 后断言 gauge 仍可见——去掉修复即失败）；另补 `TestSceneBmsHudGaugePlacement` 的 `TestRealGaugeLoadsAndIsVisible` / `TestRealGaugeVisibleAlongsideStrippedWrappedHud`（真实 gauge 在布局/strip 后可见）。验证：BMS 全套 **929/929**、`osu.Desktop.slnf` Release **0 错误**。

### gauge 下移到判定线下方 + 矩形化 + 等宽镜像 playfield（游玩区抬高，视觉/摆位，P1-A E1）

把原先摆在 playfield 顶部的圆角胶囊 gauge 改为 IIDX groove-gauge 观感的矩形条，落在判定线下方、与判定区等宽并随 P1/P2/居中侧锚；同时抬高游玩区腾出下方空带。用户在规划阶段选定「抬高 0.86 / gauge 等宽」。

- **抬高游玩区**：`BmsPlayfieldLayoutProfile` 默认 `PlayfieldHeight 0.95 → 0.86`（提为公开常量 `DEFAULT_PLAYFIELD_HEIGHT`，仍是 strict profile 唯一杠杆，config `PlayfieldHeight` 维持被忽略）。判定线上移到 86% 屏高、下方 ~14% 空带容纳 gauge。**判定时序不变**：`HitTargetVerticalOffset=0` 时 `BmsHitObjectArea.scrollLengthRatio≡1`、`TimeRange` 与场高无关，仅落条像素扫过距离变短（视觉略密），GN / 判定窗口完全不变。
- **gauge 矩形化**：`BmsGaugeBar` 圆角 `CornerRadius 10→0`、bar 高 `20→28`、数值字号 `14→18`，新增 10 等分极淡竖向刻度（`Opacity 0.08`）营造 groove-gauge 观感（不做 IIDX 逐格细节）；填充 / floor band / clear 标记 / 高光与 `NORMAL`+`20%` 文案均保留。
- **gauge 下移 + 等宽 + 侧锚镜像**：默认摆位由 `DefaultBmsHudLayoutDisplay` 负责——gauge `RelativeSizeAxes.X` + `Width = PlayfieldWidth`（与 lane 条带等宽）、`RelativePositionAxes.Both` + `Y = PlayfieldHeight + 0.012`（顶边贴判定线下方）、Anchor/Origin/X 按 `PlayfieldStyle.GetAppliedStyle(keymode)` 做 P1 左 / P2 右 / 居中（复用 `BmsPlayfield.SIDE_ANCHORED_HORIZONTAL_INSET`，与 lane 严格同列）。combo 暂留原位。
- **合同保持（满足「HUD 宿主约束 1」）**：gauge 仍留在 `IBmsHudLayoutDisplay.SetComponents(wrappedHud, gauge, combo)` 合同内，**未改签名**、未迁出 HUD。所需几何经 HUD 可见的 DI 通道取得：`PlayfieldWidth / keymode` 经 `[Resolved] GameplayState` 可玩谱面（`BmsLaneLayout.CreateFor`）、`PlayfieldStyle` 经 game 级 `IRulesetConfigCache.GetConfigFor(bms)`（与 playfield 子树同一 `BmsRulesetConfigManager` 实例，绑定 live 变化）；两者均 `CanBeNull` 解析，皮肤编辑器预览 / 测试等无 `GameplayState`/config 的宿主优雅降级（居中 + 兜底宽度 `0.4`），不抛异常。
- 仅视觉 / 摆位 / 几何，不碰判定 / 计分 / 滚动 / 键音 / chartbms 直读。`BmsPlayfield` 的 `side_anchored_horizontal_inset` 提升为公开常量 `SIDE_ANCHORED_HORIZONTAL_INSET` 供 HUD 复用（值不变）。
- 测试同步：`PlayfieldHeight 0.95→0.86`（`BmsLaneLayoutTest`、`TestSceneBmsPlayfieldLayoutConfig` 两处断言）；新增 `TestSceneBmsHudGaugePlacement`（等宽 / 判定线下方 / 居中 / P1 侧锚镜像）。`BmsSkinTransformerTest` 的「HUD 含 gauge」回归保持绿（gauge 仍是 HUD 子件）。验证：BMS 全套 **925/925**、`osu.Desktop.slnf` Release **0 错误**。

### （其二）gauge 与 playfield 一体化跟进（用户实机反馈）

首轮实机后用户反馈「间隙再贴紧 + gauge 别像外挂控件、要和 playfield 一体」。本跟进只动 `BmsGaugeBar` 视觉与摆位间隙，几何/合同链路不变：

- **贴紧判定线**：`DefaultBmsHudLayoutDisplay` 的 gauge 顶边偏移 `PlayfieldHeight + 0.012 → +0.002`（近乎贴住判定线下方）。
- **背景与 playfield 衔接**：gauge 背板改用海军蓝渐变 `BmsDefaultHudPalette.GaugeTrackTop(26,32,48) → GaugeTrackBottom(13,19,31)`，落在 playfield lane/baseplate 色域内，使 gauge band 读作 playfield 立柱的底段而非独立卡片。
- **去边框 + 单条 band**：移除 gauge 四周 1px 边框，改为仅在顶边一条 gauge-accent 着色 1px hairline（`topAccent`）作为"表头"提示；填充条占满整条 band。
- **label/value 叠加**：取消浮在空隙里的独立 header 行，把 `NORMAL` 标签（左中）与百分比数值（右中，字号 18→20）叠加在 band 上（IIDX groove-gauge 式），均加 `Shadow` 保证压在填充色上的可读性；band 高 `28→34`。
- 新增调色 `GaugeTrackTop/GaugeTrackBottom`（不动既有 `TrackBackground`/被分布图复用的 `TrackShade`）。验证：BMS 全套 **925/925**、Release **0 错误**。

### （其三）整条 play 立柱整体下移（用户实机反馈，标注目标位）

> ⚠️ **已于同日撤销**：用户随后要求"playfield 顶边贴屏幕边缘"，本节引入的 `PLAYFIELD_VERTICAL_OFFSET` 已删除、改为提高 `PlayfieldHeight` 到 `0.92`（顶边贴边、判定线/gauge 仍停在 ~0.92）。**当前真相见下方「playfield 顶边贴屏幕边缘」一节**；本节仅留作迭代历史。

用户实机标注希望「整体往下移动」到近屏底。新增共享常量 `BmsPlayfieldLayoutProfile.PLAYFIELD_VERTICAL_OFFSET = 0.06`，把 **playfield 条带 + gauge 一体下移**：`BmsPlayfield.playfieldContainer` 顶部锚定后置 `Y=OFFSET`（`RelativePositionAxes` X→Both；顶边不再贴屏幕顶、留 header 空带），`DefaultBmsHudLayoutDisplay.gauge_top` 同步加该 OFFSET，使判定线落在 `≈0.92` 屏高、gauge 紧贴其下止于近屏底。两者共用同一常量保证不错位。`PlayfieldHeight` 仍 `0.86`、判定时序不变量不受位移影响（位移只改像素扫过位置、不改 GN / 窗口）；lane 高占比断言仍 `0.86`（高度未变），`TestSceneBmsHudGaugePlacement` 的"判定线下方"断言改用 `OFFSET+0.86`。验证：BMS 全套 **925/925**、Release **0 错误**。**人工实机视觉验收待用户确认**（位移幅度 `0.06` 为单一可调常量）。

### BMS gameplay 从默认皮肤配置中移除游玩排行榜与重复（默认）连击数（用户反馈）

用户要求把「默认皮肤左下角连击数 + 左侧排行榜」**从默认皮肤配置中删去（非运行时隐藏）**。两者**同源**：上游 `LegacySkin.GetDrawableComponent` 的 ruleset-`MainHUDComponents` 默认布局里直接 `new LegacyDefaultComboCounter()` + `new DrawableGameplayLeaderboard()`（[LegacySkin.cs:420/422](../../../osu.Game/Skinning/LegacySkin.cs)），经 `BmsSkinTransformer` 包成 BMS HUD 的 wrapped 层。中央金色 combo 是 BMS 自有 `BmsComboCounter`、保留；右上 score 等来自全局（Ruleset==null）层、不受影响。

- **修复＝装配期移除**：`BmsSkinTransformer` 在 wrap BMS `MainHUDComponents` 时调 `stripDefaultHudElements(wrappedHud)`，把 wrapped 容器直接子里的 `ComboCounter` / **`LegacyDefaultComboCounter`** / `DrawableGameplayLeaderboard` **从配置树移除**（`Container.Remove(..., true)`），三类根本不进入 BMS HUD 树（不渲染、不进皮肤编辑器序列化、无首帧闪烁）。BMS combo 是 SetComponents 另行添加、不在 wrapped 层，故移除 wrapped 层所有 combo 安全；对无这些件的皮肤优雅 no-op。
- **坑（首次实机暴露）**：上游默认连击是 **`LegacyDefaultComboCounter : CompositeDrawable, ISerialisableDrawable`，并非 `ComboCounter` 子类**——只匹配 `ComboCounter` 时 leaderboard 被删、连击仍在。故 strip 必须显式包含 `LegacyDefaultComboCounter`，回归测试也用真实类型而非 `: ComboCounter` 的假替身（后者会误过）。
- **回退前一版的"隐藏"式尝试**：撤掉 `BmsSoloPlayer.Configuration.ShowLeaderboard=false`（及其 `TestGameplayLeaderboardSuppressed`）与 `DefaultBmsHudLayoutDisplay` 的 foreign-combo 有界重试隐藏（恢复原一次性循环，仅作残留 combo 兜底）——改用单一"配置移除"机制。
- 回归：`BmsSkinTransformerTest` 新增 `TestRulesetHudStripsDefaultComboAndLeaderboard`（wrapped HUD 放入 combo+leaderboard → 装配后从 wrapped 层移除、BMS combo 保留）。验证：BMS 全套 **926/926**、`osu.Desktop.slnf` Release **0 错误**。**人工实机确认左下角连击与左侧排行榜消失**。

## 2026-06-15

### BMS 默认皮肤几何二调：宽度回宽 10%、SCRATCH = 键轨 2 倍、音符贴顶无空隙

- **整体宽度 +10%**：`BmsPlayfieldLayoutProfile` 的 `PlayfieldWidth` 系数 `0.75 → 0.825`（原始 ×0.75 后再 ×1.1，覆盖上一条的 −25% 净值为 −17.5%）。
- **SCRATCH 轨 = 键轨 1.5 倍宽**：`ScratchLaneRelativeWidth` `1.25 → 1.5`（先定 2 倍，随即按口径再缩 25% 落到 1.5；归一化分配，scratch:key = 1.5:1）。
- **音符贴屏幕顶、无空隙**：`BmsPlayfield` 的 `playfieldContainer` 由居中锚定改为**顶部锚定**（初始 `TopCentre` + `applyPlayfieldStyle` 的 P1→`TopLeft`/P2→`TopRight`/居中→`TopCentre`），`PlayfieldHeight 0.9 → 0.95`。顶边贴屏幕顶（音符从顶部出现），底边/判定线仍在 95% 屏高（位置不变）。判定时序不受影响（GN = 可见时间 = `TimeRange`，与场高无关；场高只改像素扫过距离）。
- 仅几何/视觉。测试同步：`BmsLaneLayoutTest`（14K 宽 0.6→0.66、高→0.95）、`TestSceneBmsPlayfieldLayoutConfig`（8 轨宽 0.36→0.396、高→0.95、scratch 1.25→1.5、实测 scratch:key 比→1.5、lane 高占比→0.95）。验证：BMS 全套 **907/907**、Release 0 错误。

### BMS 默认皮肤：单轨/音符宽度 −25%、音符厚度 +25%、长条身宽 +25%（视觉/几何默认）

- **单轨宽度 −25%（音符随轨 −25%）**：lane 物理宽由「归一化占比 × `PlayfieldWidth`」决定（见 `BmsPlayfield.applyLaneBounds`），相对宽缩放会被归一化抵消，故唯一物理杠杆是 `PlayfieldWidth`；`BmsPlayfieldLayoutProfile.CreateDefault` 默认 `playfieldWidth` 乘 `0.75`，整条 playfield 连同 lane/音符等比收窄 25%、不引入新间隙。
- **音符厚度 +25%**：`DrawableBmsHitObject` 音符条高 `18 → 22.5`（普通音符 + 长条头/尾盖；长条父件 `28` 为非时长 fallback、被滚动容器覆盖，非可见厚度，不动）。
- **长条身宽 +25%**：`DefaultBmsLongNoteBodyDisplay.Width` `0.42 → 0.525`（相对 lane 宽）。
- 仅几何/视觉，不碰判定/计分/滚动。`PlayfieldWidth` 配置项仍是被忽略的 disabled 设置（strict profile），本次只改 profile 默认。测试同步：`BmsLaneLayoutTest`（14K 0.8→0.6）、`TestSceneBmsPlayfieldLayoutConfig`（8 轨 0.48→0.36）、`BmsSkinTransformerTest`（长条身宽 0.42→0.525 ×2）。验证：BMS 全套 **907/907**、Release 0 错误。

### BMS 默认皮肤：长条去掉尾端标识（视觉默认）

`DefaultBmsLongNoteTailDisplay`（长条释放端全宽亮色端盖）改为 `Alpha = 0`。长条 body 细竖条本就被滚动容器按 hold 时长拉满整段，隐藏尾盖后 body 仍延伸到释放端、不留空缺；最终样式＝头端亮盖 + body 延伸、无尾盖。仅改默认渲染：tail 仍是判定对象（判定/计分不受影响），`BmsNoteSkinElements.LongNoteTail` 组件与 `GetLongNoteTail` 调色保留，皮肤作者可覆盖。验证：`BmsSkinTransformerTest` + `BmsDrawableRulesetTest` **163/163**、Release 0 错误。

### BMS HUD 宿主合同简化：移除 gameplay-feedback overlay 变体（随 P1-C 速度反馈卡删除）

`P1-C` 按产品决定删除常驻速度反馈卡后，`P1-A` 拥有的 BMS HUD 宿主合同同步收窄：移除 `IBmsHudLayoutDisplayWithGameplayFeedback` 变体、`DefaultBmsHudLayoutDisplay.WrapWithGameplayFeedback` 与 transformer 的 legacy overlay 包装分支，`IBmsHudLayoutDisplay` 回到单一 `SetComponents(wrappedHud, gauge, combo)` 合同。`BmsGameplayFeedbackLayout` 收窄为只负责 **judgement 基线摆位**（`GetJudgementAnchor/Offset`、`ApplyJudgementDefaults`，仍由 `DrawableBmsJudgement` 与 `TestSceneBmsJudgementDisplayPosition` 消费）；其 gameplay-feedback 摆位常量 `DefaultGameplayFeedbackPosition`/`ApplyGameplayFeedbackDefaults` 已删除。删除细节与功能影响见 [P1-C CHANGELOG](../P1-C/CHANGELOG.md) 2026-06-15。HUD fallback 红线不变（默认路径 / 无该组件用户皮肤 / 旧接口用户皮肤三条回归仍由 `BmsSkinTransformerTest` 守）。验证：BMS 全套 **907/907**、Release 0 错误。

### 修复：皮肤布局编辑器进 BMS gameplay 报错（HUD 宿主组件序列化往返断裂）

- **背景**：审查皮肤编辑器链路时，用户实机日志暴露进 BMS gameplay 预览（`SkinEditorOverlay+EndlessPlayer`）时两处 error，均指向 `osu.Game.Rulesets.Bms.UI.DefaultBmsSpeedFeedbackDisplay`：`SkinComponentToolbox.attemptAddComponent`（组件 toolbox 反射实例化）与 `SerialisedDrawableInfo.CreateInstance`（用户皮肤布局重建）。
- **根因**：该速度反馈卡（P1-C 拥有）实现 `ISerialisableDrawable`，但唯一构造是全可选参数 `(IBindable?=null, IBindableList?=null)`；`Activator.CreateInstance(type)` 只匹配真正零参构造，全可选签名抛 `MissingMethodException`。`SkinnableContainer.Reload` 又会把 `BmsSkinTransformer` 在 `MainHUDComponents` 注入的 HUD 子件（gauge/combo/speed feedback）作为 `Components` 序列化进皮肤，故任何在 BMS HUD 上的编辑保存后、重载即崩。姊妹件 `BmsGaugeBar`/`BmsComboCounter` 均有无参构造、往返正常，唯独此卡缺失。
- **修复**：① 给 `DefaultBmsSpeedFeedbackDisplay` 补显式无参构造（链到现有构造，双参去掉可选默认值；双参唯一调用点是 `TestSceneBmsSpeedFeedbackDisplay`，零参调用点是 transformer，均无影响）；② `SerialisedDrawableInfo.GetAllAvailableDrawables` 增加"必须有公开无参构造"过滤，作为编辑器对所有 ruleset 的防御性收口。`IsEditable` 维持与 gauge/combo 一致（默认可编辑），不改编辑器可选面语义。
- **范围说明**：日志中 `VideoDecoder faulted`（被预览谱面的 BGA 视频）与缺失 jacket 属谱面自身、已优雅降级，非编辑器缺陷，本次不处理。皮肤编辑器链路本身仍是治理空白（`SKINNING.md` / 本子线尚未把编辑器作为正式 authoring surface 纳入约束），后续若把编辑器升格为皮肤自定义入口需另立专题。
- **验证**：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-build -c Release --filter "FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **121/121**；`dotnet build osu.Desktop.slnf -p:Configuration=Release` **0 错误**。

## 2026-05-26

### BMS -> mania 公共表面：persisted converted-star display 与 spread display 收口

- `BMS -> mania` 公开表面当前已不再只停留在 visibility gate：modless converted mania 星数现已改为持久化到 BMS metadata payload，并由 `BeatmapDifficultyCache`、`BackgroundDataStoreProcessor` 与 current-ruleset spread display 统一读取，因此 Song Select 的星数筛选、难度排序、按星数分组与 spread dots 都不再继续直接吃 source BMS raw star。
- 这一步仍保持 `P1-A/P1-K` 边界：`P1-K` 继续拥有 dedicated conversion、sample-only scratch runtime 与 persisted-star authority，`P1-A` 只消费 current-ruleset resolved-star display surface，不把 generic convert heuristics 重新包装成语义 authority。
- 当前剩余工作已收窄为按钮 wording、显式入口文案与更宽 presentation/manual proof，而不是再回头修 current-ruleset star surface。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore --filter "Name~BmsStarRatingResolverTest|Name~BeatmapCarouselFilterSortingTest"` **19/19** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

## 2026-05-16

### 实现：pre-start 1 号普通轨纯视觉流速预览宿主接到 `P1-A`

- `BmsHitObjectArea` / `BmsLane` 现已提供独立 preview 容器，pre-start 视觉预览不再需要借道 HUD / toast / mania lookup。
- `DrawableBmsRuleset` 现会把 skinnable fake note 固定挂到第一非 scratch 普通轨，并继续复用 BMS note fallback。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~TestSceneBmsSoloPlayerPreStart"` **24/24** 通过。

### 文档规划：pre-start 1 号普通轨纯视觉流速预览的宿主边界归到 `P1-A`

- 已明确该 feature 的 `P1-A` 职责只包括 playfield / lane 宿主、skin fallback 与产品表面，不拥有判定 / 计分 / 键音语义本身。
- 文档现已把 preview 宿主冻结为 BMS-owned playfield / lane visual surface：继续复用 BMS note lookup / fallback，不准塞进 HUD / toast，也不准误用 mania lookup。
- 本轮仅更新文档与 memory，无生产代码变更、无新增测试执行。

## 2026-05-09

### shared installation surface 跟进：数据目录迁移入口与结果说明收口

- Settings → 常规 → 安装位置 现已把入口明确为 `更改数据目录位置`，不再把实际只切换/迁移运行时数据根的功能误写成移动程序文件。
- 迁移选择页当前会直接说明三类结果：空目录直接迁入、非空非数据目录改用其下 `oms/` 子目录、已是可用数据目录则仅在重启后切换；这条产品面合同也已同步到 Release / 主线 / `P1-H` 文档口径。
- 验证：`dotnet build .\osu.Game\osu.Game.csproj -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

## 2026-05-09（续）

### shared settings-entry surface 跟进：osu!mania 滚动速度提示收口为参考值

- `ManiaSettingsSubsection` 现已为 `滚动速度` slider 补上 hover 提示，明确括号毫秒只代表标准车道几何下的参考下落时间。
- 不同 mania 皮肤可通过车道尺寸、判定线位置与缩放改变可见下落长度，因此同一数值不保证跨皮肤体感一致；更换皮肤后应按当前皮肤重新校准，且 mania / BMS 的下落时间不可互相参考。
- 这次改动不修改 `DrawableManiaRuleset.ComputeScrollTime()` 或 mania runtime authority，只收口 settings-entry surface 的解释边界。
- 验证：`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### gameplay settings-entry surface 跟进：Hi-Speed 模式说明与基础下落时间收口

- `BmsSettingsSubsection` 现已把 `Hi-Speed 模式` 的 hover 文案改为三种模式的功能区别简述：`Normal` 为基础定速、`Floating` 为按谱面初始 BPM 做补偿、`Classic` 为传统 Hi-Speed 语义。
- 当前模式的 Hi-Speed slider 现会在数值后显示括号内的基础下落时间（ms）；该数值明确按“不启用 `Sudden / Hidden / Lift`”计算，不再与 runtime `GreenNumber` / 可见时间混写。
- 当前提示文案也已收口为“括号内为不启用 sudden/hidden/lift 的下落时间（ms），绿字（GreenNumber）需要在游戏内结合 sudden/hidden/lift 调节查看”。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsRulesetConfigurationTest"` **12/12** 通过。

### gameplay settings-entry surface 跟进：BMS 键音通道默认值与悬浮提示收口

- `BmsKeysoundStore.DEFAULT_CONCURRENT_CHANNELS` 现已从 `16` 提高到 `32`；`Settings -> 游戏模式 -> BMS -> 键音通道数` 继续作为 shared keysound pool ceiling 的 `1..256` 调节入口。
- `BmsSettingsSubsection` 现为该滑条补上多行 hover 提示，直接概括低值更容易截断 BGM / 键音 / 长按尾音，高值更适合极高密谱面或较强机器；由于默认值已经是 `32`，缺音时的上调建议现已明确收口为 `48/64`。
- 这次改动属于共享 settings-entry surface 的默认值与文案收口，不改写 `BmsKeysoundStore` 的 runtime authority；BGM / note / LN / lane replay 仍共用同一池，运行时改值仍会切断当前正在播放的键音。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsRulesetConfigurationTest|FullyQualifiedName~BmsDrawableRulesetTest"` **70/70** 通过。

### shared settings-entry surface 跟进：桌面端输入设置安全隐藏 upstream mouse/touch/tablet 分区

- `OsuGameDesktop` 现已 override `CreateSettingsSubsectionFor(InputHandler)`，在 desktop Settings -> 输入 中对 `ITabletHandler`、`TouchHandler` 与 `MouseHandler` 返回 `null`，因此上游通用的数位板 / 触屏点击 / 鼠标 subsection 不再继续暴露给最终桌面产品面。
- 该改动明确是共享 settings-entry surface 的 **安全隐藏**，不改变 `MouseDisableButtons` / `MouseDisableWheel` / `ConfineMouseMode` / `TouchDisableGameplayTaps` 等既有 runtime config 消费链，也不移除 tablet/touch/mouse handler。
- 裁剪保持在 `OsuGameDesktop` 层，不下移到 `OsuGameBase`，从而继续保留 test scene / 非 desktop host 的设置装配合同。
- 验证：`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

## 2026-05-08

### gameplay speed setting 跟进：`阻止谱面开始/ingame start` 宿主语义收口

- `BmsInputStrings.PreStartHold` 的设置面可见名称现已改为 `阻止谱面开始/ingame start`；默认键位与 `UI_LaneCoverFocus` 的独立 click-to-cycle 语义保持不变。
- `BmsSoloPlayer` 现把 `UI_PreStartHold` 收口为“前 5 秒阻止开始 + 全程调速修饰键”这一宿主合同：右侧 `READY HOLD` overlay 继续只保留给前 5 秒阻止开谱窗口，正式 gameplay 开始后按住同一键仍会继续调速，并持续刷新居中的 `BMS speed` toast。
- `BmsInputManager` 现会在 hold 修饰键按住期间停止把新的 lane action 转发进 gameplay `KeyBindingContainer`，因此同一组 lane 键在 hold 期间只承担 Hi-Speed 调节，不再同时进入正常判定链。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~OmsInputRouterTest"` **9/9** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~TestSceneBmsSoloPlayerPreStart"` **10/10** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~OmsInputBridgeTest"` **23/23** 通过。

## 2026-04-28

### onboarding surface 跟进：难度表预设导入失败提示中文化

- `ScreenBehaviour` 现继续通过反射调用 `BmsDifficultyTableManager`，但导入 zris 预设失败时不再直接把英文异常透传给用户；首次启动页与 BMS settings 现统一复用 `DifficultyTableImportErrorFormatter`，把超时、HTTP 失败与格式错误收口为中文分类提示。
- 首次启动页在一次导入多张预设失败时，状态文字与通知现在都会展示失败摘要和前几条具体原因，而不是只停留在“成功/失败个数”。
- 该改动维持 `P1-A` 的共享 onboarding surface 归属，不改变 `osu.Game -> osu.Game.Rulesets.Bms` 的反射边界，也不把难度表后端实现重新归线到共享层。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsDifficultyTableManagerTest" --logger:"console;verbosity=normal"` **12/12** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### gameplay speed setting 跟进：pre-start overlay owner contract 与真实宿主绑定回归补强

- `TestSceneBmsPreStartHiSpeedOverlay` 现单独锁住 `BmsPreStartHiSpeedOverlay` 的 owner contract：mode text / value text 必须继续反映当前 tri-mode hi-speed surface，并沿 `BmsHiSpeedMode.FormatValue()` 输出；odd/even lane hi-speed adjustment 只在 overlay 可见时受理。
- `TestSceneBmsSoloPlayerPreStart` 现扩到 **8/8**：除既有 delayed-start / hold gate / target cycle / external clock suppression 外，还锁住“delay 到期但 hold 仍按住时继续可调速”以及“overlay mode/value 在真实 player flow 中反映当前 tri-mode surface”两条真实宿主链。
- 当前口径同步收口为 `UI_PreStartHold` 承担 hold gate、`UI_LaneCoverFocus` 保持 click-to-cycle；提前松开后的 authority 以 `SelectedHiSpeed` 是否变化为准，而不是把 routed key press 返回值当作唯一判断。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --configuration Release --filter "FullyQualifiedName~TestSceneBmsPreStartHiSpeedOverlay"` **3/3** 通过；`dotnet test osu.Game.Rulesets.Bms.Tests --configuration Release --filter "FullyQualifiedName~TestSceneBmsSoloPlayerPreStart"` **8/8** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

## 2026-04-23

### onboarding surface 跟进：首次启动向导收口为 OMS 六步流程

- `FirstRunSetupOverlay` 现已固定为六步：欢迎、UI 缩放、获取谱面、导入、难度表设置、按键绑定；这次变更维持主归属 `P1-A`，不为 onboarding / settings-entry surface 新开子线。
- 获取谱面页现改为 mania / BMS 外部站点导流与内部谱库补扫提示；导入页直接复用 `ExternalLibrarySettings`；难度表页通过反射调用 `BmsDifficultyTableManager` 导入 zris 镜像预设；最后一步复用全局、mania 与 BMS keybinding subsection。
- 欢迎页、获取谱面页与导入页的可见文案现已切到 OMS-owned localisation namespace + `.resx`，解决简中继续命中上游翻译的问题；手动重新打开向导并进入旧“游戏表现”页导致的 blank panel / unhandled error 也已一并修复。
- 验证：`dotnet test osu.Game.Tests --filter "FullyQualifiedName~TestSceneFirstRunScreenBehaviour|FullyQualifiedName~TestSceneFirstRunSetupOverlay|FullyQualifiedName~TestSceneFirstRunScreenImportFromStable" --configuration Release` **11/11** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

## 2026-04-22

### gameplay mod surface 修复：冷启动恢复与 startup cache 时序补全

- `OsuGameBase` 现不再把 startup 早期 `RulesetConfigCache` 未 ready 的 path 当作 ruleset failure；BMS mod memory 会先允许无 config 的首轮 apply，并在 cache ready 后 replay 当前 ruleset，补做 `PersistedModState` 恢复。
- 该修复同时消除了启动期误报的 `BMS` / `osu!mania` ruleset issue 通知，以及完全冷启动第一次进入 BMS 时 selected mod 与 remembered settings 丢失的问题。
- 新增 `BmsStartupModPersistenceIntegrationTest`，用“两段式 host 冷启动”回归锁定 BMS 冷启动恢复路径：先 seed `PersistedModState`，再用第二个同名 host 启动 `OsuGameBase`，断言 `BmsModSudden` 选中状态与配置成功恢复。
- 验证：`dotnet build .\osu.Desktop\osu.Desktop.csproj -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过；`dotnet run --project .\osu.Desktop\osu.Desktop.csproj -c Release` 进入 MainMenu 且最新 runtime log 不再出现 startup ruleset 错误；`BmsStartupModPersistenceIntegrationTest` + `BmsModStatePersistenceTest` 合计 **4/4** 通过；手测确认冷启动 / 运行中关开 / 切 mania 往返的 BMS mod 记忆均正确。

## 2026-04-21

### gameplay mod surface 跟进：BMS mod 选项与配置持久化

- `OsuGameBase` 现通过 ruleset-level mod persistence hook 在 BMS ruleset 切入 `BmsModStatePersistence`；当前选中 mod 顺序与 remembered settings 会写入 `BmsRulesetSetting.PersistedModState`，完全重启或切到其他 ruleset 再切回 BMS 时恢复，且不影响 mania。
- `ModSelectOverlay` 不再对实现 `IPreserveSettingsWhenDisabled` 的 configurable BMS mod 在 deselect 时 reset 默认值；`Auto Scratch` / `Auto Note` / `Random` / `Gauge Auto Shift` / `Judge Rank` / `Sudden` / `Hidden` / `Lift` 现在关闭再开启仍保留最后配置。
- `Sudden` / `Hidden` / `Lift` 现新增 `Remember gameplay changes` 开关，默认开启；局内滚轮调整可选择回写到持久化配置，而不是只停留在 gameplay clone 内。
- 验证：定向 `BmsRulesetConfigurationTest`、`BmsModStatePersistenceTest`、`BmsRulesetModTest` 合计 **56/56** 通过；独立输出目录 `Release` 构建通过。

### gameplay surface 跟进：`Playfield Style` 替换数值型 horizontal offset

- `BmsSettingsSubsection` 已移除数值型 `游玩区域水平偏移`，`BmsRulesetConfigManager` 改为声明四态 `Playfield Style`：`1P（居左）`、`2P（居右）`、`居中（左皿）`、`居中（右皿）`。
- 当前基础实现只作用于 single-play 5K / 7K：`1P（居左）` 与 `2P（居右）` 都会侧停靠但保留固定屏侧间距，scratch 视觉分别在左 / 右；两种 `居中` 都保持 playfield 居中，仅改变 scratch 视觉是在左还是右。9K 固定居中，14K 固定双侧布局。这不是完整 `1P/2P flip`，不会翻 bindings，也不会提前承诺 side-aware skin/HUD/BGA 合同。
- 验证：定向 `BmsRulesetConfigurationTest`、`BmsPlayfieldAdjustmentContainerTest`、`BmsLaneLayoutTest`、`TestSceneBmsPlayfieldLayoutConfig`、`BmsDrawableRulesetTest`、`BmsScrollSpeedMetricsTest` 合计 **92/92** 通过；`Build osu! (Release)` 通过。

### gameplay speed setting 跟进：移除 `Playfield Scale` 残余 surface

- `BmsSettingsSubsection` 已移除 `游玩区域缩放`，`BmsRulesetConfigManager` 也不再声明 `PlayfieldScale`；BMS settings surface 不再提供会破坏皮肤编排的整体缩放入口。
- `BmsPlayfieldAdjustmentContainer` 现明确固定为 identity transform；这样 settings / runtime 不会再通过用户缩放或数值横向偏移扭曲 strict visual-speed surface。
- 验证：后续同日回归已扩大到 `BmsLaneLayoutTest`，合计 **90/90** 通过；`Build osu! (Release)` 通过。

## 2026-04-20

### gameplay speed setting 跟进：pre-start hold integration coverage 扩面

- `TestSceneBmsSoloPlayerPreStart` 现额外锁定两类 `BmsSoloPlayer` 预开谱时序语义：提前松开 `UI_PreStartHold` 时 gameplay 仍必须继续等待 delayed-start 到时，以及 hold 期间 persistent target cycle 不得破坏临时 `Hidden` 覆写与松开后的 target 恢复。
- 同一 scene 也补上奇偶列调速双向回归，确认 paused pre-start overlay 下 odd-key 增速与 even-key 减速都能走通正式输入桥。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~TestSceneBmsSoloPlayerPreStart"` **5/5** 通过。

### gameplay speed setting 跟进：tri-mode Hi-Speed surface 与 pre-start hold operator surface 落地

- `BmsHiSpeedMode`、`BmsHiSpeedRuntimeCalculator`、mode dropdown + current-mode slider 已接通；settings 现可在 `Normal / Floating / Classic Hi-Speed` 三种模式间切换，并只显示当前模式数值。
- `DrawableBmsRuleset` 现按模式发布 runtime metrics / HUD detail / toast；`Classic` 继续锁定 `HS 10 + WN 350 => GN 300`，`Floating` 首轮为 initial-BPM anchored surface，但仍不宣称完整 `FHS`。
- `BmsSoloPlayer` 与 `BmsPreStartHiSpeedOverlay` 已把 5 秒 delayed start、`UI_PreStartHold` hold gate、奇偶键调速，以及 paused pre-start 下 `UI_LaneCoverFocus` / 滚轮 / 中键的 lane-cover 调整链接入正式 gameplay 流程；`SoloSongSelect` 则改为反射创建 `BmsSoloPlayer`，避免跨项目编译期依赖。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~BmsRulesetConfigurationTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~BmsDrawableRulesetTest"` **97/97** 通过；`Build osu! (Release)` 通过。

### gameplay speed setting 跟进：strict Classic Hi-Speed + frozen geometry surface 落地

- `DrawableBmsRuleset` 已把 Classic Hi-Speed 的 base time 从上游 mania 的 `11485 / HS` 改为官方 sample 对齐的 `(100000 / 13) / HS`，并由 `BmsScrollSpeedMetricsTest` 锁定 `HS 10 + WN 350 => GN 300`。
- `BmsPlayfield` 不再在运行时消费 playfield / receptor / bar-line 的 layout override，`BmsSettingsSubsection` 也已移除会扰动 strict profile 的 geometry sliders；内部 `BmsPlayfieldLayoutProfile` abstraction 仍保留给 ruleset / skin 侧使用。
- 当前公开 `Classic Hi-Speed` 范围仍保持 `1.0 - 20.0`，但这次已不只是范围收口，而是把 strict Classic surface 一并锁定。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~BmsRulesetConfigurationTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~TestSceneBmsPlayfieldLayoutConfig|FullyQualifiedName~BmsLaneLayoutTest|FullyQualifiedName~BmsDrawableRulesetTest"` **91/91** 通过；`Build osu! (Release)` 通过。

### gameplay feedback display 跟进：live `PERFECT / FC / FC LOST` 资格线复用现有 snapshot

- `BmsJudgementCounts` 新增 `CanStillPerfect / CanStillFullCombo`，随后又补入 `LeastSevereFullComboBreakResult / LeastSevereFullComboBreakCount` 派生语义，`DefaultBmsSpeedFeedbackDisplay` 现可在不扩 `BmsGameplayFeedbackState` 的前提下显示带紧凑原因标签的 live `PERFECT / FC / FC LOST` 状态线。
- 本次变更确认一部分 richer judge display 语义可以保留在 display 侧派生，而 recent timing history 与 aggregate snapshot 的分层不变。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsExScoreProgressInfoTest|FullyQualifiedName~BmsExScorePacemakerInfoTest|FullyQualifiedName~BmsJudgementCountsTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **69/69** 通过；`Build osu! (Debug)` 通过。

### aggregate gameplay feedback state contract 第四刀：live EX progress 并入 snapshot

- 新增 `BmsExScoreProgressInfo`，把当前 `EX-SCORE / MAX EX-SCORE` 快照为轻量值对象，并并入 `BmsGameplayFeedbackState`。
- `DefaultBmsSpeedFeedbackDisplay` 现继续沿同一 aggregate snapshot contract 显示 live `DJ LEVEL + EX 原始分子/分母 + %`，而 recent timing history 仍保持独立列表态。
- 新增 `BmsExScoreProgressInfoTest`，并扩展 `BmsGameplayFeedbackStateTest`、`BmsRulesetModTest`、`TestSceneBmsSpeedFeedbackDisplay`，锁定 EX 进度值语义、snapshot 镜像与文案显示。
- 验证：后续沿同一 feedback family 的聚焦回归已升至 `dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsExScoreProgressInfoTest|FullyQualifiedName~BmsExScorePacemakerInfoTest|FullyQualifiedName~BmsJudgementCountsTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **69/69** 通过；`Build osu! (Debug)` 通过。

### aggregate gameplay feedback state contract 第三刀：compact judgement counts 并入 snapshot

- 新增 `BmsJudgementCounts`，把 live score statistics 快照为轻量值对象，并并入 `BmsGameplayFeedbackState`。
- `DefaultBmsSpeedFeedbackDisplay` 现继续沿同一 aggregate snapshot contract 显示两行 compact live judgement summary，而 recent timing history 仍保持独立列表态。
- 新增 `BmsJudgementCountsTest`，并扩展 `BmsGameplayFeedbackStateTest`、`BmsRulesetModTest`、`TestSceneBmsSpeedFeedbackDisplay`，锁定 counts 映射、snapshot 值语义、初始镜像与文案显示。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsJudgementCountsTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **59/59** 通过；`Build osu! (Debug)` 通过。

### aggregate gameplay feedback state contract 第二刀：timing visual range 并入 snapshot

- `BmsGameplayFeedbackState` 现已额外包含 `TimingFeedbackVisualRange`，把 timing sparkline 的最后一个 scalar 输入也并入 aggregate snapshot。
- `DefaultBmsSpeedFeedbackDisplay` 现已收口为消费 `GameplayFeedbackState` 加 `RecentJudgementFeedbacks` 列表，不再直接额外绑定 `TimingFeedbackVisualRange` scalar。
- 新增 `BmsGameplayFeedbackStateTest` 并扩展 `BmsRulesetModTest`、`TestSceneBmsSpeedFeedbackDisplay`、`BmsSkinTransformerTest`，锁定 snapshot 值语义、ruleset 镜像和 sparkline/expiry 行为不回退。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~BmsSkinTransformerTest"` **153/153** 通过；`Build osu! (Debug)` 通过。

### aggregate gameplay feedback state contract 首刀落地

- 新增 `BmsGameplayFeedbackState`，把 speed metrics、target-state、最近判定与 fixed AAA pacemaker 这批 scalar feedback 收口为单个 BMS-owned snapshot。
- `DrawableBmsRuleset` 现额外暴露 `GameplayFeedbackState` bindable；`DefaultBmsSpeedFeedbackDisplay` 已改为优先消费该 aggregate state，而不是继续直接绑定多组 ruleset scalar bindable。
- recent timing history 与 visual range 暂时仍保持独立状态流，不把列表态硬塞进同一个 snapshot。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsGameplayFeedbackLayoutTest|FullyQualifiedName~TestSceneBmsJudgementDisplayPosition"` **154/154** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### shared judgement / feedback position contract 首轮落地

- 新增 `BmsGameplayFeedbackLayout`，把默认 gameplay feedback 摆位与 judgement 基线收口到同一条 BMS-owned 位置合同。
- `DrawableBmsJudgement` 不再持有独立的 `140px` judgement 偏移常量，`DefaultBmsHudLayoutDisplay.ApplyGameplayFeedbackDefaults()` 也已统一改为消费 shared contract。
- 新增 `BmsGameplayFeedbackLayoutTest`，并扩展 `TestSceneBmsJudgementDisplayPosition`，锁定 shared contract 的默认摆位与 direction-aware judgement 基线。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsGameplayFeedbackLayoutTest|FullyQualifiedName~TestSceneBmsJudgementDisplayPosition|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **117/117** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### 子线正式建档

- `P1-A` 已从旧的自由命名专题目录中拆出，成为 `doc_md/subline/P1-A/` 的正式子线入口。
- 本子线现固定维护 `DEVELOPMENT_PLAN.md`、`DEVELOPMENT_STATUS.md`、`CHANGELOG.md`、`TECHNICAL_CONSTRAINTS.md`，并与 `P1-C` 保持交叉联动。
- 当前仅完成文档重构与联动挂接，未新增构建或测试执行。
