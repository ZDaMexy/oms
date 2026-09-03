# OMS gameplay 皮肤制作手册（当前 `.osk/skin.ini` + Skin V1 路线）

> **读者**：想给 OMS BMS 做皮肤的制作者，以及在仓库内实现皮肤系统的开发者。
>
> **范围**：**仅游玩界面**。osu!lazer 已不再支持选歌/结果等非游玩界面皮肤，OMS 跟随这一边界——本文不涉及选歌页、结果页、菜单皮肤。
>
> **本文是什么（派生文档）**：面向皮肤制作者的当前能力与 Skin V1 开发视图。**权威契约不在本文**——共享/分离、ini、scene/event/script、fallback、layout 与安全约束冻结在 [P1-A 技术约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)，分期在 [P1-A `SV1-*` 计划](../subline/P1-A/DEVELOPMENT_PLAN.md)。本文只是制作者视图；冲突时以 P1-A 四件套为准。
>
> **当前作者能力（2026-09-03）**：选中的用户包可来自已导入 `.osk`、启动发现的 `chartskin/<包目录>/`，或 Folder Skin Workspace 注册的只读 external 目录。三源共享一个版本化public gameplay-skin catalog、tokenizer/codec、`Provide/Inherit/Suppress` resolver与exact package+layout+material+scene publication。C5 已接通 `gameplay-skin.json` / `gameplay-skin.scene.json` 的 v1 declarative scene（Sprite/Container/Text/Mask/Clip、allowlisted effect、frame/tween/state/binding/template）以及只读 event Snapshot/Reset；BMS 28 项均有production route，Mania 23 项可用，`object.mine`、`playfield.turntable`、`playfield.laser`、`bga.viewport`、`bga.frame`为版本化NotApplicable。BMS/mania真实host覆盖所有适用global/stage/group/lane、Note/LN/Hold、HUD、judgement/gauge/combo、effect、BGA装饰与pool，native `[Bms] NoteImage*`静态图及固定60 FPS连续编号帧兼容保持可用。所有consumer只读同一immutable publication；失败保留exact旧画面，成功诊断使用稳定脱敏code。Settings → Skin 的 `Reload current skin`仍是三源唯一手动reload，gameplay/preview在读取来源前拒绝，不存在watcher。权威状态为`5/7 closed，C6 active`；C6 sandbox与最终整包reload、C7 canonical双包和Authoring Kit仍未开放，程序化`OmsSkin`仍是迁移链底。新beatmap-local作者格式继续不可达，既有只读谱面视觉兼容不受影响。详见[公共目录](GAMEPLAY_SKIN_PUBLIC_CATALOG_V1.md)、[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)、[C5完成交接](SKIN_SYSTEM_C5_SCENE_EVENT_COMPLETION_HANDOFF_20260903.md)与[技术约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)。
>
> **受管目录删除边界**：current目标先发布fallback并等待旧revision detach；该阶段失败会恢复或保持原皮肤，且尚未创建journal或触碰目录。进入C1 journal/首个物理步骤后才只由durable recovery收口。Windows目录handle不锁住namespace，final preflight后的竞态新增不会被删除，但可能在部分目标节点已经清理后令操作冻结；这不是all-or-nothing全树删除。

---

## 目录

1. [这套皮肤能做什么 / 不做什么](#1-这套皮肤能做什么--不做什么)
2. [皮肤包结构](#2-皮肤包结构)
3. [`skin.ini` 总览与通用约定](#3-skinini-总览与通用约定)
4. [车道与几何](#4-车道与几何)
5. [静态素材族（mania 对齐）](#5-静态素材族mania-对齐)
6. [BMS 扩展族与事件挂点](#6-bms-扩展族与事件挂点)
7. [必备 / 推荐 / 可选与三态解析](#7-必备--推荐--可选与三态解析)
8. [三个作者面与布局编辑器边界](#8-三个作者面与布局编辑器边界)
9. [`oms-simple`、`oms-complex` 与最终 fallback](#9-oms-simpleoms-complex-与最终-fallback)
10. [制作流程与 V1 验收](#10-制作流程与-v1-验收)
11. [Skin Authoring Kit 是什么](#11-skin-authoring-kit-是什么)
- [当前 C5 scene/event runtime](#当前-c5-sceneevent-runtime)
- [附录 A：游玩元素全集速查（创作者上限）](#附录-a游玩元素全集速查创作者上限)
- [附录 B：必备元素清单](#附录-b必备元素清单)
- [附录 C：`skin.ini` 字段全表](#附录-cskinini-字段全表)
- [附录 D：受信任代码型 provider](#附录-d进阶受信任代码型-provider开发扩展)

---

## 当前 C5 scene/event runtime

C5 的作者文件是 package 内固定的 `gameplay-skin.json`（manifest）与 `gameplay-skin.scene.json`（scene），分别使用 `oms-gameplay-skin-manifest.v1` 与 `oms-gameplay-skin-scene.v1`。它们必须和同次 `.osk`/managed/external capture 一起进入 background prepare；作者不需要编译 DLL，但也不能让 scene 读取任意文件、网络、脚本表达式或 framework 父树。相对资源路径只允许 package 内 canonical 路径，重复、未知字段/节点/effect/event、非法类型/target/index、坏 UTF-8 或预算超限会 fail-closed 并保留上一版画面。

允许的节点是 Sprite、Container、Text、Mask、Clip；允许的动画是 frame、tween/track、state machine 与只读 property binding，blend/effect 只能使用引擎 allowlist preset。节点、资源、track/keyframe、template/instance、effect 与 binding 都有 stable ID 和有界预算。z-order、anchor/origin、size/scale、clip/mask、DPI/safe-area、stage/group/lane/HUD/BGA target 由 C3 exact layout 决定，scene 只能装饰，不能改判定、输入、对象、计分、时钟或 BGA 内容/timeline。

引擎从真实 BMS/mania/core gameplay state 发布 `oms-gameplay-skin-event.v1` 只读 envelope，包含 epoch、sequence、gameplay/layout/material/scene revision、gameplay time、LaneId/GroupId、kind 与 immutable payload。bounded stream 的初始 Snapshot/Reset 支持 late attach、reload、retry、seek、rewind 与旧 epoch 隔离；`GameplayResumed` 可以出现在 engine envelope，但 scene 状态机不接受 `gameplay.resume`，因为 Snapshot 已能重建 Running 状态。作者不会获得事件生产、判定或资源选择 authority。

当前 runtime capability 是逐 slot 的版本化决定：BMS profile 对 catalog 28 项全部提供 route（9K 的 Turntable/Laser 按适用性排除）；Mania profile 对28项逐项列出23项 Supported 与五个 NotApplicable（`object.mine`、`playfield.turntable`、`playfield.laser`、`bga.viewport`、`bga.frame`）。Note/LN/Hold、lane、HUD、effect 与 hit explosion 使用固定 pool；manifest/scene/resource/node/effect/text/event queue 等预算在 prepare/runtime 执行，update thread 不做 I/O、图片解码或模板展开。C6 才加入 sandbox/script 与最终整包 reload，C7 才交付 canonical 双包/Authoring Kit；当前仍保留程序化 `OmsSkin` 迁移链底。

---

## 1. 这套皮肤能做什么 / 不做什么

**V1 目标能做**：stage、lane、note/LN、判定位置、judgement、gauge、combo、lane cover、BGA frame，以及 turntable、keyflash、hit lighting、hold light、ghost/TD、bpm/progress 等；C5 已把其中适用项接入真实 scene/event host。当前可用范围仍以页首能力块和 P1-A STATUS 为准；元素分层见 [附录 A](#附录-a游玩元素全集速查创作者上限)。

**V1 作者面分层**：

- `skin.ini` compatibility：mania/BMS 共同素材、颜色和有限参数；
- declarative scene/animation：稳定 node type、template、binding、variant、tween/state-machine；
- optional sandbox script：只读事件驱动的复杂组合逻辑；
- lazer 布局编辑器只继续管理既有通用 HUD，不作为新 scene 文件格式。

**osu 社区对齐**：OMS 延续官方描述的社区工作流——皮肤以 `.osk` 分享、打开或拖入导入，解包后是根含 `skin.ini` 的普通目录；mania 的素材命名、`[Mania] Keys:` 分桶和 `name-{n}` 动画序列保持兼容。[osu! 官方 Skin 页面](https://osu.ppy.sh/wiki/en/Skin)说明了 `.osk`/文件夹导入方式，[osu!mania skinning](https://osu.ppy.sh/wiki/en/Skinning/osu%21mania)与 [`skin.ini`](https://osu.ppy.sh/wiki/en/Skinning/skin.ini)是共同语义基线。`[Bms]`、scene 和 script 是 OMS 对第一类 BMS ruleset 的版本化扩展，不要求编译 DLL，也不冒充上游 osu! 已原生支持。

**不做**：
- 选歌 / 结果 / 菜单皮肤（lazer 已弃，OMS 跟随）。
- 改判定窗口、手感、谱面逻辑（皮肤是**纯视觉**的）。
- 直接读取 LR2 / beatoraja 皮肤文件（`.lr2skin` / `.luaskin` / `.cim`）。本系统是 OMS 自有的素材+ini 规范，**不移植**那些引擎的运行时；但其键名与元素族**对齐**这些生态，便于制作者迁移经验。
- 让脚本访问网络、任意文件、进程/线程、反射/原生库，或修改输入、判定、计分、gauge、谱面和 BGA 时间线。

---

## 2. 皮肤包结构

当前有三种来源形态：把正式分发包作为 `.osk` 导入；把开发目录放入受管 `chartskin/<包目录>/` 并在下次启动时自动发现；或在 Folder Skin Workspace 中选择合格的只读 external 目录进行注册。三种形态的包内根部都是 `skin.ini`，其余为素材：

```text
MyBmsSkin/
  skin.ini            ← 当前兼容入口
  note_white.png
  note_blue.png
  note_scratch.png
  ln_body.png
  judgeline.png
  gauge.png
  turntable.png
  ...
```

- 正式分发仍使用 `.osk`；skin-scoped importer 会在 archive metadata、entry stream、hash、file-store 与 model publication 前执行有界准入，失败或取消保留源文件且不会清理共享 hash。作者工作目录可放入 `chartskin/<包目录>/` 由下一次启动扫描，也可在 Workspace 中注册为只读 external。用户显式选中后，合法 BMS 包会实例化为同时解析 `[Mania]` 与 `[Bms]` 的 `BmsLegacySkin`。
- V1 仍以 `.osk` 为正式社区分发物；受管目录与 external 只读目录是作者工作区/高级管理面。external 注册不自动选择，random/next/previous 不会隐式选中；切走后重新选择或 configured restart 会 fresh capture。C5 已冻结并接通 `gameplay-skin.json` / `gameplay-skin.scene.json` v1 manifest/scene 与只读 event ABI；可选沙箱脚本仍留 C6，作者不应把脚本或最终整包 reload 当作当前能力。
- 只含 mania、只含 BMS 或同含两者都合法；官方 `oms-simple/oms-complex` 选择同包双 ruleset，以证明第三方无需特殊内置路径也能完成产品级皮肤。
- 可视受管目录为 OMS 数据目录下的 `chartskin/`：每个 direct child 是一个包目录，根必须含有效 `skin.ini`。程序完整启动后会后台扫描一次，有效包进入皮肤选择面，但不会自动选中。新出现的文件、reparse或坏包不会新增记录；同路径若已有scanner exact-own记录则会保留而不因暂时无效被误清理，但选择/reload时仍须重新通过capture/factory，失败只保留旧皮肤而不会发布坏包。根扫描不完整或发生竞态时整轮零对账。启动后新增direct child仍须重启发现；已登记且current的目录原位修改可在安全screen显式Reload，不由scanner自动发布。configured selection仍按typed startup/mutation顺序fresh retry，update thread不等待。
- Folder Skin Workspace 的 managed 行和既有 current 删除按钮共用 record-ID authority、确认语义、fallback 与 journal/recovery；删除是不可撤销的物理操作。current 目标先发布受保护fallback并等待旧revision detach，成功后才创建journal或开始物理操作；在此前失败/取消会保留或恢复原皮肤且不碰目录。首个物理步骤后只由durable recovery收口并保持fallback，不保证恢复已开始删除的旧目录。final preflight 后竞态新增的 foreign 节点不会被删除，但可能令部分清理后的操作冻结，因此不能把它理解成 all-or-nothing filesystem transaction。
- Workspace 的 Rename Folder 只改变 direct-child 目录名与同一记录的 managed path，不改 `skin.ini`、作者展示名、Creator、hash 或包内容。Import Managed Copy 需要作者明确给出新的 direct-child 名称，文件只从 external 的 immutable capsule 复制，目录结构来自同次捕获的 bounded manifest，不覆盖、merge 或自动 suffix，也不自动选择新副本；external 原目录始终只读。Workspace不提供行级Reload。
- external 行的 Unregister 对noncurrent记录直接做exact pure-Realm移除；对current记录先发布受保护fallback、等待旧revision detach，再fresh compare exact service-owner/record/current revision后移除。任一步失败都保留注册并恢复或保持原皮肤；即使源目录缺失或漂移也不会解析、打开、写入或删除source。Open Folder 由 manager fresh 重读并证明精确目录后再导航，UI 不缓存绝对路径。
- 路径相对 `skin.ini` 所在目录；子目录用 `/` 或 `\` 均可。
- 素材格式：PNG（含 alpha）。动画见 [§3](#3-skinini-总览与通用约定) 的帧序列约定。
- 手动reload：安全screen上的`Reload current skin`会为ordinary `.osk`、managed或external current记录重新验证并准备fresh immutable revision；全部现有participant ready后才一次发布，失败保留exact旧皮肤，旧owner等最后consumer/work detach才释放。作者对managed/external工作目录的原位修改可用此入口；ordinary `.osk`没有作者update-import或内部file-store编辑入口，内容修改仍须编辑源目录、重新打包并导入，按钮只提供统一same-ID revision协议。gameplay/preview或其它无法staged swap的attached screen会先拒绝并提示退出；这不是watcher。C4已把public document、layout与resolved material合成同一publication，C5再加入prepared scene/event与全部适用slot；BMS已提交的Note/LN/scene资源会一直由该publication保活到renderer子树detach，失败/取消的新候选则只释放自己的prepared资源，不会让旧画面引用失效。C6前仍不能把当前范围称为ini/manifest/scene/script/全部素材的最终整包reload。

---

## 3. `skin.ini` 总览与通用约定

`skin.ini` 由若干 section 组成：

```ini
[General]
Name:     My BMS Skin
Author:   You
Version:  1.0
Keymodes: 7K, 14K          // 仅作覆盖声明/编辑器提示；实际加载看各 [Bms] Keymode bucket

[Bms]
Keymode:  7K               // 此段描述 7K 的所有车道/几何/素材
// ... 7K 的键 ...

[Bms]
Keymode:  14K              // DP 单独一段
// ... 14K 的键 ...
```

**通用约定**：
- **按 keymode 分桶**：每个 `[Bms]` 段以 `Keymode:` 开头，对应一个键位模式（沿用 osu!mania「每个 `Keys` 一个 `[Mania]` 段」的习惯）。支持的 keymode（解析自代码 `BmsKeymode`）：`5K`、`7K`、`9K`（=`9K_BMS`）、`9K_PMS`（PMS，无 scratch）、`14K`（DP）；**无 `10K`**（早期草案误列，代码 `BmsKeymode` 不含）。注释用 `//`（对齐 osu/mania skin.ini，与 mania 段同一文件），不是 `;`。
- **车道编址**：段内用 lane token 索引每条车道。

  | keymode | scratch | 键 lane token |
  | --- | --- | --- |
  | 5K | `S` | `1`..`5` |
  | 7K | `S` | `1`..`7` |
  | 9K（BMS/PMS legacy `[Bms]`） | 无 | raw `0`..`8`；public target用canonical `1`..`9` |
  | 14K（DP） | `S`(P1) `S2`(P2) | `1`..`7`(P1) `8`..`14`(P2) |

  形如 `NoteImageS`、`NoteImage1`（逐道纹理键内嵌 lane token）。

  > **9K 版本边界**：legacy `[Bms]`仍按raw `0..8`查询；public `GameplaySkin.*:1` target只接受canonical `1..9`，两者仅经`bms-gameplay-skin-nine-key-index.v1`双向映射。未知版本fail-closed，绝不同时把重叠的`1..8`静默当作两套别名；stable LaneId仍由C3 topology提供。
- **颜色**：`r,g,b` 或 `r,g,b,a`（0–255），如 `MinorBarLineColour: 138,152,182,102`；**音符颜色不是逐道键**，而是 IIDX 键色组（见 [§5.4](#54-小节线--颜色)）。
- **资源名**：写**不带扩展名**的相对路径，如 `NoteImage1: notes/white`。
- **数值几何**：像素或相对值，逐键在 [附录 C](#附录-cskinini-字段全表) 注明单位。
- **动画**：帧序列沿用 `name-0`、`name-1`… 命名；当前 BMS 普通短键与长条头/身/尾纵切固定按 60 FPS 循环，`LightFramePerSecond` 不控制这些动画。其它 V1 animation 速度 ABI 尚未冻结；不引入 LR2 的 `div_x/div_y` 雪碧图分割。
- **当前容错**：public section对未知版本/字段、非法scope/type/index/selector、duplicate与escape使用稳定`OMS-SKIN-CODEC-NNN`，catalog slot使用`OMS-SKIN-SLOT-NNN`；resolver/resource/capability使用稳定小写code。成功commit后产品日志只输出catalog ID、stable target/index、source kind与合同版本，不含路径或作者值。legacy section继续保持既有兼容容错，不把其所有宽松键反向升级成public ABI。详见[公共目录](GAMEPLAY_SKIN_PUBLIC_CATALOG_V1.md)与[§7](#7-必备--推荐--可选与三态解析)。

> **schema 来源说明**：键集 / 语义的**真实依据是代码实现**（`BmsSkinDecoder` / `BmsSkinConfigurationLookups` + `BmsGameplayLayoutSolver` / `BmsDefaultPlayfieldPalette` 暴露的可参数化量）；[P1-A 技术约束 ·「皮肤创作生态」](../subline/P1-A/TECHNICAL_CONSTRAINTS.md) 与本文都是**据代码派生的视图**，**不反向约束实现**。**与 mania 同义的键尽量沿用 mania 原名**（降低迁移成本）；BMS 独有键为 OMS 新定义。`F1` 解析层（`[General]` / `[Bms]` 段、几何 / 颜色 / 纹理键）已落地，本文相关字段已据生产代码更新。

### 3.1 Public Common/BMS v1 声明

公共作者格式位于同一个`skin.ini`的`[GameplaySkin.Common:1]`或唯一BMS扩展`[GameplaySkin.Bms:1]`。每个`Target`显式写ruleset/keymode/stage-mode、scope、stable LaneId/GroupId及适用的logical/visual/global/group-local index；下面是5K lane示例：

```ini
[GameplaySkin.Common:1]
Target: Lane ruleset=bms keymode=5k stage-mode=single group=bms.group.deck-1 lane=bms.lane.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=0 group-local-visual=0
object.note: resource Provide "notes/key-1"
object.long-note.tail: resource Suppress
```

public section区分大小写，注释为引号外的`#`或`;`；resource值必须双引号并只接受文档列出的转义。完整28项ID、scope/type、Required/Recommended/Optional、Suppress资格、selector语法与诊断见[Gameplay Skin V1公共目录](GAMEPLAY_SKIN_PUBLIC_CATALOG_V1.md)。目录是代码生成并由digest锁定的唯一作者authority，不要从下面的legacy字段表推导另一套public ID。

### 3.2 `[Mania]` 共同逻辑的 V1 兼容映射

`BmsLegacySkin`保留`[Mania]`与`[Bms]`兼容数据；两种legacy adapter现在消费public codec保留的同一immutable token stream，不重开`skin.ini`或复制tokenizer。BMS Note/LN与mania Note/Hold/KeyVisual的production material resolver会在public Common层之后按下表消费legacy候选；真实`oms-simple`仍未装载，当前末端仍是受保护程序化fallback。

gameplay package的legacy候选顺序为：`[Bms]` role-aware override → 按全部视觉列数的`[Mania]` bucket → 必要的deck/key-only bucket → ruleset/canonical层。该candidate、lane resource provenance与capability验证已进入BMS production material resolver；不存在第二张slot ID表或renderer内的二次lookup。真实`oms-simple`package仍到C7接管。

| BMS 模式 | 全视觉列兼容桶 | 普通键兼容桶 | 备注 |
| --- | --- | --- | --- |
| 5K + scratch | `Keys: 6` | `Keys: 5` | 后者只映 K1–K5 |
| 7K + scratch | `Keys: 8` | `Keys: 7` | 后者只映 K1–K7 |
| 9K / PMS | `Keys: 9` | — | BMS/PMS role 仍由 adapter 区分；同一 `Keys:9` 不重复加入 key-only candidate |
| 14K + 双 scratch | `Keys: 16` | 同一 `Keys:8` bucket 分别投影两个 deck，再接 `Keys:14` 普通键 | 固定顺序为 16→8-deck→14；legacy decoder 不保留第二个重复 `Keys:8` section |

这只描述gameplay package slot，不改写lazer既有谱面内皮肤与ruleset resource authority。P2/CenterRightScratch按global visual index、14K deck按group-local visual index取compatibility column，stable LaneId/action不变。既有谱面直接视觉兼容继续优先，但C4没有新增beatmap-local public作者格式；selected坏声明不会借本package较宽声明或从低层只取同名纹理/body宽度拼件，只有下一完整authority或protected fallback接管。

---

## 4. 车道与几何

几何键控制车道布局。所有production consumer先把当前package revision、parser-owned keymode/topology、presentation style、safe bounds、aspect/DPI交给唯一`BmsGameplayLayoutSolver`，再只读同一immutable snapshot；`BmsPlayfieldLayoutProfile`只在solver内部承载归一化配置与程序化fallback，不能由playfield、HUD或BGA重新创建。下表仍是当前公开兼容键与默认输入（非mania习惯草案——BMS无mania式`HitPosition`或逐列`ColumnWidth`）：

| 键 | 作用 | 单位/语义 | 代码默认值 |
| --- | --- | --- | --- |
| `PlayfieldWidth` | 整个车道区宽度（归一化杠杆，缩放它等比缩放每条道与音符） | 屏幕宽比例 | `Clamp(lanes×0.06, .35, .8)×0.825` |
| `PlayfieldHeight` | 判定线相对高度（playfield 顶边贴屏，判定线落此处） | 屏幕高比例 | `0.92` |
| `NormalLaneWidth` | 键道相对宽 | 相对权重 | `1` |
| `ScratchLaneWidth` | scratch 道相对宽 | 相对权重 | `1.5` |
| `NormalLaneSpacing` | 键道间距 | 相对权重 | `0` |
| `ScratchLaneSpacing` | scratch 邻接间距 | 相对权重 | `0.12` |
| `HitTargetHeight` | 判定区总高 | 像素 | `16` |
| `HitTargetBarHeight` | 判定条高 | 像素 | `12` |
| `HitTargetLineHeight` | 判定线高 | 像素 | `3` |
| `HitTargetGlowRadius` | 判定线辉光半径 | 像素 | `6` |
| `BarLineHeight` | 小节线厚 | 像素 | `2` |
| `LongNoteBodyWidth` | 长条身宽 | 相对车道宽 | `0.5775` |

> `HitTargetVerticalOffset` 必须锁 `0`（保判定时序不变量），故**不开放**给 ini。`JudgementLine` / `KeysUnderNotes` 等 mania 几何键 BMS 代码无对应，已移除。

> **当前合同**：geometry逐字段验证finite、正值、合法range、安全screen bounds和字段间non-overlap。单字段非法只对该字段采用确定程序化fallback并产生稳定脱敏diagnostic；solver仍一次产出完整snapshot，不传播NaN/Infinity/负尺寸，也不拼接部分新/部分旧结果。5K/7K四style、9K BMS/PMS、14K双deck/双scratch/centre gap，以及BGA最终viewport、gauge/combo/HUD safe placement已共享该snapshot；mania single/dual stage也通过同一ruleset-neutral publication适配。BGA内容、timeline、seek和gimmick播放仍不属于皮肤layout。

---

## 5. 静态素材族（mania 对齐）

当前用户 BMS 包的 `[Bms]` 已有生产消费方的是 note/LN、lane background/divider、hit target、barline、lane cover、backdrop/baseplate 的颜色/纹理/几何子集。其中普通 `NoteImage*` 与长条头身尾 `NoteImage*H/L/T` 已支持静态图和编号帧动画；body 还会同 revision 使用经过安全域解析的 `LongNoteBodyWidth`。其它素材仍走既有路径。stage/key area 虽有部分解析或设计名，但尚无完整生产渲染消费方；下表同时列出 V1 compatibility 目标，不能把每个键都当作当前已生效。共同项优先复用 mania 语义，BMS 的 scratch/DP role 由 adapter 补足。

### 5.1 Stage 框架
| 键 | 作用 | 必备档 |
| --- | --- | --- |
| `StageLeft` / `StageRight` / `StageBottom` | IIDX 金属框左/右/下 | 推荐 |
| `StageHint` | 判定线位置提示条 | 推荐 |
| `PlayfieldBackdrop` | 车道区外的背景底 | 推荐 |

### 5.2 Note / Long note
| 键 | 作用 | 必备档 |
| --- | --- | --- |
| `NoteImage{lane}` | 逐道普通音符（如 `NoteImageS` / `NoteImage1`） | **必备**（当前缺失/损坏时继续外层链并最终落到程序化 `OmsSkin`；V1 目标为 `oms-simple`） |
| `NoteImage{lane}H` | 长条头 | **必备**（当前选中的用户 BMS 包支持静态图/连续编号帧；坏声明回落到可见 rescue） |
| `NoteImage{lane}L` | 长条身 | **必备**（当前选中的用户 BMS 包支持静态图/连续编号帧；素材与安全解析后的 `LongNoteBodyWidth` 同 revision 发布，坏声明回落到可见 rescue） |
| `NoteImage{lane}T` | 长条尾 | 推荐（当前选中的用户 BMS 包支持静态图/连续编号帧；未声明/坏声明最终为透明迁移 fallback，但不是 `Suppress`） |
| `NoteBodyStyle` | 长条身样式（stretch/repeat） | 可选 |
| `WidthForNoteHeightScale` | 音符高度按宽缩放 | 可选 |

当前用户包 body 与程序化默认 body 复用引擎拥有的 Idle/Holding/Broken 状态宿主：active alpha `0.8`、broken alpha `0.32`，约 `80ms` 过渡；HCN regrab 可回到 Holding。皮肤只改变表现，不改变 LN/CN/HCN 判定、长度、拉伸或裁剪规则。

### 5.3 判定线 / 按键区
| 键 | 作用 | 必备档 |
| --- | --- | --- |
| `HitTargetImage` | 判定线/接收区贴图 | 推荐（缺→内置线+辉光） |
| `KeyImage{lane}` / `KeyImage{lane}D` | 按键区常态/按下态 | 可选 |

### 5.4 小节线 / 颜色 `[部分]`
音符颜色＝**IIDX 键色组**（白 / 青 / 黄 / 红，按键号 + keymode 派生），**不是逐道任意色**；其余为车道 / 判定 / 小节线 / cover 颜色。键名直接对应 `BmsDefaultPlayfieldPalette`：

| 键 | 作用 | 必备档 |
| --- | --- | --- |
| `NoteColourWhite` / `NoteColourCyan` / `NoteColourYellow` | IIDX 键色组（白 / 青 / 黄键音符） | 推荐 |
| `NoteColourScratch` | scratch 音符色 | 推荐 |
| `LaneBackgroundEvenColour` / `LaneBackgroundOddColour` | 键道交替底色 | 推荐 |
| `ScratchLaneBackgroundColour` | scratch 道底色 | 推荐 |
| `LaneDividerColour` / `ScratchLaneDividerColour` | 分隔线色 | 推荐 |
| `HitTargetBarColour` / `HitTargetLineColour` / `HitTargetGlowColour` | 判定区 条 / 线 / 辉光色（scratch 另有 `ScratchHitTarget*Colour`） | 推荐 |
| `MajorBarLineColour` / `MinorBarLineColour` | 大 / 小节线色 | 推荐 |
| `LaneCoverFillColour` / `LaneCoverShadeColour` / `LaneCoverFocusColour` | lane cover 填充 / 暗部 / 调整高亮 | 可选 |
| `PlayfieldBackdropColour` / `PlayfieldBaseplateColour` | 车道区外底 / 底板色 | 可选 |

### 5.5 Lane cover（SUDDEN+/HIDDEN+/LIFT）
| 键 | 作用 | 必备档 |
| --- | --- | --- |
| `LaneCoverTop` / `LaneCoverBottom` | 顶/底遮罩贴图 | 可选 |
| `ColourLaneCover` / `ColourLaneCoverFocus` | 遮罩填充 / 调整高亮色 | 可选 |

---

## 6. BMS 扩展族与事件挂点

这一族在 mania 的 `[Mania]` 段**没有先例**（mania 无 gauge/turntable/bomb 等机制，或当作引擎 HUD），是 OMS 为 BMS 新定义的扩展键。

**关键原则**：引擎只拥有 gameplay truth、滚动/LN 裁剪、对象池、布局、BGA 解码时钟和只读事件；具体装饰节点、显隐、动画和事件响应由外部 scene/animation 层拥有，复杂组合才进入可选沙箱脚本。不能把这一原则实现成“每个新效果再加一个固定 `DefaultBmsXxxDisplay`”，也不能把 framework `Drawable` 树直接交给脚本。

以下键名是 compatibility/slot 候选，不是当前 `.osk` 已兑现的 schema。声明式层应能通过 template、binding、variant、tween/state-machine 完成绝大多数效果；脚本不是做普通 gauge/combo/judgement 的前置条件。

### 6.1 输入 / 命中 / scratch 视觉候选
| 键 | 作用 | 必备档 | 动态来源 |
| --- | --- | --- | --- |
| `KeyFlashImage` / `ColourColumnLight` | 柱光/键闪（按下/命中列闪） | 可选 | 按键 on/off |
| `NoteHitLighting` | 命中爆闪（对齐 mania `LightingN`） | 可选 | 命中事件 |
| `LnHoldLighting` | 长条保持光（对齐 mania `LightingL`） | 可选 | 持续按住 |
| `BombImage{lane}` / `ExplosionScale` | 命中爆炸 | 可选 | 命中事件 |
| `TurntableImage` / `TurntableSpin` | 转盘贴图 + 是否随 scratch 旋转 | 可选 | scratch 输入 |
| `GhostTdDisplay` | ghost / TD 时差显示 | 可选 | 判定时差 |

### 6.2 BMS 独有 HUD 候选
| 键 | 作用 | 必备档 | 备注 |
| --- | --- | --- | --- |
| `GaugeBarImage` | gauge 条主体贴图 | 可选 | BMS 无 mania 对应；可显式 suppress |
| `ColourGaugeAssistEasy` / `…Easy` / `…Normal` / `…Hard` / `…ExHard` / `…Hazard` | 六种 gauge 类型配色 | 推荐 | 对齐内置六态 |
| `GaugeNumber` | gauge 百分比数字样式 | 可选 | |
| `LaneCoverNumber` | 遮罩绿数（GN / Hi-Speed） | 可选 | |
| `BpmDisplay` | BPM 显示 | 可选 | |
| `ProgressBar` | 曲目进度 | 可选 | |

### 6.3 判定显示候选
| 键 | 作用 | 必备档 |
| --- | --- | --- |
| `JudgePGreat` / `JudgeGreat` / `JudgeGood` / `JudgeBad` / `JudgePoor` / `JudgeEmptyPoor` | 各判定档贴图（对齐 mania `Hit300g`…`Hit0` 语义） | 可选；判定位置本身不可关闭 |
| `JudgeComboBreak` | 断连提示 | 可选 |

---

## 7. 必备 / 推荐 / 可选与三态解析

旧`ISkin` ABI仍是nullable，但public authoring/runtime使用独立显式三态；BMS Note/LN与mania Note/Hold/KeyVisual已从同一resolved material set消费结果：

- `Provide`：在background prepare完成资源验证/构造后使用；坏资源产生诊断并进入下一完整authority。
- `Inherit`：显式继续下一authority；absent、empty与invalid仍各自保留，不能把invalid偷当absent，也不能跨revision拼件。当前末端是程序化rescue，C7才由只读`oms-simple.osk`接管。
- `Suppress`：只允许catalog标为Optional且runtime capability支持的slot；Required/Recommended suppress会稳定诊断并继续确定fallback。缺字段、空字符串、坏资源、`null`与`Drawable.Empty()`都不等于Suppress。

### 7.1 三档定义
- **必备 (Required)**：只指可玩核心。lane/scratch 可辨识、note/LN/mine、判定位置，以及启用 lane cover 时的实际遮挡几何必须有 rescue，不能 suppress。清单见 [附录 B](#附录-b必备元素清单)。
- **推荐 (Recommended)**：标准预期，回退到当前 `OmsSkin`／最终 `oms-simple` 可接受。
- **可选 (Optional)**：纯表现。按键动画、判定图、combo、gauge 视觉、HUD、BGA frame 和装饰效果均可显式 suppress；`oms-simple` 不得被 fallback 强行补回来。

### 7.2 校验三层（加载期）
1. **语法合法性**：未知键、非法值与不支持 capability 结构化记录；非法值逐字段回退。
2. **资源引用**：路径 containment、文件存在、解码像素/字节/纹理/帧数预算均先验证；失败只熔断对应外部层。
3. **完备性**：必备件必须能解析（皮肤提供，或由当前 `OmsSkin`／最终 `oms-simple` 兜底）。

### 7.3 行为契约（关键分工）
- **加载期 = fail-open + 可查询诊断**：错误package不阻断游玩；manual Reload为三种source在background建立package+layout+resolved-material+scene revision，全部participant ready后一次替换，失败保留exact旧quadruple。BMS成功publication持有其prepared Note/LN/scene resource直到renderer子树detach；Skin owner开始退出不会提前释放仍被画面借用的资源，失败/取消/commit拒绝的provisional publication则exactly-once清理自身。C5 scene/event与全部适用slot已进入同一协议；C6仍只负责sandbox/script及最终整包门。
- **编辑期 = 比加载期更严**：这是后续工具目标；当前没有完整可视皮肤编辑器。
- **keymode 覆盖**：实际覆盖由对应 `[Bms] Keymode:` bucket 及具体 slot 声明决定；`[General] Keymodes:` 当前仅是 informational/editor hint，不参与加载期 gating。缺失 slot 沿当前 `OmsSkin`／最终 `oms-simple` 链回落。

---

## 8. 三个作者面与布局编辑器边界

三个面职责不同：

- **`skin.ini` compatibility**：既有素材、颜色、有限参数和帧序列。
- **declarative scene/animation**：稳定 node ID、named layout slot、template、binding、variant 与动画；这是 V1 的主要创作面。
- **optional sandbox script**：只读事件驱动的复杂组合逻辑，不负责逐帧搬动每个 note。

**布局编辑器能摆什么（已知边界）**：它只识别 `ISerialisableDrawable` 的全局 HUD 件——被 `MainHUDComponents` 包裹的通用件（key counter、song progress、计分、准确率、判定计数）。它**看不见 BMS 程序化件**（车道/音符/gauge/combo），其素材选择器也只列已导入文件、无内置资产浏览器。

现有 Skin Layout Editor JSON 会序列化 CLR `Type` 并反射构造，只能继续服务既有通用 HUD，**不能**成为外部分发 scene ABI。V1 manifest 使用 allowlist 的稳定 node ID；BMS gauge/combo/clear lamp 等锚定到引擎给出的 named slot，由外部 scene 决定具体表现，不把当前 `DefaultBmsHudLayoutDisplay` 固定编排冻结成上限。

截至当前，legacy Skin Editor菜单、hotkey、overlay以及external-edit/update-import backend均稳定不可用，不能作为author-preview或reload旁路。作者目录只使用Folder Skin Workspace；reload只使用Settings的current manual Reload。未来若重启编辑器，必须另行冻结安全格式与统一revision协议。

---

## 9. `oms-simple`、`oms-complex` 与最终 fallback

当前仍是程序化默认 + reference ini，只能算迁移基线。V1 最终只有文件皮肤承担具体产品视觉：

- **`oms-simple.osk`** `[未实现]`：同包提供 mania/BMS，只显示最小可玩件；canonical copy 随发行物只读携带并校验。用户所选皮肤缺失/损坏关键件时逐组件回落到它。
- **`oms-complex.osk`** `[未实现]`：同包提供 mania/BMS，覆盖完整 slot/event，证明只靠公共素材、scene、事件和可选脚本可达到 IIDX 级完整界面。
- **当前 reference ini** `[已实现但仅迁移参考]`：位于 [oms-bms-reference-skin/skin.ini](oms-bms-reference-skin/skin.ini)，用于锁住当前 F1 palette/profile；它不是最终 `oms-simple`，也不能证明双 ruleset 或 scene/event 能力。
- **当前程序化 `OmsSkin`** `[迁移期]`：在 `oms-simple` 达到 mania/BMS parity、完整性验证、原子恢复和实机 gate 前暂留；之后退出产品渲染链。引擎仍保留通用 renderer、对象池、layout/event bridge，但不再硬编码任何主题颜色、素材、节点或动画。

若只读 canonical `oms-simple` 自身校验失败，这是安装完整性故障：应提示修复/恢复包并阻止进入 gameplay，不能静默生成另一套程序化视觉。

---

## 10. 制作流程与 V1 验收

1. **当前复制 reference ini；V1 复制 `oms-simple` 源目录**（[§9](#9-oms-simpleoms-complex-与最终-fallback)）为起点，而非从空白开始。
2. **改色 / 换图**：先动 `Colour*` 与 `*Image` 键。普通短键可让 `NoteImage{lane}`、长条头身尾可让 `NoteImage{lane}H/L/T` 指向资源基名并提供 `name-0`、`name-1`…；body 宽度可用 `LongNoteBodyWidth`，只接受 finite 且 `0 < width <= 1`。支持范围以页首能力块为准，帧率目前固定 60 FPS。
3. **重新载入和重选**：作者修改`.osk`时仍编辑源目录、重新打包并导入，不能原位改OMS内部Realm/file store，也不能使用已禁用的update-import。已登记且当前选中的managed/external工作目录内容变更，可在退出gameplay/preview并回到安全screen后点击Settings → Skin → `Reload current skin`。ordinary Realm current也使用同一按钮做same-ID重新验证/重建，但这不是作者编辑面。新增`chartskin/` direct child仍需重启让一次性scanner发现；不要等待自动检测，也不要重复选择同一项冒充reload。
4. **逐 keymode 验证**：至少覆盖你声明的每个 `Keymode`；重点检查 scratch 与键道的可读区分、14K DP 双侧布局。
5. **看运行结果与日志**：public codec/catalog分别使用稳定`OMS-SKIN-CODEC-NNN`/`OMS-SKIN-SLOT-NNN`，resolver/resource/capability使用稳定小写code；全部产品日志均脱敏。C5 runtime profile会明确区分Supported、NotApplicable与Unsupported；NotApplicable不是`Inherit`，也不代表缺少host。legacy宽松字段的诊断仍不等于完整public合同。
6. **校准提示**：`设置 → 游戏模式 → osu!mania → 滚动速度`显示的毫秒只代表标准几何下的参考下落时间；皮肤改了车道宽/判定线位置后体感会变，换皮后应重新校准，也不要拿它直接对照 BMS 的 Hi-Speed / 下落时间。

C3/C4/C5自动矩阵逐一覆盖5K/7K的P1、P2、CenterP1、CenterP2，9K BMS/PMS、14K DP与mania single/dual，并验证public material、prepared scene/event、playfield、gauge/combo、BGA safe viewport、safe-area及不同宽高比/DPI。V1发布前仍须用`oms-simple/oms-complex`两个最终包复核相同矩阵和人工视觉。manual Reload必须持续满足“新revision任一步失败仍保持exact旧package+layout+material+scene”；C6新增script consumer也必须进入相同participant/retire gate。

---

## 11. Skin Authoring Kit 是什么

它不是程序库、编译 SDK 或第三种皮肤格式，而是“皮肤作者开工包”：

- `oms-simple` 与 `oms-complex` 的可编辑源目录；
- 带注释的 `skin.ini`、scene/animation manifest 和 optional script 模板；
- mania/BMS 元素名、字段、lane role、事件、layout slot、capability 与资源预算参考；
- validator/diagnostics 的错误定位方法；
- 从普通目录打包 `.osk`、拖入导入、测试各 keymode 和发布分享的步骤。

它的目的就是把 OMS BMS 制作体验拉回 osu 社区熟悉的路径：复制一个模板、替换素材、改 ini/manifest、进游戏验证、打成 `.osk` 分享，而不是要求作者搭 C# 工程。

---

## 附录 A：游玩元素全集速查（创作者上限）

mania 的上限审查给出的结论不是“它已有通用脚本”，而是“固定 C# 行为宿主很成熟，legacy ini 负责素材/参数”。BMS V1 要复用前者的兼容语义，但用规则集无关 scene/event runtime 打开作者上限。

| 元素族 | mania 当前普通 `.osk` | BMS 当前普通 `.osk` | Skin V1 公共作者面 |
| --- | --- | --- | --- |
| Stage / backdrop | legacy 素材与固定布局较完整 | backdrop/baseplate/stage background/foreground 均有host | named slot + 静态/动画 scene node |
| Lane / playfield | column 背景、宽度、间距、key area；行为固定 C# | lane/divider/hit target 的 F1 颜色/纹理/几何 | engine layout snapshot + per-lane template |
| Note / LN | 逐列素材、body style、固定 C# hold 状态 | note/LN legacy 静态字段；普通短键与长条头/身/尾的静态/编号帧支持边界见页首能力块；body 复用引擎 Idle/Holding/Broken 状态宿主与裁剪 | pooled note/LN template；滚动与裁剪仍归引擎 |
| Mine | 无 BMS 语义 | BMS pooled mine host，Mania 按runtime profile明确NotApplicable | pooled mine template + hit/visibility event |
| Key / hit effects | key press、column light、explosion 的素材；动画逻辑固定 C# | keyflash/hit-explosion/turntable/laser均有固定host或scene route（按适用性） | press/release/hit/scratch 事件 + effect pool |
| Judgement | legacy 图片/帧，播放逻辑固定 C# | BMS/mania judgement display 由typed HUD host与scene binding消费 | optional result variant/animation；neutral result key |
| Combo / gauge / HUD | combo/HUD 走既有固定组件 | BMS/mania typed HUD host支持combo/gauge/text与只读binding | optional typed binding；可完整 suppress |
| Lane cover | mania 兼容面有限 | F1 颜色/纹理/几何子集 | engine-owned cover geometry + scene skin |
| Scratch / DP | 无 BMS role | 有 S/S2 lane topology；无完整转盘演出作者面 | stable lane role、方向/值/速度 capability |
| BGA | 无 | engine timeline 已可播；当前 display 接 raw timeline、14K 会建多 player | 单一 engine content authority + 只读 viewport/proxy |
| Barline / timing | legacy barline | F1 barline；STOP/scroll 为 ruleset truth | timing event + pooled visual，不得改 timing |
| 任意装饰 | 只能利用已有 lookup/固定位置 | 普通包缺少通用 scene | global scene、template、binding、state-machine；复杂时可选脚本 |

V1 不预先禁止 character、立绘或风味 HUD；只要它们是可选视觉、使用公共 scene/event API 且满足预算即可。明确不开放的是改输入、判定、计分、gauge truth、谱面/BGA 时间线、网络/任意文件/进程线程和任意 shader/native code。

---

## 附录 B：必备元素清单

下列才是不可 suppress 的最小可玩核心；运行链必须始终有可玩 rescue。当前由程序化 `OmsSkin` 暂代，V1 最终必须由文件型 canonical `oms-simple.osk` 提供：

- lane/scratch 的边界和角色可辨识；
- 普通音符、长条的必要可读部分与 mine；
- 判定位置；
- 当 lane cover 玩法启用时，真实遮挡范围与可调状态。

public catalog已冻结按键动画、判定**显示**、combo、gauge **视觉**、数值HUD、BGA frame、stage/角色/爆炸等可选slot及其`Suppress`资格；C5已将全部适用slot接入production scene/event host，Mania的五个版本化NotApplicable仍按ruleset能力明确排除。它们可`Inherit`获得末端表现或由作者显式`Suppress`；C7才由真实`oms-simple`承担canonical结果。

---

## 附录 C：`skin.ini` 字段全表

> legacy字段仍以`BmsSkinDecoder`/lookups和reference test为准；public author ABI只以[Gameplay Skin V1公共目录](GAMEPLAY_SKIN_PUBLIC_CATALOG_V1.md)和`GameplaySkinSlotCatalog`为准。runtime profile中的NotApplicable/Unsupported是版本化能力决定，不改变catalog语义。单位 / 语义约定：
> - 颜色 = `r,g,b` 或 `r,g,b,a`（0–255）。
> - 比例 = `0`–`1` 浮点（如 `PlayfieldWidth` / `LongNoteBodyWidth`）。
> - 像素 = 整数（如 `HitTargetHeight` / `BarLineHeight`）。
> - 资源名 = 不带扩展名的相对路径；逐道纹理键内嵌 lane token（数字或 `S` 表 scratch）。
> - 当前 native BMS 普通短键与长条头/身/尾已复用 `name-0` / `name-1`…编号帧命名并按固定 60 FPS；scene animation 另使用已冻结的 `oms-gameplay-skin-scene.v1` frame/tween/track 合同。

---

## 附录 D：进阶——受信任代码型 provider（开发扩展）

> `[已实现，但不是普通皮肤分发面]` 这条路需要编译进受信任 C#，理论上可返回任意 `Drawable`。它适合仓库开发、测试和宿主扩展，不能作为第三方 V1 皮肤达到 `oms-complex` 表现的必要条件，也不能用它证明 scene/script API 已完成。

OMS BMS 皮肤底层是 osu!lazer 的 `ISkin` / `SkinTransformer` / `SkinnableDrawable` 体系。你实现 `ISkin.GetDrawableComponent()`，按 BMS lookup 返回自己的 `Drawable`；`BmsSkinTransformer` 负责路由与按组件 fallback。入口契约见
[BmsSkinLookups.cs](../../osu.Game.Rulesets.Bms/Skinning/BmsSkinLookups.cs)、
[BmsSkinComponentLookup.cs](../../osu.Game.Rulesets.Bms/Skinning/BmsSkinComponentLookup.cs)、
[BmsSkinTransformer.cs](../../osu.Game.Rulesets.Bms/Skinning/BmsSkinTransformer.cs)。

**最小骨架**（与测试里的 `TestSkin` 同形，参考 [BmsSkinTransformerTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsSkinTransformerTest.cs)）：

```csharp
public sealed class MyBmsSkin : ISkin
{
   public Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
      => lookup switch
      {
         BmsSkinComponentLookup { Component: BmsSkinComponents.HudLayout } => new MyHudLayout(),
         BmsSkinComponentLookup { Component: BmsSkinComponents.GaugeBar } => new MyGaugeBar(),
         BmsPlayfieldSkinLookup { Element: BmsPlayfieldSkinElements.Backdrop } => new MyBackdrop(),
         BmsLaneSkinLookup { Element: BmsLaneSkinElements.Background } lane => new MyLaneBackground(lane.IsScratch),
         BmsLaneSkinLookup { Element: BmsLaneSkinElements.HitTarget } lane => new MyHitTarget(lane.IsScratch),
         BmsNoteSkinLookup { Element: BmsNoteSkinElements.Note } note => new MyNote(note.IsScratch),
         BmsLaneCoverSkinLookup cover => new MyLaneCover(cover.Position),
         BmsJudgementSkinLookup judgement => new MyJudgement(judgement.Result),
         _ => null,
      };

   public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;
   public ISample? GetSample(ISampleInfo sampleInfo) => null;
   public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup) where TLookup : notnull where TValue : notnull => null;
}
```

**部分组件必须实现特定接口，否则会被退回默认**：

| 组件 | 必须实现的接口 | 运行时会调用 |
| --- | --- | --- |
| `HudLayout` | `IBmsHudLayoutDisplay` | `SetComponents(wrappedHud, gaugeBar, comboCounter)` |
| `LaneCover` | `IBmsLaneCoverDisplay` | `SetFocused(bool)` |
| `StaticBackgroundLayer` | `IBmsBackgroundLayerDisplay` | `SetDisplayedAssetName(string)`（默认层还会自行加载实际背景贴图） |
| `BgaPanel` | `IBmsBgaPanelDisplay` | 当前会喂入 BGA 时间线 + 资源 store + 游玩时钟；这是待迁移接口，不是 V1 外部合同 |
| `ClearLamp` | `IBmsClearLampDisplay` | `SetClearLamp(...)` |
| `GaugeHistory(Panel)` | `IBmsGaugeHistoryDisplay` / `…PanelDisplay` | `SetHistory(...)` |
| BMS judgement | `IAnimatableJudgement` | `PlayAnimation()` / `GetAboveHitObjectsProxiedContent()` |

**按 lookup 数据做变体**（不要写死 7K）：`BmsLaneSkinLookup` 带 `Element/LaneIndex/LaneCount/IsScratch/Keymode/IsMajorBarLine`；`BmsNoteSkinLookup` 带 `Element/LaneIndex/IsScratch`；`BmsJudgementSkinLookup` 带 `Result/DisplayName`。优先从 lookup 与接口回调取数据，不要偷看外层容器布局。

**验证**：

```powershell
dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"
```

至少手测：7K/14K lane 数变化、scratch/normal 差异、LaneCover focused 切换、StaticBackgroundLayer 有图/无图。
