# P1-K 开发计划：BMS 解析链路治理

> 最后更新：2026-06-01（订正 K9 #12 autoplay 谓词：`canParticipateInAutoplay` 改 nested-aware、恢复长条 autoplay，验证清单补长条用例；此前新增 K11 切片：BMS→mania 转谱 BGM/autoplay 音频补全与 LN 尾键音对齐）
> 主线总规划见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。本文件只拆解 `P1-K` 的执行顺序；存储/导入一致性见 [../P1-H/DEVELOPMENT_PLAN.md](../P1-H/DEVELOPMENT_PLAN.md)，runtime 热路径与播放期性能见 [../P1-J/DEVELOPMENT_PLAN.md](../P1-J/DEVELOPMENT_PLAN.md)，真实谱面验校见 [../P1-E/DEVELOPMENT_PLAN.md](../P1-E/DEVELOPMENT_PLAN.md)。

## 子线定位

| 维度 | 归属 | 说明 |
| --- | --- | --- |
| 主归属 | `P1-K` | 收口 BMS 解析模型、转换语义、调用复用与解析侧缓存边界 |
| 协作子线 | `P1-H` | `P1-K` 定义 parse truth，`P1-H` 负责 storage / importer / reuse / persisted metadata 的消费与持久化 |
| 协作子线 | `P1-J` | `P1-K` 提供 normalized snapshot 与 projection contract，`P1-J` 只消费它们做 runtime hot-path 与音频时序治理 |
| 协作子线 | `P1-E` | `P1-E` 用真实谱面验证 `P1-K` 语义是否足够，不另起第二套 parse model |
| 协作子线 | `P1-C` | 只有当新增 parse event 直接驱动 feedback / judgement family 时，`P1-C` 才记录从属影响 |

## 归线结论

- 该专题**不并入 `P1-H`**。`P1-H` 的 authority 在存储拓扑、导入/重扫、persisted metadata 与 read-model 一致性；它不拥有 decoder、converter 与 parse-side projection 语义。
- 该专题**不并入 `P1-J`**。`P1-J` 负责 gameplay runtime hot path、shared audio pool 与 dense-chart 播放期性能；它消费 parse 结果，但不拥有 parse truth。
- 该专题**不并入 `P1-E`**。`P1-E` 的职责是用真实谱面做 gameplay 与长条语义验校，而不是倒过来定义解析模型。
- 因此 `P1-K` 必须作为独立子线成立，专门拥有 parse-chain correctness、projection reuse 与 parse-side cache 这组 authority。

## 当前确认基线

- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs) 当前负责文本解码、header 解析、channel line 解析、raw event 提取与一部分 typed event 建模。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs) 当前已持有 `BeatmapInfo`、`RawChannelEvents`、`ChannelEvents`、`ObjectEvents`、`LongNoteEvents`、`ScrollEvents`、`BgaEvents`、`InvisibleObjectEvents`、`MineEvents`、`BpmChangeEvents`、`StopEvents` 与 `Warnings`；raw snapshot + normalized chart model 的双层 authority 已具雏形，但 ownership 仍需继续收口。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs) 已具备 WAV/BMP/BPM/STOP tables、静态背景字段、`LNOBJ/LNTYPE`、keymode、`ScrollTable`、`UnknownHeaders`、`BgaDefinitions`、`AtBgaDefinitions`、`ArgbDefinitions`、`SwBgaDefinitions`、`PoorBgaMode`，以及 `GetVisualDefinitionProjections()` / `TryGetVisualDefinitionProjection()` 与 `GetPreferredBackgroundAssetReference()`；其中 static background path 已开始实际消费 richer visual-definition family，并会先把两位 bitmap reference 解析回 `BitmapTable`。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs) 已负责时间轴、control points 与 hitobject conversion；本轮 `K3-A/K3-B/K3-C/K3-D/K3-E/K3-F` 已把同拍位 `BPM -> STOP -> object` 顺序、signed BPM 的 magnitude timeline 消费、`LNTYPE 2` 的最小 hold-note conversion proof、第一批 visual typed slot、`SCROLLxx/SC` 的 typed consumer contract、richer BGA-definition headers 的 typed surface，以及 unified visual-definition projection contract 冻结在 converter/decode authority 内，而 `K4-A/K4-B/K4-C/K4-D/K4-E/K4-F/K4-G/K4-H/K4-I/K4-J/K4-K/K4-L/K4-M/K4-N/K4-O/K4-P/K4-Q/K4-R/K4-S` 也已让 metadata/background、Song Select note-distribution、beatmap-statistics、core-side metadata read-model、Song Select artist/creator selector、`BeatmapAttributeText`、`BeatmapMetadataDisplay`、`ExpandedPanelMiddleContent`、profile metadata consumer、menu metadata consumer、online play creator consumer、matchmaking round-results score consumer、beatmap skin metadata consumer、beatmap title display consumer、scoped beatmap-set title display consumer、daily challenge title display consumer、delete confirmation title display consumer 与 set-level artist display consumer 开始复用同一 projection authority。本轮又把 `IBeatmapInfo.GetDisplayTitle()`、`IBeatmapSetInfo.GetDisplayTitle()`、`ModelExtensions.GetDisplayString()` 与剩余 set-level artist / beatmap-title consumer 统一收口到同一 helper，`K4` 数字层级现已整体完成。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs) 与 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs) 已证明 projection reuse 的价值：imported raw wrapper 可以直接复用首次 conversion 结果，原始 chart 文件也继续保留在文件系统存储中。
- 当前 parse chain 仍保留了一部分 forward-compatible 缓解面：大多数十六进制轨道还能通过 raw `ChannelEvents` 回收，原始 chart 文件没有丢；但这不能替代 decode model 自身的保数能力。
- 当前仍待收口的 parse gap 已不再是 projection reuse consumer 本身。本轮 `K5` 已把 `ICachedModlessPlayableBeatmapSource`、`WorkingBeatmap.GetPlayableBeatmap()`、`BmsDecodedBeatmap` 与 `BmsImportedBeatmapFactory` 收口为 source-bound 的 modless playable projection cache contract：无 mods 的 BMS playable projection 现按 source beatmap identity 只 materialize 一次，换 source 或带 mods 则重新转换，loader 首次 conversion 也会把现成 projection seed 回同一 cache boundary；本轮 `K6` 又把 results/statistics 邻接面收口到 `Ruleset` 的“已带 mods playable beatmap”合同，不再让 results/gauge helper 在内部再次应用 beatmap mods。`LNTYPE 2` MGQ 最小表达已于 `K3-B` 落地，BGA / invisible / mine 的第一批 typed slot 已于 `K3-C` 落地，`SCROLLxx/SC` 的 typed consumer contract 已于 `K3-D` 落地，`#BGA`、`#@BGA`、`#ARGB`、`#SWBGA` 与 `#POORBGA` 的 header-side typed surface 已于 `K3-E` 落地，unified visual-definition projection contract 已于 `K3-F` 落地，static background 的首个 projection consumer 已于 `K4-A` 落地，Song Select note distribution graph 已于 `K4-B` 开始优先复用 projected working beatmap，beatmap statistics 已于 `K4-C` 开始优先复用 metadata 中的 `ChartFilterStats`，`osu.Game` 侧 metadata display / star-rating read-model 已于 `K4-D` 开始优先复用统一的 persisted `chart_metadata` projection，Song Select author sort/group/filter 已于 `K4-E` 开始优先复用 display creator fallback，Song Select artist sort/group/filter 与 `BeatmapAttributeText` 已于 `K4-F` 开始优先复用 display artist / creator fallback，`BeatmapMetadataDisplay` 已于 `K4-G` 开始优先复用同一 gameplay metadata display fallback，`ExpandedPanelMiddleContent` 已于 `K4-H` 开始优先复用同一 results metadata display fallback，`DrawableProfileScore` 与 `DrawableMostPlayedBeatmap` 已于 `K4-I` 开始优先复用同一 profile metadata display artist fallback，`SongTicker` 与 `NowPlayingOverlay` 已于 `K4-J` 开始优先复用同一 menu metadata display artist fallback，`DailyChallengeIntro` 已于 `K4-K` 开始优先复用同一 daily challenge creator fallback，`DrawableRoomPlaylistItem` 已于 `K4-L` 开始优先复用同一 online playlist creator fallback 并保留 linked-profile 分支，`SubScreenRoundResults` 已于 `K4-M` 开始优先复用同一 round-results beatmap metadata authority，在本地谱面存在时不再重建 API 最小壳，`LegacyBeatmapSkin` 已于 `K4-N` 开始优先复用同一 beatmap skin creator fallback，`IBeatmapInfo.GetDisplayTitleRomanisable()` 已于 `K4-O` 开始优先复用同一 title display authority，`FilterControl.ScopedBeatmapSetDisplay` 已于 `K4-P` 开始在有具体 beatmap 时优先复用同一 full-beatmap title authority，`DailyChallengeIntro` 已于 `K4-Q` 开始在 title line 有具体 beatmap 时优先复用同一 full-beatmap title authority，并显式禁用 difficulty name，`BeatmapDeleteDialog` 已于 `K4-R` 开始通过 shared set-level title authority 复用首个 beatmap 的 title contract，并继续保持不展示 creator suffix，而 `PanelBeatmapSet` 与 `PlaylistItem` 已于 `K4-S` 开始通过 `BeatmapSetInfoExtensions.GetDisplayArtistRomanisable()` 复用同一 set-level artist authority；本轮又让 plain title chain、display string 与剩余 set-level artist / beatmap-title consumer 全部并入这条 authority，随后 `K7` 与 `K8` 又分别把 results summary consumer proof 与 gauge history consumer proof 收口到 plain focused 路径。因此 `K4`、`K5`、`K6`、`K7` 与 `K8` 都已整体收口，post-`K8` follow-up 现后置为 backlog，不再作为当前进行中项。
- 本子线的外部语义参考基线固定为 [hitkey BMS 命令参考](https://hitkey.bms.ms/cmds.htm) 与 [bmson specification](https://bmson-spec.readthedocs.io/)，其结构化归纳与解析审查对照清单见 [../../other/BMS_FORMAT_REFERENCE.md](../../other/BMS_FORMAT_REFERENCE.md)。若实现与当前文档冲突，必须先更新其一再继续开发。
- post-`K8` backlog 的首个正式切片现冻结为 `K9`：BMS -> mania 单向转谱合同。该切片当前已进入实现并完成 dedicated converter、public gate、sample-only scratch 运行时、persisted converted star 与 spread display current-ruleset read-model；`P1-K` 继续拥有 source keymode -> mania keycount、lane flatten、scratch-family sample-only 语义、converted-star persistence 与 conversion validity 的 authority，`P1-A` 只承接后续公开表面、入口文案与 Song Select/presentation gating。

## 首轮执行包

### 文件级切片图

| 切片 | 目的 | 主文件 | 相邻文件 | 首轮测试落点 | 当前退出条件 |
| --- | --- | --- | --- | --- | --- |
| `K1-A` raw carrier | 建立 raw snapshot 与 source-order carrier | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsChannelEvent.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsChannelEvent.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs) | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs) | [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs) | raw snapshot additive 落地，现有 converter/importer 无需跟改 |
| `K1-B` unknown bag / scroll placeholder | 让 `SCROLLxx/SC`、unknown header/definition 进入模型 | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs) | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs) | [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs) | `SCROLLxx/SC` 与 unknown bag 可回收，但 consumer 仍可暂不消费 |
| `K2-A` signed BPM / duplicate line | 保留 negative BPM 与 duplicate channel compound 语义 | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBpmChangeEvent.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBpmChangeEvent.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs) | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsChannelEvent.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsChannelEvent.cs) | [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs) | signed BPM 进入 typed model，duplicate line 有 source-order-aware compound 行为 |
| `K3-A` timeline semantics | 冻结同拍位 `BPM/STOP/object` 顺序与 signed BPM 时间推进合同 | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs) | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsStopEvent.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsStopEvent.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsLongNoteEvent.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsLongNoteEvent.cs) | [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapConverterTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapConverterTest.cs) | same-position order 与 signed BPM magnitude timeline 稳定；`K3-B` 再承接 LNTYPE 2 minimal expression |
| `K3-B` LNTYPE 2 semantics | 补齐 MGQ long-note 的最小表达与 end-to-end hold-note proof | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsLongNoteEncoding.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsLongNoteEncoding.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsLongNoteEvent.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsLongNoteEvent.cs) | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs) | [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs)、[../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapConverterTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapConverterTest.cs) | LN channel 显式 `00` close marker 可跨小节配对，且最小表达能稳定转成 `BmsHoldNote` |
| `K3-C` visual typed slots | 补齐 BGA / invisible / mine 的最薄 typed surface | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsBgaEvent.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBgaEvent.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsInvisibleObjectEvent.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsInvisibleObjectEvent.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsMineEvent.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsMineEvent.cs) | - | [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs) | BGA base/poor/layer/layer2、invisible object 与 landmine channel 进入 typed collections，consumer 不再需要从 raw carrier 猜 channel |
| `K3-D` scroll consumer contract | 补齐 `SCROLLxx/SC` 的 typed surface 与首个 runtime consumer | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsScrollEvent.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsScrollEvent.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs) | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsChannelEvent.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsChannelEvent.cs) | [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs)、[../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapConverterTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapConverterTest.cs) | `SCROLLxx` 定义 + `SC` channel line 进入 `ScrollEvents`，并在 `ControlPointInfo.EffectPoints` 上形成稳定 scroll-speed consumer contract |
| `K3-E` richer BGA-definition headers | 补齐 `#BGA`、`#@BGA`、`#ARGB`、`#SWBGA`、`#POORBGA` 的 header-side typed surface | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsVisualDefinitions.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsVisualDefinitions.cs) | - | [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs) | richer visual-definition header family 进入 typed collections，并保留 raw bitmap/reference token；consumer/runtime 仍可后置 |
| `K3-F` visual-definition projection | 把分散 header tables 收口为统一的 combined projection contract | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsVisualDefinitions.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsVisualDefinitions.cs) | - | [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs) | richer visual-definition family 可按 index 直接读取统一组合视图，不再需要下游手工重拼四张 definition 表 |
| `K4-A` static-background projection reuse | 让 converter metadata / import / background layer 共享同一 background asset projection | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs) | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs)、[../../osu.Game.Rulesets.Bms/UI/BmsBackgroundLayer.cs](../../osu.Game.Rulesets.Bms/UI/BmsBackgroundLayer.cs) | [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapConverterTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapConverterTest.cs)、[../../osu.Game.Rulesets.Bms.Tests/BmsDrawableRulesetTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsDrawableRulesetTest.cs)、[../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs) | static background metadata / import / playfield background layer 共享同一 background asset reference，不再各自只认 `STAGEFILE/BACKBMP/BANNER` |
| `K4-B` note-distribution projection reuse | 让 Song Select note-distribution consumer 优先复用 projected working beatmap | [../../osu.Game.Rulesets.Bms/SongSelect/BmsNoteDistributionGraph.cs](../../osu.Game.Rulesets.Bms/SongSelect/BmsNoteDistributionGraph.cs) | - | [../../osu.Game.Rulesets.Bms.Tests/BmsNoteDistributionGraphTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsNoteDistributionGraphTest.cs) | working beatmap 已携带 `BmsHitObject` projection 时，Song Select 不再无条件触发 playable conversion |
| `K4-C` beatmap-statistics projection reuse | 让 statistics consumer 优先复用 metadata 中的 chart-filter projection | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs) | - | [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapStatisticsTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapStatisticsTest.cs) | beatmap statistics 先读 `BeatmapInfo.Metadata.GetChartFilterStats()`，只在缺失时才现场计算并回写缓存 |
| `K4-D` core persisted-metadata read-model reuse | 让 `osu.Game` 侧 BMS metadata display / star-rating consumer 共享 typed persisted `chart_metadata` projection | [../../osu.Game/Beatmaps/BmsPersistedMetadataResolver.cs](../../osu.Game/Beatmaps/BmsPersistedMetadataResolver.cs) | [../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs](../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs)、[../../osu.Game/Beatmaps/BmsStarRatingResolver.cs](../../osu.Game/Beatmaps/BmsStarRatingResolver.cs) | [../../osu.Game.Tests/Beatmaps/BeatmapLocalMetadataDisplayResolverTest.cs](../../osu.Game.Tests/Beatmaps/BeatmapLocalMetadataDisplayResolverTest.cs)、[../../osu.Game.Tests/Beatmaps/BmsStarRatingResolverTest.cs](../../osu.Game.Tests/Beatmaps/BmsStarRatingResolverTest.cs) | core-side read-model consumer 不再各自手拆 `RulesetDataJson` / `chart_metadata` token |
| `K4-E` song-select creator selector reuse | 让 Song Select 的 author sort/group/filter consumer 共享 BMS display creator fallback | [../../osu.Game/Screens/Select/BeatmapCarouselFilterSorting.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterSorting.cs)、[../../osu.Game/Screens/Select/BeatmapCarouselFilterGrouping.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterGrouping.cs)、[../../osu.Game/Screens/Select/BeatmapCarouselFilterMatching.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterMatching.cs) | [../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs](../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs) | [../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterSortingTest.cs](../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterSortingTest.cs)、[../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterGroupingTest.cs](../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterGroupingTest.cs)、[../../osu.Game.Tests/NonVisual/Filtering/FilterMatchingTest.cs](../../osu.Game.Tests/NonVisual/Filtering/FilterMatchingTest.cs) | Song Select 不再把 legacy BMS 的空 `Author.Username` 当成 author/group/filter 的唯一 authority |
| `K4-F` local-metadata display consumer reuse | 让 Song Select 的 artist sort/group/filter 与 shared beatmap-attribute display consumer 共享 BMS display artist / creator fallback | [../../osu.Game/Screens/Select/BeatmapCarouselFilterSorting.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterSorting.cs)、[../../osu.Game/Screens/Select/BeatmapCarouselFilterGrouping.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterGrouping.cs)、[../../osu.Game/Screens/Select/BeatmapCarouselFilterMatching.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterMatching.cs)、[../../osu.Game/Skinning/Components/BeatmapAttributeText.cs](../../osu.Game/Skinning/Components/BeatmapAttributeText.cs) | [../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs](../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs) | [../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterSortingTest.cs](../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterSortingTest.cs)、[../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterGroupingTest.cs](../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterGroupingTest.cs)、[../../osu.Game.Tests/NonVisual/Filtering/FilterMatchingTest.cs](../../osu.Game.Tests/NonVisual/Filtering/FilterMatchingTest.cs)、[../../osu.Game.Tests/Beatmaps/BeatmapLocalMetadataDisplayResolverTest.cs](../../osu.Game.Tests/Beatmaps/BeatmapLocalMetadataDisplayResolverTest.cs)、[../../osu.Game.Tests/Visual/UserInterface/TestSceneBeatmapAttributeText.cs](../../osu.Game.Tests/Visual/UserInterface/TestSceneBeatmapAttributeText.cs)、[../../osu.Game.Tests/Skins/BeatmapAttributeTextLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Skins/BeatmapAttributeTextLocalMetadataDisplayTest.cs) | local-metadata display consumer 不再直接暴露 raw `Metadata.Artist` / `Metadata.ArtistUnicode` / `Metadata.Author.Username`，且 `BeatmapAttributeText` 已有 plain focused proof，不再依赖 visual scene discover gap |
| `K4-G` gameplay metadata display reuse | 让 gameplay loading surface 的 artist / mapper display 共享 BMS display artist / creator fallback | [../../osu.Game/Screens/Play/BeatmapMetadataDisplay.cs](../../osu.Game/Screens/Play/BeatmapMetadataDisplay.cs) | [../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs](../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs) | [../../osu.Game.Tests/Visual/Gameplay/TestSceneBeatmapMetadataDisplay.cs](../../osu.Game.Tests/Visual/Gameplay/TestSceneBeatmapMetadataDisplay.cs) | gameplay metadata display 不再直接展示 raw `Metadata.Artist` / `Metadata.Author.Username`，且 focused scene 要能稳定验证 display text |
| `K4-H` results metadata display reuse | 让 results screen expanded metadata surface 共享 BMS display artist / creator fallback | [../../osu.Game/Screens/Ranking/Expanded/ExpandedPanelMiddleContent.cs](../../osu.Game/Screens/Ranking/Expanded/ExpandedPanelMiddleContent.cs) | [../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs](../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs) | [../../osu.Game.Tests/Scores/ExpandedPanelMiddleContentLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Scores/ExpandedPanelMiddleContentLocalMetadataDisplayTest.cs) | results metadata display 不再直接展示 raw `Metadata.Artist` / `Metadata.Author.Username`，且 focused proof 不依赖 CLI visual scene discoverability |
| `K4-I` profile metadata display artist reuse | 让 profile beatmap metadata surface 共享 BMS display artist fallback | [../../osu.Game/Overlays/Profile/Sections/Ranks/DrawableProfileScore.cs](../../osu.Game/Overlays/Profile/Sections/Ranks/DrawableProfileScore.cs) + [../../osu.Game/Overlays/Profile/Sections/Historical/DrawableMostPlayedBeatmap.cs](../../osu.Game/Overlays/Profile/Sections/Historical/DrawableMostPlayedBeatmap.cs) | [../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs](../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs) | [../../osu.Game.Tests/Online/ProfileBeatmapMetadataLocalDisplayTest.cs](../../osu.Game.Tests/Online/ProfileBeatmapMetadataLocalDisplayTest.cs) | profile metadata display 不再直接展示 raw `Metadata.Artist` / `Metadata.ArtistUnicode`，且 focused proof 不依赖 CLI visual scene discoverability |
| `K4-J` menu metadata display artist reuse | 让 menu / now-playing metadata surface 共享 BMS display artist fallback | [../../osu.Game/Screens/Menu/SongTicker.cs](../../osu.Game/Screens/Menu/SongTicker.cs) + [../../osu.Game/Overlays/NowPlayingOverlay.cs](../../osu.Game/Overlays/NowPlayingOverlay.cs) | [../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs](../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs) | [../../osu.Game.Tests/Menus/MenuBeatmapMetadataLocalDisplayTest.cs](../../osu.Game.Tests/Menus/MenuBeatmapMetadataLocalDisplayTest.cs) | menu metadata display 不再直接展示 raw `Metadata.Artist` / `Metadata.ArtistUnicode`，且 focused proof 不依赖 visual scene 内部断言 |
| `K4-K` daily challenge creator display reuse | 让 daily challenge metadata surface 共享 BMS display creator fallback | [../../osu.Game/Screens/OnlinePlay/DailyChallenge/DailyChallengeIntro.cs](../../osu.Game/Screens/OnlinePlay/DailyChallenge/DailyChallengeIntro.cs) | [../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs](../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs) | [../../osu.Game.Tests/OnlinePlay/DailyChallengeLocalMetadataDisplayTest.cs](../../osu.Game.Tests/OnlinePlay/DailyChallengeLocalMetadataDisplayTest.cs) | daily challenge metadata display 不再直接展示 raw `Metadata.Author.Username`，且 focused proof 不依赖 visual scene 内部断言 |
| `K4-L` online playlist creator display reuse | 让 online playlist item metadata surface 共享 BMS display creator fallback | [../../osu.Game/Screens/OnlinePlay/DrawableRoomPlaylistItem.cs](../../osu.Game/Screens/OnlinePlay/DrawableRoomPlaylistItem.cs) | [../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs](../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs) | [../../osu.Game.Tests/OnlinePlay/DrawableRoomPlaylistItemLocalMetadataDisplayTest.cs](../../osu.Game.Tests/OnlinePlay/DrawableRoomPlaylistItemLocalMetadataDisplayTest.cs) | playlist creator display 不再因缺失 `Metadata.Author.Username` 而隐藏作者行，且在有真实资料时仍保留 user link |
| `K4-M` round-results local beatmap reuse | 让 matchmaking round results 在构造 `ScoreInfo` 时优先复用本地 `BeatmapInfo` | [../../osu.Game/Screens/OnlinePlay/Matchmaking/Match/RoundResults/SubScreenRoundResults.cs](../../osu.Game/Screens/OnlinePlay/Matchmaking/Match/RoundResults/SubScreenRoundResults.cs) | [../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs](../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs) | [../../osu.Game.Tests/OnlinePlay/SubScreenRoundResultsLocalMetadataDisplayTest.cs](../../osu.Game.Tests/OnlinePlay/SubScreenRoundResultsLocalMetadataDisplayTest.cs) | round-results score consumer 在本地谱面存在时不再用 API 最小壳覆盖 persisted chart metadata，且缺失时仍保留 API fallback shell |
| `K4-N` beatmap skin creator display reuse | 让 beatmap skin metadata 的 `SkinInfo.Creator` 共享 BMS display creator fallback | [../../osu.Game/Skinning/LegacyBeatmapSkin.cs](../../osu.Game/Skinning/LegacyBeatmapSkin.cs) | [../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs](../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs) | [../../osu.Game.Tests/Skins/LegacyBeatmapSkinLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Skins/LegacyBeatmapSkinLocalMetadataDisplayTest.cs) | beatmap skin metadata 不再因空 `Metadata.Author.Username` 丢失 legacy BMS creator |
| `K4-O` beatmap title display authority reuse | 让 `IBeatmapInfo.GetDisplayTitleRomanisable()` 共享 BMS display artist / creator fallback | [../../osu.Game/Beatmaps/BeatmapInfoExtensions.cs](../../osu.Game/Beatmaps/BeatmapInfoExtensions.cs) | [../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs](../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs) | [../../osu.Game.Tests/Localisation/BeatmapInfoRomanisationLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Localisation/BeatmapInfoRomanisationLocalMetadataDisplayTest.cs) | full-beatmap title display consumer 不再暴露 embedded creator suffix，且可在空 raw creator 时显示 fallback creator |
| `K4-P` scoped beatmap-set title display reuse | 让 `FilterControl.ScopedBeatmapSetDisplay` 在能拿到具体 beatmap 时复用 full-beatmap title authority | [../../osu.Game/Screens/Select/FilterControl.ScopedBeatmapSetDisplay.cs](../../osu.Game/Screens/Select/FilterControl.ScopedBeatmapSetDisplay.cs) | [../../osu.Game/Beatmaps/BeatmapInfoExtensions.cs](../../osu.Game/Beatmaps/BeatmapInfoExtensions.cs) | [../../osu.Game.Tests/Menus/ScopedBeatmapSetDisplayLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Menus/ScopedBeatmapSetDisplayLocalMetadataDisplayTest.cs) | scoped beatmap set title display 不再暴露 embedded creator suffix，且不会误带 difficulty name |
| `K4-Q` daily challenge title display reuse | 让 `DailyChallengeIntro` 在能拿到具体 beatmap 时复用 full-beatmap title authority | [../../osu.Game/Screens/OnlinePlay/DailyChallenge/DailyChallengeIntro.cs](../../osu.Game/Screens/OnlinePlay/DailyChallenge/DailyChallengeIntro.cs) | [../../osu.Game/Beatmaps/BeatmapInfoExtensions.cs](../../osu.Game/Beatmaps/BeatmapInfoExtensions.cs) | [../../osu.Game.Tests/OnlinePlay/DailyChallengeLocalMetadataDisplayTest.cs](../../osu.Game.Tests/OnlinePlay/DailyChallengeLocalMetadataDisplayTest.cs) | daily challenge title display 不再直接读 `beatmap.BeatmapSet!.Metadata.GetDisplayTitleRomanisable(false)`，且不会重新带回 difficulty name |
| `K4-R` delete confirmation title display reuse | 让 `BeatmapDeleteDialog` 在能拿到具体 beatmap 时复用 shared set-level title authority | [../../osu.Game/Screens/Select/BeatmapDeleteDialog.cs](../../osu.Game/Screens/Select/BeatmapDeleteDialog.cs) | [../../osu.Game/Beatmaps/BeatmapSetInfoExtensions.cs](../../osu.Game/Beatmaps/BeatmapSetInfoExtensions.cs) | [../../osu.Game.Tests/Menus/BeatmapDeleteDialogLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Menus/BeatmapDeleteDialogLocalMetadataDisplayTest.cs) | delete confirmation title display 不再直接读 `beatmapSet.Metadata.GetDisplayTitleRomanisable(false)`，且继续保持不展示 creator suffix、不会误带 difficulty name |
| `K4-S` set-level artist display reuse | 让 `PanelBeatmapSet` 与 `PlaylistItem` 复用 shared set-level artist authority | [../../osu.Game/Screens/Select/PanelBeatmapSet.cs](../../osu.Game/Screens/Select/PanelBeatmapSet.cs) + [../../osu.Game/Overlays/Music/PlaylistItem.cs](../../osu.Game/Overlays/Music/PlaylistItem.cs) | [../../osu.Game/Beatmaps/BeatmapSetInfoExtensions.cs](../../osu.Game/Beatmaps/BeatmapSetInfoExtensions.cs) | [../../osu.Game.Tests/Menus/BeatmapSetArtistLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Menus/BeatmapSetArtistLocalMetadataDisplayTest.cs) | set-level artist display 不再直接读 raw beatmap-set artist metadata，且 focused proof 锁住 BMS artist clean / non-BMS passthrough |
| `K5` parse-side playable cache contract | 把无 mods 的 BMS playable projection 收口到 source-bound cache / invalidation contract | [../../osu.Game/Beatmaps/WorkingBeatmap.cs](../../osu.Game/Beatmaps/WorkingBeatmap.cs)、[../../osu.Game/Beatmaps/ICachedModlessPlayableBeatmapSource.cs](../../osu.Game/Beatmaps/ICachedModlessPlayableBeatmapSource.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedBeatmap.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedBeatmap.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs) | [../../osu.Game.Rulesets.Bms.Tests/BmsPlayableBeatmapCacheTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsPlayableBeatmapCacheTest.cs)、[../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs) | [../../osu.Game.Rulesets.Bms.Tests/BmsPlayableBeatmapCacheTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsPlayableBeatmapCacheTest.cs)、[../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs) | 同源无 mods 复用、跨 source 隔离、带 mods 绕过缓存；loader 首次 conversion 继续保持 import metadata / timing contract |

### 切片执行纪律

1. `K1-A` 到 `K3-A` 首轮只允许触碰 in-memory parse chain；未到 `K4-A` 前，不改 persisted metadata、Song Select UI 或 gameplay runtime。
2. 任何切片若同时需要 parser 和 consumer 改动，必须先拆成 model-first，再做 consumer follow-up；不允许在同一刀里同时改 decode truth 和多个消费面。
3. `K1-B` 完成前，不允许任何 consumer 依赖 `SCROLL` 或 unknown bag；`K3-A` 完成前，不允许任何 consumer 依赖新的同拍位顺序假设；`K3-A` 完成后，也不得把剩余 long-note/visual 语义分散到 consumer 侧补口。
4. `K4-A/K4-B/K4-C/K4-D/K4-E/K4-F/K4-G/K4-H/K4-I/K4-J/K4-K/K4-L/K4-M/K4-N/K4-O/K4-P/K4-Q/K4-R/K4-S` 只处理 projection reuse，不负责新增播放能力；若背景层、Song Select、statistics、core-side read-model、shared display component、gameplay metadata display、results metadata display、profile metadata display、menu metadata display、online play creator display、matchmaking round-results score consumer、beatmap skin metadata consumer、beatmap title display consumer、scoped beatmap-set title display consumer、daily challenge title display consumer、delete confirmation title display consumer、set-level artist display consumer 或未来视觉层需要新 typed event，也必须先完成 `K1-K3`。
5. `K5` 的 source-bound modless playable cache 已在 `K4-E` 之后落地；后续任何新增 cache / perf work 都必须建立在既有 focused regression 上，不得绕开这条 authority 另起第二套 cache contract。

### 首轮测试落点与建议新增测试面

1. [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs)：首轮承接 `SCROLLxx/SC`、unknown bag、signed BPM、duplicate channel line、source line order，以及 BGA / invisible / mine / scroll typed surface、richer BGA-definition header coverage 等 parser 级回归。
2. [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapConverterTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapConverterTest.cs)：承接同拍位 `BPM/STOP/object` 顺序、STOP duration、long-note minimal expression、scroll -> `EffectControlPoint` consumer contract 与 initial BPM fallback 的 converter 回归。
3. [../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs)：承接 import 后 background asset projection reuse、source retention，以及图片引用正规化的 integration proof。
4. [../../osu.Game.Rulesets.Bms.Tests/BmsDrawableRulesetTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsDrawableRulesetTest.cs)：承接 `BmsBackgroundLayer` 对 background asset projection 的消费 proof，避免 playfield 侧重新拼装 `STAGEFILE/BACKBMP/BGA` 语义。
5. [../../osu.Game.Rulesets.Bms.Tests/BmsNoteDistributionGraphTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsNoteDistributionGraphTest.cs)：承接 Song Select note-distribution consumer 对 projected working beatmap 的复用 proof，避免图表面板无条件触发 second conversion。
6. [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapStatisticsTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapStatisticsTest.cs)：承接 statistics consumer 对 metadata chart-filter projection 的复用 proof，避免 beatmap statistics 每次都现场重数同一份 projected hitobjects。
7. [../../osu.Game.Tests/Beatmaps/BeatmapLocalMetadataDisplayResolverTest.cs](../../osu.Game.Tests/Beatmaps/BeatmapLocalMetadataDisplayResolverTest.cs) 与 [../../osu.Game.Tests/Beatmaps/BmsStarRatingResolverTest.cs](../../osu.Game.Tests/Beatmaps/BmsStarRatingResolverTest.cs)：承接 `osu.Game` 侧 BMS metadata display / star-rating consumer 对统一 persisted `chart_metadata` projection 的复用 proof，避免 core read-model 各自手拆 `RulesetDataJson`。
8. [../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterSortingTest.cs](../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterSortingTest.cs)、[../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterGroupingTest.cs](../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterGroupingTest.cs)、[../../osu.Game.Tests/NonVisual/Filtering/FilterMatchingTest.cs](../../osu.Game.Tests/NonVisual/Filtering/FilterMatchingTest.cs) 与 [../../osu.Game.Tests/Beatmaps/BeatmapLocalMetadataDisplayResolverTest.cs](../../osu.Game.Tests/Beatmaps/BeatmapLocalMetadataDisplayResolverTest.cs)：承接 Song Select artist/creator selector 对 `BeatmapLocalMetadataDisplayResolver` 的复用 proof，避免 selector consumer 继续只看 raw local metadata。
9. [../../osu.Game.Tests/Visual/UserInterface/TestSceneBeatmapAttributeText.cs](../../osu.Game.Tests/Visual/UserInterface/TestSceneBeatmapAttributeText.cs) 与 [../../osu.Game.Tests/Skins/BeatmapAttributeTextLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Skins/BeatmapAttributeTextLocalMetadataDisplayTest.cs)：前者继续承接 shared beatmap-attribute display consumer 的邻接 visual proof；后者则作为 CLI-stable plain NUnit focused proof，直接锁住 `BeatmapAttributeText` 的 BMS artist clean、creator fallback 与 non-BMS passthrough contract，不再退化到 compile-only。
10. [../../osu.Game.Tests/Visual/Gameplay/TestSceneBeatmapMetadataDisplay.cs](../../osu.Game.Tests/Visual/Gameplay/TestSceneBeatmapMetadataDisplay.cs)：承接 gameplay loading surface 对 local fallback authority 的 focused scene proof；当前 CLI 可 discover `TestSceneBeatmapMetadataDisplay`，应优先使用整类 scene filter，而不是宽泛匹配 `TestLocal`。
11. [../../osu.Game.Tests/Scores/ExpandedPanelMiddleContentLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Scores/ExpandedPanelMiddleContentLocalMetadataDisplayTest.cs)：承接 results screen expanded metadata surface 对 local fallback authority 的 plain NUnit focused proof；当相邻 visual scene 在 CLI 下不可 discover 时，应优先用这种 plain test 锁住最小 display contract，而不是退化到 compile-only。
12. [../../osu.Game.Tests/Online/ProfileBeatmapMetadataLocalDisplayTest.cs](../../osu.Game.Tests/Online/ProfileBeatmapMetadataLocalDisplayTest.cs)：承接 profile beatmap metadata surface 对 local fallback authority 的 plain NUnit focused proof；当 `TestSceneUserProfileScores` / `TestSceneHistoricalSection` 在 CLI 下不可 discover 时，应优先用这种 plain test 锁住最小 display contract，而不是退化到 compile-only。
13. [../../osu.Game.Tests/Menus/MenuBeatmapMetadataLocalDisplayTest.cs](../../osu.Game.Tests/Menus/MenuBeatmapMetadataLocalDisplayTest.cs)：承接 menu / now-playing metadata surface 对 local fallback authority 的 plain NUnit focused proof；当相邻 visual scene 不直接提供 metadata 断言时，应优先用这种 plain test 锁住最小 display contract，而不是退化到 compile-only。
14. [../../osu.Game.Tests/OnlinePlay/DailyChallengeLocalMetadataDisplayTest.cs](../../osu.Game.Tests/OnlinePlay/DailyChallengeLocalMetadataDisplayTest.cs)：承接 daily challenge metadata surface 对 local creator fallback 与 full-beatmap title authority 的 plain NUnit focused proof；当 visual scene 只验证转场流程、不直接断言 metadata 文本时，应优先用这种 plain test 锁住最小 creator contract 与“无难度名泄漏”的 title contract。
15. [../../osu.Game.Tests/OnlinePlay/DrawableRoomPlaylistItemLocalMetadataDisplayTest.cs](../../osu.Game.Tests/OnlinePlay/DrawableRoomPlaylistItemLocalMetadataDisplayTest.cs)：承接 online playlist item metadata surface 对 local creator fallback 的 plain NUnit focused proof；当 UI 仍需保留 linked-profile 分支时，应把文本 fallback 与 link 分支一并锁进 focused test。
16. [../../osu.Game.Tests/OnlinePlay/SubScreenRoundResultsLocalMetadataDisplayTest.cs](../../osu.Game.Tests/OnlinePlay/SubScreenRoundResultsLocalMetadataDisplayTest.cs)：承接 matchmaking round-results score consumer 对 local beatmap reuse 的 plain NUnit focused proof；当 visual scene 只看到 `ScorePanel` 最终显示结果时，应优先在入口 helper 层锁住“先复用本地 beatmap，再回退 API shell”的 contract。
17. [../../osu.Game.Tests/Skins/LegacyBeatmapSkinLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Skins/LegacyBeatmapSkinLocalMetadataDisplayTest.cs)：承接 beatmap skin metadata 对 local creator fallback 的 plain NUnit focused proof；当 beatmap skin 只通过 `SkinInfo` 暴露 metadata 时，应直接在 `SkinInfo.Creator` 上锁住 contract。
18. [../../osu.Game.Tests/Localisation/BeatmapInfoRomanisationLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Localisation/BeatmapInfoRomanisationLocalMetadataDisplayTest.cs)：承接 `IBeatmapInfo.GetDisplayTitleRomanisable()` 对 local artist / creator fallback 的 plain NUnit focused proof；当具体 UI 只是转发 title string 时，应优先在 extension authority 层锁住 contract。
19. [../../osu.Game.Tests/Localisation/BeatmapDisplayTitleLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Localisation/BeatmapDisplayTitleLocalMetadataDisplayTest.cs)：承接 `IBeatmapInfo.GetDisplayTitle()`、`IBeatmapSetInfo.GetDisplayTitle()` 与 `ModelExtensions.GetDisplayString()` 对 plain title authority 的 plain NUnit focused proof；当 dialog、activity、display-string surface 只是转发 plain title 时，应优先锁住 authority 层 contract。
20. [../../osu.Game.Tests/Menus/ScopedBeatmapSetDisplayLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Menus/ScopedBeatmapSetDisplayLocalMetadataDisplayTest.cs)：承接 scoped beatmap set title display 对 full-beatmap title authority 的 plain NUnit focused proof；当 set-level UI 能拿到具体 beatmap 时，应优先锁住“复用首个 beatmap authority 且不带 difficulty name”的 contract。
21. [../../osu.Game.Tests/Menus/BeatmapDeleteDialogLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Menus/BeatmapDeleteDialogLocalMetadataDisplayTest.cs)：承接 delete confirmation title display 对 shared set-level title authority 的 plain NUnit focused proof；当 delete dialog 需要保留“不展示 creator suffix”的既有外观时，应优先锁住 BMS/non-BMS title passthrough 与“无 creator / 无 difficulty name 泄漏”的 contract。
22. [../../osu.Game.Tests/Menus/BeatmapSetArtistLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Menus/BeatmapSetArtistLocalMetadataDisplayTest.cs)：承接 `PanelBeatmapSet`、`PlaylistItem` 与相邻 set-level artist surface 对 shared set-level artist authority 的 plain NUnit focused proof；当 set-level UI 只是转发 artist string 时，应优先锁住“复用首个 beatmap authority，不再暴露 raw `/obj:` suffix”的 contract。
23. [../../osu.Game.Rulesets.Bms.Tests/TestSceneBmsSongSelectDifficultyTable.cs](../../osu.Game.Rulesets.Bms.Tests/TestSceneBmsSongSelectDifficultyTable.cs)：只有当后续 `K4` 确实影响更广 Song Select projection 消费时才补相邻场景回归；首轮不提前扩大到 UI 层。
24. 若现有测试文件开始混入过多互不相干语义，优先新增 dedicated focused tests，而不是继续把 `BmsBeatmapDecoderTest` 和 `BmsBeatmapConverterTest` 写成巨型回归桶。
25. [../../osu.Game.Rulesets.Bms.Tests/BmsPlayableBeatmapCacheTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsPlayableBeatmapCacheTest.cs)：承接 source-bound modless playable projection cache contract，锁住同源复用、跨 source 隔离、带 mods 绕过缓存，以及 loader-seeded cache 返回的 hold-note projection 已完成 finalize；相邻 loader-focused `BmsImportIntegrationTest` 则继续锁住 import metadata / timing 合同不因 cache seed 回归。

### 推荐验证命令

1. parser focused：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "FullyQualifiedName~BmsBeatmapDecoderTest"`
2. parser + converter focused：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "(FullyQualifiedName~BmsBeatmapDecoderTest|FullyQualifiedName~BmsBeatmapConverterTest)"`
3. static-background projection focused：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "(FullyQualifiedName~BmsBeatmapConverterTest|FullyQualifiedName~BmsDrawableRulesetTest|FullyQualifiedName~BmsImportIntegrationTest)"`
4. note-distribution projection focused：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "FullyQualifiedName~BmsNoteDistributionGraphTest"`
5. statistics projection focused：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "FullyQualifiedName~BmsBeatmapStatisticsTest"`
6. core persisted-metadata projection focused：`dotnet test osu.Game.Tests --no-restore -v minimal --filter "(FullyQualifiedName~BmsStarRatingResolverTest|FullyQualifiedName~BeatmapLocalMetadataDisplayResolverTest)"`
7. Song Select creator selector focused：`dotnet test osu.Game.Tests --no-restore -v minimal --filter "(FullyQualifiedName~TestSortingByAuthorUsesBmsDisplayCreatorFallback|FullyQualifiedName~TestGroupingByAuthorUsesBmsDisplayCreatorFallback|FullyQualifiedName~TestCriteriaMatchingCreatorUsesBmsDisplayCreatorFallback)"`
8. Song Select artist selector focused：`dotnet test osu.Game.Tests --no-restore -v minimal --filter "(FullyQualifiedName~TestSortingByArtistUsesBmsDisplayArtistFallback|FullyQualifiedName~TestGroupingByArtist|FullyQualifiedName~TestCriteriaMatchingArtistDoesNotMatchBmsCreatorSuffix|FullyQualifiedName~TestCriteriaMatchingArtistWithNullUnicodeName|FullyQualifiedName~TestCriteriaNotMatchingArtist|FullyQualifiedName~TestDisplayArtistStripsEmbeddedBmsCreator)"`
9. shared beatmap-attribute display follow-up：`dotnet test osu.Game.Tests --no-restore -v minimal --filter "FullyQualifiedName~BeatmapAttributeTextLocalMetadataDisplayTest"`
10. gameplay metadata display focused：`dotnet test osu.Game.Tests --no-restore -v minimal --filter "FullyQualifiedName~TestSceneBeatmapMetadataDisplay"`
11. results metadata display focused：`dotnet test osu.Game.Tests --no-restore -v minimal --filter "FullyQualifiedName~ExpandedPanelMiddleContentLocalMetadataDisplayTest"`
12. profile metadata display focused：`dotnet test osu.Game.Tests --no-restore -v minimal --filter "FullyQualifiedName~ProfileBeatmapMetadataLocalDisplayTest"`
13. menu metadata display focused：`dotnet test osu.Game.Tests --no-restore -v minimal --filter "FullyQualifiedName~MenuBeatmapMetadataLocalDisplayTest"`
14. daily challenge creator/title metadata focused：`dotnet test osu.Game.Tests --no-restore -v minimal --filter "FullyQualifiedName~DailyChallengeLocalMetadataDisplayTest"`
15. online playlist creator metadata focused：`dotnet test osu.Game.Tests --no-restore -v minimal --filter "FullyQualifiedName~DrawableRoomPlaylistItemLocalMetadataDisplayTest"`
16. matchmaking round-results metadata focused：`dotnet test osu.Game.Tests --no-restore -v minimal --filter "FullyQualifiedName~SubScreenRoundResultsLocalMetadataDisplayTest"`
17. beatmap skin creator metadata focused：`dotnet test osu.Game.Tests --no-restore -v minimal --filter "FullyQualifiedName~LegacyBeatmapSkinLocalMetadataDisplayTest"`
18. beatmap title romanisation focused：`dotnet test osu.Game.Tests --no-restore -v minimal --filter "FullyQualifiedName~BeatmapInfoRomanisationLocalMetadataDisplayTest"`
19. plain title + set-level artist completion focused：`dotnet test osu.Game.Tests --no-restore -v minimal --filter "(FullyQualifiedName~BeatmapDisplayTitleLocalMetadataDisplayTest|FullyQualifiedName~BeatmapSetArtistLocalMetadataDisplayTest)"`
20. beatmap-set title + delete confirmation focused：`dotnet test osu.Game.Tests --no-restore -v minimal --filter "(FullyQualifiedName~BeatmapDeleteDialogLocalMetadataDisplayTest|FullyQualifiedName~ScopedBeatmapSetDisplayLocalMetadataDisplayTest)"`
21. 全量 BMS 回归：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal`
22. Release build gate：`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m`
23. parse-side playable cache focused：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "(FullyQualifiedName~BmsPlayableBeatmapCacheTest|FullyQualifiedName~BmsImportIntegrationTest.TestLoader)"`

### 依赖与回退边界

| 切片 | 进入前提 | 失败信号 | 允许回退 | 明确禁止 |
| --- | --- | --- | --- | --- |
| `K1-A` raw carrier | 当前 decoder focused suite 维持全绿，且新增 carrier 仍是 additive | 现有 converter/importer 因字段接线或排序副作用回归 | 只回退新增 carrier 的可见性或接线，不回退 source-order capture 的测试定义 | 为了保编译或旧测试继续让 source line order 丢失 |
| `K1-B` unknown bag / scroll placeholder | `K1-A` 已稳定，raw carrier 可承接 unknown/scroll 数据 | 新字段要求 persisted metadata、UI 或 runtime 立即消费 | 保留 raw/unknown bag 入口，推迟 consumer 消费与持久化扩散 | 把 `SCROLLxx/SC` 再次降回 warning-only 或文件系统兜底 |
| `K2-A` signed BPM / duplicate line | `K1-B` focused suite 全绿，unknown/scroll 已可保数 | signed BPM 进入 typed model 后导致 converter/runtime 语义冲突；duplicate compound 破坏既有排序假设 | 保留 sign/raw compound authority，推迟 converter 对 sign 的进一步消费 | 把 negative BPM 强行归一成正值，或在 consumer 侧补 ad hoc 覆盖逻辑 |
| `K3-A` timeline semantics | parser focused suite 全绿，且 control-event 语义已有 focused case 锚点 | `BPM/STOP/object` 顺序调整导致 converter focused case 或既有 integration case 回归 | 只在 converter authority 内修正顺序与时间推进；必要时保留新字段但暂缓新 projection 输出 | 在 Song Select、gameplay 或 visual consumer 各自加特判来掩盖 converter 语义问题 |
| `K3-C` visual typed slots | `K3-A/K3-B` focused suite 全绿，且 visual channel 仍主要停留在 raw fallback | typed slot 接线影响既有 object / long-note conversion，或要求 consumer 立即跟改 | 保留 additive typed collections，推迟 richer definition / consumer 消费到后续切片 | 把 BGA / invisible / mine 再次降回 raw-only，或让 consumer 继续直接猜 channel |
| `K3-D` scroll consumer contract | `K3-A/K3-B/K3-C` focused suite 全绿，且 `SCROLLxx/SC` 仍只有保留层、没有稳定 consumer | typed scroll 接线影响既有时间轴 / control point contract，或 unknown-channel compound 继续吞掉 `SC` 事件 | 保留 `ScrollEvents` additive surface，推迟 richer visual-definition / importer consumer 到后续切片 | 把 `SCROLLxx/SC` 再次降回 raw-only，或让 runtime 继续只靠 ad hoc path 猜 scroll 语义 |
| `K3-E` richer BGA-definition headers | `K3-A/K3-B/K3-C/K3-D` focused suite 全绿，且 richer visual-definition family 仍缺显式 header-side typed surface | 新定义字段要求 consumer/runtime 立即跟改，或相同 slot 的 EOF overwrite 与 `UnknownHeaders` 语义发生回归 | 保留 additive typed definition collections，推迟 unified projection 与 consumer 消费到后续切片 | 把 `#BGA`、`#@BGA`、`#ARGB`、`#SWBGA`、`#POORBGA` 再次降回 `UnknownHeaders`-only fallback |
| `K3-F` visual-definition projection | `K3-A/K3-B/K3-C/K3-D/K3-E` focused suite 全绿，且 richer visual-definition family 仍需下游手工重拼多张 definition 表 | unified projection 与原始 tables 语义不一致，或 index union/排序合同发生回归 | 保留原始 typed definition tables，推迟首个 consumer adoption 到后续切片 | 为了某个局部 consumer 方便，继续在下游重复拼接 `BgaDefinitions`、`AtBgaDefinitions`、`ArgbDefinitions` 与 `SwBgaDefinitions` |
| `K4-A` static-background projection reuse | `K1-K3` 已冻结且 full BMS suite 通过 | background metadata / import / playfield consumer 选出的资源不一致，或把两位 bitmap reference 错当成文件名导致回归 | 回退到 parse-chain 内单一 background asset projection authority，允许暂时只保留 metadata/import 复用，但不得恢复 consumer-local second parse | 为了局部 consumer 方便重新引入第二套 parser、第二套 background 选择逻辑，或绕过 `BitmapTable` 直接把两位引用当文件名 |
| `K4-B` note-distribution projection reuse | `K4-A` 已稳定，且 working beatmap projection 已可被 Song Select 读取 | note-distribution consumer 复用 source beatmap 后缺少 `BmsHitObject` projection，或仍无条件触发 playable conversion | 回退到 Song Select 内局部 projection selector，但不得恢复 consumer-local second conversion | 为了图表面板方便重新引入第二套 conversion，或在 Song Select 侧直接从 raw text 重算 note distribution |
| `K4-C` beatmap-statistics projection reuse | `K4-B` 已稳定，且 chart-filter projection 已可由 metadata authority 提供 | statistics consumer 复用 metadata 后仍出现 stale stats，或缺失时没有回写缓存导致重复现场计数 | 回退到 statistics 内局部 metadata-or-compute selector，但不得恢复 consumer-local second parse / second conversion | 为了局部显示方便持续绕开 metadata authority，在每次 `GetStatistics()` 调用都重数一遍 projected hitobjects |
| `K4-D` core persisted-metadata read-model reuse | `K4-C` 已稳定，且 persisted `chart_metadata` 已可在 core 侧被 typed projection 读取 | `osu.Game` 侧 consumer 再次各自手拆 `RulesetDataJson`，或为了显示方便引入对 `osu.Game.Rulesets.Bms` 的反向依赖 | 回退到单一 core-side persisted metadata helper，但不得恢复 per-consumer `JObject.SelectToken("chart_metadata...")` 路径 | 为了局部显示方便继续让 `BeatmapLocalMetadataDisplayResolver`、`BmsStarRatingResolver` 等各自维护一套 stringly-typed JSON token 读取合同 |
| `K4-E` song-select creator selector reuse | `K4-D` 已稳定，且 BMS display creator fallback 已可由 core helper 提供 | Song Select selector consumer 继续把空 `Metadata.Author.Username` 当唯一 authority，或再次各自补一套 creator fallback 提取逻辑 | 回退到单一 display-creator selector helper，但不得恢复 sort/group/filter 各自只看 raw username 的路径 | 为了局部 selector 方便继续让 sorting / grouping / matching 各自绕开 `BeatmapLocalMetadataDisplayResolver.GetDisplayCreator()`，维护三套 BMS creator fallback 合同 |
| `K4-F` local-metadata display consumer reuse | `K4-E` 已稳定，且 BMS display artist / creator fallback 已可由 core helper 提供 | Song Select artist selector 或 shared beatmap-attribute display consumer 继续直接暴露 raw local metadata，或 `BeatmapAttributeTextLocalMetadataDisplayTest` 回归 | 回退到单一 display-artist / display-creator helper，但不得恢复 sort/group/filter / shared display component 各自直接读 raw metadata 的路径 | 为了局部显示方便继续让 Song Select artist selector、`BeatmapAttributeText` 或相邻 display consumer 绕开 `BeatmapLocalMetadataDisplayResolver`，或在已有 plain focused test 时把 proof 降级为仅编译检查 |
| `K4-G` gameplay metadata display reuse | `K4-F` 已稳定，且 BMS display artist / creator fallback 已可由 core helper 提供 | gameplay metadata display 继续直接展示 raw local metadata，或 focused scene filter 因宽泛命名误命中无关 `TestLocal*` 用例 | 回退到单一 gameplay metadata display helper，但不得恢复 loading surface 直接读 raw artist / creator 的路径 | 为了局部验证或显示方便继续让 `BeatmapMetadataDisplay` 绕开 `BeatmapLocalMetadataDisplayResolver`，或用宽泛 test filter 混跑无关 scene 伪装 focused proof |
| `K4-H` results metadata display reuse | `K4-G` 已稳定，且 BMS display artist / creator fallback 已可由 core helper 提供 | results metadata display 继续直接展示 raw local metadata，或因 visual scene 不可 discover 而退化成 compile-only proof | 回退到单一 results metadata display helper，但不得恢复 expanded results surface 直接读 raw artist / creator 的路径 | 为了局部验证或显示方便继续让 `ExpandedPanelMiddleContent` 绕开 `BeatmapLocalMetadataDisplayResolver`，或在已有 plain focused test 可行时把 proof 降级为仅编译检查 |
| `K4-I` profile metadata display artist reuse | `K4-H` 已稳定，且 BMS display artist fallback 已可由 core helper 提供 | profile metadata display 继续直接展示 raw local artist，或因 visual scene 不可 discover 而退化成 compile-only proof | 回退到单一 profile metadata display helper，但不得恢复 profile beatmap metadata surface 直接读 raw artist 的路径 | 为了局部验证或显示方便继续让 `DrawableProfileScore` / `DrawableMostPlayedBeatmap` 绕开 `BeatmapLocalMetadataDisplayResolver`，或在已有 plain focused test 可行时把 proof 降级为仅编译检查 |
| `K4-J` menu metadata display artist reuse | `K4-I` 已稳定，且 BMS display artist fallback 已可由 core helper 提供 | menu metadata display 继续直接展示 raw local artist，或 visual scene 没有 metadata 断言时退化成 compile-only proof | 回退到单一 menu metadata display helper，但不得恢复 `SongTicker` / `NowPlayingOverlay` 直接读 raw artist 的路径 | 为了局部验证或显示方便继续让 `SongTicker` / `NowPlayingOverlay` 绕开 `BeatmapLocalMetadataDisplayResolver`，或在 plain focused test 可行时把 proof 降级为仅编译检查 |
| `K4-K` daily challenge creator display reuse | `K4-J` 已稳定，且 BMS display creator fallback 已可由 core helper 提供 | daily challenge metadata display 继续直接展示 raw local creator，或 visual scene 仅覆盖转场时退化成 compile-only proof | 回退到单一 daily challenge creator helper，但不得恢复 `DailyChallengeIntro` 直接读 raw creator 的路径 | 为了局部验证或显示方便继续让 `DailyChallengeIntro` 绕开 `BeatmapLocalMetadataDisplayResolver`，或在 plain focused test 可行时把 proof 降级为仅编译检查 |
| `K4-L` online playlist creator display reuse | `K4-K` 已稳定，且 BMS display creator fallback / linked-profile 边界已可由 core helper 提供 | playlist creator line 因空 `Metadata.Author.Username` 继续被隐藏，或 fallback creator 被错误伪装成 user link | 回退到单一 playlist creator helper，但不得恢复 `DrawableRoomPlaylistItem` 直接按 raw username 决定是否显示作者行的路径 | 为了局部验证或显示方便继续让 `DrawableRoomPlaylistItem` 绕开 `BeatmapLocalMetadataDisplayResolver`，或把 linked-profile 分支遗漏出 focused proof |
| `K4-M` round-results local beatmap reuse | `K4-L` 已稳定，且 round-results metadata display 已可通过既有 `ExpandedPanelMiddleContent` authority 读取 local fallback | `SubScreenRoundResults` 继续在本地谱面存在时重建 API 最小壳，导致 persisted chart metadata 被覆盖 | 回退到单一 score-beatmap helper，但不得恢复 `SubScreenRoundResults` 无条件使用 API shell 的路径 | 为了局部验证或显示方便继续让 round results score consumer 绕开本地 `BeatmapInfo` 复用，或遗漏 API fallback shell 分支的 focused proof |
| `K4-N` beatmap skin creator display reuse | `K4-M` 已稳定，且 BMS display creator fallback 已可由 core helper 提供 | beatmap skin metadata 在空 `Metadata.Author.Username` 时继续丢失 creator | 回退到单一 skin-info helper，但不得恢复 `LegacyBeatmapSkin` 直接读 raw username 的路径 | 为了局部验证或显示方便继续让 beatmap skin metadata 绕开 `BeatmapLocalMetadataDisplayResolver`，或在 plain focused test 可行时把 proof 降级为仅编译检查 |
| `K4-O` beatmap title display authority reuse | `K4-N` 已稳定，且 full-beatmap title display 已具备 artist / creator fallback 所需上下文 | `IBeatmapInfo.GetDisplayTitleRomanisable()` 继续暴露 embedded BMS creator suffix，或在空 raw creator 时继续丢失 fallback creator | 回退到单一 title-display helper，但不得恢复 `IBeatmapInfo` title display consumer 直接拼 raw metadata 的路径 | 为了局部验证或显示方便继续让 title display consumer 绕开 `BeatmapLocalMetadataDisplayResolver`，或遗漏 artist / creator 其中一侧的 focused proof |
| `K4-P` scoped beatmap-set title display reuse | `K4-O` 已稳定，且 scoped set banner 在有具体 beatmap 时已具备 full-beatmap title authority 所需上下文 | `FilterControl.ScopedBeatmapSetDisplay` 继续直接走 `BeatmapSetInfo.Metadata.GetDisplayTitleRomanisable()` 暴露 raw `/obj:` suffix，或误把 difficulty name 带进 set 级标题 | 回退到单一 scoped-set title helper，但不得恢复 `ScopedBeatmapSetDisplay` 直接读 metadata-only overload 的路径 | 为了局部验证或显示方便继续让 scoped beatmap-set title display 绕开 full-beatmap authority，或遗漏“禁用 difficulty name”这一侧的 focused proof |
| `K4-Q` daily challenge title display reuse | `K4-P` 已稳定，且 daily challenge intro 在有具体 beatmap 时已具备 full-beatmap title authority 所需上下文 | `DailyChallengeIntro` 继续直接走 `beatmap.BeatmapSet!.Metadata.GetDisplayTitleRomanisable(false)` 暴露 raw `/obj:` suffix，或把 difficulty name 重新带回标题行 | 回退到单一 daily challenge title helper，但不得恢复 `DailyChallengeIntro` 直接读 set-level metadata-only overload 的路径 | 为了局部验证或显示方便继续让 daily challenge title display 绕开 full-beatmap authority，或遗漏“禁用 difficulty name”这一侧的 focused proof |
| `K4-R` delete confirmation title display reuse | `K4-Q` 已稳定，且 delete dialog 在有具体 beatmap 时已具备 shared set-level title authority 所需上下文 | `BeatmapDeleteDialog` 继续直接走 `beatmapSet.Metadata.GetDisplayTitleRomanisable(false)` 暴露 raw `/obj:` suffix，或重新漏出 creator suffix / difficulty name | 回退到单一 delete-confirmation title helper，但不得恢复 `BeatmapDeleteDialog` 直接读 metadata-only overload 的路径 | 为了局部验证或显示方便继续让 delete confirmation title display 绕开 shared set-level title authority，或遗漏“无 creator suffix / 无 difficulty name”任一侧的 focused proof |
| `K4-S` set-level artist display reuse | `K4-R` 已稳定，且 set-level surface 在有具体 beatmap 时已具备首个 beatmap artist authority 所需上下文 | `PanelBeatmapSet` 或 `PlaylistItem` 继续直接走 `beatmapSet.Metadata.Artist` / `ArtistUnicode` 暴露 raw `/obj:` suffix，或 `BeatmapSetArtistLocalMetadataDisplayTest` 回归 | 回退到单一 set-level artist helper，但不得恢复各处 set-level surface 各自直接读 raw set metadata 的路径 | 为了局部显示方便继续让 `PanelBeatmapSet`、`PlaylistItem` 或相邻 set-level artist consumer 绕开 `BeatmapSetInfoExtensions.GetDisplayArtistRomanisable()`，或在已有 plain focused test 时把 proof 降级为仅编译检查 |

补充规则：

1. 任何切片一旦触发失败信号，下一步必须先回到该切片自己的 focused validation，不允许直接跳到更宽的 suite 试图“碰运气过线”。
2. 回退只允许收缩新增暴露面、消费面或复用面，不允许回退已经建立的 no-loss carrier 与 focused regression 定义。
3. 若某切片的唯一可行回退已经越过 `P1-K` authority，必须停下并改写文档分线，而不是在同一实现里继续硬推。

### 开工定义

满足下面四条后，`P1-K` 就可以直接按文档开工，而无需再补口头规划：

1. 先按 `K1-A -> K1-B -> K2-A -> K3-A -> K3-B -> K3-C -> K3-D -> K3-E -> K3-F -> K4-A -> K4-B -> K4-C -> K4-D -> K4-E -> K4-F -> K4-G -> K4-H -> K4-I -> K4-J -> K4-K -> K4-L -> K4-M -> K4-N -> K4-O -> K4-P -> K4-Q -> K4-R -> K4-S -> K5 -> K6 -> K7 -> K8` 的顺序推进；当前 `K4`、`K5`、`K6`、`K7` 与 `K8` 已整体收口，若继续推进则首个正式 post-`K8` 切片固定为 `K9`，不再继续开新的 `K4/K5/K6/K7/K8` 字母刀，也不接受未归线的 backlog 插队。
2. 每刀只改文件级切片图里列出的 primary files；相邻文件只在编译或测试明确要求时补入。
3. 每刀完成后先跑对应 focused command，再决定是否进入下一刀。
4. 任一切片若需要跨到 `P1-H` persisted metadata、`P1-J` runtime hot path 或 `P1-A` 公开 product surface，就停止并把该变动拆回对应子线，不在 `P1-K` 里硬推。

## 分期计划

### K0：归线、术语与基线冻结

状态：已完成（文档）

目标：先把“哪一层保存原始数据、哪一层保存归一化语义、哪一层只做 consumer projection”写死，避免后续继续在 importer / Song Select / gameplay / 特效层各自长出第二套 parse truth。

建议交付：

1. 建立 `P1-K` 四件套，并同步主线索引、主线总规划、主线状态页与主线变更日志。
2. 冻结三层术语：`raw source snapshot`、`normalized chart model`、`consumer projection`。
3. 把当前 parse gap、现有缓解项与外部语义参考写成 authority 文档，而不是继续散落在会话结论里。
4. 先定测试面：decoder、converter、import/raw-wrapper 三层 focused coverage；真实谱面人工验校继续后置到 `P1-E` / `P1-G`。

### K1：raw / typed 双层模型与 no-loss 解析

状态：已完成（当前定义）

目标：让 decode 阶段具备“已实现能力可 typed 消费、未实现能力仍可完整回收”的稳定模型。

当前 `K1` 定义已由 `K1-A/K1-B` 与后续 `K3-K8` 邻接合同共同收口；下面保留的建议项只作为历史设计边界与未来 backlog 参考，不再作为当前进行中事项。

当前 `P1-K` 已连续落地二十八刀 parser/model/converter/consumer slice：`K1-A` 已把 raw carrier 命名为 `RawChannelEvents`，并让 `BmsChannelEvent` 保留 `RawChannelToken` 与 `SourceLineOrder`；`K1-B` 进一步补上了 `ScrollTable`、`UnknownHeaders` 与非十六进制 channel token 的 raw placeholder 保留；`K2-A` 已让 negative `#BPMxx` 进入 typed model，并对 duplicate channel line 加上 source-order-aware compound；`K3-A` 已把同拍位 `BPM -> STOP -> object` 顺序与 signed BPM 的 magnitude timeline 消费冻结在 converter authority 内；`K3-B` 补上了 `LNTYPE 2` 的最小 MGQ long-note expression，并证明它能端到端转成 `BmsHoldNote`；`K3-C` 进一步把 BGA / invisible / mine 频道接进第一批 additive typed surface；`K3-D` 则把 `SCROLLxx` 定义 + `SC` channel line 接进 `ScrollEvents`，并进一步桥接到 `EffectControlPoint.ScrollSpeed`；`K3-E` 又把 `#BGA`、`#@BGA`、`#ARGB`、`#SWBGA` 与 `#POORBGA` 接进 `BmsBeatmapInfo` + `BmsVisualDefinitions` 的 header-side typed surface；`K3-F` 则进一步把这些分散 definitions 收口成按 index 读取的 unified projection contract；`K4-A` 现已让 static background metadata / import / playfield consumer 共享同一 background asset projection，`K4-B` 让 Song Select note distribution graph 在 projected source beatmap 可用时避免 second conversion，`K4-C` 让 beatmap statistics 优先复用 metadata 中的 chart-filter projection，`K4-D` 让 `osu.Game` 侧 metadata display / star-rating consumer 共享 persisted `chart_metadata` projection，`K4-E` 让 Song Select author sort/group/filter 共享同一 display creator fallback，`K4-F` 则进一步让 Song Select artist sort/group/filter 与 `BeatmapAttributeText` 共享同一 display artist / creator fallback，`K4-G` 则继续让 `BeatmapMetadataDisplay` 共享同一 gameplay metadata display fallback，`K4-H` 则进一步让 `ExpandedPanelMiddleContent` 共享同一 results metadata display fallback，`K4-I` 则进一步让 `DrawableProfileScore` 与 `DrawableMostPlayedBeatmap` 共享同一 profile metadata display artist fallback，`K4-J` 则进一步让 `SongTicker` 与 `NowPlayingOverlay` 共享同一 menu metadata display artist fallback，`K4-K` 则进一步让 `DailyChallengeIntro` 共享同一 daily challenge creator fallback，`K4-L` 则进一步让 `DrawableRoomPlaylistItem` 共享同一 online playlist creator fallback，并显式保留 linked-profile 分支，`K4-M` 则进一步让 `SubScreenRoundResults` 在构造 round-results `ScoreInfo` 时优先复用本地 `BeatmapInfo`，仅在缺失时回退到 API 最小壳，`K4-N` 则进一步让 `LegacyBeatmapSkin` 的 beatmap skin metadata 共享同一 display creator fallback，`K4-O` 则进一步让 `IBeatmapInfo.GetDisplayTitleRomanisable()` 同时共享同一 display artist / creator fallback，`K4-P` 则进一步让 `FilterControl.ScopedBeatmapSetDisplay` 在能拿到具体 beatmap 时优先复用首个 beatmap 的 full title authority，并显式禁用 difficulty name，`K4-Q` 则进一步让 `DailyChallengeIntro` 在 title line 能拿到具体 beatmap 时优先复用同一 full title authority，并显式禁用 difficulty name，`K4-R` 则进一步让 `BeatmapDeleteDialog` 通过 `BeatmapSetInfo.GetDisplayTitleRomanisable(includeCreator: false)` 共享同一 set-level title authority，不再继续直接走 `beatmapSet.Metadata.GetDisplayTitleRomanisable(false)`，并继续保持不展示 creator suffix，而 `K4-S` 则进一步让 `BeatmapSetInfoExtensions.GetDisplayArtistRomanisable()` 成为 shared set-level artist helper，并让 `PanelBeatmapSet` 与 `PlaylistItem` 优先复用首个 beatmap 的 display artist authority，不再继续直接走 raw set metadata。后续重点转为更零散的 core/read-model 尾项与 special long-note parity，而不是再回退成 warning-only 或 consumer 侧补口。

建议交付：

1. 为 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs) 明确 raw snapshot 与 typed collections 的职责分层，避免 `ChannelEvents` 既承担 fallback 又承担唯一 authority。
2. 继续扩展 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs) 的保留面；当前首轮已补上 `ScrollTable` 与等价 unknown header bag，后续至少还要为 `PlayerMode`、BGA config 或更细粒度的 unknown definition slot 留出 stable surface。
3. 为 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs) 增加 source-order-aware raw event surface，保留 channel token、raw value、measure、fraction 与 source line order。
4. 首轮不要求所有新字段立刻被 consumer 使用，但要求 decode 阶段不再直接丢掉未来可能需要的语义数据。

### K2：header / definition / channel coverage

状态：已完成（当前定义）

目标：把当前已经确认会丢数据的 header、indexed definition 与 channel token 收口为 no-loss coverage。

当前 `K2` 定义已由 `K2-A` 与后续 `K3-K8` 邻接合同共同收口；下面保留的建议项只作为历史设计边界与未来 backlog 参考，不再作为当前进行中事项。

建议交付：

1. 明确处理 `PLAYER`、未来 unknown header 与 indexed definition 的保存策略；`POORBGA` 已于 `K3-E` 进入 typed surface。
2. 为 duplicate channel line 增加 source-order-aware compound / overwrite pass，而不是继续按“逐条展开后排序”当成长期语义。
3. 让非十六进制 channel token 的保留与 typed projection 有明确边界，不能继续因为 parser 入口假设而直接忽略整条数据。
4. 继续保留 raw channel fallback；typed coverage 只负责给已确认重要的语义建立稳定消费面。

### K3：timeline / control-event 语义冻结

状态：已完成（`K3-A/K3-B/K3-C/K3-D/K3-E/K3-F` 已落地）

目标：让时间轴语义由 converter 单独拥有，避免 consumer 各自重新推导 `BPM/STOP/object` 顺序与 long-note 边界。

建议交付：

1. 同拍位 control-event 顺序已在 converter authority 内冻结为 `BPM -> STOP -> object`；后续若出现例外，只能继续在 `BmsBeatmapConverter` 内修正。
2. signed BPM 已进入 typed model，converter 时间推进现按绝对值消费；即使当前 runtime 只用 magnitude，方向信息也不得在 parse / convert 层被抹掉。
3. `K3-B` 已让 `LNTYPE 2` 通过 LN channel 显式 `00` close marker 获得最小表达；这条 long-note 语义现已具备 end-to-end hold-note proof。
4. `K3-C` 已把 BGA base/poor/layer/layer2、invisible object 与 landmine channel 接进第一批 typed collections。
5. `K3-D` 已把 `SCROLLxx/SC` 通过 `ScrollEvents` + `EffectControlPoint` 接进首个 runtime consumer contract。
6. `K3-E` 已把 `#BGA`、`#@BGA`、`#ARGB`、`#SWBGA` 与 `#POORBGA` 接进 `BmsBeatmapInfo` + `BmsVisualDefinitions` 的 header-side typed surface；后续重点已转为统一 projection 与其首个 consumer，而不是让“只 warning、不建模”成为长期状态。
7. `K3-F` 已把分散的 richer visual-definition headers 进一步收口为 unified projection contract，并由 `K4-A` 证明 static background consumer 可以直接复用该 authority；后续重点转为更广 projection reuse 与剩余 special long-note parity，避免任意下游继续各自手工重拼多张 definition 表。
8. `BmsBeatmapConverter` 只消费 normalized chart model，不允许 consumer 侧为同一份 chart 再次推导时间轴。

### K4：parse-once / project-many 调用复用

状态：已完成（`K4-A` 至 `K4-S` 与数字层级收口已落地）

目标：把 importer、raw working beatmap、Song Select、gameplay、背景/特效层与统计分析统一到同一份 parse 结果或等价缓存上。

建议交付：

1. 延续 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs) 当前已经建立的 projection reuse 方向，不再让 raw wrapper、Song Select、gameplay、results、profile metadata display、menu metadata display、online play creator display、matchmaking round-results score consumer、beatmap skin metadata consumer、beatmap title display consumer、plain title / display-string consumer、scoped beatmap-set title display consumer、daily challenge title display consumer、delete confirmation title display consumer 与 set-level artist display consumer 各自触发 second conversion；`K4-A` 已先证明 static background metadata / import / playfield background layer 可以共享同一 projection，`K4-B` 已让 Song Select note distribution graph 在 projected source beatmap 可用时复用同一 working-beatmap authority，`K4-C` 已让 beatmap statistics 优先复用 metadata 中的 chart-filter projection，`K4-D` 已让 core-side metadata display / star-rating consumer 共享 persisted `chart_metadata` projection，`K4-E` 已让 Song Select author sort/group/filter 继续复用同一 display creator fallback，`K4-F` 则进一步让 Song Select artist sort/group/filter 与 `BeatmapAttributeText` 继续复用同一 local display fallback，`K4-G` 已让 `BeatmapMetadataDisplay` 继续复用同一 gameplay metadata display fallback，`K4-H` 已让 `ExpandedPanelMiddleContent` 继续复用同一 results metadata display fallback，`K4-I` 已让 `DrawableProfileScore` 与 `DrawableMostPlayedBeatmap` 继续复用同一 profile metadata display artist fallback，`K4-J` 已让 `SongTicker` 与 `NowPlayingOverlay` 继续复用同一 menu metadata display artist fallback，`K4-K` 已让 `DailyChallengeIntro` 继续复用同一 daily challenge creator fallback，`K4-L` 已让 `DrawableRoomPlaylistItem` 继续复用同一 online playlist creator fallback 与 linked-profile 边界，`K4-M` 已让 `SubScreenRoundResults` 继续复用同一 round-results beatmap metadata authority，在本地谱面存在时不再重建 API 最小壳，`K4-N` 已让 `LegacyBeatmapSkin` 继续复用同一 beatmap skin creator fallback，`K4-O` 已让 `IBeatmapInfo.GetDisplayTitleRomanisable()` 继续复用同一 title display authority，随后又把 `IBeatmapInfo.GetDisplayTitle()`、`IBeatmapSetInfo.GetDisplayTitle()`、`ModelExtensions.GetDisplayString()`、`BeatmapSetHeaderContent`、`BeatmapCardNormal/Nano/Extra`、`MatchmakingSelectPanel.CardContentBeatmap` 与 `OnlinePlay.Components.BeatmapTitle` 一并并入同一 authority；因此 `K4` 已作为数字层级整体收口，不再继续扩写新的 `K4` 字母刀。
2. 为“哪些 projection 可以 persisted、哪些 projection 只应 runtime/lazy materialize”建立边界，避免 data layer 与 consumer cache 混写。
3. 让未来视觉层、背景层或特效谱层优先消费 typed visual event / projection，而不是直接在 render 期遍历 raw `ChannelEvents`；其中背景层的首个 consumer 已于 `K4-A` 落地。
4. 继续保留原始 chart 文件作为现场诊断与兜底资源，但它不再是 consumer 正常工作的主要 authority。

### K5：解析侧性能与缓存合同

状态：已完成

目标：把 parse-side 性能工作限定在“减少重复 decode / normalize / project”的范围内，而不是扩大成 runtime 热路径治理。

已完成交付：

1. [../../osu.Game/Beatmaps/WorkingBeatmap.cs](../../osu.Game/Beatmaps/WorkingBeatmap.cs) 与 [../../osu.Game/Beatmaps/ICachedModlessPlayableBeatmapSource.cs](../../osu.Game/Beatmaps/ICachedModlessPlayableBeatmapSource.cs) 现已建立稳定的 source identity 与 projection invalidation 规则：`WorkingBeatmap.GetPlayableBeatmap()` 对实现该 contract 的 source beatmap 会优先复用无 mods playable projection；换 source 或带 mods 时则重新转换。
2. [../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedBeatmap.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedBeatmap.cs) 与 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs) 现已把 lazy materialization 限定在 parse-side playable projection：`BmsDecodedBeatmap` 会按 ruleset short name 持有 modless playable cache，而 loader 首次 conversion 也会把现成 projection seed 回 source wrapper。
3. [../../osu.Game.Rulesets.Bms.Tests/BmsPlayableBeatmapCacheTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsPlayableBeatmapCacheTest.cs) 现已以 dedicated focused proof 锁住“同源复用 / 跨 source 隔离 / 带 mods 绕过缓存 / loader-seeded finalize”，并由 [../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs) 的 loader-focused 回归继续确认 import metadata / timing 既有合同未回归。
4. 任何后续 cache / perf work 仍不得改变 parse semantics；语义变化必须先有 focused regression 再谈缓存。

### K6：focused validation 与特效谱前置支持

状态：已完成

目标：在真正实现未来视觉/特效消费前，先把 results/statistics 邻接面接回 `Ruleset` 的“已带 mods playable beatmap”合同，并让 parse-side cache / results-side consumer reuse 都有 focused proof。

已完成交付：

1. [../../osu.Game/Rulesets/Ruleset.cs](../../osu.Game/Rulesets/Ruleset.cs) 已明确 `PrepareScoreInfoForResults()` 与 `CreateStatisticsForScore()` 接收的是“已应用所有相关 mods 的 playable beatmap”；[../../osu.Game.Rulesets.Bms/BmsRuleset.cs](../../osu.Game.Rulesets.Bms/BmsRuleset.cs) 与 [../../osu.Game.Rulesets.Bms/Scoring/BmsClearLampProcessor.cs](../../osu.Game.Rulesets.Bms/Scoring/BmsClearLampProcessor.cs) 现已按此合同消费 caller 传入的 beatmap，不再在 results/gauge helper 内再次调用 `BmsBeatmapModApplicator`。
2. [../../osu.Game.Rulesets.Bms.Tests/BmsPlayableBeatmapCacheTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsPlayableBeatmapCacheTest.cs) 现已新增 `Mirror` focused proof，直接锁住 `PrepareScoreInfoForResults()` 不会对已带 mods 的 playable beatmap 重复应用 beatmap mods；同一 suite 基线现为 **5/5**。
3. [../../osu.Game.Rulesets.Bms.Tests/BmsClearLampProcessorTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsClearLampProcessorTest.cs) 现已新增 `Mirror` focused proofs，分别锁住 `CreateGaugeHistory()` 与 `CalculateFinalGauge()` 不会对已带 mods 的 playable beatmap 重复应用 beatmap mods；依赖 long-note / assist 语义的 HCN、autoplay 邻接用例也已显式先应用 score mods，再进入 results helper。该 suite 基线现为 **32/32**。
4. 本轮验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsPlayableBeatmapCacheTest"` **5/5** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsClearLampProcessorTest"` **32/32** 通过。当未来开始实现视觉层、背景层或特效谱播放时，新的 player-level / visual tests 也必须继续消费 `P1-K` 已冻结的 typed surface，而不是临时重 parse 文本。

### K7：results summary consumer proof 与 clear-lamp 优先级冻结

状态：已完成

目标：把 `K6` 已冻结的“已带 mods playable beatmap”results authority 继续抬到 `CreateStatisticsForScore()` 的 results summary consumer，证明 summary panel 消费的是同一结果侧 contract，而不是由 UI / scene 侧重新猜 gauge/lamp 文本。

已完成交付：

1. [../../osu.Game.Rulesets.Bms/UI/BmsResultsSummaryDisplay.cs](../../osu.Game.Rulesets.Bms/UI/BmsResultsSummaryDisplay.cs) 的 `SkinnableBmsResultsSummaryPanelDisplay` 现已保留只读 `Summary` state，供 focused proof 直接读取 `CreateStatisticsForScore()` 生成的 summary 数据，而不再依赖 CLI 下不稳定的 skinnable scene 装载链。
2. [../../osu.Game.Rulesets.Bms.Tests/BmsRulesetStatisticsTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsRulesetStatisticsTest.cs) 新增 `TestCreateStatisticsSummaryCarriesSelectedModesAndClearLamp()`，直接锁住 gauge type / display name、gauge rules family、judge mode、long-note mode、EX-SCORE、DJ LEVEL 与 clear-lamp summary state 会从 `CreateStatisticsForScore()` 端到端进入 results summary consumer。
3. 该 proof 同时冻结结果灯级优先级：clear check 通过后，`PERFECT` / `FULL COMBO` 仍会覆盖 gauge-derived lamp，因此 results summary consumer 必须消费 `BmsClearLampProcessor` 的计算结果，不能按 gauge type 自行拼 `HAZARD CLEAR` 之类的显示文本。
4. 本轮验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsRulesetStatisticsTest.TestCreateStatistics"` **3/3** 通过；当相邻 skinnable visual scene 在 CLI 下不稳定时，plain NUnit focused proof 现已作为该 consumer slice 的正式回归路径。

### K8：gauge history consumer proof 与 auto-shift timeline state 冻结

状态：已完成

目标：把 `K6` 已冻结的 results-side gauge-history authority 继续抬到 `CreateStatisticsForScore()` 的 gauge history consumer，证明 results panel 消费的是完整的 `BmsGaugeHistory` timeline state，而不是只返回一个 skinnable shell type。

已完成交付：

1. [../../osu.Game.Rulesets.Bms/UI/BmsGaugeHistoryGraph.cs](../../osu.Game.Rulesets.Bms/UI/BmsGaugeHistoryGraph.cs) 的 `SkinnableBmsGaugeHistoryPanelDisplay` 现已保留只读 `History` state，供 focused proof 直接读取 `CreateStatisticsForScore()` 生成的 gauge history 数据，而不再依赖 CLI scene 装载链或 panel 内部 drawable 遍历。
2. [../../osu.Game.Rulesets.Bms.Tests/BmsRulesetStatisticsTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsRulesetStatisticsTest.cs) 新增 `TestCreateStatisticsGaugeHistoryCarriesAutoShiftTimelineState()`，直接锁住 `CreateStatisticsForScore()` 生成的 gauge history consumer 会携带完整 timeline state，而不是仅返回 `SkinnableBmsGaugeHistoryPanelDisplay` 的 panel type。
3. 该 proof 同时冻结 auto-shift timeline 的 consumer 语义：`EX-HARD -> HARD -> NORMAL` 的 gauge 转档与每条时间线的 sample/time/value 都必须从 `BmsClearLampProcessor.CreateGaugeHistory()` 端到端进入 results consumer，不得在 panel/UI 层重新拼装或简化成另一套 timeline。
4. 本轮验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsRulesetStatisticsTest.TestCreateStatistics"` **4/4** 通过；`CreateStatistics` focused slice 现已同时覆盖 summary consumer 与 gauge history consumer 两侧的 value-level proof。

### K9：BMS -> mania 单向转谱合同冻结

状态：进行中（dedicated converter / public gate / sample-only scratch / persisted converted star 已落地；explicit public wording 与更宽 presentation/manual proof 仍待收口）

目标：在不重开第二套 parser、不复用 generic convert heuristics 的前提下，为 BMS source chart 提供独立的 mania playable projection，并把 keymode、lane flatten、scratch-family sample-only 语义、converted-star persistence 与 conversion validity 一次性写死。

已完成 / 冻结交付：

1. 冻结 source/target 矩阵：`5K+1S -> mania 5K`、`7K+1S -> mania 7K`、`9K_Bms -> mania 9K`、`9K_Pms -> mania 9K`、`14K+2S -> mania 14K`；当前专题只允许 `BMS -> mania` 单向路径，不反向支持 `mania -> BMS`，也不把 generic all-ruleset convert surface 一并扩大。
2. 冻结 lane flatten authority：转换必须继续消费 [../../osu.Game.Rulesets.Bms/UI/BmsLaneLayout.cs](../../osu.Game.Rulesets.Bms/UI/BmsLaneLayout.cs) 与现有 BMS decoded/playable model 提供的 canonical lane order / relative width contract，而不是读取当前用户 `PlayfieldStyle`、scratch 视觉侧别或其它 runtime 布局设置；同一 source chart 的 mania 转谱结果必须与用户皮肤、HUD 与单机布局偏好无关。对于非 scratch playable lanes，flatten 结果固定为“移除 scratch lane 后保留原 canonical 顺序并重新从左到右编号”，不得再引入 geometry-aware shuffle 或 style-aware remap。
3. 冻结 target stage definition：`5K+1S/7K+1S/9K_Bms/9K_Pms` 分别落到单 stage `5/7/9` 列；`14K+2S` 固定落到 mania dual-stage `7 + 7`，而不是单 stage `14` 列。`14K` 的左右半侧在转谱后继续保持 stage 边界，左侧 object 不得跨到右 stage，右侧 object 也不得跨到左 stage。
4. 冻结 scratch-family 语义：target keycount 不保留 scratch 作为独立 judged column authority，但 scratch-family object 也不能被静默丢弃；scratch tap 与 scratch long-note 都必须保留原 keysound / head-tail sample 语义，并以 sample-only、ignore-judgement 的 converted mania object 保留时间线，而不是再退化成可判定 `Note` / `HoldNote` 并入真实列。若需要 `Column`，它只能服务同 side / 同 stage 的 drawable/sample anchor，不得重新进入 combo、statistics、star 或 judged-lane authority。`PMS` 因本身无 scratch lane，继续走 9K 一对一列映射，不额外引入 scratch 分支。
5. 冻结 mania column quantisation 方式：当前 [../../osu.Game.Rulesets.Mania/Beatmaps/ManiaBeatmapConverter.cs](../../osu.Game.Rulesets.Mania/Beatmaps/ManiaBeatmapConverter.cs) 的 pass-through path 会把 `IHasXPosition.X` 视为 `0..512` 的横向空间，再量化为目标列；因此 `K9` 不得直接把 [../../osu.Game.Rulesets.Bms/Objects/BmsHitObject.cs](../../osu.Game.Rulesets.Bms/Objects/BmsHitObject.cs) 现有的 `IHasXPosition = LaneIndex` 当成可直接复用的合同，而必须先建立 BMS lane -> normalized mania-space 的专用投影，再进入 mania object materialize path 或等价 helper。
6. 冻结 source-mod 边界：`K9` 首轮只消费 modless source BMS chart；`BmsModMirror`、`BmsModRandom`、`A-SCR`、`A-NOT`、`Autoplay` 与其它 BMS runtime mod 都不得参与转谱映射、validity 或目标列决定。若未来需要“带 source-side mods 的 BMS -> mania projection”，必须另起后续切片，不得在 `K9` 首轮里与 canonical convert contract 混写。
7. 冻结 persisted converted-star 合同：modless `BMS -> mania` 星数必须写入 `BeatmapMetadata.RulesetDataJson` 的 BMS payload，并携带 target ruleset、difficulty version 与 conversion version；`BeatmapInfo.StarRating` 继续保留为 source BMS raw star/playlevel authority，不得被 target-ruleset 星数覆盖。
8. 冻结公开表面边界：`K9` 只拥有 source chart -> mania projection、supported keymode gate、“flatten 后无可游玩对象则不产出 convert 结果”的 validity 合同，以及 current-ruleset resolved-star read-model；它不在同一刀里收口按钮 wording、显式入口文案或更宽的 product proof，相关入口与文案统一后置到 `P1-A`。
9. 冻结 focused validation 顺序：先补 dedicated mapping / sample proof，覆盖 `5K+1S/7K+1S/9K_Bms/9K_Pms/14K+2S` 五类 keymode、`14K -> 7+7` dual-stage 形态、sample-only scratch keysound 保留、source-side modless gate，以及空结果 suppress case；再补 autoplay ignore-only proof 与 selector/resolver proof；若继续推进公开表面，再补 `PresentBeatmap` / Song Select exposure focused proof 与更宽 visual / navigation proof。

建议测试锚点：

1. [../../osu.Game.Rulesets.Mania.Tests/BmsToManiaBeatmapConverterTest.cs](../../osu.Game.Rulesets.Mania.Tests/BmsToManiaBeatmapConverterTest.cs)：锁住 keymode matrix、flatten column 结果、`14K -> 7+7` dual-stage、sample-only scratch sample preservation、control-point sanitisation、modless source gate 与 converted-star recompute。
2. [../../osu.Game.Rulesets.Mania.Tests/Mods/TestSceneManiaModAutoplay.cs](../../osu.Game.Rulesets.Mania.Tests/Mods/TestSceneManiaModAutoplay.cs)：锁住 ignore-only scratch 不阻塞列输入、autoplay 不再为 scratch/BGM sample 生成假按键、**长条 autoplay 完整按放**（原生 mania 与转谱共用 `ManiaAutoGenerator`，`canParticipateInAutoplay` nested-aware；`TestPerfectScoreOnShortHoldNote` + `TestAutoplayHoldsLongNoteAlongsideSampleOnlyObject`），以及相邻 mania autoplay correctness。
3. [../../osu.Game.Tests/Beatmaps/BmsStarRatingResolverTest.cs](../../osu.Game.Tests/Beatmaps/BmsStarRatingResolverTest.cs) 与 [../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterSortingTest.cs](../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterSortingTest.cs)：锁住 persisted converted star、version gate 与 current-ruleset resolved-star display，不得再回退到 raw BMS star。
4. [../../osu.Game.Tests/Visual/Navigation/TestScenePresentBeatmap.cs](../../osu.Game.Tests/Visual/Navigation/TestScenePresentBeatmap.cs) 与 [../../osu.Game.Tests/Visual/SongSelect/TestSceneSongSelectFiltering.cs](../../osu.Game.Tests/Visual/SongSelect/TestSceneSongSelectFiltering.cs)：当继续推进公开表面时，锁住 supported / unsupported source chart 的 presentation gate 与 convert visibility 不会误用 generic heuristics。

### K10：BMS -> mania 转谱星数导入期就绪与读取加固

状态：**两刀均已落地，58k+ 谱库实测验证三大症状（卡顿 / 排序错位 / 难度动态变化）完全消除**。
- **第一刀（A 导入期持久化）已落地**：`BeatmapDifficultyCache.EnsureConvertedStarRatingPersisted` + `BeatmapUpdater.Process()` 在导入期就计算并持久化 mania 转谱星。
- **第二刀（旧库回填 + carousel 读路径闭合）已落地**：基于真实 58k+ BMS 谱库实测迭代修了一组相互衔接的瓶颈——见 [CHANGELOG.md](CHANGELOG.md) 2026-05-28 "K10 第二刀" 条目。
- **B（读校验加固）经实测推迟**：本次实测进一步确认稳态下 LAV 在 RulesetStore 单实例共享下可靠同步（详见下方"方案评估"中 B 的实测结论）。
- **已知 follow-up**：carousel panel 星数 sprite-text 过渡动画（独立 UI 切片归 P1-A）、极端谱滚动卡顿（与 texture atlas 扩展相关）、Genngaozo 系列 >10s 转谱根因（未单独排查，以 Failed 绕开）。

#### 问题与根因

native 谱面星数在**导入期**由 [../../osu.Game/Beatmaps/BeatmapUpdater.cs](../../osu.Game/Beatmaps/BeatmapUpdater.cs) 的 `Process()` 算好并写入 `BeatmapInfo.StarRating`（realm 列），之后 Song Select 直接读这一个稳定字段，排序天然稳定。而 BMS -> mania 转谱星在导入期**不计算**（`Process()` 只把 `StarRating` 设成 BMS #PLAYLEVEL），它只在两处产生：

1. 下次启动批处理 `BackgroundDataStoreProcessor.populateMissingConvertedStarRatings`；
2. Song Select 首次在 mania 下查看时由 `BeatmapDifficultyCache` **异步**懒算。

因此大批量导入后、**同一会话内**切到 mania：这些谱没有持久化的转谱星 -> carousel `getEffectiveStarRatings` 读不到 -> 回退到 `BeatmapInfo.StarRating`（BMS playlevel，量纲完全不同）-> 异步 warmup 算出 mania 星后写回 -> **重排跳变**。重启后批处理补齐才稳定。读路径本身没问题（`TryGetCachedDifficulty` / `GetDifficultyAsync` 都先走 `tryGetImmediateDifficulty` 同步读 persisted 值）；问题纯粹是导入期没就绪 + 读校验对 `LastAppliedDifficultyVersion` 的脆弱耦合。

#### 方案评估（已选 A + B）

- **A 导入期持久化（核心）**：在 `BeatmapUpdater.Process()` 对 BMS 谱顺带算并持久化 mania 转谱星，镜像 native 星的导入期计算。直接消除同会话不稳定，无需重启。
- **B 读校验加固（配套，实测后推迟）**：读路径的 `difficulty_version` 校验本拟不再信任消费者传入、可能过期的 `LastAppliedDifficultyVersion`，改用权威当前 mania 计算器版本。**实测结论：当前不需要、暂不实施。** 经核查，carousel（`FilterCriteria.Ruleset`）、spread display（`ruleset.Value`）与 `BeatmapDifficultyCache.currentRuleset` 三处消费者用的都是**同一个** `RulesetStore.AvailableRulesets` detached 实例（全局 `Ruleset.Value` 由 `RulesetStore.GetRuleset/First` 赋值），而 `clearOutdatedStarRatings` 每次启动都会把该实例的 LAV 同步成当前计算器版本（realm 已持久化后续启动直接带正确值，版本升级当次也会就地更新同一实例）。`ManiaDifficultyCalculator.Version` 还是编译期常量（20241007），写入与读取天然一致。因此"消费者 LAV 过期"仅存在于启动后台处理尚未完成的瞬时、自愈窗口，不构成用户描述的持续症状。强行改读路径会给被多处消费的 read gate 引入风险却无实际收益，故推迟；仅当将来发现确有持有 LAV=0/过期实例的消费者时再实现。
- C realm 一等字段（否决）：需 realm 迁移，相对现有 JSON metadata 方案过度工程。
- D carousel 稳定回退占位（否决）：给不出正确值，治标不治本。

#### 设计落点

1. `BeatmapDifficultyCache` 新增 `[Resolved] IRulesetStore` 与公开同步方法 `EnsureConvertedStarRatingPersisted(BeatmapInfo)`：复用现有 per-beatmap 计算 + `BmsPersistedMetadataResolver.SetConvertedStarRating`/`SetConvertedStarRatingFailure` 与已统一的失败语义（确定不可转 -> 固化 Failed；瞬时异常 -> 不持久化）。target ruleset 经 `SupportsConvertedStarRatings` 选出（当前仅 mania）。
2. `BeatmapUpdater.Process()`：对 BMS 谱在既有 `beatmapSet.Realm!.Write` 事务内、设完 playlevel 星后调用 `difficultyCache.EnsureConvertedStarRatingPersisted(beatmap)`。**必须直接写入已在事务内的 live `beatmap.Metadata`，不得再嵌套 `realmAccess.Write`**（避免嵌套写事务）。整体 best-effort：转换/计算异常绝不能让导入失败。
3. `BackgroundDataStoreProcessor.populateMissingConvertedStarRatings` 改为复用同一 `EnsureConvertedStarRatingPersisted` 逻辑去重（保留批处理作为历史库 / 版本失效的兜底补算）。
4. B：`BmsPersistedMetadataResolver.tryGetCurrentConvertedStarRating` 的版本闸改为对照"权威当前 mania 计算器版本"。实现需避免每次读都新建计算器；候选：由 `BeatmapDifficultyCache` 在初始化时缓存一次 mania 计算器 `Version` 并提供给 resolver，或在 resolver 侧 memoize（实现细节在开工前二次确认，避免引入每读一次的难度计算开销）。

#### 涉及文件

- [../../osu.Game/Beatmaps/BeatmapDifficultyCache.cs](../../osu.Game/Beatmaps/BeatmapDifficultyCache.cs)（新方法 + `IRulesetStore`）
- [../../osu.Game/Beatmaps/BeatmapUpdater.cs](../../osu.Game/Beatmaps/BeatmapUpdater.cs)（`Process()` 内调用）
- [../../osu.Game/Beatmaps/BmsPersistedMetadataResolver.cs](../../osu.Game/Beatmaps/BmsPersistedMetadataResolver.cs)（B 读校验加固）
- [../../osu.Game/Database/BackgroundDataStoreProcessor.cs](../../osu.Game/Database/BackgroundDataStoreProcessor.cs)（去重复用，兜底保留）

#### 风险与缓解

1. **导入性能**：每张 BMS 导入多跑一次 mania 转换 + 难度计算。缓解：native 谱本就在导入期算星，量级对称；可在已有 current persisted 状态时跳过重算。
2. **realm 写事务时长 / 嵌套写**：难度计算放进导入写事务（native 星计算已有同样先例，line 69）。缓解：复用同一事务、直接写 live metadata，禁止嵌套 `realmAccess.Write`。
3. **DI 接线**：`BeatmapDifficultyCache` 新增 `IRulesetStore` 依赖，需确认 headless / test-scene 容器都已注册该依赖，否则 BDL 解析失败。缓解：开工首步先验证 test-scene 装配。
4. **B 改变失效语义**：必须继续保留 `conversion_version` + `difficulty_version` 双闸，仅把后者的对照源从消费者 LAV 换成权威版本。缓解：focused 回归先锁。
5. **异常安全**：导入绝不能因转换错误失败。缓解：best-effort 包裹；`BeatmapInvalidForRulesetException` -> 固化 Failed，其它异常 -> 仅日志。

#### 测试落点

1. 新增/扩展 `BeatmapUpdater` 导入后断言：BMS set 经 `Process()` 后，metadata 已带 current-version 的 converted star（成功）或 Failed（确定不可转）。
2. [../../osu.Game.Tests/Beatmaps/BmsStarRatingResolverTest.cs](../../osu.Game.Tests/Beatmaps/BmsStarRatingResolverTest.cs)：B 的读校验改用权威版本后，旧版本/失配仍判失效、当前版本判有效。
3. [../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterSortingTest.cs](../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterSortingTest.cs)：导入后首帧 resolved star 即为 mania 转谱星、不回退 playlevel；排序稳定。
4. Release + Debug build gate；`BmsToManiaBeatmapConverterTest` 等既有聚焦面不回归。

#### 验证顺序与回退边界

- 验证顺序：`BeatmapUpdater` 导入 focused proof -> resolver 版本闸 focused proof -> carousel 稳定排序 proof -> 既有转谱/autoplay 聚焦面 -> Release/Debug build。
- 回退边界：A 与 B 相互独立，可分刀落地（A 不依赖 B，B 不依赖 A）。新方法为增量；若关闭/回退，行为退回当前"启动批处理 + 懒算"路径，不破坏现有数据。

#### 开工定义

1. 确认 `IRulesetStore` 在 `BeatmapDifficultyCache` 宿主（含 test-scene）可解析。
2. 确认 B 的"权威 mania 计算器版本"获取方式不引入每读一次的难度计算开销。
3. 确认导入期 best-effort 包裹不改变现有导入失败/通知语义。

### K11：BMS -> mania 转谱 BGM / autoplay 音频补全与 LN 尾键音对齐

状态：converter 侧已落地（2026-06-01；BGM sample-only 补全 + LN 尾 node sample 置空，`BmsToManiaBeatmapConverterTest` 19/19、BMS 869/869、Release 0 错误）；dense-BGM 播放期性能与 player-level BGM 出声 proof 后置 P1-J J6

目标：补全 K9 转谱器丢失的 BGM（autoplay channel `0x01`）音频层，并把 LN 尾键音的 mania 行为对齐到 BMS 侧「长条只头发声」，使纯键音 BMS 的 mania 转谱音频与 BMS 原生模式一致；不引入第二套样本源，不改 K9 已冻结的 lane/stage/scratch/star/control-point 语义。

#### 背景与根因（已查实）

1. BGM 丢层：`BmsBeatmapConverter` 已把 autoplay 对象落成 `BmsBgmEvent` 并放入 `BmsBeatmap.HitObjects`；但 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaBeatmapConverter.cs) 的 `ConvertHitObject` switch 只有 `BmsHoldNote` / `BmsHitObject`，base `BeatmapConverter.convertHitObjects` 把非 `ManiaHitObject` 的 `BmsBgmEvent` 丢给 `ConvertHitObject` 后得空枚举 → BGM 整层丢弃。纯键音 BMS（`detectFullMusicFile` 未命中、`AudioFile` 仅 preview/空）因此在 mania 丢掉歌曲主体。
2. 暴露面真实：`AllowGameplayWithRuleset`（mania + `ShowConvertedBeatmaps`）+ `DrawableManiaRuleset.CreateDrawableRepresentation` 表明转谱会真实进入 mania 游玩并播 sample-only 对象，非仅 star 用途。
3. 样本源已 de-risk：[../../osu.Game/Beatmaps/WorkingBeatmapCache.cs](../../osu.Game/Beatmaps/WorkingBeatmapCache.cs) 的 `createResourceProvider` 按 `BeatmapSet.FilesystemStoragePath` 建 `FilesystemBackedBeatmapResourceProvider`，与游玩 ruleset 无关 → BGM 用同型 `BmsKeysoundSampleInfo` piggyback，无需新样本源。
4. 尾键音分歧：转谱器现把尾 keysound 放进 `NodeSamples[1]`，mania `TailNote` release 时会播放，与 P1-J 约束 3a 冲突。

#### 设计落点

1. 新增 sample-only 对象类型（建议 `BmsConvertedBgmSampleHitObject`，承 `IgnoreJudgement` + `HitWindows.Empty`，与 `BmsConvertedScratchSampleHitObject` 同族）与其 drawable（Alpha=0、`timeOffset>=0` 时 `PlaySamples()` + `ApplyMinResult()`）。
2. `BmsToManiaBeatmapConverter.ConvertHitObject` 加 `case BmsBgmEvent`：发 sample-only 对象，`Column = 0`，`Samples = createSamples(bgm.KeysoundSample)`，空 sample 跳过。
3. `isScorableHitObject` 一并排除新类型；确认不进 `TotalObjectCount` / `EndTimeObjectCount` / 难度计算输入。
4. drawable 工厂 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaDrawableRepresentationFactory.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaDrawableRepresentationFactory.cs) 的 `CanCreate` / `Create` 支持新类型。
5. LN 尾对齐：`ConvertHitObject` 的 `BmsHoldNote` 分支把 `NodeSamples[1]` 改为空列表（不再 `createSamples(holdNote.TailKeysoundSample)`），头 keysound 仍走 `NodeSamples[0]`。

#### 涉及文件

- 新增 `osu.Game.Rulesets.Bms/Objects/BmsConvertedBgmSampleHitObject.cs`、`osu.Game.Rulesets.Bms/UI/DrawableBmsConvertedBgmSampleHitObject.cs`
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaBeatmapConverter.cs)（BGM 分支 + 尾对齐 + `isScorableHitObject`）
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaDrawableRepresentationFactory.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaDrawableRepresentationFactory.cs)（新类型 drawable）

#### 测试落点

1. [../../osu.Game.Rulesets.Mania.Tests/BmsToManiaBeatmapConverterTest.cs](../../osu.Game.Rulesets.Mania.Tests/BmsToManiaBeatmapConverterTest.cs)：BGM 事件 → sample-only 对象计数 == 源 autoplay 事件数、不进 scorable / `TotalObjectCount`；BGM 空 sample 跳过；LN 转谱后 `NodeSamples[1]` 为空、`NodeSamples[0]` 仍为头 keysound；star/统计不受 BGM 影响。
2.（可选）mania player-level：BGM 跨事件时间出声 / seek 语义。

#### 验证顺序与回退边界

- 验证顺序：converter focused（BGM 计数 / 不 scorable / 尾 node sample 为空）-> 难度/统计不回归 -> （可选）mania player-level BGM 出声 -> Release/Debug build。
- 回退边界：BGM 补全与尾对齐均为增量、可分刀；回退即退回当前"BGM 丢层 + 尾发声"行为，不破坏 K9 既有数据。dense-BGM 播放性能归 P1-J J6，不阻塞本切片功能正确性。

#### 开工定义

1. 确认 mania 实跑一张纯键音 BMS 转谱：note keysound 出声（基线对照）、BGM 静音（待修）。
2. 确认新 sample-only 类型经 `AffectsCombo() == false` 且**无 combo-affecting 嵌套对象**透传，autoplay key 生成 / note-lock 自动跳过（同 scratch sample-only）。注意谓词须 nested-aware：长条因有 combo 嵌套头尾**不**被跳过，新 sample-only 类型因无嵌套才被跳过，二者区分点是「有无 combo 嵌套对象」而非单看顶层 `AffectsCombo`（见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md) #12）。

## 明确不做

1. 不借本专题改写 `chartbms/` / `chartmania/` 存储拓扑、外部谱库扫描或难度表 metadata contract；这些仍归 `P1-H`。
2. 不借本专题继续处理 gameplay runtime 每帧热路径、shared audio pool 或 dense autoplay hitch；这些仍归 `P1-J`。
3. 不在当前阶段承诺完整 BGA / 特效谱播放实现；首轮只要求解析层保数、typed slot 预留与 projection 可消费。
4. 不允许 importer、Song Select、gameplay 或未来视觉层继续各自维护长期存在的 ad hoc text parser。
5. 不把 `P1-K` 写成泛化兼容愿望单；只有已经识别出 authority gap 或消费面会直接受影响的语义才进入本子线。

## 当前优先顺序

1. 先冻结 raw snapshot、normalized chart model 与 consumer projection 的三层术语，并把当前 parse gap 写成稳定 authority。
2. 先做 `K1` / `K2`，把 no-loss coverage 与 source-order-aware raw model 建起来，再继续扩 typed event surface。
3. 在 `K3` 冻结时间轴与 control-event 顺序前，不继续扩大 consumer 侧对特殊谱面语义的分散处理。
4. `K4` 的 projection reuse 现已先服务 static background metadata / import / playfield consumer；后续优先延伸到 raw working beatmap、Song Select 与 statistics，再考虑更广视觉/特效消费面。
5. 任何缓存或性能动作都排在 focused semantics validation 之后，避免先缓存一套还没冻结的语义。
6. 当前若继续 post-`K8` 工作，默认优先级固定为 `K9`：先完成 explicit public wording 与更宽的 presentation/manual proof；source mapping、sample-only scratch runtime、autoplay ignore contract 与 persisted converted star 已不再回退为“待开工”。
7. `K10`：A（导入期持久化）已落地；B（读校验加固）经实测推迟。后续若发现确有持有过期 LAV 的消费者再重开 B；当前剩余 follow-up 是 BeatmapUpdater 导入期端到端集成回归（OsuGameTestScene 级装配，与既有 visual scene CLI discover 不稳的现状一致）。
8. `K11`：converter 侧已落地（BGM sample-only 补全 + LN 尾 node sample 置空，`BmsToManiaBeatmapConverterTest` 19/19、BMS 869/869、Release 0 错误）；剩余 dense-BGM 播放期性能与 player-level BGM 出声 proof 交 P1-J J6。
