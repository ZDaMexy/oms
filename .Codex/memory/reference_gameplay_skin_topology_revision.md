---
name: reference_gameplay_skin_topology_revision
description: Skin V1 topology-only primitive与C3唯一layout publication、BMS/mania native continuity及revision原子边界
metadata:
  node_type: memory
  type: reference
---

# gameplay skin topology与layout publication/revision召回

权威状态与硬约束见 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md) / [CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)；本文件只保存实现地雷。

## C3 当前层次（2026-08-30）

- 下文`GameplaySkinLaneTopologyPublication`及其owner仍是topology-only continuity primitive，只给solver提供exact identity/order/native-context输入；它不是当前production geometry publication。
- C3最终交换点只有`GameplaySkinLayoutRevisionOwner.CurrentPublication`。一个`GameplaySkinLayoutPublication`同时持有ruleset-neutral immutable `GameplaySkinLayoutSnapshot`与引用同一neutral snapshot的`IGameplaySkinLayoutAdapter`；`Current`只是从该publication派生的neutral view，不是第二publication。
- layout context将exact native context/keymode、topology、style、safe bounds/aspect/DPI、package/current revision、topology revision与layout revision绑定。live root的package revision和layout publication形成不可分割pair，失败保持exact旧pair；完整合同见[[reference_gameplay_skin_layout_snapshot]]。

## topology-only shared owner

- `GameplaySkinLaneTopologyPublication` 只绑定 exact immutable `Topology` reference 与 process-local `Revision`。一个新 owner 首次成功 publication 为 0；之后每次成功 publication checked `+1`，即使内容等价或只是独立重建也递增，所以 revision 不是内容 hash。
- generic owner 先执行 ruleset 提供的 exact native-context comparator，再跑 neutral transition validator 与 overflow 检查；native mismatch、comparator exception、neutral rejection、invalid input 与 overflow 都保持上一成功 `Current` 且不消耗 revision。
- `TNativeContext` 必须 immutable、非敏感；owner 保存 context 本身，不会替调用方冻结可变对象。新进程/新 owner 可重新从 0 开始，owner 也不保证 thread safety。
- 这是 topology-only process-local contract，不是 package/component revision、manifest/serialization ABI、security boundary、event envelope 的 `layoutRevision`、wire producer 或完整 `GameplaySkinLayoutContext`。

## ruleset continuity

- BMS continuity authority 只有 exact `BmsKeymode`；`AppliedStyle` 是 presentation metadata，可在 neutral transition 允许时变化。9K BMS/PMS 的 neutral ID/role shape 相同，validator 会接受，所以 internal owner 必须在它之前按 keymode 拒绝。
- mania continuity authority 是防御性复制的 exact ordered stage-column vector；`[4,5]`、`[5,4]` 与 `[9]` 即使总列数相同也不是同一 native context。不能只比较 total columns，也不能长期持有可变 beatmap stage collection。
- mania projection 不接受调用方传入的任意 topology；它只从已校验/复制的 stage vector 生成 canonical group/lane token、side、role 与 index。
- ruleset topology wrapper必须绑定topology owner发出的exact topology reference；这层只保证comparator/validation/overflow等预期拒绝路径的owner-state原子性。不能从它单独推导package/layout热重载已经实现；C3的原子切换由独立`GameplaySkinLayoutRevisionOwner`与C2 participant/lease协议完成，也不能反过来让topology owner形成第二个production exchange。

## topology primitive 边界与 C3 补充

- topology-only carrier/owner、internal BMS/mania wrapper与fixture本身仍不证明production attachment、playfield/renderer、`SkinManager`或资源生命周期；这些已由C3上层唯一layout publication和真实renderer另行闭合。不要把两层证明混写，或把process-local publication描述成event/wire ABI。
- 验证必须覆盖 shared owner/transition、BMS 与 mania publication/topology；精确数字和 wider gate 只看 P1-A STATUS/CHANGELOG。
