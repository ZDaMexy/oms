# OMS

OMS 是一个面向 BMS 与 osu!mania 的 Windows-only 音游客户端。对熟悉 LR2 和 beatoraja 的玩家来说，它的价值很直接：离线优先、可便携发布、支持本地导入，把 BMS 和 mania 收进同一个更现代的客户端里。

## 项目定位

- 平台：Windows 10 22H2 及以上
- 运行时：.NET 8、DesktopGL、osu-framework
- 当前保留模式：osu!mania、BMS
- 已移除模式：Osu、Taiko、Catch
- 发布策略：Phase 1-2 以 Portable.zip 全量包 + 手工覆盖更新为基线，游戏内在线更新默认禁用
- 联网策略：账号、在线排行榜、谱面下载、新闻/聊天、多人/观战与远程难度表源在 Phase 3 前默认隐藏或禁用

OMS 的代码实现主要通过 GitHub Copilot 的 AI agent 工作流完成，底层使用 GPT-5.4 模型，Claude Opus 4.6 用于增强开发。开发者负责产品方向、架构判断、测试验收与需求输入。

## 当前状态

- 可游玩模式：osu!mania、BMS
- BMS 主链：解码、转换、导入、游玩、结算可用
- 支持键位：5K,7K,9K,14K
- 判定系统：OD、BEATORAJA、LR2、IIDX；
- 判定难度：BEATORAJA / LR2 支持判定难度调整
- Gauge：ASSIST EASY、EASY、NORMAL、HARD、EX-HARD、HAZARD，以及 GAS；默认使用 OMS LEGACY 计量槽，也可切换到 BEATORAJA、LR2、IIDX 计量槽
- 计分与结果：EX-SCORE、CLEAR LAMP、DJ LEVEL；结果页主评价与缩略徽章已按 DJ LEVEL 显示，主分数区显式使用 EX-SCORE 语义
- 长条模式相关计分已区分 CN / HCN
- 训练 Mod：Mirror、RANDOM、R-RANDOM、S-RANDOM 与自定义 pattern 已可用
- 辅助 Mod：A-SCR、A-NOT 与 BMS Autoplay 已可用；A-SCR / A-NOT 分别支持 scratch / 非 scratch 的独立可见性与染色配置
- 回放：BMS replay recording / playback / 本地归档已接通，按 lane action 持久化
- 难度表：离线难度表缓存、MD5 匹配、表分组、Song Select 音符分布图
- 谱面元数据：已解析 `#SUBTITLE`、`#SUBARTIST`、`#COMMENT`、`#PLAYLEVEL`、`#DIFFICULTY`，Song Select 可显示副标题、谱师、内部标级与表标签
- 输入：键盘组合键、XInput、MouseAxis、Raw Input，以及 Windows DirectInput HID 路径
- 存储：Release 默认 `%APPDATA%/oms/`，Debug 使用 `%APPDATA%/oms-development/`；BMS 导入落在数据根下 `chartbms/`，mania 导入落在 `chartmania/`，支持 `storage.ini` 迁移到自定义数据根；Settings -> Maintenance 现已拆为“外部谱库”和“内部谱库”两层：外部侧提供“扫描外部谱库（重建）/（增量）”，内部侧提供“扫描内部谱库（重建）/（增量）”；其中增量模式只补导当前尚未以同一 `FilesystemStoragePath` 处于 active 状态的目录
- 首次启动设置：当前为欢迎、UI 缩放、获取谱面、导入、难度表设置、按键绑定六步流程；导入页直接复用外部谱库设置，难度表页可一键导入 zris 镜像预设，最后一步可直接配置全局、mania 与 BMS 键位
- 皮肤：BMS 默认层已完成七批 OMS-owned slice 收口；mania 侧已完成 shell / preset 接线与 8 类 OMS-owned 组件升格，但公开发行物产品面与少量 legacy config/asset lookup 兼容仍在收尾
- 离线模式：默认在线 endpoint 为空，在线入口受 `OnlineFeaturesEnabled` 守卫

## BMS Gauge 规则选择

OMS 现在把「Gauge 类型」和「Gauge 规则」拆成了两个独立维度。前者决定你玩的是 ASSIST EASY、EASY、NORMAL、HARD、EX-HARD、HAZARD 还是 GAS，后者决定这些血条在每个判定下具体怎么涨、怎么掉。默认不选任何 gauge-rules mod 时，使用的是 `OMS LEGACY`。

- `OMS LEGACY`：OMS 自己的统一简化算法，目标是让不同 gauge type 在 OMS 内部保持直观、稳定、连续。适合第一次用 OMS、主要在 OMS 内部练习，或者不需要刻意对齐 beatoraja / LR2 / IIDX 外部手感时使用。
- `BEATORAJA`：按 beatoraja 风格计算 gauge。适合平时主要玩 beatoraja、看 beatoraja 难度表或想让 clear 体感尽量贴近 beatoraja 时使用。OMS 会按 keymode 套用对应 profile，PMS 也保留更高的 gauge 上限。
- `LR2`：按 LR2 风格计算 gauge。适合想和旧 LR2 习惯、老表分体验或传统 BMS clear 预期对齐时使用。
- `IIDX`：按 beatmania IIDX 风格计算 gauge。普通 groove gauge 使用 IIDX 的 a-value，HARD 在低血区也有 IIDX 风格的减伤。适合想拿 OMS 做 IIDX 向练习，或者希望 clear/failed 手感更接近 IIDX 时使用。

需要注意的是，`IIDX` 规则族里的 `HAZARD` 仍然是 OMS 在 BMS 规则集中的扩展映射，用的是 IIDX 风格恢复量加 instant-fail 惩罚，并不是官方 IIDX 原生 gauge 项目的一比一复刻。

## 仓库入口

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
