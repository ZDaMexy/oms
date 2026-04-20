# P1-C：判定语义、绿色数字与反馈闭环

本子线是 Phase 1.x 下的 `P1-C` 正式入口，主承接判定语义补强、绿色数字与速度反馈、results feedback，以及后续 `FAST/SLOW`、judge display、visual timing-offset、EX pacemaker 的统一反馈家族。

## 归线关系

- 主归属 `P1-C`：收口 BRJ / LR2 parity、绿色数字 HUD、速度反馈与训练反馈闭环。
- 协作子线 `P1-A`：`P1-C` 的 feedback family 依赖 `P1-A` 冻结的 BMS HUD 宿主与皮肤边界；对应入口见 [../P1-A/README.md](../P1-A/README.md)。
- 参考输入：以 [../../other/IIDX_REFERENCE_AUDIT.md](../../other/IIDX_REFERENCE_AUDIT.md) 做方向校准，但当前不等价于正式进入完整 `FHS` 主线。

## 子线文档

- [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)
- [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)
- [CHANGELOG.md](CHANGELOG.md)
- [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)

## 当前结论

- 当前 `GN / WN` 已在 runtime metrics、常驻 HUD、toast 与 pre-start overlay 中存在，并共享同一组 mode-aware 运行时语义。
- tri-mode Hi-Speed settings、pre-start hold 调速窗口与 `Sudden / Hidden / Lift` 联动仍属于这条既有 `P1-A / P1-C` 交叉线，不需要新开主线；真正后置的是 full Floating parity。
- `P1-C` 不能绕过 `P1-A` 直接扩写旧版 HUD 接口，也不能把 speed feedback 临时塞进 `GaugeBar`、`ComboCounter` 或 wrapped HUD 子节点。
- 所有涉及判定语义、反馈闭环、pre-start operator surface 与绿色数字表达的改动，都必须同步更新本目录四件套，并在影响全局时反向同步 `../../mainline/`。