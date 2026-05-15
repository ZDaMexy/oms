# P1-J 变更日志：BMS gameplay runtime 性能与音频时序治理

> 本文件记录 `P1-J` 相关的验证通过变更，按时间倒序排列。
> 当前进度见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，执行规划见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

---

## 2026-05-16

### 文档：新建 P1-J 子线并冻结首轮 hot-path 优化范围

- 已建立 `P1-J` 四件套，正式把 BMS gameplay runtime 的 keysound timing、lane/order hot path、sample allocation 与 live channel resize 安全合同独立归线。
- 当前已明确判定：该专题不并入 `P1-C` 或 `P1-E`；`P1-C` 继续拥有判定/反馈语义，`P1-E` 继续拥有真实谱面验校，而 `P1-J` 单独拥有 shared gameplay/audio hot path 的优化 authority。
- 最新只读审查已收口四类首轮风险：shared `BmsKeysoundStore` 的无条件 `Schedule()` 播放延后、`BmsLane` / `BmsOrderedHitPolicy` 的容器枚举热路径、重复 sample 数组分配，以及 `KeysoundConcurrentChannels` live 改值 rebuild-all 可能切断当前音频。
- 本轮仅完成文档治理与归线规划，无生产代码改动、无新增测试执行。