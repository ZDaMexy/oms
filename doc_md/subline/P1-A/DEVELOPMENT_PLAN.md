# P1-A 当前计划：Skin V1、产品面与 release gate

> 最后更新：2026-08-09
> 主线顺序见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，硬约束见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)，逐切历史见 [CHANGELOG.md](CHANGELOG.md)。

## 子线目标

交付 Windows-only、离线优先的 Skin V1：同一公开外部皮肤路径同时支持 mania/BMS；引擎拥有 gameplay truth、布局、fallback、安全和资源预算，外部 package 拥有具体视觉、动画与对只读事件的表现响应。

完成下限与上限保持不变：

- `oms-simple.osk`：同时含 mania/BMS 的最小可玩包，承担只读 canonical 逐组件 fallback。
- `oms-complex.osk`：同时含 mania/BMS，只使用公开 API 证明接近 IIDX 复杂度的表达上限。
- `.osk`、根 `skin.ini`、mania 共同素材/帧命名、解包编辑和拖入导入继续遵循 osu 社区心智；BMS/scene/script 是版本化扩展，不要求作者编译 DLL。
- 程序化 `OmsSkin` 只在迁移期保留；`oms-simple` 达到 parity、完整性与恢复 gate 后退出产品渲染链。

不属于 V1：解析 LR2/beatoraja/IIDX 皮肤格式、捆绑商业素材、允许 package 修改输入/判定/计分/谱面/BGA timeline，或提前开放联网能力。

## 当前执行门

| 顺序 | 门 | 状态 | 通过条件 |
| --- | --- | --- | --- |
| 0 | `SV1-0` 恢复与数据安全 | 已完成 | 结果只在 STATUS/CHANGELOG 保留，不重开迁移或全局 cleanup |
| 1 | 文档与 memory 健康治理 | 已完成 | 当前事实、未来步骤、稳定合同和历史重新归位；无代码/gate 变化 |
| 2 | 已实现纵切的集中视觉验收 | **`V-001`～`V-004` 待用户签收** | Skin V1/release 完成声明前确认真实已导入 `.osk` 的普通短键与长条 head/body/tail、选择切换及 selected 坏包回落；另行决定是否扩入真实 beatmap-local 格式 |
| 3 | `SV1-1` 首个 Note/LN 产品纵切自动门 | **已闭合，视觉待验收** | ordinary note 与 critical head/body、optional tail 的静态图/60 FPS 连续编号帧已通过自动、合同、安全与回退 gate；只算首个产品纵切自动闭环，不计作 `SV1-1` 完成或产品交付 |
| 4 | `SV1-2` G1 安全存储与原子重载 | **进行中，`C1`待启动** | 启动发现/选择及现有settings managed delete玩家可达；rename/import后端仍无caller/stager/UI，thin/foundation-only stager当前NO-GO。`C1`必须依次关闭external注册/选择、full managed-copy stager、rename/delete UI、journal支持与`.osk` archive安全；current consumer revision协议归`C2`，最终整包reload/G1自动门到`C6`关闭 |
| 5 | `SV1-3`～`SV1-7` | 未完成 | 按以下依赖顺序分别过门，不并行宣称完成 |

视觉验收采用[集中清单](../../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)，不再作为逐组件串行开工门。自动、合同、安全与回退 gate 通过即可按依赖继续；待签收项只能称“实现／自动 gate 通过，视觉待验收”，不得称产品交付、`SV1` 阶段完成或 release gate 通过。仅当视觉结论实际决定后续设计或自动证据无法裁决异常时暂停请求反馈。首个 Note/LN 产品纵切已满足进入 `SV1-2` 的工程依赖，但 `SV1-1` 本身仍未完成；G1、layout、shared codec、scene/script 与 canonical fallback authority 仍只按各自切片修改。

beatmap-local 的相对 provider 顺序是已有自动合同，但当前真实 `WorkingBeatmap` 只产生不解析 `[Bms]` 的 `LegacyBeatmapSkin`；仓库也未定义 `.bme` 的逐谱侧车格式。因此现有注入式 fixture 只证明 provider-order，不证明 BMS 谱面本地素材已可用；若选择实现，必须作为独立作者格式/生产 adapter 纵切重新冻结。

## 七个后续新对话 Campaign 预算

`SV1-0`～`SV1-7`继续表示能力与依赖层，不再暗示协作轮数。自2026-08-09下一次新对话启动prompt起，当前已知的Skin V1/P1-A剩余范围——包括必须在对应campaign内取得产品终态并立即实现的路线决定——必须在最多七个持久campaign内收口；第七个campaign退出时只允许保留集中视觉、真实设备、长时间体验等人工签收。该承诺是**新对话prompt预算**，不是日历或单提交工期：一个campaign允许在同一对话内经历多轮、上下文压缩、多个有意义提交和多组测试；未过退出门不得生成下一个campaign prompt。

| Campaign | 映射 | 必须闭合的非人工产品结果 | 硬退出门 |
| --- | --- | --- | --- |
| `C1` 作者文件工作区与G1 UX | `SV1-2`前半 | external settings注册与独立registrations行级管理、strict Windows capture、service-owned Realm、typed configured-restart协调、选择/重启、BMS+mania具体artifact、Open Folder、pure-Realm noncurrent unregister；register/unregister与所有managed mutation共享exact集合线性化。随后以fresh-authoritative service-owned record只作provenance/admission；external capture成对产生exact capsule与含empty directory的immutable logical-tree manifest，文件bytes只来自capsule，destination handles只负责no-follow/no-replace重建。manager-owned full managed-copy与Folder Skin Workspace闭合external Open/Import/Unregister、managed Open/Rename/Delete；managed行级Delete只按committed record ID复用fresh专用`CanDelete`、既有确认语义及`DeleteSkinAsync`，不解冻旧通用delete。首个destination write前须有single canonical v3 combined intent，覆盖copy→ProvisionalReady→既有move/publish phase并durable绑定external registry generation/digest与non-overlap disposition；pre-C1 v2 schema永久冻结只读。journal支持面只提供只读状态/稳定原因、脱敏diagnostic bundle及证据唯一时的manager-owned恢复。普通`.osk`先在archive open前限制compressed source/bounded spool，再由自身受预算的metadata parser/open在任何`Filenames/GetStream/hash/Files.Add`消费前检查central-directory/name/declared budgets，实际流继续硬计数、取消与零残留 | 作者可从settings注册只读源、选择/重启渲染、导入managed副本，并从record行Open/Rename/Delete或Unregister；外部源永不被写改删；current/noncurrent managed delete均走同一专用authority，fallback只在current必需；并发external集合变化与全部managed mutation的admission/final collision持有同一版本physical proof；partial copy crash只清理exact OMS provisional且single journal无authority空窗；fresh recovery证明collision gate且不猜测旧v2；freeze有安全支持路径；真实Windows、恶意archive、crash/cancel/restart/concurrency及宽回归通过。不得让owner token授权source bytes，也不得留下无caller后端、thin/arbitrary-path stager或仅registry/capture foundation |
| `C2` 现有consumer revision publication / reload / detach | `SV1-2`后半产品门 | 同一campaign先用真实host证据冻结并取得所需产品确认，再立即实现唯一可达的V1触发方式与允许场景；候选至少比较safe author-preview host与安全导航点same-ID manual reload，不能把路线审计单独交接。建立ruleset-neutral package-revision prepare/commit barrier、attached consumer registry、revision lease/detach receipt与retire queue，覆盖当前存在的managed/external BMS geometry/Note/LN/preview、core/mania、shell/menu，并把普通Realm`.osk` selection的旧owner生命周期纳入同一协议；`ExternalEditOverlay`/update-import要么迁移到新协议和替代caller，要么稳定禁用，禁止保留new-instance后即时dispose旁路。完成current external fallback+unregister | 选定caller可真实触达当前完整participant集合；same-ID新revision全consumer coherent publication，任一失败保留exact旧pair/revision，最后detach后旧owner dispose once；普通`.osk`、managed、external不存在即时dispose/永久泄漏旁路，latest-wins/reentrant/cancel/shutdown/诊断通过。此门只关闭当前production consumer，C3～C6新增consumer必须同切加入协议，最终ini/manifest/scene/script整包reload gate到`C6`才关闭 |
| `C3` P1-K前置与唯一layout | P1-K Skin前置 + `SV1-3` | 修正lane-count/timeline上界，冻结sparse keymode source/override/diagnostic；交付唯一immutable layout context/snapshot/solver，5K/7K四style、9K BMS/PMS、14K DP全覆盖；playfield/lane/judgement/cover/BGA/gauge/combo/HUD及mania adapter只消费该snapshot。layout revision publication和所有新consumer同切加入C2 barrier/lease/detach协议 | 全矩阵lane identity/order/bounds/scratch/deck/BGA/HUD自动证明，非法geometry逐字段回落，生产路径无第二套几何推导或mixed revision；新增layout consumer均参与owner retirement，不得停在DTO/topology或把P1-K、BGA/HUD consumer推后 |
| `C4` shared codec、三态与mania compatibility | `SV1-4`并补`SV1-1`合同 | 冻结完整V1 public field/slot catalog与三态resolver；mania/BMS common ini迁至同一codec/resolver，BMS extension版本化；现有真实Note/LN及其它production consumer使用显式`Provide/Inherit/Suppress`、stable lane ID、结构化诊断、统一fallback/revision，并保持C2/C3 revision lease/detach协议。beatmap-local作者格式在同一campaign取得终态产品决定；若纳入V1就同切交付真实sidecar/producer/adapter，若不纳入则同步移出V1缺口，决策不能作为campaign终态 | `.osk`、managed、external三路径解析一致，mania/BMS common fixture parity，public catalog/resolver完整且现有真实consumer全部迁移；不得偷跑剩余optional slot的私有C# consumer、保留重复tokenizer/resolver、破坏owner协议或让beatmap-local范围继续悬空 |
| `C5` 声明式scene / animation / event runtime | `SV1-5`并关闭`SV1-1`自动门 | 交付versioned manifest/schema、allowlisted scene graph、sprite/container/text/mask/clip、animation/tween/state machine/property binding/template；完整只读事件family与Snapshot/Reset；BMS+mania production host覆盖global/lane/pooled Note/LN/HUD/judgement/gauge/BGA/effect，并让C4 catalog中剩余optional slots经scene hosts真正进入production；实施资源/节点/effect pool/每帧预算。每个scene host必须同切注册进C2 revision barrier、持有revision lease并在detach时ack，重跑publication/owner retirement矩阵 | 普通package无需C#即可驱动全部advertised V1 scene surface和全部public optional slot；seek/retry/reload可确定重建，dense/14K预算通过，异常只回落scene；scene consumer加入统一revision协议，此时才关闭`SV1-1`自动门。不得以manifest/DTO/fixture、单个host或未消费slot作为终态 |
| `C6` 可选脚本与隔离及最终整包reload门 | `SV1-6`并最终复核`SV1-2` | 同一campaign完成VM选型spike、所需产品确认与production实现，spike/决策不能作为终态。若选择in-tree bounded bytecode VM，必须同切交付无需DLL的package作者入口、版本化source/bytecode格式、compiler/verifier、malformed/untrusted bytecode验证、version reject/compat策略、source-mapped诊断与deterministic fixtures。无论选型均须闭合只读snapshot/event、授权scene node、四方capability协商、per-skin identity授权持久化/撤销/重协商、compiler/runtime版本失效与cache规则、永久hard-deny、instruction/heap/node/resource预算、deterministic clock/seed、seek/retry/reload、异步compile、熔断、profiler与授权UI；script host同切加入C2 revision lease/detach协议 | 真实BMS/mania host运行complex候选脚本；无限循环、超限、异常、取消/shutdown不阻塞update thread且只熔断脚本/scene；ini/manifest/scene/script/素材全部参与同一publication/detach/owner矩阵，至此关闭最终整包reload与G1自动门。不得只交选型文档、catalog、mock consumer，也不得把语言ABI/工具链、授权持久化、profiler或异常回落推给`C7` |
| `C7` canonical双包、Authoring Kit与自动release收口 | `SV1-7`并汇总`SV1-1`～`SV1-7` | 交付可编辑、可复现构建的`oms-simple.osk`/`oms-complex.osk`、模板、完整schema/event/layout/capability/budget文档、validator/diagnostics与打包导入说明；发行物只读携带、完整性验证/原子恢复。canonical fallback接管必须覆盖`SkinManager`初始/current/config失败pair、ruleset providing containers、selection/reload失败回落、current managed delete/current external unregister、protected Realm record。升级时仍存在且具备完整现行证据的supported pre-C1 v2及C1以后journal，可由旧`OmsSkin`证据继续恢复或显式版本迁移；缺tombstone/fingerprint/manifest/disposition的pre-product legacy-v1/old-v2 Delete继续strict Invalid并进入安装修复，绝不猜测迁移。之后才让程序化`OmsSkin`退出产品authority；canonical缺失/损坏必须阻止进入gameplay并进入明确安装修复，不能重新生成程序化视觉。第三方包、portable/custom-root/update、性能及全套自动门收敛 | 工程状态达到`SV1-1`～`SV1-7`“自动/合同/安全/release gate通过，人工待签收”；canonical切换前后的全部受支持journal/recovery、delete/unregister receipt与失败回落均可证明收口，invalid旧intent也有不扩大authority的安装修复路径；无程序化主题fallback、私有canonical特权、TODO validator/Authoring Kit或未归因自动失败，同时生成一键人工验收包，用户只需执行视觉/实机清单 |

Campaign执行规则固定如下：

1. 只读审计、GO/NO-GO、路线冻结、红测、foundation、DTO、单个consumer、单个提交或文档同步都不能独占一个campaign，也不能推进编号；它们只能是当前campaign的前段或组成部分。
2. 每个campaign最低终态为`产品红测 → runtime/backend → 真实UI caller → 全部声明涉及的production consumer → 失败回退/owner边界 → focused/full/Release → docs/memory → 独立终审 → 有意义提交`。
3. 当前campaign未闭合就留在同一对话继续；若必须由用户改变产品语义，则在同一对话等待，不生成新的handoff prompt来消耗预算。若提前闭合，直接在同一对话进入下一个campaign也允许，因此七个prompt是上限而非配额。
4. 人工签收发现的新缺陷形成新证据后仍须修复，但不能预先虚构其不存在；七个campaign承诺覆盖2026-08-09已知P1-A范围、各campaign内须取得终态的产品路线及明确的P1-K layout前置，不把P1-B/D/E/G其它产品子线偷塞进Skin预算。

## 未完成实施顺序

### SV1-1：共同合同与玩家可见纵切

已导入 `.osk` 的 BMS 普通短键与长条 head/body/tail 已闭合 selected-package `Provide/Inherit`、逐组件 fallback、exact revision、安全预算与静态图/编号帧动画自动门；详细过程只查 [CHANGELOG](CHANGELOG.md)。四项 `V-001`～`V-004` 仍待集中签收，因此该结果不能写成 `SV1-1` 完成或产品交付。

剩余 optional slot 不再沿私有逐件 C# provider/display 扩张，等待 shared scene/runtime 接管；真实 beatmap-local 作者格式也须单独冻结。后续组件仍须先明确 critical/optional 与 `Provide/Inherit/Suppress` 语义，保持 beatmap-local → selected → ruleset resources → protected fallback 的相对 authority，绑定 exact revision，并在集中清单登记受影响视觉项。

### SV1-2：G1 安全存储与原子重载

依赖：保持 `SV1-0` 数据处置结论与现有 `.osk` 路径稳定；不得从异常期存档整包恢复。受管目录active实例绑定immutable capsule，磁盘原地变化不会混入该实例，也不会自动reload。

已闭合的窄生产链为：schema 56-origin存储声明preflight → held-root Windows native no-follow capture → pure immutable capsule → exact-capsule `BmsLegacySkin` factory/guarded selection → schema 57 exact-owner单次启动发现/reconcile → 专用mutation authority/shared coordinator/versioned journal与启动recovery foundation → directory-only managed chartskin rename → fixed-source staged import → 现有settings delete button/dialog消费的独立`CanDelete`与async managed delete。发现/选择/delete有真实玩家consumer；rename/import是production程序集内的完整operation/recovery surface，但没有非测试caller、external→fixed provisional stager或UI。external分支仍无production capture/selection consumer并保持拒绝。旧folder mutation入口按authoritative ID冻结。各切片边界和验证只查 [STATUS](DEVELOPMENT_STATUS.md) 与 [CHANGELOG](CHANGELOG.md)，不能把lexical path、owner字段、capture request、scanner结果、publication plan或Prepared receipt本身当作玩家能力。

2026-08-09对current managed atomic reload/detach的独立产品审计没有找到可例外越过external顺序的闭合纵切：settings/config/hotkey只选择，scanner只做一次启动reconcile，filesystem-backed skin不能进入现有editor/external-edit，仓库没有reload current revision的production caller。现有manager只提交selection pair并广播`SourceChanged`；`BmsPlayfield`缓存geometry且不监听该事件，BMS Note/LN、core/mania drawable与菜单背景又各自在不同host、scheduler或过渡生命周期更新。没有全consumer snapshot/ack、detach receipt或旧owner retire机制。因此当前只冻结NO-GO和重新开门条件，不建立红测或reload foundation；审计证据保留在CHANGELOG，实时状态只看STATUS。

各切片按以下顺序独立过门：

1. **mutation authority/recovery foundation（已闭合）**：已有记录按ID刷新重读完整资格，既有source与fixed staging source由held native root固定no-follow identity，尚不存在的target只表示为root-bound规范化空name slot；staged新记录只生成planned ID/path/root/version的immutable publication plan而非Realm writer。scanner、selection、mutation/recovery共用线性化边界，版本化strict journal在首个外部步骤前durable落盘，启动先幂等恢复再scanner；有效歧义精确冻结、invalid/unknown/IO全局冻结，scanner negative cleanup服从冻结。程序化`OmsSkin` protected fallback pair门已被后续managed delete纵切直接消费；foundation本身不开放通用UI或任意Realm写入。
2. **rename（已闭合，UI仍冻结）**：工作区存储身份是`chartskin/<direct-child>`目录名，作者展示身份来自包内容。操作只移动direct-child目录并更新同一Realm record的managed path；不修改根`skin.ini [General] Name`、作者名、包字节、revision/hash或scanner owner。Prepared durable、held-root no-replace move、final identity、Realm一致性、恢复矩阵、selection/scanner竞态、取消与shutdown join保持为回归门。
3. **staged import后端（已闭合，尚不可由玩家触发）**：只消费固定`skin-mutation-staging/{operationId:N}`下由OMS为本operation持有的provisional副本。未来production stager必须独立完成external source authority、no-follow复制、预算、取消、失败清理与脱敏诊断，并保留外部原来源；当前仓库没有该stager或应用caller。后端以durable content revision + full physical-tree fingerprint固定Prepared，再同卷identity-preserving no-replace move并由one-shot publisher交接scanner owner；import不自动选择。
4. **startup selection/scanner竞态（已闭合）**：configured preparation以startup/generic mutation observation贯穿typed contention waiter、deferred scheduler callback与chained retry；只等待exact startup/staged-import completion，fresh retry重新做generation/current pair/Realm/path/owner/freeze/allowlist/capture/factory检查。manual managed请求与generic mutation继续fail-closed，普通Realm `.osk`不受影响；update thread不等待，shutdown统一cancel/reap/join。该矩阵保留为后续coordinator/scheduler变更的强制回归。
5. **managed delete（已闭合，现有settings入口玩家可达）**：现有delete button/dialog只通过独立`CanDelete`与`DeleteSkinAsync`进入专用operation；eligible managed direct-child的Prepared绑定operation-derived tombstone、exact Realm fingerprint、bounded source-node manifest与durable fallback disposition，再经held-root no-follow detach、fresh delete-exclusive DELETE-handle重捕、只删除manifest完整树/崩溃子集、authoritative Realm compare-remove及restart recovery收敛。same-session live重捕仍要求exact manifest，release窄窗移出在0次disposition时拒绝；exclusive handles只共享READ，取得后阻止已持有root/child被移出tombstone，但不冒充目录namespace lock。final preflight后竞态新增/replacement不被删除；若导致exact节点部分清理后root失败，则保留journal/Realm并冻结。current目标必须先真实提交exact protected `OmsSkin` pair；`NotRequired`只代表noncurrent，split/无效fallback拒绝，首步前authority drift在exact receipt仍可证明时安全回滚，receipt/写入漂移才冻结。首个物理步骤前可取消，之后只由journal/recovery决定结果；selection/scanner/generic mutation、queued fallback scheduler与shutdown join保持强制回归。旧`CanModify`/通用`Delete`继续冻结。
6. **thin staged-import stager/caller（当前NO-GO）**：后端虽已闭合，但external source→fixed provisional不是薄封装。只有可信source authority、handle-relative no-follow copy、容量/文件数预算、取消、失败/重启清理、脱敏诊断与真实应用caller在同一纵切冻结后才重新go/no-go；禁止任意path直传或只添无production consumer的stager抽象。
7. **external registration/capture（`C1`首个顺序子门）**：与受管目录mutation分离；external永久只读，只允许register/unregister，不提供物理rename/delete。只有settings目录选择caller、独立external registrations行级管理面、Windows no-follow完整capture、版本化service-owner Realm注册、exact-capsule factory/selection、配置重启、同一external capsule的BMS Note/LN与既有legacy mania note/hold最小production artifact、打开源目录及只解除注册能同切闭合时才GO；这不等于完整mania compatibility，也不等于`C1`退出。每个管理行持有已提交record ID并提供Open Folder与Unregister，后者只对noncurrent启用；不得复用只绑定`CurrentSkin`的既有Delete按钮/dialog。service-owner token只授权管理Realm记录，不授权path/source bytes；selection与冲突判断仍须fresh held physical root/ancestry proof。注册不自动选择，active实例不持续读取live source，原地变化不自动reload。首个纵切只允许解除注册coherent current pair两半都不指向目标的noncurrent记录；任一半指向目标或pair split时settings必须禁用且manager稳定拒绝，用户须先显式选择并真实提交另一个skin。unregister本身不dispose任何旧`Skin`/capsule，也不宣称consumer detach或owner retirement。只有全部真实managed mutation admission都能把相关external held proof保持并复验到final collision linearization point，才可把合法service-owned非重叠external的全局阻断收窄为局部冲突；否则本子门NO-GO并保留全局阻断。禁止只增加request/DTO/registry/capture backend后结束，也不得借本切预建reload barrier。
8. **`C1`作者文件工作区收口**：external子门通过后必须留在同一campaign。settings新增独立Folder Skin Workspace：external行持有committed record ID并提供Open Folder / Import Managed Copy / Unregister；exact scanner-owned managed行提供Open Folder / Rename Folder / Delete，普通Realm `.osk`不进入列表。所有动作只把record ID与必要用户输入交给manager并fresh重读；managed行级Delete调用fresh `CanDelete(record ID)`并复用现有确认语义与manager-owned `DeleteSkinAsync(record ID)`，现有current按钮可保留/委托同一surface，workspace不是第二authority。Unregister使用独立API/文案，Rename Folder只改direct-child且不复用会改展示名/`skin.ini`的旧popover。Import Managed Copy的target child由用户明确输入，manager生成operation ID/staging path；同一次fresh external capture成对产出exact capsule及含empty directory的immutable logical manifest，文件bytes只来自capsule，destination handle按manifest重建目录/文件，不重开external path。register/unregister commit与全部managed mutation共享有界exact external集合线性化，configured external restart延续typed startup/generic epoch合同。首个destination write前建立single canonical v3 combined intent，覆盖copy/ProvisionalReady与既有move/publish phase并持久化external collision disposition；v2永久冻结读取。ordinary `.osk`先做pre-open compressed length/bounded spool，再用自身有界的metadata parser/open检查central-directory后才允许任何archive内容消费，实际流硬计数。不得任意path、overwrite/merge/自动改名，也不得写改删external源。只有作者可完成注册→选择/重启→导入managed副本→managed行Open/Rename/Delete或pure-Realm解除注册的完整旅程，且malicious archive、crash/cancel/restart/concurrency与宽回归通过，`C1`才可关闭并进入`C2`。
9. **当前consumer revision publication / reload / detach（`C2`，当前NO-GO）**：计划顺序在完整`C1`之后，不是external子门之后；只有代码证据证明当前production路径可以闭合时才重新GO。产品必须在同一campaign冻结真实触发方式、live gameplay等允许场景，以及BMS playfield/Note/LN/preview、core/mania drawable、菜单背景等全部现有consumer的参与、coherent publication、detach acknowledgement与owner retirement协议。获准后仍须fresh authoritative Realm/path/owner/freeze/capture/factory复核，完整准备immutable capsule与新skin instance，以generation/current-selection/revision gate发布；任一准备或publication失败保留exact旧pair/revision，全部consumer detach后才dispose旧owner，并覆盖latest-wins、reentrant、首个不可逆边界前取消、shutdown exact claim/reap/join及脱敏诊断。`C3`～`C6`新增consumer须各自同切加入该协议，直到`C6`才关闭`ini/manifest/scene/script/素材`最终整包reload；在真实caller与全consumer协议可同切闭合前，禁止manager-only reload API、强制同ID selection、逐组件`SourceChanged`拼接或预建无consumer foundation。
10. **产品 UI/实机**：安全后端与真实caller闭合后才开放最小入口；明确区分受管目录物理删除与external解除注册，选择、重启、切换、rename/import/delete、缺件和原子替换统一进入最终人工清单。

验收：真实选择链、重启、切换、rename/import/delete、缺件、原子替换和备份数据根均通过自动与人工验证。

### SV1-3：playfield/BGA layout descriptor

依赖：P1-K 先给出可信 keymode source/diagnostic/override 并修正 lane timeline 上界；P1-L 保持 BGA 内容 authority 不扩张。

1. 定义 neutral `GameplaySkinLayoutContext` 与唯一 `BmsGameplayLayoutSnapshot`，覆盖 side/style、playfield/stage/lane/judgement/cover/BGA/HUD rect 和 stable lane identity。
2. playfield、gauge、combo fallback、BGA、scene/script 全部消费同一 snapshot，禁止各自 `CreateDefault()` 推导几何。
3. skin geometry 做 finite、正值、范围、屏内与不重叠校验；非法字段逐项回落默认。
4. 矩阵覆盖 5K/7K P1/P2/CenterP1/CenterP2、9K BMS/PMS、14K 双 deck/S1/S2/centre gap。
5. BGA decode/timeline/seek/POOR 留在引擎；多个 viewport 只能镜像同一只读 content authority。

验收：各矩阵格的 lane order/bounds/scratch role/BGA/gauge/combo 无冲突，并完成常见宽高比、DPI、每轨输入/keysound 与 BGA 实机检查。

### SV1-4：mania-compatible ini 共同层

依赖：SV1-3 冻结 stable lane/layout context；现有 legacy decoder 生产行为先由 fixture 保护。

1. adapter-first 导出带 explicit presence 的 neutral snapshot，稳定后再抽 shared codec；不在第一刀切换 mania 生产 tokenizer。
2. mania/BMS 共同字段使用同一 codec/resolver；BMS 独有字段进入版本化 extension schema。
3. 统一 mania column、BMS lane token 与 stable lane ID；renderer 不再拼接 lane 字符串。
4. 未覆盖共同件按冻结 mapping 显式进入 compatibility fallback。
5. 未知键、非法值、缺素材和不支持 capability 产生结构化诊断，加载继续 fail-open。

验收：同一 fixture 在 mania/BMS 共同件上解析一致，旧 `.osk/[Mania]` 与 `.osk/[Bms]` reference 继续可用。

### SV1-5：声明式 scene、动画与事件 ABI

依赖：SV1-3 layout snapshot 与 SV1-4 shared config 可用；引擎事件 authority 已明确。

1. 以稳定 node type allowlist 提供 sprite/container/text/mask、受控 effect、clip、frame animation、tween、状态机、property binding 和 template。
2. 引擎 adapter 发布 lifecycle/layout/input/object/judgement/score/timing/BGA 只读事件；attach/reload/seek/retry 必须产生完整 Snapshot/Reset。
3. global、lane template、pooled note/LN 与 ephemeral effect 分层；scroll/LN clipping/instancing 仍由引擎 host 驱动。
4. package 只能锚定 descriptor slot 或自身 scene，不能遍历 `DrawableRuleset` 父树。
5. 新 gameplay provider 使用显式三态结果，不改写 nullable `ISkin`/`Drawable.Empty()` 旧语义。

验收：只用公开 scene/event host 实现代表性的 key press、hit、LN、judgement、combo/gauge 与 BGA 装饰，dense/14K 不产生 per-note script churn。

### SV1-6：可选沙箱脚本

依赖：SV1-5 declarative runtime 先覆盖不需要脚本的共同能力；脚本选型须先做隔离和性能 spike。

1. 脚本只读 snapshot/event，只能操作获准视觉节点。
2. capability 由 package request、host allowlist、当前支持与 per-skin authorization 共同决定；网络、任意文件、反射、进程、线程、原生库、Realm/config/gameplay mutation 永久禁止。
3. gameplay clock、确定性 seed、seek/retry/reload 状态重建必须固定。
4. VM 提供可抢占 instruction/heap quota；资源、scene/effect pool 和每帧预算有界。
5. 编译与 IO 不阻塞 update thread；异常/超限只熔断脚本/scene 层并 fallback。

验收：权限逃逸、无限循环、内存、异常、determinism、seek/retry、热重载和低端硬件预算通过。

### SV1-7：双包、作者套件与 release gate

依赖：SV1-2～SV1-6 的产品能力真实存在，不用目标包反向伪造未实现 runtime。

1. 制作同时含 mania/BMS 的 `oms-simple.osk` 与 `oms-complex.osk`，均走普通导入/导出链且保留可编辑源。
2. `oms-simple` 只保留最小可玩件并 suppress 可选视觉，随发行物只读携带，构建/启动校验并可原子恢复。
3. `oms-complex` 覆盖公开 slot/event 表达上限，不使用私有 C# provider、隐藏资源或内置专权。
4. 交付模板、schema/event/layout/预算参考、validator/diagnostics 与打包说明。
5. `oms-simple` 达到 mania/BMS parity 后，程序化主题渲染退出产品链。

验收：缺失/损坏用户包仍可玩；canonical 包完整性失败进入明确安装修复；双包、第三方包、启动/切换/reload、全 keymode、BGA、脚本性能与人工视觉全部过门。

## 跨线依赖

| 子线 | 向 P1-A 提供 | P1-A 不得越权 |
| --- | --- | --- |
| P1-B/P1-D | 只读输入状态、真实硬件结果 | 不修改输入 edge/hold/calibration authority |
| P1-C/P1-E | 判定、LN/CN/HCN 与反馈语义 | 不由皮肤解释规则结果 |
| P1-H | 路径/authority/重扫经验 | 不直接复制谱面 scanner 的删除 authority |
| P1-J/P1-K | lane keysound proof、keymode/topology truth | 不由 renderer 二次猜 lane/keymode |
| P1-L | BGA timeline/content truth | 不让皮肤创建第二套 player/clock |
| P1-G | 用户实机与 release checklist 汇总 | 不用自动测试替代视觉/硬件结论 |

## 验证矩阵

| 变更面 | 最低自动 gate | 人工 gate |
| --- | --- | --- |
| BMS ruleset 内单一皮肤组件 | BMS skin focused + relevant/full + Release | 受影响 keymode、选择/切换/回落与新增视觉 |
| shared skin/mania compatibility/fallback authority | core skin + mania relevant + BMS relevant + Release | 双 ruleset、选择、fallback 与恢复 |
| G1/Realm/storage | importer/scanner/containment/selection focused + Release | 备份数据根、重启、删改、external/managed |
| layout/BGA | topology/layout/BGA focused + BMS full + Release | keymode/style/宽高比/DPI/逐轨/BGA |
| scene/event/script | ABI/order/fallback/capability/budget + full/Release | 长时间游玩、seek/retry/reload 与 profiler |
| release 双包 | canonical integrity/recovery + core/mania/BMS/Release | `oms-simple`、`oms-complex`、第三方 `.osk` |

## 兼容与回退

- 当前 `.osk/[Mania]`、`.osk/[Bms]`、nullable `ISkin`、选择链与程序化迁移 fallback 在对应替代 gate 前保持不变。
- 任一新切片失败时只回退该切片，不恢复异常期 G1/F2/Lua/mania adapter/reference-default 整包。
- 旧 F/G 术语只作为 CHANGELOG/恢复审计索引；当前执行只看 `SV1-*` 与本页当前门。
