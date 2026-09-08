---
name: reference_bms_skin_editor
description: "Legacy Skin Layout Editor HUD-only boundary、当前UI/backend稳定不可用、非Skin V1 ABI及未来重开时的Activator地雷"
metadata: 
  node_type: memory
  type: reference
---

OMS's historical "skin editor" = upstream lazer **Skin Layout Editor** (`osu.Game/Overlays/SkinEditor/`). Historical entry was `Ctrl+Shift+S` (`GlobalAction.ToggleSkinEditor`) or Settings → Skin → "Skin Layout Editor". As of 2026-08-24, `SkinAuthoringAvailability.LegacyEditorAvailable=false`; menu, hotkey and overlay are not product-reachable.

**历史可编辑面（当前禁用）：** 仅`SkinnableContainer` targets，包括`MainHUDComponents`、ruleset HUD、跟随playfield quad的overlay（不是playfield本身）、`SongSelect`与`Results`。历史工具拖放`ISerialisableDrawable`、修改`[SettingSource]`属性并保存per-target layout JSON；候选由`SerialisedDrawableInfo.GetAllAvailableDrawables(ruleset)`反射取得。

**与当前Skin V1的边界：** 早期BMS code-provider与mania OMS-preset transformer没有通过`ISerialisableDrawable`公开完整ruleset内部视觉；C3～C5现已建立另一条真实production链：exact layout/material/prepared scene publication→BMS/mania runtime host。旧editor既不消费这条scene graph，也不能修改其revision或资源authority；不能继续把两ruleset的当前作者能力概括为“只有C# provider/preset”。迁移期仍保留的`BmsSkinTransformer`/`ManiaOmsSkinTransformer`也不等于恢复editor。几何召回见[[reference_bms_default_skin_geometry]]。

**历史素材选择边界：** 旧工具不支持从built-in素材生成皮肤；当时的路径是拖入外部图片→`SkinnableSprite`，`SpriteSelectorControl`只列出用户皮肤已导入的文件，没有OMS内置素材浏览器/面板。这不构成当前可用的authoring工具。

**当前治理定位：** editor是稳定禁用的legacy HUD/layout工具，不是scene、资源、author-preview或reload ABI。Folder Skin Workspace是可达的C1目录管理面；Settings current manual Reload是C2唯一reload入口，external-edit/update-import及base/interface dispatch在mount/store/current pair变化前拒绝。`.osk`/`skin.ini` compatibility与C5 declarative scene/animation/event已实现；sandbox/script及最终Authoring Kit分别留C6/C7。当前能力以P1-A STATUS与SKINNING页首为准。

**Activator landmine (fixed 2026-06-15, relevant only if legacy editor is ever re-opened):** any `public ISerialisableDrawable` **without a public parameterless constructor** breaks the editor — `Activator.CreateInstance(type)` throws `MissingMethodException` in BOTH the toolbox (`SkinComponentToolbox.attemptAddComponent`) and layout reload (`SerialisedDrawableInfo.CreateInstance`). An all-optional-param ctor does NOT count as parameterless. Guard added: `GetAllAvailableDrawables` now filters to types with `GetConstructor(Type.EmptyTypes) != null`. (The original offender `DefaultBmsSpeedFeedbackDisplay` was later deleted entirely — see [[reference_bms_judgement_parity]].)
