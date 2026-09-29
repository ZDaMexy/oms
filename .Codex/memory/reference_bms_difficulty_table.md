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

## 曾造成“全部 Unrated”的共享列覆盖

converted star 与难度表使用不同 DTO，却写同一个 `BeatmapMetadata.RulesetData` JSON。whole-object overwrite 会让后写者删除前者未知字段，形成“星数重算→擦难度表→全 Unrated；难度表写回→擦星数→再次重算”的 ping-pong。

修复合同：所有共享列 DTO 带 `[JsonExtensionData]` 并 round-trip 未知字段；`IsEmpty` 必须把 ExtensionData 计入，不能把只含外来字段的 payload 置 null。

## 诊断与刷新边界

- 重启后仍 Unrated：查 persisted entries、原始文件字节 MD5 和共享列 clobber，不能把所有 Unrated 都归因为历史覆盖问题。退出重进才恢复时，检查整批通知是否送达、快照 JSON 是否更新及可见谱卡是否重绑；这不是当前合同要求用户重启的理由。
- 不要恢复 per-set revision bump：5万级库会触发成千 re-detach/scheduler task，用户已验证可冻结 UI 数分钟。
- table 整批持久化后通过全局 RealmAccess 发布谱面 ID；列表合并通知，读取最新持久化 JSON，更新快照后一次重筛并重绑可见卡片（只改 metadata 不会自动刷新 PrepareForUse 中生成的等级文字）。保持选中 ID；不引入实时 table lookup。
- 事件订阅须早于初始列表快照；先冻结快照再订阅会漏掉两者之间发生的批次。通知只携带 ID、处理时读取最新已提交 JSON，避免早到的旧 payload 覆盖后续写回。
- 原生 Realm 写通知在测试中仍可能产生集合更新；新增批次通知本身不增加逐 set 替换，不能将它描述为阻止所有原生通知或证明大库零卡顿。真实大库耗时需另测。
- write-back 使用注入的全局 `RealmAccess`；不要 new 第二个实例。
- 已被旧版本擦掉的 entries 需要一次 table mutation/refresh 重写，修复只能防后续覆盖。

测试必须双向证明：难度表写保留 foreign fields，star 写保留 difficulty fields。历史数字查 P1-H CHANGELOG。
