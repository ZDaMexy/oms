# P1-A 变动日志

> 本文件只记录 `P1-A` 子线已确认、已验证或已完成挂接的变更摘要。

## 2026-06-20

### playfield 顶边贴屏幕边缘 + combo 移到 playfield 中心并去背景色块（用户实机三连改之一二）

- **playfield 顶边回到屏幕边缘**：上一版用 `PLAYFIELD_VERTICAL_OFFSET=0.06` 把整条 play 立柱下移，导致顶边离开屏幕顶、留出 header 空带，不符合 green-number「音符从屏幕最顶出现、整屏可见场 = 顶边→判定线」语义。本次**删除 `PLAYFIELD_VERTICAL_OFFSET`**，playfield 恢复纯顶部锚定（`Y=0`）；为保持判定线/gauge 仍停在原低位（~0.92 屏高），把 `DEFAULT_PLAYFIELD_HEIGHT 0.86→0.92`（顶边贴边 + 场更高、音符从顶出现）。判定时序不变量不变（`HitTargetVerticalOffset=0` → `scrollLengthRatio≡1` → `TimeRange`/GN 与场高无关，仅像素扫过距离变）。`gauge_top = DEFAULT_PLAYFIELD_HEIGHT + 0.002`（不再含 offset 项）。同步 `BmsLaneLayoutTest` / `TestSceneBmsPlayfieldLayoutConfig`（0.86→0.92）、`TestSceneBmsHudGaugePlacement`（去掉 offset 项）。
- **combo 移到 playfield 中心 + 去背景色块**：`DefaultBmsHudLayoutDisplay` 新增 `applyComboPlacement()`，把 `BmsComboCounter` 放到 **playfield 宽/高中线交点**（水平＝按 `PlayfieldStyle` 的 P1 左 / P2 右 / 居中得 playfield 横向中心、复用与 gauge 同一套 `PlayfieldWidth` + inset；垂直＝`PlayfieldHeight/2`），`Anchor=TopLeft, Origin=Centre, RelativePositionAxes=Both`，随 PlayfieldStyle live 重定位；无 GameplayState/config 宿主降级屏幕中心。`BmsComboCounter` 去掉 `TextComponent` 里的 `body` 色块容器（background 渐变 / glow / accentStrip / 圆角边框），只留居中的 `COMBO` 标签 + 数字（pulse/flash 改作用在数字上、带 Shadow）。
- 回归：`TestSceneBmsHudGaugePlacement.TestComboCentredOnPlayfield`（combo Origin=Centre、相对定位、居中 X=0.5、Y=PlayfieldHeight/2）。验证：BMS 全套 **930/930**、`osu.Desktop.slnf` Release **0 错误**。**人工实机视觉验收待用户确认**。

### 修复：BMS gauge 被通用"血条显示"开关误隐藏（用户实机"gaugebar 没了"）

去掉默认 combo / leaderboard 后用户报「gaugebar 没了」。一轮日志驱动诊断（先排除 strip：真实 `BmsGaugeBar` 在 strip 后布局里仍可见 → 不是 strip）后定位真因：**`BmsGaugeBar : HealthDisplay`，而 `HealthDisplay.LoadComplete` 把自身绑到 `[Resolved] HUDOverlay.ShowHealthBar`，`ShowHealthBar==false` 时 `this.FadeTo(0)` 把 gauge 淡到透明**。某处（NoFail 等通用"隐藏血条"开关 / 设置）把 `ShowHealthBar` 设 false → BMS groove gauge 被一起隐藏（而 combo 不受影响，故"combo 在、gauge 没"）。诊断盲区：之前的 gauge 摆位测试没有真实 `HUDOverlay`（`hudOverlay` 解析为 null → `showHealthBar` 恒 true → gauge 恒可见），掩盖了该路径。

- **修复**：`BmsGaugeBar` 解析 `[Resolved(CanBeNull)] HUDOverlay`，在 `LoadComplete` 里订阅 `ShowHealthBar` 变化并 `FinishTransforms()+Alpha=1` 重申满显——BMS groove gauge 是核心游玩信息，**免疫** 通用血条开关，始终显示。该订阅在 base 之后注册（bound-copy 订阅者先于 own 订阅者触发），故稳定压过 base 的淡出；HUD 整体 `ShowHud` 淡入仍经父级生效、不受影响。
- 回归：`TestSceneBmsSoloPlayerPreStart.TestGaugeBarStaysVisibleWhenHealthBarHidden`（真实 Player+HUDOverlay，置 `ShowHealthBar=false` 后断言 gauge 仍可见——去掉修复即失败）；另补 `TestSceneBmsHudGaugePlacement` 的 `TestRealGaugeLoadsAndIsVisible` / `TestRealGaugeVisibleAlongsideStrippedWrappedHud`（真实 gauge 在布局/strip 后可见）。验证：BMS 全套 **929/929**、`osu.Desktop.slnf` Release **0 错误**。

### gauge 下移到判定线下方 + 矩形化 + 等宽镜像 playfield（游玩区抬高，视觉/摆位，P1-A E1）

把原先摆在 playfield 顶部的圆角胶囊 gauge 改为 IIDX groove-gauge 观感的矩形条，落在判定线下方、与判定区等宽并随 P1/P2/居中侧锚；同时抬高游玩区腾出下方空带。用户在规划阶段选定「抬高 0.86 / gauge 等宽」。

- **抬高游玩区**：`BmsPlayfieldLayoutProfile` 默认 `PlayfieldHeight 0.95 → 0.86`（提为公开常量 `DEFAULT_PLAYFIELD_HEIGHT`，仍是 strict profile 唯一杠杆，config `PlayfieldHeight` 维持被忽略）。判定线上移到 86% 屏高、下方 ~14% 空带容纳 gauge。**判定时序不变**：`HitTargetVerticalOffset=0` 时 `BmsHitObjectArea.scrollLengthRatio≡1`、`TimeRange` 与场高无关，仅落条像素扫过距离变短（视觉略密），GN / 判定窗口完全不变。
- **gauge 矩形化**：`BmsGaugeBar` 圆角 `CornerRadius 10→0`、bar 高 `20→28`、数值字号 `14→18`，新增 10 等分极淡竖向刻度（`Opacity 0.08`）营造 groove-gauge 观感（不做 IIDX 逐格细节）；填充 / floor band / clear 标记 / 高光与 `NORMAL`+`20%` 文案均保留。
- **gauge 下移 + 等宽 + 侧锚镜像**：默认摆位由 `DefaultBmsHudLayoutDisplay` 负责——gauge `RelativeSizeAxes.X` + `Width = PlayfieldWidth`（与 lane 条带等宽）、`RelativePositionAxes.Both` + `Y = PlayfieldHeight + 0.012`（顶边贴判定线下方）、Anchor/Origin/X 按 `PlayfieldStyle.GetAppliedStyle(keymode)` 做 P1 左 / P2 右 / 居中（复用 `BmsPlayfield.SIDE_ANCHORED_HORIZONTAL_INSET`，与 lane 严格同列）。combo 暂留原位。
- **合同保持（满足「HUD 宿主约束 1」）**：gauge 仍留在 `IBmsHudLayoutDisplay.SetComponents(wrappedHud, gauge, combo)` 合同内，**未改签名**、未迁出 HUD。所需几何经 HUD 可见的 DI 通道取得：`PlayfieldWidth / keymode` 经 `[Resolved] GameplayState` 可玩谱面（`BmsLaneLayout.CreateFor`）、`PlayfieldStyle` 经 game 级 `IRulesetConfigCache.GetConfigFor(bms)`（与 playfield 子树同一 `BmsRulesetConfigManager` 实例，绑定 live 变化）；两者均 `CanBeNull` 解析，皮肤编辑器预览 / 测试等无 `GameplayState`/config 的宿主优雅降级（居中 + 兜底宽度 `0.4`），不抛异常。
- 仅视觉 / 摆位 / 几何，不碰判定 / 计分 / 滚动 / 键音 / chartbms 直读。`BmsPlayfield` 的 `side_anchored_horizontal_inset` 提升为公开常量 `SIDE_ANCHORED_HORIZONTAL_INSET` 供 HUD 复用（值不变）。
- 测试同步：`PlayfieldHeight 0.95→0.86`（`BmsLaneLayoutTest`、`TestSceneBmsPlayfieldLayoutConfig` 两处断言）；新增 `TestSceneBmsHudGaugePlacement`（等宽 / 判定线下方 / 居中 / P1 侧锚镜像）。`BmsSkinTransformerTest` 的「HUD 含 gauge」回归保持绿（gauge 仍是 HUD 子件）。验证：BMS 全套 **925/925**、`osu.Desktop.slnf` Release **0 错误**。

### （其二）gauge 与 playfield 一体化跟进（用户实机反馈）

首轮实机后用户反馈「间隙再贴紧 + gauge 别像外挂控件、要和 playfield 一体」。本跟进只动 `BmsGaugeBar` 视觉与摆位间隙，几何/合同链路不变：

- **贴紧判定线**：`DefaultBmsHudLayoutDisplay` 的 gauge 顶边偏移 `PlayfieldHeight + 0.012 → +0.002`（近乎贴住判定线下方）。
- **背景与 playfield 衔接**：gauge 背板改用海军蓝渐变 `BmsDefaultHudPalette.GaugeTrackTop(26,32,48) → GaugeTrackBottom(13,19,31)`，落在 playfield lane/baseplate 色域内，使 gauge band 读作 playfield 立柱的底段而非独立卡片。
- **去边框 + 单条 band**：移除 gauge 四周 1px 边框，改为仅在顶边一条 gauge-accent 着色 1px hairline（`topAccent`）作为"表头"提示；填充条占满整条 band。
- **label/value 叠加**：取消浮在空隙里的独立 header 行，把 `NORMAL` 标签（左中）与百分比数值（右中，字号 18→20）叠加在 band 上（IIDX groove-gauge 式），均加 `Shadow` 保证压在填充色上的可读性；band 高 `28→34`。
- 新增调色 `GaugeTrackTop/GaugeTrackBottom`（不动既有 `TrackBackground`/被分布图复用的 `TrackShade`）。验证：BMS 全套 **925/925**、Release **0 错误**。

### （其三）整条 play 立柱整体下移（用户实机反馈，标注目标位）

> ⚠️ **已于同日撤销**：用户随后要求"playfield 顶边贴屏幕边缘"，本节引入的 `PLAYFIELD_VERTICAL_OFFSET` 已删除、改为提高 `PlayfieldHeight` 到 `0.92`（顶边贴边、判定线/gauge 仍停在 ~0.92）。**当前真相见下方「playfield 顶边贴屏幕边缘」一节**；本节仅留作迭代历史。

用户实机标注希望「整体往下移动」到近屏底。新增共享常量 `BmsPlayfieldLayoutProfile.PLAYFIELD_VERTICAL_OFFSET = 0.06`，把 **playfield 条带 + gauge 一体下移**：`BmsPlayfield.playfieldContainer` 顶部锚定后置 `Y=OFFSET`（`RelativePositionAxes` X→Both；顶边不再贴屏幕顶、留 header 空带），`DefaultBmsHudLayoutDisplay.gauge_top` 同步加该 OFFSET，使判定线落在 `≈0.92` 屏高、gauge 紧贴其下止于近屏底。两者共用同一常量保证不错位。`PlayfieldHeight` 仍 `0.86`、判定时序不变量不受位移影响（位移只改像素扫过位置、不改 GN / 窗口）；lane 高占比断言仍 `0.86`（高度未变），`TestSceneBmsHudGaugePlacement` 的"判定线下方"断言改用 `OFFSET+0.86`。验证：BMS 全套 **925/925**、Release **0 错误**。**人工实机视觉验收待用户确认**（位移幅度 `0.06` 为单一可调常量）。

### BMS gameplay 从默认皮肤配置中移除游玩排行榜与重复（默认）连击数（用户反馈）

用户要求把「默认皮肤左下角连击数 + 左侧排行榜」**从默认皮肤配置中删去（非运行时隐藏）**。两者**同源**：上游 `LegacySkin.GetDrawableComponent` 的 ruleset-`MainHUDComponents` 默认布局里直接 `new LegacyDefaultComboCounter()` + `new DrawableGameplayLeaderboard()`（[LegacySkin.cs:420/422](../../../osu.Game/Skinning/LegacySkin.cs)），经 `BmsSkinTransformer` 包成 BMS HUD 的 wrapped 层。中央金色 combo 是 BMS 自有 `BmsComboCounter`、保留；右上 score 等来自全局（Ruleset==null）层、不受影响。

- **修复＝装配期移除**：`BmsSkinTransformer` 在 wrap BMS `MainHUDComponents` 时调 `stripDefaultHudElements(wrappedHud)`，把 wrapped 容器直接子里的 `ComboCounter` / **`LegacyDefaultComboCounter`** / `DrawableGameplayLeaderboard` **从配置树移除**（`Container.Remove(..., true)`），三类根本不进入 BMS HUD 树（不渲染、不进皮肤编辑器序列化、无首帧闪烁）。BMS combo 是 SetComponents 另行添加、不在 wrapped 层，故移除 wrapped 层所有 combo 安全；对无这些件的皮肤优雅 no-op。
- **坑（首次实机暴露）**：上游默认连击是 **`LegacyDefaultComboCounter : CompositeDrawable, ISerialisableDrawable`，并非 `ComboCounter` 子类**——只匹配 `ComboCounter` 时 leaderboard 被删、连击仍在。故 strip 必须显式包含 `LegacyDefaultComboCounter`，回归测试也用真实类型而非 `: ComboCounter` 的假替身（后者会误过）。
- **回退前一版的"隐藏"式尝试**：撤掉 `BmsSoloPlayer.Configuration.ShowLeaderboard=false`（及其 `TestGameplayLeaderboardSuppressed`）与 `DefaultBmsHudLayoutDisplay` 的 foreign-combo 有界重试隐藏（恢复原一次性循环，仅作残留 combo 兜底）——改用单一"配置移除"机制。
- 回归：`BmsSkinTransformerTest` 新增 `TestRulesetHudStripsDefaultComboAndLeaderboard`（wrapped HUD 放入 combo+leaderboard → 装配后从 wrapped 层移除、BMS combo 保留）。验证：BMS 全套 **926/926**、`osu.Desktop.slnf` Release **0 错误**。**人工实机确认左下角连击与左侧排行榜消失**。

## 2026-06-15

### BMS 默认皮肤几何二调：宽度回宽 10%、SCRATCH = 键轨 2 倍、音符贴顶无空隙

- **整体宽度 +10%**：`BmsPlayfieldLayoutProfile` 的 `PlayfieldWidth` 系数 `0.75 → 0.825`（原始 ×0.75 后再 ×1.1，覆盖上一条的 −25% 净值为 −17.5%）。
- **SCRATCH 轨 = 键轨 1.5 倍宽**：`ScratchLaneRelativeWidth` `1.25 → 1.5`（先定 2 倍，随即按口径再缩 25% 落到 1.5；归一化分配，scratch:key = 1.5:1）。
- **音符贴屏幕顶、无空隙**：`BmsPlayfield` 的 `playfieldContainer` 由居中锚定改为**顶部锚定**（初始 `TopCentre` + `applyPlayfieldStyle` 的 P1→`TopLeft`/P2→`TopRight`/居中→`TopCentre`），`PlayfieldHeight 0.9 → 0.95`。顶边贴屏幕顶（音符从顶部出现），底边/判定线仍在 95% 屏高（位置不变）。判定时序不受影响（GN = 可见时间 = `TimeRange`，与场高无关；场高只改像素扫过距离）。
- 仅几何/视觉。测试同步：`BmsLaneLayoutTest`（14K 宽 0.6→0.66、高→0.95）、`TestSceneBmsPlayfieldLayoutConfig`（8 轨宽 0.36→0.396、高→0.95、scratch 1.25→1.5、实测 scratch:key 比→1.5、lane 高占比→0.95）。验证：BMS 全套 **907/907**、Release 0 错误。

### BMS 默认皮肤：单轨/音符宽度 −25%、音符厚度 +25%、长条身宽 +25%（视觉/几何默认）

- **单轨宽度 −25%（音符随轨 −25%）**：lane 物理宽由「归一化占比 × `PlayfieldWidth`」决定（见 `BmsPlayfield.applyLaneBounds`），相对宽缩放会被归一化抵消，故唯一物理杠杆是 `PlayfieldWidth`；`BmsPlayfieldLayoutProfile.CreateDefault` 默认 `playfieldWidth` 乘 `0.75`，整条 playfield 连同 lane/音符等比收窄 25%、不引入新间隙。
- **音符厚度 +25%**：`DrawableBmsHitObject` 音符条高 `18 → 22.5`（普通音符 + 长条头/尾盖；长条父件 `28` 为非时长 fallback、被滚动容器覆盖，非可见厚度，不动）。
- **长条身宽 +25%**：`DefaultBmsLongNoteBodyDisplay.Width` `0.42 → 0.525`（相对 lane 宽）。
- 仅几何/视觉，不碰判定/计分/滚动。`PlayfieldWidth` 配置项仍是被忽略的 disabled 设置（strict profile），本次只改 profile 默认。测试同步：`BmsLaneLayoutTest`（14K 0.8→0.6）、`TestSceneBmsPlayfieldLayoutConfig`（8 轨 0.48→0.36）、`BmsSkinTransformerTest`（长条身宽 0.42→0.525 ×2）。验证：BMS 全套 **907/907**、Release 0 错误。

### BMS 默认皮肤：长条去掉尾端标识（视觉默认）

`DefaultBmsLongNoteTailDisplay`（长条释放端全宽亮色端盖）改为 `Alpha = 0`。长条 body 细竖条本就被滚动容器按 hold 时长拉满整段，隐藏尾盖后 body 仍延伸到释放端、不留空缺；最终样式＝头端亮盖 + body 延伸、无尾盖。仅改默认渲染：tail 仍是判定对象（判定/计分不受影响），`BmsNoteSkinElements.LongNoteTail` 组件与 `GetLongNoteTail` 调色保留，皮肤作者可覆盖。验证：`BmsSkinTransformerTest` + `BmsDrawableRulesetTest` **163/163**、Release 0 错误。

### BMS HUD 宿主合同简化：移除 gameplay-feedback overlay 变体（随 P1-C 速度反馈卡删除）

`P1-C` 按产品决定删除常驻速度反馈卡后，`P1-A` 拥有的 BMS HUD 宿主合同同步收窄：移除 `IBmsHudLayoutDisplayWithGameplayFeedback` 变体、`DefaultBmsHudLayoutDisplay.WrapWithGameplayFeedback` 与 transformer 的 legacy overlay 包装分支，`IBmsHudLayoutDisplay` 回到单一 `SetComponents(wrappedHud, gauge, combo)` 合同。`BmsGameplayFeedbackLayout` 收窄为只负责 **judgement 基线摆位**（`GetJudgementAnchor/Offset`、`ApplyJudgementDefaults`，仍由 `DrawableBmsJudgement` 与 `TestSceneBmsJudgementDisplayPosition` 消费）；其 gameplay-feedback 摆位常量 `DefaultGameplayFeedbackPosition`/`ApplyGameplayFeedbackDefaults` 已删除。删除细节与功能影响见 [P1-C CHANGELOG](../P1-C/CHANGELOG.md) 2026-06-15。HUD fallback 红线不变（默认路径 / 无该组件用户皮肤 / 旧接口用户皮肤三条回归仍由 `BmsSkinTransformerTest` 守）。验证：BMS 全套 **907/907**、Release 0 错误。

### 修复：皮肤布局编辑器进 BMS gameplay 报错（HUD 宿主组件序列化往返断裂）

- **背景**：审查皮肤编辑器链路时，用户实机日志暴露进 BMS gameplay 预览（`SkinEditorOverlay+EndlessPlayer`）时两处 error，均指向 `osu.Game.Rulesets.Bms.UI.DefaultBmsSpeedFeedbackDisplay`：`SkinComponentToolbox.attemptAddComponent`（组件 toolbox 反射实例化）与 `SerialisedDrawableInfo.CreateInstance`（用户皮肤布局重建）。
- **根因**：该速度反馈卡（P1-C 拥有）实现 `ISerialisableDrawable`，但唯一构造是全可选参数 `(IBindable?=null, IBindableList?=null)`；`Activator.CreateInstance(type)` 只匹配真正零参构造，全可选签名抛 `MissingMethodException`。`SkinnableContainer.Reload` 又会把 `BmsSkinTransformer` 在 `MainHUDComponents` 注入的 HUD 子件（gauge/combo/speed feedback）作为 `Components` 序列化进皮肤，故任何在 BMS HUD 上的编辑保存后、重载即崩。姊妹件 `BmsGaugeBar`/`BmsComboCounter` 均有无参构造、往返正常，唯独此卡缺失。
- **修复**：① 给 `DefaultBmsSpeedFeedbackDisplay` 补显式无参构造（链到现有构造，双参去掉可选默认值；双参唯一调用点是 `TestSceneBmsSpeedFeedbackDisplay`，零参调用点是 transformer，均无影响）；② `SerialisedDrawableInfo.GetAllAvailableDrawables` 增加"必须有公开无参构造"过滤，作为编辑器对所有 ruleset 的防御性收口。`IsEditable` 维持与 gauge/combo 一致（默认可编辑），不改编辑器可选面语义。
- **范围说明**：日志中 `VideoDecoder faulted`（被预览谱面的 BGA 视频）与缺失 jacket 属谱面自身、已优雅降级，非编辑器缺陷，本次不处理。皮肤编辑器链路本身仍是治理空白（`SKINNING.md` / 本子线尚未把编辑器作为正式 authoring surface 纳入约束），后续若把编辑器升格为皮肤自定义入口需另立专题。
- **验证**：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-build -c Release --filter "FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **121/121**；`dotnet build osu.Desktop.slnf -p:Configuration=Release` **0 错误**。

## 2026-05-26

### BMS -> mania 公共表面：persisted converted-star display 与 spread display 收口

- `BMS -> mania` 公开表面当前已不再只停留在 visibility gate：modless converted mania 星数现已改为持久化到 BMS metadata payload，并由 `BeatmapDifficultyCache`、`BackgroundDataStoreProcessor` 与 current-ruleset spread display 统一读取，因此 Song Select 的星数筛选、难度排序、按星数分组与 spread dots 都不再继续直接吃 source BMS raw star。
- 这一步仍保持 `P1-A/P1-K` 边界：`P1-K` 继续拥有 dedicated conversion、sample-only scratch runtime 与 persisted-star authority，`P1-A` 只消费 current-ruleset resolved-star display surface，不把 generic convert heuristics 重新包装成语义 authority。
- 当前剩余工作已收窄为按钮 wording、显式入口文案与更宽 presentation/manual proof，而不是再回头修 current-ruleset star surface。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore --filter "Name~BmsStarRatingResolverTest|Name~BeatmapCarouselFilterSortingTest"` **19/19** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

## 2026-05-16

### 实现：pre-start 1 号普通轨纯视觉流速预览宿主接到 `P1-A`

- `BmsHitObjectArea` / `BmsLane` 现已提供独立 preview 容器，pre-start 视觉预览不再需要借道 HUD / toast / mania lookup。
- `DrawableBmsRuleset` 现会把 skinnable fake note 固定挂到第一非 scratch 普通轨，并继续复用 BMS note fallback。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~TestSceneBmsSoloPlayerPreStart"` **24/24** 通过。

### 文档规划：pre-start 1 号普通轨纯视觉流速预览的宿主边界归到 `P1-A`

- 已明确该 feature 的 `P1-A` 职责只包括 playfield / lane 宿主、skin fallback 与产品表面，不拥有判定 / 计分 / 键音语义本身。
- 文档现已把 preview 宿主冻结为 BMS-owned playfield / lane visual surface：继续复用 BMS note lookup / fallback，不准塞进 HUD / toast，也不准误用 mania lookup。
- 本轮仅更新文档与 memory，无生产代码变更、无新增测试执行。

## 2026-05-09

### shared installation surface 跟进：数据目录迁移入口与结果说明收口

- Settings → 常规 → 安装位置 现已把入口明确为 `更改数据目录位置`，不再把实际只切换/迁移运行时数据根的功能误写成移动程序文件。
- 迁移选择页当前会直接说明三类结果：空目录直接迁入、非空非数据目录改用其下 `oms/` 子目录、已是可用数据目录则仅在重启后切换；这条产品面合同也已同步到 Release / 主线 / `P1-H` 文档口径。
- 验证：`dotnet build .\osu.Game\osu.Game.csproj -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

## 2026-05-09（续）

### shared settings-entry surface 跟进：osu!mania 滚动速度提示收口为参考值

- `ManiaSettingsSubsection` 现已为 `滚动速度` slider 补上 hover 提示，明确括号毫秒只代表标准车道几何下的参考下落时间。
- 不同 mania 皮肤可通过车道尺寸、判定线位置与缩放改变可见下落长度，因此同一数值不保证跨皮肤体感一致；更换皮肤后应按当前皮肤重新校准，且 mania / BMS 的下落时间不可互相参考。
- 这次改动不修改 `DrawableManiaRuleset.ComputeScrollTime()` 或 mania runtime authority，只收口 settings-entry surface 的解释边界。
- 验证：`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### gameplay settings-entry surface 跟进：Hi-Speed 模式说明与基础下落时间收口

- `BmsSettingsSubsection` 现已把 `Hi-Speed 模式` 的 hover 文案改为三种模式的功能区别简述：`Normal` 为基础定速、`Floating` 为按谱面初始 BPM 做补偿、`Classic` 为传统 Hi-Speed 语义。
- 当前模式的 Hi-Speed slider 现会在数值后显示括号内的基础下落时间（ms）；该数值明确按“不启用 `Sudden / Hidden / Lift`”计算，不再与 runtime `GreenNumber` / 可见时间混写。
- 当前提示文案也已收口为“括号内为不启用 sudden/hidden/lift 的下落时间（ms），绿字（GreenNumber）需要在游戏内结合 sudden/hidden/lift 调节查看”。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsRulesetConfigurationTest"` **12/12** 通过。

### gameplay settings-entry surface 跟进：BMS 键音通道默认值与悬浮提示收口

- `BmsKeysoundStore.DEFAULT_CONCURRENT_CHANNELS` 现已从 `16` 提高到 `32`；`Settings -> 游戏模式 -> BMS -> 键音通道数` 继续作为 shared keysound pool ceiling 的 `1..256` 调节入口。
- `BmsSettingsSubsection` 现为该滑条补上多行 hover 提示，直接概括低值更容易截断 BGM / 键音 / 长按尾音，高值更适合极高密谱面或较强机器；由于默认值已经是 `32`，缺音时的上调建议现已明确收口为 `48/64`。
- 这次改动属于共享 settings-entry surface 的默认值与文案收口，不改写 `BmsKeysoundStore` 的 runtime authority；BGM / note / LN / lane replay 仍共用同一池，运行时改值仍会切断当前正在播放的键音。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsRulesetConfigurationTest|FullyQualifiedName~BmsDrawableRulesetTest"` **70/70** 通过。

### shared settings-entry surface 跟进：桌面端输入设置安全隐藏 upstream mouse/touch/tablet 分区

- `OsuGameDesktop` 现已 override `CreateSettingsSubsectionFor(InputHandler)`，在 desktop Settings -> 输入 中对 `ITabletHandler`、`TouchHandler` 与 `MouseHandler` 返回 `null`，因此上游通用的数位板 / 触屏点击 / 鼠标 subsection 不再继续暴露给最终桌面产品面。
- 该改动明确是共享 settings-entry surface 的 **安全隐藏**，不改变 `MouseDisableButtons` / `MouseDisableWheel` / `ConfineMouseMode` / `TouchDisableGameplayTaps` 等既有 runtime config 消费链，也不移除 tablet/touch/mouse handler。
- 裁剪保持在 `OsuGameDesktop` 层，不下移到 `OsuGameBase`，从而继续保留 test scene / 非 desktop host 的设置装配合同。
- 验证：`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

## 2026-05-08

### gameplay speed setting 跟进：`阻止谱面开始/ingame start` 宿主语义收口

- `BmsInputStrings.PreStartHold` 的设置面可见名称现已改为 `阻止谱面开始/ingame start`；默认键位与 `UI_LaneCoverFocus` 的独立 click-to-cycle 语义保持不变。
- `BmsSoloPlayer` 现把 `UI_PreStartHold` 收口为“前 5 秒阻止开始 + 全程调速修饰键”这一宿主合同：右侧 `READY HOLD` overlay 继续只保留给前 5 秒阻止开谱窗口，正式 gameplay 开始后按住同一键仍会继续调速，并持续刷新居中的 `BMS speed` toast。
- `BmsInputManager` 现会在 hold 修饰键按住期间停止把新的 lane action 转发进 gameplay `KeyBindingContainer`，因此同一组 lane 键在 hold 期间只承担 Hi-Speed 调节，不再同时进入正常判定链。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~OmsInputRouterTest"` **9/9** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~TestSceneBmsSoloPlayerPreStart"` **10/10** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~OmsInputBridgeTest"` **23/23** 通过。

## 2026-04-28

### onboarding surface 跟进：难度表预设导入失败提示中文化

- `ScreenBehaviour` 现继续通过反射调用 `BmsDifficultyTableManager`，但导入 zris 预设失败时不再直接把英文异常透传给用户；首次启动页与 BMS settings 现统一复用 `DifficultyTableImportErrorFormatter`，把超时、HTTP 失败与格式错误收口为中文分类提示。
- 首次启动页在一次导入多张预设失败时，状态文字与通知现在都会展示失败摘要和前几条具体原因，而不是只停留在“成功/失败个数”。
- 该改动维持 `P1-A` 的共享 onboarding surface 归属，不改变 `osu.Game -> osu.Game.Rulesets.Bms` 的反射边界，也不把难度表后端实现重新归线到共享层。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsDifficultyTableManagerTest" --logger:"console;verbosity=normal"` **12/12** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### gameplay speed setting 跟进：pre-start overlay owner contract 与真实宿主绑定回归补强

- `TestSceneBmsPreStartHiSpeedOverlay` 现单独锁住 `BmsPreStartHiSpeedOverlay` 的 owner contract：mode text / value text 必须继续反映当前 tri-mode hi-speed surface，并沿 `BmsHiSpeedMode.FormatValue()` 输出；odd/even lane hi-speed adjustment 只在 overlay 可见时受理。
- `TestSceneBmsSoloPlayerPreStart` 现扩到 **8/8**：除既有 delayed-start / hold gate / target cycle / external clock suppression 外，还锁住“delay 到期但 hold 仍按住时继续可调速”以及“overlay mode/value 在真实 player flow 中反映当前 tri-mode surface”两条真实宿主链。
- 当前口径同步收口为 `UI_PreStartHold` 承担 hold gate、`UI_LaneCoverFocus` 保持 click-to-cycle；提前松开后的 authority 以 `SelectedHiSpeed` 是否变化为准，而不是把 routed key press 返回值当作唯一判断。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --configuration Release --filter "FullyQualifiedName~TestSceneBmsPreStartHiSpeedOverlay"` **3/3** 通过；`dotnet test osu.Game.Rulesets.Bms.Tests --configuration Release --filter "FullyQualifiedName~TestSceneBmsSoloPlayerPreStart"` **8/8** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

## 2026-04-23

### onboarding surface 跟进：首次启动向导收口为 OMS 六步流程

- `FirstRunSetupOverlay` 现已固定为六步：欢迎、UI 缩放、获取谱面、导入、难度表设置、按键绑定；这次变更维持主归属 `P1-A`，不为 onboarding / settings-entry surface 新开子线。
- 获取谱面页现改为 mania / BMS 外部站点导流与内部谱库补扫提示；导入页直接复用 `ExternalLibrarySettings`；难度表页通过反射调用 `BmsDifficultyTableManager` 导入 zris 镜像预设；最后一步复用全局、mania 与 BMS keybinding subsection。
- 欢迎页、获取谱面页与导入页的可见文案现已切到 OMS-owned localisation namespace + `.resx`，解决简中继续命中上游翻译的问题；手动重新打开向导并进入旧“游戏表现”页导致的 blank panel / unhandled error 也已一并修复。
- 验证：`dotnet test osu.Game.Tests --filter "FullyQualifiedName~TestSceneFirstRunScreenBehaviour|FullyQualifiedName~TestSceneFirstRunSetupOverlay|FullyQualifiedName~TestSceneFirstRunScreenImportFromStable" --configuration Release` **11/11** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

## 2026-04-22

### gameplay mod surface 修复：冷启动恢复与 startup cache 时序补全

- `OsuGameBase` 现不再把 startup 早期 `RulesetConfigCache` 未 ready 的 path 当作 ruleset failure；BMS mod memory 会先允许无 config 的首轮 apply，并在 cache ready 后 replay 当前 ruleset，补做 `PersistedModState` 恢复。
- 该修复同时消除了启动期误报的 `BMS` / `osu!mania` ruleset issue 通知，以及完全冷启动第一次进入 BMS 时 selected mod 与 remembered settings 丢失的问题。
- 新增 `BmsStartupModPersistenceIntegrationTest`，用“两段式 host 冷启动”回归锁定 BMS 冷启动恢复路径：先 seed `PersistedModState`，再用第二个同名 host 启动 `OsuGameBase`，断言 `BmsModSudden` 选中状态与配置成功恢复。
- 验证：`dotnet build .\osu.Desktop\osu.Desktop.csproj -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过；`dotnet run --project .\osu.Desktop\osu.Desktop.csproj -c Release` 进入 MainMenu 且最新 runtime log 不再出现 startup ruleset 错误；`BmsStartupModPersistenceIntegrationTest` + `BmsModStatePersistenceTest` 合计 **4/4** 通过；手测确认冷启动 / 运行中关开 / 切 mania 往返的 BMS mod 记忆均正确。

## 2026-04-21

### gameplay mod surface 跟进：BMS mod 选项与配置持久化

- `OsuGameBase` 现通过 ruleset-level mod persistence hook 在 BMS ruleset 切入 `BmsModStatePersistence`；当前选中 mod 顺序与 remembered settings 会写入 `BmsRulesetSetting.PersistedModState`，完全重启或切到其他 ruleset 再切回 BMS 时恢复，且不影响 mania。
- `ModSelectOverlay` 不再对实现 `IPreserveSettingsWhenDisabled` 的 configurable BMS mod 在 deselect 时 reset 默认值；`Auto Scratch` / `Auto Note` / `Random` / `Gauge Auto Shift` / `Judge Rank` / `Sudden` / `Hidden` / `Lift` 现在关闭再开启仍保留最后配置。
- `Sudden` / `Hidden` / `Lift` 现新增 `Remember gameplay changes` 开关，默认开启；局内滚轮调整可选择回写到持久化配置，而不是只停留在 gameplay clone 内。
- 验证：定向 `BmsRulesetConfigurationTest`、`BmsModStatePersistenceTest`、`BmsRulesetModTest` 合计 **56/56** 通过；独立输出目录 `Release` 构建通过。

### gameplay surface 跟进：`Playfield Style` 替换数值型 horizontal offset

- `BmsSettingsSubsection` 已移除数值型 `游玩区域水平偏移`，`BmsRulesetConfigManager` 改为声明四态 `Playfield Style`：`1P（居左）`、`2P（居右）`、`居中（左皿）`、`居中（右皿）`。
- 当前基础实现只作用于 single-play 5K / 7K：`1P（居左）` 与 `2P（居右）` 都会侧停靠但保留固定屏侧间距，scratch 视觉分别在左 / 右；两种 `居中` 都保持 playfield 居中，仅改变 scratch 视觉是在左还是右。9K 固定居中，14K 固定双侧布局。这不是完整 `1P/2P flip`，不会翻 bindings，也不会提前承诺 side-aware skin/HUD/BGA 合同。
- 验证：定向 `BmsRulesetConfigurationTest`、`BmsPlayfieldAdjustmentContainerTest`、`BmsLaneLayoutTest`、`TestSceneBmsPlayfieldLayoutConfig`、`BmsDrawableRulesetTest`、`BmsScrollSpeedMetricsTest` 合计 **92/92** 通过；`Build osu! (Release)` 通过。

### gameplay speed setting 跟进：移除 `Playfield Scale` 残余 surface

- `BmsSettingsSubsection` 已移除 `游玩区域缩放`，`BmsRulesetConfigManager` 也不再声明 `PlayfieldScale`；BMS settings surface 不再提供会破坏皮肤编排的整体缩放入口。
- `BmsPlayfieldAdjustmentContainer` 现明确固定为 identity transform；这样 settings / runtime 不会再通过用户缩放或数值横向偏移扭曲 strict visual-speed surface。
- 验证：后续同日回归已扩大到 `BmsLaneLayoutTest`，合计 **90/90** 通过；`Build osu! (Release)` 通过。

## 2026-04-20

### gameplay speed setting 跟进：pre-start hold integration coverage 扩面

- `TestSceneBmsSoloPlayerPreStart` 现额外锁定两类 `BmsSoloPlayer` 预开谱时序语义：提前松开 `UI_PreStartHold` 时 gameplay 仍必须继续等待 delayed-start 到时，以及 hold 期间 persistent target cycle 不得破坏临时 `Hidden` 覆写与松开后的 target 恢复。
- 同一 scene 也补上奇偶列调速双向回归，确认 paused pre-start overlay 下 odd-key 增速与 even-key 减速都能走通正式输入桥。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~TestSceneBmsSoloPlayerPreStart"` **5/5** 通过。

### gameplay speed setting 跟进：tri-mode Hi-Speed surface 与 pre-start hold operator surface 落地

- `BmsHiSpeedMode`、`BmsHiSpeedRuntimeCalculator`、mode dropdown + current-mode slider 已接通；settings 现可在 `Normal / Floating / Classic Hi-Speed` 三种模式间切换，并只显示当前模式数值。
- `DrawableBmsRuleset` 现按模式发布 runtime metrics / HUD detail / toast；`Classic` 继续锁定 `HS 10 + WN 350 => GN 300`，`Floating` 首轮为 initial-BPM anchored surface，但仍不宣称完整 `FHS`。
- `BmsSoloPlayer` 与 `BmsPreStartHiSpeedOverlay` 已把 5 秒 delayed start、`UI_PreStartHold` hold gate、奇偶键调速，以及 paused pre-start 下 `UI_LaneCoverFocus` / 滚轮 / 中键的 lane-cover 调整链接入正式 gameplay 流程；`SoloSongSelect` 则改为反射创建 `BmsSoloPlayer`，避免跨项目编译期依赖。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~BmsRulesetConfigurationTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~BmsDrawableRulesetTest"` **97/97** 通过；`Build osu! (Release)` 通过。

### gameplay speed setting 跟进：strict Classic Hi-Speed + frozen geometry surface 落地

- `DrawableBmsRuleset` 已把 Classic Hi-Speed 的 base time 从上游 mania 的 `11485 / HS` 改为官方 sample 对齐的 `(100000 / 13) / HS`，并由 `BmsScrollSpeedMetricsTest` 锁定 `HS 10 + WN 350 => GN 300`。
- `BmsPlayfield` 不再在运行时消费 playfield / receptor / bar-line 的 layout override，`BmsSettingsSubsection` 也已移除会扰动 strict profile 的 geometry sliders；内部 `BmsPlayfieldLayoutProfile` abstraction 仍保留给 ruleset / skin 侧使用。
- 当前公开 `Classic Hi-Speed` 范围仍保持 `1.0 - 20.0`，但这次已不只是范围收口，而是把 strict Classic surface 一并锁定。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~BmsRulesetConfigurationTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~TestSceneBmsPlayfieldLayoutConfig|FullyQualifiedName~BmsLaneLayoutTest|FullyQualifiedName~BmsDrawableRulesetTest"` **91/91** 通过；`Build osu! (Release)` 通过。

### gameplay feedback display 跟进：live `PERFECT / FC / FC LOST` 资格线复用现有 snapshot

- `BmsJudgementCounts` 新增 `CanStillPerfect / CanStillFullCombo`，随后又补入 `LeastSevereFullComboBreakResult / LeastSevereFullComboBreakCount` 派生语义，`DefaultBmsSpeedFeedbackDisplay` 现可在不扩 `BmsGameplayFeedbackState` 的前提下显示带紧凑原因标签的 live `PERFECT / FC / FC LOST` 状态线。
- 本次变更确认一部分 richer judge display 语义可以保留在 display 侧派生，而 recent timing history 与 aggregate snapshot 的分层不变。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsExScoreProgressInfoTest|FullyQualifiedName~BmsExScorePacemakerInfoTest|FullyQualifiedName~BmsJudgementCountsTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **69/69** 通过；`Build osu! (Debug)` 通过。

### aggregate gameplay feedback state contract 第四刀：live EX progress 并入 snapshot

- 新增 `BmsExScoreProgressInfo`，把当前 `EX-SCORE / MAX EX-SCORE` 快照为轻量值对象，并并入 `BmsGameplayFeedbackState`。
- `DefaultBmsSpeedFeedbackDisplay` 现继续沿同一 aggregate snapshot contract 显示 live `DJ LEVEL + EX 原始分子/分母 + %`，而 recent timing history 仍保持独立列表态。
- 新增 `BmsExScoreProgressInfoTest`，并扩展 `BmsGameplayFeedbackStateTest`、`BmsRulesetModTest`、`TestSceneBmsSpeedFeedbackDisplay`，锁定 EX 进度值语义、snapshot 镜像与文案显示。
- 验证：后续沿同一 feedback family 的聚焦回归已升至 `dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsExScoreProgressInfoTest|FullyQualifiedName~BmsExScorePacemakerInfoTest|FullyQualifiedName~BmsJudgementCountsTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **69/69** 通过；`Build osu! (Debug)` 通过。

### aggregate gameplay feedback state contract 第三刀：compact judgement counts 并入 snapshot

- 新增 `BmsJudgementCounts`，把 live score statistics 快照为轻量值对象，并并入 `BmsGameplayFeedbackState`。
- `DefaultBmsSpeedFeedbackDisplay` 现继续沿同一 aggregate snapshot contract 显示两行 compact live judgement summary，而 recent timing history 仍保持独立列表态。
- 新增 `BmsJudgementCountsTest`，并扩展 `BmsGameplayFeedbackStateTest`、`BmsRulesetModTest`、`TestSceneBmsSpeedFeedbackDisplay`，锁定 counts 映射、snapshot 值语义、初始镜像与文案显示。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsJudgementCountsTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **59/59** 通过；`Build osu! (Debug)` 通过。

### aggregate gameplay feedback state contract 第二刀：timing visual range 并入 snapshot

- `BmsGameplayFeedbackState` 现已额外包含 `TimingFeedbackVisualRange`，把 timing sparkline 的最后一个 scalar 输入也并入 aggregate snapshot。
- `DefaultBmsSpeedFeedbackDisplay` 现已收口为消费 `GameplayFeedbackState` 加 `RecentJudgementFeedbacks` 列表，不再直接额外绑定 `TimingFeedbackVisualRange` scalar。
- 新增 `BmsGameplayFeedbackStateTest` 并扩展 `BmsRulesetModTest`、`TestSceneBmsSpeedFeedbackDisplay`、`BmsSkinTransformerTest`，锁定 snapshot 值语义、ruleset 镜像和 sparkline/expiry 行为不回退。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~BmsSkinTransformerTest"` **153/153** 通过；`Build osu! (Debug)` 通过。

### aggregate gameplay feedback state contract 首刀落地

- 新增 `BmsGameplayFeedbackState`，把 speed metrics、target-state、最近判定与 fixed AAA pacemaker 这批 scalar feedback 收口为单个 BMS-owned snapshot。
- `DrawableBmsRuleset` 现额外暴露 `GameplayFeedbackState` bindable；`DefaultBmsSpeedFeedbackDisplay` 已改为优先消费该 aggregate state，而不是继续直接绑定多组 ruleset scalar bindable。
- recent timing history 与 visual range 暂时仍保持独立状态流，不把列表态硬塞进同一个 snapshot。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsGameplayFeedbackLayoutTest|FullyQualifiedName~TestSceneBmsJudgementDisplayPosition"` **154/154** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### shared judgement / feedback position contract 首轮落地

- 新增 `BmsGameplayFeedbackLayout`，把默认 gameplay feedback 摆位与 judgement 基线收口到同一条 BMS-owned 位置合同。
- `DrawableBmsJudgement` 不再持有独立的 `140px` judgement 偏移常量，`DefaultBmsHudLayoutDisplay.ApplyGameplayFeedbackDefaults()` 也已统一改为消费 shared contract。
- 新增 `BmsGameplayFeedbackLayoutTest`，并扩展 `TestSceneBmsJudgementDisplayPosition`，锁定 shared contract 的默认摆位与 direction-aware judgement 基线。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsGameplayFeedbackLayoutTest|FullyQualifiedName~TestSceneBmsJudgementDisplayPosition|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **117/117** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### 子线正式建档

- `P1-A` 已从旧的自由命名专题目录中拆出，成为 `doc_md/subline/P1-A/` 的正式子线入口。
- 本子线现固定维护 `DEVELOPMENT_PLAN.md`、`DEVELOPMENT_STATUS.md`、`CHANGELOG.md`、`TECHNICAL_CONSTRAINTS.md`，并与 `P1-C` 保持交叉联动。
- 当前仅完成文档重构与联动挂接，未新增构建或测试执行。
