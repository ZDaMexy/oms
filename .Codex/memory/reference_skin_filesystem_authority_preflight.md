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
- managed 只接受 `chartskin/<direct-child>` relative path；external 首批只接受 drive-letter-qualified fully-qualified Windows path且永久只读。该语法不证明物理本地盘；managed后续已由native capture把映射收窄到exact physical NT volume，external resolved-identity仍未实现。folder 的 `Files` 必须空，protected/fixed-ID/DeletePending folder 均拒绝。
- external 与 managed `chartskin` namespace 的 exact/ancestor/descendant 重叠必须拒绝，否则同一包会得到两种 scanner/delete 语义。UNC、device namespace、盘符根、traversal、ADS、尾点/尾空格和 Windows 设备名当前 fail-closed。
- normalised absolute/relative path都含敏感用户目录信息，不得进入诊断、安全字符串或持久化日志。

## 不能误推的能力

- preflight 结果不是 resolved/final physical identity、authority owner tag、mutation token、package inventory、`InstantiationInfo` 验证或选择资格。
- `File.GetAttributes()` 分段检查存在 TOCTOU，不能把 normalised path直接交给 `NativeStorage`、scanner、rename/delete或 parser 后宣称安全。managed production factory必须只消费resolver-issued request经native capture完整成功后的capsule；capture地雷见 [[reference_skin_windows_handle_capture]]。
- preflight → managed Windows native no-follow producer → pure capsule → production exact-capsule factory/guarded selection已实现于已注册合法managed记录；external的resolved-identity/capture仍未实现。其`NativeStorage`以后只能作为只读source adapter，parser/decoder只能消费完整成功后交付的OMS自有capsule，不能持续直读可变live folder。选择链地雷见[[reference_skin_managed_folder_selection]]。
- schema 57 scanner现以nullable opaque persistent owner token精确隔离authority；只维护exact-own且结构仍合法的记录。null/unknown、`.osk`、另一root/authority与不完整扫描中未见的记录都不能自动清理；observed-invalid保护同path，完整稳定inventory才可负向清理其它确实absent的exact-own记录。完整scanner地雷见[[reference_skin_managed_folder_scanner]]。
- production managed folder factory现已精确允许`InstantiationInfo`并拒绝`SkinInfo.CreateInstance()`历史`TrianglesSkin` fallback；整包reload仍需要全consumer publication barrier和旧owner安全退役，不能用单次selection pair提交冒充。

## 验证与工作流

- 第一刀 fixture 同时锁 managed/external bytes、mtime 与 `SkinInfo` 不变，避免“只比较目录项名称”产生零写入假阳性。
- capture已有真实Windows package/nested junction、hardlink、busy writer与反向share/final-inventory gate；mutation foundation另有held managed root/source、target name slot、fixed staging authority、durable journal与共享线性化，但没有物理写primitive。每个真实rename/import/delete仍需独立junction/collision/final identity/crash-point门，不能用foundation测试关闭完整G1安全门。
- 当前进度、精确测试数字与下一刀只看 P1-A STATUS/PLAN；本 memory 不把 preflight本身写成 folder skin产品能力。
