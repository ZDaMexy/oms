# P1-H 开发计划：存储拓扑支撑线

> 最后更新：2026-04-23
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
7. 继续补齐删除 / 失效语义、path identity dedup 与重扫策略。
8. 把影响导入、发布或本地数据根的结论同步到主线与发行文档。