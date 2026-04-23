# P1-A：产品面、release gate 与皮肤边界

本子线是 Phase 1.x 下的 `P1-A` 正式入口，主承接 OMS 产品面、release gate、BMS 皮肤边界、HUD 宿主合同，以及与 `P1-C` 共享的反馈组件扩展边界。

## 归线关系

- 主归属 `P1-A`：冻结 BMS HUD / skin boundary、明确 lookup 边界、约束 HUD 宿主扩展方式、守住 release gate。
- 协作子线 `P1-C`：绿色数字、速度反馈与训练反馈闭环依赖这里定义的边界与宿主合同；对应入口见 [../P1-C/README.md](../P1-C/README.md)。
- 参考输入：以 [../../other/IIDX_REFERENCE_AUDIT.md](../../other/IIDX_REFERENCE_AUDIT.md) 做方向校准，但当前不等价于正式进入完整 `FHS` 主线。

## 专题文档

- [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)
- [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)
- [CHANGELOG.md](CHANGELOG.md)
- [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)

## 当前结论

- BMS 皮肤 transformer 边界已经足够封闭，可以安全继续往 BMS-owned HUD / feedback / operator overlay 合同推进。
- 首次启动向导、`Run setup wizard` 与 Song Select 无谱面引导这类共享 onboarding / settings-entry surface 默认归 `P1-A`；若页面只是复用外部 / 内部谱库或按键绑定面板，则 `P1-H` / `P1-B` 仅作从属，不为暴露面另开子线。
- 当前 tri-mode settings、mode-aware HUD feedback 与 pre-start hold 调速窗口仍落在这条既有 `P1-A / P1-C` 交叉线上，不需要新开主线；真正后置的是 full Floating parity。
- 当前 `IBmsHudLayoutDisplay` 只接受 wrapped HUD / gauge / combo，后续必须继续通过向后兼容的方式扩展，不能直接把现有接口打断。
- 所有涉及皮肤边界、HUD 宿主、pre-start operator surface 与 release gate 的改动，都必须同步更新本目录四件套，并在影响全局时反向同步 `../../mainline/`。