# Skin C1 意外中断 checkpoint 与续接入口（2026-08-12）

> **已取代**：C1已于2026-08-13完成，当前状态为`1/7 closed，C2 active`。本文仅作中断时历史取证，不得再执行其C1 prompt；请改用[C1完成交接与C2执行prompt](SKIN_SYSTEM_C1_COMPLETION_HANDOFF_20260813.md)。

> 本文是未完成campaign的恢复入口，不是完成报告。权威当前状态仍由[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)、[PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md)与[TECHNICAL CONSTRAINTS](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)共同决定。

## 结论

2026-08-09启动的P1-A / Skin V1 `C1`对话发生意外中断。代码已越过foundation阶段并形成真实产品checkpoint，但完整退出门尚未通过：**`0/7 closed，C1 active`**。不得把本文、当前工作树或focused测试描述为C1、G1、`SV1-2`、Skin V1或release完成；不得生成C2实施prompt或提前开始reload/detach。

当前分支仍为`master`，基线HEAD与`origin/master`均为`f3ec69c51a1816aa0a26edd59269e5696e416f7f`。C1实现、测试、文档均在未提交工作树；恢复者必须先保全并审计该工作树，禁止reset、checkout覆盖、整包cherry-pick或从历史归档重做。

## 已落地的产品链

1. **Folder Skin Workspace**：真实settings subsection与folder picker；external行只提供Open Folder / Import Managed Copy / Unregister，managed行只提供Open Folder / Rename Folder / Delete，ordinary Realm `.osk`不进入列表。UI仅复制record ID、immutable label、kind和capability hint，不持有`Skin`/`Live<SkinInfo>`/path/authority proof；action由manager fresh重读。
2. **external只读工作区**：resolver-issued external request、Windows逐段no-follow held authority、physical ancestry proof、bounded exact capsule与logical manifest同捕获；注册在factory成功后发布versioned service-owned Realm记录，不自动选择。external可显式dropdown选择和configured restart，random/next/previous不选；active实例只读capsule。production BMS Note/LN head/body/tail与legacy mania note/hold均由同一external capsule真实渲染。
3. **pure-Realm Unregister**：只对coherent noncurrent pair生效，事务内fresh compare-remove exact service-owner记录，不解析/capture/写改删source；source missing/drift仍可解除陈旧记录，current/split、shutdown/reentry与unresolved journal拒绝，不dispose prior owner。
4. **exact external registry切换**：有界exact declaration set、deterministic generation与full digest、held physical sessions贯穿managed mutation；Rename/StagedImport/Delete/ManagedCopy在final Realm事务复核同一snapshot。合法非重叠external不再全局阻断；unresolved/foreign/null/overlap/generation/identity drift继续fail-closed。pre-C1 recovery无held authority时仍要求empty set。
5. **single canonical v3 journal**：v3冻结external generation/digest/disposition，新增ManagedCopy及Copying/ProvisionalReady且不重排旧enum数值。v1/v2 strict dispatch、schema、phase写回和recovery语义保持；不创建新v2，不给旧version加optional字段。startup recovery继续早于scanner。
6. **full Import Managed Copy**：唯一入口为manager-owned `ImportManagedCopyAsync(externalRecordId, targetChildName)`；manager生成operation ID与staging path。source bytes只来自fresh capsule，directory/name/kind/empty-directory来自paired manifest；destination只用handle-relative no-follow/no-replace primitive。首个provisional root/byte前durable写入并exact reload同一v3 intent；首写前取消可exact rollback，首写后由journal/recovery收口；ProvisionalReady exact后在同一intent move/publish，不自动选择。
7. **journal支持面**：动态只读snapshot、稳定reason与脱敏bundle；只有fresh inspection唯一证明既有handler可安全forward/rollback时显示manager-owned async Retry。无方向选择、删journal、force-unfreeze或raw journal/path/ID/identity/native正文输出。
8. **managed Workspace Open/Rename/Delete**：Open只导航fresh exact目录；Rename只改direct-child与同record path，不改`skin.ini`、Name、Creator、hash或revision；row Delete与existing current按钮共用detached ID确认语义、fresh `CanDelete(Guid)`、`DeleteSkinAsync(Guid)`、fallback/journal/recovery。dialog后current↔noncurrent、split及各字段/external generation漂移均fresh重判。
9. **ordinary `.osk` archive safety**：SkinImporter使用专用pre-open raw limit/bounded spool与自解析EOCD/central directory；entry/name/type/declared预算早于`Filenames`、`GetStream`、hash、`Files.Add`与model。actual byte、CRC、ratio/aggregate与cancellation继续复核。`InstantiationInfo`只走closed compatibility mapping。opt-in RealmFileStore import receipt在fault/cancel时只清理本次新增且仍零引用的record/blob，保留共享hash；成功仍为hash-backed Realm package并success-only删除源。

## 已验证的checkpoint

- `dotnet build osu.Game/osu.Game.csproj --no-restore -p:UseSharedCompilation=false -m:1 -verbosity:minimal`：**0 error**，仅9个既有MessagePack `NU1902`。
- core smoke filter（`SkinArchiveReaderTest`、`ImportSkinTest`、`SkinManagedFolderMutationJournalTest`、`SkinManagedFolderManagedCopyRecoveryTest`及6个exact-set admission/final drift）：**152/152**。
- BMS/Workspace产品smoke（`FolderSkinWorkspaceUiTest`、完整ManagedCopy Journey2、managed row、external Workspace、dynamic support及ordinary current-delete回归）：**34/34**。
- 完整Journey2已真实走完：register external → explicit target ManagedCopy且不自动选择 → managed BMS/mania render → configured restart render → Open exact path → Rename且metadata/skin.ini不变 → renamed restart render → external noncurrent Unregister → managed current Delete；前后external physical proof digest、inventory与bytes一致，journal Missing。
- external Journey1已覆盖register/workspace/dropdown/explicit select/configured restart、同capsuleBMS+mania真实renderer、显式切走、noncurrent Unregister及source proof/inventory/bytes不变；missing/drift、current/split、implicit selector、reentry/shutdown有独立测试。
- P1-A四件套、受影响mainline、作者手册、本交接与相关memory已完成中断同步；`CheckDocumentation.ps1`通过（135个Markdown、1064个相对链接、74个memory wiki链，仅PLAN数字比值复核提醒）。恢复后若代码或测试结论变化，最终提交前仍须再次同步并重跑。
- `git diff --check`在收口前后均无内容错误；Windows仅提示若Git下次触碰将LF转CRLF。

注意：组合产品smoke第一次出现Journey2 `Single(kind == Managed)`假红，因为同一fixture的Realm可能保留多个managed记录；测试已改为按唯一`chartskin/<targetChildName>`定位，单测与34项组合均转绿。这不是production故障。

## 尚未关闭的硬门

1. 未按原C1要求跑core skin、mania relevant/full、BMS relevant/full、真实Windows capture/delete/journal宽组合；历史基线中的已知失败也尚未在当前diff上重新归因。
2. 未跑`osu.Desktop.slnf` Release，未确认警告仍只为既有集合。
3. 未运行所有改动工程的targeted formatter/verify。
4. 未完成独立产品、安全、并发/recovery与测试终审；中断前的分工审计不能代替最终共享diff终审。
5. 文档/memory已做中断同步，但恢复者仍须在最终代码稳定后再次更新准确测试数字、风险、author-facing说明，并运行完整`CheckDocumentation.ps1`。
6. 当前工作树尚未提交；恢复者必须在所有门通过后于当前`master`提交，不能建branch/PR，push前仍需用户确认。
7. mapped/SUBST/shadow等依赖真实机器配置的环境级验证主要由adapter合同/确定性fake覆盖；最终终审须决定现有证据是否足够，不能未经审查宣称真机矩阵完整。

## 恢复顺序

1. 按`AGENTS.md`读取mainline、P1-A四件套、恢复审计、本文及相关memory；确认HEAD/branch/dirty tree与本文一致。
2. 先运行`git diff --check`和Debug build；再分别复跑本文两组精确smoke，避免一开始用full噪声掩盖checkpoint漂移。
3. 审查`SkinManager.cs`、journal/recovery、external registry/capture、Windows native writer、Workspace UI、SkinArchiveReader/RealmFileStore receipt及新增tests的共享diff；不要恢复旧global external gate，不要把v2改写成v3，也不要让UI持有path/Live/Skin。
4. 仅修真实回归或退出门缺口；不要进入C2、watcher、same-ID reload、current external unregister、layout/shared codec、scene/script、canonical包。
5. 跑原prompt要求的宽回归、Release与独立四向终审；同步最终docs/memory，执行`CheckDocumentation.ps1`与`git diff --check`。
6. 只有全部通过才把C1更新为`1/7 closed`、C2 active并生成C2 prompt；否则保持`0/7 closed，C1 active`并继续本campaign。

## 继续工作的完整prompt

将本文末尾以下prompt整体交给新对话；它仍是C1续接，不是C2：

> 继续 OMS P1-A / Skin V1 七个campaign中的同一个C1。上次对话意外中断，不得重做已落地foundation，也不得进入C2。先严格按`AGENTS.md`读取mainline、P1-A四件套、`doc_md/other/SKIN_SYSTEM_RECOVERY_20260710.md`、`doc_md/other/SKIN_SYSTEM_C1_INTERRUPTED_HANDOFF_20260812.md`与其中路由的memory。确认当前在`master`，HEAD/origin预期仍为`f3ec69c51a1816aa0a26edd59269e5696e416f7f`，并保全包含C1实现/测试/文档的dirty tree。
>
> 当前状态严格为`0/7 closed，C1 active`。已落地并在focused smoke验证的产品链包括Folder Skin Workspace、external strict read-only capture/registry/select/configured restart/pure-Realm noncurrent unregister、exact-set Rename/StagedImport/Delete/ManagedCopy、single canonical v3 journal与v1/v2兼容、manager-owned full ManagedCopy/recovery、managed Open/Rename/Delete与dynamic redacted support、ordinary `.osk` bounded early archive gate和exact RealmFileStore rollback receipt；两条BMS+mania真实产品旅程已贯通。不要把这些写成C1完成，因为宽回归、Release、独立终审与最终提交仍缺。
>
> 第一阶段只做恢复审计：`git status`/diff-check、Debug build、本文记录的152项core smoke与34项BMS/Workspace smoke。若失败，只修可证回归；特别保留Journey2按唯一`chartskin/<target>`定位的隔离修复。随后对共享diff做独立产品、安全、并发/recovery、测试覆盖四向终审，重点检查external exact-set从proof到final Realm线性化、ManagedCopy首字节前durable owner与partial cleanup、v1/v2 strict compatibility、journal support只读/脱敏、UI detached ID语义、archive pre-open/central-directory/actual-byte与file-store零残留。
>
> 第二阶段只补C1退出门：core skin及managed mutation/capture/journal宽组合、mania relevant/full、BMS relevant/full、ordinary importer及共享RealmArchiveModelImporter影响面、真实Windows矩阵，随后`dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m`。对已知历史失败逐项与2026-08-02基线比较，禁止把removed Osu/Taiko/Catch fixture或既有mania replay frame失败误算新回归，也禁止未经本次重跑沿用旧数字。
>
> 第三阶段同步P1-A PLAN/STATUS/CHANGELOG/TECH四件套、受影响mainline、中断handoff的最终结果、`doc_md/other/SKINNING.md`作者说明与相关memory；运行targeted formatter/verify、`powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\CheckDocumentation.ps1`及`git diff --check`。所有门绿后才在当前`master`创建有意义提交；不开branch/PR，push前重新取得用户确认。
>
> 明确排除：current external unregister、watcher/same-ID reload/force reload、consumer publication/detach/retire foundation（归C2）、P1-K/layout/shared codec、scene/script/sandbox、canonical包/Authoring Kit、任意path/thin stager、merge/overwrite/autosuffix，以及external源任何写改删。若任一C1门无法安全闭合，保持`0/7 closed，C1 active`并在同一对话继续；不得生成C2 prompt。只有产品旅程、失败恢复、宽测试、Release、文档、独立终审和提交全部完成，才更新为`1/7 closed`、C2 active，并生成覆盖整个C2的持久prompt。
