# P1-A 当前状态：Skin V1、产品面与 release gate

> 最后更新：2026-08-09
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，稳定合同见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)。恢复与架构证据按需查 [恢复审计](../../other/SKIN_SYSTEM_RECOVERY_20260710.md) 和 [V1 架构审计](../../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

## 一句话状态

`SV1-0` 自动、schema 56 数据与用户实机 gate 已全部通过；`SV1-1` 的首个已导入 `.osk` BMS Note/LN 产品纵切现已覆盖普通短键与长条 head/body/tail，四组件的自动、合同、安全和回退 gate 均已闭合，`V-001`～`V-004` 集中待验收。这只表示首个产品纵切自动闭环，不是 `SV1-1` 完成或产品交付；视觉待签收不再串行阻塞后续自动可证切片。`SV1-2` 的schema 57 owner、held-root启动scanner、exact-capsule factory/guarded selection、configured managed selection↔startup scanner非阻塞协调、专用mutation authority/recovery、directory-only rename、fixed-source staged import后端及managed delete产品纵切现已闭合。玩家可通过手工放置目录→重启发现→既有dropdown选择，并从现有settings删除确认框物理删除eligible managed skin；rename/import仍没有非测试caller，thin staged-import stager/caller当前NO-GO。2026-08-09产品链审计确认current managed atomic reload/detach当前也为**NO-GO**：既无真实production caller，也无覆盖BMS/mania/core全部consumer的publication/detach barrier；external、scene/script与canonical包同样未交付。按最终release-ready玩家能力只能概括为**约三成**，工程/安全地基约半数且显著高于玩家完成度；两者不是gate，也没有线性剩余工期含义。

## 当前产品能力

- **恢复基线可用**：`.osk`/legacy mania、BMS F1 静态颜色/纹理/几何、选择链与程序化 `OmsSkin` 迁移 fallback 保持可用。
- **实现并自动验证的新增可见能力为 4**：选中的用户 BMS 包可为普通短键提供 `name-0`、`name-1`…编号帧动画，也可为 `NoteImage{lane}H/L/T`（含 `S`/`S2`）长条 head/body/tail 提供静态图和同规则 60 FPS 动画；普通短键静态 `NoteImage` 属恢复基线，不重复计数。
- **产品视觉签收为 0/4**：普通短键、长条 head、tail 与 body 分别登记为集中视觉项 `V-001`～`V-004`，用户尚未签收，因此只能称实现/自动 gate 通过，不能称已交付功能。
- **安全回落覆盖 Note/Head/Body/Tail**：selected 单组件缺失、损坏、空值、越权或超预算时逐组件回落；body 是不可 `Suppress` 的 critical 组件，资源失败才 `Inherit`，有效 body 即使 width 缺失或非法也继续使用同组件与默认 `0.5775`。坏 body/tail 都不能从低层裸同名纹理拼件，低层自己的完整组件仍可接管；tail 保持 optional 透明 protected fallback。异步换源只发布当前 revision 的完整结果。beatmap-local 优先目前只是注入式 provider-order 合同，不是真实 BMS `WorkingBeatmap` 能力。
- **Skin V1完整产品面仍未交付**：`SV1-1` 未完成；`SV1-2` 已有玩家可达的受管目录启动发现/选择与最小settings managed delete，rename/staged import仍是无应用caller的后端；没有production stager或external。原子reload/detach已经完成独立审计但因缺caller、全consumer barrier与安全owner退役而NO-GO，`SV1-3`～`SV1-7`未实现。不能把这些窄切片描述成G1、Skin V1或产品交付完成。

| 产品交付面 | 当前状态 |
| --- | --- |
| BMS 普通短键编号帧动画 | 实现/自动 gate 已过；`V-001` 集中视觉待验收，未交付 |
| BMS 长条头静态图/编号帧动画 | 实现/自动 gate 已过；`V-002` 集中视觉待验收，未交付 |
| BMS 长条尾静态图/编号帧动画 | 实现/自动 gate 已过；`V-003` 集中视觉待验收，未交付；透明链底不是作者 `Suppress` |
| BMS 长条身静态图/编号帧动画与安全宽度 | 实现/自动 gate 已过；`V-004` 集中视觉待验收，未交付；critical、不可 `Suppress` |
| gameplay slot 三态 | 普通短键/长条头/body critical 与长条尾 optional slot 已消费 `Provide/Inherit`；作者 `Suppress` 与其它 slot 未交付 |
| canonical `oms-simple.osk` fallback | 未交付；实际链底仍是程序化 `OmsSkin` |
| G1 文件夹发现/选择/原子重载 | 部分实现、未交付；启动发现/选择及settings managed delete玩家可达，rename/import后端自动gate已过但无caller/stager/UI；external未闭合，atomic reload/detach因无真实caller及全consumer barrier当前NO-GO |
| 统一 layout descriptor/solver | 未交付；现有 geometry provenance 不是有效 layout |
| shared ini codec/结构化诊断 | 未交付 |
| scene/event runtime 与 sandbox script | 未交付 |
| `oms-simple.osk` / `oms-complex.osk` / Authoring Kit | 未交付 |

## 产品价值与最终差距

- **此前投入不是无意义劳动**：真实玩家链已经从程序化默认扩展到已导入`.osk`的BMS Note/LN渲染，以及managed目录的重启发现、选择和settings物理删除。immutable capsule、`551a`启动协调与delete journal/recovery直接保护这些真实入口和玩家文件，不是纯抽象美化。
- **投入结构明显偏向安全地基**：directory-only rename和fixed-source staged import的专属operation/recovery虽然进入production程序集，但仍没有非测试caller/stager/UI，只能算可复用风险资产，不能按代码量或测试量计作玩家功能。既有topology/config/event/capability fixture同理；没有production host/renderer/authoring consumer就不得继续横向扩张。
- **最终产品仍有主要主体未实现**：`SV1-3`唯一layout、`SV1-4`shared codec、`SV1-5`scene/event、`SV1-6`sandbox及`SV1-7`双canonical包/Authoring Kit均未形成产品链；程序化`OmsSkin`仍是链底，新增视觉人工签收仍为0/4。完整分阶段差距见[2026-08-09交接](../../other/SKIN_SYSTEM_PROGRESS_HANDOFF_20260809.md)。
- **后续按完整campaign计进度**：`C1`先闭合external settings注册、Windows resolved-identity/no-follow capture、Realm、选择/重启、renderer及unregister，再以fresh service-owned source proof接通独立Import Managed Copy、managed rename/delete和journal支持面；不得停在external DTO/registry/capture或thin stager。`C2`随后同campaign冻结并实现reload触发/允许场景/全consumer协议。

## 七个交付 Campaign 燃尽状态

`SV1-*`是能力分类而不是交付计数。执行计划已改为[P1-A PLAN的`C1`～`C7`持久campaign预算](DEVELOPMENT_PLAN.md)：当前为 **`0/7` closed，`C1`待启动**。每个campaign保持active直到完整退出门；audit、NO-GO、red test或foundation均不单独计数。`C7`退出时，当前已知非人工Skin V1任务必须全部收口，只剩集中视觉、真实设备与长时间体验签收。

燃尽映射为：`C1`作者文件工作区/G1 UX → `C2`当前consumer revision reload/detach → `C3`P1-K+唯一layout → `C4`shared codec/catalog/resolver/mania compatibility → `C5`scene/event及剩余slot production并关闭`SV1-1`自动门 → `C6`sandbox并让ini/manifest/scene/script全部加入revision协议、关闭最终G1自动门 → `C7`canonical双包/Authoring Kit/自动release。当前约三成的dated估计退居背景信息，后续主要报告`Cxx closed / active / remaining`与具体用户能力，不再用阶段编号或小提交制造进度。

## 当前实现事实

- `SkinManager` 当前皮肤后仍恒接程序化 `OmsSkin`；最终链底必须由只读、完整验证的 `oms-simple.osk` 接管。
- `BmsLegacySkin` 继续叠加解析 `[Bms]` 并保留 `[Mania]`；native BMS 普通短键与长条 head/body/tail 是当前仅有的真实 package 文件纵切。
- internal 26 项 semantic slot、neutral lane identity/topology/revision、config presence/provenance、六类 lane-resource resolution、event envelope/order 与 capability decision foundation 已落；它们仍是 process-local 合同地基，不是作者 manifest、完整 layout、生产事件 runtime 或 sandbox。
- geometry snapshot 仍只保存 parser 接受的来源事实；当前只有 `LongNoteBodyWidth` 进入唯一共享 scalar resolver，按 finite 且 `0 < width <= 1` 验证并对缺失/非法值逐字段回落 `0.5775`。其它 geometry 的 finite/range/screen-space validation 尚未进入统一 descriptor。
- G1 已在既有 folder constructor/schema 56 字段上增加 ruleset-neutral 的存储声明分类与现存目录 lexical/reparse preflight：区分 Realm `.osk`、`chartskin/<name>` managed folder、只读 drive-letter-qualified Windows external folder及 typed invalid；拒绝双 authority、managed/external namespace 重叠、root/ancestor reparse、盘符根、UNC/device/ADS/traversal/Windows 歧义名，安全字符串不展开路径。它只是一瞬时只读预检，不证明路径物理上位于本地盘，也不是 resolved identity、scanner owner 或 mutation token；当前 production selection 只能在后续 native capture 完整成功后消费其 opaque managed request。
- shared core 的 pure immutable capsule 只接收逻辑 file/directory entries、declared length与read callback，不依赖 path、authority、Storage或filesystem API；资源名统一slash/NFC后按Windows大小写语义拒绝重复、非法段与file/directory层级冲突，精确复制declared bytes，以规范名、长度和文件SHA-256形成版本化整包content revision。capsule自有backing，resource view非owning且返回defensive copy；预期读取失败typed reject，取消传播，失败/取消清理provisional backing。production folder exact constructor 现在只消费带明确 marker 的 owning revision store，并跳过 live `RealmBackedResourceStore`。
- managed-only Windows native capture producer只接受resolver-issued `chartskin/<direct-child>` request；从严格物理本地卷handle出发，以handle-relative、no-follow方式固定authority/package/全部目录与文件identity，拒绝reparse、未由resolver展开成长名的8.3/alternate alias、hardlink、重复物理identity、忙写源、unsupported volume mapping与枚举/读取竞态，再把held-handle bytes交给pure capsule。成功前复验pinned metadata、完整inventory、authority links与package root，且不泄露handle/live stream；它仍不是filesystem transaction、external capture、scanner或mutation。
- managed启动scanner从同一held `chartskin` handle建立baseline direct-child inventory，并以同一authority相对capture候选，最终复验完整inventory与authority links。合法名字即使对应file/reparse/坏包也进入Observed，只有capsule及根`skin.ini` metadata有效的包进入Valid；NTFS目录时间延迟只允许最多3次、每次25ms、可取消的完整session重试，任何失败轮不发布partial snapshot。schema 57迁移不回填owner，scanner只在完整scan的单一Realm事务内维护exact-own记录；null/foreign/同路径冲突/普通`.osk`不动，取消在commit前回滚，negative只soft-delete Realm记录而不碰磁盘。scanner现从discovery到Realm commit全程持有共享coordinator边界，并服从recovery冻结。
- production `SkinManager` 现在只为 Realm 中 authoritative `IsManaged` 且解析合法的 folder 记录启动后台 capture；capture 后以 exact ordinal allowlist 验证 `InstantiationInfo`、要求根 `skin.ini` 与精确 capsule 构造入口，factory 前后均复核 authoritative record，最终在共享coordinator内重新取得本Realm authoritative live record并一次提交 `CurrentSkinInfo`/`CurrentSkin`。configured preparation记录startup sequence与generic mutation reservation epoch；若它跨越exact startup/staged-import contention，后台worker只等待该holder的typed completion，再由update scheduler发起fresh preparation。每次direct/chained排队及retry lease内都复核generic mutation epoch；fresh retry重新验证generation/current pair、authoritative Realm ID/record、path、owner、freeze、allowlist并重新capture/factory。未注册/unmanaged、external、非法类型、hardlink、capture/factory 失败、过期 generation、真实generic mutation、竞态、reentrant 请求或 completion scheduler fault 均保留旧 pair并回收 provisional owner；普通 `.osk`、`OmsSkin` 与 mania 路径不改。
- committed selection 使用封闭绑定图与显式 request surface，generic `Bindable`/Dropdown/lease 不能两向绕过预提交 gate；settings 只镜像已提交值。folder 的旧编辑、导出、通用重命名、delete/undelete、文件 mutation、update-import 与 external-edit 路径均冻结，并在真正 Realm mutation 内按 ID 重新取得 authoritative record，调用方伪造/陈旧 `SkinInfo` 字段不能授权。新的专用foundation签发held existing/staged source、root-bound absent target slot、immutable新记录publication plan与exact durable receipt。
- 专用internal rename已形成真实production纵切：首个物理可见步骤前durable写入Prepared；Windows held-root/no-follow primitive以no-replace方式把source direct child移动到target slot并保持同一physical identity；随后依次持久化`FilesystemApplied → RealmApplied → Committed`，成功后compare-delete terminal journal并确认Missing。Realm只修改同一记录的`FilesystemStoragePath`，不发布新记录、不改scanner owner、revision/hash、`Name`、`Creator`或`skin.ini`。成功后active immutable capsule可继续服务已有consumer，全局selection generation前进并取消当时的pending selection，旧generation不得发布，未来重新选择从新路径capture；歧义恢复冻结source/target并继续禁止scanner negative cleanup，shutdown在Realm释放前cancel+join worker。active owner的全consumer detach barrier仍未实现。
- 专用internal staged import同样直接消费既有authority/coordinator/journal/recovery：source固定为`skin-mutation-staging/{operationId:N}`，输入合同假定未来upstream stager已复制并保留外部原来源，但当前仓库没有该production stager或非测试caller；managed root是既存authority root，不由import创建。source先从held staging root做no-follow完整package capture并验证根`skin.ini`、closed实例类型、capsule revision、reparse/hardlink/duplicate/busy-writer与inventory，再把uppercase content revision及覆盖空目录、全节点identity/kind/length/time/attributes/reparse/link/delete和exact ordinal层级的lowercase physical-tree fingerprint写入durable Prepared。随后由held staging parent/source和managed root执行同卷identity-preserving no-replace move；target重捕identity/capsule/tree fingerprint必须与Prepared exact一致。`FilesystemApplied`与最终Realm ID/path/owner冲突复核后，one-shot publisher只发布`ID = operationId`、exact target path、最终capsule metadata/hash、closed实例类型、空`Files`、非external/protected/DeletePending且交接exact scanner owner的一条record。publication plan不是Realm writer，普通scanner不消费plan。
- staged import不自动选择或替换current active immutable capsule，也不沿用rename的全局pending取消；无关pending selection只要最终authoritative复核仍成立就继续。selection首次final-boundary争用直接取得exact staged-import holder的typed completion，即使holder在worker观察前已完成也只沿同一completion链fresh retry，不再用全局running/epoch猜测；generic mutation仍无retry authority。它按staged-import kind复用同一phase graph、canonical journal与recovery；所有可判定physical结果返回前重枚举source/target双槽，可判定状态只前滚exact target或清理exact provisional source/计划record，部分self-cleanup崩溃可按exact root identity/tree幂等续跑。both始终保持歧义，neither仅在无法继续证明provisional可丢弃时保持歧义，root/source/target identity mismatch、physical-tree/content drift或foreign/conflicting Realm也均保留journal并冻结。worker与rename/startup/selection-retry worker服从同一Realm释放前cancel + join边界，脱敏状态只输出kind/phase/status/count。
- managed delete由现有settings delete button/dialog作为真实caller，只把确认时捕获的record ID交给独立`DeleteSkinAsync`；`CanDelete`与operation均从fresh Realm重取资格，普通Realm `.osk`继续既有soft delete + default语义，protected/fixed、external、foreign/null owner、非法path及旧通用folder `Delete/CanModify`保持fail-closed。eligible source先以held managed root做有entry/depth/path预算的迭代no-follow完整树复核，durable Prepared绑定exact authoritative record fingerprint、版本化exact source-node manifest与operation-derived `.oms-delete-{operationId:N}` tombstone；update-thread证明随后以同phase monotonic write固化`NotRequired`或`ProtectedPairCommitted`。首个物理步骤是held-root-relative source→tombstone detach；递归cleanup前用fresh no-follow delete-exclusive handles（持有DELETE、只共享READ）重捕，same-session live必须仍为完整manifest，release窄窗内完成的node移出在0次disposition时拒绝，只有fresh recovery可接受部分崩溃后的durable子集；exclusive tree取得后的已持有root/child relocation由sharing violation阻断。目录handle不封namespace：preflight前可见的新增/同名替换/metadata漂移在0次disposition时拒绝；之后竞态新增/replacement不进入held list、不被删除，若exact节点部分清理后root非空失败则保留FilesystemApplied journal与Realm record并冻结。current目标在detach前必须真实提交与`OmsSkin.CreateInfo()`逐字段一致的protected Realm record及`CurrentSkinInfo`/`CurrentSkin` coherent pair；split pair、fallback无效均拒绝，首步前authority漂移在exact receipt仍可证明时安全RolledBack，receipt/落盘歧义才冻结。首步前取消可安全回滚，首步后不再观察caller cancellation，由durable phase/recovery收口；`NotRequired`恢复不要求OMS record，`ProtectedPairCommitted`恢复继续要求exact protected fallback Realm record。selection/scanner/generic mutation继续共享coordinator，fallback scheduler callback与shutdown恰一方claim/reap，晚到callback no-op，Realm释放前join全部worker。
- playfield 可读取当前皮肤 profile，但 gauge/combo/BGA 尚未消费同一 resolved descriptor；14K 四角四 BGA player 只是临时表现。
- mania/BMS 的共同目标仍是 neutral ini codec、scene/event ABI 与 sandbox；ruleset topology/layout adapter 分离，BMS 不继承 mania 具体 Drawable/transformer。

## 当前 gate

| 顺序 | Gate | 状态 |
| --- | --- | --- |
| 1 | schema 56 数据安全 | **通过**：异常 copy 已在保全后定点处置，OMS fixed-ID 已修正；不运行全局 orphan cleanup |
| 2 | 恢复基线实机 | **通过**：无外部皮肤、`.osk`、partial fallback、5K/7K/9K/14K、双皿与 mania/BMS 隔离均正常 |
| 3 | 文档与 memory 健康治理 | **完成**：只归位当前事实、未来步骤、稳定合同和历史；未改代码或产品 gate |
| 4 | 已导入 `.osk` 的 BMS 普通短键与长条 head/body/tail 视觉 | **`V-001`～`V-004` 集中待验收**；这是完成/release 声明门，不是后续开发开工门，不可复用静态恢复结论 |
| 5 | `SV1-1` 首个 Note/LN 产品纵切自动门 | **已闭合，视觉待验收**；四组件自动、合同、安全与回退 gate 通过，但不得写成 `SV1-1` 完成或交付 |
| 6 | `SV1-2` G1 安全存储与原子重载 | **进行中**；启动发现/选择与settings managed delete真实可达，rename/import后端无caller/stager/UI，thin stager/caller当前NO-GO；external未完成，atomic reload/detach经产品链审计为NO-GO，`SV1-3`～`SV1-7`仍未完成 |

## 最新验证

### `SV1-2` current managed atomic reload/detach 产品 GO/NO-GO：2026-08-09

- 按现有产品链只读追踪settings、启动配置、hotkey、selection与editor/external-edit入口：仓库没有“重载当前managed revision”的production caller、UI、watcher或manager API。settings只提交选择；same-value选择在准备前短路；filesystem-backed skin继续被editor、update import与external edit拒绝。唯一会重建实例的`ExternalEditOverlay`只服务普通Realm skin并立即dispose旧实例，既不适用于managed source，也没有consumer barrier，不能复用为本切caller。
- 当前managed selection只在manager内提交`CurrentSkinInfo`/`CurrentSkin` pair并广播`SourceChanged`，没有package revision publication对象、consumer注册表/ack、detach receipt或旧实例retire queue。真实consumer还存在明确的mixed-revision窗口：`BmsPlayfield`在loader中一次读取并缓存geometry且不监听`SourceChanged`；BMS Note/LN与pre-start preview各自通过per-host异步准备发布；core/mania drawable混合同步、逐host scheduler和next-update更新；菜单背景会在过渡期继续持有旧`Skin`。逐组件`SkinReloadableDrawable`、`BmsAsyncNoteDrawable`及一次selection pair提交均不是整包原子reload。
- exact capsule当前由新`Skin`实例持有；`Skin.Dispose()`会释放texture/sample/fallback store与capsule，`BmsLegacySkin.Dispose()`还会取消package note preparation。成功selection没有等待consumer并退役旧实例的协议，既有产品测试只能手工dispose superseded managed skin。因此即时释放旧owner会破坏仍挂载consumer，不释放又无法闭合生命周期。
- 结论为**NO-GO，停止且不增加reload foundation**。在产品先决定触发方式（手动settings、watcher或其它安全入口）、允许场景（是否含live gameplay）和全部consumer的participation/publication/detach协议前，无法建立不先发明API/barrier的真实产品红测。本轮没有runtime或测试改动，也未运行focused/full、formatter或Release；2026-08-02的**281/281、11/11、62/62、182/182、1530/1530、911/917、827/831、Release 0 error / 20 known warnings**继续作为最近代码基线，`551a`协调矩阵与managed delete全合同保持强制回归。完整取证见[2026-08-09交接](../../other/SKIN_SYSTEM_PROGRESS_HANDOFF_20260809.md)。

### `SV1-2` managed chartskin delete：2026-08-02

- 只读追踪确认最短玩家链是现有`SkinSection.DeleteSkinButton → SkinDeleteDialog`；本切没有设计新UI。按钮改用独立fresh-authoritative `CanDelete`，确认只调用manager-owned `DeleteSkinAsync(record ID)`。普通Realm `.osk`仍走既有soft delete并回到default；只有eligible scanner-owned `chartskin/<direct-child>`进入物理delete，protected/fixed、external、foreign/null owner、非法folder及旧通用folder mutation继续拒绝。
- operation在首个物理步骤前durable写入Prepared，绑定held managed-root/source identity、operation-derived tombstone、exact existing Realm-record fingerprint与有界exact source-node manifest；fallback callback返回后以同phase monotonic journal write持久化`NotRequired`或`ProtectedPairCommitted`，physical phase不得缺失该证据。Windows primitive以显式迭代walker限制depth/entry/path和pending handle预算；detach后释放无DELETE权的verification tree，再以fresh no-follow delete-exclusive DELETE handles重捕。same-session完整live tree必须仍与manifest相等，release窄窗移出在0次disposition时拒绝；只有fresh recovery的crash survivor可为其子集。exclusive handles阻断取得后已持有root/child relocation，但目录namespace仍可竞态新增：preflight前可见的新增普通文件、同名新identity或metadata漂移在0次disposition时拒绝；之后的foreign node不进入held list且绝不删除，允许exact节点部分清理后root失败并保留journal/Realm冻结。Realm只在durable FilesystemApplied后compare-remove exact fingerprint record。成功严格经过`Prepared(null) → Prepared(disposition) → FilesystemApplied → RealmApplied → Committed`并compare-delete terminal journal；各phase crash/restart按source/tombstone/manifest/Realm/disposition证据幂等前滚或安全回滚。
- current目标在detach前必须先提交与`OmsSkin.CreateInfo()`逐字段一致的protected Realm record和exact `OmsSkin`实例pair；mutation reservation、generation与同步pair commit构成runtime线性化证明，物理边界再fresh复核exact fallback Realm record。live hard-remove与recovery对`ProtectedPairCommitted`继续要求该record，但`NotRequired`明确表示noncurrent且不创建/要求OMS record。raw disposition却出现physical进展、split pair、invalid fallback、owner/hash/path/factory漂移均拒绝；首步前authority漂移在receipt仍exact时安全清除RolledBack intent，canonical receipt/写入漂移才保留journal冻结。首步前取消可abort；detach开始后忽略caller cancellation，由journal/recovery决定最终结果。
- scanner/selection/mutation沿用共享coordinator：专用delete仍是generic mutation，不获得startup retry权；selection在resolver触盘前先做non-blocking admission，typed startup/staged-import completion retry、generic epoch fail-closed、fresh Realm/path/owner/freeze/capture/factory、latest-wins/reentrant均保留。fallback提交对`SourceChanged`做delete-scoped延迟，先完成worker等待的TCS再发布事件，关闭callback重入shutdown的join环；queued fallback由callback或shutdown恰一方claim，晚到callback no-op且不等待update scheduler。
- 自动验证：core managed mutation+contract broad **281/281**（含真实Windows delete native **11/11**），managed selection产品类 **62/62**，mania skin **182/182**，BMS full **1530/1530**；core skin broad **911/917**的六项失败与既有基线完全一致，四项依赖已移除Osu ruleset archive fixture，另两项为default-skin background/sample旧假设。mania full **827/831**的四项`TestSceneAutoGeneration` replay frame既有失败且与本切无文件交集。额外core full已执行并仍受已移除ruleset/fixture的既有广泛失败阻断；本切相关子集无新增失败。Release **0 error / 20 known warnings**，仍为MessagePack `NU1902`及BMS tests既有`CS8600`/`CA2007`。本切未启动GUI或新增视觉签收，`V-001`～`V-004`仍为0/4。
- journal payload version保持v2，大小预算因最多8193个节点的定长fingerprint manifest由128 KiB提高为1 MiB；最大manifest round-trip仍低于该界。pre-product阶段从无production delete caller，legacy-v1或旧v2 Delete intent若缺operation-derived tombstone、exact existing-record fingerprint、source-node manifest，或physical phase缺durable fallback disposition，不能安全迁移，strict load会按Invalid全局冻结且不猜测路径/record/tree。既有v2 Rename/StagedImport及legacy-v1非Delete terminal处理不变。本切没有实现thin stager、任意path import、external、reload/detach、scene/script或canonical包。

## 当前风险

- schema 56 的四个无 authority orphan blob 已保全并暂留；schema 57迁移保持owner=null，当前scanner也不会claim、去重或清理它们。
- 当前真实 package 纵切只覆盖 BMS 普通短键与长条 head/body/tail；单组件安全替换不等于整包/全 playfield 同帧原子 reload。
- `SkinFilesystemStorageResolver` 返回的 normalised lexical path 只表示检查当时的声明/preflight，不是 capability；production managed folder factory 现已只消费 resolver-issued request 经 Windows fixed-handle capture 完整成功后返回的 exact capsule，不从 normalised path 或 live `NativeStorage` 直接进入 parser。capture 仍不是 mutation token、external adapter 或 filesystem transaction。
- managed folder现可在重启后的完整稳定scan中自动发现并进入选择面；scanner不watch启动后的磁盘变化、不自动选择或reload。现有settings入口可物理删除eligible managed direct-child并收敛其Realm record；专用directory-only rename与staged import仍只有internal物理写入后端且无非测试caller。旧通用folder mutation入口继续冻结。
- configured managed selection的startup重试只覆盖exact startup/staged-import typed contention；任何generic mutation reservation跨越都会抑制retry。后续若改coordinator、scheduler或shutdown，必须保留active/completed scanner、direct/deferred/chained generic与latest-wins回归，不能退回全局running/epoch猜测或update-thread等待。
- filesystem与Realm不能组成同一原子事务；rename、staged import与managed delete都依靠Prepared journal、identity-aware recovery和歧义冻结收口。真实NTFS的directory-entry move/recapture与delete tombstone cleanup都不是filesystem transaction、oplock/TxF级全树排他或字节内容快照；可观察差异会保留journal并冻结。旧通用Rename/Delete/Import入口继续禁用。
- resolved external identity尚未实现；当前Realm只要存在任一external filesystem声明，就会保守阻断全部managed mutation。这是临时fail-closed边界；仅有owner token或瞬时resolve不足以安全局部化，external切片必须让每个真实managed mutation admission持有并复验所有相关external physical root/ancestry proof到final collision linearization point，否则本纵切NO-GO并保留全局阻断。invalid/IO journal造成的冻结仍没有用户可见支持界面；operation只提供脱敏内部状态。
- current managed delete在物理detach前要求exact protected程序化`OmsSkin` pair，canonical接管后必须改为已验证只读`oms-simple.osk`。任何split pair、fallback字段漂移或无法确认都拒绝；现有delete可达性不等于canonical fallback或atomic detach已完成。
- active capsule 已与当前实例绑定且磁盘变化不会混入，但旧 owner 的退役必须等待全consumer detach。2026-08-09审计确认现有`SourceChanged`扇出与selection pair无法提供该证明：`BmsPlayfield`缓存geometry且不监听事件，BMS/core/mania/菜单背景分别在不同调度与过渡边界更新；不得把它们写成全playfield同帧reload，也不得即时dispose旧owner。
- 成功 preparation cache 仍按 `BmsLegacySkin` 实例复用；managed folder 实例的 source 已固定为 immutable capsule，因此磁盘变化不会污染 cache，但新 revision 必须经新实例与全consumer publication/detach协议发布。当前既无真实reload caller，也没有该协议；在触发方式、允许场景与consumer参与模型完成产品路线决策前，本项维持NO-GO而不是继续增加foundation。
- 真实 BMS beatmap-local 尚无逐谱作者格式和 `WorkingBeatmap` producer；实现它会新增 core 扩展点与公开 sidecar 合同，必须先由产品冻结范围。
- runtime 图片预算不等于 `.osk` importer 的压缩比/zip-bomb gate；G1 仍须独立实现。
- 程序化 `OmsSkin` 在 `oms-simple` parity/完整性/恢复 gate 前不能删除，但也不能写成最终产品能力。
- 除 `LongNoteBodyWidth` 的单字段 scalar policy 外，parser provenance 仍不等于 validated config/layout；极端几何仍可能使 playfield、gauge/combo 与 BGA 脱节。
- 9K raw lane token 与 V1 canonical 作者 token 存在重叠迁移风险，必须版本化处理，不能静默双 alias。
- topology/event/capability foundation 尚无 production lifecycle、payload、manifest、授权存储或 runtime gate。
- sparse 7K/9K keymode 与 lane keysound timeline 上界仍分别由 P1-K/P1-J 修复和验证；不得由 skin/layout 再猜一遍。
- 皮肤异常期归档只能定点取证，禁止整包 cherry-pick/apply。

## 下一检查点

1. 将普通短键与长条 head/tail/body 的观感、选择切换和 selected 坏包回落保持在[集中视觉清单](../../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)的 `V-001`～`V-004`，等待统一用户反馈；不得把自动测试写成视觉签收。
2. 继续`C1`/`SV1-2`：先完成external只读settings注册/独立行级管理→capture→selection/restart→unregister；行持有record ID并提供Open Folder/Unregister，不复用current Delete按钮/dialog。随后同campaign以fresh service-owned external proof提供独立Import Managed Copy，接通fixed provisional stager/import、managed rename/delete及journal冻结/恢复支持面。register/select/unregister不隐式复制，显式import只向OMS staging no-follow复制且永不修改外部源。service-owner token不授权source；rename、staged-import、delete全部admission必须把external root/ancestry proof保持至final collision point，否则`C1`不能退出。首切仍只允许noncurrent unregister，current与reload/detach归`C2`；thin/arbitrary-path或foundation-only stager继续**NO-GO**。
3. 剩余 optional slot 不再沿私有逐件 C# provider/display 扩张，留给后续 shared scene/runtime 接管。只有视觉结论实际决定下一实现时才暂停；期间保持 nullable `ISkin`、程序化 `OmsSkin`、当前 fallback authority 与 Skin V1 未交付状态不变。
