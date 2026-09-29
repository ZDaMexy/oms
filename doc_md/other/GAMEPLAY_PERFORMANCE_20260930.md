# 原生 BMS 与 BMS→mania 性能验证（2026-09-30）

## 范围与基线

用户先要求代码审查，随后授权“全量优化并验证”。从干净的 `master@034d79b26c30635298954a715e34c28d86bfd27d` 开始；fetch 成功，当时与 `origin/master` 一致。实现与验证在现有非系统盘 checkout 进行，未操作用户谱库、皮肤包或保存数据，未推送。

主归属 [P1-J](../subline/P1-J/DEVELOPMENT_STATUS.md)，BGA 归 [P1-L](../subline/P1-L/DEVELOPMENT_STATUS.md)。本轮覆盖审查发现并在旧代码上测出的重复排序、长条前缀扫描、声音维护分配、转谱整谱显示对象/重复样本准备，以及四窗重复解码。不替换音频后端、不增加通道上限、不改变窗口、血量或成绩语义；50k 真实谱问题仍须现场复现。

所有 shell 的开发命令先执行 `. .\UseDevelopmentStorage.ps1`。restore 已覆盖 core tests、BMS tests 和 desktop；build/test/formatter 串行执行，期间冻结源文件写入。证据目录为 `artifacts/gameplay-performance-20260930/`，以下测试统一 Release。微基准测具体调用及固定合成谱；时间受机器负载影响，**不代表整局 FPS 或任意真实谱的提升倍数**。

## 修改前后证据

旧生产代码下先执行 `baseline.trx`，转谱测试的 ScrollSpeed 数值类型修正后另有 `baseline-converted.trx`。基准数字取自 TRX 的测试/运行输出，未把不编译的首次 fixture 草稿算作有效结果。

| 场景与口径 | 修改前 | 修改后 | 原因 |
| --- | --- | --- | --- |
| 64 个候选、256 次提前按键 | 分配 43,388,040 B；118.068 ms | 0 B；22.988 ms | owning 容器复用稳定有序快照 |
| 65,536 次长条模式读取 | 分配 5,767,168 B；8.128 ms | 0 B；0.748 ms | 每局解析一次 Mods |
| 长条晚段 16,384 次推进（LN/CN/HCN） | 88.030 / 83.281 / 82.802 ms | 2.771 / 3.022 / 2.799 ms，前后均 0 B | 游标跳过已处理 tick，撤销/Apply 重置 |
| sample 稳态 2,048 次 Update；idle/playing/stopped 分别测 | 各分配 196,608 B | 各 0 B | 无完成项时不建集合；Stop 同样覆盖 |
| 转谱末列 2,048 次查询；手动/自动分别测 | 各分配 655,360 B；32.221 / 36.879 ms | 各 0 B；0.162 / 0.296 ms | 已完成前缀游标和缓存索引，加载期预建 |
| 转谱 384 BGM + 96 scratch 的保留显示对象 | 480 | 34 | Column 既有池按生存窗口复用；含空闲池对象 |
| hosted store 接管后的 tap/head 普通 sample 数 | 各 1 | 各 0 | 保留数据与 prewarm，只省重复准备；普通 mania 仍各 1 |
| 14K 四窗：player / base image 读取 / 同一视频解码实例与读取 | 各 4 | 各 1 | 第一窗拥有内容，另外三窗绘制同一缓冲画面 |

修改后本表取自 `optimized-focused-final.trx`，30 项全通过。native 提前按键约减少 80.5% 该段耗时；转谱合成场景保留对象减少 92.9%。这些比值仅对应上表口径。进入新生存窗口、缓存重建和有效判定本身仍允许必要分配，不宣称整局零分配。

## 行为保护与诊断修正

- 手动和自动转谱均验证 BGM/皿次数、头音一次、尾静音、空击不重播伴奏、普通 mania 发声，以及首次查找不把全谱排序推给玩家。
- 有序索引覆盖相同时刻顺序、StartTime 编辑、离开生存期、回退、移除、未激活 entry 和解绑；长条保留完整 tick 结果与撤销。
- sample 维护保留多通道完成、旧 revision 尾音、先销毁 drawable 再释放 lease、热更与 shutdown 合同。
- BGA 使用真实图片和嵌入视频解码，检查四窗几何、单次读取、POOR、暂停位置、回退 revision 与替换后旧 player/video 释放。实际渲染结果另列，不用 headless 成功代替像素证据。
- 首轮新增长条回退夹具漏掉 nested Apply，导致没有真正订阅 Entry.RevertResult；补齐真实通知次序后保留重判 6 个、复用 3 个的强断言，未为此修改生产游标。
- 转谱显示对象计数读取 DrawablePool.CurrentPoolSize，不能只数当前子树而漏掉闲置对象。
- 新增真实转谱 Player 回退测试复现皮肤 Reset 提前发布空对象快照，而相同目标时刻的后续 attach 已有音符。P1-A 约束明确 Snapshot/Reset 必须原子携带完整 baseline，因此不能只放宽断言。共享事件宿主现在等待 frame-stable clock 追上当前 gameplay clock 后，再完成既有一帧 playfield barrier；未增加新时钟或调度器。手动/自动两个强断言保留，专项复跑通过。
- 新增 core 容器夹具的裸 HitObject 未设置 HitWindows，会使另外两个对象在开始时也到期，不能表示预期的 3→2 生命周期。夹具改用 HitWindows.Empty 与自己控制的到期边界；保留 3→2→3、稳定顺序、编辑和移除断言，未为此修改生产缓存。重新编译后 core 605 项全通过。
- 首次 BMS full 为 2,412 通过、30 失败、16 跳过。除 29 项已知旧失败外，`TestLegacyTranscodeFailureBecomesUnavailableAndLeavesNoPartialFile` 捕获六月已有的竞态：failedDestinations 先发布，finally 稍后才删 tmp。该 cache/fixture 原本均零 diff，本轮共享 player 路径不参与此独立测试；失败标记与摘要日志移到清理尝试之后、移除 inProgress 之前。现有“无半成品”强断言保留，不靠 sleep/retry 掩盖。

## 桌面渲染证据

`TestSceneBmsBgaSharedContent` 由真实桌面 host 运行全部步骤，退出码 0。截图 `bga-shared-content.png` 的四窗为青色，`bga-shared-content-poor.png` 四窗同步变红；逐 viewport 的屏幕中心像素断言与 DrawSize 检查均通过。其后的真实视频出帧、位置 200 ms、暂停保位、回退 revision 和旧 decoder 释放也随同一场景完成。

开发存储约束要求不生成系统盘开发产物，而默认 exact-test host 仍会使用 AppData。临时探针在 `.dev-cache/temp/bga-render-host/` 复用现有 ExactVisualTestGame，采用框架已有 `HostOptions.PortableInstallation=true`，并校验自身输出目录；host storage/cache 留在该探针输出内，game 使用临时隔离根。不改系统路径或全局设置、不启动真实 OMS 保存根。探针 source/result 保存在证据目录的 `render-probe/`，build/restore 日志和截图亦已留存。已确认探针进程退出；精确路径清理被执行策略拒绝，未提供具体原因，目录原样保留，**清理未完成**。没有换方式绕过，也未清空其它测试目录或依赖缓存。该探针不扩张为正式产品入口。

## 最终软件验证

| 验证 | 有效结果与证据 |
| --- | --- |
| 原生/转谱/音频/BGA 性能与回退专项 | 30 通过，`optimized-focused-final.trx` |
| core skin/gameplay、容器顺序与 pooling | 605 通过，`core-final-r2.trx`；夹具修正后重新编译 |
| BMS full | 2,413 通过、29 既有失败、16 跳过，`bms-full-final.trx`；cache 失败清理修复已通过 |
| mania full | 867 通过、5 既有失败，`mania-full-final.trx`；4 项与留存基线一致，1 项在纯 HEAD 重新编译复现 |
| 真实桌面 BGA | Exit 0，两个 PNG 与 `render-probe/result.json` |
| Release | 成功，0 错误、2 个既有警告，`release-final.log` |
| 格式校验 | solution 与 core owning project 的 whitespace verify 均通过，`format-desktop-verify.log` / `format-core-verify.log` |
| 文档与差异校验 | 见 `documentation-final.log` 与最终 `git diff --check`；仅公开路径/指纹提示保留审阅 |

主要命令（每个新 shell 先加载开发存储；日志与 TRX 全部在同一证据目录）：

```powershell
dotnet restore osu.Game.Tests/osu.Game.Tests.csproj
dotnet restore osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj
dotnet restore osu.Desktop.slnf
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~BmsNativeGameplayPerformanceTest|FullyQualifiedName~TestSceneBmsConvertedGameplayPerformance|FullyQualifiedName~TestBmsSampleSteadyUpdateAllocation|FullyQualifiedName~TestBmsCompletedSampleBatch|FullyQualifiedName~TestSceneBmsBgaSharedContent|FullyQualifiedName~TestSceneBmsBgaPanelLayout|FullyQualifiedName~TestSceneBmsGameplaySkinTimingEpoch" --logger "trx;LogFileName=optimized-focused-final.trx" --results-directory artifacts/gameplay-performance-20260930
dotnet test osu.Game.Tests/osu.Game.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~Skinning|FullyQualifiedName~GameplaySkin|FullyQualifiedName~TestSceneHitObjectContainer|FullyQualifiedName~TestScenePoolingRuleset" --logger "trx;LogFileName=core-final-r2.trx" --results-directory artifacts/gameplay-performance-20260930
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj -c Release --no-restore --logger "trx;LogFileName=bms-full-final.trx" --results-directory artifacts/gameplay-performance-20260930
dotnet test osu.Game.Rulesets.Mania.Tests/osu.Game.Rulesets.Mania.Tests.csproj -c Release --no-restore --logger "trx;LogFileName=mania-full-final.trx" --results-directory artifacts/gameplay-performance-20260930
dotnet build osu.Desktop.slnf -c Release --no-restore -p:GenerateFullPaths=true -m -verbosity:m
```

core 实际执行包含 `TestSceneHitObjectContainerOrdering`、现有 `TestSceneHitObjectContainer`、`TestSceneHitObjectContainerEventBuffer` 与 `TestScenePoolingRuleset`。旧 `TestSceneGameplaySampleTriggerSource` 因引用已剔除 OsuRuleset 而不参与编译，不能把该过滤器无匹配当通过；本轮用真实转谱 Player 覆盖首/末原始 fallback、LN nested 选音、32 次实际空击、首次和结束查询、回退以及普通 mania 样本行为。

中间失败保留原始 TRX，不覆盖成最终全绿。BMS 最终旧失败逐项对照来自 2026-09-29 留存基线，证据为 `bms-failures-final.json`：29 项的身份/消息一致，28 项仍在 Workspace getter 的 Single 失败，1 项仍要求已恢复的编辑按钮禁用。逐项保留业务行号与 lambda 编号，仅规范化新 partial fixture 引起的 DisplayClass 数字、移除两个 RunTestBlocking 转抛包装帧后，完整栈一致；旧基线为 Debug，本轮 Release，未把包装栈差异误记为产品回归。最终完整回归没有新增 BMS 失败，16 个需要额外环境/人工材料的跳过也未算通过。

mania 的 `TestHoldNoteChord`、`TestHoldNoteStair`、`TestHoldNoteWithReleasePress`、`TestSingleHoldNote` 与 9 月 29 日留存基线的消息和规范化堆栈一致，见 `mania-failures-prior.json`。第五项 `ManiaImportIntegrationTest.TestRegisterExternalDirectoryWithOnlyNonManiaBeatmapsReturnsNull` 当时未包含于 relevant 过滤器：9 月 29 日 `8c22fdf` 按 P1-H 合同将无有效谱面的 Register 改为明确报错，而 4 月旧测试仍期待 null。未回退正确的目录边界，也未为通过测试改写该旧 fixture。

为排除本轮影响，先逐字节保全 17 个已修改生产源码文件，在无其它构建/写入期间将它们恢复为 `034d79b` 的 git blob，并确认生产源码相对 HEAD 无差异；以 Release 重新编译精确执行上述单项。`mania-head-import-baseline.trx` 在旧代码下复现相同 InvalidDataException、Importer 第 108 行和测试第 62 行；`mania-import-baseline-comparison.json` 确认消息与堆栈一致。随后 finally 恢复全部优化源码，逐项 SHA-256 核对通过（`head-baseline-restoration.json`），再构建最终优化版 Release。备份留在证据目录用于审计恢复，没有创建分支或工作副本。该额外旧测试欠账回写 P1-H；本轮完整回归无新增失败，仍不称全套全绿。

Release 的两个已有警告分别为 `TestSceneFilesystemBackedStoryboardFallback.cs:151` 的 CS8600、`BmsRulesetStatisticsTest.cs:555` 的 CA2007；未新增全局 suppression。格式输入由 tracked diff 和 untracked 新 `.cs` 的仓库相对路径合并，覆盖 solution 内 BMS/mania 与独立 core tests；所有格式写入后已重新编译，最终 verify 无修改。文档初检只因 P1-L PLAN 链接标签含“本轮”触发活动交接规则，已改为稳定“性能验证记录”；不修改检查器规避。

## 人工边界与收尾

自动声音请求与真实后端位置证明不能代签设备听感；合成 BGA 像素验证不能代签真实谱叠层/ARGB/老视频保真。仍需真实谱覆盖密集和弦、layered/long BGM、快速空击、手动/自动长条、pause/seek，以及不同尺寸/DPI 的 BGA。不宣称 50k 极端谱、全部设备或公开发行已验收。
