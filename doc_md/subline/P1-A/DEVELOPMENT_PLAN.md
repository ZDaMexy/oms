# P1-A 开发计划：皮肤系统 V1、产品面与 release gate

> 最后更新：2026-07-15
> 主线总规划见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，硬约束见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)，恢复证据见 [SKIN_SYSTEM_RECOVERY_20260710.md](../../other/SKIN_SYSTEM_RECOVERY_20260710.md)，本轮架构审计见 [SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md](../../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

## 当前专题定位

P1-A 当前主任务是完成 OMS 皮肤系统第一个可交付版本（下称 Skin V1）。它同时拥有：

- 共享 `.osk` package/fallback/selection 边界与 OMS 内置包生命周期；
- mania 与 BMS gameplay skin 的共同运行时合同；
- BMS playfield/BGA 布局与皮肤挂点边界；
- G1 皮肤存储、选择、热重载的产品安全门；
- 最终 skin/release 验收矩阵。

规则语义仍由各子线拥有：判定/反馈归 P1-C/P1-E，输入归 P1-B/P1-D，BGA 时间线归 P1-L，存储经验由 P1-H 提供。P1-A 只把它们投影成只读皮肤状态/事件，不能修改其 authority。

## Skin V1 完成目标

Skin V1 不是“内置更多固定 BMS 视觉”，而是同一套公开外部皮肤运行时同时支持：

1. **`oms-simple` 极简下限**：只保留可读 lane/scratch、下落 note/LN/mine、判定位置和启用中的 cover 几何；key animation、judgement、combo、gauge、装饰等可显式关闭。
2. **`oms-complex` 丰富上限**：皮肤作者使用素材、声明式 scene/animation 和可选沙箱脚本，对输入、scratch、note/LN、判定、gauge、BGA 等只读事件作出表现响应；公开接口的表达力足以制作接近 IIDX 复杂度的界面。
3. **同一公开路径**：`oms-simple.osk`、`oms-complex.osk` 和第三方皮肤全部使用相同公开 API。最终 fallback 是随发行物只读、完整验证的 `oms-simple.osk`；程序化 `OmsSkin` 只是迁移期实现，V1 release 前必须退出产品渲染链。
4. **布局正确**：5K/7K 的 P1 左、P2 右、居中左皿、居中右皿，以及 9K BMS/PMS、14K DP 的 playfield/BGA bounds、lane role 与 safe viewport 均由引擎正确求解。
5. **兼容 mania**：BMS 与 mania 重合的 ini 名称、值类型、素材解析、帧命名和缺省语义使用同一 codec/resolver；BMS 只扩展 scratch/side/DP/gauge/BGA/gimmick 等独有语义。
6. **osu 社区式生态**：`.osk` 是标准分发物，根目录 `skin.ini` 和既有 mania 素材/动画命名保持兼容；作者可解包成普通文件夹编辑、重新打包并拖入导入。OMS 扩展必须版本化且不要求编译 DLL。

不属于 V1 的承诺：解析 `.lr2skin` / `.luaskin` / `.cim`、兼容 beatoraja/LR2/IIDX 文件格式、捆绑商业素材、允许脚本修改输入/判定/计分/谱面/BGA 时间线。

## 已冻结的架构决议

### 共享什么

- `skin.ini` 基础 tokenizer、颜色/数值/数组、素材路径、动画帧序列和诊断；
- gameplay scene node、tween/state-machine、生命周期、hot reload；
- versioned state/event ABI；
- `Provide / Inherit / Suppress` 三态解析；
- 沙箱权限、资源预算、异常熔断和逐组件 fallback；
- lane group / lane role / side / stable ID 等规则集无关 DTO。

### 分开什么

- mania adapter：`ManiaBeatmap` stage/column、legacy 480 坐标、mania action/result 映射；
- BMS adapter：5K/7K/9K/14K topology、scratch/DP、lane cover、gauge、BGA、STOP/scroll/gimmick；
- playfield/BGA layout solver 与实际 gameplay truth 始终在各 ruleset/引擎侧。

BMS 不直接继承 `ManiaLegacySkinTransformer`、`Column` 或 mania Drawable。共享的是中立 codec/runtime/ABI，不是带 ruleset 假设的具体渲染类。

## 当前可信基线

### 已保留

- `.osk` 导入与 `BmsLegacySkin : LegacySkin`，可同时解析 `[Mania]` 与 `[Bms]`；
- BMS F1 现存静态件的颜色/纹理/几何和 reference ini 自校验；
- `BmsSkinTransformer` 的 component lookup 与当前程序化 fallback（仅迁移基线，不是最终产品合同）；
- 5K/7K/9K/14K lane topology、5K/7K style 和 BGA host；
- G1 folder constructor、`SkinInfo.FilesystemStoragePath` / `IsExternalFilesystemStorage` 与 schema 56。

### 不计入当前能力

- G1 生产扫描/选择/删改/热重载；
- 事故期 F2 动态件、Lua、mania fallback adapter、reference-default；
- stage/key area 的 BMS 生产 renderer；
- 外部 scene/event/script runtime；
- 14K 四角 BGA 作为最终合同。

## 强制实施顺序

### SV1-0：恢复与数据安全门

状态：**已完成**。自动 focused、schema 56 数据安全与用户实机 gate 均已通过。

1. 用户验收无外部皮肤、`.osk`、partial fallback、5K/7K/9K/14K、14K S1/S2 双皿与 mania/BMS 资源隔离。已于 2026-07-14 由用户自行完成并确认全部正常。
2. 只读报告 schema 56 中 folder-backed `SkinInfo`、authority、目录存在性和当前选择状态。已完成：folder-backed/external/path conflict 均为 0，但两条记录引用已删除的异常期类型；见 [数据安全门报告](../../other/SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)。
3. 由用户选择异常 managed 记录的保全后重导入、继续保留或保全后移除方案；已选择并完成“保全后定点移除”，同时显式修正 OMS fixed-ID 记录。
4. 未经备份、用户决定与迁移设计，不改写 Realm、不清理 `chartskin/`、不降低 schema，也不以普通启动的静默 protected-record 重写替代迁移证明。

验收：恢复测试基线稳定；数据报告与用户批准的保全/迁移闭环已完成；用户实机清单已通过。`SV1-0` 闭门，后续全局人工结果仍由 P1-G 汇总。

### SV1-1：共同合同与 fixture 冻结

状态：进行中。三态 gameplay slot/fallback/precedence、semantic slot taxonomy、neutral lane identity、immutable lane topology projection/保持性 transition validation、configuration bucket explicit-presence、legacy mania 九个 primitive scalar、五组 indexed array、四项 known global colour 及 exact `Colour{n}` / `ColourLight{n}` per-column colour accepted-declaration snapshot、gameplay event envelope/order、capability negotiation/禁止 authority、六类 lane-resource neutral snapshot/BMS→mania 候选链、internal 逐字段 resolution/revision-owner，以及 topology publication/native-context continuity 十五个切片已完成；未接生产选择链，full layout/geometry、production revision/event/wire、完整 field-level config/validation、具体 event payload/producer、真实 capability catalog/manifest/runtime 等条目仍待续。

1. 建立 neutral `GameplaySkinLayoutContext`、lane group/role/side/stable ID DTO 草案。**identity + topology projection/保持性 validation + topology publication/native continuity 已完成**：强类型 GroupId/LaneId、immutable snapshot/index/order/lookup 与 transition validator 已固定；新增 topology-only publication 及 process-local monotonic owner，首发 revision 0、后续 checked `+1`，validation/comparer/overflow 失败不推进。internal BMS 以 exact keymode 区分 neutral shape 相同的 9K BMS/PMS，style 可变；mania 以 exact ordered stage-column vector 维护 continuity，并只从该向量构造 canonical topology。完整 `GameplaySkinLayoutContext`、bounds/geometry、action/source、production `layoutRevision`/event/wire 与 adapter 接线仍待后续。
2. 冻结 `Provide / Inherit / Suppress` 语义和不可 suppress 的最小可玩组件。**合同/fixture 层已完成**：首切固定三态、坏 `Provide`/provider/validator 失败诊断、optional `Suppress` 与 fake `oms-simple` 末端；第二切固定 26 个内部 semantic slot family（7 critical / 19 optional）、descriptor requirement authority、稳定诊断 ID 及 context 分离。它不是 manifest ABI，生产接线仍待后续。
3. 以真实 mania skin.ini fixture 固定 tokenizer、数组、动画帧、错误/诊断语义。
4. 固定 BMS compatibility mapping。**六类 lane-resource 候选链与 process-local resolution 地基已完成**：5K 为 `[Bms] → Keys:6 → Keys:5 → canonical marker`，7K 为 `[Bms] → Keys:8 → Keys:7 → marker`，9K BMS/PMS 为 `[Bms] → Keys:9 → marker` 且不重复同一 key-only bucket，14K 为 `[Bms] → Keys:16 → 同一 Keys:8 bucket 按两个 deck 分别投影 → Keys:14 普通键 → marker`。internal adapter 按该顺序发出 marker 前的 selected providers，逐字段借既有 shared resolver fail-open；P2/CenterRightScratch 使用 visual index、stable lane ID/action 不变，marker 不由 factory 伪造或装载 `oms-simple`。
5. neutral config 保存 explicit declaration/presence；legacy 自动合成的默认 bucket 不得误判为 `Provide`。**bucket + legacy mania 九个 primitive scalar/五组 indexed array/四项 exact known global colour/exact per-column colour accepted snapshot + 六类 lane-resource field/resolution foundation 已完成**：scalar/array/global-colour/per-column-colour sidecar 在 decoder 成功接受时捕获 presence/value，之后 native mutation 不改变 provenance；exact `Colour{n}` / `ColourLight{n}` 可绑定 immutable topology，并由 mania `GlobalLogicalIndex`、BMS full `GlobalVisualIndex`、14K 双 deck 的 `Keys:8` `GroupLocalVisualIndex` 或 key-only 非 scratch visual enumeration 显式映射到 stable lane。note、LN head/body/tail、key up/down 已投影到 immutable snapshot，但现有 lane-resource factory 在调用时仍读取公开可变 `ImageLookups`，因此只证明 snapshot 创建后不漂移，不能作为创建前 accepted provenance；下一切先为 exact 13 项 known global resource 建立 decoder-time sidecar，随后加固六类 lane resource 的 `ImageLookups` provenance。declaration 仍不等于验证成功或 `Suppress`；任意扩展 colour、`NoteBodyStyle`、真实文件 validation/materialization、malformed diagnostics、完整 neutral config/shared codec 与生产 adapter 仍待后续。
6. 用 fixture 冻结 `BeatmapSkinProvidingContainer` 与 `RulesetSkinProvidingContainer` 的既有相对 authority；三态只接管 gameplay package slot，`Suppress` 默认不得穿透更高优先 beatmap-local provider。**首切与第九切已完成合同证据**：fixture 固定 beatmap-local → selected → ruleset resources → protected built-in，并验证先命中的 beatmap `Provide` 或 optional `Suppress` 都不会被 selected adapter 穿透。
7. 建立事件 envelope：`apiVersion/epoch/sequence/gameplayTime/layoutRevision`，以及 attach/reload snapshot、seek/retry reset 与 edge 事件顺序。**envelope/order foundation 已完成**：非 generic engine-owned payload hierarchy 与内部构造的 immutable envelope 固定 `Snapshot/Reset/Edge` 类别；canonical pre-filter cursor 允许首次完整 Snapshot 从任意非负 mid-session high-water attach，之后要求 epoch 与同 epoch sequence 连续、time 非递减、layout revision 不回退，Reset 以新 epoch sequence 0 完整重锚，Edge 只能引用当前 revision，拒绝不推进状态也不排序/修复。当前 fixture 只验证 header/category/order；真实完整 Snapshot/Reset payload、lifecycle/layout/input/object/judgement/score/timing/BGA families、producer/dispatch、连续采样与 production host 仍待后续。
8. 建立禁止写入的 gameplay authority 列表和 capability negotiation 草案。**process-local foundation 已完成**：opaque stable ID、显式 request、closed allowlist definition、host feature availability、per-skin authorization snapshot 与 immutable negotiation decision 已分离；判定顺序固定为 hard deny → unknown → host unavailable → per-skin authorization missing → grant。28 个明确 authority token、其后代与保留的 gameplay terminal mutation action，以及 Realm/config/network/arbitrary filesystem/reflection/process/thread/native family 均不可被 fake allowlist/support/grant 覆盖；只读 event token 不因中间出现 reset/seek/update 名称而误杀。当前没有真实可请求 capability、manifest mapping、package identity/授权存储/UI、required/optional 与 layer activation/version/runtime gate。

验收：前十五个合同切片已通过 capability 91/91、shared gameplay 总集 250/250（其中 event envelope/cursor 23/23、transition validator + topology revision owner 20/20）、config presence shared/mania/BMS 5/5、13/13、9/9、legacy mania scalar/array/global-colour/per-column-colour snapshot 18/18、20/20、15/15、18/18（config aggregate 83/83）、lane-resource snapshot/candidate 12/12、6/6、29/29 与 resolution/owner 55/55。第十五切新增 mania/BMS per-column mapping 5/5、14/14，并复核旧 BMS candidate mapping 29/29；BMS full 1146/1146，mania full 827/831 仅既有 4 个 HoldNote auto-frame 期待失败，core skin 57/62 仍是同名既有 5 项，Release Rebuild 0 error / 20 warnings。保留 9 条 MessagePack `NU1902` 在 restore/build 重复显示和 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`；生产数据零写入，也没有接入 production lookup、renderer 或 `SkinManager`。这不等于 `SV1-1` 整体完成；full layout/geometry、production revision/event/wire、任意扩展颜色、global resources/`NoteBodyStyle`、真实文件 validation/materialization/shared codec、concrete payload/producer、真实 manifest/runtime enforcement 与 adapter 接线仍待后续 fixture。

### SV1-2：G1 安全存储与原子重载

状态：重新设计中；只有 ctor/schema 载体可信。

1. managed 与 external authority 分离；external absolute root 使用 `NativeStorage` 且永久只读。
2. 所有 managed rename/delete/import 做 resolved-root containment、冲突拒绝和 reparse-point/symlink 风险处理。
3. `SkinManager.GetSkin` 从正确 authority 建立 folder store；非 folder 与 `.osk` 路径字节一致。
4. scanner 只维护自己拥有的记录，不删除 `.osk`、未知来源或另一 authority 的 Realm 记录。
5. reload 覆盖 ini/manifest/script/素材变化和原子替换；新实例完整验证后一次切换。
6. UI 明确区分 managed 删除文件与 external 解除注册。

验收：真实 `SkinManager`/选择链、重启、切换、rename/delete、文件缺失、原子替换和用户备份数据根测试。

### SV1-3：playfield/BGA layout descriptor

状态：neutral lane identity/order projection 可复用；统一 geometry descriptor、production snapshot 与完整矩阵未开始。

1. 先冻结 keymode source/diagnostic/override；sparse 5K/7K/9K 无法确认时不得静默用错误布局。
2. 共享层定义 neutral `GameplaySkinLayoutContext`；BMS adapter 产出唯一 `BmsGameplayLayoutSnapshot`，包含 player side/style、playfield/stage/lane/judgement/cover/BGA/HUD rect、logical/visual lane index、source channel/action/role。
3. playfield、gauge、combo fallback、BGA 与外部 scene/script 全部消费同一 snapshot；禁止各自重新 `CreateDefault()` 推导几何。
4. 对 skin geometry 做 finite/正值/范围/屏内/不重叠校验；非法字段逐项回落默认，不能让 0/负/NaN/超屏值进入 lane 除法或 viewport。
5. 5K/7K 覆盖 P1/P2/CenterP1/CenterP2；style 只改视觉顺序/停靠，不改 binding authority。CenterP1 默认 BGA 对侧为右，CenterP2 为左。
6. 9K BMS/PMS 强制 center，但 context 必须区分两种模式。
7. 14K 明确两个 8-lane deck、S1/S2 外缘、centre gap 和单一 BGA content authority。
8. 移除“皮肤自己创建 BGA player”的目标语义：引擎播放/seek/POOR，皮肤只装饰/裁剪只读 content surface。
9. 当前四角四 BGA player 降为临时实现；经视觉 fixture 决定一个 content surface 对一个或多个只读 mirror viewport 的最终布局。
10. topology smoke 覆盖每条 visible/LN/invisible/mine/keysound lane，防止 key count/lane count 边界再次丢最右轨或第二皿。

验收：每个矩阵格锁 lane order/bounds/scratch role/BGA/gauge/combo 不遮挡/时序不变；16:9、16:10、21:9、4:3 与 DPI 实机截图、每轨输入/keysound、BGA 播放通过。

### SV1-4：mania-compatible ini 共同层

状态：shared codec 未开始；`SV1-1` 已提供 bucket explicit-presence、六类 lane-resource snapshot、BMS→mania 候选计划及 process-local resolution/revision-owner 前置合同，但完整 neutral configuration、真实文件加载/验证/materialization、shared codec 与生产 fallback 迁移尚未开始。

1. adapter-first：先由现有 legacy mania/BMS decoder 导出带 explicit presence 的 neutral snapshot；fixture 稳定后再抽 shared codec，不第一刀切换 mania 生产 tokenizer。
2. `[Bms]` 重合键使用同一 codec；BMS 独有字段由 extension schema 解析。
3. 统一 0-based mania column、BMS `S/S2` 与内部 stable lane ID；renderer 不再拼接 lane 字符串。
4. 当 `[Bms]` 未覆盖共同件时，按 SV1-1 mapping 进入显式 mania compatibility fallback。
5. 统一未知键、非法值、缺素材和不支持能力的结构化诊断；加载永不阻断游玩。

验收：同一 fixture 在 mania/BMS 共同件上解析一致；旧 `.osk/[Bms]` reference 继续可用。

### SV1-5：声明式 scene、动画与事件 ABI

状态：生产实现未开始；只有 `SV1-1` 的 process-local event envelope/order 前置 fixture，尚无具体 payload family、producer/dispatch、scene consumer 或脚本 ABI。

1. 文件皮肤可声明 sprite/container/text/mask、allowlisted blend/effect preset、clip、frame animation、tween、状态机、typed property binding 和 variant/template；任意自定义 shader 不作为 V1 必需面。
2. scene renderer 区分 global nodes、lane template、pooled note/LN template 和 pooled ephemeral effect；note scrolling/LN clipping/instancing 仍由引擎 host 驱动，脚本不得逐帧创建/移动谱面对象。
3. manifest 使用稳定 node type ID + allowlist，不复用会序列化 CLR `Type`/反射实例化的 Skin Layout Editor JSON。
4. ruleset adapter 发布 lifecycle/layout/input/object/judgement/score/timing/BGA 只读事件；连续 scratch/scroll 使用固定采样/节流合同。
5. 皮肤节点只能锚定 descriptor slot 或自身 scene；禁止遍历 `DrawableRuleset` 父树。
6. 新 gameplay provider 用平行 `SkinSlotResult<T>`（或等价显式类型）承载三态，不把 `Drawable.Empty()` 或 nullable `ISkin` 强行改义为 `Suppress`。
7. 动态视觉不再要求每件新增 `DefaultBmsXxxDisplay`；先证明通用 ABI 缺口，才能新增引擎专用 host。
8. 受信任 C# provider 留作开发扩展，但第三方可分发皮肤不依赖它。

验收：不写新主题专用 ruleset visual class，仅用通用 host + scene/事件实现 key press、hit explosion、LN hold、judgement、combo/gauge 与 BGA frame 装饰；dense/14K 不出现 per-note script churn。

### SV1-6：可选沙箱脚本

状态：未开始；脚本引擎选型须先做隔离/性能 spike。

1. 脚本只读 snapshot/事件，只能创建或更新获准视觉节点、启动动画。
2. package 声明 capability；高风险能力不提供，允许的可选能力须有 per-skin 用户授权与可撤销记录。
3. 禁止网络、任意文件、反射、进程、线程、原生库、写 Realm/配置和 gameplay mutation。
4. 时间来自 gameplay clock，随机数使用确定性 seed；定义 seek/retry/reload 状态重建。
5. VM 必须支持可抢占的 instruction quota/heap quota；只在回调返回后测 stopwatch 不能防 `while true`。
6. 限制 package 总字节、单资源解码像素、总 decoded bytes/纹理、atlas、帧数、scene/effect pool 节点和每帧预算；异常或超限只熔断脚本/scene 层并 fallback。
7. 编译与 IO 不阻塞 update thread；热重载以新实例原子替换。

验收：权限逃逸负测、无限循环/内存/异常熔断、replay determinism、seek/retry、热重载和低端硬件预算通过。

### SV1-7：`oms-simple` / `oms-complex`、作者套件与 release gate

状态：未开始。

1. `oms-simple.osk`：一个同时包含 mania 与 BMS 的普通社区包，只显示最小可玩件并显式 suppress 所有可选视觉；它同时是最终逐组件 fallback。
2. `oms-complex.osk`：一个同时包含 mania 与 BMS 的普通社区包，覆盖全部公开 slot/event，证明接近 IIDX 复杂度的表达上限，但只使用原创/可分发素材。
3. 两包均保留可编辑源目录并通过普通 `.osk` 导入/导出链；不得使用隐藏资源、私有 C# provider 或内置专权。
4. `oms-simple` 的 canonical copy 随发行物只读携带、构建期与启动期校验、原子恢复。用户所选包缺失/损坏关键件时逐组件回落到它；若 canonical copy 自身完整性失败，进入明确的安装修复错误，不生成程序化视觉。
5. 当前程序化 `OmsSkin` 在 `oms-simple` 达到 mania/BMS parity 后退出产品渲染链；引擎代码只保留通用 renderer、layout/event bridge、资源隔离与 gameplay truth。
6. 交付 Skin Authoring Kit：两包源目录、带注释 `skin.ini`/manifest 模板、元素与事件/布局/预算规范、验证器与 `.osk` 打包说明。它是制作者文档和模板，不是另一种皮肤运行时。
7. BMS/mania/core skin/Release、启动/切换/reload、5K/7K/9K/14K、BGA、脚本性能和人工视觉全部过门。

验收：缺失/损坏用户 package 仍由 `oms-simple` 可玩；`oms-simple` 不被 fallback 补出可选件；`oms-complex` 不使用私有接口；产品渲染链不存在主题化程序化 fallback。

## 组件 ownership

| 面 | 引擎 authority | 外部皮肤 authority |
| --- | --- | --- |
| lane topology/order/action | 是 | 只读 role/bounds/context |
| playfield bounds、判定位置、scroll timing | 是 | slot 内长相与装饰 |
| note/LN 时间与状态 | 是 | 素材、scene、动画、事件响应 |
| BGA decode/timeline/seek/POOR | 是 | viewport frame、mask、opacity、装饰和允许的 mirror 表现 |
| input/judgement/score/gauge | 是 | 只读事件与视觉表达 |
| combo/judgement/gauge 是否显示 | 语义存在 | 可 `Provide/Inherit/Suppress` |
| fallback/sandbox/budget | 是 | 不能绕过 |
| 文件素材/manifest/script | 加载与隔离 | 内容 |

第九切只在合同 fixture 中冻结首个 selected-package revision ownership：同一 revision materialize 的 winner/rejected components 由一个 owner 统一持有，resolver/consumer 只借用；失败 provisional revision 只销毁自身，成功原子替换必须先 detach 旧 consumer 再 dispose superseded owner。它不是 `SV1-2` 原子 reload 实现，也没有 concrete production owner、Drawable parenting/thread affinity 或缓存接线。

第十切只冻结前后 neutral topology snapshot 的保持性校验：exact group/lane ID set、group logical index、lane membership/role/global 与 group-local logical index 必须稳定；group side 和全部 visual index/order 可变。调用方仍须先验证 native keymode/context，因此 9K BMS/PMS 的相同 neutral shape 会被接受；它不是 revision producer、完整 layout transition 或 wire ABI，也未接生产 adapter。

第十一切只冻结 legacy mania decoder 对九个 primitive scalar 的 accepted declaration/value provenance。sidecar 保存既有转换和规范化结果，不从 synthetic native defaults 反推 presence，也不因 decode 后的 public native mutation 漂移；source-specific snapshot 不是完整 neutral configuration。五组数组需要 per-index mask，颜色/global resources/`NoteBodyStyle`、finite/range validation、malformed diagnostics 与 shared codec 必须分切处理；不得借本切改变 pending-line、malformed/duplicate `Keys` 的既有 parser 行为或接入生产 lookup。

第十二切只冻结 legacy mania decoder 五组 indexed array 的 per-index accepted declaration/value provenance：cardinality 是 `Keys+1 / Keys-1 / Keys / Keys / Keys`，line width 不缩放，其余四组保留 `×1.6`；短数组尾部 `Absent`，空/invalid item `Declared(0)`，超长尾忽略，重复短行只覆盖 prefix。source index 不等于 stable lane ID，snapshot 不派生左右 spacing、explosion/light scale，也不做 finite/range/layout validation；颜色/global resources/`NoteBodyStyle`、shared codec、malformed diagnostics 与生产接线继续分切。

第十三切只冻结四个现有 production consumer 已使用的 exact legacy mania global colour：`ColourColumnLine`、`ColourJudgementLine`、`ColourBreak`、`ColourBarline`。sidecar 仅在既有 `HandleColours()` 成功接受 RGB/RGBA 后保存 parser value；不提前 doubled alpha、修正 zero alpha、回落默认色或做视觉验证。public source-specific snapshot 是固定四属性的 closed surface，不公开 raw key/dictionary/string lookup；`Colour{n}`、`ColourLight{n}` 及其它以 exact 大写 `Colour` 前缀开头的非四项 key 继续留在 compatibility dictionary，但不进入本切合同，lowercase `colour*` 仍按旧 decoder 忽略。lane colour taxonomy、stable lane mapping、完整 neutral colour schema、shared codec/malformed diagnostics 与生产接线继续分切。

第十五切只冻结 exact legacy mania `Colour{n}` / `ColourLight{n}` 的 decoder-time accepted provenance 与 topology-bound neutral lane colour snapshot。source-column→stable-lane mapping 必须由 ruleset 显式提供：mania 使用 `GlobalLogicalIndex`；BMS full visual 使用 `GlobalVisualIndex`，14K deck 使用 `GroupLocalVisualIndex` 且两个 deck 可共享 source index，key-only 使用非 scratch visual enumeration。三个 BMS projection 当前保持 fixture-only 且彼此独立；若未来合并到同一 candidate plan，必须共享同一个 exact topology reference。该切未开放任意 `Colour*` dictionary ABI，未接 production lookup、renderer、`SkinManager` 或 fallback；下一切按顺序为 exact 13 项 known global resource（`LightingN`、`LightingL`、`StageLeft`、`StageRight`、`StageBottom`、`StageLight`、`StageHint`、`Hit0`、`Hit50`、`Hit100`、`Hit200`、`Hit300`、`Hit300g`）建立 decoder-time sidecar，之后加固六类 lane resource 的 `ImageLookups` provenance。

## 验证矩阵

| 变更面 | 自动 gate | 人工 gate |
| --- | --- | --- |
| shared ini codec | mania legacy fixtures + BMS compatibility fixtures | 代表性 `.osk` 对照 |
| layout descriptor | 5K/7K 四 style + 9K BMS/PMS + 14K bounds/role/BGA | 16:9、16:10、超宽与 DPI |
| scene/event ABI | event order/payload/version/fallback | press/hit/LN/scratch/judge/gauge 观感 |
| G1/reload | authority/containment/reparse/conflict/atomic replace | 备份数据根、重启、编辑中切换 |
| sandbox | capability denial/budget/determinism/exception | 长时间游玩与 profiler |
| release | BMS + mania + core skin + Release | `oms-simple` + `oms-complex` + 第三方 `.osk` |

## 兼容与迁移

- 当前 `.osk/[Mania]` 与 `.osk/[Bms]` 均保留；新 scene/script 是可选增强，不做一次性格式切换。
- 对齐 osu 社区的是包、ini、素材命名、动画序列、解包编辑/拖入导入与缺项兼容心智；BMS/scene/script 是 OMS 对一个第一类社区 ruleset 的版本化扩展，不虚构为上游 osu! 已支持的格式。
- 事故期归档只能定点借鉴接口/测试，不得整包 cherry-pick/apply。
- 旧 F1/F2/F3/G1/G2 术语只在 CHANGELOG/恢复审计中作为历史索引；当前执行只看 `SV1-*`。
- P1-A 的 onboarding、settings trim、BMS→mania 公开入口、HUD/gauge 既有合同保持维护状态；除非阻塞 Skin V1/release，不抢占上述顺序。
