# Gameplay Skin V1 公共目录

本文是 `GameplaySkinSlotCatalog` 的可读投影，不是第二份 ID 表。下表由 `GameplaySkinSlotCatalogDocumentation.GenerateMarkdownTable()` 生成，并由固定 contract digest 与文档一致性测试锁定；修改目录必须同时变更代码版本、生成块和测试。

公共目录冻结稳定名称、scope、值类型、Required/Recommended/Optional、继承、是否允许 `Suppress` 以及 ruleset/keymode/stage/lane-role 适用性。它**不等于当前 renderer 已实现全部 slot**：真实可达能力由独立的 `GameplaySkinRuntimeCapabilitySet` 声明，runtime capability 不能改变目录语义，也不能为 Required/Recommended slot 扩张 `Suppress` 权限。

当前合同版本：catalog `oms-gameplay-skin-catalog.v1`、codec `oms-gameplay-skin-codec.v1`、resolver `oms-gameplay-skin-resolver.v1`。BMS 扩展只有 `[GameplaySkin.Bms:1]`，与 common 共用同一个 tokenizer/codec，不存在第二套 BMS parser。

<!-- GAMEPLAY-SKIN-CATALOG:BEGIN -->
| ID | Stable name | Catalog | Scope | Type | Class | Default | Suppress | Rulesets | Stage | Lane role | Keymode | Keys | Diagnostic |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `playfield.lane-surface` | `LaneSurface` | `Common:1` | Lane | Resource | Required | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-001` |
| `playfield.judgement-line` | `JudgementLine` | `Common:1` | Stage | Resource | Required | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-002` |
| `object.note` | `Note` | `Common:1` | Lane | Resource | Required | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-003` |
| `object.long-note.head` | `LongNoteHead` | `Common:1` | Lane | Resource | Required | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-004` |
| `object.long-note.body` | `LongNoteBody` | `Common:1` | Lane | Resource | Required | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-005` |
| `object.mine` | `Mine` | `Common:1` | Lane | Resource | Required | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-006` |
| `playfield.lane-cover.fill` | `LaneCoverFill` | `Common:1` | Stage | Resource | Required | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-007` |
| `object.long-note.tail` | `LongNoteTail` | `Common:1` | Lane | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-008` |
| `playfield.key` | `KeyVisual` | `Common:1` | Lane | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-009` |
| `effect.key-flash` | `KeyFlash` | `Common:1` | Lane | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-010` |
| `effect.hit-explosion` | `HitExplosion` | `Common:1` | Lane | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-011` |
| `hud.judgement` | `JudgementDisplay` | `Common:1` | Stage | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-012` |
| `hud.combo` | `ComboDisplay` | `Common:1` | Stage | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-013` |
| `hud.gauge` | `GaugeVisual` | `Common:1` | Stage | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-014` |
| `hud.text` | `TextHud` | `Common:1` | Global/Stage | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-015` |
| `playfield.bar-line` | `BarLine` | `Common:1` | Group | Resource | Recommended | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-016` |
| `stage.background` | `StageBackground` | `Common:1` | Stage | Resource | Recommended | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-017` |
| `stage.foreground` | `StageForeground` | `Common:1` | Stage | Resource | Recommended | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-018` |
| `playfield.backdrop` | `PlayfieldBackdrop` | `Common:1` | Stage | Resource | Recommended | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-019` |
| `playfield.baseplate` | `PlayfieldBaseplate` | `Common:1` | Stage | Resource | Recommended | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-020` |
| `playfield.lane-cover.decoration` | `LaneCoverDecoration` | `Common:1` | Stage | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-021` |
| `playfield.turntable` | `Turntable` | `Bms:1` | Lane | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Bms | Single/Dual | Scratch | Bms5K/Bms7K/Bms14K | 5-14 | `OMS-SKIN-SLOT-022` |
| `playfield.laser` | `Laser` | `Bms:1` | Lane | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Bms | Single/Dual | Scratch | Bms5K/Bms7K/Bms14K | 5-14 | `OMS-SKIN-SLOT-023` |
| `bga.viewport` | `BgaViewport` | `Common:1` | Global | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-024` |
| `bga.frame` | `BgaFrame` | `Common:1` | Global | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-025` |
| `decoration` | `Decoration` | `Common:1` | Global/Stage/Group/Lane | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-026` |
| `playfield.hit-target` | `HitTarget` | `Common:1` | Lane | Resource | Recommended | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-027` |
| `playfield.lane-divider` | `LaneDivider` | `Common:1` | Lane | Resource | Recommended | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-028` |
<!-- GAMEPLAY-SKIN-CATALOG:END -->

## 作者格式

公共声明写在包内同一个 `skin.ini`：

```ini
[GameplaySkin.Common:1]
Target: Lane ruleset=bms keymode=5k stage-mode=single group=bms.group.deck-1 lane=bms.lane.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=0 group-local-visual=0
object.note: resource Provide "notes/key-1"
object.long-note.tail: resource Suppress
```

- section、字段、类型和操作均区分大小写；合法 section 只有 `GameplaySkin.Common:1` 与 `GameplaySkin.Bms:1`。
- section header 必须是没有内部首尾空白、没有尾随字符的完整 `[...]`；版本与全部 index 只接受 canonical ASCII 十进制（`0` 或非零开头），拒绝正号、前导零、全角数字和溢出。文件开头至多允许一个 UTF-8 BOM，embedded/double BOM 是 document-fatal 诊断。
- `#` 与 `;` 在引号外开始注释；资源值必须使用双引号。支持 `\\`、`\"`、`\n`、`\r`、`\t`，其它转义是稳定错误。
- `Provide "..."`、`Inherit`、`Suppress` 是显式状态。未出现、已声明空字符串、非法声明、有效声明和合法 `Suppress` 各自保留，不能互相静默折叠。
- 同一 catalog ID + exact target 重复声明是错误；未知字段、未知 ID、错误 family、未知版本、错误 scope/type/index、selector 与 exact publication stable ID/index 漂移都会产生 `OMS-SKIN-CODEC-NNN` 诊断。字段错误只沿该 slot 的确定 fallback 继续，不把 invalid 当作 absent。
- `Encode(Decode(x))` 输出规范化 UTF-8 token stream（换行归一、去除行尾空白），再次 decode 必须保留所有语义状态、section、legacy token 与诊断。

### Target

每个 `Target` 都必须显式写出 `ruleset`、`keymode` 与 `stage-mode` 三个 selector；省略、重复、未知属性或错误大小写均为 invalid。`Target` 只能为下列四种完整形式：

- `Global ruleset=<selector> keymode=<selector> stage-mode=<selector>`
- `Stage ruleset=<selector> keymode=<selector> stage-mode=<selector> group=<GroupId> group-logical=<n> group-visual=<n>`
- `Group ruleset=<selector> keymode=<selector> stage-mode=<selector> group=<GroupId> group-logical=<n> group-visual=<n>`
- `Lane ruleset=<selector> keymode=<selector> stage-mode=<selector> group=<GroupId> lane=<LaneId> group-logical=<n> group-visual=<n> global-logical=<n> global-visual=<n> group-local-logical=<n> group-local-visual=<n>`

`ruleset` 为 `any` / `mania` / `bms`，`stage-mode` 为 `any` / `single` / `dual`。`keymode=any` 可跨 keymode；BMS exact token 为 `5k`、`7k`、`9k-bms`、`9k-pms`、`14k`，mania single token 为 `<n>k`，dual-stage vector 为 `<left>k-<right>k`，且总 lane 数必须与 C3 topology 一致。portable 文档可同时声明多个 selector；不属于当前 publication 的 selector 不产生 runtime 故障，也不能命中当前 material。

同一 package 内的 winner 顺序固定为：exact ruleset 优先于 `any`，再比较 exact keymode、exact stage-mode、scope（`Lane > Group > Stage > Global`），最后才是同 specificity 的后行。最高 specificity 声明会遮蔽同 package 的更宽声明：显式 `Inherit`、empty 或 invalid 都转向下一 authority，不回头拼接本 package 的较宽声明。这保证 invalid 不冒充 absent，也防止一个 package 内发生隐式聚合。

BMS group ID 为 `bms.group.deck-1` / `bms.group.deck-2`，lane ID 为 `bms.lane.scratch-1`、`bms.lane.key-1` … `bms.lane.key-14`、`bms.lane.scratch-2`。mania group ID 为 `mania.group.stage-1` / `mania.group.stage-2`，lane ID 为全局顺序的 `mania.lane.column-1` …。ID 与所有 logical/visual/global/group-local index 必须同时匹配 C3 exact topology；resolver 不从 enum ordinal、lane count、几何、`RelativeStart` 或 drawable 次序反推。P1/P2、居中右 scratch、dual-stage 等视觉顺序不同的目标须按其 explicit index 分别声明。

9K 的公开 canonical lane index 为 `1..9`；legacy `[Mania] Keys:9` 的 raw index `0..8` 只经 `bms-gameplay-skin-nine-key-index.v1` 双向映射进入 compatibility candidate，未知版本 fail-closed。Mirror/Random 只改变对象最终目标 `LaneId`；resource、keysound 与 skin lookup 随同该 `LaneId`，不会改变 topology 或借 drawable 次序重算 lane。

## 解析与 precedence

每个 package 的 `skin.ini` bytes 只捕获、hash、tokenize 一次。legacy `[Mania]` / `[Bms]` adapter 与公共声明共享该不可变 token stream；consumer 不得重开文件或二次 tokenize。进入 gameplay 前，document 会绑定 exact source ID、configuration content revision、package/current generation 和 layout revision；完整 package bytes revision仍由同一 `GameplaySkinPackageRevision` 持有。

现有只读 legacy beatmap visual compatibility 高于 selected package，但它不是 C4 beatmap-local 作者格式，也绝不读取或绑定 beatmap skin 的公共 section。其后按 selected public document、selected legacy ruleset bucket、ruleset resources、受保护/canonical 层、程序化末端 fallback 解析；实际不存在的层会被省略，不改变相对顺序。BMS legacy keymode candidate 固定为：

- 5K：`[Bms]` → `Keys:6` → `Keys:5`
- 7K：`[Bms]` → `Keys:8` → `Keys:7`
- 9K：`[Bms]` → `Keys:9`，不得重复候选
- 14K：`[Bms]` → `Keys:16` → 同一个 `Keys:8` 分别投影两个 deck → `Keys:14`

resolver 对每个已支持 target 产出一个最终 immutable `Provide` 或 `Suppress` entry；`Inherit` 只用于继续 provider chain，不以缺 dictionary entry、`null`、异常或 `Drawable.Empty` 表示。Required/Recommended 的非法 `Suppress` 会产生稳定诊断并继续到确定 fallback；只有目录标记为 Optional + Allowed 且 runtime capability 同时支持时才可终止为 `Suppress`。

## 当前 C4 runtime 边界

C4 以 BMS Note/LN 与 mania Note/Hold/KeyVisual 的真实渲染纵切证明公共 codec/catalog/resolver；它没有提前实现 C5 的 scene/event 或全部 optional slot host。每个 ruleset 的 capability set 是唯一运行时清单，未支持的公共声明会产生 capability diagnostic，不会静默解释成 `Inherit`。BMS 的 `KeyVisual` 与 HUD/BGA/gauge/combo视觉 slot 仍由 C5 scene host 承接；这些对象在 C4 只携带同一 exact layout/material revision，不得宣传成已消费对应 slot。BMS legacy Note/LN 的静态图和固定 60 FPS `name-0`、`name-1`…连续帧合同保持不变。

prepared material、layout adapter 与 neutral snapshot 是同一个 publication 引用。所有可失败 parse、validate、resource decode 与 material 构造都在 background prepare 完成；update thread 只交换已经完成的 package + layout + material 引用。任一步失败保留 exact A；成功后 late attach 只能取得已提交 revision 与 lease，旧 owner 在最后 lease detach 后 exactly-once retire。Settings → Skin 的 `Reload current skin` 仍是唯一手动 reload，live gameplay/preview 在任何 source prepare 前拒绝；没有 watcher、same-value reload 或行级 reload。

## Beatmap-local 终态

C4 **不纳入新的 beatmap-local gameplay-skin authoring**。OMS 没有定义 sidecar 名称、可写 producer/importer、capture/archive/reload 或 `WorkingBeatmap` public document/revision authoring ownership；公共 catalog 与 `GameplaySkinDocumentSourceKind` 也没有 BeatmapLocal authority。真实 importer/manager 仍让 `WorkingBeatmap.Skin` 惰性返回同一只读 `LegacyBeatmapSkin` 实例，只承载既有 direct visual compatibility。原因是新的 sidecar 若要安全成立，必须同时冻结 `chartbms/` / `chartmania/` 直读生命周期、路径 containment、携带/复制语义、working beatmap revision、C1 capture/archive 与 C2 same-ID publication，这不是可诚实附加在 C4 Note/LN 纵切上的半功能。

既有 lazer `LegacyBeatmapSkin` 的只读直接视觉兼容继续存在，并保持高于 selected package 的历史 precedence；它只作为预准备的 `LegacyBeatmapCompatibility` material source，不消费公共 author section。因此本结论不是删除既有谱面皮肤，也不是把测试注入路径宣传成作者格式。若未来重开，必须以独立产品 gate 一次性交付安全格式、真实 producer/importer、revision ownership、两规则集 consumer 与迁移测试；当前替代流程是把 gameplay 声明和素材放入 ordinary `.osk`、managed `chartskin/<包>/` 或已注册只读 external package，再通过唯一 manual Reload 发布。

## 诊断与隐私

codec、resolver 与 material diagnostic 使用稳定 code、catalog ID、target stable ID/index、source kind 以及 exact revision 关联。任意绝对路径、作者资源值、display name、record GUID、content hash 和异常正文都不得进入持久化文本或 `ToString()`；精确 source/content identity 只保留在进程内对象用于相等性与 revision correlation。

只有 exact package+layout+material publication 成功提交后，产品 diagnostic sink 才异步输出该 revision 的去重、确定排序、安全摘要；失败或失去commit admission的候选不会留下“已生效”日志。日志只含公开诊断码、catalog ID、stable target/index、source kind与catalog/codec/resolver版本，不阻塞update thread，也不让日志故障改变已提交引用。
