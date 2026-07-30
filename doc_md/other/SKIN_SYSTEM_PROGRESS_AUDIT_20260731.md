# Skin V1 产品进度与价值审计（2026-07-31）

> 本文是基于 `c53f1e08d88a023a56267bbeb5802d6cc9bfc080` 的一次性证据与交接总览，不替代 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)、[PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md) 或 [TECHNICAL_CONSTRAINTS](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)。后续状态变化只更新权威四件套。

## 结论

当前工作不是无意义开发：已导入 `.osk` 的 BMS Note/LN 素材和手工放入 `chartskin/` 后的启动发现/选择都能进入真实玩家链；managed capture、immutable capsule、scanner、selection、journal/recovery与shutdown也已进入production生命周期。文件系统identity、reparse/hardlink、崩溃恢复和Realm一致性是用户数据安全问题，不是测试装饰。

但必须停止把“production程序集内存在”写成“玩家已获得功能”。directory-only rename与fixed-source staged import目前没有非测试caller，仓库没有external source→fixed provisional slot的production stager，也没有UI；它们是可复用的安全后端，不是已交付导入/重命名。若继续扩张没有同切或紧随切片production consumer的shared合同或mutation foundation，边际价值会转为过度工程。

## 产品可达性分层

| 层级 | 当前事实 | 产品含义 |
| --- | --- | --- |
| 玩家真实可达 | `.osk`导入/选择；BMS普通短键与LN head/body/tail的selected-package静态图/编号帧；手工放入合法`chartskin/<name>`后重启发现并从既有dropdown选择 | 有真实用户价值，但四个可见组件仍需`V-001`～`V-004`人工签收；managed目录也不是热发现/热重载 |
| production已装配、无产品caller | directory-only rename、fixed-source staged import、delete protected fallback pair门、durable recovery | rename/import后端实现了可执行并可恢复的物理/Realm操作，delete目前只有保护门；玩家当前均无法触发，staged import也没有上游stager |
| 局部production消费 | semantic slot、scalar geometry resolver等 | Note/LN纵切已有消费；其余slot和完整layout仍未形成统一产品面 |
| 合同/fixture地基 | lane topology publication、config candidate、event envelope/order、capability negotiation等 | 可以保留作为设计种子，但尚无完整production host/renderer/manifest/sandbox；不能计作玩家功能 |

真实渲染链证据在 [DrawableBmsHitObject.cs](../../osu.Game.Rulesets.Bms/UI/DrawableBmsHitObject.cs)、[BmsAsyncNoteDrawable.cs](../../osu.Game.Rulesets.Bms/UI/BmsAsyncNoteDrawable.cs)、[BmsSkinTransformer.cs](../../osu.Game.Rulesets.Bms/Skinning/BmsSkinTransformer.cs) 与 [BmsManagedPackageNoteProvider.cs](../../osu.Game.Rulesets.Bms/Skinning/BmsManagedPackageNoteProvider.cs)。managed生命周期入口在 [OsuGame.cs](../../osu.Game/OsuGame.cs)、[SkinManager.cs](../../osu.Game/Skinning/SkinManager.cs) 与 [SkinManagedFolderScanner.cs](../../osu.Game/Skinning/SkinManagedFolderScanner.cs)。

## 新发现的启动风险

configured managed skin会在`OsuGame.load()`先启动异步capture，startup scanner随后在worker上从recovery到完整discovery/reconcile持有同一coordinator。selection准备完成后的final boundary当前只对staged-import特例等待/重试；若占用者是startup scanner，则以`ManagedFolderOperationInProgress`一次性拒绝。

这使大目录或慢capture下的启动选择存在可达竞态。现有测试分别证明无争用configured selection和scanner lifecycle，没有覆盖二者交错。风险定为 **Major**，下一切片先确定性复现并修复：

- 不在update thread阻塞；
- 不把真实rename/import/delete争用改成无条件等待；
- scanner完成后仍重新验证generation、authoritative Realm、path/owner、freeze与prepared结果；
- 补慢/多包scan场景，记录耗时与包数，避免scanner持锁成本失控。

## 距最终 Skin V1 的门状态

| 目标门 | 当前状态 | 离最终预期的关键缺口 |
| --- | --- | --- |
| `SV1-0` 恢复与数据安全 | **完成** | 结论保持，不重开全局cleanup |
| `SV1-1` 共同组件与可见纵切 | **窄纵切自动门完成** | 只有BMS Note/LN四组件；`V-001`～`V-004`为0/4，其余slot、完整三态和真实beatmap-local未交付 |
| `SV1-2` G1存储/选择/reload | **managed子集进入中段** | 启动发现/选择可达；rename/import无caller；缺delete、production stager/UI、external resolved capture、watch/reload、全consumer detach与实机矩阵 |
| `SV1-3` 唯一layout/BGA descriptor | **未交付** | topology种子不含geometry；playfield、HUD、BGA仍未消费同一validated snapshot，14K仍有多player临时实现 |
| `SV1-4` shared mania/BMS codec | **未交付** | presence/provenance与候选fixture存在，但mania生产renderer仍走旧transformer，没有统一codec/resolver |
| `SV1-5` scene/event runtime | **未交付** | 只有envelope/order种子；无payload family、publisher、manifest、scene host或生产生命周期 |
| `SV1-6` sandbox script | **未交付** | 只有pure capability decision种子；无VM、授权存储、预算、determinism或熔断 |
| `SV1-7` canonical双包/Authoring Kit | **未开始产品落地** | 无真实`oms-simple.osk`/`oms-complex.osk`、可编辑源、validator与作者套件；程序化`OmsSkin`仍是protected链底 |
| release/人工门 | **未通过** | managed/external、重启/切换、rename/import/delete、reload、全keymode、性能及双包实机均待验收 |

不把阶段做算术平均。若仅为排期被迫量化，当前“最终玩家可见且可发布的Skin V1”约在 **25%～30%**：安全/存储地基显著前进，但作者runtime、统一layout/codec、canonical双包和绝大多数人工release gate尚未形成。这个区间不是release gate，也不得用于宣称接近完成。

## 后续最短价值路径

1. 先修configured managed selection↔startup scanner竞态，守住已经玩家可达的选择链。
2. 竞态闭合后做一次产品go/no-go：在thin staged-import stager/caller与managed delete之间选择最短可见纵切；不再新增无consumer的foundation。
3. 闭合managed delete、external registration/capture与整包atomic reload/detach，并补最小产品入口、脱敏诊断和人工矩阵。
4. 转入唯一layout/shared codec/scene host；只有真实consumer存在时才扩event/capability合同。
5. 制作并以普通公开链验证`oms-simple.osk`/`oms-complex.osk`与Authoring Kit；canonical完整性/原子恢复/mania+BMS parity过门后，程序化`OmsSkin`退出产品渲染链。

本次审计没有修改runtime、运行GUI或新增人工签收，也没有重跑产品测试/Release；实现基线的完整自动验证仍以 [P1-A 2026-07-29 CHANGELOG](../subline/P1-A/CHANGELOG.md) 为准。
