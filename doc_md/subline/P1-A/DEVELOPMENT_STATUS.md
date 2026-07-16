# P1-A 当前状态：Skin V1、产品面与 release gate

> 最后更新：2026-07-17
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，稳定合同见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)。恢复与架构证据按需查 [恢复审计](../../other/SKIN_SYSTEM_RECOVERY_20260710.md) 和 [V1 架构审计](../../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

## 一句话状态

`SV1-0` 自动、schema 56 数据与用户实机 gate 已全部通过；`SV1-1` 的首个 managed `.osk` BMS Note/LN 产品纵切现已覆盖普通短键与长条 head/body/tail，四组件的自动、合同、安全和回退 gate 均已闭合，`V-001`～`V-004` 集中待验收。这只表示首个产品纵切自动闭环，不是 `SV1-1` 完成或产品交付；视觉待签收不再串行阻塞后续自动可证切片，工程依赖已允许进入下一门 `SV1-2`。

## 当前产品能力

- **恢复基线可用**：`.osk`/legacy mania、BMS F1 静态颜色/纹理/几何、选择链与程序化 `OmsSkin` 迁移 fallback 保持可用。
- **实现并自动验证的新增可见能力为 4**：selected managed package 可为 BMS 普通短键提供 `name-0`、`name-1`…编号帧动画，也可为 `NoteImage{lane}H/L/T`（含 `S`/`S2`）长条 head/body/tail 提供静态图和同规则 60 FPS 动画；普通短键静态 `NoteImage` 属恢复基线，不重复计数。
- **产品视觉签收为 0/4**：普通短键、长条 head、tail 与 body 分别登记为集中视觉项 `V-001`～`V-004`，用户尚未签收，因此只能称实现/自动 gate 通过，不能称已交付功能。
- **安全回落覆盖 Note/Head/Body/Tail**：selected 单组件缺失、损坏、空值、越权或超预算时逐组件回落；body 是不可 `Suppress` 的 critical 组件，资源失败才 `Inherit`，有效 body 即使 width 缺失或非法也继续使用同组件与默认 `0.5775`。坏 body/tail 都不能从低层裸同名纹理拼件，低层自己的完整组件仍可接管；tail 保持 optional 透明 protected fallback。异步换源只发布当前 revision 的完整结果。beatmap-local 优先目前只是注入式 provider-order 合同，不是真实 BMS `WorkingBeatmap` 能力。
- **整体仍不可用**：`SV1-1` 未完成，`SV1-2` 只有 early carrier，`SV1-3`～`SV1-7` 未实现；不能把首个纵切描述成 Skin V1 可用。

| 产品交付面 | 当前状态 |
| --- | --- |
| BMS 普通短键编号帧动画 | 实现/自动 gate 已过；`V-001` 集中视觉待验收，未交付 |
| BMS 长条头静态图/编号帧动画 | 实现/自动 gate 已过；`V-002` 集中视觉待验收，未交付 |
| BMS 长条尾静态图/编号帧动画 | 实现/自动 gate 已过；`V-003` 集中视觉待验收，未交付；透明链底不是作者 `Suppress` |
| BMS 长条身静态图/编号帧动画与安全宽度 | 实现/自动 gate 已过；`V-004` 集中视觉待验收，未交付；critical、不可 `Suppress` |
| gameplay slot 三态 | 普通短键/长条头/body critical 与长条尾 optional slot 已消费 `Provide/Inherit`；作者 `Suppress` 与其它 slot 未交付 |
| canonical `oms-simple.osk` fallback | 未交付；实际链底仍是程序化 `OmsSkin` |
| G1 文件夹导入/选择/原子重载 | 未交付；只有 schema/constructor 载体 |
| 统一 layout descriptor/solver | 未交付；现有 geometry provenance 不是有效 layout |
| shared ini codec/结构化诊断 | 未交付 |
| scene/event runtime 与 sandbox script | 未交付 |
| `oms-simple.osk` / `oms-complex.osk` / Authoring Kit | 未交付 |

## 当前实现事实

- `SkinManager` 当前皮肤后仍恒接程序化 `OmsSkin`；最终链底必须由只读、完整验证的 `oms-simple.osk` 接管。
- `BmsLegacySkin` 继续叠加解析 `[Bms]` 并保留 `[Mania]`；native BMS 普通短键与长条 head/body/tail 是当前仅有的真实 package 文件纵切。
- internal 26 项 semantic slot、neutral lane identity/topology/revision、config presence/provenance、六类 lane-resource resolution、event envelope/order 与 capability decision foundation 已落；它们仍是 process-local 合同地基，不是作者 manifest、完整 layout、生产事件 runtime 或 sandbox。
- geometry snapshot 仍只保存 parser 接受的来源事实；当前只有 `LongNoteBodyWidth` 进入唯一共享 scalar resolver，按 finite 且 `0 < width <= 1` 验证并对缺失/非法值逐字段回落 `0.5775`。其它 geometry 的 finite/range/screen-space validation 尚未进入统一 descriptor。
- G1 只保留 folder constructor 与 schema 56 字段；scanner、authority、containment、选择、删改和热重载无可信生产链。
- playfield 可读取当前皮肤 profile，但 gauge/combo/BGA 尚未消费同一 resolved descriptor；14K 四角四 BGA player 只是临时表现。
- mania/BMS 的共同目标仍是 neutral ini codec、scene/event ABI 与 sandbox；ruleset topology/layout adapter 分离，BMS 不继承 mania 具体 Drawable/transformer。

## 当前 gate

| 顺序 | Gate | 状态 |
| --- | --- | --- |
| 1 | schema 56 数据安全 | **通过**：异常 copy 已在保全后定点处置，OMS fixed-ID 已修正；不运行全局 orphan cleanup |
| 2 | 恢复基线实机 | **通过**：无外部皮肤、`.osk`、partial fallback、5K/7K/9K/14K、双皿与 mania/BMS 隔离均正常 |
| 3 | 文档与 memory 健康治理 | **完成**：只归位当前事实、未来步骤、稳定合同和历史；未改代码或产品 gate |
| 4 | managed `.osk` BMS 普通短键与长条 head/body/tail 视觉 | **`V-001`～`V-004` 集中待验收**；这是完成/release 声明门，不是后续开发开工门，不可复用静态恢复结论 |
| 5 | `SV1-1` 首个 Note/LN 产品纵切自动门 | **已闭合，视觉待验收**；四组件自动、合同、安全与回退 gate 通过，但不得写成 `SV1-1` 完成或交付 |
| 6 | `SV1-2` G1 安全存储与原子重载 | **下一门**；按 [当前计划](DEVELOPMENT_PLAN.md) 开始，`SV1-3`～`SV1-7` 仍未完成 |

## 最新验证

### 产品/runtime 与 gate 工具：截至 2026-07-17

- 产品自动验收扩为 **94/94**，并连续三轮全绿：在 Note/Head/Tail 矩阵上增加 body 静态图/60 FPS 连续编号帧、7K normal/scratch、14K `S2L`、A→B、有效 body + width 缺失/非法逐字段默认、坏 body 隔离、低层裸同名防串/完整组件接管、真实 Idle/Holding/Broken、异步首次挂载当前态与 HCN regrab。beatmap-local 项仍只是注入式 provider-order fixture。
- 合并态 BMS skin/runtime focused **326/326**，BMS full **1456/1456**；`osu.Desktop.slnf` Release **0 error / 11 known warnings**。保留 9 条 MessagePack 3.1.3 `NU1902` 与 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。
- 改动文件的定向 format verify 通过；唯一排除的是文件内既存 `IDE1006` 命名债。独立终审 blocker/major **0/0**。
- 本次只改 BMS ruleset 内的 managed note provider、renderer host 与测试，没有改 shared `osu.Game` skin ABI、mania compatibility 或 fallback authority，因此未另跑 core/mania 产品测试；Release 已编译 core、mania/BMS 与两个 test project。普通短键的 generator/staging/scene/runner safety 既有 **53/53** 结论未被本切重跑或冒充本切数字。
- root generator 实跑 **1/1**，两个 staged `.osk` 与确定性原件 SHA-256 一致；staging/reparse/无关文件安全用例与 exact 参数/路径/非递归清理用例均已包含在 **53/53**。非法/缺值 exact CLI 均 exit 1，新增 AppData host 残留为 0。
- 测试只使用隔离临时存储；生产 Realm、`chartskin/`、用户皮肤目录和网络零访问、零写入。按用户要求本切未启动 GUI、未开窗或操控桌面，自动证据和用户视觉签收不互相冒充。
- 普通短键与长条 head/tail/body 视觉仍待用户集中确认；当前纵切不包含 key、mania compatibility、完整 layout/G1/scene/script、作者 `Suppress` 或整包原子重载。tail 的透明 protected fallback 只是迁移链底表现，不是作者 `Suppress`。

### 文档：2026-07-16 健康治理

- 仅治理 `doc_md` 与 memory 的职责边界和低噪声结构；未修改 runtime/code，未运行产品测试或 Release，2026-07-15 仍是当前产品证据。
- 子线当前态、计划、约束与历史已重新归位；最终相对链接与 whitespace 检查结果记录在本次 [CHANGELOG](CHANGELOG.md)。

### 手工门素材：2026-07-16

- 新增 OMS 自生成、确定性的 good/broken `.osk` 与静音 7K `.bme` 生成器，以及隔离 exact-scene 自动可视预检入口；它们只提供可复现输入和自动预检，不改变 runtime 或用户视觉 gate 结论。
- 原始 26 项中的 beatmap-local 是 provider-contract fixture，不是 `WorkingBeatmap` / `chartbms/` 集成，因此手工素材不得宣称 beatmap-local 已通过。

## 当前风险

- schema 56 的四个无 authority orphan blob 已保全并暂留；不得把本次定点处置当作 scanner 批量清理先例。
- 当前真实 package 纵切只覆盖 BMS 普通短键与长条 head/body/tail；单组件安全替换不等于整包/全 playfield 同帧原子 reload。
- 成功 preparation cache 目前不感知同一 `BmsLegacySkin` 实例内的原地 source revision 变化；现状不会混合或发布过期 material，而是安全保留旧视觉/回落并要求重建实例。`SV1-2` 必须把这项作为原子 reload 风险处理。
- 真实 BMS beatmap-local 尚无逐谱作者格式和 `WorkingBeatmap` producer；实现它会新增 core 扩展点与公开 sidecar 合同，必须先由产品冻结范围。
- runtime 图片预算不等于 `.osk` importer 的压缩比/zip-bomb gate；G1 仍须独立实现。
- 程序化 `OmsSkin` 在 `oms-simple` parity/完整性/恢复 gate 前不能删除，但也不能写成最终产品能力。
- 除 `LongNoteBodyWidth` 的单字段 scalar policy 外，parser provenance 仍不等于 validated config/layout；极端几何仍可能使 playfield、gauge/combo 与 BGA 脱节。
- 9K raw lane token 与 V1 canonical 作者 token 存在重叠迁移风险，必须版本化处理，不能静默双 alias。
- topology/event/capability foundation 尚无 production lifecycle、payload、manifest、授权存储或 runtime gate。
- sparse 7K/9K keymode 与 lane keysound timeline 上界仍分别由 P1-K/P1-J 修复和验证；不得由 skin/layout 再猜一遍。
- 皮肤异常期归档只能定点取证，禁止整包 cherry-pick/apply。

## 下一检查点

1. 将普通短键与长条 head/tail/body 的观感、选择切换和 selected 坏包回落保持在[集中视觉清单](../../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)的 `V-001`～`V-004`，等待统一用户反馈；不得把自动测试写成视觉签收。
2. 进入 `SV1-2` G1 安全存储与原子重载，优先闭合同一 package revision 的扫描、authority、完整 preparation、成功 cache 失效和实例切换；当前同一 `BmsLegacySkin` 实例原地改源需要重建实例的限制必须在该门消除或显式冻结。
3. 剩余 optional slot 不再沿私有逐件 C# provider/display 扩张，留给后续 shared scene/runtime 接管。只有视觉结论实际决定下一实现时才暂停；期间保持 nullable `ISkin`、程序化 `OmsSkin`、当前 fallback authority 与 Skin V1 未交付状态不变。
