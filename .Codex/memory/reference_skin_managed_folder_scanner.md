# managed skin folder scanner（schema 57）

> 快速召回 `chartskin/` 自动发现、Realm 归属与启动生命周期的安全边界。当前产品事实仍以 `doc_md/subline/P1-A` 为准。

## 归属与 Realm reconcile

- schema 57 新增 nullable `SkinInfo.FilesystemStorageAuthorityOwner`。它只是 opaque record ownership metadata，不是文件系统读取、删除或改名 capability。
- 旧记录迁移后 owner 必须保持 `null`；scanner 不得 claim、改写、去重或清理 null、未知 token、另一 authority、普通 `.osk`、external 或 orphan blob。
- managed scanner 的 exact token 是 `oms.skin.managed-folder.scanner.v1`。只维护这个 token 创建、结构仍合法且路径位于 `chartskin/<one direct child>` 的记录。
- reconcile 必须在单一 Realm 事务中完成。有效新包可新增；exact-own 同路径记录可更新 metadata/content revision 或 revive；同路径出现 null/foreign/多记录/混合 storage/protected/fixed-ID 时整组冲突且零 mutation。
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

- `OsuGame.LoadComplete()` 最后在线程池启动一次扫描；不得绑定 update scheduler。`OsuGame.Dispose()` 必须先 cancel + join，再进入 `OsuGameBase.Dispose()` 释放 Realm。
- 设置页已有 Realm `SkinInfo` notification → `GetAllUsableSkinsAsync()` → dropdown 刷新链；scanner 不需要第二套 UI refresh，也不会自动切换当前皮肤。
- 这是一次启动扫描，不是 watcher 或热重载。启动后原位编辑、新 revision publication、全 consumer detach、managed rename/delete/import、external registration/capture 仍是后续独立 gate。
- 测试只用 fake、隔离 Realm/临时 Windows 根和 headless lifecycle；不要为验证 scanner 启动可见 GUI，也不要触碰生产 `chartskin/`。
