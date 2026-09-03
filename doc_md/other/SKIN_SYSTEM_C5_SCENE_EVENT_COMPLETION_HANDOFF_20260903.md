# Skin V1 C5 scene/event 完成交接（2026-09-03）

> 本交接只记录 C5 已完成的真实代码、production caller、验证与终审。当前权威燃尽为 **`5/7 closed，C6 active`**；不是线性百分比。全局事实见 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)，执行门见 [P1-A PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md)，稳定合同见 [P1-A TECHNICAL_CONSTRAINTS](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)。

## 1. 退出结论

C5 已完成 versioned declarative scene、animation、只读 event runtime 与全部剩余 advertised public slot 的 production 接线。纵切从 ordinary `.osk`、managed `chartskin/<package>/`、registered external 的 fresh capture，经 `SkinManager` current revision、C4 codec/catalog/resolver/material 与 C3 exact layout，进入真实 BMS/mania/core producer、prepared graph、scene runtime、event stream 与 actual drawable/pool。没有测试 publisher 注入最终 scene snapshot，也没有把 schema、DTO、event cursor、单一 drawable 或 foundation-only work 算作产品能力。

C5 的 exact publication 是不可拆分的 **package + layout + material + scene** quadruple。所有可失败 manifest/scene parse、catalog/target validation、resource capture/decode、template expansion、graph build、pool/resource preparation 与 initial Snapshot 都止于 background prepare；update thread 只交换已经验证的 immutable 引用。任何失败保持 exact A；scene/slot 单点故障只隔离该 scene/slot，不影响判定、输入、对象、分数、layout、material、BGA 内容/timeline 或旧 publication。

## 2. 唯一合同与安全边界

- 固定文件名：`gameplay-skin.json`（manifest）、`gameplay-skin.scene.json`（scene）。合同：`oms-gameplay-skin-manifest.v1`、`oms-gameplay-skin-scene.v1`；只读事件 envelope：`oms-gameplay-skin-event.v1`。
- `GameplaySkinSceneCodec` 是唯一 scene/manifest tokenizer/codec。严格 UTF-8、单一 JSON root、BOM、duplicate/unknown field、unknown node/property/effect/event、非法 type/index/target/resource、canonical relative path、大小写/NFC collision 与 canonical encode/round-trip 均在 prepare 阶段验证，并输出稳定脱敏诊断。
- 路径只允许 captured package 内的 canonical 相对路径；绝对路径、盘符、UNC、`..`/父目录逃逸、reparse/symlink 逃逸、重复归一化目标和外部读取均拒绝。capture 使用 C1/C2 exact capsule；commit 后不得重新打开 manifest、scene、NativeStorage、filesystem、网络或 `SkinInfo`。
- decode 与 prepared graph 防御性 immutable，并保留 exact source/content/package/current/layout/material/scene contract identity。invalid 不当 absent，unsupported 不当 inherit；字段失败不会跨 revision 拼接部分旧 scene 与部分新 material。
- 允许节点：`Sprite`、`Container`、`Text`、`Mask`、`Clip`。允许 animation/state：frame、tween/track（Step/Linear/In/Out/InOut）、state machine、只读 property binding、template/instance。blend/effect 仅使用 allowlist preset；无任意 shader、反射、动态类型加载、脚本表达式、filesystem、network 或私有 C# effect authority。
- 固定 layer/z-order、anchor/origin、size/scale、clip/mask、DPI/safe-area、stage/group/lane/HUD/BGA target 与 layout-relative 坐标；geometry 只来自 C3 exact layout。scene 不能修改 input、判定、score/combo/gauge、object、gameplay clock、keysound 或 P1-L BGA 内容/timeline。

## 3. 只读 event stream

`GameplaySkinEventRuntimeHost` 是 engine-owned producer；`GameplaySkinEventStream` 只允许一个 producer，subscription queue 有界，`GameplaySkinEventStreamCursor` 只接受支持的 v1、当前 epoch 连续 sequence 与不回退的 revision/time。每个 envelope 至少含：contract/version、stream epoch、monotonic sequence、gameplay/layout/material/scene revision、authoritative gameplay time、LaneId/GroupId、event kind、immutable payload。

真实事件覆盖 gameplay lifecycle、layout/publication commit、input/key state、object spawn/despawn/state、judgement、score/combo/gauge、timing/beat/bar/BPM/stop/scroll 与 BGA viewport/content-state 只读摘要。初始 Snapshot 与 Reset 是完整状态，不是“先清空再补增量”；late attach、reload、retry、seek、rewind、publication 切换与旧 epoch 隔离均由确定顺序重建。engine envelope 可以发布 `GameplayResumed`；scene state-machine ABI 刻意不接受 `gameplay.resume`，因为完整 Snapshot 已能投影 Running 状态，避免建立第二状态 authority。

## 4. runtime capability 与 slot host

`oms-gameplay-skin-runtime-support.v1` 对每个 catalog slot 给出显式 Supported/NotApplicable/Unsupported 决策，和 author catalog、ruleset applicability、Suppress eligibility 分层。

| ruleset / 矩阵 | capability 事实 | production 证据 |
| --- | --- | --- |
| BMS 5K/7K/9K/14K、14K 双 deck、Scratch/Special | 28 项 catalog 均有 route；9K 的 Turntable/Laser 按 catalog applicability 不适用，因此矩阵为 26 个适用格 | `BmsAllKeymodeSceneProductionMatrixProductTest`、current-revision production **215/215**、BMS skin **146/146** |
| Mania single/dual | 28 项逐项决策；23 项 Supported，`object.mine`、`playfield.turntable`、`playfield.laser`、`bga.viewport`、`bga.frame` 是版本化 `NotApplicable` | `TestSceneManiaGameplaySkinLayoutProduction`、lane-cover/hit-explosion production tests、Mania `~GameplaySkin` **69/69** |

已接通的语义 host/scene route 包括 global/stage/group/lane roots、lane surface/divider、judgement line/hit target、lane cover fill/decoration、bar line、stage background/foreground、playfield backdrop/baseplate、key visual/key flash/mine、Note/LN/Hold head/body/tail、hit explosion、judgement/combo/gauge/text HUD、turntable/laser、BGA viewport/frame 与 decoration。适用性不会改变 C4 catalog 语义，五个 Mania `NotApplicable` 也不会静默变成 Inherit。

Note/LN/Hold、lane、HUD、effect 与 hit explosion 使用有界 pool；BMS static/固定 60 FPS 连续帧、mania Note/Hold 与现有程序化 fallback 兼容保持。Mirror/Random 只改变对象最终 LaneId，resource、keysound、scene、event 都随同 LaneId，固定 topology 不变。

## 5. publication、并发与资源寿命

- scene host 与 BMS/mania ruleset 共用 C2 participant/generation/work/publication lease、fresh attach barrier、commit 前 detach、late attach、跨 revision holder、latest-wins、cancel/reentrant、scheduler/dispatcher fault、shutdown、current mutation 与最后 lease exactly-once retire。
- prepare 前后和 commit admission 复核 participant generation、current selection、exact source/content/package/layout/material/scene revision、catalog/codec/resolver/manifest/scene/event contract version 及 cancel/shutdown token。prepared carrier 创建后取消、dispatcher 拒绝、commit guard 失败或日志故障不会留下 provisional lease/resource，也不会拆分 publication。
- publication 成功后只读 renderer 取得已提交 quadruple；old owner 必须等待 consumer/work/operation lease 全部 detach 后在 update thread exactly-once retire。BMS Note/LN borrow 在 material preparer → publication → scene carrier → layout owner 间只转移一次，失败路径逐一释放。
- scene/runtime 不执行 File/Directory/Storage/JSON decode/Resolve/Prepare；所有 I/O、图片解码、模板展开、graph build 与初始 Snapshot 构造均在 background prepare。event overflow/gap 走 deterministic Reset/Snapshot，不能静默丢事件。

## 6. 预算与确定性

prepare/runtime 冻结并检查：manifest/scene bytes、文件/资源数、单项及总 compressed/decompressed/decoded bytes、texture pixels、node/depth、template expansion、effect 层数/表面、track/keyframe、text/glyph/atlas、event subscription/queue/frame consumption、pool capacity/concurrent instances/per-frame create-recycle。超限或损坏只拒绝候选/熔断 scene/slot并保留 exact A；update thread 无 I/O、解码、模板展开、每帧字符串解析或无界分配。相同 input/revision 产生相同 resource selection、event order、animation sample、state transition 与 Snapshot/Reset。

## 7. 红测与验证矩阵

红测从真实 ordinary/managed/external source、`SkinManager` current revision、C4 codec/catalog/resolver/material 与真实 BMS/mania producer进入 actual renderer，覆盖：

- schema/version/unknown/duplicate/path escape/type/index/target/budget/canonical round-trip；allowlisted graph、clip/mask/blend/effect、template、animation、state machine、binding；任意 shader/script/reflection/filesystem/network 不可达；
- event epoch/sequence/time/revision/LaneId/GroupId、Snapshot/Reset、late attach、retry、seek、rewind、reload、旧 epoch 隔离；
- BMS/mania 全 public slot、5K/7K/9K/14K、14K 双 deck、Scratch/Special、mania single/dual、Mirror/Random 后 LaneId/resource/keysound/scene/event 一致；Note/LN/Hold、judgement/combo/gauge/HUD/key/effect/BGA frame/decoration 与 pool；
- 三源 same-ID A→B、latest-wins、失败保 exact A、dynamic attach/detach、跨 revision holder、last detach retire、cancel/reentrant/scheduler/dispatcher/shutdown/current mutation；beatmap-local public authoring 不可达；P1-K lane timeline/keysound、C3 geometry/aspect/DPI/safe-area、C4 digest/codec/三态/material 回归。

本次实际验证记录：

| 套件 | 结果 |
| --- | --- |
| core `FullyQualifiedName~GameplaySkin` | **429/429** |
| mania `FullyQualifiedName~GameplaySkin` | **69/69** |
| BMS `FullyQualifiedName~GameplaySkin` | **146/146** |
| BMS current-revision production | **215/215** |
| BMS full（`--blame-hang --blame-hang-timeout 5m`） | **1721/1721**，无 hang artifact |
| mania full | **860/864**；仅既有 `TestHoldNoteWithReleasePress`、`TestHoldNoteChord`、`TestSingleHoldNote`、`TestHoldNoteStair` frame-count 失败 |
| core `~Skin` | **1218/1224**；仅冻结六项既有失败 |
| core `~Skins` | **746/750**；仅冻结四项既有 archive 失败 |
| P1-K decoder/converter/timing/keysound | **126/126** |
| converted mania projection/store | **24/24** |
| BMS real keysound lifecycle | **17/17** |
| BMS shared store/DrawableRuleset | **68/68** |
| Windows Release | **0 error / 20 emitted known warnings** |

core `~Skin` 的六项失败为 `TestRetrieveAndLegacyExportJapaneseFilename`、`TestRetrieveAndNonLegacyExportJapaneseFilename`、`TestBackgroundCyclingOnDefaultSkin(True)`、`TestRetrievalWithConflictingFilenames`、`TestSampleUpdatedBeforePlaybackWhenNotPresent`、`TestRetrieveOggAudio`；与冻结基线名称/消息逐字符一致。mania full 四项同样逐字符保持冻结基线。Release 的 20 次已知 warning 是既有 MessagePack `NU1902` 18 次及 BMS tests 既有 `CS8600`、`CA2007` 各 1 次，未以 NoWarn 隐藏。

## 8. 四类独立终审记录

以下四项在本次闭门中按互不合并的检查清单分别完成；每项均记录 blocker/major **0/0**，结论 **GO**。

1. **schema / scene / event authority、安全与预算：GO（0/0）**。逐项检查 v1 contract、strict parser、path containment/reparse、unknown/duplicate/type/index/target、allowlist、prepared immutability、budget constants、runtime forbidden-operation scan；codec、scene runtime、event stream/cursor focused tests 全绿。确认 `gameplay.resume` 只在 engine envelope，scene ABI 不建立第二状态源。
2. **全部 production host / reachable bypass：GO（0/0）**。逐项核对 28 catalog ID、BMS profile、Mania 28-entry decision（23 Supported/5 NotApplicable：`object.mine`、`playfield.turntable`、`playfield.laser`、`bga.viewport`、`bga.frame`）、BMS all-keymode/double-deck/scratch matrix、Mania single/dual、HUD/BGA/effect/pool host；从三源 SkinManager current revision追到 actual drawable，未发现测试 publisher、renderer 私有 lookup、第二 resolver 或未登记 public slot。
3. **publication / event epoch / owner / concurrency：GO（0/0）**。分别复核 exact quadruple、participant generation、fresh attach、latest-wins、cancel/reentrant/scheduler/dispatcher/shutdown、Snapshot/Reset、gap/overflow、late attach、rewind/old epoch 与 borrow/lease exactly-once retire；失败保 A，未发现跨 revision 拼件或 provisional 泄漏。
4. **产品价值 / 性能 / dead foundation：GO（0/0）**。核对 BMS/mania真实玩家结果、dense 14K/双 deck/持续 LN/judgement-HUD-event压力、pool/预算/确定性、P1-K keysound、C3 geometry、C4 material、旧 helper/factory 分类；已接 production 的 event cursor/scene factory/pool/timing projection与 host均有caller，旧逐件 provider/factory已删除或只作 compatibility seam，C6/C7范围未混入。

## 9. 明确不属于 C5

- 不实现 script VM/sandbox、权限协商、作者代码执行、最终 ini/manifest/scene/script/素材整包 reload gate；这些属于 C6。
- 不实现 C7 canonical `oms-simple.osk`/`oms-complex.osk`、Authoring Kit、validator、只读恢复、程序化 `OmsSkin` 删除或自动发行。
- 不创建 P1-L BGA 内容/timeline/seek/gimmick authority；不新增在线服务、watcher/live reload、sample pool、判定/input/clock/binding authority或无关 ruleset。
- 不新增 beatmap-local public authoring；`WorkingBeatmap.Skin` 仍只读 legacy direct visual compatibility。
- `GameplaySkinCapabilityNegotiator`/authorization 继续留 C6；C5 的 runtime profile 是显式 Supported/NotApplicable 决策，不是通用协商器。

## 10. 文档与提交

本次同步：P1-A `DEVELOPMENT_STATUS.md`、`DEVELOPMENT_PLAN.md`、`TECHNICAL_CONSTRAINTS.md`、`CHANGELOG.md`；mainline 状态/计划/日志一句摘要；`SKINNING.md`、`GAMEPLAY_SKIN_PUBLIC_CATALOG_V1.md`；以及 `.Codex/memory` 中 product progress、codec/material、layout snapshot、slot contract、config presence、lane-resource compatibility、atomic reload/detach、BMS authoring、event envelope 与 `MEMORY.md`。随后运行 `CheckDocumentation.ps1`、`git diff --check`、owning-project targeted formatter `--verify-no-changes`，并在当前工作树创建唯一有意义提交；不 push，push 前另行取得用户确认。

本交接与 C5 实现、测试和权威文档同处当前工作树的唯一有意义提交；提交对象不能在自身内容中稳定自指，最终 hash 以仓库 `git log` 与交付回复为准。未取得用户确认前不执行 `git push`。
