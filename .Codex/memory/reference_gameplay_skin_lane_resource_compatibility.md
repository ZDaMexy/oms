---
name: reference_gameplay_skin_lane_resource_compatibility
description: Skin V1 六类 lane-resource neutral snapshot、BMS→mania 候选链与 9K/14K 编址地雷
metadata:
  node_type: memory
  type: reference
---

# gameplay skin lane-resource compatibility 召回

权威状态与硬约束见 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md) / [PLAN](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md) / [CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)；作者当前格式见 [SKINNING](../../doc_md/other/SKINNING.md)。本文件只保存实现地雷。

## 第八切边界

- closed process-local field catalog 只含 note、LN head/body/tail、key up/down 六个逐 lane 资源字段；关联 semantic slot，但 declaration 不等于已验证 `Provide`。
- snapshot 必须绑定 exact immutable topology、防御性复制、按 logical lane/field catalog 确定性排序；缺项为 `Absent`，显式空资源名仍为 `Declared`。拒绝 null、unknown field、topology 外 lane 和 duplicate lane-field；安全字符串不展开资源名。
- 只读实际 `LegacyManiaSkinDecoder` / `BmsSkinDecoder` output。禁止从会合成缺失 mania bucket 的 `LegacySkin` production lookup 反推 presence。
- public legacy mania factory 只是跨 ruleset 程序集的 process-local CLR bridge，不是作者/plugin/package/manifest/script ABI。

## 候选矩阵

| 模式 | 固定顺序 |
| --- | --- |
| 5K | `[Bms] → Keys:6 full visual → Keys:5 key-only → canonical marker` |
| 7K | `[Bms] → Keys:8 full visual → Keys:7 key-only → marker` |
| 9K BMS/PMS | `[Bms] → Keys:9 → marker`；不得重复同一 Keys9 key-only candidate |
| 14K | `[Bms] → Keys:16 full visual → 同一 Keys:8 bucket 分别投影两个 deck → Keys:14 key-only → marker` |

- P2/CenterRightScratch full bucket 使用 global visual index，stable lane ID/action 不变。
- 14K deck bucket 使用 group-local visual index；同一个真实 Keys8 bucket 投影两次，因为 legacy decoder 不保留第二个 duplicate Keys8 section。Keys8 必须先于 Keys14，才能优先保留 scratch/deck-local presentation。
- marker 是 `Absent` 的未来 canonical authority 终点，不是已装载 `oms-simple` snapshot；candidate plan 不验证资源、不做 first-value resolution。

## 9K raw token 地雷

当前未版本化 `BmsLegacySkin` 对非 scratch 直接使用 raw logical lane index。5K/7K/14K 因 scratch 占 index 0，普通键碰巧是 `1..`；无 scratch 的 9K BMS/PMS 实际是 `0..8`。internal stable ID 仍为 `K1..K9`，不要把两者混成 ABI。

V1 canonical 作者目标 `1..9` 必须经显式格式版本、迁移和冲突诊断引入。绝不能同时静默接受 `0..8` 与 `1..9`：两套编号的 `1..8` 含义重叠。

## 2026-07-14 验证基线

- 新增 focused：shared 12/12、mania 6/6、BMS 29/29，合计 47/47。
- 扩回归：shared gameplay 223/223、provider 6/6、mania relevant 119/119 + decoder 7/7、BMS relevant 107/107 + fallback 104/104。
- core skin 57/62 仍为恢复基线同名 5 项；Release Rebuild 0 error / 20 warnings。
- 未接生产 `SkinManager`/renderer/`ISkin`，未改变程序化 `OmsSkin` 或 fallback authority，未触碰生产数据。
