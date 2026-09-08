---
name: reference_gameplay_skin_layout_snapshot
description: 唯一 layout publication、mania stage-local 映射与 exact owner 重入/注入地雷
metadata:
  node_type: memory
  type: reference
---

# Gameplay layout snapshot 地雷

完整 context/snapshot、consumer 矩阵与验证面见 [P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)；当前门读 [STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)。这里保存为什么不能恢复第二套 geometry 权威。

## 判断是否同一结果

- GameplaySkinLayoutContext 绑定 native/ruleset context、既有 topology、presentation/environment 与 exact package/revision；Snapshot 是一次 solve 的完整 immutable 结果。
- GameplaySkinLayoutPublication 绑定 neutral snapshot、引用它的 typed adapter、resolved material 与 prepared scene。Current 是 view，不是第二交换点；event target/revision 也从同一 publication 取得。
- 同值 snapshot 不等于同一 authority。consumer 不能从 drawable size、raw profile、默认 fixed rect、event payload 或 topology-only revision 再算一份，也不能重跑 source/material lookup。
- exact one-shot 必须在 shared GameplaySkinLayoutRevisionOwner 的锁内。若只在 BMS/mania helper 防二次 prepare，cached descendant 可直接重入 shared API，造成旧 child 持 A、late child 读 B。已有 exact current 后在 work lease/solve 前拒绝第二次 prepare。

## Adapter 容易错在哪里

- BMS keymode 来自 parser-owned resolution；BmsPlayfieldLayoutProfile 只是唯一 solver 内部配置或 isolated compatibility 输入，不是另一生产 geometry。
- Mania 必须使用真实 single/dual stage-column vector；special key 按 stage-local column 判，不用 total columns、global modulo 或 enum ordinal。
- logical/visual、global/group-local index 都是显式 order，不是 stable ID。Mirror/Random/S-Random 改对象最终目标 lane，不改固定 topology；object、keysound 与 skin lookup 使用同一目标 LaneId。
- BGA 的最终 viewport/rect 属于该 layout；内容/timeline/seek 仍属 P1-L。统一 viewport 不能证明统一 decoder/clock。
- menu/shell/background 不因使用 skin texture 就成为作者 gameplay layout surface。

## Geometry fallback 与构造边界

- 每字段分别验证 finite、正值、range、safe screen 与 non-overlap，并给稳定诊断；fallback 也一次产出完整 snapshot，不拼部分新/旧 geometry。
- 测试复现要包括窄/宽 aspect、DPI、safe-area 与 14K 双 field/scratch/gap/BGA/HUD；普通 7K 正常不证明这些关系。
- 完整 host 在 child load 前通过 enclosing exact dependency scope 完成 publication。无 publication/material 不临时借 compatibility/default geometry 或 post-commit fallback。
- prepared carrier 只属于签发 owner；另一 owner carrier、同 root 第二 provider、compatibility→exact 升级、adapter 未引用 exact neutral snapshot 都属于 authority 违约。
- isolated compatibility 是显式 detached test seam，也应一次构造完整 graph；不能先让真实 provider 可见，再升级。

Prepare/commit、取消窄窗、late attach、lease/detach/retire 统一去 [[reference_skin_atomic_reload_detach]]；不要在 layout consumer 再做一套可交换状态。素材解析见 [[reference_gameplay_skin_codec_material]]，stable identity 见 [[reference_gameplay_skin_lane_identity]]。
