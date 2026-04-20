# P1-B 开发进度：输入语义与硬件验收

> 最后更新：2026-04-20

## 当前阶段

- `P1-B` 已完成子线建档，并且当前代码已具备可运行的输入主链。
- 当前仓库已有 keyboard / Raw Input / XInput / MouseAxis / DirectInput HID 主链；`OmsInputRouter` 已改成 shared-action 引用计数，`BmsInputManager` 只在全局首个 press / 最终 release 时转发 `BmsAction`。
- `MouseAxis` / `HidAxis` 已按帧/按轮询 pulse 语义处理，反向换向会先 release 再 re-press；Windows 默认 HID backend 现为 DirectInput，`HidSharp` 仅保留为 `OMS_ENABLE_HIDSHARP=1` 诊断后端。
- `TestSceneOmsScratchGameplayBridge` 已形成 43/43 的 loaded-scene 回归基线，覆盖 mouse-axis / HID-axis / XInput、mixed-source suppression、hold survival 与 takeover 等边界。
- 剩余重点是 cross-device 终态语义与真实硬件验收，而不是再开新输入后端。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 子线建档 | 已完成 | 四件套已建立 |
| mixed-source runtime 语义 | 进行中 | 引用计数、first-press/final-release gating 与 43/43 scratch bridge 已接通 |
| 真实 HID 验收 | 未开始 | 仍缺真实 IIDX/BMS 控制器覆盖 |
| 对外硬件行为口径 | 进行中 | “Windows 默认 DirectInput + HidSharp 诊断后端”已可写入文档 |

## 验证记录

- 沿用最近已验证快照：`TestSceneOmsScratchGameplayBridge` **43/43**；主线现状同步认为 keyboard / Raw Input / XInput / MouseAxis / DirectInput HID 主链稳定。