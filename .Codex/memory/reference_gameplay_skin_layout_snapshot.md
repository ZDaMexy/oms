---
name: reference_gameplay_skin_layout_snapshot
description: 唯一 layout publication、mania stage-local 映射与 exact owner 重入/注入地雷
metadata:
  node_type: memory
  type: reference
---

# Gameplay layout snapshot 地雷

完整 context/snapshot、consumer 矩阵与验证面见 [P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)；当前门读 [STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)。这里保存为什么不能恢复第二套 geometry 权威。

## 判断是否同一结果

- GameplaySkinLayoutContext 绑定 native/ruleset context、既有 topology、presentation/environment 与 exact package/revision；Snapshot 是一次 solve 的完整 immutable 结果。
- GameplaySkinLayoutPublication 绑定 neutral snapshot、引用它的 typed adapter、resolved material 与 prepared scene。Current 是 view，不是第二交换点；event target/revision 也从同一 publication 取得。
- 同值 snapshot 不等于同一 authority。consumer 不能从 drawable size、raw profile、默认 fixed rect、event payload 或 topology-only revision 再算一份，也不能重跑 source/material lookup。
- exact one-shot 必须在 shared GameplaySkinLayoutRevisionOwner 的锁内。若只在 BMS/mania helper 防二次 prepare，cached descendant 可直接重入 shared API，造成旧 child 持 A、late child 读 B。已有 exact current 后在 work lease/solve 前拒绝第二次 prepare。

## Adapter 容易错在哪里

- BMS keymode 来自 parser-owned resolution；BmsPlayfieldLayoutProfile 只是唯一 solver 内部配置或 isolated compatibility 输入，不是另一生产 geometry。
- Mania 必须使用真实 single/dual stage-column vector；special key 按 stage-local column 判，不用 total columns、global modulo 或 enum ordinal。
- logical/visual、global/group-local index 都是显式 order，不是 stable ID。Mirror/Random/S-Random 改对象最终目标 lane，不改固定 topology；object、keysound 与 skin lookup 使用同一目标 LaneId。
- BGA 的最终 viewport/rect 属于该 layout；内容/timeline/seek 仍属 P1-L。统一 viewport 不能证明统一 decoder/clock。
- 新增的可选布局字段不能直接查聚合 `ISkinSource.GetConfig`：选定包缺项会继续命中 canonical 的作者值，使旧包意外启用独立键区并放大 BGA。`KeyAreaHeight` 与三项 BGA 参数用同一 package revision 的 exact selected BMS source 读取 accepted declaration，缺项交 solver 默认；必须用普通导入旧包核实，直接传配置对象的 solver 测试看不到此问题。
- 同一 HUD slot 可以有多个平级作者节点。`TryGetHostedDrawable` 的兼容入口只返回首根，首根可能只是面板，不能据其没有 `SpriteText` 判断准确率消失；完整 HUD 检查需遍历该 slot 的已路由节点并核真实文字，同时保留 native/residual 隐藏与同一 publication 断言。
- menu/shell/background 不因使用 skin texture 就成为作者 gameplay layout surface。

## 从真实截图排查分区遮挡

- `hud.text` 容器下的 `global` 子节点不会自然继承 HUD 坐标区；顶部信息跨入落键区时先看实际 prepared target/slot 路由。当前作者写法让每个平级图形、文字显式拥有 `hud.text`，不能嵌套 slot owner；嵌套被 preparation 拒绝是合同保护，不应放宽。
- BMS 原键图在落键区的裁剪链中。只把键图画高或扩 `HitTargetHeight` 既不能得到独立控制台，还可能侵占判定附近；应在唯一 solver 产生独立键区，保留缺省旧包位置，不改判定/滚动长度。
- BGA 的 scene 外框放大不等于真实播放器 viewport 放大；应从公开尺寸参数进入唯一 solver，再由现有播放宿主消费。外部截图只用于比较视觉关系，不能推导出播放/timeline 新 authority。
- 原图凭据见[静线 1P 参考](../../doc_md/other/references/simple-1p-20260912/README.md)。底部 HUD 的公共几何变化也会影响未改作者文件的包，回归不能只观察 simple。
- 作者生成器的 `Complex=false` 同时覆盖静线与旧 Aurora 演练，不能据此把新布局应用到所有旧作品。静线通过 `author.json` 的 `compactLayout` 明确选择新模板；未声明的作品保持旧生成字节。必须实际运行 `Test-Authoring.ps1` 的重新生成与重复打包检查，只有源包一致性检查不足以发现这个问题。
- `SkinAuthoring pack` 输出普通包与控制台摘要，不替维护安装原件的 `dist/oms-simple.sha256`。更新 canonical 包后必须同步该摘要并重编 `osu.Game`（摘要为 embedded resource）；只复制新包会触发完整性保护，使实际选择及依赖它的异步测试失败。不要把这类失配误判为布局或异步 ownership 回归。
- 提交前用 `git ls-files --eol` 核作者输入的实际换行。相同 CRLF 工作树重复打包也会通过，但提交按 `.gitattributes` 规范成 LF 后重做不一致；`Test-Authoring.ps1` 的 LF 检查必须包含 `author.json`，不能只检查生成的 manifest/scene。

## 纹理线宽与共享舞台目标

- 分隔图按整车道缩放，256 像素宽画布中只有 3 像素着色时，在约 56 像素车道上不足 1 像素；转盘更宽还会放大同图线宽。排查先看作者纹理的有色占比、真实 lane rect 与槽位是否已隐藏默认组件，不先改引擎描线。首轮实机反馈见[图 06](../../doc_md/other/references/simple-1p-20260912/06-oms-simple-first-layout.png)。
- `hud.gauge` 仅支持舞台目标；shared scene 不会自动忽略不存在的 deck 或另一玩法的 group。把 deck-1/deck-2 平铺到共享场景，即使 codec/check 接受，也会在实际单舞台或 mania preparation 返回 019。不能将未适用目标误当可选节点；本次分段罩试验撤回，继续用连续血条，不放宽合同。

## Geometry fallback 与构造边界

- 每字段分别验证 finite、正值、range、safe screen 与 non-overlap，并给稳定诊断；fallback 也一次产出完整 snapshot，不拼部分新/旧 geometry。
- 测试复现要包括窄/宽 aspect、DPI、safe-area 与 14K 双 field/scratch/gap/BGA/HUD；普通 7K 正常不证明这些关系。
- 完整 host 在 child load 前通过 enclosing exact dependency scope 完成 publication。无 publication/material 不临时借 compatibility/default geometry 或 post-commit fallback。
- prepared carrier 只属于签发 owner；另一 owner carrier、同 root 第二 provider、compatibility→exact 升级、adapter 未引用 exact neutral snapshot 都属于 authority 违约。
- isolated compatibility 是显式 detached test seam，也应一次构造完整 graph；不能先让真实 provider 可见，再升级。
- Player 先加载皮肤布局根，之后才装入 DrawableRuleset；具体 ruleset config 缓存在 drawable 子树，不能从父布局根直接 Get。布局准备通过游戏 IRulesetConfigCache.GetConfigFor(ruleset) 取得同一最终配置，不合成默认值或另建缓存。真实入口回归须用不 override CreateRuleset 的 OsuGameTestScene：OsuTestScene 的 CreateRuleset 非空时会预注入 DrawableRulesetDependencies，连 PlayerTestScene 也会因此掩盖缺失依赖。用非默认样式/方向核实际 PlayerLoader、自动演示及重试。

Prepare/commit、取消窄窗、late attach、lease/detach/retire 统一去 [[reference_skin_atomic_reload_detach]]；不要在 layout consumer 再做一套可交换状态。素材解析见 [[reference_gameplay_skin_codec_material]]，stable identity 见 [[reference_gameplay_skin_lane_identity]]。
