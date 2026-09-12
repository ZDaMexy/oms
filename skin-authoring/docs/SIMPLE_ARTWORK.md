# 静线素材来源与修改（2026-09-12）

所有已交付外观都由普通皮肤文件声明和引用，游戏不读取作者配方。两款继续长期内置，下面的制作步骤只服务作者修改副本。

- `sources/oms-simple/bms/lane-*.png`、`divider*.png`、`target.png`、`stage.png` 等精确几何素材由 `tools/SkinAuthoring/SkinRecipe.cs` 离线生成。修改配方后重新生成、检查、打包即可。
- [scratch-platter.png](../sources/oms-simple/bms/scratch-platter.png) 是本轮内置 imagegen 生成的原创转盘素材，透明 PNG。已复制到作者源目录，游戏经普通 `playfield.key` 和 legacy `KeyImageS` 路径引用；保留原始 alpha，未后期裁剪或改色。生成配方不会覆盖此文件。
- [预览板](../dist/oms-simple-preview.png) 用实际素材绘制，明确标记为作者预览，不能替代游戏截图。转盘素材、配方、配置与 `.osk` 一起保留供后续打磨。

## 转盘生成提示词

使用内置 imagegen 工具；没有调用 CLI/API，也未使用外部皮肤图片作为可分发素材。

```text
Use case: stylized-concept. Asset type: production 2D game skin sprite, a single rhythm-game scratch turntable for OMS Simple, a restrained arcade controller. Create one clean orthographic top-down perfectly circular turntable platter, centered on a genuinely transparent square canvas. The disc occupies 94% of canvas width and height with equal clear margins. Dark graphite black radial brushed metal surface with subtle concentric machining rings, a slim clean satin silver outer rim, an inset black ring, a small muted cyan circular center cap (about 15% of disc diameter). Subtle realistic studio highlights to convey metal, readable at 80 pixels, clean symmetrical geometry, no perspective tilt, no cast shadow outside the circle. Simple premium hardware, restrained and functional, no elaborate fantasy decoration. No text, no logos, no labels, no screws, no scene, no keyboard, no rectangular background, no glow halo. Output transparent PNG; this image itself will be used as the actual in-game texture, not as a mockup.
```
