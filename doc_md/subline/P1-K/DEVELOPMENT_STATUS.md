# P1-K 开发进度：BMS 解析链路治理

> 最后更新：2026-05-22
> 主线全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。本文件只记录 `P1-K` 的真实进展。

## 当前阶段

- **阶段定位**：`P1-K` 当前处于 planning-only；`K0` 文档建线已完成，`K1-K6` 尚未进入代码落地。当前重点不是立即扩写播放期功能，而是先把 parse-chain 的 authority、已知 gap 与执行顺序冻结下来。
- **代码状态**：当前 parse-chain 已具备可工作的主链：`BmsBeatmapDecoder` 负责 decode / parse，`BmsDecodedChart` 与 `BmsBeatmapInfo` 充当中间模型，`BmsBeatmapConverter` 负责时间轴与 hitobject conversion，`BmsImportedBeatmapFactory` 则已证明 projection reuse 的价值。与此同时，当前仍存在几类未冻结 gap：`SCROLLxx/SC` 不进模型、signed BPM typed surface 不可表示、duplicate channel line 未 compound、同拍位 `BPM/STOP/object` 顺序未按参考语义锁定，BGA layer / mine / invisible note 也仍缺最薄 typed slot。
- **验证状态**：本轮没有新增生产代码或测试执行；`P1-K` 的基线结论来自同日只读审查与当前主线已验证快照。最近一次主线 build / test 基线仍为 `dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过，以及 `dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal` **788/788** 通过。
- **文档状态**：`P1-K` 四件套、主线总规划、主线状态页、主线变更日志与子线索引已同步到当前建线口径。

## 已确认事实

- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs) 当前已覆盖文本解码、header 解析、部分 indexed definition 与一部分 typed event 构造，但 parser 入口仍以现有 channel / header 假设为主。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs) 当前仍把 raw `ChannelEvents` 与 typed event collections 混放在同一层，没有明确的 raw snapshot / normalized chart model 分层。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs) 已具备静态背景字段、keysound/bitmap/bpm/stop tables 与 long-note 基础字段，但还没有 `ScrollTable`、BGA config 或 unknown header/definition bag。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs) 当前先记录 event time 再处理同拍位 `BPM/STOP`；这意味着 converter 语义仍未对齐当前参考基线。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs) 当前已把首次 conversion 得到的 timing / hitobject projection 回写给 raw wrapper，这证明 parse-once/project-many 是现有代码已经可行的方向。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs) 当前会保留 `LocalFilePath`，原始 chart 文件继续留在 filesystem storage；这是一条缓解项，但不是 decode model 可继续丢数据的理由。
- 当前多数十六进制轨道仍可经由 raw `ChannelEvents` 回收；但 `SCROLLxx/SC` 由于 parser 入口限制不进入模型，属于明确的数据保留缺口。
- signed BPM、duplicate channel line compound、BGA layer / mine / invisible note 最薄 typed slot 与同拍位 control-event 顺序，当前仍未形成稳定 authority；未来播放、显示、性能优化与特效谱支持都应以这些缺口为起点，而不是继续让 consumer 各自补口。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 子线归线与四件套建档 | 已完成 | `P1-K` 已正式建立 |
| `K0` 归线、术语与基线冻结 | 已完成 | 主线/子线文档与当前 parse gap 已同步 |
| `K1` raw / typed 双层模型 | 未开始 | 当前仍缺 raw snapshot 与 normalized chart model 的显式分层 |
| `K2` header / definition / channel coverage | 未开始 | `SCROLLxx/SC`、duplicate line compound 与 unknown bag 待收口 |
| `K3` timeline / control-event 语义冻结 | 未开始 | 同拍位 `BPM/STOP/object`、signed BPM 与 long-note encoding 待冻结 |
| `K4` parse-once / project-many 复用 | 未开始 | imported raw wrapper 已有基础，更多 consumer 复用边界待定义 |
| `K5` 解析侧缓存与性能合同 | 未开始 | source identity、projection invalidation 与 lazy materialization 待定义 |
| `K6` focused validation 与特效谱前置支持 | 未开始 | decoder / converter / importer focused suite 仍待补齐 |

## 当前风险

- **authority 混线风险**：如果不尽快把 parse truth 与 projection ownership 写死，`P1-H`、`P1-J`、Song Select、gameplay 与未来视觉层都可能继续长出 second parser 或 second conversion。
- **保数缺口风险**：当前 raw chart file retention 与 raw `ChannelEvents` 只能缓解一部分问题；`SCROLLxx/SC` 这类完全不进模型的语义仍会直接丢失。
- **converter 语义风险**：同拍位 `BPM/STOP/object` 顺序、signed BPM 与 long-note encoding 若不先冻结，后续 focused tests 与 consumer 行为都会继续漂移。
- **typed surface 缺口风险**：BGA layer / mine / invisible note 等未来视觉事件若长期只停留在 raw fallback，未来特效谱支持将被迫在 consumer 侧重新猜 channel 语义。
- **cache 前置风险**：在 raw/typed authority 尚未稳定前过早引入 parse cache，只会把当前未冻结的语义错误固化下来。

## 首轮开工包

1. **第一刀 `K1-A`**：只改 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedChart.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsChannelEvent.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsChannelEvent.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs)，目标是建立 raw snapshot carrier 与 source line order。完成后先跑 `BmsBeatmapDecoderTest` focused suite。
2. **第二刀 `K1-B`**：继续限定在 decoder/model，补 `SCROLLxx/SC` 与 unknown header/definition bag 的 no-loss entry，不触碰 consumer。完成后仍先跑 `BmsBeatmapDecoderTest`。
3. **第三刀 `K2-A`**：补 signed BPM typed surface 与 duplicate channel line compound；完成后跑 parser focused，再决定是否进入 converter。
4. **第四刀 `K3-A`**：只改 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs) 与紧邻 event records，冻结同拍位 control-event 顺序与最小 long-note 表达。完成后跑 `BmsBeatmapConverterTest`。
5. **第五刀 `K4-A`**：只在 `K1-K3` 全绿后，才触碰 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapLoader.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapLoader.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedBeatmap.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedBeatmap.cs) 与相邻 importer/raw-wrapper consumer。完成后跑 `BmsImportIntegrationTest` 与 `BmsBeatmapStatisticsTest`。

## 当前文档完备性结论

- `P1-K` 现已不再缺“如何开工”的实施信息：切片顺序、主文件、focused tests、验证命令、边界约束与失败回退合同都已具备。
- 后续若仍要继续改文档，应该只在实现推进后做事实同步，而不是再补一轮抽象规划；当前文档层面已经足以独立驱动 `K1-A` 开工。
- 仍然保留的开放项只属于实现期决策，不再属于规划缺口，例如具体字段命名、最薄 typed placeholder 形状与测试命名颗粒度。

## 下一检查点

1. 把 `K1-A` 的字段级目标写死到实现注释或测试命名中：source line order、unknown bag、raw token retention 三项缺一不可。
2. 先补 parser focused regressions，再进入 converter；未通过 `BmsBeatmapDecoderTest` 的 parser 切片不得推进到 `K3-A`。
3. 只有在 `K3-A` 通过后，才评估 importer、raw wrapper、Song Select 与 gameplay 还存在哪些 second conversion / second parse，并决定 `K4-A` 是否需要扩到 `BmsBackgroundLayer` 或统计 consumer。

## 验证记录

- 2026-05-22：完成 `P1-K` 文档建线与主线同步。当前结论来自只读审查，范围覆盖 `BmsBeatmapDecoder`、`BmsDecodedChart`、`BmsBeatmapInfo`、`BmsBeatmapConverter`、`BmsImportedBeatmapFactory` 与 `BmsFolderImporter` 的 parse → convert → import 主链，并结合外部语义参考确认 `SCROLLxx/SC`、signed BPM、duplicate channel line compound、同拍位 control-event 顺序与 typed visual event 预留槽位仍属当前主要 gap。本轮无生产代码改动、无新增测试执行；build / test 基线沿用主线同日快照。
