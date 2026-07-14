---
name: reference_gameplay_skin_config_presence
description: Skin V1 configuration bucket/scalar/indexed-array/global/per-column-colour accepted presence、stable-lane colour mapping、legacy mania synthetic default、ImageLookups 可变窗口与 decoder authority 地雷
metadata:
  node_type: memory
  type: reference
---

# gameplay skin config presence 召回

权威状态与硬约束见 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md) / [CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)；本文件只保存实现地雷。

## bucket presence 稳定合同

- `GameplaySkinConfigurationDeclaration<T>` 的 `default` 是 `Absent`；显式 `false`、`0`、空字符串与显式空 bucket 都是 `Declared`。declaration 只记录来源事实，不等于 slot `Provide`、有效配置或 `Suppress`，也不是 manifest/serialisation ABI。
- carrier 不 clone/freeze 任意 `T`；第五切的 ruleset adapter 因此只返回 immutable bucket key marker（mania `int`、BMS `BmsKeymode`），不得让 mutable native configuration 越过 neutral 边界。
- `ToString()` 只返回 `Absent`/`Declared`，不能输出 payload、资源名或路径。

## decoder authority 地雷

- `LegacySkin.lookupForMania()` 在缺少目标 `[Mania] Keys:` bucket 时会合成 `LegacyManiaSkinConfiguration`。未来 neutral adapter 若从 production lookup 读取，就会把未声明的默认对象误判为 `Provide` 并遮住 `oms-simple`。
- mania presence 只能来自 `LegacyManiaSkinDecoder.Decode()` 实际输出：没有 `Keys:` 就是 `Absent`，只有 `Keys:` 的空 bucket 仍是 `Declared`。当前 topology 允许 1–2 个各 1–10 列的 mixed stage，因此这里只能按总列范围 1–20 校验；`AvailableVariants` 的等分 dual 列表不能用来拒绝 11/13/15/17/19 这类内部可表达总列数。
- BMS presence 只能来自 `BmsSkinDecoder.Configurations`；`[General] Keymodes:` 是 metadata，不创建 bucket。只有有效 `[Bms] Keymode:` 才 `Declared`，9K BMS/PMS 必须分离；bucket 内 malformed field 被跳过不等于 bucket 本身缺失。
- duplicate section 的 merge/ignore 语义仍归现有 decoder；adapter 只拒绝不可能由真实 decoder 产生的同 target duplicate 输入，不重写 tokenizer 或 compatibility chain。

## legacy mania primitive scalar accepted snapshot

- 第十一切只覆盖九个 primitive scalar：`WidthForNoteHeightScale`、`HitPosition`、`LightPosition`、`ComboPosition`、`ScorePosition`、`BarlineHeight`、`JudgementLine`、`KeysUnderNotes`、`LightFramePerSecond`。decoder 在既有 parse/conversion/clamp/scale/bool/FPS normalisation 成功赋值后，把 presence 与当时 accepted value 一起写入 internal sidecar；factory 只读 sidecar，不读之后可变的 native public field。
- 缺 bucket 是 outer `Absent`；显式空 bucket 是 outer `Declared`、九个 inner declaration 均 `Absent`；显式 `0`/`false`/默认数值仍为 `Declared`。malformed numeric 不声明；当前 parser 接受的 `NaN`/`Infinity` 保持 declared-but-unvalidated，不能在 carrier 偷偷清洗。
- sidecar 是 process-local decoder provenance，不是 authority/security boundary。它解决 decode 后 native mutation 漂移与 synthetic default 反推，但不做 finite/range/layout validation，也不是完整 neutral configuration、manifest 或 serialisation ABI。
- 第十一切未覆盖颜色/resource 字典、`NoteBodyStyle` 与 `[General] Version`；后续只能按更小 closed surface 分切。
- 不要顺手修或冻结 `flushPendingLines()` 异常前不清空坏行、malformed `Keys` 可能沿用旧 current config、duplicate `Keys` 后续字段写入 discarded config 等既有坏行为；shared codec/malformed diagnostics 应另立决议。可以锁“不污染 accepted bucket”，不能把阻塞行为写成长期 V1 合同。

## legacy mania indexed-array accepted snapshot

- 第十二切覆盖五组数组：`ColumnLineWidth` 长 `Keys+1`、`ColumnSpacing` 长 `Keys-1`，`ColumnWidth`/`ExplosionWidth`/`HoldNoteLightWidth` 各长 `Keys`。每个 source index 都是独立 declaration；field-level bool 会丢失短数组尾部 presence，禁止使用。
- `ColumnLineWidth` 不缩放，其余四组保存 decoder `×1.6` 后的 converted compatibility value。不要提前派生 `ColumnSpacing / 2` 左右 spacing 或 width 相对 `DEFAULT_COLUMN_SIZE` 的 explosion/light scale；这些归后续 adapter/layout/materializer。
- 短数组未出现尾项为 `Absent`；空、invalid、trailing-empty item 按现有 `TryParse` fallback 接受为 `Declared(0)`；超长尾在容量处忽略；重复短数组逐 index last accepted，未覆盖尾部保留先前 declaration。负值/`NaN`/`Infinity` 仍是 declared-but-unvalidated。
- `Keys=1` 的 cardinality 是 `2/0/1/1/1`；任何 spacing token 因零容量被忽略。`ColumnLineWidth` 是 boundary、`ColumnSpacing` 是 gap，source index 不得冒充 stable lane ID。
- sidecar accessor 与 snapshot 各防御性复制，native array 或 accessor copy 的后续 mutation 均不漂移 provenance；power-of-two field + exact switch 拒绝 unknown/composite，field/index validation 在任何 view 写入前完成。这里的单操作异常安全不表示线程同步保证。

## legacy mania known global colour accepted snapshot

- 第十三切只覆盖现有 production lookup 已消费的四个 exact global key：`ColourColumnLine`、`ColourJudgementLine`、`ColourBreak`、`ColourBarline`。不能把 decoder 的 `StartsWith("Colour")` 任意 key 接受行为公开成 dictionary/string ABI；用户自造 key 可能包含不应进入诊断或文档的数据。
- capture 必须位于既有 `HandleColours(..., allowAlpha: true)` 成功返回之后；直接在 factory 复制公开可变 `CustomColours` 会让 decode 后 mutation 伪造来源事实。RGB accepted value alpha=255，RGBA/alpha 0 原样保存；valid duplicate last accepted，malformed 不创建或覆盖 sidecar。
- snapshot 保存 parser `Color4`，不是 renderer 最终色。不得提前调用 doubled-alpha/zero-alpha compatibility、默认回落或额外 range/视觉验证；任意扩展 colour 仍不属于 closed accepted surface。
- malformed colour 会让既有 `flushPendingLines()` 在 clear 前退出并阻断同 section 后续；fixture 只锁“不声明/不覆盖”，不能把阻断行为提升为 V1 codec 合同，也不能在 provenance 切片顺手修 parser。

## legacy mania per-column colour accepted snapshot

- 第十五切只接受 exact canonical、区分大小写的 `Colour{n}` 与 `ColourLight{n}`，其中 `{n}` 是 1-based ASCII 十进制且必须落在当前 `Keys` 范围。`Colour0`、前导零、符号、后缀、大小写变体、越界 index 与其它 `Colour*` key 都不能进入 closed sidecar。
- capture 必须位于既有 `HandleColours(..., allowAlpha: true)` 成功返回之后；decoder-time sidecar 独立保存 column background/light 的 accepted declaration 与 parser `Color4`。RGB 补 alpha=255，RGBA（含 alpha 0）原样保存；valid duplicate last accepted，malformed 不声明也不覆盖，decode 后对 public `CustomColours` 的 add/replace/remove/clear 都不能伪造或擦除 provenance。
- neutral snapshot 的 closed field 是 `LaneBackground`（`playfield.lane.background-colour`）与 `LaneLight`（`playfield.lane.light-colour`）。source column index 不是 stable lane ID；factory 必须接收明确的 source-column→`GameplaySkinLaneId` 映射并绑定 exact topology，复制 mapping 后按 logical lane、field 固定排序。partial mapping 与 many-to-one source mapping 都允许，但 target duplicate、越界 source 或 topology 外 lane 必须拒绝。
- mania 映射使用 `GlobalLogicalIndex`，dual-stage 不重启 source index。BMS full visual 使用 `GlobalVisualIndex`；14K eight-column deck 使用 `GroupLocalVisualIndex`，因此两个 deck 可共享同一组 source index；key-only projection 按 non-scratch visual enumeration 编号。三类 BMS projection 当前是相互独立的 fixture-only snapshot；若未来把它们合入同一 candidate plan，必须共享同一个 exact topology reference，不能只凭值相等的 topology 或各自重建的 projection 混装。
- 该 snapshot 只冻结 accepted provenance 与中性映射，不做 doubled-alpha/zero-alpha compatibility、视觉默认、materialization、fallback 或 renderer 接线，也不是 manifest/wire ABI。Skin V1 因此仍不可用。

第八切已在 bucket-level provenance 之上增加 note、LN head/body/tail、key up/down 六个 lane-resource 字段的 immutable snapshot 与有序 BMS→mania candidate plan，第九切又冻结其 process-local 逐字段 resolution/revision-owner 合同；细节见 [lane-resource compatibility](reference_gameplay_skin_lane_resource_compatibility.md)。第十一至十五切新增上述九个 legacy mania primitive scalar、五组 indexed array、四项 known global colour 与两类 per-column colour accepted snapshot；其余扩展 colour、global resource presence、`NoteBodyStyle`、malformed declaration 结构化诊断、完整 neutral configuration、真实文件 validation/materialization/shared codec 与生产接线仍未完成。

## lane-resource provenance 尚未闭合

- `LegacyManiaGameplaySkinLaneResourceSnapshotFactory` 当前在 factory 调用时直接读取 public mutable `LegacyManiaSkinConfiguration.ImageLookups`。既有 immutable fixture 只证明 snapshot 创建后不随 native mutation 漂移；decode 后、factory 前的新增/替换/删除仍可伪造或擦除 declaration/value。
- 因此现有 lane-resource snapshot 只能称 compatibility projection，不是完整 decoder-accepted provenance，更不是 security boundary。不得把“来自 decoder output”误写成“只能由 decoder 成功接受的行产生”。
- 下一切是 13 项 exact known global resource：`LightingN`、`LightingL`、`StageLeft`、`StageRight`、`StageBottom`、`StageLight`、`StageHint`、`Hit0`、`Hit50`、`Hit100`、`Hit200`、`Hit300`、`Hit300g` 的 decoder-time sidecar；随后加固现有 note、LN head/body/tail、key up/down 六类 lane resource 的 `ImageLookups` provenance。两者都必须在 decoder 接受行时写入 immutable/defensively copied sidecar，factory 只读 sidecar；不得从 `ImageLookups` 或其它 public mutable dictionary 反推完整 V1 config。
