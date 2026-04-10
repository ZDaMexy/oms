# OMS 皮肤制作手册

> 面向三类读者：
> 1. 想给 BMS 做可落地皮肤的人
> 2. 想在仓库内写代码型 skin provider 的开发者
> 3. 想试验当前 mania 候选包与 `skin.ini` 的制作者
>
> 本文只写仓库里已经落地并验证过的能力，不把“未来计划”伪装成“当前可用工作流”。

## 先说结论

- 现在真正适合投入制作的是 **BMS**，而且优先路线是 **代码型 provider**，不是纯素材包。
- 现在最稳定的契约是 **BMS lookup 类型、组件边界、状态回调接口、fallback 粒度**。
- 现在最不稳定的部分是 **BMS 纯素材目录结构、图片命名、`skin.ini` 桥接格式、最终公开分发包格式**。
- 当前 mania 可以做 **legacy/候选包试验**，但 **不能**把它当作已经冻结的 OMS 皮肤制作流程。
- 如果你现在就想开始做，最稳妥的做法是：
  1. 先选一个 BMS 组件族
  2. 先写代码型 provider
  3. 先验证 lookup 和状态接口
  4. 等资源命名冻结后再固化成纯素材包

## 你现在该走哪条路

| 目标 | 现在应该怎么做 | 是否推荐 | 原因 |
| --- | --- | --- | --- |
| 做 BMS 局部 override | 写 BMS 代码型 provider，只替换目标组件 | 推荐 | lookup 和 fallback 已稳定到组件粒度 |
| 做 BMS 完整视觉原型 | 写 BMS 代码型 provider，逐块替换 | 推荐 | 当前最稳的是代码入口，不是目录规范 |
| 做 BMS 纯图片皮肤并对外分发 | 先做视觉与组件拆分，暂不锁目录 | 暂不推荐直接定版 | 资源目录、图片命名、`skin.ini` 桥接都还没冻结 |
| 试验当前 mania 候选包 | 用 legacy/`skin.ini` 方式做试验 | 可做试验 | 仍有一部分路径走 legacy 资源/命名 |
| 做正式 OMS mania 皮肤 | 先不要当主目标 | 不推荐 | mania OMS-owned 默认路径只完成了第一批 Stage/Column/Key 壳层迁移 |

## 先理解当前真实工作流

OMS 当前实际存在两条皮肤制作路线：

1. **BMS 代码型 provider 路线**
   - 这是当前最推荐、最稳的路线。
   - 你直接实现 `ISkin.GetDrawableComponent()`，按 BMS lookup 返回自己的 `Drawable`。
   - 入口和契约见 [osu.Game.Rulesets.Bms/Skinning/BmsSkinLookups.cs](osu.Game.Rulesets.Bms/Skinning/BmsSkinLookups.cs)、[osu.Game.Rulesets.Bms/Skinning/BmsSkinComponentLookup.cs](osu.Game.Rulesets.Bms/Skinning/BmsSkinComponentLookup.cs)、[osu.Game.Rulesets.Bms/Skinning/BmsSkinTransformer.cs](osu.Game.Rulesets.Bms/Skinning/BmsSkinTransformer.cs)。

2. **legacy/`skin.ini` mania 试验路线**
   - 这是当前还可以用于试验的路线，但不是 OMS 最终 authoring contract。
   - 入口是 `LegacySkin` 体系；OMS 内置预览皮肤 [osu.Game/Skinning/OmsSkin.cs](osu.Game/Skinning/OmsSkin.cs) 也是挂在这条链上。
   - 候选包样例见 [SKIN/SimpleTou-Lazer/skin.ini](SKIN/SimpleTou-Lazer/skin.ini)。

如果你是第一次开始做 OMS 皮肤，**默认选 BMS 代码型 provider**。只有在你明确知道自己是在试验 legacy mania 资源语义时，才去碰 `skin.ini`。

## 工作流 A：BMS 代码型 provider 制作手册

### Step 1：先决定覆盖范围，不要一上来做“整套皮肤”

当前 BMS 是按组件 fallback，不是一张图缺失就整套回退。因此你应该先选一个清晰的目标族：

1. HUD：`HudLayout` / `GaugeBar` / `ComboCounter`
2. Results：`ResultsSummaryPanel` / `ResultsSummary` / `ClearLamp` / `GaugeHistoryPanel` / `GaugeHistory`
3. Song Select：`NoteDistributionPanel` / `NoteDistribution`
4. Playfield 壳层：`Backdrop` / `Baseplate` / lane `Background` / `Divider`
5. Gameplay accent：`HitTarget` / `BarLine` / `LaneCover` / `StaticBackgroundLayer`
6. Note 主体：`Note` / `LongNoteHead` / `LongNoteBody` / `LongNoteTail`
7. Judgement：BMS 自定义判定显示

推荐制作顺序：

1. HUD
2. Results
3. Song Select
4. Playfield 壳层与 accent
5. Note / Hold
6. Judgement

原因很简单：前 3 组的接口最清晰、回归最容易、状态复杂度最低。

### Step 2：按组件准备素材，不要按“整套皮肤”空想

当前最实用的做法，是按下面这张表准备素材和状态。

| 组件族 | lookup / 入口 | 你至少要准备什么 | 运行时必须覆盖的状态 |
| --- | --- | --- | --- |
| HUD | `BmsSkinComponentLookup(HudLayout/GaugeBar/ComboCounter)` | gauge 条主体、combo 样式、HUD 布局方案 | `HudLayout` 必须接收 wrapped HUD、gauge、combo |
| Results | `ResultsSummaryPanel` / `ResultsSummary` / `ClearLamp` / `GaugeHistoryPanel` / `GaugeHistory` | panel 外壳、summary 内容、clear lamp、gauge timeline | 有数据 / 无数据 两种状态 |
| Song Select | `NoteDistributionPanel` / `NoteDistribution` | panel 外壳、分布图本体、摘要文字样式 | 有分布数据 / 无分布数据 |
| Playfield 壳层 | `BmsPlayfieldSkinLookup` | backdrop、baseplate | 不同 `Keymode` / `LaneCount` |
| Lane 壳层 | `BmsLaneSkinLookup` | lane background、divider | `IsScratch`、不同 lane count |
| Receptor / 节拍线 | `BmsLaneSkinLookup` | hit target、bar line | `pressed / focused`、`major / minor` |
| Note / Hold | `BmsNoteSkinLookup` | note、LN 头、LN 身、LN 尾 | `IsScratch`、不同 note element |
| LaneCover | `BmsLaneCoverSkinLookup` | top cover、bottom cover、focus 表现 | top / bottom / focused |
| Static BG / 元数据壳层 | `BmsSkinComponentLookup(StaticBackgroundLayer)` | 有背景文件时的显示、无背景文件时的缺省态 | 有 `STAGEFILE/BACKBMP` / 无素材 |
| Judgement | `BmsJudgementSkinLookup` | BAD / POOR / EMPTY POOR 的显示方案 | 动画触发、前景代理内容 |

最容易漏掉的点：

- scratch lane 和 normal lane 必须可读地区分。
- `LaneCover` 不是一张黑条图就完事，至少要有 top / bottom / focused 的差异。
- `BarLine` 必须考虑 major / minor。
- `StaticBackgroundLayer` 必须处理“没有背景资源”的显示。
- `HudLayout` 不是普通容器，它必须负责把 wrapped HUD、gauge 和 combo 排进去。

### Step 3：先写最小 provider 骨架

当前 BMS 最小 skin provider 的形状，其实和测试里的 `TestSkin` 一样。参考 [osu.Game.Rulesets.Bms.Tests/BmsSkinTransformerTest.cs](osu.Game.Rulesets.Bms.Tests/BmsSkinTransformerTest.cs)。

最小骨架如下：

```csharp
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Textures;
using osu.Game.Rulesets.Bms.Skinning;
using osu.Game.Skinning;

public sealed class MyBmsSkin : ISkin
{
   public Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
      => lookup switch
      {
         BmsSkinComponentLookup { Component: BmsSkinComponents.HudLayout } => new MyHudLayout(),
         BmsSkinComponentLookup { Component: BmsSkinComponents.GaugeBar } => new MyGaugeBar(),
         BmsPlayfieldSkinLookup { Element: BmsPlayfieldSkinElements.Backdrop } => new MyBackdrop(),
         BmsLaneSkinLookup { Element: BmsLaneSkinElements.Background } lane => new MyLaneBackground(lane.IsScratch),
         BmsLaneSkinLookup { Element: BmsLaneSkinElements.HitTarget } lane => new MyHitTarget(lane.IsScratch),
         BmsNoteSkinLookup { Element: BmsNoteSkinElements.Note } note => new MyNote(note.IsScratch),
         BmsLaneCoverSkinLookup cover => new MyLaneCover(cover.Position),
         BmsJudgementSkinLookup judgement => new MyJudgement(judgement.Result),
         _ => null,
      };

   public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT) => null;

   public ISample? GetSample(ISampleInfo sampleInfo) => null;

   public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
      where TLookup : notnull
      where TValue : notnull
      => null;
}
```

重点：

- 当前 BMS 的正式 authoring 入口几乎都在 `GetDrawableComponent()`。
- 如果你不打算自己提供纹理/音效查询，`GetTexture()` 和 `GetSample()` 可以先返回 `null`。
- 当前 BMS 没有稳定公开的 ruleset 级 `skin.ini` config bridge，所以 `GetConfig()` 通常也先返回 `null`。

### Step 4：哪些组件必须实现接口，不实现就会被替换掉

`BmsSkinTransformer` 不只是“有就拿，没有就回退”。对某些组件它会要求你返回的对象实现特定接口；如果没实现，它会直接退回默认实现。参考 [osu.Game.Rulesets.Bms/Skinning/BmsSkinTransformer.cs](osu.Game.Rulesets.Bms/Skinning/BmsSkinTransformer.cs)。

| 组件 | 你必须实现的接口 | 运行时会调用什么 |
| --- | --- | --- |
| `HudLayout` | `IBmsHudLayoutDisplay` | `SetComponents(Drawable? wrappedHud, Drawable gaugeBar, ComboCounter comboCounter)` |
| `LaneCover` | `IBmsLaneCoverDisplay` | `SetFocused(bool isFocused)` |
| `StaticBackgroundLayer` | `IBmsBackgroundLayerDisplay` | `SetDisplayedAssetName(string displayedAssetName)` |
| `GaugeHistoryPanel` | `IBmsGaugeHistoryPanelDisplay` | `SetHistory(BmsGaugeHistory? history)` |
| `GaugeHistory` | `IBmsGaugeHistoryDisplay` | `SetHistory(BmsGaugeHistory? history)` |
| `ResultsSummaryPanel` | `IBmsResultsSummaryPanelDisplay` | `SetSummary(BmsResultsSummaryData? summary)` |
| `ResultsSummary` | `IBmsResultsSummaryDisplay` | `SetSummary(BmsResultsSummaryData? summary)` |
| `ClearLamp` | `IBmsClearLampDisplay` | `SetClearLamp(BmsClearLampData? clearLamp)` |
| `NoteDistributionPanel` | `IBmsNoteDistributionPanelDisplay` | `SetState(BmsNoteDistributionPanelState? state)` |
| `NoteDistribution` | `IBmsNoteDistributionDisplay` | `SetData(BmsNoteDistributionData? data)` |
| `BMS judgement` | `IAnimatableJudgement` | `PlayAnimation()` 和 `GetAboveHitObjectsProxiedContent()` |

最常见的坑：

- 你返回了自定义 `HudLayout`，但它没实现 `IBmsHudLayoutDisplay`，于是运行时仍然显示默认 HUD。
- 你返回了自定义 `LaneCover`，但没实现 `SetFocused()`，于是 focus 状态根本不会传进去。
- 你返回了自定义判定显示，但没实现 `IAnimatableJudgement`，于是会被判定为不合格并退回默认显示。

### Step 5：按 lookup 数据做变体，不要复制一堆几乎一样的类

当前 BMS lookup 已经把你需要的分支条件带进来了：

| lookup | 你能拿到的数据 | 应该怎么用 |
| --- | --- | --- |
| `BmsPlayfieldSkinLookup` | `Element`、`Keymode`、`LaneCount` | backdrop/baseplate 根据 keymode 和 lane 数做尺寸/装饰差异 |
| `BmsLaneSkinLookup` | `Element`、`LaneIndex`、`LaneCount`、`IsScratch`、`Keymode`、`IsMajorBarLine` | lane 背景、divider、hit target、bar line 都应读这些值，而不是写死 7K |
| `BmsNoteSkinLookup` | `Element`、`LaneIndex`、`IsScratch` | note / LN head / LN body / LN tail 用同一套类型分支处理 |
| `BmsLaneCoverSkinLookup` | `Position` | 顶盖和底盖优先共用同一套实现，再按 position 分支 |
| `BmsJudgementSkinLookup` | `Result`、`DisplayName` | BAD / POOR / EMPTY POOR 等自定义命名从这里拿，不要自己硬编码另一套名字 |

当前已经公开给皮肤作者的输入状态就是这些。优先从 lookup 和接口回调拿数据，不要偷看外层容器的临时布局细节。

### Step 6：最小可抄的接口实现模板

`HudLayout`：

```csharp
private sealed partial class MyHudLayout : Container, IBmsHudLayoutDisplay
{
   public void SetComponents(Drawable? wrappedHud, Drawable gaugeBar, ComboCounter comboCounter)
   {
      Clear();

      if (wrappedHud != null)
         Add(wrappedHud);

      Add(gaugeBar);
      Add(comboCounter);

      gaugeBar.Anchor = Anchor.TopCentre;
      gaugeBar.Origin = Anchor.TopCentre;

      comboCounter.Anchor = Anchor.TopCentre;
      comboCounter.Origin = Anchor.TopCentre;
   }
}
```

`LaneCover`：

```csharp
private sealed partial class MyLaneCover : CompositeDrawable, IBmsLaneCoverDisplay
{
   public void SetFocused(bool isFocused)
   {
      Alpha = isFocused ? 1f : 0.85f;
   }
}
```

`StaticBackgroundLayer`：

```csharp
private sealed partial class MyBackgroundLayer : CompositeDrawable, IBmsBackgroundLayerDisplay
{
   public void SetDisplayedAssetName(string displayedAssetName)
   {
      // 有资源名时显示资源名，没有时显示缺失态。
   }
}
```

`Judgement`：

```csharp
private sealed partial class MyJudgement : CompositeDrawable, IAnimatableJudgement
{
   public void PlayAnimation()
   {
      // 从当前时间点开始播你的判定动画。
   }

   public Drawable? GetAboveHitObjectsProxiedContent() => null;
}
```

### Step 7：怎么调样式，才不会和当前 contract 打架

1. 先把布局和状态逻辑放进代码，不要急着锁图片目录。
2. 优先让一个组件内部吃完自己的状态，不要依赖“外层再帮你补一层特殊判断”。
3. 如果一个组件需要 scratch / normal 两种表现，优先在同一类里按 `lookup.IsScratch` 分支。
4. 如果一个组件需要 major / minor 两种节拍线，优先在同一类里按 `lookup.IsMajorBarLine` 分支。
5. `HudLayout` 要保留 `wrappedHud`，不要把外层 HUD 内容吞掉。
6. 用 partial override 思维回归：只替换一个组件时，其余组件必须继续工作。

### Step 8：怎么验证你写的皮肤

当前最直接的验证入口是 [osu.Game.Rulesets.Bms.Tests/BmsSkinTransformerTest.cs](osu.Game.Rulesets.Bms.Tests/BmsSkinTransformerTest.cs)。

推荐验证顺序：

1. 先写一个只覆盖单个组件的测试 skin。
2. 先确认 `BmsSkinTransformer` 是否拿到了你的组件。
3. 再确认缺失组件是否只回退自身，而不是拖垮整套皮肤。
4. 再补 scratch / normal、major / minor、top / bottom / focused 这类状态回归。

建议至少手动检查这些场景：

1. 7K 与 14K 的 lane 数变化
2. scratch lane 与 normal lane 的差异
3. LaneCover focused / unfocused 切换
4. StaticBackgroundLayer 有图 / 无图
5. Results / Song Select 的有数据 / 无数据状态

当前建议使用的聚焦测试命令：

```powershell
dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"
```

## 工作流 B：legacy / `skin.ini` mania 试验手册

这一节只适用于：

1. 你明确是在做 **legacy mania 资源试验**
2. 你在研究当前候选包 [SKIN/SimpleTou-Lazer/skin.ini](SKIN/SimpleTou-Lazer/skin.ini)
3. 你接受它 **不是** 当前 OMS 正式 authoring contract

### 这条路线现在能做什么

- 可以快速试验 mania 的 legacy 图片命名与 `skin.ini` section 写法。
- 可以给当前候选包做资源替换、路径试验、颜色试验。
- 可以研究 `KeyImage*`、`NoteImage*`、`Colour*` 这一类 legacy mania 语义。

### 这条路线现在不能承诺什么

- 不能当作 BMS 正式皮肤制作格式。
- 不能当作 OMS mania 最终公开 contract。
- 不能假设你调的所有 `skin.ini` 值都会实时控制 `OmsSkin` preview 的布局和壳层行为。

### 目录怎么准备

一个最小 legacy mania 试验目录大致长这样：

```text
MyLegacySkin/
  skin.ini
  mania-note1.png
  mania-note2.png
  mania-noteS.png
  4k/
   1.png
  Notes4K/
   LNBody.png
```

规则：

1. 路径相对 `skin.ini` 所在目录。
2. `skin.ini` 里通常写 **不带扩展名** 的资源名。
3. 不同 keycount 用重复的 `[Mania]` section 区分。

### `skin.ini` 最小写法

当前候选包的真实样例见 [SKIN/SimpleTou-Lazer/skin.ini](SKIN/SimpleTou-Lazer/skin.ini)。

最小 7K 样例可以长这样：

```ini
[General]
Name: MyLegacyManiaTest
Author: You
Version: 2.7

[Mania]
Keys: 7
ColumnStart: 270
HitPosition: 475
JudgementLine: 0
ScorePosition: 100
ComboPosition: 90
LightFramePerSecond: 0
LightPosition: 0
ColumnWidth: 47,47,47,47,47,47,47
WidthForNoteHeightScale: 35

Colour1: 0,0,0,255
Colour2: 0,0,0,255
Colour3: 0,0,0,255
Colour4: 0,0,0,255
Colour5: 0,0,0,255
Colour6: 0,0,0,255
Colour7: 0,0,0,255
ColourBarline: 255,255,255,150

KeyImage0: 4k\1
KeyImage1: 4k\1
KeyImage2: 4k\1
KeyImage3: 4k\1
KeyImage4: 4k\1
KeyImage5: 4k\1
KeyImage6: 4k\1

KeyImage0D: 4k\1
KeyImage1D: 4k\1
KeyImage2D: 4k\1
KeyImage3D: 4k\1
KeyImage4D: 4k\1
KeyImage5D: 4k\1
KeyImage6D: 4k\1

NoteImage0: mania-note1
NoteImage1: mania-note2
NoteImage2: mania-note1
NoteImage3: mania-noteS
NoteImage4: mania-note1
NoteImage5: mania-note2
NoteImage6: mania-note1

NoteImage0H: mania-note1
NoteImage1H: mania-note2
NoteImage2H: mania-note1
NoteImage3H: mania-noteS
NoteImage4H: mania-note1
NoteImage5H: mania-note2
NoteImage6H: mania-note1

NoteImage0L: A
NoteImage1L: A
NoteImage2L: A
NoteImage3L: A
NoteImage4L: A
NoteImage5L: A
NoteImage6L: A

NoteImage0T: Notes4K\LNBody
NoteImage1T: Notes4K\LNBody
NoteImage2T: Notes4K\LNBody
NoteImage3T: Notes4K\LNBody
NoteImage4T: Notes4K\LNBody
NoteImage5T: Notes4K\LNBody
NoteImage6T: Notes4K\LNBody

ColumnLineWidth: 0,0,0,0,0,0,0,0
```

### `skin.ini` 里这些字段现在各自意味着什么

| 字段 | 当前用途 | 备注 |
| --- | --- | --- |
| `[General]` | 名称、作者、版本 | legacy 入口基础信息 |
| `[Mania]` + `Keys:` | 定义一个 keycount section | 4K/5K/6K/7K/8K/9K 分开写 |
| `KeyImage*` / `KeyImage*D` | key 正常态 / 按下态图 | 当前 `OmsSkin` preview 已由 `OmsManiaKeyAssetPreset` 返回 stage-local 资源名，但仍适合做 legacy 资源试验 |
| `NoteImage*` / `NoteImage*H` / `NoteImage*L` / `NoteImage*T` | note / LN 各部分图 | 当前 `OmsSkin` preview 已由 `OmsManiaNoteAssetPreset` 返回 stage-local 资源名，但仍适合做 legacy 资源试验 |
| `Colour*` / `ColourLight*` | legacy 颜色语义 | 当前仍属过渡语义；`OmsSkin` preview 已开始把其中一部分 shell colour 收口到 OMS preset，不要锁成公开 contract |
| `ColumnStart` | legacy mania 位置语义 | 当前只适合做 legacy 试验 |
| `ComboPosition` | legacy HUD 位置语义 | 当前 `OmsSkin` preview 已有 shared preset，mixed-stage non-column 路径固定复用第一 stage preset，但仍不应视为稳定公开 contract |
| `ScorePosition` | legacy judgement 位置语义 | 当前 `OmsSkin` preview 已有 shared preset，mixed-stage non-column 路径固定复用第一 stage preset，但仍不应视为稳定公开 contract |
| `ColourBarline` | legacy bar line 颜色语义 | 当前 `OmsSkin` preview 已有 shared preset，mixed-stage non-column 路径固定复用第一 stage preset，但仍不应视为稳定公开 contract |
| `BarlineHeight` | legacy bar line 厚度语义 | 当前 `OmsSkin` preview 已有 shared preset，mixed-stage non-column 路径固定复用第一 stage preset，但仍不应视为稳定公开 contract |
| `HitPosition` / `ColumnWidth` / `LightPosition` / `LightFramePerSecond` / `JudgementLine` / `ColumnLineWidth` | 你会在 legacy mania 里看到这些字段 | **但对 `OmsSkin` preview 不应再当成稳定的最终调参接口** |

### 当前 `OmsSkin` preview 下，哪些 mania 值已经不是 `skin.ini` 开关

这部分非常重要。

当前 [osu.Game.Rulesets.Mania/Skinning/Oms/ManiaOmsSkinTransformer.cs](osu.Game.Rulesets.Mania/Skinning/Oms/ManiaOmsSkinTransformer.cs) 已经把一批运行时值从 raw legacy lookup 收口到 OMS preset：

1. `OmsManiaLayoutPreset` 负责：
   - `HitPosition`
   - `ColumnWidth`
   - `LeftColumnSpacing`
   - `RightColumnSpacing`
   - `StagePaddingTop`
   - `StagePaddingBottom`
2. `OmsManiaShellPreset` 负责：
   - `LeftLineWidth`
   - `RightLineWidth`
   - `ShowJudgementLine`
   - `LightPosition`
   - `LightFramePerSecond`
3. `OmsManiaShellAssetPreset` 负责：
   - `LeftStageImage`
   - `RightStageImage`
   - `BottomStageImage`
   - `HitTargetImage`
   - `LightImage`
   - `KeysUnderNotes`
4. `OmsManiaColumnColourPreset` 负责：
   - `ColumnLineColour`
   - `JudgementLineColour`
   - `ColumnBackgroundColour`
   - `ColumnLightColour`
5. `OmsManiaKeyAssetPreset` 负责：
   - `KeyImage`
   - `KeyImageDown`
6. `OmsManiaNoteAssetPreset` 负责：
   - `NoteImage`
   - `HoldNoteHeadImage`
   - `HoldNoteTailImage`
   - `HoldNoteBodyImage`
7. `OmsManiaJudgementAssetPreset` 负责：
   - `Hit300g`
   - `Hit300`
   - `Hit200`
   - `Hit100`
   - `Hit50`
   - `Hit0`
8. `OmsManiaJudgementPositionPreset` 负责：
   - `ScorePosition`
   - `ComboPosition`
9. `OmsManiaBarLinePreset` 负责：
   - `BarLineHeight`
   - `BarLineColour`
10. `OmsManiaHitExplosionPreset` 负责：
   - `ExplosionImage`
   - `ExplosionScale`

这意味着：

- 你如果在当前 `OmsSkin` preview 路径里调这些值，不应该再把 `skin.ini` 当成唯一或最终生效来源。
- 这些值现在是 OMS 为 stage-local 行为、共享 shell asset、shared judgement asset、shared judgement position、首批 shell colour 与首个 hitburst config 收口后返回的 preset 值。
- 特别是 mixed-stage 场景下，带列上下文的 lookup 会按所在 stage keycount 取 preset；没有列上下文的 shared lookup（例如 `HitPosition` / `ScorePosition` / `ComboPosition` / `BarLineHeight` / `BarLineColour`）当前固定复用第一 stage keycount 的 preset，而不是照抄一个总列数 section。

目前仍要注意边界：

- `KeyImage*` / `KeyImage*D` 现在已经迁到 `OmsManiaKeyAssetPreset`；当前 `OmsSkin` preview 会按 stage keycount 返回资源名，其中 4K/6K/7K/9K 继续复用候选 `4k\1`，8K 继续使用 `7k\0..7` / `7k\0p..7p`，5K 则显式回到 OMS 侧 `mania-key1` / `mania-key2` 与 `mania-key1D` / `mania-key2D`。
- `NoteImage*` / `NoteImage*H` / `NoteImage*L` / `NoteImage*T` 现在已经迁到 `OmsManiaNoteAssetPreset`；当前 `OmsSkin` preview 会按 stage keycount 返回 note/head/tail/body 资源名，其中 5K 会显式回到 OMS 侧 `mania-note1/2` 与对应的 `H/T/L` 变体，9K 会继续使用 `mania-noteS/1/2` 与 `SH/ST/SL` 变体，而 4K/6K/7K/8K 仍保持候选包现有的 legacy 资源命名（包括 `A`、`Notes4K\LNBody`、`Notes4K\LNTail` 这一类名字）。
- `ManiaSkinComponents.Note` / `ManiaSkinComponents.HoldNoteHead` / `ManiaSkinComponents.HoldNoteTail` / `ManiaSkinComponents.HoldNoteBody` 现在也已分别显式接到 `OmsNotePiece` / `OmsHoldNoteHeadPiece` / `OmsHoldNoteTailPiece` / `OmsHoldNoteBodyPiece`；当前 `DrawableNote` / `DrawableHoldNoteHead` / `DrawableHoldNoteTail` 与 `DrawableHoldNote` 内部的 `bodyPiece` 都会实际加载 OMS note / hold 组件，且这四个组件都已升格为不再继承 `LegacyNotePiece` / `LegacyHoldNoteHeadPiece` / `LegacyHoldNoteTailPiece` / `LegacyBodyPiece` 的实际 OMS-owned implementation。其中 `OmsHoldNoteBodyPiece` 现已把 `NoteBodyStyle` / `HoldNoteLightImage` / `HoldNoteLightScale` 收口到 `OmsManiaHoldNoteBodyPreset`，并固定使用 OMS stretch 语义；`WidthForNoteHeightScale` 现也已收口到 `OmsManiaLayoutPreset`，`OmsNotePiece` 会按列读取 stage-local note-height，因此 mixed-stage note-height 不再落回第一 stage / total-columns fallback。当前 note / hold 路径剩余 gap 主要收窄为 note scrolling、tail inversion，以及 hold-body hit-light / fade 表现的后续语义整理。
- `ScorePosition` / `ComboPosition` 现在也已有 `OmsManiaJudgementPositionPreset`，而 MainHUDComponents 路径下的 combo 现在也已显式接到 `OmsManiaComboCounter`；当前 `OmsSkin` preview 会在 single-stage、same-keycount dual-stage 与 mixed-stage 的 non-column 路径下返回 OMS-owned 的 shared judgement / HUD position，其中 mixed-stage 固定复用第一 stage preset，`DrawableManiaJudgement` 与 `OmsManiaComboCounter` 都不会再因 total-columns lookup 落回 10K 一类 legacy 默认值；另外 combo 文本路径现也已不再使用 `LegacySpriteText` / `LegacyFont.Combo`。当前剩余项收窄为 shared combo-position 之上的 rolling/fade/HUD 语义，而不是 legacy 字体图集依赖。
- `BarLineHeight` / `BarLineColour` 现在也已有 `OmsManiaBarLinePreset`；当前 `OmsSkin` preview 会在 single-stage、same-keycount dual-stage 与 mixed-stage 的 non-column 路径下返回 OMS-owned 的 shared bar-line config，其中 mixed-stage 固定复用第一 stage preset，而 `ManiaSkinComponents.BarLine` 也已显式接到 `OmsBarLine`，所以 `DrawableBarLine` 不会再因 total-columns lookup 落回 18K 一类 legacy 默认值；但该组件仍继续消费 shared bar-line config 与 legacy bar-line 语义，因此完整 OMS-owned bar line 组件路径仍未迁完。
- `Hit300g` / `Hit300` / `Hit200` / `Hit100` / `Hit50` / `Hit0` 现在已经迁到 `OmsManiaJudgementAssetPreset`，而 `SkinComponentLookup<HitResult>` 也已显式迁到 `OmsManiaJudgementPiece` 路径；当前 `OmsSkin` preview 会稳定返回 `mania-hit300g` / `mania-hit300` / `mania-hit200` / `mania-hit100` / `mania-hit50` / `mania-hit0` 这一组共享资源名，`DrawableManiaJudgement` 也会实际加载 OMS judgement piece，但这仍然只是 judgement 资源名 + 组件入口收口；mixed-stage 的 non-column positioning 已按第一 stage preset 收口，不过 legacy animation 语义与更完整的 OMS-owned judgement / HUD 路径还没有迁完。
- `ExplosionImage` / `ExplosionScale` 现在已经迁到 `OmsManiaHitExplosionPreset`；当前 `OmsSkin` preview 会稳定返回 `lightingN` 与按各 stage 列宽预设换算出的 hitburst scale，因此 mixed-stage 场景下不会再把 5K 与 8K 的 explosion scale 混成同一套 absolute-column fallback；但这仍然只是 hitburst 配置收口，`LegacyHitExplosion` 本身与更完整的 OMS-owned hitburst 默认路径还没有迁完。
- `HitExplosion` 组件现在也已经显式迁到 `OmsHitExplosion` 路径；当前 `PoolableHitExplosion` 会实际加载 OMS hit explosion 组件，但它仍继续消费上面的 `ExplosionImage` / `ExplosionScale` preset，所以这一步也还不是完整的 OMS-owned hitburst 默认视觉路径。
- `Colour*` 虽然已有首批 shell colour 被 `OmsManiaColumnColourPreset` 接管，但这不等于 mania authoring contract 已冻结；它仍然只是当前 `OmsSkin` preview 路径里的 OMS-owned 过渡收口。

另一个容易踩坑的点：legacy 解码本身会做归一化，不是你写多少就等于运行时多少。例如：

- `LightFramePerSecond: 0` 在 legacy 语义里会归一成 `24`
- `LightPosition` 写的是 legacy 原始值，不是最终屏幕像素

所以如果你现在是在调 mania 候选包，请把它理解为 **legacy 试验环境**，不是正式 OMS authoring API。

## 当前 BMS 组件契约速查表

如果你只是想知道“我现在能覆盖什么”，看这张表就够了。

| 类别 | lookup / 入口 | 现在是否适合正式制作 | 说明 |
| --- | --- | --- | --- |
| HUD | `HudLayout` / `GaugeBar` / `ComboCounter` | 适合 | 当前最稳、最推荐先做 |
| Results | `ResultsSummaryPanel` / `ResultsSummary` / `ClearLamp` / `GaugeHistoryPanel` / `GaugeHistory` | 适合 | 面板外壳与内容已分离 |
| Song Select | `NoteDistributionPanel` / `NoteDistribution` | 适合 | 面板与图表已分离 |
| Playfield 壳层 | `Backdrop` / `Baseplate` | 适合 | 通过 `BmsPlayfieldSkinLookup` 进入 |
| Lane 壳层 | `Background` / `Divider` | 适合 | 注意 scratch / normal 差异 |
| Receptor / 节拍线 | `HitTarget` / `BarLine` | 适合 | 注意 pressed / focused 与 major / minor |
| Note / Hold | `Note` / `LongNoteHead` / `LongNoteBody` / `LongNoteTail` | 适合 | 当前 contract 已足够做正式视觉 |
| LaneCover | `BmsLaneCoverSkinLookup` | 适合 | 记得实现 `IBmsLaneCoverDisplay` |
| Static BG | `StaticBackgroundLayer` | 适合 | 记得处理无图缺失态 |
| BMS 自定义 judgement | `BmsJudgementSkinLookup` | 可做 | 需要实现 `IAnimatableJudgement` |
| BMS 纯素材包目录规范 | 无冻结格式 | 不适合定版 | 等资源命名冻结后再锁 |
| mania OMS 正式制作 | 仍在迁移中 | 不适合作为主目标 | 当前只完成第一批壳层与 preset 收口 |

## 当前还没有冻结的部分

以下内容 **不要** 写进对外承诺、不要提早做成“最终规范”：

- BMS 纯素材目录结构
- BMS 图片命名规范
- BMS `skin.ini` 桥接格式
- 面向终端玩家分发的完整 OMS 默认皮肤包定义
- mania OMS-owned 默认路径的最终资源和配置合同
- playfield / note / hold / judgement 的最终高保真官方视觉语言

## 如果你现在就要开始做

直接照这个顺序做：

1. 先从 HUD 或 Results 选一个组件族
2. 先写一个最小 `ISkin` provider
3. 先让它通过 `BmsSkinTransformer` 路由到你的组件
4. 再补接口实现和状态回调
5. 再做 partial override 回归
6. 最后才考虑是否把视觉资源整理成未来可分发目录

## 后续追踪文档

需要确认当前真实状态时，优先看：

- [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)：当前哪些路径已经落地，哪些仍在迁移
- [CHANGELOG.md](CHANGELOG.md)：每次已验证通过的皮肤相关变更
- [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)：下一批还会怎么推进
- [OMS_COPILOT.md](OMS_COPILOT.md)：权威产品边界、fallback 纪律与 release gate
