---
name: reference_skin_package_revision_capsule
description: Skin package post-capture immutable revision、资源名规范、ownership 与不可误作安全 capture 的边界
metadata:
  node_type: memory
  type: reference
---

# Skin package immutable revision capsule 地雷

## 当前可信合同

- `SkinPackageRevisionCapsuleFactory` 是 ruleset-neutral 的 **post-capture container factory**，不是 filesystem capture service。输入只有 producer 提供的逻辑 file/directory entries、declared length 与 read callback；自身无filesystem dependency，现已有managed/external native producer、ManagedCopy及production exact-capsule factory/guarded selection消费方。
- resource name 先把 `\` 统一为 `/`、做 Unicode NFC，再按 Windows case-insensitive 语义拒绝 duplicate、绝对/穿越/ADS/尾点尾空格/设备名及 file/directory 层级冲突。合成 parent 与空目录计入 entry/depth budget；空目录不进入内容 revision。
- 每个文件必须精确读取 declared length；whole-package content revision 使用版本 domain、确定性排序后的规范 UTF-8 名、长度和 per-file SHA-256。它只表示 capsule 实际持有内容的确定性身份，不是 authority、physical identity 或 semantic package validation。
- capsule 独占 defensive byte backing；metadata collection 只读，resource view 非 owning，`Get`/`GetStream` 返回副本。view dispose 不释放 capsule；capsule 退役幂等清零。预期 source failure typed reject，取消传播；失败/取消清理当前及此前 provisional backing，不返回半成品。

## 不能误推的能力

- 精确长度不能发现同长度内容在读取期间变化；纯 capsule 也不能证明 bytes 来自 preflight root、同一物理 entry 或一次稳定 inventory。
- managed/external Windows fixed root handle + handle-relative/no-follow capture、schema 57 exact-owner scanner/exact external registry、production exact-capsule factory/选择、公共mutation/recovery foundation、Workspace rename/ManagedCopy/delete已实现，地雷见 [[reference_skin_windows_handle_capture]]、[[reference_skin_external_workspace_managed_copy]]、[[reference_skin_managed_folder_selection]]与[[reference_skin_managed_folder_mutation_foundation]]；factory与staged-import publisher仍只能消费完整成功的capsule。不能从capsule类推通用delete capability；旧通用delete与atomic reload publication仍冻结。
- content revision 不是 `InstantiationInfo`、选择资格、generation、scanner owner、mutation token 或 active publication revision；这些 gate 不能由 hash 替代。
- staged import必须分别保留held source capture与move后target recapture的完整package fingerprint；只有physical identity、规范inventory、capsule content revision和最终`skin.ini` metadata均exact，one-shot publisher才可从最终target capsule生成Realm `Name`/`Creator`/hash。publication plan或目录名不能替代该fingerprint，也不得推导作者展示字段。
- production managed folder factory现已走exact-capsule marker/owning store构造路径，不让live `RealmBackedResourceStore`排在capsule前面。普通`.osk`与SkinEditor当前依赖Realm live store/refresh，没有被全局冻结；其原子更新另走prepared revision/new-instance协议。
- active capsule 的单一 owner 必须先 detach 全部 consumer 再 dispose。directory-only rename不销毁当前active capsule；fixed-source staged import也不自动选择或替换active capsule，且不会取消无关pending selection。逐 host 替换不等于全 playfield publication barrier；2026-08-09审计确认现有manager/renderer没有该barrier，NO-GO与重新开门条件见[[reference_skin_atomic_reload_detach]]。

## 入口

- path/authority 前置边界见 [[reference_skin_filesystem_authority_preflight]]，managed native producer见 [[reference_skin_windows_handle_capture]]。
- 当前完成度、测试数字与受管目录下一门只看P1-A STATUS/PLAN；不得把capsule本合同写成G1或Skin V1产品能力。整包reload/detach不要从capsule合同反推，先查[[reference_skin_atomic_reload_detach]]。
