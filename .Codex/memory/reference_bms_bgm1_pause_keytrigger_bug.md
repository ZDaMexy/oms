---
name: reference-bms-bgm1-pause-keytrigger-bug
description: 已解决的转谱-mania 按键误播 BGM：根因、修复与隐藏发声诊断法
metadata:
  node_type: memory
  type: reference
---

# 转谱 mania 按键误播 bgm1（已解决）

权威合同：[P1-J CONSTRAINTS](../../doc_md/subline/P1-J/TECHNICAL_CONSTRAINTS.md#bmsmania-音频合同)；当前边界见 [P1-J STATUS](../../doc_md/subline/P1-J/DEVELOPMENT_STATUS.md)，本页仅保留误播根因与定位方法。

历史症状：按最左键触发 bgm1，多按重叠，暂停不停；不是当前版本仍有此故障的结论。

## 真根因

mania `Column.OnPressed` 的默认手动反馈会通过 `GameplaySampleTriggerSource` 选择本列候选对象的 `Samples`。转谱 BGM/scratch sample-only 对象被放在可玩列且曾把键音放进 `Samples`，因此按键反馈绕过 shared store 播出 BGM；重叠和暂停漏播都由这条独立 sample pool 解释。这里的候选不总是未来第一个对象，不能用这段故障简称替代当前选音合同。

## 修复合同

- `BmsConvertedBgmSampleHitObject` 与 scratch sample-only 的 `Samples` 必须为空。
- 实际自动发声只经 `KeysoundSample/KeysoundId` + `BmsKeysoundStore`。
- 可玩 key note/LN head 可保留自身 samples；不要为修此 bug 全局禁用 mania key feedback。自动键音开启时仅对 hosted BMS store 抑制 column feedback，是独立的声音设置合同，不改变普通 mania。

## 诊断教训

- store 和 hit-object 埋点都没有记录时，在最底层 `PoolableSkinnableSample.Play()` 按文件名过滤并抓 stack trace；这次一栈定位到 `GameplaySampleTriggerSource`。
- 可用“静音 store 的 BGM”做隔离：仍能听见即证明是非 store 路径。
- orphan-on-reuse、LN head、Track preview、谱面槽粘连均曾被验证为错误方向，不要重走。

相邻但独立：长 one-shot 的暂停保位、手动转谱 LN 发声路由和 50k dense 诊断。前两项当前实现与人工边界只查 P1-J STATUS，不沿用旧“手动 store 缺口”；pause/resume 与 seek 清旧声不能混为一谈。相关诊断见 [[reference_bms_keysound_chain]]，误播修复历史按 2026-06-08 查 [P1-J CHANGELOG](../../doc_md/subline/P1-J/CHANGELOG.md)。
