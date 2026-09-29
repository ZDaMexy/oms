# P1-H 技术约束：存储拓扑支撑线

以下为行为合同；当前实现和验收边界见 [STATUS](DEVELOPMENT_STATUS.md)。谱库索引失效与用户明确物删必须分离；字符串路径检查和目录链接拒绝不构成皮肤 G1 的 held identity 或原子恢复证明。

1. 不得破坏 `chartbms/`、`chartmania/`、`portable.ini -> data/` 与本地优先的数据根约束。
2. 改变导入路径、数据根、外部谱库扫描或重扫策略时，同次同步本线实际受影响的状态、计划、约束和验证记录；涉及发行保存/更新行为时更新 `../../other/RELEASE.md`，只有影响全局优先级、release gate 或硬约束时才向 mainline 回写摘要与链接，不机械刷新四件套日期。
3. `扫描外部谱库` 与 `扫描内部谱库` 必须保持职责分离：前者只处理已注册外部根，后者只重建当前数据根下 `chartbms/` / `chartmania/` 的 managed roots。
4. 外部与内部两侧都必须显式保留 `重建` 与 `增量` 两种模式：`重建` 允许重走全部候选目录，`增量` 只允许补导当前没有 active `FilesystemStoragePath` 记录的目录；active 必须同时排除 `DeletePending` 与 `FilesystemUnavailable`。
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
21. 难度表整批持久化事务完成后，经全局 `RealmAccess` 发一次谱面 ID 通知；当前列表合并待处理通知并读取最新已提交 `RulesetDataJson`，更新现有快照后只做一次重新筛选/分组及可见谱卡重绑，保留仍可选的当前谱面身份。订阅必须早于初始列表快照，不能遗漏初始化期间的批次。不得恢复 per-set `DifficultyTableRevision` bump 或在新增通知路径逐 set re-detach；原生 Realm 写通知仍可能产生集合更新，本轮不宣称阻止所有原生通知。历史大库卡死证据见 [CHANGELOG](CHANGELOG.md) 2026-05-31。这里只消费已持久化 metadata，不改为实时难度表 lookup；真实大库响应仍须单独验收。
22. **`BeatmapMetadata.RulesetData` 单列被多个 BMS 子系统共享**：osu.Game 的 `BmsPersistedMetadataData`（转谱星数 `converted_star_ratings`）与 BMS 的 `BmsBeatmapMetadataData`（`difficulty_table_entries` / `chart_filter_stats`）都序列化进同一列。因 `SetRulesetData<T>` 是整体覆盖写、Newtonsoft 默认丢弃未知字段，任何写方**必须**用 `[JsonExtensionData]` 往返保留自己不建模的外部字段，且 `IsEmpty`/置空判断须计入扩展字段（否则置空会 `SetRulesetData(null)` 连带抹掉对方）。违反会整段抹掉对方数据——曾导致转谱星数重算冲掉全部难度表 entries（全 Unrated）、反向冲掉星数触发反复重算的破坏性 ping-pong。新增任何 RulesetData payload 必须遵守此约束或统一容器。
23. schema 58 `FilesystemUnavailable` 表示来源当前不参与选歌索引，不是物删意图。缺失、解除注册、同目录内容变化只能改变此状态，不写 `DeletePending`、不移除旧 set/beatmap/metadata、不得解除历史成绩或收藏关系。可用性查询、列表与扫描 active 谓词一致排除 unavailable；原路径原内容重新出现时复用旧身份并恢复可见。
24. 同 hash 不构成目录所有权或替换授权。BMS/mania 注册按来源权限和规范化目录选择同内容记录；不同目录保留独立来源，同目录内容变化只隐去本 ruleset 的旧版本，不影响混合目录中的另一模式。查找同目录冲突应在 Realm 按路径和权限筛选，不在每个导入目录拉取全库。谱文件改名后仍命中同集合 hash 的复用记录必须同步 `LocalFilePath`，不得返回指向旧文件名的记录；多文件改名导致排序及集合 hash 改变时按内容版本变化处理。
25. 重建只有完整遍历且扫描错误数为零、未取消时才能收敛本根缺失索引；离线根、向扫描器上报的遍历/读取/导入失败不得触发缺失回收。Register 候选目录没有任何有效谱面必须报错，不能让空结果经 Task 降型伪装为成功；普通 Import 的空结果与用户通知合同保持。有有效谱面时，既有重复/无效文件跳过及解析警告不自动升级为整目录失败。mania 分类读取的占用/权限异常必须上报扫描错误，不能吞成非 mania。此前成功的单目录更新不整体回滚，不能把此行为描述为全库事务。增量只补新，不承担已索引目录内容更新。
26. 外部根解除须与扫描串行协调，避免删除配置后仍有旧扫描重新显示歌曲；同类型剩余注册根覆盖的记录保持可见，包括历史父子重叠配置。最后覆盖解除只隐去索引，绝不删除 external 源文件。managed 用户明确删除为独立入口，不把扫描失效并入其物删流程。
27. 路径规范化保留盘根语义，统一 FullPath、尾分隔符和大小写比较。新根拒绝重复及父子重叠；新注册和扫描旧根均拒绝根及祖先 reparse 目录，遍历子目录链接或检查失败计错并跳过，阻止缺失回收。这里没有 held no-follow capture，不将路径字符串作为皮肤 mutation authority。
28. 旧版本已经存在的 `DeletePending` 不能自动解释为扫描失效并复活；需区分用户主动删除和旧导入替换证据。跨路径改名建立新来源而保留旧路径历史，不宣称自动跨路径迁移成绩身份。
