# P1-I 开发计划：BMS 选歌筛选与搜索定制

> 最后更新：2026-05-18
> 主线总规划见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。`P1-A` 负责共享产品面边界，`P1-H` 负责 read-model / backfill authority；本文件只拆解 `P1-I` 的执行顺序。

## 子线定位

| 维度 | 归属 | 说明 |
| --- | --- | --- |
| 主归属 | `P1-I` | BMS-only Song Select 筛选 UI、搜索语法与匹配语义 |
| 协作子线 | `P1-A` | `FilterControl` 的 BMS-only product surface 分支、切 ruleset 回退与公开 UI 口径 |
| 协作子线 | `P1-H` | 谱面构成统计的持久化 read-model、backfill 与 importer/reuse authority |
| 明确不归线 | `P1-C` | 该专题服务选歌筛选，不拥有 gameplay feedback / training authority |

## 当前确认基线

- Song Select 右上筛选 UI 当前完全由共享 [../../osu.Game/Screens/Select/FilterControl.cs](../../osu.Game/Screens/Select/FilterControl.cs) 承担；控件为 `sealed`，并由 [../../osu.Game/Screens/Select/SongSelect.cs](../../osu.Game/Screens/Select/SongSelect.cs) 直接构造。
- 共享搜索解析入口是 [../../osu.Game/Screens/Select/FilterQueryParser.cs](../../osu.Game/Screens/Select/FilterQueryParser.cs)；ruleset-specific 关键字只能通过 `IRulesetFilterCriteria` 扩展。
- mania 已通过 [../../osu.Game.Rulesets.Mania/ManiaFilterCriteria.cs](../../osu.Game.Rulesets.Mania/ManiaFilterCriteria.cs) 实现 `key` / `ln` 自定义查询；BMS 当前也已接通 `CreateRulesetFilterCriteria()` 与 `BmsFilterCriteria`。当前缺口已不在 parser hook 或 `谱面构成` 产品面实现，而收窄到 `I4` focused regression：单轨拖拽 headless coverage 与 shared visual gate 仍待补强。
- BMS 键数当前已可稳定从 [../../osu.Game.Rulesets.Bms/BmsRuleset.cs](../../osu.Game.Rulesets.Bms/BmsRuleset.cs) 的 `TryGetKeyCount()` / `Difficulty.CircleSize` 读取。
- RC/LN/SCR 相关统计当前已具备 persisted metadata authority；`FilterControl` 里的 BMS `谱面构成` 也已由 `BmsCompositionFilterControl` 单轨控件收口，符合“单轨上限段 + 尾段空白容差 + 独立启停”产品合同。
- 共享 `DisplayStarsMinimum` / `DisplayStarsMaximum` 当前仍驱动 star slider；若只隐藏星数 slider 而不改 criteria 生成链，BMS 仍会被旧的星数过滤误伤。

## 首轮代码锚点

| 维度 | 首轮锚点 | 用途 |
| --- | --- | --- |
| persisted metadata | [../../osu.Game.Rulesets.Bms/DifficultyTable/BmsBeatmapMetadataData.cs](../../osu.Game.Rulesets.Bms/DifficultyTable/BmsBeatmapMetadataData.cs) | 承载 typed filter stats 与 getter/setter helper |
| import / raw wrapper | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs) | 让 decoded/raw working beatmap 与导入链共享同一份 metadata truth |
| import / reuse authority | [../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs) | 落实新导入、managed/external rebuild、reuse 命中旧 set 时的写入语义 |
| ruleset search hook | [../../osu.Game.Rulesets.Bms/BmsRuleset.cs](../../osu.Game.Rulesets.Bms/BmsRuleset.cs) | 接入 `CreateRulesetFilterCriteria()` |
| BMS custom criteria | `osu.Game.Rulesets.Bms/BmsFilterCriteria.cs` | 新增 BMS-only custom search / matching authority |
| shared parser contract | [../../osu.Game/Screens/Select/FilterQueryParser.cs](../../osu.Game/Screens/Select/FilterQueryParser.cs) | 复用 shared range/text parser，不在此新增 BMS-only switch |
| shared UI host | [../../osu.Game/Screens/Select/FilterControl.cs](../../osu.Game/Screens/Select/FilterControl.cs) | 挂载 BMS-only row branch |
| visual/style reference | [../../osu.Game/Screens/Select/FilterControl.DifficultyRangeSlider.cs](../../osu.Game/Screens/Select/FilterControl.DifficultyRangeSlider.cs) | 复用现有 sheared slider 风格，不重新发明通用控件皮肤 |
| importer / metadata tests | [../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsImportIntegrationTest.cs) | 锁住新 filter stats 写入、reuse 与 backfill authority |
| BMS song select tests | [../../osu.Game.Rulesets.Bms.Tests/TestSceneBmsSongSelectDifficultyTable.cs](../../osu.Game.Rulesets.Bms.Tests/TestSceneBmsSongSelectDifficultyTable.cs) | 复用 `BmsSongSelectTestScene` 做 BMS-only carousel / grouping / filtering integration |
| shared filter UI tests | [../../osu.Game.Tests/Visual/SongSelect/TestSceneBeatmapFilterControl.cs](../../osu.Game.Tests/Visual/SongSelect/TestSceneBeatmapFilterControl.cs)、[../../osu.Game.Tests/Visual/SongSelect/TestSceneSongSelectFiltering.cs](../../osu.Game.Tests/Visual/SongSelect/TestSceneSongSelectFiltering.cs) | 锁住共享 filter host 的 UI / ruleset-switch 回归 |

## 专题目标

1. 让 BMS ruleset 在 Song Select 中拥有一套独立于 mania 的筛选产品面：`谱面构成` + `键数`。
2. `谱面构成` 的最终产品面冻结为：单轨，从左到右 `RC / LN / SCR` 三个可编辑上限段，尾段为空白容差；三段独立启用/禁用，三个值各自表示最大占比，不强制和为 `100%`。
3. 让 BMS visual filters 与自定义搜索语法共用同一套 criteria 语义，而不是 UI 一套、搜索框一套。
4. 让 RC/LN/SCR 过滤建立在同步可读的 persisted read-model 上，而不是在 carousel 过滤阶段临时加载 playable beatmap。
5. 保持 mania 与其他 ruleset 的现有筛选 UI、搜索语法与排序/分组行为不回归。

## 分期计划

### I0：归线与语义冻结

状态：已完成

- 确认该专题新建 `P1-I`，不硬并入 `P1-A` 或 `P1-H`。
- 冻结首轮交付范围：
  1. BMS-mode 星数 slider 替换为 `谱面构成` visual filter。
  2. BMS-mode 新增 `键数` multi-select 行。
  3. BMS-mode 新增与 visual filters 对应的 custom search 语法。
- 冻结三段构成的首轮分类口径：`SCR`、`LN`、`RC` 必须构成一个 **互斥且和为 100%** 的分区，不能继续沿用 note distribution 面板里可重叠的 summary 计数。
- 冻结实现边界：首轮继续在现有 shared `FilterControl` 内做 ruleset-aware row branching，不为此引入全新的 per-ruleset filter control host。

### I1：谱面构成 read-model 建模

状态：已完成

目标：为 Song Select 筛选链提供同步、可持久化的 BMS 构成统计 authority。

建议交付：

1. 在现有 `BmsBeatmapMetadataData` 或等价的 BMS metadata ruleset-data surface 下新增专用 filter stats 模型与 typed helper，至少持久化：
   - 总 playable object 数
   - `RC` 数
   - `LN` 数
   - `SCR` 数
   - 派生百分比所需的总量 authority
   - 首轮优先增加 `Get...()` / `Set...()` 型 helper，不在 Song Select 过滤链直接解析 `RulesetDataJson`
2. 明确三段分类规则并在模型层写死：
   - `SCR`：所有 scratch playable objects，包含 scratch long note
   - `LN`：非 scratch 的 long note objects
   - `RC`：剩余的非 scratch、非 long-note playable objects
3. 百分比默认从计数派生，公式固定为 `count / max(1, total_playable) * 100`；除非后续证明有缓存收益，不把三段百分比再单独当 authority 字段持久化。
4. 新谱面写入链首轮优先落在 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs) 与 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs)；这样 raw working beatmap、导入后 `BeatmapInfo.Metadata` 与 reuse path 能共享同一 truth。
5. 在 import / rebuild / reuse 命中旧 set 的链路里同步写入这份 metadata；不得把 authority 留在仅供详情面板使用的 runtime analyzer cache。
6. 为已存在且缺失新字段的 BMS 谱面准备 backfill 路径；目标是让用户不需要手工重导整个谱库才能使用新筛选。
7. backfill 首轮优先顺序固定为：
   - 先打通 managed/external rebuild 与 reuse 自愈
   - 再决定是否补 one-shot batch backfill
   - 不把第一次实现绑定到 startup 全库扫描

验收：

- Song Select 过滤阶段可只凭 `BeatmapInfo.Metadata` 读取 RC/LN/SCR 构成数据。
- 旧谱面在回填或重扫后获得稳定统计，不因 session 或 current working beatmap 不同而漂移。

### I2：BMS ruleset criteria 与搜索语法

状态：已完成

目标：让 BMS custom search 沿现有 ruleset hook 正式接入，而不是把 BMS 逻辑塞进 shared parser 特判。

建议交付：

1. 在 [../../osu.Game.Rulesets.Bms/BmsRuleset.cs](../../osu.Game.Rulesets.Bms/BmsRuleset.cs) 中正式 override `CreateRulesetFilterCriteria()`。
2. 新增 `BmsFilterCriteria`，接管 BMS-only custom keywords 与匹配。
3. 首轮关键字范围固定为：
   - `key` / `keys`：键数筛选，语义与 UI 的 `5K / 7K / 9K / 14K` multi-select 对齐
   - `rc` / `rice`：`rice` 是公开长写
   - `ln`
   - `scr`
   - 如需可读别名，只允许补 `regular` / `scratch` 这类一一对应 alias；其中 `regular` 仅保留为兼容 alias，不再作为公开文档或 tooltip 口径
4. `rc` / `rice` / `ln` / `scr` 按百分比范围处理，延续 shared `FilterQueryParser.TryUpdateCriteriaRange()` 语义；`key` / `keys` 尽量与 mania 现有 `key` 语法保持操作符一致性。
5. 共享 `star:` 文本语法首轮保持存在；本专题替换的是 **BMS visual surface**，不是删除 shared parser 的星数关键字。
6. 首轮 `FilterMayChangeFromMods()` 默认保持 `false`；当前 `key/rc/ln/scr` 都只依赖 beatmap metadata，不依赖现有 BMS mods。只有当未来真的出现会改变筛选结果的 key-count 类 mod，再回头放宽这条约束。
7. 文本语法的范围表达能力不得为了贴合当前 visual control 而缩水；即使 `谱面构成` UI 首轮只冻结单轨共边界交互，`rc/ln/scr` 文本语法仍继续保留完整比较/范围组合能力。

验收：

- BMS visual filters 与 BMS custom search 指向同一套 criteria state。
- mania 现有 `key` / `ln` 行为不变，shared parser 不新增 BMS-only switch 分支。

### I3：BMS-only FilterControl 产品面

状态：已完成

目标：在不替换整套 Song Select host 的前提下，把 BMS ruleset 的右上筛选区切成独立产品面。

建议交付：

1. 在共享 [../../osu.Game/Screens/Select/FilterControl.cs](../../osu.Game/Screens/Select/FilterControl.cs) 中引入 ruleset-aware row composition：
   - mania / shared ruleset 继续使用现有 star slider
   - BMS ruleset 改为显示 `谱面构成` + `键数`
2. `谱面构成` 行必须是一条 BMS-only 的 single-row shared-track control，而不是三条彼此独立的 range slider：
   - 从左到右固定为 `RC / LN / SCR` 三个可编辑段，尾段为空白容差
   - `RC / LN / SCR` 三个值都可调，且各自表示该分类的最大占比
   - 三个值不强制和为 `100%`；剩余尾段空白用于表达容差
   - 三个值之和不得超过 `100%`；若编辑会溢出，当前编辑值应被夹紧或阻止
3. `键数` 行采用显式 multi-select boxes：`5K`、`7K`、`9K`、`14K`。
4. `RC` / `LN` / `SCR` 必须具备独立 enabled state；禁用某段时，该段不再从 visual UI 生成对应的 criteria/query fragment。
5. `谱面构成` 显示层继续冻结为按钮式可交互表面：
   - 默认显示 `RC` / `LN` / `SCR` 标签
   - hover 可见当前占比
   - 区域宽度足够时在段内居中显示当前占比
   - 点击段位可进入数值输入，输入的是该段的最大占比
6. BMS 分支启用时，不得继续把隐藏的 star slider state 写入 `criteria.UserStarDifficulty`；否则会产生“界面看不到星数过滤，但结果仍被星数筛掉”的幽灵状态。
7. 切回 mania 或其他 ruleset 时，必须恢复原有 shared slider / dropdown surface，而不是要求重建 screen 才能回退。
8. 首轮控件复用优先级固定为：
   - `键数` 行优先复用现有 `ShearedToggleButton` 视觉风格
   - `谱面构成` 行优先复用现有 sheared slider 视觉习惯，但不得因此退化成三个独立 slider
   - 若提炼 shared generic segmented control 会阻塞开发，则允许先做 BMS-local 私有控件，不先抽共享层
9. 当前三条独立 `range slider` 只视为原型，不得继续当作 `I3` 的最终交付结果。

验收：

- BMS / mania 来回切换时，筛选区 UI 与实际 criteria 都一致切换。
- `谱面构成` 行在 BMS 下表现为单轨上限控件：`RC / LN / SCR` 三段可编辑、各自独立启停、尾段为空白容差，且 visual UI 生成的 query 只表达各段的上限语义。
- BMS custom rows 不挤压既有 search box、sort/group、collection 的布局 authority。

### I4：回归与验证收口

状态：进行中

目标：用 focused automated coverage 锁住 BMS-only UI branch、metadata authority 与 search semantics。

建议交付：

1. metadata / importer 层测试：
   - 新 filter stats 的分类与百分比计算
   - import / reuse / backfill 写入路径
2. ruleset criteria 测试：
   - `key`
   - `rc`
   - `ln`
   - `scr`
   - 多关键字叠加与 `!=` / 比较操作符边界
3. Song Select / visual tests：
   - BMS ruleset 显示 composition row 与 key-count row
   - mania ruleset 保持原有 star slider
   - 规则切换后 UI 与 criteria 不串线
4. carousel / integration tests：
   - visual filter 与 query syntax 对同一谱面集产生一致结果
   - legacy beatmaps 在缺失 metadata 时不会 silently 被错误过滤掉
5. Release 构建与至少一轮 BMS-focused test slice 通过。

建议首轮验证命令：

```powershell
dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsImportIntegrationTest|FullyQualifiedName~BmsFilterCriteria"
dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~TestSceneBmsSongSelect"
dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --configuration Release --filter "FullyQualifiedName~TestSceneSongSelectFiltering|FullyQualifiedName~TestSceneBeatmapFilterControl"
dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m
```

## 明确不做

1. 不为此专题引入新的 ruleset-level `FilterControl` 替换 hook。
2. 不在 carousel 过滤阶段按候选谱面逐张加载 playable beatmap 来算 RC/LN/SCR。
3. 不顺手扩到 mania 新 UI、shared Song Select 新产品承诺或更多 BMS-only filter families。
4. 不在本专题里重做 note distribution 面板、密度图 authoring 或 results / gameplay feedback。
5. 不默认把 BMS visual filter state 写入 shared 星数配置项；是否做 ruleset-local 持久化，留到首轮功能稳定后再评估。

## 当前优先顺序

1. `I1` 谱面构成 read-model 建模
2. `I2` BMS ruleset criteria 与搜索语法
3. `I3` BMS-only FilterControl 产品面
4. `I4` 回归与验证收口
