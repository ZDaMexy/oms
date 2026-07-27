# OMS 当前开发状态

> 最后更新：2026-07-27
> 这里只保留当前事实、风险和最新验证。执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，历史见 [CHANGELOG.md](CHANGELOG.md)。

## 一句话状态

OMS 处于 Phase 1.x 后段与收口准备期，关键 release gate 尚未完成。Skin V1 采用“自动门后连续开发、视觉集中签收”：切片通过自动、合同、安全与回退验证后即可按依赖继续，未获用户签收时只能记为“实现／自动 gate 通过，视觉待验收”，不得计为产品交付或阶段完成。选中的用户 BMS 包已经闭合普通短键与长条 head/body/tail 四个可见组件的自动、合同、安全与回退 gate；集中视觉输入当前使用已导入 `.osk`，`V-001`～`V-004` 签收 **0/4**。这只关闭了 R2 的前置合同和首个 Note/LN 纵切自动闭环。R3/`SV1-2` 现已闭合schema 57 owner/启动scanner、exact-capsule factory/selection及managed mutation authority/recovery公共地基：共享线性化、版本化durable journal、启动先恢复后scanner、歧义冻结和current delete受保护`OmsSkin` pair确认均有自动合同。foundation不含物理或Realm写入，也未开放任何rename/import/delete UI；下一门是rename独立端到端切片，external及atomic reload/detach仍缺，因此G1、`SV1-1`、Skin V1与release均未完成。详见[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)。

## 产品与仓库基线

- Windows-only，保留 osu!mania + 第一类 BMS；Osu/Taiko/Catch 已删除。
- 离线优先；Phase 3 前 OMS 私有服务与默认 endpoint 保持为空。用户主动添加公共 BMS 难度表 URL 是既有窄例外，不代表 OMS 在线产品能力已开放。
- BMS 直读 `chartbms/`，mania 直读 `chartmania/`；发行支持 `portable.ini → data/` 与 `storage.ini` 自定义根。
- 主要入口：`osu.Desktop.slnf`；BMS 主开发目标：`osu.Game.Rulesets.Bms`；统一输入：`oms.Input`。
- 当前协作分支为 `master`；可信恢复锚点是 `ef56507`，后续皮肤工作只能按小切片前进。

已关闭前置：P1-A `SV1-0` 的自动、schema 56 数据与用户恢复实机 gate 已全部通过；异常 copy 已定点移除，OMS fixed-ID 已修正，迁移归档与四个无 authority orphan blob 继续保全。2026-07-16 文档与 memory 健康治理也已完成；它只归位事实、历史和路由，不改变产品行为或 gate 结论。

## 当前执行门

| 顺序 | 工作面 | 当前状态 | 下一检查点 |
| --- | --- | --- | --- |
| 1 | R3 / `SV1-2` G1 可视文件夹 | schema 57 owner、启动发现、factory/选择及mutation authority/recovery foundation已闭合；尚无真实删改、external与reload | 依次推进rename、staged import、delete独立端到端切片；之后分别闭合external与整包原子reload/detach |
| 2 | R4 / Skin V1 后续合同 | `SV1-1` 整体仍未完成，`SV1-3`～`SV1-7` 未实现 | 补齐完整 layout/shared codec、所需 slot 三态与 scene/event/script runtime；这些不是进入 `SV1-2` 的前置 |
| 3 | 集中视觉签收 | R2 首个 Note/LN 纵切的四组件自动门已闭合；`V-001`～`V-004` 签收 0/4 | 继续登记到[集中视觉清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)，在 Skin V1/release 完成声明前统一签收 |
| 4 | P1-B/P1-D 输入 | 软件基线可用 | analog scratch、校准、真实 HID |
| 5 | P1-E/P1-G 人工验收 | 待闭合 | LN/CN/HCN、BGA、Song Select、发行 checklist |

## 皮肤系统主线摘要

- 当前保留独立 `[Bms]` 解析、`BmsLegacySkin`、`.osk` 导入、F1 静态配置与逐组件 fallback；选中的用户 BMS 包可为普通短键与长条 head/body/tail 提供静态图/编号帧动画。body 宽度只接受 finite 且 `0 < width <= 1`，否则逐字段回到 `0.5775`；素材与宽度绑定同一精确 package revision，用户包/default body 共用真实 Idle/Holding/Broken 状态宿主及 80ms 过渡。
- 程序化 `OmsSkin` 仍是实际链底，只作为迁移保障保留到 `oms-simple.osk` 通过 parity、完整性、原子恢复与实机 gate；最终产品渲染链由只读 canonical 包接管。
- Skin V1 的稳定方向是 mania/BMS 共享 neutral ini/asset/animation/event runtime、三态解析与 sandbox，ruleset topology/layout adapter 分离；当前窄纵切不代表这些能力已经完成。
- G1 的authority/path preflight、managed Windows native capture、pure capsule、production factory/guarded selection、schema 57 exact-owner启动发现及mutation authority/recovery foundation已组成窄生产链；启动scan只在完整稳定inventory上单事务维护自己的记录，并与selection/mutation共用线性化边界。有效未决journal冻结相关路径，无法可信解析时全局冻结；external capture、任一真实rename/import/delete与原子重载/detach仍未完成。完整layout/shared codec、其它slot三态、scene/event/script、`oms-simple/oms-complex`、Authoring Kit与移除程序化产品视觉也均未完成。

恢复边界见 [2026-07-10 恢复审计](../other/SKIN_SYSTEM_RECOVERY_20260710.md)，当前实现与未完成 gate 见 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)，V1 完成定义见 [架构审计](../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

## 子线快照

| 子线 | 当前状态 |
| --- | --- |
| P1-A | `SV1-0` 全过；普通短键与长条 head/body/tail 四组件自动 gate 通过、`V-001`～`V-004` 集中待验收；R3启动发现/factory/选择及mutation foundation已闭合，当前进入rename，再按staged import、delete、external、reload推进 |
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

### R3 / `SV1-2` managed mutation authority/recovery foundation：2026-07-27

受管目录scanner、selection、mutation与recovery现共用一个线性化authority：scanner从held-root discovery到Realm reconcile返回全程持有边界，selection最终在边界内刷新Realm并发布本Realm authoritative live record；`SkinManager`构造期先恢复，启动worker再以同一外层lease幂等执行recovery→scanner。既有record资格、held no-follow source identity、root-bound absent target slot、fixed operation-derived staging source及immutable新记录publication plan均已冻结；plan不是Realm writer。

canonical version-1 journal严格校验schema/type/checksum/UTF-8/大小，flush后原子replace，phase单调且terminal不可重写。有效歧义保留journal并冻结关联路径，invalid/unknown/IO全局冻结；scanner的正向与negative reconcile均服从冻结。current delete foundation只在exact durable receipt下确认程序化`OmsSkin`的`CurrentSkinInfo`/`CurrentSkin` pair；split pair或失败拒绝并安全abort，没有执行删除。

focused合并为authority/journal/coordinator/scanner/native/lifecycle **107/107**，BMS production selection/fallback **24/24**；扩大回归core skin **337/341**（4项均为既有removed Osu archive fixture，Argon旧失败本轮通过）、mania skin **182/182**、BMS full **1492/1492**。三工程targeted format verify通过，`osu.Desktop.slnf` Release **0 error / 18 emitted known warnings**；文档健康检查与`git diff --check`通过，最终独立审查blocker/major **0/0**。本轮未启动GUI或操控桌面，视觉签收仍0/4；真实rename/import/delete、external与atomic reload/detach均未实现。

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
- managed folder active实例已固定到immutable capsule，磁盘原位变化不会混入当前结果，也不会自动reload；新revision仍须在`SV1-2`以新实例、全consumer publication barrier和旧owner安全退役闭合。
- managed自动发现只在`OsuGame.LoadComplete`后执行一次；启动后新增或原位修改目录不会被watch，也不会自动reload，当前需重启重新发现。
- managed mutation foundation没有物理写primitive、operation-specific recovery handler或Realm新记录publisher；rename展示/目录语义、staged import copy/move/冲突和delete实际时序仍须分别过门，现有通用UI继续冻结。
- 当前链底仍是程序化 `OmsSkin`，不是最终只读 `oms-simple.osk`。
- BMS 单套测试全绿不证明 mania 默认资源、真实选择链或视觉事件正确。
- `LongNoteBodyWidth` 已有首个安全合法域；完整几何 descriptor 仍归 R4，统一前 playfield 与 gauge/combo/BGA 仍可能脱节。
- lane keysound timeline 仍有 5K/7K 边缘轨及 14K 第二皿丢失风险，另立 P1-K/P1-J 切片修复。
- abnormal-period 归档只能定点取证；50k dense、真实硬件和特殊 Gimmick 仍必须以 profiler/实机证据推进。

## 更新规则

- 本页只保留一个产品/runtime 验证快照和一个不冒充产品验证的文档治理边界；旧记录进入 [CHANGELOG.md](CHANGELOG.md)。
- 只记录仍影响决策的风险和未完成 gate；子线实现过程与旧数字留在对应子线。
