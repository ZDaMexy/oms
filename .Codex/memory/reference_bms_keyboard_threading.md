---
name: reference-bms-keyboard-threading
description: 原生窗口回调的线程、双通道完整tap、混合组合键和物理修饰键诊断
metadata:
  node_type: memory
  type: reference
---

# BMS 键盘线程与来源召回

行为合同见 [P1-B](../../doc_md/subline/P1-B/TECHNICAL_CONSTRAINTS.md)，源码和真实回归的来源见 [2026-10-19 审查](../../doc_md/other/GAMEPLAY_REVIEW_20261019.md)。这里仅保存诊断地雷。

- Windows WndProc 安装在 host.InputThread，不能因接口名为 input 就同步调用 KeyBindingContainer.TriggerPressed；该调用不会替你切到 Update，真实命中会继续进入 Drawable transform mutation。先在 loaded note 的实际 NewResult 检查线程，再核录制/血量，不能用未加载 handler 单测代证。
- HashSet 去重只能消除重叠的 down，不能消除 raw down/up 与 SDL down/up 两批完整 tap。重现须两种批次顺序都跑，并观察真实 action/result，而非只检查 pressed 最终为 false。
- 按单键判断原生支持会把组合拆到两个批次；尤其原生可解 Ctrl、框架独有媒体键的组合。应在加载最终绑定时确定整体来源；单个 unsupported 键成功回退不证明混合 chord 正常。
- “hook 已安装”不等于“正在交付”。如果原生只在 Playing 交付、SDL ownership 却只看 installed，Break/暂停中允许的配置按键会两路都丢。使用既有 pre-start paused 用例核产品合同，再在真实 Windows source 中核当前交付范围。
- 原生回调给出左右实体修饰键，持久化组合可能保存虚拟 Control/Shift/Alt/Super；不能只用 pressedKeys.Contains 虚拟值，也不能释放左侧就清除仍由右侧满足的组合。
- Windows VKey 的字母值随键盘布局改变，而固定框架的字母/标点按 scancode；小键盘又有 NumLock 导航语义，不能把两者笼统改成全物理或全字符映射。核对本机 package 指向的固定 framework 源码，不猜 latest 行为。
- fake 支持能力须在 input manager 加载前设置。测试保存键位到 OsuTestScene.Realm 后，还须将该 RealmAccess 明确提供给实际 renderer；只修改测试 Realm、子树仍解析宿主 Realm 会让“组合键坏了”成为夹具误报。
