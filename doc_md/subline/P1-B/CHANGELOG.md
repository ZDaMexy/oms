# P1-B 变动日志

## 2026-10-19原生事件时间与控制器稳态

原生队列保留单调接收时间，在既有offset/rate后时钟发布的样本间映射；每外层帧冻结批次，由原FrameStability走到事件时刻后再按键，真实判定、RawTime与录制一致。暂停/seek/reset/offset/source/replay所有权按epoch失效，含同帧attach/detach；未来等待、迟到不回写过去，同刻顺序保持。普通框架输入、组合键和其它设备引用合同保留。

DirectInput复用原生state和change buffer，取消HasPendingChanges隐式二次快照；设备Handler/axis稳态不物化临时集合。运行中发现与候选/退休资源释放改到唯一后台任务，Update只提交完成结果，退出封门后锁外join；断开仍当帧松键，独立幂等Dispose修复断线资源泄漏。实际卡顿输入、闭锁provider、通知/退出竞态、原生COM wrapper和完整回归证据集中见[深审记录](../../other/GAMEPLAY_PERFORMANCE_JUDGEMENT_20261019.md#验证证据)，不代签硬件延迟或所有机器零卡顿。

## 2026-10-19原生键盘线程与单一来源

原WndProc同步进入判定/Drawable的路径改为只入有序transition/reset队列，首个可消费的gameplay Update执行；reset丢旧待发事件并只释放键盘引用，关闭后拒绝晚到事件，exact sink原子替换。监听在桌面初始化安装一次，当前交付范围与SDL ownership一致；原生不可用或不能覆盖整组绑定时统一回退普通键盘，避免两批完整tap重复与混合组合被拆分。

左右实体修饰键共同满足虚拟组合，精确左右绑定仍独立；字母/标点与固定framework的物理位置一致，NumLock关闭的导航/Clear身份保持。handler热路径移除反复动作物化与All委托分配。loaded真判定、混合来源两种批次、左右Ctrl、reset/其它设备引用和独立Windows输入/Break已有证据；修前失败、测量、原生取景与最终完整回归见[集中审查](../../other/GAMEPLAY_REVIEW_20261019.md)。没有新后端或实体硬件签收。

## 2026-09-29

### 输入证据日期与同步职责复核

- 回读 `osu.Game.Rulesets.Bms.Tests/TestResults/total-bms-full.trx`，确认9月22日 scratch bridge 43/43通过；STATUS 将无日期的“当前可通过”改为留存结果及其软件范围，不代签真实控制器。
- 约束回链唯一协作规则，只同步实际受影响文档；不再强制每次刷新四件套与主线。仅文档治理，未运行产品测试、Release或硬件验收。

## 2026-09-09

### 输入 production 与文档边界复核

- 核对 `BmsInputManager`、`OmsInputRouter`、DirectInput provider、desktop settings host 与 `TestSceneOmsScratchGameplayBridge` 源码；软件输入与 first-press/final-release 主链已接线，loaded scene 不能替代真实 HID/控制器覆盖。
- STATUS 删除最新验证外重复的测试数字，保留 cross-device 终态与硬件签收门。本条只记录源码和文档审查，人工验收未推进；全局实测见主线最新验证。

## 2026-07-16

### 文档健康治理：移除已完成建档/B0 噪声

- STATUS/PLAN 保留当前输入主链、未闭合 mixed-source 语义与真实 HID gate；已完成的建档和 desktop settings `B0` 只留历史，不再伪装成未来交付。
- 本次仅改文档，未改代码，未运行产品测试或 Release。

## 2026-05-09

### desktop public settings surface 收口：安全隐藏 upstream mouse/touch/tablet 分区

- `OsuGameDesktop` 现已 override `CreateSettingsSubsectionFor(InputHandler)`，在 desktop Settings -> 输入 中对 `ITabletHandler`、`TouchHandler` 与 `MouseHandler` 返回 `null`，因此上游通用的数位板 / 触屏点击 / 鼠标 subsection 不再继续暴露给最终产品面。
- 这次变更明确属于 **安全隐藏** 而不是 runtime 删除：mouse/touch/tablet handler 与 `MouseDisableButtons` / `MouseDisableWheel` / `ConfineMouseMode` / `TouchDisableGameplayTaps` 的消费链保持不变。
- 该裁剪故意保持在 `OsuGameDesktop` 层而不是下移到 `OsuGameBase`，避免同步改写 test scene / 非 desktop host 的输入设置装配。
- 验证：`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

## 2026-04-20

### 子线正式建档

- `P1-B` 已建立独立目录与四件套文档。
- 当前仅完成文档结构治理，未新增代码、构建或测试执行。
