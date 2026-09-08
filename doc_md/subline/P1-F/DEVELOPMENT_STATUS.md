# P1-F 开发进度：发行后置与离线发布验收

> 最后更新：2026-09-09（源码/历史证据审查；未生成新发行物）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，当前执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

## 当前阶段

- [OsuGameDesktop.CreateStorage](../../../osu.Desktop/OsuGameDesktop.cs) 的 `portable.ini -> data/` 与 [OsuStorage](../../../osu.Game/IO/OsuStorage.cs) 的 `storage.ini` 重定向均仍存在。[build-release.ps1](../../../build-release.ps1) 保留 single-file/self-contained、`IncludeAllContentForSelfExtract=true`、图标与更新说明，输出 `release-repo/oms_YYYYMMDD(.zip)`。
- 手工覆盖更新路径已重新审计：用户数据仍与程序文件分离，游戏内在线更新继续保持禁用；当前主要操作风险是运行中覆盖文件，以及误删 `portable.ini` / `storage.ini` 导致数据根切换。
- 游戏内在线更新仍保持禁用，当前重点不是恢复安装器/在线更新，而是继续收口公开发行物产品面与最终 release gate。
- **现有说明缺口**：脚本生成的中英 `how to update.txt` 只提醒保留 `portable.ini`/`data/`，未写 `storage.ini`；仓库 [RELEASE](../../other/RELEASE.md) 已要求保留该重定向配置。下一候选包前须对齐，不能把仓库说明齐全等同于随包说明齐全。
- **非便携覆盖风险**：发行包总带 `portable.ini`，非便携安装直接覆盖全包会切换到 `data/` 基础数据根，原默认根的 `storage.ini`不会被读取。保持非便携模式须在启动前继续保持exe旁无该marker；`storage.ini`保存在基础数据根（便携为 `data/`，否则为host默认根），不在exe同级，也不随自定义根迁移。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 便携发布基线 | 已完成 | `build-release.ps1 -> oms_YYYYMMDD(.zip)`、`portable.ini -> data/`、single-file 完整自解压与覆盖更新路径结论已对齐 |
| 在线更新关闭基线 | 已完成 | 当前仍维持离线优先，不恢复在线更新；手工覆盖后不会进入 Velopack 自更新链 |
| 公开发行物产品面验收 | 进行中 | 依赖 `P1-A` 的默认皮肤与 release gate 收尾 |
| 发布口径同步 | 进行中 | 需持续联动 `../../other/RELEASE.md` |

## 最近一次验证

本线最后明确记载的实际打包/fresh extract/冷启动与 8 秒 smoke 证据为 **2026-05-09**，见 [CHANGELOG](CHANGELOG.md)。它证明当时包体基线，不证明当前 C5/C6/C7 候选发行物；后续代码 Release build 也不替代 publish、解压、custom-root/覆盖更新实机门。

## 文档治理验证

2026-09-09核对打包参数、marker/重定向、禁用 updater 的 production 路径与既有 smoke 记录；本次未 publish、启动发行物或复验用户数据覆盖更新。后续验收使用 [PLAN](DEVELOPMENT_PLAN.md) 的候选包矩阵。
