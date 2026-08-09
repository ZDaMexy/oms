# Skin 系统产品进度交接：reload审计、产品价值与最终差距

> 日期：2026-08-09
> runtime基线：`cf0019e8a79c0213074b0a4816884d410c3ea987`；atomic reload NO-GO文档锚点：`91221176e20cef2180048255393f3ca9ba4d308f`
> 性质：面向下一会话的派生说明，不替代[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)、[PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md)或[TECHNICAL_CONSTRAINTS](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)。2026-07-31 audit与2026-08-01/02 handoff只作为历史快照，不能覆盖当前代码事实。

## 结论

本轮对“current managed skin atomic reload/detach能否作为独立产品纵切”作出明确**NO-GO**。没有新增runtime、测试API或reload foundation。

现行PLAN把external registration/capture放在整包atomic reload/detach之前。managed路径只有在现有真实caller、host和renderer足以让本切独立闭合时才允许例外提前；只读追踪证明该条件不成立：仓库没有“重载当前managed revision”的production caller，同时没有覆盖全部consumer的coherent publication/detach协议。为了写红测而先创造caller或barrier也会越过本轮入口门，不能算产品级红测先行。

对此前Skin投入的产品复核结论是：**大量工作没有白做，但产出结构明显偏向安全和合同地基，不能再按代码量或测试量计算产品进度。** 当前按release-ready玩家能力约完成三成；下一阶段必须以完整玩家纵切为单位推进。

## 产品价值核算

| 层级 | 已有结果 | 产品判断 |
| --- | --- | --- |
| 玩家可见功能 | 普通`.osk`导入/选择后，BMS Note与LN head/body/tail静态/编号帧进入真实renderer；合法managed目录可手工放入、重启发现、dropdown选择并从settings物理删除 | 是真实产品能力；但`V-001`～`V-004`仍为0/4，只能称实现/自动gate通过 |
| 直接保护真实功能的安全基础 | immutable capsule固定active revision；`551a`闭合configured managed selection与startup scanner竞态；coordinator/journal/recovery被settings delete真实消费 | 有明确用户价值：防止错误包发布、启动丢失配置及误删/损坏玩家目录，不是抽象美化 |
| 无caller后端 | directory-only rename与fixed-source staged import各有完整operation/recovery，但没有非测试caller、external→provisional stager或UI | 只算潜在风险资产，不算玩家功能；不得继续横向扩张 |
| future合同/未交付 | topology/config/event/capability的process-local fixtures，以及external、reload、layout、shared codec、scene/script、canonical双包 | 没有production host/renderer/authoring consumer的部分不计产品进度；reload NO-GO是正确止损 |

后续最低切片定义固定为：真实caller、全部相关production consumer、完整用户旅程、失败回退、owner安全归属/释放边界与自动/人工验收同切存在；只有声称释放或替换旧owner的切片才必须同切闭合consumer detach/retirement。复杂或工作量大不是拆成无consumer foundation的理由；若完整链无法闭合，应NO-GO而不是交付半条后端。

## 产品链证据

| 边界 | 当前代码事实 | 为什么不能闭合atomic reload/detach |
| --- | --- | --- |
| settings与其它入口 | settings dropdown只请求selection；启动配置、导入后展示与hotkey也只选择。same-value selection在准备前短路。startup scanner只做一次启动reconcile，不是watcher。filesystem-backed skin被editor、update import与external edit拒绝。 | 没有真实reload command、UI、watcher或manager API，无法定义用户动作、取消边界与允许场景。 |
| 看似可复用的reload | `ExternalEditOverlay`只为普通Realm skin重建实例，赋`CurrentSkin`后立即dispose旧实例；manager明确拒绝filesystem-backed skin进入该流程。 | 不是managed caller，也没有publication/detach barrier；即时dispose正是managed owner生命周期不能照搬的反例。 |
| manager publication | managed selection完成fresh Realm/path/owner/freeze/capture/factory后，只在manager内提交`CurrentSkinInfo`/`CurrentSkin` pair并广播`SourceChanged`。 | 没有package revision publication对象、consumer snapshot/registry、ack、detach receipt或旧instance retire queue；pair coherent不等于全renderer coherent。 |
| BMS playfield | `BmsPlayfield`在loader阶段读取一次`ISkinSource`并把geometry缓存到lane layout/profile，不监听`SourceChanged`。 | 即使Note/LN或shell换到新skin，layout仍可停在旧revision，直接形成mixed revision。 |
| BMS Note/LN | gameplay hitobject与pre-start speed preview分别创建独立`BmsAsyncNoteDrawable`；该类型只保证per-host异步generation/prepare/publish。 | 不存在package/playfield barrier，也没有host detach acknowledgement。 |
| core/mania/菜单 | `SkinReloadableDrawable`与core hitobject按drawable排scheduler；mania消费者混合同步、next-update和scheduler更新；菜单背景替换时旧背景会继续fade/expire并持有旧`Skin`。 | 同一`SourceChanged`存在多个可观察发布时间点，manager不知道全部旧consumer何时脱离。 |
| owner | exact capsule经factory转入skin owning store；`Skin.Dispose()`释放textures、samples、fallback store/capsule，BMS dispose还取消package note preparation。成功selection没有退役旧active skin的协议。 | 过早dispose可能让旧consumer引用失效；不dispose则旧owner生命周期无法闭合。现有产品测试只能手工dispose superseded managed skin。 |
| tests | 现有测试证明capsule/store ownership、guarded selection和per-host A→B，不证明same-ID revision gate、全host barrier、失败保留exact旧pair/owner、detach后dispose once或reload cancel/reentrant/latest-wins/shutdown join。 | 直接加此类测试必须先决定caller与consumer参与合同，不能在本轮偷偷发明产品路线。 |

## 离最终Skin V1预期的实现总览

| 阶段 | 当前真实产品表现 | 主要缺口与顺序 |
| --- | --- | --- |
| `SV1-0` | **完成**：schema 56清点、定点数据处置、自动与恢复实机门闭合 | 已闭门，不重开；价值是可信数据基线，不是新增作者能力 |
| `SV1-1` | **部分**：selected `.osk`/managed BMS包的Note与LN head/body/tail静态/60 FPS编号帧、`Provide/Inherit`、逐组件回落、exact revision、body宽度安全域与状态宿主进入真实链 | 缺`Suppress`、其它slot、真实beatmap-local作者格式、完整shared resolver/mania compatibility；`V-001`～`V-004`为0/4。optional视觉等待scene/runtime，不再逐件堆C# |
| `SV1-2` / G1 | **进行中**：managed目录可重启发现、选择和settings物理删除；rename/import只有安全后端 | 缺external register/capture/select/unregister、可信stager/import UX、atomic reload/detach与完整G1人工矩阵。先external完整纵切，再冻结reload路线 |
| `SV1-3` layout | **目标未实现**：neutral topology与`LongNoteBodyWidth`单字段resolver只是前置 | 先由P1-K修正keymode/lane timeline，再建立5K/7K四style、9K BMS/PMS、14K DP唯一layout snapshot；当前playfield/HUD/BGA仍可能脱节 |
| `SV1-4` shared codec | **目标未实现**：decoder-time presence/provenance及candidate fixture不等于production | 依赖stable lane/layout；缺共享resolver/codec生产消费、完整字段与结构化诊断 |
| `SV1-5` scene/event | **目标未实现**：event envelope/cursor/capability negotiation仍是process-local合同 | 依赖`SV1-3/4`；缺manifest/schema、allowlisted scene graph、状态机、真实event adapter/host/renderer与Snapshot/Reset |
| `SV1-6` sandbox | **未实现**：hard-deny合同不等于VM | 依赖声明式runtime；缺权限隔离、可抢占instruction/heap/node/resource预算、determinism、seek/retry/reload与熔断 |
| `SV1-7` canonical/release | **未实现**：reference ini不是交付包，程序化`OmsSkin`仍是链底 | 缺`oms-simple.osk`、`oms-complex.osk`、validator/diagnostics、模板、Authoring Kit、canonical完整性/恢复及全人工/release gate；依赖`SV1-2`～`SV1-6`真实存在 |

按最终release-ready玩家能力审慎估计当前为**27%～32%（约三成）**。工程与安全地基约为**45%～55%**，但不能把后者直接写成产品完成度：该区间没有可线性外推的工期含义，也不是gate。`SV1-3`～`SV1-7`目标产品尚未形成、external/reload未交付、canonical双包不存在且视觉签收0/4。它也不应低于约四分之一，因为恢复门完整关闭，Note/LN确实进入renderer，managed discovery/selection/delete真实可达，Windows capture、journal/recovery与selection链已有强自动证据。

三个现实体验缺口必须与“约三成”同时阅读：managed作者流程仍依赖手工放置目录和重启，没有注册/import/rename/reload UX；`V-001`～`V-004`仍为0/4，自动gate没有证明最终观感；invalid/IO journal freeze虽然优先保护数据，却没有用户可见的支持/修复界面，异常时可能安全但不可自助地卡住mutation。

稳定依赖主链为：**external G1完整纵切 → reload路线决策与条件GO实现 → P1-K前置 → `SV1-3` → `SV1-4` → `SV1-5` → `SV1-6` → `SV1-7` → 集中视觉/性能/release**。

## 协作粒度纠偏：七个持久新对话预算

同日用户指出`SV1-0`～`SV1-7`已经被远多于八轮协作拆解，阶段编号不应掩盖prompt过小。现改用[P1-A PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md)中的`C1`～`C7`燃尽：`C1`作者工作区/G1 UX与archive安全、`C2`当前consumer reload/detach、`C3`P1-K+layout、`C4`shared codec/catalog/resolver、`C5`scene/event及剩余slot production、`C6`sandbox及最终整包reload门、`C7`canonical/Authoring Kit/自动release。当前为`0/7 closed，C1待启动`。reload触发/允许场景、beatmap-local V1范围与VM选型仍须在相应campaign同一对话取得产品决定并立即实现，不能预写成未经授权的事实，也不能单独消耗campaign。

一个campaign是一段持久对话，不是一个turn、一个commit或一次上下文窗口；允许多轮、compaction和多个有意义提交。audit、NO-GO、路线冻结、红测、foundation/DTO、单个caller/consumer或文档不能作为campaign终态。未闭合就留在同一对话继续，必须等待用户产品选择时也不生成下一handoff prompt；若提前完成则可在原对话直接进入下一campaign。`C7`退出时，当前已知非人工Skin V1范围必须全部收口，只准保留人工视觉、真实设备与长时间体验签收。人工反馈产生的新缺陷属于未来新证据，不得预先假定不存在。

## 已有能力可复用，但不足以成为reload foundation

- managed selection已有fresh authoritative Realm/path/owner/freeze/capture/factory复核、immutable capsule/new instance准备、generation/current pair、latest-wins/reentrant和shutdown ownership基础。
- `551a64af3bc2958db4baa57421b73fee61f259ac`闭合的typed startup/staged-import completion retry、generic mutation epoch fail-closed、update-thread non-blocking及exact claim/reap/join仍是未来强制回归。
- managed delete的journal/recovery、current fallback与detach后由durable状态收口是独立删除合同；它不提供reload的全consumer publication/detach barrier，也不得被横向复用成通用skin lifecycle事务。
- `SkinReloadableDrawable`、`BmsAsyncNoteDrawable`与`SourceChanged`都只是局部更新机制，不是整包原子reload。当前不可把这些部件重新命名为foundation来计进度。

## atomic reload重新开门前的产品决策

1. 冻结唯一真实触发方式：例如settings手动reload、受控watcher，或只允许安全导航点重载；不能同时留给实现猜测。
2. 冻结允许场景：是否允许live gameplay中发布；若不允许，必须定义明确的defer/reject边界与用户反馈。
3. 列出并冻结全部consumer参与模型，至少覆盖BMS playfield geometry、Note/LN gameplay、pre-start preview、ruleset/core/mania drawable、shell与菜单背景。
4. 定义单一revision publication barrier、每个consumer的attach/detach acknowledgement和old-owner retirement；任何consumer失败都必须保留exact旧revision，不能留下半数新、半数旧。
5. 再建立真实Windows`chartskin/<direct-child>`产品级/headless红测，覆盖阻塞consumer、preparation失败、same-ID revision、stale/reentrant/latest-wins、首个不可逆边界前取消、shutdown join、旧owner仅在全部detach后dispose once，以及脱敏诊断。

只有真实caller、全consumer publication/detach、失败保留旧实例和owner生命周期能在同一纵切闭合时才重新判GO。否则继续NO-GO，禁止manager-only reload API、强制同ID selection、逐组件`SourceChanged`拼接、即时dispose旧owner或无consumer DTO/barrier抽象。

## 后续高价值工作包：external只读作者工作区

下一实施候选不是external foundation，而是一条可由作者验证的完整链：

`settings添加外部皮肤文件夹及独立registrations行级管理 → Windows resolved-identity/no-follow完整capture → versioned service-owner Realm注册 → existing dropdown选择/配置重启 → BMS Note/LN及legacy mania note/hold最小renderer artifact → 行级打开源目录/只解除注册且绝不物理修改源`

冻结边界如下：

1. 用户明确选择根含`skin.ini`的package目录；external源永久只读。register/select/restart/unregister不隐式复制、写入、重命名或删除源。后续`C1`同campaign的独立Import Managed Copy必须由用户明确触发，以fresh held physical proof向OMS fixed provisional staging做no-follow复制，绝不修改外部源；这不是thin/arbitrary-path stager。
2. `NativeStorage`只作source adapter；strict local physical Windows root必须以逐段no-follow identity、完整稳定inventory、预算与package metadata生成immutable capsule，运行实例不得持续读取live store。
3. 完整capture/factory成功后才原子发布带独立versioned service-owner token的Realm记录；token只证明本服务可管理记录，不授权path/source bytes或physical identity。重复lexical/physical identity、null/foreign旧记录、reparse/hardlink/busy writer、mapped/SUBST/UNC/device/root或managed重叠全部fail-closed。
4. 注册不自动选择；记录进入现有dropdown。每次用户选择或configured restart都fresh held capture，active实例绑定exact capsule；原位修改不污染current，same-value不冒充reload。验收只冻结BMS Note/LN与既有legacy mania note/hold各一个具体artifact，不据此宣称完整mania compatibility。
5. settings新增独立external registrations列表/行级管理面；每行持有已提交record ID并提供Open Folder与Unregister，不得复用只绑定`CurrentSkin`的既有Delete按钮/dialog。rename/export/editor/update-import保持冻结，“删除”文案必须明确为解除注册，由manager-owned async record-ID入口完成。首个纵切只允许noncurrent unregister：`CurrentSkinInfo`/`CurrentSkin`两半都不指向目标时才可执行；任一半指向目标或pair split时行级操作禁用且manager稳定拒绝，用户须先显式选择并真实提交另一个skin。Realm失败保留记录，任何结果都保留物理源；unregister不dispose任何prior `Skin`/capsule，也不宣称consumer detach或owner retirement。
6. 要把当前“任一external阻断全部managed mutation”的临时策略收窄为局部冲突，每个真实managed mutation admission都必须fresh重取相关service-owned记录，并把external physical root/ancestry held proof保持、复验到该operation的final collision linearization point，再与held managed root/source/target比较。若rename、staged-import、managed delete及竞态测试不能同切闭合，则external纵切NO-GO并保留全局阻断；owner token或瞬时resolve都不够。
7. settings caller、capture、Realm、selection/restart、上述具体renderer artifact、unregister、取消/shutdown、脱敏诊断与真实Windows产品/headless测试必须同切闭合。若只能交付request/DTO/registry/capture service，应STOP而非提交半条纵切；不得借external切片预建reload barrier。

本工作包不包含watcher、reload、thin stager、任意path import、逐件optional slot、layout/shared codec、scene/script或canonical包。完成后再对手动settings reload、允许场景与全consumer协议做独立路线决策。

## 本轮验证与下一入口

- atomic reload的三路独立追踪对caller/renderer、consumer publication及capsule owner/tests得出一致NO-GO；追加的产品价值、最终差距与后续策略三路审计又一致确认：现有真实玩家链与安全投入有价值，但无caller后端不得再计产品进度，external完整作者工作区是P1-A/`SV1-2`下一工程GO/NO-GO候选。
- 没有runtime或测试文件变更，因此未运行focused/full、targeted formatter或Release。最近代码基线仍是2026-08-02记录的core managed **281/281**、Windows native delete **11/11**、managed selection/settings **62/62**、mania skin **182/182**、BMS full **1530/1530**、core skin **911/917**与mania full **827/831**同组既有失败、Release **0 error / 20 known warnings**。
- `CheckDocumentation.ps1`通过（132个Markdown、1042个相对链接、72个memory wiki链），仅有mainline PLAN数字比值的既有非失败提醒；`git diff --check`通过。external边界与后续`C1`～`C7`范围/依赖分别经过独立终审，最终blocker/major/moderate为 **0/0/0**。
- external注册/选择子门仍排在full managed-copy stager与atomic reload/detach之前；thin/arbitrary-path/foundation-only stager保持NO-GO，`C1`要求的fresh-authoritative full product stager只有在external子门成功且UI/恢复/测试同切时才GO。reload归`C2`并在其全consumer路线实际闭合前保持NO-GO。后续不得把本审计写成G1、Skin V1或release通过，也不得为追求提交量拆回无caller foundation。
