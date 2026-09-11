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
- 2026-09-11真实复杂包在BMS长条早松后、尚未到停顿前丢失组合历史，原因是`onGameplaySkinJudgement`把parent IgnoreMiss的对象progress硬写1，紧随其后的真实Broken body状态仍按当前时钟为0.02；producer因同epoch进度回退要求ConsumerRebuilt，脚本状态随完整重建清空。判定边应调用既有`getGameplaySkinObjectProgress(identityOwner)`，与spawn/body/Snapshot同源；短键早判定也按start前0/start后1，Hit/Missed事实不等于Completed。不得删进度回退检查、跳过parent事件、伪造新object ID或改早松判定。真实按下/松开回归应同时观测两条Missed进度、同epoch、同脚本实例与累积演出，再保留暂停/回跳/重试门。

## Callback 内必须拷贝

- 2026-09-11 ExactRoot 真实 Seek 取证：测试中晚加的直接 DrawableNote/DrawableHoldNote 会出现在活动对象扫描中，但非池入口不触发 HitObjectUsageBegan，不能取得唯一已登记 object ID。真实重建因此按合同抛错，不能靠放宽 ID 检查、跳过 Seek 或延长等待修复。应经 Playfield.Add(HitObject) 后从对应列按同一 HitObject 捕获真实池对象，保留材料、几何、Reset active ID 与真实击打显示断言。repair9 保留原异常栈，repair10 修复后定点通过；全部结论仍读 P1-A。
- Logger.NewEntry 的 Message 与 Exception 是两个字段；只保存通用 Message 会丢失根因和堆栈。Release 宿主可能允许一次错误继续运行，表现为后续等待超时；诊断应在当前测试开始前记录两字段、结束取消订阅，不修改游戏异常容许规则，也不把 pendingReset 已清空当作 Reset 成功。

- GameplayClockContainer.OnSeek没有reason/time，Reset也调用Seek；单callback不能区分seek/retry/initial reset，lifecycle bridge必须显式掌握reason/epoch。
- JudgementResult会在revert callback后继续通知lifetime entry，随后Reset。必须在New/Revert调用栈复制primitive/neutral ID，不能排队保存引用。
- HitEvent虽readonly struct仍含HitObject/LastHitObject引用；Drawable、Bindable、clock、Realm、native configuration都不能跨payload边界。
- SourceChanged会延后/合并且无package/layout authority，不能当原子reload producer。
- Snapshot/运行事件由engine state生产，不是整包prepare预先产好全部事件。scene只读，不获得判定/input/score/clock/BGA内容权。

publication与late attach见 [[reference_skin_atomic_reload_detach]]；capability/family filtering见 [[reference_gameplay_skin_capability_negotiation]]。

## 完整 HUD 的公开信息与真实显示

- `hud.text`接管会替换原准确率与歌曲进度owner；不能仅验证gate ready或旧owner隐藏。`score.accuracy`数值0～1，text是固定两位百分数；`timing.progress`数值0～1，text是整数百分数。两者走普通scene绑定，不需要脚本权限，也不是canonical专属字段。
- 进度只由`GameplaySkinEventRuntimeHost`使用既有`CalculatePlayableBounds`及同一个gameplay时间生成，包含最后长条结束；首物件前0、末物件后1，空谱/零时长0。原ruleset timing投影继续提供beat/BPM/stop/scroll；不要用更新时钟、累计帧数或另一timeline算进度。完整Snapshot/Reset、compact timing payload和晚订阅均须保留Progress。
- `GameplaySkinSceneRuntimeHost`是update-only controller，六个`Layers`需由真实宿主挂入drawable树并调用`MarkLayersMounted`。只把controller加到测试树，仍能读到未加载的文字对象和零尺寸，从而虚验。必要信息用例需确认文字实际IsLoaded、正宽高，并在缩放后验证边界；semantic单行64字形的prepare/runtime预算必须一致。
- 手写prepared测试节点时，slot-less子文字仍需继承父HUD的Layer和OwningSlotKey；短构造默认Underlay，放在HudForeground父节点下会被跨层保护正确拒绝并局部回落，不能当成字体未加载，更不能把文字计数要求改成回落后的数量。真实package compiler会传递这些继承关系。
- 新增进度不能放宽旧“值未变时不重新格式化计分文字”检查。原timing测试只有beat变化、Progress始终0；应继续零无效格式化，另测progress-only变化、完整显示字符串与seek/retry，而不是增加允许次数凑通过。纯timing帧按同一整数百分数投影去重，24.40%→24.49%仍显示24%，24.50%才显示25%；数值绑定仍用原0～1连续值。实际检查结论仍读P1-A，不从这条诊断推断已验。
- 三包关闭可选闪光后曾暴露BMS纯公共按键图片没有按下反馈。`BmsHitTarget`只对无RuntimeNodes的native specialised单Sprite应用release alpha 0.65/pressed 1，挂载时立即投影；有作者scene节点则不覆盖作者绑定，`KeyFlash Suppress`也不恢复。测试要从真实hit-target子树检查specialised visual内的Sprite；`TryGetHostedDrawable`仅覆盖普通semantic/scene owner，native specialised视觉不在这个字典，外围wrapper的alpha则表示应用/准备状态而非按下状态。
