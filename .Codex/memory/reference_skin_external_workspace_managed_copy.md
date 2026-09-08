---
name: reference_skin_external_workspace_managed_copy
description: external selection 与 reload 的不同写边界、pure-Realm unregister、ManagedCopy 持有证明地雷
metadata:
  node_type: memory
  type: reference
---

# External Workspace / ManagedCopy 地雷

产品入口与完整安全门见 [P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)，当前状态读 [STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)。external 永久只读；resolver 词法 request、service-owner token、digest/record/path 都不是 source capability。

## Selection 和 current Reload 不是同一事务

- register 从同一次 held capture 取得 capsule、logical manifest、physical proof，factory 验证后才发布 service-owned record；失败不留记录，不自动选择，active 实例只读 capsule。
- selection 慢 capture 不持 coordinator lease，但持 managed root、完整 external registry physical proofs 与目标 package session，让新请求可推进 generation/cancel旧准备。
- final completion 取得 fresh selection lease，复核 generic mutation epoch、generation、full declarations/digest/held proofs；同一 Realm 线性化点用本次 held metadata 更新 Name/Creator/Hash。
- current same-ID Reload 复用 proof，却只发布 prepared in-memory revision；registration metadata observation 保持 exact，成功 Reload 不写 Realm。不要将 selection 的 metadata 更新塞进 commit barrier。
- external 显式 dropdown/configured restart 与 random/next/previous 候选分开，后者不隐式选 external；same-value 不等于 reload，无 watcher。

## UI/读取 worker 不持 authority

- row/确认框只持 committed ID、immutable label、kind/capability hint，不持 Skin/Live record/path/manifest/journal/proof；action 由 manager 按 ID fresh重读。
- Workspace/support inspect 只读 worker 不占 mutation slot；UI 关闭取消自己的 refresh，manager shutdown 仍要封门/cancel/观察/join 所有读取。
- managed Open 的 initial/final Realm view 都要验证 exact record 与 normalized path 唯一；重复声明不调用 host。
- support 读取不能误调会前滚/回滚的 recovery。只有 fresh inspection 唯一证明现有 handler 安全才提供 Retry，不让用户删 journal 或选恢复方向；输出稳定脱敏 reason，不输出 ID/path/entry/native exception。

## Unregister 的“源不存在也可做”例外

- noncurrent Unregister pure-Realm compare-remove exact service-owner record，不 capture/open/write/delete source。
- current 先 fallback + old ConsumersDetached，再 fresh compare current generation/full registry/exact record，pure-Realm remove；失败借 operation lease 恢复 exact A、保 record。
- source missing/unreadable/drift 本身不应阻止解除陈旧注册；它从不授予 source I/O。不要把 selection 的 source capture 前提套到注销。
- ordinary .osk 使用 Delete，不是 Unregister；managed physical Delete 不从此例外推导。

## ManagedCopy 的独有窄窗

完整 single-v3 intent、首写/recovery 矩阵去 [[reference_skin_managed_folder_mutation_foundation]]。

- 只收 external record ID + 用户明确 target child；manager 生成 operation/staging。bytes 只来自 fresh capsule，目录含空目录只来自同次 immutable manifest，不重开 external path。
- 首个 provisional root/byte 前就须 durable intent + exact reload；no-follow/no-replace，不 merge/overwrite/auto-suffix，不按名字/年龄清 staging。
- live writer 在 CaptureStaged 建立新完整 authority 后须释放旧 writer-tree descendant/root handles再 move；不能靠放宽 share 或 source path reopen 绕过 NTFS。
- held session Validate 不能永久捕获首个调用的 token，后续要用当次 token/CancellationToken.None；physical 步骤后取消由 recovery 收口。
- non-overlap external set 必须 fresh持有到 final collision；不能恢复旧“存在 external 就全局阻断”，也不能只信旧 digest。

关联：[[reference_skin_managed_folder_selection]]、[[reference_skin_atomic_reload_detach]]。
