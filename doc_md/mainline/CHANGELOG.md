# OMS 更新日志

> 本文件记录每次验证通过的变更摘要，按时间倒序排列。
> 当前开发进度与遗留问题见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)；分步规划见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

---

## 2026-04-25

### 文档 / 代码对齐审计与恢复边界收口

- 对当前工作区、`artifacts/` 恢复快照与主线文档做了一轮对齐审计，明确后续文档基线必须以当前代码与可复核验证结果为准，而不是继续沿用 recovery 过程中残留的失真索引或过期状态页。
- `doc_md/subline/README.md` 已恢复为当前真实 `P1-A` ~ `P1-H` 子线入口索引，不再错误指向不存在的 `p1x-skin-boundary-green-number/README.md` 或把 `subline` 当成 `other` 文档入口。
- `doc_md/other/README.md` 已移除失效的 `oms_server_bridge_export.md` 入口；`doc_md/other/SKINNING.md` 内多处源码与 `SKIN/` 候选包链接已改为正确相对路径，避免继续跳到不存在位置。
- `README.md`、`DEVELOPMENT_PLAN.md` 与 `DEVELOPMENT_STATUS.md` 已同步到当前代码现实：BMS 规则集约 **167** 个源文件、BMS 测试项目 **58** 个源文件；`A-NOT` 已补回根 README 的当前状态；编译诊断口径已从过期的“0 warning / 0 error”修正为当前 `Rebuild` 下的“13 warning / 0 error”。
- 本轮还额外确认了一个容易误导后续 agent 的细节：普通增量 `dotnet build` 可能打印 `0 warning / 0 error`，但这不能当作当前真实诊断基线；主状态页现统一以 `dotnet build osu.Desktop.slnf -t:Rebuild ...` 的结果作为权威口径。
- 验证：`dotnet build osu.Desktop.slnf -t:Rebuild -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsStartupModPersistenceIntegrationTest|FullyQualifiedName~BmsModStatePersistenceTest|FullyQualifiedName~TestSceneBmsSoloPlayerPreStart|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics"` **111/111** 通过；`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --configuration Release --filter "FullyQualifiedName~ExternalLibraryScannerTest|FullyQualifiedName~TestSceneFirstRunSetupOverlay|FullyQualifiedName~TestSceneFirstRunScreenImportFromStable|FullyQualifiedName~TestSettingsMigration"` **18/18** 通过；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --configuration Release --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin"` **92/92** 通过。

## 2026-04-24

### 文档主线：第二轮全面复验与验证基线同步

- 按 `DEVELOPMENT_STATUS.md` 当前主线声明重跑权威切片：BMS 全量 **706/706**、mania OMS skin gate **92/92**、BMS user-skin fallback **105/105**、scratch bridge **43/43**、`osu.Game.Tests` 文档 gate **23/23**，并再次确认 `osu.Desktop` Release 构建通过。
- `TestSettingsMigration` 现已移除对不存在的 `DisplayStarsMaximum -> 10.1` 自动迁移假设，改为锁定当前实际合同：旧配置值保持不变，且用户重新保存后的值会跨重启继续保留。
- `DEVELOPMENT_STATUS.md` 与 `DEVELOPMENT_PLAN.md` 已同步到当前已验证基线，不再继续沿用过期的 BMS **608/608** / BMS fallback **92/92** 快照。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj` **706/706** 通过；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin"` **92/92** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter "FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics"` **105/105** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter "FullyQualifiedName~TestSceneOmsScratchGameplayBridge"` **43/43** 通过；`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --filter "FullyQualifiedName~ExternalLibraryScannerTest|FullyQualifiedName~TestSceneFirstRunScreenBehaviour|FullyQualifiedName~TestSceneFirstRunSetupOverlay|FullyQualifiedName~TestSceneFirstRunScreenImportFromStable|FullyQualifiedName~TestSceneStartupSkinMigration|FullyQualifiedName~TestSceneEditDefaultSkin|FullyQualifiedName~TestSettingsMigration" --configuration Release` **23/23** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### 最终全面查验收尾

- mania 最后一轮残留已全部收口：`TestSceneObjectPlacement` 改为锚定当前 `EditorRadioButton` / `HitObjectCompositionToolButton` 工具按钮；`TestSceneManiaModHidden` / `TestSceneManiaModFadeIn` 现按当前 gameplay scaling 合同断言 coverage；`TestSceneManiaTouchInput` 现按真实列边界而非过期固定 gap 坐标取点。
- 本轮确认 `osu.Game.Rulesets.Mania.Tests` 全量 **761/761** 通过，说明最终收尾后 mania 已恢复到当前仓库合同下的完整测试绿线，而不是仅停留在 OMS skin gate **92/92**。
- 验证：`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --filter "FullyQualifiedName~TestSceneManiaTouchInput|FullyQualifiedName~TestSceneObjectPlacement"` **12/12** 通过；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --filter "FullyQualifiedName~TestSceneManiaModHidden.TestMaxCoverageFullWidth|FullyQualifiedName~TestSceneManiaModHidden.TestMaxCoverageHalfWidth|FullyQualifiedName~TestSceneManiaModHidden.TestMinCoverageHalfWidth|FullyQualifiedName~TestSceneManiaModFadeIn.TestMaxCoverageFullWidth|FullyQualifiedName~TestSceneManiaModFadeIn.TestMaxCoverageHalfWidth"` **5/5** 通过；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj` **761/761** 通过。

## 2026-04-23

### P1-A：首次启动向导重构为 OMS 六步流程

- `FirstRunSetupOverlay` 现已固定为六步：欢迎、UI 缩放、获取谱面、导入、难度表设置、按键绑定；不再保留旧的 stable import 条件分支与旧 behaviour page 文案 / 结构。
- `ScreenBeatmaps` / `ScreenImportFromStable` / `ScreenBehaviour` / `ScreenKeyBindings` 已分别收口为 OMS onboarding surface：获取谱面页改为 mania / BMS 站点导流和内部谱库补扫提示；导入页直接嵌入 `ExternalLibrarySettings`；难度表页按分组导入 zris 镜像预设，并通过反射调用 `BmsDifficultyTableManager` 保持 `osu.Game` 与 `osu.Game.Rulesets.Bms` 的项目边界；最后一步复用全局、mania 与 BMS 的 keybinding subsection。
- 手动重新打开首次启动向导并进入旧“游戏表现”页导致的 blank panel / unhandled error 已修复；`SkinSection` 里的 skin dropdown disabled-state 现改到 `LoadComplete()` 执行。
- 欢迎页、获取谱面页与导入页的可见文案现已切到 OMS-owned localisation namespace + `.resx`，解决简中界面继续显示上游翻译的问题；本次归线维持既有 `P1-A`，导入页复用外部谱库设置仅作为 `P1-H` 从属暴露，不新开子线。
- 验证：`dotnet test osu.Game.Tests --filter "FullyQualifiedName~TestSceneFirstRunScreenBehaviour|FullyQualifiedName~TestSceneFirstRunSetupOverlay|FullyQualifiedName~TestSceneFirstRunScreenImportFromStable" --configuration Release` **11/11** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### BMS：进入选歌与切换分组时停留在根分组

- `SongSelect` 现已把“进入 BMS 选歌 fresh entry / 切换任意 BMS 分组”统一收口为 ruleset-driven root reset：共享层新增 `Ruleset.ShouldResetSongSelectGroupToRoot()` 扩展点，仅由 BMS 打开；mania 与其他 ruleset 继续沿用原有行为。
- `BeatmapCarousel` 现会在 root-level 状态下保留当前歌曲的全局 beatmap 选择，同时把该歌曲对应的最外层 `GroupDefinition` 设为 keyboard-selected 项。这样进入 BMS 或切组后，界面表现为“停在最外层分组，但已选中当前歌曲所属外层组”，不会错误回到 leaf 谱面展开态。
- 新增 / 更新 BMS 回归覆盖：`BmsRulesetStatisticsTest` 锁定 BMS 分组的 root-reset contract，`TestSceneBmsSongSelectDifficultyTable` 锁定 fresh entry 与切换到 `难度表` / `标题` 分组时均保持 root-level，并正确高亮目标外层分组。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter "FullyQualifiedName~BmsRulesetStatisticsTest|FullyQualifiedName~TestSceneBmsSongSelectDifficultyTable"` **26/26** 通过。

### BMS：选歌分组收窄并默认切到难度表

- BMS 专属 Song Select 分组下拉现已改为 ruleset-specific 显式列表：移除 `未分组`，并移除 `本地收藏`、`导入时间`、`上架时间`、`官网收藏`、`我做的谱面`、`谱面状态`、`来源` 这些不需要的上游分组；mania 继续沿用默认共享列表，不受影响。
- `Difficulty Table` 分组标签现改用 OMS-owned 本地化资源，在中文界面显示为 `难度表`。
- 由于 BMS 分组列表首项现为 `DifficultyTable`，而 song select group fallback 也已改为“当前 ruleset 的第一个可用项”，BMS 进入选曲时默认分组现会安全落到 `难度表`，不再回退到 `未分组`。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter "FullyQualifiedName~BmsRulesetStatisticsTest"` **21/21** 通过。

### BMS：选歌排序标签语义纠正

- BMS 专属 Song Select 排序下拉中，原先回落为 `Clear Lamp` 与误复用通用 `Accuracy` 语义的两个本地成绩排序项，现已明确改为 `点灯状态` 与 `达成率`；这次修正只影响 BMS 的显示语义，不改变既有排序逻辑，也不影响 mania。
- 显示层现改用 OMS-owned `OmsSongSelect` 本地化资源承载这两个标签，避免继续复用上游 `SongSelectStrings.Accuracy` 导致中文界面出现 `准度要求`，也避免缺失翻译时回退到英文 `Clear Lamp`。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter "FullyQualifiedName~BmsRulesetStatisticsTest"` **20/20** 通过。

### P1-H：谱库扫描拓扑扩展为外部/内部 + 重建/增量四模式

- Settings -> Maintenance 现已把谱库扫描拆成四个显式入口：`扫描外部谱库（重建）`、`扫描外部谱库（增量）`、`扫描内部谱库（重建）`、`扫描内部谱库（增量）`；其中内部两项已从原 `外部谱库` subsection 迁移到新的 `内部谱库` subsection，完成语义隔离。
- `ExternalLibraryScanner` 与 `ManagedLibraryScanner` 现新增 `ScanMode`（`Rebuild` / `Incremental`）与按目录判断“是否仍需导入”的回调；`OsuGameDesktop` 会把该判定下推到 BMS / mania importer。`增量` 模式只会处理当前没有 active `FilesystemStoragePath` 记录的目录，`重建` 模式则继续重走全部候选目录并允许重新注册/刷新索引。
- 新增 `InternalLibrarySettings`，`ExternalLibrarySettings` 现只保留外部根管理与外部两种扫描按钮；桌面端 Settings -> Maintenance 拓扑已从“一个 subsection 混放外部/内部扫描”改为“外部谱库 / 内部谱库”双 subsection。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --filter "FullyQualifiedName~ExternalLibraryScannerTest"` **6/6** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### P1-H：内部谱库扫描 managed-root 判定修复

- Settings -> Maintenance 的谱库扫描口径现已明确分为两条链：`扫描外部谱库` 只针对已注册的外部根目录；`扫描内部谱库` 只负责重建当前数据根下 `chartbms/` 与 `chartmania/` 的 managed roots 索引，适用于用户手动复制、解压或移动谱面目录后的补扫。
- 修复 `FilesystemSanityCheckHelpers.IsSubDirectory()` 在比较“带尾部分隔符的 managed root”与“不带尾部分隔符的子目录父路径”时出现的 false negative；当前会先用 `Path.TrimEndingDirectorySeparator()` 规范化两侧，再做同目录/父目录链判断。
- 该修复使 `BmsFolderImporter.RegisterManagedDirectory()` 与 `ManiaFolderImporter.RegisterManagedDirectory()` 不再对合法的 `chartbms/...` / `chartmania/...` 目录误报“不在 managed root 下”；并新增 `FilesystemSanityCheckHelpersTest`，锁定“child-under-parent”和“same-directory”两条 trailing-separator 回归。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter "FullyQualifiedName~FilesystemSanityCheckHelpersTest"` **2/2** 通过。

## 2026-04-22

### BMS：冷启动 mod 恢复与 startup ruleset 时序修复

- `OsuGameBase` 现不再把 `RulesetConfigCache` 尚未 `LoadComplete()` 的 startup path 当作 ruleset failure；当 cache 仍未 ready 时，BMS mod persistence 会先跳过 config-backed restore，并在 cache ready 后排队重放当前 ruleset，补做 `PersistedModState` 恢复。
- 该修复同时消除了启动期误报的 `BMS` / `osu!mania` ruleset issue 通知，以及冷启动首轮进入游戏时 BMS mod 选中状态和 remembered settings 丢失的问题。
- 新增 `BmsStartupModPersistenceIntegrationTest`：先 seed `PersistedModState`，再以第二个同名 host 冷启动 `OsuGameBase`，断言 `BmsModSudden` 的选中状态、cover 参数与 `RememberGameplayChanges` 都被恢复。
- 验证：`dotnet build .\osu.Desktop\osu.Desktop.csproj -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过；`dotnet run --project .\osu.Desktop\osu.Desktop.csproj -c Release` 进入 MainMenu，最新 runtime log 不再出现 `An issue with ruleset` / `Failed to revert from ruleset` / `Cannot retrieve IRulesetConfigManager`；`BmsStartupModPersistenceIntegrationTest` + `BmsModStatePersistenceTest` 合计 **4/4** 通过；手测确认完全关闭重启后不再弹三条通知，BMS mod 冷启动 / 运行中关开 / 切 mania 往返都能正确恢复。

## 2026-04-21

### BMS：mod 选项与配置持久化

- `osu.Game` 现已新增 ruleset 级 `IRulesetModStatePersistence` 扩展点，BMS 通过 `BmsModStatePersistence` 把当前选中的 mod 顺序与 remembered settings 写入 `BmsRulesetSetting.PersistedModState` JSON；完全关闭重开，或从 BMS 切到 mania 再切回 BMS 时都可恢复，且不影响 mania。
- 可配置 BMS mod 现通过 `IPreserveSettingsWhenDisabled` 保留停用前最后配置，解决 `ModSelectOverlay` 在 deselect 时无条件 reset 默认值的问题；`Auto Scratch` / `Auto Note` / `Random` / `Gauge Auto Shift` / `Judge Rank` / `Sudden` / `Hidden` / `Lift` 等 mod 现在手动关掉再开仍会带回上次配置。
- `Sudden`、`Hidden`、`Lift` 现新增 `Remember gameplay changes` / `记忆游戏内变动` 开关，默认开启；开启时局内滚轮调整会回写当前 BMS selected mods 与持久化快照，关闭时则保持 current-play-only 语义。
- 验证：`BmsRulesetConfigurationTest`、`BmsModStatePersistenceTest`、`BmsRulesetModTest` 合计 **56/56** 通过；独立输出目录 `Release` 构建通过。

### BMS：新增 `Auto Note` assist mod

- `osu.Game.Rulesets.Bms` 现已新增 `BmsModAutoNote`，与现有 `BmsModAutoScratch` 对称：会自动处理非 scratch note，并把对应对象从判定 / 计分 / gauge 语义中剔除。
- `BmsModAutoNote` 现提供独立的 `Note visibility`、`Tint notes` 与 `Note tint colour` 配置面；当前与 `Auto Scratch` 互斥，且二者都继续与 `BmsModAutoplay` 互斥。
- 定向 `BmsRulesetModTest`、`BmsGaugeProcessorTest`、`BmsScoreProcessorTest`、`BmsDrawableRulesetTest` 合计 **208/208** 通过；`Build osu! (Release)` 通过。

### P1-A：BMS `Playfield Style` 替换 `Playfield Horizontal Offset`

- `BmsSettingsSubsection` 已移除数值型 `游玩区域水平偏移`，`BmsRulesetConfigManager` 改为声明四态 `Playfield Style`：`1P（居左）`、`2P（居右）`、`居中（左皿）`、`居中（右皿）`。
- 当前基础实现只作用于 single-play 5K / 7K：`1P（居左）` 与 `2P（居右）` 现在都属于“侧停靠但保留固定屏侧间距”的样式，scratch 视觉分别留在最左 / 最右；两种 `居中` 都保持 playfield 居中，仅改变 scratch 视觉在左还是右。9K 固定居中，14K 保持固定双侧布局；这不是完整 `1P/2P flip`，不会翻转 bindings 或 side-aware skin/HUD/BGA 合同。
- `BmsRulesetConfigurationTest`、`BmsPlayfieldAdjustmentContainerTest`、`BmsLaneLayoutTest`、`TestSceneBmsPlayfieldLayoutConfig`、`BmsDrawableRulesetTest`、`BmsScrollSpeedMetricsTest` 合计 **92/92** 通过；`Build osu! (Release)` 通过。

### P1-A / P1-C：BMS `Playfield Scale` 残余 surface 移除

- `BmsSettingsSubsection` 已移除 `游玩区域缩放`，`BmsRulesetConfigManager` 也不再声明 `PlayfieldScale`；旧值不会再参与当前 BMS runtime contract。
- `BmsPlayfieldAdjustmentContainer` 现固定为 identity transform，不再承接用户侧缩放或数值型横向偏移；这样非权威几何缩放不会再混入当前 visual-speed surface。
- `BmsPlayfieldAdjustmentContainerTest` 与 `BmsRulesetConfigurationTest` 已改为锁定“unit scale + style-based single-play layout”合同；定向 `BmsRulesetConfigurationTest`、`BmsPlayfieldAdjustmentContainerTest`、`BmsLaneLayoutTest`、`TestSceneBmsPlayfieldLayoutConfig`、`BmsDrawableRulesetTest`、`BmsScrollSpeedMetricsTest` 合计 **90/90** 通过；`Build osu! (Release)` 通过。

## 2026-04-20

### P1-A / P1-C：pre-start 稳定性修复与 `UI_LaneCoverFocus` / `UI_PreStartHold` 语义拆分

- 修复 `BmsSoloPlayer` pre-start delayed start 引起的 clock failure：在 `BmsSoloPlayer.StartGameplay()` 开头调用 `GameplayClockContainer.Reset(startClock:false)` 强制停止从选曲页残留的 decoupled clock，并新增 `GameplayClockContainer.SoftUnpause()` 使 `isPaused=false` 但不启动底层时钟，让 `FrameStabilityContainer` 在 pre-start 期间仍能处理子组件（修复 playfield 不渲染的问题）。
- 拆分 `UI_PreStartHold`（按住阻塞开谱并弹出 pre-start overlay）与 `UI_LaneCoverFocus`（单击循环 `Sudden / Hidden / Lift` 持久目标）为独立键位。新增 `BmsAction.PreStartHold` 枚举值、`OmsBmsActionMap` 全变体映射、`BmsInputStrings.PreStartHold` 本地化字符串，让 `UI_PreStartHold` 在设置面板可见。
- `UI_LaneCoverFocus` 语义从 hold-to-temporarily-switch-to-Hidden 改为 click-to-cycle：按下时触发 `CycleGameplayAdjustmentTarget()` 在 `Sudden → Hidden → Lift` 之间循环，松开后不再恢复。修复了启用多个 mod 时无法切换到 Lift 的问题。
- `DrawableBmsRuleset.canAdjustGameplaySettings` 新增 `FrameStableClock?.IsRunning ?? true` 检查，防止 pre-start 期间（IsPaused=false 但 IsRunning=false）无 hold 键时意外调节。
- 默认键位：5K/7K/9K `UI_PreStartHold` = Q、`UI_LaneCoverFocus` = W；14K `UI_PreStartHold` = T、`UI_LaneCoverFocus` = Y。
- 验证：`TestSceneBmsSoloPlayerPreStart` **6/6** 通过；`BmsRulesetModTest` **40/40** 通过；`Build osu! (Release)` 通过。

### P1-A / P1-C：tri-mode Hi-Speed surface 与 pre-start hold 调速窗口落地

- `osu.Game.Rulesets.Bms` 已新增 `BmsHiSpeedMode` 与 `BmsHiSpeedRuntimeCalculator`；设置页现可在 `Normal / Floating / Classic Hi-Speed` 三种模式间切换，并只显示当前模式数值，不再把 `GN / ms` 写进 settings。
- `DrawableBmsRuleset` 现已按模式发布 mode-aware `BmsScrollSpeedMetrics`、HUD detail line 与 OSD toast，其中 `Classic` 继续锁定官方 sample `HS 10 + WN 350 => GN 300`，`Floating` 首轮按 initial BPM 锚定 visual speed，但仍不宣称完整 mid-song re-float parity。
- BMS song select 进入游玩后现有 5 秒 delayed start；按住 `UI_PreStartHold` 会阻塞开谱并显示 pre-start overlay，期间可按键位奇数列加速、偶数列减速，且 `UI_LaneCoverFocus` / 滚轮 / 中键仍可继续调节 `Sudden / Hidden / Lift` 与目标切换。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~BmsRulesetConfigurationTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~BmsDrawableRulesetTest"` **97/97** 通过；`Build osu! (Release)` 通过。

### P1-A / P1-C：strict Classic Hi-Speed + frozen geometry surface 落地

- `osu.Game.Rulesets.Bms` 已把 Classic Hi-Speed 的 base time 从上游 mania 的 `11485 / HS` 改为官方 sample 对齐的 `(100000 / 13) / HS`，并由 `BmsScrollSpeedMetricsTest` 锁定 `HS 10 + WN 350 => GN 300`
- `BmsPlayfield` 不再在运行时消费 playfield / receptor / bar-line 的 layout override，`BmsSettingsSubsection` 也已移除 geometry sliders；内部 `BmsPlayfieldLayoutProfile` abstraction 仍保留给 ruleset / skin 侧使用
- 当前公开 `Classic Hi-Speed` 范围仍保持 `1.0 - 20.0`，但这次已不只是范围收口，而是把 strict Classic surface 一并锁定
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~BmsRulesetConfigurationTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~TestSceneBmsPlayfieldLayoutConfig|FullyQualifiedName~BmsLaneLayoutTest|FullyQualifiedName~BmsDrawableRulesetTest"` **91/91** 通过；`Build osu! (Release)` 通过

### P1-A / P1-C：live `PERFECT / FC / FC LOST` 资格线入同一 feedback card

- `osu.Game.Rulesets.Bms` 已为 `BmsJudgementCounts` 新增 live eligibility helper，并进一步补入最轻 break bucket 派生语义，`DefaultBmsSpeedFeedbackDisplay` 现可直接从既有 counts 派生带紧凑原因标签的 live `PERFECT / FC / FC LOST` 状态线
- 本次没有继续扩大 `BmsGameplayFeedbackState`；它确认了这类 display-only 的 judge feedback 可以复用现有 aggregate snapshot，而不必新增 runtime state 发布面
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsExScoreProgressInfoTest|FullyQualifiedName~BmsExScorePacemakerInfoTest|FullyQualifiedName~BmsJudgementCountsTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **69/69** 通过；`Build osu! (Debug)` 通过

### P1-A / P1-C：live EX progress 并入 aggregate gameplay feedback snapshot

- `osu.Game.Rulesets.Bms` 已新增 `BmsExScoreProgressInfo`，把当前 `EX-SCORE / MAX EX-SCORE` 快照为轻量值对象，并并入 `BmsGameplayFeedbackState`
- `DefaultBmsSpeedFeedbackDisplay` 现会在同一张 feedback card 中显示 live `DJ LEVEL + EX 原始分子/分母 + %`，与既有最近判定、timing sparkline、compact judgement summary 和 fixed AAA EX pacemaker 共用同一条反馈容器
- `BmsGameplayFeedbackState` 现已继续把 live EX progress 一并收口到 aggregate snapshot，而 recent history 仍保持独立列表态
- 验证：后续沿同一 feedback family 的聚焦回归已升至 `dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsExScoreProgressInfoTest|FullyQualifiedName~BmsExScorePacemakerInfoTest|FullyQualifiedName~BmsJudgementCountsTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **69/69** 通过；`Build osu! (Debug)` 通过

### P1-A / P1-C：compact live judgement summary 并入 aggregate gameplay feedback snapshot

- `osu.Game.Rulesets.Bms` 已新增 `BmsJudgementCounts`，把 live score statistics 快照为轻量值对象，并并入 `BmsGameplayFeedbackState`
- `DefaultBmsSpeedFeedbackDisplay` 现会在同一张 feedback card 中显示两行 compact live judgement summary：`PGR / GR / GD` 与 `BD / PR / EP`
- `BmsGameplayFeedbackState` 现已继续把 judgement counts 一并收口到 aggregate snapshot，而 recent history 仍保持独立列表态
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsJudgementCountsTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **59/59** 通过；`Build osu! (Debug)` 通过

### P1-A / P1-C：aggregate gameplay feedback state contract 第二刀

- `BmsGameplayFeedbackState` 现已额外包含 `TimingFeedbackVisualRange`，让 compact timing sparkline 的 scalar 输入也并入同一条 aggregate snapshot
- `DefaultBmsSpeedFeedbackDisplay` 现已收口为消费 `GameplayFeedbackState` 加 `RecentJudgementFeedbacks` 列表，不再额外直接绑定 `TimingFeedbackVisualRange` scalar
- 新增 `BmsGameplayFeedbackStateTest`，并扩展 `BmsRulesetModTest`、`TestSceneBmsSpeedFeedbackDisplay`、`BmsSkinTransformerTest`，锁定 snapshot 值语义、ruleset 镜像与 sparkline/expiry 行为
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~BmsSkinTransformerTest"` **153/153** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### P1-A / P1-C：aggregate gameplay feedback state contract 首刀

- `osu.Game.Rulesets.Bms` 已新增 `BmsGameplayFeedbackState`，把 speed metrics、target-state、最近判定与 fixed AAA pacemaker 这批 scalar gameplay feedback 收口为单个 snapshot
- `DrawableBmsRuleset` 现额外暴露 `GameplayFeedbackState`；`DefaultBmsSpeedFeedbackDisplay` 已改为优先消费该 aggregate state，而不是继续分别绑定多组 ruleset scalar bindable
- recent timing history 与 visual range 仍保持独立状态流，避免把列表态与瞬时标量语义硬塞进同一个 snapshot
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsGameplayFeedbackLayoutTest|FullyQualifiedName~TestSceneBmsJudgementDisplayPosition"` **154/154** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### P1-C：fixed AAA EX pacemaker 入同一 feedback card

- `DrawableBmsRuleset` 已新增 `ExScorePacemakerInfo`，把 fixed AAA 目标的 EX pacemaker 状态暴露给 HUD
- `DefaultBmsSpeedFeedbackDisplay` 现会在同一张 feedback card 中显示 `PAC AAA +/-n` 文案，且差值按当前已判对象的目标节奏推进，而不是从开局起显示整局最终目标缺口
- 新增 `BmsExScorePacemakerInfoTest`，并扩展 `TestSceneBmsSpeedFeedbackDisplay` 锁定 pacemaker 计算与文案 / 配色回归
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsExScorePacemakerInfoTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~BmsRulesetModTest"` **52/52** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### P1-C：compact visual timing-offset 入同一 feedback card

- `DrawableBmsRuleset` 已新增 `RecentJudgementFeedbacks` 与 `TimingFeedbackVisualRange`，把 recent timing history 与当前局 visual range 暴露给 HUD
- `DefaultBmsSpeedFeedbackDisplay` 现会在同一张 feedback card 中显示 compact visual timing-offset sparkline，并只吸收有 timing 语义的 recent basic judgement
- `BmsRulesetModTest` 与 `TestSceneBmsSpeedFeedbackDisplay` 已补 runtime / visual 回归，锁定 recent history 过滤与 sparkline 渲染
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **158/158** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### P1-C：最近判定 feedback 改为瞬时 judge display

- `DefaultBmsSpeedFeedbackDisplay` 里的最近判定 feedback 不再永久停留，而是按短时 judge display 语义自动消隐
- 相同判定与相同 `FAST/SLOW` 偏移再次出现时，显示窗口会被刷新，而不是沿用旧的过期时钟
- `TestSceneBmsSpeedFeedbackDisplay` 已补“过期消隐”和“同值刷新续时”回归，并改用 `display.Time.Current` 对齐组件自己的时钟
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **157/157** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### P1-C：最近判定与 `FAST/SLOW` 入同一 feedback container

- `DrawableBmsRuleset` 已新增 `LatestJudgementFeedback`，并用 `BmsJudgementTimingFeedback` 把 `JudgementResult` 快照成 HUD 可消费的轻量状态
- `DefaultBmsSpeedFeedbackDisplay` 现会在同一 feedback container 中显示最近判定与 `FAST/SLOW` timing 文案，例如 `PGREAT | FAST 3.2ms`
- `EPOOR` 这类无真实 timing 语义的结果只显示判定名，不再硬附 `FAST/SLOW` 后缀
- `BmsRulesetModTest` 与 `TestSceneBmsSpeedFeedbackDisplay` 已补 runtime / visual 回归
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **155/155** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### P1-C：speed feedback HUD 显式区分 `HOLD` 临时覆写态

- `DrawableBmsRuleset` 已新增 `IsAdjustmentTargetTemporarilyOverridden`，把当前显示 target 是否为临时覆写暴露给 HUD
- `DefaultBmsSpeedFeedbackDisplay` 在按住 `UI_LaneCoverFocus` 导致的临时覆写场景下，现会显示 `HID HOLD` 这类显式文案，而不是继续沿用普通 cycle 文案
- `BmsRulesetModTest` 与 `TestSceneBmsSpeedFeedbackDisplay` 已补运行时与视觉回归
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **152/152** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### P1-C：恢复 `UI_LaneCoverFocus` 的 Hidden 临时覆写

- `DrawableBmsRuleset` 已恢复 `UI_LaneCoverFocus` 的按住型语义：按住时滚轮会临时转向 `Hidden`，松开后回到持久 target
- target cycle 入口已明确收口到鼠标中键点击，不再复用 lane cover focus 信号
- `BmsRulesetModTest` 已补“临时覆写不会改写持久 target，释放后回退”回归
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **151/151** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### P1-C：speed feedback 多 target cycle 序号入 HUD

- `DrawableBmsRuleset` 已新增 `ActiveAdjustmentTargetIndex`，把当前 target 在 `Sudden / Hidden / Lift` 可切换序列中的位置暴露给 HUD
- `DefaultBmsSpeedFeedbackDisplay` 在多 target 状态下现会显示显式序号，例如 `SUD 1/3`、`HID 2/3`，不再只显示 target 简写
- `BmsRulesetModTest` 与 `TestSceneBmsSpeedFeedbackDisplay` 已补 index 回归，锁定无 target、单 target、三 target cycle 的运行时与显示语义
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **150/150** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### P1-C：speed feedback target-state 首轮收口

- `DrawableBmsRuleset` 已新增 `EnabledAdjustmentTargetCount`，把 runtime 中可用的 `Sudden / Hidden / Lift` 调节目标数量暴露给 HUD
- `DefaultBmsSpeedFeedbackDisplay` 现在会按 target 可用性区分 `NONE`、`{TARGET} ONLY` 与多 target 可切换三种状态，不再只显示当前 active target
- `BmsRulesetModTest` 与 `TestSceneBmsSpeedFeedbackDisplay` 已补 target-state 回归，锁定无 target / 单 target / 多 target 的产品语义
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **149/149** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### P1-C：BMS 常驻 speed feedback HUD 首轮实现

- `osu.Game.Rulesets.Bms` 已新增公共 `BmsGameplayAdjustmentTarget`，并把 `DrawableBmsRuleset` 的 runtime 速度反馈状态提升为可绑定的 `SpeedMetrics` / `ActiveAdjustmentTarget`
- `BmsScrollSpeedMetrics` 已补 `IEquatable<>`；`BmsSkinComponents` 新增 `SpeedFeedback`；`DefaultBmsSpeedFeedbackDisplay` 已以 `IBmsSpeedFeedbackDisplay` 形式挂入 BMS HUD，显示 `GN + 可见毫秒 + HS + 当前目标`
- HUD 集成采用向后兼容策略：新增 `IBmsHudLayoutDisplayWithGameplayFeedback` 供新 layout 显式接入 speed feedback，旧 layout 则由 transformer 自动包 overlay 容器，不直接破坏既有皮肤接口
- 新增 `TestSceneBmsSpeedFeedbackDisplay`，并扩展 `BmsSkinTransformerTest` / `BmsScrollSpeedMetricsTest` / `TestSceneBmsUserSkinFallbackSemantics`，锁定 speed feedback 文案、警告态、fallback 与 legacy HUD 兼容语义
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **113/113** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### 文档重构：根目录 Markdown 收口到 doc_md，并补齐主线 / 主子线 / 参考线索引

- 根目录除 README 外的现有 Markdown 文档现已统一迁入 `doc_md/`，并按 `mainline / subline / other / mini` 四层分类；仓库根 README 继续保留为项目入口，`Templates/README.md` 继续保留为模板说明
- 新增 `doc_md/README.md` 与各层级 README 索引，后续文档导航统一从文档总索引进入，不再依赖根目录平铺查找
- 本轮同步修正了 README 与各 Markdown 文档的相对链接，避免移动后出现断链

### P1-A / P1-C：皮肤设计边界与绿色数字 / Mod 联动专题建档

- 旧的自由命名专题已拆分并正式挂到 `doc_md/subline/P1-A/` 与 `doc_md/subline/P1-C/`；`P1-A` 主承接皮肤边界、HUD 宿主与 release gate，`P1-C` 主承接绿色数字、速度反馈、判定语义与训练反馈闭环
- 主线文档已挂接这两条正式子线：`DEVELOPMENT_PLAN.md` 现把这条工作归线为 `P1-A / P1-C` 交叉主子线，`DEVELOPMENT_STATUS.md` 记录当前已完成设计审计且常驻 GN HUD / FAST-SLOW / judge display 仍未开始代码实现，`OMS_COPILOT.md` 补上了“不得直接破坏现有 HUD 布局接口、不得把当前 GN 直接包装成完整 FHS”的硬约束
- 本轮仅进行文档重构与规划建档，未新增代码构建或测试执行

## 2026-04-19

### BMS：lane cover 语义纠正为 Sudden/Hidden，新增独立 Lift，并将运行时速度反馈切到 GN 主表达

- `BmsScrollSpeedMetrics` 现已扩展为 ruleset-owned runtime 指标入口：除基础时长与可见时长外，还暴露 `SuddenUnits` / `HiddenUnits` / `LiftUnits` / `WhiteNumber` / `GreenNumber`；`DrawableBmsRuleset` 的调速 OSD 已改为 `GN xxx (yyyms)` 主表达，`BmsSettingsSubsection` 的设置文案改为 `Classic Hi-Speed`
- 进一步补齐游玩内调节链：滚轮现在会直接调当前启用的 `Sudden / Hidden / Lift` 目标，默认按 `Sudden -> Hidden -> Lift` 的顺序选择；鼠标中键会只在 2 个及以上已启用项时拦截并循环切换目标，原有 `UI_LaneCoverFocus` 仍保留为 `Hidden` 的临时覆写
- 为避免 gameplay 内继续弹出“基础 ms”这种过时反馈，`BmsRulesetConfigManager` 已停止把 scroll speed 作为 tracked setting 暴露给通用 OSD；本轮配套更新了 mod 测试、lane cover scene、skin fallback 测试、playfield layout 测试，以及 `DEVELOPMENT_PLAN.md` / `OMS_COPILOT.md` 的实现说明
- 验证：`dotnet build osu.Game.Rulesets.Bms\osu.Game.Rulesets.Bms.csproj -c Release /v:m` 通过；`dotnet build osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj -c Release /v:m` 通过；定向 `BmsRulesetModTest`、`BmsScrollSpeedMetricsTest`、`TestSceneBmsPlayfieldLayoutConfig` 合计 **53/53** 通过

### 文档、仓库记忆与上游差异文档联动校准

- `README.md`、`DEVELOPMENT_PLAN.md`、`DEVELOPMENT_STATUS.md`、`RELEASE.md`、`SKINNING.md`、`UPSTREAM.md` 与仓库摘要记忆已按 2026-04-19 代码状态同步：README 的皮肤现状不再停留在早期“mania 内置简约白蓝黄”描述；皮肤手册里的 mania authoring 风险也改为反映当前 shell / preset 接线与 8 类 OMS-owned 组件已落地、但 release-facing contract 仍未冻结的真实状态
- 规模口径现与“历史测试快照”拆开表达：按 2026-04-19 本地文件计数（排除 `bin/obj`），`osu.Game.Rulesets.Bms` 约 **146** 个源文件、`oms.Input` **15** 个、`osu.Game.Rulesets.Bms.Tests` **49** 个测试源文件；最近一次完整项目级自动化回归仍沿用 2026-04-17 的 **608/608** 已验证快照
- `UPSTREAM.md` 已从过时的少量文件清单改为当前可操作的本地 diff 基线：保留上游 tag commit `bb289363a2b8e6bf62be355f8570def018f0d7be` 作为语义锁定点，同时明确当前仓库本地应以 bootstrap commit `0b97bbdd4348de47e1d597a65f0a7734ad184000` 与 `HEAD` 比较；2026-04-19 本地审计下 `osu.Game/` 共 **147** 个变更路径（**113 M / 30 A / 4 D**），高风险目录集中在 `Screens`、`Beatmaps`、`Localisation`、`Overlays`、`Rulesets` 与 `Skinning`
- 本轮仅做文档与记忆同步，未新增自动化测试执行；最近一次已验证 gates 仍为 BMS **608/608**、mania OMS **92/92**、BMS fallback **92/92**、scratch bridge **43/43**、`osu.Game.Tests` release-gate **6/6**

## 2026-04-17

### 在线提交边界：保留 ruleset_data，避免未来 leaderboard 混算 BMS 语义

- `SoloScoreInfo` 现已显式序列化 `ruleset_data`，并在 `ToScoreInfo()` 回填到 `ScoreInfo.RulesetDataJson`；这样将来启用私服/在线排行榜时，BMS 的 `long_note_mode`、judge/gauge 等 ruleset-specific payload 不会在通用 score submission 通道里丢失
- 现有 `SubmitScoreRequest` / `SubmitSoloScoreRequest` 无需改调用面，`SoloScoreInfo.ForSubmission(score)` 已会自动携带本地 score 的 `RulesetDataJson`
- 新增在线序列化回归：`TestSoloScoreInfoJsonSerialization` 现锁定 `ruleset_data` 的输出与 round-trip 恢复，避免未来重构把 BMS 的 LN/CN/HCN、judge、gauge 约束从在线载荷中意外删掉
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~TestSoloScoreInfoJsonSerialization|FullyQualifiedName~TestAPIModJsonSerialization"` **5/5** 通过

### 文档与仓库记忆：按当前代码基线重整

- `README.md`、`DEVELOPMENT_STATUS.md`、`DEVELOPMENT_PLAN.md` 与 `IIDX_REFERENCE_AUDIT.md` 已同步到当前代码状态：四套 judge mode、Mirror / Random、A-SCR / BMS Autoplay、BMS replay 录制/回放/归档、`chartbms/` / `chartmania/` 存储命名，以及外部谱库维护 UI
- 当前规模快照已刷新为：`osu.Game.Rulesets.Bms` 147 个源文件、`oms.Input` 15 个源文件、`osu.Game.Rulesets.Bms.Tests` 46 个测试文件
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal` **608/608** 通过

### BMS：新增 Mirror / Random 训练向 lane rearrangement mod

- `osu.Game.Rulesets.Bms` 现已新增 `BmsModMirror` 与 `BmsModRandom`，并统一暴露在 `Conversion`
- `BmsModRandom` 当前支持 `RANDOM`、`R-RANDOM`、`S-RANDOM` 三种模式，内置 `Seed` 与手动 `Custom pattern` 配置；14K 下单组 pattern 可自动复制到双侧
- runtime beatmap mod 统一入口 `BmsBeatmapModApplicator` 现已先应用 `Mirror` / `Random`，再继续 long-note mode、judge mode 与 `Auto Scratch`
- 验证：`Build osu! (Debug)` 通过；`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v q --results-directory TestResults --logger "trx;LogFileName=bms-mirror-random.trx"` 结果为 **583/583** 通过

### BMS：新增 Auto Scratch 与 BMS Autoplay assist mod

- `osu.Game.Rulesets.Bms` 现已新增 `BmsModAutoScratch` 与 `BmsModAutoplay`，并统一暴露在 `DifficultyReduction`
- A-SCR 当前会把 scratch runtime 语义切换为 `AutoPlay = true` + 不参与判定 / 计分 / gauge / MaxExScore；mod 内支持可见性、染色开关与染色颜色配置
- BMS autoplay 当前已补齐专用 replay frame / replay input handler / replay recorder / auto generator，并已接入 ruleset / drawable ruleset / score processor
- 验证：初次落地时 `Build osu! (Debug)` 通过；后续在 `Mirror` / `Random` 一并接入后，当前项目级 BMS 测试已更新为 **583/583** 通过

### 文档同步：将 BMS 结果页反馈面编入 Phase 1.x / P1-C

- `DEVELOPMENT_PLAN.md` 现已明确把 BMS 结果页反馈面收口归入 `P1-C`，并把边界钉死在“沿用现有 lazer results 骨架的低风险增强”上，不把 beatoraja 风格整页重构写成当前主线
- `DEVELOPMENT_STATUS.md` 现已同步记录当前已落地的结果页反馈基线：expanded 主评价与 contracted badge 已按 `DJ LEVEL` 显示，主分数区已显式标为 `EX-SCORE`
- `README.md` 已补齐对外可见的当前状态说明，避免外部摘要继续停留在“有 EX-SCORE / DJ LEVEL 数据，但不说明结果页主表达语义”的旧表述
- 验证：本次仅为文档编排与进度同步，不涉及新的代码或测试命令

## 2026-04-13

### 修复外部谱库设置 UI 不可见（DI 注册时序 + CanBeNull）

- **根因**：`ExternalLibraryConfig` 与 `ExternalLibraryScanner` 在 `OsuGameDesktop.LoadComplete()` 中才创建和 `CacheAs`，但 Settings overlay 的 async 加载（`loadComponentSingleFile` via `Schedule`）可能在此之前解析依赖。同时 `[Resolved]` 未标注 `CanBeNull = true`，框架在类型未注册时直接抛异常，导致 `ExternalLibrarySettings` subsection 整体加载失败
- **修复**：
  1. 新增 `OsuGameDesktop` 的 `[BackgroundDependencyLoader] load()`，在 BDL 阶段（base BDL 之后、任何 scheduled load 之前）创建 `ExternalLibraryConfig`/`ExternalLibraryScanner` 并 `CacheAs` 注册到 `desktopDependencies`
  2. `LoadComplete` 中仅保留 importer 委托接线（`BmsDirectoryImporter` / `ManiaDirectoryImporter`），因为 `BmsBeatmapImporter` / `ManiaBeatmapImporter` 在 `LoadComplete` 创建
  3. `ExternalLibrarySettings` 所有 `[Resolved]` 统一加 `CanBeNull = true`，确保非桌面上下文安全降级
- 构建验证：0 warning / 0 error
- 定向验证：BMS **519/519** 通过，mania OMS **92/92** 通过，osu.Game.Tests release-gate **6/6** 通过

## 2026-04-12

### 外部谱库设置 UI + 存储目录重命名

- 新增 `ExternalLibrarySelectScreen`（基于 `DirectorySelectScreen` 的全屏目录选择器），新增 `ExternalLibrarySettings`（Settings → Maintenance 子区域）：可在设置中添加 BMS / mania 外部谱库根目录、查看已注册根列表（路径有效性 + 类型/状态/最近扫描信息）、移除根目录、一键扫描全部根目录（带进度通知）
- `OsuGameDesktop` 新增 `CreateChildDependencies` 覆盖，将 `ExternalLibraryConfig` 与 `ExternalLibraryScanner` 注册到 DI 容器，设置 UI 通过 `[Resolved]` nullable 解析（非桌面端安全降级）
- `MaintenanceSection` 子区域列表增加 `ExternalLibrarySettings` 入口
- 存储目录重命名：`songs/` → `chartbms/`、`mania/` → `chartmania/`，`SONGS_STORAGE_PATH` / `MANIA_STORAGE_PATH` 常量与全部代码注释/文档同步更新
- 构建验证：0 warning / 0 error
- 定向验证：BMS **519/519** 通过，mania OMS **92/92** 通过，osu.Game.Tests release-gate **6/6** 通过

### 修复 FilterControl.updateSortDropdownState 在二次进入 Song Select 时因 Bindable Disabled 状态残留而崩溃

- **根因**：`updateSortDropdownState()` 在 DifficultyTable 分组下将 `sortDropdown.Current.Disabled = true`。此 Disabled 状态通过 `config.BindWith` 传播到全局 config bindable。第二次进入 Song Select 时，新 sortDropdown 通过 `BindWith` 继承了 `Disabled = true`，随后 `updateSortDropdownState()` 试图设置 `Value = SortMode.Difficulty` 但 bindable 已禁用 → 抛出 `InvalidOperationException`
- **影响**：`FilterControl.LoadComplete()` 在 line 222 中断，后续所有 `BindValueChanged` 回调和末尾的 `updateCriteria()` 均未执行。虽然前一个修复保证了初始 Criteria 到达 carousel，但 FilterControl 的事件链完全断裂，导致分组/排序联动失效
- **修复**：在 `updateSortDropdownState()` 设值前先 `sortDropdown.Current.Disabled = false`，设值完成后再禁用
- 构建验证：0 warning / 0 error
- 定向验证：BMS **519/519** 通过，mania OMS **92/92** 通过，osu.Game.Tests release-gate **6/6** 通过

### 修复 Song Select 初始筛选条件丢失导致的空谱面列表

- **根因**：`FilterControl.LoadComplete()` 在 `SongSelect.LoadComplete()` 之前执行。FilterControl 末尾调用 `updateCriteria()` 触发 `CriteriaChanged` 事件时，SongSelect 尚未订阅该事件，导致初始筛选条件丢失。BeatmapCarousel 的 `Criteria` 保持 `null`，`FilterAsync()` 每帧短路返回空集，谱面始终不显示
- **触发场景**：在 Song Select 将分组设为 Difficulty Table → 返回主菜单 → 重新进入 Song Select。因 DifficultyTable 模式下所有子条目默认 `IsVisible = false`（需展开分组），缺少初始 Criteria 意味着连分组表头都不会创建
- **修复**：在 `SongSelect.LoadComplete()` 订阅 `CriteriaChanged` 后，立即调用 `criteriaChanged(FilterControl.CreateCriteria())`，确保 BeatmapCarousel 总能收到首次筛选条件
- **影响范围**：修复适用于所有分组模式；DifficultyTable 最易触发是因为该模式不会被 API 登录等延迟事件意外「救回」
- 构建验证：0 warning / 0 error
- 定向验证：BMS **519/519** 通过，mania OMS **92/92** 通过，osu.Game.Tests release-gate **6/6** 通过

### 存储拓扑演进基线 + 外部多目录谱库扫描 + mania 独立目录存储

- 新增 `ExternalLibraryRoot`（数据模型）+ `ExternalLibraryConfig`（JSON `library-roots.json` 配置管理器），支持注册/移除/启用外部谱库根目录，BMS / mania 双类型均可配置
- 新增 `ExternalLibraryScanner`（委托注入式扫描器），遍历已注册根目录的直接子目录，按文件扩展名（BMS: `.bms/.bme/.bml/.pms`；mania: `.osu`）自动分类并分派到对应导入器，返回 `ScanResult{Imported, Skipped, Errors}`
- 新增 `ManiaFolderImporter`（`chartmania/<safeName-hash>/` 文件系统直读导入器），解析 .osu 文件 → 提取元数据/难度/哈希 → 复制目录 → 设置 `FilesystemStoragePath` → 写入 Realm；与 BMS `chartbms/` 同级的独立目录树
- 新增 `ManiaBeatmapImporter`（`ICanAcceptFiles` 封装），仅处理目录（.osz 继续走标准 `BeatmapImporter`），支持拖放导入与进度通知
- `OsuGameDesktop` 集成：注册 `ManiaBeatmapImporter` 作为导入处理器，创建 `ExternalLibraryConfig` 与 `ExternalLibraryScanner` 并接通 BMS / mania 导入委托，`Dispose` 清理已补齐
- 构建验证：0 warning / 0 error
- 定向验证：BMS **519/519** 通过，mania OMS **92/92** 通过

### Phase 1.17：reverse-config late-hit sweep 收口

- `TestSceneOmsScratchGameplayBridge` 本轮继续沿 reverse-config 产品矩阵补齐 late-hit miss 排序，新增四条 loaded-scene 回归：`TestInvertedMouseAxisGameplayBridgeLateHitForcesEarlierScratchMiss()`、`TestInvertedHidAxisGameplayBridgeLateHitForcesEarlierScratchMiss()`、`TestInvertedSecondScratchMouseAxisGameplayBridgeLateHitForcesEarlierScratchMiss()`、`TestInvertedSecondScratchHidAxisGameplayBridgeLateHitForcesEarlierScratchMiss()`
- 这批场景显式锁定 `axisInverted=true` 的 mouse/HID scratch 在 Scratch1 与 lane 8 / `Scratch2` 两侧都遵循与正向输入相同的 late-hit 语义：晚到输入会强制 earlier note miss，而 later note 仍可正常命中，不会因为 reverse-config 改变 miss 排序
- 定向验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsScratchGameplayBridge" -v minimal` **43/43** 通过

### Phase 1.17：scratch bridge symmetry sweep 收口

- `TestSceneOmsScratchGameplayBridge` 本轮沿同一 loaded-scene 产品矩阵继续做对称补齐，新增六条回归：`TestKeyboardHeldScratchSuppressesInvertedHidPulseGameplayEdgeUntilFinalRelease()`、`TestKeyboardHeldScratchSuppressesInvertedMousePulseGameplayEdgeUntilFinalRelease()`、`TestSecondScratchMouseAxisGameplayBridgeLateHitForcesEarlierScratchMiss()`、`TestSecondScratchHidAxisGameplayBridgeLateHitForcesEarlierScratchMiss()`、`TestSecondScratchXInputGameplayBridgeLateHitForcesEarlierScratchMiss()`、`TestSecondScratchXInputScratchHoldResolvesTail()`
- 这批场景把 Scratch1 的 inverted suppression、以及 14K `Scratch2` 的 late-hit miss 排序与 direct XInput held-path 全部纳入同一产品级回归，显式锁定 reverse-config pulse 不会在 keyboard-held 时产生额外 gameplay edge，lane 8 / `Scratch2` 的晚到输入会强制 earlier note miss 且 later note 仍可正常命中，同时 second scratch 也已具备不依赖 keyboard takeover 的 direct XInput hold 尾判与最终释放证明
- 定向验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsScratchGameplayBridge" -v minimal` **39/39** 通过

### Phase 1.17：14K Scratch2 normal hold-survival 收口

- `TestSceneOmsScratchGameplayBridge` 已继续补齐第二 scratch 的 held-path 产品语义，本轮新增两条 loaded-scene 回归：`TestKeyboardHeldSecondScratchHoldSurvivesMousePulseAndResolvesTail()`、`TestKeyboardHeldSecondScratchHoldSurvivesHidPulseAndResolvesTail()`
- 这批场景显式锁定 lane 8 / `Scratch2` 的 keyboard-held hold 在普通 mouse/HID pulse 经过 `FinishFrame()` / `FinishPolling()` 边界后不会断 hold，tail 仍经 held path 判定，且动作直到最终 keyboard release 才真正松开
- 定向验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsScratchGameplayBridge" -v minimal` **33/33** 通过

### Phase 1.17：14K Scratch2 inverted suppression 收口

- `TestSceneOmsScratchGameplayBridge` 已继续补齐第二 scratch 的 reverse-config first-press / final-release 产品语义，本轮新增两条 loaded-scene 回归：`TestKeyboardHeldSecondScratchSuppressesInvertedHidPulseGameplayEdgeUntilFinalRelease()`、`TestKeyboardHeldSecondScratchSuppressesInvertedMousePulseGameplayEdgeUntilFinalRelease()`
- 这批场景显式锁定 lane 8 / `Scratch2` 在 keyboard-held 且 `axisInverted=true` 的 HID、mouse 追加 pulse 下不会产生额外 gameplay hit edge，且动作会一直保持到 keyboard 与 inverted pulse 全部真正释放后才结束
- 定向验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsScratchGameplayBridge" -v minimal` **31/31** 通过

### Phase 1.17：14K Scratch2 mixed-source suppression 收口

- `TestSceneOmsScratchGameplayBridge` 已继续补齐第二 scratch 的 first-press / final-release 产品语义，本轮新增三条 loaded-scene 回归：`TestKeyboardHeldSecondScratchSuppressesHidPulseGameplayEdgeUntilFinalRelease()`、`TestKeyboardHeldSecondScratchSuppressesMousePulseGameplayEdgeUntilFinalRelease()`、`TestKeyboardHeldSecondScratchSuppressesXInputGameplayEdgeUntilFinalRelease()`
- 这批场景显式锁定 lane 8 / `Scratch2` 在 keyboard-held 前提下不会因 HID、mouse 或 custom XInput 的追加输入产生额外 gameplay hit edge，并且动作会一直保持到最终 source release 才真正松开
- 定向验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsScratchGameplayBridge" -v minimal` **29/29** 通过

### Phase 1.17：14K Scratch2 reverse-config gameplay bridge 收口

- `TestSceneOmsScratchGameplayBridge` 已继续把 reverse-config 扩到 14K 第二 scratch，本轮新增四条 loaded-scene 回归：`TestInvertedSecondScratchMouseAxisGameplayBridgeResolvesScratchStreamNotes()`、`TestInvertedSecondScratchHidAxisGameplayBridgeResolvesScratchStreamNotes()`、`TestKeyboardHeldSecondScratchHoldSurvivesInvertedMousePulseAndResolvesTail()`、`TestKeyboardHeldSecondScratchHoldSurvivesInvertedHidPulseAndResolvesTail()`
- 这批场景显式锁定 lane 8 / `Scratch2` 在 `axisInverted=true` 的 mouse/HID 绑定下仍能产出真实 scratch edge，并且 keyboard-held second scratch hold 在 inverted mouse/HID pulse 经过 `FinishFrame()` / `FinishPolling()` 边界时不会断 hold，直到最终 keyboard release 才真正松开动作
- 定向验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsScratchGameplayBridge" -v minimal` **26/26** 通过

### Phase 1.17：14K Scratch2 gameplay bridge 首轮补测

- `TestSceneOmsScratchGameplayBridge` 现已支持把 mouse-axis / HID-axis / custom XInput scratch 绑定到可选 `OmsAction`，不再只覆盖 `Key1P_Scratch`；同一套 loaded headless scene 现在可以直接验证 `Key2P_Scratch -> BmsAction.Scratch2` 的真实 `DrawableBmsRuleset -> BmsPlayfield -> scratch note/hold` 玩法桥
- 本轮新增四条 14K 第二 scratch 回归：`TestSecondScratchMouseAxisGameplayBridgeResolvesScratchStreamNotes()`、`TestSecondScratchHidAxisGameplayBridgeResolvesScratchStreamNotes()`、`TestSecondScratchXInputGameplayBridgeResolvesScratchStreamNotes()`、`TestKeyboardHeldSecondScratchHoldTransfersToXInputAndResolvesTail()`。它们显式锁定 lane 8 / `Scratch2` 的 mouse、HID、custom XInput 命中链，以及 keyboard-held hold 在 second scratch 的 XInput 接管与最终释放语义
- 定向验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsScratchGameplayBridge" -v minimal` **22/22** 通过

### Phase 1.17：analog scratch reverse-config gameplay bridge 收口首刀

- `TestSceneOmsScratchGameplayBridge` 现已支持为 mouse-axis / HID-axis scratch 注入自定义 trigger，不再只用硬编码的正向 turntable 绑定；这样 loaded headless scene 可以直接覆盖 reverse-config 下的真实 `DrawableBmsRuleset -> BmsPlayfield -> scratch note/hold` 运行链
- 本轮新增四条 1.17 产品语义回归：`TestInvertedMouseAxisGameplayBridgeResolvesScratchStreamNotes()`、`TestInvertedHidAxisGameplayBridgeResolvesScratchStreamNotes()`、`TestKeyboardHeldScratchHoldSurvivesInvertedMousePulseAndResolvesTail()`、`TestKeyboardHeldScratchHoldSurvivesInvertedHidPulseAndResolvesTail()`。它们显式锁定 `axisInverted=true` 的 mouse/HID 绑定仍能产出 scratch edge，且 keyboard-held hold 在 inverted pulse 经过 `FinishFrame()` / `FinishPolling()` 边界时不会断 hold，直到最终 keyboard release 才真正松开动作
- 定向验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsScratchGameplayBridge" -v minimal` **18/18** 通过

## 2026-04-11

### Phase 1.1：SkinManager product-surface release gate 收口

- `TestSceneOmsBuiltInSkin` 现已补齐 `GetAllUsableSkins()`、`SelectRandomSkin()`、`SetSkinFromConfiguration()`、`SelectNextSkin()` / `SelectPreviousSkin()`、`SkinManager.AllSources` 与 `Delete()` 这批产品面回归，明确锁定“OMS 永远是唯一受保护默认项，用户皮肤只作为可选层叠 source”的最终行为
- 本轮把 release gate 从“transformer / fallback 是否存在”推进到“运行时可选皮肤列表、随机切换、配置回退、前后切换、source-chain、删除当前皮肤回退”这一层真实产品语义，避免 1.1.11 只剩代码层契约却缺 UI/状态机层证明
- 定向验证分三批通过：`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --filter "(Name=TestUsableSkinListContainsOmsThenUserSkins|Name=TestRandomSkinFallsBackToOmsWithoutUserSkins|Name=TestRandomSkinSelectsOnlyAvailableUserSkin)"` **3/3**，`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --filter "(Name=TestSetSkinFromConfigurationSelectsUserSkin|Name=TestUnknownSkinConfigurationFallsBackToOms|Name=TestSelectNextSkinCyclesAcrossOmsAndUserSkins|Name=TestSelectPreviousSkinCyclesAcrossOmsAndUserSkins)"` **4/4**，`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --filter "(Name=TestAllSourcesContainsOnlyOmsWhenOmsIsCurrent|Name=TestAllSourcesAddsOmsFallbackBehindUserSkin|Name=TestDeletingCurrentUserSkinFallsBackToOms|Name=TestDeletingNonCurrentUserSkinKeepsCurrentUserSkin)"` **4/4**

### Phase 1.1：startup skin migration 与 `osu.Game.Tests` release gate 恢复

- `OsuGame` 现已在 `SetSkinFromConfiguration()` 前先订阅 `CurrentSkinInfo` 到 config 的回写，遗留 upstream built-in GUID 在启动 fallback 到 OMS 时会同步把配置值纠正为 OMS；`TestSceneStartupSkinMigration` 已新增对应启动迁移回归，并改为使用公开 `CreateInfo().ID`，避免对 internal GUID 常量形成额外耦合
- `osu.Game.Tests.csproj` 现已显式排除仍强依赖已删除 Osu/Taiko/Catch 规则集的历史测试面，同时把 `TestResources` / `WaveformTestBeatmap` 默认 ruleset 改成 mania，`TestSceneHitEventTimingDistributionGraph` 也移除了对 osu! 物件的硬依赖，`TestSceneMissingBeatmapNotification` 则内联轻量 `ArchiveReader` 测试桩；`OsuGameBase` 还补上 API 组件已有父容器时不再二次挂载的保护，恢复 `OsuGameTestScene` 这条 visual regression 链
- 定向验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --filter "TestSceneStartupSkinMigration|TestSceneEditDefaultSkin|TestSettingsMigration"` **6/6** 通过

### Phase 1.1：global `Results` target 与 Skin Editor Results preview 最小闭环

- `GlobalSkinnableContainers` 新增 `Results`，`OmsSkinTransformer` 现会为 global `Results` target 返回 shared shell，`ResultsScreen` 也已补上对应 `SkinnableContainer`；`SKIN/SimpleTou-Lazer/Results.json` 同步补齐 embedded global layout metadata，使 `MainHUDComponents` / `SongSelect` / `Results` / `Playfield` 四类 global target 都有一致的 layout 装载入口
- Skin Editor 现已新增 Results scene 按钮，并通过读取本地已有 `ScoreInfo` 推出 `SoloResultsScreen`；这里刻意复用真实 score 而不是空壳模型，因为 `StatisticsPanel` 这条链要求完整 `ScoreInfo` 才能稳定工作。若本地无可预览成绩，界面会显示明确 toast，而不是静默失败
- 定向验证：`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --filter "TestOmsSkinProvidesEmbeddedGlobalLayoutMetadata"` **1/1** 通过，`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --filter "TestOmsSkinUsesSharedTransformerShell"` **1/1** 通过

## 2026-04-10

### Phase 1.1：native default registration cleanup

- `SkinManager` 构造期现已只维护 `DefaultOmsSkin` 这一条受保护 built-in realm 记录；`Argon` / `ArgonPro` / `Triangles` / `DefaultLegacy` / `Retro` 的历史内建皮肤条目会在启动时被清理，避免上游默认皮肤继续以产品内建项的形式出现在数据库中
- 由于 settings dropdown、random/previous/next skin 逻辑此前已经统一走 OMS + 非受保护用户皮肤列表，本轮等于补齐了 1.1.11 剩余的数据库/产品暴露面；旧的上游 protected skin GUID 仍继续经 `SetSkinFromConfiguration()` 安全回退到 OMS
- `TestSceneOmsBuiltInSkin` 已新增 `TestUpstreamBuiltInSkinsAreNotRegisteredInDatabase()` 回归，直接锁定 `Triangles` / `Argon` / `ArgonPro` / `Classic` / `Retro` 不再注册进 realm；本轮验证为 `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **81/81** 通过、`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics" -v minimal` **75/75** 通过，`Build osu! (Debug)` 通过

### Phase 1.1：mixed-layer 用户皮肤三态 runtime 回归收口

- `TestSceneBmsUserSkinFallbackSemantics` 现已补上真实 mania-only legacy 用户皮肤场景：当用户皮肤只提供 `mania-key*` 这类 legacy mania 资源时，BMS runtime 仍会稳定落到 OMS 默认皮肤包的 BMS 层，`ComboCounter` 与 ruleset HUD 现已分别锁定为 `BmsComboCounter` 与 `DefaultBmsHudLayoutDisplay`
- `TestSceneOmsBuiltInSkin` 现已补上真实 BMS-only 用户皮肤场景：当用户皮肤只提供 BMS lookup（当前以 `BmsSkinComponents.ComboCounter` 作为实际 BMS layer 证明）时，mania gameplay note 路径仍会稳定回落到 OMS mania 层，运行时会继续加载 `OmsNotePiece`
- 同一皮肤选择项同时含 `legacy mania` 资源与 `BMS` lookup 的场景现也已补成双侧 runtime 证明：在 BMS 侧会优先消费该皮肤自身的 `HudLayout` / `ComboCounter`，在 mania 侧则会继续走 `LegacyNotePiece`，且 BMS layer 不会泄漏到 mania note 路径；1.1.10 现在已能明确回答 mania-only、BMS-only、以及 Mania+BMS 同包三类导入/回退语义
- 最新 mixed-layer 定向基线：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics" -v minimal` **75/75** 通过；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **75/75** 通过；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal` **476/476** 通过，`Build osu! (Debug)` 通过

### Phase 1.1：BMS 用户皮肤 fallback 语义收口

- `BmsSkinTransformer` 现已不再让普通用户皮肤在缺失 BMS 组件时直接于当前 transformer 内补齐 built-in 默认件：`HudLayout` / `GaugeBar` / `ComboCounter` / `Judgement` / `NoteDistribution` / `GaugeHistory` / `ResultsSummary` / `StaticBackgroundLayer` / playfield/lane/note/lane-cover 等 BMS lookup 在 non-OMS skin 缺失时现会返回 `null`，把缺省路径继续交给后续 source 链与 OMS fallback
- BMS ruleset HUD 路径也已同步收口：`MainHUDComponents` 仅会在当前 skin 实际暴露 BMS HUD layer 时才在本 source 内组装 HUD；不含 BMS 层的用户皮肤不再拦截后续 source 的 BMS HUD / combo fallback
- `BmsSkinTransformerTest` 已把“默认 fallback”断言切到 OMS source，并新增普通用户皮肤缺失 BMS layer 时返回 `null` 的回归；`TestSceneBmsUserSkinFallbackSemantics` 也已新增 runtime source-chain 验证，锁定缺失 BMS layer 的用户皮肤会把 combo 与 ruleset HUD lookup 继续让给后续 source。`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics" -v minimal` **73/73** 通过，完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal` **474/474** 通过，`Build osu! (Debug)` 通过

### Phase 1.1：legacy 用户皮肤 combo/HUD / bar-line partial override 增量

- `ManiaLegacySkinTransformer` 现已不再因为 key-only legacy 用户皮肤具备 `mania-key*` 就无条件接管 ruleset HUD 与 bar-line：`MainHUDComponents` 现要求实际存在 legacy combo font 才会返回 `LegacyManiaComboCounter` 容器，而 `ManiaSkinComponents.BarLine` 现也改为只在检测到显式 legacy bar-line 样式覆盖时才返回 `LegacyBarLine`
- `LegacySkin` 的 mania config lookup 会为 `BarLineHeight` 始终提供默认值，因此 bar-line 门控不能简单按 bindable 是否存在判断；本轮已把条件收口为“显式 `ColourBarline` 覆盖或 `BarLineHeight` 偏离默认值 1”，避免 key-only legacy 用户皮肤因默认值误占用 OMS fallback
- `TestSceneOmsBuiltInSkin` 已新增 `TestLegacyUserSkinWithoutComboFontFallsBackToOmsComboCounter()` 与 `TestLegacyUserSkinWithoutBarLineConfigFallsBackToOmsBarLine()` 回归；新增定向回归 **2/2** 通过，`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **73/73** 通过，`Build osu! (Debug)` 通过

### Phase 1.1：legacy 用户皮肤 judgement / hit explosion partial override 增量

- `ManiaLegacySkinTransformer` 现已为 `ManiaSkinComponents.HitExplosion` 补上基于实际 legacy explosion 资源存在性的 component-level 门控：当 legacy 用户皮肤缺失 `ExplosionImage` / `lightingN` 时，runtime 不再强行实例化 `LegacyHitExplosion`，而是返回 `null` 让 OMS fallback 继续接管
- judgement 缺失资源时回退 OMS 的语义此前已实际存在于 `SkinComponentLookup<HitResult>` 路径，但尚未被 regression 锁住；本轮已补上 key-only legacy 用户皮肤在缺失 judgement 资源时回退 `OmsManiaJudgementPiece` 的验证，并确认 legacy judgement piece 不会误接管
- `TestSceneOmsBuiltInSkin` 已新增 `TestLegacyUserSkinWithoutJudgementAssetsFallsBackToOmsJudgementPiece()` 与 `TestLegacyUserSkinWithoutHitExplosionAssetsFallsBackToOmsHitExplosion()` 回归；新增定向回归 **2/2** 通过，`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **71/71** 通过，`Build osu! (Debug)` 通过

## 2026-04-09

### Phase 1.1：legacy 用户皮肤 partial override 首个 note/hold 切口

- `ManiaLegacySkinTransformer` 现已不再因为 legacy 用户皮肤“只要有 key 贴图”就无条件返回 legacy note / hold 组件；`Note` / `HoldNoteHead` / `HoldNoteTail` / `HoldNoteBody` 的 legacy 路由现已改为按实际 legacy 资源是否存在决定，缺失资源时会返回 `null` 让 OMS fallback 继续接管
- 当 legacy 用户皮肤只提供 `mania-key*` 但未提供 note / hold 资源时，runtime 不再被 `LegacyNotePiece` / `LegacyBodyPiece` 强占：缺失 note 资产时现会回退 `OmsNotePiece`，缺失 hold-body 资产时现会回退 `OmsHoldNoteBodyPiece`，为后续 judgement / hitburst / HUD / bar-line 的 component-level partial override 铺好第一条真实桥接路径
- `TestSceneOmsBuiltInSkin` 已新增 `TestLegacyUserSkinWithoutNoteAssetsFallsBackToOmsNotePiece()` 与 `TestLegacyUserSkinWithoutHoldBodyAssetsFallsBackToOmsHoldBodyPiece()` 回归；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **69/69** 通过，`Build osu! (Debug)` 通过

### Phase 1.1：ruleset resources 与 OMS fallback 顺序收口

- `RulesetSkinProvidingContainer` 现已不再按 `TrianglesSkin` 硬编码定位 ruleset resources 的插入点，而是改为在最后一个受保护 built-in skin source 之前插入 `ResourceStoreBackedSkin`；当 gameplay lookup 链中同时存在用户皮肤与 OMS built-in fallback 时，ruleset resources 现会稳定落在两者之间
- `SkinManager.AllSources` 现已按 `SkinInfo.ID` 而不是对象引用判断“当前是否已经是 `OmsSkin`”；当前选择的 OMS 皮肤实例不再把 `DefaultOmsSkin` 作为重复 fallback 再挂一次，运行时 source chain 不再出现 `Oms -> ... -> Oms` 的重复 built-in 路径
- `TestSceneOmsBuiltInSkin` 已新增 `TestRulesetResourcesPrecedeOmsBuiltInFallback()` 与 `TestRulesetResourcesPrecedeOmsFallbackForLegacyUserSkin()` 回归；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **67/67** 通过，`Build osu! (Debug)` 通过

### Phase 1.1：OMS native-default removal 首个 runtime fallback 切口

- `RulesetSkinProvidingContainer` 现已把 beatmap legacy compatibility fallback 从 `DefaultClassicSkin` 切到 `DefaultOmsSkin`；当当前选择的是非 legacy 皮肤、且 beatmap skin 需要 legacy 资源兼容时，运行时内部回退链不再悄悄落回 upstream 默认皮肤
- `SkinManager.SetSkinFromConfiguration()` 的受保护 upstream built-in id 回退语义已补上回归：`Argon` / `Triangles` / `DefaultLegacy` / `Retro` 现都会统一回到 `OmsSkin`，不再通过配置入口重新暴露 upstream 默认皮肤作为产品默认选择
- `TestSceneOmsBuiltInSkin` 已新增 legacy beatmap compatibility fallback 与 protected upstream built-in id 的回归；后续同日又补上 ruleset resources / OMS fallback 顺序与 OMS built-in 去重回归；当前最新组合过滤为 **67/67** 通过，`Build osu! (Debug)` 通过

### Phase 1.1：OmsSkin mania note scrolling 显示状态收口

- `OmsNotePiece` 现已把 direction anchor / origin / scale 收口成显式 OMS display-state contract，不再继续依赖 legacy 风格的隐含 container origin 初始值；`OmsHoldNoteTailPiece` 仍通过 `GetDisplayDirection()` 承接 tail 的反向显示语义
- `TestSceneOmsBuiltInSkin` 已新增 normal note scrolling display-state 回归；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **60/60** 通过，`Build osu! (Debug)` 通过

### Phase 1.1：OmsSkin mania bar-line major/minor 运行时语义收口

- `OmsBarLine` 现已补上 OMS 自有的 major/minor runtime 语义，不再继续沿用 legacy bar line 对 `DrawableBarLine.Major` 无感知的单态表现；major 线保持 full-height / full-opacity，minor 线则会下调高度与亮度
- `TestSceneOmsBuiltInSkin` 已新增 bar line major→minor 切换回归，并把 dual-stage / mixed-stage shared-height 断言显式锁到 major 线；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **59/59** 通过，`Build osu! (Debug)` 通过

### Phase 1.1：OmsSkin mania combo counter HUD 运行时语义收口

- `OmsManiaComboCounter` 现已移除 legacy 风格的 rolling、combo break pop-out 与滚动归零动画链；运行时改为单文本即时同步，shared `ComboPosition` 继续仅作为 OMS 的 non-column HUD position contract 保留
- `TestSceneOmsBuiltInSkin` 已新增 combo counter 只保留单一 `OsuSpriteText` 节点、且 combo break 会立即清空显示的回归；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **58/58** 通过，`Build osu! (Debug)` 通过

### 1.12：BMS 密度星级首轮重标定

- `BmsDifficultyCalculator` 现已把密度星级从原先过于激进的平方根映射，改为更保守的对数映射；同一 keymode 的排序稳定性保持不变，但低密度到中密度谱面的星级会明显下修，避免当前实际显示整体系统性偏高
- `BmsDifficultyCalculator.Version` 已同步递增到 `20260409`，让现有缓存星级按后台重算流程失效并刷新；每个 keymode 的 reference density 常数暂时保持不变，后续仍可继续基于真实谱面样本做第二轮校准
- `BmsDifficultyCalculatorTest` 已按新映射更新基准断言；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsDifficultyCalculatorTest" -v minimal` **3/3** 通过，完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal` **463/463** 通过，`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### 1.3 / 1.4 / 1.16：BMS 谱面元数据补全与 Song Select 摘要扩展

- `BmsBeatmapDecoder` 现已新增 `#SUBARTIST` / `#COMMENT` 解析；`BmsBeatmapConverter` 会把 `Subtitle` / `SubArtist` / `Comment` / `PlayLevel` / `HeaderDifficulty` 写入 `BmsBeatmapMetadataData.ChartMetadata`，并在可判定时把谱师同步到 `metadata.Author.Username`
- `BmsNoteDistributionGraph` 右侧摘要现会在统计文字之外合并显示 chart creator、内部标级、副标题与难度表标签，Song Select 不再只能看到纯 note distribution 统计
- 当前最新完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal` **463/463** 通过

### Phase 1.1：OmsSkin mania combo counter 文本路径切离 legacy 字体

- `OmsManiaComboCounter` 现已不再使用 `LegacySpriteText` / `LegacyFont.Combo`，改为 OMS 自有数码文本实现；这一步把 combo 组件从 legacy 字体图集路径上切开，但仍保留现有 rolling / fade HUD 行为
- `TestSceneOmsBuiltInSkin` 已补上 combo counter 不再生成 `LegacySpriteText` 子树的回归断言；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **50/50** 通过

### Phase 1.1：OmsSkin mania hold-body 语义收口到 OMS preset

- 已新增 `OmsManiaHoldNoteBodyPreset`，并让 `ManiaOmsSkinTransformer` 为 `HoldNoteLightImage` / `HoldNoteLightScale` / `NoteBodyStyle` 返回 OMS 自有 hold-body preset；OMS preview 路径下的 hold-body 默认值不再继续依赖 legacy `skin.ini` 推导
- `OmsHoldNoteBodyPiece` 现已删除 legacy `NoteBodyStyle` 分支，固定使用 clamp/stretch 型 body 贴图语义；运行时缩放会随 scroll direction 在 `Vector2.One` 与 `new Vector2(1, -1)` 间切换，不再进入旧的 wrap-stretch 放大量级路径
- `TestSceneOmsBuiltInSkin` 已补上 hold-body semantic config 与运行时缩放回归；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **51/51** 通过

### Phase 1.1：OmsSkin mania note-height 语义收口到 OMS layout preset

- `OmsManiaLayoutPreset` 现已把 `WidthForNoteHeightScale` 纳入 stage-local preset；4K/7K 保留候选皮 `skin.ini` 的显式 note-height override，其余 stage 显式回落到各自最小列宽，而不是继续隐式依赖 legacy decoder fallback
- `OmsNotePiece` 现已改为按列读取该 lookup，mixed-stage 场景下第二 stage 的 note-height 不再复用第一 stage 或 total-columns legacy 默认值
- `TestSceneOmsBuiltInSkin` 已新增 single-stage / mixed-stage note-height config 回归与 mixed-stage 运行时 note-height 比例回归；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **54/54** 通过，`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania hold-tail 方向语义收口到 OMS display hook

- `OmsNotePiece` 现已抽出正式的 `GetDisplayDirection()` 钩子，`OmsHoldNoteTailPiece` 通过该钩子承接 tail 的反向显示语义，不再继续伪造反向 `ValueChangedEvent<ScrollingDirection>`；这一步把 hold-tail 的方向处理从 legacy 风格事件翻转，收口为 OMS 自身的显示语义钩子
- `TestSceneOmsBuiltInSkin` 已新增 hold-tail inverted scrolling-direction 场景回归，锁定默认下滚与上下切换时的 anchor / scale 行为；定向 mania 回归现为 `55/55` 通过
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **55/55** 通过，`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania hold-body 运行时表现切离 legacy light / fade

- `OmsHoldNoteBodyPiece` 现已移除 legacy 风格的 hold-hit light 与 miss dark-gray fade 运行时链路：不再向 column 顶层插入额外 `HitTargetInsetContainer`，也不再因 body miss 把 head / tail / body 一起染暗；body 运行时仅保留 OMS stretch 贴图与 scrolling-direction 对应的 anchor / scale 行为
- `ManiaOmsSkinTransformer` 仍为 `NoteBodyStyle` / `HoldNoteLightImage` / `HoldNoteLightScale` 返回 OMS preset，以维持既有 config lookup 桥；但 `OmsHoldNoteBodyPiece` 自身不再消费 legacy 风格的 light / fade 表现
- `TestSceneOmsBuiltInSkin` 已新增 forced holding 不再插入 light container、forced body miss 不再触发 miss fade 的场景回归；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **57/57** 通过，`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### 文档同步：运行时存储与后续拓扑结论收口

- `README.md`、`RELEASE.md`、`DEVELOPMENT_PLAN.md`、`DEVELOPMENT_STATUS.md` 与 `OMS_COPILOT.md` 现已统一写明当前默认 AppData 数据根、`storage.ini` 单自定义数据根能力，以及“若后续进入存储改造，优先多目录外部谱库，不先做 mania sibling dir / 默认单包模式”的规划结论

### 1.17：Windows 默认 HID backend 切到 DirectInput

- `oms.Input` 已新增 `Devices/OmsWindowsDirectInput`，并引入 `Vortice.DirectInput`；Windows 下的 `OmsHidDeviceHandler.CreateDefaultDeviceProvider()` 与 `OmsHidDeviceDiscovery` 现默认走 DirectInput 枚举/轮询路径，避免再次触发 `HidSharp.DeviceList.Local` 的 `RegisterClass failed` 进程级崩溃
- `OmsHidDeviceCaptureSession` 与 `OmsHidDeviceHandler` 现会在目标设备缺席时主动重刷设备列表，不再依赖 provider 侧热插拔事件；DirectInput 标识符也会尽量保留 `hid:vid_xxxx&pid_xxxx` 形态，仅在无法提取 VID/PID 时退回 `dinput:instance_{guid}`
- `HidSharp` 仍保留为 Windows 上的诊断后端，仅在显式设置 `OMS_ENABLE_HIDSHARP=1` 时才会被触发；非 Windows 路径继续沿用原有 `HidSharp` provider
- `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter "FullyQualifiedName~OmsHidDeviceHandlerTest"` **14/14** 通过；较早同日完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **458/458** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过（当前同日最新完整回归见上方 **463/463** 条目）

### 1.17：Windows HidSharp 后端默认改为诊断开关，启动与设置不再因 HID 初始化闪退

- `OmsHidSharpRuntime` 现已集中接管 Windows HID backend gate；由于 `HidSharp.DeviceList.Local` 可能以 `RegisterClass failed` 在内部线程直接终止进程，Windows 构建默认不再触发 HidSharp，仅在显式设置 `OMS_ENABLE_HIDSHARP=1` 时才继续初始化该后端
- `OmsHidDeviceHandler` / `OmsHidDeviceDiscovery` 与设置页 supplemental editor 现都走同一层 gate；点击设置时看到的 HID-disabled 提示属于预期防崩溃降级，说明问题已收敛到待修复的 Windows HID 设备加载后端，而不是设置面板或皮肤系统本身挂死
- 当前防崩溃策略下，键盘 / Raw Input / XInput / MouseAxis 主链保持可用，Release 启动已不再因 HidSharp 即时闪退；Windows HID backend 稳定化仍留在 1.17 输入专项后续工作中
- `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsHidDeviceHandlerTest"` **11/11** 通过；`dotnet build .\osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过，并完成一次 Release 启动 smoke 验证未见即时崩溃

### Phase 1.1：OmsSkin mania hold-body 升格为实际 OMS-owned 实现

- `OmsHoldNoteBodyPiece` 现已改为真实 `OmsManiaColumnElement` 路径下的实际实现，不再继承 `LegacyBodyPiece`；当前继续复用 `HoldNoteBodyImage` preset、legacy `NoteBodyStyle` / `HoldNoteLightImage` / `HoldNoteLightScale`，以及 hold-body light / fade / wrap-stretch 语义
- `OmsOwnedSkinComponentContractTest` 现已扩到 note / hold-head / hold-tail / hold-body / judgement / hit explosion / combo counter / bar line 八类组件，并由 `TestSceneOmsBuiltInSkin` 补上 hold-body scrolling-direction 行为回归
- 这一步代表 mania 第二批里的 hold-body 也已从“显式组件入口”推进到“实际 OMS-owned component implementation”；当前剩余重点进一步收窄为 note/hold / combo/HUD / bar-line 仍在消费的 legacy-derived 语义清理
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin"` **50/50** 通过

### Phase 1.1：OmsSkin mania note / hold-head / hold-tail 升格为实际 OMS-owned 实现

- `OmsNotePiece` 现已改为真实 `OmsManiaColumnElement` 派生实现，不再继承 `LegacyNotePiece`；当前继续复用 `NoteImage` preset、`WidthForNoteHeightScale` 与 legacy note scrolling / sizing 语义
- `OmsHoldNoteHeadPiece` / `OmsHoldNoteTailPiece` 现已分别改为真实 `OmsNotePiece` 派生实现，不再继承 `LegacyHoldNoteHeadPiece` / `LegacyHoldNoteTailPiece`；当前继续复用 `HoldNoteHeadImage` / `HoldNoteTailImage` preset 与 legacy tail inversion / fallback 语义
- `OmsOwnedSkinComponentContractTest` 现已扩到 note / hold-head / hold-tail / judgement / hit explosion / combo counter / bar line 七类组件，持续锁定它们不再回退到 legacy implementation
- 这一步代表 mania 第二批里的 note / hold-head / hold-tail 也已从“显式组件入口”推进到“实际 OMS-owned component implementation”；当前剩余重点进一步收窄为 hold-body 与 note/hold / combo/HUD / bar-line 仍在消费的 legacy-derived 语义清理
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin"` **48/48** 通过

## 2026-04-08

### Phase 1.1：OmsSkin mania combo 与 bar-line 升格为实际 OMS-owned 实现

- `OmsManiaComboCounter` 现已改为真实 `CompositeDrawable, ISerialisableDrawable` 实现，不再继承 `LegacyManiaComboCounter`；当前继续复用 `ComboPosition` shared HUD preset、combo break colour、legacy combo font 与 rolling/fade animation 语义
- `OmsBarLine` 现已改为真实 `CompositeDrawable` 实现，不再继承 `LegacyBarLine`；当前继续复用 `BarLineHeight` / `BarLineColour` shared bar-line config 与既有 box / edge-smoothness 语义
- `OmsOwnedSkinComponentContractTest` 现已扩到 judgement / hit explosion / combo counter / bar line 四类组件，持续锁定它们不再回退到 legacy implementation
- 这一步代表 mania 第二批里的 combo counter 与 bar-line 也已从“显式组件入口”推进到“实际 OMS-owned component implementation”；当前剩余重点收窄为 note / hold 的余下默认路径迁移，以及 combo/HUD / bar-line 仍在消费的 legacy-derived 语义清理
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin"` **45/45** 通过

### Phase 1.1：OmsSkin mania judgement 与 hitburst 升格为实际 OMS-owned 实现

- `OmsManiaJudgementPiece` 现已改为真实 `CompositeDrawable, IAnimatableJudgement` 实现，不再继承 `LegacyManiaJudgementPiece`；当前继续复用 `Hit300g` / `Hit300` / `Hit200` / `Hit100` / `Hit50` / `Hit0` judgement asset preset 与 legacy-derived judgement positioning/animation 语义
- `OmsHitExplosion` 现已改为真实 `LegacyManiaColumnElement, IHitExplosion` 实现，不再继承 `LegacyHitExplosion`；当前继续复用 `ExplosionImage` / `ExplosionScale` hitburst config preset、scroll direction anchor 与既有 fade/animation 语义
- 新增 `OmsOwnedSkinComponentContractTest`，锁定 judgement / hit explosion 持续满足 `IAnimatableJudgement` / `IHitExplosion` 契约，且不再回退到 legacy implementation
- 这一步代表 mania 第二批里的 judgement / hitburst 已从“组件入口收口”推进到“实际 OMS-owned component implementation”；当前剩余重点转向 combo/HUD 与 note/hold/bar-line 的余下默认路径迁移，score-driven results 是否需要独立 preview/skinnable target 暂留后续评估
- 当次里程碑对应的 `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin"` 为 **43/43** 通过；当前最新同类定向回归结果见上方 hold-body 条目中的 **50/50**

### Phase 1.1：shared results panel shell 升格为 core stateful contract

- `osu.Game` 现已新增共享 `DefaultResultsPanelDisplay<TState>`，把 results-style panel 的 title / status / accent / shell 状态管理从单纯 `DefaultResultsPanelContainer` 抬升为 core 级 stateful contract
- `DefaultBmsResultsSummaryPanelDisplay` / `DefaultBmsGaugeHistoryPanelDisplay` / `DefaultBmsNoteDistributionPanelDisplay` 现都改走该基类，不再各自重复维护 shell 配色、空态文本与内容显隐；同时新增 `ResultsPanelDisplayContractTest` 锁定三者继续走同一 contract
- 这一步代表 1.1.4 里的 shared results panel shell 已不再只是可复用容器，而是已收口为真实的 core results panel 语义；当前剩余 gap 收窄为决定 score-driven results 是否需要独立的 preview/skinnable target，以及 mania 第二批余下的实际 OMS-owned Hold / HitBurst / Judgement / HUD 默认路径迁移
- `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~ResultsPanelDisplayContractTest|FullyQualifiedName~StatisticItemContainerTest|FullyQualifiedName~BmsRulesetStatisticsTest|FullyQualifiedName~BmsSkinTransformerTest"` **69/69** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **446/446** 通过；`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：results-style shared panel shell 首次落地

- `osu.Game` 现已新增共享 `DefaultResultsPanelContainer`，`DefaultBmsResultsSummaryPanelDisplay` / `DefaultBmsGaugeHistoryPanelDisplay` / `DefaultBmsNoteDistributionPanelDisplay` 不再各自复制同一套 rounded results panel 结构，而是统一复用这层 shared shell
- `StatisticItemContainer` 现对空标题 `StatisticItem` 跳过通用灰色 wrapper，BMS results summary / gauge history 这类 panel-owned title 与 panel-owned shell 不再被再包一层 generic shell；同时新增 `StatisticItemContainerTest` 锁定“有标题保留 generic shell、空标题移除 generic shell”的结构回归
- 这一步代表 1.1.4 里的 results summary 容器已从“纯待办”推进到“已有 shared panel shell 首落”；当前剩余 gap 收窄为将这层 shared shell 继续抬升为真正的 global results summary container 语义，以及 mania 第二批余下的实际 OMS-owned Hold / HitBurst / Judgement / HUD 默认路径迁移
- `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~StatisticItemContainerTest|FullyQualifiedName~BmsRulesetStatisticsTest|FullyQualifiedName~BmsSkinTransformerTest"` **66/66** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **443/443** 通过；`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin global layout metadata 首次收口

- `TestSceneOmsBuiltInSkin` 已新增对 `OmsSkin` 内置 `MainHUDComponents.json` / `SongSelect.json` / `Playfield.json` 的回归，锁定 `Skin.LayoutInfos` 现会稳定装载三类 global target，且 mania playfield 段包含 `BarHitErrorMeter` / `ArgonAccuracyCounter` / `ArgonComboCounter` / `ArgonPerformancePointsCounter` / `ClicksPerSecondCounter`
- 这一步代表 1.1.4 里的 `Global` layout metadata 已从“资源已嵌入但未锁定”推进到“有定向 regression 约束”；当时该小节剩余 gap 收窄为 global results summary container，以及 mania 第二批余下的实际 OMS-owned Hold / HitBurst / Judgement / HUD 默认路径迁移
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **41/41** 通过；`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### 构建审计：AutoMapper GHSA 定点抑制

- `osu.Game.csproj` 已新增针对 `https://github.com/advisories/GHSA-rvv3-g6hj-g44x` 的定点 `NuGetAuditSuppress`，不再让已评估的 `AutoMapper` `NU1903` 审计告警持续污染当前 build 输出
- 当前仓库仍保留 `RealmObjectExtensions` 里的 `MaxDepth(3)` 作为循环图路径的运行时限深；之所以不直接升到 `AutoMapper` 15.1.1+，是因为 15.x 额外引入 license 要求与配置 API 破坏变更，适合作为单独迁移切片处理
- 这一步代表构建输出已不再残留既有 `NU1903` 噪音，但 `AutoMapper` 升级或彻底移除仍继续留在中优先级跟踪项中
- `dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过，当前无残留构建告警

### Phase 1.1：OmsSkin global shared transformer shell 首次落地

- 已新增共享 `OmsSkinTransformer`，作为 OMS preview 路径的外层 wrapper；当 `GlobalSkinnableContainerLookup` 的 global HUD、`SongSelect`、`Playfield` 未被更具体的 ruleset transformer 命中时，现会返回空 `DefaultSkinComponentsContainer` 作为 shared shell
- `ManiaRuleset` 与 `BmsRuleset` 的 `OmsSkin` 入口现都会先经过该 shared shell，再分别委托 `ManiaOmsSkinTransformer` 与 `BmsSkinTransformer`，因此 ruleset-specific HUD / gameplay lookup 仍继续由各自 transformer 承接
- `TestSceneOmsBuiltInSkin` 已收紧为外层 `OmsSkinTransformer` + 内层 `ManiaOmsSkinTransformer` 组合，并新增 global HUD / `SongSelect` / `Playfield` shell 断言；`BmsSkinTransformerTest` 也已新增 Oms shared shell 回归，锁定外层 `OmsSkinTransformer` + 内层 `BmsSkinTransformer` 组合
- 这一步代表 Global shared shell / shared transformer shell 已完成首轮落地；当时尚未被 regression 锁定的 global layout metadata 现已在同日后续步骤补齐，当时剩余主线收窄为 global results summary container 与 mania 第二批余下的实际 OMS-owned Hold / HitBurst / Judgement / HUD 默认路径迁移
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **40/40** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **62/62** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **441/441** 通过；`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania mixed-stage shared transformer 首次收口

- `ManiaOmsSkinTransformer` 的 non-column shared preset lookup 现已在 mixed-stage beatmap 上固定复用第一 stage 的 OMS preset，不再落回 total-columns legacy 默认值
- `TestSceneOmsBuiltInSkin` 已新增 mixed-stage judgement / HUD / bar-line 配置与运行时回归，覆盖 `ScorePosition` / `ComboPosition` / `BarLineHeight` / `BarLineColour` 以及 `DrawableManiaJudgement` / `OmsManiaComboCounter` / `DrawableBarLine` 路径；同项定向 mania 回归现为 **40/40** 通过
- 这一步代表当前已落地的 mania non-column shared config families 已完成 mixed-stage shared-transformer 收口；但组件仍继续消费 legacy judgement / combo / bar-line 语义，Global shared shell / shared transformer shell 与 mania 第二批余下的实际 OMS-owned Hold / HitBurst / Judgement / HUD 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **40/40** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania hold note body component route 首次落地

- `ManiaSkinComponents.HoldNoteBody` 现已显式路由到 `OmsHoldNoteBodyPiece`；可见 hold body 会由 `DrawableHoldNote` 内部的 `bodyPiece` 路径实际加载 OMS hold note body 组件，而不再只停留在纯 legacy body dispatch
- `TestSceneOmsBuiltInSkin` 已补 single-stage runtime hold note body load 回归，并在 transformer 断言中收紧到 `OmsHoldNoteBodyPiece`；同项定向 mania 回归现为 **37/37** 通过
- 这一步只代表 mania 第二批又落下首个 explicit hold-note-body component slice；`OmsHoldNoteBodyPiece` 仍继续消费既有 `HoldNoteBodyImage` preset、legacy `NoteBodyStyle` / `HoldNoteLightImage` / `HoldNoteLightScale`，以及 legacy hold-body light insertion / wrap-stretch / hold-break fade 语义，所以 shared shell / shared transformer，以及 mania 第二批余下的实际 Hold / HitBurst / Judgement / HUD 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **37/37** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania hold note tail component route 首次落地

- `ManiaSkinComponents.HoldNoteTail` 现已显式路由到 `OmsHoldNoteTailPiece`；`DrawableHoldNoteTail` 在 OMS preview 路径下会实际加载 OMS hold note tail 组件，而不再继续走纯 `LegacyHoldNoteTailPiece` dispatch
- `TestSceneOmsBuiltInSkin` 已补 single-stage runtime hold note tail load 回归，并在 transformer 断言中收紧到 `OmsHoldNoteTailPiece`；同项定向 mania 回归现为 **36/36** 通过
- 这一步只代表 mania 第二批又落下首个 explicit hold-note-tail component slice；`OmsHoldNoteTailPiece` 仍继续消费既有 `HoldNoteTailImage` preset 与 legacy tail inversion / note sizing 语义，所以 shared shell / shared transformer，以及 mania 第二批余下的实际 Hold body / HitBurst / Judgement / HUD 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **36/36** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania hold note head component route 首次落地

- `ManiaSkinComponents.HoldNoteHead` 现已显式路由到 `OmsHoldNoteHeadPiece`；`DrawableHoldNoteHead` 在 OMS preview 路径下会实际加载 OMS hold note head 组件，而不再继续走纯 `LegacyHoldNoteHeadPiece` dispatch
- `TestSceneOmsBuiltInSkin` 已补 single-stage runtime hold note head load 回归，并在 transformer 断言中收紧到 `OmsHoldNoteHeadPiece`；同项定向 mania 回归现为 **35/35** 通过
- 这一步只代表 mania 第二批又落下首个 explicit hold-note-head component slice；`OmsHoldNoteHeadPiece` 仍继续消费既有 `HoldNoteHeadImage` preset 与 legacy note scrolling / sizing 语义，所以 shared shell / shared transformer，以及 mania 第二批余下的实际 Hold tail/body / HitBurst / Judgement / HUD 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **35/35** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania note component route 首次落地

- `ManiaSkinComponents.Note` 现已显式路由到 `OmsNotePiece`；`DrawableNote` 在 OMS preview 路径下会实际加载 OMS note 组件，而不再继续走纯 `LegacyNotePiece` dispatch
- `TestSceneOmsBuiltInSkin` 已补 single-stage runtime note load 回归，并在 transformer 断言中收紧到 `OmsNotePiece`；同项定向 mania 回归现为 **34/34** 通过
- 这一步只代表 mania 第二批又落下首个 explicit normal-note component slice；`OmsNotePiece` 仍继续消费既有 `NoteImage` preset、`WidthForNoteHeightScale` 与 legacy note scrolling / sizing 语义，所以 shared shell / shared transformer，以及 mania 第二批余下的实际 Hold / HitBurst / Judgement / HUD 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **34/34** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania combo counter component route 首次落地

- MainHUDComponents 里的 combo 现已显式路由到 `OmsManiaComboCounter`；OMS preview 的 HUD container 会实际加载 OMS combo counter 组件，而不再继续走纯 `LegacyManiaComboCounter` dispatch
- `TestSceneOmsBuiltInSkin` 已补 single-stage runtime combo counter load 回归，并把既有 dual-stage HUD combo 回归收紧到 `OmsManiaComboCounter` 实例；同项定向 mania 回归现为 **33/33** 通过
- 这一步只代表 mania 第二批又落下首个 explicit combo counter component slice；`OmsManiaComboCounter` 仍继续消费既有 `ComboPosition` shared preset 与 legacy combo counter 语义，所以 shared shell / shared transformer，以及 mania 第二批余下的实际 Note / Hold / HitBurst / Judgement / HUD 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **33/33** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania bar-line component route 首次落地

- `ManiaSkinComponents.BarLine` 现已显式路由到 `OmsBarLine`；`DrawableBarLine` 在 OMS preview 路径下会实际加载 OMS bar line 组件，而不再继续走纯 legacy component dispatch
- `TestSceneOmsBuiltInSkin` 已补 single-stage runtime bar line load 回归，并把既有 dual-stage bar line 回归收紧到 `OmsBarLine` 实例；同项定向 mania 回归现为 **32/32** 通过
- 这一步只代表 mania 第二批又落下首个 explicit bar-line component slice；`OmsBarLine` 仍继续消费既有 `BarLineHeight` / `BarLineColour` shared preset 与 legacy bar-line 语义，所以 shared shell / shared transformer，以及 mania 第二批余下的实际 Note / Hold / HitBurst / Judgement / HUD / bar line 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **32/32** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania shared bar-line config 首次落地

- `OmsManiaBarLinePreset` 现已同时承接 `LegacyManiaSkinConfigurationLookups.BarLineHeight` / `BarLineColour` 的 uniform-stage shared lookup；single-stage 与 same-keycount dual-stage 不再落回 total-columns legacy 默认值
- `TestSceneOmsBuiltInSkin` 已补 shared bar-line config 回归与 dual-stage runtime bar line 回归，验证 `LegacyBarLine` 在 OMS preview 的 9K+9K 路径下会实际复用同一组 OMS bar-line height；同项定向 mania 回归现为 **31/31** 通过
- 这一步只代表 mania 第二批又落下首个 uniform-stage shared bar-line config slice；mixed-stage 的 non-column config、shared transformer 收口与实际 OMS-owned bar line 组件路径仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **31/31** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania shared judgement combo-position 首次落地

- `OmsManiaJudgementPositionPreset` 现已同时承接 `LegacyManiaSkinConfigurationLookups.ScorePosition` / `ComboPosition` 的 uniform-stage shared lookup；5K+5K dual-stage 不再落回 total-columns legacy 默认值
- `TestSceneOmsBuiltInSkin` 已补 shared judgement / HUD position config 回归与 dual-stage HUD combo 回归，验证 `LegacyManiaComboCounter` 在 OMS preview 的 MainHUDComponents 路径下会实际复用同一组 OMS combo-position preset；同项定向 mania 回归现为 **29/29** 通过
- 这一步只代表 mania 第二批把 uniform-stage shared judgement-position slice 从 score-position 扩展到 combo-position；mixed-stage 的 non-column judgement / HUD positioning、shared transformer 收口与 legacy judgement animation 语义迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **29/29** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania shared judgement score-position 首次落地

- 已新增 `OmsManiaJudgementPositionPreset`，并让 `ManiaOmsSkinTransformer.GetConfig()` 为 `LegacyManiaSkinConfigurationLookups.ScorePosition` 在 uniform-stage beatmap 上返回显式 OMS shared score-position；5K+5K dual-stage 不再落回 total-columns legacy 默认值
- `TestSceneOmsBuiltInSkin` 已补 dual-stage judgement 位置回归，验证 `DrawableManiaJudgement` 在 OMS preview 的 5K+5K 路径下会实际复用同一组 OMS score-position preset；同项定向 mania 回归现为 **27/27** 通过
- 这一步只代表 mania 第二批又落下首个 shared judgement score-position slice；mixed-stage 的 non-column judgement / HUD positioning、shared transformer 收口与 legacy judgement animation 语义迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **27/27** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania judgement piece route 首次落地

- 已新增 `OmsManiaJudgementPiece`，并让 `ManiaOmsSkinTransformer.GetDrawableComponent()` 为 `SkinComponentLookup<HitResult>` 返回显式 OMS judgement piece；`DrawableManiaJudgement` 不再只通过 base legacy transformer 隐式拿到 judgement drawable
- `TestSceneOmsBuiltInSkin` 已补显式 judgement piece 路由断言与实际加载回归，验证 transformer 会返回 `OmsManiaJudgementPiece`，且 `DrawableManiaJudgement` 会在 OMS preview 路径下实际加载该组件；同项定向 mania 回归现为 **26/26** 通过
- 这一步只代表 mania 第二批又落下首个 explicit judgement piece slice；当前 `OmsManiaJudgementPiece` 仍继续消费既有 `Hit300g` / `Hit300` / `Hit200` / `Hit100` / `Hit50` / `Hit0` preset 与 legacy judgement positioning/animation 语义，所以 shared shell / shared transformer，以及 mania 第二批余下的实际 HitBurst / Judgement / HUD / note-hold 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **26/26** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania hit explosion component route 首次落地

- 已新增 `OmsHitExplosion`，并让 `ManiaOmsSkinTransformer.GetDrawableComponent()` 为 `ManiaSkinComponents.HitExplosion` 返回显式 OMS 组件；`PoolableHitExplosion` 不再只通过 base legacy transformer 隐式拿到 hit explosion drawable
- `TestSceneOmsBuiltInSkin` 已补显式 hit explosion 组件路由断言与实际加载回归，验证 transformer 会返回 `OmsHitExplosion`，且 `PoolableHitExplosion` 会在 OMS preview 路径下实际加载该组件；同项定向 mania 回归现为 **25/25** 通过
- 这一步只代表 mania 第二批又落下首个 explicit hitburst component slice；当前 `OmsHitExplosion` 仍继续消费上一轮已收口的 `ExplosionImage` / `ExplosionScale` preset，所以 shared shell / shared transformer，以及 mania 第二批余下的实际 HitBurst / Judgement / HUD / note-hold 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **25/25** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania hitburst config preset 首次落地

- 已新增 `OmsManiaHitExplosionPreset`，并让 `ManiaOmsSkinTransformer.GetConfig()` 为 `ExplosionImage` / `ExplosionScale` 返回 OMS-owned stage-local hitburst config preset；legacy `LegacyHitExplosion` 的这批 hitburst 配置 lookup 不再继续只依赖 absolute-column fallback 与隐式列宽换算
- `TestSceneOmsBuiltInSkin` 已补 hitburst config 回归与 mixed-stage 5K+8K hitburst config 回归，验证 transformer 会稳定返回 OMS-owned 的 `ExplosionImage` / `ExplosionScale` 配置，且 mixed-stage beatmap 会按各自 stage keycount 取独立 hitburst config preset；同项定向 mania 回归现为 **24/24** 通过
- 这一步只代表 mania 第二批又落下首个 stage-local hitburst config slice；shared shell / shared transformer，以及 mania 第二批余下的实际 HitBurst / Judgement / HUD / note-hold 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **24/24** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania judgement asset preset 首次落地

- 已新增 `OmsManiaJudgementAssetPreset`，并让 `ManiaOmsSkinTransformer.GetConfig()` 为 `Hit300g` / `Hit300` / `Hit200` / `Hit100` / `Hit50` / `Hit0` 返回 OMS-owned shared judgement asset preset；legacy `ManiaLegacySkinTransformer` 的这批 judgement 资源 lookup 不再继续只依赖默认文件名 fallback
- `TestSceneOmsBuiltInSkin` 已补 judgement asset 回归与 mixed-stage 5K+9K shared judgement asset 回归，验证 transformer 会稳定返回 OMS-owned 的 judgement 资源名，且 mixed-stage beatmap 会持续拿到同一组 shared judgement asset preset；同项定向 mania 回归现为 **22/22** 通过
- 这一步代表 mania 第二批也已落下首个 shared judgement asset slice；shared shell / shared transformer，以及 mania 第二批余下的 HitBurst / HUD 与实际 OMS-owned judgement / note-hold 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **22/22** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania note/hold asset preset 首次落地

- 已新增 `OmsManiaNoteAssetPreset`，并让 `ManiaOmsSkinTransformer.GetConfig()` 为 `NoteImage` / `HoldNoteHeadImage` / `HoldNoteTailImage` / `HoldNoteBodyImage` 返回 OMS-owned stage-local note/hold asset preset；legacy `LegacyNotePiece` / `LegacyHoldNoteHeadPiece` / `LegacyHoldNoteTailPiece` / `LegacyBodyPiece` 的这批 note/hold asset lookup 不再继续只依赖 absolute-column legacy config fallback
- `TestSceneOmsBuiltInSkin` 已补 note/hold asset 回归与 mixed-stage 5K+9K note/hold asset 回归，验证 transformer 会稳定返回 OMS-owned 的 note/head/tail/body 配置，且 mixed-stage beatmap 会按各自 stage keycount 取独立 note/hold asset preset；同项定向 mania 回归现为 **20/20** 通过
- 这一步只代表 mania 第二批的首个 stage-local note/hold asset slice 已落下一刀；shared shell / shared transformer，以及 mania 第二批余下的 HitBurst / Judgement / HUD 与实际 OMS-owned note/hold 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **20/20** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania key-asset preset 首次落地

- 已新增 `OmsManiaKeyAssetPreset`，并让 `ManiaOmsSkinTransformer.GetConfig()` 为 `KeyImage` / `KeyImageDown` 返回 OMS-owned stage-local key asset preset；`OmsKeyArea` 的这批 key-image lookup 不再继续直接依赖 legacy mania config fallback
- `TestSceneOmsBuiltInSkin` 已补 key-image 回归与 mixed-stage 5K+8K key-image 回归，验证 transformer 会稳定返回 OMS-owned 的 key-image 配置，且 mixed-stage beatmap 会按各自 stage keycount 取独立 key-image preset；同项定向 mania 回归现为 **18/18** 通过
- 这一步代表 mania 第一批 shell 的 key-image lookup 也已落下一刀；shared shell / shared transformer，以及 mania Note / Hold / HUD 迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **18/18** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania shell colour preset 首次落地

- 已新增 `OmsManiaColumnColourPreset`，并让 `ManiaOmsSkinTransformer.GetConfig()` 为 `ColumnLineColour` / `JudgementLineColour` / `ColumnBackgroundColour` / `ColumnLightColour` 返回 OMS-owned shell colour preset；`OmsColumnBackground` / `OmsHitTarget` 的这批 colour lookup 不再继续直接依赖 legacy mania config fallback
- `TestSceneOmsBuiltInSkin` 已补 shell colour 回归与 mixed-stage 8K+9K colour 回归，验证 transformer 会稳定返回 OMS-owned 的 shell colour 配置，且 mixed-stage beatmap 会按各自 stage keycount 取独立 colour preset；同项定向 mania 回归现为 **16/16** 通过
- 这一步只代表 mania 第一批 shell 的首批 colour lookup 也已落下一刀；shared shell / shared transformer、剩余 shell key-image lookup，以及 mania Note / Hold / HUD 迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **16/16** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania shared shell asset preset 首次落地

- 已新增 `OmsManiaShellAssetPreset`，并让 `ManiaOmsSkinTransformer.GetConfig()` 为 `LeftStageImage` / `RightStageImage` / `BottomStageImage` / `HitTargetImage` / `LightImage` / `KeysUnderNotes` 返回 OMS-owned shared shell asset preset；`OmsStageBackground` / `OmsStageForeground` / `OmsHitTarget` / `OmsKeyArea` 的这批共享 lookup 不再继续依赖 legacy mania config fallback
- `TestSceneOmsBuiltInSkin` 已补 shared shell asset 回归与 mixed-stage 7K+6K 共享 asset 回归，验证 transformer 会稳定返回 OMS-owned 的 stage / hit target / light 资源名，且 mixed-stage beatmap 仍能拿到同一组共享 asset preset；同项定向 mania 回归现为 **14/14** 通过
- 这一步只代表 mania 第一批 shell 的 shared asset lookup 也已落下一刀；shared shell / shared transformer、剩余 shell key-image/color lookup，以及 mania Note / Hold / HUD 迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **14/14** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

## 2026-04-07

### Phase 1.1：OmsSkin mania stage-local shell behaviour preset 首次落地

- 已新增 `OmsManiaShellPreset`，并让 `ManiaOmsSkinTransformer.GetConfig()` 为 `LeftLineWidth` / `RightLineWidth` / `LightPosition` / `ShowJudgementLine` / `LightFramePerSecond` 提供首批 stage-local OMS shell behaviour preset；`OmsHitTarget` 对 `ShowJudgementLine` / `LightFramePerSecond` 也已改为按列请求，使 mixed-stage beatmap 会按各 stage keycount 取值
- `TestSceneOmsBuiltInSkin` 已补 shell config 回归、mixed-stage 7K+6K 回归与 8K edge line width 回归，验证 transformer 会返回预期 behaviour 值，且第二个 stage 会按自身 keycount 使用独立 light position；同项定向 mania 回归现为 **12/12** 通过
- 这一步只代表 mania 第一批 shell 的 stage-local behaviour bridge 也已落下第一刀；shared shell / shared transformer、剩余 shell asset/color lookup，以及 mania Note / Hold / HUD 迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **12/12** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania stage-local layout preset 首次落地

- 已新增 `OmsManiaLayoutPreset`，并让 `ManiaOmsSkinTransformer.GetConfig()` 为 `HitPosition` / `StagePadding` / `ColumnWidth` / `ColumnSpacing` 提供首批 stage-local OMS layout preset，不再按 `beatmap.TotalColumns` 整体取值，也不再只依赖 legacy mania config fallback
- `TestSceneOmsBuiltInSkin` 已补 layout 回归、完整 5K `Stage` 宿主回归，以及 dual-stage 5K+5K 回归，验证 transformer 会返回预期 layout 值，完整 `Stage` 会实际使用这些 preset，且第二个 stage 会按自身 keycount 重复使用同一组 preset；同项定向 mania 回归现为 **9/9** 通过
- 这一步只代表 mania 第一批的容器级 layout bridge 已落下第一刀；remaining shell config lookup、shared shell / shared transformer，以及 mania Note / Hold / HUD 迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **9/9** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania shell 首批 OMS 组件落地

- `ManiaOmsSkinTransformer` 现会为 `StageBackground` / `StageForeground` / `ColumnBackground` / `KeyArea` / `HitTarget` 返回 `OmsStageBackground` / `OmsStageForeground` / `OmsColumnBackground` / `OmsKeyArea` / `OmsHitTarget`，不再继续直接复用 legacy shell 组件
- `TestSceneOmsBuiltInSkin` 已补 runtime load 回归，除组件类型断言外，还验证上述 OMS shell 组件可在最小依赖宿主下实际加载；该阶段首次落地时定向 mania 回归为 **5/5** 通过
- 这一刀只代表 mania 第一批已落下首个 OMS-owned shell component slice；`Stage` / `Column` / `ColumnFlow` / `ColumnHitObjectArea` 容器级收口、shared shell / shared transformer，以及 mania Note / Hold / HUD 迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **5/5** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania 显式 transformer 入口首次落地

- `ManiaRuleset.CreateSkinTransformer()` 现会为 `OmsSkin` 显式返回 `ManiaOmsSkinTransformer`，不再继续把 OMS built-in preview 入口隐式混在 generic `LegacySkin` catch-all 分支里
- 当前已验证 `StageBackground` / `ColumnBackground` / `KeyArea` / `HitTarget` 候选路径可经该 OMS mania 入口提供；这代表 mania 第一批已从“未开始”推进到“显式入口已接通”，但整体仍主要复用 legacy-derived candidate assets 与配置语义，尚未完成 OMS-owned 默认层收口
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **4/4** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OMS built-in skin host / provider 骨架首次落地

- 已新增受保护的 `OmsSkin` preview 选择项，并把 `SKIN/SimpleTou-Lazer` 候选包资源嵌入 `osu.Game`，作为 OMS built-in host / provider 的首个内置 resource root
- `SkinManager` 现会注册、枚举并允许通过配置切换到该 OMS 入口；`SkinnableSprite` 也已把 `OmsSkin` 视作可用的 built-in 候选来源之一
- 新增 `TestSceneOmsBuiltInSkin` 回归，验证 OMS 入口可选取、受保护，并能从内置资源根取到 mania stage / key 纹理；同日 `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **3/3** 通过，`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过
- 这一步只代表默认皮肤包 host / provider / resource root 已起骨架；shared shell / shared transformer 与 mania OMS-owned 默认路径仍待后续推进

### Phase 1.1：BMS note / hold 默认层第七个 OMS-owned slice 落地

- 已新增 `DefaultBmsNoteDisplay` / `DefaultBmsLongNoteHeadDisplay` / `DefaultBmsLongNoteBodyDisplay` / `DefaultBmsLongNoteTailDisplay`，并把 `Note` / `LongNoteHead` / `LongNoteBody` / `LongNoteTail` 的无皮肤默认路径切到 `BmsDefaultPlayfieldPalette`
- `BmsSkinTransformer` 不再用 `BmsTemporarySkinPalette` 生成 note / hold fallback；`BmsSkinTransformerTest` 同步补上 normal / scratch note、head / body / tail 的 concrete fallback 与 wrapped-skin 回归，`BmsTemporarySkinPalette` 也已从 live BMS fallback 链路删除
- 直接受影响 `BmsSkinTransformerTest` **61/61** 通过；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj` **440/440** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过
- BMS 默认层当前已完成 gameplay HUD、results summary / clear lamp、results gauge history、Song Select note distribution、playfield metadata / accent surfaces、playfield shell surfaces，以及 note / hold visuals 共七批 OMS-owned 默认层切片

### Phase 1.1：BMS playfield shell 默认层第六个 OMS-owned slice 落地

- `BmsDefaultPlayfieldPalette` 现已进一步承接 playfield shell surfaces：新增的 `DefaultBmsPlayfieldBackdropDisplay` / `DefaultBmsPlayfieldBaseplateDisplay` / `DefaultBmsLaneBackgroundDisplay` / `DefaultBmsLaneDividerDisplay` 已把 `Backdrop` / `Baseplate` / lane `Background` / `Divider` 的无皮肤默认路径切到 BMS-owned token
- `BmsSkinTransformer` 不再用 `BmsTemporarySkinPalette` 生成 playfield backdrop / baseplate 与 lane background / divider fallback；`BmsSkinTransformerTest` 同步补上 baseplate / divider fallback、scratch shell 路径，以及自定义 wrapped-skin 覆盖回归
- 直接受影响 `BmsSkinTransformerTest` + `TestSceneBmsLaneCover` + `TestSceneBmsHitTargetState` + `BmsDrawableRulesetTest` **114/114** 通过；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj` **433/433** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过
- 当前 BMS 默认层剩余过渡面已收敛到 note / hold visuals

### Phase 1.1：BMS playfield metadata / accent 默认层第五个 OMS-owned slice 落地

- 已新增独立 `BmsDefaultPlayfieldPalette`，并把 `DefaultBmsBackgroundLayerDisplay`、`DefaultBmsLaneCoverDisplay`、`DefaultBmsHitTargetDisplay` 与默认 `BarLine` fallback 的 metadata shell、fill / focus、bar / line / glow、major / minor 语义切到 BMS-owned token；`StaticBackgroundLayer`、`LaneCover`、`HitTarget` 与 `BarLine` 默认路径不再继续复用 `BmsTemporarySkinPalette`
- `BmsSkinTransformer` 现也会用 `BmsDefaultPlayfieldPalette` 生成 major / minor bar line 的默认 fallback；`BmsSkinTransformerTest` 同步补强为 concrete fallback 断言，并新增 `BarLine` 默认 fallback 回归
- `DefaultBmsBackgroundLayerDisplay` 里先前未接线的 `labelContainer` 已一并修正，metadata 文案与缺失态现在会跟随默认壳层正常更新
- 直接受影响 `BmsSkinTransformerTest` + `TestSceneBmsLaneCover` + `TestSceneBmsHitTargetState` + `BmsDrawableRulesetTest` **105/105** 通过；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj` **427/427** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过
- 当前 BMS 默认层剩余过渡面已收敛到 playfield backdrop / baseplate、lane background / divider 与 note / hold visuals

### Phase 1.1：BMS Song Select note distribution 默认层第四个 OMS-owned slice 落地

- `DefaultBmsNoteDistributionPanelDisplay` / `DefaultBmsNoteDistributionDisplay` 现已切到 results-style panel shell 与 BMS-owned 图表配色；Song Select 默认 note distribution 不再继续依赖 `BmsTemporarySkinPalette`
- 默认 note distribution panel 现会沿用统一的标题/状态色、卡片边框与 accent shell；内部 legend 与柱状图也已切到 BMS-owned note-distribution colours，不再把 Song Select 面板建立在临时 feedback HUD 表面上
- 已更新 `BmsSkinTransformerTest`，锁定 `NoteDistribution` / `NoteDistributionPanel` 的默认 fallback 类型分别为 `DefaultBmsNoteDistributionDisplay` / `DefaultBmsNoteDistributionPanelDisplay`；直接受影响 `BmsSkinTransformerTest` **47/47** 通过，完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj` **426/426** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过
- 当前 BMS 默认层余量已进一步收敛到 BMS-only accent

### Phase 1.1：BMS results gauge history 默认层第三个 OMS-owned slice 落地

- `BmsGaugeColours` 现已统一复用 `BmsDefaultHudPalette` 的 gauge colours；results 页默认 gauge history 不再继续依赖 `BmsTemporarySkinPalette`
- `DefaultBmsGaugeHistoryPanelDisplay` / `DefaultBmsGaugeHistoryDisplay` 与默认时间线行、plot 现已切到 results-style panel shell、BMS-owned 标题/状态色与 threshold marker；无外部皮肤时的 results gauge history 不再只是临时 feedback 图表
- 已更新 `BmsSkinTransformerTest`，锁定 `GaugeHistory` / `GaugeHistoryPanel` 的默认 fallback 类型分别为 `DefaultBmsGaugeHistoryDisplay` / `DefaultBmsGaugeHistoryPanelDisplay`；直接受影响 `BmsSkinTransformerTest` + `BmsRulesetStatisticsTest` **47/47** 通过，完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj` **426/426** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过
- 这一批之后，BMS 默认层余量曾进一步收敛到 Song Select 与 BMS-only accent

### Phase 1.1：BMS results summary + clear lamp 默认层第二个 OMS-owned slice 落地

- 已新增独立 `BmsDefaultResultsPalette`，并把 `DefaultBmsResultsSummaryPanelDisplay` / `DefaultBmsResultsSummaryDisplay` / `DefaultBmsClearLampDisplay` 的 no-custom-skin 默认路径切到专用 results token；results 页的 `BMS Statistics` 不再继续依赖临时反馈配色
- 默认 results summary 现改为使用 BMS-owned 统计卡片，而不再依赖通用 `SimpleStatisticTable`；clear lamp badge 与 summary panel 会共享 clear-lamp accent 语义，保证无外部皮肤时的 results 视觉语言开始与 gameplay HUD 对齐
- 已更新 `BmsSkinTransformerTest`，锁定 `ClearLamp` / `ResultsSummary` / `ResultsSummaryPanel` 的默认 fallback 类型分别为 `DefaultBmsClearLampDisplay` / `DefaultBmsResultsSummaryDisplay` / `DefaultBmsResultsSummaryPanelDisplay`；直接受影响测试 **46/46** 通过，完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj` **426/426** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：BMS gameplay HUD 默认层首个 OMS-owned slice 落地

- `BmsSkinTransformer` 的 `ComboCounter` 默认 fallback 现已从 upstream `DefaultComboCounter` 切到 `BmsComboCounter`；无外部皮肤时，BMS gameplay HUD 不再继续把 combo 默认实现建立在上游默认 HUD 组件上
- 已新增独立 `BmsDefaultHudPalette`，并把 `DefaultBmsHudLayoutDisplay` / `BmsGaugeBar` 的 no-custom-skin 默认路径切到专用 BMS HUD token；因此 gameplay HUD 终于开始从“皮肤加载失败时的反馈层”往正式 OMS-owned 默认层承接
- 已更新 `BmsSkinTransformerTest`，锁定 gameplay HUD 组装出的默认 combo 类型为 `BmsComboCounter`；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj` **426/426** 通过，本轮直接受影响测试 **47/47** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：BMS playfield size config bridge 首次接通

- `BmsRulesetConfigManager` 与 `BmsSettingsSubsection` 现已新增 `Playfield Width` / `Playfield Height` 配置项，并以 `Auto` 语义保留原有按 lane-count 推导的默认尺寸；`BmsPlayfieldLayoutProfile.CreateDefault()` 也已支持 playfield size override，不再把宽高固定锁死在 profile 默认值上
- loaded `BmsPlayfield` 的 runtime layout bridge 现会一并读取 playfield width / height，并在 bindable 仍处于默认值时回退到当前 `LayoutProfile` 的既有尺寸；这避免了全局固定 config default 把 7K / 14K 的默认宽度错误拉成同一个值，同时让 playfield size 正式进入同一条 OMS-owned 重布局链路
- 已扩展 `BmsRulesetConfigurationTest` 与 `TestSceneBmsPlayfieldLayoutConfig`，覆盖 playfield size 配置值、实际 lane span / lane height 的运行时生效；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj` **426/426** 通过，本轮直接受影响测试 **22/22** 通过

## 2026-04-06

### Phase 1.1：BMS hit target internal geometry config bridge 首次接通

- `BmsRulesetConfigManager` 与 `BmsSettingsSubsection` 现已新增 `Hit Target Bar Height` / `Hit Target Line Height` / `Hit Target Glow Radius` 配置项，`BmsPlayfieldLayoutProfile.CreateDefault()` 也已支持这三项内部几何 override；default receptor 的 bar / line / glow 不再只依赖 profile 默认常量
- `BmsHitTarget` 现会保存当前 layout profile，并通过新的 `IBmsHitTargetLayoutDisplay` 把 runtime profile 变更继续下推给当前 display；这不仅让 loaded `BmsPlayfield` 的重布局能更新 default receptor 内部几何，也避免 skin reload 时重新落回旧 profile 快照
- 已扩展 `BmsRulesetConfigurationTest`、`BmsLaneLayoutTest` 与 `TestSceneBmsPlayfieldLayoutConfig`，覆盖默认配置值、bar/line/glow 的运行时生效，以及 focus edge 跟随 line height 的刷新；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj` **424/424** 通过，本轮直接受影响测试 **20/20** 通过

### Phase 1.1：BMS lane width / spacing config bridge 首次接通

- `BmsRulesetConfigManager` 与 `BmsSettingsSubsection` 现已新增 `Lane Width` / `Lane Spacing` 配置项，`BmsPlayfieldLayoutProfile.CreateDefault()` 的 normal-lane width / spacing override 终于也正式接上 ruleset config bridge；normal lane 不再只依赖 profile 默认常量
- `BmsPlayfield` 的 loaded runtime layout bridge 现会与既有 scratch width / spacing、hit target height / vertical offset、bar line height 一起读取 `Lane Width` / `Lane Spacing`，并在需要时重算 `BmsLaneLayout`；因此 regular key lanes 的宽度、相邻 gap 与 total span 现在也能沿同一条 OMS-owned runtime layout 链更新
- 已扩展 `BmsRulesetConfigurationTest`、`BmsLaneLayoutTest` 与 `TestSceneBmsPlayfieldLayoutConfig`，覆盖默认配置值、normal lane width / spacing 的运行时生效；同时测试 scene 的 layout setup 现会显式重置相关 ruleset config，避免跨用例串值污染后续 layout 断言；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --no-build` **421/421** 通过，本轮直接受影响测试 **17/17** 通过

### Phase 1.1：BMS receptor vertical-position config bridge 首次接通

- `BmsRulesetConfigManager` 与 `BmsSettingsSubsection` 现已新增 `Hit Target Vertical Offset` 配置项，`BmsPlayfieldLayoutProfile.CreateDefault()` 也已支持 receptor vertical offset override；BMS receptor vertical position 不再只依赖 lane 底边硬编码
- 已新增 `BmsHitObjectArea`，把每条 lane 的 `ScrollingHitObjectContainer` 与 `BmsHitTarget` 放进同一 hit-position 容器，并按当前 scroll direction 把 top / bottom padding 落到真实 scrolling container 上；因此这次移动的不只是 receptor 视觉，而是实际 hit line 与可见 scroll length
- `DrawableBmsRuleset` 现会消费 playfield 回传的有效 scroll-length ratio，并在 `ScrollSpeed` 对应的 `TimeRange` 上做同倍率缩放；receptor vertical offset 不会悄悄退化成另一条“变相变速”路径
- 已扩展 `BmsRulesetConfigurationTest` 与 `TestSceneBmsPlayfieldLayoutConfig`，覆盖默认配置值、正向/反向 scroll direction 下的 receptor vertical offset，以及 scrolling container edge 与 receptor 的对齐；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **419/419** 通过，本轮直接受影响测试 **12/12** 通过

### Phase 1.1：BMS hit target / bar line vertical-size config bridge 首次接通

- `BmsRulesetConfigManager` 与 `BmsSettingsSubsection` 现已新增 `Hit Target Height` / `Bar Line Height` 配置项，`BmsPlayfieldLayoutProfile.CreateDefault()` 也已支持这两个垂直尺寸 override；hit target 与 measure bar line 不再只依赖默认 profile 常量
- `BmsPlayfield` 的 loaded runtime layout bridge 现会与 scratch width ratio / spacing 一起读取 `Hit Target Height` / `Bar Line Height`，并通过轻量 lane apply-layout 路径刷新现有 `BmsHitTarget` 与 `DrawableBmsBarLine`；因此无需回退到 `DrawableBmsRuleset.CreatePlayfield()` 构造期提前读 config，也能让已创建的 lane 装饰组件跟随真实 ruleset config cache 更新
- 已扩展 `BmsRulesetConfigurationTest`、`BmsLaneLayoutTest`、`BmsDrawableRulesetTest` 与 `TestSceneBmsPlayfieldLayoutConfig`，覆盖默认配置值、运行时 hit target 高度刷新与 bar line 高度刷新；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **417/417** 通过，本轮直接受影响测试 **60/60** 通过

### Phase 1.1：BMS scratch width ratio config bridge 首次接通

- `BmsRulesetConfigManager` 与 `BmsSettingsSubsection` 现已新增 `Scratch Lane Width Ratio` 配置项；scratch lane width 不再只依赖 `BmsPlayfieldLayoutProfile` 的默认 `1.25x` 常量，而是和 scratch spacing 一样进入 ruleset config bridge
- `BmsPlayfield` 现会在 load 阶段同时读取 scratch width ratio 与 scratch spacing，并在需要时重算 `BmsLaneLayout`；因此 scratch lane 的宽度、相邻 spacing 与 total span 现在都能沿同一条 runtime layout bridge 更新，而不必在 `DrawableBmsRuleset.CreatePlayfield()` 构造期提前读取 config
- 已扩展 `TestSceneBmsPlayfieldLayoutConfig` 与 `BmsRulesetConfigurationTest`，覆盖非默认 width ratio、生效后的 lane width 比例，以及 `1.0x` 时 scratch lane 回落到普通宽度的运行时表现；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **415/415** 通过，本轮直接受影响测试 **59/59** 通过

### Phase 1.1：BMS scratch spacing config bridge 首次接通

- `BmsRulesetConfigManager` 与 `BmsSettingsSubsection` 现已新增 `Scratch Lane Spacing` 配置项；scratch spacing 不再只存在于 `BmsPlayfieldLayoutProfile` 的默认常量里，而是开始拥有第一条可持久化、可验证的 ruleset config bridge
- `BmsLaneLayout` 仍负责计算 `RelativeSpacingBefore` 与 total span，但 `BmsPlayfield` 现在会在 load 阶段从真实 ruleset config cache 读取 scratch spacing，并在需要时对 lane 几何做一次重布局；这样避免了 `DrawableBmsRuleset.CreatePlayfield()` 构造期过早读取 config，同时让运行时 playfield 能实际消费配置值
- 已补充 `TestSceneBmsPlayfieldLayoutConfig`，并扩展 `BmsRulesetConfigurationTest`，覆盖默认配置值、非零 spacing 生效以及零 spacing 取消 gap 的运行时表现；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **413/413** 通过，本轮直接受影响测试 **57/57** 通过

### Phase 1.1：BMS scratch spacing 正式接通

- `BmsPlayfieldLayoutProfile` 现已新增 normal / scratch lane spacing 契约，`BmsLaneLayout` 也开始按相邻车道类型计算 `RelativeSpacingBefore`；scratch 相邻车道不再默认全贴边排列，scratch width 与 scratch spacing 终于开始沿同一条 OMS-owned layout metadata 链路收口
- `BmsLaneLayout.TotalRelativeWidth` 现会把 spacing 一并计入 total span，因此 `BmsPlayfield` 的 lane `X` / `Width` 归一化定位可以直接消费 scratch gap；regular key-key 仍保持贴合，scratch-key / key-scratch 过渡则会留下正式的相对间距
- 已扩充 `BmsLaneLayoutTest` 与 `BmsDrawableRulesetTest`，覆盖 7K / 14K scratch spacing 语义与 playfield 运行时定位；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **410/410** 通过，本轮直接受影响测试 **51/51** 通过

### Phase 1.1：BMS receptor state 正式契约首次接通

- `BmsHitTarget` 现已拥有 `IsPressed` / `IsFocused` 两个正式状态 bindable，并新增 `IBmsHitTargetDisplay` 作为 receptor display contract；默认 `DefaultBmsHitTargetDisplay` 已可消费 pressed / focused state，不必再等到后续默认皮肤迁移时才临时拼接 down-state / focus-state 语义
- `BmsLane` 现会从 `BmsInputManager.KeyBindingContainer.PressedActions` 同步当前 lane action 的 pressed state，因此 receptor pressed state 不再依赖某个具体 drawable 是否消费了按键事件；regular lane 与 scratch lane 的 hit target 都能沿同一条 runtime 状态链更新
- 已新增 `TestSceneBmsHitTargetState`，并与 `BmsDrawableRulesetTest`、`BmsSkinTransformerTest` 一起覆盖 receptor state 视觉契约与输入同步；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **410/410** 通过，本轮直接受影响测试 **100/100** 通过

### Phase 1.1：BMS playfield adjustment/config bridge 首次接通

- `DrawableBmsRuleset` 现已切入专用 `BmsPlayfieldAdjustmentContainer`，把 BMS playfield 从通用 `PlayfieldAdjustmentContainer` 升级为 ruleset 自身可控的 adjustment/scaling 入口；当前默认实现支持整体缩放与横向偏移，为后续 receptor state、scratch spacing 与更完整的 layout bridge 继续收口预留了专用落点
- `BmsRulesetConfigManager` 与 `BmsSettingsSubsection` 现已新增 `Playfield Scale` / `Playfield Horizontal Offset` 两个配置项；BMS playfield adjustment/scaling 终于不再完全写死在 runtime 结构里，而是拥有第一条可验证、可持久化、可扩展的 ruleset config bridge
- 已补充 `BmsPlayfieldAdjustmentContainerTest`、扩展 `BmsRulesetConfigurationTest` 与 `BmsDrawableRulesetTest`，覆盖默认配置值、专用 adjustment container 挂接，以及 adjustment bindable 变更后的缩放 / 偏移行为；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **405/405** 通过，本轮直接受影响测试 **50/50** 通过

### Phase 1.1：BMS playfield abstraction gate 首步落地

- 已新增共享 `BmsPlayfieldLayoutProfile`，将 BMS playfield 的默认几何参数统一收口；`BmsLaneLayout`、`BmsPlayfield`、`BmsHitTarget` 与 `DrawableBmsBarLine` 现都从同一 profile 读取 lane 宽度、playfield 尺寸、hit target 高度与 bar line 高度，不再继续散落硬编码
- `BmsLaneLayout` 现会随 keymode / lane count 一起生成默认 layout profile；`BmsPlayfield`、`BmsLane` 与 scratch lane 也已改为沿用同一份 profile 构造默认 lane 装饰组件，为后续 receptor state、spacing、playfield adjustment/scaling 与配置桥接继续收口预留统一入口
- 已补充 `BmsLaneLayoutTest` 与 `BmsDrawableRulesetTest` 的几何回归覆盖，验证默认 profile、lane 宽度映射、hit target 高度与 bar line 高度的一致性；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **402/402** 通过，本轮直接受影响测试 **48/48** 通过

### 文档治理：Phase 1.1 执行顺序、候选包语义与 release gate 收口

- 已把 `DEVELOPMENT_PLAN.md`、`DEVELOPMENT_STATUS.md`、`README.md`、`SKINNING.md`、`RELEASE.md` 与 `OMS_COPILOT.md` 统一到同一套口径：Phase 1.1 明确按“共享边界 / 宿主骨架 → BMS playfield abstraction gate → BMS 默认层 → mania OMS-owned 迁移 → partial override / 上游默认皮肤移除 / release gate”推进，不再保留 mania/BMS 双线并行复刻的模糊表述
- 已把 `SKIN/SimpleTou-Lazer` 的文档语义统一收口为“OMS 内置皮肤候选基线 / mania 侧基础与视觉参考”；文档层面不再允许把它提前描述成“已完成的 OMS 默认皮肤”
- 已把 BMS 当前真实结构性缺口显式写入权威文档：虽然 drawable lookup 已覆盖多数组件，但 `BmsLaneLayout` / `BmsPlayfield` / `BmsHitTarget` 仍缺少 config-driven playfield 几何层，因此 faithful 的视觉复刻必须先补抽象、后补样式
- 本次为**文档治理与计划收口**，未改动运行时代码；最近一次已验证结论仍为 `dotnet build osu.Desktop` 通过、`dotnet test osu.Game.Rulesets.Bms.Tests` **400/400** 通过

### Phase 1.1：BMS 第三批 Gameplay HudLayout 接入 formal skinization

- 已把 `HudLayout` 纳入 `BmsSkinComponents` 正式契约；`BmsSkinTransformer` 现支持该组件的 ruleset 级 fallback / override，要求皮肤实现 `IBmsHudLayoutDisplay`
- `BmsSkinTransformer` 现在会把 BMS `MainHUDComponents` 路由到外层 `HudLayout`：保留 wrapped HUD 内容，再注入 `GaugeBar` 与 `ComboCounter`；因此皮肤既可整体替换 gameplay HUD 布局，也可继续单独替换 gauge / combo 本体
- 默认 `DefaultBmsHudLayoutDisplay` 承担当前 fallback HUD 布局，负责沿用现有 gauge / combo 定位、抑制重复 combo counter，并继续为 `ISerialisableDrawable` 固定 anchor，避免破坏现有 HUD 保存链路
- 已扩充 `BmsSkinTransformerTest`，覆盖 `HudLayout` 的默认 fallback、自定义 override 与 ruleset HUD 集成；聚焦 `BmsSkinTransformerTest` **47/47** 通过，全量 `osu.Game.Rulesets.Bms.Tests` **400/400** 通过
- `dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过；当前仍仅有既有 `AutoMapper` `NU1903` 告警
- `README.md`、`SKINNING.md`、`DEVELOPMENT_STATUS.md` 与仓库记忆已同步更新，gameplay HUD 外层布局当前真实落地状态现与文档一致

### Phase 1.1：BMS 第三批 Results ResultsSummaryPanel 接入 formal skinization

- 已把 `ResultsSummaryPanel` 纳入 `BmsSkinComponents` 正式契约；`BmsSkinTransformer` 现支持该组件的 ruleset 级 fallback / override，要求皮肤实现 `IBmsResultsSummaryPanelDisplay`
- `BmsRuleset.CreateStatisticsForScore()` 的 summary item 现改为返回无标题 `StatisticItem`，由 `SkinnableBmsResultsSummaryPanelDisplay` 自己承载 `BMS Statistics` 标题、空态与内层 `SkinnableBmsResultsSummaryDisplay`；因此皮肤既可整体替换 results summary 面板，也可只替换内部 summary 内容
- 已扩充 `BmsSkinTransformerTest` 与 `BmsRulesetStatisticsTest`，覆盖 `ResultsSummaryPanel` 的默认 fallback / 自定义 override 与 results item 集成；直接受影响测试 **44/44** 通过，全量 `osu.Game.Rulesets.Bms.Tests` **397/397** 通过
- `dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过；当前仍仅有既有 `AutoMapper` `NU1903` 告警
- `README.md`、`SKINNING.md`、`DEVELOPMENT_STATUS.md` 与仓库记忆已同步更新，results summary panel 当前真实落地状态现与文档一致

### Phase 1.1：BMS 第三批 Results GaugeHistoryPanel 接入 formal skinization

- 已把 `GaugeHistoryPanel` 纳入 `BmsSkinComponents` 正式契约；`BmsSkinTransformer` 现支持该组件的 ruleset 级 fallback / override，要求皮肤实现 `IBmsGaugeHistoryPanelDisplay`
- `BmsRuleset.CreateStatisticsForScore()` 的 gauge history item 现改为返回无标题 `StatisticItem`，由 `SkinnableBmsGaugeHistoryPanelDisplay` 自己承载 `GAUGE HISTORY` 标题、空态与内层 `SkinnableBmsGaugeHistoryDisplay`；因此皮肤既可整体替换 results gauge history 面板，也可只替换内部时间线图表
- 已扩充 `BmsSkinTransformerTest` 与 `BmsRulesetStatisticsTest`，覆盖 `GaugeHistoryPanel` 的默认 fallback / 自定义 override 与 results item 集成；直接受影响测试 **35/35** 通过，全量 `osu.Game.Rulesets.Bms.Tests` **395/395** 通过
- `dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过；当前仍仅有既有 `AutoMapper` `NU1903` 告警
- `README.md`、`SKINNING.md`、`DEVELOPMENT_STATUS.md` 与仓库记忆已同步更新，results gauge history panel 当前真实落地状态现与文档一致

### Phase 1.1：BMS 第三批 Song Select NoteDistributionPanel 接入 formal skinization

- 已把 `NoteDistributionPanel` 纳入 `BmsSkinComponents` 正式契约；`BmsSkinTransformer` 现支持该组件的 ruleset 级 fallback / override，要求皮肤实现 `IBmsNoteDistributionPanelDisplay`
- `BmsNoteDistributionGraph` 现拆为“外层 skinnable panel + 内层 skinnable graph”两层：默认 panel 负责标题、状态文本与总数 / scratch / long note / 峰值密度摘要，内部图表仍继续走 `NoteDistribution` 组件 lookup，因此皮肤既可整体替换 Song Select 右侧面板，也可只替换图表主体
- 已扩充 `BmsSkinTransformerTest`，覆盖 `NoteDistributionPanel` 的默认 fallback 与自定义 override；直接受影响测试 `BmsSkinTransformerTest` **40/40** 通过，全量 `osu.Game.Rulesets.Bms.Tests` **393/393** 通过
- `dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过；当前仍仅有既有 `AutoMapper` `NU1903` 告警
- `README.md`、`SKINNING.md`、`DEVELOPMENT_STATUS.md` 与仓库记忆已同步更新，Song Select 分布图面板当前真实落地状态现与文档一致

### Phase 1.1：BMS 第三批 ResultsSummary / ClearLamp 接入 formal skinization

- 已把 `ClearLamp` 与 `ResultsSummary` 纳入 `BmsSkinComponents` 正式契约；`BmsSkinTransformer` 现支持这两个组件的 ruleset 级 fallback / override，分别走 `IBmsClearLampDisplay` 与 `IBmsResultsSummaryDisplay`
- `BmsRuleset.CreateStatisticsForScore()` 的 `BMS Statistics` 现改为返回 `SkinnableBmsResultsSummaryDisplay`；默认 summary 会展示 gauge type、judge mode、long note mode、EX-SCORE / MAX EX-SCORE、EMPTY POOR、EX %、DJ LEVEL 与 final gauge，并在内部嵌入可独立 override 的 `SkinnableBmsClearLampDisplay`
- 已扩充 `BmsSkinTransformerTest` 与 `BmsRulesetStatisticsTest`，覆盖 clear lamp / results summary 的 fallback / override 与 results item 集成；聚焦受影响测试 **40/40** 通过，全量 `osu.Game.Rulesets.Bms.Tests` **391/391** 通过
- `dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过；当前仍仅有既有 `AutoMapper` `NU1903` 告警
- `README.md`、`SKINNING.md`、`DEVELOPMENT_STATUS.md` 与 `OMS_COPILOT.md` 已同步更新，第三批当前真实落地状态现与文档一致

### Phase 1.1：BMS 第二批 Judgement / Combo 接入 formal skinization

- 已为 BMS 自定义判定建立正式 lookup 契约：新增 `BmsJudgementSkinLookup`，并把 `BAD / POOR / EMPTY POOR` 从通用 `SkinComponentLookup<HitResult>` 路由到 `SkinnableBmsJudgement`，再进入 ruleset 级 fallback / override；默认显示仍由 `BmsJudgementPiece` 承担，因此 OMS 的产品命名语义保持不变
- 已把 combo display 正式收口为 `BmsSkinComponents.ComboCounter`；`BmsSkinTransformer` 的 BMS `MainHUDComponents` 现在会在保留皮肤自身 ruleset HUD 内容的同时，补入 BMS gauge bar 与 combo counter，并隐藏重复的通用 combo counter
- 已扩充 `BmsSkinTransformerTest`，覆盖自定义判定 wrapper、显式 judgement lookup、combo counter fallback / override 以及 ruleset HUD 注入语义；直接受影响测试文件 `BmsSkinTransformerTest` **34/34** 通过，全量 `osu.Game.Rulesets.Bms.Tests` **386/386** 通过
- `dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过；当前仍仅有既有 `AutoMapper` `NU1903` 告警
- `README.md`、`SKINNING.md` 与 `DEVELOPMENT_STATUS.md` 已同步更新，文档基线现与第二批 `Judgement / Combo` 的真实落地状态一致

### Phase 1.1：BMS 第二批 Note / Hold / LaneCover 开始 formal skinization

- 已为 BMS 第二批主体组件建立正式 lookup 契约：新增 `BmsNoteSkinLookup` / `BmsLaneCoverSkinLookup`，并把 normal note、长条头/体/尾、top lane cover、bottom lane cover 纳入 `BmsSkinTransformer` 的 ruleset 级查找与 fallback 路由
- `DrawableBmsHitObject` 已从直接按类型绘制 `Box` 的路径切到 `SkinnableDrawable` 驱动；普通 note、长条头、长条体、长条尾都会根据 lane 与 scratch 元数据进入正式 lookup 链
- `BmsLaneCover` 已改为“外层 coverage 容器 + 内层 skinnable display”的正式结构；`top / bottom` 位置与 focus 状态现在都属于正式皮肤语义，而不是临时 overlay
- 已扩充 `BmsSkinTransformerTest`，新增 note / long-note / lane-cover 的 fallback 与 override 覆盖；直接受影响测试文件运行 **22/22** 通过，并在收尾时重新跑完整 `osu.Game.Rulesets.Bms.Tests` 项目，全量 **378/378** 通过
- 本轮全量 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` 通过；当前仍仅有既有 `AutoMapper` `NU1903` 告警

### 文档：新增玩家 / 皮肤设计师说明书

- 新增根目录 `SKINNING.md`，专门面向玩家与皮肤设计师解释 OMS 当前皮肤系统的真实落地状态、fallback 顺序、BMS 已开放组件矩阵，以及哪些部分仍未冻结
- `README.md` 已补充 `SKINNING.md` 入口，并把最近一次验证结果更新为当前的 **378/378** BMS 全量测试快照
- `DEVELOPMENT_STATUS.md` 已同步把 BMS 第二批从“未开始”修正为“进行中”，并更新当前皮肤基线与最近一次验证摘要，避免文档继续落后于仓库真实状态

### Phase 1.1：BMS 第一批壳层组件开始正式 skinization

- 已为 BMS 第一批壳层组件建立正式 skin lookup 契约：新增 `BmsPlayfieldSkinLookup` / `BmsLaneSkinLookup`，并把 `Playfield`、`Lane`、`HitTarget`、`BarLine`、`Static Background Layer` 纳入 `BmsSkinTransformer` 的 ruleset 级查找与 fallback 路由
- `BmsPlayfield`、`BmsLane`、`BmsScratchLane`、`BmsHitTarget`、`BmsScratchHitTarget`、`BmsBackgroundLayer`、`DrawableBmsBarLine` 已从纯直绘 fallback 结构切到 `SkinnableDrawable` 驱动；现阶段仍使用默认 fallback 外观，但 ruleset 层正式 skinization 入口已经接通
- `BmsLaneLayout` 现携带 `Keymode` 元信息，lane / bar line / hit target 的 lookup 也会带上 lane index、lane count、scratch 标记与 keymode，为后续默认皮肤包和用户皮肤 partial override 提供稳定上下文
- 已扩充 `BmsSkinTransformerTest`，覆盖 playfield backdrop、lane background、hit target、static background 的 fallback 与 override 路由；调试期项目级过滤运行 **21/21** 通过，并已在收尾时重新跑完整 `osu.Game.Rulesets.Bms.Tests` 项目，全量 **373/373** 通过
- `dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过；当前仍仅有既有 `AutoMapper` `NU1903` 告警

### 文档重排：Phase 1.1 改为按皮肤组件批次推进

- 已重写 `DEVELOPMENT_PLAN.md` 的 **Phase 1.1**：不再只写“共享视觉契约 / built-in skin 骨架”这类抽象目标，而是改成按具体组件批次推进的开发计划：默认皮肤包分层、组件清单与代码映射、资源命名/配置桥接、Global provider、mania 的 Stage/Column/Key 与 Note/Hold/HUD 两批迁移，以及 BMS 的 Playfield/Lane、Note/LaneCover、Gauge/Results/Song Select 三批迁移
- 已在 `DEVELOPMENT_STATUS.md` 中把 Phase 1.1 当前状态表改为组件级矩阵，明确哪些仍是 feedback 层、哪些只有局部 lookup、哪些完全未开始，避免继续把皮肤系统当成纯抽象规划
- 已澄清默认皮肤包语义：OMS 的目标是**一个默认皮肤选择项/一个默认皮肤包**，其中集成 `Global + Mania + BMS` 三层；mania 与 BMS 的 gameplay 皮肤本体彼此独立，不是“共用同一套 gameplay 皮肤语义”，只是在同一个默认皮肤包中并存
- `README.md` 与 `OMS_COPILOT.md` 已同步改写为上述语义，避免后续把“单一默认皮肤包”误读成“mania/BMS 共用一套 note / judgement / lane / HUD 皮肤”
- 本次为**文档 / 规划重排**，未改动运行时代码；未重新跑构建或测试

## 2026-04-05

### Phase 1.1：BMS fallback feedback 皮肤配色改为 IIDX 风格

- `osu.Game.Rulesets.Bms/UI/BmsTemporarySkinPalette.cs` 新增统一临时调色板，集中定义皮肤无法正常加载时的 BMS feedback/fallback 层所使用的 IIDX 风格暗底、冷色长条、暖色 scratch 与金属分隔线配色，减少 gameplay 颜色继续散落在多个类中的情况
- `BmsPlayfield`、`BmsLane`、`BmsScratchLane`、`BmsHitTarget`、`BmsScratchHitTarget`、`DrawableBmsHitObject` 与 `DrawableBmsBarLine` 现已统一改用该调色板；整体方向调整为更接近 IIDX 的深色底板、银白普通 note、青蓝长条、暖橙 scratch
- 长条头尾与长条体现在分离配色：长条判定点使用更亮的冷青色，长条主体使用更深的蓝青色；scratch 长条主体也改为更深的暖铜色，避免与普通 scratch note 混成同一亮度块
- `BmsLaneCover`、`BmsBackgroundLayer`、`BmsGaugeBar`、`BmsGaugeHistoryGraph` 与 `BmsNoteDistributionGraph` 现也统一接入同一临时调色板：lane cover 改为深烟黑 + 暖金聚焦提示，背景占位改为冷灰蓝洗版，gauge / 历史图 / 分布图面板改为统一的深色 HUD 表面与金属边框，同时 Song Select 分布图中的 scratch / long note 语义色与 gameplay 侧保持一致
- `BmsGaugeColours` 的各档位 gauge 主色/高亮色也已收口到 `BmsTemporarySkinPalette`，避免 gameplay HUD 仍保留独立硬编码色表；`BmsGaugeBar` 的默认回退色也改为沿用 HUD 文本体系
- 处理了 feedback 层对后续皮肤体系的两处直接阻塞：`BmsSkinTransformer` 的 BMS `MainHUDComponents` 现会先尊重皮肤提供的 ruleset HUD，再 fallback 到默认 gauge bar 容器；同时新增 `GaugeBar` / `GaugeHistory` 的 BMS skin lookup，使默认皮肤与未来用户导入皮肤都可以单独接管这两个组件，而不必继续被直绘 feedback 层硬拦截
- `BmsRuleset.CreateStatisticsForScore()` 的 results gauge history 现改为走 `SkinnableBmsGaugeHistoryDisplay`，并补上 `BmsSkinTransformerTest` 覆盖 HUD 覆盖优先级、gauge bar fallback 与 gauge history fallback 路由
- 注意：以上改动服务于“皮肤无法正常加载时的 feedback/fallback 层”，不代表 OMS 默认内置皮肤的正式设计方向；真正的 OMS built-in skin 仍待 Phase 1.1 后续开发
- `dotnet test osu.Game.Rulesets.Bms.Tests --filter FullyQualifiedName~BmsSkinTransformerTest` 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过；仅有既有 `AutoMapper` `NU1903` 告警

### 文档规划：Phase 1.1 皮肤系统专项

- 已在 `OMS_COPILOT.md` 中重写皮肤系统权威规范：明确 OMS 将继续使用 osu!lazer 既有 `ISkin` / `Skinnable` 架构，但最终产品默认皮肤必须迁移到 OMS 自有 built-in skin；默认皮肤应表现为一个集成 `Global + Mania + BMS` 的默认皮肤包，其中 mania 与 BMS 为各自独立的 ruleset 皮肤实现，并逐步移除 Argon / Triangles / Legacy / Retro 等上游原生默认皮肤的默认产品地位
- 已在 `DEVELOPMENT_PLAN.md` 中新增 **Phase 1.1 皮肤系统专项**（`1.1.1` ~ `1.1.12`），并将其改写为按默认皮肤包骨架、mania 组件批次、BMS 组件批次、用户皮肤兼容桥、上游默认皮肤移除、打包约束与测试矩阵推进的主线
- 已在 `DEVELOPMENT_STATUS.md`、`README.md`、`RELEASE.md` 中同步当前主执行焦点切换：后续主精力优先投入皮肤系统完善，并把“正式发行只附带 OMS 内置默认皮肤、不再以 osu!lazer 原生默认皮肤作为对外默认体验”写入状态与发行约束
- 本次为**文档 / 规划更新**，未改动运行时代码；现有最近一次 `dotnet build osu.Desktop` 通过与 `dotnet test osu.Game.Rulesets.Bms.Tests` **361/361** 通过的结论保持不变

### 信噪比优化：文档压缩与 Discord RPC 守卫

- **Discord Rich Presence 守卫**：`OsuGameDesktop.LoadComplete()` 现在只在 `OnlineFeaturesEnabled` 为 `true` 时才加载 `DiscordRichPresence` 组件，离线模式下不再向 Discord 泄露活动状态
- **联网约束表收口**："游戏内联网入口隐藏"与"上游静态资源 fallback 清理"均已升级为"已完成"，所有运行时可达的在线入口已经确认被 `OnlineFeaturesEnabled` / 空 endpoints / `LocalOfflineAPIAccess` 三重防线阻断
- **DEVELOPMENT_STATUS.md 大幅压缩**：从 ~400 行压缩到 ~160 行：Phase 1 矩阵每行从长段落压缩为 1-2 句关键事实；"已落地能力"改为引用矩阵的高层摘要（~5000 字符 → ~500）；"当前主线"改为表格；"遗留问题"去除重复描述；"下一次更新时应检查"从 11 项精简到 5 项
- **README.md 精简**："当前状态"从 ~14 条分项列表压缩为 6 项关键能力 + 1 句验证摘要
- **repo memory 精简**：`oms-project-summary.md` "当前主线与断点"从 ~30 行 verbose 流水账压缩为 5 行
- `dotnet build osu.Desktop` 退出码为 0；`dotnet test osu.Game.Rulesets.Bms.Tests` **361/361** 通过

### 文档重构：验证历史迁移与联网审计收口

- **新建 `CHANGELOG.md`**：将 DEVELOPMENT_STATUS.md 中 ~190 行的"最近一次验证"完整历史迁移至本文件，原文件仅保留最新快照与交叉引用，减少 DEVELOPMENT_STATUS.md 冗长度
- **联网入口全面审计收口**：确认 Toolbar、Song Select、所有 overlay、编辑器外链、Preview/LargeTextureStore/metadata cache/BundledBeatmapDownloader/SentryLogger/SignalR 均已被 `OnlineFeaturesEnabled` / 空 endpoints / `LocalOfflineAPIAccess` 三重防线阻断；Settings 的 Report Issue 按钮新增 `OnlineFeaturesEnabled` 守卫；`Medal.ImageUrl`、`OsuMarkdownImage` 中的硬编码 ppy.sh URL 因对应 overlay 已阻断而不可触发；Discord Rich Presence 本地 IPC 仍无条件活跃（不发网络请求），后续可按需调整
- `dotnet build osu.Desktop` 退出码为 0；`dotnet test osu.Game.Rulesets.Bms.Tests` **361/361** 通过；当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 便携发行基线与 cherry-pick 跟踪

- **新增 `RELEASE.md`**：文档化了 Portable.zip 构建命令、发行物内容、用户数据存储路径（`%APPDATA%/oms/`）、版本覆盖更新流程、冒烟测试与在线功能状态；确认程序文件覆盖不影响已导入谱面、成绩与设置
- **扩充 `UPSTREAM.md`**：完整列举了 osu.Game 中被 OMS 修改的 ~37 个文件（2 个新增 + ~35 个修改），按层级分类（离线 gate / Ruleset 扩展点 / RulesetData 持久化 / 自定义 Loader），并标注了 cherry-pick 高风险文件（`BeatmapCarousel`、`FilterControl`、`WorkingBeatmapCache`、`BeatmapManager`、`OsuGame`、`OsuGameBase`）
- **Portable 发行审计结论**：`Program.cs` 的 `setupVelopack()` 已完全早退、`OsuGameDesktop` 无安装路径假设、首次运行逻辑已绕过、用户数据与程序目录分离——无阻断项
- `dotnet build osu.Desktop` 退出码为 0；`dotnet test osu.Game.Rulesets.Bms.Tests` **361/361** 通过；当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 遗留问题噪声清理与代码清洁

- **DEVELOPMENT_STATUS 噪声清理**：移除了遗留问题与检查列表中 6 处已修复的删除线条目（results auto-jump、Directory.Build.props NuGet 元数据、slnf Templates 项目、nullable 告警、results auto-jump 实机验证检查项、测试覆盖缺口检查项），减少后续对话信息噪声
- **`BmsScoreProcessor` 诊断日志**：`[BMS] ApplyBeatmap`、`COMPLETED OK`、`COMPLETION STUCK` 三组诊断日志及关联计数器字段已包裹在 `#if DEBUG` 条件编译内，Release 构建不再无条件输出这些仅用于调试 results auto-jump 的运行时日志
- **`osu.nuspec` 元数据**：title / authors / owners / description / copyright 从上游 `ppy Pty Ltd` 更新为 OMS；移除了指向 `osu.ppy.sh` 的 projectUrl
- **在线 fallback 审计**：确认 `PreviewTrackManager`、`LocalCachedBeatmapMetadataSource`、`BundledBeatmapDownloader` 中的 `ppy.sh` URL 均已被现有离线模式守卫屏蔽，运行时不可达；`TrustedDomainOnlineStore` 仍为 `*.ppy.sh` 白名单，待 Phase 3 有 OMS 域名时再扩展；Velopack 已被 `IsInAppUpdateEnabled => false` 完全短路，相关死代码保留作为 Phase 3 技术预留
- `dotnet build osu.Desktop` 退出码为 0；`dotnet test osu.Game.Rulesets.Bms.Tests` **361/361** 通过；当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 遗留问题处理

- **测试覆盖缺口补齐**：新增 `BmsKeysoundSampleInfoTest`（20 个独立单测：构造器正规化、路径遍历拒绝、`LookupNames` 有/无扩展名、`Equals` / `GetHashCode` 大小写不敏感、`TryCreate` / `TryNormaliseFilename` 边界、`With()` 保持 filename）与 `TestSceneBmsLaneCover`（7 个 headless scene 回归：position 保留、覆盖率 0/50/clamp、focus 显/隐/zero-coverage 不显示）；`BmsOrderedHitPolicy` 已由 `BmsDrawableRulesetTest` 与 `TestSceneOmsScratchGameplayBridge` 间接覆盖，无需独立桩测试
- **`Directory.Build.props` NuGet 元数据**：Authors / Company / Copyright 从 `ppy Pty Ltd` 更新为 `OMS contributors`，PackageTags 追加 `bms`
- **nullability 告警**：构建确认仅剩 `AutoMapper` `NU1903`，无 OMS 引入的 nullable 告警
- **AutoMapper 升级评估**：14.0+ 有破坏性 API 变更（`c.Internal().ForAllMaps()` 等 internal API 不兼容），移除需手写 ~150 行替代深拷贝；当前 `MaxDepth(3)` 已缓解实际攻击面，暂不动，继续跟踪
- `dotnet build osu.Desktop` 退出码为 0；`dotnet test osu.Game.Rulesets.Bms.Tests` **361/361** 通过（较之前 326 新增 35 测试）

### 1.17 analog scratch pulse 语义、loaded gameplay bridge 与 scratch stream/hold 回归

- `OmsMouseAxisInputHandler` / `OmsHidAxisInputHandler` 不再把同向连续移动折叠成跨多帧/多轮询的一次长按，而是按帧/按轮询在边界自动 release。这样同向持续搓盘会重新产生成对的 press/release pulse，更接近 scratch 离散触发而不是"第一拍按下后一直按住"
- 同一帧/同一轮询内若同一 scratch action 从正向切到反向，axis handler 现会先 release 再 re-press，而不再被 shared-action 引用计数折叠成一次连续长按；这让快速换向也能继续产出新的 scratch edge
- `OmsHidDeviceHandlerTest` 现已补上 device polling 层 rapid-flip 回归：单次 `PollOnce()` 内若 turntable axis batch 从正向切到反向，`OmsHidDeviceHandler` 仍会排空全部 queued axis changes，并产出独立的 `+/-/+/-` scratch pulse，而不是在 device 层吞掉第二个 edge
- `BmsInputManager` 现只在 `OmsInputRouter` 的全局首个 press / 最终 release 时才把对应 `BmsAction` 转发给 `KeyBindingContainer`；keyboard、XInput、mouse-axis、HID axis 等多个 source 共享同一 scratch 时，不会再因为某个非最终 source 的 release 把 gameplay 侧动作提前放掉
- `OmsInputRouterTest` 现已额外锁定显式 `OmsMouseAxisInputHandler -> BmsInputManager -> KeyBindingContainer` 链路：mouse-axis pulse 会在帧尾释放 gameplay scratch 动作，而当 keyboard 已持有同一 scratch 时，mouse-axis pulse 的帧尾 release 不会把 gameplay 侧动作提前放掉
- 新增 `TestSceneOmsScratchGameplayBridge` loaded headless scene 回归：显式证明 `OmsMouseAxisInputHandler` / `OmsHidAxisInputHandler` / XInput scratch 经 `BmsInputManager -> DrawableBmsRuleset/BmsPlayfield` 的真实输入链都能结算 scratch stream；重复 scratch pulse / press 可连续命中，而较晚输入也会在 poor window 内强制把更早未判 note 记为 miss。新增 mixed-source scene 进一步锁定 keyboard 持有 scratch 时，HID pulse、mouse pulse 与 XInput press 都不会伪造新的 gameplay hit edge，scratch 只会在最后一个 source release 后才真正松开；若此时正在持有 scratch hold，mouse/HID pulse 的 `FinishFrame()` / `FinishPolling()` 边界不会打断 hold，XInput 也能在 keyboard 中途释放后继续接管 hold，tail 仍会沿 held path 正常结算。此前把这类 bridge 回归写在 detached plain NUnit harness 中会得到假阴性，因此现已统一迁到 loaded scene
- `BmsDrawableRulesetTest` 现已补上 gameplay-facing scratch stream 回归：重复 scratch press 可以连续命中 scratch stream，且在仍处于 poor window 内时，较晚的 scratch 命中会强制把更早未判定 note 记为 miss。为让这类 direct-drawable lane 单测与 runtime 共享同一 ordering 语义，`BmsOrderedHitPolicy` 现会优先读取 `AliveObjects`，若 detached/non-pooled harness 尚未物化 alive lifetime，则回退到当前 in-use `Objects`
- `dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter FullyQualifiedName~TestSceneOmsScratchGameplayBridge -verbosity minimal` 最近一次 **14/14** 通过，并已覆盖 mouse-axis + HID-axis + XInput 的 loaded gameplay bridge、keyboard-held mixed-source suppression、keyboard-held scratch hold 在 mouse/HID pulse 中途不被打断，以及 keyboard->XInput hold takeover 后 tail 仍正常结算的回归；全量 `dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore -verbosity minimal` 最近一次 **361/361** 通过；当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 1.17 cross-device shared-action 语义

- `OmsInputRouter` 不再只按布尔 pressed state 记录 `OmsAction`，而是改为按动作引用计数维护共享按压；这样 keyboard / mouse-axis / HID / XInput 等多个 handler 命中同一 scratch 或同一动作时，不会因为其中一个 source 先释放就把另一个仍处于激活态的 source 一并放掉
- `OmsInputRouterTest` 当前已覆盖 router 重复 press/release 计数语义、keyboard + mouse-axis 共享 scratch 状态、`BmsInputManager` 下 keyboard + XInput 共享 scratch 状态、mouse-axis 同帧反向换向 retrigger，以及 `KeyBindingContainer` 的 mixed-source shared-state 与 mouse-axis bridge 回归；而 keyboard-held + HID-axis / mouse-axis / XInput mixed-source 的 runtime 语义现已额外由 `TestSceneOmsScratchGameplayBridge` 在 loaded hierarchy 内锁定；上述修正均已包含在当前全量 BMS **361/361** 验证内

### 1.5 importer 入口与通知回归

- `BmsImportIntegrationTest` 已新增三类 importer 端到端回归：单个 `.bms` stream task 导入、archive 内重复谱面触发 skipped-file warning、以及无有效 beatmap 的 archive 触发失败 notification。这样 `BmsBeatmapImporter` 自己的入口与通知语义不再只靠 `BmsArchiveReader` / `BmsFolderImporter` 间接覆盖
- `dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter FullyQualifiedName~BmsImportIntegrationTest -verbosity minimal` 最近一次 **16/16** 通过；全量 `dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore -verbosity minimal` 最近一次 **361/361** 通过；当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 1.6 长条 tail release-window 校准

- `BmsJudgementSystem.SetLongNoteReleaseWindows()` 不再对所有 tail release 判定窗口统一乘一个 lenience；release `Perfect` / `Great` / `Good` / `Meh` 现与当前 judge mode 的普通命中窗口保持一致，只有 release `Miss` 仍保留轻微放宽。默认 `OD` 现走 `BmsHoldNote.DEFAULT_RELEASE_MISS_LENIENCE = 1.25`，`BEATORAJA` / `LR2` 则分别使用 `1.2` 的 release miss grace
- `BmsHoldNoteTailEvent.MaximumJudgementOffset`、tail miss-window 检查与 `BmsTimingWindows.WindowFor(..., isLongNoteRelease: true)` 现共用同一套 release-window 数据源；`BmsDrawableRulesetTest` 已补测 `OD` / `BEATORAJA` / `LR2` 三套模式下的 release-window 期望值、精确结果分段、`CN` / `HCN` 的 late-press miss-window 边界，以及 scratch stream repeated press / late-hit ordering 回归；`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter FullyQualifiedName~BmsDrawableRulesetTest -verbosity minimal` 最近一次 **46/46** 通过，`--filter FullyQualifiedName~TestSceneOmsScratchGameplayBridge -verbosity minimal` 最近一次 **14/14** 通过，全量 `dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore -verbosity minimal` 最近一次 **361/361** 通过；当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 1.6 CN/HCN gauge 分母修正

- `BmsGaugeProcessor` 现在会按 beatmap 当前的 nested long-note 结构统计 `TotalHittableObjects`：普通单键与 hold head 继续计入，`CN` / `HCN` 下 `BmsHoldNoteTailEvent` 若仍是 scored tail 也会进入 gauge `BaseRate` 分母，而 `HCN` gauge-only body tick 仍不进入分母；这避免了含大量 scored tail 的谱面在 gauge 变化量上被放大
- 已扩展 `BmsGaugeProcessorTest` 覆盖 LN / CN / HCN 三种模式下的 gauge scaling 与 body tick 回归；`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore -verbosity minimal` 最近一次 **361/361** 通过，当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 1.17 Windows Raw Input 键盘源

- `osu.Desktop` 已新增 `WindowsRawKeyboardSource`，在 Windows 桌面端按 gameplay `Playing` + 窗口激活态启停原生键盘捕获；当前会注册 Raw Input keyboard 设备、子类化游戏窗口接收 `WM_INPUT`，并把原生键位映射为 `InputKey` 后送入新的 `IOmsKeyboardEventSource -> IOmsKeyboardEventSink` 链
- `BmsInputManager` 现已实现 `IOmsKeyboardEventSink` 并在加载/释放时自动向桌面侧 keyboard source 注册/注销；raw keyboard 的 press/release 会复用现有 `OmsKeyboardInputHandler` 语义，disable 时还会通过 `ResetRawKeyboardState()` 主动清理残留按压，避免 gameplay 退出或失焦后卡键
- `dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 最近一次退出码为 0；`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore --verbosity minimal` 最近一次 **287/287** 通过，且 `OmsKeyboardInputHandlerTest` 已新增 raw keyboard sink 入口与 reset 回归；当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 1.17 keyboard gameplay 事件接线

- `BmsInputManager` 现已在 `OnKeyDown()` / `OnKeyUp()` 中优先把属于 OMS binding 的 framework keyboard events 交给 `OmsKeyboardInputHandler`，并在命中时直接消费事件，避免默认 `KeyBindingContainer` 对同一组键盘输入重复触发；现有 replay / 非 OMS 键路径保持不变
- 这让 `TriggerKeyPressed()` / `TriggerKeyReleased()` 不再只是测试或外部注入入口，而成为 live keyboard gameplay 的统一接收面；后续若补 Windows Raw Input，只需把新的键盘事件源送进同一条 OMS keyboard handler 链
- `dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 最近一次退出码为 0；`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore --verbosity minimal` 最近一次 **287/287** 通过，且定向执行 `OmsKeyboardInputHandlerTest` + `OmsInputBridgeTest` 回归最近一次 **24/24** 通过；当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 1.17 通用 keybinding 面板整合

- `osu.Game.Rulesets.Ruleset` 已新增通用 `CreateKeyBindingSections()` 扩展点，`RulesetBindingsSection` 现会在默认 variant keybinding rows 之后挂载 ruleset-specific keybinding panel sections；BMS 侧已通过 `BmsRuleset.CreateKeyBindingSections()` 把现有 `BmsSupplementalBindingSettingsSection` 直接接入 `Input -> Configure -> BMS` 区块，避免 supplemental trigger 只能从 ruleset settings 入口单独维护
- `BmsSupplementalBindingSettingsSection` 现已实现 `IFilterable`，可被 settings search 命中 `supplemental` / `hid` / `mouse` / `trigger` 等关键词；`BmsRulesetConfigurationTest` 也新增断言，确保 BMS ruleset 会稳定暴露该 supplemental keybinding section
- `dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 最近一次退出码为 0；`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore --verbosity minimal` 最近一次 **285/285** 通过，当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 1.17 OMS supplemental trigger mouse-axis live capture

- `oms.Input` 已新增 `OmsMouseAxisCapture`，用于将设置页累计的鼠标位移解析为 `OmsMouseAxis + OmsAxisDirection`；当前采用 dominant-axis 判定并带最小位移阈值，避免点击 `Start capture` 后的轻微抖动被误识别为绑定输入
- `BmsSupplementalBindingSettingsSection` 现已把 live capture 从 HID-only 扩到统一入口：`MouseAxisBindingRow` 也新增了 per-row `Start capture` / `Cancel capture`，capture 期间会直接从包含该设置页的 `InputManager` 读取鼠标位置并累计 delta，成功后自动回填 mouse axis 与 direction，`Invert axis` 则重置为未勾选
- `dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 最近一次退出码为 0；`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter OmsMouseAxisInputHandlerTest` 最近一次 **7/7** 通过；全量 BMS 测试现为 **285/285** 通过，当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 1.17 OMS supplemental trigger HID live capture

- `oms.Input` 已新增 `Devices/OmsHidDeviceCaptureSession`，复用现有 `IOmsHidButtonDeviceProvider` / `IOmsHidButtonDevice` 与 `OmsHidDeviceChange` 结构，提供脱离 gameplay binding 语义的单设备 HID capture 会话；`OmsHidDeviceHandler` 也同步补出默认 provider 工厂，避免设置页重复实现 HidSharp 设备打开逻辑
- `BmsSupplementalBindingSettingsSection` 的 `HidButtonBindingRow` / `HidAxisBindingRow` 现已新增 per-row `Start capture` / `Cancel capture` 按钮：若当前只连接一台 HID 设备会自动回填 device identifier，否则仍可手动指定 device identifier 后开始 capture；成功后会自动回填 button index 或 axis index，并按 axis delta 符号推导方向
- `dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 与 `dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore` 最近一次均退出码为 0；BMS 全量测试现为 **284/284** 通过，当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 1.17 OMS supplemental trigger 编辑 UI

- `oms.Input` 已新增 `Devices/OmsHidDeviceDiscovery`，复用现有 HidSharp 编译别名与 `OmsHidDeviceIdentifier`，在不侵入 gameplay 输入链的前提下为设置页提供当前连接 HID 设备摘要
- `osu.Game.Rulesets.Bms` 已新增 `BmsSupplementalBindingSettingsSection`，并接入 `BmsSettingsSubsection`：当前可按 variant 手动编辑 `HidButton` / `HidAxis` / `MouseAxis` supplemental bindings，支持刷新设备列表、重载当前 variant、应用保存与清空当前 variant，运行时仍通过 `OmsBmsBindingSettingsStorage` + `OmsBmsBindingResolver` 合并回现有 OMS 输入链
- `dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 退出码为 0；新增编译告警已清零，当前仍仅剩既有 `AutoMapper` `NU1903`；针对输入桥接回归的 `OmsInputBridgeTest` 最近一次 **20/20** 通过

### 1.17 OMS supplemental trigger 持久化基础

- `OmsBmsBindingResolver` 现仅在当前 variant 没有任何持久化 `RealmKeyBinding` 时才回退到默认绑定；如果数据库里已有 BMS 绑定行但无一可转换为 OMS binding，则不再静默重新激活默认键位，避免已保存的非常规绑定被默认值重新带回
- 已新增 `OmsBmsBindingSettingsStorage`，把通用 `RealmKeyBinding` 无法表达的 `HidButton` / `HidAxis` / `MouseAxis` trigger 以 `RealmRulesetSetting` 的 ruleset+variant scoped JSON 持久化；`OmsBmsBindingResolver` 现会把这部分 supplemental OMS bindings 与标准 keyboard/joystick keybindings 合并返回
- 已扩展 `OmsInputBridgeTest` 覆盖"persisted but unconvertible 不再 fallback 默认值""unsupported trigger round-trip""standard + supplemental merge"三类回归；`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore` 退出码为 0，**277/277** 通过

### 1.17 XInput 默认绑定与持久化回环

- `OmsBindingStore` 现为 5K / 7K profile 补出默认 XInput button 绑定，并新增 `buttonIndex <-> InputKey.JoystickN` 转换辅助；`Devices/OmsXInputButtonInputHandler` 继续按 `buttonIndex` 解析 XInput/joystick button press/release，并保持共享 action 的引用计数 release 语义
- `BmsRuleset.GetDefaultKeyBindings()` 现会把默认 XInput button 一并导出为 ruleset keybindings；`OmsBmsBindingResolver` 也已能把持久化的 joystick-only `RealmKeyBinding` 还原为 OMS `XInputButton` trigger，因此现有 keybinding UI 记录到的 joystick button 可以回到 OMS 默认/持久化链路
- 已补 `BindingSettings` 搜索词 `joystick` / `gamepad` / `controller` / `xinput`，并确认当前通用 keybinding UI 复用 `KeyBindingRow.OnJoystickPress()` 路径承载 joystick button 的默认展示与录入，因此当前不再单列独立 XInput 绑定 UI 为剩余缺口
- 已扩展 `OmsXInputButtonInputHandlerTest`、`OmsInputBridgeTest` 与 `BmsRulesetModTest` 覆盖默认 XInput 绑定、持久化 joystick 解析与 ruleset 默认绑定计数；`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore` 退出码为 0，**277/277** 通过

## 2026-04-04

### 1.17 MouseAxis delta 链路接通

- `OmsBinding` 现已补出 `MouseAxisTriggers`，`oms.Input` 新增 `Devices/OmsMouseAxisInputHandler`：按 `axis + direction + inverted` 解析每帧 mouse delta，并沿用现有 axis handler 的共享 action 引用计数语义，把鼠标位移折叠为 pressed/released `OmsAction`
- `BmsInputManager` 现新增 `TriggerMouseAxisDelta()` 入口，并在 `OnMouseMove(MouseMoveEvent e)` 内把 X/Y 方向 delta 直接送入 OMS router；该链路不会修改默认键位，只为显式 mouse-axis 绑定提供最小 backend
- 已新增 `OmsMouseAxisInputHandlerTest`，覆盖方向命中、方向翻转不断链与 axisInverted 语义；`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore` 退出码为 0，**264/264** 通过

### 1.17 HidSharp 轴 delta 链路接通

- `OmsBinding` 现已补出 `HidAxisTriggers`，`oms.Input` 新增 `Devices/OmsHidAxisInputHandler`：按 `deviceIdentifier + axisIndex + direction + inverted` 解析每轮 polling 的 axis delta，并以 pressed/released 语义把轴运动折叠回现有 `OmsAction` 路由，不额外引入新的 gameplay 输入面
- `OmsHidDeviceHandler` 现将 HidSharp 轮询从"仅数字按钮"扩到统一 `OmsHidDeviceChange` 按钮/轴变化流：可读取 relative/absolute axis logical 值、为 absolute axis 计算 delta，并在设备断开时同时 release 仍处于活动态的 button / axis action；`BmsInputManager.Update()` 继续通过 `PollOnce()` 接入该链路
- 已新增 `OmsHidAxisInputHandlerTest`，并扩展 `OmsHidDeviceHandlerTest` 覆盖 axis 方向翻转、共享 action 保持按下、空闲自动 release、断开自动 release 与未绑定设备忽略；`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore` 退出码为 0，**261/261** 通过

### 1.17 HidSharp 数字按钮设备轮询链路接通

- `oms.Input` 现已新增 `Devices/OmsHidDeviceHandler`、`OmsHidDeviceIdentifier` 与可注入的 `IOmsHidButtonDeviceProvider` / `IOmsHidButtonDevice` 抽象：运行时会按绑定中的 `deviceIdentifier` 枚举 HidSharp 设备、解析数字按钮 input report，并在设备移除时自动释放仍处于按下状态的按钮，避免断开控制器后 action 卡住
- `oms.Input.csproj` 现为 `HidSharp` 增加编译别名，规避 `OpenTabletDriver` 链路引入的 `HidSharpCore` 同名类型冲突；`OmsHidButtonInputHandler` 也已统一标准化 `deviceIdentifier`，避免大小写/空白导致绑定不命中
- `osu.Game.Rulesets.Bms` 的 `BmsInputManager` 现统一通过 `applyBindings()` 初始化 keyboard / HID button / HidSharp device handlers，并在 `Update()` 内持续 `PollOnce()`；已新增 `OmsHidDeviceHandlerTest`，覆盖队列按钮变化、断开设备自动 release 与未绑定设备忽略，`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore` 退出码为 0，**256/256** 通过

### 1.17 HID 数字按钮链路接通

- `oms.Input` 现已新增 `Devices/OmsHidButtonInputHandler`，并在 `OmsBinding` 上补出 `HidButtonTriggers` 入口：同一 `OmsAction` 可绑定多个 HID 按钮，只有最后一个活动按钮释放时才会真正触发 release，避免 scratch / 面板多备用按钮提前松键
- `osu.Game.Rulesets.Bms` 的 `BmsInputManager` 现会同步初始化 keyboard + HID button handlers，并新增 `TriggerHidButtonPressed()` / `TriggerHidButtonReleased()` 注入入口；后续 HidSharp 设备枚举/轮询或外部硬件适配层已经可以直接走 `OmsAction -> BmsAction` bridge
- 已新增 `OmsHidButtonInputHandlerTest`，覆盖同 action 多按钮引用计数、跨设备按钮隔离，以及通过 `TriggerOmsActionPressed()` / `TriggerOmsActionReleased()` 驱动 router 的 HID 注入断言；`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore` 退出码为 0，**253/253** 通过

### 1.17 键盘后端与组合键语义收口

- `oms.Input` 现已把键盘触发模型提升为 `OmsBindingTrigger`：一个 `OmsBinding` 现在可携带完整键盘 `KeyCombination` 以及为后续 HID / Mouse Axis / XInput 预留的设备触发类型，不再把"多个键"强行解释成同一层级的备用单键
- `Devices/OmsKeyboardInputHandler` 现按完整 `KeyCombination` 解析 press/release：既保持 scratch 默认 `Q/A` 这类备用单键引用计数语义，也修正了 `Ctrl+Key` 这类组合键不会再被误当成两个可独立触发的单键
- `osu.Game.Rulesets.Bms` 的 `OmsBmsBindingResolver` 现会保留 `RealmKeyBinding.KeyCombination` 的完整组合语义，而不是把数据库里的组合键拆成多个备用键；`BmsRuleset.GetDefaultKeyBindings()` 也已改为按 `OmsBinding` 中的完整 keyboard combinations 回吐默认绑定
- 已扩展 `OmsKeyboardInputHandlerTest`，新增组合键必须完整按下才触发的断言；`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore` 退出码为 0，**250/250** 通过

### 1.17 最小正式输入链路接通

- `oms.Input` 已从纯空壳推进到最小正式链路：`OmsBindingStore` 现提供 5K / 7K / 9K / 14K 的默认 profile 绑定源，`OmsInputRouter` 现提供按 `OmsAction` 路由的 pressed/released 状态与事件骨架
- `osu.Game.Rulesets.Bms` 现新增 `OmsBmsActionMap`，将 `OmsAction` 与现有 `BmsAction` gameplay 面做 variant-aware 双向桥接；`BmsRuleset.GetDefaultKeyBindings()` 也已改为从 `OmsBindingStore` 生成默认键位，而不再把键位硬编码在 ruleset 内部
- `BmsInputManager` 现新增最小 router bridge：一方面会把现有 `BmsAction` 输入镜像到 `OmsInputRouter`，另一方面也提供 `TriggerOmsActionPressed()` / `TriggerOmsActionReleased()` 入口，允许后续 Raw Input / HID / XInput backend 直接走 `OmsAction -> BmsAction` 注入路径
- 已新增 `OmsInputBridgeTest` 覆盖 profile 绑定数、`OmsAction -> BmsAction` 映射、`LaneCoverFocus` 的 router 入口，以及 ruleset 默认 scratch 绑定保持不变；`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore` 退出码为 0，**246/246** 通过

### long-note release-window 语义收束

- 已将 long-note tail release-window 从 `DrawableBmsHoldNote` / `BmsHoldNoteTailEvent` 的外部 `RELEASE_WINDOW_LENIENCE` 推导，收束为 `BmsJudgementSystem.LongNoteReleaseWindows` / `BmsTimingWindows.WindowFor(..., isLongNoteRelease: true)` 的正式判定接口
- `OsuOdJudgementSystem`、`BeatorajaJudgementSystem`、`Lr2JudgementSystem` 现都会显式生成 long-note release windows；`DrawableBmsHoldNote` 的 miss-window 判断、tail release 判定与 `BmsHoldNoteTailEvent.MaximumJudgementOffset` 现统一走这套 release-window API，而不再各自重复乘除 lenience
- 已更新 `BmsDrawableRulesetTest`，把 tail release 相关断言改为直接校验 `BmsTimingWindows` 的 long-note release windows，并补 judge mode 下 tail release window 放宽断言
- `dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore` 退出码为 0，**246/246** 通过；构建期仍仅残留 `AutoMapper` `NU1903` 告警

### results auto-jump 最终修复

- `dotnet build osu.Desktop` 退出码为 0，编译器诊断保持 2 个（仅 `AutoMapper` `NU1903`）
- `dotnet test osu.Game.Rulesets.Bms.Tests` 退出码为 0，**235/235** 通过（较 04-03 新增 2 个 hold note completion 测试）
- **Results auto-jump 根因最终修复并经实机验证**：日志诊断发现 `JudgedHits=1952` 远超 `MaxHits=1192`，差值 760 恰好等于游玩过程中动态注入的 `BmsEmptyPoorHitObject` 数量。`BmsEmptyPoorHitObject` 继承自 `HitObject`（非 `BmsHitObject`），原先的黑名单过滤 `is not BmsEmptyPoorHitObject` 在运行时未能正确排除这些动态创建的对象，导致 `JudgedHits != MaxHits` → `HasCompleted` 永远为 false → 不跳结算。修复：将 `BmsScoreProcessor.CountsResultTowardsJudgedHits` 从黑名单改为白名单 `result.HitObject is BmsHitObject and not BmsHoldNote`，仅允许继承自 `BmsHitObject` 的对象（单键音符、长键头、长键尾）计入 `JudgedHits`，自动排除所有非 `BmsHitObject` 类型（`BmsEmptyPoorHitObject`、`BmsBgmEvent`、`BmsHoldNoteBodyTick`）
- 同步简化了 `BmsGaugeProcessor.CountsResultTowardsJudgedHits`，移除多余的 `base.CountsResultTowardsJudgedHits(result)` 调用
- 为 `BmsScoreProcessorTest` 新增 `TestHoldNoteCompletionReachesTrueInLnMode` 和 `TestMixedBeatmapCompletionReachesTrue` 两个 completion 验证测试
- 保留了 `BmsScoreProcessor` 中的诊断日志（`[BMS] ApplyBeatmap` / `[BMS] COMPLETED OK` / `[BMS] COMPLETION STUCK`），方便后续排查

## 2026-04-03

### 遗留问题修复 + 全局审计

- `dotnet build osu.Desktop` 退出码为 0，编译器诊断从 10 个降至 2 个（仅剩 `AutoMapper` `NU1903`）；`dotnet test osu.Game.Rulesets.Bms.Tests` `361/361` 通过
- **Results auto-jump 根因修复**：`BmsHoldNoteBodyTick` 导致 `JudgedHits < MaxHits` → 修复 `resolveTail` 强制 judge 剩余 body tick + `CountsResultTowardsJudgedHits` 排除 body tick / hold parent
- **代码质量修复**：`WorkingBeatmapCache` nullable（7 处 `string?`→`string`）、`RealmAccess` nullable（`.Select(path => path!)`)、slnf 15→7 项目、`AutoMapper` CVE `.MaxDepth(3)` 缓解
- **全局代码审计**：覆盖 BMS ~ 96 源文件、osu.Game ~25 被修改文件、oms.Input；无桩代码/TODO/硬编码凭据；`EndpointConfiguration` 已确认清空；上游 cherry-pick 高风险区：`BeatmapCarousel`/`FilterControl`/`WorkingBeatmapCache`/`BeatmapManager`

## 2026-04-02

### completion 收紧 + 通道修正 + smoke test

- `dotnet build osu.Desktop` 通过；`dotnet test osu.Game.Rulesets.Bms.Tests` `361/361` 通过
- 新增 `SmokeTestDesktop.ps1` 8 秒非交互启动验证
- 修正 BMS SP/BME 通道语义（`16` scratch、`17` free zone 跳过）、`SliderMultiplier=1` + `RelativeScaleBeatLengths=true`
- 收紧 completion 边界（`<=`）、`HasCompleted` 单调语义、`BmsBgmEvent` 排除出 judged-hit 统计
- 修正无音频 BMS 虚拟轨长度（`BeatmapInfo.Length = GetLastObjectTime()`）

## 2026-04-01

### 离线化 + gauge/clear lamp/Empty Poor 接通

- `dotnet build osu.Desktop` 通过；`dotnet test osu.Game.Rulesets.Bms.Tests` `176/176` 通过
- 默认离线模式全面接通：endpoint 清空、`LocalOfflineAPIAccess` 装配、主菜单/Toolbar/Song Select/OsuGame 在线入口按 `OnlineFeaturesEnabled` 隐藏、`LargeTextureStore`/`PreviewTrackManager`/metadata cache 离线退化、First-run Setup 下载禁用、profile 静态资源本地占位
- Empty Poor（`BmsEmptyPoorHitObject` + `ComboBreak`）+ gauge 伤害 + combo 断裂 + 结果页计数
- `BmsGaugeBar` HUD + `BmsModGaugeAutoShift` / `BmsGasGaugeProcessor` GAS 降级链
- `BmsGaugeHistoryGraph` 结果页 gauge history 重放（单层 + GAS 多层）
- 仓库清理：上游副本移除、`.github` 目录移除
