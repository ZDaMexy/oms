---
name: reference_gameplay_skin_lane_identity
description: Skin V1 neutral lane/group stable identity、side/role 与 logical/visual index 地雷
metadata:
  node_type: memory
  type: reference
---

# gameplay skin lane identity 召回

权威状态与硬约束见 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md) / [CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)；本文件只保存实现地雷。

## 已冻结 primitives

- `GameplaySkinLaneGroupId` / `GameplaySkinLaneId` 是不同强类型，使用非敏感的小写 ASCII 点分 opaque topology token 与 ordinal equality；不得嵌入用户、包、资源名或路径信息。它们不是 manifest/event JSON ABI，hash 只限进程内。
- 同一 ID 不得分配给两个不同 semantic group/lane；同一语义实体跨不改变 topology 的 revision 重建 identity 时必须复用 ID。ID 跨 presentation style、视觉重排、geometry、skin reload/topology-preserving layout revision 稳定；同 LaneId 的 group membership 与 role 不漂移。
- 跨上述 topology-preserving revision 关联只比较 `.Id`；完整 `GameplaySkinLaneGroupIdentity` / `GameplaySkinLaneIdentity` equality 还包含当前 metadata，因此 side 改变时整体 identity 不相等。
- role 是 `Key/SpecialKey/Scratch`。未来 adapter 必须将 mania odd-stage 的 stage-local centre 映为 `SpecialKey`；它仍是 key input，绝不能因为 legacy fallback token `S` 而赋予 scratch gameplay truth。note/LN/mine 是对象类型，不是 lane role。
- side 是 `Neutral/Primary/Secondary` 的逻辑 player/deck presentation side，不是屏幕 Left/Right、BGA side 或 binding owner。5K/7K P1/CenterP1 为 Primary、P2/CenterP2 为 Secondary；9K 为 Neutral；14K 两 deck 分别 Primary/Secondary。

## 下一层 topology 地雷

- 当前 primitives 故意没有 global/group-local logical/visual index、keymode/style、action/source channel 或 geometry；这些必须由后续 aggregate/snapshot 显式携带，不能塞进 stable ID/equality。
- `BmsLaneLayout.Lanes` 按 logical `LaneIndex` 存储，即使 P2/CenterRightScratch 把 S1 画到最右，枚举位置也没有变成 visual index。后续 adapter 必须按最终 screen order 建 visual inverse mapping。
- 14K logical lanes 是 `S1,K1..K7,K8..K14,S2`；两个 skin group 分别覆盖 logical `[0..7]` 与 `[8..15]`、各 8 lane。group-local 与 global index 必须同时保存。
- mania `Column.Index` 是跨 stage global index，`StageDefinition.IsSpecialColumn()` 接受 stage-local index。双 5+5 special 的 global index 是 2/7；不能对 total columns 求一次中心，也不能把 `ManiaAction` enum ordinal 当 group-local identity。
- mirror/random/rearrangement 改 hit object 的目标 lane，而不改变固定 playfield topology；对象事件应发布 mod 后目标 LaneId，不能从原始 source channel 反推。

当前第三切没有 adapter、真实 token catalog、`GameplaySkinLayoutContext`、layout solver 或生产 `SkinManager` 接线。
