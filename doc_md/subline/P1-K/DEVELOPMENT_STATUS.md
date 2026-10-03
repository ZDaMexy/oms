# P1-K 当前状态：BMS 解析与转换治理

> 最后核对：2026-10-03（校正 K11 旧待办并收简计划/记忆；产品验证与真实特殊谱门保留）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。格式参考见 [BMS_FORMAT_REFERENCE.md](../../other/BMS_FORMAT_REFERENCE.md)。

## 当前阶段

2026-09-22 TOTAL 修正：作者值改为 nullable，未声明不再冒充200；非法值诊断并忽略，最后有效分支内合法值生效，clone/converter/cache保留状态。规则来源、具名失败与验证限制见[TOTAL报告](../../other/BMS_TOTAL_RULES_AUDIT_20260922.md)，玩法合同归P1-C。

K1–K12 主体已阶段性收口：解析 authority、主要控制事件、projection reuse、BMS→mania 转换与 converted-star 修正均已落地。P1-A C3 所需的 P1-K Skin 前置（keymode authority、全 lane armed timeline 与 mod 后键音/LaneId 一致性）也已闭合。该结论只关闭 C3 的解析/转换前置，不代表整条 P1-K 完成；公开表面 wording、真实特殊谱与更宽人工证明仍按本线继续。

## 已落地能力

- raw carrier + typed model 双层保留：未知 header/channel 不因 typed consumer 缺席而静默丢失。
- signed BPM、duplicate channel compound、同拍位 `BPM → STOP → object`、LNTYPE 2 最小表达。
- `RANDOM/SWITCH` 已有固定选择值 1 的分支链，`SETRANDOM/SETSWITCH` 使用作者指定值；IF/ELSEIF/ELSE 与 CASE/SKIP/DEF 已消费，真正随机选支未实现。
- BGA/invisible/mine/scroll 等 visual/control typed surface 与 consumer projection。
- parse-once/project-many：metadata、background、Song Select、statistics、results 等复用 parse authority。
- source-bound modless playable cache 与 invalidation；results/score consumer 使用 already-modded playable contract。
- dedicated BMS→mania converter：sample-only BGM/scratch、LN tail 静音、converted star 持久化和展示 read-model。
- converted HoldNote 保留 head sample/WAV slot，手动 pooled head 已经 shared store 发声；自动键音的只读音频快照在 NR/HO/IN 重建玩法对象后仍可播放完整原谱音乐。原统计/难度、NodeSamples 与静音尾不改，普通 mania 及无 hosted store 路径保留；播放实现和听感验收归 [P1-J](../P1-J/DEVELOPMENT_STATUS.md)，不再将手动 store 接入列为缺口。
- LNOBJ 只与同 lane 紧邻前一普通音符配对，禁止 LIFO 回抓制造重叠 LN。
- converted-star 难度入口过滤 sample-only BGM/scratch，并以 conversion version 失效旧结果。
- immutable `BmsKeymodeResolution` 由 parser 单点产出并原样流经 converter、production loader 与 gameplay layout owner：authoritative host/importer显式 override、`.pms/.bme`、P2/high channel 与完整 channel-set 的 precedence、evidence、纠正入口及稳定脱敏 diagnostic 已冻结；无充分证据或证据冲突时 fail-closed，不再按最高出现 channel、hit object 或 layout 宽度猜测。
- `LaneKeysoundTimelines` 的 canonical 上界已改为 `GetLaneCount()`，覆盖 5K/7K 最右键、9K 全 lane、14K K14/Scratch2，以及 visible note、LN head/tail armed entry、invisible object 与相邻 mine；layout/skin/runtime 只消费 parser/converter 投影，不重读 BMS 或二次推导 lane 数。
- 原生 BMS 的 Mirror/RANDOM/R-RANDOM/custom 的对象、mine 与 armed timeline 共用同一 exact permutation；S-RANDOM 因无单一列置换而稳定禁用受影响 armed timeline、保留对象自身 WAV。玩家/autoplay、native BMS 与 converted Mania 已在 production host 证明进入同一 shared keysound store 并实际请求发声，原生 BMS post-mod 对象、keysound 与 skin lookup 汇合到同一 `LaneId`；未改 sample pool、判定或 binding。

## 不可破坏的边界

- decoder/normalized model 是解析唯一 authority；consumer 不得各自 ad hoc 重读原始 BMS。
- sample-only 对象可留在 `HitObjects` 用于播放，但不得进入 scorable/star/max-combo 语义。
- persisted metadata 多个子系统共享 `RulesetData` 时必须保留未知 JSON 字段，禁止 whole-object clobber。
- display-only 标题/难度清理不改存库原值和源文件 MD5。
- 转谱器不自行计算 mania 星级；星级归 `ManiaDifficultyCalculator`/difficulty cache。

## 最近一次验证

2026-09-30 原生/转谱性能专项保留 converter 数据、普通 mania 样本及回退合同；最终 BMS full 的 `TestManualConvertedHoldUsesSharedStoreAndSilentTail` 通过，后续作者窗口工作的完整回归也保持该结果。证据分别见[性能验证](../../other/GAMEPLAY_PERFORMANCE_20260930.md)与[作者能力验证](../../other/BGA_SKIN_AUTHORING_20260930.md)；这些是运行时软件证明，不代表真实特殊谱或听感已签收。

2026-09-29 自动键音的 converter/Mod/clone 证明见 [P1-J 历史记录](../P1-J/CHANGELOG.md#bms-与-bmsmania-自动键音)；同日后续手动 LN 的 Player/store 路由证明见[体验验证](../../other/EXPERIENCE_CLOSURE_20260929.md)。

2026-09-22 TOTAL 的decoder/converter/cache、演奏/回放与results专项，以及BMS full和Release结果统一见[P1-C最新验证](../P1-C/DEVELOPMENT_STATUS.md#最近一次验证)与[TOTAL报告](../../other/BMS_TOTAL_RULES_AUDIT_20260922.md)。完整回归包含已逐项复现的旧皮肤失败，不能沿用旧C3全绿数字作为当前结论。2026-09-23仅回读留存证据，未新增产品测试。

2026-08-30 C3解析/keymode/lane/shared-store证明仍作为历史专项证据保留，详细计数和边界见[CHANGELOG](CHANGELOG.md)；它不代签当前全部代码或真实特殊谱验收。

## 当前风险

- 特殊 long-note、极端控制流和少见 header family 仍可能暴露 typed consumer 缺口。
- 新出现的稀有扩展名/channel family 若无法由现有 evidence 无歧义归类，仍会按合同 fail-closed，需要显式 override 或新的 parser 证据；不得在 layout/runtime 增补猜测兜底。当前override仅是production host/importer API seam，普通导入入口传`null`且没有终端用户纠正UI；真实模糊sparse谱的用户可用纠正流程仍是P1-K后续产品缺口。
- public wording/展示证明不完整不等于 parser 错误；先区分存储、projection 与 presentation owner。
- 任何 parser 改动都可能同时影响 native BMS、转谱 mania、统计、筛选、BGA 与缓存失效，必须跑全量。

## 下一检查点

1. 收口 BMS→mania 显式入口 wording 与更宽 presentation/manual proof。
2. 仅由真实谱证据驱动 special LN/control-event follow-up，并同步格式参考与约束。
3. 新 keymode evidence 只允许在 parser authority 内 additive 扩展，并保持无证据/冲突 fail-closed。
4. 继续保持 parser/converter focused + BMS full gate；涉及转谱或键音链时加 mania relevant 与真实 shared-store focused。

## 文档治理验证

2026-10-03：再次对照 [DrawableNote](../../../osu.Game.Rulesets.Mania/Objects/Drawables/DrawableNote.cs) 父 head sample/slot 路由，移除 K11 引言遗漏的手动 LN store 待办；PLAN 的已完成表/C3 实施史改为状态与合同回链，末端轨道记忆保留独有诊断。parser/keymode authority、终端用户纠正入口和真实听感缺口未改变；只改文档与记忆，未运行新产品测试，前次与本次审查见 [CHANGELOG](CHANGELOG.md)。
