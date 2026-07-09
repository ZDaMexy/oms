# P1-K 当前状态：BMS 解析与转换治理

> 最后更新：2026-07-10（Skin V1 topology 审查补两项风险；功能状态未改变）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。格式参考见 [BMS_FORMAT_REFERENCE.md](../../other/BMS_FORMAT_REFERENCE.md)。

## 当前阶段

K1–K12 主体已阶段性收口：解析 authority、主要控制事件、projection reuse、BMS→mania 转换与 converted-star 修正均已落地。当前不继续扩写 parser 大改，剩余工作由真实特殊谱、公开表面 wording 和更宽人工证明触发。

## 已落地能力

- raw carrier + typed model 双层保留：未知 header/channel 不因 typed consumer 缺席而静默丢失。
- signed BPM、duplicate channel compound、同拍位 `BPM → STOP → object`、LNTYPE 2 最小表达。
- BGA/invisible/mine/scroll 等 visual/control typed surface 与 consumer projection。
- parse-once/project-many：metadata、background、Song Select、statistics、results 等复用 parse authority。
- source-bound modless playable cache 与 invalidation；results/score consumer 使用 already-modded playable contract。
- dedicated BMS→mania converter：sample-only BGM/scratch、LN tail 静音、converted star 持久化和展示 read-model。
- LNOBJ 只与同 lane 紧邻前一普通音符配对，禁止 LIFO 回抓制造重叠 LN。
- converted-star 难度入口过滤 sample-only BGM/scratch，并以 conversion version 失效旧结果。

## 不可破坏的边界

- decoder/normalized model 是解析唯一 authority；consumer 不得各自 ad hoc 重读原始 BMS。
- sample-only 对象可留在 `HitObjects` 用于播放，但不得进入 scorable/star/max-combo 语义。
- persisted metadata 多个子系统共享 `RulesetData` 时必须保留未知 JSON 字段，禁止 whole-object clobber。
- display-only 标题/难度清理不改存库原值和源文件 MD5。
- 转谱器不自行计算 mania 星级；星级归 `ManiaDifficultyCalculator`/difficulty cache。

## 当前验证

- 2026-07-10 BMS 全量 **1005/1005**。
- parser/converter/metadata 的分项历史数字和反证样本只保留在 [CHANGELOG.md](CHANGELOG.md)。

## 当前风险

- 特殊 long-note、极端控制流和少见 header family 仍可能暴露 typed consumer 缺口。
- `buildLaneKeysoundTimelines()` 当前以 `GetKeyCount()` 而非 `GetLaneCount()` 过滤 lane，5K/7K 最右键与 14K 右侧末键/Scratch2 的 armed/invisible timeline 可能被丢；已由 2026-07-10 代码审查确认，待独立修复切片。
- sparse 7K/9K chart 未使用高位 channel 时可能被 keymode 启发式低估；须补来源诊断/显式纠正入口，layout/skin 不得自行二次猜测。
- public wording/展示证明不完整不等于 parser 错误；先区分存储、projection 与 presentation owner。
- 任何 parser 改动都可能同时影响 native BMS、转谱 mania、统计、筛选、BGA 与缓存失效，必须跑全量。

## 下一检查点

1. 以最小切片把 lane timeline 上界改为 lane count，补 5K/7K/9K/14K visible/invisible/edge/scratch 测试并交 P1-J 实机验收。
2. 冻结 keymode 来源/override/诊断，补 sparse 5K/7K/9K 反例。
3. 收口 BMS→mania 显式入口 wording 与更宽 presentation/manual proof。
4. 仅由真实谱证据驱动 special LN/control-event follow-up，并同步格式参考与约束。
5. 继续保持 parser/converter focused + BMS full gate；涉及转谱时加 mania relevant focused。
