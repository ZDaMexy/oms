# OMS — 产品与架构约束索引

> **读取与维护规则**：跨 Agent 协作规则唯一来源是仓库根 [AGENTS.md](../../AGENTS.md)。本文件只作为产品/架构硬约束索引，不作为当前状态页；默认先读 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md) 与 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，再用 `rg -n "关键词" OMS_COPILOT.md` 定点读取相关章节。当前能力、进度和验证只以 STATUS 为准。

> **OMS** is a Windows-only rhythm game client forked from osu!lazer.  
> It removes all game modes except osu!mania, and adds a new first-class **BMS mode**  
> designed to fully replace LR2 and beatoraja as a modern BMS player.  
> Long-term goal: private server integration (accounts, leaderboards, beatmap download).

---

## 1. Project Identity

| Field | Value |
|---|---|
| **Project Name** | OMS |
| **Base** | osu!lazer (locked commit, see [UPSTREAM.md](../other/UPSTREAM.md)) |
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
- Version-to-version updates before online features exist are manual file-overwrite updates. New packages must support replacing program files in place without forcing users to re-import local BMS content, and current release guidance must preserve the existing portable/non-portable mode, portable-mode `data/`, and the `storage.ini` pointer in the bootstrap storage. New packages contain `portable.ini`; non-portable updates must leave that marker absent before launch.
- Official ZIPs include `portable.ini`, so bootstrap storage is the program-adjacent `data/` directory. Without that marker, host defaults are `%APPDATA%/oms/` (Release) and `%APPDATA%/oms-development/` (Debug). The bootstrap `storage.ini` can redirect runtime data to one custom root.
- Beatoraja-style portable data mode is already supported via `portable.ini` -> `data/`; keep mutable user data in that dedicated subdirectory rather than mixing it directly with binaries.
- Registered multi-root external beatmap libraries have a working baseline: `ExternalLibraryConfig` (JSON-based, `library-roots.json`) for root registration, and `ExternalLibraryScanner` (delegate-injected) for walking BMS / mania roots and importing discovered sets. Settings -> Maintenance add/remove/scan and basic managed-set deletion are implemented; missing-root invalidation, root removal effects and cross-root duplicate/recovery semantics remain P1-H work.
- All OMS-owned networked product features, including account login, leaderboards, beatmap download, chat, news, multiplayer, spectator, daily challenge, and automatic update, remain disabled or hidden until Phase 3. A user explicitly adding a public BMS difficulty-table URL is an existing narrow exception independent of OMS private/default endpoints; it must not expand into an OMS online product surface.
- Current local-first builds should not ship non-empty default API / OAuth / SignalR / BSS server URLs; if online code remains in the tree, it is Phase 3 technical reserve rather than user-facing functionality.

---

## 2. Repository Structure

Use the current project and production entry points below; implementation inventories belong to the owning subline and source, rather than a second file tree in this document.

| Area | Production entry / responsibility | Governance |
| --- | --- | --- |
| Core | `osu.Game/OsuGameBase.cs`, `OsuGame.cs`: application composition, offline API, Realm and shared UI | mainline |
| Shared gameplay skin | `osu.Game/Skinning/Gameplay/`: document, material, layout publication, scene and read-only events | P1-A |
| Mania | `osu.Game.Rulesets.Mania/ManiaRuleset.cs`, `UI/`, `Skinning/`: mania gameplay and neutral runtime adapters | P1-A / P1-K |
| BMS parse/import | `osu.Game.Rulesets.Bms/Beatmaps/`: decoder, converter, folder/archive import and timing profile | P1-H / P1-K |
| BMS gameplay | `osu.Game.Rulesets.Bms/Objects/`, `Scoring/`, `Mods/`, `Replays/`, `UI/` | P1-C / P1-E |
| BMS audio/BGA | `osu.Game.Rulesets.Bms/Audio/`, `UI/BmsBgaPlayer.cs`, `UI/BmsBgaPanel.cs` | P1-J / P1-L |
| BMS skin/layout | `osu.Game.Rulesets.Bms/Skinning/`: sole layout solver, material and scene adapters | P1-A |
| Song Select / difficulty | `osu.Game.Rulesets.Bms/SongSelect/`, `Difficulty/`, `DifficultyTable/`; shared `osu.Game/Screens/Select/` | P1-I / P1-K |
| Input | `oms.Input/OmsInputRouter.cs`, `Devices/`; BMS input bridge | P1-B / P1-D |
| Desktop/release | `osu.Desktop/Program.cs`, `OsuGameDesktop.cs`, `build-release.ps1` | P1-F / P1-G |

The build entry is `osu.Desktop.slnf`; core tests live in the separate `osu.Game.Tests` project. All projects target .NET 8; `global.json` permits a newer stable SDK via `latestMajor`. No dedicated `oms.Server` project or P1-M player implementation is implied by the planned contracts below.

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
#WAV## (keysound index), #BMP## (BGA frame index)
#BPM## (BPM table for #BPMXX channels)
#STOP## (stop duration table)
#LNOBJ (long note end marker object)
#LNTYPE 1 | 2 (long note encoding style)
#RANDOM / #SETRANDOM / #IF / #ELSEIF / #ELSE / #ENDIF / #ENDRANDOM
#SWITCH / #SETSWITCH / #CASE / #SKIP / #DEF / #ENDSW
```

**Additional header field notes:**
- `#SUBTITLE`: secondary title line; store separately, do not concatenate with `#TITLE`. Display below the primary title in the song select carousel card and the chart detail panel. Omit from the result screen title line to keep it compact. If `#SUBTITLE` is an empty string or absent, do not render a subtitle element.
- `#SUBARTIST`: chart-level secondary artist / arranger credit. Store separately rather than merging into `Artist`, and surface it in the chart detail panel / Song Select summary when present.
- `#COMMENT`: chart-level freeform note. Persist it in BMS-specific metadata for detail views, but do not force it into compact carousel labels.
- `#PLAYLEVEL`: preserve the raw author level in chart metadata; native BMS numeric difficulty resolves from this value or persisted metadata, not the preview density graph (Section 9).
- `#DIFFICULTY`: integer 1–5 mapping to Beginner / Normal / Hyper / Another / Insane; used for intra-set difficulty labelling and sort order when no table entry exists

**Channel parsing (measure/channel/data blocks):**

```
#MMMCC:data
```
- `MMM` = measure number (3-digit, 0-padded, `000`–`999`)
- `CC` = raw two-character channel token. Recognised channels use hexadecimal parsing; preserve `RawChannelToken` and diagnose unsupported/non-hex tokens as `unknown_channel` rather than losing the source token.
- `data` = normally a base-36 object sequence in two-character segments; direct BPM channel `03` is hexadecimal and channel `02` is decimal. Keep these numeric domains separate.

> **Channel `02` exception:** Channel `02` data is a **decimal floating-point number** (e.g. `0.75`), not a base-36 object sequence. Parse it as `double` directly.

**Key channels to implement:**

| Channel | Meaning |
|---|---|
| `01` | BGM (background audio, no hit) |
| `02` | Measure length multiplier (e.g. `0.75` = 3/4 time for this measure) |
| `03` | BPM change (direct value, hex integer → decimal BPM) |
| `04` | BGA base layer |
| `06` | BGA poor layer (shown on POOR judgment) |
| `07` | BGA overlay layer |
| `08` | BPM change (via #BPM table) |
| `09` | STOP |
| `11`–`19` | 1P playable lanes (1-9) |
| `21`–`29` | 2P playable lanes (1-9) |
| `51`–`59` | 1P long note lanes |
| `61`–`69` | 2P long note lanes |

Channel `02` is a measure-local multiplier: `beatsPerMeasure = 4 * multiplier`, with `1.0` for an omitted measure. Multipliers never accumulate as a product. `BmsBeatmapConverter` resolves one event-time map including BPM/STOP and measure lengths; notes, BGA, mines and `MeasureStartTimes` consume it. `BmsPlayfield` builds bar lines from `MeasureStartTimes`, including fractional measures. Do not introduce a second renderer-side timing calculation or force arbitrary multipliers into integer `TimeSignature`.

**Key layout by mode:**

| Mode | Play channels | Scratch |
|---|---|---|
| 5K (5+1) | `11`–`15` | `16` |
| 7K (7+1) | `11`–`15` + `18`/`19` | `16` |
| 9K (BMS) | `11`–`19` | — |
| 14K DP | `11`–`15` + `18`/`19`; `21`–`25` + `28`/`29` | `16` + `26` |

**Keymode authority:** `BmsBeatmapDecoder` resolves keymode once from the override and chart evidence, in this order: explicit override → P2 evidence → `.pms` → `.bme` → complete 9K coverage → distinctive 9K evidence → complete 7K coverage → complete 5K coverage. Sparse/ambiguous or conflicting evidence is rejected with diagnostics; there is no silent 7K default and no current user correction UI. The converter, lane timeline and layout consume the resolved mode; rendering must not re-detect it. Detailed evidence families and rejection tests belong to [P1-K CONSTRAINTS](../subline/P1-K/TECHNICAL_CONSTRAINTS.md).

**Branch compatibility:** `#RANDOM` currently selects the fixed value `1`; `#SETRANDOM n` selects `n`. `#IF/#ELSEIF/#ELSE` execute the matching branch, so an `#ELSE` body can run when `#IF 1` is absent. `#SWITCH` defaults to `1`, while `#SETSWITCH n` fixes `n`; `#CASE/#SKIP/#DEF` are implemented. Preserve diagnostics for this deterministic compatibility path. True random selection remains future work.

**`#RANK` field mapping (default judge when no Mod active):**

| #RANK value | Judgment preset |
|---|---|
| 0 | VERY HARD |
| 1 | HARD |
| 2 | NORMAL (default) |
| 3 | EASY |
| 4 | VERY EASY |

### 4.2 BMS Package Import

BMS beatmaps arrive as `.zip`, `.rar`, or `.7z` archives, not `.osz`.

Import pipeline:
1. User drops archive onto OMS window or uses Import dialog
2. `BmsArchiveReader` prepares/extracts the archive; `BmsBeatmapImporter` delegates chart folders to `BmsFolderImporter`.
3. Scan extracted folder for any `.bms`/`.bme`/`.bml`/`.pms` file — each file is one **difficulty**
4. Group all difficulties sharing the same folder into one **BeatmapSet**, regardless of keymode differences. Files with different keymodes (e.g. a 5K chart and a 7K chart in the same folder) are not split into separate sets — keymode is stored as per-difficulty metadata and used for lane layout selection at play time. In the difficulty selector, the keymode is shown as a label on each difficulty entry (e.g. "7K", "5K").
5. Register keysound and static-art asset paths relative to the extracted folder root. Keysound, preview, and static background references are normalised against the actual extracted filenames; extension-substitution mismatches such as `stage.bmp` → `stage.png` should be resolved during import instead of left to fail silently at play time.
6. Internal import copies the chart folder into `chartbms/` and registers Realm metadata. External library registration keeps the source in place without copying. Temporary archive preparation is cleaned up by its owner.

Do **not** convert to `.osz`. OMS reads BMS files directly from disk at runtime.

Do **not** route imported BMS charts or their dependent assets through the generic `files/` hash-backed store. The extracted folder inside OMS chartbms is the source of truth; the database only persists metadata and path/location references needed for lookup and reload.

**Parse failure handling:** The folder importer reports failed charts and decoder warnings while retaining successful siblings. Do not infer failure solely from an empty `HitObjects` list: empty/no-stats charts have an explicit resolved metadata path. Encoding, keymode and malformed-input failures follow decoder diagnostics. Ownership and data-path constraints live in [P1-H CONSTRAINTS](../subline/P1-H/TECHNICAL_CONSTRAINTS.md); parser acceptance belongs to P1-K.

### 4.3 Keysound System

BMS is fully keysounded — every note triggers a specific audio sample.

Runtime contract:
- Decode `#WAV##` entries into beatmap-relative lookup metadata during `BmsBeatmapConverter`
- Carry that lookup metadata on `BmsBgmEvent`, `BmsHitObject`, and `BmsHoldNote` runtime objects so gameplay drawables do not need a back-reference to decoder output
- Every `BmsHoldNote` creates a nested `BmsHoldNoteTailEvent`; LN/CN/HCN determines whether it scores. Tail creation is not conditional on a distinct sample.
- Route BGM / note / LN keysounds through a shared `BmsKeysoundStore` instead of letting each drawable own an independent keysound player

Requirements:
- Index up to 1295 (`ZZ` in base-36) keysound slots per chart
- **Supported audio formats:** `.wav` (primary — required), `.ogg` and `.mp3` (secondary — support if ManagedBass can load without additional plugins). Format resolution: attempt the exact filename referenced by `#WAV##` first; if not found, retry with extension substituted in order (`.wav` → `.ogg` → `.mp3`). Log a warning on any substitution. Do not silently succeed without logging.
- Player/autoplay preloads referenced samples into the shared store; remaining on-demand loads use the same session cache. Preload and pool behaviour are owned by P1-J.
- Route playback through the shared `BmsKeysoundStore` pool. Native BMS keeps an internal baseline and grows automatically when demand exceeds it; converted-mania uses its own safe floor. The removed `KeysoundConcurrentChannels` user setting must not be described or reintroduced as current configuration without a new product decision and runtime proof.
- BGM channel (`01`) samples play regardless of player input
- Missing keysound files: log warning, play silence, do not crash
- On note hit: trigger the note's assigned keysound immediately
- Natural unpressed misses are silent; a consumed key-down pressed-poor/miss still plays the note WAV, and an empty press may play the armed lane timeline sample. Audio dispatch is not determined solely by judgement name; BGM remains independent.
- **LN tail audio:** Native and converted LN tails remain silent, including successful releases. Head/press audio and mode-specific tail judgement are separate; keep the [P1-J contract](../subline/P1-J/TECHNICAL_CONSTRAINTS.md) and regression tests authoritative.

### 4.4 Long Note Handling

Support both LN encoding styles:

**`#LNOBJ xx`:** The object `xx` appearing in a lane terminates the most recent note in that lane as an LN tail. Standard LN body holds in between.

**`#LNTYPE 1`:** Channels `5x`/`6x` define LN lanes. Object start = LN head, next object = LN tail.

**`#LNTYPE 2`:** MGQ format — less common. Preserve explicit `00` closing markers and convert the supported minimal expression through the normal hold-note path; unsupported edge cases must remain recoverable through typed/raw preservation rather than being silently discarded.

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
| LN tail | nested `BmsHoldNoteTailEvent` on every hold | Not top-level; time is `StartTime + Duration`, scoring follows LN/CN/HCN, and tail audio is silent |
| BGM note (channel `01`) | `BmsBgmEvent` (non-hittable) | Carries beatmap-relative keysound sample metadata and plays through the shared `BmsKeysoundStore`; excluded from judgment and scoring |

`BmsHitObject` and `BmsHoldNote` must carry all keysound and lane metadata needed at runtime, including beatmap-relative sample lookup metadata. They must not require a back-reference to the raw decoder output after conversion.

`BmsBeatmapConverter` should continue to materialise one `BmsHoldNote` timing object per parsed long note. Whether that object's tail becomes a scored judgement point is decided later by `BmsLongNoteMode`: `LN` counts head only, `CN` / `HCN` count head + tail, and `HCN` additionally feeds body gauge ticks without changing beatmap conversion.

**Timing and output authority:** The converter integrates header BPM, channel `03` direct BPM, channel `08` indexed BPM, channel `09` STOP and per-measure channel `02` multipliers into one ordered event-time calculation. STOP advances absolute time by `stop_value / 192 * 4 * 60000 / current_bpm`; its scroll-freeze interval uses the typed `BmsStopFreezeTimingControlPoint` and zero-distance `BmsScrollProfile` segment, not an untyped high-BPM heuristic. Conversion to mania strips only that BMS-specific marker, preserving genuine high BPM values.

`BmsMeasureLengthControlPoint` retains measure metadata while notes, BGA, mines and `MeasureStartTimes` share the resolved time map. Runtime consumers use those resolved outputs; no per-note multiplier reconstruction or renderer-side timing authority is permitted. The imported beatmap/set metadata belongs to the importer and keymode belongs to the decoder. Full timing/tie-order/STOP and keymode contracts are maintained in [P1-K CONSTRAINTS](../subline/P1-K/TECHNICAL_CONSTRAINTS.md).

---

### 4.6 BPM Changes and STOP

- `#BPM` in header = initial BPM
- Channel `03` = inline BPM change (value is hex, convert to decimal)
- Channel `08` = BPM table lookup via `#BPM##` header
- Channel `09` = STOP (value references `#STOP##` table; each unit = 1/192 of a **4/4 measure** at the current BPM; conversion: `stop_ms = stop_value / 192.0 × 4.0 × (60000.0 / current_bpm)`)

The resolved event times, typed control points and `BmsScrollProfile` carry timing and scroll authority together. BMS Hi-Speed consumes this model; it does not inherit mania scroll speed verbatim or identify STOP through a generic BPM threshold.

For osu!mania, settings-page milliseconds are only a reference under standard lane geometry. User skins may change lane size, hit position, and scaling, so mania fall-time readouts are not a cross-skin or cross-ruleset contract and must not be compared directly with BMS Hi-Speed milliseconds.

---

## 5. Judgment System

### 5.1 Architecture

`BmsJudgementSystem` selects the `OD`, `BEATORAJA`, `LR2` or `IIDX` family through the active mod. `Evaluate(offset, isLongNoteRelease, isScratch)` and the family-specific early/late, scratch and release windows determine the result. Default OD reads the chart rank; alternative families preserve their own rank scaling and asymmetry rather than sharing one generic strictness multiplier.

Numeric windows, truncation rules and excessive-poor semantics are maintained in [P1-C CONSTRAINTS](../subline/P1-C/TECHNICAL_CONSTRAINTS.md) and the judgement-system tests. UI and persisted score semantics must keep these families distinct. This section does not duplicate window tables that can drift from the production implementation.

### 5.2 Poor Judgment (Empty Poor)

**Empty Poor / excessive poor** is judge-family-specific, not a universal BAD-window fallback. Penalizes gauge.

Implementation:
- Maintain a per-lane active note-window state keyed by the active `BmsJudgeMode`
- `LR2` mode must treat excessive poor as pre-note-only semantics
- `BEATORAJA` mode must use its own early / late excessive-poor family rather than reusing generic BAD edges
- Scratch notes and long-note release may require specialized windows when the active judge family defines them
- `BmsPoorJudgement` (inside `BmsEmptyPoorHitObject`) uses `HitResult.Ok`; gauge damage is determined by the active gauge family, not universally equal to BAD.
- Empty Poor does **not** affect EX-SCORE
- Empty Poor does **not** break combo or affect accuracy/EX-SCORE, but it affects gauge and FULL COMBO/PERFECT eligibility. `TestEmptyPoorDoesNotBreakComboWithoutAffectingExScoreOrAccuracy` protects this distinction.
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
| Empty POOR | unchanged; neither increment nor reset |

Note: GOOD **does not break combo**, but having any GOOD in a run **disqualifies FULL COMBO** (§6.3). FULL COMBO requires PGREAT + GREAT only; PERFECT requires PGREAT only.

### 5.5 Keysound Dispatch

Keysound ownership follows the consumed input and armed lane timeline, not a yes/no lookup on judgement result. Natural misses and LN tails are silent; consumed key-down poor/miss and empty presses may sound according to §4.3 and P1-J. Judgement, scoring and BGM timing remain independent.

---

## 6. Scoring System

### 6.1 EX-SCORE

BMS uses EX-SCORE as its score value. `BmsScoreProcessor` extends the existing `ScoreProcessor` and overrides BMS scoring semantics; retain the shared processor lifecycle.

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

Gauge type and gauge rule family are separate. `BmsGaugeProcessor` supports Assist Easy, Easy, Normal, Hard, ExHard and Hazard, with Legacy / Beatoraja / LR2 / IIDX family implementations. Recovery, damage, start values, clear thresholds and `#TOTAL` treatment are family-specific; the Legacy formula is not a universal rule and IIDX does not derive its rates from `#TOTAL`.

Score/gauge pools must agree with LN/CN/HCN and A-SCR/A-NOT: BGM and assisted notes stay out of the manual pool; HCN body ticks move gauge without adding EX-SCORE or combo. Hazard's GOOD behaviour and survival/groove failure behaviour follow the active tested family. See [P1-C CONSTRAINTS](../subline/P1-C/TECHNICAL_CONSTRAINTS.md) and the gauge-family tests for numeric rules.

Current local score bucketing distinguishes implemented gauge/judge/long-note semantics. Private-server submission and online leaderboard filters remain Phase 3 work; no endpoint or new persisted filter setting is implied here.

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
- PERFECT requires positive MAX EX-SCORE, EX-SCORE == MAX EX-SCORE and Empty Poor count == 0, subject to the clear condition
- FULL COMBO / PERFECT use the active runtime long-note mode's scored points. `LN` only checks heads; `CN` / `HCN` require both head and tail to remain eligible. `HCN` body ticks can still fail gauge without adding separate combo points, so result persistence must confirm the run still satisfies the clear condition before awarding `FULL COMBO` or `PERFECT`.
- Result-side clear-lamp / final-gauge / gauge-history reconstruction consumes the already-modded playable beatmap. Configure the gauge from that beatmap; do not reapply the beatmap-mod chain, which would repeat lane rearrangement or assist transformations.

Only upgrade lamp, never downgrade.

**Current note:** Now that `A-SCR` and `A-NOT` exist, score-pool exclusion, gauge exclusion, and result-side lamp/gauge reconstruction rules must stay specified together with the mods. Do not silently diverge score / gauge / lamp semantics across later leaderboard, persistence, or results-history work.

### 6.4 Gauge Auto Shift — GAS (`BmsModGaugeAutoShift`)

`BmsModGaugeAutoShift : Mod` exposes persisted `StartingGauge` (default ExHard) and `FloorGauge` (default Easy), mutually exclusive with individual gauge mods. Gauge rank order is Hazard → ExHard → Hard → Normal → Easy → Assist Easy, but automatic downgrading applies only to failed survival gauges and is bounded by the configured floor. Entering Normal stops downgrading; a floor of Easy does not cause a later Normal → Easy transition. Groove gauges finish at song end instead of causing mid-song failure.

On downgrade, the new active gauge starts at its family/type default value. Result history displays each activated segment of this one evolving gauge run. The result lamp follows the final active gauge and the playable run, not the best of independently simulated gauges. Keep downgrade and history semantics aligned between gameplay and `BmsClearLampProcessor`; the regression `TestGaugeAutoShiftDowngradesAndAwardsFinalActiveGaugeLamp` locks this distinction. Private-server GAS submission remains unimplemented Phase 3 work.

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

Online leaderboard filters remain Phase 3 scope. Add persistent configuration only when the actual consumer and compatibility contract are implemented.

`BmsKeysoundStore` capacity is an internal runtime policy with automatic growth, not a persistent user-tunable ceiling. The removed `KeysoundConcurrentChannels` setting must not be used as replay/config authority; add new persistent state only when a consuming feature and its compatibility contract land together.

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

- BMS judgement and EX-SCORE remain the gameplay semantics. The always-on speed/FAST-SLOW/pacemaker card was removed by product decision; current speed feedback uses the pre-start preview and held-adjustment toast. Do not infer a task to restore a permanent card from this section.
- For key-sounded BMS play, the primary user-facing timing correction path is a visual draw offset / note presentation adjustment. Do not make audio playback offset the default or sole timing-fix control for BMS mode.
- Timing feedback and adjustment should be low-friction from gameplay and result flow so future retry / pacemaker / target-practice features can reuse the same loop.

---

## 7. Layout System

### 7.1 Lane Configuration (`BmsLaneLayout`)

`BmsLaneLayout` describes keymode/lane topology. C3 resolves geometry through the single immutable layout snapshot; playfield, gauge/combo, HUD and BGA viewports consume that result. Lane widths, scratch position and receptor coordinates must not gain a second per-component authority.

Current Phase 1 surface: settings now expose a basic single-play `Playfield Style` with four options for 5K / 7K only: `1P (left anchored with intentional screen-side inset)`, `2P (right anchored with intentional screen-side inset)`, `Center (left scratch)`, and `Center (right scratch)`. This adjusts playfield anchoring and scratch visual side without flipping bindings; the skin runtime consumes the resolved side-aware layout. 9K remains centered and 14K remains fixed DP.

**Phase 2 target — 1P/2P flip (`BmsModMirror1P2P`):**
- Current repository status: the workspace now has `BmsModMirror` and `BmsModRandom` for button-lane rearrangement, but it still does not have a dedicated full-side `1P/2P flip` mod.
- Mirrors the entire lane array horizontally
- Updates all key bindings to their mirrored counterparts
- Scratch moves from left to right (or vice versa)
- Skin elements that are side-dependent must respond to a `CurrentSide` bindable (1P/2P)

### 7.2 Lane Cover (`BmsLaneCover`)

Sudden and Hidden are rendered as opaque overlay panels on the playfield, while Lift independently raises the judgement line by shortening the lane from the bottom.

Controlled by Mods:
- `BmsModSudden` — enables upper masking, exposes the legacy-named `CoverPercent` setting on the 0–1000 cover-unit scale
- `BmsModHidden` — enables lower masking, exposes the legacy-named `CoverPercent` setting on the 0–1000 cover-unit scale
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

Current BMS gameplay also includes a `阻止谱面开始/ingame start` operator surface backed by `UI_PreStartHold`: entering gameplay inserts a 5-second delayed-start window, and holding that action during the window blocks the actual start while showing the selected Hi-Speed mode and current value. If that delayed-start window has already elapsed while the action is still held, releasing the hold must arm a fresh full delay instead of starting gameplay immediately. The same held action remains a full-session adjustment modifier after gameplay has started: odd-numbered lanes increase the current Hi-Speed, even-numbered lanes decrease it, `UI_LaneCoverFocus` / scroll-wheel / middle-click lane-cover controls remain available, and the centered `BMS speed` toast should stay visible while the modifier is held. While the modifier is held, new lane actions must not be forwarded into gameplay hit handling; they belong to the adjustment chain only. `UI_PreStartHold` and `UI_LaneCoverFocus` are separate actions with independent key bindings (default 5K/7K/9K: PreStartHold = Q, LaneCoverFocus = W; 14K: PreStartHold = T, LaneCoverFocus = Y). Treat this as runtime operator interaction, not as a settings-page preview or a replacement for the skinnable HUD contract.

Current OMS also includes a pre-start speed preview scoped to the blocked-start window while `UI_PreStartHold` is actively held and actual gameplay has not yet started. It is implemented as a pure visual preview layer on the first non-scratch lane using the current BMS note skin lookup and scroll/time authority (via `BmsPlayfield` / `BmsHitObjectArea` / `BmsScrollSpeedMetrics`) and follows the same pre-start clock / pause state as the visible playfield. It is not a real `BmsHitObject` / `DrawableBmsHitObject`, does not route through lane hit handling, and cannot affect judgement, score, replay, autoplay, or keysound behaviour. Once actual gameplay starts, the preview disappears even though the same held action continues to adjust Hi-Speed.

If OMS later extends Floating Hi-Speed semantics, ship it as a complete contract across scroll speed, lane cover, LIFT, BPM compensation, start-sequence behavior, and displayed terminology. Do not market or document the current OMS-local GN/WN feedback as complete FHS.

---

## 8. Input Abstraction Layer (`oms.Input`)

### 8.1 Design Goal

All input hardware — keyboard, IIDX controller, arcade controller, gamepad — must map to the same abstract `OmsAction` enum. The game layer never reads hardware signals directly.

Current implementation: the production `BmsAction` bridge (`Key1`-`Key16` + `LaneCoverFocus` + `PreStartHold`) connects `oms.Input` to BMS gameplay; `OmsAction <-> BmsAction` routing, complete keyboard-combination semantics, the Windows Raw Input keyboard path, mouse delta parsing, the XInput button path via `OnJoystickPress()` / `OnJoystickRelease()`, 5K/7K default XInput bindings, ruleset default keybinding export for joystick buttons, joystick-only persisted binding round-tripping, the generic keybinding UI path for joystick button display/capture, HID-trigger persistence/editor live capture, and the provider-backed HID code path are all present. Windows now uses a DirectInput-backed HID provider by default, while `HidSharp` remains available as a diagnostic backend behind `OMS_ENABLE_HIDSHARP=1` to avoid the historical `HidSharp.DeviceList.Local` `RegisterClass failed` crash path. Desktop Settings -> Input now intentionally hides the upstream generic `MouseSettings` / `TouchSettings` / `TabletSettings` subsections via `OsuGameDesktop.CreateSettingsSubsectionFor()`; this is a product-surface trim only, not a runtime input deletion. Do not move that suppression into `OsuGameBase` unless the intention is to also change test-scene and non-desktop host behaviour. Remaining input work is mainly richer cross-device semantics and real-hardware validation. Keep the production bridge as the current consumer; future changes require a concrete input-semantic gap rather than a parallel routing architecture.

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
Reads raw mouse delta (X or Y axis) and dispatches directional press/release pulses. Binding inversion is implemented; user-facing sensitivity/deadzone calibration remains P1-D work.

**Axis inversion:** All analog axis handlers (`OmsHidAxisInputHandler` for rotary encoders, `OmsMouseAxisInputHandler`) must expose an `AxisInverted` boolean flag per binding in `OmsBindingStore`. DIY controller encoder wiring varies in polarity; inverting the axis direction in software avoids hardware rewiring. When `AxisInverted = true`, the CW/CCW mapping is swapped before delivering the signal to `OmsInputRouter`. The flag is stored per-binding entry in `OmsBindingStore` — each bound action carries its own independent inversion state. This applies equally to HID rotary encoders and mouse axis bindings; it is not a global mouse preference.

### 8.3 Scratch / Analog Axis Handling

The scratch lane accepts three signal types simultaneously:

1. **Digital key** (keyboard or HID button): binary on/off, treated as a constant scratch input while held
2. **Analog axis** (HID rotary encoder, e.g. AS5600 on DIY controller): sampled value converted to directional press/release pulses per poll; no velocity-valued gameplay contract is implemented
3. **Mouse delta**: same as analog but sourced from mouse movement

All three can be bound at once. The scratch lane renders activation whenever any active signal exceeds its threshold.

### 8.4 Binding UI

The binding screen must support:
- Listening for any of the four signal types when recording a binding
- Displaying bound signal type with icon (keyboard key / HID button / axis / mouse)
- Separate binding profiles per keymode (5K / 7K / 9K / 14K DP)
- In 14K DP mode, a single **DP profile** contains both 1P-side and 2P-side bindings simultaneously — the binding UI presents both sides in a unified layout. There are no separate "14K 1P" and "14K 2P" profiles.
- Single-play modes use their keymode variant bindings (6/8/9 actions; DP has 16). The 5K/7K Playfield Style changes visual side without selecting a separate 1P/2P binding set; 9K has no side profile.

### 8.5 Calibration and Diagnostics

Bindings alone are not sufficient for BMS controller support. The BMS input/settings surface must eventually expose user-visible calibration and diagnostics for deadzone, sensitivity, scratch signal expectations (digital vs analog), side mapping, and live input preview / sanity checks. Keep this hardware-tuning layer separate from per-chart gameplay logic, but do not leave it hidden behind backend-only code paths.

---

## 9. Difficulty System

### 9.1 Native BMS level authority

Native BMS uses the chart author's `#PLAYLEVEL`, not a computed density-star model. `BmsDifficultyCalculator` returns ordinary `DifficultyAttributes` and creates no difficulty skills. It first parses the decoded chart's level, then uses persisted BMS metadata through `BmsStarRatingResolver`; a missing usable level resolves to zero. An already persisted non-negative `BeatmapInfo.StarRating` remains the resolver's first source.

`BmsStarRatingResolver.TryParsePlayLevel()` extracts the first positive numeric token from the author's text. Community difficulty-table labels remain separate persisted metadata and are displayed alongside the author level; they are not a replacement density calculation.

### 9.2 Converted mania stars

BMS projected to mania uses the mania difficulty pipeline and persists the converted result for the target ruleset/version. It must not reuse the native BMS author level as a mania star rating. Import/recompute readiness and exclusion of sample-only objects follow [P1-K CONSTRAINTS](../subline/P1-K/TECHNICAL_CONSTRAINTS.md).

### 9.3 Note-distribution analysis

`BmsNoteDistributionAnalyzer` belongs to the Song Select preview, producing weighted time windows, normal/scratch/LN counts, peak NPS and a percentile statistic. These values describe chart shape; they do not feed native BMS `StarRating`. The graph uses the same analysis result for its bars and summary, as described in section 11.

### 9.4 Display and filtering

Consumers use the persisted author level and difficulty-table entries through the current resolvers; selection must not reopen a BMS file solely to obtain labels. Native BMS, converted mania stars and the note-distribution preview are separate authorities. Current grouping/filtering behavior and the still-unimplemented single-track composition UI are tracked in [P1-I STATUS](../subline/P1-I/DEVELOPMENT_STATUS.md).

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
- Share one manager instance between settings and first-run via `GetShared(storage, realmAccess)` — pass the injected game-wide `RealmAccess`; never construct a second `RealmAccess` for write-back (its ctor runs `cleanupPendingDeletions`, which would over-reach and race the global instance)
- Rebuild persisted beatmap metadata after source mutations via the injected `RealmAccess`, then emit `TableDataChanged`. The write uses `BmsBeatmapMetadataData`, which **shares the single `BeatmapMetadata.RulesetData` column** with osu.Game's `BmsPersistedMetadataData` (converted star ratings) — both carry `[JsonExtensionData]` so neither write wipes the other's fields (prior bug: a star recompute wiped every chart's table entries → all Unrated; the reverse forced endless recompute). Carousel does NOT live-refresh on mutation (shallow `BeatmapSetInfo` subscription can't see deep `Metadata` writes) — mid-session table changes need a restart, startup is always correct. (A per-set `DifficultyTableRevision` bump to force re-detach was tried and reverted: multi-minute refresh storm at large library scale.)
- `SetSourceEnabled` / `RemoveSource` expose async entry points so UI callers never run write-back on the update thread
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
4. Batch-update persisted beatmap metadata for affected local BMS beatmaps (normalising `BeatmapInfo.MD5Hash` casing before matching) via the injected `RealmAccess`. The write preserves osu.Game's converted-star-rating fields in the shared `RulesetData` column via `[JsonExtensionData]`. Carousel reflects table changes on its next detach (i.e. on restart for mid-session changes; startup is always correct)
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
    string Md5,            // lowercase hex
    int TableSortOrder = 0 // persisted parent-table order
);
```

### 10.5 Song Select Integration (`BmsTableGroupMode`)

The BMS table grouping consumes persisted table entries. `TableSortOrder` / table name form the parent group and numeric `Level` / display label form its child; charts with no table entries appear in `Unrated` at the end. A chart may appear in several table groups. Hierarchical display flattens sets to individual charts below table/level; it does not insert another BeatmapSet navigation layer. Group construction does not fetch or re-parse a table or chart.

Grouping and sorting are separate. The sort dropdown remains enabled; difficulty sorting uses the resolved native author/persisted rating from Section 9, not note-distribution density. Search and collections continue to use the normal Song Select filter pipeline. Display flattening and navigation are owned by [P1-I CONSTRAINTS](../subline/P1-I/TECHNICAL_CONSTRAINTS.md).

### 10.6 Library Grouping (`InternalLibrary` / `ExternalLibrary`)

OMS now provides two additional **BMS-only** song-select grouping modes: `InternalLibrary` and `ExternalLibrary`.

This is not a shared osu! song-select feature. The dropdown remains ruleset-driven via `Ruleset.GetAvailableSongSelectGroupModes()`, and non-BMS rulesets must not expose these modes.

**Shared grouping-engine contract:** once BMS has more than one hierarchical grouping mode, shared code must stop hardcoding `GroupMode.DifficultyTable` as the only hierarchical path. `BeatmapCarouselFilterGrouping` should instead use a generic ruleset-specific hierarchical grouping path whenever the active ruleset returns `GroupDefinition` data for the selected mode. Do not add a second or third one-off special case.

**Enum / config safety:** append new `GroupMode` values at the end of the enum (or otherwise preserve persisted numeric compatibility). OMS must not churn the stored `SongSelectGroupMode` value for existing users just because BMS adds new modes.

**`InternalLibrary` grouping authority:**

- Applies only to BMS sets where `BeatmapSetInfo.IsExternalFilesystemStorage == false`
- Applies only when `BeatmapSetInfo.FilesystemStoragePath` is under the managed BMS root `chartbms/`
- Group hierarchy comes from the parent-directory segments under `chartbms/`
- Under hierarchical grouping, charts are standalone leaves. Do not repeat the final set directory as an artificial folder group or insert a BeatmapSet navigation layer.
- A set stored directly under `chartbms/` appears at the root of this grouping mode rather than under a fake "uncategorised folder" node

**`ExternalLibrary` grouping authority:**

- Applies only to BMS sets where `BeatmapSetInfo.IsExternalFilesystemStorage == true`
- The first grouping level must represent the external library root that the set belongs to
- Lower grouping levels come from the parent-directory segments relative to that external root
- Charts appear as standalone leaves below the directory groups; do not insert a BeatmapSet navigation layer.

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
| Scratch notes | Red | Notes classified as scratch by the converter (standard BMS channel 16/26) |
| LN heads/bodies | Blue | Long note heads and hold bodies |

The graph calls `BmsNoteDistributionAnalyzer.Analyze(..., 1000, 1000)` for one-second buckets. Its displayed density uses weighted counts, including chord, scratch and LN contributions; it is not a raw note-count or star-rating axis. Bucket categories are exclusive, while whole-chart SCR/LN summaries may overlap.

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
- Uses `BmsNoteDistributionAnalyzer` (see Section 9.3) for bucket computation — do not reimplement the sliding-window logic
- Read chart metadata from persisted beatmap metadata / ruleset data; do not re-open the source `.bms` file on selection just to populate summary lines
- Computed once on chart selection, cached for the session
- Rendered as a `Drawable` using osu-framework's immediate-mode drawing primitives (no external chart library)
- Bucket computation runs on a background task; results are pushed to the drawable via `Schedule()`. The song select UI thread is never blocked — the frame budget applies only to the final `Schedule()` callback and drawable update, not to the computation itself.
- Existing `NoteDistribution` and `NoteDistributionPanel` lookups provide the graph and panel skin surface
- The note distribution graph is preview authority only. Its scratch / LN summary counts may overlap and must not be reused directly as the source for any future mutually-exclusive Song Select filter taxonomy such as `RC / LN / SCR`.

---

## 12. BGA System

### 12.1 Static art chain

- **Static `#STAGEFILE`**: Primary static background art during gameplay.
- **Static `#BACKBMP`**: Fallback static background if `#STAGEFILE` missing.
- **Static `#BANNER`**: Final fallback static art if neither `#STAGEFILE` nor `#BACKBMP` resolves.
- OMS resolves static-art references through the imported working-beatmap background chain: import normalises the stored filename against real files on disk, runtime retries common image extension substitutions for older data, and the full-screen `BackgroundScreenBeatmap` shows it dimmed.

### 12.2 BGA timeline / animation contract

Current progress and manual gates belong to [P1-L STATUS](../subline/P1-L/DEVELOPMENT_STATUS.md); detailed invariants live in [P1-L CONSTRAINTS](../subline/P1-L/TECHNICAL_CONSTRAINTS.md).

- `BmsDecodedChart.BgaEvents` carries channels `04` base / `06` poor / `07` layer / `0A` layer2 plus typed `#BGA/#@BGA/#ARGB/#SWBGA/#POORBGA` definitions. Conversion produces a time-ordered `BmsBeatmap.BgaTimeline`, kept outside `HitObjects` like `Mines` and `ScrollProfile`.
- BGA renders in a skinnable `BgaPanel` above the playfield. Image frames use the beatmap `TextureStore`; video uses the framework playback clock; the POOR layer follows `#POORBGA`.
- Target: layout frames, clips or mirrors a single engine-owned BGA content surface. Current code creates a `BmsBgaPlayer` for each resolved viewport; the default 14K layout therefore has four players. C3 unified viewport geometry and C5 exposed read-only events; neither delivered a shared content/decoder instance. That migration remains P1-L work. Skins must not take playback authority; `FillMode.Fit` remains the default and converted-mania BGA stays outside this native path until separately gated.
- BGA is visual-only: it never enters `HitObjects` or judgement/scoring. Assets read directly from `chartbms/`, never the hash-backed `files/` store, and missing/decode-failed assets degrade without crashing.
- Legacy video transcode is opt-in and depends on a user-provided external ffmpeg; OMS does not ship ffmpeg. Without it, when disabled, or on failure, the runtime must retain the static-image fallback.
- Transcode publication must be concurrency-safe and atomic. Preload is bounded, cache lifetime is session-scoped, and timeout/failure falls back without turning a load optimisation into a gameplay blocker.

---

## 13. Skin System

OMS continues to use osu!lazer's `ISkin` / `ISkinSource` / `SkinnableDrawable` architecture, but OMS's **product surface** must no longer rely on upstream built-in skins as the final end-user default.

### 13.1 Product Direction

- OMS will ship two ordinary first-party `.osk` entries: **`oms-simple`** as the immutable final fallback and **`oms-complex`** as the public-API showcase/default candidate.
- Each package contains a **global layer plus separate mania and BMS ruleset layers**.
- Mania and BMS do not need to share the same gameplay asset semantics; they are integrated into each package, but remain independent ruleset skin implementations.
- `Argon`, `Triangles`, `DefaultLegacy`, `Retro`, and other osu!lazer-native built-in default skins must be removed from OMS's final shipped default selection surface once OMS replacement coverage is complete.
- During transition, code may temporarily retain upstream classes or resources, but no new OMS feature may depend on them as the intended release fallback.
- Hard-coded placeholder `Box`-based visuals are acceptable only as temporary development scaffolding. They are not an acceptable release-state fallback once the corresponding Phase 1.1 task is complete.
- `SKIN/SimpleTou-Lazer` may remain a mania compatibility/reference source, but it is not the architecture or private resource source for the BMS runtime. Direct-drawn BMS visuals are migration/failure feedback, not proof of release-ready default coverage.
- `BmsLegacySkin : LegacySkin`, `.osk` routing and F1 static config form the compatibility authoring base. The final fallback is validated `oms-simple.osk`; programmatic themed rendering is migration scaffolding only. Authority lives in [P1-A CONSTRAINTS](../subline/P1-A/TECHNICAL_CONSTRAINTS.md), [P1-A PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md) and [SKINNING.md](../other/SKINNING.md).
- Abnormal-period G1/F2/Lua/mania-adapter/reference-default work may re-enter only as isolated slices with authority-specific tests, path-containment proof and real-device acceptance; never restore it in bulk. Recovery evidence is preserved in the [recovery audit](../other/SKIN_SYSTEM_RECOVERY_20260710.md).
- **Skin V1 authority:** the first complete version is an external gameplay-skin runtime, not a larger family of fixed BMS C# visuals. The engine owns gameplay truth, playfield/BGA layout, generic rendering, package isolation and fallback resolution; `.osk` packages own every concrete colour, asset, node and animation. Mania/BMS share a neutral ini/asset/animation/event runtime and keep ruleset-specific topology adapters. V1 ships `oms-simple.osk` plus `oms-complex.osk`, both containing mania+BMS, editable source and the same authoring path as third-party skins.

### 13.1.1 Phase 1.1 implementation boundaries

- `SKIN/SimpleTou-Lazer` remains a mania-side compatibility/reference source, not the architecture or private resource source for the BMS runtime.
- Current execution order and gates are not repeated here; read [mainline PLAN](DEVELOPMENT_PLAN.md) and [P1-A PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md).
- Do not build mania and BMS twice. Share codecs, scene/animation primitives, event envelopes, diagnostics, reload and sandbox; keep mania stage/column and BMS scratch/DP/BGA/gauge adapters separate.
- Do not resume post-cutoff G1/F2/Lua/reference-default code in bulk. The old F/G labels remain historical indexes only; current execution authority is the P1-A campaign plan and its exit gates.

### 13.2 Fallback Hierarchy

OMS Skin V1 lookup is tri-state:

- `Provide`: use the package component.
- `Inherit`: continue per-component through the fallback chain.
- `Suppress`: intentionally omit an optional visual; do not re-inject the OMS visual behind it.

The final Skin V1 gameplay-package `Inherit` order is shown below. The current runtime still terminates at programmatic `OmsSkin`; canonical-package takeover remains gated by parity, integrity, atomic recovery and real-device acceptance.

1. User-selected external package.
2. Ruleset adapter / compatibility layer.
3. Read-only, validated `oms-simple.osk` canonical fallback.

Additional rules:

- This target order is not the complete provider hierarchy. Existing beatmap-local direct-visual compatibility and ruleset resource insertion retain their relative authority; `Suppress` does not pierce a higher-priority legacy provider by default. This does not expose beatmap-local ini/scene/script authoring: no public sidecar format is implemented.
- Missing components fall back **per component**, not by abandoning the entire OMS skin chain. Missing files never implicitly mean `Suppress`.
- Lane/scratch readability, note/LN/mine, judgement position and active lane-cover geometry cannot be fully suppressed. Key animation, judgement display, combo, gauge visual, HUD, BGA frame and decorative effects may be explicitly suppressed.
- BMS imported song folders are not treated as ad-hoc skin packages.
- Beatmap skin compatibility may remain as a compatibility layer for mania, but it is not allowed to become the only fallback path for OMS gameplay.
- BMS-specific lookups must never fall back to osu!lazer-native built-in skin assets once the OMS built-in skin exists.

### 13.3 Shared OMS Skin Architecture

The shared gameplay runtime and thin ruleset adapters are already implemented; canonical packages remain the C7 target. The product layers are:

- **OMS global layer**: package host, shared infrastructure, layout metadata, global HUD shells, common typography/icon resources, and fallback discipline.
- **OMS mania layer**: stage, column, key area, note, hold, hit target, bar line, hitburst, judgement/combo/HUD adapted to mania variants.
- **OMS BMS layer**: scratch-aware lanes, lane covers, note/hold parts, gauge/clear lamp, note distribution, BMS judgement naming, and static background presentation.

Shared means package structure, fallback infrastructure, and optional global UI language. It does **not** mean mania and BMS gameplay assets must be interchangeable or visually identical.

Existing implementation and remaining package boundaries:

- Both rulesets use the shared `OmsSkinTransformer` base.
- C3～C5 already supply the neutral layout/ini/material/scene/event runtime and diagnostics inside the current revision protocol. C6 adds the script sandbox and final package reload gate. The shared runtime must not depend on `BmsKeymode`, `ManiaAction` or concrete ruleset drawables.
- Existing mania and BMS adapters map gameplay state to neutral lane groups/roles/stable IDs and immutable events. Keep these production routes as the single authority.
- A protected preview `OmsSkin`-style selection entry may exist only as a migration host while `oms-simple` is incomplete. It counts as skeleton progress and must not survive as theme-specific rendering below the final file fallback.
- `ManiaRuleset.CreateSkinTransformer()` should continue migrating away from switching on osu!lazer-native built-in skin types as the final OMS product behavior. The explicit `OmsSkin` -> `ManiaOmsSkinTransformer` route already exists; it is transitional and does not establish canonical file fallback.
- `BmsRuleset.CreateSkinTransformer()` should keep returning a ruleset-specific transformer, but fallback visuals must resolve from `oms-simple`, not private themed C# drawables.

### 13.4 Shared Visual Contract

Both canonical packages must provide readable gameplay while allowing mania/BMS visuals to differ. Shared application UI and legacy lookups may follow the same visual language, but Song Select/results skinning and `ISerialisableDrawable` editing/serialization are not new public gameplay-author ABI. Non-gameplay hosts participate in revision lifecycle without expanding the author surface.

- Shared typography/colour language may guide application UI; gameplay HUD uses the versioned public slot contract.
- Shared colour-token system for package-level surfaces, separators, neutral text, focus/highlight states, and non-ruleset-specific UI.
- Shared animation-duration buckets for instant feedback, HUD transitions, and overlay entrances.
- Legacy serialisable components retain their existing restoration compatibility; the legacy editor remains disabled.
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
- Legacy mania remains a compatibility input. C5 connected key/hold/hit effects, judgement, combo and the other applicable public slots to the shared prepared scene and read-only event runtime. Gameplay truth stays in engine producers; authors control supported visual scenes. This does not include the unimplemented C6 script sandbox.
- The shared resolver must preserve whether a legacy value was explicitly declared. Legacy mania may synthesize a default configuration for a missing `Keys:` bucket; that synthetic value must not be mistaken for `Provide` in the new tri-state runtime.

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
- Note distribution panel
- Existing legacy/application UI lookups may cover results, note distribution and Song Select accents; these are outside the public gameplay author surface.

Simple enum-only lookup is insufficient for all BMS use cases. Where component rendering depends on lane metadata, use dedicated lookup types carrying:

- `LaneIndex`
- `LaneCount`
- `IsScratch`
- `Side` (`1P` / `2P`)
- `Keymode`
- Optional `CoverPosition` / `Focused` / `LongNotePart`

C3 established the single immutable layout authority for lane/scratch widths, spacing, playfield sizing and receptor geometry. Preserve this resolved authority through renderer and skin consumers; do not rebuild a default profile in individual components or add a parallel layout abstraction. Geometry overrides remain subject to the accepted layout contract.

The existing solver and adapters produce an immutable layout descriptor covering 5K/7K P1/P2/centred-left-scratch/centred-right-scratch, 9K BMS/PMS and 14K dual-deck. Gauge/combo/BGA placement must consume the same resolved descriptor as the playfield; recomputing a default profile in each component is not valid.

BGA decoding, timeline, seek and POOR state remain engine-owned; a single shared content instance is the pending P1-L target. A skin may frame, clip, mask, mirror and decorate a read-only BGA surface in descriptor-provided viewports, but must not create independent BGA players or clocks. The current 14K four-player/four-corner default is transitional, not a V1 contract.

> BMS 默认层与 mania 侧组件的当前迁移进度见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)；面向皮肤制作者的详细 lookup / preset 表见 [../other/SKINNING.md](../other/SKINNING.md)。
>
> 皮肤设计边界与绿色数字 / Mod 联动专题的执行规划、当前状态与技术约束，见 [../subline/P1-A/README.md](../subline/P1-A/README.md) 与 [../subline/P1-C/README.md](../subline/P1-C/README.md)。

BMS-specific visual rules:

- Scratch lane must remain visually distinct from normal keys even inside a shared OMS theme.
- The BMS layer lives inside the same default skin package as mania, but must keep its own lookup names, layout metadata, and gameplay semantics.
- `5K` / `7K` / `9K_Bms` / `9K_Pms` / `14K` must reuse one theme family but allow layout-sensitive per-lane rendering.
- `1P/2P` side flips must not require a second asset family; side-sensitive elements respond to runtime bindables/lookup metadata.
- Lane cover `Sudden / Hidden` states and lane-cover focus state are first-class skinnable elements, not debug overlays.
- Persistent gameplay feedback, including speed metrics and later `FAST/SLOW` / judge display / pacemaker data, must remain BMS-owned skinnable components instead of ad-hoc overlays hidden inside unrelated HUD elements. (Note: the always-on `DefaultBmsSpeedFeedbackDisplay` card that carried this family — FAST/SLOW, judge display, visual timing-offset, EX pacemaker, judgement summary, always-on GN — was removed 2026-06-15 by product decision; see P1-C. This constraint governs any future re-introduction. Live judgement *counts* are now served by the global `JudgementCounterDisplay`.)
- If BMS HUD composition needs more children than the current wrapped HUD + gauge bar + combo counter contract, extend it via a versioned optional interface or wrapper contract; do not break `IBmsHudLayoutDisplay` in place.
- Do not inject gameplay feedback widgets by crawling arbitrary wrapped HUD children or by overloading `GaugeBar` / `ComboCounter` with unrelated semantics.
- Results summary and clear lamp must remain separable skinnable components, so a skin can override the whole summary panel or only the lamp badge.
- Static art and the engine-owned BGA timeline are separate skin surfaces: the skin may present or frame them, but must not own BGA decoding, playback clocks or judgement state. Missing video capability must continue to degrade to the static-art chain.

### 13.7 Native Default Skin Removal Policy

The following must be removed from OMS's final default product surface during Phase 1.1:

- Upstream built-in skin selector entries used as OMS defaults
- Runtime fallback paths that silently land on osu!lazer-native built-in skins instead of OMS defaults
- Packaging assumptions that public OMS builds ship upstream default skin resources as the intended out-of-box experience

Permitted during transition:

- Minimal fake skins in tests
- Temporary internal compatibility shims while mania and BMS migrate
- Non-user-facing development references needed to keep the repo compiling during migration
- The current programmatic `OmsSkin` only until `oms-simple` has parity, integrity verification, atomic recovery and real-device gates

Not permitted after Phase 1.1 completion:

- Public OMS builds whose normal no-custom-skin experience is still Argon/Triangles/Legacy/Retro-derived
- BMS gameplay visuals that disappear or become blank when upstream default skin resources are removed
- Theme-specific programmatic fallback visuals below `oms-simple`; canonical-package failure must enter an explicit install-repair path

### 13.8 Implementation Rules

- All shipping mania/BMS gameplay elements must be backed by skin lookup + `oms-simple` file fallback, not by theme-specific direct-drawn primitives.
- New OMS gameplay UI should prefer `SkinnableDrawable` / `GlobalSkinnableContainerLookup` / dedicated lookup types over ruleset-local hard-coded layout.
- C3 layout is already the geometry authority. Final parity and C6/C7 work consume it without restoring hard-coded per-component geometry or creating another abstraction gate.
- Component defaults must be stable under serialization, replay, and skin reload.
- Layout-critical components such as lane covers, gauge bars, clear lamps, and note distribution panels must have predictable default sizes in the OMS built-in skin.
- Do not make BMS gameplay visuals depend on upstream mania component names or upstream built-in texture contracts.
- Reuse mania-compatible key names through a shared neutral codec; do not reuse `ManiaLegacySkinTransformer`, `Column`, `ManiaAction` or the legacy 480-coordinate renderer as BMS base classes.
- Extend dynamic visuals through the shared versioned scene/event contract and C6/C7 gates, with a real author caller, production consumer, publication/lease participation and budget proof. A fixture alone does not authorise private per-component provider/display expansion.
- User-distributed scripts are untrusted: no network, arbitrary filesystem, reflection, process/thread/native library, Realm/config writes or gameplay mutation. Enforce deterministic gameplay-clock time, seeded randomness, node/memory/time budgets and per-layer fault isolation.

### 13.9 Test Requirements

The skin system must ship with both non-visual and visual validation:

- Non-visual transformer tests for fallback order and component lookup routing.
- Ruleset tests proving mania and BMS no longer require upstream native default skins for no-custom-skin operation.
- Visual tests for lane layout, note readability, lane cover focus, judgement placement, gauge bar visibility, clear lamp rendering, and Song Select note distribution.
- Packaging checks confirming public builds do not expose upstream built-in skins as OMS defaults.
- Full layout matrix tests for 5K/7K four styles, 9K BMS/PMS and 14K, including lane roles/bounds, shared gauge/combo/BGA descriptor consumption and no timing drift.
- Tri-state `Provide/Inherit/Suppress`, legacy explicit-presence, G1 authority/containment/atomic reload, event ordering and sandbox capability/budget tests.
- `oms-simple.osk` and `oms-complex.osk`, each containing mania+BMS and using only public APIs; canonical fallback integrity/recovery and absence of a programmatic product-visual layer are release tests.

### 13.10 Phase Placement

- Core OMS Skin V1 is a **Phase 1.1** priority, not a deferred Phase 2 polish item.
- Phase 1.1 completes only when the shared external runtime, safe storage/reload, layout matrix, `oms-simple`/`oms-complex` proof, Authoring Kit and `oms-simple` canonical fallback are usable for mania and BMS, with theme-specific programmatic fallback removed from the product render chain. Additional scene nodes/events may extend in Phase 2 without changing the versioned V1 contract.

---

## 14. Phase 3 Private Server Integration (planned; no current `oms.Server` project)

This section is a frozen Phase 3 target contract. No `oms.Server` project, private-service base URL or default endpoint is implied until Phase 3 is explicitly activated.

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
- Local score metadata saved through `ScoreManager` / Realm; replay data uses the existing score file store
- No leaderboard data shown (replaced by "Offline" indicator)
- Beatmap download unavailable
- Difficulty table data uses last cached fetch
- All local gameplay fully functional

---

## 15. Development Phases

> 详细分步规划、验收标准与当前进度矩阵见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) 与 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)。
> 本节仅列出各阶段范围定义。

### Phase 1 — Core BMS

上游裁剪、BMS 解析 / 导入 / 键音 / 7K+1 playfield / 判定 / gauge / EX-SCORE / 结算 / 作者等级与星级展示 / 难度表 / MD5 匹配 / Song Select 表分组 / 音符分布图 / 静态 BG / Lane Cover / 输入绑定。

### Phase 1.1 — OMS Skin System

mania/BMS 共享外部 ini/asset/scene/event/script runtime、唯一 layout、三态组件解析、安全存储/reload、canonical 双包与 Authoring Kit、程序化主题渲染退出及 release gate。完成定义见 §13.10，当前未完成项见 P1-A PLAN。

### Phase 2 — BMS Feature Complete

能力范围包括判定/gauge/assist、完整 keymode 与 side、analog 输入、长条编码、BGA 和用户皮肤生态。其中已有能力可提前落地，但不因此改变 Phase 1 release gate；未完成项只以 PLAN/STATUS 为准。

### Phase 3 — Private Server

账号认证、成绩提交与排行榜、谱面搜索/下载、远程难度表镜像。

### Future Scope

真正随机分支选择、LR2IR 排行兼容、SHA256 + sabun linkage。现有固定分支兼容行为以 P1-K 为准；macOS / Linux 不在 OMS 产品计划内。

---

## 16. Coding Conventions

Follow osu!lazer's existing conventions throughout:

- Follow existing ruleset directory and naming conventions. Reuse shared runtime contracts; do not duplicate mania implementations or introduce abstractions without a current consumer.
- Use `Bindable<T>` for all configurable values
- Use `DependencyContainer` / `[Resolved]` attribute for dependency injection — no static singletons
- Async I/O for all file and network operations (`async`/`await`, never `.Result`)
- `IResourceStore<byte[]>` for asset loading
- All timing values in **milliseconds (double)** unless explicitly noted as beats
- Write XML doc comments on all public API surface in future private-server integration code and `oms.Input`
- Unit test coverage required for: `BmsBeatmapDecoder`, `BmsTimingWindows`, `BmsScoreProcessor`, `BmsGaugeProcessor`, `BmsDifficultyCalculator`, `BmsNoteDistributionAnalyzer`, `BmsTableMd5Index`, `BmsDifficultyTableManager`
- Any development, research, bug fix, validation pass, or release-gate adjustment that changes plan, status, constraints, or verified conclusions must update the corresponding `doc_md/mainline`, `doc_md/subline`, `doc_md/other`, documents in the same change. Do not leave code and documentation drifting.

---

## 17. Key Dependencies

| Package | Purpose |
|---|---|
| `ppy.osu-framework` | Core game loop, rendering, input base |
| `ManagedBass` | Audio engine (keysound playback) |
| `SharpCompress` | BMS archive extraction (zip/rar/7z) |
| `HidSharp` | Non-Windows / diagnostic HID enumeration and reading |
| `Vortice.DirectInput` | Windows default HID/gamepad enumeration and polling |
| `Realm` | Local beatmap, skin and score metadata |
| `Microsoft.Data.Sqlite` | BMS difficulty-table `tables.db` |
| `Ude.NetStandard` | Charset detection (Shift-JIS / UTF-8) |

---

## 18. Upstream Sync Policy

The upstream osu!lazer commit this fork is based on is recorded in [UPSTREAM.md](../other/UPSTREAM.md).

- Do **not** blindly pull upstream changes
- When a critical bug fix or performance improvement lands upstream, cherry-pick selectively
- Before cherry-picking, verify the change does not conflict with BMS ruleset additions or removed rulesets
- Re-evaluate upstream sync every 3 months

---

*This document indexes stable product and architecture constraints. Cross-Agent workflow rules come only from [AGENTS.md](../../AGENTS.md); current state and execution order come from STATUS/PLAN.*
