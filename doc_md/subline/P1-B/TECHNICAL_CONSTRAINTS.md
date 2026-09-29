# P1-B 技术约束：输入语义与硬件验收

1. 不得为了补验收而引入与当前主链无关的新输入后端。
2. Windows 默认 HID backend 维持 DirectInput；`HidSharp` 仅作为诊断或非 Windows 路径。
3. keyboard / Raw Input / XInput / MouseAxis / HID 的 shared-action 语义不得因局部修复回退。
4. 输入行为、硬件口径或验收结论变化时，同次更新本线实际受影响的状态、计划、约束和验证记录；仅影响全局优先级、release gate 或硬约束时向 mainline 回写摘要与链接。同步规则以 [AGENTS](../../../AGENTS.md#权威与文档) 为准，不机械刷新无变化文件。
5. 若 desktop public settings surface 需要裁剪 upstream `MouseSettings` / `TouchSettings` / `TabletSettings`，必须只在 `OsuGameDesktop` 这类 desktop 宿主层安全隐藏；不得在 `OsuGameBase`、runtime config 或 input handler 层直接删除，否则会连带改写 test scene / 非 desktop host 行为。
6. 安全隐藏输入设置分区不等于删除输入语义：`MouseDisableButtons`、`MouseDisableWheel`、`ConfineMouseMode`、`TouchDisableGameplayTaps` 与 tablet/touch/mouse handler 的运行时消费链不得因此断开。
