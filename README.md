# OMS

**简体中文** | [English](README.en.md) | [日本語](README.ja.md)

> 面向 BMS 与 osu!mania 的 Windows 音游客户端，离线优先、免安装便携。

[![官网](https://img.shields.io/badge/website-oms.zdamexy.work-FF6B35)](https://oms.zdamexy.work/)
![平台](https://img.shields.io/badge/platform-Windows%2010%2B-0078D6)
![运行时](https://img.shields.io/badge/.NET-8.0-512BD4)
![许可证](https://img.shields.io/badge/license-MIT-green)

OMS 从 [osu!lazer](https://github.com/ppy/osu) 出发，移除了 osu!、Taiko、Catch，把 **BMS** 与 **osu!mania** 收进同一个更现代的客户端：离线优先、可便携、支持本地谱面直读导入。判定、计分、Gauge 与速度语义对齐 IIDX / LR2 / beatoraja，熟悉这些平台的玩家可以很快上手。更多信息见项目官网 [oms.zdamexy.work](https://oms.zdamexy.work/)。

## 目录

- [特性](#特性)
- [系统要求](#系统要求)
- [安装](#安装)
- [使用](#使用)
  - [离线优先](#离线优先)
  - [BGA 背景演出](#bga-背景演出)
- [从源码构建](#从源码构建)
- [文档](#文档)
- [项目状态](#项目状态)
- [贡献](#贡献)
- [许可证](#许可证)
- [致谢](#致谢)

## 特性

- **两种模式** —— osu!mania 与 BMS，覆盖 5 / 7 / 9 / 14K。
- **判定与计分** —— 四套判定体系，EX / DJ 计分与背光（灯）反馈。
- **多种 Gauge** —— ASSIST EASY / EASY / NORMAL / HARD / EX-HARD / HAZARD / GAS，并可在 OMS LEGACY、beatoraja、LR2、IIDX 等规则族之间切换，让 clear 手感贴近你熟悉的平台。
- **BGA 背景演出** —— 静态背景、图片与视频 BGA、POOR 层，独立浮窗按布局靠边；老式视频格式配 ffmpeg 也能播（见[使用](#bga-背景演出)）。
- **训练与辅助 Mod** —— Mirror / Random（含 R-RANDOM / S-RANDOM 与自定义 pattern）、Auto Scratch / Auto Note 等面向练习的 mod。
- **广泛输入支持** —— 键盘、XInput 手柄、Raw Input、HID / DirectInput 控制器。
- **BMS 难度表** —— 本地目录与公共 URL 在线源导入、MD5 匹配、按表分组浏览。
- **便携发布** —— 免安装全量包，数据根目录可迁移。

## 系统要求

- Windows 10 22H2 或更高版本
- 基于 .NET 8 / DesktopGL / osu-framework

## 安装

前往 [GitHub Releases](https://github.com/ZDaMexy/oms/releases) 下载最新的便携全量包 `oms_YYYYMMDD.zip`，解压后直接运行即可，无需安装。

更新时下载新包覆盖旧目录，并保留 `portable.ini`、便携模式下的 `data/` 以及任何自定义数据根使用的 `storage.ini`。游戏内的在线自动更新默认关闭。

## 使用

谱面采用文件系统直读：BMS 谱面放入 `chartbms/`、mania 谱面放入 `chartmania/`，无需转换为 `.osz`。也可在 Settings → Maintenance 中注册多个外部 / 内部谱库根目录并扫描导入。

### 离线优先

OMS 目前完全离线运行。账号、在线排行榜、谱面下载、新闻 / 聊天、多人与观战等联网功能默认隐藏或禁用，计划在后续阶段逐步开放。

唯一的例外是 **BMS 难度表**：已支持本地路径与公共 URL 的导入 / 刷新，不依赖任何 OMS 私有服务器。

### BGA 背景演出

BMS 游玩时，BGA 显示在 playfield 旁的浮窗里，按布局靠边（1P 右、2P 左、居中右、14K 居中）。静态背景、图片 BGA、POOR 层和 `.mp4` 视频都直接可用，全屏背景为谱面背景图的模糊版。BMS 设置里的「显示 BGA」可关闭整个浮窗。

老式视频格式（`.mpg`、`.wmv`、`.avi`、`.flv`）内置播放器无法解码，默认显示静态图。要播放它们需配一份 ffmpeg：

- 装到系统 PATH：`winget install ffmpeg`（OMS 已开着则重开一次），或
- 下载 [ffmpeg](https://www.gyan.dev/ffmpeg/builds/)，把 `bin\ffmpeg.exe` 放到 OMS 程序目录（`osu!.exe` 旁）或数据目录（默认 `%APPDATA%\oms`）。

随后保持 BMS 设置里「转码无法解码的 BGA 视频」开启。首次进入这类谱面会在后台转码，转好前显示静态图、转好后切到视频，结果缓存在数据目录的 `bga-video-cache\`，再进即直接播放。

## 从源码构建

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download)，以及 Visual Studio、JetBrains Rider 或 Visual Studio Code 之一。优先打开 `osu.Desktop.slnf`。

```shell
# 克隆
git clone https://github.com/ZDaMexy/oms.git
cd oms

# 构建
dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m

# 运行
dotnet run --project osu.Desktop

# 运行 BMS 测试
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore
```

## 文档

完整的产品边界、开发计划、当前状态与技术约束都收口在 [`doc_md/`](doc_md/README.md)：

- [产品约束与 release gate](doc_md/mainline/OMS_COPILOT.md)
- [开发计划](doc_md/mainline/DEVELOPMENT_PLAN.md)
- [当前状态与遗留问题](doc_md/mainline/DEVELOPMENT_STATUS.md)
- [变更日志](doc_md/mainline/CHANGELOG.md)

仓库导航与「改代码必须同步改文档」的联动约定见 [CLAUDE.md](CLAUDE.md)。

## 项目状态

OMS 处于 **Phase 1**（本地 BMS / mania 主流程）收尾阶段，当前正在进行皮肤系统专项与输入硬件验收。联网相关的 Phase 3 功能在此之前保持冻结。最新进度以 [DEVELOPMENT_STATUS.md](doc_md/mainline/DEVELOPMENT_STATUS.md) 为准。

## 贡献

欢迎通过 [Issue](https://github.com/ZDaMexy/oms/issues) 反馈问题，或提交 Pull Request。提交代码前请注意：

- 优先打开 `osu.Desktop.slnf` 构建，确保 Release 构建零警告零错误。
- 改动 BMS 相关逻辑时请运行 `osu.Game.Rulesets.Bms.Tests`。
- 任何改变计划、状态、约束或验证结论的改动，必须在**同一次提交**中同步更新 [`doc_md/`](doc_md/README.md) 中对应的治理文档（详见 [CLAUDE.md](CLAUDE.md)）。

## 许可证

本项目采用 [MIT 许可证](LICENCE)，继承自上游 osu!lazer。

OMS 是 osu!lazer 的定向分支，项目目标与内容已与上游明显分化，并非 [`ppy/osu`](https://github.com/ppy/osu) 的镜像或替代发布源。

## 致谢

- [osu!lazer](https://github.com/ppy/osu) 与 [osu-framework](https://github.com/ppy/osu-framework) —— OMS 的上游基础。
- IIDX、LR2、beatoraja —— 判定、Gauge 与速度语义的方向校准来源。
</content>
</invoke>
