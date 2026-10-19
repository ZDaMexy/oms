---
name: reference_bms_judgement_parity
description: BMS 判定家族的 sourced 合同、heuristic 边界与 LN regrab 地雷
metadata:
  node_type: memory
  type: reference
---

# BMS 判定 parity 召回

权威当前态：[P1-C STATUS](../../doc_md/subline/P1-C/DEVELOPMENT_STATUS.md)；窗口约束/来源在 P1-C CONSTRAINTS/CHANGELOG。

## 稳定合同

- judge family：OD、IIDX、LR2、beatoraja；基类支持早晚非对称、scratch、LN release、excessive/empty poor。
- IIDX 主窗口与 LR2 四档、beatoraja 缩放/非对称均已有 sourced 基线；任何数值修改先改 `BmsJudgementSystemParityTest`。
- beatoraja BAD 是早宽晚窄；不要因 `WindowFor(Meh)=max` 的显示怪象反向改错。
- IIDX empty/excessive poor `500/150` 与 CN release 属 OMS documented heuristic，不宣称闭源 parity。
- 边界统一 `<= window + BoundaryEpsilon`。
- Empty Poor 使用 `HitResult.Ok` 独立计数，不计 EX-SCORE/accuracy、不断 combo；BAD/Miss 才断连。Empty Poor 仍影响 gauge 与 FC/PERFECT 资格，不能从旧表或 `BmsPoorJudgement` 名字推断它等同 Miss。
- gauge rules family 与 judge family/gauge type 分离；Legacy 数表不代表全部 family。GAS 只按实际 active gauge 顺序推进，最终 lamp 看最终 active gauge，不在 results 重跑全部 mods 或并行取所有 gauge 的最高灯。
- 全自动BMS drawable会写AutoPlay=true，但CountsForScore仍为true；按AutoPlay排除会使实况与results重建分母不一致。辅助的真实排除依据是CountsForScore，测试fixture若把AutoPlay当辅助必须显式标成不计分。
- 跨列结果的Health快照依赖全局应用顺序；每列栈自己逆序不等于全局逆序，同刻和弦也能错。须真实Player手动回放同时核health、统计/连击，不能只看EX还原；根Playfield沿既有entry统一撤销，见[深审记录](../../doc_md/other/GAMEPLAY_PERFORMANCE_JUDGEMENT_20261019.md)。

## LN 模式地雷

- `RequiresTailJudgement`：CN+HCN 尾判。
- `RequiresBodyGaugeTicks`：HCN body gauge。
- `AllowsRegrabAfterRelease`：**仅 HCN**。CN 早释放永久 miss，不能用 `RequiresTailJudgement` 门控 regrab。
- LN body state 纯派生自 holding/judgement，因此只有 HCN 能 Broken→Holding。
- 长条完成后倒带时，head 可能仍命中、tail 已撤销，而 transient holding 已清；这不是一次允许 LN/CN 重抓的新按键。须按真实 replay pressed、既有 entry 结果和对象级自动辅助还原，并拦截反向输入的新判定。只撤销 body tick 的旧测试不覆盖跨 tail 的丢状态，见[游玩审查](../../doc_md/other/GAMEPLAY_REVIEW_20261019.md)。

## 产品面

旧常驻 FAST/SLOW/pacemaker/summary/GN feedback card 已删除；全局 `JudgementCounterDisplay` 与皮肤作者信息区不能混为同一入口。静线已用通用只读绑定显示实时判定统计，并非恢复旧固定反馈卡。ComboBreak 是 score statistics 派生项，不一定经过真实 judgement event；计数器应从 statistics 同步，绑定诊断见 [[reference_skin_cabinet_surfaces]]。

旧 29-case 数字、窗口表与删除史查 P1-C CHANGELOG。相关：[[reference_mania_autoplay_holdnote]]、[[reference_bms_default_skin_geometry]]。
