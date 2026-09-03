# P1-A 当前计划：Skin V1、产品面与 release gate

> 最后更新：2026-09-03
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
| 2 | 已实现纵切的集中视觉验收 | **`V-001`～`V-004` 待用户签收** | Skin V1/release 完成声明前确认真实已导入 `.osk` 的普通短键与长条 head/body/tail、选择切换及 selected 坏包回落；C4 已决定不新增 beatmap-local 作者格式 |
| 3 | `SV1-1` 首个 Note/LN 产品纵切自动门 | **已闭合，视觉待验收** | ordinary note 与 critical head/body、optional tail 的静态图/60 FPS 连续编号帧已通过自动、合同、安全与回退 gate；只算首个产品纵切自动闭环，不计作 `SV1-1` 完成或产品交付 |
| 4 | `SV1-2` G1 安全存储与原子重载 | **进行中，`5/7 closed，C6 active`** | C1作者工作区/archive、C2当前consumer revision、C3唯一layout、C4 shared codec/catalog/resolver/material与C5 scene/event/剩余slot production均已闭合；C6进入sandbox与最终整包reload门 |
| 5 | `SV1-5`～`SV1-7` | 未完成 | `SV1-3`与`SV1-4`已分别随C3/C4闭合；其余按以下依赖顺序分别过门，不并行宣称完成 |

视觉验收采用[集中清单](../../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)，不再作为逐组件串行开工门。自动、合同、安全与回退 gate 通过即可按依赖继续；待签收项只能称“实现／自动 gate 通过，视觉待验收”，不得称产品交付、`SV1` 阶段完成或 release gate 通过。仅当视觉结论实际决定后续设计或自动证据无法裁决异常时暂停请求反馈。首个 Note/LN 产品纵切已满足进入 `SV1-2` 的工程依赖，但 `SV1-1` 本身仍未完成；C3 layout与C4 public material合同已冻结，G1最终整包门、scene/script与canonical fallback authority仍只按后续各自切片修改。

C4 终态决定是不新增 beatmap-local gameplay-skin 作者格式：当前没有安全的sidecar命名、producer/importer、`WorkingBeatmap` public document/revision authoring ownership、C1 capture/archive或C2 same-ID publication闭环，公共catalog/source kind与production candidate均不暴露虚假入口。真实importer/manager仍让`WorkingBeatmap.Skin`惰性返回同一只读`LegacyBeatmapSkin`实例，继续提供高于selected package的既有direct visual compatibility，但不消费公共author section；作者应使用ordinary `.osk`、managed `chartskin/<包>/` 或已注册external包。未来若重开，必须以独立产品gate一次性交付完整安全与双ruleset闭环。

## 七个持久 Campaign 预算

`C1` 已于2026-08-13闭合，`C2` 已于2026-08-24闭合，`C3` 已于2026-08-30闭合，`C4` 已于2026-09-02闭合，`C5` 已于2026-09-03闭合，当前为`5/7 closed，C6 active`。C1冻结边界见[C1完成交接](../../other/SKIN_SYSTEM_C1_COMPLETION_HANDOFF_20260813.md)，C3见[C3完成交接](../../other/SKIN_SYSTEM_C3_LAYOUT_COMPLETION_HANDOFF_20260830.md)，C4见[C4完成交接](../../other/SKIN_SYSTEM_C4_CODEC_MATERIAL_COMPLETION_HANDOFF_20260831.md)，C5见[C5完成交接](../../other/SKIN_SYSTEM_C5_SCENE_EVENT_COMPLETION_HANDOFF_20260903.md)；当前事实与最终验证见[当前状态](DEVELOPMENT_STATUS.md)和[变动日志](CHANGELOG.md)，完整inventory、owner、layout、material、scene与event合同见[技术约束](TECHNICAL_CONSTRAINTS.md)，C6执行门直接由本计划维护。

`SV1-0`～`SV1-7`继续表示能力与依赖层，不再暗示协作轮数。自2026-08-09的campaign启动prompt起，已知Skin V1/P1-A剩余范围——包括必须在对应campaign内取得产品终态并立即实现的路线决定——必须在最多七个持久campaign内收口；第七个campaign退出时只允许保留集中视觉、真实设备、长时间体验等人工签收。该承诺是**campaign prompt预算**，不是日历或单提交工期：一个campaign允许在同一对话内经历多次交互、上下文压缩、多个有意义提交和多组测试；未过退出门不得生成后续campaign prompt。

| Campaign | 映射 | 必须闭合的非人工产品结果 | 硬退出门 |
| --- | --- | --- | --- |
| `C1` 作者文件工作区与G1 UX | `SV1-2`前半 | **已闭合**：external只读注册/选择/configured restart/Open/pure-Realm noncurrent Unregister，Folder Skin Workspace的managed Open/Rename/Delete、single-v3 full ManagedCopy/exact-set recovery，以及ordinary `.osk` bounded ingress/zero-residue receipt均进入真实caller；selection/import/ManagedCopy包生效旅程进入BMS/mania consumer，其余动作形成直接用户结果 | Windows/archive/crash/cancel/restart/concurrency宽回归、Release、文档与独立终审已过；保留external永久只读、held proof到final Realm线性化、v1/v2 strict frozen、thin/arbitrary-path stager冻结等合同 |
| `C2` 现有consumer revision publication / reload / detach | `SV1-2`后半产品门 | **已闭合**：唯一入口为Settings current manual Reload；live gameplay/preview在source prepare前拒绝，不实现watcher。ruleset-neutral immutable revision、background prepare、update-thread rollback barrier、participant registry、lease/detach receipt与retire queue覆盖ordinary `.osk`、managed、external及现有BMS/core/mania与shell生命周期holder；无staged receipt的attached consumer fail-closed。current external/managed/ordinary mutation先fallback+detach；legacy editor/external-edit/update-import稳定禁用 | 真实caller/renderer三源A→B、participant/prepare失败保A、动态attach/detach/late attach、跨fade/sample/materializer holder、latest-wins/reentrant/cancel/scheduler/shutdown、最后detach exactly-once retire与current mutation失败原子性已通过focused/full/Release、文档门与独立终审。C3 layout、C4 material与C5 scene/event consumer已同切加入；C6新增consumer继续遵守；最终ini/manifest/scene/script整包门仍到C6 |
| `C3` P1-K前置与唯一layout | P1-K Skin前置 + `SV1-3` | **已闭合**：parser/converter唯一keymode与lane-count authority、`GetLaneCount()` keysound timeline上界、sparse source/override/fail-closed脱敏diagnostic及玩家/autoplay共享store真实发声均已冻结；唯一ruleset-neutral immutable context/snapshot/publication、BMS solver与mania adapter覆盖5K/7K四style、9K BMS/PMS、14K DP和mania single/dual，全部BMS/mania/core production consumer只读同一exact publication | stable identity/显式index、全矩阵bounds/scratch/deck/centre gap/BGA/HUD、逐字段geometry fallback与mod后LaneId均已自动证明；neutral+typed adapter单引用publication及所有layout consumer已加入C2 participant/generation/lease/detach/retire协议，失败保A、live reject、late attach与exactly-once retire通过并发/全量/Release与独立终审 |
| `C4` shared codec、三态与mania compatibility | `SV1-4`并补`SV1-1`合同 | **已闭合**：core冻结28项版本化public catalog、唯一shared tokenizer/codec、显式`Provide/Inherit/Suppress` resolver、exact target/stable ID/index验证与稳定脱敏diagnostic；BMS Note/LN和mania Note/Hold/KeyVisual从ordinary/managed/external真实current revision准备同一immutable material set，C2+C3扩为package+layout+material单引用publication。legacy BMS候选顺序与9K映射版本化；beatmap-local新作者格式明确排除，既有直接视觉兼容保留 | 三源public Provide与invalid/suppress旅程、BMS 5K/7K/9K/14K及mania single/dual、mod后LaneId、失败保A、动态attach/detach/late attach/retire、宽测试/Release/文档和四类独立终审均过门；**C4当时**剩余optional slot仅有capability diagnostic，不冒充C4 consumer，现已由C5 scene/runtime接管 |
| `C5` 声明式scene / animation / event runtime | `SV1-5`并关闭`SV1-1`自动门 | **已闭合（2026-09-03）**：versioned manifest/schema、allowlisted scene graph、sprite/container/text/mask/clip、blend/effect preset、frame/tween/state machine/property binding/template/instance、完整只读事件family与Snapshot/Reset均从exact package background prepare进入真实BMS/mania renderer；BMS/mania production host覆盖global/stage/group/lane、pooled Note/LN/Hold、HUD/judgement/gauge/BGA/effect及全部适用public slot。资源/节点/effect/text/event/pool预算、epoch/revision与C2 participant/lease/detach/retire同切闭合；mania Mine/BGA viewport/BGA frame以版本化runtime profile明确NotApplicable，BMS 28项均有route。 | 普通package无需C#即可驱动全部适用advertised V1 scene surface与public slot；seek/retry/reload/rewind可确定重建，dense/14K预算通过，异常只回落scene/slot；真实ordinary/managed/external caller、core/mania/BMS focused/full/Release、P1-K与四类终审均有证据。C6仍负责sandbox与最终ini/manifest/scene/script/素材整包reload门，不得把其提前算入C5 |
| `C6` 可选脚本与隔离及最终整包reload门 | `SV1-6`并最终复核`SV1-2` | 同一campaign完成VM选型spike、所需产品确认与production实现，spike/决策不能作为终态。若选择in-tree bounded bytecode VM，必须同切交付无需DLL的package作者入口、版本化source/bytecode格式、compiler/verifier、malformed/untrusted bytecode验证、version reject/compat策略、source-mapped诊断与deterministic fixtures。无论选型均须闭合只读snapshot/event、授权scene node、四方capability协商、per-skin identity授权持久化/撤销/重协商、compiler/runtime版本失效与cache规则、永久hard-deny、instruction/heap/node/resource预算、deterministic clock/seed、seek/retry/reload、异步compile、熔断、profiler与授权UI；script host同切加入C2 revision lease/detach协议 | 真实BMS/mania host运行complex候选脚本；无限循环、超限、异常、取消/shutdown不阻塞update thread且只熔断脚本/scene；ini/manifest/scene/script/素材全部参与同一publication/detach/owner矩阵，至此关闭最终整包reload与G1自动门。不得只交选型文档、catalog、mock consumer，也不得把语言ABI/工具链、授权持久化、profiler或异常回落推给`C7` |
| `C7` canonical双包、Authoring Kit与自动release收口 | `SV1-7`并汇总`SV1-1`～`SV1-7` | 交付可编辑、可复现构建的`oms-simple.osk`/`oms-complex.osk`、模板、完整schema/event/layout/capability/budget文档、validator/diagnostics与打包导入说明；发行物只读携带、完整性验证/原子恢复。canonical fallback接管必须覆盖`SkinManager`初始/current/config失败pair、ruleset providing containers、selection/reload失败回落、current managed delete/current external unregister、protected Realm record。升级时仍存在且具备完整现行证据的supported pre-C1 v2及C1以后journal，可由旧`OmsSkin`证据继续恢复或显式版本迁移；缺tombstone/fingerprint/manifest/disposition的pre-product legacy-v1/old-v2 Delete继续strict Invalid并进入安装修复，绝不猜测迁移。之后才让程序化`OmsSkin`退出产品authority；canonical缺失/损坏必须阻止进入gameplay并进入明确安装修复，不能重新生成程序化视觉。第三方包、portable/custom-root/update、性能及全套自动门收敛 | 工程状态达到`SV1-1`～`SV1-7`“自动/合同/安全/release gate通过，人工待签收”；canonical切换前后的全部受支持journal/recovery、delete/unregister receipt与失败回落均可证明收口，invalid旧intent也有不扩大authority的安装修复路径；无程序化主题fallback、私有canonical特权、TODO validator/Authoring Kit或未归因自动失败，同时生成一键人工验收包，用户只需执行视觉/实机清单 |

Campaign执行规则固定如下：

1. 只读审计、GO/NO-GO、路线冻结、红测、foundation、DTO、单个consumer、单个提交或文档同步都不能独占一个campaign，也不能推进编号；它们只能是当前campaign的前段或组成部分。
2. 每个campaign最低终态为`产品红测 → runtime/backend → 真实UI caller → 全部声明涉及的production consumer → 失败回退/owner边界 → focused/full/Release → docs/memory → 独立终审 → 有意义提交`。
3. 当前campaign未闭合就留在同一对话继续；若必须由用户改变产品语义，则在同一对话等待，不生成新的handoff prompt来消耗预算。若提前闭合，直接在同一对话进入下一个campaign也允许，因此七个prompt是上限而非配额。
4. 人工签收发现的新缺陷形成新证据后仍须修复，但不能预先虚构其不存在；七个campaign承诺覆盖2026-08-09已知P1-A范围、各campaign内须取得终态的产品路线及明确的P1-K layout前置，不把P1-B/D/E/G其它产品子线偷塞进Skin预算。

## 当前与后续实施顺序

### SV1-1：共同合同与玩家可见纵切

已导入 `.osk` 的 BMS 普通短键与长条 head/body/tail 已闭合 selected-package `Provide/Inherit`、逐组件 fallback、exact revision、安全预算与静态图/编号帧动画自动门；详细过程只查 [CHANGELOG](CHANGELOG.md)。四项 `V-001`～`V-004` 仍待集中签收，因此该结果不能写成 `SV1-1` 完成或产品交付。

剩余 optional slot 不再沿私有逐件 C# provider/display 扩张；C5 shared scene/runtime 已接管全部可达 public slot，并以 BMS 28 项、mania 23 Supported + 5 版本化 NotApplicable 的 production profile 对外诚实表达。C4 public catalog已冻结critical/optional与`Provide/Inherit/Suppress`语义；新beatmap-local作者格式已从V1当前authoring面排除，既有只读direct visual compatibility仍高于selected。后续 C6 组件必须继续绑定exact package+layout+material+scene revision，并在集中清单登记受影响视觉项。

### SV1-2：G1 安全存储与原子重载

依赖：保持 `SV1-0` 数据处置结论与现有 `.osk` 路径稳定；不得从异常期存档整包恢复。active实例绑定immutable revision，磁盘原地变化不会混入；只允许Settings显式manual Reload准备并发布新revision，不开放watcher。

当前已闭合链为：schema 56-origin preflight → managed/external held-root Windows no-follow capture → pure immutable capsule与external paired manifest → exact-capsule `BmsLegacySkin` factory/guarded selection → schema 57 scanner/exact external registry → shared coordinator/single-v3 journal/recovery → Folder Skin Workspace的external Open/Import/Unregister与managed Open/Rename/Delete → ordinary `.osk` bounded ingress/transactional receipt。C2以该链为输入，但path/token/digest/DTO/receipt本身仍不是authority，也不能代替consumer publication/detach/retire证明。

C2已以真实host证据冻结为：Settings `Reload current skin`是三源唯一触发，Folder Skin Workspace无第二入口，same-value selection仍短路，live gameplay/preview在source prepare前拒绝。manager以immutable revision、background prepare、update-thread rollback barrier、participant/work lease与retire queue覆盖完整inventory；无staged receipt的attached host阻断reload，旧owner等待最后detach。C3 layout、C4 codec/material与C5 scene/event consumer已复用并扩展该协议；C6新增consumer必须继续同切加入，不能另造publication旁路。

各切片按以下顺序独立过门：

1. **mutation authority/recovery foundation（已闭合）**：已有记录按ID刷新重读完整资格，既有source与fixed staging source由held native root固定no-follow identity，尚不存在的target只表示为root-bound规范化空name slot；staged新记录只生成planned ID/path/root/version的immutable publication plan而非Realm writer。scanner、selection、mutation/recovery共用线性化边界，版本化strict journal在首个外部步骤前durable落盘，启动先幂等恢复再scanner；有效歧义精确冻结、invalid/unknown/IO全局冻结，scanner negative cleanup服从冻结。程序化`OmsSkin` protected fallback pair门已被后续managed delete纵切直接消费；foundation本身不开放通用UI或任意Realm写入。
2. **rename（C1已闭合Workspace caller）**：工作区存储身份是`chartskin/<direct-child>`目录名，作者展示身份来自包内容。操作只移动direct-child目录并更新同一Realm record的managed path；不修改根`skin.ini [General] Name`、作者名、包字节、revision/hash或scanner owner。Prepared durable、held-root no-replace move、final identity、Realm一致性、恢复矩阵、selection/scanner竞态、取消与shutdown join仍是强制回归门。
3. **staged import后端（已由full ManagedCopy消费）**：只消费固定`skin-mutation-staging/{operationId:N}`下由OMS为本operation持有的provisional副本。manager-owned stager现从registered external的fresh held capture取得paired capsule/manifest，并以single v3 intent在首字节前建立durable owner；后端仍以content revision + physical-tree fingerprint、同卷no-replace move和one-shot publisher交接scanner owner，不自动选择。
4. **startup selection/scanner竞态（已闭合）**：configured preparation以startup/generic mutation observation贯穿typed contention waiter、deferred scheduler callback与chained retry；只等待exact startup/staged-import completion，fresh retry重新做generation/current pair/Realm/path/owner/freeze/allowlist/capture/factory检查。manual managed请求与generic mutation继续fail-closed，普通Realm `.osk`不受影响；update thread不等待，shutdown统一cancel/reap/join。该矩阵保留为后续coordinator/scheduler变更的强制回归。
5. **managed delete（已闭合，现有settings入口玩家可达）**：现有delete button/dialog只通过独立`CanDelete`与`DeleteSkinAsync`进入专用operation；eligible managed direct-child的Prepared绑定operation-derived tombstone、exact Realm fingerprint、bounded source-node manifest与durable fallback disposition，再经held-root no-follow detach、fresh delete-exclusive DELETE-handle重捕、只删除manifest完整树/崩溃子集、authoritative Realm compare-remove及restart recovery收敛。same-session live重捕仍要求exact manifest，release窄窗移出在0次disposition时拒绝；exclusive handles只共享READ，取得后阻止已持有root/child被移出tombstone，但不冒充目录namespace lock。final preflight后竞态新增/replacement不被删除；若导致exact节点部分清理后root失败，则保留journal/Realm并冻结。current目标必须先真实提交exact protected `OmsSkin` pair；`NotRequired`只代表noncurrent，split/无效fallback拒绝，首步前authority drift在exact receipt仍可证明时安全回滚，receipt/写入漂移才冻结。首个物理步骤前可取消，之后只由journal/recovery决定结果；selection/scanner/generic mutation、queued fallback scheduler与shutdown join保持强制回归。旧`CanModify`/通用`Delete`继续冻结。
6. **thin staged-import stager/caller（永久冻结旧入口）**：C1已由manager-owned full ManagedCopy闭合external source→fixed provisional，因此不再存在独立thin stager产品门。禁止任意path直传、普通递归copy或无production consumer的stager抽象；后续只回归已闭合的full ManagedCopy authority、budget、cancel/recovery与脱敏诊断。
7. **external registration/capture（C1已闭合）**：继续保持external永久只读、settings record-ID管理、held no-follow capture、service-owner Realm、显式选择/configured restart、BMS+mania artifact、fresh Open与pure-Realm noncurrent Unregister。service-owner不授权path/source；active实例只读capsule，原位变化不reload。
8. **`C1`作者文件工作区（已闭合）**：external Open/Import/Unregister、managed Open/Rename/Delete、single-v3 ManagedCopy、动态journal支持和ordinary `.osk` early gate/receipt已通过产品旅程、宽回归、Release与独立终审；保持旧通用folder mutation与thin stager冻结。
9. **当前consumer revision publication / reload / detach（C2已闭合）**：Settings唯一manual Reload经manager覆盖ordinary Realm `.osk`、managed、external同ID新revision；所有可失败I/O/解析/资源准备止于background，全部participant ready后才在update thread一次交换。prepare中attach触发fresh barrier、commit前detach移出待确认、late attach只取已提交revision；失败保A，最后consumer/work lease detach后旧owner exactly-once retire。current external/managed/ordinary mutation先fallback+detach；无统一协议的legacy authoring/update旁路已稳定禁用。
10. **产品 UI/实机（C2已闭合，G1最终门仍到C6）**：入口已明确区分manual Reload、受管目录物理删除与external解除注册，并为success/no-change/live reject/unsafe reject/failure给出反馈；真实选择、重启、切换、rename/import/delete、三源原子替换、缺件与holder生命周期已纳入自动宽门。C3 layout、C4 codec/material与C5 scene/event consumer已加入revision协议；C6新增scene/script consumer继续同切加入。

验收：真实选择链、重启、切换、rename/import/delete、缺件、原子替换和备份数据根均通过自动与人工验证。

### SV1-3：playfield/BGA layout descriptor（C3已闭合）

冻结依赖：P1-K parser/converter是keymode、lane count与keysound timeline的唯一authority；P1-L继续拥有BGA内容/timeline/seek/POOR，C3只统一最终viewport/rect。

1. 唯一ruleset-neutral `GameplaySkinLayoutContext`、immutable neutral snapshot/publication、BMS solver与mania adapter绑定exact native context/keymode、topology、style、safe bounds/aspect/DPI及package/current/content/topology/layout revision；neutral与typed adapter以同一publication引用发布。
2. BMS playfield/stage/group/lane、Note/LN、barline、hit/judgement line/target、lane cover、pre-start、BGA viewport、gauge/combo/HUD，以及mania playfield/stage/column/note/hold/hit target/judgement与core provider都从owner current publication取同一exact snapshot；禁止`CreateDefault()`、profile、fixed rect/local offset或drawable尺寸二次求解。
3. geometry逐字段验证finite、正值、合法range、安全screen bounds与non-overlap；单字段非法只用确定性程序化fallback并诊断，但始终产出一个完整immutable snapshot，不发布部分新旧pair。
4. frozen矩阵覆盖5K/7K P1/P2/CenterP1/CenterP2、9K BMS/PMS、14K双deck/S1/S2/centre gap及mania single/dual stage；stable LaneId/GroupId与显式logical/visual/global/group-local index不得由enum位置或总lane count反推。
5. package+layout在background prepare、update-thread single-reference commit中共同进入C2 participant generation/fresh barrier/lease/detach/retire协议；失败保A，live gameplay/preview仍prepare前拒绝，不开放watcher/live reload。

验收：P1-K decode/converter、真实shared keysound、全layout矩阵、完整production graph、三源A→B/失败保A、并发/holder/retire、BMS/mania/core full与Release均已通过；精确数字见[当前状态](DEVELOPMENT_STATUS.md)与[变动日志](CHANGELOG.md)。C4已在该冻结publication上增加material，C5又加入prepared scene/event与slot host；不得重开C3合同或另建geometry。

### SV1-4：mania-compatible ini 共同层（C4已闭合）

依赖：SV1-3 冻结 stable lane/layout context；现有 legacy decoder 生产行为由兼容回归保护。

1. 28项public catalog、Common v1与唯一BMS v1 extension是codec、validator、resolver、consumer与文档的同一authority；runtime capability与Suppress资格分层。
2. mania/BMS共享一个exact-bytes tokenizer/codec；legacy `[Mania]`/`[Bms]`只消费同一immutable token stream，不重开文件或二次解析。
3. target显式携带C3 stable LaneId/GroupId和全部logical/visual/global/group-local index；9K raw `0..8`只经版本化映射进入canonical `1..9`。
4. resolver显式产出Provide/Inherited winner/Suppress，critical suppress拒绝；invalid/empty不能冒充absent，也不能回头借同package较宽声明。
5. package+layout+material+scene在background prepare完成，update thread只提交immutable引用；未知键、非法值、缺素材和不支持capability产生稳定脱敏诊断，失败保exact A。

验收：ordinary/managed/external三源的public declaration经过真实SkinManager/ruleset/provider驱动BMS Note/LN与mania Note/Hold/KeyVisual；旧`.osk/[Mania]`与`.osk/[Bms]`兼容、BMS静态/60FPS、candidate顺序、三态、diagnostic、revision/owner与宽门均通过。完整事实见[C4交接](../../other/SKIN_SYSTEM_C4_CODEC_MATERIAL_COMPLETION_HANDOFF_20260831.md)。

### SV1-5：声明式 scene、动画与事件 ABI（C5已闭合）

依赖：SV1-3 layout snapshot 与 SV1-4 shared config 可用；引擎事件 authority 已明确。C5结果见[C5完成交接](../../other/SKIN_SYSTEM_C5_SCENE_EVENT_COMPLETION_HANDOFF_20260903.md)。

1. 以稳定 node type allowlist 提供 sprite/container/text/mask/clip、受控 blend/effect、frame animation、tween、状态机、只读property binding、template/instance；manifest/scene bytes、路径、类型、target、resource与graph budget在background prepare校验。
2. 引擎 adapter 发布 lifecycle/layout/input/object/judgement/score/combo/gauge/timing/BGA 只读事件；bounded stream提供完整Snapshot/Reset，attach由publication reset锚定，reload/seek/retry/rewind和epoch切换顺序确定。envelope可发resume，但scene ABI只接受可由Snapshot重建的状态更新。
3. global/stage/group/lane roots、pooled note/LN、HUD与ephemeral effect分层；scroll/LN clipping/instancing、z-order、mask/clip、DPI/safe-area与geometry仍由C3 exact layout及引擎host驱动。
4. package 只能锚定descriptor slot或自身scene，不能遍历`DrawableRuleset`父树；renderer只读单一prepared graph，不二次parse/resolver/resource prepare。
5. BMS/mania真实production host与`GameplaySkinRuntimeSupportProfile`逐slot决策已接入同一package+layout+material+scene publication；不改写nullable `ISkin`/`Drawable.Empty()`旧语义，失败只熔断scene/slot并保留gameplay。

验收：已由真实ordinary/managed/external source、SkinManager current revision、BMS/mania ruleset producer与实际renderer证明全部适用slot、key press/hit/LN/judgement/combo/gauge/HUD/BGA装饰、late attach/seek/rewind/旧epoch隔离、dense/14K固定pool及预算；无per-note脚本/解析/GC authority。精确矩阵见C5交接。

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
