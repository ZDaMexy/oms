# OMS gameplay 皮肤制作手册（当前 `.osk/skin.ini` + Skin V1 路线）

> **读者**：想给 OMS BMS 做皮肤的制作者，以及在仓库内实现皮肤系统的开发者。
>
> **范围**：**仅游玩界面**。osu!lazer 已不再支持选歌/结果等非游玩界面皮肤，OMS 跟随这一边界——本文不涉及选歌页、结果页、菜单皮肤。
>
> **本文是什么（派生文档）**：面向皮肤制作者的当前能力与 Skin V1 开发视图。**权威契约不在本文**——共享/分离、ini、scene/event/script、fallback、layout 与安全约束冻结在 [P1-A 技术约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)，分期在 [P1-A `SV1-*` 计划](../subline/P1-A/DEVELOPMENT_PLAN.md)。本文只是制作者视图；冲突时以 P1-A 四件套为准。
>
> **实现状态（务必先读）**：截至 **2026-07-10 可信恢复基线**，当前玩家可用的是 `.osk` + `[Mania]/[Bms] skin.ini`；BMS 现存静态件已有颜色/纹理/几何支持。`chartskin/` 生产链、热重载、三态 suppress、declarative scene、事件 ABI、沙箱脚本和文件型默认均未启用。事故期 F2/Lua 不算当前能力。Skin V1 的目标是交付同权的 `oms-simple.osk` 与 `oms-complex.osk`，并让第三方使用完全相同的公开 API；当前程序化 `OmsSkin` 仅为迁移基线，最终不进入产品渲染链。恢复依据见 [恢复审计](SKIN_SYSTEM_RECOVERY_20260710.md)，新架构见 [V1 架构审计](SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

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
10. [当前制作流程与 V1 验收](#10-当前制作流程与-v1-验收)
11. [Skin Authoring Kit 是什么](#11-skin-authoring-kit-是什么)
- [附录 A：游玩元素全集速查（创作者上限）](#附录-a游玩元素全集速查创作者上限)
- [附录 B：必备元素清单](#附录-b必备元素清单)
- [附录 C：`skin.ini` 字段全表](#附录-cskinini-字段全表)
- [附录 D：受信任代码型 provider](#附录-d进阶受信任代码型-provider开发扩展)
- [附录 E：状态与路线图](#附录-e状态与路线图)

---

## 1. 这套皮肤能做什么 / 不做什么

**V1 目标能做（不是当前全部可用）**：stage、lane、note/LN、判定位置、judgement、gauge、combo、lane cover、BGA frame，以及 turntable、keyflash、hit lighting、hold light、ghost/TD、bpm/progress 等。当前 `.osk/ini` 只实现其中的静态子集；分层矩阵见 [附录 A](#附录-a游玩元素全集速查创作者上限)。

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

当前可用形态是导入 `.osk`；包内根部为 `skin.ini`，其余为素材：

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

- 当前生产路径：把该文件夹内容打包为 `.osk` 后经游戏导入；导入器会把皮肤实例化为同时解析 `[Mania]` 与 `[Bms]` 的 `BmsLegacySkin`。
- V1 以 `.osk` 为正式社区分发物，并恢复受管理/外部只读文件夹作为作者工作区/高级管理面。包内还会容纳 declarative scene/animation manifest 与可选沙箱脚本；文件名和 schema 要到 `SV1-5/6` 才冻结，当前不要据草案制作。
- 只含 mania、只含 BMS 或同含两者都合法；官方 `oms-simple/oms-complex` 选择同包双 ruleset，以证明第三方无需特殊内置路径也能完成产品级皮肤。
- 规划中的可视目录为 OMS 数据目录下的 `chartskin/`，但可信恢复基线尚未启用扫描、选择、删除、重命名或热重载；不要手工放入后期待自动发现。
- 路径相对 `skin.ini` 所在目录；子目录用 `/` 或 `\` 均可。
- 素材格式：PNG（含 alpha）。动画见 [§3](#3-skinini-总览与通用约定) 的帧序列约定。
- 热重载：当前可信基线未启用；修改来源后需重新导入/重选。安全路径 authority、完整验证后原子切换与失败保留旧实例属于 `SV1-2` 验收项。

---

## 3. `skin.ini` 总览与通用约定

`skin.ini` 由若干 section 组成：

```ini
[General]
Name:     My BMS Skin
Author:   You
Version:  1.0
Keymodes: 7K, 14K          // 本皮肤声明覆盖的 keymode；未声明的回退内置默认

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
  | 9K（BMS/PMS，当前未版本化） | 无 | `0`..`8` |
  | 14K（DP） | `S`(P1) `S2`(P2) | `1`..`7`(P1) `8`..`14`(P2) |

  形如 `NoteImageS`、`NoteImage1`（逐道纹理键内嵌 lane token）。

  > **9K 兼容债务**：当前 `BmsLegacySkin` 对非 scratch 直接使用 raw logical lane index，因此无 scratch 的 9K BMS/PMS 实际查询 `0..8`。V1 canonical 作者格式目标仍是 `1..9`，但必须通过显式格式版本、迁移和冲突诊断切换；`1..8` 在两套编号中含义重叠，不能把 `0..8` 与 `1..9` 同时静默当作别名。internal stable lane ID 仍为 `K1..K9`，不等同该 raw token。
- **颜色**：`r,g,b` 或 `r,g,b,a`（0–255），如 `MinorBarLineColour: 138,152,182,102`；**音符颜色不是逐道键**，而是 IIDX 键色组（见 [§5.4](#54-小节线--颜色)）。
- **资源名**：写**不带扩展名**的相对路径，如 `NoteImage1: notes/white`。
- **数值几何**：像素或相对值，逐键在 [附录 C](#附录-cskinini-字段全表) 注明单位。
- **动画**：帧序列用 `name-0`、`name-1`… 命名 + `LightFramePerSecond` 控速（对齐 mania）；不引入 LR2 的 `div_x/div_y` 雪碧图分割。
- **当前容错**：未知键通常被忽略、非法值回落；当前 decoder 没有完整、可查询的结构化诊断，不能承诺每个坏行都有告警。V1 才冻结未知键、非法值、缺资源、不支持 capability 与 fallback 来源的诊断合同。详见 [§7](#7-必备--推荐--可选与三态解析)。

> **schema 来源说明**：键集 / 语义的**真实依据是代码实现**（`BmsSkinDecoder` / `BmsSkinConfigurationLookups` + `BmsPlayfieldLayoutProfile` / `BmsDefaultPlayfieldPalette` 暴露的可参数化量）；[P1-A 技术约束 ·「皮肤创作生态」](../subline/P1-A/TECHNICAL_CONSTRAINTS.md) 与本文都是**据代码派生的视图**，**不反向约束实现**。**与 mania 同义的键尽量沿用 mania 原名**（降低迁移成本）；BMS 独有键为 OMS 新定义。`F1` 解析层（`[General]` / `[Bms]` 段、几何 / 颜色 / 纹理键）已落地，本文相关字段已据 `F1` 代码更新。

### 3.1 `[Mania]` 共同逻辑的 V1 兼容映射

当前 `BmsLegacySkin` 会保留 `[Mania]` 数据，但 BMS 生产查询尚未把它作为共同件 fallback。V1 采用 **adapter-first**：现已先把六类逐 lane 资源从现有 mania/BMS decoder 适配到保留“是否显式声明”的 neutral snapshot，并建立未接生产的候选计划；后续再扩齐配置并逐步共用 codec，不在这一刀重写成熟 mania 生产解析器。

gameplay package 的目标候选顺序为：`[Bms]` role-aware override → 按全部视觉列数的 `[Mania]` bucket → 必要的 deck/key-only bucket → `oms-simple`。当前合同 fixture 只保留整个有序候选计划和末端 canonical marker，不验证资源、不选择首值，也没有装载真实 `oms-simple` package。

| BMS 模式 | 全视觉列兼容桶 | 普通键兼容桶 | 备注 |
| --- | --- | --- | --- |
| 5K + scratch | `Keys: 6` | `Keys: 5` | 后者只映 K1–K5 |
| 7K + scratch | `Keys: 8` | `Keys: 7` | 后者只映 K1–K7 |
| 9K / PMS | `Keys: 9` | — | BMS/PMS role 仍由 adapter 区分；同一 `Keys:9` 不重复加入 key-only candidate |
| 14K + 双 scratch | `Keys: 16` | 同一 `Keys:8` bucket 分别投影两个 deck，再接 `Keys:14` 普通键 | 固定顺序为 16→8-deck→14；legacy decoder 不保留第二个重复 `Keys:8` section |

这只描述 gameplay package slot，不改写 lazer 现有的谱面内皮肤与 ruleset resource provider authority。当前 fixture 已固定 P2/CenterRightScratch 按 visual index 取 compatibility column，而 stable lane ID/action 不变；生产查询链尚未接入。

---

## 4. 车道与几何

几何键控制车道布局；当前内置默认几何走 `BmsPlayfieldLayoutProfile` 的**归一化策略**（车道按相对权重填满 `PlayfieldWidth`），所以是「相对/像素混合」语义。**下表键名与默认值直接对应代码 `BmsPlayfieldLayoutProfile.CreateDefault(...)`**（非 mania 习惯草案——BMS 无 mania 式 `HitPosition` 或逐列 `ColumnWidth`）：

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

> **当前风险**：这些 float 尚未完整校验 finite、正值、范围和屏内边界；`0`、负值、`NaN` 或超屏值可能进入归一化。并且 playfield 读取皮肤 profile，gauge/combo 会重新创建默认 profile，BGA 仍使用固定 rect。V1 在 `SV1-3` 引入唯一 `BmsGameplayLayoutSnapshot`，先逐字段 fail-open，再让 playfield、HUD、BGA 与外部 scene 共用同一最终 rect。

---

## 5. 静态素材族（mania 对齐）

当前 `.osk/[Bms]` 已有生产消费方的是 note/LN、lane background/divider、hit target、barline、lane cover、backdrop/baseplate 的颜色/纹理/几何子集。stage/key area 虽有部分解析或设计名，但尚无完整生产渲染消费方；下表同时列出 V1 compatibility 目标，不能把每个键都当作当前已生效。共同项优先复用 mania 语义，BMS 的 scratch/DP role 由 adapter 补足。

### 5.1 Stage 框架
| 键 | 作用 | 必备档 |
| --- | --- | --- |
| `StageLeft` / `StageRight` / `StageBottom` | IIDX 金属框左/右/下 | 推荐 |
| `StageHint` | 判定线位置提示条 | 推荐 |
| `PlayfieldBackdrop` | 车道区外的背景底 | 推荐 |

### 5.2 Note / Long note
| 键 | 作用 | 必备档 |
| --- | --- | --- |
| `NoteImage{lane}` | 逐道普通音符（如 `NoteImageS` / `NoteImage1`） | **必备**（缺→内置色块） |
| `NoteImage{lane}H` | 长条头 | **必备** |
| `NoteImage{lane}L` | 长条身 | **必备** |
| `NoteImage{lane}T` | 长条尾 | 推荐（默认透明） |
| `NoteBodyStyle` | 长条身样式（stretch/repeat） | 可选 |
| `WidthForNoteHeightScale` | 音符高度按宽缩放 | 可选 |

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

当前 nullable lookup 只有“提供 / `null` 后继续 fallback”，没有显式关闭。V1 新增平行 gameplay provider 结果，不直接破坏现有 `ISkin` ABI：

- `Provide`：验证成功后使用；资源坏掉则降为 `Inherit` 并诊断。
- `Inherit`：按组件继续 compatibility，并最终落到只读 `oms-simple.osk`。
- `Suppress`：作者明确不显示，只允许可选视觉；缺文件绝不等于 suppress。

### 7.1 三档定义
- **必备 (Required)**：只指可玩核心。lane/scratch 可辨识、note/LN/mine、判定位置，以及启用 lane cover 时的实际遮挡几何必须有 rescue，不能 suppress。清单见 [附录 B](#附录-b必备元素清单)。
- **推荐 (Recommended)**：标准预期，回退到内置可接受。
- **可选 (Optional)**：纯表现。按键动画、判定图、combo、gauge 视觉、HUD、BGA frame 和装饰效果均可显式 suppress；`oms-simple` 不得被 fallback 强行补回来。

### 7.2 校验三层（加载期）
1. **语法合法性**：未知键、非法值与不支持 capability 结构化记录；非法值逐字段回退。
2. **资源引用**：路径 containment、文件存在、解码像素/字节/纹理/帧数预算均先验证；失败只熔断对应外部层。
3. **完备性**：必备件必须能解析（皮肤给或内置兜底）。

### 7.3 行为契约（关键分工）
- **加载期 = fail-open + 可查询诊断**：错误 package 不阻断游玩；完整验证成功后才原子替换旧实例。当前恢复基线只具备部分 fail-open，没有这套完整诊断/原子 reload。
- **编辑期 = 比加载期更严**：这是后续工具目标；当前没有完整可视皮肤编辑器。
- **keymode 覆盖**：皮肤可只声明部分 keymode（如只做 7K），未声明的 keymode → 回退内置默认；`[General] Keymodes:` 用于声明覆盖面与编辑期提示。

---

## 8. 三个作者面与布局编辑器边界

三个面职责不同：

- **`skin.ini` compatibility**：既有素材、颜色、有限参数和帧序列。
- **declarative scene/animation**：稳定 node ID、named layout slot、template、binding、variant 与动画；这是 V1 的主要创作面。
- **optional sandbox script**：只读事件驱动的复杂组合逻辑，不负责逐帧搬动每个 note。

**布局编辑器能摆什么（已知边界）**：它只识别 `ISerialisableDrawable` 的全局 HUD 件——被 `MainHUDComponents` 包裹的通用件（key counter、song progress、计分、准确率、判定计数）。它**看不见 BMS 程序化件**（车道/音符/gauge/combo），其素材选择器也只列已导入文件、无内置资产浏览器。

现有 Skin Layout Editor JSON 会序列化 CLR `Type` 并反射构造，只能继续服务既有通用 HUD，**不能**成为外部分发 scene ABI。V1 manifest 使用 allowlist 的稳定 node ID；BMS gauge/combo/clear lamp 等锚定到引擎给出的 named slot，由外部 scene 决定具体表现，不把当前 `DefaultBmsHudLayoutDisplay` 固定编排冻结成上限。

---

## 9. `oms-simple`、`oms-complex` 与最终 fallback

当前仍是程序化默认 + reference ini，只能算迁移基线。V1 最终只有文件皮肤承担具体产品视觉：

- **`oms-simple.osk`** `[未实现]`：同包提供 mania/BMS，只显示最小可玩件；canonical copy 随发行物只读携带并校验。用户所选皮肤缺失/损坏关键件时逐组件回落到它。
- **`oms-complex.osk`** `[未实现]`：同包提供 mania/BMS，覆盖完整 slot/event，证明只靠公共素材、scene、事件和可选脚本可达到 IIDX 级完整界面。
- **当前 reference ini** `[已实现但仅迁移参考]`：位于 [oms-bms-reference-skin/skin.ini](oms-bms-reference-skin/skin.ini)，用于锁住当前 F1 palette/profile；它不是最终 `oms-simple`，也不能证明双 ruleset 或 scene/event 能力。
- **当前程序化 `OmsSkin`** `[迁移期]`：在 `oms-simple` 达到 mania/BMS parity、完整性验证、原子恢复和实机 gate 前暂留；之后退出产品渲染链。引擎仍保留通用 renderer、对象池、layout/event bridge，但不再硬编码任何主题颜色、素材、节点或动画。

若只读 canonical `oms-simple` 自身校验失败，这是安装完整性故障：应提示修复/恢复包并阻止进入 gameplay，不能静默生成另一套程序化视觉。

---

## 10. 当前制作流程与 V1 验收

1. **当前复制 reference ini；V1 复制 `oms-simple` 源目录**（[§9](#9-oms-simpleoms-complex-与最终-fallback)）为起点，而非从空白开始。
2. **改色 / 换图**：先动 `Colour*` 与 `*Image` 键。
3. **重新打包、导入和重选**：当前没有可信热重载；不要依赖 `chartskin/` 自动扫描。
4. **逐 keymode 验证**：至少覆盖你声明的每个 `Keymode`；重点检查 scratch 与键道的可读区分、14K DP 双侧布局。
5. **看运行结果与日志**：当前诊断并不完整；遇到静默回退时以实际渲染与 focused test 为准。
6. **校准提示**：`设置 → 游戏模式 → osu!mania → 滚动速度`显示的毫秒只代表标准几何下的参考下落时间；皮肤改了车道宽/判定线位置后体感会变，换皮后应重新校准，也不要拿它直接对照 BMS 的 Hi-Speed / 下落时间。

V1 发布前还必须逐一验收 5K/7K 的 P1、P2、CenterP1、CenterP2，9K BMS/PMS，14K DP；每项同时检查 playfield、gauge/combo slot、BGA safe viewport、不同宽高比/DPI，以及 `oms-simple/oms-complex` 两个包。热重载只在“新实例验证失败仍保留旧实例”成立后开放。

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
| Stage / backdrop | legacy 素材与固定布局较完整 | backdrop/baseplate 可配；stage 消费方不完整 | named slot + 静态/动画 scene node |
| Lane / playfield | column 背景、宽度、间距、key area；行为固定 C# | lane/divider/hit target 的 F1 颜色/纹理/几何 | engine layout snapshot + per-lane template |
| Note / LN | 逐列素材、body style、固定 C# hold 状态 | note/LN F1 子集；hold truth 在引擎 | pooled note/LN template；滚动与裁剪仍归引擎 |
| Mine | 无 BMS 语义 | 当前为程序化 visual，未形成外部完整槽 | pooled mine template + hit/visibility event |
| Key / hit effects | key press、column light、explosion 的素材；动画逻辑固定 C# | hit target 已有，keyflash/bomb/turntable 等未生产化 | press/release/hit/scratch 事件 + effect pool |
| Judgement | legacy 图片/帧，播放逻辑固定 C# | 主要是程序化/受信任 code provider；非完整 `[Bms]` 作者面 | optional result variant/animation；neutral result key |
| Combo / gauge / HUD | combo/HUD 走既有固定组件 | BMS 固定 C# HUD，可由受信任 provider 换；普通 ini 上限低 | optional typed binding；可完整 suppress |
| Lane cover | mania 兼容面有限 | F1 颜色/纹理/几何子集 | engine-owned cover geometry + scene skin |
| Scratch / DP | 无 BMS role | 有 S/S2 lane topology；无完整转盘演出作者面 | stable lane role、方向/值/速度 capability |
| BGA | 无 | engine timeline 已可播；当前 display 接 raw timeline、14K 会建多 player | 单一 engine content authority + 只读 viewport/proxy |
| Barline / timing | legacy barline | F1 barline；STOP/scroll 为 ruleset truth | timing event + pooled visual，不得改 timing |
| 任意装饰 | 只能利用已有 lookup/固定位置 | 普通包缺少通用 scene | global scene、template、binding、state-machine；复杂时可选脚本 |

V1 不预先禁止 character、立绘或风味 HUD；只要它们是可选视觉、使用公共 scene/event API 且满足预算即可。明确不开放的是改输入、判定、计分、gauge truth、谱面/BGA 时间线、网络/任意文件/进程线程和任意 shader/native code。

---

## 附录 B：必备元素清单

下列才是不可 suppress 的最小可玩核心；引擎必须始终有风格中立的 rescue：

- lane/scratch 的边界和角色可辨识；
- 普通音符、长条的必要可读部分与 mine；
- 判定位置；
- 当 lane cover 玩法启用时，真实遮挡范围与可调状态。

按键动画、判定**显示**、combo、gauge **视觉**、数值 HUD、BGA frame、stage/角色/爆炸等均为可选。它们可 `Inherit` 获得 `oms-simple` 表现，也可由作者显式 `Suppress`；这正是“只有色块下落与按键游玩”的合法 `oms-simple` 下限。

---

## 附录 C：`skin.ini` 字段全表

> 当前真实键以 `BmsSkinDecoder` / lookups 和 reference test 为准；§6 是 V1 候选槽，不等于已实现 ini schema。V1 在 neutral codec fixture 冻结后再发布机器可读 schema。单位 / 语义约定：
> - 颜色 = `r,g,b` 或 `r,g,b,a`（0–255）。
> - 比例 = `0`–`1` 浮点（如 `PlayfieldWidth` / `LongNoteBodyWidth`）。
> - 像素 = 整数（如 `HitTargetHeight` / `BarLineHeight`）。
> - 资源名 = 不带扩展名的相对路径；逐道纹理键内嵌 lane token（数字或 `S` 表 scratch）。
> - mania compatibility 帧序列沿用 `name-0` / `name-1`…；新 scene animation 的格式尚未冻结。

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

---

## 附录 E：状态与路线图

| 能力 | 状态 | 阶段 |
| --- | --- | --- |
| 可信恢复、数据/实机 gate | `[已完成]` | `SV1-0` |
| neutral layout/event/config DTO、三态、capability 与 fixture | `[进行中：十二个合同地基切片已落，含 neutral topology-preserving validation 与 legacy mania primitive scalar/indexed-array accepted snapshot]` | `SV1-1` |
| 安全 G1：路径 authority、扫描/选择、原子 reload | `[规划]` | `SV1-2` |
| 5K/7K 四布局、9K、14K 的唯一 layout snapshot 与单一 BGA content authority | `[规划]` | `SV1-3` |
| adapter-first 共同 ini codec 与 mania compatibility fallback | `[规划]` | `SV1-4` |
| declarative scene、模板/对象池、typed binding、只读事件 ABI | `[规划]` | `SV1-5` |
| 可选脚本沙箱、capability 授权与资源/指令/heap 预算 | `[规划]` | `SV1-6` |
| `oms-simple` fallback + `oms-complex` + Authoring Kit + 全 release gate | `[规划]` | `SV1-7` |

旧 `F/G` 编号只用于查询 2026-06-27 至恢复事故前后的历史，不再是执行顺序。完整设计取舍、当前代码证据与完成定义见 [Skin V1 架构审计](SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

### 后续追踪文档
- [../mainline/DEVELOPMENT_STATUS.md](../mainline/DEVELOPMENT_STATUS.md)：当前真实状态
- [../mainline/DEVELOPMENT_PLAN.md](../mainline/DEVELOPMENT_PLAN.md)：执行顺序与阶段依赖
- [../mainline/OMS_COPILOT.md](../mainline/OMS_COPILOT.md)：权威产品边界、fallback 纪律、release gate
- [../subline/P1-A/README.md](../subline/P1-A/README.md)：P1-A 皮肤边界子线（本规划主归属）
- [SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md](SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)：本轮 mania/BMS 代码审查、共享边界、layout/event/script 完成定义
