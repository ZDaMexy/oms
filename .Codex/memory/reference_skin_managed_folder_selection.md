---
name: reference_skin_managed_folder_selection
description: guarded managed selection、typed startup retry、双 epoch 与 fresh Realm commit 地雷
metadata:
  node_type: memory
  type: reference
---

# Managed folder selection 地雷

现行 factory/selection 合同与允许类型见 [P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)，进度读 [STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)。前置是 resolver request→held no-follow capture→immutable capsule→closed factory；record token 不授予文件能力。

## 为什么不能直接写 Bindable

- 调用方 SkinInfo 即使同 ID 也不是 authority；final commit 在共享 coordinator 内重新取本 Realm 的 Live record，复核 generation、record/path/owner/freeze 与资格，然后发布。
- Dropdown、generic Bindable 和 lease 只能单向镜像 committed value，不能获得双向写入口。managed 请求在 update thread admission；普通 Realm .osk 保持其既有线程路径。
- 同值/disabled 检查在 prepare 前。失败、取消、stale/reentrant、scheduler fault 只回收 provisional，保旧 pair；active immutable capsule 不随磁盘变化。
- 旧通用 mutation 需在真实事务内重读资格，旧 importer 在 Files.Clear 前也要复核；专用 manager record-ID API 的存在不解禁任意 path 或 base/interface 旁路。

## Startup / generic mutation 双 epoch

复现前提：configured managed capture 在 OsuGame.load 开始，startup scanner 到 LoadComplete 才在 typed StartupSequence 中执行 recovery→reconcile；两者可交错。

- 只等待 exact startup/staged-import holder completion，后台等完经 update scheduler 做 fresh preparation，重新查 generation、current、Realm、path/owner/freeze、allowlist、capture/factory。update thread 不等待。
- scanner 可能在 capture/factory 前已完成；仅看“当前有无 holder”会丢失原因，必须保留 preparation 观察到的 startup epoch。
- 同时原样保留 generic mutation epoch，贯穿 waiter→deferred callback→chained contention。generic mutation 可在 mismatch 前、factory 后、completion 登记后 callback 前或下一 typed holder 前完整穿越；排队及 retry short lease 内均须核对，否则把 rename/delete 的新状态借成 startup 刷新。
- staged import 有自己的 typed completion 且不增 generic epoch；manual managed 请求遇 startup 仍即时拒绝，普通 Realm .osk 不走该 retry 链。
- 第一次 admission 失败后取得 exact holder completion object，即使 import 立即完成也能等待该对象；不要再用 IsRunning 或全局 completion epoch 猜。

## 别混用操作副作用

- Rename 成功推进 selection generation、取消当时 pending，但不替换/dispose active capsule；未来从新 Realm path capture。
- Staged import/ManagedCopy 不自动选择、不复用 Rename 的全局 pending 取消；无关请求仍可按 fresh authoritative 条件提交。Realm notification 刷新列表不等于 selection commit。
- current managed Delete 的 held capture→fallback/detach→journal/physical 分界去 [[reference_skin_managed_folder_mutation_foundation]]；不能凭确认框旧快照认定 noncurrent。
- same-ID reload、owner 退休和 shutdown/callback 的 exactly-once 回收去 [[reference_skin_atomic_reload_detach]]。成功诊断也要守 request generation，不能覆盖 observer 重入后新 reason。

scanner 的 owner/Observed/Valid 见 [[reference_skin_managed_folder_scanner]]；capsule/factory ownership 见 [[reference_skin_package_revision_capsule]]。
