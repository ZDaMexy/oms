---
name: reference_skin_recovery_20260710
description: 皮肤异常恢复的 Git/运行时保全、可信基线选择理由与不可重复执行的数据处置边界
metadata:
  node_type: memory
  type: reference
---

# 2026-07-10 皮肤恢复召回

恢复权威：[SKIN_SYSTEM_RECOVERY_20260710](../../doc_md/other/SKIN_SYSTEM_RECOVERY_20260710.md)。当前实现/门只读 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md) / [PLAN](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md)，不能把恢复快照当现状。

## 保全与基线为什么这样选

- 协作分界：2026-06-30 00:05（北京时间）；严格分界前最后正式提交b53b798。
- 采用2b27c09仅因为其schema56 patch已存在于分界前WIP a4c3346，实际Realm可能已升级；不是认可后续异常协作或允许整包恢复。
- 恢复前HEAD 9e37087与dirty tree分别保存在refs/archive/pre-recovery-20260710/head、.../dirty-stash，完整bundle在仓库外脱敏恢复归档，未丢弃。
- 运行时备份在同一归档的runtime/{production,release-test,appdata}；生产authority是自定义数据根，包含Realm与chartskin。精确本机证据不复制进memory。

## 恢复时的可信范围

- F1是BmsSkinDecoder/BmsLegacySkin、.osk路由、静态颜色/纹理/几何与reference ini校验。
- G1当时只保留folder-backed ctor、FilesystemStoragePath/IsExternalFilesystemStorage和schema56，没有生产scanner/selection/安全删改/reload。后续恢复史查P1-A CHANGELOG，不往本页叠campaign完成态。
- 两个独立修正保留：复制流后reset position再交base parser；14K右皿S2→P2素材。
- F2/F3/G2、Lua、mania fallback adapter、reference-default是当时撤回/未入可信基线的历史名称；现行等价范围由PLAN判断，异常归档只定点取证。
- 程序化OmsSkin是恢复迁移保障；canonical oms-simple通过parity/完整性/原子恢复/实机门前保留，最终接管后退出程序化主题视觉。

## 不要重演或重复执行

- schema56清点/定点迁移与SV1-0数据门已处置；四个无authority blob继续保全，不再打开生产库“确认一下”或跑全局清理。取证方法和处置依据见 [[reference_skin_schema56_inventory_20260713]]。
- 2026-07-14只签收恢复静态基线；后来Note/LN动画仍需各自集中视觉签收，不能复用。
- 扫描不能清理非自身authority的记录；“目标存在就递归删”不是恢复方法。path containment与reparse检查也不能替代held identity；见 [[reference_skin_filesystem_authority_preflight]]、[[reference_skin_managed_folder_mutation_foundation]]。
- parser/unit类型断言不证明manager→ruleset→真实renderer/event可达。BMS绿测不能以破坏mania默认资源为代价，shared/mania/fallback改动要覆盖受影响ruleset。
- 旧测试“用户BMS皮肤缺件不得落到OmsSkin”的期待错误；正确的是逐组件回落到该阶段有效的protected fallback，不能通过撤掉回落让测试变绿。

capsule/source、scanner、selection/reload的独有地雷分别见 [[reference_skin_package_revision_capsule]]、[[reference_skin_managed_folder_scanner]]、[[reference_skin_managed_folder_selection]]、[[reference_skin_atomic_reload_detach]]。
