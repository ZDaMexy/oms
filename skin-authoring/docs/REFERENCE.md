# 普通作者参考：布局、场景、事件与可选脚本

从 [完整制作流程](../README.md)、[静线源文件](../sources/oms-simple/) 或 [星轨源文件](../sources/oms-complex/) 开始。本页用于查具体写法，不替代完整作品。作者只编辑普通 INI、PNG/WAV、JSON 和数值脚本，无须阅读游戏源码、编译游戏或编写插件。

本页对应 `oms-gameplay-skin-manifest.v1`、`oms-gameplay-skin-scene.v1`、`oms-gameplay-skin-event.v1` 和 `oms-script 1`。字段区分大小写，不接受自创字段。

## 一份可以直接重做的完整练习

练习仍复制完整静线，保留两种玩法的全部音符、长条、舞台、声音和必要部件。以下三个文件共同演示信息绑定、真实谱面进度、循环旋转、判定换图、模板实例、运行/暂停状态和可选的近期击打组合：

- [完整清单](examples/reference-study/gameplay-skin.json)引用完整模板已有的三张图片，并声明脚本。
- [完整场景](examples/reference-study/gameplay-skin.scene.json)包含节点、动画、状态机、绑定、图片变体和模板实例的完整字段。
- [完整脚本](examples/reference-study/gameplay-skin.script)把最近击打累积在固定单元中，按游玩时钟衰减，与当前能量组合，只改变右侧图形大小。

在发行套件目录打开 PowerShell，使用尚不存在的目录名：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action new -Output .\work\reference-study -Name '我的场景练习'
Copy-Item -LiteralPath .\docs\examples\reference-study\gameplay-skin.json -Destination .\work\reference-study\gameplay-skin.json -Force
Copy-Item -LiteralPath .\docs\examples\reference-study\gameplay-skin.scene.json -Destination .\work\reference-study\gameplay-skin.scene.json -Force
Copy-Item -LiteralPath .\docs\examples\reference-study\gameplay-skin.script -Destination .\work\reference-study\gameplay-skin.script -Force
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action check -Source .\work\reference-study
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action import -Source .\work\reference-study -Output .\dist\reference-study.osk
```

只拖入最后提示的导入副本，在设置选择新作品。分别游玩 BMS 和 mania，先拒绝额外效果，再授权、暂停、撤销和重试。修改 `ref.rotate.end` 的时间改变旋转周期；修改 `ref.panel` 和文字节点的位置调整信息布局；修改脚本的 `0.05`、`0.15` 调整击打和能量的视觉权重。每次直接 `check → import`，不要再 `generate` 覆盖手写文件。重做时换新目录，不删除已有作者内容。

2026-09-11 已将三个完整文件覆盖到静线独立副本，实际通过作者工具的设置、图片、场景、脚本检查，见 [检查记录](reference-verification.json)。这不表示游戏画面或真实设备已验收。

## 文件和完整结构

清单 `gameplay-skin.json` 的必填字段：

```json
{
  "contract": "oms-gameplay-skin-manifest.v1",
  "scene": "gameplay-skin.scene.json",
  "sceneContract": "oms-gameplay-skin-scene.v1",
  "eventContract": "oms-gameplay-skin-event.v1",
  "resources": [
    { "id": "texture.white", "type": "texture", "path": "scene/white.png" }
  ]
}
```

仅在需要脚本时增加 `"script": "gameplay-skin.script"`。也接受固定文件 `gameplay-skin.bytecode`，必须由兼容编译器生成并通过验证；作者日常直接保留源码即可。场景和脚本不接受自定义路径或自动发现其它文件。

每项资源必须有 `id/type/path`，type 只能是 `texture`。path 是带扩展名的包内相对路径；拒绝绝对路径、上级目录、链接和大小写不同但指向同一文件的重复资源。场景节点引用资源 id；INI 则使用不带扩展名的图片基名。

| 场景顶层字段 | 内容 | 必填 |
| --- | --- | --- |
| `contract` | 固定 `oms-gameplay-skin-scene.v1` | 是 |
| `root` | 一个完整节点 | 是 |
| `tracks` | 动画数组 | 是，可 `[]` |
| `stateMachines` | 状态机数组 | 是，可 `[]` |
| `bindings` | 只读绑定数组 | 是，可 `[]` |
| `variants` | 图片变体数组 | 否，建议明确 `[]` |
| `templates`、`instances` | 模板、实例数组 | 是，可 `[]` |

id 使用稳定 ASCII 名字，如 `my.hud.score`。场景中的节点、轨迹、帧、状态、赋值、转移、绑定、变体、分支、模板和实例共用唯一名字空间。JSON 不允许重复或未知字段。下面短例子是对应数组的一项；整份文件见上面的完整练习。

## 节点、目标和属性

每个节点必填 `id/type/target/blend/properties/effects/children`；不用的对象和数组仍写 `{}`、`[]`。`slot/resource` 可省略。

```json
{
  "id": "my.panel", "type": "sprite", "target": { "kind": "global" },
  "resource": "texture.white", "blend": "alpha",
  "properties": { "x": 0.18, "y": 0.02, "width": 0.64, "height": 0.06, "colour": "#101827ee" },
  "effects": [], "children": []
}
```

可见节点必须属于同包明确 `Provide` 的槽。通常在父容器写 `"slot": "hud.text"` 或 `"slot": "decoration"`，子节点沿用；已有槽位所有者的子节点不能再写另一个 slot，也不能切到不同的轨道/舞台身份。分派多个槽位的最外层是无资源、无属性、无效果、无程序控制且 `blend: inherit` 的 container；两款完整源已采用此结构。

sprite 可引用资源；省略 resource 时使用所属槽位已提供的普通图片，能沿 INI 为两种玩法选不同图片。其它节点不能写 resource。给 Inherit、Suppress 或无效资源槽添加场景，不会把它变成自己提供的内容。

| target 完整形式 | 区域 |
| --- | --- |
| `{ "kind": "global" }` | 安全屏幕 |
| `{ "kind": "stage", "id": "…", "index": 0 }` | 精确舞台，id/index 都必填 |
| `{ "kind": "group", "id": "…", "index": 0 }` | 精确轨道组，id/index 都必填 |
| `{ "kind": "lane", "id": "…", "index": 0 }` | 精确轨道，id/index 都必填 |
| `{ "kind": "hud" }` | 全局信息区 |
| `{ "kind": "hud", "id": "…", "index": 0 }` | 对应舞台信息区，不能只给一半身份 |
| `{ "kind": "bga" }` 或带 `"index": 1` | 视频区域，省略 index 表示第0个 |

id 与非负整数 index 必须同时匹配布局。BMS 舞台/组为 `bms.group.deck-1/2`，mania 为 `mania.group.stage-1/2`；BMS 轨道为 `bms.lane.scratch-1/2` 和 `bms.lane.key-N`，mania 为 `mania.lane.column-N`。不要从图片次序猜 index。

场景没有“目标不存在则忽略”或玩法选择器。双玩法作品优先用共通 global 目标做信息和演出，用 INI 的玩法/键数组合选择具体舞台与音符。

引擎先解析目标，再选槽位表面。例如 hud.text 所有者使用既有信息区，其明确 global 子节点仍可定位安全屏幕。`x/y/width/height` 是区域比例，父节点的大小、位移、旋转、缩放继续影响子树。离线预览不等于真实窗口像素。

| 属性 | 类型/范围 | 节点 |
| --- | --- | --- |
| `x/y` | 有限数，-4..4 | 全部 |
| `width/height` | 0..4 | 全部 |
| `scale-x/scale-y` | 0..8 | 全部 |
| `rotation` | 角度，-36000..36000 | 全部 |
| `z` | 层内深度，-32768..32768；不能越过槽位层 | 全部 |
| `opacity` / `visible` | 0..1 / JSON true或false | 全部 |
| `colour` | #RRGGBB 或 #RRGGBBAA | 全部 |
| `anchor/origin` | top-left/top-centre/top-right/centre-left/centre/centre-right/bottom-left/bottom-centre/bottom-right | 全部 |
| `fill-mode` | stretch/fit/fill | sprite |
| `text/font-size/alignment` | 字符串 / 1..128 / left或centre或right | text |
| `mask-mode` | ellipse | mask |
| `clip-mode/corner-radius` | bounds或rounded / 0..256 | clip |

container 不增加专属属性。blend 必填，允许 inherit/alpha/additive/multiply/screen。零进度使用定宽容器里的子图片 width=0；零缩放在绘图矩阵中会夹到极小值，可能留下细线。实际字高需在最小窗口核对。

effects 每项必填 id/type/properties。blur 允许 radius；glow 允许 radius/strength/colour；outline 允许 width/colour；shadow 允许 x/y/blur/colour。radius、blur 为0..64，strength为0..4，描边width为0..32，阴影偏移为-128..128，颜色沿用上表。效果会增加中间图片成本。

## 真实车道布局与 INI

下落区域和车道几何仍由公开 INI 控制，场景负责对应表面的图形。游戏按当前键数、样式、单/双舞台、屏幕比例与缩放统一准备；皮肤不能改判定时序或视频时钟。

| BMS [Bms] 字段 | 语义 |
| --- | --- |
| Keymode | 5K/7K/9K/9K_PMS/14K，修改对应块 |
| PlayfieldWidth/PlayfieldHeight | 下落区域整体宽高，屏幕比例 |
| NormalLaneWidth/ScratchLaneWidth | 普通键/转盘的相对宽度权重 |
| NormalLaneSpacing/ScratchLaneSpacing | 对应间隔权重 |
| HitTargetHeight/HitTargetBarHeight/HitTargetLineHeight | 判定区域、条和线的尺寸输入 |
| HitTargetGlowRadius/BarLineHeight | 判定线辉光半径、小节线厚度 |
| LongNoteBodyWidth | 相对车道宽，有限且0<value<=1 |

BMS 不接受 mania 的 HitPosition、逐列 ColumnWidth 或自创 HitTargetVerticalOffset。例如略减新作品每个 BMS 块的 PlayfieldWidth，检查后比较5K/7K四样式与14K，不能只改第一块就当作全键数完成。

mania 在对应 [Mania] Keys 块修改 ColumnWidth、ColumnSpacing、HitPosition 等既有设置，数组项数必须对应该块，双舞台和特殊键保持模板映射。KeyImageN/KeyImageND 分别提供松开/按下图片，模板对 playfield.key 保留 Inherit 才能使用同包这对素材。关闭可选闪光不会关闭按下图片；BMS普通按键图片也沿真实输入反馈。

公共资源声明的四种完整 Target 形式：

```text
Global ruleset=… keymode=… stage-mode=…
Stage ruleset=… keymode=… stage-mode=… group=… group-logical=… group-visual=…
Group ruleset=… keymode=… stage-mode=… group=… group-logical=… group-visual=…
Lane ruleset=… keymode=… stage-mode=… group=… lane=… group-logical=… group-visual=… global-logical=… global-visual=… group-local-logical=… group-local-visual=…
```

ruleset 为 any/mania/bms，stage-mode 为 any/single/dual。keymode 为 any、BMS的5k/7k/9k-bms/9k-pms/14k、mania单舞台Nk或双舞台Nk-Mk。不要混淆旧 [Bms] Keymode 的大小写。公共 section 为 [GameplaySkin.Common:1] 或 [GameplaySkin.Bms:1]，下一条 Target 前的资源行属于当前目标。

资源行是 `槽位ID: resource Provide "图片基名"`、`槽位ID: resource Inherit` 或 `槽位ID: resource Suppress`。精确玩法优先，再比较键数、舞台模式、Lane/Group/Stage/Global和同等级后行；错误的精确声明不回头拼本包更宽声明。必备/推荐部分不能关闭，明确关闭的可选部分不会偷偷恢复。缺少必要部分由正式静线补齐。全部资源名和适用范围见 [CATALOG](CATALOG.md)。

## 动画、状态机、绑定、变体、模板

tracks 每项必填 id/type/target/property/easing/loop/keyframes；target为节点id。帧必填id/time/value，时间为毫秒，非负、严格递增且不超过86400000，至少一帧。

```json
{
  "id": "my.rotate", "type": "tween", "target": "ref.icon", "property": "rotation",
  "easing": "linear", "loop": true,
  "keyframes": [
    { "id": "my.rotate.start", "time": 0, "value": 0 },
    { "id": "my.rotate.end", "time": 2000, "value": 360 }
  ]
}
```

tween只改变数值属性；frame只给sprite的resource换帧，value必须是清单资源id。easing为step/linear/in/out/in-out。循环周期来自最后帧，循环动画应给正周期。它们沿游玩时钟，暂停不推进，重试/跳转使用新位置。

编号PNG短键/长条动画是另一条路径：名字-0.png、名字-1.png等，从0连续编号、固定60帧/秒，不写上述关键帧JSON。

| 状态机对象 | 全部必填字段 |
| --- | --- |
| stateMachines项 | id/initial/states/transitions |
| states项 | id/set |
| set项 | id/target/property/value |
| transitions项 | id/from/to/event |

initial/from/to引用本状态机已有状态；target引用主场景或模板节点，value匹配属性类型。完整练习 ref.lifecycle 可直接查看准备、运行、暂停写法；星轨还含完成和失败。

合法事件字符串为 gameplay.attach/loaded/start/pause/complete/fail、input.key.down/up、object.spawn/state、judgement.hit、timing.stop、bga.state。它是当前完整事实的投影，新加入、跳转、重试会重建，不适合任意历史累计。gameplay.resume不是合法字符串；恢复后的Running由当前状态重建。历史组合使用获准脚本。不要让轨迹、状态机和绑定同时争抢同一属性；例子分别写旋转、状态文字和信息。

bindings每项必填id/target/property/source，例如：

```json
{ "id": "my.accuracy", "target": "ref.accuracy", "property": "text", "source": "score.accuracy" }
```

| source | 值 | 能驱动的属性 |
| --- | --- | --- |
| layout.stage/layout.group/layout.lane | 目标稳定身份字符串，不是矩形或对象 | text |
| input.pressed | 当前目标轨道是否按下 | visible或text |
| object.state | scheduled/visible/holding/hit/missed/completed/despawned | text |
| judgement.result | miss/meh/ok/good/great/perfect | text |
| judgement.offset | 当前判定偏差，毫秒 | 数值或text |
| score.value | 当前玩法分数 | 数值或text |
| score.accuracy | 0..1；text为两位小数百分比 | 数值或text |
| combo.value / gauge.value | 连击 / 归一化能量 | 数值或text |
| timing.beat/timing.measure/timing.bpm | 拍、小节索引、BPM | 数值或text |
| timing.progress | 可玩起止间0..1；text为整数百分比 | 数值或text |
| bga.content-state | empty/ready/playing/paused/failed | text |

字符串不能直接驱动数字或资源；换图用variants。数值限制在目标属性范围内，不修改玩法。首物件前进度0、末物件后1、零时长0；暂停保持、重试和跳转反映新位置。局部对象、轨道和组沿自身范围取值，不应由另一轨道的判定驱动。

没有当前判定或判定显示期结束时，judgement.result投影为miss，judgement.offset为0；这不代表玩家刚发生了一次失误。没有当前对象时object.state为scheduled，没有视频内容时bga.content-state为empty。记录击打发生时的历史应使用获准的真实判定事件，不能把持续显示的miss文字当作新事件。当前数值脚本的判定事件只提供偏差值，没有判定等级输入，也不能据offset为0推断失误。

若额外信息条只应在有当前判定时显示文字，保留 `judgement.result → text` 绑定，另用下面的普通状态机控制同一文字节点的透明度。`judgement.hit` 在这里表示目标有当前判定，包含真实的 Miss；它不是“成功击打”的等级筛选。显示期结束后游戏从 initial 重新投影当前事实，因而回到 empty。这个显示规则无需授权脚本，拒绝额外演出也仍然准确。

```json
{
  "id": "astral.current-judgement",
  "initial": "astral.judgement.empty",
  "states": [
    { "id": "astral.judgement.empty", "set": [
      { "id": "astral.judgement.hide", "target": "astral.console.judgement", "property": "opacity", "value": 0 }
    ] },
    { "id": "astral.judgement.active", "set": [
      { "id": "astral.judgement.show", "target": "astral.console.judgement", "property": "opacity", "value": 1 }
    ] }
  ],
  "transitions": [
    { "id": "astral.judgement.arrive", "from": "astral.judgement.empty", "to": "astral.judgement.active", "event": "judgement.hit" }
  ]
}
```

variants必填id/target/property/source/default/cases。target必须sprite，property固定resource，default为资源id。每case必填id/key/resource，至少一项，同一variant的key不能重复。

```json
{
  "id": "my.grade-image", "target": "ref.icon", "property": "resource",
  "source": "judgement.result", "default": "ref.cool",
  "cases": [ { "id": "my.grade-perfect", "key": "perfect", "resource": "ref.warm" } ]
}
```

source只允许object.state、judgement.result、bga.content-state，key必须是上表对应字符串；当前值没有列入cases时使用default。空态先按上一节投影，例如没有判定时也会命中作者的miss分支，不会越过该分支自动使用default。没有任意表达式、范围条件或外部资源路径。

templates每项必填id/root，root是完整节点；instances每项必填id/template/target，没有实例properties、参数或循环生成字段：

```json
{ "id": "my.status-copy", "template": "ref.status-template", "target": { "kind": "global" } }
```

实例target覆盖模板整棵子树目标。动画、绑定、状态机和脚本仍引用模板源节点id，作用到它所有实际实例，不引用内部clone名。展开和真实对象实例均计预算。练习用ref.status-template的一个全局实例显示状态：外层ref.status-owner拥有hud.text，内层无slot的global文字ref.status与顶部面板使用同一安全屏幕位置；状态机继续写ref.status。直接把hud.text放在文字自身上会使用既有信息区域，不能据其y值推断全屏顶部位置。需要独立控制的全局图形应使用不同源节点。

## 可选脚本与能力

UTF-8无BOM，首个非空非注释行是oms-script 1；#开始注释，空白分词。名字用小写ASCII token，数字使用小数点且必须有限。声明全部放在指令前：

| 声明 | 作用 |
| --- | --- |
| required 能力ID | 未获准则整个脚本不激活 |
| optional 能力ID | 无权仍可激活，调用前用granted选择无权分支 |
| deny 能力ID | 作者禁止，玩家不能再授权 |
| state 名字 初值 | 固定持久数值单元 |
| heap 数量 | 固定数组，省略为0 |
| target 节点ID 属性 | 明确允许写的表现目标 |

公开能力为gameplay.snapshot.read、gameplay.events.read、scene.numeric.write、math.random.read，最后一项提供引擎种子的可重现随机数。未知能力不因声明而获支持；网络、任意文件、进程、反射、修改玩法均禁止。无snapshot/events权限没有对应回调，写权限不隐式授予读时钟或输入。

声明式绑定不要求额外脚本权限。脚本只写alpha/x/y/rotation/scale-x/scale-y；脚本alpha对应场景opacity，不能混用。不能写width、文字、图片路径、判定、分数或视频内容。

每次回调从第一条指令开始；state/heap保留至新实例或重试/回跳重建。操作数为数字常量或state名，dst只能是已声明state：

| 指令 | 作用 |
| --- | --- |
| mov dst value | 赋值 |
| add/sub/mul/div dst a b | 算术 |
| min/max dst a b | 取较小/较大值 |
| lt/eq dst a b | 小于/相等，输出0或1 |
| clamp dst value min max | 限制范围 |
| read dst input | 读获准输入 |
| random dst | 获准随机 |
| set node property value | 写已声明目标 |
| name:、jump name、when value name | 标签、跳转、非零跳转 |
| load dst index、store index value | 固定数组；索引为范围内整数 |
| granted dst capability | 查询已声明能力当前是否获准 |
| halt | 结束回调 |

snapshot权限允许time/delta/combo/gauge/beat/bpm/running；events权限允许event-kind/event-value。time/delta是游玩毫秒，time可为负，running为0/1。脚本没有accuracy/progress输入名，这两项用声明式绑定。

event-kind=-1是固定60Hz游玩更新；真实事件号码如下：

| 号码 | 事件 |
| --- | --- |
| 1/2/3 | 完整状态/重建/新布局发布 |
| 10/11/12/13/14/15 | 加载/开始/暂停/恢复/完成/失败 |
| 20/21 | 按下/松开 |
| 30/31/32 | 对象出现/离开/状态变化 |
| 40/41/42/43 | 判定/分数/连击/能量 |
| 50/51/52/53/54/55 | 拍/小节/BPM/停顿开始/停顿结束/滚动变化 |
| 60/61 | 视频区域/视频内容状态 |

event-value是单一数值：输入强度、对象进度、判定偏差毫秒、分数/连击/能量、小节索引/BPM/滚动倍率；停顿开始1结束0，其余时序为拍。视频状态为未指定0、空1、准备2、播放3、暂停4、失败5；生命周期为未指定0、加载1、运行2、暂停3、完成4、失败5；完整状态、重建、新发布、固定更新为0。脚本不直接取得对象引用或任意事件对象。

同一游玩时刻真实事件按原顺序处理；固定更新等该时刻事件完整后提交。暂停不推进演出，重试/回跳重建历史和种子；不使用显示帧数或墙上时钟。原生恢复事件13不等于合法声明式gameplay.resume字符串。

设置中可以允许、拒绝、撤销，暂停撤销也应立即撤去脚本输出。新导入记录或包内容变化需要重新授权，名字不是权限凭据。无限循环、越界、非有限运算或越权只停止可选脚本，拒绝后必要信息与基本外观仍应清楚。

## 预算和错误定位

以下是上限而非推荐目标。完整场景、当前谱面对象实例、文字和效果共同占用资源；工具通过不能代替实际屏幕与谱面的准备。

| 项目 | 上限 |
| --- | --- |
| 作者目录打包 | 128MiB、4096普通文件，不接受链接 |
| skin.ini / 名称与作者 | 1MiB；各256字符，无控制字符 |
| 清单 / 场景 / JSON深度 | 64KiB / 512KiB / 32 |
| 稳定id / 资源路径 | 128 / 240字符 |
| 资源数 / 单资源 / 总资源 | 256 / 16MiB / 64MiB |
| 单图像素 / 总图像素 / 解码字节 | 16M / 64M / 256MiB |
| 源节点 / 树深 / 每节点孩子 | 2048 / 16 / 128 |
| 每对象属性 / 单节点效果 / 总效果 | 32 / 8 / 512 |
| 动画 / 每条帧 / 总帧 | 512 / 512 / 8192 |
| 状态机 / 状态 / 赋值 / 转移 | 128 / 1024 / 4096 / 2048 |
| 绑定 / 变体 / 单变体分支 / 总分支 | 512 / 512 / 32 / 4096 |
| 模板 / 实例 / 展开节点 | 128 / 1024 / 8192 |
| 每帧属性 / 单事件状态属性应用 | 16384 / 8192 |
| 源静态文字总字符 | 65536 |
| 运行节点 / 效果实例 | 32768 / 8192 |
| 效果表面像素 / 字节 | 128M / 512MiB |
| 文字字形 / 像素 / 字节 | 4096 / 64M / 256MiB |
| 单动态文字字形预留 / 每帧创建 | 384 / 256 |
| 每目标专用表现 / 击打效果 | 256 / 16 |
| 信息原生所有者 / 单槽所有者 | 64 / 32 |
| 信息分区 / 残余分区 / 工厂实例 | 1280 / 1344 / 512 |
| 信息捕获表面 / 像素 | 5 / 64M |
| 脚本源码 / 字节码 / 行数 / 编译指令 | 64KiB / 256KiB / 4096 / 2048 |
| state / heap / 请求 / 写目标 | 128 / 512 / 32 / 128 |
| 单回调指令 / 单画面指令 / 单画面set | 4096 / 16384 / 256 |
| 单画面脚本实例属性应用 / 追赶更新 | 16384 / 120 |

先按工具给的文件、行号和编号定位。场景常见编号：003重复字段、004未知字段、005缺字段、009重复id、010危险路径、013未知资源、017未知事件、018未知绑定、019未知目标、021引用错误、023动画错误、024状态机错误、025预算超限、027当前玩法不支持该槽；前缀为OMS-SKIN-SCENE-。脚本为OMS-SKIN-SCRIPT-并带源行。无效更新保留原作品，修好后退出游玩和预览再重新载入。

最终按 [制作说明的实际核对](AUTHORING.md#每次作品更新后实际核对) 检查键数样式、单/双舞台、音符长条、信息视频、比例缩放、授权和暂停重试。设备、声音、延迟与长时间体验不能由预算或示例工具通过代签。

开发证据：字段逐项核对当前GameplaySkinSceneCodec/Schema/PreparedScene/SceneRuntimeHost、GameplaySkinEventKind、GameplaySkinScriptCompiler/Program和公开目录。作者无需读取这些源码；套件中的完整作品、练习和本页即可查阅。
