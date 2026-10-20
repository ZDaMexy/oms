# 2026-10-21 设备输入审查与校准

## 范围与基线

用户要求审查并优化现有键盘、HID、手柄与模拟轴链路，通过分析 spice2x 明确 OMS 的实际需要。基线为 `bbe3afd5a7b816b0dfff57c5fbae007903a41abc`，开始时工作区干净，`master` 比已 fetch 的 `origin/master` 领先 36 个提交。本轮不启动或修改原 IIDX HDD，不读取正式玩家数据库，不更新私下分享的 Omni 包，也不实施此前仅评估的 IIDX 图表、判定面板或入场动画。

正式行为合同由 [P1-B](../subline/P1-B/TECHNICAL_CONSTRAINTS.md)、[P1-D](../subline/P1-D/TECHNICAL_CONSTRAINTS.md) 和 [P1-E](../subline/P1-E/TECHNICAL_CONSTRAINTS.md) 持有。本页保存本轮发现、外部研究和可复查证据。

## spice2x 的研究结论

来源固定为官方仓库提交 [`c83496776e4dfa06f286e483d21fd269c331505c`](https://github.com/spice2x/spice2x.github.io/commit/c83496776e4dfa06f286e483d21fd269c331505c)，而非用户 HDD 内 spice2x 的版本；未下载、编译或运行该项目。

| 观察到的设计 | 对 OMS 的判断 |
| --- | --- |
| 设备绑定、方向、范围、反转和现场预览集中在配置面；有设备通知与热插拔处理 | 玩家需要能辨认具体设备、看到原始信号与最终动作，并在现场调参数；不能只显示不明设备编号。来源：[配置面](https://github.com/spice2x/spice2x.github.io/blob/c83496776e4dfa06f286e483d21fd269c331505c/src/spice2x/overlay/windows/config.cpp)、[热插拔](https://github.com/spice2x/spice2x.github.io/blob/c83496776e4dfa06f286e483d21fd269c331505c/src/spice2x/rawinput/hotplug.cpp)。 |
| 输入入口更新内部状态，游戏适配侧读取机台当前值；IIDX 的数字转盘路径会在 `get_tt` 调用时推进 | 这是 HDD 机台接口的任务。OMS 自己负责判定与回放，必须保存边沿顺序和时间，不能把“本轮最后仍按着什么”当作完整输入，也不能用读取次数决定转盘速度。来源：[状态读取](https://github.com/spice2x/spice2x.github.io/blob/c83496776e4dfa06f286e483d21fd269c331505c/src/spice2x/cfg/api.cpp)、[IIDX 转盘](https://github.com/spice2x/spice2x.github.io/blob/c83496776e4dfa06f286e483d21fd269c331505c/src/spice2x/games/iidx/iidx.cpp)。 |
| analog 的 relative 处理包含按偏转和经过时间积分到环形位置；另有平滑历史 | 界面名相似不代表信号相同。鼠标位移、编码器位置和摇杆偏转必须明确区分；平滑会增加响应等待，不应默认加到判定前。来源：[analog 变换与平滑](https://github.com/spice2x/spice2x.github.io/blob/c83496776e4dfa06f286e483d21fd269c331505c/src/spice2x/cfg/analog.cpp)。 |
| XInput 有独立轮询与摇杆死区处理 | 不能假定 HID 选项会自动作用于所有手柄入口，也不能只凭后端名称承诺更低延迟。OMS 的设备选择和诊断必须说明实际入口。来源：[XInput](https://github.com/spice2x/spice2x.github.io/blob/c83496776e4dfa06f286e483d21fd269c331505c/src/spice2x/rawinput/xinput.cpp)。 |
| 原生报告可记录软件接收时刻 | 接收戳不等于物理开关闭合时刻。没有外部测量，不能比较两套软件的端到端延迟。来源：[原生报告](https://github.com/spice2x/spice2x.github.io/blob/c83496776e4dfa06f286e483d21fd269c331505c/src/spice2x/rawinput/rawinput.cpp)。 |

因此本轮保留 OMS 现有后端，修复真实入口、时间、来源引用和设备生命周期；校准与诊断复用游玩解释器。没有移植 spice2x 的机台状态结构、游戏适配代码或平滑算法。

## 审查确认的缺陷

1. **鼠标与手柄的真实入口未被既有测试覆盖。** 固定 Framework 的 `PassThroughInputManager.Handle()` 直接转换父输入，相关 `OnMouseMove`／`OnJoystickPress` 覆盖不会自动被调用；输入管理器自己的子队列也不回调自身。直接调用 `TriggerMouseAxisDelta`／`TriggerXInputButtonPressed` 的桥测试证明不了真实父事件路径。固定来源：[PassThroughInputManager](https://github.com/ppy/osu-framework/blob/ba04d538f02cecaabe5ab7dc3b102cd47d59d785/osu.Framework/Input/PassThroughInputManager.cs)、[InputManager](https://github.com/ppy/osu-framework/blob/ba04d538f02cecaabe5ab7dc3b102cd47d59d785/osu.Framework/Input/InputManager.cs)。
2. **普通鼠标位置差会在边缘失效。** Framework 的用户输入位置会先被限制范围，下游 `MouseMoveEvent.Delta` 不能保证保存原始移动量。仅设置 `UseRelativeMode` 不会把丢失的位移补回来。使用现有窗口相对事件，不另建 Raw Input 注册；Win32 同进程同设备类别后注册会覆盖前注册。来源：[UserInputManager](https://github.com/ppy/osu-framework/blob/ba04d538f02cecaabe5ab7dc3b102cd47d59d785/osu.Framework/Input/UserInputManager.cs)、[MouseHandler](https://github.com/ppy/osu-framework/blob/ba04d538f02cecaabe5ab7dc3b102cd47d59d785/osu.Framework/Input/Handlers/Mouse/MouseHandler.cs)、[Microsoft 注册合同](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerrawinputdevices)。
3. **DirectInput 只读当前快照会漏掉轮询间的完整 tap。** 改为原生缓冲边沿，首次显式 Acquire，并保留报告顺序、位置能力和接收信息；溢出按明确失败释放设备，不用当前快照伪造丢失历史。
4. **设备发现与销毁不能阻塞游玩或设置操作。** 已连接设备持续轮询，发现与退休资源在单一后台所有者处理；设置取消、替换和关闭撤销 UI 所有权，后台仍负责最终释放和报告异常。
5. **同型号设备不能凭 VID/PID 随机认领旧绑定。** 新绑定使用 DirectInput 具体实例标识和可读名称；旧标识只在唯一候选时解析，多个候选要求重新选择。实例标识并不保证跨换口、驱动重装永远不变。
6. **键盘需要先汇总实体设备，再输出逻辑键。** 一台键盘自动重复不增加持有引用；另一台仍按同键时，拔出第一台不能松开动作。释放沿使用按下时捕获的身份，避免 NumLock／布局变化造成卡键。暂停／来源切换保留已见实体的抑制状态，避免恢复后的系统重复变成新攻击；恢复焦点时只核对已知持有键，清掉在其它应用已松开的旧状态，不增加后台键盘采集。OS 核对是全局 VKey 状态，无法据此证明多台同键设备在失焦期间各自的完整历史。mania 使用与 BMS 相同的接收时间队列，交付仍进入其真实 Framework 绑定解析器。
7. **运动脉冲与长皿持续状态不同。** 每次有效运动仍能攻击普通皿音符；最近运动维持一个单独的持续状态，停止期限到达才松开长皿。不能靠永久按住普通皿动作掩盖长条问题，否则连皿会失去后续攻击。
8. **历史子帧不能轮询并倒填当前设备输入。** 外层冻结设备批次，在其可确认的观察时刻交付；运动到期只是下一输入时刻，不能让到期子步读到更晚的实体状态。epoch 改变要同时丢弃轴基线、摇杆偏转、运动期限和旧报告。
9. **释放回调中的 `PressedActions` 仍包含正在释放的动作。** 长条判断其它来源是否还持有时必须排除本次释放位，否则普通键盘松开也会被吞。来源：[固定 KeyBindingContainer](https://github.com/ppy/osu-framework/blob/ba04d538f02cecaabe5ab7dc3b102cd47d59d785/osu.Framework/Input/Bindings/KeyBindingContainer.cs)。
10. **输入统计不能把运动持续位算成第二次击打。** KPS 只计实际轨道攻击；计数窗口通过有序时间边界查找，避免每帧扫描整首歌的全部历史，并保留回退删除未来历史的行为。
11. **结束录制不能只让旧组件过期。** 回退到旧生命周期可能让过期录制器再次收到事件，把早时刻追加到已结束的回放，首次实测出现 `frameStart > frameEnd`。结束录制现明确封闭写入，BMS 元数据结束仍调用该共同终态；判定回调改为可正确卸订的同一实例方法，避免每次重新创建闭包后卸订失败。修复没有排序或修补非法回放来掩盖错误。
12. **暂停与回放不能共用父输入提前退出。** 真实 Player 暂停会设置 `UseParentInput=false`，但预开局调速控制仍须处理父事件；整体提前退出会让 Q／Z 无法工作。暂停输入现即时进入 Router，继续由既有门控阻止 note 判定；回放保留独占。完整回归中的普通开局失败经隔离单跑通过，暂停项单跑稳定失败，不能把两项都当作独立故障或修改测试清键掩盖问题。
13. **恢复层不能抢占主游玩键盘。** mania 的恢复界面还会创建一个输入管理器，无条件注册会覆盖真正接收音符输入的主管理器；现在由真实主区域显式取得来源，恢复层继续走普通框架输入。新测试同时核对注册次数与实际所有者。另修正测试的取证位置：命中后的音符可能已回收，判定在真实 `NewResult` 时按原谱面对象复制，不能事后枚举池化 Drawable 当作成绩证据。
14. **同一时钟周期中的浮点投影不是倒带。** 独立 QPC／谱面锚可让同一时刻反投影少一个可表示值；运动 tracker 用它推断倒带会清掉新运动，到期反投影又可能低于自身截止点。真实加载回归记录到同一个停止时刻访问 1493 次且持续状态未释放。真实倒带仍由管理器的时钟周期重置处理；访问自身已公布的到期停表后，只推进本次到期比较，不修改其它输入、判定或录制时刻，也不加 epsilon／等待参数。
15. **原生接管不能吞掉普通键盘的配平释放。** mania 在原生 inactive 时可通过 SDL 开始持有；进入 Playing 后，该实体的原生状态仍可能处于 suppressed，Raw break 不交付动作，SDL up 又被 active 回声门吞掉。框架当前键状态在事件前已清空，后续同步也不能救回绑定解析器。两路须分别记录真正开始的键引用，同键最后来源松开才释放；手柄仍由真实基类绑定合并，不能靠全局松开动作修复。

## 玩家配置与数据边界

绑定仍按 BMS 变体保存。键盘和标准手柄按钮沿既有按键设置；补充编辑器选择具体 HID 按钮／轴或鼠标轴。轴有四种明确用途：

| 用途 | 输入与输出 | 主要参数 |
| --- | --- | --- |
| 相对位移 | 每次位移变成有方向的运动／攻击 | 灵敏度、累积运动阈值、方向、停止等待 |
| 环形编码器 | 有范围的绝对位置，取跨圈最短位移；首次只建基线 | 位置范围、灵敏度、累积阈值、方向、停止等待 |
| 位置按钮 | 相对中立位置的偏转，过按下阈值后持有，低于松开阈值才结束 | 范围／中立位置、正负方向、按下／松开阈值 |
| 摇杆模拟运动 | 去死区后的偏转按实际经过时间积分，回中停止产生运动 | 死区、满偏速度、灵敏度、累积阈值、方向、停止等待 |

相对位移的累积阈值单位是设备计数，环形／摇杆模拟是圈数，反向运动抵消尚未到阈值的累积。停止等待不延迟首次输入，只定义运动停止多久后松开长皿。没有默认平滑或防抖；鼠标只开放相对位移。范围和中立校准在高级区域，不能用轴能力自动猜测玩家用途。

原有未保存轴设置的绑定保持既有相对脉冲默认值。配置在反序列化或编辑提交边界验证；无效设置要明确报告，不能静默变成别的模式。实时测试显示原始报告和解释后的动作，不把试按注入游玩，也不改写已保存配置；保存仍由明确的应用操作完成。

玩家入口为「游戏模式 → BMS → OMS 扩展绑定」：先选变体和设备，再为轴选择用途；每行的「测试设备信号」可边观察边调参数，最后应用保存。键盘与标准手柄按钮继续在原按键设置绑定，不要求为使用新校准功能重做已有键位。

回放在原 16 轨之外使用 legacy `MouseX` 掩码保留第 20／21 位运动状态，总值小于 `2^24`，可精确经过既有单精度转换。旧轨道位和旧回放默认行为保持；运动位本身不命中 note head，不进入计分或 TOTAL。长条仍按原 LN／CN／HCN 合同消费实际持有，回退读取同一回放状态。

时间域明确分开：键盘／相对鼠标使用 QPC 接收时间；HID 保留整批外层软件观察时间，不承诺逐报告物理时刻或原生毫秒戳已精确映射。摇杆模拟速度与运动停止等待按配对时钟反投影后的实际毫秒推进，运动期限再映射为 gameplay clock stop；1x／2x不能改变同一50ms等待的手感。无配对时钟的隔离预览沿其提供的毫秒时钟。

固定 `ppy.osu.Framework 2026.303.0` 的 SDL 相对事件与模式状态位于内部 `ISDLWindow`，并非公开窗口 API。Desktop 在加载时核对真实接口及签名，一次性绑定强类型委托；每个输入事件不执行反射，也不注册另一原生输入窗口。缺少该契约或现有 MouseHandler 会明确启动失败，依赖升级必须复核此处。普通窗口捕获、DPI 与失焦行为仍属于下方人工门。

## 验证证据

原始日志、TRX、复跑脚本与对照 JSON 保存在 [本轮证据目录](../../artifacts/input-signal-review-20261021/)。每个新 shell 均先执行 `. .\UseDevelopmentStorage.ps1`；构建、格式与测试由主执行者串行运行，相关源码冻结。最终完整运行使用格式后重新编译的 Release 产物，没有用旧程序集代替当前源码。

### 最终软件门

| 验证 | 实际结果与产物 |
| --- | --- |
| Desktop Release | `dotnet build osu.Desktop.slnf -c Release --no-restore -m:1` 成功，0 error；最终交接修复后为 `desktop-release-final-handover.log`。保留原包漏洞提示与两个未修改测试文件的 CS8600／CA2007 警告，不称零警告 |
| BMS 修复定点 | 65／65 通过，含真实 Player 预开局、运动 LN／CN／HCN、父输入、独立时钟锚与回放；`bms-final-fixes.trx` |
| mania 输入定点 | 33／33 通过，含 20 个真实加载场景与 13 个实体键盘状态用例；`mania-input-handover-green.trx` |
| 选择夹具隔离 | 前序故意损坏 owner＋真实外部选择用例同场景 2／2 通过；`skin-candidate-isolation-green.trx`。只隔离本用例的测试 Realm 候选，不改产品选择规则 |
| shared 输入／回放／KPS | 格式后重编译并复跑 34／34 通过；`core-input-final.trx`，覆盖 `GameplayInputQueueTest`、`TestSceneClicksPerSecondCalculator`、`FramedReplayInputHandlerTest`、`TestSceneFrameStabilityContainer` |
| BMS 完整 | 2974 通过、0 失败、19 跳过，共 2993；`bms-full-complete.trx`。与当前 HEAD 对应留存基线逐身份对照，原有保留用例结局无变化；新增 136、退休 1，见 `bms-full-complete-comparison.json` |
| mania 完整 | 993 通过、0 失败、2 跳过，共 995；`mania-full-complete.trx`。相同基线逐身份对照，新增 33、无退休或旧用例结局变化；`mania-full-complete-comparison.json` |
| SDL 固定契约 | `sdl-contract-probe.ps1` 与实际 `sdl-contract-probe.json` 确认本机 2026.303.0.0 的两种 Windows SDL 窗口都能绑定 getter／事件委托；只创建未初始化托管目标来绑定，不运行构造器、窗口或委托，不注入输入 |

自动跳过的备份根、素材取景或专用宿主用例仍须按原手动入口执行；没有借用正式玩家数据补齐这些签收。

完整回归按 `className::testName` 比较，基线取 HEAD 对应的 `omni-mania-note-visibility-20261020` 结果，未拿更早的失败数量代替。退休项为 `OmsDirectInputPollingTest.TestOneNativeSnapshotPerGameplayPollKeepsButtonAndAxisEdges`；原快照断言已由 `TestBufferedCompleteTapAndAxisOrderSurviveOneGameplayPoll` 等有序缓冲、完整 tap 与首次快照用例接替。

### 失败定位与有效修后证明

- 首次输入定点为 273 通过、12 失败。录制停止后又回退造成乱序帧，是生产缺陷；旧 UI 测试的 `[TearDown]` 在实际步骤执行前释放闭锁，是夹具生命周期错误。修复后相同相关 26 项通过，原始失败留在 `bms-input-focused-initial.trx`，修后为 `bms-input-fixes.trx`。
- 第一轮完整 BMS 为 2970 通过、2 失败、19 跳过，共 2991 项。未暂停开局单独运行通过，暂停项单独稳定失败；修正真实父输入提前退出后，整组 27 个预开局用例通过，见 `prestart-normal-red.trx`、`prestart-paused-red.trx` 与 `rounding-prestart-red-compiled.trx`。失败后的连锁现象没有通过清键或放宽断言掩盖。
- 第一轮完整 mania 为 973 通过、17 失败、2 跳过，共 992 项。定点诊断证实主管理器未拥有来源；普通回退路径已产生真实 Key1 动作与录制，命中音符随即离树。只修正真实来源所有权与判定取证位置；此前 17 项全在修后 30 项门内通过，诊断原件为 `mania-native-diagnostic.trx`。
- 独立时钟锚探针先修正测试包装器提前复制未初始化设备来源的问题，恢复原延迟工厂。随后有效失败先暴露同一时刻反投影被误当倒带，再在 `rounding-expiry-red.trx` 记录同一停表访问 1493 次、motion 仍持有且 NextInputTime 不变。修后真实停表只访问一次，追赶继续，唯一运动释放按原精确时刻录制；另验证其它来源跨舍入仍保留。
- 首次修后完整 mania 已 990 通过、2 跳过，但未覆盖途中交接。新增用例在 `mania-key-handover-red.trx` 确认先释放原生键盘后，仍按着的普通键盘不能维持长条；同批另一项因前一失败残留按键停在首按，不能拿它证明独立故障。隔离新宿主后，`mania-key-handover-single-red.trx` 在旧普通按键应释放处稳定超时。修复只合并两路真实键引用，原断言保留，完整 33 项输入定点通过，并核下一真实音符的 Perfect／RawTime、唯一录制与手柄最终释放。
- 中间 BMS 完整运行为 2973 通过、1 失败、19 跳过，失败为 `TestExternalFolderWorkspaceExplicitSelectionIsFreshAndImplicitSelectorsExcludeIt` 等待随机提交。该次日志没有保存抽到的目标，不能倒推具体 ID。增加候选断言后，`skin-candidate-isolation-red.trx` 在两个同场景用例中明确发现前序留下的 `changed-owner` 非外部候选；产品会依法拒绝，测试却无条件等成功。只隔离本用例的测试 Realm 候选、准备完整 control 并保留真实随机／前后选择，修后相同两项通过；不删目录、不改变生产选择策略或延长等待。
- 最初 Desktop 直接引用不存在或不可公开访问的 SDL 窗口类型，编译失败；通过核对真实本机程序集并绑定固定内部契约修正，最终 Release 成功。没有用静默关闭鼠标功能来通过编译。

### 性能证据的范围

稳态专项保留原 256 次热身与零分配断言。DirectInput 6000 个完整按钮 tap 的后续分段取样与完整回归内取样均为 0 字节，按下／松开各 6000 次；环形／摇杆 16384 次轮询、32768 个运动回调的后续取样均为 0 字节。共享接收队列、鼠标与原生键盘等专项的零分配断言也随对应软件门验证。

首次按钮专项曾读到 2128 字节、环形／摇杆专项曾读到 24 字节；这两次初读的原因尚未定位。后续增加分段取样而未改变热身次数或放宽零分配要求，没有复现；不能将首次读数归因于已排除的 500ms 设备发现或凭空声称已找到分配根因。上述证据只覆盖给定组件和输入负载，不证明整局零 GC、前台帧时间改善或所有机器零卡顿。

### 格式与文档

`format-desktop-final-verify.json` 为空数组。Desktop 改动集的样式验证仅排除 `BmsLane.cs` 中经 HEAD 复核仍存在的三个旧 protected 名称 `createLookup`／`createHitTarget`／`createHitObjectArea`，不为格式检查改动原受保护 API；该文件的空白检查已通过。交接修复涉及的三个文件另行自动格式化、重编译并通过 `format-handover-final-verify.json`；core 两个测试文件的 `format-core-final-verify.json` 为空数组。

最终 `CheckDocumentation.ps1` 通过：208 个 Markdown、2364 个相对链接、548 个本地锚点、123 个 memory wiki 链；保留既有通用绝对路径／公开制品指纹提示，原件为 `docs-check-final.log`。`git diff --check` 通过。没有因文档治理刷新硬件、实网、生产或发行状态。

## 未完成的人工门

- 正常 VS Code 非调试窗口的设备选择、捕获、持续测试、失焦／恢复及设置关闭体验。
- 实体键盘多设备、实际 HID 转盘、手柄及热插拔；含不同报告频率、范围、USB 口和同型号多台。
- 真实谱长皿与方向反转手感、停止等待调节，以及输入至声音／画面的外部延迟测量。
- 正常窗口前台帧时间与长期稳定性。软件零分配专项不代表整局零 GC 或所有机器零卡顿。

以上人工门仍归 P1-B／P1-D／P1-E 并由 P1-G 汇总，不把外部源码研究或软件测试当作硬件签收。
