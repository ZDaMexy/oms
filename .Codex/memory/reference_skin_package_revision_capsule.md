---
name: reference_skin_package_revision_capsule
description: immutable capsule 的 post-capture 身份、内容 revision、非 owning view 与物理 proof 区别
metadata:
  node_type: memory
  type: reference
---

# Package revision capsule 地雷

完整 container/capture/factory 合同见 [P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)，当前 gate 读 [STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)。

## 它证明了什么

- SkinPackageRevisionCapsuleFactory 是 post-capture container，输入logical entries、declared length、read callback，自身无filesystem authority。
- 名字统一slash/NFC，再按Windows case-insensitive检查duplicate、绝对/穿越/ADS/尾点尾空格/device、file-directory conflict。合成parent/空目录计entry/depth预算；空目录不进内容revision。
- exact读取declared length；版本domain + deterministic规范UTF-8名/长度/per-file hash形成content revision。它表示实际持有bytes的内容身份，不是physical identity、semantic validity或权限。
- 独占defensive byte backing，metadata只读，resource view非owning，Get/GetStream返回副本。view Dispose不释放capsule；owner退役幂等清零。失败/取消清本次及此前provisional backing，不返回半包。

## 它没有证明什么

- 同长度不能检测读取中的同长改写；纯container不能证明bytes来自preflight root、同一物理entry或稳定inventory。native producer proof见 [[reference_skin_windows_handle_capture]]。
- content hash不能代替InstantiationInfo allowlist、record owner、generation、选择资格、mutation token或active publication revision。
- move前后必须分别保留source/target physical/tree proof；空目录不进content hash，所以仅比capsule hash不足以证明完整tree。target metadata须来自最终capture，不能从目录名或publication plan推作者字段。
- factory必须走exact-capsule owning store，不能让live RealmBackedResourceStore/NativeStorage排在前面；否则同名资源或capture后改写可混入active实例。
- ordinary .osk Reload以fresh Realm declaration +逐blob hash准备；active owner不随Realm file-path/external/DeletePending projection漂移，fresh operation则重新拒绝不匹配。不要将此误称external registry file drift。

## Ownership 导航

capsule单owner与participant/work/operation lease一起退役；Rename不销毁active capsule，Import不自动选择或取消无关pending。完整发布/失败回退见 [[reference_skin_atomic_reload_detach]]，managed move/publisher见 [[reference_skin_managed_folder_mutation_foundation]]。不得从“有capsule”直接推出G1或Skin V1已完成。
