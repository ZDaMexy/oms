---
name: reference_skin_mode_selection
description: BMS 与 osu!mania 的模式/键数皮肤选择、配置继承、独立设置目标与进曲顺序诊断
metadata:
  node_type: memory
  type: reference
---

# 按模式皮肤选择召回

当前行为入口：[P1-A 状态](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)、[技术约束](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#g1选择ui与startup协调)、随作者套件发行的[制作手册](../../skin-authoring/docs/SKINNING.md)。实现位于 `OsuConfigManager`、`OsuGame` 和 `SkinSection`。

## 配置语义

- `OsuSetting.Skin` 继续作为旧版全局值和兼容投影；新增 `SkinBms`、`SkinMania` 两个 mode override。
- 新安装或旧配置首次进入 `OsuGame.LoadComplete` 时，先迁移旧内置 complex 并规范化失效配置，再把结果复制到空的 mode key。仍有效的普通用户皮肤选择保持；旧内置 complex、缺失或不合资格的记录回到静线，不能笼统承诺所有旧外观不变。之后两个 key 独立保存，修改一个不会改另一个。
- 启动与规则集切换均在 update thread 的既有 `SkinManager` selection publication 入口应用对应 key。SkinManager 仍独占 current pair、revision、filesystem capture 与失败回退 authority；UI 只保留本地展示 bindable。
- 无法解析的 ID、缺失记录、受保护或已删除的配置回到已验证的 `oms-simple`；记录有效但整包准备失败沿用 SkinManager 的保留旧 pair 语义，不能把两种失败都归为自动回退。异步文件夹选择完成时必须按发起的 ruleset 绑定，防止 BMS 的晚到结果写入 mania 偏好。

## 设置入口诊断

设置的当前产品面以 STATUS 为准。排查按钮时不要把原编辑器可用性绑回 `CanModify(current)`：该能力故意拒绝 current，编辑器则修改独立副本。刷新按钮同时负责发现目录和 reload，二者区别见 [[reference_skin_managed_folder_scanner]]。移除 Workspace UI 不代表删除旧 external 记录或解除其只读边界，见 [[reference_skin_external_workspace_managed_copy]]。

## 验证与踩坑

- 皮肤选项与选择皮肤不同：`SkinOptionPreferences`按record+ruleset+可选键数覆盖，root准备才冻结到Context；不要为了更换图案触发reload、重选current或改包。键数恢复只清当前层，不清模式默认；作者默认仍可覆盖已改过的模式配置。首次显示控件不写覆盖，失效旧值读作者默认；声明删除后的旧ID仅在玩家主动保存时清理，避免占满32项。
- 数值网格以作者min为原点，不能直接把step给BindableDouble.Precision（framework按0取整）。提交时按min+n×step吸附，再保存canonical值。控件重建要验证值和store一致；未加载就Dispose必须容许Resolved尚为空。
- SettingsSection的旧独立caller可能没有应用级preferences/ruleset服务；可选边界保持空块，不能让无关删除/脚本/刷新入口因新块加载失败。正常GameHost缓存唯一服务，真实UI验证走该scope。
- 独立设置卡片需经manager跟踪的只读准备/lease，不能为了展示其它模式就写current；Realm列表刷新不得取消尚未保存的用户目标。dropdown同值不触发变更，固定继承值需显式按钮。整行缩略图与收起的操作容器需参与过滤，否则搜索会留下孤立图块或不可见命中。
- RoundedButton基类有40px绝对Height；改RelativeSizeAxes=Both时必须同时Size=Vector2.One，否则实际按钮高度变成40倍，headless行为测试也可能通过。视觉回归核实际DrawHeight，并取真实桌面图；图卡搜索的空字符串初值可为null，边界统一为空。
- 真实PlayerLoader原先先登记initial Live屏障，再加载Player；把最终键数换肤塞进Player.load会被自身屏障拒绝。当前在唯一转谱/Mod之后准备scope，再登记同一pending ownership持有的屏障，注册后核revision ID；窄窗被抢先只重准备皮肤，不重复转换。只测manager会漏掉这个生产顺序，需真实进曲/重试/退出。
- current删除的保护投影不是玩家选择。OsuGame原全局镜像订阅会把它写成当前键数静线覆盖，阻断删除→模式默认；较晚C1的SourceChanged deferral不包真实C2投影。共同fallback/rollback helper用独立provenance供订阅抑制，只保留全局兼容投影；真实OsuGame的Realm/managed删除与失败回滚见[本轮记录](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-19按键数皮肤设置与素材预览)。
- 同current的scope快速成功也必须取消旧候选并废弃generation，否则迟到managed回调会改回旧scope。CurrentSkin投影标记在SourceChanged前已清零，需另核pending scope的commit claim，重入只能拒绝内层，不能finish已提交外层。隔离fixture应明确清空稀疏配置，避免前例保存值冒充重入写入。
- 收起的管理动作按CanBeShown隐藏，可能未加载；真实调用测试先等卡片目标、点击展开，再等可见动作IsLoaded，不能用默认Enabled=true当作ready。设置卡片可能持有当前revision的只读lease；shutdown测试需沿OsuGameBase先销毁视觉图再关闭manager，避免把仍活着的卡片租约误判成背景work泄漏。
- 旧style测试仍写BmsRulesetSetting.PlayfieldStyle时，即使canonical已有选项也不会改变新root。真实非默认布局fixture应由该临时作者包声明presentation，再写包级偏好；不能为通过测试恢复全局读取、借canonical声明或放宽生产guard。另用原样无声明包加旧P2配置证明仍居中，历史complex源保持原样。
- 删除重入检查可能受前序测试状态影响，孤立通过不证明修复；先核对完整消息与失败阶段，再用干净修改前源码按实际前序顺序对照。路径只替换明确的worktree前缀，不能归一化行号、空白或业务栈来抹平差异；原失败分类保留，补充证据见[P1-A记录](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-18皮肤配置项与bpl作品)。

`TestSceneStartupRuleset.TestRulesetSpecificSkinPreferences` 用真实 `OsuGame` 在 BMS → mania 切换后断言两条配置和当前皮肤互不污染；`TestSceneStartupSkinMigration` 覆盖旧全局值迁移。设置布局变化后仍需保留现有 `ChildrenOfType` caller（删除、脚本授权、刷新）并运行 P1-A 规定的 focused tests。不要把移除工作区 UI 写成删除旧用户数据，也不要在设置页绕过 `SkinManager.CurrentSkinInfo` 直接写提交 bindable。
