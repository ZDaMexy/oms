# 皮肤系统可信恢复审计（2026-07-10）

> 本文记录 2026-06-30 00:05（北京时间）协作分界点之后皮肤改动的取证、保留/撤回决定与重新准入条件。它是恢复过程的证据账，不替代 [P1-A 四件套](../subline/P1-A/) 的当前计划与约束。

## 结论

- **不把仓库指针硬回退到旧提交，也不继续接收分界点后的整批皮肤实现。** 当前分支保留完整 Git 历史，以 `2b27c09` 的可信树为代码基线，再只补入可独立证明的 H1/H2 修正；恢复结果以新的正常提交承载。
- 严格按提交时间，分界点前最后一个正式提交是 `b53b798`（2026-06-29 15:57 +08:00）。
- 实际恢复基线选 `2b27c09`，仅因为它相对 `b53b798` 的核心变化——`SkinInfo.FilesystemStoragePath`、`IsExternalFilesystemStorage` 与 Realm schema `55 → 56`——已经存在于分界点前的 WIP `a4c3346`（2026-06-29 16:11 +08:00），而且现有用户 Realm 已经由后续程序打开过，降回 schema 55 会制造不必要的数据兼容风险。`2b27c09` 的正式提交时间在分界点后，不代表其代码来源在分界点后。
- `2b27c09..9e37087` 的 G1 生产链、F2 动态件、Lua、mania fallback adapter、reference-default 替换及恢复前 dirty tree **全部不直接接纳**。其中设计信息可以重用，代码必须以后按独立切片重新实现和验证。

## 取证对象与保全

恢复前仓库状态：`master` 位于 `9e37087`，工作区还包含未提交的皮肤改动。以下对象已在改树前保全：

| 对象 | 保全位置 |
| --- | --- |
| 恢复前 HEAD | `refs/archive/pre-recovery-20260710/head` |
| 严格分界提交 | `refs/archive/pre-recovery-20260710/cutoff-b53` |
| 采用的隔离基线 | `refs/archive/pre-recovery-20260710/isolation-2b27` |
| 恢复前 dirty tree（含 untracked） | `refs/archive/pre-recovery-20260710/dirty-stash` / stash `4bde4c3400517e4c505b9be4fc2b321aa6bbe51b` |
| 当时可达与不可达 Git 对象 | `refs/archive/pre-recovery-20260710/unreachable/*` |
| 完整 Git bundle | 仓库外脱敏恢复归档中的 `oms-pre-recovery.bundle` |
| 运行时数据备份 | 同一脱敏恢复归档中的 `runtime/{production,release-test,appdata}` |

`git bundle verify` 已确认 bundle 包含完整历史及恢复用 refs。运行时备份包含三个已发现数据根的 Realm、配置和存在的 `chartskin/`；恢复时没有 OMS/osu 进程占用这些文件。归档仅用于审计和定点取回，**禁止整包 apply/cherry-pick 回主线**。

## 保留的实现

1. **F1 静态素材/ini 主链**：独立 `[Bms]` 解析、`BmsLegacySkin` 配置源、`.osk` 导入路由、现有静态渲染件的颜色/纹理/几何配置、reference `skin.ini` 自校验门。
2. **恢复期不可提前删除的程序化迁移兜底**：`OmsSkin` 仍处于实际 fallback 链底，用户皮肤缺件必须逐组件回落；它不是最终产品 fallback，Skin V1 最终由只读 `oms-simple.osk` 接管并在发布前让程序化主题视觉退出产品链。
3. **G1 的两个无生产行为建块**：folder-backed 构造入口；`SkinInfo` 两个 nullable/scalar 字段与 Realm schema 56。它们不等于 `chartskin/` 已可扫描、选择、删改或热重载。
4. **H1 流位置修正**：`BmsLegacySkin` 复制 `skin.ini` 后，在交给 base mania parser 前把流位置重置到 0。
5. **H2 14K 双皿映射修正**：decoder 接受 `S2`，14K 右皿 lane 映射到 `P2` 素材，左皿仍映射 `S`/`P1`。

## 撤回原因

恢复前代码与实机反馈共同表明，分界点后的实现不能作为可信主线：

- G1 删除/重命名路径存在递归作用到错误目标的风险；外部绝对路径还被错误地交给 contained storage，managed/external 边界没有形成可证明的安全合同。
- 启动扫描可能删除既有用户皮肤 Realm 记录；热重载仅覆盖部分文件变更，且缺乏生产行为级测试。
- 一项新测试反向要求错误的 fallback 语义，导致干净 HEAD 出现 BMS 失败；dirty tree 虽让 BMS 数字变绿，却用 BMS reference skin 替换全局 `OmsSkin`，造成 mania 默认资源回归失败。
- Lua 与 reference skin 路线存在未接入生产链、以类型断言代替运行时证明、文档先于代码宣称完成等问题。

因此，旧提交数量、测试数量或文档完成度均不能单独作为恢复依据；必须以生产调用链、跨 ruleset fallback、路径安全和实机视觉结果联合判定。

## 重新准入门

后续皮肤工作按以下顺序重新推进：

1. 先让本恢复基线通过 BMS/mania/core focused tests 与 Release 构建，再由用户完成无外部皮肤、`.osk` 用户皮肤和 5K/7K/9K/14K 的实机视觉验收。
2. 清点 schema 56 Realm 中可能由旧 G1 写入的 folder-backed 记录；未经备份和显式迁移设计，不自动删除或改写用户数据。
3. G1 从 managed/external 路径模型开始重做：外部根使用 `NativeStorage`；所有删除/重命名做 resolved-root containment、冲突拒绝和 symlink/reparse-point 风险处理；扫描不得清除非本轮 authority 拥有的记录。
4. 热重载覆盖 `skin.ini`、素材变化及原子替换，并用真实 `SkinManager`/选择链测试，不以孤立 parser 测试替代。
5. F2、Lua、文件型默认皮肤后置；每个组件必须同时证明用户皮肤 → `OmsSkin` 逐组件 fallback、mania 不回归、真实事件能触发、常见 keymode 布局正确。

## 验证记录

- H1/H2 focused：`BmsLegacySkinTest` **15/15**（2026-07-10）。
- BMS 全量：`osu.Game.Rulesets.Bms.Tests` **1005/1005**。
- mania 默认资源专项：`TestOmsBuiltInSkinIsRegisteredAndProvidesResources` **1/1**；确认恢复前 dirty tree 的“BMS reference 覆盖全局 OmsSkin”回归未保留。
- mania 全量：**787/791**；4 项失败均为 `TestSceneAutoGeneration` 的既有 HoldNote frame-count 期待，本恢复未修改 mania/autoplay 代码。
- core skin focused：**57/62**；1 项仍期待已移除的 Argon 默认类型，4 项依赖已移除 ruleset 的 osu beatmap 归档，属于恢复基线既有测试失配。
- `osu.Desktop.slnf` Release：**0 error / 20 warnings**。18 条是 MessagePack 3.1.3 的 9 个 NU1902 在 restore/build 两阶段重复报告；其余为 BMS test 工程既有 CS8600/CA2007。恢复不保留用全局 NoWarn 隐藏依赖告警的改动。
- 本审计形成时，人工视觉验收在用户确认前保持未完成；用户已于 2026-07-14 完成 `SV1-0` 全清单，当前结论见 [schema 56 / 实机门报告](SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)。其后新增的 managed `.osk` BMS 普通短键编号帧动画是独立产品 gate，不能复用该静态恢复结论。
