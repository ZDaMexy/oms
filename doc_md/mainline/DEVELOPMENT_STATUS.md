# OMS 当前开发状态

> 最后更新：2026-09-09（全线代码对照与自动复验；无新增人工签收）
> 这里只保留当前事实、风险和最新验证。执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，历史见 [CHANGELOG.md](CHANGELOG.md)。

## 一句话状态

OMS 处于 Phase 1.x 后段，Skin V1 为 **`5/7 closed，C6 active`**。C1～C5 的作者工作区、revision 生命周期、唯一 layout、shared codec/material 与 scene/event 已闭合；C6 sandbox/最终整包 reload、C7 canonical 双包/Authoring Kit 仍未交付。`V-001`～`V-004` 签收 **0/4**，Skin V1 与 release 均未完成。详情见 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)。

## 产品与仓库基线

进度按 [P1-A 持久 campaign](../subline/P1-A/DEVELOPMENT_PLAN.md)的真实 caller/consumer 与硬退出门核算；campaign 非等权，不换算线性工期或产品完成百分比。已关闭合同作为后续输入，完整边界见 [P1-A 技术约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)。

- Windows-only，保留 osu!mania + 第一类 BMS；Osu/Taiko/Catch 已删除。
- 离线优先；Phase 3 前 OMS 私有服务与默认 endpoint 保持为空。用户主动添加公共 BMS 难度表 URL 是既有窄例外，不代表 OMS 在线产品能力已开放。
- BMS 直读 `chartbms/`，mania 直读 `chartmania/`；发行支持 `portable.ini → data/` 与 `storage.ini` 自定义根。
- 主要入口：`osu.Desktop.slnf`；BMS 主开发目标：`osu.Game.Rulesets.Bms`；统一输入：`oms.Input`。
- 当前协作分支为 `master`；可信恢复锚点是 `ef56507`，后续皮肤工作只能按小切片前进。

已关闭前置：P1-A `SV1-0` 的自动、schema 56 数据与用户恢复实机 gate 已全部通过；异常 copy 已定点移除，OMS fixed-ID 已修正，迁移归档与四个无 authority orphan blob 继续保全。

## 当前执行门

| 顺序 | 工作面 | 当前状态 | 下一检查点 |
| --- | --- | --- | --- |
| 1 | R3/R4 / Skin V1 storage + layout/material | `C1`～`C5`已闭合 | 保持P1-K authority、唯一immutable layout/material/scene/event与C2 exact publication合同，不重开或旁路 |
| 2 | R4 / Skin V1 后续合同 | `C6 active`；`SV1-1`整体仍未完成 | 交付sandbox与最终整包reload门；C5 scene/event/slot host已加入同一revision协议 |
| 3 | 集中视觉签收 | R2 首个 Note/LN 纵切的四组件自动门已闭合；`V-001`～`V-004` 签收 0/4 | 继续登记到[集中视觉清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)，在 Skin V1/release 完成声明前统一签收 |
| 4 | P1-B/P1-D 输入 | 软件基线可用 | analog scratch、校准、真实 HID |
| 5 | P1-E/P1-G 人工验收 | 待闭合 | LN/CN/HCN、BGA、Song Select、发行 checklist |

## 皮肤系统主线摘要

- `.osk`、legacy `[Mania]`/`[Bms]` compatibility 与显式 `Provide/Inherit/Suppress` 共用 shared codec；BMS/mania 的适用 public slot 已接入真实 material/scene host，具体作者能力见 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)。
- 程序化 `OmsSkin` 仍是实际链底，只作为迁移保障保留到 `oms-simple.osk` 通过 parity、完整性、原子恢复与实机 gate；最终产品渲染链由只读 canonical 包接管。
- Skin V1 的稳定方向是 mania/BMS 共享neutral ini/asset/animation/event runtime、三态解析与sandbox，ruleset topology/layout adapter分离；C5已交付versioned prepared scene、只读event Snapshot/Reset、全部适用public slot host与预算/池化证明，C6仍负责sandbox和最终整包reload。
- G1 的managed scanner/selection/mutation基线与C1 Folder Skin Workspace已成为C2冻结输入：external永久只读，copy bytes只来自immutable capsule，目录来自同次manifest；ordinary `.osk`继续是hash-backed Realm package。C2已用explicit manual Reload统一三源current revision并稳定关闭legacy update/editor旁路；C3/C4/C5又把唯一layout、resolved material、prepared scene与read-only event作为同一exact publication及participant/lease加入协议。C6仍需纳入sandbox与最终整包门，C7才处理canonical双包/Authoring Kit与移除程序化产品视觉。

恢复边界见 [2026-07-10 恢复审计](../other/SKIN_SYSTEM_RECOVERY_20260710.md)，当前实现与未完成 gate 见 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)，V1 完成定义见 [架构审计](../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

各子线当前状态与下一道门统一见[子线路由](../subline/README.md)。

## 最近一次验证

2026-09-09：Release 构建与 BMS 全量通过；mania full/core Skin 的失败名称及故障与既有基线对应。额外选歌/谱库子集仍有 shared TestSearch 的 fixture 依赖缺失。范围、精确失败、命令与远端 TLS 限制见[本轮审查证据](../other/PROJECT_PROGRESS_AUDIT_20260909.md#本轮实际验证)。未跑全 core、publish 或实机；C5 完整闭门证据仍为 [2026-09-03](../other/SKIN_SYSTEM_C5_SCENE_EVENT_COMPLETION_HANDOFF_20260903.md)，不据此重签 campaign。

## 待人工验收

| 事项 | 状态 |
| --- | --- |
| 恢复基线：无外部皮肤、`.osk`、partial fallback、5K/7K/9K/14K、双皿与隔离 | **2026-07-14 已通过** |
| 已导入 `.osk` 的 BMS 普通短键编号帧动画 | **`V-001` 集中待验收**；不阻塞后续自动可证切片，不计为产品交付 |
| 已导入 `.osk` 的 BMS 长条头静态图/编号帧动画 | **`V-002` 集中待验收**；不阻塞后续自动可证切片，不计为产品交付 |
| 已导入 `.osk` 的 BMS 长条尾静态图/编号帧动画 | **`V-003` 集中待验收**；不阻塞后续自动可证切片，不计为产品交付；透明链底不是作者 `Suppress` |
| 已导入 `.osk` 的 BMS 长条 body 静态图/编号帧动画、安全宽度及三态过渡 | **`V-004` 集中待验收**；不阻塞后续自动可证切片，不计为产品交付 |
| analog scratch、真实 HID、LN/CN/HCN、长 BGM、密集键音真实谱 | 待做 |
| BGA 图序列/POOR/seek、Gimmick、Song Select 大库与最终发行 | 待做或待复核 |

## 当前风险

- 四个无 authority orphan blob 暂留并已保全；schema 57迁移保持owner=null，当前scanner也不会claim、去重或清理它们。
- C5已把28项catalog的适用性逐项落到runtime profile与production host：BMS profile 28项均有route（9K按catalog applicability为26项），mania 23项Supported，`object.mine`、`playfield.turntable`、`playfield.laser`、`bga.viewport`、`bga.frame`为版本化NotApplicable；不得将NotApplicable写成静默inherit或普遍unsupported。
- C5的package+layout+material+scene publication仍不等于C6最终ini/manifest/scene/script/素材整包门；C6新增sandbox consumer必须继续加入同一revision/lease协议。
- active实例固定到immutable owner，磁盘变化不会混入。已登记且current的managed/external内容可在安全screen显式点击`Reload current skin`准备新revision；ordinary Realm `.osk`也走同一协议，但没有作者update-import入口。gameplay/preview在source prepare前拒绝，不实现watcher。
- managed自动发现只在`OsuGame.LoadComplete`后执行一次；启动后新增direct child仍需重启发现，已有record的manual Reload不由scanner触发。
- configured selection仍只对typed startup/staged-import contention异步重试，generic mutation epoch跨越即fail-closed；manual Reload另有participant/source revision复核，不得把两条链或watcher混写。
- C1的Workspace Rename/Delete与full ManagedCopy已过退出门，但held-root mutation与journal/recovery仍不是filesystem transaction。C2冻结的current external/managed/ordinary mutation均先fallback+detach；external只pure-Realm remove且source零I/O，managed首个physical后的uncertain failure保持fallback并由durable recovery收口。
- 当前链底仍是程序化 `OmsSkin`，不是最终只读 `oms-simple.osk`。
- BMS 单套测试全绿不证明 mania 默认资源、真实选择链或视觉事件正确。
- C3/C4/C5已统一layout/material/scene/event authority；C6 sandbox继续加入同一协议。P1-L当前仍逐viewport创建BGA player，单content/decoder迁移未完成。
- abnormal-period 归档只能定点取证；50k dense、真实硬件和特殊 Gimmick 仍必须以 profiler/实机证据推进。

## 文档治理验证

2026-09-09：全量核对 P1-A～M、主约束、派生说明与 memory。P1-I 单轨仍待实现；P1-J 已完成的 lane 前置移出待办；纠正密度星、BGA 单内容源、播放器及皮肤作者能力漂移。非便携覆盖 marker 会切根，已修发行说明并登记 P1-F 包内说明欠账。覆盖表与同步范围见[审查报告](../other/PROJECT_PROGRESS_AUDIT_20260909.md)，校验结果见 [CHANGELOG](CHANGELOG.md)。

## 更新规则

- 本页只保留一个自动验证快照及治理边界；未重跑范围不得刷新验证日期，旧记录进入 [CHANGELOG.md](CHANGELOG.md)。
- 只记录仍影响决策的风险和未完成 gate；子线实现过程与旧数字留在对应子线。
