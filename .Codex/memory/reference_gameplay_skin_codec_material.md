---
name: reference_gameplay_skin_codec_material
description: C4 public catalog/shared codec/三态resolver、C5 exact material+scene/event publication、diagnostic与beatmap-local终态地雷
metadata:
  node_type: memory
  type: reference
---

# gameplay skin codec/material召回

权威状态与硬约束只看[P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)、[PLAN](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md)与[CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)；公共作者合同见[Gameplay Skin V1目录](../../doc_md/other/GAMEPLAY_SKIN_PUBLIC_CATALOG_V1.md)，C4历史边界见[C4交接](../../doc_md/other/SKIN_SYSTEM_C4_CODEC_MATERIAL_COMPLETION_HANDOFF_20260831.md)，C5当前边界见[C5交接](../../doc_md/other/SKIN_SYSTEM_C5_SCENE_EVENT_COMPLETION_HANDOFF_20260903.md)。本页只保存实现地雷。

## 单一authority

- `GameplaySkinSlotCatalog`是Common v1 + BMS v1共28项public ID的唯一authority；codec、applicability validator、resolver、BMS/mania consumer与文档生成都引用同一descriptor。runtime capability与catalog requirement/suppress eligibility分层，renderer支持不能改作者ABI。
- `Skin`只捕获一次exact `skin.ini` bytes并构造防御性immutable `GameplaySkinDocument`；public Common/BMS与legacy `[Mania]`/`[Bms]` adapter消费同一token stream。禁止consumer重开ini、二次tokenize或ruleset复制parser。
- document必须保留Absent/DeclaredEmpty/Invalid/Valid/Suppress、legacy token及exact source/configuration-content/package/current identity。malformed第一声明仍占duplicate target；后行不能借“第一行没完整tokenize”篡夺winner。

## target与resolver

- public `Target`显式携带ruleset、keymode、stage-mode、scope、stable LaneId/GroupId及全部适用logical/visual/global/group-local index；每项都必须与C3 exact topology一致。禁止从enum ordinal、lane count、geometry、`RelativeStart`或drawable顺序反推。
- package内specificity固定为ruleset → keymode → stage-mode → scope，最高specificity会遮蔽本package较宽声明；其`Inherit`、empty或invalid直接进入下一authority，不回头聚合本package。
- relative authority为legacy beatmap direct visual compatibility → selected public → selected legacy candidates → ruleset resources → protected/canonical → programmatic末端。新beatmap-local public source不存在。
- Required/Recommended不能Suppress；Optional还必须有runtime capability。非法Suppress和invalid产生稳定诊断后进入确定fallback；null、缺entry、异常、`Drawable.Empty()`都不是状态。

## BMS/mania兼容

- BMS legacy候选固定：5K `[Bms]→Keys6→Keys5`；7K `[Bms]→Keys8→Keys7`；9K `[Bms]→Keys9`且不重复；14K `[Bms]→Keys16→同一Keys8两个deck→Keys14`。
- 9K legacy raw `0..8`与public canonical `1..9`只经`bms-gameplay-skin-nine-key-index.v1`映射，未知版本fail-closed。Mirror/Random只改变对象最终LaneId；resource、keysound与skin lookup跟随同一LaneId，不改变topology。
- C5真实capability已扩展为逐slot runtime profile：BMS 28项均有production route（9K按catalog applicability为26格），Mania 23项Supported，`object.mine`、`playfield.turntable`、`playfield.laser`、`bga.viewport`、`bga.frame`为明确NotApplicable；catalog、applicability与runtime decision仍分层。

## publication与诊断

- 所有codec parse、catalog validate、resource/scene prepare、graph build与resolved material构造止于background prepare。update thread只commit一个已完成的package+layout+material+scene引用；exact source只接受current contract，`CompatibilityEmpty`仅供显式detached compatibility host。
- prepare前后与commit锁内复核participant generation、current selection、exact source/content/package/layout/material/scene revision及catalog/codec/resolver/scene/event version。失败保exact A；late attach只取得已提交quadruple与lease；最后detach exactly-once retire。
- `GameplaySkinLayoutRevisionOwner.PreparePublication(..., CancellationToken)`是production carrier ownership门：carrier一旦取得fresh work lease与publication retirement，随后可见的取消必须先Dispose。BMS/mania caller都要使用该入口，并在`using (prepared)`内、`TryCommit`前再查token；否则solver最后检查后的取消会泄漏BMS borrow/work lease，或让mania提交已取消material。
- product diagnostic的去重、确定排序与完整persistence-safe payload在immutable material set构造时预生成；成功commit后的observer只捕获immutable字符串与轻量receipt，不捕获material、snapshot、package、texture或lease。文本只含public code、catalog ID、stable target/index、source kind与合同版本；绝对路径、作者值、display name、record ID/hash和exception text禁止持久化。queue、listener或observer故障不得改变commit或延长旧material生命周期。

## beatmap-local与foundation

- C4明确不新增beatmap-local gameplay-skin authoring：没有sidecar、producer/importer、`WorkingBeatmap` public document/revision authoring ownership或C1/C2 revision闭环；public source kind/candidate必须保持不可达。真实importer/manager的`WorkingBeatmap.Skin`仍惰性返回同一只读`LegacyBeatmapSkin`实例，只保留direct visual compatibility且不消费public section。
- BMS candidate/resource/capability已接production；被resolved material取代的lane-colour/bucket snapshots已删除。event cursor归C5、capability negotiator归C6；`Create(BmsLaneLayout,...)`、raw requirement resolver overload与`PublishForTesting`只是isolation/compat seam，均不计C4产品进度。

## C5 scene/event 接线召回

- C5 的 `GameplaySkinSceneCodec` 是 manifest/scene 唯一 parser；固定 `gameplay-skin.json` / `gameplay-skin.scene.json` 与 v1 contract，严格 UTF-8、duplicate/unknown/path/type/index/target/resource/canonical 校验及硬预算均在 background prepare完成。prepared graph、compiled animation/state/binding program、资源与初始 event state 是 immutable，renderer 不重读 source 或二次 resolver。
- `GameplaySkinEventStream`/`GameplaySkinEventStreamCursor` 已进入真实 BMS/mania/core caller。envelope 带 epoch、连续 sequence、gameplay/layout/material/scene revision、gameplay time、LaneId/GroupId 与 immutable payload；bounded stream 以 Snapshot/Reset支持 late attach、retry、seek、rewind与旧epoch隔离。engine 可发 `GameplayResumed`，scene ABI 不接受 `gameplay.resume`，因 Snapshot 可重建 Running。
- C5 的 exact publication 是 package+layout+material+scene；slot/scene 故障只能回退自身。通用 capability negotiator、sandbox/script 与最终整包 reload 仍归 C6，canonical 双包/Authoring Kit归C7。
