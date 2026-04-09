# Upstream Lock

| Field | Value |
|---|---|
| Repository | https://github.com/ppy/osu |
| Tag | `2026.305.0-lazer` |
| Commit | `bb289363a2b8e6bf62be355f8570def018f0d7be` |
| Date | 2026-03-05 |

## Sync Policy

- Do **not** blindly pull upstream changes.
- Cherry-pick critical bug fixes selectively.
- Verify cherry-picks do not conflict with BMS ruleset or removed rulesets.
- Re-evaluate upstream sync every 3 months.

## OMS Modified Files in osu.Game

> 以下文件被 OMS 直接修改或新增，上游 cherry-pick 时需逐一检查冲突。
> 最后更新：2026-04-05

### 新增文件（上游不存在）

| 文件 | 说明 |
|---|---|
| `osu.Game/Online/API/LocalOfflineAPIAccess.cs` | 离线 API provider，替代 `APIAccess` |
| `osu.Game/Beatmaps/ICustomBeatmapLoader.cs` | 自定义谱面加载器接口 |

### 修改的上游文件

修改可归纳为 4 大类：

1. **离线模式 gate** (`OnlineFeaturesEnabled`)
2. **Ruleset 扩展点** (`GetScoreDisplayBucket` / `CreateBeatmapDetailsComponent` / `CreateKeyBindingSections` / `GetGroupModes`)
3. **RulesetDataJson 持久化**
4. **自定义 Beatmap Loader**

#### 核心 / 启动

| 文件 | 改动类型 | 说明 |
|---|---|---|
| `osu.Game/OsuGameBase.cs` | 离线 gate + Loader + 扩展点 | `STORAGE_NAME` 改名；`OnlineFeaturesEnabled`；`CreateCustomBeatmapLoaders()` 虚方法；endpoint / API 按 online flag 切换 |
| `osu.Game/OsuGame.cs` | 离线 gate | ~15 处隐藏在线 overlay 与功能入口 |

#### Ruleset 扩展

| 文件 | 改动类型 | 说明 |
|---|---|---|
| `osu.Game/Rulesets/Ruleset.cs` | 扩展点 | `GetScoreDisplayBucket()`、`CreateKeyBindingSections()`、`CreateBeatmapDetailsComponent()`、`GetGroupModes()` |

#### Beatmap 管线

| 文件 | 改动类型 | 说明 |
|---|---|---|
| `osu.Game/Beatmaps/BeatmapMetadata.cs` | RulesetData | `RulesetDataJson` + 泛型 get/set |
| `osu.Game/Beatmaps/BeatmapManager.cs` | Loader | 接收 `ICustomBeatmapLoader` |
| `osu.Game/Beatmaps/WorkingBeatmapCache.cs` | Loader | 使用 `ICustomBeatmapLoader` |
| `osu.Game/Beatmaps/BeatmapUpdaterMetadataLookup.cs` | 离线 gate | `OnlineFeaturesEnabled` 控制远端查找 |
| `osu.Game/Beatmaps/LocalCachedBeatmapMetadataSource.cs` | 离线 gate | `allowRemoteFetch` 参数 |
| `osu.Game/Beatmaps/Drawables/BundledBeatmapDownloader.cs` | 离线 gate | 早退 |

#### Scoring / Replay

| 文件 | 改动类型 | 说明 |
|---|---|---|
| `osu.Game/Scoring/ScoreInfo.cs` | RulesetData | `RulesetDataJson` + 泛型 get/set |
| `osu.Game/Scoring/ScoreInfoExtensions.cs` | 扩展点 | `GetScoreDisplayBucket()` 过滤 |
| `osu.Game/Scoring/Legacy/LegacyScoreDecoder.cs` | RulesetData | 读写 `RulesetDataJson` |
| `osu.Game/Scoring/Legacy/LegacyReplaySoloScoreInfo.cs` | RulesetData | 序列化 `RulesetDataJson` |

#### Song Select / Carousel

| 文件 | 改动类型 | 说明 |
|---|---|---|
| `osu.Game/Screens/Select/Filter/GroupMode.cs` | 扩展点 | `DifficultyTable` 枚举值 |
| `osu.Game/Screens/Select/FilterControl.cs` | 扩展点 | 表分组锁定 `SortMode.Difficulty` |
| `osu.Game/Screens/Select/BeatmapCarouselFilterGrouping.cs` | 扩展点 | 表分组逻辑 |
| `osu.Game/Screens/Select/BeatmapCarousel.cs` | 扩展点 | `GetScoreDisplayBucket()` 过滤 |
| `osu.Game/Screens/Select/SongSelect.cs` | 离线 gate | 隐藏在线 lookup / leaderboard / 更新 |
| `osu.Game/Screens/Select/SoloSongSelect.cs` | 离线 gate | |
| `osu.Game/Screens/Select/BeatmapMetadataWedge.cs` | 扩展点 | 挂载 ruleset 自定义详情组件 |
| `osu.Game/Screens/Select/BeatmapLeaderboardWedge.cs` | 离线 gate + 扩展点 | Local scope + bucket 过滤 |
| `osu.Game/Screens/Select/PanelLocalRankDisplay.cs` | 扩展点 | bucket 过滤 |
| `osu.Game/Screens/Select/PanelUpdateBeatmapButton.cs` | 离线 gate | |
| `osu.Game/Screens/Select/PanelBeatmapSet.cs` | 离线 gate | |
| `osu.Game/Screens/Select/NoResultsPlaceholder.cs` | 离线 gate | |
| `osu.Game/Screens/Select/BeatmapDetailsArea.Header.cs` | 离线 gate | |

#### 菜单 / Toolbar / Overlay

| 文件 | 改动类型 | 说明 |
|---|---|---|
| `osu.Game/Screens/Menu/ButtonSystem.cs` | 离线 gate | 隐藏多人 / playlists / daily |
| `osu.Game/Screens/Menu/OnlineMenuBanner.cs` | 离线 gate | |
| `osu.Game/Overlays/Toolbar/Toolbar.cs` | 离线 gate | 隐藏在线按钮 |
| `osu.Game/Overlays/FirstRunSetup/ScreenBeatmaps.cs` | 离线 gate | 禁用下载，改为本地导入提示 |
| `osu.Game/Overlays/Settings/Sections/Input/RulesetBindingsSection.cs` | 扩展点 | `CreateKeyBindingSections()` |
| `osu.Game/Overlays/Profile/Header/Components/DrawableTournamentBanner.cs` | 离线 gate | |
| `osu.Game/Users/Drawables/ClickableTeamFlag.cs` | 离线 gate | |

#### 编辑器

| 文件 | 改动类型 | 说明 |
|---|---|---|
| `osu.Game/Screens/Edit/Submission/BeatmapSubmissionScreen.cs` | 离线 gate | |
| `osu.Game/Screens/Edit/Submission/ScreenFrequentlyAskedQuestions.cs` | 离线 gate | |

#### 音频

| 文件 | 改动类型 | 说明 |
|---|---|---|
| `osu.Game/Audio/PreviewTrackManager.cs` | 离线 gate | `onlinePreviewEnabled` 参数 |

#### 本地化

| 文件 | 改动类型 | 说明 |
|---|---|---|
| `osu.Game/Localisation/SongSelectStrings.cs` | 扩展点 | `DifficultyTable` 字符串 |
| `osu.Game/Localisation/FirstRunSetupBeatmapScreenStrings.cs` | 离线 gate | 离线提示字符串 |

#### 数据库 / 后台

| 文件 | 改动类型 | 说明 |
|---|---|---|
| `osu.Game/Database/BackgroundDataStoreProcessor.cs` | 离线 gate | 控制 metadata 远端 fetch |

#### 测试

| 文件 | 改动类型 | 说明 |
|---|---|---|
| `osu.Game/Tests/Visual/EditorTestScene.cs` | Loader | 适配 `ICustomBeatmapLoader` 参数 |

### Cherry-pick 高风险区域

以下文件属于上游高频改动区域，cherry-pick 冲突概率最高：

- `osu.Game/Screens/Select/BeatmapCarousel.cs`
- `osu.Game/Screens/Select/FilterControl.cs`
- `osu.Game/Beatmaps/WorkingBeatmapCache.cs`
- `osu.Game/Beatmaps/BeatmapManager.cs`
- `osu.Game/OsuGame.cs`
- `osu.Game/OsuGameBase.cs`
