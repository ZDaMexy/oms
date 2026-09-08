# P1-D 变动日志

## 2026-09-09

### 校准产品入口复核

- 对照 `BmsSettingsSubsection` 实际挂载的 supplemental editor 与测试源码：HID/button/axis/mouse live capture、反转和保存已有入口，deadzone/sensitivity 校准与持续 diagnostics 面板仍无正式产品面。
- 更新 STATUS 的审查边界，不推进校准/硬件 gate。本节仅记录源码审查；全局实测见主线最新验证。

## 2026-07-16

### 文档健康复核

- 移除“子线已建档”这类结构噪声，确认当前仍只有 supplemental binding/live capture 基线，未闭合项仍是 deadzone、sensitivity、scratch 说明与 live diagnostics。
- 本次仅改文档，未改代码，未运行产品测试或 Release。

## 2026-04-20

### 子线正式建档

- `P1-D` 已建立独立目录与四件套文档。
- 当前仅完成文档结构治理，未新增代码、构建或测试执行。
