# P1-A 当前状态：Skin V1、产品面与 release gate

> 最后更新：2026-08-13
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，稳定合同见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)。

## 一句话状态

`SV1-0` 自动、schema 56 数据与用户实机 gate 已全部通过；`SV1-1` 已导入 `.osk` 的 BMS Note/LN 纵切覆盖普通短键与长条 head/body/tail，`V-001`～`V-004` 仍为 **0/4**。`SV1-2` 的 `C1` 已闭合 Folder Skin Workspace、external 严格只读工作区、exact-set managed mutation/single-v3 ManagedCopy/journal recovery 与 ordinary `.osk` bounded ingress/zero-residue receipt；宽回归、Release与独立终审已过，当前是 **`1/7 closed，C2 active`**。`C2` 需实现当前全部production consumer的revision publication/reload/detach/retire；G1、`SV1-2`、`SV1-1`、Skin V1与release均未完成。

## 当前产品能力

- **恢复基线可用**：`.osk`/legacy mania、BMS F1 静态颜色/纹理/几何、选择链与程序化 `OmsSkin` 迁移 fallback 保持可用。
- **已实现的可见纵切**：选中的用户BMS包可为普通短键与长条head/body/tail提供静态图或固定60 FPS连续编号帧；body宽度只接受finite且`0 < width <= 1`，否则回到`0.5775`。这四项仅自动gate通过，视觉签收仍为0/4。
- **C1作者工作区**：可在settings注册external目录、显式选择并从configured restart重捕，或导入新的managed direct child；external行可Open/Import/Unregister，managed行可Open/Rename/Delete。external始终只读，random/next/previous不会隐式选中它。
- **C1安全边界**：external source bytes只来自fresh held capture的immutable capsule，目录/空目录来自同次manifest；service-owner只授权Realm记录管理。所有managed mutation持有exact external registry物理证明至final Realm线性化，single v3 journal与recovery保护crash/cancel/partial copy。
- **ordinary `.osk` 安全导入**：仍是hash-backed Realm package；skin-scoped reader在内容消费前完成raw/central-directory/name/type/size有界准入，actual stream持续验证CRC/比率/总量/取消。fault/cancel的exact receipt只回滚本次新增的零引用record/blob，不会删共享hash。
- **未交付**：current revision reload/detach、唯一layout、shared codec、剩余slot三态、scene/event、sandbox、canonical双包与Authoring Kit；程序化`OmsSkin`仍是迁移链底。

## 最终 Skin V1 差距总览

`1/7 closed`是硬退出门计数，不是14%的线性工期或完成度；各campaign不等权，后续scene、sandbox与canonical发行体量显著更大。当前按玩家结果分面如下：

| 产品面 | 当前结论 | 距最终预期 |
| --- | --- | --- |
| 恢复、数据与导入安全 | **通过** | 保持现有恢复/receipt边界，后续不得重做或放宽 |
| 作者目录工作区 | **C1通过** | 已有真实注册/选择/复制/Open/Rename/Delete/Unregister；原位即时reload不在C1 |
| current revision生命周期 | **C2 active，未实现** | 三种source仍缺全consumer coherent publication、detach ack与owner retire；current external不能安全解除注册 |
| 唯一layout | **未实现** | 5K/7K四style、9K BMS/PMS、14K DP及BGA/HUD/gauge/combo仍未统一到一个revision snapshot |
| shared codec/catalog/三态 | **未实现** | 当前只有BMS Note/LN窄`Provide/Inherit`，尚无全slot `Suppress`、mania parity和完整结构化诊断 |
| scene/event与完整表现力 | **未实现** | 当前用户包仅覆盖窄静态/编号帧Note/LN；其余key/mine/judgement/gauge/combo/BGA/effect尚未进入公共声明式runtime |
| optional sandbox | **未实现** | 无受限VM、工具链、授权、预算、确定性、熔断与profiler |
| canonical发行闭环 | **未实现** | `oms-simple/oms-complex`、Authoring Kit、validator、只读恢复与程序化`OmsSkin`退役尚未交付 |
| 集中视觉与release | **0/4待签收，release未完成** | 仍需最终自动门、真实设备/谱面/视觉签收与发行复核 |

产品价值按`真实caller → authoritative manager/backend → production consumer或直接用户结果`核算。C1新增的主要交付均有真实设置/导入caller；selection/import/ManagedCopy等包生效链另有BMS/mania consumer证据，Open/Rename/Delete/Unregister/support形成直接用户结果。大量Windows authority、journal/recovery与receipt代码直接防止外部源被写、错目标删改、partial copy和共享blob误删，不是用内部类型数量制造进度。仓库仍保留一个C1前已有、没有独立非测试caller的fixed-staging import surface；其StagedImport operation/handler仍无production caller，但共同的fixed-slot authority/native move+inspection及journal/coordinator/recovery框架已被ManagedCopy复用，因此既不计作额外用户功能，也不能把全部共同底层判成死代码。后续继续禁止无caller/consumer的foundation独占campaign。

## 七个交付 Campaign

`SV1-*` 是能力分类，`C1`～`C7` 才是交付燃尽。当前为 **`1/7 closed，C2 active`**：

`C1` 作者文件工作区/G1 UX ✓ → `C2` 当前consumer revision reload/detach → `C3` P1-K+唯一layout → `C4` shared codec/catalog/resolver/mania compatibility → `C5` scene/event与剩余slot production → `C6` sandbox并关闭最终整包reload门 → `C7` canonical双包/Authoring Kit/自动release。

C2不得只造manager-only reload API、same-ID selection或per-host reloadable。它需冻结真实可达触发方式及允许场景，并同切覆盖BMS geometry/Note/LN/pre-start preview、core/mania drawable、menu/shell/background/transition、ordinary Realm `.osk` owner生命周期和current external unregister。完整执行边界见[C1完成交接](../../other/SKIN_SYSTEM_C1_COMPLETION_HANDOFF_20260813.md)。

## 当前 gate

| Gate | 状态 |
| --- | --- |
| schema 56 数据安全 / 恢复基线实机 | **通过**；无authority orphan blob继续保全，不运行全局cleanup |
| `SV1-1` Note/LN首个产品纵切 | **自动门已闭合，视觉待签收**；`V-001`～`V-004` 为0/4，不等于`SV1-1`完成 |
| `SV1-2` / `C1` 作者工作区 | **通过**；真实caller/renderer、恢复、宽回归、Release、文档和独立终审已闭合 |
| `SV1-2` / `C2` current revision reload/detach | **active，未实现**；C1产生的active capsule仍不会因磁盘原位变化自动reload |
| `SV1-3`～`SV1-7` / G1最终门 / Skin V1 / release | **未完成** |

## 最新验证：2026-08-13

- `osu.Game` Debug build **0 error**，仅9个既有MessagePack `NU1902`；core C1 focused **490/490**。
- archive/receipt合并门 **84/84**；BMS产品组合 **118/118**；mania Skin **182/182**；BMS full **1586/1586**。
- core Skin **679/683**；4项失败均是依赖已移除Osu ruleset mode 0 fixture的已知OMS基线，不是C1回归。
- `osu.Desktop.slnf` Release **0 error**，仅9个既有MessagePack `NU1902`；external与receipt最终独立复审均为blocker/major/moderate **0/0/0**。

## 当前风险

- scanner仍只在启动后对账一次，不是watcher；managed新增/原位修改仍需重启，external active capsule也不会混入原位变化。
- C1的held-root copy/move/delete与journal/recovery不是filesystem transaction；foreign addition/replacement或证据漂移可导致冻结，这是fail-closed而不是all-or-nothing承诺。
- current external unregister必须由C2先发布coherent fallback/新revision并等待所有consumer detach；现有C1只允许pure-Realm noncurrent unregister。
- core Skin的4个已知fixture失败不得用于隐藏新回归；后续改shared importer/skin时仍须比对同一基线。
- 当前可见纵切仅覆盖BMS普通短键与LN head/body/tail；唯一layout、完整三态/scene/script与canonical fallback未完成。
- C1体量与集中度已形成维护风险，代码量本身不得作为进度。C2应从真实caller切入，提取小而封闭的revision/participant/lease协议并复用fixture，避免继续把生命周期复杂度堆入单一manager或复制超长场景测试；这不授权先造无caller framework，也不要求本轮回头重构C1。
