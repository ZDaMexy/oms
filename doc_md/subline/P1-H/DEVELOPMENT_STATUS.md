# P1-H 开发进度：存储拓扑支撑线

> 最后更新：2026-04-20

## 当前阶段

- `P1-H` 已完成子线建档。
- 当前仓库已具备 `chartbms/`、`chartmania/`、`portable.ini -> data/` 与外部多目录谱库扫描基线；`ExternalLibraryConfig` / `ExternalLibraryScanner` 已接通，Settings -> Maintenance 已有添加 / 移除 / 扫描 UI。
- `OsuGameDesktop` 已把 BMS / mania 两类根目录注册进 managed library scanner，当前剩余重点是删除 / 失效语义、path identity dedup 与重扫策略。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 子线建档 | 已完成 | 四件套已建立 |
| 基础存储拓扑 | 已完成 | `chartbms/` / `chartmania/` / `storage.ini` / `portable.ini` 主链已落地 |
| 外部谱库管理 | 已完成 | `ExternalLibraryConfig` / `ExternalLibraryScanner` + Maintenance UI 已接通 |
| 删除 / 失效语义 | 未开始 | 待收口 |
| path identity dedup / 重扫策略 | 未开始 | 待收口 |

## 验证记录

- 本轮状态同步基于当前代码结构完成，未新增构建或测试命令。