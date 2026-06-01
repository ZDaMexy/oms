# P1-K 变更日志：BMS 解析链路治理

> 本文件记录 `P1-K` 相关的验证通过变更，按时间倒序排列。
> 当前进度见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，执行规划见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

---

## 2026-06-01

### 代码 / 测试：修复 mania autoplay 整类长条不按（`canParticipateInAutoplay` 越界跳过 HoldNote；K9 #12 实现订正）

审查 mania autoplay mod 链路（`ManiaModAutoplay` → `ManiaAutoGenerator`）时查实一处回归：**autoplay 完全不处理长条**，原生 mania 谱与 `BMS -> mania` 转谱同坏（转谱长条经 `BmsToManiaBeatmapConverter` 即落成 mania `HoldNote`，两者共用同一 `ManiaAutoGenerator`）。

- **根因**：K9 #12 的「autoplay 跳过 ignore-only sample 对象」契约在 [../../osu.Game.Rulesets.Mania/Replays/ManiaAutoGenerator.cs](../../osu.Game.Rulesets.Mania/Replays/ManiaAutoGenerator.cs) 里实现为 `canParticipateInAutoplay(o) => o.Judgement.MaxResult.AffectsCombo()`（随 `4aa76f0` "P1-L Phase 2 accumulated WIP snapshot" 落入）。但 mania `HoldNote.CreateJudgement()` 返回 `IgnoreJudgement`（`MaxResult = IgnoreHit`，`AffectsCombo()` 为 **false**）——长条自身不计 combo，可玩性在其嵌套 `HeadNote`/`TailNote`（`ManiaJudgement`）。该谓词遍历的是**顶层** `Beatmap.HitObjects` 且**不**下探嵌套，于是 `generateActionPoints` 与同列 `GetNextObject` 两侧都把每条 `HoldNote` 整体 `continue` 掉 → autoplay 不生成任何按/放帧 → 头尾全 miss、combo 不涨。
- **与 note-lock 的不对称**：并列的 `OrderedHitPolicy.canParticipateInLocking` 同样按 `AffectsCombo` 过滤，却**正确**——它额外遍历 `obj.NestedHitObjects` 并逐个判定，长条经嵌套 Head/Tail 仍参与 note-lock。autoplay 生成器缺这层嵌套回退，这是本回归的不对称点。
- **为何漏网（两道闸都放过）**：K9 收口（2026-05-29）的 mania 验证只跑了窄 filter（`BmsToManiaBeatmapConverterTest` + ignore-only drawable + `AutoplayIgnoresSampleOnlyScratchObjects`，**未含**长条用例）；而**上游既有**的 `TestPerfectScoreOnShortHoldNote`（专测长条 autoplay，期望 combo 4）其实已在失败，却被当日 STATUS 误记为「CLI 下 visual scene flake / TearDownSteps 10s 超时」（见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md) 2026-05-29 条，已于本日订正）；随后 `4aa76f0` 快照又只验 BMS 套件（860/860）。本次实测基线确证其为真失败：combo 始终为 0、PassCondition 不满足、重试至 gameplay 时钟 228s 超时——正是「长条被整体跳过」的表现，非 flake。
- **修复**：`canParticipateInAutoplay` 改为 nested-aware —— `o.Judgement.MaxResult.AffectsCombo() || o.NestedHitObjects.Any(n => n.Judgement.MaxResult.AffectsCombo())`，对齐 `OrderedHitPolicy` 的「self 或任一嵌套对象影响 combo 即参与」语义。长条经嵌套 Head/Tail 复活；BGM/scratch sample-only 对象（`IgnoreHit` 且**无任何嵌套对象**）继续被两侧跳过，K9 #6 / #12 与 K11 的 sample-only 契约不动。同步订正 [../../osu.Game.Rulesets.Bms/Objects/BmsConvertedScratchSampleHitObject.cs](../../osu.Game.Rulesets.Bms/Objects/BmsConvertedScratchSampleHitObject.cs) 与 [../../osu.Game.Rulesets.Bms/Objects/BmsConvertedBgmSampleHitObject.cs](../../osu.Game.Rulesets.Bms/Objects/BmsConvertedBgmSampleHitObject.cs) 的契约注释（旧措辞"凡 MaxResult 不影响 combo 即跳过"对 HoldNote 失真）。
- **测试**：[../../osu.Game.Rulesets.Mania.Tests/Mods/TestSceneManiaModAutoplay.cs](../../osu.Game.Rulesets.Mania.Tests/Mods/TestSceneManiaModAutoplay.cs) 新增 `TestAutoplayHoldsLongNoteAlongsideSampleOnlyObject`（1000ms 长条与 BGM sample-only 共存 → combo 2：长条完整按住、sample 跳过），顺带补上 `BmsConvertedBgmSampleHitObject` 的 autoplay 覆盖。修复后 `TestSceneManiaModAutoplay` **4/4**（修复前 1 失败）。
- **验证**：基线（修复前）`TestPerfectScoreOnShortHoldNote` 实测失败；修复后 `TestSceneManiaModAutoplay` **4/4** 全绿；`dotnet build osu.Desktop.slnf -c Release` **0 错误**、生产代码 0 新增警告（仅 2 条预存于未改动测试文件的 CS8600/CA2007）。**2026-06-01 用户人工实机验证确认**：mania 原生谱与 `BMS -> mania` 转谱在 mania mode 下的长条 autoplay 表现均已正常。BMS-native autoplay（`BmsAutoGenerator`，按 `OfType<BmsHitObject>()` 过滤、自带 `BmsHoldNote` 释放分支、无 `AffectsCombo` 谓词）不受影响。

### 代码 / 测试：BMS -> mania 转谱 BGM/autoplay 音频补全 + LN 尾键音对齐落地（K11）

审查 `BMS -> mania` 单向转谱的音频链路（承接 K9 转谱器）后，补全一处音频保真缺口并落地：

- **缺口（已查实）**：[../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaBeatmapConverter.cs) 的 `ConvertHitObject` switch 只覆盖 `BmsHoldNote` / `BmsHitObject`（含 scratch），**无 `BmsBgmEvent` 分支**。BGM（autoplay channel `0x01`）在 `BmsBeatmapConverter` 期已落成 `BmsBgmEvent` 并进入 `BmsBeatmap.HitObjects`，但 base `BeatmapConverter.convertHitObjects` 把非 `ManiaHitObject` 的它丢给 `ConvertHitObject` 后得空枚举 → **整层 BGM 被静默丢弃**。对纯键音 BMS（`detectFullMusicFile` 未命中、`AudioFile` 仅 preview/空），mania 转谱因此丢掉歌曲主体（鼓/贝斯/铺底/人声等非可击打层），听感为"空壳"。
- **暴露面（真实可玩路径）**：`BeatmapInfoExtensions.AllowGameplayWithRuleset`（mania ruleset + `ShowConvertedBeatmaps`）允许 BMS 作为 mania 游玩，`DrawableManiaRuleset.CreateDrawableRepresentation` 会为 BMS-assembly 对象建 drawable 并播 sample-only 对象 → 缺口在实际 mania 游玩时发生，非仅 star 计算用途。
- **已 de-risk 的前提**：样本解析与游玩 ruleset 无关——[../../osu.Game/Beatmaps/WorkingBeatmapCache.cs](../../osu.Game/Beatmaps/WorkingBeatmapCache.cs) 的 `createResourceProvider` 按 `BeatmapSet.FilesystemStoragePath` 建 `FilesystemBackedBeatmapResourceProvider`，BMS 与 mania 模式共享同一指向 `chartbms/` 的 WAV 源；note keysound 已能在 mania 出声，BGM 用同型 `BmsKeysoundSampleInfo` piggyback，无需新建样本源。
- **连带（次要发现）**：转谱器把 LN 尾 keysound 放进 `NodeSamples[1]`，mania `TailNote` release 时会播放它，与 BMS 侧「LN tail 一律不发声」（P1-J 约束 3a）冲突；对 LNTYPE1 尾复用头 WAV 的谱会复现已在 BMS 侧修掉的 double。

**实现**：

1. 新增 sample-only [../../osu.Game.Rulesets.Bms/Objects/BmsConvertedBgmSampleHitObject.cs](../../osu.Game.Rulesets.Bms/Objects/BmsConvertedBgmSampleHitObject.cs)（`IgnoreJudgement` + `HitWindows.Empty`，与 scratch sample-only 同族）与其 drawable [../../osu.Game.Rulesets.Bms/UI/DrawableBmsConvertedBgmSampleHitObject.cs](../../osu.Game.Rulesets.Bms/UI/DrawableBmsConvertedBgmSampleHitObject.cs)（Alpha=0、`timeOffset>=0` 时 `PlaySamples()` + `ApplyMinResult()`）。
2. `BmsToManiaBeatmapConverter.ConvertHitObject` 加 `case BmsBgmEvent`：发 BGM sample-only 对象（column 0、空 sample 跳过）；`isScorableHitObject` 一并排除新类型，BGM 不进 `TotalObjectCount` / `EndTimeObjectCount` / 星数输入。
3. drawable 工厂 `BmsToManiaDrawableRepresentationFactory` 改 switch，支持 BGM 类型。
4. **LN 尾键音 mania 对齐（两条路径）**：(a) 非 scratch 长条——`BmsHoldNote` 分支把 `NodeSamples[1]` 改空列表（mania `TailNote` release 会播末位 node sample，此前携带尾 keysound 会对 LNTYPE1 尾复用头 WAV 的谱复现 double）；(b) scratch 长条——`createScratchSampleHitObjects` 此前还会为尾单独发一个 sample-only 对象，**经人工实测（GOODBOUNCE 人声长条在 scratch 轨）首轮只改 (a) 时仍 double**，现已只发 head sample。两者均对齐 BMS 侧 P1-J #3a「长条只头发声」，头 keysound 仍走 `NodeSamples[0]` / head sample 对象。

**测试**：`BmsToManiaBeatmapConverterTest` 新增 `TestBgmEventsBecomeSampleOnlyObjectsWithoutAffectingScorableCounts`（channel 01 叠层 → 2 个 sample-only 对象、column 0、不进 `TotalObjectCount`）与 `TestConvertedHoldNoteKeepsHeadKeysoundButSilencesTailToMatchBmsContract`（`NodeSamples[0]`=头、`[1]` 空），并把既有 `TestSevenKeyScratchHold...` 改名为 `...KeepsHeadSampleButSilencesTail...`、断言 scratch 长条尾 sample 不再发出，由 17 扩到 **19/19**。

**验证**：`dotnet build osu.Desktop.slnf -c Release` **0 错误**、生产代码 0 新增警告（仅 2 条预存于未改动测试文件的 CS8600/CA2007）；`BmsToManiaBeatmapConverterTest` **19/19**；完整 `osu.Game.Rulesets.Bms.Tests` **869/869**（Release）无回归；**2026-06-01 用户人工实测确认**：GOODBOUNCE [A] scratch 人声长条 double 消失、BGM 出声与 BMS 原生一致、普通 mania 无回归。

**后置**：BGM 现已能在 mania 转谱中发声（drawable `PlaySamples`），但 **dense 键音谱（数千 BGM 事件）的播放期性能**（mania 侧走非池化每对象 `SkinnableSound`、无 `BmsKeysoundStore` 等价设施）与 player-level BGM 出声 / seek proof 归 [P1-J J6](../P1-J/CHANGELOG.md)，待真实 dense 谱实测。约束见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md) K11 节、[DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) K11 节。

## 2026-05-31

### 修复：缺省 `#LNTYPE` 时长条被整条丢弃（默认应为 1）

- 现象（用户实测定位）：`GOODBOUNCE [A]`（`_goodbounce(SPA).bms`）等少数差分出现"少键"，且一段人声 keysound "stomp your feet" 念到结尾突然截断。最初怀疑音频/通道池，最终查实是**解码器层**：该谱用 5X/6X 长条通道但**省略 `#LNTYPE`**，而 OMS 的 `BmsBeatmapInfo.LongNoteType` 为 `int?` 缺省 `null`，`handleLongNoteChannelEvent` 只认 `case 1/2`，`null` 直接忽略并仅告警 → **全谱 31 条长条（含 key 与 scratch）被丢弃**。
- 关键耦合：该曲人声被拆成 `voice1 (1)..(4)`（对象 `7E/7F/7G/7H`），收尾段 `voice1 (4)=7H` 恰好放在 **scratch 长条**（`#00856:...7H00007H...`）上；长条被丢 → "feet" 永不发声 → 听感即"念到 f 截断"。这与 [BMS_FORMAT_REFERENCE §5](../../other/BMS_FORMAT_REFERENCE.md) 写明的「`#LNTYPE 1`（RDM 记法，默认）」直接冲突——是代码没实现该默认。
- 修复：`BmsBeatmapDecoder.handleLongNoteChannelEvent` 改用 `LongNoteType ?? 1`，缺省按规范当作 type 1（保留 `LongNoteType` 为 `null` 以记录"未声明"，默认只在消费处生效）。显式 `#LNTYPE 1/2` 不变，显式非法值仍告警。
- 测试：新增 `BmsBeatmapDecoderTest.TestDefaultsToLnType1WhenLnTypeHeaderOmitted`（同时含 key 长条 51 与 scratch 长条 56、无 `#LNTYPE` → 解析出 2 条 LnType1，且其一 LaneChannel=0x16 scratch）。LN 相关 focused **43/43**、完整 `osu.Game.Rulesets.Bms.Tests` **864/864**（Debug）通过。
- 连带项（已在 P1-J 2026-05-31 闭合）：本次修复后用户实测出现 "stomp your fee feet"——LNTYPE1 长条尾对象重复头 WAV，OMS 此前会播尾 keysound 与头叠 double。已让 `DrawableBmsHoldNoteTail.PlaySamples()` 静音（对齐 LR2/beatoraja「长条只头发声」），详见 [P1-J CHANGELOG](../P1-J/CHANGELOG.md) 2026-05-31。

## 2026-05-29

### 解析 → 谱面/音乐/键音呈现链路审查后的全量修复（含性能优化）

对 `BmsBeatmapDecoder → BmsDecodedChart/BmsBeatmapInfo → BmsBeatmapConverter → DrawableBmsRuleset/BmsPlayfield/BmsLane → BmsKeysoundStore` 全链做了一轮审查（对照 [../../other/BMS_FORMAT_REFERENCE.md](../../other/BMS_FORMAT_REFERENCE.md)），把暴露出的链路正确性/保真 bug 与两处性能问题一次性修完；纯属功能新增的项（负 BPM 反向滚动、独立 10K 键位、地雷可玩化、`#SPEED`/文本/`#CHANGEOPTION` 建模）显式后置为 backlog。

**1. BGM（channel `01`）叠层不再被同位去重（高优）**

[../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs) 的 `compoundDuplicateChannelEvents` 此前对所有 channel 一视同仁做"同 measure/fraction/channel 取最后一个"去重，把 BGM channel `0x01` 也卷入——同一时刻的多条 `#xxx01` 并行 keysound 层只剩最后一个，违反规范"channel 01 不合并"。`isDuplicateChannelCollision` 现对 `0x01` 返回 false（BGM 永不复合），同位 BGM 全部进入 `ObjectEvents` → 各生成一个 `BmsBgmEvent`。新增 `TestBgmChannelKeepsSimultaneousLayers`。

**2. LNOBJ 长条头移除由 O(n²) 改为 O(n)（性能）**

LNOBJ 尾对象消费头时，旧实现对 `ObjectEvents` 做反向线性扫描删除（每条 LNOBJ 长条一次，全 LNOBJ 谱退化为 O(n²)）。改为：`pendingLnObjHeads` 记录头在 `ObjectEvents` 中的索引，消费时只把索引加入 `consumedLnObjHeadIndices`，解析末尾 `removeConsumedLnObjHeads` 一次性过滤重建。新增 `TestPairsMultipleLnObjLongNotesAcrossLanesWithBgm` 锁住"多轨 LNOBJ + BGM 交错"下的正确移除。

**3. 控制流：`#RANDOM` 增量支持 `#ELSE`/`#ELSEIF`/`#SETRANDOM`，并新增 `#SWITCH` 家族（修复错谱）**

预处理器重写为递归下降：保持 `#RANDOM` 默认只执行 `#IF 1`（含原告警文案与"无 #IF 1"告警，两条锁定测试不变）；新增 (a) `#ELSE`/`#ELSEIF` 链式求值——修复 `#IF 1` 命中时 `#ELSE` 内容被错误并入；(b) `#SETRANDOM n` 按作者固定值选支、无告警；(c) `#SWITCH`/`#SETSWITCH`/`#CASE`/`#SKIP`/`#DEF`/`#ENDSW` 的确定性单段选择（默认 `#CASE 1`，C 风格 fall-through 到 `#SKIP`，无匹配走 `#DEF`，支持嵌套）——此前 `#SWITCH` 多 case 被无条件全收、叠成错谱。新增 5 条测试（`#ELSE` 命中/跳过、`#SETRANDOM`、`#SWITCH`、`#SETSWITCH`+`#DEF`）。

**4. 亚 1 BPM 不再被钳为 1（保真）**

[../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs) 的 `getBeatLength` 此前 `Math.Max(1, Abs(bpm))` 把 `0<bpm<1` 错钳为 1 BPM（规范允许低至 ~0.014）。改用 `minimum_effective_bpm = 1e-3` 作为有限下界。时间轴推进直接消费 `getBeatLength`，故对象时间已修正；`TimingControlPoint.BeatLength` 仍受框架 bindable `[6,60000]` 钳制（仅影响显示滚动，非对象时序）。新增 `TestSubUnitBpmUsesMagnitudeWithoutClampingToOne`。

**5. 轨键音时间线：消费不可见对象、修复开局静音、改为基于谱面、O(log n)（保真 + 性能）**

此前 `BmsLane` 的空键/误击 keysound 取"最近一次已判定音符"的音（`onNewResult` 更新），开局首音符前为 null（静音），且**不可见对象（channel 31-49）解码后从未被消费**。改为：`BmsBeatmapConverter` 在转换期构建 per-lane 时间线（可见音符 + LN 头/尾 + 不可见对象，按时间排序，不可见对象先注册进 `eventTimes` 再按 `channel-0x20` 映射回 lane），存入 `BmsBeatmap.LaneKeysoundTimelines`；`BmsPlayfield.createLane` 把对应 lane 的时间线注入 `BmsLane.SetKeysoundTimeline`；`BmsLane.playCurrentLaneKeysound` 改用二分查找解析"当前时刻 at-or-before 已 arm 的键音"，开局前回退到首条目（不再静音）。移除了基于 `NewResult` 判定的旧更新路径。该改动不改变 `OnPressed` 的调用模式（命中由音符 drawable 消费、空键才落到 lane），故无重复播放。新增 `TestBuildsLaneKeysoundTimelineIncludingInvisibleObjects`。新增 typed 载体 [../../osu.Game.Rulesets.Bms/Audio/BmsLaneKeysoundEntry.cs](../../osu.Game.Rulesets.Bms/Audio/BmsLaneKeysoundEntry.cs)。

**未改动的设计契约（审查确认、显式保留）**

- 同拍位音符落在 STOP **之后**（T+D）：由 `TestSamePositionBpmAndStopApplyBeforeObjectTime` 锁定、K3-A 约束明文背书、且与 K9 #16 的 STOP-freeze 剥离耦合；同位事件整体平移 D 仍自洽，不按单方解读擅改。
- BGM/键音播放路由（`DrawableBmsHitObject.PlaySamples → BmsKeysoundStore` 共享池）、长条头/尾键音拆分、编码探测（strict-UTF8→Ude→Shift_JIS）、channel `02/03/08/09` 编码区分、STOP 单位（1/192 小节）、复合规则（`00` 不覆盖 + source-line 决胜）均经审查确认正确，未改。

**验证**：`BmsBeatmapDecoderTest` **40/40**（33→40，+7）、`BmsBeatmapConverterTest` **15/15**（13→15，+2）；`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal` **830/830**（821→830）；`dotnet build osu.Desktop.slnf -p:Configuration=Release` **0 错误**、生产代码 0 新增警告（仅 2 条预存于未改动测试文件的 CS8600/CA2007）。

### 沉淀 BMS / bmson 格式权威参考（解析审查对照基线）

为后续 BMS 解析链路审查提供权威外部参考，把五个规范源的关键信息收敛成一份 in-repo 文档 [../../other/BMS_FORMAT_REFERENCE.md](../../other/BMS_FORMAT_REFERENCE.md)：

- **来源与分级**：[hitkey command memo](https://hitkey.bms.ms/cmds.htm)（首选事实标准，记录 LR2/nanasi/ruvit/pomu2/beatoraja/bemaniaDX 跨实现差异）+ [SaxxonPike 镜像](https://github.com/SaxxonPike/bms-command-memo) + [bemusic/bmspec](https://github.com/bemusic/bmspec)（可执行 Gherkin 回归基线）+ [BM98 1998 原始规范](http://bm98.yaneu.com/bm98/bmsformat.html)（历史起点）+ [bmson spec](https://bmson-spec.readthedocs.io/)（JSON 旁系）。
- **覆盖面**：文件/行结构与编码、完整 channel 表（重点标注 `02` 浮点 / `03` 十六进制字面量 / `08·09·SC·SP` base36 索引这组"非 base36 对象"编码陷阱）、键位映射（`16/26`=scratch、`18/19`=KEY6/7、PMS 多约定澄清）、时序语义（signed/扩展 BPM、STOP=1/192 小节、SCROLL/SPEED、同拍位 `BPM→STOP→object` 顺序）、长条三套记法（`#LNTYPE 1/2`、`#LNOBJ`、`#LNMODE` CN/HCN）、同小节复合/覆盖规则（含 hitkey 原例逐槽核对）、`#RANDOM/#SWITCH` 控制流、header 速查表，并以一份对照 `BmsBeatmapDecoder/BmsDecodedChart/BmsBeatmapInfo/BmsBeatmapConverter` 的 14 项**解析审查清单**收口。
- **URL 修正**：hitkey 旧 dyndns 域名（`hitkey.nekokan.dyndns.info`）已失效，本子线 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md) 与 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) 引用统一改指新权威域名 `hitkey.bms.ms`，并补指向上述 in-repo 参考文档。
- **联动**：参考文档已索引进 [../../other/README.md](../../other/README.md)。本条为参考材料沉淀与文档联动，无生产代码改动、无 build/test 变化；若其中某条结论升级为硬约束或正式回归门槛，再回写本子线四件套与必要的 mainline。

### K9/K10 邻接审查后的全量收口（11 项发现一次性落地 + K9 #15/#16 约束补齐）

针对 BMS→mania 转谱链路的全量审查暴露出 1 项 K9 硬约束未实施、1 项哨兵值碰撞、3 项设计纪律偏差与 6 项隐患/nit。本轮把全部 11 项一次性修完，并补齐缺失的 K9 #15（mania 转谱期不传递 BMS scroll EffectControlPoint）与 K9 #16（STOP-freeze 必须用 dedicated subclass 而非 sentinel BeatLength）两条硬约束。

**1. K9 #9 实施：空可游玩结果抛 `BeatmapInvalidForRulesetException`**

[../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaBeatmapConverter.cs) 此前 `CanConvert() => totalColumns > 0` 只检查 keymode 层面的列数，纯 scratch 谱、空谱在 flatten 后 `scorableHitObjects.Count == 0` 仍被产为合法 mania 谱并被持久化为 0 星，违反 K9 #9。本轮在 `ConvertBeatmap` 算完 `scorableHitObjects` 后显式判空并抛 `BeatmapInvalidForRulesetException`。[BeatmapDifficultyCache.computeDifficulty](../../osu.Game/Beatmaps/BeatmapDifficultyCache.cs) 与 [BackgroundDataStoreProcessor.populateMissingConvertedStarRatings](../../osu.Game/Database/BackgroundDataStoreProcessor.cs) 已有的 `catch (BeatmapInvalidForRulesetException)` 分支会把它固化为 Failed，与 K10 的失败语义自然衔接，不会被反复重试。新增 focused 回归 `TestScratchOnlyChartIsRejectedAsInvalidForRuleset` 与 `TestEmptyChartIsRejectedAsInvalidForRuleset` 锁住这条边界。

**2. 引入 `BmsStopFreezeTimingControlPoint` 替代魔数哨兵（K9 #16 新约束）**

[../../osu.Game/Beatmaps/ControlPoints/TimingControlPoint.cs](../../osu.Game/Beatmaps/ControlPoints/TimingControlPoint.cs) 的 `BeatLengthBindable` 强制 `MinValue = 6`（对应 BPM = 10000），意味着任何合法 BMS BPM ≥ 10000 写入 mania 都会落在 `BeatLength = 6`，与原 `BmsBeatmapConverter.StopFreezeBeatLength = 6` 沿用的 sentinel 值完全碰撞。[BmsToManiaBeatmapConverter.isBmsStopFreezeTimingPoint](../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaBeatmapConverter.cs) 此前以 ε=1e-4 比较 BeatLength 剥离，会误删极端 BPM 谱的真实 timing 点。本轮新增 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsStopFreezeTimingControlPoint.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsStopFreezeTimingControlPoint.cs) 作为 dedicated `TimingControlPoint` 子类——`BmsBeatmapConverter` 写入 STOP 期改用 `new BmsStopFreezeTimingControlPoint { BeatLength = stop_freeze_beat_length }`（值仍为 6，让 BMS-side playable 渲染为硬冻结），`BmsToManiaBeatmapConverter` 改按 `timingPoint is BmsStopFreezeTimingControlPoint` 类型剥离。两条新 focused 回归 `TestExtremeBpmIsPreservedInManiaTimingPointsAndNotMistakenForStopFreezeSentinel` 与 `TestStopFreezeIsStrippedFromManiaWhileExtremeBpmTimingSurvives` 锁住 BPM 10000 真实 timing 点的存活，并验证 STOP 期 freeze 被正确剥离。

**3. K9 #15 新约束：mania 转谱期显式声明不传递 BMS scroll EffectControlPoint**

[BmsBeatmapConverter](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs) 通过 `addEffectControlPoint` 把 `SCROLLxx/SC` 写入 `ControlPointInfo.EffectPoints`（K3-D 已建立的 BMS-side scroll-speed consumer contract），但 [BmsToManiaBeatmapConverter.createConvertedControlPointInfo](../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaBeatmapConverter.cs) 只迭代 `TimingPoints`，EffectPoints 在 mania 转谱后被静默丢弃；既有测试 `TestConvertedBeatmapSanitisesBmsOnlyControlPoints` 也已 assert `EffectPoints, Is.Empty`，说明这是 K9 的设计选择（BMS scroll 是引擎专属视觉语义，不映射 mania SV）但此前从未在约束里写明。本轮在 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md) K9 节追加第 15 条显式声明，把"silently 丢"升格为合同；若未来需要 BMS scroll→mania SV 映射须另起切片并显式更新本条。

**4. `BmsToManiaBeatmapConverter.createSourceBeatmap` 复用 K5 cache**

原实现每次都 `new BmsRuleset().CreateBeatmapConverter(sourceBeatmap).Convert(...)`，绕开 [BmsDecodedBeatmap](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedBeatmap.cs) 实现的 `ICachedModlessPlayableBeatmapSource` cache。同会话内先 BMS 再 mania 的场景会让 `BmsBeatmapConverter` 跑两次，违反 K5 spirit "无 mods 的 BMS playable projection 现按 source beatmap identity 只 materialize 一次"。本轮先查 `sourceBeatmap.TryGetCachedModlessPlayableBeatmap(bmsRuleset.RulesetInfo, out _)` 命中即返；未命中则转换后 `CacheModlessPlayableBeatmap` 回写。同时把 `new BmsRuleset()` 提为 `static readonly` 共享实例，避免每次重建 input bindings / mod state。新增 focused 回归 `TestRepeatedConversionReusesCachedBmsPlayableSource` 验证第二次 mania 转谱命中 cache 且返回同一引用。`TestConversionIgnoresMutatedSourceWrapperHitObjects` 不受影响——cache 存的是 converter 从 `DecodedChart` 推导的 BmsBeatmap，与 source wrapper 的 `HitObjects` 被外部 mutate 无关。

**5. `ManiaRuleset.tryCreateBmsConverter` 反射缓存对齐 `DrawableManiaRuleset`**

[ManiaRuleset.cs](../../osu.Game.Rulesets.Mania/ManiaRuleset.cs) 此前每次 `CreateBeatmapConverter` 都 `Type.GetType` + 两次 `GetMethod` + 两次 `Invoke`。对 58k+ BMS 谱库的 carousel filter 反复 query 是不必要的反射开销。本轮把 BMS factory `CanCreate` / `Create` 反射结果固化为 `static readonly Func<IBeatmap, bool>?` 与 `static readonly Func<IBeatmap, Ruleset, IBeatmapConverter>?` 委托（用 `Delegate.CreateDelegate`），完全对齐同文件 [DrawableManiaRuleset](../../osu.Game.Rulesets.Mania/UI/DrawableManiaRuleset.cs) 已在用的 drawable-factory 缓存模式。

**6. `createDifficultyBeatmap` metadata 防护 + scratch sample 死代码清理**

[BmsToManiaBeatmapConverter.createDifficultyBeatmap](../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaBeatmapConverter.cs) 原把 `convertedBeatmap.Metadata` 引用直接传给 difficulty beatmap 的 `BeatmapInfo` 构造，若未来 difficulty/performance/score 计算器决定写 metadata 会反向污染 converted beatmap。改用 `convertedBeatmap.Metadata.DeepClone()`。`initialiseLaneColumnMaps` 原对所有 lane 都填 `scratchSampleColumnsByLane`，非 scratch 项永远不会被 `getScratchSampleColumn` 读到——改为只对 scratch lane 赋值，意图也更清晰。

**7. `BmsPersistedMetadataResolver.parsedDataCache` evict-on-write + K10-B 推迟决定加注释**

[BmsPersistedMetadataResolver.cs](../../osu.Game/Beatmaps/BmsPersistedMetadataResolver.cs) 的按 JSON 字符串 key 的 cache 此前承认"stale entries simply leak"。本轮在 `setConvertedStarRating` 写入新 payload 后主动 `parsedDataCache.TryRemove(previousJson, out _)`，让每张谱的 cache 占用上界归一到 1 条；historical payload 写完即清，避免 mutation cycle 导致无界增长。同时在 `tryGetCurrentConvertedStarRating` 的 `LastAppliedDifficultyVersion` 比较处加注释指向 K10-B 的"实测确认 LAV 在单 RulesetStore 实例下稳态正确，故推迟改造权威版本读取"决定（详见 K10 节约束 #5-6 与 DEVELOPMENT_PLAN K10-B）。

**8. `BeatmapDifficultyCache.persist* IsManaged` 守护 + XML 合同声明**

[BeatmapDifficultyCache.persistConvertedStarRatingIfApplicable](../../osu.Game/Beatmaps/BeatmapDifficultyCache.cs) 与 `persistConvertedStarRatingFailureIfApplicable` 此前先无差别地 `if (beatmapInfo.Metadata is BeatmapMetadata m) Set(m, ...)` 再开 realm.Write。若调用方传入的是 live realm 实例（managed metadata）而当前线程没有 realm write 上下文，第一次 Set 会抛 RealmException。本轮加 `!metadata.IsManaged` 守护——只对 detached metadata 写 in-memory，managed metadata 一律通过 realm.Write 落盘——并在两个方法上补 XML 注释，把"detached metadata 是 in-memory bindable 更新，managed 路径必须经 realm.Write"的隐式合同写明。

**9. `BmsConvertedScratchSampleHitObject` 合同注释收口**

[BmsConvertedScratchSampleHitObject.cs](../../osu.Game.Rulesets.Bms/Objects/BmsConvertedScratchSampleHitObject.cs) 通过 `IgnoreJudgement` + `HitWindows.Empty` 让 [ManiaAutoGenerator](../../osu.Game.Rulesets.Mania/Replays/ManiaAutoGenerator.cs) 与 [OrderedHitPolicy](../../osu.Game.Rulesets.Mania/UI/OrderedHitPolicy.cs) 各自的 `AffectsCombo()` 过滤自动跳过它，但此前没有任何文档/注释指明这条跨模块的依赖。本轮在类上补完整 XML 注释，把"依赖 IgnoreJudgement.MaxResult.AffectsCombo() == false 串联起 autoplay 与 note-lock"的合同写明，并指向现有 focused 测试作为 regression guard，避免未来引入新的 ignore-only mania 变体时悄无声息地回归。

**约束补齐**

[TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md) K9 节新增第 15、16 条（mania 转谱不传 EffectControlPoint；STOP-freeze 必须 dedicated subclass）；本文件涉及的所有代码改动均锚定到这两条新约束 + 已存在的 K9 #9 / K5 / K10 contract 上，文档与代码一次同步。

**测试 / 构建**

- `dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m`：0 错误 0 警告。
- `dotnet test osu.Game.Rulesets.Bms.Tests --no-build -c Release -v minimal`：**821/821** 通过。
- `dotnet test osu.Game.Rulesets.Mania.Tests --filter BmsToManiaBeatmapConverterTest`：**17/17** 通过（12 原有 + 5 本轮新增）。
- `dotnet test osu.Game.Tests --filter "BmsStarRatingResolverTest|BeatmapCarouselFilterSortingTest|BeatmapCarouselFilterMatchingTest|BeatmapCarouselFilterGroupingTest|BmsPersistedMetadataResolverTest|BeatmapLocalMetadataDisplayResolverTest"`：**40/40** 通过。
- 已知 follow-up：`TestSceneManiaModAutoplay.TestPerfectScoreOnShortHoldNote` 在 CLI 下 `ModTestScene.TearDownSteps` 10s 超时——该 visual scene 与 BMS 无关、由 mania 短 hold 自身覆盖，与 P1-K 文档已记录的 "CLI 下 visual scene discover 不稳" 一致；同 scene 中关键的 K9 用例 `TestAutoplayIgnoresSampleOnlyScratchObjects` 通过。**⚠️【2026-06-01 订正】此「flake」判断已被推翻**：该 10s 超时实为本回归——`canParticipateInAutoplay` 只看顶层 `AffectsCombo`、误跳所有长条 → combo 到不了 4 → PassCondition 不满足 → ModTestScene 重试至超时，非 visual scene flake；已修复（nested-aware 谓词）并经用户实测，详见本 CHANGELOG 2026-06-01「修复 mania autoplay 整类长条不按」条。

---

## 2026-05-28

### K10 第二刀：大量 BMS 库下 carousel 性能与可用性闭环（基于真实 58k 谱库实测迭代）

针对真实 58k+ BMS 谱库实测中暴露的"启动批处理空跑、carousel filter 滑块卡 60s、难度动态变化"系列症状，做了一组相互衔接的修复。第一刀 K10-A（导入期持久化）保证未来导入即时就绪；本次第二刀闭合旧库回填路径与 carousel 读路径的剩余瓶颈。

**1. 启动批处理空跑——Realm 谓词翻译失效的回退**

[../../osu.Game/Database/BackgroundDataStoreProcessor.cs](../../osu.Game/Database/BackgroundDataStoreProcessor.cs) 的 `populateMissingConvertedStarRatings` 此前把 BMS 过滤写成 Realm-side `b.Ruleset.ShortName == BmsStarRatingResolver.RulesetShortName`（K9 后置维护时引入），意图让 query engine 翻译。**实测在 Realm 20.1.0 + 真实 58k 谱库下静默返回 0 结果**（既不抛 NotSupportedException 也不命中），导致旧 BMS 库永远不被批处理回填。改回客户端 `IsBmsBeatmap(b)` 过滤（先 `Where(b.BeatmapSet != null)` 让 Realm 物化非空集合，再客户端筛 BMS），并把 `Found N beatmaps which require converted star rating reprocessing.` 日志移到 early-return 之前——N=0 时也写日志，"通知未出现"不再是默默吞掉。

**2. DifficultyCalculator 内置 10 秒超时下的确定性失败固化**

[../../osu.Game/Rulesets/Difficulty/DifficultyCalculator.cs](../../osu.Game/Rulesets/Difficulty/DifficultyCalculator.cs) 在 `Calculate()` 无 token 调用时套了 10 秒内部超时（上游设计）。极端谱（如 Genngaozo 系列）转谱 >10s 必然抛 `OperationCanceledException`，原 catch 把它归类 "transient" 不持久化 → 每次启动浪费 10-25s、每次 carousel filter 都把它排进 async 队列 10s 后失败 → `Task.WhenAll` 卡到下一次 filter 取消它（用户实测 12-60s 卡顿）。修复：[BackgroundDataStoreProcessor.populateMissingConvertedStarRatings](../../osu.Game/Database/BackgroundDataStoreProcessor.cs) 与 [BeatmapDifficultyCache.EnsureConvertedStarRatingPersisted](../../osu.Game/Beatmaps/BeatmapDifficultyCache.cs) 都新增 `catch (OperationCanceledException)` 分支——这两条路径**不传 token**，OCE 唯一来源是内置 10s 超时，故为该谱的确定性属性，固化为 Failed。下次启动批处理直接跳过这些谱。

**3. Carousel 读路径同步识别 Failed 状态**

[../../osu.Game/Beatmaps/BeatmapDifficultyCache.cs](../../osu.Game/Beatmaps/BeatmapDifficultyCache.cs) 的 `MemoryCachingComponent` 设 `CacheNullValues => false`（上游）——失败的 compute 不缓存，下次 lookup 又重跑。即便 Failed 持久化了，`tryGetImmediateDifficulty` 原本只在 "Success" 时同步返回，Failed 时返回 false → carousel 走 async compute 路径 → 又触发 10s 超时。修复：扩展 `tryGetImmediateDifficulty`，当 `BmsPersistedMetadataResolver.HasCurrentConvertedStarRatingState` 为 true（包括 Failed）时同步返回 fallback `StarDifficulty(beatmapInfo.StarRating, ...)`（用 BMS playlevel 作为已知失败谱的近似值）。这样 carousel `getEffectiveStarRatingsStrict` 的 sync-first 循环**永不**把已知失败谱排入 async list。代价：这些谱在 mania 视图按 BMS playlevel 排序（2/58000 ≈ 可忽略）。

**4. Carousel sync-first 优化（避免 57k Task 分配）**

[../../osu.Game/Screens/Select/BeatmapCarousel.cs](../../osu.Game/Screens/Select/BeatmapCarousel.cs) 的 `getEffectiveStarRatingsStrict` 原本对 `beatmapsRequiringLookup` 中每个 beatmap 都 `Task.WhenAll(Select(async beatmap => await GetDifficultyAsync(...)))`，即便 `GetDifficultyAsync` 走 `Task.FromResult` 立即返回，57k 张 BMS 谱仍要付一次 async lambda + state machine + Task 实例的 GC/CPU 代价。改成两段式：先 sync 调用 `TryGetCachedDifficulty` 命中尽量多（57k 持久化的 BMS 全在这里），仅把真未命中的零头交给 `Task.WhenAll`。对 99% 命中率的库，Task 分配从 57k → 接近 0。

**5. 难度过滤滑块在 restricted 范围下的可用性恢复**

`BeatmapCarouselFilterMatching` 通过 `requiresStarRatingLookup(criteria)`（star 滤镜或 user-star 滤镜任一启用即触发）决定是否走 `getStarRatings` 路径。滑块在 "0-无限制" 时 `HasFilter=false` 走 `empty_star_ratings` 立即矩阵，故快；其它范围都触发上述 sync-first 路径——本次修复后，restricted 范围在 86k 库下也 <1 秒响应。

**实测验证（用户提供 1779978684.runtime/performance log）**

冷启动 + 不再回填：`Found 0 beatmaps which require converted star rating reprocessing.`，启动批处理在同一秒内完成，通知栏不再出现 reprocess 通知。
Carousel filter ops（含滑块各档位调整）：每次 400-800ms 全部走完 `Performing FilterMatching → Performing FilterSorting → Performing FilterGrouping → Updating Y positions → Items ready for display`，无 12-60s 卡死、无 `Cancelled due to newer request arriving`。
`BeatmapDifficultyCache: i:3 h:10 m:3 77%` 极低活动量，证明所有 BMS 谱都走 immediate 路径、async compute 路径几乎闲置。
排序正确、滑动流畅、滤镜可用——三大症状完全消除。

**已知 follow-up（不在本轮）**

1. 高难谱星数显示存在 sprite-text 数值过渡动画（"数字跳动"），系 carousel panel 层显示行为，属独立 UI 切片。期望直接显示，需要在 panel 关闭星数 incrementing animation 或改用 instant counter，归 `P1-A` / Song Select UI。
2. 极端难度谱滚动到时存在卡顿（越极限越明显，如压力测试谱面）——可能与 panel 内大量符头预览渲染 / texture atlas 频繁扩展（performance log 显示 `TextureAtlas size exceeded` 10+ 次）相关，与本轮数据层修复无关。
3. Genngaozo 系列 BMS 谱转谱 >10 秒的根因（pathological measure / event 模式）未单独排查；当前以 Failed 固化绕开，谱本身可在 mania 下显示为 BMS playlevel 星，不阻塞流程。
4. K10-B（读校验加固）继续推迟——本次实测进一步确认 LAV 在 RulesetStore 单实例共享下稳态正确，无需改造。
5. BeatmapUpdater 导入期端到端集成回归仍属测试缺口（OsuGameTestScene 级，CLI 下 visual scene discover 不稳）。

**测试 / 构建**

`dotnet build osu.Game/osu.Game.csproj -c Release` **0 错误**；`dotnet test .\osu.Game.Tests\... --filter "FullyQualifiedName~BeatmapCarouselFilterSortingTest|FullyQualifiedName~BeatmapCarouselFilterMatching|FullyQualifiedName~BeatmapCarouselFilterGrouping|Name~BmsStarRatingResolverTest"` **37/37** 通过；`Name~BmsStarRatingResolverTest` 单跑 **12/12** 通过。

### K10 第一刀（A 落地）：转谱星导入期持久化；B 经实测推迟；附 native 星批处理 return->continue 修复

- A 落地：[../../osu.Game/Beatmaps/BeatmapDifficultyCache.cs](../../osu.Game/Beatmaps/BeatmapDifficultyCache.cs) 现新增 `[Resolved] IRulesetStore` 与公开同步方法 `EnsureConvertedStarRatingPersisted(BeatmapInfo, IWorkingBeatmap)`：以 best-effort 复用现有 `BmsPersistedMetadataResolver` 与已统一的失败语义（确定不可转 -> 固化 Failed；瞬时异常 -> 不持久化），写入 caller 当前写事务中的 live metadata，不嵌套 `realmAccess.Write`。
- [../../osu.Game/Beatmaps/BeatmapUpdater.cs](../../osu.Game/Beatmaps/BeatmapUpdater.cs) 的 `Process()` 现对 BMS 谱在既有写事务内、设完 playlevel 星后调用该方法计算并持久化 mania 转谱星。结果：大批量导入后**同一会话内**切到 mania 即可直接读到持久化转谱星，carousel 不再回退 BMS playlevel + 异步 warmup 后重排，无需重启等启动批处理。
- B（读校验加固）实测后推迟：经核查，carousel `FilterCriteria.Ruleset`、spread display `ruleset.Value` 与 `BeatmapDifficultyCache.currentRuleset` 三处消费者用的都是同一个 `RulesetStore.AvailableRulesets` detached 实例（全局 `Ruleset.Value` 由 `RulesetStore.GetRuleset/First` 赋值），`clearOutdatedStarRatings` 每次启动都会就地同步该实例的 LAV；`ManiaDifficultyCalculator.Version` 亦为编译期常量。"消费者 LAV 过期"仅存在于启动后台处理尚未完成的瞬时、自愈窗口，不构成持续症状，强行改读路径风险大于收益，故推迟（详见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) 的 K10 节）。
- 附带：审查发现 [../../osu.Game/Database/BackgroundDataStoreProcessor.cs](../../osu.Game/Database/BackgroundDataStoreProcessor.cs) 的 `populateMissingStarRatings` 循环中 `if (beatmap == null) return;` 在某项被删除时会中断整批 + 让进度通知卡在 Active；改为 `continue`，与同文件其它批处理循环一致。
- 验证：`dotnet build osu.Desktop.slnf -c Release` **0 错误**；`dotnet test .\osu.Game.Tests\... --filter "Name~BmsStarRatingResolverTest|Name~BeatmapCarouselFilterSortingTest"` **20/20** 通过；`dotnet test .\osu.Game.Rulesets.Mania.Tests\... --filter "FullyQualifiedName~BmsToManiaBeatmapConverterTest"` **12/12** 通过。BeatmapUpdater 导入期端到端集成回归仍属测试缺口（需 OsuGameTestScene 级装配，与既有 visual scene 在 CLI 下 discover 不稳的现状一致），暂未补。

### K9 后置维护：转谱难度持久化链路失败语义统一与启动期收窄

- 统一"确定失败 vs 瞬时失败"语义：[../../osu.Game/Database/BackgroundDataStoreProcessor.cs](../../osu.Game/Database/BackgroundDataStoreProcessor.cs) 的 `populateMissingConvertedStarRatings` 现拆分 catch——只有 `BeatmapInvalidForRulesetException`（谱面确定无法转 mania，结果确定性）才写 `SetConvertedStarRatingFailure` 固化为 Failed；其它异常（IO / 临时错误）改为仅日志、不持久化，使下次启动按 `HasCurrentConvertedStarRatingState == false` 重新尝试，避免瞬时故障被粘成永久 raw BMS 星数。
- [../../osu.Game/Beatmaps/BeatmapDifficultyCache.cs](../../osu.Game/Beatmaps/BeatmapDifficultyCache.cs) 的 `computeDifficulty` 现先创建 difficulty calculator 取版本号、再执行转换，并新增 `persistConvertedStarRatingFailureIfApplicable`：当跨 ruleset 转换确定失败（`BeatmapInvalidForRulesetException` 且目标 ≠ 谱面自身 ruleset）时，按正确 difficulty version 持久化 Failed。此前懒写路径对任何异常都不写条目、每次都重算，现已与批处理路径行为对齐。
- [../../osu.Game/Database/BackgroundDataStoreProcessor.cs](../../osu.Game/Database/BackgroundDataStoreProcessor.cs) 的 BMS 谱面查询改用可被 Realm 翻译的谓词 `b.Ruleset.ShortName == BmsStarRatingResolver.RulesetShortName`，由查询引擎过滤，不再用客户端方法谓词物化并扫描全表；并给仅改内存的 `maniaRulesetInfo.LastAppliedDifficultyVersion` 赋值补注释，钉死"realm 持久化由更早的 `clearOutdatedStarRatings` 负责"这条隐式前置依赖。
- 未改动项与理由：会话内新导入谱面的懒补算（设计行为，改造需在导入流程挂钩子）、即时显示 MaxCombo 取 BMS 源值（纯展示，星数正确，取准确转换 combo 与"即时"目的冲突）、`conversion_version` 手动 bump（固有约束，注释已在 2026-05-27 补齐）。
- 验证：`dotnet build osu.Desktop.slnf -c Release` / `-c Debug` 均 **0 错误**；`dotnet test .\osu.Game.Tests\... --filter "Name~BmsStarRatingResolverTest|Name~BeatmapCarouselFilterSortingTest"` **13/13**；`dotnet test .\osu.Game.Rulesets.Mania.Tests\... --filter "FullyQualifiedName~BmsToManiaBeatmapConverterTest|Name~AutoplayIgnoresSampleOnlyScratchObjects"` **13/13** 通过。resolver 级 Failed 契约由 `BmsStarRatingResolverTest` 锁定；缓存/批处理 realm 集成层的"确定失败固化 / 瞬时失败重试"端到端回归暂列为待补。

## 2026-05-27

### K9 后置维护：转谱链常量去重、drawable 工厂反射改委托、conversion_version 维护导线与 Test Explorer 退出标记

- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs) 现已把 STOP-freeze 的 `BeatLength = 6` 提升为公开常量 `StopFreezeBeatLength` 并附契约注释；[../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaBeatmapConverter.cs) 的 STOP-freeze 剔除判定改为引用同一常量，消除两份独立 `6` 定义的静默失配风险（BMS 侧改变 freeze 值而 mania 侧未同步会让凍结点漏进 converted timing）。
- [../../osu.Game.Rulesets.Mania/UI/DrawableManiaRuleset.cs](../../osu.Game.Rulesets.Mania/UI/DrawableManiaRuleset.cs) 现已把每个 hit object 的 `MethodInfo.Invoke(null, object[])` 反射调用改为缓存的强类型委托（`Delegate.CreateDelegate` 生成 `Func<ManiaHitObject, bool>` 与 `Func<ManiaHitObject, DrawableHitObject<ManiaHitObject>?>`），去掉密谱面 drawable 生成时的逐对象装箱；仍保留程序集名前置守卫，只有 BMS-assembly 对象走该路径。纯重构，无行为变化。
- [../../osu.Game/Beatmaps/BmsPersistedMetadataResolver.cs](../../osu.Game/Beatmaps/BmsPersistedMetadataResolver.cs) 现已为 `current_bms_to_mania_conversion_version` 补上维护契约注释：转谱对象/时序/星数逻辑变更时必须按日期 bump，否则旧 persisted converted star 会变成陈旧缓存。
- [../../osu.Game/osu.Game.csproj](../../osu.Game/osu.Game.csproj) 现已显式设置 `<IsTestProject>false</IsTestProject>`：osu.Game 因引用 NUnit（仅为抽象测试场景基类）会被 C# Dev Kit 误当测试容器，对其启动 testhost 时因缺 runtimeconfig 探测路径而解析不到 NuGet 缓存里的 AutoMapper，导致测试发现中止。该退出标记仅影响 IDE/VSTest 发现，对构建与运行无影响，CLI `dotnet test` 与各 `*.Tests` 项目发现不受影响。
- 验证：`dotnet build osu.Desktop.slnf -c Release` 与 `-c Debug` 均 **0 错误**；`dotnet test .\osu.Game.Rulesets.Mania.Tests\... --filter "FullyQualifiedName~BmsToManiaBeatmapConverterTest|Name~AutoplayIgnoresSampleOnlyScratchObjects|Name~IgnoreOnlyDrawableDoesNotBlockColumnInput"` Release **14/14**、Debug `BmsToManiaBeatmapConverterTest` **12/12** 通过；`dotnet test .\osu.Game.Tests\... --filter "Name~BmsStarRatingResolverTest|Name~BeatmapCarouselFilterSortingTest"` **13/13** 通过。

## 2026-05-26

### K9：scratch sample-only 语义、autoplay ignore contract 与 persisted converted star 收口

- [../../osu.Game.Rulesets.Bms/Objects/BmsConvertedScratchSampleHitObject.cs](../../osu.Game.Rulesets.Bms/Objects/BmsConvertedScratchSampleHitObject.cs) 与 [../../osu.Game.Rulesets.Bms/UI/DrawableBmsConvertedScratchSampleHitObject.cs](../../osu.Game.Rulesets.Bms/UI/DrawableBmsConvertedScratchSampleHitObject.cs) 现已把 converted scratch-family object 冻结为 sample-only、ignore-judgement 的 mania object：它们继续保留原 keysound / head-tail sample 的时间线，但不再占 mania judged column，也不再进入 combo、statistics 与 star 计算 authority。
- [../../osu.Game.Rulesets.Mania/Replays/ManiaAutoGenerator.cs](../../osu.Game.Rulesets.Mania/Replays/ManiaAutoGenerator.cs) 现已在 action-point 生成与同列 next-object lookup 两侧都跳过 `Judgement.MaxResult.AffectsCombo() == false` 的对象，因此 converted scratch sample 不再为 autoplay 生成假按键，也不再扰动真实列的 key-up 时机。[../../osu.Game.Rulesets.Mania.Tests/Mods/TestSceneManiaModAutoplay.cs](../../osu.Game.Rulesets.Mania.Tests/Mods/TestSceneManiaModAutoplay.cs) 也已补上同列 scratch sample + 实 note 的 dedicated autoplay proof。
- [../../osu.Game/Beatmaps/BmsPersistedMetadataResolver.cs](../../osu.Game/Beatmaps/BmsPersistedMetadataResolver.cs)、[../../osu.Game/Beatmaps/BmsStarRatingResolver.cs](../../osu.Game/Beatmaps/BmsStarRatingResolver.cs)、[../../osu.Game/Beatmaps/BeatmapDifficultyCache.cs](../../osu.Game/Beatmaps/BeatmapDifficultyCache.cs) 与 [../../osu.Game/Database/BackgroundDataStoreProcessor.cs](../../osu.Game/Database/BackgroundDataStoreProcessor.cs) 现已把 modless `BMS -> mania` 星数写入 `BeatmapMetadata.RulesetDataJson` 的 BMS payload，并按 target ruleset、difficulty version 与 conversion version 做读取、失效和后台补算；[../../osu.Game/Screens/Select/PanelBeatmapStandalone.SpreadDisplay.cs](../../osu.Game/Screens/Select/PanelBeatmapStandalone.SpreadDisplay.cs) 与 [../../osu.Game/Screens/Select/PanelBeatmapSet.SpreadDisplay.cs](../../osu.Game/Screens/Select/PanelBeatmapSet.SpreadDisplay.cs) 则开始消费同一 current-ruleset resolved-star 读口，不再让 spread dots 回退到 raw BMS 星数。
- 验证：`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "Name~BmsToManiaBeatmapConverterTest|Name~IgnoreOnlyDrawableDoesNotBlockColumnInput|Name~AutoplayIgnoresSampleOnlyScratchObjects"` **14/14** 通过；`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore --filter "Name~BmsStarRatingResolverTest|Name~BeatmapCarouselFilterSortingTest"` **19/19** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

## 2026-05-25

### K8：gauge history consumer proof 完成，auto-shift timeline state 写死

- [../../osu.Game.Rulesets.Bms/UI/BmsGaugeHistoryGraph.cs](../../osu.Game.Rulesets.Bms/UI/BmsGaugeHistoryGraph.cs) 现已让 `SkinnableBmsGaugeHistoryPanelDisplay` 暴露只读 history state，供 focused proof 直接读取 `CreateStatisticsForScore()` 生成的 gauge history 数据，而不再依赖 CLI 下不稳定的 skinnable scene 装载链。
- [../../osu.Game.Rulesets.Bms.Tests/BmsRulesetStatisticsTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsRulesetStatisticsTest.cs) 新增 `TestCreateStatisticsGaugeHistoryCarriesAutoShiftTimelineState()`，直接锁住 auto-shift `EX-HARD -> HARD -> NORMAL` timeline 与对应 sample/time/value 会端到端进入 gauge history consumer，而不是仅返回 `SkinnableBmsGaugeHistoryPanelDisplay` 的 panel type。
- 该 proof 也已明确写死 gauge history consumer 语义：results panel 必须直接消费 `BmsClearLampProcessor.CreateGaugeHistory()` 计算出的 timeline state，不得在 panel/UI 层重新拼装或简化成另一套 timeline。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsRulesetStatisticsTest.TestCreateStatistics"` **4/4** 通过。

### K7：results summary consumer proof 完成，clear-lamp 优先级写死

- [../../osu.Game.Rulesets.Bms/UI/BmsResultsSummaryDisplay.cs](../../osu.Game.Rulesets.Bms/UI/BmsResultsSummaryDisplay.cs) 现已让 `SkinnableBmsResultsSummaryPanelDisplay` 暴露只读 summary state，供 focused proof 直接读取 `CreateStatisticsForScore()` 生成的 results summary 数据，而不再依赖 CLI 下不稳定的 skinnable scene 装载链。
- [../../osu.Game.Rulesets.Bms.Tests/BmsRulesetStatisticsTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsRulesetStatisticsTest.cs) 新增 `TestCreateStatisticsSummaryCarriesSelectedModesAndClearLamp()`，直接锁住 gauge type / display name、gauge rules family、judge mode、long-note mode、EX-SCORE、DJ LEVEL 与 computed clear lamp 会端到端进入 summary consumer。
- 该 proof 也已明确写死 clear-lamp 优先级：clear check 通过后，`PERFECT` / `FULL COMBO` 仍会覆盖 gauge-derived lamp，因此 results summary consumer 不得按 gauge type 自行派生 `HAZARD CLEAR` 一类显示文本。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsRulesetStatisticsTest.TestCreateStatistics"` **3/3** 通过。

### K6：results-side focused validation 完成，已带 mods playable contract 写死

- [../../osu.Game/Rulesets/Ruleset.cs](../../osu.Game/Rulesets/Ruleset.cs) 已明确 `PrepareScoreInfoForResults()` 与 `CreateStatisticsForScore()` 接收的是“已应用所有相关 mods 的 playable beatmap”；[../../osu.Game.Rulesets.Bms/BmsRuleset.cs](../../osu.Game.Rulesets.Bms/BmsRuleset.cs) 与 [../../osu.Game.Rulesets.Bms/Scoring/BmsClearLampProcessor.cs](../../osu.Game.Rulesets.Bms/Scoring/BmsClearLampProcessor.cs) 现已按此 contract 消费 caller 传入的 beatmap，不再在 results/gauge helper 内再次调用 `BmsBeatmapModApplicator`。
- [../../osu.Game.Rulesets.Bms.Tests/BmsPlayableBeatmapCacheTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsPlayableBeatmapCacheTest.cs) 新增 `Mirror` dedicated focused proof，直接锁住 `PrepareScoreInfoForResults()` 不会对已带 mods 的 playable beatmap 重复应用 beatmap mods；该 suite 基线现为 **5/5**。
- [../../osu.Game.Rulesets.Bms.Tests/BmsClearLampProcessorTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsClearLampProcessorTest.cs) 新增两条 `Mirror` focused proofs，分别锁住 `CreateGaugeHistory()` 与 `CalculateFinalGauge()` 不会对已带 mods 的 playable beatmap 重复应用 beatmap mods；依赖 long-note / assist 语义的 HCN、autoplay 邻接用例也已改为显式先应用 score mods，再进入 clear-lamp helper。该 suite 基线现为 **32/32**。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsPlayableBeatmapCacheTest"` **5/5** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsClearLampProcessorTest"` **32/32** 通过。

### K5：让 parse-side playable projection 收口为 source-bound cache contract

- [../../osu.Game/Beatmaps/ICachedModlessPlayableBeatmapSource.cs](../../osu.Game/Beatmaps/ICachedModlessPlayableBeatmapSource.cs) 现已定义 source-bound 的 modless playable cache contract；[../../osu.Game/Beatmaps/WorkingBeatmap.cs](../../osu.Game/Beatmaps/WorkingBeatmap.cs) 则会在 `GetPlayableBeatmap()` 中优先复用实现该 contract 的 source beatmap 上已缓存的无 mods playable projection，只有换 source 或带 mods 时才重新转换。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedBeatmap.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedBeatmap.cs) 现已按 ruleset short name 持有 modless playable cache，而 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs) 也会把 loader 首次 conversion 的现成 projection seed 回 source wrapper；同时 factory seed 现已补齐 no-mod finalize，不会再把“只 convert、未生成 hold nested objects”的半成品 playable 缓进 source beatmap。
- [../../osu.Game.Rulesets.Bms.Tests/BmsPlayableBeatmapCacheTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsPlayableBeatmapCacheTest.cs) 新增 dedicated focused proof，锁住同源复用、跨 source 隔离、带 mods 绕过缓存，以及 loader-seeded cache 返回的 hold-note projection 已完成 finalize；相邻 loader-focused `BmsImportIntegrationTest` 回归也已继续确认 import metadata / timing 合同未因 cache seed 回归。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsPlayableBeatmapCacheTest"` **4/4** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsImportIntegrationTest.TestLoader"` **9/9** 通过。

### K4-S：让 set-level artist display 复用 shared artist authority

- [../../osu.Game/Beatmaps/BeatmapSetInfoExtensions.cs](../../osu.Game/Beatmaps/BeatmapSetInfoExtensions.cs) 现已新增 `GetDisplayArtistRomanisable()` shared helper：当 beatmap set 持有具体 beatmap 时，优先复用首个 beatmap 的 display artist authority，只在没有 beatmap 时才回退到 set metadata 的 raw artist text。
- [../../osu.Game/Screens/Select/PanelBeatmapSet.cs](../../osu.Game/Screens/Select/PanelBeatmapSet.cs) 与 [../../osu.Game/Overlays/Music/PlaylistItem.cs](../../osu.Game/Overlays/Music/PlaylistItem.cs) 现已通过 `BeatmapSetInfo.GetDisplayArtistRomanisable()` 显示 set-level artist，不再继续直接走 raw `beatmapSet.Metadata.Artist` / `ArtistUnicode`；因此 Song Select set panel 与 playlist tray 都不会再暴露 raw `/obj:` 后缀。
- [../../osu.Game.Tests/Menus/BeatmapSetArtistLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Menus/BeatmapSetArtistLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 BMS artist clean 与 non-BMS passthrough contract；因此 set-level artist display surface 现已具备独立 plain focused proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BeatmapSetArtistLocalMetadataDisplayTest"` **2/2** 通过。

### K4-F follow-up：补齐 BeatmapAttributeText plain focused proof

- [../../osu.Game/Skinning/Components/BeatmapAttributeText.cs](../../osu.Game/Skinning/Components/BeatmapAttributeText.cs) 现已补出 `GetDisplayedArtist()` 与 `GetDisplayedCreator()` internal helper，让 shared beatmap-attribute display consumer 的 artist / creator 读口可以直接复用组件内 authority，并脱离 CLI scene discoverability 做最窄 plain proof。
- [../../osu.Game.Tests/Skins/BeatmapAttributeTextLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Skins/BeatmapAttributeTextLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 BMS artist clean、creator fallback 与 non-BMS passthrough contract；因此 `BeatmapAttributeText` 现已具备独立 plain focused proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BeatmapAttributeTextLocalMetadataDisplayTest"` **2/2** 通过。

### K4-R：让 delete confirmation title display 复用 set-level title authority

- [../../osu.Game/Beatmaps/BeatmapSetInfoExtensions.cs](../../osu.Game/Beatmaps/BeatmapSetInfoExtensions.cs) 现已新增 shared set-level title helper：当 beatmap set 持有具体 beatmap 时，优先复用首个 beatmap 的 full title authority，并允许各个 set-level surface 显式保持是否展示 creator 的既有合同。
- [../../osu.Game/Screens/Select/BeatmapDeleteDialog.cs](../../osu.Game/Screens/Select/BeatmapDeleteDialog.cs) 现已通过 `BeatmapSetInfo.GetDisplayTitleRomanisable(includeCreator: false)` 显示删除确认标题，不再继续直接走 `beatmapSet.Metadata.GetDisplayTitleRomanisable(false)`；因此 delete confirmation title 不再暴露 raw `/obj:` 后缀，也不会误带 difficulty name，同时继续保持不展示 creator suffix 的既有外观。
- [../../osu.Game.Tests/Menus/BeatmapDeleteDialogLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Menus/BeatmapDeleteDialogLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 BMS fallback、non-BMS passthrough、“无 creator 泄漏”与“无难度名泄漏”的 contract；[../../osu.Game.Tests/Menus/ScopedBeatmapSetDisplayLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Menus/ScopedBeatmapSetDisplayLocalMetadataDisplayTest.cs) 也已补强相邻 shared-helper contract 的难度名断言。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BeatmapDeleteDialogLocalMetadataDisplayTest|FullyQualifiedName~ScopedBeatmapSetDisplayLocalMetadataDisplayTest"` **4/4** 通过。

## 2026-05-23

### K4-Q：让 Daily Challenge title display 复用 full-beatmap title authority

- [../../osu.Game/Screens/OnlinePlay/DailyChallenge/DailyChallengeIntro.cs](../../osu.Game/Screens/OnlinePlay/DailyChallenge/DailyChallengeIntro.cs) 现已让 daily challenge title display 在可拿到具体 beatmap 时优先调用 `IBeatmapInfo.GetDisplayTitleRomanisable(includeDifficultyName: false)`，不再继续直接走 `beatmap.BeatmapSet!.Metadata.GetDisplayTitleRomanisable(false)`，因此不会再暴露 raw `/obj:` 后缀，也不会把难度名重新带回标题行。
- [../../osu.Game.Tests/OnlinePlay/DailyChallengeLocalMetadataDisplayTest.cs](../../osu.Game.Tests/OnlinePlay/DailyChallengeLocalMetadataDisplayTest.cs) 现已把 plain NUnit focused proof 扩展到 creator fallback 与 title authority 两侧，同时锁住 BMS fallback、non-BMS passthrough 与“无难度名泄漏”的 contract；当 visual scene 主要覆盖转场时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~DailyChallengeLocalMetadataDisplayTest"` **4/4** 通过。

### K4-P：让 scoped beatmap-set title display 复用 full-beatmap title authority

- [../../osu.Game/Screens/Select/FilterControl.ScopedBeatmapSetDisplay.cs](../../osu.Game/Screens/Select/FilterControl.ScopedBeatmapSetDisplay.cs) 现已让 scoped beatmap set title display 在能拿到具体 beatmap 时优先调用 `IBeatmapInfo.GetDisplayTitleRomanisable(includeDifficultyName: false)`，只在空 set 时才回退到 metadata-only overload，因此 scoped-set banner 不再暴露 raw `/obj:` 后缀，也不会误带难度名。
- [../../osu.Game.Tests/Menus/ScopedBeatmapSetDisplayLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Menus/ScopedBeatmapSetDisplayLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住首个 beatmap authority reuse、BMS fallback、non-BMS passthrough 与“无难度名泄漏”的 contract；当 set-level UI 只是转发标题字符串时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~ScopedBeatmapSetDisplayLocalMetadataDisplayTest"` **2/2** 通过。

### K4-O：让 IBeatmapInfo title display 复用 display artist / creator fallback

- [../../osu.Game/Beatmaps/BeatmapInfoExtensions.cs](../../osu.Game/Beatmaps/BeatmapInfoExtensions.cs) 现已让 `IBeatmapInfo.GetDisplayTitleRomanisable()` 同时通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()` / `GetDisplayCreator()` 读取 BMS local metadata display authority，不再在 title display consumer 上直接暴露 embedded creator suffix 或空 creator。
- [../../osu.Game.Tests/Localisation/BeatmapInfoRomanisationLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Localisation/BeatmapInfoRomanisationLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 BMS fallback 与非 BMS passthrough；当具体 UI 只是转发 title string 时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BeatmapInfoRomanisationLocalMetadataDisplayTest"` **2/2** 通过。

### K4-N：让 beatmap skin metadata 复用 display creator fallback

- [../../osu.Game/Skinning/LegacyBeatmapSkin.cs](../../osu.Game/Skinning/LegacyBeatmapSkin.cs) 现已让 beatmap skin metadata 的 `SkinInfo.Creator` 通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayCreator()` 读取 BMS local creator fallback，不再继续直接展示 raw `Metadata.Author.Username`。
- [../../osu.Game.Tests/Skins/LegacyBeatmapSkinLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Skins/LegacyBeatmapSkinLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 beatmap skin metadata 的 creator 读口；当 beatmap skin 只通过 `SkinInfo` 暴露 metadata 时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~LegacyBeatmapSkinLocalMetadataDisplayTest"` **2/2** 通过。

### K4-M：让 matchmaking round results 优先复用本地 BeatmapInfo

- [../../osu.Game/Screens/OnlinePlay/Matchmaking/Match/RoundResults/SubScreenRoundResults.cs](../../osu.Game/Screens/OnlinePlay/Matchmaking/Match/RoundResults/SubScreenRoundResults.cs) 现已在按 API scores 构造 `ScoreInfo` 时优先复用本地 `BeatmapInfo`，仅在本地谱面缺失时才回退到 API 最小壳，从而保住 round-results `ScorePanel` / `ExpandedPanelMiddleContent` 已接好的 BMS local metadata display authority。
- [../../osu.Game.Tests/OnlinePlay/SubScreenRoundResultsLocalMetadataDisplayTest.cs](../../osu.Game.Tests/OnlinePlay/SubScreenRoundResultsLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 local beatmap reuse 与 API fallback shell；当 visual scene 只看到最终 `ScorePanel` 内容时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~SubScreenRoundResultsLocalMetadataDisplayTest"` **2/2** 通过。

### K4-L：让 online playlist creator display 复用 display creator fallback

- [../../osu.Game/Screens/OnlinePlay/DrawableRoomPlaylistItem.cs](../../osu.Game/Screens/OnlinePlay/DrawableRoomPlaylistItem.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayCreator()` / `HasLinkedCreatorProfile()` 读取 BMS local creator fallback，不再因空 `Metadata.Author.Username` 隐藏作者行；有真实作者资料时继续保留 user link，没有时回退为 plain text creator。
- [../../osu.Game.Tests/OnlinePlay/DrawableRoomPlaylistItemLocalMetadataDisplayTest.cs](../../osu.Game.Tests/OnlinePlay/DrawableRoomPlaylistItemLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 creator 文本与 linked-profile 分支；当 visual scene 难以稳定断言 user link 行为时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~DrawableRoomPlaylistItemLocalMetadataDisplayTest"` **2/2** 通过。

### K4-K：让 daily challenge creator display 复用 display creator fallback

- [../../osu.Game/Screens/OnlinePlay/DailyChallenge/DailyChallengeIntro.cs](../../osu.Game/Screens/OnlinePlay/DailyChallenge/DailyChallengeIntro.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayCreator()` 读取 BMS local creator fallback，不再在 daily challenge metadata surface 内继续直接展示 raw local creator。
- [../../osu.Game.Tests/OnlinePlay/DailyChallengeLocalMetadataDisplayTest.cs](../../osu.Game.Tests/OnlinePlay/DailyChallengeLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 creator 读口；当 visual scene 主要覆盖转场时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~DailyChallengeLocalMetadataDisplayTest"` **2/2** 通过。

### K4-J：让 menu metadata display 复用 display artist fallback

- [../../osu.Game/Screens/Menu/SongTicker.cs](../../osu.Game/Screens/Menu/SongTicker.cs) 与 [../../osu.Game/Overlays/NowPlayingOverlay.cs](../../osu.Game/Overlays/NowPlayingOverlay.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()` 读取 BMS local artist fallback，不再在 menu / now-playing metadata surface 内继续直接展示 raw local artist。
- [../../osu.Game.Tests/Menus/MenuBeatmapMetadataLocalDisplayTest.cs](../../osu.Game.Tests/Menus/MenuBeatmapMetadataLocalDisplayTest.cs) 新增 plain NUnit focused test，直接锁住两个 surface 的 artist 读口；当 visual scene 没有直接暴露 metadata 断言时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~MenuBeatmapMetadataLocalDisplayTest"` **2/2** 通过。

### K4-I：让 profile metadata display 复用 display artist fallback

- [../../osu.Game/Overlays/Profile/Sections/Ranks/DrawableProfileScore.cs](../../osu.Game/Overlays/Profile/Sections/Ranks/DrawableProfileScore.cs) 与 [../../osu.Game/Overlays/Profile/Sections/Historical/DrawableMostPlayedBeatmap.cs](../../osu.Game/Overlays/Profile/Sections/Historical/DrawableMostPlayedBeatmap.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()` 读取 BMS local artist fallback，不再在 profile beatmap metadata surface 内继续直接展示 raw local artist。
- [../../osu.Game.Tests/Online/ProfileBeatmapMetadataLocalDisplayTest.cs](../../osu.Game.Tests/Online/ProfileBeatmapMetadataLocalDisplayTest.cs) 新增 plain NUnit focused test，直接锁住两个 surface 的 artist 读口；当 `TestSceneUserProfileScores` 与 `TestSceneHistoricalSection` 这类 visual scene 在 CLI 下不可 discover 时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~ProfileBeatmapMetadataLocalDisplayTest"` **2/2** 通过。

### K4-H：让 results metadata display 复用 display artist / creator fallback

- [../../osu.Game/Screens/Ranking/Expanded/ExpandedPanelMiddleContent.cs](../../osu.Game/Screens/Ranking/Expanded/ExpandedPanelMiddleContent.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()` / `GetDisplayCreator()` 读取 BMS local artist / creator fallback，不再在 results screen 的 expanded metadata surface 内继续直接展示 raw local metadata。
- [../../osu.Game.Tests/Scores/ExpandedPanelMiddleContentLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Scores/ExpandedPanelMiddleContentLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 artist / creator 读口；当 `TestSceneExpandedPanelMiddleContent` 这类 visual scene 在 CLI 下不可 discover 时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~ExpandedPanelMiddleContentLocalMetadataDisplayTest"` **2/2** 通过。

### K4-G：让 gameplay metadata display 复用 display artist / creator fallback

- [../../osu.Game/Screens/Play/BeatmapMetadataDisplay.cs](../../osu.Game/Screens/Play/BeatmapMetadataDisplay.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()` / `GetDisplayCreator()` 读取 BMS local artist / creator fallback，不再在 gameplay loading surface 内直接展示 raw local metadata。
- [../../osu.Game.Tests/Visual/Gameplay/TestSceneBeatmapMetadataDisplay.cs](../../osu.Game.Tests/Visual/Gameplay/TestSceneBeatmapMetadataDisplay.cs) 现改用组件 internal readback 锚点锁住 display text，避免继续依赖不稳定的 scene 树遍历断言；focused validation 也固定为整类 `TestSceneBeatmapMetadataDisplay` filter，而不是宽泛匹配 `TestLocal`。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~TestSceneBeatmapMetadataDisplay"` **8/8** 通过。

### K4-F：让 local-metadata display consumer 复用 display artist / creator fallback

- [../../osu.Game/Screens/Select/BeatmapCarouselFilterSorting.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterSorting.cs)、[../../osu.Game/Screens/Select/BeatmapCarouselFilterGrouping.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterGrouping.cs) 与 [../../osu.Game/Screens/Select/BeatmapCarouselFilterMatching.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterMatching.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()` 读取 BMS local artist fallback，不再把 embedded creator suffix 暴露给 Song Select 的 artist sort/group/filter。
- [../../osu.Game/Skinning/Components/BeatmapAttributeText.cs](../../osu.Game/Skinning/Components/BeatmapAttributeText.cs) 现也通过 `BeatmapLocalMetadataDisplayResolver` 统一读取 BMS local artist / creator display text，不再在 shared beatmap-attribute display consumer 内直接使用 raw `Metadata.Artist` / `Metadata.Author.Username`。
- [../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterSortingTest.cs](../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterSortingTest.cs)、[../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterGroupingTest.cs](../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterGroupingTest.cs)、[../../osu.Game.Tests/NonVisual/Filtering/FilterMatchingTest.cs](../../osu.Game.Tests/NonVisual/Filtering/FilterMatchingTest.cs) 与 [../../osu.Game.Tests/Beatmaps/BeatmapLocalMetadataDisplayResolverTest.cs](../../osu.Game.Tests/Beatmaps/BeatmapLocalMetadataDisplayResolverTest.cs) 已锁住这条 artist selector reuse path。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "(FullyQualifiedName~TestSortingByArtistUsesBmsDisplayArtistFallback|FullyQualifiedName~TestGroupingByArtist|FullyQualifiedName~TestCriteriaMatchingArtistDoesNotMatchBmsCreatorSuffix|FullyQualifiedName~TestCriteriaMatchingArtistWithNullUnicodeName|FullyQualifiedName~TestCriteriaNotMatchingArtist|FullyQualifiedName~TestDisplayArtistStripsEmbeddedBmsCreator)"` **9/9** 通过；相邻 `BeatmapAttributeText` plain focused proof 已于 `2026-05-25` 由 `BeatmapAttributeTextLocalMetadataDisplayTest` **2/2** 补齐。

### K4-E：让 Song Select creator selector 复用 display creator fallback

- [../../osu.Game/Screens/Select/BeatmapCarouselFilterSorting.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterSorting.cs)、[../../osu.Game/Screens/Select/BeatmapCarouselFilterGrouping.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterGrouping.cs) 与 [../../osu.Game/Screens/Select/BeatmapCarouselFilterMatching.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterMatching.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayCreator()` 读取 BMS local creator fallback，不再只按 `Metadata.Author.Username` 做 Song Select 的 author sort/group/filter。
- [../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterSortingTest.cs](../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterSortingTest.cs)、[../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterGroupingTest.cs](../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterGroupingTest.cs) 与 [../../osu.Game.Tests/NonVisual/Filtering/FilterMatchingTest.cs](../../osu.Game.Tests/NonVisual/Filtering/FilterMatchingTest.cs) 已锁住这条 selector reuse path。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "(FullyQualifiedName~TestSortingByAuthorUsesBmsDisplayCreatorFallback|FullyQualifiedName~TestGroupingByAuthorUsesBmsDisplayCreatorFallback|FullyQualifiedName~TestCriteriaMatchingCreatorUsesBmsDisplayCreatorFallback)"` **3/3** 通过。

### K4-D：让 core metadata read-model 复用 persisted chart_metadata projection

- [../../osu.Game/Beatmaps/BmsPersistedMetadataResolver.cs](../../osu.Game/Beatmaps/BmsPersistedMetadataResolver.cs) 现为 `osu.Game` 提供统一的 typed persisted `chart_metadata` projection，避免 core consumer 各自手拆 `RulesetDataJson`。
- [../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs](../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs) 与 [../../osu.Game/Beatmaps/BmsStarRatingResolver.cs](../../osu.Game/Beatmaps/BmsStarRatingResolver.cs) 现已共享这条读取路径，不再各自维护 `JObject.SelectToken("chart_metadata...")` 的 stringly-typed token 合同。
- [../../osu.Game.Tests/Beatmaps/BeatmapLocalMetadataDisplayResolverTest.cs](../../osu.Game.Tests/Beatmaps/BeatmapLocalMetadataDisplayResolverTest.cs) 与 [../../osu.Game.Tests/Beatmaps/BmsStarRatingResolverTest.cs](../../osu.Game.Tests/Beatmaps/BmsStarRatingResolverTest.cs) 已锁住这条 core-side persisted metadata reuse path。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "(FullyQualifiedName~BmsStarRatingResolverTest|FullyQualifiedName~BeatmapLocalMetadataDisplayResolverTest)"` **11/11** 通过。

### K4-C：让 beatmap statistics 复用 metadata 中的 chart-filter projection

- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs) 现会在 `GetStatistics()` 中优先读取 `BeatmapInfo.Metadata.GetChartFilterStats()`，只有缺失时才回退到 `BmsChartFilterStats.FromBeatmap(this)`。
- 同一处 consumer 在缺失 `ChartFilterStats` 时会把现场计算结果写回 metadata，避免同一 runtime beatmap 反复本地重数同一份 projected hitobjects。
- [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapStatisticsTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapStatisticsTest.cs) 已补上 focused regressions，锁住“优先复用 metadata / 缺失时回写缓存”的 statistics consumer 选择逻辑。
- 验证：`BmsBeatmapStatisticsTest` **3/3** 通过，`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal` **812/812** 通过。

## 2026-05-22

### K4-B：让 note distribution graph 复用 projected working beatmap

- [../../osu.Game.Rulesets.Bms/SongSelect/BmsNoteDistributionGraph.cs](../../osu.Game.Rulesets.Bms/SongSelect/BmsNoteDistributionGraph.cs) 新增 `ResolveBeatmapForAnalysis()`，会在 working beatmap 的 source beatmap 已携带 `BmsHitObject` projection 时直接复用它，只在缺失时才回退到 `GetPlayableBeatmap()`。
- 同一文件的 note-distribution 数据构造现统一从 `BeatmapInfo.Metadata` 读取 `ChartMetadata`，使 projected source beatmap 与 playable beatmap 继续共享同一摘要来源，而不是再依赖 consumer-local second conversion。
- [../../osu.Game.Rulesets.Bms.Tests/BmsNoteDistributionGraphTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsNoteDistributionGraphTest.cs) 已补上两条 focused regressions，锁住“优先复用 projected source beatmap / 无 projection 时回退 playable conversion”的选择逻辑。
- 验证：`BmsNoteDistributionGraphTest` **5/5** 通过，`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal` **810/810** 通过。

### K4-A：让 static background 首次复用 unified projection

- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs) 新增 `GetPreferredBackgroundAssetReference()`，统一选择 `STAGEFILE/BACKBMP/BANNER` 或 richer visual-definition family 的首个 bitmap；若 `#BGA/#@BGA` 持有的是两位 bitmap reference，还会先通过 `BitmapTable` 解析回实际资源名。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs) 与 [../../osu.Game.Rulesets.Bms/UI/BmsBackgroundLayer.cs](../../osu.Game.Rulesets.Bms/UI/BmsBackgroundLayer.cs) 现已共享这条 background asset projection，使 metadata background、导入后的图片正规化与 playfield static background consumer 不再各自只认 `STAGEFILE/BACKBMP/BANNER`。
- 这一步把 richer visual-definition family 的首个 consumer 真正接到了运行中的 static background 路径上，并顺手修正了一个常见坑：不能把 `#BGA/#@BGA` 的两位引用直接当文件名，必须先过 `BitmapTable`。
- 验证：新增 static-background targeted regressions **3/3** 通过，`BmsBeatmapConverterTest` **13/13** 通过，`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal` **808/808** 通过。

### K3-F：补齐 unified visual-definition projection contract

- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsVisualDefinitions.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsVisualDefinitions.cs) 现新增 `BmsVisualDefinitionProjection`；[../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs) 也新增 `GetVisualDefinitionProjections()` 与 `TryGetVisualDefinitionProjection()`，把 `#BGA`、`#@BGA`、`#ARGB`、`#SWBGA` 与 `#POORBGA` 的分散 header tables 收口为统一组合视图。
- 本轮仍严格限制在 decoder/model：原始 definition tables 继续保留，新的 projection 只是把同 index 的 header family 组合给下游读取，不改 converter、importer、Song Select 与 runtime visual consumer。
- 这一步把 richer visual-definition family 的“projection contract”正式冻结下来；剩余 gap 已从“如何组合四张表”收窄到“哪个 consumer 先采用这条统一投影”。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsBeatmapDecoderTest"` **33/33** 通过，`BmsBeatmapConverterTest` **12/12** 通过，`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal` **805/805** 通过。

### K3-E：补齐 richer BGA-definition header typed surface

- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsVisualDefinitions.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsVisualDefinitions.cs) 现新增 richer BGA-definition header family 的 typed model；[../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs) 也新增 `BgaDefinitions`、`AtBgaDefinitions`、`ArgbDefinitions`、`SwBgaDefinitions` 与 `PoorBgaMode`，让 `#BGA`、`#@BGA`、`#ARGB`、`#SWBGA`、`#POORBGA` 不再只停留在 generic unknown bag。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs) 现会把上述 header 解析进 typed surface，并保留 `#BGA/#@BGA` 的 bitmap/reference 原始 token，不提前把 bitmap 绑定、动画调度或运行时播放语义锁死在 decoder。
- 本轮仍严格限制在 decoder/model；converter、importer、Song Select 与 runtime visual consumer 均未改动。K3-E 只负责把 header-side definition surface 冻结下来，把剩余 gap 收窄到 consumer/projection 层。
- 验证：`BmsBeatmapDecoderTest` **32/32** 通过，`BmsBeatmapConverterTest` **12/12** 通过，`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal` **804/804** 通过。

### K3-D：补齐 `SCROLLxx/SC` 的 typed consumer contract

- `BmsDecodedChart` 现新增 `ScrollEvents`；`BmsBeatmapDecoder` 会把 `SCROLLxx` 定义 + `SC` channel line 解析成 typed scroll surface，而不再只把它们留在 `ScrollTable` 与 raw placeholder 层。
- 为避免 `SC` 与其它 unknown channel 在同拍位被错误 compound，decoder 的 unknown-channel duplicate 键现已按 `RawChannelToken` 区分；`SC` 不会再因共享 `channel = -1` 而被其它未知轨覆盖掉。
- `BmsBeatmapConverter` 现已把 `ScrollEvents` 接到 `ControlPointInfo.EffectPoints`，让 `SCROLLxx/SC` 首次进入 runtime scroll-speed consumer contract，同时不改 importer、Song Select 或现有 visual consumer。
- 验证：`BmsBeatmapDecoderTest` **31/31** 通过，`BmsBeatmapConverterTest` **12/12** 通过，`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal` **803/803** 通过。

### K3-C：补齐 BGA / invisible / mine 的最薄 typed surface

- `BmsDecodedChart` 现新增 `BgaEvents`、`InvisibleObjectEvents` 与 `MineEvents`；`BmsBeatmapDecoder` 也已对 BGA base/poor/layer/layer2、invisible object 与 landmine channel 建立 typed post-process 分派，不再要求下游从 raw carrier 重新猜 channel 语义。
- 本轮只收口 parse/model additive surface，不改 converter、importer、runtime 或现有 visual consumer；第一批 typed slot 先为后续背景层、统计面与特效谱支持冻结中间模型合同。
- `BmsBeatmapDecoderTest` 已新增 BGA / invisible / mine 三条 parser focused regression；为确认更宽基线没有被新 typed surface 破坏，本轮还追加重跑了 `BmsBeatmapConverterTest` 与全量 BMS suite。
- 验证：`BmsBeatmapDecoderTest` **29/29** 通过，`BmsBeatmapConverterTest` **11/11** 通过，`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal` **800/800** 通过。

### K3-B：补齐 LNTYPE 2 的最小 MGQ long-note expression

- `BmsBeatmapDecoder` 现会在 LN channel 保留显式 `00` 作为 `LNTYPE 2` 的 closing marker，并把 duplicate line compound 收口为“`00` 不覆盖已有对象”；这让 MGQ 长条可以跨小节连续，并在 zero slot 处收口，而不再停留在 warning-only。
- `BmsLongNoteEncoding` 新增 `LnType2`，`BmsLongNoteEvent` 的 carrier 注释也已扩到 `LNTYPE` 全族；decoder focused regressions 现覆盖跨小节配对与 duplicate zero 不覆盖已有 segment 两条关键语义。
- `BmsBeatmapConverterTest` 现已补上 end-to-end proof，证明 `LNTYPE 2` 的最小表达可直接沿既有 hold-note conversion path 转成 `BmsHoldNote`。
- 验证：`BmsBeatmapDecoderTest` **26/26** 通过，`BmsBeatmapConverterTest` **11/11** 通过。

### K3-A：冻结同拍位 control-event 顺序与 signed BPM converter contract

- `BmsBeatmapConverter` 现会先应用同拍位 `BPM` 与 `STOP`，再结算 object / long-note endpoint 的 event time；converter authority 现固定为 `BPM -> STOP -> object`，不再让 consumer 各自猜顺序。
- signed BPM 的 timeline 推进现按绝对值消费；negative `#BPMxx` 不再在 converter 里被 `Math.Max(1, bpm)` 错误钳成 `1 BPM`。
- `BmsBeatmapConverterTest` 已新增 same-position control-event order 与 signed BPM timing regressions；本轮 focused suite **8/8** 通过。

### K2-A：signed BPM 与 duplicate channel compound 进入 parser contract

- `BmsBpmChangeEvent` 现允许 non-zero signed BPM 进入 typed model，`BmsBeatmapDecoder` 也会保留 negative `#BPMxx`，不再在 parser 阶段直接把方向信息拒掉。
- decoder 的 typed post-process 现已对同 `measure/channel/fraction` 的 duplicate channel collision 做 source-order-aware compound；raw carrier 继续完整保留全部原始 channel events。
- `BmsBeatmapDecoderTest` 已新增 signed BPM 与 duplicate channel focused regression；本轮 focused suite **23/23** 通过。

### K1-B：scroll 定义、unknown bag 与非十六进制 channel raw placeholder 落地

- `BmsBeatmapInfo` 现新增 `ScrollTable` 与 `UnknownHeaders`；`BmsBeatmapDecoder` 会保留 `#SCROLLxx` 定义，并把未识别的 header / indexed definition 写入 unknown bag，而不是继续静默跳过。
- decoder 现也会接受非十六进制 channel token，并将其作为 raw placeholder 写入 `RawChannelEvents`；这让 `SC` 这类 channel line 至少可以 no-loss 回收，而不会在 parser 入口直接丢失。
- `BmsBeatmapDecoderTest` 已新增 `SCROLLxx/SC` 与 unknown bag focused regression；本轮 focused suite **21/21** 通过。

### K1-A：raw channel carrier 与 source line order 首刀落地

- `BmsDecodedChart` 现已显式暴露 `RawChannelEvents`，并保留 `ChannelEvents` 作为兼容别名；raw channel carrier 不再只是隐式 fallback 列表。
- `BmsChannelEvent` 现新增 `RawChannelToken` 与 `SourceLineOrder`；`BmsBeatmapDecoder` 会按 source channel line 填充这两个字段，并以 `SourceLineOrder` 作为同 `measure/fraction/channel` 下的最终 tie-break。
- `BmsBeatmapDecoderTest` 已新增 raw carrier focused regression，验证 `RawChannelEvents`、`RawChannelToken` 与 `SourceLineOrder` 的首轮合同；本轮 focused suite **20/20** 通过。

### 文档：补齐 P1-K 的依赖与回退边界

- `DEVELOPMENT_PLAN.md` 现新增“依赖与回退边界”表，把 `K1-A` 到 `K4-A` 的进入前提、失败信号、允许回退与明确禁止项固定下来。
- `TECHNICAL_CONSTRAINTS.md` 现新增回退约束，明确失败后只能收缩新增暴露面，不能把 no-loss carrier、source line order 或 focused regression 一并删掉。
- `DEVELOPMENT_STATUS.md` 现明确记录：当前文档层面已经足以独立驱动 `K1-A` 开工，剩余开放项属于实现期决策而非规划缺口。

### 文档：把 P1-K 扩写成可直接开工的执行包

- `P1-K` 的 `DEVELOPMENT_PLAN.md` 现已补齐文件级切片图、首轮开工顺序、focused test 落点、推荐验证命令与“何时算可以直接开工”的进入条件，不再只是方向性规划。
- `TECHNICAL_CONSTRAINTS.md` 现新增切片边界约束，明确 `K1-K3` 首轮只允许改 in-memory parse chain，`K4` 之后才触碰 projection reuse 与 importer/raw-wrapper consumer。
- `DEVELOPMENT_STATUS.md` 现新增首轮开工包，把 `K1-A` 到 `K4-A` 的主文件、目标与每刀验证顺序固定下来，后续可以直接照文档执行。

### 文档：新建 P1-K 子线并冻结 BMS 解析链路治理范围

- 已新建 `P1-K` 四件套，并把 **BMS 解析链路治理** 正式归入 Phase 1.x 子线编排；主 authority 明确落在 decoder、normalized chart model、converter 语义、projection reuse 与 parse-side cache。
- 主线总规划、主线状态页、主线变更日志与子线索引已同步加入 `P1-K`，首轮执行顺序冻结为：`raw/typed 双层模型冻结` → `header/definition/channel no-loss coverage` → `timeline/control-event semantics` → `parse-once/project-many 复用` → `focused validation 与缓存边界`。
- 本轮同时把当前 parse-chain 的主要 gap 写入子线基线：`SCROLLxx/SC` 未进入模型、signed BPM typed surface 不可表示、duplicate channel line 未 compound、同拍位 `BPM/STOP/object` 顺序未冻结，以及 BGA layer / mine / invisible note 仍缺最薄 typed slot。
- 本轮仅完成文档规划与主线编排，无生产代码改动、无新增测试执行；代码与验证基线继续沿用主线同日 `788/788` 快照。
