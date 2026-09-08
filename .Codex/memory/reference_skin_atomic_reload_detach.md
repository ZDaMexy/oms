---
name: reference_skin_atomic_reload_detach
description: current skin reload 的 participant/lease、scheduler、取消与 owner 回收竞态诊断
metadata:
  node_type: memory
  type: reference
---

# Current revision reload/detach 地雷

入口、全部 participant/holder/bypass inventory 与三源协议只维护于 [P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md) 的 current revision 章节；完成态/后续门读 [STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md) / [PLAN](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md)。本页不复制 campaign 燃尽。

## 先判断操作是否经过正确入口

- Settings 的 `Reload current skin` 才是 reload；same-value selection、Workspace row、startup scanner 与 watcher 不是替代入口。legacy editor/external-edit/update-import 的禁用边界不能从 UI 绕到 backend。
- live gameplay/preview 在任何 source capture/parse/provisional prepare 前拒绝；attached consumer 没有 staged receipt 也不能先切 active pair 再补。
- exact publication 把 package/layout/material/scene 绑在同一 owner；consumer 只读同一已提交引用。NoChange 比较 fresh prepared content revision，不替换 owner。
- ordinary .osk 的 authoritative 输入是 fresh Realm file declaration + blob bytes；active immutable owner 不随 Realm projection 漂移。managed/external 的 record token 同样不代替 held physical proof。具体 capture 去 [[reference_skin_package_revision_capsule]]、[[reference_skin_managed_folder_selection]]。

## 排查“旧资源提前释放 / reload 卡住”

先分清 manager、participant、work、operation 四类 lease，再分别观察 ConsumersDetached 与 WorkDetached。旧 manager lease 释放并不表示 texture、sample、callback 已退役。

| 容易漏掉的 holder | 诊断落点 |
| --- | --- |
| menu/background cross-fade | SkinBackground / BackgroundScreenDefault 要等真实 fade 结束 |
| sample 旧 tail、pending swap cleanup | PoolableSkinnableSample 的 channel 持 work lease；SkinnableSound 只聚合子项，不重复登记 |
| BmsAsyncNoteDrawable/materializer | prepared visual、outer callback、generation 与 lease transfer 必须一起追踪 |
| ordinary provider/source array、storyboard sprite | 没有 staged swap 就是 blocker，不能靠 SourceChanged 假装整体提交 |
| Loader/Intro/PlayerLoader 未完成 graph | candidate 创建前已有 temporary blocker；取消/push 失败先 reclaim，再 detach |
| rollback transaction | fallback 已提交后仍需 operation lease 保住旧 owner，直到 exact rollback 或完成 |

完整 inventory 在合同；新增 caller 时只核对实际持有的对象，不给纯 UI record-ID/immutable-label projection 无谓加资源 ownership。

## 已踩过的 scheduler / generation 竞态

- 非 alive 的 `BmsAsyncNoteDrawable/SkinnableContainer` 要用 GameHost scheduler。outer Loading 时先等 Ready 再 admit inner；后续 SourceChanged rebuild 也走同一 host scheduler。
- base source event 先同步 invalidate 旧 work，再按 generation 调度 rebuild。另起一个订阅者会与 host scheduler 竞速并误杀新 generation。
- framework callback 与 ownership sentinel 必须处于同一 scheduler FIFO；Editor graph 用 ScreenContainer.Scheduler。只发 cancel 而不 join worker/callback 不能证明旧 work 已退出。
- work admission gate 内推进 generation 并 exact claim pending owner/CTS；prepare install、finish publish、dispose/shutdown 都核对 captured generation，避免 double-dispose 或旧 worker 写回字段。
- shutdown 先令 participant terminal、推进 generation 并关闭 Ready admission，再调用真实 owner hook cancel/reap/join；晚到 callback 只能回收。manager 不得替 consumer 提前释放 work lease。
- latest-wins 允许新请求先发布，但旧 uncooperative worker 永不 commit，旧 operation admission 保留到它真实退出。
- 成功后的诊断清理也要比较自己的 request generation：SourceChanged observer 可重入并产生新拒绝；outer completion 不能把较新 reason 清成 None。

## Prepare / commit 窄窗

- source I/O、decode、solver、material/scene 构造止于 background prepare；update thread 只交换已准备且可逆的引用。
- carrier 一取得 work lease/retirement 就进入可 Dispose scope；solver 最后检查后到 TryCommit 前仍可能取消，shared owner 与 ruleset caller 须释放 carrier，不能提交已取消结果。
- prepare 中 attach、commit 前 detach 会使 participant snapshot 失效；fresh barrier 重试与 late attach 消费已提交 publication 是不同阶段。不能只比较 record ID。
- commit 开始前取消保留 A；commit fault 逆序 rollback 保 exact A；commit 开始后取消不能制造半新半旧。
- consumer/work/operation 最后 detach 后，才在 update thread exactly-once retire。测试中单个总 timeout 会掩盖卡在 publication、consumer detach 还是 physical mutation，三个门应分开观测。

## Current mutation 不同失败边界

- external Unregister：fallback + old detach 后 fresh compare，仅 pure-Realm remove，source 零 I/O；失败借旧 operation lease 恢复 exact A。
- managed Delete：先 held capture 证明目标与 current exact，再 fallback + detach；此门前不写 journal/不触物理树。首个 physical 步骤后的不确定失败保持 fallback，交 durable recovery，不承诺恢复 A。
- ordinary .osk Delete：fallback + detach 后 Realm soft-delete；Realm 失败恢复旧 pair/revision、record/blob。
- shutdown 必须在 Realm 释放前 join 这些 worker；fallback callback 与 shutdown 恰一方 claim/reap，先完成 worker TCS 再发可能重入的 SourceChanged。

物理 mutation/recovery 见 [[reference_skin_managed_folder_mutation_foundation]]；layout/material/event 的局部地雷见 [[reference_gameplay_skin_layout_snapshot]]、[[reference_gameplay_skin_codec_material]]、[[reference_gameplay_skin_event_envelope]]。
