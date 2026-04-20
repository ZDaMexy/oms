# Upstream Lock

| Field | Value |
|---|---|
| Repository | https://github.com/ppy/osu |
| Tag | `2026.305.0-lazer` |
| Upstream commit | `bb289363a2b8e6bf62be355f8570def018f0d7be` |
| Local bootstrap commit | `0b97bbdd4348de47e1d597a65f0a7734ad184000` |
| Date | 2026-03-05 |
| Last audited | 2026-04-19 |

## Sync Policy

- Do **not** blindly pull upstream changes.
- Cherry-pick critical bug fixes selectively.
- Verify cherry-picks do not conflict with BMS ruleset or removed rulesets.
- In the current local repository, use `0b97bbdd4348de47e1d597a65f0a7734ad184000..HEAD` for `git diff` / `git log` comparisons. The upstream object `bb289363a2b8e6bf62be355f8570def018f0d7be` is the semantic lock point from `ppy/osu`, but it is not present as a local object in this repo clone.
- Re-evaluate upstream sync every 3 months.

## Current OMS Delta in osu.Game

> 以下统计基于本地 bootstrap commit `0b97bbdd4348de47e1d597a65f0a7734ad184000` 对比当前 `HEAD`，用于替代已过时的少量文件白名单。
> 2026-04-19 本地审计结果：`osu.Game/` 共 **147** 个变更路径（**113 modified / 30 added / 4 deleted**）。4 个 deleted 均为 `.idea` 工作区残留文件，不纳入 cherry-pick 风险判断。

### 变更主类

1. **离线模式与本地优先产品面**：`OnlineFeaturesEnabled`、`LocalOfflineAPIAccess`、首跑/主菜单/Toolbar/Song Select/编辑器外链的离线 gate
2. **文件系统直读存储与谱库管理**：`chartbms/`、`chartmania/`、`ExternalLibraryConfig` / `ExternalLibraryScanner` / `ManagedLibraryScanner`
3. **Ruleset / scoring / results 扩展点**：`RulesetDataJson`、score bucket、ruleset 自定义 details / keybinding / panel accent / results panel shell
4. **OMS 内置皮肤与 fallback 链**：`OmsSkin`、`OmsSkinTransformer`、source-chain、Skin Editor / runtime skin selection / startup migration
5. **本地化与设置扩张**：BMS / Mod / Maintenance / ExternalLibrary 等新增字符串与设置入口

### 目录级风险分桶（2026-04-19）

| 目录 | 变更路径数 | 代表文件 | 风险说明 |
|---|---:|---|---|
| `Screens` | 38 | `Screens/Select/SongSelect.cs`, `Screens/Ranking/ResultsScreen.cs`, `Screens/Menu/MainMenu.cs` | 选歌、结算、主菜单与 play 流程都被 OMS 定制；上游高频改动区 |
| `Beatmaps` | 20 | `Beatmaps/BeatmapManager.cs`, `Beatmaps/ExternalLibraryScanner.cs`, `Beatmaps/WorkingBeatmapCache.cs` | 直读存储、外部谱库、metadata 与 custom loader 全集中在这里 |
| `Localisation` | 19 | `Localisation/BmsMod.resx`, `Localisation/ExternalLibrarySettingsStrings.cs`, `Localisation/SongSelectStrings.cs` | 新增大量字符串资源；上游同步时极易漏字符串或资源清单 |
| `Overlays` | 19 | `Overlays/Toolbar/Toolbar.cs`, `Overlays/Settings/Sections/Maintenance/ExternalLibrarySettings.cs`, `Overlays/SkinEditor/SkinEditorOverlay.cs` | 设置页、Toolbar、Skin Editor 都有 OMS 产品面改造 |
| `Rulesets` | 11 | `Rulesets/Ruleset.cs`, `Rulesets/Scoring/ScoreProcessor.cs`, `Rulesets/UI/ReplayRecorder.cs` | 自定义扩展点与 scoring/replay 入口都被改动 |
| `Skinning` | 9 | `Skinning/OmsSkin.cs`, `Skinning/OmsSkinTransformer.cs`, `Skinning/SkinManager.cs` | 内置皮肤、fallback source-chain 与启动迁移主链 |
| `Online` | 6 | `Online/API/LocalOfflineAPIAccess.cs`, `Online/Leaderboards/LeaderboardManager.cs`, `Online/Rooms/RoomExtensions.cs` | 离线根装配与 URL / leaderboard 降级 |
| `Scoring` | 5 | `Scoring/ScoreInfo.cs`, `Scoring/Legacy/LegacyScoreEncoder.cs`, `Scoring/Legacy/LegacyReplaySoloScoreInfo.cs` | `RulesetDataJson` 与 replay/score 持久化 |
| `Database` | 3 | `Database/BackgroundDataStoreProcessor.cs`, `Database/RealmAccess.cs`, `Database/RealmObjectExtensions.cs` | 后台 metadata 与 realm 读写边界 |
| 其他 | 17 | `Audio/PreviewTrackManager.cs`, `Configuration/OsuConfigManager.cs`, `Users/UserCoverBackground.cs` 等 | 次级分布区，逐个 cherry-pick 审核 |

### 当前新增文件（上游不存在）

以下文件是当前本地 diff 中最关键的新增文件；它们比旧版文档中的 2 个新增文件清单更接近当前现实：

- `osu.Game/Online/API/LocalOfflineAPIAccess.cs`
- `osu.Game/Beatmaps/ICustomBeatmapLoader.cs`
- `osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs`
- `osu.Game/Beatmaps/BmsStarRatingResolver.cs`
- `osu.Game/Beatmaps/ExternalLibraryConfig.cs`
- `osu.Game/Beatmaps/ExternalLibraryRoot.cs`
- `osu.Game/Beatmaps/ExternalLibraryScanner.cs`
- `osu.Game/Beatmaps/ManagedLibraryScanner.cs`
- `osu.Game/Beatmaps/Formats/OsuFileModeDetector.cs`
- `osu.Game/Skinning/OmsSkin.cs`
- `osu.Game/Skinning/OmsSkinTransformer.cs`
- `osu.Game/Overlays/Settings/Sections/Maintenance/ExternalLibrarySelectScreen.cs`
- `osu.Game/Overlays/Settings/Sections/Maintenance/ExternalLibrarySettings.cs`
- `osu.Game/Screens/Ranking/Statistics/DefaultResultsPanelContainer.cs`
- `osu.Game/Screens/Ranking/Statistics/DefaultResultsPanelDisplay.cs`
- 多组新增本地化资源：`Localisation/BmsMod*.resx`、`ExternalLibrarySettings*.resx`、`MaintenanceSettings*.resx`、`ModSettings*.resx`

### Cherry-pick 高风险文件与区域

以下路径在上游同步时最容易冲突，或最容易把 OMS 产品约束意外冲掉：

- `osu.Game/OsuGameBase.cs`
- `osu.Game/OsuGame.cs`
- `osu.Game/Beatmaps/BeatmapManager.cs`
- `osu.Game/Beatmaps/WorkingBeatmapCache.cs`
- `osu.Game/Beatmaps/BeatmapUpdaterMetadataLookup.cs`
- `osu.Game/Screens/Select/SongSelect.cs`
- `osu.Game/Screens/Select/BeatmapCarousel.cs`
- `osu.Game/Screens/Select/FilterControl.cs`
- `osu.Game/Screens/Ranking/ResultsScreen.cs`
- `osu.Game/Skinning/SkinManager.cs`
- `osu.Game/Skinning/RulesetSkinProvidingContainer.cs`
- `osu.Game/Rulesets/Ruleset.cs`

### Cherry-pick Checklist

- 先比较 `0b97bbdd4348de47e1d597a65f0a7734ad184000..HEAD -- osu.Game`，不要直接假设当前仓库里存在 `bb289363a2b8e6bf62be355f8570def018f0d7be` 对象
- 忽略 `.idea` 删除项；它们不是产品代码差异
- 核对离线 gate 是否仍保持：`OnlineFeaturesEnabled`、`LocalOfflineAPIAccess`、URL/leaderboard/update/login 入口的 no-op 或隐藏
- 核对存储主链是否仍保持：`chartbms/`、`chartmania/`、external library scan、filesystem-backed beatmap loading
- 核对 ruleset/scoring 持久化是否仍保持：`RulesetDataJson`、score display bucket、results statistics shell、replay archival
- 核对皮肤主链是否仍保持：`OmsSkin`、`OmsSkinTransformer`、`SkinManager`、runtime fallback/source-chain、startup skin migration

