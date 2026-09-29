---
name: reference-bms-auto-offset
description: 自动调整偏移的继承入口、显示寿命和回放记录诊断
metadata:
  node_type: memory
  type: reference
---

# 自动调整偏移诊断

权威：[P1-C 合同](../../doc_md/subline/P1-C/TECHNICAL_CONSTRAINTS.md#自动调整偏移合同2026-09-29)、[状态](../../doc_md/subline/P1-C/DEVELOPMENT_STATUS.md)。

- 不仅查 BMS 配置：继承的 `BeatmapOffsetControl` 已有上一局自动谱面校准，`AudioOffsetAdjustControl` 是另一个手动全局建议入口。新增 style 应改唯一选择与既有 consumer，不能留下第二个自动开关。
- 视觉滚动位置不是全部：负偏移下 bar line/mine 的原真实时刻 fade 会提前消失；Alpha=0 后对象可能不再 Update，需在有界寿命内保持更新才能在偏移跨回时恢复。可判分 note/hold 仍以原判定控制消失，不能把它们改成视觉时间判定。
- 提前加载会扩大 pooled `AliveObjects`，进而影响 OD 空 POOR 和 auto-lane armed keysound 屏蔽。候选须恢复原加载边界；非 pooled compatibility 的 `Objects` 本来含未来对象，不能用同一过滤修改它。测试要明确 `IsInPool`，不要用 `AllHitObjects` 代替池化存活集合；`Playfield.Remove(drawable)` 返回 false 不代表没有执行移除。
- `ScoreInfo.GetRulesetData` 每次反序列化；不能只修改返回对象就声称已保存，也不能逐判定序列化增长轨迹。录制结束先刷新扩展数据再由 Player clone/import；回放按时间查询记录，不反向撤销 clamp 后的调整。
- ManualClock 一次跳转后 frame-stable clock 可能尚在分帧追赶，HCN tick 尤其容易使直接测试松键发生在错误时刻；先等待 drawable 的实际 clock 到达目标再注入按键。无等待造成的失败不是判定窗口问题。
- 上游 offset visual test 曾因已删除 Osu 类型被 csproj 排除；源码仍在不代表测试会执行。该 fixture 已改 Mania 对象恢复纳入，检查实际发现数及 TRX，不能只看 filter 命中命令。
