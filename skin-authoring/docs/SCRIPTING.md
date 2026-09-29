# 可选数值脚本：从第一个效果到组合演出

先用JSON完成实时分数、血条、旋转、判定换图与运行状态；这些都不需要脚本。当你想把**最近几次击打、血量和可复现随机数**组合起来时，再用 `oms-script 1`。它只计算数值并修改场景节点的少数表现属性，不是Lua/JavaScript，也不用DLL。

## 先跑一个完整例子

[First Scene](examples/first-scene/README.md)是带图片、INI、清单、场景和脚本的完整可打包目录。在制作套件目录运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action check -Source .\docs\examples\first-scene
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action import -Source .\docs\examples\first-scene -Output .\dist\first-scene.osk
```

导入并分别在BMS/mania选择它。分数、进度与旋转由JSON负责；下面这份**完整脚本**只让 `lesson.accent` 根据血量放大，节点存在于配套场景中：

```text
oms-script 1
required gameplay.snapshot.read
required scene.numeric.write
state energy 0
state size 1
target lesson.accent scale-x
target lesson.accent scale-y
read energy gauge
mul size energy 0.25
add size size 1
set lesson.accent scale-x size
set lesson.accent scale-y size
halt
```

设置 → 皮肤 → 可选皮肤脚本中允许两项请求后，血量0.8对应缩放1.2。拒绝或撤销后缩放回到场景本来的1；分数、进度和旋转仍工作。脚本自己的 `required` 只表示“此脚本需要这项权限”，不意味着皮肤的基本显示必须依赖它。

清单增加 `"script": "gameplay-skin.script"` 才启用源码；未声明就没有脚本。也可使用工具生成的固定文件 `gameplay-skin.bytecode`。不接受自定义脚本文件名、任意路径、多个脚本实例入口或自动探测。源码、场景、图片都要放在同一包中。

## 声明与语言

源码为UTF-8、无BOM。首个非空非注释行必须是 `oms-script 1`；`#` 开始注释，参数按空白分隔。数字使用小数点并且必须有限，运算使用double。名字使用小写ASCII token；不支持字符串、对象、外部库或反射。所有声明写在指令之前。

| 声明 | 意义 |
| --- | --- |
| `required 能力ID` | 缺少授权时整个脚本不运行 |
| `optional 能力ID` | 无权仍可运行；使用前先 `granted` 检查 |
| `deny 能力ID` | 作者明确不用，玩家也不能授予 |
| `state 名字 初值` | 一个持久数值单元 |
| `heap 数量` | 固定数组，省略即0 |
| `target 节点ID 属性` | 明确列出可写的场景节点和属性 |

写入只允许 `alpha / x / y / rotation / scale-x / scale-y`。**脚本的alpha对应场景JSON的opacity**，不要混用名字。节点必须存在于同包主场景或模板中；有限数值写入会夹到该属性允许范围，例如 scale-x=10 最终显示为8。非有限运算则停止脚本。不能写width、文字、图片资源、轨道几何、分数、判定、输入或BGA内容/时钟。

每次回调从第一条指令开始；state/heap在本次有效演出中保留，重试/回退重建时恢复初值。操作数可为数字或state名，写入的dst必须是已声明state。

| 指令 | 作用 |
| --- | --- |
| `mov dst value` | 赋值 |
| `add/sub/mul/div dst a b` | 四则运算，非有限结果会停用脚本 |
| `min/max dst a b` | 较小/较大值 |
| `lt/eq dst a b` | 小于/等于，得到0或1 |
| `clamp dst value min max` | 限制范围 |
| `read dst input` | 读获准的游戏数值 |
| `random dst` | 获准的可复现随机数 |
| `set node property value` | 写已声明目标 |
| `name:`、`jump name`、`when value name` | 标签、跳转、非零时跳转 |
| `load dst index`、`store index value` | 读写heap；索引必须是范围内整数 |
| `granted dst capability` | 查询已声明能力当前是否获准 |
| `halt` | 结束本次回调 |

## 可以读取什么

| 能力 | input或作用 |
| --- | --- |
| `gameplay.snapshot.read` | `time / delta / combo / gauge / beat / bpm / running`；固定60Hz游戏时间更新 |
| `gameplay.events.read` | `event-kind / event-value`；真实游戏事件回调 |
| `scene.numeric.write` | `set`到已声明的节点数值属性 |
| `math.random.read` | `random`，由游戏给出的种子保证可复现 |

这些权限默认未授权。没有snapshot或events权限，就没有对应回调；scene写权限不暗中授予读取时间的能力。脚本没有accuracy、progress、曲名或判定等级输入；这类实时显示用JSON绑定。网络、任意文件、进程/线程、系统调用、设置数据库、玩法写入不开放。

`time/delta`使用游戏毫秒，time可为负；running为0/1，gauge为归一化数值。固定tick的event-kind为-1，其它事件如下：

| event-kind | 事件 | event-value |
| --- | --- | --- |
| 1 / 2 / 3 | 完整状态 / 重建 / 新发布 | 0 |
| 10～15 | 加载/开始/暂停/恢复/完成/失败 | 生命周期值：未指定0、加载1、运行2、暂停3、完成4、失败5 |
| 20 / 21 | 按下 / 松开 | 输入强度 |
| 30 / 31 / 32 | 对象出现 / 离开 / 状态变化 | 对象进度 |
| 40 | 判定 | 偏差毫秒，不提供判定等级 |
| 41 / 42 / 43 | 分数 / 连击 / 血量 | 对应当前值 |
| 50 / 51 / 52 | 拍 / 小节 / BPM | 拍 / 小节索引 / BPM |
| 53 / 54 / 55 | STOP开始 / 结束 / 滚动变化 | 1 / 0 / 滚动倍率 |
| 60 / 61 | BGA区域 / 内容状态 | 未指定0、空1、准备2、播放3、暂停4、失败5 |

事件40包括真实Miss，不能把offset=0当成某个等级。脚本只收到这个数值投影，不取得任意对象引用或可遍历的事件对象。JSON状态机的合法字符串与此编号表不同，例如引擎事件13存在，但JSON没有 `gameplay.resume`。

## 进阶：记住最近几次击打

[Reference Study](examples/reference-study/README.md)给出完整制作命令。复制静线后替换清单、场景和脚本，图片仍来自完整模板。脚本在事件40时增加heap中的近期判定数（最多8），每个tick乘0.9衰减，再计算：

```text
缩放 = 1 + 近期判定数 × 0.05 + 当前血量 × 0.15
```

结果在1～1.55之间；脚本只写缩放，JSON轨迹继续写旋转，变体继续根据当前判定换图，三者不争写同一个属性。去掉脚本声明或拒绝权限后，基础信息和声明式效果保留。

同一游戏时刻的真实事件按原顺序处理，固定tick等待该时刻的事件处理完整后提交。tick不跟随显示FPS：暂停停止，重试/回退清历史并恢复相同种子。不要用真实帧数、回调次数或电脑时钟推算谱面时间。普通Reset不会自动清除已触发的脚本故障。

## 更新、授权与排错

修改脚本后直接 `check → pack/import` 或退出游玩/预览后刷新固定目录，不运行`generate`覆盖手写文件。新导入形成新记录；任何包内容变化或脚本工具/运行协议变化可能要求重新授权，不能按作品名称复用旧许可。相同内容重新载入可保留授权。

玩家可允许、拒绝、撤销；暂停中撤销也立即撤掉脚本输出，保留JSON和基础部件。授权保存失败时界面会提示，不能把“本次已撤销”当作“重启后一定已持久化”。作者不需要手改权限文件。

常见失败：target拼错、没有声明state、跳转标签不存在、heap索引越界、除以0、无限循环或每帧写入太多。按 `OMS-SKIN-SCRIPT-…` 和源行号定位。故障停止可选脚本，不允许它影响游戏时钟与判定；设置中的profiler可查看回调数、指令数、VM耗时、数值内存和故障行号。VM耗时不包含后续场景绘制，不能据此推断整帧成本。

## 硬上限与制作建议

| 项目 | 上限 |
| --- | --- |
| source / bytecode | 64KiB / 256KiB |
| source行 / 编译指令 | 4096 / 2048 |
| state / heap数值单元 | 128 / 512 |
| 能力请求 / 写目标 | 32 / 128 |
| 单回调 / 每个画面更新指令 | 4096 / 16384 |
| 每个画面更新set / 实际实例属性应用 | 256 / 16384 |
| 每个画面更新追赶tick | 120，同时受总指令限额约束 |

预算逐指令执行，不能用循环拖住界面再等回调结束。将频繁事件只用于更新少量历史，在固定tick统一投影图形；减少对大量音符实例写同一属性。脚本仍与场景共用节点、图片、效果和文字预算，完整上限见[手册](SKINNING.md#8-格式资源与运行上限)。

## 需要预编译时

普通作者用附带 `SkinAuthoring` 的check就能编译检查源码。下面是**源码仓库开发者**的独立compiler命令，在仓库根的新PowerShell中执行；发行套件不要求这些路径或.NET SDK：

```powershell
. .\UseDevelopmentStorage.ps1
dotnet run --project tools/SkinScriptCompiler -c Release -- check skin-authoring/docs/examples/first-scene/gameplay-skin.script
dotnet run --project tools/SkinScriptCompiler -c Release -- compile skin-authoring/docs/examples/first-scene/gameplay-skin.script artifacts/first-scene.bytecode
dotnet run --project tools/SkinScriptCompiler -c Release -- verify artifacts/first-scene.bytecode
```

返回非零表示失败。预编译后将字节码放入自己的包根并把manifest改为 `"script": "gameplay-skin.bytecode"`；仍要检查完整包。source/bytecode当前均为V1，源码头和字节码版本必须受当前客户端支持，字节码也会完整验证；它不是绕过权限的可信缓存。二进制精确定义由源码仓库`osu.Game/Skinning/Gameplay/Scripting/GameplaySkinScriptCompiler.cs`维护，不手写字节。

维护者技术合同和验证状态在源码仓库`doc_md/subline/P1-A/`维护。历史Momentum/星轨候选只作研究参考，不是本指南的制作起点。
