---
name: reference_gameplay_skin_config_presence
description: Skin V1 configuration bucket/scalar/indexed-array/global/per-column-colour/native-BMS-colour/geometry/bucket-global/NoteBodyStyle accepted presence、semantic mapping、legacy mania synthetic default 与 decoder authority 地雷
metadata:
  node_type: memory
  type: reference
---

# gameplay skin config presence 召回

权威状态与硬约束见 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md) / [CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)；本文件只保存实现地雷。

## bucket presence 稳定合同

- `GameplaySkinConfigurationDeclaration<T>` 的 `default` 是 `Absent`；显式 `false`、`0`、空字符串与显式空 bucket 都是 `Declared`。declaration 只记录来源事实，不等于 slot `Provide`、有效配置或 `Suppress`，也不是 manifest/serialisation ABI。
- carrier 不 clone/freeze 任意 `T`；ruleset adapter 因此只返回 immutable bucket key marker（mania `int`、BMS `BmsKeymode`），不得让 mutable native configuration 越过 neutral 边界。
- `ToString()` 只返回 `Absent`/`Declared`，不能输出 payload、资源名或路径。

## decoder authority 地雷

- `LegacySkin.lookupForMania()` 在缺少目标 `[Mania] Keys:` bucket 时会合成 `LegacyManiaSkinConfiguration`。neutral adapter若从production lookup读取，就会把未声明的默认对象误判为`Provide`并遮住`oms-simple`；presence必须继续只来自decoder-time accepted sidecar。
- mania presence 只能来自 `LegacyManiaSkinDecoder.Decode()` 实际输出：没有 `Keys:` 就是 `Absent`，只有 `Keys:` 的空 bucket 仍是 `Declared`。当前 topology 允许 1–2 个各 1–10 列的 mixed stage，因此这里只能按总列范围 1–20 校验；`AvailableVariants` 的等分 dual 列表不能用来拒绝 11/13/15/17/19 这类内部可表达总列数。
- BMS presence 只能来自 `BmsSkinDecoder.Configurations`；`[General] Keymodes:` 是 metadata，不创建 bucket。只有有效 `[Bms] Keymode:` 才 `Declared`，9K BMS/PMS 必须分离；bucket 内 malformed field 被跳过不等于 bucket 本身缺失。
- duplicate section 的 merge/ignore 语义仍归现有 decoder；adapter 只拒绝不可能由真实 decoder 产生的同 target duplicate 输入，不重写 tokenizer 或 compatibility chain。

## legacy mania primitive scalar accepted snapshot

- primitive scalar snapshot 只覆盖九项：`WidthForNoteHeightScale`、`HitPosition`、`LightPosition`、`ComboPosition`、`ScorePosition`、`BarlineHeight`、`JudgementLine`、`KeysUnderNotes`、`LightFramePerSecond`。decoder 在既有 parse/conversion/clamp/scale/bool/FPS normalisation 成功赋值后，把 presence 与当时 accepted value 一起写入 internal sidecar；factory 只读 sidecar，不读之后可变的 native public field。
- 缺 bucket 是 outer `Absent`；显式空 bucket 是 outer `Declared`、九个 inner declaration 均 `Absent`；显式 `0`/`false`/默认数值仍为 `Declared`。malformed numeric 不声明；当前 parser 接受的 `NaN`/`Infinity` 保持 declared-but-unvalidated，不能在 carrier 偷偷清洗。
- sidecar 是 process-local decoder provenance，不是 authority/security boundary。它解决 decode 后 native mutation 漂移与 synthetic default 反推，但不做 finite/range/layout validation，也不是完整 neutral configuration、manifest 或 serialisation ABI。
- 颜色/resource 字典、`NoteBodyStyle` 与 native `[Bms]` exact colour 各有独立 snapshot；production version-derived default 仍故意留在 `NoteBodyStyle` source snapshot 之外。
- 不要顺手修或冻结 `flushPendingLines()` 异常前不清空坏行、malformed `Keys` 可能沿用旧 current config、duplicate `Keys` 后续字段写入 discarded config 等既有坏行为；shared codec/malformed diagnostics 应另立决议。可以锁“不污染 accepted bucket”，不能把阻塞行为写成长期 V1 合同。

## legacy mania NoteBodyStyle accepted snapshot

- 该 snapshot 只覆盖 exact、区分大小写的 `NoteBodyStyle` key。decoder 继续使用原有 `Enum.TryParse<LegacyNoteBodyStyle>()`：命名值、未命名数值、`+2`/`02` 等非 canonical 数值及逗号组合值都按 parser 结果保留；大小写错误、空值或其它 malformed 值不声明，也不覆盖上一个 accepted 值。不得在 provenance 层用 `Enum.IsDefined()` 清洗这些兼容值。
- 缺 bucket 是 outer `Absent`；显式 bucket 即使无该 key 也是 outer `Declared` + inner `Absent`。pending-before-`Keys:`、malformed `Keys` 沿用 prior current bucket 和 duplicate bucket 写入 discarded configuration 继续沿用 decoder 现状，未被提升为 shared codec 长期语义。
- decoder 成功 parse 后同时更新 public compatibility field 与 private accepted sidecar；factory 只读 sidecar。手工构造 configuration 或 decode 后对 public `NoteBodyStyle` 的 erase/alter 都不能伪造、擦除或改变 provenance。
- 此 snapshot 不是 production effective style：`LegacySkin` 在 declaration 缺失时仍按 `[General] Version < 2.5` 推导 `Stretch`，否则推导 `RepeatBottom`；source-specific factory 禁止查询或复制该默认。
- 真实 validation/materialization 仍受 package authority 边界约束；native BMS 普通短键与长条头/身/尾只有 package-scoped read/帧序列/预算/owner 窄路径。Realm/hash-backed store、zip与external directory的完整containment/case/duplicate统一语义、共享codec、其它资源和G1原子reload是否完成只看P1-A STATUS，不得从本snapshot推断。

## native BMS exact colour accepted snapshot

- 该 snapshot 只覆盖 `BmsSkinConfigurationLookups` 中已有 production consumer 的 22 项 colour：4 项 note colour group、3 项 lane background、2 项 divider、6 项 hit-target、2 项 barline、3 项 lane-cover，以及 backdrop/baseplate。catalog/snapshot/factory 都是 BMS assembly 内部 source-specific process-local carrier，不是 neutral slot、作者 manifest/wire ABI，也没有新增主题色或 renderer default。
- 只有 ordinal、区分大小写的 exact source key 在 RGB/RGBA byte parse 成功后进入 private accepted sidecar。既有 `Enum.TryParse` 可把逗号组合名折叠到某个 defined enum value；这种 composite alias 仍只更新 public compatibility `Colours` 字典，不能升格为 exact provenance。valid exact duplicate 取 last accepted，malformed 不声明、也不抹除上一个 accepted 值。
- decoder-time `acceptedColours` 与 public `Colours` 分离；decode 后对 public dictionary 的 overwrite/remove/clear/late-add，以及手工构造 configuration 后填表，都不能伪造、擦除或改写 factory 结果。factory/snapshot 组合拒绝 invalid keymode、null/duplicate bucket、unknown/non-colour field、`Absent` stored entry 与 duplicate entry，snapshot 另做一次防御性复制；安全 `ToString()` 不展开 field、keymode 或颜色。
- 这仍只是 accepted provenance：除 `LongNoteBodyWidth` 的下游字段级 resolver 外，geometry 尚缺 finite/正值/range/屏内/不重叠 validation 与唯一 resolved layout snapshot；除 native BMS 普通短键与长条头/身/尾窄路径外，真实 resource validation/materialization 仍需 package-scoped authority/containment、共享资源名/animation codec、解码预算与 concrete owner/thread-affinity。

## native BMS exact geometry accepted snapshot

- 该 snapshot 只覆盖 `BmsSkinConfigurationLookups` 中已有 production consumer 的 12 项 geometry：`PlayfieldWidth`、`PlayfieldHeight`、`NormalLaneWidth`、`ScratchLaneWidth`、`NormalLaneSpacing`、`ScratchLaneSpacing`、`HitTargetHeight`、`HitTargetBarHeight`、`HitTargetLineHeight`、`HitTargetGlowRadius`、`BarLineHeight`、`LongNoteBodyWidth`。catalog/snapshot/factory 都是 BMS assembly 内部 source-specific process-local carrier，不是 neutral layout descriptor、validation schema、author manifest/wire ABI 或 production resolved layout。
- 只有 `trySplit` trim 后 ordinal、区分大小写的 exact source key，在既有 `float.TryParse(NumberStyles.Float, InvariantCulture)` 成功后进入 private accepted sidecar。保留 parser 实际接受的符号/小数/指数写法、`-0` 的 sign bit、大小写 `NaN`、正负 `Infinity`；.NET 8 overflow 得到正负无穷，underflow 得到保留符号的正负零，这些都是 declared-but-unvalidated，不得在 provenance 层清洗。空值、thousands comma、hex、underscore、type suffix、坏 exponent、Unicode infinity 与非 ASCII 数字不声明；valid duplicate 取 last accepted，malformed 不声明也不抹除上一成功值。
- 既有稠密 enum 与默认区分大小写的 `Enum.TryParse` 会把部分逗号 composite source key 按 bitwise value 折叠到某个 `Enum.IsDefined` geometry field；这些别名继续改写 public mutable `Geometry` compatibility view，但不进入 exact sidecar/snapshot。纯 numeric enum key 虽可被 `Enum.TryParse` 表达，仍会被 decoder 的首字符 `char.IsLetter` gate 拒绝。
- decoder-time `acceptedGeometry` 与 public `Geometry` 分离；decode 后对 public dictionary 的 overwrite/remove/clear/late-add、composite overwrite，以及手工构造 configuration 后填表，都不能伪造、擦除或改写 factory 结果。factory/snapshot 组合拒绝 invalid keymode、null/duplicate bucket、unknown/non-geometry field、`Absent` stored entry 与 duplicate entry，snapshot 防御性复制；安全 `ToString()` 不展开 field、keymode 或数值。
- 这仍只是 accepted provenance：它本身不提供 finite/正值/range/屏内/不重叠 validation、唯一 neutral descriptor/solver 或完整 production materializer。当前唯一窄下游例外是 `LongNoteBodyWidth`：可复用 scalar resolver 只接受 finite 且 `0 < width <= 1`，并用稳定 typed reason 区分 `DeclarationAbsent`、`NonFinite`、`AtOrBelowMinimum`、`AboveMaximum`，逐字段回到 `0.5775`。body 素材帧与该 resolved value 必须由同一 source-bound package revision 发布，renderer 不得重读可变 aggregate geometry；这个例外不验证其余 11 个字段，也不代表 shared codec、screen-space/cross-field solver 或 `SV1-3` layout snapshot 已完成。

## legacy mania indexed-array accepted snapshot

- 该 snapshot 覆盖五组数组：`ColumnLineWidth` 长 `Keys+1`、`ColumnSpacing` 长 `Keys-1`，`ColumnWidth`/`ExplosionWidth`/`HoldNoteLightWidth` 各长 `Keys`。每个 source index 都是独立 declaration；field-level bool 会丢失短数组尾部 presence，禁止使用。
- `ColumnLineWidth` 不缩放，其余四组保存 decoder `×1.6` 后的 converted compatibility value。不要提前派生 `ColumnSpacing / 2` 左右 spacing 或 width 相对 `DEFAULT_COLUMN_SIZE` 的 explosion/light scale；这些归后续 adapter/layout/materializer。
- 短数组未出现尾项为 `Absent`；空、invalid、trailing-empty item 按现有 `TryParse` fallback 接受为 `Declared(0)`；超长尾在容量处忽略；重复短数组逐 index last accepted，未覆盖尾部保留先前 declaration。负值/`NaN`/`Infinity` 仍是 declared-but-unvalidated。
- `Keys=1` 的 cardinality 是 `2/0/1/1/1`；任何 spacing token 因零容量被忽略。`ColumnLineWidth` 是 boundary、`ColumnSpacing` 是 gap，source index 不得冒充 stable lane ID。
- sidecar accessor 与 snapshot 各防御性复制，native array 或 accessor copy 的后续 mutation 均不漂移 provenance；power-of-two field + exact switch 拒绝 unknown/composite，field/index validation 在任何 view 写入前完成。这里的单操作异常安全不表示线程同步保证。

## legacy mania known global colour accepted snapshot

- 该 snapshot 只覆盖现有 production lookup 已消费的四个 exact global key：`ColourColumnLine`、`ColourJudgementLine`、`ColourBreak`、`ColourBarline`。不能把 decoder 的 `StartsWith("Colour")` 任意 key 接受行为公开成 dictionary/string ABI；用户自造 key 可能包含不应进入诊断或文档的数据。
- capture 必须位于既有 `HandleColours(..., allowAlpha: true)` 成功返回之后；直接在 factory 复制公开可变 `CustomColours` 会让 decode 后 mutation 伪造来源事实。RGB accepted value alpha=255，RGBA/alpha 0 原样保存；valid duplicate last accepted，malformed 不创建或覆盖 sidecar。
- snapshot 保存 parser `Color4`，不是 renderer 最终色。不得提前调用 doubled-alpha/zero-alpha compatibility、默认回落或额外 range/视觉验证；任意扩展 colour 仍不属于 closed accepted surface。
- malformed colour 会让既有 `flushPendingLines()` 在 clear 前退出并阻断同 section 后续；fixture 只锁“不声明/不覆盖”，不能把阻断行为提升为 V1 codec 合同，也不能在 provenance 切片顺手修 parser。

## legacy mania per-column colour accepted snapshot

- 该 snapshot 只接受 exact canonical、区分大小写的 `Colour{n}` 与 `ColourLight{n}`，其中 `{n}` 是 1-based ASCII 十进制且必须落在当前 `Keys` 范围。`Colour0`、前导零、符号、后缀、大小写变体、越界 index 与其它 `Colour*` key 都不能进入 closed sidecar。
- capture 必须位于既有 `HandleColours(..., allowAlpha: true)` 成功返回之后；decoder-time sidecar 独立保存 column background/light 的 accepted declaration 与 parser `Color4`。RGB 补 alpha=255，RGBA（含 alpha 0）原样保存；valid duplicate last accepted，malformed 不声明也不覆盖，decode 后对 public `CustomColours` 的 add/replace/remove/clear 都不能伪造或擦除 provenance。
- neutral snapshot 的 closed field 是 `LaneBackground`（`playfield.lane.background-colour`）与 `LaneLight`（`playfield.lane.light-colour`）。source column index 不是 stable lane ID；factory 必须接收明确的 source-column→`GameplaySkinLaneId` 映射并绑定 exact topology，复制 mapping 后按 logical lane、field 固定排序。partial mapping 与 many-to-one source mapping 都允许，但 target duplicate、越界 source 或 topology 外 lane 必须拒绝。
- mania 映射使用 `GlobalLogicalIndex`，dual-stage 不重启 source index。BMS full visual 使用 `GlobalVisualIndex`；14K eight-column deck 使用 `GroupLocalVisualIndex`，因此两个 deck 可共享同一组 source index；key-only projection 按 non-scratch visual enumeration 编号。三类 BMS projection 当前是相互独立的 fixture-only snapshot；若未来把它们合入同一 candidate plan，必须共享同一个 exact topology reference，不能只凭值相等的 topology 或各自重建的 projection 混装。
- 该 snapshot 只冻结 accepted provenance 与中性映射，不做 doubled-alpha/zero-alpha compatibility、视觉默认、materialization、fallback 或 renderer 接线，也不是 manifest/wire ABI；它本身不构成 Skin V1 已交付能力，Skin V1 完整产品面仍未交付。

## legacy mania bucket-global resource accepted snapshot

- 该 snapshot 只覆盖 exact、区分大小写且已有 production lookup 的 13 项 legacy mania resource key：`LightingN`、`LightingL`、`StageLeft`、`StageRight`、`StageBottom`、`StageLight`、`StageHint`、`Hit0`、`Hit50`、`Hit100`、`Hit200`、`Hit300`、`Hit300g`。任意其它 `Lighting*` / `Stage*` / `Hit*` 仍可留在 compatibility dictionary，但不能进入 closed sidecar；valid duplicate 使用 last accepted，包括后一个显式空值覆盖前值。
- 这 13 项属于 exact `Keys:` bucket 的 global/non-column declaration：`LightingN/L` 虽可被各 lane 的视觉消费，源配置仍只有一个 bucket-global resource name；stage frame/hint 与 judgement 同样没有 stable lane identity。因此 snapshot 只保留 `SourceColumnCount`，不绑定或重建 topology。
- public fixed-property surface 使用 gameplay semantic，而不是把 raw legacy key 提升为跨程序集合同：`LightingN → ExplosionResource`、`LightingL → HoldNoteLightResource`、`StageLeft/Right/Bottom → LeftStageResource/RightStageResource/BottomStageResource`、`StageLight → KeyFlashResource`、`StageHint → HitTargetResource`、`Hit0/50/100/200/300/300g → MissJudgementResource/MehJudgementResource/OkJudgementResource/GoodJudgementResource/GreatJudgementResource/PerfectJudgementResource`。public snapshot 不提供 raw string-key query；raw key 仍留在 decoder-internal classifier 与 compatibility dictionary。
- `SplitKeyVal()` 只按首个冒号切分并 trim key/value；`StageHint:` 因此是 `Declared(string.Empty)`，引号与其余冒号仍属于 `SplitKeyVal`-trimmed、尚未 `CleanFilename`/validation 的 compatibility string。此处不做扩展名、文件存在性、绝对/越界路径、containment、解码预算或 materialization validation；source-provided resource name/path 不得进入安全字符串或持久诊断。
- decoder 在接受 exact key 时同时更新 compatibility `ImageLookups` 与 private declaration sidecar；factory 只读 sidecar。decode 后、factory 前或 snapshot 后对 public dictionary 的 add/overwrite/remove/clear/整体替换均不能伪造、擦除或改变 accepted provenance；手工构造 configuration 再填 dictionary 也不能制造 declaration。
- resource declaration 只证明来源事实，不等于文件有效、slot `Provide`、`Suppress` 或 fallback winner。显式空字符串仍必须进入后续 materializer/diagnostic，而不能在 provenance 层折叠成 `Absent`/`Inherit`；该 snapshot 本身不证明 production lookup、renderer、`SkinManager` 或 fallback 已接线。

lane-resource的immutable snapshot、有序BMS→mania candidate plan、逐字段resolution/revision-owner、decoder-time accepted sidecar与mutable-window地雷统一见[lane-resource compatibility](reference_gameplay_skin_lane_resource_compatibility.md)。本文件只保存configuration presence的反直觉parser/provenance合同；完整neutral configuration、validation/materialization/shared codec与生产接线状态以P1-A为准。
