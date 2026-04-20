# P1-D 开发计划：控制器校准与诊断

> 最后更新：2026-04-20
> 主线总规划见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。

## 子线目标

- 提供 deadzone、sensitivity、scratch 模式说明与 live diagnostics UI。
- 把当前 supplemental bindings 与 live capture 扩展为可面向真实控制器调试的产品面。

## 当前执行顺序

1. 明确 diagnostics 所需最小输入状态集。
2. 明确校准 UI 与 `P1-B` 的真实硬件验收接口。
3. 明确对外说明文案与限制边界。