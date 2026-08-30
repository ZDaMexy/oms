---
name: reference_skin_managed_folder_selection
description: Managed skin folder exact-capsule factory、guarded selection、typed startup retry、C2 explicit reload与current delete pre-physical边界
metadata:
  node_type: memory
  type: reference
---

# Managed skin folder factory / selection 地雷

## 窄生产链合同

- 只对Realm中已注册、authoritative `IsManaged`且resolver认定合法的`chartskin/<direct-child>`记录工作：resolver-issued request → Windows handle-relative no-follow capture → immutable capsule → exact marker/owning store → closed allowlist factory → guarded atomic selection。
- schema 57 startup scanner现会从同一held `chartskin` authority产生完整stable inventory，以exact owner token新增、更新、revive或soft-delete自身或staged-import publisher经同等级门合法交接的唯一记录；`ObservedPaths`与`ValidDiscoveries`分离，坏包只保护同path，null/foreign/冲突记录永不claim。它只做一轮启动reconcile，不是watcher或reload，也不消费publication plan；mutation共享线性化与恢复见[[reference_skin_managed_folder_mutation_foundation]]。
- folder allowlist当前必须ordinal精确匹配`osu.Game.Rulesets.Bms.Skinning.BmsLegacySkin, osu.Game.Rulesets.Bms`，并要求根`skin.ini`和公开exact-capsule四参数构造入口；不得走live `RealmBackedResourceStore`、宽松type解析或历史`TrianglesSkin` fallback。普通`.osk`、`OmsSkin`与mania路径不能被全局冻结。
- capsule/store ownership只转移一次；失败、取消、stale、竞态、reentrant或scheduler fault都保留旧`CurrentSkinInfo`/`CurrentSkin` pair并清理provisional owner。active Note/Head/Body/Tail只读同一immutable revision，capture后的磁盘变化不会混入当前实例。
- managed folder selection request在update thread进入；普通Realm `.osk`选择保留既有线程兼容。所有请求的generation bump与Realm commit、managed completion的最终generation check、authoritative Realm重读和commit必须串在共享managed-folder coordinator边界内，防止scanner/mutation或后台Realm请求插入最终复核与发布之间。final commit使用本Realm重新取得的authoritative `Live<SkinInfo>`，不发布调用方的陈旧live对象。generic `Bindable`、Dropdown或lease不得获得committed值的双向写入口，UI只通过request surface提交并单向镜像committed状态。
- 启动顺序地雷已闭合但必须保留回归：configured managed skin在`OsuGame.load()`开始异步capture，startup scanner到`LoadComplete()`才启动并从recovery到reconcile持有typed startup sequence。selection只等待exact startup/staged-import holder completion；后台waiter完成后经update scheduler做fresh preparation，重新验证shutdown/disabled、generation、旧current pair、Realm ID/record、path/owner/freeze/allowlist并重新capture/factory，update thread不等待。scanner已在capture/factory前完成而导致旧request mismatch时，由preparation观察到的startup epoch识别；不能只看当前holder。
- 双epoch借权地雷：preparation observation还必须记录generic mutation reservation epoch，并原样贯穿waiter→deferred callback→chained contention。generic mutation可能在mismatch前、factory后direct startup boundary前、startup completion已注册但callback未运行前，或下一typed holder前完整穿越；每次继续排队及持有retry short lease后都要复核epoch，否则fresh retry会把rename/delete/普通mutation的新状态误当成startup刷新而接受。staged import不增generic epoch并保持自己的typed completion；manual managed请求在startup期间仍即时fail-closed，普通Realm `.osk`不走该链。

## mutation 与生命周期地雷

- 调用方持有的`SkinInfo`即使ID相同也不是authority。delete/undelete、update import、external edit和base/interface mutation必须在真正Realm事务内按ID重新取得authoritative记录后判定；旧importer要在`Files.Clear()`前复核。Folder Skin Workspace已为directory-only rename、manager-owned external→provisional ManagedCopy/fixed-source staged import及managed delete提供record-ID真实caller；旧通用rename/import/delete及其它folder mutation继续冻结，不得把专用delete类推为任意path cleanup、external源删除或通用Realm hard-delete。
- rename成功只推进全局selection generation并取消当时的pending preparation，不替换或dispose当前active immutable capsule；旧generation不得发布，旧request还会被authoritative path复核与coordinator final boundary阻止发布，未来重新选择只能从Realm新managed path capture。shutdown必须cancel+join rename worker后才释放Realm。
- staged import成功不自动选择新record、不替换或dispose active immutable capsule，也不复用rename的全局pending取消。无关pending selection在one-shot publication后按authoritative ID/path/owner复核仍成立时继续；selection首次final admission失败后从coordinator取得exact staged-import holder的completion object，即使import在失败后立刻完成，该对象也会可靠完成并触发fresh retry，不再通过外部`IsRunning`或全局completion epoch猜测。generic mutation争用仍拒绝。任何planned ID/path冲突必须在首个物理步骤前拒绝。Realm notification刷新候选列表不等于selection commit。
- current managed Delete先取得并持有mutation reservation与held exact source/content authority，证明其revision等于`CurrentRevision`；随后经C2 barrier发布与`OmsSkin.CreateInfo()`逐字段一致的protected fallback并等待旧revision detach。此边界前不得创建journal或触碰physical tree，失败须保留/恢复A并释放reservation/session；成功后才写Prepared/`ProtectedPairCommitted` receipt并允许physical step。三元authority coherent且都非目标时才允许`NotRequired`。首个physical步骤后的uncertain failure只由C1 durable recovery收口并保持fallback，不承诺恢复A；canonical接管后policy才改为`oms-simple.osk`。
- 同值/disabled检查必须先于准备；内部commit不能留下可被reentrant请求复用的“正在提交”旁路。异步completion也不能覆盖更晚的reentrant rejection reason，task fault必须显式观察且诊断不能泄露路径。
- C2已以`SkinCurrentRevision`、participant/work lease与update-thread barrier签发managed same-ID reload；旧capsule/owner只在所有lease detach后exactly-once retire。same-value selection仍no-op，唯一入口为Settings current manual Reload；完整语义见[[reference_skin_atomic_reload_detach]]。
- startup scanner、rename、staged-import、managed delete、selection/reload capture-scheduling、contention waiter、materializer/work fence与retire queue必须在Realm释放前统一cancel + synchronous join；queued callback由正常路径或shutdown恰好一方claim/reap，晚到callback只能no-op。成功诊断清理还须由request generation守卫，不得覆盖observer重入后较新generation的拒绝reason。

## 不可误推的完成度

- C1已经闭合factory/selection、Workspace rename/ManagedCopy/delete及external registration/capture；C2同ID revision/consumer detach与C3 package+layout pair也已通过宽测、Release、文档门与独立终审。燃尽为`3/7 closed，C4 active`；实时状态只看P1-A STATUS/PLAN。
- 前置安全边界见[[reference_skin_filesystem_authority_preflight]]、[[reference_skin_windows_handle_capture]]与[[reference_skin_package_revision_capsule]]；整包生命周期边界见[[reference_skin_atomic_reload_detach]]。
