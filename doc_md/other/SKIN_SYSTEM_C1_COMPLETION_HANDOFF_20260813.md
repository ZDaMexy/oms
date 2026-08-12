# Skin C1 完成交接与 C2 执行入口（2026-08-13）

> 本文固定C1闭门边界并提供C2持久执行prompt。当前精确测试结论只查[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)与[CHANGELOG](../subline/P1-A/CHANGELOG.md)；本文不复制数字，避免形成第二份current status。

## 结论

P1-A / Skin V1 七个campaign中的`C1`已通过真实caller/consumer、失败恢复、宽回归、Release、文档和独立终审，当前为 **`1/7 closed，C2 active`**。C1只关闭作者文件工作区/G1 UX与ordinary `.osk` 安全导入；它不包含current consumer revision publication/reload/detach/retire。G1、`SV1-2`、`SV1-1`、Skin V1和release仍未完成，`V-001`～`V-004`仍为0/4。

## 产品价值复核

C1不是无caller的后端堆叠。真实链已经覆盖settings中的目录注册、显式选择/configured restart、Open、复制为managed、managed Rename/Delete、noncurrent external Unregister与journal支持；普通`.osk`仍从真实拖入导入进入选择和BMS/mania consumer。Windows held authority、exact-set、journal/recovery和RealmFileStore receipt的主要价值是保证external源永不被写、故障不留半包、错目标不被删、共享blob不被误清，而不是增加视觉表现力。

仓库仍保留一个C1前已有的internal fixed-staging import surface：它的StagedImport operation/handler没有独立非测试caller，但共同的fixed-slot authority/native move+inspection及journal/coordinator/recovery框架已被ManagedCopy复用，因此不计作额外用户功能，也不能把全部共同底层判成死代码。C1新增并列入完成态的主要能力均有production caller；selection/import/ManagedCopy等涉及包生效的链另有BMS/mania consumer证据，Open/Rename/Delete/Unregister/support则形成直接用户结果。当前原位修改不会立即进入active revision，完整reload/detach仍严格属于C2。

C1的实现密度也是明确维护风险：代码量不是进度指标。C2应从真实caller纵切出发，把revision/participant/lease做成小而封闭、可复用的协议并复用测试fixture，避免继续把生命周期复杂度集中进单一manager或复制超长场景测试；这不授权先造无caller framework，也不要求本次交接回头重构已验证的C1。

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
> 候选触发至少用真实host比较Folder Skin Workspace/author-preview中的显式Reload与安全导航点的same-ID manual reload；明确live gameplay是允许、defer还是reject。若不允许live publication，必须实现确定的defer/reject边界与用户可见反馈，且不得先改变active pair。只有产品语义确需用户决定时才在本campaign内询问并等待，取得决定后继续实现；不得改成watcher，也不得以路线审计结束C2。
>
> 先从当前production object graph冻结完整participant/owner-reference inventory；BMS playfield/geometry、Note/LN、pre-start preview、core/mania/ruleset drawable/providing container、menu/shell/background/transition只是最低集合。任何跨update/frame/fade持有Skin、texture、sample、fallback store或capsule的宿主都必须入表，成为publication participant或持revision lease的holder，并以detach acknowledgement参与retire。实现background prepare + update-thread publication barrier + participant registry + revision lease/detach receipt + retire queue；以代码与真实host测试证明没有当前长寿命consumer留在协议外。
>
> 将inventory明确分成：(a) 必须coherent重建的视觉consumer，(b) 只持revision lease并在最后detach确认生命周期的holder，(c) 经代码与真实host证明不持有旧owner者。menu/shell/background/transition在这里是生命周期覆盖，不是开放菜单、选歌或结果页作者皮肤面；Skin V1的作者面仍限定在gameplay。冻结prepare开始、publication snapshot与commit期间的动态attach/detach语义：prepare中新增consumer不得漏过barrier或看到半revision，commit前detach须安全移出待ack集合，commit后late attach只能取得已提交current revision及对应lease。
>
> participant snapshot、target generation、current selection与exact source revision必须在prepare前后及commit barrier复核；所有可失败I/O/解析止于prepare，commit barrier不再执行可失败工作。所有participant ready前任何consumer都看不到新revision；commit后caller cancellation不得造成split，必须收敛。失败只dispose provisional owner exactly once且旧owner不动；成功后旧owner只在最后一个lease detach后retire exactly once。覆盖latest-wins、reentrant、取消、shutdown exact claim/reap/join、重试、中途失败与脱敏诊断。
>
> ordinary Realm `.osk`、managed、external三种source均必须走同一生命周期协议，且红测必须包含same-record-ID/content-revision reload，不得只测不同skin切换。定点审计`ExternalEditOverlay`、update-import及所有new-instance→CurrentSkin→immediate-dispose可达链；要么迁为真实caller/统一协议，要么连UI与backend稳定禁用并证明无reachable bypass。不得放宽C1对三种source各自的fresh authority/capture/Realm语义。
>
> 完成current external fallback+unregister：真实Workspace current external行级caller必须先发布受保护fallback/新revision并等待旧revision全部detach，再fresh compare exact service-owner/record/current revision做pure-Realm remove。prepare/publication/detach/Realm任一步失败都保留原record、exact旧pair/revision且source零变化；覆盖blocked/late participant、split pair、Realm failure、retry及source missing/drift仍不触源。程序化`OmsSkin`在C7前仍是受保护fallback authority。
>
> 现有current managed Delete也必须把受保护fallback publication与旧revision detach纳入C2协议；复用C1 record-ID authority、journal与物理mutation，不重做或削弱它们。fallback prepare/publication/detach失败必须在首个物理步骤前拒绝，不能先删目录再等待consumer收敛。
>
> 先写从真实production caller跨manager到真实renderer/owner的失败红测，至少覆盖`.osk`/managed/external三源、same-ID coherent A→B、delayed/failed participant、attach/detach与menu fade旧lease、任一失败保留旧pair、latest-wins/reentrant/cancel/shutdown、旧owner最后detach后exactly-once retire、current external unregister源目录不变；不接受DTO/mock-only终态。实现后跑focused、core Skin、mania relevant、BMS relevant/full与Release，稳定比对既有core fixture基线，按风险运行targeted formatter；同步P1-A四件套与路由、必要mainline、SKINNING/other索引和相关memory，运行`CheckDocumentation.ps1`与`git diff --check`，完成reachable bypass、participant inventory、并发、owner和测试独立终审并在当前分支创建有意义提交；不建分支/PR，push前取得用户确认。
>
> 明确排除C3+：P1-K/lane timeline、唯一layout、shared codec/catalog/resolver、scene/event、剩余slot、sandbox、canonical双包和Authoring Kit；也不删除程序化`OmsSkin`。不要在audit、产品决定、红测、foundation、单consumer或单提交处停下。只有真实触发、当前完整participant inventory、失败保旧revision、detach/retire、ordinary `.osk` 旁路和current external unregister、宽测试、Release、文档、独立终审与提交全部闭合后，才把状态更新为`2/7 closed，C3 active`并生成C3执行prompt。
