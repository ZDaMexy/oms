---
name: project-oms-overview
description: "What OMS is — a Windows-only BMS + osu!mania rhythm game client forked from osu!lazer; scope, architecture, and current phase"
metadata: 
  node_type: memory
  type: project
  originSessionId: d9f40bda-cfd5-4076-8bbf-1f08a85c9e5c
---

OMS is a Windows-only (Win10 22H2+) rhythm game client forked from osu!lazer, targeting .NET 8 / DesktopGL / osu-framework. It keeps only **osu!mania** and adds a first-class **BMS** mode; Osu/Taiko/Catch are fully deleted (do not reference or re-add them). Goal: replace LR2 and beatoraja as a modern BMS player.

**Why:** Offline-first BMS/mania client; long-term (Phase 3) goal is private-server integration (accounts, leaderboards, downloads), but all networked features are hidden/disabled until then.

**How to apply:**
- Current phase: **Phase 1.1 skin recovery baseline** + public-release product surface wrap-up + 1.17 input hardware/semantics acceptance. Before any skin work, read `reference_skin_recovery_20260710.md`; G1 production/F2/Lua are not current capabilities. Phase 2/3 are frozen.
- Release model: portable full packages `oms_YYYYMMDD(.zip)` + manual file-overwrite updates. In-game online update disabled. Don't ship auto-update or online endpoints.
- Default endpoints are empty; online code in-tree is Phase 3 reserve, not user-facing.
- Build: `dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m`. BMS tests: `dotnet test osu.Game.Rulesets.Bms.Tests/...`.
- Key projects: `osu.Game` (core), `osu.Game.Rulesets.Mania`, `osu.Game.Rulesets.Bms` (primary dev target, ~167 files), `oms.Input` (unified input abstraction), `osu.Desktop` (entry).
- Data root: release `%APPDATA%/oms/`, debug `%APPDATA%/oms-development/`; `portable.ini`→`data/`; `storage.ini` can redirect. BMS charts in `chartbms/`, mania in `chartmania/` (filesystem direct-read, NOT routed through hash-backed `files/` store).

See [[project-oms-docs-governance]] for the mandatory doc-update discipline.
