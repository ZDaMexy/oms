# OMS

[简体中文](README.md) | **English** | [日本語](README.ja.md)

> A Windows rhythm-game client for BMS and osu!mania — offline-first and portable, no installation required.

[![Website](https://img.shields.io/badge/website-oms.zdamexy.work-FF6B35)](https://oms.zdamexy.work/)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-0078D6)
![Runtime](https://img.shields.io/badge/.NET-8.0-512BD4)
![License](https://img.shields.io/badge/license-MIT-green)

OMS starts from [osu!lazer](https://github.com/ppy/osu), removes osu!, Taiko and Catch, and brings **BMS** and **osu!mania** together into a single, more modern client: offline-first, portable, with direct-read local chart import. Judgement, scoring, gauges and speed semantics are aligned with IIDX / LR2 / beatoraja, so players familiar with those platforms will feel at home quickly. Learn more on the official site: [oms.zdamexy.work](https://oms.zdamexy.work/).

## Table of Contents

- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Usage](#usage)
  - [Offline-first](#offline-first)
  - [BGA playback](#bga-playback)
- [Building from source](#building-from-source)
- [Documentation](#documentation)
- [Project status](#project-status)
- [Contributing](#contributing)
- [License](#license)
- [Acknowledgements](#acknowledgements)

## Features

- **Two modes** — osu!mania and BMS, covering 5 / 7 / 9 / 14K.
- **Judgement & scoring** — four judgement systems, EX / DJ scoring with backlight (lamp) feedback.
- **Multiple gauges** — ASSIST EASY / EASY / NORMAL / HARD / EX-HARD / HAZARD / GAS, switchable across the OMS LEGACY, beatoraja, LR2 and IIDX rule families so the clear feel matches the platform you know.
- **BGA playback** — static backgrounds, image and video BGA, and the POOR layer, with window size and position configurable by skins; legacy video formats can also play with ffmpeg (see [Usage](#bga-playback)).
- **Training & assist mods** — Mirror / Random (including R-RANDOM / S-RANDOM and custom patterns), Auto Scratch / Auto Note and other practice-oriented mods.
- **Input integration** — keyboard, XInput, Raw Input and DirectInput/HID software paths are connected; real-device coverage, analog scratch and calibration still require validation.
- **BMS difficulty tables** — import from local directories and public URL sources, MD5 matching, browse grouped by table.
- **Portable distribution** — installation-free full package with a relocatable data root.

## Requirements

- Windows 10 22H2 or later
- Built on .NET 8 / DesktopGL / osu-framework

## Installation

Download the latest portable full package `oms_YYYYMMDD.zip` from [GitHub Releases](https://github.com/ZDaMexy/oms/releases), extract it, and run directly — no installation required.

To update, close the game, extract the full new package into a separate directory, then run its `Update-OMS.ps1` and specify the existing installation directory. The updater preserves the existing portable/non-portable mode, user data and custom storage location, and retains the replaced program files. See the [release guide](doc_md/other/RELEASE.md) for details. In-game online auto-update is disabled by default.

## Usage

Charts are read directly from the filesystem: put BMS charts in `chartbms/` and mania charts in `chartmania/` — no conversion to `.osz` is needed. You can also register and scan multiple external / internal library roots in Settings → Maintenance.

### Offline-first

OMS keeps its core gameplay, libraries and user-data paths offline by default. Until Phase 3, private OMS services stay disabled and default endpoints stay empty. Accounts, online leaderboards, OMS / osu!mania official downloads, news / chat, multiplayer and spectator features are hidden or disabled by default.

In-game Browse supports the authorised public BMS sources [Ginger Rush](https://gingerrush.com/) and [616 / Alvorna](https://616.sb/bms/download). Search, filter by difficulty table, select a chart and download its package, then open the installed chart in song select. Downloads continue when the browser closes; failed tasks can be retried manually. Music previews use local song select after import. Sources are contacted only when Browse opens, without an OMS account. Missing packages and unsupported targets cannot report a successful import. See the [download contract](doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#第三方-bms-浏览下载).

**BMS difficulty tables** also support import / refresh from local paths and public URLs, independently of any private OMS server.

### BGA playback

During BMS play, skins can position and size up to 16 BGA windows, using Fit, Fill or Stretch, or disable the windows while retaining separate information panels. All windows share the chart's presentation, and rebuilding the display layout preserves playback progress. Older skins without an explicit window declaration retain automatic placement: usually right for 1P, left for 2P, right for a centred playfield, and four corners for 14K, with adjustments when space is limited. Explicitly declared windows that overlap protected gameplay or HUD areas are hidden as a group rather than repositioned. See the [complete skinning manual](skin-authoring/docs/SKINNING.md) for instructions and examples (in Chinese).

Static backgrounds, image BGA, the POOR layer and `.mp4` video work directly, with a blurred version of the chart background shown full-screen. "Show BGA" in the BMS settings turns the windows off. BGA currently applies to native BMS only; BMS charts converted to mania do not include it.

Legacy video formats (`.mpg`, `.wmv`, `.avi`, `.flv`) cannot be decoded by the built-in player and show a static image by default. To play them you need an ffmpeg binary:

- Install it on the system PATH: `winget install ffmpeg` (restart OMS once if it is already running), or
- Download [ffmpeg](https://www.gyan.dev/ffmpeg/builds/) and place `bin\ffmpeg.exe` in the OMS program directory (next to `osu!.exe`) or the data directory (default `%APPDATA%\oms`).

Then keep "ffmpeg完整BGA支持" (full BGA support via ffmpeg) enabled in the BMS settings. On first entry, loading waits for up to about eight seconds: a timely transcode starts the video from the beginning, while a timeout keeps the static image until the video is ready. `bga-video-cache\` is reused only within the current process session; restarting OMS clears it and retranscodes on demand.

## Building from source

You need the [.NET 8 SDK](https://dotnet.microsoft.com/download) and one of Visual Studio, JetBrains Rider or Visual Studio Code. Prefer opening `osu.Desktop.slnf`.

```shell
# Clone
git clone https://github.com/ZDaMexy/oms.git
cd oms

# Build
dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m

# Run
dotnet run --project osu.Desktop

# Run the BMS tests
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore
```

## Documentation

The full product boundaries, development plan, current status and technical constraints all live under [`doc_md/`](doc_md/README.md):

- [Product constraints & release gate](doc_md/mainline/OMS_COPILOT.md)
- [Development plan](doc_md/mainline/DEVELOPMENT_PLAN.md)
- [Current status & open issues](doc_md/mainline/DEVELOPMENT_STATUS.md)
- [Changelog](doc_md/mainline/CHANGELOG.md)

Repository navigation and the "code changes must update the docs" discipline are described in [AGENTS.md](AGENTS.md); `CLAUDE.md` is only a compatibility redirect.

## Project status

OMS is in the closing work of **Phase 1.x** (local BMS / mania and skinning). “静线” (oms-simple) is the sole built-in, default and fallback skin for both modes. Normal development launches, builds and publishing include updates from its author source; players do not need to import it. “星轨” (oms-complex) has been retired from the built-in selection, and its repository files remain historical references. Authors can edit templates, check, package and import their own work using the [authoring kit](skin-authoring/README.md). Settings save separate BMS and mania skin choices and let players open and refresh the fixed `chartskin` folder. The restored component layout editor supports component and property changes, image imports and saving a separate copy. 静线's recent layout, information and lane-proportion improvements are in place; further visual polish is on hold following the user's decision. Visual, hardware and long-session acceptance remain incomplete; Skin V1 and the overall release are not complete. Phase 3 online features remain frozen. See [DEVELOPMENT_STATUS.md](doc_md/mainline/DEVELOPMENT_STATUS.md) for authoritative progress and acceptance status.

## Contributing

Feedback via [Issues](https://github.com/ZDaMexy/oms/issues) and Pull Requests are welcome. Before submitting code, please note:

- Prefer building with `osu.Desktop.slnf`; Release must be error-free and introduce no unexplained warnings. See [DEVELOPMENT_STATUS.md](doc_md/mainline/DEVELOPMENT_STATUS.md) for the current known-warning baseline.
- Run `osu.Game.Rulesets.Bms.Tests` when changing BMS-related logic.
- Any change that alters a plan, status, constraint or verification conclusion must update the corresponding governance doc under [`doc_md/`](doc_md/README.md) **in the same commit**, then run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\CheckDocumentation.ps1` and `git diff --check` (see [AGENTS.md](AGENTS.md)).

## License

This project is licensed under the [MIT License](LICENCE), inherited from upstream osu!lazer.

OMS is a directed fork of osu!lazer; its goals and content have diverged noticeably from upstream, and it is not a mirror or alternative release source of [`ppy/osu`](https://github.com/ppy/osu).

## Acknowledgements

- [osu!lazer](https://github.com/ppy/osu) and [osu-framework](https://github.com/ppy/osu-framework) — the upstream foundation of OMS.
- IIDX, LR2 and beatoraja — direction references for judgement, gauge and speed semantics.
