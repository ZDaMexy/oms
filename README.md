# OMS

OMS 是一个面向 BMS 与 osu!mania 的 Windows-only 音游客户端。对熟悉 LR2 和 beatoraja 的玩家来说，它的价值很直接：离线优先、可便携发布、支持本地导入，把 BMS 和 mania 收进同一个更现代的客户端里。

## 项目定位

- 平台：Windows 10 22H2 及以上
- 运行时：.NET 8、DesktopGL、osu-framework
- 当前保留模式：osu!mania、BMS
- 已移除模式：Osu、Taiko、Catch
- 发布策略：Phase 1-2 以 Portable.zip 全量包 + 手工覆盖更新为基线，游戏内在线更新默认禁用
- 联网策略：账号、在线排行榜、谱面下载、新闻/聊天、多人/观战与远程难度表源在 Phase 3 前默认隐藏或禁用

OMS 的代码实现主要通过 GitHub Copilot 的 AI agent 工作流完成，底层使用 GPT 模型。开发者负责产品方向、架构判断、测试验收与需求输入。

## 当前状态

- 可游玩模式：osu!mania、BMS
- BMS 主链：解码、转换、导入、游玩、结算可用
- 支持键位：7K+1
- 判定系统：OD、BEATORAJA、LR2
- Gauge：ASSIST EASY、EASY、NORMAL、HARD、EX-HARD、HAZARD，以及 GAS
- 计分与结果：EX-SCORE、CLEAR LAMP、DJ LEVEL
- 长条模式相关计分已区分 CN / HCN
- 难度表：离线难度表缓存、MD5 匹配、表分组、Song Select 音符分布图
- 输入：键盘组合键、XInput、MouseAxis、Raw Input，以及 Windows 默认启用的 DirectInput HID 路径；`HidSharp` 在 Windows 仅保留为 `OMS_ENABLE_HIDSHARP=1` 诊断后端，用于隔离 `DeviceList.Local` 的 `RegisterClass failed` 崩溃风险
- 离线模式：默认在线 endpoint 为空，在线入口与 Discord Rich Presence 受 `OnlineFeaturesEnabled` 守卫
- 最近验证：mania 定向回归 `50/50` 通过，BMS 过滤回归 `69/69` 通过，BMS HID handler 定向回归 `14/14` 通过，完整 `osu.Game.Rulesets.Bms.Tests` 为 `458/458` 通过，Debug/Release 构建通过，Release 启动 smoke 已确认不再因 HidSharp 闪退

## 仓库入口

优先阅读：

- `OMS_COPILOT.md`：产品约束、技术纪律与 release gate
- `DEVELOPMENT_PLAN.md`：执行顺序、阶段依赖与验收标准
- `DEVELOPMENT_STATUS.md`：已验证的仓库现状与遗留问题
- `CHANGELOG.md`：按日期倒序的变更摘要
- `SKINNING.md`：皮肤契约、fallback 与未冻结边界
- `UPSTREAM.md`：上游锁定点与同步策略
- `RELEASE.md`：便携发行构建与发布门槛

主要工程：

- `osu.Game`：核心游戏层
- `osu.Game.Rulesets.Mania`：保留的 mania 规则集
- `osu.Game.Rulesets.Bms`：BMS 解码、转换、判定、计分、导入与布局
- `oms.Input`：统一输入抽象层
- `osu.Desktop`：桌面入口

## 开发环境

- Windows 10/11
- .NET 8 SDK
- Visual Studio、JetBrains Rider 或 Visual Studio Code

仓库通过 `global.json` 锁定 .NET 8 基线，并允许在更高主版本 SDK 上滚动构建。

## 构建、运行与验证

优先打开 `osu.Desktop.slnf`。

构建：

```shell
dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m
```

运行：

```shell
dotnet run --project osu.Desktop
```

执行完整 BMS 测试：

```shell
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore
```

## 设计原则

- 不重新引入 Osu、Taiko、Catch
- 不盲目同步上游，按 `UPSTREAM.md` 选择性 cherry-pick
- BMS 功能优先级高于通用平台扩展
- 输入、判定、Gauge、难度表与 Song Select 行为都以 BMS 生态兼容性为优先
- 最终正式发行物不再以 osu!lazer 原生默认皮肤作为产品表面；OMS 将维护一套适用于 mania 与 BMS 的自有内置皮肤
- 在 Phase 3 前保持离线优先，不把“在线功能预留”当作“当前可用能力”对外描述

## 许可证

本仓库继承上游代码所使用的 MIT 许可证，详见 `LICENCE`。

需要注意的是，OMS 是基于 osu!lazer 的定向分支，项目目标、支持范围和仓库内容已经与上游仓库明显分化；请不要将本仓库视为 `ppy/osu` 的直接镜像或替代发布源。
