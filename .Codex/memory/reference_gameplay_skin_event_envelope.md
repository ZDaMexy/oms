---
name: reference_gameplay_skin_event_envelope
description: canonical event stream 与 filtered view、mutable callback 拷贝、Snapshot/Reset 和时钟域地雷
metadata:
  node_type: memory
  type: reference
---

# Event envelope 地雷

envelope/scene ABI、版本与producer合同见 [P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)，公开声明见 [catalog](../../doc_md/other/GAMEPLAY_SKIN_PUBLIC_CATALOG_V1.md)，接线状态只读 [STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)。

## CLR 载体不等于作者能力

- GameplaySkinEventEnvelope是engine构造的只读CLR载体；dispatcher盖epoch/sequence/revision header，ruleset只提交shared neutral payload。friend assembly不授予subclass/publish架构权限。
- manifest声明的oms-gameplay-skin-event.v1是公开只读合同；CLR构造/继承本身不是package、序列化或script API。正future version可以表达为header输入，但V1 consumer明确拒绝，attachment内不换版。
- gameplayTime是finite gameplay-clock毫秒；lead-in可负值，同time靠sequence排序，不换wall/update clock。

## Cursor / Reset 的诊断顺序

- canonical cursor检查过滤前完整流。初次attach先完整Snapshot，可从mid-session非负epoch/sequence high-water进入；同epoch sequence严格+1/time不递减，换epoch严格+1。
- Reset是下一epoch、sequence0的完整原子baseline，可前后重锚时间，不是先清空稍后再Snapshot。layout revision在attachment内不回退；Edge匹配当前revision。
- 拒绝不改变last accepted，不排序/补洞/自动修复；long.MaxValue不能wrap。bounded queue溢出通过明确Reset/Snapshot重建，不静默丢事件。
- filtered view若保留原sequence可有gap，不能把过滤前连续性cursor直接接到它后面。
- engine可发GameplayResumed；scene state-machine不接受gameplay.resume，因为Snapshot重建Running。这不是漏producer，也不授权另一套状态。

## Callback 内必须拷贝

- GameplayClockContainer.OnSeek没有reason/time，Reset也调用Seek；单callback不能区分seek/retry/initial reset，lifecycle bridge必须显式掌握reason/epoch。
- JudgementResult会在revert callback后继续通知lifetime entry，随后Reset。必须在New/Revert调用栈复制primitive/neutral ID，不能排队保存引用。
- HitEvent虽readonly struct仍含HitObject/LastHitObject引用；Drawable、Bindable、clock、Realm、native configuration都不能跨payload边界。
- SourceChanged会延后/合并且无package/layout authority，不能当原子reload producer。
- Snapshot/运行事件由engine state生产，不是整包prepare预先产好全部事件。scene只读，不获得判定/input/score/clock/BGA内容权。

publication与late attach见 [[reference_skin_atomic_reload_detach]]；capability/family filtering见 [[reference_gameplay_skin_capability_negotiation]]。
