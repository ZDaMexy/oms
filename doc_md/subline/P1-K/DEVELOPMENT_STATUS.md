# P1-K 当前状态：BMS 解析与转换治理

> 最后更新：2026-08-30（P1-A C3 的 P1-K Skin 前置闭合）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。格式参考见 [BMS_FORMAT_REFERENCE.md](../../other/BMS_FORMAT_REFERENCE.md)。

## 当前阶段

K1–K12 主体已阶段性收口：解析 authority、主要控制事件、projection reuse、BMS→mania 转换与 converted-star 修正均已落地。P1-A C3 所需的 P1-K Skin 前置（keymode authority、全 lane armed timeline 与 mod 后键音/LaneId 一致性）也已闭合。该结论只关闭 C3 的解析/转换前置，不代表整条 P1-K 完成；公开表面 wording、真实特殊谱与更宽人工证明仍按本线继续。

## 已落地能力

- raw carrier + typed model 双层保留：未知 header/channel 不因 typed consumer 缺席而静默丢失。
- signed BPM、duplicate channel compound、同拍位 `BPM → STOP → object`、LNTYPE 2 最小表达。
- BGA/invisible/mine/scroll 等 visual/control typed surface 与 consumer projection。
- parse-once/project-many：metadata、background、Song Select、statistics、results 等复用 parse authority。
- source-bound modless playable cache 与 invalidation；results/score consumer 使用 already-modded playable contract。
- dedicated BMS→mania converter：sample-only BGM/scratch、LN tail 静音、converted star 持久化和展示 read-model。
- LNOBJ 只与同 lane 紧邻前一普通音符配对，禁止 LIFO 回抓制造重叠 LN。
- converted-star 难度入口过滤 sample-only BGM/scratch，并以 conversion version 失效旧结果。
- immutable `BmsKeymodeResolution` 由 parser 单点产出并原样流经 converter、production loader 与 gameplay layout owner：显式 override、`.pms/.bme`、P2/high channel 与完整 channel-set 的 precedence、evidence、纠正入口及稳定脱敏 diagnostic 已冻结；无充分证据或证据冲突时 fail-closed，不再按最高出现 channel、hit object 或 layout 宽度猜测。
- `LaneKeysoundTimelines` 的 canonical 上界已改为 `GetLaneCount()`，覆盖 5K/7K 最右键、9K 全 lane、14K K14/Scratch2，以及 visible note、LN head/tail armed entry、invisible object 与相邻 mine；layout/skin/runtime 只消费 parser/converter 投影，不重读 BMS 或二次推导 lane 数。
- Mirror/RANDOM/R-RANDOM/custom 的对象、mine 与 armed timeline 共用同一 exact permutation；S-RANDOM 因无单一列置换而稳定禁用受影响 armed timeline、保留对象自身 WAV。玩家/autoplay、native BMS 与 converted Mania 已在 production host 证明进入同一 shared keysound store 并实际请求发声，post-mod 对象、keysound 与 skin lookup 汇合到同一 `LaneId`；未改 sample pool、判定或 binding。

## 不可破坏的边界

- decoder/normalized model 是解析唯一 authority；consumer 不得各自 ad hoc 重读原始 BMS。
- sample-only 对象可留在 `HitObjects` 用于播放，但不得进入 scorable/star/max-combo 语义。
- persisted metadata 多个子系统共享 `RulesetData` 时必须保留未知 JSON 字段，禁止 whole-object clobber。
- display-only 标题/难度清理不改存库原值和源文件 MD5。
- 转谱器不自行计算 mania 星级；星级归 `ManiaDifficultyCalculator`/difficulty cache。

## 当前验证

- 2026-08-30 C3 P1-K 前置最终证据：decoder/converter **176/176**、projection **24/24**、BMS sound **14/14**、converted Mania **2/2**；格式化后关键 BMS 路径 **235/235**、BMS Skin **802/802**、BMS full **1763/1763**，Release **0 error**。
- 上述数字只证明 P1-K parser/converter/keysound 前置及其回归；C3 的唯一 layout、全 production consumer 与 revision protocol 总证据由 [P1-A STATUS](../P1-A/DEVELOPMENT_STATUS.md) 统一承载。逐项测试与边界见 [CHANGELOG.md](CHANGELOG.md)。

## 当前风险

- 特殊 long-note、极端控制流和少见 header family 仍可能暴露 typed consumer 缺口。
- 新出现的稀有扩展名/channel family 若无法由现有 evidence 无歧义归类，仍会按合同 fail-closed，需要显式 override 或新的 parser 证据；不得在 layout/runtime 增补猜测兜底。
- public wording/展示证明不完整不等于 parser 错误；先区分存储、projection 与 presentation owner。
- 任何 parser 改动都可能同时影响 native BMS、转谱 mania、统计、筛选、BGA 与缓存失效，必须跑全量。

## 下一检查点

1. 收口 BMS→mania 显式入口 wording 与更宽 presentation/manual proof。
2. 仅由真实谱证据驱动 special LN/control-event follow-up，并同步格式参考与约束。
3. 新 keymode evidence 只允许在 parser authority 内 additive 扩展，并保持无证据/冲突 fail-closed。
4. 继续保持 parser/converter focused + BMS full gate；涉及转谱或键音链时加 mania relevant 与真实 shared-store focused。
