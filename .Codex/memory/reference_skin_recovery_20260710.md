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
- 恢复前 HEAD `9e37087` 与 dirty tree 没有丢弃：分别保存在 `refs/archive/pre-recovery-20260710/head` 和 `.../dirty-stash`；完整 bundle 位于 `F:\oms-recovery-archive\20260710-skin-recovery\oms-pre-recovery.bundle`。
- 运行时数据备份位于同目录 `runtime/{production,release-test,appdata}`。生产数据根是 `D:\oms\data`，其 Realm 在恢复时约 108 MB，并有 `chartskin/`。

## 当前可信皮肤面

- F1：`BmsSkinDecoder` / `BmsLegacySkin` / `.osk` 导入路由；现存静态件的颜色、纹理、几何；reference ini 自校验。
- 程序化 `OmsSkin` 是最终兜底，用户皮肤缺件逐组件回落。
- G1 只保留两块：folder-backed ctor；`SkinInfo.FilesystemStoragePath` / `IsExternalFilesystemStorage` + Realm schema 56。**没有**生产扫描、选择、安全删改或热重载。
- 恢复时新增两个独立修正：复制流后 reset position 再交 base parser；14K 右皿 `S2` → `P2` 素材映射。
- F2/F3/G2、Lua、mania fallback adapter、reference-default 替换均未落地。

## 旧实现地雷

- external absolute path 不能交给 contained storage；使用 `NativeStorage`，并明确 authority root。
- 删除/重命名必须先 resolve、做 root containment、拒绝冲突并考虑 reparse point；禁止“目标存在就递归删除”。
- 启动扫描不能以本轮扫描结果删除不属于自身 authority 的 Realm 记录。
- parser/unit 类型断言不证明生产 `SkinManager`、ruleset fallback 或真实事件链已接通。
- BMS 测试数字变绿不能以破坏 mania 默认资源为代价；跨 ruleset focused gate 必跑。
- 不要恢复旧测试中“用户 BMS 皮肤缺件不得落到 OmsSkin”的错误期待，正确合同是逐组件 fail-open fallback。

## 继续工作的入口

1. 先读 `doc_md/other/SKIN_SYSTEM_RECOVERY_20260710.md` 与 P1-A 四件套。
2. 清点 schema 56 数据中的 folder-backed 记录，只读诊断优先，不自动清理。
3. G1 按 managed/external、安全删改、扫描 authority、热重载四个独立切片重做；每刀都跑 BMS、mania 默认资源、core skin focused 与 Release build。
4. 真机视觉验收未通过前，不写“已完成/发行可用”。
