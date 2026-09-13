# 静线判定与血槽细节迭代（2026-09-13）

## 输入与范围

从干净master `315baf3`继续，已fetch且origin无新增提交。用户图10原样保存在[参考目录](references/simple-1p-20260912/README.md)，用户认可明显提升并要求自行反复对照。观察依据是保存的LITONE/IIDX图片：当前音符偏厚、判定与连击过于分散，血槽无读数且亮框只跟随已填充部分。没有照搬外部图像或将其打包发行。

补查[beatoraja槽类型源码](https://github.com/exch-bms2/beatoraja/blob/master/src/bms/player/beatoraja/play/GaugeProperty.java)，其中不同槽类型分别定义初始值、边界与增减规则。本轮据此保持血槽读数与真实状态绑定，不将参考图静态红色分区硬编码为通用过关线；外部资料仅辅助判断，不改变OMS判定或槽规则。

## 作者结果与公共合同

- BMS普通音符及长条头尾收薄高光轮廓，保持24像素画布和末端不透明行21，落键边缘不移动。
- 同包Stage模板显示大写判定和相邻连击。血槽分为固定金属外框、GAUGE标签、真实百分比、固定暗格与裁切亮格。
- `GaugeHeight`为选定包公开BMS设置，默认0.036、合法0.02..0.12；静线使用0.07，与键盘、BGA和信息区由同一布局共同避让。
- `instances.material`根据已解析的Stage Provide素材挂载模板，严格匹配选定包同一内容版本及资源名；模板根slot一致，实际目标覆盖整棵子树，状态和绑定按各舞台事实运行。无匹配时不挂载，不继承其它包的scene/resources，不提供任意条件语言。
- `clip.reveal-x`裁切固定宽度内容；`text.format`仅提供静态percent/uppercase。百分比适用于数值文字路径，accuracy/progress保留既有专用格式。展开数量、事件应用和格式化后文字均受现有预算限制。

源PNG与JSON由普通离线作者配方生成；既有原创底板/转盘不变。静线仍是唯一内置，开发启动/build/publish沿原自动源同步路径更新。

## 检查与纠错

首次作者check报`OMS-SKIN-SCENE-005`：新增实例字段误把target/material都列为必填；修正为二选一后check通过。首次实际desktop捕获触发现有outline/shadow容器未启用Masking的绘制异常，已修复两种特效的公共容器。outline是矩形边框而非字形描边，静线文字已撤掉该误用，保留准确的文字排版。

第二次desktop捕获暴露子元素仍按整条Stage定位，血槽遮住落键区；现将material模板无slot子节点限定为根owner的表面矩形，显式target模板保留原坐标合同。clip新增外层变换后，子节点须挂到内部裁切容器，否则只改空mask宽度不能裁切图片；已修复并在实际内置双玩法产品测试中约束子矩形与裁切挂载关系。该首错截图不作为最终效果证据。

本轮实现、回归、作者制作与Release验证完成。图10属于用户实机；图11隔离desktop合成谱截图只证明该场景实际绘制，不替代完整歌曲、其它窗口及整体视觉签收。仍能明确观察到控制台外框衔接、底部信息区留白和判定字形完成度可继续打磨，保留下一轮实际反馈。

已完成：core focused90 Passed/0 Failed；BMS布局/公开配置/真实内置双玩法focused143 Passed/0 Failed，补充子矩形与mask挂载真实产品路径1/1。BMS Release full2242 Passed/0 Failed/16 Skipped（11m19s）；15项缺核验备份根而跳过，1项headless截图主动跳过，实际desktop另已exit0。BMS.Tests仍有既有`TestSceneFilesystemBackedStoryboardFallback.cs:151` CS8600与`BmsRulesetStatisticsTest.cs:555` CA2007两项编译警告。

core Skin full1359 Passed/5 Failed；mania full863 Passed/4 Failed。`Compare-FailureBaseline.ps1 -ResolvedCoreSampleFixture`对失败名称、类别及完整消息逐项比较通过，输出`artifacts/simple-detail-baseline.json`。核心focused补测时曾因编辑编码误将é改为茅，破坏原规范化路径冲突fixture；恢复原字面量后90项通过，没有放宽生产路径校验。混合实例、双Stage运行/晚挂载/重建、边框Masking与原显式实例均有本轮覆盖。

作者完整制作、重复打包、错误拒绝与中断成品保护通过，记录`skin-authoring/docs/authoring-tool-verification.json`。最终静线包SHA256为`789f08257444635fc2098bac17264e9575c5cc8d6830959055ecfe3554c7b9cb`。Release构建0警告/0错误；正常build再次执行内置源同步。未推送远端，未将整体视觉门标记完成。

本轮命令（均Release，串行构建/检查；普通输出保存在`artifacts/simple-detail-*.log`）：

```powershell
dotnet build tools/SkinAuthoring/SkinAuthoring.csproj -c Release --no-restore
tools/SkinAuthoring/bin/Release/net8.0/SkinAuthoring.exe generate skin-authoring/sources/oms-simple
tools/SkinAuthoring/bin/Release/net8.0/SkinAuthoring.exe check skin-authoring/sources/oms-simple
dotnet test osu.Game.Tests/osu.Game.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~GameplaySkinSceneCodecTest|FullyQualifiedName~GameplaySkinSceneRuntimeHostTest|FullyQualifiedName~TestSceneGameplaySkinGauge'
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~TestBuiltInSimplePlaysBmsAndManiaWithoutImport|FullyQualifiedName~BmsGameplayLayoutSolverTest|FullyQualifiedName~BmsLegacySkinTest'
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj -c Release --no-restore
dotnet test osu.Game.Tests/osu.Game.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~Skin'
dotnet test osu.Game.Rulesets.Mania.Tests/osu.Game.Rulesets.Mania.Tests.csproj -c Release --no-restore
powershell.exe -NoProfile -ExecutionPolicy Bypass -File skin-authoring/Build-Skins.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File skin-authoring/Test-Authoring.ps1 -Tool tools/SkinAuthoring/bin/Release/net8.0/SkinAuthoring.exe
dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m
```

实际像素使用`osu.Game.Rulesets.Bms.Tests.exe --exact-test osu.Game.Rulesets.Bms.Tests.Skinning.TestSceneBmsSimpleGameplayCapture`，输出由`OMS_SIMPLE_CAPTURE_PATH`指定，最终exit0。图11保存原始framebuffer，SHA与边界见[图片索引](references/simple-1p-20260912/README.md)。该headless测试主动Skip，不能用跳过结果声称已截图。

## 图12之后的控制区外壳迭代

本轮从干净`e01781c`继续，fetch后origin无变化。图12用户实机继续认可改善，但要求反复对照；保存原图并核对SHA。对照图04/05后选择三项明确差距：键面/凹槽层次仍平，血槽与仪表像普通矩形卡片，大面积拉丝底板亮度抢眼。

白键增加底部斜面，黑键增加上方高光和凹入支座，保留原图尺寸和连续上下轨。血槽及仪表采用同一切角金属外壳配方，读数区与标签区分开，血槽字高调整为12/22。普通scene使用Global `stage.background`已准备的本玩法纹理，乘以`#adb6c0ff`压低底板亮度；BMS原材质文件不改，mania透明图继续透明。没有按1P截图硬编码跨左右布局的控制台底座，也没有修改游戏运行时、公共合同、音轨或输入几何。

首版相关产品检查113 Passed/0 Failed；增加背景压暗后，以完整作者包在BMS全部模式/样式/窗口、mania单/双舞台、真实按键反馈及信息布局路径重新检查，403 Passed/0 Failed/0 Skipped。它属于本轮素材/作者声明验证，不将上一轮full计数重新写成本轮执行。实际desktop最终首次运行exit1、未生成新PNG且标准输出为空，临时日志已由runner清理，原因无法据此归因；独立复验exit0并生成图13，已逐图观察且保存原始像素。

作者正常制作、重复打包、错误拒绝与中断保护通过；最终包SHA256 `e8e3b1259ba9d400a47bc10a39e61d98c7b5371b21d8564b211e605c10eea9da`。Release构建0警告/0错误，内置包自动源同步保持。整体视觉未代签，图13仍是无BGA媒体、带测试边栏的合成7K证据。所有命令串行执行，最终验证日志前缀`artifacts/simple-shell-`。

```powershell
dotnet build tools/SkinAuthoring/SkinAuthoring.csproj -c Release --no-restore
tools/SkinAuthoring/bin/Release/net8.0/SkinAuthoring.exe generate skin-authoring/sources/oms-simple
tools/SkinAuthoring/bin/Release/net8.0/SkinAuthoring.exe check skin-authoring/sources/oms-simple
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~TestCanonicalProducts|FullyQualifiedName~TestAuthoredInformationUsesSafeLayoutAndGameplayValues|FullyQualifiedName~TestBuiltInSimplePlaysBmsAndManiaWithoutImport|FullyQualifiedName~TestSimpleLaneContrast' --logger 'trx;LogFileName=simple-shell-final-product.trx'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File skin-authoring/Build-Skins.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File skin-authoring/Test-Authoring.ps1 -Tool tools/SkinAuthoring/bin/Release/net8.0/SkinAuthoring.exe
dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m
```
