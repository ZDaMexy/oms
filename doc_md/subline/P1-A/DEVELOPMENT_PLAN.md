# P1-A 开发计划：皮肤系统 V1、产品面与 release gate

> 最后更新：2026-07-16
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

## 当前产品进度与暂停点

恢复后的 `.osk`/legacy mania、BMS F1 静态件与程序化 `OmsSkin` fallback 是当前可用基线，不是 Skin V1 新增交付。前二十个 `SV1-1` 切片只建立合同、fixture 与 decoder-time accepted provenance；其后首个产品纵切已让用户选中的已导入 managed `.osk` 以 BMS 普通短键 `name-{n}` 编号帧动画进入真实 gameplay。Skin V1 新增可见功能现为 **1**，自动 gate 已通过，新增动画实机待确认。

尚未交付的产品闭环包括：其它 slot 的完整三态接线、`oms-simple.osk` canonical 逐组件 fallback、安全 G1、统一 layout descriptor/solver、shared ini codec 与结构化诊断、scene/event runtime、sandbox script，以及同时含 mania/BMS 的 `oms-simple.osk` / `oms-complex.osk` 两个普通社区包。实现暂停于 `d1ea483`；下一新对话先做文档与 memory 健康治理，且不得借治理启动代码工作或提升 gate。治理完成并重新冻结执行门后，先由用户实机确认新增编号帧动画，再选择下一项玩家可见组件。

## 强制实施顺序

### SV1-0：恢复与数据安全门

状态：**已完成**。自动 focused、schema 56 数据安全与用户实机 gate 均已通过。

1. 用户验收无外部皮肤、`.osk`、partial fallback、5K/7K/9K/14K、14K S1/S2 双皿与 mania/BMS 资源隔离。已于 2026-07-14 由用户自行完成并确认全部正常。
2. 只读报告 schema 56 中 folder-backed `SkinInfo`、authority、目录存在性和当前选择状态。已完成：folder-backed/external/path conflict 均为 0，但两条记录引用已删除的异常期类型；见 [数据安全门报告](../../other/SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)。
3. 由用户选择异常 managed 记录的保全后重导入、继续保留或保全后移除方案；已选择并完成“保全后定点移除”，同时显式修正 OMS fixed-ID 记录。
4. 未经备份、用户决定与迁移设计，不改写 Realm、不清理 `chartskin/`、不降低 schema，也不以普通启动的静默 protected-record 重写替代迁移证明。

验收：恢复测试基线稳定；数据报告与用户批准的保全/迁移闭环已完成；用户实机清单已通过。`SV1-0` 闭门，后续全局人工结果仍由 P1-G 汇总。

### SV1-1：共同合同与首个产品纵切

状态：进行中。前二十个合同/provenance 切片已完成；其后第一个玩家可见纵切已把 managed `.osk` 的 native BMS 普通短键静态/编号帧素材接入真实 gameplay 与速度预览，并以 `Provide/Inherit`、逐组件 fallback、精确 package authority 和后台替换守住可玩性。只把编号帧动画计为新增功能；完整三态、其它 slot、full layout/geometry、production event/runtime、shared codec、G1 与最终包仍待续。路线仍为 `SV1-0` 完成、`SV1-1` 进行中、`SV1-2` 仅 early carrier、`SV1-3`～`SV1-7` 未实现。

1. 建立 neutral `GameplaySkinLayoutContext`、lane group/role/side/stable ID DTO 草案。**identity + topology projection/保持性 validation + topology publication/native continuity 已完成**：强类型 GroupId/LaneId、immutable snapshot/index/order/lookup 与 transition validator 已固定；新增 topology-only publication 及 process-local monotonic owner，首发 revision 0、后续 checked `+1`，validation/comparer/overflow 失败不推进。internal BMS 以 exact keymode 区分 neutral shape 相同的 9K BMS/PMS，style 可变；mania 以 exact ordered stage-column vector 维护 continuity，并只从该向量构造 canonical topology。完整 `GameplaySkinLayoutContext`、bounds/geometry、action/source、production `layoutRevision`/event/wire 与 adapter 接线仍待后续。
2. 冻结 `Provide / Inherit / Suppress` 语义和不可 suppress 的最小可玩组件。**合同/fixture 层已完成，普通短键已有首个窄接线**：managed BMS 普通短键 critical slot 已消费 `Provide/Inherit`，失败继续逐组件 fallback；作者 `Suppress`、其它 slot 和 manifest ABI 仍待后续。
3. 以真实 mania skin.ini fixture 固定 tokenizer、数组、动画帧、错误/诊断语义。
4. 固定 BMS compatibility mapping。**六类 lane-resource 候选链与 process-local resolution 地基已完成；native BMS 普通短键是首个真实文件例外**：其 exact `[Bms] NoteImage*` 已由 managed package 自身提供静态/编号帧视觉。完整 mania compatibility 候选链、其它 lane resource 与 `oms-simple` marker 仍未整体生产化。
5. neutral config 保存 explicit declaration/presence；legacy 自动合成的默认 bucket 不得误判为 `Provide`。**bucket + legacy mania 九个 primitive scalar/五组 indexed array/四项 exact known global colour/exact per-column colour/十三项 bucket-global resource-name/独立 `NoteBodyStyle` accepted snapshot + 两侧六类 lane-resource field/provenance/resolution + native `[Bms]` exact 22-colour / exact 12-geometry snapshot foundation 已完成**：各 sidecar 都在 decoder 成功接受时捕获 presence/value，之后 native mutation 不改变 provenance；exact per-column colour 可绑定 immutable topology，十三项 global resource 与两侧六类 lane-resource 保持各自 closed classifier/raw token 兼容边界。native colour/geometry 只记录 exact source key，comma-composite key 仅保留 public compatibility 行为而不进入 closed provenance；geometry 原样保存 parser 接受的负值、零、`NaN` 与无穷值。declaration 仍不等于验证成功、有效 layout、slot `Provide` 或 `Suppress`；普通短键已闭合精确 package validation/materialization/authority 窄纵切，任意扩展 colour/resource key、其它真实资源、malformed diagnostics、完整 neutral config/shared codec 与生产 adapter 仍待后续。
6. 用 fixture 冻结 `BeatmapSkinProvidingContainer` 与 `RulesetSkinProvidingContainer` 的既有相对 authority；三态只接管 gameplay package slot，`Suppress` 默认不得穿透更高优先 beatmap-local provider。**合同与首个真实纵切均有证据**：真实 Ruleset/beatmap 容器固定 beatmap-local → selected → ruleset resources → protected built-in；有效 beatmap drawable 胜出，高层损坏时 selected managed package 才接管，跨 package 同名纹理不能拼接。
7. 建立事件 envelope：`apiVersion/epoch/sequence/gameplayTime/layoutRevision`，以及 attach/reload snapshot、seek/retry reset 与 edge 事件顺序。**envelope/order foundation 已完成**：非 generic engine-owned payload hierarchy 与内部构造的 immutable envelope 固定 `Snapshot/Reset/Edge` 类别；canonical pre-filter cursor 允许首次完整 Snapshot 从任意非负 mid-session high-water attach，之后要求 epoch 与同 epoch sequence 连续、time 非递减、layout revision 不回退，Reset 以新 epoch sequence 0 完整重锚，Edge 只能引用当前 revision，拒绝不推进状态也不排序/修复。当前 fixture 只验证 header/category/order；真实完整 Snapshot/Reset payload、lifecycle/layout/input/object/judgement/score/timing/BGA families、producer/dispatch、连续采样与 production host 仍待后续。
8. 建立禁止写入的 gameplay authority 列表和 capability negotiation 草案。**process-local foundation 已完成**：opaque stable ID、显式 request、closed allowlist definition、host feature availability、per-skin authorization snapshot 与 immutable negotiation decision 已分离；判定顺序固定为 hard deny → unknown → host unavailable → per-skin authorization missing → grant。28 个明确 authority token、其后代与保留的 gameplay terminal mutation action，以及 Realm/config/network/arbitrary filesystem/reflection/process/thread/native family 均不可被 fake allowlist/support/grant 覆盖；只读 event token 不因中间出现 reset/seek/update 名称而误杀。当前没有真实可请求 capability、manifest mapping、package identity/授权存储/UI、required/optional 与 layer activation/version/runtime gate。

验收：首个产品纵切自动验收 **26/26**、相关 focused **283/283**、BMS full **1333/1333**；`osu.Desktop.slnf` Release **0 error / 20 warnings**，独立终审 blocker/major **0/0**，Markdown **119 文件 / 934 相对链接 / 0 断链**。保留 9 条 MessagePack `NU1902` 在 restore/build 重复显示和 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn`。测试只使用隔离 headless 临时存储，生产 Realm、`chartskin/`、用户皮肤目录与网络零访问、零写入。`SV1-0` 恢复实机 gate 已通过，但本次新增编号帧动画必须单独实机确认；`SV1-1` 仍未完成。

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

状态：shared codec 未开始；`SV1-1` 已提供 bucket explicit-presence、六类 lane-resource snapshot、BMS→mania 候选计划及 process-local resolution/revision-owner 前置合同，且 native BMS 普通短键已有首个 package-scoped 文件加载/验证/materialization 纵切；完整 neutral configuration、mania compatibility、其它资源、shared codec 与生产 fallback 迁移尚未开始。

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

> 以下逐切说明是待下一轮文档健康治理归档的历史快照，不是当前执行顺序；当前暂停点与下一门只以上方“当前产品进度与暂停点”为准。

第九切只在合同 fixture 中冻结首个 selected-package revision ownership：同一 revision materialize 的 winner/rejected components 由一个 owner 统一持有，resolver/consumer 只借用；失败 provisional revision 只销毁自身，成功原子替换必须先 detach 旧 consumer 再 dispose superseded owner。该切本身不是 `SV1-2` 原子 reload 实现，也没有 concrete production owner、Drawable parenting/thread affinity 或缓存接线；其后的普通短键纵切已补 concrete owner/private cache/per-component replacement，但没有把完整六字段候选链或整包原子 reload 生产化。

第十切只冻结前后 neutral topology snapshot 的保持性校验：exact group/lane ID set、group logical index、lane membership/role/global 与 group-local logical index 必须稳定；group side 和全部 visual index/order 可变。调用方仍须先验证 native keymode/context，因此 9K BMS/PMS 的相同 neutral shape 会被接受；它不是 revision producer、完整 layout transition 或 wire ABI，也未接生产 adapter。

第十一切只冻结 legacy mania decoder 对九个 primitive scalar 的 accepted declaration/value provenance。sidecar 保存既有转换和规范化结果，不从 synthetic native defaults 反推 presence，也不因 decode 后的 public native mutation 漂移；source-specific snapshot 不是完整 neutral configuration。五组数组需要 per-index mask，颜色/global resources/`NoteBodyStyle`、finite/range validation、malformed diagnostics 与 shared codec 必须分切处理；不得借本切改变 pending-line、malformed/duplicate `Keys` 的既有 parser 行为或接入生产 lookup。

第十二切只冻结 legacy mania decoder 五组 indexed array 的 per-index accepted declaration/value provenance：cardinality 是 `Keys+1 / Keys-1 / Keys / Keys / Keys`，line width 不缩放，其余四组保留 `×1.6`；短数组尾部 `Absent`，空/invalid item `Declared(0)`，超长尾忽略，重复短行只覆盖 prefix。source index 不等于 stable lane ID，snapshot 不派生左右 spacing、explosion/light scale，也不做 finite/range/layout validation；颜色/global resources/`NoteBodyStyle`、shared codec、malformed diagnostics 与生产接线继续分切。

第十三切只冻结四个现有 production consumer 已使用的 exact legacy mania global colour：`ColourColumnLine`、`ColourJudgementLine`、`ColourBreak`、`ColourBarline`。sidecar 仅在既有 `HandleColours()` 成功接受 RGB/RGBA 后保存 parser value；不提前 doubled alpha、修正 zero alpha、回落默认色或做视觉验证。public source-specific snapshot 是固定四属性的 closed surface，不公开 raw key/dictionary/string lookup；`Colour{n}`、`ColourLight{n}` 及其它以 exact 大写 `Colour` 前缀开头的非四项 key 继续留在 compatibility dictionary，但不进入本切合同，lowercase `colour*` 仍按旧 decoder 忽略。lane colour taxonomy、stable lane mapping、完整 neutral colour schema、shared codec/malformed diagnostics 与生产接线继续分切。

第十五切只冻结 exact legacy mania `Colour{n}` / `ColourLight{n}` 的 decoder-time accepted provenance 与 topology-bound neutral lane colour snapshot。source-column→stable-lane mapping 必须由 ruleset 显式提供：mania 使用 `GlobalLogicalIndex`；BMS full visual 使用 `GlobalVisualIndex`，14K deck 使用 `GroupLocalVisualIndex` 且两个 deck 可共享 source index，key-only 使用非 scratch visual enumeration。三个 BMS projection 当前保持 fixture-only 且彼此独立；若未来合并到同一 candidate plan，必须共享同一个 exact topology reference。该切未开放任意 `Colour*` dictionary ABI，未接 production lookup、renderer、`SkinManager` 或 fallback；下一切按顺序为 exact 13 项 known global resource（`LightingN`、`LightingL`、`StageLeft`、`StageRight`、`StageBottom`、`StageLight`、`StageHint`、`Hit0`、`Hit50`、`Hit100`、`Hit200`、`Hit300`、`Hit300g`）建立 decoder-time sidecar，之后加固六类 lane resource 的 `ImageLookups` provenance。

第十六切只冻结 exact `LightingN`、`LightingL`、`StageLeft`、`StageRight`、`StageBottom`、`StageLight`、`StageHint`、`Hit0`、`Hit50`、`Hit100`、`Hit200`、`Hit300`、`Hit300g` 在实际 legacy mania `[Mania] Keys:` bucket 中的 decoder-time accepted string provenance。explicit empty 保持 `Declared`，valid duplicate 保持 last accepted；unknown `Lighting*`/`Stage*`/`Hit*` broad-prefix key 继续保留既有 mutable compatibility dictionary 行为，但不进入 closed sidecar。public source-specific snapshot 将它们映射为 `ExplosionResource`、`HoldNoteLightResource`、`LeftStageResource`、`RightStageResource`、`BottomStageResource`、`KeyFlashResource`、`HitTargetResource` 与 `MissJudgementResource`/`MehJudgementResource`/`OkJudgementResource`/`GoodJudgementResource`/`GreatJudgementResource`/`PerfectJudgementResource`；factory 只读 sidecar，因此之后的 `ImageLookups` mutation 不能伪造或改写 provenance。本切不做文件验证、路径/动画解析、资源物化、production lookup/candidate/renderer/`SkinManager`/fallback 接线。下一切同时加固 legacy mania 与 native `[Bms]` 两侧六类 lane-resource 的 decoder-time provenance。

第十七切同时冻结 legacy mania 与 native `[Bms]` 两侧六类 lane-resource 的 decoder-time accepted provenance。legacy mania 只接受严格零基 ASCII canonical `NoteImage{n}`、`NoteImage{n}H/L/T`、`KeyImage{n}`、`KeyImage{n}D`；native BMS 保留 decoder 实际接受的 raw numeric/`S`/`S2` lane token，不借本切静默兼容另一套编号。两侧仍同步写既有 `ImageLookups` compatibility view，但 topology-bound snapshot factory 只读 closed sidecar，因此手工 dictionary、两侧内容 mutation 与 legacy 整表重赋值均不能伪造、擦除或改变 declaration；explicit empty 与 valid duplicate 的既有语义不变。本切不是文件 validation/materialization、shared codec、production lookup、candidate/fallback 切换、renderer 或 `SkinManager` 接线。下一切不预设实现，先只读审计剩余 `NoteBodyStyle`、资源 validation/materialization 与其它 mutable compatibility 入口，再据证据选择最小切片；`SV1-1` 仍未完成。

第十八切只冻结 exact legacy mania `NoteBodyStyle` 的 decoder-time accepted provenance，并使用独立 source-specific immutable bucket snapshot，避免扩大既有 primitive scalar carrier。sidecar 保留当前 case-sensitive `Enum.TryParse` 的 named、numeric、undefined numeric 与 comma-combined compatibility value；invalid declaration 不声明也不擦除此前成功值。factory 只读 sidecar，缺 bucket、显式空 bucket与显式 `Stretch=0` 保持 outer/inner declaration 区分，public nullable field 的后续 forge/erase/alter 不能改变 provenance。本切不读取 `[General] Version`，不把 `LegacySkin` 派生的 `Stretch`/`RepeatBottom` effective default 或 synthetic bucket 误写成 declaration，也不改 production consumer、native BMS schema/renderer、资源 validation/materialization、package authority、shared codec、fallback 或 `SkinManager`。下一切不预设实现，优先只读审计 native BMS remaining closed fields 与 package authority/materializer 边界，再按证据选择一个最小闭环；`SV1-1` 仍未完成。

第十九切只冻结 native `[Bms]` 当前 exact 22-colour decoder-time accepted provenance，使用 internal closed field catalog 与 source-specific immutable bucket snapshot。exact raw key 在 RGB/RGBA parser 成功后同步写入 private sidecar 与既有 public `Colours` compatibility view；comma-composite key 继续按既有 `Enum.TryParse` 行为写 compatibility view，但不进入 closed provenance。factory 只读 sidecar，因此后续 dictionary forge/overwrite/remove/clear/late-add 不能伪造、擦除或改写 declaration；缺 bucket 与显式空 bucket、RGB 默认 alpha 255、RGBA alpha 0、valid duplicate last accepted 均保持 source 语义。本切不做 neutral semantic mapping、visibility/range validation、fallback resolution 或 production 接线。其后第二十切已完成 native `[Bms]` exact 12-geometry accepted provenance，完整 finite/正值/范围/屏内/不重叠 validation 仍归 neutral descriptor/solver；`SV1-1` 仍未完成。

第二十切只冻结 native `[Bms]` 当前 exact 12-geometry decoder-time accepted provenance，使用 internal closed field catalog 与 source-specific immutable bucket snapshot。exact raw key 在 invariant float parser 成功后同步写入 private sidecar 与既有 public `Geometry` compatibility view；comma-composite alias 继续只写 compatibility view。snapshot 原样保留 sign/decimal/exponent、负值、零、`-0`、`NaN`、正负无穷与 overflow/underflow 结果；factory 只读 sidecar，public dictionary mutation 或手工填表不能伪造 declaration。该切没有 finite/正值/range/screen-space/不重叠 validation、neutral mapping、solver、fallback resolution 或 production 接线，因此截至第二十切它不是有效 layout，Skin V1 产品新增可见功能为 0。其后已完成首个 managed `.osk` BMS 普通短键编号帧动画纵切，使当前新增可见功能成为 1；这不改变 geometry 与其它资源仍未完成的结论。

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
