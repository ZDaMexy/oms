# P1-A 开发计划：皮肤系统 V1、产品面与 release gate

> 最后更新：2026-07-10
> 主线总规划见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，硬约束见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)，恢复证据见 [SKIN_SYSTEM_RECOVERY_20260710.md](../../other/SKIN_SYSTEM_RECOVERY_20260710.md)，本轮架构审计见 [SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md](../../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

## 当前专题定位

P1-A 当前主任务是完成 OMS 皮肤系统第一个可交付版本（下称 Skin V1）。它同时拥有：

- 共享 `OmsSkin` package/fallback/selection 边界；
- mania 与 BMS gameplay skin 的共同运行时合同；
- BMS playfield/BGA 布局与皮肤挂点边界；
- G1 皮肤存储、选择、热重载的产品安全门；
- 最终 skin/release 验收矩阵。

规则语义仍由各子线拥有：判定/反馈归 P1-C/P1-E，输入归 P1-B/P1-D，BGA 时间线归 P1-L，存储经验由 P1-H 提供。P1-A 只把它们投影成只读皮肤状态/事件，不能修改其 authority。

## Skin V1 完成目标

Skin V1 不是“内置更多固定 BMS 视觉”，而是同一套公开外部皮肤运行时同时支持：

1. **极简下限**：只保留可读 lane/scratch、下落 note/LN 和判定位置；key animation、judgement、combo、gauge、装饰等可显式关闭。
2. **丰富上限**：皮肤作者使用素材、声明式 scene/animation 和可选沙箱脚本，对输入、scratch、note/LN、判定、gauge、BGA 等只读事件作出表现响应；公开接口的表达力足以制作接近 IIDX 复杂度的界面。
3. **同一公开路径**：OMS 文件型默认、极简验收皮肤和第三方皮肤使用相同 API；程序化 `OmsSkin` 只作为不可删除的最小 rescue fallback。
4. **布局正确**：5K/7K 的 P1 左、P2 右、居中左皿、居中右皿，以及 9K BMS/PMS、14K DP 的 playfield/BGA bounds、lane role 与 safe viewport 均由引擎正确求解。
5. **兼容 mania**：BMS 与 mania 重合的 ini 名称、值类型、素材解析、帧命名和缺省语义使用同一 codec/resolver；BMS 只扩展 scratch/side/DP/gauge/BGA/gimmick 等独有语义。

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
- `BmsSkinTransformer` 的 component lookup 与程序化 rescue fallback；
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

状态：自动恢复完成；人工/数据 gate 未完成。

1. 用户验收无外部皮肤、`.osk`、partial fallback 和 5K/7K/9K/14K 视觉。
2. 只读报告 schema 56 中 folder-backed `SkinInfo`、authority、目录存在性和当前选择状态。
3. 未经备份与迁移设计，不改写 Realm、不清理 `chartskin/`、不降低 schema。

验收：恢复测试基线稳定；人工记录进入 P1-G；数据报告不产生任何写入。

### SV1-1：共同合同与 fixture 冻结

状态：架构/文档决议已完成；代码 fixture 未开始。

1. 建立 neutral `GameplaySkinLayoutContext`、lane group/role/side/stable ID DTO 草案。
2. 冻结 `Provide / Inherit / Suppress` 语义和不可 suppress 的最小可玩组件。
3. 以真实 mania skin.ini fixture 固定 tokenizer、数组、动画帧、错误/诊断语义。
4. 固定 BMS compatibility mapping：`[Bms]` role override → full visual bucket（5K→6、7K→8、9K→9、14K→16）→ key-only bucket（5/7/14，scratch `Inherit`）/14K 显式双 8-column deck → default/rescue；P2/CenterP2 mapping 用 fixture 钉死。
5. neutral config 保存 explicit declaration/presence；legacy 自动合成的默认 bucket 不得误判为 `Provide`。
6. 用 fixture 冻结 `BeatmapSkinProvidingContainer` 与 `RulesetSkinProvidingContainer` 的既有相对 authority；三态只接管 gameplay package slot，`Suppress` 默认不得穿透更高优先 beatmap-local provider。
6. 建立事件 envelope：`apiVersion/epoch/sequence/gameplayTime/layoutRevision`，以及 attach/reload snapshot、seek/retry reset 与 edge 事件顺序。
7. 建立禁止写入的 gameplay authority 列表和 capability negotiation 草案。

验收：仅合同/fixture，不接生产脚本；mania/BMS 对共同输入产生同构 neutral config。

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

状态：现有 topology 可复用；统一 descriptor 与完整矩阵未开始。

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

状态：未开始；当前两套 decoder 仅可作为迁移输入。

1. adapter-first：先由现有 legacy mania/BMS decoder 导出带 explicit presence 的 neutral snapshot；fixture 稳定后再抽 shared codec，不第一刀切换 mania 生产 tokenizer。
2. `[Bms]` 重合键使用同一 codec；BMS 独有字段由 extension schema 解析。
3. 统一 0-based mania column、BMS `S/S2` 与内部 stable lane ID；renderer 不再拼接 lane 字符串。
4. 当 `[Bms]` 未覆盖共同件时，按 SV1-1 mapping 进入显式 mania compatibility fallback。
5. 统一未知键、非法值、缺素材和不支持能力的结构化诊断；加载永不阻断游玩。

验收：同一 fixture 在 mania/BMS 共同件上解析一致；旧 `.osk/[Bms]` reference 继续可用。

### SV1-5：声明式 scene、动画与事件 ABI

状态：未开始。

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

### SV1-7：双极限皮肤与 release gate

状态：未开始。

1. `Minimal` 验收皮肤：只显示最小可玩件，显式 suppress 所有可选视觉。
2. `Showcase` 验收皮肤：覆盖全部公开 slot/event，证明接近 IIDX 复杂度的表达上限，但只使用原创/可分发素材。
3. OMS 文件型默认从相同公开接口实现；程序化 `OmsSkin` 只保留 rescue fallback。
4. BMS/mania/core skin/Release、启动/切换/reload、5K/7K/9K/14K、BGA、脚本性能和人工视觉全部过门。

验收：缺失/损坏 package 仍可玩；Minimal 不被 fallback 强行补出可选件；Showcase 不使用私有接口。

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

## 验证矩阵

| 变更面 | 自动 gate | 人工 gate |
| --- | --- | --- |
| shared ini codec | mania legacy fixtures + BMS compatibility fixtures | 代表性 `.osk` 对照 |
| layout descriptor | 5K/7K 四 style + 9K BMS/PMS + 14K bounds/role/BGA | 16:9、16:10、超宽与 DPI |
| scene/event ABI | event order/payload/version/fallback | press/hit/LN/scratch/judge/gauge 观感 |
| G1/reload | authority/containment/reparse/conflict/atomic replace | 备份数据根、重启、编辑中切换 |
| sandbox | capability denial/budget/determinism/exception | 长时间游玩与 profiler |
| release | BMS + mania + core skin + Release | Minimal + Showcase + 用户皮肤 |

## 兼容与迁移

- 当前 `.osk/[Mania]` 与 `.osk/[Bms]` 均保留；新 scene/script 是可选增强，不做一次性格式切换。
- 事故期归档只能定点借鉴接口/测试，不得整包 cherry-pick/apply。
- 旧 F1/F2/F3/G1/G2 术语只在 CHANGELOG/恢复审计中作为历史索引；当前执行只看 `SV1-*`。
- P1-A 的 onboarding、settings trim、BMS→mania 公开入口、HUD/gauge 既有合同保持维护状态；除非阻塞 Skin V1/release，不抢占上述顺序。
