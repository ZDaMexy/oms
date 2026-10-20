# P1-D 开发计划：控制器校准与诊断

> 最后更新：2026-10-21（软件校准入口后的设备体验验收）
> 主线总规划见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。

## 子线目标

- 提供 deadzone、sensitivity、scratch 模式说明与 live diagnostics UI。
- 把当前 supplemental bindings 与 live capture 扩展为可面向真实控制器调试的产品面。

## 当前执行顺序

1. 在正常 VS Code 窗口走完选择设备→捕获→持续测试→调参数→应用→重新进入的完整路径，验窄窗口、设备名称和高级设置可读性。
2. 与 [P1-B](../P1-B/DEVELOPMENT_PLAN.md) 一起验鼠标、实体转盘和摇杆：记录原始范围、回中／跨圈、报告率、阈值与反转，不用一个默认值替代实际校准。
3. 验同型号两台、离线保存项、后台发现迟到、取消和关闭释放；任何歧义都要有可理解的原因，不自动换设备或静默换模式。
4. 实谱验停止等待、连皿和长皿手感及回放一致性；文案变化以实际证据回写本线，现有软件证据取 [10月21日审查](../../other/INPUT_SIGNAL_REVIEW_20261021.md)。
