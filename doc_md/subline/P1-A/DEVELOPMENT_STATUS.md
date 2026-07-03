# P1-A 开发进度：产品面、release gate 与皮肤边界

> 最后更新：2026-06-29
> 主线全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。本文件只记录 `P1-A` 的真实进展；`P1-C` 的反馈闭环进展见 [../P1-C/DEVELOPMENT_STATUS.md](../P1-C/DEVELOPMENT_STATUS.md)。

## 当前阶段

- **阶段定位**：子线建档完成，HUD 宿主与边界冻结已基本稳定，当前进入“tri-mode operator surface 挂接后的稳态化 + `阻止谱面开始/ingame start` 运行时宿主语义收口 + BMS mod surface 记忆合同收口 + onboarding/settings-entry surface 收口”阶段。
- **代码状态**：HUD 宿主合同为单一 `IBmsHudLayoutDisplay`（wrapped HUD + gauge + combo）+ `DefaultBmsHudLayoutDisplay` 默认摆位；`BmsGameplayFeedbackLayout` 仅保留 judgement 基线摆位。**（2026-06-15 移除）** `IBmsHudLayoutDisplayWithGameplayFeedback` 变体、legacy overlay wrapper、`BmsGameplayFeedbackState` aggregate 与 `DefaultBmsSpeedFeedbackDisplay`（含 FAST/SLOW、judgement counts、EX progress、pacemaker、recent-history、live `PERFECT / FC` 状态线）均按产品决定删除；judgement 计数改走全局 `JudgementCounterDisplay`（已修 COMBO BREAK）。当前 BMS 速度产品面已扩到 tri-mode：settings 提供 `Normal / Floating / Classic Hi-Speed` 下拉和当前模式 slider，`BmsSoloPlayer` / `BmsPreStartHiSpeedOverlay` 已把 `UI_PreStartHold` 收口为“前 5 秒阻止开始 + 全程调速修饰键”这一正式 gameplay operator surface，paused pre-start 状态下仍可继续使用 `Sudden / Hidden / Lift` 调整链；右侧 `READY HOLD` overlay 只保留给前 5 秒阻止开谱窗口，正式 gameplay 开始后按住同一键仍会继续调速，并持续刷新居中的 `BMS speed` toast。对应覆盖现已分成 owner-level `TestSceneBmsPreStartHiSpeedOverlay`、real-player `TestSceneBmsSoloPlayerPreStart` 与输入桥 `OmsInputRouterTest` 三层：前者锁住 tri-mode 文案 formatting 与“仅在 overlay 可见时响应 odd/even lane 调速”的组件合同，后两者分别锁住 **10/10** 的真实宿主链 delayed-start / hold modifier / mode-value binding，以及 **9/9** 的 hold 期间 lane-action gameplay 转发抑制。`UI_PreStartHold`（阻止开谱/调速修饰键）与 `UI_LaneCoverFocus`（单击循环持久目标）已拆为独立动作。desktop shared Settings -> 输入 现已通过 `OsuGameDesktop.CreateSettingsSubsectionFor()` 安全隐藏 upstream 的数位板 / 触屏点击 / 鼠标 subsection，避免把非 OMS 通用设置表面继续暴露给最终桌面产品面；该裁剪不触碰 mouse/touch/tablet runtime config 与 handler 链，并明确保留在 desktop 宿主层。与此同时，BMS mod 选项表面也已收口为 ruleset-local memory surface：`OsuGameBase` 现会在 BMS 切入点恢复 selected mods 与 remembered settings，`ModSelectOverlay` 对标记 mod 不再在 deselect 时 reset，而 `Sudden` / `Hidden` / `Lift` 也已把 `记忆游戏内变动` 作为 mod-local 开关暴露给用户。考虑 `RulesetConfigCache` 的 startup 顺序后，宿主现会在 cache ready 后 replay 当前 ruleset，因此完全冷启动第一次进入 BMS 也会恢复 selected mods / remembered settings，且不会再冒出误报的 ruleset issue 通知。与此同时，`BMS -> mania` 公开表面的第三刀也已接上 persisted converted-star authority：modless converted mania 星数现已写入 BMS metadata payload，并由 `BeatmapDifficultyCache`、`BackgroundDataStoreProcessor` 与 Song Select spread display 统一按 current-ruleset 视角读取，因此 carousel selector 与 spread dots 都不再继续直接吃 source BMS raw star。
- **首次启动向导状态**：共享层 first-run wizard 现已收口为六步 OMS flow：欢迎、UI 缩放、获取谱面、导入、难度表设置、按键绑定。获取谱面页改为 mania / BMS 外部站点导流与内部谱库补扫提示；导入页直接复用 `ExternalLibrarySettings`；难度表页通过反射调用 `BmsDifficultyTableManager` 导入 zris 镜像预设；最后一步复用全局、mania 与 BMS keybinding subsection。欢迎页、获取谱面页与导入页的可见文案已切到 OMS-owned localisation namespace + `.resx`，确保简中不再继续显示上游翻译。该专题主归属继续是 `P1-A`；导入页对 `P1-H`、按键绑定页对 `P1-B` 都只形成从属暴露面。
- **BMS -> mania 转谱公开表面状态**：该表面现已从“首轮 visibility gate 已落地”推进到“visibility gate、persisted converted-star display 与 spread display 已落地、显式 wording 仍未收口”状态。当前 `AllowGameplayWithRuleset()` / `RequiresRulesetSwitch()` 已把 `BMS source -> mania target` 接回真实可玩性判断，modless converted mania 星数也已改为持久化到 BMS metadata payload，并由 `BeatmapDifficultyCache`、后台补算与 current-ruleset spread display 统一读取；因此 Song Select 的星数筛选、难度排序、按星数分组与 spread dots 都不再继续直接吃 source BMS raw star。剩余主要是按钮 wording、显式入口文案与更宽 presentation/manual proof。
- **文档状态**：`P1-A` 的计划、状态、变动日志、技术约束已与当前宿主合同实现同步，并已把 2026-04-28 的 pre-start overlay / real-player coverage 与 mainline 文档口径一并收平。
- **皮肤创作生态（素材 + ini）立项状态（2026-06-27 新增）**：BMS 素材 + `skin.ini` 皮肤创作/编辑生态已正式立项（大型规划，**未开工实现**）。`F0`（组件契约 + ini schema 草案 + 必备分档冻结，纯文档）已落地：契约按真实生态（osu!mania / beatoraja / LR2，含联网检索校准）铺开并叠加必备 / 推荐 / 可选三档，权威源冻结进 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)「皮肤创作生态（素材 + ini）约束」与 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) `F` 系列，面向制作者的渲染视图见 [../../other/SKINNING.md](../../other/SKINNING.md)（已从"自称 P0 契约"降级重锚为派生视图）。`F1`（`BmsAssetSkin` 加载器 / 校验器 / 热重载 + 参考皮肤）起未开工，排在主交付线之后。当前 BMS 默认皮肤仍是 100% 程序化（零位图素材），按 CONSTRAINTS 决议保留为不可删兜底。**（2026-06-27 代码勘探追加 `F1` 实现架构）**：fail-open 已天然成立于「OmsSkin 恒在 fallback 链底」（`SkinManager.AllSources` + `RulesetSkinProvidingContainer`），**F1 不改 `providesBuiltInFallbacks`**（立项期「兜底需重构」设想作废）；核心 `LegacySkin` 硬编码 mania 段解析，BMS ini 段不得侵入核心、须落 ruleset 内独立解析；纹理走被包裹 skin `GetTexture`。**头号 gate 已定（用户 2026-06-27 拍板 A：统一 OMS 皮肤实例化类型）**——导入皮肤实例化为同时解析 mania + BMS 段的 OMS 自有 `LegacySkin` 子类；候选与权衡见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) F1「实现架构」第 4 条。皮肤来源复用 SkinManager 皮肤体系。**第一刀 ini 解析三件套（`BmsSkinConfigurationLookups`/`Lookup`/`Configuration` + 独立 `BmsSkinDecoder` + 8 用例单测）已落地、BMS 全套 969/969；[SKINNING.md](../../other/SKINNING.md) 已据代码更正失真（§3/§4/§5.4/附录 C）。** ②刀配置源（方案 A）接入待续。

## 已确认事实

- BMS 皮肤边界已足够封闭，可继续向 BMS-owned feedback component 扩展。
- 当前 `GN` / `WN` 来自 `BmsScrollSpeedMetrics`，其输入现已覆盖 `Normal / Floating / Classic Hi-Speed`、`ScrollLengthRatio`、`Sudden`、`Hidden`、`Lift`。
- 当前 tri-mode Hi-Speed surface 已完成首轮产品接线：`Normal` 走默认 settings surface，`Floating` 提供 initial-BPM anchored runtime surface，`Classic` 继续锁定 `HS 10 + WN 350 => GN 300`；这仍不等价于完整 FHS。
- settings 页现显示 mode + value，并在数值后括号显示不启用 `Sudden / Hidden / Lift` 时的基础下落时间（ms）；`GreenNumber` 仍不进入 settings，而继续留在 gameplay feedback 链。
- `osu!mania` settings 页的 `滚动速度` hover 提示当前也已明确为参考值说明：括号毫秒只代表标准车道几何下的参考下落时间，不作为跨皮肤或跨 ruleset 的严格体感合同；更换 mania 皮肤后应重新校准，且 mania / BMS 的下落时间不可交叉参考。
- Settings → 常规 → 安装位置 当前已把入口明确为 `更改数据目录位置`；选择空目录时会直接迁入当前数据内容，非空非数据目录会改用其下 `oms/` 子目录，若所选目录本身已是可用数据目录则只在重启后切换。该产品面只切换/迁移运行时数据根，不移动程序文件。
- `键音通道数` 设置项已于 2026-06-22 **删除**（键音池自动增长后无需手调，基线回落硬编码 32；详见 [P1-J CHANGELOG](../P1-J/CHANGELOG.md) 2026-06-22）。BMS settings surface 不再公开该滑条。
- BMS mod 选中状态与非默认配置现按 ruleset-local JSON snapshot 记忆，仅作用于 BMS；切到 mania 再切回或完全重启后仍恢复。
- 启动早期若 `RulesetConfigCache` 尚未 ready，`OsuGameBase` 现在会延后 replay 当前 ruleset 到 cache ready 后再做 BMS restore；这条 host-boundary 合同同时修复了冷启动首轮漏恢复与误报 ruleset failure。
- 实现 `IPreserveSettingsWhenDisabled` 的 configurable BMS mod 在 Song Select 中停用 / 启用不会丢配置；停用不再被视为“恢复默认值”。
- 首次启动向导当前已固定为六步 OMS flow：欢迎、UI 缩放、获取谱面、导入、难度表设置、按键绑定；这属于共享产品表面收口，而不是新的输入或存储主线。
- desktop 通用 Settings -> 输入 当前已主动隐藏 upstream 的数位板 / 触屏点击 / 鼠标 subsection；这是共享 settings-entry surface 的产品裁剪，不等于删除底层 input contract。
- 欢迎页、获取谱面页与导入页的可见文案现已切到 OMS-owned localisation namespace + `.resx`；若仍指向上游 localisation namespace，简中会继续读取上游翻译而不是代码 fallback。
- 共享层难度表设置页当前通过反射调用 `BmsDifficultyTableManager`，继续保持 `osu.Game` 与 `osu.Game.Rulesets.Bms` 的项目边界。
- `Playfield Scale` 已从 settings / runtime config 移除并固定为 `1.0`；原来的 `Playfield Horizontal Offset` 也已退出，改为四态 `Playfield Style`（`1P（居左）` / `2P（居右）` / `居中（左皿）` / `居中（右皿）`）这一 single-play playfield surface：当前只作用于 5K / 7K 的 playfield 停靠与 scratch 视觉侧别，其中 `1P / 2P` 为“侧停靠但保留固定屏侧间距”；不改变尺寸 / 可见时间语义，也不承担完整 `1P/2P flip` 的绑定与 side-aware skin 合同。
- `UI_PreStartHold` 现已承担“前 5 秒阻止开始 + 全程调速修饰键”这一统一 operator contract；`UI_LaneCoverFocus` 保持为 click-to-cycle 持久 target，且 HUD / skin boundary 与 legacy fallback 合同保持未破坏。
- 若后续加入 pre-start 视觉流速预览，宿主应落在第一非 scratch 普通轨的 playfield / lane visual surface，并继续复用 BMS note lookup / fallback，而不是 HUD / toast。
- `Sudden` / `Hidden` / `Lift` 现都暴露 `记忆游戏内变动` 开关；默认开启时局内滚轮调整会延续到回场后的 BMS mod 配置，关闭时保持 current-play-only。
- 当前 `IBmsHudLayoutDisplay` 为单一 `SetComponents(wrappedHud, gauge, combo)` 签名；**`IBmsHudLayoutDisplayWithGameplayFeedback` 变体与 legacy overlay wrapper 已于 2026-06-15 随速度反馈卡移除**（接口回到原始三件套，不实现该接口的旧 HUD layout 仍正常工作）。
- `BmsSkinTransformer` 在 `MainHUDComponents` 路径组装 gauge / combo；**speed feedback 注入已移除**。
- **（2026-06-20）gauge 默认摆位改为判定线下方矩形 groove-gauge**：`DefaultBmsHudLayoutDisplay` 把 gauge 放在判定线下方、与 playfield 条带等宽（`Width=PlayfieldWidth`）、随 `PlayfieldStyle` 做 P1/P2/居中侧锚、贴 `PlayfieldHeight`；`BmsGaugeBar` 圆角归零 + 加高 + 等分刻度 + 一体化（海军蓝背板、叠加 `NORMAL`/数值）。`PlayfieldWidth/keymode` 经 `GameplayState`、`PlayfieldStyle` 经 game 级 `IRulesetConfigCache.GetConfigFor(bms)` 解析，**未改 `SetComponents` 签名、gauge 仍是 HUD 子件**；无 `GameplayState`/config 宿主优雅降级居中。`BmsGaugeBar` 继承 `HealthDisplay`，已订阅 `HUDOverlay.ShowHealthBar` 重申满显、**免疫**通用血条/NoFail 开关。playfield **顶边贴屏幕边缘**（top-anchored `Y=0`，音符从屏顶出现）、`PlayfieldHeight 0.95→0.92`（判定线 92% 屏高、下方容纳 gauge；GN/时序不变；曾试 `PLAYFIELD_VERTICAL_OFFSET` 整体下移已删除）。`BmsComboCounter` 默认摆位移到 playfield 宽/高中线交点、并去掉背后 `body` 色块。
- `BmsGameplayFeedbackLayout` 现仅收口 **judgement 基线摆位**（`GetJudgementAnchor/Offset`、`ApplyJudgementDefaults`，供 `DrawableBmsJudgement` / `TestSceneBmsJudgementDisplayPosition` 用）；其 gameplay-feedback 摆位常量已随卡移除。
- **（2026-06-15 整体移除）** `DrawableBmsRuleset` 的 `GameplayFeedbackState` / `LatestJudgementFeedback` / `RecentJudgementFeedbacks` / `TimingFeedbackVisualRange` / `ExScorePacemakerInfo` 暴露面与 refresh/pacemaker 管线，连同 `DefaultBmsSpeedFeedbackDisplay` / `BmsGameplayFeedbackState` / `BmsJudgementCounts` / `BmsJudgementTimingFeedback` / `BmsExScoreProgressInfo` / `BmsExScorePacemakerInfo` / `BmsTimingOffsetSparkline`，均按产品决定删除。**保留**：`SpeedMetrics`（pre-start 预览）、调整目标状态（lane cover focus）、toast、BGA miss-flash、judgement 基线摆位。
- 当前 IIDX 参考文档仍明确要求：不要把现有 OMS speed feedback 对外包装成完整 `FHS`。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 子线归线到 `P1-A` | 已完成 | 主线文档已挂接 |
| 皮肤边界与 HUD 宿主审计 | 已完成 | 已明确当前模型与风险点 |
| 子线级计划 / 状态 / 约束文档 | 已完成 | 文档位于当前目录 |
| 首次启动向导与设置导流 | 已完成 | 主归属 `P1-A`，`P1-H` / `P1-B` 仅从属暴露 |
| `GameplayFeedbackDisplay` 合同设计 | 已移除（2026-06-15） | 常驻速度反馈卡及其 HUD-host overlay 变体、aggregate state contract 已按产品决定整体删除；如需重建须另立专题。HUD 宿主合同回到 wrapped HUD + gauge + combo |
| 常驻 GN HUD | 已移除（2026-06-15） | 随速度反馈卡删除；GN 仅留 toast / pre-start overlay，不再常驻 |
| `Sudden / Hidden / Lift` HUD 联动 | 进行中 | 宿主与默认摆位已稳定，target-state / cycle / `HOLD` 语义由 `P1-C` 推进；其在 toast 的展示保留 |
| pre-start 视觉流速预览宿主边界 | 已完成首轮实现 | playfield / lane host、第一非 scratch 轨宿主与 BMS note fallback 已接通；运行时 gate 与 pause 行为由 `P1-C` focused tests 锁定 |
| `BMS -> mania` 单向转谱公开表面 | 进行中 | visibility gate、persisted converted-star display 与 spread display 已接通；按钮文案、显式入口与更宽 surface proof 仍待后续收口 |
| `FAST/SLOW` / judge display / pacemaker 统一承载 | 已移除（2026-06-15） | 承载这套家族的常驻卡已删除；judgement 位置合同保留（供判定显示），feedback 家族如需重建须另立专题 |
| gauge 下移判定线 + 矩形化 + 等宽镜像 + playfield 顶边贴边（E1） | 已落地（2026-06-20，#1#2 实机验收通过） | gauge 判定线下方矩形 groove-gauge、等宽 + P1/P2/居中侧锚、一体化（导航海军蓝、叠加文案）；playfield **顶边贴屏幕边缘**（曾试整体下移 `PLAYFIELD_VERTICAL_OFFSET` 已删除）、`PlayfieldHeight 0.95→0.92`（GN/时序不变）；`SetComponents` 签名不变、gauge 仍在 HUD 合同内；BMS 933/933 |
| gauge 免疫 ShowHealthBar / NoFail | 已落地（2026-06-20） | `BmsGaugeBar : HealthDisplay` 会被通用"血条显示"开关（NoFail 等设 `ShowHealthBar=false`）淡出隐藏；订阅 `ShowHealthBar` 重申 `Alpha=1` 始终显示（核心游玩信息）。回归 `TestGaugeBarStaysVisibleWhenHealthBarHidden`（须真实 HUDOverlay 才能复现） |
| combo 移到 playfield 中心 + 去背景色块 | 已落地（2026-06-20，实机验收通过） | `applyComboPlacement` 放 playfield 宽/高中线交点（随 PlayfieldStyle 镜像）；`BmsComboCounter` 去掉 `body` 色块容器只留居中标签 + 数字。回归 `TestComboCentredOnPlayfield` |
| 从默认皮肤配置移除 leaderboard + 重复默认 combo | 已落地（2026-06-20，实机验收通过） | `BmsSkinTransformer.stripDefaultHudElements` 在装配期把 wrapped HUD 里的 `LegacyDefaultComboCounter` + `DrawableGameplayLeaderboard` 从配置树移除（非隐藏）；BMS combo / 全局 score 保留。回归 `TestRulesetHudStripsDefaultComboAndLeaderboard` |
| 皮肤创作生态 `F0`：组件契约 + ini schema 草案 + 必备分档冻结（纯文档） | 已落地（2026-06-27） | 权威源进 CONSTRAINTS「皮肤创作生态」节 + PLAN `F` 系列；`SKINNING.md` 重置为派生制作者视图；mainline 已回写立项。**未开工实现** |
| 皮肤创作生态 `F1`：ini 数据层 → 配置源 → 最小闭环 → ①类铺开 → reference skin | 进行中（**主面已成**：①解析 + ②配置源 + ③**颜色/纹理/几何三轴皮肤化** + **reference 验收 capstone** 均落，1001/1001 + core gate 绿；剩仅净新增件 stage/`KeyImage`） | gate 定方案 A。**①刀** ini 解析三件套（不侵入核心）+ **②刀** `BmsLegacySkin`（`ParseConfigurationStream` hook + `GetConfig`）+ 接入（`SkinImporter` 路由·最小 core 改动 + fallback 保护）已落。**③刀（本会话六刀，986→998）**：**颜色 + 纹理**——所有现存渲染件接 ini（note 家族 + lane bg/divider[`Box→CompositeDrawable`·贴图优先/颜色回退·抽共享 `BmsSkinnableVisual.Resolve`·per-lane 分桶] / hit target[composite 6 颜色 + `HitTargetImage` 贴图·glow 提字段防 layout 冲] / bar line[提成 `DefaultBmsBarLineDisplay`·颜色-only] / lane cover[3 颜色 + top/bottom 贴图·keymode 经 `GameplayState` 自解析] / backdrop[贴图>谱面背景>平涂] / baseplate[颜色]）；**几何**——`BmsPlayfield.applySkinGeometry`（`[Resolved] ISkinSource` 读 11 几何键→`CreateDefault` override 重建 profile→`CreateFor`·`HitTargetVerticalOffset` 锁 0 守时序·`if(!anyOverride)return` 保非皮肤路径字节一致）+ LN body 宽（件内读）。**reference skin 验收 capstone**——创作者模板 [oms-bms-reference-skin/skin.ini](../../other/oms-bms-reference-skin/skin.ini)（7K 全键 + 注释）+ 自校验门 `BmsReferenceSkinTest`（逐键断言 ini 解析值 == 真实 palette/profile 常量·写错或默认漂移即失败·并证全键 round-trip）。BMS **1001/1001**（含 reference 2 用例）；`TestLegacyTranscodeFailureBecomesUnavailableAndLeavesNoPartialFile` 为间歇 BGA flaky·已证与本工作零因果（属 P1-L）。SKINNING.md 已据代码更正。**待（均净新增件·宜独立评估）**：stage 框架 + `KeyImage`（无现成件·新增组件 + lookup 接线·`KeyImage` 与现"无物理按键区"设计冲突） |
| 皮肤后续路线（`G` 系列 + `F2` 扩写） | `G1` 进行中（2026-06-29 立项·**刀①+②+③已落**：① folder-backed 直读建块 + ② `SkinInfo` realm 载体·schema 55→56 + ③ **`SkinManager.GetSkin` folder 分支（D4 反射三参 ctor·守卫测试·非 folder 零变化·2026-07-04）**；1003/1003 + gate 绿·下一刀④ `chartskin/` 文件夹导入器/扫描器）；`F2`/`G2`/`F3` 立项待续 | 应用户三问勘察落账（见 [PLAN](DEVELOPMENT_PLAN.md) F2/G1/G2 + [CHANGELOG](CHANGELOG.md) 2026-06-29）：**① 皮肤存储**＝现走核心 `SkinImporter`→realm hash-backed `files/`（不可读/不可手管）·无 chartbms 式可视文件夹→立 **`G1`**（仿 `BmsFolderImporter` 建 `chartskin/` 可视文件夹·**revisit F1「复用 SkinManager·不走 chartbms」gate**）；**② 默认皮肤**＝确认 100% 程序化·reference skin.ini 仅模板未接默认→立 **`G2`**（文件型默认·可选·保留程序化兜底）；**③ IIDX 还原度**＝**部分**·静态件可换皮但 turntable/keyflash/explosion/bomb/ghost **全仓零渲染**（盘面亦无 turntable 区/键区）→ 扩写 **`F2`**（还原 IIDX 真正大头·未开工）。**G 与 F 正交**（G=文件如何存放·F=什么可被控制）。 |

## 当前风险

- **接口破坏风险**：如果直接改 `IBmsHudLayoutDisplay.SetComponents(...)` 签名，会立刻打断现有自定义 HUD provider。
- **术语冒进风险**：如果把当前常驻 GN 直接写成完整 `FHS`，会与 IIDX 参考约束冲突，也会误导用户对当前模型的预期。
- **边界污染风险**：如果为了赶功能把 speed feedback 偷塞进 `GaugeBar`、`ComboCounter` 或 wrapped HUD 子节点，后续 `FAST/SLOW` / pacemaker 将继续复制同类问题。
- **preview 宿主污染风险**：如果 pre-start 视觉流速预览被塞进 HUD / toast 或误复用 mania lookup，就会把 playfield 视觉 preview 变成错误的宿主边界问题。
- **布局扩散风险**：如果不先冻结 judgement / feedback 的位置合同，后续容易继续用新的硬编码偏移叠层。

## 下一检查点

1. **（已作废，2026-06-15）** 原「评估 richer judge display state 是否进入 `GameplayFeedbackState` contract」一项随该 contract 与常驻速度反馈卡整体删除而失效；如未来重建 gameplay 反馈家族须另立专题重新设计，不再沿用已删的 snapshot 合同。
2. `BMS -> mania` 公开表面当前已不再以 raw star 驱动 Song Select selector 或 spread dots；下一刀应转向 explicit wording 与更宽 presentation/manual proof，而不是回头重做 current-ruleset star surface。
3. 若启动 pre-start 视觉流速预览切片，先补 playfield / lane host、第一非 scratch 轨解析与 note fallback 路径，再把可见性 gate 与“无判定副作用”语义交给 `P1-C` focused validation。
4. 维持 `OmsSkin` 默认路径、legacy HUD wrapper 与 fallback 语义稳定，并把 remaining full Floating parity 缺口明确留在后续路线，不在 `P1-A` 里误写成已完成。

## 历史变动与验证

- 当前仍影响判读的验证结论已在“当前阶段”“进度矩阵”与“下一检查点”中汇总；按日期展开的宿主改动、回归命令与构建记录见 [CHANGELOG.md](CHANGELOG.md)。
