# P1-A 技术约束：产品面、release gate 与皮肤边界

> 最后更新：2026-06-20
> 本文件记录该专题的硬约束。若实现与本文冲突，先修正文档或代码其中一边，再继续开发。

## 归线约束

1. 本子线属于 Phase 1.x 下的 `P1-A`，但与 `P1-C` 强耦合；不得借题把 `FHS`、`dan`、`1P/2P flip`、BSS / MSS 提前带入当前主交付。
2. `P1-A` 负责边界、lookup、HUD 宿主与 fallback 合同；`P1-C` 只能在这条边界内承接 runtime 反馈表达。

## 术语与产品约束

1. 当前 `GN / WN` 可以表述为 OMS 当前 `Normal / Floating / Classic Hi-Speed + Sudden / Hidden / Lift` runtime surface 的反馈，但不得对外宣称为完整 IIDX `FHS`。
2. settings 页当前允许暴露 Hi-Speed 模式、当前模式数值，以及不启用 `Sudden / Hidden / Lift` 时的基础下落时间（ms）；不得在 settings 中显示 `GreenNumber` 本身或 runtime-adjusted 可见毫秒，也不得制造“已完整支持 BPM 补偿 / FHS 全语义”的错误预期。
3. `Lift` 继续是 geometry control；`Hidden` 继续是下遮挡。两者在命名、状态、HUD 表达和 pre-start overlay 中都不得重新混写。
4. 当前公开 Hi-Speed 范围必须保持：`Normal 1.0 - 20.0`、`Floating 0.5 - 10.0`、`Classic 0.5 - 10.0`；其中 `Classic` 的 base time 映射应保持 `TimeRange = (100000 / 13) / HS`，官方 sample `HS 10 + WN 350 => GN 300` 必须持续成立。
5. 当前运行时 geometry profile 已冻结；`Playfield Scale` 必须固定为 `1.0` 并保持不可配置，因为缩放会破坏皮肤编排并扭曲权威 visual-speed surface。
6. 除 `Sudden / Hidden / Lift` 与当前 single-play `Playfield Style`（`1P（居左）` / `2P（居右）` / `居中（左皿）` / `居中（右皿）`，仅作用于 5K / 7K 的 playfield 停靠与 scratch 视觉侧别，不改 binding flip）外，旧的 playfield / receptor / bar-line layout config 不得继续作为用户可见 contract 影响速度或几何语义。
7. `UI_PreStartHold` 必须承担“前 5 秒阻止开始 + 全程调速修饰键”这一统一运行时合同；`UI_LaneCoverFocus` 必须保持为 click-to-cycle 持久 target 的独立动作。右侧 `READY HOLD` overlay 只保留给前 5 秒阻止开谱窗口，居中的 `BMS speed` toast 则应在 hold 修饰键按住期间持续可见；该 operator surface 不得退化成 debug overlay 或无 fallback 的临时实现。
8. BMS mod 选项与配置记忆必须保持 ruleset-local；当前 `PersistedModState` 只允许作用于 BMS，不得让 mania / 全局 `SelectedMods` 获得隐式共享持久化。
9. 冷启动时不得在 `RulesetConfigCache` 未 ready 前直接调用 `GetConfigFor()` 去构建 BMS mod persistence；正确合同是先允许无 config 的首轮 ruleset apply，再在 cache ready 后 replay 当前 ruleset 完成 restore。否则会同时打破 startup release gate（误报 ruleset issue）与 BMS mod 冷启动记忆。
10. 对实现 `IPreserveSettingsWhenDisabled` 的 configurable BMS mod，停用只意味着 inactive，不等同于 reset；除显式重置入口或配置迁移外，不得在 mod 菜单关闭时清空其最后配置。
11. `首次启动向导`、`Run setup wizard` 与无谱面引导这类共享 onboarding / settings-entry surface 默认归 `P1-A`；若页面只是复用外部 / 内部谱库与按键绑定面板，其存储 / 输入语义仍分别归 `P1-H` / `P1-B`，不得为暴露面调整另开主线。
12. 共享层首次启动向导若需触发 BMS-only runtime 能力，必须保持 `osu.Game` 不直接引用 `osu.Game.Rulesets.Bms`；可用反射 / 抽象边界，但模块缺失时页面需优雅退化，而不是在构造或 load 阶段抛异常。
13. 首次启动向导中用户可见的 OMS 文案，若需覆盖上游翻译，必须使用 OMS-owned localisation namespace + 对应 `.resx`；只改 `*Strings.cs` fallback 不足以覆盖简中等非英文资源。
14. 若共享 desktop settings-entry surface 需要裁剪 upstream 的数位板 / 触屏点击 / 鼠标 subsection，应只在 `OsuGameDesktop` 这类 desktop 宿主层安全隐藏，不能把这类 product-surface trim 下移成 `OsuGameBase` 的全宿主行为；否则会连带改写 test scene / 非 desktop host 的设置装配合同。
15. `osu!mania` settings 页的 `滚动速度` 若显示毫秒，只能表述为标准车道几何下的参考下落时间；不得包装成跨皮肤或跨 ruleset 的严格体感合同，也不得鼓励拿它直接与 BMS 下落时间对照。
16. 若后续公开 `BMS -> mania` 单向转谱入口，产品口径必须显式收窄为 `BMS source -> mania target`，不得沿用或暗示 generic all-ruleset convert surface；`ShowConvertedBeatmaps`、`AllowGameplayWithRuleset()` 与顶层“显示转谱”按钮都不能直接被包装成该专题已支持的 authority。
17. `P1-A` 只拥有 `BMS -> mania` 单向转谱的入口位置、按钮文案、Song Select / presentation gating 与 unavailable feedback；source keymode matrix、lane flatten、scratch-family 退化与空结果 suppress 仍归 `P1-K/K9`，unsupported / invalid case 必须隐藏或明确不可用，而不是展示可点击空壳入口。
18. **playfield 顶边必须贴屏幕边缘**（top-anchored `Y=0`，音符从屏幕最顶出现，符合 green-number「整屏可见场 = 顶边→判定线」语义）。默认 `PlayfieldHeight` 当前为 `0.92`，是 strict profile 的唯一杠杆（config `PlayfieldHeight` 维持被忽略的 disabled 项，改值改 `BmsPlayfieldLayoutProfile.CreateDefault` 默认），判定线落在 `0.92` 屏高、gauge（`DefaultBmsHudLayoutDisplay.gauge_top = PlayfieldHeight + 0.002`）紧贴其下并止于近屏底。**不得再用整体下移**（曾用的 `PLAYFIELD_VERTICAL_OFFSET` 已删除——它让顶边离开屏幕边缘、违背本约束；要把 gauge 放低就调高 `PlayfieldHeight`，让顶边留在边缘、判定线下移）。调整 `PlayfieldHeight` 必须保持判定时序不变量：仅在 `HitTargetVerticalOffset=0`（`scrollLengthRatio≡1`、`TimeRange` 与场高无关）下成立，场高只改像素扫过距离、不改 GN / 判定窗口；change 后须同步 `BmsLaneLayoutTest` / `TestSceneBmsPlayfieldLayoutConfig` 断言。gauge 的相对-Y 贴线对齐依赖「HUD 与 ruleset 同屏」假设（`BmsPlayfieldAdjustmentContainer` Scale 固定 `1.0`、HUD `MainHUDComponents` 与 playfield 均全屏），不得引入会破坏该假设的 HUD 安全区 inset 或 playfield 缩放。

## 皮肤边界约束

1. 新的 gameplay feedback 必须是 BMS-owned skinnable component，不得复用 mania lookup，也不得回落到上游默认皮肤语义。
2. 若新增纹理、采样或 config key，必须使用 BMS 专属命名空间，不得借用 legacy mania 资源键名。
3. 不得通过遍历 wrapped HUD 子节点、偷改 `GaugeBar`、偷改 `ComboCounter` 的方式植入 speed feedback。
4. pre-start 视觉流速预览若实现，必须作为 BMS-owned playfield / lane visual surface 接入，并复用 BMS note lookup / fallback；不得塞进 `GameplayFeedbackDisplay`、toast 或 mania lookup。产品口径的“1 号轨道”应解析为第一非 scratch 普通轨，且实际开谱后必须立即消失。
5. 任何更改皮肤边界、HUD 宿主、fallback 语义或 release gate 的改动，都必须同步更新本目录四件套以及受影响的 `../../mainline/` 文档。

## HUD 宿主约束

1. 不得直接破坏现有 `IBmsHudLayoutDisplay` 签名。若需要额外组件，必须使用 versioned optional interface、wrapper contract，或等价的向后兼容方案。
2. 旧版 HUD provider 在未实现新接口时必须保持可用；新反馈组件应由 `OmsSkin` 默认路径独立 fallback。
3. 默认 HUD 不得依赖 Debug overlay、临时 Box 或只在 toast 中可见的链路来维持功能完整。
4. 默认 gauge 摆位（`DefaultBmsHudLayoutDisplay`）规定为：判定线下方、与 playfield 条带等宽、并随 `PlayfieldStyle` 侧锚（P1 左 / P2 右 / 居中，与 lane 同一套 inset）、垂直贴 `PlayfieldHeight`。该摆位所需的 `PlayfieldWidth / keymode` 必须经 `GameplayState` 可玩谱面解析、`PlayfieldStyle` 必须经 game 级 `IRulesetConfigCache.GetConfigFor(bms)` 解析（与 playfield 子树同一 `BmsRulesetConfigManager` 实例）；**不得为此改 `IBmsHudLayoutDisplay.SetComponents` 签名，也不得把 gauge 迁出 HUD 合同**（gauge 仍是 HUD 子件 + 可皮肤化组件）。无 `GameplayState` / config 的宿主（皮肤编辑器预览 / 测试）须优雅降级（居中 + 兜底宽度），不得抛异常。
5. BMS gameplay 的 wrapped 全局 HUD 自带的 **默认 combo（`LegacyDefaultComboCounter`，与 `BmsComboCounter` 重复）** 与 **gameplay leaderboard（`DrawableGameplayLeaderboard`，offline-first）** 必须在 `BmsSkinTransformer` 装配 BMS `MainHUDComponents` 时 **从配置树移除**（`stripDefaultHudElements`：`Container.Remove` wrapped 直接子里的 `ComboCounter` / **`LegacyDefaultComboCounter`** / `DrawableGameplayLeaderboard`），而**不是运行时隐藏**（避免首帧闪烁 / 仍进皮肤编辑器序列化）。⚠️ **坑**：上游默认连击 `LegacyDefaultComboCounter` 是 `CompositeDrawable, ISerialisableDrawable`，**不是 `ComboCounter` 子类**——strip 与其回归测试都必须显式覆盖该类型，不能只匹配 `ComboCounter`（否则连击删不掉）。wrapped HUD 其余件（全局层 score 等）保留；BMS combo 由 `SetComponents` 另行添加、不在 wrapped 层。两者**同源**＝上游 `LegacySkin` 的 ruleset-`MainHUDComponents` 默认布局（`new LegacyDefaultComboCounter()` + `new DrawableGameplayLeaderboard()`）。重新放开须显式改 `stripDefaultHudElements` 并同步本约束；不得用 `Alpha=0` / `ShowLeaderboard=false` 之类隐藏式替代"移除"。
6. `BmsGaugeBar` 继承 `HealthDisplay`，而 `HealthDisplay` 会把自身绑到 `HUDOverlay.ShowHealthBar` 并在其为 false（NoFail 等通用"隐藏血条"开关）时 `FadeTo(0)`。**BMS groove gauge 是核心游玩信息，必须免疫该开关、始终显示**：`BmsGaugeBar.LoadComplete` 订阅 `ShowHealthBar` 变化并重申 `Alpha=1`（在 base 之后注册以压过其淡出）。不得让 BMS gauge 随通用血条开关消失。注意：gauge 摆位/可见性的回归测试必须用**真实 `HUDOverlay`**（裸 `DependencyProvidingContainer` 下 `hudOverlay` 解析为 null、`showHealthBar` 恒 true，会掩盖该淡出路径）。HUD 整体 `ShowHud` 淡入仍应经父级正常生效。

## 反馈家族约束

1. 当前专题的第一阶段只收口 speed feedback；后续 `FAST/SLOW`、judge display、visual timing-offset、EX pacemaker 必须尽量沿同一 feedback family 承载，不再各自新开 ad-hoc overlay。（注：承载这套家族的常驻 `DefaultBmsSpeedFeedbackDisplay` 卡已于 2026-06-15 按产品决定整体移除；本条适用于任何未来重新引入。）
2. judgement 位置若需要与 feedback 联动，必须新增显式 BMS 位置合同；不得继续复制新的硬编码偏移值。
3. toast 可以保留为瞬时强调层，但不得继续承担唯一权威反馈职责。

## 测试与发布约束

1. 任何新增 feedback component 都必须补 fallback 回归：`OmsSkin` 默认路径、无该组件的用户皮肤路径、实现旧版 HUD 接口的用户皮肤路径。
2. 数值回归必须同时锁定 tri-mode 当前合同：`Classic` sample、mode-aware `GN / WN` 语义、以及 pre-start odd/even key 调速映射，直到明确进入新的速度语义专题。
3. 只有当默认路径、fallback 路径、HUD 宿主兼容性和文档同步全部完成后，才允许把该专题标记为已落地。
