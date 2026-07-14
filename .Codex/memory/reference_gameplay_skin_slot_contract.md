---
name: reference_gameplay_skin_slot_contract
description: Skin V1 平行三态 slot 的 fail-open、provider precedence、诊断与候选生命周期地雷
metadata:
  node_type: memory
  type: reference
---

# gameplay skin slot 三态合同

权威状态与硬约束见 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md) / [CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)；本文件只做实现地雷召回。

## 稳定合同

- 使用平行 `SkinSlotResult<T>`，不改变 nullable `ISkin` ABI。`default(SkinSlotResult<T>) == Inherit`；默认 requirement 是 `Critical`。
- 三态结果使用普通 `readonly struct`；不要改成会自动枚举属性的 `record struct`，否则生成的 `ToString()` 可能读取非 `Provide` 状态下会抛异常的 `Value`。
- `Provide` 只表示已完成 provider 自身构造/基础验证的值；`Inherit` 继续链；`Suppress` 只在 optional slot 终止。critical `Suppress` 必须诊断后继续。
- resolver 严格使用调用方提供的 provider 顺序。provider/GetSlot/构造异常、validator=false 或 validator 异常都诊断后逐组件继续；取消异常必须传播，不能伪装成损坏皮肤。
- `Drawable.Empty()` 是普通值，不具有 `Suppress` 魔法语义。测试中的 fake `oms-simple` 只证明末端回落语义，不代表文件型 canonical fallback 已接入。

## semantic taxonomy 地雷

- `GameplaySkinSlotCatalog` 是 `SV1-1` 内部语义分类，不是作者 manifest ABI、layout descriptor 或旧 lookup 的一对一重命名。稳定 ID 只用小写 ASCII 点分段并采用 ordinal 精确查询；catalog 顺序没有 provider precedence、z-order、绘制或布局含义。
- catalog descriptor 与 lane/keymode/side/result context 通过 `GameplaySkinSlotLookup<TContext>` 分离。新接线只能使用 descriptor overload；旧 raw resolver 是 uncatalogued compatibility 入口，不能拿来宣称全 API 已强制 taxonomy。
- critical 是 lane surface、judgement line、note、LN head/body、mine、active lane-cover fill；LN tail cap 和其它表现件 optional。lane-cover fill 只能挂在引擎强制 geometry/clip host 内，BGA viewport 只呈现引擎拥有的 content surface。
- 未知 ID 当前只有 `TryGet=false`，manifest parser 与作者诊断尚未实现；不要写成已能加载第三方 scene/schema。

## 生命周期地雷

- resolver 不自动 dispose 被 validator 拒绝的 `Drawable`/`IDisposable`：候选可能是 provider 缓存或共享值，擅自释放会造成双重释放/悬空引用。
- 第九切已为 BMS 六字段冻结首个 revision-scoped owner 合同：materializer 返回前 owner 取得所有权并完成基础验证，winner/rejected 都只借用；失败 provisional 只释放自身，成功替换先 detach 旧 consumer 再释放 superseded owner。它仍只有 internal interface/fake fixture，不是 production reload。
- concrete provider 与最终消费方仍须冻结缓存、Drawable parenting/thread affinity 和真实回收；一个已挂 parent 的 `Drawable` 不能被多个消费方直接复用。provider 应尽量在返回 `Provide` 前完成会分配资源的验证。
- catalogued 诊断用 `SlotId` 作为稳定字段；process-local `Slot`/`Exception` 已排除出 JSON，安全 `ToString()` 也不展开。旧 raw lookup 的 `SlotId` 仍为 null。`ProviderName` 的安全只靠 provider 遵守“非敏感 authority、不得含绝对路径”合同，当前没有自动脱敏。

## precedence 与测试夹具

- 现有相对 authority 固定为 beatmap-local → selected/user layers → ruleset resources → protected built-in。先命中的 beatmap `Provide` 不能被后层 `Suppress` 穿透。
- mania-only OMS 测试环境里，`Ruleset.Value.CreateInstance()` 可能取得 mania ruleset，却配到通用 `Beatmap`，触发 `ManiaBeatmap` 强转失败。测试 generic provider container 时使用夹具自己声明的 `CreateRuleset()`，不要据此修改生产 transformer。

前两个合同切片都没有接 `SkinManager`、真实 `.osk`、layout/codec/scene/event/script，也没有切换或删除程序化 `OmsSkin`。
