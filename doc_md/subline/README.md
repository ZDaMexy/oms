# Phase 1.x 子线路由

每条子线维护 `PLAN / STATUS / CHANGELOG / TECHNICAL_CONSTRAINTS` 四件套。日常先读 `STATUS`；只有准备实施时才读 `PLAN` 和任务相关约束；历史用 `CHANGELOG` 搜索。

| 子线 | 负责范围 | 当前判定 | 下一道门 |
| --- | --- | --- | --- |
| [P1-A](P1-A/DEVELOPMENT_STATUS.md) | 产品面、Skin V1、release gate | `C1`作者工作区/archive、`C2`三源revision、`C3`唯一layout、`C4` public catalog/shared codec/三态resolved material 与 `C5` versioned scene/animation/read-only event、真实 BMS/mania production hosts 均已闭合；当前`5/7 closed，C6 active`，`V-001`～`V-004`签收0/4 | C6 脚本隔离/权限协商与最终 ini/manifest/scene/script/素材整包 reload 门；随后 G1/SV1-1/SV1-2 与 Skin V1 release gate 仍未完成 |
| [P1-B](P1-B/DEVELOPMENT_STATUS.md) | 输入语义与硬件 | 软件链可用，真实 HID 覆盖未闭合 | analog scratch 跨设备与实机验收 |
| [P1-C](P1-C/DEVELOPMENT_STATUS.md) | 判定语义与反馈 | 判定 parity 主体已落；常驻反馈卡已按产品决定删除 | 保持 parity gate，补剩余人工/展示面 |
| [P1-D](P1-D/DEVELOPMENT_STATUS.md) | 控制器校准与诊断 | 未完成 | deadzone、sensitivity、live diagnostics |
| [P1-E](P1-E/DEVELOPMENT_STATUS.md) | gameplay 与 LN/CN/HCN | 自动链已具备，真实谱面验校未闭合 | 真实谱面长条与输入验收 |
| [P1-F](P1-F/DEVELOPMENT_STATUS.md) | 离线发行物与覆盖更新 | portable/custom-root 基线已验证 | 随 release gate 产出候选包并复核 fresh extract/覆盖更新 |
| [P1-G](P1-G/DEVELOPMENT_STATUS.md) | 人工验收汇总 | 静态皮肤与 portable 已有分项证据；总清单未闭合 | 汇总皮肤、输入、长条/音频、Song Select、BGA 与发行矩阵 |
| [P1-H](P1-H/DEVELOPMENT_STATUS.md) | 存储拓扑 | `chartbms/chartmania` 与多根扫描基线已落 | 删除/失效、去重和重扫策略 |
| [P1-I](P1-I/DEVELOPMENT_STATUS.md) | BMS 选歌筛选与搜索 | 主功能已落 | 单轨拖拽 headless 与 shared visual gate |
| [P1-J](P1-J/DEVELOPMENT_STATUS.md) | gameplay 性能与音频 | 普通密度主故障与C3末端lane/shared store真实发声proof已收口 | 转谱 LN、50k profile、人工清单 |
| [P1-K](P1-K/DEVELOPMENT_STATUS.md) | BMS 解析与转换 | K1–K12主体阶段性收口；C3所需lane timeline上界、sparse keymode authority/override/diagnostic与真实发声已闭合 | 保持parser/converter唯一authority，继续P1-K自身剩余特殊谱尾项 |
| [P1-L](P1-L/DEVELOPMENT_STATUS.md) | Gimmick/BGA 视觉 | 播放主链与C3只读最终viewport已落；内容/timeline仍归P1-L | 单内容源、逐谱视觉与反向滚动 |
| [P1-M](P1-M/DEVELOPMENT_STATUS.md) | 内置音乐播放器 | 规划完成，未开工 | 主线 R3–R6/release gate 完成或产品改序后，再启动 PlayQueue 地基 |

子线变化只有在影响全局优先级、release gate 或硬约束时才回写 mainline；禁止把整段子线实现史复制到主线。
