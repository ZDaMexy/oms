# OMS 当前开发状态

> 最后更新：2026-08-13
> 这里只保留当前事实、风险和最新验证。执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，历史见 [CHANGELOG.md](CHANGELOG.md)。

## 一句话状态

OMS 处于 Phase 1.x 后段与收口准备期，关键 release gate 尚未完成。P1-A / Skin V1 的 `C1` 已闭合 Folder Skin Workspace、external 只读注册/显式选择/configured restart/pure-Realm noncurrent unregister、exact-set managed mutation、single-v3 ManagedCopy、managed Open/Rename/Delete、动态脱敏 journal 支持面和 ordinary `.osk` bounded ingress/zero-residue receipt；宽回归、Release 与独立终审已过，当前为 **`1/7 closed，C2 active`**。这只是作者文件工作区与安全导入纵切闭门；current consumer revision publication/reload/detach/retire 仍待 `C2` 实现，`V-001`～`V-004` 签收仍为 **0/4**，G1、`SV1-2`、Skin V1、`SV1-1` 和 release 均未完成。详见[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)与[C1 完成交接](../other/SKIN_SYSTEM_C1_COMPLETION_HANDOFF_20260813.md)。

## 产品与仓库基线

Skin V1后续进度以[P1-A `C1`～`C7`持久campaign燃尽](../subline/P1-A/DEVELOPMENT_PLAN.md)报告。`C1` 已通过真实caller/consumer、失败恢复、宽测试、Release、文档和独立终审的退出门；当前是`1/7 closed，C2 active`，不用C1的完成证据代替C2的全consumer原子发布与owner退役证明。

`1/7`是非等权硬退出门计数，不换算14%或线性工期。产品核算确认C1主要交付均有真实caller；selection/import/ManagedCopy等涉及包生效的链另有BMS/mania production consumer证据，Open/Rename/Delete/Unregister/support则形成直接用户结果。底层复杂度直接保护用户目录与共享资源；但最终用户可见Skin V1仍处早期，统一revision生命周期、layout、shared codec/三态、scene/event、sandbox、canonical双包与发行闭环均未交付。

- Windows-only，保留 osu!mania + 第一类 BMS；Osu/Taiko/Catch 已删除。
- 离线优先；Phase 3 前 OMS 私有服务与默认 endpoint 保持为空。用户主动添加公共 BMS 难度表 URL 是既有窄例外，不代表 OMS 在线产品能力已开放。
- BMS 直读 `chartbms/`，mania 直读 `chartmania/`；发行支持 `portable.ini → data/` 与 `storage.ini` 自定义根。
- 主要入口：`osu.Desktop.slnf`；BMS 主开发目标：`osu.Game.Rulesets.Bms`；统一输入：`oms.Input`。
- 当前协作分支为 `master`；可信恢复锚点是 `ef56507`，后续皮肤工作只能按小切片前进。

已关闭前置：P1-A `SV1-0` 的自动、schema 56 数据与用户恢复实机 gate 已全部通过；异常 copy 已定点移除，OMS fixed-ID 已修正，迁移归档与四个无 authority orphan blob 继续保全。2026-07-16 文档与 memory 健康治理也已完成；它只归位事实、历史和路由，不改变产品行为或 gate 结论。

## 当前执行门

| 顺序 | 工作面 | 当前状态 | 下一检查点 |
| --- | --- | --- | --- |
| 1 | R3 / `SV1-2` G1 可视文件夹 | `C1` 已闭合作者工作区/ManagedCopy/journal/archive安全链；`C2 active`，整包原子reload/detach仍未实现 | 以真实可达触发和当前全部production consumer闭合revision publication/detach/retire；新增consumer在`C3`～`C6`同切加入 |
| 2 | R4 / Skin V1 后续合同 | `SV1-1` 整体仍未完成，`SV1-3`～`SV1-7` 未实现 | 补齐完整 layout/shared codec、所需 slot 三态与 scene/event/script runtime；这些不是进入 `SV1-2` 的前置 |
| 3 | 集中视觉签收 | R2 首个 Note/LN 纵切的四组件自动门已闭合；`V-001`～`V-004` 签收 0/4 | 继续登记到[集中视觉清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)，在 Skin V1/release 完成声明前统一签收 |
| 4 | P1-B/P1-D 输入 | 软件基线可用 | analog scratch、校准、真实 HID |
| 5 | P1-E/P1-G 人工验收 | 待闭合 | LN/CN/HCN、BGA、Song Select、发行 checklist |

## 皮肤系统主线摘要

- 当前保留独立 `[Bms]` 解析、`BmsLegacySkin`、`.osk` 导入、F1 静态配置与逐组件 fallback；选中的用户 BMS 包可为普通短键与长条 head/body/tail 提供静态图/编号帧动画。body 宽度只接受 finite 且 `0 < width <= 1`，否则逐字段回到 `0.5775`；素材与宽度绑定同一精确 package revision，用户包/default body 共用真实 Idle/Holding/Broken 状态宿主及 80ms 过渡。
- 程序化 `OmsSkin` 仍是实际链底，只作为迁移保障保留到 `oms-simple.osk` 通过 parity、完整性、原子恢复与实机 gate；最终产品渲染链由只读 canonical 包接管。
- Skin V1 的稳定方向是 mania/BMS 共享 neutral ini/asset/animation/event runtime、三态解析与 sandbox，ruleset topology/layout adapter 分离；当前窄纵切不代表这些能力已经完成。
- G1 的managed scanner/selection/mutation基线与C1闭合的Folder Skin Workspace产品链已经成为C2输入：external持有no-follow物理证明，service-owner只管Realm记录，copy bytes只来自immutable capsule，目录来自同次manifest，external源不写改删。ordinary `.osk` 继续是hash-backed Realm package，新增bounded archive ingress与精确rollback receipt不改变它的选择/编辑/导出语义。current consumer revision protocol仍归`C2`，最终整包门到`C6`关闭；完整layout/shared codec、其它slot三态、scene/event/script、`oms-simple/oms-complex`、Authoring Kit与移除程序化产品视觉均未完成。

恢复边界见 [2026-07-10 恢复审计](../other/SKIN_SYSTEM_RECOVERY_20260710.md)，当前实现与未完成 gate 见 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)，V1 完成定义见 [架构审计](../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

## 子线快照

| 子线 | 当前状态 |
| --- | --- |
| P1-A | `SV1-0` 全过；Note/LN四组件自动 gate 通过、`V-001`～`V-004`待验收；C1作者工作区/ManagedCopy/archive已闭合，当前`1/7 closed，C2 active` |
| P1-B | 输入基础链可用；analog scratch/真实硬件未闭合 |
| P1-C | 判定 parity 主体已落；常驻速度反馈卡已删除，不作为当前能力 |
| P1-D | deadzone/sensitivity/live diagnostics 未完成 |
| P1-E | gameplay 主链具备；真实 LN/CN/HCN 组合验收待做 |
| P1-F | portable 离线发行基线已验证，最终 release 复核待做 |
| P1-G | 人工验收汇总待做 |
| P1-H | 文件系统谱库与多根扫描基线已落；删除/失效/去重仍是 backlog |
| P1-I | 选歌分组/筛选/搜索主功能已落；拖拽 headless 与 shared visual 待补 |
| P1-J | 普通密度音频/性能主故障已收口；转谱 LN/50k/人工清单待做 |
| P1-K | K1–K12 主体阶段性收口；lane timeline 上界与 sparse keymode authority 待修 |
| P1-L | BGA 播放主链已落；内容/viewport 解耦、逐谱视觉与反向滚动待做 |
| P1-M | 规划完成，未开工 |

入口和下一道门见 [子线路由](../subline/README.md)。

## 最近一次验证

### R3 / `SV1-2` C1完成：2026-08-13

`osu.Game` Debug build为**0 error**（仅9个既有MessagePack `NU1902`）；core C1 focused **490/490**，archive/receipt 合并门 **84/84**，BMS 产品组合 **118/118**，mania Skin **182/182**，BMS full **1586/1586**。core Skin 为 **679/683**，4项失败均是依赖已移除 Osu ruleset mode 0 fixture 的已知OMS基线，与C1无关。`osu.Desktop.slnf` Release **0 error**，仅9个既有MessagePack `NU1902`。external与receipt最终独立复审均为blocker/major/moderate **0/0/0**；完成边界见[C1 完成交接](../other/SKIN_SYSTEM_C1_COMPLETION_HANDOFF_20260813.md)。

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
- 当前可见纵切只覆盖 BMS 普通短键与 LN head/body/tail，不含 key、mania、完整 layout/三态或 scene/script；Skin V1 不能据此宣称可用。
- 当前逐组件异步替换不等于 `SV1-2` 的整包原子重载；runtime 资源预算也不等于 importer 的 zip-bomb gate。
- managed folder active实例已固定到immutable capsule，磁盘原位变化不会混入当前结果，也不会自动reload；当前production consumer的新revision publication与旧owner退役归`C2`，`C3`～`C6`新增consumer须逐次加入，最终整包reload/G1自动门只在`C6`闭合。
- managed自动发现只在`OsuGame.LoadComplete`后执行一次；启动后新增或原位修改目录不会被watch，也不会自动reload，当前需重启重新发现。
- configured managed selection只对typed startup/staged-import contention异步重试；generic mutation epoch一旦跨越即fail-closed。该协调不是watcher或热重载，启动后新增/原位修改仍需重启；后续变更必须保留direct、completed、deferred与chained四类交错回归。
- C1的Workspace Rename/Delete与full ManagedCopy已过退出门，但held-root move/delete/copy与journal/recovery仍不是filesystem transaction，不得退化成thin/arbitrary-path copy。current external unregister必须等coherent fallback/新revision发布且全consumer detach后才可解除注册；这与atomic reload/detach均归`C2`。
- 当前链底仍是程序化 `OmsSkin`，不是最终只读 `oms-simple.osk`。
- BMS 单套测试全绿不证明 mania 默认资源、真实选择链或视觉事件正确。
- `LongNoteBodyWidth` 已有首个安全合法域；完整几何 descriptor 仍归 R4，统一前 playfield 与 gauge/combo/BGA 仍可能脱节。
- lane keysound timeline 仍有 5K/7K 边缘轨及 14K 第二皿丢失风险，另立 P1-K/P1-J 切片修复。
- abnormal-period 归档只能定点取证；50k dense、真实硬件和特殊 Gimmick 仍必须以 profiler/实机证据推进。

## 更新规则

- 本页只保留一个产品/runtime 验证快照和一个不冒充产品验证的文档治理边界；旧记录进入 [CHANGELOG.md](CHANGELOG.md)。
- 只记录仍影响决策的风险和未完成 gate；子线实现过程与旧数字留在对应子线。
