# P1-A 当前状态：Skin V1、产品面与 release gate

> 最后更新：2026-07-16
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，稳定合同见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)。恢复与架构证据按需查 [恢复审计](../../other/SKIN_SYSTEM_RECOVERY_20260710.md) 和 [V1 架构审计](../../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

## 一句话状态

`SV1-0` 自动、schema 56 数据与用户实机 gate 已全部通过；`SV1-1` 首个玩家可见纵切已把用户选中的 managed `.osk` BMS 普通短键编号帧动画接入真实 gameplay，自动 gate 已通过、用户实机仍待单独确认。2026-07-16 文档与 memory 健康治理已完成，未改变代码、产品合同或 gate；下一道门只闭合该动画实机确认，不启动新组件。

## 当前产品能力

- **恢复基线可用**：`.osk`/legacy mania、BMS F1 静态颜色/纹理/几何、选择链与程序化 `OmsSkin` 迁移 fallback 保持可用。
- **Skin V1 新增可见功能为 1**：selected managed package 可为 BMS 普通短键提供 `name-0`、`name-1`…编号帧动画；静态 `NoteImage` 属恢复基线，不计新增功能。
- **安全回落已覆盖本纵切**：selected 单槽缺失、损坏、越权或超预算时逐组件回落；跨 package 不拼接资源，异步换源只发布当前 revision 的完整结果。beatmap-local 优先目前只是注入式 provider-order 合同，不是真实 BMS `WorkingBeatmap` 能力。
- **整体仍不可用**：`SV1-1` 未完成，`SV1-2` 只有 early carrier，`SV1-3`～`SV1-7` 未实现；不能把首个纵切描述成 Skin V1 可用。

| 产品交付面 | 当前状态 |
| --- | --- |
| BMS 普通短键编号帧动画 | 自动 gate 已过；用户实机待确认 |
| gameplay slot 三态 | 普通短键 critical slot 已消费 `Provide/Inherit`；作者 `Suppress` 与其它 slot 未交付 |
| canonical `oms-simple.osk` fallback | 未交付；实际链底仍是程序化 `OmsSkin` |
| G1 文件夹导入/选择/原子重载 | 未交付；只有 schema/constructor 载体 |
| 统一 layout descriptor/solver | 未交付；现有 geometry provenance 不是有效 layout |
| shared ini codec/结构化诊断 | 未交付 |
| scene/event runtime 与 sandbox script | 未交付 |
| `oms-simple.osk` / `oms-complex.osk` / Authoring Kit | 未交付 |

## 当前实现事实

- `SkinManager` 当前皮肤后仍恒接程序化 `OmsSkin`；最终链底必须由只读、完整验证的 `oms-simple.osk` 接管。
- `BmsLegacySkin` 继续叠加解析 `[Bms]` 并保留 `[Mania]`；native BMS 普通短键是当前唯一真实 package 文件纵切。
- internal 26 项 semantic slot、neutral lane identity/topology/revision、config presence/provenance、六类 lane-resource resolution、event envelope/order 与 capability decision foundation 已落；它们仍是 process-local 合同地基，不是作者 manifest、完整 layout、生产事件 runtime 或 sandbox。
- geometry snapshot 只保存 parser 接受的来源事实，可包含负值、零、`NaN` 或无穷；finite/range/screen-space validation 尚未进入统一 descriptor。
- G1 只保留 folder constructor 与 schema 56 字段；scanner、authority、containment、选择、删改和热重载无可信生产链。
- playfield 可读取当前皮肤 profile，但 gauge/combo/BGA 尚未消费同一 resolved descriptor；14K 四角四 BGA player 只是临时表现。
- mania/BMS 的共同目标仍是 neutral ini codec、scene/event ABI 与 sandbox；ruleset topology/layout adapter 分离，BMS 不继承 mania 具体 Drawable/transformer。

## 当前 gate

| 顺序 | Gate | 状态 |
| --- | --- | --- |
| 1 | schema 56 数据安全 | **通过**：异常 copy 已在保全后定点处置，OMS fixed-ID 已修正；不运行全局 orphan cleanup |
| 2 | 恢复基线实机 | **通过**：无外部皮肤、`.osk`、partial fallback、5K/7K/9K/14K、双皿与 mania/BMS 隔离均正常 |
| 3 | 文档与 memory 健康治理 | **完成**：只归位当前事实、未来步骤、稳定合同和历史；未改代码或产品 gate |
| 4 | managed `.osk` BMS 普通短键编号帧动画实机 | **待用户单独确认**；使用[确定性手工门素材](../../other/SKIN_BMS_NOTE_ANIMATION_MANUAL_GATE.md)，不可复用静态恢复结论 |
| 5 | `SV1-1` 其余共同合同与玩家可见组件 | 未冻结下一组件；完成 gate 4 后再由产品选择 |
| 6 | `SV1-2`～`SV1-7` | 未完成；按 [当前计划](DEVELOPMENT_PLAN.md) 独立过门 |

## 最新验证

### 产品/runtime：2026-07-15 首个玩家可见纵切

- 产品自动验收 **26/26**：真实 `.osk` 导入/游玩对象/Ruleset 链、14K S2、帧推进与循环、SkinManager A→B、同包坏轨逐组件回落、跨包隔离及异步换源；其中 beatmap-local 项只是注入式 provider-order fixture。
- 相关 focused **283/283**，BMS full **1333/1333**，`osu.Desktop.slnf` Release **0 error / 20 warnings**，独立终审 blocker/major **0/0**。
- 本切未修改 shared `osu.Game`、mania compatibility 或 fallback authority，因此未重跑 core/mania；保留 MessagePack 3.1.3 `NU1902` 与 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。
- 测试只使用隔离 headless 临时存储；生产 Realm、`chartskin/`、用户皮肤目录和网络零访问、零写入。
- 本次新动画仍待用户实机确认；当前能力不包含 LN、key、mania compatibility、完整 layout/G1/scene/script 或整包原子重载。

### 文档：2026-07-16 健康治理

- 仅治理 `doc_md` 与 memory 的职责边界和低噪声结构；未修改 runtime/code，未运行产品测试或 Release，2026-07-15 仍是当前产品证据。
- 子线当前态、计划、约束与历史已重新归位；最终相对链接与 whitespace 检查结果记录在本次 [CHANGELOG](CHANGELOG.md)。

### 手工门素材：2026-07-16

- 新增 OMS 自生成、确定性的 good/broken `.osk` 与静音 7K `.bme` 生成器；generator smoke **1/1**，包含两项真实 package 产品链用例后的 `BmsManagedPackageNoteProductTest` **28/28**。
- 这只提供可复现实机输入，不改变 runtime 或 gate 结论；原始 26 项中的 beatmap-local 是 provider-contract fixture，不是 `WorkingBeatmap` / `chartbms/` 集成，因此手工素材不得宣称 beatmap-local 已通过。

## 当前风险

- schema 56 的四个无 authority orphan blob 已保全并暂留；不得把本次定点处置当作 scanner 批量清理先例。
- 首个纵切只覆盖 BMS 普通短键；单组件安全替换不等于整包/全 playfield 同帧原子 reload。
- 真实 BMS beatmap-local 尚无逐谱作者格式和 `WorkingBeatmap` producer；实现它会新增 core 扩展点与公开 sidecar 合同，必须先由产品冻结范围。
- runtime 图片预算不等于 `.osk` importer 的压缩比/zip-bomb gate；G1 仍须独立实现。
- 程序化 `OmsSkin` 在 `oms-simple` parity/完整性/恢复 gate 前不能删除，但也不能写成最终产品能力。
- parser provenance 不等于 validated config/layout；极端几何仍可能使 playfield、gauge/combo 与 BGA 脱节。
- 9K raw lane token 与 V1 canonical 作者 token 存在重叠迁移风险，必须版本化处理，不能静默双 alias。
- topology/event/capability foundation 尚无 production lifecycle、payload、manifest、授权存储或 runtime gate。
- sparse 7K/9K keymode 与 lane keysound timeline 上界仍分别由 P1-K/P1-J 修复和验证；不得由 skin/layout 再猜一遍。
- 皮肤异常期归档只能定点取证，禁止整包 cherry-pick/apply。

## 下一检查点

1. 按[手工门说明](../../other/SKIN_BMS_NOTE_ANIMATION_MANUAL_GATE.md)，由用户单独确认 managed `.osk` 的 BMS 普通短键编号帧动画观感与切换/selected 坏包回落表现。
2. 实机 gate 通过后，由产品选择 `SV1-1` 下一项玩家可见组件并重新冻结最小切片、依赖与验收。
3. 在下一组件冻结前保持 nullable `ISkin`、程序化 `OmsSkin`、当前 fallback authority 与 G1 未交付状态不变。
