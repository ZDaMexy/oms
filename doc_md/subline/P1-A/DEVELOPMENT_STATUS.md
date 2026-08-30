# P1-A 当前状态：Skin V1、产品面与 release gate

> 最后更新：2026-08-30
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，稳定合同见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)。

## 一句话状态

`SV1-0` 自动、schema 56 数据与用户实机 gate 已全部通过；`SV1-1` 已导入 `.osk` 的 BMS Note/LN 纵切覆盖普通短键与长条 head/body/tail，`V-001`～`V-004` 仍为 **0/4**。`C1`～`C3` 均已闭合：作者工作区/archive、唯一Settings manual Reload与三源same-ID coherent publication、P1-K lane/keymode authority、唯一immutable gameplay layout及其完整production consumer、participant/lease/detach/retire与current mutation原子边界均已通过宽回归、Release和独立终审。当前为 **`3/7 closed，C4 active`**；G1最终整包门、`SV1-2`整体、`SV1-1`、Skin V1与release仍未完成。

## 当前产品能力

- **恢复基线可用**：`.osk`/legacy mania、BMS F1 静态颜色/纹理/几何、选择链与程序化 `OmsSkin` 迁移 fallback 保持可用。
- **已实现的可见纵切**：选中的用户BMS包可为普通短键与长条head/body/tail提供静态图或固定60 FPS连续编号帧；body宽度只接受finite且`0 < width <= 1`，否则回到`0.5775`。这四项仅自动gate通过，视觉签收仍为0/4。
- **C1作者工作区**：可在settings注册external目录、显式选择并从configured restart重捕，或导入新的managed direct child；external行可Open/Import/Unregister，managed行可Open/Rename/Delete。external始终只读，random/next/previous不会隐式选中它。
- **C1安全边界**：external source bytes只来自fresh held capture的immutable capsule，目录/空目录来自同次manifest；service-owner只授权Realm记录管理。所有managed mutation持有exact external registry物理证明至final Realm线性化，single v3 journal与recovery保护crash/cancel/partial copy。
- **ordinary `.osk` 安全导入**：仍是hash-backed Realm package；skin-scoped reader在内容消费前完成raw/central-directory/name/type/size有界准入，actual stream持续验证CRC/比率/总量/取消。fault/cancel的exact receipt只回滚本次新增的零引用record/blob，不会删共享hash。
- **C2产品触发已冻结**：Settings → Skin 的 `Reload current skin` 是唯一手动触发；Folder Skin Workspace不新增行级Reload，same-value selection不冒充reload，也不实现watcher。按钮覆盖ordinary Realm `.osk`、managed与external current source；live gameplay/gameplay preview在任何source prepare前确定性拒绝并反馈退出后重试。
- **C2生命周期已闭合**：immutable current revision先在后台完成全部I/O/capture/解析/资源和participant staged prepare，再由update-thread可回滚引用barrier一次发布；失败保留exact旧pair/revision，成功后旧owner只在consumer/work lease最后detach后exactly-once retire。完整participant/holder/bypass稳定分类见[技术约束](TECHNICAL_CONSTRAINTS.md)，诊断召回见[atomic reload/detach memory](../../../.Codex/memory/reference_skin_atomic_reload_detach.md)。
- **C2 mutation已闭合**：current external Unregister、current managed Delete与ordinary current `.osk` Delete都先发布受保护fallback并等待旧revision detach；external只做fresh exact Realm remove且source零I/O，managed只在此后进入C1 journal/physical边界。legacy Skin Editor、external-edit与update-import UI/backend均稳定禁用。
- **C3 P1-K authority已冻结**：decoder/parser与converter是keymode、lane count及keysound timeline的唯一truth；显式override、source precedence、extension/channel冲突与无证据fail-closed均产生稳定脱敏diagnostic。全部logical lane以`GetLaneCount()`为上界，5K/7K末键、9K全lane、14K右deck末键与Scratch2的visible、LN head/tail armed、invisible、mine及相邻armed timeline均不得静默丢失；player/autoplay通过同一`BmsKeysoundStore`真实发声。layout/skin/runtime不得重读BMS或从最高channel、layout宽度、enum位置或总lane数二次猜测。
- **C3唯一layout已冻结**：ruleset-neutral `GameplaySkinLayoutContext`、唯一immutable neutral snapshot/publication、BMS唯一solver与mania adapter绑定exact native context/keymode、topology、presentation style、safe bounds/aspect/DPI、package/current/content/topology/layout revision；构造后防御性不可变。5K/7K四style、9K BMS/PMS、14K双deck/S1/S2/centre gap及mania single/dual stage使用stable LaneId/GroupId和显式logical/visual/global/group-local index；Mirror/Random只改变对象目标lane，不改变固定topology。
- **C3 production与C2扩展已闭合**：BMS playfield/stage/group/lane、Note/LN、barline、hit/judgement line/target、lane cover、pre-start、BGA最终viewport、gauge/combo/HUD，以及mania playfield/stage/column/note/hold/hit target/judgement与core provider都只读同一exact publication；不存在第二套profile/default/fixed/local-offset求解。neutral snapshot与typed adapter作为单一publication引用在background prepare后由update thread一次提交；participant generation、selection/source/content/layout revision、fresh barrier、lease/detach、late attach、跨revision holder及最后detach exactly-once retire均沿C2协议复核，失败保留exact A package+layout pair。live gameplay/preview仍在source prepare前拒绝，不开放watcher或live reload。
- **C3最终验证**：P1-K decode/converter **176/176**、mania projection **24/24**、BMS真实keysound **14/14**、converted mania shared store **2/2**、BMS relevant **316/316**、mania C3 **27/27**、core final-audit **48/48**（此前focused **56/56**）、product concurrency **17/17**、storyboard **7/7**；core Skin **1164/1170**为精确既有六项失败，mania Skin **209/209**，mania full **854/858**为精确既有四项`AutoGeneration`失败，BMS Skin **802/802**，BMS full **1763/1763**且无hang；最终Release **0 error / 9 warnings**。
- **未交付**：C4的shared codec/public catalog/`Provide/Inherit/Suppress` resolver与mania parity、beatmap-local作者格式终态；C5 scene/animation/event与剩余optional slot；C6 sandbox/script VM及最终ini/manifest/scene/script/素材整包门；C7 canonical双包与Authoring Kit。BGA内容/timeline/seek仍归P1-L，程序化`OmsSkin`仍是迁移链底。

## 最终 Skin V1 差距总览

`3/7 closed`是硬退出门计数，不换算线性工期或完成度；各campaign不等权，后续scene、sandbox与canonical发行体量显著更大。当前按玩家结果分面如下：

| 产品面 | 当前结论 | 距最终预期 |
| --- | --- | --- |
| 恢复、数据与导入安全 | **通过** | 保持现有恢复/receipt边界，后续不得重做或放宽 |
| 作者目录工作区 | **C1通过** | 已有真实注册/选择/复制/Open/Rename/Delete/Unregister；原位即时reload不在C1 |
| current revision生命周期 | **C2+C3通过** | 三源same-ID package+layout单引用publication、完整participant/holder lease、detach/retire及current mutation已闭合；C4～C6新增consumer仍须同切加入协议 |
| 唯一layout | **C3通过** | P1-K authority、BMS唯一solver、mania adapter及BMS/mania/core全部production consumer已统一到同一exact immutable publication；禁止恢复第二套几何 |
| shared codec/catalog/三态 | **未实现** | 当前只有BMS Note/LN窄`Provide/Inherit`，尚无全slot `Suppress`、mania parity和完整结构化诊断 |
| scene/event与完整表现力 | **未实现** | 当前用户包仅覆盖窄静态/编号帧Note/LN；其余key/mine/judgement/gauge/combo/BGA/effect尚未进入公共声明式runtime |
| optional sandbox | **未实现** | 无受限VM、工具链、授权、预算、确定性、熔断与profiler |
| canonical发行闭环 | **未实现** | `oms-simple/oms-complex`、Authoring Kit、validator、只读恢复与程序化`OmsSkin`退役尚未交付 |
| 集中视觉与release | **0/4待签收，release未完成** | 仍需最终自动门、真实设备/谱面/视觉签收与发行复核 |

产品价值按`真实caller → authoritative manager/backend → production consumer或直接用户结果`核算。C1新增的主要交付均有真实设置/导入caller；selection/import/ManagedCopy等包生效链另有BMS/mania consumer证据，Open/Rename/Delete/Unregister/support形成直接用户结果。大量Windows authority、journal/recovery与receipt代码直接防止外部源被写、错目标删改、partial copy和共享blob误删，不是用内部类型数量制造进度。仓库仍保留一个C1前已有、没有独立非测试caller的fixed-staging import surface；其StagedImport operation/handler仍无production caller，但共同的fixed-slot authority/native move+inspection及journal/coordinator/recovery框架已被ManagedCopy复用，因此既不计作额外用户功能，也不能把全部共同底层判成死代码。后续继续禁止无caller/consumer的foundation独占campaign。

## 七个交付 Campaign

`SV1-*` 是能力分类，`C1`～`C7` 才是交付燃尽。当前为 **`3/7 closed，C4 active`**：

`C1` 作者文件工作区/G1 UX ✓ → `C2` 当前consumer revision reload/detach ✓ → `C3` P1-K+唯一layout ✓ → **`C4` shared codec/catalog/resolver/mania compatibility（active）** → `C5` scene/event与剩余slot production → `C6` sandbox并关闭最终整包reload门 → `C7` canonical双包/Authoring Kit/自动release。

C2/C3冻结边界不得退化成manager-only reload API、same-ID selection、per-host reloadable、分离的package/layout交换或consumer自建geometry：Settings唯一manual Reload、live gameplay/preview prepare前拒绝、三源same-ID package+layout publication、BMS/core/mania与shell生命周期participant、ordinary Realm `.osk` owner、current external/managed/ordinary mutation及legacy旁路均已进入统一协议；不得新增watcher。完整inventory见[技术约束](TECHNICAL_CONSTRAINTS.md)，C4工作门见[当前计划](DEVELOPMENT_PLAN.md)。

## 当前 gate

| Gate | 状态 |
| --- | --- |
| schema 56 数据安全 / 恢复基线实机 | **通过**；无authority orphan blob继续保全，不运行全局cleanup |
| `SV1-1` Note/LN首个产品纵切 | **自动门已闭合，视觉待签收**；`V-001`～`V-004` 为0/4，不等于`SV1-1`完成 |
| `SV1-2` / `C1` 作者工作区 | **通过**；真实caller/renderer、恢复、宽回归、Release、文档和独立终审已闭合 |
| `SV1-2` / `C2` current revision reload/detach | **通过**；只允许Settings显式manual Reload，磁盘变化仍不自动reload，live gameplay/preview明确拒绝 |
| `SV1-3` / `C3` P1-K前置与唯一layout | **通过**；authority、唯一immutable publication/solver+adapter、全部production consumer及C2 lifecycle扩展均已闭合 |
| `SV1-4` / `C4` shared codec/catalog/resolver/mania compatibility | **active**；保持C3 layout与revision合同，不提前实现C5+ |
| `SV1-5`～`SV1-7` / G1最终门 / Skin V1 / release | **未完成** |

## 最新验证：2026-08-30 C3闭门

- P1-K decode/converter **176/176**；mania projection **24/24**；BMS真实keysound **14/14**；converted mania shared `BmsKeysoundStore` **2/2**。
- BMS relevant **316/316**；mania C3 production/solver **27/27**；core layout/revision/ruleset-provider final-audit **48/48**，此前focused矩阵 **56/56**；product concurrency **17/17**；storyboard **7/7**。终审硬化后的直接production复验另为mania **51/51**、BMS **37/37**。
- core Skin **1164/1170**；六项与既有精确基线相同：4项removed-Osu `TestSceneBeatmapSkinResources`、1项default background cycling、1项Argon sample。mania Skin **209/209**；mania full **854/858**，四项均为精确既有`AutoGeneration` fixture失败。
- BMS Skin **802/802**；BMS full **1763/1763**且无hang。最终Release **0 error / 9 warnings**。
- targeted formatter曾把一个`[Cached]`显式field误写为field-attributed auto-property，而该形态不受当前DI source generator支持；已恢复显式backing field，并重新build、重跑上述focused/full与Release门。该事件是已修复的formatter/source-generator适配问题，不是产品runtime缺陷。
- 最终owner审计先跑红并闭合两个真实窗口：shared `GameplaySkinLayoutRevisionOwner`现于同一锁内拒绝exact root已有publication后的任何直接二次prepare，禁止cached descendant绕过ruleset helper；mania stage vector/topology/environment也全部移入fresh work lease与participant-generation之后的solve callback。BMS managed入口在任何config/skin/solve前对称拒绝compatibility token；explicit compatibility继续仅是detached solver/visual fixture opt-in，不能进入exact production tree。
- P1-K authority、唯一geometry、全部production consumer/reachable bypass、revision participant/owner与并发终审最终均为blocker/major/moderate **0/0/0**。

## 当前风险

- scanner仍只在启动后对账一次，不是watcher；managed新增目录仍需重启发现，已登记current source的原位内容只能由Settings显式Reload进入新revision，external active capsule不会自行混入磁盘变化。
- C1的held-root copy/move/delete与journal/recovery不是filesystem transaction；foreign addition/replacement或证据漂移可导致冻结，这是fail-closed而不是all-or-nothing承诺。
- current external unregister已先发布coherent protected fallback并等待旧revision detach，再fresh compare exact service-owner/record/current revision做pure-Realm remove；任一步失败恢复或保持旧pair/revision且source零变化。该边界随C2冻结。
- core Skin的6个已知fixture失败不得用于隐藏新回归；后续改shared importer/skin时仍须比对同一精确基线。
- 当前可见作者纵切仍只覆盖BMS普通短键与LN head/body/tail；唯一runtime layout已闭合，但完整public catalog/三态、scene/script与canonical fallback未完成。
- C1～C3体量与集中度已形成维护风险，代码量本身不得作为进度。C4新增codec/resolver/consumer必须复用C3 exact publication和C2 owner协议；不得把DTO、fixture或新旁路当作纵切，也不得重开live reload/watcher。
