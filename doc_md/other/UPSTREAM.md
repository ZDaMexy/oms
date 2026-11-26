# osu!lazer 上游维护

| 用途 | 固定来源 |
| --- | --- |
| 上游原始基线 | [ppy/osu 2026.305.0-lazer](https://github.com/ppy/osu/releases/tag/2026.305.0-lazer)，`bb289363a2b8e6bf62be355f8570def018f0d7be` |
| OMS 初始快照 | `0b97bbdd4348de47e1d597a65f0a7734ad184000`，单独起算的 Git 历史 |
| 最近一次评估 | [2026-11-24 专项审查](UPSTREAM_AUDIT_20261124.md)以 3 月为原始基线。6 月发布到 11 月目标的界面续审见下节 |
| 最近一次采用 | [2026-11-25 十一月底座接续](../mainline/CHANGELOG.md#2026-11-25-osulazer-十一月底座接续)、[2026-11-26 稳定版界面接续](../mainline/CHANGELOG.md#2026-11-26-稳定版界面接续) |

## 十一月目标采用范围

| 子系统 | 上游来源与 OMS 采用边界 |
| --- | --- |
| 框架与资源 | [框架 2026.1117.0](https://github.com/ppy/osu-framework/releases/tag/2026.1117.0)、[资源 2026.1112.1](https://github.com/ppy/osu-resources/releases/tag/2026.1112.1)。带上 Windows 游戏键 [#35727](https://github.com/ppy/osu/pull/35727) 的适配、资源图标 [#37848](https://github.com/ppy/osu/pull/37848) 和 Retro 配置 [#39165](https://github.com/ppy/osu/pull/39165)；旧 TIFF 局部边界、SDL 所有权与 FFmpeg 采用记录见当日主线。 |
| 谱库与选歌 | 谱库冻结引用 [#38572](https://github.com/ppy/osu/pull/38572)、本地最佳 [#37666](https://github.com/ppy/osu/pull/37666) / [#37722](https://github.com/ppy/osu/pull/37722) / [#37711](https://github.com/ppy/osu/pull/37711)、筛选 [#37701](https://github.com/ppy/osu/pull/37701)、键数 [#37285](https://github.com/ppy/osu/pull/37285)、收藏 [#38549](https://github.com/ppy/osu/pull/38549)、难度 [#37178](https://github.com/ppy/osu/pull/37178)。另接入搜索条件、复用面板、整套恢复、选歌焦点与旧选择纠错。 |
| 游玩与回放 | 滚速 [#37149](https://github.com/ppy/osu/pull/37149) 及[更正](https://github.com/ppy/osu/commit/b3c15339d0)、长条 [#37008](https://github.com/ppy/osu/pull/37008)、回放 [#38006](https://github.com/ppy/osu/pull/38006) / [#38898](https://github.com/ppy/osu/pull/38898)、Mod 组合 [#38267](https://github.com/ppy/osu/pull/38267)、原生 mania 击键音量 [#38990](https://github.com/ppy/osu/pull/38990) / [#39208](https://github.com/ppy/osu/pull/39208)。 |
| 谱面编辑 | Storyboard 保存 [#37790](https://github.com/ppy/osu/pull/37790) / [#38877](https://github.com/ppy/osu/pull/38877) / [#38895](https://github.com/ppy/osu/pull/38895) / [#38332](https://github.com/ppy/osu/pull/38332)、背景偏移 [#37841](https://github.com/ppy/osu/pull/37841)、视频 [#37857](https://github.com/ppy/osu/pull/37857)、Kiai [#38107](https://github.com/ppy/osu/pull/38107)、书签区域格式 [#38451](https://github.com/ppy/osu/pull/38451)、切换难度时钟 [#38042](https://github.com/ppy/osu/pull/38042)。 |

后续判断仍取原[候选条件](UPSTREAM_AUDIT_20261124.md#下一批可取得的收益与条件)和已实施记录。上游 Windows 音频偏移修复以 WASAPI 默认切换为前置。OMS 已有 15 ms Windows 偏移，强制切换前须测既有外设上的听感和判定。归档 200 MiB 单条目补丁源于上游谱面站策略，OMS 的 BMS 大体积 BGA、文件夹谱库及现有包预算走各自实际入口。新的 storyboard 命中触发、回放血条数据和历史成绩重算须各自覆盖现有谱、成绩和回放。官方线上系统与已移除的模式依本页底线处理。

## 六月至十一月正式发布间的界面

用 [2026.624.0-lazer](https://github.com/ppy/osu/releases/tag/2026.624.0-lazer)（`8c3c1b544bd6bbbd1d9065c76035e87f515a887d`）到 [2026.1117.0-lazer](https://github.com/ppy/osu/releases/tag/2026.1117.0-lazer)（`078f5a226368e62fd2974f8eb167303b41d71cab`）核对发布间隔。第一父链 347 条，其中 186 条触及 `osu.Game` 的 `Graphics`、`Overlays`、`Screens`。数量包含协作、在线和原型提交。

| 正常入口 | 采用与取舍 |
| --- | --- |
| 主菜单、设置和按键 | 主菜单仍是稳定入口。接入设置侧栏 [#38640](https://github.com/ppy/osu/pull/38640)，BMS、mania 各自独立。按键页接入 [#38166](https://github.com/ppy/osu/pull/38166)、[#38316](https://github.com/ppy/osu/pull/38316) 和 [#38296](https://github.com/ppy/osu/pull/38296)，按现有玩法变体保留手台及游戏键绑定。接入连续视差 [#38443](https://github.com/ppy/osu/pull/38443)，从旧开关迁移。仍需单独处理 [#38608](https://github.com/ppy/osu/pull/38608) 对模式专用编辑键的存储变体，避免既有 BMS 和 mania 键位互相覆盖。 |
| 选歌、暂停和回放 | 接入背景预览区域 [#38105](https://github.com/ppy/osu/pull/38105)、动态底部栏 [#39026](https://github.com/ppy/osu/pull/39026)，保留 OMS 选歌快捷键、Mod 面板和谱库筛选。暂停、失败和对话按钮接入 [#38357](https://github.com/ppy/osu/pull/38357)、[#38429](https://github.com/ppy/osu/pull/38429)；回放焦点和临时加速已在 11 月 25 日接入。 |
| 结算与皮肤 | 稳定 `SoloResultsScreen` 采用详情卡片 [#38643](https://github.com/ppy/osu/pull/38643) 与时间误差图 [#39073](https://github.com/ppy/osu/pull/39073)。BMS 详情和皮肤结算页仍各自拥有布局和数据来源。Argon 和 Legacy 的全屏新结算属于 [#24996](https://github.com/ppy/osu/issues/24996)，目标 tag 的 `SoloResultsScreenV2` 仅由测试场景实例化。等正常入口启用后复核回放、历史成绩和作者布局，再决定新结算及其右侧底部栏。传统皮肤继承的 [#39175](https://github.com/ppy/osu/pull/39175) 改动跨 `skin.ini` 和合成默认值，需独立验证 OMS 默认、作者皮肤和外置只读目录。 |
| 编辑器与皮肤编辑器 | 接入时间线效果点、校验长文字换行、时间线选择排序、皮肤组件翻转位置和 Sprite 透明度。保持老皮肤的 `BoxAlpha` 序列化名称，并保留仅改动设置项时的默认外观。 |

## 同步与验收

- 基准从 OMS 上次正式发布对应的上游 tag 起算，到最新稳定 tag。先按客户端的正常入口覆盖框架与本机、启动、设置、选歌、游玩、回放、结算、谱面与皮肤编辑、谱库和发行，结合相关源码确认哪些改动已采用、哪些需要适配、哪些需要暂缓。
- 沿用三个月一次的复查周期。出现影响现行 OMS 输入或依赖的紧急修复时，直接追该修复及后续更正。进度按实际采用和验证范围标记。用户可见界面从正常入口验证。
- 用上游原始基线比较上游发布，用 OMS 初始快照追 OMS 自有演进。评估两个当前树的共同改动时以同一个上游提交为基准。Git 历史无共同祖先，原版 tag 与 OMS 初始快照的树也有差异。
- 先确认正常使用路径会触发的问题或新增能力，再读上游补丁、同系列前置和后续修正。记录固定来源、触发条件、实际受影响的功能和验证结果。完整提交适用时可保留原作者与来源，涉及不存在的项目、过期的存储或线上 API 时，只沿 OMS 现有入口移植必要行为，并在提交中注明原 PR 和 commit。
- 要判断一个候选是否可采用，用同一输入对照当前版和候选版。缺陷修复应证明原问题可见、修后消失，性能改动应区分操作次数、耗时和实机帧时间。文本可以自动应用仅说明该文本匹配。
- 将不同的回退边界拆开提交。凡是写入既有数据、改变计分身份、升级运行时或替换原生依赖的候选，独立运行相应回归。单独的上游复核不移动原始基线。后续以评估终点继续筛查新上游变化，并保留已登记、尚未采用的候选。

## OMS 验收边界

| 影响面 | 采用前要证明的行为 |
| --- | --- |
| 启动、设置、菜单和网络入口 | Windows 上只加载 mania 和 BMS。旧 osu! API 地址为空、在线总开关关闭。固定 OMSIR 保持原账号、真实谱面身份及保存后交分。已批准下载和用户主动添加的公共难度表按原合同工作。见 [P3-IR](../subline/P3-IR/TECHNICAL_CONSTRAINTS.md)、[P1-A](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)。 |
| 谱库、Realm 和选歌 | `chartbms/`、`chartmania/` 原位直读。用可恢复的隔离数据根证明升级、重扫及回退保住旧成绩、回放、收藏、外部库、谱面可用性和当前选谱。Realm schema 及数据根由 OMS 负责。见 [P1-H](../subline/P1-H/TECHNICAL_CONSTRAINTS.md)、[P1-I](../subline/P1-I/TECHNICAL_CONSTRAINTS.md)、[P1-F](../subline/P1-F/TECHNICAL_CONSTRAINTS.md)。 |
| 计分、难度、演奏和回放 | 分别对照原生 BMS、原生 mania、BMS→mania。检查历史成绩身份、同谱旧回放、新局成绩保存及结算。涉及长条、输入或时钟时加入真实设备、LN/CN/HCN、暂停、回退和 seek。见 [P1-B](../subline/P1-B/TECHNICAL_CONSTRAINTS.md)、[P1-C](../subline/P1-C/TECHNICAL_CONSTRAINTS.md)、[P1-E](../subline/P1-E/TECHNICAL_CONSTRAINTS.md)、[P1-K](../subline/P1-K/TECHNICAL_CONSTRAINTS.md)。 |
| 皮肤、音频、BGA 和发行 | 静线是内置、默认和保底。保有现行皮肤选择、缺件回落、外部只读和作者权限。音频保有 BMS 键音与 BGM 的时序及 BGA 共用播放链。涉及依赖时验证实际 Windows 运行库、许可旁带及 portable / 自定义根。见 [P1-A](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)、[P1-J](../subline/P1-J/TECHNICAL_CONSTRAINTS.md)、[P1-L](../subline/P1-L/TECHNICAL_CONSTRAINTS.md)。 |

## 复查命令

在 OMS 客户端目录执行。示例使用最近一次已核对的正式 tag，下一次从发布页确认并替换目标。

```powershell
git fetch --no-tags upstream refs/tags/2026.1117.0-lazer:refs/remotes/upstream/lazer-2026.1117.0
$original = 'bb289363a2b8e6bf62be355f8570def018f0d7be'
$upstream = 'refs/remotes/upstream/lazer-2026.1117.0'
git rev-parse HEAD origin/master $upstream
git merge-base --is-ancestor $original $upstream
git diff --name-status 0b97bbdd HEAD -- osu.Game osu.Game.Rulesets.Mania
git diff --name-status $original HEAD
git diff --name-status $original $upstream
git log --first-parent --format='%H %s' "${original}..${upstream}"
git merge-tree --write-tree --name-only --messages --merge-base=$original HEAD $upstream
```

最后一条用于测量整合风险，产生的树只用来核对冲突和被带回的文件。候选在非系统盘的独立工作副本编译和测试。正常分支提交、实机和发行入口沿 [AGENTS](../../AGENTS.md) 及[改动验收矩阵](../mainline/DEVELOPMENT_PLAN.md#改动验收矩阵)。
