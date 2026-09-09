# 从完整模板制作自己的皮肤

制作分成两个常用方式。改色和复现官方作品时，编辑 `author.json`，然后运行 `generate`。自己画素材、调整舞台或编写演出时，直接编辑 `bms/*.png`、`mania/*.png`、`scene/*.png`、`skin.ini`、`gameplay-skin.scene.json` 和可选 `gameplay-skin.script`，随后运行 `check`、`pack`；此时不要再运行 `generate` 覆盖手工修改。

所有文件相对于作品目录。图片名在 `skin.ini` 中不写扩展名，在场景清单中必须写完整 `.png` 路径。短键、长条头身尾支持 `名字-0.png`、`名字-1.png` 等从 0 连续编号的动画，固定每秒 60 帧。静态同名 PNG 保留可读的封面帧；普通作者同样可使用这条路径。

## 先做一个能完整游玩的版本

1. 从 `sources/oms-simple` 复制新作品，修改名字与作者。它已经含 BMS 全支持键数、mania 每舞台 1～10 键及双舞台组合；不必手工从空文件填写所有覆盖目标。
2. 优先替换 `bms/note-*`、`head-*`、`body-*`、`tail-*` 和 `mania/` 同组文件。头身尾保持一致色彩；长条头和普通音符保留能看清的横向边缘。
3. 调整 `lane`、`divider`、`target` 与 `frame`，保持音符与背景对比。`cover` 必须不透明；用于遮挡谱面的必要区域不能用透明图伪装完成。
4. 保留判定位置、长条、地雷、分道和必要信息。关闭可选部分写明确的 `Suppress`；空字符串或丢失图片并不表示关闭。
5. 检查、打包、普通导入后，按本文末尾清单实际游玩。使用自己的谱面或验收包内的观察谱，先慢速辨认，再观察密集下落。

## 公共设置

完整 28 项资源目录见 [CATALOG.md](CATALOG.md)，由生产目录生成。目标必须写 `ruleset`、`keymode`、`stage-mode`；按范围再写组、轨道及明确索引。不要删除索引来“适配所有键数”，不要从图片顺序猜转盘位置。完整模板已展开所有支持的目标。

```ini
[GameplaySkin.Common:1]
Target: Lane ruleset=bms keymode=5k stage-mode=single group=bms.group.deck-1 lane=bms.lane.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=1 group-local-visual=1
object.note: resource Provide "bms/note-white"
effect.key-flash: resource Suppress
```

`Provide` 提供自己的素材；`Inherit` 沿下一层补齐；`Suppress` 仅关闭目录允许的可选部分。必要与推荐部分不能关闭。资源声明的精确规则优先级为玩法、键数、舞台模式、轨道/组/舞台/全局范围，再比较同等级后行；错误的精确声明不会回头拼本包更宽的声明。

BMS `[Bms]` 的 `Keymode` 支持 `5K`、`7K`、`9K`、`9K_PMS`、`14K`。9 键旧字段 `NoteImage0`～`8` 对应公开轨道 `key-1`～`9`。5/7 键 `S` 是转盘，14 键含 `S`、`S2`。mania `[Mania] Keys:` 保留传统字段。mania 的 `KeyImage` 与 `KeyImageD` 分别提供松开和按下图片；模板的 `playfield.key` 显式 `Inherit` 到同包这些普通字段，避免单张公共图把按下态覆盖。关闭可选按键闪光仍保留按下图片。两种玩法可使用不同目录、颜色与布局；[General] 的覆盖文字不代替真正的素材设置。

公开 BMS 布局字段：`PlayfieldWidth` 与 `PlayfieldHeight` 是屏幕比例，普通键/转盘宽度和间距是相对权重，`LongNoteBodyWidth` 是相对车道宽度。判定线相对滚动的时序关系不交给作者修改。mania 使用 `ColumnWidth`、`ColumnSpacing`、`HitPosition` 等现有兼容设置。布局必须留出双舞台、必要文字、背景视频区域和不同屏幕比例的空间；不重做视频播放器。

## 声明式演出

`gameplay-skin.json` 固定使用 `oms-gameplay-skin-manifest.v1`，声明固定场景文件、`oms-gameplay-skin-scene.v1`、`oms-gameplay-skin-event.v1` 以及图片资源。资源路径只在本包内有效。

场景根包含 `root`、`tracks`、`stateMachines`、`bindings`、`variants`、`templates`、`instances`。节点必填 `id/type/target/properties/effects/children`；可声明 `slot/resource/blend`。节点类型为 `sprite/container/text/mask/clip`。根若只分派不同资源槽，使用无资源、无动画、无属性、`blend: inherit` 的容器。一个可见分组必须属于自己显式 `Provide` 的公开资源槽。

| 面 | 公开内容 |
| --- | --- |
| 坐标 | `x/y/width/height/scale-x/scale-y/rotation/z`；相对于引擎给定的区域 |
| 显示 | `opacity/visible/colour/anchor/origin/fill-mode` |
| 文字与裁剪 | `font-size/text/alignment/mask-mode/clip-mode/corner-radius` |
| 目标 | `global`；带 id/index 的 `stage/group/lane`；`hud`；`bga` |
| 混合 | `inherit/alpha/additive/multiply/screen` |
| 轨迹 | `frame/tween`，`step/linear/in/out/in-out`，毫秒 keyframe，显式 loop |
| 状态变化 | `gameplay.attach/loaded/start/pause/complete/fail`、`input.key.down/up`、`object.spawn/state`、`judgement.hit`、`timing.stop`、`bga.state` |
| 只读绑定 | `layout.stage/group/lane`、`input.pressed`、`object.state`、`judgement.result/offset`、`score.value/accuracy`、`combo.value`、`gauge.value`、`timing.beat/measure/bpm/progress`、`bga.content-state` |
| 图片变体 | 根据 `object.state`、`judgement.result` 或 `bga.content-state` 从清单内选择资源 |

当前场景精确轨道目标不支持“目标不存在则忽略”。同一包跨玩法时用两种玩法都适用的共通目标演出，玩法专属外观使用公开设置中的选择条件与素材。星轨是完整的实际范例：普通核心部件由各自公开素材提供，`hud.text` 全局槽拥有包含分数/准确率/连击/速度/判定/能量与谱面进度的完整信息控制台；控台使用只读绑定与生命周期状态机，不需要额外授权。成组装饰使用另一个共同全局槽，脚本只改变这组获准节点。不要把 mania 不适用的地雷或 BGA 场景节点强塞进共同场景。

`score.accuracy` 与 `timing.progress` 的数值都是 0～1；绑定文字时分别显示两位小数准确率百分数和整数进度百分数。进度来自真实谱面可玩起止与游玩时钟，首个物件前为零，末物件后为一，零时长谱为零；暂停不推进，重试或回跳使用新位置。星轨控制台下方的金色细条表示谱面进度，青色细条表示能量。它们只读取游戏状态，不需要组合脚本授权。

允许的效果类型为 `blur/glow/outline/shadow`，仅接受各类型的公开数值与颜色参数；节点最多 8 个效果，全图最多 512 个，仍受准备后的总实例与表面积预算约束。星轨直接使用有透明度的几何图片，避免把多层模糊变成基本显示的前置条件。

`gameplay.resume` 不是声明式状态机事件；恢复以完整当前状态重建。游戏负责所有输入、判定、谱面对象、得分、时钟和背景视频内容，演出不能改写这些事实。

## 组合脚本与玩家选择

星轨的 `gameplay-skin.script` 是完整可编辑例子。它保存最近八次实际判定间隔，用近期击打强度和当前能量共同影响两侧棱镜、圆环透明度与分段柱。旋转轨迹属于普通场景，所以拒绝额外权限后仍保留协调静态/声明式外观，基本游玩不依赖脚本。

文件首行 `oms-script 1`。可请求 `gameplay.snapshot.read`、`gameplay.events.read`、`scene.numeric.write`、`math.random.read`。`required` 缺少授权时脚本不激活；`optional` 通过 `granted` 检查后选择处理；`deny` 不可再授权。写目标仅允许已声明节点的 `alpha/x/y/rotation/scale-x/scale-y`。

`state` 为固定数值，`heap` 为固定数组。支持赋值、四则运算、min/max/lt/eq/clamp、只读输入、已获准随机、节点写入、label/jump/when、load/store、granted、halt。输入含 `time/delta/combo/gauge/beat/bpm/running/event-kind/event-value`。`event-kind=-1` 是固定 60 Hz 游玩时钟更新，40 是判定，42 连击、43 能量；暂停不推进，重试和回跳重建历史。

授权跟随当前皮肤记录与全部内容。修改文件重新导入或重新载入后可能需要重新授权；这不是制作错误。网络、任意文件读写、系统程序、反射和玩法改写永久禁止。设置中可以查询、拒绝和撤销；暂停中撤销同样生效，撤销不是重新载入入口。

## 预算与定位

普通击打声音位于作品根目录，使用 `normal-`、`soft-`、`drum-` 前缀和 `hitnormal`、`hitclap`、`hitfinish`、`hitwhistle` 文件名，格式为 WAV。带滑动音标志的长条还读取各组的 `sliderslide`、`sliderwhistle`；普通长条是否播放这些声音仍由谱面决定，皮肤不改规则。两款和演练作品均保留原创声音，制作配方可重新合成；也可用音频编辑器替换同名文件。持续声音的首尾应自然衔接，击打声音应保留清楚但短促的起音，最后在游戏内听实际效果。

| 内容 | 上限 |
| --- | --- |
| 目录皮肤设置 / 名称、作者 | `skin.ini` 1 MiB；名称与作者各 256 字符，不含控制字符 |
| 清单 / 场景 JSON | 64 KiB / 512 KiB；JSON 深度 32 |
| 资源 | 256 个；单个 16 MiB，合计 64 MiB；单张 16M 像素，合计 64M 像素 |
| 节点 | 2048 个、深度 16、每节点 128 子项；模板展开 8192 个 |
| 动画 | 512 条轨迹，每条 512 帧，合计 8192 帧 |
| 状态机 | 128 个，1024 个状态，4096 赋值，2048 条转移 |
| 绑定 / 图片变体 | 各 512；每变体最多 32 分支 |
| 模板 / 实例 | 128 / 1024 |
| 脚本源码 / 字节码 | 64 KiB / 256 KiB |
| 脚本状态 / 数组 | 128 / 512 个数值 |
| 脚本请求 / 写目标 | 32 / 128 |
| 单回调 / 每画面指令 | 4096 / 16384 |
| 每画面脚本写入 / 实际克隆属性应用 | 256 / 16384 |

`check` 使用游戏的目录皮肤准入、公开设置解析、场景解析和脚本编译器，校验图片解码与引用。它是制作前检查，不假称具备当前谱面、屏幕、音符池和实际输入信息；导入后游戏还会按当前游玩场景严格检查完整准备。错误先按 `skin.ini:行号` 或 `OMS-SKIN-SCENE-...`、`OMS-SKIN-SCRIPT-...` 定位；JSON 用文本编辑器检查未知字段、重复 id、拼写、引用和节点范围。设置中重新载入失败时旧作品仍可用，修正源文件后退出游玩/预览再次重载。

## 每次作品更新后实际核对

两种玩法各选一张普通谱和长条谱；BMS 再观察转盘侧变换、9 键两种模式、14 键双台与视频区域；mania 通过原生观察谱验证 1～10K 单舞台与 12/14/16/18K 双舞台；当前原生谱面上限是 18K，不能把 20K 文件按读取后的 18K 当作 20K 签收。模板中其余内部支持的双舞台组合由完整自动使用验证覆盖，不要求作者用 DS 按钮猜造入口。切换不同宽高比和缩放，确认最窄键道仍分得清头身尾、地雷和判定线，文字不被遮挡。星轨分别授权、拒绝、撤销，暂停和重试，确认组合演出可选而必要信息始终清楚。最后在设置导出，再普通导入导出的包核对。这些观察不能由制作工具代签。
