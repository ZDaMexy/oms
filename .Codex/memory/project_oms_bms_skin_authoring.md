---
name: project_oms_bms_skin_authoring
description: BMS 素材+ini 皮肤的当前可信面、稳定设计决议与实现地雷
metadata:
  node_type: memory
  type: project
---

# BMS 皮肤创作召回

权威当前态：[P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)；计划/约束：[P1-A PLAN](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md)、[CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)；V1 审计：[SKIN_SYSTEM_V1_ARCHITECTURE_20260710](../../doc_md/other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)；恢复证据：[[reference_skin_recovery_20260710]]。

## 当前可信面

- F1：独立 `[Bms]` parser、`BmsLegacySkin` 配置源、`.osk` 导入路由、现存静态件的颜色/纹理/几何、reference ini 自校验；其上已有 managed package 的 BMS 普通短键、critical `LongNoteHead` / `LongNoteBody` 与 optional `LongNoteTail` 静态/`name-{n}` 编号帧窄生产路径。body 素材与安全解析后的宽度绑定同一 package revision；tail 的透明链底是迁移 fallback，不是作者 `Suppress`。当前 gate 只看 P1-A STATUS，不在 memory 复制提交和测试数字。
- 当前 `OmsSkin` 是程序化逐组件链底，但只算迁移基线；最终由只读 `oms-simple.osk` 接管并移出程序化产品视觉。
- G1 产品面仍只保留 folder ctor 与 `SkinInfo.FilesystemStoragePath/IsExternalFilesystemStorage` + schema 56 载体；authority/path preflight 与 pure immutable capsule 只是无 production caller 的内部地基，Windows native capture、扫描/选择/删改/热重载均未接通。
- F2/F3/G2、Lua、mania fallback adapter、reference-default 均未落地。
- 恢复修正：base parser 前重置配置流；14K 第二皿使用 `S2`/P2 素材。

## 稳定产品决议

1. 皮肤范围以 gameplay 为主；不移植 LR2/beatoraja runtime，只对齐元素族。
2. OMS 方言为 mania-aligned 静态项 + `[Bms]` 扩展；按 keymode 分桶。
3. mania 普通 `.osk` 是固定 C# 行为宿主 + legacy 素材/参数，不是通用作者脚本上限。V1 共享 neutral codec/scene/event/reload/sandbox，mania 与 BMS 保留各自 topology adapter；采用 adapter-first 迁移。
4. 引擎拥有 gameplay truth、playfield/BGA layout、滚动/LN 裁剪、对象池、BGA 内容时钟和安全边界；外部 package 拥有具体 scene、动画与只读事件响应。
5. 三态为 `Provide/Inherit/Suppress`；仅 lane/scratch 可辨识、note/LN/mine、判定位置和启用中的 cover 几何不可 suppress。judgement display、combo、gauge visual、HUD、BGA frame、按键/命中特效均可关闭。
6. 发布交付同时含 mania/BMS 的 `oms-simple.osk` 与 `oms-complex.osk`。前者是只读 canonical fallback，后者证明 IIDX 级公开 API 上限；程序化 `OmsSkin` 在文件 fallback parity/完整性/恢复 gate 后退出产品渲染链。
7. 5K/7K 覆盖 P1/P2/CenterP1/CenterP2，9K BMS/PMS 居中，14K 双 deck/双皿/centre gap；所有 playfield/HUD/BGA/scene 消费一个 layout snapshot。
8. lazer layout editor 只管理既有 `ISerialisableDrawable` HUD；新 scene manifest 使用稳定 allowlisted node ID，不能复用序列化 CLR `Type` 的 editor JSON。
9. 对齐 osu 社区工作流：`.osk` 分发、根 `skin.ini`、mania 共同素材/动画命名、普通目录编辑与拖入导入；BMS/scene/script 是版本化扩展，不要求 DLL。
10. Skin Authoring Kit 是两包可编辑源 + 注释模板 + 字段/事件/layout/预算规范 + validator/diagnostics + `.osk` 打包说明，不是另一套 SDK/runtime。

## 实现地雷

- core `LegacySkin` 不得编译依赖 BMS ruleset；BMS 配置留在 ruleset，通过精确反射类型接入。类型匹配必须排除 `LegacyBeatmapSkin` 等其它子类。
- 不得把注入测试的 `BeatmapNoteSkin` 写成真实 BMS beatmap-local 能力：`WorkingBeatmapCache` 当前只创建不解析 `[Bms]` 的 `LegacyBeatmapSkin`，仓库也未定义 `.bme` 逐谱 sidecar。若产品选择实现，先冻结公开格式与 core custom-loader skin factory；根 `skin.ini` 只能称 beatmap-set-local，不能冒充逐谱。
- schema 由代码/真实组件确立，`SKINNING.md` 是派生说明，不能反向驱动实现。
- 贴图优先；无贴图才使用 ini colour/palette。composite 化后测试要读内层 visual，不读容器自身 colour。
- lane 宽经总相对宽归一化；同比缩放所有 lane relative width 无效。几何细节见 [[reference_bms_default_skin_geometry]]。
- `HitTargetVerticalOffset` 保持 0 以守住时间/滚动合同。
- geometry 仍整体缺少 finite/正值/range/screen-space/cross-field 的统一 solver；唯一窄例外是 `LongNoteBodyWidth` 已使用可复用标量解析器，只接受 finite 且 `0 < width <= 1`，否则以稳定 typed 原因逐字段回到 `0.5775`。这不是 `SV1-3` layout snapshot；playfield 读 skin profile，但 gauge/combo 会另建默认 profile、BGA 固定 rect。
- legacy mania 缺 `Keys:` bucket 时会合成默认 configuration；neutral model 必须保留 explicit presence，不能把合成默认误判为 `Provide`。
- 六类 lane-resource 的 `[Mania]` 兼容候选顺序已固定：5K `6→5`、7K `8→7`、9K 只用一个 `9`、14K `16→同一 Keys:8 bucket 分投两 deck→14`，scratch 在 key-only 层保持缺失；candidate plan 整体仍未接生产、也不是已装载 fallback，native BMS 普通短键/长条头/长条身/长条尾的 source-bound 加载只是当前窄例外。
- `LongNoteBody` 的 resource frames、resolved width 与解析后 `skin.ini` identity 必须进入同一个 source-bound material；发布后 renderer 不得再向 aggregate skin 查询宽度。selected body 坏声明不得与下层裸同名纹理或裸宽度拼件，只有下层自己的完整组件或 protected rescue 能接管。
- managed/default body 共用一个状态宿主，由真实 `DrawableBmsHoldNote` 驱动 Idle/Holding/Broken；active alpha `0.8`、broken alpha `0.32`，约 `80ms` tint/fade，HCN 才允许 regrab 回 Holding。异步 body 在状态已改变后到达时要立即投影当前状态，不得另造 gameplay state authority。
- 当前成功 preparation cache 仍按 `BmsLegacySkin` 实例复用，并非 revision-aware；同一实例原地改变来源时不会混合发布，但可能安全回退或继续使用旧 preparation，需重建 skin 实例才能取得新来源。这个缺口归 `SV1-2` 整包 revision/reload，不得把逐组件 A→B 描述成完整热重载。
- 当前未版本化 9K BMS/PMS per-lane raw token 实际为 `0..8`；V1 canonical `1..9` 必须做版本化迁移/冲突诊断，禁止静默双 alias。
- 当前 BGA skin display 接 raw timeline 并在 14K 建四个 player。V1 改成单一 engine-owned content session + 只读 viewport/proxy，多视图不得复制 decoder/clock authority。
- 三态使用平行 gameplay provider result，不直接改 nullable `ISkin` ABI；还要保留 beatmap-local skin 与 ruleset resource skin 的既有 authority。
- canonical `oms-simple` 自身失败是安装完整性故障，必须走明确修复路径；禁止偷偷落到另一套程序化颜色/节点。
- 脚本 VM 必须可抢占并有 instruction/heap/node/resource quota；回调返回后再看 stopwatch 无法阻止 `while true`。
- G1 external absolute path 使用 `NativeStorage`；删除/重命名先做 resolved-root containment，扫描不得删除不属于自身 authority 的 Realm 记录。
- 异常期代码只可定点参考，禁止整批恢复。
- lane keysound timeline 上界地雷见 [[reference_bms_lane_keysound_timeline_bounds]]。

## 下一入口

视觉验收采用集中签收：切片通过自动、合同、安全与回退 gate 后即可按依赖继续，待签收只能记为“实现／自动 gate 通过，视觉待验收”，不得计作交付、`SV1` 完成或 release gate 通过；只有视觉结论确实影响下一实现才暂停。当前 `V-001`～`V-004` 分别是 managed `.osk` BMS 普通短键、长条头、长条尾与长条身，均必须在 Skin V1/release 声明前由用户确认，且不得复用 2026-07-14 静态恢复验收。`LongNoteBody` 的素材/宽度同 revision、状态宿主与回退隔离已通过自动 gate，但仍只是逐组件窄纵切；后续入口以 P1-A STATUS/PLAN 为准，并保留上节 `SV1-2` preparation-cache 缺口。安全 G1、layout/BGA snapshot、shared ini compatibility、scene/event、sandbox、`oms-simple/oms-complex`/Authoring Kit/file fallback 均不得因该纵切提前计为完成；旧 F/G 编号只作历史索引，被撤回实现查 P1-A CHANGELOG/Git。
