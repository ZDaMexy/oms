# P1-J 开发进度：BMS gameplay runtime 性能与音频时序治理

> 最后更新：2026-05-16
> 主线全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。本文件只记录 `P1-J` 的真实进展。

## 当前阶段

- **阶段定位**：`P1-J` 已完成归线与文档建档，当前仍处于 planning-only 阶段；还没有进入生产代码修补。
- **代码状态**：shared `BmsKeysoundStore` 当前继续作为 BGM / note / LN / lane replay 的统一播放池，但 gameplay 链上仍保留无条件 `Schedule()`、按键热路径容器枚举、重复 sample 数组分配，以及 live channel resize rebuild-all 的风险点。
- **文档状态**：`P1-J` 四件套已建立，主线总规划、主线状态页、主线 changelog 与子线索引已同步到“新开独立子线，而不是并入 `P1-C` / `P1-E`”的口径。

## 已确认事实

- `BmsPlayfield.KeysoundStore` 是当前 shared keysound pool owner；`DrawableBmsHitObject` 和 `BmsLane` 都通过它播放样本。
- `BmsKeysoundStore.Play()` 当前先 materialize 样本数组，再无条件 `Schedule()` 到后续帧执行播放。
- `BmsLane.shouldTriggerEmptyPoor()` 当前每次按键都会 materialize 候选数组；`BmsOrderedHitPolicy.getParticipatingHitObjects()` 也会枚举容器对象。
- `DrawableBmsHitObject.PlaySamples()` 与 `BmsKeysoundStore.Play()` 之间存在重复 `ToArray()`；`BmsLane.playCurrentLaneKeysound()` 还会额外 new 单元素数组。
- `KeysoundConcurrentChannels` 当前默认值为 `32`；live 改值会 rebuild channel container，现有合同允许切断当前播放中的样本。
- `BmsDrawableRulesetTest` 现已锁住 late-empty-poor 行为；`P1-J` 任何优化都必须以“不回归这些语义”为前提。
- 当前同轮审查**没有**新增证据证明 `BmsSoloPlayer` 的 pre-start / start-sequence 主音乐 handoff 仍有新的 gameplay bug，因此 `P1-J` 当前不把它列为首轮重点。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 子线归线与四件套建档 | 已完成 | `P1-J` 已正式建立 |
| `J1` keysound timing hardening | 未开始 | 已确认 `Schedule()` 播放延后风险 |
| `J2` lane / ordered-hit hot path 收口 | 未开始 | 已确认 `BmsLane` / `BmsOrderedHitPolicy` 容器枚举风险 |
| `J3` sample allocation tightening | 未开始 | 已确认双重 `ToArray()` 与单元素数组分配 |
| `J4` live channel reconfigure safety | 未开始 | 已确认 rebuild-all 可能切断当前音频 |
| `J5` focused validation / dense-chart checklist | 未开始 | 现阶段只有只读审查，没有新增自动化验证 |

## 当前风险

- **线程假设风险**：若直接移除 `BmsKeysoundStore` 的 `Schedule()`，可能暴露现有调用者对 thread-affinity 的隐含依赖；需要先把 same-frame playback 与跨线程安全边界分清。
- **语义回归风险**：`BmsLane` / `BmsOrderedHitPolicy` 的性能优化若落在错误 abstraction，最容易回归的是 empty-poor、late-empty-poor 与 judge-family-specific lane 行为。
- **配置行为风险**：`KeysoundConcurrentChannels` 若继续宣称“runtime 改值立即生效”，但实现仍是 rebuild-all，就会持续保留无提示硬切音频的 UX 风险。
- **范围膨胀风险**：`P1-J` 若不坚持 hot-path authority，很容易被扩成“整个 BMS 都要优化”的无边界愿望单。

## 下一检查点

1. 在 `BmsKeysoundStore` 上做 same-frame playback 的第一刀方案设计，并补 owner-level focused test 落点。
2. 确定 `BmsLane` / `BmsOrderedHitPolicy` 的候选缓存或 next-object authority 归属，不让容器枚举继续作为 gameplay 默认热路径。
3. 补一条 config -> drawable ruleset -> playfield shared store 的 focused binding 覆盖，为 `J4` 提前建测试锚点。

## 验证记录

- 2026-05-16：完成只读代码审查，范围覆盖 `BmsKeysoundStore`、`DrawableBmsHitObject`、`DrawableBmsHoldNote`、`BmsLane`、`BmsOrderedHitPolicy`、`BmsPlayfield`、`DrawableBmsRuleset` 与 `BmsSoloPlayer` 的相关链路；本轮仅完成规划与归线，无新增自动化测试执行。