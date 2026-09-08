---
name: reference_gameplay_skin_slot_contract
description: 三态 Value/ToString、候选 ownership、Drawable.Empty 与 generic test ruleset 误判地雷
metadata:
  node_type: memory
  type: reference
---

# Skin slot 三态地雷

public slot、requirement/applicability/runtime 支持只读 [catalog](../../doc_md/other/GAMEPLAY_SKIN_PUBLIC_CATALOG_V1.md) 与 [P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)；当前 gate 读 [STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)。

## 返回值不替代 resolver 合同

- 平行 SkinSlotResult/resolved entry 不改 nullable ISkin 兼容 ABI。default=Inherit；production 只读准备完成的显式 winner，不能靠 null/缺字典项现场重跑决策。
- 三态是普通 readonly struct；改成 record struct 后生成 ToString 可能枚举 Value，而非 Provide 下 Value 会抛异常。这是选择类型形状的实际故障依据。
- Provide 表示 provider 已构造并做基础验证，Inherit 转下一 authority；Suppress 受 catalog eligibility/runtime 决定。Drawable.Empty 是普通值，没有 Suppress 魔法。
- resolver 按传入 provider 顺序处理可恢复 provider/validator 失败；取消必须传播，不能伪装坏包。programming violation 与该层已定义可恢复失败不可混成宽泛吞异常。
- catalog 顺序不表示 z-order/layout/provider precedence；renderer 支持也不能反改 author ABI。NotApplicable、Optional 和 Suppress permission 是不同层。

## Candidate 生命周期

- resolver 不自动 dispose 被 validator 拒绝的 Drawable/IDisposable；它可能是 provider cache/shared 值，擅自释放会双重释放。
- materializer 返回前 revision owner 接管，winner/rejected 都只借用；一个已挂 parent 的 Drawable 不能直接给多个 consumer，共享资源不等于共享 Drawable。
- failed reload 仅回收 provisional；旧 consumer detach 后再退役 owner。完整 lease/scheduler 见 [[reference_skin_atomic_reload_detach]]，BMS borrow 见 [[reference_gameplay_skin_lane_resource_compatibility]]。
- LN body 的 source-bound/default visual 共用真实 DrawableBmsHoldNote 状态宿主；异步挂载先投影当下 Idle/Holding/Broken，再做后续 transition，不自建玩法状态。
- 日志 observer 不能持 material/package/lease 或改变已成功 commit，见 [[reference_gameplay_skin_codec_material]]。

## 测试误判

- fake oms-simple 只证明末端回落语义，不证明文件型 canonical 已接入。
- beatmap legacy direct visual 优先权不等于开放 public beatmap-local authoring；后层 Suppress 不能穿透该高层 compatibility。
- mania-only 测试环境可能 Ruleset.Value.CreateInstance() 得到 mania，却配 generic Beatmap，触发 ManiaBeatmap 强转失败。测 generic provider container 时使用 fixture 声明的 CreateRuleset，不据此修改生产 transformer。
- fixture/DTO/cursor 独立通过不证明 manager→source→ruleset prepare→actual host 链，也不刷新人工签收。
