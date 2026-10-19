# P1-B 开发进度：输入语义与硬件验收

> 最后核对：2026-10-19（原生键盘线程、单一来源与组合键修复；实体设备门保持）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，当前执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

## 当前阶段

- 当前代码已具备可运行的输入主链。
- 当前仓库已有 keyboard / Raw Input / XInput / MouseAxis / DirectInput HID 主链；`OmsInputRouter` 已改成 shared-action 引用计数，`BmsInputManager` 只在全局首个 press / 最终 release 时转发 `BmsAction`。
- 原生窗口回调只入队，在 gameplay Update 判定；失焦/暂停清旧待发事件。绑定组使用单一键盘来源，原生监听不可用或不能覆盖整组时保留框架路径；暂停/Break交付范围一致，虚拟修饰键按左右实体键合并，正常布局/NumLock身份保持。
- `MouseAxis` / `HidAxis` 已按帧/按轮询 pulse 语义处理，反向换向会先 release 再 re-press；Windows 默认 HID backend 现为 DirectInput，`HidSharp` 仅保留为 `OMS_ENABLE_HIDSHARP=1` 诊断后端。
- desktop public settings surface 现已通过 `OsuGameDesktop.CreateSettingsSubsectionFor()` 安全隐藏 upstream 的 `MouseSettings` / `TouchSettings` / `TabletSettings`；这不等于删除 mouse/touch/tablet runtime config 或 handler，只是停止把非 OMS 通用 subsection 暴露给最终桌面产品面。
- `TestSceneOmsScratchGameplayBridge` 已有 loaded-scene 回归，覆盖 mouse-axis / HID-axis / XInput、mixed-source suppression、hold survival 与 takeover 等边界；这些软件场景不等于真实控制器覆盖。
- 剩余重点是 cross-device 终态语义与真实硬件验收，而不是再开新输入后端。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| desktop 输入设置公开表面 | 已完成 | upstream mouse/touch/tablet subsection 已安全隐藏；runtime chain 保持不变 |
| mixed-source runtime 语义 | 进行中 | 引用计数、first-press/final-release gating 与 scratch bridge 已接通，终态/硬件清单待签收 |
| 真实 HID 验收 | 未开始 | 仍缺真实 IIDX/BMS 控制器覆盖 |
| 对外硬件行为口径 | 进行中 | “Windows 默认 DirectInput + HidSharp 诊断后端”已可写入文档 |

## 最近一次验证

2026-10-19真实loaded判定、两路完整tap、失焦reset、左右修饰键和混合组合键已有回归；独立Windows宿主验证实际原生/SDL去重和Break回退。有效运行、探针纠正与完整回归只取[游玩审查](../../other/GAMEPLAY_REVIEW_20261019.md#验证证据)。SendInput与软件轴/控制器用例不代签实体键盘、HID或cross-device人工清单。

2026-09-22 BMS full 留存 TRX 中，`TestSceneOmsScratchGameplayBridge` 43/43 通过。它证明所覆盖的软件输入/玩法桥，不代表真实 HID、控制器或 cross-device 人工清单通过。同日完整回归的旧皮肤失败与 Release 历史执行边界见 [TOTAL 原验证](../../other/BMS_TOTAL_RULES_AUDIT_20260922.md#验证状态)；desktop 设置裁剪的原始构建记录见 [CHANGELOG](CHANGELOG.md#2026-05-09)。

## 文档治理验证

2026-09-29：复核 [BmsInputManager](../../../osu.Game.Rulesets.Bms/Input/BmsInputManager.cs)、[OmsInputRouter](../../../oms.Input/OmsInputRouter.cs)、HID backend 与 [scratch bridge 测试源码](../../../osu.Game.Rulesets.Bms.Tests/TestSceneOmsScratchGameplayBridge.cs)，回读9月22日 TRX 并为软件验证补齐日期和范围；未重新构建或测试，真实硬件门保持。此前审查见 [CHANGELOG](CHANGELOG.md)。
