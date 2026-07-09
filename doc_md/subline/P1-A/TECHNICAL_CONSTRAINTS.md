# P1-A 技术约束：产品面、release gate 与皮肤边界

> 最后更新：2026-07-10
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
6. 长条 body 的**游玩状态视觉**必须经皮肤无关的 `DrawableBmsHoldNote.BodyState`（`IBindable<BmsLongNoteBodyState>`，三态 `Idle/Holding/Broken`，由 `isHolding`+head/tail 判定纯派生）暴露给皮肤；默认皮肤 `DefaultBmsLongNoteBodyDisplay` 经 `[Resolved] DrawableHitObject` 解析父 hold note 绑定该 bindable（mania `DefaultBodyPiece` 同范式）。**不得**把状态判断硬塞进默认 body、也不得让 body 直接读判定内部。默认皮肤当前几何/映射＝width `0.5775`（相对车道宽，body 唯一物理宽度杠杆，缩放相对宽会抵消）、`Idle==Holding`＝head 色（`GetLongNoteHead`）+alpha `0.8`、`Broken`＝`GetLongNoteBodyBroken`（去色变灰+dim）+alpha `0.32`，由 `BmsSkinTransformerTest` 钉值；改这些值须同步该测试。`Broken→恢复` 仅 HCN 成立（见 [P1-E 约束 #4](../P1-E/TECHNICAL_CONSTRAINTS.md)）。仅视觉，不碰判定/计分/滚动/键音/chartbms 直读，也不动 head/tail（tail 仍 `Alpha=0`）。

## 皮肤创作生态（素材 + ini）约束

> 立项 2026-06-27（大型规划，未开工实现）。本节是 BMS 素材 + `skin.ini` 皮肤创作/编辑生态的**权威约束源**；面向制作者的渲染视图见 [../../other/SKINNING.md](../../other/SKINNING.md)，其内容必须从本节与 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) 的 F 系列**派生**，不得反过来把 `other/` 参考层当契约源。分期与验收口径见 PLAN 的 `F0–F3`。

1. **范围＝仅游玩界面。** 跟随 osu!lazer 已弃非游玩界面皮肤的边界：本生态只覆盖游玩界面视觉（stage / lane / note / LN / 判定线 / 判定显示 / gauge / combo / lane cover / 小节线 / BGA + IIDX 演出件）；选歌 / 结果 / 菜单皮肤不在内，相关 lookup（`ResultsSummary*` / `NoteDistribution*`）不属本生态范围。
2. **不移植 LR2 / beatoraja 运行时。** 不解析 `.lr2skin` / `.luaskin` / `.cim`，不引入其 timer / op / 关键帧 `dst` 动态体系。OMS 自有 ini 是**静态素材模型**；键名 / 元素族**对齐**这些生态仅为降低制作者迁移成本，不构成引擎兼容承诺。
3. **ini 方言＝自有 `[Mania]`-对齐静态段 + `[Bms]` 扩展段。** 与 mania 同义的键（lane / note / LN / judgeline / lighting / stage / barline）一律沿用 mania 原名（`NoteImage#` / `KeyImage#` / `ColourColumn#` / `HitPosition` / `BarlineHeight` 等）；BMS 独有件（gauge / turntable / scratch / keyflash / bomb / cover 绿数 / ghost-TD / bpm / progress）为 OMS 新定义键，须用 BMS 专属命名（不得借 legacy mania 资源键名，与「皮肤边界约束 2」一致）。按 keymode 分桶（`5K/7K/9K/10K/14K`，每段以 `Keymode:` 开头）。schema 键名当前为 **P0 草案**，改键必须同步回写本节与 `SKINNING.md`。**schema 的真实依据是代码实现**（mania `LegacyManiaSkin*` 实现蓝本 + BMS 现有 `DefaultBms*Display` / `BmsPlayfieldLayoutProfile` / `BmsDefaultPlayfieldPalette` 实际暴露的几何/颜色/纹理槽位）；草案字段表（本节键名与 `SKINNING.md` 同属 `[规划]`/派生）**不得当作规范反向约束实现**，`F1` 落地后据代码据实回写 `SKINNING.md`。⚠️ 已知草案失真：`HitPosition`/逐列 `ColumnWidth`/逐道 `ColourColumn#` 等是 mania 习惯草案——BMS 代码无 `HitPosition`（真实＝`PlayfieldHeight`+`HitTargetVerticalOffset`）、几何是 normal/scratch 两类 relative width 非逐列、颜色是 **IIDX 键色组**（White/Cyan/Yellow/Scratch 按键号+keymode 派生）非逐道任意色（详见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) F1「实现架构」第 6 条）。
4. **架构＝素材 + ini 层作为 `ISkin` 注入，零改现有 lookup 契约。** 现有 BMS lookup 类型 / 组件边界 / fallback 粒度是最稳的一层，素材层塞在其下；不得为接入 ini 改动 lookup 签名或 `BmsSkinComponents` 既有成员语义（与「皮肤边界约束 1/2」一致）。**（`F1` 实现校准 2026-06-27）** 立项期设想的独立 `BmsAssetSkin` 实际实现为 **`BmsLegacySkin : LegacySkin`**：经 `ParseConfigurationStream` hook 解析 `[Bms]` 段、override `GetConfig` 应答 `BmsSkinConfigurationLookup`，由 `SkinImporter` 路由导入皮肤实例化（非「被 `BmsSkinTransformer` 包裹」）；纹理走被包裹 skin 的 `GetTexture`，不侵入核心 `LegacySkin`。lookup 契约确实零改。详见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) F1「实现架构」与 CHANGELOG 2026-06-27。
5. **默认皮肤＝程序化兜底（不可删）+ 参考素材皮肤。** 当前 100% 程序化默认（`BmsTemporarySkinPalette` 静态色板 + `BmsPlayfieldLayoutProfile` 几何、零位图素材）保留为必备件的最终兜底，无 `skin.ini` 时即此（延续 `OMS_COPILOT.md` 「IIDX 直绘层只是 skin-load-failure fallback、非 OMS 内置皮肤方向」口径）。另产出一份能用本生态复现默认观感的 reference `skin.ini`（颜色 / 几何为主、近零位图）作 `F1` 验收对象兼创作者模板。
6. **不烤图（程序化对象不导出成 PNG）。** 纯色块 → ini 颜色键（`ColourColumn{lane}` 等），不烤成位图；程序化辉光 / 渐变保留为「引擎绘制、ini 参数化」（半径 / 颜色可调），不烤死。与 osu! 一致（Argon 程序化默认从不导出成文件）。
7. **校验＝加载期 fail-open + 诊断，编辑期更严。** 加载期**永不阻断游玩**：未知键忽略 + 告警（前向兼容，皮肤可写未来才实现的键）、值非法回退默认 + 告警、缺素材回退内置 + 告警；必备件始终有内置兜底，故皮肤理论上做不出"不可玩"结果。编辑期主动暴露必备槽位、实时校验、阻止保存结构性损坏皮肤。
8. **必备 / 推荐 / 可选三档。** 必备（引擎始终兜底、皮肤可覆盖不可删）＝playfield / lane、note、LN 头 / 身、判定线、判定显示、gauge、combo。其余为推荐 / 可选，缺省回退或不显示。keymode 可只声明子集，未声明回退内置默认（`[General] Keymodes:` 声明覆盖面）。
9. **两编辑面正交 + 布局编辑器边界（决议 X）。** `skin.ini` + 素材管"长相"，lazer 布局编辑器管通用全局 HUD 件（`ISerialisableDrawable`）的"摆位"。BMS 专属 HUD（gauge / combo / clear lamp）**保持 `DefaultBmsHudLayoutDisplay` 代码编排 + ini 调外观，不升格为布局编辑器可拖摆的 `ISerialisableDrawable`**（与「HUD 宿主约束 1/4」一致：不改 `SetComponents` 签名、不迁出 HUD 合同）；升格为决议 Y，明确后置。
10. **②③类引擎驱动件＝引擎驱动、ini 仅供素材。** keyflash / explosion / bomb / LN hold light / turntable / ghost-TD 等动态由引擎驱动，ini 只提供素材 + 位置 + 缩放 + 颜色，作者不写关键帧脚本；这些件落地前（`F2`）不得在 `SKINNING.md` 标为"当前可用"。
11. **fail-open 依赖「OmsSkin 恒在 fallback 链底」，F1 不得改 `providesBuiltInFallbacks` 语义。** `SkinManager.AllSources` 在 `CurrentSkin` 后恒 yield `DefaultOmsSkin`、`RulesetSkinProvidingContainer` 亦将其作为链底 fallback；素材皮肤缺件经 `SkinProvidingContainer` 链式查找回退到链底 OmsSkin 那层的程序化兜底。`BmsSkinTransformer.providesBuiltInFallbacks = skin is OmsSkin` 是「仅链底默认皮肤兜底」的正确设计，不得为「让素材皮肤兜底」改它（否则每层重复注入兜底、破坏分层）。（2026-06-27 代码勘探修正立项期「兜底需重构」设想。）
12. **BMS ini 段解析禁止侵入核心 `osu.Game/LegacySkin`。** 核心 `LegacySkin` 把 mania 段解析硬编码进 osu.Game（`LegacyManiaSkinDecoder` / `maniaConfigurations` / `GetConfig` mania 分支）是上游历史包袱；BMS 不得照搬，`BmsSkinDecoder` / `BmsSkinConfiguration` / lookup 必须落 `osu.Game.Rulesets.Bms`。纹理走被包裹 skin 的 `GetTexture`（不侵入），结构化配置由独立 BMS 皮肤配置源解析。「SkinManager 选中的皮肤如何被 BMS 读到结构化配置」（实例化类型 / 借壳 / 独立文件）是 F1 动工前**头号 gate**，候选方案见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) F1「实现架构」第 4 条。

13. **皮肤存储拓扑（文件如何存放）与授权面（什么可被控制）是正交两轴。** F1「**头号 gate**」（约束 12）只解决"BMS 如何读到结构化配置"，**不等于**皮肤文件的存放方式。F1 期皮肤沿用核心 `SkinManager` / `SkinImporter`（`.osk` → realm hash-backed `files/`·不可读）。**该"复用 SkinManager·不走 chartbms 旁路"是 F1 立项决议，2026-06-29 起被用户重审**：若要皮肤像 chartmania/chartbms 一样可视文件夹直读管理，须走 `G1`（仿 `BmsFolderImporter`），属方向性调整，**未拍板前不得擅自改皮肤存储路径**。约束 11/12 的 fail-open 与不侵入核心语义在任一存储方案下都须保持。**（2026-06-29 进展：用户已拍板走 `G1`；realm 载体已落地——`SkinInfo` 加 `FilesystemStoragePath` + `IsExternalFilesystemStorage`、`schema_version` 55→56，镜像 `BeatmapSetInfo`。`ExternalLibraryRootPath` 暂未加，留刀④定。folder 皮肤 `SkinInfo.Files` 须留空·经 `RealmBackedResourceStore` 安全回落 fallbackStore。）

14. **2026-07-10 恢复准入门槛。** 2026-06-30 00:05（北京时间）之后的 G1 生产接线、F2 动态件、Lua、mania fallback adapter 与 reference-default 替换均不构成已落地能力。重新引入时必须按独立小切片实现；至少覆盖用户皮肤→OmsSkin 逐组件 fallback、mania 默认资源、managed/external 路径、删除/重命名 containment、热重载、5K/7K/9K/14K 布局与真实事件链，且完成用户实机视觉验收。禁止以同批新增的类型断言测试代替生产链证明。

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
