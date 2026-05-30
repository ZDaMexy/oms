# CLAUDE.md — OMS 仓库导航与文档联动约定

本文件是给 AI 助手（及任何协作者）的**快速定位索引**。OMS 把治理文档统一收口到 `doc_md/`，本索引帮助每次对话第一时间找到权威来源，并约束"改代码必须同步改文档"的联动规则。

## 项目一句话

OMS 是基于 osu!lazer 的 **Windows-only** 音游客户端：只保留 **osu!mania**，新增第一类 **BMS** 模式，已删除 Osu/Taiko/Catch。离线优先，Phase 3 前所有联网功能隐藏/禁用。详见 [README.md](README.md)。

## 每次对话推荐起步顺序

1. [doc_md/mainline/OMS_COPILOT.md](doc_md/mainline/OMS_COPILOT.md) — 权威产品约束、技术纪律、release gate（约 1500 行，按需用搜索定位章节，勿整篇加载）
2. [doc_md/mainline/DEVELOPMENT_STATUS.md](doc_md/mainline/DEVELOPMENT_STATUS.md) — 仓库当前真实状态、指标、遗留问题、当前主线
3. [doc_md/mainline/DEVELOPMENT_PLAN.md](doc_md/mainline/DEVELOPMENT_PLAN.md) — 执行顺序、阶段依赖、验收标准
4. 按归属进入对应 subline / other / mini 文档（见下方索引）

## 文档分层与索引

### 总索引
- [doc_md/README.md](doc_md/README.md) — 文档总索引与分层说明

### mainline（主线，全局权威）
- [OMS_COPILOT.md](doc_md/mainline/OMS_COPILOT.md) — 产品边界、技术约束、release gate（权威约束源）
- [DEVELOPMENT_PLAN.md](doc_md/mainline/DEVELOPMENT_PLAN.md) — 全局开发计划与阶段编排
- [DEVELOPMENT_STATUS.md](doc_md/mainline/DEVELOPMENT_STATUS.md) — 已验证现状、指标、遗留问题
- [CHANGELOG.md](doc_md/mainline/CHANGELOG.md) — 按日期倒序的变更摘要

### subline（主线子方向，每条固定维护四件套：PLAN / STATUS / CHANGELOG / TECHNICAL_CONSTRAINTS）
- [P1-A](doc_md/subline/P1-A/) — 产品面与 release gate，含皮肤边界冻结（当前 Phase 1.1 皮肤专项主归属）
- [P1-B](doc_md/subline/P1-B/) — 输入语义与硬件验收（analog scratch / 真实 HID）
- [P1-C](doc_md/subline/P1-C/) — 判定语义与反馈闭环（BRJ/LR2 parity、FAST/SLOW、pacemaker）
- [P1-D](doc_md/subline/P1-D/) — 控制器校准与诊断（deadzone / sensitivity / diagnostics）
- [P1-E](doc_md/subline/P1-E/) — gameplay 与长条真实谱面验校（LN/CN/HCN）
- [P1-F](doc_md/subline/P1-F/) — 首发离线发行基线（portable.ini + data/）
- [P1-G](doc_md/subline/P1-G/) — 人工验收后置
- [P1-H](doc_md/subline/P1-H/) — 存储拓扑支撑线（chartmania/、外部/内部谱库扫描）
- [P1-I](doc_md/subline/P1-I/) — BMS 选歌筛选与搜索定制
- [P1-J](doc_md/subline/P1-J/) — BMS gameplay runtime 性能与音频时序治理
- [P1-K](doc_md/subline/P1-K/) — BMS 解析链路治理（decoder / 转换 / projection / parse cache）
- [P1-L](doc_md/subline/P1-L/) — BMS 演出/Gimmick 谱视觉复刻（地雷渲染 / 专用滚动旁路；红线：不改坏正常游玩链路）
- 子线总入口：[doc_md/subline/README.md](doc_md/subline/README.md)

### other（参考材料，不替代主线计划/约束）
- [SKINNING.md](doc_md/other/SKINNING.md) — 皮肤契约、fallback 粒度、未冻结边界
- [RELEASE.md](doc_md/other/RELEASE.md) — 发行方式、打包约束、公开 release gate
- [IIDX_REFERENCE_AUDIT.md](doc_md/other/IIDX_REFERENCE_AUDIT.md) — IIDX/LR2/beatoraja 方向校准与训练反馈基线
- [BMS_FORMAT_REFERENCE.md](doc_md/other/BMS_FORMAT_REFERENCE.md) — BMS/bmson 格式权威参考与解析审查对照清单（服务 P1-K）
- [BMS_GIMMICK_CHART_RENDERING.md](doc_md/other/BMS_GIMMICK_CHART_RENDERING.md) — 演出/Gimmick 谱视觉复刻可行性与架构分析（已升级为 P1-L）
- [UPSTREAM.md](doc_md/other/UPSTREAM.md) — 上游锁定点、本地 diff 基线、cherry-pick 风险面
- 参考材料入口：[doc_md/other/README.md](doc_md/other/README.md)

### mini（与主线无关的独立事项，每项亦维护四件套）
- [doc_md/mini/README.md](doc_md/mini/README.md) — mini 事项索引
- [doc_md/mini/TEMPLATE/](doc_md/mini/TEMPLATE/) — 新建 mini 事项的四件套模板

## 文档联动更新规则（强制）

> 核心纪律：**不允许只改代码不改文档，也不允许保留已失真的文档叙事。**

1. **先归线**：任何开发、调研、修复、验收开始前，先判断归属 `mainline` / `subline` / `other` / `mini`。
2. **同次同步**：一旦实现/调研/修复/验收改变了计划、状态、约束或验证结论，必须在同一次改动中同步对应文档。
3. **子线 → 主线反向同步**：subline 或 mini 的变化若影响全局优先级、全局状态或硬约束，必须回写 `doc_md/mainline/` 的四件套。
4. **other → 主线升级**：`other` 中的参考结论一旦升级为正式约束或执行优先级，必须回写 `mainline` 与相关 `subline`。
5. **STATUS vs CHANGELOG 分工**：`DEVELOPMENT_STATUS.md` 只保留当前仍影响判读的阶段/事实/风险/验证基线；按日期展开的实现切片、回归命令、构建记录写入同目录 `CHANGELOG.md`，且"最近一次验证"只保留最新一条。
6. **专项同步面**：Phase 1.1 执行顺序/门槛/候选包语义变化时，须同步 `DEVELOPMENT_PLAN.md`、`README.md`、`SKINNING.md`、`RELEASE.md`、`OMS_COPILOT.md`。

## 关键工程与命令

主要工程：`osu.Game`（核心）· `osu.Game.Rulesets.Mania` · `osu.Game.Rulesets.Bms`（BMS 主开发目标）· `oms.Input`（统一输入抽象）· `osu.Desktop`（桌面入口）。

```shell
# 构建（优先 osu.Desktop.slnf）
dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m

# 运行
dotnet run --project osu.Desktop

# BMS 全量测试
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore
```

## 红线约束（详见 OMS_COPILOT.md）

- 不重新引入 Osu / Taiko / Catch。
- 不盲目同步上游，按 [UPSTREAM.md](doc_md/other/UPSTREAM.md) 选择性 cherry-pick。
- Phase 3 前保持离线优先：默认 endpoint 清空，不把"在线功能预留"当作当前可用能力描述。
- BMS 谱面直读 `chartbms/`、mania 直读 `chartmania/`，不经过通用 hash-backed `files/` store，不转 `.osz`。
- 发行物不再以 osu!lazer 原生默认皮肤作为产品表面；维护 OMS 自有内置皮肤。
