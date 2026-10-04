# P1-G 当前状态：Phase 1.x 人工验收汇总

> 最后核对：2026-10-04（补录已有候选证据与独立 IR 归属；无新增人工签收）
> 全局状态与待人工项见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，执行清单见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

## 当前阶段

P1-G 仍处于分项收集与最终汇总待闭合阶段。它不实现功能，只记录无法由 headless tests 证明的设备、真实谱、视觉、交互和发行结果，并把缺陷重新归回 owning 子线。

用户已明确放弃星轨，静线是唯一内置，外观打磨按用户决定暂停；不再安排星轨开发或签收。静线的参考对比、按键/gauge、演奏信息与轨道比例调整获得“可以，先暂时打磨到这里”的反馈；随后另行授权的 gameplay 性能优化、BGA 作者窗口与完整手册已具软件证据，未恢复外观打磨。有限接受不代填未逐项验证的键数、设备、长期体验或原 V-001～V-005。旧双包总体反馈保留历史身份，见[集中清单](../../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md#c7-集中体验)。

9 月 30 日[性能验证](../../other/GAMEPLAY_PERFORMANCE_20260930.md)与[作者能力验证](../../other/BGA_SKIN_AUTHORING_20260930.md)分别证明热路径改善、共享播放会话及作者窗口投影；桌面合成图像通过不等于真实歌曲、全部设备/DPI 或最终发行组合已验收。

## 已有分项证据

- 2026-07-14 无外部皮肤、`.osk`、partial fallback、BMS 5K/7K/9K/14K、14K 双皿与 mania/BMS 资源隔离已通过。
- P1-F 旧候选保留真实跨版本证据，2026-10-03 新候选另有便携/自定义根、恢复与同包覆盖的自动启动记录；生成物随后按用户要求删除，原结果仍在。新记录不代签 Windows Shell 解包、跨版本、新作者演练、下载或整体真人体验；非便携独立账户及画面/设备/长期门仍待验，见 [P1-F 状态](../P1-F/DEVELOPMENT_STATUS.md)。
- P1-J 普通密度主要音频故障已有历史用户实机结论；最终跨谱音频清单仍未汇总闭门。

这些分项结论不等于 Phase 1.x 人工 release checklist 已完成。独立 IR 的真实游玩、断网重启与账号归属验收沿 [P3-IR](../P3-IR/DEVELOPMENT_PLAN.md)，不由本矩阵代签。

## 当前待汇总矩阵

| 面 | 当前待人工项 | owning 子线 |
| --- | --- | --- |
| 皮肤 | 星轨退役，不再要求签收；静线既有调整获用户接受并暂止打磨，`V-001`～`V-004` 仍 0/4、`V-005` 未签收，当前单内置与第三方的具体矩阵继续待验 | P1-A |
| 输入/控制器 | analog scratch、跨设备 edge/hold、deadzone/sensitivity、真实 HID | P1-B/P1-D |
| gameplay/长条/音频 | LN/CN/HCN、手动转谱长条、长 BGM 暂停保位与 seek/retry 清旧声、dense keysound、empty-strike；两模式自动键音开关听感见 [P1-J](../P1-J/DEVELOPMENT_PLAN.md#0-用户指定自动键音2026-09-29) | P1-C/P1-E/P1-J |
| Song Select/导入 | 单轨筛选手感与窄窗口、大库分组/搜索、当前页难度表刷新；隔离根重扫/缺失恢复/解除后文件与历史保全 | P1-H/P1-I |
| 公共下载 | 两源 BMS 修复后首次完成与精确打开、真实大包/网络/完整歌曲；Sayobot 实际包成功、稳定入库与打开；软件/历史实站证据和未签收范围见 [P1-A](../P1-A/DEVELOPMENT_STATUS.md) | P1-A |
| Gimmick/BGA | 实谱叠层/ARGB、图序列、POOR、seek、老视频与长时解码；作者窗口在不同尺寸/DPI 下的 Fit/Fill/Stretch、零窗与信息区，代表 Gimmick 谱及默认 14K 布局 | P1-L/P1-A |
| 发行 | fresh extract、portable/custom root、覆盖更新与公开口径；当前完整候选承接公共下载与离线启动组合 | P1-F/P1-A |

## 当前边界

- 自动测试通过不能替代真实设备、视觉、听感和大库交互。
- P1-G 不修缺陷、不补功能，也不让未闭合项以“人工可接受”绕过 owning gate。
- 每项必须记录版本/发行物、设备或谱面、步骤、期望、实际、证据和归线；没有可复现上下文的口头结论不升级为 release 证据。
- 用户总体否定仍是有效的产品修订依据；缺少具体环境时单独登记并归线，不用证据不足将其还原为“未知”，也不代判其它矩阵格或通过签收。
- 已通过项只有受影响功能变化时才重测，不重复消费用户时间。

## 下一检查点

1. 当前仅完成已授权收尾；后续有新的静线改动或验收安排时，按[集中视觉清单](../../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)记录受影响结果及原 V-001～V-005，不重开星轨。保留[确定性短键素材说明](../../other/SKIN_BMS_NOTE_ANIMATION_MANUAL_GATE.md)的原输入与步骤，不扩大成 beatmap-local public authoring 证明，不恢复逐组件串行开工门。
2. 按 [当前计划](DEVELOPMENT_PLAN.md) 逐项吸收 P1-A/B/D/E/H/I/J/L/F 的可验收切片，不等待所有代码线同时结束才建账。
3. 所有 release gate 就绪后执行一次候选发行物总清单；阻塞项归线修复后只重测受影响矩阵格。

## 文档治理验证

2026-10-04：分清旧跨版本、10 月 3 日同包自动候选与未签收人工门，独立 IR 真人验收回链 owning 子线；当前合资格删除说明不提升原不可达格或任何签收状态。无新产品/人工验证，过程见 [CHANGELOG](CHANGELOG.md)，统一检查归 [主线日志](../../mainline/CHANGELOG.md#项目进度与文档记忆一致性复核)。
