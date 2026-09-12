# OMS Simple · 静线

这是同包支持 BMS 与 mania 的完整静线作者目录。视觉素材在 bms/、mania/、scene/，普通击打与持续音在根目录 WAV，完整配置在 skin.ini；author.json 保留可重复绘制的配色与制作配方输入。图片和配置都可直接修改，再通过制作套件检查、打包、普通导入。

当前 BMS 采用收窄的落键区，判定线下方为独立黑白键与转盘，随后是血条；分数、准确率和 BPM 放在底部，连击保留在判定附近。mania 仍使用顶部信息区。BGA 的最大宽高和竖直位置由各 `[Bms]` 块中的 `BgaWidth`、`BgaHeight`、`BgaVerticalPosition` 决定，独立键盘高度由 `KeyAreaHeight` 决定；这些字段需要支持本轮公开布局参数的 OMS 客户端。

`author.json` 使用 `compactLayout: true` 保留这版生成配方。该选项仅供作者工具生成普通文件，游戏不读取它；未声明的旧作品继续使用原配方。

BMS 轨道底色分别引用 `bms/lane-white.png`、`lane-accent.png` 和 `lane-scratch.png`；分隔线引用 `divider.png` 与较窄有色带的 `divider-scratch.png`，补偿转盘车道的宽度。`stage.png` 提供暗色底座，`target.png` 提供红色判定线。它们均为普通文件素材。

转盘引用原创生成位图 `bms/scratch-platter.png`，该文件由内置 imagegen 制作，生成配方不会覆盖它；请随源目录保留，或用自己的方形转盘图替换。游戏以等比方式显示该纹理。其余简单几何素材由离线作者配方生成，游戏运行时不执行配方。

静线显式关闭按键闪光、额外命中爆光、遮挡装饰、转盘装饰、激光、视频边框和自由装饰。普通音符、长条、键区、地雷、判定位置、分道、实际遮挡、连击、判定文字和能量保留。作者关闭的可选部分不应被补回。

这是普通皮肤文件，不含隐藏素材、DLL 或特殊作者能力。最终安装保底由经验证的随游戏原件承担；本作者目录不是安装恢复位置。

MIT License · OMS contributors

根目录 WAV 是可重复生成的原创普通音效：normal、soft、drum 三组各含击打、拍手、结束、哨音，以及带滑动音长条使用的 sliderslide、sliderwhistle。音效走普通皮肤读取路径；谱面自带声音仍按游戏既有优先级使用。可使用音频编辑器替换，再检查、打包。

mania 的普通 KeyImage / KeyImageD 分别保留松开与按下图片，公共 playfield.key 显式 Inherit 到同包配置；关闭闪光也保留清楚的按下态。准确率与谱面进度通过所有作者可用的只读信息显示，不需要额外效果授权。
