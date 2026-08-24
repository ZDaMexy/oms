# Skin C2 完成交接与 C3 执行入口（2026-08-24）

> **已签发**：C2 的真实产品纵切、宽回归、Release、文档门与四项独立终审已闭合，P1-A 权威状态推进为 **`2/7 closed，C3 active`**。本文冻结 C2 产品决定、participant/holder inventory、并发/owner/mutation协议和后续不得放宽的边界，并签发文末 C3 持久执行 prompt。当前master的有意义提交由主任务在本轮文档检查后创建；本文不预写尚未生成的commit hash，也不授权push。

## 闭门结论

C2 已接通并签发一个真实可达的 current revision 产品纵切：用户从 Settings → Skin 点击 `Reload current skin`，`SkinManager` 对 ordinary Realm `.osk`、managed folder 与 external folder 的同一 record ID 准备新的 immutable content revision，在 update thread 的 participant barrier 一次发布 current selection/owner/revision；失败保持 exact 旧 pair/revision，成功后旧 owner 只在最后 consumer/work lease detach 后 exactly-once retire。

这只关闭七个持久 campaign 中的第二项，不等于 Skin V1 整体完成。G1、`SV1-2`、`SV1-1`、Skin V1 与 release 均未完成，`V-001`～`V-004` 仍为 0/4；程序化 `OmsSkin` 仍是 protected fallback authority。

## 唯一 Reload 触发与 live 边界

| 候选 | 真实 host 证据 | 决定 |
| --- | --- | --- |
| Folder Skin Workspace 行级 Reload | Workspace 只管理 committed folder record，ordinary Realm `.osk` 不在列表；行级 authority 也不等于 current pair | 不新增第二个 source-specific Reload |
| legacy Skin Editor / author-preview | legacy authoring 已 fail-closed；preview 挂载 `RulesetSkinProvidingContainer`，属于 live gameplay host | 不作为 Reload host，不以 editor 绕过 publication |
| Settings → Skin 的 current selection | `SkinSection.ReloadCurrentSkinButton` 可覆盖三种 current source，并从真实 UI 调用 `ReloadCurrentRevisionAsync()` | **唯一产品触发** |
| watcher / same-value selection | scanner 仍只启动扫描一次；selection same-value 仍短路 | 不实现 watcher，也不把 selection 冒充 reload |

按钮只在 current revision source 为 Realm package、managed folder 或 external folder 且 current pair coherent 时启用。结果反馈固定为：成功、未发现文件变化、live gameplay/preview 拒绝、当前 screen/participant/source 暂不可安全重载且旧 revision 仍 active，以及一般失败仍保留旧 revision。

live gameplay 与 gameplay preview 的语义是 **prepare 前确定拒绝**，不是先切 pair 再延后 consumer。`RulesetSkinProvidingContainer` 及 `PlayerLoader` initial player graph 注册 `LiveGameplayHost`；manager 在 capture participant snapshot 时先返回 `LiveGameplayActive`，不会开始 source capture、解析或 provisional owner 准备。用户须退出 gameplay/preview 后重试。其它不具 staged receipt 的 visual consumer 同样确定性拒绝，并提示返回安全 screen；旧 pair/revision 不变。

## 真实产品纵切

```text
SkinSection.ReloadCurrentSkinButton
  -> SkinManager.ReloadCurrentRevisionAsync (update thread admission)
  -> exact participant/current selection/owner/revision/source snapshot
  -> background source capture + capsule/parser/resource preparation
  -> every participant ready + staged immutable swaps and leases
  -> update-thread reversible reference barrier
  -> guarded CurrentSkinInfo / CurrentSkin / CurrentRevision pair
  -> old revision ConsumersDetached + WorkDetached
  -> update-thread exactly-once owner retire
```

`CurrentSkinInfo` 与 `CurrentSkin` 均不再是可由普通 bindable 反向写入的 authority。guarded root/copy 禁止替换或解除 authority binding，UI 只持单向 projection；observer reentry/exception 不得把 selection、owner 与 revision 拆成 split pair。

## production participant / holder inventory

本 inventory 从 production inheritance、DI source、直接 `Skin`/`ISkinSource`/texture/sample/capsule 字段、异步 load callback 与跨 fade 持有点反向清点。类型按其对 **current selected owner** 的实际关系分类；beatmap-local skin 与 ruleset resource store 是独立 authority，不因同处 provider tree 被误算为 current owner。

### 1. 必须 coherent 重建的视觉 consumer

| production object graph | current revision 行为 | C2 证明边界 |
| --- | --- | --- |
| `RulesetSkinProvidingContainer` → `BeatmapSkinProvidingContainer` → core/mania/BMS gameplay provider tree | 整棵树为 `LiveGameplayHost`；持有 transformed current source、beatmap-local source与fallback source | live gameplay/preview 在 source prepare 前拒绝；不开放在线重建 |
| BMS `BmsPlayfield`/lane/backdrop/baseplate、`BmsAsyncNoteDrawable`、Note/LN、barline、hit target、lane cover、judgement、background/BGA 与 pre-start preview | 均位于 live provider 下；async note/materializer另持 exact work lease | 真实 BMS pre-start 拒绝保 A；成功 publication 后 late-attached production Note/LN renderer只取得 B |
| mania Stage/Column/key area、Note/Hold、barline、hit target/explosion、judgement及 core ruleset drawable/HUD/playfield | 均位于 live provider 下，直接或经 `SkinnableDrawable` 消费 current source | 真实 mania player 拒绝保 A；不得把玩法中的逐件 `SourceChanged` 冒充 coherent reload |
| generic `SkinReloadableDrawable` family：`SkinnableDrawable`、`SkinnableContainer`、`SkinnableSprite/Text` 及其 song-select/results/HUD/BMS/mania 实例 | load 前先注册 temporary coherent blocker；load 后注册 exact participant lease | 只有提供 staged receipt 的派生类可跨 barrier；默认无 receipt 时确定性拒绝。`BmsAsyncNoteDrawable`/`SkinnableContainer`的首次admission及SourceChanged rebuild由GameHost scheduler推进，但outer `Loading`期间须等到`Ready`；base Dispose取消pending skin change，禁止non-alive local scheduler吞掉rebuild或晚到发布 |
| ordinary `SkinProvidingContainer`/`BeatmapSkinProvidingContainer`（非 live root） | source array 与fallback lookup可跨 frame 持有当前 owner | 当前未提供 staged source-array swap，attached 时 fail-closed；自然 detach 后重试，late instance绑定已提交 revision |
| `StarFountain` | background prepare 新 texture，commit 只交换 `spewer.Texture` | B 在 barrier 前不可见；abort 保 A；attach/detach 令 participant generation 失效并 fresh retry |
| `PoolableSkinnableSample` | background prepare 新 `DrawableSample`，commit 只交换 prepared sample 引用 | B coherent发布；正在播放的 A tail另持 work lease，停止/销毁后才允许 A retire |
| skin-sprite `DrawableStoryboardSprite` / `DrawableStoryboardAnimation` | 直接从 global skin source查 texture/frame，当前无 staged swap | initial load 与 loaded participant都 fail-closed；真实 storyboard detach 后才允许 same-ID reload |
| `Loader`、`IntroScreen`、`IntroWelcome` 的未完成 screen/sequence load | 候选 graph 尚未完成时无法给出 staged receipt | temporary coherent blocker从 candidate 创建前保持到 transfer/reclaim；无不可见 load 空窗 |
| `PlayerLoader` 的未完成 player load/handoff | candidate player 已可持完整 gameplay graph | temporary `LiveGameplayHost`；退出、取消、push失败与shutdown均先 reclaim graph再 detach |

### 2. 只持 revision lease、负责最后 detach 的生命周期 holder

| holder | 持有内容 | detach / retire 语义 |
| --- | --- | --- |
| `SkinBackground` 与 `BackgroundScreenDefault` | exact `Skin` owner、menu background texture、delayed/pending background graph | publication可先到 B，旧 `SkinBackground` 在真实 cross-fade 结束后释放 A holder；最后 detach 才 retire A |
| `PoolableSkinnableSample.ActiveRevisionChannel` 与 pending swap cleanup | historical `DrawableSample`、playing channel、exact A work lease | 先从 drawable hierarchy同步移除/销毁旧 sample，再释放最后 work lease，避免 owner先退役令尾音中断 |
| `BmsAsyncNoteDrawable` / `BmsManagedPackageNoteProvider` materializer work | prepared visual、outer callback、owner-internal materializer generation、exact revision lease transfer | cancel/supersede先发取消但必须 join 真正退出；callback fault、dispose和shutdown各自 exactly-once claim/reap |
| manager current pair、reload/mutation operation 与 rollback transaction | manager lease、provisional owner、protected fallback rollback lease | pre-commit失败只 retire provisional；fallback已commit后的失败持 A operation lease直到 exact rollback或transaction completion |
| participant registry 与 retire queue | 每个 attached consumer 的 exact participant lease；已无 lease 的 revision | participant/work detach fence分离；retire排入update thread，protected fallback只退役wrapper而保留可复用 `OmsSkin` owner |
| `PendingAsyncDrawableOwnership<T>` 覆盖的 background/storyboard/editor/results/statistics/screen/player graph | worker、未parent candidate、scheduled callback及其 participant/work cleanup | worker完成、callback跳过/抛错、parent dispose、cancel和shutdown之间只有一方取得最终 ownership；framework callback与ownership sentinel必须在同一scheduler FIFO（Editor固定为ScreenContainer scheduler），不得双 dispose或漏 lease |

### 3. 经代码与真实 host 证明不持有旧 current owner 的对象

| object | 证明 |
| --- | --- |
| `SkinnableSound` / `PausableSkinnableSound` aggregate wrapper | 自身只控制 descendant，显式 `ParticipatesInCurrentRevision=false`；实际 sample/resource/tail 由每个 `PoolableSkinnableSample` 登记 |
| guarded selection/instance bindable、Settings/Workspace row与notification | 只投影 committed value或持 record ID/immutable label，不拥有 texture/sample/capsule；真实 Settings dispose与root/copy篡改测试保持 exact pair |
| 不含 direct skin/resource 字段的 menu/shell layout wrapper | wrapper本身不登记；其 `StarFountain`、background、storyboard、skinnable sample/drawable descendant各自登记或持 lease |
| beatmap-local `WorkingBeatmap.Skin` 与 ruleset built-in resource source | 不是 current selected owner；其与 current source的组合生命周期由 live/ordinary provider participant覆盖 |
| legacy editor/external-edit/update-import UI/backend | 产品入口与backend均稳定禁用，不能创建新的 current owner、mount临时store或走 immediate-dispose替换 |

## immutable revision、prepare 与 commit barrier

- `SkinCurrentRevision` 绑定 generation、record ID、content revision、source kind 与 exact owning `Skin`；manager、participant、work与operation lease分别表达 current authority、视觉attach、隐藏异步work和rollback存活，不用 record ID 猜 owner。
- participant registry 的 attach/detach 都推进 generation。prepare开始前 capture snapshot；source prepare、participant readiness、staged resource swap完成后再次复核 generation、selection、exact old revision与source authority；commit时在publication lock内再复核。
- ordinary Realm file读取/hash/capsule构造、managed held capture、external package/registry proof、ini解析、texture/sample/materializer准备都止于background prepare。commit只执行已准备、可回滚的内存引用交换和guarded pair publication；participant commit fault会逆序rollback已应用交换并保持 exact A。
- 所有participant staged ready前，任何consumer都看不到 B。participant失败/取消/source drift/scheduler fault/shutdown只回收provisional B；A的selection、owner、content revision与lease集合不变。
- prepare中attach或commit前detach令旧snapshot失效：manager有界fresh recapture/reprepare，新增consumer不能漏过barrier，已detach consumer不会悬挂ack。commit后late attach直接取得已提交revision及对应lease。outer异步consumer尚处`Loading`时，Ready admission gate先延迟inner load；outer进入`Ready`后才允许GameHost callback发布。之后即使ancestor仍`!IsAlive`，`BmsAsyncNoteDrawable`/`SkinnableContainer`的source invalidation也须由host scheduler重建exact B。base `SourceChanged`在event调用栈内先同步调用旧work invalidation，再以generation标记调度唯一fresh rebuild；独立第二订阅者不得与host scheduler竞速并误杀fresh generation。
- commit前取消保 A；commit callback内或commit后观察到取消，不得回滚已经coherent发布的 B。observer failure被隔离，不能拆pair。
- manager lease释放后，只有 `ConsumersDetached` 与 `WorkDetached` 均满足才可claim retirement。owner dispose与retirement observer exactly once；shutdown先claim participant immutable set，并让每个participant进入不可逆terminal：推进source-change/Ready generation、取消pending dispatch，禁止任何晚到callback重新admit work、adopt revision或发布。随后调用真实owner的shutdown hook去cancel/reap callback与join reload/materializer worker；work lease仍由该owner的真实完成路径释放，最终才同步detach/reap revision。
- BMS异步note与generic skinnable的`SourceChanged` invalidation都在各自work admission gate内先推进generation，再exact claim pending owner/CTS；prepare install与finish publish必须比较进入该段时captured generation，任何跨generation work都不能装入field或发布。shutdown/dispose也在同一gate内terminal、推进generation并claim owner/CTS，再于锁外cancel、join和exactly-once cleanup。field claim从合同上消除了completion与invalidation之间的CTS double-dispose/已dispose窄窗，不再把吞异常当作正常并发语义。

## 三种 same-record-ID source

| source | exact prepare authority | 成功 / 失败 |
| --- | --- | --- |
| ordinary Realm `.osk` | fresh detached Realm metadata+完整file declaration；逐blob读取并核对SHA-256；规范capsule content revision；prepare后与commit前重读exact declaration set | 同一record ID发布新owner/revision；缺blob、hash/声明漂移或解析失败保留A，provisional exactly-once retire。发布后Realm record的file-declaration path、external或DeletePending projection漂移不污染active immutable owner，late renderer仍取active revision；fresh reload/mutation重读到path改变造成的declaration mismatch时拒绝。这里不是registry file drift |
| managed folder | exact scanner-owned record、resolver request、held no-follow package capture session与metadata content revision | held session从capture保持到commit validation；source drift/不可用保A，成功发布B且不靠live store |
| external folder | exact service-owned record、full external registry declaration/physical proof、held package session与content revision | package与registry proof都保持到commit；OMS始终不写source，失败保A，成功只发布fresh in-memory B，不改Realm registration的Name/Creator/Hash observation |

`NoChange` 比较 exact prepared content revision，不触发owner替换。latest request可在较早uncooperative worker仍退出中发布最新revision；旧worker永远不能commit，且其operation/mutation admission直到真实退出才释放。shutdown会join所有superseded worker。成功publication清理诊断时还必须compare该请求generation：同代startup contention的最终成功可清为`None`；若`SourceChanged` observer重入并推进generation产生新的invalid/reentrant拒绝，outer completion不得覆盖该脱敏reason。

## current mutation 原子边界

### current external Unregister

真实 Folder Skin Workspace current external 行先 capture exact record/full registry declaration/current selection-owner-revision-generation，再通过统一barrier发布受保护 `OmsSkin` fallback并等待旧revision `ConsumersDetached`。随后fresh比较 fallback仍current、selection generation、full registry declaration、exact service-owner record与record字段，仅做 Realm compare-remove；全程不解析、打开、写、改名或删除external source。

fallback prepare/publication/detach、fresh compare或Realm任一步失败，统一通过old-revision operation lease和participant barrier恢复 exact旧 selection/owner/revision；record保留，source零变化。source missing/drift不产生source authority，也不妨碍在其它条件exact时解除陈旧注册。fallback期间late/half-loaded participant在rollback前被纳入staged restore或等待其detach，不会看到split pair。

### current managed Delete

current managed delete必须先held capture exact source/content revision并证明它等于current revision，再准备C1既有delete transaction。protected fallback publication与旧 `ConsumersDetached` **先于** journal/首个physical mutation；participant失败、source drift、split/fallback invalid或该边界前取消都不创建journal、不移动/删除目录，并恢复或保留exact A。

fallback+detach成功后才执行C1既有single-v3 journal、held-root tombstone/physical cleanup与exact Realm mutation。若首个physical步骤后的结果不确定，继续由C1 durable recovery收口并保持protected fallback；不得错误承诺所有post-physical failure均可还原A。C7前fallback authority仍是程序化 `OmsSkin`。

### ordinary current `.osk`

current ordinary delete同样先验证exact Realm package content revision、发布fallback并等待detach，再做既有 Realm soft-delete。Realm失败恢复exact旧pair/revision、record与blob；直接current file add/replace/delete、retained stale handle及update-import均由统一mutation admission/backend gate拒绝。noncurrent既有导入/编辑/导出语义不从C2扩大。

## reachable bypass 审计

- `ExternalEditOverlay` 在mount前返回稳定 unavailable feedback；`SkinEditor`菜单项 disabled。
- `SkinManager.BeginExternalEditing`、`SkinImporter.BeginExternalEditing`及base/interface dispatch都在mount/store/current pair变化前抛出同一稳定diagnostic。
- `SkinManager.ImportAsUpdate`、`SkinImporter.ImportAsUpdate`及base/interface dispatch稳定拒绝；direct current `UpdateSkinIniMetadata`/file mutation由Realm package mutation boundary拒绝。
- global legacy Skin Editor hotkey/overlay统一受 `SkinAuthoringAvailability.LegacyEditorAvailable=false` 控制并给用户反馈。它不是 Folder Skin Workspace，也不是 Skin V1 author-preview。
- current owner只能经统一 selection/publication/fallback transaction替换；审计未保留 reachable `new instance → CurrentSkin → immediate dispose previous owner` 产品旁路。

## failure / concurrency 最终验证矩阵

最终focused与production-host测试覆盖：三源same-ID A→B；source/participant/commit失败保A；barrier前B不可见；attach-during-prepare、detach-before-commit、late attach；live mania/BMS pre-start拒绝；StarFountain staged texture；sample跨revision tail；background cross-fade；storyboard blocker；latest-wins与uncooperative superseded worker；reentrant/throwing observer；commit前后cancel；scheduler fault；shutdown claim/reap/join；BMS async note/materializer/callback ownership；最后detach后exactly-once retire；current external/ordinary/managed mutation失败与重试；ordinary Realm record的file-declaration path改变形成declaration mismatch后，late真实BMS renderer仍消费active immutable owner；Ready admission与ancestor `!IsAlive`后publication/source invalidation；同步invalidation、captured generation、exact owner/CTS claim与participant shutdown terminal；以及“同代成功清旧诊断 / 新generation重入拒绝不被outer成功覆盖”的相反语义配对。current managed Delete的测试等待拆为fallback publication、old consumers detach、physical/Realm三个独立门，避免单一timeout掩盖卡点。core focused为 **204/204**，`FullyQualifiedName~PendingAsyncDrawableOwnership` visual/host为 **11/11**；core canonical `~Skin`为 **1137/1143**，六项与既有精确基线一致：四项`TestSceneBeatmapSkinResources`依赖removed-Osu fixture、`TestBackgroundCyclingOnDefaultSkin(True)`及Argon `TestSampleUpdatedBeforePlaybackWhenNotPresent`；mania `~Skin`为 **182/182**。BMS `~Skin`首轮 **785/787** 暴露两项non-alive ancestor source invalidation问题，按专用host scheduler/generation/Dispose合同修正后最终为 **796/796（8m53s）**；BMS full最终为 **1670/1670（10m09s）**，`--blame-hang 5m`明确所有测试完成且未生成hang sequence文件。另有后台/queued真实host与source/Ready/shutdown组合 **14/14**、final drift/half-loaded/Ready sentinel **6/6**、完整真实C2产品路径集 **314/314**。participant/holder、reachable bypass、concurrency/owner及tests/product-contract四项独立终审均为blocker/major/moderate **0/0/0**。

Release 命令 `dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 在 **41.88s** 内成功，**0 error / 20 known warnings**：18次MessagePack `NU1902`输出及BMS tests既有`CS8600`/`CA2007`，未用`NoWarn`隐藏。targeted formatter按core、core-tests、BMS、BMS-tests与mania-tests owning project最终执行均exit 0，只保留`IDE1006`不能自动修复提示；formatter后同一slnf加`--no-restore`复验在 **36.58s** 内成功，**0 error / 11 known warnings**（9次`NU1902`及`CS8600`/`CA2007`）。最终`CheckDocumentation.ps1`通过（137个Markdown、1071个相对链接、80个memory wiki链，仅两份PLAN的预期数字比值提醒），`git diff --check -- doc_md .Codex/memory`通过。当前主任务随后以同一工作树在master创建有意义提交，不建分支/PR，push前仍须重新取得用户确认。

## C1/C2 冻结输入与 C3+ 排除

1. [C1完成交接](SKIN_SYSTEM_C1_COMPLETION_HANDOFF_20260813.md)中的external永久只读、exact-set ManagedCopy/single-v3 journal/recovery、Folder Skin Workspace与ordinary `.osk` bounded ingress/receipt继续有效，不因C2重做或放宽。
2. C2只关闭当前production consumer。C3～C6新增layout/codec/scene/script consumer必须在同一切片加入participant/lease/detach协议；`ini/manifest/scene/script/素材`最终整包reload与G1自动门仍到C6关闭。
3. C2不交付P1-K lane/keymode修复、唯一layout、shared codec/catalog/resolver、scene/event、剩余slot、sandbox、canonical双包或Authoring Kit；不实现watcher，不开放live gameplay reload，不删除程序化`OmsSkin`。

## C3 持久执行 prompt（已签发）

> **当前执行入口：P1-A 已推进为 `2/7 closed，C3 active`。**
>
> 继续 OMS P1-A / Skin V1 七个持久campaign的C3：P1-K lane/keymode前置 + 唯一 gameplay layout。先严格按`AGENTS.md`读取mainline STATUS/PLAN、P1-A四件套、P1-K四件套、subline README、`doc_md/other/SKIN_SYSTEM_RECOVERY_20260710.md`、本C2完成交接，以及`reference_skin_atomic_reload_detach`、`reference_bms_lane_keysound_timeline_bounds`、`reference_bms_keysound_chain`、`reference_gameplay_skin_lane_identity`、`reference_gameplay_skin_topology_revision`、`reference_gameplay_skin_lane_resource_compatibility`、`reference_bms_lane_rearrangement` memory。以P1-A STATUS的当前燃尽为唯一authority。C1作者工作区/archive安全与C2唯一Settings manual Reload、live gameplay/preview prepare前拒绝、三源same-ID publication、participant/lease/detach/retire、current mutation和legacy authoring fail-closed均已冻结，禁止重做、放宽或长出watcher；C6才关闭最终ini/manifest/scene/script/素材整包门。
>
> C3先闭合P1-K Skin前置。修正`BmsBeatmapConverter.buildLaneKeysoundTimelines()`的上界authority，所有logical lane必须使用`GetLaneCount()`而非`GetKeyCount()`；覆盖5K/7K最右键、9K全部lane、14K右deck末键与Scratch2，并逐类证明visible note、LN head/tail armed entry、invisible object及相邻mine/armed timeline没有末端lane静默丢失。parser/converter仍是唯一truth，layout/skin/runtime不得自行重读BMS或二次推导lane数。冻结sparse 5K/7K/9K keymode的单一source/precedence、显式override/纠正入口和稳定脱敏diagnostic；`.pms`/`.bme`、P2/high channel与sparse chart必须可追溯采用的authority。没有证据时fail-closed/显式诊断，禁止以最高出现channel或layout宽度继续猜测。若影响真实键音链，P1-K先证明DTO/timeline完整，P1-J production host再证明玩家/autoplay进入同一shared keysound store并实际发声；不得顺带改sample pool、判定或binding。
>
> 在该前置上交付唯一ruleset-neutral immutable `GameplaySkinLayoutContext`、唯一BMS layout snapshot/solver和mania adapter。现有LaneIdentity/Topology及topology-only revision只是输入，不得冒充完整layout或创建第二组稳定ID。context绑定exact native context/keymode、topology、presentation style、safe bounds/aspect/DPI、package/current revision与layout revision；构造后防御性不可变，所有consumer只读同一exact snapshot，禁止各自重新创建`BmsPlayfieldLayoutProfile`、默认geometry或按drawable尺寸二次求解。
>
> BMS矩阵至少覆盖5K/7K的P1/P2/CenterP1/CenterP2、9K BMS/PMS、14K双deck/S1/S2/centre gap；mania adapter覆盖真实single/dual stage vector并保持stage-local special-key语义。stable LaneId/GroupId跨style、视觉重排、geometry和topology-preserving revision不变；logical/visual、global/group-local index必须显式使用，不能靠枚举位置、`RelativeStart`、enum ordinal或总lane count反推。Mirror/Random只改变mod后对象目标lane，不改变固定playfield topology；对象、keysound和skin lookup最终指向同一LaneId。
>
> 完整production consumer必须迁到同一snapshot：BMS playfield/stage/group/lane、Note/LN、barline、hit/judgement line/target、lane cover、pre-start preview、BGA viewport、gauge/combo及HUD；mania playfield/stage/column/note/hold/hit target/judgement与core ruleset/provider适配不得保留第二套几何。BGA本轮只统一最终viewport/rect，内容、timeline、seek与gimmick播放继续归P1-L；menu/shell/background不是作者layout surface。审计每个既有profile/default profile/fixed rect/local offset创建点，迁入唯一solver、证明为非layout数据或稳定禁用；不得在主要playfield接线后结束。
>
> geometry逐字段验证finite、正值、合法range、安全screen bounds与字段间non-overlap；单字段非法只对该字段使用确定程序化fallback并产生稳定脱敏diagnostic，不能传播NaN/Infinity/负尺寸，也不能拼出部分新/部分旧snapshot。14K两个field、scratch、centre gap、BGA与HUD须在常见/极窄/极宽aspect、DPI scaling与safe-area矩阵可证；fallback仍产出一个完整snapshot。
>
> C3新增layout context/snapshot及全部新consumer须同切接入C2 revision协议。可失败geometry解析/求解/资源准备止于background prepare，update-thread commit只发布prepared immutable引用；participant generation、current selection、exact source/content revision和layout revision在prepare前后及commit时复核，任一失败保留exact旧package+layout pair。prepare中attach强制fresh barrier，commit前detach安全移出，late attach只取已提交package/layout revision及lease；旧owner最后lease detach后exactly-once retire。live gameplay/preview仍在source prepare前拒绝，不得为layout测试开放live reload；真实playfield通过拒绝保A和成功后late attach取B证明。三源same-ID、latest-wins、reentrant、cancel、scheduler fault、shutdown与current external/managed mutation失败原子性继续回归。
>
> 红测必须从真实BMS decode/keymode source/override跨converter、manager/layout owner到真实BMS/mania/core renderer，不能只测DTO、solver mock或静态矩形。至少覆盖末端lane timeline与真实发声、sparse keymode authority、全部style/deck/scratch/centre gap、同一snapshot被playfield/Note/LN/pre-start/BGA/HUD/gauge/combo消费、mania single/dual stage、invalid geometry逐字段fallback、aspect/DPI/safe bounds、mod后LaneId、三源same-ID layout A→B、失败保A、动态attach/detach、live reject、late attach、跨revision holder及最后detach exactly-once retire。测试必须先在旧production路径红，不得用测试专用publisher或绕过caller注入最终snapshot。
>
> 完成后运行P1-K decoder/converter/projection focused与相关真实键音host、core Skin、mania relevant/full、BMS relevant/full及Release，稳定比对core fixture基线，按风险运行targeted formatter。同步P1-K四件套、P1-A四件套与路由、必要mainline、SKINNING/other索引、相关memory及MEMORY索引，运行`CheckDocumentation.ps1`与`git diff --check`。完成P1-K authority、唯一geometry、全consumer/reachable bypass、revision participant/owner、并发与测试独立终审，在当前master创建有意义提交；不建分支/PR，push前重新取得用户确认。
>
> 明确排除C4+：shared codec/public catalog/`Provide-Inherit-Suppress` resolver与mania parity、beatmap-local作者格式终态、scene/animation/event、剩余optional slot、sandbox/script VM、canonical双包和Authoring Kit；不处理P1-L的BGA内容/timeline/seek，不提前删除程序化`OmsSkin`，不开放watcher或live gameplay reload。不要在P1-K修补、layout DTO、单solver、单consumer、红测、foundation或单提交处结束。只有P1-K前置、唯一immutable layout、全部production consumer、C2 protocol extension、宽测试、Release、文档、独立终审与提交全部闭合后，才推进下一campaign并生成完整后续执行prompt。
