# P1-A：Skin V1、产品面与 release gate

P1-A 负责共享产品表面、Skin V1 架构、BMS playfield/BGA 皮肤边界和最终 release gate。`SV1-0` 恢复/数据门已完成；`SV1-1` 已进入首个玩家可见纵切，但完整 V1 仍按 `SV1-0`～`SV1-7` 的 shared ini、布局、外部 scene/event/script runtime 与安全 G1 路线推进。

## 当前结论

- F1 静态素材/`skin.ini` 主链、当前程序化迁移 fallback 与 Realm schema 56 保留；selected managed `.osk` 的 BMS 普通短键编号帧动画是首个新增可见能力，自动 gate 已过、实机待确认。程序化视觉不是最终产品合同。
- G1 生产扫描/删改/热重载、F2/Lua/reference-default 已撤回。
- 第一个完成版已重新定义为“引擎拥有 gameplay truth 与 playfield/BGA 布局，外部 package 拥有具体视觉/动画/只读事件响应”。
- mania/BMS 共享 neutral ini codec、scene/event ABI 和 sandbox；ruleset topology/adapter 分离。
- V1 最终交付普通 `oms-simple.osk` 与 `oms-complex.osk`，两包均同含 mania/BMS；前者是只读 canonical fallback，后者证明公开 API 上限。
- 作者生态沿用 `.osk` + 根 `skin.ini` + 素材/动画命名 + 解包编辑/拖入导入，并交付两包源目录、模板、规范、validator/diagnostics 和打包说明。
- 当前检查点只剩新增普通短键动画的用户实机确认；本任务提交后停止，不提前把 G1、layout、ini、scene/script 或下一组件计为已开始。
- 恢复证据：[SKIN_SYSTEM_RECOVERY_20260710.md](../../other/SKIN_SYSTEM_RECOVERY_20260710.md)。
- V1 架构审计：[SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md](../../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)。

## 阅读顺序

1. [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)：当前能力、风险和下一检查点。
2. [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)：`SV1-0`～`SV1-7` 分期与验收。
3. [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)：共享/分离、fallback、layout、sandbox、路径与恢复红线。
4. [CHANGELOG.md](CHANGELOG.md)：按日期查询历史，不默认整篇加载。

## 归线关系

- `P1-C`：判定/反馈语义；P1-A 只拥有 HUD/skin 宿主边界。
- `P1-H`：提供文件系统存储经验；皮肤 managed/external authority 必须独立建模。
- `P1-G`：承接皮肤和共享产品面的人工验收结果。
- onboarding、settings-entry 等共享暴露面归 P1-A；页面复用其它子线功能时只建立链接，不复制其状态。
