# P1-B 开发计划：输入语义与硬件验收

> 最后更新：2026-04-20
> 主线总规划见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。

## 子线目标

- 收口 analog scratch、cross-device trigger、mixed-source runtime 与真实 HID 硬件验收。
- 保持 keyboard / Raw Input / XInput / MouseAxis / DirectInput HID 的主链稳定。

## 当前执行顺序

1. 完成 cross-device edge / hold contract 收口。
2. 统一真实硬件覆盖 checklist。
3. 把可公开说明的硬件行为回写主线与相关参考文档。

## 近期交付

- `B1` mixed-source runtime 语义收口。
- `B2` 真实 HID / IIDX 控制器验收清单落地。
- `B3` 将稳定行为同步到 diagnostics / calibration 需求输入。