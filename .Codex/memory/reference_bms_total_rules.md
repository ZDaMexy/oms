---
name: reference-bms-total-rules
description: BMS TOTAL 输入、缺省物量与新旧成绩重建地雷
metadata:
  node_type: memory
  type: reference
---

# TOTAL 与成绩版本

权威合同见[P1-C](../../doc_md/subline/P1-C/TECHNICAL_CONSTRAINTS.md)、[P1-K](../../doc_md/subline/P1-K/TECHNICAL_CONSTRAINTS.md)，来源与实测记录见[TOTAL报告](../../doc_md/other/BMS_TOTAL_RULES_AUDIT_20260922.md)。

- 不在 decoder 把缺失改成200：这会失去作者明确写200和缺省的区别，也会让家族切换无从重算。
- 缺省 TOTAL 的N在辅助排除前；回血/Hard物量修正在辅助排除后。辅助会关闭尾判CountsForScore，因此恢复原始N必须使用保留的运行时长条模式，不能从被辅助修改的尾判反推CN/HCN。
- 解析器只接受有限正数，后续非法行不覆盖已有合法值。不要照搬上游只判>0而接受Infinity的漏洞。
- results中data null也可能是旧回放，不能在保存/展示阶段直接把它升级v7。`BmsScoreInfoData.Version` 默认仍是v6，用于旧数据缺省；只有 `InitialiseNewPlay` 显式设v7，不能为了“统一版本”改DTO默认。新autoplay通过瞬时生成的Replay类型识别；已保存Replay按成绩版本，不能仅看Autoplay Mod认定新局。
- 血量曲线重放HitEvents，与已存终值/灯不同路径；修改缺省公式时两条路径都要测。旧低物量扣血曾用浮点折半，新v7按来源整数折半，旧成绩保留旧算法。
- 历史非法TOTAL不承诺复现旧的无效数值传播；解析仍要求有限正数，已存终值/灯保留。测试空跑与临时证据失效的通用诊断见[构建记忆](reference_build_and_test.md)。
