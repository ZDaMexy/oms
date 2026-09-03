# P1-A 当前状态：Skin V1、产品面与 release gate

> 最后更新：2026-09-03
> 全局状态见[../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，执行顺序见[DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，稳定合同见[TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)。

## 一句话状态

`SV1-0`自动、schema 56数据与用户实机gate已全部通过；`V-001`～`V-004`仍为 **0/4**。七个持久campaign中的`C1`作者工作区/archive、`C2`三源current revision、`C3` P1-K前置+唯一immutable gameplay layout、`C4` 28项public catalog+唯一shared codec+显式三态resolved material、`C5` versioned scene/animation/read-only event与全部剩余public slot production均已闭合，当前为 **`5/7 closed，C6 active`**。这不是线性百分比；`SV1-1`、`SV1-2`整体、Skin V1与release仍未完成，C6的sandbox/final package reload gate及C7 canonical双包/Authoring Kit尚未交付，程序化`OmsSkin`仍是迁移链底。

## 当前产品能力

- **C1作者工作区与安全导入已冻结**：external只读注册/选择/configured restart/Open/Managed Copy/Unregister，managed Open/Rename/Delete，single-v3 journal/recovery及ordinary`.osk`有界准入/zero-residue receipt均有真实caller。external source bytes只来自fresh held capture，service-owner不授权source mutation。
- **C2 revision生命周期已冻结**：Settings → Skin的`Reload current skin`是ordinary Realm`.osk`、managed与external三源唯一manual reload；live gameplay/preview在任何source prepare前拒绝。background prepare、update-thread可回滚commit、participant/work lease、dynamic attach/detach、late attach与最后detach exactly-once retire统一覆盖current mutation；没有watcher、same-value reload、行级reload或legacy editor/update-import旁路。
- **C3 P1-K与唯一layout已冻结**：parser/converter是keymode、lane count、keysound timeline唯一truth；BMS唯一solver与mania adapter消费同一neutral snapshot。5K/7K四style、9K BMS/PMS、14K双deck及mania single/dual使用stable LaneId/GroupId和显式logical/visual/global/group-local index；BMS/mania/core/playfield/HUD/BGA viewport全部从同一exact publication取geometry。
- **C4 public catalog已冻结**：`GameplaySkinSlotCatalog`包含Common v1与唯一BMS v1 extension共28项stable ID，digest为`28f282d31eeb9097fa8184729b72f7b59d9635bab11c0dd459648325ec65b96d`。scope、type、Required/Recommended/Optional、inherit、Suppress资格、ruleset/keymode/stage/lane-role与diagnostic code完整定义；runtime capability与目录语义分层。作者合同见[Gameplay Skin V1公共目录](../../other/GAMEPLAY_SKIN_PUBLIC_CATALOG_V1.md)。
- **C4唯一shared codec已冻结**：每个package的exact`skin.ini` bytes只capture/hash/tokenize一次；public Common/BMS与legacy`[Mania]`/`[Bms]` adapter消费同一防御性immutable document。Absent、DeclaredEmpty、Invalid、Valid与Suppress、duplicate、escaping、case、comment、unknown version/field、illegal scope/type/index/selector及canonical round-trip均有稳定合同；consumer不重开ini或二次tokenize。
- **C4显式三态resolver已冻结**：null、缺dictionary entry、异常与`Drawable.Empty()`不再暗示状态。package内exact ruleset/keymode/stage/scope specificity及authority precedence固定；Required/Recommended非法Suppress稳定诊断并继续确定fallback，只有Optional + runtime capability允许终止。invalid/empty不会冒充absent或回头借同package较宽声明。
- **C4 BMS/mania真实consumer已闭合**：ordinary/managed/external三源public声明从真实`SkinManager` current revision经ruleset prepare驱动actual BMS Note/LN与mania Note/Hold/KeyVisual。BMS 5K`[Bms]→Keys6→Keys5`、7K`→Keys8→Keys7`、9K`→Keys9`且不重复、14K`→Keys16→两个Keys8 deck→Keys14`进入production；9K raw`0..8`/canonical`1..9`由版本化合同映射。BMS static/固定60FPS与mania legacy兼容保持。
- **C2+C3+C4+C5同一publication**：`GameplaySkinLayoutPublication`一次绑定package、neutral snapshot、typed adapter、resolved material set与prepared scene/event state。所有parse/validate/resource/material/scene失败止于background prepare；commit只交换prepared immutable引用。prepare/commit复核participant generation、selection、exact source/content/package/layout/material/scene与全部contract version；失败保exact A，late attach只取得已提交quadruple，old owner最后detach后retire。
- **BMS exact material资源寿命已闭合**：selected package的Note/LN exact preparation按layout revision签发ref-counted borrow；publication构造、prepare、取消或commit拒绝都会exactly-once退役未采用borrow，成功commit则把borrow转交唯一layout owner。`RulesetSkinProvidingContainer`先完成renderer子树detach/dispose、再释放owner；`BmsLegacySkin.Dispose`会封门、将generation标记为退役并取消/join work，但不会在active publication仍借用prepared texture revision时提前清理它。
- **diagnostic可理解且脱敏**：只有成功commit后才异步输出该revision的去重、确定排序安全摘要；持久文本只含public code、catalog ID、stable target/index、source kind与合同版本，不含路径、作者值、display name、record ID/hash或exception text。日志故障不能改变commit。
- **beatmap-local终态**：C4不新增beatmap-local gameplay-skin作者格式；public source/candidate不可达，因为没有安全sidecar、producer/importer、`WorkingBeatmap` public document/revision authoring ownership与C1/C2闭环。真实importer/manager仍让`WorkingBeatmap.Skin`惰性持有同一只读`LegacyBeatmapSkin`实例，只用于更高precedence的既有direct visual compatibility；resolver/production author path不把其public section作为author authority。作者使用ordinary`.osk`、managed或registered external包。
- **C5 scene/event纵切已接入真实consumer**：exact package content中的`gameplay-skin.json`与`gameplay-skin.scene.json`按`oms-gameplay-skin-manifest.v1`、`oms-gameplay-skin-scene.v1`解码为防御性immutable prepared graph；Sprite/Container/Text/Mask/Clip、allowlisted blend/effect、frame/tween/track、state machine、只读binding、template/instance均在background prepare完成，renderer不重读文件、不二次resolver/资源prepare。坏内容、未知版本/字段、重复、路径逃逸、类型/target/resource错误与预算超限只产生稳定脱敏诊断并保留exact旧publication。
- **C5 read-only event runtime已接入BMS/mania/core**：`oms-gameplay-skin-event.v1` envelope携带stream epoch、monotonic sequence、gameplay/layout/material/scene revision、authoritative gameplay time、LaneId/GroupId、kind与immutable payload；lifecycle/input/object/judgement/score/combo/gauge/timing/BGA只读摘要来自真实ruleset/core host。bounded single-producer stream提供完整Snapshot/Reset，late attach、retry、seek、rewind、publication切换与旧epoch隔离均可重建。envelope可发`GameplayResumed`，scene state-machine ABI刻意不接受`gameplay.resume`：Snapshot已包含Running状态，避免增量事件成为第二状态authority。
- **C5 public slot/runtime capability已诚实闭合**：BMS `oms-gameplay-skin-runtime-support.v1`对catalog 28项均有显式production route（9K适用矩阵为26项，Turntable/Laser按catalog applicability不适用）；mania对28项逐项给出决策，23项真实host可用，`object.mine`、`playfield.turntable`、`playfield.laser`、`bga.viewport`、`bga.frame`为版本化`NotApplicable`，不是静默inherit/unsupported。global/stage/group/lane roots、lane surface/divider、judgement/hit target、cover、barline、stage/backdrop/baseplate、key/keyflash/mine、hit explosion、judgement/combo/gauge/text HUD、turntable/laser、BGA frame/viewport与decoration均通过固定host/scene gate；Note/LN/Hold继续使用池化与同一material。
- **C5预算与确定性**：manifest/scene/package资源、纹理像素/decoded bytes、node/depth、template、effect、track/keyframe、text/glyph、event subscription/queue/frame消费及pool并发均在prepare或runtime硬预算内；update thread无I/O/解码/模板展开，大量14K、双deck、持续LN、judgement/effect/HUD压力走固定池。相同revision/input产生相同event顺序、animation采样、state transition、Snapshot/Reset与resource选择。

## 玩家结果与剩余差距

| 产品面 | 当前结论 | 剩余门 |
| --- | --- | --- |
| 恢复、导入与作者目录安全 | **C1通过** | 保持恢复/receipt/journal边界；不得重做或扩大authority |
| current revision生命周期 | **C2通过** | C5 scene/event host已加入同一participant/lease协议；C6才关闭最终整包门 |
| keymode/lane与唯一geometry | **C3通过** | 保持P1-K唯一truth与single publication；不得创建第二solver/ID/index authority |
| public catalog/codec/三态/material | **C4通过** | C5在其exact publication上接通scene与全部版本化runtime capability |
| 当前可见public纵切 | **BMS/mania全部适用public slot** | 受ruleset applicability约束；mania五项NotApplicable，C6仍需sandbox/final package gate |
| scene/animation/event | **C5通过** | versioned manifest/graph、read-only event Snapshot/Reset、预算、真实BMS/mania host与pool已闭合 |
| sandbox/script | **未实现** | C6 VM/toolchain/authorization/budget/determinism/fuse/profiler与最终整包reload |
| canonical发行闭环 | **未实现** | C7双包、Authoring Kit、validator、只读恢复、程序化`OmsSkin`退出与自动release |
| 人工视觉/release | **0/4待签收，release未完成** | 最终包、真实设备/谱面、视觉与发行复核 |

## 七个持久Campaign

当前为 **`5/7 closed，C6 active`**：

`C1`作者文件工作区/G1 UX ✓ → `C2` current revision reload/detach ✓ → `C3` P1-K+唯一layout ✓ → `C4` shared codec/catalog/resolved material ✓ → `C5` scene/event与剩余slot production ✓ → **`C6` sandbox并关闭最终整包reload门（active）** → `C7` canonical双包/Authoring Kit/自动release。

每个campaign必须由真实authoring caller、production consumer、失败回退、宽测试、文档、独立终审与有意义提交共同闭合；audit、路线决定、DTO、fixture、foundation、单一consumer或提交数不能推进编号。C4完成边界见[C4完成交接](../../other/SKIN_SYSTEM_C4_CODEC_MATERIAL_COMPLETION_HANDOFF_20260831.md)。

## Foundation/caller结论

- **已接production**：BMS configuration candidate、lane-resource provenance/provider、runtime capability、9K mapping及BMS/mania resolved material。
- **已删除**：被catalog/exact layout/material取代的`GameplaySkinLaneColourSnapshot`、legacy mania bucket scalar/array/known-global-colour/known-global-resource/NoteBodyStyle、BMS bucket colour/geometry/declaration factories及其fixtures。
- **已接production**：`GameplaySkinEventStreamCursor`、scene/event runtime、BMS/mania slot hosts、pool与timing projection均已进入C5真实caller；通用capability negotiator/authorization与sandbox仍明确留给C6。
- **isolation/compat seam且不计产品能力**：`BmsGameplaySkinConfigurationCandidateFactory.Create(BmsLaneLayout,...)`、raw requirement resolver overload、`BmsGameplayLayoutProvider.PublishForTesting`与`CompatibilityEmpty`。production只允许exact source/current-contract carrier。

## 当前gate

| Gate | 状态 |
| --- | --- |
| schema 56数据安全/恢复基线实机 | **通过**；无authority orphan blob继续保全，不运行全局cleanup |
| `SV1-1` Note/LN首个纵切 | **自动门已闭合，视觉待签收**；`V-001`～`V-004`为0/4，不等于`SV1-1`完成 |
| `C1`作者工作区 | **通过** |
| `C2`current revision reload/detach | **通过** |
| `C3` P1-K前置与唯一layout | **通过** |
| `C4` public catalog/shared codec/三态/material/mania compatibility | **通过** |
| `C5` scene/animation/event与剩余slot | **通过** |
| `C6` sandbox/最终整包reload | **active** |
| `C7` canonical双包/Authoring Kit/自动release | **未完成** |

## 最新验证：2026-09-03 C5闭门

- core `FullyQualifiedName~GameplaySkin` **429/429**；mania `~GameplaySkin` **69/69**；BMS `~GameplaySkin` **146/146**。新增scene codec/runtime/event、publication、全部slot host与BMS/mania production矩阵均从真实SkinManager current revision进入实际consumer，不使用测试publisher注入最终snapshot。
- BMS full（`--blame-hang --blame-hang-timeout 5m`）**1721/1721**，无hang artifact；BMS current-revision production **215/215**。BMS C5 all-keymode/双deck、custom-fallback/opaque-shell/partial-stage、hit-explosion与timing-epoch矩阵均通过；9K中Turntable/Laser按catalog applicability不计入适用格。
- mania full **860/864**，四项`TestHoldNoteWithReleasePress`、`TestHoldNoteChord`、`TestSingleHoldNote`、`TestHoldNoteStair`与冻结的既有frame-count基线逐字符一致；mania C5 real production matrix覆盖single/dual、lane-cover、hit-explosion及28项逐项capability decision（23 Supported、5 NotApplicable）。
- core `~Skin` **1218/1224**，六项失败精确保持冻结基线：`TestRetrieveAndLegacyExportJapaneseFilename`、`TestRetrieveAndNonLegacyExportJapaneseFilename`、`TestBackgroundCyclingOnDefaultSkin(True)`、`TestRetrievalWithConflictingFilenames`、`TestSampleUpdatedBeforePlaybackWhenNotPresent`、`TestRetrieveOggAudio`；错误分类/消息与既有基线一致，无新增失败。core `~Skins` **746/750**同样只含既有四项archive失败。
- P1-K decoder/converter/timing/keysound **126/126**、converted mania **24/24**、真实BMS keysound lifecycle **17/17**、shared store/DrawableRuleset **68/68**。Windows Release **0 error / 20 emitted known warnings**：既有MessagePack `NU1902` 18次及BMS tests既有`CS8600`、`CA2007`各1次；未用NoWarn隐藏。
- `CheckDocumentation.ps1`、`git diff --check`及owning-project targeted formatter `--verify-no-changes`在本次文档/代码闭门后复验；四类独立终审均GO，blocker/major **0/0**。完整交接见[C5 scene/event完成交接](../../other/SKIN_SYSTEM_C5_SCENE_EVENT_COMPLETION_HANDOFF_20260903.md)。

## 历史验证：2026-09-02 C4闭门

- core public catalog/codec/resolver/revision/beatmap-local focused **141/141**；mania solver、三源public production与Skinning consumer合并C4 relevant **172/172**。formatter后重新build的同两组仍为 **141/141、172/172**。
- P1-K decoder/converter/cache **102/102**、BMS→mania projection **24/24**、BMS真实shared keysound **14/14**、converted mania shared store **2/2**。
- BMS C4 relevant **315/315**（含真实importer→Realm→`BeatmapManager.GetWorkingBeatmap()`→`WorkingBeatmap.Skin`不可达证明及carrier取消所有权）、current-revision product **197/197**、managed candidate/Note product **115/115**；formatter后重新build的C4 relevant仍为 **315/315**。BMS Skin **726/726**，BMS full **1687/1687**，`--blame-hang-timeout 5m`确认全部完成且无hang sequence/artifact。
- core Skin **1110/1116**，六项精确既有失败为`TestRetrievalWithConflictingFilenames`、`TestRetrieveAndLegacyExportJapaneseFilename`、`TestRetrieveAndNonLegacyExportJapaneseFilename`、`TestRetrieveOggAudio`、`TestBackgroundCyclingOnDefaultSkin(True)`、`TestSampleUpdatedBeforePlaybackWhenNotPresent`；失败名称与消息逐字符匹配既有基线，没有新增失败。mania Skin **193/193**，mania full **838/842**，四项精确既有失败为`TestSingleHoldNote`、`TestHoldNoteChord`、`TestHoldNoteStair`、`TestHoldNoteWithReleasePress`，名称与消息逐字符匹配既有基线。
- Windows Release **0 error / 20 emitted known warnings**：9项既有MessagePack `NU1902`在restore/build两阶段共输出18次，另有既有BMS tests `CS8600`与`CA2007`各1次。六个工程的97个本轮C#文件经默认targeted formatter与`--verify-no-changes`复验；`CheckDocumentation.ps1`和`git diff --check`通过。
- 四类独立终审均为GO、blocker/major **0/0**：public catalog/codec/三态authority；全production consumer/reachable bypass；revision participant/owner/concurrency；产品价值/dead foundation。

## 当前风险与未完成项

- `BmsBeatmapDecoderOptions.KeymodeOverride`只是host/importer correction seam；普通`ICustomBeatmapLoader`仍传`null`，没有终端用户纠正UI。证据不足的sparse`.bms/.bml`继续fail-closed，这是P1-K产品可用性缺口，不重开C3安全门，也不得描述成已交付用户功能。
- C5已闭合全部advertised slot的production host/capability决策；后续不得把C6 sandbox、最终整包reload、C7双包或人工视觉签收提前算入C5，也不得把mania的五个版本化NotApplicable写成普遍unsupported。
- scanner仍只在启动后对账一次，不是watcher；新增managed direct child需重启发现，已登记current managed/external只由Settings manual Reload进入新revision。
- C1 held-root/journal/recovery不是filesystem transaction；foreign addition/replacement可导致fail-closed冻结。current mutation仍须先fallback+detach，external永久source零写入。
- core Skin六项与mania full四项既有fixture失败必须按同名精确基线比较，不能隐藏新回归。
- BGA内容/timeline/seek/gimmick仍归P1-L；在线服务、sample pool、判定、binding与无关ruleset不属于P1-A C5。
- 程序化`OmsSkin`在C7 canonical包通过parity、完整性、原子恢复与实机gate前不得删除。
