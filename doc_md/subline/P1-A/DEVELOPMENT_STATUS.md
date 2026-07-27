# P1-A 当前状态：Skin V1、产品面与 release gate

> 最后更新：2026-07-27
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，稳定合同见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)。恢复与架构证据按需查 [恢复审计](../../other/SKIN_SYSTEM_RECOVERY_20260710.md) 和 [V1 架构审计](../../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

## 一句话状态

`SV1-0` 自动、schema 56 数据与用户实机 gate 已全部通过；`SV1-1` 的首个已导入 `.osk` BMS Note/LN 产品纵切现已覆盖普通短键与长条 head/body/tail，四组件的自动、合同、安全和回退 gate 均已闭合，`V-001`～`V-004` 集中待验收。这只表示首个产品纵切自动闭环，不是 `SV1-1` 完成或产品交付；视觉待签收不再串行阻塞后续自动可证切片。`SV1-2` 的schema 57 owner、held-root启动scanner、exact-capsule factory/guarded selection及专用mutation authority/recovery foundation现已闭合：scanner/selection/mutation共享线性化边界，版本化durable journal在启动scanner前幂等恢复，歧义状态冻结相关路径，current delete只可先确认程序化`OmsSkin`受保护pair。foundation没有物理或Realm写primitive，也未开放rename/import/delete UI；下一门仍是rename独立端到端切片，随后才是staged import、delete、external与atomic reload/detach。

## 当前产品能力

- **恢复基线可用**：`.osk`/legacy mania、BMS F1 静态颜色/纹理/几何、选择链与程序化 `OmsSkin` 迁移 fallback 保持可用。
- **实现并自动验证的新增可见能力为 4**：选中的用户 BMS 包可为普通短键提供 `name-0`、`name-1`…编号帧动画，也可为 `NoteImage{lane}H/L/T`（含 `S`/`S2`）长条 head/body/tail 提供静态图和同规则 60 FPS 动画；普通短键静态 `NoteImage` 属恢复基线，不重复计数。
- **产品视觉签收为 0/4**：普通短键、长条 head、tail 与 body 分别登记为集中视觉项 `V-001`～`V-004`，用户尚未签收，因此只能称实现/自动 gate 通过，不能称已交付功能。
- **安全回落覆盖 Note/Head/Body/Tail**：selected 单组件缺失、损坏、空值、越权或超预算时逐组件回落；body 是不可 `Suppress` 的 critical 组件，资源失败才 `Inherit`，有效 body 即使 width 缺失或非法也继续使用同组件与默认 `0.5775`。坏 body/tail 都不能从低层裸同名纹理拼件，低层自己的完整组件仍可接管；tail 保持 optional 透明 protected fallback。异步换源只发布当前 revision 的完整结果。beatmap-local 优先目前只是注入式 provider-order 合同，不是真实 BMS `WorkingBeatmap` 能力。
- **Skin V1完整产品面仍未交付**：`SV1-1` 未完成；`SV1-2` 已有受管目录启动发现、生产选择和mutation/recovery公共地基，但没有任一真实rename/import/delete、external或原子reload；`SV1-3`～`SV1-7`未实现。不能把foundation描述成G1、Skin V1或产品交付完成。

| 产品交付面 | 当前状态 |
| --- | --- |
| BMS 普通短键编号帧动画 | 实现/自动 gate 已过；`V-001` 集中视觉待验收，未交付 |
| BMS 长条头静态图/编号帧动画 | 实现/自动 gate 已过；`V-002` 集中视觉待验收，未交付 |
| BMS 长条尾静态图/编号帧动画 | 实现/自动 gate 已过；`V-003` 集中视觉待验收，未交付；透明链底不是作者 `Suppress` |
| BMS 长条身静态图/编号帧动画与安全宽度 | 实现/自动 gate 已过；`V-004` 集中视觉待验收，未交付；critical、不可 `Suppress` |
| gameplay slot 三态 | 普通短键/长条头/body critical 与长条尾 optional slot 已消费 `Provide/Inherit`；作者 `Suppress` 与其它 slot 未交付 |
| canonical `oms-simple.osk` fallback | 未交付；实际链底仍是程序化 `OmsSkin` |
| G1 文件夹发现/选择/原子重载 | 部分实现、未交付；schema 57 exact-owner启动发现与production factory/选择自动gate已过，但专用删改、external与原子reload/detach未实现 |
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
- managed启动scanner从同一held `chartskin` handle建立baseline direct-child inventory，并以同一authority相对capture候选，最终复验完整inventory与authority links。合法名字即使对应file/reparse/坏包也进入Observed，只有capsule及根`skin.ini` metadata有效的包进入Valid；NTFS目录时间延迟只允许最多3次、每次25ms、可取消的完整session重试，任何失败轮不发布partial snapshot。schema 57迁移不回填owner，scanner只在完整scan的单一Realm事务内维护exact-own记录；null/foreign/同路径冲突/普通`.osk`不动，取消在commit前回滚，negative只soft-delete Realm记录而不碰磁盘。scanner现从discovery到Realm commit全程持有共享coordinator边界，并服从recovery冻结。
- production `SkinManager` 现在只为 Realm 中 authoritative `IsManaged` 且解析合法的 folder 记录启动后台 capture；capture 后以 exact ordinal allowlist 验证 `InstantiationInfo`、要求根 `skin.ini` 与精确 capsule 构造入口，factory 前后均复核 authoritative record，最终在共享coordinator内重新取得本Realm authoritative live record并一次提交 `CurrentSkinInfo`/`CurrentSkin`。未注册/unmanaged、external、非法类型、hardlink、capture/factory 失败、过期 generation、竞态、reentrant 请求或 completion scheduler fault 均保留旧 pair并回收 provisional owner；普通 `.osk`、`OmsSkin` 与 mania 路径不改。
- committed selection 使用封闭绑定图与显式 request surface，generic `Bindable`/Dropdown/lease 不能两向绕过预提交 gate；settings 只镜像已提交值。folder 的旧编辑、导出、重命名、delete/undelete、文件 mutation、update-import 与 external-edit 路径均冻结，并在真正 Realm mutation 内按 ID 重新取得 authoritative record，调用方伪造/陈旧 `SkinInfo` 字段不能授权。新的专用foundation只签发held existing/staged source、root-bound absent target slot、immutable新记录publication plan与exact durable receipt；它没有实际写入方法，active owner的全consumer detach barrier也尚未实现。
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
| 6 | `SV1-2` G1 安全存储与原子重载 | **进行中**；schema 57受管目录启动发现、production factory/选择及mutation authority/recovery foundation自动门已闭合；下一步按rename、staged import、delete独立过门，之后推进external与atomic reload/detach；`SV1-3`～`SV1-7`仍未完成 |

## 最新验证

### `SV1-2` managed mutation authority/recovery foundation：2026-07-27

- 代码取证确认旧scanner只在单实例内串行、discovery后释放held root，selection使用私有边界，普通delete只异步调度fallback。现以共享coordinator串行scanner完整discovery→Realm commit、selection最终authoritative重读/pair发布、mutation与recovery；`SkinManager`构造期先恢复，启动worker再在同一外层lease中幂等执行recovery→scanner。
- 既有record authority按ID刷新重读并锁唯一direct-child、空`Files`、非external/protected/fixed/DeletePending、exact owner、allowlist与revision；Windows session从物理卷持有data root/`chartskin`/source no-follow identity，target只是held root绑定的规范化空name slot。staged source固定为`skin-mutation-staging/{operationId:N}`同卷held identity；新记录只获得planned ID/path/root/version的immutable publication plan，scanner不能消费且尚不是Realm writer。
- canonical version-1 journal使用严格schema/checksum/UTF-8/大小预算、flush与Windows write-through原子replace，phase单调且terminal不可重写。启动恢复对可判定状态幂等forward/rollback；无operation handler的有效非terminal保留journal并精确冻结路径，invalid/unknown/IO全局冻结，terminal compare-delete后确认Missing才解冻。scanner正向与negative reconcile、selection及新mutation都服从冻结。
- delete foundation要求update thread、held mutation reservation与exact Prepared receipt；current目标必须先把`CurrentSkinInfo`/`CurrentSkin`都确认到受保护程序化`OmsSkin`，只有两半ID一致且都不是目标才可`NotRequired`。split pair、selection disabled、fallback/receipt/authority失败都拒绝并在无外部mutation时回滚journal；没有执行Realm或物理删除。
- focused合并为authority/journal/coordinator/scanner/native/lifecycle **107/107**，BMS production selection/fallback **24/24**；扩大回归core skin **337/341**（4项均为既有removed Osu archive fixture，Argon旧失败本轮通过）、mania skin **182/182**、BMS full **1492/1492**。三工程targeted format verify通过，`osu.Desktop.slnf` Release **0 error / 18 emitted known warnings**；文档健康检查与`git diff --check`通过。最终独立审查blocker/major **0/0**。全程未启动GUI或操控桌面，`V-001`～`V-004`仍0/4。

### `SV1-2` schema 57 scanner owner与受管目录启动发现：2026-07-17

- Realm schema升至57并增加nullable opaque `FilesystemStorageAuthorityOwner`，真实schema56升级保持旧owner=null；Windows discovery从held `chartskin` authority严格分离Observed/Valid并复用native no-follow capsule，只有完整稳定scan可在单一事务内维护exact-own记录，根失败/竞态/非法snapshot/异常/取消零提交且commit前取消整事务回滚。`OsuGame.LoadComplete`只在线程池执行一轮scan，Dispose先cancel+join再释放Realm，owner变化也会阻止in-flight旧prepared skin发布。focused为schema/scanner **12/12**、native fake+真实Windows **55/55**、headless lifecycle **2/2**、BMS production selection **15/15**；扩大回归为core相关 **222/222**、mania skin **182/182**、BMS full **1483/1483**，三工程format verify通过，Release **0 error / 20 emitted known warnings**，独立安全审查blocker/major **0/0**。该产品基线未操控GUI，视觉仍0/4；它不是watcher/热重载，也未实现专用managed mutation、external或atomic reload/detach。

## 当前风险

- schema 56 的四个无 authority orphan blob 已保全并暂留；schema 57迁移保持owner=null，当前scanner也不会claim、去重或清理它们。
- 当前真实 package 纵切只覆盖 BMS 普通短键与长条 head/body/tail；单组件安全替换不等于整包/全 playfield 同帧原子 reload。
- `SkinFilesystemStorageResolver` 返回的 normalised lexical path 只表示检查当时的声明/preflight，不是 capability；production managed folder factory 现已只消费 resolver-issued request 经 Windows fixed-handle capture 完整成功后返回的 exact capsule，不从 normalised path 或 live `NativeStorage` 直接进入 parser。capture 仍不是 mutation token、external adapter 或 filesystem transaction。
- managed folder现可在重启后的完整稳定scan中自动发现并进入选择面；scanner不watch启动后的磁盘变化、不自动选择或reload。专用mutation foundation已提供held authority、durable journal/recovery与共享线性化，但旧mutation入口仍冻结，且没有任何物理写入或Realm新记录publisher。
- filesystem与Realm不能组成同一原子事务；公共foundation只关闭首个外部步骤前的Prepared journal、启动恢复先于scanner、歧义冻结和参与者互斥。受管目录“rename”究竟只改目录名、只改`skin.ini`展示名还是两者联动仍未冻结，staged import的copy/move与冲突语义、新记录one-shot publisher、各操作crash-point handler也尚未实现；现有通用Rename/Delete/Import UI继续保持禁用。
- current managed delete的fallback pair确认门已存在，但实际delete仍没有入口；canonical接管前只接受受保护程序化`OmsSkin`，以后必须改为已验证只读`oms-simple.osk`。任何split pair或无法确认都拒绝，不得把foundation写成删除能力。
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
2. 继续 `SV1-2`：下一刀先冻结rename究竟改变目录名、`skin.ini`展示名或两者联动，再以现有authority/journal foundation实现physical source→target、final identity、Realm一致性和各crash point的独立恢复handler；自动、安全与恢复gate闭合前不开放UI。之后依次推进staged import（copy/move、冲突策略与真正one-shot record publisher）、delete（消费已闭合的protected pair门后才允许实际删除）、external与atomic reload/detach。
3. 剩余 optional slot 不再沿私有逐件 C# provider/display 扩张，留给后续 shared scene/runtime 接管。只有视觉结论实际决定下一实现时才暂停；期间保持 nullable `ISkin`、程序化 `OmsSkin`、当前 fallback authority 与 Skin V1 未交付状态不变。
