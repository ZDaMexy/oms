# OMS Simple · 静线

这是同包支持 BMS 与 mania 的完整静线作者目录。视觉素材在 `bms/`、`mania/`、`scene/`，普通击打与持续音在根目录 WAV，配置在 `skin.ini`。`author.json` 是可重复绘制的配色输入。可以直接修改素材和配置，再通过制作套件检查、打包和普通导入。

两玩法沿用经典的矩形音符、长条和键面。mania 保留 Classic 的白色、粉色和金色分组，顶部仪表同时显示血量、分数、准确率和 BPM。BMS 按白键、短黑键和皿轨分别适配素材，保留淡蓝与粉色的角色区分。分数、Hi-Speed 和分段血槽跟随控制区，曲目信息、判定统计及 MIN/BPM/MAX 围绕 BGA 布置。判定和 FAST/SLOW 均读取实际游玩数据。

`author.json` 中的 `classicArtwork: true` 由作者工具读取。正常构建打包已有文件，`Author.ps1 generate` 可从随工具引用的资源重新生成。编辑作者副本时可以直接替换图片。

BMS 的 BGA 最大宽高和竖直位置由 `[Bms]` 中的 `BgaWidth`、`BgaHeight`、`BgaVerticalPosition` 控制，独立键区高度由 `KeyAreaHeight` 控制。BGA 由谱面提供，关闭了常驻装饰框。轨宽、下落位置和输入由原布局决定。

静线直接使用了 team osu! 为 osu! Classic 制作的 mania 音符、长条图片和键面，来自项目依赖 `ppy.osu.Game.Resources` 的 `Skins/Legacy`。mania 音符和长条保留原图像字节，键面缩放到键区。BMS 为不同轨宽、短键和皿轨调整了尺寸、留边与部分颜色。上游素材依 [CC BY-NC 4.0](https://github.com/ppy/osu-resources/blob/master/LICENCE.md) 使用。转盘、底板、仪表和命中光由 OMS 绘制。所有素材均无 HDD 原素材输入。

BMS 的按键光和命中光以短、窄的普通纹理表现，独立键区绘制转盘。mania 的按下态沿 `KeyImageD`，普通按下与松开有不同键帽图片。遮挡装饰、转盘装饰、激光和自由装饰继续由包显式关闭。

这是普通皮肤文件，不含隐藏素材、DLL 或特殊作者能力。最终安装保底由经验证的随游戏原件承担；本作者目录不是安装恢复位置。

OMS 自制部分 · MIT License · OMS contributors

根目录 WAV 是可重复生成的原创普通音效：normal、soft、drum 三组各含击打、拍手、结束、哨音，以及带滑动音长条使用的 sliderslide、sliderwhistle。音效走普通皮肤读取路径；谱面自带声音仍按游戏既有优先级使用。可使用音频编辑器替换，再检查、打包。

准确率、血量、谱面进度和判定统计由普通公开的只读信息驱动。
