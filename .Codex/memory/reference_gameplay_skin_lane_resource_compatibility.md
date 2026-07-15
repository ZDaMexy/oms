---
name: reference_gameplay_skin_lane_resource_compatibility
description: Skin V1 六类 lane-resource snapshot、BMS→mania 候选链、逐字段 resolution/revision owner、package-scoped materialization/source isolation/async reload 与 9K/14K 编址地雷
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
- marker 是 `Absent` 的未来 canonical authority 终点，不是已装载 `oms-simple` snapshot；第八切 candidate plan 自身不验证资源、不做 first-value resolution。第九切另由 internal provider adapter 消费该 plan，不能把两层混写成 production loader。当前 native BMS 普通短键是真实文件的首个窄例外，但没有把整套 candidate plan 或 `oms-simple` marker 生产化。

## 9K raw token 地雷

当前未版本化 `BmsLegacySkin` 对非 scratch 直接使用 raw logical lane index。5K/7K/14K 因 scratch 占 index 0，普通键碰巧是 `1..`；无 scratch 的 9K BMS/PMS 实际是 `0..8`。internal stable ID 仍为 `K1..K9`，不要把两者混成 ABI。

V1 canonical 作者目标 `1..9` 必须经显式格式版本、迁移和冲突诊断引入。绝不能同时静默接受 `0..8` 与 `1..9`：两套编号的 `1..8` 含义重叠。

## 第十七切 decoder-time provenance 地雷

- shared legacy mania 只接受区分大小写的 `NoteImage{0-based column}[H|L|T]` 与 `KeyImage{column}[D]`，column 必须是当前 `Keys` 范围内的 canonical ASCII 十进制；前导零、符号、Unicode lookalike、越界与其它后缀仍可按既有 parser 留在 compatibility dictionary，但不能进入 private sidecar。
- native `[Bms]` 只把 note、LN head/body/tail、key up/down 六个 exact prefix/suffix 组合写入 sidecar。raw lane token 服从既有 regex 的 `\d+`、`S`、`S2` 并且不做 normalization；因此 `01` 或 Unicode decimal token 即使被 decoder 接受也不会冒充 projection 查询的 ASCII `1`。`LaneBackground`、`LaneDivider` 与 stray suffix 等 regex-compatible key 继续只存在于 `ImageLookups`。
- 两侧 factory 均只读 decoder-time accepted sidecar。public dictionary 在 factory 前被 forge、overwrite、remove、clear、replace 或 late-add 都不能改变 provenance；显式空值与 valid duplicate-last 仍保留。invalid enum/composite/index/token/null 必须在任何 sidecar/compatibility 双写前原子拒绝。
- 这仍是 process-local declaration bridge，不是安全、文件或渲染 authority。文件存在性、containment、解码预算、materialization、component ownership、slot `Provide` 与 fallback winner 均属于后续层。

## 第九切 resolution / revision-owner 地雷

- selected-package factory 只发出 canonical marker 前的 candidates 并保持 plan 顺序；完整层级仍由 caller 显式组合 beatmap-local → selected candidates → ruleset resources → canonical。factory 绝不能制造名为 `oms-simple` 的 provider。
- 缺 bucket/field 直接 `Inherit` 且不得调用 materializer；显式空字符串仍是 declaration，必须交给 materializer 做基础验证。ini declaration 永不产生 `Suppress`，取消异常必须传播。
- source-aware reference 至少区分 source、Keys、stable lane ID、field 与 raw resource name。同一 raw name 在 BMS/mania 或不同 bucket 下可以有不同结果，不能只按字符串名跨 authority 共用；resource name 不得进入稳定诊断、JSON 或安全字符串。
- materializer 返回前必须由一个 revision-scoped owner 取得 component 所有权并完成基础验证。winner 与被 outer validator reject/throw 的 component 都只是 resolver/consumer 借用，延迟到 owner dispose 回收；resolver/provider 不单独 dispose。
- active/provisional owner 不能交叉：失败 reload 只 dispose 新 provisional owner，旧 active owner 继续存活；成功原子替换先 detach superseded consumer，再 dispose 旧 owner；teardown 同样先 detach 后 dispose。第九切只有 internal interface/fake owner fixture，没有 concrete production owner、Drawable parenting/thread affinity、缓存或 atomic reload。

## 首个 production ordinary-note 纵切地雷

- 不能分别向聚合 skin 查询 config 和 texture：声明可能来自 selected package、同名 texture 却来自 ruleset/内置 package，形成跨 provider 拼接。declaration、frame discovery 与 texture decode 必须绑定同一个精确 package revision。
- `GetTexture()` 之后才检查输入大小、尺寸/像素或累计预算已经太晚；先读受限元数据并做 pre-decode gate，解码后再核对实际值。runtime cap 也不等于 importer 的总解压字节、解压比或 zip-bomb gate。
- Realm 的 hash-backed package 要先冻结不可变文件名→内容身份快照，再由该 revision 独占 private resource cache；大小写重复、路径越界、身份冲突或缺 blob 均只让对应 slot `Inherit`，不能从另一 package 补齐。
- generic 同步 reload 会把文件 IO/图片解码带回 update thread。专用异步 note host 应在新视觉完整就绪前保留旧 visual 或 critical fallback，用 generation/cancellation 阻止过期结果发布，并释放所有未采用结果。
- 当前保证是 per-component publication，不是 package atomic reload；ini/scene/script/所有素材共同验证后一次切换仍属 `SV1-2`。
- `Box` 继承 `Sprite`；测试若只用 `drawable is Sprite` 会把程序化 fallback 误判为用户贴图，必须验证 source-bound 类型/纹理身份或明确的宿主状态。

## 2026-07-15 验证基线

- 第十七切 focused：shared old+new 21/21、mania 6/6、BMS old+new 40/40；新增 provenance fixture 分别为 legacy mania 9/9 与 BMS 11/11。
- BMS full 1157/1157；mania full 827/831 的 4 项仍为同名 HoldNote auto-frame 恢复基线；core skin 57/62 仍为同名 5 项恢复基线。
- `osu.Desktop.slnf` Release Rebuild 0 error / 20 warnings；保留 9 条 MessagePack `NU1902` 重复显示及 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。
- 该组数字是第十七切当时基线；其后 native BMS 普通短键已成为首个 package-scoped production 窄纵切。完整 candidate plan、其它 lane-resource、`oms-simple` authority 与 nullable `ISkin` ABI 仍未切换，也未访问或写入生产数据。
