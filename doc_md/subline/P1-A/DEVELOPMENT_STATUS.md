# P1-A 当前状态：Skin V1、产品面与 release gate

> 最后更新：2026-07-15
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，恢复证据见 [SKIN_SYSTEM_RECOVERY_20260710.md](../../other/SKIN_SYSTEM_RECOVERY_20260710.md)，本轮架构审计见 [SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md](../../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

## 一句话状态

皮肤异常代码已撤回并恢复到可信 `.osk/F1/schema 56` 基线；`SV1-0` 的自动回归、数据安全与用户实机 gate 已全部通过。`SV1-1` 已完成三态/precedence、ruleset-neutral semantic slot taxonomy、neutral lane identity/immutable topology/保持性 transition validation、configuration bucket explicit-presence、legacy mania 九个 primitive scalar、五组 indexed array、四项 known global colour、exact `Colour{n}` / `ColourLight{n}` per-column colour、exact 十三项 `[Mania] Keys:` bucket-global/non-column resource-name、legacy mania/native `[Bms]` 六类 lane-resource、legacy mania `NoteBodyStyle`、native `[Bms]` exact 22-colour 与 exact 12-geometry decoder-time accepted provenance、gameplay event envelope/order、capability negotiation/禁止 authority、六类 lane-resource neutral snapshot/BMS→mania 候选链、internal 逐字段 resolution/revision-owner、topology publication/native-context continuity 二十个合同切片。路线仍是 `SV1-1` 进行中、`SV1-2` 只有 early carrier，`SV1-3`～`SV1-7` 未实现；未接入 `SkinManager`、未改变 nullable `ISkin` ABI，Skin V1 仍不可用。

## Skin V1 目标

- mania/BMS 共同 ini 语义使用同一 codec/resolver，BMS 只增加 scratch/DP/gauge/BGA/gimmick 扩展。
- 5K/7K P1/P2/居中左右皿、9K BMS/PMS、14K DP 拥有引擎权威的 playfield/BGA descriptor。
- 外部皮肤通过声明式 scene/animation 和可选沙箱脚本响应输入、判定、LN、scratch、gauge、BGA 等只读事件。
- 同一公开 API 既可制作只剩可玩核心的 `oms-simple.osk`，也可制作接近 IIDX 复杂度的 `oms-complex.osk`；两包均同时包含 mania/BMS。
- 最终 fallback 是只读、完整验证的 `oms-simple.osk`。当前程序化 `OmsSkin` 只算迁移基线，V1 release 前必须退出产品渲染链；引擎只保留通用 renderer 与挂载桥。
- `.osk`、根目录 `skin.ini`、mania 共同素材/动画命名、解包编辑与拖入导入遵循 osu 社区心智；BMS/scene/script 是版本化 ruleset 扩展，不要求编译 DLL。

## 产品功能进度

- **恢复基线功能可用**：当前 `.osk`/legacy mania、BMS F1 静态颜色/纹理/几何、程序化 `OmsSkin` 迁移 fallback 与既有选择链保持可用，自动、数据与实机恢复 gate 已通过。
- **Skin V1 新增可见功能仍为 0**：前二十切均为 process-local 合同、fixture 或 accepted-provenance 地基，没有让用户或皮肤作者获得新的生产能力，也没有把任一 fake/internal carrier 接入真实选择、渲染或文件链。

| Skin V1 产品交付面 | 当前状态 |
| --- | --- |
| 三态 gameplay slot 生产接线 | 未交付；只有平行合同/fixture |
| `oms-simple.osk` canonical 逐组件 fallback | 未交付；实际链底仍是程序化 `OmsSkin` |
| 安全 G1 文件夹导入/选择/重载 | 未交付；只有 schema/ctor early carrier |
| 统一 layout descriptor/solver | 未交付；exact geometry snapshot 只是未验证的来源事实 |
| shared ini codec 与结构化诊断 | 未交付 |
| scene/event runtime 与 sandbox script | 未交付 |
| `oms-simple.osk` / `oms-complex.osk` 两个 mania+BMS 普通社区包 | 均未交付 |

## 当前代码事实

| 面 | 当前状态 | 判读 |
| --- | --- | --- |
| 共享选择/fallback | 仅迁移基线 | `SkinManager` 当前皮肤后恒接程序化 `OmsSkin`；目标 `oms-simple.osk` 尚未接管 |
| mania 默认 | 可用基线 | `ManiaOmsSkinTransformer` 覆盖 stage/column/key/note/LN/hit/judgement/combo/HUD；复杂交互仍由内部 C# 固定行为驱动 |
| mania 用户皮肤 | 可用 | `.osk/[Mania]` legacy 资源、配置和帧动画链成熟 |
| BMS `.osk` 配置 | 可信主面 | `BmsLegacySkin` 叠加解析 `[Bms]`，保留 `[Mania]`；现存静态件颜色/纹理/几何可配置 |
| BMS 共同 ini 实现 | bucket + 六类 lane-resource resolution + legacy mania scalar/indexed-array/colour/resource/NoteBodyStyle + native exact colour/geometry presence 地基已落、codec 未统一 | 九个 scalar、五组 array、四项 exact global colour、exact `Colour{n}` / `ColourLight{n}`、exact 十三项 bucket-global/non-column resource-name、legacy mania/native `[Bms]` 的 note/LN head/body/tail/key up/down、legacy mania `NoteBodyStyle`，以及 native `[Bms]` exact 22-colour / exact 12-geometry 均已有 decoder-time accepted sidecar；对应 factory 不再从 mutable compatibility view 反推 declaration。native colour/geometry 只把 exact source key 记入 closed provenance，逗号 composite key 仍只保留既有 public compatibility 行为。geometry snapshot 接受 parser 已接受的负值、零、`NaN` 与无穷值，不是有效 layout；真实 validation/materializer、shared codec/诊断与生产接线仍未落 |
| BMS 动态外部运行时 | 未开始 | 当前无 declarative scene/event ABI/sandbox script；事故期 F2/Lua 不计能力 |
| component suppress | 合同地基已落，生产未接入 | `SkinSlotResult<T>` 已区分 `Provide/Inherit/Suppress`；现有文件皮肤和 `SkinManager` 尚不能消费该合同 |
| semantic slot taxonomy | 第二个合同切片已落 | 26 个内部语义 slot 固定 7 critical / 19 optional、稳定诊断 ID 与 context 分离；不是作者 manifest ABI 或 layout descriptor |
| neutral lane identity/topology | 第四、十、十四个合同切片已落 | 强类型 identity、immutable snapshot/group/entry、四类零基 logical/visual index 与 transition validator 之上，新增 topology-only publication 和 process-local monotonic revision owner；internal BMS 以 exact keymode、mania 以 ordered stage-column vector 维护 native continuity。仍不是完整 layout/geometry、生产 `layoutRevision` producer、event/wire ABI 或 adapter 接线 |
| gameplay event envelope/order | 第六个合同切片已落 | V1 process-local immutable envelope、engine-owned payload hierarchy 与 internal fail-closed canonical-stream cursor 已固定；只有 header/ownership/order fixture，没有具体 event family、完整 state payload、lifecycle producer、dispatch 或 script ABI |
| capability negotiation/禁止 authority | 第七个合同切片已落 | process-local request/host support/per-skin authorization/decision 分层、closed allowlist 与 hard-deny classifier 已固定；没有真实 capability catalog、manifest、授权存储/UI、runtime gate 或 sandbox |
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
| 3 | shared contract/fixture 代码冻结 | 进行中：三态/precedence、semantic slot taxonomy、neutral lane identity/topology/保持性 validation、config presence、legacy mania scalar/array/四项 global colour/exact per-column colour/bucket-global resource/`NoteBodyStyle` snapshot、legacy mania/native BMS lane-resource accepted provenance、native BMS exact 22-colour / exact 12-geometry snapshot、event envelope/order、capability、lane-resource candidate/resolution，以及 topology publication/native continuity 二十切已完成。full layout/geometry、生产 revision/event/wire、完整 config/验证、具体 event family/producer、真实 capability manifest/runtime 仍未完成；Skin V1 产品可见新增仍为 0 |
| 4 | G1 authority/containment/atomic reload | 未开始重做 |
| 5 | 全 keymode playfield/BGA descriptor | 未开始 |
| 6 | mania-compatible shared ini codec | 未开始 |
| 7 | scene/event ABI + sandbox script | 未开始 |
| 8 | `oms-simple` / `oms-complex` / Authoring Kit / file fallback release gate | 未开始 |

## 最近验证

### `SV1-0` 闭门与 `SV1-1` 前二十个合同切片（2026-07-15）

| 检查 | 结果 |
| --- | --- |
| 用户实机 gate | **通过**：无外部皮肤、当前 `.osk`、partial fallback、BMS 5K/7K/9K/14K、14K S1/S2 双皿、mania/BMS 资源隔离均正常 |
| `GameplaySkinSlotCatalogTest` / `GameplaySkinSlotResolverTest` | **34/34；13/13（合计 47/47）** |
| shared declaration / mania bucket / BMS bucket focused | **5/5；13/13；9/9** |
| legacy mania primitive scalar accepted snapshot | **18/18**；连同既有 decoder/declaration 合同 **30/30**；覆盖缺/空/exact/duplicate bucket、显式默认、转换与 bool/FPS 规范化、malformed、non-finite、重复字段、native mutation 隔离与非法 sidecar field 拒绝 |
| legacy mania indexed-array accepted snapshot | **20/20**；连同 scalar/decoder/declaration 合同 **50/50**；覆盖五组 cardinality/缩放、逐 index absence、空/坏 item=`Declared(0)`、短/超长/partial overlay、1K、non-finite、双层 defensive copy 与非法 field/index |
| legacy mania known global colour accepted snapshot | **15/15**；连同 scalar/array/decoder/declaration 合同 **65/65**；覆盖四个 exact global RGB/RGBA、alpha 0、缺/空/exact/duplicate bucket、malformed 不声明/不覆盖、大小写与 unknown/per-column 排除、native dictionary mutation 隔离、closed public surface 与安全字符串 |
| legacy mania per-column colour accepted snapshot | shared focused **18/18**；config presence/decoder/snapshot aggregate **83/83**；覆盖 exact `Colour{n}` / `ColourLight{n}`、RGB/RGBA/alpha 0、缺/显式空、duplicate last accepted、malformed 不声明/不覆盖、非 canonical/越界拒绝、decode 后 dictionary mutation 隔离、defensive copy 与 immutable topology-bound snapshot |
| legacy mania bucket-global resource-name accepted snapshot | focused **15/15**；config presence/decoder/snapshot aggregate **98/98**；覆盖 exact 十三项、缺 bucket/显式空 bucket、explicit empty=`Declared`、duplicate last accepted、Keys 前声明、exact classifier、unknown broad-prefix compatibility、完整 `ImageLookups` mutation 隔离、closed semantic public surface 与安全字符串 |
| legacy mania `NoteBodyStyle` accepted snapshot | focused **26/26**；shared config aggregate **126/126**；覆盖 named/numeric/undefined/comma compatibility、大小写/空/未知拒绝、缺 bucket 与显式空 bucket、pending/duplicate/malformed `Keys` 既有行为、public field mutation 隔离、outer/inner declaration、immutable public surface 与安全字符串 |
| native `[Bms]` exact 22-colour accepted snapshot | 首次 focused **27/28** 的唯一失败是测试自身的 `Count` 断言写法错误，修正并扩展后 **31/31**；覆盖 exact RGB/RGBA/alpha 0、缺/显式空 bucket、pending/duplicate/malformed、valid-last-wins、public `Colours` forge/overwrite/remove/clear/late-add 隔离、defensive copy 与 closed field guard。逗号 composite key 继续写既有 compatibility dictionary，但不进入 exact sidecar/snapshot |
| native `[Bms]` exact 12-geometry accepted snapshot | 首次 / targeted format 后最终 focused 均为 **49/49**；覆盖 12 项 exact + public compatibility view、sign/decimal/exponent、`-0` bits、`NaN` 大小写、正负无穷、overflow/underflow、malformed、duplicate/pending/repeated `Parse()`、composite compatibility-only、mutable `Geometry` 隔离、全 keymode、closed guards/defensive copy/internal surface。它只保存 parser accepted provenance，不证明 geometry 有效 |
| 既有 `BmsSkinDecoderTest` | **8/8** |
| per-column colour ruleset mapping | mania **5/5**；BMS **14/14**；旧 BMS candidate mapping regression **29/29**。mania 使用 `GlobalLogicalIndex`；BMS full visual 使用 `GlobalVisualIndex`，14K deck 使用 `GroupLocalVisualIndex` 且两个 deck 可共享 source index，key-only 使用非 scratch visual enumeration |
| lane-resource snapshot / mania adapter / BMS candidate plan | **12/12；6/6；29/29（新增合计 47/47）**；覆盖六字段、显式空值、逐字段 fallback candidate、P2/CenterRightScratch、9K BMS/PMS 与 14K 双 deck |
| lane-resource decoder-time accepted provenance | shared focused **21/21**；mania focused **6/6**；BMS focused **40/40**；legacy mania/native `[Bms]` factory 均只读 accepted sidecar，两侧字典内容的 overwrite/remove/clear/late-add 及 legacy 整表重赋值均不能伪造、擦除或改写六字段 declaration |
| BMS lane-resource resolution / revision owner | **55/55**；覆盖六字段 source-aware fail-open、5K/7K scratch、9K 去重、14K 双 deck、outer precedence、canonical 失败及 active/provisional owner 生命周期 |
| lane identity / topology snapshot / transition validator | **26/26；19/19；12/12**；独立重建、side/visual reorder 允许，ID set、logical order、membership、role 漂移拒绝 |
| topology publication / native-context owner | shared owner **8/8**，BMS publication **14/14**，mania publication **7/7**；首发 revision 0、成功 checked `+1`，native mismatch/comparer 异常/neutral rejection/overflow 不推进；BMS exact keymode 关闭 9K BMS/PMS neutral-shape 盲区，mania exact ordered stage vector 拒绝换列数与双 stage 重排 |
| gameplay event envelope / canonical stream cursor | **23/23**；覆盖 Snapshot/Reset/Edge、mid-session attach high-water、epoch/sequence 连续、负 lead-in time、layout revision、拒绝原子性与溢出边界 |
| capability negotiation / shared neutral gameplay 总集 | **91/91；250/250**；总集包含 per-column neutral snapshot 7 项与 revision owner 8 项，覆盖 closed allowlist、host support、per-skin authorization、hard-deny family、只读 event token、不可变/矛盾快照、native/neutral failure 原子性与公开面；第十六至二十切的 source-specific provenance 加固不改变该 neutral aggregate，第二十切未改 shared 且未重跑本集 |
| `SkinProvidingContainer` / `RulesetSkinProvidingContainer` authority guard | **6/6**；实链顺序为 beatmap-local → selected → ruleset resources → protected built-in |
| BMS lane layout / topology projection / publication | **7/7；20/20；14/14**（topology projection+publication **34/34**）；5K/7K style-only 与 visual reorder、5K→7K、9K BMS/PMS native mismatch、14K 双 deck/双皿及 malformed composition 均固定 |
| mania topology projection / publication | **9/9；7/7（合计 16/16）**；canonical topology 只从防御性复制的 ordered stage vector 生成，同 shape 重建可递增，4→5 与 `[4,5]→[5,4]` 从 native gate 原子拒绝 |
| BMS skin relevant | **381/381** |
| BMS relevant 连续性 / 本切全量 | 上一切 full **1188/1188**；本切为 **1237/1237** |
| BMS transformer + user fallback | 104/104 |
| mania skin relevant / 本切全量 | 第二十切未改 shared/mania 且未重跑；既有基线 skin **182/182**、full **827/831**，仅恢复基线同组 4 个 HoldNote auto-frame 期待失败 |
| core skin focused | 第二十切未改 shared/core 且未重跑；既有基线 57/62，5 项与恢复审计同名 |
| `osu.Desktop.slnf` Release | **0 error / 20 warnings** |
| source review | blocker / major / minor = **0 / 0 / 0** |
| Markdown 相对链接 / diff | 121 个文件、936 个相对链接、0 断链；working tree 与 staged diff 检查通过 |

第二十切为 native `[Bms]` 当前 12 个 exact geometry field 增加 decoder-time private sidecar、internal closed field catalog 与 source-specific immutable bucket snapshot/factory。只有 raw source key 与解析后 canonical field 同名的 exact declaration 进入 sidecar；既有 `Enum.TryParse` comma-composite alias 继续按恢复基线写 public `Geometry` compatibility dictionary，但不升格为 closed source declaration。factory 只读 sidecar，因此 decode 后 public dictionary 的 forge/overwrite/remove/clear/late-add 不能伪造、擦除或改写 accepted provenance。

snapshot 原样保留 invariant float parser 接受的 sign/decimal/exponent、负值、零、`-0`、`NaN`、正负无穷与 overflow/underflow 结果；它不进行 finite、正值、range、screen-space、不重叠或跨字段 validation，也不是 neutral geometry descriptor、resolved layout 或 production `GameplaySkinLayoutContext`。首次和 targeted format 后最终 focused 均为 **49/49**，既有 decoder **8/8**、BMS skin **381/381**、BMS full **1237/1237**；Release Rebuild **0 error / 20 warnings**，source review blocker/major/minor **0/0/0**。第二十切未改 shared/mania/core，未重跑该三组；生产 Realm、`chartskin/`、用户皮肤目录与网络均零访问、零写入。

从产品功能看，第二十切仍只增加不可见的来源事实地基：三态生产接线、`oms-simple` 逐组件 fallback、安全 G1、统一 layout、shared codec/结构化诊断、scene/script 与两个外部包均未交付，Skin V1 新增可见功能仍为 **0**。下一检查点停止继续堆同类 presence 切片，先按产品纵切只读审计“真实文件组件受控选择 + 缺失/损坏时逐组件 fallback”所需最短依赖路径，再据证据决定一个最小实现切片；这不是已接线承诺。

第十九切为 native `[Bms]` 当前 22 个 exact colour field 增加 decoder-time private sidecar、internal closed field catalog 与 source-specific immutable bucket snapshot/factory。只有 raw source key 与解析后 canonical field 同名的 exact declaration 进入 sidecar；既有 `Enum.TryParse` 可接受的 comma-composite key 仍按恢复基线写入 public `Colours` compatibility dictionary，但不升格为 closed source declaration。这与第十八切 `NoteBodyStyle` 的 value-composite 保留为 parser-accepted value 是两个不同边界。

缺 keymode bucket 保持 outer `Absent`，显式 bucket 即使没有成功 exact colour 也保持 outer `Declared` 与全字段 inner `Absent`；RGB 保存 alpha 255，RGBA/alpha 0 保存完整 parser value，valid duplicate last accepted，malformed 不声明也不擦除既有成功值。factory 只读 sidecar，所以 decode 后 public dictionary 的 forge/overwrite/remove/clear/late-add 不能改写 provenance。该 snapshot 不进行 neutral semantic mapping、颜色/可见性 validation、fallback resolution，也未改 production `BmsLegacySkin`、renderer、candidate chain、`SkinManager` 或 nullable `ISkin` ABI。

第十九切首次 focused **27/28** 的唯一失败为新测试 `Count` 断言写法错误，不是产品实现失败；修正并扩展后 focused **31/31**、BMS skin **332/332**、BMS full **1188/1188**。Release Rebuild **0 error / 20 warnings**，保留 9 条 MessagePack `NU1902` 在 restore/build 重复显示与 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`；独立终审 blocker/major/minor **0/0/0**。本切未改 shared/mania/core，因此未重跑该三组；生产 Realm、`chartskin/`、用户皮肤目录与网络均零访问、零写入。`SV1-1` 仍未完成；其后第二十切已完成 native `[Bms]` exact 12-geometry accepted provenance，完整 finite/range/screen-space 验证仍留给 neutral descriptor/solver。

第十八切为 exact legacy mania `NoteBodyStyle` 增加 decoder-time accepted sidecar，并由独立、source-specific、immutable bucket snapshot/factory 只读该 sidecar。既有 `Enum.TryParse` compatibility 完整保留：大小写敏感，named、numeric、undefined numeric 与 comma-combined value 仍按当前 parser 接受并保存解析后的 enum；invalid declaration 不伪造 presence，也不擦除此前成功值。缺真实 `Keys:` bucket 为 outer `Absent`，显式 bucket 但无成功字段为 outer `Declared` + inner `Absent`，显式 `Stretch=0` 仍与缺失可区分；decode 后 public nullable field 的 forge/erase/alter 均不改变 provenance。

该 snapshot 刻意不接收 `[General] Version`，也不把 `LegacySkin` 在字段缺失时按 version 派生的 `Stretch`/`RepeatBottom` effective default 或缺 bucket 时合成的 configuration 当作 source declaration。第十八切未改变 production `LegacySkin`、mania `LegacyBodyPiece`、OMS preset、native BMS schema/renderer、`SkinManager`、candidate/fallback、nullable `ISkin` ABI，也没有资源 validation/materialization、package authority 或 shared codec 接线。

第十八切最终 focused **26/26**、shared config aggregate **126/126**、mania skin **182/182**、BMS skin **301/301**、BMS full **1157/1157**；mania full **827/831** 仍仅恢复基线同名 4 个 HoldNote auto-frame 期待失败，core skin **57/62** 仍为恢复基线同名 5 项。Release Rebuild **0 error / 20 warnings**；告警保持为 9 条 MessagePack `NU1902` 在 restore/build 重复显示，以及 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。生产 Realm、`chartskin/` 与用户皮肤目录零写入。`SV1-1` 整体仍未完成；下一切不预设实现，优先只读审计 native BMS remaining closed fields、package authority/materializer 边界，再据证据选择一个最小闭环。

第十七切同时为 legacy mania 与 native `[Bms]` 的 note、LN head/body/tail、key up/down 六类 lane-resource 保存 decoder-time accepted declaration/value sidecar，并将两侧 topology-bound snapshot factory 改为只读 sidecar。legacy mania 只把 `NoteImage{n}`、`NoteImage{n}H/L/T`、`KeyImage{n}`、`KeyImage{n}D` 的严格零基 ASCII canonical token 记为 accepted；大小写变体、前导零、符号、越界与相似 suffix 不进入 sidecar，既有 broad-prefix compatibility dictionary 行为保持。native BMS 不在本切规范化 token，继续保留 decoder 实际接受的 raw numeric/`S`/`S2` token 与现有 9K `0..8` compatibility 事实。

两侧 decoder 仍同步维护现有 `ImageLookups` compatibility view，但 factory 不再信任它：手工填表不能伪造 declaration，两侧字典内容在 decode 后的 overwrite/remove/clear/late-add，以及 legacy 字典整表重赋值，都不能擦除或改变 accepted provenance；explicit empty 仍为 `Declared("")`，valid duplicate 仍以 last accepted 为准。这只关闭创建 snapshot 前的 provenance window，不是资源存在性、路径 containment、动画序列、素材完整性 validation/materialization，也未改变 production `LegacySkin`/`BmsLegacySkin` compatibility lookup、candidate precedence、renderer、`SkinManager`、fallback 或 nullable `ISkin` ABI。

第十七切最终 shared focused **21/21**、mania focused **6/6**、BMS focused **40/40**，BMS full **1157/1157**；mania full **827/831** 仍仅恢复基线同名 4 个 HoldNote auto-frame 期待失败，core skin **57/62** 仍为恢复基线同名 5 项。Release Rebuild **0 error / 20 warnings**；告警保持为 9 条 MessagePack `NU1902` 在 restore/build 重复显示，以及 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。生产 Realm、`chartskin/` 与用户皮肤目录零写入。`SV1-1` 整体仍未完成，下一最小切片须先只读审计剩余 `NoteBodyStyle`、资源 validation/materialization 与其它未封闭配置面，再按证据确定，不能把本切描述为 Skin V1 可用。

第十六切为 exact `LightingN`、`LightingL`、`StageLeft`、`StageRight`、`StageBottom`、`StageLight`、`StageHint`、`Hit0`、`Hit50`、`Hit100`、`Hit200`、`Hit300`、`Hit300g` 保存 legacy mania decoder 成功接受时的 bucket-global/non-column resource-name declaration 与 `SplitKeyVal`-trimmed、尚未 `CleanFilename`/validation 的 compatibility string。exact classifier 只让这十三项进入 closed sidecar；既有大写 `Lighting*`、`Stage*`、`Hit*` broad-prefix unknown key 仍按 compatibility 行为写入 `ImageLookups`，但不进入 snapshot。显式空资源名保持 `Declared("")`，duplicate 仍以 last accepted 为准；不在本切新增文件名清理、路径/存在性/动画/素材完整性验证或 materialization。

public、source-specific、immutable snapshot 将 raw legacy key 映射为 `ExplosionResource`、`HoldNoteLightResource`、`LeftStageResource`、`RightStageResource`、`BottomStageResource`、`KeyFlashResource`、`HitTargetResource` 与 `MissJudgementResource`/`MehJudgementResource`/`OkJudgementResource`/`GoodJudgementResource`/`GreatJudgementResource`/`PerfectJudgementResource`，不公开 raw string-key lookup。factory 只读 decoder-time sidecar；decode 后对公开可变 `ImageLookups` 的 add/replace/remove/clear 或整表替换都不能伪造、擦除或改写 provenance。该切未接 production lookup、candidate plan、renderer、`SkinManager`、fallback、nullable `ISkin` 或 package/wire ABI。

第十六切最终 focused **15/15**、config presence/decoder/snapshot aggregate **98/98**、shared neutral gameplay **250/250**、BMS full **1146/1146**；mania full **827/831** 仍仅既有 4 个 HoldNote auto-frame 期待失败，core skin **57/62** 仍为恢复基线同名 5 项。Release Rebuild **0 error / 20 warnings**；告警保持为 9 条 MessagePack `NU1902` 在 restore/build 重复显示，以及 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。本切没有文件验证、资源物化或生产接线，也未写入生产皮肤数据。

第十五切只为 exact legacy mania `Colour{n}` / `ColourLight{n}` 保存 decoder 成功接受时的 per-source-column declaration 与 parser colour value；非 canonical、越界或 malformed key/value 不进入 sidecar，也不能覆盖已接受值。public neutral field catalog 只含 lane background/light colour，snapshot 防御性复制并绑定 exact immutable topology，以 deterministic lane/field 顺序公开 declaration；它不公开任意 `Colour*` dictionary ABI，也不进行主题化默认、文件验证、materialization 或 fallback。

ruleset mapping 由显式 source-column→stable-lane 对照驱动：mania 使用 `GlobalLogicalIndex`；BMS full visual 使用 `GlobalVisualIndex`，14K 双 deck 的 `Keys:8` 投影使用 `GroupLocalVisualIndex`，所以两个 deck 可共享同一组 legacy source index；key-only 使用剔除 scratch 后的 visual enumeration。三个 BMS projection 当前是彼此独立的 fixture-only 入口；若未来合并到同一 candidate plan，必须共享同一个 exact topology reference，不能分别从等形但不同实例的 topology 构造同一 revision。该切未接 production lookup、renderer、`SkinManager`、fallback 或既有 nullable `ISkin` ABI。

第十五切最终 shared focused **18/18**、mania **5/5**、BMS **14/14**、旧 BMS candidate regression **29/29**、config aggregate **83/83**、shared gameplay **250/250**、BMS full **1146/1146**；mania full **827/831** 仍仅既有 4 个 HoldNote auto-frame 期待失败，core skin **57/62** 仍是 1 项 Argon 与 4 项已删 ruleset archive 基线失败。Release Rebuild **0 error / 20 warnings**；保留 9 条 MessagePack `NU1902` 在 restore/build 重复显示和 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。本切对生产 Realm、`chartskin/` 与用户皮肤目录零写入。

第十四切新增 public、immutable、topology-only 的 `GameplaySkinLaneTopologyPublication` 与 generic process-local revision owner。首个成功 publication 为 revision 0，后续只有 exact native-context comparer、既有 neutral transition validator、checked increment 与新 publication 全部成功才提交；native mismatch、comparer 异常、neutral rejection 与 `long.MaxValue` overflow 都保持 `Current` 不变且不消耗 revision。该 revision 不是 package revision、event `layoutRevision` 或 serialisation/wire ABI，owner 也不是 thread-safe/security boundary。

BMS internal owner 以 exact `BmsKeymode` 为 continuity authority，`AppliedStyle` 只算可变 presentation metadata；因此 5K/7K 的同 topology style change 与 P1/P2 visual reorder 可发布，而 neutral validator 会接受的 9K BMS→PMS 仍由 native gate 拒绝。mania projection 只接受防御性复制的 ordered stage-column vector 并自行生成 canonical topology；同 shape 重建可发布，4→5 与 `[4,5]→[5,4]` 原子拒绝。两条 ruleset wrapper 均为 internal，未接 playfield、renderer、event producer 或 `SkinManager`。

第十四切最终 shared owner+transition **20/20**、shared gameplay **243/243**、BMS topology **34/34** 且 full **1132/1132**、mania topology **16/16** 且 full **822/826**；mania 非零仍只有恢复基线同名 4 个 HoldNote auto-frame 期待，core skin **57/62** 仍是 1 项 Argon 与 4 项已删 ruleset archive 基线失败。Release Rebuild **0 error / 20 warnings**；保留 9 条 MessagePack `NU1902` 在 restore/build 重复显示和 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。三轮独立审查均为 0 blocker / 0 major；审查要求的 native exception source、canonical mania projection、BMS style-only 与 9K reject 后 revision fixture 已补齐。

第十一切在实际 `LegacyManiaSkinDecoder` output 上为九个 primitive scalar 保存 decoder 成功接受时的 declaration 与转换/规范化后值，并由 public、process-local factory 生成 source-specific immutable bucket snapshot。缺 bucket 为外层 `Absent`，显式空 bucket 的九字段均为内层 `Absent`，显式默认仍为 `Declared`；factory 不读取之后可变的 native public 值，因此 decode 后的 mutation 不会篡改 accepted provenance。当前未覆盖五组数组/per-index mask、颜色、global resource、`NoteBodyStyle`、范围/finite validation、完整 neutral config、shared codec、真实文件 materialization 或生产 adapter。malformed pending line、malformed/duplicate `Keys` 的既有 parser 行为没有顺手改变，仍须另立 shared codec/diagnostic 决议。

第十一切最终 focused 18/18、连同 legacy decoder/declaration 30/30；扩大回归为 shared gameplay 235/235、mania relevant 120/120、BMS relevant 108/108，core skin 57/62 仍是恢复审计同名 5 项。最终 `osu.Desktop.slnf` Release 0 error / 20 warnings；只保留 9 条 MessagePack `NU1902`（restore/build 重复为 18）与 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。多轮 owning-project formatter/verify 均 exit 0，仅显示已知 workspace-load 概括 warning；独立审查 0 blocker / 0 major / 0 minor。

第十二切为 `ColumnLineWidth`、`ColumnSpacing`、`ColumnWidth`、`LightingNWidth → ExplosionWidth`、`LightingLWidth → HoldNoteLightWidth` 建立逐 source index 的 accepted declaration/value sidecar 与 source-specific immutable snapshot。长度固定为 `Keys+1 / Keys-1 / Keys / Keys / Keys`；line width 不缩放，其余四组保留现有 `×1.6` compatibility conversion。短数组尾部保持 `Absent`，空/invalid item 是现有 decoder 已接受的 `Declared(0)`，超长尾忽略，重复短行只覆盖其 prefix，未覆盖的先前 declaration 保留。snapshot 防御性复制且不把 boundary/gap index 冒充 stable lane ID，也不提前派生左右 spacing 或 explosion/light scale。

第十二切最终 focused 20/20、连同 scalar/legacy decoder/declaration 50/50；扩大回归为 shared gameplay 235/235、mania relevant 120/120、BMS relevant 108/108，core skin 57/62 仍是同名既有 5 项。`osu.Desktop.slnf` Release 0 error / 20 warnings；最终告警仍只有 18 条重复显示的 MessagePack `NU1902` 与 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。独立实现审查初次 0 blocker / 0 major / 2 minor，补 int-max overflow 与 accessor defensive-copy fixture 并收窄异常原子性措辞后收口。

第十三切只为现有 production lookup 已消费的四个 exact legacy mania global colour 建立 decoder-time accepted declaration/value sidecar 与 source-specific immutable snapshot：`ColourColumnLine`、`ColourJudgementLine`、`ColourBreak`、`ColourBarline`。RGB 三分量保留 alpha 255，RGBA/alpha 0 原样保留；不提前做 doubled-alpha、零 alpha 修正、默认回落或视觉验证。只有 `HandleColours()` 成功返回后才捕获，malformed 不创建或覆盖 sidecar，valid duplicate 保持 last accepted；factory 不从之后可变的 `CustomColours` 反推，因此 clear/overwrite/manual mutation 均不能伪造 provenance。

该 closed snapshot 不公开 raw key、字符串 lookup 或字典，也明确排除 `Colour{n}`、`ColourLight{n}` 与任意扩展 `Colour*`；这些键仍按原 decoder 进入 compatibility dictionary，但 lane colour taxonomy、stable lane mapping 与完整 neutral colour schema 尚未冻结。malformed colour 使 `flushPendingLines()` 在 clear 前退出的既有坏行为未修复也未提升为 V1 合同。第十三切 focused 15/15、连同 scalar/array/decoder/declaration 65/65；shared gameplay 235/235、mania relevant 120/120、BMS relevant 108/108，core skin 57/62 仍为同名既有 5 项。额外 mania 全量 815/819 的 4 项仍是恢复基线同组 HoldNote auto-frame 期待；Release Rebuild 0 error / 20 warnings。告警仅为 9 条 MessagePack `NU1902` 在 restore/build 重复和 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`；独立审查 0 blocker / 0 major / 0 minor。

第十切新增 public、process-local 的 neutral topology-preserving transition validator。它要求 group/lane ID 集合、group logical index，以及 lane group membership、role、global/group-local logical index 保持不变，同时明确允许 group side 和全部 visual index/order 改变。BMS 5K/7K P1↔P2 与 mania 同 stage 独立重建通过，5K→7K 与 mania 4K→5K 拒绝；9K BMS/PMS 因 neutral shape 相同会通过，native keymode 连续性仍由外层 projection/context 负责。它不携带 keymode/style/action/source/geometry/revision，也未接 production producer、layout context、event、renderer 或 `SkinManager`。

第十切实际重跑 transition 12/12、shared gameplay 235/235、mania relevant 120/120、BMS relevant 108/108、core skin 57/62 与 Release 0 error / 20 warnings；core 的 1 项 Argon 旧期待和 4 项已删除 ruleset archive 失败与恢复审计同名。第九切 BMS resolution 55/55 与 BMS 全量 1117/1117 是上一切已记录结果，本切未重跑 BMS 全量。最终构建仍只有 18 条重复显示的 MessagePack `NU1902` 和 BMS tests 既有 `CS8600`/`CA2007`，没有新增编译告警，未使用 `NoWarn`。

第九切新增 internal selected-package candidate provider adapter。factory 只按 plan 顺序发出 canonical marker 之前的 candidates；完整调用方仍显式组合 beatmap-local → selected candidates → ruleset resources → fake `oms-simple`，不会由 adapter 伪造 canonical authority。缺字段直接 `Inherit` 且不调用 owner；显式声明先变成包含 source/Keys/lane/field 的进程内 reference，只有 revision-scoped owner 已构造、持有并完成基础验证的组件才可能 `Provide`。空值、缺文件、构造失败、额外 validator 拒绝/异常均记录结构化诊断后逐字段继续，取消异常传播，ini candidate 永不产生 `Suppress`。winner 与 rejected component 都只借自同一 revision owner；失败 reload 只 dispose 新 provisional owner，成功替换先 detach superseded consumer 再 dispose 旧 owner。当前只有 internal interface/fake owner 和 fixture，没有真实 `.osk` 文件 containment/解码/纹理预算、production materializer、Drawable parenting/thread affinity 或原子 reload；复用的是既有 shared resolver，不是新的生产 resolver。

第八切新增 closed process-local 六字段 catalog：note、LN head/body/tail、key up/down；声明被防御性复制到绑定 exact immutable topology 的确定性快照，缺 bucket/字段为 `Absent`，显式空 bucket/资源名仍为 `Declared`，安全字符串不展开资源名。跨 ruleset 的 public legacy mania factory 只作 CLR bridge，不是作者/plugin/manifest ABI；它和 internal mania/BMS adapter 都只读实际 decoder output，不经过会合成默认 bucket 的 `LegacySkin` lookup。BMS ordered plan 固定 5K `Bms→Keys6→Keys5→marker`、7K `Bms→Keys8→Keys7→marker`、9K BMS/PMS `Bms→Keys9→marker`、14K `Bms→Keys16→同一 Keys8 双 deck 投影→Keys14→marker`，P2/CenterRightScratch 按 visual index 投影而 stable ID/action 不变。当前未版本化 9K production token 实际是 `0..8`；V1 canonical `1..9` 必须另做版本化迁移/冲突诊断，不能静默双 alias。末端只是 `Absent` canonical marker，没有真实 `oms-simple` package、生产资源验证或接线。

第七切新增 opaque `GameplaySkinCapabilityId`、只读 request/diagnostic/negotiation snapshot 与 internal pure negotiator。request、closed allowlist definition、host feature availability 和当前 skin authorization snapshot 四者任何单项都不能产生 grant；hard-denied authority 优先于 fake allowlist/support/authorization，unknown 不动态注册，host unavailable 优先于 stale grant，允许的 per-skin capability 必须存在当前授权。28 个明确 token、其 descendant、gameplay terminal mutation action 与 Realm/config/network/arbitrary-filesystem/reflection/process/thread/native family 均 hard deny；classifier 只看末端 action，`reset/seek/create/update` 等事件名位于前序 segment 且以 `.read` 结束时不会误杀。结果拒绝 hard-denied grant、同 ID grant+deny、重复 denial 和 code/ID 不一致，且只携带 decision，不携带 service/delegate/authority handle。当前没有 production definition；测试仅使用临时 fake definitions（含只读 token 分类 fixture），没有真实 capability、manifest/package identity/授权持久化/UI、required/optional、layer activation/version、host-call gate、VM 或 sandbox；重新协商只证明 revocation snapshot 变化，不证明运行层会原子停用旧实例。

第六切新增非 generic、只读的 `GameplaySkinEventEnvelope`，固定 `apiVersion/epoch/sequence/gameplayTime/layoutRevision` 与 `Snapshot/Reset/Edge` 投递类别；payload 只能由 shared engine hierarchy 定义，envelope 只由内部 dispatcher 边界构造。internal cursor 只校验 capability/family filtering 前的完整 canonical stream：首次中途 attach 可用完整 Snapshot 建立任意非负 high-water，之后 epoch 与同 epoch sequence 必须连续；gameplay time 同 epoch 非递减但允许负 lead-in 与同时间，Reset 在下一 epoch 的 sequence 0 原子重锚，layout revision 全流不回退且 Edge 不得先行切 revision。任何拒绝不推进 cursor，也不排序、补洞或修复。最终 event 23/23、shared gameplay aggregate 120/120、provider authority 6/6、mania relevant 108/108 + legacy decoder 7/7、BMS relevant 78/78 + transformer/fallback 104/104；core skin 57/62 仍是同名既有 5 项，Release Rebuild 0 error / 20 warnings。每个测试/构建保留 9 条 MessagePack `NU1902`，BMS 编译另保留既有 `CS8600`/`CA2007`，未使用 `NoWarn`。这些 fixture 不能证明真实 reload/seek/retry producer 或 Snapshot/Reset 完整 payload，具体 event families、dispatch、连续采样与生产 host 仍未实施。

第五切新增 default=`Absent` 的 `GameplaySkinConfigurationDeclaration<T>`；显式 `false`、`0`、空字符串与显式空 bucket 保持 `Declared`，但 declaration 不等于 slot `Provide`、配置有效或 `Suppress`。internal mania/BMS adapter 只从真实 decoder 产出的 bucket 选择 immutable key marker，不经过会为缺失 `[Mania] Keys:` 合成默认对象的 `LegacySkin` lookup，也不把 mutable native configuration 穿过 neutral 边界。最终相关回归为 shared 97/97、legacy mania decoder 7/7、mania relevant 113/113、BMS relevant 71/71 与 transformer/fallback 104/104、provider authority 6/6；core skin 57/62 仍为同名既有 5 项，Release Rebuild 0 error / 20 warnings。

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
- 生产 `LegacySkin` lookup 仍会为缺失 `[Mania] Keys:` bucket 合成默认 configuration；九个 scalar、五组 array、四项 global colour、exact per-column colour、exact 十三项 bucket-global/non-column resource-name、两侧六类 lane-resource、legacy mania `NoteBodyStyle`、native `[Bms]` exact 22-colour 与 exact 12-geometry 已有 decoder-time provenance，但任意扩展 colour/resource key 与 production validation/materialization 仍未进入 closed schema。第二十切只让 exact geometry snapshot 脱离 mutable `Geometry`，并原样保存 parser-accepted 非法域值；它不是 validated layout。第十九切只让 exact colour snapshot 脱离 mutable `Colours`，两类 comma-composite 均仍只留在 production compatibility view。
- 当前未版本化 9K BMS/PMS per-lane 资源使用 raw token `0..8`，与 V1 canonical 作者目标 `1..9` 不同；两套编号的 `1..8` 含义重叠，必须通过版本化迁移和诊断解决，不能在 production lookup 静默同时接受。
- semantic catalog 的未知 ID 目前只会由 `TryGet()` 拒绝，尚无 manifest parser/作者诊断接线；旧 raw resolver 仍是 uncatalogued compatibility 入口，生产接线必须只走 descriptor overload。
- catalogued 诊断的 context/exception 已从 JSON 与安全 `ToString()` 排除，但 `ProviderName` 的隐私仍依赖 provider 遵守“非敏感 authority 名、不得含绝对路径”合同。
- neutral topology aggregate 与 validator 仍只认识 neutral shape；第十四切以 internal BMS exact keymode / mania ordered stage vector 外壳关闭首个 native-context 盲区并发布 process-local topology revision，但尚无 production attachment/lifecycle、event `layoutRevision` 盖章、thread safety、style/action/source、geometry、wire ABI、完整 `GameplaySkinLayoutContext` 或生产接线。
- event foundation 目前只有空 fixture payload 可验证 header/ownership/order；无法证明 lifecycle bridge 会在 attach/reload/seek/retry 的真实时点发布完整 Snapshot/Reset，也没有 lifecycle/layout/input/object/judgement/score/timing/BGA payload、连续 scratch/scroll 节流、结构化 runtime fault isolation 或生产 dispatcher。现有 `GameplayClockContainer.OnSeek` 无 reason/time 且 `Reset()` 同样经过 `Seek()`，不得直接冒充 producer。
- capability foundation 目前没有任何真实 production allowlist entry；ID/request/diagnostic 不是 manifest、持久化或 script ABI，per-skin authorization snapshot 也没有 package identity 绑定、存储、查询/UI 或原子撤销。`Granted` 只是纯决策且不是 authority handle，未来每个 host API 仍须重新做实际 runtime gate；hard-deny catalog/classifier 只是 closed allowlist 后的第二道屏障，不是对任意同义词的穷举。
- 皮肤几何值无完整合法域校验；playfield、gauge/combo 与 BGA 尚未消费同一 resolved descriptor，极端值会脱节或重叠。
- sparse 7K/9K chart 可能因未使用高位 channel 被 keymode 启发式低估；布局正确性必须以前置解析诊断/override 为条件。
- 设置文案仍写 `14K→中缝`，当前代码实际为四角四 player；两者都不是 V1 authority，发布前必须统一到 descriptor。
- `buildLaneKeysoundTimelines()` 的 lane 上界仍错误使用 `GetKeyCount()`；5K/7K 最右键与 14K 右侧末键/第二皿存在 timeline 丢失风险。
- 只测 parser/类型或孤立接口不能证明真实选择链、事件顺序、脚本安全和视觉正确。
- 14K 四角 BGA、程序化动态件和内部固定动画都不能被提前描述为 V1 最终方向。
- 当前程序化 `OmsSkin` 仍是实际链底；在 `oms-simple` 完整性、自动恢复和 mania/BMS parity 过门前不能直接删除，但它也不能进入 V1 最终发行架构。
- resolver 不拥有候选组件生命周期：第九切已冻结首个 BMS 六字段 revision-owner 借用合同与 active/provisional 隔离，但 concrete production owner、Drawable parenting/thread affinity、缓存与真实 atomic swap/reload 仍未实现，resolver 也不得擅自 dispose 被额外 validator 拒绝的值。

## 下一检查点

1. 停止继续堆叠同类 source-presence 切片；先按产品纵切只读审计“真实文件组件受控选择 + 用户包缺失/损坏时逐组件 fallback”从现状到首个可验证闭环的最短依赖路径，明确 package-scoped authority、组件 validation/materialization、三态 adapter 与 `oms-simple` 责任边界后，再选择一个最小实现切片。
2. 该审计不得写成生产接线已开始或承诺直接交付：完整 `GameplaySkinLayoutContext`、neutral geometry descriptor/solver、production revision/event producer、lifecycle dispatch、shared codec/结构化诊断、真实 G1、manifest/runtime gate、scene/script 与两个外部包仍属后续；前二十个合同切片不等于整个 `SV1-1` 完成，Skin V1 新增可见功能仍为 0。
3. 在另立并过门的生产纵切前保持 `SkinManager`、nullable `ISkin`、程序化 `OmsSkin` 与当前 fallback authority 不变；`oms-simple` 尚未成为真实 provider，G1 仍按 `SV1-2` 独立重做。
