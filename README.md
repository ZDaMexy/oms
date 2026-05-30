# OMS

OMS 是一个面向 BMS 与 osu!mania 的 Windows 音游客户端。它从 osu!lazer 出发，砍掉了 osu!、Taiko、Catch，专注把 BMS 和 mania 收进同一个更现代的客户端里：离线优先、可便携、支持本地谱面导入。熟悉 LR2 或 beatoraja 的玩家可以很快上手。

## 特性

- **两种模式**：osu!mania 与 BMS，覆盖 5 / 7 / 9 / 14K
- **判定与计分**：四判定体系，EX / DJ 计分与背光（灯）反馈
- **多种 Gauge**：ASSIST EASY / EASY / NORMAL / HARD / EX-HARD / HAZARD / GAS，并可在 OMS LEGACY、beatoraja、LR2、IIDX 等规则族之间切换，让 clear 手感贴近你熟悉的平台
- **训练与辅助 Mod**：内置面向练习的 mod
- **广泛输入支持**：键盘、XInput 手柄、Raw Input、HID 控制器
- **BMS 难度表**：本地与公共 URL 在线源导入、MD5 匹配、分组浏览
- **便携发布**：免安装全量包，数据根目录可迁移

## 系统要求

- Windows 10 22H2 或更高版本
- 基于 .NET 8 / DesktopGL / osu-framework

## 离线优先

OMS 目前完全离线运行。账号、在线排行榜、谱面下载、新闻 / 聊天、多人与观战等联网功能默认隐藏或禁用，计划在后续阶段逐步开放。

唯一的例外是 BMS 难度表：已支持本地路径与公共 URL 的导入 / 刷新，不依赖任何 OMS 私有服务器。

## 获取与运行

前往 [GitHub Releases](https://github.com/ZDaMexy/oms/releases) 下载最新的便携全量包 `oms_YYYYMMDD.zip`，解压后直接运行即可，无需安装；更新时下载新包覆盖旧目录。游戏内的在线自动更新默认关闭。

## 从源码构建

需要 .NET 8 SDK，以及 Visual Studio、JetBrains Rider 或 Visual Studio Code 之一。优先打开 `osu.Desktop.slnf`。

```shell
# 构建
dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m

# 运行
dotnet run --project osu.Desktop
```

## 深入文档

完整的产品边界、开发计划、当前状态与技术约束都收口在 [doc_md/README.md](doc_md/README.md)。

## 许可证

本仓库继承上游 MIT 许可证，详见 `LICENCE`。OMS 是 osu!lazer 的定向分支，项目目标与内容已与上游明显分化，并非 `ppy/osu` 的镜像或替代发布源。
