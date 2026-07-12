---
name: reference_skin_schema56_inventory_20260713
description: Skin V1 SV1-0 的 schema 56 只读取证、失效皮肤类型地雷与迁移 stop/go
metadata:
  node_type: memory
  type: reference
---

# 2026-07-13 schema 56 皮肤数据安全门

权威证据：[SV1-0 脱敏报告](../../doc_md/other/SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)；当前状态与执行顺序仍看 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md) / [PLAN](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md)。

## 只读取证方法

- 先确认 OMS/osu 进程关闭，记录生产 `client.realm` 的 length/mtime/SHA-256。
- 不用 `RealmAccess` 打开生产库；先做逐字节副本，再只用 Realm SDK dynamic + read-only 打开副本。
- 结束后重算生产证据；本次 before/after 完全相同。
- 文档只记录 authority、相对/脱敏记录和数量，不记录用户皮肤名、绝对路径或内容 hash。

## 新地雷

- schema/path 正常不代表 `SkinInfo` 可实例化。本次 3 条记录中 folder-backed/external 均为 0，但两条 `InstantiationInfo` 指向恢复树已删除的 `BmsOmsReferenceSkin`。
- fixed-ID protected OMS 记录会被 `SkinManager` 构造时静默重写；managed 记录不会。不要用一次普通启动把前者改绿后宣称数据迁移完成。
- `SkinInfo.CreateInstance()` 对解析失败类型仍回落 `TrianglesSkin`。对 BMS `.osk` 而言这会掩盖失效类型并绕开预期的 `BmsLegacySkin`，也违反 OMS 最终不依赖上游默认视觉的产品方向。
- 处理顺序：重新备份 → 保全 managed hash-backed 内容 → 用户选择重导入/保留/移除 → 独立定点迁移与实机验证。scanner 不得自动清理。

## 当前 gate

`SV1-0` 数据 gate 为 STOP；实机 gate 未执行；`SV1-1` 三态合同未开始。恢复 focused 自动门无新回归。
