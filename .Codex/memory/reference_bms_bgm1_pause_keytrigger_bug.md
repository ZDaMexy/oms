---
name: reference-bms-bgm1-pause-keytrigger-bug
description: "RESOLVED (2026-06-08): converted-mania 'press key1 -> bgm1 / key-mash long-audio overlap / pause doesn't stop'. Real root cause = mania key-press sample feedback (GameplaySampleTriggerSource) playing the converted BGM/scratch sample-only objects' Samples; fix = empty their Samples (they play via the store's KeysoundSample). Includes the full diagnostic journey and the discarded wrong theories."
metadata:
  node_type: memory
  type: reference
  originSessionId: 20fbcdec-71c0-4b92-b255-775b78ab861a
---

# ✅ RESOLVED：转谱-mania「按 key1 触发 bgm1 / 胡乱按键长音重叠 / 暂停不停」

> 2026-06-07 用户实测移交、2026-06-08 确诊+修复+用户多谱实测确认。权威：P1-J CHANGELOG 2026-06-08 / STATUS / TECHNICAL_CONSTRAINTS #11。键音链路细节见 [[reference-bms-keysound-chain]]。

## 🎯 真根因（调用栈铁证）= mania 按键音效反馈 `GameplaySampleTriggerSource`

mania `Column.OnPressed`（`osu.Game.Rulesets.Mania/UI/Column.cs:192`）**每次按键都调 `GameplaySampleTriggerSource.Play()`**（按键音效反馈）。它播放**本列 `HitObjectContainer` 中"下一个对象"的 `Samples`**（`GetMostValidObject` 按 StartTime 取最近未判定对象 / 回退首个），用它**自己一池非循环、不受 store 暂停管**的 `PausableSkinnableSound`（`max_concurrent_hitsounds` 轮转）。

转谱时 **BGM/scratch sample-only 对象被钉在可玩列**（BGM `BmsConvertedBgmSampleHitObject`→column 0；scratch→其锚定列）、**且其 `Samples` 装着键音**（bgm1 等）。于是：按 **key1（最左=column 0）** → 反馈取到 column 0 的 BGM 对象 → 播 **bgm1**；反复按 → 反馈池轮转**重叠**；非循环 + 完全绕开 store → **暂停不停**。**一个根因解释全部现象**，也解释了为何一切 store/hit-object 埋点都抓不到（这条路径既不经 `BmsKeysoundStore` 也不经 `DrawableHitObject.PlaySamples`）。

## ✅ 修复（已落地、用户多谱实测确认）

`BmsToManiaBeatmapConverter`：把 `BmsConvertedBgmSampleHitObject` 与 `BmsConvertedScratchSampleHitObject` 的 **`Samples` 置空**（`new List<HitSampleInfo>()`），保留 `KeysoundSample`/`KeysoundId`。这些 sample-only 对象本就经 shared `BmsKeysoundStore` 用 `KeysoundSample` 自动发声，`Samples` 对其实际播放多余、只会被按键反馈错误取用。置空后自动 BGM 照常播/暂停，按键反馈再取不到 bgm1。**改动只在转谱器、不碰 osu 核心。** 可玩 KEY note / LN head 仍保留 `Samples`（反馈播下一个真实 KEY 音是 BMS-like、可接受）。

**验证**：用户真实 app「按 key1 不再出 bgm1」+ 其他原本同问题谱面一并修好；日志佐证（`[KEYHIT]` 含 col=0 全是鼓/water、无 bgm1）。回归守卫：`BmsToManiaBeatmapConverterTest` 断言 BGM/scratch `Samples` 为空、键音在 `KeysoundSample`。转谱 **19/19**、BMS **871/871**、Release **0 错**。git：基线 HEAD=`2157f7c`（tap-note→store 路由）；最终生产改动只有转谱器 `BmsToManiaBeatmapConverter.cs`（BGM/scratch `Samples` 置空）+ 其回归守卫测试 `BmsToManiaBeatmapConverterTest.cs`，诊断埋点 + orphan 尝试已全部回退/删除、0 残留。**注：该修复 + 本次文档梳理截至 2026-06-09 仍在工作区未提交。**

## 🔻 本会话先后试过的错误方向（均已回退/否定，留作认知演变 + 防止重蹈）

1. **store「脱挂留响 orphan-on-reuse」假设**（最花时间的错路）：以为 per-WAV cut 复用通道时 `PlaySingleSample` 重设 `Samples`→`SkinnableSound.updateSamples` 对非循环样本不 Stop、把旧实例退回池仍响→重叠+暂停漏。曾在 `PlaySingleSample`/数组 `Play` 加 `Stop()`，**用户实测无效、已回退**。orphan 机制是否真实存在属**独立未验证遗留**，与本 bug 无关。
2. **转谱 LN head 经 mania 一次性致重叠**假设：**否定**——埋点 `[ONESHOT]`（`DrawableHitObject.PlaySamples`）实测 =0，本谱按键期间根本无 LN head 发声。
3. **谱面键音/BGM「粘连」/ 跨槽同文件冲突**假设：**否定**——三重重解析 `macchitodoncho_SP_HYPER.bms`：bgm1=`#WAVYX` 全谱仅 `#00101:YX` 一次、BGM-only、无任何 KEY/LN 通道引用 YX、无"同物理文件不同槽一 BGM 一 KEY"的冲突（全谱 0 个）。解码→转谱链通读确认**完全正确**（`AutoPlay`→`BmsBgmEvent`；`buildLaneKeysoundTimelines` 绝不收 BGM 层）。
4. **Track/preview 泄漏**假设：**否定**——本谱无 master AudioFile（`detectFullMusicFile` 要非键音≥1MB、bgm1 是键音被排除；无 `#PREVIEW`；有物理 `preview.ogg` 但 importer 不引用）。
5. **「长 BGM 当一次性样本无法 resume」**：这是**另一条独立、仍未修的问题**（暂停 StopAllPlayback 停掉后、恢复时一次性样本无法保位续播→截断，native+转谱通用），与本 key-trigger bug 无关，后置。

## 🧭 诊断方法教训（关键，下次省时间）

- 用户多次坚称"按 key1 就播 bgm1"而代码/谱面看似不可能时——**别再用"按规范不可能"打发，要用数据终结对撞**。
- 当某发声路径既不经已知 store、也不经 hit-object `PlaySamples` 时，**直接在最底层发声点 `osu.Game/Skinning/PoolableSkinnableSample.Play()` 加 `Environment.StackTrace` 探针**（按文件名过滤 bgm）是定位"隐藏发声路径"最快的手段——一条调用栈直接指认 `GameplaySampleTriggerSource`。
- 有效流程：按来源标签分层埋点（store / 一次性 / 反馈 / BGM-auto / KEY-hit，带列号）+ **哨兵静音隔离实验**（`.bms_mute_bgm` 把 store 的 bgm 静音→若仍听到 bgm1 则证明走非 store 路径）。哨兵 gate（`F:\oms\.bms_keysound_diag`）→ `%TEMP%\oms_bms_keysound_diag.log`。这些诊断器（`osu.Game/Utils/BmsKeysoundDiagnostics.cs` 等）用完已全部删除。

## 历史复现事实（用户实测，留档）

- 谱面：`D:\beatoraja0.8.8-jre-win64\BMS\added\[Juka_Box]macchitodoncho\macchitodoncho_SP_HYPER.bms`，转谱-mania（也在 BMS 原生侧有相关现象）。bgm1.ogg = 44s，`#WAVYX`，仅 `#00101:YX`（channel 01 BGM 自动层）一次。
- 决定性实测：只按 key1（最左列）→ 反复触发 bgm1、多按重叠、暂停不停；静音 store 的 bgm 后仍出 bgm1（证明非 store 路径）；调用栈定位 `GameplaySampleTriggerSource`。
- （早先 2026-06-07 的 F1–F4 表述基于"orphan/通道偷取"误判框架，已被本次真根因取代，不再适用。）

## 仍开放的相邻遗留（非本 bug）

- **长 BGM resume 截断**（暂停后恢复续播）：需把长 BGM 改走**时钟驱动 Track**（保位 pause/seek/resume），一次性 store 样本做不到。native + 转谱通用。后置。
- **per-WAV cut orphan-on-reuse**：traced 但未验证、已回退，后置（见上错误方向 1）。
- ~~BGM/scratch autoplay prewarm 缺口~~ **已补（2026-06-10/11）**：预热改为额外按 `IHasManiaKeysound.KeysoundSample` 路径执行（BGM/scratch `Samples` 空、键音仅此可达），且自 2026-06-11 起对**玩家模式一律执行**（游玩中冷解码实测触发 ~220ms 阻塞 gen2 冻结）；**红线不变：不得把键音放回 `Samples`**（否则复现本 bug，见 CONSTRAINTS #11）。详见 [[reference-bms-keysound-chain]]。
- **转谱键音重复（per-WAV cut 跨/同路径）**：tap-note 部分已转正（HEAD 2157f7c），LN 部分仍后置（须池化嵌套头）。见 [[reference-bms-keysound-chain]]。
