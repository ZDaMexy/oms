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
- **BGA 背景演出** —— 静态背景、图片与视频 BGA、POOR 层，窗口尺寸和位置可由皮肤设置；老式视频格式配 ffmpeg 也能播（见[使用](#bga-背景演出)）。
- **训练与辅助 Mod** —— Mirror / Random（含 R-RANDOM / S-RANDOM 与自定义 pattern）、Auto Scratch / Auto Note 等面向练习的 mod。
- **输入接入** —— 已接入键盘、XInput、Raw Input 与 DirectInput/HID 软件路径；真实控制器覆盖、模拟皿与校准仍待验收。
- **BMS 难度表** —— 本地目录与公共 URL 在线源导入、MD5 匹配、按表分组浏览。
- **便携发布** —— 免安装全量包，数据根目录可迁移。

## 系统要求

- Windows 10 22H2 或更高版本
- 基于 .NET 8 / DesktopGL / osu-framework

## 安装

前往 [GitHub Releases](https://github.com/ZDaMexy/oms/releases) 下载最新的便携全量包 `oms_YYYYMMDD.zip`，解压后直接运行即可，无需安装。

更新时先关闭游戏，将完整新包解压到另一个目录，再运行新包内的 `Update-OMS.ps1`，按提示指定旧安装目录。更新工具保留原便携模式、用户数据和自定义保存位置，并保留替换前的程序文件；具体步骤见[发行说明](doc_md/other/RELEASE.md)。游戏内的在线自动更新默认关闭。

## 使用

谱面采用文件系统直读：BMS 谱面放入 `chartbms/`、mania 谱面放入 `chartmania/`，无需转换为 `.osz`。也可在 Settings → Maintenance 中注册多个外部 / 内部谱库根目录并扫描导入。

### 离线优先

OMS 的核心玩法、谱库与用户数据链默认离线运行，默认服务地址为空。原官网账号、OMS / osu!mania 官方谱面下载、新闻 / 聊天、多人与观战等功能继续隐藏或禁用。

独立 OMS IR 已进入主动连接试运行：从工具栏奖杯入口填写 `https://oms.zdamexy.work`，启用并注册 / 登录后，支持的正常完整新局在本地保存后交分，可在端内和网页查榜、查看本人记录及恢复待交。关闭 IR 仍可离线游玩；真实手动游玩、断网重启与账号归属验收尚未完成，多来源和 LR2 历史榜扩展仍待正式审查。当前范围见 [P3-IR 状态](doc_md/subline/P3-IR/DEVELOPMENT_STATUS.md)。

游戏内“浏览”支持 [Ginger Rush](https://gingerrush.com/) 和 [616 / Alvorna](https://616.sb/bms/download) 公共 BMS 源：依次选择下载源、难度表和表内等级，可结合关键词查找。在紧凑歌曲卡片中展开并选择谱面，点击下载图标获取资源包，入库后直接在选歌中打开；图标悬停显示操作提示。难度表暂不可用时会保留提示，点击搜索图标或回车可重试并恢复表和等级选择。关闭页面后下载继续，失败可手动重试；音乐试听使用已入库谱面的本地选歌试听。首次打开才访问来源，不要求 OMS 账号；来源没有资源包、包损坏或目标不受当前解码器支持时不会显示下载成功。详见 [下载合同](doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#第三方-bms-浏览下载)。

在同一浏览页切换到 osu!mania，可从 [Sayobot 镜像](https://osu.sayobot.cn/) 按曲名、作者或原谱面集编号查找，并选择键数、星级范围和收录状态。展开卡片选择具体难度，下载无视频原包后自动加入 `chartmania`，完成图标或通知可准确打开所选难度；混合包只加入 mania。切换分区或关闭页面后下载继续，进入 mania 分区前不访问镜像，无需官网账号。星级来自镜像资料，试听沿本地选歌；不含官网下载、独立在线试听或续传。详见 [mania 下载合同](doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#sayobot-mania-浏览下载)。

最近一次留存的实站验收（2026-10-01）在包下载的安全连接处失败，实网包成功入库仍待补验；软件与素材窗口已有通过记录。这是当时的网络结果，不代表节点持续故障。当前进度见 [P1-A 状态](doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)，当时证据见 [验证记录](doc_md/other/MANIA_SAYOBOT_DOWNLOAD_20261001.md)。

此外，**BMS 难度表**支持本地路径与公共 URL 的导入 / 刷新，同样不依赖任何 OMS 私有服务器。

### BGA 背景演出

BMS 游玩时，皮肤可设置最多 16 个 BGA 窗口的位置、大小和完整适配／居中裁切／拉伸方式，也可关闭窗口并保留独立信息区。所有窗口共享谱面演出，更换显示布局保留播放进度。未声明窗口的旧皮肤继续使用自动布局：通常 1P 右侧、2P 左侧、居中右侧，14K 四角，空间不足时调整位置和轨道区域；明确声明的窗口若遮挡轨道、HUD 等安全区域，则整组不显示，不自动搬位。制作说明与例子见[完整皮肤手册](skin-authoring/docs/SKINNING.md)。

静态背景、图片 BGA、POOR 层和 `.mp4` 视频都直接可用，全屏背景为谱面背景图的模糊版。BMS 设置里的「显示 BGA」可关闭 BGA 窗口。BGA 当前仅用于原生 BMS，BMS 转谱后的 mania 不提供此演出。

老式视频格式（`.mpg`、`.wmv`、`.avi`、`.flv`）内置播放器无法解码，默认显示静态图。要播放它们需配一份 ffmpeg：

- 装到系统 PATH：`winget install ffmpeg`（OMS 已开着则重开一次），或
- 下载 [ffmpeg](https://www.gyan.dev/ffmpeg/builds/)，把 `bin\ffmpeg.exe` 放到 OMS 程序目录（`osu!.exe` 旁）或数据目录（默认 `%APPDATA%\oms`）。

随后保持 BMS 设置里「ffmpeg完整BGA支持」开启。首次进入这类谱面时，加载流程最多等待约 8 秒；及时完成即可从头播放视频，超时则先显示静态图、转好后再切换。`bga-video-cache\` 只在当前进程会话内复用，重启 OMS 后会清理并按需重新转码。

## 从源码构建

需要 [.NET 8 SDK](https://dotnet.microsoft.com/download)，以及 Visual Studio、JetBrains Rider 或 Visual Studio Code 之一。优先打开 `osu.Desktop.slnf`。

在非系统盘目录克隆；每个新 PowerShell 在构建、测试、打包或开发检查前加载开发存储入口。首次切换缓存后重新 restore 对应工程，不复用旧 assets；完整约定见[开发磁盘约束](AGENTS.md#开发磁盘约束)。

```powershell
# 克隆
git clone https://github.com/ZDaMexy/oms.git
cd oms

# 当前 PowerShell 及其子进程使用仓库旁的开发缓存
. .\UseDevelopmentStorage.ps1

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

仓库导航与「改代码必须同步改文档」的联动约定见 [AGENTS.md](AGENTS.md)；`CLAUDE.md` 仅是兼容跳转。

## 项目状态

OMS 处于 **Phase 1.x**（本地 BMS / mania 主流程与皮肤）收尾阶段。静线是支持 BMS 与 mania 的唯一内置、默认与保底皮肤，正常开发启动、构建与发布会带上作者源更新，玩家无需导入；星轨已退出内置，仓库旧文件仅保留历史参考。作者可从模板修改、检查、打包并导入自己的作品，见[皮肤制作套件](skin-authoring/README.md)。设置中可分别选择 BMS/mania 皮肤，使用固定 `chartskin` 文件夹并手动刷新；原组件布局编辑器可调整组件和属性、导入图片，保存独立副本。静线已完成此前布局、信息区与轨道比例打磨，外观打磨按用户决定暂停；整体画面、输入设备与长时间体验仍未验收，Skin V1 与整体发行未完成。独立 IR 按需试运行，其余 Phase 3 联网功能保持冻结。具体进度与验收状态只以 [DEVELOPMENT_STATUS.md](doc_md/mainline/DEVELOPMENT_STATUS.md) 为准。

## 贡献

欢迎通过 [Issue](https://github.com/ZDaMexy/oms/issues) 反馈问题，或提交 Pull Request。提交代码前请注意：

- 优先打开 `osu.Desktop.slnf` 构建，确保 Release 零错误且不新增未归因告警；当前已知告警基线见 [DEVELOPMENT_STATUS.md](doc_md/mainline/DEVELOPMENT_STATUS.md)。
- 改动 BMS 相关逻辑时请运行 `osu.Game.Rulesets.Bms.Tests`。
- 任何改变计划、状态、约束或验证结论的改动，必须在**同一次提交**中同步更新 [`doc_md/`](doc_md/README.md) 中对应的治理文档，并运行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\CheckDocumentation.ps1` 与 `git diff --check`（详见 [AGENTS.md](AGENTS.md)）。

## 许可证

本项目采用 [MIT 许可证](LICENCE)，继承自上游 osu!lazer。

OMS 是 osu!lazer 的定向分支，项目目标与内容已与上游明显分化，并非 [`ppy/osu`](https://github.com/ppy/osu) 的镜像或替代发布源。

## 致谢

- [osu!lazer](https://github.com/ppy/osu) 与 [osu-framework](https://github.com/ppy/osu-framework) —— OMS 的上游基础。
- IIDX、LR2、beatoraja —— 判定、Gauge 与速度语义的方向校准来源。
