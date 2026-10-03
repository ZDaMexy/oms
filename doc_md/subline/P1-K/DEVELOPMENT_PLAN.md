# P1-K 当前计划：BMS 解析与转换治理

> 最后核对：2026-10-03（已完成表与 C3 实施史回归状态/历史；保留公开表面与特殊谱门）
> 当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，稳定合同见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)，逐刀历史见 [CHANGELOG.md](CHANGELOG.md)。

## 子线职责

P1-K 拥有 decoder、normalized chart model、converter、projection reuse 与 parse-side cache 的 correctness。它只定义 parse truth 和转换合同：

- P1-H 负责导入、存储与 persisted metadata 一致性。
- P1-J 负责 gameplay runtime 和音频 hot path。
- P1-E 用真实谱面验证，不建立第二套 parse model。
- P1-L 消费 visual/control events，不回写 parser 私有解释。

外部格式基线统一查 [BMS_FORMAT_REFERENCE.md](../../other/BMS_FORMAT_REFERENCE.md)。

## 后续输入

既有解析/转换、C3 keymode/lane 前置、TOTAL 和手动 LN shared-store 的实现与验证统一见 [STATUS](DEVELOPMENT_STATUS.md)及 [CHANGELOG](CHANGELOG.md)，不重复开发。保持 [CONSTRAINTS](TECHNICAL_CONSTRAINTS.md) 的 raw/typed、parse-once、缓存、sample-only 与 metadata 共存边界；TOTAL 与 [P1-C](../P1-C/DEVELOPMENT_PLAN.md)共同守住 loader→gauge→results 验证，真实听感门归 [P1-J](../P1-J/DEVELOPMENT_PLAN.md)。

## 当前活动顺序

### 1. Public surface 收尾

1. 明确 BMS→mania 的入口 wording、source/target ruleset 与转换后限制。
2. 复核 Song Select、loading、results 的标题/难度/键数/star 展示使用同一 persisted/display authority。
3. 用人工清单证明 native BMS 与 converted-mania 的公开表面，不在 converter 内新增展示逻辑。
4. 普通 `ICustomBeatmapLoader` 仍不提供用户 override，模糊 sparse 谱证据不足时继续 fail-closed。若最终产品要接受此类谱，为现有 decoder seam 补 authoritative importer/UI caller 及真实导入回归，不用 layout 猜测代替；已闭合的 C3 前置不代表用户纠正入口已交付。

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
