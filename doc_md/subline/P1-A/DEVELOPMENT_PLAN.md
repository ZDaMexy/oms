# P1-A 开发计划：产品面、release gate 与皮肤边界

> 最后更新：2026-06-29
> 主线总规划见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。本文件只拆解 `P1-A` 的执行顺序；`P1-C` 的反馈闭环计划见 [../P1-C/DEVELOPMENT_PLAN.md](../P1-C/DEVELOPMENT_PLAN.md)。

## 专题定位

| 维度 | 归属 | 说明 |
| --- | --- | --- |
| 主归属 | `P1-A` | 冻结 BMS HUD / skin boundary，确定 skin lookup、HUD 宿主与 fallback 的扩展边界 |
| 协作子线 | `P1-C` | 绿色数字、速度反馈与训练反馈闭环依赖本子线冻结的宿主合同 |
| 支线参考 | `other` | 受 [../../other/IIDX_REFERENCE_AUDIT.md](../../other/IIDX_REFERENCE_AUDIT.md) 约束，但当前不等价于完整 `FHS` 落地 |
| 明确不归线 | Phase 2 | `FHS`、`dan`、`1P/2P flip`、BSS / MSS、其他训练模式保持冻结 |

## 当前确认基线

- `BmsSkinTransformer` 的 BMS / mania 边界已收口，BMS lookup 与 fallback 语义已可作为稳定宿主合同继续扩展。
- `BmsScrollSpeedMetrics` 现已按 `Normal / Floating / Classic Hi-Speed + Sudden / Hidden / Lift` 计算 `VisibleLaneTime`、`WhiteNumber`、`GreenNumber`；其中 `Classic` 继续锁定官方 sample `HS 10 + WN 350 => GN 300`，`Floating` 目前为 initial-BPM anchored surface。
- 当前 runtime 已同时具备 `BmsSpeedMetricsToast` 与常驻 speed-feedback HUD；toast 退位为操作确认层，常驻 feedback card 承担权威表达。
- 若后续追加 pre-start 1 号普通轨纯视觉流速预览，宿主必须继续停留在 playfield / lane visual surface，并复用 BMS note lookup / fallback；它不是 HUD / toast 的扩展槽位。
- 内部 `BmsPlayfieldLayoutProfile` abstraction gate 仍保留，但当前 runtime geometry override surface 已冻结，不再通过设置页暴露会扰动 strict profile 的 layout sliders。
- 当前 `IBmsHudLayoutDisplay` 只接受 wrapped HUD、gauge bar、combo counter 三类组件；若直接扩签名，会打断现有 HUD provider 合同。
- judgement 基线摆位现由 `BmsGameplayFeedbackLayout` 收口为 shared position contract（其默认 gameplay feedback 摆位已随速度反馈卡于 2026-06-15 移除，仅余 judgement 基线）；后续若联动 judge display，应扩展这条合同，而不是重新散落新的位置常量。
- BMS mod ruleset-local memory surface 现已补齐 cold-start path：若 startup 首次 ruleset change 早于 `RulesetConfigCache.LoadComplete()`，宿主必须延后 replay 当前 ruleset 到 cache ready 后再做 restore；这条路径现已有 dedicated integration coverage。
- 首次启动向导、`Run setup wizard` 与无谱面引导这类共享 onboarding / settings-entry surface 归 `P1-A`；若页面只是复用外部 / 内部谱库或按键绑定面板，则 `P1-H` / `P1-B` 只记从属影响，不为此另开子线。
- desktop 通用 Settings -> 输入 当前也属于共享 settings-entry surface 的产品裁剪范围；若要隐藏 upstream 的数位板 / 触屏点击 / 鼠标 subsection，应在 desktop 宿主层安全隐藏，而不是下移成全宿主删除。
- 共享层 first-run wizard 若需触发 BMS-only runtime 能力，必须继续避开 `osu.Game -> osu.Game.Rulesets.Bms` 编译期依赖；当前难度表导入页使用反射加载 `BmsDifficultyTableManager`，这条边界应继续保持。
- 若后续公开 `BMS -> mania` 单向转谱入口，`P1-A` 只承接按钮文案、入口位置与 Song Select / presentation gating；source keymode -> mania keycount、lane flatten、scratch-family 退化与 validity 仍归 `P1-K/K9`，不得把现有 generic `显示转谱` surface 直接升格成该专题的 authority。
- BMS Song Select 共享 `FilterControl` / carousel 产品面已新增「展示层级」下拉与「层级返回条」（**已落地 2026-06-16**，主归 `P1-I` I5–I7，见 [../P1-I/DEVELOPMENT_PLAN.md](../P1-I/DEVELOPMENT_PLAN.md)）。落地维持了 ruleset-aware row branching（展示层级下拉是共享 sort/group/collection 行里的 BMS-only 第 4 列，非 BMS 收 0 宽、行布局不变）、切 ruleset 完整回退、未新开 per-ruleset `FilterControl` host；`GroupNavigationDisplay` 复用 `ScopedBeatmapSetDisplay` 视觉但状态独立；共享 `PanelGroup` 按 `group.Depth` 区分层级配色（非层级分组全 depth 0、零影响）。`P1-A` 在此只记从属影响：展示层级 / 层级导航的语义 authority 归 `P1-I`，不下沉到 `P1-A`。

## 专题目标

1. 先冻结 BMS-owned feedback contract，避免绿色数字、lane cover focus、后续 `FAST/SLOW` 与 pacemaker 各走一条 ad-hoc HUD 链。
2. 把当前 OMS runtime 的速度反馈明确为“tri-mode runtime surface 下的权威表达”，同时明确 `Floating` 仍不是完整 `FHS`。
3. 保持对现有 BMS 用户皮肤和 HUD 布局接口的向后兼容，不为专题推进破坏既有 fallback 语义。

## 分期计划

### A0：文档与边界冻结

状态：已完成

- 盘点 BMS skin boundary、HUD 宿主接口、`GN / WN / Lift` 计算链与 `Sudden / Hidden` 联动。
- 建立专题级计划 / 状态 / 技术约束文档。
- 把专题明确归线到 `P1-A / P1-C`，并同步挂接到主线文档。

### A0.5：首次启动向导与设置导流

状态：已完成首轮落地

- 首次启动设置已收口为六步 OMS flow：欢迎、UI 缩放、获取谱面、导入、难度表设置、按键绑定。
- `获取谱面` / `导入` / `难度表设置` / `按键绑定` 四页当前都属于共享产品表面：可复用现有 `ExternalLibrarySettings`、keybinding subsection 与 BMS difficulty-table runtime，但不应因此改写各自底层子线归属。
- 欢迎页、获取谱面页与导入页的可见文案若需覆盖上游翻译，必须使用 OMS-owned localisation namespace + `.resx`，不能只改 `*Strings.cs` fallback。

验收：

- 手动重新打开向导后各页可稳定加载，不因复用 settings 组件而在 load 阶段崩溃。
- focused first-run tests + Release build 通过。

### A1：反馈组件合同冻结

状态：进行中

目标：为当前和后续 gameplay feedback 建立统一入口，而不是继续把 speed feedback 固定在 toast，同时保持 release gate 与现有 HUD provider 不被打断。

建议交付：

1. 新增 `BmsSkinComponents.GameplayFeedbackDisplay`，作为 BMS-owned feedback 宿主组件。
2. 新增 `IBmsGameplayFeedbackDisplay`，由 ruleset 向组件推送稳定的 state 对象，而不是让组件反查 `DrawableBmsRuleset` 内部字段。
3. 保持现有 `IBmsHudLayoutDisplay` 不变，新增可选的 versioned HUD layout 接口或 wrapper contract，用于额外接入 feedback display。
4. `BmsSkinTransformer` 只在新接口存在时把 feedback display 交给 HUD layout；旧接口保持现状不破坏。

> 当前已完成的合同骨架：`IBmsHudLayoutDisplayWithGameplayFeedback`、legacy HUD overlay wrapper 与 `DefaultBmsHudLayoutDisplay` 默认 fallback 已落地；`BmsGameplayFeedbackLayout` 现已把默认 gameplay feedback 摆位与 judgement 基线收口到 shared position contract；`BmsGameplayFeedbackState` aggregate snapshot 也已完成两刀接线，当前已包含 `TimingFeedbackVisualRange`。剩余工作是决定 richer judge display / history 类状态继续如何分层，而不是回退到组件直接反查 ruleset 多组 bindable。

验收：

- 不破坏现有 `HudLayout` / `GaugeBar` / `ComboCounter` fallback。
- 当前用户皮肤不实现新接口时仍能正常显示旧 HUD。
- 新 feedback display 在 `OmsSkin` 默认路径下可独立 fallback。

### B1：权威绿色数字常驻反馈

状态（2026-06-15 更新）：常驻 GN 宿主曾完成，但承载它的常驻速度反馈卡已按产品决定整体移除——**常驻 GN 不复存在，GN 仅留 toast / pre-start overlay**；本节为历史规划记录，tri-mode operator surface 仍在。

目标（历史）：为 `P1-C` 的常驻 GN 与 feedback family 提供稳定宿主边界。**常驻 GN 与该 feedback family 已于 2026-06-15 整体移除**；本子线现维护的是 tri-mode settings / pre-start overlay 与 HUD 宿主（gauge / combo）边界，feedback 家族如需重建须另立专题。

建议交付：

1. 冻结 `GameplayFeedbackDisplay` 所需的最小宿主接口与 fallback 语义。
2. 冻结 HUD layout 的向后兼容扩展方式，不让 `P1-C` 再次修改旧接口。

### B2：tri-mode settings 与 pre-start hold operator surface

状态：已完成功能面落地，后续维持验证与边界维护

目标：在不破坏 HUD / skin boundary 与项目依赖边界的前提下，把三模式设置、runtime feedback 与 pre-start 调速窗口收口成同一条产品合同。

当前已完成：

1. `BmsSettingsSubsection` 现提供 `Normal / Floating / Classic Hi-Speed` 下拉与当前模式 slider；slider 当前会显示“模式数值 + 括号内基础下落时间（ms，不启用 `Sudden / Hidden / Lift`）”，但仍不把 `GreenNumber` 写回 settings。`键音通道数` 现也补上 hover 提示，并以 `32` 作为 shared keysound pool 的默认折中值。与此同时，`osu!mania` 的 `滚动速度` slider 也已补上 hover，明确括号毫秒只代表标准车道几何下的参考下落时间，不应在不同皮肤间或与 BMS 下落时间直接互相参考。
2. `BmsSoloPlayer` / `BmsPreStartHiSpeedOverlay` 已把“前 5 秒 delayed start 阻塞 + 全程调速修饰键”这一 `UI_PreStartHold` 合同接入正式 gameplay 流程：前 5 秒仍承担 hold gate，正式 gameplay 开始后继续受理奇偶键调速；paused pre-start 下的 `UI_LaneCoverFocus` / 滚轮 / 中键 `Sudden / Hidden / Lift` 调整链也保持同一条运行时入口。
3. `SoloSongSelect` 通过反射创建 `BmsSoloPlayer`，避免 `osu.Game` 对 `osu.Game.Rulesets.Bms` 新增编译期依赖。
4. owner-level `TestSceneBmsPreStartHiSpeedOverlay` 与 real-player `TestSceneBmsSoloPlayerPreStart` 已形成双层 focused coverage，当前分别锁住 overlay 文案 / 输入合同与 delayed-start / hold gate / mode-value binding 的真实宿主链。
5. 若按 `P1-C` 启动 pre-start 1 号普通轨纯视觉流速预览，宿主必须继续放在 playfield / lane surface，而不是 HUD / toast；用户皮肤缺失时仍应回退到 BMS note lookup / OMS 默认 note 外观。

后续检查点：

1. 当前 dedicated coverage 已锁住 overlay owner contract、hold 跨过 delay 到期仍可调速、正式 gameplay 中 hold 仍可调速、以及 real-player mode/value binding；后续只在新增 host / fallback surface 时补更广 visual / integration 覆盖。
2. 持续守住 tri-mode settings、HUD 与 overlay 的 BMS-owned fallback 合同。
3. 保留 toast 作为补充反馈层，但不再让任何新功能直接依赖 toast 作为唯一宿主。
4. 把数值 state 的具体字段集留给 [../P1-C/DEVELOPMENT_PLAN.md](../P1-C/DEVELOPMENT_PLAN.md) 继续细化。
5. 若推进 pre-start 视觉预览，先补 playfield / lane host + fallback route，再由 `P1-C` 接 runtime 语义与 focused validation。

验收：

- `P1-C` 可以在不破坏现有 HUD provider 的前提下安全接入 feedback display。
- HUD 默认实现与皮肤 fallback 均可稳定显示，不依赖 Debug overlay。

### B2：`Sudden / Hidden / Lift` 联动收口

状态：进行中

目标：让 lane cover focus、当前 target、geometry-effect 与 HUD feedback 表达统一，避免视觉上各说各话。

建议交付：

1. 当前 target 与 lane cover focus state 必须一一对应，HUD 不允许出现“焦点已切换但 display 仍停留旧 target”的状态。
2. `Lift` 继续保持 geometry control，不与 `Hidden` 混写；HUD 文案与状态表达必须延续这条边界。
3. 对“仅启用 1 个 target”“无 target 可切换”“当前 target 因 mod 未启用而失效”给出明确显示策略。

> 当前 click-to-cycle、temporary override 与多 target 循环位置已接通；剩余重点是 richer HUD 状态表达、边界态 fallback 与 authoring 口径继续收口。

验收：

- `Sudden / Hidden / Lift` 三项在启用 / 禁用 / 切换时的 HUD 行为可预测。
- 焦点与 HUD 指示一致。

### B3：BMS -> mania 单向转谱公开表面

状态：进行中（visibility gate、resolved-star selector 与 spread display 已落地；explicit public wording 与更宽 presentation/manual proof 待收口）

目标：在不污染现有 generic convert surface 的前提下，为后续 `BMS -> mania` 单向转谱冻结公开产品表面的入口、文案与可见性边界。

建议交付：

1. 公开表面必须显式视为 `BMS source -> mania target` 的单向入口，不得沿用或暗示 generic all-ruleset convert 口径。
2. `P1-A` 只拥有入口位置、按钮文案、Song Select / `PresentBeatmap` 可见性与 unsupported case 的产品反馈；source keymode matrix、lane flatten、scratch-family 退化与空结果 suppress contract 继续由 `P1-K/K9` 冻结。
3. 在 `P1-K/K9` 的 dedicated mapping / autoplay / persisted-star proof 已落地后，`P1-A` 当前已可继续承接 `AllowGameplayWithRuleset()`、`RequiresRulesetSwitch()`、`ShowConvertedBeatmaps` 与 current-ruleset star display 的公开表面；后续不得再回退到 raw-star selector 或 raw-star spread surface。
4. 首轮 product surface 只允许暴露已支持的 `5K+1S/7K+1S/9K_Bms/9K_Pms/14K+2S` source charts；unsupported source keymode、flatten 后无可游玩对象或 target ruleset gate 失败时，入口必须直接隐藏或给出明确不可用反馈，而不是展示可点击空壳。
5. focused validation 优先复用 [../P1-K/DEVELOPMENT_PLAN.md](../P1-K/DEVELOPMENT_PLAN.md) 已冻结的 mania convert/autoplay/selector/resolver 与 `PresentBeatmap` / Song Select 测试锚点；只有当 `P1-K/K9` 的 value-level proof 先成立后，才允许补更宽 visual / UX / manual 验证。

验收：

- 用户不会把该入口误读为 generic convert 全面开放。
- supported / unsupported source chart 的入口可见性与反馈口径一致。
- `P1-A` 不需要拥有任何 BMS -> mania 映射语义即可稳定承接公开表面。

### C1：扩展到统一 gameplay feedback 家族

状态：进行中

目标：在 speed feedback 合同稳定后，把 `FAST/SLOW`、judge display、visual timing-offset、EX pacemaker 纳入同一反馈家族，而不是再开新的临时 overlay。

建议交付：

1. `FAST/SLOW` 与 judge display 优先进入同一 feedback container，而不是和 judgement piece 互相硬耦合。
2. visual timing-offset 与 EX pacemaker 也沿同一状态流接入，避免每个功能单独占用 HUD 注入点。
3. judgement 位置如果需要与 feedback 排布联动，应显式新增位置合同，不继续扩散硬编码偏移值。

> shared position contract 已落地；后续这一步的重点不再是“先抽常量”，而是决定如何在不破坏现有 skin/judgement 生命周期的前提下继续扩 judge display 的语义与排布。
> （2026-06-15 更新）shared position contract 中的 **judgement 基线摆位保留**；但 **feedback container 与 aggregate snapshot 已随速度反馈卡移除**，本节 richer judge display / history 分层延展随之作废，如重建须另立专题。

验收：

- gameplay feedback 家族拥有稳定宿主，不再依赖临时 toast 链。
- 新功能的加入不需要继续修改旧版 HUD layout 接口签名。

### D1：作者文档与 release gate 收口

状态：进行中

目标：把这条专题从“实现中合同”变成“可维护的 authoring / release gate 文档”。

建议交付：

1. 在 [../../other/SKINNING.md](../../other/SKINNING.md) 中补齐 `GameplayFeedbackDisplay` 的 authoring 入口、fallback 粒度与状态合同。
2. 在 [../../mainline/OMS_COPILOT.md](../../mainline/OMS_COPILOT.md) 中把接口和命名边界收口成硬约束。
3. 在 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md) 中记录实现状态，在 [../../mainline/CHANGELOG.md](../../mainline/CHANGELOG.md) 中记录验证结果。

> 当前 mainline 四件套与 `OMS_COPILOT.md` 已跟随 2026-04-28 的 tri-mode / pre-start / library grouping 状态同步；剩余主要是 `SKINNING.md` 的作者入口与 release gate 口径继续补齐。

### E1：gauge 下移判定线下方 + 矩形化 + 等宽镜像 playfield（游玩区抬高）

状态：**已落地（2026-06-20，#1#2 实机验收通过）**。落地过程多轮迭代演进（详见 [CHANGELOG](CHANGELOG.md) 2026-06-20）：本节原计划值 `0.86` 经"间隙贴紧/一体化 → 整体下移(已删 offset) → 顶边贴屏幕边缘"后**最终落到 `PlayfieldHeight=0.92` + playfield 顶边贴边**（GN 语义不变）；另含 gauge 免疫 `ShowHealthBar`、combo 移到 playfield 中心去色块、从默认皮肤配置移除 leaderboard + 重复 combo。

背景：原 gauge bar（`NORMAL / 20%` 圆角胶囊）摆在 playfield 顶部；判定线在 95% 屏高、下方仅 ~5% 空带、且与底部歌曲进度条相挤。按 IIDX groove-gauge 观感把 gauge 下移到判定线下方，并抬高游玩区腾出空带。用户规划阶段选定「gauge 与判定区等宽」；抬高幅度原定 0.86、落地演进为 0.92（顶边贴边）。

目标 / 交付：

1. **抬高游玩区**：`BmsPlayfieldLayoutProfile` 默认 `PlayfieldHeight 0.95 → 0.86`（提常量 `DEFAULT_PLAYFIELD_HEIGHT`，仍是 strict profile 的唯一杠杆，config `PlayfieldHeight` 维持被忽略的 disabled 项）。判定线上移到 86% 屏高、下方 ~14% 空带容纳 gauge。**时序 / GN 不变**：`HitTargetVerticalOffset=0` 时 `BmsHitObjectArea` 的 `scrollLengthRatio≡1`、`TimeRange` 与场高无关，仅落条像素扫过距离变短（视觉略密），判定窗口完全不变。
2. **gauge 矩形化**：`BmsGaugeBar` 圆角 `CornerRadius 10→0`、bar 高 `20→~28`，保留填充 / floor band / clear 标记 / 高光与 `NORMAL`+`20%` 文案，加极淡等分刻度营造 groove-gauge 观感（不做 IIDX 逐格细节）。
3. **gauge 下移 + 等宽 + 侧锚镜像**：默认摆位由 `DefaultBmsHudLayoutDisplay` 负责——gauge 顶边贴判定线下方（相对 `Y≈PlayfieldHeight`）、宽度等于 playfield 条带（`Width=PlayfieldWidth`）、并随 `PlayfieldStyle.GetAppliedStyle(keymode)` 做 P1 左 / P2 右 / 居中侧锚（复用与 lane 同一套 `side_anchored_horizontal_inset`），与判定区严格同列。combo 暂留原位（本轮只动 gauge）。
4. **合同保持**：gauge 仍留在 HUD `IBmsHudLayoutDisplay.SetComponents(wrappedHud, gauge, combo)` 合同内，**不改签名**（满足「HUD 宿主约束 1」）。所需几何经 HUD 可见的 DI 通道取得：`PlayfieldWidth / keymode` 经 `GameplayState` 可玩谱面（`BmsLaneLayout.CreateFor`），`PlayfieldStyle` 经 game 级 `IRulesetConfigCache.GetConfigFor(bms)`（与 playfield 子树同一 `BmsRulesetConfigManager` 实例，可绑定 live 变化）。

红线 / 验收：

- 不动判定窗口 / 计分 / 滚动时序；chartbms 直读不变。
- `SetComponents` 签名不变；旧 HUD provider 与 gauge / combo / GaugeBar 皮肤 fallback 回归保持绿。
- 无 `GameplayState` / config 的宿主（皮肤编辑器预览 / 测试）下优雅降级（居中 + 兜底宽度），不抛异常。
- 测试同步 `PlayfieldHeight 0.95→0.86`（`BmsLaneLayoutTest`、`TestSceneBmsPlayfieldLayoutConfig`）；补 gauge 下移摆位 / 等宽 focused 断言。
- 实机 7K / 14K / P1 / P2 验收：判定线抬高、gauge 贴线且与 lane 同列等宽、矩形观感、时序无感知变化。

### F：BMS 素材 + ini 皮肤创作生态（P0–P3）

立项 2026-06-27（大型规划，未开工实现）。目标＝把当前"临时应付"的纯代码型 BMS 皮肤升级成像 mania 那样「放文件夹 + `skin.ini` 即换皮」的产品。**硬约束见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md) 「皮肤创作生态（素材 + ini）约束」（权威源）**；面向制作者的渲染文档见 [../../other/SKINNING.md](../../other/SKINNING.md)。范围锁定**仅游玩界面**（lazer 已弃非游玩皮肤）。

锁定决议（用户已拍板，详见 CONSTRAINTS 1–10）：游玩界面 only / 自有 `[Mania]`-对齐 + `[Bms]` 扩展段·keymode 分桶 / 程序化兜底 + 参考素材皮肤·不烤 PNG / fail-open + 诊断·必备三档 / 手改 ini + 热重载 + 复用 lazer 布局编辑器（决议 X：BMS 专属 HUD 保持代码编排不升格）/ 新 `BmsAssetSkin` 包在 `BmsSkinTransformer` 下零改 lookup。

#### F0：组件契约 + ini schema + 必备分档冻结（纯文档）

状态：**已落地（2026-06-27）**

- 组件契约（创作者上限）已按真实生态（osu!mania / beatoraja / LR2，含联网检索校准）铺开，叠加必备 / 推荐 / 可选三档；契约与 ini schema 草案、校验行为已冻结进 CONSTRAINTS 与 `SKINNING.md`。
- 权威层已立账：本节（PLAN F 系列）+ CONSTRAINTS「皮肤创作生态」节为权威源，`SKINNING.md` 降为派生的制作者视图。
- 验收：CONSTRAINTS / PLAN / STATUS / CHANGELOG 四件套与 `SKINNING.md` 口径一致；mainline 已回写本立项。

#### F1：素材 + ini 加载器 / 校验器 / 热重载 + 参考皮肤（①类静态件）

状态：**已完成**（ini 三轴皮肤化 + reference 验收 + Stage 框架 + KeyImage 替代路线全部落地，BMS 1024/1024 + core gate 绿；架构见本节末「实现架构」）

建议交付：

1. 新增 `BmsAssetSkin`（读皮肤文件夹 + `skin.ini`）作为被 `BmsSkinTransformer` 包裹的 `ISkin`，按文件名约定取图喂给现有 `Default*` 组件路径；零改现有 lookup 契约。
2. ini 解码器 + 配置 schema（对应 mania 的 `LegacyManiaSkinDecoder` / `LegacyManiaSkinConfiguration`）+ 强类型配置 lookup；按 `Keymode:` 分桶。
3. 校验器：加载期 fail-open + 诊断（未知键忽略 + 告警、缺素材回退 + 告警），必备件内置兜底。
4. 覆盖①类静态件（lane 背景 / 分隔、note、LN 头身尾、判定线、judgement、stage frame、barline、combo、lane cover、playfield backdrop）；跑通"放文件夹 + 热重载即换皮"最小闭环。
5. 产出 reference 素材皮肤（复现程序化默认观感、颜色 / 几何为主、近零位图）＝本期验收对象兼创作者模板。

验收：放入 reference 皮肤后游玩界面与程序化默认一致；缺键 / 缺图按 fail-open 回退 + 记诊断；BMS 全套与 Release gate 保持绿；新增 `BmsAssetSkin` 加载 / 校验 focused 测试。

实现架构（2026-06-27 代码勘探落账，修正立项期表述）：

1. **fail-open 已天然成立，F1 不得改兜底语义**：`SkinManager.AllSources` 在 `CurrentSkin` 后恒 yield `DefaultOmsSkin`，`RulesetSkinProvidingContainer` 亦把 `DefaultOmsSkin` 显式作为链底 fallback；用户选非 OmsSkin 素材皮肤时，缺件经 `SkinProvidingContainer` 链式查找自动回退到链底 OmsSkin 那层（`providesBuiltInFallbacks = skin is OmsSkin` 为 true）的程序化兜底。该判定是「仅链底默认皮肤兜底」的**正确分层设计**，F1 **不得**为「让素材皮肤兜底」去改它（改了会每层重复注入兜底、破坏分层）。立项期「兜底需重构 / 解耦」的设想**作废**。
2. **纹理来源 = 被包裹 skin 的 `GetTexture`（不侵入核心）**：素材贴图按文件名约定经被 `BmsSkinTransformer` 包裹的 skin（`LegacySkin` / 用户素材皮肤）`GetTexture` / `GetAnimation` 直取，对齐 mania `NoteImage#` 等，无需新增核心能力。
3. **结构化配置（颜色/几何/`[Bms]` 段）= 落 ruleset 内独立解析，禁止侵入核心 `LegacySkin`**：核心 `osu.Game/LegacySkin` 把 mania 段解析（`LegacyManiaSkinDecoder` + `maniaConfigurations` 字段 + `GetConfig` 的 `LegacyManiaSkinConfigurationLookup` 分支）**硬编码进 osu.Game**——上游历史包袱。BMS 作为 ruleset 不得照搬；`BmsSkinDecoder` / `BmsSkinConfiguration` / `BmsSkinConfigurationLookup` 必须落在 `osu.Game.Rulesets.Bms`，由独立 BMS 皮肤配置源解析（与红线「保持 `osu.Game` 与 ruleset 模块边界」一致）。
4. **头号 gate（动工前最后决策）= BMS 结构化配置如何从「SkinManager 选中的皮肤」读到**：皮肤来源已定为复用 SkinManager；用户选的皮肤通常实例化为通用 `LegacySkin`，它不解析 BMS 段。候选：(A) **统一 OMS 皮肤实例化类型**——导入皮肤实例化为同时解析 mania + BMS 段的 OMS 自有 `LegacySkin` 子类（与 [`OMS_COPILOT.md`](../../mainline/OMS_COPILOT.md) 「`CreateSkinTransformer` 从 thin override 进化为完整 OMS fallback provider / 迁离 upstream built-in skin」方向一致，最干净，但触及皮肤导入实例化合同）；(B) `BmsAssetSkin` 借被包裹 skin 的 `IStorageResourceProvider` 重解析皮肤 ini 的 BMS 段（不改导入，但需拿到被包裹 skin 的资源句柄）；(C) BMS 配置走皮肤内独立约定文件 `skin.bms.ini`，`BmsAssetSkin` 单独读。**已定 (A)（用户 2026-06-27 拍板）**：导入皮肤实例化为同时解析 mania + BMS 段的 OMS 自有 `LegacySkin` 子类；F1 第②刀据此接入，须同步守住 mania 侧（`ManiaLegacySkinTransformer` 经核心 `LegacySkin` 读 mania 段的链路）不回归。
5. **素材化策略 = 改造现有 `DefaultBms*Display` 读 config（颜色/几何/可选纹理），不另起 Asset* 全家桶**：契合 CONSTRAINTS「皮肤创作生态」第 6 条（程序化辉光保留为引擎绘制 + ini 参数化、不烤 PNG），避免双份组件维护。
6. **schema 由代码实现确立、`SKINNING.md` 据代码派生（勿反向）**：F1 的 ini 键集/语义以「mania `LegacyManiaSkin*` 实现 + BMS 现有 `DefaultBms*Display` / `BmsPlayfieldLayoutProfile` / `BmsDefaultPlayfieldPalette` 真实暴露的可参数化量」为依据，**而非照抄** `SKINNING.md`（`[规划]` 派生草案）或本节/CONSTRAINTS 的 P0 草案键名。已知草案失真点（落码以代码为准）：① **几何**——代码无 mania 式 `HitPosition`/逐列 `ColumnWidth`，真实量＝`PlayfieldWidth`(归一化杠杆 `Clamp(lanes×0.06,.35,.8)×0.825`) / `PlayfieldHeight 0.92` / `Normal·ScratchLaneRelativeWidth`(1 / 1.5) / `…Spacing`(0 / 0.12) / `HitTarget{Height16, BarHeight12, LineHeight3, GlowRadius6, VerticalOffset0}` / `BarLineHeight2`（均 `CreateDefault` 参数、当前 runtime config 被忽略）；② **颜色**——代码是 **IIDX 键色组**（`NoteColourGroup` White/Cyan/Yellow/Scratch 按键号+keymode 派生）+ lane bg(even/odd/scratch) + divider + hit target(bar/line/glow) + barline(major/minor)，**非逐道 `ColourColumn{lane}` 任意色**；③ **LN body** 有真实 `Width 0.5775` / `alpha 0.8·broken 0.32` / 三态 `BodyState`，tail `Alpha=0`（草案未列）。F1 落地后据此回写 `SKINNING.md`。

修正后落地顺序（替代立项期含「解耦兜底」的旧拆分）：① ini 三件套（`BmsSkinDecoder` / `BmsSkinConfiguration` / `BmsSkinConfigurationLookup` + `GetBmsSkinConfig<T>` 扩展，照 `LegacyManiaSkinDecoder`、`Keymode:` 分桶）→ ② BMS 皮肤配置源（按头号 gate 选定方案接入）→ ③ 单条 lookup 最小闭环（note：ini 指定纹理/颜色 → `DefaultBmsNoteDisplay` 渲染，缺件回退程序化 + 诊断）→ ④ 铺开①类静态件 + 校验器（fail-open + 诊断）+ 热重载 → ⑤ reference `skin.ini`（复现程序化默认观感）作验收。

#### F2：②类引擎驱动件补挂点（ini 仅供素材）—— 仿 IIDX/LR2/beatoraja 的真正大头

状态：**进行中（5 件 + Ghost-TD + 接口契约已落地·剩 turntable/bomb/comboburst）**——keyflash + hit lighting + LN hold light + mine hit + ghost-TD 已落，含 5 个接口契约（`IBmsKeyFlashDisplay`/`IBmsHitLightingDisplay`/`IBmsHoldLightDisplay`/`IBmsMineHitDisplay`/`IBmsGhostTdDisplay`）+ Transformer `satisfiesF2InterfaceContract`/`satisfiesPlayfieldInterfaceContract` 检查；BMS 1024/1024。原 2026-06-29 勘察确认这些件在 OMS BMS 当前零渲染——全仓 grep 无 turntable / keyflash / hit explosion / bomb / LN hold light / ghost-TD 任何组件；盘面结构本身也无 turntable 区、无键区

定位：**这是"皮肤制作者能否还原 LR2/beatoraja 体验、甚至仿 IIDX"的决定性一期。** F1 只让现有静态件（音符/车道/判定线/cover/背景…）可换色换图调几何；IIDX 之所以是 IIDX 的招牌动态件**目前没有组件可供贴图**，必须本期从零造。

- **结构前置（先于贴图）**：盘面需先有承载位——turntable 区（盘侧）、键区（判定线下方，与现 gauge 带的空间冲突需先裁决；`KeyImage`/`KeyImageDown` lookup 已留槽但无件）。这部分会动到 E1/几何已精调的布局，须单独定位决策。
- **组件清单**（每件＝引擎驱动 + ini 仅供素材/位置/缩放/颜色，作者不写关键帧）：keyflash / 键光柱 · hit explosion（按判定档）· LN hold light · turntable + laser（圆盘旋转 + 扫描光）· bomb 命中动画 · ghost-TD（判定偏移幽灵）· lane beam/lighting。
- **建议分期**：先 keyflash + hit explosion（最常见、最影响打击反馈观感、无结构改动）→ LN hold light → turntable（需结构前置）→ ghost / laser。
- **与 P1-L 协作**：[P1-L](../P1-L/) 已落地地雷渲染 / BGA 链；bomb / 演出类件须对齐复用、不重复造；本期仅补"皮肤可换素材"的挂点。
- **红线**：仅视觉，不碰判定 / 计分 / 滚动 / 键音 / chartbms 直读（CONSTRAINTS 第 10 条）；落地前不得在 `SKINNING.md` 标为"当前可用"。

**F2 进展（2026-07-04 起）**：5 件 + Ghost-TD 已落地——keyflash + hit lighting + LN hold light + mine hit + ghost-TD，含 5 个接口契约（`IBmsKeyFlashDisplay`/`IBmsHitLightingDisplay`/`IBmsHoldLightDisplay`/`IBmsMineHitDisplay`/`IBmsGhostTdDisplay`）+ Transformer `satisfiesF2InterfaceContract`/`satisfiesPlayfieldInterfaceContract` 检查。`BmsLaneSkinElements.KeyFlash`/`HitLighting` + `BmsSkinConfigurationLookups.KeyFlashImage`/`HitLightingImage`/`KeyFlashColour`/`HitLightingColour` + `DefaultBmsKeyFlashDisplay`（绑定 `BmsHitTarget.IsPressed`·`SkinnerDrawable`·可换图/色）+ `DefaultBmsHitLightingDisplay`（`DrawableBmsHitObject.ApplyResult` 触发）。F2 剩余 turntable（需布局裁决）/ bomb / comboburst 待续。

#### F3：③类 `[Bms]` 扩展段独有件 + 契约冻结

状态：未开工

- gauge 条 / 类型 / GN%、cover 绿数、bpm、progress、note distribution 等无 osu-ini 先例的 `[Bms]` 扩展段键落地；公开 authoring 文档收尾；冻结 schema 契约。
- 含 **gauge 外观皮肤化**（当前 `BmsGaugeBar` 是代码型）；属"必备件 gauge 可覆盖"的 ini 落地面。

### G 系列：皮肤存储与分发轨（与 F 授权面正交，排序独立）

立项 2026-06-29（路线图，未开工）。**F1–F3 解决"什么可被皮肤控制"；G 系列解决"皮肤文件如何被存放/管理/分发"**，两轴正交。源于用户 2026-06-29 提出"皮肤要像 chartmania/chartbms 一样在数据目录可视可管"。

#### G1：皮肤可视文件夹存储（**revisit F1「复用 SkinManager hash 体系」gate 决议**）

状态：未开工（**这是对 F1 gate 决议的方向性调整，需用户拍板后启动**）

- **现状（2026-06-29 勘察）**：皮肤走核心 `SkinImporter : RealmArchiveModelImporter<SkinInfo>`（只认 `.osk`），文件进 realm 的 **hash-backed `files/` 存储**（文件名=内容哈希·人不可读·不可手动管理）。F1 gate（2026-06-27）曾明确"复用 SkinManager·**不走 chartbms 旁路**"——本项即重审该决议。
- **目标**：皮肤像 chartmania/chartbms 一样在数据目录有**可视、人类可读文件夹**（如 `chartskin/<名>/skin.ini` + 资源），直接读，realm 只索引路径/元数据，不进 hash store。
- **可复用模型**：[`BmsFolderImporter`](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs)（`SONGS_STORAGE_PATH="chartbms"` → 复制进可视文件夹 + realm 索引 `FilesystemStoragePath`；managed/external 两态、路径遍历守卫、复用判重）。新建皮肤文件夹导入/扫描器仿之。
- **技术要点**：(a) `BmsLegacySkin` 现从 hash store/fallbackStore 读 skin.ini → 改为从可视文件夹的 `IStorageResourceProvider` 读；(b) SkinManager 列表/设置切换仍要能列出+选用文件夹皮肤（`SkinInfo` 需承载"folder-backed"·类比 `BeatmapSetInfo.FilesystemStoragePath`/`IsExternalFilesystemStorage`）；(c) 文件夹直读后改 ini 可**热重载**（F1 既定能力，本项才真正可落）；(d) 与 [P1-H](../P1-H/) 存储拓扑协作（同款外部/内部扫描机制）。
- **红线**：不破坏核心 SkinManager 对 `.osk`/内置皮肤既有路径；离线优先；不破坏本会话已落的 `BmsLegacySkin` 路由（SkinImporter 改写点）。
- **风险**：比 F1 的 SkinImporter 路由更深，触及 `SkinInfo.Files` 语义与皮肤实例化的资源来源。

实现架构（2026-06-29 代码勘探落账——读 `SkinManager` / `Skin` 基类 / `SkinImporter` / `BmsFolderImporter`）：

1. **读取机制已证、零改核心资源链。** `Skin` 基类把 `fallbackStore` 并入资源 `store`：skin.ini 经 `store.GetStream(配置名)`、纹理经 `TextureStore(CreateTextureLoaderStore(resources, store))`；realm `Files` 经 `RealmBackedResourceStore`（`SkinInfo.Files[名]→hash→resources.Files`）先查、查不到回落 `fallbackStore`。`OmsSkin` 正是用内嵌 `NamespacedResourceStore` 当 fallbackStore + 空 `SkinInfo.Files`。**故文件夹皮肤 = fallbackStore 换成 `StorageBackedResourceStore(storage.GetStorageForDirectory("chartskin/<名>"))`**（与 SkinManager 既有 `userFiles = StorageBackedResourceStore("files")` 同款 API），skin.ini + 纹理直接从可视文件夹读、不进 hash store、**不改 `Skin`/资源解析核心**。
2. **实例化接法 = 方案 D4（复用本会话反射范式）。** `SkinManager.GetSkin = skinInfo.CreateInstance(this)` 走 `InstantiationInfo` 反射 `(SkinInfo, IStorageResourceProvider)` ctor——不带 fallbackStore。folder 皮肤改走分支：`SkinInfo.FilesystemStoragePath` 非空时，SkinManager **反射调用皮肤类型的 `(SkinInfo, IStorageResourceProvider, IResourceStore<byte[]> fallbackStore)` ctor**（`BmsLegacySkin` 已有该 protected ctor·改 public 供反射），传 `StorageBackedResourceStore(chartskin/<path>)`。核心仍**不编译依赖 ruleset**（反射字符串·同本会话 `SkinImporter` 路由）；非 folder/非 BMS 时零变化。
3. **realm 模型（已迁移·刀②落地 2026-06-29）。** `SkinInfo` 已加 `string? FilesystemStoragePath` + `bool IsExternalFilesystemStorage`（镜像 `BeatmapSetInfo`）；`RealmAccess.schema_version` 55→56——加性 nullable/scalar 字段，无 migration case（与 v50–55 同模式·realm 自动加列填默认）。**本刀同时加 `IsExternalFilesystemStorage`**（原标"可选"）以免刀④再 bump 一次 schema（beatmap 侧分 v13/v54 两次 bump 是外部库后到的反例）；**未加 `ExternalLibraryRootPath`**（皮肤外部库嵌套语义未设计·投机字段·留刀④定）。字段本刀不写入任何生产路径（填充在刀④）。
4. **文件夹导入/扫描（仿 [`BmsFolderImporter`](../../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs)）。** 新 `chartskin/` 目录；皮肤文件夹导入器：managed（folder 落 `chartskin/` 下·realm 存相对路径·**不复制到 hash**）/ external（注册外部目录·不复制）；启动扫描 `chartskin/` 入 realm（同 chartbms）。
5. **SkinManager 列表/选择。** `GetAllUsableSkins` 已从 realm 列非 protected 皮肤→folder 皮肤入 realm 后**自动出现在皮肤下拉、可选用、`CurrentSkinInfo` 切换**；`Delete`/`Rename` 须对 folder 皮肤正确处理（删文件夹 vs 仅删 realm 记录·参 `BmsFolderImporter` managed/external 语义）。
6. **热重载。** folder 直读后监视 `chartskin/<名>/skin.ini` 变化→重建 skin（F1 既定"热重载"本项才真落地）。
7. **红线。** 不破坏核心对 `.osk` 导入 / `OmsSkin` 既有路径；不破坏本会话 `SkinImporter` 的 `BmsLegacySkin` 路由；离线优先；mania 段解析仍走核心 `LegacySkin`（folder `BmsLegacySkin` 继承之·共存）；folder 皮肤 `SkinInfo.Files` 留空（空 Files 经 `RealmBackedResourceStore` 安全回落 fallbackStore·已证）。

落地顺序（刀）：✅① `BmsLegacySkin` folder ctor 转 public + 真实临时目录直读 skin.ini/纹理测试（ruleset-only·低风险，**已落 2026-06-29**）→ ✅② `SkinInfo` 加 `FilesystemStoragePath`+`IsExternalFilesystemStorage` + `RealmAccess` schema 55→56（核心 realm·加性·**已落 2026-06-29**：Release gate 绿 + BMS 1002/1002 + 核心 Skins 57 通过·5 失败为预存 osu-beatmap 解码失败·`git stash` 干净树同样·零因果）→ ✅③ `SkinManager.GetSkin` folder 分支（D4 反射 folder ctor·守卫测试钉死字符串·非 folder 零变化·**已落 2026-07-04**：BMS 1003/1003 + gate 绿）→ ✅④ `chartskin/` 文件夹导入器/扫描器（仿 `BmsFolderImporter`·managed/external + 启动扫描 + `SkinManager` 接入·**已落 2026-07-04**：BMS 1003/1003 + Skins 88/97·零因果 + gate 绿）→ ✅⑤ 列表/选择/删除/重命名 + UI 入口（`SkinManager.Delete/Rename` folder 感知 + `SkinSection` 扫描/打开按钮·**已落 2026-07-04**：BMS 1003/1003 + Skins 88/97 + gate 绿）→ ✅⑥ 热重载（`FileSystemWatcher` 监视 chartskin/skin.ini 变化→debounce 1s→自动重建当前皮肤·**已落 2026-07-04**：BMS 1003/1003 + gate 绿）。**G1 可视文件夹存储链路贯通**。**与 [P1-H](../P1-H/) 存储拓扑协作**（external/managed 扫描机制可复用）。

#### G2：文件型内置默认皮肤（可选）

状态：未开工

- **现状**：BMS 默认皮肤 **100% 程序化**（`BmsDefaultPlayfieldPalette` 颜色 + `BmsPlayfieldLayoutProfile` 几何 via `DefaultBms*Display`）；本会话的 [reference skin.ini](../../other/oms-bms-reference-skin/skin.ini) 仅文档模板，**未接成运行时默认**。
- **目标（可选）**：把 reference skin.ini 接成实际加载的内置默认（OmsSkin 内嵌 or G1 落地后随 `chartskin/` 自带）。
- **权衡**：程序化兜底的价值＝任何缺键/文件损坏都有最终保底；改文件型默认须保证文件缺失/损坏时回落程序化（保留 `DefaultBms*` 兜底，文件默认只作"预填覆盖层"）。小工作量；可独立（内嵌）或依赖 G1（随文件夹）。

## 当前优先顺序

1. `E1` gauge 下移判定线 + 矩形化 + 等宽镜像（本轮）
2. `A1` 反馈组件合同冻结
3. 与 [../P1-C/DEVELOPMENT_PLAN.md](../P1-C/DEVELOPMENT_PLAN.md) 对齐常驻绿色数字与速度反馈字段集
4. 与 [../P1-C/DEVELOPMENT_PLAN.md](../P1-C/DEVELOPMENT_PLAN.md) 对齐 pre-start 1 号普通轨纯视觉流速预览的 host / fallback route
5. `B2` `Sudden / Hidden / Lift` 联动收口
6. `C1` 扩展到统一 gameplay feedback 家族
7. `D1` 作者文档与 release gate 收口
8. **`F1` 已完成**：ini 三轴皮肤化 + reference 验收 + Stage 框架 + KeyImage 替代路线全部落地（BMS 1024/1024）
9. **皮肤后续路线（2026-06-29 立项——见 `F2` / `G1` / `G2`）**：
   - `G1` 皮肤可视文件夹存储（**已完成**·全链路贯通）
   - `F2` ②类引擎驱动件（5 件 + Ghost-TD + 接口契约已落地·剩 turntable（需布局裁决）/ bomb / comboburst）
   - `G2` 文件型默认皮肤（小·可选） / `F3` ③类 `[Bms]` 扩展段（gauge 样式/GN/bpm）
