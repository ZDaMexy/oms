---
name: reference_gameplay_skin_topology_revision
description: Skin V1 topology-only publication、owner-local revision、BMS/mania native continuity 与原子失败地雷
metadata:
  node_type: memory
  type: reference
---

# gameplay skin topology publication/revision 召回

权威状态与硬约束见 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md) / [CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)；本文件只保存实现地雷。

## shared owner

- `GameplaySkinLaneTopologyPublication` 只绑定 exact immutable `Topology` reference 与 process-local `Revision`。一个新 owner 首次成功 publication 为 0；之后每次成功 publication checked `+1`，即使内容等价或只是独立重建也递增，所以 revision 不是内容 hash。
- generic owner 先执行 ruleset 提供的 exact native-context comparator，再跑 neutral transition validator 与 overflow 检查；native mismatch、comparator exception、neutral rejection、invalid input 与 overflow 都保持上一成功 `Current` 且不消耗 revision。
- `TNativeContext` 必须 immutable、非敏感；owner 保存 context 本身，不会替调用方冻结可变对象。新进程/新 owner 可重新从 0 开始，owner 也不保证 thread safety。
- 这是 topology-only process-local contract，不是 package/component revision、manifest/serialization ABI、security boundary、event envelope 的 `layoutRevision`、wire producer 或完整 `GameplaySkinLayoutContext`。

## ruleset continuity

- BMS continuity authority 只有 exact `BmsKeymode`；`AppliedStyle` 是 presentation metadata，可在 neutral transition 允许时变化。9K BMS/PMS 的 neutral ID/role shape 相同，validator 会接受，所以 internal owner 必须在它之前按 keymode 拒绝。
- mania continuity authority 是防御性复制的 exact ordered stage-column vector；`[4,5]`、`[5,4]` 与 `[9]` 即使总列数相同也不是同一 native context。不能只比较 total columns，也不能长期持有可变 beatmap stage collection。
- mania projection 不接受调用方传入的任意 topology；它只从已校验/复制的 stage vector 生成 canonical group/lane token、side、role 与 index。
- ruleset wrapper 必须绑定 shared owner 发出的 exact topology reference，但现有 ruleset owner 先让 shared owner commit，再构造 wrapper。合同保证的是 comparator/validation/overflow 等预期拒绝路径的 owner-state 原子性；不要把灾难性对象分配失败写成跨 assembly transaction，也不要据此宣称 `SV1-2` hot reload 原子切换已实现。

## 当前边界与证据

- shared carrier/owner、internal BMS/mania wrapper 与 fixture 不证明 production attachment、playfield/renderer、event producer/wire、`SkinManager` 或资源生命周期已接线；当前完成度只看 P1-A STATUS。
- 验证必须覆盖 shared owner/transition、BMS 与 mania publication/topology；精确数字和 wider gate 只看 P1-A STATUS/CHANGELOG。
