---
name: reference_gameplay_skin_codec_material
description: C4 public catalog/shared codec/三态resolver、exact material publication、diagnostic与beatmap-local终态地雷
metadata:
  node_type: memory
  type: reference
---

# gameplay skin codec/material召回

权威状态与硬约束只看[P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)、[PLAN](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md)与[CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)；公共作者合同见[Gameplay Skin V1目录](../../doc_md/other/GAMEPLAY_SKIN_PUBLIC_CATALOG_V1.md)，完成边界见[C4交接](../../doc_md/other/SKIN_SYSTEM_C4_CODEC_MATERIAL_COMPLETION_HANDOFF_20260831.md)。本页只保存实现地雷。

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
- C4真实capability是BMS Note/LN与mania Note/Hold/KeyVisual。其余public optional slot只允许产生capability diagnostic并由C5 host承接；catalog存在不等于renderer存在。

## publication与诊断

- 所有codec parse、catalog validate、resource prepare和resolved material构造止于background prepare。update thread只commit一个已完成的package+layout+material引用；exact source只接受current contract，`CompatibilityEmpty`仅供显式detached compatibility host。
- prepare前后与commit锁内复核participant generation、current selection、exact source/content/package/layout revision及catalog/codec/resolver version。失败保exact A；late attach只取得已提交triple与lease；最后detach exactly-once retire。
- `GameplaySkinLayoutRevisionOwner.PreparePublication(..., CancellationToken)`是production carrier ownership门：carrier一旦取得fresh work lease与publication retirement，随后可见的取消必须先Dispose。BMS/mania caller都要使用该入口，并在`using (prepared)`内、`TryCommit`前再查token；否则solver最后检查后的取消会泄漏BMS borrow/work lease，或让mania提交已取消material。
- product diagnostic的去重、确定排序与完整persistence-safe payload在immutable material set构造时预生成；成功commit后的observer只捕获immutable字符串与轻量receipt，不捕获material、snapshot、package、texture或lease。文本只含public code、catalog ID、stable target/index、source kind与合同版本；绝对路径、作者值、display name、record ID/hash和exception text禁止持久化。queue、listener或observer故障不得改变commit或延长旧material生命周期。

## beatmap-local与foundation

- C4明确不新增beatmap-local gameplay-skin authoring：没有sidecar、producer/importer、`WorkingBeatmap` public document/revision authoring ownership或C1/C2 revision闭环；public source kind/candidate必须保持不可达。真实importer/manager的`WorkingBeatmap.Skin`仍惰性返回同一只读`LegacyBeatmapSkin`实例，只保留direct visual compatibility且不消费public section。
- BMS candidate/resource/capability已接production；被resolved material取代的lane-colour/bucket snapshots已删除。event cursor归C5、capability negotiator归C6；`Create(BmsLaneLayout,...)`、raw requirement resolver overload与`PublishForTesting`只是isolation/compat seam，均不计C4产品进度。
