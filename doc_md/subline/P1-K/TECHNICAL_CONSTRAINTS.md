# P1-K 技术约束：BMS 解析链路治理

> 最后更新：2026-05-23
> 本文件记录 `P1-K` 的硬约束。若实现与本文冲突，先修正文档或代码其中一边，再继续开发。

## 归线约束

1. 本子线属于 Phase 1.x 下的 `P1-K`；主 authority 是 decoder、normalized chart model、converter 语义、projection reuse 与 parse-side cache，不得回写成 `P1-H`、`P1-J` 或 `P1-E` 的主线任务。
2. `P1-H` 只承接 storage / importer / reuse / persisted metadata 的从属影响；不得再长出第二套 parse semantics。
3. `P1-J` 只承接 gameplay runtime hot path、shared audio pool 与 dense-chart 播放期性能；不得为 autoplay、keysound 或 visual runtime 路径再长出第二套 parser。
4. `P1-E` 只承接真实谱面 checklist 与人工验校；不得把“某张真实 chart 看起来能跑”当成替代 parse authority 的依据。
5. `P1-C` 只有在新的 parse event 直接驱动 feedback / judgement family 时才记录从属影响；默认不拥有 parse-chain authority。

## 解析 authority 约束

1. `BmsBeatmapDecoder`、`BmsDecodedChart`、`BmsBeatmapInfo`、`BmsBeatmapConverter` 是当前 parse-chain 的单一 authority；consumer 不得各自维护长期存在的 ad hoc text parser。
2. “当前尚未实现播放/显示”不是允许在 decode 阶段丢数据的理由；未消费语义必须保留在 raw snapshot、typed placeholder 或等价扩展槽位中。
3. 原始 `.bms/.bme/.bml/.pms` 文件的 filesystem retention 只是诊断与兜底手段，不得被当成 decode model 可以继续丢数据的替代方案。
4. 本子线的外部语义参考固定为 [hitkey BMS 命令参考](https://hitkey.nekokan.dyndns.info/cmds.htm) 与 [bmson specification](https://bmson-spec.readthedocs.io/)；若实现与参考冲突，必须先明确文档或兼容策略，再继续开发。

## raw / typed model 约束

1. raw source snapshot 至少必须保留：measure、fraction、channel token、raw value、source line order，以及必要时的原始 header / definition key。
2. typed model 可以只为已确认重要的语义建立最薄 surface，但不得通过“typed 未使用”把 raw carrier 一并删掉。
3. unknown header、unknown indexed definition 与 unknown channel event 必须有显式 bag、typed placeholder 或等价保留槽位；不能继续默默跳过。
4. `SCROLLxx` 与 `SC` 轨道不得继续在 parser 入口直接丢失；首轮至少要进入 raw snapshot，并为后续 typed projection 预留稳定位置。
5. duplicate channel line 必须保留 source line order，并具备 compound / overwrite 语义；不得继续把“逐条展开后按 measure/fraction/channel 排序”当成长期合同。
6. signed BPM 必须能进入 typed model；即使当前时间推进只使用绝对值，方向信息也必须可恢复。
7. `LNTYPE 2`、future long-note encoding 与未实现视觉事件必须至少能通过 typed placeholder 或 raw snapshot 被可靠回收；“只 warning、不建模”不能成为长期状态。
8. BGA base / layer / poor、mine、invisible note 与类似视觉事件，必须至少拥有最薄 typed descriptor 或明确的 projection slot；不得长期只停留在 consumer 自行猜 channel 的状态。

## 切片边界约束

1. `K1-A` 到 `K3-A` 首轮只允许改动 in-memory parse chain：`BmsBeatmapDecoder`、`BmsDecodedChart`、`BmsBeatmapInfo`、event records 与 `BmsBeatmapConverter`；不得提前改 persisted metadata、Song Select UI 或 runtime hot-path。
2. `K4-A` 之前，`BmsImportedBeatmapFactory`、`BmsBeatmapLoader`、`BmsFolderImporter`、`BmsBackgroundLayer` 只允许因编译依赖做最小适配，不得在未冻结 parse semantics 的情况下承诺新消费行为。
3. parse model 的新增字段首轮必须是 additive；不得 repurpose 现有字段含义，也不得让旧字段在不同 consumer 下语义漂移。
4. parse model 新字段默认只存在于 transient snapshot / projection；除非 `K4` follow-up 明确需要，否则不得自动扩写到 persisted metadata contract 并把 `P1-H` 一并拖入同一刀；`BeatmapInfo.Metadata` 上既有的 derived projection 只能复用或回填，不得借 consumer 之名改写第二套 authority。
5. 若某个 consumer 需求同时要求 parser 变更和 UI/runtime 变更，必须拆成“先保数 / 后消费”的两刀；不得把 parse semantics 变化埋在 UI 或 runtime 改动里一起提交。

## 回退约束

1. 失败后的首选回退只能是“收缩新增暴露面”，不能把已经建立的 no-loss carrier、source line order 或 focused regression 一并删除。
2. 若 `signed BPM`、duplicate line compound 或 typed visual placeholder 的首轮消费无法稳定，允许把消费层回退到未启用状态；不允许把 parse authority 回退成“继续丢数据”或“继续只保 warning”。
3. `K3-A` 若暴露出 control-event 顺序问题，只能在 `BmsBeatmapConverter` authority 内修正；不得在 Song Select、gameplay、背景层或未来视觉层各自补局部顺序特判。
4. `K4` follow-up 若 projection reuse 导致 stale cache 或 invalidation 不清，允许暂时停留在 parse-chain 内单一 projection authority；不得为此恢复 consumer-local second parse 或 second conversion。
5. 若某次回退已经需要修改 `P1-H` persisted contract、`P1-J` runtime hot-path 或 `P1-A` UI authority，说明当前切片拆分失败，必须先停下重写文档分线，再继续实现。

## 时间轴与 converter 约束

1. 同拍位 control-event 顺序必须由 converter 单独拥有并显式冻结；至少要保证 `BPM -> STOP -> object` 的顺序是单一 authority。
2. `STOP`、measure length、default BPM 与 long-note timeline 边界不得由不同 consumer 各自重新推导。
3. 任何对 BPM 的时间推进计算都不得 silently 抹去原始 sign 信息；若 runtime 当前只支持正向推进，也必须把 sign 单独保留在模型里。
4. `BmsBeatmapConverter` 只消费 normalized chart model；Song Select、gameplay、背景层或未来特效层不得绕过它自行从 raw text 再算一次 timing。

## consumer / projection reuse 约束

1. importer、raw working beatmap、Song Select、gameplay、背景层、未来特效层与统计分析必须共享同一份 parse snapshot 或等价 cache；不得让每个消费面各自 ruleset conversion 或二次 parse。
2. `BmsImportedBeatmapFactory` 与 `BmsFolderImporter` 可以写 persisted projection，但不得因此拥有第二套 parse authority。
3. raw working beatmap consumer 可以复用 import 阶段得到的 timing/hitobject/metadata projection；不得再触发 second parser 作为默认显示路径。
4. runtime visual/effect layers 必须消费 typed visual event 或显式 projection；不得在 render/update 时直接遍历未冻结语义的 raw `ChannelEvents` 充当长期实现。
5. 任何 derived metadata 若从 parse snapshot 派生，必须有单一写入 authority 与明确 invalidation 规则；不得一边 persisted、一边 runtime cache 各算各的。
6. `osu.Game` 侧的 BMS display/read-model consumer 不得反向依赖 `osu.Game.Rulesets.Bms`；若需要 persisted BMS `chart_metadata`，必须共享单一 core-side typed projection helper，不得让每个 consumer 各自手拆 `RulesetDataJson` / `JObject.SelectToken("chart_metadata...")`。
7. Song Select 中暴露 BMS 谱师的 sort/group/filter selector 必须共享 `BeatmapLocalMetadataDisplayResolver.GetDisplayCreator()`；不得继续把 `Metadata.Author.Username` 当成 legacy BMS 的唯一 creator authority，或在 sorting / grouping / matching 各自维护一套 fallback 提取逻辑。
8. Song Select 中暴露 BMS 曲师的 sort/group/filter selector，以及 shared beatmap-attribute display 这类本地 metadata consumer，必须共享 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()` / `GetDisplayCreator()`；不得继续直接暴露 raw `Metadata.Artist` / `Metadata.ArtistUnicode` / `Metadata.Author.Username`。
9. gameplay loading / metadata display consumer 也必须共享 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()` / `GetDisplayCreator()`；不得在 `BeatmapMetadataDisplay` 或相邻 gameplay metadata surface 内继续直接展示 raw local artist / creator。
10. results / ranking metadata display consumer 也必须共享 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()` / `GetDisplayCreator()`；不得在 `ExpandedPanelMiddleContent` 或相邻 results metadata surface 内继续直接展示 raw local artist / creator。
11. profile beatmap metadata display consumer 也必须共享 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()`；不得在 `DrawableProfileScore`、`DrawableMostPlayedBeatmap` 或相邻 profile metadata surface 内继续直接展示 raw local artist。
12. menu / now-playing metadata display consumer 也必须共享 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()`；不得在 `SongTicker`、`NowPlayingOverlay` 或相邻 menu metadata surface 内继续直接展示 raw local artist。
13. online play creator display consumer 也必须共享 `BeatmapLocalMetadataDisplayResolver.GetDisplayCreator()`；不得在 `DailyChallengeIntro`、`DrawableRoomPlaylistItem` 或相邻 online play creator surface 内继续直接展示 raw `Metadata.Author.Username`。
14. 若 online play creator surface 需要 user link，只有在 `BeatmapLocalMetadataDisplayResolver.HasLinkedCreatorProfile()` 为真时才允许保留 link；fallback creator 只能显示为纯文本，不得伪造可点击用户资料。
15. matchmaking round-results score consumer 在构造 `ScoreInfo` 使用的 beatmap 时，若本地 `BeatmapInfo` 可按 `OnlineID` 命中则必须优先复用；不得无条件把 `APIBeatmap` 重建成最小壳覆盖 persisted `chart_metadata`，否则会绕开 `ExpandedPanelMiddleContent` 已接好的 local metadata display authority。
16. beatmap skin metadata consumer 也必须共享 `BeatmapLocalMetadataDisplayResolver.GetDisplayCreator()`；不得在 `LegacyBeatmapSkin` 或相邻 beatmap skin metadata surface 内继续直接展示 raw `Metadata.Author.Username`。
17. `IBeatmapInfo` title display consumer 也必须共享 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()` / `GetDisplayCreator()`；不得在 `BeatmapInfoExtensions.GetDisplayTitleRomanisable()` 或相邻 full-beatmap title display surface 内继续直接拼 raw artist / creator。
18. daily challenge title display consumer 在能拿到具体 `BeatmapInfo` 时也必须优先共享 full-beatmap title authority，并显式禁用 difficulty name；不得让 `DailyChallengeIntro` 或相邻 daily challenge title surface 继续直接走 `beatmap.BeatmapSet!.Metadata.GetDisplayTitleRomanisable(false)`。
19. scoped beatmap-set title display consumer 在能拿到具体 `BeatmapInfo` 时也必须优先共享 full-beatmap title authority，并显式禁用 difficulty name；不得让 `FilterControl.ScopedBeatmapSetDisplay` 或相邻 set banner surface 继续直接读 `BeatmapSetInfo.Metadata.GetDisplayTitleRomanisable()` 暴露 raw `/obj:` suffix 或把难度名带进 set 级标题。
20. delete confirmation title display consumer 在能拿到具体 `BeatmapInfo` 时也必须优先共享同一 set-level title authority，并显式保持“无 creator suffix + 无 difficulty name”的既有合同；不得让 `BeatmapDeleteDialog` 或相邻 delete confirmation title surface 继续直接走 `beatmapSet.Metadata.GetDisplayTitleRomanisable(false)`。

## 解析侧性能约束

1. `P1-K` 只处理 parse-side 性能：减少重复 decode、normalize 与 projection；不得借此处理 runtime 每帧热路径或音频 mixer 压力。
2. 不允许 importer、Song Select、preview、gameplay 因为“方便”而重复读取同一 chart 文本并重新解析。
3. cache 必须以稳定的 source identity 为前提，并在 source change、config 变更或 projection schema 变化时具备明确 invalidation 规则。
4. expensive projection 应优先采用 lazy materialization 或按需缓存；不得在没有 consumer 的情况下预先生成全部投影结果。
5. 任何 parse-side cache / perf work 都不得先于语义冻结落地；没有 focused regression 的缓存优化不允许成为长期合同。

## 测试与发布约束

1. 至少补齐三层 focused coverage：decoder、converter、import/raw-wrapper；不要等到播放层开始消费新语义后再补底层测试。
2. 新的 typed placeholder、visual event surface 或 timeline 语义，必须先有 focused regression，再允许 player-level 或 UI 层使用。
3. Release build 继续是子线门槛；本专题不能以“只是数据结构与文档治理”为理由绕过 build gate。
4. 真实谱面 acceptance 与人工 checklist 继续后置到 `P1-E` / `P1-G`；但不得把自动化缺口全部甩给人工验收。
5. 任何改变 parse semantics、projection ownership 或 cache authority 的实现，都必须同步更新本目录四件套以及 `../../mainline/DEVELOPMENT_PLAN.md`、`../../mainline/DEVELOPMENT_STATUS.md`、`../../mainline/CHANGELOG.md`。
6. 第一轮执行必须遵守“focused parser -> focused converter -> focused projection -> full BMS suite -> Release build”这一验证顺序；在更窄的 executable proof 可用时，不得只看 diff 或只依赖人工推理。
