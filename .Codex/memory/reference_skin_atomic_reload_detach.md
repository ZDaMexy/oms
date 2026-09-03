---
name: reference_skin_atomic_reload_detach
description: C2三源current revision与C3/C4/C5 package+layout+material+scene不可分publication、lease/detach/retire、manual Reload和live fail-closed边界
metadata:
  node_type: memory
  type: reference
---

# Skin current revision atomic reload/detach 地雷

## 当前结论（2026-09-03，C2+C3+C4+C5已签发）

- C2已由真实Settings caller接通ordinary Realm`.osk`、managed与external三源same-record-ID/content-revision纵切；C3把唯一immutable gameplay layout加入，C4又把shared document与resolved material作为同一package+layout+material引用加入publication、participant与owner协议，C5再把 versioned scene/animation 与只读 event runtime 接入同一 exact package+layout+material+scene publication，并覆盖真实 BMS/mania production hosts。四项均已通过宽测试、Release与独立终审；权威燃尽是`5/7 closed，C6 active`。当前状态只看[P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)，完整participant/holder/bypass inventory与稳定合同见[P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)，C5边界见[C5交接](../../doc_md/other/SKIN_SYSTEM_C5_SCENE_EVENT_COMPLETION_HANDOFF_20260903.md)。
- 唯一产品入口是Settings → Skin → `Reload current skin`。Folder Skin Workspace无行级Reload，same-value selection仍no-op，startup scanner仍只做一次reconcile，不实现watcher；legacy Skin Editor、external-edit与update-import的UI/backend均稳定fail-closed。
- live gameplay/gameplay preview由`RulesetSkinProvidingContainer`和`PlayerLoader`登记`LiveGameplayHost`，manager在任何source capture/parse/provisional prepare前确定拒绝并给出退出后重试反馈。其它attached且无staged receipt的visual consumer也fail-closed；禁止先改变active pair再延后。
- C3已把BMS/mania/core gameplay layout、BGA最终viewport与HUD consumer加入协议；C4又加入public codec/catalog/resolver及BMS Note/LN、mania Note/Hold/KeyVisual material consumer；C5已将 scene/event/剩余适用素材 consumer 同切加入。C6仍负责脚本隔离/权限协商与最终 `ini/manifest/scene/script/素材` 整包 reload 门，G1 自动门也仍待 C6 关闭。

## production object graph inventory

[P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)的`3m1`～`3m3`是稳定最低集合；这里保留逐宿主审计召回，避免把一次性执行prompt当authority。

### 必须coherent处理的视觉consumer

- `RulesetSkinProvidingContainer → BeatmapSkinProvidingContainer → core/mania/BMS provider tree`整体登记`LiveGameplayHost`，持有transformed current、beatmap-local与fallback source；gameplay/preview只允许prepare前拒绝，不开放在线逐件重建。
- BMS playfield/stage/group/lane、Note/LN、barline、hit/judgement line/target、lane cover、pre-start、BGA最终viewport、gauge/combo/HUD、scene/event与mania migrated consumer都从同一个exact publication取geometry/material/scene；`BmsAsyncNoteDrawable`/materializer另持exact work lease，late attach只取得已提交package+layout+material+scene quadruple。
- mania playfield/stage/column/flow、Note/Hold、barline/hidden、hit target、judgement/adjustment/touch input和gameplay HUD，以及core ruleset/provider root都消费同一publication的neutral snapshot或typed adapter；逐件`SourceChanged`、transformer snapshot hand-off或consumer-local `Apply`不能冒充coherent commit。
- generic `SkinReloadableDrawable` family（`SkinnableDrawable`、`SkinnableContainer`、`SkinnableSprite/Text`及song-select/results/HUD/BMS/mania实例）在load前登记temporary blocker，load后登记exact participant lease；只有可提供staged receipt者能过barrier，否则fail-closed。
- ordinary非live `SkinProvidingContainer`/`BeatmapSkinProvidingContainer`会跨frame持有source array与fallback lookup；未提供staged source-array swap时attached即拒绝，自然detach后重试，late instance绑定已提交revision。
- `StarFountain`在prepare阶段构造新texture，commit只交换`spewer.Texture`；`PoolableSkinnableSample`同样只交换prepared `DrawableSample`，playing旧tail由work lease继续保活。
- skin-sprite `DrawableStoryboardSprite`/`DrawableStoryboardAnimation`直接查询global source且无staged swap，initial/loaded participant都fail-closed，真实storyboard detach后才允许reload。
- `Loader`、`IntroScreen`、`IntroWelcome`的未完成screen/sequence graph从candidate创建前持temporary blocker到transfer/reclaim；`PlayerLoader`未完成player graph登记temporary `LiveGameplayHost`，取消、push失败与shutdown都先reclaim再detach。

### 只持revision lease并负责最后detach的holder

- `SkinBackground`与`BackgroundScreenDefault`持exact owner、menu texture及pending graph；真实cross-fade结束才释放旧revision holder。
- `PoolableSkinnableSample.ActiveRevisionChannel`与pending swap cleanup持historical sample、playing channel和exact work lease；旧sample从hierarchy移除/销毁后才释放lease。
- `BmsAsyncNoteDrawable`/`BmsManagedPackageNoteProvider` materializer持prepared visual、outer callback、owner generation与revision lease transfer；cancel/supersede必须join真实退出，callback fault、dispose与shutdown须exactly-once claim/reap。
- manager current pair、reload/mutation/rollback transaction持manager/provisional/operation lease；pre-commit失败只retire provisional，fallback已commit后的失败持旧revision到exact rollback或transaction completion。
- participant registry/retire queue持attached participant lease并分离consumer/work detach fence；`PendingAsyncDrawableOwnership<T>`还覆盖background/storyboard/editor/results/statistics/screen/player candidate、worker与scheduled callback，framework callback和ownership sentinel必须位于同一scheduler FIFO。

### 经代码与真实host证明自身不持旧current owner的对象

- `SkinnableSound`/`PausableSkinnableSound`只聚合descendant且`ParticipatesInCurrentRevision=false`；实际sample/tail由各`PoolableSkinnableSample`登记。
- guarded selection/instance bindable、Settings/Workspace row与notification只投影committed value或持record ID/immutable label，不拥有texture/sample/capsule。
- 不含direct skin/resource字段的menu/shell wrapper由其`StarFountain`、background、storyboard及skinnable descendant分别登记；排除wrapper不等于排除child。
- beatmap-local `WorkingBeatmap.Skin`只作为既有只读legacy direct visual compatibility authority，与ruleset built-in resource source的组合生命周期由live/ordinary provider participant覆盖；它不是public authoring authority，其public section不得进入C4 resolver。
- legacy editor、external-edit、update-import UI/backend均稳定禁用，不能创建新current owner、mount临时store或走immediate-dispose替换。

## revision、participant 与 owner

- `GameplaySkinLayoutRevisionOwner`由live root的exact`GameplaySkinPackageRevision`创建，只能发布一个`GameplaySkinLayoutPublication`；one-shot guard必须由shared owner在自己的锁内执行，不能只靠ruleset helper。publication把ruleset-neutral snapshot、exact typed adapter与`GameplaySkinResolvedMaterialSet`绑定为同一引用；exact source只接受current catalog/codec/resolver合同，`CompatibilityEmpty`仅允许detached compatibility owner。package/current、layout与material不可分割，不允许先发package/layout再补material或让consumer各自merge lookup。
- 可失败的native stage vector/topology、environment读取、skin geometry解析/solve、shared codec/catalog validate、resource/scene decode与resolved material/prepared graph构造都在fresh work lease内的background prepare完成；BMS/mania不能在进入owner callback前预发布topology/material/scene。production只使用shared cancellation-aware `PreparePublication`：carrier取得fresh work lease/publication retirement后立即进入可Dispose scope，BMS/mania caller还要在`using`内、`TryCommit`前复核token，关闭solver最后检查后的取消窄窗。update thread只交换prepared immutable publication引用。prepare前后及commit锁内同时复核root、participant generation、selection/source/content/package/layout/material/scene合同；attach触发fresh barrier重试，commit前detach拒绝carrier，成功后late attach只看已提交quadruple。
- production consumer只能从exact owner的`CurrentPublication`取得neutral/typed同一引用。外部注入carrier、另一owner的prepared carrier、同root第二provider、compatibility→exact mutation、无publication或二次prepare都fail-closed；BMS/mania managed preparer在任何config/skin/solve前拒绝compatibility token。isolated compatibility只是detached测试dependency scope的显式opt-in，不承担production lifetime authority，也不能进入真实provider树。
- `SkinCurrentRevision`绑定generation、record ID、content revision、source kind和exact owning `Skin`；manager、participant、work与operation lease分别表示current authority、visible attach、隐藏异步work与rollback存活，不能用record ID或`SourceChanged`猜owner。
- participant inventory分三类：必须coherent处理的core/mania/BMS provider/renderer、generic skinnable、ordinary provider、fountain/sample/storyboard与pending screen/player graph；跨fade/sample/materializer/callback的lease-only holder；以及只聚合descendant的sound wrapper、guarded UI projection、独立beatmap/ruleset authority和已禁用authoring路径。完整稳定分类固化在[P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)的C2合同，本memory只保留诊断召回。
- prepare开始前capture participant/current/source snapshot；source与全部staged material准备后、commit前以及publication lock内再次复核participant generation、target generation、current selection/owner/revision和exact source revision。Realm/blob/held filesystem I/O、capture、parser、texture/sample/materializer等所有可失败工作必须止于background prepare。
- `BmsAsyncNoteDrawable`/`SkinnableContainer`的non-alive host需要GameHost scheduler：outer `Loading`时先等Ready再admit inner，之后`SourceChanged` rebuild也走host scheduler。base source event先同步调用旧work invalidation，再以generation标记调度fresh rebuild；独立第二订阅者会与host scheduler竞速并误杀新generation。Dispose或publication shutdown先进入participant terminal、推进generation并取消pending/Ready admission；否则可能非法mutation、吞掉exact-B rebuild或让晚到callback复活已关闭participant。
- update-thread commit只交换已准备且可逆的内存引用；全部participant ready前B不可见，commit fault须逆序rollback并保持exact A。prepare中attach或commit前detach使snapshot失效并有界fresh retry；commit后late attach只取得已提交revision与lease。commit前取消保A，commit开始后取消不得回滚成split。
- old manager lease释放后，还必须同时满足`ConsumersDetached`与`WorkDetached`；最后participant/work/operation lease detach后才能在update thread exactly-once retire owner。异步graph的framework callback与ownership sentinel须使用同一scheduler保持FIFO；Editor mode graph固定为`ScreenContainer.Scheduler`。shutdown先claim participant集合并令每个participant进入terminal，再调用真实owner hook cancel/reap callback、join真实worker/materializer/work fence，最后同步detach/revision回收；manager不能只发cancel或代替consumer释放work lease。
- BMS/Skinnable invalidation须在各自work admission gate内推进generation并exact claim pending owner/CTS；prepare install和finish publish都比较captured generation，shutdown/dispose同样在gate内推进generation后claim。因此CTS completion不能与invalidation形成double-dispose/已dispose正常窄窗；跨代worker只能回收，不能装入field或发布。
- latest-wins允许新request在旧uncooperative worker退出前发布，但旧worker永不commit且operation admission保持到真实退出。成功publication清理诊断必须compare自己的generation：同代startup contention成功可清`None`；若`SourceChanged` observer重入并推进generation产生新的invalid/reentrant拒绝，outer completion不得覆盖其脱敏reason。

## 三源 exact authority

- ordinary Realm `.osk`：fresh detached metadata与完整file declaration，逐blob读取并核对SHA-256，再构造规范capsule/content revision；declaration/blob漂移保A。发布后Realm record的file-declaration path、external或DeletePending projection漂移不得改变active selection/owner/revision，late renderer继续消费active immutable owner；fresh reload/mutation重读到path改变造成的declaration mismatch时拒绝。不要误称registry file drift。
- managed：exact scanner-owner record、resolver request、held no-follow package session与metadata content revision保持到commit validation。
- external：exact service-owner record、full registry declaration/physical proof、held package session与content revision保持到commit；OMS始终不写source。
- `NoChange`只比较exact prepared content revision，不替换owner。direct current file mutation、retained stale handle、update-import或external-edit不能绕过统一admission。

## current mutation

- current external Unregister先发布protected fallback并等待old `ConsumersDetached`，再fresh compare fallback/current generation/full registry/exact service-owner record并pure-Realm remove。prepare/publication/detach/fresh compare/Realm任一步失败借old-revision operation lease恢复exact A并保留record；source missing/drift不授予source I/O，任何结果source零变化。
- current managed Delete先held capture并证明exact source/content revision等于current，再发布fallback并等待detach；此边界成功前不得创建journal或触碰physical tree，失败保留或恢复A。之后才进入C1 single-v3 journal/physical mutation；首个physical步骤后的uncertain failure只保证durable recovery与protected fallback，不承诺恢复A。C7前fallback仍为程序化`OmsSkin`。
- current ordinary `.osk` Delete同样先fallback+detach，再做Realm soft-delete；Realm失败恢复exact旧pair/revision、record与blob。
- 禁止manager-only API、强制same-ID selection、per-host reloadable、逐component `SourceChanged`拼接、即时dispose旧owner或无caller/consumer的barrier foundation。

## 关联入口

- 唯一layout/scene publication：[[reference_gameplay_skin_layout_snapshot]]；shared codec/material：[[reference_gameplay_skin_codec_material]]；完成边界见[C5交接](../../doc_md/other/SKIN_SYSTEM_C5_SCENE_EVENT_COMPLETION_HANDOFF_20260903.md)。
- exact capsule/owner：[[reference_skin_package_revision_capsule]]。
- managed/external authority：[[reference_skin_managed_folder_selection]]、[[reference_skin_managed_folder_scanner]]、[[reference_skin_external_workspace_managed_copy]]。
- mutation recovery：[[reference_skin_managed_folder_mutation_foundation]]、[[reference_skin_osk_archive_import_safety]]。
- authoring/product边界：[[project_oms_bms_skin_authoring]]、[[project_oms_skin_product_progress]]。

## C5 publication 扩展

C5 将 prepared scene、animation/state program、slot runtime host 与 read-only event Snapshot/Reset 纳入原有 package+layout+material+scene exact publication。scene/event consumer 与 BMS/mania host 共用 participant generation、work/publication lease、fresh attach barrier、detach fence 和 exactly-once retire；任何 scene parse/resource/template/event 失败保留完整旧 quadruple。C6 才把 sandbox 与最终整包 reload纳入同一门。
