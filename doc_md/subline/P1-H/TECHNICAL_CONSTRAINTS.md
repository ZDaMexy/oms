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
11. 难度表来源变更后的 **既有 BMS 谱面 metadata 同步** 必须收口为 manager-owned contract；不得继续隐含依赖 importer 链上的 lazy `BmsTableMd5Index` 已先被构造，也不得要求用户通过重启或重导谱面来“补同步”。
12. `Settings -> 游戏模式 -> BMS -> 难度表` 与首次启动向导难度表页虽属 `P1-A` 共享产品表面，但它们触发的导入、刷新、启用、禁用、移除都必须与底层 manager 使用同一条 refresh / sync 语义，不得派生第二套结果合同。
13. `RefreshAllTables()` 不得继续吞掉单源失败后再向上层暴露纯成功语义；调用方必须能拿到结构化成功/失败结果，至少足以区分全成功、部分成功与全失败。
14. HTML wrapper -> `header.json` -> body 的 source identity 与 fallback naming 必须稳定；缺省 `name` 时不得 silently 退化成 `header` 这类瞬时文件名，也不得因此打乱 preset 认领或来源去重语义。
15. `Song Select` 分组与详情面板继续只消费 persisted beatmap metadata；不得在消费端临时增补 live lookup 来掩盖底层同步缺口。
16. correctness 未收口前，不得把异步化、分批执行或 busy/progress UI 当作替代方案；响应性优化只能后置到 metadata 同步与 refresh 结果合同都稳定之后。
17. `BmsFolderImporter` 的 reuse path（包括 internal / external rebuild 与 re-register 命中已有 beatmap set）也必须重新按当前 `BmsTableMd5Index` 套用 difficulty table metadata；不得沿用历史 persisted metadata 直接返回。
18. 当前难度表 chart identity 仍严格绑定原始 `.bms` / `.bme` / `.bml` / `.pms` 文件字节的 MD5；在没有明确迁移方案前，不得因为现场 `Unrated` 反馈就临时放宽匹配口径、改用模糊 fallback 或在 consumer 侧补 live lookup。
19. `BmsBeatmapLoader` / `BmsImportedBeatmapFactory` 返回给 `WorkingBeatmap.Beatmap` 的 BMS raw wrapper，必须继续复用首次 ruleset conversion 得到的 `ControlPointInfo` / `HitObjects` / `Breaks`；`SongSelect`、`BeatmapTitleWedge` 等 raw working beatmap consumer 必须能直接读取正确 BPM / 时长 / 时序统计，不得回退到默认 `60 BPM`，也不得要求显示层为此再做一次额外 conversion。
20. 难度表 metadata 回写必须经**注入的全局 `RealmAccess`**（与 game 共用同一实例），不得再 `new` 第二个 `RealmAccess` 做回写：第二实例的构造会触发 `cleanupPendingDeletions` 越权物删待删谱面集目录，并与全局实例的写事务/删除时序竞争。`BmsDifficultyTableManager.GetShared` 因此必须携带 `RealmAccess` 参数，由 settings / first-run / importer 传入全局实例。
21. carousel 集合订阅（`RealmDetachedBeatmapStore` → `All<BeatmapSetInfo>()`，无 keyPaths）不穿透 `Beatmaps.Metadata` 深层 link 属性 → 难度表回写不会即时刷新 carousel（**会话中途开关表需重启才反映到分组；启动恒正确**）。曾用 per-set `BeatmapSetInfo.DifficultyTableRevision` bump 强制 re-detach，但在大库（5.7 万谱 + 万级条目）下一次开关命中数千 set → 数千次 per-set re-detach → UI 卡死 1~2 分钟（实测），**已撤销**（字段保留 schema 55，闲置）。中途即时刷新的正确方向是分组改走内存索引 live lookup + 一次性 re-filter（不 re-detach），留作后续；当前**不得恢复 per-set bump**。
22. **`BeatmapMetadata.RulesetData` 单列被多个 BMS 子系统共享**：osu.Game 的 `BmsPersistedMetadataData`（转谱星数 `converted_star_ratings`）与 BMS 的 `BmsBeatmapMetadataData`（`difficulty_table_entries` / `chart_filter_stats`）都序列化进同一列。因 `SetRulesetData<T>` 是整体覆盖写、Newtonsoft 默认丢弃未知字段，任何写方**必须**用 `[JsonExtensionData]` 往返保留自己不建模的外部字段，且 `IsEmpty`/置空判断须计入扩展字段（否则置空会 `SetRulesetData(null)` 连带抹掉对方）。违反会整段抹掉对方数据——曾导致转谱星数重算冲掉全部难度表 entries（全 Unrated）、反向冲掉星数触发反复重算的破坏性 ping-pong。新增任何 RulesetData payload 必须遵守此约束或统一容器。
