---
name: reference_skin_managed_folder_scanner
description: schema 57 exact-owner受管目录启动发现、Observed/Valid、Realm reconcile及mutation/recovery协调地雷
metadata:
  node_type: memory
  type: reference
---

# managed skin folder scanner（schema 57）

> 快速召回 `chartskin/` 自动发现、Realm 归属与启动生命周期的安全边界。当前产品事实以 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md) 为准；前后authority链见[[reference_skin_filesystem_authority_preflight]]、[[reference_skin_windows_handle_capture]]、[[reference_skin_managed_folder_selection]]与[[reference_skin_managed_folder_mutation_foundation]]。

## 归属与 Realm reconcile

- schema 57 新增 nullable `SkinInfo.FilesystemStorageAuthorityOwner`。它只是 opaque record ownership metadata，不是文件系统读取、删除或改名 capability。
- 旧记录迁移后 owner 必须保持 `null`；scanner 不得 claim、改写、去重或清理 null、未知 token、另一 authority、普通 `.osk`、external 或 orphan blob。
- managed scanner 的 exact token 是 `oms.skin.managed-folder.scanner.v1`。只维护owner为该exact token、结构仍合法、路径位于`chartskin/<one direct child>`，且由scanner创建或由staged-import one-shot publisher通过scanner同等级注册/capsule/Realm冲突门合法交接的记录；直接写token不构成合法来源。
- reconcile 必须在单一 Realm 事务中完成。有效新包可新增；exact-own 同路径记录可更新 metadata/content revision 或 revive；同路径出现null/foreign/多记录/混合 storage/protected/fixed-ID时整组冲突且零mutation。staged-import publisher完成exact handoff后，scanner必须把该单一record视为自己的既有记录而不是再发布第二条。
- negative reconcile 只能发生在**完整且稳定**的扫描：exact-own、唯一、结构合法的记录若不在 `ObservedPaths` 才 soft-delete。只改 `DeletePending`，绝不删除磁盘目录。
- 取消在事务 apply/commit 线性化点前被观察时必须让 Realm 回滚全部新增、更新与 soft-delete；退出会等待已进入的原子事务结束/回滚后再释放 Realm。

## Observed 与 Valid 必须分离

- `ObservedPaths` 是稳定 direct-child inventory 中所有可表达为合法 managed 相对路径的名字，即使实体是普通文件、reparse、忙写目录、坏包、缺 `skin.ini` 或 metadata 无效。
- `ValidDiscoveries` 只能包含 native no-follow capture 成功、immutable capsule 完整且根 `skin.ini` metadata 可安全读取的包；每个 valid path 必须也在 observed。
- observed-but-invalid 的意义是“此路径仍存在，不能据未见删除旧记录”，不是“可以导入”。把 Observed 与 Valid 合成一张表会重现异常期 scanner 误清理风险。
- duplicate/case-collision、valid-not-observed、非法 direct-child path 或不完整 snapshot 都 fail-closed，Realm 零写入。

## Windows authority 与稳定性

- discovery session 从物理本地 volume handle 经 data-root segments 固定到同一个 held `chartskin` handle；候选枚举、相对 no-follow capture 和最终 inventory/authority-link 复验必须共享该根。
- 每个有效包由 `CaptureObservedChild()` 独立完成包内 identity/metadata/inventory、hardlink/reparse/alias、预算和 immutable capsule gate；最终 root inventory 负责 direct-child membership/metadata 稳定，不替代包内 capture。
- NTFS 在刚创建包后可能延迟落稳目录 `LastWriteTime/ChangeTime`。生产 discovery 对 retryable identity/inventory race 只做有界、可取消的完整 session 重试；不得无限重试，也不得在失败轮次发布 partial snapshot。
- source/result/exception 的安全字符串只允许 reason 与计数；data root、目录名、skin metadata、content revision 和 native exception 正文不得进入日志或 `ToString()`。

## 启动与当前产品边界

- `SkinManager`构造期先做durable mutation recovery；`OsuGame.LoadComplete()`最后在线程池以typed `StartupSequence`外层lease连续执行幂等recovery→一次扫描，不得绑定update scheduler。`OsuGame.Dispose()`必须在`OsuGameBase.Dispose()`释放Realm前统一cancel + synchronous join startup scanner、rename/staged-import worker及selection capture-scheduling/contention worker。pending queued completion由shutdown或scheduler callback恰好一方claim/reap capsule与CTS，晚到callback只no-op；queued completion本身不是可join worker，也不得为它等待update scheduler。细节见[[reference_skin_managed_folder_selection]]。
- 设置页已有 Realm `SkinInfo` notification → `GetAllUsableSkinsAsync()` → dropdown 刷新链；scanner 不需要第二套 UI refresh，也不会自动切换当前皮肤。
- 这是一次启动扫描，不是watcher或热重载。directory-only rename与fixed-source staged import已有独立internal production operation，但scanner本身不执行move/import，也不消费`SkinManagedFolderNewRecordPublicationPlan`；启动后原位编辑、新revision publication、全consumer detach、managed delete、所有相关UI及external registration/capture仍是后续独立gate。
- 测试只用 fake、隔离 Realm/临时 Windows 根和 headless lifecycle；不要为验证 scanner 启动可见 GUI，也不要触碰生产 `chartskin/`。

## 与 mutation foundation 的协调地雷

- `scanGate`仍只负责同一scanner实例去重；真正跨scanner/selection/mutation/recovery的authority是共享`SkinManagedFolderOperationCoordinator`。独立scanner从discovery开始持有short scope直到Realm事务返回；startup lifecycle则在typed `StartupSequence`外层嵌套该short scope，因此snapshot→Realm reconcile不再允许进程内mutation插入，并可让已先开始的configured selection辨识exact startup completion后异步fresh retry。
- 启动先恢复后scanner；有效未决journal冻结其source/target，invalid/unknown/IO冻结整个namespace。reconcile在规划valid add/update/revive和negative soft-delete时都必须查询冻结状态，不能把半成品解释成普通新增或缺失。
- 公共线性化与按kind恢复地基已被directory-only rename及fixed-source staged import直接消费；scanner从native snapshot到Realm commit全程串在同一coordinator内，不能与one-shot publisher竞争或把half-applied target解释成普通新增。managed delete仍须独立闭合，scanner lease绝不能被当作任何物理写能力。
- staged import不自动选择；Realm notification可以刷新选择列表，但不能替换current pair或取消无关pending selection。本切没有GUI/视觉签收，不能把scanner record可见性写成玩家完成gate。
