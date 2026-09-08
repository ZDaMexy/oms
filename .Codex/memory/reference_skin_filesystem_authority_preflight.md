---
name: reference_skin_filesystem_authority_preflight
description: storage declaration 与 lexical preflight 不能授予文件能力，Windows 路径歧义及外部只读边界
metadata:
  node_type: memory
  type: reference
---

# Folder authority/path preflight 地雷

路径声明和production安全链以 [P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md) 为准，完成度读 [STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)。此页只解释preflight最容易被误当成什么。

- 无path且external=false是Realm .osk/built-in，不触Storage；无path且external=true无效。
- managed只接受chartskin/<direct-child>相对path；external初始语法为drive-letter fully-qualified Windows path且永久只读。语法合法不证明物理本地盘，native capture还要固定exact NT volume与held ancestry。
- folder Files必须空，protected/fixed-ID/DeletePending folder不准入。external与managed namespace exact/ancestor/descendant重叠拒绝，否则同包可能获得冲突的scanner/delete语义。
- UNC/device namespace/盘符根/traversal/ADS/尾点尾空格/设备名的边界在resolver合同维护；normalised path仍是敏感用户信息，不进入诊断。
- File.GetAttributes分段检查有TOCTOU。resolver签发的是capture request，不是final physical identity、package inventory、InstantiationInfo验证、选择权或mutation capability；不能直接把normalised path交NativeStorage/parser/rename/delete就宣称安全。
- NativeStorage最多作为明确root下只读source adapter；decoder消费完整OMS-owned capsule，不能持续读live folder。proof要保持到相应final线性化，见 [[reference_skin_windows_handle_capture]]、[[reference_skin_external_workspace_managed_copy]]。
- record owner也不是文件能力。schema57旧记录owner=null，scanner只维护合法exact-own来源；observed-invalid与完整scan才可negative reconcile见 [[reference_skin_managed_folder_scanner]]。
- factory allowlist失败不能借SkinInfo.CreateInstance的历史TrianglesSkin fallback伪装成功；capture与选择资格是两个门，见 [[reference_skin_managed_folder_selection]]。
- 专用held mutation与current Reload各有自己的admission，不能从一次preflight/capture成功解禁通用CanModify/Delete或任意path操作。

回归要同时比较managed/external bytes、mtime与SkinInfo；只比较目录名会漏掉源被写的假阳性。恢复保全入口见 [[reference_skin_recovery_20260710]]。
