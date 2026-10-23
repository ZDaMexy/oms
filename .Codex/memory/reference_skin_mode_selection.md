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

- 有preview的配置只保留整行/箭头打开图卡这一个入口；左图另开图库、右侧文字dropdown会让正常操作看不到全部预览。真实输入测试须点主体和箭头、再点图卡核scope；不能只直接调用Action。图库收起后GridContainer会延后重建隐藏cells，按IsExpanded/保存值验证收起，别要求ChildrenOfType立刻为空。截图与Xecus事故取[P1-A日志](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-19配置图库入口与xecus崩溃修复)。
- 皮肤选项与选择皮肤不同：`SkinOptionPreferences`按record+ruleset+可选键数覆盖，root准备才冻结到Context；不要为了更换图案触发reload、重选current或改包。键数恢复只清当前层，不清模式默认；作者默认仍可覆盖已改过的模式配置。首次显示控件不写覆盖，失效旧值读作者默认；声明删除后的旧ID仅在玩家主动保存时清理，避免占满32项。
- 数值网格以作者min为原点，不能直接把step给BindableDouble.Precision（framework按0取整）。提交时按min+n×step吸附，再保存canonical值。控件重建要验证值和store一致；未加载就Dispose必须容许Resolved尚为空。
- SettingsSection的旧独立caller可能没有应用级preferences/ruleset服务；可选边界保持空块，不能让无关删除/脚本/刷新入口因新块加载失败。正常GameHost缓存唯一服务，真实UI验证走该scope。
- 独立设置卡片需经manager跟踪的只读准备/lease，不能为了展示其它模式就写current；Realm列表刷新不得取消尚未保存的用户目标。dropdown同值不触发变更，固定继承值需显式按钮。整行缩略图、管理容器和折叠图库控件需参与过滤，否则搜索会留下孤立图块或不可见命中。
- RoundedButton基类有40px绝对Height；改RelativeSizeAxes=Both时必须同时Size=Vector2.One，否则实际按钮高度变成40倍，headless行为测试也可能通过。视觉回归核实际DrawHeight，并取真实桌面图；图卡搜索的空字符串初值可为null，边界统一为空。
- `GridContainer.Content`换页时为复用而移除旧子项、不替调用方Dispose。一次性图库须先从实际cell父容器Remove并释放旧卡；尚未挂载的卡直接Dispose，再发布新页。只核新页数量会漏掉遗留图片，应保留旧卡引用断言换页和收起后已Dispose；当前合同取[P1-A约束](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#皮肤配置项与显式舞台)。
- 真实PlayerLoader原先先登记initial Live屏障，再加载Player；把最终键数换肤塞进Player.load会被自身屏障拒绝。当前在唯一转谱/Mod之后准备scope，再登记同一pending ownership持有的屏障，注册后核revision ID；窄窗被抢先只重准备皮肤，不重复转换。只测manager会漏掉这个生产顺序，需真实进曲/重试/退出。
- current删除的保护投影不是玩家选择。OsuGame原全局镜像订阅会把它写成当前键数静线覆盖，阻断删除→模式默认；较晚C1的SourceChanged deferral不包真实C2投影。共同fallback/rollback helper用独立provenance供订阅抑制，只保留全局兼容投影；真实OsuGame的Realm/managed删除与失败回滚见[本轮记录](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-19按键数皮肤设置与素材预览)。
- 同current的scope快速成功也必须取消旧候选并废弃generation，否则迟到managed回调会改回旧scope。CurrentSkin投影标记在SourceChanged前已清零，需另核pending scope的commit claim，重入只能拒绝内层，不能finish已提交外层。隔离fixture应明确清空稀疏配置，避免前例保存值冒充重入写入。
- 管理动作现直接展示，仍按CanBeShown隐藏不可用项；真实点击测试先等卡片目标和可见动作IsLoaded，不能用默认Enabled=true或单个子节点IsPresent证明整条父链可见。设置卡片可能持有当前revision的只读lease；shutdown测试需沿OsuGameBase先销毁视觉图再关闭manager，避免把仍活着的卡片租约误判成背景work泄漏。
- 删除回归不能停在确认框取消，也不能只在主菜单直接调用manager：需从实际SongSelect设置卡片长按到记录删除及卡片回落。空选曲layout原先因无staged receipt被拒绝；补空→空证明后，隐藏主菜单sample的pending swap又会因不Update卡detach。真实修复由各owner完成维护，不能让manager代放lease；同步退出需从已提交fallback的真实Delete任务验证，而非用Reload替代。current变更仅重读同ID的设置preview，保护fallback不抢先重读尚未删除的managed源；成功完成和Realm通知再解析已删除目标。target先Disabled=false会同步触发Rename/Export读取旧已删Live；必须立即禁用、延后重新启用到新Value发布之后，不能吞RealmInvalidObjectException。合同及证据取[P1-A记录](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-19选曲设置确认删除修复)。
- 旧style测试仍写BmsRulesetSetting.PlayfieldStyle时，即使canonical已有选项也不会改变新root。真实非默认布局fixture应由该临时作者包声明presentation，再写包级偏好；不能为通过测试恢复全局读取、借canonical声明或放宽生产guard。另用原样无声明包加旧P2配置证明仍居中，历史complex源保持原样。
- 删除重入检查可能受前序测试状态影响，孤立通过不证明修复；先核对完整消息与失败阶段，再用干净修改前源码按实际前序顺序对照。路径只替换明确的worktree前缀，不能归一化行号、空白或业务栈来抹平差异；原失败分类保留，补充证据见[P1-A记录](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-18皮肤配置项与bpl作品)。

- managed删除后的卡片失败不能只查UI：异步reservation可跨线程存活，旧owner线程已被pool复用时，独立preview不能沿线程ID进入“同线程嵌套”。独立read须等待真实lease释放，原同步重入guard保持；等待后Live可能已删除，需边界返回明确不可用结果。UI删除完成前后还可能持旧Realm快照，必须fresh刷新再解析模式默认，不能把null传给storage resolver，也不能为一般准备错误循环重试；旧删除完成不取消较新的选择。Dropdown.Items移除选中项会由framework写本地Current，不能在其Disabled=true时直接换集合；仅同步mirror期间暂解禁并抑制保存，之后恢复ready规则。真实确认还须断言全过程无日志异常，不能卡片最终ready就掩盖中途错误。Sample graph gate内只能拆图/取lease，不能Release：同步退休续体可能反向进入publication锁，撞上后台reject下的abort而卡死。确定性等待/取消、软/硬目标消失与重复真实确认见上方本轮记录。

- 同一包在BMS可见而mania隐藏时，先对照当前模式/键数保存覆盖与包默认值，再核真实publication.OptionValues。仅BMS保存了开启而作者默认关闭，会让mania按合同继续隐藏，不能据此改共享renderer或跨模式复制配置；用同包显式开启的真实mania Player区分显示能力与设置作用域。Omni默认合同取[P1-A约束](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#iidx-素材作品约束)。

`TestSceneStartupRuleset.TestRulesetSpecificSkinPreferences` 用真实 `OsuGame` 在 BMS → mania 切换后断言两条配置和当前皮肤互不污染；`TestSceneStartupSkinMigration` 覆盖旧全局值迁移。设置布局变化后仍需保留现有 `ChildrenOfType` caller（删除、脚本授权、刷新）并运行 P1-A 规定的 focused tests。不要把移除工作区 UI 写成删除旧用户数据，也不要在设置页绕过 `SkinManager.CurrentSkinInfo` 直接写提交 bindable。
