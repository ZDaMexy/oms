# P1-A 当前状态：Skin V1、产品面与 release gate

> 最后更新：2026-08-30
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，稳定合同见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)。

## 一句话状态

`SV1-0` 自动、schema 56 数据与用户实机 gate 已全部通过；`SV1-1` 已导入 `.osk` 的 BMS Note/LN 纵切覆盖普通短键与长条 head/body/tail，`V-001`～`V-004` 仍为 **0/4**。`SV1-2` 的 `C1` 与 `C2` 均已闭合：唯一Settings manual Reload、三源same-ID coherent publication、participant/lease/detach/retire、current mutation原子边界及宽回归/Release/文档/独立终审均已签发。当前为 **`2/7 closed，C3 active`**；G1最终整包门、`SV1-2`整体、`SV1-1`、Skin V1与release仍未完成。

## 当前产品能力

- **恢复基线可用**：`.osk`/legacy mania、BMS F1 静态颜色/纹理/几何、选择链与程序化 `OmsSkin` 迁移 fallback 保持可用。
- **已实现的可见纵切**：选中的用户BMS包可为普通短键与长条head/body/tail提供静态图或固定60 FPS连续编号帧；body宽度只接受finite且`0 < width <= 1`，否则回到`0.5775`。这四项仅自动gate通过，视觉签收仍为0/4。
- **C1作者工作区**：可在settings注册external目录、显式选择并从configured restart重捕，或导入新的managed direct child；external行可Open/Import/Unregister，managed行可Open/Rename/Delete。external始终只读，random/next/previous不会隐式选中它。
- **C1安全边界**：external source bytes只来自fresh held capture的immutable capsule，目录/空目录来自同次manifest；service-owner只授权Realm记录管理。所有managed mutation持有exact external registry物理证明至final Realm线性化，single v3 journal与recovery保护crash/cancel/partial copy。
- **ordinary `.osk` 安全导入**：仍是hash-backed Realm package；skin-scoped reader在内容消费前完成raw/central-directory/name/type/size有界准入，actual stream持续验证CRC/比率/总量/取消。fault/cancel的exact receipt只回滚本次新增的零引用record/blob，不会删共享hash。
- **C2产品触发已冻结**：Settings → Skin 的 `Reload current skin` 是唯一手动触发；Folder Skin Workspace不新增行级Reload，same-value selection不冒充reload，也不实现watcher。按钮覆盖ordinary Realm `.osk`、managed与external current source；live gameplay/gameplay preview在任何source prepare前确定性拒绝并反馈退出后重试。
- **C2生命周期已闭合**：immutable current revision先在后台完成全部I/O/capture/解析/资源和participant staged prepare，再由update-thread可回滚引用barrier一次发布；失败保留exact旧pair/revision，成功后旧owner只在consumer/work lease最后detach后exactly-once retire。完整participant/holder/bypass稳定分类见[技术约束](TECHNICAL_CONSTRAINTS.md)，诊断召回见[atomic reload/detach memory](../../../.Codex/memory/reference_skin_atomic_reload_detach.md)。
- **C2 mutation已闭合**：current external Unregister、current managed Delete与ordinary current `.osk` Delete都先发布受保护fallback并等待旧revision detach；external只做fresh exact Realm remove且source零I/O，managed只在此后进入C1 journal/physical边界。legacy Skin Editor、external-edit与update-import UI/backend均稳定禁用。
- **C2最终验证**：core focused **204/204**、PendingAsync ownership visual/host **11/11**、core canonical `~Skin` **1137/1143**（六项精确既有基线）、mania `~Skin` **182/182**、BMS `~Skin` **796/796**、BMS full **1670/1670**；full的`--blame-hang 5m`明确全数完成且无hang sequence。完整真实产品路径集 **314/314**，final drift/half-loaded/Ready sentinel **6/6**。participant/holder、reachable bypass、concurrency/owner及tests/product-contract独立终审均为 **0/0/0**；Release **0 error**。
- **未交付**：C3的P1-K前置与唯一layout，以及shared codec、剩余slot三态、scene/event、sandbox、canonical双包与Authoring Kit；程序化`OmsSkin`仍是迁移链底。

## 最终 Skin V1 差距总览

`2/7 closed`是硬退出门计数，不换算线性工期或完成度；各campaign不等权，后续scene、sandbox与canonical发行体量显著更大。当前按玩家结果分面如下：

| 产品面 | 当前结论 | 距最终预期 |
| --- | --- | --- |
| 恢复、数据与导入安全 | **通过** | 保持现有恢复/receipt边界，后续不得重做或放宽 |
| 作者目录工作区 | **C1通过** | 已有真实注册/选择/复制/Open/Rename/Delete/Unregister；原位即时reload不在C1 |
| current revision生命周期 | **C2通过** | 三源same-ID publication、完整participant/holder lease、detach/retire及current mutation已闭合；C3～C6新增consumer仍须同切加入协议 |
| 唯一layout | **C3 active** | 先闭合P1-K lane timeline/keymode authority，再把5K/7K四style、9K BMS/PMS、14K DP及BGA/HUD/gauge/combo统一到一个revision snapshot |
| shared codec/catalog/三态 | **未实现** | 当前只有BMS Note/LN窄`Provide/Inherit`，尚无全slot `Suppress`、mania parity和完整结构化诊断 |
| scene/event与完整表现力 | **未实现** | 当前用户包仅覆盖窄静态/编号帧Note/LN；其余key/mine/judgement/gauge/combo/BGA/effect尚未进入公共声明式runtime |
| optional sandbox | **未实现** | 无受限VM、工具链、授权、预算、确定性、熔断与profiler |
| canonical发行闭环 | **未实现** | `oms-simple/oms-complex`、Authoring Kit、validator、只读恢复与程序化`OmsSkin`退役尚未交付 |
| 集中视觉与release | **0/4待签收，release未完成** | 仍需最终自动门、真实设备/谱面/视觉签收与发行复核 |

产品价值按`真实caller → authoritative manager/backend → production consumer或直接用户结果`核算。C1新增的主要交付均有真实设置/导入caller；selection/import/ManagedCopy等包生效链另有BMS/mania consumer证据，Open/Rename/Delete/Unregister/support形成直接用户结果。大量Windows authority、journal/recovery与receipt代码直接防止外部源被写、错目标删改、partial copy和共享blob误删，不是用内部类型数量制造进度。仓库仍保留一个C1前已有、没有独立非测试caller的fixed-staging import surface；其StagedImport operation/handler仍无production caller，但共同的fixed-slot authority/native move+inspection及journal/coordinator/recovery框架已被ManagedCopy复用，因此既不计作额外用户功能，也不能把全部共同底层判成死代码。后续继续禁止无caller/consumer的foundation独占campaign。

## 七个交付 Campaign

`SV1-*` 是能力分类，`C1`～`C7` 才是交付燃尽。当前为 **`2/7 closed，C3 active`**：

`C1` 作者文件工作区/G1 UX ✓ → `C2` 当前consumer revision reload/detach ✓ → **`C3` P1-K+唯一layout（active）** → `C4` shared codec/catalog/resolver/mania compatibility → `C5` scene/event与剩余slot production → `C6` sandbox并关闭最终整包reload门 → `C7` canonical双包/Authoring Kit/自动release。

C2冻结边界不得退化成manager-only reload API、same-ID selection或per-host reloadable：Settings唯一manual Reload、live gameplay/preview prepare前拒绝、三源same-ID publication、BMS/core/mania与shell生命周期participant、ordinary Realm `.osk` owner、current external/managed/ordinary mutation及legacy旁路均已进入统一协议。完整inventory见[技术约束](TECHNICAL_CONSTRAINTS.md)，C3工作门见[当前计划](DEVELOPMENT_PLAN.md)。

## 当前 gate

| Gate | 状态 |
| --- | --- |
| schema 56 数据安全 / 恢复基线实机 | **通过**；无authority orphan blob继续保全，不运行全局cleanup |
| `SV1-1` Note/LN首个产品纵切 | **自动门已闭合，视觉待签收**；`V-001`～`V-004` 为0/4，不等于`SV1-1`完成 |
| `SV1-2` / `C1` 作者工作区 | **通过**；真实caller/renderer、恢复、宽回归、Release、文档和独立终审已闭合 |
| `SV1-2` / `C2` current revision reload/detach | **通过**；只允许Settings显式manual Reload，磁盘变化仍不自动reload，live gameplay/preview明确拒绝 |
| `SV1-3` / `C3` P1-K前置与唯一layout | **active**；执行门见[当前计划](DEVELOPMENT_PLAN.md) |
| `SV1-4`～`SV1-7` / G1最终门 / Skin V1 / release | **未完成** |

## 最新验证：2026-08-30文档 / 2026-08-24代码

- core C2 focused **204/204**，PendingAsync ownership visual/host **11/11**，完整真实C2产品路径 **314/314**，final drift/half-loaded/Ready sentinel **6/6**。
- core canonical `~Skin` **1137/1143**；六项与既有精确基线相同：4项removed-Osu `TestSceneBeatmapSkinResources`、1项default background cycling、1项Argon sample。mania `~Skin` **182/182**。
- BMS `~Skin` **796/796（8m53s）**；BMS full **1670/1670（10m09s）**，`--blame-hang 5m`全数完成且无hang sequence。
- Release含restore首跑 **0 error / 20 known warnings（41.88s）**；formatter后`--no-restore`复验 **0 error / 11 known warnings（36.58s）**。20项为18次MessagePack `NU1902`输出加既有`CS8600`/`CA2007`；11项为9次`NU1902`加同两项。
- core、core-tests、BMS、BMS-tests、mania-tests owning-project targeted formatter均exit 0，仅`IDE1006`不可自动修复提示。participant/holder、reachable bypass、concurrency/owner、tests/product-contract四项独立终审均为blocker/major/moderate **0/0/0**。
- 2026-08-30删除一次性C2 handoff及四份已取代的跨会话续接文档、把长期事实归回四件套/CHANGELOG/atomic memory后，`CheckDocumentation.ps1`通过 **132 Markdown / 1051 relative links / 81 memory wiki links**，仅两份PLAN数字比值提醒；`git diff --check`通过。该轮无代码改动，未重跑.NET/Release，代码基线仍为以上2026-08-24结果。

## 当前风险

- scanner仍只在启动后对账一次，不是watcher；managed新增目录仍需重启发现，已登记current source的原位内容只能由Settings显式Reload进入新revision，external active capsule不会自行混入磁盘变化。
- C1的held-root copy/move/delete与journal/recovery不是filesystem transaction；foreign addition/replacement或证据漂移可导致冻结，这是fail-closed而不是all-or-nothing承诺。
- current external unregister已先发布coherent protected fallback并等待旧revision detach，再fresh compare exact service-owner/record/current revision做pure-Realm remove；任一步失败恢复或保持旧pair/revision且source零变化。该边界随C2冻结。
- core Skin的6个已知fixture失败不得用于隐藏新回归；后续改shared importer/skin时仍须比对同一精确基线。
- 当前可见纵切仅覆盖BMS普通短键与LN head/body/tail；唯一layout、完整三态/scene/script与canonical fallback未完成。
- C1/C2体量与集中度已形成维护风险，代码量本身不得作为进度。C3必须以P1-K真实decode/keysound authority、唯一layout snapshot、完整production consumer及C2 owner协议扩展证明产品价值；DTO/solver fixture只能补合同，不能替代纵切。
