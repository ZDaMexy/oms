# OMS 当前开发状态

> 最后更新：2026-07-10
> 这里只保留当前事实、风险和最新验证。执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，历史见 [CHANGELOG.md](CHANGELOG.md)。

## 一句话状态

OMS 处于 Phase 1.x 收尾期。2026-07-10 已把皮肤系统恢复到可信 F1/schema 56 基线并同步远端；当前先完成皮肤实机验收和数据只读清点，再重做 G1 安全合同，同时继续输入硬件与真实谱面验收。

## 产品与仓库基线

- Windows-only，保留 osu!mania + 第一类 BMS；Osu/Taiko/Catch 已删除。
- 离线优先；Phase 3 前默认 endpoint 为空，联网与自动更新不作为当前能力。
- BMS 直读 `chartbms/`，mania 直读 `chartmania/`；发行支持 `portable.ini → data/` 与 `storage.ini` 自定义根。
- 主要入口：`osu.Desktop.slnf`；BMS 主开发目标：`osu.Game.Rulesets.Bms`；统一输入：`oms.Input`。
- 当前分支 `master` 的恢复提交为 `ef56507`，已与 `origin/master` 同步。

## 当前优先级

| 顺序 | 工作面 | 状态 | 下一检查点 |
| --- | --- | --- | --- |
| 1 | P1-A 皮肤恢复 gate | 自动恢复已完成 | 无外部皮肤、`.osk`、5K/7K/9K/14K 实机视觉 |
| 2 | schema 56 用户数据 | 未清点 | 只读报告 folder-backed `SkinInfo`，不自动修复 |
| 3 | G1 可视文件夹 | 异常实现已撤回 | managed/external authority 与删改 containment 重设计 |
| 4 | P1-B/P1-D 输入 | 软件基线可用 | analog scratch、校准、真实 HID |
| 5 | P1-E/P1-G 人工验收 | 待闭合 | LN/CN/HCN、BGA、Song Select、发行 checklist |

## 皮肤系统当前事实

- **保留**：独立 `[Bms]` 解析、`BmsLegacySkin` 配置源、`.osk` 导入路由、现存静态件颜色/纹理/几何、reference ini 自校验。
- **fallback**：程序化 `OmsSkin` 仍是最终兜底；用户皮肤缺件必须逐组件回落。
- **schema**：`SkinInfo.FilesystemStoragePath`、`IsExternalFilesystemStorage` 与 Realm schema 56 保留，但没有生产扫描/选择/删改/热重载。
- **恢复修正**：base legacy parser 前重置配置流位置；14K 第二皿使用 `S2`/P2 素材映射。
- **未落地**：G1 生产链、F2/F3/G2、Lua、mania fallback adapter、reference-default 替换。

完整取证和重新准入门见 [SKIN_SYSTEM_RECOVERY_20260710.md](../other/SKIN_SYSTEM_RECOVERY_20260710.md)。

## 子线快照

| 子线 | 当前状态 |
| --- | --- |
| P1-A | 皮肤可信恢复完成；实机和 G1 安全重设计待续 |
| P1-B | 输入基础链可用；analog scratch/真实硬件未闭合 |
| P1-C | 判定 parity 主体已落；常驻速度反馈卡已删除，不作为当前能力 |
| P1-D | deadzone/sensitivity/live diagnostics 未完成 |
| P1-E | gameplay 主链具备；真实 LN/CN/HCN 组合验收待做 |
| P1-F | portable 离线发行基线已验证，最终 release 复核待做 |
| P1-G | 人工验收汇总待做 |
| P1-H | 文件系统谱库与多根扫描基线已落；删除/失效/去重仍是 backlog |
| P1-I | 选歌分组/筛选/搜索主功能已落；拖拽 headless 与 shared visual 待补 |
| P1-J | 普通密度音频/性能主故障已收口；转谱 LN、50k profile 与人工音频清单待做 |
| P1-K | K1–K12 主体阶段性收口；public wording、特殊谱尾项与人工证明待做 |
| P1-L | 地雷、滚动旁路与 BGA 主链已落；逐谱视觉/反向滚动待做 |
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
- 皮肤 abnormal-period 归档只能定点取证，禁止整包 cherry-pick/apply。
- 50k 极端 dense、真实硬件、特殊 Gimmick 谱仍需要 profiler/真机证据，禁止凭猜测优化。
- mainline 与子线旧 `CHANGELOG` 中的历史数字不代表当前 gate；只看本页“最近一次验证”。

## 更新规则

- 本页只保留一个最新验证快照；下一次验证覆盖本节，旧记录进入 [CHANGELOG.md](CHANGELOG.md)。
- 只记录仍影响决策的风险和未完成 gate；实现过程、命令和旧失败进入对应子线 `CHANGELOG`。
- 子线详情不复制到这里，只保留一句状态和链接。
