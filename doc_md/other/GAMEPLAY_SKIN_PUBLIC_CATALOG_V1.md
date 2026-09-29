# Gameplay Skin V1 部件与目标查表

已经知道要改音符、血槽或BGA，但不知道应写哪个名字时，用这张表查部件ID、作用范围以及能否关闭。第一次制作先读[图解和元素速查](../../skin-authoring/docs/SKINNING.md#3-按画面元素查找文件)；完整文件与命令见[制作套件](../../skin-authoring/README.md)。本页保留精确技术表，发行套件内也有 [CATALOG](../../skin-authoring/docs/CATALOG.md) 和 [REFERENCE](../../skin-authoring/docs/REFERENCE.md)，不需要源码目录。

读表：`ID`是写进INI/scene的名字；`Scope`是所在区域；`Required/Recommended`不能关闭，只有`Optional + Allowed`可写`Suppress`。资源缺失时继续使用可用的默认部件。表里的键数上界是格式适用范围，不等于当前所有谱面入口都支持该键数。

实际玩法有差异：原生BMS使用28项（9K无装饰转盘/皿光束）；mania可用23项，`object.mine`、`playfield.turntable`、`playfield.laser`、`bga.viewport`、`bga.frame`不适用。**BMS转谱使用mania表现，也没有BGA。** 不要将表中宽泛的目录适用性当作mania实际支持全部28项。

| 找什么 | ID前缀或名称 |
| --- | --- |
| 音符、长条头身尾、地雷 | `object.*` |
| 车道、判定线、键帽、小节线、遮挡 | `playfield.*` |
| 按键闪光、命中效果 | `effect.*` |
| 判定、连击、血量、文字信息 | `hud.*` |
| 背景、舞台外框、自由装饰 | `stage.*`、`decoration` |
| BGA窗口与外框 | `bga.viewport`、`bga.frame`，内容仍来自谱面 |

当前格式为catalog `oms-gameplay-skin-catalog.v1`、codec `oms-gameplay-skin-codec.v1`、resolver `oms-gameplay-skin-resolver.v1`。下方生成块来自`GameplaySkinSlotCatalog`并受文档一致性测试检查，维护时不手改ID或适用性。INI段只有`[GameplaySkin.Common:1]`与`[GameplaySkin.Bms:1]`。

<!-- GAMEPLAY-SKIN-CATALOG:BEGIN -->
| ID | Stable name | Catalog | Scope | Type | Class | Default | Suppress | Rulesets | Stage | Lane role | Keymode | Keys | Diagnostic |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `playfield.lane-surface` | `LaneSurface` | `Common:1` | Lane | Resource | Required | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-001` |
| `playfield.judgement-line` | `JudgementLine` | `Common:1` | Stage | Resource | Required | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-002` |
| `object.note` | `Note` | `Common:1` | Lane | Resource | Required | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-003` |
| `object.long-note.head` | `LongNoteHead` | `Common:1` | Lane | Resource | Required | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-004` |
| `object.long-note.body` | `LongNoteBody` | `Common:1` | Lane | Resource | Required | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-005` |
| `object.mine` | `Mine` | `Common:1` | Lane | Resource | Required | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-006` |
| `playfield.lane-cover.fill` | `LaneCoverFill` | `Common:1` | Stage | Resource | Required | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-007` |
| `object.long-note.tail` | `LongNoteTail` | `Common:1` | Lane | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-008` |
| `playfield.key` | `KeyVisual` | `Common:1` | Lane | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-009` |
| `effect.key-flash` | `KeyFlash` | `Common:1` | Lane | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-010` |
| `effect.hit-explosion` | `HitExplosion` | `Common:1` | Lane | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-011` |
| `hud.judgement` | `JudgementDisplay` | `Common:1` | Stage | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-012` |
| `hud.combo` | `ComboDisplay` | `Common:1` | Stage | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-013` |
| `hud.gauge` | `GaugeVisual` | `Common:1` | Stage | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-014` |
| `hud.text` | `TextHud` | `Common:1` | Global/Stage | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-015` |
| `playfield.bar-line` | `BarLine` | `Common:1` | Group | Resource | Recommended | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-016` |
| `stage.background` | `StageBackground` | `Common:1` | Global/Stage | Resource | Recommended | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-017` |
| `stage.foreground` | `StageForeground` | `Common:1` | Stage | Resource | Recommended | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-018` |
| `playfield.backdrop` | `PlayfieldBackdrop` | `Common:1` | Stage | Resource | Recommended | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-019` |
| `playfield.baseplate` | `PlayfieldBaseplate` | `Common:1` | Stage | Resource | Recommended | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-020` |
| `playfield.lane-cover.decoration` | `LaneCoverDecoration` | `Common:1` | Stage | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-021` |
| `playfield.turntable` | `Turntable` | `Bms:1` | Lane | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Bms | Single/Dual | Scratch | Bms5K/Bms7K/Bms14K | 5-14 | `OMS-SKIN-SLOT-022` |
| `playfield.laser` | `Laser` | `Bms:1` | Lane | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Bms | Single/Dual | Scratch | Bms5K/Bms7K/Bms14K | 5-14 | `OMS-SKIN-SLOT-023` |
| `bga.viewport` | `BgaViewport` | `Common:1` | Global | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-024` |
| `bga.frame` | `BgaFrame` | `Common:1` | Global | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | None | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-025` |
| `decoration` | `Decoration` | `Common:1` | Global/Stage/Group/Lane | Resource | Optional | InheritToLowerAuthorityThenCanonicalFallback | Allowed | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-026` |
| `playfield.hit-target` | `HitTarget` | `Common:1` | Lane | Resource | Recommended | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-027` |
| `playfield.lane-divider` | `LaneDivider` | `Common:1` | Lane | Resource | Recommended | InheritToLowerAuthorityThenCanonicalFallback | Forbidden | Mania/Bms | Single/Dual | Key/SpecialKey/Scratch | Mania/Bms5K/Bms7K/Bms9K/Bms14K | 1-20 | `OMS-SKIN-SLOT-028` |
<!-- GAMEPLAY-SKIN-CATALOG:END -->

## 作者格式

公共声明写在包内同一个 `skin.ini`：

```ini
[GameplaySkin.Common:1]
Target: Lane ruleset=bms keymode=5k stage-mode=single presentation=p1 group=bms.group.deck-1 lane=bms.lane.key-1 group-logical=0 group-visual=0 global-logical=1 global-visual=1 group-local-logical=1 group-local-visual=1
object.note: resource Provide "notes/key-1"
object.long-note.tail: resource Suppress
```

- section、字段、类型和操作均区分大小写；合法 section 只有 `GameplaySkin.Common:1` 与 `GameplaySkin.Bms:1`。
- section header 必须是没有内部首尾空白、没有尾随字符的完整 `[...]`；版本与全部 index 只接受 canonical ASCII 十进制（`0` 或非零开头），拒绝正号、前导零、全角数字和溢出。文件开头至多允许一个 UTF-8 BOM，embedded/double BOM 是 document-fatal 诊断。
- `#` 与 `;` 在引号外开始注释；资源值必须使用双引号。支持 `\\`、`\"`、`\n`、`\r`、`\t`，其它转义是稳定错误。
- 公共 section 内，去除行首空白后以 `//` 开头的整行也作为注释跳过，并保留在往返 token stream 中。`//` 不开始行内注释；引号中的 `//` 原样保留，但不因此绕过资源路径准入。未知字段、坏 header 与 BOM 仍按原规则诊断。
- `Provide "..."`、`Inherit`、`Suppress` 是显式状态。未出现、已声明空字符串、非法声明、有效声明和合法 `Suppress` 各自保留，不能互相静默折叠。
- 同一 catalog ID + exact target 重复声明是错误；未知字段、未知 ID、错误 family、未知版本、错误 scope/type/index、selector 与 exact publication stable ID/index 漂移都会产生 `OMS-SKIN-CODEC-NNN` 诊断。字段错误只沿该 slot 的确定 fallback 继续，不把 invalid 当作 absent。
- `Encode(Decode(x))` 输出规范化 UTF-8 token stream（换行归一、去除行尾空白），再次 decode 必须保留所有语义状态、section、legacy token 与诊断。

整行 `//` 兼容覆盖普通导入在新 `[General]` 元数据前添加的说明，包括说明紧跟公共 section 的既有导入包。读取旧内容即可恢复，不要求重写作者文件、修改导入写入规则或授予额外权限；它不掩盖实际声明错误。

### Target

每个 `Target` 都必须显式写出 `ruleset`、`keymode` 与 `stage-mode` 三个 selector；省略这些必填项、重复属性、未知属性或非法 token 均为 invalid。四种 scope 均可另写可选 `presentation=<token>`；省略等同 `presentation=any`，保持旧声明的严格索引语义。下列基本形式与增加该可选属性的形式都有效：

- `Global ruleset=<selector> keymode=<selector> stage-mode=<selector>`
- `Stage ruleset=<selector> keymode=<selector> stage-mode=<selector> group=<GroupId> group-logical=<n> group-visual=<n>`
- `Group ruleset=<selector> keymode=<selector> stage-mode=<selector> group=<GroupId> group-logical=<n> group-visual=<n>`
- `Lane ruleset=<selector> keymode=<selector> stage-mode=<selector> group=<GroupId> lane=<LaneId> group-logical=<n> group-visual=<n> global-logical=<n> global-visual=<n> group-local-logical=<n> group-local-visual=<n>`

`ruleset` 为 `any` / `mania` / `bms`，`stage-mode` 为 `any` / `single` / `dual`。`keymode=any` 可跨 keymode；BMS exact token 为 `5k`、`7k`、`9k-bms`、`9k-pms`、`14k`，mania single token 为 `<n>k`，dual-stage vector 为 `<left>k-<right>k`，且总 lane 数必须与 C3 topology 一致。portable 文档可同时声明多个 selector；不属于当前 publication 的 selector 不产生 runtime 故障，也不能命中当前 material。

同一 package 内的 winner 顺序固定为：exact ruleset 优先于 `any`，再比较 exact keymode、exact stage-mode、exact presentation、scope（`Lane > Group > Stage > Global`），最后才是同 specificity 的后行。最高 specificity 声明会遮蔽同 package 的更宽声明：显式 `Inherit`、empty 或 invalid 都转向下一 authority，不回头拼接本 package 的较宽声明。这保证 invalid 不冒充 absent，也防止一个 package 内发生隐式聚合。

BMS group ID 为 `bms.group.deck-1` / `bms.group.deck-2`，lane ID 为 `bms.lane.scratch-1`、`bms.lane.key-1` … `bms.lane.key-14`、`bms.lane.scratch-2`。mania group ID 为 `mania.group.stage-1` / `mania.group.stage-2`，lane ID 为全局顺序的 `mania.lane.column-1` …。ID 与所有 logical/visual/global/group-local index 必须同时匹配 C3 exact topology；resolver 不从 enum ordinal、lane count、几何、`RelativeStart` 或 drawable 次序反推。BMS 5K/7K 的四种样式须用 `presentation=p1`、`p2`、`center-p1`、`center-p2` 分别限定其完整 explicit index；P1 与 Center 使用同一左皿坐标，P2 与 CenterRightScratch 使用同一右皿坐标，不能只写两组未限定样式的目标。dual-stage 等其它布局也继续按其 explicit index 声明。

`presentation` 使用 1–80 个小写 ASCII 字母、数字、`.` 或 `-`；除 `any` 外，与当前 `GameplaySkinLayoutContext.PresentationStyleId` 按 ordinal 精确匹配。合法但不匹配当前样式的 token 只是不适用，不命中 material，也不产生 `OMS-SKIN-CODEC-021`。语法合法的拼写错误因此可能没有适用声明，作者须使用实际样式 token 并逐样式验证。当前 BMS 的 P1/P2/Center/CenterRightScratch 分别发布上述四个 token；9K 两格式与 14K 的 applied style 为 Center，可保持 `any`。mania 及不区分样式的声明也可省略该属性。

匹配当前 presentation 后，GroupId/LaneId、所属 group 和全部 logical/visual/global/group-local index 仍逐项匹配；错误坐标继续产生 `OMS-SKIN-CODEC-021`。其它正确声明、另一组合法排列或实际能进入游玩都不能掩盖当前声明的错误。省略与显式 `any` 在 target identity 中等价，重复声明规则不变。

这是 codec v1 的可选增量，须使用支持 `presentation` 的 OMS 与配套作者工具；较旧客户端会把该字段当未知属性拒绝。旧包同时携带未限定样式的左右两组坐标时仍可能得到严格诊断，应修改作者副本、检查并打包，再普通导入，或退出游玩/预览后刷新固定 `chartskin/` 目录。当前设置没有新外部目录注册入口，游戏不改写只读作者目录。

9K 的公开 canonical lane index 为 `1..9`；legacy `[Mania] Keys:9` 的 raw index `0..8` 只经 `bms-gameplay-skin-nine-key-index.v1` 双向映射进入 compatibility candidate，未知版本 fail-closed。Mirror/Random 只改变对象最终目标 `LaneId`；resource、keysound 与 skin lookup 随同该 `LaneId`，不会改变 topology 或借 drawable 次序重算 lane。

## 同一部件写了多处时

先检查当前玩法、键数、样式与目标是否匹配。包内优先选择更精确的ruleset、keymode、stage-mode、presentation、scope，最后才比较同等级的后行；重复的相同目标仍是错误。`Provide`选自己的资源，`Inherit`继续使用下层可用部件，`Suppress`只允许表中标为可关闭的可选项。精确声明无效时不反选本包更宽的声明。

公共资源声明高于同包的旧版兼容字段。换文件名时同时检查公共声明和`NoteImage*`等字段；直接替换现有同名PNG通常最容易看出效果。旧BMS兼容查找顺序如下，不能用它替代公共Target的明确坐标：

- 5K：`[Bms]` → `[Mania] Keys:6` → `Keys:5`。
- 7K：`[Bms]` → `Keys:8` → `Keys:7`。
- 9K：`[Bms]` → `Keys:9`。
- 14K：`[Bms]` → `Keys:16` → 两侧各用同一个`Keys:8` → `Keys:14`。

必要部件缺失时由当前可用默认外观补齐，不代表作者已经提供了这项素材。既有谱面视觉兼容也可能覆盖皮肤的部分显示；排查时使用不带该兼容覆盖的测试谱。

## 与scene和BGA配合

`slot`引用本表ID，但可见场景节点只能使用本包明确Provide的对应素材。未提供、Inherit、Suppress不会被附加scene变成Provide；不要借用默认包的节点或资源。公共INI支持玩法/键数/样式选择，scene的直接目标没有“目标不存在就忽略”的开关。跨玩法专用样式使用[素材匹配模板](../../skin-authoring/docs/REFERENCE.md#动画状态机绑定变体模板)。

BGA窗口几何使用`[Bms] BgaViewports`，资源名仍是`bga.viewport/frame`；窗口数量/适配不会改变部件ID或允许脚本控制视频。`none`或无空间时不生成窗口装饰，BgaInformationHeight大于0的信息区域独立保留；声明和素材仍完整校验。CLI检查语法、范围和数量，实际空间冲突需进入游戏并在`runtime.log`搜索`bms.layout.bga-viewports-unavailable`，设置没有专属布局错误面板。完整例子与边界见[制作手册](../../skin-authoring/docs/SKINNING.md#bga窗口与内容)。BMS转mania不提供BGA。

## 检查与更新

作者工具check给出文件、行号和稳定错误码；按元素查图可用[手册](../../skin-authoring/docs/SKINNING.md)，场景/脚本具体格式与预算用[参考](../../skin-authoring/docs/REFERENCE.md)。修改后打包普通.osk重新导入，或将完整作品放在固定chartskin目录并在退出游玩/预览后点击“刷新皮肤”。当前画面重载失败会保留旧版本；没有自动watcher或游玩中替换。

没有新的beatmap-local public INI/scene/script格式。既有只读谱面视觉兼容保留，普通作者作品仍使用皮肤包或固定目录；当前设置没有新的外部作者目录注册入口。

## 维护者参考

生成表来自`GameplaySkinSlotCatalogDocumentation.GenerateMarkdownTable()`，文档测试只锁生成块及对应合同。来源、版本一致性、诊断隐私与失败恢复的完整细节见[P1-A技术约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)，实际软件和实机状态见[P1-A状态](../subline/P1-A/DEVELOPMENT_STATUS.md)。作者不需要复制这些内部流程才能制作作品。
