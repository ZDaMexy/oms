# P1-A 当前状态：产品面、release gate 与皮肤边界

> 最后更新：2026-07-10
> 全局状态见 [../../mainline/DEVELOPMENT_STATUS.md](../../mainline/DEVELOPMENT_STATUS.md)，恢复证据见 [SKIN_SYSTEM_RECOVERY_20260710.md](../../other/SKIN_SYSTEM_RECOVERY_20260710.md)。

## 当前阶段

皮肤异常代码已撤回并恢复到可信 F1/schema 56 基线。自动回归完成，当前阻塞是用户实机视觉验收与生产 Realm 的只读清点；两者完成前不进入 G1 生产实现，也不恢复 F2/Lua/reference-default。

## 当前能力

| 面 | 状态 | 当前事实 |
| --- | --- | --- |
| 共享 onboarding | 已落地 | 六步 OMS flow：欢迎、UI 缩放、获取谱面、导入、难度表、按键绑定 |
| settings 产品裁剪 | 已落地 | 隐藏不属于 OMS 产品面的上游 tablet/touch/mouse subsection，不删除底层 runtime |
| BMS→mania 公开表面 | 进行中 | 可玩性、converted star、spread display 已接通；显式 wording/更宽人工证明待做 |
| BMS HUD 宿主 | 稳定 | `IBmsHudLayoutDisplay` 保持 wrapped HUD + gauge + combo 三件套；不得破坏签名 |
| gauge/combo 产品面 | 已落地 | 矩形 gauge 位于判定线下；combo 居 playfield 中心；已有实机确认 |
| tri-mode/pre-start | 已落地基线 | Normal/Floating/Classic 与 pre-start hold/operator surface 已接通；完整 FHS 不得宣称 |
| 皮肤 F1 | 可信主面保留 | `.osk` + `[Bms]` parser/config source；现存静态件颜色/纹理/几何；reference ini 自校验 |
| 程序化 fallback | 已保留 | `OmsSkin` 是最终兜底，用户皮肤缺件逐组件回落 |
| G1 可视文件夹 | 重新设计中 | 仅 folder ctor + `SkinInfo` 两字段/schema 56 载体；没有生产扫描/选择/删改/热重载 |
| F2/F3/G2/Lua | 未开始 | 异常期实现不计入当前能力 |

## 恢复时保留的两个修正

1. `BmsLegacySkin` 复制配置流后，在 base legacy parser 前把位置重置为 0，保证 `[General]/[Colours]/[Mania]` 共存解析。
2. per-lane image key 支持 `S2`；14K 第二皿映射到 P2 素材，第一皿保持 `S`/P1。

## 最近验证

| 检查 | 结果 |
| --- | --- |
| H1/H2 `BmsLegacySkinTest` | 15/15 |
| BMS 全量 | 1005/1005 |
| mania 默认 OMS 资源 | 1/1 |
| mania 全量 | 787/791；4 项既有 HoldNote auto-frame 失配 |
| core skin focused | 57/62；5 项 Argon/已删 ruleset 旧测试失配 |
| Release | 0 error / 20 warnings |

命令、归因和归档位置见 [CHANGELOG.md](CHANGELOG.md) 2026-07-10；本页不保留更旧测试数字。

## 当前风险

- schema 56 生产 Realm 可能含异常期写入的 folder-backed 记录；未经备份和只读报告，不得清理。
- external absolute path、删除/重命名 containment、扫描 authority 和原子热重载都没有可信生产实现。
- parser/类型测试不能证明真实 `SkinManager`、ruleset fallback 或事件驱动视觉。
- mania 默认资源必须与 BMS gate 同时通过，禁止用 BMS reference 替换全局 `OmsSkin`。

## 下一检查点

1. 用户完成无外部皮肤、`.osk`、5K/7K/9K/14K 和双皿素材视觉验收。
2. 只读列出 schema 56 中 folder-backed `SkinInfo` 及对应目录存在性，不做修复。
3. 在 PLAN 的恢复顺序下重做 G1 路径模型与安全删改测试。
4. G1 稳定后再评估 F2；每个动态件必须有真实事件、fallback、mania 不回归和实机证明。
