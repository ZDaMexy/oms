# Phase 1.x 子线路由

每条子线维护 `PLAN / STATUS / CHANGELOG / TECHNICAL_CONSTRAINTS` 四件套。日常先读 `STATUS`；只有准备实施时才读 `PLAN` 和任务相关约束；历史用 `CHANGELOG` 搜索。

| 子线 | 负责范围 | 当前判定 | 下一道门 |
| --- | --- | --- | --- |
| [P1-A](P1-A/DEVELOPMENT_STATUS.md) | 产品面、Skin V1、release gate | 原C1～C7非人工工作`7/7 closed`；双包、完整制作、正式保底、安装恢复及实际集中体验包已交付 | 原V-001～V-004仍0/4、V-005未签，双包观感/设备/长时待验；旧OmsSkin仅历史对照，Skin V1/release未完成 |
| [P1-B](P1-B/DEVELOPMENT_STATUS.md) | 输入语义与硬件 | 软件链可用，真实 HID 覆盖未闭合 | analog scratch 跨设备与实机验收 |
| [P1-C](P1-C/DEVELOPMENT_STATUS.md) | 判定语义与反馈 | 判定 parity 主体已落；常驻反馈卡已按产品决定删除 | 保持 parity gate，补剩余人工/展示面 |
| [P1-D](P1-D/DEVELOPMENT_STATUS.md) | 控制器校准与诊断 | 未完成 | deadzone、sensitivity、live diagnostics |
| [P1-E](P1-E/DEVELOPMENT_STATUS.md) | gameplay 与 LN/CN/HCN | 自动链已具备，真实谱面验校未闭合 | 真实谱面长条与输入验收 |
| [P1-F](P1-F/DEVELOPMENT_STATUS.md) | 离线发行物与覆盖更新 | 最终多文件包便携/自定义根、恢复、完整覆盖后真实启动及正常退出通过；旧候选事故事后保全 | 独立账户非便携、设备/长时及公开发行组合人工门；不把隔离通过追溯为旧事故无损 |
| [P1-G](P1-G/DEVELOPMENT_STATUS.md) | 人工验收汇总 | 静态皮肤与 portable 已有分项证据；总清单未闭合 | 汇总皮肤、输入、长条/音频、Song Select、BGA 与发行矩阵 |
| [P1-H](P1-H/DEVELOPMENT_STATUS.md) | 存储拓扑 | `chartbms/chartmania` 与多根扫描基线已落 | 删除/失效、去重和重扫策略 |
| [P1-I](P1-I/DEVELOPMENT_STATUS.md) | BMS 选歌筛选与搜索 | read-model/搜索主链已落；实际仍三行双端原型 | 先实现既定单轨产品面，再补 headless/shared visual gate |
| [P1-J](P1-J/DEVELOPMENT_STATUS.md) | gameplay 性能与音频 | 普通密度主故障与C3末端lane/shared store真实发声proof已收口 | 转谱 LN、50k profile、人工清单 |
| [P1-K](P1-K/DEVELOPMENT_STATUS.md) | BMS 解析与转换 | K1–K12主体阶段性收口；C3所需lane timeline上界、sparse keymode authority/override/diagnostic与真实发声已闭合 | 保持parser/converter唯一authority，继续P1-K自身剩余特殊谱尾项 |
| [P1-L](P1-L/DEVELOPMENT_STATUS.md) | Gimmick/BGA 视觉 | 播放主链、C3唯一viewport与C5只读事件已落；当前仍每viewport一个player | 单内容源、逐谱视觉与反向滚动 |
| [P1-M](P1-M/DEVELOPMENT_STATUS.md) | 内置音乐播放器 | 规划完成，未开工 | 主线 R3–R6/release gate 完成或产品改序后，再启动 PlayQueue 地基 |

子线变化只有在影响全局优先级、release gate 或硬约束时才回写 mainline；禁止把整段子线实现史复制到主线。
