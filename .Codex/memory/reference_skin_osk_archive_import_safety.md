---
name: reference_skin_osk_archive_import_safety
description: .osk 早期 archive 准入、实际字节预算与 same-hash/asymmetric baseline 零残留回滚地雷
metadata:
  node_type: memory
  type: reference
---

# Ordinary .osk import safety 地雷

完整预算、类型与格式准入见 [P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md) 的 ordinary .osk ingress；当前证据读 [STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)。ordinary .osk 仍是 hash-backed Realm package，不接 managed-copy stager。

## 为什么 Populate / texture 阶段检查太晚

历史失败链：ImportTask.GetReader 构造 reader → 枚举/缩短 common prefix → fast hash → Files.Add → model Realm 事务。后段拒绝时前段可能已分配大内存、写 blob；Realm rollback 不能撤销全部文件副作用。

- 通用 reader 曾在 entry.Size>0 时 long→int 一次分配，size=0 则 ReadAllRemainingBytesToArray。unknown/data-descriptor/欺骗 header 不能靠 declared size 获得无限读取。
- skin-scoped pre-open 先限 compressed length，nonseekable 用 bounded/cancellable/delete-on-close spool；自己的 bounded metadata parser 在通用 reader 无界物化前冻结 EOCD/CEN/local metadata。
- entry/name/type/declared budget gate 完成前不暴露 Filenames，不 GetStream/hash/Files.Add/model；实际 copy/hash/CRC 流继续硬计 actual bytes、aggregate ratio 与 cancellation。
- name 同时查 raw 与 common-prefix-shortened 后的 slash/NFC/Windows case-fold、file-directory conflict、traversal/ADS/device/trailing-dot-space。只查原名会漏掉缩短后的碰撞。
- skininfo.json 只读最小 DTO、经 closed compatibility map；不能 Type.GetType(raw) 激活不可信 CLR 类型。archive 预算与 runtime texture 预算是两层，不能替代。
- 失败/取消保留原 archive、无新增 model/reference/本次孤儿；共享 blob 不误删。成功才沿原 ShouldDeleteArchive 语义。Shift-JIS、公共顶层目录、Skin.InI 和历史合法包保留回归。

## Receipt 与 same-hash 并发

- opt-in receipt 按 RealmAccess + storage identity + hash 组成 participant group；并发 same-hash scope共同参与。active scope/add 没结束不能 finalize，有成功/unscoped writer、usage/backlink 或新 generation 就保留共享资产。
- baseline Realm record 与 physical blob 独立记账，不能只用同 hash 或单一 created flag：

| 导入前 | 本 group 回滚最多可处理 |
| --- | --- |
| record 有、blob 缺 | 仅本 group 写出的 blob |
| blob 有、record 缺 | 仅本 group 创建且仍零引用的 record |
| 两者都缺 | 分别按各自 ownership 处理 |
| 两者都有 | 两者都保留 |

- 不调用全局 Cleanup。每 hash rollback 异常隔离，不能阻止其它 group 收口；失败 group 可由后续同 hash scope按证据重试。
- 锁顺序 Realm transaction→import group lock；参与清单锁内摘取、锁外逐 group finalize。物理删除在 Realm 阶段完成且 generation仍 exact 后执行；新 add/finalizer 竞态要复核 active计数、generation、group identity。

## 有意义的回归面

- size=0 但有内容、unknown/data-descriptor、高 ratio、declared/actual 不一致、CRC/truncated、取消、fast-hash失败、N次 Files.Add 后失败。
- same-hash多 participant 与上表两种 asymmetric baseline；不能只生成小正常 zip。
- 除 typed reason，还查原 archive 保留、Realm模型/引用未增、无本次孤儿、队列与后续合法导入仍可用。日志不能泄露路径/entry 原文。
- 共享 reader/importer/file-store hook 的修改必须查 beatmap/score 默认路径；skin opt-in 不等于全局换 reader。

导入 receipt 不授予 reload/mutation authority；current .osk 的 fresh declaration/blob capture、fallback+detach 后 soft-delete 见 [[reference_skin_atomic_reload_detach]]。
