# 静线素材来源与修改（2026-09-12）

所有已交付外观都由普通皮肤文件声明和引用，游戏不读取作者配方。静线是唯一内置，complex 只保留历史参考；下面的制作步骤服务作者修改副本。当前目标是高质量、合格的 beatmania style 外观，参考 LITONE 的完成度，不照抄素材，也不以简洁限制素材质量。

- `sources/oms-simple/bms/lane-*.png`、`divider*.png`、`target.png`、`stage.png` 等精确几何素材由 `tools/SkinAuthoring/SkinRecipe.cs` 离线生成。修改配方后重新生成、检查、打包即可。
- [scratch-platter.png](../sources/oms-simple/bms/scratch-platter.png) 是本轮内置 imagegen 生成的原创转盘素材，透明 PNG。已复制到作者源目录，游戏经普通 `playfield.key` 和 legacy `KeyImageS` 路径引用；保留原始 alpha，未后期裁剪或改色。生成配方不会覆盖此文件。
- [预览板](../dist/oms-simple-preview.png) 用实际素材绘制，明确标记为作者预览，不能替代游戏截图。转盘素材、配方、配置与 `.osk` 一起保留供后续打磨。

## 转盘生成提示词

使用内置 imagegen 工具；没有调用 CLI/API，也未使用外部皮肤图片作为可分发素材。

```text
Use case: stylized-concept. Asset type: production 2D game skin sprite, a single rhythm-game scratch turntable for OMS Simple, a restrained arcade controller. Create one clean orthographic top-down perfectly circular turntable platter, centered on a genuinely transparent square canvas. The disc occupies 94% of canvas width and height with equal clear margins. Dark graphite black radial brushed metal surface with subtle concentric machining rings, a slim clean satin silver outer rim, an inset black ring, a small muted cyan circular center cap (about 15% of disc diameter). Subtle realistic studio highlights to convey metal, readable at 80 pixels, clean symmetrical geometry, no perspective tilt, no cast shadow outside the circle. Simple premium hardware, restrained and functional, no elaborate fantasy decoration. No text, no logos, no labels, no screws, no scene, no keyboard, no rectangular background, no glow halo. Output transparent PNG; this image itself will be used as the actual in-game texture, not as a mockup.
```

## 完整底板与精细仪表

新原创素材位于 [scene/cabinet.png](../sources/oms-simple/scene/cabinet.png)：通过内置 imagegen 生成金属面板，没有输入外部参考图片，保存生成原图，不改写前述外部图片凭据。原始生成文件为 `exec-dcd5ecdf-092d-4aa0-a10d-437f686b49f4.png`，1536×1024，原样复制，不裁剪或改色，生成配方不覆盖。完整实际提示词：

```text
Create one production-ready ORIGINAL game UI texture asset, not a mockup or screenshot. A seamless-looking dark brushed graphite anodized metal console panel, perfectly flat front orthographic view, rectangular 3:2 landscape image. Full opaque background, edge to edge material. Premium Japanese rhythm arcade cabinet industrial finish: extremely fine horizontal brushed metal grain, restrained cool silver illumination along the very top, subtle charcoal geometric inset panel seams near the outer corners, tiny countersunk screws at four corners, one slim muted ice-cyan horizontal accent inset near the lower edge. Overall very dark (near #10151d to #242d37), sophisticated understated precision-machined finish. Center 80 percent must remain quiet uniform dark metal to support separately composited game controls and numbers. No text, no logos, no numbers, no buttons, no screen, no keyboard, no turntable, no existing brands, no perspective, no scene, no shadow outside panel. Asset must be useful as a scalable game console background with tasteful metallic detail rather than a complete game interface. Crisp clean high-resolution game production artwork.
```

本轮同时完善 BMS 键面和插槽、BGA 独立内边框、连续血条金属边及分立 HUD 仪表，并启用作者文件提供的按键灯和命中光。完整底板使用 Global `stage.background` 公共最小扩展，mania 透明 Global 背景保持原样。不是通过私有皮肤接口加载，也不新增跨拓扑 scene 条件实例能力。

源文件、生成包、Release和相关回归已验证，并保存实际desktop合成谱截图；整体视觉质量与完整歌曲下的画面仍未签收。命令、纠错及限制见[机台记录](../../doc_md/other/SKIN_SIMPLE_CABINET_20260912.md)。用户参考原图及图07见[参考凭据](../../doc_md/other/references/simple-1p-20260912/README.md)。

## 按键区与固定血槽

继续对照LITONE/IIDX的控制台比例：静线声明`ScratchKeyWidth: 2`，让既有原创转盘在独立按键区向外扩展，仍按比例显示。白键和短黑键重绘为带统一上下金属轨、凹槽与下方灯座的PNG，白键面占区域约80%、黑键约60%。这些精确拼接素材继续由离线配方制作，不修改转盘生成原图。

`bms/gauge.png`为1024×96的完整50格血槽。纯纹理消费者保留同图暗化的全长底槽，再从左向右裁切亮层，低血量不会压缩边框和格距。各格使用统一青色，不把静态红色分区误称为所有gauge模式通用的过关线。尚未增加轨道下方的独立实时百分比读数。
