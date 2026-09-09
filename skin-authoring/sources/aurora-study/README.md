# Aurora Study · 作者演练

这是用作者套件从完整静线模板实际制作的独立作品，BMS 改为薄荷绿色，mania 改为杏色，特殊键使用浅柠檬色。名字、作者和色彩均在 author.json 中编辑，然后通过 generate、check、pack 完成普通 .osk。

制作过程中不修改游戏源码、不读取隐藏素材，也不取得安装原件的特殊权限。作品仍含 BMS 全部支持键数和样式、mania 单双舞台完整素材，可由任何作者使用同一入口继续修改。

制作说明与复核证据见作者套件 docs/WORKSHOP.md。游戏导入、导出以及实际画面由本轮产品验证与人工验收分别记录。

MIT License · Authoring Kit workshop

根目录 WAV 是可重复生成的原创普通音效：normal、soft、drum 三组各含击打、拍手、结束、哨音，以及带滑动音长条使用的 sliderslide、sliderwhistle。音效走普通皮肤读取路径；谱面自带声音仍按游戏既有优先级使用。可使用音频编辑器替换，再检查、打包。

mania 的普通 KeyImage / KeyImageD 分别保留松开与按下图片，公共 playfield.key 显式 Inherit 到同包配置；关闭闪光也保留清楚的按下态。准确率与谱面进度通过所有作者可用的只读信息显示，不需要额外效果授权。
