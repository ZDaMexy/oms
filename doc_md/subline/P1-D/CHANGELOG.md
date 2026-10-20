# P1-D 变动日志

## 2026-10-21校准与持续设备信号测试

补充绑定编辑器增加具体设备选择、离线保存项和歧义提示；后台发现迟到保留编辑行。四种轴用途按需显示参数和单位，高级范围／中心可选择覆盖设备能力。单次捕获与持续测试独立，持续原始信号和最终动作复用真实解释器，鼠标复用宿主相对输入和临时捕获所有权；不注入游玩，应用操作才保存。

换设备时重新监听正确设备；取消、删除、换变体及关闭卸订并释放 UI 所有权，驱动资源后台释放、失败明确记录。实际 loaded UI／生命周期及校准回归结果见[输入审查](../../other/INPUT_SIGNAL_REVIEW_20261021.md#验证证据)，正常窗口与实体设备待验。

## 2026-09-29

### 区分设备枚举测试与校准产品验收

- 回读9月22日 `total-bms-full.trx`，`TestDeviceDiscoveryRunsOffCallingThread` 通过；对照测试源码确认它只证明设备枚举离开调用线程，未验证完整 live capture/UI 或真实设备校准。
- STATUS 补明确的产品证据日期与范围，保留 deadzone/sensitivity、持续 diagnostics 和硬件缺口；约束改为仅同步实际变化与有全局影响的摘要。未改产品、未重跑测试或实机。

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
