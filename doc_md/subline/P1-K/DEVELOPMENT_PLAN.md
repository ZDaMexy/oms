# P1-K 当前计划：BMS 解析与转换治理

> 最后更新：2026-08-30（P1-A C3 的 P1-K Skin 前置已闭合）
> 当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，稳定合同见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)，逐刀历史见 [CHANGELOG.md](CHANGELOG.md)。

## 子线职责

P1-K 拥有 decoder、normalized chart model、converter、projection reuse 与 parse-side cache 的 correctness。它只定义 parse truth 和转换合同：

- P1-H 负责导入、存储与 persisted metadata 一致性。
- P1-J 负责 gameplay runtime 和音频 hot path。
- P1-E 用真实谱面验证，不建立第二套 parse model。
- P1-L 消费 visual/control events，不回写 parser 私有解释。

外部格式基线统一查 [BMS_FORMAT_REFERENCE.md](../../other/BMS_FORMAT_REFERENCE.md)。

## 已完成阶段

| 阶段 | 结果 | 当前处理 |
| --- | --- | --- |
| K0 | 归线、术语与 authority 冻结 | 保持 |
| K1 | raw/typed 双层与 no-loss carrier | 保持；新增格式先 raw 后 typed |
| K2 | header/definition/channel coverage | 真实谱驱动补口 |
| K3 | timeline/control-event 语义 | parity gate 守护 |
| K4 | parse-once/project-many | 禁止 consumer ad hoc 重解析 |
| K5 | modless playable cache/invalidation | 禁止第二套 cache authority |
| K6 | focused validation 基线 | 按改动面扩展 |
| K7 | results summary consumer proof | 保持 |
| K8 | gauge history/auto-shift proof | 保持 |
| K9 | dedicated BMS→mania 转换合同 | 主体完成，wording/manual 待续 |
| K10 | converted-star 导入/读取加固 | 完成 |
| K11 | 转谱 BGM/autoplay 音频与 LN 尾对齐 | converter 主体完成，runtime 尾项归 P1-J |
| K12 | sample-only 对象不进入 mania difficulty | 完成 |

## 当前活动顺序

### 0. Skin V1 topology 前置修正（已闭合，2026-08-30）

1. `buildLaneKeysoundTimelines()` 已以 canonical `GetLaneCount()` 为唯一上界；5K/7K 最右键、9K 全 lane、14K K14/S2 的 visible、LN head/tail armed、invisible 与相邻 mine fixture 已锁住末端不丢失。
2. `BmsKeymodeResolution` 已冻结 parser-owned precedence、source/evidence、显式 override/纠正入口与稳定脱敏 diagnostic；`.pms/.bme`、P2/high channel 与 sparse chart 可追溯，无充分证据或冲突时 fail-closed。converter、manager/layout owner 只携带同一 resolution，不按对象最高 lane 或 layout 宽度二次猜测。
3. production keysound proof 已覆盖 native BMS 玩家/autoplay 与 converted Mania 的同一 shared store；Mirror/RANDOM/R-RANDOM/custom 搬移同一 exact permutation，S-RANDOM 稳定禁用不可搬移的 armed timeline，post-mod 对象、keysound 与 skin lookup 使用同一 `LaneId`。本切片未改 sample pool、判定或 binding。

本节仅标记 P1-A C3 的 P1-K Skin 前置 gate 闭合；后续 public surface、特殊谱与 projection/cache 治理仍开放，不能据此把整条 P1-K 标成完成。验证数字见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md) 与 [CHANGELOG.md](CHANGELOG.md)。

### 1. Public surface 收尾

1. 明确 BMS→mania 的入口 wording、source/target ruleset 与转换后限制。
2. 复核 Song Select、loading、results 的标题/难度/键数/star 展示使用同一 persisted/display authority。
3. 用人工清单证明 native BMS 与 converted-mania 的公开表面，不在 converter 内新增展示逻辑。

### 2. 真实特殊谱驱动的解析补口

只有同时具备原始谱、预期语义和失败 consumer 时才开新切片：

1. 先把未知内容保留进 raw carrier。
2. 再定义最薄 typed model 和 source order。
3. converter 投影与首个 consumer 分开提交。
4. 更新格式参考、约束、decoder/converter focused tests。
5. 最后跑 BMS full；涉及转谱时加 mania relevant focused。

优先候选是尚有真实失败证据的 special LN/control-flow/header family，不按“支持更多命令”泛化扩表。

### 3. Projection 与缓存治理

- 新 consumer 优先读取现有 projected working beatmap/persisted read-model。
- 若现有 projection 不足，先扩 authority DTO，再接 consumer；禁止 UI/runtime 自行重读 `.bms`。
- cache 必须绑定 source identity、mods 与 conversion version，并有失效测试。
- 性能改动必须由 profile 证明；解析正确性优先于常数因子优化。

## 改动纪律

1. **model-first**：parser 与多个 consumer 不得同刀扩张；先 model/contract，再逐 consumer。
2. **no-loss first**：暂时不理解的 header/channel 也必须保留原始信息和顺序。
3. **单一 authority**：decoder/converter 是语义真源，consumer 只投影。
4. **scorable 分离**：BGM/scratch sample-only 可参与播放，不能进入 score/star/max combo。
5. **metadata 共存**：共享 `RulesetData` 的 DTO 必须 round-trip 未知字段。
6. **display-only**：标题/难度清理不改源值、存库 MD5 或 parse truth。
7. **版本化**：行为改变影响 persisted projection 时显式 bump version，并提供旧库失效/重算路径。

## 验证矩阵

| 改动 | focused | 更宽 gate |
| --- | --- | --- |
| header/channel/raw carrier | decoder tests | BMS full |
| timeline/control events/LN | decoder + converter tests | BMS full + 代表谱 |
| BMS→mania | converter + mania relevant tests | BMS full + mania public surface |
| persisted metadata/version | resolver/import/cache tests | 旧库重算与共存检查 |
| projection consumer | owner consumer test | 不触发 second parse/conversion 的检查 |
| parser performance/cache | correctness + invalidation | profile 前后对比，不只看总耗时 |

当前全量基线统一看主线 STATUS，本页不维护数字。

## 明确不做

- 不在 P1-K 处理文件目录扫描、删除/失效或 Realm 生命周期；归 P1-H。
- 不在 P1-K 优化 gameplay sample pool、帧率或音频调度；归 P1-J。
- 不为单一 UI 需求复制 parser；先扩共享 projection。
- 不因 bmson/外部规范存在某能力就无样本、无 consumer 地预实现。
- 不把 Phase 3 在线 metadata 或 API 接入混进当前解析收尾。
