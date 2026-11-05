# Standard 皮肤实际 Player 取景

此工具只在隔离存储中通过普通 OSK 导入、模式选择、原生 Player 输入与计分和普通 Results / FailOverlay 获取证据，不读取正式玩家数据。常规取景使用 ReplayPlayer；读谱设置与失败专项使用真实 BmsSoloPlayer。合成谱证明此次包的实际绘制与计分路径，不代替原版游戏、完整真实歌曲或实体设备验收。工具不携带原版素材，也不使用 Lightning 成品。

先从当前源码准备合格的 OMS runtime。每次新 shell 先执行仓库根的 `UseDevelopmentStorage.ps1`；再构建本工具，`CaptureRuntimeDirectory` 指向该 runtime，`OutputPath`、`BaseIntermediateOutputPath` 明确指向非系统盘。宿主运行目录须包含其引用的程序集、`runtimes/win-x64/native` 中的原生依赖和 `Skins/Canonical`，不得从现有程序目录复制 `data`、`cache`、日志或玩家配置。宿主启动时检查当前架构的原生目录和 SDL2，仅将该目录加入自身进程的 PATH，不修改用户或系统 PATH。全部测试共用一份程序依赖即可，不为每个组合复制整套程序。

```powershell
. .\UseDevelopmentStorage.ps1
dotnet build tools/StandardSkinCapture/StandardSkinCapture.csproj -c Release `
  -p:CaptureRuntimeDirectory='E:/OMS-Development/evidence/standard-model-20261105/player/runtime' `
  -p:OutputPath='E:/OMS-Development/evidence/standard-model-20261105/player/runtime/' `
  -p:BaseIntermediateOutputPath='E:/OMS-Development/evidence/standard-model-20261105/player/host-build/' `
  -p:MSBuildProjectExtensionsPath='E:/OMS-Development/evidence/standard-model-20261105/player/host-build/' `
  -p:AppendTargetFrameworkToOutputPath=false `
  -p:AppendRuntimeIdentifierToOutputPath=false
```

工程显式包含本地 C# 文件和公共 ExactVisualTestRunner，不扫描历史 `bin/obj` 中的源码；切换中间输出目录无需删除旧目录。

运行参数均通过 CLI 提供，未知参数、重复值、错误模式和不匹配的 OSK SHA256 会失败。`--output` 必须尚不存在；`--temp-root` 缺省为该目录下的 `temporary`，进程会在创建宿主前将自身 TEMP/TMP 设到这里。运行程序、临时存储及输出都拒绝系统盘和 reparse 路径。皮肤布局以 `--presentation` 指定，不能同时在 `--options` 内另写 `layout`。

```powershell
& 'E:/OMS-Development/evidence/standard-model-20261105/player/runtime/StandardSkinCapture.exe' `
  --package 'E:/Path/Standard Model Skin Omni Until 26.osk' `
  --sha256 '<完整成品SHA256>' `
  --output 'E:/OMS-Development/evidence/standard-model-20261105/player/bms7-p1' `
  --ruleset bms --keys 7 --presentation p1 --long-note-mode cn `
  --options '{"frame":"<包内实际选项值>"}'
```

BMS 支持 5/7/14K，mania 支持 5/6/7/8/14/16K；单舞台使用 `p1/p2`，14/16K 使用 `dp`。BMS 长条为 `ln/cn/hcn`，缺省 `cn`。包实际未声明的选项不会被猜测或静默替代。

`--chart` 缺省为 `standard`，沿用普通矩阵的原合成谱。四位 Combo 专项使用 `--ruleset mania --keys 16 --presentation dp --chart combo-stress`；其它模式、键数或布局不能使用 `combo-stress`。该谱从 2000 毫秒开始，以 125 毫秒行距、每轨 0.2 毫秒偏移生成 64×16 个原生对象：前 63 行为 Note，末行为 700 毫秒 HoldNote，同轨长条不重叠，末行头部位于 9875～9878 毫秒，最后结束于 10578 毫秒。10100 毫秒取景位于末行判定后的 500 毫秒内；该自然取景和完整计分时都要求实际 `ScoreProcessor.Combo` 至少达到 1000，回执保留真实当前 Combo，不向皮肤注入数字。最终计分、full combo 与自然 Results 继续由原生玩法判定，不硬写预期最终 Combo。`host-started.json`、`chart.json`、`player-chart.json`、游玩帧和完成回执均记录 `ChartVariant`，与普通矩阵分开取证。

`--chart long-note-transitions` 使用构造的真实 Replay 帧，由普通 `ReplayPlayer` 和各玩法的原生回放输入器消费；不添加 Autoplay 或 NoFail，不注入皮肤事件。BMS 以 `--long-note-mode cn` / `hcn` 分别验证原生不可重新按下 / 可重新按下的差异，mania 使用自身 HoldNote 作为回归。默认 `standard` 和 `combo-stress` 的自动演奏路径保持不变。

| 长条区间（毫秒） | 实际回放输入 | 检查目的 |
| --- | --- | --- |
| 4000–8000 | 4000 按下、5200 松开、6400 再按、8000 松开 | 持续、释放、HCN/mania 重新按下，CN 提前结束 |
| 10000–14000 | 漏头，11600 才按下、14000 松开 | 直接漏头无凭空瞬间闪光，HCN 断开与恢复 |
| 16000–20000 | 16000 按下、19960 提前松开 | 真实尾判立即完成，源终端闪光播完，谱面 EndTime 不改 |
| 22000–24000 | 准时按下与松开 | 实际固定判定线光效池复用，不继承上一物件的时钟 |

每段同时含三条长条；BMS 使用白键、蓝键、皿，mania 使用前三列。其它轨道的正常输入持续到 28000 毫秒，使真实成绩自然结束。输出九组 `long-note-*.png`、游玩快照与 `*-native.json`，逐帧只读收集实际 object state、PreviousState、StateChangedAt、LastLongNoteTransitionAt、exact object ID、固定线视觉池租约和原动画已应用帧/可见纹理片段；另有完整 `native-input-sequence.json` 与 `long-note-transitions-complete.json`。此专项要求真实存在漏判，不能以 Full Combo 验收；它跳过 Full Combo 尾声取景，继续普通 Results。回执只能说明本次原生回放行为与实际图片，不能代签实体设备输入、seek/retry 或其它尚未运行的场景。

`--assist` 缺省 `none`；`--assist scratch` 只允许 BMS 的 `standard` 谱。此入口添加真实 `BmsModAutoScratch`，使用普通 Replay，并从生成输入中删除全部 scratch attack/motion；不携 Autoplay/NoFail，不给皮肤注入辅助角色。ASCR Mod 的 Tint 明确关闭、原生可见性保留 Visible，使两次 `--options '{"auto-scratch-display":"true"}'` / `false` 运行只改变作者显示项。四张普通游玩图同时输出 `*-auto-scratch.json`，要求实际存在 Scratch 角色和 None 普通键，关闭时所有被观察的 scratch 原音符纹理不可见、普通键仍可见；开启时须有真实纹理可见。`auto-scratch-qualification.json` 记录实际对象的 AutoPlay、CountsForScore 及长条头尾/身体资格，分数和命中数仍取真实 ScoreProcessor。只有原生 Full Combo 为真时捕获尾声；否则写明确的未具资格回执并自然进入 Results，不改变资格来满足取景。

`--chart reading` 只允许 BMS，配合 `--reading-mode normal|floating|classic` 分别运行三局，缺省为 `normal`。该入口继承普通 `BmsSoloPlayer`，使用原生新成绩与录制器，不添加 Autoplay/NoFail，也不调用 `SetReplayScore`。真实产品的 Autoplay 使用 ReplayPlayer 并独占输入，因此读谱专项必须保持普通玩家输入归属。进入与初始时钟 Reset 完成后，工具经 `BmsInputManager.TriggerOmsActionPressed/Released` 合成 `UI_PreStartHold` 和 `Key1P_2`；不在 LoadComplete 提前持有 Start，不直接调用 `SetAdjustmentHeld`，不写入皮肤事件或 GN/Preview 数字。回执明确这是合成原生输入 API 验证，未代签实体键盘或控制器。

读谱专项按 26 自身允许的组合取景：Normal/Classic 使用 LIFT+SUDDEN（80/125），Floating 使用 SUDDEN+HIDDEN（125/75）。关闭项保留配置值但不显示边缘读数；不以原版不存在的 Hidden+Lift 三开组合签原型。Normal 从 8 经十二次真实黑键输入升到 20，验证 1–10 与 11–20 分页，Floating 从 2.5、Classic 从 3.5 各调高一档。原标准谱实际含 150→180 BPM 变化，GN、实际 Hi-Speed、绿数范围和 Preview 均读取真实规则集状态。输出 `reading-held`、`reading-first-row`、`reading-adjusted`、`reading-ready` 四组图片、普通游玩快照及 `*-native.json`；要求实际源字形/首排 cue/READY 纹理已可见，Normal Preview 仅在 Start 保持时显示，另两模式隐藏它。保持 Start 直到真实准备倒计时耗尽，松开后等待自然 READY 与 chart clock 开始，再按原生生成器提供的动作计划，在 Player 更新中按 InputManager 实际谱面时钟投递到期 OmsAction 键/皿按松，并核对实际投递误差。计划从不挂载为 Replay；实际成绩、判定与录制内容来自普通玩家路径。`reading-native-chart-input-plan.json` 与 `reading-native-chart-input-complete.json` 保存计划时间、实际投递时间/动作和原生结果，继续四张游玩图、完整计分、Full Combo 尾声及 Results。Start 等待最多写一份初始/1.5 秒诊断快照与 12 条输入/reset 记录，不逐帧写文件。默认 `standard` 与 `combo-stress` 不启用此入口。

`--chart failure` 只允许 BMS，使用标准谱和真实 Hazard Gauge。独立 `BmsSoloPlayer` 先让原生录制器初始化本局元数据，再加载只有 0/60000 毫秒空输入帧的普通 Replay；不携 Autoplay/NoFail，不调用 `PerformFail` 或注入皮肤失败事件，也不采用 ReplayPlayer 的末帧延迟失败逻辑。实际漏判触发原生失败后，在独立失败呈现时钟达到 300/1500/2800 毫秒且源动画已存在可见纹理时取景，要求 chart clock、成绩及已计分命中数不再变化，原生输入已关闭，源动画 AppliedFrame 持续推进。最后等待并拍摄普通 FailOverlay。此分支不经过 Results、不宣称 Passed 或完整计分；`completed.json` 的成功仅表示失败场景验收闭合。可再以 `--options '{"judgement-display":"false"}'` 运行，确认原 Stage Failed 独立于 Judgement 开关。

`--chart failure-skip` 沿用相同真实 Hazard 失败路径，拍摄 300 毫秒后的源动画后，经测试宿主 `InputManager.PressKey(Key.Escape)` 按住 Esc，普通 FailOverlay 出现即松开；保留当前菜单按住确认设置，不直接调用退出、失败或呈现完成函数。外层测试时钟必须证明菜单比原三秒尾声至少提前 250 毫秒出现，chart clock、实际成绩和命中数始终不变，第一次 Esc 仍停留在 Player 的失败菜单。输出 `failure-skip-request.json`、`failure-skip-result.json`、源动画帧与普通失败菜单图。完成回执区分自然结束与 Esc 跳过；它不覆盖第二次退出、QuickExit、Retry 或实体设备验收。

每局输出包身份及运行程序集 SHA256、选择与精确 publication、全部材料来源、选项/轨道几何/绑定/时钟声明，以及各专项所述的自然取景和真实状态。常规完成谱面局输出四张游玩图、普通 Results 与真实成绩；失败专项输出源失败尾声和普通 FailOverlay。最终均以 `completed.json` 和 `host-exit.json` 闭合。运行失败或缺少完成回执均返回非零；图片文件写出不等于目视验收。宿主使用真实渲染 API 取景，不使用 computer use。展示完整 framebuffer 时连同 ManualInputManager 一起移动，保持实际游戏的键盘输入父树；不能只移动 Game 后向原空输入树按 Esc。

数值配置按实际数值核对，保留浮点档位归一化允许的舍入误差，不把`0.7`与其G17保存串不同误判为未应用。Normal读谱图还核对两条原生预览音符已有实际纹理、至少一条进入当前预览窗口，并记录各音符可见纹理数与屏幕矩形；仅有父对象active或phase变化不足以证明预览可见。

完整计分后还会确认真实 full combo 与保留的 `gameplay.complete` 事件，沿自然时钟等待至少 300 毫秒及实际已应用动画帧中可见的纹理片段，然后输出 `full-combo-outro.png`、对应游玩状态与 `full-combo-observation.json`。取景不 seek、不暂停时钟、不修改 alpha；采图后继续自然进入 Results。

`chart.json` 的 `rawWrapperObjects` 是转换前包装对象数，BMS 解码包装在此阶段为零；实际 Player 转换后的 `convertedHitObjects` 单独写入 `player-chart.json` 并要求大于零。该回执另记实际 Mods、曲目元数据、原生 BMS 长条模式及每条长条的模式；捕获要求原生模式符合请求、没有 NoFail，并在普通 Results 确认真正通过。异常日志保留异常链中键名明确的有限数值 `Data`，便于核对引擎给出的场景预算计数，不改变引擎限制。

BMS 每局在自己的 `bga-fixture/` 生成一张 640×480 四色棋盘测试图，含白边、四角编号及 `OMS BGA TEST` 字样；它只用于视口验收，不含原版图像。实际合成 BMS 文件通过 `#BMP01` 和 `#00104:01` 引用它，原生 BGA 播放器经 `WorkingBeatmap.GetStream` 读取该 PNG 的只读文件流。`bga-fixture.json` 保存谱面和图片 SHA256；每张游玩截图的 JSON 记录原生 base layer 实际选中的 image ID、文件、纹理尺寸、读取次数、BGA state/revision、各视口与实际显示窗口的矩形。截图前要求原生纹理确已加载，不能以默认背景或事件声明替代 BGA 图像证据。mania 继续使用原本的合成谱，不注入 BMS BGA。

既有内层测试存储依框架合同在退出时回收；输出证据与共享 runtime 字形缓存保持。工具没有清理旧任务产物的入口。
