# Skin C1 完成边界（2026-08-13）

> 本文只保留C1闭门的长期边界；下述进度是交付时历史快照。当前状态、工作门与精确测试结论只查[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)、[PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md)和[CHANGELOG](../subline/P1-A/CHANGELOG.md)。

## 结论

P1-A / Skin V1 七个campaign中的`C1`已通过真实caller/consumer、失败恢复、宽回归、Release、文档和独立终审，交付时为 **`1/7 closed，C2 active`**。C1只关闭作者文件工作区/G1 UX与ordinary `.osk` 安全导入；它不包含current consumer revision publication/reload/detach/retire。G1、`SV1-2`、`SV1-1`、Skin V1和release仍未完成，`V-001`～`V-004`仍为0/4。

## 产品价值复核

C1不是无caller的后端堆叠。真实链已经覆盖settings中的目录注册、显式选择/configured restart、Open、复制为managed、managed Rename/Delete、noncurrent external Unregister与journal支持；普通`.osk`仍从真实拖入导入进入选择和BMS/mania consumer。Windows held authority、exact-set、journal/recovery和RealmFileStore receipt的主要价值是保证external源永不被写、故障不留半包、错目标不被删、共享blob不被误清，而不是增加视觉表现力。

仓库仍保留一个C1前已有的internal fixed-staging import surface：它的StagedImport operation/handler没有独立非测试caller，但共同的fixed-slot authority/native move+inspection及journal/coordinator/recovery框架已被ManagedCopy复用，因此不计作额外用户功能，也不能把全部共同底层判成死代码。C1新增并列入完成态的主要能力均有production caller；selection/import/ManagedCopy等涉及包生效的链另有BMS/mania consumer证据，Open/Rename/Delete/Unregister/support则形成直接用户结果。当前原位修改不会立即进入active revision，完整reload/detach仍严格属于C2。

C1的实现密度也是明确维护风险：代码量不是进度指标。后续工作应从真实caller纵切出发，把revision/participant/lease做成小而封闭、可复用的协议并复用测试fixture，避免继续把生命周期复杂度集中进单一manager或复制超长场景测试；这不授权先造无caller framework，也不要求回头重构已验证的C1。

## 最终系统进度总览

`1/7 closed`只表示一个硬campaign通过，不应换算成14%的线性完成度；后续campaign不等权。数据恢复/导入安全与作者目录工作区已经过门，但最终用户可见Skin V1仍处早期：C2补当前全consumer revision生命周期，C3交付唯一layout，C4统一codec/catalog/三态与mania compatibility，C5交付scene/event和剩余slot，C6交付受限sandbox并关闭最终整包reload门，C7才交付canonical双包、Authoring Kit、validator、恢复与自动release。视觉签收、程序化`OmsSkin`退出和最终发行均未完成；详细矩阵见[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)。

## C1 已冻结的输入

1. Folder Skin Workspace只持有committed record ID与immutable label，每个action由manager fresh重读。external行只有Open / Import Managed Copy / Unregister，managed行只有Open / Rename / Delete，ordinary Realm `.osk`不进工作区。
2. external永久只读：service-owner只授权Realm记录，source bytes/collision只由fresh held no-follow physical proof授权。active instance只读capsule，原位变化不混入当前revision；random/next/previous不隐式选external。
3. external selection在慢capture期不持有coordinator，但持有managed authority、full registry snapshot与target package session到final callback。final时fresh重取selection lease，复核generation/generic mutation epoch/current pair、target/full registry declarations与physical proof，再原子发布fresh metadata。latest distinct request可提交，旧capture不得后到发布。
4. Rename/StagedImport/Delete/ManagedCopy从准入到final Realm线性化保持exact external set声明与physical proof。full ManagedCopy只接external record ID和用户明确direct-child；source bytes只来自capsule，目录来自同次manifest，不overwrite/merge/autosuffix，不自动选择。
5. canonical v3 journal使用封闭`(version, kind, phase)`图；pre-C1 v1/v2 strict frozen，不补字段或重解释。terminal只在exact Realm/held authority下compare-delete，删除后fresh inspection确认Missing才解冻。
6. ordinary `.osk` 仍是hash-backed Realm package。bounded reader在内容消费前验证archive metadata/type/path/size，流式阅读继续验证actual bytes/CRC/ratio/cancellation。transactional RealmFileStore receipt按same-hash participant group调节，精确处理record/blob非对称baseline，不伤共享blob。
7. scanner仍只在启动对账一次，不是watcher；thin/arbitrary-path stager、旧通用folder mutation、external源写改删仍冻结。

## C2 闭合时必须保持的边界

1. 以真实host证据冻结唯一可达的V1 reload触发方式与允许场景；路线审计不能作为产品终态。
2. ruleset-neutral immutable package revision、background prepare、update-thread publication barrier、participant registry、revision lease/detach receipt与retire queue必须形成真实纵切。
3. participant必须覆盖BMS geometry/playfield、Note/LN、pre-start preview、core/mania drawable及menu/shell/background/transition生命周期；不得把长寿命宿主留在协议外。
4. 任一prepare、validation或publication失败都保留exact旧pair/revision；旧owner只能在最后consumer/work lease detach后exactly-once retire。
5. ordinary Realm `.osk` 立即dispose旁路必须迁入统一协议或稳定禁用，不得遗留生命周期特例。
6. current external fallback+unregister只能在coherent fallback publication和旧revision全部detach后做pure-Realm compare-remove，仍不触碰source。
7. latest-wins、reentrant、首个不可逆边界前取消、shutdown exact claim/reap/join、失败保旧pair与脱敏诊断均须覆盖；`C3`～`C6`新增consumer各自同切加入，直到`C6`才关闭ini/manifest/scene/script/素材最终整包reload门。

这些边界已由C2闭合并固化到[P1-A技术约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)；当前实现与后续工作不得从本历史快照推断，仍只看P1-A四件套。
