# BGA 皮肤呈现与作者手册验证（2026-09-30）

## 范围与基线

用户授权在既有性能优化后实施 BGA 的皮肤呈现控制，并要求制作手册同时覆盖基础指引与系统上限。开始时 `master@807064b` 干净、相对已记录的 `origin/master` 领先 1；接续 fetch 因 TLS EOF 失败，不能把本地远端引用当作最新线上状态。没有新建分支或操作玩家保存根，未经确认不推送。

归属 [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md) / [P1-L](../subline/P1-L/DEVELOPMENT_STATUS.md)。本次不恢复静线外观打磨，不扩张 mania 或 BMS 转谱的 BGA 支持，不更新硬件、完整真实谱或安装发行签收。

## 玩家与作者能力

- 每局游戏拥有唯一 BGA 播放会话。皮肤显示销毁、重建和增减窗口保留同一个 player、视频位置和 POOR；退出游戏才释放该会话。皮肤只取得画面 view 与只读状态，不能换媒体或控制播放时钟。
- `[Bms]` 的每个 Keymode 可声明 `BgaViewports: x,y,width,height,fit; ...`，最多 16 窗；`fit/fill/stretch` 分别为完整适配、居中裁切和拉伸。时间线内容先在固定 4:3 画布合成，再适配整幅画面，保持 base/layer/POOR 的相对关系。没有时间线的背景图保留自身比例。
- `none` 明确关闭窗口；没有声明继续旧自动布局（14K 默认四窗）。窗口相对安全区域定义；实际与轨道、键区、血槽、HUD、信息区碰撞时整组不显示，不擅自更改作者坐标。诊断沿现有成功发布通道进入 `runtime.log`，代码为 `bms.layout.bga-viewports-unavailable`。
- `BgaInformationHeight` 独立于窗口开关，零窗仍保留曲目、判定、速度和玩家信息。专属 BGA 场景装饰完整验证其声明、所有者、资源和层次后省略，不为零窗绕过作者包验证。BGA 外框池按实际窗口数分配。
- 多窗预算按每个实际窗口大小和复制数量累计：第一窗很小不能掩盖后续大窗的效果表面，Global 模板的绑定预算计入全部窗口，显式索引只计对应窗口。超量输入在准备时拒绝，不通过创建巨型 GPU 表面试探上限。
- 关闭显示或零窗停止合成画面重绘，播放状态继续按游戏时间推进。单窗也经过共享画布，这是会话与显示寿命解耦的成本；本次不宣称单窗 FPS 提升。

## 制作手册

唯一完整内容位于发行套件 [SKINNING](../../skin-authoring/docs/SKINNING.md)、[入门](../../skin-authoring/docs/START_HERE.md)和[脚本](../../skin-authoring/docs/SCRIPTING.md)，旧仓库入口只保留路由。覆盖实际元素位置、文件名、尺寸起点、编号帧、布局、三态资源、场景动画/绑定/状态/变体/模板、脚本权限和预算，以及排错和发布步骤。历史实机图明确日期，素材预览不冒充游戏截图；主导航和示例不依赖源码仓库。

三个练习分别是可独立打包的 First Scene、可独立使用的 BGA Layout，以及先复制静线再覆盖三个文件的 Reference Study。示例内容作为测试输入复制，避免测试维护另一份“看起来一样”的样例。游戏暂停会冻结整个游玩场景，不能用状态机制作即时更新的暂停菜单；示例验证暂停保持上一帧、恢复后重建 RUNNING。BGA 是 Common 目录部件，只应用于 BMS 应通过 Target 限定，而非移入 Bms 扩展目录。

## 验证记录

每个新开发 shell 先执行 `. .\UseDevelopmentStorage.ps1`，构建、测试和格式化由同一执行者串行调度，相关源码在检查期间冻结。日志、TRX、打包样例和桌面截图保存在 `artifacts/bga-skin-authoring-20260930/`。中间失败保留原始日志，不计为通过。

| 验证 | 结果与证据 |
| --- | --- |
| BGA、真实作者例子与布局专项 | 126 通过、无失败或跳过；`bms-focused-final.trx` |
| 日志范围修正专项 | 5 通过，三种来源旧诊断及 BGA 冲突诊断均保留正确范围；`diagnostics-final.trx` |
| core skin/gameplay、顺序与池化相关 | 624 通过、无失败或跳过；预算修正后 `core-final.trx`，最终日志修正后 `core-diagnostics-final.trx` |
| Release | 成功、0 错误、2 项既有警告；`release-final.log` |
| 作者工具 | 最终编译无错误或警告；三个例子实际 new/覆盖（Reference Study）、check、pack 成功，输出 `.osk` 与逐步日志留存；最终工具复查见 `author-checks-final.log` |
| 作者工具负例 | NaN、越界、非法大小写模式、17 窗分别 exit 1，精确行号 7 与稳定代码；`author-negative-results.json`。First Scene 排错练习的未知绑定返回 `OMS-SKIN-SCENE-018`，见 `first-scene-negative-binding.log` |
| 离线手册内容 | 55 个主导航/示例本地链接存在且在套件内；`manual-links.json`。历史截图 SHA-256 与原图一致 |
| BMS full | 2,454 通过、29 既有失败、16 跳过；`bms-full-final.trx`，逐项对照 `bms-failures-final.json` 全部一致 |
| mania full | 867 通过、5 既有失败、无跳过；`mania-full-final.trx`，逐项对照 `mania-failures-final.json` 全部一致 |
| 真实桌面 BGA | Exit 0，`bga.png` / `bga-poor.png` / `bga-projections.png` 及 `render-probe/result.json` |
| 格式、文档、差异 | Desktop、core tests、作者工具的最终格式检查通过；`format-*-final.log`。文档与差异检查见 `documentation-final.log` / `diff-check-final.log` |

专项包含实际选包后的 First Scene 未授权/授权，以及 Reference Study 明确拒绝/授权的两模式路径，真实按键驱动得分、判定换图、进度、旋转、模板文字及历史/血量缩放；5/16 窗作者外框、唯一 player、none/无安全空间仍保留普通 HUD 与静线四类信息区域。预算负例来自真实选包后的 prepare：小首窗加大后窗的效果面积、16 窗模板展开绑定超量均拒绝；显式索引按实际窗口计量且合法用例通过。核心 parser 检查有限数值、边界、模式、数量与结果不可变性。

主要命令（所有命令在上述开发存储环境、Release 产物下执行）：

```powershell
dotnet build osu.Desktop.slnf -c Release --no-restore -p:GenerateFullPaths=true -m -verbosity:minimal
dotnet test osu.Game.Tests/osu.Game.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~Skinning|FullyQualifiedName~GameplaySkin|FullyQualifiedName~TestSceneHitObjectContainer|FullyQualifiedName~TestScenePoolingRuleset' --logger 'trx;LogFileName=core-final.trx' --results-directory artifacts/bga-skin-authoring-20260930
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=bms-full-final.trx' --results-directory artifacts/bga-skin-authoring-20260930
dotnet test osu.Game.Rulesets.Mania.Tests/osu.Game.Rulesets.Mania.Tests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=mania-full-final.trx' --results-directory artifacts/bga-skin-authoring-20260930
dotnet build tools/SkinAuthoring/SkinAuthoring.csproj -c Release -m -verbosity:minimal
```

专项过滤按 fixture/method 覆盖 `BmsAuthoredBgaLayoutTest`、`TestBgaWindowsUseOnlyTheAcceptedSelectedPackageBucket`、`TestSceneBmsBgaSharedContent`、`TestSceneBmsBgaPanelLayout`、`TestManualFirstSceneInBothRulesets`、`TestManualReferenceStudyInBothRulesets`、`TestNoBgaWindowKeepsTheAuthoredHud`、`TestSimpleSkinInformationRemainsWhenBgaWindowsDisabled`、`TestAbsentBgaSurfaceStillValidatesSceneOwnership`、`TestEveryAuthoredBgaWindowMountsItsFrameWithOnePlayer`、`TestBgaBudgetCountsActualWindowProjection`、`BmsGameplayLayoutSolverTest` 和 `TestSceneBmsGameplaySkinTimingEpoch`；没有把无匹配或跳过算作通过。Release 既有警告仍为 `TestSceneFilesystemBackedStoryboardFallback.cs:151` CS8600 和 `BmsRulesetStatisticsTest.cs:555` CA2007。

已确认的中间问题：框架 view 没有 OnDispose 事件，改为显示端明确释放 view 登记；新增测试漏 import 和修改池函数签名的旧测试调用已修正。首个有效 focused run 的五项失败中，两项是将 Common BGA 部件误写入 Bms section，一项授权了示例未请求的权限，一项期待冻结场景即时显示 PAUSED，另一项暴露了零窗时 HostedSlots 仍索取几何的问题。修正示例/夹具与真实零窗准备逻辑，并增加同基线正向例子，避免负例因同一无效声明而假通过。

首次 full BMS 为 2,451 通过、32 失败、16 跳过，见 `bms-full-r1.trx`。比既有基线多出的三项均为 `TestInvalidPublicDeclarationLogsOneRedactedDiagnosticFromExactCurrentRevision`：最初把全部 layout diagnostics 并入持久材料日志，导致临时 `environment-window-fallback` 多算成作者错误。收窄为 `bms.layout.bga-viewports` 前缀后，保留全部 BGA 作者错误而不扩大原环境诊断范围；三种来源及 BGA 无窗提示专项通过，重新编译后再跑完整 BMS，恢复为仅 29 项旧失败。没有修改这些既有测试的期望值。

最终 BMS 的 29 项全部与上一轮 `artifacts/gameplay-performance-20260930/bms-full-final.trx` 身份、消息和规范化业务堆栈一致：28 项仍依赖已移除的 Workspace getter，1 项仍要求已恢复的编辑按钮禁用。对照仅规范化新增 partial fixture 导致的 DisplayClass 数字、去除两个 RunTestBlocking 转抛帧，保留业务行号与 lambda 编号；不以失败数量代替归因。跳过项未计为通过。

最终 mania 的 5 项同样与上一轮 `artifacts/gameplay-performance-20260930/mania-full-final.trx` 身份、消息和规范化业务堆栈一致：`TestSceneAutoGeneration` 的四项长条回放帧数断言，以及 `TestRegisterExternalDirectoryWithOnlyNonManiaBeatmapsReturnsNull` 的旧返回空值预期（当前按合同抛出 `InvalidDataException`）。本轮没有新增失败，也没有修改这些旧断言。

桌面验证复用现有非系统盘 portable 探针，完整运行 `TestSceneBmsBgaSharedContent`。真实图片和嵌入视频、皮肤显示替换后的 POOR、单播放器身份、视频位置、暂停/回退/退出，以及 Fit 的留边、Fill 裁切上下边、Stretch 保留四边均由实际像素/几何断言检查，截图另经目视核对。该次使用 `release-r7` 渲染代码，之后唯一生产修改是上文的持久日志过滤；没有改变任何成像逻辑，未重复无关图像验收。探针源码及退出结果保存在证据目录，不以 headless 的跳过截图路径冒充桌面证据。

收尾将本轮三个作者验证输入目录从 `.dev-cache/temp/` 移到证据目录的 `reference-study-source`、`bga-negative-source` 和 `first-scene-negative-source`，保留可复查输入；没有新建工作副本。依赖缓存继续复用。既有 `.dev-cache/temp/bga-render-host` 探针保留，此前清理曾被工具策略拦截，本轮复用后没有重试删除，也不将其报告为已清理。

## 保留边界

真实歌曲中的叠层/ARGB/老视频保真、不同设备和 DPI、长时解码与最终发行组合仍需人工验收。本次合成图像和自动测试不代签这些门；完整 BMS/mania 回归仍保留上述逐项核实的既有失败，不能将其称为全绿。
