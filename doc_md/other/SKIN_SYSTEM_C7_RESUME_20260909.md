# C7 暂停检查点与直接续接入口

2026-09-09 用户因额度要求立即暂停。**C7 未完成，仍为原七阶段中的最后阶段；不新开阶段，不推送。** 全部 agent 已冻结，没有运行中的构建、测试或游戏。基线开始时工作区干净，fetch 成功，`master` 与线上 `origin/master` 同为 `de9eb3e39e84c2d0dffe49f313a37eae61149b80`；暂停提交只是保全检查点，不是验收完成。

**历史说明（2026-09-11）：原 C7 非人工工作已完成。** 下文保留暂停当日事实，不作为最新执行状态。两款成品、实际发行和可运行集中体验包均已交付；此前被外部清理的 TEMP 记录没有充当本次证据。最终实际数据保护、精确既有失败、安装/作者验证与剩余人工未签边界统一见 [C7 报告](SKIN_SYSTEM_C7_VALIDATION_20260909.md)与 [P1-A 当前状态](../subline/P1-A/DEVELOPMENT_STATUS.md)，不复用失效路径或旧成品。

## 已保全的产品成果

- 两款普通作者包、Aurora 实际作者练习、完整源文件/制作配方/检查打包工具、预览与说明；两款支持 BMS、mania。复杂款仍是展示包和默认候选。
- canonical 普通简洁包接管、只读原件验证、工作副本完整恢复、旧数据/未知固定 ID/中断 journal 保全、普通导入导出、安装异常阻止游玩/预览。旧 OmsSkin 只保留历史证据与人工对照，不是产品回退。
- 集中验收/更新/保存位置工具，原 V 输入、19 份真实原生观察谱生成器、无需外部转码的原创 MP4；尚未组装最终发行与验收包。
- 原 V-001～V-004 仍 0/4，V-005 未签收；不宣称 Skin V1 或 release 完成。

## 暂停时必须知道的不一致与未验问题

1. **作者配方比成品新。** `tools/SkinAuthoring/SkinRecipe.cs` 已补 mania 松开/按下不同素材、关闭各舞台重复文字、复杂控台准确率/进度、静线/Aurora 普通公开 scene 顶栏；`SkinPreview.cs` 同步。`sources/*/skin.ini`、scene 和 `dist/*.osk`、PNG 尚未重新生成；源 README 已改，因此当前源与旧成品不能声称逐字节一致。旧 dist 有18份普通声音但尚缺最新信息/按下态改进。
2. **新增公共信息实现已编译，测试未验。** `score.accuracy`、`timing.progress` 沿唯一事件流提供数值0～1及百分数文字，进度沿真实谱面可玩首尾和既有游玩时钟；普通语义文字也补齐。Game Release 成功（`%TEMP%/oms-c7-public-info-build.log`，0 error/9既有 NU1902）。新增 `TestSceneGameplaySkinEssentialInformation`、EventRuntimeHost/SceneRuntimeHostTest 的新增用例尚未编译/执行；TECH/作者公共目录与 memory 仍需完成新字段同步。
3. **最新两玩法测试尚未编译。** 旧 Oms 默认测试改为真实 canonical/普通导入，历史几何仍明确测试旧实例且不改原断言；修正了非公开纹理对比、stage lookup 与 current canonical 的 SelectedPackage 来源身份。新复杂演出、声音、按下态、三源 G1、首次作者目录，以及完整 HUD 矩阵须实际执行。
4. `ExactLayoutJourneyHost` 已给 mania 子树缓存独立计分/血量，并按 Player 的 NewResult/RevertResult 顺序接真实计分；此前只有 judgement 事件、分数恒零且 mania 继承 BMS processor，旧结果不能证明最新信息正确。新行为可能暴露需修的测试问题。
5. root 已把 `CanonicalSkinProductsTest` 的 mania HUD 断言改为：各 stage 文字明确 Suppress、Global 真正显示百分数，gauge 仍完整替代。**必须先重生成新包才能运行这组新断言。** 静线/复杂顶栏实际 SafeBounds、最终屏幕位置与文字边界测试尚未补；1K窄舞台溢出/重复信息已通过作者方案修正，但不能只凭 gate ready 或预览假称画面通过。
6. formatter 曾完成七个 owning 工程及工具的当时修改；随后又有公共字段/测试/配方修改，最终格式门未完成。不要在旧编译产物上使用 `--no-build`。

## 已有有效证据与失效范围

- `%TEMP%/oms-c7-tests/c7-core-candidate.trx`：1312总、1307通过、5原有失败、0跳过；新增 canonical/旧数据/导出/链接保护通过。原 `TestSampleUpdatedBeforePlaybackWhenNotPresent` 因固定真实声音前提而 Passed，原断言未改。**此结果早于新准确率/进度实现，须复验。**
- `%TEMP%/oms-c7-tests/c7-products-fixed.trx`：旧版372款式/屏幕产品格及19原生输入已通过，另10项暴露夹具和目录限制问题，已经据证修正但未完成最终复验。旧成品/旧 HUD 范围不算最终双包通过。
- `%TEMP%/oms-c7-tests/c7-backup-compact.trx` 的9个新 G1失败分别定位为实际导入记录读取、未挂玩法树、首次 chartskin 缺失；已修夹具和真实首次登记产品问题，尚待复验。
- `%TEMP%/oms-c7-tests/c7-mania-legacy-initial.trx` 保留旧类型/旧默认前提导致的失败；最新适配未执行，不能按数量归为旧基线。
- 最终作者重现验证暂对应旧 dist。更新保护、原生 MP4 制作/重现、PS5语法与链接边界证据见 [C7报告](SKIN_SYSTEM_C7_VALIDATION_20260909.md)及随包证据；它们不等于最终游戏启动。
- C6精确失败基线在 `%TEMP%/oms-c6-tests/`；比较工具默认要求 core6/mania4逐名、分类、完整消息一致。只有新运行确认原声音用例唯一 Passed，才使用 `-ResolvedCoreSampleFixture`，其余core5/mania4仍严格比较。

## 收到“继续”后的顺序

1. 按 AGENTS 先查状态、HEAD/跟踪差异并 fetch，保全检查点；读本页与 [P1-A状态](../subline/P1-A/DEVELOPMENT_STATUS.md)、[计划](../subline/P1-A/DEVELOPMENT_PLAN.md)。不要退回旧默认或重做 C1～C6。
2. 确认所有作者修改完整，先编译 `tools/SkinAuthoring/SkinAuthoring.csproj -c Release`（它依赖 Game）。重生成三源、检查、打包/摘要/预览、更新公开目录，实际运行作者重现与错误保护。普通导入只消费副本，不能删安装原件或 dist。
3. 补顶栏位置/必要信息实际消费证明；统一 owning formatter（仓库相对 include）后重新编译 `osu.Desktop.slnf` **及独立的 `osu.Game.Tests/osu.Game.Tests.csproj`** Release。逐个修真实新失败，不能删检查或改预期凑通过。
4. 串行执行 core Skin/FileStore、BMS/mania relevant和full。优先跑新信息、声音/按下态/复杂演出、首次登记、旧 Oms适配与三源G1，再宽测。root统一调度，源冻结；无隔离输出不得并行 build/test/formatter。
5. G1 使用先前已验证的**真实备份 baseline 的一次性工作副本**；暂停时的私有续接值曾位于 `%TEMP%/oms-c7-resume-private.json`。当时没有非protected皮肤记录，新 G1 在一次性工作副本注入真正预存普通皮肤验证非空保护，原有未知文件仍须保全。不要把它写成原根本来有非空用户记录，不打开用户原 Realm。重跑前后只读摘要核对；当前已替换为上方报告记录的新真实备份依据。
6. 运行 `build-release.ps1`；审查完整 publish 清单、只读双原件、独立作者工具和干净源交付，再组装 `skin-c7-acceptance/Build-Acceptance.ps1`。审阅并执行本机 `%TEMP%/oms-c7-release-startup-proof-20260909.ps1`，使用全新输出根取证便携/自定义/工作副本损坏恢复；该脚本尚未启动过游戏。再验证实际发行覆盖更新和重启，非便携冷启动需要隔离 Windows 账户，不触碰现有默认根。
7. 独立复核最新修改，同步 P1-A 四件套、新字段合同、必要 memory、C7报告和主线摘要，记录最终实测结果；文档/diff检查后在当前分支提交。推送仍单独等用户确认。

最终只允许留下真实视觉、设备与长时体验等人工项；本页未列为通过的非人工事项必须继续完成。暂停不建立新阶段，也不计作 C7 完成。
