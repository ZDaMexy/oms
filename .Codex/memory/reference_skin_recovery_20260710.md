---
name: reference_skin_recovery_20260710
description: 2026-06-30 00:05 后皮肤异常的 Git/运行时保全、可信恢复基线与重新准入合同
metadata:
  node_type: memory
  type: reference
---

# 2026-07-10 皮肤系统可信恢复

## 必记结论

- 协作分界点：**2026-06-30 00:05（北京时间）**。
- 严格分界前最后正式提交：`b53b798`。
- 采用的工作树基线：`2b27c09`。只因它的 schema 56 patch 已存在于分界前 WIP `a4c3346`，且现有 Realm 已可能升级；不是认可它之后的协作质量。
- 恢复前 HEAD `9e37087` 与 dirty tree 没有丢弃：分别保存在 `refs/archive/pre-recovery-20260710/head` 和 `.../dirty-stash`；完整 bundle 位于仓库外脱敏恢复归档。
- 运行时数据备份位于同一脱敏归档的 `runtime/{production,release-test,appdata}`。恢复时的生产 authority 是脱敏自定义数据根，其 Realm 约 108 MB，并有 `chartskin/`。

## 恢复时的可信皮肤面与当前补充

- 恢复基线 F1：`BmsSkinDecoder` / `BmsLegacySkin` / `.osk` 导入路由；现存静态件的颜色、纹理、几何；reference ini 自校验。其后只有 native BMS 普通短键编号帧成为窄生产例外；当前全貌看 P1-A STATUS。
- 程序化 `OmsSkin` 是恢复时及当前实际迁移链底，用户皮肤缺件逐组件回落；最终产品 fallback 是只读 `oms-simple.osk`，程序化主题视觉必须在 V1 发布前退出。
- 恢复基线的G1只保留两块：folder-backed ctor；`SkinInfo.FilesystemStoragePath` / `IsExternalFilesystemStorage` + Realm schema 56；在恢复当时**没有**生产扫描、选择、安全删改或热重载。
- 恢复后 `SV1-2` 已把 authority/path preflight、managed Windows native no-follow capture、pure immutable capsule、schema 57 exact-owner启动scanner与production factory/guarded selection闭合成窄生产链；它仍不是专用managed mutation、external capture或热重载。地雷见 [[reference_skin_filesystem_authority_preflight]]、[[reference_skin_package_revision_capsule]]、[[reference_skin_windows_handle_capture]]与[[reference_skin_managed_folder_selection]]。
- 恢复时新增两个独立修正：复制流后 reset position 再交 base parser；14K 右皿 `S2` → `P2` 素材映射。
- F2/F3/G2、Lua、mania fallback adapter、reference-default 替换均未落地。

## 旧实现地雷

- external absolute path 不能交给 contained storage；使用 `NativeStorage`，并明确 authority root。
- 删除/重命名必须先 resolve、做 root containment、拒绝冲突并考虑 reparse point；禁止“目标存在就递归删除”。
- 启动扫描不能以本轮扫描结果删除不属于自身 authority 的 Realm 记录。
- parser/unit 类型断言不证明生产 `SkinManager`、ruleset fallback 或真实事件链已接通。
- BMS 测试数字变绿不能以破坏 mania 默认资源为代价；触碰 shared skin、mania compatibility 或 fallback authority 时，跨 ruleset focused gate 必跑。
- 不要恢复旧测试中“用户 BMS 皮肤缺件不得落到 OmsSkin”的错误期待，正确合同是逐组件 fail-open fallback。

## 恢复后的历史重开清单与当前入口

1. 先读 `doc_md/other/SKIN_SYSTEM_RECOVERY_20260710.md` 与 P1-A 四件套；当前执行门只看 P1-A STATUS/PLAN。
2. schema 56 清点与定点迁移、`SV1-0` 自动/数据/2026-07-14 实机 gate 均已完成；不要重复打开或清理生产数据。
3. G1 仍按 managed/external、安全删改、扫描 authority、热重载独立过门；当前已闭合schema 57 exact-owner启动自动发现/reconcile及合法managed record的production factory/选择，external capture、专用mutation与reload仍未完成。测试按实际改动面选择，修改 shared/mania/fallback authority 时才强制追加 core/mania gate。
4. 2026-07-14 只闭合恢复静态基线；普通短键编号帧动画仍须由用户单独实机确认，不能复用该结论。当前 gate 只看 P1-A STATUS。
