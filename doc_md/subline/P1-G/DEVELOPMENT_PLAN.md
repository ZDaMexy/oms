# P1-G 当前计划：Phase 1.x 人工验收汇总

> 最后更新：2026-09-12（星轨体验否定归回 P1-A；两款皮肤迭代待用户恢复）
> 全局 gate 见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)，当前分项见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，历史结论见 [CHANGELOG.md](CHANGELOG.md)。

## 子线目标

建立一份可追溯的 Phase 1.x 人工 release checklist，统一承接皮肤、真实输入、长条/音频、Song Select、Gimmick/BGA 和发行物中无法由自动测试证明的结果。

P1-G 只汇总，不实现：发现问题必须回到 owning 子线，修复后只重测受影响矩阵格。

## 记录格式

每项至少包含：

1. build/commit 或候选发行物标识。
2. Windows/显示/音频/控制器环境；不记录用户敏感绝对路径。
3. 谱面、皮肤或数据根的可复用脱敏标识。
4. 操作步骤、预期、实际和截图/日志/用户确认。
5. 结论：通过、阻塞、非阻塞偏差或无法判断，以及 owning 子线。

总体体验反馈缺少完整环境时，单独保留用户实际结论和证据缺口并归回所属子线；不得据此代填具体键数、设备或其它矩阵格。已明确否定的作品需要修订，不能仅因缺少分项资料继续称其只待例行签字。

## 执行顺序

### 1. 皮肤

- 保留已通过的静态恢复矩阵，不无理由重跑。
- 星轨动画、美术安排与精细度的总体否定已归回 [P1-A 计划](../P1-A/DEVELOPMENT_PLAN.md)；依用户要求暂缓作品修改，待其恢复迭代后再调整静线与星轨，不用继续催签或扩大检查代替作品修改。
- 后续按[集中视觉清单](../../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)确认已导入 `.osk` 普通短键与 LN head/body/tail 的 V-001～V-004、原 Momentum 的 V-005，以及 C7 两包与第三方矩阵。原 V-001～V-004 仍 0/4、V-005 未签收，静线没有正式观感签收。现有 legacy beatmap direct visual compatibility 不等于新增 beatmap-local public authoring。
- 后续 P1-A 玩家可见变化更新受影响矩阵格，不复用前一组件结论；待签收不作为逐组件串行开工门，只有视觉决定设计或自动证据无法裁决时才暂停取反馈。

### 2. 输入与控制器

- keyboard/Raw/XInput/MouseAxis/DirectInput 主链 smoke。
- analog scratch、跨设备 first-press/final-release、方向换向和 hold survival。
- deadzone/sensitivity/live diagnostics 与真实 IIDX/BMS HID 控制器。

### 3. gameplay、长条与音频

- LN/CN/HCN 的 release、tail、HCN regrab、gauge 和可见状态。
- dense fully-keysounded、layered/long BGM、rapid empty-strike、pause/seek/retry。
- 原生 BMS 与转谱-mania 对照；明确长 one-shot 当前不保证保位 resume。

### 4. Song Select 与桌面导入

- 大库分组、筛选、搜索、展示层级、返回导航与无结果条件。
- shared visual/ruleset 切换不串线。
- P1-I 当前仍为三行双端构成原型；单轨三段目标须先实现，不能由人工体验提前豁免产品合同。
- 桌面拖放导入、首次启动/重扫后的可见结果和基本 UI smoke。

### 5. Gimmick 与 BGA

- 代表图序列、POOR、seek、老视频转码和重进缓存。
- DEAD SOUL 等代表 Gimmick 谱的 freeze/snap/Auto/Off。
- 5K/7K/9K/14K 当前布局：C3唯一layout/viewport已落；P1-L内容播放与后续C6/C7新增consumer分别按实际改动复核，不把viewport闭合等同于单内容源已闭合。

### 6. 候选发行物

- fresh extract 冷启动、portable `data/`、custom root 与覆盖更新。
- 保留 `portable.ini`、`storage.ini` 和用户内容；公开说明与真实能力一致。
- 本项由 P1-F 提供候选包与步骤，P1-G 记录最终人工结果。

## 关闭规则

- 单项只有自动 gate 与人工证据均满足 owning 子线要求时才关闭。
- 阻塞缺陷归线后，P1-G 记录链接和待重测格，不在本线维护修复计划。
- 所有 release 必需格通过或有经产品确认的非阻塞归因后，才向 mainline 回写一句总结果。
- 不把未实现功能、Phase 2/3 能力或“顺便体验”加入 Phase 1.x 必过矩阵。
