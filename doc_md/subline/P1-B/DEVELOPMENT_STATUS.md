# P1-B 开发进度：输入语义与硬件验收

> 最后核对：2026-10-26（Start修饰轴、双击及模式组合；实体设备门保持）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，当前执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

## 当前阶段

- 当前代码已具备可运行的输入主链。
- 当前仓库已有 keyboard / Raw Input / XInput / MouseAxis / DirectInput HID 主链；`OmsInputRouter` 已改成 shared-action 引用计数，`BmsInputManager` 只在全局首个 press / 最终 release 时转发 `BmsAction`。
- BMS／mania 共用接收时刻与 clock epoch 队列，各自仍使用本玩法的动作和绑定解析器。Windows 先汇总实体键盘，重复 down 不增加引用，拔出一台只释放其实体键；释放沿保留按下时的布局／NumLock 身份。原生不可用或不能覆盖整个绑定组时保留框架路径。
- 主游玩区域明确持有原生键盘，恢复层不抢占；中途接管仍配平普通键盘松开，同键最后来源才释放。BMS 暂停时配置按键即时生效，音符判定仍隔离，回放保持独占。
- HID 轴明确区分相对位移、环形编码器、位置按钮与摇杆模拟运动；鼠标仅相对位移。灵敏度、累积运动阈值、位置阈值／回差、死区、实际时间积分和停止等待由同一解释器消费；旧绑定保留相对脉冲默认值。
- 运动攻击与长皿持续状态分开；连续运动可继续打普通皿，停止／断开／epoch 重置按来源结束持有。持续位和原 16 轨一同录制／播放，不改变计分、窗口或 TOTAL；具体长条合同归 [P1-E](../P1-E/TECHNICAL_CONSTRAINTS.md)。
- 按住Start时，校准后的实际轴运动转入原生调速/挡板消费，不产生皿攻击；带符号余量按来源累积，反向亚单位抵消，失焦/epoch等重置清理。双击第二下立即切换上下挡板；原EFFECT动作显示名为“游玩：挡板／调速”，单按选择用鼠标滚轮调节的上挡板、下挡板或LIFT，按住Start再按此键切换普通调速与浮动调速。绑定行长说明显示省略号并可悬停查看全文，保留右侧按键空间；真实源状态与Omni面板同步。原版观察、软件/Player证据和范围见[游玩Start复核](../../other/OMNI_GAMEPLAY_CONTROLS_20261025.md)，鼠标counts与圈数映射不代签实体手感。
- Windows 默认 HID backend 仍为 DirectInput，HidSharp 仅保留 `OMS_ENABLE_HIDSHARP=1` 诊断入口。DirectInput 显式 Acquire 并读取有序缓冲，快照不再代替完整 tap；溢出明确失败并释放，不伪造丢失历史。设备发现／退休仍唯一后台所有者，已连接设备持续采样，缺席设备500ms退避。
- 新 HID 绑定指向具体实例并显示名称；旧型号标识只接受唯一匹配，多台歧义不任意认领。实例标识不承诺跨换口／驱动重装不变。配置和持续信号测试归 [P1-D](../P1-D/DEVELOPMENT_STATUS.md)。
- desktop public settings surface 现已通过 `OsuGameDesktop.CreateSettingsSubsectionFor()` 安全隐藏 upstream 的 `MouseSettings` / `TouchSettings` / `TabletSettings`；这不等于删除 mouse/touch/tablet runtime config 或 handler，只是停止把非 OMS 通用 subsection 暴露给最终桌面产品面。
- `TestSceneOmsScratchGameplayBridge` 已有 loaded-scene 回归，覆盖 mouse-axis / HID-axis / XInput、mixed-source suppression、hold survival 与 takeover 等边界；这些软件场景不等于真实控制器覆盖。
- 实际入口、批次时间和性能范围继续取[2026-10-21输入审查](../../other/INPUT_SIGNAL_REVIEW_20261021.md)；本次Start增量取[游玩Start复核](../../other/OMNI_GAMEPLAY_CONTROLS_20261025.md)。剩余重点是正常窗口与实体设备验收。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| desktop 输入设置公开表面 | 已完成 | upstream mouse/touch/tablet subsection 已安全隐藏；runtime chain 保持不变 |
| mixed-source runtime 语义 | 软件实现已接通 | 引用计数、真实入口、运动／持有与回放；实体设备清单待签收 |
| 真实 HID 验收 | 未开始 | 仍缺真实 IIDX/BMS 控制器覆盖 |
| 对外硬件行为口径 | 进行中 | “Windows 默认 DirectInput + HidSharp 诊断后端”已可写入文档 |

## 最近一次验证

2026-10-26说明与显示修正：Core.Tests及Game/BMS依赖Release构建通过，绑定行4项回归通过，覆盖长说明、窄行/缩窄、新增绑定和恢复默认按钮状态；文档检查与diff通过。此前“对象/模式”简写已换为实际挡板、鼠标滚轮及普通/浮动调速说明，旧绑定保留；下方游戏输入矩阵未重复运行。

2026-10-26：Start双击/长按、模式组合、按住时的真实轴消费与不攻击音符、重置和带符号余量纳入BMS完整回归3050通过/19跳过。最终同包10组BMS普通Player控制取景通过，真实帧缓冲及源状态共同核对；宿主活跃状态显式模拟、动作软件注入，未代签前台或实体设备。失败、复跑与准确软件/画面范围见[专项记录](../../other/OMNI_GAMEPLAY_CONTROLS_20261025.md#本轮验证)。

2026-10-21：实际结果、命令和证据只取 [输入审查的验证记录](../../other/INPUT_SIGNAL_REVIEW_20261021.md#验证证据)；输入接收／轮询观察时刻不等于物理触点时间，软件场景不代签硬件延迟。

此前输入专项、独立 Windows 宿主与完整运行保留原日期，见 [CHANGELOG](CHANGELOG.md)。

## 文档治理验证

2026-09-29：复核 [BmsInputManager](../../../osu.Game.Rulesets.Bms/Input/BmsInputManager.cs)、[OmsInputRouter](../../../oms.Input/OmsInputRouter.cs)、HID backend 与 [scratch bridge 测试源码](../../../osu.Game.Rulesets.Bms.Tests/TestSceneOmsScratchGameplayBridge.cs)，回读9月22日 TRX 并为软件验证补齐日期和范围；未重新构建或测试，真实硬件门保持。此前审查见 [CHANGELOG](CHANGELOG.md)。
