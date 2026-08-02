# Skin V1 managed delete 产品纵切交接（2026-08-02）

> 本文是2026-08-02 managed delete切片的跨会话派生说明，不替代[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)、[PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md)或[TECHNICAL_CONSTRAINTS](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)。2026-08-01的[前一份交接](SKIN_SYSTEM_PROGRESS_HANDOFF_20260801.md)继续作为`551a64a`阶段历史快照。

## 结论

managed chartskin delete已从conditional GO闭合为独立玩家纵切。合法且由scanner exact-own的`chartskin/<direct-child>`可通过现有settings删除按钮和确认框触发；操作会物理删除该受管目录并hard-remove其exact authoritative Realm record。若目标是当前皮肤，任何物理步骤前必须先真实提交受保护程序化`OmsSkin`的coherent fallback pair。

这不是G1、`SV1-2`或Skin V1完成。rename与fixed-source staged import仍没有非测试caller；thin staged-import stager/caller仍为NO-GO；external、atomic reload/detach、scene/script、canonical `oms-simple/oms-complex`与Authoring Kit均未交付。`V-001`～`V-004`视觉签收仍为0/4。

## 玩家可见行为

- 现有皮肤设置删除按钮对eligible managed folder启用，不新增另一套UI。
- 点击确认后立即返回update thread；后台operation负责完整结果。成功时目录与对应Realm记录都消失。
- 删除当前managed skin前先切换到受保护`OmsSkin`；fallback无效、pair分裂或状态无法证明时拒绝删除。
- 普通已导入Realm `.osk`保持既有soft delete + default语义。protected/fixed、external、foreign/null owner、非法folder和其它不满足资格的记录保持既有行为或fail-closed。
- external未来只能解除注册，不允许删除外部原目录。rename/import仍没有玩家或制作者入口。

## 安全与恢复合同

- settings只传record ID；`CanDelete`和operation都从fresh Realm按ID重取authoritative资格，旧通用folder `CanModify/Delete`不解冻。
- Prepared journal在首个物理步骤前绑定held managed-root/source、operation-derived tombstone、exact existing-record fingerprint与有界、版本化的exact source-node manifest；update-thread fallback证明随后以同phase monotonic write固化`NotRequired`或`ProtectedPairCommitted`。首个物理步骤是held-root-relative source→tombstone no-replace detach；之后不再根据caller cancellation猜测结果，只由journal/recovery收口。
- Windows路径保持held-root、no-follow、完整identity/inventory验证，并以显式迭代walker限制depth/entry/path与pending handle预算。rename后的verification handles先释放，再用fresh no-follow delete-exclusive handles（持有DELETE、只共享READ）重捕；same-session live完整树仍须与manifest精确相等，release→exclusive窄窗内完成的node移出会在0次disposition时拒绝，只有fresh restart的partial survivor可接受manifest子集。exclusive tree取得后的已持有root/child relocation由sharing violation阻断；但目录handle不封namespace，final preflight后竞态新增/replacement不会进入held delete list或被删除，可能在exact manifest节点部分清理后令root删除失败，此时保留FilesystemApplied journal与Realm record并冻结。preflight前可见的foreign addition/replacement，以及reparse、hardlink、duplicate identity、metadata/tree漂移、source replacement或同级collision仍在0次disposition时拒绝；全程不跟随link或触及foreign/sibling/root。
- Realm只在durable FilesystemApplied后compare-remove journal绑定的exact record。current目标在detach前必须提交并证明exact coherent runtime pair，物理边界fresh复核exact protected fallback Realm record；live hard-remove与terminal recovery只对`ProtectedPairCommitted`继续要求该record，`NotRequired`非current恢复不创建或要求OMS record。首步前authority drift且receipt仍exact时安全RolledBack；receipt/写入漂移或其它物理/Realm证据歧义才保留journal冻结，缺exact durable phase/disposition/fallback证据时不得只凭“都不在”猜成成功。
- recovery只有在Prepared已经确认fallback disposition、物理状态为TargetOnly且Realm record仍exact时才允许前滚；Prepared+Neither即使disposition已确认也保持歧义。source/tombstone都不在的terminal前滚只属于FilesystemApplied/RealmApplied，且必须同时满足exact phase、fallback与Realm证据。
- selection/scanner/delete共用coordinator；delete是generic mutation，不获得startup retry authority。typed startup/staged-import retry、generic epoch fail-closed、fresh Realm/path/owner/freeze/capture/factory与latest-wins/reentrant继续是强制回归。
- fallback scheduler callback与shutdown恰一方claim/reap；worker TCS先完成，再发布可能重入shutdown的`SourceChanged`。update thread不等待，Realm释放前join全部真实worker。
- payload version保持v2，journal上限因最多8193个定长manifest节点由128 KiB提高为1 MiB；缺derived tombstone、exact existing-record fingerprint、source-node manifest，或physical phase缺fallback disposition的pre-product legacy-v1/旧v2 Delete intent按Invalid全局冻结，不猜测迁移。既有v2 Rename/StagedImport及legacy-v1非Delete terminal处理不变。

## 验证基线

- core managed mutation+contract broad：**281/281**（含真实Windows delete native **11/11**）。
- managed selection产品类：**62/62**。
- mania skin：**182/182**；mania full为**827/831**，四项既有`TestSceneAutoGeneration` replay-frame失败隔离复跑仍为4/4失败，与本切无文件交集。
- BMS full：**1530/1530**。
- core skin broad：**911/917**；仍是四项removed Osu archive fixture和两项default-skin旧假设。
- core full已执行，仍受已移除ruleset/fixture的既有广泛失败阻断；本切相关managed与skin子集没有新增失败。
- Release：**0 error / 20 known warnings**，为MessagePack `NU1902`与BMS tests既有`CS8600`/`CA2007`。
- 本切没有启动GUI或做新的视觉签收；真实settings caller由headless产品fixture覆盖，不冒充实机验收。

## 下一会话边界

1. 保留managed delete、`551a64a` startup retry、generic epoch、fresh authority、latest-wins/reentrant、update-thread非阻塞及shutdown exact-claim回归。
2. thin staged-import stager/caller维持NO-GO，除非可信external source→fixed provisional复制合同与真实caller能在同一切片闭合。
3. external registration/capture与atomic reload/detach分别重新go/no-go；每个新抽象必须指出同切production caller/host/renderer。
4. 不把managed delete扩张成任意path cleanup、external物理删除、通用Realm hard-delete、rename/import UI、scene/script或canonical包。
