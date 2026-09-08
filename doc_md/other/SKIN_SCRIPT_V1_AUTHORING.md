# Skin V1 可选数值脚本

这是 C6 的公开脚本语言与工具说明；当前完成状态和自动结果以 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md) 为准。它不是 Lua、JavaScript、`.luaskin` 或 beatoraja runtime，也不需要作者编译 DLL。完整场景、布局和素材合同继续使用 C3～C5；C7 的 canonical 双包和完整 Authoring Kit 另行交付。

## 从作者源文件到普通皮肤

可直接编辑 [Momentum 候选源目录](skin-c6-candidate/README.md)，然后在仓库根运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File doc_md/other/skin-c6-candidate/Build-Candidate.ps1
```

结果为 `artifacts/skin-c6/oms-complex-c6.osk`。普通拖入导入、Settings → Skin 选择即可；也可使用现有 managed 工作区或注册 external 目录。external 始终只读，脚本不能写包或访问任意文件。

在已有 `gameplay-skin.json` 中增加 `"script": "gameplay-skin.script"`，即可让 OMS 在后台编译这个固定文件。也可指定 `"script": "gameplay-skin.bytecode"` 使用预编译字节码。未声明 script 时没有脚本；不接受自定义路径、DLL、自动探测或多个 runtime。脚本和 manifest、scene、skin.ini、全部资源参加同一 C2 package revision。

工具直接编译同一份生产 compiler/verifier 源码：

```powershell
dotnet run --project tools/SkinScriptCompiler -c Release -- check path/to/gameplay-skin.script
dotnet run --project tools/SkinScriptCompiler -c Release -- compile path/to/gameplay-skin.script path/to/gameplay-skin.bytecode
dotnet run --project tools/SkinScriptCompiler -c Release -- verify path/to/gameplay-skin.bytecode
```

工具返回非零表示失败，输出稳定 `OMS-SKIN-SCRIPT-...` 与源行号，不输出作者源码或系统路径。普通选择/reload 的脚本准备错误也显示在 Settings；失败保留旧包。修改 source 后重新构建 `.osk` 或退出 gameplay/preview 后使用 Settings 唯一的 Reload current skin；没有 watcher 或 live reload。

## 语言和版本

UTF-8、无 BOM，首个非空非注释行必须为 `oms-script 1`。`#` 开始行内注释，空白分词。数字使用 invariant 格式且必须 finite，运算是 IEEE 754 double。名字使用小写 ASCII token；不支持字符串、对象、函数库或反射。声明必须位于指令之前。

```text
oms-script 1
required gameplay.events.read
required scene.numeric.write
state event 0
state pressed 0
state rotation 0
target node.glow rotation
read event event-kind
eq pressed event 20
when pressed hit
halt
hit:
add rotation rotation 15
set node.glow rotation rotation
halt
```

`state 名称 初值` 声明持久数值单元，`heap 数量` 声明固定数组（可省略为 0），`target 场景节点ID 属性` 声明可写目标。目标必须存在于同包的 scene/template；实例和池化 clone 都由引擎持有。只允许 `alpha / x / y / rotation / scale-x / scale-y`，数值遵循现有 scene property 的范围，不改变 layout、输入、判定、音符时序或 BGA 内容/时钟。

每次回调从第一条指令开始，state/heap 在有效事件期间保留。操作数可以是数字常量或已声明 state 名；目标 state 只能是已声明的名称。

| 指令 | 作用 |
| --- | --- |
| `mov dst value` | 赋值 |
| `add/sub/mul/div dst a b` | 算术，非有限结果熔断脚本 |
| `min/max/lt/eq dst a b` | 比较；lt/eq 输出 0 或 1 |
| `clamp dst value min max` | 限制数值 |
| `read dst input` | 读取获准的引擎数值输入 |
| `random dst` | 读取引擎 seed 派生的可复现随机数 |
| `set node property value` | 设置已声明的 scene target |
| `label:`、`jump label`、`when value label` | 标签、跳转、非零条件跳转 |
| `load dst index`、`store index value` | 读写固定 heap；索引必须为范围内整数 |
| `granted dst capability` | 查询已声明能力当前是否获准 |
| `halt` | 正常结束回调 |

source、bytecode、compiler/runtime 当前均为 V1。不支持的 source 头、bytecode 魔数或 runtime wire version 严格拒绝，不能当 absent、自动降级或沿用旧授权。compiler version 是引擎实现版本，不是字节码输入字段：编译/验证语义改变须递增该版本并使授权指纹失效；仍符合现行 V1 格式和语义的字节码不按其生成工具年代拒绝。字节码魔数 `OMSSBC01`，little-endian 固定整数/double，保存 runtime version、32 字节 source digest、五个有界 count、request/target 表、state 初值与固定 49 字节指令（opcode、destination、三个 operand、argument、line）；ASCII token 为单字节长度前缀。精确定义由同一 `GameplaySkinScriptCompiler` 的 encode/decode/verifier 维护，作者应使用工具生成。版本、截断、尾随内容、opcode、寄存器、跳转、target、权限声明与数值范围均在边界验证。

源码 digest 只关联作者诊断，不是信任证明；所有输入字节码仍运行完整 verifier。没有从磁盘加载的“可信编译缓存”。同一 immutable package 内复用已验证 program；新 revision 重做准备。授权指纹包含整包内容与 program（含版本），不复用旧 CLR hash 或 process-local carrier。

## 能力与用户控制

| 公开能力 | 输入或效果 | 默认 |
| --- | --- | --- |
| `gameplay.snapshot.read` | 固定 gameplay tick；`time / delta / combo / gauge / beat / bpm / running` | 未授权 |
| `gameplay.events.read` | `event-kind / event-value` | 未授权 |
| `scene.numeric.write` | 已声明 target 的数值表现 | 未授权 |
| `math.random.read` | 确定性 PRNG | 未授权 |

`required` 表示缺少该权限时整个脚本不激活；`optional` 不阻止脚本激活，调用前用 `granted` 选择无权分支；`deny` 表示作者明确禁止，不能再由用户授权。unknown、hard-deny 或 host 不支持都不能制造权限。每次实际 read/set/random 都复核当前 token；未获 `gameplay.events.read` 时不向作者派发真实事件回调，防止回调计数泄露事件。内部 Snapshot/Reset 重建照常，固定 tick 仅在 `gameplay.snapshot.read` 获准时派发。声明和保存的 grant 本身不等于运行时权限。`NoAdditionalAuthorization` 不用于这些可选脚本能力。

Settings → Skin → 可选皮肤脚本显示每项 request、用户决定、实际可用状态以及运行诊断；提供授权、拒绝、撤销。无 snapshot 和 events 授权时不会向作者派发任何回调，scene 写权限本身不授予读取时钟或事件的通道。网络、任意 filesystem、reflection、process/thread/native、Realm/config 或 gameplay authority 写入永久禁止。运行时只有上述数值通道，未注册能力没有隐式扩展。

授权保存在当前数据根的 `skin-script-authorizations.json`，以引擎 stable record ID 和整包/VM 指纹绑定。重启、相同内容的 reload 和只改 managed 目录名可保留授权；重新导入形成新记录、改变任何包内容或 VM/compiler 协议则须重新授权。授权不由皮肤自报 ID、名称、路径或作者字符串继承。

撤销立即让旧 token 失权；GameHost update 通知即使在暂停的场景树中也会停用旧 VM、撤去 script overlay，保留 C5 当前状态和必要视觉，持久化在后台完成。快速撤销再授权创建新 VM，无关皮肤保存不重置当前脚本。它不运行 source capture/reload，不解禁 live reload。存储无效、未知版本或存在未完成 `.pending` 原子替换时拒绝旧 grant；界面显示稳定错误。写入失败时当前会话仍撤销，界面明确提示持久化未确认。

## 事件、时钟与恢复

输入只来自 C5 唯一 stream 的完整 Snapshot/Reset 和真实 BMS/mania producer。时间以 gameplay clock 毫秒为单位，可为负；`running` 为 0/1，gauge 使用引擎归一化值。`event-kind` 为 `-1` 时是固定 60 Hz gameplay presentation tick，普通事件沿用公开 discriminator：Snapshot 1、Reset 2、publication 3；lifecycle 10～15；按下20/松开21；object30～32；judgement40/score41/combo42/gauge43；timing50～55；BGA60～61。

`event-value` 是当前事件的单一数值投影，未扩张 C5 payload 或玩法 authority：

| 事件 | `event-value` |
| --- | --- |
| 按下/松开 | 输入 strength |
| object | 引擎提供的 progress |
| judgement | 判定 offset，毫秒 |
| score/combo/gauge | 对应当前 score、combo、归一化 gauge |
| timing bar/BPM/scroll | 对应 bar index、BPM、scroll multiplier |
| stop started/ended | 1 / 0 |
| 其余 timing | beat |
| BGA | Unspecified 0、Empty 1、Ready 2、Playing 3、Paused 4、Failed 5 |
| lifecycle | Unspecified 0、Loaded 1、Running 2、Paused 3、Completed 4、Failed 5 |
| Snapshot、Reset、publication、固定 tick | 0 |

相同 gameplay time 的真实事件按原 sequence 消费，固定 tick 使用当前引擎状态。引擎时间须推进越过 tick 时间才封闭并提交该 tick；同一 update 中暂时空的事件队列不能证明后续 playfield 不再产生等时事件。render FPS 不改变脚本 tick 数；pause 停止 clock/tick，seek、retry、reload、epoch 重建清 state/heap 并恢复同 seed。故障熔断不会被普通 Reset 隐式清除；新授权实例或新包须重新协商。不要用真实 frame 次数或 wall-clock 推算玩法。

候选将 judgement 历史累计和 tick 投影分开：事件仅更新最近八次间隔与随机旋转，tick 才写五个节点属性。这样 dense object/event 流不会把每条事件都变成全 scene 投影。

## 硬预算、故障与 profiler

| 项目 | 上限 |
| --- | --- |
| source / bytecode | 64 KiB / 256 KiB |
| source 行 / 编译指令 | 4096 / 2048 |
| state / heap 数值单元 | 128 / 512（固定分配） |
| requests / targets | 32 / 128 |
| 单 callback / 单 update frame 指令 | 4096 / 16384 |
| 单 update frame set / 实际 clone 属性应用 | 256 / 16384 |
| 每 frame 追赶 tick | 120；仍受指令总额限制 |

每条指令前检查 instruction/cancel，heap 访问前检查整数索引，set 与实际 clone 应用分别限额。没有等回调结束再用 stopwatch 阻止无限循环的旁路。source/编译/图片解码均在既有后台 work/lease 内；update 只执行已验证数值程序和有界 scene 投影。总资源、decoded bytes、texture/effect/text、node/template/pool 继续执行 C5 的预算。

无限循环、越界、非有限运算、越权、取消或预算超限只熔断脚本，撤去 overlay、恢复现有声明式和程序化迁移视觉；游戏时钟、判定、输入和 BGA 不受脚本控制。Settings profiler显示回调数、指令数、VM elapsed、numeric heap 和故障行号。VM elapsed不包括后置 scene clone 投影；性能结论须区分 VM spike 与真实 host 整帧测量，低端实机仍需人工验收。
