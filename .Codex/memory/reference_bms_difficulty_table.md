---
name: reference-bms-difficulty-table
description: BMS 难度表持久化、共享 RulesetData clobber 地雷与大库刷新边界
metadata:
  node_type: memory
  type: reference
---

# BMS 难度表召回

权威当前态：[P1-H STATUS](../../doc_md/subline/P1-H/DEVELOPMENT_STATUS.md)；详细约束/历史位于 P1-H。

## 主链

- manager 管理本地/HTTP bmstable source，写回 persisted difficulty-table entries。
- consumer 从 persisted metadata 分组为表→等级；无条目进入 Unrated。
- osu.Game 侧难度表 badge 只读 ExtensionData，不用不完整 DTO 写回。

## “全部 Unrated”真根因

converted star 与难度表使用不同 DTO，却写同一个 `BeatmapMetadata.RulesetData` JSON。whole-object overwrite 会让后写者删除前者未知字段，形成“星数重算→擦难度表→全 Unrated；难度表写回→擦星数→再次重算”的 ping-pong。

修复合同：所有共享列 DTO 带 `[JsonExtensionData]` 并 round-trip 未知字段；`IsEmpty` 必须把 ExtensionData 计入，不能把只含外来字段的 payload 置 null。

## 诊断与刷新边界

- 重启后仍 Unrated：查 persisted entries、MD5 和共享列 clobber。退出再进入 Song Select 后恢复：属于 carousel 深层 link staleness。
- 不要恢复 per-set revision bump：5万级库会触发成千 re-detach/scheduler task，用户已验证可冻结 UI 数分钟。
- mid-session table 变化当前通过退出/重进 Song Select 或重启反映；proper future fix 是内存 MD5 index + one-shot refilter。
- write-back 使用注入的全局 `RealmAccess`；不要 new 第二个实例。
- 已被旧版本擦掉的 entries 需要一次 table mutation/refresh 重写，修复只能防后续覆盖。

测试必须双向证明：难度表写保留 foreign fields，star 写保留 difficulty fields。历史数字查 P1-H CHANGELOG。
