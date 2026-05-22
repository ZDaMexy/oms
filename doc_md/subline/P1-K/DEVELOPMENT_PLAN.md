# P1-K 开发计划：BMS 解析链路治理

> 最后更新：2026-05-22
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
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs) 当前只持有 `BeatmapInfo`、`ChannelEvents`、`ObjectEvents`、`LongNoteEvents`、`BpmChangeEvents`、`StopEvents` 与 `Warnings`，尚未形成 raw snapshot + normalized chart model 的双层 authority。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs) 已具备 WAV/BMP/BPM/STOP tables、静态背景字段、`LNOBJ/LNTYPE` 与 keymode；但还没有 `ScrollTable`、BGA config、PlayerMode 或 unknown header bag 这类扩展保留槽位。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs) 已负责时间轴、control points 与 hitobject conversion；当前同拍位 `BPM/STOP/object` 顺序、signed BPM 语义与部分 long-note/visual event 边界仍未冻结成显式合同。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs) 与 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs) 已证明 projection reuse 的价值：imported raw wrapper 可以直接复用首次 conversion 结果，原始 chart 文件也继续保留在文件系统存储中。
- 当前 parse chain 仍保留了一部分 forward-compatible 缓解面：大多数十六进制轨道还能通过 raw `ChannelEvents` 回收，原始 chart 文件没有丢；但这不能替代 decode model 自身的保数能力。
- 当前已确认的 parse gap 包括：`SCROLLxx/SC` 不进模型、signed BPM typed surface 不可表示、duplicate channel line 未 compound、同拍位 `BPM/STOP/object` 顺序未冻结、BGA layer / mine / invisible note 仍缺最薄 typed slot。
- 本子线的外部语义参考基线固定为 [hitkey BMS 命令参考](https://hitkey.nekokan.dyndns.info/cmds.htm) 与 [bmson specification](https://bmson-spec.readthedocs.io/)。若实现与当前文档冲突，必须先更新其一再继续开发。

## 首轮执行包

### 文件级切片图

| 切片 | 目的 | 主文件 | 相邻文件 | 首轮测试落点 | 当前退出条件 |
| --- | --- | --- | --- | --- | --- |
| `K1-A` raw carrier | 建立 raw snapshot 与 source-order carrier | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsChannelEvent.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsChannelEvent.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs) | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs) | [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs) | raw snapshot additive 落地，现有 converter/importer 无需跟改 |
| `K1-B` unknown bag / scroll placeholder | 让 `SCROLLxx/SC`、unknown header/definition 进入模型 | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs) | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs) | [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs) | `SCROLLxx/SC` 与 unknown bag 可回收，但 consumer 仍可暂不消费 |
| `K2-A` signed BPM / duplicate line | 保留 negative BPM 与 duplicate channel compound 语义 | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBpmChangeEvent.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBpmChangeEvent.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs) | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsChannelEvent.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsChannelEvent.cs) | [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs) | signed BPM 进入 typed model，duplicate line 有 source-order-aware compound 行为 |
| `K3-A` timeline semantics | 冻结 `BPM/STOP/object` 顺序与最小 long-note 表达 | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs) | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsStopEvent.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsStopEvent.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsLongNoteEvent.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsLongNoteEvent.cs) | [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapConverterTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapConverterTest.cs) | converter 语义锁定，consumer 不再各自猜同拍位顺序 |
| `K4-A` projection reuse | 让 raw wrapper / import / Song Select 共享同一 projection | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedBeatmap.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedBeatmap.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapLoader.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapLoader.cs) | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs)、[../../osu.Game.Rulesets.Bms/UI/BmsBackgroundLayer.cs](../../osu.Game.Rulesets.Bms/UI/BmsBackgroundLayer.cs) | [../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs)、[../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapStatisticsTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapStatisticsTest.cs) | raw working beatmap / importer / background consumer 不再触发 second conversion |

### 切片执行纪律

1. `K1-A` 到 `K3-A` 首轮只允许触碰 in-memory parse chain；未到 `K4-A` 前，不改 persisted metadata、Song Select UI 或 gameplay runtime。
2. 任何切片若同时需要 parser 和 consumer 改动，必须先拆成 model-first，再做 consumer follow-up；不允许在同一刀里同时改 decode truth 和多个消费面。
3. `K1-B` 完成前，不允许任何 consumer 依赖 `SCROLL` 或 unknown bag；`K3-A` 完成前，不允许任何 consumer 依赖新的同拍位顺序假设。
4. `K4-A` 只处理 projection reuse，不负责新增播放能力；若背景层或未来视觉层需要新 typed event，也必须先完成 `K1-K3`。
5. `K5` 缓存工作必须后置到 `K4-A` 之后；任何还没通过 focused regression 的语义不得先做 cache 固化。

### 首轮测试落点与建议新增测试面

1. [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapDecoderTest.cs)：首轮承接 `SCROLLxx/SC`、unknown bag、signed BPM、duplicate channel line、source line order 等 parser 级回归。
2. [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapConverterTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapConverterTest.cs)：承接同拍位 `BPM/STOP/object` 顺序、STOP duration、long-note minimal expression 与 initial BPM fallback 的 converter 回归。
3. [../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs)：承接 raw wrapper / imported projection reuse、source retention、import 后 consumer 不再 second conversion 的 integration proof。
4. [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapStatisticsTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapStatisticsTest.cs)：若 `K4-A` 触碰 timing/statistics projection，优先在这里补 Song Select / statistics 侧 proof。
5. [../../osu.Game.Rulesets.Bms.Tests/TestSceneBmsSongSelectDifficultyTable.cs](../../osu.Game.Rulesets.Bms.Tests/TestSceneBmsSongSelectDifficultyTable.cs)：只有当 `K4-A` 确实影响 Song Select projection 消费时才补相邻场景回归；首轮不提前扩大到 UI 层。
6. 若现有测试文件开始混入过多互不相干语义，优先新增 dedicated focused tests，而不是继续把 `BmsBeatmapDecoderTest` 和 `BmsBeatmapConverterTest` 写成巨型回归桶。

### 推荐验证命令

1. parser focused：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "FullyQualifiedName~BmsBeatmapDecoderTest"`
2. parser + converter focused：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "(FullyQualifiedName~BmsBeatmapDecoderTest|FullyQualifiedName~BmsBeatmapConverterTest)"`
3. projection focused：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "(FullyQualifiedName~BmsImportIntegrationTest|FullyQualifiedName~BmsBeatmapStatisticsTest)"`
4. 全量 BMS 回归：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal`
5. Release build gate：`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m`

### 依赖与回退边界

| 切片 | 进入前提 | 失败信号 | 允许回退 | 明确禁止 |
| --- | --- | --- | --- | --- |
| `K1-A` raw carrier | 当前 decoder focused suite 维持全绿，且新增 carrier 仍是 additive | 现有 converter/importer 因字段接线或排序副作用回归 | 只回退新增 carrier 的可见性或接线，不回退 source-order capture 的测试定义 | 为了保编译或旧测试继续让 source line order 丢失 |
| `K1-B` unknown bag / scroll placeholder | `K1-A` 已稳定，raw carrier 可承接 unknown/scroll 数据 | 新字段要求 persisted metadata、UI 或 runtime 立即消费 | 保留 raw/unknown bag 入口，推迟 consumer 消费与持久化扩散 | 把 `SCROLLxx/SC` 再次降回 warning-only 或文件系统兜底 |
| `K2-A` signed BPM / duplicate line | `K1-B` focused suite 全绿，unknown/scroll 已可保数 | signed BPM 进入 typed model 后导致 converter/runtime 语义冲突；duplicate compound 破坏既有排序假设 | 保留 sign/raw compound authority，推迟 converter 对 sign 的进一步消费 | 把 negative BPM 强行归一成正值，或在 consumer 侧补 ad hoc 覆盖逻辑 |
| `K3-A` timeline semantics | parser focused suite 全绿，且 control-event 语义已有 focused case 锚点 | `BPM/STOP/object` 顺序调整导致 converter focused case 或既有 integration case 回归 | 只在 converter authority 内修正顺序与时间推进；必要时保留新字段但暂缓新 projection 输出 | 在 Song Select、gameplay 或 visual consumer 各自加特判来掩盖 converter 语义问题 |
| `K4-A` projection reuse | `K1-K3` 已冻结且 full BMS suite 通过 | projection stale、invalidation 不清或 importer/raw-wrapper consumer 发生 second conversion 回归 | 回退到 parse-chain 内单一 projection authority，允许暂时不复用新增 projection，但不得恢复 consumer-local second parse | 为了局部 consumer 方便重新引入第二套 parser 或第二套 conversion authority |

补充规则：

1. 任何切片一旦触发失败信号，下一步必须先回到该切片自己的 focused validation，不允许直接跳到更宽的 suite 试图“碰运气过线”。
2. 回退只允许收缩新增暴露面、消费面或复用面，不允许回退已经建立的 no-loss carrier 与 focused regression 定义。
3. 若某切片的唯一可行回退已经越过 `P1-K` authority，必须停下并改写文档分线，而不是在同一实现里继续硬推。

### 开工定义

满足下面四条后，`P1-K` 就可以直接按文档开工，而无需再补口头规划：

1. 先按 `K1-A -> K1-B -> K2-A -> K3-A -> K4-A` 的顺序推进，不并刀。
2. 每刀只改文件级切片图里列出的 primary files；相邻文件只在编译或测试明确要求时补入。
3. 每刀完成后先跑对应 focused command，再决定是否进入下一刀。
4. 任一切片若需要跨到 `P1-H` persisted metadata 或 `P1-J` runtime hot path，就停止并把该变动拆回对应子线，不在 `P1-K` 里硬推。

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

状态：未开始

目标：让 decode 阶段具备“已实现能力可 typed 消费、未实现能力仍可完整回收”的稳定模型。

建议交付：

1. 为 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs) 明确 raw snapshot 与 typed collections 的职责分层，避免 `ChannelEvents` 既承担 fallback 又承担唯一 authority。
2. 为 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs) 增加扩展保留面；首轮至少要为 `ScrollTable`、`PlayerMode`、BGA config 或等价 unknown header/definition bag 留出 stable slot。
3. 为 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs) 增加 source-order-aware raw event surface，保留 channel token、raw value、measure、fraction 与 source line order。
4. 首轮不要求所有新字段立刻被 consumer 使用，但要求 decode 阶段不再直接丢掉未来可能需要的语义数据。

### K2：header / definition / channel coverage

状态：未开始

目标：把当前已经确认会丢数据的 header、indexed definition 与 channel token 收口为 no-loss coverage。

建议交付：

1. 明确处理 `SCROLLxx/SC`、`PLAYER`、`POORBGA`、未来 unknown header 与 indexed definition 的保存策略。
2. 为 duplicate channel line 增加 source-order-aware compound / overwrite pass，而不是继续按“逐条展开后排序”当成长期语义。
3. 让非十六进制 channel token 的保留与 typed projection 有明确边界，不能继续因为 parser 入口假设而直接忽略整条数据。
4. 继续保留 raw channel fallback；typed coverage 只负责给已确认重要的语义建立稳定消费面。

### K3：timeline / control-event 语义冻结

状态：未开始

目标：让时间轴语义由 converter 单独拥有，避免 consumer 各自重新推导 `BPM/STOP/object` 顺序与 long-note 边界。

建议交付：

1. 冻结同拍位 control-event 顺序，至少把 `BPM -> STOP -> object` 写成单一 authority；涉及视觉滚速的 control-event 也必须拥有明确阶段位置。
2. 让 signed BPM 进入 typed model；即使当前 runtime 只按绝对值推进时间，也不得在 parse / convert 层直接抹掉方向信息。
3. 明确 `LNTYPE 2`、特殊 long-note encoding 与未来 visual-event 的最小表达，避免“只 warning、不建模”成为长期状态。
4. `BmsBeatmapConverter` 只消费 normalized chart model，不允许 consumer 侧为同一份 chart 再次推导时间轴。

### K4：parse-once / project-many 调用复用

状态：未开始

目标：把 importer、raw working beatmap、Song Select、gameplay、背景/特效层与统计分析统一到同一份 parse 结果或等价缓存上。

建议交付：

1. 延续 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs) 当前已经建立的 projection reuse 方向，不再让 raw wrapper、Song Select 与 gameplay 各自触发 second conversion。
2. 为“哪些 projection 可以 persisted、哪些 projection 只应 runtime/lazy materialize”建立边界，避免 data layer 与 consumer cache 混写。
3. 让未来视觉层、背景层或特效谱层优先消费 typed visual event / projection，而不是直接在 render 期遍历 raw `ChannelEvents`。
4. 继续保留原始 chart 文件作为现场诊断与兜底资源，但它不再是 consumer 正常工作的主要 authority。

### K5：解析侧性能与缓存合同

状态：未开始

目标：把 parse-side 性能工作限定在“减少重复 decode / normalize / project”的范围内，而不是扩大成 runtime 热路径治理。

建议交付：

1. 建立稳定的 source identity 与 projection invalidation 规则，明确何时可复用、何时必须重建。
2. 对 timing、metadata、visual event、statistics 等 projection 建立按需 materialize 策略，而不是一次性生成所有 consumer 结果。
3. 明确 importer、Song Select、preview 与 gameplay 之间的 parse cache 边界，减少同一 chart 在单次会话内的重复解析。
4. 任何 cache / perf work 都不得改变 parse semantics；语义变化必须先有 focused regression 再谈缓存。

### K6：focused validation 与特效谱前置支持

状态：未开始

目标：在真正实现未来视觉/特效消费前，先让 parse-layer correctness 与 projection reuse 有自动化证明。

建议交付：

1. decoder 层 focused tests：header/definition coverage、duplicate channel line、signed BPM、`SCROLLxx/SC`、unknown bag 与 raw snapshot 保留。
2. converter 层 focused tests：同拍位 control-event 顺序、STOP duration、long-note encoding、typed visual event minimal projection。
3. import/raw-wrapper focused tests：projection reuse、source retention、Song Select/raw consumer 复用 timing data。
4. 当未来开始实现视觉层、背景层或特效谱播放时，新的 player-level / visual tests 必须消费 `P1-K` 的 typed surface，而不是临时重 parse 文本。

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
4. `K4` 的 projection reuse 先优先服务 importer、raw working beatmap、Song Select 与 gameplay；未来视觉/特效消费面作为后续同一 authority 的延伸。
5. 任何缓存或性能动作都排在 focused semantics validation 之后，避免先缓存一套还没冻结的语义。
