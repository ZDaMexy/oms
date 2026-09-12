# P1-G 当前状态：Phase 1.x 人工验收汇总

> 最后更新：2026-09-12（星轨总体体验不通过，归回 P1-A；依用户要求暂停后续迭代，不代填分项签收）
> 全局状态与待人工项见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，执行清单见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

## 当前阶段

P1-G 仍处于分项收集与最终汇总待闭合阶段。它不实现功能，只记录无法由 headless tests 证明的设备、真实谱、视觉、交互和发行结果，并把缺陷重新归回 owning 子线。

用户明确否定现版星轨的动画、美术安排与精细度，认为整体不可用；该产品问题已归回 [P1-A](../P1-A/DEVELOPMENT_STATUS.md)，本轮仅按用户要求完成收尾，后续新对话再调整静线与星轨。现包保留作待改对照，不能把自动证据等同于可用成品。总体反馈与分项证据边界见[集中清单](../../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md#c7-双包集中体验)。

## 已有人工证据

- 2026-07-14 无外部皮肤、`.osk`、partial fallback、BMS 5K/7K/9K/14K、14K 双皿与 mania/BMS 资源隔离已通过。
- P1-F 已有 portable fresh extract/冷启动与覆盖更新基线；最终候选发行物仍须复核。
- P1-J 普通密度主要音频故障已有历史用户实机结论；最终跨谱音频清单仍未汇总闭门。

这些分项结论不等于 Phase 1.x 人工 release checklist 已完成。

## 当前待汇总矩阵

| 面 | 当前待人工项 | owning 子线 |
| --- | --- | --- |
| 皮肤 | 星轨总体体验不通过，作品调整待用户新对话恢复；静线未正式签收，`V-001`～`V-004` 仍 0/4、`V-005` 未签收，C7 具体矩阵继续待验 | P1-A |
| 输入/控制器 | analog scratch、跨设备 edge/hold、deadzone/sensitivity、真实 HID | P1-B/P1-D |
| gameplay/长条/音频 | LN/CN/HCN、长 BGM、dense keysound、empty-strike、pause/seek | P1-C/P1-E/P1-J |
| Song Select/导入 | 大库分组/筛选/搜索、shared visual、桌面拖放；单轨构成目标须先由P1-I实现 | P1-H/P1-I |
| Gimmick/BGA | 图序列、POOR、seek、老视频转码、代表 Gimmick 谱与 14K 布局 | P1-L/P1-A |
| 发行 | fresh extract、portable/custom root、覆盖更新与公开口径 | P1-F |

## 当前边界

- 自动测试通过不能替代真实设备、视觉、听感和大库交互。
- P1-G 不修缺陷、不补功能，也不让未闭合项以“人工可接受”绕过 owning gate。
- 每项必须记录版本/发行物、设备或谱面、步骤、期望、实际、证据和归线；没有可复现上下文的口头结论不升级为 release 证据。
- 用户总体否定仍是有效的产品修订依据；缺少具体环境时单独登记并归线，不用证据不足将其还原为“未知”，也不代判其它矩阵格或通过签收。
- 已通过项只有受影响功能变化时才重测，不重复消费用户时间。

## 下一检查点

1. 等用户在新对话恢复 P1-A 两款皮肤迭代后，按[集中视觉清单](../../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)记录受影响结果及原 V-001～V-005；保留[确定性短键素材说明](../../other/SKIN_BMS_NOTE_ANIMATION_MANUAL_GATE.md)的原输入与步骤，不扩大成 beatmap-local public authoring 证明，不恢复逐组件串行开工门。
2. 按 [当前计划](DEVELOPMENT_PLAN.md) 逐项吸收 P1-B/D/E/I/J/L/F 的可验收切片，不等待所有代码线同时结束才建账。
3. 所有 release gate 就绪后执行一次候选发行物总清单；阻塞项归线修复后只重测受影响矩阵格。

## 文档治理验证

2026-09-12 同步用户的星轨总体否定、P1-A 归线和本轮暂停边界。原 V-001～V-004 仍 0/4，V-005 未签收；没有新增具体设备或矩阵结论，不刷新既有产品自动验证或历史实机日期。本次仅改文档，文档检查由本轮统一执行。
