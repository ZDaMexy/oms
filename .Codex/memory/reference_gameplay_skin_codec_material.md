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
- section 的 Common/Bms 是 catalog 元素族，不是目标玩法。BGA 只适用于 BMS，元素仍属于 Common；写入 `[GameplaySkin.Bms:1]` 会失败，玩法由 target 的 ruleset 限定。2026-09-30 作者验证曾误写该 section；必须先用同基线成功例确认真实选择与挂载，防止共用的无效声明掩盖负例预期的拒绝原因。
- canonical 原件无错而普通导入出现 `OMS-SKIN-CODEC-008` 时，先查末尾公共段与追加 `[General]` 之间的游戏说明：importer 使用整行 `//`。公共 parser 兼容 trim 后的整行注释即可读取既有导入内容，不迁移或重写用户文件；不可改成全局截断 `//`。复验应涵盖真实说明位置、General 元数据、quoted 值和 round-trip，并保留行内 `//`、未知字段、BOM/坏 header 的严格边界。
- Absent、DeclaredEmpty、Invalid、Valid、Suppress 必须区分。malformed 第一声明仍占 duplicate target；后行不能借“第一行没完整 tokenize”夺 winner。
- scene变体遇到重复key时，只记`DuplicateField`却仍加入case，会在immutable字典构造时抛异常，来不及返回输入诊断。应在codec边界记录后跳过重复项，整份输入仍拒绝，不削弱构造器唯一键契约；同时复验旧typed resource/visible路径，不能只测新增literal来源。复现与修正见[Header记录](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-28gameplay33header原图与歌曲literal变体)。
- package 内 specificity 为 ruleset→keymode→stage-mode→presentation→scope。最高项遮蔽该 package 更宽声明：它 Inherit/empty/invalid 时转下一 authority，不回头聚合同 package。
- legacy beatmap direct visual compatibility 优先于 selected public；但它不读取 public section。selected public/legacy、ruleset resource与真实canonical的顺序以合同为准；旧programmatic阶段仅作历史/隔离测试，不重新接入产品链，也不构造伪canonical candidate。
- Required/Recommended 不可 Suppress；Optional 仍受 applicability/runtime capability。null、缺 entry、异常、Drawable.Empty 都不是三态声明。
- Target 的 ruleset/keymode/stage/presentation/scope、LaneId/GroupId 与四类 index 须对应 exact topology；不要从 lane count、geometry、RelativeStart 或 drawable 顺序推导。
- 原图正确、材料诊断为空，却不响应scene尺寸或LN变体时，先核真实native consumer的RoutedNodes、AppliedSceneNodeIds及fallback隐藏，不能只核模板JSON。BMS Note/LN的C4 payload是`IBmsResolvedNoteMaterial`，并非`GameplaySkinPublicSlotMaterial`；按后者CLR类型筛material实例会合法匹配零个节点，留下原生尺寸。匹配应追同publication绑定文档的精确声明，再以真实pool实例与屏幕高度验证；具名26px/12px诊断见[IIDX33记录](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-18iidx33-游玩皮肤与公共表现能力)。
- BMS 5/7K 皮肤曾同时写左右皿两组 exact visual index，却没有区分适用样式；GetEntry 可选对当前素材，BindToPublication 仍会诊断另一组，因此“能进入游玩”不证明声明无错。必须用公开 presentation 选择实际样式，再严格验当前坐标；省略/any不获得推断许可，valid peer或合法排列不能掩盖wrong coordinates。旧包通过普通作者更新解决，不改用户源；版本与完整语法见public catalog。
- 完整成品矩阵应同时核 requested style、实际同一配置/布局以及MaterialSet.Diagnostics全空。故意缺省可由canonical补齐，不能因此笼统豁免第三方diagnostic；另保留当前样式误坐标与非当前样式不应用的正反用例。

Legacy raw index、source-bound frame/width 与 borrow 地雷见 [[reference_gameplay_skin_lane_resource_compatibility]]；accepted provenance 见 [[reference_gameplay_skin_config_presence]]。

## Diagnostic observer 也可能拖住旧 owner

- parse、catalog validate、resource/scene prepare、graph/material 构造止于 background prepare；publication/lease 的统一边界见 [[reference_skin_atomic_reload_detach]]。
- 稳定诊断去重、排序、完整 persistence-safe payload 在 immutable material 构造时预生成。成功 commit 后 observer 只捕获 immutable 字符串和轻量 receipt，不能捕获 material/snapshot/package/texture/lease。
- 作者 BGA 布局错误需进入既有持久批次，但不能把 snapshot 的全部环境诊断一并抄入。2026-09-30 首次合并曾把临时 `environment-window-fallback` 计成作者错误；现仅追加 `bms.layout.bga-viewports` 前缀，保留原材料日志范围。不能为了新诊断放宽原普通来源的数量断言；复现与修复证据见[作者能力记录](../../doc_md/other/BGA_SKIN_AUTHORING_20260930.md)。
- 文本只含 public code、catalog ID、stable target/index、source kind、contract version；作者值、路径、display name、record ID/hash、exception text 不持久化。
- queue/listener/observer 失败不得从成功 commit 逸出、改变结果或延长旧 material 生命周期。诊断故障不应触发第二次材料发布。

## Scene 与未开放作者面

- manifest/scene 的唯一 codec 在 background prepare 做 UTF-8、duplicate/unknown/path/type/target/resource/canonical 与预算检查；renderer 只读 prepared graph，不重读来源或二次 resolve。
- 作者CLI报 `OMS-SKIN-SCENE-008` 时先递归核JSON中的稳定ID段；生成器插入组号曾形成 `.turntable.0`，矩形/quad数值全部合法仍被拒绝。改用n0/n1等合法段并生成新候选；先看完整诊断命名空间，勿套用上文 `OMS-SKIN-CODEC-008` 的注释解析修复。复现取[32制作记录](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-18iidx32-pinky-crush-游玩候选)。
- 原红线已在其它作者层绘制时，必需 `playfield.judgement-line` 槽仍不能写 `Suppress`，否则CLI报 `OMS-SKIN-CODEC-016`；用显式透明材质Provide替换原生重复线，保持原槽合同和实际红线消费者。字体预算计算全部声明，不只当前布局使用的字体；SP/DP相同字形和度量应复用，限额只取[作者参考](../../skin-authoring/docs/REFERENCE.md#用自己的图片字形显示数字)，不在记忆复制旧数值。作品合同见[P1-A素材边界](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#iidx-素材作品约束)。
- Snapshot/Reset 与运行事件来自 engine-owned bounded stream；engine 可发 GameplayResumed，scene ABI 不接受 gameplay.resume，因为 Snapshot 重建 Running。CLR envelope 构造能力不等于 scene author ABI。
- scene/slot 故障只隔离其自身表现，不能获得判定、input、score、clock、BGA 内容 authority。
- 将原material的sprite包进新container时，slot只能留在唯一owner；normal/AUTO等后代必须继承，不能复制原sprite的slot。静态codec通过也不证明C3实际prepare可用：嵌套owner会在`prepareNode`明确报`OMS-SKIN-SCENE-021`，即使该分支当前不可见。先按真实owner/layer数据流递归检查整棵模板，不应放宽prepare保护或用后续超时掩盖首个错误。原图/UV/variant保全和唯一owner是两条独立验收。
- 新 beatmap-local public authoring 不在已开放面；真实 WorkingBeatmap.Skin 仍可惰性返回同一只读 LegacyBeatmapSkin direct compatibility。没有 sidecar/producer/revision authoring ownership 时，测试注入不能当用户能力。
- 被 resolved material 取代的旧 lane-colour/bucket snapshot factory 不应恢复。Create(BmsLaneLayout,...)、raw requirement overload、PublishForTesting 等 isolated seam 不计产品进度；是否已有 sandbox/通用 authorization 只看 P1-A 当前门。

事件与版本导航：[[reference_gameplay_skin_event_envelope]]、[[reference_gameplay_skin_capability_negotiation]]。
