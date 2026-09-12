# 静线演奏布局调整与验证（2026-09-12）

本记录对应用户重新授权的 simple 打磨与 BGA 皮肤布局要求。起点为当前 `master` 的 `7c4772593e7aedee303c52630ad2ce3087a37607`；工作开始已 fetch，当时相对记录的远端 ahead 1、behind 0。没有新建分支、修改用户安装或打开原游戏保存根。五张用户原始 PNG 已逐文件核对复制前后 SHA256，保存在[参考凭据](references/simple-1p-20260912/README.md)。

## 实际改动

静线 BMS 落键区高度声明为 `0.70`，7K 宽度声明为 `0.25`，实际区域仍受宽高比、DPI 和安全区约束；独立按键区 `KeyAreaHeight=0.12`，血条紧随其后。按键保留实际输入反馈，圆形转盘纹理等比居中；键图与判定位置分开，不通过扩大判定目标实现控制台。连击移至判定附近，额外重复连击从信息栏移除。公开 `hud.text` 负责必要的分数、准确率、BPM 和进度：BMS 在底部，mania 仍在顶部。

BGA 尺寸与纵向位置现由 `[Bms]` 的公开 `BgaWidth/BgaHeight/BgaVerticalPosition` 决定；静线分别为 `0.60/0.76/0.45`。尺寸是安全区相对最大框，solver 在可用空间内等比放入 `4:3` viewport，保留 P2 镜像、14K 多窗及窄屏底部退让。scene 和既有播放器消费同一最终区域；未修改 BGA 内容、时序或解码器归属。精确范围与适配规则见[作者参考](../../skin-authoring/docs/REFERENCE.md)。

四个新增字段只读取当前选定 package revision 的已接受声明，缺项用旧默认，避免第三方包通过聚合来源意外继承静线的大 BGA 和独立键区。未声明独立键区时，键图仍在原判定目标内并随真实 Lift 移动。**公共底部 HUD 会影响其它皮肤的可用高度**：旧默认 `0.92` 落键区约缩为 `0.878`，不能称全部旧布局完全不变。星轨和 Aurora 作者文件、成品未改；它们受影响的公共布局也纳入回归。

作者工具新增 `compactLayout` 生成选项，只有静线明确启用；游戏不读取该选项或按作品名识别布局。旧作品未声明时保持原生成配方。素材使用已有代码绘制方式生成，不复用外部截图中的装饰。静线成品 SHA256 为 `9cb8a025975f5537c54dbcfb6d0a4c248f923e623dee88993ef74b8a34c08996`。素材展示图标明并非游戏截图，不能替代实机对照。

## 自动验证

build/test 由主执行者统一调度；源码在对应检查期间停止写入，最终格式检查使用只读 verify 模式。日志与 TRX 留存于本地忽略目录 `artifacts/skin-simple-layout-20260912/`。以下为本次真实运行，不沿用旧 C7 发行物的通过结论。

| 检查 | 本次实际结果 |
| --- | --- |
| 聚焦成品信息、按键反馈、布局及 fallback | `simple-focused-verified.trx`：56 Passed；真实输入、Lift、窄窗口文字与 BGA/键区兼容 |
| mania 成品键数、单/双舞台和窗口矩阵 | `simple-mania-host-matrix.trx`：240 Passed |
| 默认配置与布局修复定点 | `simple-default-layout-recheck.trx`：11 Passed，保留配置忽略断言并检查血条不进入底部信息区 |
| 最终包摘要同步后的定点 | `simple-final-package-focused.trx`：67 Passed，重新编译后核实际来源加载、输入反馈和布局 |
| 格式收尾后的定点 | `simple-final-format-focused.trx`：67 Passed；仅将新增常量符号改为规则要求的 `KEY_AREA`，公开字符串和全部布局值不变，重新编译后复验 |
| LF 规范化后的最终包 | `simple-lf-package-focused.trx`：67 Passed；重新编译后再次核加载、输入和两种玩法的信息区 |
| BMS 完整运行 | `simple-bms-complete.trx`：2213 Passed / 0 Failed / 15 Skipped，完整运行成功 |
| mania 完整运行 | `simple-mania-full.trx`：863 Passed / 4 Failed / 0 Skipped |
| core Skin | `simple-core-skin.trx`：1339 Passed / 5 Failed / 0 Skipped |
| 冻结失败精确比较 | `failure-comparison.json`：名称、分类与完整消息逐项一致；原 sample 夹具对应项仍 Passed |
| 作者完整制作 | [authoring-tool-verification.json](../../skin-authoring/docs/authoring-tool-verification.json)：三作品检查、重复打包、重新生成、错误拒绝和中断成品保护全部通过 |
| Release | `dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 成功，0 warnings / 0 errors；最终桌面与测试目录的包均与源包及摘要一致 |
| 格式、文档与 diff | whitespace 与作者工具 style 检查通过；游戏 style 检查仍报告 10 项 HEAD 已有的常量命名诊断，详见下文；文档与 diff 检查通过 |

mania 的四项旧失败为 `TestHoldNoteChord`、`TestHoldNoteStair`、`TestHoldNoteWithReleasePress`、`TestSingleHoldNote`，均为原自动演奏帧数断言。core 五项为 `TestBackgroundCyclingOnDefaultSkin(True)` 的旧背景超时，以及 `TestRetrieveAndLegacyExportJapaneseFilename`、`TestRetrieveAndNonLegacyExportJapaneseFilename`、`TestRetrievalWithConflictingFilenames`、`TestRetrieveOggAudio` 的旧无有效 beatmap 输入。比较使用随仓库固定的完整消息，不按数量认定一致，不删除测试或改写这些预期。

mania/core 运行后，作者工具修复使包内 `author.json` 与 `README.md` 更新；与此前已验证包逐项比较，两版均为 108 条目，只有这两项改变，全部运行时声明和素材字节一致，记录为 `final-package-metadata-diff.json`。因此没有再次重复 mania/core 全套；最新包仍重新执行 BMS 的实际来源、两玩法成品与完整检查。

完整检查之后，提交前按 `.gitattributes` 将 `author.json` 的 CRLF 统一为 LF 并重打包；`final-line-ending-diff.json` 证明只有该作者配置文件改变，全部运行时条目不变。作者验证脚本将 `author.json` 纳入原有 LF 检查，避免同类问题漏过重复打包验证。最终源码和成品使用规范化后的摘要，重新编译并执行最终包定点检查、作者制作与 Release；未因纯作者元数据换行再次重复完整套件。

编译 BMS 测试项目时仍报告 `TestSceneFilesystemBackedStoryboardFallback.cs` 的 CS8600 与 `BmsRulesetStatisticsTest.cs` 的 CA2007；增量 Release 命令的零警告不能扩大为所有配置无警告。未运行 core 全套、FileStore 全套和新的安装/覆盖发布流程：没有改变对应存储、导入权限或发行逻辑；core Skin 与 BMS 实际来源路径按受影响面覆盖。实机观感、设备性能、长时间游玩及真实保存根门没有在本次重新签收。

附加的游戏 `dotnet format style --verify-no-changes` 返回 2：仅有 `BmsGameplayLayoutSurfaceIds` 的十个旧 PascalCase 常量产生 IDE1006；与 HEAD 的声明名称逐项对应，记录为 `style-declaration-review.json`。新增符号已按规则改名，没有扩大修改旧公开名称或压制诊断。whitespace 和作者工具 style 均返回 0；不能将这次游戏 style 检查写成完全通过。

主要复验命令如下（均在仓库根目录）：

```powershell
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=simple-bms-complete.trx' --logger 'console;verbosity=normal'
dotnet test osu.Game.Rulesets.Mania.Tests/osu.Game.Rulesets.Mania.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=simple-mania-full.trx'
dotnet test osu.Game.Tests/osu.Game.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~Skin' --logger 'trx;LogFileName=simple-core-skin.trx' --logger 'console;verbosity=normal'
powershell.exe -NoProfile -ExecutionPolicy Bypass -File skin-c7-acceptance/Compare-FailureBaseline.ps1 -ActualCoreTrx osu.Game.Tests/TestResults/simple-core-skin.trx -ActualManiaTrx osu.Game.Rulesets.Mania.Tests/TestResults/simple-mania-full.trx -ResolvedCoreSampleFixture -OutputFile artifacts/skin-simple-layout-20260912/failure-comparison.json
powershell.exe -NoProfile -ExecutionPolicy Bypass -File skin-authoring/Test-Authoring.ps1 -Tool tools/SkinAuthoring/bin/Release/net8.0/SkinAuthoring.exe
```

## 发现的问题与复验边界

- 早期信息栏嵌套 slot owner 与分派容器使用可见 blend 被 preparation 拒绝；修正作者声明，没有放宽场景合同。窄窗口文字与重复连击检查随后修正。
- 真实输入检查发现旧键图必须继续挂在判定目标内才能跟随 Lift；独立键区才挂到谱面裁剪区外。另发现聚合来源会使旧包继承静线新增几何，改为当前精确选定来源的声明。
- 一次完整 BMS 运行因旧 mania 检查只查看 HUD 首根而中止，不计通过。检查改为遍历同一 slot 的实际路由节点，保留必要文字、native/residual 隐藏和所有权检查；其后成品矩阵 240 项通过。
- `simple-bms-full-verified.trx` 完整结束为 2212 Passed / 1 Failed / 15 Skipped；唯一失败仍要求实际车道高度 `0.92`，与已明确的底部留白冲突。仅更新该测试预期并补血条避让断言，定点 11 项通过；最终完整结果以上表为准。15 项跳过均要求额外提供已核验的用户备份根，本次没有提供，不能计为通过。
- 作者完整制作首轮发现 `Complex=false` 会把 Aurora 演练重生成为新布局。新增明确生成选项并恢复旧配方的精确字节后，重新绘制和重复打包全部通过；首败日志保留。
- 最后一次重新打包后，执行者漏同步安装原件的 `oms-simple.sha256`，新包与嵌入摘要失配；`.codex-simple-bms-full-final.log` 对应运行被主动中止，未产出完整 TRX，不能计通过。同步摘要并重新编译后重新验证，未修改或绕过 canonical 完整性保护。此失误未涉及用户安装或保存根。

后续需用支持新增公开字段的当前客户端实际游玩，对照[原图](references/simple-1p-20260912/README.md)继续调整比例、文字及判定观感；只导入新 `.osk` 到旧发行客户端不能验证新布局能力。当前没有重新制作安装包，也没有代填 V-001～V-005 或 C7 观感签收。当前任务状态以 [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md) 为准。

## 二次素材打磨与实机反馈

用户提供[首轮布局实机反馈图 06](references/simple-1p-20260912/06-oms-simple-first-layout.png)，明确指出轨道分隔线仍不正确。下述为首轮与双内置接入之后的素材调整，前文测试结果及成品身份不自动覆盖本轮。

- [普通分隔图](../../skin-authoring/sources/oms-simple/bms/divider.png)在 256 像素画布中改为 10 像素中性灰；[转盘分隔图](../../skin-authoring/sources/oms-simple/bms/divider-scratch.png)采用 7 像素，补偿转盘约 1.5 倍车道宽。原 3 像素着色区域被整车道缩放后不足一屏幕像素，是细暗表现的来源；不是默认分隔组件重复叠加。
- BMS 改用 `lane-white/accent/scratch.png` 三种轨道底色；[stage.png](../../skin-authoring/sources/oms-simple/bms/stage.png)采用不透明暗底，[target.png](../../skin-authoring/sources/oms-simple/bms/target.png)提供红色判定线。[scratch-platter.png](../../skin-authoring/sources/oms-simple/bms/scratch-platter.png)为 ImageGen 原创转盘文件，按既有等比显示路径保持正圆，外部参考截图未作为发行装饰素材复用。
- [作者预览板](../../skin-authoring/dist/oms-simple-preview.png)展示对应轨宽、底色、分隔图和约 90 像素高的独立键区，转盘为正方尺寸居中。保留 `NOT A GAMEPLAY CAPTURE` 标记；它是素材对照板，不证明真实播放器布局或实机观感通过。
- 固定分段血条 scene 试验引用了当前拓扑不存在的 `bms.group.deck-2`，且不能跨玩法忽略不适用的舞台，实际 preparation 返回 019。试验已完全撤回，当前仍为连续 `gauge.png` 填充，作者板仅配暗底轨；没有为此扩展公共 scene API 或放宽未知目标拒绝。

本轮未修改运行时生产 C#，mania 与 complex 作者作品未改。本轮最终 BMS Release full 为 **2219 Passed / 0 Failed / 15 Skipped**，15 项仍要求核验备份根；此前素材与两玩法场景 focused **263/263** 通过。作者完整制作、重复生成与重复打包检查通过，Release 构建 0 警告 / 0 错误，输出 simple 包与源包摘要一致；文档和 diff 检查通过。未签收整体视觉。

本轮从干净的 a548332 开始，fetch 成功，HEAD 相对跟踪分支为 ahead 3 / behind 0。最终 simple 包 SHA-256 为 162c90ab5e3925f627306aa569ff3c66e906137ed57abce7012c1b4a41b20a5f，complex 与 Aurora 原包未变；转盘源 PNG 为 1254×1254，角落 alpha=0，保持生成图原始字节。完整生成提示词见[素材说明](../../skin-authoring/docs/SIMPLE_ARTWORK.md)。

首轮分段血条尝试先被 stable ID 语法检查拒绝（008）；修正命名后实际跨玩法 prepare 仍拒绝未知舞台（019），该候选 focused 133 Passed / 130 Failed。撤回整个 scene/manifest 改动后，重新打包并复验 263/263，再对最终包运行上述完整套件；没有修改未知目标拒绝或放宽作者权限。原失败和成功 TRX/log 分开保存在本地忽略目录 artifacts/simple-polish-20260912。

本轮命令：

~~~powershell
dotnet build tools/SkinAuthoring/SkinAuthoring.csproj -c Release --no-restore
tools/SkinAuthoring/bin/Release/net8.0/SkinAuthoring.exe generate skin-authoring/sources/oms-simple
tools/SkinAuthoring/bin/Release/net8.0/SkinAuthoring.exe pack skin-authoring/sources/oms-simple skin-authoring/dist/oms-simple.osk
powershell.exe -NoProfile -ExecutionPolicy Bypass -File skin-authoring/Test-Authoring.ps1 -Tool tools/SkinAuthoring/bin/Release/net8.0/SkinAuthoring.exe
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~TestSimpleLaneContrastAndDividersUseTheActualAuthoredImages|FullyQualifiedName~TestAuthoredInformationUsesSafeLayoutAndGameplayValues|FullyQualifiedName~TestCanonicalProductsManiaAllStageSizesAndScreens'
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj -c Release --no-restore --logger 'trx;LogFileName=simple-polish-full.trx'
dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m
~~~
