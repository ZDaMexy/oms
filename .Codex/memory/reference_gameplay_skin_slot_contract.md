---
name: reference_gameplay_skin_slot_contract
description: Skin V1 public catalog、显式三态resolver、provider precedence、诊断与候选生命周期地雷
metadata:
  node_type: memory
  type: reference
---

# gameplay skin slot 三态合同

权威状态与硬约束见 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md) / [CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)；本文件只做实现地雷召回。

## 稳定合同

- public runtime使用平行`SkinSlotResult<T>`/resolved material entry，不改变nullable `ISkin`兼容ABI。`default(SkinSlotResult<T>) == Inherit`，但production consumer只能读取一次准备完成的显式material winner，不能靠null/缺字典项重跑决策。
- 三态结果使用普通 `readonly struct`；不要改成会自动枚举属性的 `record struct`，否则生成的 `ToString()` 可能读取非 `Provide` 状态下会抛异常的 `Value`。
- `Provide`只表示已完成provider自身构造/基础验证的值；`Inherit`继续下一authority；`Suppress`只在catalog Optional且runtime capability允许时终止。Required/Recommended `Suppress`必须稳定诊断后继续。
- resolver 严格使用调用方提供的 provider 顺序。provider/GetSlot/构造异常、validator=false 或 validator 异常都诊断后逐组件继续；取消异常必须传播，不能伪装成损坏皮肤。
- `Drawable.Empty()` 是普通值，不具有 `Suppress` 魔法语义。测试中的 fake `oms-simple` 只证明末端回落语义，不代表文件型 canonical fallback 已接入。

## semantic taxonomy 地雷

- `GameplaySkinSlotCatalog`从C4起是版本化作者ABI与codec/validator/resolver/consumer/doc唯一ID authority；Common v1 + BMS v1共28项，digest由文档一致性测试锁定。稳定ID只用小写ASCII点分段并ordinal精确查询；catalog顺序没有provider precedence、z-order、绘制或布局含义。
- catalog requirement/suppress eligibility与`GameplaySkinRuntimeCapabilitySet`分层；renderer支持不能改目录语义。descriptor与exact lane/keymode/stage/target context分离，新接线只能使用catalog descriptor；旧raw resolver只保留兼容入口，不能形成第二张ID表。
- critical 是 lane surface、judgement line、note、LN head/body、mine、active lane-cover fill；LN tail cap 和其它表现件 optional。lane-cover fill 只能挂在引擎强制 geometry/clip host 内，BGA viewport 只呈现引擎拥有的 content surface。
- shared codec已对未知版本/字段/ID、scope/type/index/selector、duplicate/escaping/case产生稳定`OMS-SKIN-CODEC-NNN`；但C5 scene/manifest仍未实现，不能把catalog存在冒充全部slot已有renderer。

## 生命周期地雷

- resolver 不自动 dispose 被 validator 拒绝的 `Drawable`/`IDisposable`：候选可能是 provider 缓存或共享值，擅自释放会造成双重释放/悬空引用。
- BMS lane-resource candidate与revision-scoped owner已进入C4 production material resolver：materializer返回前owner取得所有权并完成基础验证，winner/rejected都只借用；失败provisional只释放自身，成功替换先detach旧consumer再释放superseded owner。独立fixture仍不能替代SkinManager/ruleset/actual drawable产品证据。
- C4 production纵切覆盖BMS Note/LN与mania Note/Hold/KeyVisual；不能外推到C5剩余optional slot。其它slot仍须逐项冻结缓存、Drawable parenting/thread affinity和真实回收；一个已挂parent的`Drawable`不能被多个消费方直接复用。provider应在返回`Provide`前完成会分配资源的验证。
- critical `LongNoteBody` 的 source-bound visual 和程序化默认 visual 共用一个状态宿主；它只投影真实 `DrawableBmsHoldNote` 的 Idle/Holding/Broken，不创建第二套 gameplay state authority。异步候选发布时必须立即应用当时状态，之后才按约 `80ms` 过渡；素材与 resolved width 则由同一 revision-scoped material 一起拥有。
- catalogued诊断使用public code/SlotId；process-local exception、path、resource value、record ID/hash不进入持久文本。成功publication后product sink才异步输出去重、确定排序的safe摘要，日志故障不能影响commit；失败候选不得留下已生效日志。

## precedence 与测试夹具

- 现有相对authority固定为legacy beatmap direct visual compatibility → selected public document → selected legacy ruleset candidates → ruleset resources → protected/canonical → programmatic末端。新beatmap-local public作者格式已排除；legacy direct visual不能消费public section，也不能被后层Suppress穿透。
- mania-only OMS 测试环境里，`Ruleset.Value.CreateInstance()` 可能取得 mania ruleset，却配到通用 `Beatmap`，触发 `ManiaBeatmap` 强转失败。测试 generic provider container 时使用夹具自己声明的 `CreateRuleset()`，不要据此修改生产 transformer。

C4已经以ordinary/managed/external真实`SkinManager` current revision、ruleset prepare与actual BMS/mania drawable证明public codec/catalog/resolver；package+layout+material由同一owner提交。它仍不代表C5 scene/event、全部optional slot、C6 script或C7 `oms-simple`已接入，程序化`OmsSkin`尚未退出。
