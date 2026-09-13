# 静线黑白轨与皿轨比例（2026-09-13）

用户反馈作者标级和当前BPM新增的矩形底板突兀，皿音符与轨道宽度不一致，并要求核实IIDX黑白轨比例。此次只沿用普通作者文件修改美术，通过BMS公开布局配置提供必要的黑键宽度；不修改判定、输入或滚动时间。

## 参考取证

原始图像见[长期参考索引](references/simple-1p-20260912/README.md)。读取原PNG第400行的轨道分隔线坐标，并对照相邻行与整图判断，未编辑参考图片：

| 原图 | 皿轨宽 | 白键轨宽 | 黑键轨宽 | 边界依据 |
| --- | --- | --- | --- | --- |
| 图04 LITONE12 | 92px | 54px | 42px | 分隔线起点74/166/220/262/316/358/412/454/508 |
| 图05 IIDX32实机 | 约90–92px | 约53–54px | 约42–43px | 压缩图细线约78/169/222/264/318/360/413/456/509，存在1–2px误差 |

因此采用白键:黑键:皿轨约 **1:0.778:1.704**，取图04清晰边界的54:42:92，并由用户实机图05交叉确认方向和量级。这是参考图测量，不是KONAMI发布的精确像素规格。网络检索也查阅了[BMS Community皮肤目录](https://github.com/BMS-Community/resources/blob/master/README.md#skins)，未找到足以替代实机图测量的官方宽度参数，不拿机台物理按钮尺寸或民间环境摆位数据冒充屏幕轨道比例。

## 修正与边界

- 移除作者标级和当前BPM额外叠加的矩形色块，保留原仪表外壳及细刻线，恢复一致底色。
- 此前普通键全部等宽、皿轨权重1.5，另有ScratchLaneSpacing=0.12的独立间隔。静线5K/7K/14K改为NormalLaneWidth=1、BlackLaneWidth=0.7777778、ScratchLaneWidth=1.7037037、ScratchLaneSpacing=0。
- 黑键由每侧规范键序2/4/6确定，不按显示次序或音符颜色猜测；7K第4键即使颜色为Yellow仍是黑键，14K第二侧从第一白键重新开始。同步修正静线第二侧素材角色的奇偶起点。
- 作者工具的同一键序修正也更新Aurora Study练习包的14K素材引用与成品，未改变其美术或重新启用complex；保证练习包仍可从保留源重复生成。
- BlackLaneWidth只读选定包，缺失/非法回到已解析NormalLaneWidth，旧包仍等宽；9K/PMS保持其等宽布局。新参数仅横向权重，不改变场高、判定线、时间区或输入身份。
- 实际音符drawable沿exact lane宽度渲染，未发现运行时强制使用普通键宽。旧128px音符左右透明3px，仅122px可见；皿轨较宽时边隙更大，再与独立皿旁间隔叠加。按新宽度补偿普通音符及LN头尾边距：白键3px、黑键4px、皿2px，分隔线素材分别10/13/6px（256px底图），使投影后的边缘厚度接近。纵向落点仍为row21；LN body继续既有独立窄条合同，不一起放大。

## 验证

- Release focused：185通过、0失败；覆盖轨宽、旧包缺省/非法值、5K/7K左右皿、14K第二侧、9K不变、实际皿note/LN头尾宽度，以及内置静线BMS/mania正常加载。命令与结果保存在`artifacts/simple-lanes-focused.log`和`osu.Game.Rulesets.Bms.Tests/TestResults/simple-lanes-focused.trx`。
- 新增皿LN验证第一次有2项等待超时：测试平移StartTime后又平移EndTime，将LN错误延长，且要求三个组件同帧存活。修正测试为保留1000ms时长，分别在真实生存期验证同一lane的源Sprite宽度；没有修改生产时序或放宽宽度断言。上述185通过为修正后重建结果。
- 实际desktop捕获exit0，见[图17](references/simple-1p-20260912/17-simple-lane-proportions-runtime.png)，SHA256 `b3730ff42153893c2c46c8d652f719611cdb8ee5d519e67e0a0cb58b6359399d`。可见皿note接近皿轨两边、窄黑轨/宽白轨、等厚分隔线，作者标级与BPM已无额外矩形底色。原1366×768像素含测试浏览器边栏、合成7K自动游玩、无BGA媒体，不代签用户完整歌曲或其它设备。
- 作者检查19项通过。首次检查指出Aurora Study保留成品与修正键序后的重新生成不一致；同步其14K引用并重新打包后通过。记录`artifacts/simple-lanes-author-verify.log`与`skin-authoring/docs/authoring-tool-verification.json`。

- BMS Release full：2265通过、5失败、16跳过，12m22s。5失败为`TestNoPublicDeclarationRetainsActualLegacyCustomFallbackInstances`、`TestSelectedPublicSlotsGateActualLegacyCustomFallbackInstances`及`TestSimpleLaneContrastAndDividersUseTheActualAuthoredImages`的7K P1/P2、14K Center三项：旧预期仍指定`bms/divider`，实际按新角色提供`bms/divider-accent`。来源、Provide、实际文件实例与禁止程序补画的断言保留。同步后首次相关复验5通过/2失败，暴露两个custom fallback预期还未将14K第二侧角色重置；修正后重建，全部7项通过。没有在此期间修改生产代码或皮肤源，未再重复整个full，也不把分次计数相加冒充单次全绿。首次full、首次复验及最终复验分别保留在`artifacts/simple-lanes-bms-full.log`、`artifacts/simple-lanes-material-expectations-first.log`、`artifacts/simple-lanes-material-expectations.log`。
- 16跳过为15项未提供已核验备份根的历史产品检查及1项headless截图入口；实际desktop截图已单独执行。
- 最终`dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m`通过，0警告/0错误（`artifacts/simple-lanes-release-final.log`）。此前并行准备测试期间的预备build不计最终gate；最终构建前源文件已冻结。
- 作者成品与正常Release内置canonical成品各119项，逐条解压内容与作者源一致，见`artifacts/simple-lanes-package-content.json`。作者包SHA256 `397f255da21b007ed53bdb5d3d5f87030d7437fb88591901d5e6f08b58e37240`；canonical包SHA256 `30bd5510cb6af473bc1866348883fa394dc8c6fccae1f5875cbd90edb09ffc3a`。两种ZIP工具摘要不同不代表内容差异；此次没有另跑发行安装包升级。
- `CheckDocumentation.ps1`与`git diff --check`通过；文档检查覆盖169个Markdown、1539个相对链接、155个本地锚点、105个memory链接。指纹提示对应公开成品及取证图片校验值，P1-A STATUS已回到建议预算内。

本次没有共享osu.Game或mania源代码改动，按BMS专属组件合同不重跑core/mania full；此前结果只保留历史身份。整体视觉仍待用户完整歌曲反馈，不以合成谱截图代签。
