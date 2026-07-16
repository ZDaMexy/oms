---
name: reference_skin_filesystem_authority_preflight
description: Skin folder schema-56 authority/path preflight、Windows 路径歧义与不可误作 mutation capability 的安全边界
metadata:
  node_type: memory
  type: reference
---

# Skin folder authority/path preflight 地雷

## 当前可信合同

- `SkinFilesystemStorageResolver` 只做 schema 56 storage declaration 与检查当时的 lexical/reparse preflight；当前没有 production caller。
- 无 path + external=false 是既有 Realm `.osk`/built-in authority，且不触碰 `Storage`；无 path + external=true invalid。
- managed 只接受 `chartskin/<direct-child>` relative path；external 首批只接受 drive-letter-qualified fully-qualified Windows path且永久只读。该语法不证明物理本地盘，mapped network drive、SUBST 与 final identity 仍需后续 resolved-identity gate。folder 的 `Files` 必须空，protected/fixed-ID/DeletePending folder 均拒绝。
- external 与 managed `chartskin` namespace 的 exact/ancestor/descendant 重叠必须拒绝，否则同一包会得到两种 scanner/delete 语义。UNC、device namespace、盘符根、traversal、ADS、尾点/尾空格和 Windows 设备名当前 fail-closed。
- normalised absolute/relative path都含敏感用户目录信息，不得进入诊断、安全字符串或持久化日志。

## 不能误推的能力

- preflight 结果不是 resolved/final physical identity、authority owner tag、mutation token、package inventory、`InstantiationInfo` 验证或选择资格。
- `File.GetAttributes()` 分段检查存在 TOCTOU；8.3、SUBST、alias/final-path identity、包内 reparse与真实 junction traversal尚未闭合。不能把 normalised path直接交给 `NativeStorage`、scanner、rename/delete或 parser 后宣称安全。
- external 的 `NativeStorage` 以后只能作为只读 source adapter；parser/decoder 应消费 no-follow 完整捕获并验证后的 OMS 自有 immutable revision capsule，不能持续直读可变 live folder。
- scanner 开工前仍需 nullable opaque persistent owner token；null/unknown、`.osk`、另一 root/authority与不完整扫描中未见的记录都不能自动清理。
- folder factory 必须精确允许 `InstantiationInfo`，不能走 `SkinInfo.CreateInstance()` 的历史 `TrianglesSkin` fallback。整包 reload 还需要 generation/current-selection/revision gate、全 consumer publication barrier和旧 owner安全退役。

## 验证与工作流

- 第一刀 fixture 同时锁 managed/external bytes、mtime 与 `SkinInfo` 不变，避免“只比较目录项名称”产生零写入假阳性。
- reparse fake probe只证明 typed branch；真实 Windows junction/no-follow integration test留到 inventory/mutation service，不能用前者关闭完整 G1 安全门。
- 当前进度、精确测试数字与下一刀只看 P1-A STATUS/PLAN；本 memory 不把 preflight 写成 folder skin产品能力。
