# 皮肤系统 V1 架构审计（2026-07-10）

> 本文记录 2026-07-10 可信恢复之后，对 mania 现有皮肤系统、BMS playfield/BGA 布局和外部皮肤运行时的重新评估。它是证据账与设计解释；权威执行顺序和硬约束分别位于 [P1-A PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md) 与 [P1-A CONSTRAINTS](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)。恢复边界仍以 [皮肤恢复审计](SKIN_SYSTEM_RECOVERY_20260710.md) 为准。

## 结论

第一个完成版不再定义为“把更多固定视觉写进 BMS 内部代码”，而定义为一套可伸缩的外部皮肤运行时：

1. 引擎拥有谱面/输入/判定/BGA 的真实语义、playfield 与 BGA 的安全布局、资源和脚本隔离、fallback 与性能保护。
2. 外部皮肤拥有具体视觉树、素材、静态/动态装饰、动画和对只读事件的表现响应；除最小可玩 fallback 外，不要求内部代码为每种视觉提供固定实现。
3. mania 与 BMS 的共同部分使用同一套配置值、素材解析、帧动画、场景节点和事件 ABI；两者只在 lane topology、scratch/DP、BGA/gauge/gimmick 等规则语义适配上分离。
4. V1 必须同时证明两个极端：只显示可玩色块的极简皮肤，以及使用同一公开接口实现接近 IIDX 丰富度的展示皮肤。后者证明能力上限，不捆绑或复制商业素材。
5. 当前 F1/G1/F2 命名保留作历史索引；新的执行 authority 改为 P1-A `SV1-0`～`SV1-7`，避免继续把“可配置键数”误当成系统完成度。

## 用户目标转译为工程合同

### 极简下限

最小皮肤只需要：

- 可区分的 lane/scratch；
- 可见的 note、LN 与 mine；
- 可判断到达时刻的 hit target / judgement line；
- 当 lane cover 玩法启用时，真实遮挡范围与可调状态；
- 引擎拥有但皮肤可选择是否展示的 BGA viewport。

按键动画、hit explosion、judgement、combo、gauge、key area、turntable、lane cover 装饰、文本 HUD 等都可以显式关闭。为区分“作者选择不显示”和“文件缺失/加载失败”，V1 查找结果必须是三态：

- `Provide`：皮肤提供实现；
- `Inherit`：未覆盖，逐组件进入下一 fallback；
- `Suppress`：作者明确不显示可选组件，不得被 OMS fallback 重新补出。

note/LN/mine、lane/scratch 可读性、判定位置和启用中的 cover 几何属于不可完全 suppress 的最小可玩层；加载失败时仍由程序化 rescue fallback 保底。

### 表现上限

公开接口至少要能表达：

- 多层 sprite/container/mask/text、混合模式、裁剪与标准视觉效果；
- 帧动画、tween、循环、状态机和按游戏时钟驱动的 timeline；
- key press/release、scratch 方向/速度、note hit/miss、LN hold/break/recover、judgement/offset、combo、gauge、beat/measure/BPM/STOP/scroll、mine 和 BGA/POOR 等只读事件；
- per-lane、per-side、per-keymode 和当前 playfield/BGA slot 的上下文；
- 皮肤脚本在沙箱内组合上述能力，而不能写入判定、计分、输入、谱面、Realm 或文件系统。

“能够仿 IIDX”指公开运行时具备足够表达力，不指 OMS 自带 IIDX 资产、兼容 IIDX 文件格式或复刻其内部脚本语言。

## mania 当前系统审查

### 实际上限来自哪里

mania 的传统 `skin.ini` 能配置：

- column width/spacing/line width；
- hit/light/combo/score position；
- key/note/LN/stage/lighting/judgement 素材；
- barline、颜色、body style 和部分帧率/缩放参数。

连续命名的 `name-0`、`name-1`… 可以经共享 `GetAnimation()` 形成帧动画。但是按键亮起、LN holding、hit explosion、judgement 播放、combo 绑定等行为不是 ini 定义的；它们由 `LegacyKeyArea`、`LegacyColumnBackground`、`LegacyBodyPiece`、`LegacyHitExplosion`、`LegacyManiaComboCounter` 等 C# 组件硬编码，再从 ini 取素材和参数。

因此 mania 当前证明了“固定行为宿主 + 外部素材”的成熟路径，没有证明“外部作者可定义任意交互逻辑”。把 BMS 做到 mania parity 是 V1 的兼容下限，不是能力上限。

### 可共享与不可直接共享

| 面 | 决议 |
| --- | --- |
| `skin.ini` token、颜色/数值/数组解析、素材路径、帧序列 | 提取为规则集无关共享层；mania/BMS 不再各维护近似实现 |
| note/LN/key/stage/lighting/judgement 的共同键名 | BMS 共同件使用与 mania 相同的名字、值类型、帧命名与缺省语义 |
| scene node、tween/state-machine、事件 envelope、诊断、热重载 | 完全共享 |
| `ManiaLegacySkinTransformer`、`Column`、`ManiaAction`、480 高度坐标 | 不直接复用；这些类型含 mania 假设 |
| keymode/lane 映射 | 由 ruleset adapter 分离；共享层只认识 lane group、lane role、side 和 stable ID |
| hit/LN/input 事件 | 共享事件名和 payload 基类，各 ruleset adapter 负责从真实对象投影 |

### BMS 使用 mania 配置的兼容映射

BMS `[Bms]` 段保留 `Keymode:` 和 scratch/side 扩展，但重合字段必须经过同一个 shared legacy-mania codec。若 `[Bms]` 未覆盖共同件，可显式进入 mania compatibility fallback：

| BMS 模式 | full visual-lane bucket | key-only/deck fallback | 解释 |
| --- | --- | --- | --- |
| 5K + 1 scratch | `[Mania] Keys: 6` | `[Mania] Keys: 5` 只映普通键，scratch `Inherit` | 同时兼容 6-column 与常见 5K mania 皮肤 |
| 7K + 1 scratch | `[Mania] Keys: 8` | `[Mania] Keys: 7` 只映普通键，scratch `Inherit` | 同时兼容 8-column 与常见 7K mania 皮肤 |
| 9K BMS/PMS | `[Mania] Keys: 9` | 同一 bucket | 无 scratch；BMS/PMS 差异留在 ruleset context |
| 14K + 2 scratch | `[Mania] Keys: 16` | 显式双 `[Mania] Keys:8` deck fallback；`Keys:14` 只映普通键 | legacy mania 按 total columns 查桶；layout 仍建模为两个独立 deck |

gameplay package 内的统一优先级为：`[Bms]` role-aware override → full visual-lane bucket → 显式 deck/key-only bucket（普通键映射、scratch `Inherit`）→ 文件型默认/rescue。具体索引从当前 BMS 的 `S/S2` 与 mania 的 0-based column 统一到内部 stable lane ID；P2/CenterP2 必须用 fixture 钉死 visual index 与 stable lane ID 的关系。

这不是完整的 lazer skin-provider 顺序。`BeatmapSkinProvidingContainer` 仍可能提供高于 selected skin 的谱面内皮肤，`RulesetSkinProvidingContainer` 仍会插入 ruleset resource skin；V1 三态只替换 gameplay package resolution，先保持两者既有 enable/colour/hitsound 与相对 authority。`Suppress` 默认不能越权穿透更高优先的 beatmap-local provider；改变这一点必须另立兼容迁移决议和 fixture。

shared resolver 还必须保存“作者是否显式声明该值”。当前 legacy mania 在缺少对应 `Keys:` bucket 时会现场生成默认 configuration；这些合成默认不能被新运行时误判为 `Provide`，否则会遮住后层 OMS fallback。

## BMS 当前代码审查

### 已经可信的部分

- `BmsSkinTransformer` 已提供逐组件 fallback 入口，lookup 带 keymode/lane/scratch 上下文。
- `BmsLegacySkin` 能在不破坏父类 `[General]/[Colours]/[Mania]` 的前提下解析 `[Bms]`。
- F1 已让现存 note/LN、lane/divider、hit target、barline、lane cover、backdrop/baseplate 读取颜色/纹理/几何。
- `BmsLaneLayout` 能建立 5K/7K/9K/14K lane topology，14K 有双 scratch 和 centre gap；5K/7K 支持左右/居中及左右皿视觉。
- `BmsBgaPanel` 已有 skinnable host 和时间线播放链。

### 与 V1 目标不一致的部分

1. BMS 和 mania 配置 decoder 仍是两套实现；共同键只做到“名字相似”，没有共同 codec 和一致的错误/诊断语义。
2. 当前动态视觉依赖为每件新增 C# 类/接口；事故期的 keyflash/hold-light/ghost 等实现即使方向上有事件推送价值，也不能形成可扩展作者运行时。
3. `null` 只表示 fallback，文件皮肤缺少显式 `Suppress`，无法自然表达“没有 combo/judgement/动画”的极简皮肤。
4. BGA 接口把原始 timeline 和 POOR 信号交给 display，让 display 自己创建 player；这把内容播放 authority 与皮肤视觉混在一起。V1 应由引擎持有唯一 BGA clock/content surface，皮肤只取得只读 viewport/content handle 和事件。
5. 当前 14K 默认把 BGA 播放器复制到四角；这是一种临时默认表现，不是经过产品确认的布局合同，也不应成为外部皮肤必须继承的语义。
6. stage/key area 的配置键已可解析但没有生产渲染消费方；gauge/judgement/combo/BGA 在“代码 provider 可替换”和“ini/外部包可制作”两个维度上仍被文档混写。
7. 当前程序化 fallback 包含较多具体风格。V1 目标应将其收敛为隐藏的 minimal rescue skin；公开默认/展示皮肤必须走与第三方完全相同的文件、scene 和 script 接口。
8. playfield 会消费皮肤的宽/高 profile，但默认 gauge/combo 会另建默认 profile，BGA 使用固定 rect；合法 partial geometry skin 已可令三者脱节。
9. geometry parser 接受任意可解析 float，profile 未验证 finite/正值/范围；零宽、负值、NaN 或超屏值可能进入归一化/布局，违背 fail-open 可玩性。
10. current lookup 不带 style/player side/visual index/stage/final rect；CenterRightScratch 的 BGA 与 Center 同为右上，无法表达真正 CenterP2 对侧布局。
11. sparse 7K/9K 的 channel 启发式可能低估 keymode；正确布局必须包含 keymode 来源、诊断和显式纠正入口。
12. `buildLaneKeysoundTimelines()` 当前用 key count 而非 lane count 作上界，可能丢 5K/7K 最右键及 14K 右侧末键/Scratch2；修复归 P1-K/P1-J，但 Skin V1 topology smoke 必须防回归。

## V1 共享运行时设计

### 分层

```text
Skin package (.osk / managed folder / external read-only folder)
  ├─ skin.ini compatibility layer
  ├─ scene/animation manifest (declarative)
  ├─ optional sandboxed script
  └─ assets
          ↓
shared gameplay-skin runtime
  ├─ resource resolver + diagnostics + reload
  ├─ scene graph + animation/state machine
  ├─ versioned event/state ABI
  ├─ Provide / Inherit / Suppress resolution
  └─ sandbox/performance/failure isolation
          ↓
ruleset adapter
  ├─ mania: stage/column/legacy coordinates
  └─ BMS: scratch/DP/cover/gauge/gimmick/BGA
          ↓
engine-owned layout + gameplay truth
```

声明式 scene/state-machine 应覆盖大多数皮肤；它至少区分 global scene、per-lane template、pooled note/LN template 和 pooled event-effect template，并支持 typed property binding/variant（如 gauge value→clip/scale、combo value→text、result key→sprite variant）。动画编译为 gameplay-clock transform，note 滚动/LN 裁剪/对象池仍由引擎 host 驱动；脚本不得用 `on_update` 逐帧搬动每个谱面对象。脚本只用于声明式层难以表达的组合逻辑。受信任 C# code provider 可留作开发扩展，但不能作为可分发用户皮肤的必要条件。

外部拥有视觉内容不等于获得 framework Drawable/tree。manifest 使用稳定 node type ID 和 allowlist，不序列化 CLR `Type`、不反射构造任意类，也不直接复用 Skin Layout Editor JSON。V1 只提供 allowlisted blend/effect/shader preset，不把任意 shader 作为完成条件。

### 事件 ABI

事件必须是不可变、版本化、按 gameplay clock 排序的 DTO，不允许外部层反查 `DrawableRuleset` 或遍历父节点。envelope 至少包含 `apiVersion/epoch/sequence/gameplayTime/layoutRevision`；attach/reload 先发完整 snapshot，seek/retry 产生 reset/new epoch。建议 V1 family：

- lifecycle：load/show/hide/reload/dispose；
- layout：keymode、lane groups、lane bounds、side/style、scroll direction、BGA viewport；
- input：lane press/release、scratch direction/value/velocity；
- object：spawn/despawn、hit/miss、LN start/hold/release/break/recover、mine；
- judgement：result、offset、combo break；
- score：combo、EX score/accuracy、gauge type/value（只读）；
- timing：beat、measure、BPM、STOP、scroll/gimmick；
- BGA：source changed、POOR begin/end、visibility；实际解码与 seek 仍归引擎。

脚本只能创建/更新获准的视觉节点、启动 animation/tween、读 snapshot 和订阅事件。连续 scratch/scroll 走宿主采样/节流，不暴露无界原始事件。禁止网络、任意文件访问、反射、进程、线程、原生库、写配置/Realm、修改输入/判定/计分/BGA 时间线。

### 确定性与性能

- 时间只来自 gameplay clock；随机数必须使用引擎提供的确定性 seed。
- 固定事件顺序和 replay 语义；seek/retry/reload 必须定义状态重建规则。
- 脚本 VM 必须支持可抢占 instruction/heap quota；不能只在回调返回后用 stopwatch，因为无限循环不会返回。
- 每皮肤限制 package 压缩/解压总字节、单资源解码像素、总 decoded bytes/纹理/atlas/帧数、各模板节点和每帧预算；超限或异常只熔断对应外部层并逐组件 fallback。
- package capability 显式声明；允许的可选能力须 per-skin 授权并可撤销，禁止能力永不授权。
- 加载、编译和文件 IO 不在 update thread 执行；脚本事件不得阻塞。
- 热重载采用新实例完整验证后原子切换，不在运行实例上半更新。

## playfield 与 BGA 布局合同

shared runtime 定义 neutral `GameplaySkinLayoutContext`，BMS adapter 输出唯一 `BmsGameplayLayoutSnapshot`；playfield、gauge、combo、BGA 和皮肤全部消费该 snapshot。snapshot 还必须声明 z-layer、clip 和 input-pass-through。皮肤只能锚定/裁剪/装饰其命名 slot，不能改变 lane order、判定位置、scroll timing 或 BGA 内容时钟。

| 模式 | 引擎拥有的 lane group | 必须覆盖的 style | BGA authority |
| --- | --- | --- | --- |
| 5K | 1 组：S1 + K1–K5 | P1 左、P2 右、CenterP1 左皿、CenterP2 右皿 | P1/CenterP1 默认右侧；P2/CenterP2 默认左侧安全 viewport |
| 7K | 1 组：S1 + K1–K7 | 同 5K | 同 5K |
| 9K BMS | 1 组 K1–K9 | 居中；P1/P2 请求规范化为 center | 单一 engine-owned viewport，不覆盖 lane strip |
| 9K PMS | 1 组 K1–K9 | 居中，context 明确 PMS | 同 9K BMS |
| 14K | 2 组：S1+K1–K7、K8–K14+S2 | 固定 DP；双皿在两组外缘；明确 centre gap | 一个内容 authority；descriptor 可提供中心或其它安全 mirror viewport，但不能创建多个独立 player/clock |

V1 自动测试必须覆盖上述每一格的 lane bounds/order、scratch role、playfield bounds、BGA/gauge/combo viewport 不相交、非法 geometry 逐字段回落、skin override 后不改变时序；另覆盖每轨 visible/LN/invisible/mine/keysound、16:9/16:10/21:9/4:3。实机还要覆盖 DPI、BGA、cover/lift 和用户脚本。

当前 `14K → 四角四 player` 只记为待替换的临时实现，不是目标合同。

## V1 完成定义

以下条件全部成立才可称“第一个完成版”：

1. schema 56 数据清点与 G1 managed/external 安全合同完成，外部目录只读、删改 containment 和原子 reload 有生产测试。
2. 5K/7K 四种 style、9K BMS/PMS、14K DP 的 playfield/BGA descriptor 通过自动与实机矩阵。
3. mania/BMS 共同 ini 字段由同一 codec/resolver 解释；BMS compatibility mapping 有 fixtures。
4. 外部 package 可通过 declarative scene + versioned event ABI 实现所有公开视觉，不需要新增 ruleset C# 类。
5. sandbox script 可选启用，具备确定性、权限、预算、熔断和 reload 测试。
6. `Provide/Inherit/Suppress` 贯穿默认、用户、缺件和加载失败路径。
7. 极简验收皮肤只保留最小可玩件；展示验收皮肤覆盖 press/hit/LN/scratch/judge/gauge/BGA 等事件。两者使用同一公开 API。
8. 程序化 `OmsSkin` rescue fallback 仍不可删除；公开文件型默认不享有私有接口。
9. BMS、mania、core skin、Release 和视觉/性能 gate 达标，文档不宣称 LR2/beatoraja/IIDX 文件格式兼容。

## 迁移原则

- 不恢复事故期 G1/F2/Lua 整批代码；可定点复用测试场景、接口命名和事件推送教训。
- 现有 F1 `.osk/[Bms]` 静态支持作为兼容输入保留，不要求现有皮肤立即迁移 scene/script。
- 新 shared runtime 先以 adapter 包裹现有实现，组件逐个迁移；不同时重写 parser、布局、存储和脚本。
- 任何新动态件优先扩展通用事件/scene 能力；在 ABI 缺口被证明前，不新增固定 `DefaultBmsXxxDisplay` 家族。
