# P1-C 当前计划：判定语义与反馈边界

> 最后核对：2026-09-29（按用户指定优先完成自动调整偏移；软件交付后转人工验收）
> 主线顺序见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，稳定合同见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)，已完成实现与删除记录见 [CHANGELOG.md](CHANGELOG.md)。

## 子线职责

P1-C 只维护 BMS 判定家族、窗口/poor/release parity、判定结果到当前产品反馈面的语义边界，以及相关回归门。

- HUD/skin 宿主归 P1-A；P1-C 不扩写旧宿主接口。
- 真实 LN/CN/HCN 与谱面体验归 P1-E，最终人工结果由 P1-G 汇总。
- 视觉滚动与 Gimmick 位置映射归 P1-L，不得改变 P1-C 的时间判定结果。
- 完整 FHS、dan、1P/2P flip、BSS/MSS 不进入本线当前交付。

## 当前基线

- IIDX/LR2/beatoraja/OD 的主要窗口、边界、scratch、release 与 excessive/empty-poor 合同已落，并由 parity test 守门。
- 常驻 `DefaultBmsSpeedFeedbackDisplay`、`GameplayFeedbackState` 及其 FAST/SLOW、pacemaker、summary、常驻 GN 已按产品决定删除，不是当前能力。
- 当前反馈面包含全局 `JudgementCounterDisplay`、静线通过普通 scene 绑定的实时判定统计、调速 toast、pre-start overlay 与既有 target/cycle/remember 行为。皮肤统计只读同一 score statistics，不恢复旧反馈卡。
- pre-start 纯视觉流速 preview 已存在，但不得进入 hit object、判定、计分、键音、replay 或 autoplay authority。

完成阶段的设计和删除经过不在本页展开，按日期查 [CHANGELOG](CHANGELOG.md)。

## 当前执行顺序

### 2026-09-29 用户指定：自动调整偏移 style 互斥

本专题按以下依赖与验收顺序执行；已交付软件状态见 STATUS，实现与验证记录见 CHANGELOG，真实设备体验单独签收。

1. **统一入口与迁移**：音频→偏移中以「自动调整偏移」单选 `关闭 / osu!lazer style / beatoraja style` 替代旧布尔开关。只有一个持久化选择；旧开启迁移为 lazer、旧关闭为 Off，新选择存在时不得被旧键覆盖。全局推荐偏移仍是手动应用。beatoraja 首版只作用于 BMS，说明中明确；mania 的 lazer 行为保留。
2. **固定偏移地基**：同处提供 BMS 显示偏移，默认 0ms、范围 ±500ms、步长 1ms；正值使音符更早到线。该值独立于既有全局音频/谱面偏移，切换 style 不清除手动值。纯显示映射在 BMS 内实现；普通音符、皿、长条、小节线、地雷同步，BGA/BGM/键音、输入、判定窗口、score/gauge 时间不变。关闭自动且 0ms 恢复原算法实例与行为；自动启用时跨零保持同一包装；普通和 STOP 映射均覆盖，嵌套长条不能重复加偏移。
3. **自动采样**：依据[合同固定的 upstream 来源](TECHNICAL_CONSTRAINTS.md#自动调整偏移合同2026-09-29)独立实现；PG/GR/GD 且绝对误差 ≤150ms 才参与，按 OMS 晚正早负方向加 `sign(error)*floor((abs(error)+15)/30)` ms，每次限幅 ±500ms；无平均窗口/置信度/设备学习。普通键、皿及长条有效判定必须明确对应，LN 合并判定与 CN/HCN 头尾不可被持续 tick/父对象重复采样；自动辅助音、自动播放、回放不写个人偏移。
4. **生命周期与保存**：真实游玩调整当前值并由既有配置持久化，完成/失败/退出/重试保留；关闭停止自动调整但保持固定显示值。lazer consumer 只在 Lazer 选择下自动应用上一局结果，Beatoraja 只在 BMS 真实演奏且当前选择匹配时调整；切换立即停止不匹配的自动路径，不自动归零既有谱面/音频偏移。
5. **回放边界**：保证相同输入判定/成绩/血槽一致，并阻止回放改个人设置。优先在现有 BMS 成绩扩展中记录本局显示偏移变化供回放读取，旧回放使用固定 0ms；不修改按键帧编码或既有 TOTAL 版本含义。回放 seek/重看使用记录的显示状态，不重新学习。录制结束、score clone 前刷新一次轨迹；结果数据重建须保留轨迹。
6. **验证**：配置旧值迁移、三态互斥与 lazer 既有统计回归；beatoraja 阈值/方向/边界/辅助过滤/LN-CN-HCN；普通与 STOP 的位置、长条长度和加载时机；真实 drawable 输入下不同偏移的判定/声音不变；回放、重试及设置保存。串行运行 core offset focused、BMS focused/full、mania relevant、Release；full 既有失败须具名对照已有证据，不能视为全绿。最后文档检查与 diff 检查，当前分支提交，不推送。
7. **人工门**：熟悉普通谱、和弦、皿、LN/CN/HCN、变速/STOP 各实测；检查收敛方向、画面变化、关闭固定、重启保存、回放不污染配置。自动验证不得替代听感/真实控制器签收。

范围不包含新常驻 HUD 卡、皮肤协议、谱面作者设置、设备校准、反向滚动支持或多个自动调整算法扩展。当前偏移可在统一设置直接查看和恢复 0ms；不新增结算大卡/训练系统。

### 2026-09-22 用户指定：TOTAL 正确性收口

作者/缺省 TOTAL、辅助前后物量、各 family 的回血/扣血与新旧成绩一致性已完成软件切片，验证及既有失败归因见[取证报告](../../other/BMS_TOTAL_RULES_AUDIT_20260922.md)。后续守门：P1-K 保持声明与解析诊断，P1-C 保持有效 TOTAL 与成绩版本；变更须覆盖 loader→演奏/回放→results。保留 Gauge Mod、默认 Legacy、GAS 与外观；HCN 持续速率另案处理，不代签 P1-E 的真实长条谱门。

### 1. 保持 parity gate

本项是守门职责，不主动扩功能：

1. 任何窗口、非对称方向、scratch、long-note release、excessive/empty poor 改动，先修改 `BmsJudgementSystemParityTest` 让差异可审，再改实现。
2. 明确区分 audit/source-backed 数值与 IIDX 闭源 documented heuristic；不得把启发式写成完整精确复刻。
3. 保持跨家族 inclusive/epsilon 边界一致，同时保留各家族的早晚非对称和 poor/release 差异。
4. 判定时间链与视觉滚动正交；P1-L 旁路、lane cover、皮肤或 HUD 变化不得改变 score/replay truth。

验收：受影响的 parity focused 通过；涉及 gameplay/LN 时补 BMS relevant/full，并确认现有 counter/results 消费没有语义倒退。

### 2. 把真实谱人工证明交给 P1-E/P1-G

1. P1-E 维护代表性 LN/CN/HCN、scratch/release/poor 真实谱 checklist，并记录谱面与预期语义。
2. P1-G 汇总设备、真实谱与当前可见反馈的人工结果；P1-C 只解释判定合同，不重复建立验收总表。
3. 人工发现窗口/规则缺陷时重新归回 P1-C；显示、输入或长条 runtime 问题分别回 P1-A/B/D/E。

验收：每个反馈都有谱面、模式、输入条件、期望/实际和归线结论，不能用单一 headless 数字替代真实组合证明。

### 3. 未来反馈需求必须另立专题

只有用户重新确认 FAST/SLOW、pacemaker 或其它训练反馈的产品价值后，才允许新建实施专题；不得直接复活已删除 aggregate/card。

新专题必须先冻结：

1. 玩家问题与最小用户可见结果。
2. 数据 authority、生命周期与是否需要持久化。
3. P1-A 提供的宿主/skin slot 与 fallback 粒度。
4. 与全局 `JudgementCounterDisplay`、toast、pre-start overlay、results 的去重边界。
5. 自动与人工验收、回退路径和删除策略。

未完成以上决议前，反馈需求不得偷塞进 gauge、combo、wrapped HUD 子节点或旧 `IBmsHudLayoutDisplay` 扩展。

## 当前不做

- 不恢复常驻 GN/FAST-SLOW/pacemaker/summary card，也不把其历史设计当作待实现清单。
- 不为展示需求修改判定窗口、score bucket、gauge truth 或 replay。
- 不把完整 Floating/FHS、训练系统或新 gameplay mod 混入 parity 维护。
- 不复制 P1-E/P1-G 的人工验收状态到本页。
