# OMS 开发进度与遗留问题

> 最后更新：2026-04-09
> 本文档只记录“仓库里已经真实存在的状态”，不重复规划全文。
> 详细分步规划见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，权威技术约束见 [OMS_COPILOT.md](OMS_COPILOT.md)。

## 状态定义

| 状态 | 含义 |
| --- | --- |
| 已完成 | 代码已落地，且至少通过一次构建、测试或明确手动验证 |
| 进行中 | 已有实际实现，但功能尚未满足该步骤验收标准 |
| 仅骨架 | 项目、类或入口已创建，但核心逻辑尚未接入 |
| 未开始 | 仓库中尚无对应实现 |
| 阻塞 | 当前步骤依赖尚未满足，暂时不能有效推进 |

## 最新快照

- 当前阶段：**Phase 1.1 皮肤系统专项执行中**（执行顺序已收敛为“先 BMS playfield abstraction，再 BMS 默认层，再 mania OMS-owned 迁移”）
- 当前主入口：`osu.Desktop.slnf`（含 7 个项目）
- 当前仓库定位：Windows-only，保留 `osu!mania`，新增 BMS，已移除 Osu/Taiko/Catch
- 当前 BMS 规则集规模：**124 个源文件，12213 行实现代码**；`oms.Input` **14 个源文件，1753 行实现代码**；36 个测试文件，6785 行测试代码
- 当前已落地主链：BMS 解码、转换、自定义导入、OMS `songs/` 目录直读、运行时 loader、共享 keysound 池、7K+1 最小 gameplay、三套 judge mode（OD / BEATORAJA / LR2）、六种 gauge type + GAS、EX-SCORE / CLEAR LAMP / DJ LEVEL、CN / HCN mode-aware 计分、本地 best/replay/排行榜 judge + long-note mode 分桶、离线难度表缓存 / MD5 匹配 / 表分组、Song Select 音符分布图，以及 **gameplay → results 自动跳转闭环**
- 当前皮肤基线：BMS 默认层已完成七批 OMS-owned 默认层切片；运行时 `OmsSkin` preview host / provider / resource root、共享 `OmsSkinTransformer` 外壳、显式 `ManiaOmsSkinTransformer` 入口、首批 mania shell 组件、首批 stage-local layout preset、首批 stage-local shell behaviour preset、首批 shared shell asset preset、首批 shell colour preset、首批 stage-local key-asset preset，以及 mania 第二批的首个 stage-local note/hold asset preset、首个 explicit note component slice、首个 shared judgement asset preset、首个 shared judgement-position slice、首个 shared bar-line config slice、首个 explicit judgement piece slice、首个 explicit bar-line component slice、首个 explicit combo counter component slice、首个 stage-local hitburst config preset与首个 explicit hitburst component slice 也已落地；已验证 5K `Stage` 宿主实际加载、5K+5K dual-stage repeated layout、mixed-stage shell behaviour / shell colour / key-image / note-hold / hitburst config 分流、shared judgement asset 持续共享，且 shared judgement score / combo-position、shared bar-line height / colour 与现有其它 non-column shared lookup 现已在 same-keycount dual-stage 与 mixed-stage 路径下稳定收口：mixed-stage 会固定复用第一 stage 的 OMS preset，不再落回 total-columns legacy 默认值；`GlobalSkinnableContainerLookup` 的 global HUD / `SongSelect` / `Playfield` 缺省 shell 现也可经 OMS preview 路径返回空 `DefaultSkinComponentsContainer`，并由 Mania / BMS ruleset transformer 继续承接各自 gameplay 语义；嵌入 `MainHUDComponents.json` / `SongSelect.json` / `Playfield.json` 的 global layout metadata 现也已由 `TestSceneOmsBuiltInSkin` regression 锁定，results-style shared panel shell 现也已通过 `DefaultResultsPanelContainer` + `DefaultResultsPanelDisplay<TState>` 收口为 core stateful contract。产品默认值仍未切离 Argon，这些路径也仍主要消费 legacy-derived candidate assets 与配置语义；当前自动推进已继续转向 mania 第二批的 actual OMS-owned 迁移，其中 `OmsNotePiece` / `OmsHoldNoteHeadPiece` / `OmsHoldNoteTailPiece` / `OmsHoldNoteBodyPiece` / `OmsManiaJudgementPiece` / `OmsHitExplosion` / `OmsManiaComboCounter` / `OmsBarLine` 已分别升级为实际 OMS-owned 组件，并已由 `OmsOwnedSkinComponentContractTest` + `TestSceneOmsBuiltInSkin` 锁定；score-driven results 是否需要独立 preview/skinnable target 仍待后续决定，mania 侧剩余重点收窄为 note / hold 与 combo/HUD / bar-line 的余下 legacy 语义清理。
- 当前 BMS abstraction gate 落点：已新增共享 `BmsPlayfieldLayoutProfile`，把 `BmsLaneLayout`、`BmsPlayfield`、`BmsHitTarget` 与 `DrawableBmsBarLine` 的默认几何参数收口到同一 profile；`DrawableBmsRuleset` 也已切入专用 `BmsPlayfieldAdjustmentContainer`，并接通 `Playfield Scale` / `Playfield Horizontal Offset` 两个 ruleset 配置项；`BmsHitTarget` 已有 `pressed / focused` 正式状态契约，`BmsLane` 也会从输入管理器同步 receptor pressed state；lane width / lane spacing / scratch width ratio / scratch spacing / playfield width / playfield height / hit target height / hit target bar height / hit target line height / hit target glow radius / hit target vertical offset / bar line height 现都已正式接入 ruleset config、`BmsPlayfieldLayoutProfile`、`BmsLaneLayout` 与 loaded `BmsPlayfield` 的 runtime 重布局链路，新的 `BmsHitObjectArea` 也会把 scrolling container 与 receptor 一起下沉到真实 hit line，并把有效 scroll length ratio 回传给 `DrawableBmsRuleset` 以保持 scroll-speed 语义；BMS abstraction gate 的 layout/config contract 已基本闭合，当前下一主焦点转向 OMS 默认皮肤包宿主 / shared shell 与 mania OMS-owned 迁移
- 当前自动推进优先主线：**Phase 1.1 皮肤系统专项** 当前已明确按“共享骨架 / 文档门槛 → BMS playfield abstraction gate → BMS 默认层 → mania OMS-owned 迁移 → partial override / native default removal / release gate”推进；其中 BMS playfield abstraction gate、BMS 默认层、OMS shared shell / shared transformer shell，以及 global layout metadata 都已完成首轮收口，results-style shared panel shell 也已进一步收口为 `DefaultResultsPanelDisplay<TState>` core contract；当前自动推进已继续转向 mania OMS-owned 迁移，并优先处理已有 transformer 路径上的 actual component migration；score-driven results 是否需要独立 preview/skinnable target 暂留后续再评估；1.17 analog scratch、1.6 真实谱面长条验校与 1.5 导入 UI 人工验收暂退居次优先级
- 需要人工操作的真实 UI / 发行物验收已独立收束到本文后文“待人工操作验收（统一后置）”板块；默认放在 Phase 1 阶段末尾统一执行，仅在其成为当前阻塞项时再提前请求用户介入
- 当前已知主断点：无阻断性崩溃；Windows 下 HID 设备加载根因已收敛到 HidSharp `RegisterClass failed` 初始化链，当前通过“Windows 默认禁用 HidSharp、设置页提示说明、仅 `OMS_ENABLE_HIDSHARP=1` 显式开启”规避；results auto-jump 已修复并经实机验证

## 开发指标

| 指标 | 当前值 | 说明 |
| --- | --- | --- |
| Phase 1 完成率 | `70.6%` (`12/17`) | 仅按 Phase 1 步骤中标记为"已完成"的项计算 |
| Phase 1 加权进度 | `85.3%` (`14.5/17`) | 按 `已完成=1`、`进行中=0.5`、`仅骨架=0.25`、`未开始/阻塞=0` 计算 |
| Phase 1.1 皮肤系统专项 | 进行中，BMS 默认层已完成、宿主骨架与 OMS shared shell 已接通，mania 第一批 shell 切片及第二批 note/hold asset + explicit note component + explicit hold-note-head component + explicit hold-note-tail component + explicit hold-note-body component + judgement asset + shared judgement-position + bar-line config + judgement piece + bar-line component + combo counter component + hitburst config + hitburst component 首切片已起 | 当前已完成规范与主线切换文档化，落地 BMS 第一批、第二批、第三批与默认层收口，并新增 `OmsSkin` host / provider / resource root 首个运行时骨架、共享 `OmsSkinTransformer` 外壳、mania shell 首批 OMS 组件、首批 stage-local layout preset、首批 stage-local shell behaviour preset、首批 shared shell asset preset、首批 shell colour preset、首批 stage-local key-asset preset，以及 mania 第二批的首个 stage-local note/hold asset preset、首个 explicit note component slice、首个 explicit hold-note-head component slice、首个 explicit hold-note-tail component slice、首个 explicit hold-note-body component slice、首个 shared judgement asset preset、首个 shared judgement-position slice、首个 shared bar-line config slice、首个 explicit judgement piece slice、首个 explicit bar-line component slice、首个 explicit combo counter component slice、首个 stage-local hitburst config preset与首个 explicit hitburst component slice，并已补齐现有 mania non-column shared config 的 mixed-stage shared-transformer fallback 以及 global HUD / `SongSelect` / `Playfield` shell；不计入既有 `12/17` 核心 BMS 进度 |
| 桌面端构建验证 | 通过 | `2026-04-08` 最近一次 `dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 退出码为 0 |
| BMS 单测验证 | 通过 | `2026-04-08` 最近一次全量 `dotnet test osu.Game.Rulesets.Bms.Tests.csproj` **446/446** 通过；同日直接受影响过滤回归 **69/69** 通过 |
| 关键回归测试 | 通过 | `2026-04-09` 最近一次 BMS HID handler 定向回归（`OmsHidDeviceHandlerTest`）**11/11** 通过；同日 Mania 定向回归（`OmsOwnedSkinComponentContractTest` + `TestSceneOmsBuiltInSkin`）**50/50** 通过；最近一次 results-panel contract 受影响 BMS 过滤回归 **69/69** 通过 |
| 启动手工 smoke test | 自动通过 / 手工部分已补 | `2026-04-02` 已用 `SmokeTestDesktop.ps1` 对桌面端完成 8 秒非交互启动验证；`2026-04-09` 已确认 Release 启动不再因 HidSharp `RegisterClass failed` 闪退，点击设置会显示 Windows 默认禁用 HID 的说明提示；真实导入 / Song Select UI 验收仍需用户操作 |
| BMS 可玩状态 | 可启动，可游玩，可结算 | gameplay → results 闭环已实机验证；键盘 / Raw Input / XInput / MouseAxis 主链可用，HID 代码路径已接通但 Windows 下因 HidSharp 初始化崩溃风险默认禁用并在设置页提示说明；剩余 analog scratch 语义、cross-device 终态输入链与稳定 HID backend 收口 |
| 编译器诊断残留 | 0 个告警 | `AutoMapper` GHSA `rvv3-g6hj-g44x` 已在 `osu.Game.csproj` 通过定点 `NuGetAuditSuppress` 压制；`RealmObjectExtensions` 的循环图路径仍由 `MaxDepth(3)` 限深，升级到 15.1.1+ 或移除继续跟踪；`BmsScoreProcessor` 诊断日志已包裹 `#if DEBUG`，Release 构建不再输出 |

## 最近一次验证

> 完整验证历史已迁移至 [CHANGELOG.md](CHANGELOG.md)，本节仅保留最新快照。

### 2026-04-09

- 新增 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsHidDeviceHandlerTest"` **11/11** 通过；`dotnet build .\osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过，并完成一次 Release 启动 smoke 验证，未再出现 HidSharp `RegisterClass failed` 即时闪退
- 主要变更：Windows 下 `OmsHidSharpRuntime` 已改成默认不触发 HidSharp，只有显式设置 `OMS_ENABLE_HIDSHARP=1` 时才会继续初始化 HID backend；设置页当前看到的 HID-disabled 提示属于预期防崩溃降级，说明问题已收敛到待修复的 Windows HID 设备加载后端，而不是设置/皮肤 overlay 本身卡死
- 新增 `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin"` **50/50** 通过；最近一次 `dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 退出码仍为 0
- 最近一次 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~ResultsPanelDisplayContractTest|FullyQualifiedName~StatisticItemContainerTest|FullyQualifiedName~BmsRulesetStatisticsTest|FullyQualifiedName~BmsSkinTransformerTest"` **69/69** 通过；最近一次完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **446/446** 通过
- `AutoMapper` GHSA `rvv3-g6hj-g44x` 已通过 `NuGetAuditSuppress` 定点压制；当前 `dotnet build` 不再输出既有 `NU1903` 告警，但升级到 15.1.1+ 或移除仍继续跟踪
- 主要变更：`OmsHoldNoteBodyPiece` 现已改为真实 `OmsManiaColumnElement` 实现，不再回退为 `LegacyBodyPiece` 的 thin wrapper；配套 `OmsOwnedSkinComponentContractTest` 已扩到 note / hold-head / hold-tail / hold-body / judgement / hit explosion / combo counter / bar line 八类组件，`TestSceneOmsBuiltInSkin` 也已新增 hold-body scrolling-direction 行为回归。当前自动推进已继续转向 mania 第二批里最便宜的 actual OMS-owned component migration；score-driven results 是否需要独立 preview/skinnable target 暂留后续再评估，mania 侧剩余重点收窄为 note/hold / combo/HUD / bar-line 的余下 legacy 语义清理
- 详细条目见 [CHANGELOG.md](CHANGELOG.md)

## 当前版本发行与联网约束

| 项目 | 状态 | 当前仓库现状 | 与目标差距 |
| --- | --- | --- | --- |
| 便携版发布基线 | 已文档化 | 已新增 `RELEASE.md`，文档化了 `dotnet publish` 构建命令、发行物内容、用户数据存储路径、覆盖更新流程与在线状态；Velopack 已完全早退，程序目录与用户数据目录分离 | 仍需实机运行验证（待人工操作验收） |
| 文件覆盖更新策略 | 已文档化 | `RELEASE.md` 已说明解压覆盖流程，用户数据在 `%APPDATA%/oms/` 不受影响 | 仍需实机验证（待人工操作验收） |
| 游戏内在线更新与入口隐藏 | 已完成 | 桌面端已新增 `IsInAppUpdateEnabled = false`，`Program.setupVelopack()` 已跳过 Velopack 初始化，`CreateUpdateManager()` 已切回基础 `UpdateManager()`，设置页不再显示 `UpdateSettings`；已经审计确认 `IsFirstRun` 永迎 `false`，所有更新链路均不可达 | 无 |
| 默认在线 endpoint 清理 | 已完成 | `ProductionEndpointConfiguration` / `DevelopmentEndpointConfiguration` 已清空默认 URL，`OsuGameBase` 默认装配 `LocalOfflineAPIAccess`，多人 / 观战 / metadata client 不再获得 hub connector，`BeatmapManager` startup online lookup 已关闭 | 无 |
| 游戏内联网入口隐藏 | 已完成 | 更新入口、Toolbar / 主菜单 / Song Select / overlay / 编辑器外链 / First-run Setup 下载入口均已按 `OnlineFeaturesEnabled` / 空 `WebsiteUrl` 收口；Report Issue 按钮与 Discord Rich Presence 也已按 `OnlineFeaturesEnabled` 守卫 | 无 |
| 上游静态资源 fallback 清理 | 已完成 | 离线模式下 `LargeTextureStore` / `PreviewTrackManager` / `LocalCachedBeatmapMetadataSource` / `BackgroundDataStoreProcessor` 的在线源均已关闭；profile 静态资源已补本地占位；`Medal.ImageUrl` 等属性虽残留硬编码 ppy.sh URL，但对应 overlay 已阻断，运行时不可达 | 无（残留死代码可在 Phase 3 统一清理） |
| BMS 原样目录存储 | 已完成 | `BmsFolderImporter` 已把导入内容写入 `songs/` 目录，数据库已记录 `FilesystemStoragePath` / `LocalFilePath`，`WorkingBeatmapCache` 已可从文件系统型 beatmap set 回读 BMS 文件与关联资源，且 skipped-file 警告已接通 | 无 |

## 已落地能力

> 本节仅列出已落地能力的高层摘要，各步骤的详细实现状态见 Phase 1 进度矩阵。

- 上游裁剪与项目基础收口，主入口以桌面端为准
- BMS 解码、转换、自定义导入、OMS songs/ 目录直读、运行时 loader、共享 keysound 池
- 7K+1 最小 gameplay、三套 judge mode、六种 gauge type + GAS、EX-SCORE / CLEAR LAMP / DJ LEVEL
- LN / CN / HCN mode-aware 计分、本地 best/replay/排行榜 judge + long-note mode 分桶
- 离线难度表缓存 / MD5 匹配 / 表分组、Song Select 音符分布图
- oms.Input 多源输入（键盘组合键 / XInput / MouseAxis / Raw Input 主链可用，HID 代码路径与 supplemental trigger 编辑 UI 已落地但 Windows 默认禁用 HidSharp）、supplemental trigger 编辑 UI、通用 keybinding 面板整合
- gameplay → results 自动跳转闭环
- BMS 最小皮肤链路：ruleset transformer、judgement naming，以及 `Playfield / Lane / HitTarget / BarLine / Static BG / Note / Hold / LaneCover / Judgement / Combo / GaugeBar / GaugeHistory / NoteDistribution / ClearLamp / ResultsSummary` 的 lookup 接线

## Phase 1 进度矩阵

| 步骤 | 状态 | 当前仓库现状 | 与验收标准的差距 |
| --- | --- | --- | --- |
| 1.1 上游清理与项目脚手架 | 已完成 | Osu/Taiko/Catch 已移除；osu.Game.Rulesets.Bms 与 oms.Input 已创建 | 无 |
| 1.2 BMS 数据模型 | 已完成 | BmsBeatmapInfo、BmsHitObject、BmsHoldNote、BmsBgmEvent、BmsKeymode 等已落地 | 无 |
| 1.3 BMS 文件解析器 | 已完成 | BmsBeatmapDecoder 覆盖头字段、通道、LN、条件分支与键模式语义 | 无 |
| 1.4 谱面转换器 | 已完成 | BmsDecodedBeatmap → BmsBeatmap → BmsBeatmapConverter 全链路已落地 | 无 |
| 1.5 归档导入 | 进行中 | .zip/.rar/.7z/.bms/.bme/.bml/.pms 导入已注册；OMS songs/ 目录直读、skipped-file 告警、[5K]/[7K]/[9K]/[14K] 标签已落地 | 仅剩桌面端拖放导入真实 UI 手工验收 |
| 1.6 键音系统 | 进行中 | 共享 keysound 池、LN/CN/HCN nested head/tail/body-tick 结构、release-window 对齐（miss-only 放宽）、CN/HCN gauge 分母修正均已落地 | 仍缺基于真实谱面的长条边界人工验校 |
| 1.7 BMS 规则集入口 | 进行中 | BmsRuleset 全部 Create*() 已接入；mod 列表、OMS 多源输入 bridge、score/gauge/results 主链已接通 | 缺 replay 边角与更完整 gameplay HUD |
| 1.8 7K+1 Playfield | 进行中 | lane layout、scratch lane、bar line、hit target、background layer、lane cover、drawable note/hold 已接通；BMS timing windows + 判定显示已切入 | 缺真实车道样式、背景图加载、皮肤化 drawable；相关收口现并入 Phase 1.1 皮肤系统专项 |
| 1.9 OD 判定系统 | 已完成 | 三套 judge mode（OD / BEATORAJA / LR2）窗口已落地；POOR window 独立、held-past-tail late-release、CN/HCN re-hold 语义已收口 | 无 |
| 1.10 Normal Gauge | 已完成 | 六种 gauge type + GAS 降级链；CN/HCN scored tail 计入分母、Empty Poor 注入、BmsGaugeBar HUD 已接通 | 无 |
| 1.11 EX-SCORE 与结算 | 已完成 | EX-SCORE / DJ LEVEL / CLEAR LAMP / gauge history / RulesetData 持久化 / 本地 leaderboard judge+LN mode 分桶已落地；results auto-jump 已实机验证 | 私服提交与远端排行榜归 Phase 3 |
| 1.12 密度星级 | 已完成 | 1000ms/500ms 滑动窗口 + 和弦/scratch/LN 权重 + 95th 百分位映射 0–20 星 | 无 |
| 1.13 本地难度表预置与缓存 | 已完成 | sqlite 缓存 + preset subscriptions + settings 管理 UI（导入/浏览/刷新/启停/移除） | 无 |
| 1.14 MD5 匹配管线 | 已完成 | 导入时按谱面 MD5 自动匹配表缓存，表刷新后重建索引并回写 metadata | 无 |
| 1.15 Song Select 表分组 | 已完成 | 表名 → 等级 → BeatmapSet → 难度层级分组，Unrated 末尾，激活时锁定密度星级升序 | 无 |
| 1.16 音符分布图 | 已完成 | BmsNoteDistributionGraph 通过 ruleset details 扩展点挂入 Song Select 右侧 | 无 |
| 1.17 基础输入绑定与 Lane Cover | 进行中 | oms.Input 键盘组合键 / MouseAxis / XInput button / Windows Raw Input 主链已接通；HID button+axis 代码路径、supplemental trigger 持久化与编辑 UI（含 live capture）、通用 keybinding 面板整合已落地，但 Windows 下 HidSharp `RegisterClass failed` 可导致进程级崩溃，因此当前默认禁用 HidSharp、仅允许 `OMS_ENABLE_HIDSHARP=1` 显式开启诊断后端；OmsInputRouter shared-action 引用计数、axis pulse 语义、mixed-source scratch 回归已补测 | 更丰富的 analog scratch 专用语义、cross-device 终态输入链与稳定 HID backend |

## Phase 1.1 皮肤系统专项（当前主线）

| 项目 | 当前状态 | 说明 |
| --- | --- | --- |
| 默认皮肤包结构 | 已澄清 | 目标是一个默认皮肤选择项内集成 `Global + Mania + BMS` 三层；mania 与 BMS 的 gameplay 皮肤语义彼此独立 |
| 当前候选包基线（`SimpleTou-Lazer`） | 进行中 | 已完成 legacy mania 侧资源清理、瘦身与兼容化，可作为 OMS 内置皮肤候选基础；当前不能被视为“已完成的 OMS 默认皮肤” |
| Global provider / shared shell | 进行中 | 受保护的 `OmsSkin` preview 选择项、`SkinManager` 注册 / 枚举 / 配置入口、嵌入 `SKIN/SimpleTou-Lazer` 的 built-in resource root，以及共享 `OmsSkinTransformer` 已落地；global HUD / `SongSelect` / `Playfield` 的缺省 shell 现可经 OMS preview 路径返回空 `DefaultSkinComponentsContainer`，mania 现有 non-column shared preset 的 mixed-stage fallback 也已在 ruleset transformer 层收口；嵌入 `MainHUDComponents.json` / `SongSelect.json` / `Playfield.json` 的 global layout metadata 也已由 `TestSceneOmsBuiltInSkin` regression 锁定，results-style shared panel shell 则已通过 `DefaultResultsPanelContainer` + `DefaultResultsPanelDisplay<TState>` 收口为 core stateful contract；score-driven results 是否需要独立 preview/skinnable target 仍待后续决定，当前自动推进先继续 mania 第二批余下迁移 |
| BMS playfield abstraction gate | 已完成 | 已新增共享 `BmsPlayfieldLayoutProfile`，并在 `DrawableBmsRuleset` 侧接入专用 `BmsPlayfieldAdjustmentContainer` 与 `Playfield Scale` / `Playfield Horizontal Offset` 配置项；`BmsHitTarget` 已拥有 `pressed / focused` 正式状态契约，`BmsLane` 会从输入管理器同步 receptor pressed state；lane width / lane spacing / scratch width ratio / scratch spacing / playfield width / playfield height / hit target height / hit target bar height / hit target line height / hit target glow radius / hit target vertical offset / bar line height 现都已正式进入 ruleset config / layout profile / lane layout / runtime playfield 链路，且新的 `BmsHitObjectArea` 已把真实 scrolling container 与 receptor 一起重定位；当前主线已从 gate 收口转向 BMS 默认层 |
| Mania 第一批：Stage / Column / Key | 进行中 | `OmsSkin` 当前已通过共享 `OmsSkinTransformer` 接出显式 `ManiaOmsSkinTransformer`，并把 `StageBackground` / `StageForeground` / `ColumnBackground` / `KeyArea` / `HitTarget` 切到首批 OMS shell 组件，同时把 `HitPosition` / `StagePadding` / `ColumnWidth` / `ColumnSpacing` 的 stage-local layout lookup 接到首批 OMS preset，把 `LeftLineWidth` / `RightLineWidth` / `ShowJudgementLine` / `LightPosition` / `LightFramePerSecond` 的 stage-local shell behaviour lookup 接到 `OmsManiaShellPreset`，把 `LeftStageImage` / `RightStageImage` / `BottomStageImage` / `HitTargetImage` / `LightImage` / `KeysUnderNotes` 的共享 shell asset lookup 接到 `OmsManiaShellAssetPreset`，把 `ColumnLineColour` / `JudgementLineColour` / `ColumnBackgroundColour` / `ColumnLightColour` 的首批 shell colour lookup 接到 `OmsManiaColumnColourPreset`，并把 `KeyImage` / `KeyImageDown` 的 stage-local key asset lookup 接到 `OmsManiaKeyAssetPreset`；这些路径都已验证可经 OMS preview 入口实际加载，且 5K+5K 双阶段会按 stage 重复使用对应 layout preset，mixed-stage 7K+6K 会按各自 stage 使用独立 shell behaviour preset 并持续拿到同一组共享 shell asset preset，mixed-stage 8K+9K 会按各自 stage 使用独立 shell colour preset，mixed-stage 5K+8K 也会按各自 stage 使用独立 key-image preset，而现有 non-column shared lookup 在 mixed-stage 上也已固定复用第一 stage preset；但整体仍主要消费 legacy-derived candidate assets 与配置语义，global layout metadata 与后续 OMS-owned 默认路径迁移仍未完成 |
| Mania 第二批：Note / Hold / HitBurst / Judgement / HUD | 进行中 | `OmsManiaNoteAssetPreset` 已把 `NoteImage` / `HoldNoteHeadImage` / `HoldNoteTailImage` / `HoldNoteBodyImage` 的 stage-local lookup 接到 OMS preview 路径，`OmsNotePiece` / `OmsHoldNoteHeadPiece` / `OmsHoldNoteTailPiece` / `OmsHoldNoteBodyPiece` 也已分别把 `ManiaSkinComponents.Note` / `ManiaSkinComponents.HoldNoteHead` / `ManiaSkinComponents.HoldNoteTail` / `ManiaSkinComponents.HoldNoteBody` 接到 OMS preview 路径，并已验证 `DrawableNote` / `DrawableHoldNoteHead` / `DrawableHoldNoteTail` / `DrawableHoldNote` 与 4K / 5K / mixed-stage 5K+9K 资产路径；shared judgement / HUD / bar-line 的 no-column lookup 现也已在 mixed-stage beatmap 上固定复用第一 stage preset；其中 `OmsNotePiece` / `OmsHoldNoteHeadPiece` / `OmsHoldNoteTailPiece` / `OmsHoldNoteBodyPiece` 已改为真实 `OmsManiaColumnElement` 派生实现，且不再继承 `LegacyNotePiece` / `LegacyHoldNoteHeadPiece` / `LegacyHoldNoteTailPiece` / `LegacyBodyPiece`，`OmsManiaJudgementPiece` 现已改为真实 `CompositeDrawable, IAnimatableJudgement` 实现且不再继承 `LegacyManiaJudgementPiece`，`OmsHitExplosion` 也已改为真实 `LegacyManiaColumnElement, IHitExplosion` 实现且不再继承 `LegacyHitExplosion`，并由 `OmsOwnedSkinComponentContractTest` + `TestSceneOmsBuiltInSkin` 锁定；但 note / hold 路径当前仍复用 legacy note scrolling / sizing、tail inversion、`NoteBodyStyle`、`HoldNoteLightImage` / `HoldNoteLightScale` 与 hold-body light / fade / stretch 语义，而 combo / bar-line 也仍继续消费 legacy combo / bar-line 语义，因此实际 OMS-owned note / hold / combo / HUD / bar line 默认路径仍未迁移完成 |
| BMS 第一批：Playfield / Lane / HitTarget / BarLine / Static BG | 已完成 | `BmsPlayfieldSkinLookup` / `BmsLaneSkinLookup`、ruleset fallback 与 `SkinnableDrawable` 包装已接通；`StaticBackgroundLayer` metadata shell、`BarLine` major / minor 语义，以及 playfield backdrop / baseplate、lane background / divider 默认外观都已切到 `BmsDefaultPlayfieldPalette` 驱动的 OMS-owned 默认层 |
| BMS 第二批：Note / Hold / LaneCover / Judgement / Combo | 已完成 | `BmsNoteSkinLookup` / `BmsLaneCoverSkinLookup` / `BmsJudgementSkinLookup` / `BmsSkinComponents.ComboCounter` 已接入正式 lookup / fallback；其中 `ComboCounter` fallback 已切到 `BmsComboCounter`，`LaneCover` 默认 fill / shade / focus 与 `Note` / `LongNoteHead` / `LongNoteBody` / `LongNoteTail` 默认面也都已切到 `BmsDefaultPlayfieldPalette` 驱动的 OMS-owned 默认层 |
| BMS 第三批：Gameplay HUD / Gauge / ClearLamp / Results / Song Select panels | 已完成 | `HudLayout` / `GaugeBar` / `GaugeHistoryPanel` / `GaugeHistory` / `ResultsSummaryPanel` / `ResultsSummary` / `NoteDistributionPanel` / `NoteDistribution` / `ClearLamp` 已有 lookup；gameplay HUD、results gauge history、results summary 与 Song Select 右侧分布图面板现都支持“外层整体 override + 内部内容单独 override”；BMS 默认层现已覆盖 gameplay HUD、results summary / clear lamp、results gauge history、Song Select note distribution、playfield metadata / accent surfaces、playfield shell surfaces 与 note / hold visuals 七批切片 |
| 用户皮肤导入与 partial override | 引擎层可用，ruleset 层未闭环 | `.osk` / `skin.ini` / importer 在引擎层可用，但 BMS 仍不能仅靠素材/语法独立构建完整皮肤 |
| 发布约束 | 已文档化 | `README.md` / `RELEASE.md` / `DEVELOPMENT_PLAN.md` 已把“正式发行只附带 OMS 默认皮肤包”写为未来 release gate |

本专项当前优先顺序：

1. 冻结默认皮肤包分层、组件矩阵、资源命名、候选包语义与文档职责。
2. 在已落地的 `OmsSkin` host / provider / resource root 基础上，继续补 Global shared shell 与 shared transformer。
3. 先完成 **BMS playfield abstraction gate**：lane geometry、scratch 宽度/间距、hit target / receptor 状态、bar line 参数、playfield adjustment/scaling。
4. 在 BMS gate 通过后，继续把当前候选视觉语言移植到 BMS 默认层；gameplay HUD、results summary / clear lamp、results gauge history、Song Select note distribution、playfield metadata / accent surfaces、playfield shell surfaces，以及 note / hold visuals 七批切片现已全部落地；默认皮肤包 host / provider 与 `OmsSkin` → `ManiaOmsSkinTransformer` 入口、shared shell / shared transformer，以及 global layout metadata 也已接通，results-style shared panel shell 也已进一步收口为 `DefaultResultsPanelDisplay<TState>` core contract；接下来转向决定 score-driven results 是否需要独立 preview/skinnable target，并推进 mania 两批迁移。
5. 然后再推进 mania 两批迁移：先 Stage/Column/Key，再 Note/Hold/HitBurst/Judgement/HUD。
6. 最后收口用户皮肤 partial override、上游默认皮肤移除与发行打包。

## Phase 2 / Phase 3 快照

| 阶段 | 状态 | 备注 |
| --- | --- | --- |
| Phase 2 | 阻塞 | 依赖 Phase 1 的 gameplay、判定、Gauge、结算闭环，以及 Phase 1.1 的 OMS 内置皮肤基线先落地 |
| Phase 3 | 阻塞 | 依赖本地 BMS 主流程和离线功能先稳定；在线更新、账号、排行榜、谱面下载、聊天 / 新闻、多人 / 观战与远程难度表源在此之前保持禁用或隐藏 |

## 待人工操作验收（统一后置）

- 原则：需要用户亲自点击、拖放、观察 UI 或验证发行物行为的事项，不默认占用当前自动开发主线；统一放到 Phase 1 阶段末尾处理，或在其已经构成真实阻塞时再单独拉起

| 事项 | 当前状态 | 默认触发时机 |
| --- | --- | --- |
| 1.5 桌面端拖放导入 / Song Select 标签 / skipped-file 通知 / OMS `songs/` 目录直读真实 UI 验收 | 待做 | Phase 1 自动开发项基本收口后统一执行 |
| 桌面端真实 UI smoke test（补最近一次人工操作记录） | 待做 | 需要对外确认当前可玩链路时统一执行 |
| 便携发行物实际运行与手工覆盖更新验证（含 OMS 内置皮肤发行门槛） | 待做 | 首发离线便携发布基线与 Phase 1.1 默认皮肤门槛接近完成后统一执行 |

## 当前主线

| 主线 | 焦点 | 状态 |
| --- | --- | --- |
| A: Phase 1.1 皮肤系统专项 | OMS 默认皮肤包、当前候选包语义、BMS 默认层迁移、后续 mania OMS-owned 迁移、移除 upstream 原生默认皮肤 | 进行中（BMS abstraction gate 与默认层已收口，`OmsSkin` host / explicit mania transformer 入口、首批 mania shell 组件、首批 stage-local layout preset、首批 stage-local shell behaviour preset、首批 shared shell asset preset、首批 shell colour preset、首批 stage-local key-asset preset，以及 mania 第二批的首个 stage-local note/hold asset preset、首个 explicit note component slice、首个 shared judgement asset preset、首个 shared judgement-position slice、首个 shared bar-line config slice、首个 explicit judgement piece slice、首个 explicit bar-line component slice、首个 explicit combo counter component slice、首个 stage-local hitburst config preset与首个 explicit hitburst component slice 已起，且现有 non-column shared config 的 mixed-stage fallback、Global shared shell / shared transformer shell、global layout metadata 与 results-style shared panel shell 也已补齐首轮实现；当前已继续转向 mania 第二批里最便宜的 actual OMS-owned component migration，`OmsNotePiece` / `OmsHoldNoteHeadPiece` / `OmsHoldNoteTailPiece` / `OmsHoldNoteBodyPiece` / `OmsManiaJudgementPiece` / `OmsHitExplosion` / `OmsManiaComboCounter` / `OmsBarLine` 已升级为实际实现，后续重点转向 note/hold / combo/HUD / bar-line 的余下 legacy 语义清理，score-driven results preview/skinnable target 暂留后续评估） |
| B: gameplay 与长条语义 | LN/CN/HCN release-window 边界回归与真实谱面验校 | 次优先级，待人工验校 |
| C: 正式输入与多 keymode | analog scratch 专用语义、cross-device 终态与稳定 HID backend | 次优先级，进行中 |
| D: 首发离线发行基线 | RELEASE.md 已文档化；后续需叠加 OMS 内置皮肤发行门槛 | 已文档化，待实机验证 |
| E: 人工验收后置 | 1.5 UI / smoke test / 便携发行物统一后置 | Phase 1 / Phase 1.1 接近收口后统一执行 |

## 遗留问题

### 高优先级

- **Phase 1.1 皮肤系统专项**：BMS playfield abstraction gate 与 BMS 默认层都已在 ruleset 侧收口；`OmsSkin` host / provider / resource root、显式 `ManiaOmsSkinTransformer` 入口、首批 mania shell 组件、首批 stage-local layout preset、首批 stage-local shell behaviour preset、首批 shared shell asset preset、首批 shell colour preset、首批 stage-local key-asset preset，以及 mania 第二批的首个 stage-local note/hold asset preset、首个 explicit note component slice、首个 shared judgement asset preset、首个 shared judgement-position slice、首个 shared bar-line config slice、首个 explicit judgement piece slice、首个 explicit bar-line component slice、首个 explicit combo counter component slice、首个 stage-local hitburst config preset与首个 explicit hitburst component slice 也已落地，现有 non-column shared config 的 mixed-stage fallback、Global shared shell / shared transformer shell、global layout metadata，以及 results-style shared panel shell 也已补齐首轮实现；当前必须继续推进 mania 第二批 OMS-owned 迁移，优先清掉 note/hold 默认路径与 combo/HUD / bar-line 的余下 legacy 语义；score-driven results 是否需要独立 preview/skinnable target 暂待后续决定
- **内置皮肤候选包语义**：`SimpleTou-Lazer` 当前只可视为 mania 侧基础与视觉参考，不可提前当作“已完成的 OMS 默认皮肤”对外描述
- **移除 upstream 原生默认皮肤的产品默认地位**：需要从设置入口、runtime fallback 与正式发行物中逐步退出 Argon / Triangles / Legacy / Retro 的默认产品表面角色
- **默认皮肤包宿主与 mania OMS-owned 迁移**：BMS 默认层已完成七批切片的 ruleset 侧收口；默认皮肤包宿主 / provider / resource root 首个运行时骨架、mania Stage / Column / Key shell 的首批 OMS 组件、首批 stage-local layout preset、首批 stage-local shell behaviour preset、首批 shared shell asset preset、首批 shell colour preset、首批 stage-local key-asset preset，以及 mania 第二批的首个 stage-local note/hold asset preset、首个 explicit note component slice、首个 shared judgement asset preset、首个 shared judgement-position slice、首个 shared bar-line config slice、首个 explicit judgement piece slice、首个 explicit bar-line component slice、首个 explicit combo counter component slice、首个 stage-local hitburst config preset与首个 explicit hitburst component slice 也已通过验证，且现有 non-column shared config 的 mixed-stage fallback、Global shared shell / shared transformer shell、global layout metadata 与 results-style shared panel shell 都已补齐首轮实现；其中 judgement / hitburst / combo counter / bar line 已升级为实际 OMS-owned component implementation，当前高优先级剩余项转为继续清理 mania 第二批余下的 note/hold 默认路径与 combo/HUD / bar-line legacy 语义，并在后续再决定 score-driven results 是否需要独立 preview/skinnable target
- **1.6 真实谱面长条验校**：当前 Phase 1 自动推进里最贴近玩法质量的剩余项
- **便携发行物实机验证**：RELEASE.md 已文档化，后续还需带上 OMS 内置皮肤发行门槛一起验收

### 中优先级

- **Windows HidSharp 初始化兼容问题**：`DeviceList.Local` 在部分 Windows 环境下会以 `RegisterClass failed` 直接终止进程；当前已通过 Windows 默认禁用 HidSharp 规避闪退，设置页提示属预期行为，但稳定 HID backend 仍待后续专项处理
- **IIDX 实机验证**：oms.Input 全链路已接通，仍缺真实硬件全链路验证
- **AutoMapper GHSA-rvv3-g6hj-g44x**：构建告警已通过 `NuGetAuditSuppress` 定点压制；运行时循环图路径仍以 `MaxDepth(3)` 限深，升级到 15.1.1+ 或移除仍待跟踪（15.x 额外引入 license 与配置 API 迁移）
- **上游 cherry-pick 风险**：~37 个文件被修改（见 UPSTREAM.md），高频改动区 BeatmapCarousel/OsuGameBase 等冲突风险高
- **密度星级标定常数**：后续可基于真实谱面样本校准各 keymode 的标定常数

### 低优先级

- 少量既有 nullability warnings（非 OMS 引入），不阻断开发

## 下一次更新时应检查的内容

- 待人工验收项是否已执行（导入 UI / smoke test / 便携发行物）
- 默认离线 provider / 空 endpoint 装配是否仍保持为默认
- `SimpleTou-Lazer` 的候选包语义是否仍被文档准确描述为“候选基线”，而不是“已完成默认皮肤”
- BMS 是否已经从 playfield abstraction gate 的 layout/config bridge 收口，转入默认层与候选视觉语言承接，而不是继续停留在旧的 geometry bridge 叙述
- BMS 默认层是否仍保持不依赖 feedback/fallback 直绘，并且当前主线是否已真实转向默认皮肤包宿主 / mania OMS-owned 迁移
- mania 的 `Stage` / `Column` / `DrawableNote` / `DrawableHoldNote` 是否仍被放在 BMS abstraction gate 之后推进
- AutoMapper 是否已升级或移除
- 新增功能是否至少经过一次构建、测试或手动验证

## 更新约定

- 本文档优先更新“状态变化”和“遗留问题变化”，不要把每次提交都写成流水账
- 每完成一个 `DEVELOPMENT_PLAN` 步骤，至少同步更新一次“Phase 1 进度矩阵”
- 只有代码已落地并至少完成一次构建、测试或手动验证，才能从“进行中/仅骨架”改为“已完成”
- Phase 1.1 的执行顺序、门槛、候选包语义或 release gate 发生变化时，必须与 `DEVELOPMENT_PLAN.md`、`README.md`、`SKINNING.md`、`RELEASE.md`、`OMS_COPILOT.md` 同次同步
- 若实现与 `OMS_COPILOT.md` 发生偏离，先修正文档或代码，再更新本文件
