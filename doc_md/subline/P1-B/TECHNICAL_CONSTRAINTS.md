# P1-B 技术约束：输入语义与硬件验收

1. 不得为了补验收而引入与当前主链无关的新输入后端。
2. Windows 默认 HID backend 维持 DirectInput；`HidSharp` 仅作为诊断或非 Windows 路径。
3. keyboard / Raw Input / XInput / MouseAxis / HID 的 shared-action 语义不得因局部修复回退。
3a. Windows 原生键盘回调只排入带单调接收时间的 transition/reset，不得在窗口线程读取音乐时钟、修改动作、判定或 Drawable。既有 offset/rate 后时钟发布配对样本，Update 每外层帧冻结批次，经原有 FrameStability 在事件时刻交付，判定/RawTime/录制保持同一时间；迟到不倒改已提交结果，未来事件等待。失焦/暂停、seek/reset、offset/source 或实际 replay ownership 切换失效旧批次，释放已应用键盘状态而不释放其它设备引用；同外层帧 attach/detach 也不得漏掉失效。关闭后的旧 sink 不再接纳输入，也不能解除新 sink。
3b. 原生交付范围与普通框架输入的 ownership 必须一致。监听不可用、当前暂停/Break，或当前绑定组不能全部由原生来源提供时，保留普通键盘路径；同一组合不能拆到两条异步来源。成功交付的完整 tap 不得被 SDL 回声再次判定。虚拟修饰键由左右实体键共同满足，最终一侧释放才结束组合；精确左/右绑定仍独立，布局与 NumLock 键位身份须与固定框架核对。
3c. 已保存 HID 绑定的首次发现随真实后台加载完成；运行中发现/重复候选和退休设备释放走唯一后台任务，Update 不等待该任务，已连设备仍每帧轮询。缺席设备使用500ms退避，任务中的设备通知不得在提交时清丢；退出封闭新任务、锁外join并释放未提交结果与provider。DirectInput每轮取得一次快照，buffered报告才继续drain；断开当帧释放动作，资源释放不能因IsConnected=false被跳过。
4. 输入行为、硬件口径或验收结论变化时，同次更新本线实际受影响的状态、计划、约束和验证记录；仅影响全局优先级、release gate 或硬约束时向 mainline 回写摘要与链接。同步规则以 [AGENTS](../../../AGENTS.md#权威与文档) 为准，不机械刷新无变化文件。
5. 若 desktop public settings surface 需要裁剪 upstream `MouseSettings` / `TouchSettings` / `TabletSettings`，必须只在 `OsuGameDesktop` 这类 desktop 宿主层安全隐藏；不得在 `OsuGameBase`、runtime config 或 input handler 层直接删除，否则会连带改写 test scene / 非 desktop host 行为。
6. 安全隐藏输入设置分区不等于删除输入语义：`MouseDisableButtons`、`MouseDisableWheel`、`ConfineMouseMode`、`TouchDisableGameplayTaps` 与 tablet/touch/mouse handler 的运行时消费链不得因此断开。
