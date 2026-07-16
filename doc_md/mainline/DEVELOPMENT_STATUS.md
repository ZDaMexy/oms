# OMS 当前开发状态

> 最后更新：2026-07-17
> 这里只保留当前事实、风险和最新验证。执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，历史见 [CHANGELOG.md](CHANGELOG.md)。

## 一句话状态

OMS 处于 Phase 1.x 后段与收口准备期，关键 release gate 尚未完成。Skin V1 采用“自动门后连续开发、视觉集中签收”：切片通过自动、合同、安全与回退验证后即可按依赖继续，未获用户签收时只能记为“实现／自动 gate 通过，视觉待验收”，不得计为产品交付或阶段完成。managed `.osk` BMS 普通短键与长条 head/body/tail 四个可见组件的自动、合同、安全与回退 gate 已闭合，集中视觉项 `V-001`～`V-004` 签收 **0/4**；这只关闭了 R2 的前置合同和首个 Note/LN 纵切自动闭环，工程现转入 R3/`SV1-2`，不代表 `SV1-1`、Skin V1 或 release 完成。真实 BMS beatmap-local 作者格式仍须产品另行决定，完整 Skin V1 仍不可用。详见 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)。

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
| 1 | R3 / `SV1-2` G1 可视文件夹 | 只有 early carrier；异常实现已撤回，整包原子重载未实现 | 建立 managed/external authority、路径 containment、安全存储与整包原子 reload |
| 2 | R4 / Skin V1 后续合同 | `SV1-1` 整体仍未完成，`SV1-3`～`SV1-7` 未实现 | 补齐完整 layout/shared codec、所需 slot 三态与 scene/event/script runtime；这些不是进入 `SV1-2` 的前置 |
| 3 | 集中视觉签收 | R2 首个 Note/LN 纵切的四组件自动门已闭合；`V-001`～`V-004` 签收 0/4 | 继续登记到[集中视觉清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)，在 Skin V1/release 完成声明前统一签收 |
| 4 | P1-B/P1-D 输入 | 软件基线可用 | analog scratch、校准、真实 HID |
| 5 | P1-E/P1-G 人工验收 | 待闭合 | LN/CN/HCN、BGA、Song Select、发行 checklist |

## 皮肤系统主线摘要

- 当前保留独立 `[Bms]` 解析、`BmsLegacySkin`、`.osk` 导入、F1 静态配置与逐组件 fallback；新增可见纵切为 managed `.osk` 的 BMS 普通短键与长条 head/body/tail 静态图/编号帧动画。body 宽度只接受 finite 且 `0 < width <= 1`，否则逐字段回到 `0.5775`；素材与宽度绑定同一精确 package revision，managed/default body 共用真实 Idle/Holding/Broken 状态宿主及 80ms 过渡。
- 程序化 `OmsSkin` 仍是实际链底，只作为迁移保障保留到 `oms-simple.osk` 通过 parity、完整性、原子恢复与实机 gate；最终产品渲染链由只读 canonical 包接管。
- Skin V1 的稳定方向是 mania/BMS 共享 neutral ini/asset/animation/event runtime、三态解析与 sandbox，ruleset topology/layout adapter 分离；当前窄纵切不代表这些能力已经完成。
- G1、完整 layout/shared codec、其它 slot 三态、scene/event/script、`oms-simple/oms-complex`、Authoring Kit 与移除程序化产品视觉均未完成。

恢复边界见 [2026-07-10 恢复审计](../other/SKIN_SYSTEM_RECOVERY_20260710.md)，当前实现与未完成 gate 见 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)，V1 完成定义见 [架构审计](../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

## 子线快照

| 子线 | 当前状态 |
| --- | --- |
| P1-A | `SV1-0` 全过；普通短键与长条 head/body/tail 四组件自动 gate 通过、`V-001`～`V-004` 集中待验收；工程下一优先级为 R3/`SV1-2` G1 安全存储与整包原子重载 |
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

### 产品/runtime：截至 2026-07-17 的 P1-A 普通短键与长条 head/body/tail 纵切

| 证据面 | 结果与边界 |
| --- | --- |
| 产品自动验收 | **94/94**，连续三轮全绿；覆盖普通短键及长条 head/body/tail 静态与动画、真实 hold 链、body 安全宽度与同 revision 发布、Idle/Holding/Broken 状态及异步 hydration、7K scratch/SL、14K S2L、A→B、selected 坏组件隔离、低层裸同名防串与完整组件接管、provider-order、authority 冲突及异步换源；beatmap-local 仍只是注入式 fixture |
| 自动回归 | 合并态 BMS skin/runtime focused **326/326**、BMS full **1456/1456**；当前 `osu.Desktop.slnf` Release **0 error / 11 known warnings**。普通短键 gate 工具/生成/staging/场景的既有 **53/53** 未在本切重跑 |
| 未重跑范围 | 本次只改 BMS ruleset 内 provider/renderer/test，未改 shared `osu.Game` skin ABI、mania compatibility 或 fallback authority，因此未另跑 core/mania 产品测试；Release 已编译 core、mania/BMS 与两个 test project |
| 已知告警 | 保留 MessagePack 3.1.3 `NU1902` 及 BMS tests 既有 `CS8600`/`CA2007`，未使用 `NoWarn` |
| 数据与网络 | 测试使用隔离临时存储；exact CLI 失败用例未创建 host 残留。生产 Realm、`chartskin/`、用户皮肤目录及网络零访问、零写入 |
| 未证明能力 | `V-001`～`V-004` 仍待用户集中确认；mania compatibility、完整 slot 三态/layout/G1/scene/script、双包与整包原子重载不在首个纵切范围；transparent tail 是迁移 fallback，不是作者 `Suppress` |

### 文档治理：2026-07-16

本轮只治理文档与 memory 的职责、路由和重复事实，未改产品代码、生产数据或 runtime gate，也未运行产品测试或 Release；Windows PowerShell 5.1 与 PowerShell 7 均运行 `CheckDocumentation.ps1` 通过（118 个 Markdown、946 个相对链接、22 个 memory wiki 链），`git diff --check` 通过。该历史文档检查不替代上方产品验证；2026-07-15 的完整回归仍是当前广基线。

### 手工门素材：2026-07-16

自生成 good/broken `.osk` 与静音 7K `.bme` 的 generator smoke **1/1**，普通短键实际包仍由既有产品链/场景 gate 覆盖；两次生成、Windows PowerShell 5.1/7 输出的 SHA-256 一致。`import-staging` 只覆盖两个已知副本，目录/目标 reparse 与目录冲突 fail-closed；安全/重跑用例仍保留既有 **53/53** 证据。exact runner 只接受内部 GUID host/data storage，含 120 秒 watchdog、显式退出码和不跟随 reparse 的清理；缺失/非法 exact CLI 均 exit 1 且新增 AppData host 残留为 0。按用户当前“不操控电脑”的要求，四组件纵切均未开窗或操控桌面；用户视觉签收保持 `V-001`～`V-004` 待统一验收。

## 待人工验收

| 事项 | 状态 |
| --- | --- |
| 恢复基线：无外部皮肤、`.osk`、partial fallback、5K/7K/9K/14K、双皿与隔离 | **2026-07-14 已通过** |
| managed `.osk` BMS 普通短键编号帧动画 | **`V-001` 集中待验收**；不阻塞后续自动可证切片，不计为产品交付 |
| managed `.osk` BMS 长条头静态图/编号帧动画 | **`V-002` 集中待验收**；不阻塞后续自动可证切片，不计为产品交付 |
| managed `.osk` BMS 长条尾静态图/编号帧动画 | **`V-003` 集中待验收**；不阻塞后续自动可证切片，不计为产品交付；透明链底不是作者 `Suppress` |
| managed `.osk` BMS 长条 body 静态图/编号帧动画、安全宽度及三态过渡 | **`V-004` 集中待验收**；不阻塞后续自动可证切片，不计为产品交付 |
| analog scratch、真实 HID、LN/CN/HCN、长 BGM、密集键音真实谱 | 待做 |
| BGA 图序列/POOR/seek、Gimmick、Song Select 大库与最终发行 | 待做或待复核 |

## 当前风险

- 四个无 authority orphan blob 暂留并已保全，不得把异常处置作为 G1 scanner 批量清理的先例。
- 当前可见纵切只覆盖 BMS 普通短键与 LN head/body/tail，不含 key、mania、完整 layout/三态或 scene/script；Skin V1 不能据此宣称可用。
- 当前逐组件异步替换不等于 `SV1-2` 的整包原子重载；runtime 资源预算也不等于 importer 的 zip-bomb gate。
- 同一 `BmsLegacySkin` 实例的成功 preparation cache 当前不感知 revision；包在原位变化时会安全地维持旧结果或回落，不会混发，但需在 `SV1-2` 的整包 revision/原子重载中消除该陈旧风险。
- 当前链底仍是程序化 `OmsSkin`，不是最终只读 `oms-simple.osk`。
- BMS 单套测试全绿不证明 mania 默认资源、真实选择链或视觉事件正确。
- `LongNoteBodyWidth` 已有首个安全合法域；完整几何 descriptor 仍归 R4，统一前 playfield 与 gauge/combo/BGA 仍可能脱节。
- lane keysound timeline 仍有 5K/7K 边缘轨及 14K 第二皿丢失风险，另立 P1-K/P1-J 切片修复。
- abnormal-period 归档只能定点取证；50k dense、真实硬件和特殊 Gimmick 仍必须以 profiler/实机证据推进。

## 更新规则

- 本页只保留一个产品/runtime 验证快照和一个不冒充产品验证的文档治理边界；旧记录进入 [CHANGELOG.md](CHANGELOG.md)。
- 只记录仍影响决策的风险和未完成 gate；子线实现过程与旧数字留在对应子线。
