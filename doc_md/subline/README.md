# Phase 1.x 子线路由

每条子线维护 `PLAN / STATUS / CHANGELOG / TECHNICAL_CONSTRAINTS` 四件套。日常先读 `STATUS`；只有准备实施时才读 `PLAN` 和任务相关约束；历史用 `CHANGELOG` 搜索。

| 子线 | 负责范围 | 当前判定 | 下一道门 |
| --- | --- | --- | --- |
| [P1-A](P1-A/DEVELOPMENT_STATUS.md) | 产品面、Skin V1、release gate | `C1`作者工作区/archive与`C2`三源publication/participant/detach/retire均已闭合；当前`2/7 closed，C3 active`，`V-001`～`V-004`签收0/4 | 闭合P1-K lane/keymode前置与唯一layout；G1最终整包门、`SV1-1`、`SV1-2`/Skin V1仍未完成 |
| [P1-B](P1-B/DEVELOPMENT_STATUS.md) | 输入语义与硬件 | 软件链可用，真实 HID 覆盖未闭合 | analog scratch 跨设备与实机验收 |
| [P1-C](P1-C/DEVELOPMENT_STATUS.md) | 判定语义与反馈 | 判定 parity 主体已落；常驻反馈卡已按产品决定删除 | 保持 parity gate，补剩余人工/展示面 |
| [P1-D](P1-D/DEVELOPMENT_STATUS.md) | 控制器校准与诊断 | 未完成 | deadzone、sensitivity、live diagnostics |
| [P1-E](P1-E/DEVELOPMENT_STATUS.md) | gameplay 与 LN/CN/HCN | 自动链已具备，真实谱面验校未闭合 | 真实谱面长条与输入验收 |
| [P1-F](P1-F/DEVELOPMENT_STATUS.md) | 离线发行物与覆盖更新 | portable/custom-root 基线已验证 | 随 release gate 产出候选包并复核 fresh extract/覆盖更新 |
| [P1-G](P1-G/DEVELOPMENT_STATUS.md) | 人工验收汇总 | 静态皮肤与 portable 已有分项证据；总清单未闭合 | 汇总皮肤、输入、长条/音频、Song Select、BGA 与发行矩阵 |
| [P1-H](P1-H/DEVELOPMENT_STATUS.md) | 存储拓扑 | `chartbms/chartmania` 与多根扫描基线已落 | 删除/失效、去重和重扫策略 |
| [P1-I](P1-I/DEVELOPMENT_STATUS.md) | BMS 选歌筛选与搜索 | 主功能已落 | 单轨拖拽 headless 与 shared visual gate |
| [P1-J](P1-J/DEVELOPMENT_STATUS.md) | gameplay 性能与音频 | 普通密度主故障已收口 | 末端 lane 发声 proof、转谱 LN、50k profile、人工清单 |
| [P1-K](P1-K/DEVELOPMENT_STATUS.md) | BMS 解析与转换 | K1–K12 主体阶段性收口 | lane timeline 上界、sparse keymode authority、特殊谱尾项 |
| [P1-L](P1-L/DEVELOPMENT_STATUS.md) | Gimmick/BGA 视觉 | 播放主链已落，skin ownership 待迁移 | 单内容源/只读 viewport、逐谱视觉与反向滚动 |
| [P1-M](P1-M/DEVELOPMENT_STATUS.md) | 内置音乐播放器 | 规划完成，未开工 | 主线 R3–R6/release gate 完成或产品改序后，再启动 PlayQueue 地基 |

子线变化只有在影响全局优先级、release gate 或硬约束时才回写 mainline；禁止把整段子线实现史复制到主线。
