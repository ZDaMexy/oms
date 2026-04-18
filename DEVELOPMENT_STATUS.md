# OMS 开发进度与遗留问题

> 最后更新：2026-04-17
> 本文档只记录"仓库里已经真实存在的状态"，不重复规划全文。
> 详细分步规划见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，权威技术约束见 [OMS_COPILOT.md](OMS_COPILOT.md)，外部 IIDX / BMS 方向校准见 [IIDX_REFERENCE_AUDIT.md](IIDX_REFERENCE_AUDIT.md)。

## 状态定义

| 状态 | 含义 |
| --- | --- |
| 已完成 | 代码已落地，且至少通过一次构建、测试或明确手动验证 |
| 进行中 | 已有实际实现，但功能尚未满足该步骤验收标准 |
| 仅骨架 | 项目、类或入口已创建，但核心逻辑尚未接入 |
| 未开始 | 仓库中尚无对应实现 |
| 阻塞 | 当前步骤依赖尚未满足，暂时不能有效推进 |

## 最新快照

- **当前阶段**：Phase 1.1 皮肤系统专项执行中（BMS 默认层已收口，mania OMS-owned 组件与 release-gate 回归已继续收口；当前主线仍以公开发行物产品面收尾与 1.17 输入硬件/语义验收为先，但外部 IIDX 审计导出的反馈闭环与判定 parity 缺口已抬升为下一优先补强项，其中 BMS 结果页反馈面已明确归到 `P1-C`）
- **仓库定位**：Windows-only，保留 osu!mania + BMS，已移除 Osu/Taiko/Catch
- **主入口**：`osu.Desktop.slnf`（含 7 个项目）
- **BMS 规模**：147 源文件；`oms.Input` 15 源文件（含 Windows DirectInput backend）；46 个测试文件
- **已落地主链**：BMS 解码 → 转换 → 导入 → 7K+1 gameplay → 四套判定 → 六种 gauge + GAS → EX-SCORE / CLEAR LAMP / DJ LEVEL → CN/HCN mode-aware 计分 → 本地 best/replay/排行榜按 judge mode + long-note mode 分桶 → BMS replay recording / playback / 本地归档 → 难度表缓存 / MD5 匹配 / 表分组 → Song Select 分布图 → 谱面元数据摘要 → gameplay → results 自动跳转
- **BMS 元数据**：`#SUBTITLE` / `#SUBARTIST` / `#COMMENT` / `#PLAYLEVEL` / `#DIFFICULTY` 已解析，Song Select 可显示谱师、内部标级与表标签
- **存储**：Release 默认 `%APPDATA%/oms/`；`storage.ini` 可迁移到单一自定义数据根；BMS 使用 `chartbms/` 目录、mania 使用 `chartmania/` 目录的文件系统直读存储；外部多目录谱库扫描基线已落地（`ExternalLibraryConfig` JSON + `ExternalLibraryScanner` 委托注入）；Settings → Maintenance 已有外部谱库管理 UI（添加/移除/扫描）
- **输入**：键盘 / Raw Input / XInput / MouseAxis 主链可用；Windows 默认 HID 已切到 DirectInput；`HidSharp` 仅为 `OMS_ENABLE_HIDSHARP=1` 诊断后端
- **训练 Mod**：`BmsModMirror` 与 `BmsModRandom` 已落地；`RANDOM` / `R-RANDOM` / `S-RANDOM` + seed / custom pattern 已接通，14K 单组 pattern 可自动复制到双侧
- **辅助 Mod**：`BmsModAutoScratch` 与 `BmsModAutoplay` 已落地，均归 `DifficultyReduction`；A-SCR 会让 scratch 退出判定 / 计分 / gauge 池，并提供 mod 内可见性 / 染色配置；autoplay 已接通 BMS replay frame / replay input handler / replay recorder / auto generator
- **结果页反馈基线**：BMS results 的 expanded 主评价与 contracted badge 已按 `DJ LEVEL` 显示，主分数区已显式标为 `EX-SCORE`；results summary / gauge history 仍作为后续反馈面收口的复用基础
- **回放基线**：BMS replay frame / replay input handler / replay recorder / auto generator 已接通；本地 replay 归档复用 core legacy replay encode/decode 的 custom-ruleset fallback，当前按 lane action 持久化
- **反馈/训练闭环缺口**：BMS 专用 FAST/SLOW / judge display、低摩擦 visual timing-offset 交互、controller calibration / deadzone / sensitivity 可见入口，以及 gameplay 侧 EX pacemaker 仍未落地；按 [IIDX_REFERENCE_AUDIT.md](IIDX_REFERENCE_AUDIT.md) 已提升为下一阶段优先补强项
- **判定系统语义差距**：`OD` 主路径已稳定；`BEATORAJA` / `LR2` / `IIDX` judge mode 已显式接通，其中 `BEATORAJA` / `LR2` 的 judge-rank difficulty 已进入 runtime 与 score bucket；但 early/late 非对称 BAD / excessive poor、scratch 特例、更完整 long-note release parity 与按 judge family 参数化的 `Empty Poor` 仍未收口
- **联网**：全部在线入口与 Discord RPC 已按 `OnlineFeaturesEnabled` 守卫；默认 endpoint 已清空

### 皮肤系统现状

- **BMS 默认层**：七批 OMS-owned 切片已在 ruleset 侧收口（playfield / lane / note / hold / LaneCover / HUD / gauge / results / Song Select panels）
- **OmsSkin 基础设施**：`OmsSkin` host / provider / resource root、共享 `OmsSkinTransformer`、显式 `ManiaOmsSkinTransformer` 入口已落地
- **Global shell**：global HUD / SongSelect / Results / Playfield 缺省 shell 经 OMS preview 返回；`MainHUDComponents.json` / `SongSelect.json` / `Results.json` / `Playfield.json` layout metadata 由 regression 锁定；`ResultsScreen` global target 与 Skin Editor Results preview 已完成最小闭环
- **Mania 第一批**（Stage / Column / Key）：StageBackground / StageForeground / ColumnBackground / KeyArea / HitTarget 已切到 OMS shell 组件；10 类 stage-local / shared preset 已接通（layout、shell behaviour、shell asset、shell colour、key asset）
- **Mania 第二批**（Note / Hold / HitBurst / Judgement / HUD）：8 类 OMS-owned 组件已升格：`OmsNotePiece` / `OmsHoldNoteHeadPiece` / `OmsHoldNoteTailPiece` / `OmsHoldNoteBodyPiece` / `OmsManiaJudgementPiece` / `OmsHitExplosion` / `OmsManiaComboCounter` / `OmsBarLine`（均不再继承 legacy 类型），由 `OmsOwnedSkinComponentContractTest` + `TestSceneOmsBuiltInSkin` 锁定；note scrolling、combo 与 bar-line 的主要 runtime 语义已收口，当前剩余 gap 主要在 legacy config/asset lookup 兼容路径与公开发行物产品面收尾
- **Native-default removal**：`SetSkinFromConfiguration()` 已把 Argon / Triangles / DefaultLegacy / Retro 统一回退 OMS；`SkinManager` 只注册 `DefaultOmsSkin` 为受保护 built-in，启动时清理历史上游条目；legacy beatmap fallback 已切到 `DefaultOmsSkin`；`SkinManager.AllSources` 已去重
- **Partial override**：BMS 用户皮肤缺失 BMS 组件时返回 null 让后续 source 承接；mania legacy 用户皮肤缺失 note / hold / judgement / explosion / combo / bar-line 时回退 OMS 组件；mixed-layer 三类语义（mania-only / BMS-only / 双层皮肤）已有 runtime 证明
- **候选包语义**：`SimpleTou-Lazer` 仅为 mania 侧内置皮肤候选基线，不可对外称为"已完成默认皮肤"

### 1.17 输入切片现状

- `TestSceneOmsScratchGameplayBridge` 已覆盖：Scratch1 reverse-config / inverted suppression / reverse-config late-hit、14K Scratch2 全路径、second scratch mixed-source / inverted suppression、normal / inverted mouse/HID hold-survival、XInput takeover
- 剩余：更广的 analog scratch cross-device 产品语义、终态输入链、controller calibration / deadzone / sensitivity / diagnostics UI，以及真实 HID 硬件验收

## 开发指标

| 指标 | 当前值 | 说明 |
| --- | --- | --- |
| Phase 1 完成率 | 70.6% (12/17) | 仅按标记"已完成"项计算 |
| Phase 1 加权进度 | 85.3% (14.5/17) | 已完成=1, 进行中=0.5, 仅骨架=0.25, 未开始/阻塞=0 |
| Phase 1.1 皮肤专项 | 进行中 | BMS 默认层已收口；mania OMS-owned 组件、runtime 语义与 release-gate 回归已继续收口；公开发行物产品面待收尾 |
| 桌面端构建 | 通过 | `dotnet build osu.Desktop` 退出码 0 |
| BMS 全量测试 | **608/608** | 最近一次 `osu.Game.Rulesets.Bms.Tests` 全量 |
| Mania 皮肤回归 | **92/92** | `OmsOwnedSkinComponentContractTest` + `TestSceneOmsBuiltInSkin` |
| BMS 皮肤 fallback | **92/92** | `BmsSkinTransformerTest` / `TestSceneBmsUserSkinFallbackSemantics` |
| Scratch bridge | **43/43** | `TestSceneOmsScratchGameplayBridge` |
| osu.Game.Tests gate | **6/6** | startup migration / default-skin-edit / settings migration |
| 编译器诊断残留 | 0 | AutoMapper GHSA 已定点抑制 |

## 最近一次验证

> 严格只保留一条最新快照；详细命令与历史记录归档到 [CHANGELOG.md](CHANGELOG.md)。

### 2026-04-17

- **范围**：为消除文档漂移重跑当前 BMS 全量回归基线
- **本轮重跑**：BMS **608/608**
- **沿用最近已验证快照**：mania OMS **92/92**，BMS fallback **92/92**，scratch bridge **43/43**，`osu.Game.Tests` release-gate **6/6**

## 联网约束

| 项目 | 状态 | 说明 |
| --- | --- | --- |
| 便携版发布基线 | 已落地 | `portable.ini` 标记 → `<exe>/data/` 自动生成；已实机 Release publish 验证 |
| 游戏内在线更新 | 已禁用 | Velopack 跳过，`CreateUpdateManager()` 切回基础实现 |
| 默认 endpoint | 已清空 | `LocalOfflineAPIAccess` 默认装配；hub connector 返回 null |
| 游戏内联网入口 | 已隐藏 | Toolbar / 主菜单 / Song Select / overlay / 编辑器外链 / First-run Setup 均按 `OnlineFeaturesEnabled` 收口 |
| 上游静态资源 fallback | 已离线化 | LargeTextureStore / PreviewTrackManager / metadata cache 在线源已关闭；profile 资源已补本地占位 |
| BMS 原样目录存储 | 已完成 | `chartbms/` 直读，`FilesystemStoragePath` / `LocalFilePath` 已记录 |
| Mania 目录存储 | 已完成 | `chartmania/` 直读，与 BMS `chartbms/` 同级的独立目录树；`ManiaFolderImporter` + `ManiaBeatmapImporter` 已落地 |
| 多谱库根扫描 | 已完成 | `ExternalLibraryConfig`（JSON）+ `ExternalLibraryScanner`（委托注入）已落地；Settings → Maintenance `ExternalLibrarySettings` 设置 UI 可添加/移除/扫描；BMS / mania 双类型根均可注册 |

## 已落地能力

- 上游裁剪与项目基础，主入口以桌面端为准
- BMS 解码 → 转换 → 导入 → 7K+1 gameplay → 三套判定 → 六种 gauge + GAS → EX-SCORE / CLEAR LAMP / DJ LEVEL
- LN / CN / HCN mode-aware 计分与分桶
- BMS 结果页反馈首轮收口：expanded 主环 / contracted badge 使用 DJ LEVEL，主分数区显式使用 EX-SCORE 文案
- 离线难度表缓存 / MD5 匹配 / 表分组 / Song Select 音符分布图
- oms.Input 多源输入（键盘 / XInput / MouseAxis / Raw Input / DirectInput HID）
- gameplay → results 自动跳转
- BMS 皮肤链路：ruleset transformer + 全组件 lookup 接线

## Phase 1 进度矩阵

| 步骤 | 状态 | 差距 |
| --- | --- | --- |
| 1.1 上游清理 | 已完成 | — |
| 1.2 BMS 数据模型 | 已完成 | — |
| 1.3 BMS 解析器 | 已完成 | — |
| 1.4 谱面转换器 | 已完成 | — |
| 1.5 归档导入 | 进行中 | 仅剩桌面端拖放导入 UI 手工验收 |
| 1.6 键音系统 | 进行中 | 缺真实谱面长条边界人工验校 |
| 1.7 BMS 规则集入口 | 进行中 | 缺更完整 gameplay HUD 与真实谱面 gameplay 边角验校 |
| 1.8 7K+1 Playfield | 进行中 | 缺真实车道样式、皮肤化 drawable；并入 Phase 1.1 |
| 1.9 OD 判定系统 | 已完成 | — |
| 1.10 Normal Gauge | 已完成 | — |
| 1.11 EX-SCORE 与结算 | 已完成 | — |
| 1.12 密度星级 | 已完成 | — |
| 1.13 本地难度表 | 已完成 | — |
| 1.14 MD5 匹配 | 已完成 | — |
| 1.15 Song Select 表分组 | 已完成 | — |
| 1.16 音符分布图 | 已完成 | — |
| 1.17 输入绑定与 Lane Cover | 进行中 | analog scratch cross-device 产品语义与真实 HID 验收 |

## Phase 1.1 皮肤系统专项

| 步骤 | 状态 | 说明 |
| --- | --- | --- |
| 1.1.1 默认皮肤包分层 | 已澄清 | Global + Mania + BMS 三层独立 |
| 1.1.2 组件矩阵与 lookup | 已文档化 | 可直接驱动开发的映射矩阵 |
| 1.1.3 资源命名与配置桥 | 已文档化 | mania legacy 兼容 + BMS 自有命名 |
| 1.1.4 Global provider / shell | 进行中 | host / provider / resource root / shared transformer / layout metadata / results contract 已落地；当前维持 release gate 稳定 |
| 1.1.5 Mania 第一批 | 进行中 | 5 类 shell 组件 + 10 类 preset 已接通；仍主要消费 legacy-derived assets |
| 1.1.6 Mania 第二批 | 进行中 | 8 类 OMS-owned 组件已升格；主要 runtime 语义已收口，剩余 legacy config/asset lookup 兼容与公开发行物收尾 |
| 1.1.7 BMS 第一批 | 已完成 | playfield / lane / hit target / bar line / static BG 的 lookup 与 OMS 默认层 |
| 1.1.8 BMS 第二批 | 已完成 | note / hold / LaneCover / judgement / combo 的 lookup 与 OMS 默认层 |
| 1.1.9 BMS 第三批 | 已完成 | HUD / gauge / results / Song Select panels 的 lookup 与 OMS 默认层 |
| 1.1.10 Partial override | 进行中 | mixed-layer 三类语义已有 runtime 证明；legacy 用户皮肤 component-level fallback 已接通 |
| 1.1.11 Native-default removal | 进行中 | built-in realm 注册面已瘦身；settings / runtime fallback / source-chain 已收口；公开发行物剥离待收尾 |
| 1.1.12 测试矩阵与 release gate | 进行中 | Mania 92/92, BMS fallback 92/92, osu.Game.Tests 6/6, scratch 43/43 |

执行优先顺序：维持 release gate 稳定 → 1.17 analog scratch cross-device edge/hold contract → 真实硬件验收。

## Phase 2 / Phase 3

| 阶段 | 状态 | 备注 |
| --- | --- | --- |
| Phase 2 | 阻塞 | 依赖 Phase 1 + Phase 1.1 先落地 |
| Phase 3 | 阻塞 | 依赖本地 BMS 主流程稳定；在线功能保持禁用 |

## 待人工操作验收

默认放在 Phase 1 阶段末尾统一执行，仅在构成阻塞时提前请求用户介入。

| 事项 | 状态 |
| --- | --- |
| 1.5 桌面端拖放导入 / Song Select UI 验收 | 待做 |
| 桌面端真实 UI smoke test | 待做 |
| 便携发行物实际运行与覆盖更新验证 | 已完成 |

说明：Release publish 后 `portable.ini` 已验证会触发 `data/` 自动生成，目录结构正确。

## 当前主线

以下主线全部归属于 **Phase 1.x 大主线**，仅用于执行编排；不表示项目已经正式进入 Phase 2。除阻塞修复外，Phase 2 / Phase 3 功能仍按冻结处理。

| 子主线 | 焦点 | 状态 |
| --- | --- | --- |
| P1-A 产品面与 release gate | Phase 1.1 皮肤专项 → 公开发行物皮肤收尾 | 进行中 |
| P1-B 输入语义与硬件验收 | analog scratch cross-device contract → 真实 HID 覆盖 | 进行中 |
| P1-C 判定语义与反馈闭环补强 | BEATORAJA / LR2 parity / FAST/SLOW / judge display / BMS 结果页反馈面 / visual timing-offset / EX pacemaker | 已编排，下一优先级 |
| P1-D 控制器校准与诊断 | deadzone / sensitivity / scratch 模式说明 / live diagnostics | 下一优先级 |
| P1-E gameplay 与长条语义 | LN/CN/HCN 真实谱面验校 | 次优先级 |
| P1-F 首发离线发行基线 | portable.ini + data/ 便携模式已落地 | 已验证 |
| P1-G 人工验收后置 | 统一后置到 Phase 1 / 1.1 收口后 | 待做 |
| P1-H 存储拓扑支撑线 | chartmania/ 目录存储 + 外部多目录谱库扫描 + portable.ini 便携模式 | 已落地 |

## 遗留问题

### 高优先级

- **训练向 lane rearrangement 已落地**：`BmsModMirror` 与 `BmsModRandom`（`RANDOM` / `R-RANDOM` / `S-RANDOM` + 自定义 pattern）现已接入 BMS ruleset；当前 Phase 2 冻结重点已转向 `1P/2P flip` / `dan` / `FHS` / BSS / MSS 等更大范围能力
- **Phase 1.1 剩余**：mania 侧仍有 legacy config/asset lookup 兼容路径与公开发行物产品面收尾；维持 release gate 稳定后继续转向 1.17 输入与真实硬件验收
- **判定系统 parity 缺口**：当前 `BEATORAJA` / `LR2` / `IIDX` judge mode 已接通，但 `BEATORAJA` / `LR2` 仍缺 early/late 非对称窗口、scratch / long-note release 特例，以及按 judge family 参数化的 Empty Poor / excessive poor 触发语义；`IIDX` 也仍待进一步对齐细部体验
- **反馈闭环缺口**：仍缺 BMS 专用 FAST/SLOW / judge display、低摩擦 visual timing-offset 交互与 gameplay 侧 EX pacemaker；results 页主评价 / 缩略徽章 / 主分数文案虽已切到 BMS 语义，但结果反馈面本身仍只完成第一轮收口，尚未形成完整的 key-sounded BMS 训练闭环
- **控制器校准 / 诊断**：deadzone / sensitivity / scratch 模式说明 / live diagnostics 尚未落地；当前仅有 supplemental bindings 与 live capture，不足以覆盖 IIDX/BMS 控制器的一致性调校
- **内置皮肤候选包**：`SimpleTou-Lazer` 仅为 mania 候选基线，不可提前对外描述为已完成
- **upstream 默认皮肤移除**：runtime fallback 已大部分收口到 OMS；剩余公开发行物剥离与 partial override 全路径收口
- **osu.Game.Tests 稳定性**：6/6 已恢复；后续扩大范围应沿 csproj exclusion 清单逐步清退
- **1.6 真实谱面长条验校**：Phase 1 最贴近玩法质量的剩余项
- **便携发行物实机验证**：portable.ini → data/ 已验证；剩余：内置皮肤发行门槛

### 中优先级

- **Windows HID 实机验收**：DirectInput backend 已接通，需真实 IIDX/BMS 控制器覆盖
- **存储拓扑**：portable.ini 便携模式已落地（data/ 子目录自包含）；chartmania/ 目录已落地；外部多目录谱库扫描与 Maintenance UI 已完成；剩余删除/失效语义、path identity dedup 与重扫策略
- **AutoMapper GHSA**：`NuGetAuditSuppress` + `NU1903 NoWarn` 已定点抑制，运行时 `MaxDepth(3)` 缓解攻击面；升级到 15.x 需 ~150 行 API 迁移 + Realm 操作全回归，暂维持现状
- **上游 cherry-pick 风险**：42 个 osu.Game 文件（40 修改 + 2 新增），其中 6 个属于高频改动区（详见 UPSTREAM.md）
- **密度星级标定**：已压到保守区间，需真实样本继续校准

### 低优先级

（当前无低优先级遗留项；构建已确认 0 warning / 0 error）

## 更新约定

- 优先更新"状态变化""遗留问题变化"和"一条最新验证快照"
- "最近一次验证"只保留最新一条；历史归 `CHANGELOG.md`
- Phase 1.1 执行顺序 / 门槛 / 候选包语义变化时必须与 `DEVELOPMENT_PLAN.md`、`README.md`、`SKINNING.md`、`RELEASE.md`、`OMS_COPILOT.md` 同步
