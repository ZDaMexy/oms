# 普通作者参考：布局、场景、事件与可选脚本

第一次制作从[入门](START_HERE.md)开始；按画面找素材用[图解手册](SKINNING.md)，最小完整动态例子用[First Scene](examples/first-scene/README.md)。本页用于查具体字段，完整静线源在[sources/oms-simple](../sources/oms-simple/)；普通作者无需阅读游戏源码、编译游戏或编写插件。

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

可选字段包括 `bitmapFonts`、`script`、[皮肤配置项](#皮肤配置项)的 `options`、[按选项组合内容](#按选项组合内容)的 `contentCatalog`、[记录动画](#记录动画)的 `animations`、[辅助显示区](#辅助显示区)的 `auxiliaryDisplay`，以及[显式布局](#显式舞台与留空位置)的 `layouts`；旧清单省略它们时保持原有路径。仅在需要脚本时增加 `"script": "gameplay-skin.script"`。也接受固定文件 `gameplay-skin.bytecode`，必须由兼容编译器生成并通过验证；作者日常直接保留源码即可。普通场景和脚本使用固定路径；内容目录的子贡献须逐项显式声明，没有自动发现。

每个 texture 资源可选 `"filter":"nearest"` 或 `"filter":"linear"`；缺省保持 linear。nearest始终取原始图层的最近像素，不自动生成缩小平均层；linear保留原有平滑采样。选择在准备该资源专属纹理时生效，不修改其它皮肤或共用纹理；不开放自定义采样器或作者着色器。

每项资源必须有 `id/type/path`，type 只能是 `texture`。path 是带扩展名的包内相对路径；拒绝绝对路径、上级目录、链接和大小写不同但指向同一文件的重复资源。场景节点引用资源 id；INI 则使用不带扩展名的图片基名。

### 用自己的图片字形显示数字

清单的 `bitmapFonts` 是可选数组。每个字体必填 `id/lineHeight/glyphs`；每个字形接受 `character/resource/advance` 和可选 `rect:[x,y,width,height]`。`resource` 引用同一清单已有的 texture id；省略rect使用整张图片，声明rect则从同一张已准备图片取像素区域，不额外解码或复制纹理。坐标为非负整数，尺寸为正整数且必须完全位于图片内；字形尺寸上限继续适用于区域。字体id在此数组中唯一，同一字体内字符不得重复，字形数组不能空。

以下是清单片段，应与前面的必填字段合并。先在 `resources` 中分别声明 `digit.0`～`digit.9` 的图片；每张图片只画一个字形，示例图片高度不超过32像素：

```json
{ "bitmapFonts": [
  { "id": "digits.score", "lineHeight": 32, "glyphs": [
    { "character": "0", "resource": "digit.0", "advance": 24 },
    { "character": "1", "resource": "digit.1", "advance": 24 },
    { "character": "2", "resource": "digit.2", "advance": 24 },
    { "character": "3", "resource": "digit.3", "advance": 24 },
    { "character": "4", "resource": "digit.4", "advance": 24 },
    { "character": "5", "resource": "digit.5", "advance": 24 },
    { "character": "6", "resource": "digit.6", "advance": 24 },
    { "character": "7", "resource": "digit.7", "advance": 24 },
    { "character": "8", "resource": "digit.8", "advance": 24 },
    { "character": "9", "resource": "digit.9", "advance": 24 }
  ] }
] }
```

`character` 必须是一个字符，只允许 `0`～`9`、`.`、`,`、`%`、`+`、`-`、小写 `x`，不包括空格、换行或其它字母。每份清单及内容目录选中组合最多12个字体声明，每字体最多16字形。确需不同原始行高或步长时可分别声明字体；单纯改显示字号可复用同一字体。不同字体可共享同一已准备图片，十二套独立字形度量仍计入同一上限。`lineHeight` 与 `advance` 都是有限数，范围1..512，单位为原图像素；字形图片每边1..512像素，高度还必须不超过 `lineHeight`。图片顶端对齐，没有独立偏移、基线或自动补字。显示时以 `font-size / lineHeight` 缩放图片和横向步长；`advance` 可与图片宽度不同，用来控制数字间距。

在 `text` 节点的静态 `properties` 中选择字体，例如以下节点和绑定分别放入已有树与 `bindings` 数组；所属 `hud.text` 仍须由本包 `Provide`：

```json
{
  "id": "my.score", "type": "text", "target": { "kind": "global" },
  "slot": "hud.text", "blend": "alpha",
  "properties": { "text": "0", "bitmap-font": "digits.score", "font-size": 32, "format": "integer" },
  "effects": [], "children": []
}
```

```json
{ "id": "my.score.value", "target": "my.score", "property": "text", "source": "score.value" }
```

`bitmap-font` 只能静态引用已声明字体，不能由绑定、轨迹、状态机或脚本更换。位图文字每节点最多64字符，静态文字和状态赋值中的每个字符都必须有字形。绑定接受数值来源及配置开关的0/1投影；曲名、状态词、身份和其它布尔来源不能写入这种数字节点。它不接受 `uppercase` 或 `text-overflow`，也不会在缺字时切换系统字体。

数值绑定必须声明全部十个数字。`fixed-2` 还需要 `.`，`signed-fixed-2` 必须同时提供 `.`、`+`、`-`，`percent` 还需要 `%`；未给 `format` 时使用最多三位小数，非整数来源还需要 `.`。这里的整数来源包括 `score.maximum`、`score.value`、`score.personal-best`、`score.pacemaker-next`、`score.rank-aaa/aa/a`、`combo.value`、`combo.maximum`、`timing.measure`、六档及FAST/SLOW的 `judgement.count.*` 和 `combo.breaks`；例如希望BPM只使用数字字形，应显式选 `integer`。`judgement.offset`、`timing.beat`、`timing.time`、`arrival.time/remaining`、两组 `input.motion-counts-* / input.motion-revolutions-*` 和 `offset.value` 还必须声明 `-`。其它可能出现负值的显示也应提供负号，例如前奏中的 `timing.measure` 可为-1。`,`、`+`、`x` 可用于自己写的静态数字文字，不会自动添加千位分隔或倍率前缀。

位图节点的数字统一沿 `format`：缺省最多三位小数，`integer` 舍入为无小数数字，`fixed-2` 固定两位小数，`signed-fixed-2` 固定两位小数并始终显示正负号（零为`+0.00`），`percent` 乘100后显示整数和百分号。因此位图准确率/进度要显示百分比时也应显式选择 `percent`。所有已声明字体都会准备；超尺寸、缺资源或运行数值超出64字符/缺少字形会报对应场景错误。字形图片和可选区域计入普通资源的文件、像素与解码预算，区域共享同一已准备纹理；动态位图文字每节点预留64字形，仍计入全场景8192字形和真实实例预算。

| 场景顶层字段 | 内容 | 必填 |
| --- | --- | --- |
| `contract` | 固定 `oms-gameplay-skin-scene.v1` | 是 |
| `root` | 一个完整节点 | 是 |
| `tracks` | 动画数组 | 是，可 `[]` |
| `stateMachines` | 状态机数组 | 是，可 `[]` |
| `bindings` | 只读绑定数组 | 是，可 `[]` |
| `variants` | 图片、显隐或横向x/width变体数组 | 否，建议明确 `[]` |
| `completionOutro` | 全连演出总时长，有限数值0..3500毫秒；缺省0保持Player原等待 | 否 |
| `preparationDuration` | 开局准备总时长，有限数值0..10000毫秒；缺省0保持原启动流程 | 否 |
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

可见节点必须属于同包明确 `Provide` 的槽。通常在父容器写 `"slot": "hud.text"` 或 `"slot": "decoration"`，子节点沿用；已有槽位所有者的子节点不能再写另一个 slot，也不能切到不同的轨道/舞台身份。分派多个槽位的最外层是无资源、无属性、无效果、无程序控制且 `blend: inherit` 的 container；静线完整源已采用此结构。

sprite 和 projective-sprite 可引用资源；省略 resource 时使用所属槽位已提供的普通图片，能沿 INI 为两种玩法选不同图片。其它节点不能写 resource。给 Inherit、Suppress 或无效资源槽添加场景，不会把它变成自己提供的内容。

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
| `brightness` | 有限数0..1，省略为1；独立RGB倍率 | 全部 |
| `anchor/origin` | top-left/top-centre/top-right/centre-left/centre/centre-right/bottom-left/bottom-centre/bottom-right | 全部 |
| `fill-mode` | stretch/fit/fill | sprite |
| `text/font-size/alignment` | 字符串 / 1..128 / left或centre或right | text |
| `bitmap-font` | 清单 `bitmapFonts` 中的字体id；静态声明，不能动态切换 | text |
| `layout` / `gap-px` | horizontal / 有限逻辑像素-512..512，缺省0；均为静态属性 | container |
| `height-px` / `width-px` | 有限逻辑像素1..512；前者必填，后者可省略；均为静态属性 | 横排的直接sprite孩子 |
| `mask-mode` | ellipse | mask |
| `clip-mode/corner-radius` | bounds或rounded / 0..256 | clip |
| `reveal-x` | 0..1；从左揭露固定宽度内容，0仍保留作者的独立底槽 | clip |
| `format` | percent / uppercase / fixed-2 / signed-fixed-2 / integer；静态声明，不接受绑定、动画或状态赋值；位图文字不接受uppercase | text |
| `text-overflow` | ellipsis；静态声明，不能用于位图文字 | text |
| `layout-surface` | information.song / information.judgements / information.tempo / information.player；静态布局区域 | hud.text 所有者及其子树 |

普通container沿用比例属性。blend 必填，允许 inherit/alpha/additive/multiply/screen。零进度使用定宽容器里的子图片 width=0；零缩放在绘图矩阵中会夹到极小值，可能留下细线。实际字高需在最小窗口核对。

`brightness`使节点及子树变暗，保留原`colour`、纹理、透明度和显隐状态；0表示黑色，1保留原色。它接受静态数值、数值绑定、数值轨迹及状态赋值，绑定结果夹到0..1；非有限数拒绝。父子分别声明时倍率叠乘，通常只在已有美术根声明一次。它复用现有节点载体，不增加图片、离屏效果或音符池实例；不能将`opacity`降低当作亮度降低。选项绑定仍在下次游玩/预览准备时冻结，不改活跃场景。

effects 每项必填 id/type/properties。blur 允许 radius；glow 允许 radius/strength/colour；outline 允许 width/colour；shadow 允许 x/y/blur/colour。radius、blur 为0..64，strength为0..4，描边width为0..32，阴影偏移为-128..128，颜色沿用上表。效果会增加中间图片成本。

### 标签与数字整体居中的横排

container可静态声明 `"layout":"horizontal"`。直接孩子按源文件顺序排列，整组占位矩形的并集在该容器内水平、垂直居中；`gap-px` 是相邻占位之间的有符号间隔，负值允许重叠，也允许后面的孩子延伸到第一个孩子左侧。透明或visible=false的孩子保留占位，不会改变其它孩子的位置。父容器的既有target、比例位置、原点及C3槽位矩形不变；例如 `x/y:0.5, origin:centre` 将整组中心放在原槽位中心。

```json
{
  "id":"my.judge-row", "type":"container", "target":{"kind":"global"}, "blend":"alpha",
  "properties":{"layout":"horizontal","gap-px":-16,"x":0.5,"y":0.5,"origin":"centre"},
  "effects":[], "children":[
    {"id":"my.judge-label","type":"sprite","target":{"kind":"global"},"resource":"ref.grade",
     "blend":"alpha","properties":{"height-px":100},"effects":[],"children":[]},
    {"id":"my.judge-digits","type":"text","target":{"kind":"global"},"blend":"alpha",
     "properties":{"bitmap-font":"digits.score","font-size":100,"format":"integer","text":"12"},
     "effects":[],"children":[]}
  ]
}
```

这是已有Provide所有者内的子树片段，资源和字体须先在清单声明。横排至少有一个直接孩子；只接受无children、无独立slot的sprite和位图text叶子。sprite必填height-px；省略width-px时按本帧已准备纹理的宽高比求宽，因此切换250×100、204×100、160×100标签时自然宽分别变化，不会统一拉伸。显式width-px固定占位宽。位图text按当前字符和font-size求自然宽，取总advance与各字形图片右边界的最大值：字形宽74、advance55时，两位宽129，三位宽184；字形实际绘制步长仍为55。高度为font-size。

这些px与font-size同属引擎逻辑像素，不能直接当窗口像素；来源画布高1080、游戏逻辑高768时，所有高度、间距和字号均乘768/1080。横排会随当前文字和资源变化重新测量，首帧、晚挂载与独立模板实例使用相同规则，无需脚本或额外时钟。

layout/gap-px/width-px/height-px不能被动画、绑定、状态机或脚本写入；gap-px只能随horizontal声明，两个像素尺寸只用于横排的sprite直接孩子。直接孩子不能声明或被程序修改x/y、width/height、scale、rotation、anchor/origin、alignment、layout-surface；可改文字、字号、资源、颜色、透明度与可见性。脚本对直接孩子只可写alpha。横排、直接孩子及其祖先不能挂C5 effects，避免动态自然宽绕过固定效果缓冲预算。需要统一位移、缩放或旋转时，控制父横排容器。

## 皮肤配置项

玩家在设置中选择 BMS / mania、模式默认或某个键数，再调整卡片上皮肤的配置。皮肤选择和配置分别支持键数覆盖：未指定的键数跟随模式默认；未修改的配置继承同一皮肤的模式配置，再取作者默认，不跨皮肤继承。恢复操作只清除当前层。浏览设置不会切换玩法、谱面或当前皮肤；配置在下次进入游玩或重新打开预览时冻结，不改进行中的一局，不写皮肤或外部来源目录。未声明选项的旧包继续使用原有表现。

清单可选 `options` 数组，支持 `choice`、`toggle`、`number`：

```json
"options": [
  {"id":"layout","type":"choice","label":"游玩区域样式","default":"p1",
   "choices":[{"value":"p1","label":"1P"},{"value":"p2","label":"2P"}],
   "keymodes":["5k","7k"],"effect":"presentation"},
  {"id":"notes","type":"choice","label":"音符","default":"normal",
   "choices":[{"value":"normal","label":"普通","preview":"previews/notes-normal.png"}]},
  {"id":"beam","type":"toggle","label":"显示光柱","default":true},
  {"id":"strength","type":"number","label":"效果缩放","default":1,"min":0.5,"max":1.5,"step":0.05}
]
```

每项必填 `id/type/label/default`；choice 必填非空 `choices`，每项为 `value/label`；number 必填有限 `min/max/step`，要求min<max、step>0，范围和默认值都在min+n×step上。默认值使用对应 JSON 类型。id和choice value沿现有stable ID语法，最多128字符；可选 `rulesets`（`bms/mania`）和 `keymodes` 为非空数组，token仅小写ASCII字母、数字、点、连字符，最多80字符。省略表示通用；它们不会增加玩法或可打车道。mania 双舞台使用实际键数向量，例如 `7k-7k`。

`effect:"presentation"` 只用于 choice，值限 `p1/p2/center-p1/center-p2`，交给各玩法既有布局准备入口；每个适用组合只能有一项。作者默认替代旧全局 BMS 样式设置，未声明仍取原居中默认。mania侧置还需匹配的layouts；仅声明choice而没有布局时保留原居中几何。

只读来源 `layout.ruleset` 将当前context投影为 `bms/mania`，可写普通文字或用于resource/visible variants；不修改玩法。需要给两种计分格式不同排版时分别放节点并按此显隐。option toggle显式投影的0/1，以及min和step均为整数的number，还可用于已有整数位图格式；其它bool来源和不符合整数约束的option不因此获准。

场景 binding 的 `source:"option.<id>"` 读取冻结值：toggle 可驱动 visible / 普通文字或作0/1数值；number 可驱动已有受限数值属性或数值文字；choice 写普通文字。variants 同样接受 `option.<id>`：choice 的 case key 来自 choices，toggle 为 `true/false`；按原规则提供 default 与至少一条 case，控制 resource、visible或横向x/width。x/width使用数字default/case值，沿同一属性的有限数值/范围限制，不开放其它数字属性。所有资源仍须提前声明并受完整包预算限制。没有命中适用范围时，binding 保留作者原值，variant 使用自己的 default；未知来源拒绝。用不同节点承载输入显隐和用户开关，避免两个写入者争抢同一属性。

最多32项、每项32个选择、标签128字符，每个适用列表最多32个token；数值绝对值最多1,000,000、区间最多10,000步。坏声明拒绝整份清单；保存值失效、越界或类型失配时使用作者默认。配置只控制表现，不能改输入、计分、判定、谱面或视频时钟。

choice可选`preview`，引用包根相对路径的静态PNG；不允许绝对/网络路径、穿越、冒号或其它格式。省略字段表示仅文字，不填写null。可为爆炸、音符、判定、光柱、转盘提供代表图；框体/全连等不适合小图的选项可省略。界面显示选中图，点击该配置整行或箭头直接展开可搜索、每页12项的全部款式图卡，点图才保存选择；没有预览的旧包保留文字下拉。

单图最多512×512、256KiB，最多4096个不同文件、合计32MiB编码字节。预览有独立预算，不计入游玩资源/像素预算，仍受整包归档和捕获总量限制；不能以预览代替场景声明的资源。准备时捕获图片头与不可变编码字节，可见区域才解码，离屏回收；不会播放作者脚本或实时读取来源。`SkinAuthoring check`完整解码全部声明图，应先检查再打包。像素损坏时设置显示“无法预览”，非法路径或尺寸在包准备时拒绝。

## 按选项组合内容

大量备选美术可放在同一包，根清单声明 `"contentCatalog":"gameplay/content.json"`。目录合同为 `oms-gameplay-skin-content.v1`，必填 `modules` 数组；每个模块使用 `id/label/default/choices`，可选 `rulesets/keymodes`，会成为玩家可搜索的下拉配置项。它与根options共用最多32项的上限。

```json
{"contract":"oms-gameplay-skin-content.v1","modules":[
  {"id":"frame","label":"Game Frame","default":"modern","choices":[
    {"value":"modern","label":"Modern","manifest":"parts/modern/manifest.json","scene":"parts/modern/scene.json","ini":"parts/modern/materials.ini"},
    {"value":"classic","label":"Classic","manifest":"parts/classic/manifest.json","scene":"parts/classic/scene.json","ini":"parts/classic/materials.ini"}
  ]}
]}
```

每个choice可省略全部贡献，表示此项不添加内容；manifest与scene必须成对，ini可独立提供。子清单沿普通资源、字形、layouts和animations语法，不允许嵌套contentCatalog、options、脚本或auxiliaryDisplay。根scene与子scene的root都必须是没有属性、效果及槽位的global中性container。选择完成后合并实际资源、场景、INI；相同ID的资源/字体等声明必须完全相同才可复用，不按“最后一个覆盖”掩盖冲突。INI按模块顺序合并，选定材料与布局在同一个root准备中冻结。

目录choice同样可声明`preview`，例如`"preview":"previews/frame-modern.png"`，使用上节相同路径、预算与静态代表图语义。它独立于贡献与variants，不改变游玩选中分支。

子清单的`scene`字段仍填写合同固定值`gameplay-skin.scene.json`；实际子场景路径取上方choice或variant的`scene`字段。这与根清单读取固定根场景的规则不同，不能把子场景的物理路径写进manifest合同头。

choice可声明 `variants`，例如 `{"when":{"layout":"p2","note-size":"short"},"manifest":"…","scene":"…","ini":"…"}`。when只读取另一项已冻结的choice/toggle，不引用自身、数值选项或未知项；缺省或 `orderedVariants:false` 时，同choice各variant条件键集合须一致、值组合唯一。命中后使用该variant的整份贡献；没有命中使用choice基本贡献。它不重新选择其它选项、不产生循环求值，也不允许游玩中换文件。

需要不同条件宽度时，可在该choice显式声明 `"orderedVariants":true`，并保留非空的 `variants`。此时各variant可使用不同的非空条件键集合，仍拒绝完全相同的条件组合；按数组顺序取第一项全部条件都满足的贡献，不合并后续分支。应将具体单舞台条件放在前面，较宽的双舞台或共用后备放在后面；例如先列 `{"layout":"p1","detail":true}`、`{"layout":"p2","detail":true}`，再列 `{"detail":true}`。最后一项不检查layout，可承接双舞台；这只是作者声明的顺序，不是引擎自动识别SP/DP。没有命中仍取choice基本贡献，旧包省略字段时保持原选择规则。

每模块最多1024个choice、全目录4096个、每choice64个variant；普通inline choice仍最多32个。目录最多4MiB，捕获闭包最多8192文件、512MiB，结构文件合计64MiB；普通作者工具整包4096文件/128MiB限制不因未声明目录而放宽。归档自身仍有16MiB配置总量门。所有分支的结构和图片头在选择前校验，实际选中的组合才解码像素，仍受384资源、64MiB图片字节、96MiP像素/384MiB解码及既有场景预算约束；普通未声明目录的包继续64MiP/256MiB。作者工具另外逐个完整解码全部PNG、检查每个choice/variant的代表组合；这不等于穷举所有独立选项的笛卡尔积，作者应检查自己允许的重型混搭。

## 辅助显示区

根清单可声明 `auxiliaryDisplay:{"enabledOption":"auxiliary-display","concentrationOption":"concentration","brightnessOption":"concentration-brightness"}`，对应根options必须分别为toggle、toggle、0..1范围number，且适用范围完全一致。只有显式声明且enabledOption开启的皮肤才创建独立辅助面板，关闭时不额外显示按钮；素材分别由Global `hud.auxiliary`、`hud.auxiliary-concentration` 的Provide和场景模板提供。两个画布的坐标均为完整0..1矩形，固定16:9，在主游玩之外的可收起面板显示；不使用主BGA开关，不改变主屏图层亮度或视频时钟。

游玩开始且正在演奏时，concentration选项可切换到第二画布，亮度只作用于其外层。暂停恢复普通画布；点击/触摸辅助内容恢复本局普通显示，暂停/恢复不会取消这次恢复，重新开局才重新进入专注状态。自定义图标只是作者组件；它不会凭空获得账号、真实触控菜单或新游戏操作。

BMS可额外显式Provide Stage `playfield.lift.fill` 与 `playfield.lift.decoration`，显示位置跟随原生LIFT已经应用的几何结果。fill保留完整stage画布并裁切下端/反向上端，不压缩源图；decoration的局部y=0位于实际边缘，文字不会倒置。未声明的包不会新增这两种材料或显示节点；mania没有原生LIFT机制，不能用皮肤配置伪造。

只读 `lift.value` 是原生已应用的0..1000单位，`lift.offset`为实际几何偏移比例0..1；`lift.available`和`lift.reversed`为布尔。它们随同Timing快照、跳转与重试更新，不允许皮肤修改挡板参数。理论满分 `score.maximum`取真实计分器的MaximumTotalScore，`score.maximum-available`在其大于0时为true，`score.progress`是当前分数/理论满分的0..1比例；不可用时为0，应使用available控制未知满分显示。它不是对手成绩、历史曲线或IIDX EX SCORE的统一刻度；BMS/mania各自保持原计分语义。

## 记录动画

复杂逐帧图形可使用数据记录文件，不需要把源游戏的脚本或运行时加入OMS。清单声明 `animations:[{"id":"clip.example","path":"clips/example.json.br","resources":["texture.example"]}]`；节点使用 `type:"recorded-animation"` 和 `animation:"clip.example"`，不再指定resource。文件可为JSON或Brotli压缩JSON，路径必须在包内闭包中。

记录节点还可显式写数值 `clock`，例如 `"clock":"preparation.elapsed"` 或 `"clock":"preparation.ready-elapsed"`，按该来源的毫秒采样原记录；不写时保持原trigger语义。此字段只允许在recorded-animation节点，布尔或文字来源不接受。所选clock不可用时隐藏片段；准备/READY来源分别沿对应准备状态，`presentation.elapsed`则在准备或真实Start任一可用时采样。终止空帧不参与循环，非循环片段到达该边界后隐藏，不延长末帧。它不产生按键、判定或脚本事件。

合同为 `oms-gameplay-recorded-animation.v1`。必填width/height/fps/loop/trigger以及geometries/nodes/states/frames：geometry保留XYZUV顶点、三角形索引和纹理ID；纯色图形使用resource:null及colour RGBA。nodes使用指向前序父节点的parent、geometry索引或-1、normal/additive混合；state保留12个矩阵数、三维origin、affine/perspective投影、multiply/add/HSL颜色以及可空mask/20系数colourMatrix。frame按父先子后顺序给nodes的[node,state]对及可空camera[cx,cy,cz,focal]。最后必须有一个没有节点的终止帧；时长按其前面的实际源帧计算，循环不重复终止帧。

循环片段可在记录文件中指定 `loopStartFrame`，表示开头播放一次后，重复从该帧到终止空帧之前的区间。例如301帧（第300帧为空）与 `loopStartFrame:180` 会先播放0～299，再重复180～299。此值只接受 `loop:true` 且无 `heldLoop` 的片段，范围为0～倒数第二帧；省略时为0，保留整段循环。它不截断或延长源帧，也不增加逐帧资源或运行时脚本。

trigger限gameplay.attach/gameplay.complete/judgement.hit/input.key.down。按键光柱可另外声明heldLoop:[start,end]与releaseFrame，分别保留按下的开头、按住的循环和放开的尾段；它们只用于input.key.down且loop=false。镜像、剪切、透视裁切、真实祖先多边形mask和源三角形由同一游玩时钟采样。滤镜处理顺序属于此数据合同的渲染定义，不能仅凭成功导出声称与某外部游戏逐像素一致。

每选中组合最多32记录，单条最多3600帧/4096节点，单文件编码16MiB、解压128MiB；总状态131072、节点32768、顶点262144、帧内应用200万均有界。记录资源继续占普通纹理预算；实际原生对象池会计入节点与三角形开销。用于原生HitExplosion的一次性记录最多1000ms，完整时长由对应效果池保留；全连仍须有gameplay.full-combo可见绑定及有界completionOutro/结束轨迹，不能延长音符或游戏判定。

## 显式舞台与留空位置

保留框体空位时可在同一清单添加 `layouts`，交给原玩法 solver 一次解算后发布；没有匹配声明仍走原 INI / 默认布局。最多32种配置、每种2个舞台、每舞台10条真实轨道。以下仅为已有4列舞台的片段：

```json
"layouts": [{"id":"example.four","ruleset":"mania","keymode":"4k","presentation":"any",
  "stages":[{"rect":[0.25,0,0.5,0.7],"lanes":[[0.1,0.18],[0.3,0.18],[0.5,0.18],[0.7,0.18]]}],
  "keyAreaHeight":0.1,"gaugeHeight":0.08,"hitPosition":1}]
```

`ruleset/keymode/presentation` 匹配当前上下文，精确 presentation 优先于 `any`。stages 按真实逻辑舞台顺序；`rect:[x,y,width,height]` 相对安全区。`lanes:[[x,width],...]` 按舞台真实逻辑轨道顺序，相对完整stage；缺口只是空白，不是新的输入轨道。可选 keys 同样逐轨描述键图宽度，允许向外扩展，但须保持在安全区且互不重叠；省略沿用 lanes。

keyAreaHeight / gaugeHeight 相对安全区高度，分别限0..0.18 / 0..0.25；hitPosition 相对舞台高度，mania限0..1，BMS须为1。舞台和轨道不得重叠，双舞台保持共同纵向跨度，预留键区、血条及间隙。声明必须与实际stage向量和轨道顺序一致；不能安全使用时记录稳定诊断并保留原有布局，不伪造轨道。`layout` 等名字没有隐藏的 IIDX 行为。

可选`modsRect:[x,y,width,height]`为已有原生Mod提示指定安全区内的位置，坐标相对同一安全区、尺寸须为正且全矩形在0..1内。它随同一有效layout冻结为`hud.mods`表面，沿C5全画布投影定位现有图标；不足时等比缩小，不放大。真实Mods、回放淡入、悬停展开与HUD可见性仍由原生控件负责；不新增轨道、输入或作者自造Mod。省略时保持原右上布局；不匹配或失效的舞台配置也不会单独应用这个矩形。

## 真实车道布局与 INI

下落区域和车道几何仍由公开 INI 控制，场景负责对应表面的图形。游戏按当前键数、样式、单/双舞台、屏幕比例与缩放统一准备；皮肤不能改判定时序或视频时钟。

| BMS [Bms] 字段 | 语义 |
| --- | --- |
| Keymode | 5K/7K/9K/9K_PMS/14K，修改对应块 |
| PlayfieldWidth/PlayfieldHeight | 下落区域整体宽高，安全区比例0.15..0.95 / 0.45..0.94 |
| PlayfieldSideInset | 仅5K/7K的1P/2P侧置样式：落键区距对应安全区边缘的宽度比例，0..0.5，默认0.05；空间不足时夹到可用左右余量的一半。居中样式、9K和14K不受此字段影响 |
| LaneSidePadding | 每个stage/deck外框宽度的对称左右内边距，0..0.25，默认0；真实车道和间隔一起映射到内部宽度，双deck分别应用；不缩小外框或改变BGA避让边界 |
| DualStageGap | 仅14K：中间空隙占整体落键区外框宽度的比例，0..0.5；声明后两侧deck各分剩余宽度的一半。省略或非法时保留既有双deck间距，不等于显式0 |
| KeyAreaHeight | 判定线以下独立键盘区高度，安全区高度比例 0..0.18；默认 0，保留原判定区内的键图位置 |
| ScratchKeyWidth | 独立键盘区中转盘视觉宽度相对于scratch音轨的倍率，1..4，默认1；向普通键之外扩展，受安全区与BGA限制，不改变音轨 |
| BgaInformationHeight | BGA上下信息区域总高度，安全区高度比例0..0.30，默认0；大于0才发布四个information区域 |
| GaugeHeight | 血槽区域占安全区高度的比例，0.02..0.12，默认0.036；与独立键区、信息区共同避让 |
| BgaWidth/BgaHeight | BGA 最大框的安全区宽高比例 0.01..1；指定尺寸后在可用侧区按 4:3 适配。缺省宽为 0.225（14K 为 0.13），高上限 0.30 |
| BgaVerticalPosition | 单侧单窗在剩余竖直空间中的位置 0..1，0 顶部、0.5 居中、1 底部；14K 上下窗在各半区对称向中间定位，窄屏底窗固定；默认 0 |
| BgaViewports | 明确窗口列表：`x,y,width,height,fit; …`，相对安全区域，mode可fit/fill/stretch，最多16窗；`none`关闭窗口；详见下文 |
| BgaViewportsP1 / BgaViewportsP2 | 同一键数桶中，分别覆盖5K/7K的1P/2P停靠样式；语法与BgaViewports相同。缺少该侧合法声明时使用本桶BgaViewports或旧自动布局；居中、9K和14K仍用通用声明 |
| NormalLaneWidth/ScratchLaneWidth | 普通键/转盘的相对宽度权重 |
| BlackLaneWidth | 5K/7K/14K每侧第2/4/6键的相对宽度，合法0.25..4，与NormalLaneWidth同单位；只读选定包，缺失/非法沿用已解析普通键宽。9K不应用；不改变键身份或滚动时间 |
| NormalLaneSpacing/ScratchLaneSpacing | 对应间隔权重 |
| HitTargetHeight/HitTargetBarHeight/HitTargetLineHeight | 判定区域、条和线的尺寸输入 |
| HitTargetGlowRadius/BarLineHeight | 判定线辉光半径、小节线厚度 |
| LongNoteBodyWidth | 相对车道宽，有限且0<value<=1 |

BMS 不接受 mania 的 HitPosition、逐列 ColumnWidth 或自创 HitTargetVerticalOffset。例如略减新作品每个 BMS 块的 PlayfieldWidth，检查后比较5K/7K四样式与14K，不能只改第一块就当作全键数完成。

BMS 在底部保留 8% 的独立信息区；落键区、独立键盘区、血条与 BGA 共同避让，旧自动布局空间不足时可缩短落键区，显式窗口冲突则按下文整组关闭。**未声明BgaViewports时**，BGA按1P/2P放旁侧，14K默认两侧上下四窗，侧边不足时使用底部小窗。PlayfieldSideInset、LaneSidePadding、DualStageGap、KeyAreaHeight、ScratchKeyWidth、GaugeHeight、BgaInformationHeight和BGA布局参数只读取当前选定包对应键数块，缺项不继承默认静线的作者布局。三个新宽度字段都要求有限数；非法值产生 `bms.layout.invalid-playfield-side-inset`、`bms.layout.invalid-lane-side-padding` 或 `bms.layout.invalid-dual-stage-gap` 诊断，并沿上表缺省行为。布局仍由同一个求解器提供真实车道、键区、外框与BGA区域，不改变输入身份、滚动/判定时序或视频时钟。独立转盘键图按原图比例居中；旧键图继续随Lift与滚动方向移动，独立控制台保持在落键区下方。

`BgaViewports`按声明顺序给scene索引0..N-1。x/y非负，宽高为正，x+width与y+height不超过1；只接受有限、使用小数点的数值，mode小写。fit完整显示并留边，fill中心裁切填满，stretch拉伸。谱面时间线先在共享4:3画布合成，再适配各个矩形；没有时间线的静态背景按自然比例适配。语法/数值/越界/mode/超16窗会被CLI拒绝；实际与键区、血槽或信息区冲突则整组0窗，不改作者数量或位置。这个冲突必须进入当前样式与屏幕比例下检查，在日志目录的`runtime.log`搜索`bms.layout.bga-viewports-unavailable`；设置没有专属布局错误面板，离线check不证明实际布局可用。`none`明确0窗，不等于缺省；`BgaInformationHeight`大于0仍独立保留四类信息区域，不因关闭BGA而删掉歌曲/HUD。原生BMS无窗口时省略BGA专属场景装饰，仍完整校验声明、素材和所属部件；mania及BMS转谱不因此获得BGA支持。完整小包见[BGA Layout](examples/bga-layout/README.md)。

`BgaViewportsP1`、`BgaViewportsP2` 只对实际1P/2P停靠样式生效，字段大小写须完全一致，不自动镜像坐标。它们与通用声明使用同一个语法、16窗上限、安全区坐标和碰撞检查；只读取当前选定包的同一 `[Bms] Keymode` 桶，不继承默认包或其它键数的安排。每种声明独立保留最后一次成功解析的值：非法重复行不会抹去该种已接受值，但会记录同一解析诊断；该侧从未成功解析时沿通用声明/自动布局并保留该侧错误。其它样式不报告未使用侧的解析错误。该侧显式 `none` 就是0窗；已接受的侧置安排若碰撞也是整组0窗，不再换回通用安排。通用 `BgaViewports` 继续供居中样式、9K与14K使用，所有窗口仍共享原谱BGA内容和时钟。

纯纹理 `hud.gauge` 资源是一张完整满槽图：引擎固定显示其25%亮度暗槽，再按真实血量从左揭露原色图，不缩放图内格距。零血量仍显示全长暗槽；作者可在文件里绘制分段和边框。有scene节点的血条保持作者自己的绑定表现。

静线的信息元素各自使用 `slot: "hud.text"`，从对应玩法的信息区域定位；无槽位的共同父容器保持 `blend: "inherit"`，不嵌套槽位。BMS 因而位于底部，mania 保留顶部；显式 global 且无槽位的子节点仍使用整屏，不能据父节点属于 HUD 就假定它不会遮挡落键区。

mania 在对应 [Mania] Keys 块修改 ColumnWidth、ColumnSpacing、HitPosition 等既有设置，数组项数必须对应该块，双舞台和特殊键保持模板映射。KeyImageN/KeyImageND 分别提供松开/按下图片，模板对 playfield.key 保留 Inherit 才能使用同包这对素材。关闭可选闪光不会关闭按下图片；BMS普通按键图片也沿真实输入反馈。

公共资源声明的四种基本 Target 形式如下。每种都可另加 `presentation=样式名`；省略等同 `presentation=any`：

```text
Global ruleset=… keymode=… stage-mode=…
Stage ruleset=… keymode=… stage-mode=… group=… group-logical=… group-visual=…
Group ruleset=… keymode=… stage-mode=… group=… group-logical=… group-visual=…
Lane ruleset=… keymode=… stage-mode=… group=… lane=… group-logical=… group-visual=… global-logical=… global-visual=… group-local-logical=… group-local-visual=…
```

ruleset 为 any/mania/bms，stage-mode 为 any/single/dual。keymode 为 any、BMS的5k/7k/9k-bms/9k-pms/14k、mania单舞台Nk或双舞台Nk-Mk。不要混淆旧 [Bms] Keymode 的大小写。section 必须匹配部件目录：通用部件（包括 BGA）用 [GameplaySkin.Common:1]，只有 BMS 扩展部件用 [GameplaySkin.Bms:1]。通过 Target 的 ruleset=bms 限定玩法，不通过更换目录限定；下一条 Target 前的资源行属于当前目标。

公共 INI 段可用 `#` 或 `;` 在引号外开始行内注释，也可单独写一行 `// 说明`，行首空白不影响。不要把 `// 说明` 接在资源声明或公共段标题后；这种行内写法仍会报错。资源双引号内的 `//` 不会被截断，但文件路径仍须通过正常检查。此版本也能读取普通导入时游戏在新 `[General]` 前添加的整行说明，已有导入作品无需为这条说明改文件；不增加权限，也不写入外部作者目录。

BMS 5K/7K 的四种样式使用以下 `presentation` 值。配方会为四种样式分别生成声明；同一侧转盘可共用图片和完整轨道位置，但不能把不同位置写成两组未限定样式的声明。

| 游戏内样式 | presentation | 轨道位置 |
| --- | --- | --- |
| 1P（居左） | `p1` | 左皿 |
| 2P（居右） | `p2` | 右皿 |
| 居中（左皿） | `center-p1` | 左皿 |
| 居中（右皿） | `center-p2` | 右皿 |

例如以下目标只用于 7K 的 2P 样式：转盘稳定名称与逻辑位置仍为 scratch-1/0，视觉位置为该布局的 7。

```ini
Target: Lane ruleset=bms keymode=7k stage-mode=single presentation=p2 group=bms.group.deck-1 lane=bms.lane.scratch-1 group-logical=0 group-visual=0 global-logical=0 global-visual=7 group-local-logical=0 group-local-visual=7
```

9K 两格式和 14K 使用其实际居中布局，可省略 presentation；mania 和共用文字、装饰也可保持省略。Global/Stage/Group 同样支持此属性；它只选择声明，不改变玩家的玩法设置。值须为不超过 80 个字符的小写 ASCII 字母、数字、点或连字符，`any` 匹配所有样式，其它值与实际样式名称精确匹配。语法正确但拼写不对应当前样式的值不会生效，因此应按表填写，并实际验证所有目标样式。

非当前样式的声明不应用，也不会报告“轨道位置不匹配”。当前样式中的稳定轨道名称、所属舞台及全部位置仍须准确；写错仍会报告 `OMS-SKIN-CODEC-021`，不能靠另一条正确声明消除错误。省略或填写 any 保留旧的严格检查，不会自动判断两组坐标各属于哪种样式。

使用该字段须配套支持 presentation 的皮肤样式提示修复版 OMS 与作者工具；较旧客户端会把它当未知字段拒绝。已有作品若包含未限定样式的左右两组声明，应在自己的作者副本中补齐四种样式，检查、打包后正常导入新版本；放在固定 `chartskin/` 下的作者目录，可在退出游玩与预览后点击设置 → 皮肤 → “刷新皮肤”；旧版已登记的外部目录仍可重新载入，但设置不再提供注册入口。游戏不会替作者改写原包或外部目录。

资源行是 `槽位ID: resource Provide "图片基名"`、`槽位ID: resource Inherit` 或 `槽位ID: resource Suppress`。精确玩法优先，再比较键数、舞台模式、精确 presentation、Lane/Group/Stage/Global和同等级后行；错误的精确声明不回头拼本包更宽声明。必备/推荐部分不能关闭，明确关闭的可选部分不会偷偷恢复。缺少必要部分由正式静线补齐。全部资源名和适用范围见 [CATALOG](CATALOG.md)。

## 动画、状态机、绑定、变体、模板

tracks 每项必填 id/type/target/property/easing/loop/keyframes，可选 `trigger` 或 `clock`，两者不能同时声明；target为节点id。帧必填id/time/value，time须有限、严格递增、绝对值不超过86400000，至少一帧。仅`loop:false`且显式`clock:"option.<id>"`引用min小于0的number配置时允许负time；其它轨迹仍只接受非负time。负关键帧没有扩大循环或事件轨迹的时间域。缺省time单位为游玩毫秒；显式clock时使用该数值来源的单位，number配置的原值直接采样，不转成毫秒或绑定到配置最小/最大值。

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

tween只改变数值属性；tween配step可逐帧保持数值，在关键帧的精确时刻切入新值。frame给sprite的resource换帧时，value必须是清单资源id；projective-sprite还接受下述projective-quad帧。easing为step/linear/in/out/in-out。循环周期来自最后帧，循环动画应给正周期；负的来源值也按正模取周期，因此反向运动能连续向回采样。非循环轨迹在首帧前、末帧后分别保持首帧、末帧。省略 `trigger` 和 `clock` 时仍按全局游玩时间采样，已有包不改变语义；暂停不推进，重试/跳转使用新位置。

`clock` 是一个已有数值只读绑定来源字符串，适用于tween和frame，例如 `"clock":"timing.beat"`、`"clock":"timing.elapsed"` 或 `"clock":"arrival.remaining"`。普通数值来源、number配置及toggle的显式0/1数值投影均可；其它布尔、choice和状态文字来源不可以，也不接受表达式、私有时钟或外部输入。它只决定读取关键帧的位置，不改变游戏时钟、输入或判定。以下已有节点的轨迹每拍变亮一次，time单位为拍：

```json
{"id":"my.beat-pulse","type":"tween","target":"ref.icon","property":"opacity",
 "clock":"timing.beat","easing":"linear","loop":true,
 "keyframes":[{"id":"my.beat-pulse.start","time":0,"value":1},
              {"id":"my.beat-pulse.dim","time":0.5,"value":0.2},
              {"id":"my.beat-pulse.end","time":1,"value":1}]}
```

`timing.elapsed` 从真实Gameplay Start起计。尚无Start的设置预览或尚未开始的宿主不会采样该轨迹，保留作者原属性；从已开始状态Reset到无Start时恢复该属性，再等待真实Start。普通 `timing.time` 仍可在前奏为负。自选clock不携带触发器起点，也不扩大记录动画文件的trigger语法；完整结尾等待仍须使用下文的 `gameplay.complete` 轨迹。

`presentation.elapsed` 用于需要从准备动画连续播放到游玩的装饰，数值为本次准备已过毫秒加上 `timing.elapsed`。准备自然完成后保留其已过时间，游玩开始后继续累加；暂停沿现有游玩时钟停止，跳过准备则只剩实际游玩时间。没有准备提供者且尚无真实Start时不可采样；Reset撤回这两种来源后恢复普通轨迹原属性并隐藏记录节点。它只派生已有的准备与游玩时间，不创建新的计时器、事件或玩法状态。

### 开局准备阶段

场景顶层声明正的`preparationDuration`后，Player在原歌曲计时开始前播放一次准备阶段，使用父Player的真实更新时间；歌曲、输入判定和回放时钟保持原位置。BMS以此替代原开局等待，不叠加两次等待；其它玩法只有皮肤显式声明才增加准备。暂停冻结准备；BMS按住原调速键会延长剩余等待，已完成美术不重播，松手沿原等待合同处理。最后1500毫秒提供READY；较短准备从相应片段中途采样。自动跳过前奏的快速重试保持原快捷路径，BMS保留原有强制等待/按住调速合同。

进入时先建立停表屏障；Player与游玩画面完全显现、场景节点实际就绪后，首个更新从零建立准备时间样本。载入、切屏与渐显不消耗开场，后续按同一父时间域自然推进；暂停不补算，暂时不可见不重播已完成相位。首排预告若须与准备一起结束，可给独立父容器绑定`preparation.active`，不改变图片上原生`arrival.cue-opacity`的滚动退场含义。同一节点的`visible`和`opacity`都写透明度，不能将这两道门叠在同一图片节点上。

`preparation.elapsed/remaining/ready-elapsed`为只读毫秒，`available/active/ready`为只读布尔。自然完成保留available与已完成elapsed，active/ready关闭；跳过前奏或退出撤销准备。预览和无提供者宿主不可用。以三个准备数值为clock的轨迹在来源不可用时保留或恢复作者原属性；记录节点则隐藏，因此取消不会让半程开场冻结覆盖音符。作者不应以它们改变判定、难度或音频位置，配置也不能在游玩中修改准备长度。

### 已投影纹理的四角动画

需要保留已有透视动画时，可使用 `type:"projective-sprite"`，通过普通清单resource提供纹理；此类型按完整纹理的固定UV绘制双面四边形。`frame`轨道的property为 `projective-quad`，easing必须为step。value必填 `vertices/opacity`，四角顺序为左上、右上、右下、左下（保持纹理角的身份，背面也不重新排序）；每个角为 `[x,y,q]`。xy相对于该节点矩形，有限且各在−4..4；q为正的倒数W，范围1/256..256；opacity在0..1。q用于正确插值纹理，不能把透视四角当成普通拉伸图片。

```json
{"id":"my.projective.frames","type":"frame","target":"my.projective","property":"projective-quad","easing":"step","loop":false,"trigger":"gameplay.complete",
 "keyframes":[{"id":"my.projective.start","time":0,"value":{"vertices":[[0,0,1],[1,0,1],[0.8,1,2],[0.2,1,2]],"opacity":1}},
              {"id":"my.projective.end","time":2500,"value":{"vertices":[[0,0,1],[1,0,1],[1,1,1],[0,1,1]],"opacity":0}}]}
```

这是已有sprite节点和完整scene内的轨道片段，仍需声明清单资源及所属槽位。四角跟随同一游玩时钟及trigger，按实际角点参与屏幕裁切；需要限制画布范围时用普通clip父节点。帧数、轨道、节点和纹理继续计入原预算。quad不接受tween、binding、状态赋值、静态properties或脚本写入；此类型当前不提供resource变体、状态换图或fill-mode，不加载作者相机、网格程序或着色器。

判定图和击打效果可以显式写 `"trigger": "judgement.hit"`，使帧时间相对真实判定发生时刻从0开始。`trigger` 只接受 `judgement.hit` 或 `gameplay.complete`，不接受输入事件或其它状态机事件。例如先在清单声明两帧图片，再写一项轨迹：

```json
{
  "id": "my.hit-animation", "type": "frame", "target": "my.hit-sprite", "property": "resource",
  "easing": "step", "loop": false, "trigger": "judgement.hit",
  "keyframes": [
    { "id": "my.hit-first", "time": 0, "value": "texture.hit-0" },
    { "id": "my.hit-second", "time": 40, "value": "texture.hit-1" }
  ]
}
```

作用范围沿节点的真实所有者：对象专用表现优先取精确对象，其次为节点所在lane、stage/group，最后才是global；缺少局部判定不会借用另一轨道的判定。普通global/stage/lane节点在该范围的新判定到达后重新起播。原生池化击打效果每次应用各有独立起点：即使CN头尾共用同一长条身份，新效果仍从首帧开始，已存在的效果继续原相位。暂停沿原游玩时钟冻结，重试、seek/rewind或其它epoch替换会清除旧起点，按新完整快照中的当前事实恢复，池化复用也不带入上次相位。

未触发时采样首帧，触发本身不控制可见性、生存期或判定等级筛选；这些仍由槽位、状态机和原生效果所有者负责。这里的 `judgement.hit` 也包含真实Miss，不等于只显示成功击打。`loop:false` 到末帧后保持末帧，是否消失由同一所有者决定。

全连演出可用 `gameplay.full-combo → visible` 绑定控制父容器，子图片的换帧、位置、透明度轨迹写 `"trigger":"gameplay.complete"`。该触发取引擎完成整张谱面的真实时刻，全局和各舞台共用一个起点，不取最后一次击打或BGA结束时间。完成前采样首帧，因此仍需父容器绑定；非全连完成也会有完成触发。暂停与变速沿既有游玩时钟，晚挂载和完整快照恢复保留原起点，已经结束的演出不会重新播放。跳转仍位于同一次已完成演出后时保留起点，回退到完成前或重试后按新的真实完成恢复。不要让父容器的visible绑定与孩子的透明度动画写同一节点；透明尾应显式写到末帧，防止末图一直保留。

需要超过原1000毫秒结算等待时，在场景顶层写 `"completionOutro":2500`。这是从完成原点计算的演出总时长，不是额外再加2500毫秒。正值仅在同一合法场景含 `gameplay.full-combo → visible` 绑定、至少一条覆盖声明时长的非循环 `gameplay.complete` 轨迹，且全部完成轨迹均不循环时接受；上限3500毫秒，越界为025预算错误，缺少这些消费者为023动画错误。只有实际全连且当前作者场景准备完毕、没有运行故障时才延长等待；0、缺省、非全连或失败保持原等待，玩家主动跳过结尾仍沿原操作。等待读取同一游玩时钟，暂停不会用墙钟耗尽演出，也不会因状态机重投影而重启等待。不提供脚本或任意循环延迟结算的能力。

编号PNG短键/长条动画是另一条路径：名字-0.png、名字-1.png等，从0连续编号、固定60帧/秒，不写上述关键帧JSON。

| 状态机对象 | 全部必填字段 |
| --- | --- |
| stateMachines项 | id/initial/states/transitions |
| states项 | id/set |
| set项 | id/target/property/value |
| transitions项 | id/from/to/event |

initial/from/to引用本状态机已有状态；target引用主场景或模板节点，value匹配属性类型。完整练习 ref.lifecycle 可直接查看准备、运行、暂停写法；仓库历史星轨示例还含完成和失败。

合法事件字符串为 gameplay.attach/loaded/start/pause/complete/fail、input.key.down/up、object.spawn/state、judgement.hit、timing.stop、bga.state。它是当前完整事实的投影，新加入、跳转、重试会重建，不适合任意历史累计。gameplay.resume不是合法字符串；恢复后的Running由当前状态重建。历史组合使用获准脚本。不要让轨迹、状态机和绑定同时争抢同一属性；例子分别写旋转、状态文字和信息。

bindings每项必填id/target/property/source，例如：

```json
{ "id": "my.accuracy", "target": "ref.accuracy", "property": "text", "source": "score.accuracy" }
```

| source | 值 | 能驱动的属性 |
| --- | --- | --- |
| layout.stage/layout.group/layout.lane | 目标稳定身份字符串，不是矩形或对象 | text |
| layout.presentation | 已应用布局上下文的PresentationStyleId，精确字符串token；支持text和variants，不读取未适用的保存选项 | text或variants |
| input.pressed | 当前目标轨道是否按下 | visible或text |
| input.motion-counts-1 / input.motion-counts-2 | 对应逻辑组累计的带符号相对移动counts，已应用实际绑定的灵敏度与反转 | 数值或text |
| input.motion-revolutions-1 / input.motion-revolutions-2 | 对应逻辑组累计的带符号圆形轴或摇杆模拟转数，已应用实际绑定配置 | 数值或text |
| input.motion-available-1 / input.motion-available-2 | 对应逻辑组当前是否具有可用的真实运动投影 | visible或普通text |
| gameplay.full-combo | 实际完成且达到引擎最大可达连击、未失败；未完成或空谱为false | visible或普通text |
| object.state | scheduled/visible/holding/hit/missed/completed/despawned | text |
| object.long-note-mode | none/ln/cn/hcn；实际对象的长条模式 | text |
| object.long-note-appearance | none/ln/ln-holding/ln-released/cn/cn-holding/cn-released/hcn/hcn-holding/hcn-released | text |
| object.auto-assist | 当前精确物件的自动辅助角色：none/scratch/note | 普通text或resource/visible variants |
| judgement.result | miss/meh/ok/good/great/perfect | text |
| judgement.offset | 当前判定偏差，毫秒 | 数值或text |
| judgement.timing-direction | 当前精确组最后一次合格输入的早晚提示；none/fast/slow | 普通text或resource/visible variants |
| judgement.keys-timing-direction / judgement.scratch-timing-direction | 同组键盘 / Scratch各自最后一次合格输入；none/fast/slow | 普通text或resource/visible variants |
| score.value | 当前计分器分数；原生BMS为EX SCORE，mania为自身计分器分数 | 数值或text |
| score.accuracy | 0..1；text为两位小数百分比 | 数值或text |
| score.personal-best / score.personal-best-progress | 同条件有效本地最佳分数 / 最佳分数除以当前理论满分后夹至0..1；不可用时0 | 数值或text |
| score.personal-best-available | 是否查得符合条件的本地成绩 | visible或普通text |
| score.pacemaker-next / score.pacemaker-next-progress | 按匹配的本地最佳成绩选择下一档目标 / 该整数目标除以当前理论满分；不可用为0 | 数值或text |
| score.pacemaker-next-label | LV A/A+/AA/AA+/AAA/AAA+或DJ AUTO；不可用为空 | 普通text |
| score.pacemaker-next-available | BMS EX SCORE、正满分与有效匹配最佳成绩均可用 | visible或普通text |
| score.rank-aaa / score.rank-aa / score.rank-a | BMS EX SCORE的AAA/AA/A阈值；不可用时0 | 数值或text |
| score.rank-aaa-progress / score.rank-aa-progress / score.rank-a-progress | 对应阈值除以当前理论满分；不可用时0 | 数值或text |
| score.rank-available | 当前是否具有BMS EX SCORE与正的理论满分 | visible或普通text |
| combo.value / gauge.value | 连击 / 归一化能量 | 数值或text |
| combo.maximum | 本局已经达到的最高连击；真实计分器快照的非负整数，断连后保留 | 数值或text |
| combo.active | 同一真实计分快照的当前连击是否大于0；不表示全连资格 | visible、普通text或布尔variants |
| gauge.type | none/assist-easy/easy/normal/hard/ex-hard/hazard | text |
| gauge.maximum / gauge.clear-threshold | 实际血槽最大值 / 通关阈值，与gauge.value使用相同0..1量纲；无此能力时都为0 | 数值或text |
| gauge.clear-state | unavailable/uncleared/clear；引擎当前通关条件状态 | text |
| timing.beat/timing.measure/timing.bpm | 拍、小节索引、BPM | 数值或text |
| timing.time | 当前权威游玩时间，毫秒，可为负 | 数值或text |
| timing.elapsed | 从真实Gameplay Start起的非负游玩毫秒；尚无Start时数值绑定为0 | 数值或text |
| presentation.elapsed | 本次准备已过毫秒加上timing.elapsed；准备和Start均不可用时0 | 数值或text |
| timing.start-available | 是否已经取得真实Gameplay Start起点 | visible或普通text |
| preparation.available / preparation.active / preparation.ready | 有准备进度 / 正在准备 / 正在最后1500ms且未按住调速 | visible或普通text |
| preparation.elapsed / preparation.remaining / preparation.ready-elapsed | 准备已过 / 剩余 / READY已过毫秒；不可用时0 | 数值或text |
| timing.beat-phase | 当前拍的小数部分，范围0≤值<1；负拍也保持此范围 | 数值或text |
| arrival.time / arrival.remaining | 当前目标范围首个真实可玩对象的时间 / 距该时间的剩余毫秒；无entry时0 | 数值或text |
| arrival.available / arrival.visible | 当前范围有首音entry / 有entry且当前时间早于首音时间 | visible或普通text |
| arrival.first-row | 当前目标首音是否属于全谱最早同一排；空目标为false | visible或普通text |
| arrival.cue-opacity | 全谱第一排预告0..1；实际音符进入可视区域前淡出，无原生可视投影时0 | 数值或text |
| song.title / song.artist / song.difficulty / song.level | 当前曲名、艺术家、难度类别或名称、原始等级文字；每项最多256个UTF-16单元 | text |
| song.table-classification | 已启用难度表归类，独立于作者标级；每项为“表名 等级标签”，多表按保存顺序以 ` / ` 连接，无归类为空。最多256个UTF-16单元，可使用 ellipsis。BMS及BMS转谱mania从当前游玩锁定的原谱资料读取 | text |
| timing.bpm-min / timing.bpm-max | 当前谱面实际音乐BPM范围，不含滚速倍率或STOP兼容点 | 数值或text |
| scroll.speed | 当前玩法已选滚速设置值，不是滚动倍率或绿数；未提供时0 | 数值或text |
| scroll.available / scroll.adjusting | 原生调速快照是否可用 / 玩家是否正按住Start调速；当前由BMS提供，mania为false | visible或普通text |
| scroll.mode | 原生模式normal/floating/classic；不可用为unavailable | text或variants |
| scroll.green-number | 由当前BPM、真实滚速、挡板和LIFT几何计算的绿字；不可用为0 | 数值或text |
| cover.sudden-units / cover.hidden-units | 上下挡板各自保留的0..1000长度；关闭不清值 | 数值或text |
| cover.sudden-active / cover.hidden-active | 原生上下挡板当前是否生效 | visible或普通text |
| judgement.count.perfect / great / good / ok / meh / miss | 计分器对应六档判定累计值 | 数值或text |
| judgement.count.fast / judgement.count.slow | 满档为Perfect的真实基础成功判定中，偏差小于0 / 大于0的累计数 | 数值或text |
| combo.breaks | 计分器断连统计 | 数值或text |
| offset.value | 玩法已经应用的显示时机偏移，毫秒；不可用时0 | 数值或text |
| offset.available / offset.automatic | 是否提供此偏移投影 / 对应原生自动调整是否开启 | visible或普通text |
| timing.progress | 可玩起止间0..1；text为整数百分比 | 数值或text |
| bga.content-state | empty/ready/playing/paused/failed | text |

字符串不能直接驱动数字或资源；换图用variants。数值限制在目标属性范围内，不修改玩法。首物件前进度0、末物件后1、零时长0；暂停保持、重试和跳转反映新位置。局部对象、轨道和组沿自身范围取值，不应由另一轨道的判定驱动。

调速与挡板读数随同一Timing快照和静止时钟下的真实调整同步；作者不得累积按键或自行反推可见时间。BMS的新游玩由游戏“读谱设置”提供Sudden/Hidden/Lift，不再要求选中同名Mod；作者应读取实际值和active，而非用Mod列表猜开关。上下白字分别用`cover.sudden-units`、`cover.hidden-units`，LIFT读`lift.value`，不能把Hidden长度或LIFT写成Sudden白字。只在`scroll.available`及`scroll.adjusting`成立时显示调整面板/读数，挡板行另检查各自active；mania及没有原生来源的预览应隐藏。`lift.reversed`可用true/false显隐变体，位置仍按实际`lift.offset`。`score.maximum-available`、`score.personal-best-available`与`score.rank-available`可用布尔variants的true/false分支，使主目标没有数据时才显示备用目标；两者都缺数据时显示NO DATA，不补造对手或成绩。

`arrival.*` 取应用Mod后的实际谱面和真实C3轨道，Lane为该轨首音，Stage/Group为该组最早首音，Global为全谱最早首音；空谱、空组及只作留白的作者槽没有entry，不借用别轨。BMS排除自动背景物件，mania取普通音符或长条的开始。`arrival.visible`只表示尚未到来，不限制提前多久；作者应另用非循环 `arrival.remaining` 轨迹定义预告窗，并将窗口两端透明度设为0，以免远处首音长期显示。剩余值可以为负，重试、seek和完整状态恢复直接使用新位置，不由皮肤累计倒计时。

首排预告应同时绑定`arrival.first-row`和`arrival.cue-opacity`。后者读取首排实际列的原生滚动位置，按当前滚速、方向、偏移、LIFT、STOP/SV预测200毫秒后进入；淡出后锁定，已经进入可视区域时立即清空，和玩家是否击打无关。普通跳转/倒带保持退场；真正重试才恢复。原逐轨`arrival.time/remaining/visible`语义保持，后续某轨首音不能变成第二次开局预告。双舞台共用全谱第一排，不各自等到首个命中。

两组motion来源的 `1/2` 分别指拓扑逻辑组0/1；单人切换左右位置仍使用1，双舞台右组使用2，不根据屏幕左右重新编号。counts和revolutions分别累计，不能把鼠标一个count当成物理一圈；作者可用不同父子容器及不同clock轨迹叠加常态旋转与两种真实增量。当前运动投影来自BMS已接收的真实轴信号，mania及无方向信号保持不可用；数字键、Auto和没有方向记录的旧回放不补造逆转。默认转速、阻尼及视觉比例属于作品，皮肤不能改写设备灵敏度、发送输入或改变判定。重试、seek及输入来源重置沿引擎快照同步，不能把累计值视为永久硬件里程。

FAST/SLOW读取真实HitEvent：只统计最大判定为Perfect且结果为基础成功击打的事件，零偏差两边均不计；Miss、自动长条body tick、长条父级记账及BMS空POOR不计入。计数随真实计分历史撤销和完整状态重建，不用 `judgement.hit` 在皮肤里再次累计。`offset.value`当前由BMS已经应用的VisualOffset提供，`offset.automatic`对应其原生自动显示时机调整开关；它不表示判定窗口或音频设备延迟。mania未提供相同投影，available/automatic为false、value为0；不可用时应隐藏读数，不显示伪造的已校准状态。

三项 `judgement.*timing-direction` 是单次输入提示，与累计FAST/SLOW及500ms的判定文字显示期分别保存。当前OMS资格为BMS真实按下/抬起（含原始回放输入）、非Autoplay、结果为Great/Good/Ok/Meh、偏差绝对值至少0.000001ms；Perfect、自动完成、body tick和超时Miss不生成或覆盖提示。Combined按交付次序更新，Keys和Scratch独立保留；SpecialKey归Keys。没有合格输入时为none，后续非合格判定保留原提示；重试、跳转、倒带清空，重挂与背压恢复最新完整状态。只读取节点的精确material group，Global不借用第一组；不能直接驱动数字时钟，也不新增脚本权限。此为明确的OMS显示策略，原机全等级资格、覆盖及保留规则尚未整体复核，不能把它写成IIDX完全等价。

`score.personal-best`只在选中场景实际消费最佳分数、比例或available来源时，于游玩后台加载一次。条件匹配当前本地玩家ID、谱面ID与hash、玩法及可比较的Mod设置；只选未删除、未失败、当前计分版本且具有效统计的本地成绩，BMS还校验其当前规则版本和结果统计。BMS比较忽略Sudden、Hidden、Lift的选用、位置、挡板透明度与记忆偏好，以及Auto Scratch/Auto Note的叠加着色开关和颜色；辅助Mod本身及其Hidden/Visible仍须匹配。Judge/Gauge、LN/CN/HCN、Random/Mirror等玩法条件继续区分，Random seed仍保留。已知Mod的显式默认设置与省略默认等价；未知Mod、未知设置和值保持严格比较，Sudden/Hidden/Lift若带未知设置仍保留其身份与未知字段。其它玩法保持完整Mod设置精确匹配。此规则只影响查询比较，不修改保存的Mod、回放或传分。没有成绩或查询条件不可用时available为false，0不是已查得的最佳成绩。它不查询线上排行榜，不把其它玩家或不兼容玩法条件的成绩作为目标；作者不能在游玩中发起数据库查询。比例使用当前理论满分，满分不可用时为0。

`score.rank-aaa/aa/a`只在BMS EX SCORE及正的理论满分可用时提供，分别为理论满分的8/9、7/9、6/9向上取整；比例来自这些实际整数阈值。mania不按该刻度造等级，available为false。上述来源只用于声明式绑定或数值clock，不增加可选脚本的输入名、权限或玩法写入能力。

`score.pacemaker-next*`使用同一匹配最佳成绩和真实满分，不读取作者的目标选项。七档依次是满分的12/18至18/18，各自只进行一次整数向上取整，选择严格高于最佳成绩的第一档；最高饱和为DJ AUTO。缺最佳成绩、零满分、超界最佳成绩或非BMS EX SCORE时不可用，不把缺数据当成0分。只有NEXT标签、available或数值clock的场景也会加载匹配成绩。七档含义取原版官方说明；整数舍入、等值升档是OMS明示策略，原机边界未实测；不据此提供NEXT+、rotate、Ghost或不存在的对手数据。`score.pacemaker-next`可使用现有十个数字的整数图片字体，label需普通文字。

`combo.maximum` 直接读取与 `combo.value` 同一不可变计分快照的 `MaxCombo`，由真实计分器的 `HighestCombo` 提供。例如当前连击从19断到0，最高连击仍显示19；跳转采用重建快照中的最高值，重试归零，晚挂载保留当前快照。它不是谱面理论最大可达连击 `MaximumCombo`，也不是全连资格；不用皮肤脚本累计或取最大值。只显示整数的图片字体可以省略 `format`，仅声明十个数字即可。`combo.active`则从同一快照的当前`Combo>0`派生布尔值，支持普通文字、visible绑定及true/false变体，禁止数字、图片数字文字和动画clock；可用它切换“判定＋连击”和“单独居中判定”，不以判定等级猜零连击。

BMS统计映射是perfect→PG、great→GR、good→GD、meh→BD、miss→PR、ok→空POOR，combo.breaks为真正断连；mania的ok仍是其自身判定档位，不叫空POOR。统计从真实计分器读取，随撤销、回放跳转和重试恢复，不以judgement.hit自行累计。曲目信息在完整快照中携带，重附着和epoch替换保留；BMS难度来自原始DIFFICULTY类别，等级保留PLAYLEVEL字符串，缺失保持空，不冒用星级。MIN/MAX取实际音乐时间线，未使用的BPM定义不计入范围。BMS scroll.speed为当前NHS/FHS/CHS选中数值，mania为自身ScrollSpeed设置；该绑定不输出模式名称，不把不同玩法数值当作同一物理单位。

`gameplay.full-combo` 直接使用计分器完成状态和 `MaximumCombo>0 && Combo==MaximumCombo`，不是已达最高连击 `MaxCombo`，也不以 `combo.breaks==0` 推断；全Miss同样可能没有断连记录。它描述完整连击演出的资格，不等同于玩法自己的结算清灯枚举，不提供“仍可能全连”的预测值，也不接受数值比较表达式。

`object.long-note-mode` 从真实长条对象读取，不从素材名或整局选项猜测；普通mania长条为 `ln`，非长条或没有当前对象为 `none`。`object.long-note-appearance` 只由同一模式和当前 `object.state` 派生：holding加 `-holding`，missed加 `-released`，其它状态使用基础 `ln/cn/hcn`。因此 `-released` 也可表示未命中或断持的missed外观，不是新的一条释放输入事件。HCN重新抓取后回到 `hcn-holding`；对象专用头/身/尾沿同一真实长条身份，seek/reset保留模式并重新取当前对象状态。写在lane或全局信息上时仍读取其范围当前对象，不代表整首谱面的统一模式。

`object.auto-assist` 只读当前绑定物件的真实辅助角色。BMS仅对 `AutoPlay && !CountsForScore` 的物件按皿轨/普通键给出 `scratch`/`note`；完整Autoplay仍为 `none`。普通mania、非note/长条、已退休物件及未绑定精确物件的lane/global节点也为 `none`，不会借用范围内第一颗note的状态。长条头、身、尾沿同一父物件身份，seek/reset和晚挂载保留角色。它不是全局Mod开关，不提供数值绑定或动画clock，也不改变原生Hidden/Tint设置及计分资格。

`gauge.type/maximum/clear-threshold/clear-state` 来自实际BMS血槽处理器，包含GAS切换后的当前类型、规则族阈值与失败状态。`clear` 表示此刻引擎的通关条件成立，不是已结算、全连或另一个玩家成绩。生存槽不能只用 `gauge.value >= gauge.clear-threshold` 判断通关，应直接读 `gauge.clear-state`。血量数值未改变时，真实类型或通关状态变化仍会更新。未提供此投影的玩法为 `type:none`、`clear-state:unavailable`，不会根据mania血量猜通关。重附着、跳转和重试通过完整快照恢复这些事实；作者只能显示它们，不能写回。

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

variants必填id/target/property/source/default/cases，至少一条case，同一variant的key不能重复。property只接受两种：resource仅用于sprite，default为资源id，每case必填id/key/resource；visible用于container、text、sprite，default为JSON布尔，每case必填id/key/value且value也必须为布尔。两种语法不能混用。

准备三个布尔来源与`arrival.first-row`可按true/false选择变体。`layout.group`按当前真实材质目标的稳定组ID选择，case key必须是合法稳定ID；无对应目标取default。组ID属于拓扑事实，不能把画面左右、作者空槽或另一组ID当成本组。此选择不创建新舞台或输入。

```json
{
  "id": "my.grade-image", "target": "ref.icon", "property": "resource",
  "source": "judgement.result", "default": "ref.cool",
  "cases": [ { "id": "my.grade-perfect", "key": "perfect", "resource": "ref.warm" } ]
}
```

不同判定等级需要不同的静态数字字体时，可建立独立横排，用同一个只读等级源切换可见行；字体本身仍不能动态替换：

```json
{
  "id":"my.perfect-row-visible","target":"my.judge-row","property":"visible",
  "source":"judgement.result","default":false,
  "cases":[{"id":"my.perfect-row-case","key":"perfect","value":true}]
}
```

除前述配置、准备布尔、`arrival.first-row`与`layout.group`外，source还允许`layout.ruleset`（bms/mania）、`offset.automatic`（true/false）及 `object.state`、`object.long-note-mode`、`object.long-note-appearance`、`object.auto-assist`、`judgement.result`、`gauge.type`、`gauge.clear-state`、`bga.content-state`，key必须是上表对应字符串；当前值没有列入cases时使用default。长条模式和holding/released换图宜用单个 `object.long-note-appearance` 变体，避免两个变体同时改同一sprite资源。空态先按上一节投影，例如没有判定时也会命中作者的miss分支，不会越过该分支自动使用default。没有任意表达式、范围条件或外部资源路径。

`combo.active`的变体key只接受`true/false`，使用同一Score快照，不按当前判定等级推测连击。`layout.presentation`除既有x/width外可单独选择y，y与x同为有限的-4..4数值；值来自当前实际应用的布局上下文，保存但不适用的选项不参与。其它source的y、height/scale/rotation等几何变体仍不开放，也不能将`layout.presentation`用作数值binding或clock。共享一个template时，每个实际material/Group实例仍有独立判定起点和runtime状态。

templates每项必填id/root，root是完整节点；instances每项必填id/template，并在target与material之间二选一，没有实例properties、参数或循环生成字段：

```json
{ "id": "my.status-copy", "template": "ref.status-template", "target": { "kind": "global" } }
```

同包跨玩法或单双舞台可按已经解析的公开素材声明挂载模板，例如：

```json
{ "id": "my.gauge-stages", "template": "my.gauge-style", "material": { "slot": "hud.gauge", "resource": "bms/gauge" } }
```

仅匹配当前选定包同一内容版本、公共声明中Provide状态的Global、Stage、Group或Lane素材及精确资源基名。模板根slot必须与 `material.slot` 相同，模板根的完整占位target可写global，准备时整棵子树替换为匹配到的实际目标。Global只生成一个全局实例；Stage、Group按实际匹配的舞台或轨道组生成，保留各自作用域及真实稳定group id和逻辑索引；Lane按实际匹配车道各生成，使用真实稳定lane id和逻辑索引，不需为1P/2P或键数硬写一组索引。Group素材实例可用于 `playfield.bar-line` 等已允许Group的slot，不扩大slot本身允许的作用域。子元素默认在根slot区域内排版，局部输入、对象和判定绑定沿该车道/轨道组/舞台事实，全局统计仍来自同一计分器。显式target模板仍保留原有目标矩形规则。

例如为已由本包公共INI声明 `Provide "notes/my-hit"` 的所有击打车道复用一棵模板：

```json
{ "id": "my.hit-lanes", "template": "my.hit-style", "material": { "slot": "effect.hit-explosion", "resource": "notes/my-hit" } }
```

`my.hit-style` 根slot应为 `effect.hit-explosion`。同样可匹配 `playfield.key` 或音符槽等实际Lane素材；原生音符和击打效果仍由实际对象的池化所有者应用模板，不会由实例声明自行制造击打或延长音符。没有匹配时不挂载，仍检查模板语法与资源；不能匹配默认包、旧兼容NoteImage字段或另一内容版本的素材来借用scene，也不支持任意条件表达式。实际展开数量、池化对象和击打效果继续受既有预算限制。

`format: "percent"`将数值乘100后转为整数百分比，适合`gauge.value`；普通字体的准确率和进度保留原有专用文字格式，位图节点则按前文显式format显示。`fixed-2`固定两位小数（如速度2.50），`signed-fixed-2`始终带正负号（如偏移+0.00、-3.25），`integer`将数值舍入为无小数数字；`uppercase`将普通文本转换为大写。格式化后仍执行文字长度限制。`reveal-x`可绑定数值，不会压缩格距；固定外框和暗槽放在clip之外。`GaugeHeight`与独立键区参数一样只读取选定包，缺项使用默认值。

`layout-surface`只选择上述四个已发布的信息矩形，不能填写任意surface ID；只允许hud.text节点或其无slot子树。未声明区域的孩子继承父区域，显式声明可重新选择另一信息区；区域未发布时准备失败，不能自行退回全屏。绑定、动画、状态机和脚本均不能修改它。BMS `BgaInformationHeight`大于0时，由同一布局器为曲名保留BGA上方区域，为判定统计与MIN/BPM/MAX保留下方区域，为Score/HiSpeed保留控制区下方区域；这些是全局区域，14K不应按Stage重复生成整套信息。默认0保留旧布局。

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
| 作者目录打包 | 普通包128MiB、4096普通文件；显式内容目录512MiB、8192文件，物理目录含子目录仍须通过8192条目门；不接受链接 |
| skin.ini / 名称与作者 | 1MiB；各256字符，无控制字符 |
| 清单 / 场景 / JSON深度 | 64KiB / 512KiB / 32 |
| 稳定id / 资源路径 | 128 / 240字符 |
| 资源数 / 单资源 / 总资源 | 普通256、显式目录冻结选中组合384 / 16MiB / 64MiB |
| 单图像素 / 总图像素 / 解码字节 | 16×1024² / 普通64×1024²、显式目录选中组合96×1024² / 普通256MiB、选中组合384MiB |
| 位图字体 / 每字体字形 / 每节点字符 | 10 / 16 / 64 |
| 位图字形图片 / lineHeight / advance | 每边1..512像素，图片高度不超过lineHeight；lineHeight与advance均1..512、有限 |
| 源节点 / 树深 / 每节点孩子 | 2048 / 16 / 128 |
| 每对象属性 / 单节点效果 / 总效果 | 32 / 8 / 512 |
| 动画 / 每条帧 / 总帧 | 512 / 512 / 8192 |
| 全连演出完成等待 | `completionOutro`为0..3500毫秒；缺省0保持原1000毫秒等待 |
| 状态机 / 状态 / 赋值 / 转移 | 128 / 1024 / 4096 / 2048 |
| 绑定 / 变体 / 单变体分支 / 总分支 | 512 / 512 / 32 / 4096 |
| 模板 / 实例 / 展开节点 | 128 / 1024 / 8192 |
| 每帧属性 / 单事件状态属性应用 | 16384 / 8192 |
| 源静态文字总字符 | 65536 |
| 运行节点 / 效果实例 | 32768 / 8192 |
| 效果表面像素 / 字节 | 128×1024² / 512MiB |
| 文字字形 / 普通字形图集像素 / 图集字节 | 8192 / 64×1024² / 256MiB；位图像素只计普通scene纹理 |
| 单动态文字字形预留 / 每帧创建 | 位图64；普通字体song元数据256、判定计数与断连10、其他384 / 256 |
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


文字节点可静态声明 `text-overflow: ellipsis`，按其作者容器宽度显示省略号，绑定字符串仍保留完整值；缺省维持原行为。该属性不能由轨道、绑定或状态修改。
