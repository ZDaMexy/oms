---
name: reference_skin_windows_handle_capture
description: Managed skin folder 的 Windows fixed-handle/handle-relative no-follow capture、identity/TOCTOU 与非 transaction 边界
metadata:
  node_type: memory
  type: reference
---

# Managed skin Windows handle capture 地雷

## 当前可信合同

- `SkinManagedPackageCapture` 只接受 `SkinFilesystemStorageResolver` 为合法 `chartskin/<direct-child>` 发出的 opaque request；构造会校验private issuer，但request不是security/filesystem/mutation capability。Realm `.osk`、external与invalid declaration均不产生request；request含敏感process-local data root，`ToString()`不得展开。
- native adapter只接受 `QueryDosDevice` 的exact `\Device\HarddiskVolume<uint>` target，直接开NT volume后逐段handle-relative `NtOpenFile`；SUBST、mapped/remote drive、shadow/device alias fail-closed。当前只覆盖managed，external capture仍未实现。
- authority/package/子目录/文件都以`OBJ_DONT_REPARSE` + `FILE_OPEN_REPARSE_POINT`打开；enumeration用`FileIdExtdDirectoryInformation`，在native循环里先检查取消与entry budget再增长managed集合。名称双侧NFC后按Windows ordinal-ignore-case匹配，并拒绝未由resolver展开成长名、missing-long-name却可alias-open的8.3/alternate alias。
- 每个节点校验volume serial + 128-bit file ID、kind、attributes/reparse tag、时间、length、link count与delete-pending；文件要求单link，所有物理identity全包唯一。文件handle只共享read，因此既有writer使capture fail，capture期间的新write/rename打不开。
- 所有目录和文件在read前pin住；pure capsule从non-owning held-handle stream读取，单次最多1 MiB以保留取消响应。capsule完成后仍须复验全部pinned metadata、每个directory inventory、每级authority link与package root；只有全部一致才先关完handle再返回capsule。
- cleanup必须在typed失败、取消、意外异常和某个handle `Dispose()`抛出时继续释放其余handle，并清理provisional capsule。成功结果不得携带live handle、stream或deferred filesystem callback。
- fixed-source staged import在既存held `skin-mutation-staging` authority root上复用同等级full package capture，而不是伪造resolver request或退回absolute path。operation source固定为`{operationId:N}` direct child，source handle需要`DELETE | SYNCHRONIZE`且允许share delete；staging root、source与既存managed root必须同volume，首个move/cleanup前仍须durable Prepared exact reload。
- staged move最终preflight后只释放NTFS要求释放的descendant handles，继续持有staging parent/source和managed root identity；move attempted后不再观察caller cancellation。target以no-follow完整重捕并比较source→target physical identity、pinned descendants、inventory和capsule fingerprint，任何差异都交journal/recovery冻结。

## 不能误推的能力

- 这不是filesystem transaction。保证仅为：发布bytes来自held identity，且final validation前观察到的变化会拒绝；final check之后的外部变化不影响已复制capsule，但也不存在跨文件系统的事务快照声明。
- resolver对真实存在的8.3 data-root path可能先经`Path.GetFullPath`展开成长名；native层仍必须拒绝任何未展开alias。真实SUBST命令级integration未跑，当前证据是exact volume-target classifier和fake alias合同。
- capture已有pure capsule及production `SkinManager` exact-capsule factory/guarded selection消费方，但capture本身不验证`InstantiationInfo`/选择资格，也不拥有scanner record、mutation token、external registration或reload publication。factory只能消费完整成功的exact capsule，不能回到normalised path或live `NativeStorage`；选择地雷见[[reference_skin_managed_folder_selection]]。
- 测试中的反向share gate证明write/file rename受阻，新建child仍可能发生但会被final inventory拒绝；不能把共享模式描述成冻结整个目录树。
- NTFS的DELETE access可以合法推进provisional父目录项相关metadata；held source node的pinned metadata仍须exact，但provisional parent inventory应比较name/identity/kind，不能把该时间推进当作foreign mutation。移动后package root的rename-related时间只可在明确target recapture合同内前进，不得放宽descendant metadata、content revision或安全gate。

## 入口

- 声明/路径前置见 [[reference_skin_filesystem_authority_preflight]]，capsule ownership见 [[reference_skin_package_revision_capsule]]。
- 当前测试数字、production接线与下一门只看P1-A STATUS/PLAN；held mutation authority另见[[reference_skin_managed_folder_mutation_foundation]]，不得把capture producer本身写成G1、folder skin或Skin V1产品能力。
