# P1-K 变更日志：BMS 解析链路治理

> 本文件记录 `P1-K` 相关的验证通过变更，按时间倒序排列。
> 当前进度见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，执行规划见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

---

## 2026-05-25

### K8：gauge history consumer proof 完成，auto-shift timeline state 写死

- [../../osu.Game.Rulesets.Bms/UI/BmsGaugeHistoryGraph.cs](../../osu.Game.Rulesets.Bms/UI/BmsGaugeHistoryGraph.cs) 现已让 `SkinnableBmsGaugeHistoryPanelDisplay` 暴露只读 history state，供 focused proof 直接读取 `CreateStatisticsForScore()` 生成的 gauge history 数据，而不再依赖 CLI 下不稳定的 skinnable scene 装载链。
- [../../osu.Game.Rulesets.Bms.Tests/BmsRulesetStatisticsTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsRulesetStatisticsTest.cs) 新增 `TestCreateStatisticsGaugeHistoryCarriesAutoShiftTimelineState()`，直接锁住 auto-shift `EX-HARD -> HARD -> NORMAL` timeline 与对应 sample/time/value 会端到端进入 gauge history consumer，而不是仅返回 `SkinnableBmsGaugeHistoryPanelDisplay` 的 panel type。
- 该 proof 也已明确写死 gauge history consumer 语义：results panel 必须直接消费 `BmsClearLampProcessor.CreateGaugeHistory()` 计算出的 timeline state，不得在 panel/UI 层重新拼装或简化成另一套 timeline。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsRulesetStatisticsTest.TestCreateStatistics"` **4/4** 通过。

### K7：results summary consumer proof 完成，clear-lamp 优先级写死

- [../../osu.Game.Rulesets.Bms/UI/BmsResultsSummaryDisplay.cs](../../osu.Game.Rulesets.Bms/UI/BmsResultsSummaryDisplay.cs) 现已让 `SkinnableBmsResultsSummaryPanelDisplay` 暴露只读 summary state，供 focused proof 直接读取 `CreateStatisticsForScore()` 生成的 results summary 数据，而不再依赖 CLI 下不稳定的 skinnable scene 装载链。
- [../../osu.Game.Rulesets.Bms.Tests/BmsRulesetStatisticsTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsRulesetStatisticsTest.cs) 新增 `TestCreateStatisticsSummaryCarriesSelectedModesAndClearLamp()`，直接锁住 gauge type / display name、gauge rules family、judge mode、long-note mode、EX-SCORE、DJ LEVEL 与 computed clear lamp 会端到端进入 summary consumer。
- 该 proof 也已明确写死 clear-lamp 优先级：clear check 通过后，`PERFECT` / `FULL COMBO` 仍会覆盖 gauge-derived lamp，因此 results summary consumer 不得按 gauge type 自行派生 `HAZARD CLEAR` 一类显示文本。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsRulesetStatisticsTest.TestCreateStatistics"` **3/3** 通过。

### K6：results-side focused validation 完成，已带 mods playable contract 写死

- [../../osu.Game/Rulesets/Ruleset.cs](../../osu.Game/Rulesets/Ruleset.cs) 已明确 `PrepareScoreInfoForResults()` 与 `CreateStatisticsForScore()` 接收的是“已应用所有相关 mods 的 playable beatmap”；[../../osu.Game.Rulesets.Bms/BmsRuleset.cs](../../osu.Game.Rulesets.Bms/BmsRuleset.cs) 与 [../../osu.Game.Rulesets.Bms/Scoring/BmsClearLampProcessor.cs](../../osu.Game.Rulesets.Bms/Scoring/BmsClearLampProcessor.cs) 现已按此 contract 消费 caller 传入的 beatmap，不再在 results/gauge helper 内再次调用 `BmsBeatmapModApplicator`。
- [../../osu.Game.Rulesets.Bms.Tests/BmsPlayableBeatmapCacheTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsPlayableBeatmapCacheTest.cs) 新增 `Mirror` dedicated focused proof，直接锁住 `PrepareScoreInfoForResults()` 不会对已带 mods 的 playable beatmap 重复应用 beatmap mods；该 suite 基线现为 **5/5**。
- [../../osu.Game.Rulesets.Bms.Tests/BmsClearLampProcessorTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsClearLampProcessorTest.cs) 新增两条 `Mirror` focused proofs，分别锁住 `CreateGaugeHistory()` 与 `CalculateFinalGauge()` 不会对已带 mods 的 playable beatmap 重复应用 beatmap mods；依赖 long-note / assist 语义的 HCN、autoplay 邻接用例也已改为显式先应用 score mods，再进入 clear-lamp helper。该 suite 基线现为 **32/32**。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsPlayableBeatmapCacheTest"` **5/5** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsClearLampProcessorTest"` **32/32** 通过。

### K5：让 parse-side playable projection 收口为 source-bound cache contract

- [../../osu.Game/Beatmaps/ICachedModlessPlayableBeatmapSource.cs](../../osu.Game/Beatmaps/ICachedModlessPlayableBeatmapSource.cs) 现已定义 source-bound 的 modless playable cache contract；[../../osu.Game/Beatmaps/WorkingBeatmap.cs](../../osu.Game/Beatmaps/WorkingBeatmap.cs) 则会在 `GetPlayableBeatmap()` 中优先复用实现该 contract 的 source beatmap 上已缓存的无 mods playable projection，只有换 source 或带 mods 时才重新转换。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedBeatmap.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedBeatmap.cs) 现已按 ruleset short name 持有 modless playable cache，而 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs) 也会把 loader 首次 conversion 的现成 projection seed 回 source wrapper；同时 factory seed 现已补齐 no-mod finalize，不会再把“只 convert、未生成 hold nested objects”的半成品 playable 缓进 source beatmap。
- [../../osu.Game.Rulesets.Bms.Tests/BmsPlayableBeatmapCacheTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsPlayableBeatmapCacheTest.cs) 新增 dedicated focused proof，锁住同源复用、跨 source 隔离、带 mods 绕过缓存，以及 loader-seeded cache 返回的 hold-note projection 已完成 finalize；相邻 loader-focused `BmsImportIntegrationTest` 回归也已继续确认 import metadata / timing 合同未因 cache seed 回归。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsPlayableBeatmapCacheTest"` **4/4** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsImportIntegrationTest.TestLoader"` **9/9** 通过。

### K4-S：让 set-level artist display 复用 shared artist authority

- [../../osu.Game/Beatmaps/BeatmapSetInfoExtensions.cs](../../osu.Game/Beatmaps/BeatmapSetInfoExtensions.cs) 现已新增 `GetDisplayArtistRomanisable()` shared helper：当 beatmap set 持有具体 beatmap 时，优先复用首个 beatmap 的 display artist authority，只在没有 beatmap 时才回退到 set metadata 的 raw artist text。
- [../../osu.Game/Screens/Select/PanelBeatmapSet.cs](../../osu.Game/Screens/Select/PanelBeatmapSet.cs) 与 [../../osu.Game/Overlays/Music/PlaylistItem.cs](../../osu.Game/Overlays/Music/PlaylistItem.cs) 现已通过 `BeatmapSetInfo.GetDisplayArtistRomanisable()` 显示 set-level artist，不再继续直接走 raw `beatmapSet.Metadata.Artist` / `ArtistUnicode`；因此 Song Select set panel 与 playlist tray 都不会再暴露 raw `/obj:` 后缀。
- [../../osu.Game.Tests/Menus/BeatmapSetArtistLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Menus/BeatmapSetArtistLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 BMS artist clean 与 non-BMS passthrough contract；因此 set-level artist display surface 现已具备独立 plain focused proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BeatmapSetArtistLocalMetadataDisplayTest"` **2/2** 通过。

### K4-F follow-up：补齐 BeatmapAttributeText plain focused proof

- [../../osu.Game/Skinning/Components/BeatmapAttributeText.cs](../../osu.Game/Skinning/Components/BeatmapAttributeText.cs) 现已补出 `GetDisplayedArtist()` 与 `GetDisplayedCreator()` internal helper，让 shared beatmap-attribute display consumer 的 artist / creator 读口可以直接复用组件内 authority，并脱离 CLI scene discoverability 做最窄 plain proof。
- [../../osu.Game.Tests/Skins/BeatmapAttributeTextLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Skins/BeatmapAttributeTextLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 BMS artist clean、creator fallback 与 non-BMS passthrough contract；因此 `BeatmapAttributeText` 现已具备独立 plain focused proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BeatmapAttributeTextLocalMetadataDisplayTest"` **2/2** 通过。

### K4-R：让 delete confirmation title display 复用 set-level title authority

- [../../osu.Game/Beatmaps/BeatmapSetInfoExtensions.cs](../../osu.Game/Beatmaps/BeatmapSetInfoExtensions.cs) 现已新增 shared set-level title helper：当 beatmap set 持有具体 beatmap 时，优先复用首个 beatmap 的 full title authority，并允许各个 set-level surface 显式保持是否展示 creator 的既有合同。
- [../../osu.Game/Screens/Select/BeatmapDeleteDialog.cs](../../osu.Game/Screens/Select/BeatmapDeleteDialog.cs) 现已通过 `BeatmapSetInfo.GetDisplayTitleRomanisable(includeCreator: false)` 显示删除确认标题，不再继续直接走 `beatmapSet.Metadata.GetDisplayTitleRomanisable(false)`；因此 delete confirmation title 不再暴露 raw `/obj:` 后缀，也不会误带 difficulty name，同时继续保持不展示 creator suffix 的既有外观。
- [../../osu.Game.Tests/Menus/BeatmapDeleteDialogLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Menus/BeatmapDeleteDialogLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 BMS fallback、non-BMS passthrough、“无 creator 泄漏”与“无难度名泄漏”的 contract；[../../osu.Game.Tests/Menus/ScopedBeatmapSetDisplayLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Menus/ScopedBeatmapSetDisplayLocalMetadataDisplayTest.cs) 也已补强相邻 shared-helper contract 的难度名断言。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BeatmapDeleteDialogLocalMetadataDisplayTest|FullyQualifiedName~ScopedBeatmapSetDisplayLocalMetadataDisplayTest"` **4/4** 通过。

## 2026-05-23

### K4-Q：让 Daily Challenge title display 复用 full-beatmap title authority

- [../../osu.Game/Screens/OnlinePlay/DailyChallenge/DailyChallengeIntro.cs](../../osu.Game/Screens/OnlinePlay/DailyChallenge/DailyChallengeIntro.cs) 现已让 daily challenge title display 在可拿到具体 beatmap 时优先调用 `IBeatmapInfo.GetDisplayTitleRomanisable(includeDifficultyName: false)`，不再继续直接走 `beatmap.BeatmapSet!.Metadata.GetDisplayTitleRomanisable(false)`，因此不会再暴露 raw `/obj:` 后缀，也不会把难度名重新带回标题行。
- [../../osu.Game.Tests/OnlinePlay/DailyChallengeLocalMetadataDisplayTest.cs](../../osu.Game.Tests/OnlinePlay/DailyChallengeLocalMetadataDisplayTest.cs) 现已把 plain NUnit focused proof 扩展到 creator fallback 与 title authority 两侧，同时锁住 BMS fallback、non-BMS passthrough 与“无难度名泄漏”的 contract；当 visual scene 主要覆盖转场时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~DailyChallengeLocalMetadataDisplayTest"` **4/4** 通过。

### K4-P：让 scoped beatmap-set title display 复用 full-beatmap title authority

- [../../osu.Game/Screens/Select/FilterControl.ScopedBeatmapSetDisplay.cs](../../osu.Game/Screens/Select/FilterControl.ScopedBeatmapSetDisplay.cs) 现已让 scoped beatmap set title display 在能拿到具体 beatmap 时优先调用 `IBeatmapInfo.GetDisplayTitleRomanisable(includeDifficultyName: false)`，只在空 set 时才回退到 metadata-only overload，因此 scoped-set banner 不再暴露 raw `/obj:` 后缀，也不会误带难度名。
- [../../osu.Game.Tests/Menus/ScopedBeatmapSetDisplayLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Menus/ScopedBeatmapSetDisplayLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住首个 beatmap authority reuse、BMS fallback、non-BMS passthrough 与“无难度名泄漏”的 contract；当 set-level UI 只是转发标题字符串时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~ScopedBeatmapSetDisplayLocalMetadataDisplayTest"` **2/2** 通过。

### K4-O：让 IBeatmapInfo title display 复用 display artist / creator fallback

- [../../osu.Game/Beatmaps/BeatmapInfoExtensions.cs](../../osu.Game/Beatmaps/BeatmapInfoExtensions.cs) 现已让 `IBeatmapInfo.GetDisplayTitleRomanisable()` 同时通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()` / `GetDisplayCreator()` 读取 BMS local metadata display authority，不再在 title display consumer 上直接暴露 embedded creator suffix 或空 creator。
- [../../osu.Game.Tests/Localisation/BeatmapInfoRomanisationLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Localisation/BeatmapInfoRomanisationLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 BMS fallback 与非 BMS passthrough；当具体 UI 只是转发 title string 时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BeatmapInfoRomanisationLocalMetadataDisplayTest"` **2/2** 通过。

### K4-N：让 beatmap skin metadata 复用 display creator fallback

- [../../osu.Game/Skinning/LegacyBeatmapSkin.cs](../../osu.Game/Skinning/LegacyBeatmapSkin.cs) 现已让 beatmap skin metadata 的 `SkinInfo.Creator` 通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayCreator()` 读取 BMS local creator fallback，不再继续直接展示 raw `Metadata.Author.Username`。
- [../../osu.Game.Tests/Skins/LegacyBeatmapSkinLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Skins/LegacyBeatmapSkinLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 beatmap skin metadata 的 creator 读口；当 beatmap skin 只通过 `SkinInfo` 暴露 metadata 时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~LegacyBeatmapSkinLocalMetadataDisplayTest"` **2/2** 通过。

### K4-M：让 matchmaking round results 优先复用本地 BeatmapInfo

- [../../osu.Game/Screens/OnlinePlay/Matchmaking/Match/RoundResults/SubScreenRoundResults.cs](../../osu.Game/Screens/OnlinePlay/Matchmaking/Match/RoundResults/SubScreenRoundResults.cs) 现已在按 API scores 构造 `ScoreInfo` 时优先复用本地 `BeatmapInfo`，仅在本地谱面缺失时才回退到 API 最小壳，从而保住 round-results `ScorePanel` / `ExpandedPanelMiddleContent` 已接好的 BMS local metadata display authority。
- [../../osu.Game.Tests/OnlinePlay/SubScreenRoundResultsLocalMetadataDisplayTest.cs](../../osu.Game.Tests/OnlinePlay/SubScreenRoundResultsLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 local beatmap reuse 与 API fallback shell；当 visual scene 只看到最终 `ScorePanel` 内容时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~SubScreenRoundResultsLocalMetadataDisplayTest"` **2/2** 通过。

### K4-L：让 online playlist creator display 复用 display creator fallback

- [../../osu.Game/Screens/OnlinePlay/DrawableRoomPlaylistItem.cs](../../osu.Game/Screens/OnlinePlay/DrawableRoomPlaylistItem.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayCreator()` / `HasLinkedCreatorProfile()` 读取 BMS local creator fallback，不再因空 `Metadata.Author.Username` 隐藏作者行；有真实作者资料时继续保留 user link，没有时回退为 plain text creator。
- [../../osu.Game.Tests/OnlinePlay/DrawableRoomPlaylistItemLocalMetadataDisplayTest.cs](../../osu.Game.Tests/OnlinePlay/DrawableRoomPlaylistItemLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 creator 文本与 linked-profile 分支；当 visual scene 难以稳定断言 user link 行为时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~DrawableRoomPlaylistItemLocalMetadataDisplayTest"` **2/2** 通过。

### K4-K：让 daily challenge creator display 复用 display creator fallback

- [../../osu.Game/Screens/OnlinePlay/DailyChallenge/DailyChallengeIntro.cs](../../osu.Game/Screens/OnlinePlay/DailyChallenge/DailyChallengeIntro.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayCreator()` 读取 BMS local creator fallback，不再在 daily challenge metadata surface 内继续直接展示 raw local creator。
- [../../osu.Game.Tests/OnlinePlay/DailyChallengeLocalMetadataDisplayTest.cs](../../osu.Game.Tests/OnlinePlay/DailyChallengeLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 creator 读口；当 visual scene 主要覆盖转场时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~DailyChallengeLocalMetadataDisplayTest"` **2/2** 通过。

### K4-J：让 menu metadata display 复用 display artist fallback

- [../../osu.Game/Screens/Menu/SongTicker.cs](../../osu.Game/Screens/Menu/SongTicker.cs) 与 [../../osu.Game/Overlays/NowPlayingOverlay.cs](../../osu.Game/Overlays/NowPlayingOverlay.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()` 读取 BMS local artist fallback，不再在 menu / now-playing metadata surface 内继续直接展示 raw local artist。
- [../../osu.Game.Tests/Menus/MenuBeatmapMetadataLocalDisplayTest.cs](../../osu.Game.Tests/Menus/MenuBeatmapMetadataLocalDisplayTest.cs) 新增 plain NUnit focused test，直接锁住两个 surface 的 artist 读口；当 visual scene 没有直接暴露 metadata 断言时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~MenuBeatmapMetadataLocalDisplayTest"` **2/2** 通过。

### K4-I：让 profile metadata display 复用 display artist fallback

- [../../osu.Game/Overlays/Profile/Sections/Ranks/DrawableProfileScore.cs](../../osu.Game/Overlays/Profile/Sections/Ranks/DrawableProfileScore.cs) 与 [../../osu.Game/Overlays/Profile/Sections/Historical/DrawableMostPlayedBeatmap.cs](../../osu.Game/Overlays/Profile/Sections/Historical/DrawableMostPlayedBeatmap.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()` 读取 BMS local artist fallback，不再在 profile beatmap metadata surface 内继续直接展示 raw local artist。
- [../../osu.Game.Tests/Online/ProfileBeatmapMetadataLocalDisplayTest.cs](../../osu.Game.Tests/Online/ProfileBeatmapMetadataLocalDisplayTest.cs) 新增 plain NUnit focused test，直接锁住两个 surface 的 artist 读口；当 `TestSceneUserProfileScores` 与 `TestSceneHistoricalSection` 这类 visual scene 在 CLI 下不可 discover 时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~ProfileBeatmapMetadataLocalDisplayTest"` **2/2** 通过。

### K4-H：让 results metadata display 复用 display artist / creator fallback

- [../../osu.Game/Screens/Ranking/Expanded/ExpandedPanelMiddleContent.cs](../../osu.Game/Screens/Ranking/Expanded/ExpandedPanelMiddleContent.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()` / `GetDisplayCreator()` 读取 BMS local artist / creator fallback，不再在 results screen 的 expanded metadata surface 内继续直接展示 raw local metadata。
- [../../osu.Game.Tests/Scores/ExpandedPanelMiddleContentLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Scores/ExpandedPanelMiddleContentLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 artist / creator 读口；当 `TestSceneExpandedPanelMiddleContent` 这类 visual scene 在 CLI 下不可 discover 时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~ExpandedPanelMiddleContentLocalMetadataDisplayTest"` **2/2** 通过。

### K4-G：让 gameplay metadata display 复用 display artist / creator fallback

- [../../osu.Game/Screens/Play/BeatmapMetadataDisplay.cs](../../osu.Game/Screens/Play/BeatmapMetadataDisplay.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()` / `GetDisplayCreator()` 读取 BMS local artist / creator fallback，不再在 gameplay loading surface 内直接展示 raw local metadata。
- [../../osu.Game.Tests/Visual/Gameplay/TestSceneBeatmapMetadataDisplay.cs](../../osu.Game.Tests/Visual/Gameplay/TestSceneBeatmapMetadataDisplay.cs) 现改用组件 internal readback 锚点锁住 display text，避免继续依赖不稳定的 scene 树遍历断言；focused validation 也固定为整类 `TestSceneBeatmapMetadataDisplay` filter，而不是宽泛匹配 `TestLocal`。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~TestSceneBeatmapMetadataDisplay"` **8/8** 通过。

### K4-F：让 local-metadata display consumer 复用 display artist / creator fallback

- [../../osu.Game/Screens/Select/BeatmapCarouselFilterSorting.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterSorting.cs)、[../../osu.Game/Screens/Select/BeatmapCarouselFilterGrouping.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterGrouping.cs) 与 [../../osu.Game/Screens/Select/BeatmapCarouselFilterMatching.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterMatching.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()` 读取 BMS local artist fallback，不再把 embedded creator suffix 暴露给 Song Select 的 artist sort/group/filter。
- [../../osu.Game/Skinning/Components/BeatmapAttributeText.cs](../../osu.Game/Skinning/Components/BeatmapAttributeText.cs) 现也通过 `BeatmapLocalMetadataDisplayResolver` 统一读取 BMS local artist / creator display text，不再在 shared beatmap-attribute display consumer 内直接使用 raw `Metadata.Artist` / `Metadata.Author.Username`。
- [../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterSortingTest.cs](../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterSortingTest.cs)、[../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterGroupingTest.cs](../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterGroupingTest.cs)、[../../osu.Game.Tests/NonVisual/Filtering/FilterMatchingTest.cs](../../osu.Game.Tests/NonVisual/Filtering/FilterMatchingTest.cs) 与 [../../osu.Game.Tests/Beatmaps/BeatmapLocalMetadataDisplayResolverTest.cs](../../osu.Game.Tests/Beatmaps/BeatmapLocalMetadataDisplayResolverTest.cs) 已锁住这条 artist selector reuse path。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "(FullyQualifiedName~TestSortingByArtistUsesBmsDisplayArtistFallback|FullyQualifiedName~TestGroupingByArtist|FullyQualifiedName~TestCriteriaMatchingArtistDoesNotMatchBmsCreatorSuffix|FullyQualifiedName~TestCriteriaMatchingArtistWithNullUnicodeName|FullyQualifiedName~TestCriteriaNotMatchingArtist|FullyQualifiedName~TestDisplayArtistStripsEmbeddedBmsCreator)"` **9/9** 通过；相邻 `BeatmapAttributeText` plain focused proof 已于 `2026-05-25` 由 `BeatmapAttributeTextLocalMetadataDisplayTest` **2/2** 补齐。

### K4-E：让 Song Select creator selector 复用 display creator fallback

- [../../osu.Game/Screens/Select/BeatmapCarouselFilterSorting.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterSorting.cs)、[../../osu.Game/Screens/Select/BeatmapCarouselFilterGrouping.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterGrouping.cs) 与 [../../osu.Game/Screens/Select/BeatmapCarouselFilterMatching.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterMatching.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayCreator()` 读取 BMS local creator fallback，不再只按 `Metadata.Author.Username` 做 Song Select 的 author sort/group/filter。
- [../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterSortingTest.cs](../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterSortingTest.cs)、[../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterGroupingTest.cs](../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterGroupingTest.cs) 与 [../../osu.Game.Tests/NonVisual/Filtering/FilterMatchingTest.cs](../../osu.Game.Tests/NonVisual/Filtering/FilterMatchingTest.cs) 已锁住这条 selector reuse path。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "(FullyQualifiedName~TestSortingByAuthorUsesBmsDisplayCreatorFallback|FullyQualifiedName~TestGroupingByAuthorUsesBmsDisplayCreatorFallback|FullyQualifiedName~TestCriteriaMatchingCreatorUsesBmsDisplayCreatorFallback)"` **3/3** 通过。

### K4-D：让 core metadata read-model 复用 persisted chart_metadata projection

- [../../osu.Game/Beatmaps/BmsPersistedMetadataResolver.cs](../../osu.Game/Beatmaps/BmsPersistedMetadataResolver.cs) 现为 `osu.Game` 提供统一的 typed persisted `chart_metadata` projection，避免 core consumer 各自手拆 `RulesetDataJson`。
- [../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs](../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs) 与 [../../osu.Game/Beatmaps/BmsStarRatingResolver.cs](../../osu.Game/Beatmaps/BmsStarRatingResolver.cs) 现已共享这条读取路径，不再各自维护 `JObject.SelectToken("chart_metadata...")` 的 stringly-typed token 合同。
- [../../osu.Game.Tests/Beatmaps/BeatmapLocalMetadataDisplayResolverTest.cs](../../osu.Game.Tests/Beatmaps/BeatmapLocalMetadataDisplayResolverTest.cs) 与 [../../osu.Game.Tests/Beatmaps/BmsStarRatingResolverTest.cs](../../osu.Game.Tests/Beatmaps/BmsStarRatingResolverTest.cs) 已锁住这条 core-side persisted metadata reuse path。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "(FullyQualifiedName~BmsStarRatingResolverTest|FullyQualifiedName~BeatmapLocalMetadataDisplayResolverTest)"` **11/11** 通过。

### K4-C：让 beatmap statistics 复用 metadata 中的 chart-filter projection

- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs) 现会在 `GetStatistics()` 中优先读取 `BeatmapInfo.Metadata.GetChartFilterStats()`，只有缺失时才回退到 `BmsChartFilterStats.FromBeatmap(this)`。
- 同一处 consumer 在缺失 `ChartFilterStats` 时会把现场计算结果写回 metadata，避免同一 runtime beatmap 反复本地重数同一份 projected hitobjects。
- [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapStatisticsTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapStatisticsTest.cs) 已补上 focused regressions，锁住“优先复用 metadata / 缺失时回写缓存”的 statistics consumer 选择逻辑。
- 验证：`BmsBeatmapStatisticsTest` **3/3** 通过，`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal` **812/812** 通过。

## 2026-05-22

### K4-B：让 note distribution graph 复用 projected working beatmap

- [../../osu.Game.Rulesets.Bms/SongSelect/BmsNoteDistributionGraph.cs](../../osu.Game.Rulesets.Bms/SongSelect/BmsNoteDistributionGraph.cs) 新增 `ResolveBeatmapForAnalysis()`，会在 working beatmap 的 source beatmap 已携带 `BmsHitObject` projection 时直接复用它，只在缺失时才回退到 `GetPlayableBeatmap()`。
- 同一文件的 note-distribution 数据构造现统一从 `BeatmapInfo.Metadata` 读取 `ChartMetadata`，使 projected source beatmap 与 playable beatmap 继续共享同一摘要来源，而不是再依赖 consumer-local second conversion。
- [../../osu.Game.Rulesets.Bms.Tests/BmsNoteDistributionGraphTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsNoteDistributionGraphTest.cs) 已补上两条 focused regressions，锁住“优先复用 projected source beatmap / 无 projection 时回退 playable conversion”的选择逻辑。
- 验证：`BmsNoteDistributionGraphTest` **5/5** 通过，`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal` **810/810** 通过。

### K4-A：让 static background 首次复用 unified projection

- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs) 新增 `GetPreferredBackgroundAssetReference()`，统一选择 `STAGEFILE/BACKBMP/BANNER` 或 richer visual-definition family 的首个 bitmap；若 `#BGA/#@BGA` 持有的是两位 bitmap reference，还会先通过 `BitmapTable` 解析回实际资源名。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs) 与 [../../osu.Game.Rulesets.Bms/UI/BmsBackgroundLayer.cs](../../osu.Game.Rulesets.Bms/UI/BmsBackgroundLayer.cs) 现已共享这条 background asset projection，使 metadata background、导入后的图片正规化与 playfield static background consumer 不再各自只认 `STAGEFILE/BACKBMP/BANNER`。
- 这一步把 richer visual-definition family 的首个 consumer 真正接到了运行中的 static background 路径上，并顺手修正了一个常见坑：不能把 `#BGA/#@BGA` 的两位引用直接当文件名，必须先过 `BitmapTable`。
- 验证：新增 static-background targeted regressions **3/3** 通过，`BmsBeatmapConverterTest` **13/13** 通过，`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal` **808/808** 通过。

### K3-F：补齐 unified visual-definition projection contract

- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsVisualDefinitions.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsVisualDefinitions.cs) 现新增 `BmsVisualDefinitionProjection`；[../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs) 也新增 `GetVisualDefinitionProjections()` 与 `TryGetVisualDefinitionProjection()`，把 `#BGA`、`#@BGA`、`#ARGB`、`#SWBGA` 与 `#POORBGA` 的分散 header tables 收口为统一组合视图。
- 本轮仍严格限制在 decoder/model：原始 definition tables 继续保留，新的 projection 只是把同 index 的 header family 组合给下游读取，不改 converter、importer、Song Select 与 runtime visual consumer。
- 这一步把 richer visual-definition family 的“projection contract”正式冻结下来；剩余 gap 已从“如何组合四张表”收窄到“哪个 consumer 先采用这条统一投影”。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsBeatmapDecoderTest"` **33/33** 通过，`BmsBeatmapConverterTest` **12/12** 通过，`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal` **805/805** 通过。

### K3-E：补齐 richer BGA-definition header typed surface

- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsVisualDefinitions.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsVisualDefinitions.cs) 现新增 richer BGA-definition header family 的 typed model；[../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs) 也新增 `BgaDefinitions`、`AtBgaDefinitions`、`ArgbDefinitions`、`SwBgaDefinitions` 与 `PoorBgaMode`，让 `#BGA`、`#@BGA`、`#ARGB`、`#SWBGA`、`#POORBGA` 不再只停留在 generic unknown bag。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs) 现会把上述 header 解析进 typed surface，并保留 `#BGA/#@BGA` 的 bitmap/reference 原始 token，不提前把 bitmap 绑定、动画调度或运行时播放语义锁死在 decoder。
- 本轮仍严格限制在 decoder/model；converter、importer、Song Select 与 runtime visual consumer 均未改动。K3-E 只负责把 header-side definition surface 冻结下来，把剩余 gap 收窄到 consumer/projection 层。
- 验证：`BmsBeatmapDecoderTest` **32/32** 通过，`BmsBeatmapConverterTest` **12/12** 通过，`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal` **804/804** 通过。

### K3-D：补齐 `SCROLLxx/SC` 的 typed consumer contract

- `BmsDecodedChart` 现新增 `ScrollEvents`；`BmsBeatmapDecoder` 会把 `SCROLLxx` 定义 + `SC` channel line 解析成 typed scroll surface，而不再只把它们留在 `ScrollTable` 与 raw placeholder 层。
- 为避免 `SC` 与其它 unknown channel 在同拍位被错误 compound，decoder 的 unknown-channel duplicate 键现已按 `RawChannelToken` 区分；`SC` 不会再因共享 `channel = -1` 而被其它未知轨覆盖掉。
- `BmsBeatmapConverter` 现已把 `ScrollEvents` 接到 `ControlPointInfo.EffectPoints`，让 `SCROLLxx/SC` 首次进入 runtime scroll-speed consumer contract，同时不改 importer、Song Select 或现有 visual consumer。
- 验证：`BmsBeatmapDecoderTest` **31/31** 通过，`BmsBeatmapConverterTest` **12/12** 通过，`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal` **803/803** 通过。

### K3-C：补齐 BGA / invisible / mine 的最薄 typed surface

- `BmsDecodedChart` 现新增 `BgaEvents`、`InvisibleObjectEvents` 与 `MineEvents`；`BmsBeatmapDecoder` 也已对 BGA base/poor/layer/layer2、invisible object 与 landmine channel 建立 typed post-process 分派，不再要求下游从 raw carrier 重新猜 channel 语义。
- 本轮只收口 parse/model additive surface，不改 converter、importer、runtime 或现有 visual consumer；第一批 typed slot 先为后续背景层、统计面与特效谱支持冻结中间模型合同。
- `BmsBeatmapDecoderTest` 已新增 BGA / invisible / mine 三条 parser focused regression；为确认更宽基线没有被新 typed surface 破坏，本轮还追加重跑了 `BmsBeatmapConverterTest` 与全量 BMS suite。
- 验证：`BmsBeatmapDecoderTest` **29/29** 通过，`BmsBeatmapConverterTest` **11/11** 通过，`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal` **800/800** 通过。

### K3-B：补齐 LNTYPE 2 的最小 MGQ long-note expression

- `BmsBeatmapDecoder` 现会在 LN channel 保留显式 `00` 作为 `LNTYPE 2` 的 closing marker，并把 duplicate line compound 收口为“`00` 不覆盖已有对象”；这让 MGQ 长条可以跨小节连续，并在 zero slot 处收口，而不再停留在 warning-only。
- `BmsLongNoteEncoding` 新增 `LnType2`，`BmsLongNoteEvent` 的 carrier 注释也已扩到 `LNTYPE` 全族；decoder focused regressions 现覆盖跨小节配对与 duplicate zero 不覆盖已有 segment 两条关键语义。
- `BmsBeatmapConverterTest` 现已补上 end-to-end proof，证明 `LNTYPE 2` 的最小表达可直接沿既有 hold-note conversion path 转成 `BmsHoldNote`。
- 验证：`BmsBeatmapDecoderTest` **26/26** 通过，`BmsBeatmapConverterTest` **11/11** 通过。

### K3-A：冻结同拍位 control-event 顺序与 signed BPM converter contract

- `BmsBeatmapConverter` 现会先应用同拍位 `BPM` 与 `STOP`，再结算 object / long-note endpoint 的 event time；converter authority 现固定为 `BPM -> STOP -> object`，不再让 consumer 各自猜顺序。
- signed BPM 的 timeline 推进现按绝对值消费；negative `#BPMxx` 不再在 converter 里被 `Math.Max(1, bpm)` 错误钳成 `1 BPM`。
- `BmsBeatmapConverterTest` 已新增 same-position control-event order 与 signed BPM timing regressions；本轮 focused suite **8/8** 通过。

### K2-A：signed BPM 与 duplicate channel compound 进入 parser contract

- `BmsBpmChangeEvent` 现允许 non-zero signed BPM 进入 typed model，`BmsBeatmapDecoder` 也会保留 negative `#BPMxx`，不再在 parser 阶段直接把方向信息拒掉。
- decoder 的 typed post-process 现已对同 `measure/channel/fraction` 的 duplicate channel collision 做 source-order-aware compound；raw carrier 继续完整保留全部原始 channel events。
- `BmsBeatmapDecoderTest` 已新增 signed BPM 与 duplicate channel focused regression；本轮 focused suite **23/23** 通过。

### K1-B：scroll 定义、unknown bag 与非十六进制 channel raw placeholder 落地

- `BmsBeatmapInfo` 现新增 `ScrollTable` 与 `UnknownHeaders`；`BmsBeatmapDecoder` 会保留 `#SCROLLxx` 定义，并把未识别的 header / indexed definition 写入 unknown bag，而不是继续静默跳过。
- decoder 现也会接受非十六进制 channel token，并将其作为 raw placeholder 写入 `RawChannelEvents`；这让 `SC` 这类 channel line 至少可以 no-loss 回收，而不会在 parser 入口直接丢失。
- `BmsBeatmapDecoderTest` 已新增 `SCROLLxx/SC` 与 unknown bag focused regression；本轮 focused suite **21/21** 通过。

### K1-A：raw channel carrier 与 source line order 首刀落地

- `BmsDecodedChart` 现已显式暴露 `RawChannelEvents`，并保留 `ChannelEvents` 作为兼容别名；raw channel carrier 不再只是隐式 fallback 列表。
- `BmsChannelEvent` 现新增 `RawChannelToken` 与 `SourceLineOrder`；`BmsBeatmapDecoder` 会按 source channel line 填充这两个字段，并以 `SourceLineOrder` 作为同 `measure/fraction/channel` 下的最终 tie-break。
- `BmsBeatmapDecoderTest` 已新增 raw carrier focused regression，验证 `RawChannelEvents`、`RawChannelToken` 与 `SourceLineOrder` 的首轮合同；本轮 focused suite **20/20** 通过。

### 文档：补齐 P1-K 的依赖与回退边界

- `DEVELOPMENT_PLAN.md` 现新增“依赖与回退边界”表，把 `K1-A` 到 `K4-A` 的进入前提、失败信号、允许回退与明确禁止项固定下来。
- `TECHNICAL_CONSTRAINTS.md` 现新增回退约束，明确失败后只能收缩新增暴露面，不能把 no-loss carrier、source line order 或 focused regression 一并删掉。
- `DEVELOPMENT_STATUS.md` 现明确记录：当前文档层面已经足以独立驱动 `K1-A` 开工，剩余开放项属于实现期决策而非规划缺口。

### 文档：把 P1-K 扩写成可直接开工的执行包

- `P1-K` 的 `DEVELOPMENT_PLAN.md` 现已补齐文件级切片图、首轮开工顺序、focused test 落点、推荐验证命令与“何时算可以直接开工”的进入条件，不再只是方向性规划。
- `TECHNICAL_CONSTRAINTS.md` 现新增切片边界约束，明确 `K1-K3` 首轮只允许改 in-memory parse chain，`K4` 之后才触碰 projection reuse 与 importer/raw-wrapper consumer。
- `DEVELOPMENT_STATUS.md` 现新增首轮开工包，把 `K1-A` 到 `K4-A` 的主文件、目标与每刀验证顺序固定下来，后续可以直接照文档执行。

### 文档：新建 P1-K 子线并冻结 BMS 解析链路治理范围

- 已新建 `P1-K` 四件套，并把 **BMS 解析链路治理** 正式归入 Phase 1.x 子线编排；主 authority 明确落在 decoder、normalized chart model、converter 语义、projection reuse 与 parse-side cache。
- 主线总规划、主线状态页、主线变更日志与子线索引已同步加入 `P1-K`，首轮执行顺序冻结为：`raw/typed 双层模型冻结` → `header/definition/channel no-loss coverage` → `timeline/control-event semantics` → `parse-once/project-many 复用` → `focused validation 与缓存边界`。
- 本轮同时把当前 parse-chain 的主要 gap 写入子线基线：`SCROLLxx/SC` 未进入模型、signed BPM typed surface 不可表示、duplicate channel line 未 compound、同拍位 `BPM/STOP/object` 顺序未冻结，以及 BGA layer / mine / invisible note 仍缺最薄 typed slot。
- 本轮仅完成文档规划与主线编排，无生产代码改动、无新增测试执行；代码与验证基线继续沿用主线同日 `788/788` 快照。
