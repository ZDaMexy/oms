---
name: reference_skin_mode_selection
description: BMS 与 osu!mania 按模式保存皮肤的配置、切换与设置页收简诊断
metadata:
  node_type: memory
  type: reference
---

# 按模式皮肤选择召回

当前行为入口：[P1-A 状态](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)、[技术约束](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#g1选择ui与startup协调)、[制作手册](../../doc_md/other/SKINNING.md)。实现位于 `OsuConfigManager`、`OsuGame` 和 `SkinSection`。

## 配置语义

- `OsuSetting.Skin` 继续作为旧版全局值和兼容投影；新增 `SkinBms`、`SkinMania` 两个 mode override。
- 新安装或旧配置首次进入 `OsuGame.LoadComplete` 时，空的 mode key 从已迁移/规范化的全局值复制，因此旧用户看到的皮肤不变。之后两个 key 独立保存，修改一个不会改另一个。
- 启动与规则集切换均在 update thread 的既有 `SkinManager` selection publication 入口应用对应 key。SkinManager 仍独占 current pair、revision、filesystem capture 与失败回退 authority；UI 只保留本地展示 bindable。
- 非法、受保护、已删除或无法解析/选择的配置回到已验证的 `oms-simple`；真正的准备失败沿用 SkinManager 的保留旧 pair 语义。异步文件夹选择完成时必须按发起的 ruleset 绑定，防止 BMS 的晚到结果写入 mania 偏好。

## 设置页收简

常规玩家路径只显示 BMS/mania 选择和当前可用的重新载入/作者操作。脚本区块只在当前包声明脚本时出现；作者按钮按 `CanModify`/`CanExport`/`CanDelete` 能力显示；设置不再挂入 Folder Skin Workspace；固定 chartskin 目录由“打开皮肤文件夹”和“刷新皮肤”使用，既有外部记录与后端恢复保护保留。原编辑器以独立副本恢复，入口不再依赖拒绝 current 的 CanModify。不要为了减少视觉噪声删除 manager 的安全拒绝、journal、fallback 或导入/导出路径。

## 验证与踩坑

`TestSceneStartupRuleset.TestRulesetSpecificSkinPreferences` 用真实 `OsuGame` 在 BMS → mania 切换后断言两条配置和当前皮肤互不污染；`TestSceneStartupSkinMigration` 覆盖旧全局值迁移。设置布局变化后仍需保留现有 `ChildrenOfType` caller（删除、脚本授权、刷新）并运行 P1-A 规定的 focused tests。不要把移除工作区 UI 写成删除旧用户数据，也不要在设置页绕过 `SkinManager.CurrentSkinInfo` 直接写提交 bindable。
