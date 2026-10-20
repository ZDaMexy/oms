---
name: reference-bms-keyboard-threading
description: BMS/mania原生来源交接、卡顿事件时间与舍入停表、真实父输入和设备后台发现诊断
metadata:
  node_type: memory
  type: reference
---

# BMS／mania 键盘线程与来源召回

行为合同见 [P1-B](../../doc_md/subline/P1-B/TECHNICAL_CONSTRAINTS.md)，源码和真实回归的来源见 [2026-10-19 审查](../../doc_md/other/GAMEPLAY_REVIEW_20261019.md)。这里仅保存诊断地雷。

- Windows WndProc 安装在 host.InputThread，不能因接口名为 input 就同步调用 KeyBindingContainer.TriggerPressed；该调用不会替你切到 Update，真实命中会继续进入 Drawable transform mutation。先在 loaded note 的实际 NewResult 检查线程，再核录制/血量，不能用未加载 handler 单测代证。
- HashSet 去重只能消除重叠的 down，不能消除 raw down/up 与 SDL down/up 两批完整 tap。重现须两种批次顺序都跑，并观察真实 action/result，而非只检查 pressed 最终为 false。
- 按单键判断原生支持会把组合拆到两个批次；尤其原生可解 Ctrl、框架独有媒体键的组合。应在加载最终绑定时确定整体来源；单个 unsupported 键成功回退不证明混合 chord 正常。
- “hook 已安装”不等于“正在交付”。如果原生只在 Playing 交付、SDL ownership 却只看 installed，Break/暂停中允许的配置按键会两路都丢。使用既有 pre-start paused 用例核产品合同，再在真实 Windows source 中核当前交付范围。
- `UseParentInput=false` 同时用于真实 Player 暂停和回放。不能在实际父事件入口整体提前退出：暂停时 FrameStability 不更新子树，配置控制须即时交给 Router，note 判定仍由原门控隔离；回放继续独占。完整运行里一个暂停失败残留 Q 可使后续普通开局也失败，先隔离两项再确定根因，勿用清键掩盖生产错误。
- 原生回调给出左右实体修饰键，持久化组合可能保存虚拟 Control/Shift/Alt/Super；不能只用 pressedKeys.Contains 虚拟值，也不能释放左侧就清除仍由右侧满足的组合。
- Windows VKey 的字母值随键盘布局改变，而固定框架的字母/标点按 scancode；小键盘又有 NumLock 导航语义，不能把两者笼统改成全物理或全字符映射。核对本机 package 指向的固定 framework 源码，不猜 latest 行为。
- fake 支持能力须在 input manager 加载前设置。测试保存键位到 OsuTestScene.Realm 后，还须将该 RealmAccess 明确提供给实际 renderer；只修改测试 Realm、子树仍解析宿主 Realm 会让“组合键坏了”成为夹具误报。
- “首个Update已收到”不证明判定时刻正确。FrameStability卡顿后先走16.67ms子步，整批raw若在这里drain，两次tap的真实judgement与replay一起偏移；测试须在加载后的1000→1100期间排入1040/1080完整tap，严格核RawTime/offset与录制，而不是只在note当前时间触发。
- 单调接收戳须在音乐clock刚ProcessFrame后配对，不能等到input manager.Update才取锚。pause/seek/offset/source和ownership结束旧epoch；只逐外层帧观察replay会漏掉同帧attach/detach，需在实际handler切换处失效队列。来源与真实用例见[深审记录](../../doc_md/other/GAMEPLAY_PERFORMANCE_JUDGEMENT_20261019.md)。
- 降低发现频率不等于去掉同步阻塞。要用真正闭锁provider且gate持续关闭，证明PollOnce返回且已有控制器仍触发；设备通知发生在任务内、Dispose join及未提交candidate也须观察。资源退休改后台后，Release断言须等owner.Dispose drain，不能与Interlocked赢家尚未实际COM.Release的窗口竞速。
- `PassThroughInputManager.Handle` 自己转换父事件，不一定经过 Drawable 的 `OnMouseMove`／`OnJoystickPress`；KeyBindingContainer 的 action 队列也只遍历其子树，放在兄弟的 mirror 不能当作已接通。输入测试要从真实父 ManualInputManager 发送事件，同时看 Router、实际 note 与最终释放，不能只调公开 Trigger 方法。
- 下游 `MouseMoveEvent.Delta` 可能已经过窗口限幅。打开 relative 偏好或测试移动到窗内坐标均不能证明碰边后仍可打皿；检查固定 Framework 的原始相对事件入口、窗口捕获条件与 DPI 单位。不能另注册同类别 Raw Input 来抢现有宿主注册。
- 固定 Framework XML 列出成员不代表类型公开；2026.303.0 的 SDL2Window、SDL3Window 和 ISDLWindow 均为 internal，也没有 IDesktopWindow。Desktop 对真实接口一次校验并绑定事件／模式 getter 委托，升级依赖需复核；不能只凭 XML 名称提交不可编译的强类型引用。
- 长皿持续期限会使 FrameStability 停在历史子帧；若在那里 Poll 当前 HID，较晚报告会被写到过去。每外层冻结批次后再按可确认的观察时刻交付，时钟 epoch 重置要清摇杆偏转、编码器基线及旧报告，而不只清 keyboard queue。来源与本轮回归见 [2026-10-21 输入审查](../../doc_md/other/INPUT_SIGNAL_REVIEW_20261021.md)。
- 时钟域来回投影未必相等：同epoch的新QPC锚可让同一music time反投影少1ULP。不能据此自动清运动；实际seek由manager重置。自己公布的expiry停表已经到达，就须消费它，否则同一帧可能重复千次。到期cutoff与实际deviceNow分开，不能把修正后的deadline写回上一采样时刻，误清其它来源。
- mania 的恢复层也会调用 CreateInputManager；在每个manager.LoadComplete自动注册单sink会让隐藏恢复层抢走主键盘。由主DrawableRuleset显式激活，并在真实加载测试核唯一注册与owner。判定证据在NewResult时复制，池化音符事后可能已不在树中或Result已重置。
- SDL已向真实resolver交付down后才进入Playing时，native实体可能仍为suppressed且不会交付up；此时按active吞掉SDL up会永久留下resolver键。CurrentState在事件到来前已被PassThrough清掉，不能指望后续sync补救。分别保留真正交付的framework/native键引用，最后来源才释放，同action手柄继续由基类Unique合并；用中途切换及下一真实note验证，而非仅开场固定fallback。
- Framework 的 release 回调结束后才从 PressedActions 移除当前动作。判断“别的来源仍持有”须排除当前释放位；否则长条松键会被看作仍按着。运动持续位不能作为 note head 攻击，也不能增加 KPS；回放掩码须考虑实际 legacy float 精度，低于 2^24 的保留位才可和完整16轨精确往返。
- 原生键盘实体状态不能在每次最后 up 时删掉每设备字典；那会把每个下一次 tap 变成新分配。只在真实拔出或所有权重置时回收，稳态专项要覆盖反复按下松开，而不只覆盖重复 down。
