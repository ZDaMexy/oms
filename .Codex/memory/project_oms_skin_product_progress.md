---
name: project_oms_skin_product_progress
description: Skin V1 产品价值核算、用户 campaign 粒度决定与避免 foundation 冒充交付
metadata:
  node_type: memory
  type: project
---

# Skin V1 产品进度核算召回

当前完成项、燃尽与下一门只读 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md) / [PLAN](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md)；稳定行为见 [CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)，历次证据按 campaign 查 [CHANGELOG](../../doc_md/subline/P1-A/CHANGELOG.md)。本页不复制完成态和测试数字。

## 用户决定与核算方法

- 按“真实 caller → manager/backend → production host/renderer → 用户结果 → 失败回退/必要人工验收”核算，不按提交、DTO、fixture 或代码量计进度。
- capture、owner、coordinator、journal/recovery 保护真实用户数据，属于产品安全价值；与新增可见功能分栏，不能换算成 release-ready 百分比。
- 分开核算“已能表达的效果”“完整成品皮肤”“作者操作是否方便”和“真实观感已验收”。一个可导入的组合效果示例不等于完整复杂皮肤；能编辑文件并打包，也不等于完整创作套件已交付，更不意味着已有可视化编辑器。
- production 程序集中的 internal API 也可能没有非测试 caller。先查调用链；不得因底层复杂就一概删为无用，也不得因类型存在就横向扩展。
- 一个实际例子：fixed-staging import 没有独立非测试 caller；它的固定槽 move/inspection 与 recovery 被 ManagedCopy 复用。独立入口不计额外玩家功能，共同底层不能因此当死代码。
- 一个反例：keymode override 的 host/importer seam 不等于普通导入已有用户纠正 UI；拒绝模糊谱与用户修正流程分属不同交付。

## Campaign 粒度决定

用户在 2026-08-09 要求最多七个持久 campaign；`SV1-*` 只是能力/依赖分类，不代表会话轮数。完整 C1～C7 内容和退出门只维护于 P1-A PLAN。

- 同一 campaign 持续到真实 caller/consumer、失败回退、所需宽测试、文档及终审闭合；可跨多个提交和 compaction。
- 审计、NO-GO、路线决定、红测、DTO/foundation 或单个 caller 都不推进编号。需要产品决定时仍在原任务等待。
- 提前闭合可在同一任务进入下一 campaign，但用户明确结束本轮或留待新对话时必须停止开发；七个是上限，不是配额。
- C7 退出时，已约定 P1-A 范围的非人工代码/测试/工具/release 任务必须清零，只留集中视觉、真实设备和长时间体验签收。人工反馈产生的新缺陷按新证据修，不预先伪称不存在。
- “多推进”意味着闭合更完整的用户路径，不放宽数据保护或 owner 生命周期；也不要求保存既有抽象层数。

## 人工反馈与完成态误读

- 2026-09-12 用户实际体验星轨后认为不可用，明确指出动画、美术安排和精细度不符合预期。已有自动验证、安装恢复和制作证明不能推翻该体验结论，也不能继续把问题写成“仅待观察”。后续 simple/complex 调整属于原 C7 的用户反馈迭代，不重计七阶段，不凭这次反馈猜测用户尚未确认的风格、动效或默认选择方案；准确状态见 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)与 [C7 反馈原始结论](../../doc_md/other/SKIN_SYSTEM_C7_VALIDATION_20260909.md#2026-09-12-用户体验结论与本轮暂停)。
- 紧邻此前的“暂时没问题”只确认启动/预览问题的当时体验，不能扩张为 simple 美术签收、复杂款可用或任何原 V 项签收。原先的工程收口记录应保留为历史证据，但不能据此抹掉后续发现的产品问题。
- 用户本轮明确将两款调整留给新对话，只授权核对、文档与记忆、提交和推送收尾；不得顺势继续制作或修改游戏。此轮 push 授权不沿用到后续迭代，后续仍遵循 [AGENTS](../../AGENTS.md#工作流)。

## 容易混淆的边界

- canonical 默认包接管须满足 parity、完整性、原子恢复与实机 gate；程序化 `OmsSkin` 的保留/退出由合同控制，不能只因材料体系已接线就删掉。
- legacy beatmap-local direct visual compatibility 不等于开放新的 public sidecar authoring。
- runtime profile 的 NotApplicable 是明确版本化决定；不能拿 catalog 总数掩盖 ruleset 差异。
- scene 只控制表现；判定、输入、分数、clock、BGA 内容和资源 authority 仍在引擎。Snapshot/Reset 来自运行期 engine state，不能写成整包 prepare 已预产全部事件。
- 后续 consumer 复用同一 publication/lease；不重建第二套 layout、material、scene 或 event 权威。

相关地雷：[[reference_skin_atomic_reload_detach]]、[[reference_gameplay_skin_layout_snapshot]]、[[reference_gameplay_skin_codec_material]]、[[reference_gameplay_skin_event_envelope]]。
