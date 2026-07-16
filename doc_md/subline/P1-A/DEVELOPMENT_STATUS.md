# P1-A 当前状态：Skin V1、产品面与 release gate

> 最后更新：2026-07-17
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，稳定合同见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)。恢复与架构证据按需查 [恢复审计](../../other/SKIN_SYSTEM_RECOVERY_20260710.md) 和 [V1 架构审计](../../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

## 一句话状态

`SV1-0` 自动、schema 56 数据与用户实机 gate 已全部通过；`SV1-1` 的首个 managed `.osk` BMS Note/LN 产品纵切现已覆盖普通短键与长条 head/body/tail，四组件的自动、合同、安全和回退 gate 均已闭合，`V-001`～`V-004` 集中待验收。这只表示首个产品纵切自动闭环，不是 `SV1-1` 完成或产品交付；视觉待签收不再串行阻塞后续自动可证切片。工程现处 `SV1-2`：folder authority/path preflight、resolver-issued managed Windows fixed-handle/handle-relative no-follow capture 与 pure immutable capsule 的内部链已闭合到 capsule，但尚无 `SkinManager`/production managed folder factory/选择消费方；scanner、删改与整包原子重载也未接通。

## 当前产品能力

- **恢复基线可用**：`.osk`/legacy mania、BMS F1 静态颜色/纹理/几何、选择链与程序化 `OmsSkin` 迁移 fallback 保持可用。
- **实现并自动验证的新增可见能力为 4**：selected managed package 可为 BMS 普通短键提供 `name-0`、`name-1`…编号帧动画，也可为 `NoteImage{lane}H/L/T`（含 `S`/`S2`）长条 head/body/tail 提供静态图和同规则 60 FPS 动画；普通短键静态 `NoteImage` 属恢复基线，不重复计数。
- **产品视觉签收为 0/4**：普通短键、长条 head、tail 与 body 分别登记为集中视觉项 `V-001`～`V-004`，用户尚未签收，因此只能称实现/自动 gate 通过，不能称已交付功能。
- **安全回落覆盖 Note/Head/Body/Tail**：selected 单组件缺失、损坏、空值、越权或超预算时逐组件回落；body 是不可 `Suppress` 的 critical 组件，资源失败才 `Inherit`，有效 body 即使 width 缺失或非法也继续使用同组件与默认 `0.5775`。坏 body/tail 都不能从低层裸同名纹理拼件，低层自己的完整组件仍可接管；tail 保持 optional 透明 protected fallback。异步换源只发布当前 revision 的完整结果。beatmap-local 优先目前只是注入式 provider-order 合同，不是真实 BMS `WorkingBeatmap` 能力。
- **整体仍不可用**：`SV1-1` 未完成；`SV1-2` 只有 early carrier、内部 authority/path preflight、pure capsule 与 managed Windows native capture，没有生产 folder skin 能力；`SV1-3`～`SV1-7` 未实现。不能把首个纵切或这三项内部原语描述成 Skin V1 可用。

| 产品交付面 | 当前状态 |
| --- | --- |
| BMS 普通短键编号帧动画 | 实现/自动 gate 已过；`V-001` 集中视觉待验收，未交付 |
| BMS 长条头静态图/编号帧动画 | 实现/自动 gate 已过；`V-002` 集中视觉待验收，未交付 |
| BMS 长条尾静态图/编号帧动画 | 实现/自动 gate 已过；`V-003` 集中视觉待验收，未交付；透明链底不是作者 `Suppress` |
| BMS 长条身静态图/编号帧动画与安全宽度 | 实现/自动 gate 已过；`V-004` 集中视觉待验收，未交付；critical、不可 `Suppress` |
| gameplay slot 三态 | 普通短键/长条头/body critical 与长条尾 optional slot 已消费 `Provide/Inherit`；作者 `Suppress` 与其它 slot 未交付 |
| canonical `oms-simple.osk` fallback | 未交付；实际链底仍是程序化 `OmsSkin` |
| G1 文件夹导入/选择/原子重载 | 未交付；managed preflight/capture 内部链已闭合到 pure capsule，但仍无 `SkinManager`/production managed folder factory/选择消费方 |
| 统一 layout descriptor/solver | 未交付；现有 geometry provenance 不是有效 layout |
| shared ini codec/结构化诊断 | 未交付 |
| scene/event runtime 与 sandbox script | 未交付 |
| `oms-simple.osk` / `oms-complex.osk` / Authoring Kit | 未交付 |

## 当前实现事实

- `SkinManager` 当前皮肤后仍恒接程序化 `OmsSkin`；最终链底必须由只读、完整验证的 `oms-simple.osk` 接管。
- `BmsLegacySkin` 继续叠加解析 `[Bms]` 并保留 `[Mania]`；native BMS 普通短键与长条 head/body/tail 是当前仅有的真实 package 文件纵切。
- internal 26 项 semantic slot、neutral lane identity/topology/revision、config presence/provenance、六类 lane-resource resolution、event envelope/order 与 capability decision foundation 已落；它们仍是 process-local 合同地基，不是作者 manifest、完整 layout、生产事件 runtime 或 sandbox。
- geometry snapshot 仍只保存 parser 接受的来源事实；当前只有 `LongNoteBodyWidth` 进入唯一共享 scalar resolver，按 finite 且 `0 < width <= 1` 验证并对缺失/非法值逐字段回落 `0.5775`。其它 geometry 的 finite/range/screen-space validation 尚未进入统一 descriptor。
- G1 已在既有 folder constructor/schema 56 字段上增加 ruleset-neutral 的存储声明分类与现存目录 lexical/reparse preflight：区分 Realm `.osk`、`chartskin/<name>` managed folder、只读 drive-letter-qualified Windows external folder及 typed invalid；拒绝双 authority、managed/external namespace 重叠、root/ancestor reparse、盘符根、UNC/device/ADS/traversal/Windows 歧义名，安全字符串不展开路径。它只是一瞬时只读预检，不证明路径物理上位于本地盘，也不是 resolved identity、mutation token、package inventory 或生产 folder store；scanner、选择、删改和原子热重载仍无可信生产链。
- shared core 已增加 pure immutable capsule：自身只接收逻辑 file/directory entries、declared length与read callback，不依赖 path、authority、Storage或filesystem API；现在已有managed native capture这一内部producer，但仍无产品消费方。资源名统一slash/NFC后按Windows大小写语义拒绝重复、非法段与file/directory层级冲突；精确复制declared bytes，以规范名、长度和文件SHA-256形成版本化整包content revision。capsule自有backing，resource view非owning且返回defensive copy；预期读取失败typed reject，取消传播，失败/取消清理provisional backing。
- shared core 已增加 managed-only Windows native capture producer：resolver成功分类的 `chartskin/<direct-child>` 会得到构造受internal issuer约束的opaque request；这不是filesystem或mutation capability。producer从严格物理本地卷handle出发，以handle-relative、no-follow方式固定authority/package/全部目录与文件identity，拒绝reparse、未由resolver展开成长名的8.3/alternate alias、hardlink、重复物理identity、忙写源、unsupported volume mapping与枚举/读取竞态，再把held-handle bytes交给pure capsule。成功前会复验pinned metadata、完整inventory、authority links与package root，且不泄露handle/live stream；它仍无`SkinManager`/production managed folder factory/选择消费方，也不是filesystem transaction、external capture、scanner、mutation或reload。
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
| 6 | `SV1-2` G1 安全存储与原子重载 | **进行中**；resolver-issued managed request → Windows handle-relative no-follow capture → pure capsule 的内部自动合同已闭合，但无产品消费方；下一步是 production managed folder factory/选择，`SV1-3`～`SV1-7` 仍未完成 |

## 最新验证

### `SV1-2` 第三刀 managed Windows handle-relative no-follow capture：2026-07-17

- resolver 现在只为合法 managed `chartskin/<direct-child>` 发出构造受 internal issuer 约束的 process-local capture request；它不是 filesystem/mutation capability，Realm `.osk`、external 与 invalid declaration 均不产生 request。Windows adapter 只接受 `QueryDosDevice` 返回的 exact `\Device\HarddiskVolume<uint>` 映射，从该 NT volume handle 逐段相对打开 data root、`chartskin`、package 与全部子项，并组合 `OBJ_DONT_REPARSE`、`FILE_OPEN_REPARSE_POINT`、`FileIdExtdDirectoryInformation`、file ID/volume serial、link count、metadata 与 inventory 复验。SUBST、mapped/remote drive、shadow/device alias、reparse、未由 resolver 展开成长名的 8.3/alternate alias、hardlink/重复 identity、unsupported entry、busy writer 及竞态均 fail-closed。
- 所有目录和文件在读取前固定并持有到 capsule 构造及最终复验完成；native enumeration 在扩充托管集合前执行取消与 entry budget，文件流按最多 1 MiB 分段读取以保留取消响应。capsule 只在所有 pinned metadata、目录 inventory、authority link 与 package root 再次一致后返回，成功前先释放全部 handle；失败、取消、意外异常或 dispose 异常会继续回收全部 handle 与 provisional capsule。保证刻意窄于 filesystem transaction：只保证已发布 bytes 来自 held identity，且最终复验前观察到的变化会拒绝。
- capture focused **47/47**（36 项 deterministic fake 合同 + 11 项真实 Windows，0 skipped），preflight + capsule + capture 合并 **167/167**。真实门覆盖当前 x64 进程 ABI layout、嵌套/空包、entry budget、busy writer、hardlink、package/nested junction、resolver 对现存 8.3 路径的长名规范化、捕获期间反向写入/文件 rename 被共享模式阻止，以及新增 child 后 final inventory typed reject；fake 另锁 request 发出后 package 在首次 open 前消失的 typed cleanup。core `osu.Game.Tests.Skins` **224/229**，5 项失败仍是 1 项 Argon 默认皮肤旧期待与 4 项已删除 Osu ruleset archive fixture；mania skin **182/182**、BMS skin **583/583**。Release Rebuild **0 error / 11 known warnings**，即 9 条 MessagePack 3.1.3 `NU1902` 与 BMS tests 既有 `CS8600`/`CA2007`。
- 生产/测试 targeted format verify 通过；文档健康检查通过 **123 个 Markdown / 969 个相对链接 / 31 个 memory wiki 链**，仅保留 mainline plan 数字比值的既有非失败提醒，`git diff --check` 通过。测试只使用 deterministic fake 与系统临时目录；真实 junction 在隔离目录创建并定点清理，未访问生产 Realm、`chartskin/`、用户目录或网络，未启动 GUI 或操控桌面。未实际执行 SUBST 命令级集成；其 fail-closed 由 exact volume-target classifier 与 fake alias 合同覆盖。当前仍没有 production managed folder factory、`InstantiationInfo`/选择资格、scanner owner、mutation service 或原子 reload，不得称 G1、Skin V1 或产品交付完成。

### `SV1-2` 第二刀 pure immutable package revision capsule：2026-07-17

- shared core 新增纯 post-capture capsule：只接收调用方提供的逻辑 file/directory entries，不接收 path、authority 或 Storage，自身不访问文件系统；本节当时尚无 producer，现已有第三刀 managed native internal producer，但仍无产品消费方。空目录参与 entry/depth budget，但不改变内容 revision。
- focused **66/66**，与第一刀 preflight 合并 focused **120/120**；覆盖 slash/NFC/Windows case canonicalisation、固定 revision vector、duplicate/path-type conflict、entry/file/depth/name/raw-byte budget、全量预检零 source open、精确长度与合法短读、不可读/预期及非预期异常/malformed stream、取消、stream disposal、失败与 capsule 退役时 backing 清零、defensive copy、非 owning view、只读 metadata 和安全字符串。core `osu.Game.Tests.Skins` **177/182**，5 项失败仍是恢复基线同名的 1 项 Argon 默认皮肤旧期待与 4 项依赖已删除 Osu ruleset archive fixture；mania `FullyQualifiedName~Skin` **182/182**、BMS `FullyQualifiedName~Skin` **583/583**。`osu.Desktop.slnf --no-restore` Release Rebuild **0 error / 11 known warnings**，即 9 条 MessagePack 3.1.3 `NU1902` 与 BMS tests 既有 `CS8600`/`CA2007`。
- 独立代码、安全和测试终审在补即时 parent-entry 预算、当前/历史 buffer 清零、合法短读、全预检零 source open、默认预算固定与 NFC lookup 后为 blocker/major **0/0**；剩余仅为测试实现耦合和未穷举同实现分支的 minor，不阻塞 pure capsule 合同。
- 生产与测试改动文件的 targeted format verify 通过；文档健康检查通过 **122 个 Markdown / 967 个相对链接 / 26 个 memory wiki 链**，仅保留 mainline plan 数字比值的既有非失败提醒；`git diff --check` 通过。
- 测试全为 deterministic in-memory fake；生产 Realm、`chartskin/`、用户目录和网络零访问，未启动 GUI 或操控桌面。本刀不证明 no-follow、final physical identity、8.3/SUBST/junction/hardlink alias、同长度读取中变化、capture atomicity、`InstantiationInfo`、选择资格或原子 reload；不得称 G1、Skin V1 或产品交付完成。

### `SV1-2` 第一刀 authority/path preflight：2026-07-17

- 新增 focused **54/54**：闭合 schema 56 的 Realm/managed/external/invalid 分类、folder `Files` 双 authority、protected/fixed-ID/DeletePending、managed direct-child、drive-letter-qualified external、managed/external namespace exact/ancestor/descendant 冲突、traversal/ADS/UNC/device/盘符根/尾点尾空格/Windows 设备名、缺失/文件目标、data root/managed root/package/external ancestor reparse、typed IO/path failure、诊断脱敏及 managed/external bytes+mtime+`SkinInfo` 零 mutation。
- core skin aggregate **111/116**；5 项失败仍是恢复基线同名的 1 项 Argon 默认皮肤旧期待与 4 项依赖已删除 Osu ruleset beatmap archive fixture，无新增失败。跨规则集 relevant 为 mania `FullyQualifiedName~Skin` **182/182**、BMS `FullyQualifiedName~Skin` **583/583**。最终 `osu.Desktop.slnf` Release Rebuild **0 error / 11 known warnings**，即 9 条 MessagePack 3.1.3 `NU1902` 与 BMS tests 既有 `CS8600`/`CA2007`。
- 三路独立只读终审在修正 mutation-token/canonical/no-follow 误称、external 与 managed namespace 重叠、external 纯度和 typed reason 后为 blocker/major **0/0**。当前 preflight 没有生产调用者；测试只用隔离临时目录和注入式文件属性 probe，未访问生产 Realm、`chartskin/`、用户目录或网络，未启动 GUI 或操控桌面。
- 尚未证明 8.3/SUBST/物理 identity 去重、真实 junction 集成、TOCTOU 安全打开、完整资源 inventory、`InstantiationInfo`、`NativeStorage` folder store、scanner ownership、mutation 或原子 reload；这些继续是同一 `SV1-2` 的后续 gate，不能用本组 54 项冒充。

### 产品/runtime 与 gate 工具：截至 2026-07-17

- 产品自动验收扩为 **94/94**，并连续三轮全绿：在 Note/Head/Tail 矩阵上增加 body 静态图/60 FPS 连续编号帧、7K normal/scratch、14K `S2L`、A→B、有效 body + width 缺失/非法逐字段默认、坏 body 隔离、低层裸同名防串/完整组件接管、真实 Idle/Holding/Broken、异步首次挂载当前态与 HCN regrab。beatmap-local 项仍只是注入式 provider-order fixture。
- 合并态 BMS skin/runtime focused **326/326**，BMS full **1456/1456**；`osu.Desktop.slnf` Release **0 error / 11 known warnings**。保留 9 条 MessagePack 3.1.3 `NU1902` 与 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。
- 改动文件的定向 format verify 通过；唯一排除的是文件内既存 `IDE1006` 命名债。独立终审 blocker/major **0/0**。
- 本次只改 BMS ruleset 内的 managed note provider、renderer host 与测试，没有改 shared `osu.Game` skin ABI、mania compatibility 或 fallback authority，因此未另跑 core/mania 产品测试；Release 已编译 core、mania/BMS 与两个 test project。普通短键的 generator/staging/scene/runner safety 既有 **53/53** 结论未被本切重跑或冒充本切数字。
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

- schema 56 的四个无 authority orphan blob 已保全并暂留；不得把本次定点处置当作 scanner 批量清理先例。
- 当前真实 package 纵切只覆盖 BMS 普通短键与长条 head/body/tail；单组件安全替换不等于整包/全 playfield 同帧原子 reload。
- `SkinFilesystemStorageResolver` 返回的 normalised lexical path 只表示检查当时的声明/preflight，不是 capability；production managed folder factory 必须只消费 resolver-issued request 经 Windows fixed-handle capture 完整成功后返回的 exact capsule，不得从 normalised path 或 live `NativeStorage` 直接进入 parser。当前 capture 也不是 mutation token、external adapter 或 filesystem transaction。
- 成功 preparation cache 目前不感知同一 `BmsLegacySkin` 实例内的原地 source revision 变化；现状不会混合或发布过期 material，而是安全保留旧视觉/回落并要求重建实例。`SV1-2` 必须把这项作为原子 reload 风险处理。
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
2. 继续 `SV1-2`：下一刀接 production managed folder factory/选择，只允许 resolver-issued managed request 的 native capture 完整成功 capsule 建立新实例，并精确验证 folder `InstantiationInfo`；普通 `.osk`/Realm 路径保持原行为，失败保留当前选择并释放 provisional owner。随后 scanner owner tag、安全 mutation 与原子 reload 各自独立过门，当前同一 `BmsLegacySkin` 实例原地改源需要重建实例的限制必须在该门消除或显式冻结。
3. 剩余 optional slot 不再沿私有逐件 C# provider/display 扩张，留给后续 shared scene/runtime 接管。只有视觉结论实际决定下一实现时才暂停；期间保持 nullable `ISkin`、程序化 `OmsSkin`、当前 fallback authority 与 Skin V1 未交付状态不变。
