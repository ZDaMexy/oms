# P1-H 技术约束：存储拓扑支撑线

1. 不得破坏 `chartbms/`、`chartmania/`、`portable.ini -> data/` 与本地优先的数据根约束。
2. 任何改变导入路径、数据根、外部谱库扫描或重扫策略的改动，都必须同步更新本目录四件套、`../../mainline/` 与 `../../other/RELEASE.md`。
3. `扫描外部谱库` 与 `扫描内部谱库` 必须保持职责分离：前者只处理已注册外部根，后者只重建当前数据根下 `chartbms/` / `chartmania/` 的 managed roots。
4. 外部与内部两侧都必须显式保留 `重建` 与 `增量` 两种模式：`重建` 允许重走全部候选目录，`增量` 只允许补导当前没有 active `FilesystemStoragePath` 记录的目录。
5. `扫描内部谱库（重建）` 与 `扫描内部谱库（增量）` 必须继续停留在独立的 `内部谱库` subsection，不得重新与外部根管理混放。
6. 托管根的父子目录判定必须先归一化尾部分隔符，再做同目录/父目录链比较；在 Windows 上需继续保持大小写不敏感语义，避免合法的 managed 子目录因路径表示差异被拒绝。
7. `RegisterManagedDirectory()` 路径必须继续写入相对 `FilesystemStoragePath`（如 `chartbms/...`、`chartmania/...`），不得回退成外部绝对路径语义。
8. 任何触碰 managed-root 判定或补扫入口的改动，都必须保留 focused regression coverage，至少覆盖“child-under-parent”和“same-directory”两条路径，并为模式语义保留“增量跳过已索引目录 / 重建不受增量过滤影响”的 scanner 回归。
9. `ExternalLibrarySettings` / `InternalLibrarySettings` 可被 `Settings -> Maintenance` 之外的共享产品表面（如首次启动向导）复用，但不得派生出第二套扫描按钮语义、路径解释或状态统计口径。
10. 不得让存储拓扑回退为需要重新导入或依赖在线迁移的模型。