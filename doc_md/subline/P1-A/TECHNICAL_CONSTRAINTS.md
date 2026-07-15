# P1-A 技术约束：Skin V1、产品面与 release gate

> 最后更新：2026-07-15
> 本文件是 Skin V1 的硬约束源。执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，设计证据见 [SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md](../../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。若代码与本文冲突，先确认新事实并同步修正文档/代码，不能用历史 CHANGELOG 覆盖当前 authority。

## 归线与产品边界

1. P1-A 拥有 shared skin package/runtime/fallback、BMS playfield/BGA skin boundary、G1 和 release gate；判定/反馈、输入、BGA 时间线、存储底层语义仍分别归 P1-C/P1-E、P1-B/P1-D、P1-L、P1-H。
2. 不得借 Skin V1 提前带入完整 FHS、dan、1P/2P binding flip、BSS/MSS、联网或其它 Phase 2/3 功能。5K/7K 的 P1/P2/center style 是视觉 lane order/停靠，不是 binding flip。
3. `osu.Game` 不得新增对 `osu.Game.Rulesets.Bms` 的编译期依赖。共享合同只能包含 ruleset-neutral DTO/runtime；BMS/mania 具体适配留在各自 ruleset。
4. BMS→mania 公开入口、映射语义和 Skin V1 是不同专题。P1-A 只拥有入口/文案/presentation gate，source keymode→mania mapping 仍归 P1-K。
5. 首次启动向导、settings trim 等既有产品面继续遵守 desktop-only 装配、OMS localisation 和模块缺失优雅退化约束；它们不因 Skin V1 重写。

## 既有产品面硬约束（未被 Skin V1 取代）

1. 当前 `GN / WN` 只能表述为 OMS 当前 `Normal / Floating / Classic Hi-Speed + Sudden / Hidden / Lift` runtime surface 的反馈，不得对外宣称完整 IIDX `FHS`。
2. settings 可显示 Hi-Speed 模式、当前模式值和未启用 `Sudden / Hidden / Lift` 时的基础下落时间；不得在 settings 显示 runtime-adjusted `GreenNumber`/可见毫秒或暗示完整 BPM 补偿/FHS。
3. `Lift` 是 geometry control，`Hidden` 是下遮挡；命名、状态、HUD 与 pre-start overlay 不得混写。
4. Hi-Speed 范围保持 `Normal 1.0–20.0`、`Floating 0.5–10.0`、`Classic 0.5–10.0`；Classic 映射保持 `TimeRange = (100000 / 13) / HS`，`HS 10 + WN 350 => GN 300` 必须成立。
5. ruleset runtime geometry profile 继续冻结；`Playfield Scale` 固定 `1.0` 且不可配置。除 `Sudden/Hidden/Lift` 与 5K/7K `Playfield Style` 外，旧 playfield/receptor/bar-line config 不得作为用户可见 runtime contract 影响速度/几何。Skin package geometry 是独立、受 descriptor/合法域约束的作者面，不能借此恢复旧 runtime sliders。
6. `UI_PreStartHold` 继续承担“前 5 秒阻止开始 + 全程调速修饰键”；`UI_LaneCoverFocus` 是 click-to-cycle 持久 target。`READY HOLD` 只用于阻止开谱窗口，`BMS speed` toast 在 hold 调速期间持续可见。
7. BMS mod/config 记忆必须 ruleset-local；不得隐式共享到 mania/全局 `SelectedMods`。
8. 冷启动不得在 `RulesetConfigCache` ready 前调用 `GetConfigFor()` 构建 BMS mod persistence；先允许无 config 首轮 apply，再在 cache ready 后 replay 当前 ruleset restore。
9. 实现 `IPreserveSettingsWhenDisabled` 的 configurable BMS mod 停用只表示 inactive；除显式 reset/迁移外，不得清空最后配置。
10. `首次启动向导`、`Run setup wizard` 与无谱面引导归 P1-A；复用的存储/输入语义仍归 P1-H/P1-B。
11. 共享 onboarding 调用 BMS-only 能力时，`osu.Game` 不得直接引用 BMS ruleset；模块缺失须优雅退化。
12. 首次启动向导 OMS 文案必须使用 OMS-owned localisation namespace + `.resx`；只改 `*Strings.cs` fallback 不足以覆盖非英文资源。
13. desktop settings 隐藏 upstream tablet/touch/mouse subsection 只能在 `OsuGameDesktop` 等 desktop host 完成，不得下移为 `OsuGameBase` 全宿主行为。
14. mania settings 的滚动速度毫秒只代表标准车道几何参考下落时间，不是跨皮肤/跨 ruleset 体感合同。
15. `BMS -> mania` 公开口径只能是 `BMS source -> mania target`，不能暗示 generic convert；P1-A 只拥有入口/文案/presentation/unavailable feedback，mapping/flatten/scratch 退化/空结果仍归 P1-K/K9。
16. unsupported/invalid BMS→mania case 必须隐藏或明确不可用，不能显示可点击空壳。
17. playfield 顶边默认贴屏幕顶边；当前默认 `PlayfieldHeight=0.92`，`HitTargetVerticalOffset=0`，保持 `scrollLengthRatio == 1`、TimeRange/GN/判定窗口不受场高改变。不得用整体下移或 HUD safe-area inset 破坏该合同；改变 descriptor 时须同步 lane/layout/GN 测试。

## 核心 ownership

### 引擎必须拥有

1. 谱面、输入、判定、计分、gauge、scroll/STOP/gimmick、BGA timeline/seek/POOR 的真实状态和时钟。
2. lane topology/order/action、playfield group/bounds、judgement position、BGA safe viewport 和 gameplay clock。
3. 外部 package 的资源解析、权限隔离、版本协商、预算、错误熔断、fallback 与原子 reload。
4. 对皮肤发布不可变、版本化、只读的 layout/state/event DTO。

### 外部皮肤可以拥有

1. slot 内的 sprite/container/text/mask、素材、颜色、混合、裁剪和标准视觉效果。
2. 帧动画、tween、timeline、状态机和对只读 gameplay event 的表现响应。
3. 可选组件是否显示，以及在 descriptor slot 内的局部布局/装饰。
4. 可选沙箱脚本，但只能调用获准的 scene/animation API。

### 外部皮肤不得拥有

1. lane order/action、判定线时序、scroll transform、BGA playback clock 或任何 gameplay mutation。
2. 输入注入、判定/计分/gauge 写入、谱面/Realm/配置写入。
3. 网络、任意文件系统、反射、进程、线程、原生库和未声明宿主能力。

## 共享与分离约束

1. mania/BMS 共同的 ini tokenizer、颜色/数值/数组、资源路径、帧序列、诊断、scene node、animation/state-machine、event envelope、fallback 和 sandbox 必须共享实现，不得继续维护两套近似 codec。
2. 共享层只能认识 `LaneGroupId`、`LaneId`、`LaneRole`、`Side`、bounds、capability 和 stable event DTO；不得出现 `BmsKeymode`、`ManiaAction`、`BmsGaugeProcessor`、`DrawableRuleset` 等具体类型。
2a. shared identity 使用强类型 `GameplaySkinLaneGroupId` / `GameplaySkinLaneId`，值必须是非敏感的小写 ASCII 点分 opaque topology token 并按 ordinal 比较，不得嵌入用户、包、资源名或路径信息。GroupId 在单一 topology 内不得分配给两个不同 semantic group，LaneId 跨该 topology 全部 group 不得分配给两个不同 semantic lane；同一语义实体可跨不改变 topology 的 revision 重建 identity，但必须复用相同 ID。consumer 不得从 visual order、geometry、本地化文本或 ruleset CLR enum 反推 ID，也不得把进程内 hash 持久化。
2b. stable ID 跨 P1/P2/center presentation、视觉重排、geometry、skin reload 和不改变 topology 的 layout revision 保持不变；同 LaneId 的 group membership 与 role 不得漂移。跨这类 revision 关联比较 `Id`，而 group/lane identity 的完整值相等包含当前 neutral metadata；`GameplaySkinLaneTopologySnapshot` 负责拒绝单 snapshot 内的重复 ID 或 membership metadata 冲突，transition validator 只验证调用方已声明为 topology-preserving 的两个 neutral snapshot，不替代 native context/revision authority。
2c. `GameplaySkinLaneRole` 固定为 `Key/SpecialKey/Scratch`（另有必须拒绝的默认 `Unspecified`）。mania internal projection 必须将 odd-stage 的 stage-local centre 映为 `SpecialKey`，但它仍是 key input、绝不能映为 `Scratch`；note/LN/mine 是对象类型，不是 lane role。
2d. `GameplaySkinLaneSide` 固定为 `Neutral/Primary/Secondary`（另有必须拒绝的默认 `Unspecified`），表示逻辑 player/deck presentation side，绝不等同屏幕 Left/Right、BGA side 或 input binding owner。5K/7K P1/CenterP1 为 Primary、P2/CenterP2 为 Secondary，9K BMS/PMS 为 Neutral，14K 两 deck 分别 Primary/Secondary；style 改变 side 时 stable ID/action 不变。
2e. identity primitive 不携带 index。`GameplaySkinLaneTopologyEntry` 显式承载零基 `GlobalLogicalIndex`、`GroupLocalLogicalIndex`、`GlobalVisualIndex`、`GroupLocalVisualIndex`；group 承载零基 logical/visual group index；snapshot 提供 immutable logical/visual 有序视图与强类型 ID lookup。该 snapshot 故意不携带 keymode/style、action/source channel、bounds/rect、geometry、revision 或 native context，也不是完整 `GameplaySkinLayoutContext`、manifest/event/JSON ABI。
2f. topology snapshot/group 必须非空且防御性复制输入；拒绝 null、负 index、重复 GroupId/LaneId、lane 与 containing group identity 不一致、非 `0..count-1` permutation、group-local 与对应 global order 不一致，以及 group 在全局 logical 或 visual 序列中非连续块。所有 index 均为当前 snapshot 的 0-based order 数据，不得升级成 stable identity。
2g. BMS internal projection 只能以 `BmsLaneLayout` 为 authority：`Lanes` 仍按 logical `LaneIndex` 存储，resolved left-to-right order 只读 `Lane.VisualIndex`，不得从枚举位置或 geometry 反推。canonical token 固定为 `bms.group.deck-1/2`、`bms.lane.scratch-1/2`、`bms.lane.key-1..14`；5K/7K 为 `S1,K1..`，9K BMS/PMS 为 `K1..K9`，14K 为 `S1,K1..K14,S2`。unknown enum、非 canonical lane count 或 action/scratch composition 必须拒绝。
2h. BMS 5K/7K 的 P1/Center group side 为 Primary、P2/CenterRightScratch 为 Secondary；皿左右重排只通过 visual index 表达，不得改变 logical index、action 或 binding authority。9K BMS/PMS 的 neutral topology 相同、side 为 Neutral、applied style 为 Center，但 internal native keymode 必须保持区分；14K 为两个各 8-lane 的 Primary/Secondary group，S1 global/local=`0/0`、S2=`15/7`。
2i. topology-preserving validator 必须要求前后 GroupId/LaneId 集合完全相同、每个 group 的 logical index 稳定，并要求每个 lane 的 group ID、role、global logical index 与 group-local logical index 稳定；group side、group visual index 与 lane global/group-local visual index 可变。不得比较完整 group/lane identity equality，也不得把 9K BMS/PMS 等相同 neutral shape 的通过写成 native keymode、完整 `GameplaySkinLayoutContext`、revision 或 wire transition 已验证。
2j. mania internal projection 先防御性复制 stage authority，只接受 1–2 stage、每 stage 1–`ManiaRuleset.MAX_STAGE_KEYS`；single group side 为 Neutral，dual stage 0/1 为 Primary/Secondary。token 固定为 `mania.group.stage-1/2` 与全局前缀序 `mania.lane.column-N`；global index 用 stage column count 前缀和，group-local index 用 stage-local ordinal，当前 visual=logical；odd-stage special 判定必须使用 stage-local index。
2k. `GameplaySkinLaneTopologyPublication` 只发布一个 immutable neutral topology 与 process-local revision；首个成功值为 0，后续成功值 checked `+1`。owner 必须先通过 exact immutable/non-sensitive native-context comparator，再通过 neutral transition validator 与 overflow 检查，全部成功后才替换 `Current`；mismatch、comparer 异常、neutral rejection 与 overflow 均不得推进。它不是 package revision、event `layoutRevision`、serialisation/wire ABI、thread-safe owner 或 security boundary。
2l. ruleset native continuity 留在 internal adapter：BMS owner 以 exact `BmsKeymode` 为 authority，`AppliedStyle` 只算 presentation metadata，可在 neutral transition 允许时改变；9K BMS/PMS 即使 neutral validator 接受也必须由 native gate 拒绝。mania owner 以 exact ordered stage-column vector 为 authority，4→5、stage count 变化与 `[4,5]→[5,4]` 必须拒绝；projection 不接受外部 topology 参数，只能从已复制向量生成 canonical topology。ruleset wrapper 不得上升为 shared CLR 或作者 ABI。
3. mania adapter 拥有 stage/column、legacy 480 坐标、mania action/result 映射；BMS adapter 拥有 scratch/DP、lane cover、gauge、BGA、STOP/scroll/gimmick 映射。
4. BMS 不得直接继承/复用 `ManiaLegacySkinTransformer`、`Column` 或 mania Drawable 作为生产架构。可以复用其 fixture、共同 codec 和 neutral runtime。
5. 新动态视觉优先由通用 event/scene 能力表达。在 ABI 缺口经 fixture 证明前，不得继续按“一件效果一个 `DefaultBmsXxxDisplay` + 私有 interface”扩张。

## ini 兼容约束

1. `[Mania]` 现有语法与素材包必须兼容；抽 shared codec 时不得无迁移改变既有用户皮肤行为。
2. BMS 与 mania 重合的字段使用相同键名、值类型、数组/颜色语义、资源名和 `name-0`/`name-1` 帧序列。`[Bms]` 只定义 scratch/side/DP/gauge/BGA/gimmick 等真正独有字段。
3. `[Bms]` 共同字段和 `[Mania]` 必须进入同一个 neutral configuration model，不得只复制 enum/key 名。
4. BMS compatibility 优先级固定为：5K `[Bms] → Keys:6 → Keys:5 → canonical marker`；7K `[Bms] → Keys:8 → Keys:7 → marker`；9K BMS/PMS `[Bms] → Keys:9 → marker`，不得重复制造同一 `Keys:9` key-only candidate；14K `[Bms] → Keys:16 → 同一个真实 Keys:8 bucket 按两个 engine-owned deck 分别投影 → Keys:14 普通键 → marker`。14K deck-local `Keys:8` 必须先于 `Keys:14`，以保留双 scratch 与 deck-local presentation；当前末端 marker 只能是 `Absent` 的未来 `oms-simple` authority 标记，不得伪造已装载 package。P2/CenterRightScratch 的 visual index 与 stable lane ID/action 必须由 fixture 固定，不能靠 renderer 猜。
5. mania 0-based column、当前 BMS `S/S2`/数字 token 必须在 adapter 边界转换成 stable lane ID；renderer/scene 不得继续自行拼 lane key。当前未版本化 `[Bms]` production lookup 对非 scratch 沿用 raw logical lane index：5K/7K/14K 因 scratch 占 index 0 而得到 `1..`，无 scratch 的 9K BMS/PMS 实际得到 `0..8`；这不改变 internal stable lane ID `K1..K9`。V1 canonical 作者 token 目标 `1..9` 必须经显式格式版本、迁移与冲突诊断引入，禁止同时静默接受 `0..8`/`1..9` 两套重叠别名。
6. BMS 现有 `PlayfieldWidth/Height`、normal/scratch width/spacing 等 F1 字段作为兼容输入保留，但最终映射须进入 engine layout descriptor，不允许脚本直接改时序。
7. `HitTargetVerticalOffset` 继续锁 0，直到独立专题证明 `scrollLengthRatio`、GN、判定窗口和 replay 不变量；不能通过皮肤偷偷改变。
8. 加载行为必须 fail-open 并产生结构化诊断：未知键、非法值、缺素材、不支持 capability 和 fallback 来源可查询。当前“静默跳过”只算恢复基线，不算 V1 完成。
9. neutral config 必须保留 explicit declaration/presence：presence carrier 的默认值是 `Absent`，显式 bucket 即使为空也为 `Declared`；declaration 只表示来源事实，不等于 slot `Provide`、验证成功或 `Suppress`。通用 carrier 必须区分显式 `false`、`0`、空字符串与缺失。legacy mania primitive scalar/indexed-array/known-global-colour/per-column-colour/`NoteBodyStyle` sidecar 必须在 decoder 成功转换、规范化、颜色解析或 enum 解析时同时捕获 presence 与 accepted value；exact known `[Mania] Keys:` bucket-global/non-column resource-name sidecar 与 mania/BMS 六类逐 lane resource sidecar 必须在 exact key 被 decoder 接受时同时捕获 accepted string。所有 sidecar 都不得从 synthetic/effective default 或之后可变的 native public 值反推；数组必须逐 index 保存，短数组未出现尾部为 `Absent`，空/invalid item 依既有兼容规则为 `Declared(0)`。现有 field-level config 只覆盖九个 primitive scalar、五组 indexed array、四个 exact known global colour、exact `Colour{n}`/`ColourLight{n}` 两类逐列颜色、十三个 exact bucket-global resource-name、note/LN head/body/tail/key up/down 六个逐 lane 资源字段、legacy mania `NoteBodyStyle`，以及 native `[Bms]` exact canonical 二十二项 colour；任意扩展颜色、其它 arbitrary/prefix-only `ImageLookups`、native BMS 其余 geometry/resource 与完整 neutral config 仍未冻结。
10. shared codec 采用 adapter-first 迁移：mania presence 只能来自 `LegacyManiaSkinDecoder` 实际产出的 bucket，不能来自会为缺失 bucket 合成默认 configuration 的 `LegacySkin` lookup；BMS 只有 `BmsSkinDecoder` 接受并产出的有效 `Keymode` bucket 才算 `Declared`。第八切新增 decoder-output lane-resource snapshot 与有序候选计划，第九切只新增 process-local provider/resolution/owner 合同，第十一至十三切只新增 primitive scalar/indexed-array/四项 known global colour accepted-value sidecar/snapshot，第十五切只新增 per-column colour accepted-value sidecar、neutral lane-colour snapshot 与 ruleset fixture projection，第十六切只新增十三项 exact bucket-global/non-column resource-name accepted-string sidecar 与 source-specific snapshot，第十七切只把 legacy mania/native `[Bms]` 六类逐 lane resource 的两个 factory 改为读取各自 decoder-time accepted-string sidecar，第十八切只新增 legacy mania `NoteBodyStyle` decoder-time accepted-value sidecar 与 source-specific bucket snapshot，第十九切只新增 native `[Bms]` exact canonical 二十二项 colour 的 decoder-time accepted-value sidecar 与 internal source-specific bucket snapshot；均未改变 tokenizer、mania/BMS production lookup 的消费语义、renderer、`SkinManager`、真实 fallback 或 reload。array snapshot 保存 converted compatibility values 本身，不得提前把 `ColumnSpacing` 派生成左右间距或把 explosion/light width 派生成 scale，也不得将 boundary/gap source index 冒充 stable lane ID。known-global-colour snapshot 只保存 `ColourColumnLine`、`ColourJudgementLine`、`ColourBreak`、`ColourBarline` 的 parser-accepted `Color4`，不得提前 doubled alpha、修正 zero alpha、回落默认色或公开任意 raw `Colour*` key/dictionary。`flushPendingLines()` 异常前不清空坏行、malformed `Keys` 沿用旧 current config、duplicate `Keys` 写入 discarded config 等既有行为不得被本切静默修复或误写成 V1 合同，须另立 shared codec/malformed diagnostics 决议。fixture 稳定后再替换共同解析，不得把这些变更合并成一次生产切换。
10a. per-column colour 只接受 exact case-sensitive `Colour{n}` / `ColourLight{n}`，其中 `{n}` 是严格 1-based ASCII decimal token 且范围为 `1..Keys`；`01`、`+1`、大小写变化、`0`、越界、后缀或任意其它 `Colour*` 均不进入 sidecar。只有既有 decoder 成功接受并解析颜色后才能捕获当时的 parser `Color4`；sidecar 与公开副本都必须 defensive copy，不得从之后可变的 `CustomColours` 反推、伪造或擦除 declaration。
10b. lane-colour field catalog 仅包含 closed process-local 的 `LaneBackground` / `LaneLight`，不是 manifest/wire ABI。snapshot 必须绑定调用时同一个 exact immutable topology，防御性复制并按 logical lane/field 确定性排序；source-column 到 stable lane 的 ruleset projection 固定为 mania `GlobalLogicalIndex`、BMS full `GlobalVisualIndex`、14K deck `GroupLocalVisualIndex`（两个 deck 共享同一组 source index）、BMS key-only 的 non-scratch visual order。partial mapping 与多个 target lane 共享同一 source column 的 many-to-one mapping 合法；重复 target lane、target 不属于 exact topology 或 source 越界必须拒绝。当前 BMS full/deck/key-only 三个入口彼此独立且仅用于 fixture；若未来把多个投影合入同一 candidate plan，必须共享同一个 exact topology reference，禁止分别重建等价 snapshot 后混入同一计划。
10c. exact known `[Mania] Keys:` bucket-global/non-column resource-name 只包含区分大小写的 `LightingN`、`LightingL`、`StageLeft`、`StageRight`、`StageBottom`、`StageLight`、`StageHint`、`Hit0`、`Hit50`、`Hit100`、`Hit200`、`Hit300`、`Hit300g`。当前 source semantic mapping 固定为 `LightingN → ExplosionResource`、`LightingL → HoldNoteLightResource`、`StageLeft/StageRight/StageBottom → LeftStageResource/RightStageResource/BottomStageResource`、`StageLight → KeyFlashResource`、`StageHint → HitTargetResource`、`Hit0/Hit50/Hit100/Hit200/Hit300/Hit300g → MissJudgementResource/MehJudgementResource/OkJudgementResource/GoodJudgementResource/GreatJudgementResource/PerfectJudgementResource`；这些 mapping 只是 source-specific compatibility 语义，不是 neutral slot/manifest ID。exact key 仍必须写入既有 compatibility `ImageLookups`，同时保存 `SplitKeyVal` trim 后、尚未 `CleanFilename` 的 accepted string；显式 `Key:` 空值为 `Declared("")`，valid duplicate 取 last accepted。其它 broad-prefix `Hit*`/`Stage*`/`Lighting*` 可按旧 decoder 继续留在 mutable dictionary，但不得进入 closed sidecar。public snapshot 只能暴露固定十三属性，不得提供 raw string-key query/dictionary；decode 后 dictionary 的 add/replace/remove/reassign/clear 不得伪造、擦除或改变 sidecar。该 declaration 尚未经过 containment、文件存在/解码、动画帧、纹理预算、materialization 或 slot validation，不等于 `Provide`，也不是 topology、neutral resource catalog、manifest/wire ABI 或 production fallback。
10d. 六类逐 lane resource 的 decoder provenance 已在第十七切关闭 legacy mania 与 native `[Bms]` 两个 decoder→factory mutable window。legacy mania 只把区分大小写的 `NoteImage{n}`、`NoteImage{n}H/L/T`、`KeyImage{n}`、`KeyImage{n}D` 投入 sidecar，其中 `{n}` 是 `0..Keys-1` 的 canonical ASCII decimal，禁止 leading zero、符号、空 token、越界与后缀 lookalike；native `[Bms]` 只把既有 per-lane regex 接受后精确落入 note/LN head/body/tail/key up/down 六种 prefix+suffix 组合的声明投入 sidecar，并原样保存 regex 捕获的 `\d+`、`S` 或 `S2` raw token，禁止 numeric normalisation。故 9K factory 仍只查询当前 production-compatible ASCII `0..8`，14K 仍查询 `S`、ASCII `1..14`、`S2`；例如 raw `9`、`01` 或 Unicode decimal token 即使被 `[Bms]` decoder 接受并留在 sidecar，也不得静默别名到 9K stable lane。两侧 exact key 继续写入既有 public compatibility `ImageLookups`，显式空资源名保持 `Declared("")`，valid duplicate/重复 BMS bucket 保持 last accepted；其它 broad/prefix-only mania image key、BMS 错配 suffix、`LaneBackgroundImage*`、`LaneDividerImage*` 与 tokenless compatibility key 依旧遵守原 dictionary 行为，但不得进入 closed sidecar。两个 snapshot factory 只能读取 sidecar；decode 后对两侧 dictionary 内容的 add/overwrite/remove/clear、legacy dictionary 的整表 reassign 与手工 dictionary 注入均不得伪造、擦除或改变 declaration。internal accept/get 入口必须先拒绝 unknown/composite/non-canonical field、越界/非法 token 与 null resource，再同时写 compatibility view 与 sidecar，不得留下半写状态。该闭合仍不执行 containment、文件存在/解码、动画帧、纹理预算、materialization 或 slot validation，不把 declaration 提升为 `Provide`，也不冻结任意其它 `ImageLookups` 为完整 V1 config。
10e. legacy mania `NoteBodyStyle` provenance 必须只在 exact、区分大小写的 source key 进入既有 `Enum.TryParse<LegacyNoteBodyStyle>(string, out ...)` 成功路径时捕获；本阶段不得追加 `Enum.IsDefined`、canonical numeric 或 flags 检查。命名值大小写敏感，而 undefined numeric（如 `1`、`99`、`-1`）、非 canonical numeric（如 `+2`、`02`）及逗号 composite 只要被现 decoder 接受，就必须原样保存其 parsed enum value；这只表示 compatibility parser 接受，不表示 V1 validated/author-supported style，后续 validation 必须另层诊断。缺 bucket 为 outer `Absent`，显式 bucket 但缺/坏字段为 inner `Absent`，valid duplicate 为 last accepted，后续 malformed value 不得擦除先前 accepted declaration；pending、malformed/duplicate `Keys` 等既有 bucket 行为保持。factory 只能读取 decoder-time sidecar；手工设置或 decode 后 erase/alter public `NoteBodyStyle` 字段不得伪造、擦除或改变 provenance。`LegacySkin` 根据全局 Version `< 2.5` 合成 `Stretch`、否则合成 `RepeatBottom` 的 effective default 永远不是 source declaration。本切不得新增 native BMS field、改变任何 production consumer/fallback，或把 source-specific snapshot 提升为 neutral config、manifest/wire ABI。
10f. native `[Bms]` colour accepted provenance 的 closed set 固定为 exact、区分大小写的 `NoteColourWhite`、`NoteColourCyan`、`NoteColourYellow`、`NoteColourScratch`、`LaneBackgroundEvenColour`、`LaneBackgroundOddColour`、`ScratchLaneBackgroundColour`、`LaneDividerColour`、`ScratchLaneDividerColour`、`HitTargetBarColour`、`HitTargetLineColour`、`HitTargetGlowColour`、`ScratchHitTargetBarColour`、`ScratchHitTargetLineColour`、`ScratchHitTargetGlowColour`、`MajorBarLineColour`、`MinorBarLineColour`、`LaneCoverFillColour`、`LaneCoverShadeColour`、`LaneCoverFocusColour`、`PlayfieldBackdropColour`、`PlayfieldBaseplateColour`。accepted value 必须沿用既有 `tryParseColour`：恰好 3 或 4 个逗号分量，各分量经 invariant `byte.TryParse(NumberStyles.Integer)`；RGB 补 alpha 255，RGBA 原样保留 alpha（包括 0），现 parser 接受的正号、leading zero 与 `-0` 也不得在 provenance 层改写规则，错误分量数、空分量、负数、超 255、小数、hex 或非 ASCII 数字不声明。valid duplicate、重复 bucket 与同一 decoder 的 repeated `Parse` 继续 last accepted，后续 malformed value 不得擦除此前 declaration，pending-before-`Keymode` 保持现 decoder 归属。当前 `Enum.TryParse` 会让部分逗号 composite source key 折叠到已定义 lookup；这种 key 必须继续写入 public mutable `Colours` compatibility view 以保持生产行为，但绝不能进入 exact accepted sidecar，exact declaration 后的 composite overwrite 也不得改变 sidecar。factory 只读 sidecar，手工或 decode 后对 `Colours` 的 forge/overwrite/remove/clear/late-add 均不得伪造、擦除或改变 snapshot。catalog、snapshot 与 factory 保持 BMS-internal、source-specific、process-local；它们不是 neutral colour/slot/author manifest/wire ABI，不执行 contrast/theme default、renderer transformation、validation、fallback 或 production wiring，也不得借此在引擎中新增主题色或默认视觉。
11. lane-resource field catalog 是 closed process-local taxonomy，不是 manifest/serialization ABI；snapshot 必须绑定 exact immutable topology、防御性复制、确定性排序、拒绝 null/未知字段/重复 lane-field，缺项为 `Absent`、显式空资源名仍为 `Declared`，安全字符串不得展开资源名。public legacy mania factory 只因跨 ruleset 程序集桥接而公开，不得当作作者/plugin/package/script API。
12. lane-resource candidate adapter 只发出 canonical marker 前的 selected-package providers，并保持 plan 顺序；完整调用方顺序仍为 beatmap-local → selected candidates → ruleset resources → explicit canonical。lookup 必须匹配 exact topology、canonical field 与该 field 的 semantic descriptor；缺声明不得 materialize，ini candidate 不得制造 `Suppress`，取消异常不得转换为 fallback。lane-resource component owner 与第十四切 topology publication owner 是两套不同的 process-local revision 合同，均没有 concrete production lifecycle manager，不得混用 revision 语义。

## osu 社区式制作者合同

1. `.osk` 是 V1 的正式分发单位；打开/拖入即可导入，解包后是根含 `skin.ini` 的普通可编辑目录。managed/external folder 是作者工作区与高级管理面，不能取代 `.osk` 的社区交换地位。
2. `[General]`、`[Colours]`、`[Mania]` 的语法、`Keys:` 分桶、既有素材名、`name-{n}` 动画序列、资源缩放/缺项 fallback 等共同语义以当前 osu legacy compatibility 为基线；BMS 不另造一套同义基础格式。
3. `[Bms]`、declarative manifest 和 optional script 是 OMS 对第一类 BMS ruleset 的版本化扩展。扩展文件必须可被 OMS validator 识别并产生清晰诊断，不要求作者编译 DLL，也不得冒充上游 osu! 已原生支持的格式。
4. 只做 mania、只做 BMS 或同时做两者的 `.osk` 都合法；`oms-simple.osk` 与 `oms-complex.osk` 必须在一个包内同时提供 mania/BMS，并可作为第三方作者的真实参考源。
5. common mania assets/ini 在 OMS 中的行为须有代表性社区皮肤 fixture；OMS 生成的组合包若宣称 mania-compatible，也须验证其 mania 部分不会因 BMS 扩展而改变。
6. 制作者套件（Skin Authoring Kit）至少包含：两内置包的可编辑源、带注释模板、字段/素材/事件/layout/capability/budget 参考、validator/diagnostic 用法、打包与导入说明。它不是 SDK DLL，也不是第三种 package 格式。

## fallback 与最小可玩约束

1. V1 lookup 必须区分 `Provide`、`Inherit`、`Suppress`。`null`/缺文件继续表示 `Inherit`，不得同时承担作者主动关闭语义。
1a. 三态应由平行的 `SkinSlotResult<T>`/gameplay provider（或等价显式类型）承载，不直接改变现有 nullable `ISkin` ABI。`Drawable.Empty()` 不能长期冒充 `Suppress`；`Provide` 构造/资源失败必须降为 `Inherit` + 诊断。
1b. `default(SkinSlotResult<T>)` 必须是 fail-open 的 `Inherit`，默认 requirement 必须是更安全的 `Critical`。resolver 严格按调用方给定顺序解析，不得自行重排；首个有效 `Provide` 或 optional `Suppress` 终止，critical `Suppress`、provider/构造/validator 失败记录结构化诊断后继续。
1c. 结构化诊断至少包含 slot、provider 与稳定原因码；持久化或写入仓库时不得携带用户绝对资源路径。resolver 不拥有候选值生命周期，也不得自动 dispose 被 validator 拒绝的 `Drawable`/`IDisposable`：provider 必须先完成基础验证，并与最终消费方显式冻结缓存、parenting 与回收责任。
1d. `SV1-1` 内部 semantic taxonomy 固定为 26 个 slot family。critical 仅为：`playfield.lane-surface`、`playfield.judgement-line`、`object.note`、`object.long-note.head`、`object.long-note.body`、`object.mine`、`playfield.lane-cover.fill`。optional 为：`object.long-note.tail`、`playfield.key`、`effect.key-flash`、`effect.hit-explosion`、`hud.judgement`、`hud.combo`、`hud.gauge`、`hud.text`、`playfield.bar-line`、`stage.background`、`stage.foreground`、`playfield.backdrop`、`playfield.baseplate`、`playfield.lane-cover.decoration`、`playfield.turntable`、`playfield.laser`、`bga.viewport`、`bga.frame`、`decoration`。
1e. taxonomy ID 使用小写 ASCII 点分段、ordinal/case-sensitive，不从 CLR enum/type/本地化文本派生；未知或畸形 ID 不得动态注册或默认为 optional。ID 只用于内部语义与稳定诊断，当前不是作者 manifest ABI；catalog 顺序也不表示 provider precedence、绘制顺序、z-order 或 layout。
1f. lane/keymode/side/result 等 context 必须由 `GameplaySkinSlotLookup<TContext>` 与 descriptor 分离承载。catalogued resolution 的 requirement 只能来自 descriptor；旧 raw overload 只保留给 uncatalogued compatibility，未来生产接线不得用它绕过 taxonomy。
1g. LN head/body 保证必要可读性，tail cap 可 suppress。lane cover 只在玩法启用时请求 critical `fill`，该视觉必须挂在引擎强制 geometry/clip host 内，皮肤不得改变真实遮挡范围；`decoration` 仍 optional。`bga.viewport` 只呈现引擎拥有的只读 content surface，不拥有 player/timeline/clock。
1h. catalogued diagnostic 的 `SlotId` 是可持久化稳定字段；进程内 `Slot`/`Exception` 不得序列化，安全 `ToString()` 也不得展开它们。`ProviderName` 必须是非敏感 authority 名且不得含绝对路径；当前代码只靠 provider 合同约束，不能宣称自动脱敏。
1i. source-aware lane-resource reference 可以把未验证 resource name 交给 materializer，但稳定诊断、JSON 与安全字符串不得展开它；同一 raw name 在不同 source/Keys/lane/field 下必须保留 authority 区分，不能只按字符串名共享或去重。
1j. lane-resource materializer 必须在返回前由 revision owner 持有 component 并完成基础验证；winner 与被额外 validator 拒绝/异常隔离的 component 都继续归 owner，resolver/provider/consumer 不得单独 dispose。active 与 provisional owner 不得跨 revision 混用：失败 provisional 只 dispose 自身；成功替换先原子切换并 detach superseded consumer，再 dispose 旧 owner；teardown 同样先 detach 后 dispose。
1k. 第九切 revision-owner 只是 internal contract/fixture，不证明真实 `.osk` containment/存在性/解码、纹理预算、Drawable parenting/thread affinity、缓存或原子 reload 已实现；这些责任不得下沉给 shared resolver，也不得借此宣称 `SV1-2` 已开工。
2. 三态链只替换 gameplay package 内的组件解析：用户所选 `.osk` → ruleset adapter/compatibility → 随发行物只读携带的 `oms-simple.osk`。现有 `BeatmapSkinProvidingContainer` 的谱面内皮肤 enable/colour/hitsound 语义，以及 `RulesetSkinProvidingContainer` 注入 ruleset resource skin 的相对 authority，在另有迁移决议前必须保持；不得把完整现有链误写成只有上述三层。
2a. `Suppress` 默认只作用于声明它的 gameplay package slot，不得越权穿透或屏蔽更高优先级的 beatmap-local provider；若未来需要改变该边界，必须单独冻结 precedence fixture 和用户迁移规则。
2b. 测试中名为 `oms-simple` 的 fake provider 只证明 canonical 末端语义，不代表真实 `oms-simple.osk` 已制作、校验或接入生产 fallback authority。
3. 以下 gameplay-critical 元素不得被完全 suppress：lane/scratch 可辨识、note、LN、mine、判定位置，以及启用 lane-cover 玩法时的 cover 几何/遮挡。用户包缺失、损坏或非法时必须逐组件回落 `oms-simple`。
4. key/keyflash、hit explosion、judgement display、combo、gauge visual、文本 HUD、turntable/laser、BGA frame、装饰和其它非核心视觉可以显式 suppress；其 gameplay 状态仍在引擎中正常运行。
5. `BmsSkinTransformer.providesBuiltInFallbacks = skin is OmsSkin` 只记录当前迁移基线：在 `oms-simple` 达到 mania/BMS parity 前不得贸然删除；达到 parity 后必须由文件包 fallback 取代并退出产品渲染链。
6. 最终产品不得存在主题化程序化 fallback、硬编码色块/辉光或私有默认视觉。引擎代码可以并且必须保留通用 scene renderer、note/LN host、对象池、layout/event bridge、资源隔离与 gameplay truth，但所有具体颜色、素材、节点和动画来自 `.osk`。
7. `oms-simple.osk` 是不可被用户修改/删除的 canonical fallback：发行构建锁定版本/hash，启动验证并可从只读 canonical copy 原子恢复工作副本。canonical copy 自身失败属于安装完整性故障，应阻止进入 gameplay 并给出修复指引，禁止静默生成程序化视觉。
8. `oms-complex.osk` 与 `oms-simple.osk` 同权使用公共 package/scene/script API；可以作为默认选择或展示包，但不得成为 `oms-simple` 之下的隐藏第二 fallback。

## scene、事件与脚本约束

1. 声明式 scene/state-machine 是首选作者面；脚本用于声明式能力无法合理表达的组合逻辑，不能成为显示普通 note/key/judgement 的必需条件。
2. 外部拥有视觉内容不等于外部直接拥有 framework Drawable/tree。引擎必须提供通用 scene renderer、global/lane template、pooled note/LN instancing/scroll/clip host、effect pool、z-order、资源和事件调度；删除的是主题/效果硬编码，不是内部通用表现运行时。
2a. 声明式层必须有 typed property binding/variant/template，可直接表达 gauge value→clip/scale、combo value→text、result key→sprite variant 和 per-lane context；动画编译成 gameplay-clock transform，不能强迫普通 gauge/combo/judgement 使用脚本 `on_update`。
3. manifest 使用稳定 node type ID + allowlist，不得复用序列化 CLR `Type` 并反射实例化的 Skin Layout Editor JSON 作为第三方 ABI。
3a. V1 只开放 allowlisted blend/effect/shader preset；任意 shader/驱动特性不属于 V1 完成条件，后续扩展也必须 capability/version/budget 化。
4. V1 event family 至少覆盖 lifecycle、layout、input、object/LN/mine、judgement/offset、combo/score/gauge、beat/measure/BPM/STOP/scroll、BGA/POOR。
5. event envelope 至少携带 `apiVersion/epoch/sequence/gameplayTime/layoutRevision`；attach/reload 先发完整 snapshot，seek/retry 发 reset/new epoch，之后才发 edge。result 使用 neutral key + 可选 ruleset-native ID，不泄露内部 Drawable/HitObject。
5a. `GameplaySkinEventEnvelope` 是 process-local engine contract，不是 serialisation、script 或 author manifest ABI。外部 package 只能消费已验证 DTO，不能构造 envelope 或发布 gameplay truth；ruleset adapter 只能提交 neutral primitives/调用 shared concrete payload factory，shared dispatcher 独占 `apiVersion/epoch/sequence/gameplayTime/layoutRevision` 盖章。即使 friend assembly 技术上可见 internal member，也禁止直接派生 payload 或发布 envelope。
5b. `gameplayTime` 使用 gameplay clock 的毫秒域，必须 finite；lead-in/storyboard 可为负，同一时刻可有多个事件并由 sequence 定序，绝不是 wall/update clock。当前 envelope 可承载正数 future version 以便 fail-closed 拒绝，但 canonical cursor 只接受显式支持的 V1，版本不得在 attachment 内变化。
5c. payload hierarchy 由 shared engine 拥有；每个 concrete payload 必须 sealed、不可变、防御性复制集合且 ruleset-neutral，不得暴露 `Drawable`、`HitObject`、`JudgementResult`、`HitEvent`、`Bindable`、clock、Realm object 或其它可变 native state。`JudgementResult`/revert 等事实必须在 authority 回调栈内复制 primitives，不能排队保存对象引用后再读。
5d. internal cursor 只校验 capability/family filtering 前的完整 canonical stream，绝不排序、补洞、重放或自动修复。首次 attach/reload 的完整 Snapshot 可从任意非负 mid-session epoch/sequence high-water 建立状态；之后 epoch 严格 `+1`、同 epoch sequence 严格 `+1` 且 time 非递减。Reset 必须位于下一 epoch 的 sequence 0，可重锚前后跳的 time；layout revision 在 attachment 全程不回退，Snapshot/Reset 可保持或推进，Edge 必须等于当前 revision。任何拒绝都不得推进 cursor，计数器耗尽时 fail-closed，禁止 wrap。
5e. Snapshot 和 Reset 都必须原子携带完整 baseline；Reset 不是“先清空、稍后再等 Snapshot”。当前第六切只有 fake payload 的 envelope/category/order fixture，不能证明完整 baseline、真实 attach/reload/seek/retry producer 或 delivery；在 concrete payload family 与 lifecycle bridge 落地前不得把本合同写成可用 event runtime。
6. 连续 scratch/scroll 采用固定采样/节流/合并合同；脚本不得要求每个原始轴采样或每帧谱面对象位置回调。
7. 事件必须按 gameplay clock 排序且 payload 不可变；脚本不得反查 `DrawableBmsRuleset`、遍历父节点或订阅内部 bindable。
8. replay、seek、retry、pause 和 hot reload 必须定义状态重建；随机数只来自引擎提供的确定性 seed。
9. 脚本 VM 必须支持可抢占 instruction quota 与 heap quota；只在回调返回后检查 stopwatch 无法防无限循环，不可作为安全预算。
10. package/runtime 限制总压缩/解压字节、单资源解码像素、总 decoded bytes/纹理/atlas/动画帧、global/lane/note/effect 节点和每帧预算；超限/异常只熔断脚本/scene/对应组件，不能中断 gameplay。
11. 脚本 capability 必须声明；允许的可选能力须 per-skin 显式授权、可查询、可撤销。网络/任意文件/反射/进程/线程/原生库/gameplay writes 永不授权。
11a. capability grant 必须同时满足：package 显式 request、ID 存在于 engine closed allowlist、required host feature 当前可用、需要 per-skin authorization 的项已由当前 skin 获准，并且未命中 hard deny。任一单独的 request/support/grant snapshot 都不能制造权限；unknown 不动态注册，hard deny 永远优先。
11b. capability ID 是非敏感小写 ASCII opaque token，不得嵌入包名、用户数据、资源名或路径。当前 CLR carrier/diagnostic/JSON 仅为 process-local decision 与隐私 fixture，不是 manifest、持久化或 script ABI；未来 parser 必须先实施 ID 长度、request 数量与 package budget。
11c. hard-deny catalog/classifier 是 closed allowlist 后的第二道 fail-closed 屏障，不是对任意同义词的穷举。明确禁止 gameplay input/lane/action/layout/judgement-line/cover/scroll/timing/clock/judgement/score/combo/gauge/chart/beatmap/BGA mutation、Realm/config authority，以及 network/arbitrary filesystem/reflection/process/thread/native family。禁止 `gameplay.layout.write` 不等于禁止经 schema 校验的声明式 geometry；package-scoped resource read 也不等于 arbitrary filesystem。
11d. 当前内部 classifier 对明确 deny root 及 descendant、以及 terminal mutation action 生效；只读 event fixture 以 terminal `.read` 区分，因此 `reset/seek/create/update` 等事件名可出现在前序 segment。该命名规则尚不是 author ABI，未来 catalog/manifest 定稿不得把只读 event family 误分类成写 authority。
11e. negotiation result 只能携带 immutable granted ID 与结构化 denial，不得携带 delegate/service/object/authority handle；同一 ID 不得同时 grant/deny，hard-denied ID 不得进入 grant。撤销通过重新协商产生新 snapshot，但每个 future host API 仍须实际 runtime gate；本合同不证明旧 scene/script 已原子停用。
11f. 第七切没有真实 production capability、package identity、授权存储/UI、required/optional、explicit deny 状态、layer activation/fallback、protocol version 或 sandbox runtime。`NoAdditionalAuthorization` 只可用于经产品策略确认的低风险 baseline；允许的可选 package 能力仍必须 per-skin authorization。
12. 脚本编译和文件 IO 不在 update thread 执行；不得在事件回调做阻塞操作。
13. 任何脚本引擎选型先通过 preemption/capability isolation、license、Windows 打包、性能/GC 和调试诊断 spike。采用 Lua 不等于兼容 `.luaskin` 或 beatoraja runtime。
14. 受信任 C# `ISkin` provider 可留作开发/高级扩展，但用户可分发 V1 皮肤不得依赖编译 DLL。

## playfield 与 BGA 布局约束

1. shared runtime 只定义 neutral `GameplaySkinLayoutContext`；BMS adapter 必须产出唯一不可变 `BmsGameplayLayoutSnapshot`：screen/safe bounds、命名 z-layer/clip/input-pass-through、playfield/stage groups、lanes、judgement line/cover、BGA viewport、HUD anchor slots、keymode 来源、style、明确 `PlayerSide`、scroll direction，以及每轨 logical/visual index、source channel、action、role、最终 rect。
2. playfield 顶边默认贴屏幕顶边，当前 `HitTargetVerticalOffset=0` 下保持 `scrollLengthRatio == 1`。皮肤改变视觉尺寸不得隐式改变 GN/判定时序。
3. 5K/7K 必须覆盖：P1 左停靠+左皿、P2 右停靠+右皿、CenterP1 居中左皿、CenterP2 居中右皿。style 只改 visual order/anchor，不改 action binding。默认 BGA side 与 player side 对置：P1/CenterP1→右，P2/CenterP2→左；当前 CenterRightScratch 仍右上是待迁移缺口。
4. 9K BMS 与 9K PMS 均 center，但 descriptor 必须保留不同 keymode context；P1/P2 请求规范化为 center。
5. 14K 必须建模为两个 lane group：S1+K1–K7 与 K8–K14+S2，双皿位于两 deck 外缘，centre gap 明确且不改变 action identity。
6. BGA decode、timeline、seek、POOR 和唯一 content authority 归引擎。皮肤只取得只读 content surface/viewport/event，可加 frame/mask/opacity/装饰；不得自行创建独立 `BmsBgaPlayer` 或不同 clock。
7. 若 descriptor 暴露多个 BGA mirror viewport，它们必须引用同一 content authority。当前 14K 四角四 player 只是临时实现，不得冻结为 V1 合同。
8. BGA viewport、playfield 和 gameplay-critical HUD safe slot 在支持的宽高比/DPI 下不得不受控重叠；需要重叠的艺术表现只能在皮肤自有装饰层，不得遮断核心可玩层。
9. layout 变化须覆盖 5K/7K 四 style、9K BMS/PMS、14K 的自动 screen-space 测试和实机截图；不能只用 7K fixture 代表全部模式。
10. playfield、gauge、combo fallback、BGA 与 scene/script 必须消费同一 resolved snapshot；不得在各组件内按 keymode 重新建立默认 profile。skin geometry 变化以新 snapshot 原子发布。
11. geometry 值必须逐字段验证 finite、正值、合理范围、屏内与不重叠；非法/NaN/负值/零宽/超屏值逐字段回落并诊断，不得进入 `TotalRelativeWidth` 除法。
12. keymode 是 layout 前置 authority：snapshot 必须包含 detector source/diagnostic。sparse chart 不确定时使用明确纠正/override 流程，skin/layout 不得根据 lane 内容二次猜测。
13. topology 自动 gate 必须覆盖每条 visible/LN/invisible/mine/armed-keysound lane；所有边界用 lane count（含 scratch），不能用 key count 丢最右键或 14K Scratch2。

## G1 存储与恢复约束

1. 2026-06-30 00:05 之后的 G1/F2/Lua/mania adapter/reference-default 不构成当前能力，禁止整批 cherry-pick/apply；只可定点复用设计和测试教训。
2. schema 56 生产 Realm 未只读清点前，不得降 schema、自动删除/改写 folder-backed `SkinInfo` 或清理 `chartskin/`。
3. managed relative path 与 external absolute root 是不同 authority；external 必须使用 `NativeStorage` 且永久只读。
4. managed rename/delete/import 必须做 resolved-root containment、冲突拒绝和 reparse-point/symlink 风险处理；不允许仅做字符串前缀判断。
5. scanner 只能维护自己创建的 authority 记录；不得删除 `.osk`、未知来源、另一扫描根或用户手工记录。
6. folder-backed `SkinInfo.Files` 保持空；资源从正确 folder store 读取。非 folder/.osk/Oms 路径必须保持既有行为。
7. reload 覆盖 ini/scene/script/素材和原子替换；新 package 完整解析/验证成功后原子切换，失败保留旧实例并报告诊断。
8. 删除 current skin 时先安全切回 `oms-simple`；external 删除操作只能解除注册，不能写/改名/删物理目录。
9. schema 56 只读清点发现的失效 `InstantiationInfo` 不得依赖 `SkinInfo.CreateInstance()` 的历史 `TrianglesSkin` fallback 静默吞掉，也不得靠普通启动只修 fixed-ID protected 记录后宣称迁移完成。managed hash-backed 记录必须先保全内容，再按用户批准的定点方案重导入、保留或移除；scanner 不得代办该清理。Realm 写事务可能不更新文件 mtime，迁移证据必须同时验证 SHA-256 与动态 schema 状态。

## HUD 与既有迁移约束

1. 在 scene/event ABI 接管前，不得破坏现有 `IBmsHudLayoutDisplay.SetComponents(wrapped HUD, gauge, combo)` 签名；需要扩展时使用 versioned optional contract。
2. BMS 默认 gauge 在迁移期继续免疫通用 `HUDOverlay.ShowHealthBar`，保持当前可玩信息不因 NoFail 等通用开关消失。
3. wrapped HUD 中重复的 upstream combo 和 offline leaderboard 继续在装配树中移除，不得只以 `Alpha=0` 隐藏。
4. 新 feedback 不得通过遍历 wrapped HUD、偷改 gauge/combo 或 ad-hoc toast 接入；应发布 neutral event/state，由 scene/script 消费。
5. pre-start lane preview 若重启，仍属于 playfield/lane slot，不属于 HUD/toast；第一轨解释为第一非 scratch 普通轨。
6. 当前 strip 必须显式覆盖 `ComboCounter`、`LegacyDefaultComboCounter` 和 `DrawableGameplayLeaderboard`；`LegacyDefaultComboCounter` 不是 `ComboCounter` 子类，测试不得只匹配前者。
7. 当前 `BmsGaugeBar : HealthDisplay` 必须在真实 `HUDOverlay` 下压过 `ShowHealthBar=false` 的淡出并保持可见；裸 dependency container 不能作为该路径的唯一测试。

## 既有 LN 视觉状态合同

1. 在 neutral event adapter 完成前，长条 body 状态继续由 `DrawableBmsHoldNote.BodyState : IBindable<BmsLongNoteBodyState>` 暴露，状态 `Idle/Holding/Broken` 只由 hold gameplay truth 派生；默认 body 不得自行读取判定内部。
2. 当前兼容默认值保持：body width `0.5775`；`Idle==Holding` 使用 head 色、alpha `0.8`；`Broken` 去色并使用 alpha `0.32`；tail 仍 `Alpha=0`。修改须同步 `BmsSkinTransformerTest`。
3. `Broken → recover` 只允许 HCN；CN 中途松开不可接回，语义 authority 见 [P1-E 约束](../P1-E/TECHNICAL_CONSTRAINTS.md)。Skin V1 event adapter 只能投影该状态，不得重新解释 LN/CN/HCN 规则。

## 测试与发布约束

1. 任何 skin 改动至少同时覆盖用户 package、`Inherit` fallback、`Suppress`、`oms-simple`、mania 默认资源和 BMS 对应 keymode；迁移期间另保留当前 `OmsSkin` 对照，直到文件 fallback 正式接管。
2. parser/type assertion 不能替代真实 `SkinManager`、选择链、folder authority、event order 和生产 host 测试。
3. layout 最低矩阵：5K/7K × 四 style，9K BMS，9K PMS，14K；每格覆盖 lane order/bounds/scratch role/BGA viewport/gauge safe slot/时序不变。
4. sandbox 最低矩阵：权限拒绝、无限循环、内存/节点超限、异常熔断、replay determinism、seek/retry/pause/reload 和 profiler。
5. V1 release 必须有两个普通 `.osk` 公共 API 验收包：`oms-simple` 与 `oms-complex`，两者均同时包含 mania/BMS。`oms-simple` 不得被补出已显式 suppress 的可选件；`oms-complex` 不得使用私有接口。
6. BMS full、mania relevant/full、core skin focused、Release 构建和实机视觉/性能结果均需记录；已知失败必须稳定归因。
7. 文档不得把“代码 provider 可替换”“ini 可配置”“scene 可声明”“script 可编程”混成一个完成状态；能力矩阵必须分列。
8. 不得宣称 LR2/beatoraja/IIDX 文件格式兼容；“接近 IIDX 表现上限”只描述公开接口表达力。
