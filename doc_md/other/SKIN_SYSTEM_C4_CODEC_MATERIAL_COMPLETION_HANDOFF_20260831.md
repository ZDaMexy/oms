# Skin System C4 Codec / Material 完成交接（始建2026-08-31；最终闭门2026-09-02）

本文记录P1-A Skin V1七个持久campaign中`C4`的闭门边界。当前authority仍是[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)、[PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md)与[TECHNICAL_CONSTRAINTS](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)；本文只用于防止后续重做、放宽或误宣传。C4完成后燃尽为`4/7 closed，C5 active`，不换算线性百分比。

## 1. 玩家与作者结果

- ordinary Realm`.osk`、managed`chartskin/<包>/`与registered external三源中的public Common/BMS声明都由同一个codec/catalog/resolver解析，并随exact current revision进入真实ruleset provider。
- BMS Note/LN与mania Note/Hold/KeyVisual从同一个immutable resolved material set取得最终`Provide`/`Suppress`结果；renderer不在commit后重读ini、再次合并来源或重新决定fallback。
- 任何parse、catalog validation、resource prepare或material构造失败都发生在background prepare；update thread只提交已完成的package+layout+material引用。失败保持exact A画面，成功后late attach只取得已提交revision与lease。
- 成功publication的author diagnostic按真实分层使用稳定code：catalog/codec分别为`OMS-SKIN-SLOT-NNN`与`OMS-SKIN-CODEC-NNN`，resolver/resource/capability为稳定小写token（如`bms.material.decode-failed`、`bms.capability.unsupported-slot`）。它们异步、去重、确定排序地输出，且不含绝对路径、作者值、display name、record ID/hash或exception text。
- C4没有把public catalog中的C5+ slot冒充renderer能力：BMS真实capability只覆盖Note/LN，mania覆盖Note/Hold/KeyVisual；其它声明产生明确capability diagnostic，等待C5 scene/event host。

## 2. 唯一public catalog

- `GameplaySkinSlotCatalog`冻结Common v1与唯一BMS v1 extension共28项stable ID；完整人读投影见[Gameplay Skin V1公共目录](GAMEPLAY_SKIN_PUBLIC_CATALOG_V1.md)。catalog digest为`28f282d31eeb9097fa8184729b72f7b59d9635bab11c0dd459648325ec65b96d`。
- 每项明确scope、value/resource type、Required/Recommended/Optional、默认/继承语义、Suppress资格、ruleset/keymode/stage/lane-role适用性与稳定diagnostic code。
- catalog是codec、applicability validator、resolver、BMS/mania consumer与文档生成/一致性测试的单一ID authority；ruleset、renderer与fixture不得复制第二张ID表。
- runtime capability与catalog requirement/suppress eligibility分层。renderer当前支持不能改变public ABI；Required/Recommended永不因某ruleset缺host而变成可Suppress。

## 3. 唯一shared codec

- `Skin`只捕获一次exact`skin.ini` bytes并构造防御性immutable`GameplaySkinDocument`；Common/BMS public section与legacy`[Mania]`/`[Bms]` adapter只消费同一token stream，不重开文件或复制tokenizer。
- 合法public section只有`[GameplaySkin.Common:1]`与`[GameplaySkin.Bms:1]`。codec区分Absent、DeclaredEmpty、Invalid、Valid与Suppress，并保留legacy token、exact source/configuration-content/package/current identity。
- whitespace、大小写、comment、escaping、unknown version/field、illegal scope/type/index/selector、duplicate与canonical encode/decode round-trip均有稳定合同。malformed first declaration仍占用exact target，后行产生Duplicate，不能借首行tokenization失败夺取winner。
- document-fatal与field-local failure分层；invalid不会静默变absent，unknown/unsupported不会静默变inherit。diagnostic保留进程内exact identity用于revision correlation，持久文本只输出脱敏字段。

## 4. 显式三态与precedence

- `Provide`、`Inherit`、`Suppress`是显式数据模型；null、缺dictionary entry、异常或`Drawable.Empty()`均不表示状态。
- package内winner specificity固定为exact ruleset → keymode → stage-mode → scope；Lane > Group > Stage > Global。同specificity按后行，最高specificity的Inherit/empty/invalid会转到下一authority，不回头借本package较宽声明。
- authority固定为既有legacy beatmap direct visual compatibility → selected public document → selected legacy ruleset candidates → ruleset resources → protected/canonical → programmatic末端。实际不存在的层可以省略，但顺序不可改。
- Required/Recommended的Suppress会稳定诊断并继续确定fallback；只有catalog Optional + Allowed且runtime capability支持时才终止为Suppress。resolver输出一个immutable material set，consumer不得各自重跑链。

## 5. Stable target、BMS/mania compatibility

- public target显式携带ruleset、keymode、stage-mode、scope、C3 stable LaneId/GroupId及全部适用logical/visual/global/group-local index；每项必须匹配exact topology。不得从enum ordinal、lane count、geometry、`RelativeStart`或drawable顺序反推。
- BMS候选顺序进入production material resolver：5K`[Bms]→Keys6→Keys5`；7K`[Bms]→Keys8→Keys7`；9K`[Bms]→Keys9`且不重复；14K`[Bms]→Keys16→同一Keys8投影两个deck→Keys14`。
- 9K legacy raw`0..8`与public canonical`1..9`只经`bms-gameplay-skin-nine-key-index.v1`双向映射；未知版本fail-closed。
- mania single/dual stage vector保持stage-local special-key语义；所有索引显式。Mirror/Random/S-Random只改变对象post-mod目标LaneId，resource、keysound与skin lookup跟随同一LaneId，不改变固定topology。
- BMS legacy Note/LN静态图与固定60 FPS连续编号帧合同无损保留；mania legacy资源也经shared document adapter进入最终material precedence。

## 6. C2+C3+C4 publication

- exact root只允许`GameplaySkinLayoutRevisionOwner`发布一次`GameplaySkinLayoutPublication`；publication同时绑定neutral snapshot、typed adapter与`GameplaySkinResolvedMaterialSet`。exact source只接受current catalog/codec/resolver contract，`CompatibilityEmpty`仅允许显式detached compatibility owner。
- prepare前后及commit锁内复核participant generation、current selection、exact source/content/package/layout revision及catalog/codec/resolver version；fresh attach强制barrier重试，commit前detach使carrier失效。
- cancellation一路传入ruleset layout/material preparer并在resource loops中检查；shared cancellation-aware owner在carrier取得fresh work lease/publication retirement后负责失败Dispose，BMS与mania caller还必须在carrier `using`内、`TryCommit`前复核token。即使取消落在solver最后检查与carrier返回之间，也不得泄漏lease/borrow或提交已取消publication；cancel、supersede、scheduler fault与shutdown不得发布partial material或在update thread二次prepare。
- BMS selected Note/LN的exact preparation不是由material entry各自拥有；`BmsLegacySkin`按exact layout generation签发ref-counted、幂等borrow，material preparer将完成borrow整体转给publication。publication验证/prepare失败、取消、dispatcher拒绝或commit admission失败均exactly-once释放provisional borrow，不能留下隐藏的TextureStore owner。
- diagnostic的去重、确定排序与完整persistence-safe payload在immutable material set构造时预生成；commit后observer closure只捕获immutable字符串与轻量receipt，不捕获material、snapshot、package、texture或lease。queue/listener/observer失败全部隔离，既不能改变成功commit，也不能延长旧owner material生命周期。
- 成功commit把prepared-resource retirement转交唯一`GameplaySkinLayoutRevisionOwner`。`RulesetSkinProvidingContainer`必须先通过base生命周期完成renderer子树detach/dispose，再释放layout owner/borrow；`BmsLegacySkin.Dispose`会封门、标记generation退役并取消/join work，但active borrow归零前不得清理其prepared revision。old revision仍在最后consumer/work/operation lease detach后于update thread exactly-once retire；late attach、dynamic attach/detach与跨revision holder只使用已提交triple。
- Settings → Skin的`Reload current skin`仍是唯一manual reload；live gameplay/preview在任何source prepare前拒绝。没有watcher、same-value reload、行级reload或live reload。

## 7. Beatmap-local终态

C4明确不新增beatmap-local gameplay-skin authoring。仓库没有安全的sidecar命名、producer/importer、`WorkingBeatmap` public document/revision authoring ownership、C1 capture/archive或C2 same-ID publication闭环；因此public source kind、catalog precedence与production candidate都不保留半可达BeatmapLocal入口。真实BMS importer→manager→`WorkingBeatmap.Skin`仍惰性返回同一只读`LegacyBeatmapSkin`实例，仅证明既有direct visual compatibility生命周期继续存在。

既有lazer`LegacyBeatmapSkin`只读直接视觉兼容继续存在并保持较高precedence。它作为`Skin`可能由shared codec构造immutable document，但resolver/production author path明确拒绝把其public section作为author authority或绑定到resolved material。作者当前替代流程是ordinary`.osk`、managed`chartskin/<包>/`或registered external包，再用唯一manual Reload发布。若未来重开，必须以独立产品gate一次性交付路径安全、携带/复制、producer/importer、WorkingBeatmap revision、三源capture/reload、双ruleset consumer、诊断与迁移测试。

## 8. 真实production证明

- BMS三源参数化产品测试从真实`SkinManager` current revision进入5K`BmsRuleset` prepare与exact publication，最终由actual`BmsAsyncNoteDrawable`消费selected-document public Note素材；验证prepared texture identity、同一material set及未进入legacy/programmatic fallback。
- mania三源参数化产品测试从真实`SkinManager` current revision进入`RulesetSkinProvidingContainer`和mania prepare，actual`DrawableNote`、`DrawableHoldNote`、head/body/tail与`Column`消费五个selected-common public Provide；验证真实prepared texture、同一material set及无default fallback。
- BMS playfield、Note/LN、pre-start、BGA viewport、HUD/gauge/combo与mania playfield/stage/column/note/hold/core provider均携带同一exact publication/material reference；非C4视觉host只携带revision，不冒充已消费其public slot。
- 三源invalid declaration各只在成功commit后输出一批脱敏diagnostic；失败candidate、foreign same-identity owner、取消与lost admission不产生已生效material或日志。

## 9. Foundation/caller审计

| 资产 | C4结论 |
| --- | --- |
| BMS configuration candidate、lane-resource provenance/provider、9K mapping、runtime capability | 已接`BmsGameplayResolvedNoteMaterialPreparer` production路径 |
| `GameplaySkinLaneColourSnapshot`、legacy mania bucket scalar/array/known-global-colour/known-global-resource/NoteBodyStyle与BMS bucket colour/geometry/declaration factories | 已由catalog/exact layout/resolved material取代并删除，连同fixture删除；production仍使用的lane-resource provenance snapshot不在此列 |
| `GameplaySkinEventStreamCursor` | 明确留给C5 scene/event，不计C4 |
| capability negotiator/authorization foundation | 明确留给C6 sandbox，不计C4 |
| `BmsGameplaySkinConfigurationCandidateFactory.Create(BmsLaneLayout,...)` | 仅C3 isolation fixture seam；production只用`CreateExact(layout,...)`，不计C4 |
| `GameplaySkinSlotResolver` raw requirement overload | legacy/test compatibility seam；public production只用catalog descriptor overload，不计C4 |
| `BmsGameplayLayoutProvider.PublishForTesting`与CompatibilityEmpty | isolated layout/visual fixture seam；禁止进入exact production，不计C4 |

## 10. 验证与终审

最终验证数字以[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)的2026-09-02 C4闭门快照为准。闭门最低证明包括：core catalog/codec/resolver/revision、BMS/mania三源public product、5K/7K/9K/14K与single/dual、P1-K decode/converter/projection/真实keysound、C3 geometry/aspect/DPI/safe-area、三源A→B/失败保A/动态attach/detach/live reject/late attach/holder/retire、core/mania/BMS full与Release。core Skin六项与mania full四项既有失败必须按同名精确基线比对，不能遮蔽新回归。

四类独立终审结论均为GO、blocker/major **0/0**：public catalog/codec/三态authority；全production consumer与reachable bypass；revision participant/owner/concurrency；产品价值与dead foundation。后续C5不得重开C1～C4，也不得以manifest/DTO、event cursor、单一scene host或catalog总数代替全部advertised optional slot的真实production纵切。

## 11. C5准入

C5只处理versioned declarative scene/animation/event runtime、剩余optional slot production host及其资源/节点/effect/每帧预算。每个scene host必须同切加入现有package+layout+material publication、participant lease/detach/retire协议；seek/retry/reload必须由Snapshot/Reset确定重建。C6 sandbox、C7 canonical双包/Authoring Kit、P1-L BGA内容/timeline/seek、在线服务、watcher与live gameplay reload仍明确排除。
