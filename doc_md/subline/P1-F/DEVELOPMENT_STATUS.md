# P1-F 开发进度：发行后置与离线发布验收

> 最后更新：2026-09-09（C7 随包作者套件、完整性清单与保模式离线更新工具）
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，当前执行顺序见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

## 当前阶段

- [OsuGameDesktop.CreateStorage](../../../osu.Desktop/OsuGameDesktop.cs) 的 `portable.ini -> data/` 与 [OsuStorage](../../../osu.Game/IO/OsuStorage.cs) 的 `storage.ini` 重定向均仍存在。[build-release.ps1](../../../build-release.ps1) 保留 single-file/self-contained、`IncludeAllContentForSelfExtract=true`、图标与更新说明，输出 `release-repo/oms_YYYYMMDD(.zip)`。
- 手工覆盖更新路径已重新审计：用户数据仍与程序文件分离，游戏内在线更新继续保持禁用；当前主要操作风险是运行中覆盖文件，以及误删 `portable.ini` / `storage.ini` 导致数据根切换。
- 游戏内在线更新仍保持禁用，当前重点不是恢复安装器/在线更新，而是继续收口公开发行物产品面与最终 release gate。
- 随包中英 `how to update.txt` 已明确 `storage.ini` 的基础目录位置，并提供 `Update-OMS.ps1` 实际入口：新包先独立解压，工具校验完整性后覆盖程序文件，保留目标原 portable 模式、`data/` 与 `storage.ini`，备份被替换文件，中断时保留现场与修复说明。
- 发行包仍带 `portable.ini`；普通解压覆盖非便携安装仍会改变保存位置。因此使用随包工具，非便携目标持续保持程序旁无 marker；自定义指针始终在基础数据根，不在 exe 同级，也不随自定义根迁移。
- C7 打包入口同时发布无需 SDK 的作者工具，携带双 canonical 原件、两款普通可导入包、完整源文件、制作说明、验收/更新工具及文件完整性清单；是否满足当前 Skin V1 退出门仍见 P1-A。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 便携发布基线 | 已完成 | `build-release.ps1 -> oms_YYYYMMDD(.zip)`、`portable.ini -> data/`、single-file 完整自解压与覆盖更新路径结论已对齐 |
| 在线更新关闭基线 | 已完成 | 当前仍维持离线优先，不恢复在线更新；手工覆盖后不会进入 Velopack 自更新链 |
| 公开发行物产品面验收 | 进行中 | 依赖 `P1-A` 的默认皮肤与 release gate 收尾 |
| 发布口径同步 | 已对齐当前工具 | 随包中英说明、storage.ini、非便携模式、备份与作者套件均与 `../../other/RELEASE.md` 一致；实际候选包启动仍单独记录 |

## 最近一次验证

本次离线更新工具的 portable/nonportable/custom 文件保护、只读 canonical 更新及坏新包拒绝已实际验证，见 [C7 证据](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md#覆盖更新与数据保护工具)。合成程序文件验证不代替发行物启动。此前明确记载的真实打包/fresh extract/冷启动与 8 秒 smoke 为 **2026-05-09**；当前 C7 publish、解压、custom-root/覆盖更新运行结果须在实际执行后追加。

## 文档治理验证

2026-09-09 的 C7 变更已同步打包工具、marker/重定向、中断备份与数据保护说明；后续真实发行验收使用 [PLAN](DEVELOPMENT_PLAN.md) 的候选包矩阵，不以文件保护脚本验证代替冷启动。
