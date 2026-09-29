# Phase 1.x 子线路由

每条子线维护 `PLAN / STATUS / CHANGELOG / TECHNICAL_CONSTRAINTS` 四件套。日常先读 `STATUS`；只有准备实施时才读 `PLAN` 和任务相关约束；历史用 `CHANGELOG` 搜索。

| 子线 | 负责范围 | 当前判定 | 下一道门 |
| --- | --- | --- | --- |
| [P1-A](P1-A/DEVELOPMENT_STATUS.md) | 产品面、Skin V1、release gate | 静线唯一内置；模式选择、固定目录刷新和原组件编辑恢复已落；外观打磨暂停，星轨已退役 | 局部认可不代签整体；保留V-001～V-005及设备/长时门，当前只做核对收尾；Skin V1/release未完成 |
| [P1-B](P1-B/DEVELOPMENT_STATUS.md) | 输入语义与硬件 | 软件链可用，真实 HID 覆盖未闭合 | analog scratch 跨设备与实机验收 |
| [P1-C](P1-C/DEVELOPMENT_STATUS.md) | 判定语义与反馈 | 判定 parity、TOTAL 兼容与自动调整偏移互斥 style 已落；常驻反馈卡已删除 | 保持演奏/回放/结算一致性，补实谱判定与偏移收敛、真实设备验收 |
| [P1-D](P1-D/DEVELOPMENT_STATUS.md) | 控制器校准与诊断 | 未完成 | deadzone、sensitivity、live diagnostics |
| [P1-E](P1-E/DEVELOPMENT_STATUS.md) | gameplay 与 LN/CN/HCN | 自动链已具备，真实谱面验校未闭合 | 真实谱面长条与输入验收 |
| [P1-F](P1-F/DEVELOPMENT_STATUS.md) | 离线发行物与覆盖更新 | 历史多文件候选已通过便携/自定义根、恢复、完整覆盖后真实启动及正常退出；旧候选事故事后保全 | 后续产品改动未重新发行验收；仍缺独立账户非便携、设备/长时及公开发行组合人工门，不追溯宣称旧事故无损 |
| [P1-G](P1-G/DEVELOPMENT_STATUS.md) | 人工验收汇总 | 静态皮肤与 portable 已有分项证据；总清单未闭合 | 汇总皮肤、输入、长条/音频、Song Select、BGA 与发行矩阵 |
| [P1-H](P1-H/DEVELOPMENT_STATUS.md) | 存储拓扑 | 缺失恢复、同内容多目录及历史保全、当前页难度表刷新已落 | 隔离数据根与真实大库验收、只读诊断 |
| [P1-I](P1-I/DEVELOPMENT_STATUS.md) | BMS 选歌筛选与搜索 | read-model/搜索与单轨上限筛选已落 | 拖拽手感、窄窗口与真实大库体验 |
| [P1-J](P1-J/DEVELOPMENT_STATUS.md) | gameplay 性能与音频 | 自动键音基线保留；长伴奏暂停保位、手动转谱 LN 已落 | 双模式实谱听感、按现场证据触发50k profile、人工清单 |
| [P1-K](P1-K/DEVELOPMENT_STATUS.md) | BMS 解析与转换 | K1–K12主体及C3前置已落；TOTAL保留作者声明/缺省区别与有限正数边界 | 保持parser/converter唯一authority，补模糊谱纠正入口及真实特殊谱证明 |
| [P1-L](P1-L/DEVELOPMENT_STATUS.md) | Gimmick/BGA 视觉 | 播放主链、C3唯一viewport、C5只读事件及 BMS 显示偏移已落；当前仍每viewport一个player | 单内容源、逐谱视觉与反向滚动；偏移人工门归 P1-C |
| [P1-M](P1-M/DEVELOPMENT_STATUS.md) | 内置音乐播放器 | 规划完成，未开工 | 主线 R3–R6/release gate 完成或产品改序后，再启动 PlayQueue 地基 |

子线变化只有在影响全局优先级、release gate 或硬约束时才回写 mainline；禁止把整段子线实现史复制到主线。
