# Skin V1 产品进度、开发价值与跨会话交接（2026-08-01）

> 本文以 runtime 提交 `551a64af3bc2958db4baa57421b73fee61f259ac` 为证据基线，记录产品层判断和下一会话边界；不替代 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)、[PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md) 或 [TECHNICAL_CONSTRAINTS](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)。[2026-07-31 审计](SKIN_SYSTEM_PROGRESS_AUDIT_20260731.md) 只代表 `c53f1e0` 时点，其中“启动竞态尚未修复”已不是当前事实。

## 结论

本轮不是无意义开发，但价值类型必须说准：它没有新增一个玩家可见按钮或皮肤元素，而是闭合了真实玩家链上“已配置的 managed skin 在启动后仍可靠生效”的确定性竞态。native capture、immutable capsule、scanner、selection coordinator、Realm 复核与 shutdown ownership 在这条生产链中都有实际 consumer，是文件来源可信、当前皮肤一致性和用户数据安全的必要成本。

同时，Skin V1 已到必须停止横向扩 foundation 的位置。directory-only rename、fixed-source staged import 及若干 topology/event/capability 合同目前没有玩家 caller 或完整 production host；这些代码可作为后续安全后端和设计种子保留，但不能计作已交付功能，也不得在没有同切 consumer 时继续扩张。

若排期必须量化，最终玩家可见且可发布的 Skin V1 仍约 **25%～30%**。本轮提高的是已有 `SV1-2` 窄链的可靠性，没有扩大可见能力宽度，因此不人为上调比例；该区间不是 release gate。

## 当前产品可达性

| 层级 | 当前事实 | 产品判断 |
| --- | --- | --- |
| 玩家真实可达 | `.osk` 拖入/启动参数导入与现有 settings 选择；已选包的 BMS 普通 Note、LN head/body/tail 静态图和连续编号帧进入真实渲染链 | 有真实玩家价值；四个可见项自动门已过，但 `V-001`～`V-004` 人工视觉仍为 **0/4** |
| 玩家真实可达 | 合法 `chartskin/<direct-child>` 手工工作目录可在下次启动被 scanner 注册并从现有 dropdown 选择；已配置的 managed skin 现在可跨 startup scanner 争用可靠恢复 | 是受管作者工作区的窄闭环；仍不是 watcher、热重载或完整管理 UI |
| production 安全生命周期 | resolver、held-root no-follow capture、capsule、scanner、selection、typed coordinator、journal/recovery、取消与 shutdown join | 直接支撑上述玩家链和未来受管 mutation；属于正确性/数据安全，不等于新增交互 |
| production 后端、无 caller | directory-only rename、fixed-source staged import、delete protected fallback pair gate | 不能称玩家已能 rename/import/delete；staged import 也没有 external→fixed provisional production stager |
| 合同/fixture 种子 | topology/config candidate、event envelope/order、capability negotiation 等 | 没有完整 host/renderer/manifest/sandbox consumer，不计产品进度且暂不再横向扩张 |

真实渲染链见 [DrawableBmsHitObject.cs](../../osu.Game.Rulesets.Bms/UI/DrawableBmsHitObject.cs)、[BmsSkinTransformer.cs](../../osu.Game.Rulesets.Bms/Skinning/BmsSkinTransformer.cs) 与 [BmsManagedPackageNoteProvider.cs](../../osu.Game.Rulesets.Bms/Skinning/BmsManagedPackageNoteProvider.cs)；启动选择、scanner 和设置入口见 [OsuGame.cs](../../osu.Game/OsuGame.cs)、[SkinManager.cs](../../osu.Game/Skinning/SkinManager.cs)、[SkinManagedFolderScanner.cs](../../osu.Game/Skinning/SkinManagedFolderScanner.cs) 与 [SkinSection.cs](../../osu.Game/Overlays/Settings/Sections/SkinSection.cs)。

## 本轮竞态闭合的产品意义

启动会先按配置发起 managed capture，随后才在后台执行 recovery→scanner。旧实现若让 capture completion 撞上 scanner 持有的共享 coordinator，会把有效 configured selection 当作普通 mutation 争用一次性拒绝，慢 capture、大目录或多包扫描因此可能让玩家继续看到旧 `OmsSkin` pair。

新的确定性 headless 产品交错先证明了该失败，再将 startup 与 staged import 变为可辨识的 typed holder：configured selection 只在 exact startup/staged completion 后异步 fresh retry；真实 generic mutation 继续 fail-closed。retry 不等待 update thread，并重新验证 generation、authoritative Realm record、path、owner、freeze、factory/capsule、current pair 与 latest-wins/reentrant 状态；generic mutation epoch 在任一边界跨越仍拒绝。shutdown会封门并在Realm释放前join capture-scheduling/contention worker；pending queued completion则由shutdown或scheduler callback恰好一方claim/reap，晚到callback只no-op，不等待update scheduler。

这证明的是产品生命周期正确性，不是 GUI 或视觉签收。后续进度应按“玩家能完成的新操作、真实渲染面和人工 gate”统计，而不是按测试数、internal 类型数或 production 程序集代码量统计。

## 距最终 Skin V1 的实现总览

最终预期不是“更多 `.osk` 字段”，而是一条完整产品链：

1. 安全管理 `.osk`、managed workspace 与 external 只读来源，支持可恢复 CRUD、选择、原子 reload/detach 和清晰诊断。
2. 引擎唯一拥有 gameplay truth、validated layout/BGA snapshot、时序、LN 裁剪和安全边界；mania/BMS 共享 neutral codec、resolver 与发布生命周期，各自只保留 topology adapter。
3. 所需 gameplay slot 具备 `Provide/Inherit/Suppress` 合同，缺失/坏资源稳定回落到只读 canonical `oms-simple.osk`。
4. declarative scene/event host 提供稳定 manifest、节点、binding、动画和只读事件；optional sandbox VM 有确定性、配额、熔断和零网络/任意文件能力。
5. `oms-simple.osk` 与 `oms-complex.osk` 走普通公开链证明 fallback 与作者上限，配套 Authoring Kit、validator/diagnostics；parity、完整性、恢复及实机 gate 后程序化 `OmsSkin` 才退出产品渲染链。

| 目标门 | 当前状态 | 主要缺口 |
| --- | --- | --- |
| `SV1-0` 恢复与数据安全 | **完成** | 保持历史边界，不重开全局 cleanup |
| `SV1-1` 共同组件与可见纵切 | **窄纵切自动门完成** | 仅 BMS Note/LN 四组件；视觉 0/4，其余 slot、完整三态与 shared 消费未交付 |
| `SV1-2` G1 存储/选择/reload | **managed 发现/选择子集稳定，整体进行中** | rename/import 无 caller；缺 managed delete、external、atomic reload/detach、完整 UI/诊断和实机矩阵 |
| `SV1-3` 唯一 layout/BGA descriptor | **未交付** | topology 种子不含统一 validated geometry；playfield/HUD/BGA 尚未共享唯一 snapshot |
| `SV1-4` shared mania/BMS codec | **未交付** | presence/provenance/candidate 有种子，尚无统一 production codec/resolver 与完整 consumer |
| `SV1-5` scene/event runtime | **未交付** | 无完整 payload publisher、manifest、scene host 和 production 生命周期 |
| `SV1-6` sandbox script | **未交付** | 无 VM、授权存储、预算、determinism 与熔断 |
| `SV1-7` canonical 双包/Authoring Kit | **未开始产品落地** | 无真实双包、可编辑源、validator/作者套件；程序化 `OmsSkin` 仍在链底 |
| release/人工门 | **未通过** | managed/external、重启/切换/删除/reload、全 keymode、性能、双包与视觉实机均待验收 |

## 下一产品决策

- **managed delete：conditional GO。** 现有 settings delete dialog 是最短真实 caller，但 filesystem-backed skin 仍被通用 `CanModify/Delete` 禁止。下一切只能在同一纵切接通独立 `CanDelete`、async caller、held-root 物理删除、Realm 收敛、durable journal/recovery、current protected fallback、取消/shutdown和脱敏诊断；不能只再增加 primitive。
- **thin staged-import stager/caller：NO-GO。** external→fixed provisional 可信复制、no-follow source、预算、取消、清理、隐私和真实 caller 不能在当前最小切片一起冻结，继续扩它只会增加无 consumer foundation。
- managed delete之后，最短价值链转向 external registration/capture 与整包 atomic reload/detach，再进入 shared runtime、canonical 双包和作者工具；每个新增抽象都必须指出同切 production caller/host/renderer。

## 验证基线

runtime 提交 `551a64a` 已通过 coordinator **11/11**、startup lifecycle **2/2**、managed selection 产品类 **52/52**、core managed **275/275**、mania skin **182/182**、BMS full **1520/1520**；core skin broad **863/869** 的六项失败与既有基线完全一致。Release 为 **0 error / 20 known warnings**，targeted formatter通过。本文的交接收口只改文档与 memory，不重复运行产品测试或 Release，也没有启动 GUI 或新增视觉签收；`CheckDocumentation.ps1` 通过（128个Markdown、1013个相对链接、58个memory wiki链），仅保留mainline PLAN数字比值的既有非失败提醒，`git diff --check`通过。

## 新会话继续边界

新会话先按 `AGENTS.md` 读取 mainline、P1-A 四件套、恢复审计、本文及 managed selection/scanner/mutation memory；以 fetch 后的 `origin/master` 为基线，并确认包含 `551a64a`。只推进 managed delete 独立产品纵切：先追踪真实 settings caller 并做确定性产品级/headless 红测；若不能同切闭合真实 caller、物理/Realm/journal/recovery，则明确 NO-GO 并停止。不得借机实现 thin stager、任意路径 import、external、reload/detach、scene/script、canonical 包或新的横向 foundation；仍在当前分支提交，不建分支/PR，push 前重新确认。
