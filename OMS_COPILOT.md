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

---

## 2. Repository Structure

```
oms/
├── osu.Game/                        # Core game framework (upstream, minimal modification)
├── osu.Game.Rulesets.Mania/         # Retained mania ruleset (upstream, minimal modification)
├── osu.Game.Rulesets.Bms/           # NEW — BMS ruleset (primary development target)
│   ├── Beatmaps/
│   │   ├── BmsBeatmapDecoder.cs         # BMS file parser (.bms/.bme/.bml/.pms)
│   │   ├── BmsBeatmapConverter.cs       # Converts parsed BMS → IBeatmap
│   │   ├── BmsBeatmapInfo.cs            # BMS-specific beatmap metadata (Keymode, MeasureLengthControlPoints, etc.)
│   │   ├── BmsArchiveReader.cs          # Handles .zip/.rar/.7z BMS package import
│   │   └── BmsKeysoundStore.cs          # Keysound sample index and channel manager
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
│   │   └── BmsNoteDistributionGraph.cs  # Note distribution preview panel
│   ├── Layout/
│   │   ├── BmsPlayfield.cs              # Playfield rendering
│   │   ├── BmsLaneLayout.cs             # Lane config for 5K/7K/9K/14K, 1P/2P
│   │   ├── BmsLaneCover.cs              # Top/bottom lane cover (Mod-controlled)
│   │   └── BmsScratchLane.cs            # Scratch lane rendering and input handling
│   ├── Mods/
│   │   ├── BmsModJudgeBeatoraja.cs      # Switches to beatoraja timing windows
│   │   ├── BmsModJudgeLr2.cs            # Switches to LR2 timing windows
│   │   ├── BmsModGaugeAssistEasy.cs     # Assist Easy gauge
│   │   ├── BmsModGaugeEasy.cs           # Easy gauge
│   │   ├── BmsModGaugeHard.cs           # Hard gauge
│   │   ├── BmsModGaugeExHard.cs         # EX-Hard gauge
│   │   ├── BmsModGaugeHazard.cs         # Hazard gauge
│   │   ├── BmsModGaugeAutoShift.cs      # GAS — Gauge Auto Shift
│   │   ├── BmsModLaneCoverTop.cs        # Top cover (Sudden)
│   │   ├── BmsModLaneCoverBottom.cs     # Bottom cover (Hidden)
│   │   ├── BmsModMirror1P2P.cs          # 1P/2P side flip
│   │   ├── BmsModAutoScratch.cs         # A-SCR — Auto Scratch
│   │   └── BmsModRandom.cs              # Note randomization (Future Scope)
│   ├── Input/
│   │   └── BmsInputManager.cs           # BMS-specific input routing
│   ├── Background/
│   │   └── BmsBackgroundLayer.cs        # Static BG + future BGA hook
│   ├── Configuration/
│   │   └── BmsRulesetConfigManager.cs   # Persistent BMS mode settings (e.g. AutoScratchNoteVisibility)
│   ├── Resources/
│   │   └── bms_table_presets.json       # Bundled preset difficulty table URLs (not hardcoded)
│   ├── BmsMod.cs                        # Abstract base class for all BMS mods (extends Mod)
│   └── BmsRuleset.cs                    # Ruleset entry point
├── oms.Input/                        # NEW — Unified Input Abstraction Layer
│   ├── OmsInputRouter.cs                # Routes all signal types to game actions
│   ├── OmsBindingStore.cs               # Persists all bindings
│   └── Devices/
│       ├── RawKeyboardHandler.cs        # Windows Raw Input keyboard
│       ├── HidDeviceHandler.cs          # HID/DirectInput (controllers, IIDX hardware)
│       ├── XInputHandler.cs             # XInput (Xbox-compatible controllers)
│       └── MouseAxisHandler.cs          # Mouse movement delta → scratch axis
├── oms.Server/                       # NEW — Private server API client
│   ├── OmsApiClient.cs
│   ├── Endpoints/
│   │   ├── AuthEndpoint.cs
│   │   ├── LeaderboardEndpoint.cs
│   │   └── BeatmapDownloadEndpoint.cs
│   └── Models/
│       ├── OmsScore.cs
│       └── OmsUser.cs
└── oms.Desktop/                      # Windows entry point
    └── Program.cs
```

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
#TITLE, #SUBTITLE, #ARTIST, #GENRE, #BPM, #PLAYLEVEL, #DIFFICULTY, #RANK, #TOTAL
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
6. Move extracted folder to OMS songs directory, clean up temp

Do **not** convert to `.osz`. OMS reads BMS files directly from disk at runtime.

**Parse failure handling:** If `BmsBeatmapDecoder` throws or produces a critically incomplete result (no playable notes after channel parse, unrecognisable encoding after detection attempt), `BmsArchiveReader` must:
1. Log the error with the file path and exception message.
2. Skip the failed file — do not add it to the BeatmapSet.
3. If all `.bms`/`.bme`/`.bml`/`.pms` files in the archive fail, abort the import entirely and surface an error notification to the user: "Import failed: no valid BMS files found in archive."
4. If at least one file succeeds, complete the import and surface a warning notification that lists the skipped filenames.

### 4.3 Keysound System (`BmsKeysoundStore`)

BMS is fully keysounded — every note triggers a specific audio sample.

Requirements:
- Index up to 1295 (`ZZ` in base-36) keysound slots per chart
- **Supported audio formats:** `.wav` (primary — required), `.ogg` and `.mp3` (secondary — support if ManagedBass can load without additional plugins). Format resolution: attempt the exact filename referenced by `#WAV##` first; if not found, retry with extension substituted in order (`.wav` → `.ogg` → `.mp3`). Log a warning on any substitution. Do not silently succeed without logging.
- Load samples lazily (on first play), cache in memory during session
- Support concurrent playback of many samples simultaneously — channel ceiling is read from `BmsRulesetConfigManager.KeysoundConcurrentChannels` (default: 256). Benchmark ManagedBass limits during development and adjust the default accordingly. Expose the setting in the BMS mode settings screen so users with lower-spec hardware can reduce it.
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

### 4.5 Beatmap Conversion (`BmsBeatmapConverter`)

`BmsBeatmapConverter` consumes the raw decoded BMS data from `BmsBeatmapDecoder` and produces a fully populated `IBeatmap` that the osu-framework game loop can execute. It is the single boundary between the BMS-specific parse world and the osu! runtime world.

**HitObject mapping:**

| BMS concept | osu! HitObject type | Notes |
|---|---|---|
| Single note (channels `1x`/`2x`) | `BmsHitObject` (extends `HitObject`) | Carries `LaneIndex`, `KeysoundId`, `IsScratch`, `AutoPlay` fields |
| LN head (LNOBJ or LNTYPE `5x`/`6x` start) | `BmsHoldNote` (extends `HitObject`) | `StartTime` = head timing; `Duration` = tail time − head time; carries `HeadKeysoundId` and `TailKeysoundId` |
| LN tail | embedded in `BmsHoldNote.Duration` | Not a separate HitObject in the game object list; tail time = `StartTime + Duration` |
| BGM note (channel `01`) | `BmsBgmEvent` (non-hittable) | Queued to `BmsKeysoundStore`; excluded from judgment and scoring |

`BmsHitObject` and `BmsHoldNote` must carry all keysound and lane metadata needed at runtime. They must not require a back-reference to the raw decoder output after conversion.

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

All timing changes must be integrated into the `ControlPointInfo` of the converted `IBeatmap`. The scroll speed system (inherited from mania) will use these timing points for note rendering.

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

| Grade | Window (ms) — EASY base |
|---|---|
| PGREAT | ±20 |
| GREAT | ±40 |
| GOOD | ±100 |
| BAD | ±200 |
| POOR | beyond BAD |

NORMAL = ×0.75, HARD = ×0.5, VERY HARD = ×0.25 of EASY windows.  
Active judgerank tier determined by `#RANK` field even within beatoraja system.

**`Lr2JudgementSystem`** (Mod: `BmsModJudgeLr2`):

| Grade | Window (ms) |
|---|---|
| PGREAT | ±18 |
| GREAT | ±40 |
| GOOD | ±90 |
| BAD | ±200 |
| POOR | beyond BAD |

LR2 does not vary windows by rank tier — fixed values always.

### 5.2 Poor Judgment (Empty Poor)

**Empty Poor** = pressing a key when no hittable note is within the BAD window. Penalizes gauge.

Implementation:
- Maintain a per-lane "active note window" state
- On key press: if no note is within BAD timing range → fire `BmsPoorJudgement`
- `BmsPoorJudgement` triggers gauge damage equivalent to a BAD hit
- Empty Poor does **not** affect EX-SCORE
- Empty Poor **breaks Combo** (resets the current combo counter to 0)
- Empty Poor is only active in BMS mode, not osu!mania mode

### 5.3 Combo Rules

| Judgment | Combo effect |
|---|---|
| PGREAT | +1 (continues combo) |
| GREAT | +1 (continues combo) |
| GOOD | +1 (continues combo) |
| BAD | reset to 0 (breaks combo) |
| POOR (miss) | reset to 0 (breaks combo) |
| Empty POOR | reset to 0 (breaks combo) |

Note: GOOD **does not break combo**, but having any GOOD in a run **disqualifies FULL COMBO** (§6.3). FULL COMBO requires PGREAT + GREAT only; PERFECT requires PGREAT only.

### 5.4 Keysound Trigger by Judgment

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

`hittable_note_count` = all playable notes including LN heads. **When A-SCR Mod is active, scratch lane notes are excluded from `hittable_note_count`** — those notes are auto-triggered and outside the scoring pool.

`BmsScoreProcessor` must track: PGREAT, GREAT, GOOD, BAD, POOR, EMPTY POOR, MAX COMBO.

### 6.2 Gauge System (`BmsGaugeProcessor`)

Six gauge types, selectable via Mod. Default = NORMAL.

**Gauge Rate Derivation (`#TOTAL`)**

All recovery and damage rates are derived from the chart's `#TOTAL` header value and total hittable note count. Do not use fixed constants.

```
base_rate (% per note) = #TOTAL ÷ (total_hittable_notes × 100)
```

`total_hittable_notes` = all playable notes including LN heads; excludes LN tails and BGM channel (`01`) notes. If `#TOTAL` is absent from the chart header, default to `200`. **When A-SCR Mod is active, scratch lane notes are also excluded from `total_hittable_notes`** — consistent with their exclusion from `hittable_note_count` in §6.1. This ensures gauge recovery and damage rates scale correctly to the reduced note pool.

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

**Ranking:** All gauge types participate in the leaderboard. Scores are tagged with their gauge type and the leaderboard UI supports filtering by specific gauge Mod combinations.

**Leaderboard filter architecture:** The chart leaderboard (`GET /scores/chart/{hash}`) accepts a composite filter on the server side. The client passes filter parameters as query string fields:

```
GET /scores/chart/{hash}?gauge=HARD&ascr=false&judge=OD
```

All filter fields are optional and independently combinable (AND logic):
- `gauge` — one of `ASSIST_EASY`, `EASY`, `NORMAL`, `HARD`, `EX_HARD`, `HAZARD`, `GAS`, or omit to show all
- `ascr` — `true` / `false` / omit to show all (filters by `ModAutoScratch`)
- `judge` — `OD` / `BEATORAJA` / `LR2` / omit to show all

The leaderboard UI exposes these as independent dropdowns/toggles. Filter state persists per-chart across sessions in `BmsRulesetConfigManager` (add `LeaderboardGaugeFilter`, `LeaderboardAscrFilter`, `LeaderboardJudgeFilter` to the settings inventory above).

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

Only upgrade lamp, never downgrade.

**A-SCR and lamp eligibility:** Scratch lane notes are excluded from scoring entirely when A-SCR is active. MaxExScore is calculated from non-scratch notes only, and FULL COMBO / PERFECT evaluation is based solely on non-scratch note results. A-SCR does not disqualify any lamp — it only affects the leaderboard tag.

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

### 6.5 Auto Scratch — A-SCR (`BmsModAutoScratch`)

A-SCR is an assist Mod that removes all scratch lane notes from player input and handles them automatically. All scratch notes are auto-judged as PGREAT at the correct timing with their keysounds triggered normally.

**Behavior:**

- On chart load with A-SCR active, all scratch lane notes (channel `11` for 1P, `21` for 2P) are flagged as `AutoPlay = true` and **excluded from scoring**
- During gameplay, flagged notes are triggered automatically at their exact timing — no player input required or accepted on the scratch lane
- Auto-triggered scratch notes **do not contribute to EX-SCORE, MaxExScore, Gauge, or Combo** — they are treated as BGM-equivalent events for audio purposes only
- Keysounds for auto-triggered scratch notes play normally at their correct timing
- The scratch lane input binding remains active but has no effect — pressing the scratch during an auto note does not double-trigger or produce an Empty Poor
- In 14K DP mode, A-SCR applies to both channel `11` (1P scratch) and channel `21` (2P scratch) simultaneously

**Interaction with other Mods:**

- Compatible with all gauge Mods including GAS
- Compatible with 1P/2P flip — A-SCR applies to whichever lane is the scratch lane after flip
- Compatible with all judgment Mods
- Incompatible with full AUTOPLAY mode — A-SCR is scratch-only assist

**Scratch note visibility (game mode setting, not a Mod):**  
When A-SCR is active, whether scratch notes are rendered on the playfield is controlled by a persistent setting in the BMS game mode configuration, not by the Mod itself.

```csharp
// In BmsRulesetConfigManager
public enum AscScratchVisibility
{
    Visible,   // Scratch notes render normally (default)
    Hidden,    // Scratch notes are invisible; keysounds still play at correct timing
}

public Bindable<AscScratchVisibility> AutoScratchNoteVisibility { get; }
    = new(AscScratchVisibility.Visible);
```

- This setting is accessible from the BMS mode settings screen, independent of which Mods are currently selected
- When set to `Hidden`, scratch lane notes are not drawn but the auto-judge and keysound playback are unaffected
- When A-SCR is not active, this setting has no effect — scratch notes always render normally
- The setting persists across sessions via `BmsRulesetConfigManager`

**`BmsRulesetConfigManager` — full persistent settings inventory:**

All BMS mode persistent settings live here. The list below is the authoritative registry; add new entries here when introducing new persistent state.

```csharp
// In BmsRulesetConfigManager
AutoScratchNoteVisibility  : AscScratchVisibility  = Visible
KeysoundConcurrentChannels : int                   = 256      // ManagedBass channel ceiling; benchmarked limit
LeaderboardGaugeFilter     : string?               = null     // null = show all; e.g. "HARD", "GAS" (§6.2)
LeaderboardAscrFilter      : bool?                 = null     // null = show all; true = A-SCR only; false = manual only
LeaderboardJudgeFilter     : string?               = null     // null = show all; e.g. "OD", "BEATORAJA", "LR2"
```

When adding a new persistent BMS setting, define its type and default here before wiring it into the feature that uses it. Do not scatter persistent state across unrelated classes.

**Ranking:**  
A-SCR scores participate in the leaderboard and are tagged with `mod_ascr = true`. The leaderboard UI supports filtering to show or hide A-SCR scores separately from full manual runs, allowing players to compare both with and without scratch assist.

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

---

## 7. Layout System

### 7.1 Lane Configuration (`BmsLaneLayout`)

BMS mode does not use osu!mania's `ManiaStage` directly. Define a `BmsLaneLayout` that specifies:

- Number of lanes
- Lane widths (scratch lane is wider than key lanes)
- Lane colors (alternating key colors per BMS convention)
- Scratch lane position (leftmost for 1P, rightmost for 2P)

**1P/2P flip (`BmsModMirror1P2P`):**
- Mirrors the entire lane array horizontally
- Updates all key bindings to their mirrored counterparts
- Scratch moves from left to right (or vice versa)
- Skin elements that are side-dependent must respond to a `CurrentSide` bindable (1P/2P)

### 7.2 Lane Cover (`BmsLaneCover`)

Top cover (Sudden) and bottom cover (Hidden) are rendered as opaque overlay panels on the playfield.

Controlled by Mods:
- `BmsModLaneCoverTop` — enables top cover, exposes a `CoverPercent` setting (0–100%)
- `BmsModLaneCoverBottom` — enables bottom cover, exposes a `CoverPercent` setting (0–100%)

Both Mods can be active simultaneously (Sudden+Hidden). Cover percent is adjustable in-game via scroll wheel or assigned key without pausing (matching LR2 behavior). When both covers are active, the scroll wheel adjusts the Top cover (Sudden) by default. Holding the `UI_LaneCoverFocus` action (add to `OmsAction` enum; configurable in bindings) redirects scroll-wheel input to the Bottom cover (Hidden) for the duration it is held. No persistent focus state — releasing the modifier immediately returns scroll control to the Top cover.

### 7.3 Scroll Speed

Inherit osu!mania's scroll speed system (user-configured multiplier). No separate HiSpeed value. BPM changes in the chart affect note spacing natively via timing points.

---

## 8. Input Abstraction Layer (`oms.Input`)

### 8.1 Design Goal

All input hardware — keyboard, IIDX controller, arcade controller, gamepad — must map to the same abstract `OmsAction` enum. The game layer never reads hardware signals directly.

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
    UI_LaneCoverFocus,  // Hold to redirect scroll-wheel from Top cover to Bottom cover (§7.2)
    // ... extend as needed
}
```

> **5K mapping:** 5K mode reuses a subset of the 7K+1 actions: `Key1P_Scratch`, `Key1P_1`–`Key1P_5`. Actions `Key1P_6` and `Key1P_7` are unused in 5K. The binding profile for 5K only exposes the relevant 6 actions.

### 8.2 Signal Handlers

**`RawKeyboardHandler`:**  
Uses Windows Raw Input API (`RegisterRawInputDevices`). Captures key press/release with minimal latency. Each key mapped to an `OmsAction` via `OmsBindingStore`.

**`HidDeviceHandler`:**  
Enumerates HID devices via `HidSharp` or `SharpDX.DirectInput`. Reads button states and axis values. Maps HID button indices and axis ranges to `OmsAction`.  
This is the primary path for IIDX-style controllers (including custom DIY controllers).

**`XInputHandler`:**  
For Xbox-compatible controllers. Maps buttons and triggers to `OmsAction`.

**`MouseAxisHandler`:**  
Reads raw mouse delta (X or Y axis) and converts to scratch input. Define a threshold and direction: positive delta = CW, negative delta = CCW. Expose sensitivity setting.

**Axis inversion:** All analog axis handlers (`HidDeviceHandler` for rotary encoders, `MouseAxisHandler`) must expose an `AxisInverted` boolean flag per binding in `OmsBindingStore`. DIY controller encoder wiring varies in polarity; inverting the axis direction in software avoids hardware rewiring. When `AxisInverted = true`, the CW/CCW mapping is swapped before delivering the signal to `OmsInputRouter`. The flag is stored per-binding entry in `OmsBindingStore` — each bound action carries its own independent inversion state. This applies equally to HID rotary encoders and mouse axis bindings; it is not a global mouse preference.

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

`BeatmapInfo.StarRating` is always populated with the density star. Table match data is stored as a serialized list of `BmsDifficultyTableEntry` records on the beatmap metadata (not a single string field — a chart may appear in multiple tables). The display layer reads from `BmsTableMd5Index` at runtime to get the full entry list for the selected chart.

---

## 10. Difficulty Table System

### 10.1 Overview

BMS difficulty tables are community-maintained external resources mapping chart MD5 hashes to level labels. They are **independent of the OMS private server**. OMS supports both built-in preset tables and player-added custom tables via URL — both use the same subscription mechanism.

Chart identity uses the **MD5 hash of the raw `.bms`/`.bme`/`.bml`/`.pms` file** (lowercase hex). SHA256 and `parent_hash` (sabun/derivative chart linkage) are Future Scope.

### 10.2 Preset Tables

OMS ships with a built-in list of well-known community tables. These appear in the subscription settings UI pre-populated and can be enabled with a single toggle — no manual URL entry required.

**Default preset list:**

| Table | Symbol | URL (reference) |
|---|---|---|
| Satellite | ★ | `https://www.ribbit.xyz/bms/tables/satellite.html` |
| Stella | ★ | `https://www.ribbit.xyz/bms/tables/stella.html` |
| Normal1 (通常1) | ☆ | community URL |
| Insane1 (発狂1) | ★ | community URL |
| Normal2 (通常2) | ☆ | community URL |
| Insane2 (発狂2) | ★ | community URL |
| LN table | ln★ | community URL |

Preset URLs are stored in a bundled `bms_table_presets.json` resource file, not hardcoded. This allows URL updates without a client release.

Players may also add any arbitrary URL as a custom table. Custom tables are stored alongside presets in the same SQLite subscription list, differentiated by an `is_preset` flag.

### 10.3 Table Subscription (`BmsDifficultyTableManager`)

Supported fetch formats:

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

`BmsDifficultyTableManager` must implement this two-step resolution chain. If the subscription URL points directly to a JSON file (no HTML wrapper), attempt to parse it as either a header JSON or a body array.

**Text/HTML format (legacy, LR2IR-style):** parse on best-effort basis. These typically embed chart lists in `<tr>` rows with MD5 and level fields.

`BmsDifficultyTableManager` responsibilities:
- Persist subscription list (URL, display name, is_preset, enabled, last fetched timestamp) in SQLite
- Fetch and cache table data on first enable and on manual/scheduled refresh
- Expose `RefreshAllTables()` and `RefreshTable(id)` async methods
- Emit `TableDataChanged` event so `BmsTableMd5Index` rebuilds automatically
- Disabled tables are excluded from index rebuild and song select grouping

### 10.4 MD5 Matching Pipeline (`BmsTableMd5Index`)

**On beatmap import:**
1. After `BmsArchiveReader` extracts the archive, compute MD5 of each `.bms` file
2. Store hash in `BeatmapInfo.Hash` (reuse osu!'s existing hash field)
3. Query `BmsTableMd5Index` immediately — if a match exists, write `BmsTableLevel` to the beatmap metadata and persist

**On table refresh:**
1. Fetch new table JSON, parse into `List<BmsDifficultyTableEntry>`
2. Collect all MD5 values from the fetched entries
3. Batch query local SQLite: `SELECT * FROM beatmaps WHERE hash IN (...)`
4. For each matched beatmap, update its `BmsTableLevel` entries in DB
5. Rebuild in-memory index from full DB state
6. Emit `TableDataChanged` to trigger song select group refresh

**In-memory index structure:**
```csharp
// Key: MD5 hex string (lowercase)
// Value: all matched entries across all enabled tables
Dictionary<string, List<BmsDifficultyTableEntry>> md5ToEntries;
```

Index is rebuilt at startup (from cached DB data — no network required) and after any table refresh.

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
  ordered by subscription list order (user-reorderable in settings)

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

### 11.3 Implementation Notes

- `BmsNoteDistributionGraph` reads from the already-converted `IBeatmap` — no BMS re-parse needed
- Uses `BmsNoteDensityAnalyzer` (see Section 9.3) for bucket computation — do not reimplement the sliding-window logic
- Computed once on chart selection, cached for the session
- Rendered as a `Drawable` using osu-framework's immediate-mode drawing primitives (no external chart library)
- Bucket computation runs on a background task; results are pushed to the drawable via `Schedule()`. The song select UI thread is never blocked — the frame budget applies only to the final `Schedule()` callback and drawable update, not to the computation itself.
- Add `BmsNoteDistribution` to the skin lookup table so the graph panel can be skinned

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

OMS uses osu!lazer's `ISkinSource` / `Skinnable` component system without modification.

BMS mode skin elements that need new `SkinComponentLookup` keys:
- `BmsPlayfield` background
- `BmsLane` (per lane index, per active side 1P/2P)
- `BmsScratchLane`
- `BmsLaneCover` (top and bottom variants)
- `BmsJudgementText` (PGREAT / GREAT / GOOD / BAD / POOR)
- `BmsComboCounter`
- `BmsGaugeBar`
- `BmsClearLamp`
- `BmsNoteDistribution` (chart preview panel)

All BMS skin lookups must fall back gracefully to a built-in default skin if no BMS-specific skin is active.

---

## 14. Private Server Integration (`oms.Server`)

### 14.1 API Client

`OmsApiClient` wraps all server communication. Base URL is configurable in settings (default: official OMS server, overridable for private instances).

Authentication: Bearer token stored in OS credential store. Refresh token flow.

### 14.2 Endpoints (Interface Contract)

These are the API endpoints OMS expects. Backend implementation is external.

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

### Phase 1 — Core BMS (Current Focus)

- [ ] Delete standard/taiko/catch rulesets
- [ ] BMS file parser (header + all play channels)
- [ ] BMS archive import (zip/rar/7z)
- [ ] Keysound store + concurrent playback
- [ ] 7K+1 playfield (1P layout)
- [ ] OD judgment system (default)
- [ ] NORMAL gauge
- [ ] EX-SCORE processor
- [ ] Clear lamp + DJ Level on result screen
- [ ] Density star calculator (7K baseline)
- [ ] Difficulty table preset list + subscription UI (enable/disable per table)
- [ ] MD5 matching pipeline (import-time + refresh-time batch query)
- [ ] BmsTableGroupMode in song select
- [ ] Note distribution graph in chart detail panel
- [ ] Basic input binding (keyboard + HID)
- [ ] Static BG (`#STAGEFILE`)
- [ ] Lane cover Mods (top + bottom)

### Phase 2 — BMS Feature Complete

- [ ] beatoraja + LR2 judgment Mods
- [ ] ASSIST EASY / EASY gauge Mods
- [ ] HARD / EX-HARD / HAZARD gauge Mods
- [ ] GAS (Gauge Auto Shift) Mod with configurable start/floor gauge
- [ ] A-SCR (Auto Scratch) Mod
- [ ] 5K / 9K / 14K DP layouts + density star calibration per keymode
- [ ] 1P/2P flip Mod
- [ ] Empty Poor judgment
- [ ] Analog axis input (HID rotary, mouse delta)
- [ ] LNTYPE 2 (MGQ format) long note support
- [ ] BGA video playback
- [ ] Full skin lookup coverage

### Phase 3 — Private Server

- [ ] Account auth + session management
- [ ] Score submission (with Mod combination tagging; all Mods participate in ranking)
- [ ] Score integrity / anti-tamper design (client-side replay hash, server-side validation — detailed design TBD)
- [ ] Online leaderboard with Mod filter
- [ ] Beatmap search + download
- [ ] Server-hosted difficulty table mirrors

### Future Scope

- `#RANDOM` chart branching
- LR2IR leaderboard compatibility read
- SHA256 + `parent_hash` (sabun linkage) in difficulty table matching
- macOS / Linux (not planned, not blocked)

---

## 16. Coding Conventions

Follow osu!lazer's existing conventions throughout:

- All new classes in `osu.Game.Rulesets.Bms` namespace mirror the structure of `osu.Game.Rulesets.Mania`
- Use `Bindable<T>` for all configurable values
- Use `DependencyContainer` / `[Resolved]` attribute for dependency injection — no static singletons
- Async I/O for all file and network operations (`async`/`await`, never `.Result`)
- `IResourceStore<byte[]>` for asset loading
- All timing values in **milliseconds (double)** unless explicitly noted as beats
- Write XML doc comments on all public API surface in `oms.Server` and `oms.Input`
- Unit test coverage required for: `BmsBeatmapDecoder`, `BmsTimingWindows`, `BmsScoreProcessor`, `BmsGaugeProcessor`, `BmsDifficultyCalculator`, `BmsNoteDensityAnalyzer`, `BmsTableMd5Index`, `BmsDifficultyTableManager`

---

## 17. Key Dependencies

| Package | Purpose |
|---|---|
| `ppy.osu-framework` | Core game loop, rendering, input base |
| `ManagedBass` | Audio engine (keysound playback) |
| `SharpCompress` | BMS archive extraction (zip/rar/7z) |
| `HidSharp` | HID device enumeration and reading |
| `SharpDX.DirectInput` | Alternative HID/gamepad input |
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
