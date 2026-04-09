# OMS 开发规划

> 本文档是 OMS 项目的详细开发规划，将 `OMS_COPILOT.md` 中的三个阶段拆解为可执行的开发步骤。
> 每个步骤标注前置依赖、产出文件和验收标准。
> 当前实际推进状态与遗留问题请同步维护在 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)。

---

## 总览

| 阶段 | 目标 | 步骤数 | 当前现状 |
| --- | --- | --- | --- |
| **Phase 1** | 核心 BMS — 能导入并游玩 7K+1 BMS 谱面 | 17 步 | 历史主线；12 步已完成，5 步进行中 |
| **Phase 1.1** | OMS 皮肤系统专项 — 单一默认皮肤包内集成 Global + Mania + BMS 独立 ruleset 皮肤，并移除上游原生默认皮肤 | 12 步 | 新主线；现改为按具体组件开发推进 |
| **Phase 2** | BMS 功能完善 — 全键模式、全 Mod、全 Gauge | 13 步 | 个别支撑能力已提前落地，但不视为正式进入 Phase 2 |
| **Phase 3** | 私服集成 — 在线账号、排行榜、谱面下载 | 6 步 | 维持冻结，等待离线主流程稳定 |

## 当前执行快照（2026-04-09）

- 当前主执行焦点从“单纯的 Phase 1 收口”切换为 **Phase 1.1 皮肤系统专项**：在保持 Phase 1 核心 BMS 进度不回退的前提下，后续主精力优先投入 OMS 自有默认皮肤包、mania/BMS 各自独立的 ruleset 皮肤实现，以及上游原生默认皮肤替换
- 当前已完成：1.1、1.2、1.3、1.4、1.9、1.10、1.11、1.12、1.13、1.14、1.15、1.16（共 12 步）
- 当前进行中：1.5、1.6、1.7、1.8、1.17（共 5 步）
- Phase 1.1 当前强制执行顺序已收敛为：**1.1.1-1.1.4 → 1.1.7-1.1.9 → 1.1.5-1.1.6 → 1.1.10-1.1.12**；也就是先冻结边界与宿主，再补 BMS playfield 抽象和默认层，之后才启动 mania OMS-owned 默认路径迁移，最后再做 partial override、上游默认皮肤退出和 release gate
- 当前自动推进优先级顺序：**Phase 1.1 皮肤系统专项（BMS playfield abstraction gate 与 BMS 默认层已完成收口，OMS 默认皮肤包宿主 / Global shared shell / shared transformer shell 已完成首轮实现，当前继续转向 mania OMS-owned 迁移）** → 1.17 的 analog scratch / cross-device trigger 语义 → 首发离线便携发布基线 → 1.5 / 1.6 人工验收项后置
- 需要人工操作的 1.5 真实导入/UI 验收与发行物实机验证统一记录在 `DEVELOPMENT_STATUS.md` 的独立板块，默认放在 Phase 1 阶段末尾或出现阻塞时再执行
- 当前代码规模：BMS 规则集 **124 个源文件 / 12213 行**；`oms.Input` **14 个源文件 / 1753 行**；36 个测试文件 / 6785 行；最近一次完整 `osu.Game.Rulesets.Bms.Tests` 为 **446/446** 通过，另有 `OmsHidDeviceHandlerTest` 定向回归 **11/11** 通过
- 当前皮肤基线：BMS 已完成 **Playfield / Lane / HitTarget / BarLine / Static Background Layer**、**Note / Hold / LaneCover / Judgement / Combo**、以及 **HudLayout / GaugeBar / GaugeHistoryPanel / GaugeHistory / ResultsSummaryPanel / ResultsSummary / NoteDistributionPanel / NoteDistribution / ClearLamp** 的正式 lookup / fallback 接线；当前 `SKIN/SimpleTou-Lazer` 已完成 legacy mania 侧候选基线清理与兼容化，可作为 OMS 内置皮肤候选的 mania 侧基础；BMS playfield abstraction gate 当前已接通 lane / scratch / playfield size / hit target / bar-line 的主要配置桥，layout/config gate 已基本闭合；本轮 gameplay HUD、results summary / clear lamp、results gauge history、Song Select note distribution、playfield metadata / accent surfaces（`StaticBackgroundLayer` / `LaneCover` / `HitTarget` / `BarLine`）、playfield shell surfaces（`Backdrop` / `Baseplate` / lane `Background` / `Divider`），以及 note / hold visuals（`Note` / `LongNoteHead` / `LongNoteBody` / `LongNoteTail`）已落下七批 OMS-owned 默认层切片：`ComboCounter` 默认 fallback 现已切到 `BmsComboCounter`，`HudLayout` / `GaugeBar` 使用独立 BMS HUD token，results 页的 `DefaultBmsResultsSummaryPanelDisplay` / `DefaultBmsResultsSummaryDisplay` / `DefaultBmsClearLampDisplay` 已切到独立 `BmsDefaultResultsPalette` 与 BMS-owned 统计卡片，`DefaultBmsGaugeHistoryPanelDisplay` / `DefaultBmsGaugeHistoryDisplay` 已改用 results-style panel shell 与 `BmsDefaultHudPalette` gauge colours，Song Select 侧的 `DefaultBmsNoteDistributionPanelDisplay` / `DefaultBmsNoteDistributionDisplay` 也已切到 results-style panel shell 与 BMS-owned 图表配色，`BmsDefaultPlayfieldPalette` 当前则已同时承接静态背景 metadata shell、lane cover fill / focus、hit target bar / line / glow、major / minor bar line、playfield shell，以及 note / hold 默认色；新增的 `DefaultBmsNoteDisplay` / `DefaultBmsLongNoteHeadDisplay` / `DefaultBmsLongNoteBodyDisplay` / `DefaultBmsLongNoteTailDisplay` 已让 BMS ruleset 的 no-custom-skin 默认 gameplay 表面不再依赖 `BmsTemporarySkinPalette`；而 mania 侧除既有首批 OMS shell 组件、首批 stage-local layout preset 与首批 stage-local shell behaviour preset 外，又已把 `LeftStageImage` / `RightStageImage` / `BottomStageImage` / `HitTargetImage` / `LightImage` / `KeysUnderNotes` 的 shared shell asset lookup 接到 `OmsManiaShellAssetPreset`，把 `ColumnLineColour` / `JudgementLineColour` / `ColumnBackgroundColour` / `ColumnLightColour` 的首批 shell colour lookup 接到 `OmsManiaColumnColourPreset`，把 `KeyImage` / `KeyImageDown` 的 stage-local key asset lookup 接到 `OmsManiaKeyAssetPreset`，把 `NoteImage` / `HoldNoteHeadImage` / `HoldNoteTailImage` / `HoldNoteBodyImage` 的 stage-local note/hold asset lookup 接到 `OmsManiaNoteAssetPreset`，并把 `ManiaSkinComponents.Note` / `ManiaSkinComponents.HoldNoteHead` / `ManiaSkinComponents.HoldNoteTail` / `ManiaSkinComponents.HoldNoteBody` 分别显式接到 `OmsNotePiece` / `OmsHoldNoteHeadPiece` / `OmsHoldNoteTailPiece` / `OmsHoldNoteBodyPiece`，把 `Hit300g` / `Hit300` / `Hit200` / `Hit100` / `Hit50` / `Hit0` 的 shared judgement asset lookup 接到 `OmsManiaJudgementAssetPreset`，把 `ScorePosition` / `ComboPosition` / `BarLineHeight` / `BarLineColour` 这批 non-column shared lookup 也收口为在 mixed-stage 路径固定复用第一 stage preset，并把 `SkinComponentLookup<HitResult>` 的 judgement drawable 显式接到 `OmsManiaJudgementPiece`，把 MainHUDComponents 里的 combo 显式接到 `OmsManiaComboCounter`，再把 `ExplosionImage` / `ExplosionScale` 的首个 stage-local hitburst config lookup 接到 `OmsManiaHitExplosionPreset`，同时把 `ManiaSkinComponents.HitExplosion` 显式接到 `OmsHitExplosion`；同时 `OmsNotePiece` / `OmsHoldNoteHeadPiece` / `OmsHoldNoteTailPiece` / `OmsHoldNoteBodyPiece` / `OmsManiaJudgementPiece` / `OmsHitExplosion` / `OmsManiaComboCounter` / `OmsBarLine` 已分别升级为实际 OMS-owned component implementation，其中前四类已不再继承对应 legacy piece 类型，judgement / hitburst / combo counter / bar-line 也已不再继承 `LegacyManiaJudgementPiece` / `LegacyHitExplosion` / `LegacyManiaComboCounter` / `LegacyBarLine`，因此现阶段重点转为继续清理 note/hold / combo/HUD / bar-line 的余下 legacy 语义，并把 score-driven results preview/skinnable target 留待后续评估
- normal-note / hold-note-head / hold-note-tail / hold-note-body 路径本轮又继续落下首个 explicit note component slice、首个 explicit hold-note-head component slice、首个 explicit hold-note-tail component slice与首个 explicit hold-note-body component slice：`ManiaSkinComponents.Note` / `ManiaSkinComponents.HoldNoteHead` / `ManiaSkinComponents.HoldNoteTail` / `ManiaSkinComponents.HoldNoteBody` 现已分别显式接到 `OmsNotePiece` / `OmsHoldNoteHeadPiece` / `OmsHoldNoteTailPiece` / `OmsHoldNoteBodyPiece`，`DrawableNote` / `DrawableHoldNoteHead` / `DrawableHoldNoteTail` 与 `DrawableHoldNote` 内部 `bodyPiece` 也已会在 OMS preview 路径下实际加载对应 OMS 组件；其中 `OmsNotePiece` / `OmsHoldNoteHeadPiece` / `OmsHoldNoteTailPiece` / `OmsHoldNoteBodyPiece` 已进一步升级为不再继承 `LegacyNotePiece` / `LegacyHoldNoteHeadPiece` / `LegacyHoldNoteTailPiece` / `LegacyBodyPiece` 的实际 OMS-owned component implementation，但这些路径仍继续消费既有 `NoteImage` / `HoldNoteHeadImage` / `HoldNoteTailImage` / `HoldNoteBodyImage` preset，以及 legacy note scrolling / sizing、tail inversion、`NoteBodyStyle`、`HoldNoteLightImage`、`HoldNoteLightScale` 与 hold-body light / fade / wrap-stretch 语义，因此还不等于完整 OMS-owned note / hold 默认路径。
- bar-line non-column shared 路径本轮又新增 `BarLineHeight` / `BarLineColour` slice，并继续落下首个 explicit bar-line component slice：`OmsManiaBarLinePreset` 现已在 single-stage、same-keycount dual-stage 与 mixed-stage non-column 路径下同时接管这两项 shared bar-line config，其中 mixed-stage 固定复用第一 stage preset，`OmsBarLine` 也已把 `ManiaSkinComponents.BarLine` 显式接回 OMS preview 路径；`DrawableBarLine` 现会实际加载 OMS bar line 组件并复用对应 OMS bar-line config，但该组件仍继续消费 legacy bar-line 语义，因此还不等于完整 OMS-owned bar line 默认路径。
- 1.6 最新状态：`BmsGaugeProcessor` 的 `TotalHittableObjects` / `BaseRate` 现已尊重 beatmap 当前的 long-note 结构，`CN` / `HCN` 的 scored tail 会计入 gauge 分母，`HCN` body tick 仍保持 gauge-only；long-note release-window 也已改成“score window 对齐普通命中、仅 miss grace 轻微放宽”的 judge-mode-aware 模型（`OD` 默认 `1.25`，`BEATORAJA` / `LR2` 为 `1.2`）；剩余重点转为长条边界回归与真实谱面验校
- 1.17 最新状态：已接通当前键位驱动键盘后端、完整组合键语义、gameplay `OnKeyDown()` / `OnKeyUp()` 实时键盘入口、Windows-only Raw Input 键盘源、HID 数字按钮注入入口、MouseAxis delta 注入入口、基于 `OnJoystickPress()` / `OnJoystickRelease()` 的 XInputButton 注入入口、5K/7K 默认 XInput 绑定、default keybinding 导出、joystick-only 持久化回环、基于 `RealmRulesetSetting` 的 OMS-only trigger supplemental 持久化基础，以及通用 keybinding UI 的 joystick button 展示/录入路径与基于 HidSharp 的设备枚举、按钮/轴轮询核心；`BmsSettingsSubsection` 已补上首版 variant-aware supplemental editor，并为 HID button / HID axis / mouse-axis 行提供首版 per-row live capture；同时 `Ruleset.CreateKeyBindingSections()` / `BmsRuleset.CreateKeyBindingSections()` 已把该 supplemental editor 直接接入通用 `KeyBindingPanel` 的 BMS 区块，`OmsInputRouter` 已改为 shared-action 引用计数以承接 keyboard / HID / XInput / mouse-axis 的混合激活，`BmsInputManager` 也只在全局首个 press / 最终 release 时转发 `BmsAction`，而 `MouseAxis` / `HidAxis` handler 已改成按帧/按轮询 pulse 语义，并在同一帧/同一轮询内的反向换向时先 release 再 re-press，避免快速换向被折叠成单次长按；`OmsHidDeviceHandler` 轮询层现也已补上 same-poll rapid-flip 回归，锁定单次 `PollOnce()` 内排空的 turntable axis 批变化仍会产出新的 scratch edge；同时已新增 `OmsMouseAxisInputHandler -> BmsInputManager -> KeyBindingContainer` gameplay bridge 回归、handler/router 级 same-frame same-poll direction-flip retrigger 回归、`BmsDrawableRulesetTest` scratch stream repeated-press / late-hit ordering 回归，以及 loaded headless scene `TestSceneOmsScratchGameplayBridge`，显式锁定 OMS mouse-axis / HID-axis / XInput 经 `DrawableBmsRuleset` / `BmsPlayfield` 到 scratch note settlement 的真实运行链，并补上 keyboard-held scratch 会同时压住 HID pulse、mouse pulse 与 XInput press 的 gameplay edge、直到 final release 才真正松开动作的 mixed-source scene 回归，以及 keyboard-held scratch hold 在 mouse/HID pulse 的 `FinishFrame()` / `FinishPolling()` 边界间不会断 hold、XInput 也能在 keyboard 中途释放后继续接管 hold、tail 仍沿 held path 正常结算的 loaded scene 回归；`BmsOrderedHitPolicy` 现会优先读取 `AliveObjects`，若 detached/non-pooled 测试 harness 尚未物化 alive lifetime，则回退到当前 in-use `Objects`，使 direct-drawable lane 测试与 runtime 共享同一 ordering 语义；下一步继续收口更丰富的 analog scratch 专用语义与设备体验
- Windows 上 `HidSharp.DeviceList.Local` 目前仍可能以 `RegisterClass failed` 直接终止进程；仓库现已把 HidSharp 改成 Windows 默认不触发、仅在显式设置 `OMS_ENABLE_HIDSHARP=1` 时才继续初始化，因此当前 1.17 的真实对外状态应描述为“键盘 / Raw Input / XInput / MouseAxis 主链稳定，HID 代码路径已存在但后端仍待稳定化”，设置页出现 HID-disabled 提示属于预期降级行为
- 当前 osu.Game 核心修改：~25 个文件被修改或新增，涵盖离线开关、文件系统直读、Ruleset 数据扩展、成绩分桶、Song Select 扩展与自定义加载器 6 大类别
- 当前已知断点：无阻断性问题；results auto-jump 已修复并经实机验证（2026-04-04）
- 当前已知代码质量问题：`AutoMapper` NU1903 安全漏洞（已缓解实际攻击面）；均已记入 DEVELOPMENT_STATUS.md 遗留问题

---

## 文档治理与防偏移规则

| 文档 | 职责 | 禁止内容 |
| --- | --- | --- |
| `OMS_COPILOT.md` | 权威产品约束、技术纪律、release gate、不可违背的 fallback 规则 | 不记录“仓库里已经做完多少”的流水状态 |
| `DEVELOPMENT_PLAN.md` | 未来执行顺序、阶段依赖、门槛、验收标准与强制先后手 | 不把未验证的实现写成“当前已落地” |
| `DEVELOPMENT_STATUS.md` | 只记录仓库当前真实存在并已验证的状态、断点与遗留问题 | 不写尚未落地的 aspirational 方案 |
| `README.md` | 对仓库现状的高层摘要、入口文档和当前开发重心 | 不代替详细计划或状态矩阵 |
| `SKINNING.md` | 面向玩家与皮肤设计师的当前皮肤契约、fallback 和未冻结边界 | 不把未来资源命名或未落地能力写成稳定契约 |
| `RELEASE.md` | 公开发行方式、打包约束与公开 release gate | 不把开发态候选包描述成已经可公开发行的默认体验 |
| `CHANGELOG.md` | 已发生且已验证的变更摘要，按日期倒序 | 不记录纯规划但未执行的内容 |

**同步规则：**

1. 只要 Phase 1.1 的执行顺序、门槛、候选包语义或 release gate 发生变化，必须在同一次工作里同步更新 `OMS_COPILOT.md`、`DEVELOPMENT_PLAN.md`、`DEVELOPMENT_STATUS.md`、`README.md`、`SKINNING.md`、`RELEASE.md`
2. `README.md` 与 `DEVELOPMENT_STATUS.md` 不得把某个组件写成“OMS 内置皮肤已完成”，除非它在 no-custom-skin 路径下已经走 OMS-owned 默认实现，并至少有一次构建、测试或明确手动验证
3. `SKIN/SimpleTou-Lazer` 当前只能被描述为“OMS 内置皮肤候选基线”或“mania 侧基础/视觉参考”，不得被提前写成“已经完成的 OMS 默认皮肤”
4. 如果实现与规划发生分叉，先修正文档或代码其中一边，再继续开发；不要允许 README、计划、状态三者长期并行讲不同故事

---

## 当前版本通用约束

### 发行与更新基线

当前从 Phase 1 到 Phase 2 的 Windows 首发策略，统一按“离线便携发布 + 手工覆盖更新”设计，不提前恢复在线安装或在线更新。

**最小改造清单：**

1. 首发正式发行物只提供 Portable.zip 这类全量便携包，不把 `Setup.exe` / `MSI` / delta 包作为当前阶段的正式用户路径
2. 从第一版发布到逐步实现联网功能前，版本升级统一采用“解压后文件覆盖”流程，不依赖安装器自更新、后台下载或增量补丁
3. 程序目录与用户数据目录布局必须对覆盖更新友好；覆盖新版本时不应要求重新导入谱面，也不应依赖在线迁移逻辑
4. 游戏内在线更新暂时禁用，相关入口也一并隐藏；至少包括自动检查更新、手动检查更新、`ReleaseStream` 切换和其他面向终端用户的更新选项
5. 所有联网功能（在线更新、账号、排行榜、谱面下载、远程难度表源）统一延后到 Phase 3 再重新评估是否启用

### 联网功能冻结基线

当前对终端用户发布的 Phase 1-2 构建，一律按“彻底本地化优先”处理。

1. 不向终端构建写入默认可用的 production API、OAuth、SignalR、BSS 或其他远端服务器地址；在 Phase 3 真正启动前，这些地址应为空、未配置，或显式指向本地 stub / disabled path
2. 游戏内所有依赖联网的用户入口默认隐藏或禁用；至少包括账号登录、在线排行榜 scope、成绩上传、谱面搜索/下载、新闻、聊天、多人、观战、Daily Challenge 与其他社交/在线面板
3. 远程难度表源、自定义 URL 刷新与服务端镜像一律后移到 Phase 3；Phase 1-2 若需要表数据，只允许本地缓存、本地导入或随发行物携带的离线镜像
4. 头像、勋章、预览音频、远程 metadata cache 等上游静态资源 fallback，不应作为当前本地化版本的默认行为；要么提供本地 fallback，要么显式显示“离线不可用”

### BMS 文件落盘基线

1. BMS 包导入后，解压目录直接进入 OMS songs 目录作为最终存储
2. 运行时从 OMS songs 目录直接读取 `.bms/.bme/.bml/.pms` 与关联音频/BMP 资源
3. BMS 谱面正文与关联资源不进入 lazer 现有 `files/` 哈希文件仓库；数据库只保存索引、元数据和定位信息
4. 覆盖更新不得改写、重打包或重新哈希用户自己的 BMS 目录内容

### 皮肤系统替换基线

1. OMS 继续使用 osu!lazer 现有 `ISkin` / `SkinTransformer` / `SkinnableDrawable` 体系，但正式产品默认皮肤必须迁移到 **OMS 自有内置皮肤**，而不是沿用上游默认皮肤作为对外产品表面
2. mania 与 BMS 同属一个 OMS 默认皮肤包/选择项，但二者的 gameplay 资源、布局、命名与语义默认彼此独立；用户自定义皮肤缺失的组件也应逐项回落到对应 ruleset 的 OMS 内置组件，而不是回落到 Argon / Triangles / Legacy / Retro
3. 在 Phase 1.1 完成前，代码中允许存在过渡期直绘 placeholder 与“皮肤无法正常加载时的 feedback 层”；但这层仅用于失败反馈，不等同于未来 OMS built-in skin 设计，一旦对应 Phase 1.1 子步骤完成，该组件就不应再以硬编码 `Box` / 颜色作为正式 release fallback
4. BMS 的 scratch lane、lane cover、clear lamp、gauge bar、note distribution、BAD/POOR/EMPTY POOR 判定命名属于 OMS 产品语义的一部分，不得在皮肤迁移过程中被上游默认皮肤语义反向覆盖
5. “移除当前所有 osu!lazer 原生默认皮肤”在本规划中的含义是：从 OMS 的最终对外默认体验、默认选择入口和正式发行包中移除其产品地位；测试桩、过渡期兼容代码与未公开开发态残留不构成完成条件

---

## Phase 1 — 核心 BMS

### 1.1 上游清理与项目脚手架

**目标：** 移除三个无用规则集，确认项目可编译运行。

**步骤：**

1. 删除 `osu.Game.Rulesets.Osu`、`osu.Game.Rulesets.Taiko`、`osu.Game.Rulesets.Catch` 项目及所有引用
2. 在解决方案中清理对应的 `.csproj` 引用和反射注册代码
3. 移除 osu.Game 中对已删除模式的硬编码引用（测试场景、默认规则集列表等）
4. 创建 `osu.Game.Rulesets.Bms` 项目骨架（.csproj + BmsRuleset.cs 入口）
5. 创建 `oms.Input` 项目骨架（.csproj + OmsAction 枚举 + OmsInputRouter 空壳）
6. 创建 `oms.Desktop` 入口项目（如尚不存在）
7. 添加 NuGet 依赖：`SharpCompress`、`Ude.NetStandard`、`HidSharp`

**前置依赖：** 无
**验收：** `dotnet build` 通过，启动后进入 osu!mania Song Select（无 BMS 内容）。

---

### 1.2 BMS 数据模型

**目标：** 定义 BMS 规则集的核心数据结构。

**产出文件：**

- `BmsBeatmapInfo.cs` — Keymode、MeasureLengthControlPoints、#TOTAL、#RANK 等 BMS 专属元数据
- `BmsHitObject.cs` — 单键音符（LaneIndex, KeysoundId, IsScratch, AutoPlay）
- `BmsHoldNote.cs` — 长条音符（StartTime, Duration, HeadKeysoundId, TailKeysoundId）
- `BmsBgmEvent.cs` — 非可判定 BGM 事件
- `BmsKeymode.cs` — 枚举：Key5K, Key7K, Key9K_Bms, Key9K_Pms, Key14K
- `BmsMeasureLengthControlPoint.cs` — record(int MeasureIndex, double Multiplier)

**前置依赖：** 1.1
**验收：** 所有数据模型编译通过，无运行时使用。

---

### 1.3 BMS 文件解析器 (`BmsBeatmapDecoder`)

**目标：** 将 `.bms`/`.bme`/`.bml`/`.pms` 解析为内存中间表示。

**实现要点：**

1. 编码检测（Ude）→ Shift-JIS / UTF-8 自动切换
2. 头字段解析（#TITLE, #SUBTITLE, #ARTIST, #GENRE, #BPM, #PLAYLEVEL, #DIFFICULTY, #RANK, #TOTAL, #STAGEFILE, #BANNER, #BACKBMP）
3. 索引表解析（#WAV##, #BMP##, #BPM##, #STOP##）
4. LN 声明解析（#LNOBJ, #LNTYPE）
5. #RANDOM / #IF / #ENDIF：解析所有分支，执行 `#IF 1` 块，记录警告
6. 通道解析（#MMMCC:data）— base-36 对象切分
7. Channel 02 特殊处理（十进制浮点数，非 base-36）
8. Channel 03/08 BPM 变化解析
9. Channel 09 STOP 解析
10. Channel 01 BGM、11–19/21–29 可打击、51–59/61–69 LN 通道
11. LNOBJ 逆向绑定（尾判标记前一个音符为 LN 头）
12. LNTYPE 1 通道对解析（5x/6x 头尾配对）
13. 键模式自动检测（6 条有序规则）
14. 输出：填充 BmsBeatmapInfo + 原始通道事件列表

**前置依赖：** 1.2
**验收：** 单元测试覆盖——至少包含以下测试用例：

- 基础 7K 谱面完整解析
- Shift-JIS 编码文件正确读取
- Channel 02 浮点值 0.75 解析
- #LNOBJ 长条配对
- #LNTYPE 1 长条配对
- BPM 变速 (Channel 03 + 08)
- STOP (Channel 09)
- 键模式检测（5K、7K、14K、PMS 分别验证）
- #RANDOM 块跳过并输出警告

---

### 1.4 谱面转换器 (`BmsBeatmapConverter`)

**目标：** 将解析后的 BMS 原始数据转换为 osu-framework 可执行的 `IBeatmap`。

**实现要点：**

1. 绝对时间计算引擎——按小节累加 `measure_start_ms`，处理 Channel 02 倍率和 BPM 变化
2. ControlPointInfo 填充——Initial BPM → Channel 03/08 BPM → Channel 09 STOP（合成极大 BPM 冻结 + 恢复）
3. MeasureLengthControlPoints 填充
4. HitObject 生成——单键 → BmsHitObject，LN → BmsHoldNote，BGM → BmsBgmEvent
5. 排序保证——HitObjects 按 StartTime 升序，ControlPointInfo 按时间升序无重叠
6. 输出契约校验

**前置依赖：** 1.3
**验收：** 单元测试——验证已知 BMS 文件转换后的 HitObject 时间精度（±1ms 容差）、ControlPoint 数量、STOP 时间窗口正确性。

---

### 1.5 归档导入 (`BmsArchiveReader`)

**目标：** 支持 .zip/.rar/.7z 归档拖放导入，在 OMS songs 目录下直接保留 BMS 文件结构。

**实现要点：**

1. SharpCompress 解压到临时目录
2. 扫描 .bms/.bme/.bml/.pms 文件
3. 每个文件调用 BmsBeatmapDecoder，收集成功/失败
4. 同文件夹文件组为一个 BeatmapSet（不按键模式拆分）
5. 计算每个文件 MD5 哈希，写入 BeatmapInfo.Hash
6. 解析失败处理（部分成功警告/全部失败错误通知）
7. 移动到 OMS songs 目录，清理临时文件，并把该目录作为 BMS 的最终来源路径
8. 向 osu! BeatmapManager 注册可重载的元数据与定位信息，而不是把 BMS 正文和资源复制到现有 `files/` 哈希文件仓库
9. 运行时 loader 从 OMS songs 目录直接读取 `.bms/.bme/.bml/.pms` 及其关联资源

**前置依赖：** 1.3, 1.4
**验收：** 手动测试——拖放一个 7K BMS zip 包，Song Select 中出现对应谱面集，每个难度显示键模式标签，且解压后的原始文件夹保留在 OMS songs 目录并可被运行时直接重载。

---

### 1.6 键音系统 (`BmsKeysoundStore`)

**目标：** 按 base-36 索引管理每张谱面最多 1295 个音频采样，支持并发播放。

**实现要点：**

1. #WAV## 索引构建（base-36 → 音频文件路径）
2. 格式降级查找（精确文件名 → .wav → .ogg → .mp3，替代时记录警告）
3. ManagedBass 懒加载 + 会话内缓存
4. 并发通道上限由 `BmsRulesetConfigManager.KeysoundConcurrentChannels` 控制
5. 缺失键音：记录警告，播放静音
6. BGM 通道（Channel 01）事件队列：按时间自动触发
7. 可打击音符键音：由 gameplay 层在判定时调用触发

**前置依赖：** 1.2, 1.5（需要导入后的文件结构）
**验收：** 导入一个有键音的 BMS 包 → 在测试场景播放 BGM 通道 → 听到正确音频序列。缺失文件不崩溃。

---

### 1.7 BMS 规则集入口 (`BmsRuleset`)

**目标：** 将 BMS 注册为 osu-framework 可发现的规则集，串联解析→转换→gameplay 管线。

**实现要点：**

1. BmsRuleset 继承 Ruleset，注册 RulesetInfo
2. CreateBeatmapConverter → BmsBeatmapConverter
3. CreateDifficultyCalculator → BmsDifficultyCalculator（先返回 stub 值，1.12 完善）
4. GetModsFor → 返回 Phase 1 的 Mod 列表（Lane Cover Top/Bottom）
5. BmsRulesetConfigManager 初始化

**前置依赖：** 1.4
**验收：** 启动 OMS → 模式选择器中出现 BMS 模式图标 → 可切换到 BMS。

---

### 1.8 7K+1 Playfield (`BmsPlayfield` + `BmsLaneLayout`)

**目标：** 渲染 7K+1 (1P) 布局的 BMS 游玩界面。

**实现要点：**

1. BmsLaneLayout — 8 车道（1 scratch + 7 key），宽度/颜色/位置定义
2. BmsPlayfield — 继承 ScrollingPlayfield，注册车道
3. BmsScratchLane — scratch 车道渲染（加宽）
4. 小节线渲染——读取 MeasureLengthControlPoints 计算位置
5. BmsBackgroundLayer — 预留渲染槽 + 静态 #STAGEFILE 显示
6. 音符 Drawable 绑定——BmsHitObject/BmsHoldNote → 车道内可视元素
7. 滚动速度——继承 mania 的 scroll speed 系统

**前置依赖：** 1.7, 1.6
**验收：** 选择已导入的 7K BMS 谱面 → 进入 gameplay → 音符从上/下滚动，scratch 车道在最左（1P），BGM 键音自动播放。

---

### 1.9 OD 判定系统 (`OsuOdJudgementSystem`)

**目标：** 实现默认判定——基于 #RANK → OD 的 osu!mania 时间窗口。

**实现要点：**

1. BmsJudgementSystem 抽象基类（Evaluate + Windows）
2. OsuOdJudgementSystem 具体实现——#RANK → OD 映射 → 时间窗口
3. BmsTimingWindows — 存储 PGREAT/GREAT/GOOD/BAD/POOR 以及 long-note end 窗口值
4. 引入 `BmsLongNoteMode` 抽象（默认 LN；后续扩展 CN/HCN），保证 head / tail 判定点可由同一套判定系统驱动
5. Gameplay 集成——HitObject 判定回调接入 BmsJudgementSystem.Evaluate
6. 键音触发规则接入——PGREAT/GREAT/GOOD/BAD 播放键音，POOR 不播放

**前置依赖：** 1.8
**验收：** 单元测试——#RANK 2 时各窗口值与 osu!mania OD 7 一致。Gameplay 中按键得到判定文字反馈。

---

### 1.10 Normal Gauge (`BmsGaugeProcessor`)

**目标：** 实现 NORMAL Gauge（默认），包含 #TOTAL 驱动的回复/伤害率。

**实现要点：**

1. BmsGaugeProcessor 基类架构（当前值、base_rate 计算、Apply 方法）
2. Normal Gauge 具体参数——起始 20%，回复 base_rate×0.8，伤害 base_rate×8.0
3. 全判定回复/伤害倍率实现（PGREAT 1.0×回复、GREAT 1.0×、GOOD 0.5× 回复 + 0 伤害、BAD 1.0×伤害、POOR 1.5×、Empty POOR 1.0×）
4. 2% 生存底线
5. 结算条件——≥80% 为 NORMAL CLEAR
6. 为后续 `CN` / `HCN` 预留长条模式输入契约：`LN` 只消费头判，`CN` / `HCN` 额外消费尾判，`HCN` body tick 走时间驱动的独立 gauge 事件流
7. Gauge 条 UI 渲染

**前置依赖：** 1.9
**验收：** 单元测试——给定 #TOTAL=200、1000 音符，验证 base_rate 和各判定的精确 gauge 变化。Gameplay 中 gauge 条实时变化。

---

### 1.11 EX-SCORE 与结算 (`BmsScoreProcessor` + `BmsClearLampProcessor` + `BmsDjLevelCalculator`)

**目标：** 完成 BMS 计分管线和结算画面。

**实现要点：**

1. BmsScoreProcessor — PGREAT×2 + GREAT×1 EX-SCORE；追踪各判定计数、MAX COMBO，与 active `BmsLongNoteMode`
2. MaxExScore / EX% 分母按长条模式计算：`LN` 只计头，`CN` / `HCN` 计头+尾，`HCN` body tick 不进入 EX-SCORE
3. Combo 规则——PGREAT/GREAT/GOOD 续连，BAD/POOR/Empty POOR 断连；FULL COMBO / PERFECT 资格按 active `BmsLongNoteMode` 的 scored points 计算
4. BmsClearLampProcessor — 灯级层次（NO PLAY → ... → PERFECT），仅升不降
5. BmsDjLevelCalculator — EX% → AAA/AA/.../F（8/9、7/9、6/9 边界）
6. 本地 best / replay / 结算模型从一开始就持久化 `BmsLongNoteMode`，避免后续加入 `CN` / `HCN` 时混算成绩
7. 结算画面集成——显示 EX-SCORE、判定分布、Clear Lamp、DJ Level、gauge 图表、长条模式标签

**前置依赖：** 1.10
**验收：** 打完一首谱面 → 结算画面正确显示所有数据。单元测试覆盖 DJ Level 边界值和 Clear Lamp 升级逻辑。

---

### 1.12 密度星级 (`BmsNoteDensityAnalyzer` + `BmsDifficultyCalculator`)

**目标：** 基于加权音符密度计算 0–20 星级。

**实现要点：**

1. BmsNoteDensityAnalyzer — 滑动窗口分析（1000ms 窗口，500ms 步进）
2. 权重规则——基础 1.0，和弦 +0.3/额外音符（≤1ms 容差），scratch +0.5，LN +0.1/100ms
3. GetPercentileDensity — 95th 百分位
4. BmsDifficultyCalculator — 调用 Analyzer，7K 标准化常数，映射到 0–20 星
5. BmsDifficultyAttributes — StarRating, TotalNoteCount, ScratchNoteCount, LnNoteCount, PeakDensityNps, PeakDensityMs
6. 替换 1.7 中的 stub 返回值

**前置依赖：** 1.4
**验收：** 单元测试——已知音符分布的合成谱面，验证 bucket 计数、百分位值和最终星级在预期范围内。

---

### 1.13 难度表本地预置与缓存 (`BmsDifficultyTableManager`)

**目标：** 在 Phase 1-2 先实现离线难度表管理，不直接联网拉取社区表。

**实现要点：**

1. `bms_table_presets.json` 资源文件——记录内置离线镜像或导入模板，而不是当前阶段直接请求远端 URL
2. SQLite 来源表——source_name, local_path, display_name, is_preset, enabled, imported_at, last_refreshed
3. 支持从本地 `header.json` / `body.json` / 离线镜像目录导入
4. RefreshAllTables / RefreshTable 仅重读本地文件与缓存
5. TableDataChanged 事件
6. 管理 UI（启用/禁用切换、手动重读、导入本地镜像、移除来源）
7. 自定义 URL、在线刷新与服务端镜像回退统一延后到 Phase 3

**前置依赖：** 1.1
**验收：** 导入本地 Satellite 镜像 / 导出文件 → 本地缓存写入 → 重启后无需网络即可读取缓存。

---

### 1.14 MD5 匹配管线 (`BmsTableMd5Index`)

**目标：** 导入时自动匹配难度表等级，表刷新时批量更新。

**实现要点：**

1. 导入时——BmsArchiveReader 计算 MD5 → 查询内存索引 → 写入 BmsTableLevel
2. 表刷新时——批量 SQL IN 查询 → 更新 DB → 重建内存索引 → 发出事件
3. 内存结构 `Dictionary<string, List<BmsDifficultyTableEntry>>`
4. 启动时从 DB 缓存重建（不需要网络）

**前置依赖：** 1.5, 1.13
**验收：** 导入一个在本地 Satellite 表缓存中存在的 BMS 包 → 谱面元数据自动标记表等级。手动重读本地表缓存 → 新匹配自动生效。

---

### 1.15 Song Select 表分组 (`BmsTableGroupMode`)

**目标：** BMS 模式下 Song Select 支持按难度表→等级的层级分组。

**实现要点：**

1. BmsTableGroupMode 实现 GroupDefinition
2. 分组层级：表名 → 等级 → BeatmapSet → 难度
3. "Unrated" 分组放在最后
4. 多表谱面在每个表的分组下独立出现
5. 分组激活时禁用排序下拉，内部固定按密度星级升序
6. 注册到 BMS 规则集的 Song Select 配置

**前置依赖：** 1.14, 1.12
**验收：** BMS 模式 Song Select 中选择表分组 → 看到 Satellite/Stella 等分组 → 展开看到 ★1/★2 等级 → 内部谱面按星级排列。

---

### 1.16 音符分布图 (`BmsNoteDistributionGraph`)

**目标：** Song Select 右侧面板显示谱面音符密度预览。

**实现要点：**

1. 调用 BmsNoteDensityAnalyzer（windowMs=1000, stepMs=1000）
2. 读取 BmsDifficultyAttributes 中的统计数据
3. 白色（普通）/ 红色（scratch）/ 蓝色（LN）堆叠柱状图
4. 统计文字：总音符、scratch 占比、LN 占比、峰值密度
5. 后台任务计算 → Schedule() 推送到 UI 线程
6. BmsNoteDistribution SkinComponentLookup 注册
7. 选中谱面缓存，切换清除

**前置依赖：** 1.12, 1.8
**验收：** BMS Song Select 选中谱面 → 右侧出现分布图，数据与实际音符一致。

---

### 1.17 基础输入绑定与 Lane Cover

**目标：** 键盘输入绑定可用、HID 输入后端稳定可用，Lane Cover Mod 可用。

**实现要点：**

*输入：*

1. OmsAction 枚举完整定义（1P/2P 7K+1, 9K, UI 动作）
2. OmsBindingStore — 键盘按键 → OmsAction 的持久化映射
3. RawKeyboardHandler — Windows Raw Input 键盘捕获（已补；`WindowsRawKeyboardSource` 现通过 `IOmsKeyboardEventSource -> IOmsKeyboardEventSink` 把 `WM_INPUT` 原生键盘事件送入 `BmsInputManager`）
4. HidDeviceHandler — HID 设备枚举、按钮读取、映射（当前代码路径基于 HidSharp；Windows 默认构建因 `RegisterClass failed` 崩溃风险已关闭该后端，待稳定化或替换）
5. XInputButtonHandler — joystick/gamepad button 捕获与映射
6. OmsInputRouter — 路由信号到 gameplay
7. 绑定 UI — 现已复用现有 keybinding UI 录入 keyboard/joystick button，并在 `BmsSettingsSubsection` 提供首版 variant-aware supplemental editor；其中 HID button / HID axis / mouse-axis 已补上 per-row live capture，supplemental editor 也已整合进通用 keybinding 面板；当 Windows 默认禁用 HidSharp 时，设置页会明确提示这是防崩溃降级而非设置面板故障，后续主要剩信号类型图标、更细的跨设备语义与稳定 HID backend

*Lane Cover：*

1. BmsLaneCover — 不透明遮挡层 Drawable
2. BmsModLaneCoverTop — 顶部遮挡，CoverPercent (0–100%)
3. BmsModLaneCoverBottom — 底部遮挡，CoverPercent (0–100%)
4. 游戏内滚轮调节（默认调 Top；按住 UI_LaneCoverFocus 调 Bottom）

**前置依赖：** 1.8
**验收：** 键盘绑定后能正常打 7K BMS 谱面。Windows 默认构建不得再因 HID 设备加载而闪退；在稳定 HID backend 上，HID 控制器接入后按钮动作正确。Lane Cover 显示并可滚轮调节。

---

### Phase 1 完成里程碑

**达成条件：**

- [ ] 可导入 .zip/.rar/.7z BMS 归档（核心导入链已接通，仍缺真实 UI 手工验收）
- [ ] 7K+1 谱面可完整游玩（音符、键音、判定、gauge、计分）
- [x] 结算画面显示 EX-SCORE、Clear Lamp、DJ Level（数据管线已接通，results auto-jump 已修复并经实机验证）
- [x] 本地难度表缓存/MD5 匹配/表分组可用
- [x] 音符分布图在 Song Select 显示
- [ ] 键盘 + 稳定 HID 基础输入可用
- [x] Lane Cover 可用

---

## Phase 1.1 — OMS 皮肤系统专项

> 本专项是新的**产品主线**，并行承接既有 Phase 1 收口。为避免与既有 `1.1`~`1.17` 的历史核心 BMS 编号冲突，下列皮肤任务统一使用 `1.1.x` 编号。
> **语义澄清：** OMS 的目标是提供**一个默认皮肤包 / 一个默认皮肤选择项**，其中同时集成 Global、mania、BMS 三层内容；但 mania 与 BMS 的 gameplay 皮肤本体彼此独立，不要求共用同一套 note / judgement / lane / HUD 语义或素材。

### 当前执行策略（强制）

1. 当前默认皮肤候选基线是 `SKIN/SimpleTou-Lazer`；它目前只能被定义为 OMS 内置皮肤候选的 mania 侧基础与视觉参考，不得被描述成“已经完成的 OMS 默认皮肤包”
2. Phase 1.1 明确**不采用 mania/BMS 双线并行美术复刻**；当前强制执行顺序为 **1.1.1-1.1.4 → 1.1.7-1.1.9 → 1.1.5-1.1.6 → 1.1.10-1.1.12**
3. 原因不是 mania 不重要，而是 BMS 虽已有大量 drawable lookup，但 `BmsLaneLayout`、`BmsPlayfield`、`BmsHitTarget` 等仍缺少配置驱动的 playfield 几何层；如果不先补这层，mania 侧设计语言无法稳定、低成本地复刻到 BMS playfield
4. 因此 1.1.7-1.1.9 的首要目标不是“先把 BMS 美术画完”，而是先完成 **BMS playfield abstraction gate**：lane geometry、scratch 宽度/间距、hit target / receptor 状态、bar line 参数、playfield adjustment/scaling 等 layout-critical 能力
5. 只有在 BMS abstraction gate 达成后，才允许把当前候选视觉语言正式移植到 BMS 默认层，并进一步启动 mania OMS-owned 默认路径迁移

### Phase 1.1 执行门槛

| 门槛 | 关联步骤 | 出口条件 |
| --- | --- | --- |
| A. 候选包边界门槛 | 1.1.1 ~ 1.1.3 | 默认皮肤包分层、lookup 矩阵、资源命名与候选包语义冻结；文档口径一致 |
| B. 宿主骨架门槛 | 1.1.4 | 运行时存在明确的 OMS built-in skin provider / shared shell / resource root，而不是只靠 scattered fallback 类拼装 |
| C. BMS 抽象门槛 | 1.1.7 ~ 1.1.8 | BMS playfield 不再只依赖硬编码 geometry；lane / scratch / receptor / bar line / lane cover 等 layout-critical 参数可通过 OMS-owned 契约表达 |
| D. BMS 默认层门槛 | 1.1.9 | 无外部皮肤时，BMS gameplay HUD / results / Song Select details 走 OMS-owned 默认层，并能承接当前候选视觉语言 |
| E. mania + release 门槛 | 1.1.5、1.1.6、1.1.10 ~ 1.1.12 | mania 默认路径迁移完成，partial override 语义稳定，上游默认皮肤退出产品表面，公开发行 gate 达成 |

### 1.1.1 默认皮肤包边界、ruleset 分层与 fallback 链冻结

**目标：** 冻结 OMS 默认皮肤包的产品边界，先把“一个皮肤包里有哪些层、各层的职责是什么”定死。

**实现要点：**

1. 明确 OMS 默认皮肤包由 `Global`、`Mania`、`BMS` 三层组成，对外只表现为一个默认皮肤选择项
2. 明确 `Global` 只负责共享基础设施、shared HUD 外壳、通用排版/图标/布局元信息，不负责强迫 mania 与 BMS 共用 gameplay 语义
3. 明确 mania 与 BMS 的 gameplay 资源、命名、布局、判定图层和特殊语义彼此独立，默认允许完全不同的视觉方案
4. 定义 fallback 顺序：用户皮肤 → ruleset transformer → 对应 ruleset 的 OMS 默认皮肤层 → 仅开发期 feedback/placeholder
5. 明确 beatmap skin / 用户皮肤只属于覆盖层，不能成为 OMS 默认可用体验的前置依赖

**前置依赖：** 无
**验收：** `OMS_COPILOT.md`、`DEVELOPMENT_PLAN.md`、`DEVELOPMENT_STATUS.md`、`README.md`、`RELEASE.md` 对“一个默认皮肤包内集成 Global + Mania + BMS 独立皮肤层”的描述一致。

---

### 1.1.2 组件清单、代码映射与 lookup 矩阵

**目标：** 不再空谈“皮肤系统”，而是把要开发的部件逐项列出来，并映射到现有代码落点。

**实现要点：**

1. 列出 `Global` 组件：MainHUD 容器、results summary 容器、通用 digits/text/icon、layout json / serialisable layout 约束
2. 列出 mania 组件并映射代码：`Stage`、`Column`、`ColumnFlow`、`ColumnHitObjectArea`、`DrawableNote`、`DrawableHoldNote`、`DrawableBarLine`、`PoolableHitExplosion`、judgement/combo/HUD 入口
3. 列出 BMS 组件并映射代码：`BmsPlayfield`、`BmsLane`、`BmsScratchLane`、`BmsHitTarget`、`BmsScratchHitTarget`、`DrawableBmsHitObject`、`DrawableBmsBarLine`、`BmsLaneCover`、`BmsBackgroundLayer`、`BmsGaugeBar`、`BmsGaugeHistoryGraph`、`BmsNoteDistributionGraph`、`BmsJudgementPiece`
4. 为每个组件指定目标 lookup 类型、默认实现类、布局来源、是否允许用户皮肤 partial override
5. 标出当前仍是 feedback 直绘层、当前已接入 lookup、当前仍依赖 upstream 默认皮肤的组件状态

**前置依赖：** 1.1.1
**验收：** 形成一份可以直接驱动开发的组件矩阵，而不是只有“共享视觉契约”这类抽象描述。

---

### 1.1.3 资源目录、命名规范与配置桥接

**目标：** 确定素材型皮肤开发的实际契约，明确哪些走 legacy `skin.ini`，哪些走 OMS 自己的 lookup / layout 约定。

**实现要点：**

1. 约定默认皮肤包目录分层：`Global`、`Mania`、`BMS` 的资源、样本、布局/配置如何组织
2. mania 继续兼容 legacy mania 命名与 `[Mania]` 配置桥接，例如 `mania-stage-*`、`mania-key*`、`mania-note*`、`mania-hit*`、`HitPosition`、`ColumnWidth` 等
3. BMS 定义自己的资源命名与 lookup 契约，不强行复用 mania 的 note/stage 命名；即使当前候选视觉语言来自 `SimpleTou-Lazer`，BMS 的 playfield geometry、布局与状态信息也必须走 OMS-owned lookup / layout 配置，而不是直接照搬 legacy mania 配置名
4. 统一说明 `@2x`、帧动画命名、样本命名、layout json、serialisable drawable anchor/size 约束
5. 明确一个用户皮肤可以同时包含 mania 与 BMS 资源，也可以只覆盖其中一边；缺失的一边必须回落到 OMS 默认皮肤包对应层

**前置依赖：** 1.1.1, 1.1.2
**验收：** 设计师可以据此判断“某个 mania/BMS 组件应该用什么名字、什么配置、放在哪个目录”，而不是只能读代码猜测。

---

### 1.1.4 Global provider、shared shell 与默认皮肤包骨架

**目标：** 先把默认皮肤包的宿主和 shared shell 搭起来，再往里填 ruleset 组件。

**实现要点：**

1. 建立 OMS built-in skin provider / resource root / shared transformer 基础设施
2. 建立 `Global` 层默认资源与 shared shell：MainHUD 容器、results summary 容器、shared digits/text/icon/layout metadata
3. 确保 mania 与 BMS 都能通过同一个默认皮肤包入口拿到各自的 ruleset 子层
4. 保留当前 feedback 层作为最终 safety net，但它不得再充当正式默认皮肤实现

**前置依赖：** 1.1.1, 1.1.3
**验收：** 运行时存在清晰的 OMS 默认皮肤包骨架，而不是只靠 ruleset 里 scattered fallback 类拼装。

**当前进展（2026-04-08）：** `OmsSkin` preview 选择项、`SkinManager` 注册 / 枚举 / 配置入口，以及基于嵌入 `SKIN/SimpleTou-Lazer` 的 built-in resource root 已落地；mania 现有 non-column shared preset 的 mixed-stage fallback、Global shared shell、shared transformer shell，以及基于 `MainHUDComponents.json` / `SongSelect.json` / `Playfield.json` 的 `Global` layout metadata 现也都已收口并补上 regression；本轮又把 results-style shared panel shell 从 `DefaultResultsPanelContainer` 进一步抬升为 `DefaultResultsPanelDisplay<TState>` 这层 core stateful contract，当前 1.1.4 剩余 gap 收窄为决定 score-driven results 是否需要独立的 preview/skinnable target。

---

### 1.1.5 mania 第一批：Stage / Column / Key 区与配置桥接

> **执行门槛：** 当前这一步不再作为 Phase 1.1 的第一执行批；只有在 1.1.7-1.1.9 的 BMS abstraction / default-layer gate 达成后，才进入 mania 侧正式视觉迁移。

**目标：** 先把 mania 的舞台壳和列布局拉到 OMS 默认皮肤路径上。

**实现要点：**

1. 迁移 `Stage`、`Column`、`ColumnFlow`、`ColumnHitObjectArea` 的默认资源路径，确保不再以 upstream 默认皮肤作为最终产品实现
2. 明确并落地 stage background / foreground / hint / light、column background / line、key idle / pressed、hit target 的 OMS 默认实现
3. 保持 `HitPosition`、`ColumnWidth`、`ColumnSpacing`、`StagePadding` 等 mania 配置桥接能力
4. 让 4K / 5K / 7K / 9K / 14K / 18K 等 variant 仍能通过同一 mania 子层派生，而不是拆成多个产品皮肤

**前置依赖：** 1.1.3, 1.1.4
**验收：** mania 舞台壳、列背景、key 区和 hit target 在无外部皮肤时已有 OMS-owned 默认路径。

**当前进展（2026-04-09）：** `OmsSkin` 现已在 `ManiaRuleset.CreateSkinTransformer()` 中拥有显式 `ManiaOmsSkinTransformer` 入口，并把 `StageBackground` / `StageForeground` / `ColumnBackground` / `KeyArea` / `HitTarget` 切到首批 OMS shell 组件；已把 `HitPosition` / `StagePadding` / `ColumnWidth` / `ColumnSpacing` 的 stage-local layout lookup 接到首批 OMS preset，把 `LeftLineWidth` / `RightLineWidth` / `ShowJudgementLine` / `LightPosition` / `LightFramePerSecond` 的 stage-local shell behaviour lookup 接到 `OmsManiaShellPreset`，把 `LeftStageImage` / `RightStageImage` / `BottomStageImage` / `HitTargetImage` / `LightImage` / `KeysUnderNotes` 的 shared shell asset lookup 接到 `OmsManiaShellAssetPreset`，把 `ColumnLineColour` / `JudgementLineColour` / `ColumnBackgroundColour` / `ColumnLightColour` 的首批 shell colour lookup 接到 `OmsManiaColumnColourPreset`，把 `KeyImage` / `KeyImageDown` 的 stage-local key asset lookup 接到 `OmsManiaKeyAssetPreset`，把 `NoteImage` / `HoldNoteHeadImage` / `HoldNoteTailImage` / `HoldNoteBodyImage` 的 stage-local note/hold asset lookup 接到 `OmsManiaNoteAssetPreset`，把 `ManiaSkinComponents.Note` / `ManiaSkinComponents.HoldNoteHead` / `ManiaSkinComponents.HoldNoteTail` / `ManiaSkinComponents.HoldNoteBody` 分别显式接到 `OmsNotePiece` / `OmsHoldNoteHeadPiece` / `OmsHoldNoteTailPiece` / `OmsHoldNoteBodyPiece`，把 `Hit300g` / `Hit300` / `Hit200` / `Hit100` / `Hit50` / `Hit0` 的 shared judgement asset lookup 接到 `OmsManiaJudgementAssetPreset`，把 `ScorePosition` / `ComboPosition` 的 shared judgement / HUD-position lookup 接到 `OmsManiaJudgementPositionPreset`，把 `BarLineHeight` / `BarLineColour` 的 shared bar-line lookup 接到 `OmsManiaBarLinePreset`，并把 `SkinComponentLookup<HitResult>` 的 judgement drawable 显式接到 `OmsManiaJudgementPiece`，把 `ManiaSkinComponents.BarLine` 显式接到 `OmsBarLine`，把 MainHUDComponents 里的 combo 显式接到 `OmsManiaComboCounter`，再把 `ExplosionImage` / `ExplosionScale` 的首个 stage-local hitburst config lookup 接到 `OmsManiaHitExplosionPreset`，并把 `ManiaSkinComponents.HitExplosion` 显式接到 `OmsHitExplosion`；`TestSceneOmsBuiltInSkin` 已验证组件类型、最小依赖宿主加载、5K 完整 `Stage` 宿主对 layout preset 的实际使用、5K+5K 双阶段对同一 stage preset 的重复应用、mixed-stage shell behaviour 与 8K edge line width 行为、共享 shell asset 行为、mixed-stage 8K+9K shell colour 行为、4K/5K 与 mixed-stage 5K+8K 的 stage-local key-image 行为、4K/5K 与 mixed-stage 5K+9K 的 stage-local note/hold asset 行为、`DrawableNote` / `DrawableHoldNoteHead` / `DrawableHoldNoteTail` / `DrawableHoldNote` 的实际 OMS note / hold 组件加载、5K 与 mixed-stage 5K+9K 的 shared judgement asset 行为、5K+5K dual-stage 的 shared judgement score / combo-position 行为、mixed-stage 7K+6K 的 shared judgement / HUD position 行为、9K+9K dual-stage 与 mixed-stage 8K+9K / 9K+8K 的 shared bar-line config 行为，以及 `DrawableManiaJudgement` / `OmsManiaComboCounter` / `DrawableBarLine` / `PoolableHitExplosion` 的实际 OMS 组件加载或定位行为；Global shared shell / shared transformer shell 已完成首轮收口，但这些路径仍主要消费 legacy-derived candidate assets 与配置语义；其中 `OmsNotePiece` / `OmsHoldNoteHeadPiece` / `OmsHoldNoteTailPiece` / `OmsHoldNoteBodyPiece` / `OmsManiaJudgementPiece` / `OmsBarLine` / `OmsManiaComboCounter` / `OmsHitExplosion` 已升级为实际 OMS-owned 组件，当前剩余重点转向 note / hold / combo/HUD / bar-line 的余下 legacy 语义清理。

---

### 1.1.6 mania 第二批：Note / Hold / HitBurst / Judgement / HUD

**目标：** 把 mania gameplay 的可玩核心部件全部拉到 OMS 默认皮肤路径上。

**实现要点：**

1. 迁移 `DrawableNote`、`DrawableHoldNote`、`DrawableBarLine`、`PoolableHitExplosion` 的默认素材与 fallback 路由
2. 明确 normal note、hold head/body/tail、bar line、hitburst、judgement、combo 与 gameplay HUD 的默认实现
3. 替换 `ManiaRuleset.CreateSkinTransformer()` 中对 upstream built-in skin type 的产品级依赖
4. 保证用户皮肤只覆盖部分 mania 资源时，其余 mania 组件仍能回落到 OMS mania 子层，而不是退回 upstream 默认皮肤

**前置依赖：** 1.1.4, 1.1.5
**验收：** mania 在无外部皮肤时完整可玩，且 note / hold / judgement / combo / HUD 的默认来源均为 OMS 默认皮肤包。

**当前进展（2026-04-09）：** mania 第二批现已落下首个 stage-local note/hold asset slice、首个 explicit note component slice、首个 explicit hold-note-head component slice、首个 explicit hold-note-tail component slice、首个 explicit hold-note-body component slice、首个 shared judgement asset slice、首个 shared judgement-position slice、首个 shared bar-line config slice、首个 explicit judgement piece slice、首个 explicit bar-line component slice、首个 explicit combo counter component slice、首个 stage-local hitburst config slice与首个 explicit hitburst component slice：`OmsManiaNoteAssetPreset` 已把 `NoteImage` / `HoldNoteHeadImage` / `HoldNoteTailImage` / `HoldNoteBodyImage` 接到 OMS preview 路径，`OmsNotePiece` / `OmsHoldNoteHeadPiece` / `OmsHoldNoteTailPiece` / `OmsHoldNoteBodyPiece` 也已分别把 `ManiaSkinComponents.Note` / `ManiaSkinComponents.HoldNoteHead` / `ManiaSkinComponents.HoldNoteTail` / `ManiaSkinComponents.HoldNoteBody` 接到 OMS preview 路径并经 `DrawableNote` / `DrawableHoldNoteHead` / `DrawableHoldNoteTail` / `DrawableHoldNote` 实际加载，`OmsManiaJudgementAssetPreset` 也已把 `Hit300g` / `Hit300` / `Hit200` / `Hit100` / `Hit50` / `Hit0` 接到 OMS preview 路径，`OmsManiaJudgementPositionPreset` 已把 `ScorePosition` / `ComboPosition` 接到 OMS preview 路径并让 mixed-stage 的 non-column 路径固定复用第一 stage preset，`OmsManiaBarLinePreset` 已把 `BarLineHeight` / `BarLineColour` 接到 OMS preview 路径并遵循同一 mixed-stage first-stage fallback 规则，`OmsManiaJudgementPiece` 也已把 `SkinComponentLookup<HitResult>` 接到 OMS preview 路径并经 `DrawableManiaJudgement` 实际加载，`OmsBarLine` 也已把 `ManiaSkinComponents.BarLine` 接到 OMS preview 路径并经 `DrawableBarLine` 实际加载，`OmsManiaComboCounter` 也已把 MainHUDComponents 里的 combo 接到 OMS preview 路径并经实际 HUD container 加载，而 `OmsManiaHitExplosionPreset` 则已把 `ExplosionImage` / `ExplosionScale` 接到 OMS preview 路径，`OmsHitExplosion` 也已接到 `ManiaSkinComponents.HitExplosion` 并经 `PoolableHitExplosion` 实际加载；其中 `OmsNotePiece` 现已升级为真实 `OmsManiaColumnElement` 派生实现，不再继承 `LegacyNotePiece`，`OmsHoldNoteHeadPiece` / `OmsHoldNoteTailPiece` / `OmsHoldNoteBodyPiece` 也已升级为真实 `OmsManiaColumnElement` 路径下的实际实现，不再继承 `LegacyHoldNoteHeadPiece` / `LegacyHoldNoteTailPiece` / `LegacyBodyPiece`，`OmsManiaJudgementPiece` 现已升级为真实 `CompositeDrawable, IAnimatableJudgement` 实现，不再继承 `LegacyManiaJudgementPiece`，`OmsHitExplosion` 也已升级为真实 `LegacyManiaColumnElement, IHitExplosion` 实现，不再继承 `LegacyHitExplosion`，`OmsManiaComboCounter` 也已升级为真实 `CompositeDrawable, ISerialisableDrawable` 实现，不再继承 `LegacyManiaComboCounter`，`OmsBarLine` 也已升级为真实 `CompositeDrawable` 实现，不再继承 `LegacyBarLine`，八者并已由 `OmsOwnedSkinComponentContractTest` 锁定，相关 `OmsOwnedSkinComponentContractTest|TestSceneOmsBuiltInSkin` 定向回归现为 **50/50** 通过，且已新增 hold-body scrolling-direction 行为回归；但 note / hold 默认路径仍继续复用 legacy note scrolling / sizing、tail inversion、`NoteBodyStyle`、`HoldNoteLightImage` / `HoldNoteLightScale` 与 hold-body light / fade / wrap-stretch 语义，而 `OmsManiaComboCounter` / `OmsBarLine` 也仍继续消费 legacy combo / bar-line 语义，因此还不等于 mania 第二批完整 OMS-owned 默认路径已经完成。

---

### 1.1.7 BMS 第一批：Playfield 壳、Lane 框架、Hit Target、BarLine、Static BG

**目标：** 先把 BMS 的外框、背景、车道、判定线壳体以及 layout-critical 参数做成正式皮肤契约。

**实现要点：**

1. 为 `BmsPlayfield`、`BmsLane`、`BmsScratchLane`、`BmsHitTarget`、`BmsScratchHitTarget`、`DrawableBmsBarLine`、`BmsBackgroundLayer` 建立正式 lookup 与默认实现
2. 建立携带 `LaneIndex` / `LaneCount` / `IsScratch` / `Side` / `Keymode` 等元数据的 BMS lookup 类型
3. 为 lane width、scratch width ratio、lane spacing、hit target / receptor 垂直位置与尺寸、bar line 高度/厚度等参数建立 OMS-owned 的 layout/config 来源，不再只依赖 `BmsLaneLayout` / `BmsPlayfield` / `BmsHitTarget` 的硬编码默认值
4. 为未来 SimpleTou 式 key area / receptor down-state / focus-state 预留正式状态契约，避免后续只能靠临时直绘特判拼接
5. 明确 playfield backdrop / baseplate、lane background / divider、scratch lane、hit target、scratch hit target、bar line、static background slot 的默认组件
6. `#STAGEFILE` / `#BACKBMP` / `#BANNER` 的静态图展示进入正式默认皮肤路径；视频 BGA 仍留在 Phase 2

**前置依赖：** 1.1.2, 1.1.3, 1.1.4
**验收：** BMS 不再只靠 feedback 直绘层与硬编码 geometry 承载 playfield 外壳和背景框架；layout-critical 参数已有 OMS-owned 契约可接管。

---

### 1.1.8 BMS 第二批：Note / Hold / LaneCover / Judgement / Combo

**目标：** 把 BMS gameplay 主体可判定部件拉到正式皮肤契约上。

**实现要点：**

1. 为 `DrawableBmsHitObject` / `DrawableBmsHoldNote*` 建立 normal note、scratch note、hold head/body/tail、scratch hold head/body/tail 的正式组件 lookup
2. 把 `BmsLaneCover` 拆成 top / bottom / focus 三类正式皮肤元素，而不是仅靠直绘 overlay
3. 明确 `BmsJudgementPiece` 与 combo display 的默认实现入口；`BAD` / `POOR` / `EMPTY POOR` 命名继续是产品语义，不交给皮肤决定
4. 保持 scratch 与普通轨道的可读性差异，以及 5K / 7K / 9K_Bms / 9K_Pms / 14K、1P/2P flip 下的布局可用性
5. 让 note / hold / lane cover / judgement / combo 可以消费 1.1.7 中落地的 BMS geometry / receptor / layout metadata，而不是继续依附一套固定的临时布局

**前置依赖：** 1.1.7
**验收：** BMS note / hold / lane cover / judgement / combo 已能通过正式 lookup 被默认皮肤包和用户皮肤接管。

---

### 1.1.9 BMS 第三批：Gauge / ClearLamp / Results / Song Select Panels

**目标：** 收口 BMS 的 HUD、结算与选歌信息面板，让 BMS 专属信息不再散落为特殊直绘类。

**实现要点：**

1. 已完成 `BmsGaugeBar`、results summary、clear lamp、`BmsGaugeHistoryGraph`、`BmsNoteDistributionGraph`、playfield metadata / accent surfaces、playfield 壳层，以及 note / hold 的 OMS-owned 默认层承接
2. 区分 gameplay HUD、results、Song Select details 三类面板的 lookup 和默认实现
3. 保证用户皮肤可以单独覆盖 gauge、gauge history、note distribution，而不需要整包接管全部 BMS UI
4. 把当前候选视觉语言正式移植到 BMS 默认层，验证其在无外部皮肤时可完整承接 gameplay HUD / results / Song Select 与 playfield metadata 语义；当前 gameplay HUD、results summary / clear lamp、results gauge history、Song Select note distribution、playfield metadata / accent surfaces、playfield shell surfaces，以及 note / hold visuals 已全部落地，BMS 默认层在 ruleset 侧已完成收口
5. 清理当前仅为 feedback 层服务的直绘面板，使其退回“最后兜底”而非正式默认实现

**前置依赖：** 1.1.7, 1.1.8
**验收：** BMS HUD / results / Song Select details 都已有正式默认皮肤实现和独立 override 入口。

---

### 1.1.10 用户皮肤导入、partial override 与兼容桥

**目标：** 明确“一个皮肤选择项里同时含 mania 与 BMS 资源”的导入和缺省行为，而不是只把默认皮肤做好。

**实现要点：**

1. 明确 OMS 用户皮肤可以同时包含 `Global` / `Mania` / `BMS` 三层资源，也可以只覆盖其中一层或几层
2. mania 继续通过 legacy `skin.ini` / 资源命名兼容导入；BMS 则通过 OMS-own lookup / layout 约定接入
3. 用户皮肤仅覆盖 mania 时，BMS 必须稳定回落到 OMS 默认皮肤包的 BMS 层；反之亦然
4. 缺失组件按组件粒度 fallback，不允许“一缺就整套退回 upstream 默认皮肤”

**前置依赖：** 1.1.3, 1.1.6, 1.1.9
**验收：** 可以明确回答“用户导入一个只含 mania 资源、只含 BMS 资源、或同时含两者资源的皮肤时，系统如何解析和 fallback”。

---

### 1.1.11 上游默认皮肤退出、设置入口清理与发行打包

**目标：** 在产品表面和发行物里彻底切掉上游原生默认皮肤的默认地位。

**实现要点：**

1. 清理设置页、默认推荐项、runtime fallback 和 public build 中对 Argon / Triangles / DefaultLegacy / Retro 的默认暴露路径
2. 调整 selector 与默认值，让 OMS 默认皮肤包成为 no-custom-skin 的唯一产品默认入口
3. 明确哪些 upstream 资源只可保留为开发态兼容依赖，哪些必须从正式发行中剥离
4. 确保一个公开发行构建在删掉 upstream 默认皮肤作为产品默认前提后，mania 与 BMS 仍完整可用

**前置依赖：** 1.1.4, 1.1.6, 1.1.9, 1.1.10
**验收：** 在无自定义皮肤的终端体验里，用户不会再把 upstream 原生默认皮肤误认为 OMS 默认皮肤。

---

### 1.1.12 测试矩阵与 release gate

**目标：** 把默认皮肤包、ruleset 子层和用户皮肤 partial override 一起纳入可重复验证。

**实现要点：**

1. 非视觉测试：transformer fallback、lookup route、missing component fallback、partial override、native-default removal semantics
2. mania 视觉测试：stage、column、key、hit target、note、hold、hitburst、judgement、combo、HUD
3. BMS 视觉测试：playfield、lane、scratch lane、hit target、note、hold、lane cover、background、gauge、results、note distribution、judgement
4. 打包测试：公开发行构建只附带 OMS 默认皮肤包；无自定义皮肤下 mania/BMS 均可完整游玩 / 结算 / 选歌

**前置依赖：** 1.1.5 ~ 1.1.11
**验收：** 达到“一个默认皮肤包内集成 Global + Mania + BMS 独立规则集皮肤层”的正式 release gate，后续视觉迭代不再依赖上游默认皮肤。

---

### Phase 1.1 完成里程碑

**达成条件：**

- [ ] OMS 默认皮肤包成为无自定义皮肤时的正式默认路径
- [ ] mania 与 BMS 已在同一个默认皮肤选择项中集成，但各自拥有独立的 ruleset gameplay 皮肤实现
- [ ] mania stage / note / judgement / combo / HUD 已迁移到 OMS-owned 默认路径
- [ ] BMS playfield / lane / note / lane cover / static background / HUD / results / Song Select details 已完成 skinization
- [ ] 用户皮肤 partial override 已有明确导入与 fallback 语义
- [ ] upstream 原生默认皮肤不再作为 OMS 默认产品表面
- [ ] 正式发行包已纳入“只附带 OMS 默认皮肤包”约束
- [ ] 皮肤系统具备 non-visual / visual / packaging 三层回归

---

## Phase 2 — BMS 功能完善

### 2.1 beatoraja + LR2 判定 Mod

> **✅ 已在 Phase 1 提前落地。** `BeatorajaJudgementSystem`、`Lr2JudgementSystem`、`BmsModJudgeBeatoraja`、`BmsModJudgeLr2` 均已实现，窗口值校验已经补测，三套判定系统已通过 `BmsScoreProcessorTest` / `BmsDrawableRulesetTest` 回归。

**实现：** `BeatorajaJudgementSystem` + `Lr2JudgementSystem`；`BmsModJudgeBeatoraja` + `BmsModJudgeLr2` Mod 类。beatoraja 按 #RANK 缩放窗口，LR2 固定窗口。

**前置依赖：** Phase 1 完成
**验收：** 单元测试覆盖全部判定系统的窗口值。切换 Mod 后 gameplay 使用对应窗口。

---

### 2.2 全 Gauge 类型

> **✅ 已在 Phase 1 提前落地。** `BmsGaugeProcessor` 现已统一承载六种 gauge type，各 gauge mod 已通过 `BmsGaugeProcessorTest` 数值语义核验。

**实现：**

- BmsModGaugeAssistEasy / BmsModGaugeEasy（非生存型，20% 起始，2% 底线）
- BmsModGaugeHard / BmsModGaugeExHard（生存型，100% 起始，0% 失败）
- BmsModGaugeHazard（100% 起始，BAD/POOR 瞬间归零，GOOD 不触发失败）
- 各 Gauge Mod 互斥

**前置依赖：** Phase 1 完成
**验收：** 单元测试——每种 gauge 的回复/伤害/结算/失败条件全覆盖。HAZARD gauge 下 GOOD 不扣血。

---

### 2.3 GAS (Gauge Auto Shift)

> **✅ 已在 Phase 1 提前落地。** `BmsModGaugeAutoShift`、`BmsGasGaugeProcessor` 与 GAS-aware 的 `BmsClearLampProcessor` 已接通，`BmsGasGaugeProcessorTest` 已覆盖降级链、重算与结果持久化。

**实现：** `BmsModGaugeAutoShift`（StartingGauge / FloorGauge Bindable），降级链 HAZARD→EX-HARD→HARD→NORMAL→EASY→ASSIST EASY，非生存 gauge 不再降级，结算取最佳灯。

**前置依赖：** 2.2
**验收：** 测试场景——从 EX-HARD 开始，触发降级到 NORMAL，通过结算 → 最佳灯为 NORMAL CLEAR。结算画面显示各层 gauge 图表。

---

### 2.4 A-SCR (Auto Scratch)

**实现：** `BmsModAutoScratch`——scratch 音符标记为 AutoPlay，排除出计分/gauge/combo/MaxExScore 池，键音照常播放。`AscScratchVisibility` 设置。14K DP 双侧 scratch 同时处理。

**前置依赖：** Phase 1 完成
**验收：** A-SCR 激活后 scratch 自动触发键音，不计入 EX-SCORE。MaxExScore 正确减少。可见性设置切换有效。

---

### 2.5 Empty Poor 判定

> **✅ 已在 Phase 1 提前落地。** `BmsLane` 现会在 lane action 未被任何 drawable 消费时注入 synthetic `BmsEmptyPoorHitObject`，以 `ComboBreak` 承载 Empty Poor；已通过测试验证 gauge 伤害、combo 断裂、结果页计数。

**实现：** `BmsPoorJudgement`——每车道维护活跃音符窗口状态，按键时无可打击音符 → 触发 Empty Poor → gauge 伤害 = BAD 1.0×、断 combo、不影响 EX-SCORE。

**前置依赖：** Phase 1 完成
**验收：** 空打确实触发 gauge 伤害和 combo 断裂；结算画面 Empty Poor 计数正确。

---

### 2.6 5K / 9K / 14K DP 布局

**实现：**

- BmsLaneLayout 扩展——5K(5+1) / 9K(BMS) / 9K(PMS Pop'n 排列) / 14K(双侧 7+1)
- 14K DP 单一绑定档案（1P + 2P 统一界面）
- 密度星级校准常数——每种键模式独立的标准化参数

**前置依赖：** Phase 1 完成
**验收：** 分别导入 5K / 9K / 14K 谱面 → 布局正确渲染，绑定功能正常，星级值合理。

---

### 2.7 1P/2P 翻转 Mod

**实现：** `BmsModMirror1P2P`——水平镜像车道数组，scratch 左右互换，绑定跟随翻转，皮肤元素响应 CurrentSide bindable。

**前置依赖：** 2.6
**验收：** 7K 1P 侧翻转为 2P → scratch 从左移到右。14K DP 下无效果或双侧对调。

---

### 2.8 模拟轴输入

**实现：**

- HidDeviceHandler 扩展——当前按钮/HID axis delta 最小链路已接通；下一步补旋转编码器速度语义
- MouseAxisHandler 扩展——当前 gameplay mouse delta -> `OmsAction` 最小链路已接通；下一步补原始 delta / 灵敏度设置
- AxisInverted 标志——每绑定独立极性翻转（HID axis / MouseAxis 最小链路已接通）
- scratch 车道同时接受数字键 + 模拟轴 + 鼠标三种信号（当前已接通数字键 + delta 驱动 action，后续补速度/灵敏度）

**前置依赖：** 1.17
**验收：** HID 旋转编码器产生 scratch 输入；鼠标横向移动触发 scratch；反转标志生效。

---

### 2.9 LNTYPE 2 (MGQ) 长条

**实现：** BmsBeatmapDecoder 中追加 LNTYPE 2 解析逻辑。MGQ 格式的 LN 头/尾配对规则。

**前置依赖：** 1.3
**验收：** LNTYPE 2 格式的测试 BMS 文件正确解析为 BmsHoldNote。

---

### 2.10 BGA 视频播放

**实现：**

- #BMP## 索引解析（BmsBeatmapDecoder 中已预留）
- Channel 04/06/07 BGA 事件时间轴
- BmsBackgroundLayer 扩展——ffmpeg.autogen 解码视频帧，纹理更新
- POOR 层（Channel 06）在 POOR 判定时显示

**前置依赖：** 1.8（BmsBackgroundLayer 已预留槽位）
**验收：** 含 BGA 视频的 BMS 谱面播放时正确显示视频，POOR 层在误判时切换。

---

### 2.11 用户皮肤生态扩展

> **说明：** 核心内置皮肤替换、mania/BMS 默认视觉迁移、BMS 全量 lookup / drawable skinization 已前移至 **Phase 1.1**。Phase 2 不再负责“默认皮肤能不能用”，而负责默认 OMS 内置皮肤完成后的用户皮肤生态扩展。

**实现：**

- 用户自定义皮肤兼容性说明、样例工程与校验工具
- 兼容 beatmap skin / 用户 skin 的更细粒度策略与设置项
- 对 mania / BMS 的自定义主题扩展点继续补全，但不允许破坏 OMS built-in fallback

**前置依赖：** Phase 1 完成, Phase 1.1 完成
**验收：** 用户安装自定义皮肤后，mania/BMS 缺失组件仍稳态回退 OMS built-in；文档与样例足以支撑外部主题制作。

---

### 2.12 BmsRulesetConfigManager 设置画面

**实现：** BMS 模式设置画面集成所有持久化设置——AutoScratchNoteVisibility、KeysoundConcurrentChannels、LeaderboardGaugeFilter、LeaderboardAscrFilter、LeaderboardJudgeFilter、LeaderboardLnModeFilter。

**前置依赖：** 2.4
**验收：** 设置画面各项可修改 → 重启后保持。

---

### 2.13 LN / CN / HCN 长条模式

> **✅ 已在 Phase 1 提前落地。** `BmsLongNoteMode` 运行时枚举、`BmsModChargeNote` / `BmsModHellChargeNote` 互斥 conversion mod、head/tail score-point 分离、mode-aware EX 分母、`HCN` 固定量化 body gauge tick、本地 best/replay/排行榜 judge + long-note mode 分桶均已接通。

**实现：**

- 引入 `BmsLongNoteMode` 运行时枚举：`LN`（默认）、`CN`、`HCN`
- `LN` 作为默认路径，不启用 Mod；`CN` / `HCN` 作为互斥可选 Mod 或单一配置型 Mod 的两个值
- `CN`：头判 + 尾判；`HCN`：头判 + 尾判 + body gauge tick；三者共用同一份 beatmap conversion 输出
- `EX-SCORE` / `MaxExScore` / `Combo` / `FULL COMBO` / `DJ LEVEL` 分母与资格按 active long-note mode 切换
- `HCN` body 采用时间驱动的固定 tick 累积实现，不直接绑定渲染帧率
- 本地 best、replay、本地排行榜按 `BmsLongNoteMode` 分桶；Phase 3 的私服提交与在线排行榜复用同一 bucket 语义

**前置依赖：** 1.9, 1.10, 1.11
**验收：** 同一张谱面分别以 `LN` / `CN` / `HCN` 游玩后，会生成彼此独立的本地成绩与 replay；`CN` 尾判生效；`HCN` body 仅影响 gauge、不直接增加 EX-SCORE；若后续接入 Phase 3 联网提交，则沿用同一分桶键。

---

### Phase 2 完成里程碑

**达成条件：**

- [x] 三套判定系统全部可用（已在 Phase 1 提前落地）
- [x] 六种 Gauge + GAS 全部可用（已在 Phase 1 提前落地）
- [ ] A-SCR 可用
- [x] LN / CN / HCN 长条模式与分桶成绩可用（已在 Phase 1 提前落地）
- [x] Empty Poor 功能正常（已在 Phase 1 提前落地）
- [ ] 5K / 9K / 14K DP 布局完整
- [ ] 1P/2P 翻转功能正常
- [ ] HID 旋转编码器和鼠标 scratch 输入可用
- [ ] BGA 视频播放（含 POOR 层）
- [ ] 用户皮肤生态扩展与兼容性工具可用

---

## Phase 3 — 私服集成

> Phase 3 启动前，OMS 维持“便携全量包发布 + 手工文件覆盖更新 + 禁用游戏内在线更新”的策略，不提前恢复联网分发能力，也不向终端构建写入默认 production 服务器地址；账号、在线排行榜、谱面下载、聊天/新闻、多人/观战与远程难度表源入口统一保持隐藏或禁用。

### 3.1 API 客户端基础 (`OmsApiClient`)

**实现：** HttpClient 封装、可配置 Base URL、Bearer Token 存储（Windows 凭据管理器）、Refresh Token 流程、请求/响应序列化、网络错误处理、离线回退逻辑。

**前置依赖：** Phase 2 完成
**验收：** 可连接测试服务器完成登录/刷新流程。网络断开时 OMS 自动进入离线模式无崩溃。

---

### 3.2 账号认证

**实现：**

- AuthEndpoint — POST /auth/login, POST /auth/refresh
- 登录 UI（用户名/密码输入、记住登录状态）
- GET /user/me → OmsUser 模型，主界面显示用户信息
- 退出登录 / 切换账号

**前置依赖：** 3.1
**验收：** 完整登录→显示用户名→退出→重新登录流程。

---

### 3.3 成绩提交

**实现：**

- POST /scores/submit — OmsScore 完整载荷（含 Mod 标签、gauge_mode、judge_mode、long_note_mode、EmptyPoorCount 等）
- 结算画面增加上传按钮/自动上传
- 离线成绩本地暂存 → 联网后批量补传
- 客户端 replay hash（防篡改基础措施）

**前置依赖：** 3.2
**验收：** 打完一首谱面 → 成绩上传 → 服务端可查。离线打完 → 联网后自动补传。

---

### 3.4 在线排行榜

**实现：**

- LeaderboardEndpoint — GET /scores/chart/{hash}（含 gauge/ascr/judge/lnmode 筛选参数）
- Song Select 排行榜面板——显示 Top N 成绩
- 筛选 UI（gauge / A-SCR / judge / LN mode 独立下拉）
- 筛选状态持久化到 BmsRulesetConfigManager

**前置依赖：** 3.3
**验收：** Song Select 选中谱面 → 排行榜面板加载成绩列表 → 筛选条件生效 → 切换谱面后筛选保持。

---

### 3.5 谱面搜索与下载

**实现：**

- BeatmapDownloadEndpoint — GET /beatmaps/search, GET /beatmaps/{id}/download
- 搜索 UI（关键词、分页）
- 下载进度指示 → 自动调用 BmsArchiveReader 导入
- 已拥有谱面标记

**前置依赖：** 3.1, 1.5
**验收：** 搜索 → 选择 → 下载 → 自动导入 → Song Select 中出现。

---

### 3.6 服务端难度表镜像

**实现：**

- GET /difficulty-tables, GET /difficulty-tables/{id}
- BmsDifficultyTableManager 增加服务端源——拉取镜像表数据作为社区表的补充/替代
- 当社区 URL 不可达时自动回退到服务端镜像

**前置依赖：** 3.1, 1.13
**验收：** 社区表 URL 模拟不可达 → 自动从服务端镜像拉取 → 数据一致。

---

### Phase 3 完成里程碑

**达成条件：**

- [ ] 账号登录/登出/刷新完整可用
- [ ] 成绩自动上传（包含所有 Mod / long-note mode 标签）
- [ ] 在线排行榜（带复合筛选）
- [ ] 谱面搜索与下载
- [ ] 难度表服务端镜像
- [ ] 离线模式优雅降级

---

## 步骤依赖关系图

```text
Phase 1:
  1.1 ──┬── 1.2 ── 1.3 ── 1.4 ──┬── 1.5 ──┬── 1.6
        │                        │          │
        │                        │          └── 1.14 ── 1.15
        │                        │
        │                        ├── 1.7 ── 1.8 ──┬── 1.9 ── 1.10 ── 1.11
        │                        │                 │
        │                        │                 ├── 1.16
        │                        │                 │
        │                        │                 └── 1.17
        │                        │
        │                        └── 1.12
        │
        └── 1.13

Phase 1.1（执行顺序，不按编号大小）:
  1.1.1 ── 1.1.2 ── 1.1.3 ── 1.1.4 ── 1.1.7 ── 1.1.8 ── 1.1.9 ── 1.1.5 ── 1.1.6 ── 1.1.10 ── 1.1.11 ── 1.1.12

Phase 2 (全部依赖 Phase 1 核心闭环 + Phase 1.1 内置皮肤基线完成):
  2.1
  2.2 ── 2.3
  2.4
  2.5
  2.6 ── 2.7
  2.8
  2.9
  2.10
  2.11
  2.12
  2.13

Phase 3 (全部依赖 Phase 2 完成):
  3.1 ──┬── 3.2 ── 3.3 ── 3.4
        ├── 3.5
        └── 3.6
```

---

## 单元测试优先级

以下组件**必须**在实现同步编写测试，不可推迟：

| 优先级 | 组件 | 测试重点 |
| --- | --- | --- |
| **P0** | BmsBeatmapDecoder | 头字段、通道解析、LN 配对、键模式检测、编码检测、#RANDOM 跳过 |
| **P0** | BmsBeatmapConverter | 绝对时间精度、ControlPoint 生成、STOP 时间窗口、MeasureLengthControlPoints |
| **P0** | BmsTimingWindows | 三套判定系统的全部窗口值 |
| **P0** | BmsScoreProcessor | EX-SCORE 计算、Combo 规则、各判定计数、long-note mode 分母/分桶 |
| **P0** | BmsGaugeProcessor | 全部 gauge 类型的回复/伤害/底线/结算/失败，以及 HCN body tick 契约 |
| **P0** | OMS / mania / BMS skin transformer | fallback 顺序、native-default removal semantics、lookup route |
| **P0** | BMS gameplay skinization | lane / scratch / hit target / note / hold / lane cover / static BG 在无外部皮肤下都能落到 OMS built-in |
| **P1** | BmsDifficultyCalculator | 合成谱面的星级计算、百分位值 |
| **P1** | BmsNoteDensityAnalyzer | bucket 计数、加权规则、边界条件 |
| **P1** | BmsTableMd5Index | 导入匹配、表刷新批量更新、内存索引重建 |
| **P1** | BmsDifficultyTableManager | 两步拉取、缓存持久化、禁用表排除 |
| **P1** | mania 默认视觉迁移 | 无外部皮肤时 stage / column / note / HUD 均走 OMS-owned 默认路径 |
| **P1** | 皮肤打包约束 | 公开发行构建不再把 upstream 原生默认皮肤当作 OMS 默认体验 |
| **P2** | BmsClearLampProcessor | 灯级升级逻辑、A-SCR 下的灯判定 |
| **P2** | BmsDjLevelCalculator | EX% 边界值（8/9、7/9 等分数精度） |
