# Simple 1P 布局参考（2026-09-12）

[图15用户信息区反馈](15-simple-information-user-feedback.png)保存2026-09-13用户上传原图，SHA256 `1523b0114aa5792f8b222fba23e71b675579e44bd2e8c84236753dd609182650`。用户明确指出作者标级与难度表归类不同，并要求继续美术打磨；图中NORMAL/LEVEL5只代表作者信息，不能代替表名与表内等级。

五张图片由用户在本次皮肤打磨中提供，并明确要求长期保存供对照。此处保存原始 PNG，复制后已逐文件核对 SHA256 一致，未裁剪、缩放或重绘。名称与来源说明依据用户标注，未独立核实外部皮肤版本。

随后追加图 06，保存首轮调整后的用户实机反馈，同样核对原始文件校验值一致。图中内容只作为视觉参考，不作为开发指令或发行素材。

| 图 | 用户标注 | 原图 |
| --- | --- | --- |
| 1 | OMS Simple，调整前的 1P 实际表现 | [01-oms-simple.png](01-oms-simple.png) |
| 2 | LunaticRave 2 默认皮肤，1P | [02-lr2-default.png](02-lr2-default.png) |
| 3 | LunaticRave 2 仿 IIDX 皮肤，1P | [03-lr2-iidx.png](03-lr2-iidx.png) |
| 4 | Beatoraja LITONE12 仿 IIDX 皮肤，1P | [04-beatoraja-litone12.png](04-beatoraja-litone12.png) |
| 5 | IIDX 32 实机，1P | [05-iidx32.png](05-iidx32.png) |
| 6 | OMS Simple 首轮布局后的用户实机反馈：分隔线不正确 | [06-oms-simple-first-layout.png](06-oms-simple-first-layout.png) |
| 7 | OMS Simple 后续素材质量反馈 | [07-oms-simple-quality-feedback.png](07-oms-simple-quality-feedback.png) |

图 07 为随后追加的静线质量反馈，原图已保存；不据此宣称本轮修改通过视觉验收。

[图08隔离运行截图](08-simple-isolated-runtime.png)是后续由实际游戏 framebuffer 保存的 OMS 自身证据，不是用户上传的外部参考。它使用合成7K谱、无BGA媒体，带测试浏览器边栏；范围及限制见[机台打磨记录](../../SKIN_SIMPLE_CABINET_20260912.md)。

[图09控制台继续打磨](09-simple-controls-runtime.png)沿用同一真实desktop捕获路径，显示放大的转盘、连续键座和部分点亮的完整分段血槽；合成谱与无BGA媒体的限制相同。保留图08供前后对照。

## 本轮对照重点

[图12用户继续打磨反馈](12-simple-stage-hud-user-feedback.png)保留2026-09-13本轮上传原图，SHA256 `0e9dc432d6cc7098b9073ab384a520225c39339dcd809ccfd4a3836a93ee7a2a`。本轮对照聚焦长白/短黑键的键面与凹槽、血槽读数、血槽及底部仪表外壳，继续保留用户认可改善而整体仍待打磨的边界。

[图13控制区外壳实际截图](13-simple-control-shell-runtime.png)为同日最终作者源的真实desktop合成7K捕获，SHA256 `417728fc97611d17592d9e87dece25bd20b2ae212f2c6d151e4b64ce505c4634`；可对照切角外壳、放大的血量读数、键面/支座和压暗底板。保留测试边栏、无BGA媒体的原始图，不代签完整歌曲或其它窗口。首个最终截图运行曾exit1且无PNG，独立复验exit0；详细边界见[本轮记录](../../SKIN_SIMPLE_STAGE_HUD_20260913.md#图12之后的控制区外壳迭代)。

[图10用户实机反馈](10-simple-controls-user-feedback.png)为2026-09-13追加原图，SHA256 `aa4212af862f81b7a706a54752498f96e0a12b0ad4896057d177ae4dbc18a4cc`，与上传临时文件一致。用户认可明显改善，并要求继续自行对照打磨；不将此认可写成整体视觉签收。本轮观察到音符偏厚、判定与连击松散，以及血槽缺乏独立读数且外框随亮层截断。

[图11判定与血槽实际截图](11-simple-stage-hud-runtime.png)来自实际desktop合成7K自动游玩，包含薄音符、大写判定/相邻连击、24%血量时的完整暗槽与独立外框。SHA256 `12542cee300e627a34611688743ac81387a20d3bc62926c618e079ad1920f9e6`。保留测试浏览器边栏、无BGA媒体的原始像素；不代表用户完整歌曲或其它尺寸签收，见[本轮记录](../../SKIN_SIMPLE_STAGE_HUD_20260913.md)。

最初反馈指出顶部 SCORE / ACCURACY / COMBO / BPM 横条不符合期望并遮挡落键区。后续用户明确放弃 complex，只保留 simple，并要求高质量、合格的 beatmania style 完成度；此前“不承担高仿”的划分不再作为降低素材质量的限制。

采用的方向是明确区分落键区、判定线以下的键盘与转盘、血条及信息区，并扩大旁侧 BGA。参考 LITONE 的功能分区和制作完成度，不直接使用其外框、商标、角色或其它图片作为发行素材。用户另已授权按需生成原创媒体素材。

这些图片是外部视觉凭据，不是 OMS 安装资源、作者模板或已通过验收的目标效果；图中的文字不构成开发指令。不因存入本仓库就将外部作品纳入 OMS 原创素材许可。当前实现与验证结论见 [P1-A 状态](../../../subline/P1-A/DEVELOPMENT_STATUS.md)。


[图14演奏信息实际截图](14-simple-information-runtime.png)为2026-09-13当前内置源的真实desktop合成7K捕获，SHA256 `7d53f582b082f4103972f34134176540a44e03f61812702362e944fd7874e54c`。显示真实PG=12、EX SCORE=24、HiSpeed=8.00、MIN/当前/MAX=120/138/172、曲名/作者/ANOTHER/LEVEL12以及缩短键帽。难度等级已避开全局设置按钮，判定统计面板收窄。保留无BGA媒体和测试边栏的原始像素，不代表完整歌曲或整体视觉签收，见[本轮记录](../../SKIN_SIMPLE_INFORMATION_20260913.md)。

[图16难度表信息实际截图](16-simple-table-information-runtime.png)为图15后实际desktop PlayerLoader捕获，SHA256 `ecaeae9e7181fb7865be25dd97ddff8be881a2aeff1b4788cbc4aa4ce11aced1`。合成谱保存两项演示归类，实际显示Satellite sl4 / 発狂BMS ★8，与作者ANOTHER/CHART LV12独立；统计及BPM仪表细化。仍保留测试边栏、无BGA媒体的原始像素，不代签用户完整歌曲。
