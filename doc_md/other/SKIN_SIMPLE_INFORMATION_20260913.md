# 静线演奏信息区与键区比例（2026-09-13）

## 用户目标与范围

用户明确指出五处缺口：实时判定统计、BGA下方MIN/BPM/MAX、略长的白黑键、Score与HiSpeed、BGA上方曲名及难度。本轮以通用只读皮肤能力和布局区域实现这些信息，由内置simple普通作者文件使用；不新增静线专用运行时视觉接口，也不恢复complex内置。

本轮从干净master 3e34326继续，fetch成功，HEAD相对已更新origin/master为ahead11/behind0。以下记录公共合同、纠错与实际验收，不将合成谱截图作为整体视觉签收。

## 信息来源

| 作者绑定 | 权威来源与语义 |
| --- | --- |
| song.title、song.artist | 通用BeatmapInfo.Metadata显示文字 |
| song.difficulty、song.level | BMS原始DIFFICULTY映射类别与PLAYLEVEL字符串；mania难度名称及空等级，缺项不伪造 |
| timing.bpm-min、timing.bpm-max | BMS转换器生成的音乐时间线范围；mania实际TimingPoints范围 |
| timing.bpm | 既有当前音乐BPM，STOP保持上一真实BPM |
| scroll.speed | BMS当前NHS/FHS/CHS选中设置值；mania自身ScrollSpeed设置值；不代表相同物理单位 |
| judgement.count.perfect/great/good/ok/meh/miss | ScoreProcessor.Statistics对应HitResult六档计数 |
| combo.breaks | ScoreProcessor.Statistics的ComboBreak计数 |
| score.value | 既有通用分数；BMS是EX SCORE |

BMS六档映射为perfect=PG、great=GR、good=GD、meh=BD、miss=PR、ok=空POOR；空POOR与真正ComboBreak分开。mania的ok保持其自身判定档位。皮肤不得根据judgement.hit或偏差自行推算累计计数，长条内部事件与判定展示事件不等于计分器统计。

统计以不可变值结构内联进入Score事件，避免每个edge分配新的引用对象。runtime每帧和epoch屏障从真实计分器读取：Combo变动通知先于BMS断连计数更新，不能只在该通知里采样。撤销由计分器减计数，重试清空，回放跳转从头部统计恢复；皮肤沿已有完整重建屏障同步。

SongInformation仅随完整快照携带，EventStream聚合器在晚订阅和epoch替换中保留同一不可变信息。每项文字限制256个UTF-16单元，截断不分裂代理对。统计文本预留10字形，其他旧动态数字保留384；全局字形预算8192，原像素/字节、单帧创建和实例展开预算保留。`format: fixed-2`固定两位小数，用于速度；原percent、uppercase和准确率/进度专用格式保留。

BMS MIN/MAX只取实际音乐时间线，不读取可能含STOP兼容点的通用控制点，不将未使用BPM定义、SCROLL倍率、TimeRange或绿数计入范围。当前速度绑定只公开数值，不提供模式名称绑定。

## 布局及归属

`BgaInformationHeight`是选定BMS包的公开布局参数，默认0、合法0..0.30，单位为安全区高度比例。正值让唯一布局器在BGA上下保留信息区域并发布`information.song`、`information.judgements`、`information.tempo`、`information.player`。主BGA上方用于曲名，下方分别用于判定与速度，控制区下方用于分数/HiSpeed；BGA尺寸仍归皮肤布局系统共同求解。

场景静态属性`layout-surface`只允许四个封闭区域名，仅适用于hud.text所有者及其无slot子树。孩子默认继承所选区域，显式属性可选择另一信息区；区域缺失必须准备失败。禁止通过绑定、动画、状态机或脚本修改区域，不能借此逃逸exact材质所有者或创建任意布局查询。

材质模板在原Stage匹配基础上支持Global Provide：仍严格匹配选定包同内容版本、slot与资源名，根slot一致；Global只生成一次，Stage按实际舞台生成。静线BMS的整套信息使用Global hud.text，避免14K重复绘制曲名和全局统计。mania通过自身材质选择保留对应信息布局，不挂载BMS专有区域。没有匹配不挂载，不能借用canonical scene或其他包资源。

按键长度由普通作者图与已有独立键区尺寸调整。所有素材、场景与INI继续进入既有开发启动、build、publish的内置源同步流程。

## 资源预算：共享字体图集

本轮新增多个短文本后暴露原准入估算的归属错误：`reserveGlyphPixels()`为每个独立SpriteText至少预留一张2048×2048图集，使64 MP总额度最多容纳16个文本，不论文本只有两个字符还是更长。当前`createText()`仅创建`OsuSpriteText`并指定字号，没有创建私有FontStore或私有图集。

取证先读取本地`osu.Game/osu.Game.csproj`与NuGet包`ppy.osu.Framework/2026.303.0/ppy.osu.framework.nuspec`：项目固定依赖2026.303.0，包记录源码commit为`ba04d538f02cecaabe5ab7dc3b102cd47d59d785`。以下均对照该固定版本官方源码，未引用浮动分支：

- [SpriteText.cs](https://github.com/ppy/osu-framework/blob/ba04d538f02cecaabe5ab7dc3b102cd47d59d785/osu.Framework/Graphics/Sprites/SpriteText.cs)：通过依赖注入取得FontStore，以字体名与字符查找字形，没有按文本节点新建图集。
- [Game.cs](https://github.com/ppy/osu-framework/blob/ba04d538f02cecaabe5ab7dc3b102cd47d59d785/osu.Framework/Game.cs)：创建并缓存游戏范围共享的Fonts，供子树解析。
- [FontStore.cs](https://github.com/ppy/osu-framework/blob/ba04d538f02cecaabe5ab7dc3b102cd47d59d785/osu.Framework/IO/Stores/FontStore.cs)：缓存以`(fontName, character)`为键；没有自带图集的嵌套字体库共享父库Atlas。
- [TextureStore.cs](https://github.com/ppy/osu-framework/blob/ba04d538f02cecaabe5ab7dc3b102cd47d59d785/osu.Framework/Graphics/Textures/TextureStore.cs)：该版本图集边长上限为1024，实际还受渲染器最大纹理尺寸限制。

因此本轮预算修正合同为：每个文本继续按原字形cell面积、padding及四倍余量保守预留，不去重同一字符；先汇总所有文本，再统一按既有2048×2048页尺寸向上对齐一次。2048仍保守大于当前依赖的1024上限，但不再把共享页开销乘以文本节点数量。64 MP / 256 MB上限、总字形数限制及其余资源预算保持不变，不通过抬高到1 GB为新界面放行。

上述数值是场景准入的保守估算，不是实际GPU显存测量；默认字体缓存跨文本共享，渲染字号也不意味着重新生成同尺寸私有字形纹理。本节固定源码取证与下文实际运行验证分别记录。

## 实绘修正与边界

键帽可见高度由白键80%/黑键60%改为70%/52%，独立按键区域、转盘与输入几何不变。信息区首张实绘发现设置按钮压住难度等级，作者布局将曲名与难度分区向内收；判定面板宽度取BGA的44%与安全区14%的较小值，避免宽屏下统计行过度拉开。

窄屏产品检查发现长曲名越出屏幕，新增静态text-overflow=ellipsis，只约束可见字形，不修改绑定原文。初版派生类误用了OsuSpriteText隐藏的Truncate setter，实际加载报RUNTIME-001；按已有TruncatingSpriteText方式显式转换SpriteText设置后，真实加载复验通过。产品等待步骤现在直接检查RuntimeFaults，以免真实创建错误只表现为超时。

[图14实际画面](references/simple-1p-20260912/14-simple-information-runtime.png)由当前Release测试产物的隔离desktop PlayerLoader/ReplayPlayer捕获，最终exit0。逐图确认PG12、EX SCORE24、HiSpeed8.00、MIN/当前/MAX120/138/172、曲名/作者/ANOTHER/LEVEL12显示；难度不被按钮挡住。SHA256为7d53f582b082f4103972f34134176540a44e03f61812702362e944fd7874e54c。

## 验证结果

全部检查使用Release，由root串行构建与测试，相关源码在检查期间停止修改。最终结果：

- core event/scene focused：144 Passed/0 Failed。
- BMS布局、实际信息加载、真实按键与内置双玩法focused：最终80 Passed/0 Failed；此前包含TimingProfile的84项也通过。
- core Skin full：1388 Passed/5 Failed/0 Skipped。
- BMS full：2253 Passed/0 Failed/16 Skipped，用时12m17s，包含修正后的canonical/普通作者包键数、样式、窗口和mania舞台矩阵。15项因缺核验备份根跳过，1项headless截图主动跳过；真实desktop另已exit0，不混算。
- mania full：863 Passed/4 Failed/0 Skipped。
- Compare-FailureBaseline.ps1 -ResolvedCoreSampleFixture：core与mania剩余失败名称、类别和完整消息逐项一致，记录artifacts/simple-information-baseline.json；未只按失败数量判断。
- 作者正常制作、重复打包、错误拒绝和中断成品保护通过，记录skin-authoring/docs/authoring-tool-verification.json。
- 最终Release构建0警告/0错误。测试项目构建曾出现既有TestSceneFilesystemBackedStoryboardFallback.cs:151 CS8600与BmsRulesetStatisticsTest.cs:555 CA2007。

作者成品SHA256为`19126a7aa3d8084e46b74eb294ddcdeff1d246981c03c3f80824b6f08d8b058e`；正常构建生成的内置原件SHA256为`cc797f9607d18da9d9718aab9bf7a8b08bf5bb70407ce93a2cd91b9612a1b59b`。两条既有打包路径的ZIP字节不相同，已逐项解压读取并与作者源核对：两包均118个文件，每个文件路径及内容全部一致，记录artifacts/simple-information-package-content.json。正常开发构建继续自动从源生成内置原件，未把手动导入作为生效前置。

纠错记录：初次生成因模板/实例稳定ID重复被作者检查拒绝，改用不同模板ID后通过；旧逐文本图集预算导致完整信息区准备失败，按共享字体归属修正后通过。一次DisplayName筛选未匹配任何用例，不算验证；改回实际测试名。窄屏长曲名越界、截断setter异常按上文修正。扩大矩阵时，旧测试以Source.Id查询模板实例失败，改用实际InstanceId；未改变生产查询或绕过归属校验。主动中止的失败检查不计有效gate，以上是最终完整运行结果。

图14只证明合成谱的真实绘制，不含实际BGA媒体，也不替代完整歌曲、其它窗口或用户整体视觉签收。原外部参考与此前用户反馈图保留，未将参考作品作为发行素材。未推送远端。

本轮命令（普通日志前缀artifacts/simple-information-）：

```powershell
dotnet build tools/SkinAuthoring/SkinAuthoring.csproj -c Release --no-restore
tools/SkinAuthoring/bin/Release/net8.0/SkinAuthoring.exe generate skin-authoring/sources/oms-simple
tools/SkinAuthoring/bin/Release/net8.0/SkinAuthoring.exe check skin-authoring/sources/oms-simple
dotnet test osu.Game.Tests/osu.Game.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~GameplaySkinSceneCodecTest|FullyQualifiedName~GameplaySkinSceneRuntimeHostTest|FullyQualifiedName~GameplaySkinEventStreamTest|FullyQualifiedName~GameplaySkinEventRuntimeHostTest'
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~TestAuthoredInformationUsesSafeLayoutAndGameplayValues|FullyQualifiedName~BmsGameplayLayoutSolverTest|FullyQualifiedName~TestBuiltInSimplePlaysBmsAndManiaWithoutImport'
dotnet test osu.Game.Tests/osu.Game.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~Skin'
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj -c Release --no-restore
dotnet test osu.Game.Rulesets.Mania.Tests/osu.Game.Rulesets.Mania.Tests.csproj -c Release --no-restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File skin-c7-acceptance/Compare-FailureBaseline.ps1 -ActualCoreTrx osu.Game.Tests/TestResults/simple-information-core-full.trx -ActualManiaTrx osu.Game.Rulesets.Mania.Tests/TestResults/simple-information-mania-full.trx -ResolvedCoreSampleFixture -OutputFile artifacts/simple-information-baseline.json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File skin-authoring/Build-Skins.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File skin-authoring/Test-Authoring.ps1 -Tool tools/SkinAuthoring/bin/Release/net8.0/SkinAuthoring.exe
dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m
```
