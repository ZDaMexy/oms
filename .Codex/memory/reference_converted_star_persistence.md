---
name: reference-converted-star-persistence
description: BMS→mania converted star 的存储、版本、计算过滤与大库地雷
metadata:
  node_type: memory
  type: reference
---

# Converted star 持久化召回

权威约束：[P1-K CONSTRAINTS](doc_md/subline/P1-K/TECHNICAL_CONSTRAINTS.md)；历史：[P1-K CHANGELOG](doc_md/subline/P1-K/CHANGELOG.md)。

## 存储与版本

- `BeatmapMetadata.RulesetDataJson` 中的 `BmsPersistedMetadataData.converted_star_ratings["mania"]` 保存 star、difficulty version、conversion version、failed。
- 有效读取要求 conversion version 与 mania difficulty version 同时匹配。
- converter 行为变化只 bump BMS conversion version；对 native mania 可证 no-op 时不要 bump mania calculator version。
- 多子系统共享 `RulesetData`，写 DTO 必须 `[JsonExtensionData]` round-trip 未知字段，避免难度表/构成/星数互擦。

## 计算合同

- mania difficulty 输入只保留自身或 nested judgement `AffectsCombo` 的对象；sample-only BGM/scratch 不进 strain/max combo。
- regression 必须运行真实 `ManiaDifficultyCalculator`；只断言 `TotalObjectCount` 不能证明 difficulty input 干净。
- converter 不计算 star；唯一 authority 是 `ManiaDifficultyCalculator` + difficulty cache。
- 零 scorable 对象的转谱抛 `BeatmapInvalidForRulesetException`，作为版本内 sticky Failed；瞬时 IO 等错误不持久化 Failed。

## 写入/读取路径

- import-time：现有 Realm transaction 内 best-effort 持久化。
- lazy：difficulty cache 首次需要时计算并持久化。
- batch：版本失效/旧库启动补算，分块并行计算、单线程 Realm 批量写。
- read：先 persisted immediate，再 memory cache，最后异步计算；Failed 立即回退 BMS playlevel，避免重复慢算。

## 大库地雷

1. Realm link-traversal predicate 可能报错或静默零结果；先 materialize，再用 `IsBmsBeatmap` 客户端过滤。
2. `DifficultyCalculator.Calculate()` 无 token 时有内部 10s timeout；batch/import 的 OCE 对同谱通常是确定失败，lazy 用户取消则不是。
3. `CacheNullValues=false`：null/failed 不会被 memory cache，必须靠 persisted Failed 的同步 fallback 阻止重复计算。
4. 5万级不要为每谱创建 async lambda/task；sync-first 收集 misses，只对 miss `WhenAll`。
5. worker 不碰 Realm；并行度保留 UI 核心，batched write 减少竞争。
6. `parsedDataCache` 写后驱逐旧 JSON key，避免每次 metadata mutation 留一份缓存。

K12 曾证明 sample-only 污染可把 scratch-dense star 抬高一倍以上；以后任何新 sample-only 类型都必须同时审查 scorable、combo 和 difficulty 三条链。
