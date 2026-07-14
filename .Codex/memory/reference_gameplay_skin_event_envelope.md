---
name: reference_gameplay_skin_event_envelope
description: Skin V1 gameplay event envelope、canonical stream ordering、producer authority 与 mutable callback 地雷
metadata:
  node_type: memory
  type: reference
---

# gameplay skin event envelope 召回

权威状态与硬约束见 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md) / [CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)；本文件只保存实现地雷。

## 当前稳定合同

- `GameplaySkinEventEnvelope` 是 process-local、非 generic、只读的 engine contract，固定 `apiVersion/epoch/sequence/gameplayTime/layoutRevision` 与 `Snapshot/Reset/Edge` delivery category；不是 serialisation、script 或 author manifest ABI。envelope 只有内部 dispatcher 能盖 header，ruleset adapter 不拥有 epoch/sequence authority。
- payload hierarchy 由 shared engine 定义。第三方 package 不能派生；ruleset adapter 只提交 neutral primitives 或调用 shared concrete payload factory。BMS 是 friend assembly 也不获得直接 subclass/publish 的架构权限。
- `gameplayTime` 是 gameplay clock 毫秒域，必须 finite；lead-in/storyboard 允许负值，同时间事件由 sequence 决定顺序，绝不能换成 wall/update clock。
- internal cursor 校验 capability/family filtering 前的完整 canonical stream。新 consumer（包括 reload 后新实例）先以完整 Snapshot 从任意非负 mid-session epoch/sequence high-water attach；之后 epoch 严格 `+1`，同 epoch sequence 严格 `+1`，time 非递减。
- Reset 是下一 epoch、sequence 0 的完整原子 baseline，可把 gameplay time 向前或向后重锚；不是“先清空、稍后 Snapshot”。layout revision 在一个 attachment 内不回退，Snapshot/Reset 可保持或推进，Edge 必须等于当前 revision。
- cursor 只 validate-and-advance；任何拒绝不改变 last accepted envelope，不排序、不补洞、不重放、不自动修复。sequence/epoch 到达 `long.MaxValue` 后 fail-closed，不能 wrap。
- envelope header 可表示正数 future version，让 V1 consumer 明确拒绝；当前 canonical cursor 只支持 V1，attachment 内不得换版本。

## producer 与 mutable state 地雷

- `GameplayClockContainer.OnSeek` 没有 reason/time，且 `Reset()` 也会调用 `Seek()`；无法单靠该 callback 区分普通 seek、retry、initial reset 或其它 discontinuity。生产 lifecycle bridge 必须显式拥有 reason/epoch。
- `JudgementResult` 是可变对象；`Playfield.revertResult()` 在回调后会继续通知 lifetime entry，随后调用 `result.Reset()`。未来 adapter 必须在 New/Revert 回调栈内立即复制 primitive/neutral ID，不能排队保存引用后读取。
- `HitEvent` 虽是 readonly struct，仍含 `HitObject`/`LastHitObject` 引用，不是安全 payload。`Drawable`、`HitObject`、`Bindable`、clock、Realm object 与 ruleset-native mutable configuration 都不能越过 event 边界。
- `SkinReloadableDrawable` / `ISkinSource.SourceChanged` 会被 scheduler 延后或合并，也没有 package validation/layout revision authority，不能直接当原子 reload producer。
- canonical cursor 的 sequence 连续规则适用于过滤前内部流；未来 capability/family filtering 若保留原 sequence，外部 filtered view 可以看到 gap，不能把该 cursor 错接到过滤后流。

## 尚未闭合

第六切只有空 fixture payload 的 envelope/category/order foundation。它不能证明具体 Snapshot/Reset 携带完整 state，也不能证明 attach/reload/seek/retry 的真实 producer 会正确投递。lifecycle/layout/input/object/LN/mine/judgement/score/gauge/timing/BGA concrete payload、continuous scratch/scroll sampling、structured runtime fault isolation、dispatcher/scene/script consumer 与生产接线均未实现。
