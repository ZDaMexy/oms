---
name: project_oms_bms_skin_authoring
description: BMS 素材+ini 皮肤的当前可信面、稳定设计决议与实现地雷
metadata:
  node_type: memory
  type: project
---

# BMS 皮肤创作召回

权威当前态：[P1-A STATUS](doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)；计划/约束：[P1-A PLAN](doc_md/subline/P1-A/DEVELOPMENT_PLAN.md)、[CONSTRAINTS](doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)；恢复证据：[[reference_skin_recovery_20260710]]。

## 当前可信面

- F1：独立 `[Bms]` parser、`BmsLegacySkin` 配置源、`.osk` 导入路由、现存静态件的颜色/纹理/几何、reference ini 自校验。
- `OmsSkin` 是不可删除的逐组件最终兜底。
- G1 只保留 folder ctor 与 `SkinInfo.FilesystemStoragePath/IsExternalFilesystemStorage` + schema 56 载体；没有扫描/选择/删改/热重载。
- F2/F3/G2、Lua、mania fallback adapter、reference-default 均未落地。
- 恢复修正：base parser 前重置配置流；14K 第二皿使用 `S2`/P2 素材。

## 稳定产品决议

1. 皮肤范围以 gameplay 为主；不移植 LR2/beatoraja runtime，只对齐元素族。
2. OMS 方言为 mania-aligned 静态项 + `[Bms]` 扩展；按 keymode 分桶。
3. 程序化 fallback + reference 素材皮肤；不做“代码绘制→PNG”导出器。
4. 加载 fail-open，未知键/缺素材诊断但不阻断游玩；编辑器可更严格。
5. ini 控制长相；lazer layout editor 只摆 `ISerialisableDrawable` HUD，二者正交。

## 实现地雷

- core `LegacySkin` 不得编译依赖 BMS ruleset；BMS 配置留在 ruleset，通过精确反射类型接入。类型匹配必须排除 `LegacyBeatmapSkin` 等其它子类。
- schema 由代码/真实组件确立，`SKINNING.md` 是派生说明，不能反向驱动实现。
- 贴图优先；无贴图才使用 ini colour/palette。composite 化后测试要读内层 visual，不读容器自身 colour。
- lane 宽经总相对宽归一化；同比缩放所有 lane relative width 无效。几何细节见 [[reference_bms_default_skin_geometry]]。
- `HitTargetVerticalOffset` 保持 0 以守住时间/滚动合同。
- G1 external absolute path 使用 `NativeStorage`；删除/重命名先做 resolved-root containment，扫描不得删除不属于自身 authority 的 Realm 记录。
- 异常期代码只可定点参考，禁止整批恢复。

## 下一入口

先完成实机视觉和 schema 56 只读清点；之后按“路径模型→安全删改→扫描/选择→热重载→F2”推进。历史刀序、旧测试数字与被撤回实现统一查 P1-A CHANGELOG/Git。
