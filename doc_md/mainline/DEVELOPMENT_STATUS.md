# OMS 当前开发状态

> 最后更新：2026-07-10
> 这里只保留当前事实、风险和最新验证。执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，历史见 [CHANGELOG.md](CHANGELOG.md)。

## 一句话状态

OMS 处于 Phase 1.x 收尾期。2026-07-10 已把皮肤系统恢复到可信 `.osk/F1/schema 56` 基线；Skin V1 随后重新定义为“引擎拥有 gameplay truth 与 playfield/BGA 布局，外部 package 拥有视觉/动画/只读事件响应”。当前先完成实机验收和数据只读清点，再按 P1-A `SV1-*` 重做共享合同与 G1，同时继续输入硬件与真实谱面验收。

## 产品与仓库基线

- Windows-only，保留 osu!mania + 第一类 BMS；Osu/Taiko/Catch 已删除。
- 离线优先；Phase 3 前默认 endpoint 为空，联网与自动更新不作为当前能力。
- BMS 直读 `chartbms/`，mania 直读 `chartmania/`；发行支持 `portable.ini → data/` 与 `storage.ini` 自定义根。
- 主要入口：`osu.Desktop.slnf`；BMS 主开发目标：`osu.Game.Rulesets.Bms`；统一输入：`oms.Input`。
- 当前协作分支为 `master`；可信恢复锚点是 `ef56507`，后续皮肤工作只能在该基线上按小切片前进。

## 当前优先级

| 顺序 | 工作面 | 状态 | 下一检查点 |
| --- | --- | --- | --- |
| 1 | P1-A 皮肤恢复 gate | 自动恢复已完成 | 无外部皮肤、`.osk`、5K/7K/9K/14K 实机视觉 |
| 2 | schema 56 用户数据 | 未清点 | 只读报告 folder-backed `SkinInfo`，不自动修复 |
| 3 | Skin V1 共同合同 | 架构/文档已冻结 | neutral ini/layout/event/fallback fixtures |
| 4 | G1 可视文件夹 | 异常实现已撤回 | managed/external authority、containment 与原子 reload |
| 5 | P1-B/P1-D 输入 | 软件基线可用 | analog scratch、校准、真实 HID |
| 6 | P1-E/P1-G 人工验收 | 待闭合 | LN/CN/HCN、BGA、Song Select、发行 checklist |

## 皮肤系统当前事实

- **保留**：独立 `[Bms]` 解析、`BmsLegacySkin` 配置源、`.osk` 导入路由、现存静态件颜色/纹理/几何、reference ini 自校验。
- **fallback**：程序化 `OmsSkin` 仍是最终兜底；用户皮肤缺件必须逐组件回落。
- **schema**：`SkinInfo.FilesystemStoragePath`、`IsExternalFilesystemStorage` 与 Realm schema 56 保留，但没有生产扫描/选择/删改/热重载。
- **恢复修正**：base legacy parser 前重置配置流位置；14K 第二皿使用 `S2`/P2 素材映射。
- **V1 方向**：mania/BMS 共享 neutral ini codec、scene/animation、只读事件 ABI、`Provide/Inherit/Suppress` 与 sandbox；ruleset topology/layout adapter 分离。
- **V1 下限/上限**：同一公开 API 必须支持只剩最小可玩色块的 Minimal 皮肤，以及覆盖完整事件/动画能力的 Showcase 皮肤。
- **未落地**：G1 生产链、shared codec/layout descriptor、scene/event/script runtime、三态 suppress、文件型默认；事故期 F2/Lua/mania adapter/reference-default 均不计能力。
- **布局风险**：现有 playfield 可读皮肤几何，而 gauge/combo/BGA 仍各自按默认 profile/固定 rect 计算；14K 四角四 BGA player 只是临时实现，不能作为 V1 合同。

恢复取证见 [SKIN_SYSTEM_RECOVERY_20260710.md](../other/SKIN_SYSTEM_RECOVERY_20260710.md)；V1 架构与完成定义见 [SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md](../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

## 子线快照

| 子线 | 当前状态 |
| --- | --- |
| P1-A | 皮肤可信恢复完成；Skin V1 路线已重构，实机/数据/shared fixtures/G1 待续 |
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

### 2026-07-10 皮肤可信恢复

| 检查 | 结果 | 判读 |
| --- | --- | --- |
| `BmsLegacySkinTest` | **15/15** | H1 流复位与 H2 双皿映射通过 |
| BMS 全量 | **1005/1005** | 恢复树通过 |
| mania 默认 OMS 资源 | **1/1** | 未重现 BMS reference 覆盖全局 OmsSkin |
| mania 全量 | **787/791** | 4 项既有 HoldNote auto-frame 期待失败；恢复未改 mania/autoplay |
| core skin focused | **57/62** | 1 项 Argon 旧期待 + 4 项已删除 ruleset 的 beatmap archive 依赖 |
| `osu.Desktop.slnf` Release | **0 error / 20 warnings** | 18 条为 9 个 MessagePack NU1902 重复报告；另有既有 CS8600/CA2007 |

构建告警必须保留可见，不能恢复异常期的全局 `NoWarn`。MessagePack 安全升级另立依赖治理切片。

## 待人工验收

| 事项 | 状态 |
| --- | --- |
| 无外部皮肤 + `.osk` 用户皮肤 + fallback | 待做 |
| BMS 5K/7K/9K/14K 皮肤布局与双皿素材 | 待做 |
| analog scratch 与真实 HID 控制器 | 待做 |
| LN/CN/HCN、长 BGM、密集键音真实谱 | 待做 |
| BGA 图序列/POOR/seek 与 Gimmick 逐谱视觉 | 待做 |
| Song Select 大库分组、筛选和 UI | 待做 |
| 最终 portable/custom-root 覆盖更新 | 待复核 |

## 当前风险

- 生产 Realm 已经是 schema 56；未经只读清点，不得降 schema、自动删除记录或清理 `chartskin/`。
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
