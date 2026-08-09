# OMS 当前开发状态

> 最后更新：2026-08-09
> 这里只保留当前事实、风险和最新验证。执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，历史见 [CHANGELOG.md](CHANGELOG.md)。

## 一句话状态

OMS 处于 Phase 1.x 后段与收口准备期，关键 release gate 尚未完成。Skin V1 采用“自动门后连续开发、视觉集中签收”：切片通过自动、合同、安全与回退验证后即可按依赖继续，未获用户签收时只能记为“实现／自动 gate 通过，视觉待验收”，不得计为产品交付或阶段完成。选中的用户 BMS 包已经闭合普通短键与长条 head/body/tail 四个可见组件的自动、合同、安全与回退 gate；集中视觉输入当前使用已导入 `.osk`，`V-001`～`V-004` 签收 **0/4**。这只关闭了 R2 的前置合同和首个 Note/LN 纵切自动闭环。R3/`SV1-2` 现已闭合schema 57 owner/启动scanner、exact-capsule factory/selection、configured managed selection与startup scanner的非阻塞协调、managed mutation authority/recovery、directory-only rename、fixed-source staged import后端及settings managed delete产品纵切；手工放入的managed folder可被发现、选择并经现有确认框物理删除，但rename/import没有非测试caller，thin staged-import stager/caller当前NO-GO。external仍未交付；current managed atomic reload/detach因没有真实caller及全consumer publication/detach协议已明确NO-GO。按release-ready玩家能力审慎估计Skin V1约完成三成，工程/安全地基高于产品完成度但不得混算；G1、`SV1-1`、Skin V1与release均未完成。详见[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)。

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
| 1 | R3 / `SV1-2` G1 可视文件夹 | 启动发现/选择、configured startup协调与settings managed delete已闭合；rename及staged import后端仍无应用caller/stager/UI；external未交付，atomic reload/detach当前NO-GO | 下一高价值候选是settings external只读工作区的注册→capture→选择/重启→解除注册完整纵切；禁止先建无caller backend，thin stager/caller继续NO-GO |
| 2 | R4 / Skin V1 后续合同 | `SV1-1` 整体仍未完成，`SV1-3`～`SV1-7` 未实现 | 补齐完整 layout/shared codec、所需 slot 三态与 scene/event/script runtime；这些不是进入 `SV1-2` 的前置 |
| 3 | 集中视觉签收 | R2 首个 Note/LN 纵切的四组件自动门已闭合；`V-001`～`V-004` 签收 0/4 | 继续登记到[集中视觉清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)，在 Skin V1/release 完成声明前统一签收 |
| 4 | P1-B/P1-D 输入 | 软件基线可用 | analog scratch、校准、真实 HID |
| 5 | P1-E/P1-G 人工验收 | 待闭合 | LN/CN/HCN、BGA、Song Select、发行 checklist |

## 皮肤系统主线摘要

- 当前保留独立 `[Bms]` 解析、`BmsLegacySkin`、`.osk` 导入、F1 静态配置与逐组件 fallback；选中的用户 BMS 包可为普通短键与长条 head/body/tail 提供静态图/编号帧动画。body 宽度只接受 finite 且 `0 < width <= 1`，否则逐字段回到 `0.5775`；素材与宽度绑定同一精确 package revision，用户包/default body 共用真实 Idle/Holding/Broken 状态宿主及 80ms 过渡。
- 程序化 `OmsSkin` 仍是实际链底，只作为迁移保障保留到 `oms-simple.osk` 通过 parity、完整性、原子恢复与实机 gate；最终产品渲染链由只读 canonical 包接管。
- Skin V1 的稳定方向是 mania/BMS 共享 neutral ini/asset/animation/event runtime、三态解析与 sandbox，ruleset topology/layout adapter 分离；当前窄纵切不代表这些能力已经完成。
- G1 的authority/path preflight、managed Windows native capture、pure capsule、production factory/guarded selection、schema 57 exact-owner启动发现、configured selection↔startup scanner非阻塞协调、mutation authority/recovery、directory-only rename、fixed-source staged import及managed delete已组成窄生产链；玩家可达的是启动发现/选择和现有settings确认式物理delete，rename/import仍只是production-assembled internal surface，仓库没有external→fixed provisional stager、非测试caller或UI。capsule、`551a`协调与delete journal直接保护真实选择/删除和玩家文件，具有产品安全价值；但无caller的rename/import专属后端不能继续按代码量计进度。下一纵切只评估具备真实settings caller、只读capture、selection/restart和unregister的external完整作者工作区；atomic reload/detach保持NO-GO。完整layout/shared codec、其它slot三态、scene/event/script、`oms-simple/oms-complex`、Authoring Kit与移除程序化产品视觉也均未完成。

恢复边界见 [2026-07-10 恢复审计](../other/SKIN_SYSTEM_RECOVERY_20260710.md)，当前实现与未完成 gate 见 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)，V1 完成定义见 [架构审计](../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

## 子线快照

| 子线 | 当前状态 |
| --- | --- |
| P1-A | `SV1-0` 全过；普通短键与长条 head/body/tail 四组件自动 gate 通过、`V-001`～`V-004` 集中待验收；R3启动发现/选择、startup协调与settings managed delete已闭合，rename/import仍为无caller的安全后端 |
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

### R3 / `SV1-2` managed chartskin delete：2026-08-02

现有settings删除按钮/确认框现在通过独立fresh-authoritative `CanDelete`与manager-owned async caller进入专用managed delete；普通Realm `.osk`保持既有soft delete，旧通用folder `CanModify/Delete`、protected/fixed、external、foreign/null owner和非法path继续fail-closed。eligible direct-child的durable Prepared同时绑定operation-derived tombstone、Realm fingerprint、exact source-node manifest，并在detach前固化`NotRequired`或`ProtectedPairCommitted`；held-root no-follow清理要求same-session完整manifest，fresh recovery只接受部分崩溃后的durable子集。已持有节点不能被移出；preflight后竞态新增的foreign node不进入delete list且绝不删除，若导致partial exact cleanup后root失败则保留journal/Realm冻结。current目标必须在任何物理步骤前真实提交exact protected `OmsSkin` pair；首步前authority漂移在exact receipt仍可证明时安全回滚，receipt/落盘歧义才冻结，首步后只由journal/recovery收口。`NotRequired`恢复不要求OMS record，current committed路径继续要求exact protected fallback Realm record；scanner/selection/generic mutation、typed startup/staged retry、update-thread non-blocking与shutdown claim/join回归均保留。core managed mutation+contract broad **281/281**、产品类 **62/62**、mania skin **182/182**、BMS full **1530/1530**，core skin broad **911/917**仍为同六项既有失败，Release **0 error / 20 known warnings**。精确实现、兼容说明与额外full基线见[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)。本切没有实现thin stager、external、reload/detach、scene/script或canonical包，视觉仍0/4。

### R3 / `SV1-2` configured managed selection ↔ startup scanner：2026-08-01

确定性产品交错先复现了慢capture在startup scanner占用最终发布边界时保留旧`OmsSkin` pair的风险。startup recovery→scanner现以typed startup sequence lease占用共享coordinator；只有已经开始准备且确实跨越startup/staged-import的managed selection会在后台等待exact completion，再回到update scheduler做fresh retry。retry重新验证generation/current pair、authoritative Realm record、path、owner、freeze、allowlist并重新capture/factory；preparation observation同时记录startup与generic mutation epoch，防止rename/delete/普通mutation在direct、deferred或chained窗口借用startup重试资格。等待不阻塞update thread，manual managed selection在scanner期间仍即时fail-closed，普通Realm `.osk`不进该链，取消/shutdown会封门、回收capsule/CTS并join worker。产品夹具 **52/52**、core managed **275/275**、mania skin **182/182**、BMS full **1520/1520**；core skin broad **863/869**，六项失败与既有基线完全相同；Release **0 error / 20 known warnings**。精确实现与验证见[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)。本切没有新增UI、stager、delete、external、reload、scene/script或canonical包。

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
- configured managed selection只对typed startup/staged-import contention异步重试；generic mutation epoch一旦跨越即fail-closed。该协调不是watcher或热重载，启动后新增/原位修改仍需重启；后续变更必须保留direct、completed、deferred与chained四类交错回归。
- directory-only rename与fixed-source staged import已有internal production纵切但无非测试caller；仓库也没有把玩家外部来源安全复制到fixed provisional slot的production stager。当前因此不应把stager称作thin切片，也不得继续横向扩张无caller foundation。managed delete已由现有settings入口消费，但其tombstone/recovery同样不是filesystem transaction；external未完成，atomic reload/detach已经产品审计为NO-GO。
- 当前链底仍是程序化 `OmsSkin`，不是最终只读 `oms-simple.osk`。
- BMS 单套测试全绿不证明 mania 默认资源、真实选择链或视觉事件正确。
- `LongNoteBodyWidth` 已有首个安全合法域；完整几何 descriptor 仍归 R4，统一前 playfield 与 gauge/combo/BGA 仍可能脱节。
- lane keysound timeline 仍有 5K/7K 边缘轨及 14K 第二皿丢失风险，另立 P1-K/P1-J 切片修复。
- abnormal-period 归档只能定点取证；50k dense、真实硬件和特殊 Gimmick 仍必须以 profiler/实机证据推进。

## 更新规则

- 本页只保留一个产品/runtime 验证快照和一个不冒充产品验证的文档治理边界；旧记录进入 [CHANGELOG.md](CHANGELOG.md)。
- 只记录仍影响决策的风险和未完成 gate；子线实现过程与旧数字留在对应子线。
