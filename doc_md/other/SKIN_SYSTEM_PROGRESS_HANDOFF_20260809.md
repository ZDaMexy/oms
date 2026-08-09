# Skin 系统产品进度交接：current managed atomic reload/detach

> 日期：2026-08-09
> 基线：`cf0019e8a79c0213074b0a4816884d410c3ea987`（审计开始时`HEAD == origin/master`且工作树干净）
> 性质：面向下一会话的派生说明，不替代[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)、[PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md)或[TECHNICAL_CONSTRAINTS](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)。2026-07-31 audit与2026-08-01/02 handoff只作为历史快照，不能覆盖当前代码事实。

## 结论

本轮对“current managed skin atomic reload/detach能否作为独立产品纵切”作出明确**NO-GO**。没有新增runtime、测试API或reload foundation。

现行PLAN把external registration/capture放在整包atomic reload/detach之前。managed路径只有在现有真实caller、host和renderer足以让本切独立闭合时才允许例外提前；只读追踪证明该条件不成立：仓库没有“重载当前managed revision”的production caller，同时没有覆盖全部consumer的coherent publication/detach协议。为了写红测而先创造caller或barrier也会越过本轮入口门，不能算产品级红测先行。

## 产品链证据

| 边界 | 当前代码事实 | 为什么不能闭合atomic reload/detach |
| --- | --- | --- |
| settings与其它入口 | settings dropdown只请求selection；启动配置、导入后展示与hotkey也只选择。same-value selection在准备前短路。startup scanner只做一次启动reconcile，不是watcher。filesystem-backed skin被editor、update import与external edit拒绝。 | 没有真实reload command、UI、watcher或manager API，无法定义用户动作、取消边界与允许场景。 |
| 看似可复用的reload | `ExternalEditOverlay`只为普通Realm skin重建实例，赋`CurrentSkin`后立即dispose旧实例；manager明确拒绝filesystem-backed skin进入该流程。 | 不是managed caller，也没有publication/detach barrier；即时dispose正是managed owner生命周期不能照搬的反例。 |
| manager publication | managed selection完成fresh Realm/path/owner/freeze/capture/factory后，只在manager内提交`CurrentSkinInfo`/`CurrentSkin` pair并广播`SourceChanged`。 | 没有package revision publication对象、consumer snapshot/registry、ack、detach receipt或旧instance retire queue；pair coherent不等于全renderer coherent。 |
| BMS playfield | `BmsPlayfield`在loader阶段读取一次`ISkinSource`并把geometry缓存到lane layout/profile，不监听`SourceChanged`。 | 即使Note/LN或shell换到新skin，layout仍可停在旧revision，直接形成mixed revision。 |
| BMS Note/LN | gameplay hitobject与pre-start speed preview分别创建独立`BmsAsyncNoteDrawable`；该类型只保证per-host异步generation/prepare/publish。 | 不存在package/playfield barrier，也没有host detach acknowledgement。 |
| core/mania/菜单 | `SkinReloadableDrawable`与core hitobject按drawable排scheduler；mania消费者混合同步、next-update和scheduler更新；菜单背景替换时旧背景会继续fade/expire并持有旧`Skin`。 | 同一`SourceChanged`存在多个可观察发布时间点，manager不知道全部旧consumer何时脱离。 |
| owner | exact capsule经factory转入skin owning store；`Skin.Dispose()`释放textures、samples、fallback store/capsule，BMS dispose还取消package note preparation。成功selection没有退役旧active skin的协议。 | 过早dispose可能让旧consumer引用失效；不dispose则旧owner生命周期无法闭合。现有产品测试只能手工dispose superseded managed skin。 |
| tests | 现有测试证明capsule/store ownership、guarded selection和per-host A→B，不证明same-ID revision gate、全host barrier、失败保留exact旧pair/owner、detach后dispose once或reload cancel/reentrant/latest-wins/shutdown join。 | 直接加此类测试必须先决定caller与consumer参与合同，不能在本轮偷偷发明产品路线。 |

## 已有能力可复用，但不足以成为reload foundation

- managed selection已有fresh authoritative Realm/path/owner/freeze/capture/factory复核、immutable capsule/new instance准备、generation/current pair、latest-wins/reentrant和shutdown ownership基础。
- `551a64af3bc2958db4baa57421b73fee61f259ac`闭合的typed startup/staged-import completion retry、generic mutation epoch fail-closed、update-thread non-blocking及exact claim/reap/join仍是未来强制回归。
- managed delete的journal/recovery、current fallback与detach后由durable状态收口是独立删除合同；它不提供reload的全consumer publication/detach barrier，也不得被横向复用成通用skin lifecycle事务。
- `SkinReloadableDrawable`、`BmsAsyncNoteDrawable`与`SourceChanged`都只是局部更新机制，不是整包原子reload。当前不可把这些部件重新命名为foundation来计进度。

## 重新开门前的产品决策

1. 冻结唯一真实触发方式：例如settings手动reload、受控watcher，或只允许安全导航点重载；不能同时留给实现猜测。
2. 冻结允许场景：是否允许live gameplay中发布；若不允许，必须定义明确的defer/reject边界与用户反馈。
3. 列出并冻结全部consumer参与模型，至少覆盖BMS playfield geometry、Note/LN gameplay、pre-start preview、ruleset/core/mania drawable、shell与菜单背景。
4. 定义单一revision publication barrier、每个consumer的attach/detach acknowledgement和old-owner retirement；任何consumer失败都必须保留exact旧revision，不能留下半数新、半数旧。
5. 再建立真实Windows`chartskin/<direct-child>`产品级/headless红测，覆盖阻塞consumer、preparation失败、same-ID revision、stale/reentrant/latest-wins、首个不可逆边界前取消、shutdown join、旧owner仅在全部detach后dispose once，以及脱敏诊断。

只有真实caller、全consumer publication/detach、失败保留旧实例和owner生命周期能在同一纵切闭合时才重新判GO。否则继续NO-GO，禁止manager-only reload API、强制同ID selection、逐组件`SourceChanged`拼接、即时dispose旧owner或无consumer DTO/barrier抽象。

## 本轮验证与下一入口

- 三路独立只读追踪对caller/renderer、consumer publication及capsule owner/tests得出一致NO-GO；没有发现可以独立闭合的production纵切。
- 没有runtime或测试文件变更，因此未运行focused/full、targeted formatter或Release。最近代码基线仍是2026-08-02记录的core managed **281/281**、Windows native delete **11/11**、managed selection/settings **62/62**、mania skin **182/182**、BMS full **1530/1530**、core skin **911/917**与mania full **827/831**同组既有失败、Release **0 error / 20 known warnings**。
- `CheckDocumentation.ps1`通过（131个Markdown、1030个相对链接、67个memory wiki链），仅有mainline PLAN数字比值的既有非失败提醒；`git diff --check`通过。独立终审blocker/major/moderate为 **0/0/0**，只补齐了本验证记录。
- external registration/capture仍排在atomic reload/detach之前；thin staged-import stager/caller同样保持NO-GO。下一会话不要把本审计写成reload实现、G1完成、Skin V1完成或release通过。
