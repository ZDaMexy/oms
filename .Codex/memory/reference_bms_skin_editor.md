---
name: reference_bms_skin_editor
description: "Legacy Skin Layout Editor HUD-only boundary、当前UI/backend稳定不可用、非Skin V1 ABI及未来重开时的Activator地雷"
metadata: 
  node_type: memory
  type: reference
---

OMS's historical "skin editor" = upstream lazer **Skin Layout Editor** (`osu.Game/Overlays/SkinEditor/`). Historical entry was `Ctrl+Shift+S` (`GlobalAction.ToggleSkinEditor`) or Settings → Skin → "Skin Layout Editor". As of 2026-08-24, `SkinAuthoringAvailability.LegacyEditorAvailable=false`; menu, hotkey and overlay are not product-reachable.

**What it actually edits:** ONLY `SkinnableContainer` targets — `MainHUDComponents`, ruleset HUD layer, `Playfield` (an overlay layer tracking the playfield quad, NOT the playfield itself), `SongSelect`, `Results`. Within those it drag-places `ISerialisableDrawable` components, edits their `[SettingSource]` props, and serialises per-target layout JSON into the skin. Placeable list = `SerialisedDrawableInfo.GetAllAvailableDrawables(ruleset)` = reflection over `public ISerialisableDrawable` types in the ruleset assembly.

**THE core collision (why it can't touch BMS/mania visuals):** OMS's visual identity uses models the editor can't see —
- BMS = **code-provider** (`BmsSkinTransformer` lookups `BmsNoteSkinLookup`/`BmsLaneSkinLookup`/… instantiated INSIDE the playfield, not in a `SkinnableContainer`). See [[reference_bms_default_skin_geometry]].
- mania = **OMS-preset transformer** (`ManiaOmsSkinTransformer`, `OmsMania*Preset`).
- Neither flows through `ISerialisableDrawable`, so notes/lanes/gauge/judgement/BGA are **invisible to the editor**. Only the global HUD overlay + the BMS HUD layer (`BmsSkinTransformer` intercepts `GlobalSkinnableContainerLookup(MainHUDComponents, bms)` to wrap gauge/combo — fixed-anchor, see `BmsHudLayoutDisplay`) are editable.

**"Build skin from built-in assets" = UNSUPPORTED:** only asset path is drag an external image → `SkinnableSprite`; its `SpriteSelectorControl` lists files **already imported into the user skin**, NOT built-in OMS assets; no asset browser/palette. Also almost no BMS/mania skin component carries `[SettingSource]` (those are all on Mods), so even "editable" components expose ~no config knobs.

**当前治理定位：** 该editor是稳定禁用的legacy HUD/layout-only工具面，不是Skin V1 scene、资源、author-preview或reload ABI，也不能承担“从素材制作完整BMS/mania皮肤”。Folder Skin Workspace是独立可达的C1目录管理面；Settings的`Reload current skin`是独立C2入口，二者都不能被legacy editor绕过。`ExternalEditOverlay`、manager/importer的external-edit与update-import以及base/interface dispatch均在mount/store/current pair变化前稳定拒绝。P1-A/SKINNING定义的`.osk`/`skin.ini` compatibility、declarative scene/animation、可选sandbox script与最终Authoring Kit是目标作者面/约束，不代表后续能力当前已实现；实时能力只看SKINNING页首与P1-A STATUS。

**Activator landmine (fixed 2026-06-15, relevant only if legacy editor is ever re-opened):** any `public ISerialisableDrawable` **without a public parameterless constructor** breaks the editor — `Activator.CreateInstance(type)` throws `MissingMethodException` in BOTH the toolbox (`SkinComponentToolbox.attemptAddComponent`) and layout reload (`SerialisedDrawableInfo.CreateInstance`). An all-optional-param ctor does NOT count as parameterless. Guard added: `GetAllAvailableDrawables` now filters to types with `GetConstructor(Type.EmptyTypes) != null`. (The original offender `DefaultBmsSpeedFeedbackDisplay` was later deleted entirely — see [[reference_bms_judgement_parity]].)
