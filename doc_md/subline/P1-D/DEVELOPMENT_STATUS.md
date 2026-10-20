# P1-D 开发进度：控制器校准与诊断

> 最后核对：2026-10-21（校准与持续信号测试接入；正常窗口与实体设备待签收）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，当前执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

## 当前阶段

- BMS 补充绑定按变体编辑，设备下拉显示具体实例、名称及离线保存项；旧标识出现多候选时明确阻止猜选。异步发现完成不会重建并抹去当前编辑行。
- HID 轴可选择相对位移、环形编码器、位置按钮或摇杆模拟运动；鼠标仅相对位移。按用途显示灵敏度／运动阈值、按下／松开回差、死区／满偏速度及运动停止等待；高级范围／中心校准可留空使用设备范围。
- 鼠标区域提供可操作的“高精度鼠标输入”开关，直接绑定宿主原有持久化值，不自动启用或另存第二份配置；无相对来源／鼠标 handler 的宿主显示原因。
- 单次捕获保留；每行增加持续“测试设备信号”，同时显示原始报告和同一游玩解释器产生的触发／运动。编辑参数立即作用于测试，测试不进入游玩，应用操作才保存。
- 捕获／测试每次只有一个所有者，删除行、换变体、取消和关闭会撤销 UI 所有权；驱动发现／销毁在后台完成，错误明确记录。实际回归与边界取 [输入审查](../../other/INPUT_SIGNAL_REVIEW_20261021.md)。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| diagnostics state | 软件入口已接通 | 持续原始信号与解释动作，正常窗口和硬件待验 |
| calibration UI | 软件入口已接通 | 按用途显示参数／单位，高级范围与中心校准 |
| 对外说明文案 | 已接入设置 | 明确模式、阈值、停止等待和不可用信号；手感仍需实际设备验证 |

## 最近一次验证

2026-10-21：实际加载编辑器、后台发现、持续测试与生命周期结果集中见 [本轮验证](../../other/INPUT_SIGNAL_REVIEW_20261021.md#验证证据)。软件夹具不替代实体设备、USB 热插拔或正常窗口体验。

此前仅设备枚举的验证保留原日期与范围，见 [CHANGELOG](CHANGELOG.md)。

## 文档治理验证

2026-09-29：对照 [BmsSettingsSubsection](../../../osu.Game.Rulesets.Bms/BmsSettingsSubsection.cs)、[supplemental editor](../../../osu.Game.Rulesets.Bms/BmsSupplementalBindingSettingsSection.cs) 与[设备枚举测试](../../../osu.Game.Rulesets.Bms.Tests/TestSceneBmsSupplementalBindingSettingsSection.cs)，区分源码可见的按键/轴捕获、反转/保存与留存测试实际覆盖范围。未运行新的产品测试；独立校准及持续 diagnostics 产品面仍未交付。此前审查见 [CHANGELOG](CHANGELOG.md)。
