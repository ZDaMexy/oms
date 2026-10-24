# Phase 1.x 子线路由

每条子线维护 `PLAN / STATUS / CHANGELOG / TECHNICAL_CONSTRAINTS` 四件套。日常先读 `STATUS`；只有准备实施时才读 `PLAN` 和任务相关约束；历史用 `CHANGELOG` 搜索。

| 子线 | 负责范围 | 当前判定 | 下一道门 |
| --- | --- | --- | --- |
| [P1-A](P1-A/DEVELOPMENT_STATUS.md) | 产品面、Skin V1、release gate | 静线唯一内置且外观暂停；模式/键数皮肤、作者能力与独立Omni已有交付；依赖迁移后的完整BMS补验通过 | Autoplay/Omni修后正常窗口、品牌、真实下载、V-001～V-005与设备/长时门保持，Skin V1/release未完成 |
| [P1-B](P1-B/DEVELOPMENT_STATUS.md) | 输入语义与硬件 | 软件链可用，真实 HID 覆盖未闭合 | analog scratch 跨设备与实机验收 |
| [P1-C](P1-C/DEVELOPMENT_STATUS.md) | 判定语义与反馈 | 判定 parity、TOTAL 兼容与自动调整偏移互斥 style 已落；常驻反馈卡已删除 | 保持演奏/回放/结算一致性，补实谱判定与偏移收敛、真实设备验收 |
| [P1-D](P1-D/DEVELOPMENT_STATUS.md) | 控制器校准与诊断 | 轴用途、参数校准与持续原始信号/动作测试的软件入口已接通 | 正常窗口、USB热插拔、同型号与实体控制器体验待验 |
| [P1-E](P1-E/DEVELOPMENT_STATUS.md) | gameplay 与 LN/CN/HCN | 自动链已具备，真实谱面验校未闭合 | 真实谱面长条与输入验收 |
| [P1-F](P1-F/DEVELOPMENT_STATUS.md) | 离线发行物与覆盖更新 | 2026-10-03 候选同包隔离启动/恢复/覆盖已有证据，生成物随后删除；旧跨版本和事故事后保全分别保留 | 后续候选承接品牌与补丁依赖/源码许可；Windows Shell、跨版本、新作者演练、独立账户非便携与整体人工门未新签收，不追溯宣称旧事故无损 |
| [P1-G](P1-G/DEVELOPMENT_STATUS.md) | 人工验收汇总 | 静态皮肤与 portable 已有分项证据；总清单未闭合 | 汇总品牌正常窗口、皮肤、输入、长条/音频、Song Select、公共下载、BGA 与发行矩阵 |
| [P1-H](P1-H/DEVELOPMENT_STATUS.md) | 存储拓扑 | 缺失恢复、历史保全、当前页难度表刷新及扫描热路径优化已落；便携日志定位宿主链已修复 | 隔离数据根与真实大库、Explorer 实机定位及现场读取错误诊断 |
| [P1-I](P1-I/DEVELOPMENT_STATUS.md) | BMS 选歌筛选与搜索 | read-model/搜索与单轨上限筛选已落 | 拖拽手感、窄窗口与真实大库体验 |
| [P1-J](P1-J/DEVELOPMENT_STATUS.md) | gameplay 性能与音频 | 原生/转谱第二轮热路径优化；自动键音、长伴奏暂停保位与手动 LN 基线保留 | 双模式实谱听感、按现场证据触发50k profile、人工清单 |
| [P1-K](P1-K/DEVELOPMENT_STATUS.md) | BMS 解析与转换 | K1–K12主体及C3前置已落；TOTAL保留作者声明/缺省区别与有限正数边界 | 保持parser/converter唯一authority，补模糊谱纠正入口及真实特殊谱证明 |
| [P1-L](P1-L/DEVELOPMENT_STATUS.md) | Gimmick/BGA 视觉 | 游戏持有共享播放会话；作者窗口与显示偏移保持；进程内四库已整体升级并通过专项 | 最终配方从零重编及发行输出复验；真实素材、窗口/DPI与长时待验，反向滚动未实现；偏移人工门归 P1-C |
| [P1-M](P1-M/DEVELOPMENT_STATUS.md) | 内置音乐播放器 | 规划完成，未开工 | 主线 R3–R6/release gate 完成或产品改序后，再启动 PlayQueue 地基 |

子线变化只有在影响全局优先级、release gate 或硬约束时才回写 mainline；禁止把整段子线实现史复制到主线。

## 授权提前工作的 Phase 3 子线

[P3-IR](P3-IR/DEVELOPMENT_STATUS.md) 已按用户授权实现固定 OMSIR 直接登录、保存后交分、持久待交与具体谱面公开榜，独立服务 / 网页已部署待验收。旧 osu! API endpoint 仍空，原 Phase 1.x 人工 / 发行门保持；完整范围与退出条件见该线[计划](P3-IR/DEVELOPMENT_PLAN.md)和[约束](P3-IR/TECHNICAL_CONSTRAINTS.md)。
