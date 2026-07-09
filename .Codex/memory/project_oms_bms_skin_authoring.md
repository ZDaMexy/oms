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

- F1：独立 `[Bms]` parser、`BmsLegacySkin` 配置源、`.osk` 导入路由、现存静态件的颜色/纹理/几何、reference ini 自校验。
- `OmsSkin` 是不可删除的逐组件最终兜底。
- G1 只保留 folder ctor 与 `SkinInfo.FilesystemStoragePath/IsExternalFilesystemStorage` + schema 56 载体；没有扫描/选择/删改/热重载。
- F2/F3/G2、Lua、mania fallback adapter、reference-default 均未落地。
- 恢复修正：base parser 前重置配置流；14K 第二皿使用 `S2`/P2 素材。

## 稳定产品决议

1. 皮肤范围以 gameplay 为主；不移植 LR2/beatoraja runtime，只对齐元素族。
2. OMS 方言为 mania-aligned 静态项 + `[Bms]` 扩展；按 keymode 分桶。
3. mania 普通 `.osk` 是固定 C# 行为宿主 + legacy 素材/参数，不是通用作者脚本上限。V1 共享 neutral codec/scene/event/reload/sandbox，mania 与 BMS 保留各自 topology adapter；采用 adapter-first 迁移。
4. 引擎拥有 gameplay truth、playfield/BGA layout、滚动/LN 裁剪、对象池、BGA 内容时钟和安全边界；外部 package 拥有具体 scene、动画与只读事件响应。
5. 三态为 `Provide/Inherit/Suppress`；仅 lane/scratch 可辨识、note/LN/mine、判定位置和启用中的 cover 几何不可 suppress。judgement display、combo、gauge visual、HUD、BGA frame、按键/命中特效均可关闭。
6. 程序化 `OmsSkin` 收敛为不可删除 minimal rescue；OMS 文件型默认和第三方同权。发布必须用公共 API 同时通过 Minimal 与 Showcase。
7. 5K/7K 覆盖 P1/P2/CenterP1/CenterP2，9K BMS/PMS 居中，14K 双 deck/双皿/centre gap；所有 playfield/HUD/BGA/scene 消费一个 layout snapshot。
8. lazer layout editor 只管理既有 `ISerialisableDrawable` HUD；新 scene manifest 使用稳定 allowlisted node ID，不能复用序列化 CLR `Type` 的 editor JSON。

## 实现地雷

- core `LegacySkin` 不得编译依赖 BMS ruleset；BMS 配置留在 ruleset，通过精确反射类型接入。类型匹配必须排除 `LegacyBeatmapSkin` 等其它子类。
- schema 由代码/真实组件确立，`SKINNING.md` 是派生说明，不能反向驱动实现。
- 贴图优先；无贴图才使用 ini colour/palette。composite 化后测试要读内层 visual，不读容器自身 colour。
- lane 宽经总相对宽归一化；同比缩放所有 lane relative width 无效。几何细节见 [[reference_bms_default_skin_geometry]]。
- `HitTargetVerticalOffset` 保持 0 以守住时间/滚动合同。
- geometry 当前缺 finite/正值/范围校验；playfield 读 skin profile，但 gauge/combo 会另建默认 profile、BGA 固定 rect。先建唯一 snapshot，再扩表现。
- legacy mania 缺 `Keys:` bucket 时会合成默认 configuration；neutral model 必须保留 explicit presence，不能把合成默认误判为 `Provide`。
- `[Mania]` 兼容同时考虑 full visual-lane bucket（6/8/9/16）与 key-only bucket（5/7/9/14，scratch `Inherit`）；14K 可支持显式双 `Keys:8` deck。
- 当前 BGA skin display 接 raw timeline 并在 14K 建四个 player。V1 改成单一 engine-owned content session + 只读 viewport/proxy，多视图不得复制 decoder/clock authority。
- 三态使用平行 gameplay provider result，不直接改 nullable `ISkin` ABI；还要保留 beatmap-local skin 与 ruleset resource skin 的既有 authority。
- 脚本 VM 必须可抢占并有 instruction/heap/node/resource quota；回调返回后再看 stopwatch 无法阻止 `while true`。
- G1 external absolute path 使用 `NativeStorage`；删除/重命名先做 resolved-root containment，扫描不得删除不属于自身 authority 的 Realm 记录。
- 异常期代码只可定点参考，禁止整批恢复。
- lane keysound timeline 上界地雷见 [[reference_bms_lane_keysound_timeline_bounds]]。

## 下一入口

按 P1-A `SV1-0..7` 推进：恢复/数据 gate → neutral contract/fixture → 安全 G1 → layout/BGA snapshot → shared ini compatibility → scene/event → sandbox → Minimal/Showcase/file-default。旧 F/G 编号仅作历史索引；被撤回实现查 P1-A CHANGELOG/Git。
