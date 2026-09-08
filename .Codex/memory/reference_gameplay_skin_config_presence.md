---
name: reference_gameplay_skin_config_presence
description: legacy decoder accepted provenance、空声明、数值/数组兼容与已删 factory 的诊断边界
metadata:
  node_type: memory
  type: reference
---

# Legacy configuration presence 地雷

权威为 [P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md) 的“ini 兼容约束 / Legacy compatibility输入事实”；当前生产接线读 [STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)。已删 snapshot/factory 的历史按 [CHANGELOG](../../doc_md/subline/P1-A/CHANGELOG.md) 搜 `accepted` / `snapshot`，不得据本页重建旧类型或第二套 public catalog。

## 来源事实与验证是两层

- `GameplaySkinConfigurationDeclaration<T>` 默认 Absent；显式 false、0、空字符串、空 bucket 均为 Declared。它不表示有效配置、slot Provide 或 Suppress，也不 clone/freeze 任意 T；跨 neutral 边界只传 immutable bucket marker。
- `LegacySkin.lookupForMania()` 会合成缺失 bucket 的默认对象。若据此推导 presence，会把未声明内容误作 Provide 并挡住 fallback。必须用 decoder-time accepted sidecar，不能从后续 mutable dictionary 或手工构造对象反推。
- Mania 没有 `Keys:` 是 Absent，只有 Keys 的空 bucket 仍 Declared。mixed stage 可表达 1～20 总列，不能拿等分 dual 的 `AvailableVariants` 拒绝 11/13/15/17/19。
- BMS `[General] Keymodes:` 只是 metadata；有效 `[Bms] Keymode:` 才建 bucket，9K BMS/PMS 分开。坏字段被跳过不代表 bucket 缺失。
- provenance 只记 parser 接受事实，不是安全权限。finite/range、文件路径、解码预算与资源有效性在真实 layout/material/package 边界验证，不能由 Declared 推定已安全。

## 修改 legacy parser 前先找这些复现条件

| 输入或操作 | 必须区分的既有行为 |
| --- | --- |
| pending-before-Keys、malformed Keys、duplicate Keys | 坏 Keys 可沿用旧 current config；重复 bucket 后续字段可能写入 discarded config。不要借 provenance 修改这些兼容行为，也不把它们提升为 public codec 规则。 |
| malformed colour | `flushPendingLines()` 可在 clear 前退出并阻断同 section 后续；fixture 锁事实，不代表应推广这种行为。 |
| NoteBodyStyle | 使用原 `Enum.TryParse`，命名/未命名数字、+2/02、逗号组合可能被接受；不能在 provenance 层加 Enum.IsDefined 清洗。malformed 不覆盖上次 accepted。缺声明时 production 按 General Version < 2.5 取 Stretch，否则 RepeatBottom。 |
| BMS geometry | Float/InvariantCulture 接受指数、-0、NaN、Infinity，以及 .NET 8 overflow/underflow 结果；这些仍待 layout 验证。thousands comma、hex、suffix 等坏值不声明且不覆盖上次 accepted。 |
| BMS composite enum key | 逗号组合可改写 compatibility dictionary，却不等于 ordinal exact-key provenance；numeric key 还受首字符 char.IsLetter 限制。colour 的无 reader sidecar/factory 已删除，不因旧规格恢复。 |
| decode 后字典/数组 mutation | 不得伪造或擦除 decoder 接受事实；测试应区分 public compatibility view 与 accepted provenance。 |

Public section 由唯一 shared codec 的严格 malformed/duplicate 合同处理；它与 legacy 兼容事实分层，不能另起 tokenizer。

## 数组、颜色与资源的反直觉处

- Mania `ColumnLineWidth` 容量 Keys+1 且不缩放；`ColumnSpacing` 容量 Keys-1；`ColumnWidth/ExplosionWidth/HoldNoteLightWidth` 容量 Keys，后四种存 decoder ×1.6 值。boundary/gap/source index 不是 stable lane ID。
- 短数组尾为 Absent；空/invalid/trailing-empty 项按原 parser 接受为 Declared(0)；超长尾忽略。重复短数组逐 index 覆盖，未覆盖尾保留旧 declaration。Keys=1 时 spacing 容量为 0。不要改成 field-level presence 或先计算半 spacing/renderer scale。
- exact `Colour{n}/ColourLight{n}` 的 n 是范围内 1-based ASCII，前导零/符号/大小写变体不属于 closed accepted key。RGB 补 alpha=255，RGBA 含 alpha 0 原样保存；renderer 的 doubled/zero-alpha 处理不是 decoder 值。
- bucket-global Lighting/Stage/Hit 资源不具有 lane identity；资源语义及映射去 [[reference_gameplay_skin_lane_resource_compatibility]]。把 global key 逐 lane 扩展会制造错误 provenance。
- `SplitKeyVal()` 只切首个冒号并 trim；`StageHint:` 是显式空字符串，其余冒号/引号仍属原 compatibility value。空资源应进入后续 materializer 诊断，不得折叠成 Absent/Inherit。
- `LongNoteBodyWidth` 的 finite、0 < width ≤ 1 与 fallback 由现行 LN/layout 合同定义；不要从 raw accepted geometry 直接渲染，也不在 memory 重复常数表。

诊断只输出 Absent/Declared、公共字段与稳定 reason；不得输出作者值、资源路径或 payload。lane/source 映射见 [[reference_gameplay_skin_lane_identity]]，公共三态与材料见 [[reference_gameplay_skin_codec_material]]。
