# Skin C1 完成交接与 C2 执行入口（2026-08-13）

> 本文固定C1闭门边界并提供C2持久执行prompt。当前精确测试结论只查[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)与[CHANGELOG](../subline/P1-A/CHANGELOG.md)；本文不复制数字，避免形成第二份current status。

## 结论

P1-A / Skin V1 七个campaign中的`C1`已通过真实caller/consumer、失败恢复、宽回归、Release、文档和独立终审，当前为 **`1/7 closed，C2 active`**。C1只关闭作者文件工作区/G1 UX与ordinary `.osk` 安全导入；它不包含current consumer revision publication/reload/detach/retire。G1、`SV1-2`、`SV1-1`、Skin V1和release仍未完成，`V-001`～`V-004`仍为0/4。

## C1 已冻结的输入

1. Folder Skin Workspace只持有committed record ID与immutable label，每个action由manager fresh重读。external行只有Open / Import Managed Copy / Unregister，managed行只有Open / Rename / Delete，ordinary Realm `.osk`不进工作区。
2. external永久只读：service-owner只授权Realm记录，source bytes/collision只由fresh held no-follow physical proof授权。active instance只读capsule，原位变化不混入当前revision；random/next/previous不隐式选external。
3. external selection在慢capture期不持有coordinator，但持有managed authority、full registry snapshot与target package session到final callback。final时fresh重取selection lease，复核generation/generic mutation epoch/current pair、target/full registry declarations与physical proof，再原子发布fresh metadata。latest distinct request可提交，旧capture不得后到发布。
4. Rename/StagedImport/Delete/ManagedCopy从准入到final Realm线性化保持exact external set声明与physical proof。full ManagedCopy只接external record ID和用户明确direct-child；source bytes只来自capsule，目录来自同次manifest，不overwrite/merge/autosuffix，不自动选择。
5. canonical v3 journal使用封闭`(version, kind, phase)`图；pre-C1 v1/v2 strict frozen，不补字段或重解释。terminal只在exact Realm/held authority下compare-delete，删除后fresh inspection确认Missing才解冻。
6. ordinary `.osk` 仍是hash-backed Realm package。bounded reader在内容消费前验证archive metadata/type/path/size，流式阅读继续验证actual bytes/CRC/ratio/cancellation。transactional RealmFileStore receipt按same-hash participant group调节，精确处理record/blob非对称baseline，不伤共享blob。
7. scanner仍只在启动对账一次，不是watcher；thin/arbitrary-path stager、旧通用folder mutation、external源写改删仍冻结。

## C2 必须一次闭合的产品门

1. 以真实host证据冻结唯一可达的V1 reload触发方式与允许场景，在同一campaign立即实现；路线审计不能作为C2终态。
2. 建立ruleset-neutral immutable package revision、background prepare、update-thread publication barrier、participant registry、revision lease/detach receipt与retire queue。
3. 当前participant必须全部覆盖：BMS geometry/playfield、Note/LN、pre-start preview；core/mania drawable；menu/shell/background/transition。不得把任一长寿命宿主留在协议外。
4. 任一prepare、validation或publication失败都保留exact旧pair/revision，新revision只能整体coherent发布；旧owner在最后一个consumer detach后dispose exactly once。
5. ordinary Realm `.osk` 实例的既有立即dispose旁路必须迁移到同一协议或稳定禁用，不得遗留生命周期特例。
6. current external fallback+unregister只能在coherent fallback/新revision发布且所有consumer detach后做pure-Realm compare-remove，仍不触碰source。
7. 覆盖latest-wins、reentrant、首个不可逆边界前取消、shutdown exact claim/reap/join、失败保留旧pair与脱敏诊断。`C3`～`C6`新增consumer各自同切加入，直到`C6`才关闭ini/manifest/scene/script/素材最终整包reload门。

## C2 明确排除

- 不重做C1 registry/capture/journal/Workspace/archive foundation，不改external永久只读与ordinary `.osk` hash-backed语义。
- 不以manager-only reload API、强制same-ID selection、逐组件`SourceChanged`、per-host reloadable或无consumer barrier/DTO作为终态。
- 不进入P1-K/layout/shared codec、scene/event/script/sandbox、canonical双包/Authoring Kit；这些分属`C3`～`C7`。
- 不提前删除程序化`OmsSkin`，不把C1闭门写成G1、`SV1-2`、Skin V1或release完成。

## C2 持久执行 prompt

> 继续 OMS P1-A / Skin V1 七个持久campaign的C2：当前consumer revision publication / reload / detach / retire。先严格按`AGENTS.md`读取mainline STATUS/PLAN、P1-A四件套、`doc_md/other/SKIN_SYSTEM_RECOVERY_20260710.md`和`doc_md/other/SKIN_SYSTEM_C1_COMPLETION_HANDOFF_20260813.md`，按任务关键词定点查OMS_COPILOT/CHANGELOG/memory。当前权威状态是`1/7 closed，C2 active`；C1作者工作区、external只读链、exact-set ManagedCopy/journal/recovery与ordinary `.osk` bounded ingress/receipt已闭合，不得重做或放宽它们。
>
> C2的产品结果是为当前存在的所有production consumer建立一个ruleset-neutral、immutable package revision协议，并以真实可达触发完成coherent reload、detach acknowledgement与owner retirement。先用代码/真实host证据冻结唯一触发方式和允许场景；若需产品选择，在同一campaign取得决定后立即实现，不把审计/路线文档作为C2终态。
>
> 实现background prepare + update-thread publication barrier + participant registry + revision lease/detach receipt + retire queue。同切覆盖BMS playfield/geometry、Note/LN、pre-start preview，core/mania drawable，menu/shell/background/transition。新revision只能在所有participant准备成功后一次coherent发布；任一准备/发布失败保留exact旧pair/revision，最后一个consumer detach后旧owner dispose exactly once。覆盖latest-wins、reentrant、取消、shutdown exact claim/reap/join、重试、中途失败与脱敏诊断。
>
> ordinary Realm `.osk`、managed、external三种source均必须走同一生命周期协议；把ordinary `.osk` new-instance后立即dispose的旧旁路迁移或稳定禁用。完成current external fallback+unregister：只在coherent fallback/新revision发布并且旧revision所有consumer detach后做pure-Realm compare-remove，任何时候都不写改删external source。不得以manager-only API、强制same-ID selection、逐组件`SourceChanged`、per-host reloadable或无consumer DTO/barrier冒充C2。
>
> 先写失败红测，至少覆盖`.osk`/managed/external三源、coherent A→B、任一participant失败保留旧pair、latest-wins/reentrant/cancel/shutdown、旧owner最后detach后exactly-once retire、current external unregister源目录不变。实现后跑focused、core/mania/BMS宽回归与Release，按风险运行targeted formatter；同步mainline/P1-A/SKINNING/memory，运行`CheckDocumentation.ps1`与`git diff --check`，完成独立产品/并发/owner/测试终审并在当前分支创建有意义提交；不建分支/PR，push前取得用户确认。
>
> 明确排除C3+：P1-K/lane timeline、唯一layout、shared codec/catalog/resolver、scene/event、剩余slot、sandbox、canonical双包和Authoring Kit；也不删除程序化`OmsSkin`。只有真实触发、当前全consumer、失败保旧revision、detach/retire、ordinary `.osk` 旁路和current external unregister、宽测试、Release、文档、独立终审与提交全部闭合后，才把状态更新为`2/7 closed，C3 active`并生成C3执行prompt。
