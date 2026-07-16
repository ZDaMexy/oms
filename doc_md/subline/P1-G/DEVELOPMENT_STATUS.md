# P1-G 当前状态：Phase 1.x 人工验收汇总

> 最后更新：2026-07-16
> 全局状态与待人工项见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，执行清单见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

## 当前阶段

P1-G 仍处于分项收集与最终汇总待闭合阶段。它不实现功能，只记录无法由 headless tests 证明的设备、真实谱、视觉、交互和发行结果，并把缺陷重新归回 owning 子线。

## 已有人工证据

- 2026-07-14 无外部皮肤、`.osk`、partial fallback、BMS 5K/7K/9K/14K、14K 双皿与 mania/BMS 资源隔离已通过。
- P1-F 已有 portable fresh extract/冷启动与覆盖更新基线；最终候选发行物仍须复核。
- P1-J 普通密度主要音频故障已有历史用户实机结论；最终跨谱音频清单仍未汇总闭门。

这些分项结论不等于 Phase 1.x 人工 release checklist 已完成。

## 当前待汇总矩阵

| 面 | 当前待人工项 | owning 子线 |
| --- | --- | --- |
| 皮肤 | managed `.osk` BMS 普通短键编号帧动画；后续真实新增组件 | P1-A |
| 输入/控制器 | analog scratch、跨设备 edge/hold、deadzone/sensitivity、真实 HID | P1-B/P1-D |
| gameplay/长条/音频 | LN/CN/HCN、长 BGM、dense keysound、empty-strike、pause/seek | P1-C/P1-E/P1-J |
| Song Select/导入 | 大库分组/筛选/搜索、shared visual、桌面拖放 | P1-H/P1-I |
| Gimmick/BGA | 图序列、POOR、seek、老视频转码、代表 Gimmick 谱与 14K 布局 | P1-L/P1-A |
| 发行 | fresh extract、portable/custom root、覆盖更新与公开口径 | P1-F |

## 当前边界

- 自动测试通过不能替代真实设备、视觉、听感和大库交互。
- P1-G 不修缺陷、不补功能，也不让未闭合项以“人工可接受”绕过 owning gate。
- 每项必须记录版本/发行物、设备或谱面、步骤、期望、实际、证据和归线；没有可复现上下文的口头结论不升级为 release 证据。
- 已通过项只有受影响功能变化时才重测，不重复消费用户时间。

## 下一检查点

1. 先按[确定性手工门说明](../../other/SKIN_BMS_NOTE_ANIMATION_MANUAL_GATE.md)记录 P1-A managed `.osk` 普通短键编号帧动画、选择切换与 selected 坏包回落的单独用户确认；该素材不声称 beatmap-local `WorkingBeatmap` 集成。
2. 按 [当前计划](DEVELOPMENT_PLAN.md) 逐项吸收 P1-B/D/E/I/J/L/F 的可验收切片，不等待所有代码线同时结束才建账。
3. 所有 release gate 就绪后执行一次候选发行物总清单；阻塞项归线修复后只重测受影响矩阵格。

## 本次文档验证

2026-07-16 仅重建人工验收 ownership 与矩阵，未改代码、未运行产品测试或 Release，现有产品结论不变。
