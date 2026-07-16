# Upstream Lock

| Field | Value |
|---|---|
| Repository | https://github.com/ppy/osu |
| Tag | `2026.305.0-lazer` |
| Upstream commit | `bb289363a2b8e6bf62be355f8570def018f0d7be` |
| Local bootstrap commit | `0b97bbdd4348de47e1d597a65f0a7734ad184000` |
| Date | 2026-03-05 |
| Lock last audited | 2026-04-19 |

## Sync Policy

- Do **not** blindly pull upstream changes.
- Cherry-pick critical bug fixes selectively.
- Verify cherry-picks do not conflict with BMS ruleset or removed rulesets.
- In the current local repository, use `0b97bbdd4348de47e1d597a65f0a7734ad184000..HEAD` for `git diff` / `git log` comparisons. The upstream object `bb289363a2b8e6bf62be355f8570def018f0d7be` is the semantic lock point from `ppy/osu`, but it is not present as a local object in this repo clone.
- Re-evaluate upstream sync every 3 months.

## 可复跑的 OMS Delta 审计

`HEAD` 会持续变化，本页不缓存“当前共有多少 modified/added/deleted”这类易腐数字。每次评估 upstream patch 前，从本地 bootstrap 重新生成事实清单：

```powershell
git diff --name-status 0b97bbdd4348de47e1d597a65f0a7734ad184000..HEAD -- osu.Game
git diff --stat 0b97bbdd4348de47e1d597a65f0a7734ad184000..HEAD -- osu.Game
git diff --name-only --diff-filter=A 0b97bbdd4348de47e1d597a65f0a7734ad184000..HEAD -- osu.Game
```

数量只能描述命令运行当时的快照，不能代替逐路径审查；需要保存某次审计数字时写入对应 `CHANGELOG`，并同时记录 commit。

### 变更主类

1. **离线模式与本地优先产品面**：`OnlineFeaturesEnabled`、`LocalOfflineAPIAccess`、首跑/主菜单/Toolbar/Song Select/编辑器外链的离线 gate
2. **文件系统直读存储与谱库管理**：`chartbms/`、`chartmania/`、`ExternalLibraryConfig` / `ExternalLibraryScanner` / `ManagedLibraryScanner`
3. **Ruleset / scoring / results 扩展点**：`RulesetDataJson`、score bucket、ruleset 自定义 details / keybinding / panel accent / results panel shell
4. **OMS 内置皮肤与 fallback 链**：`OmsSkin`、`OmsSkinTransformer`、source-chain、Skin Editor / runtime skin selection / startup migration
5. **本地化与设置扩张**：BMS / Mod / Maintenance / ExternalLibrary 等新增字符串与设置入口

### 稳定目录风险面

| 目录 | 代表文件 | 风险说明 |
|---|---|---|
| `Screens` | `Screens/Select/SongSelect.cs`, `Screens/Ranking/ResultsScreen.cs`, `Screens/Menu/MainMenu.cs` | 选歌、结算、主菜单与 play 流程都被 OMS 定制；上游高频改动区 |
| `Beatmaps` | `Beatmaps/BeatmapManager.cs`, `Beatmaps/ExternalLibraryScanner.cs`, `Beatmaps/WorkingBeatmapCache.cs` | 直读存储、外部谱库、metadata 与 custom loader 集中区 |
| `Localisation` | `Localisation/BmsMod.resx`, `Localisation/ExternalLibrarySettingsStrings.cs`, `Localisation/SongSelectStrings.cs` | 新增字符串资源较多；同步时容易漏字符串或资源清单 |
| `Overlays` | `Overlays/Toolbar/Toolbar.cs`, `Overlays/Settings/Sections/Maintenance/ExternalLibrarySettings.cs`, `Overlays/SkinEditor/SkinEditorOverlay.cs` | 设置页、Toolbar、Skin Editor 有 OMS 产品面改造 |
| `Rulesets` | `Rulesets/Ruleset.cs`, `Rulesets/Scoring/ScoreProcessor.cs`, `Rulesets/UI/ReplayRecorder.cs` | 自定义扩展点与 scoring/replay 入口 |
| `Skinning` | `Skinning/OmsSkin.cs`, `Skinning/OmsSkinTransformer.cs`, `Skinning/SkinManager.cs` | 内置皮肤、fallback source-chain 与启动迁移主链 |
| `Online` | `Online/API/LocalOfflineAPIAccess.cs`, `Online/Leaderboards/LeaderboardManager.cs`, `Online/Rooms/RoomExtensions.cs` | 离线根装配与 URL / leaderboard 降级 |
| `Scoring` | `Scoring/ScoreInfo.cs`, `Scoring/Legacy/LegacyScoreEncoder.cs`, `Scoring/Legacy/LegacyReplaySoloScoreInfo.cs` | `RulesetDataJson` 与 replay/score 持久化 |
| `Database` | `Database/BackgroundDataStoreProcessor.cs`, `Database/RealmAccess.cs`, `Database/RealmObjectExtensions.cs` | 后台 metadata 与 Realm 读写边界 |
| 其它 | `Audio/PreviewTrackManager.cs`, `Configuration/OsuConfigManager.cs`, `Users/UserCoverBackground.cs` 等 | 次级分布区仍须逐个 patch 审核 |

### 代表性新增文件（非当前清单）

以下路径帮助快速定位 OMS-owned 设计面，但不是完整或实时清单；是否仍为新增文件，以本节开头的 `--diff-filter=A` 命令结果为准：

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
- 对命令结果中确认属于 `.idea` 的工作区残留可排除产品风险；不得根据旧统计预先假设所有 deleted 都可忽略
- 核对离线 gate 是否仍保持：`OnlineFeaturesEnabled`、`LocalOfflineAPIAccess`、URL/leaderboard/update/login 入口的 no-op 或隐藏
- 核对存储主链是否仍保持：`chartbms/`、`chartmania/`、external library scan、filesystem-backed beatmap loading
- 核对 ruleset/scoring 持久化是否仍保持：`RulesetDataJson`、score display bucket、results statistics shell、replay archival
- 核对皮肤主链是否仍保持：`OmsSkin`、`OmsSkinTransformer`、`SkinManager`、runtime fallback/source-chain、startup skin migration

