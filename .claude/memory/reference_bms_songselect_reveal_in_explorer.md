---
name: reference_bms_songselect_reveal_in_explorer
description: "Song-select right-click 'reveal song folder / chart file in OS explorer' — the FilesystemBeatmapLocation helper, the host-vs-storage gotcha, gating, and call sites"
metadata: 
  node_type: memory
  type: reference
  originSessionId: 7c49e0d4-4fbb-406a-a077-769f249b4a85
---

Song-select right-click **「打开歌曲文件位置」/「打开谱面文件位置」** (reveal in OS file browser, landed 2026-06-22, P1-I; path resolution leans on P1-H storage fields). Lets the user open the file explorer at a beatmap's folder/file with it selected.

**Capability + gating**: only **filesystem-backed** beatmaps (`FilesystemBeatmapLocation.IsFilesystemBacked` == `BeatmapSetInfo.FilesystemStoragePath` non-empty → BMS chartbms/ + direct-read mania chartmania/). Hash-backed (imported .osz, files/ store) have no folder → no menu item. User chose "all local-folder beatmaps", NOT BMS-only.

**THE helper (single source of truth)**: `osu.Game/Beatmaps/FilesystemBeatmapLocation.cs` (static). `TryGetSetDirectory` (external → absolute `FilesystemStoragePath` verbatim; managed → `storage.GetFullPath(relative)`), `TryGetBeatmapFile` (set dir + `BeatmapInfo.LocalFilePath`, with `/`→native separator), `IsFilesystemBacked`, `Reveal(host, absPath)`, `CreateOpenSongFolderItem`/`CreateOpenChartFileItem` (return null if not filesystem-backed). **Same resolution pattern as `BmsBgaPlayer.tryGetAbsolutePath`** — keep them in sync; if P1-H changes the relative/absolute convention of `FilesystemStoragePath` or `LocalFilePath` semantics, update both.

**THE gotcha (don't regress)**: reveal MUST go through **`GameHost.PresentFileExternally(absolutePath)`** (Windows = `explorer /select,path` — opens parent + selects), NOT `Storage.PresentFileExternally`. External-library absolute paths live OUTSIDE the data-storage root, and `NativeStorage.GetFullPath` throws a traversal guard for paths that escape the root → `Storage.PresentFileExternally(externalAbsolute)` would throw. `host.PresentFileExternally` has no such guard. `GameHost.PresentFileExternally`/`OpenFileExternally` are `[Resolved]`-able and confirmed present in framework. External dirs are READ-only here (just open explorer) — never mutate. Target missing (external lib moved) → graceful fallback to `OpenFileExternally(parentDir)`, else no-op.

**Call sites (3 shared osu.Game panels, each `[Resolved] GameHost host` + `Storage storage`)**: set header `PanelBeatmapSet.ContextMenuItems` (before Delete) → song folder; difficulty via `SoloSongSelect.GetForwardActions` (after Edit) → chart file (auto-covers `PanelBeatmap` rows + `PanelBeatmapStandalone` + footer Options popover); `PanelBeatmapStandalone.ContextMenuItems` also gets the song-folder item (it's both song bar + the single difficulty). Labels hardcoded Chinese (matches OMS BMS zh UI). Adding `[Resolved] GameHost`/`Storage` to these panels is safe — they already require `BeatmapManager`/`RealmAccess` (full game DI); verified by `TestScenePanelSet`/`TestScenePanelBeatmapStandalone`.

Tests: `FilesystemBeatmapLocationTest` (6, in BMS test suite) covers managed/external × set/file + gating. P1-I CONSTRAINTS 实现边界 #7. See [[project_oms_songselect_display_nav]].
