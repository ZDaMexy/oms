# P1-A 技术约束：Skin V1、产品面与 release gate

> 最后更新：2026-07-14
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
3. mania adapter 拥有 stage/column、legacy 480 坐标、mania action/result 映射；BMS adapter 拥有 scratch/DP、lane cover、gauge、BGA、STOP/scroll/gimmick 映射。
4. BMS 不得直接继承/复用 `ManiaLegacySkinTransformer`、`Column` 或 mania Drawable 作为生产架构。可以复用其 fixture、共同 codec 和 neutral runtime。
5. 新动态视觉优先由通用 event/scene 能力表达。在 ABI 缺口经 fixture 证明前，不得继续按“一件效果一个 `DefaultBmsXxxDisplay` + 私有 interface”扩张。

## ini 兼容约束

1. `[Mania]` 现有语法与素材包必须兼容；抽 shared codec 时不得无迁移改变既有用户皮肤行为。
2. BMS 与 mania 重合的字段使用相同键名、值类型、数组/颜色语义、资源名和 `name-0`/`name-1` 帧序列。`[Bms]` 只定义 scratch/side/DP/gauge/BGA/gimmick 等真正独有字段。
3. `[Bms]` 共同字段和 `[Mania]` 必须进入同一个 neutral configuration model，不得只复制 enum/key 名。
4. BMS compatibility 优先级固定为：`[Bms]` role-aware override → full visual-lane bucket（5K+S→`Keys:6`、7K+S→`Keys:8`、9K→`Keys:9`、14K+2S→`Keys:16`）→ key-only bucket（`Keys:5/7/14` 只映普通键、scratch `Inherit`）/14K 显式双 `Keys:8` deck → `oms-simple`。P2/CenterP2 的 visual index 与 stable lane ID 必须由 fixture 固定，不能靠 renderer 猜。
5. mania 0-based column、当前 BMS `S/S2`/数字 token 必须在 adapter 边界转换成 stable lane ID；renderer/scene 不得继续自行拼 lane key。
6. BMS 现有 `PlayfieldWidth/Height`、normal/scratch width/spacing 等 F1 字段作为兼容输入保留，但最终映射须进入 engine layout descriptor，不允许脚本直接改时序。
7. `HitTargetVerticalOffset` 继续锁 0，直到独立专题证明 `scrollLengthRatio`、GN、判定窗口和 replay 不变量；不能通过皮肤偷偷改变。
8. 加载行为必须 fail-open 并产生结构化诊断：未知键、非法值、缺素材、不支持 capability 和 fallback 来源可查询。当前“静默跳过”只算恢复基线，不算 V1 完成。
9. neutral config 必须保留 explicit declaration/presence。legacy mania 在缺失 `Keys:` bucket 时生成的默认 configuration 不能视为 `Provide`，也不能遮住后层 `oms-simple`。
10. shared codec 采用 adapter-first 迁移：先让现有 legacy decoder 导出 neutral snapshot 并以 fixture 锁定，再替换共同解析；不得一次同时改 tokenizer、mania 生产链和 BMS mapping。

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
6. 连续 scratch/scroll 采用固定采样/节流/合并合同；脚本不得要求每个原始轴采样或每帧谱面对象位置回调。
7. 事件必须按 gameplay clock 排序且 payload 不可变；脚本不得反查 `DrawableBmsRuleset`、遍历父节点或订阅内部 bindable。
8. replay、seek、retry、pause 和 hot reload 必须定义状态重建；随机数只来自引擎提供的确定性 seed。
9. 脚本 VM 必须支持可抢占 instruction quota 与 heap quota；只在回调返回后检查 stopwatch 无法防无限循环，不可作为安全预算。
10. package/runtime 限制总压缩/解压字节、单资源解码像素、总 decoded bytes/纹理/atlas/动画帧、global/lane/note/effect 节点和每帧预算；超限/异常只熔断脚本/scene/对应组件，不能中断 gameplay。
11. 脚本 capability 必须声明；允许的可选能力须 per-skin 显式授权、可查询、可撤销。网络/任意文件/反射/进程/线程/原生库/gameplay writes 永不授权。
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
