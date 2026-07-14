# P1-A 当前状态：Skin V1、产品面与 release gate

> 最后更新：2026-07-14
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，恢复证据见 [SKIN_SYSTEM_RECOVERY_20260710.md](../../other/SKIN_SYSTEM_RECOVERY_20260710.md)，本轮架构审计见 [SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md](../../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

## 一句话状态

皮肤异常代码已撤回并恢复到可信 `.osk/F1/schema 56` 基线；`SV1-0` 的自动回归、数据安全与用户实机 gate 已全部通过。`SV1-1` 已完成三态/precedence、ruleset-neutral semantic slot taxonomy、neutral lane identity primitives 与 immutable lane topology projection 四个合同切片，但未接入 `SkinManager`、未改变 nullable `ISkin` ABI，Skin V1 仍不可用。

## Skin V1 目标

- mania/BMS 共同 ini 语义使用同一 codec/resolver，BMS 只增加 scratch/DP/gauge/BGA/gimmick 扩展。
- 5K/7K P1/P2/居中左右皿、9K BMS/PMS、14K DP 拥有引擎权威的 playfield/BGA descriptor。
- 外部皮肤通过声明式 scene/animation 和可选沙箱脚本响应输入、判定、LN、scratch、gauge、BGA 等只读事件。
- 同一公开 API 既可制作只剩可玩核心的 `oms-simple.osk`，也可制作接近 IIDX 复杂度的 `oms-complex.osk`；两包均同时包含 mania/BMS。
- 最终 fallback 是只读、完整验证的 `oms-simple.osk`。当前程序化 `OmsSkin` 只算迁移基线，V1 release 前必须退出产品渲染链；引擎只保留通用 renderer 与挂载桥。
- `.osk`、根目录 `skin.ini`、mania 共同素材/动画命名、解包编辑与拖入导入遵循 osu 社区心智；BMS/scene/script 是版本化 ruleset 扩展，不要求编译 DLL。

## 当前代码事实

| 面 | 当前状态 | 判读 |
| --- | --- | --- |
| 共享选择/fallback | 仅迁移基线 | `SkinManager` 当前皮肤后恒接程序化 `OmsSkin`；目标 `oms-simple.osk` 尚未接管 |
| mania 默认 | 可用基线 | `ManiaOmsSkinTransformer` 覆盖 stage/column/key/note/LN/hit/judgement/combo/HUD；复杂交互仍由内部 C# 固定行为驱动 |
| mania 用户皮肤 | 可用 | `.osk/[Mania]` legacy 资源、配置和帧动画链成熟 |
| BMS `.osk` 配置 | 可信主面 | `BmsLegacySkin` 叠加解析 `[Bms]`，保留 `[Mania]`；现存静态件颜色/纹理/几何可配置 |
| BMS 共同 ini 实现 | 未统一 | BMS/mania 仍有两套 decoder/resolver，共同键只做到近似语义 |
| BMS 动态外部运行时 | 未开始 | 当前无 declarative scene/event ABI/sandbox script；事故期 F2/Lua 不计能力 |
| component suppress | 合同地基已落，生产未接入 | `SkinSlotResult<T>` 已区分 `Provide/Inherit/Suppress`；现有文件皮肤和 `SkinManager` 尚不能消费该合同 |
| semantic slot taxonomy | 第二个合同切片已落 | 26 个内部语义 slot 固定 7 critical / 19 optional、稳定诊断 ID 与 context 分离；不是作者 manifest ABI 或 layout descriptor |
| neutral lane identity/topology | 第四个合同切片已落 | 强类型 identity 之上新增 immutable snapshot/group/entry、四类零基 logical/visual index、只读排序视图与强类型 lookup；internal BMS/mania projection fixtures 已固定，仍不是完整 layout context、geometry 或生产 adapter 接线 |
| playfield topology | 部分可用 | 5K/7K/9K/14K lane order、双皿和 single-play style 已有自动覆盖；统一 descriptor/全矩阵未落，sparse chart 的 keymode 推断仍有低估风险 |
| HUD 几何联动 | 存在缺口 | playfield 读取皮肤 profile，gauge/combo 却重新取默认 profile；皮肤改宽/高后会脱节 |
| BGA host | 部分可用 | 时间线和 skinnable panel 已有；固定 rect 不消费 skin-resolved playfield，center-right-scratch 仍按右侧 BGA，14K 四角四 player 是临时表现 |
| G1 文件夹 | 仅载体 | folder ctor + schema 56 字段保留；扫描/实例化/选择/删改/热重载无可信生产链 |
| `oms-simple/complex` | 未落 | 当前 reference ini 仅模板/自校验；两个 mania+BMS 组合 `.osk`、canonical fallback 与作者套件均未制作 |

## mania 审查结论

mania `skin.ini` 的上限是“固定行为宿主 + 素材/有限参数”：key press、LN holding、hit explosion、judgement、combo 等事件行为由 C# 组件实现，不是作者脚本定义。因此：

- mania parser/asset/frame conventions 是 BMS compatibility 下限；
- `ManiaLegacySkinTransformer`、`Column`、`ManiaAction` 和 480 坐标不能直接成为 BMS 基类；
- Skin V1 必须抽出 neutral codec/scene/event runtime，再由 mania/BMS adapter 接入；
- 后续动态件不得继续默认采用“一件效果一个固定 `DefaultBmsXxxDisplay`”的扩张方式。

## BMS layout 审查结论

- 5K/7K：已有 P1 左、P2 右、居中左皿、居中右皿；现有 headless screen-space 测试只以 7K fixture 为主，5K 完整矩阵仍需补。
- 9K BMS/PMS：style 会规范化为 center；两者 context 和 BGA safe viewport 尚未形成 V1 descriptor 测试。
- 14K：已有 16 lanes、S1/S2、两个 deck 间 centre gap；当前 BGA 默认创建四个独立 player，仅证明临时默认行为，不是最终正确布局。
- 当前 BGA custom display 接收 timeline 并创建 player，与“引擎拥有 BGA truth、皮肤只负责表现”目标冲突，须在 SV1-3 重构。
- 当前 5K/7K/14K lane keysound timeline 用 key count 作上界，可能丢最右轨及 14K 第二皿；属 P1-K/P1-J 待修代码缺口，Skin V1 每轨 smoke matrix 必须覆盖。

## 当前 gate

| 顺序 | Gate | 状态 |
| --- | --- | --- |
| 1 | schema 56 `SkinInfo` 数据安全门 | **通过**：备份与副本演练后定点移除异常 copy、修正 OMS 固定记录；路径 authority 正常 |
| 2 | 无外部皮肤、`.osk`、partial fallback、5K/7K/9K/14K 实机视觉 | **通过**：用户于 2026-07-14 自行确认全清单正常；Agent 未操控 GUI |
| 3 | shared contract/fixture 代码冻结 | 进行中：三态/precedence、semantic slot taxonomy、neutral lane identity 与 topology projection 四切完成；full layout/config/event/capability 仍未完成 |
| 4 | G1 authority/containment/atomic reload | 未开始重做 |
| 5 | 全 keymode playfield/BGA descriptor | 未开始 |
| 6 | mania-compatible shared ini codec | 未开始 |
| 7 | scene/event ABI + sandbox script | 未开始 |
| 8 | `oms-simple` / `oms-complex` / Authoring Kit / file fallback release gate | 未开始 |

## 最近验证

### `SV1-0` 闭门与 `SV1-1` 前四个合同切片（2026-07-14）

| 检查 | 结果 |
| --- | --- |
| 用户实机 gate | **通过**：无外部皮肤、当前 `.osk`、partial fallback、BMS 5K/7K/9K/14K、14K S1/S2 双皿、mania/BMS 资源隔离均正常 |
| `GameplaySkinSlotCatalogTest` / `GameplaySkinSlotResolverTest` | **34/34；13/13（合计 47/47）** |
| lane identity / topology snapshot / shared 合并 | **26/26；19/19；slot+identity+topology 92/92** |
| `SkinProvidingContainer` / `RulesetSkinProvidingContainer` authority guard | **6/6**；实链顺序为 beatmap-local → selected → ruleset resources → protected built-in |
| BMS lane layout + topology projection | **26/26**（其中 projection 19/19）；5K/7K 四 style、9K BMS/PMS、14K 双 deck/双皿与 malformed composition 均固定 |
| BMS parser/legacy/reference/render focused | 43/43 |
| BMS transformer + user fallback | 104/104 |
| mania topology/special/`TestSceneOmsBuiltInSkin` / 默认资源专项 | **95/95**（其中 projection 8/8）；专项 1/1 |
| core skin focused | 57/62；5 项与恢复审计同名，无新失败 |
| `osu.Desktop.slnf` Release | **0 error / 20 warnings** |
| Markdown 相对链接 / diff | 114 个文件、916 个相对链接、0 断链；`git diff --check` 通过 |

第四切 focused 首轮与审计修正后均保持 core topology 19/19、BMS projection 19/19、mania projection 8/8；独立提交前审计发现并在最终验证前补齐三个潜在漏报面：BMS 不能只校验 lane 数而接受额外 scratch 的伪 canonical composition、14K 必须锁完整 `Scratch + 14×Key + Scratch` role 序列、连续 group block fixture 必须覆盖真实多 lane 交错。mania projection 另对可变 stage authority 做防御性复制，并 fail-closed 拒绝 null/空/>2 stage、null element 与单 stage 超过 10 keys。静态收尾中 targeted formatter 将 fixture 必需 using 误报 `IDE0005`；按告警移除后出现两处 `CS0246 HashSet<>` 编译失败，改为 LINQ `ToHashSet()` 后 BMS projection 19/19、targeted verify exit 0。每次测试均保留 9 条 MessagePack `NU1902`；`dotnet format` 的泛化 workspace-load warning 仍对应同一组 advisory，source targeted verify 重报 `BmsLaneLayout` 两个既有 array declaration 的 `IDE0008`，BMS 首次编译与 Release 另保留既有 `CS8600`/`CA2007`。core skin 的 5 项失败仍与恢复审计同名；最终强制 `.slnf` Release Rebuild 为 0 error / 20 warnings，未使用 `NoWarn`。完整 schema 56 脱敏证据见 [`SV1-0` 数据安全门报告](../../other/SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)。

### 恢复基线（2026-07-10）

| 检查 | 结果 |
| --- | --- |
| H1/H2 `BmsLegacySkinTest` | 15/15 |
| BMS 全量 | 1005/1005 |
| mania 默认 OMS 资源 | 1/1 |
| mania 全量 | 787/791；4 项既有 HoldNote auto-frame 期待失配 |
| core skin focused | 57/62；5 项 Argon/已删 ruleset 旧测试失配 |
| Release | 0 error / 20 warnings |

### 本轮只读架构审查（2026-07-10）

- 复跑 BMS F1 parser/legacy/reference/render focused：43/43。
- 复跑 mania `TestSceneOmsBuiltInSkin`：84/84。
- 两组均只有既有 MessagePack 3.1.3 `NU1902` 漏洞告警，无测试失败。
- 未改运行时代码；未重跑 BMS 全量/Release。
- 文档路线从旧 F/G 组件堆叠改为 `SV1-0`～`SV1-7`，事故期代码仍未恢复。

## 当前风险

- schema 56 异常记录已定点处置；四个无 authority 的 orphan blob 暂留且已另行保全，未运行会波及其它记录的全局 cleanup。
- external absolute path、删除/重命名 containment、scanner authority 和原子 reload 尚无可信生产实现。
- 当前 parser 对未知/非法 BMS 值是静默 fail-open，作者文档曾误写为“会告警”；结构化诊断是 SV1-4 未完成能力。
- semantic catalog 的未知 ID 目前只会由 `TryGet()` 拒绝，尚无 manifest parser/作者诊断接线；旧 raw resolver 仍是 uncatalogued compatibility 入口，生产接线必须只走 descriptor overload。
- catalogued 诊断的 context/exception 已从 JSON 与安全 `ToString()` 排除，但 `ProviderName` 的隐私仍依赖 provider 遵守“非敏感 authority 名、不得含绝对路径”合同。
- neutral topology aggregate 已对单 snapshot 的重复 ID/metadata membership conflict、索引 permutation、local/global 顺序和 group 连续块 fail-closed，并显式携带 global/group-local logical/visual index；但跨 revision 稳定仍只是 producer 合同，尚无 transition validator，也没有 keymode/style/action/source、geometry、full `GameplaySkinLayoutContext` 或生产接线。
- 皮肤几何值无完整合法域校验；playfield、gauge/combo 与 BGA 尚未消费同一 resolved descriptor，极端值会脱节或重叠。
- sparse 7K/9K chart 可能因未使用高位 channel 被 keymode 启发式低估；布局正确性必须以前置解析诊断/override 为条件。
- 设置文案仍写 `14K→中缝`，当前代码实际为四角四 player；两者都不是 V1 authority，发布前必须统一到 descriptor。
- `buildLaneKeysoundTimelines()` 的 lane 上界仍错误使用 `GetKeyCount()`；5K/7K 最右键与 14K 右侧末键/第二皿存在 timeline 丢失风险。
- 只测 parser/类型或孤立接口不能证明真实选择链、事件顺序、脚本安全和视觉正确。
- 14K 四角 BGA、程序化动态件和内部固定动画都不能被提前描述为 V1 最终方向。
- 当前程序化 `OmsSkin` 仍是实际链底；在 `oms-simple` 完整性、自动恢复和 mania/BMS parity 过门前不能直接删除，但它也不能进入 V1 最终发行架构。
- resolver 不拥有候选组件生命周期：被额外 validator 拒绝的 `Drawable`/`IDisposable` 不能由 resolver 擅自 dispose，provider/消费方必须在接线前冻结缓存、parenting 与回收合同。

## 下一检查点

1. 继续 `SV1-1` 的 config/event/capability 与 compatibility fixtures；neutral topology projection 已冻结四类 index、5K/7K 四 style、9K BMS/PMS、14K 双 group 和 mania stage-local SpecialKey，但完整 `GameplaySkinLayoutContext`、geometry 与 transition/wire ABI 仍属后续。前四个合同切片不等于整个 `SV1-1` 完成。
2. 在另立生产接线切片前保持 `SkinManager`、nullable `ISkin`、程序化 `OmsSkin` 与当前 fallback authority 不变；G1 仍按 `SV1-2` 独立重做。
