# OMS 当前开发状态

> 最后更新：2026-07-15
> 这里只保留当前事实、风险和最新验证。执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，历史见 [CHANGELOG.md](CHANGELOG.md)。

## 一句话状态

OMS 处于 Phase 1.x 收尾期。P1-A `SV1-0` 的自动、schema 56 数据与用户实机 gate 已全部通过；`SV1-1` 的三态/precedence、semantic taxonomy、neutral lane identity/topology/保持性 validation、config presence、legacy mania scalar/array/四项 global colour snapshot、event envelope/order、capability、lane-resource candidate/resolution，以及 process-local topology publication/native continuity 十四个切片已完成但未接生产链。详见 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)。

## 产品与仓库基线

- Windows-only，保留 osu!mania + 第一类 BMS；Osu/Taiko/Catch 已删除。
- 离线优先；Phase 3 前默认 endpoint 为空，联网与自动更新不作为当前能力。
- BMS 直读 `chartbms/`，mania 直读 `chartmania/`；发行支持 `portable.ini → data/` 与 `storage.ini` 自定义根。
- 主要入口：`osu.Desktop.slnf`；BMS 主开发目标：`osu.Game.Rulesets.Bms`；统一输入：`oms.Input`。
- 当前协作分支为 `master`；可信恢复锚点是 `ef56507`，后续皮肤工作只能在该基线上按小切片前进。

## 当前优先级

| 顺序 | 工作面 | 状态 | 下一检查点 |
| --- | --- | --- | --- |
| 1 | schema 56 用户数据 | **通过**：异常 copy 定点移除，OMS fixed-ID 修正 | 保留迁移归档，不运行全局 orphan cleanup |
| 2 | P1-A 皮肤恢复 gate | **通过**：自动、数据与用户实机清单闭环 | 保留证据，不重复迁移/清理 |
| 3 | Skin V1 共同合同 | `SV1-1` 三态/precedence、semantic taxonomy、lane identity/topology/保持性 validation、config presence、event/capability、lane-resource resolution 与 topology publication/native continuity 十四切完成；生产未接入 | 继续 decoder-time per-column/global config provenance、真实资源 validation/materialization/shared codec、concrete event payload/producer、真实 capability manifest/runtime、full layout/geometry 与 production revision/wire fixtures |
| 4 | G1 可视文件夹 | 异常实现已撤回 | managed/external authority、containment 与原子 reload |
| 5 | P1-B/P1-D 输入 | 软件基线可用 | analog scratch、校准、真实 HID |
| 6 | P1-E/P1-G 人工验收 | 待闭合 | LN/CN/HCN、BGA、Song Select、发行 checklist |

## 皮肤系统当前事实

- **保留**：独立 `[Bms]` 解析、`BmsLegacySkin` 配置源、`.osk` 导入路由、现存静态件颜色/纹理/几何、reference ini 自校验。
- **fallback 当前态**：程序化 `OmsSkin` 仍是实际链底，但只算迁移实现；最终由只读 `oms-simple.osk` 接管，程序化主题渲染退出产品链。
- **schema**：schema 56 路径 authority 正常；异常 mutable copy 已经用户授权定点移除，OMS fixed-ID 已修正为当前 `OmsSkin`，生产剩余 2 条皮肤记录。
- **恢复修正**：base legacy parser 前重置配置流位置；14K 第二皿使用 `S2`/P2 素材映射。
- **V1 方向**：mania/BMS 共享 neutral ini codec、scene/animation、只读事件 ABI、`Provide/Inherit/Suppress` 与 sandbox；ruleset topology/layout adapter 分离。
- **slot taxonomy**：26 个内部 semantic family 已固定 7 critical / 19 optional，descriptor 与 ruleset context 分离；它不是作者 manifest ABI、layout descriptor 或生产 suppress 接线。
- **lane identity/topology**：强类型 identity、immutable snapshot/index/order 与 neutral validator 之上，已落 topology-only publication/process-local revision owner；internal BMS exact keymode 与 mania ordered stage vector 维护 native continuity。尚无 full layout/geometry、production `layoutRevision`/event producer、wire ABI 或生产接线。
- **config presence/resolution**：default=`Absent` 的共享 declaration carrier、internal decoder bucket projection、六类 lane-resource immutable snapshot、有序 BMS→mania candidate plan、process-local source-aware resolution 与 revision-owner 合同已落；尚无其余完整 field config、真实文件 validation/materialization/shared codec、production fallback 或接线。
- **event foundation**：process-local immutable envelope、engine-owned payload hierarchy 与 internal canonical-stream ordering cursor 已落；尚无 concrete payload family、lifecycle producer/dispatch、sampling、scene/script consumer 或生产接线。
- **capability foundation**：process-local closed-allowlist evaluator、host support/per-skin authorization 分层、immutable decision 与 hard-deny authority classifier 已落；当前没有真实 capability、manifest/身份绑定/授权存储 UI、runtime gate 或 sandbox。
- **V1 下限/上限**：同一公开 API 必须交付同时含 mania/BMS 的 `oms-simple.osk` 与 `oms-complex.osk`；前者是最终 fallback，后者证明完整事件/动画能力。
- **社区合同**：`.osk`、根 `skin.ini`、mania 共同素材/动画命名和拖入导入沿用 osu 社区心智；BMS/scene/script 是版本化扩展，作者不需要编译 DLL。
- **未落地**：G1 生产链、shared codec/layout descriptor、scene/event/script runtime、生产三态 suppress、`oms-simple/complex`、Authoring Kit 与文件 fallback；事故期 F2/Lua/mania adapter/reference-default 均不计能力。
- **布局风险**：现有 playfield 可读皮肤几何，而 gauge/combo/BGA 仍各自按默认 profile/固定 rect 计算；14K 四角四 BGA player 只是临时实现，不能作为 V1 合同。

恢复取证见 [SKIN_SYSTEM_RECOVERY_20260710.md](../other/SKIN_SYSTEM_RECOVERY_20260710.md)；V1 架构与完成定义见 [SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md](../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

## 子线快照

| 子线 | 当前状态 |
| --- | --- |
| P1-A | `SV1-0` 自动/数据/实机全过；`SV1-1` 前十四个合同切片完成，未接生产链 |
| P1-B | 输入基础链可用；analog scratch/真实硬件未闭合 |
| P1-C | 判定 parity 主体已落；常驻速度反馈卡已删除，不作为当前能力 |
| P1-D | deadzone/sensitivity/live diagnostics 未完成 |
| P1-E | gameplay 主链具备；真实 LN/CN/HCN 组合验收待做 |
| P1-F | portable 离线发行基线已验证，最终 release 复核待做 |
| P1-G | 人工验收汇总待做 |
| P1-H | 文件系统谱库与多根扫描基线已落；删除/失效/去重仍是 backlog |
| P1-I | 选歌分组/筛选/搜索主功能已落；拖拽 headless 与 shared visual 待补 |
| P1-J | 普通密度音频/性能主故障已收口；新增末端 lane keysound runtime proof，转谱 LN/50k/人工清单待做 |
| P1-K | K1–K12 主体阶段性收口；末端 lane timeline 上界与 sparse keymode authority 是 Skin V1 前置修正 |
| P1-L | BGA 播放主链已落；内容/viewport 解耦协作 P1-A，逐谱视觉/反向滚动待做 |
| P1-M | 规划完成，未开工 |

入口和下一道门见 [子线路由](../subline/README.md)。

## 最近一次验证

### 2026-07-15 P1-A `SV1-1` 第十四切

第十四切新增 topology-only publication/process-local revision owner，并以 internal BMS exact keymode 与 mania ordered stage-column vector 关闭 neutral shape 无法表达的 native continuity 盲区；没有接 playfield、renderer、event producer 或 `SkinManager`。shared gameplay 243/243，BMS publication 14/14、topology 34/34、full 1132/1132；mania publication 7/7、topology 16/16、full 822/826，非零仍只有恢复基线同名 4 个 HoldNote auto-frame 期待；core skin 57/62 仍是同名既有 5 项。`osu.Desktop.slnf` Release Rebuild 0 error / 20 warnings，未使用 `NoWarn`。下一步是 decoder-time per-column colour provenance；full layout/geometry、production revision/event/wire、真实资源 validation/materialization/shared codec、manifest/runtime、scene/script 与 fallback 接线均未完成。精确矩阵见 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)。

### 2026-07-13 `SV1-0` 数据安全门

| 检查 | 结果 | 判读 |
| --- | --- | --- |
| BMS skin focused | **43/43** | parser/legacy/reference/render 通过 |
| BMS transformer/fallback | **104/104** | 当前 nullable 生产链对照通过 |
| mania OMS / 默认资源 | **84/84；专项 1/1** | 默认资源未被 BMS reference 覆盖 |
| core skin focused | **57/62** | 1 项 Argon 旧期待 + 4 项已删除 ruleset 的 beatmap archive 依赖 |
| schema 56 清点/迁移 | **通过** | 备份/副本演练后生产 3→2 条；异常 GUID 消失，OMS fixed-ID 修正 |
| post-migration read-only reopen | **通过** | 记录状态精确，复核前后 SHA-256 一致；未启动客户端 |

每次 focused 命令均保留 9 条 MessagePack `NU1902`；BMS 另有既有 `CS8600`/`CA2007`。临时 dynamic-only 迁移工具 0 error / 1 条预期空 schema Fody warning。本切片未改代码，未重跑 BMS full/Release；恢复基线仍为 BMS 1005/1005、Release 0 error/20 warnings。完整证据见 [`SV1-0` 报告](../other/SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)。

## 待人工验收

| 事项 | 状态 |
| --- | --- |
| 无外部皮肤 + `.osk` 用户皮肤 + partial fallback | **2026-07-14 已通过** |
| BMS 5K/7K/9K/14K 皮肤布局、双皿素材与 mania/BMS 隔离 | **2026-07-14 已通过** |
| analog scratch 与真实 HID 控制器 | 待做 |
| LN/CN/HCN、长 BGM、密集键音真实谱 | 待做 |
| BGA 图序列/POOR/seek 与 Gimmick 逐谱视觉 | 待做 |
| Song Select 大库分组、筛选和 UI | 待做 |
| 最终 portable/custom-root 覆盖更新 | 待复核 |

## 当前风险

- schema 56 异常记录已定点处置；四个无 authority orphan blob 暂留并已保全，不得把它们作为 G1 scanner 批量清理的先例。
- BMS 单套测试全绿不证明 mania 默认资源、真实 `SkinManager` 选择链或视觉事件正确。
- 皮肤几何值当前缺少完整合法域校验；在统一 descriptor 前，极端值还可能让 playfield 与 gauge/combo/BGA 脱节或重叠。
- BMS lane keysound timeline 仍以 key count 而非 lane count 过滤，5K/7K 边缘轨及 14K 第二皿存在丢失风险；另立 P1-K/P1-J 修复切片，不混入本轮文档改线。
- “代码 provider 可替换”“ini 可配置”“scene 可声明”“script 可编程”是四种不同完成度，文档和发布说明不得混写。
- 皮肤 abnormal-period 归档只能定点取证，禁止整包 cherry-pick/apply。
- 50k 极端 dense、真实硬件、特殊 Gimmick 谱仍需要 profiler/真机证据，禁止凭猜测优化。
- mainline 与子线旧 `CHANGELOG` 中的历史数字不代表当前 gate；只看本页“最近一次验证”。

## 更新规则

- 本页只保留一个最新验证快照；下一次验证覆盖本节，旧记录进入 [CHANGELOG.md](CHANGELOG.md)。
- 只记录仍影响决策的风险和未完成 gate；实现过程、命令和旧失败进入对应子线 `CHANGELOG`。
- 子线详情不复制到这里，只保留一句状态和链接。
