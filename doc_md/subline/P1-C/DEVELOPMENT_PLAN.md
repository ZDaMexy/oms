# P1-C 开发计划：判定语义、绿色数字与反馈闭环

> 最后更新：2026-04-20
> 主线总规划见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。本文件只拆解 `P1-C` 的执行顺序；皮肤边界与 HUD 宿主合同见 [../P1-A/DEVELOPMENT_PLAN.md](../P1-A/DEVELOPMENT_PLAN.md)。

## 子线定位

| 维度 | 归属 | 说明 |
| --- | --- | --- |
| 主归属 | `P1-C` | 收口 BRJ / LR2 parity、绿色数字与速度反馈、results feedback 与训练反馈闭环 |
| 协作子线 | `P1-A` | `P1-C` 的 feedback family 必须建立在 `P1-A` 冻结的 BMS HUD 宿主与皮肤边界上 |
| 参考输入 | `other` | 受 [../../other/IIDX_REFERENCE_AUDIT.md](../../other/IIDX_REFERENCE_AUDIT.md) 约束，但当前不等价于完整 `FHS` |

## 当前确认基线

- 当前 `BmsScrollSpeedMetrics` 已能按 `Normal / Floating / Classic Hi-Speed + Sudden / Hidden / Lift` 计算 `VisibleLaneTime`、`WhiteNumber`、`GreenNumber`；其中 `Classic` 继续锁定官方 sample `HS 10 + WN 350 => GN 300`，`Floating` 目前为 initial-BPM anchored runtime surface。
- 当前 runtime 已同时具备 `BmsSpeedMetricsToast` 与常驻 `GameplayFeedbackDisplay`；toast 只承担操作确认，常驻 feedback card 承担权威表达。
- 当前 runtime 已具备 5 秒 delayed-start + pre-start hold 调速窗口；`UI_LaneCoverFocus` 作为阻塞键，奇偶列按键用于调节当前 Hi-Speed，滚轮 / 中键则继续用于 lane-cover 调整与 target cycle。
- 当前 results 页已完成 `DJ LEVEL` / `EX-SCORE` 第一轮收口，但还没有形成完整训练反馈闭环。
- 当前 `BEATORAJA` / `LR2` judge mode 已接通基础路径，但 early/late 非对称窗口、excessive poor 与更完整 long-note release parity 仍未收口。

## 分期计划

### C0：文档与方向冻结

**状态：已完成**

- 明确 `P1-C` 的工作不等价于提前进入 Phase 2 `FHS`。
- 把绿色数字、速度反馈、判定语义与训练反馈统一归到同一子线。

### C1：权威绿色数字常驻反馈

**状态：首轮实现已完成，后续进入 C2 / C3 收口**

目标：让当前 `GN + 可见毫秒 + adjustment target` 不再只存在于 toast，而是拥有常驻、可皮肤化的权威 HUD 表达。

#### C1 外部参考基线（2026-04-20 已验证）

以下事实来源于 [iidx.org](https://iidx.org/) 并经代码对照确认：

| 术语 | IIDX | beatoraja | LR2 | OMS 当前 |
| --- | --- | --- | --- | --- |
| **Green Number** | `10 × (note 可见帧数 @60fps)` = `VisibleTime_ms × 0.6` | 默认直接显示原始毫秒；部分皮肤同时显示 IIDX GN（绿字）与原始 ms（蓝字） | 依 skin 而变 | `VisibleLaneTime × 0.6`，与 IIDX 公式一致 ✅ |
| **White Number** | `SUDDEN+ 遮挡面积 / 全屏高 × 1000`，不受 LIFT 影响 | 无 LIFT 时同 IIDX；有 LIFT 时以 `(全高 - LIFT)` 为基准重新计算 | 依 skin 而变 | `= SuddenUnits`，语义同 IIDX ✅ |
| **LIFT** | `底部抬升面积 / 全屏高 × 1000`，与 SUDDEN+ 独立 | 同 IIDX | — | 独立 geometry control，影响 `ScrollLengthRatio` ✅ |
| **VisibleLaneUnits** | `1000 - SUD - HID`（LIFT 不参与） | 同上 | — | `1000 - SuddenUnits - HiddenUnits` ✅ |
| **GN 典型区间** | 250（极快）– 330（偏慢） | 416ms – 550ms（等价 IIDX GN 250-330） | — | 取决于用户 HS + cover |
| **Normal Hi-Speed** | BPM 无关，基础流速型 | 类似 | — | 已作为默认 settings surface 落地；runtime 已接 HUD / toast / pre-start overlay ✅ |
| **Classic Hi-Speed** | BPM 依赖，倍率型 | 类似 | BPM 依赖 | strict Classic surface 已按官方 sample 收口，`ComputeScrollTime = (100000 / 13) / HS`；BPM 变化仍通过 relative beat scaling 改变视觉滚速 ✅ |
| **Floating Hi-Speed** | BPM 无关，恒定视觉速度，需 SUDDEN+/LIFT 才能执行"floating"操作 | 类似 | — | 首轮 surface 已落地：按 initial BPM 锚定 visual speed，并支持 pre-start hold 调速；mid-song re-float / GN range 仍未完成 △ |

> **GN 公式验证**：`BmsScrollSpeedMetrics.GreenNumber = Max(0, Round(VisibleLaneTime × 0.6))`。IIDX 原始公式 = `10 × (VisibleTime / (1000/60))` = `VisibleTime × 0.6`。完全一致，无需修改。

> **Lift 对 GN 的间接影响**：Lift 不参与 `VisibleLaneUnits` 计算（与 IIDX 一致），但 Lift 改变 `BmsHitObjectArea` 内容区几何高度 → `ScrollLengthRatio` 下降 → `RuntimeTimeRange` 下降 → `VisibleLaneTime` 下降 → GN 下降。这条间接链路是正确的。

> **tri-mode 基线**：当前 OMS 已把 `Normal / Floating / Classic` 三模式 settings 与 runtime 反馈接通；其中 `Classic` 的 base time 映射继续保持 `(100000 / 13) / HS` 并锁定 `HS 10 + WN 350 => GN 300`，`Floating` 则暂按 initial BPM 锚定 visual speed。

> **Soflan（曲内 BPM 变化）**：当前 HUD / pre-start overlay 只显示单个 runtime GN，不显示 soflan 范围。IIDX 在完整 FHS 模式下会显示 GN 变动范围（如 "210-840"）；当前 OMS tri-mode surface 仍不引入这类范围显示。

> **归线结论**：当前 tri-mode settings、mode-aware HUD 与 pre-start hold 调速窗口仍归现有 `P1-A / P1-C` 交叉专题，不新开路线；真正后置的是 full Floating parity、soflan GN range 与更严格的 start sequencing。

#### C1 详细实现方案

##### C1.1 提取公共枚举：`BmsGameplayAdjustmentTarget`

- **当前状态**：`BmsGameplayAdjustmentTarget` 是 `DrawableBmsRuleset` 内部 `private enum`。
- **改动**：提取到独立文件 `osu.Game.Rulesets.Bms/UI/BmsGameplayAdjustmentTarget.cs`，设为 `public enum`。
- **枚举值**：`Sudden`、`Hidden`、`Lift`（不新增值）。
- **影响**：`DrawableBmsRuleset` 内的 `currentGameplayAdjustmentTarget` / `getPersistentGameplayAdjustmentTarget()` / toast 等全部改用公共枚举；`formatGameplayAdjustmentTarget()` 也移出或改为公共 static。
- **零行为变更**。

##### C1.2 添加响应式速度指标 Bindable

- **新增字段**（`DrawableBmsRuleset`）：
  ```csharp
  public IBindable<BmsScrollSpeedMetrics> SpeedMetrics => speedMetrics;
  private readonly Bindable<BmsScrollSpeedMetrics> speedMetrics = new();

  public IBindable<BmsGameplayAdjustmentTarget?> ActiveAdjustmentTarget => activeAdjustmentTarget;
  private readonly Bindable<BmsGameplayAdjustmentTarget?> activeAdjustmentTarget = new();
  ```
- **`BmsScrollSpeedMetrics`** 补 `IEquatable<BmsScrollSpeedMetrics>` + `Equals` / `GetHashCode` 覆写，确保 Bindable 仅在值真正变化时触发通知。
- **订阅链**（`LoadComplete` 内）：
  - `configScrollSpeed.BindValueChanged(_ => refreshSpeedMetrics())`
  - `playfieldScrollLengthRatio.BindValueChanged(_ => refreshSpeedMetrics())`
  - `getSuddenMod()?.CoverPercent.BindValueChanged(_ => refreshSpeedMetrics())`
  - `getHiddenMod()?.CoverPercent.BindValueChanged(_ => refreshSpeedMetrics())`
  - `getLiftMod()?.LiftUnits.BindValueChanged(_ => refreshSpeedMetrics())`
- **`refreshSpeedMetrics()`**：调用 `GetScrollSpeedMetrics()` 并写入 `speedMetrics.Value`。
- **`activeAdjustmentTarget`** 在 `CycleGameplayAdjustmentTarget()` 和 `RefreshLaneCoverFocus()` 中同步更新。
- **Toast** 继续工作，保持原有触发逻辑不变。

##### C1.3 皮肤组件注册

- **`BmsSkinComponents` 枚举**：末尾添加 `SpeedFeedback`。
- **`BmsSkinTransformer.GetDrawableComponent`**：添加 `case BmsSkinComponentLookup { Component: BmsSkinComponents.SpeedFeedback }:`，采用接口验证模式：
  ```csharp
  Drawable? skinnedComponent = base.GetDrawableComponent(lookup);
  return skinnedComponent is IBmsSpeedFeedbackDisplay ? skinnedComponent : new DefaultBmsSpeedFeedbackDisplay();
  ```

##### C1.4 接口与默认实现

- **新文件** `osu.Game.Rulesets.Bms/UI/BmsSpeedFeedbackDisplay.cs`：
  ```csharp
  public interface IBmsSpeedFeedbackDisplay { }

  public partial class DefaultBmsSpeedFeedbackDisplay : CompositeDrawable, IBmsSpeedFeedbackDisplay
  {
      // 通过 [Resolved] 获取 DrawableBmsRuleset
      // 订阅 SpeedMetrics 与 ActiveAdjustmentTarget
      // 渲染：
      //   主行：GN {value}（绿色大号文本，使用 BmsDefaultHudPalette 色）
      //   副行：{VisibleLaneTime:0}ms（灰色小号文本）
      //   目标行：HS {ScrollSpeed:N1}  ▶ {Target}（小号文本）
      //   异常态：当 VisibleLaneUnits ≤ 0 时主行改为 "GN ---"（红色警告）
  }
  ```
- **布局规格**：
  ```
  ┌─────────────────┐
  │  GN 310         │  ← 主指标，绿色
  │  517ms          │  ← 可见毫秒，灰色
  │  HS 8.0  ▶ SUD  │  ← 速度 + 当前目标
  └─────────────────┘
  ```
  - 组件锚点默认 `TopRight`，位于 HUD 右上区域。
  - 尺寸紧凑，不遮挡主 gameplay 区。
  - 当 `VisibleLaneUnits <= 0`：主行文本变为 `GN ---`，颜色改为 `BmsDefaultHudPalette` 的警告色。
  - 当没有任何 adjustment target（Sudden/Hidden/Lift 全未启用）时：目标行仅显示 `HS {value}`，不显示 `▶`。

##### C1.5 HUD 布局集成

- **已采用方案**：保留旧 `IBmsHudLayoutDisplay` 签名，新增可选扩展接口 `IBmsHudLayoutDisplayWithGameplayFeedback`。
- `BmsSkinTransformer` 继续统一组装 `MainHUDComponents`，并 resolve `SpeedFeedback`。
- 新 HUD layout 若实现扩展接口，则可直接接收 `gameplayFeedback` 组件并自行摆位。
- 旧 HUD layout 若只实现原接口，则 transformer 自动包一层 overlay 容器，把 HUD layout 与 speed feedback 同时返回。
- 默认布局位置收口在 `DefaultBmsHudLayoutDisplay.ApplyGameplayFeedbackDefaults()`，当前锚点为 `TopCentre`。

##### C1.6 Toast 语义退位

- **不做代码改动**。`BmsSpeedMetricsToast` 继续在用户执行调速 / 切换目标时弹出。
- **语义变更**：toast 不再是 GN 的唯一权威来源，仅作为"操作确认"的瞬时强调层。
- **文档**：在本文件与 `TECHNICAL_CONSTRAINTS.md` 中明确记录这一主次关系。

##### C1.7 测试清单

| 测试 | 类型 | 验证目标 |
| --- | --- | --- |
| `BmsScrollSpeedMetrics` IEquatable 正确性 | 单元测试 | 相同参数 `==` 返回 true，不同参数返回 false |
| `DrawableBmsRuleset.SpeedMetrics` 响应性 | 集成测试 | 改变 ScrollSpeed / CoverPercent 后 Bindable 收到新值 |
| `DefaultBmsSpeedFeedbackDisplay` 渲染 | 视觉测试 | GN / ms / target 文本正确显示，异常态正确触发 |
| `BmsSkinTransformer` SpeedFeedback fallback | 皮肤测试 | 外部皮肤返回 null → fallback 到 `DefaultBmsSpeedFeedbackDisplay` |
| `BmsSkinTransformer` SpeedFeedback 接口验证 | 皮肤测试 | 外部皮肤返回非 `IBmsSpeedFeedbackDisplay` → 被拒绝，fallback |
| Release build 通过 | 门禁 | `dotnet build osu.Desktop -p:Configuration=Release` exit 0 |

#### C1 文件变动清单

| 操作 | 文件 | 说明 |
| --- | --- | --- |
| 新建 | `osu.Game.Rulesets.Bms/UI/BmsGameplayAdjustmentTarget.cs` | 公共枚举 |
| 新建 | `osu.Game.Rulesets.Bms/UI/BmsSpeedFeedbackDisplay.cs` | 接口 + 默认实现 |
| 新建 | `osu.Game.Rulesets.Bms.Tests/TestSceneBmsSpeedFeedbackDisplay.cs` | 视觉测试场景 |
| 修改 | `osu.Game.Rulesets.Bms/UI/BmsScrollSpeedMetrics.cs` | 补 `IEquatable<>` |
| 修改 | `osu.Game.Rulesets.Bms/UI/DrawableBmsRuleset.cs` | 提取枚举引用、添加响应式 Bindable |
| 修改 | `osu.Game.Rulesets.Bms/Skinning/BmsSkinComponentLookup.cs` | 枚举添加 `SpeedFeedback` |
| 修改 | `osu.Game.Rulesets.Bms/Skinning/BmsSkinTransformer.cs` | 添加 SpeedFeedback 分发 case |
| 修改 | `osu.Game.Rulesets.Bms/UI/BmsHudLayoutDisplay.cs` | 接口扩展 + 默认布局定位 |
| 修改 | `osu.Game.Rulesets.Bms/UI/BmsDefaultHudPalette.cs` | 可选：添加 GN 配色常量 |
| 修改 | `osu.Game.Rulesets.Bms.Tests/BmsSkinTransformerTest.cs` | 添加 SpeedFeedback fallback 测试 |
| 新建 | `osu.Game.Rulesets.Bms.Tests/TestSceneBmsSpeedFeedbackDisplay.cs` | speed feedback 视觉回归 |
| 更新 | `doc_md/subline/P1-C/*` 四件套 | 同步状态 |
| 更新 | `doc_md/mainline/DEVELOPMENT_STATUS.md` | 反映 C1 实现进度 |
| 更新 | `doc_md/mainline/CHANGELOG.md` | 记录变更 |

### C2：`Sudden / Hidden / Lift` 联动收口

**状态：已完成**

目标：让 lane cover focus、当前 target、geometry-effect 与 HUD feedback 表达统一，避免视觉上各说各话。

建议交付：

1. 当前 target 与 lane cover focus state 必须一一对应。
2. `Lift` 继续保持 geometry control，不与 `Hidden` 混写。
3. 对“仅启用 1 个 target”“无 target 可切换”“当前 target 因 mod 未启用而失效”给出明确显示策略。

> 当前已完成四刀：HUD 已能区分 `NONE`、`{TARGET} ONLY`、多 target cycle 状态，以及按住 `UI_LaneCoverFocus` 时的 `HOLD` 临时覆写文案；lane cover focus、cycle 入口与 HUD 状态已完成代码侧收口，后续主线转入 C3。

### C3：判定语义与训练反馈收口

**状态：进行中**

目标：在 speed feedback 稳定后，把 `FAST/SLOW`、judge display、visual timing-offset、EX pacemaker 与 results feedback 统一到同一反馈家族。

建议交付：

1. `FAST/SLOW` 与 judge display 优先进入同一 feedback container，而不是和 judgement piece 硬耦合。
2. visual timing-offset 与 EX pacemaker 也沿同一状态流接入。
3. BRJ / LR2 的 early/late 非对称窗口、excessive poor 与更完整 long-note release parity 优先纳入同一反馈验证链。
4. results summary / gauge history / `DJ LEVEL` / `EX-SCORE` 继续沿同一训练反馈叙事收口。

> 当前已完成四刀：`DrawableBmsRuleset` 已把最近判定快照为 `LatestJudgementFeedback`，`DefaultBmsSpeedFeedbackDisplay` 已在同一 feedback container 中显示最近判定与 `FAST/SLOW` 文案，并对 `EPOOR` 等无真实 timing 语义的结果省略 timing 后缀；最近判定 feedback 现已具备瞬时 judge display 生命周期，重复同类判定也会刷新显示窗口；同一张 feedback card 现已补上 compact visual timing-offset sparkline，并沿 `RecentJudgementFeedbacks` / `TimingFeedbackVisualRange` 同步消费 recent timing history；fixed AAA EX pacemaker 也已通过 `ExScorePacemakerInfo` 并入同一状态流。剩余工作是继续补更完整 judge display，以及后续可配置 / 更丰富来源的 pacemaker 扩展。

### C4：作者文档与 release gate 收口

**状态：未开始**

建议交付：

1. 在 [../../other/SKINNING.md](../../other/SKINNING.md) 中补齐 `GameplayFeedbackDisplay` 的 authoring 入口、fallback 粒度与状态合同。
2. 在 [../../mainline/OMS_COPILOT.md](../../mainline/OMS_COPILOT.md) 中把绿色数字、速度反馈与训练反馈的术语边界写成硬约束。
3. 在 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md) 与 [../../mainline/CHANGELOG.md](../../mainline/CHANGELOG.md) 中记录实现状态与验证结果。

## 当前优先顺序

1. `C3` 判定语义与训练反馈收口
2. `C4` 作者文档与 release gate 收口