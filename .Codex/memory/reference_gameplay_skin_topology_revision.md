---
name: reference_gameplay_skin_topology_revision
description: topology-only revision 计数与 exact native continuity，避免冒充最终 layout publication
metadata:
  node_type: memory
  type: reference
---

# Topology revision 地雷

稳定合同见 [P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)，最终生产交换点见 [[reference_gameplay_skin_layout_snapshot]]；这里仅说明 topology-only primitive 的诊断边界。

- GameplaySkinLaneTopologyPublication 只绑定 exact Topology reference 与 process-local Revision。新 owner 首次成功为0，之后每次成功 checked +1，内容等价/独立重建也递增，因此不是 content hash。
- 先做 ruleset native-context comparator，再 neutral transition、overflow 检查；native mismatch、comparator exception、neutral reject/invalid/overflow 都保留上一 Current，不消耗 revision。
- owner 保存 TNativeContext 本身，不替调用者冻结 mutable 对象；输入须 immutable/非敏感。新进程/owner可从0开始，该 primitive不承诺线程安全。
- BMS continuity 比 exact keymode，style只是 presentation。9K BMS/PMS neutral shape相同会通过通用 validator，不能省 native比较。
- Mania 比防御性复制的 exact ordered stage vector；[4,5]、[5,4]、[9]总列相同但不同 context。不要保留 mutable beatmap stage collection，projection不接任意调用方 topology。
- wrapper须持 topology owner签发的 exact reference；同值重建不能代替。上述拒绝原子性不等于 package/material/renderer 热重载已安全。
- 最终 exchange 是 GameplaySkinLayoutRevisionOwner.CurrentPublication；neutral Current只是view，package/layout/material/scene一起发布。topology owner不能成为第二个production交换点，也不是event envelope的layoutRevision或wire ABI。
- identity/index差异见 [[reference_gameplay_skin_lane_identity]]，完整 participant/lease lifecycle见 [[reference_skin_atomic_reload_detach]]。
