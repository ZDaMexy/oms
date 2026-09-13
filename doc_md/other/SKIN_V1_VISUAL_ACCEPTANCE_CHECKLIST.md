# Skin V1 集中视觉验收清单

本清单汇总 Skin V1 中自动 gate 无法替代的用户视觉结论。它不是逐组件开发开工门：切片通过自动、合同、安全与回退验证后可以继续按依赖推进；未获用户签收的项目只能称“实现／自动 gate 通过，视觉待验收”，不得称产品交付、`SV1` 阶段完成或 release gate 通过。只有视觉选择确实决定后续设计或自动证据无法裁决异常时，才把该项升级为开发 blocker。

构建锚点只在实际验收时填写当次 commit/build；实现时的自动证据与历史数字统一查 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md)，不复制到本清单的当前汇总。

## 状态定义

- **自动 gate 通过，视觉待验收**：实现可继续作为后续依赖，但不能进入交付完成数。
- **视觉已签收**：用户基于列明的 build、输入和矩阵确认预期表现。
- **视觉阻塞**：反馈会改变后续实现语义，或出现自动证据无法裁决的异常；必须先闭合再继续受影响切片。

## 当前汇总

| ID | 能力 | 自动状态 | 用户状态 | 是否阻塞后续开发 |
| --- | --- | --- | --- | --- |
| `V-001` | 已导入 `.osk` 的 BMS 普通短键编号帧动画、选择切换、selected 坏包回落 | 产品自动 gate 已通过；隔离自动可视预检工具已建立 | 待统一反馈 | 否；但阻塞 Skin V1/release 完成声明 |
| `V-002` | 已导入 `.osk` 的 BMS 长条头静态图/编号帧动画、scratch/S2、选择切换与坏 head 回落 | 产品自动 gate 已通过；集中验收输入已随 C7 体验包提供 | 待统一反馈 | 否；但阻塞 Skin V1/release 完成声明 |
| `V-003` | 已导入 `.osk` 的 BMS 长条尾静态图/编号帧动画、透明回落与下层完整组件接管 | 产品自动 gate 已通过；集中验收输入已随 C7 体验包提供 | 待统一反馈 | 否；但阻塞 Skin V1/release 完成声明 |
| `V-004` | 已导入 `.osk` 的 BMS 长条身静态图/编号帧动画、同 revision 宽度、状态与隔离回落 | 产品自动 gate 已通过 | 待统一反馈 | 否；但阻塞 Skin V1/release 完成声明 |
| `V-005` | C6 可选脚本、授权 UI、双规则集 Momentum 候选与整包 Reload | 双 host 产品自动验证已通过；完整证据见[C6 报告](SKIN_SYSTEM_C6_VALIDATION_20260909.md) | 待统一反馈 | 否；但阻塞 Skin V1/release 完成声明 |

原 `V-001`～`V-004` 签收仍为 **0/4**；新增 `V-005` 同样未签收，不替代任何原有项目。

## C7 集中体验

静线是唯一内置、默认与必要部件保底，complex 已退役，旧源与 C6 固定输入只保留历史身份。静线、普通第三方/Aurora 对照、原 V-001/V-005 输入、支持键数观察谱及制作套件，由[集中验收入口](../../skin-c7-acceptance/README.md)提供；逐项记录使用[同一份观察表](../../skin-c7-acceptance/CHECKLIST.csv)。既有体验目录保留生成时快照，当前产品状态以 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)为准。

2026-09-12 用户对星轨动画、美术安排及精细度的总体否定作为历史反馈保留，随后已明确放弃星轨。当前不再要求修复或签收 complex，也不清除普通用户导入的同名作品。原 V-005 的 `oms-complex-c6.osk` 是固定脚本验收输入，不恢复星轨产品要求。

2026-09-13 用户要求静线暂时打磨至此，转入实际项目、文档与记忆核对。该指示不是逐项或整体视觉签收；参考图与合成谱实机捕获、布局信息区及轨宽验证见[凭据索引](references/simple-1p-20260912/README.md)。V-001～V-004 仍为 0/4，V-005 未签收，Skin V1 和 release 未完成。

后续人工验收使用当次完整候选，在隔离副本中游玩静线的 BMS 和 mania，按清单核对第三方导入、导出、更新、缺件补齐、安装修复、外部目录只读与可选效果允许/拒绝/撤销。作者按[极光习作](../../skin-authoring/docs/WORKSHOP.md)完成制作流程；屏幕比例、缩放、低端设备、长时间体验和整体美术填写实际证据。本次收尾不自行启动这些后续任务，也不代填通过。

当前版本的必要部件保底来自 `oms-simple.osk`。下文 V-003 中“透明程序化尾部”仅描述迁移期对照；C7 的末端尾部使用简洁包公开声明的完整素材，检查重点仍是坏用户素材能完整回落、不混用其它层的裸文件、不改变长条尺寸和判定。此变化不视为 V-003 已签收。

## V-001：BMS 普通短键编号帧动画

构建锚点：验收时填写实际 commit/build。详细素材与完整隔离流程见[BMS 普通短键编号帧动画手工门](SKIN_BMS_NOTE_ANIMATION_MANUAL_GATE.md)。

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

构建锚点：验收时填写实际 commit/build。本项不单独启动桌面门；确定性素材与启动步骤已由上方 C7 集中体验包提供，按实际使用版本记录，不因打包完成代签。

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

## V-003：BMS 长条尾静态图与编号帧动画

构建锚点：验收时填写实际 commit/build。本项不单独启动桌面门；确定性素材与启动步骤已由上方 C7 集中体验包提供，按实际使用版本记录，不因打包完成代签。

视觉矩阵：

1. **7K 普通 LN**：`NoteImage1T` 静态图能只覆盖真实长条尾 cap；编号帧版本在固定 22.5px tail host 内连续循环，不改变 head/body、长条长度、裁剪或 release 判定位置。
2. **scratch / DP**：7K `NoteImageST` 与 14K 第二皿 `NoteImageS2T` 均落到正确长条尾，不串到 head、普通键、P1 scratch 或另一 deck。
3. **选择切换**：A 包 2 帧 → B 包 3 帧时，准备期间保留旧 tail，完成后整件替换；不混用 A/B 帧，也不残留上一包视觉。
4. **selected 坏 tail**：缺失、空值、损坏、断帧、越界或超预算时回到透明 protected tail，不显示低层仅同名裸文件；低层若拥有自己的完整 tail 声明和素材，可按 `Inherit` 正常接管。透明链底只是当前迁移 fallback，不能反馈为“作者 Suppress 已生效”。
5. **隔离与边界**：同包有效 ordinary note/head 保持包内视觉，body 与 Idle/Holding/Broken、LN/CN/HCN、尺寸/滚动/layout 均与切片前一致。本项不验作者 `Suppress`、mania、G1、scene/script、整包原子重载或真实 BMS beatmap-local 作者格式。

反馈记录：

- Windows、显示分辨率/DPI、build/commit：待填写。
- `V-003` 结论：待通过／失败。
- 若失败：注明矩阵项、实际观感、截图或日志，以及是否会改变后续实现语义。

## V-004：BMS 长条身素材、宽度与保持状态

构建锚点：验收时填写实际 commit/build。本项不单独启动桌面门；确定性素材与启动步骤已由上方 C7 集中体验包提供，按实际使用版本记录，不因打包完成代签。

视觉矩阵：

1. **静态与 60 FPS 编号帧**：7K 普通键 `NoteImage1L`、scratch `NoteImageSL` 与 14K 第二皿 `NoteImageS2L` 分别落到正确长条身；静态素材稳定，`name-0`、`name-1`…连续编号帧以固定 60 FPS 循环，不串到其它 lane、head 或 tail。
2. **宽度安全域**：合法 `LongNoteBodyWidth` 按声明显示；字段缺失、非 finite、`<= 0` 或 `> 1` 时只把 body 宽度回退到 `0.5775`，同包有效 body 素材仍保留，不污染其它字段或组件。
3. **同 revision 切换**：A 包使用 2 帧窄 body，B 包使用 3 帧宽 body；准备期间保留旧 body，完成后素材帧组与宽度作为同一个 package revision 一起切换，不出现 A 素材+B 宽度或反向拼接。这是逐组件 A→B 验收，不代表整包原子热重载已开放。
4. **selected 坏 body 与下层隔离**：selected 包 body 缺失、空值、损坏、断帧、越界或超预算时仍有可读 critical rescue；不得从下层仅同名裸纹理或裸宽度拼件。下层若拥有自己的完整 body 声明、素材与宽度，可按 `Inherit` 整体接管。
5. **保持状态**：在真实长条上检查 Idle/Holding 为 alpha `0.8`、Broken 为灰暗 alpha `0.32`，状态变化约用 `80ms` 过渡；HCN release 后 Broken、regrab 后恢复 Holding。状态变化和异步 body 到达不得闪回错误状态。
6. **边界不变**：head/tail 外观、长条长度、拉伸/裁剪、判定位置以及 LN/CN/HCN 判定与保持规则均与切片前一致。本项不验作者 `Suppress`、mania、G1、screen-space/layout、scene/script、整包原子重载或真实 BMS beatmap-local 作者格式。

反馈记录：

- Windows、显示分辨率/DPI、build/commit：待填写。
- `V-004` 结论：待通过／失败。
- 若失败：注明矩阵项、实际观感、截图或日志，以及是否会改变后续实现语义。

## V-005：C6 可选脚本与双规则集 Momentum 候选

构建锚点：验收时填写实际 commit/build。[作者源文件与可复现构建步骤](skin-c6-candidate/README.md)提供 `oms-complex-c6.osk`；它是普通可导入的原 C6 候选，已随集中体验包保留，不能用 C7 星轨替代其原验收输入。自动测试使用真实 source、SkinManager、引擎 producer 与 scene host，但 headless runner 未加载字体，不证明字形、GPU 观感或低端实机性能。

视觉与操作矩阵：

1. **未授权与拒绝**：普通导入后选择 `OMS Momentum C6 [oms-complex-c6]`，分别进入 BMS/mania，note/key/judgement 与长条必要信息持续可读，Momentum 保持静态；Settings 列出三个 required 与一个 optional request，拒绝不得隐式激活脚本。
2. **授权与组合效果**：授权三个 required，先拒绝 random，再授权 random；两规则集实际命中后，右侧最近八次判定间隔与 gauge 组合出的脉冲、旋转、能量条可见且不遮挡玩法。检查 Momentum 文字、DPI/宽高比、字体可读性及必要视觉回退。
3. **撤销与恢复**：运行时及暂停时撤销 scene 写入，旧脚本效果均撤去，继续可玩；快速撤销再授权不复用旧历史。暂停停止效果时间，retry/seek 重建历史；profiler/诊断可查看，不出现持续刷屏或卡住。
4. **三源与最终整包**：在测试数据根分别使用 ordinary、managed、registered external；退出 gameplay 后由 Settings 唯一 Reload 生效，live/preview 内操作仍拒绝。相同内容重启保留授权，改内容后重授权；损坏 scene/script/素材保留旧包，修复后可恢复。选择切换和适用的 rename/delete/unregister 不残留旧效果，external 原文件保持不变。
5. **低端与长时**：记录低端 Windows 设备、谱面/keymode、帧率和 profiler，观察高密度事件、长谱、反复暂停/重试及包切换。自动 instruction/heap/node/resource 限额已另行验证；本项确认真实 GPU、字体、输入设备和长期体验。

反馈记录：

- Windows、设备、显示分辨率/DPI、谱面/keymode、build/commit：待填写。
- `V-005` 结论：待通过／失败。
- 若失败：注明矩阵项、实际观感、截图或日志，以及是否会改变后续实现语义。

后续每个新增可见切片在自动 gate 通过后追加新 ID；P1-G 最终 release checklist 只汇总这里已经签收的结论，不用自动测试替代视觉反馈。
