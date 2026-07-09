---
name: reference-bms-bgm1-pause-keytrigger-bug
description: 已解决的转谱-mania 按键误播 BGM：根因、修复与隐藏发声诊断法
metadata:
  node_type: memory
  type: reference
---

# 转谱 mania 按键误播 bgm1（已解决）

症状：按最左键触发 bgm1，多按重叠，暂停不停。

## 真根因

mania `Column.OnPressed` 会通过 `GameplaySampleTriggerSource` 播放本列“下一个对象”的 `Samples`。转谱 BGM/scratch sample-only 对象被放在可玩列且曾把键音放进 `Samples`，因此按键反馈绕过 shared store 播出 BGM；重叠和暂停漏播都由这条独立 sample pool 解释。

## 修复合同

- `BmsConvertedBgmSampleHitObject` 与 scratch sample-only 的 `Samples` 必须为空。
- 实际自动发声只经 `KeysoundSample/KeysoundId` + `BmsKeysoundStore`。
- 可玩 key note/LN head 可保留自身 samples；不要为修此 bug 全局禁用 mania key feedback。

## 诊断教训

- store 和 hit-object 埋点都没有记录时，在最底层 `PoolableSkinnableSample.Play()` 按文件名过滤并抓 stack trace；这次一栈定位到 `GameplaySampleTriggerSource`。
- 可用“静音 store 的 BGM”做隔离：仍能听见即证明是非 store 路径。
- orphan-on-reuse、LN head、Track preview、谱面槽粘连均曾被验证为错误方向，不要重走。

相邻但独立：长 one-shot BGM resume、转谱 LN 池化和 50k dense。完整键音合同见 [[reference_bms_keysound_chain]]，历史见 P1-J CHANGELOG 2026-06-08。
