# BeatorajaSkinCapture

在独立测试目录运行官方 beatoraja 0.8.9，生成合成 BMS 与原生 Replay，读取真实 OpenGL framebuffer，验证实际加载的皮肤身份、选项、判定、Gauge、FC、失败与自然结算。工具不使用 computer use，不修改安装 JAR，不注入 score、Gauge 或皮肤计时器。

## 运行

从 OMS 仓库使用 PowerShell 7：

```powershell
. .\UseDevelopmentStorage.ps1
.\tools\BeatorajaSkinCapture\Invoke-BeatorajaSkinCapture.ps1 `
    -Skin 'D:\beatoraja\skin\default\play\play7.luaskin' `
    -Mode sp1 -Scenario success
```

`-Java` 默认寻找 `D:\beatoraja\jre\bin\java.exe`，其次寻找原安装目录 `D:\beatoraja0.8.8-jre-win64`。`-ResourceRoot` 默认使用该 JRE 所在安装根，仅引用其中的 `font/` 和 `skin/default/`；不打开安装配置、真实 player 或 song DB。可显式指定其它完整官方资源目录。

`-Jar` 默认使用该资源根的 `beatoraja.jar`，`-NativeRoot` 默认取 JAR 同目录的 `natives/`。编译使用现有 JDK 17.0.20.8；运行使用带完整 JavaFX 的 JRE 21。通过参数可指定其它路径；不自动下载或升级依赖。

| 参数 | 行为 |
| --- | --- |
| `-Mode sp1/sp2/dp` | 选择单侧或双侧布局；SP2仍是单玩家的右侧呈现。默认 SP 侧别选项为 `Play Side=920/921`，要求加载后的选项一致。 |
| `-KeyMode 5k/7k/14k` | 省略时沿原行为：SP推断7k、DP推断14k。5k使用独立5KEYS/type1/BEAT_5K与6条轨道，7k为type0/8条，14k为type2/16条；5k/7k仅允许sp1/sp2，14k仅允许dp。实际模型、皮肤type、lane和stage数量均须匹配。 |
| `-SideOptionName` | 改变侧别选项名称；其它 op 值用同名 `-SkinOption` 覆盖。独立 SP 文件没有该选项时显式传空字符串，此时侧别由指定文件负责，工具不证明其几何是左侧或右侧。 |
| `-SkinOption @('名称=op', ...)` | 写入临时玩家的 skin properties，并核对实际 header 中的选中值。未知或被忽略的 option 会失败。 |
| `-SkinFile @('名称=文件名', ...)` | 传入原生文件选择项；其实际美术仍需核对 PNG。 |
| `-Scenario autoplay` | 官方 AUTOPLAY 自然完成，要求全部 notes、满 EX SCORE、FC 和返回选曲。 |
| `-Scenario success` | 由谱面实际微秒时间和 LaneProperty 生成原生 Replay，经引擎判定后要求全部 notes、满 EX SCORE、FC 和 MusicResult。 |
| `-Scenario failure` | Replay 只击中第一颗 note，其余自然 miss；固定 HARD / 不自动切 Gauge，要求 Gauge 归零、Failed timer 和失败 MusicResult。 |
| `-Scenario hcn-recovery` | 必须配 HCN；首个键长条在头后550ms松开、1100ms重按，经真实输入产生active→damage→active，要求自然MusicResult、掉Gauge及实际失误。 |
| `-Scenario timing` | 开头每侧所有轨道各一颗音符交替提前/延后50ms（5K为6颗，7K/14K每侧8颗），其余按原时刻输入；要求真实每个Judge region都出现FAST/SLOW、非满EX的FC与MusicResult。保存首次两方向0/50/100ms取景及原生判定时差，不注入判定。 |
| `-LongNoteMode LN/CN/HCN` | 为合成谱面的未指定类型长条设置原生 LN mode。包含普通键和 Scratch 长条。 |
| `-Gauge AssistEasy/Easy/Normal/Hard/ExHard/Hazard` | 真实单曲Gauge，默认Hard、关闭自动换Gauge，并核对实际加载类型；failure及hcn-recovery固定要求Hard。Course Gauge不由这个参数冒充。 |
| `-ShowReadouts` | 在真实Replay的Play 4500–5250ms经公开输入接口按住START并松开，要求原生270读数门实际开/关；保存三张按住与一张退出帧。Replay不执行普通PLAY的按键调速。谱面结束后START会直接退出，故不在结束尾段按住来冒充读数。 |
| `-Resolution HD/FULLHD/WQHD/ULTRAHD`、`-HiSpeed` | 原生显示分辨率和 Hi-Speed；默认 HD、2.0。 |
| `-LaneCover`、`-Lift`、`-Hidden` | 可选原生比例 0–1，只写独立测试玩家的 PlayConfig；大于 0 启用该项，0 关闭。不传时保持工具原有关闭设置；支持组合，用于检查遮挡与判定线。 |
| `-TimeoutSeconds` | 内部超时 20–180 秒，默认 60；外层再留 20 秒退出时间。超时只结束本次启动的进程树，记失败。 |
| `-OutputRoot` | 只能在本仓 `.dev-cache/temp/beatoraja-capture-dev/` 或 `artifacts/beatoraja-capture/` 内；拒绝 reparse 祖先，每次新建唯一子目录。 |

## 隔离与回执

每次工作目录独立保存 `config_sys.json`、合成 `player/capture/`、song/review DB、`folder/`、`random/`、table、日志、JVM 解包目录与编译 class。源皮肤和安装资源通过绝对路径读取；不复制全程序或完整 skin 库。皮肤图像缓存关闭，避免 `.cim` 写回资产旁。

IR 配置为空，IPFS、Discord、请求服务和自动扫描关闭。官方选曲仍会无条件启动 BMS Search；宿主显式拒绝 Java HTTP/HTTPS 连接，回执记录被阻止的请求数。预期的 `Network disabled by skin capture host` 日志不算皮肤警告，其余 WARNING/SEVERE 使 `diagnosticsClean=false`，本轮判失败。SLF4J 的无绑定提示和 parser 的 INFO 消息保留在 stderr；不把未登记到 Logger 的输出概括成“无警告”。本工具不是不可信 Lua 的文件系统 sandbox，只用于本任务审查过的皮肤。

产物：

- `capture-workspace.json`：官方 JAR、工具源文件、皮肤入口及存在时的组件包收据SHA256、runtime、只读资源位置和用例；`mode`记录布局，`keyMode`记录键数。结束再核入口与收据未变。完整资产闭包仍由`beatoraja_verify.py package`检查。
- `receipt.json`：目标与实际 skin 身份、实际 option、自然状态转移、看到的 timer、判定/Gauge、PNG 的尺寸和 hash、警告与 `normalDispose`。适配器准备期10020有效时，追加0/100/350/500/1000ms、最后1500/1000/500ms及首次PLAY取景；记录实际年龄、源playstart及四个准备时钟，不写计时器，也不把目标检查点当作精确采样帧。
- `graphics`记录实际OpenGL vendor、renderer与版本；140节拍时钟纳入只读遥测。硬件名不代表已测显存峰值，历史未记录这些字段的用例不能补推。
- `frames/*.png`：真实 backbuffer，在状态变化、Play每秒、Ready/Failed/FC/曲终及Result取帧；Ready/Failed增加1000/2000/3000ms观察，实际仍开放该timer时才捕获。另围绕51（爆炸）/71（长按）/101（按键）/10101和10111（两侧长条持续）首次触发取目标0/50/100/250ms，最多新增20张；关闭或重触发不会重开这组取景。`firstEffect`记录真实render-clock age与lateness，不把延迟帧冒充精确相位。HCN恢复场景按真实251/271状态顺序另外取每阶段0/50/200ms。单曲AUTOPLAY返回选曲时，原生`changeState`立即退出，因此终态只取首帧。
- `engine.log`、`stdout.log`、`stderr.log`：原始日志；`host-receipt.json` 保存已退出的 PID、退出码、超时与结束时间。

官方 LWJGL 2 后端在正常资源释放后执行 `System.exit(-1)`；脚本仅在正常 dispose、身份与行为断言全部通过时接受该退出码，不把所有 `-1` 当成功。宿主调用 `main.exit()`，通过原生生命周期释放资源；不依赖强杀结束正常用例。

挡板参数使用 0.8.9 `PlayConfig.setLanecover/setLift/setHidden(float)` 及各自 enable setter，在生成 Replay 前设置，Replay 同时保存该配置。回执的 `readingRequested`、`readingConfigured`、`readingLoaded` 及每帧 `reading` 记录请求与真正 LaneRenderer 配置；若宿主忽略指定比例或启用状态，用例失败。这只验证固定配置加载，不等于实键调整操作或全部挡板组合验收。

截图读回发生在 `MainController.render()` 之后的 GL 线程。同一 render 的多个检查点只读回、转换和编码一次画面，各自仍保存遥测与 PNG；`sharedFramebufferWith` 指向共享画面的首个检查点。PNG采用较快的无损DEFLATE，保留原不透明像素；编码/写入和 hash 使用单后台线程、最多 4 个独立画面的队列，队列满时由调用线程完成以限制内存。退出前先提交当帧再等待写入完成，写入失败或超时使回执失败，不留下未检查的后台工作。原生判定线程独立运行，状态采样不保证与每个像素原子一致；精确动画相位须结合实际采样年龄继续分析。本工具不能验硬件延迟、持续帧率或手感。

`performance` 单独统计 `MainController.render()` 调用的墙钟耗时：`playBeforeFc` 是 render 后仍为 BMSPlayer/STATE_PLAY 且 timer 48 未开启，`afterFc` 是 BMSPlayer 的 timer 48 已开启（包括其曲终/退场状态）。两组均给 count、median、p95、max 毫秒；p95 使用 nearest-rank，空组为 null。PNG 编码/写入和 framebuffer 读回按独立画面计数，含队列等待及 hash 的完成耗时按检查点计数；每张 `captureTimingMs` 还保存 queueWait，共享画面的编码耗时包含该批全部文件写入。这些操作不计入 render 调用区间，但仍可能改变后续调度/GC；调用耗时也可能包含引擎内部 GPU 等待，不能表述为纯 CPU 时间或硬件帧率验收。

## 已核接口与限制

实现针对 [官方 0.8.9 发布](https://github.com/exch-bms2/beatoraja/releases/tag/0.8.9)，研究源码固定标签提交 `83574048ba489082eb46df7fe9806b464bf1f526`。发行 JAR 的默认皮肤路径指向 ModernChic，与 tag 中 `skin/default/` 不同，因此 QA 显式指定配套默认皮肤。源码 ZIP 的 default skin 缺少旁置 `VL-Gothic-Regular.ttf`，直接使用会得到字体警告；应引用安装中的完整资源，不能把带缺字警告的截图签收。

5KEYS是原生独立槽位，不能把7KEYS的Lua入口直接填入来代签。官方只为LR2皮肤提供7/14到5/10的菜单兼容；本工具仍按实际type严格核对。已读的安装默认`skin/default/play5.json`和新作品`play5.luaskin`均声明`Play Side=920/921`。5K合成普通音符只写11–16通道，长条沿51/56通道，原生Replay根据BEAT_5K的LaneProperty生成，不把7K的第六键当作Scratch。

- Lua header/body 各执行一次；运行时用 `require('main_state')`。表形状见 [JsonSkin](https://github.com/exch-bms2/beatoraja/blob/0.8.9/src/bms/player/beatoraja/skin/json/JsonSkin.java)，运行时 API 见 [官方 Lua 文档](https://github.com/exch-bms2/beatoraja/blob/0.8.9/docs/lua-main-state-api.md)。几何关键帧为整数；Lua callback 仅支持 property 类型字段。
- 原生CustomTimer由皮肤每个render缓存一次。本工具只观察10000与10010–10015首次真实起点并保存0/100/250ms帧，不写这些时钟；FC另取1000/2000/3000/4000ms，检查完整原片后段。回执区分目标取景时间与实际年龄。
- 图片裁切、格动画、矩形 clip、层序、旋转与加算/乘算可直接表达。公开格式没有任意纹理遮罩或自由 UV 网格；`blend=3` 的减算实现需专门实测，不能据字段存在签收。
- 新长条字段使用 `lnbodyActive`、`hcnbodyActive/Reactive/Miss`；旧 `lnactive/hcnactive/hcndamage/hcnreactive` 的对应不直观。Gauge nodes 使用 4/8/12/36 个。
- `note.id` 必须进入 destination；`close/loadend/playstart/judgetimer/finishmargin` 在该分支设置。Ready/Play/Failed/FC/EndOfNote/MusicEnd timer 分别为 40/41/3/48/143/908，destination 时间为毫秒，Lua timer 与 Replay 为微秒。
- AUTOPLAY 不验 Result；REPLAY 可以验原生 Result，但不注册正常成绩。全程无输入的 PLAY 会因没有 PG/GR/GD/BD 而返回选曲，所以失败用例先真实命中一颗。生成 Replay 使用真实 `TimeLine.getMicroTime()` 与 `LaneProperty`；不使用当前单位不一致的 `KeyInputLog.createAutoplayLog`。
- `success=true` 只表示该合成用例和工具退出门通过，不代表作品美术、其它选项/分辨率、BGA 视频、重开、物理输入、PB/Target 数据或真实玩家保存已经验收。不得从官方默认皮肤的工具自测签收 Standard 或 Lightning。

## 工具自测（2026-11-07）

已使用实际 `D:\beatoraja\beatoraja.jar`、同目录 natives 与 JRE 21.0.1、官方安装的 default skin 完成以下用例。留存证据在本机 `artifacts/beatoraja-capture/selftest-20261107/`；各回执保留原执行目录，复制到此的证据不改写历史路径。

| 用例 | 实际结果 |
| --- | --- |
| SP1 / LN / success / Full HD | 42 notes、84 EX、FC、MusicResult；26 张 1920×1080 PNG。 |
| SP2 / LN / failure / HD | 实际选项 921；先命中一颗后 HARD 归零、Failed、MusicResult；16 张 PNG。 |
| DP / HCN / success / HD | 88 notes、176 EX、FC、MusicResult；两侧判定及 HCN active timers；26 张 PNG。 |
| SP2 / CN / autoplay / HD | 实际选项 921；44 notes、88 EX、FC、自然返回选曲；24 张 PNG。 |
| 未声明 option | `CaptureUnsupported=99999` 被明确拒绝，`success=false` 且正常 dispose。 |

正例均满足诊断门、原生 dispose 与进程退出，无残留用例 PID；唯一预期的 SEVERE 为宿主拒绝 BMS Search。已目视检查 SP1 和 DP 真实游戏帧，不把默认皮肤自测签收为两款新作品。官方 JAR parser 会无条件追加一条 `#PLAYER 1`/2P 的 INFO，已由其字节码确认；合成 DP 用其接受的 `#PLAYER 2`，并强制核对实际模型为 `BEAT_14K`。

同日补测 `-LaneCover 0.3 -Lift 0.1 -Hidden 0.05` 的官方 default DP / LN / success：真正 LaneRenderer 配置三项均启用且比例一致，84 notes、168 EX、FC、MusicResult，26 帧，诊断门通过、正常 dispose 且 PID 已退出。证据为 `.dev-cache/temp/beatoraja-capture-dev/reading-selftest-20261107/20261107-070355-dp-success-9b1eceae/`。此项只证明工具固定比例配置加载和该合成通关，不签收新作品挡板美术或实键调整。

首次效果取景与耗时诊断补测使用官方 default SP1 / LN / success。证据 `.dev-cache/temp/beatoraja-capture-dev/effect-performance-selftest-20261107/20261107-072542-sp1-success-fcc730c9/`：39 帧中恰好 12 张首次效果帧，51/101 的目标 50/100 毫秒实际为 56.36/105.48 毫秒，71 的对应帧为 53.59/103.60 毫秒；39 张 PNG 的 hash、写入完成数量、线程 join、诊断门和进程退出均通过。该次普通 Play / FC 后 render 样本数为 1078/723，median 为 0.1072/0.0870 毫秒、p95 为 0.2524/0.1767 毫秒，仅作为工具能分别记录两组调用耗时的证据，不能用于签收新作品性能。更早同目录的同步写入试跑暴露了 50 毫秒采样延后到约 106 毫秒的问题，由本轮有界后台编码修正，保留原回执作诊断历史。

HCN恢复自测使用同一官方安装、SP1/default skin/HARD，真实44 notes、EX86、BP1、最低Gauge85，active→damage→active三阶段顺序成立、自然MusicResult、50张PNG完整、正常dispose。证据在`artifacts/beatoraja-capture/hcn-recovery-selftest/`；谱面与Replay仅在QA目录，未改原生判定或计时器。

Timing自测使用官方default DP/HCN，开头两侧各8颗交替提前/延后50ms；两个原生Judge region均观察到FAST/SLOW，真实非满EX FC和MusicResult，56张PNG完整且正常dispose。证据在`artifacts/beatoraja-capture/timing-selftest/`；`firstTimingJudgeOriginsUs`与每帧`timingFeedback`记录资格、符号和首个判定年龄，不把目标50ms截图当作精确50ms。后续增加的单曲Gauge选择参数已编译，尚待非Hard运行验证。

尚未覆盖：最终作品包矩阵、重开、PB/Target历史数据、其它挡板组合与交互调整、视频BGA、物理设备与正常PLAY存分。P1-A的产品进度由主线统一维护。
