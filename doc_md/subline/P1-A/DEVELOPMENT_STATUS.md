# P1-A 当前状态：Skin V1、产品面与 release gate

> 最后更新：2026-07-17
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，稳定合同见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)。恢复与架构证据按需查 [恢复审计](../../other/SKIN_SYSTEM_RECOVERY_20260710.md) 和 [V1 架构审计](../../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

## 一句话状态

`SV1-0` 自动、schema 56 数据与用户实机 gate 已全部通过；`SV1-1` 的首个 managed `.osk` BMS Note/LN 产品纵切现已覆盖普通短键与长条 head/body/tail，四组件的自动、合同、安全和回退 gate 均已闭合，`V-001`～`V-004` 集中待验收。这只表示首个产品纵切自动闭环，不是 `SV1-1` 完成或产品交付；视觉待签收不再串行阻塞后续自动可证切片。`SV1-2` 第五刀已把schema 57 nullable exact owner、held-root native discovery与完整scan单事务reconcile接到`OsuGame`启动/退出生命周期；合法`chartskin/<direct-child>`可在重启后自动进入选择面，再由既有native capture → exact `BmsLegacySkin` factory → guarded原子选择发布immutable capsule。该scan不自动选肤且不是watcher/热重载；专用managed mutation、external registration/capture与整包atomic reload/detach barrier仍未接通。

## 当前产品能力

- **恢复基线可用**：`.osk`/legacy mania、BMS F1 静态颜色/纹理/几何、选择链与程序化 `OmsSkin` 迁移 fallback 保持可用。
- **实现并自动验证的新增可见能力为 4**：selected managed package 可为 BMS 普通短键提供 `name-0`、`name-1`…编号帧动画，也可为 `NoteImage{lane}H/L/T`（含 `S`/`S2`）长条 head/body/tail 提供静态图和同规则 60 FPS 动画；普通短键静态 `NoteImage` 属恢复基线，不重复计数。
- **产品视觉签收为 0/4**：普通短键、长条 head、tail 与 body 分别登记为集中视觉项 `V-001`～`V-004`，用户尚未签收，因此只能称实现/自动 gate 通过，不能称已交付功能。
- **安全回落覆盖 Note/Head/Body/Tail**：selected 单组件缺失、损坏、空值、越权或超预算时逐组件回落；body 是不可 `Suppress` 的 critical 组件，资源失败才 `Inherit`，有效 body 即使 width 缺失或非法也继续使用同组件与默认 `0.5775`。坏 body/tail 都不能从低层裸同名纹理拼件，低层自己的完整组件仍可接管；tail 保持 optional 透明 protected fallback。异步换源只发布当前 revision 的完整结果。beatmap-local 优先目前只是注入式 provider-order 合同，不是真实 BMS `WorkingBeatmap` 能力。
- **整体仍不可用**：`SV1-1` 未完成；`SV1-2` 已有managed启动自动发现与生产选择窄链，但没有专用删改、external或原子reload；`SV1-3`～`SV1-7`未实现。不能把这个scanner切片描述成G1、Skin V1或产品交付完成。

| 产品交付面 | 当前状态 |
| --- | --- |
| BMS 普通短键编号帧动画 | 实现/自动 gate 已过；`V-001` 集中视觉待验收，未交付 |
| BMS 长条头静态图/编号帧动画 | 实现/自动 gate 已过；`V-002` 集中视觉待验收，未交付 |
| BMS 长条尾静态图/编号帧动画 | 实现/自动 gate 已过；`V-003` 集中视觉待验收，未交付；透明链底不是作者 `Suppress` |
| BMS 长条身静态图/编号帧动画与安全宽度 | 实现/自动 gate 已过；`V-004` 集中视觉待验收，未交付；critical、不可 `Suppress` |
| gameplay slot 三态 | 普通短键/长条头/body critical 与长条尾 optional slot 已消费 `Provide/Inherit`；作者 `Suppress` 与其它 slot 未交付 |
| canonical `oms-simple.osk` fallback | 未交付；实际链底仍是程序化 `OmsSkin` |
| G1 文件夹导入/选择/原子重载 | 部分实现、未交付；schema 57 exact-owner启动自动发现与production factory/选择自动gate已过，但专用删改、external与原子reload/detach未实现 |
| 统一 layout descriptor/solver | 未交付；现有 geometry provenance 不是有效 layout |
| shared ini codec/结构化诊断 | 未交付 |
| scene/event runtime 与 sandbox script | 未交付 |
| `oms-simple.osk` / `oms-complex.osk` / Authoring Kit | 未交付 |

## 当前实现事实

- `SkinManager` 当前皮肤后仍恒接程序化 `OmsSkin`；最终链底必须由只读、完整验证的 `oms-simple.osk` 接管。
- `BmsLegacySkin` 继续叠加解析 `[Bms]` 并保留 `[Mania]`；native BMS 普通短键与长条 head/body/tail 是当前仅有的真实 package 文件纵切。
- internal 26 项 semantic slot、neutral lane identity/topology/revision、config presence/provenance、六类 lane-resource resolution、event envelope/order 与 capability decision foundation 已落；它们仍是 process-local 合同地基，不是作者 manifest、完整 layout、生产事件 runtime 或 sandbox。
- geometry snapshot 仍只保存 parser 接受的来源事实；当前只有 `LongNoteBodyWidth` 进入唯一共享 scalar resolver，按 finite 且 `0 < width <= 1` 验证并对缺失/非法值逐字段回落 `0.5775`。其它 geometry 的 finite/range/screen-space validation 尚未进入统一 descriptor。
- G1 已在既有 folder constructor/schema 56 字段上增加 ruleset-neutral 的存储声明分类与现存目录 lexical/reparse preflight：区分 Realm `.osk`、`chartskin/<name>` managed folder、只读 drive-letter-qualified Windows external folder及 typed invalid；拒绝双 authority、managed/external namespace 重叠、root/ancestor reparse、盘符根、UNC/device/ADS/traversal/Windows 歧义名，安全字符串不展开路径。它只是一瞬时只读预检，不证明路径物理上位于本地盘，也不是 resolved identity、scanner owner 或 mutation token；当前 production selection 只能在后续 native capture 完整成功后消费其 opaque managed request。
- shared core 的 pure immutable capsule 只接收逻辑 file/directory entries、declared length与read callback，不依赖 path、authority、Storage或filesystem API；资源名统一slash/NFC后按Windows大小写语义拒绝重复、非法段与file/directory层级冲突，精确复制declared bytes，以规范名、长度和文件SHA-256形成版本化整包content revision。capsule自有backing，resource view非owning且返回defensive copy；预期读取失败typed reject，取消传播，失败/取消清理provisional backing。production folder exact constructor 现在只消费带明确 marker 的 owning revision store，并跳过 live `RealmBackedResourceStore`。
- managed-only Windows native capture producer只接受resolver-issued `chartskin/<direct-child>` request；从严格物理本地卷handle出发，以handle-relative、no-follow方式固定authority/package/全部目录与文件identity，拒绝reparse、未由resolver展开成长名的8.3/alternate alias、hardlink、重复物理identity、忙写源、unsupported volume mapping与枚举/读取竞态，再把held-handle bytes交给pure capsule。成功前复验pinned metadata、完整inventory、authority links与package root，且不泄露handle/live stream；它仍不是filesystem transaction、external capture、scanner或mutation。
- managed启动scanner从同一held `chartskin` handle建立baseline direct-child inventory，并以同一authority相对capture候选，最终复验完整inventory与authority links。合法名字即使对应file/reparse/坏包也进入Observed，只有capsule及根`skin.ini` metadata有效的包进入Valid；NTFS目录时间延迟只允许最多3次、每次25ms、可取消的完整session重试，任何失败轮不发布partial snapshot。schema 57迁移不回填owner，scanner只在完整scan的单一Realm事务内维护exact-own记录；null/foreign/同路径冲突/普通`.osk`不动，取消在commit前回滚，negative只soft-delete Realm记录而不碰磁盘。
- production `SkinManager` 现在只为 Realm 中 authoritative `IsManaged` 且解析合法的 folder 记录启动后台 capture；capture 后以 exact ordinal allowlist 验证 `InstantiationInfo`、要求根 `skin.ini` 与精确 capsule 构造入口，factory 前后均复核 authoritative record，再在 update thread 一次提交 `CurrentSkinInfo`/`CurrentSkin`。未注册/unmanaged、external、非法类型、hardlink、capture/factory 失败、过期 generation、竞态、reentrant 请求或 completion scheduler fault 均保留旧 pair并回收 provisional owner；普通 `.osk`、`OmsSkin` 与 mania 路径不改。
- committed selection 使用封闭绑定图与显式 request surface，generic `Bindable`/Dropdown/lease 不能两向绕过预提交 gate；settings 只镜像已提交值。folder 的旧编辑、导出、重命名、delete/undelete、文件 mutation、update-import 与 external-edit 路径均冻结，并在真正 Realm mutation 内按 ID 重新取得 authoritative record，调用方伪造/陈旧 `SkinInfo` 字段不能授权。专用 managed mutation service 与 active owner 的全 consumer detach barrier 尚未实现。
- playfield 可读取当前皮肤 profile，但 gauge/combo/BGA 尚未消费同一 resolved descriptor；14K 四角四 BGA player 只是临时表现。
- mania/BMS 的共同目标仍是 neutral ini codec、scene/event ABI 与 sandbox；ruleset topology/layout adapter 分离，BMS 不继承 mania 具体 Drawable/transformer。

## 当前 gate

| 顺序 | Gate | 状态 |
| --- | --- | --- |
| 1 | schema 56 数据安全 | **通过**：异常 copy 已在保全后定点处置，OMS fixed-ID 已修正；不运行全局 orphan cleanup |
| 2 | 恢复基线实机 | **通过**：无外部皮肤、`.osk`、partial fallback、5K/7K/9K/14K、双皿与 mania/BMS 隔离均正常 |
| 3 | 文档与 memory 健康治理 | **完成**：只归位当前事实、未来步骤、稳定合同和历史；未改代码或产品 gate |
| 4 | managed `.osk` BMS 普通短键与长条 head/body/tail 视觉 | **`V-001`～`V-004` 集中待验收**；这是完成/release 声明门，不是后续开发开工门，不可复用静态恢复结论 |
| 5 | `SV1-1` 首个 Note/LN 产品纵切自动门 | **已闭合，视觉待验收**；四组件自动、合同、安全与回退 gate 通过，但不得写成 `SV1-1` 完成或交付 |
| 6 | `SV1-2` G1 安全存储与原子重载 | **进行中**；schema 57 managed启动自动发现与production factory/选择自动门已闭合；下一步是专用managed mutation，再分别推进external与atomic reload/detach，`SV1-3`～`SV1-7`仍未完成 |

## 最新验证

### `SV1-2` 第五刀 schema 57 scanner owner与managed启动自动发现：2026-07-17

- Realm schema升至57并增加nullable opaque `FilesystemStorageAuthorityOwner`，真实schema56升级保持旧owner=null；Windows discovery从held `chartskin` authority严格分离Observed/Valid并复用native no-follow capsule，只有完整稳定scan可在单一事务内维护exact-own记录，根失败/竞态/非法snapshot/异常/取消零提交且commit前取消整事务回滚。`OsuGame.LoadComplete`只在线程池执行一轮scan，Dispose先cancel+join再释放Realm，owner变化也会阻止in-flight旧prepared skin发布。focused为schema/scanner **12/12**、native fake+真实Windows **55/55**、headless lifecycle **2/2**、BMS production selection **15/15**；扩大回归为core相关 **222/222**、mania skin **182/182**、BMS full **1483/1483**，三工程format verify通过，Release **0 error / 20 emitted known warnings**，独立安全审查blocker/major **0/0**。本切未操控GUI，视觉仍0/4；它不是watcher/热重载，也未实现专用managed mutation、external或atomic reload/detach。

### `SV1-2` 第四刀 production managed folder factory/选择：2026-07-17

- 生产 `SkinManager` 已把已注册合法 managed folder 的 resolver/native capture/exact capsule 接入真实选择路径。folder `SkinInfo.CreateInstance()` 不再有历史 `TrianglesSkin` fallback；factory只接受精确允许的 BMS `InstantiationInfo`、根 `skin.ini` 与 exact-capsule 构造入口。capture完成后与factory完成后双重复核authoritative Realm记录，prepared target还要求同一对象identity；只有当前generation/selection匹配时才原子发布info/skin pair。
- guarded selection关闭generic binding、Dropdown和lease绕过；同值/disabled请求、reentrant rejection、异步capture/factory失败、目标竞态、hardlink及scheduler fault均保持旧pair，稳定reason不被后续completion覆盖，provisional capsule/store在所有失败路径清理。active BMS普通短键与长条head/body/tail均从同一immutable revision读取，capture后的磁盘改动不影响当前实例。
- folder旧mutation面已在UI和manager/importer双层冻结；delete/undelete、update import、external edit及base/interface dispatch均在真正Realm事务内按ID重新取得authoritative记录，伪造同ID shadow object不能绕过。普通`.osk`、`OmsSkin`与mania路径保持既有行为。
- 产品路径 focused **14/14**；除既有12项外，新增“并发Realm请求在managed最终提交窗口必定胜出”和“off-thread managed请求不得取消既有pending”两项确定性回归。最终独立审查为 blocker/major **0/0**。本切未启动GUI或操控桌面，`V-001`～`V-004`仍为集中待验收。它未实现schema 57 scanner owner、自动发现/导入、专用managed mutation、external registration/capture或整包atomic reload/全consumer detach；因此G1、`SV1-2`、Skin V1及产品交付均未完成。
- 扩回归在修正“普通Realm `.osk`不应受managed capture update-thread门影响”并以同一commit lock封闭请求/completion竞态后全绿：core importer/selection/capsule **101/101**、BMS factory/legacy/selection/package Note-LN **210/210**、settings/startup **3/3**、mania `OmsSkin` **84/84**、BMS full **1482/1482**。改动文件四工程format verify均exit 0；`osu.Desktop.slnf` Release **0 error / 20 emitted known warnings**，即9条MessagePack `NU1902`在restore/build重复为18条，加BMS tests既有`CS8600`/`CA2007`各1条。

### `SV1-2` 第一刀 authority/path preflight：2026-07-17

- 新增 focused **54/54**：闭合 schema 56 的 Realm/managed/external/invalid 分类、folder `Files` 双 authority、protected/fixed-ID/DeletePending、managed direct-child、drive-letter-qualified external、managed/external namespace exact/ancestor/descendant 冲突、traversal/ADS/UNC/device/盘符根/尾点尾空格/Windows 设备名、缺失/文件目标、data root/managed root/package/external ancestor reparse、typed IO/path failure、诊断脱敏及 managed/external bytes+mtime+`SkinInfo` 零 mutation。
- core skin aggregate **111/116**；5 项失败仍是恢复基线同名的 1 项 Argon 默认皮肤旧期待与 4 项依赖已删除 Osu ruleset beatmap archive fixture，无新增失败。跨规则集 relevant 为 mania `FullyQualifiedName~Skin` **182/182**、BMS `FullyQualifiedName~Skin` **583/583**。最终 `osu.Desktop.slnf` Release Rebuild **0 error / 11 known warnings**，即 9 条 MessagePack 3.1.3 `NU1902` 与 BMS tests 既有 `CS8600`/`CA2007`。
- 三路独立只读终审在修正 mutation-token/canonical/no-follow 误称、external 与 managed namespace 重叠、external 纯度和 typed reason 后为 blocker/major **0/0**。当前 preflight 没有生产调用者；测试只用隔离临时目录和注入式文件属性 probe，未访问生产 Realm、`chartskin/`、用户目录或网络，未启动 GUI 或操控桌面。
- 尚未证明 8.3/SUBST/物理 identity 去重、真实 junction 集成、TOCTOU 安全打开、完整资源 inventory、`InstantiationInfo`、`NativeStorage` folder store、scanner ownership、mutation 或原子 reload；这些继续是同一 `SV1-2` 的后续 gate，不能用本组 54 项冒充。

### 产品/runtime 与 gate 工具：截至 2026-07-17

- 产品自动验收扩为 **94/94**，并连续三轮全绿：在 Note/Head/Tail 矩阵上增加 body 静态图/60 FPS 连续编号帧、7K normal/scratch、14K `S2L`、A→B、有效 body + width 缺失/非法逐字段默认、坏 body 隔离、低层裸同名防串/完整组件接管、真实 Idle/Holding/Broken、异步首次挂载当前态与 HCN regrab。beatmap-local 项仍只是注入式 provider-order fixture。
- 当前合并态以本页上一节的factory/选择扩回归为准：BMS full **1482/1482**，core **101/101**、settings/startup **3/3**、mania `OmsSkin` **84/84**，`osu.Desktop.slnf` Release **0 error**；保留9条MessagePack 3.1.3 `NU1902`与BMS tests既有`CS8600`/`CA2007`，未使用`NoWarn`。
- 改动文件四工程定向format verify均exit 0；独立终审 blocker/major **0/0**。
- 普通短键的generator/staging/scene/runner safety既有 **53/53** 结论未在factory/选择本切重跑或冒充本切数字。
- root generator 实跑 **1/1**，两个 staged `.osk` 与确定性原件 SHA-256 一致；staging/reparse/无关文件安全用例与 exact 参数/路径/非递归清理用例均已包含在 **53/53**。非法/缺值 exact CLI 均 exit 1，新增 AppData host 残留为 0。
- 测试只使用隔离临时存储；生产 Realm、`chartskin/`、用户皮肤目录和网络零访问、零写入。按用户要求本切未启动 GUI、未开窗或操控桌面，自动证据和用户视觉签收不互相冒充。
- 普通短键与长条 head/tail/body 视觉仍待用户集中确认；当前纵切不包含 key、mania compatibility、完整 layout/G1/scene/script、作者 `Suppress` 或整包原子重载。tail 的透明 protected fallback 只是迁移链底表现，不是作者 `Suppress`。

### 文档：2026-07-16 健康治理

- 仅治理 `doc_md` 与 memory 的职责边界和低噪声结构；未修改 runtime/code，未运行产品测试或 Release，2026-07-15 仍是当前产品证据。
- 子线当前态、计划、约束与历史已重新归位；最终相对链接与 whitespace 检查结果记录在本次 [CHANGELOG](CHANGELOG.md)。

### 手工门素材：2026-07-16

- 新增 OMS 自生成、确定性的 good/broken `.osk` 与静音 7K `.bme` 生成器，以及隔离 exact-scene 自动可视预检入口；它们只提供可复现输入和自动预检，不改变 runtime 或用户视觉 gate 结论。
- 原始 26 项中的 beatmap-local 是 provider-contract fixture，不是 `WorkingBeatmap` / `chartbms/` 集成，因此手工素材不得宣称 beatmap-local 已通过。

## 当前风险

- schema 56 的四个无 authority orphan blob 已保全并暂留；schema 57迁移保持owner=null，当前scanner也不会claim、去重或清理它们。
- 当前真实 package 纵切只覆盖 BMS 普通短键与长条 head/body/tail；单组件安全替换不等于整包/全 playfield 同帧原子 reload。
- `SkinFilesystemStorageResolver` 返回的 normalised lexical path 只表示检查当时的声明/preflight，不是 capability；production managed folder factory 现已只消费 resolver-issued request 经 Windows fixed-handle capture 完整成功后返回的 exact capsule，不从 normalised path 或 live `NativeStorage` 直接进入 parser。capture 仍不是 mutation token、external adapter 或 filesystem transaction。
- managed folder现可在重启后的完整稳定scan中自动发现并进入选择面；scanner不watch启动后的磁盘变化、不自动选择或reload，旧mutation入口虽已冻结，也不能替代专用no-follow journal/rollback服务。
- active capsule 已与当前实例绑定且磁盘变化不会混入，但旧 owner 的退役必须等待全consumer detach；当前没有整包reload publication barrier，不得把一次selection pair提交写成全playfield同帧reload。
- 成功 preparation cache 仍按 `BmsLegacySkin` 实例复用；managed folder 实例的 source 已固定为 immutable capsule，因此磁盘变化不会污染 cache，但新 revision 必须经新实例与 publication barrier 发布。`SV1-2` 仍须闭合这一整包 reload/旧 owner 退役流程。
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
2. 继续 `SV1-2`：下一刀进入专用managed mutation，以no-follow identity、containment、冲突拒绝与journal/rollback闭合import/rename/delete；随后external registration/capture与atomic reload/detach barrier各自独立过门。当前启动scanner只负责记录reconcile，active实例对磁盘变化保持immutable而不会自动reload。
3. 剩余 optional slot 不再沿私有逐件 C# provider/display 扩张，留给后续 shared scene/runtime 接管。只有视觉结论实际决定下一实现时才暂停；期间保持 nullable `ISkin`、程序化 `OmsSkin`、当前 fallback authority 与 Skin V1 未交付状态不变。
