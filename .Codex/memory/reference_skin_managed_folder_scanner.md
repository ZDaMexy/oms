---
name: reference_skin_managed_folder_scanner
description: schema 57 exact owner、Observed/Valid 分离与完整扫描才可负向 reconcile 的地雷
metadata:
  node_type: memory
  type: reference
---

# Managed skin scanner 地雷

现行扫描/owner/启动合同见 [P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md) 的 G1 章节；能力边界读 [STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)。测试只用隔离 Realm/临时 Windows 根，不扫描生产 chartskin。

## 数据保护先看 record owner

- schema 57 的 FilesystemStorageAuthorityOwner 是 opaque record metadata，不是文件能力。旧记录迁移后保持 null；null/unknown/foreign、普通 .osk、external、orphan blob 都不 claim、去重或清理。
- exact scanner token 只管理由 scanner 创建或 one-shot publisher 经同等级注册/capsule/Realm 冲突门交接的合法 direct child。直接写 token 不能制造该来源。
- 同 path 有 foreign/null/多记录/混合 storage/protected/fixed-ID 时整组零 mutation。合法 handed-off record 是既有记录，scanner 不能再发第二条。

## Observed 与 Valid 不能合表

| 集合 | 含义 |
| --- | --- |
| ObservedPaths | stable direct-child inventory 中所有可表达为合法 managed path 的名字，含文件、reparse、忙写、坏包、缺 ini/metadata 的目录 |
| ValidDiscoveries | no-follow capture、capsule 与 metadata gate 均成功的包；必须也是 observed |

- observed-invalid 只表示“还存在，不能负向删除”，不授予导入；它只保护 exact path。root inventory 完整稳定时仍可清理别处真正 absent 的 exact-own record。
- duplicate/case collision、valid-not-observed、非法 path、不完整 snapshot 必须拒绝，不发布 partial 结果。
- 只有完整稳定 scan 可在单 Realm 事务 add/update/revive，以及对唯一合法 exact-own 且不在 Observed 的记录 soft-delete；仅改 DeletePending，不删目录。
- 取消在 apply/commit 线性化点前出现则整笔回滚；退出需等事务结束/回滚再释放 Realm。

## Capture 与协调的诊断点

- root inventory 与每包 capture 共用同一 held chartskin authority。前者证明 direct-child membership 稳定，后者证明包内 tree/capsule，两者不能互替。
- 新建包后 NTFS LastWriteTime/ChangeTime 可能延迟落稳；只对已定义 retryable identity/inventory race 做有界、可取消完整 session 重试，不无限重试或提交失败轮的部分数据。
- scanGate 只防同实例重入；跨 scanner/selection/mutation/recovery 用共享 coordinator，从 discovery 持到 Realm commit。snapshot→commit 之间不能让 mutation 插入。
- startup 在同一 typed sequence 先幂等 recovery、后一次 scanner。有效未决 journal 冻结相关 path；invalid/unknown/IO 冻结 namespace。add/update/revive 与 negative cleanup 都查冻结，不能将半成品当新包或缺失。
- Realm notification 已能刷新 dropdown，不加第二 UI refresh，也不自动选择。scanner 不消费 publication plan，不执行 physical move/delete。
- 启动后新增 direct child 需重启发现；已登记 current 的原位编辑走 Settings manual Reload，不能称 scanner 为 watcher。
- shutdown/callback 与 configured selection retry 见 [[reference_skin_managed_folder_selection]]、[[reference_skin_atomic_reload_detach]]。

安全诊断只含 reason/计数，不输出 root/name/skin metadata/revision 或 native 异常正文。native 捕获见 [[reference_skin_windows_handle_capture]]，物理恢复见 [[reference_skin_managed_folder_mutation_foundation]]。
