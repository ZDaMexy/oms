# P1-H 开发计划：存储拓扑支撑线

> 最后更新：2026-04-29
> 主线总规划见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。

## 子线目标

- 维持 `chartbms/`、`chartmania/`、便携模式，以及“外部/内部谱库各自的重建+增量扫描”这条存储拓扑支撑线稳定。
- 为后续导入、发布与本地优先策略提供稳定地基。

## 当前执行顺序

1. 维持现有存储拓扑不回退。
2. 把 `扫描外部谱库` 与 `扫描内部谱库` 的职责持续分离：前者只面向已注册外部根，后者只面向当前数据根下 `chartbms/` / `chartmania/` 的托管目录。
3. 维持四模式语义清晰：`重建` 必须重走全部候选目录，`增量` 只补导当前没有 active `FilesystemStoragePath` 记录的目录。
4. `内部谱库` 的两种扫描必须继续停留在独立 subsection，不得重新与外部根管理混放。
5. 允许 `ExternalLibrarySettings` 作为首次启动向导等共享产品表面的复用入口，但复用不改变扫描语义，也不新增第二套 storage contract。
6. 维持 managed-root 子目录判定稳定，不因尾部分隔符、大小写或同目录比较而回退；相关修改必须同步保留 focused regression coverage。
7. 把难度表来源变更到既有谱面 metadata / Song Select read-model 的一致性收口为 manager-owned contract，不再依赖 importer 链上的 lazy side effect。
8. `RefreshAll` 必须向 settings / first-run 返回真实结果合同；部分失败不能再伪装成全成功。
9. correctness 收口完成前，不得把“异步化 / 大库响应性优化”当成替代方案；响应性优化只能后置到一致性问题解决之后。
10. 维持 raw working beatmap consumer 的 timing/statistics authority 稳定；BMS loader 返回给 `WorkingBeatmap.Beatmap` 的 wrapper 必须直接携带已转换的 `ControlPointInfo` / `HitObjects` / `Breaks`，避免 Song Select 等显示面回退到默认 `60 BPM` 或再做一次额外 ruleset conversion。
11. 继续补齐删除 / 失效语义、path identity dedup 与重扫策略。
12. 把影响导入、发布或本地数据根的结论同步到主线与发行文档。

## 当前新增专题：BMS 难度表一致性与刷新合同收口

该专题不重开 `1.13 难度表来源管理`、`1.14 MD5 匹配` 或 `1.15 Song Select 表分组`，也不新建一套独立子线文档。它继续挂在 `P1-H` 下，原因是 authority 在 difficulty-table source cache、persisted beatmap metadata 与 Song Select read-model 的一致性，而不是 settings 或 first-run 的产品表面。

### 专题目标

1. 把“难度表来源变更后，既有 BMS 谱面 metadata 立即更新”收口为正式合同。
2. 把 `RefreshAll` 的结果语义做实，不再吞掉失败并误报成功。
3. 稳定 wrapper / header / body 的 source identity、fallback naming 与 preset 认领语义。
4. 只在 correctness 收口后，再做大库响应性优化。

### 主归线与从属影响

1. 主归属固定为 `P1-H`；因为这是 persisted data / read-model 的一致性问题。
2. `Settings -> 游戏模式 -> BMS -> 难度表` 与首次启动向导难度表页继续只作为 `P1-A` 的共享产品表面，不拥有第二套 refresh / sync 语义。
3. `Song Select` 与 `BmsNoteDistributionGraph` 只消费 beatmap metadata；本专题不把它们重新定义成 authority。

### 执行批次

1. **批次一：metadata 同步收口**
   将已存在 BMS 谱面的 difficulty-table metadata bulk update 提升为 manager 的显式职责；导入、单源刷新、全量刷新、启用、禁用、移除都必须走同一条同步路径。
2. **批次二：RefreshAll 结果合同**
   为 `RefreshAll` 增加结构化返回值或等价的结果模型，让 settings / first-run 能准确显示总数、成功数、失败数与失败来源。
3. **批次三：source identity / fallback naming**
   稳定 HTML wrapper -> `header.json` -> body 的 fallback/source name 传递；缺省 `name` 时不得退化成临时文件名。
4. **批次四：响应性与交互打磨**
   correctness 绿线后，再评估把全量 metadata 回写改为后台任务、分批更新或等价 busy/progress 表达。
5. **批次五：reuse 自愈与现场诊断边界**
   internal / external rebuild 或 re-register 命中已有 beatmap set 时，也必须重新按当前 table index 套用 metadata；若现场仍见 `Unrated`，先确认**重启后是否仍 `Unrated`**——重启后正常属 carousel 中途未刷新（已知限制，需重启反映；per-set `DifficultyTableRevision` bump 因大库卡死已撤），重启后仍 `Unrated` 才查 `RulesetData` 字段是否被其它子系统覆盖（CONSTRAINTS #22）或原始 `.bms` 字节 MD5 差异，而不是怀疑 Song Select consumer 或临时放宽匹配规则。

### 明确不做

1. 不新开 `mini` 或平行 `P1-*` 子线。
2. 不借此启动远端难度表后台同步、OMS backend 镜像或定时刷新。
3. 不把难度表来源管理泛化成跨 ruleset 通用框架。
4. 不在 correctness 尚未收口前，优先做复杂管理 UI 或来源运营功能。

### 完成条件

1. manager-only 来源变更可在当前会话内立即更新既有谱面 metadata、Song Select 分组与详情，不依赖 importer 已先构造、重启或重导。
2. `RefreshAll` 在存在失败时会准确反馈结果，不再显示纯成功提示。
3. 本地与远端 wrapper/header 缺省命名链路都保持稳定 source identity。
4. manager-only source mutation、settings / first-run surface 与 Song Select 消费面都具备 focused regression coverage。
5. rebuild / reuse 命中旧 beatmap set 时不会沿用历史空 metadata；若后续仍有 `Unrated` 反馈，先按「重启后是否仍 `Unrated`」二分——重启后正常归 carousel 中途未刷新（已知限制），重启后仍 `Unrated` 才查 `RulesetData` 字段覆盖（CONSTRAINTS #22）或现场 MD5 差异，而非主链一致性缺口。
