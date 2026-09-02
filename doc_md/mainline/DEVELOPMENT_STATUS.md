# OMS 当前开发状态

> 最后更新：2026-09-02
> 这里只保留当前事实、风险和最新验证。执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，历史见 [CHANGELOG.md](CHANGELOG.md)。

## 一句话状态

OMS 处于 Phase 1.x 后段与收口准备期，关键 release gate 尚未完成。P1-A / Skin V1 的`C1`作者工作区/archive、`C2`当前consumer revision、`C3` P1-K前置+唯一gameplay layout与`C4` public catalog/shared codec/三态resolved material均已闭合；当前为 **`4/7 closed，C5 active`**，转入声明式scene/animation/event与剩余optional slot production。`V-001`～`V-004`签收仍为 **0/4**，G1最终整包门、Skin V1、`SV1-1`和release均未完成。详见[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)。

## 产品与仓库基线

Skin V1后续进度以[P1-A `C1`～`C7`持久campaign燃尽](../subline/P1-A/DEVELOPMENT_PLAN.md)报告。`C1`～`C4`均已通过退出门，当前是`4/7 closed，C5 active`；C4冻结28项public catalog、唯一shared codec、显式`Provide/Inherit/Suppress`、BMS/mania真实material consumer及C2+C3 package+layout+material publication，完成边界见[C4交接](../other/SKIN_SYSTEM_C4_CODEC_MATERIAL_COMPLETION_HANDOFF_20260831.md)。

`4/7`是非等权硬退出门计数，不换算线性工期。C1～C4均由真实caller、production consumer或直接用户结果闭合；底层复杂度直接保护用户目录、共享资源、lane/keymode authority、统一geometry/material与跨revision owner生命周期。但最终用户可见Skin V1仍处早期，scene/event、剩余optional slot、sandbox、canonical双包与发行闭环均未交付。

- Windows-only，保留 osu!mania + 第一类 BMS；Osu/Taiko/Catch 已删除。
- 离线优先；Phase 3 前 OMS 私有服务与默认 endpoint 保持为空。用户主动添加公共 BMS 难度表 URL 是既有窄例外，不代表 OMS 在线产品能力已开放。
- BMS 直读 `chartbms/`，mania 直读 `chartmania/`；发行支持 `portable.ini → data/` 与 `storage.ini` 自定义根。
- 主要入口：`osu.Desktop.slnf`；BMS 主开发目标：`osu.Game.Rulesets.Bms`；统一输入：`oms.Input`。
- 当前协作分支为 `master`；可信恢复锚点是 `ef56507`，后续皮肤工作只能按小切片前进。

已关闭前置：P1-A `SV1-0` 的自动、schema 56 数据与用户恢复实机 gate 已全部通过；异常 copy 已定点移除，OMS fixed-ID 已修正，迁移归档与四个无 authority orphan blob 继续保全。2026-07-16 文档与 memory 健康治理也已完成；它只归位事实、历史和路由，不改变产品行为或 gate 结论。

## 当前执行门

| 顺序 | 工作面 | 当前状态 | 下一检查点 |
| --- | --- | --- | --- |
| 1 | R3/R4 / Skin V1 storage + layout/material | `C1`～`C4`已闭合 | 保持P1-K authority、唯一immutable layout/material与C2 package+layout+material合同，不重开或旁路 |
| 2 | R4 / Skin V1 后续合同 | `C5 active`；`SV1-1`整体仍未完成 | 交付声明式scene/animation/event与剩余optional slot production；全部新host加入同一revision协议 |
| 3 | 集中视觉签收 | R2 首个 Note/LN 纵切的四组件自动门已闭合；`V-001`～`V-004` 签收 0/4 | 继续登记到[集中视觉清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)，在 Skin V1/release 完成声明前统一签收 |
| 4 | P1-B/P1-D 输入 | 软件基线可用 | analog scratch、校准、真实 HID |
| 5 | P1-E/P1-G 人工验收 | 待闭合 | LN/CN/HCN、BGA、Song Select、发行 checklist |

## 皮肤系统主线摘要

- 当前保留独立 `[Bms]` 解析、`BmsLegacySkin`、`.osk` 导入、F1 静态配置与逐组件 fallback；选中的用户 BMS 包可为普通短键与长条 head/body/tail 提供静态图/编号帧动画。body 宽度只接受 finite 且 `0 < width <= 1`，否则逐字段回到 `0.5775`；素材与宽度绑定同一精确 package revision，用户包/default body 共用真实 Idle/Holding/Broken 状态宿主及 80ms 过渡。
- 程序化 `OmsSkin` 仍是实际链底，只作为迁移保障保留到 `oms-simple.osk` 通过 parity、完整性、原子恢复与实机 gate；最终产品渲染链由只读 canonical 包接管。
- Skin V1 的稳定方向是 mania/BMS 共享neutral ini/asset/animation/event runtime、三态解析与sandbox，ruleset topology/layout adapter分离；C4已交付共同catalog/codec/resolver与Note/Hold/Key production证明，但未把C5 scene/event和全部optional slot冒充当前能力。
- G1 的managed scanner/selection/mutation基线与C1 Folder Skin Workspace已成为C2冻结输入：external永久只读，copy bytes只来自immutable capsule，目录来自同次manifest；ordinary `.osk`继续是hash-backed Realm package。C2已用explicit manual Reload统一三源current revision并稳定关闭legacy update/editor旁路；C3/C4又把唯一layout与resolved material作为同一package+layout+material publication及participant/lease加入协议。C5～C6新增scene/script consumer仍须同切加入，最终整包门到C6。scene/event/script、canonical双包/Authoring Kit与移除程序化产品视觉均未完成。

恢复边界见 [2026-07-10 恢复审计](../other/SKIN_SYSTEM_RECOVERY_20260710.md)，当前实现与未完成 gate 见 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)，V1 完成定义见 [架构审计](../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

## 子线快照

| 子线 | 当前状态 |
| --- | --- |
| P1-A | `SV1-0` 全过；Note/LN四组件自动 gate 通过、`V-001`～`V-004`待验收；C1～C4已闭合，当前`4/7 closed，C5 active` |
| P1-B | 输入基础链可用；analog scratch/真实硬件未闭合 |
| P1-C | 判定 parity 主体已落；常驻速度反馈卡已删除，不作为当前能力 |
| P1-D | deadzone/sensitivity/live diagnostics 未完成 |
| P1-E | gameplay 主链具备；真实 LN/CN/HCN 组合验收待做 |
| P1-F | portable 离线发行基线已验证，最终 release 复核待做 |
| P1-G | 人工验收汇总待做 |
| P1-H | 文件系统谱库与多根扫描基线已落；删除/失效/去重仍是 backlog |
| P1-I | 选歌分组/筛选/搜索主功能已落；拖拽 headless 与 shared visual 待补 |
| P1-J | 普通密度音频/性能主故障已收口；转谱 LN/50k/人工清单待做 |
| P1-K | K1–K12主体阶段性收口；C3所需lane timeline上界、sparse keymode authority与真实末端lane发声已闭合，其余状态见子线 |
| P1-L | BGA 播放主链已落；内容/viewport 解耦、逐谱视觉与反向滚动待做 |
| P1-M | 规划完成，未开工 |

入口和下一道门见 [子线路由](../subline/README.md)。

## 最近一次验证

### R3/R4 / Skin V1 C4完成：2026-09-02

core public catalog/codec/resolver/revision/beatmap-local focused **141/141**；mania C4 relevant **172/172**；BMS C4 relevant/current-revision/managed-candidate product **315/315、197/197、115/115**，其中real WorkingBeatmap不可达与carrier取消所有权均走production fixture；P1-K decoder/converter/cache、projection、真实shared keysound与converted store **102/102、24/24、14/14、2/2**。core Skin **1110/1116**，六项失败名称/消息逐字符匹配精确既有基线；mania Skin **193/193**，mania full **838/842**，四项HoldNote失败同样逐字符匹配既有基线；BMS Skin **726/726**，BMS full **1687/1687**且无hang artifact。formatter后重新build的core/BMS/mania C4 production focused仍为 **141/141、315/315、172/172**。Release **0 error / 20 emitted known warnings**（9项既有MessagePack `NU1902`在restore/build重复为18次，另有既有BMS tests `CS8600`/`CA2007`）；六工程97个C#文件的默认targeted formatter、文档门与diff检查通过。public authority、production bypass、revision/concurrency、产品价值/dead foundation四类独立终审均GO，blocker/major **0/0**。燃尽只推进至 **`4/7 closed，C5 active`**；详见[C4完成交接](../other/SKIN_SYSTEM_C4_CODEC_MATERIAL_COMPLETION_HANDOFF_20260831.md)。

### R3/R4 / Skin V1 C3完成：2026-08-30

P1-K decoder/converter authority **176/176**、BMS→mania projection **24/24**、BMS/converted-mania shared keysound实际发声 **14/14 + 2/2**；BMS C3 relevant **316/316**、mania C3 **27/27**、core focused **56/56**、产品并发/原子性 **17/17**。formatter后宽关键集core/BMS/mania **47/47、235/235、51/51**；最终owner审计红绿硬化后critical复验为core/mania/BMS **48/48、51/51、37/37**。core canonical `~Skin` **1164/1170**（六项精确既有基线），mania `~Skin` **209/209**、mania full **854/858**（四项既有AutoGeneration基线），BMS `~Skin` **802/802**、BMS full **1763/1763**且无hang sequence；Release **0 error / 9 known warnings**。唯一publication、reachable bypass、P1-K authority、participant/owner与并发独立终审为blocker/major/moderate/minor **0/0/0/0**。燃尽推进至 **`3/7 closed，C4 active`**；详见[C3完成交接](../other/SKIN_SYSTEM_C3_LAYOUT_COMPLETION_HANDOFF_20260830.md)。

### R3 / `SV1-2` C2完成：2026-08-24

core focused **204/204**，PendingAsync ownership visual/host **11/11**，完整真实C2产品路径 **314/314**；core canonical `~Skin` **1137/1143**的六项失败与精确既有基线相同，mania `~Skin` **182/182**，BMS `~Skin` **796/796**，BMS full **1670/1670**且`--blame-hang 5m`无hang sequence。Release含restore首跑 **0 error / 20 known warnings（41.88s）**，formatter后`--no-restore`复验 **0 error / 11 known warnings（36.58s）**；targeted formatter均exit 0。participant/holder、reachable bypass、concurrency/owner、tests/product-contract四项独立终审均为blocker/major/moderate **0/0/0**。完成事实见[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)，稳定合同见[P1-A技术约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)，历史验证见[P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md)。

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
- 当前public作者合同已有28项ID与完整三态，但C4真实可见consumer只覆盖BMS Note/LN及mania Note/Hold/KeyVisual；其余optional slot的scene/script host未进入production，Skin V1不能据目录数量宣称完成。
- C4的package+layout+material triple仍不等于C6最终ini/manifest/scene/script/素材整包门；C5～C6新增consumer必须继续加入同一revision/lease协议。
- active实例固定到immutable owner，磁盘变化不会混入。已登记且current的managed/external内容可在安全screen显式点击`Reload current skin`准备新revision；ordinary Realm `.osk`也走同一协议，但没有作者update-import入口。gameplay/preview在source prepare前拒绝，不实现watcher。
- managed自动发现只在`OsuGame.LoadComplete`后执行一次；启动后新增direct child仍需重启发现，已有record的manual Reload不由scanner触发。
- configured selection仍只对typed startup/staged-import contention异步重试，generic mutation epoch跨越即fail-closed；manual Reload另有participant/source revision复核，不得把两条链或watcher混写。
- C1的Workspace Rename/Delete与full ManagedCopy已过退出门，但held-root mutation与journal/recovery仍不是filesystem transaction。C2冻结的current external/managed/ordinary mutation均先fallback+detach；external只pure-Realm remove且source零I/O，managed首个physical后的uncertain failure保持fallback并由durable recovery收口。
- 当前链底仍是程序化 `OmsSkin`，不是最终只读 `oms-simple.osk`。
- BMS 单套测试全绿不证明 mania 默认资源、真实选择链或视觉事件正确。
- C3/C4已关闭playfield、gauge/combo、HUD、BGA viewport与resolved Note/Hold material的第二套authority；后续风险是C5～C6新增scene/script consumer是否继续消费同一publication，不能另建布局、lookup或material merge。
- abnormal-period 归档只能定点取证；50k dense、真实硬件和特殊 Gimmick 仍必须以 profiler/实机证据推进。

## 更新规则

- 本页只保留一个产品/runtime 验证快照和一个不冒充产品验证的文档治理边界；旧记录进入 [CHANGELOG.md](CHANGELOG.md)。
- 只记录仍影响决策的风险和未完成 gate；子线实现过程与旧数字留在对应子线。
