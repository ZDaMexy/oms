# 2026-11-24 osu!lazer 上游整合评估

评估对象为 Windows 客户端。建议保留当前 OMS 产品线，优先分别移植两项已有对照证据的修复，同时开出独立的 .NET 10 迁移任务。后续按玩家收益推进输入与导入可靠性、选歌性能、mania 编辑和回放能力。本轮在独立副本验证候选，主分支只记录评估与维护路径。

## 基线与整合成本

| 角色 | 本轮固定对象 |
| --- | --- |
| 上游起点 | [`2026.305.0-lazer`](https://github.com/ppy/osu/tree/bb289363a2b8e6bf62be355f8570def018f0d7be) |
| 上游终点 | [`2026.1117.0-lazer`](https://github.com/ppy/osu/releases/tag/2026.1117.0-lazer)，`078f5a226368e62fd2974f8eb167303b41d71cab`。2026-11-24 核对为最近的正式发布。 |
| OMS 对照 | `3eaa9ae38338c60fc07de912d199ce417006d6b8`，开工时工作树干净，fetch 后与 `origin/master` 无差异。 |
| 当日上游开发线 | 远端 `master` 指向 `a0aee62a843cd7478e3446021ce12db7a164bda0`。本轮观察范围固定在正式 tag，用于排除当日开发变动。 |

原上游提交是发布终点的祖先。OMS 从 `0b97bbdd` 重新起算，Git 无共同祖先，其初始树相对上游原版已有 87 个差异路径，含工程元数据、资源和文档。测量整合风险时显式指定上游原版树为三方基准，读取当前 OMS 与上游终点的实际变动。

- 上游范围有 673 个提交，第一父链为 672 个。完整目录筛查按第一父链将 525 个归为仅触及桌面核心目录，95 个同时触及范围内外，52 个只触及其它目录。目录范围含共享游戏、mania、桌面、对应测试和公共项目设置。这里只登记候选来源，归类本身无法判定功能适用性。
- 禁用重命名识别后，上游改动 1,682 个路径，OMS 改动 4,472 个，交集 449 个，终点内容相同的交集有 8 个。共享 `osu.Game/` 有 147 个交集，mania 实现有 15 个。BMS 和 `oms.Input` 的源码目录只存在于 OMS，它们仍消费共享时钟、皮肤、按键、选歌和存档入口。
- 固定基线的 `git merge-tree` 返回冲突，涉及 331 个路径，含 93 个内容冲突和 238 个修改与删除冲突。内容冲突包括启动、Realm、结算、选歌筛选、SkinManager 和 mania 游玩。模拟树相对当前存在 606 个新增路径，含 CI、手机项目和被移除玩法的文件。只消除文本冲突仍会把这些路径带回。
- 两边的 [`RealmAccess`](../../osu.Game/Database/RealmAccess.cs) 在版本 52 就已表达不同内容。OMS 52 保存规则集成绩数据并继续演进至 58，lazer 52 落的是在线资源缓存。带版本名的开发库选择只在特定 Debug 启动生效。直接替换时，Debug 有读到旧库或另建库的风险，Release 有从同一文件尝试按更低版本打开的风险。迁移需以 OMS 保存根和迁移链为准，并保留可用旧版本恢复副本。见 [上游 Realm 52](https://github.com/ppy/osu/blob/078f5a226368e62fd2974f8eb167303b41d71cab/osu.Game/Database/RealmAccess.cs)。

整树同步和在新上游上重建 OMS 都要重新对齐存档、谱库、启动、BMS 接线和皮肤合同。现有目标先采用独立修复。若将来有一组确切收益只能随同一子系统取得，再为该子系统单独评估接口改造、覆盖面和回退成本。

## 有实际对照的候选

| 候选 | 当前 OMS 的对照 | 隔离候选的结果 | 采用建议 |
| --- | --- | --- | --- |
| mania 保存谱面后串轨 [#37256](https://github.com/ppy/osu/pull/37256)，`6231e06ebe` | 合成原生 14K 谱写出后重读，右侧首轨的音符落到左侧末轨。相同测试在其它 10 种键数通过。探针含每轨短音和长条，并核对头尾时刻。 | 只采用 `LegacyBeatmapEncoder` 的轨道中心坐标换算，11 种键数全部通过。连同已有 mania 转换、导入和成绩回放往返共 28 项通过。 | 先独立采用修复及适配回归。完整上游提交触及手机项目，并借用 OMS 已因其它玩法依赖而排除的 core 测试。移植时用独立的 mania 测试，保留 BMS 转谱分流。 |
| 大谱集更新后返回选歌 [#38514](https://github.com/ppy/osu/pull/38514)，`fc39aa5cec` | 128 个难度的谱集更新，选歌在更新线程同步向 Realm 刷新 128 次。谱面数及重选 ID 检查通过。 | 只刷新当前难度，调用数降到 1。选歌更新测试类 21 项通过，覆盖迟到的已删除难度、选中难度保持和 BMS 难度表批量刷新。 | 可独立采用，收益对应谱集更新和回到选歌。大库帧时间与普通滚动仍需各自验收。 |
| 只切换 .NET 目标 [上游 #38039](https://github.com/ppy/osu/pull/38039) | 当前桌面和共享项目是 `net8.0`，微软于 2026-11-10 结束 .NET 8 和 .NET 9 支持。 | 撤掉前两项补丁，仅把 8 个客户端和测试工程改为 `net10.0`，用 SDK 10.0.400，保持现有框架、依赖和玩法实现。桌面 Release 通过，BMS 137 项及 mania 22 项通过。Release 新增 3 种 `NU1510` 冗余引用警告，restore 和编译各报一次，另有基线的 2 项警告。 | 优先独立推进。正式迁移须处理 VS Code 启动、测试和皮肤工具中写死的框架目录，复核仍使用的 9.x 依赖，核对自包含发行的实际运行时和更新后打开旧数据的行为。见 [微软生命周期](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core)。 |

基线与两项修复合并试件均以现行 `net8.0` 构建 Windows Release，结果各为 0 错误、同两项测试工程警告（`CS8600`、`CA2007`）。去掉选歌计数插桩后，两项修复合并试件另通过 BMS 117 项专项，覆盖选歌分组、判定、计分、TOTAL 回放及直读资源的 storyboard 保底。运行时试件独立于该结果，覆盖同一组 BMS 测试及 20 项输入处理软件测试，另验 mania 转换、导入、成绩回放和批量松键。

以上在隔离数据和测试宿主上完成。运行时试件的 core 测试工程也设为 `net10.0`，本轮的桌面解决方案筛选器不含该工程，运行结果来自 BMS 和 mania 工程。正常窗口帧时间、真实设备、旧用户库覆盖升级、IR 实服、长条与 BGA 实谱，以及发布包载荷仍需对应验收。

## 下一批可取得的收益与条件

34 个具名候选逐个检查了原始补丁能否直接应用，7 个可以，27 个有路径或上下文差异。两个已验证候选说明，文本能用和产品能用需分别判断。以下按已查明的依赖和用户路径排优先级。

| 用户收益 | 候选与取舍 | 采用时看什么 |
| --- | --- | --- |
| 陌生谱面导入、加载更稳 | storyboard 解析与循环修复 [#38505](https://github.com/ppy/osu/pull/38505)、[#38817](https://github.com/ppy/osu/pull/38817) 的文本可应用。归档大小和重复文件检查 [#38845](https://github.com/ppy/osu/pull/38845)、[#38792](https://github.com/ppy/osu/pull/38792) 要先定位 OMS 入口，大小策略还需一起读[后续上调](https://github.com/ppy/osu/commit/25efb546e4cc8ae70d7bfdbe8f571db1e9c3da2e)。 | 先将保活、取消和异常路径接到真实消费者。用原生 mania 与共用 storyboard 路径验证，区分一般归档、Sayobot 和 BMS 下载各自的解包与预检，保有旧库和用户可读的失败提示。 |
| 大库浏览、过滤和重载更顺 | [#37666](https://github.com/ppy/osu/pull/37666) 将本地最佳查询由谱面记录关系转成按谱面哈希检索并建索引，OMS 还有当前玩法及其成绩分组。[#37701](https://github.com/ppy/osu/pull/37701) 含旧多人筛选改名，拖拽节流可单独取思路。Realm 冻结对象释放 [#38572](https://github.com/ppy/osu/pull/38572) 可直接套文本，但牵涉存储移动和阻塞。 | 以同一真实大库比较滚动、拖拽和返回选歌。对照重扫或缺失恢复前后的旧成绩、玩法分组、离线本地最佳和用户切换。Realm 生命周期变化用隔离根验证连续移动、锁释放及订阅恢复。 |
| mania 编辑更安全、长条外观更稳 | 高键数保存保护 [#37668](https://github.com/ppy/osu/pull/37668) 可套文本，排在串轨修复之后。旧皮肤长条变暗 [#37008](https://github.com/ppy/osu/pull/37008) 改动 pooled drawable 生命周期，OMS 的松键和旧皮肤分支已改。改滚速两处 [#37149](https://github.com/ppy/osu/pull/37149) 与[后续更正](https://github.com/ppy/osu/commit/b3c15339d0) 应一起评估。 | 高键数、长条与速率变化同时核对编辑和保存后的实际谱。长条效果跨旧皮肤、静线、回放倒带、对象复用和 BMS→mania 检验。 |
| 皮肤编辑确认正常弹出 | 上游重置修复 [#38324](https://github.com/ppy/osu/pull/38324) 修的是 [#37838](https://github.com/ppy/osu/pull/37838) 引入的一段弹窗判断，OMS 当前缺该前置。 | 先在 OMS 真实皮肤编辑器核对重置、退出和保存确认。只有复现同类问题才移植，局内自动弹窗继续遵守当前启用条件。 |
| 搜索和回放有可用的新能力 | 按键数分组 [#37285](https://github.com/ppy/osu/pull/37285) 涉及已定制的 OMS 分组层，跟筛选 [#38228](https://github.com/ppy/osu/pull/38228) 一起核对原生和转谱；现有键数条件已存在。回放按住键短暂加速 [#38006](https://github.com/ppy/osu/pull/38006) 以独立体验候选推进。 | 键数分组沿 OMS ruleset 语义，核对 9K、14K、Dual Stages 和转谱范围。回放同时验释放、失焦、seek、音频、BGA 与现有手柄键绑定。 |
| 局内键音独立可调 | [#38990](https://github.com/ppy/osu/pull/38990) 依赖 [osu-framework #6834](https://github.com/ppy/osu-framework/pull/6834)，还要带上 [#39208](https://github.com/ppy/osu/pull/39208) 的旧音量迁移。上游切换时钟与 Windows 偏移亦有联动，见 [#37856](https://github.com/ppy/osu/pull/37856)、[#38025](https://github.com/ppy/osu/pull/38025)。 | 先定原生 BMS 和转谱中普通键、皿、BGM、界面音效、storyboard 音效分别受哪个设置控制，再对照暂停、seek 和设备听感。音量变化须保持计分、回放和原 BMS 键音同帧。 |
| storyboard 作者功能和结算扩展 | storyboard 保存相关修复 [#38877](https://github.com/ppy/osu/pull/38877)、[#38895](https://github.com/ppy/osu/pull/38895) 依赖新 [encoder #37790](https://github.com/ppy/osu/pull/37790)，OMS 当前树没有对应 encoder。结算和健康图 [#39078](https://github.com/ppy/osu/pull/39078) 会改变共享回放字段。 | 按完整功能安排。新作者功能证明编辑、持久化和重新加载。血条字段变化验历史 .osr、失败、TOTAL、BMS 灯和皮肤读取。 |
| 评分或在线生态 | 新乘数 [#37967](https://github.com/ppy/osu/pull/37967)、评分/难度版本 [#38226](https://github.com/ppy/osu/pull/38226)、[Q2 SR/PP #37850](https://github.com/ppy/osu/pull/37850) 需要玩法和历史成绩兼容方案。上游在线账户、多人、自动更新、其它玩法及上游默认主题不进入 OMS 移植范围。 | 评分线单独对照旧分、新分、同谱 ID 和总分版本。影响 OMSIR 的事实或契约时遵循 Client Bridge 到 Dev Bridge 的现有链路。 |

## 复跑与采用顺序

1. 从 [上游维护入口](UPSTREAM.md#复查命令) 重做固定端点比较。对选定补丁以 `git diff <commit>^ <commit>` 获取原始变更，再对固定 OMS 副本运行 `git apply --check`。在本地重演失败和修复，每项保留独立提交与来源。先处理串轨和大谱集刷新。
2. 把运行时方案单独作为 .NET 10 迁移，先完成解决方案、VS Code、正常窗口、自定义根、输入、键音和 BGA 再进发行验证。单独判断是否要升 `osu-framework`。本轮证据表明当前核心可先换 .NET 目标，完整上游使用的 Framework `2026.1117.0`、Resources `2026.1112.1`、NUnit 4 和 C# 14 仍需按实际依赖评估。依赖比较也保留 OMS 现有的 SharpCompress `0.50.7` 和原生媒体修复，上游在该 tag 使用 SharpCompress `0.50.4`。
3. 以后续真实收益和前置为条件，优先验证导入和 storyboard 客户端卡死，再按实测推进大库、mania 编辑、回放和独立键音。数据、评分与底层时钟的改动分别验证并保留各自回退位置。玩家真实库与正式发行沿现有验收入口。

本机原始证据在 `artifacts/upstream-audit-20261124/`。其中 `full-merge-tree.txt`、`shared-paths.txt`、`upstream-commit-scope.txt` 和 `candidate-apply.json` 支持复核目录筛查。对应 `*.trx`、日志、探针源码和补丁支持重放三个实验。选歌基线与候选计数补丁只用于复跑操作计数，Release 组合以两处产品补丁构建。
