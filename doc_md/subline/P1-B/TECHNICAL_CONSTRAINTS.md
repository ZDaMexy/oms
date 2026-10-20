# P1-B 技术约束：输入语义与硬件验收

1. 不得为了补验收而引入与当前主链无关的新输入后端。
2. Windows 默认 HID backend 维持 DirectInput；`HidSharp` 仅作为诊断或非 Windows 路径。
3. keyboard / Raw Input / XInput / MouseAxis / HID 的 shared-action 语义不得因局部修复回退。
3a. Windows 原生键盘回调只排入带单调接收时间的 transition/reset，不得在窗口线程读取音乐时钟、修改动作、判定或 Drawable。既有 offset/rate 后时钟发布配对样本，Update 每外层帧冻结批次，经原有 FrameStability 在事件时刻交付，判定/RawTime/录制保持同一时间；迟到不倒改已提交结果，未来事件等待。失焦/暂停、seek/reset、offset/source 或实际 replay ownership 切换失效旧批次，释放已应用键盘状态而不释放其它设备引用；同外层帧 attach/detach 也不得漏掉失效。关闭后的旧 sink 不再接纳输入，也不能解除新 sink。
3b. 原生交付范围与普通框架输入的 ownership 必须一致。监听不可用、当前暂停/Break，或当前绑定组不能全部由原生来源提供时，保留普通键盘路径；同一组合不能拆到两条异步来源。普通路径已开始的持有必须收到配平释放，不能因途中原生接管而吞掉 up；同键两路交叠只在最后来源松开时交付最终释放，其它手柄引用仍有效。成功交付的完整 tap 不得被 SDL 回声再次判定。虚拟修饰键由左右实体键共同满足，最终一侧释放才结束组合；精确左/右绑定仍独立，布局与 NumLock 键位身份须与固定框架核对。
3c. 已保存 HID 绑定的首次发现随真实后台加载完成；运行中发现/重复候选和退休设备释放走唯一后台任务，Update 不等待该任务，已连设备持续采样。缺席设备使用500ms退避，任务中的设备通知不得在提交时清丢；最终设备所有者退出封闭新任务、锁外drain并释放未提交结果与provider。DirectInput 显式 Acquire、有界读取原生缓冲并保留按钮边沿顺序；初始状态不能抹掉完整 tap，溢出／失去设备不得用新快照伪造历史。断开释放该设备引用，资源释放不能因IsConnected=false被跳过。
3d. HID 轴的相对位移、环形绝对位置、位置按钮和摇杆模拟运动须由玩家明确选择，不能根据 capability 猜用途；鼠标仅相对位移。校准与诊断复用同一解释器，默认无平滑／防抖。灵敏度后的有符号位移累积越过阈值才产生运动，反向抵消尚未触发的累积；位置按钮使用独立按下／松开阈值，回中不能产生反方向攻击。环形首次位置只建基线；摇杆用实际经过时间积分，reset 不保留旧偏转。
3e. 鼠标必须消费现有宿主未被光标限幅的相对位移，并仅在明确所有权期间请求捕获；不得新增同类别 Raw Input 注册抢走宿主输入。HID 外层冻结采样批次，历史 catch-up 子帧只交付已冻结的到时事件，不轮询更晚设备状态。观察戳只表达软件接收／轮询时间；原生时间没有可信时钟映射时不得宣称物理事件时刻。
3f. 新 HID 绑定使用具体实例标识和可读名称；旧型号标识只在唯一候选时解析，同型号歧义须明确失败并解除该旧别名引用，不能影响仍有效的具体实例绑定。该标识不承诺跨换口／驱动重装永久稳定。
3g. Windows 以实体设备和扫描身份去重，重复 down 不增加引用；拔出一个键盘只移除它的按键，up 使用 down 捕获的身份。暂停／来源切换后已见实体 held 保持抑制到 break，不能把系统重复当新攻击；恢复焦点只核对已知 held 的当前 OS VKey level，不增加后台键盘采集，不把全局 level 当作每设备完整历史。mania 和 BMS 共用接收时间／epoch 队列，各自仍消费原动作与真实绑定解析器，不引入玩法间依赖。
3h. 原生来源由主游玩管理器显式取得，恢复层等辅助管理器不得覆盖它。`UseParentInput=false` 不能同时代表暂停控制失效与回放独占：允许的暂停控制即时进入 Router，note 判定仍隔离；回放不接受父输入。
4. 输入行为、硬件口径或验收结论变化时，同次更新本线实际受影响的状态、计划、约束和验证记录；仅影响全局优先级、release gate 或硬约束时向 mainline 回写摘要与链接。同步规则以 [AGENTS](../../../AGENTS.md#权威与文档) 为准，不机械刷新无变化文件。
5. 若 desktop public settings surface 需要裁剪 upstream `MouseSettings` / `TouchSettings` / `TabletSettings`，必须只在 `OsuGameDesktop` 这类 desktop 宿主层安全隐藏；不得在 `OsuGameBase`、runtime config 或 input handler 层直接删除，否则会连带改写 test scene / 非 desktop host 行为。
6. 安全隐藏输入设置分区不等于删除输入语义：`MouseDisableButtons`、`MouseDisableWheel`、`ConfineMouseMode`、`TouchDisableGameplayTaps` 与 tablet/touch/mouse handler 的运行时消费链不得因此断开。
