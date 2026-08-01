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

- 恢复基线 F1：`BmsSkinDecoder` / `BmsLegacySkin` / `.osk` 导入路由；现存静态件的颜色、纹理、几何；reference ini 自校验。恢复后先以native BMS普通短键编号帧重开窄生产纵切，之后又扩到长条head/body/tail；这是历史顺序，当前可见能力与待验收项只看P1-A STATUS和集中视觉清单。
- 程序化`OmsSkin`是恢复时的实际迁移链底；在P1-A明确记录canonical接管前继续作为保护性迁移保障，最终fallback必须是只读`oms-simple.osk`，程序化主题视觉须在V1发布前退出。
- 恢复基线的G1只保留两块：folder-backed ctor；`SkinInfo.FilesystemStoragePath` / `IsExternalFilesystemStorage` + Realm schema 56；在恢复当时**没有**生产扫描、选择、安全删改或热重载。
- 截至2026-07-29的恢复后重开历史曾依次加入authority/path preflight、managed Windows native no-follow capture、pure immutable capsule、schema 57 exact-owner启动scanner、production factory/guarded selection、公共mutation/recovery foundation、directory-only rename及fixed-source staged import；这只是带日期的历史锚点，不是实时完成度。当前状态只看P1-A，专项地雷见[[reference_skin_filesystem_authority_preflight]]、[[reference_skin_package_revision_capsule]]、[[reference_skin_windows_handle_capture]]、[[reference_skin_managed_folder_scanner]]、[[reference_skin_managed_folder_selection]]与[[reference_skin_managed_folder_mutation_foundation]]。
- 恢复时新增两个独立修正：复制流后 reset position 再交 base parser；14K 右皿 `S2` → `P2` 素材映射。
- F2/F3/G2、Lua、mania fallback adapter与reference-default是恢复时撤回或未进入可信基线的历史面；当前等价实现状态须从`SV1-*`计划重新判断。

## 旧实现地雷

- future external adapter不得把absolute path交给contained storage；只能在明确authority root后以`NativeStorage`作为只读source，并经自身resolved-identity/capture gate。
- 删除/重命名必须先 resolve、做 root containment、拒绝冲突并考虑 reparse point；禁止“目标存在就递归删除”。
- 启动扫描不能以本轮扫描结果删除不属于自身 authority 的 Realm 记录。
- parser/unit 类型断言不证明生产 `SkinManager`、ruleset fallback 或真实事件链已接通。
- BMS 测试数字变绿不能以破坏 mania 默认资源为代价；触碰 shared skin、mania compatibility 或 fallback authority 时，跨 ruleset focused gate 必跑。
- 不要恢复旧测试中“用户 BMS 皮肤缺件不得落到 OmsSkin”的错误期待，正确合同是逐组件 fail-open fallback。

## 恢复后的历史重开清单与当前入口

1. 先读 `doc_md/other/SKIN_SYSTEM_RECOVERY_20260710.md` 与 P1-A 四件套；当前执行门只看 P1-A STATUS/PLAN。
2. schema 56 清点与定点迁移、`SV1-0` 自动/数据/2026-07-14 实机 gate 均已完成；不要重复打开或清理生产数据。
3. directory-only rename、fixed-source staged import与managed delete已经各自按独立切片过门；G1后续仍是external registration/capture与atomic reload/detach。staged import只接受operationId固定slot下由OMS持有、外部原来源已保留的provisional副本，按kind复用同一journal/recovery并合法交接scanner owner；完整NTFS/selection/delete/shutdown地雷只看[[reference_skin_managed_folder_mutation_foundation]]，不要倒写本恢复历史。不要从本恢复memory推断任一门的实时完成度，只看P1-A。
4. 2026-07-14只闭合恢复静态基线；其后新增的普通短键与长条head/body/tail视觉都必须按集中清单单独签收，不能复用该结论。
5. fixed-source staged import仍没有GUI或视觉签收；managed delete只有既有settings确认框这一条窄玩家入口。rename/import UI、external、reload与atomic detach继续冻结。
