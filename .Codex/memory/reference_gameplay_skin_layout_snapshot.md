---
name: reference_gameplay_skin_layout_snapshot
description: 唯一 layout publication、mania stage-local 映射、exact owner 与准备失败地雷
metadata:
  node_type: memory
  type: reference
---

# Gameplay layout snapshot 地雷

完整 context/snapshot、consumer 矩阵与验证面见 [P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)；当前门读 [STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)。这里保存为什么不能恢复第二套 geometry 权威。

## 判断是否同一结果

- 显式作者stage/lanes是同一solver的输入，不是新拓扑；按真实group-local logical顺序映射，空槽不进lane集合。BMS不得重建field时丢authorTop，旧BGA退让也不能重新移动作者field；键区/默认血槽越safe应整profile诊断回退，不留半套几何。
- Mania键区放到field底下后，ColumnFlow仍有滚动mask。仅有显式key surface时使用现有TopLevelContainer proxy出mask；修改snapshot正确不证明真实按键图可见，需原输入和framebuffer证明。无声明路径不改。
- 同一stage-relative判定模板可按两个真实group分别Provide，状态机/事件须按实例scope隔离。明确Suppress第二group会丢该stage独立判定，不能靠另一侧同时命中或全局combo增长证明已显示。

- GameplaySkinLayoutContext 绑定 native/ruleset context、既有 topology、presentation/environment 与 exact package/revision；Snapshot 是一次 solve 的完整 immutable 结果。
- GameplaySkinLayoutPublication 绑定 neutral snapshot、引用它的 typed adapter、resolved material 与 prepared scene。Current 是 view，不是第二交换点；event target/revision 也从同一 publication 取得。
- 同值 snapshot 不等于同一 authority。consumer 不能从 drawable size、raw profile、默认 fixed rect、event payload 或 topology-only revision 再算一份，也不能重跑 source/material lookup。
- exact one-shot 必须在 shared GameplaySkinLayoutRevisionOwner 的锁内。若只在 BMS/mania helper 防二次 prepare，cached descendant 可直接重入 shared API，造成旧 child 持 A、late child 读 B。已有 exact current 后在 work lease/solve 前拒绝第二次 prepare。

## Adapter 容易错在哪里

- BMS keymode 来自 parser-owned resolution；BmsPlayfieldLayoutProfile 只是唯一 solver 内部配置或 isolated compatibility 输入，不是另一生产 geometry。
- Mania 必须使用真实 single/dual stage-column vector；special key 按 stage-local column 判，不用 total columns、global modulo 或 enum ordinal。
- logical/visual、global/group-local index 都是显式 order，不是 stable ID。Mirror/Random/S-Random 改对象最终目标 lane，不改固定 topology；object、keysound 与 skin lookup 使用同一目标 LaneId。
- BGA 的最终 viewport/rect 属于该 layout；内容/timeline/seek 仍属 P1-L。统一 viewport 不能证明统一 decoder/clock；会话与显示替换的释放边界见 [[reference_bms_bga_chain]]。
- 可选布局字段不能直接查聚合 `ISkinSource.GetConfig`：选定包缺项会继续命中 canonical 的作者值，使旧包意外启用独立键区或另一套 BGA 窗口。`KeyAreaHeight`、`BgaViewports` 等作者布局字段须从同一 package revision 的 exact selected BMS source 读取 accepted declaration，缺项交 solver 默认；必须用普通导入旧包核实，直接传配置对象的 solver 测试看不到此问题。
- 同一 HUD slot 可以有多个平级作者节点。`TryGetHostedDrawable` 的兼容入口只返回首根，首根可能只是面板，不能据其没有 `SpriteText` 判断准确率消失；完整 HUD 检查需遍历该 slot 的已路由节点并核真实文字，同时保留 native/residual 隐藏与同一 publication 断言。
- 屏外 `BufferedContainer` 的 `AlwaysPresent` 不会取消屏幕蒙版裁切。HUD 共用 capture 固定在 `X=2` 时须显式保留离屏绘制并先于前景 view；否则框架的 `MainBuffer` getter 会在 owner 初始化 renderer 前被 view 访问，表现为启动绘制线程空引用，之后捕获步骤才超时。不能把末尾 Until 超时当根因，也不能靠隐藏 fallback 或吞异常修复。view 还须明确填满前景画布；默认零尺寸不是有效镜像。合同见[P1-A HUD约束](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#hud-与既有迁移约束)。
- menu/shell/background 不因使用 skin texture 就成为作者 gameplay layout surface。

## 从真实截图排查分区遮挡

- 配置取景的目标取`SkinSection.ScopeSelector`，不再跟随`Game.Ruleset`或当前游玩皮肤。旧fixture须在隔离偏好中指定相应模式皮肤，再等待卡片目标就绪；图卡选择器为`SkinOptionChoiceSettings`，不能只数或操作`FormDropdown`。否则正确导入后仍会超时或修改另一模式，见[P1-A记录](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-20omni移除lightning副屏)。
- scene的`z`只排序同一固定layer，不能把Overlay里的`decoration`放到原生Note/LN后。Omni光柱误用decoration会盖住正在Holding的CN；逐轨背景应声明LaneSurface，并移除Frame对同一target的透明占位以避免DuplicateDeclaration。不要直接换KeyFlash：该槽投影的是hit-target区域，和整条lane不是同一几何。既有Underlay可满足作者行为，不要为单个包更改公共层级；修正和原生取景见[P1-A日志](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-19omni光柱层级修正)。
- AFP跨版本矩阵对照须保留同名binding的全部实例与深度路径；DP两侧可同时叫lane_1p，仅用名称作字典键会覆盖一侧并制造假差异。按movie/name/occurrence逐项比较，再与红线、键灯和实机交叉核定，不能由背景矩阵单独推出原生演奏几何变更。诊断例见[31制作记录](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-18iidx31-epolis-游玩候选)。
- 同名或同字节PNG不证明相同源几何。SD小BPM的28×18共用图在原GEO中是19×16、UV仍为完整0～1；须按网格非等比显示，不能据PNG比例裁图或统一乘画布倍数。框体的SD投影不适用于另有HD局部stage的共用音符/击打。源单个score占位符也不证明动态位数或advance，应另测面板内区域并把多位排布标为候选；作品边界取[P1-A素材约束](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#iidx-素材作品约束)。
- CUSTOM的long/short档须比较有效alpha、原图内容与GEO，不能只读PNG外尺寸后套scale：同尺寸音符图可能靠顶部留白改变厚度，CN档可能只改头尾而不改body，beam档可同几何而换整束图。源IFS、菜单缩略图、可选/解锁与真实consumer分别取证；frame预览编号也不能当版本号。FC解析的unknown placement可携带滤镜，原总帧数不等于最后可见帧，不能静默丢字段或截尾声称原样；诊断证据见[完整性记录](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-18iidx-custom完整性核对)。
- AFP裸movie bounds未计框体遮挡，可能为负坐标且跨过轨道。先核原静态层的全部视频开口，黑底放框体下方，真实播放器使用开口内安全矩形；单个movie不能证明原host的复制/裁切规则。DP固定符号可能在另一透明侧框层，不能只取center底图或假定侧层为空。
- `hud.text` 容器下的显式 `global` 子节点不会自然继承 HUD 坐标区，而是沿安全屏幕定位；顶部信息跨入落键区时先看实际 prepared target/slot 路由。平级节点可各自拥有 `hud.text`，也可由一个 owner 容器组织无 slot 子节点，位置取决于实际 target 与 layout-surface；不能把合法 owner 容器误禁，也不能嵌套 slot owner 来修位置，后者被 preparation 拒绝是合同保护。
- BMS 原键图在落键区的裁剪链中。只把键图画高或扩 `HitTargetHeight` 既不能得到独立控制台，还可能侵占判定附近；应在唯一 solver 产生独立键区，保留缺省旧包位置，不改判定/滚动长度。
- BGA 的 scene 外框放大不等于真实播放器 viewport 放大；应从公开尺寸参数进入唯一 solver，再由现有播放宿主消费。外部截图只用于比较视觉关系，不能推导出播放/timeline 新 authority。
- 原图凭据见[静线 1P 参考](../../doc_md/other/references/simple-1p-20260912/README.md)。底部 HUD 的公共几何变化也会影响未改作者文件的包，回归不能只观察 simple。
- 作者生成器的 `Complex=false` 同时覆盖静线与旧 Aurora 演练，不能据此把新布局应用到所有旧作品。静线通过 `author.json` 的 `compactLayout` 明确选择新模板；未声明的作品保持旧生成字节。必须实际运行 `Test-Authoring.ps1` 的重新生成与重复打包检查，只有源包一致性检查不足以发现这个问题。
- `SkinAuthoring pack` 输出普通作者包与控制台摘要，作者交付的 `dist/oms-simple.osk/.sha256` 仍须配对更新。正常 Game 构建已从 sources 另生成 `obj/canonical/<Configuration>` 原件及嵌入摘要，不再把 dist 作为安装信任输入；只手工覆盖运行目录包而不重编配对摘要仍会触发完整性保护。不要把这类失配误判为布局或异步 ownership 回归；两条打包链按解压内容核同源，见 [[reference_skin_canonical_installation]]。
- 提交前用 `git ls-files --eol` 核作者输入的实际换行。相同 CRLF 工作树重复打包也会通过，但提交按 `.gitattributes` 规范成 LF 后重做不一致；`Test-Authoring.ps1` 的 LF 检查必须包含 `author.json`，不能只检查生成的 manifest/scene。

## 纹理线宽与共享舞台目标

- 分隔图按整车道缩放，256 像素宽画布中只有 3 像素着色时，在约 56 像素车道上不足 1 像素；转盘更宽还会放大同图线宽。排查先看作者纹理的有色占比、真实 lane rect 与槽位是否已隐藏默认组件，不先改引擎描线。首轮实机反馈见[图 06](../../doc_md/other/references/simple-1p-20260912/06-oms-simple-first-layout.png)。
- `hud.gauge` 仅支持舞台目标；把 deck-1/deck-2 无条件平铺到共享场景，即使 codec/check 接受，也会在实际单舞台或 mania preparation 返回 019。早期分段罩因此撤回；现有 `instances.material` 按同一选定包已解析的 Global/Stage Provide 素材实例化，固定暗格与裁切亮格已接入，不能继续沿用“连续血条”结论，也不能放宽不存在目标的合同。诊断见 [[reference_skin_cabinet_surfaces]]。

## Geometry fallback 与构造边界

- `Player.load`在缓存`FrameStableClock`时为空，先查规则集loader是否真正进入：该字段只在loader赋值，Dispose并不清空。布局准备Rejected或有界Retry耗尽可使delayed Content未挂，构造过规则集不等于加载成功。逻辑screen栈空也不等于旧layout owner已退役；测试先通过真实Parent移除并Dispose上一局，再等其owner退休，不直接Dispose participant、不靠sleep/GC或提高次数。实际失败/取消收尾见[P1-A 3m4d](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#c2-current-revision与publication)，不能用半载成功或读取空时钟收尾。
- 回收尚未加载的renderer会经过已经物化的lazy Playfield；Stage已构造不表示`SkinnableDrawable.Drawable`已创建。退订只针对真实存在的背景Drawable，其余注册和base子树仍须释放；不能用`IsLoaded`跳过整段或把测试改为强制挂载来掩盖构造期泄漏。具体红灯见[P1-A收尾](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-20选曲与皮肤遗留软件问题收尾)。

- 作者输入的数值边界与 solver 的实际安全布局分开验证；无论缺省还是拒绝结果，都一次产出完整 snapshot，不拼部分新/旧 geometry。不要把旧标量的默认布局推广到显式 `BgaViewports`：合法排列在当前画面放不下时整组零窗并诊断，不擅自换回默认；`none` 也不是缺省。窗口与独立信息带边界见 [[reference_bms_bga_chain]]。
- 测试复现要包括窄/宽 aspect、DPI、safe-area 与 14K 双 field/scratch/gap/BGA/HUD；普通 7K 正常不证明这些关系。
- 显式作者矩形允许小于一像素的高度，判定线须限制到实际field高度再Clamp；Union会带来float减法误差，几何断言用合理容差。scene的RootDrawables是ownership wrapper，可见绑定写RuntimeNode.TransformDrawable；检查键灯时按源节点ID选中实际transform，验证原生松开→按下→松开，不能凭wrapper的Alpha证明输入响应。
- 同一specialised槽名不保证不同规则集的原生owner尺寸一致。BMS爆炸填命中区，mania爆炸填ColumnHitObjectArea有效全列；移植作者模板时须按实际host核世界尺寸，不能为了某作品修改公共host。首帧可能尚未布局，取有效实绘矩形并核宽高/命中锚点；素材与runtime无诊断仍可能出现成屏拉伸。
- Mania的DrawableNote以原生headPiece AutoSize Y撑高，替换将其Alpha归零后会退出布局；相对Both的作者owner也随之零高。长条头尾继承同一路径，body显式计算长度仍可见。仅检查IsApplied、纹理引用和Alpha会漏掉此错，pool等待准备的首帧也可能暂时遮蔽；须等后续更新核真实DrawHeight及Sprite屏幕矩形，再核源像素尺寸，见[P1-A合同](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#shared-prepared-graph与runtime)。
- 完整 host 在 child load 前通过 enclosing exact dependency scope 完成 publication。无 publication/material 不临时借 compatibility/default geometry 或 post-commit fallback。
- prepared carrier 只属于签发 owner；另一 owner carrier、同 root 第二 provider、compatibility→exact 升级、adapter 未引用 exact neutral snapshot 都属于 authority 违约。
- isolated compatibility 是显式 detached test seam，也应一次构造完整 graph；不能先让真实 provider 可见，再升级。
- Player 先加载皮肤布局根，之后才装入 DrawableRuleset；具体 ruleset config 缓存在 drawable 子树，不能从父布局根直接 Get。需要玩法配置时（如 mania 方向），布局准备通过游戏 IRulesetConfigCache.GetConfigFor(ruleset) 取得同一最终配置，不合成默认值或另建缓存；BMS 外观样式只取选定包声明及其配置覆盖，不重新读取旧全局样式。真实入口回归须用不 override CreateRuleset 的 OsuGameTestScene：OsuTestScene 的 CreateRuleset 非空时会预注入 DrawableRulesetDependencies，连 PlayerTestScene 也会因此掩盖缺失依赖。用非默认样式/方向核实际 PlayerLoader、自动演示及重试。

Prepare/commit、取消窄窗、late attach、lease/detach/retire 统一去 [[reference_skin_atomic_reload_detach]]；不要在 layout consumer 再做一套可交换状态。素材解析见 [[reference_gameplay_skin_codec_material]]，stable identity 见 [[reference_gameplay_skin_lane_identity]]。
