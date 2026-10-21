# 重进游玩时的页面交接崩溃（2026-10-21）

## 现场与根因

用户通过当前工作区的VS Code非调试运行反馈崩溃。本次只读提取正常开发启动的五类日志，保全到`artifacts/player-loader-crash-20261021/private/`；原runtime与副本摘要相同，完整摘要留本地证据索引。不打开正式Realm或改写玩家配置，不启动原HDD。

现场UTC 09:08:40首次进入BMS自动演奏，09:08:47按退出返回，09:08:48再次打开同一谱面。09:08:51在`PlayerLoader.pushWhenLoaded → ScreenStack.finishPush → CompositeDrawable.AddInternal`抛出`InvalidOperationException: May not add a drawable to multiple containers.`。这次栈发生在进入页面时，不是音符绘制或判定。日志同时存在视频转码回退和纹理上传排队；它们不能解释这个所有权异常，本次也没有据此宣称修复了这些独立现象。

`prepareNewPlayer()`在发起后台加载前便保存`CurrentPlayer`。后台会先将Player标为`LoadState.Ready`，再完成包裹容器的加载并向Update排队交还回调。旧`readyForPush`只检查Player的Ready与交互条件，500ms等待也不能证明回调已执行；窗口错开时，Player仍在`PendingAsyncDrawableOwnership.LoadWrapper`内，却被推入真实ScreenStack。首次成功不证明再次加载也会遵循相同时序。

固定依赖的实现依据为[ScreenStack.Push/finishPush](https://github.com/ppy/osu-framework/blob/2026.303.0/osu.Framework/Screens/ScreenStack.cs)、[CompositeDrawable加载与AddInternal](https://github.com/ppy/osu-framework/blob/2026.303.0/osu.Framework/Graphics/Containers/CompositeDrawable.cs)及[Drawable.Ready/Parent](https://github.com/ppy/osu-framework/blob/2026.303.0/osu.Framework/Graphics/Drawable.cs)。本轮结论同时由当前源码、正常日志和受控红绿复现确认。

## 修复与回归方法

开局条件增加`pendingPlayerOwnership == null`：只有既有Update回调成功从包裹容器移出Player并交还所有权后才允许排队切页。保留原Ready、玩家交互、暂停和取消条件，不增加固定等待、重试、异常吞并或强行移除未知父容器。正式进行中的游玩没有新增逐帧工作。

`SkinPlayerLoaderHandoffProductTest`在真实Player的父容器赋值失效回调处，用闸门延长“Player已Ready、包裹仍拥有Player”的实际后台窗口。不伪造LoadState、不直接调用私有开局方法。保持超过正常500ms等待后，验证加载页仍为当前页；放行后实际ReplayPlayer以对应玩法的Autoplay加载成功，进入真实ScreenStack，退出后同谱再进。BMS使用内存BMS文本经真实decoder，mania使用既有测试谱。旧取消交接和异步资源归还检查继续保留。

证据集中在`artifacts/player-loader-crash-20261021/`。早期夹具两次缺少using导致编译失败，后续BMS夹具未明确设置全局Ruleset导致不可玩超时，均保留原日志；修正的是测试上下文，没有放宽生产跨玩法校验。旧生产条件的双例对照留在`replay-ownership-before.trx`：先出现过早切页断言，随后后台异常以与现场相同的multiple-containers堆栈逸出；共享宿主的后例可接到前例迟到异常，不能把两个失败都称作各自独立的异常复现。最终修后结果与构建见下节。

## 最终验证

以下均在最终C#源码上完成，源码摘要见`final-source-fingerprints.json`。构建/测试/formatter每个新shell先加载`UseDevelopmentStorage.ps1`，相关源码冻结，全部串行调度。

| 检查 | 结果 | 证据文件 |
| --- | --- | --- |
| Core Release：`FullyQualifiedName~Skinning\|FullyQualifiedName~.Skins.` | 1987通过、4跳过；含新增两种玩法的真实ReplayPlayer交接检查 | `core-skin-final.trx`、同名log |
| BMS Release：`TestSceneBmsPlayerLayoutPreparationFailure`、`TestSceneBmsPlayerSkinEntry`、`BmsAsyncRevisionLeaseProductTest`、`TestSceneBmsSoloPlayerPreStart`匹配范围 | 37通过，无跳过 | `bms-player-final.trx`、同名log |
| mania Release：`FullyQualifiedName~TestSceneManiaPlayer` | 4通过，无跳过 | `mania-player-final.trx`、同名log |
| `dotnet build osu.Desktop/osu.Desktop.csproj -c Debug --no-restore -m -verbosity:m` | 0错误、2条既有NU1902 | `desktop-debug-build.log` |
| 同命令`-c Release` | 0错误、2条既有NU1902 | `desktop-release-build.log` |
| `dotnet format osu.Desktop.slnf --no-restore --include osu.Game/Screens/Play/PlayerLoader.cs osu.Game.Tests/Skins/SkinPlayerLoaderHandoffProductTest.cs --verify-no-changes` | 通过；初次因新增System using使旧全限定Func名称可简化，修正后定点复查通过 | `format-final.log` |

Core的4个跳过是3项opt-in作者动画测量与1项本机8.3路径别名能力检查。BMS编译仍有既有CS8600/CA2007告警；未删除测试或屏蔽警告。本轮没有重跑整个BMS/mania全集，较早全量不记为本补丁后的全量。新增交接检查通过并不表示自然重放了用户那一局的全部配置、BGA和设备状态。

文档、工作区导航与最终差异检查结果随本轮收尾记录在`documentation.log`、`workspace-docs.log`与`diff-check.log`；最终证据索引为`validation-final.json`。

## 交付与边界

本次修改客户端开局交接，不修改Omni作者包、美术、用户保存配置、谱库、判定或计分。现有独立`.v261021.osk`继续使用，无需为此修复重新导入。必须重新编译并重启客户端；本轮受控软件检查不代签用户正常窗口的再次游玩、实体设备、前台帧时间或长期稳定性。原[作品验证](LIGHTING_DETAILS_20261021.md)保留原日期和范围。

没有新建发行包、publish目录或临时运行安装副本；本轮新文件仅为受控检查证据及源码文档，不需要另加一次大目录清理。
