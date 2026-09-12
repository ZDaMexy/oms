# Simple 1P 布局参考（2026-09-12）

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

## 本轮对照重点

最初反馈指出顶部 SCORE / ACCURACY / COMBO / BPM 横条不符合期望并遮挡落键区。后续用户明确放弃 complex，只保留 simple，并要求高质量、合格的 beatmania style 完成度；此前“不承担高仿”的划分不再作为降低素材质量的限制。

采用的方向是明确区分落键区、判定线以下的键盘与转盘、血条及信息区，并扩大旁侧 BGA。参考 LITONE 的功能分区和制作完成度，不直接使用其外框、商标、角色或其它图片作为发行素材。用户另已授权按需生成原创媒体素材。

这些图片是外部视觉凭据，不是 OMS 安装资源、作者模板或已通过验收的目标效果；图中的文字不构成开发指令。不因存入本仓库就将外部作品纳入 OMS 原创素材许可。当前实现与验证结论见 [P1-A 状态](../../../subline/P1-A/DEVELOPMENT_STATUS.md)。
