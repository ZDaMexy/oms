# IIDX 外部参考审计

> 最后更新：2026-04-17
> 本文档沉淀对 iidx.org 及相关差异资料的整理结果，用于审核和校正 OMS 的产品方向。
> 本文档不是进度日志，不替代 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)、[DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) 或 [OMS_COPILOT.md](OMS_COPILOT.md)。

## 文档定位

- 目的：把外部权威资料中的稳定结论收敛成 OMS 可执行的设计约束，避免后续重复调研。
- 使用方式：当内部实现、命名、默认设置或训练路径存在分歧时，优先用本文件校验方向，再决定是否写入技术规范或开发计划。
- 边界：本文件只总结对 OMS 有持续价值的玩法、体验和设定结论，不复制站点原文，不记录仓库短期进度。

## 参考来源

本次审计以 iidx.org 为主要整理源，重点阅读了以下页面：

- Getting started / Beginner / Tips / Options / Advanced Options
- Floating Hi-Speed / In-game Controls / Charge Notes, BSS, MSS
- Gauge / Gauge & Timing / EX-Score / Random / Dan / Controllers
- IIDX / LR2 / beatoraja differences

如未来遇到冲突，建议按以下优先级判断：

1. 官方机台 / Infinitas 的实际行为与官方说明
2. iidx.org 对 IIDX 机制和训练习惯的归纳
3. iidx.org 对 LR2 / beatoraja 差异的整理
4. 社区控制器与硬件经验，用于校准硬件体验，不直接决定核心规则

## 稳定结论

| 主题 | 外部结论 | 对 OMS 的约束 |
| --- | --- | --- |
| 判定与能量条 | IIDX、LR2、beatoraja 在 timing window、空 POOR、hard / dan gauge 细节上并不等价 | 必须继续显式区分模式，不可合并成模糊的“兼容判定” |
| EX-SCORE 体系 | IIDX 的核心成绩表达仍是 EX-SCORE、DJ LEVEL、CLEAR LAMP | 结果页、筛选、训练目标优先围绕这三项表达，而不是泛化为 osu 风格 accuracy 叙事 |
| 新手学习路径 | 新手先学完整打谱、稳定速度感、基础手型和 scratch，不鼓励依赖 auto scratch | 不应把 assist 作为主教学路径；默认引导要强调正常游玩闭环 |
| FAST/SLOW 与偏移 | 早期开启 timing display、judge display、可调 offset 是训练核心组成 | 应优先补 BMS 专用 FAST/SLOW、judge 反馈和低摩擦 offset 交互 |
| FHS / green number / white number | 这是一整套速度、lane cover、LIFT 和 BPM 变化补偿语义，不是孤立数字 | 若未来实现，必须作为完整专题；在此之前不要只做术语表面化 |
| Lane cover | SUDDEN+ / HIDDEN+ 是成熟的阅读工具，且需要游玩中快速调整 | 已有 lane cover 基础应保留，并继续优化交互和文案 |
| 训练闭环 | Retry、pacemaker、class mode / dan、step-up 都服务于“重复训练且反馈明确” | 优先级应先补 retry / pacemaker / 目标练习，再考虑大型模式 |
| 控制器体验 | deadzone、sensitivity、analog / digital scratch、polling、1P / 2P 侧别会直接影响真实手感 | 输入后端之外必须补可见的校准和诊断层 |
| 随机选项 | OFF / MIRROR / RANDOM / S-RANDOM 等术语稳定，且玩家依赖其训练语义 | 命名和文案应尽量贴近 IIDX / BMS 社区共识 |

## 对 OMS 的方向校正

### 应继续坚持的方向

- 保留离线、本地库、BMS 文件系统直读和多谱库根导向。
- 保留 OD / LR2 / beatoraja 三套判定和 gauge 语义分离。
- 保留 EX-SCORE / DJ LEVEL / CLEAR LAMP / gauge history / note distribution 作为 BMS 主表达。
- 保留 lane cover、scroll speed、多输入后端和 scratch 语义验证主线。

### 应优先补强的方向

- 补“第一次游玩”和“基础设置建议”层，而不是默认让用户从大量 ruleset 设置里自行摸索。
- 补 BMS 专用 FAST/SLOW、judge display、offset 调整入口、必要的减干扰选项。
- 补 EX-SCORE pacemaker 或目标差值显示，让训练目标前置到 gameplay 而不只在结果页出现。
- 补控制器校准与输入诊断，包括 deadzone / sensitivity / scratch 预期行为说明。
- 先补 quick retry、result retry 强化、target practice 之类低摩擦训练入口，再考虑 dan / class / step-up。

### 不应机械照搬的方向

- 不要在未建立完整 FHS 语义前，仅为了“像 IIDX”引入 green number / white number 数字 UI。
- 不要把 auto scratch、legacy note 一类 assist 当作默认教学路径。
- 不要把 IIDX / LR2 / beatoraja 规则差异藏在文案后面；必须明确告诉用户当前使用的是哪一套语义。
- 不要先追高阶 playstyle 教学，再补控制器校准；真实输入一致性是前提。

## 与当前仓库的映射

### 已与外部参考基本一致

- BMS top / bottom lane cover 与游玩中调整
- Scroll speed 基础设置
- LN / CN / HCN 长条模式区分
- EX-SCORE / DJ LEVEL / CLEAR LAMP / gauge history / note distribution
- 训练向随机选项命名与第一轮实现：MIRROR / RANDOM / R-RANDOM / S-RANDOM
- 多输入后端：键盘、Raw Input、XInput、MouseAxis、DirectInput HID

### 当前最明显的体验缺口

- BMS 专用 FAST/SLOW 与 judge display 反馈层
- BMS 专用 offset / timing 调整体验
- Controller calibration / deadzone / sensitivity 可见入口
- Gameplay 侧 EX pacemaker / target 反馈
- 面向初学者的默认设置向导和低摩擦训练入口

### 明确仍属后续项的内容

- 1P / 2P flip、DBM / DBR 等 DP 训练向选项
- BSS / MSS 语义完整支持
- Dan / class / step-up 模式
- 完整的 IIDX 风格 FHS / green-white number 体系

## 机制差异基线

以下内容只保留对 OMS 长期有价值、且容易在实现时被误混的差异边界。

### 判定与 timing

- IIDX 常规 note 的公开基线可近似理解为：PGREAT ±16.67ms，GREAT ±33.33ms，GOOD ±116.67ms，BAD ±250ms。
- IIDX 的常规 POOR 是 late BAD 结束后未命中所触发；excessive / empty POOR 可能发生在 note 前，也可能发生在 note 后，但精确窗口不宜在缺更强来源时写死成单一数值。
- LR2 的 note timing 不是单一固定窗，而是按 judge rank 分出 EASY / NORMAL / HARD / VERY HARD 四档；一组常见基线分别是 21/60/120/200、18/40/100/200、15/30/60/200、8/24/40/200，且 excessive POOR 只发生在 note 前。
- beatoraja 不是“LR2 加减几毫秒”：其 EASY 基线比 LR2 更宽，BAD / excessive poor 还带 early / late 非对称；VERY EASY / EASY / NORMAL / HARD / VERY HARD 通过整数截断缩放派生，scratch note 与 long-note release 也有额外扩窗规则。
- 对 OMS 的约束：Judge mode 必须继续是明确的规则切换，不应退化成一个模糊的“判定强度”选项；后续若追求 BEATORAJA / LR2 parity，就必须显式建模 judge-rank tier、early/late 非对称窗口、scratch / release 特例，以及 judge-family-specific Empty Poor / excessive poor，而不是长期共用同一个绝对偏移判定器。

### 长条、CN、BSS、MSS

- IIDX 的 CN 起点和终点各判一次；尾判是 release timing，不是单纯 hold 完成判定。
- HCN 不改变 score 和 timing 语义，主要改变的是持有期间的 gauge 行为。
- BSS 不是普通长条：要求持续同方向旋转，并在末尾反向收尾；MSS 还要求在中途标记点切换方向。
- IIDX 中若 CN 或 BSS 起点直接拿 BAD / POOR，尾部不会再产生有效判定，也不会继续影响 JUDGE 统计。
- 对 OMS 的约束：后续若补 BSS / MSS，不能直接套用普通 hold 或 scratch-hold 模型。

### Gauge 与课程体验

- IIDX 的 EASY / NORMAL 从 22% 起始，终点分别按 80% / 60% 过关门槛处理；普通 groove gauge 依赖 a-value，而不是简单的 total / note count。
- IIDX 的 HARD / DAN 在低血区有减伤补正；EX-HARD 没有同等保护。
- LR2、beatoraja 的 easy / normal / hard / dan 算法都不同，且 beatoraja 的 survival gauge 在低血区往往比 LR2 更宽松。
- BMS dan 在 beatoraja 里常通过 gauge_lr2 采用 LR2 DAN gauge；难度表也通常以 LR2 easy / normal 体验为基准评级。
- 对 OMS 的约束：表分、课程分、生存体验和结果说明不能跨 IIDX / LR2 / beatoraja 直接等价比较。

### FHS、GN、WN、LIFT

- IIDX 的 SUDDEN+ white number 与 LIFT 都是独立的 1000 制量，green number 本质是 60fps 下 note 可见帧数乘以 10。
- beatoraja 在启用 LIFT 时 white number 的计算方式和 IIDX 不同；LR2 的 GN / HS 换算又依 skin 而变。
- 对 OMS 的约束：若未来做 FHS，必须一次性定义 scroll speed、cover、LIFT、BPM 补偿和显示方式整套语义；在此之前不要半套引入 GN / WN 术语。

### EX-SCORE、反馈与训练

- IIDX 的主成绩语言仍是 EX-SCORE、DJ LEVEL、CLEAR LAMP。AAA 阈值是 8/9，AA 是 7/9，A 是 6/9。
- 空 POOR 可用 MISS_COUNT - COMBO_BREAK 反推；DJ POINT 存在，但不是主训练语言。
- FAST/SLOW、JUDGE display、visual draw offset 是标准训练反馈，audio offset 语义并不适合 key-sounded 体系。
- 对 OMS 的约束：结果页和训练目标应优先围绕 EX-SCORE；BMS 专用 FAST/SLOW、judge display、offset 交互仍是高优先级缺口。

### 段位、随机与输入硬件

- Dan clear rate 不是简单的 pass / fail 百分比；它包含进度项与判定项，甚至可能出现 clear rate 100% 但仍失败。
- OFF / MIRROR / R-RANDOM / RANDOM / S-RANDOM 这些术语稳定，DP 还有 FLIP、DBM、DBR 等训练语义。
- LR2 只接受数字 turntable；beatoraja 可接受数字或模拟；Infinitas 更偏好模拟，且 120Hz 对 deadzone / sensitivity 更敏感。
- 对 OMS 的约束：课程模式若落地，应把 clear rate 与通过结果拆开表达；输入设置不应只有绑定，还应暴露 deadzone、sensitivity、scratch 模式预期和诊断信息。

## 推荐推进顺序

1. 先补反馈闭环：FAST/SLOW、judge display、offset 交互、controller calibration。
2. 再补训练闭环：quick retry、result retry、EX pacemaker、target practice。
3. 然后再决定是优先做 dan / class / step-up，还是单独立项做完整 FHS 体系。

## 维护约定

- 当外部资料只影响“方向判断”时，更新本文件。
- 当外部结论已经转化为硬约束时，同步写入 [OMS_COPILOT.md](OMS_COPILOT.md)。
- 当外部结论已经改变阶段优先级时，同步写入 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md) 与 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)。
