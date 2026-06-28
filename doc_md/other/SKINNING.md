# OMS BMS 皮肤制作手册（素材 + skin.ini 路线）

> **读者**：想给 OMS BMS 做皮肤的制作者，以及在仓库内实现皮肤系统的开发者。
>
> **范围**：**仅游玩界面**。osu!lazer 已不再支持选歌/结果等非游玩界面皮肤，OMS 跟随这一边界——本文不涉及选歌页、结果页、菜单皮肤。
>
> **本文是什么（派生文档）**：面向皮肤制作者的 **BMS 素材 + `skin.ini` 皮肤开发视图**。**权威契约不在本文**——组件挂点、ini schema、必备分档与校验行为冻结在 **[P1-A 技术约束 ·「皮肤创作生态（素材 + ini）」](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)**，分期在 **[P1-A 开发计划 · `F` 系列](../subline/P1-A/DEVELOPMENT_PLAN.md)**。本文只是把那份契约渲染成制作者可读的形式。若两者冲突，**以 P1-A 四件套为准**（`other/` 是参考材料，不替代主线计划 / 约束）。
>
> **实现状态（务必先读）**：截至 **2026-06-27**，皮肤创作生态处于 `F0`（契约冻结，纯文档，未开工实现）；运行时真正执行的皮肤路线仍是**代码型 provider**（见 [附录 D](#附录-d进阶代码型-provider当前实际运行路线)）。素材 + ini 的加载器 / 校验器 / 热重载 / 参考皮肤在 `F1+` 分期落地。本文每个主章节用 `[规划]` / `[部分]` / `[已实现]` 标注当前可用性。**延续本仓库纪律：不把"未来计划"伪装成"当前可用工作流"。** 想立刻动手做皮肤，今天仍走 [附录 D](#附录-d进阶代码型-provider当前实际运行路线)。

---

## 目录

1. [这套皮肤能做什么 / 不做什么](#1-这套皮肤能做什么--不做什么)
2. [皮肤包结构](#2-皮肤包结构-规划)
3. [`skin.ini` 总览与通用约定](#3-skinini-总览与通用约定-规划)
4. [车道与几何](#4-车道与几何-规划)
5. [静态素材族（①类，mania 对齐）](#5-静态素材族类mania-对齐-规划)
6. [BMS 扩展族（②③类，`[Bms]` 扩展段）](#6-bms-扩展族类bms-扩展段-规划)
7. [必备 / 推荐 / 可选 三档与校验行为](#7-必备--推荐--可选-三档与校验行为-部分)
8. [两个编辑面与布局编辑器边界](#8-两个编辑面与布局编辑器边界-部分)
9. [程序化默认与参考皮肤](#9-程序化默认与参考皮肤-规划)
10. [制作流程](#10-制作流程-规划)
- [附录 A：游玩元素全集速查（创作者上限）](#附录-a游玩元素全集速查创作者上限)
- [附录 B：必备元素清单](#附录-b必备元素清单)
- [附录 C：`skin.ini` 字段全表](#附录-cskinini-字段全表)
- [附录 D：进阶——代码型 provider（当前实际运行路线）](#附录-d进阶代码型-provider当前实际运行路线)
- [附录 E：状态与路线图](#附录-e状态与路线图)

---

## 1. 这套皮肤能做什么 / 不做什么

**能做（游玩界面内的一切视觉）**：stage 框架、车道、音符、长条（LN/CN/HCN）、判定线、判定显示、gauge、combo、lane cover、小节线、BGA 浮窗，以及 IIDX 风格的演出件——转盘（turntable）、柱光（keyflash）、命中爆炸（bomb / hit lighting）、长条保持光、ghost/TD 时差、bpm、progress 等。完整上限见 [附录 A](#附录-a游玩元素全集速查创作者上限)。

**两个正交的编辑面**：
- **`skin.ini` + 素材 = "长相"**：颜色、贴图、几何、显隐。本文主体。
- **lazer 布局编辑器 = "摆位"**：通用全局 HUD 件（key counter / song progress / 计分 / 判定计数）的位置。边界见 [§8](#8-两个编辑面与布局编辑器边界-部分)。

**不做**：
- 选歌 / 结果 / 菜单皮肤（lazer 已弃，OMS 跟随）。
- 改判定窗口、手感、谱面逻辑（皮肤是**纯视觉**的）。
- 直接读取 LR2 / beatoraja 皮肤文件（`.lr2skin` / `.luaskin` / `.cim`）。本系统是 OMS 自有的素材+ini 规范，**不移植**那些引擎的运行时；但其键名与元素族**对齐**这些生态，便于制作者迁移经验。

---

## 2. 皮肤包结构 `[规划]`

一个 BMS 皮肤是一个文件夹，根部一个 `skin.ini`，其余是素材：

```text
MyBmsSkin/
  skin.ini            ← 入口，必需
  note_white.png
  note_blue.png
  note_scratch.png
  ln_body.png
  judgeline.png
  gauge.png
  turntable.png
  ...
```

- 放置位置：OMS 数据目录下的 `skins/`（与 mania 皮肤同级）。
- 路径相对 `skin.ini` 所在目录；子目录用 `/` 或 `\` 均可。
- 素材格式：PNG（含 alpha）。动画见 [§3](#3-skinini-总览与通用约定-规划) 的帧序列约定。
- 热重载：编辑 `skin.ini` 或替换素材后，游戏内可触发重载预览，无需重启（[§10](#10-制作流程-规划)）。

---

## 3. `skin.ini` 总览与通用约定 `[部分]`（`F1` 解析层已实现）

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
  | 9K（PMS） | 无 | `1`..`9` |
  | 14K（DP） | `S`(P1) `S2`(P2) | `1`..`7`(P1) `8`..`14`(P2) |

  形如 `NoteImageS`、`NoteImage1`（逐道纹理键内嵌 lane token）。
- **颜色**：`r,g,b` 或 `r,g,b,a`（0–255），如 `MinorBarLineColour: 138,152,182,102`；**音符颜色不是逐道键**，而是 IIDX 键色组（见 [§5.4](#54-小节线--颜色)）。
- **资源名**：写**不带扩展名**的相对路径，如 `NoteImage1: notes/white`。
- **数值几何**：像素或相对值，逐键在 [附录 C](#附录-cskinini-字段全表) 注明单位。
- **动画**：帧序列用 `name-0`、`name-1`… 命名 + `LightFramePerSecond` 控速（对齐 mania）；不引入 LR2 的 `div_x/div_y` 雪碧图分割。
- **前向兼容**：**未知键被忽略并记一条告警**（不报错），所以皮肤可以安全写入未来才实现的键；**未知值**回退默认 + 告警。详见 [§7](#7-必备--推荐--可选-三档与校验行为-部分)。

> **schema 来源说明**：键集 / 语义的**真实依据是代码实现**（`BmsSkinDecoder` / `BmsSkinConfigurationLookups` + `BmsPlayfieldLayoutProfile` / `BmsDefaultPlayfieldPalette` 暴露的可参数化量）；[P1-A 技术约束 ·「皮肤创作生态」](../subline/P1-A/TECHNICAL_CONSTRAINTS.md) 与本文都是**据代码派生的视图**，**不反向约束实现**。**与 mania 同义的键尽量沿用 mania 原名**（降低迁移成本）；BMS 独有键为 OMS 新定义。`F1` 解析层（`[General]` / `[Bms]` 段、几何 / 颜色 / 纹理键）已落地，本文相关字段已据 `F1` 代码更新。

---

## 4. 车道与几何 `[部分]`（`F1` 已可解析这些键）

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

---

## 5. 静态素材族（①类，mania 对齐）`[规划]`

这一族**纯素材 + 颜色 + 位置**，无需引擎动态逻辑——是落地最早、最稳的一批（对应规划中的 P1）。键名尽量对齐 mania，使 osu!mania 皮肤作者零学习成本迁移。

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

## 6. BMS 扩展族（②③类，`[Bms]` 扩展段）`[规划]`

这一族在 mania 的 `[Mania]` 段**没有先例**（mania 无 gauge/turntable/bomb 等机制，或当作引擎 HUD），是 OMS 为 BMS 新定义的扩展键。

**关键原则**：这些件的**动态由引擎驱动**，`skin.ini` 只提供**素材 + 位置 + 缩放 + 颜色**。你不需要写关键帧脚本（这与 LR2/beatoraja 的 timer/op/dst 体系不同；OMS 的可编辑标准对齐 osu-ini 的静态模型）。

### 6.1 ②类：引擎驱动、ini 供素材（实现见规划 P2）
| 键 | 作用 | 必备档 | 动态来源 |
| --- | --- | --- | --- |
| `KeyFlashImage` / `ColourColumnLight` | 柱光/键闪（按下/命中列闪） | 可选 | 按键 on/off |
| `NoteHitLighting` | 命中爆闪（对齐 mania `LightingN`） | 可选 | 命中事件 |
| `LnHoldLighting` | 长条保持光（对齐 mania `LightingL`） | 可选 | 持续按住 |
| `BombImage{lane}` / `ExplosionScale` | 命中爆炸 | 可选 | 命中事件 |
| `TurntableImage` / `TurntableSpin` | 转盘贴图 + 是否随 scratch 旋转 | 可选 | scratch 输入 |
| `GhostTdDisplay` | ghost / TD 时差显示 | 可选 | 判定时差 |

### 6.2 ③类：BMS 独有 HUD（实现见规划 P3）
| 键 | 作用 | 必备档 | 备注 |
| --- | --- | --- | --- |
| `GaugeBarImage` | gauge 条主体贴图 | **必备**（缺→内置槽位条） | BMS 无 mania 对应 |
| `ColourGaugeAssistEasy` / `…Easy` / `…Normal` / `…Hard` / `…ExHard` / `…Hazard` | 六种 gauge 类型配色 | 推荐 | 对齐内置六态 |
| `GaugeNumber` | gauge 百分比数字样式 | 可选 | |
| `LaneCoverNumber` | 遮罩绿数（GN / Hi-Speed） | 可选 | |
| `BpmDisplay` | BPM 显示 | 可选 | |
| `ProgressBar` | 曲目进度 | 可选 | |

### 6.3 判定显示
| 键 | 作用 | 必备档 |
| --- | --- | --- |
| `JudgePGreat` / `JudgeGreat` / `JudgeGood` / `JudgeBad` / `JudgePoor` / `JudgeEmptyPoor` | 各判定档贴图（对齐 mania `Hit300g`…`Hit0` 语义） | **必备**（缺→内置文字） |
| `JudgeComboBreak` | 断连提示 | 可选 |

---

## 7. 必备 / 推荐 / 可选 三档与校验行为 `[部分]`

生态惯例（osu / LR2 / beatoraja）是 **fail-open**：缺件→引擎默认，坏行→跳过，不硬校验。OMS 在此之上加一层**显式分档 + 诊断**，以支撑"编辑生态"。

### 7.1 三档定义
- **必备 (Required)**：缺了不可玩或视觉破碎。引擎**始终**有内置兜底，皮肤**可覆盖、不可删**——所以皮肤永远做不出"不可玩"的结果。清单见 [附录 B](#附录-b必备元素清单)。
- **推荐 (Recommended)**：标准预期，回退到内置可接受。
- **可选 (Optional)**：纯增强，缺省 = 不显示。

### 7.2 校验三层（加载期）
1. **语法合法性**：`skin.ini` 可解析、段/键可识别。**未知键→忽略 + 告警**（前向兼容）；**值类型/范围错→回退默认 + 告警**。
2. **资源引用**：引用的贴图存在；**缺失→回退内置 + 告警**（绝不崩）。
3. **完备性**：必备件必须能解析（皮肤给或内置兜底）。

### 7.3 行为契约（关键分工）
- **加载期 = fail-open + 诊断日志**：**永不阻断游玩**。任何缺失/非法都降级到内置兜底并记一条诊断，玩家照常进图。
- **编辑期 = 比加载期更严**：主动暴露必备槽位、实时校验、**阻止保存**结构性损坏的皮肤。
- **keymode 覆盖**：皮肤可只声明部分 keymode（如只做 7K），未声明的 keymode → 回退内置默认；`[General] Keymodes:` 用于声明覆盖面与编辑期提示。

---

## 8. 两个编辑面与布局编辑器边界 `[部分]`

两个面**正交、不重叠**：
- **`skin.ini` + 素材** 管"长相"（贴图/颜色/几何/显隐）。
- **lazer 布局编辑器** 管"摆位"（位置/缩放/显隐）。

**布局编辑器能摆什么（已知边界）**：它只识别 `ISerialisableDrawable` 的全局 HUD 件——被 `MainHUDComponents` 包裹的通用件（key counter、song progress、计分、准确率、判定计数）。它**看不见 BMS 程序化件**（车道/音符/gauge/combo），其素材选择器也只列已导入文件、无内置资产浏览器。

**因此的设计边界（决议 X）**：BMS 专属 HUD（gauge 条、combo、clear lamp）**保持 `DefaultBmsHudLayoutDisplay` 代码编排 + `skin.ini` 调外观**，不强行升格为可在布局编辑器里自由拖摆的件。换言之——它们的"长相"可换，"摆位"由皮肤布局方案决定，而非布局编辑器。（把它们也做成可编辑器拖摆是未来选项，不在当前范围。）

---

## 9. 程序化默认与参考皮肤 `[规划]`

OMS 同时维护两层"默认"，对齐 osu! 的范式：

- **程序化内置默认（兜底，不可删）** `[已实现]`：当前 BMS 默认皮肤是 **100% 程序化**的——纯色块 + 几何 + 程序化辉光，**零位图素材**（见 `BmsTemporarySkinPalette` / `BmsPlayfieldLayoutProfile`）。**没有 `skin.ini` 时就是它**。它是所有必备件的最终兜底，永远存在。
- **参考素材皮肤（创作者模板）** `[规划]`：一份能用本系统**复现内置默认观感**的 `skin.ini`。因为默认是程序化的，这份参考皮肤**绝大部分是 ini 数值（颜色/几何/开关），位图素材极少甚至为零**。它既是 P1 的验收对象，也是你制作时**最佳的起点模板**。

> **为什么不"把代码渲染对象导出成 PNG"**：内置默认是纯色块+程序化辉光。把一个 `Colour` 烤成位图会冗余、丢失可缩放性、辉光烤死不可调——纯色车道应是一个 `ColourColumn{lane}` 键，不是一张图。因此程序化辉光这类件保留为"**引擎绘制、ini 参数化**"，不烤图。这与 osu! 一致：osu! 的程序化默认皮肤（Argon）从不导出成文件。

---

## 10. 制作流程 `[规划]`

1. **复制参考皮肤**（[§9](#9-程序化默认与参考皮肤-规划)）为起点，而非从空白开始。
2. **改色 / 换图**：先动 `Colour*` 与 `*Image` 键。
3. **热重载预览**：游戏内重载，立即看效果。
4. **逐 keymode 验证**：至少覆盖你声明的每个 `Keymode`；重点检查 scratch 与键道的可读区分、14K DP 双侧布局。
5. **看诊断**：加载日志里的告警会列出被忽略/回退的键与缺失素材。
6. **校准提示**：`设置 → 游戏模式 → osu!mania → 滚动速度`显示的毫秒只代表标准几何下的参考下落时间；皮肤改了车道宽/判定线位置后体感会变，换皮后应重新校准，也不要拿它直接对照 BMS 的 Hi-Speed / 下落时间。

---

## 附录 A：游玩元素全集速查（创作者上限）

按真实生态（osu!mania `M` / beatoraja `B` / LR2 `L`）铺开的可皮肤化元素全集，作为"创作者上限"。`性质`：`S`=静态素材型 / `D`=动态引擎型。`OMS`：`✅`已有挂点 / `◐`不完备 / `✗`缺。

| 元素族 | 元素 | M | B | L | 性质 | OMS |
| --- | --- | :-: | :-: | :-: | :-: | :-: |
| Stage | 框架左/右/下、hint、backdrop | ✓ | ✓ | ✓ | S | ◐ |
| Lane | 逐道宽/间距/分隔/背景色、柱光 | ✓ | ✓ | ✓ | S/D | ◐ |
| Note | 逐道贴图/色、高度缩放、body style | ✓ | ✓ | ✓ | S | ◐ |
| Long note | 头/身/尾、持松态、HCN、保持光 | ~ | ✓ | ✓ | S/D | ◐ |
| Mine | 地雷 | — | ✓ | ✓ | S | ◐ |
| 判定线 | 线/接收区、按键常/按下态、位置 | ✓ | ✓ | ✓ | S | ◐ |
| 命中反馈 | hit lighting、bomb、keyflash、comboburst | ✓ | ✓ | ✓ | D | ✗ |
| 判定显示 | 各档图、动画、fast/slow、ghost/TD、断连色 | ~ | ✓ | ✓ | S/D | ◐ |
| Gauge | 条、类型变体、GN% 数字、历史曲线 | — | ✓ | ✓ | D | ✅ |
| 数字/文本 HUD | combo、计分、判定计数、bpm、progress、title | ~ | ✓ | ✓ | D | ◐ |
| Lane cover | SUDDEN+/HIDDEN+/LIFT、绿数 | ~ | ✓ | ✓ | D | ✅ |
| Scratch | 转盘贴图+旋转、激光 | — | ✓ | ✓ | D | ✗ |
| BGA | BGA 层、POOR 层、frame/定位 | — | ✓ | ✓ | S/D | ✅ |
| Barline | 小节线 | ✓ | ✓ | ✓ | S | ✅ |
| Character | poor/judge 立绘（风味·可选） | — | ✓ | ✓ | D | ✗ |

**明确不做**：FAST/SLOW pacemaker（产品已删）、warning arrow（mania 专属）、auto-note 变体、character 立绘（风味）。

---

## 附录 B：必备元素清单

下列为"必备"档——引擎**始终**内置兜底、皮肤可覆盖不可删。皮肤即便完全留空，这些也保证可玩：

- 车道 / playfield 本体
- 普通音符（`NoteImage{lane}`）
- 长条头 / 身（`NoteImage{lane}H` / `…L`）
- 判定线
- 判定显示（`JudgePGreat`…`JudgeEmptyPoor`）
- gauge 条（`GaugeBarImage`）
- combo

其余为推荐 / 可选（[§5](#5-静态素材族类mania-对齐-规划) / [§6](#6-bms-扩展族类bms-扩展段-规划) 各表已标注）。

---

## 附录 C：`skin.ini` 字段全表

> 完整键表随实现细化；当前以 §4（几何）/ §5（静态素材）/ §6（`[Bms]` 扩展，`F2`+）的分族表为准。单位 / 语义约定：
> - 颜色 = `r,g,b` 或 `r,g,b,a`（0–255）。
> - 比例 = `0`–`1` 浮点（如 `PlayfieldWidth` / `LongNoteBodyWidth`）。
> - 像素 = 整数（如 `HitTargetHeight` / `BarLineHeight`）。
> - 资源名 = 不带扩展名的相对路径；逐道纹理键内嵌 lane token（数字或 `S` 表 scratch）。
> - 帧动画属 ②③类引擎驱动件（`F2`+）：`name-0` / `name-1`… 帧序列。

---

## 附录 D：进阶——代码型 provider（当前实际运行路线）

> `[已实现]` **这是今天唯一真正运行的皮肤路线**，也是 ②③类引擎驱动件在素材+ini 落地前的唯一覆盖方式。素材+ini 系统上线后，代码型 provider 仍保留为"高级/精细控制"入口。

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
| `BgaPanel` | `IBmsBgaPanelDisplay` | 喂入 BGA 时间线 + 资源 store + 游玩时钟 |
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
| 代码型 provider（附录 D） | `[已实现]` | 当前唯一运行路线 |
| 程序化内置默认（兜底） | `[已实现]` | 当前默认皮肤 |
| 组件契约 + ini schema 冻结（权威源 = P1-A 约束 / PLAN） | `[已实现]` | **`F0`**（2026-06-27，纯文档） |
| 素材 + ini 加载器 / 校验器 / 热重载 / 参考皮肤（①类静态件） | `[规划]` | `F1`（含验收） |
| ②类引擎驱动件（keyflash/explosion/bomb/turntable/ghost…）补挂点 | `[规划]` | `F2` |
| ③类 `[Bms]` 扩展段独有件 + 契约冻结 | `[规划]` | `F3` |
| 完整可视化 BMS 皮肤编辑器 | `[未排期]` | 后置（决议 Y） |

### 后续追踪文档
- [../mainline/DEVELOPMENT_STATUS.md](../mainline/DEVELOPMENT_STATUS.md)：当前真实状态
- [../mainline/DEVELOPMENT_PLAN.md](../mainline/DEVELOPMENT_PLAN.md)：执行顺序与阶段依赖
- [../mainline/OMS_COPILOT.md](../mainline/OMS_COPILOT.md)：权威产品边界、fallback 纪律、release gate
- [../subline/P1-A/README.md](../subline/P1-A/README.md)：P1-A 皮肤边界子线（本规划主归属）
