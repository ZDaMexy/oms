# OMS 开发进度与遗留问题

> 最后更新：2026-06-01（修复 mania autoplay 整类长条不按：`ManiaAutoGenerator.canParticipateInAutoplay` 改 nested-aware，用户实测原生+转谱长条 autoplay 均正常；此前同日补 BMS→mania 转谱 BGM 音频遗留与 K11/J6 立项）
> 本文档只记录"仓库里已经真实存在的状态"，不重复规划全文。
> 详细分步规划见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，权威技术约束见 [OMS_COPILOT.md](OMS_COPILOT.md)，外部 IIDX / BMS 方向校准见 [../other/IIDX_REFERENCE_AUDIT.md](../other/IIDX_REFERENCE_AUDIT.md)。

## 状态定义

| 状态 | 含义 |
| --- | --- |
| 已完成 | 代码已落地，且至少通过一次构建、测试或明确手动验证 |
| 进行中 | 已有实际实现，但功能尚未满足该步骤验收标准 |
| 仅骨架 | 项目、类或入口已创建，但核心逻辑尚未接入 |
| 未开始 | 仓库中尚无对应实现 |
| 阻塞 | 当前步骤依赖尚未满足，暂时不能有效推进 |

## 最新快照

- **当前阶段**：Phase 1.1 皮肤系统专项执行中（BMS 默认层已收口，mania OMS-owned 组件与 release-gate 回归已继续收口；当前主线仍以公开发行物产品面收尾与 1.17 输入硬件/语义验收为先，但外部 IIDX 审计导出的反馈闭环与判定 parity 缺口已抬升为下一优先补强项；与此同时，`P1-J` 已把 dense full autoplay 从“50k 密段明显慢放”收口到“50k 可完成但仍需确认是否还存在 once-per-run 致命卡顿”）
- **仓库定位**：Windows-only，保留 osu!mania + BMS，已移除 Osu/Taiko/Catch
- **主入口**：`osu.Desktop.slnf`（含 7 个项目）
- **BMS 规模**：约 167 个源文件；`oms.Input` 15 个源文件（含 Windows DirectInput backend）；58 个测试源文件（以上为 2026-04-25 本地文件计数，排除 `bin/obj`）
- **已落地主链**：BMS 解码 → 转换 → 导入 → 7K+1 gameplay → 四套判定 → 六种 gauge + GAS → EX-SCORE / CLEAR LAMP / DJ LEVEL → CN/HCN mode-aware 计分 → 本地 best/replay/排行榜按 judge mode + long-note mode 分桶 → BMS replay recording / playback / 本地归档 → 难度表来源管理 / 缓存 / MD5 匹配 / 表分组 → Song Select 分布图 → 谱面元数据摘要 → gameplay → results 自动跳转
- **BMS 元数据**：`#SUBTITLE` / `#SUBARTIST` / `#COMMENT` / `#PLAYLEVEL` / `#DIFFICULTY` 已解析，Song Select 可显示谱师、内部标级与表标签
- **BMS 选歌分组**：Song Select 当前已把 BMS 可见分组收窄为 `难度表`、`外部谱库`、`内部谱库`、`曲师`、`谱师`、`BPM`、`星数`、`最近游玩时间`、`谱面时长`、`成绩评级`、`标题`；`难度表` 现为默认分组，`未分组` 与若干上游通用分组只在非 BMS ruleset 保留。进入 BMS 选歌与切换任一 BMS 分组时，当前视图会停留在分组最外层，并以 keyboard selection 高亮当前歌曲/谱面所属的最外层分组；`外部谱库` / `内部谱库` 当前也已走同一条 ruleset-specific hierarchical grouping 管线，不再依赖 `DifficultyTable` 专用特判。该功能面已按主线收口，剩余仅为 `P1-G` 下的 Song Select UI 人工展开验收与后续测试回归。
- **BMS 选歌排序**：Song Select 当前已使用 ruleset-specific 8 项排序：`标题`、`曲师`、`BPM`、`时长`、`星数`、`点灯状态`、`达成率`、`miss 数`；其中本地成绩派生项的显示标签已明确改用 BMS 专用文案，不再回落到通用 `Clear Lamp` / `准度要求`，mania 不受影响。
- **P1-I 子线状态**：`I1` / `I2` / `I3` 均已完成落地。`BmsCompositionFilterControl` 已以 BMS-local 私有单轨控件形式落地：`RC / LN / SCR` 三段可独立启停、各自表示最大占比、尾段为空白容差；`BmsCompositionHandle` 拖拽句柄可在段间边界拖拽并显示当前数值；`BmsCompositionRowButton` / `BmsKeyCountToggleButton` 非激活态用 `ColourProvider.Background3/Background1`（hover 效果可见）；`SearchHintTooltip` 已接入并修复 DI 崩溃（构造函数注入，对齐 `ModTooltip` 模式）；颜色冻结 RC=蓝(94,190,255) / LN=黄(255,212,92) / SCR=橙(255,119,86)。`I4` focused regression 仍在进行中（单轨拖拽 headless regression 与 shared visual gate 待补强）。
- **P1-J 子线状态**：shared keysound timing、lane/order 首轮 hot-path 收口、live channel non-destructive resize、pause/seek 生命周期回收、player-level 音频语义 proof、hold-note body-tick early-break、BMS replay frame 缓存化、full autoplay 的对象级 `AutoPlay` + direct-time replay 分流，以及 full autoplay keysound sample pool 预热都已落地。当前自动化基线已覆盖 full autoplay correctness、HUD/key-counter replay surface 与 keysound 邻接回归；人工侧最新回报是 10k autoplay 无明显变化，50k chart 已可在约 150 平均 / 70 最低 FPS、8ms 延迟下完成，但是否还残留 once-per-run 致命卡顿仍待下一轮现场确认。
- **P1-K 子线状态**：`P1-K` 当前范围已阶段性收口，`K1-A/K1-B/K2-A/K3-A/K3-B/K3-C/K3-D/K3-E/K3-F/K4-A/K4-B/K4-C/K4-D/K4-E/K4-F/K4-G/K4-H/K4-I/K4-J/K4-K/K4-L/K4-M/K4-N/K4-O/K4-P/K4-Q/K4-R/K4-S` 已连续落地，且 `K4` 数字层级现已整体收口：`BmsDecodedChart` 现显式暴露 `RawChannelEvents`，并新增 `ScrollEvents`、`BgaEvents`、`InvisibleObjectEvents` 与 `MineEvents`；`BmsChannelEvent` 已保留 `RawChannelToken` 与 `SourceLineOrder`，`BmsBeatmapInfo` 也新增了 `ScrollTable`、`UnknownHeaders`、`BgaDefinitions`、`AtBgaDefinitions`、`ArgbDefinitions`、`SwBgaDefinitions`、`PoorBgaMode`、`GetVisualDefinitionProjections()`、`TryGetVisualDefinitionProjection()` 与 `GetPreferredBackgroundAssetReference()`；[../../osu.Game.Rulesets.Bms/Beatmaps/BmsVisualDefinitions.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsVisualDefinitions.cs) 现已承接 richer BGA-definition header family 的 typed model 与 unified projection。`BmsBeatmapDecoder` 现既会按 source channel line 写入 raw carrier，也会把 `SC` 这类非十六进制 channel token 保留为 raw placeholder，让 negative `#BPMxx` 与 duplicate line compound 进入 parser contract，在 LN channel 保留显式 `00` 作为 `LNTYPE 2` 的 closing marker，把 BGA base/poor/layer/layer2、invisible object 与 landmine channel 接进第一批 visual typed surface，并进一步将 `SCROLLxx` 定义 + `SC` channel line 解析成 typed `ScrollEvents`，同时把 `#BGA`、`#@BGA`、`#ARGB`、`#SWBGA` 与 `#POORBGA` 解析进 header-side typed surface，再将这些分散 definitions 收口为按 index 读取的组合视图；`K4-A` 进一步把 static background metadata、import normalisation 与 `BmsBackgroundLayer` 接到同一 background asset projection 上，并通过 `BitmapTable` 把 `#BGA/#@BGA` 的两位引用解析回实际资源名，`K4-B` 让 `BmsNoteDistributionGraph` 在 projected source beatmap 可用时避免 second conversion，`K4-C` 让 `BmsBeatmap.GetStatistics()` 优先复用 metadata 中的 `ChartFilterStats`，`K4-D` 让 `BeatmapLocalMetadataDisplayResolver` 与 `BmsStarRatingResolver` 通过 `BmsPersistedMetadataResolver` 共享 persisted `chart_metadata` 读取，`K4-E` 让 Song Select 的 author sort/group/filter 通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayCreator()` 共享 BMS local creator fallback，`K4-F` 则继续让 Song Select 的 artist sort/group/filter 与 `BeatmapAttributeText` 通过同一 resolver 共享 BMS local artist / creator fallback，`K4-G` 又让 `BeatmapMetadataDisplay` 通过同一 resolver 共享 gameplay metadata display 的 BMS local artist / creator fallback，`K4-H` 又让 `ExpandedPanelMiddleContent` 通过同一 resolver 共享 results metadata display 的 BMS local artist / creator fallback，`K4-I` 又让 `DrawableProfileScore` 与 `DrawableMostPlayedBeatmap` 通过同一 resolver 共享 profile metadata display 的 BMS local artist fallback，`K4-J` 又让 `SongTicker` 与 `NowPlayingOverlay` 通过同一 resolver 共享 menu metadata display 的 BMS local artist fallback，`K4-K` 又让 `DailyChallengeIntro` 通过同一 resolver 共享 daily challenge creator fallback，`K4-L` 又让 `DrawableRoomPlaylistItem` 通过同一 resolver 共享 online playlist creator fallback，并保留 linked-profile 分支，`K4-M` 又让 `SubScreenRoundResults` 在构造 round-results `ScoreInfo` 时优先复用本地 `BeatmapInfo`，仅在缺失时才回退到 API 最小壳，`K4-N` 又让 `LegacyBeatmapSkin` 通过同一 resolver 共享 beatmap skin metadata 的 creator fallback，`K4-O` 又让 `IBeatmapInfo.GetDisplayTitleRomanisable()` 通过同一 resolver 共享 title display 的 artist / creator fallback，`K4-P` 又让 `FilterControl.ScopedBeatmapSetDisplay` 在能拿到具体 beatmap 时优先复用首个 beatmap 的 full title authority，并显式禁用 difficulty name，只在空 set 时才回退到 metadata-only overload，`K4-Q` 又让 `DailyChallengeIntro` 在可拿到具体 beatmap 时优先复用 `IBeatmapInfo.GetDisplayTitleRomanisable(includeDifficultyName: false)` 这条 full title authority，不再继续直接走 `BeatmapSetInfo.Metadata.GetDisplayTitleRomanisable(false)`，因此 daily challenge title line 也不再暴露 raw `/obj:` 后缀或误带难度名，`K4-R` 又让 `BeatmapDeleteDialog` 通过 `BeatmapSetInfo.GetDisplayTitleRomanisable(includeCreator: false)` 共享同一 set-level title authority，不再继续直接走 `beatmapSet.Metadata.GetDisplayTitleRomanisable(false)`，因此 delete confirmation title 也不再暴露 raw `/obj:` 后缀、不会误带难度名，并继续保持不展示 creator suffix，而 `K4-S` 又让 `PanelBeatmapSet` 与 `PlaylistItem` 通过 `BeatmapSetInfo.GetDisplayArtistRomanisable()` 共享同一 set-level artist authority，不再继续直接走 raw set metadata；本轮又让 `IBeatmapInfo.GetDisplayTitle()`、`IBeatmapSetInfo.GetDisplayTitle()`、`ModelExtensions.GetDisplayString()` 与剩余 set-level artist / beatmap-title consumer 统一复用同一 authority，并进一步把 `ICachedModlessPlayableBeatmapSource`、`WorkingBeatmap.GetPlayableBeatmap()`、`BmsDecodedBeatmap` 与 `BmsImportedBeatmapFactory` 收口为 source-bound 的 modless playable projection cache / invalidation contract：无 mods 的同源 BMS playable projection 只 materialize 一次，换 source 或带 mods 则重新转换，loader 首次 conversion 也会把现成 projection seed 回同一 cache boundary。本轮 `K6` 又把 results/statistics 邻接面写回 `Ruleset` 的“已带 mods playable beatmap”合同：`BmsRuleset.PrepareScoreInfoForResults()`、`BmsClearLampProcessor.CreateGaugeHistory()` 与 `CalculateFinalGauge()` 现都消费 caller 传入的 beatmap，不再在 helper 内再次应用 beatmap mods；`BmsPlayableBeatmapCacheTest` 与 `BmsClearLampProcessorTest` 也已分别补出 `Mirror` focused proof，锁住这条结果侧 contract。本轮 `K7` 则进一步把 `CreateStatisticsForScore()` 的 results summary consumer proof 收口为稳定的 plain focused 路径：`SkinnableBmsResultsSummaryPanelDisplay` 现可暴露只读 summary state，而 `BmsRulesetStatisticsTest` 已锁住 selected gauge/judge/long-note mode、EX-SCORE、DJ LEVEL 与 computed clear lamp 会端到端进入 summary consumer，并明确 `PERFECT` / `FULL COMBO` 会继续覆盖 gauge-derived lamp。本轮 `K8` 则进一步把 `CreateStatisticsForScore()` 的 gauge history consumer proof 收口为同一条 plain focused 路径：`SkinnableBmsGaugeHistoryPanelDisplay` 现可暴露只读 history state，而 `BmsRulesetStatisticsTest` 已锁住 auto-shift `EX-HARD -> HARD -> NORMAL` timeline 与对应 sample/time/value 会端到端进入 gauge history consumer。因此 `K5`、`K6`、`K7` 与 `K8` 也已完成；当前阶段已收口，post-`K8` 的 special-chart / long-note consumer follow-up 后置为 backlog，不再作为当前进行中项。当前 backlog 的首个正式切片 `K9` 已继续推进到“scratch sample-only 运行时 + autoplay ignore contract + persisted converted star + spread display/read-model 收口”阶段：converted scratch 现以 sample-only ignore-judgement object 保留键音，不再占 mania judged column；`ManiaAutoGenerator` 也已按 nested-aware 谓词跳过这类 ignore-only sample 对象（自身与嵌套均不影响 combo）、autoplay 不再被 scratch/BGM sample 干扰，同时长条经嵌套 `HeadNote`/`TailNote` 正常参与（早期只看顶层 `AffectsCombo`、误跳全部长条—原生 mania 与转谱同坏—的回归已于 2026-06-01 修复并经用户实测确认）；modless converted mania 星数则已持久化到 `BeatmapMetadata.RulesetDataJson`，并由 `BeatmapDifficultyCache`、`BackgroundDataStoreProcessor` 与 Song Select spread display 统一按 current-ruleset 读口消费。当前剩余缺口已主要收窄为 explicit public wording 与更宽的 presentation/manual proof。
- **BMS→mania 转谱音频补全（K11 + J6 首版已落地：E 实测修复 / D 未解后置）**：此前 `BMS -> mania` 转谱只搬玩家可击打对象的键音、BGM（autoplay channel `0x01`）被静默丢弃 → 纯键音 BMS 在 mania 游玩（mania ruleset + `ShowConvertedBeatmaps`）丢掉鼓/贝斯/铺底/人声等背景层。`K11` 已落地：BGM 以 sample-only `BmsConvertedBgmSampleHitObject` autoplay 发声、不进 scorable/star，同刀把 LN 尾 node sample 置空对齐 BMS「长条只头发声」（`BmsToManiaBeatmapConverterTest` 19/19、BMS 869/869、Release 0 错误）。P1-J `J6` 首版亦已落地：转谱 BGM/scratch 改走复用的 `BmsKeysoundStore`（`DrawableManiaRuleset` 反射宿主 + 缓存 + 挂载到游玩树），暂停/seek 由 store 统一停、通道有上限，缺席安全回退；**用户实测 E（暂停停 BGM）已修复 ✅、普通 mania 无回归 ✅；D（dense 极端谱高密段仍极度缓慢）未解、后置**（瓶颈不在音频对象数、待 profile），残留 mania `Note`/`HoldNote` 自身键音仍 per-drawable。详见 [P1-K CHANGELOG](../subline/P1-K/CHANGELOG.md) 与 [P1-J CHANGELOG](../subline/P1-J/CHANGELOG.md) 2026-06-01。
- **BMS 解析-导入-静态背景链**：本轮已补齐三处主链缺口：非 `.bme` 稀疏 `9K_Bms` 谱面现可通过 `channel 17` 进入 9 键路径，不再要求九个 lane 全部出现；decoder 的 non-fatal warning 现会在导入成功时汇总成单独通知并写入日志，而不是静默吞掉；静态背景链现统一为 `STAGEFILE > BACKBMP > BANNER`，导入期会把静态图引用规范化到实际存在的文件名，运行时也会对旧数据补做常见图片扩展名 fallback，默认 `BmsBackgroundLayer` 在有当前 `WorkingBeatmap` 时会优先尝试显示真实背景贴图。
- **BMS 选歌 BPM 显示**：Song Select 左上 BPM 统计现已按 imported chart 的真实 timing data 显示；`BmsImportedBeatmapFactory` 会把首次转换得到的 `ControlPointInfo` / `HitObjects` / `Breaks` 复用回 raw wrapper，使 `BeatmapTitleWedge` 这类 raw working beatmap consumer 不再回退到默认 `60 BPM`。BPM 分组与排序仍继续使用 persisted `BeatmapInfo.BPM`，两条链当前已不再失配。
- **存储**：Release 默认 `%APPDATA%/oms/`；`storage.ini` 可迁移到单一自定义数据根；BMS 使用 `chartbms/` 目录、mania 使用 `chartmania/` 目录的文件系统直读存储；Settings → Maintenance 现已拆成 `外部谱库` 与 `内部谱库` 两个 subsection，并把谱库扫描扩展为四个显式入口：`扫描外部谱库（重建）`、`扫描外部谱库（增量）`、`扫描内部谱库（重建）`、`扫描内部谱库（增量）`。其中 `增量` 模式只补导当前没有 active `FilesystemStoragePath` 记录的目录，`重建` 模式则继续重走全部候选目录；当前 managed-root 子目录判定也已补齐 trailing-separator 归一化，避免合法内部目录被误判为“不在托管根下”。`BeatmapSetInfo` 现还会持久化 `ExternalLibraryRootPath`，把 external root snapshot 固定到 beatmap set 上，供 `外部谱库` 分组与后续 fallback 使用。Settings → 常规 → 安装位置 现已把入口明确为 `更改数据目录位置`：选择空目录时会把当前数据内容直接迁入所选目录；若所选目录已有无关文件，则会改用其下 `oms/` 子目录；若所选目录本身已是可用数据目录，则只写入 `storage.ini` 并在重启后切换。整个流程只改变运行时数据根，不移动程序文件。
- **BMS 难度表来源管理**：Settings → 游戏模式 → BMS → 难度表 当前统一支持本地目录、`index.html`、`header.json`、表体 json 与 `http/https` URL；seeded preset 会按 `source_name` / `display_name` 自动认领现有预置来源；移除已导入 preset 时会清空来源并恢复隐藏占位，而不是删除内置 preset；导入或刷新失败时，设置页与首次启动页都会显示中文分类原因。
- **BMS 难度表当前状态**：`manager-owned metadata sync`、`RefreshAll` 真实结果合同与 `wrapper/source identity fallback` 三批修补已落地；在此基础上，响应性后置已继续推进两刀：persisted metadata 回写已从“单次全量重写所有 BMS 谱面”收窄成“按受影响 MD5 集合分批写入”，`RefreshAll` 也已补上逐源进度合同和 settings 页持续反馈；同时，internal / external rebuild 命中旧 beatmap set 时也会重新套用当前难度表 metadata。当前这轮工程修补已可收尾；若后续现场仍见 `Unrated`，优先进入原始 `.bms` 字节 MD5 差异诊断，而不是继续怀疑 Song Select 分组消费面。
- **首次启动向导**：首次启动设置当前已收口为六步 OMS flow：欢迎、UI 缩放、获取谱面、导入、难度表设置、按键绑定。获取谱面页改为 mania / BMS 外部站点导流与内部谱库补扫提示；导入页直接复用 `ExternalLibrarySettings`；难度表页通过反射调用 BMS 难度表管理器按分组导入 zris 预设 URL，并在多项失败时显示中文摘要；最终页复用全局、mania 与 BMS 的按键绑定 subsection。
- **首次启动稳定性与本地化**：手动重新打开首次启动向导并切到旧“游戏表现”页导致的 blank panel / unhandled error 已修复；欢迎页、获取谱面页与导入页的可见文案现已切到 OMS-owned localisation namespace + `.resx`，确保简中界面不再继续显示上游翻译。该表面主归属 `P1-A`，导入页复用外部谱库设置只形成 `P1-H` 从属暴露，不新开子线。
- **输入**：键盘 / Raw Input / XInput / MouseAxis 主链可用；Windows 默认 HID 已切到 DirectInput；`HidSharp` 仅为 `OMS_ENABLE_HIDSHARP=1` 诊断后端。桌面端 Settings -> 输入 当前已主动隐藏上游通用的数位板 / 触屏点击 / 鼠标 subsection，保留 OMS 相关键位 / supplemental binding 表面，但不移除对应 runtime config 与 handler 链。
- **训练 Mod**：`BmsModMirror` 与 `BmsModRandom` 已落地；`RANDOM` / `R-RANDOM` / `S-RANDOM` + seed / custom pattern 已接通，14K 单组 pattern 可自动复制到双侧
- **辅助 Mod**：`BmsModAutoScratch`、`BmsModAutoNote` 与 `BmsModAutoplay` 已落地，均归 `DifficultyReduction`；`A-SCR` 会让 scratch 退出判定 / 计分 / gauge 池，并提供 mod 内可见性 / 染色配置；`A-NOT` 会对非 scratch note 做同样的 assist 处理，并提供独立的 note 可见性 / 染色 / 染色盘；`A-SCR` 与 `A-NOT` 当前互斥，且二者都继续与 `autoplay` 互斥。`autoplay` 已接通 BMS replay frame / replay input handler / replay recorder / auto generator
- **BMS mod 记忆**：BMS 现通过 `BmsRulesetSetting.PersistedModState` 以 ruleset-local JSON snapshot 持久化 mod 选中状态与非默认配置；完全重启或从 mania 切走再切回 BMS 时都可恢复，且不影响 mania。实现 `IPreserveSettingsWhenDisabled` 的 configurable BMS mod 现在关闭再开启也不会丢配置；`Sudden / Hidden / Lift` 还额外提供 `记忆游戏内变动` 开关，默认开启时会把局内调整回写到当前 BMS mod 配置并在回场 / 下次启动后延续。启动早期若 `RulesetConfigCache` 尚未完成加载，`OsuGameBase` 现在会延后 replay 当前 ruleset 到 cache ready 后再执行恢复，避免冷启动首轮漏恢复或把 ruleset 误标记失败；该路径已由 `BmsStartupModPersistenceIntegrationTest` 锁定。
- **BMS 速度语义**：lane cover 现已按 IIDX/LR2 语义显式拆成 `Sudden`（上遮挡）与 `Hidden`（下遮挡）；`Lift` 已作为独立 mod 接入 playfield 几何并影响 `ScrollLengthRatio`；设置页现提供 `Normal / Floating / Classic Hi-Speed` 下拉与当前模式数值 slider，并在数值后追加“不启用 `Sudden / Hidden / Lift` 时的基础下落时间（ms）”；对应 hover 文案现已简述三种模式的区别，并明确括号内只是基础下落时间，而 `GreenNumber` 需要在游戏内结合 `Sudden / Hidden / Lift` 调节查看。设置页仍不暴露 `GreenNumber` 本身，也不再暴露 `Playfield Scale` 或数值型 `Playfield Horizontal Offset`。当前 playfield scale 已固定为 `1.0`，避免缩放破坏皮肤编排并扭曲权威 visual-speed surface；single-play 侧当前改为四态 `Playfield Style`：`1P（居左）`、`2P（居右）`、`居中（左皿）`、`居中（右皿）`。它只作用于 5K / 7K 的 playfield 停靠与 scratch 视觉侧别，其中 `1P / 2P` 为“侧停靠但保留固定屏侧间距”；不改变尺寸与可见时间语义，也不等价于完整 `1P/2P flip`。9K 固定居中，14K 固定双侧布局。runtime 速度反馈继续保留在 gameplay 内，以 mode-aware `GN + WN + 当前模式和值 + 当前目标` 表达；游玩内滚轮会按当前持久目标调节 `Sudden / Hidden / Lift`，单击 `UI_LaneCoverFocus`（或鼠标中键）会在已启用项之间循环切换持久目标。进入 BMS 游玩后，`UI_PreStartHold` 现已收口为“前 5 秒阻止开始 + 全程调速修饰键”这一运行时合同：前 5 秒按住时继续阻塞开谱并显示右侧 `READY HOLD` overlay，期间奇数列增速、偶数列减速，且 `UI_LaneCoverFocus` / 滚轮 / 中键仍可继续调整 lane cover 与目标切换；若 delayed-start 已在 hold 期间耗尽，则松开 hold 时必须重新给满一段 fresh delay，而不是立即开谱；正式 gameplay 开始后按住同一键也可继续调速，且居中的 `BMS speed` toast 会持续刷新显示。hold 修饰键按住期间，新的 lane action 不再转发进 gameplay 判定链，只承担 Hi-Speed 调节；`UI_PreStartHold` 与 `UI_LaneCoverFocus` 仍保持独立动作（默认 5K/7K/9K：PreStartHold = Q、LaneCoverFocus = W；14K：PreStartHold = T、LaneCoverFocus = Y）。当前 tri-mode surface 已落地，其中 `Classic` 仍锁定官方 sample `HS 10 + WN 350 => GN 300`，`Floating` 目前只实现 initial-BPM anchored surface，不应包装成完整 mid-song re-float parity。pre-start 1 号普通轨纯视觉流速预览第一版现已落地：`BmsSoloPlayer` 会按 pre-start pending + hold + pause state gate 到 `DrawableBmsRuleset`，并在第一非 scratch 轨的独立 preview 容器中复用 `BmsNoteSkinLookup` 与 `BmsScrollSpeedMetrics` 渲染纯视觉假音符；它不复用真实 `BmsHitObject` / `DrawableBmsHitObject` / lane keysound / judgement 链，且正式 gameplay 中即使继续按住同一调速键也不会再出现。对应自动覆盖现已补到 owner-level `TestSceneBmsPreStartHiSpeedOverlay` **3/3**、pre-start focused slice **24/24** 与输入桥 `OmsInputRouterTest` **9/9**，分别锁定 overlay 文案 / 输入合同、真实 delayed-start / hold modifier / preview gate / release-after-elapsed fresh-delay 语义，以及 hold 期间 lane action 的 gameplay 转发抑制。
- **osu!mania 滚动速度设置**：`Settings -> 游戏模式 -> osu!mania -> 滚动速度` 当前已通过 hover 文案明确为“标准车道几何下的参考下落时间”；由于不同皮肤会改变车道尺寸、判定线位置与缩放，同一数值不保证跨皮肤体感一致。更换皮肤后应重新校准，且 mania 与 BMS 的下落时间当前不可互相参考。
- **BMS 键音通道设置**：`Settings -> 游戏模式 -> BMS -> 键音通道数` 当前已把 shared `BmsKeysoundStore` ceiling 公开为 `1..256` 滑条，默认值现已从 `16` 调整为 `32`。hover 提示会直接概括低值更容易截断 BGM / 键音 / 长按尾音、`32` 为常用折中、高值更适合极高密谱面或较强机器，以及“缺音时先升到 `48/64`、额外负载增加时再逐步下调”的调参路径。该设置仍继续作用于同一 shared pool；运行时调高会立即补充通道，调低则会在超额 channel 停播后逐步回收，不再直接切断当前正在播放的键音。
- **P1-A / P1-C 交叉专题**：现阶段这条交叉线已从“strict Classic 收口”推进到“tri-mode Hi-Speed control surface + 阻止谱面开始/ingame start operator surface”。`P1-A` 继续负责 settings / HUD 宿主 / fallback / skin boundary 与 operator overlay / toast 的产品边界，`P1-C` 继续负责 mode-aware speed metrics、`Sudden / Hidden / Lift` 联动、hold modifier 调速语义与同一 feedback family 下的训练表达；pre-start 1 号普通轨纯视觉流速预览现也沿这条 split 落地，宿主/fallback 归 `P1-A`，显示时序、lane 选择与“绝不接判定链”语义归 `P1-C`。aggregate scalar state contract 仍停在第四刀，但当前 `GN` / `WN` 已明确属于 OMS 的 tri-mode runtime surface，而非完整 `FHS`；除冷启动 BMS mod 恢复外，pre-start overlay owner contract、real-player host binding、preview gate 与 hold 期间 lane 输入转发抑制也已补 focused coverage，后续 backlog 主要转为 full Floating parity（mid-song re-float、soflan range、更加严格的 IIDX start sequencing）、更广的 real-input integration coverage 与后置人工验收。
- **文档治理基线**：文档目录现已固定为 `doc_md/mainline`、`doc_md/subline`、`doc_md/other`、`doc_md/mini`；任何后续开发必须同步更新对应目录文档，子线与 mini 的变化若影响全局，必须反向同步主线四件套
- **结果页反馈基线**：BMS results 的 expanded 主评价与 contracted badge 已按 `DJ LEVEL` 显示，主分数区已显式标为 `EX-SCORE`；`BmsClearLampProcessor` 的结果侧 final gauge / gauge history 重放现会复用运行时 long-note mode 与 caller 提供的已带 mods playable beatmap，不再在 helper 内重复应用 beatmap mods，`HCN` body-tick fail 也不会再把 failed score 误持久化成 `PERFECT` / `FULL COMBO`
- **回放基线**：BMS replay frame / replay input handler / replay recorder / auto generator 已接通；本地 replay 归档复用 core legacy replay encode/decode 的 custom-ruleset fallback，当前按 lane action 持久化
- **反馈/训练闭环缺口**：BMS 最近判定 + `FAST/SLOW` + compact judgement summary + live `DJ LEVEL + EX 原始分子/分母 + %` + 带紧凑原因标签的 live `PERFECT / FC / FC LOST` 已入同一 feedback container，并具备瞬时 judge display 生命周期、compact visual timing-offset 与 fixed AAA EX pacemaker；pre-start 1 号普通轨纯视觉流速预览第一版也已以纯视觉 preview layer 落地。当前剩余缺口主要转为 controller calibration / deadzone / sensitivity 可见入口、更完整 judge display（超出当前 compact counts）与更丰富 pacemaker 来源。按 [../other/IIDX_REFERENCE_AUDIT.md](../other/IIDX_REFERENCE_AUDIT.md) 已提升为下一阶段优先补强项
- **判定系统语义差距**：`OD` 主路径已稳定；`BEATORAJA` / `LR2` / `IIDX` judge mode 已显式接通，其中 `BEATORAJA` / `LR2` 的 judge-rank difficulty 已进入 runtime 与 score bucket；但 early/late 非对称 BAD / excessive poor、scratch 特例、更完整 long-note release parity 与按 judge family 参数化的 `Empty Poor` 仍未收口
- **联网**：账号、在线排行榜、谱面下载、新闻/聊天、多人与观战入口及 Discord RPC 已按 `OnlineFeaturesEnabled` 守卫；默认 endpoint 已清空。BMS 难度表的公共 URL 导入/刷新为当前例外，不依赖 OMS backend。

### 皮肤系统现状

- **BMS 默认层**：七批 OMS-owned 切片已在 ruleset 侧收口（playfield / lane / note / hold / LaneCover / HUD / gauge / results / Song Select panels）
- **OmsSkin 基础设施**：`OmsSkin` host / provider / resource root、共享 `OmsSkinTransformer`、显式 `ManiaOmsSkinTransformer` 入口已落地
- **Global shell**：global HUD / SongSelect / Results / Playfield 缺省 shell 经 OMS preview 返回；`MainHUDComponents.json` / `SongSelect.json` / `Results.json` / `Playfield.json` layout metadata 由 regression 锁定；`ResultsScreen` global target 与 Skin Editor Results preview 已完成最小闭环
- **Mania 第一批**（Stage / Column / Key）：StageBackground / StageForeground / ColumnBackground / KeyArea / HitTarget 已切到 OMS shell 组件；10 类 stage-local / shared preset 已接通（layout、shell behaviour、shell asset、shell colour、key asset）
- **Mania 第二批**（Note / Hold / HitBurst / Judgement / HUD）：8 类 OMS-owned 组件已升格：`OmsNotePiece` / `OmsHoldNoteHeadPiece` / `OmsHoldNoteTailPiece` / `OmsHoldNoteBodyPiece` / `OmsManiaJudgementPiece` / `OmsHitExplosion` / `OmsManiaComboCounter` / `OmsBarLine`（均不再继承 legacy 类型），由 `OmsOwnedSkinComponentContractTest` + `TestSceneOmsBuiltInSkin` 锁定；note scrolling、combo 与 bar-line 的主要 runtime 语义已收口，当前剩余 gap 主要在 legacy config/asset lookup 兼容路径与公开发行物产品面收尾
- **Native-default removal**：`SetSkinFromConfiguration()` 已把 Argon / Triangles / DefaultLegacy / Retro 统一回退 OMS；`SkinManager` 只注册 `DefaultOmsSkin` 为受保护 built-in，启动时清理历史上游条目；legacy beatmap fallback 已切到 `DefaultOmsSkin`；`SkinManager.AllSources` 已去重
- **Partial override**：BMS 用户皮肤缺失 BMS 组件时返回 null 让后续 source 承接；mania legacy 用户皮肤缺失 note / hold / judgement / explosion / combo / bar-line 时回退 OMS 组件；mixed-layer 三类语义（mania-only / BMS-only / 双层皮肤）已有 runtime 证明
- **候选包语义**：`SimpleTou-Lazer` 仅为 mania 侧内置皮肤候选基线，不可对外称为"已完成默认皮肤"

### 1.17 输入切片现状

- `TestSceneOmsScratchGameplayBridge` 已覆盖：Scratch1 reverse-config / inverted suppression / reverse-config late-hit、14K Scratch2 全路径、second scratch mixed-source / inverted suppression、normal / inverted mouse/HID hold-survival、XInput takeover
- desktop product surface 当前已通过 `OsuGameDesktop.CreateSettingsSubsectionFor()` 安全隐藏 upstream `MouseSettings` / `TouchSettings` / `TabletSettings`；这属于 public settings surface 收口，不等于删除 mouse/touch/tablet runtime 语义
- 剩余：更广的 analog scratch cross-device 产品语义、终态输入链、controller calibration / deadzone / sensitivity / diagnostics UI，以及真实 HID 硬件验收

## 开发指标

| 指标 | 当前值 | 说明 |
| --- | --- | --- |
| Phase 1 完成率 | 70.6% (12/17) | 仅按标记"已完成"项计算 |
| Phase 1 加权进度 | 85.3% (14.5/17) | 已完成=1, 进行中=0.5, 仅骨架=0.25, 未开始/阻塞=0 |
| Phase 1.1 皮肤专项 | 进行中 | BMS 默认层已收口；mania OMS-owned 组件、runtime 语义与 release-gate 回归已继续收口；公开发行物产品面待收尾 |
| 桌面端构建 | 通过 | `dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 退出码 0（2026-05-26） |
| BMS 全量测试 | **869/869** | 最近一次全量 `osu.Game.Rulesets.Bms.Tests`（2026-05-31） |
| Mania 全量测试 | **761/761** | 最近一次全量 `osu.Game.Rulesets.Mania.Tests`（2026-04-24） |
| BMS 聚焦回归 | **111/111** | `BmsStartupModPersistenceIntegrationTest` / `BmsModStatePersistenceTest` / `TestSceneBmsSoloPlayerPreStart` / `BmsSkinTransformerTest` / `TestSceneBmsUserSkinFallbackSemantics`（2026-04-25） |
| Mania 皮肤回归 | **92/92** | `OmsOwnedSkinComponentContractTest` + `TestSceneOmsBuiltInSkin`（2026-04-25） |
| Scratch bridge | **43/43** | `TestSceneOmsScratchGameplayBridge` 最近一次快照（2026-04-24） |
| osu.Game.Tests gate | **18/18** | `ExternalLibraryScannerTest` / `TestSceneFirstRunSetupOverlay` / `TestSceneFirstRunScreenImportFromStable` / `TestSettingsMigration`（2026-04-25） |
| K9 转谱聚焦回归 | **33/33** | mania convert/autoplay **14/14** + selector/resolver **19/19**（2026-05-26） |
| 编译器诊断残留 | 0 | 当前 Release 构建已清零；`CS1574`、本地化 OLOC、`AD0001` 兼容性问题均已处理，SharpCompress GHSA 通过 `NuGetAuditSuppress` 做定点抑制（2026-05-09） |

## 最近一次验证

> 严格只保留一条最新快照；详细命令与历史记录归档到 [CHANGELOG.md](CHANGELOG.md)。

### 2026-05-31（P1-H：难度表全 Unrated 真根因 —— 共用 RulesetData 列互相覆盖）

- **真根因**：转谱星数（osu.Game `BmsPersistedMetadataData`：`converted_star_ratings`）与难度表（BMS `BmsBeatmapMetadataData`：`difficulty_table_entries`/`chart_filter_stats`）各自定义容器类却**共用同一个 `BeatmapMetadata.RulesetData` 列**；`SetRulesetData<T>` 整体覆盖 + Newtonsoft 默认丢弃未知字段 → 互相抹掉对方独有字段。转谱星数重算冲掉难度表 entries（全 `Unrated`，用户实测重算 11336 后即复现）；难度表回写冲掉星数 → 启动判 missing → 重算 → 再冲，破坏性 ping-pong（"有概率"取决于最后谁写）。
- **修复**：两容器类均加 `[JsonExtensionData]` 往返保留对方字段（`IsEmpty` 须计入扩展字段，否则置空连带抹掉对方）。双向回归 `TestDifficultyTableWriteBackPreservesForeignRulesetDataFields`（BMS）+ `TestConvertedStarRatingWritePreservesDifficultyTableFields`（osu.Game）。约束见 [P1-H CONSTRAINTS #22](../subline/P1-H/TECHNICAL_CONSTRAINTS.md)。
- **同日订正**：早先误判为 carousel staleness 并加 per-set `BeatmapSetInfo.DifficultyTableRevision` bump（schema 55）—— 大库（5.7 万谱 + 万级条目）下一次开关表命中数千 set，per-set re-detach 致 UI 卡死 1~2 分钟（实测），已**撤销** bump（字段保留闲置，schema 55 不回退）。注入全局 `RealmAccess`（消除第二实例 `cleanupPendingDeletions` 越权）/ enable·disable·remove 异步化 / MD5 归一化 / `loadTableSource` 递归保护 / 去 `GetSources().Single` 等改动保留有效。
- **已知限制**：carousel 不在会话中途刷新难度表分组（深层 `Metadata` 写不触发浅层 `BeatmapSetInfo` 订阅）——中途开关表需**退出选曲重进或重启**反映、启动恒正确；中途即时刷新留作后续（分组改走内存索引 live lookup + 一次性 re-filter）。另：大库（5.7 万谱）下该回写仍约 1 分钟（`updatePersistedBeatmaps` 用 `AsEnumerable` 全表过滤，#1 后续；后台执行不硬阻塞但掉帧、音乐正常）。
- **测试 / 构建**：BMS 全套 **869/869**、`BmsStarRatingResolverTest` **13/13**、`dotnet build osu.Desktop.slnf -p:Configuration=Release` 0 警告 0 错误。**用户实机确认**：修复后转谱星数重算不再复发（ping-pong 打破）、难度表分组正确（退出选曲重进/重启生效）；旧构建已冲掉的 entries 经一次 `禁用→启用` 补回后持久。
- **更早快照**（P1-L Phase 2 滚动旁路 + Phase 1 地雷、P1-J 键音链路、P1-K 解析→呈现修复、K9/K10 邻接与 carousel 性能等）均已归档 [CHANGELOG.md](CHANGELOG.md)；P1-L Phase 4 人工验收 / Floating·Classic 标定等遗留见 `subline/P1-L`。本状态页只保留最新一条快照。

## 联网约束

| 项目 | 状态 | 说明 |
| --- | --- | --- |
| 便携版发布基线 | 已落地 | `portable.ini` 标记 → `<exe>/data/` 自动生成；已实机 Release publish 验证 |
| 游戏内在线更新 | 已禁用 | Velopack 跳过，`CreateUpdateManager()` 切回基础实现；手工覆盖后不会进入游戏内自更新链 |
| 默认 endpoint | 已清空 | `LocalOfflineAPIAccess` 默认装配；hub connector 返回 null |
| 游戏内联网入口 | 已隐藏 | Toolbar / 主菜单 / Song Select / overlay / 编辑器外链 / First-run Setup 均按 `OnlineFeaturesEnabled` 收口 |
| 上游静态资源 fallback | 已离线化 | LargeTextureStore / PreviewTrackManager / metadata cache 在线源已关闭；profile 资源已补本地占位 |
| BMS 原样目录存储 | 已完成 | `chartbms/` 直读，`FilesystemStoragePath` / `LocalFilePath` 已记录 |
| Mania 目录存储 | 已完成 | `chartmania/` 直读，与 BMS `chartbms/` 同级的独立目录树；`ManiaFolderImporter` + `ManiaBeatmapImporter` 已落地 |
| 多谱库根扫描 | 已完成 | `ExternalLibraryConfig`（JSON）+ `ExternalLibraryScanner`（委托注入）已落地；Settings → Maintenance `ExternalLibrarySettings` 设置 UI 可添加/移除/扫描；BMS / mania 双类型根均可注册 |

## 已落地能力

- 上游裁剪与项目基础，主入口以桌面端为准
- BMS 解码 → 转换 → 导入 → 7K+1 gameplay → 四套判定 → 六种 gauge + GAS → EX-SCORE / CLEAR LAMP / DJ LEVEL
- LN / CN / HCN mode-aware 计分与分桶
- BMS 结果页反馈首轮收口：expanded 主环 / contracted badge 使用 DJ LEVEL，主分数区显式使用 EX-SCORE 文案，结果侧 gauge / lamp 重建已与 gameplay mod 链对齐
- 本地/在线难度表来源管理 / 缓存 / MD5 匹配 / 表分组 / Song Select 音符分布图
- BMS Song Select `外部谱库` / `内部谱库` 分组与 external root snapshot 持久化
- oms.Input 多源输入（键盘 / XInput / MouseAxis / Raw Input / DirectInput HID）
- gameplay → results 自动跳转
- BMS 皮肤链路：ruleset transformer + 全组件 lookup 接线

## Phase 1 进度矩阵

| 步骤 | 状态 | 差距 |
| --- | --- | --- |
| 1.1 上游清理 | 已完成 | — |
| 1.2 BMS 数据模型 | 已完成 | — |
| 1.3 BMS 解析器 | 已完成 | — |
| 1.4 谱面转换器 | 已完成 | — |
| 1.5 归档导入 | 进行中 | 仅剩桌面端拖放导入 UI 手工验收 |
| 1.6 键音系统 | 进行中 | 缺真实谱面长条边界人工验校 |
| 1.7 BMS 规则集入口 | 进行中 | 缺更完整 gameplay HUD 与真实谱面 gameplay 边角验校 |
| 1.8 7K+1 Playfield | 进行中 | 缺真实车道样式、皮肤化 drawable；并入 Phase 1.1 |
| 1.9 OD 判定系统 | 已完成 | — |
| 1.10 Normal Gauge | 已完成 | — |
| 1.11 EX-SCORE 与结算 | 已完成 | — |
| 1.12 密度星级 | 已完成 | — |
| 1.13 难度表来源管理 | 已完成 | — |
| 1.14 MD5 匹配 | 已完成 | — |
| 1.15 Song Select 表分组 | 已完成 | — |
| 1.16 音符分布图 | 已完成 | — |
| 1.17 输入绑定与 Lane Cover | 进行中 | analog scratch cross-device 产品语义与真实 HID 验收 |

## Phase 1.1 皮肤系统专项

| 步骤 | 状态 | 说明 |
| --- | --- | --- |
| 1.1.1 默认皮肤包分层 | 已澄清 | Global + Mania + BMS 三层独立 |
| 1.1.2 组件矩阵与 lookup | 已文档化 | 可直接驱动开发的映射矩阵 |
| 1.1.3 资源命名与配置桥 | 已文档化 | mania legacy 兼容 + BMS 自有命名 |
| 1.1.4 Global provider / shell | 进行中 | host / provider / resource root / shared transformer / layout metadata / results contract 已落地；当前维持 release gate 稳定 |
| 1.1.5 Mania 第一批 | 进行中 | 5 类 shell 组件 + 10 类 preset 已接通；仍主要消费 legacy-derived assets |
| 1.1.6 Mania 第二批 | 进行中 | 8 类 OMS-owned 组件已升格；主要 runtime 语义已收口，剩余 legacy config/asset lookup 兼容与公开发行物收尾 |
| 1.1.7 BMS 第一批 | 已完成 | playfield / lane / hit target / bar line / static BG 的 lookup 与 OMS 默认层 |
| 1.1.8 BMS 第二批 | 已完成 | note / hold / LaneCover / judgement / combo 的 lookup 与 OMS 默认层 |
| 1.1.9 BMS 第三批 | 已完成 | HUD / gauge / results / Song Select panels 的 lookup 与 OMS 默认层 |
| 1.1.10 Partial override | 进行中 | mixed-layer 三类语义已有 runtime 证明；legacy 用户皮肤 component-level fallback 已接通 |
| 1.1.11 Native-default removal | 进行中 | built-in realm 注册面已瘦身；settings / runtime fallback / source-chain 已收口；公开发行物剥离待收尾 |
| 1.1.12 测试矩阵与 release gate | 进行中 | Mania skin 92/92、BMS 聚焦 111/111、osu.Game.Tests 18/18 已复核；BMS 全量 **812/812** 已于 2026-05-23 复核，mania 全量与 scratch bridge 继续沿用 2026-04-24 快照 |

执行优先顺序：维持 release gate 稳定 → 1.17 analog scratch cross-device edge/hold contract → 真实硬件验收。

## Phase 2 / Phase 3

| 阶段 | 状态 | 备注 |
| --- | --- | --- |
| Phase 2 | 阻塞 | 依赖 Phase 1 + Phase 1.1 先落地 |
| Phase 3 | 阻塞 | 依赖本地 BMS 主流程稳定；在线功能保持禁用 |

## 待人工操作验收

默认放在 Phase 1 阶段末尾统一执行，仅在构成阻塞时提前请求用户介入。

| 事项 | 状态 |
| --- | --- |
| 1.5 桌面端拖放导入 / Song Select UI 验收（含外部谱库 / 内部谱库分组展开与 fallback） | 待做 |
| 桌面端真实 UI smoke test | 已完成 |
| 便携发行物实际运行与覆盖更新验证 | 已完成 |

说明：Release publish 后 `portable.ini` 已验证会触发 `data/` 自动生成，目录结构正确；当前覆盖更新路径也已复核通过，但需要在程序完全退出后替换文件，并保留 `portable.ini`、便携模式下的 `data/` 以及任何自定义数据根使用的 `storage.ini`。

## 当前主线

以下主线全部归属于 **Phase 1.x 大主线**，仅用于执行编排；不表示项目已经正式进入 Phase 2。除阻塞修复外，Phase 2 / Phase 3 功能仍按冻结处理。

| 子主线 | 焦点 | 状态 |
| --- | --- | --- |
| P1-A 产品面与 release gate | Phase 1.1 皮肤专项 → 公开发行物皮肤收尾 | 进行中 |
| P1-I BMS 选歌筛选与搜索定制 | `I1` / `I2` / `I3` 已完成；BMS-only `谱面构成` / `键数` visual filter、custom search 与 persisted matching authority 已落地，公开搜索口径已统一为 `key/keys`、`rc/rice`、`ln`、`scr`（`regular` 仅保留兼容 alias），剩余单轨拖拽 headless regression 与 shared visual gate 收口 | 进行中（`I4`） |
| P1-B 输入语义与硬件验收 | analog scratch cross-device contract → 真实 HID 覆盖 | 进行中 |
| P1-C 判定语义与反馈闭环补强 | BEATORAJA / LR2 parity / FAST/SLOW / judge display / BMS 结果页反馈面 / visual timing-offset / EX pacemaker / 权威 GN 与调速反馈 / pre-start 1 号普通轨纯视觉流速预览 | 已阶段性收口（当前 feedback family、tri-mode/pre-start 链与 results-side consumer proof 已落地；BRJ/LR2 parity 与 richer judge display 后置 backlog） |
| P1-J BMS gameplay runtime 性能与音频时序治理 | shared keysound pool 时序 / dense-lane hot path / live channel resize 安全合同 / dense full autoplay replay 分流 | 进行中（`J1` / `J4` 已完成，`J2` / `J3` 首刀已落地，`J5` 自动化已闭合；full autoplay 专用 replay 分流与 keysound 预热已落地，剩余 once-per-run hitch 现场确认与人工验收） |
| P1-K BMS 解析链路治理 | decoder / normalized chart model / converter 语义 / projection reuse / parse-side cache | 已阶段性收口（`K1-A/K1-B/K2-A/K3-A/K3-B/K3-C/K3-D/K3-E/K3-F`、`K4`、`K5`、`K6`、`K7` 与 `K8` 数字层级已落地并整体收口；`K9` 已落地 dedicated converter、public gate、sample-only scratch runtime、persisted converted star、autoplay fix 与 spread-display read-model，剩余 wording / broader presentation/manual proof 未完工） |
| P1-D 控制器校准与诊断 | deadzone / sensitivity / scratch 模式说明 / live diagnostics | 下一优先级 |
| P1-E gameplay 与长条语义 | LN/CN/HCN 真实谱面验校 | 次优先级 |
| P1-F 首发离线发行基线 | portable.ini + data/ 便携模式已落地 | 已验证 |
| P1-G 人工验收后置 | 统一后置到 Phase 1 / 1.1 收口后 | 待做 |
| P1-H 存储拓扑支撑线 | chartmania/ 目录存储 + 外部/内部谱库重建与增量扫描 + portable.ini 便携模式；BMS 谱库分组与 external root snapshot 已接通，难度表一致性 / 刷新合同修补专题主链也已收口 | 已落地，剩余仅为后置诊断 / backlog |

## 遗留问题

### 高优先级

- **训练向 lane rearrangement 已落地**：`BmsModMirror` 与 `BmsModRandom`（`RANDOM` / `R-RANDOM` / `S-RANDOM` + 自定义 pattern）现已接入 BMS ruleset；当前 Phase 2 冻结重点已转向 `1P/2P flip` / `dan` / `FHS` / BSS / MSS 等更大范围能力
- **Phase 1.1 剩余**：mania 侧仍有 legacy config/asset lookup 兼容路径与公开发行物产品面收尾；维持 release gate 稳定后继续转向 1.17 输入与真实硬件验收
- **判定系统 parity 缺口**：当前 `BEATORAJA` / `LR2` / `IIDX` judge mode 已接通，但 `BEATORAJA` / `LR2` 仍缺 early/late 非对称窗口、scratch / long-note release 特例，以及按 judge family 参数化的 Empty Poor / excessive poor 触发语义；`IIDX` 也仍待进一步对齐细部体验
- **反馈闭环缺口**：results 页主评价 / 缩略徽章 / 主分数文案虽已切到 BMS 语义，但结果反馈面本身仍只完成第一轮收口；gameplay 侧当前已具备最近判定、瞬时 judge display、compact judgement summary、compact visual timing-offset、fixed AAA EX pacemaker 与 live `DJ LEVEL + EX %`，后续仍缺更完整 judge display 与更丰富 pacemaker 来源，尚未形成完整的 key-sounded BMS 训练闭环
- **权威绿色数字后续缺口**：常驻 GN HUD 与 C2 的 target-state / cycle / `HOLD` 语义已落地，C3 的最近判定 + `FAST/SLOW` 已具备瞬时 judge display 生命周期，并补上 compact judgement summary、compact visual timing-offset、fixed AAA EX pacemaker 与 live `DJ LEVEL + EX %`；后续剩余更完整 judge display 与 pacemaker 扩展仍待继续收口
- **gameplay hot path / 音频时序缺口**：`P1-J` 已从首轮 hot-path 收口继续推进到 dense full autoplay 专项：shared `BmsKeysoundStore` 的 gameplay keysound 已不再无条件 `Schedule()` 到下一帧，`BmsLane.shouldTriggerEmptyPoor()` 与 `BmsOrderedHitPolicy.getParticipatingHitObjects()` 已去掉首批热路径对象物化，`DrawableBmsHitObject.PlaySamples()` 已收口到单样本 keysound 路径，`KeysoundConcurrentChannels` live 改值也已从 rebuild-all 改成 non-destructive resize，并补上 `config -> drawable ruleset -> playfield shared store` 的 direct binding coverage；其后又加上 pause/seek 生命周期回收、player-level 音频语义 proof、`BmsReplayFrame` 缓存化、BMS-only full autoplay replay 分流，以及 full autoplay keysound sample pool 预热。当前主风险已不再是“50k 一进密段就明显慢放”，而是 dense real-chart 下是否仍残留 once-per-run 单次致命卡顿，以及 `P1-G` 下的人工 checklist 尚未完成。
- **解析链路治理缺口**：当前 parse chain 已先补上 raw carrier 的显式入口，并把 `SCROLLxx` 定义、unknown bag 与 `SC` 这类非十六进制 channel line 接进 no-loss 保留层；signed BPM、duplicate channel line compound、同拍位 `BPM -> STOP -> object` 顺序、`LNTYPE 2` 的最小 MGQ long-note expression、BGA / invisible / mine 的第一批 typed surface、`SCROLLxx/SC` 的 typed consumer contract、richer BGA-definition header family、unified visual-definition projection，以及 static background / Song Select note distribution / beatmap statistics / core-side metadata read-model 的 consumer reuse 也都已进入 parser/converter/import contract。当前剩余主缺口则收缩为更零散的 core/read-model 尾项，以及更广 special long-note parity。若不继续推进 `P1-K`，后续播放期优化、真实谱面验校与特效谱支持仍会建立在不完整的 parse projection 上。
- **控制器校准 / 诊断**：deadzone / sensitivity / scratch 模式说明 / live diagnostics 尚未落地；当前仅有 supplemental bindings 与 live capture，不足以覆盖 IIDX/BMS 控制器的一致性调校
- **难度表一致性 / 刷新合同**：manager-owned metadata sync、`RefreshAll` 真实结果合同、wrapper/source identity fallback、分批回写 / 进度反馈，以及 rebuild / reuse 命中旧 set 时的 metadata 自愈都已落地。2026-05-31 定位并修复**全 `Unrated`** 真根因：转谱星数（`BmsPersistedMetadataData`）与难度表（`BmsBeatmapMetadataData`）各自定义容器类却共用同一 `BeatmapMetadata.RulesetData` 列，`SetRulesetData<T>` 整体覆盖写互相抹掉对方字段（转谱星数重算冲掉难度表 entries → 全 Unrated；反向冲掉星数触发反复重算）；已用两侧 `[JsonExtensionData]` 往返保留修复（CONSTRAINTS #22）。同日早先误判为 carousel staleness 并加 per-set `DifficultyTableRevision` bump，因大库 UI 卡死已撤（中途开关表改为重启反映）。**判读顺序**：再现 `Unrated` 先确认**重启后是否仍 Unrated**——重启后正常属 carousel 中途未刷新（已知限制），重启后仍 Unrated 则查 RulesetData 字段是否被其它子系统覆盖、或原始 `.bms` 字节 MD5 与表项 MD5 不一致。
- **内置皮肤候选包**：`SimpleTou-Lazer` 仅为 mania 候选基线，不可提前对外描述为已完成
- **upstream 默认皮肤移除**：runtime fallback 已大部分收口到 OMS；剩余公开发行物剥离与 partial override 全路径收口
- **osu.Game.Tests 稳定性**：6/6 已恢复；后续扩大范围应沿 csproj exclusion 清单逐步清退
- **1.6 真实谱面长条验校**：Phase 1 最贴近玩法质量的剩余项
- **便携发行物实机验证**：portable.ini → data/ 与 single-file 冷启动已验证；剩余：内置皮肤发行门槛

### 中优先级

- **Windows HID 实机验收**：DirectInput backend 已接通，需真实 IIDX/BMS 控制器覆盖
- **存储拓扑**：portable.ini 便携模式已落地（data/ 子目录自包含）；chartmania/ 目录已落地；外部多目录谱库扫描与 Maintenance UI 已完成；剩余删除/失效语义、path identity dedup 与重扫策略
- **AutoMapper GHSA**：`NuGetAuditSuppress` + `NU1903 NoWarn` 已定点抑制，运行时 `MaxDepth(3)` 缓解攻击面；升级到 15.x 需 ~150 行 API 迁移 + Realm 操作全回归，暂维持现状
- **上游 cherry-pick 风险**：42 个 osu.Game 文件（40 修改 + 2 新增），其中 6 个属于高频改动区（详见 UPSTREAM.md）
- **密度星级标定**：已压到保守区间，需真实样本继续校准

### 低优先级

（当前无低优先级功能遗留；Release 构建已确认 `0 warning / 0 error`）

## 更新约定

- 优先更新"状态变化""遗留问题变化"和"一条最新验证快照"
- "最近一次验证"只保留最新一条；历史归 `CHANGELOG.md`
- Phase 1.1 执行顺序 / 门槛 / 候选包语义变化时必须与 `DEVELOPMENT_PLAN.md`、`README.md`、`SKINNING.md`、`RELEASE.md`、`OMS_COPILOT.md` 同步
