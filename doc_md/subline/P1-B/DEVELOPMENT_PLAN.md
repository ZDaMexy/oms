# P1-B 开发计划：输入语义与硬件验收

> 最后更新：2026-10-21（输入软件收口后的真实设备验收）
> 主线总规划见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。

## 子线目标

- 收口 analog scratch、cross-device trigger、mixed-source runtime 与真实 HID 硬件验收。
- 保持 keyboard / Raw Input / XInput / MouseAxis / DirectInput HID 的主链稳定。
- 把 desktop public settings surface 与底层 runtime input contract 明确拆开，避免因为产品裁剪误删 mouse/touch/tablet 输入链。

## 当前执行顺序

1. 以 [10月21日软件输入审查](../../other/INPUT_SIGNAL_REVIEW_20261021.md) 为输入，在正常 VS Code 窗口验鼠标持续位移、真实父事件、暂停／失焦和恢复；不把直接 Trigger 调用当作真实设备证据。
2. 联同 [P1-D](../P1-D/DEVELOPMENT_PLAN.md) 验实体转盘、摇杆、键盘、同型号多设备及断线：观察原始值和解释动作，核跨设备最终释放、连皿攻击和长皿停止等待。
3. 在真实谱验证 LN／CN／HCN、回放与方向切换；记录报告率、范围、后端和具体设备，不预设通用最优阈值，不新增平行后端。
4. 维持 OMS-owned 设置入口裁剪和底层 runtime 消费链，真实体验结果由本线记录、P1-G 汇总。

## 未闭合交付

- `B1` 已交付软件合同的真实窗口与 mixed-source 手感验收。
- `B2` 真实 HID / IIDX 控制器验收清单落地。
- `B3` 现场校准与持续 diagnostics 的实际设备覆盖，沿 P1-D 接续，不重复开发已交付解释器。
