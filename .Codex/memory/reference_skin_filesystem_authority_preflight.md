---
name: reference_skin_filesystem_authority_preflight
description: Skin folder schema-56-origin authority/path preflight、Windows 路径歧义与不可误作 mutation capability 的安全边界
metadata:
  node_type: memory
  type: reference
---

# Skin folder authority/path preflight 地雷

## 当前可信合同

- `SkinFilesystemStorageResolver` 只处理schema 56引入、当前schema继续保留的storage declaration与检查当时的lexical/reparse preflight；managed成功结果会签发internal capture request，production factory/guarded selection现可消费完整native capture capsule，但resolver结果本身仍不是选择授权。
- 无 path + external=false 是既有 Realm `.osk`/built-in authority，且不触碰 `Storage`；无 path + external=true invalid。
- managed 只接受 `chartskin/<direct-child>` relative path；external首批只接受drive-letter-qualified fully-qualified Windows path且永久只读。该语法不证明物理本地盘；进入生产链时，managed与external都必须由native capture把映射收窄到exact physical NT volume及held no-follow ancestry/root proof。folder 的 `Files` 必须空，protected/fixed-ID/DeletePending folder 均拒绝。
- external 与 managed `chartskin` namespace 的 exact/ancestor/descendant 重叠必须拒绝，否则同一包会得到两种 scanner/delete 语义。UNC、device namespace、盘符根、traversal、ADS、尾点/尾空格和 Windows 设备名当前 fail-closed。
- normalised absolute/relative path都含敏感用户目录信息，不得进入诊断、安全字符串或持久化日志。

## 不能误推的能力

- preflight 结果不是 resolved/final physical identity、authority owner tag、mutation token、package inventory、`InstantiationInfo` 验证或选择资格。
- `File.GetAttributes()` 分段检查存在 TOCTOU，不能把 normalised path直接交给 `NativeStorage`、scanner、rename/delete或 parser 后宣称安全。managed production factory必须只消费resolver-issued request经native capture完整成功后的capsule；capture地雷见 [[reference_skin_windows_handle_capture]]。
- preflight → managed/external Windows native no-follow producer → pure capsule → production exact-capsule factory/guarded selection已实现于合法authoritative记录；directory-only rename、fixed-source staged import与ManagedCopy也已有各自专用held-authority consumer，但都不能从normalised path授权。external的`NativeStorage`只可作为只读source adapter，parser/decoder只能消费完整成功后交付的OMS自有capsule，不能持续直读可变live folder；registry/selection/ManagedCopy还须保持完整held physical proof到final线性化。选择链地雷见[[reference_skin_managed_folder_selection]]与[[reference_skin_external_workspace_managed_copy]]。
- schema 57 scanner现以nullable opaque persistent owner token精确隔离authority；只维护exact-own且结构仍合法、由scanner创建或由staged-import one-shot publisher通过同等级门合法交接的记录。普通scanner不消费publication plan。null/unknown、`.osk`、另一root/authority与不完整扫描中未见的记录都不能自动清理；observed-invalid保护同path，完整稳定inventory才可负向清理其它确实absent的exact-own记录。完整scanner地雷见[[reference_skin_managed_folder_scanner]]。
- production managed/external folder factory精确允许`InstantiationInfo`并拒绝`SkinInfo.CreateInstance()`历史`TrianglesSkin` fallback。preflight/capture只提供background prepare的source authority；C2 current publication另由`SkinCurrentRevision`、participant/work lease与update-thread barrier完成，仍不能用单次selection pair或capture success冒充。

## 验证与工作流

- 第一刀 fixture 同时锁 managed/external bytes、mtime 与 `SkinInfo` 不变，避免“只比较目录项名称”产生零写入假阳性。
- capture已有managed/external真实Windows package/nested junction、hardlink、busy writer与反向share/final-inventory gate；mutation foundation另有既存held managed/staging authority roots、target name slot、durable journal与共享线性化。Folder Skin Workspace已消费directory-only rename、fixed-source staged import/ManagedCopy、managed delete与external Open/Unregister真实caller。仍不得把通用preflight或专用操作类推成任意path/delete/publication能力；旧通用`CanModify/Delete`及reload入口继续冻结，current revision唯一入口是Settings manual Reload并走独立C2协议。
- 当前进度、精确测试数字与下一刀只看 P1-A STATUS/PLAN；本 memory 不把 preflight本身写成 folder skin产品能力。
