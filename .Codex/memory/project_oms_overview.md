---
name: project-oms-overview
description: OMS 项目身份与进入任务时必须记住的边界
metadata:
  node_type: memory
  type: project
---

# OMS 项目召回

- Windows-only osu!lazer fork，只保留 mania，新增第一类 BMS；Osu/Taiko/Catch 已删除。
- 离线优先、旧 osu! API endpoint 为空；固定 OMSIR 的直接登录 / 按需公开查榜与旧在线链分开，边界读 [P3-IR](../../doc_md/subline/P3-IR/TECHNICAL_CONSTRAINTS.md)。当前阶段与执行门读 mainline STATUS/PLAN，不从阶段编号推导全部联网可用或全部禁用。
- BMS 直读 `chartbms/`，mania 直读 `chartmania/`；支持 `portable.ini→data/` 与 `storage.ini`。
- 官方包含 `portable.ini`；非便携覆盖更新须保持该标记缺席，否则会改用 `data/` 并绕过原 AppData 中的自定义根指针。`storage.ini` 位于 bootstrap storage（便携 `data/`、非便携 host 默认目录），不随数据迁移；详见[发行说明](../../doc_md/other/RELEASE.md)。
- 主工程：`osu.Game`、Mania、Bms、`oms.Input`、`osu.Desktop`。
- 皮肤任务先读[[reference_skin_recovery_20260710]]；G1及其它Skin V1面的实时完成度只看P1-A STATUS/PLAN，scanner、selection与mutation/recovery技术地雷分别从[[reference_skin_managed_folder_scanner]]、[[reference_skin_managed_folder_selection]]和[[reference_skin_managed_folder_mutation_foundation]]进入。

实时状态、计划和命令不要在 memory 复制，统一读 `AGENTS.md` 与 `doc_md/mainline/{DEVELOPMENT_STATUS,DEVELOPMENT_PLAN}.md`。
