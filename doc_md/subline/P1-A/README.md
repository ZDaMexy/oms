# P1-A：Skin V1、产品面与 release gate

P1-A 负责共享产品表面、Skin V1 架构、BMS playfield/BGA 皮肤边界和最终 release gate。当前先完成恢复后的实机/数据门，再按 `SV1-0`～`SV1-7` 建立共享 ini、布局 descriptor、外部 scene/event/script runtime 与安全 G1。

## 当前结论

- F1 静态素材/`skin.ini` 主链、程序化 fallback 与 Realm schema 56 保留。
- G1 生产扫描/删改/热重载、F2/Lua/reference-default 已撤回。
- 第一个完成版已重新定义为“引擎拥有 gameplay truth 与 playfield/BGA 布局，外部 package 拥有具体视觉/动画/只读事件响应”。
- mania/BMS 共享 neutral ini codec、scene/event ABI 和 sandbox；ruleset topology/adapter 分离。
- 下一步固定为：实机视觉 → schema 56 只读清点 → shared fixture → G1 路径安全合同 → layout/ini/scene/script 小切片。
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
