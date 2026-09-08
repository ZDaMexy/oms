---
name: reference_gameplay_skin_lane_identity
description: stable LaneId 与四类 index 的区别、mania stage-local special key 和 BMS visual 投影地雷
metadata:
  node_type: memory
  type: reference
---

# Lane identity / index 地雷

ID/topology 完整合同见 [P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md) 的“共享与分离约束”；public target 见 [catalog](../../doc_md/other/GAMEPLAY_SKIN_PUBLIC_CATALOG_V1.md)。本页不复述 production 完成态。

## 相等不是同一个问题

- LaneId / GroupId 是不同强类型的非敏感 opaque token，ordinal 比较，不嵌用户/包/资源/path。公开 target/scene/event 可引用稳定 token；CLR 对象、进程内 hash 不是 wire 表示。
- topology-preserving 的 style、视觉重排、geometry/reload 跨 revision 关联比 Id；完整 identity equality 还含 side 等 metadata，因此 presentation 改变时可能不等。membership/role 仍不能漂移。
- role 是 Key/SpecialKey/Scratch，note/LN/mine 是对象类型。Mania special key 仍是按键，不因 legacy fallback token S 变成 scratch gameplay。
- side 是 Neutral/Primary/Secondary 逻辑 player/deck presentation，不是屏幕 Left/Right、BGA side 或 binding owner。

## 四类 index 的复现例

- GlobalLogical、GroupLocalLogical、GlobalVisual、GroupLocalVisual 是当次 order。stable ID 不携 index；snapshot 保存显式 permutation/连续 group blocks，拒绝重复 ID、membership/order 冲突。
- BmsLaneLayout.Lanes 按 logical LaneIndex 存放；P2/CenterRightScratch 把 S1 画右边也不改变枚举意义。视觉序读显式 VisualIndex，不能按 RelativeStart 或枚举位置猜。
- 总 lane count 一样不代表 composition 一样：假 S1,K1..K6,S2 也有 8 lane。BMS adapter 要验证 LaneIndex/Action/IsScratch，不只数数量。
- 14K logical 顺序是 S1,K1..K7,K8..K14,S2，两个 group 为 logical 0..7、8..15；global 与 group-local 不能丢一个。
- Mania 先复制真实 1～2 stage、每 stage 1～10 keys。special 按 stage-local column；双 5+5 的 global special 是 2/7，mixed 4+5 是 6。global index 用前缀和，不对 total columns 求一个中心或用 ManiaAction enum ordinal。
- Mirror/Random 改对象 post-mod 目标 lane，keysound/resource/event 同走目标 LaneId，固定 topology 不变。有 exact permutation 的 mod 同步搬 timeline；S-Random 没有单一 permutation 时禁用受影响 timeline，不伪造映射。

## Neutral 验证缺少什么

- neutral transition validator 只验证调用方声明为 topology-preserving 的输入：ID set、logical index、membership/role 稳定；side/visual order 可变。
- 9K BMS/PMS 的 neutral shape 相同仍能通过；native continuity 必须额外比较 exact keymode。Mania 比 exact ordered stage vector，不能仅比总列。
- topology snapshot 故意没有 keymode/style、action/channel、geometry、revision/native context；不能往 ID/equality 塞这些字段修补上层缺失。
- topology-only revision 不等于 layout 或 package revision；native continuity 见 [[reference_gameplay_skin_topology_revision]]，最终唯一 publication 见 [[reference_gameplay_skin_layout_snapshot]]。
