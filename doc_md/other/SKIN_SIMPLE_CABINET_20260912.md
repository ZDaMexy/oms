# 静线机台结构打磨（2026-09-12）

## 方向与证据

用户图07再次否定静线整体质量，随后明确目标是高质量、合格的 beatmania style，LITONE 是参考，不是必须照抄的命令。complex 已放弃；本轮不恢复双内置。

- [图07原始反馈](references/simple-1p-20260912/07-oms-simple-quality-feedback.png)已原样保存，SHA256=`fdf241e367f2a7292e2bcb07be25ea4db9ceda17e44cae13967f3ea57b6166b8`，与临时附件逐字节摘要相同。
- 对照原有 LR2、LITONE12、IIDX32 图像及 [LITONE 作者页面](https://desout2.com/litone12-beatoraja/)、[KONAMI 成绩图说明](https://p.eagate.573.jp/game/2dx/26/howto/epass/score_graph.html)。学习功能分区和信息层级；未复制外部素材、未增加假成绩图或未实现的玩法信息。
- [图08隔离运行截图](references/simple-1p-20260912/08-simple-isolated-runtime.png)由实际 desktop framebuffer 原样保存，包含测试浏览器边栏。真实 PlayerLoader、ReplayPlayer、canonical simple、BMS renderer 与合成7K谱参与；没有用户歌曲/BGA媒体，不代替实际歌曲与设备签收。

## 本轮变化

原不透明 `bms/stage.png` 仅覆盖轨道 group，不能遮住键区和 BGA 外侧的歌曲背景。公共 `stage.background` 增加独立 Global 声明，使用 SafeBounds；保留 Stage 原范围。二者在 Background 层分别 depth2/1，BGA和物件在前方。缺省继承、Forbidden Suppress、canonical权限和读取链不变。静线 BMS 引用新原创 `scene/cabinet.png`；mania Global 提供透明 `mania/stage`。

新增 BGA 内缘金属框，中心透明，继续由每个真实 viewport 的 native clip 管理。BMS 白键/短黑键有完整插槽与边缘厚度；连续血条增加边缘层次；作者启用按键灯与命中光。SCORE、ACCURACY、BPM 分别置于文件纹理仪表中，文字放大，缩窄窗口时仍保持标签和值不重叠。没有改变下落时间、输入、判定、BGA内容播放或各键数求解。

金属底板由内置 imagegen 生成，1536×1024 原样复制，配方不覆盖。完整提示词、原图身份和其它素材来源见[美术记录](../../skin-authoring/docs/SIMPLE_ARTWORK.md)。精确边框、键面、仪表由离线作者配方生成 PNG；运行时不读取配方，不增加私有主题 provider。开发启动与打包继续从作者源构建 canonical 包。

## 已运行与纠错

1. 作者工具 Release 编译成功；最终字号修改后的真实作者检查、正常制作、重复生成/打包、错误拒绝和中断成品保护再次通过（`artifacts/simple-cabinet-authoring-final.log`）。作者分发包SHA256=`01ea599bacfb2b42929f3be9290e534585adc90e1cf935e26740f00a282d55a7`；构建器可有不同压缩字节，始终与各自嵌入摘要成对。
2. Shared focused 158/158 通过（`simple-cabinet-core-focused-final.trx`）。首次新增测试误用未加载容器的 Parent，改为验证层 Children 及精确范围/深度；同时同步公有目录生成块。三项首败已明确修复，不计作旧基线。
3. BMS/mania实际加载、按键和信息 focused 26/26 通过（`simple-cabinet-bms-focused-final.trx`）。首轮发现小窗口数字越框，修改字号和位置；新独立仪表的测试改为逐字段核对应边框，仍核标签不重叠、数值真实更新和playfield不相交。
4. 此前退役 complex 后普通历史导入测试仍错误读取 `Skins/Canonical/oms-complex.osk`。修为测试工程 `SkinAuthoringSamples` 归档输入，保持普通导入覆盖；游戏发行仍只有 simple。首败日志保留。
5. 截图 fixture 首编缺 Mod 扩展命名空间，修复后实际执行成功，退出0。命令：`osu.Game.Rulesets.Bms.Tests/bin/Release/net8.0/osu.Game.Rulesets.Bms.Tests.exe --exact-test osu.Game.Rulesets.Bms.Tests.Skinning.TestSceneBmsSimpleGameplayCapture`；`OMS_SIMPLE_CAPTURE_PATH`指定隔离截图输出。headless明确跳过像素证据。
6. BMS Release full：2216 Passed / 7 Failed / 16 Skipped，12分43秒。五个失败为 `TestEveryPublicSlotExpandsToExactApplicableBmsTargets` 的旧总数，另两个为 `TestNoPublicDeclarationRetainsActualLegacyCustomFallbackInstances` / `TestSelectedPublicSlotsGateActualLegacyCustomFallbackInstances` 的旧 canonical 灯效 Suppress 预期。同步测试合同后七项复验7/7通过；14K额外拆分验证两个Stage背景与一个Global背景。只修改测试预期，未再修改生产代码或作者包；没有宣称随后又完整重跑并得到0失败。跳过项为15个缺核验备份根检查和1个headless像素捕获（后者已单独desktop成功）。日志和TRX为 `simple-cabinet-bms-full`、`simple-cabinet-bms-recheck-final`。
7. core Skin Release full：1347 Passed / 5 Failed；mania Release full：863 Passed / 4 Failed。`Compare-FailureBaseline.ps1 -ResolvedCoreSampleFixture`逐项核失败名称、类别及完整消息完全一致，并确认已关闭sample用例本次Passed，证据`artifacts/simple-cabinet-baseline.json`。日志和TRX分别`simple-cabinet-core-full`、`simple-cabinet-mania-full`。完整命令为 `dotnet test <项目.csproj> -c Release --no-restore`，core另加 `--filter FullyQualifiedName~Skin`；BMS full使用对应源码已编译产物的`--no-build`。
8. `dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m`成功，0警告/0错误（`artifacts/simple-cabinet-release.log`）；BMS测试工程编译仍报告既有CS8600、CA2007两条警告，未隐藏。未重新publish发行ZIP、未重跑旧安装覆盖流程：构建/安装脚本无修改，当前真实desktop证据已使用新包。文档检查与diff检查同次执行。

## 当前视觉边界

图08已证明歌曲背景穿透被消除，机台底板、控制区和仪表使用同一材质基底；判定文字与转盘视觉分量仍弱，连续血条仍有后续改进空间。素材预览不是游戏截图，自动通过也不表示高质量目标已签收。

当前 scene Stage/Group/Lane 需要精确稳定身份，不能把 BMS deck-2 或 BMS 专属节点无条件放进共用 mania/单舞台场景。variant不条件实例化，隐藏节点仍须解析；本轮未为分段血条或判定字体扩张条件实例 ABI，也未借程序化主题掩盖该限制。后续改动应从实际画面和可复现作者需求推进。

## 按键区与血槽继续打磨

用户进一步要求细看参考图的按键区和gauge。图04/05的共同结构是接近控制区全高的转盘、长白键与短黑键所在的连续底座、始终保留全长的暗血槽。图08仍是偏小转盘和缩短的连续条，因此继续修改实际消费者与普通文件。

- 新公开`ScratchKeyWidth`默认1、范围1..4；仅在独立键区有效，静线设2。solver向普通键外侧求解每lane `KeyVisualRect`，由安全区及同高度BGA边界限制；native及公开specialised key场景消费同一矩形。音符、输入lane、判定线和BGA几何不随此倍率变化。缺声明或非法值保留旧宽，不继承canonical的2。
- 键面文件重绘为约80%区域高的白键和60%的短黑键，增加统一金属上下轨、凹槽、下方灯座与底座。既有原创转盘原图未修改；纯纹理真实按压alpha合同不变。
- `hud.gauge`纯纹理不再缩放整个图：固定全宽25%亮度暗槽，原色图保持满宽并通过mask裁切揭露真实血量；零值保留空槽。静线使用完整50格青色PNG，不增加公共槽位、条件实例或假过关阈值；作者scene保持原绑定表现。
- [图09实际截图](references/simple-1p-20260912/09-simple-controls-runtime.png)通过原desktop fixture保存，退出0。与图08对照可见转盘直径约翻倍、白黑键下方形成完整底座，低血量时完整暗槽仍清楚。截图含测试边栏、合成7K谱且无BGA媒体，未代签完整歌曲/控制器体验。

### 继续打磨的验证

Shared gauge/runtime focused 53/53；BMS布局、配置解析与真实皮肤按键/素材focused 58/58。首次shared fixture编译错误为SRGBColour通道访问，改用Linear.R；首次BMS解析测试因新增合法字段仍断言旧数量3而失败，改为4并保留未知字段拒绝断言，复验通过。日志前缀`simple-controls-core-focused`、`simple-controls-bms-focused`，`-final`为修正后结果。

BMS Release full首次为2224 Passed / 8 Failed / 16 Skipped（13分27秒，`simple-controls-bms-full.trx`）。其中四项在旧helper对血条`Sprite.Single()`的假设处失败；修正为血条严格两层、其它槽一层，并逐图断言同一已准备素材。另四项为一次canonical安装准入错误及随后诊断/切换检查失败，未把它们当作既有基线，也未据相邻顺序断言污染。相关9项独立复验全部通过（`simple-controls-bms-recheck.trx`），安装故障未复现；临时runtime日志已自动清理，原console/TRX保留，不能反推已查明安装失败原因。为排除单独运行的局限，追加整个皮肤选择fixture的组合复验。

2026-09-13整组`BmsManagedFolderSelectionProductTest`复验684 Passed / 0 Failed / 15 Skipped，8分12秒（`simple-controls-bms-selection-full.trx`）；安装准入、诊断和切换问题未再出现。未把这次整组复验写成又跑了一遍BMS全部测试。15项跳过均为未提供已核验备份根；真实截图另外执行，未以headless补签。

Shared Skin Release full 1349 Passed / 5 Failed（2分54秒，`simple-controls-core-full.trx`）；mania Release full 863 Passed / 4 Failed（2分38秒，`simple-controls-mania-full.trx`）。`Compare-FailureBaseline.ps1 -ResolvedCoreSampleFixture`逐项核对名称、类别及完整消息一致，已关闭sample项本次Passed，输出`artifacts/simple-controls-baseline.json`。命令使用`dotnet test <项目> -c Release --no-restore`；core full另用`--filter FullyQualifiedName~Skin`，core/BMS完整与整组复验使用当前配置已编译产物的`--no-build`。

作者工具Release编译、`Test-Authoring.ps1 -Tool .../tools/SkinAuthoring/bin/Release/net8.0/SkinAuthoring.exe`正常制作、重复生成/打包与错误/中断保护通过（`simple-controls-authoring.log`）；`dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m`通过，0警告/0错误（`simple-controls-release.log`）。BMS测试编译仍有既有CS8600/CA2007，未隐藏。未重跑发行ZIP安装矩阵，构建/安装脚本未变；正常构建已更新canonical。文档健康检查与`git diff --check`同次执行。

作者包SHA256=`ed5ede384469f061481052f858f1199bf4638a145c38f5add60be7a3fe72d641`。源码与生成PNG/INI/分发包同步，正常开发/build/publish继续自动构建只读canonical。

整体质量仍未签收。血槽标签与独立实时百分比、判定文字、控制台更完整的外围结构仍有提升空间；本次未为没有落位的数值改动绑定格式。
