# P1-F 当前计划：离线发行物与覆盖更新

> 最后更新：2026-09-09（C7 保模式更新工具与随包说明已实现）
> 主线顺序见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，稳定发行红线见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)，历史见 [CHANGELOG.md](CHANGELOG.md)。

## 子线职责

P1-F 只拥有离线发行物、portable/custom data root、覆盖更新与最终 publish/release 复核。桌面拖放、Song Select、玩法和跨功能 UI smoke 的人工结果由 P1-G 汇总；P1-F 只提供待验发行物与发行专属步骤。

## 已有基线

- `build-release.ps1 → release-repo/oms_YYYYMMDD(.zip)` 是当前正式打包入口。
- 发行根包含 `osu!.exe`、`portable.ini`、图标与中英双语 `how to update.txt`；single-file 自解压内容已锁定。
- `portable.ini → data/` 与 `storage.ini` 自定义数据根均受保护；游戏内在线更新保持关闭。
- 手工覆盖流程为“退出程序 → 解压覆盖 → 再启动”，不得删除用户数据根标记。

完成修复、旧 smoke 和测试数字按日期查 [CHANGELOG](CHANGELOG.md)。

## 当前执行顺序

1. 保持打包脚本、文件名、根目录内容与 `IncludeAllContentForSelfExtract=true` 不回退；当前随包中英说明及 `Update-OMS.ps1` 已覆盖 bootstrap `storage.ini`、原 portable 模式、坏新包拒绝与更新前程序备份，候选包实际执行须继续覆盖这些行为。
2. 在 P1-A 最终皮肤/release gate 就绪后产出候选发行物，不提前把迁移 fallback 描述为最终产品面。
3. 对fresh extract、portable `data/`、custom root、覆盖更新和旧内部OMS版号迁移执行最终复核；非便携覆盖必须验证新包 `portable.ini`不会意外启用便携模式，保留原bootstrap数据根的 `storage.ini`，不能错误要求只保留exe旁配置。
4. 将发行专属人工步骤与结果交 P1-G 汇总；阻塞缺陷仍回 P1-F 修复。
5. 仅当公开口径变化时同步 `../../other/RELEASE.md`；Phase 3 前不恢复联网更新、endpoint 或安装器承诺。

## 最终验收

- Release publish/build 成功，压缩包可 fresh extract 冷启动。
- portable/custom root 的已有用户数据、皮肤、谱面与配置在覆盖后保持可读，不发生数据根静默切换。
- `Update-OMS.ps1` 按新包完整性清单覆盖，保留非便携 marker 缺席、只读 canonical 保护与中断备份；无法确认的旧内容不得自动删除。
- 运行中覆盖被明确禁止；说明文件与实际包内容一致。
- 发行物不暴露未过 gate 的 Skin V1、G1、script、在线或格式兼容能力。

## 明确不做

- 不承接通用 drag-drop、Song Select、输入、长条、BGA 或 gameplay UI 验收 ownership。
- 不恢复 Velopack/在线更新链，也不把离线包描述成严格“只有一个 exe”。
