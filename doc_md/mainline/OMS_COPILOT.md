# OMS — GitHub Copilot Development Context

> **OMS** is a Windows-only rhythm game client forked from osu!lazer.  
> It removes all game modes except osu!mania, and adds a new first-class **BMS mode**  
> designed to fully replace LR2 and beatoraja as a modern BMS player.  
> Long-term goal: private server integration (accounts, leaderboards, beatmap download).

---

## 1. Project Identity

| Field | Value |
|---|---|
| **Project Name** | OMS |
| **Base** | osu!lazer (locked commit, see `UPSTREAM.md`) |
| **Target Platform** | Windows only (Win10 22H2+) |
| **Runtime** | .NET 8, DesktopGL via osu-framework |
| **Language** | C# 12 |
| **Primary Game Modes** | `OsuMania` (retained), `BMS` (new) |
| **Removed Modes** | `Osu` (standard), `Taiko`, `Catch` — fully deleted |

### Current Release and Connectivity Policy

Until Phase 3 begins, OMS follows these product constraints:

- Windows releases are portable full packages only. Prefer `build-release.ps1` 产出的 `oms_YYYYMMDD(.zip)`；do not treat `Setup.exe`, MSI, or delta packages as the primary user path for current OMS releases.
- In-game online update is disabled for early OMS releases. Do not ship automatic check, download, or apply-update flows to end users yet.
- Hide or remove release-stream switching and manual "Check for updates" UI while update delivery is intentionally disabled.
- Version-to-version updates before online features exist are manual file-overwrite updates. New packages must support replacing program files in place without forcing users to re-import local BMS content, and current release guidance must explicitly preserve `portable.ini`, portable-mode `data/`, and any `storage.ini` custom-root pointer.
- Current official builds still keep mutable user data under a separate data root (default `%APPDATA%/oms/` for release, `%APPDATA%/oms-development/` for debug). `storage.ini` may redirect everything to one custom root, but do not describe OMS as already shipping an out-of-box program+data single-package layout.
- Beatoraja-style portable data mode is already supported via `portable.ini` -> `data/`; keep mutable user data in that dedicated subdirectory rather than mixing it directly with binaries.
- Registered multi-root external beatmap libraries have a working baseline: `ExternalLibraryConfig` (JSON-based, `library-roots.json`) for root registration, and `ExternalLibraryScanner` (delegate-injected) for walking BMS / mania roots and importing discovered sets. Settings -> Maintenance add/remove/scan UI is already landed; deletion/invalidation semantics remain future work.
- All other networked features, including account login, leaderboards, beatmap download, chat, news, multiplayer, spectator, daily challenge, and remote table sources, remain disabled or hidden until Phase 3.
- Current local-first builds should not ship non-empty default API / OAuth / SignalR / BSS server URLs; if online code remains in the tree, it is Phase 3 technical reserve rather than user-facing functionality.

---

## 2. Repository Structure

```
oms/
├── osu.Game/                        # Core game framework (upstream, minimal modification)
├── osu.Game.Rulesets.Mania/         # Retained mania ruleset (upstream, minimal modification)
├── osu.Game.Rulesets.Bms/           # NEW — BMS ruleset (primary development target)
│   ├── Audio/
│   │   ├── BmsKeysoundSampleInfo.cs   # Beatmap-relative keysound sample lookup wrapper
│   │   └── BmsKeysoundStore.cs        # Shared keysound channel pool for BGM/note/LN playback
│   ├── Beatmaps/
│   │   ├── BmsBeatmapDecoder.cs         # BMS file parser (.bms/.bme/.bml/.pms)
│   │   ├── BmsBeatmapConverter.cs       # Converts parsed BMS → IBeatmap
│   │   ├── BmsBeatmapInfo.cs            # BMS-specific beatmap metadata (Keymode, MeasureLengthControlPoints, etc.)
│   │   ├── BmsArchiveReader.cs          # Handles .zip/.rar/.7z BMS package import
│   │   ├── BmsBeatmapLoader.cs          # Runtime reloader for imported BMS charts
│   │   ├── BmsBeatmapImporter.cs        # Ruleset importer entry for managed/external BMS registration
│   │   └── BmsFolderImporter.cs         # Folder registration authority for FilesystemStoragePath + ExternalLibraryRootPath
│   ├── Judgements/
│   │   ├── BmsJudgementSystem.cs        # Pluggable judgment engine
│   │   ├── BmsTimingWindows.cs          # Timing window definitions per system
│   │   └── BmsPoorJudgement.cs          # Empty-poor (ghost note penalty) logic
│   ├── Scoring/
│   │   ├── BmsScoreProcessor.cs         # EX-SCORE calculation
│   │   ├── BmsGaugeProcessor.cs         # All six gauge types (ASSIST EASY/EASY/NORMAL/HARD/EX-HARD/HAZARD)
│   │   ├── BmsClearLampProcessor.cs     # Clear lamp tracking
│   │   └── BmsDjLevelCalculator.cs      # DJ LEVEL (AAA/AA/A/B/C/D/E/F) from EX-SCORE%
│   ├── Difficulty/
│   │   ├── BmsNoteDensityAnalyzer.cs    # Shared sliding-window note density utility (used by calculator + graph)
│   │   ├── BmsDifficultyCalculator.cs   # Density-based star rating calculator (uses BmsNoteDensityAnalyzer)
│   │   ├── BmsDifficultyAttributes.cs   # Stores calculated difficulty attributes
│   │   └── BmsKeymode.cs                # Enum: Key5K, Key7K, Key9K_Bms, Key9K_Pms, Key14K
│   ├── DifficultyTable/
│   │   ├── BmsDifficultyTableManager.cs # Subscription list, fetch, refresh
│   │   ├── BmsDifficultyTableEntry.cs   # Single table entry (MD5 → level label)
│   │   └── BmsTableMd5Index.cs          # Local MD5 → table level lookup index
│   ├── SongSelect/
│   │   ├── BmsTableGroupMode.cs         # Custom SongSelect grouping: table → level → set
│   │   ├── BmsLibraryGroupMode.cs       # BMS library grouping: external root / internal managed hierarchy
│   │   └── BmsNoteDistributionGraph.cs  # Note distribution preview panel
│   ├── Layout/
│   │   ├── BmsPlayfield.cs              # Playfield rendering
│   │   ├── BmsLaneLayout.cs             # Lane config for 5K/7K/9K/14K, 1P/2P
│   │   ├── BmsLaneCover.cs              # Sudden/Hidden lane cover + focus visuals (Mod-controlled)
│   │   └── BmsScratchLane.cs            # Scratch lane rendering and input handling
│   ├── Mods/
│   │   ├── BmsModJudgeBeatoraja.cs      # Switches to beatoraja timing windows
│   │   ├── BmsModJudgeLr2.cs            # Switches to LR2 timing windows
│   │   ├── BmsModLongNoteMode.cs        # Switches runtime long-note mode (CN/HCN; default LN uses no Mod)
│   │   ├── BmsModGaugeAssistEasy.cs     # Assist Easy gauge
│   │   ├── BmsModGaugeEasy.cs           # Easy gauge
│   │   ├── BmsModGaugeHard.cs           # Hard gauge
│   │   ├── BmsModGaugeExHard.cs         # EX-Hard gauge
│   │   ├── BmsModGaugeHazard.cs         # Hazard gauge
│   │   ├── BmsModGaugeAutoShift.cs      # GAS — Gauge Auto Shift
│   │   ├── BmsModSudden.cs              # Sudden cover
│   │   ├── BmsModHidden.cs              # Hidden cover
│   │   ├── BmsModLift.cs                # Lift (judgement-line raise)
│   │   ├── BmsModAutoScratch.cs         # A-SCR — Auto Scratch
│   │   ├── BmsModAutoNote.cs            # A-NOT — Auto Note
│   │   ├── BmsModAutoplay.cs            # BMS-specific autoplay
│   │   ├── BmsModMirror.cs              # Button-lane mirror (scratch stays fixed)
│   │   ├── BmsModRandom.cs              # RANDOM / R-RANDOM / S-RANDOM + custom pattern
│   │   └── Planned Phase 2 mod not yet in tree: 1P/2P flip
│   ├── Input/
│   │   └── BmsInputManager.cs           # BMS-specific input routing + pre-start hold / hi-speed lane mapping
│   ├── Background/
│   │   └── BmsBackgroundLayer.cs        # Static BG + future BGA hook
│   ├── UI/
│   │   ├── BmsGameplayAdjustmentTarget.cs # Shared Sudden/Hidden/Lift gameplay adjustment target enum
│   │   └── BmsPreStartHiSpeedOverlay.cs   # Pre-start hold overlay for tri-mode mode/value feedback
│   ├── Configuration/
│   │   ├── BmsModStatePersistence.cs    # Ruleset-local selected-mod/settings persistence for BMS startup and ruleset switches
│   │   └── BmsRulesetConfigManager.cs   # Persistent BMS mode settings (layout, keysound, mod-state snapshot, later feature flags)
│   ├── Resources/
│   │   └── bms_table_presets.json       # Bundled preset difficulty table URLs (not hardcoded)
│   ├── BmsMod.cs                        # Abstract base class for all BMS mods (extends Mod)
│   └── BmsRuleset.cs                    # Ruleset entry point
├── oms.Input/                        # NEW — Unified Input Abstraction Layer
│   ├── OmsInputRouter.cs                # Routes all signal types to game actions
│   ├── OmsBindingStore.cs               # Default per-profile bindings + trigger helpers
│   └── Devices/
│       ├── OmsKeyboardInputHandler.cs   # Keyboard combinations -> OmsAction
│       ├── OmsHidButtonInputHandler.cs  # HID digital buttons -> OmsAction
│       ├── OmsHidAxisInputHandler.cs    # HID axis delta -> OmsAction
│       ├── OmsHidDeviceHandler.cs       # HID provider polling + auto-release
│       ├── OmsXInputButtonInputHandler.cs # Joystick/gamepad buttons -> OmsAction
│       └── OmsMouseAxisInputHandler.cs  # Mouse movement delta -> scratch axis
└── osu.Desktop/                      # Windows entry point
    └── Program.cs
```

Phase 2 / Phase 3 design targets such as 1P/2P flip, Random, and a dedicated private-server client project are documented later in this file. They are not all present in the current workspace tree.

---

## 3. Removed Upstream Components

When working in this codebase, the following upstream modules **do not exist** and must not be referenced:

- `osu.Game.Rulesets.Osu` — deleted
- `osu.Game.Rulesets.Taiko` — deleted
- `osu.Game.Rulesets.Catch` — deleted

If upstream code references these via reflection or ruleset discovery, stub them out or remove the references. Do not re-add them.

---

## 4. BMS Ruleset — Core Systems

### 4.1 BMS File Parsing (`BmsBeatmapDecoder`)

The BMS decoder must handle:

**Supported file extensions:** `.bms`, `.bme`, `.bml`, `.pms`

**Encoding detection:** Auto-detect Shift-JIS vs UTF-8 before parsing. Use `Ude` or `charset-normalizer`-equivalent. Never hard-code encoding.

**Header fields to parse:**

```
#TITLE, #SUBTITLE, #ARTIST, #SUBARTIST, #GENRE, #COMMENT, #BPM, #PLAYLEVEL, #DIFFICULTY, #RANK, #TOTAL
#STAGEFILE, #BANNER, #BACKBMP
#WAV## (keysound index), #BMP## (BGA frame index, reserved)
#BPM## (BPM table for #BPMXX channels)
#STOP## (stop duration table)
#LNOBJ (long note end marker object)
#LNTYPE 1 | 2 (long note encoding style)
#RANDOM / #IF / #ENDIF (Future Scope — parse all branches; execute the `#IF 1` block if present; if no `#IF 1` branch exists in a `#RANDOM` block, skip the entire block and log a warning; always log a warning that random branching is unsupported regardless of which branch is selected)
```

**Additional header field notes:**
- `#SUBTITLE`: secondary title line; store separately, do not concatenate with `#TITLE`. Display below the primary title in the song select carousel card and the chart detail panel. Omit from the result screen title line to keep it compact. If `#SUBTITLE` is an empty string or absent, do not render a subtitle element.
- `#SUBARTIST`: chart-level secondary artist / arranger credit. Store separately rather than merging into `Artist`, and surface it in the chart detail panel / Song Select summary when present.
- `#COMMENT`: chart-level freeform note. Persist it in BMS-specific metadata for detail views, but do not force it into compact carousel labels.
- `#PLAYLEVEL`: preserve the raw internal level string in BMS chart metadata even when the main difficulty display uses a table rating or density star.
- `#DIFFICULTY`: integer 1–5 mapping to Beginner / Normal / Hyper / Another / Insane; used for intra-set difficulty labelling and sort order when no table entry exists

**Channel parsing (measure/channel/data blocks):**

```
#MMMCC:data
```
- `MMM` = measure number (3-digit, 0-padded, `000`–`999`)
- `CC` = channel code (hex)
- `data` = base-36 object sequence, split into equal-length 2-character segments

> **Channel `02` exception:** Channel `02` data is a **decimal floating-point number** (e.g. `0.75`), not a base-36 object sequence. Parse it as `double` directly.

**Key channels to implement:**

| Channel | Meaning |
|---|---|
| `01` | BGM (background audio, no hit) |
| `02` | Measure length multiplier (e.g. `0.75` = 3/4 time for this measure) |
| `03` | BPM change (direct value, hex integer → decimal BPM) |
| `04` | BGA base layer (reserved — Phase 2) |
| `06` | BGA poor layer (reserved — Phase 2, shown on POOR judgment) |
| `07` | BGA overlay layer (reserved — Phase 2) |
| `08` | BPM change (via #BPM table) |
| `09` | STOP |
| `11`–`19` | 1P playable lanes (1-9) |
| `21`–`29` | 2P playable lanes (1-9) |
| `51`–`59` | 1P long note lanes |
| `61`–`69` | 2P long note lanes |

Channel `02` affects the duration of the measure in beats. A value of `0.75` makes the measure 3/4 as long.

> **⚠ Implementation Warning:** osu!'s `TimeSignature` uses integer numerator/denominator and cannot represent arbitrary float multipliers (e.g. `0.6`, `0.333…`) without precision loss. **Do not force channel `02` values into `TimeSignature`**. Instead, store measure length multipliers in a BMS-specific `BmsMeasureLengthControlPoint` list maintained alongside `ControlPointInfo`. The note placement and scroll rendering layers must read from this list to correctly position notes. Only values that reduce cleanly to simple fractions (e.g. `0.75 → 3/4`, `0.5 → 1/2`) may optionally populate `TimeSignature` for bar-line rendering.
>
> **`BmsMeasureLengthControlPoint` integration contract:**
>
> ```csharp
> public record BmsMeasureLengthControlPoint(int MeasureIndex, double Multiplier);
> ```
>
> Stored in `BmsBeatmapInfo.MeasureLengthControlPoints` as a sorted `IReadOnlyList<BmsMeasureLengthControlPoint>`. A missing entry for a measure implies `Multiplier = 1.0`.
>
> **Reading in the note placement layer:** When computing the absolute pixel position of a note, look up `BmsMeasureLengthControlPoints` by `MeasureIndex` to obtain that measure's multiplier before calculating beat offsets. The scroll renderer must query this list once per visible measure — do not cache per-note.
>
> **Reading in the bar-line renderer (`BmsPlayfield`):** Iterate measures in visible scroll range; for each measure, compute bar-line position using the accumulated `Multiplier` product from measure 0 to that measure. Only draw a bar line if `Multiplier` reduces to a simple fraction (denominator ≤ 16) to avoid sub-pixel artifacts on irrational values.

**Key layout by mode:**

| Mode | Play channels | Scratch |
|---|---|---|
| 5K (5+1) | `12`–`16` | `11` |
| 7K (7+1) | `12`–`18` | `11` |
| 9K (BMS) | `11`–`19` | — |
| 14K DP | `12`–`18` + `22`–`28` | `11` + `21` |

**Keymode auto-detection:** `BmsBeatmapDecoder` infers keymode after channel parse using this ordered rule set:

1. **`.pms` extension** → `Key9K_Pms`. No further analysis needed. PMS uses channels `11`–`19` mapped to 9 buttons in Pop'n Music order: `11`=Button1, `12`=Button2, …, `19`=Button9. In `BmsLaneLayout`, the `Key9K_Pms` lane order matches Pop'n convention (symmetrical button spread), distinct from `Key9K_Bms` which uses BMS channel order.
2. **Any note in channels `22`–`28`** → `Key14K` (DP). This rule must be checked early — 14K charts always have 1P-side notes that would otherwise trigger a 5K or 7K match. If both 1P and 2P channels are present, treat as 14K regardless of which 2P channels are used.
3. **`.bms` / `.bme` / `.bml` with channel `11` and no channels `12`–`19`** (i.e. only lane 1 used in the 1x series) → ambiguous; treat as `Key7K` with scratch on `11`.
4. **Any note in channels `12`–`16` and none in `17`–`18`** → `Key5K` (5+1 scratch on `11`).
5. **Any note in channels `17` or `18`** → `Key7K` (7+1 scratch on `11`).
6. **Notes in all of `11`–`19` with no scratch distinction pattern** → `Key9K_Bms`.

Store the resolved `BmsKeymode` in `BmsBeatmapInfo` immediately after decode. `BmsLaneLayout` and `BmsDifficultyCalculator` read from this field; they must never re-derive keymode independently.

**`#RANK` field mapping (default judge when no Mod active):**

| #RANK value | Judgment preset |
|---|---|
| 0 | VERY HARD |
| 1 | HARD |
| 2 | NORMAL (default) |
| 3 | EASY |
| 4 | VERY EASY |

### 4.2 BMS Package Import (`BmsArchiveReader`)

BMS beatmaps arrive as `.zip`, `.rar`, or `.7z` archives, not `.osz`.

Import pipeline:
1. User drops archive onto OMS window or uses Import dialog
2. `BmsArchiveReader` extracts to a temp directory
3. Scan extracted folder for any `.bms`/`.bme`/`.bml`/`.pms` file — each file is one **difficulty**
4. Group all difficulties sharing the same folder into one **BeatmapSet**, regardless of keymode differences. Files with different keymodes (e.g. a 5K chart and a 7K chart in the same folder) are not split into separate sets — keymode is stored as per-difficulty metadata and used for lane layout selection at play time. In the difficulty selector, the keymode is shown as a label on each difficulty entry (e.g. "7K", "5K").
5. Register keysound and BMP asset paths relative to the extracted folder root
6. Move extracted folder to OMS chartbms directory, clean up temp

Do **not** convert to `.osz`. OMS reads BMS files directly from disk at runtime.

Do **not** route imported BMS charts or their dependent assets through the generic `files/` hash-backed store. The extracted folder inside OMS chartbms is the source of truth; the database only persists metadata and path/location references needed for lookup and reload.

**Parse failure handling:** If `BmsBeatmapDecoder` throws or produces a critically incomplete result (no playable notes after channel parse, unrecognisable encoding after detection attempt), `BmsArchiveReader` must:
1. Log the error with the file path and exception message.
2. Skip the failed file — do not add it to the BeatmapSet.
3. If all `.bms`/`.bme`/`.bml`/`.pms` files in the archive fail, abort the import entirely and surface an error notification to the user: "Import failed: no valid BMS files found in archive."
4. If at least one file succeeds, complete the import and surface a warning notification that lists the skipped filenames.

### 4.3 Keysound System

BMS is fully keysounded — every note triggers a specific audio sample.

Current minimum implementation:
- Decode `#WAV##` entries into beatmap-relative lookup metadata during `BmsBeatmapConverter`
- Carry that lookup metadata on `BmsBgmEvent`, `BmsHitObject`, and `BmsHoldNote` runtime objects so gameplay drawables do not need a back-reference to decoder output
- Create nested `BmsHoldNoteTailEvent` objects when a long note defines a distinct tail keysound
- Route BGM / note / LN keysounds through a shared `BmsKeysoundStore` instead of letting each drawable own an independent keysound player

Requirements:
- Index up to 1295 (`ZZ` in base-36) keysound slots per chart
- **Supported audio formats:** `.wav` (primary — required), `.ogg` and `.mp3` (secondary — support if ManagedBass can load without additional plugins). Format resolution: attempt the exact filename referenced by `#WAV##` first; if not found, retry with extension substituted in order (`.wav` → `.ogg` → `.mp3`). Log a warning on any substitution. Do not silently succeed without logging.
- Load samples lazily (on first play), cache in memory during session
- Current code routes playback through a shared `BmsKeysoundStore` pool whose ceiling is exposed via `BmsRulesetConfigManager` / `BmsSettingsSubsection` as `KeysoundConcurrentChannels`; default is 32 and runtime/UI writes are clamped to `1..256`. The settings tooltip should continue to frame low values as truncation-prone and higher values as higher-cost, rather than implying that larger is always better. `DrawableBmsHitObject` currently auto-applies max result only for `BmsBgmEvent` and any `BmsHitObject` flagged with `AutoPlay = true`; ordinary single notes now accept player-triggered input via the temporary ruleset-local `BmsAction` bridge and resolve against default hit windows, while `DrawableBmsHoldNote` now accepts a valid head press, applies a basic tail release-lenience window, merges head/tail timing into a single final result, and only triggers the tail keysound when that final result is still a hit. POOR grading and full LN head/body/tail semantics remain future work.
- BGM channel (`01`) samples play regardless of player input
- Missing keysound files: log warning, play silence, do not crash
- On note hit: trigger the note's assigned keysound immediately
- On note miss/poor: BGM channel continues; note keysound is skipped
- **LN tail behaviour:** If a player releases an LN early or fails to release before the tail deadline, the tail is judged as POOR. If the tail has a distinct keysound slot assigned (some charts define separate `#WAV` entries for LN heads and tails), that tail keysound is skipped on miss. If no separate tail keysound is defined, no additional audio event is fired at the tail timing.

### 4.4 Long Note Handling

Support both LN encoding styles:

**`#LNOBJ xx`:** The object `xx` appearing in a lane terminates the most recent note in that lane as an LN tail. Standard LN body holds in between.

**`#LNTYPE 1`:** Channels `5x`/`6x` define LN lanes. Object start = LN head, next object = LN tail.

**`#LNTYPE 2`:** MGQ format — less common, implement after LNTYPE 1 is stable.

**Chart encoding and runtime long-note judgment mode are separate axes.** Parsing `#LNOBJ` / `#LNTYPE` only determines head/tail timing. Gameplay then applies a runtime `BmsLongNoteMode`, mirroring beatoraja's distinction between chart `lntype` and play `lnmode`.

| Runtime mode | Selection | Judged points | Body gauge behavior | EX-SCORE / MaxExScore pool |
|---|---|---|---|---|
| `LN` | Default, no Mod active | Head only | None | LN head only |
| `CN` | Optional Mod | Head + tail | None | LN head + tail |
| `HCN` | Optional Mod | Head + tail | Continuous hold gain/loss | LN head + tail |

- `LN` is the default OMS behavior. A chart with long notes does not require any Mod to remain playable.
- `CN` and `HCN` are mutually exclusive runtime options. The UI may expose them as two Mods or as one configurable Mod, but persistence, replay serialization, score storage, and server payloads must normalize them to a single `BmsLongNoteMode` enum value.
- Tail judgments in `CN` / `HCN` use the active judgement system's long-note-end window family, not the head window.
- `HCN` body gain/loss is conceptually continuous, but implementation must be time-based and deterministic rather than tied to render FPS. Accumulate elapsed time into a fixed tick quantum inside gameplay/gauge code.
- Local best scores, replays, leaderboard entries, and private-server submissions must include `BmsLongNoteMode` so `LN` / `CN` / `HCN` runs never share the same best-score bucket.

### 4.5 Beatmap Conversion (`BmsBeatmapConverter`)

`BmsBeatmapConverter` consumes the raw decoded BMS data from `BmsBeatmapDecoder` and produces a fully populated `IBeatmap` that the osu-framework game loop can execute. It is the single boundary between the BMS-specific parse world and the osu! runtime world.

**HitObject mapping:**

| BMS concept | osu! HitObject type | Notes |
|---|---|---|
| Single note (channels `1x`/`2x`) | `BmsHitObject` (extends `HitObject`) | Carries `LaneIndex`, `KeysoundId`, `KeysoundSample`, `IsScratch`, `AutoPlay` fields |
| LN head (LNOBJ or LNTYPE `5x`/`6x` start) | `BmsHoldNote` (extends `HitObject`) | `StartTime` = head timing; `Duration` = tail time − head time; carries `HeadKeysoundId` / `TailKeysoundId` and matching `HeadKeysoundSample` / `TailKeysoundSample` |
| LN tail | nested `BmsHoldNoteTailEvent` when a distinct tail keysound exists | Not a top-level HitObject in the beatmap list; runtime tail time remains `StartTime + Duration` and is materialised as a nested conditional event that only fires when the current minimal hold path reaches the tail or resolves a successful lenient tail release |
| BGM note (channel `01`) | `BmsBgmEvent` (non-hittable) | Carries beatmap-relative keysound sample metadata and plays through the shared `BmsKeysoundStore`; excluded from judgment and scoring |

`BmsHitObject` and `BmsHoldNote` must carry all keysound and lane metadata needed at runtime, including beatmap-relative sample lookup metadata. They must not require a back-reference to the raw decoder output after conversion.

`BmsBeatmapConverter` should continue to materialise one `BmsHoldNote` timing object per parsed long note. Whether that object's tail becomes a scored judgement point is decided later by `BmsLongNoteMode`: `LN` counts head only, `CN` / `HCN` count head + tail, and `HCN` additionally feeds body gauge ticks without changing beatmap conversion.

**ControlPointInfo population:**

All timing data is written into `IBeatmap.ControlPointInfo` in time-ascending order before any HitObjects are added.

1. **Initial BPM** — insert a `TimingControlPoint` at `t = 0` with the `#BPM` header value.
2. **Channel `03` BPM changes** — for each event: parse hex value → decimal BPM, insert `TimingControlPoint` at the computed absolute time.
3. **Channel `08` BPM table references** — resolve `#BPM##` entry, insert `TimingControlPoint` at computed time.
4. **Channel `09` STOP events** — resolve `#STOP##` table value. Each unit = 1/192 of a 4/4 measure. Convert to ms: `stop_ms = stop_value / 192.0 × 4.0 × (60000.0 / current_bpm)`. Insert a synthetic `TimingControlPoint` that sets BPM to an arbitrarily large value (effectively freezing scroll) for the stop duration, followed by a `TimingControlPoint` that restores the pre-stop BPM. **⚠ Risk note:** the extremely large BPM approach is fragile — if any note timing falls within the STOP window, spacing calculations may produce sub-pixel results. Validate during implementation that no HitObjects exist in the STOP interval.
5. **Channel `02` measure length multipliers** — store in a parallel `List<BmsMeasureLengthControlPoint>` on `BmsBeatmapInfo` (not in `ControlPointInfo`). See §4.1 implementation warning.

**Absolute time computation:**

All BMS timing is beat-based. Convert to absolute milliseconds using:

```
absolute_ms = measure_start_ms + (beat_fraction × beat_duration_ms)
beat_duration_ms = 60000.0 / current_bpm
measure_duration_ms = beats_per_measure × beat_duration_ms
beats_per_measure = 4.0 × measure_length_multiplier  // default multiplier = 1.0
```

Accumulate `measure_start_ms` by iterating through measures 0 → N in order, applying any Channel `02` multiplier and BPM changes encountered within each measure. All BPM changes within a measure are processed in beat-fraction order before the measure's notes.

**Output contract:**

After conversion, the `IBeatmap` must satisfy:
- `HitObjects` sorted ascending by `StartTime`
- `ControlPointInfo` sorted ascending by time with no overlapping entries at the same timestamp
- `BeatmapInfo.BeatmapSet` populated (set by `BmsArchiveReader` before converter runs)
- `BmsBeatmapInfo.Keymode` populated (set by `BmsBeatmapDecoder`)
- `BmsBeatmapInfo.MeasureLengthControlPoints` populated and sorted ascending

---

### 4.6 BPM Changes and STOP

- `#BPM` in header = initial BPM
- Channel `03` = inline BPM change (value is hex, convert to decimal)
- Channel `08` = BPM table lookup via `#BPM##` header
- Channel `09` = STOP (value references `#STOP##` table; each unit = 1/192 of a **4/4 measure** at the current BPM; conversion: `stop_ms = stop_value / 192.0 × 4.0 × (60000.0 / current_bpm)`)

All timing changes must be integrated into the `ControlPointInfo` of the converted `IBeatmap`. The underlying scroll-time model must use these timing points for note rendering; BMS now layers its own Hi-Speed surface on top rather than inheriting osu!mania scroll speed verbatim.

For osu!mania, settings-page milliseconds are only a reference under standard lane geometry. User skins may change lane size, hit position, and scaling, so mania fall-time readouts are not a cross-skin or cross-ruleset contract and must not be compared directly with BMS Hi-Speed milliseconds.

---

## 5. Judgment System

### 5.1 Architecture

`BmsJudgementSystem` is a pluggable class selected at game start based on active Mods.

```csharp
public abstract class BmsJudgementSystem
{
    public abstract HitResult Evaluate(double offsetMs, bool isLongNoteRelease);
    public abstract double[] Windows { get; } // [PGREAT, GREAT, GOOD, BAD, POOR] in ms
}
```

Three concrete implementations:

**`OsuOdJudgementSystem`** (default, no Mod active):  
Uses osu!mania OD timing windows. OD value sourced from `#RANK` field mapping:

| #RANK | OD |
|---|---|
| VERY HARD | OD 9 |
| HARD | OD 8 |
| NORMAL | OD 7 |
| EASY | OD 5 |
| VERY EASY | OD 3 |

**`BeatorajaJudgementSystem`** (Mod: `BmsModJudgeBeatoraja`):

Target parity is not a symmetric EASY-base scalar. The rule family must preserve judge-rank tiers, early/late asymmetry, and note-class special cases.

| Tier | PGREAT | GREAT | GOOD | BAD (early / late) |
|---|---|---|---|---|
| VERY EASY (125%) | ±25 | ±75 | ±187 | 275 / 350 |
| EASY (100%) | ±20 | ±60 | ±150 | 220 / 280 |
| NORMAL (75%) | ±15 | ±45 | ±112 | 165 / 210 |
| HARD (50%) | ±10 | ±30 | ±75 | 110 / 140 |
| VERY HARD (25%) | ±5 | ±15 | ±37 | 55 / 70 |

Additional constraints:
- Excessive poor uses its own early / late family rather than simply reusing BAD edges.
- Judge-rank scaling uses integer truncation semantics, not arbitrary rounding.
- Scratch notes extend both timing edges by an additional 10ms.
- Long-note release windows are significantly wider than normal note windows; long scratch release extends again on top of that.
- Active judgerank tier is still determined by `#RANK`.

**`Lr2JudgementSystem`** (Mod: `BmsModJudgeLr2`):

LR2 parity is also judge-rank-aware rather than a single fixed NORMAL window.

| Tier | PGREAT | GREAT | GOOD | BAD |
|---|---|---|---|---|
| EASY | ±21 | ±60 | ±120 | ±200 |
| NORMAL | ±18 | ±40 | ±100 | ±200 |
| HARD | ±15 | ±30 | ±60 | ±200 |
| VERY HARD | ±8 | ±24 | ±40 | ±200 |

Additional constraints:
- Excessive poor is only valid before the note, not after it.
- A fixed NORMAL-like window is an acceptable bootstrap, but not the final LR2 parity contract.

Judge mode is an explicit rule-family switch, not a generic "strictness" slider. UI, score tags, leaderboard filters, and any future table/course messaging must continue to surface `OD`, `BEATORAJA`, and `LR2` as separate semantics rather than implying direct equivalence.

### 5.2 Poor Judgment (Empty Poor)

**Empty Poor / excessive poor** is judge-family-specific, not a universal BAD-window fallback. Penalizes gauge.

Implementation:
- Maintain a per-lane active note-window state keyed by the active `BmsJudgeMode`
- `LR2` mode must treat excessive poor as pre-note-only semantics
- `BEATORAJA` mode must use its own early / late excessive-poor family rather than reusing generic BAD edges
- Scratch notes and long-note release may require specialized windows when the active judge family defines them
- `BmsPoorJudgement` triggers gauge damage equivalent to a BAD hit
- Empty Poor does **not** affect EX-SCORE
- Empty Poor **breaks Combo** (resets the current combo counter to 0)
- Empty Poor is only active in BMS mode, not osu!mania mode

### 5.3 Long Note Judge Modes

`BmsJudgementSystem` must expose both head windows and long-note-end windows. Runtime long-note mode determines which judged points actually participate in play:

| Mode | Head judgment | Tail judgment | Body ticks | EX-SCORE / MaxExScore | Combo / lamp eligibility |
|---|---|---|---|---|---|
| `LN` | Yes | No | No | Head only | Head only |
| `CN` | Yes | Yes | No | Head + tail | Head + tail |
| `HCN` | Yes | Yes | Yes, gauge-only | Head + tail | Head + tail |

Rules:
- `LN` is the default Phase 1 path and the fallback interpretation for any score record with no explicit long-note-mode tag.
- `CN` / `HCN` reuse the same chart long-note timing data; do not fork beatmap conversion per mode.
- `HCN` body ticks never add EX-SCORE or combo directly. They only modify gauge while the body is active.
- A broken `HCN` body may damage gauge without creating an extra result-screen judged note; only head/tail create judgement counts.

### 5.4 Combo Rules

| Judgment | Combo effect |
|---|---|
| PGREAT | +1 (continues combo) |
| GREAT | +1 (continues combo) |
| GOOD | +1 (continues combo) |
| BAD | reset to 0 (breaks combo) |
| POOR (miss) | reset to 0 (breaks combo) |
| Empty POOR | reset to 0 (breaks combo) |

Note: GOOD **does not break combo**, but having any GOOD in a run **disqualifies FULL COMBO** (§6.3). FULL COMBO requires PGREAT + GREAT only; PERFECT requires PGREAT only.

### 5.5 Keysound Trigger by Judgment

| Judgment | Keysound plays? |
|---|---|
| PGREAT | Yes — immediately on input |
| GREAT | Yes — immediately on input |
| GOOD | Yes — immediately on input |
| BAD | Yes — immediately on input (offset audible) |
| POOR (miss) | No — keysound is skipped; BGM channel continues normally |
| Empty POOR | No — no note keysound to trigger |

---

## 6. Scoring System

### 6.1 EX-SCORE

BMS mode uses EX-SCORE exclusively. Do not use osu's `ScoreProcessor`.

```
EX-SCORE = (PGREAT count × 2) + (GREAT count × 1)
MAX EX-SCORE = hittable_note_count × 2
```

`hittable_note_count` = all scored judgement points under the active runtime long-note mode:
- Single notes always count once
- `LN` mode counts each long note head once
- `CN` / `HCN` modes count long note head + tail
- `HCN` body ticks never increase `hittable_note_count`
- **When A-SCR Mod is active, scratch lane notes are excluded from `hittable_note_count`** — those notes are auto-triggered and outside the scoring pool

`BmsScoreProcessor` must track: PGREAT, GREAT, GOOD, BAD, POOR, EMPTY POOR, MAX COMBO, and the `BmsLongNoteMode` used to derive `MaxExScore`.

### 6.2 Gauge System (`BmsGaugeProcessor`)

Six gauge types, selectable via Mod. Default = NORMAL.

**Gauge Rate Derivation (`#TOTAL`)**

All recovery and damage rates are derived from the chart's `#TOTAL` header value and total hittable note count. Do not use fixed constants.

```
base_rate (% per note) = #TOTAL ÷ (total_hittable_notes × 100)
```

`total_hittable_notes` = all scored per-note judgement points that participate in gauge scaling under the active runtime long-note mode:
- Single notes always count once
- `LN` mode counts each long note head once
- `CN` / `HCN` modes count long note head + tail
- `HCN` body ticks are processed as time-based gauge events and do **not** change `total_hittable_notes`
- BGM channel (`01`) notes are excluded
- If `#TOTAL` is absent from the chart header, default to `200`
- **When A-SCR Mod is active, scratch lane notes are also excluded from `total_hittable_notes`** — consistent with their exclusion from `hittable_note_count` in §6.1

This keeps EX-SCORE, gauge scaling, and DJ LEVEL denominator aligned per long-note mode while still allowing `HCN` to add body-only gauge movement.

Each gauge applies multipliers to `base_rate` for recovery and damage. The `~%` values in the table below are **reference approximations** for a standard chart (`#TOTAL 200`, ~1000 hittable notes → `base_rate ≈ 0.2%`). Actual in-game values vary with chart parameters.

**Recovery multipliers per judgment (relative to PGREAT):**

| Judgment | Recovery multiplier |
|---|---|
| PGREAT | 1.0× (full recovery as listed in gauge table) |
| GREAT | 1.0× (same as PGREAT) |
| GOOD | 0.5× (half of PGREAT recovery) |

**Damage multipliers per judgment (relative to BAD):**

| Judgment | Damage multiplier | Notes |
|---|---|---|
| GOOD | 0.0× (no gauge damage) | Except HAZARD: GOOD deals 0% and does not trigger fail |
| BAD | 1.0× (as listed in gauge table) | |
| POOR (miss) | 1.5× of BAD damage | Note passed without any input |
| Empty POOR | 1.0× of BAD damage | Key pressed with no hittable note in window |

**Gauge parameters per type:**

| Gauge | Start | Recovery per PGREAT | Damage per BAD | Clear condition | Fail condition | Clear lamp awarded |
|---|---|---|---|---|---|---|
| ASSIST EASY | 20% | base_rate × 1.6 (~0.32%) | base_rate × 4.0 (~0.8%) | ≥80% at end | — | ASSIST EASY CLEAR |
| EASY | 20% | base_rate × 1.2 (~0.24%) | base_rate × 6.0 (~1.2%) | ≥80% at end | — | EASY CLEAR |
| NORMAL | 20% | base_rate × 0.8 (~0.16%) | base_rate × 8.0 (~1.6%) | ≥80% at end | — | NORMAL CLEAR |
| HARD | 100% | base_rate × 0.8 (~0.16%) | base_rate × 25.0 (~5%) | >0% at end | hits 0% mid-song | HARD CLEAR |
| EX-HARD | 100% | base_rate × 0.8 (~0.16%) | base_rate × 50.0 (~10%) | >0% at end | hits 0% mid-song | EX-HARD CLEAR |
| HAZARD | 100% | base_rate × 0.8 (~0.16%) | instant 0% | >0% at end | any BAD/POOR mid-song | HAZARD CLEAR (if no BAD/POOR) |

> **HAZARD gauge and GOOD:** GOOD judgements deal **0% gauge damage** in HAZARD mode and do not trigger the fail condition. Only BAD and POOR collapse the gauge to 0% immediately. This must be enforced explicitly — do not apply any damage formula on GOOD when HAZARD is active.

ASSIST EASY, EASY, and NORMAL gauges cannot drop below 2% mid-song (survival floor).

**Ranking:** Current local score bucketing must continue distinguishing gauge, judge, and long-note semantics where those rules are already implemented. Full online leaderboard filtering remains a Phase 3 contract, and any dedicated `A-SCR` filter is additionally blocked on leaderboard/config support landing.

**Phase 3 target leaderboard filters:** Once private server integration is intentionally enabled, chart leaderboards should accept independent `gauge`, `judge`, and `lnmode` filters, for example:

```
GET /scores/chart/{hash}?gauge=HARD&judge=OD&lnmode=CN
```

If `A-SCR` leaderboard segregation is added later, an additional `ascr` filter may be added at the same time. Do not document these filters as current `BmsRulesetConfigManager` settings until the corresponding code is present in the workspace.

### 6.3 Clear Lamp (`BmsClearLampProcessor`)

Track the best clear lamp per chart per player. Lamp hierarchy (lowest → highest):

```
NO PLAY → FAILED → ASSIST EASY CLEAR → EASY CLEAR → NORMAL CLEAR
  → HARD CLEAR → EX-HARD CLEAR → HAZARD CLEAR → FULL COMBO → PERFECT (MAX)
```

- ASSIST EASY CLEAR = passed with ASSIST EASY gauge
- EASY CLEAR = passed with EASY gauge
- NORMAL/HARD/EX-HARD CLEAR = passed with corresponding gauge
- HAZARD CLEAR = survived HAZARD gauge to end (no BAD/POOR, but GOOD is permitted — GOOD does not trigger HAZARD fail). Note: HAZARD CLEAR requires no BAD or POOR but allows GOOD, so it is strictly below FULL COMBO.
- FULL COMBO = no GOOD/BAD/POOR throughout the chart
- PERFECT = EX-SCORE == MAX EX-SCORE
- FULL COMBO / PERFECT use the active runtime long-note mode's scored points. `LN` only checks heads; `CN` / `HCN` require both head and tail to remain eligible. `HCN` body ticks can still fail gauge without adding separate combo points, so result persistence must confirm the run still satisfies the clear condition before awarding `FULL COMBO` or `PERFECT`.
- Result-side clear-lamp / final-gauge / gauge-history reconstruction must reapply the full BMS beatmap-mod chain, not only `BmsLongNoteMode`. Assist mods such as `A-SCR` / `A-NOT` also remove objects from score/gauge pools, and results must remain gameplay-identical.

Only upgrade lamp, never downgrade.

**Current note:** Now that `A-SCR` and `A-NOT` exist, score-pool exclusion, gauge exclusion, and result-side lamp/gauge reconstruction rules must stay specified together with the mods. Do not silently diverge score / gauge / lamp semantics across later leaderboard, persistence, or results-history work.

### 6.4 Gauge Auto Shift — GAS (`BmsModGaugeAutoShift`)

GAS is a Mod that starts the player on the highest configured gauge and automatically downgrades when that gauge fails, continuing play on the next lower gauge. At the end of the song, the best clear result across all gauges played is awarded.

**Mechanism (matching beatoraja `GAUGEAUTOSHIFT_BESTCLEAR` behavior):**

1. Player selects a starting gauge (default: EX-HARD) and a floor gauge (default: EASY).
2. At song start, the active gauge is set to the starting gauge.
3. If a survival gauge (HAZARD, EX-HARD, HARD) reaches 0%, the gauge **downgrades** to the next lower tier in this sequence. In HAZARD mode, any single BAD or POOR collapses the gauge to 0% and triggers an immediate downgrade — GOOD does not trigger downgrade.
   ```
   HAZARD → EX-HARD → HARD → NORMAL → EASY → ASSIST EASY
   ```
   Downgrade stops at the configured floor gauge.
4. When downgrading, the new gauge initializes at its default start value (not carried over).
5. Play continues without interruption — no fail screen is shown on downgrade.
6. **NORMAL, EASY, and ASSIST EASY gauges do not trigger further downgrade.** Once the active gauge has shifted to one of these non-survival tiers, it plays to song end regardless of final gauge value — there is no mid-song fail condition on these gauges.
7. At the end of the song, `BmsClearLampProcessor` evaluates each gauge independently and awards the highest lamp achieved.
8. The result screen displays a gauge graph for each tier that was active during the run (matching beatoraja result screen behavior).

**GAS settings (exposed as Mod configuration):**

```csharp
// BmsMod is the abstract base class for all BMS-specific mods.
// It extends osu!'s Mod class and may add BMS-specific validation hooks.
public class BmsModGaugeAutoShift : BmsMod
{
    // Highest gauge to start on
    public Bindable<BmsGaugeType> StartingGauge { get; } = new(BmsGaugeType.ExHard);

    // Lowest gauge allowed to downgrade to (floor)
    public Bindable<BmsGaugeType> FloorGauge { get; } = new(BmsGaugeType.Easy);
}
```

`StartingGauge` and `FloorGauge` are persisted via osu!'s standard Mod configuration serialization — their values survive across sessions without manual re-selection. The defaults (`ExHard` / `Easy`) apply only on first launch before any user configuration has been saved.

`BmsGaugeType` enum order must match downgrade sequence: `AssistEasy = 0, Easy = 1, Normal = 2, Hard = 3, ExHard = 4, Hazard = 5`. HAZARD is the highest tier. It fails on BAD or POOR only — GOOD is permitted and does not trigger a downgrade.

**Interaction with other gauge Mods:**  
`BmsModGaugeAutoShift` is mutually exclusive with all individual gauge Mods (`BmsModGaugeHard`, `BmsModGaugeExHard`, etc.). When GAS is active, those Mods are disabled.

**Score submission with GAS:**  
Scores set with GAS active are tagged with `gauge_mode = GAS` on the private server. The lamp submitted is the best lamp achieved during the run.

### 6.5 Auto Assist Mods — A-SCR / A-NOT

Current repository status: `BmsModAutoScratch` and `BmsModAutoNote` now exist in the current workspace as `DifficultyReduction` mods. The current implementation exposes mod-local `ScratchVisibility` / `TintScratchNotes` / `ScratchTintColour` and `NoteVisibility` / `TintNotes` / `NoteTintColour` settings. Configurable BMS mods now also use a BMS-only ruleset-config snapshot for selection/config persistence; leaderboard filters remain future work.

**Current behavior / contract:**

- Scratch notes may be auto-triggered for audio-only handling and excluded from manual scoring / gauge / combo.
- Non-scratch notes may be auto-triggered for the same assist purpose and excluded from manual scoring / gauge / combo.
- `A-SCR` and `A-NOT` are mutually incompatible, and both remain incompatible with full `autoplay`.
- The feature must remain an opt-in assist path and must not become the default BMS teaching baseline.
- Any score tagging, lamp handling, or leaderboard filtering for `A-SCR` must land together with the gameplay implementation rather than being documented ahead of code.

**Current BMS mod-state persistence contract:**

- Configurable BMS mods now use a BMS-only ruleset-config snapshot (`BmsRulesetConfigManager.PersistedModState`) that remembers both selected-mod order and non-default per-mod settings across restart and BMS ↔ mania ruleset switches.
- Disabling a configurable BMS mod is not treated as a request to reset it; if the mod opts into preserved settings, re-enabling it must restore the last remembered configuration.
- This contract is currently BMS-only and must not be generalized to mania or to a global cross-ruleset `SelectedMods` persistence layer without a separate design and product contract.

**Planned `BmsRulesetConfigManager` additions when A-SCR / leaderboard filters land:**

```csharp
AutoScratchNoteVisibility  : AscScratchVisibility  = Visible
LeaderboardGaugeFilter     : string?               = null
LeaderboardAscrFilter      : bool?                 = null
LeaderboardJudgeFilter     : string?               = null
LeaderboardLnModeFilter    : string?               = null
```

`BmsKeysoundStore` already uses `KeysoundConcurrentChannels` as its persistent shared-pool ceiling. Keep that ceiling sourced from `BmsRulesetConfigManager`; add new persistent state only when the consuming feature lands.

### 6.6 DJ LEVEL (`BmsDjLevelCalculator`)

Calculated from EX-SCORE percentage at result screen:

| EX% | DJ LEVEL |
|---|---|
| ≥ 8/9 (~88.9%) | AAA |
| ≥ 7/9 (~77.8%) | AA |
| ≥ 6/9 (~66.7%) | A |
| ≥ 5/9 (~55.6%) | B |
| ≥ 4/9 (~44.4%) | C |
| ≥ 3/9 (~33.3%) | D |
| ≥ 2/9 (~22.2%) | E |
| < 2/9 | F |

This intentionally follows the 27-step beatoraja / IIDX rank ladder: `AAA = 24/27`, `AA = 21/27`, `A = 18/27`, and so on.

### 6.7 Timing Feedback and Offset

- Gameplay-facing BMS feedback should prioritize judgement name, `FAST/SLOW`, and EX-SCORE-relevant deltas during play. Do not frame BMS performance primarily around osu!-style accuracy messaging.
- For key-sounded BMS play, the primary user-facing timing correction path is a visual draw offset / note presentation adjustment. Do not make audio playback offset the default or sole timing-fix control for BMS mode.
- Timing feedback and adjustment should be low-friction from gameplay and result flow so future retry / pacemaker / target-practice features can reuse the same loop.

---

## 7. Layout System

### 7.1 Lane Configuration (`BmsLaneLayout`)

BMS mode does not use osu!mania's `ManiaStage` directly. Define a `BmsLaneLayout` that specifies:

- Number of lanes
- Lane widths (scratch lane is wider than key lanes)
- Lane colors (alternating key colors per BMS convention)
- Scratch lane position (leftmost for 1P, rightmost for 2P)

Current Phase 1 surface: settings now expose a basic single-play `Playfield Style` with four options for 5K / 7K only: `1P (left anchored with intentional screen-side inset)`, `2P (right anchored with intentional screen-side inset)`, `Center (left scratch)`, and `Center (right scratch)`. This adjusts playfield anchoring and scratch visual side without flipping bindings or introducing a full side-aware skin contract. 9K remains centered and 14K remains fixed DP.

**Phase 2 target — 1P/2P flip (`BmsModMirror1P2P`):**
- Current repository status: the workspace now has `BmsModMirror` and `BmsModRandom` for button-lane rearrangement, but it still does not have a dedicated full-side `1P/2P flip` mod.
- Mirrors the entire lane array horizontally
- Updates all key bindings to their mirrored counterparts
- Scratch moves from left to right (or vice versa)
- Skin elements that are side-dependent must respond to a `CurrentSide` bindable (1P/2P)

### 7.2 Lane Cover (`BmsLaneCover`)

Sudden and Hidden are rendered as opaque overlay panels on the playfield, while Lift independently raises the judgement line by shortening the lane from the bottom.

Controlled by Mods:
- `BmsModSudden` — enables upper masking, exposes a `CoverPercent` setting (0–100%)
- `BmsModHidden` — enables lower masking, exposes a `CoverPercent` setting (0–100%)
- `BmsModLift` — raises the judgement line with an independent `LiftUnits` setting (0–1000)

Sudden and Hidden can be active simultaneously. In gameplay, the scroll wheel adjusts the current persistent range target without pausing: default target order prefers Sudden, and clicking `UI_LaneCoverFocus` (or mouse middle-click) cycles across enabled `Sudden / Hidden / Lift` targets. Lift remains a separate geometry control and must not be conflated with Hidden.

Current Phase 1 contract: configurable BMS mods keep their remembered settings across deselect / re-enable and across BMS session restoration. `BmsModSudden`, `BmsModHidden`, and `BmsModLift` also expose a `Remember gameplay changes` toggle (default `true`). When enabled, gameplay wheel adjustments must write back to the selected BMS mod instance and its persisted ruleset snapshot; when disabled, those adjustments remain current-play-only.

### 7.3 Scroll Speed

OMS now exposes a BMS-local tri-mode Hi-Speed surface rather than inheriting osu!mania scroll speed verbatim.

- `Normal Hi-Speed`: user-facing range `1.0 - 20.0`; primary general-purpose surface.
- `Floating Hi-Speed`: user-facing range `0.5 - 10.0`; current OMS implementation anchors visual speed to the chart's initial BPM and is intended to cooperate with `Sudden / Hidden / Lift`, but it does **not** yet claim full mid-song re-float parity or soflan GN-range output.
- `Classic Hi-Speed`: user-facing range `0.5 - 10.0`; base time mapping must remain `(100000 / 13) / HS` so the official sample `HS 10 + WN 350 => GN 300` continues to hold.

Settings may show the selected Hi-Speed mode, that mode's numeric value, and the base fall time in milliseconds when `Sudden / Hidden / Lift` are not applied. Do not surface `Green Number` itself or runtime-adjusted visible milliseconds in settings; those remain gameplay-runtime feedback only.

OMS may surface `Green Number` / `White Number` during gameplay as part of its current BMS runtime speed-feedback model, but that model is presently scoped to `Normal / Floating / Classic Hi-Speed + Sudden / Hidden / Lift` and must not be described as proof that full IIDX-style Floating Hi-Speed parity already exists.

Current BMS gameplay also includes a `阻止谱面开始/ingame start` operator surface backed by `UI_PreStartHold`: entering gameplay inserts a 5-second delayed-start window, and holding that action during the window blocks the actual start while showing the selected Hi-Speed mode and current value. The same held action remains a full-session adjustment modifier after gameplay has started: odd-numbered lanes increase the current Hi-Speed, even-numbered lanes decrease it, `UI_LaneCoverFocus` / scroll-wheel / middle-click lane-cover controls remain available, and the centered `BMS speed` toast should stay visible while the modifier is held. While the modifier is held, new lane actions must not be forwarded into gameplay hit handling; they belong to the adjustment chain only. `UI_PreStartHold` and `UI_LaneCoverFocus` are separate actions with independent key bindings (default 5K/7K/9K: PreStartHold = Q, LaneCoverFocus = W; 14K: PreStartHold = T, LaneCoverFocus = Y). Treat this as runtime operator interaction, not as a settings-page preview or a replacement for the skinnable HUD contract.

If OMS later extends Floating Hi-Speed semantics, ship it as a complete contract across scroll speed, lane cover, LIFT, BPM compensation, start-sequence behavior, and displayed terminology. Do not market or document the current OMS-local GN/WN feedback as complete FHS.

---

## 8. Input Abstraction Layer (`oms.Input`)

### 8.1 Design Goal

All input hardware — keyboard, IIDX controller, arcade controller, gamepad — must map to the same abstract `OmsAction` enum. The game layer never reads hardware signals directly.

Current implementation note: `osu.Game.Rulesets.Bms` is now partially wired to `oms.Input`. The current playable prototype still relies on a ruleset-local `BmsAction` bridge (`Key1`-`Key16` + `LaneCoverFocus` + `PreStartHold`) as temporary scaffolding, but `OmsAction <-> BmsAction` routing, complete keyboard-combination semantics, the Windows Raw Input keyboard path, mouse delta parsing, the XInput button path via `OnJoystickPress()` / `OnJoystickRelease()`, 5K/7K default XInput bindings, ruleset default keybinding export for joystick buttons, joystick-only persisted binding round-tripping, the generic keybinding UI path for joystick button display/capture, HID-trigger persistence/editor live capture, and the provider-backed HID code path are all present. Windows now uses a DirectInput-backed HID provider by default, while `HidSharp` remains available as a diagnostic backend behind `OMS_ENABLE_HIDSHARP=1` to avoid the historical `HidSharp.DeviceList.Local` `RegisterClass failed` crash path. Desktop Settings -> Input now intentionally hides the upstream generic `MouseSettings` / `TouchSettings` / `TabletSettings` subsections via `OsuGameDesktop.CreateSettingsSubsectionFor()`; this is a product-surface trim only, not a runtime input deletion. Do not move that suppression into `OsuGameBase` unless the intention is to also change test-scene and non-desktop host behaviour. Remaining input work is mainly richer cross-device semantics and real-hardware validation. Treat the current bridge as temporary scaffolding, not the final input contract.

```csharp
public enum OmsAction
{
    // BMS 7K+1 (1P)
    Key1P_Scratch, Key1P_1, Key1P_2, Key1P_3, Key1P_4,
    Key1P_5, Key1P_6, Key1P_7,
    // BMS 7K+1 (2P)
    Key2P_Scratch, Key2P_1, Key2P_2, Key2P_3, Key2P_4,
    Key2P_5, Key2P_6, Key2P_7,
    // 9K
    Key9K_1, Key9K_2, Key9K_3, Key9K_4, Key9K_5,
    Key9K_6, Key9K_7, Key9K_8, Key9K_9,
    // UI / System
    UI_Confirm, UI_Back, UI_ModMenu, UI_LaneCoverAdjust,
    UI_LaneCoverFocus,  // Click to cycle scroll-wheel target across enabled Sudden/Hidden/Lift (§7.2)
    UI_PreStartHold,     // Hold to block gameplay start and open pre-start adjust overlay (§7.3)
    // ... extend as needed
}
```

> **5K mapping:** 5K mode reuses a subset of the 7K+1 actions: `Key1P_Scratch`, `Key1P_1`–`Key1P_5`. Actions `Key1P_6` and `Key1P_7` are unused in 5K. The binding profile for 5K only exposes the relevant 6 actions.

### 8.2 Signal Handlers

**`OmsKeyboardInputHandler`:**  
Consumes resolved lazer `KeyCombination` state and maps complete keyboard combinations to `OmsAction`. On Windows, raw keyboard events are additionally fed through `WindowsRawKeyboardSource -> IOmsKeyboardEventSource -> IOmsKeyboardEventSink -> BmsInputManager` so the gameplay path is not limited to framework-level key events.

**`OmsHidDeviceHandler`:**  
Current implementation uses a provider-backed polling path for HID buttons and axes. Windows defaults to a DirectInput backend for enumeration/polling/capture, while `HidSharp` remains the non-Windows path and an opt-in Windows diagnostic backend behind `OMS_ENABLE_HIDSHARP=1`; this avoids the historical `DeviceList.Local` `RegisterClass failed` crash path while preserving a fallback for investigation. Remaining work is device coverage validation and richer cross-device semantics rather than the core Windows backend swap itself.

**`OmsXInputButtonInputHandler`:**  
For Xbox-compatible controllers / framework joystick buttons. Maps button indices to `OmsAction`, supports shared-action reference counting, and now participates in both default binding export and joystick-only persisted keybinding round-trips.

**`OmsMouseAxisInputHandler`:**  
Reads raw mouse delta (X or Y axis) and converts to scratch input. Define a threshold and direction: positive delta = CW, negative delta = CCW. Expose sensitivity setting.

**Axis inversion:** All analog axis handlers (`OmsHidAxisInputHandler` for rotary encoders, `OmsMouseAxisInputHandler`) must expose an `AxisInverted` boolean flag per binding in `OmsBindingStore`. DIY controller encoder wiring varies in polarity; inverting the axis direction in software avoids hardware rewiring. When `AxisInverted = true`, the CW/CCW mapping is swapped before delivering the signal to `OmsInputRouter`. The flag is stored per-binding entry in `OmsBindingStore` — each bound action carries its own independent inversion state. This applies equally to HID rotary encoders and mouse axis bindings; it is not a global mouse preference.

### 8.3 Scratch / Analog Axis Handling

The scratch lane accepts three signal types simultaneously:

1. **Digital key** (keyboard or HID button): binary on/off, treated as a constant scratch input while held
2. **Analog axis** (HID rotary encoder, e.g. AS5600 on DIY controller): continuous value, converted to delta → scratch direction + velocity
3. **Mouse delta**: same as analog but sourced from mouse movement

All three can be bound at once. The scratch lane renders activation whenever any active signal exceeds its threshold.

### 8.4 Binding UI

The binding screen must support:
- Listening for any of the four signal types when recording a binding
- Displaying bound signal type with icon (keyboard key / HID button / axis / mouse)
- Separate binding profiles per keymode (5K / 7K / 9K / 14K DP)
- In 14K DP mode, a single **DP profile** contains both 1P-side and 2P-side bindings simultaneously — the binding UI presents both sides in a unified layout. There are no separate "14K 1P" and "14K 2P" profiles.
- For single-side modes (5K / 7K / 9K), per-profile 1P/2P binding sets distinguish which physical side is active

### 8.5 Calibration and Diagnostics

Bindings alone are not sufficient for BMS controller support. The BMS input/settings surface must eventually expose user-visible calibration and diagnostics for deadzone, sensitivity, scratch signal expectations (digital vs analog), side mapping, and live input preview / sanity checks. Keep this hardware-tuning layer separate from per-chart gameplay logic, but do not leave it hidden behind backend-only code paths.

---

## 9. Difficulty System

### 9.1 Design Principle

osu!lazer's built-in star rating algorithm is designed for osu!mania key patterns and is not applicable to BMS. BMS mode uses an independent difficulty calculator (`BmsDifficultyCalculator`) based on **weighted note density**, producing a star value used as a fallback when no difficulty table entry exists for a chart.

The difficulty table community ratings (Satellite, Stella, 発狂BMS, etc.) are the authoritative source of difficulty for charts that have been rated. The density star is a supplementary metric only.

### 9.2 Density Star Calculation (`BmsDifficultyCalculator`)

The calculator operates on the converted `IBeatmap` after `BmsBeatmapConverter` has run.

**Input:** note timing list with per-note metadata (lane index, is-scratch, is-LN-head, is-LN-tail, is-chord).

**Algorithm — sliding window density:**

1. Divide the chart into overlapping windows of fixed duration (1000ms, step 500ms)
2. For each window, compute a **weighted note count**:
   ```
   base weight per note  = 1.0
   chord bonus           = +0.3 per additional simultaneous note in the same tick (same `StartTime` within ≤1ms tolerance)
   scratch bonus         = +0.5 (scratch notes require split attention)
   LN body               = +0.1 per 100ms of hold duration
   ```
3. Take the 95th percentile window density across the chart (ignores outlier bursts)
4. Normalize against a per-keymode reference density to produce a 0–20 star scale
5. Store results in `BmsDifficultyAttributes`

**`BmsDifficultyAttributes` fields:**

```csharp
public class BmsDifficultyAttributes : DifficultyAttributes
{
    public double StarRating       { get; set; }  // density-based star rating (0–20 scale)
    public int    TotalNoteCount   { get; set; }  // all hittable notes including LN heads
    public int    ScratchNoteCount { get; set; }  // notes in scratch lane (channels 11/21)
    public int    LnNoteCount      { get; set; }  // LN head count (not tails)
    public double PeakDensityNps   { get; set; }  // highest weighted notes-per-second across all windows
    public double PeakDensityMs    { get; set; }  // chart position (ms) of the peak density window start
}
```

`BmsNoteDistributionGraph` reads `TotalNoteCount`, `ScratchNoteCount`, `LnNoteCount`, and `PeakDensityNps`/`PeakDensityMs` directly from the cached attributes — do not recompute these statistics in the graph layer.

**BPM normalization:** density is computed in notes-per-second space, not notes-per-beat, so BPM changes are naturally accounted for.

**Per-keymode calibration:** normalization reference constants are stored in a static lookup per `BmsKeymode` enum value (5K, 7K, 9K, 14K). These constants are expected to be tuned over time as playtest data accumulates — do not over-engineer the first implementation.

`BmsDifficultyCalculator` delegates all sliding-window computation to `BmsNoteDensityAnalyzer` (see Section 9.3). Do not duplicate the density logic here.

---

### 9.3 Shared Density Utility (`BmsNoteDensityAnalyzer`)

The sliding-window density computation is shared between `BmsDifficultyCalculator` and `BmsNoteDistributionGraph`. Extract this logic into a dedicated utility class to avoid parallel implementations diverging:

```csharp
public class BmsNoteDensityAnalyzer
{
    // Returns density buckets across the chart timeline
    public IReadOnlyList<DensityBucket> Analyze(
        IBeatmap beatmap,
        int windowMs = 1000,
        int stepMs   = 500);

    // Returns the Nth-percentile density value across all buckets
    public double GetPercentileDensity(
        IReadOnlyList<DensityBucket> buckets,
        double percentile);
}

public record DensityBucket(double StartMs, double WeightedNoteCount, int NormalCount, int ScratchCount, int LnCount);
```

- `BmsDifficultyCalculator` calls `Analyze()` then `GetPercentileDensity(buckets, 0.95)` for star rating
- `BmsNoteDistributionGraph` calls `Analyze(windowMs: 1000, stepMs: 1000)` explicitly — do not rely on default parameter values. Reuses cached output if available for the selected chart.

### 9.4 Difficulty Display Priority

```
1. Difficulty table match found  →  table label (e.g. "Satellite ★5") as primary
                                     density star as secondary
2. No table match                →  density star only
```

`BeatmapInfo.StarRating` is always populated with the density star. Table match data is stored as a serialized list of `BmsDifficultyTableEntry` records on the beatmap metadata (not a single string field — a chart may appear in multiple tables). Song Select / details / note distribution consumers read the persisted metadata; `BmsTableMd5Index` is now only an importer-time and in-memory lookup helper, not the display-layer authority.

Chart-level metadata (`Subtitle`, `SubArtist`, `Comment`, `PlayLevel`, `HeaderDifficulty`) is stored on `BmsBeatmapMetadataData.ChartMetadata`. When a clear creator credit can be inferred from chart metadata, mirroring it to `BeatmapMetadata.Author.Username` is allowed so generic UI can show a creator without BMS-specific plumbing.

If future BMS-only Song Select filter data is added, store it as typed data on `BmsBeatmapMetadataData` with helper methods rather than parsing `BeatmapMetadata.RulesetDataJson` ad hoc from the filter chain. Ruleset-specific song select keywords must continue to flow through `IRulesetFilterCriteria`; do not add BMS-only switch cases to the shared `FilterQueryParser`.

---

## 10. Difficulty Table System

### 10.1 Overview

BMS difficulty tables are community-maintained external resources mapping chart MD5 hashes to level labels. They are **independent of the OMS private server**. OMS supports both built-in preset tables and player-added custom tables via URL — both use the same subscription mechanism.

Chart identity uses the **MD5 hash of the raw `.bms`/`.bme`/`.bml`/`.pms` file** (lowercase hex). SHA256 and `parent_hash` (sabun/derivative chart linkage) are Future Scope.

### 10.2 Preset Tables

OMS ships with a built-in list of well-known community table slots. These are seeded from the bundled `bms_table_presets.json` resource and materialised in SQLite as disabled placeholder rows with `is_preset = true` and `local_path = null`.

**Default seeded presets:**

| Source name | Display name | Default symbol |
|---|---|---|
| `satellite` | `Satellite` | `★` |
| `stella` | `Stella` | `★` |
| `normal1` | `Normal1` | `☆` |
| `insane1` | `Insane1` | `★` |
| `normal2` | `Normal2` | `☆` |
| `insane2` | `Insane2` | `★` |
| `ln` | `LN` | `ln★` |

The resource stores seeded preset identity (`source_name`, `display_name`, `symbol`), not first-run download URLs.

The first-run wizard maintains a separate curated list of zris preset URLs. When an imported table matches an unclaimed seeded preset by `source_name` or `display_name`, `BmsDifficultyTableManager` should auto-claim that preset row instead of creating a parallel custom source.

Players may also add arbitrary local paths or public URLs as custom sources. Custom sources are stored alongside presets in the same SQLite subscription list, differentiated by `is_preset = false`.

Removing an imported preset from settings should clear its imported data and restore the hidden preset placeholder, not permanently delete the seeded preset row.

### 10.3 Table Subscription (`BmsDifficultyTableManager`)

Supported source locations:

- local directory, HTML wrapper, header JSON, or body JSON
- `http` / `https` URL pointing to a BMSTable HTML wrapper, header JSON, or body JSON

**BMSTable format (standard — used by Satellite, Stella, and most modern tables):**

Most community tables use a two-step fetch:
1. The subscription URL points to an HTML page containing `<meta name="bmstable" content="header.json">`.
2. Fetch the header JSON (URL resolved relative to the HTML page):
   ```json
   {
     "name": "Satellite",
     "symbol": "★",
     "data_url": "score.json"
   }
   ```
3. Fetch the body JSON referenced by `data_url`:
   ```json
   [
     { "md5": "abc123...", "level": "1", "title": "...", "artist": "..." },
     { "md5": "def456...", "level": "2", "title": "...", "artist": "..." }
   ]
   ```
   Body format is a flat array of chart entries. `level` is a string (may contain non-numeric labels like "▲10").

`BmsDifficultyTableManager` must implement this resolution chain for both local files and remote URLs. If the source points directly to a JSON file (no HTML wrapper), attempt to parse it as either a header JSON or a body array. HTML without a `bmstable` meta tag is not a supported source.

`BmsDifficultyTableManager` responsibilities:
- Persist subscription list (`source_name`, `display_name`, `symbol`, `local_path`, `is_preset`, `enabled`, timestamps) in SQLite; `local_path` is the unified source location and may be a full local path or an absolute URL
- Fetch and cache table data on import and on manual refresh
- Retry remote downloads on transient failures using request-scoped timeout/retry policy (currently 20s, then 60s for timeout / `408` / `429` / `5xx`-style failures)
- Auto-claim matching seeded presets instead of duplicating them as custom rows
- Restore seeded preset placeholders when removing an imported preset source
- Expose `RefreshAllTables()` and `RefreshTable(id)` async methods
- Share one manager instance between settings and first-run via `GetShared(storage)`
- Rebuild persisted beatmap metadata after source mutations, then emit `TableDataChanged` so `BmsTableMd5Index` can rebuild its in-memory cache
- Disabled tables are excluded from index rebuild and song select grouping

### 10.4 MD5 Matching And Persistence Pipeline

**On beatmap import:**
1. After `BmsArchiveReader` extracts the archive, compute MD5 of each `.bms` file
2. Store the lowercase MD5 in `BeatmapInfo.MD5Hash` (`BeatmapInfo.Hash` continues to hold the SHA-2 content hash used for duplicate detection)
3. Query `BmsTableMd5Index` immediately — if a match exists, write `BmsDifficultyTableEntry` records to beatmap metadata and persist

**On table refresh:**
1. Fetch new table JSON, parse into `List<BmsDifficultyTableEntry>`
2. Build the current enabled MD5 lookup from cached source rows
3. Diff the new lookup against the previous lookup to find affected MD5 values
4. Batch-update persisted beatmap metadata for affected local BMS beatmaps
5. Rebuild the in-memory `BmsTableMd5Index`
6. Emit `TableDataChanged` so consumers refresh from persisted metadata

**In-memory index structure:**
```csharp
// Key: MD5 hex string (lowercase)
// Value: all matched entries across all enabled tables
Dictionary<string, List<BmsDifficultyTableEntry>> md5ToEntries;
```

Index is rebuilt at startup (from cached DB data — no network required) and after any table mutation. `BmsTableMd5Index` no longer owns persisted metadata writes.

**`BmsDifficultyTableEntry` fields:**
```csharp
public record BmsDifficultyTableEntry(
    string TableName,      // e.g. "Satellite"
    string Symbol,         // e.g. "★"
    int    Level,          // numeric level for sorting
    string LevelLabel,     // display string e.g. "★5"
    string Md5             // lowercase hex
);
```

### 10.5 Song Select Integration (`BmsTableGroupMode`)

`BmsTableGroupMode` is a custom `GroupDefinition` registered only when the active ruleset is BMS. It appears as an option in the native osu!lazer sort/group dropdown.

**Grouping hierarchy:**

```
Group level 1 — Table name
  e.g. "Satellite", "Stella", "発狂BMS", "Unrated"
  ordered by source sort order (seeded preset order first, then user import order)

Group level 2 — Level within table
  e.g. "★1", "★2", "▲18"
  sorted by numeric Level field ascending

Group level 3 — BeatmapSet (native carousel node)
  = one BMS folder
  sorted by density star ascending within the level group

Difficulty — individual .bms file
  sorted by density star ascending
```

**"Unrated" group:** charts with no match in any enabled table, sorted by density star ascending. Always appears last.

**Multi-table charts:** a chart appearing in multiple enabled tables appears under each table's group independently.

**Sort interaction:** when `BmsTableGroupMode` is active, the sort dropdown is disabled — within-group sort is fixed to density star ascending. Search and collections continue to work normally.

### 10.6 Library Grouping (`InternalLibrary` / `ExternalLibrary`)

OMS now provides two additional **BMS-only** song-select grouping modes: `InternalLibrary` and `ExternalLibrary`.

This is not a shared osu! song-select feature. The dropdown remains ruleset-driven via `Ruleset.GetAvailableSongSelectGroupModes()`, and non-BMS rulesets must not expose these modes.

**Shared grouping-engine contract:** once BMS has more than one hierarchical grouping mode, shared code must stop hardcoding `GroupMode.DifficultyTable` as the only hierarchical path. `BeatmapCarouselFilterGrouping` should instead use a generic ruleset-specific hierarchical grouping path whenever the active ruleset returns `GroupDefinition` data for the selected mode. Do not add a second or third one-off special case.

**Enum / config safety:** append new `GroupMode` values at the end of the enum (or otherwise preserve persisted numeric compatibility). OMS must not churn the stored `SongSelectGroupMode` value for existing users just because BMS adds new modes.

**`InternalLibrary` grouping authority:**

- Applies only to BMS sets where `BeatmapSetInfo.IsExternalFilesystemStorage == false`
- Applies only when `BeatmapSetInfo.FilesystemStoragePath` is under the managed BMS root `chartbms/`
- Group hierarchy comes from the parent-directory segments under `chartbms/`
- The final beatmap-set directory remains the native `BeatmapSet` carousel node; do not duplicate that folder as an extra artificial grouping layer
- A set stored directly under `chartbms/` appears at the root of this grouping mode rather than under a fake "uncategorised folder" node

**`ExternalLibrary` grouping authority:**

- Applies only to BMS sets where `BeatmapSetInfo.IsExternalFilesystemStorage == true`
- The first grouping level must represent the external library root that the set belongs to
- Lower grouping levels come from the parent-directory segments relative to that external root
- The final beatmap-set directory remains the native `BeatmapSet` carousel node

**Critical persistence rule:** the selected external root for each imported/scanned external set must be persisted as stable beatmap-set data. OMS now stores this as a normalised `BeatmapSetInfo.ExternalLibraryRootPath` snapshot; `ExternalLibraryConfig` alone is not sufficient authority because users can reorder, remove, rename, or overlap roots after import.

Runtime longest-prefix matching against the current config may be used only for one-time legacy backfill or explicit fallback. It must not remain the sole long-term authority for external grouping membership.

**Fallback rule:** if an existing external set cannot be matched back to a currently registered root, it must remain visible under an explicit fallback group rather than silently disappearing or drifting to a different root.

**Regression requirements:**

- BMS-only dropdown exposure and persisted-group-mode compatibility
- Internal managed-path grouping, including root-level managed sets
- External root persistence, nested/overlapping-root resolution, and missing-root fallback
- Existing BMS grouped-entry behaviour still resets to the outermost layer on group-mode switch

---

## 11. Chart Preview (Note Distribution)

### 11.1 Overview

When a BMS chart is selected in song select, the chart detail panel displays a **note distribution graph** giving the player a visual overview of the chart's structure and composition before playing.

This is a BMS-mode-specific component rendered in the beatmap detail area (right panel in song select). It replaces or supplements osu!'s native beatmap preview panel for BMS charts.

### 11.2 Distribution Graph (`BmsNoteDistributionGraph`)

The graph uses time as the horizontal axis and renders colored blocks representing note density across the chart duration.

**Visual encoding:**

| Element | Color | Meaning |
|---|---|---|
| Normal notes | White | Standard single notes (non-scratch, non-LN) |
| Scratch notes | Red | Notes in the scratch lane (channel 11/21) |
| LN heads/bodies | Blue | Long note heads and hold bodies |

The graph is divided into fixed-width time buckets (e.g. 1 bucket per 1000ms). Each bucket's height or fill density represents the note count within that window, independently stacked per note type.

**Summary statistics displayed alongside the graph:**

```
Total notes:  1842
Scratch:       214  (11.6%)
LN:            308  (16.7%)
Max density:  24.3 notes/sec  (at 02:14)
```

When available, the same panel may also show compact chart metadata lines for:

- chart creator
- internal `#PLAYLEVEL`
- subtitle / sub-artist
- difficulty table labels

### 11.3 Implementation Notes

- `BmsNoteDistributionGraph` reads from the already-converted `IBeatmap` — no BMS re-parse needed
- Uses `BmsNoteDensityAnalyzer` (see Section 9.3) for bucket computation — do not reimplement the sliding-window logic
- Read chart metadata from persisted beatmap metadata / ruleset data; do not re-open the source `.bms` file on selection just to populate summary lines
- Computed once on chart selection, cached for the session
- Rendered as a `Drawable` using osu-framework's immediate-mode drawing primitives (no external chart library)
- Bucket computation runs on a background task; results are pushed to the drawable via `Schedule()`. The song select UI thread is never blocked — the frame budget applies only to the final `Schedule()` callback and drawable update, not to the computation itself.
- Add `BmsNoteDistribution` to the skin lookup table so the graph panel can be skinned
- The note distribution graph is preview authority only. Its scratch / LN summary counts may overlap and must not be reused directly as the source for any future mutually-exclusive Song Select filter taxonomy such as `RC / LN / SCR`.

---

## 12. BGA System

### 12.1 Current Scope

- **Static `#STAGEFILE`**: Display as background image during gameplay. Load on chart load.
- **Static `#BACKBMP`**: Fallback background if `#STAGEFILE` missing.
- BGA video and BMP sequence animation: **not implemented in Phase 1**.

### 12.2 Future Scope (BGA Video — Phase 2)

Reserve a `BmsBackgroundLayer` rendering slot in the playfield scene graph. In Phase 2:
- Parse `#BMP##` index and channel `04`/`06`/`07` BGA events
- Decode video frames via `ffmpeg.autogen` or similar
- Render decoded frames to a texture updated per frame

Do not implement video decoding in Phase 1. The slot must exist to avoid architectural rework.

---

## 13. Skin System

OMS continues to use osu!lazer's `ISkin` / `ISkinSource` / `SkinnableDrawable` architecture, but OMS's **product surface** must no longer rely on upstream built-in skins as the final end-user default.

### 13.1 Product Direction

- OMS will ship a single **OMS built-in skin package / selection entry** as the authoritative default visual layer for the product.
- That default package contains a **global layer plus separate mania and BMS ruleset layers**.
- Mania and BMS do not need to share the same gameplay asset semantics; they are integrated into one package, but remain independent ruleset skin implementations.
- `Argon`, `Triangles`, `DefaultLegacy`, `Retro`, and other osu!lazer-native built-in default skins must be removed from OMS's final shipped default selection surface once OMS replacement coverage is complete.
- During transition, code may temporarily retain upstream classes or resources, but no new OMS feature may depend on them as the intended release fallback.
- Hard-coded placeholder `Box`-based visuals are acceptable only as temporary development scaffolding. They are not an acceptable release-state fallback once the corresponding Phase 1.1 task is complete.
- Current built-in skin work has started around `SKIN/SimpleTou-Lazer` as the mania-side candidate baseline plus BMS-side contract expansion. The current BMS IIDX-coloured direct-drawn layer remains only a skin-load-failure feedback/fallback surface; it is not the intended OMS built-in skin direction or proof of release-ready default-skin coverage.

### 13.1.1 Current Phase 1.1 Execution Order

- The current built-in candidate baseline is `SKIN/SimpleTou-Lazer`. At this stage it is only the mania-side/reference-side base for OMS built-in skin work, not proof that OMS already has a release-ready integrated default skin.
- Phase 1.1 must not attempt full mania/BMS visual reproduction in parallel.
- The forced order is: package boundary/docs -> shared OMS skin shell -> BMS playfield abstraction gate -> BMS default visual layer -> mania OMS-owned migration -> partial override semantics -> upstream native default removal -> release gate.
- The reason is structural: BMS already has many drawable lookups, but `BmsLaneLayout`, `BmsPlayfield`, and `BmsHitTarget` still need a configuration-driven playfield layer before faithful reproduction of the chosen mania-side visual language is practical.

### 13.2 Fallback Hierarchy

OMS skin fallback must obey this order:

1. User-selected custom skin, if it provides the requested component.
2. Ruleset-specific OMS transformer for the active ruleset.
3. OMS built-in default component for that lookup.
4. Skin-load-failure feedback drawable / development placeholder, only while the related Phase 1.1 item remains unfinished and only to preserve basic readability when normal skin loading fails.

Additional rules:

- Missing components fall back **per component**, not by abandoning the entire OMS skin chain.
- BMS imported song folders are not treated as ad-hoc skin packages.
- Beatmap skin compatibility may remain as a compatibility layer for mania, but it is not allowed to become the only fallback path for OMS gameplay.
- BMS-specific lookups must never fall back to osu!lazer-native built-in skin assets once the OMS built-in skin exists.

### 13.3 Shared OMS Skin Architecture

OMS should converge on an integrated package architecture with the following layers:

- **OMS global layer**: package host, shared infrastructure, layout metadata, global HUD shells, common typography/icon resources, and fallback discipline.
- **OMS mania layer**: stage, column, key area, note, hold, hit target, bar line, hitburst, judgement/combo/HUD adapted to mania variants.
- **OMS BMS layer**: scratch-aware lanes, lane covers, note/hold parts, gauge/clear lamp, note distribution, BMS judgement naming, and static background presentation.

Shared means package structure, fallback infrastructure, and optional global UI language. It does **not** mean mania and BMS gameplay assets must be interchangeable or visually identical.

Recommended implementation structure:

- A shared `OmsSkinTransformer` or equivalent base used by both rulesets.
- A protected preview `OmsSkin`-style selection entry may land earlier as the built-in host / provider / resource-root slice. That counts as skeleton progress, not as completion of the shared shell or release-ready default-skin coverage.
- `ManiaRuleset.CreateSkinTransformer()` should continue migrating away from switching on osu!lazer-native built-in skin types as the final OMS product behavior. An explicit `OmsSkin` -> `ManiaOmsSkinTransformer` route can land earlier as a transitional slice, but that still does not mean mania defaults are fully OMS-owned.
- `BmsRuleset.CreateSkinTransformer()` should keep returning a ruleset-specific transformer, but that transformer must evolve from a thin override layer into the full BMS-side OMS fallback provider.

### 13.4 Shared Visual Contract

The OMS built-in skin package must provide a coherent product shell while still allowing mania and BMS gameplay layers to remain independent:

- Shared typography scale for global UI, shared HUD shells, Song Select details, and results containers.
- Shared colour-token system for package-level surfaces, separators, neutral text, focus/highlight states, and non-ruleset-specific UI.
- Shared animation-duration buckets for instant feedback, HUD transitions, and overlay entrances.
- Shared serialization rules for `ISerialisableDrawable`: fixed anchors, stable sizes, no skin-dependent layout drift that breaks replay/UI restoration.
- Shared contrast policy: gameplay-critical information must remain readable on both bright and dark user skins.

Ruleset-specific gameplay semantics remain independent:

- Mania and BMS may use completely different note, lane, judgement, and HUD art/animation systems while still living inside the same skin package.
- Shared package ownership must not force BMS to mimic mania naming or vice versa.

OMS-specific judgement naming remains authoritative:

- `HitResult.Meh` displays as `BAD`
- `HitResult.Miss` displays as `POOR`
- `HitResult.ComboBreak` displays as `EMPTY POOR`

This naming is product semantics, not a skin choice.

### 13.5 Mania Skin Contract

OMS mania must stop treating upstream built-in skins as the release-state visual contract.

The OMS mania layer must cover at least:

- Stage background / foreground
- Column background
- Key area
- Hit target
- Bar line
- Note head
- Hold note head / body / tail
- Hit explosion
- Combo counter
- Judgement display
- Main gameplay HUD placement

Requirements:

- Variant changes must not require separate product themes; 4K/7K/etc. all derive from the same OMS visual family.
- The mania layer lives inside the same default skin package as BMS, but its assets/config bridge remain mania-specific.
- Timing-based recolour or configuration-based note recolour remains a ruleset behavior, but the default asset/fallback source must be OMS-owned.
- Mania defaults must remain readable even when no external skin is installed.

### 13.6 BMS Skin Contract

BMS has stricter layout and semantics requirements than mania and therefore needs richer lookup data.

The BMS skin contract must cover at least:

- Playfield background frame
- Static background presentation (`#STAGEFILE` / `#BACKBMP` / `#BANNER` display slot)
- Lane background per lane index
- Scratch lane background and separators
- Hit target / scratch hit target
- Bar line
- Single note head
- Long-note head / body / tail
- Lane cover (top / bottom) and focus state
- Judgement display
- Combo counter
- Gauge bar
- Gauge history panel
- Clear lamp display
- Results summary panel
- Note distribution panel
- Song Select metadata accents for BMS-only information when needed

Simple enum-only lookup is insufficient for all BMS use cases. Where component rendering depends on lane metadata, use dedicated lookup types carrying:

- `LaneIndex`
- `LaneCount`
- `IsScratch`
- `Side` (`1P` / `2P`)
- `Keymode`
- Optional `CoverPosition` / `Focused` / `LongNotePart`

Drawable replacement alone is also insufficient for a release-ready BMS default path. The BMS contract must grow an OMS-owned playfield abstraction layer for layout-critical parameters, including lane width / scratch-width ratio and spacing, playfield sizing, hit target / receptor geometry, bar line emphasis rules, and pressed / focused states. The current runtime strict surface may freeze user-facing geometry overrides, but that freeze must sit on top of this abstraction rather than bypass it.

> BMS 默认层与 mania 侧组件的当前迁移进度见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)；面向皮肤制作者的详细 lookup / preset 表见 [../other/SKINNING.md](../other/SKINNING.md)。
>
> 皮肤设计边界与绿色数字 / Mod 联动专题的执行规划、当前状态与技术约束，见 [../subline/P1-A/README.md](../subline/P1-A/README.md) 与 [../subline/P1-C/README.md](../subline/P1-C/README.md)。

BMS-specific visual rules:

- Scratch lane must remain visually distinct from normal keys even inside a shared OMS theme.
- The BMS layer lives inside the same default skin package as mania, but must keep its own lookup names, layout metadata, and gameplay semantics.
- `5K` / `7K` / `9K_Bms` / `9K_Pms` / `14K` must reuse one theme family but allow layout-sensitive per-lane rendering.
- `1P/2P` side flips must not require a second asset family; side-sensitive elements respond to runtime bindables/lookup metadata.
- Lane cover `Sudden / Hidden` states and lane-cover focus state are first-class skinnable elements, not debug overlays.
- Persistent gameplay feedback, including speed metrics and later `FAST/SLOW` / judge display / pacemaker data, must remain BMS-owned skinnable components instead of ad-hoc overlays hidden inside unrelated HUD elements.
- If BMS HUD composition needs more children than the current wrapped HUD + gauge bar + combo counter contract, extend it via a versioned optional interface or wrapper contract; do not break `IBmsHudLayoutDisplay` in place.
- Do not inject gameplay feedback widgets by crawling arbitrary wrapped HUD children or by overloading `GaugeBar` / `ComboCounter` with unrelated semantics.
- Results summary and clear lamp must remain separable skinnable components, so a skin can override the whole summary panel or only the lamp badge.
- `BmsBackgroundLayer` should present static art through the skin system in Phase 1.1; video BGA remains a later phase and must not block the static background contract.

### 13.7 Native Default Skin Removal Policy

The following must be removed from OMS's final default product surface during Phase 1.1:

- Upstream built-in skin selector entries used as OMS defaults
- Runtime fallback paths that silently land on osu!lazer-native built-in skins instead of OMS defaults
- Packaging assumptions that public OMS builds ship upstream default skin resources as the intended out-of-box experience

Permitted during transition:

- Minimal fake skins in tests
- Temporary internal compatibility shims while mania and BMS migrate
- Non-user-facing development references needed to keep the repo compiling during migration

Not permitted after Phase 1.1 completion:

- Public OMS builds whose no-custom-skin experience is still Argon/Triangles/Legacy/Retro-derived
- BMS gameplay visuals that disappear or become blank when upstream default skin resources are removed

### 13.8 Implementation Rules

- All shipping mania/BMS gameplay elements must be backed by skin lookup + OMS fallback, not only by direct code-drawn primitives.
- New OMS gameplay UI should prefer `SkinnableDrawable` / `GlobalSkinnableContainerLookup` / dedicated lookup types over ruleset-local hard-coded layout.
- Do not attempt final BMS visual parity by baking geometry into temporary hard-coded defaults before the BMS playfield abstraction gate exists; once that gate exists, the current strict runtime surface may intentionally freeze user-facing geometry overrides.
- Component defaults must be stable under serialization, replay, and skin reload.
- Layout-critical components such as lane covers, gauge bars, clear lamps, and note distribution panels must have predictable default sizes in the OMS built-in skin.
- Do not make BMS gameplay visuals depend on upstream mania component names or upstream built-in texture contracts.

### 13.9 Test Requirements

The skin system must ship with both non-visual and visual validation:

- Non-visual transformer tests for fallback order and component lookup routing.
- Ruleset tests proving mania and BMS no longer require upstream native default skins for no-custom-skin operation.
- Visual tests for lane layout, note readability, lane cover focus, judgement placement, gauge bar visibility, clear lamp rendering, and Song Select note distribution.
- Packaging checks confirming public builds do not expose upstream built-in skins as OMS defaults.

### 13.10 Phase Placement

- Core OMS skin-system replacement is a **Phase 1.1** priority, not a deferred Phase 2 polish item.
- Phase 2 may extend the skin ecosystem further, but Phase 1.1 must already deliver a complete OMS-owned default path for mania and BMS.

---

## 14. Phase 3 Private Server Integration (planned; no current `oms.Server` project)

Current repository status: the workspace does not contain an `oms.Server` project. This section documents the Phase 3 target contract only.

### 14.1 API Client

`OmsApiClient` wraps all Phase 3 server communication. Before Phase 3, OMS should not ship a default official server base URL or expose account / leaderboard / beatmap-download flows to end users.

Base URL becomes configurable once private server integration is intentionally enabled; until then the client should treat it as unset / disabled.

Authentication: Bearer token stored in OS credential store. Refresh token flow.

### 14.2 Endpoints (Interface Contract)

These are the Phase 3 API endpoints OMS expects. Backend implementation is external, and current local-only releases must not call them by default.

```
POST   /auth/login              → { token, refresh_token, user }
POST   /auth/refresh            → { token }
GET    /user/me                 → OmsUser

POST   /scores/submit           → Submit BMS or mania score
GET    /scores/chart/{hash}     → Top scores for a chart (leaderboard)
GET    /scores/user/{id}        → User's score history

GET    /beatmaps/search?q=&page=  → Beatmap search results
GET    /beatmaps/{id}/download    → Download beatmap archive
GET    /difficulty-tables         → List server-hosted difficulty table mirrors
GET    /difficulty-tables/{id}    → Table entries with chart hashes
```

Chart identity is keyed by **MD5 hash of the `.bms` file** (standard in BMS ecosystem).

**`OmsScore` submission payload (key fields):**

```csharp
public class OmsScore
{
    public string   ChartMd5       { get; set; }  // lowercase hex MD5 of .bms file
    public string   Ruleset        { get; set; }  // "bms" or "mania"
    public string   Keymode        { get; set; }  // "5k", "7k", "9k_bms", "9k_pms", "14k"
    public int      ExScore        { get; set; }  // BMS only
    public int      MaxExScore     { get; set; }  // BMS only
    public int      PgreatCount    { get; set; }
    public int      GreatCount     { get; set; }
    public int      GoodCount      { get; set; }
    public int      BadCount       { get; set; }
    public int      PoorCount      { get; set; }
    public int      EmptyPoorCount { get; set; }  // ghost note penalties (§5.2)
    public int      MaxCombo       { get; set; }
    public string   ClearLamp      { get; set; }  // e.g. "HARD_CLEAR", "FULL_COMBO"
    public string   GaugeMode      { get; set; }  // "NORMAL","HARD","EX_HARD","HAZARD","ASSIST_EASY","EASY","GAS"
    public string   JudgeMode      { get; set; }  // "OD", "BEATORAJA", "LR2"
    public string   LongNoteMode   { get; set; }  // "LN", "CN", "HCN"
    public bool     ModAutoScratch { get; set; }
    public bool     ModMirror      { get; set; }
    public string   ClientVersion  { get; set; }
    public DateTime PlayedAt       { get; set; }
}
```

### 14.3 Offline Mode

If the server is unreachable, OMS runs fully offline:
- Local scores saved to SQLite via EF Core (same as osu!lazer)
- No leaderboard data shown (replaced by "Offline" indicator)
- Beatmap download unavailable
- Difficulty table data uses last cached fetch
- All local gameplay fully functional

---

## 15. Development Phases

> 详细分步规划、验收标准与当前进度矩阵见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) 与 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)。
> 本节仅列出各阶段范围定义。

### Phase 1 — Core BMS

上游裁剪、BMS 解析 / 导入 / 键音 / 7K+1 playfield / 判定 / gauge / EX-SCORE / 结算 / 密度星级 / 难度表 / MD5 匹配 / Song Select 表分组 / 音符分布图 / 静态 BG / Lane Cover / 输入绑定。

### Phase 1.1 — OMS Skin System

集成默认皮肤包（Global + Mania + BMS 三层独立 ruleset 皮肤）、组件 lookup / fallback / OMS 默认层迁移、partial override 语义、上游原生默认皮肤退出、release gate。

### Phase 2 — BMS Feature Complete

beatoraja + LR2 判定 Mod、全 gauge Mod (ASSIST EASY ~ HAZARD) + GAS、A-SCR、LN/CN/HCN 运行时模式、5K/9K/14K DP 布局、1P/2P flip、Empty Poor、analog axis 输入、LNTYPE 2、BGA 视频、用户皮肤生态。

### Phase 3 — Private Server

账号认证、成绩提交与排行榜、谱面搜索/下载、远程难度表镜像。

### Future Scope

`#RANDOM` 分支、LR2IR 排行兼容、SHA256 + sabun linkage、macOS / Linux（不计划，不阻断）。

---

## 16. Coding Conventions

Follow osu!lazer's existing conventions throughout:

- All new classes in `osu.Game.Rulesets.Bms` namespace mirror the structure of `osu.Game.Rulesets.Mania`
- Use `Bindable<T>` for all configurable values
- Use `DependencyContainer` / `[Resolved]` attribute for dependency injection — no static singletons
- Async I/O for all file and network operations (`async`/`await`, never `.Result`)
- `IResourceStore<byte[]>` for asset loading
- All timing values in **milliseconds (double)** unless explicitly noted as beats
- Write XML doc comments on all public API surface in future private-server integration code and `oms.Input`
- Unit test coverage required for: `BmsBeatmapDecoder`, `BmsTimingWindows`, `BmsScoreProcessor`, `BmsGaugeProcessor`, `BmsDifficultyCalculator`, `BmsNoteDensityAnalyzer`, `BmsTableMd5Index`, `BmsDifficultyTableManager`
- Any development, research, bug fix, validation pass, or release-gate adjustment that changes plan, status, constraints, or verified conclusions must update the corresponding `doc_md/mainline`, `doc_md/subline`, `doc_md/other`, or `doc_md/mini` documents in the same change. Do not leave code and documentation drifting.

---

## 17. Key Dependencies

| Package | Purpose |
|---|---|
| `ppy.osu-framework` | Core game loop, rendering, input base |
| `ManagedBass` | Audio engine (keysound playback) |
| `SharpCompress` | BMS archive extraction (zip/rar/7z) |
| `HidSharp` | Non-Windows / diagnostic HID enumeration and reading |
| `Vortice.DirectInput` | Windows default HID/gamepad enumeration and polling |
| `Microsoft.EntityFrameworkCore.Sqlite` | Local score storage + table cache |
| `Ude.NetStandard` | Charset detection (Shift-JIS / UTF-8) |

---

## 18. Upstream Sync Policy

The upstream osu!lazer commit this fork is based on is recorded in `UPSTREAM.md` at repo root.

- Do **not** blindly pull upstream changes
- When a critical bug fix or performance improvement lands upstream, cherry-pick selectively
- Before cherry-picking, verify the change does not conflict with BMS ruleset additions or removed rulesets
- Re-evaluate upstream sync every 3 months

---

*This document is the authoritative context for GitHub Copilot and all AI-assisted development on OMS. Keep it updated as architectural decisions change.*
