# Skin V1 集中视觉验收清单

本清单汇总 Skin V1 中自动 gate 无法替代的用户视觉结论。它不是逐组件开发开工门：切片通过自动、合同、安全与回退验证后可以继续按依赖推进；未获用户签收的项目只能称“实现／自动 gate 通过，视觉待验收”，不得称产品交付、`SV1` 阶段完成或 release gate 通过。只有视觉选择确实决定后续设计或自动证据无法裁决异常时，才把该项升级为开发 blocker。

## 状态定义

- **自动 gate 通过，视觉待验收**：实现可继续作为后续依赖，但不能进入交付完成数。
- **视觉已签收**：用户基于列明的 build、输入和矩阵确认预期表现。
- **视觉阻塞**：反馈会改变后续实现语义，或出现自动证据无法裁决的异常；必须先闭合再继续受影响切片。

## 当前汇总

| ID | 能力 | 自动状态 | 用户状态 | 是否阻塞后续开发 |
| --- | --- | --- | --- | --- |
| `V-001` | managed `.osk` BMS 普通短键编号帧动画、选择切换、selected 坏包回落 | 产品自动 gate 已通过；隔离自动可视预检工具已建立 | 待统一反馈 | 否；但阻塞 Skin V1/release 完成声明 |
| `V-002` | managed `.osk` BMS 长条头静态图/编号帧动画、scratch/S2、选择切换与坏 head 回落 | 产品自动 gate 已通过；集中验收输入待统一打包 | 待统一反馈 | 否；但阻塞 Skin V1/release 完成声明 |

## V-001：BMS 普通短键编号帧动画

构建锚点：本条所在提交。详细素材与完整隔离流程见[BMS 普通短键编号帧动画手工门](SKIN_BMS_NOTE_ANIMATION_MANUAL_GATE.md)。

视觉矩阵：

1. **7K good 包**：lane 1 普通短键尺寸保持固定，深蓝音符内的白/品红亮带连续横向循环；其它 lane 保持外层静态 fallback。
2. **选择切换**：good → OMS 内建 → good 后，新进入 gameplay 的普通短键立即使用当前选择，不跨 package 拼接旧帧。
3. **selected broken 包**：缺少必需 frame 0 时，lane 1 仍显示可玩默认视觉，不消失、不残留 good 包动画。
4. **边界**：本项不验 LN/key、mania、G1、layout、scene/script、整包原子热重载、canonical `oms-simple`，也不证明真实 BMS beatmap-local 作者格式。

反馈记录：

- Windows、显示分辨率/DPI、build/commit：待填写。
- `V-001` 结论：待通过／失败。
- 若失败：注明矩阵项、实际观感、截图或日志，以及是否会改变后续实现语义。

## V-002：BMS 长条头静态图与编号帧动画

构建锚点：本条所在提交。为避免逐组件打断开发，本项不单独启动桌面门；最终集中验收前会把确定性素材、build 与启动步骤统一打包。

视觉矩阵：

1. **7K 普通 LN**：`NoteImage1H` 静态图能稳定覆盖真实长条头；编号帧版本在长条头固定宿主内连续循环，不改变长条身宽、长度、裁剪或判定位置。
2. **scratch / DP**：7K `NoteImageSH` 与 14K 第二皿 `NoteImageS2H` 均落到正确长条头，不串到普通键、P1 scratch 或另一 deck。
3. **选择切换**：A 包 2 帧 → B 包 3 帧时，准备期间保留旧 head，完成后整件替换；不混用 A/B 帧，也不残留上一包视觉。
4. **selected 坏 head**：缺失、空值、损坏、断帧或越界 head 仍显示可读默认头，不消失、不从低层仅同名纹理拼件；同包有效 ordinary note 继续使用包内素材。
5. **边界**：长条 body/tail、Idle/Holding/Broken、LN/CN/HCN 判定与保持语义、尺寸/裁剪/layout 均应与切片前一致。本项不验作者 `Suppress`、mania、G1、scene/script、整包原子重载或真实 BMS beatmap-local 作者格式。

反馈记录：

- Windows、显示分辨率/DPI、build/commit：待填写。
- `V-002` 结论：待通过／失败。
- 若失败：注明矩阵项、实际观感、截图或日志，以及是否会改变后续实现语义。

后续每个新增可见切片在自动 gate 通过后追加新 ID；P1-G 最终 release checklist 只汇总这里已经签收的结论，不用自动测试替代视觉反馈。
