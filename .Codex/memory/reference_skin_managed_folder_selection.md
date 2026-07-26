---
name: reference_skin_managed_folder_selection
description: Managed skin folder exact-capsule factory、guarded selection、authoritative mutation freeze 与尚未闭合的 reload 边界
metadata:
  node_type: memory
  type: reference
---

# Managed skin folder factory / selection 地雷

## 窄生产链合同

- 只对Realm中已注册、authoritative `IsManaged`且resolver认定合法的`chartskin/<direct-child>`记录工作：resolver-issued request → Windows handle-relative no-follow capture → immutable capsule → exact marker/owning store → closed allowlist factory → guarded atomic selection。
- schema 57 startup scanner现会从同一held `chartskin` authority产生完整stable inventory，以exact owner token新增、更新、revive或soft-delete自身的唯一合法记录；`ObservedPaths`与`ValidDiscoveries`分离，坏包只保护同path，null/foreign/冲突记录永不claim。它只做一轮启动reconcile，不是watcher、mutation或reload；完整协调地雷见[[reference_skin_managed_folder_scanner]]。
- folder allowlist当前必须ordinal精确匹配`osu.Game.Rulesets.Bms.Skinning.BmsLegacySkin, osu.Game.Rulesets.Bms`，并要求根`skin.ini`和公开exact-capsule四参数构造入口；不得走live `RealmBackedResourceStore`、宽松type解析或历史`TrianglesSkin` fallback。普通`.osk`、`OmsSkin`与mania路径不能被全局冻结。
- capsule/store ownership只转移一次；失败、取消、stale、竞态、reentrant或scheduler fault都保留旧`CurrentSkinInfo`/`CurrentSkin` pair并清理provisional owner。active Note/Head/Body/Tail只读同一immutable revision，capture后的磁盘变化不会混入当前实例。
- managed folder selection request在update thread进入；普通Realm `.osk`选择保留既有线程兼容。所有请求的generation bump与Realm commit、managed completion的最终generation check与commit必须串在同一commit lock内，防止后台Realm/invalid/external请求被旧folder completion覆盖。capture完成后与factory完成后双重复核authoritative记录，prepared target还要求同一对象identity。generic `Bindable`、Dropdown或lease不得获得committed值的双向写入口，UI只通过request surface提交并单向镜像committed状态。

## mutation 与生命周期地雷

- 调用方持有的`SkinInfo`即使ID相同也不是authority。delete/undelete、update import、external edit和base/interface mutation必须在真正Realm事务内按ID重新取得authoritative记录后判定；importer要在`Files.Clear()`前复核。旧folder mutation目前只是冻结，不是专用managed rename/delete/import已实现。
- 既有普通delete命中current时只向scheduler排队切`DefaultOmsSkin`，并不等待selection pair确认提交；受管目录current delete不得复用该时序。未来必须先确认当时受保护fallback真实提交，失败则拒绝删除；在确认式路径落地前current delete保持冻结。
- 同值/disabled检查必须先于准备；内部commit不能留下可被reentrant请求复用的“正在提交”旁路。异步completion也不能覆盖更晚的reentrant rejection reason，task fault必须显式观察且诊断不能泄露路径。
- selection pair一次提交不等于全playfield atomic reload。旧capsule/owner只能在所有consumer detach后退役；在publication barrier闭合前，不要为即时释放旧owner破坏仍挂载consumer。

## 不可误推的完成度

- 即使本页的factory/selection窄合同存在，也不证明专用no-follow mutation journal/recovery、external registration/capture、整包reload或全consumer detach barrier已经完成，更不能单独证明G1、`SV1-2`、Skin V1或产品交付。实时状态、测试数字与下一门只看P1-A STATUS/PLAN。
- 前置安全边界见[[reference_skin_filesystem_authority_preflight]]、[[reference_skin_windows_handle_capture]]与[[reference_skin_package_revision_capsule]]。
