# P1-A 开发进度：产品面、release gate 与皮肤边界

> 最后更新：2026-05-26
> 主线全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)。本文件只记录 `P1-A` 的真实进展；`P1-C` 的反馈闭环进展见 [../P1-C/DEVELOPMENT_STATUS.md](../P1-C/DEVELOPMENT_STATUS.md)。

## 当前阶段

- **阶段定位**：子线建档完成，HUD 宿主与边界冻结已基本稳定，当前进入“tri-mode operator surface 挂接后的稳态化 + `阻止谱面开始/ingame start` 运行时宿主语义收口 + BMS mod surface 记忆合同收口 + onboarding/settings-entry surface 收口”阶段。
- **代码状态**：向后兼容的 HUD 宿主扩展已落地：`IBmsHudLayoutDisplayWithGameplayFeedback`、legacy HUD overlay wrapper 与 `DefaultBmsHudLayoutDisplay` 默认摆位已接通；`BmsGameplayFeedbackLayout` 已把默认 gameplay feedback 摆位与 judgement 基线收口到同一条显式位置合同；当前 `TimingFeedbackVisualRange`、compact judgement counts 与 live EX progress 已并入 `BmsGameplayFeedbackState` aggregate snapshot，`DefaultBmsSpeedFeedbackDisplay` 现已收口为消费单个 BMS-owned state contract 加一条 recent-history 列表，并可基于既有 judgement counts 在 display 侧派生带紧凑原因标签的 live `PERFECT / FC / FC LOST` 状态线与 EX 原始分子/分母。当前 BMS 速度产品面已扩到 tri-mode：settings 提供 `Normal / Floating / Classic Hi-Speed` 下拉和当前模式 slider，`BmsSoloPlayer` / `BmsPreStartHiSpeedOverlay` 已把 `UI_PreStartHold` 收口为“前 5 秒阻止开始 + 全程调速修饰键”这一正式 gameplay operator surface，paused pre-start 状态下仍可继续使用 `Sudden / Hidden / Lift` 调整链；右侧 `READY HOLD` overlay 只保留给前 5 秒阻止开谱窗口，正式 gameplay 开始后按住同一键仍会继续调速，并持续刷新居中的 `BMS speed` toast。对应覆盖现已分成 owner-level `TestSceneBmsPreStartHiSpeedOverlay`、real-player `TestSceneBmsSoloPlayerPreStart` 与输入桥 `OmsInputRouterTest` 三层：前者锁住 tri-mode 文案 formatting 与“仅在 overlay 可见时响应 odd/even lane 调速”的组件合同，后两者分别锁住 **10/10** 的真实宿主链 delayed-start / hold modifier / mode-value binding，以及 **9/9** 的 hold 期间 lane-action gameplay 转发抑制。`UI_PreStartHold`（阻止开谱/调速修饰键）与 `UI_LaneCoverFocus`（单击循环持久目标）已拆为独立动作。desktop shared Settings -> 输入 现已通过 `OsuGameDesktop.CreateSettingsSubsectionFor()` 安全隐藏 upstream 的数位板 / 触屏点击 / 鼠标 subsection，避免把非 OMS 通用设置表面继续暴露给最终桌面产品面；该裁剪不触碰 mouse/touch/tablet runtime config 与 handler 链，并明确保留在 desktop 宿主层。与此同时，BMS mod 选项表面也已收口为 ruleset-local memory surface：`OsuGameBase` 现会在 BMS 切入点恢复 selected mods 与 remembered settings，`ModSelectOverlay` 对标记 mod 不再在 deselect 时 reset，而 `Sudden` / `Hidden` / `Lift` 也已把 `记忆游戏内变动` 作为 mod-local 开关暴露给用户。考虑 `RulesetConfigCache` 的 startup 顺序后，宿主现会在 cache ready 后 replay 当前 ruleset，因此完全冷启动第一次进入 BMS 也会恢复 selected mods / remembered settings，且不会再冒出误报的 ruleset issue 通知。与此同时，`BMS -> mania` 公开表面的第三刀也已接上 persisted converted-star authority：modless converted mania 星数现已写入 BMS metadata payload，并由 `BeatmapDifficultyCache`、`BackgroundDataStoreProcessor` 与 Song Select spread display 统一按 current-ruleset 视角读取，因此 carousel selector 与 spread dots 都不再继续直接吃 source BMS raw star。
- **首次启动向导状态**：共享层 first-run wizard 现已收口为六步 OMS flow：欢迎、UI 缩放、获取谱面、导入、难度表设置、按键绑定。获取谱面页改为 mania / BMS 外部站点导流与内部谱库补扫提示；导入页直接复用 `ExternalLibrarySettings`；难度表页通过反射调用 `BmsDifficultyTableManager` 导入 zris 镜像预设；最后一步复用全局、mania 与 BMS keybinding subsection。欢迎页、获取谱面页与导入页的可见文案已切到 OMS-owned localisation namespace + `.resx`，确保简中不再继续显示上游翻译。该专题主归属继续是 `P1-A`；导入页对 `P1-H`、按键绑定页对 `P1-B` 都只形成从属暴露面。
- **BMS -> mania 转谱公开表面状态**：该表面现已从“首轮 visibility gate 已落地”推进到“visibility gate、persisted converted-star display 与 spread display 已落地、显式 wording 仍未收口”状态。当前 `AllowGameplayWithRuleset()` / `RequiresRulesetSwitch()` 已把 `BMS source -> mania target` 接回真实可玩性判断，modless converted mania 星数也已改为持久化到 BMS metadata payload，并由 `BeatmapDifficultyCache`、后台补算与 current-ruleset spread display 统一读取；因此 Song Select 的星数筛选、难度排序、按星数分组与 spread dots 都不再继续直接吃 source BMS raw star。剩余主要是按钮 wording、显式入口文案与更宽 presentation/manual proof。
- **文档状态**：`P1-A` 的计划、状态、变动日志、技术约束已与当前宿主合同实现同步，并已把 2026-04-28 的 pre-start overlay / real-player coverage 与 mainline 文档口径一并收平。

## 已确认事实

- BMS 皮肤边界已足够封闭，可继续向 BMS-owned feedback component 扩展。
- 当前 `GN` / `WN` 来自 `BmsScrollSpeedMetrics`，其输入现已覆盖 `Normal / Floating / Classic Hi-Speed`、`ScrollLengthRatio`、`Sudden`、`Hidden`、`Lift`。
- 当前 tri-mode Hi-Speed surface 已完成首轮产品接线：`Normal` 走默认 settings surface，`Floating` 提供 initial-BPM anchored runtime surface，`Classic` 继续锁定 `HS 10 + WN 350 => GN 300`；这仍不等价于完整 FHS。
- settings 页现显示 mode + value，并在数值后括号显示不启用 `Sudden / Hidden / Lift` 时的基础下落时间（ms）；`GreenNumber` 仍不进入 settings，而继续留在 gameplay feedback 链。
- `osu!mania` settings 页的 `滚动速度` hover 提示当前也已明确为参考值说明：括号毫秒只代表标准车道几何下的参考下落时间，不作为跨皮肤或跨 ruleset 的严格体感合同；更换 mania 皮肤后应重新校准，且 mania / BMS 的下落时间不可交叉参考。
- Settings → 常规 → 安装位置 当前已把入口明确为 `更改数据目录位置`；选择空目录时会直接迁入当前数据内容，非空非数据目录会改用其下 `oms/` 子目录，若所选目录本身已是可用数据目录则只在重启后切换。该产品面只切换/迁移运行时数据根，不移动程序文件。
- `键音通道数` 当前也已作为 BMS settings surface 的独立滑条公开 shared `BmsKeysoundStore` ceiling，范围 `1..256`，默认 `32`；hover 提示会直接说明低值截音风险、高值负载取舍，以及缺音时优先上调到 `48/64` 的调参路径。
- BMS mod 选中状态与非默认配置现按 ruleset-local JSON snapshot 记忆，仅作用于 BMS；切到 mania 再切回或完全重启后仍恢复。
- 启动早期若 `RulesetConfigCache` 尚未 ready，`OsuGameBase` 现在会延后 replay 当前 ruleset 到 cache ready 后再做 BMS restore；这条 host-boundary 合同同时修复了冷启动首轮漏恢复与误报 ruleset failure。
- 实现 `IPreserveSettingsWhenDisabled` 的 configurable BMS mod 在 Song Select 中停用 / 启用不会丢配置；停用不再被视为“恢复默认值”。
- 首次启动向导当前已固定为六步 OMS flow：欢迎、UI 缩放、获取谱面、导入、难度表设置、按键绑定；这属于共享产品表面收口，而不是新的输入或存储主线。
- desktop 通用 Settings -> 输入 当前已主动隐藏 upstream 的数位板 / 触屏点击 / 鼠标 subsection；这是共享 settings-entry surface 的产品裁剪，不等于删除底层 input contract。
- 欢迎页、获取谱面页与导入页的可见文案现已切到 OMS-owned localisation namespace + `.resx`；若仍指向上游 localisation namespace，简中会继续读取上游翻译而不是代码 fallback。
- 共享层难度表设置页当前通过反射调用 `BmsDifficultyTableManager`，继续保持 `osu.Game` 与 `osu.Game.Rulesets.Bms` 的项目边界。
- `Playfield Scale` 已从 settings / runtime config 移除并固定为 `1.0`；原来的 `Playfield Horizontal Offset` 也已退出，改为四态 `Playfield Style`（`1P（居左）` / `2P（居右）` / `居中（左皿）` / `居中（右皿）`）这一 single-play playfield surface：当前只作用于 5K / 7K 的 playfield 停靠与 scratch 视觉侧别，其中 `1P / 2P` 为“侧停靠但保留固定屏侧间距”；不改变尺寸 / 可见时间语义，也不承担完整 `1P/2P flip` 的绑定与 side-aware skin 合同。
- `UI_PreStartHold` 现已承担“前 5 秒阻止开始 + 全程调速修饰键”这一统一 operator contract；`UI_LaneCoverFocus` 保持为 click-to-cycle 持久 target，且 HUD / skin boundary 与 legacy fallback 合同保持未破坏。
- 若后续加入 pre-start 视觉流速预览，宿主应落在第一非 scratch 普通轨的 playfield / lane visual surface，并继续复用 BMS note lookup / fallback，而不是 HUD / toast。
- `Sudden` / `Hidden` / `Lift` 现都暴露 `记忆游戏内变动` 开关；默认开启时局内滚轮调整会延续到回场后的 BMS mod 配置，关闭时保持 current-play-only。
- 当前 `IBmsHudLayoutDisplay` 原签名仍被保留；额外 gameplay feedback 通过 `IBmsHudLayoutDisplayWithGameplayFeedback` 与 legacy overlay wrapper 向后兼容接入。
- `BmsSkinTransformer` 已在 `MainHUDComponents` 路径统一组装 gauge / combo / speed feedback；不实现新接口的旧 HUD layout 仍能正常工作。
- `BmsGameplayFeedbackLayout` 已收口默认 gameplay feedback 摆位与 judgement 基线；后续若继续联动 judge display / feedback，必须扩这条合同，而不是再复制位置常量。
- `DrawableBmsRuleset` 现已额外暴露 `GameplayFeedbackState` aggregate bindable；当前 display scalar state 已收口到单个 snapshot，recent timing history 仍保持独立状态流，而 `TimingFeedbackVisualRange`、compact judgement counts 与 live EX progress 已并入 aggregate snapshot，避免继续保留无必要的额外 scalar 直连。
- `DefaultBmsSpeedFeedbackDisplay` 现已开始直接利用既有 `BmsJudgementCounts` 与其派生 helper 显示带紧凑原因标签的 live `PERFECT / FC / FC LOST` 状态线，证明一部分 display-only 训练语义不必继续扩大 `GameplayFeedbackState`。
- 当前 IIDX 参考文档仍明确要求：不要把现有 OMS speed feedback 对外包装成完整 `FHS`。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 子线归线到 `P1-A` | 已完成 | 主线文档已挂接 |
| 皮肤边界与 HUD 宿主审计 | 已完成 | 已明确当前模型与风险点 |
| 子线级计划 / 状态 / 约束文档 | 已完成 | 文档位于当前目录 |
| 首次启动向导与设置导流 | 已完成 | 主归属 `P1-A`，`P1-H` / `P1-B` 仅从属暴露 |
| `GameplayFeedbackDisplay` 合同设计 | 进行中 | HUD 宿主兼容扩展、default fallback、shared position contract 与 aggregate scalar state contract 四刀已落地；基于既有 snapshot 的派生 status line 也已验证可继续收口 richer judge display 而无需新字段；剩余 richer judge display 语义继续收口 |
| 常驻 GN HUD | 进行中 | 默认 feedback container 已由 `P1-C` 接通；本子线继续维护宿主与边界合同 |
| `Sudden / Hidden / Lift` HUD 联动 | 进行中 | 宿主与默认摆位已稳定，具体状态语义由 `P1-C` 继续推进 |
| pre-start 视觉流速预览宿主边界 | 已完成首轮实现 | playfield / lane host、第一非 scratch 轨宿主与 BMS note fallback 已接通；运行时 gate 与 pause 行为由 `P1-C` focused tests 锁定 |
| `BMS -> mania` 单向转谱公开表面 | 进行中 | visibility gate、persisted converted-star display 与 spread display 已接通；按钮文案、显式入口与更宽 surface proof 仍待后续收口 |
| `FAST/SLOW` / judge display / pacemaker 统一承载 | 进行中 | 统一 feedback container 已存在，shared judgement/feedback position contract 已落地 |

## 当前风险

- **接口破坏风险**：如果直接改 `IBmsHudLayoutDisplay.SetComponents(...)` 签名，会立刻打断现有自定义 HUD provider。
- **术语冒进风险**：如果把当前常驻 GN 直接写成完整 `FHS`，会与 IIDX 参考约束冲突，也会误导用户对当前模型的预期。
- **边界污染风险**：如果为了赶功能把 speed feedback 偷塞进 `GaugeBar`、`ComboCounter` 或 wrapped HUD 子节点，后续 `FAST/SLOW` / pacemaker 将继续复制同类问题。
- **preview 宿主污染风险**：如果 pre-start 视觉流速预览被塞进 HUD / toast 或误复用 mania lookup，就会把 playfield 视觉 preview 变成错误的宿主边界问题。
- **布局扩散风险**：如果不先冻结 judgement / feedback 的位置合同，后续容易继续用新的硬编码偏移叠层。

## 下一检查点

1. 在现有 `GameplayFeedbackState` 已并入 compact judgement counts 与 live EX progress 的基础上，继续评估后续 richer judge display state 是否进入同一 contract，还是保持与 recent history 分层；当前 live `PERFECT / FC` 资格线与 EX 原始分子/分母文案都已证明一部分 display-only 语义可直接从既有 snapshot 派生。
2. `BMS -> mania` 公开表面当前已不再以 raw star 驱动 Song Select selector 或 spread dots；下一刀应转向 explicit wording 与更宽 presentation/manual proof，而不是回头重做 current-ruleset star surface。
3. 若启动 pre-start 视觉流速预览切片，先补 playfield / lane host、第一非 scratch 轨解析与 note fallback 路径，再把可见性 gate 与“无判定副作用”语义交给 `P1-C` focused validation。
4. 维持 `OmsSkin` 默认路径、legacy HUD wrapper 与 fallback 语义稳定，并把 remaining full Floating parity 缺口明确留在后续路线，不在 `P1-A` 里误写成已完成。

## 历史变动与验证

- 当前仍影响判读的验证结论已在“当前阶段”“进度矩阵”与“下一检查点”中汇总；按日期展开的宿主改动、回归命令与构建记录见 [CHANGELOG.md](CHANGELOG.md)。
