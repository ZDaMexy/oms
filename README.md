# OMS

OMS 是一个面向 BMS 与 osu!mania 的 Windows-only 音游客户端。对熟悉 LR2 和 beatoraja 的玩家来说，它的价值很直接：离线优先、可便携发布、支持本地导入，把 BMS 和 mania 收进同一个更现代的客户端里。

## 项目定位

- 平台：Windows 10 22H2 及以上
- 运行时：.NET 8、DesktopGL、osu-framework
- 当前保留模式：osu!mania、BMS
- 已移除模式：Osu、Taiko、Catch
- 发布策略：Phase 1-2 以 `oms_YYYYMMDD(.zip)` 便携全量包 + 手工覆盖更新为基线，游戏内在线更新默认禁用
- 联网策略：账号、在线排行榜、谱面下载、新闻/聊天与多人/观战在 Phase 3 前默认隐藏或禁用；BMS 难度表当前已支持本地路径与公共 URL 导入/刷新，不依赖 OMS 私服

## 当前状态

| 功能域 | 状态 | 备注 |
|--------|------|------|
| 游玩核心 | ✅ 可用 | mania/BMS；5/7/9/14K |
| 判定与计分 | ✅ 可用 | 四判定；EX/DJ/灯 |
| Mod 与训练 | ✅ 可用 | 训练与辅助 Mod 已接通 |
| 输入 | ✅ 可用 | 键盘/XInput/Raw/HID |
| 难度表 | ✅ 可用 | 本地在线源/MD5/分组 |
| 皮肤 | 🔧 进行中 | BMS 已收口，mania 收尾 |
| 发行与存储 | ✅ 可用 | 便携包/数据根迁移 |
| 联网功能 | ⬜ 未开始 | 默认隐藏或禁用 |

> 详细状态与遗留问题见 [DEVELOPMENT_STATUS.md](doc_md/mainline/DEVELOPMENT_STATUS.md)

## 当前待办 / TODO

- 公开发行物产品面与 release gate 收尾
- 输入语义与真实 HID 验收
- dense autoplay 剩余一次性卡顿确认
- 详细执行顺序见 [doc_md/mainline/DEVELOPMENT_PLAN.md](doc_md/mainline/DEVELOPMENT_PLAN.md)

## BMS Gauge 规则选择

OMS 现在把「Gauge 类型」和「Gauge 规则」拆成了两个独立维度。前者决定你玩的是 ASSIST EASY、EASY、NORMAL、HARD、EX-HARD、HAZARD 还是 GAS，后者决定这些血条在每个判定下具体怎么涨、怎么掉。默认不选任何 gauge-rules mod 时，使用的是 `OMS LEGACY`。

- `OMS LEGACY`：OMS 自己的统一简化算法，目标是让不同 gauge type 在 OMS 内部保持直观、稳定、连续。适合第一次用 OMS、主要在 OMS 内部练习，或者不需要刻意对齐 beatoraja / LR2 / IIDX 外部手感时使用。
- `BEATORAJA`：按 beatoraja 风格计算 gauge。适合平时主要玩 beatoraja、看 beatoraja 难度表或想让 clear 体感尽量贴近 beatoraja 时使用。OMS 会按 keymode 套用对应 profile，PMS 也保留更高的 gauge 上限。
- `LR2`：按 LR2 风格计算 gauge。适合想和旧 LR2 习惯、老表分体验或传统 BMS clear 预期对齐时使用。
- `IIDX`：按 beatmania IIDX 风格计算 gauge。普通 groove gauge 使用 IIDX 的 a-value，HARD 在低血区也有 IIDX 风格的减伤。适合想拿 OMS 做 IIDX 向练习，或者希望 clear/failed 手感更接近 IIDX 时使用。

需要注意的是，`IIDX` 规则族里的 `HAZARD` 仍然是 OMS 在 BMS 规则集中的扩展映射，用的是 IIDX 风格恢复量加 instant-fail 惩罚，并不是官方 IIDX 原生 gauge 项目的一比一复刻。

## 仓库入口

建议按以下顺序阅读：先看 OMS_COPILOT.md 了解产品边界，再看 DEVELOPMENT_PLAN.md 了解阶段安排，最后按需查阅其余文档。

优先阅读：

- [doc_md/README.md](doc_md/README.md)：文档总索引
- [doc_md/mainline/OMS_COPILOT.md](doc_md/mainline/OMS_COPILOT.md)：产品约束、技术纪律与 release gate
- [doc_md/mainline/DEVELOPMENT_PLAN.md](doc_md/mainline/DEVELOPMENT_PLAN.md)：执行顺序、阶段依赖与验收标准
- [doc_md/mainline/DEVELOPMENT_STATUS.md](doc_md/mainline/DEVELOPMENT_STATUS.md)：已验证的仓库现状与遗留问题
- [doc_md/mainline/CHANGELOG.md](doc_md/mainline/CHANGELOG.md)：按日期倒序的变更摘要
- [doc_md/subline/README.md](doc_md/subline/README.md)：主线子方向索引与开发线入口
- [doc_md/other/SKINNING.md](doc_md/other/SKINNING.md)：皮肤契约、fallback 与未冻结边界
- [doc_md/other/RELEASE.md](doc_md/other/RELEASE.md)：便携发行构建与发布门槛
- [doc_md/other/UPSTREAM.md](doc_md/other/UPSTREAM.md)：上游锁定点与同步策略
- [doc_md/subline/P1-A/README.md](doc_md/subline/P1-A/README.md)：P1-A 产品面与 release gate 子线
- [doc_md/subline/P1-C/README.md](doc_md/subline/P1-C/README.md)：P1-C 判定语义与反馈闭环子线

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
dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m
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
- 不盲目同步上游，按 [doc_md/other/UPSTREAM.md](doc_md/other/UPSTREAM.md) 选择性 cherry-pick
- BMS 功能优先级高于通用平台扩展
- 输入、判定、Gauge、难度表与 Song Select 行为都以 BMS 生态兼容性为优先
- 最终正式发行物不再以 osu!lazer 原生默认皮肤作为产品表面；OMS 将维护一套适用于 mania 与 BMS 的自有内置皮肤
- 在 Phase 3 前保持离线优先，不把“在线功能预留”当作“当前可用能力”对外描述
- 任何开发、调研、修复或验收一旦改变计划、状态、约束、验证结论，必须同步更新 [doc_md/README.md](doc_md/README.md) 所归属的文档，不允许只改代码或只改叙事

## 许可证

本仓库继承上游代码所使用的 MIT 许可证，详见 `LICENCE`。

需要注意的是，OMS 是基于 osu!lazer 的定向分支，项目目标、支持范围和仓库内容已经与上游仓库明显分化；请不要将本仓库视为 `ppy/osu` 的直接镜像或替代发布源。
