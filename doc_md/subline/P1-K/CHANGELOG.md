# P1-K 变更日志：BMS 解析链路治理

> 本文件记录 `P1-K` 相关的验证通过变更，按时间倒序排列。
> 当前进度见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，执行规划见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

---

## 2026-05-22

### 文档：补齐 P1-K 的依赖与回退边界

- `DEVELOPMENT_PLAN.md` 现新增“依赖与回退边界”表，把 `K1-A` 到 `K4-A` 的进入前提、失败信号、允许回退与明确禁止项固定下来。
- `TECHNICAL_CONSTRAINTS.md` 现新增回退约束，明确失败后只能收缩新增暴露面，不能把 no-loss carrier、source line order 或 focused regression 一并删掉。
- `DEVELOPMENT_STATUS.md` 现明确记录：当前文档层面已经足以独立驱动 `K1-A` 开工，剩余开放项属于实现期决策而非规划缺口。

### 文档：把 P1-K 扩写成可直接开工的执行包

- `P1-K` 的 `DEVELOPMENT_PLAN.md` 现已补齐文件级切片图、首轮开工顺序、focused test 落点、推荐验证命令与“何时算可以直接开工”的进入条件，不再只是方向性规划。
- `TECHNICAL_CONSTRAINTS.md` 现新增切片边界约束，明确 `K1-K3` 首轮只允许改 in-memory parse chain，`K4` 之后才触碰 projection reuse 与 importer/raw-wrapper consumer。
- `DEVELOPMENT_STATUS.md` 现新增首轮开工包，把 `K1-A` 到 `K4-A` 的主文件、目标与每刀验证顺序固定下来，后续可以直接照文档执行。

### 文档：新建 P1-K 子线并冻结 BMS 解析链路治理范围

- 已新建 `P1-K` 四件套，并把 **BMS 解析链路治理** 正式归入 Phase 1.x 子线编排；主 authority 明确落在 decoder、normalized chart model、converter 语义、projection reuse 与 parse-side cache。
- 主线总规划、主线状态页、主线变更日志与子线索引已同步加入 `P1-K`，首轮执行顺序冻结为：`raw/typed 双层模型冻结` → `header/definition/channel no-loss coverage` → `timeline/control-event semantics` → `parse-once/project-many 复用` → `focused validation 与缓存边界`。
- 本轮同时把当前 parse-chain 的主要 gap 写入子线基线：`SCROLLxx/SC` 未进入模型、signed BPM typed surface 不可表示、duplicate channel line 未 compound、同拍位 `BPM/STOP/object` 顺序未冻结，以及 BGA layer / mine / invisible note 仍缺最薄 typed slot。
- 本轮仅完成文档规划与主线编排，无生产代码改动、无新增测试执行；代码与验证基线继续沿用主线同日 `788/788` 快照。
