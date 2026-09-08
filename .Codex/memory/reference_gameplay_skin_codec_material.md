---
name: reference_gameplay_skin_codec_material
description: 唯一 shared codec、specificity 遮蔽、诊断 observer 与 beatmap-local 边界地雷
metadata:
  node_type: memory
  type: reference
---

# Gameplay codec/material 地雷

public ID/字段/适用性只读 [public catalog](../../doc_md/other/GAMEPLAY_SKIN_PUBLIC_CATALOG_V1.md)，稳定 pipeline 读 [P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)，当前能力读 [STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)。本页不复制 slot 数量、完成史或候选矩阵。

## 输入与 resolver 的反直觉处

- package 的根 skin.ini 只捕获/tokenize 一次，public 与 legacy adapter 消费同一 immutable token stream；consumer 不重开文件，ruleset 不另写 tokenizer。
- Absent、DeclaredEmpty、Invalid、Valid、Suppress 必须区分。malformed 第一声明仍占 duplicate target；后行不能借“第一行没完整 tokenize”夺 winner。
- package 内 specificity 为 ruleset→keymode→stage-mode→scope。最高项遮蔽该 package 更宽声明：它 Inherit/empty/invalid 时转下一 authority，不回头聚合同 package。
- legacy beatmap direct visual compatibility 优先于 selected public；但它不读取 public section。selected public/legacy、ruleset resource、protected/canonical、programmatic 的顺序以合同为准，不构造伪 canonical candidate。
- Required/Recommended 不可 Suppress；Optional 仍受 applicability/runtime capability。null、缺 entry、异常、Drawable.Empty 都不是三态声明。
- Target 的 ruleset/keymode/stage/scope、LaneId/GroupId 与四类 index 须对应 exact topology；不要从 lane count、geometry、RelativeStart 或 drawable 顺序推导。

Legacy raw index、source-bound frame/width 与 borrow 地雷见 [[reference_gameplay_skin_lane_resource_compatibility]]；accepted provenance 见 [[reference_gameplay_skin_config_presence]]。

## Diagnostic observer 也可能拖住旧 owner

- parse、catalog validate、resource/scene prepare、graph/material 构造止于 background prepare；publication/lease 的统一边界见 [[reference_skin_atomic_reload_detach]]。
- 稳定诊断去重、排序、完整 persistence-safe payload 在 immutable material 构造时预生成。成功 commit 后 observer 只捕获 immutable 字符串和轻量 receipt，不能捕获 material/snapshot/package/texture/lease。
- 文本只含 public code、catalog ID、stable target/index、source kind、contract version；作者值、路径、display name、record ID/hash、exception text 不持久化。
- queue/listener/observer 失败不得从成功 commit 逸出、改变结果或延长旧 material 生命周期。诊断故障不应触发第二次材料发布。

## Scene 与未开放作者面

- manifest/scene 的唯一 codec 在 background prepare 做 UTF-8、duplicate/unknown/path/type/target/resource/canonical 与预算检查；renderer 只读 prepared graph，不重读来源或二次 resolve。
- Snapshot/Reset 与运行事件来自 engine-owned bounded stream；engine 可发 GameplayResumed，scene ABI 不接受 gameplay.resume，因为 Snapshot 重建 Running。CLR envelope 构造能力不等于 scene author ABI。
- scene/slot 故障只隔离其自身表现，不能获得判定、input、score、clock、BGA 内容 authority。
- 新 beatmap-local public authoring 不在已开放面；真实 WorkingBeatmap.Skin 仍可惰性返回同一只读 LegacyBeatmapSkin direct compatibility。没有 sidecar/producer/revision authoring ownership 时，测试注入不能当用户能力。
- 被 resolved material 取代的旧 lane-colour/bucket snapshot factory 不应恢复。Create(BmsLaneLayout,...)、raw requirement overload、PublishForTesting 等 isolated seam 不计产品进度；是否已有 sandbox/通用 authorization 只看 P1-A 当前门。

事件与版本导航：[[reference_gameplay_skin_event_envelope]]、[[reference_gameplay_skin_capability_negotiation]]。
