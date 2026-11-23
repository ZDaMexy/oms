# osu!lazer 上游维护

| 用途 | 固定来源 |
| --- | --- |
| 上游原始基线 | [ppy/osu 2026.305.0-lazer](https://github.com/ppy/osu/releases/tag/2026.305.0-lazer)，`bb289363a2b8e6bf62be355f8570def018f0d7be` |
| OMS 初始快照 | `0b97bbdd4348de47e1d597a65f0a7734ad184000`，单独起算的 Git 历史 |
| 最近一次评估 | [2026-11-24 专项审查](UPSTREAM_AUDIT_20261124.md)，评估终点为 `2026.1117.0-lazer` |

## 选择性同步

- 沿用三个月一次的复查周期。出现影响现行 OMS 输入或依赖的紧急修复时，直接追该修复及后续更正。
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
