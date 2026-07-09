---
name: reference_bms_judgement_parity
description: BMS 判定家族的 sourced 合同、heuristic 边界与 LN regrab 地雷
metadata:
  node_type: memory
  type: reference
---

# BMS 判定 parity 召回

权威当前态：[P1-C STATUS](doc_md/subline/P1-C/DEVELOPMENT_STATUS.md)；窗口约束/来源在 P1-C CONSTRAINTS/CHANGELOG。

## 稳定合同

- judge family：OD、IIDX、LR2、beatoraja；基类支持早晚非对称、scratch、LN release、excessive/empty poor。
- IIDX 主窗口与 LR2 四档、beatoraja 缩放/非对称均已有 sourced 基线；任何数值修改先改 `BmsJudgementSystemParityTest`。
- beatoraja BAD 是早宽晚窄；不要因 `WindowFor(Meh)=max` 的显示怪象反向改错。
- IIDX empty/excessive poor `500/150` 与 CN release 属 OMS documented heuristic，不宣称闭源 parity。
- 边界统一 `<= window + BoundaryEpsilon`。

## LN 模式地雷

- `RequiresTailJudgement`：CN+HCN 尾判。
- `RequiresBodyGaugeTicks`：HCN body gauge。
- `AllowsRegrabAfterRelease`：**仅 HCN**。CN 早释放永久 miss，不能用 `RequiresTailJudgement` 门控 regrab。
- LN body state 纯派生自 holding/judgement，因此只有 HCN 能 Broken→Holding。

## 产品面

常驻 FAST/SLOW/pacemaker/summary/GN feedback card 已删除；计数由全局 `JudgementCounterDisplay`。ComboBreak 是 score statistics 派生项，不一定经过真实 judgement event；计数器应从 statistics 同步。

旧 29-case 数字、窗口表与删除史查 P1-C CHANGELOG。相关：[[reference_mania_autoplay_holdnote]]、[[reference_bms_default_skin_geometry]]。
