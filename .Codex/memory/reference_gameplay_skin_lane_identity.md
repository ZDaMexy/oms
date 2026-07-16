---
name: reference_gameplay_skin_lane_identity
description: Skin V1 neutral lane/group stable identity、topology snapshot、internal adapter 与 logical/visual index 地雷
metadata:
  node_type: memory
  type: reference
---

# gameplay skin lane identity 召回

权威状态与硬约束见 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md) / [CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)；本文件只保存实现地雷。

## 已冻结 identity 与 topology

- `GameplaySkinLaneGroupId` / `GameplaySkinLaneId` 是不同强类型，使用非敏感的小写 ASCII 点分 opaque topology token 与 ordinal equality；不得嵌入用户、包、资源名或路径信息。它们不是 manifest/event JSON ABI，hash 只限进程内。
- 同一 ID 不得分配给两个不同 semantic group/lane；同一语义实体跨不改变 topology 的 revision 重建 identity 时必须复用 ID。ID 跨 presentation style、视觉重排、geometry、skin reload/topology-preserving layout revision 稳定；同 LaneId 的 group membership 与 role 不漂移。
- 跨上述 topology-preserving revision 关联只比较 `.Id`；完整 `GameplaySkinLaneGroupIdentity` / `GameplaySkinLaneIdentity` equality 还包含当前 metadata，因此 side 改变时整体 identity 不相等。
- role 是 `Key/SpecialKey/Scratch`。mania internal projection 将 odd-stage 的 stage-local centre 映为 `SpecialKey`；它仍是 key input，绝不能因为 legacy fallback token `S` 而赋予 scratch gameplay truth。note/LN/mine 是对象类型，不是 lane role。
- side 是 `Neutral/Primary/Secondary` 的逻辑 player/deck presentation side，不是屏幕 Left/Right、BGA side 或 binding owner。5K/7K P1/CenterP1 为 Primary、P2/CenterP2 为 Secondary；9K 为 Neutral；14K 两 deck 分别 Primary/Secondary。
- `GameplaySkinLaneTopologyEntry` 保存 global/group-local logical/visual 四类零基 index；group/snapshot 提供 defensive immutable logical/visual order 与强类型 lookup。单 snapshot 拒绝 null/empty、重复 ID、membership conflict、非 permutation、local/global order 不一致与 group 非连续块。
- public process-local `GameplaySkinLaneTopologyTransitionValidator` 只校验调用方已声明为 topology-preserving 的两个 neutral snapshot：GroupId/LaneId set、group logical index、lane membership/role/global 与 group-local logical index 稳定；side 和全部 visual index/order 可变。不要比较完整 identity equality。
- validator 本身不含 native context，9K BMS/PMS neutral shape 相同仍会通过；外层 internal owner 以 BMS exact keymode、mania exact ordered stage-column vector 补上 process-local continuity/revision。不能据此把 helper 或 owner 写成完整 layout transition ABI；详见 [topology publication/revision](reference_gameplay_skin_topology_revision.md)。

## internal projection 地雷

- identity 故意不含 index；snapshot 虽已显式携带四类 index，仍故意没有 keymode/style、action/source channel、geometry/bounds、revision/native context。它不是 full `GameplaySkinLayoutContext` 或 wire/manifest ABI，不能把这些字段继续塞进 stable ID/equality。
- `BmsLaneLayout.Lanes` 按 logical `LaneIndex` 存储，即使 P2/CenterRightScratch 把 S1 画到最右，枚举位置也没有变成 visual index；只能读取 solver 显式产出的 `Lane.VisualIndex`，不得按 `RelativeStart` 或枚举位置反推。
- canonical lane count 不等于 canonical composition：额外 scratch 可在总数不变时把 7K 变成 `S1,K1..K6,S2`、把 9K 变成假 scratch。projection 必须逐 lane 校验 `(LaneIndex, Action, IsScratch)`，不能只看 count。
- 14K logical lanes 是 `S1,K1..K7,K8..K14,S2`；两个 skin group 分别覆盖 logical `[0..7]` 与 `[8..15]`、各 8 lane。group-local 与 global index 必须同时保存。
- mania projection 只接受 1–2 stage、每 stage 1–10 keys，并先复制可变 stage 列表；single side=Neutral，dual stage 0/1=Primary/Secondary。`StageDefinition.IsSpecialColumn()` 接受 stage-local index；双 5+5 special 的 global index 是 2/7，mixed 4+5 是 6。global index 用 stage count 前缀和，不能对 total columns 求一次中心，也不能把 `ManiaAction` enum ordinal 当 group-local identity。
- mirror/random/rearrangement 改 hit object 的目标 lane，而不改变固定 playfield topology；对象事件应发布 mod 后目标 LaneId，不能从原始 source channel 反推。

identity/topology/neutral validator 与 topology-only publication/ruleset-native continuity 是不同层；它们都不等于 full `GameplaySkinLayoutContext`、geometry/layout solver、production attachment/event `layoutRevision`/wire ABI 或 `SkinManager` 接线。当前完成度只看 P1-A STATUS。
