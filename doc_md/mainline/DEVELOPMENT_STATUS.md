# OMS 当前开发状态

> 最后更新：2026-09-12（静线布局打磨已恢复；星轨仍待改，视觉未签收）
> 本页只保留全局状态与风险。执行顺序见[当前计划](DEVELOPMENT_PLAN.md)，专项事实从[子线路由](../subline/README.md)进入，历史见[CHANGELOG](CHANGELOG.md)。

## 一句话状态

OMS处于Phase 1.x后段。Skin 原七阶段已有工程与交付证据保留，不重计阶段；用户已否定星轨的动画、美术安排和精细度，现版总体不可用，不能再称只剩签收。用户已重新授权静线布局打磨，本轮先解决落键区遮挡、按键与信息分区及 BGA 主画面；星轨作品仍待后续修改；两款均随安装内置、无需玩家导入；静线保持默认与唯一保底，`V-001`～`V-004`仍 **0/4**、`V-005`未签收，Skin V1与release未完成。详见 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)。

## 产品与仓库基线

两款内置皮肤已接入正常开发启动、build/publish 的源文件同步，发行作者副本跟随本次安装原件；验证与未完成的视觉门见[构建更新记录](../other/SKIN_BUILTIN_BUILD_20260912.md)。

- Windows-only，保留osu!mania与第一类BMS，Osu/Taiko/Catch已删除；离线优先，Phase 3前OMS私有服务与默认endpoint为空。用户主动添加公共BMS难度表URL仅是既有窄例外。
- BMS直读`chartbms/`，mania直读`chartmania/`；支持portable `data/`与自定义数据根。主要工程为`osu.Desktop.slnf`、`osu.Game.Rulesets.Bms`及`oms.Input`。
- 当前协作分支为`master`。皮肤恢复/数据门`SV1-0`已关闭；迁移归档和四个无authority orphan blob继续保全，不能由scanner认领或清理。恢复事实见[恢复审计](../other/SKIN_SYSTEM_RECOVERY_20260710.md)及[数据/实机报告](../other/SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)。

## 当前执行门与全局风险

| 顺序 | 当前事实与下一道门 | 归属 |
| --- | --- | --- |
| 1 | 原 C7 工程证据保留；静线首轮分区与受影响公共布局自动复验完成，实机待对照，星轨观感不通过且本轮不改作品 | [P1-A](../subline/P1-A/DEVELOPMENT_PLAN.md) |
| 2 | canonical普通简洁包接管已实现；旧OmsSkin只保留历史/人工对照，物理删除仍待实机门 | [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md) |
| 3 | 输入软件基线可用；analog scratch跨设备、校准与真实HID尚未闭合 | [P1-B](../subline/P1-B/DEVELOPMENT_STATUS.md)、[P1-D](../subline/P1-D/DEVELOPMENT_STATUS.md) |
| 4 | 真实LN/CN/HCN、音频/特殊谱、BGA、选歌大库与发行组合仍需验收；P1-L仍逐viewport创建player，单content/decoder未完成 | [子线路由](../subline/README.md) |
| 5 | V-001～V-005及候选发行包人工签收；2026-07-14恢复验收不能代替新增视觉与最终包验证 | [集中清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)、[P1-G](../subline/P1-G/DEVELOPMENT_STATUS.md) |

P1-I仍是三行双端筛选原型，须落实既定单轨上限段并补shared fixture/大库门；P1-J的C3末端lane前置已完成，剩余转谱LN、dense profile和听感验收。发行覆盖须保持原便携模式，非便携真实设备与公开发行组合验收归P1-F/P1-G。各项具体风险只在owning子线维护。

此前 C7 完整包已完成实际安装恢复及从上一修复版跨版本覆盖，原保存根前后字节和属性一致；旧候选误入已有保存根的事故仍只有事后保全、没有该根事前快照，不能追溯宣称无损。发行和数据边界见 [P1-F 状态](../subline/P1-F/DEVELOPMENT_STATUS.md)。

皮肤安全与失败回退详见[P1-A技术约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)：当前并无live gameplay reload或watcher，external永久只读；授权撤销不扩大Reload准入，C6完成不等于C7或人工门关闭。异常期归档只能定点取证。局部自动测试不能代替完整真实选择链，自动证据也不能替代视觉、硬件或特殊Gimmick证明。

## 最近一次验证

静线首轮分区、BGA 公开布局设置、作者重新生成与 Release 验证已完成；受影响完整回归没有新增失败，原 mania/core 失败已精确比较，备份根检查和实机观感未代签。见 [P1-A 最新验证](../subline/P1-A/DEVELOPMENT_STATUS.md#最近一次验证)。以下保留此前修复证据的历史身份。

**2026-09-12，原 C7 交付后修复：** 默认皮肤的样式提示与普通导入说明误报已修复，实际 Release 构建不再出现本次告警；完整复验、旧失败精确比较、独立制作、真实安装恢复和跨版本更新已有证据，最终产物独立复核通过。范围、仍保留的依赖风险、旧入口故障与原失败历史见 [P1-A 状态](../subline/P1-A/DEVELOPMENT_STATUS.md)及 [C7 当前修复](../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md#交付后默认皮肤提示与构建警告修复)。不重新计数，不代签人工，也不改写其它子线的验收结论。

## 文档治理验证

2026-09-12 对照已 fetch 且与远端一致的 `c477cda`，完成 P1-A～M 代码/测试源码与文档、记忆的专项对齐；修正默认包、公开说明、存储刷新及历史待办的残留冲突。范围、证据与检查结果见 [本次治理记录](CHANGELOG.md#全项目实际内容文档与记忆专项对齐)。仅文档修改，未重跑产品测试或实机，未推进开发或签收门。
