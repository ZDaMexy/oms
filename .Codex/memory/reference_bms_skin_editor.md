---
name: reference_bms_skin_editor
description: "Skin Layout Editor chain in OMS — what it can edit, the BMS/mania code-provider collision, why \"build skin from assets\" is unsupported, the governance blank, and the Activator landmine"
metadata: 
  node_type: memory
  type: reference
---

OMS's "skin editor" = upstream lazer **Skin Layout Editor** (`osu.Game/Overlays/SkinEditor/`), essentially untouched. Entry: `Ctrl+Shift+S` (`GlobalAction.ToggleSkinEditor`) or Settings → Skin → "Skin Layout Editor". Reviewed 2026-06-15.

**What it actually edits:** ONLY `SkinnableContainer` targets — `MainHUDComponents`, ruleset HUD layer, `Playfield` (an overlay layer tracking the playfield quad, NOT the playfield itself), `SongSelect`, `Results`. Within those it drag-places `ISerialisableDrawable` components, edits their `[SettingSource]` props, and serialises per-target layout JSON into the skin. Placeable list = `SerialisedDrawableInfo.GetAllAvailableDrawables(ruleset)` = reflection over `public ISerialisableDrawable` types in the ruleset assembly.

**THE core collision (why it can't touch BMS/mania visuals):** OMS's visual identity uses models the editor can't see —
- BMS = **code-provider** (`BmsSkinTransformer` lookups `BmsNoteSkinLookup`/`BmsLaneSkinLookup`/… instantiated INSIDE the playfield, not in a `SkinnableContainer`). See [[reference_bms_default_skin_geometry]].
- mania = **OMS-preset transformer** (`ManiaOmsSkinTransformer`, `OmsMania*Preset`).
- Neither flows through `ISerialisableDrawable`, so notes/lanes/gauge/judgement/BGA are **invisible to the editor**. Only the global HUD overlay + the BMS HUD layer (`BmsSkinTransformer` intercepts `GlobalSkinnableContainerLookup(MainHUDComponents, bms)` to wrap gauge/combo — fixed-anchor, see `BmsHudLayoutDisplay`) are editable.

**"Build skin from built-in assets" = UNSUPPORTED:** only asset path is drag an external image → `SkinnableSprite`; its `SpriteSelectorControl` lists files **already imported into the user skin**, NOT built-in OMS assets; no asset browser/palette. Also almost no BMS/mania skin component carries `[SettingSource]` (those are all on Mods), so even "editable" components expose ~no config knobs.

**Governance blank:** SKINNING.md / P1-A do NOT cover the editor; "user-skin ecosystem" is Phase 2 deferred; authoring contract NOT frozen. Three paths to the "skin from scratch" goal: A=layout-only (add `[SettingSource]`, reuses upstream, never reaches full authoring), B=config-driven transformers, C=asset-composition. B/C blocked by the un-frozen lookup/preset/asset-naming contract. Documented this gap in P1-A.

**Activator landmine (fixed 2026-06-15):** any `public ISerialisableDrawable` **without a public parameterless constructor** breaks the editor — `Activator.CreateInstance(type)` throws `MissingMethodException` in BOTH the toolbox (`SkinComponentToolbox.attemptAddComponent`) and layout reload (`SerialisedDrawableInfo.CreateInstance`). An all-optional-param ctor does NOT count as parameterless. Guard added: `GetAllAvailableDrawables` now filters to types with `GetConstructor(Type.EmptyTypes) != null`. (The original offender `DefaultBmsSpeedFeedbackDisplay` was later deleted entirely — see [[reference_bms_judgement_parity]].)
