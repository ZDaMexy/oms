# P1-K 技术约束：BMS 解析链路治理

> 最后更新：2026-06-01（新增 K11：BMS→mania 转谱 BGM/autoplay 音频补全约束）
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
4. 本子线的外部语义参考固定为 [hitkey BMS 命令参考](https://hitkey.bms.ms/cmds.htm) 与 [bmson specification](https://bmson-spec.readthedocs.io/)；上述外部规范的结构化归纳、跨实现差异与解析审查对照清单收口在 [../../other/BMS_FORMAT_REFERENCE.md](../../other/BMS_FORMAT_REFERENCE.md)，应优先以该文对照实现。若实现与参考冲突，必须先明确文档或兼容策略，再继续开发。

## raw / typed model 约束

1. raw source snapshot 至少必须保留：measure、fraction、channel token、raw value、source line order，以及必要时的原始 header / definition key。
2. typed model 可以只为已确认重要的语义建立最薄 surface，但不得通过“typed 未使用”把 raw carrier 一并删掉。
3. unknown header、unknown indexed definition 与 unknown channel event 必须有显式 bag、typed placeholder 或等价保留槽位；不能继续默默跳过。
4. `SCROLLxx` 与 `SC` 轨道不得继续在 parser 入口直接丢失；首轮至少要进入 raw snapshot，并为后续 typed projection 预留稳定位置。
5. duplicate channel line 必须保留 source line order，并具备 compound / overwrite 语义；不得继续把“逐条展开后按 measure/fraction/channel 排序”当成长期合同。BGM channel `0x01` 是 compound 的强制例外：同位多条 `#xxx01` 是并行 keysound 层，必须全部保留，不得被同位去重折叠成一个。
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
5. 从 BPM 推导 beat length 不得把合法的亚 1 BPM（规范允许低至约 0.014）钳成 1；下界只用于避免零/近零 BPM 产生非有限值。`TimingControlPoint.BeatLength` 受框架 bindable `[6,60000]` 钳制只影响显示滚动，不得反过来用作对象时序的钳制依据。

## 键音呈现与控制流约束（2026-05-29 链路审查后冻结）

1. BGM（channel `0x01`）同位叠层不得被去重：解析层必须让同 measure/fraction 的多条 `#xxx01` keysound 全部进入 `ObjectEvents` 并各自生成 `BmsBgmEvent`。
2. 空键 / 误击的轨 keysound 必须由 `BmsBeatmapConverter` 在转换期构建的 per-lane keysound 时间线（`BmsBeatmap.LaneKeysoundTimelines`）单一驱动；该时间线必须涵盖可见音符、long-note 头/尾与不可见对象（channel `31-49`）的 keysound，并按时间排序。`BmsLane` 只能消费该时间线（at-or-before 二分解析、开局前回退首条目），不得回退到基于判定事件（`NewResult`）的 ad hoc “last judged” keysound，也不得让每个 lane 各自从 hitobjects 再算一套。
3. 不可见对象（channel `31-49`）属“已解码但此前未消费”语义：现固定经 `channel-0x20` 映射回对应可见 lane 并进入 keysound 时间线；不得再在 decode 后被静默丢弃。
4. `#RANDOM` 的确定性合同冻结为“仅执行 `#IF 1` 分支”并保留既有告警；`#SETRANDOM n` 必须按作者固定值选支。`#IF`/`#ELSEIF`/`#ELSE` 必须按 chain 语义求值（命中后续分支短路），不得让 `#ELSE` 内容在 `#IF` 已命中时泄漏。
5. `#SWITCH`/`#SETSWITCH` 必须做确定性单段选择（默认 `#CASE 1` 或 `#SETSWITCH` 固定值，C 风格 fall-through 至 `#SKIP`，无匹配走 `#DEF`，支持嵌套）；不得退回“把所有 `#CASE` 内容无条件并入”导致错谱。
6. 解析期对 LNOBJ 头的回收不得使用每条长条一次的 O(n) 线性扫描（全 LNOBJ 谱会退化为 O(n²)）；必须用索引标记 + 单次重建等 O(n) 路径。

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

## K9：BMS -> mania 单向转谱约束

1. `K9` 只允许支持 `BMS -> mania` 单向转谱；当前冻结的 source/target 矩阵只有 `5K+1S -> mania 5K`、`7K+1S -> mania 7K`、`9K_Bms -> mania 9K`、`9K_Pms -> mania 9K` 与 `14K+2S -> mania 14K`。不得把 `mania -> BMS`、generic all-ruleset convert 或其它 keymode 扩张混进同一切片。
2. `K9` 继续属于 `P1-K` 的 parse / conversion authority：它必须消费现有 `BmsBeatmapDecoder`、`BmsDecodedChart`、`BmsBeatmapInfo`、`BmsBeatmapConverter` 与等价 BMS playable projection；不得为转谱再长出第二套 text parser，也不得把现有 `ShowConvertedBeatmaps` / `AllowGameplayWithRuleset()` generic heuristics 当成 conversion semantics authority。
3. BMS -> mania 的 lane flatten 必须使用 canonical BMS lane contract，而不是当前用户 `PlayfieldStyle`、scratch 视觉侧别或皮肤布局。相同 source chart 的 mania 转谱结果必须与本地单机布局偏好无关；非 scratch playable lanes 的 flatten 结果固定为“移除 scratch lane 后保留 canonical 顺序并重新从左到右编号”。
4. target stage definition 必须写死在 `K9`：`5K+1S/7K+1S/9K_Bms/9K_Pms` 只能生成单 stage `5/7/9` 列，`14K+2S` 只能生成 dual-stage `7 + 7`。不得把 `14K` 临时实现成单 stage `14` 列，也不得让左右半侧 object 跨 stage 漂移。
5. 当前 `ManiaBeatmapConverter` 的 pass-through quantisation 会把 `IHasXPosition.X` 视为 `0..512` 横向空间；因此不得直接把 `BmsHitObject.IHasXPosition = LaneIndex` 当成 `K9` 的输入合同。实现必须先建立 BMS lane -> normalized mania-space 的专用投影，再进入 mania column quantisation 或等价 helper。
6. target keycount 不保留 scratch 作为独立 judged column authority，但 scratch-family object 也不得被静默丢弃。scratch tap / hold 都必须保留原 keysound 与 head-tail sample 语义，并以 sample-only、`IgnoreJudgement` / empty-hitwindow 的 converted mania object 保留时间线；不得再回退成可判定 `Note` / `HoldNote` 并入真实列，也不得重新进入 combo、statistics 或 star-rating authority。若需要 `Column`，它只能作为同 side / 同 stage 的 drawable/sample anchor，不得重新变成 judged-lane merge 语义。
7. `PMS` 继续视为无 scratch lane 的 9K source path：它不进入 scratch sample-only 分支，也不允许为了统一实现而额外制造 fake scratch 列。
8. `K9` 首轮只允许消费 modless source BMS chart。`BmsModMirror`、`BmsModRandom`、`A-SCR`、`A-NOT`、`Autoplay` 与其它 BMS runtime mod 都不得参与转谱映射、validity 或目标列决定；若未来要支持 source-side mod-aware conversion，必须另起后续切片。
9. 若 source chart 在 flatten / degrade 后得不到任何可游玩 mania object，则该 conversion result 必须判定为 invalid，并从 presentation / Song Select surface 隐藏；不得为了“显示转谱”按钮可见而产出空壳结果。
10. modless converted mania star 必须持久化到 `BeatmapMetadata.RulesetDataJson` 的 BMS payload 中，并至少携带 target ruleset、difficulty version 与 conversion version；不得覆盖 `BeatmapInfo.StarRating`，因为该字段仍是 source BMS raw star / playlevel authority。
11. current-ruleset 的 converted-star display surface（carousel selector、spread display、cache warmup/backfill）在 `BMS -> mania` 场景下都必须走 resolved converted-star lookup；raw `BeatmapInfo.StarRating` 只继续服务 source BMS surface，不得在 target-ruleset display 中回流。
12. converted autoplay / replay generation 必须在 press/release schedule 与同列 next-object lookup 两侧都跳过 ignore-only scratch / BGM sample object；这些对象可以继续播放 sample，但不得生成假 auto 输入，也不得扰动真实 judged column 的 key-up 时机。**实现谓词必须 nested-aware**：判定一个对象是否参与 autoplay 时，须检查其自身**或任一嵌套对象**的 `MaxResult.AffectsCombo()`（对齐 `OrderedHitPolicy.canParticipateInLocking` 的同源语义），不得只看顶层对象自身的 `MaxResult`。因为 mania `HoldNote` 自身是 `IgnoreJudgement`（`IgnoreHit`，不计 combo），combo 落在其嵌套 `HeadNote`/`TailNote`——只看顶层 `AffectsCombo` 会把**所有长条**误跳过，而 sample-only 对象因**无任何嵌套对象**仍被正确排除。注意 `ManiaAutoGenerator` 是原生 mania 与 BMS→mania 转谱**共用**的 mania-core 生成器，此处任何过broad的过滤会同时回归原生 mania 长条 autoplay；回归守卫为 `TestSceneManiaModAutoplay`（`TestPerfectScoreOnShortHoldNote` + `TestAutoplayHoldsLongNoteAlongsideSampleOnlyObject` 锁长条参与、`TestAutoplayIgnoresSampleOnlyScratchObjects` 锁 sample 跳过）。
13. `K9` 不得把 mania keycount-changing mods、dual-stage 分裂或其它 runtime mod surface 当成 source keymode 适配的替代方案；首轮 target stage definition 只由 source keymode matrix 决定。
14. `K9` 与公开产品表面拆刀：在同一实现中若已经触及顶层入口、按钮文案、ruleset switch 或 generic convert visibility，则停止并拆到 `P1-A`；`P1-K` 只拥有 conversion semantics、validity 与 focused proof。
15. BMS `SCROLLxx/SC` 经 `BmsBeatmapConverter` 写入的 `EffectControlPoint.ScrollSpeed` 是 BMS 引擎专属视觉滚动语义，不得在 `BmsToManiaBeatmapConverter` 中沿用为 mania 的 `EffectControlPoint`/SV 语义；mania 转谱后的 `ControlPointInfo` 只保留 timing 边界（`TimingControlPoint`），不传递 `EffectControlPoint`。若未来需要把 BMS scroll 语义映射到 mania 视图，必须另起后续切片，并在 K9 约束中显式更新本条。
16. BMS `STOP` 在 `BmsBeatmapConverter` 中以"冻结滚动"形式写入 `ControlPointInfo` 的 timing 边界时，必须用 dedicated `TimingControlPoint` 子类（如 `BmsStopFreezeTimingControlPoint`）做类型级标记，并由 `BmsToManiaBeatmapConverter.createConvertedControlPointInfo` 按类型剥离；不得用 sentinel `BeatLength` 值做标记，因为 mania `TimingControlPoint.BeatLengthBindable` 的合法范围下界（`MinValue = 6`）与极端高 BPM 真实谱面存在不可分辨碰撞。

## K10：转谱星导入期就绪与读取加固约束（规划中）

1. 导入期持久化只允许写 `BeatmapMetadata.RulesetDataJson` 的 converted-star payload；继续禁止覆盖 `BeatmapInfo.StarRating`（仍是 source BMS playlevel authority）。
2. 导入期计算必须复用现有 `BMS -> mania` 转换链与 `BmsPersistedMetadataResolver` 持久化合同及已统一的失败语义（确定不可转 -> 固化 Failed；瞬时异常 -> 不持久化待重试）；不得为导入期再造第二套转换或星数计算路径。
3. 导入期持久化必须在 `BeatmapUpdater.Process()` 既有 realm 写事务内直接写 live metadata，**不得嵌套 `realmAccess.Write`**；且整体 best-effort，转换/计算异常绝不能让导入失败或改变现有导入通知语义。
4. 启动批处理 `populateMissingConvertedStarRatings` 必须保留为历史库与版本失效的兜底补算，不得因导入期持久化落地而删除。
5. 读校验加固（B）必须继续保留 `conversion_version` 与 `difficulty_version` 双闸，仅允许把后者的对照源从消费者传入的 `LastAppliedDifficultyVersion` 换成权威当前计算器版本；不得借此弱化或移除任一版本闸。
6. B 的权威版本获取不得引入"每次读一次难度计算"的开销；必须 memoize / 缓存为每构建一次的常量级读取。
7. `K10` 为中高风险切片：A（导入期持久化）与 B（读校验加固）必须可分刀独立落地与回退；任一刀落地前先补 focused 回归，禁止只凭 diff 或人工推理跳过 build/test gate。

## K11：BMS -> mania 转谱 BGM / autoplay 音频补全约束（2026-06-01 落地，converter 侧）

> 背景：`K9` 的 `BmsToManiaBeatmapConverter` 当前只把玩家可击打对象（note / LN / scratch sample-only）的键音搬到 mania；BGM（autoplay channel `0x01`）经 `BmsBeatmapConverter` 落成 `BmsBgmEvent` 后进入 `BmsBeatmap.HitObjects`，但 `ConvertHitObject` 的 switch 无 `BmsBgmEvent` 分支，base `BeatmapConverter.convertHitObjects` 将其作为非 `ManiaHitObject` 丢给 `ConvertHitObject` 后得空枚举 → 整层 BGM 被静默丢弃。对纯键音 BMS（无完整 master 音轨，`AudioFile` 仅 preview/空），这等于 mania 转谱丢掉歌曲主体（鼓/贝斯/铺底/人声等非可击打层）。详见 [CHANGELOG](CHANGELOG.md) 2026-06-01 与 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) K11 节。

1. BGM（`BmsBgmEvent`）在 `BMS -> mania` 转谱中不得继续被静默丢弃；必须作为 sample-only、`IgnoreJudgement` / empty-hitwindow 的 converted mania object 保留 autoplay 键音时间线，与 scratch sample-only（K9 #6）同族对待——不进 combo、statistics、star-rating、autoplay key 生成与 note-lock。
2. BGM sample-only 对象只承载 keysound 播放，不得映射到任何 judged column 语义；其 `Column` 只能作为 drawable/sample anchor（默认锚到 column 0），不得因 column 选择改变判定列或 stereo 语义。
3. converted BGM 对象不得进入 `TotalObjectCount` / `EndTimeObjectCount` / `isScorableHitObject` / 难度计算输入；`scorableHitObjects` 口径必须与现有 scratch sample-only 排除规则一致（统一通过 `isScorableHitObject` 过滤，新类型一并排除），不得让 BGM 撑大 mania 转谱的判定物数量或星数输入。
4. converted BGM 的样本解析不得新建第二套 sample 源：它与 note keysound 同走 `BmsKeysoundSampleInfo`（`useBeatmapSamples`）→ 经 `WorkingBeatmapCache` 的 `FilesystemBackedBeatmapResourceProvider`（按 `BeatmapSet.FilesystemStoragePath` 建、与游玩 ruleset 无关）解析 `chartbms/` 内 WAV。因此 BGM 补全是 piggyback；不得借此引入 mania 专用 keysound store 或预解码 player（dense-BGM 播放期性能归 `P1-J` J6，见其 TECHNICAL_CONSTRAINTS 第 10 条）。
5. LN 尾键音 mania 对齐：`BmsToManiaBeatmapConverter` 不得把 LN 尾 keysound 放进 mania `HoldNote.NodeSamples[1]`（mania `TailNote` 会在 release 播放该 node sample，与 BMS 侧「LN tail 一律不发声」即 `P1-J` 约束 3a 冲突，对 LNTYPE1 尾复用头 WAV 的谱会复现 double）。尾 node sample 必须为空列表；头 keysound 仍走 `NodeSamples[0]`。**scratch 长条尾同理**：`createScratchSampleHitObjects` 只发 head sample 对象，不得再为尾单独发 sample-only 对象——scratch 长条尾对象常复用头 WAV，发出即 double（实测 GOODBOUNCE 人声 scratch 长条 "stomp your fee feet"）。
6. 本切片只补音频保真，不得顺手改 K9 已冻结的 lane flatten / stage definition / scratch sample-only / converted-star / control-point 剥离语义；若发现这些需要联动改动，先停下拆刀。
7. 验证顺序固定为「converter focused（BGM 计数 / 不 scorable / 尾 node sample 为空）-> 难度/统计不回归 focused -> mania player-level BGM 出声 / seek 语义（可选）-> Release build」；在 converter focused proof 可用时不得只靠 generic convert UI 手测代替。

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
7. `K9` 的第一轮验证顺序固定为“focused mapping / sample-preservation proof -> autoplay ignore-only proof -> selector/resolver focused proof -> `PresentBeatmap` / Song Select focused proof -> Release build”；mapping proof 必须同时锁住 `14K -> 7+7` dual-stage 形态、sample-only scratch 语义与 source-side modless gate。在这些更窄 proof 可用时，不得直接拿 generic convert UI 手测代替。
