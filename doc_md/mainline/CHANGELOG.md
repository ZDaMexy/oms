# OMS 更新日志

> 本文件记录每次验证通过的变更摘要，按时间倒序排列。
> 当前开发进度与遗留问题见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)；分步规划见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)。

---

## 2026-08-13

### Skin V1 C1作者文件工作区闭门

P1-A七个campaign的`C1`已闭合。Folder Skin Workspace现以record-ID fresh authority管理external Open / Import Managed Copy / Unregister与managed Open / Rename / Delete；external继续永久只读，选择/configured restart由fresh held no-follow capture产生immutable capsule+manifest，service-owner只管Realm记录。Rename/StagedImport/Delete/ManagedCopy持有exact external registry物理证明至final Realm线性化，single-v3 journal按封闭`(version, kind, phase)`图恢复，terminal compare-delete后仅在fresh Missing可证时解冻。ordinary `.osk` 新增skin-scoped bounded archive reader与transactional RealmFileStore receipt；并发same-hash participant、分组fault重试、record/blob非对称baseline均保持exact rollback且不伤共享blob。

终审收口将Workspace records/support只读worker纳入manager shutdown cancel+同步join，并让managed Open在held capture前后都fresh复核normalized path唯一；因此UI关闭、应用退出及旧row同路径重复声明均fail-closed。

最终验证：`osu.Game` Debug build **0 error**/仅9个既有MessagePack `NU1902`；core C1 focused **490/490**，archive/receipt **84/84**，BMS产品组合 **118/118**，mania Skin **182/182**，BMS full **1586/1586**。core Skin **679/683**，4项均为已移除Osu ruleset mode 0 fixture基线；`osu.Desktop.slnf` Release **0 error**/仅9个既有MessagePack `NU1902`。external与receipt独立最终复审均为blocker/major/moderate **0/0/0**。燃尽更新为`1/7 closed，C2 active`；current consumer revision publication/reload/detach/retire、G1、`SV1-2`、Skin V1、release与`V-001`～`V-004`仍未完成。完成边界与C2执行prompt见[C1完成交接](../other/SKIN_SYSTEM_C1_COMPLETION_HANDOFF_20260813.md)。

## 2026-08-12

### Skin V1 C1意外中断最小checkpoint

同一`C1`对话已在未提交工作树形成Folder Skin Workspace、external只读capture/registry/select/restart/pure-Realm noncurrent unregister、exact-set managed mutation、single-v3 ManagedCopy、managed Open/Rename/Delete、动态脱敏journal支持及ordinary `.osk` bounded ingress的真实产品checkpoint。停止扩功能后仅做确定性收口：`osu.Game` Debug build 0 error，core smoke **152/152**、BMS/Workspace产品smoke **34/34**；组合旅程的唯一测试隔离假红已按exact target record定位修正。P1-A/mainline/作者手册/交接与memory同步后，`CheckDocumentation.ps1`通过（135个Markdown、1064个相对链接、74个memory wiki链，仅PLAN数字比值复核提醒），`git diff --check`无内容错误。宽回归、Release、独立终审、targeted formatter与提交尚未执行，因此严格保持`0/7 closed，C1 active`；完整事实、缺口与续接prompt见[C1中断交接](../other/SKIN_SYSTEM_C1_INTERRUPTED_HANDOFF_20260812.md)及[P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)。

## 2026-08-09

### Skin V1 产品价值、最终差距与后续工作包收口

三路只读审计按真实caller→manager/backend→renderer→用户结果重新核算Skin投入：已导入`.osk`的BMS Note/LN、managed目录重启发现/选择及settings物理删除是玩家可达能力；immutable capsule、`551a`启动协调和delete journal/recovery直接保护这些入口与玩家文件，具有真实安全价值。directory-only rename与fixed-source staged import的专属部分仍无caller/stager/UI，只能算潜在后端；external、atomic reload、layout/shared codec、scene/script、canonical双包与Authoring Kit均不得计入已交付。按release-ready产品能力只能概括为**约三成**，工程/安全地基约半数且显著高于玩家完成度；两者不是gate，也没有线性剩余工期含义。

下一高价值候选改为external只读作者工作区的完整settings→resolved-identity/no-follow capture→Realm注册→dropdown选择/配置重启→真实renderer→只解除注册纵切；没有完整caller和consumer链不得先建foundation。current managed atomic reload/detach因无production caller、全consumer publication/detach和owner退役协议保持NO-GO；thin staged-import stager/caller同样NO-GO。该优先级只改变后续产品工作包，不改变现有release gate或2026-08-02代码验证基线；完整分阶段差距与门槛见[2026-08-09交接](../other/SKIN_SYSTEM_PROGRESS_HANDOFF_20260809.md)及[P1-A当前状态](../subline/P1-A/DEVELOPMENT_STATUS.md)。

同日根据用户对协作粒度的纠偏，`SV1-*`明确只作能力taxonomy，剩余自动工作另设`C1`～`C7`最多七个持久新对话campaign：作者工作区/G1 UX与archive安全、当前consumer reload/detach、P1-K+layout、shared codec/catalog/resolver、scene/event及剩余slot production、sandbox并关闭最终整包reload门、canonical/Authoring Kit/自动release。未闭合campaign不得生成下一handoff prompt；audit、产品路线决定、NO-GO、DTO/foundation或单个提交不能消耗编号。第七个退出时只留人工签收，详见[P1-A PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md)。

规划变更后的全层级复审进一步消除了旧的“external子门完成后立即进入reload”路由：external只占`C1`首个顺序子门，随后必须在同一campaign关闭full managed-copy stager/import、managed rename/delete与journal支持UX、普通`.osk` archive安全，`C1`整体通过后才进入`C2`。`C2`只关闭当前production consumer协议，`C3`～`C6`新增consumer逐次加入，最终`ini/manifest/scene/script/素材`整包reload与G1自动门到`C6`关闭；该澄清不改变任何runtime或既有验证基线。

## 2026-08-01

### Skin V1 启动可靠性与产品价值收口

configured managed selection与startup scanner的Major竞态已在`551a64af3bc2958db4baa57421b73fee61f259ac`闭合：真实配置恢复可在typed startup/staged completion后后台fresh retry，update thread不阻塞，generic mutation继续fail-closed。产品复核确认本切保护现有玩家链但不增加可见功能，最终可发布Skin V1仍约25%～30%；后续停止扩张无consumer foundation，managed delete为下一conditional GO，thin staged-import stager/caller维持NO-GO。完整价值分层、最终目标差距和跨会话边界见 [2026-08-01 Skin V1产品进度与交接](../other/SKIN_SYSTEM_PROGRESS_HANDOFF_20260801.md) 与 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md)。

## 2026-07-31

### Skin V1 产品可达性与交接审计

以已推送的`c53f1e08d88a023a56267bbeb5802d6cc9bfc080`为runtime基线完成只读产品审计：`.osk` BMS Note/LN与手工放入`chartskin/`后的启动发现/选择确认进入真实玩家链；rename/staged import确认只有production-assembled internal后端，没有非测试caller、external→provisional stager或UI。新发现configured managed selection与startup scanner coordinator争用可能一次性拒绝有效启动选择，列为下一刀Major风险；后续禁止用无production consumer的shared合同或foundation冒充产品进度。未改runtime、未重跑产品测试/Release、未运行GUI或新增视觉签收；详细门状态与证据见 [2026-07-31 Skin V1产品进度审计](../other/SKIN_SYSTEM_PROGRESS_AUDIT_20260731.md) 和 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md)。

## 2026-07-29

### `SV1-2` managed chartskin staged import 闭合

`SV1-2` 已闭合internal production staged import：只消费OMS持有的固定`skin-mutation-staging/{operationId:N}` provisional副本，upstream stager保留外部原来源；held no-follow完整capture以durable content revision + full physical-tree fingerprint固定Prepared，再同卷identity-preserving no-replace move到既存managed authority root，并由exact one-shot publisher合法交接scanner owner。current recovery严格逐阶段write + exact reload`FilesystemApplied → RealmApplied → Committed`且publisher不早于durable filesystem phase，fixed-ID journal拒绝；普通scanner不消费plan，import不自动选择、无关pending selection继续，操作按kind复用既有coordinator/journal/recovery并统一shutdown。focused **265/265**、BMS selection产品类 **36/36**、core skin **856/862**（6项既有基线）、mania **182/182**、BMS full **1504/1504**、Release **0 error / 20 known warnings**，终审blocker/major/moderate **0/0/0**。未启动GUI或操控桌面，视觉与实机gate仍未完成；UI、managed delete、external、reload与atomic detach继续冻结，下一优先级为managed delete。详见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md)。

### `SV1-2` directory-only managed chartskin rename 闭合

`SV1-2` 已闭合directory-only managed chartskin rename：只移动`chartskin/<direct-child>`工作目录并更新同一Realm record path，作者展示与包内容不变；durable phase、identity-aware recovery、selection/scanner/shutdown gate及真实Windows held-root-relative no-replace门均已覆盖。真实NTFS的descendant release→move→recapture窄窗口不是filesystem transaction，歧义由journal与路径冻结收口。focused **195/195**、BMS full **1497/1497**、Release **0 error / 20 known warnings**，独立审查blocker/major/minor **0/0/0**；未操控GUI。UI、staged import、delete、external和reload仍冻结，下一优先级为staged import。详见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md)。

## 2026-07-27

### `SV1-2` managed mutation authority/recovery公共地基闭合

R3现已补齐scanner/selection/mutation共享线性化、held existing/staged source与root-bound target slot、strict versioned durable journal、启动先恢复后scanner、歧义冻结及current delete程序化`OmsSkin` protected pair确认；split pair与任何无法确认状态都拒绝。本切没有物理/Realm写primitive或UI，下一门仍是rename，再按staged import、delete、external和atomic reload/detach独立过门；focused **107/107**、BMS选择/fallback **24/24**、core skin **337/341**（4项既有removed archive）、mania **182/182**、BMS full **1492/1492**，Release **0 error / 18 known warnings**，文档/diff门通过，独立审查blocker/major **0/0**，全程未操控GUI。详见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md)。

## 2026-07-26

### 皮肤系统文档与 memory authority 同步

- 只读复核当前代码、mainline/P1-A四件套、恢复审计、制作者说明、视觉清单与相关memory；产品代码基线保持`bd40966`，本轮未修改runtime/生产数据，未重跑产品测试或Release，也未启动GUI或操控桌面。
- 两份STATUS移除已被最新scanner基线取代的factory/preflight/旧测试数字与“scanner尚未实现”等逐刀历史；当前只保留schema 57 exact-owner启动发现→native capture→immutable capsule→guarded selection窄链及其边界。产品语言区分“已导入`.osk`（Realm/hash-backed包）”与“`chartskin`受管目录（folder-backed）”，自动发现明确为只加入选择面、不自动选中。
- R3计划收敛为mutation authority/recovery foundation → rename → staged import → delete → external → atomic reload/detach。filesystem/Realm跨域写入必须先有durable journal、启动幂等恢复及scanner/selection/mutation共享线性化；rename/import语义与staged import新记录发布authority未冻结前不开放写面。删除current skin必须等待已验证protected fallback真正提交，迁移期为程序化`OmsSkin`、canonical接管后为`oms-simple.osk`，失败则拒绝删除。
- 集中视觉清单不再复制会漂移的自动测试数字或“本条所在提交”锚点；实际验收时填写真实commit/build，`V-001`～`V-004`仍待统一反馈。memory索引按恢复→清点→preflight→capsule→capture→scanner→selection→authoring重排，并补齐scanner与未来mutation协调、普通delete异步调度不可冒充fallback提交等地雷。
- 文档门禁：`CheckDocumentation.ps1`通过（125个Markdown、978个相对链接、48个memory wiki链），`git diff --check`通过。该结论只验证文档治理，不改变任何产品自动/人工gate。

## 2026-07-17

### `SV1-2` schema 57 scanner owner与managed启动自动发现闭合

Realm现以nullable opaque owner区分scanner归属，旧/null/foreign记录零claim；Windows启动scanner从held `chartskin` authority handle完成Observed/Valid分离、逐包immutable capture与最终稳定inventory复验，只在完整scan的单一事务内维护exact-own记录。合法direct child重启后可自动进入选肤面，坏包/文件/reparse仍observed而不触发误删；根缺失、竞态、取消或异常零提交，退出先cancel+join再释放Realm。focused为schema/scanner **12/12**、native fake+真实 **55/55**、生命周期 **2/2**、production选择 **15/15**；扩大回归core相关 **222/222**、mania skin **182/182**、BMS full **1483/1483**，三工程format verify通过，Release **0 error / 20 emitted known warnings**，独立终审blocker/major **0/0**。全程未操控GUI，视觉仍0/4。本切不是watcher/热重载，也未实现专用managed mutation、external或atomic reload/detach。详见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md)。

### `SV1-2` managed Windows native capture 内部安全门闭合

R3 现已把 resolver-issued managed `chartskin/<name>` request 接到 strict physical NT volume、fixed-handle/handle-relative no-follow capture，再交给 immutable capsule；全节点 identity/metadata、hardlink/alias/reparse、busy writer、预算、读取/枚举竞态与最终 inventory/authority-link复验均有typed fail-closed合同，成功返回前不遗留live handle或stream。capture focused **47/47**（真实Windows 11/11、0 skipped）、三项内部合同合并 **167/167**，core skin **224/229**（5项既有失败）、mania **182/182**、BMS **583/583**，Release Rebuild **0 error / 11 known warnings**；全程未操控GUI。该能力仅是managed内部producer，当前无`SkinManager`/production managed folder factory/选择消费方，也不含external capture、scanner/mutation或原子reload；下一门转为production managed folder factory/选择，G1、Skin V1及`V-001`～`V-004`状态均未因此完成。详见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md)。

### managed `.osk` BMS 长条 body 纵切自动门通过，工程转入 `SV1-2`

selected managed package 现可为 critical `LongNoteBody` 提供 `NoteImage{lane}L`/`SL`/`S2L` 静态图与 60 FPS 连续编号帧；`LongNoteBodyWidth` 由唯一安全标量策略解析，只接受 finite 且 `0 < width <= 1`，无声明或非法值逐字段回到 `0.5775` 并保留稳定拒绝原因。body 素材与解析后宽度绑定同一精确 package revision 后一起发布，坏资源不会与低层裸同名素材/宽度拼件；managed/default body 共用真实 Idle/Holding/Broken 状态宿主与 80ms 过渡，未复制或改写 `DrawableBmsHoldNote` 的 gameplay 状态权威、拉伸/裁剪及 LN/CN/HCN 语义。产品 fixture **94/94** 连续三轮、合并 focused **326/326**、BMS full **1456/1456**、Release **0 error / 11 known warnings**；全程未开窗或操控 GUI。新视觉登记为 `V-004`，至此普通短键与 LN head/body/tail 四个可见组件的自动、合同、安全与回退 gate 闭合，但 `V-001`～`V-004` 视觉签收仍为 **0/4**，不得称 `SV1-1`、Skin V1 或 release 完成。

R2 至此只关闭进入 G1 所需的前置合同和首个 Note/LN 纵切自动闭环，工程下一优先级转为 R3/`SV1-2` 的 G1 安全存储与整包原子重载；完整 layout/shared codec、所需 slot 三态与 scene/event/script runtime 仍归 R4，不是启动 `SV1-2` 的前置。已知同一 `BmsLegacySkin` 实例的成功 preparation cache 尚不感知 revision：包原位变化时会安全维持旧结果或回落，不会混发，但该陈旧风险必须在 `SV1-2` 的 package revision/原子重载中消除。详见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md) 与[集中视觉清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)。

## 2026-07-16

### managed `.osk` BMS 长条尾纵切自动门通过

selected managed package 现可为 optional `LongNoteTail` 提供 `NoteImage{lane}T`/`ST`/`S2T` 静态图与 60 FPS 连续编号帧；真实 hold tail 走后台 preparation 与 per-host 原子发布。未声明或坏 tail 逐组件继承并最终保持程序化透明迁移 fallback，这不是作者 `Suppress`；低层仅有同名裸纹理时不会拼件，但低层自己的完整 tail 组件仍可接管。protected `OmsSkin` tail 保持 `Alpha=0` 且不反查 aggregate 纹理，body/head、LN/CN/HCN 与 22.5px tail host 均未改。产品 fixture **60/60** 连续三轮、合并 focused **271/271**、BMS full **1401/1401**、Release **0 error / 11 known warnings**，独立终审 blocker/major **0/0**。新视觉登记为 `V-003` 集中待验收；下一切片冻结为统一标量几何策略下的 critical `LongNoteBody`。详见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md) 与[集中视觉清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)。

### managed `.osk` BMS 长条头纵切自动门通过

selected managed package 现可为 critical `LongNoteHead` 提供 `NoteImage{lane}H`/`SH`/`S2H` 静态图与 60 FPS 连续编号帧；真实 hold head 走后台 preparation 与 per-host 原子发布，坏声明回落到可见 rescue，不从低层同名裸纹理拼件，也不拖垮同包有效 ordinary note。产品 fixture **39/39**、合并 focused **248/248**、BMS full **1378/1378**、Release **0 error / 11 known warnings**；未改 body/tail、LN/CN/HCN、shared/mania authority、layout/G1/scene/script。新视觉登记为 `V-002` 集中待验收，不计作交付；下一切片冻结为 optional `LongNoteTail` 的静态/编号帧 `Provide/Inherit`，本刀不开放作者 `Suppress`。详见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md) 与[集中视觉清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)。

### 普通短键动画 gate 完成隔离自动化与安全硬化

在不改产品 runtime 的前提下，为确定性 good/broken 包增加只运行一个 exact scene 的自动可视预检：真实 `SkinManager` 导入、60 帧加载、3 轮 good 动画/broken 回落、120 秒 watchdog、`0/1/3` 退出码，以及内部 GUID host/data storage。host 清理只作用于规范 AppData 直系子目录，逐层非递归删除且不跟随 reparse；手工素材 staging 同样只覆盖两个精确副本并拒绝 reparse/目录冲突。合并 focused **53/53**、root generator **1/1**、非法/缺值 exact CLI exit 1 且新增 host 残留 0，Release **0 error / 20 warnings**；exact 类型只在 executable test project 条件编译，保留 `osu.Game` 原 legacy runner API 且未留下新增 `CS0436`。最终代码按用户要求未重新开窗，`V-001` 仍是视觉待验收而非产品交付。

### Skin V1 视觉验收改为集中签收

按产品决定，Skin V1 不再等待每个组件逐项实机签收后才启动下一组件：自动、合同、安全与回退 gate 通过即可继续按依赖推进，视觉项统一登记到[集中清单](../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)。未签收项不得称产品交付、阶段完成或 release gate 通过；只有视觉结论实际决定后续设计时才暂停。首项 `V-001` 仍是 managed `.osk` BMS 普通短键编号帧动画，必须在 Skin V1/release 声明前取得确认；下一最小切片已冻结为 managed `.osk` BMS 长条头静态图/编号帧动画。该调度变化不放松 G1、layout、双包、真实谱/硬件或最终人工 gate。

### Skin V1 首个人工门改为确定性输入

新增 OMS 自生成的 good/broken `.osk`、静音 7K `.bme` 与 SHA-256 清单，并用真实 package 产品链将 focused 验收扩为 **28/28**；generator smoke **1/1**。取证同时确认原 beatmap-local 用例只是注入式 provider-order 合同，当前没有真实 BMS `WorkingBeatmap` 作者格式/生产 producer；手工门因此只验动画、选择切换和 selected 坏包回落，beatmap-local 是否扩入产品范围待决定。详见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md) 与[手工门说明](../other/SKIN_BMS_NOTE_ANIMATION_MANUAL_GATE.md)。

### 全仓文档与 memory 健康治理完成

在不改代码、产品行为、生产数据或 runtime gate 的前提下，重新冻结开工阅读路径和四类文档职责：mainline 只承载全局当前态与编排，subline 只承载专项状态/未来动作/稳定合同/历史，`other/` 回到参考与派生说明，memory 回到踩坑和诊断召回。活动 STATUS/PLAN 移除逐切流水、旧测试数字、提交点和会话级交接，历史完整留在 CHANGELOG/Git；根 README 的工具封装残片、非标准链接、过期状态副本和本机/生产取证敏感值也已治理。新增 `CheckDocumentation.ps1`，持续检查标准相对链接、四件套与索引完整性、低噪声预算、隐私残片和 PLAN 会话污染；全仓检查与 `git diff --check` 通过。没有运行产品测试或 Release，2026-07-15 runtime 证据仍为当前产品验证；Skin V1 下一门仍是用户实机确认普通短键编号帧动画。子线细节见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md)。

## 2026-07-14

### Skin V1 config bucket presence 第五切（P1-A）

新增 default=`Absent` 的共享 configuration declaration carrier，并由 internal mania/BMS adapter 只依据实际 decoder output 固定 missing/explicit-empty bucket provenance，避免未来 neutral mapping 把 legacy mania 合成默认对象误判为声明；未改 production lookup、decoder、fallback 或用户数据。shared gameplay focused 97/97、mania/BMS presence 13/13 与 9/9，既有回归保持基线，Release Rebuild 0 error / 20 warnings；field-level config/shared codec 与 Skin V1 生产能力仍未完成。详见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md)。

### Skin V1 neutral lane topology 第四切（P1-A）

在 shared identity 之上新增 immutable lane topology snapshot/group/entry、global/group-local logical/visual 四类零基 index、只读排序视图与 fail-closed invariant；internal BMS/mania projection fixtures 固定 5K/7K 四 style、9K BMS/PMS、14K 双 deck/双皿和 mania stage-local `SpecialKey`。唯一对既有运行时类型的修改是只读暴露 BMS solver 已计算的 `Lane.VisualIndex`，未改变 geometry/render、`SkinManager`、`OmsSkin` authority 或用户数据。shared focused 92/92，BMS/mania projection 19/19 与 8/8，既有回归保持基线，Release Rebuild 0 error / 20 warnings；这仍不是 full layout context 或可用 Skin V1。详见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md)。

### Skin V1 neutral lane identity 第三切（P1-A）

新增 ruleset-neutral 强类型 lane/group ID、group/lane identity、逻辑 presentation side 与 `Key/SpecialKey/Scratch` role；stable ID 跨视觉重排及 topology-preserving layout revision 保持，完整 identity equality 保留当前 metadata。该切片不含 layout aggregate、索引/geometry、真实 mania/BMS adapter、manifest/event ABI 或生产接线；shared focused 73/73，mania/BMS 保持基线，core skin 仍为既有 57/62，强制 Release Rebuild 0 error / 20 warnings。详见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md)。

### Skin V1 semantic slot taxonomy 第二切（P1-A）

在平行三态地基上新增 26 项 ruleset-neutral 内部 semantic slot taxonomy、descriptor/context 分离与稳定诊断 ID，固定 7 个不可 suppress 的最小可玩 family 和 19 个 optional family；catalog requirement 不能被调用方降级，process-local diagnostic context/exception 不进入 JSON。该 taxonomy 不是作者 manifest ABI、layout descriptor 或生产接线；`SkinManager`、nullable `ISkin`、`OmsSkin` authority 与用户数据均未改变。slot focused 47/47，provider 6/6，mania/BMS 保持基线，core skin 仍为既有 57/62，Release 0 error / 20 warnings。详见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md)。

### Skin V1 安全开工门闭环与三态合同首切（P1-A）

用户自行确认 `SV1-0` 完整实机清单正常，自动/数据/实机三门至此全部通过；随后完成 `SV1-1` 首个平行 `Provide/Inherit/Suppress` slot resolver、结构化 fallback 诊断与实际 provider authority fixtures，但未接入 `SkinManager` 或真实 `oms-simple.osk`，Skin V1 尚不可用。详见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md)。

## 2026-07-13

### Skin V1 数据安全门 STOP（P1-A）

schema 56 只读清点确认 folder-backed/external/path conflict 均为 0，但两条 `SkinInfo` 仍引用已删除的异常期 `BmsOmsReferenceSkin`，其中 managed 记录不会被普通启动修正；生产 Realm 前后 hash/mtime/length 一致。按 stop/go 未启动生产客户端、未进入 `SV1-1`，等待用户决定显式保全/迁移/移除；详见 [脱敏取证报告](../other/SKIN_SYSTEM_SV1_0_INVENTORY_20260713.md)。

用户随后确认异常 mutable copy 无价值并授权定点处置。迁移前 Realm/配置/四个 blob 已保全，副本单事务演练通过后才应用生产：皮肤记录 3→2、异常 GUID 消失、OMS fixed-ID 修正为当前 `OmsSkin`；read-only reopen 验证通过。未操控 GUI、未运行全局 orphan cleanup；数据 blocker 解除，实机 gate 仍待用户反馈。

## 2026-07-10

### Skin V1 产品 fallback 与社区交付物修订（P1-A）

最终 fallback 从“程序化 minimal rescue”改为只读、完整验证、可原子恢复的 `oms-simple.osk`；当前 `OmsSkin` 仅保留到文件包 parity gate，V1 发布前必须退出产品渲染链。正式双包为 `oms-simple.osk` 与 `oms-complex.osk`，两者均同包含 mania/BMS、与第三方同权；前者证明最小可玩并承担 fallback，后者证明 IIDX 级公开 API 上限。作者工作流明确对齐 osu 社区的 `.osk`、根 `skin.ini`、mania 素材/动画命名、解包编辑与拖入导入；新增 Authoring Kit 交付门。纯文档决议，无运行时代码变化。

### Skin V1 目标与执行路线重冻（P1-A，文档/架构切片）

按用户给出的首版目标完成 mania/BMS 代码审查：mania 当前是“成熟固定 C# 行为宿主 + legacy ini 素材/参数”，并非可供第三方编程的通用运行时。主线因此改为共享 neutral gameplay-skin runtime、规则集专属 topology adapter、唯一 playfield/BGA layout snapshot、declarative scene/read-only event ABI、可选受限脚本与 `Provide/Inherit/Suppress`；发布用 Minimal 与 Showcase 两个同权公共包证明下限和上限。同步识别 HUD/BGA geometry 脱节、14K 多 player、CenterP2 BGA、外部 geometry 校验、sparse keymode 和末端 lane 键音缺失风险，分别路由 P1-A/P1-L/P1-K/P1-J。新增 [架构审计](../other/SKIN_SYSTEM_V1_ARCHITECTURE_20260710.md)，执行权威改为 P1-A `SV1-0..7`；无运行时代码改动，focused 基线 BMS **43/43**、mania **84/84**，仅见既有 MessagePack `NU1902` 告警。

### Agent 入口与 Codex memory 无损去重

- `AGENTS.md` **107→53 行**：保留开始路径、权威顺序、工作流、命令、提交/push 权限和项目红线，删除与 doc 索引重复的逐子线清单；成为唯一跨 Agent 规则源。
- `CLAUDE.md` **92→11 行**：改为只引导 Claude 读取 `AGENTS.md`、主线 STATUS/PLAN、子线路由和皮肤恢复审计，不再维护第二套规则副本。
- `.Codex/memory` 保持 **28 个文件/稳定文件名**，**209.3 KB→66.0 KB**：高噪声记忆改成“权威链接 + 稳定合同 + 地雷/诊断 + 未闭合项”；逐日实现史和旧数字回归 CHANGELOG/Git。
- 修正 `feedback_workflow.md` 的失真权限：旧“直接 push”改为与 `AGENTS.md` 一致的“当前分支提交，push 前用户确认”。所有 memory 单行已压到 800 字符以内。

### 文档治理降噪与活动视图重构

- 重写 `doc_md/README.md`，冻结“一个事实一个权威落点”和四类文件边界：STATUS=当前、PLAN=未来/依赖、CONSTRAINTS=稳定合同、CHANGELOG=历史。
- 主线 PLAN **1412→113 行**、主线 STATUS **243→97 行**；保留活动顺序、release gate、最新验证和显式风险，删除已由 Git/CHANGELOG/subline 承载的实现百科与多轮旧数字。
- P1-K PLAN **504→91 行**，把 K0–K12 的逐文件切片史收回 CHANGELOG，只保留 authority、完成矩阵、真实谱驱动的补口流程与验证纪律。
- 重写 P1-A/C/H/I/J/K/L STATUS 为统一短模板；P1-A **87→55 行**、P1-C **87→43**、P1-H **33→37**（拆开千字符长行）、P1-I **66→42**、P1-J **84→49**、P1-K **78→44**、P1-L **57→40**。历史取证仍完整保留在各自 CHANGELOG 与 Git。
- 重构 mainline/subline 路由，默认阅读顺序改为“主线 STATUS → 主线 PLAN → 子线 STATUS/相关约束 → 按需查 OMS_COPILOT/CHANGELOG”；AGENTS/CLAUDE 兼容入口与 `.Codex/memory` 同步。
- 新预算：STATUS 建议 ≤120 行，禁止抬头塞调查史；PLAN 不维护旧测试数字；大型 CHANGELOG 只通过日期/子线/关键词定点搜索。

### 皮肤系统可信恢复与协作基线重建（P1-A）

以 2026-06-30 00:05（北京时间）为异常协作分界完成 Git/代码/运行时数据三层取证。恢复没有重写旧历史：恢复前 HEAD、dirty tree、不可达对象、完整 bundle 和三个已发现数据根均已保全；当前树以 `2b27c09` 的可信面为基线（其 schema 56 patch 已由分界前 WIP `a4c3346` 证明），撤回其后的 G1 生产链、F2/Lua、mania adapter 与 reference-default 替换。保留 F1 静态素材/ini 主链、程序化逐组件 fallback、folder ctor/schema 56 载体，并独立修复 base parser 流位置及 14K `S2`/P2 双皿素材映射。

自动验证：BMS **1005/1005**、H1/H2 focused **15/15**、mania 默认 OMS 资源 **1/1**、Release **0 error / 20 warnings**；mania 全量 **787/791** 的 4 项失败为既有 HoldNote auto-frame 期待，core skin focused **57/62** 的 5 项为 Argon/已删 ruleset 旧测试失配。无外部皮肤、`.osk` 用户皮肤与 5K/7K/9K/14K 实机视觉仍是显式 release gate。恢复证据、归档位置和重新准入门见 [SKIN_SYSTEM_RECOVERY_20260710.md](../other/SKIN_SYSTEM_RECOVERY_20260710.md)，执行细节见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md)。

## 2026-06-29

### BMS 皮肤：`F1` 主面完成（颜色/纹理/几何三轴皮肤化 + reference 验收）+ `G1` 可视文件夹存储启动（realm schema 56）（P1-A）

把 BMS 皮肤从「纯代码型、默认 100% 程序化」推进到「`skin.ini` 数据层 + 三轴可配 + 可视文件夹存储」。两轴正交进展：**`F1`（什么可被皮肤控制）主面已成**——ini 解析三件套（不侵入核心 `LegacySkin`）→ 配置源（`BmsLegacySkin` + `SkinImporter` 路由）→ **所有现存渲染件颜色/纹理/几何三轴皆 ini 可配**（note 家族 / lane bg / divider / hit target / bar line / lane cover / backdrop / baseplate·贴图优先 Sprite/颜色回退 Box；`BmsPlayfield` 读 11 几何键·`HitTargetVerticalOffset` 锁 0 守时序）+ reference skin.ini 创作者模板 + 自校验门 `BmsReferenceSkinTest`（逐键断言 == 真实默认）；剩 stage 框架 / `KeyImage` 净新增件。**`G1`（皮肤文件如何存放）启动**——目标＝皮肤像 chartmania/chartbms 一样可视文件夹（`chartskin/<名>/skin.ini`）直读管理，revisit「复用 SkinManager hash 存储」决议。**刀①** folder-backed 直读建块（`StorageBackedResourceStore` 当 fallbackStore·零改核心资源链）；**刀②** 核心 realm 迁移——`SkinInfo` 加 `FilesystemStoragePath` + `IsExternalFilesystemStorage`（镜像 `BeatmapSetInfo`）、**`schema_version` 55→56**（加性 nullable/scalar·无 migration case·realm 自动加列填默认；升级即迁移所有用户库，无数据迁移）。**验证**：BMS 全套 **1002/1002** + `osu.Desktop.slnf` Release gate 绿；核心 `osu.Game.Tests.Skins` 57 通过（5 失败为 OMS 删 Osu/Taiko/Catch 后 osu 模式 beatmap 归档解码的预存失败·`git stash` 干净树同样·与皮肤/ schema 改动零因果）。详见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md) 2026-06-27/06-29、[P1-A DEVELOPMENT_PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md) `F`/`G` 系列、[SKINNING.md](../other/SKINNING.md)。

---

## 2026-06-23

### 修复（K12）：BMS→mania 转谱星数被 BGM/scratch 灌高（P1-K）

应用户要求审查 `BMS→mania` 转谱星数的「计算 / 展示 / 持久化」链路（mania 模式）。结论：**持久化与展示两段健康；计算段有确凿缺陷**——converted mania beatmap 的 `HitObjects` 始终含 BGM（`Column=0`）与 scratch sample-only 对象，`isScorableHitObject` 只改 `TotalObjectCount` 计数、**不移除对象**，而 `ManiaDifficultyCalculator` 直接遍历完整 `HitObjects` 零过滤计入 Strain/MaxCombo（mania 无 beatmap-processor 剥离）→ **转谱星数系统性灌高**（键音型 BMS 灌水可观），影响选歌星数显示/排序/按星分组；**仅星数，游玩计分不受影响**（IgnoreHit 不计分）。长期未发现的根因＝K9 #17/K11 #3 与对应测试注释把「计数排除」误当「难度输入排除」，且该测试从未真跑难度计算。**K12 修复已落地**：`ManiaDifficultyCalculator.isDifficultyRelevant` 在难度入口（`CreateDifficultyHitObjects` + `maxComboForObject`）按 nested-aware combo 谓词过滤（对原生 mania 可证 no-op，故**不** bump mania `Version`）+ bump `conversion_version` `20260623`（只失效重算 BMS 库）+ 测试改**真跑** `ManiaDifficultyCalculator.Calculate()` 断言「含 vs 不含 sample-only 星数相等」。**pre-fix 反证**＝临时回退后 scratch-dense 谱星 `0.817→1.742`（+113%）；修复后原生 no-op 2/2、转谱器 24/24、resolver 14/14、BMS **961/961**、Release **0/0**。存量灌水星升级后首启由 conversion_version 失配一次性重算。详见 [P1-K CHANGELOG](../subline/P1-K/CHANGELOG.md) 2026-06-23、[P1-K DEVELOPMENT_PLAN](../subline/P1-K/DEVELOPMENT_PLAN.md) K12、[P1-K TECHNICAL_CONSTRAINTS](../subline/P1-K/TECHNICAL_CONSTRAINTS.md) K12 与 [P1-K STATUS](../subline/P1-K/DEVELOPMENT_STATUS.md)「近期修复」。

### 选歌试听音频泄漏进游玩开头 已修（P1-J）

用户以 Lyrith `_7INSANE.bms` 为例反映：部分 BMS 谱面在 songselect 有 preview 音频，autoplay 或正常游玩在游戏**开头都会播这段 preview 音频**（疑广泛存在）。根因＝BMS 游玩音频全由键音驱动（`BmsBeatmapConverter` 从不用 `Metadata.AudioFile`），但 `BmsFolderImporter` 把 AudioFile 设成选歌试听源（`detectFullMusicFile` 取 ≥1MB 未被键音引用的音频——连无 `#PREVIEW` 头的 `_preview.wav` 也中——`?? resolvePreviewFile` 的 `#PREVIEW`），该 `working.Track` 在游玩被 `MasterGameplayClockContainer` 从 0 驱动播放 → 试听音频叠在键音上；bms 原生/转谱-mania、autoplay/正常游玩四种组合全中招。**修复＝mute 方案**：新增 core `Ruleset.PlayBeatmapTrackDuringGameplay`（默认 true，`BmsRuleset` override false）；MGCC **仍以 `working.Track` 作时钟源**（时序/变速/pause-resume-seek 全不变），仅对 opt-out ruleset 在 `addAdjustmentsToTrack`（`ResetTrackAdjustments` 之后）加 `Volume=0`、`removeAdjustmentsFromTrack` 移除（退出还原试听）。门控读谱面**原生** ruleset → 转谱-mania（beatmap 仍 bms）也命中、原生 mania 不动。**此前已废的虚拟轨/换源方案被显式禁止回退**（破坏 audio-semantics 确定性驱时 + 真实耦合时钟下无效 + 单测假阳性）。BMS **957/957**（新增 `TestSceneBmsGameplayTrackMuting` 2 条 + 修回 `TestSceneBmsPlayerAudioSemantics` 2 条）、Release **0/0**；**用户实机确认 ✅（2026-06-23，autoplay+正常开始不再泄漏试听、暂无异常）**。

**同日 follow-up（审查 song-select preview 链路后，用户决定）：选歌试听策略收紧——只有 `#PREVIEW` 头才有试听、且从文件头播（`PreviewTime=0`）；其它情况一律无试听**。`BmsFolderImporter` 的 `Metadata.AudioFile` 改为只取 `#PREVIEW`（`resolvePreviewFile`）、**删除 `detectFullMusicFile` 启发式**（≥1MB 未引用音频不再当 AudioFile）+ 其 `allKeysoundFiles` 排除集；无 `#PREVIEW` 谱（含 Lyrith `_7INSANE.bms`）从此无试听、AudioFile 空。两改互补（mute 让 `#PREVIEW` 谱游玩静音、本策略限定哪些谱有试听）。**存量谱回写**：新增 `BmsPreviewAudioBackfill`（挂 `OnSongSelectSetup`、后台、候选=非空 AudioFile、批量回写注入 RealmAccess），让已导入库也套用新策略（导入改动只对新导入生效）。**首版每启动跑 + 逐项 Realm 读 → 进选歌掉帧（用户报告，同日修）**：重做为 **① 完成标记文件一次性**（`bms-preview-audio-backfill-v1.marker`，跑过即整段跳过）+ **② 单次 Realm 读快照候选成 struct、解码循环零 Realm** + **③ 进度通知**（`ProgressNotification`，首启示「正在更新 BMS 选歌预览…X/Y」；用户面术语用「选歌预览」非「试听」）。BMS **961/961**（+4 `BmsPreviewAudioBackfillTest`，同时覆盖了此前无单测的导入侧）、Release 0/0；**用户 2026-06-23 实机已运行（图示首启进度通知 ~700/3056），暂未见异常**。

### carousel 面板两处音符图标（P1-I）

两项独立 UI（用户提出）：**① 曲名右侧加 preview 指示音符**——`PanelBeatmapStandalone` 曲名右侧 `SpriteIcon`(Music)，仅「BMS 模式 + BMS 谱 + `Metadata.AudioFile` 非空（＝有 #PREVIEW＝会试听）」时显示；**② 删最左 lamp 块上的 ruleset 音符**——`BmsRuleset.CreateIcon()` 是 `FontAwesome.Solid.Music`，`PanelBeatmap`/`PanelBeatmapStandalone` 对 BMS 谱设 `difficultyIcon.Alpha=0` **且创建时 `AlwaysPresent=true`**（base `Panel.iconContainer` 是 AutoSize、**不计 Alpha=0 的非 present child**，故只设 Alpha=0 会收缩容器、连 lamp 块一起没了〔首版踩坑、用户截图发现〕；`AlwaysPresent` 让隐藏 icon 仍占布局宽 → 标题不位移、lamp 颜色块保留、仅音符不可见）。两改皆 core osu.Game、panel 无 headless 单测（`<Compile Remove>`），Release 0 错误、BMS 961/961 不回归；**用户 2026-06-23 实机暂未见异常**。详见 [P1-I CHANGELOG](../subline/P1-I/CHANGELOG.md) 2026-06-23 与 [P1-I 约束 #23](../subline/P1-I/TECHNICAL_CONSTRAINTS.md)。

详见 [P1-J CHANGELOG](../subline/P1-J/CHANGELOG.md) 2026-06-23 与 [P1-J 约束 #12](../subline/P1-J/TECHNICAL_CONSTRAINTS.md)。

## 2026-06-22

### 删除「键音通道数（基线）」设置 + BGA 转码设置改名加按钮（P1-J / P1-L）

两项 BMS 设置改动：① **删除**「设置-游戏模式-BMS-键音通道数（基线）」——键音池自动增长已收口、无需手调；彻底退役 `BmsRulesetSetting.KeysoundConcurrentChannels`（enum 成员+默认+UI+`DrawableBmsRuleset` 绑定+转谱 factory 的配置读取全移除），原生回落到硬编码基线 32+自动增长、转谱楼底 128。**无隐患删除**：ruleset 配置按 enum 名持久化（非序号，删中间成员不移位其它），且所有消费方已移除，旧库残留值永不再被读取（惰性无害），用户旧自定义值不再生效。删除失效的 `TestSceneBmsKeysoundChannelConfigBinding`、去掉相关默认断言。② BGA「转码无法解码的 BGA 视频」**改名「ffmpeg完整BGA支持」**、描述精简为「对老式BGA提供转码播放支持，需自行放置ffmpeg到数据目录」，并加两个按钮：**检测 ffmpeg 安装状态**、**打开 ffmpeg 安装目录**（`host.OpenFileExternally` 打开数据目录）。**跟进修复**（用户反馈 ffmpeg 已检测到但 `.mpg` 仍只静态图）：先做诊断改造——① 转码失败把 ffmpeg 输出记日志（原本静默吞掉）；② 「检测」改 `ProbeFfmpeg` **真跑** `ffmpeg -encoders` 验证可执行+是否含 libx264（回应「检测只查文件存在」疑点）；③ 编码器回退 libx264→内置 mpeg4（防御非完整版 ffmpeg）。**实机日志定位真因＝输出写 `<hash>.mp4.tmp`、ffmpeg 按扩展名推断容器、`.tmp` 选不出 muxer（`Unable to choose an output format`）；libx264 一直可用（与回退 mpeg4 同错，证实是容器非编码器）。真修＝转码命令加 `-f mp4`**（`BuildTranscodeArguments` 抽出可测+回归）。降噪＝stderr 记 Verbose、每失败源一条 Important 摘要去重。经多轮（`-f mp4`→baseline→看门狗干净降级）后**真因确诊**：用户给出 `Error splitting the input into NAL units / Invalid NAL unit size` + 提议查 ffmpeg；直接验证发现**用户 ffmpeg 完全正常（gyan.dev 8.x 含 libx264），但它解不了缓存里的 .mp4＝文件本身坏的**；同参数全新转码解码干净；**模拟两个 ffmpeg 并发写同一 temp 复现出一模一样的 `Invalid NAL unit size (0 > 25614)`**。**根因＝并发转码写同一固定 temp `<hash>.mp4.tmp` 互相穿插污染**（转码 Task 在 BgaPlayer dispose 不取消，退出/快速重放对同一 temp 起第二个 ffmpeg；坏文件落缓存后被 `File.Exists` 永久端出）——之前 profile/HW/demux 全是被坏文件带偏的误判。**修复**＝temp 加 Guid（并发各写各的）+ `inProgress` 改 static（跨实例去重）+ `File.Move(overwrite:true)` 原子发布 + `transcode_version`→3 失效旧坏缓存；配套干净降级（看门狗 `Video.FramesProcessed` 1s 内 0 帧 drop+dispose 掐刷屏、静态优先于黑、legacy 不裸开 .mpg、转码失败日志降 Verbose）。BMS 全量 **948/948**，代码编译 **0 错误**。**实机验收待用户确认（视频应真正播放）**。详见 [P1-J CHANGELOG](../subline/P1-J/CHANGELOG.md) 2026-06-22 与 [P1-L CHANGELOG](../subline/P1-L/CHANGELOG.md) 2026-06-22。

### BMS 模式选曲星级 → IIDX 难度等级胶囊（P1-I）

按用户要求：BMS 模式下 BMS 谱面的选曲「星级」早就只反馈谱师标级、不再以星级形态展示。改为保留原圆角胶囊背景，显示「难度标签 等级」（如 `NORMAL 7`）、去掉小数、难度色改用 IIDX 配色（`#DIFFICULTY` 0/未定义=UNKNOWN 白｜1=BEGINNER 绿｜2=NORMAL 蓝｜3=HYPER 黄｜4=ANOTHER 红｜5=INSANE 紫）。**仅 BMS 模式下的 BMS 谱面**；转谱-mania 视图保留真实转谱星级。等级用**原始 `#PLAYLEVEL` 文本**（谱师标级 verbatim）；未定义难度显示 `UNKNOWN ＋等级`；小星星 / 分布点等其它星形元素**全部保持现状**（用户选定）。数据源已就绪（`#DIFFICULTY`/`#PLAYLEVEL` 导入即持久化、`BmsPersistedMetadataResolver.GetChartMetadata` 直读），零碰解析层、零重导。新增 `BmsDifficultyLevelDisplay` 组件 + `OsuColour.ForBmsDifficultyLevel` 配色 + `BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyLevel/GetBmsDifficultyTier` 文案，挂到 `PanelBeatmap`/`PanelBeatmapStandalone`/`BeatmapTitleWedge` 三处（alpha 互斥、原星级胶囊保持存活喂色给保持现状的元素）。**同日追加**：开始游玩的加载界面 `PlayerLoader`（`BeatmapMetadataDisplay`）此前难度名/星级仍读存库原值，现统一为标题剥尾括号 + 难度名走 `GetDisplayDifficultyName`（→`SP ANOTHER`）+ 星级换等级胶囊（门控同选曲、转谱-mania 保留真实星级；不动中央 `GetDisplayTitleRomanisable`）。回归 `BmsLocalMetadataDisplayResolverTest` 14/14（+5），`osu.Desktop.slnf` Release **0 错误**。**选曲胶囊用户已确认「符合预期」，加载界面待确认**。详见 [P1-I CHANGELOG](../subline/P1-I/CHANGELOG.md) 2026-06-22（其五）与 [P1-I 约束 #22](../subline/P1-I/TECHNICAL_CONSTRAINTS.md)。

### mania 选曲新增「难度表」分组：只显示 BMS 转谱 + 空结果指导（P1-I）

按用户要求给 mania 选曲分组下拉新增「难度表」分组（行为同 BMS 难度表分组），但只显示 BMS 转谱、排除原生 mania；空结果时给针对性指导。mania ruleset 不引用 BMS ruleset，故难度表分组定义由 osu.Game 新增共享 `BmsConvertedDifficultyTableGrouping` 提供（复用只读 `BmsPersistedMetadataResolver.GetDifficultyTableEntries`，构 表名→等级 树、无条目 Unrated、有界缓存）。**「只显示转谱」用 grouping 丢弃法实现、零改 matching**：`addHierarchicalGroups` 丢弃返回空 group 定义的谱面，且「N matches」取 grouping 阶段计数，故 helper 对非 BMS 谱面返回空 → 原生 mania 被丢弃、计数与列表一致；转谱可见性仍由「显示转谱」设置在 matching 把关（禁用→空）。`ManiaRuleset` override 4 个分组虚方法接线（复用既有 `GroupMode.DifficultyTable`）。`NoResultsPlaceholder` 加空结果指导（转谱禁用→可点启用；转谱已开但空→提示导入 BMS）。回归 `BmsConvertedDifficultyTableGroupingTest`(4)+`ManiaDifficultyTableGroupingTest`(3 端到端)+`BeatmapCarouselFilterGroupingTest` 17/17 不回归，`osu.Desktop.slnf` Release **0 错误**。**用户 2026-06-22 实机验收通过（观测暂无异常）**。详见 [P1-I CHANGELOG](../subline/P1-I/CHANGELOG.md) 2026-06-22（其四）与 [P1-I 约束 #21](../subline/P1-I/TECHNICAL_CONSTRAINTS.md)。

### mania 选曲「显示转谱」按钮改三态：禁用 / 启用 / 仅显示转谱（P1-I）

按用户要求把 mania 选曲筛选区的「显示转谱」从二态（启用/禁用）升为三态：**禁用**＝只原生 mania｜**启用**＝原生+转谱（BMS→mania）｜**仅显示转谱**＝只显示转谱、隐藏原生 mania。单一收口为新 enum `ConvertedBeatmapsDisplay { Hidden, Shown, ConvertedOnly }`：`OsuSetting.ShowConvertedBeatmaps`(bool) 改名 `ConvertedBeatmapsDisplay`(enum，默认 Shown)；`FilterCriteria` 加 enum 字段、`AllowConvertedBeatmaps` 降为其 bool 投影；过滤行为收口在 `BeatmapCarouselFilterMatching` 一处 switch。UI＝mania 用三态循环按钮 `ConvertedBeatmapsDisplayButton`、BMS 保留二态（带 echo 守卫双向同步）、设置面板改三态下拉。**红线**＝OMS 只有 BMS→mania 一条转谱路，`仅转谱` 对非 mania ruleset 会清空列表，故 `FilterControl.CreateCriteria` 对非 mania 把 ConvertedOnly 夹回 Shown、BMS 按钮不暴露该档。回归 `FilterMatchingTest` +4（98/98）、carousel sort+group 25/25、`osu.Desktop.slnf` Release **0 错误**。**用户 2026-06-22 实机验收通过（观测暂无异常）**。详见 [P1-I CHANGELOG](../subline/P1-I/CHANGELOG.md) 2026-06-22（其三）与 [P1-I 约束 #20](../subline/P1-I/TECHNICAL_CONSTRAINTS.md)。

### 标准面板第 4 排加难度表归类（星级 ↔「展示全部难度」按钮 之间，BMS + 转谱-mania）（P1-I / P1-H）

展示层级＝「谱面」时，选歌列表扁平行 `PanelBeatmapStandalone` 第 4 排在 星级 与「临时展示所有难度」按钮之间插入**难度表难度归类**标签（如 Satellite-sl4 → `sl4`；若还属发狂难易度表-★8 → `★8/sl4`，按难度表 `TableSortOrder` 顺序、`/` 分隔一一列举），BMS 选曲与转谱-mania 选曲都生效。osu.Game 不引用 BMS ruleset，故经 `BmsPersistedMetadataResolver.GetDifficultyTableEntries` **只读** `BmsPersistedMetadataData.ExtensionData` 里的 `difficulty_table_entries`（osu.Game 侧 DTO 只建模 4 字段、绝不回写——**严禁建模成可写字段**，否则 converted-star 写回会抹掉 `Symbol`/`Md5` 重蹈 P1-H #22 共享列 clobber）；展示收口在 `BeatmapLocalMetadataDisplayResolver.GetDisplayDifficultyTableClassification`（display-only，键于 bms ruleset，故两模式取值一致）。无归类（非 BMS / Unrated）则 `Alpha=0` 收起、无幽灵间距。新增 `BeatmapLocalMetadataDisplayResolverTest` 4 条全过、`osu.Game` Release 0 错误 0 警告。**用户 2026-06-22 实机验收通过（观测暂无异常）**。详见 [P1-I CHANGELOG](../subline/P1-I/CHANGELOG.md) 2026-06-22（其二）与 [P1-I 约束 #19](../subline/P1-I/TECHNICAL_CONSTRAINTS.md)。

### 修复 LNOBJ 长条解码：连续 LNOBJ 尾造"长条内单点"（P1-K）

用户实机发现 `Stella/st4/Grayed Out -Antifront-/spf.bml`（`#LNOBJ 01` 长条）~11–13s 长条中出现一个"单点"，bms 与转谱 mania 两模式同现。脚本逐字节复刻 OMS 解码逻辑确诊为**解析器 bug、非谱面错误**：通道14 序列 `7O 7P 01 01`（两连续 LNOBJ 尾），OMS 用 **LIFO 栈**存每轨待配对长条头，第 2 个 `01` 回头抓更早的 `7O`（本应已是单点），造出 `LN(7O)12.316→12.868s` 完全包住 `LN(7P)12.474→12.632s` 的**同轨时间重叠长条**（物理不可能同时按住 → 渲染成"长条里的单点"）。修复＝[BmsBeatmapDecoder](../subline/P1-K/CHANGELOG.md) 把每轨待配对头由栈改**单头**（`Dictionary<int,List<int>>`→`Dictionary<int,int>`），尾只配紧邻前一普通音符、消费即清空，连续第 2 个尾作孤儿丢弃（合规范/beatoraja）；正常长条零影响（全谱长条 1110→1109，仅去掉那条假长条）。一处解码修复同纠两模式。新增回归 `TestConsecutiveLnObjTailsDoNotFabricateOverlappingLongNote`；BMS 全套 **943/943**、Release **0 错误**。**用户 2026-06-22 实机确认暂无异常（验收通过）。** 详见 [P1-K CHANGELOG](../subline/P1-K/CHANGELOG.md) 2026-06-22 与 [P1-K 约束 键音呈现与控制流 #7](../subline/P1-K/TECHNICAL_CONSTRAINTS.md)。

### 选歌右键「打开歌曲/谱面文件位置」——在资源管理器中定位（P1-I / P1-H）

按用户要求在选歌右键菜单加两项「在系统资源管理器中定位」：歌曲条（`PanelBeatmapSet` / 单难度合并条 `PanelBeatmapStandalone`）→「打开歌曲文件位置」（打开父目录并选中歌曲文件夹）；难度（经 `SoloSongSelect.GetForwardActions`，覆盖 `PanelBeatmap` 行 + standalone + footer Options 弹层）→「打开谱面文件位置」（打开歌曲文件夹并选中该 .bms）。**范围＝所有 filesystem-backed 谱面**（`BeatmapSetInfo.FilesystemStoragePath` 非空：BMS chartbms/ + 直读 mania chartmania/；hash 库无文件夹故不显示）。路径解析收口在新共享 helper `osu.Game/Beatmaps/FilesystemBeatmapLocation.cs`（复用 `BmsBgaPlayer.tryGetAbsolutePath` 范式：external＝绝对原样、managed＝`storage.GetFullPath`，难度＝set 目录 + `LocalFilePath` 且 `/`→原生分隔符）；定位走 `GameHost.PresentFileExternally(绝对路径)`（Windows＝`explorer /select`），**不走 `Storage.PresentFileExternally`**（外部库绝对路径越数据根会触发 traversal 守卫抛异常）。外部目录只读打开、绝不改动；目标缺失优雅退回父目录。仅加菜单项 + 解析/定位，不碰筛选/排序/分组/存储写入。新增 `FilesystemBeatmapLocationTest`（6）+ `osu.Game.Tests` 面板 4/4 确认 `[Resolved]` 不破坏加载。验证：BMS 全套 **942/942**、`osu.Desktop.slnf` Release **0 错误**。**人工实机定位行为待用户确认**。详见 [P1-I CHANGELOG](../subline/P1-I/CHANGELOG.md) 2026-06-22 与 [P1-I 约束 实现边界 #7](../subline/P1-I/TECHNICAL_CONSTRAINTS.md)。

---

## 2026-06-21

### BMS 长条 body 改造（增宽/同 head 色/三态视觉）+ 更正 CN 接回语义（P1-A / P1-E）

按用户要求改造默认皮肤长条 body 视觉，并在评审中确认顺带更正一处 CN 机制 bug。**视觉（P1-A）**：`DefaultBmsLongNoteBodyDisplay` body 增宽 10%（`0.525→0.5775`）、颜色由暗条改为与 head 一致（`GetLongNoteHead`，保留 0.8 透明）；新增皮肤无关三态 `BmsLongNoteBodyState{Idle,Holding,Broken}`，父 `DrawableBmsHoldNote` 每帧由 `isHolding`+head/tail 判定纯派生并经 `IBindable BodyState` 暴露，默认 body 经 `[Resolved] DrawableHitObject` 绑定（mania `DefaultBodyPiece` 同范式）按状态切视觉——**unactivated==activated**（head 色+0.8）、**missed＝去色变灰+降透明度**（新 `GetLongNoteBodyBroken`，alpha 0.32）。**机制更正（P1-E）**：用户确认「CN 松开可重按接回」是开发错误——正确为 `LN`=头判+长条、`CN`=头判+长条+尾判（中途松开永久 miss 不可接回）、`HCN`=头判+长条（持续 gauge）+尾判（可重按恢复）。新增 `AllowsRegrabAfterRelease()`（==HCN），把 `CanApplyLateBodyPress` 与 `OnReleased` 等待接回分支的门控由错误的 `RequiresTailJudgement()`（CN+HCN）收窄为 HCN-only；body 三态纯派生自 `isHolding`，故「仅 HCN 恢复」自动成立。仅视觉+长条松开语义，不碰 head/tail（tail 仍 `Alpha=0`）/判定窗口/计分/滚动/键音/chartbms 直读。更正 3 测 + 新增 1 测（body 生命周期含 HCN 恢复）+ 改 2 条皮肤回归值。验证：BMS 全套 **936/936**、`osu.Game.Rulesets.Bms.Tests` 0 错。**人工实机视觉验收待用户确认**。详见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md) / [P1-E CHANGELOG](../subline/P1-E/CHANGELOG.md) 2026-06-21 与 [P1-A 约束 皮肤边界 6](../subline/P1-A/TECHNICAL_CONSTRAINTS.md) / [P1-E 约束 #4](../subline/P1-E/TECHNICAL_CONSTRAINTS.md)。

### 原生 BMS 键音保真两改：autoplay=完美游玩 + 键音池自动增长（P1-J）

用户对照 beatoraja 反映 autoplay 的「音乐演奏」不正确（疑不发声/重复/发错/截断），并立两条判据：**(A) autoplay 必须等同 100% 完美游玩**（否则真实游玩也有问题）、**(B) 「键音通道数」是否好设计/是否该智能自动**。审查链路后落地两改：① **autoplay = 完美游玩**——确诊 autoplay 每音符**双触发**（音符 `AutoPlay=true` 退出输入 → replay 合成按键直达 `BmsLane` → lane armed 键音叠音符自身 auto-apply 键音；per-WAV cut 多数掩盖、armed≠音符槽或异步通道状态时露馅重复/发错），修复 = `BmsLane` 在本 lane 有自动音符时抑制 armed 键音、发声交给音符（每音符一次声、与完美游玩等价）；连带结论：**32 通道截断 + 同步触发抖动非 autoplay 专属、玩家完美游玩同样存在**。② **键音池固定上限 32 → 自动增长**——`getNextChannel` 饱和不再轮转偷断，改为**新增通道（封顶 256）、仅 256 仍饱和才偷**（保真单调、自然有界、消「截音」；原生此前只有 32、转谱-mania 早 floor 128），`KeysoundConcurrentChannels` 降级为「起始/常驻基线」、tooltip 同步。仅改原生 BMS 键音发声，不碰判定/计分/转谱链。验证：`osu.Desktop.slnf` Release **0 错**、BMS 全套 **936/936**（改写 1 + 新增 2 测）。**实机听感（对照 beatoraja）用户实测确认 ✅（2026-06-21，用户回报「优化及其明显、暂时无异常」；虚拟轨测试对真发声/真不截是盲区，故以实机为准）**；同步触发挤堆（架构性）与解析少键（P1-K）后置。详见 [P1-J CHANGELOG](../subline/P1-J/CHANGELOG.md) 2026-06-21 与 [P1-J 约束 #3b/#4/#8](../subline/P1-J/TECHNICAL_CONSTRAINTS.md)。

### 修复选歌两处「自动跳滑」：选谱 root-jump + 窗口还原 group-jump（P1-I）

用户实机报两处 carousel 视图被自动滚走的 bug，都出现在「mania 曲目正在播放时进入 BMS 难度表分组」，但机制不同：**Bug 1**＝进 BMS 后手动选中任一谱面，画面自动滑到该谱所属最外层（表名）分组——根因＝`pendingRootGroupFocus`（fresh-entry root 聚焦）在当前全局谱面对 BMS invalid（mania）时永远满足不了、标记长挂，等用户选第一张 BMS 谱时被 `tryFocusRootGroupForCurrentBeatmap` 劫持滚到 root；修复＝出现具体选中（`CurrentGroupedBeatmap != null`）即放弃该 pending focus（与 2026-06-18 #16「抑制 invalid 期间自动选中」对称收口）。**Bug 2**＝选歌列表里自由滚到别处后按 Win 最小化再返回，画面跳到当前展开层级的组头——根因＝窗口最小化/还原改变 carousel `DrawSize`，base `Carousel.OnInvalidate` 无条件重跑「保持选中居中」滚动，而无具体选中时 `BeatmapCarousel.GetScrollTarget` 回退到键盘光标/`ExpandedGroup` 位置、拽回组头；修复＝DrawSize re-center 限定在 `currentSelection.CarouselItem != null` 才执行（mania 选歌恒有已提交选中、零行为变化；「键盘光标停在组头不提交选中」是 BMS 层级分组特性、本 bug 成因边界）。改动＝`SongSelect.cs`（bug 1）+ shared base `Carousel.cs`（bug 2，mania-safe）。回归＝`TestSelectingChartWhileNonBmsPlayingDoesNotJumpToRootGroup` + `TestDrawSizeChangeDoesNotRecentreWithoutSelection` + 守护 `TestDrawSizeChangeRecentresCommittedSelection`（去修复均验证失败）；BMS 选歌分组套件 11/11、新增 3 条全过、`osu.Game.Tests` carousel 18 条 pre-existing 失败经 baseline 比对确认与本改动无关、Release 0 错误。**用户 2026-06-21 实机验收通过（两个原始复现场景观感正常）。** 详见 [P1-I CHANGELOG](../subline/P1-I/CHANGELOG.md) 2026-06-21 与 [P1-I 约束 #17/#18](../subline/P1-I/TECHNICAL_CONSTRAINTS.md)。

---

## 2026-06-20

### BMS 游玩 HUD 三连改：playfield 顶边贴边 + combo 居中去色块 + 14K BGA 移到角落（P1-A / P1-L）

按用户实机反馈一次性改三点：① **playfield 顶边贴屏幕边缘（P1-A）**——删除上一版的整体下移 `PLAYFIELD_VERTICAL_OFFSET`（它让顶边离开屏幕顶、违背 green-number「音符从屏幕最顶出现」语义），playfield 恢复纯顶部锚定；为保持 gauge 仍在原低位把 `DEFAULT_PLAYFIELD_HEIGHT 0.86→0.92`（判定时序不变量不受场高影响）。② **combo 移到 playfield 中心 + 去背景色块（P1-A）**——`DefaultBmsHudLayoutDisplay.applyComboPlacement` 把 `BmsComboCounter` 放到 playfield 宽/高中线交点（随 PlayfieldStyle 镜像）、`BmsComboCounter` 去掉背后的 body 色块容器只留居中标签 + 数字。③ **14K BGA 镜像到屏幕四角（P1-L）**——`BmsBgaPlacement` 扩为四角 + Center，14K 默认从居中 gap 改到**四角各 mount 一个 BGA**（紧凑尺寸贴窄双打侧边距、不压车道；keymode 改用可靠的 `GameplayState`；首版误做成单角 + 检测失败致尺寸过大，已返工修正）。仅视觉/摆位/挂载，不碰判定/计分/滚动/BGA 解码。回归 `TestComboCentredOnPlayfield` + `TestSceneBmsBgaPanelLayout`（14K=4 player/单打=1）+ `BmsBgaPlayerTest` + 几何断言（0.86→0.92）；BMS 全套 **933/933**、`osu.Desktop.slnf` Release **0 错误**。**人工实机视觉验收待用户确认**。详见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md) / [P1-L CHANGELOG](../subline/P1-L/CHANGELOG.md) 2026-06-20 与 [P1-A 约束 #18](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)。

### 修复 BMS gauge 被通用"血条显示"开关误隐藏（P1-A）

去掉默认 combo / leaderboard 后用户报「gaugebar 没了」。诊断（先排除 strip：真实 gauge 在 strip 后布局仍可见）后定位真因：**`BmsGaugeBar : HealthDisplay`，`HealthDisplay` 把自身绑到 `HUDOverlay.ShowHealthBar`、其为 false（NoFail 等通用隐藏血条开关）时 `FadeTo(0)`** → BMS groove gauge 被一并隐藏（combo 不受影响，故"combo 在、gauge 没"）。诊断盲区＝旧 gauge 测试无真实 `HUDOverlay`（`hudOverlay`→null→`showHealthBar` 恒 true）掩盖该路径。修复＝`BmsGaugeBar` 解析 `[Resolved(CanBeNull)] HUDOverlay`，订阅 `ShowHealthBar` 并重申 `Alpha=1`（在 base 之后注册以压过其淡出），**免疫**通用血条开关、始终显示；HUD 整体 `ShowHud` 淡入仍经父级生效。回归 `TestGaugeBarStaysVisibleWhenHealthBarHidden`（真实 Player+HUDOverlay，置 `ShowHealthBar=false` 断言 gauge 仍可见）+ `TestRealGaugeLoadsAndIsVisible` / `TestRealGaugeVisibleAlongsideStrippedWrappedHud`；BMS 全套 **929/929**、`osu.Desktop.slnf` Release **0 错误**。详见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md) 2026-06-20 与 [P1-A 约束 HUD 宿主约束 6](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)。

### BMS gauge 下移到判定线下方 + 矩形化 + 等宽镜像 playfield（游玩区抬高，视觉/摆位，P1-A）

把原先摆在 playfield 顶部的圆角胶囊 gauge 改为 IIDX groove-gauge 观感的矩形条，落在判定线下方、与判定区等宽并随 P1/P2/居中侧锚；同时抬高游玩区腾出下方空带（用户规划阶段选定「抬高 0.86 / gauge 等宽」）。① **抬高游玩区**——`BmsPlayfieldLayoutProfile` 默认 `PlayfieldHeight 0.95 → 0.86`（提为公开常量 `DEFAULT_PLAYFIELD_HEIGHT`），判定线上移到 86% 屏高、下方 ~14% 空带容纳 gauge；**判定时序不变**（`HitTargetVerticalOffset=0` 时 `scrollLengthRatio≡1`、`TimeRange` 与场高无关，仅落条像素扫过距离变短）。② **gauge 矩形化**——`BmsGaugeBar` 圆角 `10→0`、bar 高 `20→28`、数值字号 `14→18`、加 10 等分极淡刻度；标记/填充/文案保留。③ **下移 + 等宽 + 侧锚镜像**——`DefaultBmsHudLayoutDisplay` 把 gauge 设为 `RelativeSizeAxes.X + Width=PlayfieldWidth`、`Y=PlayfieldHeight+0.012`、Anchor 按 `PlayfieldStyle.GetAppliedStyle(keymode)` 做 P1 左/P2 右/居中，与 lane 严格同列。④ **合同保持**——gauge 仍在 `IBmsHudLayoutDisplay.SetComponents(wrappedHud, gauge, combo)` 合同内、**未改签名**（满足「不得破坏 IBmsHudLayoutDisplay 签名」硬约束）；所需几何经 HUD 可见 DI 通道取得（`PlayfieldWidth/keymode` ← `GameplayState`，`PlayfieldStyle` ← game 级 `IRulesetConfigCache.GetConfigFor(bms)`，均 `CanBeNull`，皮肤编辑器预览/测试优雅降级居中）。仅视觉/摆位/几何，不碰判定/计分/滚动/chartbms 直读。测试同步 `PlayfieldHeight 0.95→0.86`（`BmsLaneLayoutTest`、`TestSceneBmsPlayfieldLayoutConfig`）+ 新增 `TestSceneBmsHudGaugePlacement`。验证：BMS 全套 **925/925**、`osu.Desktop.slnf` Release **0 错误**。**（其二·一体化跟进）** 首轮实机后按用户反馈再调 gauge 视觉：间隙贴紧（顶边偏移 `+0.012→+0.002`）、背景改 playfield 海军蓝渐变（`GaugeTrackTop/Bottom`，落在 lane/baseplate 色域）、去四周边框只留顶边 accent hairline、`NORMAL`/数值叠加在 band 上（IIDX 式、带 Shadow）取代浮在空隙的 header 行——使 gauge 读作 playfield 立柱底段而非外挂控件；仅 `BmsGaugeBar` 视觉与间隙改动，几何/合同链路不变，BMS 925/925。**（其三·整体下移）** 再按用户标注把整条 play 立柱（playfield + gauge 一体）下移：新增共享常量 `PLAYFIELD_VERTICAL_OFFSET=0.06`，playfield 顶部锚定后置 `Y=OFFSET`（顶边留 header 空带）、gauge_top 同步加该 OFFSET，判定线落 `≈0.92` 屏高、gauge 止于近屏底；`PlayfieldHeight` 仍 `0.86`、判定时序不变。BMS 925/925。**人工实机视觉验收待用户确认**（7K/14K/P1/P2）。详见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md) 2026-06-20 与 [P1-A 计划 E1](../subline/P1-A/DEVELOPMENT_PLAN.md) / [P1-A 约束 #18、HUD 宿主约束 4](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)。

### BMS gameplay 从默认皮肤配置移除游玩排行榜 + 重复默认连击数（P1-A）

按用户反馈把「默认皮肤左下角连击数 + 左侧排行榜」**从默认皮肤配置删去（非运行时隐藏）**。两者**同源**＝上游 `LegacySkin` 的 ruleset-`MainHUDComponents` 默认布局直接 `new LegacyDefaultComboCounter()` + `new DrawableGameplayLeaderboard()`，经 `BmsSkinTransformer` 包成 BMS HUD 的 wrapped 层（中央金色 combo 是 BMS 自有 `BmsComboCounter`、保留；右上 score 等来自全局 Ruleset==null 层、不受影响）。修复＝`BmsSkinTransformer.stripDefaultHudElements` 在装配 BMS `MainHUDComponents` 时把 wrapped 容器直接子里的 `ComboCounter` 与 `DrawableGameplayLeaderboard` **从配置树移除**（`Container.Remove`），二者根本不进入 BMS HUD 树（不渲染 / 不进皮肤编辑器序列化 / 无首帧闪烁）。同时回退上一版的"隐藏"式尝试（`ShowLeaderboard=false` + foreign-combo 有界重试隐藏），改用单一"配置移除"。回归 `TestRulesetHudStripsDefaultComboAndLeaderboard`；BMS 全套 **926/926**、`osu.Desktop.slnf` Release **0 错误**。详见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md) 2026-06-20 与 [P1-A 约束 HUD 宿主约束 5](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)。

---

## 2026-06-18

### BMS 谱面构成 backfill 不再每次启动重算"空/不可算"谱面（P1-I）

用户反馈"每次新启动进 BMS 选歌总会跑一次谱面构成计算、没有持久化"。根因＝composition-filter backfill 只在算出**非空** stats 时写回（`sanitise` 把空结果折叠成 `null`、`SetChartFilterStats(null)` 不写盘），凡是计算结果为空/不可用的谱面（空谱＝仅 BGM/autoplay 0 playable，或直读+`GetWorkingBeatmap` 回落双双失败）`ChartFilterStats` 恒 `null` 且无"已处理"痕迹，每次启动被 Phase 1 重新归类为 missing 并重算（通知每启动一冒）。修复＝引入持久化负缓存标记 `BmsBeatmapMetadataData.ChartFilterStatsResolved`（同列、`IsEmpty` 计入、`[JsonExtensionData]` 仍兼容 converted_star）+ `ResolveChartFilterStats`（非空存 stats、空只置标记，都标记已处理）+ `GetChartFilterStatsState` 单次反序列化取 `(Stats, Resolved)`；Phase 1 跳过已 resolved、Phase 2 对每张已 Detach 谱面写回（空也落标记）、import/reuse/GetOrBackfill 一律改走 `ResolveChartFilterStats`，写回 `catch` 改记 `Important` 日志。空谱在 `Matches` 仍按 `null` fail-open（标记只阻止重算、不隐藏谱面）。验证：`BmsChartFilterStatsBackfillTest` 新增 resolved-marker 四条、BMS 全套 **922/922**、`osu.Game.Tests` converted-star/persisted-metadata 17/17。详见 [P1-I CHANGELOG](../subline/P1-I/CHANGELOG.md) 2026-06-18（其三）与 [P1-I 约束 #16](../subline/P1-I/TECHNICAL_CONSTRAINTS.md)。

### 非 BMS 播放谱面进入 BMS 不再误展开分组（P1-I）

启动自动随机播放的曲目若是非 BMS（mania）谱面，直接进入或从 mania 切到 BMS（难度表分组）时总会误展开某分组（用户观察为 Unrated）。根因＝`SongSelect.ensureGlobalBeatmapValid` 的 `shouldSuppressGroupedAutoSelection()`（fresh-entry root-focus 期间抑制自动选中）只在 `if (validSelection)` 分支内检查；mania 当前谱面对 BMS invalid → 走 invalid 回退 `SetDefault→IsDefault→NextRandom` 自动选中并展开某分组，而 `FocusRootGroupForBeatmap` 聚焦不到 mania 谱面 → `pendingRootGroupFocus` 一直为真、抑制被回退绕过。修复＝把该抑制提前到 valid/invalid 分支之前短路返回，fresh-entry 期间抑制所有自动选中（含 invalid 回退）、保持 root 层不展开。只影响 `ShouldResetSongSelectGroupToRoot` 为真的 ruleset（BMS）；mania 等零影响。回归 `TestNonBmsPlayingBeatmapDoesNotExpandGroupOnEntry`（去掉修复即失败），BMS 全套 **918/918**。详见 [P1-I CHANGELOG](../subline/P1-I/CHANGELOG.md) 2026-06-18（其二）与 [P1-I 约束 #16](../subline/P1-I/TECHNICAL_CONSTRAINTS.md)。

### BMS 层级分组（难度表）展开态/缩进方向修正（P1-I I6 跟进）

用户实机反馈两处层级分组下不符合直觉的行为，均为**路径根组**（表名层）的处理缺口：① 展开的表名组不显示展开箭头——`setExpandedGroup` 只通过父组的 `setExpansionStateOfGroup` 给子组置 `IsExpanded`，而路径根组无父组、其 `IsExpanded` 从不被置位，故表名层即便展开也无 chevron、且吃"未展开"的右推偏移；修复＝`setExpandedGroup` 显式管理根组 `IsExpanded`。② 子组（等级）比父组（表名）突出更多——`Panel.updateXOffset` 的突出量只看 expanded/selected/keyboard-selected、不看深度，键盘选中的子组反而比未选中的父组更靠左；修复＝新增 `Panel.AdditionalXOffset`（虚方法、默认 0，`PanelGroup` override 返回 `Depth*30`，`30>active_x_offset 25` 压过键盘选中偏移），使祖先组突出 ≥ 其键盘选中后代组、并略多一点。两处均只影响层级分组（mania 等 depth 0 → 零影响）。验证：`TestSceneBmsSongSelectDifficultyTable` 6/6（新增 `TestExpandedTableHeaderSharesExpandedStateWithLevel`）、BMS 全套 **917/917**、共享 `TestScenePanelGroup`/`PanelSet`/`PanelBeatmap` 10/10、Release 编译干净（最终拷贝步被运行中的游戏锁住=文件锁，非代码问题）。详见 [P1-I CHANGELOG](../subline/P1-I/CHANGELOG.md) 2026-06-18 与 [P1-I 约束 #15](../subline/P1-I/TECHNICAL_CONSTRAINTS.md)。

---

## 2026-06-16

### BMS 选歌展示层级下拉 + 层级返回条 + 难度表分组解析缓存（P1-I I5–I7）

承接对当前 BMS 选歌分组/展示链路的审查，落地三项（用户授权 detailed 实现）：① **展示层级**——新增 BMS-only 两档下拉「歌曲→谱面 / 谱面」（共享 sort/group/collection 行里「分组」与「收藏夹」之间的第 4 列，非 BMS 收 0 宽、行布局不变），把原先由 Sort/Group 隐式组合决定的折叠/扁平行为收口为显式 `FilterCriteria.DisplayLevel`（nullable）→ `BeatmapCarouselFilterGrouping.ShouldGroupBeatmapsTogether`；强制扁平的分组（难度表/内外库/`Group=Difficulty`/`Sort=Difficulty`/`RankAchieved`/`LastPlayed×LastPlayed`，提取为 `GroupingForcesStandaloneDifficulties`）下锁定为「谱面」并禁用下拉、且不污染持久化偏好；**mania 与其他 ruleset 零行为变化**（`DisplayLevel==null` 等价改写原启发式）。② **层级返回条**——大库多层分组下不必滚动找组头即可上退一级：`BeatmapCarousel` 暴露 `CurrentExpandedGroup` + `CollapseExpandedGroupOneLevel`（cursor 停在刚折叠的组、不重新展开，逐级上退），新增 `FilterControl.GroupNavigationDisplay`（复用 scoped-set banner 视觉但**状态独立**，只显示面包屑路径 + 返回 + `GlobalAction.Back`，scope 退出优先于层级退回）。③ **难度表分组解析缓存**——`BmsTableGroupMode` 按 `RulesetDataJson` 内容键缓存 `GroupDefinition[]`，消除每次 refilter × 每张谱面的全量 JSON 反序列化（correctness-neutral、stale-proof、有界）。④ **层级视觉区分**——共享 `PanelGroup` 按 `group.Depth` 高对比分级：根/表名层 = 更亮背景 + 三角纹理 + 大亮标题；嵌套/等级层 = 更深背景 + 纯平无纹理 + 更小更暗标题 + 缩进；非层级分组全为 depth 0、零影响。验证：`BmsTableGroupModeTest` 4/4、`BmsDisplayLevelGroupingTest` 3/3、`TestSceneBmsSongSelectDifficultyTable` 5/5、`TestSceneBmsFilterControl` 8/8、BMS 全套 **916/916**、共享 `TestScenePanelGroup` 6/6，`osu.Desktop.slnf` Release 0 错误（`TestSceneSongSelectGrouping` 中 6 条失败为既有 OMS 分歧：测试取已删除的 osu! 标准模式 OnlineID 0 致 `TestResources` `%0`，与本改动无关）。**人工视觉验收待用户实机确认**。详见 [P1-I CHANGELOG](../subline/P1-I/CHANGELOG.md) 2026-06-16「其二」与 [P1-I 约束「展示层级与层级导航约束」](../subline/P1-I/TECHNICAL_CONSTRAINTS.md)。

### 修复 BMS 选歌「谱面构成」过滤大曲库"失效" + Phase 2 backfill 性能/UX 全面收口（P1-I）

用户报「谱面构成」(RC/LN/SCR) 过滤在 ~5.8 万谱面库下完全无效，一整轮日志驱动调试后定位真因并收口，用户实测确认正常。**真因（用户 database.log 确诊）**：`BmsChartFilterStatsBackfill` 的 Phase 1 在 Realm `IQueryable` 上比较 link-traversal 属性 `b.Ruleset.ShortName`，Realm LINQ provider 翻译不了抛异常 → 被 `catch` **静默吞掉** → 缓存恒空 + `missingIds==0` 使 Phase 2 整体跳过 → 过滤全库 fail-open。修复＝`EnumerateBmsBeatmaps` 用 `.AsEnumerable()` 内存求值。**性能/UX 收口**：旧库（2026-05-11 导入持久化前）首轮 Phase 2 大规模补算曾致选歌卡顿——逐层定位到真瓶颈是逐张走 `BeatmapManager.GetWorkingBeatmap`（进程级 `lock(workingCache)` 与 UI 同抢、阻塞 update 线程），改为**直读 .bms 旁路**（`computeStatsDirect`）+ **轻量计数解码**（`ComputeFromDecodedChart`，与完整转谱可证等价）+ **批量写回**（~5万微事务→几百个）+ **一次性进度通知**（`ProgressNotification`）+ re-filter 20s 节流 + 诊断日志降 `Verbose`。**核心 API 变更**：`Ruleset.OnSongSelectSetup` 签名加 `Storage` + `INotificationOverlay` 两参（OMS 自有方法，非上游；`FilterControl` / `BmsNoteDistributionGraph` 从 DI 传入）。**沉淀的全局教训**：① Realm 查询禁止在 `IQueryable` 上比较 link-traversal 属性（先 `.AsEnumerable()`）；② 后台 `catch` 必须记日志（否则链路级崩溃表现为"无声失效"）；③ 大批量后台处理禁止逐张走 `GetWorkingBeatmap`（全局锁阻塞 UI，要批处理就直读文件）。06-15 当日"主因=回调竞态"判断被本轮推翻（仍是真实但次要缺陷，保留）。验证：BMS 全套 **910/910**（新增 3 条 backfill 回归）、`osu.Desktop.slnf` Debug 0 错误。详见 [P1-I CHANGELOG](../subline/P1-I/CHANGELOG.md) 2026-06-16 与 [P1-I 约束 #8–#15](../subline/P1-I/TECHNICAL_CONSTRAINTS.md)。

---

## 2026-06-15

### BMS 默认皮肤几何二调：宽度回宽 10%、SCRATCH = 键轨 2 倍、音符贴顶无空隙（视觉默认，P1-A）

承接上一条几何调整再按用户口径微调：① **整体宽度 +10%**——`PlayfieldWidth` 系数 `0.75 → 0.825`（即原始 ×0.75 后再 ×1.1，覆盖上一条的 0.75）；② **SCRATCH 轨 = 键轨 1.5 倍宽**——`ScratchLaneRelativeWidth` `1.25 → 1.5`（先定 2 倍，随即按口径再缩 25% 落到 1.5；lane 按相对宽归一化分配，scratch 占 1.5 份、key 占 1 份）；③ **音符贴屏幕顶、无空隙**——playfield 容器由居中改为 **顶部锚定**（`BmsPlayfield` 初始 + `applyPlayfieldStyle` 的 P1/P2/居中三态同步改 `Top*`），并 `PlayfieldHeight 0.9 → 0.95`，使顶边贴屏幕顶（音符从顶部出现）、底边/判定线仍停在 95% 屏高（位置不变）。判定时序不变（GN = 可见时间 = TimeRange，与场高无关）。同步更新测试：`BmsLaneLayoutTest`（14K 宽 0.6→0.66、高 0.9→0.95）、`TestSceneBmsPlayfieldLayoutConfig`（8 轨宽 0.36→0.396、高→0.95、scratch 1.25→1.5、实测 scratch:key 比 1.25→1.5、lane 高占比 0.9→0.95）。验证：BMS 全套 **907/907**、Release 0 错误。音符厚度 +25%、长条身宽 +25%（上一条）保持不变。

### BMS 默认皮肤：单轨/音符宽度 −25%、音符厚度 +25%、长条身宽 +25%（视觉默认，P1-A）

按用户口径调默认 BMS playfield/note 几何：① **所有单轨宽度 −25%**（音符随轨自动 −25%）——lane 物理宽 = 归一化占比 × `PlayfieldWidth`，故 `BmsPlayfieldLayoutProfile.CreateDefault` 的 `playfieldWidth` 默认乘 `0.75`（整条 playfield 连同 lane/音符按比例收窄 25%，不引入新间隙）；② **音符厚度 +25%**——`DrawableBmsHitObject` 的音符条高 `18 → 22.5`（覆盖普通音符与长条头/尾盖；长条父件的 `28` 是非时长 fallback、被滚动容器按 hold 长覆盖，不是可见厚度，保持不动）；③ **长条身宽 +25%**——`DefaultBmsLongNoteBodyDisplay.Width` `0.42 → 0.525`（相对 lane 宽）。**仅几何/视觉**，判定/计分/滚动链路不变。同步更新锁这些值的测试：`BmsLaneLayoutTest`（14K PlayfieldWidth 0.8→0.6）、`TestSceneBmsPlayfieldLayoutConfig`（8 轨 0.48→0.36）、`BmsSkinTransformerTest`（长条身宽 0.42→0.525 ×2）。验证：BMS 全套 **907/907**、Release 0 错误。

### BMS 默认皮肤：长条去掉尾端标识（视觉默认，P1-A）

按用户口径把 BMS 默认皮肤长条改成「无尾巴标识」样式：`DefaultBmsLongNoteTailDisplay`（长条释放端的全宽亮色端盖）现 `Alpha = 0`。长条 body（细半透明竖条）本就由父 drawable 按 hold 时长被滚动容器拉满整段，故隐藏尾盖后 body 仍延伸到释放端、不留空缺——头端亮盖 + body 延伸、无尾盖，符合目标样式。**仅视觉**：tail 仍是判定对象，`DrawableBmsHoldNoteTail` 判定/计分链不受影响；`LongNoteTail` skin 组件与 `GetLongNoteTail` 调色仍保留，皮肤作者仍可覆盖出自己的尾端表现。验证：`BmsSkinTransformerTest` + `BmsDrawableRulesetTest` **163/163**、Release 0 错误。

### 移除 BMS 常驻速度反馈卡（产品决定）+ 修复右侧 COMBO BREAK 计数器（P1-C / P1-A）

两项用户驱动改动。**① 移除常驻速度反馈卡** `DefaultBmsSpeedFeedbackDisplay`（即游玩中右上「GN/WN/FAST·SLOW/PAC/LIVE」卡）及其**专属反馈子系统**——玩家不再使用（按住调整键调挡板时浮窗已显示速度信息）。删前审查确认该卡是唯一显示载体、`BmsGameplayFeedbackState` 子系统仅服务于它。删除：卡 + `BmsGameplayFeedbackState`/`BmsTimingOffsetSparkline`/`BmsExScoreProgressInfo`/`BmsExScorePacemakerInfo`/`BmsJudgementCounts`/`BmsJudgementTimingFeedback`、`BmsSkinComponents.SpeedFeedback` 与 transformer 接线、`IBmsHudLayoutDisplayWithGameplayFeedback` 变体与 overlay 包装、`DrawableBmsRuleset` 的 `GameplayFeedbackState`/`LatestJudgementFeedback`/`RecentJudgementFeedbacks`/`TimingFeedbackVisualRange`/`ExScorePacemakerInfo` 暴露面与 pacemaker/timing 管线，连带对应测试。**功能影响**：游戏内不再有 FAST/SLOW 逐 note 计时、timing sparkline、EX pacemaker、EX progress、LIVE PERFECT/FC；判定计数改由右侧 7 计数器承担。**保留**：`SpeedMetrics`（pre-start 预览）、调整目标状态/lane cover focus、toast、BGA miss-flash、judgement 基线摆位。**② 修复右侧 `JudgementCounterDisplay` 的 COMBO BREAK 计数器游玩中恒为 0**：`HitResult.ComboBreak` 是 `BmsScoreProcessor` 派生统计（断连时旁路 +1），从不经 `NewJudgement` 按 type 流过，而 `JudgementCountController` 旧逻辑按 `judgement.Type` 自增；修复＝改为每次判定从 `ScoreProcessor.Statistics` 全量同步各计数器（统计先于事件更新，对 mania 行为中性）。**与上一条 6-15 条目的衔接**：上一条给 `DefaultBmsSpeedFeedbackDisplay` 补的无参构造已随本次整删作废，但 `SerialisedDrawableInfo.GetAllAvailableDrawables` 的「必须有公开无参构造」防御性收口独立保留、仍有效。验证：BMS 全套 **907/907**、`osu.Desktop.slnf` Release 0 错误（仅运行中游戏的 dll 拷贝锁，编译 0 错）。详见 [P1-C CHANGELOG](../subline/P1-C/CHANGELOG.md) 与 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md) 2026-06-15。

### 修复：皮肤布局编辑器在 BMS 下报错（`DefaultBmsSpeedFeedbackDisplay` 无无参构造）

用户实机日志暴露皮肤编辑器进 BMS gameplay 时两处报错，同一根因。**根因**：`DefaultBmsSpeedFeedbackDisplay`（P1-C 速度反馈卡）实现 `ISerialisableDrawable`，但唯一构造函数是**全可选参数**形式 `(IBindable?=null, IBindableList?=null)`——C# 允许 `new X()`，但 `Activator.CreateInstance(type)` 只认**真正零参**构造，全可选签名不匹配，抛 `MissingMethodException`。后果：① 编辑器组件 toolbox 反射枚举 BMS 程序集全部 `ISerialisableDrawable` 并逐个实例化时崩在它上（`SkinComponentToolbox.attemptAddComponent`）；② `SkinnableContainer.Reload` 会把 transformer 注入的 HUD 子件（gauge/combo/**speed feedback**）当作 `Components` 序列化进用户皮肤，重载时 `SerialisedDrawableInfo.CreateInstance` 重建该卡失败（其姊妹 `BmsGaugeBar`/`BmsComboCounter` 都有无参构造、能正常往返，唯独它不能）。**修复**：① 给 `DefaultBmsSpeedFeedbackDisplay` 补显式无参构造（链到现有双参构造，双参去掉可选默认值；唯一双参调用点是测试，零参调用点是 transformer，均不受影响）——既修 toolbox 崩溃，又让已存皮肤里的该卡按序列化位置自愈重建；② 防御性收口：`SerialisedDrawableInfo.GetAllAvailableDrawables` 增加"必须有公开无参构造"过滤，编辑器今后永不再把无法实例化的 `ISerialisableDrawable` 喂进 toolbox（对所有 ruleset 生效）。**未改**：`IsEditable` 保持与 gauge/combo 一致（默认可编辑），不改编辑器可选面语义；BGA 视频 `VideoDecoder faulted`、缺失 jacket 等日志属被预览谱面自身、非编辑器缺陷，且已优雅降级，本次不在范围。验证：`BmsSkinTransformerTest` + `TestSceneBmsSpeedFeedbackDisplay` 焦点 **121/121**，`osu.Desktop.slnf` Release **0 错误**。详见 [P1-A CHANGELOG](../subline/P1-A/CHANGELOG.md) 2026-06-15。

### BGA Phase 5.1：老式视频外部 ffmpeg 转码播放（opt-in；P1-L）

承接 BGA 落地后的实测——框架捆绑 FFmpeg 打不开老式 MPEG-1 `.mpg`（及 `.wmv/.avi/.flv`），此前回退静态图。新增 `BmsBgaVideoCache`（opt-in `BgaVideoTranscode` 开关，默认开）用**用户自备的外部 ffmpeg**（PATH 或放进数据目录；OMS 不分发）把这类视频后台转 H.264 `.mp4` 缓存到 `<dataRoot>/bga-video-cache/`，`BmsBgaPlayer` 预热转码 + 转好后本场热替换；无 ffmpeg/关闭/失败＝静态图回退（无回归），`.mp4` 谱不受影响。新增 `BmsBgaVideoCacheTest` 13；BMS **946/946**、Release 0 错。详见 [P1-L Phase 5.1](../subline/P1-L/) + [约束 5.1](../subline/P1-L/TECHNICAL_CONSTRAINTS.md)。实机端到端需用户装 ffmpeg 后验。

---

## 2026-06-14

### BGA 链路激活 + 归入 P1-L Phase 5（落地；自动化通过，人工视觉验收待办）

审查「BMS 模式游玩时 background image/animation 链路」后，把此前标为 **Phase 2 future-scope** 的 BGA 视频/动画**正式激活并归入子线 P1-L**（演出/Gimmick 谱视觉复刻），新增 Phase 5 并落地全链路。**问题定性**：解析层（P1-K）已完整产出 BGA 事件/定义，但转换层只取一个静态 `metadata.BackgroundFile`、BGA 时间线被丢弃；显示层 `BmsBackgroundLayer` 是静态占位件且挂在 `playfieldContainer` 内被不透明 lane 背板完全遮挡（14K DP 中缝坐实）。**落地**：转换携带 `BmsBeatmap.BgaTimeline`（`BmsBgaTimelineEntry`，复用 `eventTimes` + `BitmapTable`，照 `Mines` 不进 `HitObjects`；并补回 `buildEventTimeline` 漏注册的 BGA 事件时刻）；运行时 `BmsBgaPlayer` 在皮肤可定制浮窗 `BgaPanel`（挂 `DrawableRuleset.Overlays`，不被遮挡）按时间线合成图序列 + 视频（视频复用 osu!framework FFmpeg `Video`+`PlaybackPosition` 时钟同步），资源经 `WorkingBeatmap.GetStream` 直读 `chartbms/`（不经 hash store），POOR 层按 `#POORBGA` 在 miss flash，letterbox；默认布局镜像 playfield（P1→右/P2→左/居中→右/14K DP→中缝），`ShowBga` 开关，无 BGA 回退静态图；退役被遮挡的 `BmsBackgroundLayer` 挂点。仅 native 路径，converted-mania 不在 v1。**验证**：BMS 全套 **933/933**（新增 17）、`osu.Desktop.slnf` Release **0 错误 0 警告**；实机逐谱人工视觉验收交接人工。详见 [P1-L 四件套](../subline/P1-L/) Phase 5、[OMS_COPILOT §12](OMS_COPILOT.md)、[DEVELOPMENT_PLAN §2.10](DEVELOPMENT_PLAN.md)。

### 判定 parity 第 2 刀：溯源校正 beatoraja BAD 早/晚非对称（修复方向写反，**行为变更**）+ IIDX empty-poor 结论性收口（P1-C）

承接第 1 刀溯源 G3/G4。**G3＝真实 bug**：从 beatoraja 源码 `JudgeProperty.SEVENKEYS`（exch-bms2/beatoraja）取权威窗口——note BAD = `{…,-280000,220000}`µs（负=早/正=晚 → **早 280ms 比晚 220ms 宽**），scratch 290/230，LN release 280/220。OMS 的 PG/GR/GD 与 scratch/LN 基值逐项吻合，但 [BeatorajaJudgementSystem.cs](../../osu.Game.Rulesets.Bms/Scoring/BeatorajaJudgementSystem.cs) 把 BAD 早/晚**写反**（早 220/晚 280）；修复 = 四个 `createProfile` 改回 `(280,220)/(290,230)/(280,220)/(290,230)` + 补来源注释。**行为影响**：beatoraja 早击至 280ms 仍 BAD、晚击 BAD 收紧到 220ms（晚自动 miss 280→220）；EX-SCORE 存档不变、replay 按新窗口重判。属性显示层本就分早/晚读取，修后 `BAD hit window` 自动正确显示 `-早/+晚`（约束 #1 在该显示面已自动满足）。**G4**：IIDX 闭源、无权威 empty/excessive POOR 单值（审计仅述「note 前或后均可」），故 IIDX `500/150` 与 IIDX CN release 沿用 note 窗口**收口为标注清楚的 OMS 启发式，不宣称 parity**。测试同步（约束 #14 改窗口先改测试）：parity 契约改断言早窗更宽；三处既有锁旧值测试按新值更新（`BmsDrawableRulesetTest` ×2、`BmsRulesetStatisticsTest` ×1）。剩第 3 刀：把区分接进 gameplay judge display / counts。验证：parity **29/29**、BMS **916/916**、Release **0 错误**。约束见 [P1-C CONSTRAINTS #15/#17](../subline/P1-C/TECHNICAL_CONSTRAINTS.md)。详见 [P1-C CHANGELOG](../subline/P1-C/CHANGELOG.md) 2026-06-14。

### 判定 parity 第 1 刀：判定窗口 parity 契约测试 + 统一跨家族边界约定（行为不变；P1-C）

通读 BMS 判定链路后**修正一处文档失真**：`BEATORAJA` / `LR2` / `IIDX` parity **并非「全缺」**。基类已预留全部钩子，且 **IIDX `16.67/33.33/116.67/250`**、**LR2 四档 `8/24/40`·`15/30/60`·`18/40/100`·`21/60/120`（尾 200）** 已与 [../other/IIDX_REFERENCE_AUDIT.md](../other/IIDX_REFERENCE_AUDIT.md) 逐项吻合，**beatoraja** 已有 `25/50/75/100/125` 整数截断缩放 + early/late 非对称 BAD + scratch/release profile，excessive/empty poor 也已按家族参数化（LR2 `1000/0` 仅 note 前）。真正缺口收窄为：G1 跨家族边界约定不一致、G2 缺 parity 契约测试、G3 beatoraja 非对称溯源、G4 IIDX empty-poor / CN release 溯源。**第 1 刀只动 G1+G2（零行为风险）**：① [BeatorajaJudgementSystem.cs](../../osu.Game.Rulesets.Bms/Scoring/BeatorajaJudgementSystem.cs) 的 `Evaluate` 边界统一加 `+ BoundaryEpsilon`（保留其自有非对称 BAD 分支）；② 新建 [BmsJudgementSystemParityTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsJudgementSystemParityTest.cs)，按家族×rank 锁定窗口边界 / LR2 四档 / beatoraja 缩放档与非对称 / scratch·release 扩窗 / excessive-poor early-late 真值表 / 跨家族 inclusive 边界，audit-backed 数值锁基线、placeholder（IIDX `500/150`、beatoraja `220/280`）显式标注待溯源（第 2 刀输入）。约束见 [P1-C CONSTRAINTS #14–#17](../subline/P1-C/TECHNICAL_CONSTRAINTS.md)。验证：`BmsJudgementSystemParityTest` **29/29**、`osu.Game.Rulesets.Bms.Tests` **916/916**（887+29）、`osu.Desktop.slnf` Release **0 错误**（生产代码 0 警告）。详见 [P1-C CHANGELOG](../subline/P1-C/CHANGELOG.md) 2026-06-14。

---

## 2026-06-13

### 修复：BMS `Random` / `Mirror` 重排被重复应用（同一 playable beatmap 上 3 次 → 自定义 pattern 失真 / 地雷错位）

全面审查 BMS mode 下 `Random` mod 链路，定位一个长期潜伏的重排重复应用 bug。**根因**：`BmsModRandom` / `BmsModMirror` 都实现 `IApplicableToBeatmap`，`WorkingBeatmap.GetPlayableBeatmap` 已对其各应用一次（#1）；而 `BmsBeatmapModApplicator` 又在 `DrawableBmsRuleset` 构造（#2）与 `BmsScoreProcessor.ApplyBeatmap`（#3）对**同一个** playable beatmap 实例（`DrawableRuleset` 基类只强转不克隆）再各应用一次——同组 hitobject 的 lane 置换被叠加成 **P∘P∘P**。后果：① **自定义固定 pattern 失真**（招牌功能：非对合 pattern 得到 P³≠P，3-循环 pattern 直接复位成恒等 → 什么都不做）；② RANDOM/R-RANDOM/S-RANDOM 仍随机但偏离 seed 名义排列、且做 3 倍无用功；③ 难度计算只走 `GetPlayableBeatmap`（P）与游玩（P³）排列不一致；`Mirror` 仅因奇数次（reverse³=reverse）侥幸正确。旧测试只单次应用、且 custom-pattern 测试恰用全反转（对合）pattern，故全绿却掩盖了该 bug。**修复**：`BmsBeatmapModApplicator` 不再应用 `Mirror`/`Random`（lane 置换非幂等、会复合），交由 `GetPlayableBeatmap` 的 `IApplicableToBeatmap` 管线单次应用；applicator 仅保留幂等 state-setter（`A-SCR`/`A-NOT`）与必须落默认值的 LongNote/Judge。**附带修复（P1-L）**：地雷（`BmsBeatmap.Mines`，刻意在 `HitObjects` 之外）此前完全不随重排移动 → 与重排后谱面错位；现 `applyPermutation` 用同一 lane 映射同步重排地雷（覆盖 `Mirror`/`RANDOM`/`R-RANDOM`/custom；`S-RANDOM` 无列置换故地雷留原位）。**显示去重（UX）**：随机种子与自定义 pattern 并非功能重合（种子是 RANDOM 系的可复现锚点 + replay 确定性，pattern 是固定手动排列、命中即绕开随机与种子），但面板上三者并列会让人误以为种子在 pattern 下仍生效；`SettingDescription` 改为「填了有效 pattern 时只展示 pattern、不再并列 Random 类型/种子」（字段保留，种子不删）。**面板输入过滤 + 互斥提示（UX）**：自定义 pattern 输入框此前无任何过滤、也无互斥提示（用户实测可输入 `123123…` 这类非排列）。新增 `BmsRandomCustomPatternSettingsControl`（复合控件 = 过滤文本框 + 实时预览行）：① keystroke 级只接受 pattern 合法字符（数字 + 分隔符 `| / , ; -` + scratch 标记 `S`），过滤规则抽成 `BmsLaneRearrangement.IsCustomPatternCharacter` 与解析清洗同源；② 文本框下方**实时校验/预览行**——按当前选中谱面真实键数（`BmsRuleset.TryGetKeyCount` 读 `CircleSize`，无谱面时退化为按所填位数推断）校验，合法绿字「`{K}`K 谱面下，pattern 将变为 `xxx`」、非法红字「所填 pattern 不合法（每侧需为完整排列）」；校验逻辑 `BmsLaneRearrangement.TryNormaliseCustomPattern` 与解析的 `tryCreateCustomPatterns` 同源（含 **14K = 两段 1–7 排列、7 位镜像到两侧**，纠正"1–14"误解）；③ tooltip（SettingSource description，逐键数换行排版）列各键数示例（5K `54321` / 7K `7654321` / 9K `987654321` / 14K `7654321 1234567`）；④ placeholder「填写以覆盖 Random 类型与随机种子」。**未采用「禁用 type/seed 控件」**——实测置灰会让 `Mod.CopyFrom`/clone/反序列化经 `BindTo` 写入已禁用 bindable 时抛异常（这也是全 osu 无任何 mod 这么做的原因），且复合控件 `Current` 直接转发到文本框单一 bindable（预览只读）以杜绝绑定回环。**应用层**：非空但非法的 pattern **不再静默回退随机**，改为保持谱面不变（行为可预期、与"pattern 覆盖随机"语义一致）。验证：新增 9 个回归（含地雷随 Mirror/custom 移动、applicator 不再重排、描述去重、字符过滤、非法 pattern 不重排、`TryNormaliseCustomPattern` 各键数/14K 镜像/非法、设置控件可实例化冒烟）；BMS 全套 **887/887**、`osu.Desktop.slnf` Release 0 错误。约束见 [P1-L CONSTRAINTS](../subline/P1-L/TECHNICAL_CONSTRAINTS.md) Phase 1 #6。详见 [P1-L CHANGELOG](../subline/P1-L/CHANGELOG.md) 2026-06-13。

### 展示：BMS 选歌曲名清理 + 难度名"谱师显式名优先、丢冗余数字"（展示层，bms / 转谱两模式一致；P1-K）

继续审查 BMS 谱面在 bms mode 与转谱 mania mode 的 Song Select 信息展示链路（曲名/难度名/曲师/谱师）。两模式共用同一套 carousel 面板、`beatmap.Ruleset` 始终是 `bms`，故 4 字段两模式显示一致、无 per-mode bug；曲师/谱师原已正常。按用户口径修两处缺口（均**展示层**、不改存库、现有库免重导即生效）：① **曲名清理**——BMS 惯例把难度塞进 `#TITLE` 尾部括号（`GOODBOUNCE [ANOTHER]`），新增 `BeatmapLocalMetadataDisplayResolver.GetDisplayTitle`/`GetDisplayTitleUnicode`，**仅无 `#SUBTITLE` 时**切掉尾部成对括号（含全角/CJK）或 `-X-`/`~X~` 包裹，曲名只显示主体；② **难度名"谱师显式名优先、丢数字"**——此前 `DifficultyName` 含冗余 play-level 数字（无 `#DIFFICULTY` 时退化成裸数字，与星数重复），新增 `GetDisplayDifficultyName`，优先级 `#SUBTITLE`/标题括号 → `#DIFFICULTY` 类别标签 → 丢裸数字返回空。**谱师显式名必须压过 `#DIFFICULTY` 类别**——用户实测 `Dead Soul [Revive]`+`#DIFFICULTY 5` 被类别"Insane"盖掉真实名"Revive"（大量谱面真实难度名被 Normal/Hyper/Another 覆盖），初版误把类别放第一已翻转。接线 `PanelBeatmapStandalone`/`PanelBeatmap`/`PanelBeatmapSet`/`BeatmapTitleWedge`。验证：`BmsLocalMetadataDisplayResolverTest` **9/9**，`osu.Game` Release 编译 0/0。约束 [P1-K CONSTRAINTS #21/#22](../subline/P1-K/TECHNICAL_CONSTRAINTS.md)。详见 [P1-K CHANGELOG](../subline/P1-K/CHANGELOG.md) 2026-06-13。

### 修复：converted-mania 选歌键数显示错误（任意 keymode 坍缩成 6/7 → 采信权威 keymode；P1-K）

审查 `BMS -> mania` 转谱在 Song Select 的信息展示链路，定位用户实测的「部分谱面键数显示错误、错的五花八门」。**根因**：mania ruleset 下浏览 BMS 转谱时，carousel `[NK]` badge（`PanelBeatmap` / `PanelBeatmapStandalone` 的 `OnlineID == 3` 分支）与 wedge `KC` 难度属性条（`ManiaRuleset.GetBeatmapAttributesForDisplay`）都经 `ManiaBeatmapConverter.GetColumnCount` → `getColumnCount` 计算键数；该函数只对 `SourceRuleset.ShortName == "mania"` 直接采信 `CircleSize`，而 BMS 持久化 `BeatmapInfo` 的 ruleset 是 `bms`，遂落入为 osu!/taiko/catch→mania convert 设计的列数启发式（按 long-note 占比 + OD）。OMS 删除 osu/taiko/catch 后该分支**只会被 BMS 命中且必然算错**——任意 5K/7K/9K/14K 被坍缩成 **6/7**（随 LN 占比与 rank 跳变）。**修复**：单点改 `getColumnCount`——对 `bms` source 与 `mania` source 一视同仁直接采信权威 `CircleSize`（BMS 由 `BmsBeatmapConverter.populateMetadata` 写为 keymode 列数）；因 badge / `KC` 属性条 / `ManiaFilterCriteria` 键数过滤皆经 `GetColumnCount`，三处同纠。验证：新增 `BmsToManiaBeatmapConverterTest.TestSongSelectKeyCountUsesStoredBmsKeymodeNotConvertHeuristic`（5K/7K/9K_Bms/9K_Pms/14K 参数化），`BmsToManiaBeatmapConverterTest` **23/23**，`osu.Game.Rulesets.Mania` Release **0 错误 0 警告**。新增约束 [P1-K CONSTRAINTS K9 #18](../subline/P1-K/TECHNICAL_CONSTRAINTS.md)（与 #11 转换星级 display 同源）。详见 [P1-K CHANGELOG](../subline/P1-K/CHANGELOG.md) 2026-06-13。

---

## 2026-06-11

### 性能 / 代码：转谱-mania 游玩期帧抖动 + 偶发 ~220ms 冻结双确诊修复（P1-J J6；用户实测 ✅）

用户实测（原生 mania 顶级密度全程 1000fps 平稳、本谱密度远不及却抖）推翻「通用高帧 GC / 渲染预算」旧叙事后，经临时探针 `BmsGameplayStallDiagnostics` 三轮取证 + 代码审查确诊两条根因：① **帧抖动**（越后越抖、按键挂钩、休息段恢复、规律一顿一顿）= store 通道每键音触发都换 `Samples` 引用 → 每次跑 `SkinnableSound.updateSamples()` 全量重建 sample-drawable（实测 ~30KB/次中寿命对象）→ **gen1 晋升风暴**（gen0:gen1 锁死 1:1、~100 次/秒、15–30ms 帧尖峰）；原生 mania 音符持久 sound 重播零重建，故同密度原生不抖。修复 = 通道**同样本快路径**（同槽重触发零重建、per-WAV cut 语义不变）；用户同谱实测密集区 maxFrame 15–30ms→**5–10ms**、全程稳定 ✅。② **偶发 ~220ms 冻结** = 开局段阻塞 gen2 全量 GC——玩家模式无键音预热、全场 362 个 WAV 游玩中冷解码（探针抓到 3 次 STALL+GEN2 全在前 30s）；修复 = **keysound prewarm 放开到玩家模式**（BMS 原生 + 转谱两侧对等，对齐 LR2/beatoraja 全量预载，加载期变长属预期取舍）。验证：Release 0 错、焦点 78/78、BMS 871/871×2、mania 22/22；**用户同谱回归实测 ✅（游玩中 stalls=0、阻塞 gen2=0、maxFrame 2–12ms @ ~1000fps，判定「合格」）**。P1-J CONSTRAINTS #7 重写、#10 D 项重写（作废「疑渲染」框架）。详见 [P1-J CHANGELOG](../subline/P1-J/CHANGELOG.md) 2026-06-11。

---

## 2026-06-10

### 性能 / 代码：转谱 tap KEY note 改池化（经 mania 自有接口路由 shared store，消除非池化 drawable 代价；行为不变；P1-J J6）

对 `BMS -> mania` 转谱**游玩期**性能面做审查（帧数/延迟向）。结论：转谱-mania 的 drawable 策略与**原生 BMS** 基本持平（均非池化、BGM 均为隐形 drawable），唯一相对**原生 mania**更重处 = **每个转谱 tap KEY note 一个常驻非池化 drawable**（`loadObjects` 即全部构造、整局常驻 → 加载/内存/GC 大堆代价，疑 once-per-run 致命卡顿来源）。落地 P1-J CONSTRAINTS #10 早标注的「正解」：在 mania 定义自有接口 `IManiaKeysoundStore`/`IHasManiaKeysound`，让转谱 KEY note 退回**池化 `DrawableNote`**（转谱 drawable 工厂不再认领它 → `CreateDrawableRepresentation` 返回 null → 框架基类型池回退命中 mania `Note` 池），其 `PlaySamples` 经接口把键音交给 hosted `BmsKeysoundStore`（额外按接口 `CacheAs`）；删 `DrawableBmsConvertedKeyNote`。**音频语义零改动**（命中时同时机调用、同 store、同 per-WAV cut）。同轮经 `KeysoundSample` 补回 BGM/scratch 的 autoplay 预热缺口（bgm1 置空 `Samples` 后丢失、仅此一途）并修正失真注释。**明确后置（须先 profile，避免拿已验证音频修复赌投机微优化）**：BGM/scratch 调度器化（消除每帧隐形 drawable）、store 128 floor 下调（会回归长 BGM 偷断修复）、稳态高密段渲染。验证：`TestSceneBmsToManiaKeyNoteStoreRouting`（#10(b) player-level harness）**2/2**、`BmsToManiaBeatmapConverterTest`+`TestSceneManiaModAutoplay` **22/22**、`osu.Game.Rulesets.Bms.Tests` **871/871**、完整 mania **778 通过**（4 个 `TestSceneAutoGeneration` HoldNote 失败经 `git stash -u` 回基线对照确认为**既有失败**、非本轮引入）、`osu.Desktop.slnf` Release **0 错**。详见 [P1-J CHANGELOG](../subline/P1-J/CHANGELOG.md) 2026-06-10。

### 性能 / 代码：BMS→mania 转谱链路审查落地两项（库级冗余消除，行为不变；P1-K）

对 `BMS -> mania` 转谱性能面做审查，落地两项**行为不变**的冗余消除：① **转谱器移除 mania 星级自算**——`BmsToManiaBeatmapConverter.ConvertBeatmap` 此前每次转换都自跑一遍完整 `ManiaDifficultyCalculator` 写入 `BeatmapInfo.StarRating`，但所有消费面（`BeatmapDifficultyCache.computeDifficulty` / `BackgroundDataStoreProcessor` 启动批处理 / 导入期持久化）都在其后用 `ManiaDifficultyCalculator` 重算并持久化、从不读转谱器那次结果 → 持久化路径 strain **算两遍**（5.7万 库启动重处理时翻倍）、gameplay 加载期凭空多算一遍无人消费；删除后星级唯一归 `ManiaDifficultyCalculator`/难度缓存（与上游 `ManiaBeatmapConverter` 一致，持久化行为不变）。② **keysound 样本按 WAV 槽号 memo**——`BmsBeatmapConverter.createKeysoundSample` 此前对每个音符/LN 头尾/BGM/不可见对象重跑文件名规范化 + 分配，改为 per-conversion 按 `KeysoundId` 缓存（同槽只物化一次，O(音符数)→O(distinct 槽数)，减 dense 谱加载期 CPU/GC）。同次评估**暂缓** `buildEventTimeline` 重写（时序热路径常数因子优化、无 profiler 证据，应由 profiling 触发）。验证：`BmsToManiaBeatmapConverterTest` **18/18**（移除 1 个失效的「转谱器自算星级」契约用例）、`osu.Game.Rulesets.Bms.Tests` **871/871**、`osu.Desktop.slnf` Release **0 错误 0 警告**。新增约束 [P1-K CONSTRAINTS K9 #17](../subline/P1-K/TECHNICAL_CONSTRAINTS.md)（禁止转谱器自算星级）。详见 [P1-K CHANGELOG](../subline/P1-K/CHANGELOG.md) 2026-06-10。

---

## 2026-06-08

### 代码 / 测试：**解决转谱-mania「按 key1 触发 bgm1 / 胡乱按键长音重叠 / 暂停不停」**——真凶 = mania 按键音效反馈（用户多谱实测确认 ✅，P1-J）

承接 2026-06-07 移交的最高优先级 bug（用户坚称转谱-mania 下按 key1 反复触发 `bgm1.ogg`、多按重叠、暂停不停）。经谱面三重重解析 + 解码/转谱链通读确认**解析与转谱完全正确、无键音/BGM 粘连**，再用分层来源埋点 + 哨兵静音隔离实验 + 最底层 `PoolableSkinnableSample.Play()` 调用栈探针定位**真凶**：mania `Column.OnPressed` **每次按键都调 `GameplaySampleTriggerSource.Play()`**（按键音效反馈），它播放**本列下一个对象的 `Samples`**、用自己一池非循环且不受 store 暂停管的 sound；而转谱 BGM/scratch sample-only 对象被钉在可玩列（BGM→column 0）、其 `Samples` 装着键音（bgm1）→ 按 key1 即经反馈播 bgm1、反复按重叠、绕开 store 故暂停不停。**一个根因解释全部现象**，也解释了为何 store/hit-object 埋点全抓不到。**修复**：`BmsToManiaBeatmapConverter` 把 `BmsConvertedBgmSampleHitObject`/`BmsConvertedScratchSampleHitObject` 的 `Samples` **置空**（它们经 shared `BmsKeysoundStore` 用 `KeysoundSample` 自动发声，`Samples` 多余且只会被按键反馈错误取用）；改动只在转谱器、不碰 osu 核心。**作废本会话先后基于错误诊断的尝试**（store「orphan-on-reuse」已回退、LN-head 已否定、Track/preview 泄漏与"长 BGM 无法 resume"属独立后置问题）。验证：用户真实 app 实测「按 key1 不再触发 bgm1」并在其他原本同问题谱面一并确认；`BmsToManiaBeatmapConverterTest`（含新回归守卫「BGM/scratch `Samples` 必空、键音在 `KeysoundSample`」）**19/19**、`osu.Game.Rulesets.Bms.Tests` **871/871**、`osu.Desktop.slnf` Release **0 错**。详见 [P1-J CHANGELOG](../subline/P1-J/CHANGELOG.md) 2026-06-08。

---

## 2026-06-07

### 代码 / 测试：BMS→mania 转谱音频链路审查 + **autoplay/游玩 BGM 人声丢失真因确诊修复（用户实测确认 ✅）**；同日落地 prewarm + store 通道 floor（P1-J J6）

对 BMS→mania 转谱音频链路（BGM 事件 + 键音事件）做完整静态审查，定性为冷热不均两条播放路径：BGM/scratch 走共享 `BmsKeysoundStore`（直接用 sample 对象播放、**绕过惰性 LoadSamples**、per-WAV cut、pause/seek 停、常驻通道池），KEY note/LN head 走 mania 一次性 `PlaySamples`（**依赖惰性 `LoadSamples`**、无 per-WAV cut）；BMS 原生则全键音走 store + autoplay 预热全样本。识别**三条确定结构缺陷**并修复其中两条（均不动对象模型、对齐 BMS 原生）：① **mania 转谱此前完全无 keysound prewarm** → `DrawableManiaRuleset.LoadComplete` 在 converted-BMS + autoplay 下遍历 `Samples`/`NodeSamples` 预热全部 keysound 的 sample pool（对齐 BMS 原生 `PrewarmKeysounds`）；② **转谱 store 固定 32 通道** → `BmsToManiaKeysoundStoreFactory.Create` 改为 `Math.Max(config, 128)` floor（见下方真因）。第三条（跨/同路径无 per-WAV cut = 转谱键音重复）仍后置——唯一修复路径「note/LN 走 store」此前两次运行时回归、须先补 player-level 集成测试。**autoplay/游玩 BGM 人声丢失：真因确诊并修复（用户实测 autoplay+游玩均恢复 ✅）**——后续用户给出「单文件 bgm1、开头正常播、第 16s 人声才消失」的决定性线索，静态解析谱面确诊：是单个长 BGM `bgm1`（measure 1 单次触发、712KB；该谱 channel-01 共 4032 事件、并发峰值 27–36 > 32）被 `BmsKeysoundStore` 32 通道饱和时**轮转偷取掐断**（长 BGM 占通道最久最易被偷），**非** prewarm、**非**惰性 `LoadSamples`、**非** KEY-note 路径（争议人声是 BGM bgm1 长样本，那些方向全部作废）；修复=mania 转谱 store 通道 floor 128（`Math.Max(config, 128)`，远超峰值 → 长 BGM 永不被偷，用户调更高 config≤256 仍生效）。**#1 转谱键音重复（跨路径 per-WAV cut）是独立遗留、仍后置。**排除误区：playback discrepancy 只 log 不 Seek、不会掐断 store BGM。验证：`osu.Desktop.slnf` Release **0 错误 0 警告**、mania autoplay + 转谱 + drawable focused **30/30**、完整 `osu.Game.Rulesets.Bms.Tests` **869/869**；**用户实测 2026-06-07：autoplay 与游玩下 bgm1 第 16s 人声均恢复 ✅**。详见 [P1-J CHANGELOG](../subline/P1-J/CHANGELOG.md) 2026-06-07。

## 2026-06-01

### 代码 / 测试：修复 mania autoplay 整类长条不按（原生 mania 与 BMS→mania 转谱同坏；P1-K K9 #12）

审查 mania autoplay 链路查实回归：autoplay **完全不处理长条**，原生 mania 谱与 BMS→mania 转谱同坏（两者共用 `ManiaAutoGenerator`）。根因：K9「autoplay 跳过 ignore-only sample 对象」契约在 `ManiaAutoGenerator.canParticipateInAutoplay` 里实现为只看顶层对象自身的 `MaxResult.AffectsCombo()`（随 `4aa76f0` P1-L WIP 快照落入），而 mania `HoldNote` 自身是 `IgnoreJudgement`（`IgnoreHit`，不计 combo，combo 落在嵌套 `HeadNote`/`TailNote`）——谓词不下探嵌套，于是每条长条被整体跳过、不生成按/放帧。并列的 note-lock `OrderedHitPolicy` 同按 `AffectsCombo` 过滤却正确，因为它额外遍历嵌套对象。该回归随 `4aa76f0`（只验 BMS 套件、未跑 mania 套件）漏网，上游既有的 `TestPerfectScoreOnShortHoldNote` 当时已静默失败。修复：谓词改 nested-aware（self 或任一嵌套对象影响 combo 即参与），对齐 note-lock 语义；长条复活、sample-only 对象（无嵌套）仍被跳过。`TestSceneManiaModAutoplay` 4/4（新增长条+BGM sample 共存用例）、`osu.Desktop.slnf` Release 0 错误 0 新增警告；**2026-06-01 用户人工实机验证确认 mania 原生谱与 BMS→mania 转谱在 mania mode 下长条 autoplay 均正常**。详见 [P1-K CHANGELOG](../subline/P1-K/CHANGELOG.md) 与 CONSTRAINTS #12。

### 代码 / 测试：BMS -> mania 转谱 BGM/autoplay 音频补全落地（K11；dense-BGM 性能 J6 后置）

审查 `BMS -> mania` 单向转谱的非键音音频链路，查实一处音频保真缺口并完成补全规划：BGM（autoplay channel `0x01`）在转 mania 时被静默丢弃——`BmsToManiaBeatmapConverter.ConvertHitObject` 无 `BmsBgmEvent` 分支，base converter 将其作为非 `ManiaHitObject` 丢弃。对纯键音 BMS（无完整 master 音轨、`AudioFile` 仅 preview/空），mania 转谱因此只剩可击打 note 的键音、丢掉鼓/贝斯/铺底/人声等背景层。该缺口在 mania ruleset + `ShowConvertedBeatmaps` 实际游玩 BMS 时发生（`AllowGameplayWithRuleset` + `DrawableManiaRuleset.CreateDrawableRepresentation`），非仅 star 计算用途。已 de-risk：样本源与游玩 ruleset 无关（`WorkingBeatmapCache` 的 `FilesystemBackedBeatmapResourceProvider` 按 BeatmapSet 建、指向 `chartbms/`），BGM 用同型 `BmsKeysoundSampleInfo` piggyback、无需新样本源。连带规划 LN 尾键音 mania 对齐（消除与 BMS「长条只头发声」的分歧）。归线：转谱语义 + 尾对齐归 P1-K K11，mania-runtime 播放保真与 dense-BGM 性能归 P1-J J6。**K11 已落地（converter 侧）**：新增 BGM sample-only 对象/drawable + 转谱器 BGM 分支 + LN 尾 node sample 置空；`BmsToManiaBeatmapConverterTest` 17→**19/19**、完整 `osu.Game.Rulesets.Bms.Tests` **869/869**、`osu.Desktop.slnf` Release 0 错误 0 新增警告。**J6 首版亦已落地**：转谱 BGM/scratch 改走复用的 `BmsKeysoundStore`（`DrawableManiaRuleset` 反射宿主 + `CreateChildDependencies` 缓存 + `load` 挂载到游玩树），暂停/seek 由 store 统一 `StopAllPlayback`（修用户反馈的「暂停不停 BGM」）、通道有上限（原意缓解 dense 卡顿），store 缺席安全回退 per-object 播放。**用户实测：E（暂停停 BGM）已修复 ✅、B 的 scratch double 消失 ✅、普通 mania 无回归 ✅；D（dense 极端谱高密段仍极度缓慢）未解、后置**——共享 store 已排除「音频对象数」为主因，真瓶颈（疑 drawable 数量/转换/渲染）待 profile。残留：mania `Note`/`HoldNote` 自身键音仍 per-drawable。详见 [P1-K CHANGELOG](../subline/P1-K/CHANGELOG.md) 与 [P1-J CHANGELOG](../subline/P1-J/CHANGELOG.md) 2026-06-01。

## 2026-05-31

### 代码 / 测试：难度表全 Unrated 真根因订正 —— 共用 RulesetData 列互相覆盖（P1-H）

实机验证推翻同日早先的 staleness 判断。**真根因**：转谱星数（osu.Game `BmsPersistedMetadataData`：`converted_star_ratings`）与难度表（BMS `BmsBeatmapMetadataData`：`difficulty_table_entries`/`chart_filter_stats`）**各自定义容器类却共用同一个 `BeatmapMetadata.RulesetData` 列**；`SetRulesetData<T>` 整体覆盖 + Newtonsoft 默认丢弃未知字段 → 互相抹掉对方独有字段。转谱星数重算冲掉难度表 entries（全 Unrated，用户实测重算 11336 后即复现）；难度表回写冲掉星数 → 启动判 missing → 重算 → 再冲难度表，破坏性 ping-pong（"有概率"取决于最后谁写）。**修复**：两容器类均加 `[JsonExtensionData]` 往返保留对方字段（`IsEmpty` 计入扩展字段，避免置空连带抹掉对方），双向回归测试已加。同时**撤销**上一条的 per-set `DifficultyTableRevision` bump —— 大库下一次开关表命中数千 set，per-set re-detach 致 UI 卡死 1~2 分钟（用户实测）；改为中途开关表需重启反映、启动恒正确。详见 [P1-H CHANGELOG](../subline/P1-H/CHANGELOG.md) 与 CONSTRAINTS #22。验证：BMS **869/869**、`BmsStarRatingResolverTest` **13/13**、`osu.Desktop.slnf` Release 0 错误；用户实机重启后分组恢复正常。

### 代码 / 测试：修复难度表分组「会话中途启用/刷新后全落 Unrated」+ 回写架构收口（P1-H）

> ⚠️ **本条为初判，主因判断已被上一条订正推翻**：staleness 非全 Unrated 主因；per-set `DifficultyTableRevision` bump 已撤销（大库卡死）。注入全局 `RealmAccess` / 异步化 / MD5 归一化 / 递归保护 / 去 `GetSources().Single` 等改动保留有效。

全面审查 BMS 难度表链路后定位并修复一个用户实测 bug：按难度表分组时谱面有概率全落 `Unrated`。根因是 carousel 缓存陈旧——回写改的是 `BeatmapInfo.Metadata.RulesetData`（`BeatmapSetInfo → Beatmaps → Metadata` 下 2 层深 link 属性），而 `RealmDetachedBeatmapStore` 只浅层订阅 `All<BeatmapSetInfo>()`（`RegisterForNotifications` 不带 keyPaths），深层变更不触发 re-detach；会话中途变更后 carousel 持旧快照，完全重启才恢复（故表现"有概率"）。MD5 大小写经查不是根因（两侧均小写）。

主修（用户拍板「精准增量刷新」方案）：新增 `BeatmapSetInfo.DifficultyTableRevision`（realm schema 54→55，纯新增字段），回写 metadata 同事务内对实际变化的 set bump 该标量字段，强制集合 modified → carousel 增量 re-detach 重组，无需重启。

并连带收口审查暴露的回写架构与健壮性问题：
- 回写不再 `new` 第二个 `RealmAccess`（消除其构造期 `cleanupPendingDeletions` 越权物删谱面集 + 与全局实例时序竞争），改为注入全局 `RealmAccess`（`GetShared/GetSharedAsync` 增 `RealmAccess` 参数，settings / first-run 反射桥 / importer 同步接线）。
- `SetSourceEnabled` / `RemoveSource` 异步化，启用/禁用/移除不再在 UI 线程同步回写 + 全表扫描。
- 回写匹配归一化 `MD5Hash` 大小写（防御，不放宽 identity 口径）；`loadTableSource` 递归加深度上限防 HTML 环路；`import`/`refresh` 去掉末尾 `GetSources().Single` 全量重查（`RefreshAll` 由 O(N) 次全表重载降为常量）。
- 详见 [P1-H CHANGELOG](../subline/P1-H/CHANGELOG.md) 与 TECHNICAL_CONSTRAINTS #20/#21。

验证：难度表相关 **23/23**、导入集成 **26/26** 通过；`osu.Desktop.slnf` Release 0 警告 0 错误。carousel 中途刷新待用户人工确认。

### 代码 / 测试：修复缺省 `#LNTYPE` 长条被整条丢弃（P1-K，解码器）

承接 2026-05-30 键音链路审查：用户实测把"键音截断"精确定位到 `GOODBOUNCE [A]` 等少数差分的**少键 + scratch 人声截断**，最终查实是**解码器层**——这些谱用 5X/6X 长条通道却省略 `#LNTYPE`，而 OMS 把缺省（`null`）当作"不支持"整条忽略（实测丢 31 条长条，含承载 vocal 收尾段 `voice1 (4)` 的 scratch 长条 → 听感即"念到 f 就断"）。这与 [BMS_FORMAT_REFERENCE §5](../other/BMS_FORMAT_REFERENCE.md) 写明的「`#LNTYPE 1` 为默认」冲突。修复：`handleLongNoteChannelEvent` 改用 `LongNoteType ?? 1`。详见 [P1-K CHANGELOG](../subline/P1-K/CHANGELOG.md)。

并连带闭合两处（P1-J）：
- 长条恢复后用户实测 scratch 长条出现 "stomp your fee feet"——LNTYPE1 尾对象重复头 WAV，OMS 此前会播尾 keysound 与头叠 double。已让长条尾静音（`DrawableBmsHoldNoteTail.PlaySamples()` 空实现，对齐 LR2/beatoraja「长条只头发声」）。
- 复盘前两轮（误判为播放层截断）的副作用时，把 per-WAV cut 的归组粒度从**文件名**收紧到 **WAV 槽号（`KeysoundId`）**：避免错误掐断"多槽同文件做自重叠"的谱（对齐 LR2/beatoraja）。pressed-POOR 出声经确认保留，idle-first / shrink dispose 为净改进。

验证：完整 `osu.Game.Rulesets.Bms.Tests` **866/866**（Debug），Release 0 警告 0 错误。

## 2026-05-30

### 代码 / 测试：BMS 键音链路审查修复（P1-J）——消除两处"截断"

bms-play 键音链路审查定位并修复：
- **通道分配提前截断**：`BmsKeysoundStore` 原始纯 round-robin 在仍有空闲通道时也会回收正在播放的通道，长样本（尤其 layered BGM）被提前切断。改为 idle-first（仅在真正复音饱和才轮转偷取），空闲集每帧重建、`getNextChannel` 保持 O(1)，不回退 dense-chart 热路径。
- **pressed-POOR 静音**：按了键但判 POOR/miss 的 note 此前完全无声（基类只在 Hit 状态播 keysound），偏离 IIDX/LR2/beatoraja"按键必出声"。改为非命中按键在 key-down 补播该 note keysound（含 LN head），clean hit 不 double，未按键自然 miss 与 tail release miss 仍静音。
- 附带修复 live channel shrink 不 dispose 的脱挂 sound drawable。
- 详见 [P1-J CHANGELOG](../subline/P1-J/CHANGELOG.md)。验证：完整 `osu.Game.Rulesets.Bms.Tests` **862/862**（Debug），Release 构建 0 警告 0 错误。

### 调查与决策：C# Dev Kit 误把 osu.Game 当测试工程（确认良性，接受现状，不改功能）

现象：C# Dev Kit Test Explorer 反复对 `osu.Game.dll` 起 VSTest testhost 并中止发现；切 `osu.Desktop.slnf`、`Developer: Reload Window` 均无效。

根因（实证定位，先后排除两条错误假设）：
- **不是** `<IsTestProject>` 没设——MSBuild 实测求值确为 `false`，Dev Kit 的 Project System 不采纳该属性。
- **不是** deps.json 里的 `nunit.framework.dll`——把它从 `osu.Game.deps.json` 移除（`NUnit` 改 `ExcludeAssets=runtime`）后 Dev Kit 照样选中 osu.Game。
- 真因：Dev Kit 的 Project System 按"**是否引用 NUnit 包**"把工程归类为测试容器。osu.Game 为给 `*.Tests` 提供抽象测试场景基类（`osu.Game/Tests/**` 的 `BeatmapConversionTest` / `DifficultyCalculatorTest` 等）而引用 NUnit，于是被误归（slnf 日志"3 projects added" = BMS.Tests + Mania.Tests + 被误判的 osu.Game）。

为何不修（两条路都不可取）：
- "让 testhost 不崩"：试过 `CopyLocalLockFileAssemblies`（依赖闭包落 `osu.Game/bin`），它修掉第一道坎 `AutoMapper`，但 Dev Kit 紧接着撞第二道坎 `Microsoft.TestPlatform.CommunicationUtilities`——testhost **平台自身**缺失（osu.Game 不引用 `Microsoft.NET.Test.Sdk`）；继续补下去等于把 osu.Game 变成完整测试工程——**该尝试已回退**。
- "阻止选中"：只能移走 NUnit 包引用，需迁出 7 个上游文件，违背少改上游红线。
- 设置层面：无干净的 Dev Kit/VS Code 开关可单独屏蔽该源。

**结论：接受良性现状。** 该错误只波及 osu.Game 这个伪测试源，**不阻断真实测试**——同一轮日志里 BMS.Tests（860）与 Mania.Tests（780）经 `NUnit Adapter 4.6.0.0` 正常发现；Test Explorer 内真实用例可正常跑，CLI `dotnet test` 一向不受影响。忽略 Test Explorer 里 osu.Game 那个失败节点即可。

- 唯一落库改动：[../../osu.Game/osu.Game.csproj](../../osu.Game/osu.Game.csproj) 在 `<IsTestProject>false</IsTestProject>` 注释上补说明，**无任何功能/构建产物变化**。

## 2026-05-29

### P1-L Phase 2（Step A–C）：BMS 专用滚动位置积分旁路落地（门控默认 OFF）

落地 beatoraja 风格逐对象位置积分旁路，让 DEAD SOUL [Revive] 的「瞬移 snap / STOP 真冻结 / measure-length 任意定高」成立。**绕开而非改写**共享核心：新增 BMS 专用 `BmsScrollProfile`（converter 并行积分原始未钳制 `D(t)`）+ `BmsStopMotionScrollAlgorithm : IScrollAlgorithm`，经 `BmsPlayfield.CreateChildDependencies` 重缓存的 `BmsScrollingInfo` 注入——**不动 `TimingControlPoint` 钳制、不动 `ScrollingHitObjectContainer`、零核心文件改动**。门控 `BmsGimmickScrollMode` 默认 `Off`（设置面板可切 On/Auto），**判定/计分继续走时间链路、语义不变**。标定实测：DEAD SOUL `GetMostCommonBeatLength`=6（STOP 冻结占 43%）证实正常链路 squash，但默认 Normal hi-speed 模式 `timeRange` 与之无关 + profile base=132 → **Normal 模式零标定即忠实**。详见 [../subline/P1-L/CHANGELOG.md](../subline/P1-L/CHANGELOG.md)。

- 验证：新增 `BmsScrollProfileTest`/`BmsStopMotionScrollAlgorithmTest`/`BmsScrollingInfoTest` + 扩 `BmsBeatmapConverterTest`；BMS 全套 **854/854**；`osu.Desktop.slnf` Release 0 错误、生产代码 0 新增警告。正常链路无回归：OFF 时算法逐实例跟随基类（单测锁定）+ Player 系 gameplay TestScene 全绿。
- Step D（2026-05-29 续）：演出谱自动检测 `BmsScrollProfile.IsStopMotionGimmick`（`MaxSlope≥50 || FrozenFraction≥0.05`，保守区分特效/变速谱与正常 soflan）已落地。**门控默认改为 `Auto`**（用户拍板）：特效/变速谱开箱即用，正常谱不命中检测、零改动；`Off` 为回退开关，设置文案改发行向。默认 Auto 下正常谱无回归依赖检测无误报（保守）+ `Off` 兜底。
- 待办：DEAD SOUL 逐帧人工视觉验收（Phase 4）、Floating/Classic 绝对刻度标定、负向滚动（Phase 3）。
- 附带修复（人工验证发现）：Phase 1 地雷既有 off-by-one——`buildMines` 用键数而非轨道数做 lane 上界，导致最右键轨道（7K lane 7 / 5K lane 5 / 14K lane 14,15）地雷被误丢；新增权威 `BmsRuleset.GetLaneCount`（`BmsLaneLayout` 委托之），加 `TestBuildsMineOnRightmostKeyLane` 回归。

### P1-L 建线 + Phase 1：BMS 演出/Gimmick 谱地雷视觉呈现

新增独立子线 `P1-L`（由 [../other/BMS_GIMMICK_CHART_RENDERING.md](../other/BMS_GIMMICK_CHART_RENDERING.md) 可行性分析升级而来），目标是尽可能忠实复刻 DEAD SOUL [Revive] 这类**演出/观赏谱**。机理结论：这类谱是**定格动画**（132 万 BPM 瞬移 + measure-length 摆位 + STOP 定帧 + 大量地雷作像素，全谱无负值），osu! 前进式滚动 + `BeatLength` 钳制会压扁极端反差，故需 beatoraja 风格的专用滚动旁路（Phase 2 核心）。

**Phase 1（地雷视觉，已落地，完全隔离）**：此前地雷（channel D/E）解码后从不渲染；本轮把地雷渲染为可视、非判定对象，**仿小节线模式**——`BmsMine` 用 `IgnoreJudgement`、不进 `beatmap.HitObjects`、由 `BmsPlayfield.addMines` 直接加到对应 lane，`DrawableBmsMine` 非 `DrawableBmsHitObject`。**零滚动模型改动、零判定/计分改动、零正常游玩链路风险。** 详见 [../subline/P1-L/CHANGELOG.md](../subline/P1-L/CHANGELOG.md)。

- 验证：`BmsBeatmapConverterTest` **16/16**（新增"地雷不泄漏进判定路径"回归）；BMS 全套 **831/831**；`osu.Desktop.slnf` Release 0 错误、生产代码 0 新增警告。
- 已知限制：Phase 1 仅让地雷可见；DEAD SOUL 的"瞬移定格"仍需 Phase 2 专用滚动旁路才能忠实复刻。

### P1-K：解析 → 谱面/音乐/键音呈现链路审查后的全量修复（含性能优化）

对 BMS「解析 → 谱面/音乐/键音呈现」全链做了一轮审查，把链路正确性/保真 bug 与两处性能问题一次性修完；纯功能新增项（负 BPM 反向滚动、独立 10K 键位、地雷可玩化、`#SPEED`/文本/`#CHANGEOPTION` 建模）显式后置 backlog。详细切片见 [../subline/P1-K/CHANGELOG.md](../subline/P1-K/CHANGELOG.md) 2026-05-29 条目，约束见 [../subline/P1-K/TECHNICAL_CONSTRAINTS.md](../subline/P1-K/TECHNICAL_CONSTRAINTS.md)「键音呈现与控制流约束」。

- **BGM 叠层不再丢失（高优音频保真）**：channel `0x01` 此前被同位去重，同一时刻多条 `#xxx01` 并行 keysound 只剩一个；现 BGM 永不复合，叠层全部保留。
- **控制流修复错谱**：`#RANDOM` 增量支持 `#ELSE`/`#ELSEIF`/`#SETRANDOM`（修复 `#IF 1` 命中时 `#ELSE` 内容被并入），新增 `#SWITCH` 家族确定性单段选择（修复多 case 被无条件叠成错谱）；`#RANDOM` 默认仍只跑 `#IF 1`。
- **亚 1 BPM 保真**：`getBeatLength` 不再把 `0<bpm<1` 钳为 1 BPM（对象时序修正）。
- **键音轨保真**：空键/误击 keysound 改由 converter 期构建的 per-lane 时间线驱动，消费此前被丢弃的不可见对象（channel 31-49），修复开局静音，改为基于谱面而非判定。
- **性能**：LNOBJ 头移除由 O(n²) 改 O(n)；轨键音解析由每次 O(n) 扫描改二分 O(log n)。
- **显式保留**：STOP 同拍位（T+D）经审查确认为 K3-A 既有契约，未改。
- **验证**：`BmsBeatmapDecoderTest` **40/40**、`BmsBeatmapConverterTest` **15/15**、BMS 全套 **830/830**、`osu.Desktop.slnf` Release 构建 0 错误。

### P1-K：BMS→mania 转谱链路审查后全量收口（11 项发现 + K9 #15/#16 新约束）

一次审查暴露的 11 项发现一次性修完，并补齐缺失的两条 K9 硬约束。详细切片见 [../subline/P1-K/CHANGELOG.md](../subline/P1-K/CHANGELOG.md) 2026-05-29 条目。

- **K9 #9 兑现**：`BmsToManiaBeatmapConverter.ConvertBeatmap` 在 flatten 后 `scorableHitObjects.Count == 0`（纯 scratch / 空谱）显式抛 `BeatmapInvalidForRulesetException`，让既有 `BeatmapDifficultyCache` / `BackgroundDataStoreProcessor` 的 catch 自然把它固化为 Failed，与 K10 失败语义对齐。
- **K9 #16 新约束 + 实施**：新增 `BmsStopFreezeTimingControlPoint : TimingControlPoint` dedicated subclass 替代 `BeatLength = 6` 哨兵——mania `BeatLengthBindable.MinValue = 6` 下任何 BPM ≥ 10000 真实谱面与 sentinel 完全碰撞，类型级标记是唯一不会误删极端 BPM timing 的方式。
- **K9 #15 新约束**：mania 转谱期不传递 BMS scroll `EffectControlPoint`（既有行为，本轮在 K9 约束里显式声明）。
- **K5 cache 复用**：`createSourceBeatmap` 改为先查 `ICachedModlessPlayableBeatmapSource`，避免 BMS-side intermediate 被重复转换。
- **反射缓存对齐**：`ManiaRuleset.tryCreateBmsConverter` 改 `static readonly Func<...>` 委托对齐 `DrawableManiaRuleset` 既有模式。
- **持久化合同收口**：`BmsPersistedMetadataResolver.parsedDataCache` 写入时主动 evict 旧 key 消除无界增长；`BeatmapDifficultyCache.persist*IfApplicable` 加 `!metadata.IsManaged` 守护防 live realm 实例写抛异常；K10-B 推迟决定在 `LastAppliedDifficultyVersion` 比较处补注释指向 P1-K 文档。
- **合同注释**：`BmsConvertedScratchSampleHitObject` 补完整 XML 注释，把"依赖 IgnoreJudgement.MaxResult.AffectsCombo() == false 让 autoplay + note-lock 自动跳过"的跨模块合同写明。
- **次级清理**：`createDifficultyBeatmap` 改 `Metadata.DeepClone()` 防 calculator 反向污染；`initialiseLaneColumnMaps` 收窄 `scratchSampleColumnsByLane` 死赋值。

构建：`dotnet build osu.Desktop -p:Configuration=Release` 0 错误 0 警告。测试：`BmsToManiaBeatmapConverterTest` **17/17**（含 5 条本轮新增 focused 回归）；BMS 全套 **821/821**；核心 focused suite **40/40**。

已知 follow-up：`TestSceneManiaModAutoplay.TestPerfectScoreOnShortHoldNote` CLI 下 TearDownSteps 10s 超时——与 BMS 无关的 mania 自身 visual scene flake，已在文档历史中记录。**⚠️【2026-06-01 订正】此判断已被推翻**：该超时实为 mania autoplay 长条回归（`canParticipateInAutoplay` 误跳所有 `HoldNote` → combo 到不了 4 → PassCondition 不满足 → 重试至超时），非 visual scene flake；已修复（nested-aware 谓词）并经用户实测，见本日「修复 mania autoplay 整类长条不按」条。

---

## 2026-05-28

### P1-K：K10 第二刀（大量 BMS 库 carousel 性能与可用性闭环）

基于真实 58k+ BMS 谱库实测迭代。第一刀 K10-A 已保证未来导入即时就绪；本次第二刀闭合旧库回填路径与 carousel 读路径的剩余瓶颈：

- 启动批处理 BMS 谓词改回客户端 `IsBmsBeatmap()` 过滤——此前 Realm-side 链路在 Realm 20.1.0 + 58k 库下静默返回 0 结果导致旧库永远不被回填；并把 `Found N` 日志移到 early-return 之前，0 命中也写日志。
- 把 `Calculate()` 无 token 调用下唯一来源是上游 10s 内置超时的 `OperationCanceledException` 改判为确定性失败固化为 Failed（批处理与 K10-A 导入期路径同步）——避免极端谱每次启动浪费 10-25s。
- `BeatmapDifficultyCache.tryGetImmediateDifficulty` 现也在 `HasCurrentConvertedStarRatingState`（含 Failed）时同步返回 fallback（BMS playlevel），让 carousel 永不为已知失败谱排进 async compute 队列——这是上游 `CacheNullValues => false` 设计的实际绕开方式。
- `BeatmapCarousel.getEffectiveStarRatingsStrict` 改为 sync-first 两段式：57k+ 持久化的 BMS 谱在第一段 `TryGetCachedDifficulty` 命中，不再为每张分配 async lambda + Task；只有真未命中的零头才走 `Task.WhenAll`。难度过滤滑块在 restricted 范围下也立即响应。
- 详见 [../subline/P1-K/CHANGELOG.md](../subline/P1-K/CHANGELOG.md) 同日 K10 第二刀条目。

实测验证（用户提供 1779978684 会话 log）：`Found 0` 启动批处理瞬间完成、carousel filter ops 全部 400-800ms 顺利完结、`BeatmapDifficultyCache i:3 h:10 m:3 77%` 极低活动量。"卡顿/排序错位/难度动态变化"三大症状完全消除。

**Known follow-up**：高难谱星数 sprite-text 过渡动画（"数字跳动"）属 UI 显示层独立切片归 P1-A；极端谱滚动卡顿与 texture atlas 多次扩展相关、与本数据层修复无关；Genngaozo 系列谱 >10s 转谱的根因未单独排查（以 Failed 绕开）。

构建：Release 0 错误；`BeatmapCarouselFilter*Test + BmsStarRatingResolverTest` **37/37** 通过。

### P1-K：K10 第一刀（A 导入期持久化）落地；B 实测推迟；附 native 星批处理 return->continue

- [../../osu.Game/Beatmaps/BeatmapDifficultyCache.cs](../../osu.Game/Beatmaps/BeatmapDifficultyCache.cs) 新增 `EnsureConvertedStarRatingPersisted`；[../../osu.Game/Beatmaps/BeatmapUpdater.cs](../../osu.Game/Beatmaps/BeatmapUpdater.cs) 的 `Process()` 在导入期就为 BMS 谱计算并持久化 mania 转谱星，mirror native 星的导入期计算。大批量导入后同一会话切到 mania 即就绪，carousel 不再回退 BMS playlevel 然后异步 warmup 再重排，无需重启等启动批处理。
- B（读校验加固）经核查：carousel / spread display / difficulty cache 三处消费者共享同一个 RulesetStore detached 实例，`clearOutdatedStarRatings` 每次启动都会就地同步其 LAV；mania 难度版本是编译期常量。LAV 在稳态下可靠同步，B 收益不足以抵消改读路径的风险，**推迟实施**。详见 [../subline/P1-K/DEVELOPMENT_PLAN.md](../subline/P1-K/DEVELOPMENT_PLAN.md) K10 节。
- 附带：审查发现 [../../osu.Game/Database/BackgroundDataStoreProcessor.cs](../../osu.Game/Database/BackgroundDataStoreProcessor.cs) 的 `populateMissingStarRatings` 在单项谱面被删时会 `return` 中断整批并让进度通知卡死，改为 `continue` 与同文件其它批处理一致。
- 验证：Release slnf 构建 0 错误；`BmsStarRatingResolverTest` + `BeatmapCarouselFilterSortingTest` 20/20、`BmsToManiaBeatmapConverterTest` 12/12 通过。导入期端到端集成回归列为已知测试缺口。

### P1-K：转谱难度持久化链路失败语义统一

- BMS→mania 转谱星数持久化链一轮失败语义收口：[../../osu.Game/Database/BackgroundDataStoreProcessor.cs](../../osu.Game/Database/BackgroundDataStoreProcessor.cs) 与 [../../osu.Game/Beatmaps/BeatmapDifficultyCache.cs](../../osu.Game/Beatmaps/BeatmapDifficultyCache.cs) 现统一为"只有确定不可转（`BeatmapInvalidForRulesetException`）才持久化 Failed，瞬时异常仅日志、待重试"；缓存懒写路径也补上跨 ruleset 确定失败的 Failed 持久化，与批处理对齐，不再每次重算。
- 同时收窄启动期 BMS 谱面查询为 Realm 可翻译谓词（按 ruleset short-name 过滤，避免全表客户端扫描），并给 `LastAppliedDifficultyVersion` 内存赋值补依赖注释。
- 验证：`dotnet build osu.Desktop.slnf -c Release` / `-c Debug` 均 0 错误；`BmsStarRatingResolverTest` + `BeatmapCarouselFilterSortingTest` 13/13、`BmsToManiaBeatmapConverterTest` + autoplay 13/13 通过。详见 [../subline/P1-K/CHANGELOG.md](../subline/P1-K/CHANGELOG.md)。

## 2026-05-27

### P1-K：转谱链后置维护与 Test Explorer 退出标记

- BMS→mania 转谱链一轮无行为变化的维护性收口：STOP-freeze `BeatLength = 6` 提升为 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs) 的公开常量 `StopFreezeBeatLength` 并由 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaBeatmapConverter.cs) 共享，消除两份独立魔法常量；[../../osu.Game.Rulesets.Mania/UI/DrawableManiaRuleset.cs](../../osu.Game.Rulesets.Mania/UI/DrawableManiaRuleset.cs) 的 BMS drawable 工厂反射改为缓存强类型委托；[../../osu.Game/Beatmaps/BmsPersistedMetadataResolver.cs](../../osu.Game/Beatmaps/BmsPersistedMetadataResolver.cs) 的 `conversion_version` 补维护契约注释。
- [../../osu.Game/osu.Game.csproj](../../osu.Game/osu.Game.csproj) 增加 `<IsTestProject>false</IsTestProject>`：osu.Game 因引用 NUnit（抽象测试场景基类）被 C# Dev Kit 误当测试容器，对其启动 testhost 时缺探测路径解析不到缓存 AutoMapper 而中止发现。退出标记仅影响 IDE 测试发现，不改构建/运行，CLI `dotnet test` 不受影响。
- 验证：`dotnet build osu.Desktop.slnf -c Release` / `-c Debug` 均 0 错误；Mania 转谱聚焦 Release 14/14、Debug 12/12，`osu.Game.Tests` star/sort 聚焦 13/13 通过。详见 [../subline/P1-K/CHANGELOG.md](../subline/P1-K/CHANGELOG.md)。

## 2026-05-26

### P1-K / P1-A：`K9` 第三刀落地，sample-only scratch autoplay 与 persisted converted star 收口

- [../../osu.Game.Rulesets.Bms/Objects/BmsConvertedScratchSampleHitObject.cs](../../osu.Game.Rulesets.Bms/Objects/BmsConvertedScratchSampleHitObject.cs) 与 [../../osu.Game.Rulesets.Bms/UI/DrawableBmsConvertedScratchSampleHitObject.cs](../../osu.Game.Rulesets.Bms/UI/DrawableBmsConvertedScratchSampleHitObject.cs) 现已把 converted scratch-family 语义冻结为 sample-only、ignore-judgement 的 mania object：它们继续保留原 keysound / head-tail sample 的时间线，但不再占 mania judged column，也不再进入 combo / statistics / star 计算 authority。
- [../../osu.Game.Rulesets.Mania/Replays/ManiaAutoGenerator.cs](../../osu.Game.Rulesets.Mania/Replays/ManiaAutoGenerator.cs) 现已在 action-point 生成与同列 next-object lookup 两侧都跳过 `Judgement.MaxResult.AffectsCombo() == false` 的对象；因此 converted scratch sample 不再为 autoplay 生成假按键，也不再扰动真实列的 key-up 时机。[../../osu.Game.Rulesets.Mania.Tests/Mods/TestSceneManiaModAutoplay.cs](../../osu.Game.Rulesets.Mania.Tests/Mods/TestSceneManiaModAutoplay.cs) 现已补上同列 scratch sample + 实 note 的 dedicated autoplay proof。
- [../../osu.Game/Beatmaps/BmsPersistedMetadataResolver.cs](../../osu.Game/Beatmaps/BmsPersistedMetadataResolver.cs)、[../../osu.Game/Beatmaps/BmsStarRatingResolver.cs](../../osu.Game/Beatmaps/BmsStarRatingResolver.cs)、[../../osu.Game/Beatmaps/BeatmapDifficultyCache.cs](../../osu.Game/Beatmaps/BeatmapDifficultyCache.cs) 与 [../../osu.Game/Database/BackgroundDataStoreProcessor.cs](../../osu.Game/Database/BackgroundDataStoreProcessor.cs) 现已把 modless `BMS -> mania` 星数写入 `BeatmapMetadata.RulesetDataJson` 的 BMS payload，并按 target ruleset、difficulty version 与 conversion version 做读取、失效和后台补算；[../../osu.Game/Screens/Select/PanelBeatmapStandalone.SpreadDisplay.cs](../../osu.Game/Screens/Select/PanelBeatmapStandalone.SpreadDisplay.cs) 与 [../../osu.Game/Screens/Select/PanelBeatmapSet.SpreadDisplay.cs](../../osu.Game/Screens/Select/PanelBeatmapSet.SpreadDisplay.cs) 也已改为消费 current-ruleset resolved star，因此重启、切换与 spread dots 不再回退到 raw BMS 星数。
- 验证：`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "Name~BmsToManiaBeatmapConverterTest|Name~IgnoreOnlyDrawableDoesNotBlockColumnInput|Name~AutoplayIgnoresSampleOnlyScratchObjects"` **14/14** 通过；`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore --filter "Name~BmsStarRatingResolverTest|Name~BeatmapCarouselFilterSortingTest"` **19/19** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### P1-K / P1-A：`K9` 第二刀落地，converted mania 星数 authority 与 Song Select effective-star selector 接通

- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaBeatmapConverter.cs) 现已在 dedicated `BMS -> mania` conversion 完成后，对 converted mania beatmap 先执行 `ApplyDefaults()`，再通过 [../../osu.Game/Beatmaps/DirectPlayableWorkingBeatmap.cs](../../osu.Game/Beatmaps/DirectPlayableWorkingBeatmap.cs) 包装成 direct playable working beatmap，并喂给 `ManiaDifficultyCalculator` 完成 target-ruleset 星数重算；因此 converted beatmap 不再继续沿用 source BMS 星数。
- [../../osu.Game/Screens/Select/BeatmapCarousel.cs](../../osu.Game/Screens/Select/BeatmapCarousel.cs) 现已把 `BeatmapDifficultyCache` 接成 Song Select 的 shared effective-star resolver；[../../osu.Game/Screens/Select/BeatmapCarouselFilterMatching.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterMatching.cs)、[../../osu.Game/Screens/Select/BeatmapCarouselFilterSorting.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterSorting.cs) 与 [../../osu.Game/Screens/Select/BeatmapCarouselFilterGrouping.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterGrouping.cs) 则开始消费这条 resolved-star lookup，因此在 mania 视角下可见的 BMS converted chart，当前已会用 converted mania 星数参与 Song Select 的星数筛选、难度排序与按星数分组，而不再直接吃 raw `BeatmapInfo.StarRating`。
- [../../osu.Game/Beatmaps/BeatmapInfoExtensions.cs](../../osu.Game/Beatmaps/BeatmapInfoExtensions.cs) 现已新增“是否需要目标 ruleset 星数”和“读取 resolved star rating”的窄 helper，供 dedicated converter 与 Song Select selector 共享同一条 current-ruleset effective-star authority，而不是在各个 surface 内继续手写 `Ruleset.ShortName` / raw star fallback。
- focused proof 现已扩到两侧：一方面 [../../osu.Game.Rulesets.Mania.Tests/BmsToManiaBeatmapConverterTest.cs](../../osu.Game.Rulesets.Mania.Tests/BmsToManiaBeatmapConverterTest.cs) 已补到 **9/9**，锁住 converted beatmap 的星数会按 mania 难度算法重算；另一方面 [../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterSortingTest.cs](../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterSortingTest.cs)、[../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterGroupingTest.cs](../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterGroupingTest.cs) 与 [../../osu.Game.Tests/NonVisual/Filtering/FilterMatchingTest.cs](../../osu.Game.Tests/NonVisual/Filtering/FilterMatchingTest.cs) 现已新增 conversion-aware effective-star regressions，锁住 sort / group / filter 三条 selector surface。
- 验证：`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --filter BmsToManiaBeatmapConverterTest --no-restore` **9/9** 通过；`dotnet test osu.Game.Tests --filter "FullyQualifiedName~TestSortingByDifficultyUsesResolvedConvertedStarRating|FullyQualifiedName~TestGroupedSetDifficultyOrderingUsesResolvedConvertedStarRating|FullyQualifiedName~TestGroupingByDifficultyUsesResolvedConvertedStarRating|FullyQualifiedName~TestCriteriaMatchingUsesResolvedConvertedStarRating" --no-restore` **4/4** 通过。

### P1-K / P1-A：`K9` 首刀落地，BMS -> mania dedicated conversion 与首轮 public gate 接通

- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsToManiaBeatmapConverter.cs) 现已新增 dedicated `BMS -> mania` converter：它从 decoded BMS source chart 重新 materialize canonical modless BMS playable，再固定按 `5K/7K/9K_Bms/9K_Pms/14K` 矩阵映射到 mania；其中 `14K+2S` 固定为 dual-stage `7 + 7`，scratch-family object 则按同 side / 同 stage 退化到最近存活列，并保留 head/tail keysound sample。
- [../../osu.Game.Rulesets.Mania/ManiaRuleset.cs](../../osu.Game.Rulesets.Mania/ManiaRuleset.cs) 现已在 mania 入口增加一条反射式 BMS converter factory 分流：当 source beatmap 是 BMS decoded wrapper 时，mania 会委托 BMS 侧 factory 创建 dedicated converter，而不会回退到通用 `IHasXPosition` pass-through 路径；这同时保持了项目依赖方向为 `BMS -> mania`，而不是让 upstream mania 项目直接引用 OMS 的 BMS 项目。
- [../../osu.Game/Beatmaps/BeatmapInfoExtensions.cs](../../osu.Game/Beatmaps/BeatmapInfoExtensions.cs)、[../../osu.Game/OsuGame.cs](../../osu.Game/OsuGame.cs) 与 [../../osu.Game/Screens/Select/SongSelect.cs](../../osu.Game/Screens/Select/SongSelect.cs) 现已把 `BMS source -> mania target` 的 visibility / ruleset-maintenance gate 接回真实可玩性判断：`AllowGameplayWithRuleset()` 明确承认这条单向转谱，而 `PresentBeatmap` / Song Select 则改为通过 `RequiresRulesetSwitch()` 决定是否继续维持当前 mania ruleset，不再依赖 generic `OnlineID` heuristics。
- [../../osu.Game.Rulesets.Mania.Tests/BmsToManiaBeatmapConverterTest.cs](../../osu.Game.Rulesets.Mania.Tests/BmsToManiaBeatmapConverterTest.cs) 现已新增 **8/8** focused tests，锁住 `5K/7K/9K_Bms/9K_Pms/14K` 五类 keymode、`14K -> 7+7` dual-stage、scratch-family head/tail sample 保留、modless source gate，以及 `AllowGameplayWithRuleset()` / `RequiresRulesetSwitch()` 的首轮 public gate。
- 验证：`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --filter BmsToManiaBeatmapConverterTest --no-restore` **8/8** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

## 2026-05-25

### P1-K：K8 gauge history consumer proof 完成，auto-shift timeline state 写死

- [../../osu.Game.Rulesets.Bms/UI/BmsGaugeHistoryGraph.cs](../../osu.Game.Rulesets.Bms/UI/BmsGaugeHistoryGraph.cs) 现已让 `SkinnableBmsGaugeHistoryPanelDisplay` 暴露只读 history state，供 focused proof 直接读取 `CreateStatisticsForScore()` 生成的 gauge history 数据，而不再依赖 CLI 下不稳定的 skinnable scene 装载链。
- [../../osu.Game.Rulesets.Bms.Tests/BmsRulesetStatisticsTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsRulesetStatisticsTest.cs) 新增 `TestCreateStatisticsGaugeHistoryCarriesAutoShiftTimelineState()`，直接锁住 auto-shift `EX-HARD -> HARD -> NORMAL` timeline 与对应 sample/time/value 会端到端进入 gauge history consumer，而不是仅停留在 panel type proof。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsRulesetStatisticsTest.TestCreateStatistics"` **4/4** 通过。

### P1-K：K7 results summary consumer proof 完成，clear-lamp 优先级写死

- [../../osu.Game.Rulesets.Bms/UI/BmsResultsSummaryDisplay.cs](../../osu.Game.Rulesets.Bms/UI/BmsResultsSummaryDisplay.cs) 现已让 `SkinnableBmsResultsSummaryPanelDisplay` 暴露只读 summary state，供 focused proof 直接读取 `CreateStatisticsForScore()` 生成的 results summary 数据，而不再依赖 CLI 下不稳定的 skinnable scene 装载链。
- [../../osu.Game.Rulesets.Bms.Tests/BmsRulesetStatisticsTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsRulesetStatisticsTest.cs) 新增 `TestCreateStatisticsSummaryCarriesSelectedModesAndClearLamp()`，直接锁住 selected modes、EX-SCORE、DJ LEVEL 与 computed clear lamp 会端到端进入 summary consumer，并明确 `PERFECT` / `FULL COMBO` 会继续覆盖 gauge-derived lamp。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsRulesetStatisticsTest.TestCreateStatistics"` **3/3** 通过。

### P1-K：K6 focused validation 完成，results-side 已带 mods playable contract 写死

- [../../osu.Game/Rulesets/Ruleset.cs](../../osu.Game/Rulesets/Ruleset.cs) 已明确 `PrepareScoreInfoForResults()` 与 `CreateStatisticsForScore()` 接收的是“已应用所有相关 mods 的 playable beatmap”；[../../osu.Game.Rulesets.Bms/BmsRuleset.cs](../../osu.Game.Rulesets.Bms/BmsRuleset.cs) 与 [../../osu.Game.Rulesets.Bms/Scoring/BmsClearLampProcessor.cs](../../osu.Game.Rulesets.Bms/Scoring/BmsClearLampProcessor.cs) 现已按此 contract 消费 caller 传入的 beatmap，不再在 results/gauge helper 内再次调用 `BmsBeatmapModApplicator`。
- [../../osu.Game.Rulesets.Bms.Tests/BmsPlayableBeatmapCacheTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsPlayableBeatmapCacheTest.cs) 与 [../../osu.Game.Rulesets.Bms.Tests/BmsClearLampProcessorTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsClearLampProcessorTest.cs) 现已分别补出 `Mirror` focused proof，锁住 results-side helper 不会对已带 mods 的 playable beatmap 重复应用 beatmap mods；依赖 long-note / assist 语义的 HCN、autoplay 邻接用例也已改为显式先应用 score mods，再进入 clear-lamp helper。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsPlayableBeatmapCacheTest"` **5/5** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsClearLampProcessorTest"` **32/32** 通过。

### P1-K：K5 数字层级完成收口，parse-side playable cache contract 写死

- [../../osu.Game/Beatmaps/ICachedModlessPlayableBeatmapSource.cs](../../osu.Game/Beatmaps/ICachedModlessPlayableBeatmapSource.cs) 现已定义 source-bound 的 modless playable cache contract；[../../osu.Game/Beatmaps/WorkingBeatmap.cs](../../osu.Game/Beatmaps/WorkingBeatmap.cs) 则会在 `GetPlayableBeatmap()` 中优先复用实现该 contract 的 source beatmap 上已缓存的无 mods playable projection，只有换 source 或带 mods 时才重新转换。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedBeatmap.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsDecodedBeatmap.cs) 现已按 ruleset short name 持有 modless playable cache，而 [../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsImportedBeatmapFactory.cs) 也会把 loader 首次 conversion 的现成 projection seed 回 source wrapper；因此无 mods 的同源 BMS playable projection 现只 materialize 一次，不再为同一 source 重复构造等价 playable beatmap。
- [../../osu.Game.Rulesets.Bms.Tests/BmsPlayableBeatmapCacheTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsPlayableBeatmapCacheTest.cs) 新增 dedicated focused proof，直接锁住同源复用、跨 source 隔离、带 mods 绕过缓存，以及 loader-seeded cache 返回的 hold-note projection 已完成 finalize；相邻 loader-focused `BmsImportIntegrationTest` 回归也已确认 import metadata / timing 合同未因 cache seed 回归。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsPlayableBeatmapCacheTest"` **4/4** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsImportIntegrationTest.TestLoader"` **9/9** 通过。

### P1-K：K4 数字层级完成收口，plain title chain 与残余 set-level consumer 全部复用 shared authority

- [../../osu.Game/Beatmaps/BeatmapInfoExtensions.cs](../../osu.Game/Beatmaps/BeatmapInfoExtensions.cs) 现已让 `IBeatmapInfo.GetDisplayTitle()` 复用 shared title authority，不再继续绕回 raw metadata title path；因此 `BeatmapInfo.ToString()`、`ScoreInfoExtensions`、`UserActivity`、`NowPlayingCommand` 与 `BeatmapClearScoresDialog` 这类 plain title consumer 也随之复用同一 display authority。
- [../../osu.Game/Beatmaps/BeatmapSetInfoExtensions.cs](../../osu.Game/Beatmaps/BeatmapSetInfoExtensions.cs) 现已把 set-level artist/title helper 提升到 `IBeatmapSetInfo`，并新增 plain `GetDisplayTitle()`；[../../osu.Game/Extensions/ModelExtensions.cs](../../osu.Game/Extensions/ModelExtensions.cs) 则已改为通过这条 helper 读取 beatmap-set display string，不再继续直接走 metadata-only `GetDisplayTitle()`。
- [../../osu.Game/Overlays/BeatmapSet/BeatmapSetHeaderContent.cs](../../osu.Game/Overlays/BeatmapSet/BeatmapSetHeaderContent.cs)、[../../osu.Game/Beatmaps/Drawables/Cards/BeatmapCardNormal.cs](../../osu.Game/Beatmaps/Drawables/Cards/BeatmapCardNormal.cs)、[../../osu.Game/Beatmaps/Drawables/Cards/BeatmapCardNano.cs](../../osu.Game/Beatmaps/Drawables/Cards/BeatmapCardNano.cs)、[../../osu.Game/Beatmaps/Drawables/Cards/BeatmapCardExtra.cs](../../osu.Game/Beatmaps/Drawables/Cards/BeatmapCardExtra.cs)、[../../osu.Game/Screens/OnlinePlay/Matchmaking/Match/BeatmapSelect/MatchmakingSelectPanel.CardContentBeatmap.cs](../../osu.Game/Screens/OnlinePlay/Matchmaking/Match/BeatmapSelect/MatchmakingSelectPanel.CardContentBeatmap.cs) 与 [../../osu.Game/Screens/OnlinePlay/Components/BeatmapTitle.cs](../../osu.Game/Screens/OnlinePlay/Components/BeatmapTitle.cs) 现也统一复用 shared artist/title authority，不再继续各自手工拼 raw set metadata 或 raw beatmap metadata。
- [../../osu.Game.Tests/Localisation/BeatmapDisplayTitleLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Localisation/BeatmapDisplayTitleLocalMetadataDisplayTest.cs) 新增 plain NUnit focused proof，直接锁住 plain beatmap title、set-level plain title 与 display-string authority；配合既有 [../../osu.Game.Tests/Menus/BeatmapSetArtistLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Menus/BeatmapSetArtistLocalMetadataDisplayTest.cs)，`K4` 现已具备数字层级完成所需的最窄 authority proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BeatmapDisplayTitleLocalMetadataDisplayTest|FullyQualifiedName~BeatmapSetArtistLocalMetadataDisplayTest"` **6/6** 通过。

### P1-K：K4-S 让 set-level artist display 复用 shared artist authority

- [../../osu.Game/Beatmaps/BeatmapSetInfoExtensions.cs](../../osu.Game/Beatmaps/BeatmapSetInfoExtensions.cs) 现已新增 `GetDisplayArtistRomanisable()` shared helper：当 beatmap set 持有具体 beatmap 时，优先复用首个 beatmap 的 display artist authority，只在没有 beatmap 时才回退到 set metadata 的 raw artist text。
- [../../osu.Game/Screens/Select/PanelBeatmapSet.cs](../../osu.Game/Screens/Select/PanelBeatmapSet.cs) 与 [../../osu.Game/Overlays/Music/PlaylistItem.cs](../../osu.Game/Overlays/Music/PlaylistItem.cs) 现已通过 `BeatmapSetInfo.GetDisplayArtistRomanisable()` 显示 set-level artist，不再继续直接走 raw `beatmapSet.Metadata.Artist` / `ArtistUnicode`；因此 Song Select set panel 与 playlist tray 都不会再暴露 raw `/obj:` 后缀。
- [../../osu.Game.Tests/Menus/BeatmapSetArtistLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Menus/BeatmapSetArtistLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 BMS artist clean 与 non-BMS passthrough contract；因此 set-level artist display surface 现已具备独立 plain focused proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BeatmapSetArtistLocalMetadataDisplayTest"` **2/2** 通过。

### P1-K：K4-F 补齐 BeatmapAttributeText plain focused proof

- [../../osu.Game/Skinning/Components/BeatmapAttributeText.cs](../../osu.Game/Skinning/Components/BeatmapAttributeText.cs) 现已补出 `GetDisplayedArtist()` 与 `GetDisplayedCreator()` internal helper，让 shared beatmap-attribute display consumer 的 artist / creator 读口可以直接复用组件内 authority，并脱离 CLI scene discoverability 做最窄 plain proof。
- [../../osu.Game.Tests/Skins/BeatmapAttributeTextLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Skins/BeatmapAttributeTextLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 BMS artist clean、creator fallback 与 non-BMS passthrough contract；因此 `BeatmapAttributeText` 现已具备独立 plain focused proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BeatmapAttributeTextLocalMetadataDisplayTest"` **2/2** 通过。

### P1-K：K4-R 让 delete confirmation title display 复用 set-level title authority

- [../../osu.Game/Beatmaps/BeatmapSetInfoExtensions.cs](../../osu.Game/Beatmaps/BeatmapSetInfoExtensions.cs) 现已新增 shared set-level title helper：当 beatmap set 持有具体 beatmap 时，优先复用首个 beatmap 的 full title authority，并允许各个 set-level surface 显式保持是否展示 creator 的既有合同。
- [../../osu.Game/Screens/Select/BeatmapDeleteDialog.cs](../../osu.Game/Screens/Select/BeatmapDeleteDialog.cs) 现已通过 `BeatmapSetInfo.GetDisplayTitleRomanisable(includeCreator: false)` 显示删除确认标题，不再继续直接走 `beatmapSet.Metadata.GetDisplayTitleRomanisable(false)`；因此 delete confirmation title 不再暴露 raw `/obj:` 后缀，也不会误带 difficulty name，同时继续保持不展示 creator suffix 的既有外观。
- [../../osu.Game.Tests/Menus/BeatmapDeleteDialogLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Menus/BeatmapDeleteDialogLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 BMS fallback、non-BMS passthrough、“无 creator 泄漏”与“无难度名泄漏”的 contract；[../../osu.Game.Tests/Menus/ScopedBeatmapSetDisplayLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Menus/ScopedBeatmapSetDisplayLocalMetadataDisplayTest.cs) 也已补强相邻 shared-helper contract 的难度名断言。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BeatmapDeleteDialogLocalMetadataDisplayTest|FullyQualifiedName~ScopedBeatmapSetDisplayLocalMetadataDisplayTest"` **4/4** 通过。

## 2026-05-23

### P1-K：K4-Q 让 Daily Challenge title display 复用 full-beatmap title authority

- [../../osu.Game/Screens/OnlinePlay/DailyChallenge/DailyChallengeIntro.cs](../../osu.Game/Screens/OnlinePlay/DailyChallenge/DailyChallengeIntro.cs) 现已让 daily challenge title display 在可拿到具体 beatmap 时优先调用 `IBeatmapInfo.GetDisplayTitleRomanisable(includeDifficultyName: false)`，不再继续直接走 `beatmap.BeatmapSet!.Metadata.GetDisplayTitleRomanisable(false)`，因此不会再暴露 raw `/obj:` 后缀，也不会把难度名重新带回标题行。
- [../../osu.Game.Tests/OnlinePlay/DailyChallengeLocalMetadataDisplayTest.cs](../../osu.Game.Tests/OnlinePlay/DailyChallengeLocalMetadataDisplayTest.cs) 现已把 plain NUnit focused proof 扩展到 creator fallback 与 title authority 两侧，同时锁住 BMS fallback、non-BMS passthrough 与“无难度名泄漏”的 contract；当 visual scene 主要覆盖转场时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~DailyChallengeLocalMetadataDisplayTest"` **4/4** 通过。

### P1-K：K4-P 让 scoped beatmap-set title display 复用 full-beatmap title authority

- [../../osu.Game/Screens/Select/FilterControl.ScopedBeatmapSetDisplay.cs](../../osu.Game/Screens/Select/FilterControl.ScopedBeatmapSetDisplay.cs) 现已让 scoped beatmap set title display 在能拿到具体 beatmap 时优先调用 `IBeatmapInfo.GetDisplayTitleRomanisable(includeDifficultyName: false)`，只在空 set 时才回退到 metadata-only overload，因此 Song Select scoped-set banner 不再暴露 raw `/obj:` 后缀，也不会误带难度名。
- [../../osu.Game.Tests/Menus/ScopedBeatmapSetDisplayLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Menus/ScopedBeatmapSetDisplayLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住首个 beatmap authority reuse、BMS fallback、non-BMS passthrough 与“无难度名泄漏”的 contract；当 set-level UI 只是转发标题字符串时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~ScopedBeatmapSetDisplayLocalMetadataDisplayTest"` **2/2** 通过。

### P1-K：K4-O 让 IBeatmapInfo title display 复用 display artist / creator fallback

- [../../osu.Game/Beatmaps/BeatmapInfoExtensions.cs](../../osu.Game/Beatmaps/BeatmapInfoExtensions.cs) 现已让 `IBeatmapInfo.GetDisplayTitleRomanisable()` 同时通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()` / `GetDisplayCreator()` 读取 BMS local metadata display authority，不再在 title display consumer 上直接暴露 embedded creator suffix 或空 creator。
- [../../osu.Game.Tests/Localisation/BeatmapInfoRomanisationLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Localisation/BeatmapInfoRomanisationLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 BMS fallback 与非 BMS passthrough；当具体 UI 只是转发 title string 时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BeatmapInfoRomanisationLocalMetadataDisplayTest"` **2/2** 通过。

### P1-K：K4-N 让 beatmap skin metadata 复用 display creator fallback

- [../../osu.Game/Skinning/LegacyBeatmapSkin.cs](../../osu.Game/Skinning/LegacyBeatmapSkin.cs) 现已让 beatmap skin metadata 的 `SkinInfo.Creator` 通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayCreator()` 读取 BMS local creator fallback，不再继续直接展示 raw `Metadata.Author.Username`。
- [../../osu.Game.Tests/Skins/LegacyBeatmapSkinLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Skins/LegacyBeatmapSkinLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 beatmap skin metadata 的 creator 读口；当 beatmap skin 只通过 `SkinInfo` 暴露 metadata 时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~LegacyBeatmapSkinLocalMetadataDisplayTest"` **2/2** 通过。

### P1-K：K4-M 让 matchmaking round results 优先复用本地 BeatmapInfo

- [../../osu.Game/Screens/OnlinePlay/Matchmaking/Match/RoundResults/SubScreenRoundResults.cs](../../osu.Game/Screens/OnlinePlay/Matchmaking/Match/RoundResults/SubScreenRoundResults.cs) 现已在按 API scores 构造 `ScoreInfo` 时优先复用本地 `BeatmapInfo`，仅在本地谱面缺失时才回退到 API 最小壳，从而保住 round-results `ScorePanel` / `ExpandedPanelMiddleContent` 已接好的 BMS local metadata display authority。
- [../../osu.Game.Tests/OnlinePlay/SubScreenRoundResultsLocalMetadataDisplayTest.cs](../../osu.Game.Tests/OnlinePlay/SubScreenRoundResultsLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 local beatmap reuse 与 API fallback shell；当 visual scene 只看到最终 `ScorePanel` 内容时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~SubScreenRoundResultsLocalMetadataDisplayTest"` **2/2** 通过。

### P1-K：K4-L 让 online playlist creator display 复用 display creator fallback

- [../../osu.Game/Screens/OnlinePlay/DrawableRoomPlaylistItem.cs](../../osu.Game/Screens/OnlinePlay/DrawableRoomPlaylistItem.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayCreator()` / `HasLinkedCreatorProfile()` 读取 BMS local creator fallback，不再因空 `Metadata.Author.Username` 隐藏作者行；有真实作者资料时继续保留 user link，没有时回退为 plain text creator。
- [../../osu.Game.Tests/OnlinePlay/DrawableRoomPlaylistItemLocalMetadataDisplayTest.cs](../../osu.Game.Tests/OnlinePlay/DrawableRoomPlaylistItemLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 creator 文本与 linked-profile 分支；当 visual scene 难以稳定断言 user link 行为时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~DrawableRoomPlaylistItemLocalMetadataDisplayTest"` **2/2** 通过。

### P1-K：K4-K 让 daily challenge creator display 复用 display creator fallback

- [../../osu.Game/Screens/OnlinePlay/DailyChallenge/DailyChallengeIntro.cs](../../osu.Game/Screens/OnlinePlay/DailyChallenge/DailyChallengeIntro.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayCreator()` 读取 BMS local creator fallback，不再在 daily challenge metadata surface 内继续直接展示 raw local creator。
- [../../osu.Game.Tests/OnlinePlay/DailyChallengeLocalMetadataDisplayTest.cs](../../osu.Game.Tests/OnlinePlay/DailyChallengeLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 creator 读口；当 visual scene 主要覆盖转场时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~DailyChallengeLocalMetadataDisplayTest"` **2/2** 通过。

### P1-K：K4-J 让 menu metadata display 复用 display artist fallback

- [../../osu.Game/Screens/Menu/SongTicker.cs](../../osu.Game/Screens/Menu/SongTicker.cs) 与 [../../osu.Game/Overlays/NowPlayingOverlay.cs](../../osu.Game/Overlays/NowPlayingOverlay.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()` 读取 BMS local artist fallback，不再在 menu / now-playing metadata surface 内继续直接展示 raw local artist。
- [../../osu.Game.Tests/Menus/MenuBeatmapMetadataLocalDisplayTest.cs](../../osu.Game.Tests/Menus/MenuBeatmapMetadataLocalDisplayTest.cs) 新增 plain NUnit focused test，直接锁住两个 surface 的 artist 读口；当 visual scene 不直接提供 metadata 断言时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~MenuBeatmapMetadataLocalDisplayTest"` **2/2** 通过。

### P1-K：K4-I 让 profile metadata display 复用 display artist fallback

- [../../osu.Game/Overlays/Profile/Sections/Ranks/DrawableProfileScore.cs](../../osu.Game/Overlays/Profile/Sections/Ranks/DrawableProfileScore.cs) 与 [../../osu.Game/Overlays/Profile/Sections/Historical/DrawableMostPlayedBeatmap.cs](../../osu.Game/Overlays/Profile/Sections/Historical/DrawableMostPlayedBeatmap.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()` 读取 BMS local artist fallback，不再在 profile beatmap metadata surface 内继续直接展示 raw local artist。
- [../../osu.Game.Tests/Online/ProfileBeatmapMetadataLocalDisplayTest.cs](../../osu.Game.Tests/Online/ProfileBeatmapMetadataLocalDisplayTest.cs) 新增 plain NUnit focused test，直接锁住两个 surface 的 artist 读口；当相邻 visual scene 在 CLI 下不可 discover 时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~ProfileBeatmapMetadataLocalDisplayTest"` **2/2** 通过。

### P1-K：K4-H 让 results metadata display 复用 display artist / creator fallback

- [../../osu.Game/Screens/Ranking/Expanded/ExpandedPanelMiddleContent.cs](../../osu.Game/Screens/Ranking/Expanded/ExpandedPanelMiddleContent.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()` / `GetDisplayCreator()` 读取 BMS local artist / creator fallback，不再在 results screen 的 expanded metadata surface 内继续直接展示 raw local metadata。
- [../../osu.Game.Tests/Scores/ExpandedPanelMiddleContentLocalMetadataDisplayTest.cs](../../osu.Game.Tests/Scores/ExpandedPanelMiddleContentLocalMetadataDisplayTest.cs) 新增 plain NUnit focused test，直接锁住 artist / creator 读口；当相邻 visual scene 在 CLI 下不可 discover 时，不再退化成 compile-only proof。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~ExpandedPanelMiddleContentLocalMetadataDisplayTest"` **2/2** 通过。

### P1-K：K4-G 让 gameplay metadata display 复用 display artist / creator fallback

- [../../osu.Game/Screens/Play/BeatmapMetadataDisplay.cs](../../osu.Game/Screens/Play/BeatmapMetadataDisplay.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()` / `GetDisplayCreator()` 读取 BMS local artist / creator fallback，不再在 gameplay loading surface 内直接展示 raw local metadata。
- [../../osu.Game.Tests/Visual/Gameplay/TestSceneBeatmapMetadataDisplay.cs](../../osu.Game.Tests/Visual/Gameplay/TestSceneBeatmapMetadataDisplay.cs) 现改用组件 internal readback 锚点锁住 display text，避免继续依赖不稳定的 scene 树遍历断言；focused validation 也固定为整类 `TestSceneBeatmapMetadataDisplay` filter，而不是宽泛匹配 `TestLocal`。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~TestSceneBeatmapMetadataDisplay"` **8/8** 通过。

### P1-K：K4-F 让 local-metadata display consumer 复用 display artist / creator fallback

- [../../osu.Game/Screens/Select/BeatmapCarouselFilterSorting.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterSorting.cs)、[../../osu.Game/Screens/Select/BeatmapCarouselFilterGrouping.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterGrouping.cs) 与 [../../osu.Game/Screens/Select/BeatmapCarouselFilterMatching.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterMatching.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayArtist()` / `GetDisplayArtistUnicode()` 读取 BMS local artist fallback，不再把 embedded creator suffix 暴露给 Song Select 的 artist sort/group/filter。
- [../../osu.Game/Skinning/Components/BeatmapAttributeText.cs](../../osu.Game/Skinning/Components/BeatmapAttributeText.cs) 现也通过 `BeatmapLocalMetadataDisplayResolver` 统一读取 BMS local artist / creator display text，不再在 shared beatmap-attribute display consumer 内直接暴露 raw local metadata。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "(FullyQualifiedName~TestSortingByArtistUsesBmsDisplayArtistFallback|FullyQualifiedName~TestGroupingByArtist|FullyQualifiedName~TestCriteriaMatchingArtistDoesNotMatchBmsCreatorSuffix|FullyQualifiedName~TestCriteriaMatchingArtistWithNullUnicodeName|FullyQualifiedName~TestCriteriaNotMatchingArtist|FullyQualifiedName~TestDisplayArtistStripsEmbeddedBmsCreator)"` **9/9** 通过；相邻 `BeatmapAttributeText` plain focused proof 已于 `2026-05-25` 由 `BeatmapAttributeTextLocalMetadataDisplayTest` **2/2** 补齐。

### P1-K：K4-E 让 Song Select creator selector 复用 display creator fallback

- [../../osu.Game/Screens/Select/BeatmapCarouselFilterSorting.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterSorting.cs)、[../../osu.Game/Screens/Select/BeatmapCarouselFilterGrouping.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterGrouping.cs) 与 [../../osu.Game/Screens/Select/BeatmapCarouselFilterMatching.cs](../../osu.Game/Screens/Select/BeatmapCarouselFilterMatching.cs) 现已统一通过 `BeatmapLocalMetadataDisplayResolver.GetDisplayCreator()` 读取 BMS local creator fallback，不再只按 `Metadata.Author.Username` 做 Song Select 的 author sort/group/filter。
- [../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterSortingTest.cs](../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterSortingTest.cs)、[../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterGroupingTest.cs](../../osu.Game.Tests/Visual/SongSelect/BeatmapCarouselFilterGroupingTest.cs) 与 [../../osu.Game.Tests/NonVisual/Filtering/FilterMatchingTest.cs](../../osu.Game.Tests/NonVisual/Filtering/FilterMatchingTest.cs) 已锁住这条 selector reuse path。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "(FullyQualifiedName~TestSortingByAuthorUsesBmsDisplayCreatorFallback|FullyQualifiedName~TestGroupingByAuthorUsesBmsDisplayCreatorFallback|FullyQualifiedName~TestCriteriaMatchingCreatorUsesBmsDisplayCreatorFallback)"` **3/3** 通过。

### P1-K：K4-D 让 core metadata read-model 复用 persisted chart_metadata projection

- [../../osu.Game/Beatmaps/BmsPersistedMetadataResolver.cs](../../osu.Game/Beatmaps/BmsPersistedMetadataResolver.cs) 现为 `osu.Game` 提供统一的 typed persisted `chart_metadata` projection，避免 core consumer 各自手拆 `RulesetDataJson`。
- [../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs](../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs) 与 [../../osu.Game/Beatmaps/BmsStarRatingResolver.cs](../../osu.Game/Beatmaps/BmsStarRatingResolver.cs) 现已共享这条读取路径，不再各自维护 `JObject.SelectToken("chart_metadata...")` 的 stringly-typed token 合同。
- [../../osu.Game.Tests/Beatmaps/BeatmapLocalMetadataDisplayResolverTest.cs](../../osu.Game.Tests/Beatmaps/BeatmapLocalMetadataDisplayResolverTest.cs) 与 [../../osu.Game.Tests/Beatmaps/BmsStarRatingResolverTest.cs](../../osu.Game.Tests/Beatmaps/BmsStarRatingResolverTest.cs) 已锁住这条 core-side persisted metadata reuse path。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "(FullyQualifiedName~BmsStarRatingResolverTest|FullyQualifiedName~BeatmapLocalMetadataDisplayResolverTest)"` **11/11** 通过。

### P1-K：K4-C 让 beatmap statistics 复用 metadata chart-filter projection

- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmap.cs) 现会在 `GetStatistics()` 中优先读取 `BeatmapInfo.Metadata.GetChartFilterStats()`，只在缺失时才回退到 `BmsChartFilterStats.FromBeatmap(this)`。
- 同一 consumer 在缺失 `ChartFilterStats` 时会把现场计算结果回写到 metadata，避免同一 runtime beatmap 反复本地重数同一份 projected hitobjects。
- [../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapStatisticsTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsBeatmapStatisticsTest.cs) 已补上 focused regressions，锁住“优先复用 metadata / 缺失时回写缓存”的 statistics consumer 选择逻辑。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsBeatmapStatisticsTest"` **3/3** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal` **812/812** 通过。

## 2026-05-22

### P1-K：K4-B 让 Song Select note distribution graph 复用 projected working beatmap

- [../../osu.Game.Rulesets.Bms/SongSelect/BmsNoteDistributionGraph.cs](../../osu.Game.Rulesets.Bms/SongSelect/BmsNoteDistributionGraph.cs) 新增 `ResolveBeatmapForAnalysis()`，当 working beatmap 的 source beatmap 已携带 `BmsHitObject` projection 时会直接复用它，只在缺失时才回退到 `GetPlayableBeatmap()`。
- 同一文件的 note-distribution 数据构造现统一从 `BeatmapInfo.Metadata` 读取 `ChartMetadata`，让 projected source beatmap 与 playable beatmap 继续共享同一摘要 authority，而不是由 Song Select 长出第二套 conversion 语义。
- [../../osu.Game.Rulesets.Bms.Tests/BmsNoteDistributionGraphTest.cs](../../osu.Game.Rulesets.Bms.Tests/BmsNoteDistributionGraphTest.cs) 已补上 focused regressions，锁住“优先复用 projected source beatmap / 无 projection 时回退 playable conversion”的 consumer 选择逻辑。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsNoteDistributionGraphTest"` **5/5** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal` **810/810** 通过。

### P1-K：K4-A 让 static background 首次复用 unified projection

- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs) 新增 `GetPreferredBackgroundAssetReference()`，统一选择 `STAGEFILE/BACKBMP/BANNER` 或 richer visual-definition family 的首个 bitmap，并在遇到两位 bitmap reference 时先通过 `BitmapTable` 解析成真实资源名。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapConverter.cs)、[../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsFolderImporter.cs) 与 [../../osu.Game.Rulesets.Bms/UI/BmsBackgroundLayer.cs](../../osu.Game.Rulesets.Bms/UI/BmsBackgroundLayer.cs) 现已共享这条 background asset projection，使 metadata background、导入后的图片正规化与 playfield static background consumer 不再各自只认 `STAGEFILE/BACKBMP/BANNER`。
- 这一步把 richer visual-definition family 的首个 consumer 真正接到了运行中的 static background 路径上，并修正了一个关键语义点：不能把 `#BGA/#@BGA` 的两位引用直接当文件名，必须先过 `BitmapTable`。
- 验证：新增 static-background targeted regressions **3/3** 通过；`BmsBeatmapConverterTest` **13/13** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal` **808/808** 通过。

### P1-K：K3-F 补齐 unified visual-definition projection contract

- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsVisualDefinitions.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsVisualDefinitions.cs) 现新增 `BmsVisualDefinitionProjection`；[../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs) 现提供 `GetVisualDefinitionProjections()` 与 `TryGetVisualDefinitionProjection()`，把 `#BGA`、`#@BGA`、`#ARGB`、`#SWBGA` 与 `#POORBGA` 的分散 header tables 收口为按 index 读取的组合视图。
- 这一刀仍限定在 decoder/model，不改 converter、importer、Song Select 或 runtime visual consumer；目的是让后续任一 consumer 有单一投影可复用，而不再各自手工重拼四张 definition 表。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~BmsBeatmapDecoderTest"` **33/33** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter FullyQualifiedName~BmsBeatmapConverterTest` **12/12** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal` **805/805** 通过。

### P1-K：K3-E 补齐 richer BGA-definition header typed surface

- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsVisualDefinitions.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsVisualDefinitions.cs) 现新增 richer BGA-definition header family 的 typed model；[../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapInfo.cs) 也新增 `BgaDefinitions`、`AtBgaDefinitions`、`ArgbDefinitions`、`SwBgaDefinitions` 与 `PoorBgaMode`，让 `#BGA`、`#@BGA`、`#ARGB`、`#SWBGA`、`#POORBGA` 不再只停留在 generic unknown bag。
- [../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs](../../osu.Game.Rulesets.Bms/Beatmaps/BmsBeatmapDecoder.cs) 现会把这些 header 解析进 typed surface，并保留 `#BGA/#@BGA` 的 bitmap/reference 原始 token，不提前把 bitmap 绑定或运行时播放语义锁死在 decoder。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter FullyQualifiedName~BmsBeatmapDecoderTest` **32/32** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter FullyQualifiedName~BmsBeatmapConverterTest` **12/12** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal` **804/804** 通过。

### P1-K：K3-D 补齐 `SCROLLxx/SC` 的 typed consumer contract

- `BmsDecodedChart` 现新增 `ScrollEvents`；`BmsBeatmapDecoder` 会把 `SCROLLxx` 定义 + `SC` channel line 解析成 typed scroll surface，并让 unknown-channel duplicate compound 按 `RawChannelToken` 区分，不再把 `SC` 与其它 unknown track 在同拍位错误折叠。
- `BmsBeatmapConverter` 现已把 `ScrollEvents` 接到 `ControlPointInfo.EffectPoints`，让 `SCROLLxx/SC` 首次进入 runtime scroll-speed consumer contract，同时不改 importer、Song Select 或现有 visual consumer。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter FullyQualifiedName~BmsBeatmapDecoderTest` **31/31** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter FullyQualifiedName~BmsBeatmapConverterTest` **12/12** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal` **803/803** 通过。

### P1-K：K3-C 补齐 BGA / invisible / mine 的最薄 typed surface

- `BmsDecodedChart` 现新增 `BgaEvents`、`InvisibleObjectEvents` 与 `MineEvents`；`BmsBeatmapDecoder` 也已对 BGA base/poor/layer/layer2、invisible object 与 landmine channel 建立 typed post-process 分派，不再要求下游从 raw carrier 重新猜 channel 语义。
- 本轮只收口 parse/model additive surface，不改 converter、importer、runtime 或现有 visual consumer；第一批 typed slot 先为后续背景层、统计面与特效谱支持冻结中间模型合同。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter FullyQualifiedName~BmsBeatmapDecoderTest` **29/29** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter FullyQualifiedName~BmsBeatmapConverterTest` **11/11** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal` **800/800** 通过。

### P1-K：K3-B 补齐 LNTYPE 2 的最小 MGQ long-note expression

- `BmsBeatmapDecoder` 现会在 LN channel 保留显式 `00` 作为 `LNTYPE 2` closing marker，并把 duplicate line compound 收口为“`00` 不覆盖已有对象”；这让 MGQ 长条可以跨小节连续，并在 zero slot 处收口，而不再停留在 warning-only。
- `BmsLongNoteEncoding` 新增 `LnType2`，`BmsBeatmapConverter` 也已通过 focused regression 证明该最小表达可沿既有 hold-note conversion path 端到端转成 `BmsHoldNote`。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter FullyQualifiedName~BmsBeatmapDecoderTest` **26/26** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter FullyQualifiedName~BmsBeatmapConverterTest` **11/11** 通过。

### P1-K：K3-A 冻结同拍位 control-event 顺序与 signed BPM converter contract

- `BmsBeatmapConverter` 现会先应用同拍位 `BPM` 与 `STOP`，再结算 object / long-note endpoint 的 event time，converter authority 固定为 `BPM -> STOP -> object`。
- signed BPM 的 timeline 推进现按绝对值消费；negative `#BPMxx` 不再在 converter 里被错误钳成 `1 BPM`。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "FullyQualifiedName~BmsBeatmapConverterTest"` **8/8** 通过。

### P1-K：K2-A 让 signed BPM 与 duplicate line 进入 parser contract

- `BmsBpmChangeEvent` 现允许 non-zero signed BPM 进入 typed model，`BmsBeatmapDecoder` 也会保留 negative `#BPMxx`，不再在 parser 阶段直接丢掉方向信息。
- decoder 的 typed post-process 现已对同 `measure/channel/fraction` 的 duplicate channel collision 做 source-order-aware compound；raw carrier 继续完整保留全部原始 channel events。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "FullyQualifiedName~BmsBeatmapDecoderTest"` **23/23** 通过。

### P1-K：K1-B 把 scroll / unknown bag 接进 no-loss 保留层

- `BmsBeatmapInfo` 现新增 `ScrollTable` 与 `UnknownHeaders`；`BmsBeatmapDecoder` 会保留 `#SCROLLxx` 定义，并把未识别的 header / indexed definition 写入 unknown bag，而不是继续静默跳过。
- decoder 现也会接受 `SC` 这类非十六进制 channel token，并将其作为 raw placeholder 写入 `RawChannelEvents`；这让 `SCROLLxx/SC` 首次同时进入模型，但当前仍只停留在 no-loss 保留层。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "FullyQualifiedName~BmsBeatmapDecoderTest"` **21/21** 通过。

### P1-K：K1-A raw carrier 首刀开始落地

- `BmsDecodedChart` 现已显式暴露 `RawChannelEvents`，并保留 `ChannelEvents` 作为兼容别名；`BmsChannelEvent` 现新增 `RawChannelToken` 与 `SourceLineOrder`，`BmsBeatmapDecoder` 也会按 source channel line 写入这两个字段。
- 这一刀让 raw channel carrier 不再只是隐式 fallback 列表，并把同 `measure/fraction/channel` 下的最终 tie-break 固定到 `SourceLineOrder`，作为后续 duplicate line 语义收口前的最低 no-loss carrier。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "FullyQualifiedName~BmsBeatmapDecoderTest"` **20/20** 通过。

### 文档：新建 P1-K 子线并冻结 BMS 解析链路治理范围

- 已新建 `doc_md/subline/P1-K/` 四件套，并把 **BMS 解析链路治理** 正式编排为独立 Phase 1.x 子线；主 authority 明确落在 `BmsBeatmapDecoder`、`BmsDecodedChart`、`BmsBeatmapInfo`、`BmsBeatmapConverter` 与 parse-side projection reuse，不再继续散挂到 `P1-H`、`P1-J`、`P1-E` 或 consumer 侧的 ad hoc parse。
- 主线总规划、主线状态页、子线索引已同步加入 `P1-K`，并冻结首轮执行顺序为：`raw/typed 双层模型冻结` → `header/definition/channel no-loss coverage` → `timeline/control-event semantics` → `parse-once/project-many 复用` → `focused validation 与缓存边界`。
- 本轮文档还把当前 parse-chain 的主要 gap 统一写成子线基线：`SCROLLxx/SC` 未入模型、signed BPM typed surface 不可表示、duplicate channel line 未 compound、同拍位 `BPM/STOP/object` 顺序未冻结，以及 BGA layer / mine / invisible note 仍缺最薄 typed slot。
- 本轮仅完成文档规划与主线编排，无生产代码改动、无新增测试执行；代码与验证基线继续沿用同日 `788/788` 的主线快照。

### BMS：稀疏 9K_Bms 判定、导入 warning 透传与静态背景链统一修复

- `BmsBeatmapDecoder` 现把“非 `.bme` 且出现 `channel 17`”视为 sparse `9K_Bms` 的合法早期信号，不再要求九个 lane 全部出现后才进入 9 键路径；同时保留 `.bme` 的现有 7 键兼容约定，避免回归既有 seven-key `.bme` conversion/tests。
- `BmsFolderImporter` / `BmsBeatmapImporter` 现会保留 decoder 的 non-fatal warnings：导入成功时会额外发出一条 parser-warning 摘要通知，并把逐 chart 的 warning 详情写入日志，而不是继续静默吞掉降级解析结果。
- 静态背景链已统一为 `STAGEFILE > BACKBMP > BANNER`：converter 的 metadata 优先级已与 `BmsBackgroundLayer` 对齐；导入期会把背景文件名规范化到实际存在的图片文件，支持常见扩展名替换；`WorkingBeatmapCache` 也会对旧 metadata 做图片扩展名 fallback；默认 `BmsBackgroundLayer` 在有当前 `WorkingBeatmap` 时会优先尝试显示真实背景贴图，而不再只显示文件名。
- 验证：`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过；`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "(FullyQualifiedName~BmsBeatmapDecoderTest|FullyQualifiedName~BmsScoreProcessorTest)"` **79/79** 通过；`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "(FullyQualifiedName~BmsBeatmapConverterTest|FullyQualifiedName~BmsImportIntegrationTest|FullyQualifiedName~BmsDrawableRulesetTest)"` **94/94** 通过；`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal` **788/788** 通过。

## 2026-05-18

### P1-I：BMS 搜索语法公开口径统一为 `rice`

- BMS Song Select 搜索框 tooltip 已把 `rc / regular` 更正为 `rc / rice`，避免把 `rc` 误解释成错误的长写；当前公开语法统一为 `key/keys`、`rc/rice`、`ln`、`scr`。
- `BmsFilterCriteria` 已同步支持 `rice` 关键字；`regular` 继续仅作为向后兼容 alias 保留，避免旧查询立即失效，但后续文档、提示与用户口径不再把它作为公开写法。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter FullyQualifiedName~BmsFilterCriteriaTest` **4/4** 通过。

### P1-J：BMS full autoplay 分流到对象级 `AutoPlay` 与 direct-time replay 采样

- dense full autoplay 不再继续尝试放宽 core `FramedReplayInputHandler` 合同；当前实现改为只对 BMS full autoplay 分流：`DrawableBmsRuleset` 会给 full autoplay 下的 `BmsHitObject` 设置对象级 `AutoPlay`，并改用 `BmsAutoplayReplayInputHandler` 直接按当前时间采样 replay state。
- 这条分流让 replay 输入继续服务 `ReplayPlayer` / HUD / key counter，但不再让 dense full autoplay 的 correctness 继续依赖逐 replay frame 边界推进。普通 replay 仍保留原有 `BmsFramedReplayInputHandler` 与 one-boundary-per-call stepping contract。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter FullyQualifiedName~TestSceneBmsAutoplayReplayPlayback` **3/3** 通过；相邻回归 `dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "FullyQualifiedName~TestSceneBmsAutoplayReplayPlayback|FullyQualifiedName~BmsReplayFrameTest|FullyQualifiedName~TestSceneBmsReplayStability|FullyQualifiedName~TestSceneBmsReplayRecording|FullyQualifiedName~TestAutoPlayObjectsStillApplyMaxResult"` **11/11** 通过。

### P1-J：补上 full autoplay keysound 预热，前移首次 sample pool 初始化

- 进一步排查 dense full autoplay 剩余的“整局只卡一次”后，当前更具体的结论是：core `Playfield` 虽然会预建 `hitObject.Samples` / `AuxiliarySamples` 的 sample pool，但 BMS gameplay keysound 走的是 `BmsKeysoundStore` 专用路径，并不自动吃这条通用预热链。
- 现在 `DrawableBmsRuleset` 会在 full autoplay 的 `LoadComplete()` 时收集 beatmap 内唯一 `BmsKeysoundSampleInfo`，并通过 `BmsPlayfield.PrewarmKeysounds()` / `Playfield.PrepareSamplePool()` 提前建好底层 sample pool，把首次命中的懒初始化成本前移到进场加载。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "FullyQualifiedName~TestSceneBmsAutoplayReplayPlayback|FullyQualifiedName~TestAutoPlayObjectsStillApplyMaxResult"` **4/4** 通过；邻接 keysound 回归 `dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "FullyQualifiedName~TestSceneBmsSharedKeysoundTiming|FullyQualifiedName~TestSceneBmsKeysoundPlaybackLifecycle|FullyQualifiedName~TestSceneBmsKeysoundChannelConfigBinding"` **9/9** 通过。

## 2026-05-17

### P1-J：pause/seek 语义、shared keysound 生命周期与 dense-chart hot path 继续收口

- `BmsKeysoundStore` 已补上 gameplay pause / seek 生命周期回收，player harness `TestSceneBmsPlayerAudioSemantics` 也独立锁住了 `GameplayClockContainer` pause / resume 持位与 `BmsBgmEvent` seek 后重播两条当前用户侧最关心的语义。
- 同轮 `BmsLane` 移除了玩家命中后的重复 ordered-hit 扫描，empty-poor 检查去掉 per-press `HashSet` 分配，shared store 单样本路径改成 channel-local 双缓冲；`DrawableBmsHoldNote.resolveBodyTicksUpToCurrentTime()` 也改成遇到首个 future tick 就 early-break，避免 dense long-note 场景每帧扫完整条 body tick 尾部。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "FullyQualifiedName~TestSceneBmsPlayerAudioSemantics"` **3/3** 通过；targeted shared-store / ordered-hit focused suite **11/11** 通过；`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v minimal --filter "FullyQualifiedName~BmsDrawableRulesetTest|FullyQualifiedName~BmsGaugeProcessorTest"` **111/111** 通过。

## 2026-05-16

### P1-J：shared keysound timing focused proof 补齐，并把 pooled sample retrieval 收口为安全降级边界

- 新增 `TestSceneBmsSharedKeysoundTiming`，把 `DrawableBmsHitObject` 命中与 `BmsLane` lane replay 的 same-frame shared-store 请求独立成 owner-level focused suite，不再只靠 `BmsDrawableRulesetTest` 间接覆盖。
- 该 focused scene 还暴露出 lane replay 的 pooled sample retrieval 可能把错误冒泡到 gameplay 链；`Playfield.GetPooledSample()` 现已在 pool 未 ready 或取样失效时返回 `null`，由既有 `SkinnableSound` consumer contract 自动降级为 unpooled sample。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~TestSceneBmsSharedKeysoundTiming"` **3/3** 通过；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release` **774/774** 通过。

### P1-J：补齐 `KeysoundConcurrentChannels` 的 drawable binding coverage 与 settings 口径同步

- 新增 headless focused suite `TestSceneBmsKeysoundChannelConfigBinding`，把 `RulesetConfigs` 中的 `KeysoundConcurrentChannels` 改值真实驱到 `DrawableBmsRuleset -> BmsPlayfield.KeysoundStore`，覆盖初始加载和 live update 两条链路。
- `BmsSettingsSubsection` 的 `键音通道数` hover 提示现已明确 grow-immediately / shrink-deferred 的实际语义：调高会立即补充可用通道，调低会等超额 channel 自然停播后再逐步回收，不再暗示 runtime 改值会直接切断当前音频。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~TestSceneBmsKeysoundChannelConfigBinding"` **3/3** 通过。

### P1-J：BMS live channel resize 首刀落地，runtime 改值不再整池切音

- `BmsKeysoundStore` 已把 `KeysoundConcurrentChannels` 的 live 改值从 rebuild-all 改成 non-destructive resize：grow 立即扩容，shrink 延后到超额 channel 停播后再裁剪，不再通过整池 `Clear()` 立刻切断当前键音。
- `BmsDrawableRulesetTest` 新增 shrink 保活与停播后回收两条回归，当前 focused slice 已扩大到 **60/60**；完整 `osu.Game.Rulesets.Bms.Tests` 现为 **766/766**。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsDrawableRulesetTest"` **60/60** 通过；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release` **766/766** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### P1-J：BMS gameplay hot path 首轮代码优化开始落地

- `BmsKeysoundStore` 已移除 gameplay keysound 的无条件下一帧 `Schedule()`，并新增数组快路径与单样本入口；命中与 lane replay keysound 默认改走 same-frame 播放。
- `BmsLane.shouldTriggerEmptyPoor()` 与 `BmsOrderedHitPolicy.getParticipatingHitObjects()` 已去掉首批按键热路径对象物化；`DrawableBmsHitObject.PlaySamples()` 也已收口到单样本 keysound 路径。
- focused validation：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsDrawableRulesetTest"` **58/58** 通过；补回缺失 chart filter stats 合同后，更宽 `osu.Game.Rulesets.Bms.Tests` 全量回归已恢复，当前最新快照为 **766/766**；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### BMS：缺失 chart filter stats 不再静默过滤，test resolver backfill 合同恢复

- `BmsFilterCriteria` 现继续优先使用 metadata / cache 中的 chart filter stats，但当 stats 缺失时不再直接把 beatmap 静默过滤掉；未知 stats 当前会先放行，等待后台缓存或显式回填收紧匹配结果。
- 为避免把 song-select filter loop 重新改回 working-beatmap I/O，本轮只在 test resolver 路径上显式调用 `GetOrBackfill()`；这让测试仍可验证“可用时回填并写回 metadata”的合同，同时不把运行时过滤循环重新变重。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsFilterCriteriaTest"` **4/4** 通过；完整 `osu.Game.Rulesets.Bms.Tests` 当前最新快照为 **766/766**。

### 文档：新建 P1-J 子线承接 BMS gameplay runtime 性能与音频时序治理

- 已正式建立 `doc_md/subline/P1-J/` 四件套，把 shared `BmsKeysoundStore` 播放时序、`BmsLane` / `BmsOrderedHitPolicy` 热路径、sample allocation tightening 与 live channel resize 安全合同统一归线到 `P1-J`。
- 本轮明确判定：该专题不并入 `P1-C` 或 `P1-E`；`P1-C` 继续负责判定/反馈语义与回归守门，`P1-E` 继续负责真实谱面验校，而 `P1-J` 单独拥有 dense-chart audio/runtime hot path 的优化 authority。
- 当前仅完成规划与主线同步，无生产代码改动、无新增测试执行。

### BMS：pre-start delay 在 hold 期间耗尽后改为松手重给满一段 delay

- `BmsSoloPlayer` 现会在 `UI_PreStartHold` 仍按住时允许既有 delayed-start 正常耗尽，但若用户是在 delay 已过后才松手，则 runtime 会重新调度一整段 fresh delay，而不是立即开始 gameplay。
- 该修复保留了原有 press-side reset 语义：快速重新按下 hold 仍会重置 pre-start 窗口；变化只收口在 release-after-elapsed 分支。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~TestSceneBmsSoloPlayerPreStart|FullyQualifiedName~TestSceneBmsSoloPlayerPreStartScheduledDelay"` **24/24** 通过。

### BMS：pre-start 1 号普通轨纯视觉流速预览首轮落地

- `BmsSoloPlayer` 现会把 pre-start pending / hold / pause state 下发给 `DrawableBmsRuleset`，仅在 actual gameplay 未开始且 hold 生效时显示 preview。
- `BmsHitObjectArea` / `BmsLane` 新增了独立 preview 容器；`DrawableBmsRuleset` 现会把 skinnable fake note 固定挂到第一非 scratch 普通轨，并继续复用 `BmsNoteSkinLookup` 与 `BmsScrollSpeedMetrics`。
- 该 preview 不进入 judgement / score / keysound / replay 链；`TestSceneBmsSoloPlayerPreStart` 已扩到 **24/24**，覆盖 lane 选择、动画 / pause freeze 与“正式 gameplay 不再出现 preview”。

### 文档：pre-start 1 号普通轨纯视觉流速预览完成可行性评估与归线规划

- 已确认该需求可在当前 BMS 架构内安全实现，但实现路径必须是 pre-start-only 的纯视觉 preview layer，而不是伪造真实 `BmsHitObject` / `DrawableBmsHitObject`。
- 文档现已统一收口为同一条 `P1-A / P1-C` 交叉线：`P1-A` 负责 playfield / lane 宿主与 fallback，`P1-C` 负责可见性 gate、第一非 scratch 轨选择，以及“不参与判定 / 成绩 / 键音 / replay”的运行时硬约束。
- 同步记录了一个关键实现细节：5K / 7K / 14K 当前 raw `laneIndex = 0` 是 scratch，因此“1 号轨道”应按第一非 scratch 普通轨解析；9K 才可直接落第一轨。
- 本轮仅完成文档与 memory 规划，无生产代码改动、无新增测试执行。

### BMS：结果侧 clear lamp / gauge history 与 gameplay mod 链重新对齐

- `BmsClearLampProcessor` 现会先检查 clear condition 再授予 `PERFECT` / `FULL COMBO`；`HCN` body-tick `IgnoreMiss` 即使不改变 EX-SCORE 或 `Good/Meh/Miss` 计数，也不会再把 failed run 持久化成更高 lamp。
- 结果侧 `CreateGaugeHistory()` 与 fallback final gauge 重算现会先通过 `BmsBeatmapModApplicator` 重新应用完整 beatmap-mod 链，而不是只重放 long-note mode；因此 `A-SCR` / `A-NOT` 这类会改写 score/gauge 池的 assist mod 不再让 results/history 偏离 gameplay。
- `BmsClearLampProcessorTest` 新增 HCN gauge-fail lamp persistence 与 assist-mod gauge-history replay 回归，锁住此次修正。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore --filter "FullyQualifiedName~BmsClearLampProcessorTest"` **30/30** 通过；`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore --filter "FullyQualifiedName~BmsClearLampProcessorTest|FullyQualifiedName~BmsGaugeProcessorTest"` **80/80** 通过。

## 2026-05-13

### P1-I：BMS 选歌筛选 UI 视觉收口

- `BmsCompositionRowButton` / `BmsKeyCountToggleButton` hover 效果修复：非激活态改为 `ColourProvider.Background3/Background1`，`ShearedButton` 内置 `Lighten(0.2f)` 机制现在产生清晰可见的色变（原使用 `Color4.Black.Opacity()` 导致 hover 几乎不可辨）。
- RC/LN/SCR 颜色重排：RC=蓝(94,190,255)、LN=黄(255,212,92)、SCR=橙(255,119,86)；`SearchHintTooltip` BMS 段落强调色同步更新为蓝色匹配 RC。
- `SearchHintTooltip` DI 崩溃修复：`[Resolved] OverlayColourProvider` 移到 `SongSelectSearchTextBox`（确在 SongSelect DI 作用域内），通过构造函数传入 tooltip（对齐 `ModTooltip` 模式）；同时把不稳定的 `GridContainer + AutoSizeAxes.Both` 布局替换为 `FillFlowContainer + Container(Width=160f)`。
- `I3` 实质完成：`BmsCompositionFilterControl` 单轨控件已全面落地，符合"单轨上限段+尾段空白容差+独立启停"产品合同；`I4` 回归收口进行中（单轨拖拽 headless regression 待补）。
- 验证：`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` **0 error**。

### P1-I：文档校正 `谱面构成` 最终产品合同

- `P1-I` 的 `谱面构成` 需求已进一步冻结为：单轨、从左到右 `RC / LN / SCR` 三个可编辑上限段、三段独立启用/禁用、尾段为空白容差。
- `RC / LN / SCR` 三个值现在明确表示各自的最大占比，不强制和为 `100%`；shared `FilterControl` 里的“三条独立 range slider”只保留为一次原型尝试，不再被视为 `I3` 已完成交付。
- visual UI 首轮只负责生成 enabled segment 的上限约束；文本 `rc/ln/scr` 语法继续保留完整范围表达能力。本轮仅做文档校正，无代码变更、无新增测试执行。

### P1-I：BMS 选歌筛选与搜索定制进入首轮代码落地

- `BmsBeatmapMetadataData` 现已具备 persisted `ChartFilterStats`，并由 importer / reuse 链写入与自愈；`BmsFilterCriteria` 与 `BmsRuleset.CreateRulesetFilterCriteria()` 也已接入 `key/keys`、`rc`、`ln`、`scr` custom search。
- shared `FilterControl` 已切出 BMS-only `谱面构成` / `键数` rows，并显式避免隐藏 star slider 继续污染 BMS `UserStarDifficulty`。
- focused validation 已新增并通过 BMS 侧 importer / statistics / criteria / Song Select FilterControl 切片：`dotnet test osu.Game.Rulesets.Bms.Tests -p:GenerateFullPaths=true --filter "FullyQualifiedName~BmsImportIntegrationTest|FullyQualifiedName~BmsBeatmapStatisticsTest|FullyQualifiedName~BmsFilterCriteriaTest|FullyQualifiedName~TestSceneBmsFilterControl"` **30/30**；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### 文档：新建 P1-I 子线并收口 BMS 选歌筛选/搜索规划

- 新建 `doc_md/subline/P1-I/` 四件套，正式把 **BMS 选歌筛选与搜索定制** 作为独立 Phase 1.x 子线维护，不再硬并入 `P1-A` 或 `P1-H`。
- 当前文档已把首轮执行顺序冻结为：`read-model 建模` → `ruleset criteria / custom search` → `BMS-only FilterControl UI` → `focused regression`，并明确 `键数` 已有 authority、`RC/LN/SCR` 仍缺 persisted filter stats，因此不能跳过 metadata/read-model 直接做 UI。
- 第二轮复查已把具体代码锚点、测试落点、`谱面构成` 交互降级路线与建议验证命令补进 `P1-I` 文档，并把两条全局技术纪律同步写入 `OMS_COPILOT.md`：BMS filter data 走 typed metadata helper，BMS custom search 继续走 `IRulesetFilterCriteria`。
- 主线总规划、主线状态页与子线索引已同步加入 `P1-I`；当前只完成文档治理，无代码变更、无新增测试执行。

## 2026-05-09

### P1-F / Shared：single-file 发行包补齐完整自解压并复核冷启动

- `build-release.ps1` 现已在 `PublishSingleFile=true` 之外显式保留 `IncludeAllContentForSelfExtract=true`，避免 fresh extract 的便携发行物首次运行只创建初始 `data/` 后就无窗退出。
- 本轮复核确认：问题根因在 single-file 发行物的自解压行为，而不在用户启动流程；修正后，新解压的便携 zip 可正常冷启动，程序窗口与运行期存活状态都已恢复。
- 验证：重新执行 `.\build-release.ps1` 生成发行包后，fresh extract 冷启动通过；`.\SmokeTestDesktop.ps1 -Configuration Release -WaitSeconds 8` 通过。

### Shared / Tooling：Release 构建告警已清零

- 本轮把 VS Code “以非调试模式运行”路径下暴露的 15 条告警收口为 0：`GameplayClockContainer` 的 `CS1574`、多处 localisation OLOC、以及 `ppy.LocalisationAnalyser` 抛出的 `AD0001` 均已处理。
- 根因上，OMS-owned localisation 文案已从混合 helper 的 `*Strings.cs` 中拆到独立类，统一回到分析器可识别的 `getKey()` 模式；同时 XMLDoc 摘要与 `.resx` fallback 也已重新严格对齐。
- 验证：`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过，当前为 `0 warning / 0 error`。

### P1-A / P1-H：数据目录迁移入口文案与结果说明收口

- Settings → 常规 → 安装位置 现已把入口明确为 `更改数据目录位置`，不再误导成移动程序文件；迁移选择页也已直接说明三类结果：空目录直接迁入、非空非数据目录改用其下 `oms/` 子目录、已是可用数据目录则仅在重启后切换。
- 这次收口明确了该入口的真实 authority：它只切换/迁移运行时数据根，不移动程序文件；便携 build 选择新目录后，原同级 `data/` 也不再继续作为当前数据根。
- 验证：`dotnet build .\osu.Game\osu.Game.csproj -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### Workspace：关闭 Python 终端自动激活以避免打断发行脚本

- 工作区级 [../../.vscode/settings.json](../../.vscode/settings.json) 现已加入 `python.terminal.activateEnvironment = false`，避免 VS Code 直接点 Run 执行 `build-release.ps1` 时，新 PowerShell 终端在前台 `dotnet publish` 过程中又被 `.venv` 自动激活命令打断。
- 这次修正不改变 OMS 正式工具链：当前仓库没有 Python 源文件、`pyproject.toml`、`requirements.txt` 或 Python 任务；根目录 `.venv/` 仅是本地工作区环境，不属于 OMS 正式构建 / 测试 / 发行链。
- 验证：工作区 [../../.vscode/settings.json](../../.vscode/settings.json) 已更新且无错误；仓库级 `.vscode/settings.json` / `.vscode/tasks.json` 未发现项目级 Python 依赖配置。

### P1-F：发行包新增中英双语手动更新说明

- `build-release.ps1` 现会在发行根目录生成 `how to update.txt`，并随 `oms_YYYYMMDD(.zip)` 一起打包；该文件同时提供中文与英文的手动覆盖更新步骤，并以更精炼的终端用户口径强调“覆盖整个压缩包内容”以及“便携模式保留 `portable.ini` / `data/`”。
- 发行说明与 `P1-F` 状态文档已同步到“当前发行根目录额外包含一份中英双语手动更新说明”的口径。
- 验证：PowerShell 语法解析通过；实际执行 ` .\build-release.ps1 ` 成功生成 `release-repo/oms_20260509_2.zip`，且 `publish/` 与 zip 根目录均已确认包含 `how to update.txt`。

### P1-F / Shared：离线覆盖更新审计与 OMS 版号兼容硬化

- 重新审计了当前离线发行链：`build-release.ps1` 继续产出 `release-repo/oms_YYYYMMDD(.zip)`，根目录布局为 `osu!.exe` + `portable.ini` + `lazer.ico` + `beatmap.ico`；覆盖更新时真正需要保留的是便携模式下的 `data/` 与任何自定义数据根使用的 `storage.ini`，而不是假定“严格只有一个 exe”。
- 代码侧补上了两处未来切换内部 OMS 版号前必须具备的兼容护栏：`ChangelogOverlay.ShowBuild(string)` 不再硬拆 `版本-流`，遇到纯 OMS 版号会安全回退列表；`OsuConfigManager.Migrate()` 现可同时解析旧上游日期版号与 `oms_YYYYMMDD`，避免迁移逻辑静默失效。
- 同轮已把 [../other/RELEASE.md](../other/RELEASE.md)、根 [../../README.md](../../README.md) 与 `P1-F` 文档同步到当前发行物命名、覆盖更新步骤和注意事项口径。
- 验证：`dotnet build .\osu.Game\osu.Game.csproj -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### P1-A：osu!mania 滚动速度设置提示收口为参考值

- `ManiaSettingsSubsection` 现已为 `滚动速度` slider 补上 hover 提示，明确括号毫秒只代表标准车道几何下的参考下落时间。
- 不同 mania 皮肤可通过车道尺寸、判定线位置与缩放改变可见下落长度，因此同一数值不保证跨皮肤体感一致；更换皮肤后应按当前皮肤重新校准，且 mania / BMS 的下落时间不可互相参考。
- 这次改动不修改 `DrawableManiaRuleset.ComputeScrollTime()` 或 mania runtime authority，只收口 settings-entry surface 的解释边界。
- 验证：`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### P1-A / P1-C：BMS Hi-Speed 设置面改为“三模式简述 + 基础下落时间”

- `BmsSettingsSubsection` 现已把 `Hi-Speed 模式` 的 hover 文案改为三种模式的功能区别简述：`Normal` 为基础定速、`Floating` 为按谱面初始 BPM 做补偿、`Classic` 为传统 Hi-Speed 语义。
- 当前模式的 Hi-Speed 数值 slider 现会在数值后追加括号内的基础下落时间（ms）；该数值明确按“不启用 `Sudden / Hidden / Lift`”计算，不再与 runtime `GreenNumber` / 可见时间混写。
- 对应 slider hover 文案现已同步收口为：“括号内为不启用 sudden/hidden/lift 的下落时间（ms），绿字（GreenNumber）需要在游戏内结合 sudden/hidden/lift 调节查看”。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsRulesetConfigurationTest"` **12/12** 通过。

### P1-A / 1.6：BMS 键音通道默认值与设置提示收口

- `BmsKeysoundStore.DEFAULT_CONCURRENT_CHANNELS` 现已从 `16` 提高到 `32`，`Settings -> 游戏模式 -> BMS -> 键音通道数` 继续保持 `1..256` 的共享播放池上限调节范围。
- `BmsSettingsSubsection` 现为 `键音通道数` 滑条补上多行 hover 提示，直接概括低值更容易截断 BGM / 键音 / 长按尾音、高值更适合极高密谱面或较强机器，以及“缺音时先升到 `48/64`、额外负载增加时再逐步下调”的调参路径。
- 这次改动属于 BMS settings product surface 的默认值与说明收口，不改变 shared `BmsKeysoundStore` 的 runtime authority：BGM / note / LN / lane replay 仍共用同一池，运行时改值仍会因重建 channel container 而切断当前正在播放的键音。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsRulesetConfigurationTest|FullyQualifiedName~BmsDrawableRulesetTest"` **70/70** 通过。

### P1-A / P1-B：桌面端输入设置安全隐藏上游 mouse/touch/tablet 分区

- `OsuGameDesktop` 现已 override `CreateSettingsSubsectionFor(InputHandler)`，在桌面宿主的 Settings -> 输入 中对 `ITabletHandler`、`TouchHandler` 与 `MouseHandler` 返回 `null`，因此上游通用的数位板 / 触屏点击 / 鼠标 subsection 不再继续暴露给最终桌面产品面。
- 这次变更属于 **安全隐藏** 而不是 runtime 删除：`MouseDisableButtons`、`MouseDisableWheel`、`ConfineMouseMode`、`TouchDisableGameplayTaps` 等既有配置与运行时消费链全部保留，tablet/touch/mouse input handler 也未被移除。
- 该裁剪故意保持在 `osu.Desktop` 的 `OsuGameDesktop` 层，而不是下移到 `OsuGameBase`；这样 desktop product surface 会收口，但 `OsuGame` test scene / 非 desktop host 的输入设置装配行为不会被一并改写。
- 验证：`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

## 2026-05-08

### BMS：Song Select 左上 BPM 显示不再回退 60

- `BmsImportedBeatmapFactory` 现会把首次 ruleset conversion 得到的 `ControlPointInfo`、`HitObjects` 与 `Breaks` 复用回 `BmsDecodedBeatmap` raw wrapper，修正 `BeatmapTitleWedge` 这类直接读取 `WorkingBeatmap.Beatmap` 的 raw consumer 在 BMS imported chart 上缺少 timing data、从而恒显示默认 `60 BPM` 的问题。
- 这次修补不改变 BMS Song Select 既有的 BPM 分组与排序 authority；分组 / 排序仍继续消费 persisted `BeatmapInfo.BPM`，本轮只把 raw working beatmap display chain 与之重新对齐。
- 新增 `BmsImportIntegrationTest.TestLoaderPopulatesTimingDataForSongSelectDisplays()`，锁定 BMS loader 返回的 raw beatmap 已带有正确 timing point、most-common beat length 与 hitobject 数据。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsImportIntegrationTest"` **23/23** 通过。

### P1-A / P1-C：`阻止谱面开始/ingame start` 改为全程调速修饰键

- `BmsInputStrings.PreStartHold` 的设置面可见名称现已改为 `阻止谱面开始/ingame start`；该动作继续保留独立默认键位（5K/7K/9K = `Q`，14K = `T`），`UI_LaneCoverFocus` 仍保持 click-to-cycle 的独立目标循环键。
- `BmsSoloPlayer` 现把 `UI_PreStartHold` 收口为“前 5 秒阻止开始 + 全程调速修饰键”这一单一运行时合同：前 5 秒按住时继续阻塞真正开谱，正式 gameplay 开始后按住同一键仍可继续用奇数列增速、偶数列减速。
- `BmsInputManager` 现会在 hold 修饰键按住期间停止把新的 lane action 转发进 gameplay `KeyBindingContainer`，因此同一组 lane 键在 hold 期间只承担 Hi-Speed 调节，不再同时进入正常判定链；已在 hold 前转发过的按下态仍会沿原链正确释放。
- 居中的 `BMS speed` toast 现会在 hold 修饰键按住期间持续刷新显示；右侧 `READY HOLD` overlay 仍只保留给前 5 秒阻止开谱窗口，不会错误常驻到正式 gameplay。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~OmsInputRouterTest"` **9/9** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~TestSceneBmsSoloPlayerPreStart"` **10/10** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~OmsInputBridgeTest"` **23/23** 通过。

## 2026-04-29

### BMS：难度表 manager 同步、RefreshAll 合同与 wrapper identity 收口

- `BmsDifficultyTableManager` 现已正式拥有 persisted beatmap metadata 回写职责；`导入 / 刷新 / 启用 / 禁用 / 移除` 来源后，会先把既有 BMS 谱面的难度表 metadata 回写到 realm，再发出 `TableDataChanged`。`BmsTableMd5Index` 现只保留内存索引职责，不再承担 persisted metadata 同步 authority。
- `RefreshAllTables()` 现返回结构化结果，Settings → 游戏模式 → BMS → 难度表 已改为按真实结果区分“全成功 / 部分成功 / 全失败”，不再在 partial failure 下 blanket success。
- `RefreshAllTables()` 现还会逐源报告进度；Settings → 游戏模式 → BMS → 难度表 的“全部刷新”在长任务期间会持续更新进度摘要，并通过 `ProgressNotification` 展示处理中数量与完成状态，避免大库刷新期间只有最终结果而缺少过程反馈。
- `index.html -> header.json -> body` 的 wrapper/source identity 已补稳定 fallback：`index` / `header` 这类 generic 文件名会优先回落到父目录名，并在递归解析链上保留初始 fallback；同时 preset 认领也已收紧为“仅当 display name 本身就是 fallback 时，才允许按 `source_name` 命中 preset”，避免显式 `name` 被过度认领。
- 响应性后置的首个切片也已落地：persisted metadata 回写不再在单个长事务里对所有 BMS 谱面做全量重写，而是先计算受影响 MD5 集合，再按 beatmap id 分批写入，减少大库下的长写锁与无关谱面的 JSON 处理成本。
- `BmsFolderImporter` 现还会在复用已有 beatmap set（例如 internal/external rebuild 命中相同 set hash）时，重新按当前 table index 套用 difficulty table metadata，避免旧 set 因沿用历史 persisted metadata 而继续在 Song Select 中落入 `Unrated`。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsDifficultyTableManagerTest.TestManagerMutationsUpdatePersistedBeatmapMetadataWithoutIndexOwner|FullyQualifiedName~BmsTableMd5IndexTest|FullyQualifiedName~BmsImportIntegrationTest.TestDifficultyTableRefreshUpdatesPersistedImportedBeatmaps" -v n` **4/4** 通过；`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsDifficultyTableManagerTest.TestRefreshAllReturnsPartialFailuresAndStillAppliesSuccessfulSources|FullyQualifiedName~BmsDifficultyTableManagerTest.TestManagerMutationsUpdatePersistedBeatmapMetadataWithoutIndexOwner|FullyQualifiedName~BmsDifficultyTableManagerTest.TestRefreshTableUpdatesEntriesAndEnabledLookupRespectsToggle" -v n` **3/3** 通过；`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsDifficultyTableManagerTest.TestImportRemoteHtmlWrapperWithoutNameKeepsStablePresetIdentity|FullyQualifiedName~BmsDifficultyTableManagerTest.TestImportRemoteHtmlWrapperRefreshesRelativeSources|FullyQualifiedName~BmsDifficultyTableManagerTest.TestImportMatchingBundledPresetUsesSeededSource" -v n` **3/3** 通过；`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsDifficultyTableManagerTest.TestRefreshAllReportsProgressPerProcessedSource|FullyQualifiedName~BmsDifficultyTableManagerTest.TestRefreshAllReturnsPartialFailuresAndStillAppliesSuccessfulSources|FullyQualifiedName~BmsDifficultyTableManagerTest.TestManagerMutationsUpdatePersistedBeatmapMetadataWithoutIndexOwner" -v n` **3/3** 通过；`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsImportIntegrationTest.TestManagedDirectoryReuseReappliesDifficultyTableMetadata|FullyQualifiedName~BmsImportIntegrationTest.TestManagedDirectoryRegistrationPreservesRelativeManagedPath|FullyQualifiedName~BmsImportIntegrationTest.TestFolderImporterAppliesDifficultyTableMatchesDuringImport|FullyQualifiedName~BmsImportIntegrationTest.TestDifficultyTableRefreshUpdatesPersistedImportedBeatmaps" -v n` **4/4** 通过。

### 文档：BMS 难度表修补专题归线为 P1-H

- 针对难度表方向新增一轮 correctness-first 修补规划：不新开独立主/子线，不重开 `1.13` / `1.15` 的完成判定，而是把专题正式挂到 `P1-H` 下，作为“来源变更 -> 已有谱面 metadata -> Song Select / 详情消费面”一致性合同的修补切片。
- 主线与 `P1-H` 文档现已同步明确当前推进顺序：`既有谱面 metadata 同步` → `RefreshAll 真实结果合同` → `wrapper/source identity fallback` → `大库响应性`；`P1-A` 只记录 settings / first-run 等共享产品表面的从属影响。
- 本轮仅完成文档与约束建档，无代码变更、无新增测试执行。

## 2026-04-28

### BMS：外部谱库 / 内部谱库选歌分组落地

- BMS Song Select 分组下拉现已新增 `外部谱库` 与 `内部谱库` 两个 BMS-only 模式，并继续保持 ruleset-driven 可见性；共享 `GroupMode` 已在不破坏既有持久化兼容性的前提下扩展，非 BMS ruleset 不会暴露这两个模式。
- `BeatmapCarouselFilterGrouping` 已从只特判 `DifficultyTable` 的层级路径，泛化为 ruleset-specific hierarchical grouping；BMS 现在可通过 `GetSongSelectGroupDefinitions()` 同时驱动难度表、外部谱库与内部谱库三种层级分组，而不再继续堆新的共享层特判。
- `BeatmapSetInfo` 现新增 `ExternalLibraryRootPath` 持久化字段；BMS external scan / register 链已把 registered root path 沿 `ExternalLibraryScanner -> BmsBeatmapImporter -> BmsFolderImporter` 显式传下去并写入 beatmap set，使外部谱库分组不再依赖运行时读取当前 `ExternalLibraryConfig` 做临时最长前缀猜测。增量扫描侧也会把“同一路径但 root snapshot 缺失/不一致”的 external set 视为仍需更新。
- `BmsLibraryGroupMode` 已接通 `内部谱库` 与 `外部谱库` 的分组 authority：internal 按 `chartbms/` 下的父目录层级分组；external 以持久化 root snapshot 为第一层，再按相对父目录层级展开；无法回映到有效 root snapshot 的 legacy / missing-root set 会落入显式 `未归档外部谱库` fallback。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --configuration Release --filter "FullyQualifiedName~ExternalLibraryScannerTest"` **7/7** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsImportIntegrationTest"` **21/21** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsLibraryGroupModeTest|FullyQualifiedName~BmsTableGroupModeTest|FullyQualifiedName~BmsRulesetStatisticsTest"` **29/29** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~TestSceneBmsSongSelectDifficultyTable"` **4/4** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~TestSceneBmsSongSelectLibraryGrouping"` **3/3** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### BMS：难度表来源管理与导入反馈收口

- `BmsDifficultyTableManager` 现把“移除已导入 preset source”的语义收口为“清空来源、条目与刷新时间，并恢复隐藏的 preset placeholder”，而不是硬删 seeded preset 行；Settings → 游戏模式 → BMS → 难度表 的可见来源当前都会显示 `移除`，包括已被自动认领的 preset。
- 远端难度表下载现改为 request-level timeout + retry：首轮 20 秒，遇到瞬时 `TaskCanceledException` 或 transient HTTP 失败（如 `408/429/5xx`）时再走一轮 60 秒重试，减少 zris 这类公开表源的偶发超时直接打断导入。
- 新增 `DifficultyTableImportErrorFormatter`，把难度表导入/刷新失败统一翻译成中文分类提示；设置页与首次启动向导的难度表页都改为复用这套文案，首次启动页在一次导入多张表失败时也会直接显示前几条具体原因摘要。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsDifficultyTableManagerTest" --logger:"console;verbosity=normal"` **12/12** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### P1-A / P1-C：tri-mode pre-start overlay 合同与真实宿主绑定回归补强

- `TestSceneBmsPreStartHiSpeedOverlay` 现单独锁住 `BmsPreStartHiSpeedOverlay` 的 owner contract：mode text / value text 必须继续反映当前 tri-mode hi-speed surface，并沿 `BmsHiSpeedMode.FormatValue()` 输出；odd/even lane hi-speed adjustment 只在 overlay 可见时受理。
- `TestSceneBmsSoloPlayerPreStart` 现扩到 **8/8**：除既有 delayed-start / hold gate / target cycle / external clock suppression 外，还锁住“delay 到期但 hold 仍按住时继续可调速”以及“overlay mode/value 在真实 player flow 中反映当前 tri-mode surface”两条真实宿主链。
- 当前文档口径同步收口为 `UI_PreStartHold` 承担 hold gate、`UI_LaneCoverFocus` 保持 click-to-cycle；提前松开后的 authority 以 `SelectedHiSpeed` 是否变化为准，而不是把 routed key press 的返回值当作唯一判断。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --configuration Release --filter "FullyQualifiedName~TestSceneBmsPreStartHiSpeedOverlay"` **3/3** 通过；`dotnet test osu.Game.Rulesets.Bms.Tests --configuration Release --filter "FullyQualifiedName~TestSceneBmsSoloPlayerPreStart"` **8/8** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

## 2026-04-25

### 文档 / 代码对齐审计与恢复边界收口

- 对当前工作区、`artifacts/` 恢复快照与主线文档做了一轮对齐审计，明确后续文档基线必须以当前代码与可复核验证结果为准，而不是继续沿用 recovery 过程中残留的失真索引或过期状态页。
- `doc_md/subline/README.md` 已恢复为当前真实 `P1-A` ~ `P1-H` 子线入口索引，不再错误指向不存在的 `p1x-skin-boundary-green-number/README.md` 或把 `subline` 当成 `other` 文档入口。
- `doc_md/other/README.md` 已移除失效的 `oms_server_bridge_export.md` 入口；`doc_md/other/SKINNING.md` 内多处源码与 `SKIN/` 候选包链接已改为正确相对路径，避免继续跳到不存在位置。
- `README.md`、`DEVELOPMENT_PLAN.md` 与 `DEVELOPMENT_STATUS.md` 已同步到当前代码现实：BMS 规则集约 **167** 个源文件、BMS 测试项目 **58** 个源文件；`A-NOT` 已补回根 README 的当前状态；编译诊断口径已从过期的“0 warning / 0 error”修正为当前 `Rebuild` 下的“13 warning / 0 error”。
- 本轮还额外确认了一个容易误导后续 agent 的细节：普通增量 `dotnet build` 可能打印 `0 warning / 0 error`，但这不能当作当前真实诊断基线；主状态页现统一以 `dotnet build osu.Desktop.slnf -t:Rebuild ...` 的结果作为权威口径。
- 验证：`dotnet build osu.Desktop.slnf -t:Rebuild -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --configuration Release --filter "FullyQualifiedName~BmsStartupModPersistenceIntegrationTest|FullyQualifiedName~BmsModStatePersistenceTest|FullyQualifiedName~TestSceneBmsSoloPlayerPreStart|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics"` **111/111** 通过；`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --configuration Release --filter "FullyQualifiedName~ExternalLibraryScannerTest|FullyQualifiedName~TestSceneFirstRunSetupOverlay|FullyQualifiedName~TestSceneFirstRunScreenImportFromStable|FullyQualifiedName~TestSettingsMigration"` **18/18** 通过；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --configuration Release --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin"` **92/92** 通过。

## 2026-04-24

### 文档主线：第二轮全面复验与验证基线同步

- 按 `DEVELOPMENT_STATUS.md` 当前主线声明重跑权威切片：BMS 全量 **706/706**、mania OMS skin gate **92/92**、BMS user-skin fallback **105/105**、scratch bridge **43/43**、`osu.Game.Tests` 文档 gate **23/23**，并再次确认 `osu.Desktop` Release 构建通过。
- `TestSettingsMigration` 现已移除对不存在的 `DisplayStarsMaximum -> 10.1` 自动迁移假设，改为锁定当前实际合同：旧配置值保持不变，且用户重新保存后的值会跨重启继续保留。
- `DEVELOPMENT_STATUS.md` 与 `DEVELOPMENT_PLAN.md` 已同步到当前已验证基线，不再继续沿用过期的 BMS **608/608** / BMS fallback **92/92** 快照。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj` **706/706** 通过；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin"` **92/92** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter "FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics"` **105/105** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter "FullyQualifiedName~TestSceneOmsScratchGameplayBridge"` **43/43** 通过；`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --filter "FullyQualifiedName~ExternalLibraryScannerTest|FullyQualifiedName~TestSceneFirstRunScreenBehaviour|FullyQualifiedName~TestSceneFirstRunSetupOverlay|FullyQualifiedName~TestSceneFirstRunScreenImportFromStable|FullyQualifiedName~TestSceneStartupSkinMigration|FullyQualifiedName~TestSceneEditDefaultSkin|FullyQualifiedName~TestSettingsMigration" --configuration Release` **23/23** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### 最终全面查验收尾

- mania 最后一轮残留已全部收口：`TestSceneObjectPlacement` 改为锚定当前 `EditorRadioButton` / `HitObjectCompositionToolButton` 工具按钮；`TestSceneManiaModHidden` / `TestSceneManiaModFadeIn` 现按当前 gameplay scaling 合同断言 coverage；`TestSceneManiaTouchInput` 现按真实列边界而非过期固定 gap 坐标取点。
- 本轮确认 `osu.Game.Rulesets.Mania.Tests` 全量 **761/761** 通过，说明最终收尾后 mania 已恢复到当前仓库合同下的完整测试绿线，而不是仅停留在 OMS skin gate **92/92**。
- 验证：`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --filter "FullyQualifiedName~TestSceneManiaTouchInput|FullyQualifiedName~TestSceneObjectPlacement"` **12/12** 通过；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --filter "FullyQualifiedName~TestSceneManiaModHidden.TestMaxCoverageFullWidth|FullyQualifiedName~TestSceneManiaModHidden.TestMaxCoverageHalfWidth|FullyQualifiedName~TestSceneManiaModHidden.TestMinCoverageHalfWidth|FullyQualifiedName~TestSceneManiaModFadeIn.TestMaxCoverageFullWidth|FullyQualifiedName~TestSceneManiaModFadeIn.TestMaxCoverageHalfWidth"` **5/5** 通过；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj` **761/761** 通过。

## 2026-04-23

### P1-A：首次启动向导重构为 OMS 六步流程

- `FirstRunSetupOverlay` 现已固定为六步：欢迎、UI 缩放、获取谱面、导入、难度表设置、按键绑定；不再保留旧的 stable import 条件分支与旧 behaviour page 文案 / 结构。
- `ScreenBeatmaps` / `ScreenImportFromStable` / `ScreenBehaviour` / `ScreenKeyBindings` 已分别收口为 OMS onboarding surface：获取谱面页改为 mania / BMS 站点导流和内部谱库补扫提示；导入页直接嵌入 `ExternalLibrarySettings`；难度表页按分组导入 zris 镜像预设，并通过反射调用 `BmsDifficultyTableManager` 保持 `osu.Game` 与 `osu.Game.Rulesets.Bms` 的项目边界；最后一步复用全局、mania 与 BMS 的 keybinding subsection。
- 手动重新打开首次启动向导并进入旧“游戏表现”页导致的 blank panel / unhandled error 已修复；`SkinSection` 里的 skin dropdown disabled-state 现改到 `LoadComplete()` 执行。
- 欢迎页、获取谱面页与导入页的可见文案现已切到 OMS-owned localisation namespace + `.resx`，解决简中界面继续显示上游翻译的问题；本次归线维持既有 `P1-A`，导入页复用外部谱库设置仅作为 `P1-H` 从属暴露，不新开子线。
- 验证：`dotnet test osu.Game.Tests --filter "FullyQualifiedName~TestSceneFirstRunScreenBehaviour|FullyQualifiedName~TestSceneFirstRunSetupOverlay|FullyQualifiedName~TestSceneFirstRunScreenImportFromStable" --configuration Release` **11/11** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### BMS：进入选歌与切换分组时停留在根分组

- `SongSelect` 现已把“进入 BMS 选歌 fresh entry / 切换任意 BMS 分组”统一收口为 ruleset-driven root reset：共享层新增 `Ruleset.ShouldResetSongSelectGroupToRoot()` 扩展点，仅由 BMS 打开；mania 与其他 ruleset 继续沿用原有行为。
- `BeatmapCarousel` 现会在 root-level 状态下保留当前歌曲的全局 beatmap 选择，同时把该歌曲对应的最外层 `GroupDefinition` 设为 keyboard-selected 项。这样进入 BMS 或切组后，界面表现为“停在最外层分组，但已选中当前歌曲所属外层组”，不会错误回到 leaf 谱面展开态。
- 新增 / 更新 BMS 回归覆盖：`BmsRulesetStatisticsTest` 锁定 BMS 分组的 root-reset contract，`TestSceneBmsSongSelectDifficultyTable` 锁定 fresh entry 与切换到 `难度表` / `标题` 分组时均保持 root-level，并正确高亮目标外层分组。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter "FullyQualifiedName~BmsRulesetStatisticsTest|FullyQualifiedName~TestSceneBmsSongSelectDifficultyTable"` **26/26** 通过。

### BMS：选歌分组收窄并默认切到难度表

- BMS 专属 Song Select 分组下拉现已改为 ruleset-specific 显式列表：移除 `未分组`，并移除 `本地收藏`、`导入时间`、`上架时间`、`官网收藏`、`我做的谱面`、`谱面状态`、`来源` 这些不需要的上游分组；mania 继续沿用默认共享列表，不受影响。
- `Difficulty Table` 分组标签现改用 OMS-owned 本地化资源，在中文界面显示为 `难度表`。
- 由于 BMS 分组列表首项现为 `DifficultyTable`，而 song select group fallback 也已改为“当前 ruleset 的第一个可用项”，BMS 进入选曲时默认分组现会安全落到 `难度表`，不再回退到 `未分组`。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter "FullyQualifiedName~BmsRulesetStatisticsTest"` **21/21** 通过。

### BMS：选歌排序标签语义纠正

- BMS 专属 Song Select 排序下拉中，原先回落为 `Clear Lamp` 与误复用通用 `Accuracy` 语义的两个本地成绩排序项，现已明确改为 `点灯状态` 与 `达成率`；这次修正只影响 BMS 的显示语义，不改变既有排序逻辑，也不影响 mania。
- 显示层现改用 OMS-owned `OmsSongSelect` 本地化资源承载这两个标签，避免继续复用上游 `SongSelectStrings.Accuracy` 导致中文界面出现 `准度要求`，也避免缺失翻译时回退到英文 `Clear Lamp`。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter "FullyQualifiedName~BmsRulesetStatisticsTest"` **20/20** 通过。

### P1-H：谱库扫描拓扑扩展为外部/内部 + 重建/增量四模式

- Settings -> Maintenance 现已把谱库扫描拆成四个显式入口：`扫描外部谱库（重建）`、`扫描外部谱库（增量）`、`扫描内部谱库（重建）`、`扫描内部谱库（增量）`；其中内部两项已从原 `外部谱库` subsection 迁移到新的 `内部谱库` subsection，完成语义隔离。
- `ExternalLibraryScanner` 与 `ManagedLibraryScanner` 现新增 `ScanMode`（`Rebuild` / `Incremental`）与按目录判断“是否仍需导入”的回调；`OsuGameDesktop` 会把该判定下推到 BMS / mania importer。`增量` 模式只会处理当前没有 active `FilesystemStoragePath` 记录的目录，`重建` 模式则继续重走全部候选目录并允许重新注册/刷新索引。
- 新增 `InternalLibrarySettings`，`ExternalLibrarySettings` 现只保留外部根管理与外部两种扫描按钮；桌面端 Settings -> Maintenance 拓扑已从“一个 subsection 混放外部/内部扫描”改为“外部谱库 / 内部谱库”双 subsection。
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --filter "FullyQualifiedName~ExternalLibraryScannerTest"` **6/6** 通过；`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

### P1-H：内部谱库扫描 managed-root 判定修复

- Settings -> Maintenance 的谱库扫描口径现已明确分为两条链：`扫描外部谱库` 只针对已注册的外部根目录；`扫描内部谱库` 只负责重建当前数据根下 `chartbms/` 与 `chartmania/` 的 managed roots 索引，适用于用户手动复制、解压或移动谱面目录后的补扫。
- 修复 `FilesystemSanityCheckHelpers.IsSubDirectory()` 在比较“带尾部分隔符的 managed root”与“不带尾部分隔符的子目录父路径”时出现的 false negative；当前会先用 `Path.TrimEndingDirectorySeparator()` 规范化两侧，再做同目录/父目录链判断。
- 该修复使 `BmsFolderImporter.RegisterManagedDirectory()` 与 `ManiaFolderImporter.RegisterManagedDirectory()` 不再对合法的 `chartbms/...` / `chartmania/...` 目录误报“不在 managed root 下”；并新增 `FilesystemSanityCheckHelpersTest`，锁定“child-under-parent”和“same-directory”两条 trailing-separator 回归。
- 验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter "FullyQualifiedName~FilesystemSanityCheckHelpersTest"` **2/2** 通过。

## 2026-04-22

### BMS：冷启动 mod 恢复与 startup ruleset 时序修复

- `OsuGameBase` 现不再把 `RulesetConfigCache` 尚未 `LoadComplete()` 的 startup path 当作 ruleset failure；当 cache 仍未 ready 时，BMS mod persistence 会先跳过 config-backed restore，并在 cache ready 后排队重放当前 ruleset，补做 `PersistedModState` 恢复。
- 该修复同时消除了启动期误报的 `BMS` / `osu!mania` ruleset issue 通知，以及冷启动首轮进入游戏时 BMS mod 选中状态和 remembered settings 丢失的问题。
- 新增 `BmsStartupModPersistenceIntegrationTest`：先 seed `PersistedModState`，再以第二个同名 host 冷启动 `OsuGameBase`，断言 `BmsModSudden` 的选中状态、cover 参数与 `RememberGameplayChanges` 都被恢复。
- 验证：`dotnet build .\osu.Desktop\osu.Desktop.csproj -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过；`dotnet run --project .\osu.Desktop\osu.Desktop.csproj -c Release` 进入 MainMenu，最新 runtime log 不再出现 `An issue with ruleset` / `Failed to revert from ruleset` / `Cannot retrieve IRulesetConfigManager`；`BmsStartupModPersistenceIntegrationTest` + `BmsModStatePersistenceTest` 合计 **4/4** 通过；手测确认完全关闭重启后不再弹三条通知，BMS mod 冷启动 / 运行中关开 / 切 mania 往返都能正确恢复。

## 2026-04-21

### BMS：mod 选项与配置持久化

- `osu.Game` 现已新增 ruleset 级 `IRulesetModStatePersistence` 扩展点，BMS 通过 `BmsModStatePersistence` 把当前选中的 mod 顺序与 remembered settings 写入 `BmsRulesetSetting.PersistedModState` JSON；完全关闭重开，或从 BMS 切到 mania 再切回 BMS 时都可恢复，且不影响 mania。
- 可配置 BMS mod 现通过 `IPreserveSettingsWhenDisabled` 保留停用前最后配置，解决 `ModSelectOverlay` 在 deselect 时无条件 reset 默认值的问题；`Auto Scratch` / `Auto Note` / `Random` / `Gauge Auto Shift` / `Judge Rank` / `Sudden` / `Hidden` / `Lift` 等 mod 现在手动关掉再开仍会带回上次配置。
- `Sudden`、`Hidden`、`Lift` 现新增 `Remember gameplay changes` / `记忆游戏内变动` 开关，默认开启；开启时局内滚轮调整会回写当前 BMS selected mods 与持久化快照，关闭时则保持 current-play-only 语义。
- 验证：`BmsRulesetConfigurationTest`、`BmsModStatePersistenceTest`、`BmsRulesetModTest` 合计 **56/56** 通过；独立输出目录 `Release` 构建通过。

### BMS：新增 `Auto Note` assist mod

- `osu.Game.Rulesets.Bms` 现已新增 `BmsModAutoNote`，与现有 `BmsModAutoScratch` 对称：会自动处理非 scratch note，并把对应对象从判定 / 计分 / gauge 语义中剔除。
- `BmsModAutoNote` 现提供独立的 `Note visibility`、`Tint notes` 与 `Note tint colour` 配置面；当前与 `Auto Scratch` 互斥，且二者都继续与 `BmsModAutoplay` 互斥。
- 定向 `BmsRulesetModTest`、`BmsGaugeProcessorTest`、`BmsScoreProcessorTest`、`BmsDrawableRulesetTest` 合计 **208/208** 通过；`Build osu! (Release)` 通过。

### P1-A：BMS `Playfield Style` 替换 `Playfield Horizontal Offset`

- `BmsSettingsSubsection` 已移除数值型 `游玩区域水平偏移`，`BmsRulesetConfigManager` 改为声明四态 `Playfield Style`：`1P（居左）`、`2P（居右）`、`居中（左皿）`、`居中（右皿）`。
- 当前基础实现只作用于 single-play 5K / 7K：`1P（居左）` 与 `2P（居右）` 现在都属于“侧停靠但保留固定屏侧间距”的样式，scratch 视觉分别留在最左 / 最右；两种 `居中` 都保持 playfield 居中，仅改变 scratch 视觉在左还是右。9K 固定居中，14K 保持固定双侧布局；这不是完整 `1P/2P flip`，不会翻转 bindings 或 side-aware skin/HUD/BGA 合同。
- `BmsRulesetConfigurationTest`、`BmsPlayfieldAdjustmentContainerTest`、`BmsLaneLayoutTest`、`TestSceneBmsPlayfieldLayoutConfig`、`BmsDrawableRulesetTest`、`BmsScrollSpeedMetricsTest` 合计 **92/92** 通过；`Build osu! (Release)` 通过。

### P1-A / P1-C：BMS `Playfield Scale` 残余 surface 移除

- `BmsSettingsSubsection` 已移除 `游玩区域缩放`，`BmsRulesetConfigManager` 也不再声明 `PlayfieldScale`；旧值不会再参与当前 BMS runtime contract。
- `BmsPlayfieldAdjustmentContainer` 现固定为 identity transform，不再承接用户侧缩放或数值型横向偏移；这样非权威几何缩放不会再混入当前 visual-speed surface。
- `BmsPlayfieldAdjustmentContainerTest` 与 `BmsRulesetConfigurationTest` 已改为锁定“unit scale + style-based single-play layout”合同；定向 `BmsRulesetConfigurationTest`、`BmsPlayfieldAdjustmentContainerTest`、`BmsLaneLayoutTest`、`TestSceneBmsPlayfieldLayoutConfig`、`BmsDrawableRulesetTest`、`BmsScrollSpeedMetricsTest` 合计 **90/90** 通过；`Build osu! (Release)` 通过。

## 2026-04-20

### P1-A / P1-C：pre-start 稳定性修复与 `UI_LaneCoverFocus` / `UI_PreStartHold` 语义拆分

- 修复 `BmsSoloPlayer` pre-start delayed start 引起的 clock failure：在 `BmsSoloPlayer.StartGameplay()` 开头调用 `GameplayClockContainer.Reset(startClock:false)` 强制停止从选曲页残留的 decoupled clock，并新增 `GameplayClockContainer.SoftUnpause()` 使 `isPaused=false` 但不启动底层时钟，让 `FrameStabilityContainer` 在 pre-start 期间仍能处理子组件（修复 playfield 不渲染的问题）。
- 拆分 `UI_PreStartHold`（按住阻塞开谱并弹出 pre-start overlay）与 `UI_LaneCoverFocus`（单击循环 `Sudden / Hidden / Lift` 持久目标）为独立键位。新增 `BmsAction.PreStartHold` 枚举值、`OmsBmsActionMap` 全变体映射、`BmsInputStrings.PreStartHold` 本地化字符串，让 `UI_PreStartHold` 在设置面板可见。
- `UI_LaneCoverFocus` 语义从 hold-to-temporarily-switch-to-Hidden 改为 click-to-cycle：按下时触发 `CycleGameplayAdjustmentTarget()` 在 `Sudden → Hidden → Lift` 之间循环，松开后不再恢复。修复了启用多个 mod 时无法切换到 Lift 的问题。
- `DrawableBmsRuleset.canAdjustGameplaySettings` 新增 `FrameStableClock?.IsRunning ?? true` 检查，防止 pre-start 期间（IsPaused=false 但 IsRunning=false）无 hold 键时意外调节。
- 默认键位：5K/7K/9K `UI_PreStartHold` = Q、`UI_LaneCoverFocus` = W；14K `UI_PreStartHold` = T、`UI_LaneCoverFocus` = Y。
- 验证：`TestSceneBmsSoloPlayerPreStart` **6/6** 通过；`BmsRulesetModTest` **40/40** 通过；`Build osu! (Release)` 通过。

### P1-A / P1-C：tri-mode Hi-Speed surface 与 pre-start hold 调速窗口落地

- `osu.Game.Rulesets.Bms` 已新增 `BmsHiSpeedMode` 与 `BmsHiSpeedRuntimeCalculator`；设置页现可在 `Normal / Floating / Classic Hi-Speed` 三种模式间切换，并只显示当前模式数值，不再把 `GN / ms` 写进 settings。
- `DrawableBmsRuleset` 现已按模式发布 mode-aware `BmsScrollSpeedMetrics`、HUD detail line 与 OSD toast，其中 `Classic` 继续锁定官方 sample `HS 10 + WN 350 => GN 300`，`Floating` 首轮按 initial BPM 锚定 visual speed，但仍不宣称完整 mid-song re-float parity。
- BMS song select 进入游玩后现有 5 秒 delayed start；按住 `UI_PreStartHold` 会阻塞开谱并显示 pre-start overlay，期间可按键位奇数列加速、偶数列减速，且 `UI_LaneCoverFocus` / 滚轮 / 中键仍可继续调节 `Sudden / Hidden / Lift` 与目标切换。
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~BmsRulesetConfigurationTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~BmsDrawableRulesetTest"` **97/97** 通过；`Build osu! (Release)` 通过。

### P1-A / P1-C：strict Classic Hi-Speed + frozen geometry surface 落地

- `osu.Game.Rulesets.Bms` 已把 Classic Hi-Speed 的 base time 从上游 mania 的 `11485 / HS` 改为官方 sample 对齐的 `(100000 / 13) / HS`，并由 `BmsScrollSpeedMetricsTest` 锁定 `HS 10 + WN 350 => GN 300`
- `BmsPlayfield` 不再在运行时消费 playfield / receptor / bar-line 的 layout override，`BmsSettingsSubsection` 也已移除 geometry sliders；内部 `BmsPlayfieldLayoutProfile` abstraction 仍保留给 ruleset / skin 侧使用
- 当前公开 `Classic Hi-Speed` 范围仍保持 `1.0 - 20.0`，但这次已不只是范围收口，而是把 strict Classic surface 一并锁定
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~BmsRulesetConfigurationTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~TestSceneBmsPlayfieldLayoutConfig|FullyQualifiedName~BmsLaneLayoutTest|FullyQualifiedName~BmsDrawableRulesetTest"` **91/91** 通过；`Build osu! (Release)` 通过

### P1-A / P1-C：live `PERFECT / FC / FC LOST` 资格线入同一 feedback card

- `osu.Game.Rulesets.Bms` 已为 `BmsJudgementCounts` 新增 live eligibility helper，并进一步补入最轻 break bucket 派生语义，`DefaultBmsSpeedFeedbackDisplay` 现可直接从既有 counts 派生带紧凑原因标签的 live `PERFECT / FC / FC LOST` 状态线
- 本次没有继续扩大 `BmsGameplayFeedbackState`；它确认了这类 display-only 的 judge feedback 可以复用现有 aggregate snapshot，而不必新增 runtime state 发布面
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsExScoreProgressInfoTest|FullyQualifiedName~BmsExScorePacemakerInfoTest|FullyQualifiedName~BmsJudgementCountsTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **69/69** 通过；`Build osu! (Debug)` 通过

### P1-A / P1-C：live EX progress 并入 aggregate gameplay feedback snapshot

- `osu.Game.Rulesets.Bms` 已新增 `BmsExScoreProgressInfo`，把当前 `EX-SCORE / MAX EX-SCORE` 快照为轻量值对象，并并入 `BmsGameplayFeedbackState`
- `DefaultBmsSpeedFeedbackDisplay` 现会在同一张 feedback card 中显示 live `DJ LEVEL + EX 原始分子/分母 + %`，与既有最近判定、timing sparkline、compact judgement summary 和 fixed AAA EX pacemaker 共用同一条反馈容器
- `BmsGameplayFeedbackState` 现已继续把 live EX progress 一并收口到 aggregate snapshot，而 recent history 仍保持独立列表态
- 验证：后续沿同一 feedback family 的聚焦回归已升至 `dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsExScoreProgressInfoTest|FullyQualifiedName~BmsExScorePacemakerInfoTest|FullyQualifiedName~BmsJudgementCountsTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **69/69** 通过；`Build osu! (Debug)` 通过

### P1-A / P1-C：compact live judgement summary 并入 aggregate gameplay feedback snapshot

- `osu.Game.Rulesets.Bms` 已新增 `BmsJudgementCounts`，把 live score statistics 快照为轻量值对象，并并入 `BmsGameplayFeedbackState`
- `DefaultBmsSpeedFeedbackDisplay` 现会在同一张 feedback card 中显示两行 compact live judgement summary：`PGR / GR / GD` 与 `BD / PR / EP`
- `BmsGameplayFeedbackState` 现已继续把 judgement counts 一并收口到 aggregate snapshot，而 recent history 仍保持独立列表态
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsJudgementCountsTest|FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **59/59** 通过；`Build osu! (Debug)` 通过

### P1-A / P1-C：aggregate gameplay feedback state contract 第二刀

- `BmsGameplayFeedbackState` 现已额外包含 `TimingFeedbackVisualRange`，让 compact timing sparkline 的 scalar 输入也并入同一条 aggregate snapshot
- `DefaultBmsSpeedFeedbackDisplay` 现已收口为消费 `GameplayFeedbackState` 加 `RecentJudgementFeedbacks` 列表，不再额外直接绑定 `TimingFeedbackVisualRange` scalar
- 新增 `BmsGameplayFeedbackStateTest`，并扩展 `BmsRulesetModTest`、`TestSceneBmsSpeedFeedbackDisplay`、`BmsSkinTransformerTest`，锁定 snapshot 值语义、ruleset 镜像与 sparkline/expiry 行为
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsGameplayFeedbackStateTest|FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~BmsSkinTransformerTest"` **153/153** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### P1-A / P1-C：aggregate gameplay feedback state contract 首刀

- `osu.Game.Rulesets.Bms` 已新增 `BmsGameplayFeedbackState`，把 speed metrics、target-state、最近判定与 fixed AAA pacemaker 这批 scalar gameplay feedback 收口为单个 snapshot
- `DrawableBmsRuleset` 现额外暴露 `GameplayFeedbackState`；`DefaultBmsSpeedFeedbackDisplay` 已改为优先消费该 aggregate state，而不是继续分别绑定多组 ruleset scalar bindable
- recent timing history 与 visual range 仍保持独立状态流，避免把列表态与瞬时标量语义硬塞进同一个 snapshot
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsGameplayFeedbackLayoutTest|FullyQualifiedName~TestSceneBmsJudgementDisplayPosition"` **154/154** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### P1-C：fixed AAA EX pacemaker 入同一 feedback card

- `DrawableBmsRuleset` 已新增 `ExScorePacemakerInfo`，把 fixed AAA 目标的 EX pacemaker 状态暴露给 HUD
- `DefaultBmsSpeedFeedbackDisplay` 现会在同一张 feedback card 中显示 `PAC AAA +/-n` 文案，且差值按当前已判对象的目标节奏推进，而不是从开局起显示整局最终目标缺口
- 新增 `BmsExScorePacemakerInfoTest`，并扩展 `TestSceneBmsSpeedFeedbackDisplay` 锁定 pacemaker 计算与文案 / 配色回归
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsExScorePacemakerInfoTest|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay|FullyQualifiedName~BmsRulesetModTest"` **52/52** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### P1-C：compact visual timing-offset 入同一 feedback card

- `DrawableBmsRuleset` 已新增 `RecentJudgementFeedbacks` 与 `TimingFeedbackVisualRange`，把 recent timing history 与当前局 visual range 暴露给 HUD
- `DefaultBmsSpeedFeedbackDisplay` 现会在同一张 feedback card 中显示 compact visual timing-offset sparkline，并只吸收有 timing 语义的 recent basic judgement
- `BmsRulesetModTest` 与 `TestSceneBmsSpeedFeedbackDisplay` 已补 runtime / visual 回归，锁定 recent history 过滤与 sparkline 渲染
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **158/158** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### P1-C：最近判定 feedback 改为瞬时 judge display

- `DefaultBmsSpeedFeedbackDisplay` 里的最近判定 feedback 不再永久停留，而是按短时 judge display 语义自动消隐
- 相同判定与相同 `FAST/SLOW` 偏移再次出现时，显示窗口会被刷新，而不是沿用旧的过期时钟
- `TestSceneBmsSpeedFeedbackDisplay` 已补“过期消隐”和“同值刷新续时”回归，并改用 `display.Time.Current` 对齐组件自己的时钟
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **157/157** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### P1-C：最近判定与 `FAST/SLOW` 入同一 feedback container

- `DrawableBmsRuleset` 已新增 `LatestJudgementFeedback`，并用 `BmsJudgementTimingFeedback` 把 `JudgementResult` 快照成 HUD 可消费的轻量状态
- `DefaultBmsSpeedFeedbackDisplay` 现会在同一 feedback container 中显示最近判定与 `FAST/SLOW` timing 文案，例如 `PGREAT | FAST 3.2ms`
- `EPOOR` 这类无真实 timing 语义的结果只显示判定名，不再硬附 `FAST/SLOW` 后缀
- `BmsRulesetModTest` 与 `TestSceneBmsSpeedFeedbackDisplay` 已补 runtime / visual 回归
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **155/155** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### P1-C：speed feedback HUD 显式区分 `HOLD` 临时覆写态

- `DrawableBmsRuleset` 已新增 `IsAdjustmentTargetTemporarilyOverridden`，把当前显示 target 是否为临时覆写暴露给 HUD
- `DefaultBmsSpeedFeedbackDisplay` 在按住 `UI_LaneCoverFocus` 导致的临时覆写场景下，现会显示 `HID HOLD` 这类显式文案，而不是继续沿用普通 cycle 文案
- `BmsRulesetModTest` 与 `TestSceneBmsSpeedFeedbackDisplay` 已补运行时与视觉回归
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **152/152** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### P1-C：恢复 `UI_LaneCoverFocus` 的 Hidden 临时覆写

- `DrawableBmsRuleset` 已恢复 `UI_LaneCoverFocus` 的按住型语义：按住时滚轮会临时转向 `Hidden`，松开后回到持久 target
- target cycle 入口已明确收口到鼠标中键点击，不再复用 lane cover focus 信号
- `BmsRulesetModTest` 已补“临时覆写不会改写持久 target，释放后回退”回归
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **151/151** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### P1-C：speed feedback 多 target cycle 序号入 HUD

- `DrawableBmsRuleset` 已新增 `ActiveAdjustmentTargetIndex`，把当前 target 在 `Sudden / Hidden / Lift` 可切换序列中的位置暴露给 HUD
- `DefaultBmsSpeedFeedbackDisplay` 在多 target 状态下现会显示显式序号，例如 `SUD 1/3`、`HID 2/3`，不再只显示 target 简写
- `BmsRulesetModTest` 与 `TestSceneBmsSpeedFeedbackDisplay` 已补 index 回归，锁定无 target、单 target、三 target cycle 的运行时与显示语义
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **150/150** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### P1-C：speed feedback target-state 首轮收口

- `DrawableBmsRuleset` 已新增 `EnabledAdjustmentTargetCount`，把 runtime 中可用的 `Sudden / Hidden / Lift` 调节目标数量暴露给 HUD
- `DefaultBmsSpeedFeedbackDisplay` 现在会按 target 可用性区分 `NONE`、`{TARGET} ONLY` 与多 target 可切换三种状态，不再只显示当前 active target
- `BmsRulesetModTest` 与 `TestSceneBmsSpeedFeedbackDisplay` 已补 target-state 回归，锁定无 target / 单 target / 多 target 的产品语义
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsRulesetModTest|FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **149/149** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### P1-C：BMS 常驻 speed feedback HUD 首轮实现

- `osu.Game.Rulesets.Bms` 已新增公共 `BmsGameplayAdjustmentTarget`，并把 `DrawableBmsRuleset` 的 runtime 速度反馈状态提升为可绑定的 `SpeedMetrics` / `ActiveAdjustmentTarget`
- `BmsScrollSpeedMetrics` 已补 `IEquatable<>`；`BmsSkinComponents` 新增 `SpeedFeedback`；`DefaultBmsSpeedFeedbackDisplay` 已以 `IBmsSpeedFeedbackDisplay` 形式挂入 BMS HUD，显示 `GN + 可见毫秒 + HS + 当前目标`
- HUD 集成采用向后兼容策略：新增 `IBmsHudLayoutDisplayWithGameplayFeedback` 供新 layout 显式接入 speed feedback，旧 layout 则由 transformer 自动包 overlay 容器，不直接破坏既有皮肤接口
- 新增 `TestSceneBmsSpeedFeedbackDisplay`，并扩展 `BmsSkinTransformerTest` / `BmsScrollSpeedMetricsTest` / `TestSceneBmsUserSkinFallbackSemantics`，锁定 speed feedback 文案、警告态、fallback 与 legacy HUD 兼容语义
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests --filter "FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~BmsScrollSpeedMetricsTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics|FullyQualifiedName~TestSceneBmsSpeedFeedbackDisplay"` **113/113** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### 文档重构：根目录 Markdown 收口到 doc_md，并补齐主线 / 主子线 / 参考线索引

- 根目录除 README 外的现有 Markdown 文档现已统一迁入 `doc_md/`，并按 `mainline / subline / other / mini` 四层分类；仓库根 README 继续保留为项目入口，`Templates/README.md` 继续保留为模板说明
- 新增 `doc_md/README.md` 与各层级 README 索引，后续文档导航统一从文档总索引进入，不再依赖根目录平铺查找
- 本轮同步修正了 README 与各 Markdown 文档的相对链接，避免移动后出现断链

### P1-A / P1-C：皮肤设计边界与绿色数字 / Mod 联动专题建档

- 旧的自由命名专题已拆分并正式挂到 `doc_md/subline/P1-A/` 与 `doc_md/subline/P1-C/`；`P1-A` 主承接皮肤边界、HUD 宿主与 release gate，`P1-C` 主承接绿色数字、速度反馈、判定语义与训练反馈闭环
- 主线文档已挂接这两条正式子线：`DEVELOPMENT_PLAN.md` 现把这条工作归线为 `P1-A / P1-C` 交叉主子线，`DEVELOPMENT_STATUS.md` 记录当前已完成设计审计且常驻 GN HUD / FAST-SLOW / judge display 仍未开始代码实现，`OMS_COPILOT.md` 补上了“不得直接破坏现有 HUD 布局接口、不得把当前 GN 直接包装成完整 FHS”的硬约束
- 本轮仅进行文档重构与规划建档，未新增代码构建或测试执行

## 2026-04-19

### BMS：lane cover 语义纠正为 Sudden/Hidden，新增独立 Lift，并将运行时速度反馈切到 GN 主表达

- `BmsScrollSpeedMetrics` 现已扩展为 ruleset-owned runtime 指标入口：除基础时长与可见时长外，还暴露 `SuddenUnits` / `HiddenUnits` / `LiftUnits` / `WhiteNumber` / `GreenNumber`；`DrawableBmsRuleset` 的调速 OSD 已改为 `GN xxx (yyyms)` 主表达，`BmsSettingsSubsection` 的设置文案改为 `Classic Hi-Speed`
- 进一步补齐游玩内调节链：滚轮现在会直接调当前启用的 `Sudden / Hidden / Lift` 目标，默认按 `Sudden -> Hidden -> Lift` 的顺序选择；鼠标中键会只在 2 个及以上已启用项时拦截并循环切换目标，原有 `UI_LaneCoverFocus` 仍保留为 `Hidden` 的临时覆写
- 为避免 gameplay 内继续弹出“基础 ms”这种过时反馈，`BmsRulesetConfigManager` 已停止把 scroll speed 作为 tracked setting 暴露给通用 OSD；本轮配套更新了 mod 测试、lane cover scene、skin fallback 测试、playfield layout 测试，以及 `DEVELOPMENT_PLAN.md` / `OMS_COPILOT.md` 的实现说明
- 验证：`dotnet build osu.Game.Rulesets.Bms\osu.Game.Rulesets.Bms.csproj -c Release /v:m` 通过；`dotnet build osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj -c Release /v:m` 通过；定向 `BmsRulesetModTest`、`BmsScrollSpeedMetricsTest`、`TestSceneBmsPlayfieldLayoutConfig` 合计 **53/53** 通过

### 文档、仓库记忆与上游差异文档联动校准

- `README.md`、`DEVELOPMENT_PLAN.md`、`DEVELOPMENT_STATUS.md`、`RELEASE.md`、`SKINNING.md`、`UPSTREAM.md` 与仓库摘要记忆已按 2026-04-19 代码状态同步：README 的皮肤现状不再停留在早期“mania 内置简约白蓝黄”描述；皮肤手册里的 mania authoring 风险也改为反映当前 shell / preset 接线与 8 类 OMS-owned 组件已落地、但 release-facing contract 仍未冻结的真实状态
- 规模口径现与“历史测试快照”拆开表达：按 2026-04-19 本地文件计数（排除 `bin/obj`），`osu.Game.Rulesets.Bms` 约 **146** 个源文件、`oms.Input` **15** 个、`osu.Game.Rulesets.Bms.Tests` **49** 个测试源文件；最近一次完整项目级自动化回归仍沿用 2026-04-17 的 **608/608** 已验证快照
- `UPSTREAM.md` 已从过时的少量文件清单改为当前可操作的本地 diff 基线：保留上游 tag commit `bb289363a2b8e6bf62be355f8570def018f0d7be` 作为语义锁定点，同时明确当前仓库本地应以 bootstrap commit `0b97bbdd4348de47e1d597a65f0a7734ad184000` 与 `HEAD` 比较；2026-04-19 本地审计下 `osu.Game/` 共 **147** 个变更路径（**113 M / 30 A / 4 D**），高风险目录集中在 `Screens`、`Beatmaps`、`Localisation`、`Overlays`、`Rulesets` 与 `Skinning`
- 本轮仅做文档与记忆同步，未新增自动化测试执行；最近一次已验证 gates 仍为 BMS **608/608**、mania OMS **92/92**、BMS fallback **92/92**、scratch bridge **43/43**、`osu.Game.Tests` release-gate **6/6**

## 2026-04-17

### 在线提交边界：保留 ruleset_data，避免未来 leaderboard 混算 BMS 语义

- `SoloScoreInfo` 现已显式序列化 `ruleset_data`，并在 `ToScoreInfo()` 回填到 `ScoreInfo.RulesetDataJson`；这样将来启用私服/在线排行榜时，BMS 的 `long_note_mode`、judge/gauge 等 ruleset-specific payload 不会在通用 score submission 通道里丢失
- 现有 `SubmitScoreRequest` / `SubmitSoloScoreRequest` 无需改调用面，`SoloScoreInfo.ForSubmission(score)` 已会自动携带本地 score 的 `RulesetDataJson`
- 新增在线序列化回归：`TestSoloScoreInfoJsonSerialization` 现锁定 `ruleset_data` 的输出与 round-trip 恢复，避免未来重构把 BMS 的 LN/CN/HCN、judge、gauge 约束从在线载荷中意外删掉
- 验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --no-restore -v minimal --filter "FullyQualifiedName~TestSoloScoreInfoJsonSerialization|FullyQualifiedName~TestAPIModJsonSerialization"` **5/5** 通过

### 文档与仓库记忆：按当前代码基线重整

- `README.md`、`DEVELOPMENT_STATUS.md`、`DEVELOPMENT_PLAN.md` 与 `IIDX_REFERENCE_AUDIT.md` 已同步到当前代码状态：四套 judge mode、Mirror / Random、A-SCR / BMS Autoplay、BMS replay 录制/回放/归档、`chartbms/` / `chartmania/` 存储命名，以及外部谱库维护 UI
- 当前规模快照已刷新为：`osu.Game.Rulesets.Bms` 147 个源文件、`oms.Input` 15 个源文件、`osu.Game.Rulesets.Bms.Tests` 46 个测试文件
- 验证：`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal` **608/608** 通过

### BMS：新增 Mirror / Random 训练向 lane rearrangement mod

- `osu.Game.Rulesets.Bms` 现已新增 `BmsModMirror` 与 `BmsModRandom`，并统一暴露在 `Conversion`
- `BmsModRandom` 当前支持 `RANDOM`、`R-RANDOM`、`S-RANDOM` 三种模式，内置 `Seed` 与手动 `Custom pattern` 配置；14K 下单组 pattern 可自动复制到双侧
- runtime beatmap mod 统一入口 `BmsBeatmapModApplicator` 现已先应用 `Mirror` / `Random`，再继续 long-note mode、judge mode 与 `Auto Scratch`
- 验证：`Build osu! (Debug)` 通过；`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore -v q --results-directory TestResults --logger "trx;LogFileName=bms-mirror-random.trx"` 结果为 **583/583** 通过

### BMS：新增 Auto Scratch 与 BMS Autoplay assist mod

- `osu.Game.Rulesets.Bms` 现已新增 `BmsModAutoScratch` 与 `BmsModAutoplay`，并统一暴露在 `DifficultyReduction`
- A-SCR 当前会把 scratch runtime 语义切换为 `AutoPlay = true` + 不参与判定 / 计分 / gauge / MaxExScore；mod 内支持可见性、染色开关与染色颜色配置
- BMS autoplay 当前已补齐专用 replay frame / replay input handler / replay recorder / auto generator，并已接入 ruleset / drawable ruleset / score processor
- 验证：初次落地时 `Build osu! (Debug)` 通过；后续在 `Mirror` / `Random` 一并接入后，当前项目级 BMS 测试已更新为 **583/583** 通过

### 文档同步：将 BMS 结果页反馈面编入 Phase 1.x / P1-C

- `DEVELOPMENT_PLAN.md` 现已明确把 BMS 结果页反馈面收口归入 `P1-C`，并把边界钉死在“沿用现有 lazer results 骨架的低风险增强”上，不把 beatoraja 风格整页重构写成当前主线
- `DEVELOPMENT_STATUS.md` 现已同步记录当前已落地的结果页反馈基线：expanded 主评价与 contracted badge 已按 `DJ LEVEL` 显示，主分数区已显式标为 `EX-SCORE`
- `README.md` 已补齐对外可见的当前状态说明，避免外部摘要继续停留在“有 EX-SCORE / DJ LEVEL 数据，但不说明结果页主表达语义”的旧表述
- 验证：本次仅为文档编排与进度同步，不涉及新的代码或测试命令

## 2026-04-13

### 修复外部谱库设置 UI 不可见（DI 注册时序 + CanBeNull）

- **根因**：`ExternalLibraryConfig` 与 `ExternalLibraryScanner` 在 `OsuGameDesktop.LoadComplete()` 中才创建和 `CacheAs`，但 Settings overlay 的 async 加载（`loadComponentSingleFile` via `Schedule`）可能在此之前解析依赖。同时 `[Resolved]` 未标注 `CanBeNull = true`，框架在类型未注册时直接抛异常，导致 `ExternalLibrarySettings` subsection 整体加载失败
- **修复**：
  1. 新增 `OsuGameDesktop` 的 `[BackgroundDependencyLoader] load()`，在 BDL 阶段（base BDL 之后、任何 scheduled load 之前）创建 `ExternalLibraryConfig`/`ExternalLibraryScanner` 并 `CacheAs` 注册到 `desktopDependencies`
  2. `LoadComplete` 中仅保留 importer 委托接线（`BmsDirectoryImporter` / `ManiaDirectoryImporter`），因为 `BmsBeatmapImporter` / `ManiaBeatmapImporter` 在 `LoadComplete` 创建
  3. `ExternalLibrarySettings` 所有 `[Resolved]` 统一加 `CanBeNull = true`，确保非桌面上下文安全降级
- 构建验证：0 warning / 0 error
- 定向验证：BMS **519/519** 通过，mania OMS **92/92** 通过，osu.Game.Tests release-gate **6/6** 通过

## 2026-04-12

### 外部谱库设置 UI + 存储目录重命名

- 新增 `ExternalLibrarySelectScreen`（基于 `DirectorySelectScreen` 的全屏目录选择器），新增 `ExternalLibrarySettings`（Settings → Maintenance 子区域）：可在设置中添加 BMS / mania 外部谱库根目录、查看已注册根列表（路径有效性 + 类型/状态/最近扫描信息）、移除根目录、一键扫描全部根目录（带进度通知）
- `OsuGameDesktop` 新增 `CreateChildDependencies` 覆盖，将 `ExternalLibraryConfig` 与 `ExternalLibraryScanner` 注册到 DI 容器，设置 UI 通过 `[Resolved]` nullable 解析（非桌面端安全降级）
- `MaintenanceSection` 子区域列表增加 `ExternalLibrarySettings` 入口
- 存储目录重命名：`songs/` → `chartbms/`、`mania/` → `chartmania/`，`SONGS_STORAGE_PATH` / `MANIA_STORAGE_PATH` 常量与全部代码注释/文档同步更新
- 构建验证：0 warning / 0 error
- 定向验证：BMS **519/519** 通过，mania OMS **92/92** 通过，osu.Game.Tests release-gate **6/6** 通过

### 修复 FilterControl.updateSortDropdownState 在二次进入 Song Select 时因 Bindable Disabled 状态残留而崩溃

- **根因**：`updateSortDropdownState()` 在 DifficultyTable 分组下将 `sortDropdown.Current.Disabled = true`。此 Disabled 状态通过 `config.BindWith` 传播到全局 config bindable。第二次进入 Song Select 时，新 sortDropdown 通过 `BindWith` 继承了 `Disabled = true`，随后 `updateSortDropdownState()` 试图设置 `Value = SortMode.Difficulty` 但 bindable 已禁用 → 抛出 `InvalidOperationException`
- **影响**：`FilterControl.LoadComplete()` 在 line 222 中断，后续所有 `BindValueChanged` 回调和末尾的 `updateCriteria()` 均未执行。虽然前一个修复保证了初始 Criteria 到达 carousel，但 FilterControl 的事件链完全断裂，导致分组/排序联动失效
- **修复**：在 `updateSortDropdownState()` 设值前先 `sortDropdown.Current.Disabled = false`，设值完成后再禁用
- 构建验证：0 warning / 0 error
- 定向验证：BMS **519/519** 通过，mania OMS **92/92** 通过，osu.Game.Tests release-gate **6/6** 通过

### 修复 Song Select 初始筛选条件丢失导致的空谱面列表

- **根因**：`FilterControl.LoadComplete()` 在 `SongSelect.LoadComplete()` 之前执行。FilterControl 末尾调用 `updateCriteria()` 触发 `CriteriaChanged` 事件时，SongSelect 尚未订阅该事件，导致初始筛选条件丢失。BeatmapCarousel 的 `Criteria` 保持 `null`，`FilterAsync()` 每帧短路返回空集，谱面始终不显示
- **触发场景**：在 Song Select 将分组设为 Difficulty Table → 返回主菜单 → 重新进入 Song Select。因 DifficultyTable 模式下所有子条目默认 `IsVisible = false`（需展开分组），缺少初始 Criteria 意味着连分组表头都不会创建
- **修复**：在 `SongSelect.LoadComplete()` 订阅 `CriteriaChanged` 后，立即调用 `criteriaChanged(FilterControl.CreateCriteria())`，确保 BeatmapCarousel 总能收到首次筛选条件
- **影响范围**：修复适用于所有分组模式；DifficultyTable 最易触发是因为该模式不会被 API 登录等延迟事件意外「救回」
- 构建验证：0 warning / 0 error
- 定向验证：BMS **519/519** 通过，mania OMS **92/92** 通过，osu.Game.Tests release-gate **6/6** 通过

### 存储拓扑演进基线 + 外部多目录谱库扫描 + mania 独立目录存储

- 新增 `ExternalLibraryRoot`（数据模型）+ `ExternalLibraryConfig`（JSON `library-roots.json` 配置管理器），支持注册/移除/启用外部谱库根目录，BMS / mania 双类型均可配置
- 新增 `ExternalLibraryScanner`（委托注入式扫描器），遍历已注册根目录的直接子目录，按文件扩展名（BMS: `.bms/.bme/.bml/.pms`；mania: `.osu`）自动分类并分派到对应导入器，返回 `ScanResult{Imported, Skipped, Errors}`
- 新增 `ManiaFolderImporter`（`chartmania/<safeName-hash>/` 文件系统直读导入器），解析 .osu 文件 → 提取元数据/难度/哈希 → 复制目录 → 设置 `FilesystemStoragePath` → 写入 Realm；与 BMS `chartbms/` 同级的独立目录树
- 新增 `ManiaBeatmapImporter`（`ICanAcceptFiles` 封装），仅处理目录（.osz 继续走标准 `BeatmapImporter`），支持拖放导入与进度通知
- `OsuGameDesktop` 集成：注册 `ManiaBeatmapImporter` 作为导入处理器，创建 `ExternalLibraryConfig` 与 `ExternalLibraryScanner` 并接通 BMS / mania 导入委托，`Dispose` 清理已补齐
- 构建验证：0 warning / 0 error
- 定向验证：BMS **519/519** 通过，mania OMS **92/92** 通过

### Phase 1.17：reverse-config late-hit sweep 收口

- `TestSceneOmsScratchGameplayBridge` 本轮继续沿 reverse-config 产品矩阵补齐 late-hit miss 排序，新增四条 loaded-scene 回归：`TestInvertedMouseAxisGameplayBridgeLateHitForcesEarlierScratchMiss()`、`TestInvertedHidAxisGameplayBridgeLateHitForcesEarlierScratchMiss()`、`TestInvertedSecondScratchMouseAxisGameplayBridgeLateHitForcesEarlierScratchMiss()`、`TestInvertedSecondScratchHidAxisGameplayBridgeLateHitForcesEarlierScratchMiss()`
- 这批场景显式锁定 `axisInverted=true` 的 mouse/HID scratch 在 Scratch1 与 lane 8 / `Scratch2` 两侧都遵循与正向输入相同的 late-hit 语义：晚到输入会强制 earlier note miss，而 later note 仍可正常命中，不会因为 reverse-config 改变 miss 排序
- 定向验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsScratchGameplayBridge" -v minimal` **43/43** 通过

### Phase 1.17：scratch bridge symmetry sweep 收口

- `TestSceneOmsScratchGameplayBridge` 本轮沿同一 loaded-scene 产品矩阵继续做对称补齐，新增六条回归：`TestKeyboardHeldScratchSuppressesInvertedHidPulseGameplayEdgeUntilFinalRelease()`、`TestKeyboardHeldScratchSuppressesInvertedMousePulseGameplayEdgeUntilFinalRelease()`、`TestSecondScratchMouseAxisGameplayBridgeLateHitForcesEarlierScratchMiss()`、`TestSecondScratchHidAxisGameplayBridgeLateHitForcesEarlierScratchMiss()`、`TestSecondScratchXInputGameplayBridgeLateHitForcesEarlierScratchMiss()`、`TestSecondScratchXInputScratchHoldResolvesTail()`
- 这批场景把 Scratch1 的 inverted suppression、以及 14K `Scratch2` 的 late-hit miss 排序与 direct XInput held-path 全部纳入同一产品级回归，显式锁定 reverse-config pulse 不会在 keyboard-held 时产生额外 gameplay edge，lane 8 / `Scratch2` 的晚到输入会强制 earlier note miss 且 later note 仍可正常命中，同时 second scratch 也已具备不依赖 keyboard takeover 的 direct XInput hold 尾判与最终释放证明
- 定向验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsScratchGameplayBridge" -v minimal` **39/39** 通过

### Phase 1.17：14K Scratch2 normal hold-survival 收口

- `TestSceneOmsScratchGameplayBridge` 已继续补齐第二 scratch 的 held-path 产品语义，本轮新增两条 loaded-scene 回归：`TestKeyboardHeldSecondScratchHoldSurvivesMousePulseAndResolvesTail()`、`TestKeyboardHeldSecondScratchHoldSurvivesHidPulseAndResolvesTail()`
- 这批场景显式锁定 lane 8 / `Scratch2` 的 keyboard-held hold 在普通 mouse/HID pulse 经过 `FinishFrame()` / `FinishPolling()` 边界后不会断 hold，tail 仍经 held path 判定，且动作直到最终 keyboard release 才真正松开
- 定向验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsScratchGameplayBridge" -v minimal` **33/33** 通过

### Phase 1.17：14K Scratch2 inverted suppression 收口

- `TestSceneOmsScratchGameplayBridge` 已继续补齐第二 scratch 的 reverse-config first-press / final-release 产品语义，本轮新增两条 loaded-scene 回归：`TestKeyboardHeldSecondScratchSuppressesInvertedHidPulseGameplayEdgeUntilFinalRelease()`、`TestKeyboardHeldSecondScratchSuppressesInvertedMousePulseGameplayEdgeUntilFinalRelease()`
- 这批场景显式锁定 lane 8 / `Scratch2` 在 keyboard-held 且 `axisInverted=true` 的 HID、mouse 追加 pulse 下不会产生额外 gameplay hit edge，且动作会一直保持到 keyboard 与 inverted pulse 全部真正释放后才结束
- 定向验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsScratchGameplayBridge" -v minimal` **31/31** 通过

### Phase 1.17：14K Scratch2 mixed-source suppression 收口

- `TestSceneOmsScratchGameplayBridge` 已继续补齐第二 scratch 的 first-press / final-release 产品语义，本轮新增三条 loaded-scene 回归：`TestKeyboardHeldSecondScratchSuppressesHidPulseGameplayEdgeUntilFinalRelease()`、`TestKeyboardHeldSecondScratchSuppressesMousePulseGameplayEdgeUntilFinalRelease()`、`TestKeyboardHeldSecondScratchSuppressesXInputGameplayEdgeUntilFinalRelease()`
- 这批场景显式锁定 lane 8 / `Scratch2` 在 keyboard-held 前提下不会因 HID、mouse 或 custom XInput 的追加输入产生额外 gameplay hit edge，并且动作会一直保持到最终 source release 才真正松开
- 定向验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsScratchGameplayBridge" -v minimal` **29/29** 通过

### Phase 1.17：14K Scratch2 reverse-config gameplay bridge 收口

- `TestSceneOmsScratchGameplayBridge` 已继续把 reverse-config 扩到 14K 第二 scratch，本轮新增四条 loaded-scene 回归：`TestInvertedSecondScratchMouseAxisGameplayBridgeResolvesScratchStreamNotes()`、`TestInvertedSecondScratchHidAxisGameplayBridgeResolvesScratchStreamNotes()`、`TestKeyboardHeldSecondScratchHoldSurvivesInvertedMousePulseAndResolvesTail()`、`TestKeyboardHeldSecondScratchHoldSurvivesInvertedHidPulseAndResolvesTail()`
- 这批场景显式锁定 lane 8 / `Scratch2` 在 `axisInverted=true` 的 mouse/HID 绑定下仍能产出真实 scratch edge，并且 keyboard-held second scratch hold 在 inverted mouse/HID pulse 经过 `FinishFrame()` / `FinishPolling()` 边界时不会断 hold，直到最终 keyboard release 才真正松开动作
- 定向验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsScratchGameplayBridge" -v minimal` **26/26** 通过

### Phase 1.17：14K Scratch2 gameplay bridge 首轮补测

- `TestSceneOmsScratchGameplayBridge` 现已支持把 mouse-axis / HID-axis / custom XInput scratch 绑定到可选 `OmsAction`，不再只覆盖 `Key1P_Scratch`；同一套 loaded headless scene 现在可以直接验证 `Key2P_Scratch -> BmsAction.Scratch2` 的真实 `DrawableBmsRuleset -> BmsPlayfield -> scratch note/hold` 玩法桥
- 本轮新增四条 14K 第二 scratch 回归：`TestSecondScratchMouseAxisGameplayBridgeResolvesScratchStreamNotes()`、`TestSecondScratchHidAxisGameplayBridgeResolvesScratchStreamNotes()`、`TestSecondScratchXInputGameplayBridgeResolvesScratchStreamNotes()`、`TestKeyboardHeldSecondScratchHoldTransfersToXInputAndResolvesTail()`。它们显式锁定 lane 8 / `Scratch2` 的 mouse、HID、custom XInput 命中链，以及 keyboard-held hold 在 second scratch 的 XInput 接管与最终释放语义
- 定向验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsScratchGameplayBridge" -v minimal` **22/22** 通过

### Phase 1.17：analog scratch reverse-config gameplay bridge 收口首刀

- `TestSceneOmsScratchGameplayBridge` 现已支持为 mouse-axis / HID-axis scratch 注入自定义 trigger，不再只用硬编码的正向 turntable 绑定；这样 loaded headless scene 可以直接覆盖 reverse-config 下的真实 `DrawableBmsRuleset -> BmsPlayfield -> scratch note/hold` 运行链
- 本轮新增四条 1.17 产品语义回归：`TestInvertedMouseAxisGameplayBridgeResolvesScratchStreamNotes()`、`TestInvertedHidAxisGameplayBridgeResolvesScratchStreamNotes()`、`TestKeyboardHeldScratchHoldSurvivesInvertedMousePulseAndResolvesTail()`、`TestKeyboardHeldScratchHoldSurvivesInvertedHidPulseAndResolvesTail()`。它们显式锁定 `axisInverted=true` 的 mouse/HID 绑定仍能产出 scratch edge，且 keyboard-held hold 在 inverted pulse 经过 `FinishFrame()` / `FinishPolling()` 边界时不会断 hold，直到最终 keyboard release 才真正松开动作
- 定向验证：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsScratchGameplayBridge" -v minimal` **18/18** 通过

## 2026-04-11

### Phase 1.1：SkinManager product-surface release gate 收口

- `TestSceneOmsBuiltInSkin` 现已补齐 `GetAllUsableSkins()`、`SelectRandomSkin()`、`SetSkinFromConfiguration()`、`SelectNextSkin()` / `SelectPreviousSkin()`、`SkinManager.AllSources` 与 `Delete()` 这批产品面回归，明确锁定“OMS 永远是唯一受保护默认项，用户皮肤只作为可选层叠 source”的最终行为
- 本轮把 release gate 从“transformer / fallback 是否存在”推进到“运行时可选皮肤列表、随机切换、配置回退、前后切换、source-chain、删除当前皮肤回退”这一层真实产品语义，避免 1.1.11 只剩代码层契约却缺 UI/状态机层证明
- 定向验证分三批通过：`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --filter "(Name=TestUsableSkinListContainsOmsThenUserSkins|Name=TestRandomSkinFallsBackToOmsWithoutUserSkins|Name=TestRandomSkinSelectsOnlyAvailableUserSkin)"` **3/3**，`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --filter "(Name=TestSetSkinFromConfigurationSelectsUserSkin|Name=TestUnknownSkinConfigurationFallsBackToOms|Name=TestSelectNextSkinCyclesAcrossOmsAndUserSkins|Name=TestSelectPreviousSkinCyclesAcrossOmsAndUserSkins)"` **4/4**，`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --filter "(Name=TestAllSourcesContainsOnlyOmsWhenOmsIsCurrent|Name=TestAllSourcesAddsOmsFallbackBehindUserSkin|Name=TestDeletingCurrentUserSkinFallsBackToOms|Name=TestDeletingNonCurrentUserSkinKeepsCurrentUserSkin)"` **4/4**

### Phase 1.1：startup skin migration 与 `osu.Game.Tests` release gate 恢复

- `OsuGame` 现已在 `SetSkinFromConfiguration()` 前先订阅 `CurrentSkinInfo` 到 config 的回写，遗留 upstream built-in GUID 在启动 fallback 到 OMS 时会同步把配置值纠正为 OMS；`TestSceneStartupSkinMigration` 已新增对应启动迁移回归，并改为使用公开 `CreateInfo().ID`，避免对 internal GUID 常量形成额外耦合
- `osu.Game.Tests.csproj` 现已显式排除仍强依赖已删除 Osu/Taiko/Catch 规则集的历史测试面，同时把 `TestResources` / `WaveformTestBeatmap` 默认 ruleset 改成 mania，`TestSceneHitEventTimingDistributionGraph` 也移除了对 osu! 物件的硬依赖，`TestSceneMissingBeatmapNotification` 则内联轻量 `ArchiveReader` 测试桩；`OsuGameBase` 还补上 API 组件已有父容器时不再二次挂载的保护，恢复 `OsuGameTestScene` 这条 visual regression 链
- 定向验证：`dotnet test .\osu.Game.Tests\osu.Game.Tests.csproj --filter "TestSceneStartupSkinMigration|TestSceneEditDefaultSkin|TestSettingsMigration"` **6/6** 通过

### Phase 1.1：global `Results` target 与 Skin Editor Results preview 最小闭环

- `GlobalSkinnableContainers` 新增 `Results`，`OmsSkinTransformer` 现会为 global `Results` target 返回 shared shell，`ResultsScreen` 也已补上对应 `SkinnableContainer`；`SKIN/SimpleTou-Lazer/Results.json` 同步补齐 embedded global layout metadata，使 `MainHUDComponents` / `SongSelect` / `Results` / `Playfield` 四类 global target 都有一致的 layout 装载入口
- Skin Editor 现已新增 Results scene 按钮，并通过读取本地已有 `ScoreInfo` 推出 `SoloResultsScreen`；这里刻意复用真实 score 而不是空壳模型，因为 `StatisticsPanel` 这条链要求完整 `ScoreInfo` 才能稳定工作。若本地无可预览成绩，界面会显示明确 toast，而不是静默失败
- 定向验证：`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --filter "TestOmsSkinProvidesEmbeddedGlobalLayoutMetadata"` **1/1** 通过，`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --filter "TestOmsSkinUsesSharedTransformerShell"` **1/1** 通过

## 2026-04-10

### Phase 1.1：native default registration cleanup

- `SkinManager` 构造期现已只维护 `DefaultOmsSkin` 这一条受保护 built-in realm 记录；`Argon` / `ArgonPro` / `Triangles` / `DefaultLegacy` / `Retro` 的历史内建皮肤条目会在启动时被清理，避免上游默认皮肤继续以产品内建项的形式出现在数据库中
- 由于 settings dropdown、random/previous/next skin 逻辑此前已经统一走 OMS + 非受保护用户皮肤列表，本轮等于补齐了 1.1.11 剩余的数据库/产品暴露面；旧的上游 protected skin GUID 仍继续经 `SetSkinFromConfiguration()` 安全回退到 OMS
- `TestSceneOmsBuiltInSkin` 已新增 `TestUpstreamBuiltInSkinsAreNotRegisteredInDatabase()` 回归，直接锁定 `Triangles` / `Argon` / `ArgonPro` / `Classic` / `Retro` 不再注册进 realm；本轮验证为 `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **81/81** 通过、`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics" -v minimal` **75/75** 通过，`Build osu! (Debug)` 通过

### Phase 1.1：mixed-layer 用户皮肤三态 runtime 回归收口

- `TestSceneBmsUserSkinFallbackSemantics` 现已补上真实 mania-only legacy 用户皮肤场景：当用户皮肤只提供 `mania-key*` 这类 legacy mania 资源时，BMS runtime 仍会稳定落到 OMS 默认皮肤包的 BMS 层，`ComboCounter` 与 ruleset HUD 现已分别锁定为 `BmsComboCounter` 与 `DefaultBmsHudLayoutDisplay`
- `TestSceneOmsBuiltInSkin` 现已补上真实 BMS-only 用户皮肤场景：当用户皮肤只提供 BMS lookup（当前以 `BmsSkinComponents.ComboCounter` 作为实际 BMS layer 证明）时，mania gameplay note 路径仍会稳定回落到 OMS mania 层，运行时会继续加载 `OmsNotePiece`
- 同一皮肤选择项同时含 `legacy mania` 资源与 `BMS` lookup 的场景现也已补成双侧 runtime 证明：在 BMS 侧会优先消费该皮肤自身的 `HudLayout` / `ComboCounter`，在 mania 侧则会继续走 `LegacyNotePiece`，且 BMS layer 不会泄漏到 mania note 路径；1.1.10 现在已能明确回答 mania-only、BMS-only、以及 Mania+BMS 同包三类导入/回退语义
- 最新 mixed-layer 定向基线：`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics" -v minimal` **75/75** 通过；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **75/75** 通过；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal` **476/476** 通过，`Build osu! (Debug)` 通过

### Phase 1.1：BMS 用户皮肤 fallback 语义收口

- `BmsSkinTransformer` 现已不再让普通用户皮肤在缺失 BMS 组件时直接于当前 transformer 内补齐 built-in 默认件：`HudLayout` / `GaugeBar` / `ComboCounter` / `Judgement` / `NoteDistribution` / `GaugeHistory` / `ResultsSummary` / `StaticBackgroundLayer` / playfield/lane/note/lane-cover 等 BMS lookup 在 non-OMS skin 缺失时现会返回 `null`，把缺省路径继续交给后续 source 链与 OMS fallback
- BMS ruleset HUD 路径也已同步收口：`MainHUDComponents` 仅会在当前 skin 实际暴露 BMS HUD layer 时才在本 source 内组装 HUD；不含 BMS 层的用户皮肤不再拦截后续 source 的 BMS HUD / combo fallback
- `BmsSkinTransformerTest` 已把“默认 fallback”断言切到 OMS source，并新增普通用户皮肤缺失 BMS layer 时返回 `null` 的回归；`TestSceneBmsUserSkinFallbackSemantics` 也已新增 runtime source-chain 验证，锁定缺失 BMS layer 的用户皮肤会把 combo 与 ruleset HUD lookup 继续让给后续 source。`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest|FullyQualifiedName~TestSceneBmsUserSkinFallbackSemantics" -v minimal` **73/73** 通过，完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal` **474/474** 通过，`Build osu! (Debug)` 通过

### Phase 1.1：legacy 用户皮肤 combo/HUD / bar-line partial override 增量

- `ManiaLegacySkinTransformer` 现已不再因为 key-only legacy 用户皮肤具备 `mania-key*` 就无条件接管 ruleset HUD 与 bar-line：`MainHUDComponents` 现要求实际存在 legacy combo font 才会返回 `LegacyManiaComboCounter` 容器，而 `ManiaSkinComponents.BarLine` 现也改为只在检测到显式 legacy bar-line 样式覆盖时才返回 `LegacyBarLine`
- `LegacySkin` 的 mania config lookup 会为 `BarLineHeight` 始终提供默认值，因此 bar-line 门控不能简单按 bindable 是否存在判断；本轮已把条件收口为“显式 `ColourBarline` 覆盖或 `BarLineHeight` 偏离默认值 1”，避免 key-only legacy 用户皮肤因默认值误占用 OMS fallback
- `TestSceneOmsBuiltInSkin` 已新增 `TestLegacyUserSkinWithoutComboFontFallsBackToOmsComboCounter()` 与 `TestLegacyUserSkinWithoutBarLineConfigFallsBackToOmsBarLine()` 回归；新增定向回归 **2/2** 通过，`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **73/73** 通过，`Build osu! (Debug)` 通过

### Phase 1.1：legacy 用户皮肤 judgement / hit explosion partial override 增量

- `ManiaLegacySkinTransformer` 现已为 `ManiaSkinComponents.HitExplosion` 补上基于实际 legacy explosion 资源存在性的 component-level 门控：当 legacy 用户皮肤缺失 `ExplosionImage` / `lightingN` 时，runtime 不再强行实例化 `LegacyHitExplosion`，而是返回 `null` 让 OMS fallback 继续接管
- judgement 缺失资源时回退 OMS 的语义此前已实际存在于 `SkinComponentLookup<HitResult>` 路径，但尚未被 regression 锁住；本轮已补上 key-only legacy 用户皮肤在缺失 judgement 资源时回退 `OmsManiaJudgementPiece` 的验证，并确认 legacy judgement piece 不会误接管
- `TestSceneOmsBuiltInSkin` 已新增 `TestLegacyUserSkinWithoutJudgementAssetsFallsBackToOmsJudgementPiece()` 与 `TestLegacyUserSkinWithoutHitExplosionAssetsFallsBackToOmsHitExplosion()` 回归；新增定向回归 **2/2** 通过，`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **71/71** 通过，`Build osu! (Debug)` 通过

## 2026-04-09

### Phase 1.1：legacy 用户皮肤 partial override 首个 note/hold 切口

- `ManiaLegacySkinTransformer` 现已不再因为 legacy 用户皮肤“只要有 key 贴图”就无条件返回 legacy note / hold 组件；`Note` / `HoldNoteHead` / `HoldNoteTail` / `HoldNoteBody` 的 legacy 路由现已改为按实际 legacy 资源是否存在决定，缺失资源时会返回 `null` 让 OMS fallback 继续接管
- 当 legacy 用户皮肤只提供 `mania-key*` 但未提供 note / hold 资源时，runtime 不再被 `LegacyNotePiece` / `LegacyBodyPiece` 强占：缺失 note 资产时现会回退 `OmsNotePiece`，缺失 hold-body 资产时现会回退 `OmsHoldNoteBodyPiece`，为后续 judgement / hitburst / HUD / bar-line 的 component-level partial override 铺好第一条真实桥接路径
- `TestSceneOmsBuiltInSkin` 已新增 `TestLegacyUserSkinWithoutNoteAssetsFallsBackToOmsNotePiece()` 与 `TestLegacyUserSkinWithoutHoldBodyAssetsFallsBackToOmsHoldBodyPiece()` 回归；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **69/69** 通过，`Build osu! (Debug)` 通过

### Phase 1.1：ruleset resources 与 OMS fallback 顺序收口

- `RulesetSkinProvidingContainer` 现已不再按 `TrianglesSkin` 硬编码定位 ruleset resources 的插入点，而是改为在最后一个受保护 built-in skin source 之前插入 `ResourceStoreBackedSkin`；当 gameplay lookup 链中同时存在用户皮肤与 OMS built-in fallback 时，ruleset resources 现会稳定落在两者之间
- `SkinManager.AllSources` 现已按 `SkinInfo.ID` 而不是对象引用判断“当前是否已经是 `OmsSkin`”；当前选择的 OMS 皮肤实例不再把 `DefaultOmsSkin` 作为重复 fallback 再挂一次，运行时 source chain 不再出现 `Oms -> ... -> Oms` 的重复 built-in 路径
- `TestSceneOmsBuiltInSkin` 已新增 `TestRulesetResourcesPrecedeOmsBuiltInFallback()` 与 `TestRulesetResourcesPrecedeOmsFallbackForLegacyUserSkin()` 回归；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **67/67** 通过，`Build osu! (Debug)` 通过

### Phase 1.1：OMS native-default removal 首个 runtime fallback 切口

- `RulesetSkinProvidingContainer` 现已把 beatmap legacy compatibility fallback 从 `DefaultClassicSkin` 切到 `DefaultOmsSkin`；当当前选择的是非 legacy 皮肤、且 beatmap skin 需要 legacy 资源兼容时，运行时内部回退链不再悄悄落回 upstream 默认皮肤
- `SkinManager.SetSkinFromConfiguration()` 的受保护 upstream built-in id 回退语义已补上回归：`Argon` / `Triangles` / `DefaultLegacy` / `Retro` 现都会统一回到 `OmsSkin`，不再通过配置入口重新暴露 upstream 默认皮肤作为产品默认选择
- `TestSceneOmsBuiltInSkin` 已新增 legacy beatmap compatibility fallback 与 protected upstream built-in id 的回归；后续同日又补上 ruleset resources / OMS fallback 顺序与 OMS built-in 去重回归；当前最新组合过滤为 **67/67** 通过，`Build osu! (Debug)` 通过

### Phase 1.1：OmsSkin mania note scrolling 显示状态收口

- `OmsNotePiece` 现已把 direction anchor / origin / scale 收口成显式 OMS display-state contract，不再继续依赖 legacy 风格的隐含 container origin 初始值；`OmsHoldNoteTailPiece` 仍通过 `GetDisplayDirection()` 承接 tail 的反向显示语义
- `TestSceneOmsBuiltInSkin` 已新增 normal note scrolling display-state 回归；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **60/60** 通过，`Build osu! (Debug)` 通过

### Phase 1.1：OmsSkin mania bar-line major/minor 运行时语义收口

- `OmsBarLine` 现已补上 OMS 自有的 major/minor runtime 语义，不再继续沿用 legacy bar line 对 `DrawableBarLine.Major` 无感知的单态表现；major 线保持 full-height / full-opacity，minor 线则会下调高度与亮度
- `TestSceneOmsBuiltInSkin` 已新增 bar line major→minor 切换回归，并把 dual-stage / mixed-stage shared-height 断言显式锁到 major 线；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **59/59** 通过，`Build osu! (Debug)` 通过

### Phase 1.1：OmsSkin mania combo counter HUD 运行时语义收口

- `OmsManiaComboCounter` 现已移除 legacy 风格的 rolling、combo break pop-out 与滚动归零动画链；运行时改为单文本即时同步，shared `ComboPosition` 继续仅作为 OMS 的 non-column HUD position contract 保留
- `TestSceneOmsBuiltInSkin` 已新增 combo counter 只保留单一 `OsuSpriteText` 节点、且 combo break 会立即清空显示的回归；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **58/58** 通过，`Build osu! (Debug)` 通过

### 1.12：BMS 密度星级首轮重标定

- `BmsDifficultyCalculator` 现已把密度星级从原先过于激进的平方根映射，改为更保守的对数映射；同一 keymode 的排序稳定性保持不变，但低密度到中密度谱面的星级会明显下修，避免当前实际显示整体系统性偏高
- `BmsDifficultyCalculator.Version` 已同步递增到 `20260409`，让现有缓存星级按后台重算流程失效并刷新；每个 keymode 的 reference density 常数暂时保持不变，后续仍可继续基于真实谱面样本做第二轮校准
- `BmsDifficultyCalculatorTest` 已按新映射更新基准断言；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsDifficultyCalculatorTest" -v minimal` **3/3** 通过，完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal` **463/463** 通过，`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### 1.3 / 1.4 / 1.16：BMS 谱面元数据补全与 Song Select 摘要扩展

- `BmsBeatmapDecoder` 现已新增 `#SUBARTIST` / `#COMMENT` 解析；`BmsBeatmapConverter` 会把 `Subtitle` / `SubArtist` / `Comment` / `PlayLevel` / `HeaderDifficulty` 写入 `BmsBeatmapMetadataData.ChartMetadata`，并在可判定时把谱师同步到 `metadata.Author.Username`
- `BmsNoteDistributionGraph` 右侧摘要现会在统计文字之外合并显示 chart creator、内部标级、副标题与难度表标签，Song Select 不再只能看到纯 note distribution 统计
- 当前最新完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore -v minimal` **463/463** 通过

### Phase 1.1：OmsSkin mania combo counter 文本路径切离 legacy 字体

- `OmsManiaComboCounter` 现已不再使用 `LegacySpriteText` / `LegacyFont.Combo`，改为 OMS 自有数码文本实现；这一步把 combo 组件从 legacy 字体图集路径上切开，但仍保留现有 rolling / fade HUD 行为
- `TestSceneOmsBuiltInSkin` 已补上 combo counter 不再生成 `LegacySpriteText` 子树的回归断言；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **50/50** 通过

### Phase 1.1：OmsSkin mania hold-body 语义收口到 OMS preset

- 已新增 `OmsManiaHoldNoteBodyPreset`，并让 `ManiaOmsSkinTransformer` 为 `HoldNoteLightImage` / `HoldNoteLightScale` / `NoteBodyStyle` 返回 OMS 自有 hold-body preset；OMS preview 路径下的 hold-body 默认值不再继续依赖 legacy `skin.ini` 推导
- `OmsHoldNoteBodyPiece` 现已删除 legacy `NoteBodyStyle` 分支，固定使用 clamp/stretch 型 body 贴图语义；运行时缩放会随 scroll direction 在 `Vector2.One` 与 `new Vector2(1, -1)` 间切换，不再进入旧的 wrap-stretch 放大量级路径
- `TestSceneOmsBuiltInSkin` 已补上 hold-body semantic config 与运行时缩放回归；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **51/51** 通过

### Phase 1.1：OmsSkin mania note-height 语义收口到 OMS layout preset

- `OmsManiaLayoutPreset` 现已把 `WidthForNoteHeightScale` 纳入 stage-local preset；4K/7K 保留候选皮 `skin.ini` 的显式 note-height override，其余 stage 显式回落到各自最小列宽，而不是继续隐式依赖 legacy decoder fallback
- `OmsNotePiece` 现已改为按列读取该 lookup，mixed-stage 场景下第二 stage 的 note-height 不再复用第一 stage 或 total-columns legacy 默认值
- `TestSceneOmsBuiltInSkin` 已新增 single-stage / mixed-stage note-height config 回归与 mixed-stage 运行时 note-height 比例回归；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **54/54** 通过，`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania hold-tail 方向语义收口到 OMS display hook

- `OmsNotePiece` 现已抽出正式的 `GetDisplayDirection()` 钩子，`OmsHoldNoteTailPiece` 通过该钩子承接 tail 的反向显示语义，不再继续伪造反向 `ValueChangedEvent<ScrollingDirection>`；这一步把 hold-tail 的方向处理从 legacy 风格事件翻转，收口为 OMS 自身的显示语义钩子
- `TestSceneOmsBuiltInSkin` 已新增 hold-tail inverted scrolling-direction 场景回归，锁定默认下滚与上下切换时的 anchor / scale 行为；定向 mania 回归现为 `55/55` 通过
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **55/55** 通过，`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania hold-body 运行时表现切离 legacy light / fade

- `OmsHoldNoteBodyPiece` 现已移除 legacy 风格的 hold-hit light 与 miss dark-gray fade 运行时链路：不再向 column 顶层插入额外 `HitTargetInsetContainer`，也不再因 body miss 把 head / tail / body 一起染暗；body 运行时仅保留 OMS stretch 贴图与 scrolling-direction 对应的 anchor / scale 行为
- `ManiaOmsSkinTransformer` 仍为 `NoteBodyStyle` / `HoldNoteLightImage` / `HoldNoteLightScale` 返回 OMS preset，以维持既有 config lookup 桥；但 `OmsHoldNoteBodyPiece` 自身不再消费 legacy 风格的 light / fade 表现
- `TestSceneOmsBuiltInSkin` 已新增 forced holding 不再插入 light container、forced body miss 不再触发 miss fade 的场景回归；`dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin" -v minimal` **57/57** 通过，`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### 文档同步：运行时存储与后续拓扑结论收口

- `README.md`、`RELEASE.md`、`DEVELOPMENT_PLAN.md`、`DEVELOPMENT_STATUS.md` 与 `OMS_COPILOT.md` 现已统一写明当前默认 AppData 数据根、`storage.ini` 单自定义数据根能力，以及“若后续进入存储改造，优先多目录外部谱库，不先做 mania sibling dir / 默认单包模式”的规划结论

### 1.17：Windows 默认 HID backend 切到 DirectInput

- `oms.Input` 已新增 `Devices/OmsWindowsDirectInput`，并引入 `Vortice.DirectInput`；Windows 下的 `OmsHidDeviceHandler.CreateDefaultDeviceProvider()` 与 `OmsHidDeviceDiscovery` 现默认走 DirectInput 枚举/轮询路径，避免再次触发 `HidSharp.DeviceList.Local` 的 `RegisterClass failed` 进程级崩溃
- `OmsHidDeviceCaptureSession` 与 `OmsHidDeviceHandler` 现会在目标设备缺席时主动重刷设备列表，不再依赖 provider 侧热插拔事件；DirectInput 标识符也会尽量保留 `hid:vid_xxxx&pid_xxxx` 形态，仅在无法提取 VID/PID 时退回 `dinput:instance_{guid}`
- `HidSharp` 仍保留为 Windows 上的诊断后端，仅在显式设置 `OMS_ENABLE_HIDSHARP=1` 时才会被触发；非 Windows 路径继续沿用原有 `HidSharp` provider
- `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --filter "FullyQualifiedName~OmsHidDeviceHandlerTest"` **14/14** 通过；较早同日完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **458/458** 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过（当前同日最新完整回归见上方 **463/463** 条目）

### 1.17：Windows HidSharp 后端默认改为诊断开关，启动与设置不再因 HID 初始化闪退

- `OmsHidSharpRuntime` 现已集中接管 Windows HID backend gate；由于 `HidSharp.DeviceList.Local` 可能以 `RegisterClass failed` 在内部线程直接终止进程，Windows 构建默认不再触发 HidSharp，仅在显式设置 `OMS_ENABLE_HIDSHARP=1` 时才继续初始化该后端
- `OmsHidDeviceHandler` / `OmsHidDeviceDiscovery` 与设置页 supplemental editor 现都走同一层 gate；点击设置时看到的 HID-disabled 提示属于预期防崩溃降级，说明问题已收敛到待修复的 Windows HID 设备加载后端，而不是设置面板或皮肤系统本身挂死
- 当前防崩溃策略下，键盘 / Raw Input / XInput / MouseAxis 主链保持可用，Release 启动已不再因 HidSharp 即时闪退；Windows HID backend 稳定化仍留在 1.17 输入专项后续工作中
- `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsHidDeviceHandlerTest"` **11/11** 通过；`dotnet build .\osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过，并完成一次 Release 启动 smoke 验证未见即时崩溃

### Phase 1.1：OmsSkin mania hold-body 升格为实际 OMS-owned 实现

- `OmsHoldNoteBodyPiece` 现已改为真实 `OmsManiaColumnElement` 路径下的实际实现，不再继承 `LegacyBodyPiece`；当前继续复用 `HoldNoteBodyImage` preset、legacy `NoteBodyStyle` / `HoldNoteLightImage` / `HoldNoteLightScale`，以及 hold-body light / fade / wrap-stretch 语义
- `OmsOwnedSkinComponentContractTest` 现已扩到 note / hold-head / hold-tail / hold-body / judgement / hit explosion / combo counter / bar line 八类组件，并由 `TestSceneOmsBuiltInSkin` 补上 hold-body scrolling-direction 行为回归
- 这一步代表 mania 第二批里的 hold-body 也已从“显式组件入口”推进到“实际 OMS-owned component implementation”；当前剩余重点进一步收窄为 note/hold / combo/HUD / bar-line 仍在消费的 legacy-derived 语义清理
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin"` **50/50** 通过

### Phase 1.1：OmsSkin mania note / hold-head / hold-tail 升格为实际 OMS-owned 实现

- `OmsNotePiece` 现已改为真实 `OmsManiaColumnElement` 派生实现，不再继承 `LegacyNotePiece`；当前继续复用 `NoteImage` preset、`WidthForNoteHeightScale` 与 legacy note scrolling / sizing 语义
- `OmsHoldNoteHeadPiece` / `OmsHoldNoteTailPiece` 现已分别改为真实 `OmsNotePiece` 派生实现，不再继承 `LegacyHoldNoteHeadPiece` / `LegacyHoldNoteTailPiece`；当前继续复用 `HoldNoteHeadImage` / `HoldNoteTailImage` preset 与 legacy tail inversion / fallback 语义
- `OmsOwnedSkinComponentContractTest` 现已扩到 note / hold-head / hold-tail / judgement / hit explosion / combo counter / bar line 七类组件，持续锁定它们不再回退到 legacy implementation
- 这一步代表 mania 第二批里的 note / hold-head / hold-tail 也已从“显式组件入口”推进到“实际 OMS-owned component implementation”；当前剩余重点进一步收窄为 hold-body 与 note/hold / combo/HUD / bar-line 仍在消费的 legacy-derived 语义清理
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin"` **48/48** 通过

## 2026-04-08

### Phase 1.1：OmsSkin mania combo 与 bar-line 升格为实际 OMS-owned 实现

- `OmsManiaComboCounter` 现已改为真实 `CompositeDrawable, ISerialisableDrawable` 实现，不再继承 `LegacyManiaComboCounter`；当前继续复用 `ComboPosition` shared HUD preset、combo break colour、legacy combo font 与 rolling/fade animation 语义
- `OmsBarLine` 现已改为真实 `CompositeDrawable` 实现，不再继承 `LegacyBarLine`；当前继续复用 `BarLineHeight` / `BarLineColour` shared bar-line config 与既有 box / edge-smoothness 语义
- `OmsOwnedSkinComponentContractTest` 现已扩到 judgement / hit explosion / combo counter / bar line 四类组件，持续锁定它们不再回退到 legacy implementation
- 这一步代表 mania 第二批里的 combo counter 与 bar-line 也已从“显式组件入口”推进到“实际 OMS-owned component implementation”；当前剩余重点收窄为 note / hold 的余下默认路径迁移，以及 combo/HUD / bar-line 仍在消费的 legacy-derived 语义清理
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin"` **45/45** 通过

### Phase 1.1：OmsSkin mania judgement 与 hitburst 升格为实际 OMS-owned 实现

- `OmsManiaJudgementPiece` 现已改为真实 `CompositeDrawable, IAnimatableJudgement` 实现，不再继承 `LegacyManiaJudgementPiece`；当前继续复用 `Hit300g` / `Hit300` / `Hit200` / `Hit100` / `Hit50` / `Hit0` judgement asset preset 与 legacy-derived judgement positioning/animation 语义
- `OmsHitExplosion` 现已改为真实 `LegacyManiaColumnElement, IHitExplosion` 实现，不再继承 `LegacyHitExplosion`；当前继续复用 `ExplosionImage` / `ExplosionScale` hitburst config preset、scroll direction anchor 与既有 fade/animation 语义
- 新增 `OmsOwnedSkinComponentContractTest`，锁定 judgement / hit explosion 持续满足 `IAnimatableJudgement` / `IHitExplosion` 契约，且不再回退到 legacy implementation
- 这一步代表 mania 第二批里的 judgement / hitburst 已从“组件入口收口”推进到“实际 OMS-owned component implementation”；当前剩余重点转向 combo/HUD 与 note/hold/bar-line 的余下默认路径迁移，score-driven results 是否需要独立 preview/skinnable target 暂留后续评估
- 当次里程碑对应的 `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~OmsOwnedSkinComponentContractTest|FullyQualifiedName~TestSceneOmsBuiltInSkin"` 为 **43/43** 通过；当前最新同类定向回归结果见上方 hold-body 条目中的 **50/50**

### Phase 1.1：shared results panel shell 升格为 core stateful contract

- `osu.Game` 现已新增共享 `DefaultResultsPanelDisplay<TState>`，把 results-style panel 的 title / status / accent / shell 状态管理从单纯 `DefaultResultsPanelContainer` 抬升为 core 级 stateful contract
- `DefaultBmsResultsSummaryPanelDisplay` / `DefaultBmsGaugeHistoryPanelDisplay` / `DefaultBmsNoteDistributionPanelDisplay` 现都改走该基类，不再各自重复维护 shell 配色、空态文本与内容显隐；同时新增 `ResultsPanelDisplayContractTest` 锁定三者继续走同一 contract
- 这一步代表 1.1.4 里的 shared results panel shell 已不再只是可复用容器，而是已收口为真实的 core results panel 语义；当前剩余 gap 收窄为决定 score-driven results 是否需要独立的 preview/skinnable target，以及 mania 第二批余下的实际 OMS-owned Hold / HitBurst / Judgement / HUD 默认路径迁移
- `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~ResultsPanelDisplayContractTest|FullyQualifiedName~StatisticItemContainerTest|FullyQualifiedName~BmsRulesetStatisticsTest|FullyQualifiedName~BmsSkinTransformerTest"` **69/69** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **446/446** 通过；`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：results-style shared panel shell 首次落地

- `osu.Game` 现已新增共享 `DefaultResultsPanelContainer`，`DefaultBmsResultsSummaryPanelDisplay` / `DefaultBmsGaugeHistoryPanelDisplay` / `DefaultBmsNoteDistributionPanelDisplay` 不再各自复制同一套 rounded results panel 结构，而是统一复用这层 shared shell
- `StatisticItemContainer` 现对空标题 `StatisticItem` 跳过通用灰色 wrapper，BMS results summary / gauge history 这类 panel-owned title 与 panel-owned shell 不再被再包一层 generic shell；同时新增 `StatisticItemContainerTest` 锁定“有标题保留 generic shell、空标题移除 generic shell”的结构回归
- 这一步代表 1.1.4 里的 results summary 容器已从“纯待办”推进到“已有 shared panel shell 首落”；当前剩余 gap 收窄为将这层 shared shell 继续抬升为真正的 global results summary container 语义，以及 mania 第二批余下的实际 OMS-owned Hold / HitBurst / Judgement / HUD 默认路径迁移
- `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~StatisticItemContainerTest|FullyQualifiedName~BmsRulesetStatisticsTest|FullyQualifiedName~BmsSkinTransformerTest"` **66/66** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **443/443** 通过；`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin global layout metadata 首次收口

- `TestSceneOmsBuiltInSkin` 已新增对 `OmsSkin` 内置 `MainHUDComponents.json` / `SongSelect.json` / `Playfield.json` 的回归，锁定 `Skin.LayoutInfos` 现会稳定装载三类 global target，且 mania playfield 段包含 `BarHitErrorMeter` / `ArgonAccuracyCounter` / `ArgonComboCounter` / `ArgonPerformancePointsCounter` / `ClicksPerSecondCounter`
- 这一步代表 1.1.4 里的 `Global` layout metadata 已从“资源已嵌入但未锁定”推进到“有定向 regression 约束”；当时该小节剩余 gap 收窄为 global results summary container，以及 mania 第二批余下的实际 OMS-owned Hold / HitBurst / Judgement / HUD 默认路径迁移
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **41/41** 通过；`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### 构建审计：AutoMapper GHSA 定点抑制

- `osu.Game.csproj` 已新增针对 `https://github.com/advisories/GHSA-rvv3-g6hj-g44x` 的定点 `NuGetAuditSuppress`，不再让已评估的 `AutoMapper` `NU1903` 审计告警持续污染当前 build 输出
- 当前仓库仍保留 `RealmObjectExtensions` 里的 `MaxDepth(3)` 作为循环图路径的运行时限深；之所以不直接升到 `AutoMapper` 15.1.1+，是因为 15.x 额外引入 license 要求与配置 API 破坏变更，适合作为单独迁移切片处理
- 这一步代表构建输出已不再残留既有 `NU1903` 噪音，但 `AutoMapper` 升级或彻底移除仍继续留在中优先级跟踪项中
- `dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过，当前无残留构建告警

### Phase 1.1：OmsSkin global shared transformer shell 首次落地

- 已新增共享 `OmsSkinTransformer`，作为 OMS preview 路径的外层 wrapper；当 `GlobalSkinnableContainerLookup` 的 global HUD、`SongSelect`、`Playfield` 未被更具体的 ruleset transformer 命中时，现会返回空 `DefaultSkinComponentsContainer` 作为 shared shell
- `ManiaRuleset` 与 `BmsRuleset` 的 `OmsSkin` 入口现都会先经过该 shared shell，再分别委托 `ManiaOmsSkinTransformer` 与 `BmsSkinTransformer`，因此 ruleset-specific HUD / gameplay lookup 仍继续由各自 transformer 承接
- `TestSceneOmsBuiltInSkin` 已收紧为外层 `OmsSkinTransformer` + 内层 `ManiaOmsSkinTransformer` 组合，并新增 global HUD / `SongSelect` / `Playfield` shell 断言；`BmsSkinTransformerTest` 也已新增 Oms shared shell 回归，锁定外层 `OmsSkinTransformer` + 内层 `BmsSkinTransformer` 组合
- 这一步代表 Global shared shell / shared transformer shell 已完成首轮落地；当时尚未被 regression 锁定的 global layout metadata 现已在同日后续步骤补齐，当时剩余主线收窄为 global results summary container 与 mania 第二批余下的实际 OMS-owned Hold / HitBurst / Judgement / HUD 默认路径迁移
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **40/40** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **62/62** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **441/441** 通过；`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania mixed-stage shared transformer 首次收口

- `ManiaOmsSkinTransformer` 的 non-column shared preset lookup 现已在 mixed-stage beatmap 上固定复用第一 stage 的 OMS preset，不再落回 total-columns legacy 默认值
- `TestSceneOmsBuiltInSkin` 已新增 mixed-stage judgement / HUD / bar-line 配置与运行时回归，覆盖 `ScorePosition` / `ComboPosition` / `BarLineHeight` / `BarLineColour` 以及 `DrawableManiaJudgement` / `OmsManiaComboCounter` / `DrawableBarLine` 路径；同项定向 mania 回归现为 **40/40** 通过
- 这一步代表当前已落地的 mania non-column shared config families 已完成 mixed-stage shared-transformer 收口；但组件仍继续消费 legacy judgement / combo / bar-line 语义，Global shared shell / shared transformer shell 与 mania 第二批余下的实际 OMS-owned Hold / HitBurst / Judgement / HUD 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **40/40** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania hold note body component route 首次落地

- `ManiaSkinComponents.HoldNoteBody` 现已显式路由到 `OmsHoldNoteBodyPiece`；可见 hold body 会由 `DrawableHoldNote` 内部的 `bodyPiece` 路径实际加载 OMS hold note body 组件，而不再只停留在纯 legacy body dispatch
- `TestSceneOmsBuiltInSkin` 已补 single-stage runtime hold note body load 回归，并在 transformer 断言中收紧到 `OmsHoldNoteBodyPiece`；同项定向 mania 回归现为 **37/37** 通过
- 这一步只代表 mania 第二批又落下首个 explicit hold-note-body component slice；`OmsHoldNoteBodyPiece` 仍继续消费既有 `HoldNoteBodyImage` preset、legacy `NoteBodyStyle` / `HoldNoteLightImage` / `HoldNoteLightScale`，以及 legacy hold-body light insertion / wrap-stretch / hold-break fade 语义，所以 shared shell / shared transformer，以及 mania 第二批余下的实际 Hold / HitBurst / Judgement / HUD 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **37/37** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania hold note tail component route 首次落地

- `ManiaSkinComponents.HoldNoteTail` 现已显式路由到 `OmsHoldNoteTailPiece`；`DrawableHoldNoteTail` 在 OMS preview 路径下会实际加载 OMS hold note tail 组件，而不再继续走纯 `LegacyHoldNoteTailPiece` dispatch
- `TestSceneOmsBuiltInSkin` 已补 single-stage runtime hold note tail load 回归，并在 transformer 断言中收紧到 `OmsHoldNoteTailPiece`；同项定向 mania 回归现为 **36/36** 通过
- 这一步只代表 mania 第二批又落下首个 explicit hold-note-tail component slice；`OmsHoldNoteTailPiece` 仍继续消费既有 `HoldNoteTailImage` preset 与 legacy tail inversion / note sizing 语义，所以 shared shell / shared transformer，以及 mania 第二批余下的实际 Hold body / HitBurst / Judgement / HUD 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **36/36** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania hold note head component route 首次落地

- `ManiaSkinComponents.HoldNoteHead` 现已显式路由到 `OmsHoldNoteHeadPiece`；`DrawableHoldNoteHead` 在 OMS preview 路径下会实际加载 OMS hold note head 组件，而不再继续走纯 `LegacyHoldNoteHeadPiece` dispatch
- `TestSceneOmsBuiltInSkin` 已补 single-stage runtime hold note head load 回归，并在 transformer 断言中收紧到 `OmsHoldNoteHeadPiece`；同项定向 mania 回归现为 **35/35** 通过
- 这一步只代表 mania 第二批又落下首个 explicit hold-note-head component slice；`OmsHoldNoteHeadPiece` 仍继续消费既有 `HoldNoteHeadImage` preset 与 legacy note scrolling / sizing 语义，所以 shared shell / shared transformer，以及 mania 第二批余下的实际 Hold tail/body / HitBurst / Judgement / HUD 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **35/35** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania note component route 首次落地

- `ManiaSkinComponents.Note` 现已显式路由到 `OmsNotePiece`；`DrawableNote` 在 OMS preview 路径下会实际加载 OMS note 组件，而不再继续走纯 `LegacyNotePiece` dispatch
- `TestSceneOmsBuiltInSkin` 已补 single-stage runtime note load 回归，并在 transformer 断言中收紧到 `OmsNotePiece`；同项定向 mania 回归现为 **34/34** 通过
- 这一步只代表 mania 第二批又落下首个 explicit normal-note component slice；`OmsNotePiece` 仍继续消费既有 `NoteImage` preset、`WidthForNoteHeightScale` 与 legacy note scrolling / sizing 语义，所以 shared shell / shared transformer，以及 mania 第二批余下的实际 Hold / HitBurst / Judgement / HUD 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **34/34** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania combo counter component route 首次落地

- MainHUDComponents 里的 combo 现已显式路由到 `OmsManiaComboCounter`；OMS preview 的 HUD container 会实际加载 OMS combo counter 组件，而不再继续走纯 `LegacyManiaComboCounter` dispatch
- `TestSceneOmsBuiltInSkin` 已补 single-stage runtime combo counter load 回归，并把既有 dual-stage HUD combo 回归收紧到 `OmsManiaComboCounter` 实例；同项定向 mania 回归现为 **33/33** 通过
- 这一步只代表 mania 第二批又落下首个 explicit combo counter component slice；`OmsManiaComboCounter` 仍继续消费既有 `ComboPosition` shared preset 与 legacy combo counter 语义，所以 shared shell / shared transformer，以及 mania 第二批余下的实际 Note / Hold / HitBurst / Judgement / HUD 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **33/33** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania bar-line component route 首次落地

- `ManiaSkinComponents.BarLine` 现已显式路由到 `OmsBarLine`；`DrawableBarLine` 在 OMS preview 路径下会实际加载 OMS bar line 组件，而不再继续走纯 legacy component dispatch
- `TestSceneOmsBuiltInSkin` 已补 single-stage runtime bar line load 回归，并把既有 dual-stage bar line 回归收紧到 `OmsBarLine` 实例；同项定向 mania 回归现为 **32/32** 通过
- 这一步只代表 mania 第二批又落下首个 explicit bar-line component slice；`OmsBarLine` 仍继续消费既有 `BarLineHeight` / `BarLineColour` shared preset 与 legacy bar-line 语义，所以 shared shell / shared transformer，以及 mania 第二批余下的实际 Note / Hold / HitBurst / Judgement / HUD / bar line 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **32/32** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania shared bar-line config 首次落地

- `OmsManiaBarLinePreset` 现已同时承接 `LegacyManiaSkinConfigurationLookups.BarLineHeight` / `BarLineColour` 的 uniform-stage shared lookup；single-stage 与 same-keycount dual-stage 不再落回 total-columns legacy 默认值
- `TestSceneOmsBuiltInSkin` 已补 shared bar-line config 回归与 dual-stage runtime bar line 回归，验证 `LegacyBarLine` 在 OMS preview 的 9K+9K 路径下会实际复用同一组 OMS bar-line height；同项定向 mania 回归现为 **31/31** 通过
- 这一步只代表 mania 第二批又落下首个 uniform-stage shared bar-line config slice；mixed-stage 的 non-column config、shared transformer 收口与实际 OMS-owned bar line 组件路径仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **31/31** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania shared judgement combo-position 首次落地

- `OmsManiaJudgementPositionPreset` 现已同时承接 `LegacyManiaSkinConfigurationLookups.ScorePosition` / `ComboPosition` 的 uniform-stage shared lookup；5K+5K dual-stage 不再落回 total-columns legacy 默认值
- `TestSceneOmsBuiltInSkin` 已补 shared judgement / HUD position config 回归与 dual-stage HUD combo 回归，验证 `LegacyManiaComboCounter` 在 OMS preview 的 MainHUDComponents 路径下会实际复用同一组 OMS combo-position preset；同项定向 mania 回归现为 **29/29** 通过
- 这一步只代表 mania 第二批把 uniform-stage shared judgement-position slice 从 score-position 扩展到 combo-position；mixed-stage 的 non-column judgement / HUD positioning、shared transformer 收口与 legacy judgement animation 语义迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **29/29** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania shared judgement score-position 首次落地

- 已新增 `OmsManiaJudgementPositionPreset`，并让 `ManiaOmsSkinTransformer.GetConfig()` 为 `LegacyManiaSkinConfigurationLookups.ScorePosition` 在 uniform-stage beatmap 上返回显式 OMS shared score-position；5K+5K dual-stage 不再落回 total-columns legacy 默认值
- `TestSceneOmsBuiltInSkin` 已补 dual-stage judgement 位置回归，验证 `DrawableManiaJudgement` 在 OMS preview 的 5K+5K 路径下会实际复用同一组 OMS score-position preset；同项定向 mania 回归现为 **27/27** 通过
- 这一步只代表 mania 第二批又落下首个 shared judgement score-position slice；mixed-stage 的 non-column judgement / HUD positioning、shared transformer 收口与 legacy judgement animation 语义迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **27/27** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania judgement piece route 首次落地

- 已新增 `OmsManiaJudgementPiece`，并让 `ManiaOmsSkinTransformer.GetDrawableComponent()` 为 `SkinComponentLookup<HitResult>` 返回显式 OMS judgement piece；`DrawableManiaJudgement` 不再只通过 base legacy transformer 隐式拿到 judgement drawable
- `TestSceneOmsBuiltInSkin` 已补显式 judgement piece 路由断言与实际加载回归，验证 transformer 会返回 `OmsManiaJudgementPiece`，且 `DrawableManiaJudgement` 会在 OMS preview 路径下实际加载该组件；同项定向 mania 回归现为 **26/26** 通过
- 这一步只代表 mania 第二批又落下首个 explicit judgement piece slice；当前 `OmsManiaJudgementPiece` 仍继续消费既有 `Hit300g` / `Hit300` / `Hit200` / `Hit100` / `Hit50` / `Hit0` preset 与 legacy judgement positioning/animation 语义，所以 shared shell / shared transformer，以及 mania 第二批余下的实际 HitBurst / Judgement / HUD / note-hold 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **26/26** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania hit explosion component route 首次落地

- 已新增 `OmsHitExplosion`，并让 `ManiaOmsSkinTransformer.GetDrawableComponent()` 为 `ManiaSkinComponents.HitExplosion` 返回显式 OMS 组件；`PoolableHitExplosion` 不再只通过 base legacy transformer 隐式拿到 hit explosion drawable
- `TestSceneOmsBuiltInSkin` 已补显式 hit explosion 组件路由断言与实际加载回归，验证 transformer 会返回 `OmsHitExplosion`，且 `PoolableHitExplosion` 会在 OMS preview 路径下实际加载该组件；同项定向 mania 回归现为 **25/25** 通过
- 这一步只代表 mania 第二批又落下首个 explicit hitburst component slice；当前 `OmsHitExplosion` 仍继续消费上一轮已收口的 `ExplosionImage` / `ExplosionScale` preset，所以 shared shell / shared transformer，以及 mania 第二批余下的实际 HitBurst / Judgement / HUD / note-hold 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **25/25** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania hitburst config preset 首次落地

- 已新增 `OmsManiaHitExplosionPreset`，并让 `ManiaOmsSkinTransformer.GetConfig()` 为 `ExplosionImage` / `ExplosionScale` 返回 OMS-owned stage-local hitburst config preset；legacy `LegacyHitExplosion` 的这批 hitburst 配置 lookup 不再继续只依赖 absolute-column fallback 与隐式列宽换算
- `TestSceneOmsBuiltInSkin` 已补 hitburst config 回归与 mixed-stage 5K+8K hitburst config 回归，验证 transformer 会稳定返回 OMS-owned 的 `ExplosionImage` / `ExplosionScale` 配置，且 mixed-stage beatmap 会按各自 stage keycount 取独立 hitburst config preset；同项定向 mania 回归现为 **24/24** 通过
- 这一步只代表 mania 第二批又落下首个 stage-local hitburst config slice；shared shell / shared transformer，以及 mania 第二批余下的实际 HitBurst / Judgement / HUD / note-hold 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **24/24** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania judgement asset preset 首次落地

- 已新增 `OmsManiaJudgementAssetPreset`，并让 `ManiaOmsSkinTransformer.GetConfig()` 为 `Hit300g` / `Hit300` / `Hit200` / `Hit100` / `Hit50` / `Hit0` 返回 OMS-owned shared judgement asset preset；legacy `ManiaLegacySkinTransformer` 的这批 judgement 资源 lookup 不再继续只依赖默认文件名 fallback
- `TestSceneOmsBuiltInSkin` 已补 judgement asset 回归与 mixed-stage 5K+9K shared judgement asset 回归，验证 transformer 会稳定返回 OMS-owned 的 judgement 资源名，且 mixed-stage beatmap 会持续拿到同一组 shared judgement asset preset；同项定向 mania 回归现为 **22/22** 通过
- 这一步代表 mania 第二批也已落下首个 shared judgement asset slice；shared shell / shared transformer，以及 mania 第二批余下的 HitBurst / HUD 与实际 OMS-owned judgement / note-hold 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **22/22** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania note/hold asset preset 首次落地

- 已新增 `OmsManiaNoteAssetPreset`，并让 `ManiaOmsSkinTransformer.GetConfig()` 为 `NoteImage` / `HoldNoteHeadImage` / `HoldNoteTailImage` / `HoldNoteBodyImage` 返回 OMS-owned stage-local note/hold asset preset；legacy `LegacyNotePiece` / `LegacyHoldNoteHeadPiece` / `LegacyHoldNoteTailPiece` / `LegacyBodyPiece` 的这批 note/hold asset lookup 不再继续只依赖 absolute-column legacy config fallback
- `TestSceneOmsBuiltInSkin` 已补 note/hold asset 回归与 mixed-stage 5K+9K note/hold asset 回归，验证 transformer 会稳定返回 OMS-owned 的 note/head/tail/body 配置，且 mixed-stage beatmap 会按各自 stage keycount 取独立 note/hold asset preset；同项定向 mania 回归现为 **20/20** 通过
- 这一步只代表 mania 第二批的首个 stage-local note/hold asset slice 已落下一刀；shared shell / shared transformer，以及 mania 第二批余下的 HitBurst / Judgement / HUD 与实际 OMS-owned note/hold 默认路径迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **20/20** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania key-asset preset 首次落地

- 已新增 `OmsManiaKeyAssetPreset`，并让 `ManiaOmsSkinTransformer.GetConfig()` 为 `KeyImage` / `KeyImageDown` 返回 OMS-owned stage-local key asset preset；`OmsKeyArea` 的这批 key-image lookup 不再继续直接依赖 legacy mania config fallback
- `TestSceneOmsBuiltInSkin` 已补 key-image 回归与 mixed-stage 5K+8K key-image 回归，验证 transformer 会稳定返回 OMS-owned 的 key-image 配置，且 mixed-stage beatmap 会按各自 stage keycount 取独立 key-image preset；同项定向 mania 回归现为 **18/18** 通过
- 这一步代表 mania 第一批 shell 的 key-image lookup 也已落下一刀；shared shell / shared transformer，以及 mania Note / Hold / HUD 迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **18/18** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania shell colour preset 首次落地

- 已新增 `OmsManiaColumnColourPreset`，并让 `ManiaOmsSkinTransformer.GetConfig()` 为 `ColumnLineColour` / `JudgementLineColour` / `ColumnBackgroundColour` / `ColumnLightColour` 返回 OMS-owned shell colour preset；`OmsColumnBackground` / `OmsHitTarget` 的这批 colour lookup 不再继续直接依赖 legacy mania config fallback
- `TestSceneOmsBuiltInSkin` 已补 shell colour 回归与 mixed-stage 8K+9K colour 回归，验证 transformer 会稳定返回 OMS-owned 的 shell colour 配置，且 mixed-stage beatmap 会按各自 stage keycount 取独立 colour preset；同项定向 mania 回归现为 **16/16** 通过
- 这一步只代表 mania 第一批 shell 的首批 colour lookup 也已落下一刀；shared shell / shared transformer、剩余 shell key-image lookup，以及 mania Note / Hold / HUD 迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **16/16** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania shared shell asset preset 首次落地

- 已新增 `OmsManiaShellAssetPreset`，并让 `ManiaOmsSkinTransformer.GetConfig()` 为 `LeftStageImage` / `RightStageImage` / `BottomStageImage` / `HitTargetImage` / `LightImage` / `KeysUnderNotes` 返回 OMS-owned shared shell asset preset；`OmsStageBackground` / `OmsStageForeground` / `OmsHitTarget` / `OmsKeyArea` 的这批共享 lookup 不再继续依赖 legacy mania config fallback
- `TestSceneOmsBuiltInSkin` 已补 shared shell asset 回归与 mixed-stage 7K+6K 共享 asset 回归，验证 transformer 会稳定返回 OMS-owned 的 stage / hit target / light 资源名，且 mixed-stage beatmap 仍能拿到同一组共享 asset preset；同项定向 mania 回归现为 **14/14** 通过
- 这一步只代表 mania 第一批 shell 的 shared asset lookup 也已落下一刀；shared shell / shared transformer、剩余 shell key-image/color lookup，以及 mania Note / Hold / HUD 迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **14/14** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

## 2026-04-07

### Phase 1.1：OmsSkin mania stage-local shell behaviour preset 首次落地

- 已新增 `OmsManiaShellPreset`，并让 `ManiaOmsSkinTransformer.GetConfig()` 为 `LeftLineWidth` / `RightLineWidth` / `LightPosition` / `ShowJudgementLine` / `LightFramePerSecond` 提供首批 stage-local OMS shell behaviour preset；`OmsHitTarget` 对 `ShowJudgementLine` / `LightFramePerSecond` 也已改为按列请求，使 mixed-stage beatmap 会按各 stage keycount 取值
- `TestSceneOmsBuiltInSkin` 已补 shell config 回归、mixed-stage 7K+6K 回归与 8K edge line width 回归，验证 transformer 会返回预期 behaviour 值，且第二个 stage 会按自身 keycount 使用独立 light position；同项定向 mania 回归现为 **12/12** 通过
- 这一步只代表 mania 第一批 shell 的 stage-local behaviour bridge 也已落下第一刀；shared shell / shared transformer、剩余 shell asset/color lookup，以及 mania Note / Hold / HUD 迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **12/12** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania stage-local layout preset 首次落地

- 已新增 `OmsManiaLayoutPreset`，并让 `ManiaOmsSkinTransformer.GetConfig()` 为 `HitPosition` / `StagePadding` / `ColumnWidth` / `ColumnSpacing` 提供首批 stage-local OMS layout preset，不再按 `beatmap.TotalColumns` 整体取值，也不再只依赖 legacy mania config fallback
- `TestSceneOmsBuiltInSkin` 已补 layout 回归、完整 5K `Stage` 宿主回归，以及 dual-stage 5K+5K 回归，验证 transformer 会返回预期 layout 值，完整 `Stage` 会实际使用这些 preset，且第二个 stage 会按自身 keycount 重复使用同一组 preset；同项定向 mania 回归现为 **9/9** 通过
- 这一步只代表 mania 第一批的容器级 layout bridge 已落下第一刀；remaining shell config lookup、shared shell / shared transformer，以及 mania Note / Hold / HUD 迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **9/9** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania shell 首批 OMS 组件落地

- `ManiaOmsSkinTransformer` 现会为 `StageBackground` / `StageForeground` / `ColumnBackground` / `KeyArea` / `HitTarget` 返回 `OmsStageBackground` / `OmsStageForeground` / `OmsColumnBackground` / `OmsKeyArea` / `OmsHitTarget`，不再继续直接复用 legacy shell 组件
- `TestSceneOmsBuiltInSkin` 已补 runtime load 回归，除组件类型断言外，还验证上述 OMS shell 组件可在最小依赖宿主下实际加载；该阶段首次落地时定向 mania 回归为 **5/5** 通过
- 这一刀只代表 mania 第一批已落下首个 OMS-owned shell component slice；`Stage` / `Column` / `ColumnFlow` / `ColumnHitObjectArea` 容器级收口、shared shell / shared transformer，以及 mania Note / Hold / HUD 迁移仍待后续推进
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **5/5** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OmsSkin mania 显式 transformer 入口首次落地

- `ManiaRuleset.CreateSkinTransformer()` 现会为 `OmsSkin` 显式返回 `ManiaOmsSkinTransformer`，不再继续把 OMS built-in preview 入口隐式混在 generic `LegacySkin` catch-all 分支里
- 当前已验证 `StageBackground` / `ColumnBackground` / `KeyArea` / `HitTarget` 候选路径可经该 OMS mania 入口提供；这代表 mania 第一批已从“未开始”推进到“显式入口已接通”，但整体仍主要复用 legacy-derived candidate assets 与配置语义，尚未完成 OMS-owned 默认层收口
- `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **4/4** 通过；`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：OMS built-in skin host / provider 骨架首次落地

- 已新增受保护的 `OmsSkin` preview 选择项，并把 `SKIN/SimpleTou-Lazer` 候选包资源嵌入 `osu.Game`，作为 OMS built-in host / provider 的首个内置 resource root
- `SkinManager` 现会注册、枚举并允许通过配置切换到该 OMS 入口；`SkinnableSprite` 也已把 `OmsSkin` 视作可用的 built-in 候选来源之一
- 新增 `TestSceneOmsBuiltInSkin` 回归，验证 OMS 入口可选取、受保护，并能从内置资源根取到 mania stage / key 纹理；同日 `dotnet test .\osu.Game.Rulesets.Mania.Tests\osu.Game.Rulesets.Mania.Tests.csproj --no-restore --filter "FullyQualifiedName~TestSceneOmsBuiltInSkin"` **3/3** 通过，`dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsSkinTransformerTest"` **61/61** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过
- 这一步只代表默认皮肤包 host / provider / resource root 已起骨架；shared shell / shared transformer 与 mania OMS-owned 默认路径仍待后续推进

### Phase 1.1：BMS note / hold 默认层第七个 OMS-owned slice 落地

- 已新增 `DefaultBmsNoteDisplay` / `DefaultBmsLongNoteHeadDisplay` / `DefaultBmsLongNoteBodyDisplay` / `DefaultBmsLongNoteTailDisplay`，并把 `Note` / `LongNoteHead` / `LongNoteBody` / `LongNoteTail` 的无皮肤默认路径切到 `BmsDefaultPlayfieldPalette`
- `BmsSkinTransformer` 不再用 `BmsTemporarySkinPalette` 生成 note / hold fallback；`BmsSkinTransformerTest` 同步补上 normal / scratch note、head / body / tail 的 concrete fallback 与 wrapped-skin 回归，`BmsTemporarySkinPalette` 也已从 live BMS fallback 链路删除
- 直接受影响 `BmsSkinTransformerTest` **61/61** 通过；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj` **440/440** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过
- BMS 默认层当前已完成 gameplay HUD、results summary / clear lamp、results gauge history、Song Select note distribution、playfield metadata / accent surfaces、playfield shell surfaces，以及 note / hold visuals 共七批 OMS-owned 默认层切片

### Phase 1.1：BMS playfield shell 默认层第六个 OMS-owned slice 落地

- `BmsDefaultPlayfieldPalette` 现已进一步承接 playfield shell surfaces：新增的 `DefaultBmsPlayfieldBackdropDisplay` / `DefaultBmsPlayfieldBaseplateDisplay` / `DefaultBmsLaneBackgroundDisplay` / `DefaultBmsLaneDividerDisplay` 已把 `Backdrop` / `Baseplate` / lane `Background` / `Divider` 的无皮肤默认路径切到 BMS-owned token
- `BmsSkinTransformer` 不再用 `BmsTemporarySkinPalette` 生成 playfield backdrop / baseplate 与 lane background / divider fallback；`BmsSkinTransformerTest` 同步补上 baseplate / divider fallback、scratch shell 路径，以及自定义 wrapped-skin 覆盖回归
- 直接受影响 `BmsSkinTransformerTest` + `TestSceneBmsLaneCover` + `TestSceneBmsHitTargetState` + `BmsDrawableRulesetTest` **114/114** 通过；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj` **433/433** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过
- 当前 BMS 默认层剩余过渡面已收敛到 note / hold visuals

### Phase 1.1：BMS playfield metadata / accent 默认层第五个 OMS-owned slice 落地

- 已新增独立 `BmsDefaultPlayfieldPalette`，并把 `DefaultBmsBackgroundLayerDisplay`、`DefaultBmsLaneCoverDisplay`、`DefaultBmsHitTargetDisplay` 与默认 `BarLine` fallback 的 metadata shell、fill / focus、bar / line / glow、major / minor 语义切到 BMS-owned token；`StaticBackgroundLayer`、`LaneCover`、`HitTarget` 与 `BarLine` 默认路径不再继续复用 `BmsTemporarySkinPalette`
- `BmsSkinTransformer` 现也会用 `BmsDefaultPlayfieldPalette` 生成 major / minor bar line 的默认 fallback；`BmsSkinTransformerTest` 同步补强为 concrete fallback 断言，并新增 `BarLine` 默认 fallback 回归
- `DefaultBmsBackgroundLayerDisplay` 里先前未接线的 `labelContainer` 已一并修正，metadata 文案与缺失态现在会跟随默认壳层正常更新
- 直接受影响 `BmsSkinTransformerTest` + `TestSceneBmsLaneCover` + `TestSceneBmsHitTargetState` + `BmsDrawableRulesetTest` **105/105** 通过；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj` **427/427** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过
- 当前 BMS 默认层剩余过渡面已收敛到 playfield backdrop / baseplate、lane background / divider 与 note / hold visuals

### Phase 1.1：BMS Song Select note distribution 默认层第四个 OMS-owned slice 落地

- `DefaultBmsNoteDistributionPanelDisplay` / `DefaultBmsNoteDistributionDisplay` 现已切到 results-style panel shell 与 BMS-owned 图表配色；Song Select 默认 note distribution 不再继续依赖 `BmsTemporarySkinPalette`
- 默认 note distribution panel 现会沿用统一的标题/状态色、卡片边框与 accent shell；内部 legend 与柱状图也已切到 BMS-owned note-distribution colours，不再把 Song Select 面板建立在临时 feedback HUD 表面上
- 已更新 `BmsSkinTransformerTest`，锁定 `NoteDistribution` / `NoteDistributionPanel` 的默认 fallback 类型分别为 `DefaultBmsNoteDistributionDisplay` / `DefaultBmsNoteDistributionPanelDisplay`；直接受影响 `BmsSkinTransformerTest` **47/47** 通过，完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj` **426/426** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过
- 当前 BMS 默认层余量已进一步收敛到 BMS-only accent

### Phase 1.1：BMS results gauge history 默认层第三个 OMS-owned slice 落地

- `BmsGaugeColours` 现已统一复用 `BmsDefaultHudPalette` 的 gauge colours；results 页默认 gauge history 不再继续依赖 `BmsTemporarySkinPalette`
- `DefaultBmsGaugeHistoryPanelDisplay` / `DefaultBmsGaugeHistoryDisplay` 与默认时间线行、plot 现已切到 results-style panel shell、BMS-owned 标题/状态色与 threshold marker；无外部皮肤时的 results gauge history 不再只是临时 feedback 图表
- 已更新 `BmsSkinTransformerTest`，锁定 `GaugeHistory` / `GaugeHistoryPanel` 的默认 fallback 类型分别为 `DefaultBmsGaugeHistoryDisplay` / `DefaultBmsGaugeHistoryPanelDisplay`；直接受影响 `BmsSkinTransformerTest` + `BmsRulesetStatisticsTest` **47/47** 通过，完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj` **426/426** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过
- 这一批之后，BMS 默认层余量曾进一步收敛到 Song Select 与 BMS-only accent

### Phase 1.1：BMS results summary + clear lamp 默认层第二个 OMS-owned slice 落地

- 已新增独立 `BmsDefaultResultsPalette`，并把 `DefaultBmsResultsSummaryPanelDisplay` / `DefaultBmsResultsSummaryDisplay` / `DefaultBmsClearLampDisplay` 的 no-custom-skin 默认路径切到专用 results token；results 页的 `BMS Statistics` 不再继续依赖临时反馈配色
- 默认 results summary 现改为使用 BMS-owned 统计卡片，而不再依赖通用 `SimpleStatisticTable`；clear lamp badge 与 summary panel 会共享 clear-lamp accent 语义，保证无外部皮肤时的 results 视觉语言开始与 gameplay HUD 对齐
- 已更新 `BmsSkinTransformerTest`，锁定 `ClearLamp` / `ResultsSummary` / `ResultsSummaryPanel` 的默认 fallback 类型分别为 `DefaultBmsClearLampDisplay` / `DefaultBmsResultsSummaryDisplay` / `DefaultBmsResultsSummaryPanelDisplay`；直接受影响测试 **46/46** 通过，完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj` **426/426** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：BMS gameplay HUD 默认层首个 OMS-owned slice 落地

- `BmsSkinTransformer` 的 `ComboCounter` 默认 fallback 现已从 upstream `DefaultComboCounter` 切到 `BmsComboCounter`；无外部皮肤时，BMS gameplay HUD 不再继续把 combo 默认实现建立在上游默认 HUD 组件上
- 已新增独立 `BmsDefaultHudPalette`，并把 `DefaultBmsHudLayoutDisplay` / `BmsGaugeBar` 的 no-custom-skin 默认路径切到专用 BMS HUD token；因此 gameplay HUD 终于开始从“皮肤加载失败时的反馈层”往正式 OMS-owned 默认层承接
- 已更新 `BmsSkinTransformerTest`，锁定 gameplay HUD 组装出的默认 combo 类型为 `BmsComboCounter`；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj` **426/426** 通过，本轮直接受影响测试 **47/47** 通过，`dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过

### Phase 1.1：BMS playfield size config bridge 首次接通

- `BmsRulesetConfigManager` 与 `BmsSettingsSubsection` 现已新增 `Playfield Width` / `Playfield Height` 配置项，并以 `Auto` 语义保留原有按 lane-count 推导的默认尺寸；`BmsPlayfieldLayoutProfile.CreateDefault()` 也已支持 playfield size override，不再把宽高固定锁死在 profile 默认值上
- loaded `BmsPlayfield` 的 runtime layout bridge 现会一并读取 playfield width / height，并在 bindable 仍处于默认值时回退到当前 `LayoutProfile` 的既有尺寸；这避免了全局固定 config default 把 7K / 14K 的默认宽度错误拉成同一个值，同时让 playfield size 正式进入同一条 OMS-owned 重布局链路
- 已扩展 `BmsRulesetConfigurationTest` 与 `TestSceneBmsPlayfieldLayoutConfig`，覆盖 playfield size 配置值、实际 lane span / lane height 的运行时生效；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj` **426/426** 通过，本轮直接受影响测试 **22/22** 通过

## 2026-04-06

### Phase 1.1：BMS hit target internal geometry config bridge 首次接通

- `BmsRulesetConfigManager` 与 `BmsSettingsSubsection` 现已新增 `Hit Target Bar Height` / `Hit Target Line Height` / `Hit Target Glow Radius` 配置项，`BmsPlayfieldLayoutProfile.CreateDefault()` 也已支持这三项内部几何 override；default receptor 的 bar / line / glow 不再只依赖 profile 默认常量
- `BmsHitTarget` 现会保存当前 layout profile，并通过新的 `IBmsHitTargetLayoutDisplay` 把 runtime profile 变更继续下推给当前 display；这不仅让 loaded `BmsPlayfield` 的重布局能更新 default receptor 内部几何，也避免 skin reload 时重新落回旧 profile 快照
- 已扩展 `BmsRulesetConfigurationTest`、`BmsLaneLayoutTest` 与 `TestSceneBmsPlayfieldLayoutConfig`，覆盖默认配置值、bar/line/glow 的运行时生效，以及 focus edge 跟随 line height 的刷新；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj` **424/424** 通过，本轮直接受影响测试 **20/20** 通过

### Phase 1.1：BMS lane width / spacing config bridge 首次接通

- `BmsRulesetConfigManager` 与 `BmsSettingsSubsection` 现已新增 `Lane Width` / `Lane Spacing` 配置项，`BmsPlayfieldLayoutProfile.CreateDefault()` 的 normal-lane width / spacing override 终于也正式接上 ruleset config bridge；normal lane 不再只依赖 profile 默认常量
- `BmsPlayfield` 的 loaded runtime layout bridge 现会与既有 scratch width / spacing、hit target height / vertical offset、bar line height 一起读取 `Lane Width` / `Lane Spacing`，并在需要时重算 `BmsLaneLayout`；因此 regular key lanes 的宽度、相邻 gap 与 total span 现在也能沿同一条 OMS-owned runtime layout 链更新
- 已扩展 `BmsRulesetConfigurationTest`、`BmsLaneLayoutTest` 与 `TestSceneBmsPlayfieldLayoutConfig`，覆盖默认配置值、normal lane width / spacing 的运行时生效；同时测试 scene 的 layout setup 现会显式重置相关 ruleset config，避免跨用例串值污染后续 layout 断言；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore --no-build` **421/421** 通过，本轮直接受影响测试 **17/17** 通过

### Phase 1.1：BMS receptor vertical-position config bridge 首次接通

- `BmsRulesetConfigManager` 与 `BmsSettingsSubsection` 现已新增 `Hit Target Vertical Offset` 配置项，`BmsPlayfieldLayoutProfile.CreateDefault()` 也已支持 receptor vertical offset override；BMS receptor vertical position 不再只依赖 lane 底边硬编码
- 已新增 `BmsHitObjectArea`，把每条 lane 的 `ScrollingHitObjectContainer` 与 `BmsHitTarget` 放进同一 hit-position 容器，并按当前 scroll direction 把 top / bottom padding 落到真实 scrolling container 上；因此这次移动的不只是 receptor 视觉，而是实际 hit line 与可见 scroll length
- `DrawableBmsRuleset` 现会消费 playfield 回传的有效 scroll-length ratio，并在 `ScrollSpeed` 对应的 `TimeRange` 上做同倍率缩放；receptor vertical offset 不会悄悄退化成另一条“变相变速”路径
- 已扩展 `BmsRulesetConfigurationTest` 与 `TestSceneBmsPlayfieldLayoutConfig`，覆盖默认配置值、正向/反向 scroll direction 下的 receptor vertical offset，以及 scrolling container edge 与 receptor 的对齐；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **419/419** 通过，本轮直接受影响测试 **12/12** 通过

### Phase 1.1：BMS hit target / bar line vertical-size config bridge 首次接通

- `BmsRulesetConfigManager` 与 `BmsSettingsSubsection` 现已新增 `Hit Target Height` / `Bar Line Height` 配置项，`BmsPlayfieldLayoutProfile.CreateDefault()` 也已支持这两个垂直尺寸 override；hit target 与 measure bar line 不再只依赖默认 profile 常量
- `BmsPlayfield` 的 loaded runtime layout bridge 现会与 scratch width ratio / spacing 一起读取 `Hit Target Height` / `Bar Line Height`，并通过轻量 lane apply-layout 路径刷新现有 `BmsHitTarget` 与 `DrawableBmsBarLine`；因此无需回退到 `DrawableBmsRuleset.CreatePlayfield()` 构造期提前读 config，也能让已创建的 lane 装饰组件跟随真实 ruleset config cache 更新
- 已扩展 `BmsRulesetConfigurationTest`、`BmsLaneLayoutTest`、`BmsDrawableRulesetTest` 与 `TestSceneBmsPlayfieldLayoutConfig`，覆盖默认配置值、运行时 hit target 高度刷新与 bar line 高度刷新；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **417/417** 通过，本轮直接受影响测试 **60/60** 通过

### Phase 1.1：BMS scratch width ratio config bridge 首次接通

- `BmsRulesetConfigManager` 与 `BmsSettingsSubsection` 现已新增 `Scratch Lane Width Ratio` 配置项；scratch lane width 不再只依赖 `BmsPlayfieldLayoutProfile` 的默认 `1.25x` 常量，而是和 scratch spacing 一样进入 ruleset config bridge
- `BmsPlayfield` 现会在 load 阶段同时读取 scratch width ratio 与 scratch spacing，并在需要时重算 `BmsLaneLayout`；因此 scratch lane 的宽度、相邻 spacing 与 total span 现在都能沿同一条 runtime layout bridge 更新，而不必在 `DrawableBmsRuleset.CreatePlayfield()` 构造期提前读取 config
- 已扩展 `TestSceneBmsPlayfieldLayoutConfig` 与 `BmsRulesetConfigurationTest`，覆盖非默认 width ratio、生效后的 lane width 比例，以及 `1.0x` 时 scratch lane 回落到普通宽度的运行时表现；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **415/415** 通过，本轮直接受影响测试 **59/59** 通过

### Phase 1.1：BMS scratch spacing config bridge 首次接通

- `BmsRulesetConfigManager` 与 `BmsSettingsSubsection` 现已新增 `Scratch Lane Spacing` 配置项；scratch spacing 不再只存在于 `BmsPlayfieldLayoutProfile` 的默认常量里，而是开始拥有第一条可持久化、可验证的 ruleset config bridge
- `BmsLaneLayout` 仍负责计算 `RelativeSpacingBefore` 与 total span，但 `BmsPlayfield` 现在会在 load 阶段从真实 ruleset config cache 读取 scratch spacing，并在需要时对 lane 几何做一次重布局；这样避免了 `DrawableBmsRuleset.CreatePlayfield()` 构造期过早读取 config，同时让运行时 playfield 能实际消费配置值
- 已补充 `TestSceneBmsPlayfieldLayoutConfig`，并扩展 `BmsRulesetConfigurationTest`，覆盖默认配置值、非零 spacing 生效以及零 spacing 取消 gap 的运行时表现；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **413/413** 通过，本轮直接受影响测试 **57/57** 通过

### Phase 1.1：BMS scratch spacing 正式接通

- `BmsPlayfieldLayoutProfile` 现已新增 normal / scratch lane spacing 契约，`BmsLaneLayout` 也开始按相邻车道类型计算 `RelativeSpacingBefore`；scratch 相邻车道不再默认全贴边排列，scratch width 与 scratch spacing 终于开始沿同一条 OMS-owned layout metadata 链路收口
- `BmsLaneLayout.TotalRelativeWidth` 现会把 spacing 一并计入 total span，因此 `BmsPlayfield` 的 lane `X` / `Width` 归一化定位可以直接消费 scratch gap；regular key-key 仍保持贴合，scratch-key / key-scratch 过渡则会留下正式的相对间距
- 已扩充 `BmsLaneLayoutTest` 与 `BmsDrawableRulesetTest`，覆盖 7K / 14K scratch spacing 语义与 playfield 运行时定位；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **410/410** 通过，本轮直接受影响测试 **51/51** 通过

### Phase 1.1：BMS receptor state 正式契约首次接通

- `BmsHitTarget` 现已拥有 `IsPressed` / `IsFocused` 两个正式状态 bindable，并新增 `IBmsHitTargetDisplay` 作为 receptor display contract；默认 `DefaultBmsHitTargetDisplay` 已可消费 pressed / focused state，不必再等到后续默认皮肤迁移时才临时拼接 down-state / focus-state 语义
- `BmsLane` 现会从 `BmsInputManager.KeyBindingContainer.PressedActions` 同步当前 lane action 的 pressed state，因此 receptor pressed state 不再依赖某个具体 drawable 是否消费了按键事件；regular lane 与 scratch lane 的 hit target 都能沿同一条 runtime 状态链更新
- 已新增 `TestSceneBmsHitTargetState`，并与 `BmsDrawableRulesetTest`、`BmsSkinTransformerTest` 一起覆盖 receptor state 视觉契约与输入同步；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **410/410** 通过，本轮直接受影响测试 **100/100** 通过

### Phase 1.1：BMS playfield adjustment/config bridge 首次接通

- `DrawableBmsRuleset` 现已切入专用 `BmsPlayfieldAdjustmentContainer`，把 BMS playfield 从通用 `PlayfieldAdjustmentContainer` 升级为 ruleset 自身可控的 adjustment/scaling 入口；当前默认实现支持整体缩放与横向偏移，为后续 receptor state、scratch spacing 与更完整的 layout bridge 继续收口预留了专用落点
- `BmsRulesetConfigManager` 与 `BmsSettingsSubsection` 现已新增 `Playfield Scale` / `Playfield Horizontal Offset` 两个配置项；BMS playfield adjustment/scaling 终于不再完全写死在 runtime 结构里，而是拥有第一条可验证、可持久化、可扩展的 ruleset config bridge
- 已补充 `BmsPlayfieldAdjustmentContainerTest`、扩展 `BmsRulesetConfigurationTest` 与 `BmsDrawableRulesetTest`，覆盖默认配置值、专用 adjustment container 挂接，以及 adjustment bindable 变更后的缩放 / 偏移行为；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **405/405** 通过，本轮直接受影响测试 **50/50** 通过

### Phase 1.1：BMS playfield abstraction gate 首步落地

- 已新增共享 `BmsPlayfieldLayoutProfile`，将 BMS playfield 的默认几何参数统一收口；`BmsLaneLayout`、`BmsPlayfield`、`BmsHitTarget` 与 `DrawableBmsBarLine` 现都从同一 profile 读取 lane 宽度、playfield 尺寸、hit target 高度与 bar line 高度，不再继续散落硬编码
- `BmsLaneLayout` 现会随 keymode / lane count 一起生成默认 layout profile；`BmsPlayfield`、`BmsLane` 与 scratch lane 也已改为沿用同一份 profile 构造默认 lane 装饰组件，为后续 receptor state、spacing、playfield adjustment/scaling 与配置桥接继续收口预留统一入口
- 已补充 `BmsLaneLayoutTest` 与 `BmsDrawableRulesetTest` 的几何回归覆盖，验证默认 profile、lane 宽度映射、hit target 高度与 bar line 高度的一致性；完整 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` **402/402** 通过，本轮直接受影响测试 **48/48** 通过

### 文档治理：Phase 1.1 执行顺序、候选包语义与 release gate 收口

- 已把 `DEVELOPMENT_PLAN.md`、`DEVELOPMENT_STATUS.md`、`README.md`、`SKINNING.md`、`RELEASE.md` 与 `OMS_COPILOT.md` 统一到同一套口径：Phase 1.1 明确按“共享边界 / 宿主骨架 → BMS playfield abstraction gate → BMS 默认层 → mania OMS-owned 迁移 → partial override / 上游默认皮肤移除 / release gate”推进，不再保留 mania/BMS 双线并行复刻的模糊表述
- 已把 `SKIN/SimpleTou-Lazer` 的文档语义统一收口为“OMS 内置皮肤候选基线 / mania 侧基础与视觉参考”；文档层面不再允许把它提前描述成“已完成的 OMS 默认皮肤”
- 已把 BMS 当前真实结构性缺口显式写入权威文档：虽然 drawable lookup 已覆盖多数组件，但 `BmsLaneLayout` / `BmsPlayfield` / `BmsHitTarget` 仍缺少 config-driven playfield 几何层，因此 faithful 的视觉复刻必须先补抽象、后补样式
- 本次为**文档治理与计划收口**，未改动运行时代码；最近一次已验证结论仍为 `dotnet build osu.Desktop` 通过、`dotnet test osu.Game.Rulesets.Bms.Tests` **400/400** 通过

### Phase 1.1：BMS 第三批 Gameplay HudLayout 接入 formal skinization

- 已把 `HudLayout` 纳入 `BmsSkinComponents` 正式契约；`BmsSkinTransformer` 现支持该组件的 ruleset 级 fallback / override，要求皮肤实现 `IBmsHudLayoutDisplay`
- `BmsSkinTransformer` 现在会把 BMS `MainHUDComponents` 路由到外层 `HudLayout`：保留 wrapped HUD 内容，再注入 `GaugeBar` 与 `ComboCounter`；因此皮肤既可整体替换 gameplay HUD 布局，也可继续单独替换 gauge / combo 本体
- 默认 `DefaultBmsHudLayoutDisplay` 承担当前 fallback HUD 布局，负责沿用现有 gauge / combo 定位、抑制重复 combo counter，并继续为 `ISerialisableDrawable` 固定 anchor，避免破坏现有 HUD 保存链路
- 已扩充 `BmsSkinTransformerTest`，覆盖 `HudLayout` 的默认 fallback、自定义 override 与 ruleset HUD 集成；聚焦 `BmsSkinTransformerTest` **47/47** 通过，全量 `osu.Game.Rulesets.Bms.Tests` **400/400** 通过
- `dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过；当前仍仅有既有 `AutoMapper` `NU1903` 告警
- `README.md`、`SKINNING.md`、`DEVELOPMENT_STATUS.md` 与仓库记忆已同步更新，gameplay HUD 外层布局当前真实落地状态现与文档一致

### Phase 1.1：BMS 第三批 Results ResultsSummaryPanel 接入 formal skinization

- 已把 `ResultsSummaryPanel` 纳入 `BmsSkinComponents` 正式契约；`BmsSkinTransformer` 现支持该组件的 ruleset 级 fallback / override，要求皮肤实现 `IBmsResultsSummaryPanelDisplay`
- `BmsRuleset.CreateStatisticsForScore()` 的 summary item 现改为返回无标题 `StatisticItem`，由 `SkinnableBmsResultsSummaryPanelDisplay` 自己承载 `BMS Statistics` 标题、空态与内层 `SkinnableBmsResultsSummaryDisplay`；因此皮肤既可整体替换 results summary 面板，也可只替换内部 summary 内容
- 已扩充 `BmsSkinTransformerTest` 与 `BmsRulesetStatisticsTest`，覆盖 `ResultsSummaryPanel` 的默认 fallback / 自定义 override 与 results item 集成；直接受影响测试 **44/44** 通过，全量 `osu.Game.Rulesets.Bms.Tests` **397/397** 通过
- `dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过；当前仍仅有既有 `AutoMapper` `NU1903` 告警
- `README.md`、`SKINNING.md`、`DEVELOPMENT_STATUS.md` 与仓库记忆已同步更新，results summary panel 当前真实落地状态现与文档一致

### Phase 1.1：BMS 第三批 Results GaugeHistoryPanel 接入 formal skinization

- 已把 `GaugeHistoryPanel` 纳入 `BmsSkinComponents` 正式契约；`BmsSkinTransformer` 现支持该组件的 ruleset 级 fallback / override，要求皮肤实现 `IBmsGaugeHistoryPanelDisplay`
- `BmsRuleset.CreateStatisticsForScore()` 的 gauge history item 现改为返回无标题 `StatisticItem`，由 `SkinnableBmsGaugeHistoryPanelDisplay` 自己承载 `GAUGE HISTORY` 标题、空态与内层 `SkinnableBmsGaugeHistoryDisplay`；因此皮肤既可整体替换 results gauge history 面板，也可只替换内部时间线图表
- 已扩充 `BmsSkinTransformerTest` 与 `BmsRulesetStatisticsTest`，覆盖 `GaugeHistoryPanel` 的默认 fallback / 自定义 override 与 results item 集成；直接受影响测试 **35/35** 通过，全量 `osu.Game.Rulesets.Bms.Tests` **395/395** 通过
- `dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过；当前仍仅有既有 `AutoMapper` `NU1903` 告警
- `README.md`、`SKINNING.md`、`DEVELOPMENT_STATUS.md` 与仓库记忆已同步更新，results gauge history panel 当前真实落地状态现与文档一致

### Phase 1.1：BMS 第三批 Song Select NoteDistributionPanel 接入 formal skinization

- 已把 `NoteDistributionPanel` 纳入 `BmsSkinComponents` 正式契约；`BmsSkinTransformer` 现支持该组件的 ruleset 级 fallback / override，要求皮肤实现 `IBmsNoteDistributionPanelDisplay`
- `BmsNoteDistributionGraph` 现拆为“外层 skinnable panel + 内层 skinnable graph”两层：默认 panel 负责标题、状态文本与总数 / scratch / long note / 峰值密度摘要，内部图表仍继续走 `NoteDistribution` 组件 lookup，因此皮肤既可整体替换 Song Select 右侧面板，也可只替换图表主体
- 已扩充 `BmsSkinTransformerTest`，覆盖 `NoteDistributionPanel` 的默认 fallback 与自定义 override；直接受影响测试 `BmsSkinTransformerTest` **40/40** 通过，全量 `osu.Game.Rulesets.Bms.Tests` **393/393** 通过
- `dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过；当前仍仅有既有 `AutoMapper` `NU1903` 告警
- `README.md`、`SKINNING.md`、`DEVELOPMENT_STATUS.md` 与仓库记忆已同步更新，Song Select 分布图面板当前真实落地状态现与文档一致

### Phase 1.1：BMS 第三批 ResultsSummary / ClearLamp 接入 formal skinization

- 已把 `ClearLamp` 与 `ResultsSummary` 纳入 `BmsSkinComponents` 正式契约；`BmsSkinTransformer` 现支持这两个组件的 ruleset 级 fallback / override，分别走 `IBmsClearLampDisplay` 与 `IBmsResultsSummaryDisplay`
- `BmsRuleset.CreateStatisticsForScore()` 的 `BMS Statistics` 现改为返回 `SkinnableBmsResultsSummaryDisplay`；默认 summary 会展示 gauge type、judge mode、long note mode、EX-SCORE / MAX EX-SCORE、EMPTY POOR、EX %、DJ LEVEL 与 final gauge，并在内部嵌入可独立 override 的 `SkinnableBmsClearLampDisplay`
- 已扩充 `BmsSkinTransformerTest` 与 `BmsRulesetStatisticsTest`，覆盖 clear lamp / results summary 的 fallback / override 与 results item 集成；聚焦受影响测试 **40/40** 通过，全量 `osu.Game.Rulesets.Bms.Tests` **391/391** 通过
- `dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过；当前仍仅有既有 `AutoMapper` `NU1903` 告警
- `README.md`、`SKINNING.md`、`DEVELOPMENT_STATUS.md` 与 `OMS_COPILOT.md` 已同步更新，第三批当前真实落地状态现与文档一致

### Phase 1.1：BMS 第二批 Judgement / Combo 接入 formal skinization

- 已为 BMS 自定义判定建立正式 lookup 契约：新增 `BmsJudgementSkinLookup`，并把 `BAD / POOR / EMPTY POOR` 从通用 `SkinComponentLookup<HitResult>` 路由到 `SkinnableBmsJudgement`，再进入 ruleset 级 fallback / override；默认显示仍由 `BmsJudgementPiece` 承担，因此 OMS 的产品命名语义保持不变
- 已把 combo display 正式收口为 `BmsSkinComponents.ComboCounter`；`BmsSkinTransformer` 的 BMS `MainHUDComponents` 现在会在保留皮肤自身 ruleset HUD 内容的同时，补入 BMS gauge bar 与 combo counter，并隐藏重复的通用 combo counter
- 已扩充 `BmsSkinTransformerTest`，覆盖自定义判定 wrapper、显式 judgement lookup、combo counter fallback / override 以及 ruleset HUD 注入语义；直接受影响测试文件 `BmsSkinTransformerTest` **34/34** 通过，全量 `osu.Game.Rulesets.Bms.Tests` **386/386** 通过
- `dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过；当前仍仅有既有 `AutoMapper` `NU1903` 告警
- `README.md`、`SKINNING.md` 与 `DEVELOPMENT_STATUS.md` 已同步更新，文档基线现与第二批 `Judgement / Combo` 的真实落地状态一致

### Phase 1.1：BMS 第二批 Note / Hold / LaneCover 开始 formal skinization

- 已为 BMS 第二批主体组件建立正式 lookup 契约：新增 `BmsNoteSkinLookup` / `BmsLaneCoverSkinLookup`，并把 normal note、长条头/体/尾、top lane cover、bottom lane cover 纳入 `BmsSkinTransformer` 的 ruleset 级查找与 fallback 路由
- `DrawableBmsHitObject` 已从直接按类型绘制 `Box` 的路径切到 `SkinnableDrawable` 驱动；普通 note、长条头、长条体、长条尾都会根据 lane 与 scratch 元数据进入正式 lookup 链
- `BmsLaneCover` 已改为“外层 coverage 容器 + 内层 skinnable display”的正式结构；`top / bottom` 位置与 focus 状态现在都属于正式皮肤语义，而不是临时 overlay
- 已扩充 `BmsSkinTransformerTest`，新增 note / long-note / lane-cover 的 fallback 与 override 覆盖；直接受影响测试文件运行 **22/22** 通过，并在收尾时重新跑完整 `osu.Game.Rulesets.Bms.Tests` 项目，全量 **378/378** 通过
- 本轮全量 `dotnet test .\osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj --no-restore` 通过；当前仍仅有既有 `AutoMapper` `NU1903` 告警

### 文档：新增玩家 / 皮肤设计师说明书

- 新增根目录 `SKINNING.md`，专门面向玩家与皮肤设计师解释 OMS 当前皮肤系统的真实落地状态、fallback 顺序、BMS 已开放组件矩阵，以及哪些部分仍未冻结
- `README.md` 已补充 `SKINNING.md` 入口，并把最近一次验证结果更新为当前的 **378/378** BMS 全量测试快照
- `DEVELOPMENT_STATUS.md` 已同步把 BMS 第二批从“未开始”修正为“进行中”，并更新当前皮肤基线与最近一次验证摘要，避免文档继续落后于仓库真实状态

### Phase 1.1：BMS 第一批壳层组件开始正式 skinization

- 已为 BMS 第一批壳层组件建立正式 skin lookup 契约：新增 `BmsPlayfieldSkinLookup` / `BmsLaneSkinLookup`，并把 `Playfield`、`Lane`、`HitTarget`、`BarLine`、`Static Background Layer` 纳入 `BmsSkinTransformer` 的 ruleset 级查找与 fallback 路由
- `BmsPlayfield`、`BmsLane`、`BmsScratchLane`、`BmsHitTarget`、`BmsScratchHitTarget`、`BmsBackgroundLayer`、`DrawableBmsBarLine` 已从纯直绘 fallback 结构切到 `SkinnableDrawable` 驱动；现阶段仍使用默认 fallback 外观，但 ruleset 层正式 skinization 入口已经接通
- `BmsLaneLayout` 现携带 `Keymode` 元信息，lane / bar line / hit target 的 lookup 也会带上 lane index、lane count、scratch 标记与 keymode，为后续默认皮肤包和用户皮肤 partial override 提供稳定上下文
- 已扩充 `BmsSkinTransformerTest`，覆盖 playfield backdrop、lane background、hit target、static background 的 fallback 与 override 路由；调试期项目级过滤运行 **21/21** 通过，并已在收尾时重新跑完整 `osu.Game.Rulesets.Bms.Tests` 项目，全量 **373/373** 通过
- `dotnet build .\osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过；当前仍仅有既有 `AutoMapper` `NU1903` 告警

### 文档重排：Phase 1.1 改为按皮肤组件批次推进

- 已重写 `DEVELOPMENT_PLAN.md` 的 **Phase 1.1**：不再只写“共享视觉契约 / built-in skin 骨架”这类抽象目标，而是改成按具体组件批次推进的开发计划：默认皮肤包分层、组件清单与代码映射、资源命名/配置桥接、Global provider、mania 的 Stage/Column/Key 与 Note/Hold/HUD 两批迁移，以及 BMS 的 Playfield/Lane、Note/LaneCover、Gauge/Results/Song Select 三批迁移
- 已在 `DEVELOPMENT_STATUS.md` 中把 Phase 1.1 当前状态表改为组件级矩阵，明确哪些仍是 feedback 层、哪些只有局部 lookup、哪些完全未开始，避免继续把皮肤系统当成纯抽象规划
- 已澄清默认皮肤包语义：OMS 的目标是**一个默认皮肤选择项/一个默认皮肤包**，其中集成 `Global + Mania + BMS` 三层；mania 与 BMS 的 gameplay 皮肤本体彼此独立，不是“共用同一套 gameplay 皮肤语义”，只是在同一个默认皮肤包中并存
- `README.md` 与 `OMS_COPILOT.md` 已同步改写为上述语义，避免后续把“单一默认皮肤包”误读成“mania/BMS 共用一套 note / judgement / lane / HUD 皮肤”
- 本次为**文档 / 规划重排**，未改动运行时代码；未重新跑构建或测试

## 2026-04-05

### Phase 1.1：BMS fallback feedback 皮肤配色改为 IIDX 风格

- `osu.Game.Rulesets.Bms/UI/BmsTemporarySkinPalette.cs` 新增统一临时调色板，集中定义皮肤无法正常加载时的 BMS feedback/fallback 层所使用的 IIDX 风格暗底、冷色长条、暖色 scratch 与金属分隔线配色，减少 gameplay 颜色继续散落在多个类中的情况
- `BmsPlayfield`、`BmsLane`、`BmsScratchLane`、`BmsHitTarget`、`BmsScratchHitTarget`、`DrawableBmsHitObject` 与 `DrawableBmsBarLine` 现已统一改用该调色板；整体方向调整为更接近 IIDX 的深色底板、银白普通 note、青蓝长条、暖橙 scratch
- 长条头尾与长条体现在分离配色：长条判定点使用更亮的冷青色，长条主体使用更深的蓝青色；scratch 长条主体也改为更深的暖铜色，避免与普通 scratch note 混成同一亮度块
- `BmsLaneCover`、`BmsBackgroundLayer`、`BmsGaugeBar`、`BmsGaugeHistoryGraph` 与 `BmsNoteDistributionGraph` 现也统一接入同一临时调色板：lane cover 改为深烟黑 + 暖金聚焦提示，背景占位改为冷灰蓝洗版，gauge / 历史图 / 分布图面板改为统一的深色 HUD 表面与金属边框，同时 Song Select 分布图中的 scratch / long note 语义色与 gameplay 侧保持一致
- `BmsGaugeColours` 的各档位 gauge 主色/高亮色也已收口到 `BmsTemporarySkinPalette`，避免 gameplay HUD 仍保留独立硬编码色表；`BmsGaugeBar` 的默认回退色也改为沿用 HUD 文本体系
- 处理了 feedback 层对后续皮肤体系的两处直接阻塞：`BmsSkinTransformer` 的 BMS `MainHUDComponents` 现会先尊重皮肤提供的 ruleset HUD，再 fallback 到默认 gauge bar 容器；同时新增 `GaugeBar` / `GaugeHistory` 的 BMS skin lookup，使默认皮肤与未来用户导入皮肤都可以单独接管这两个组件，而不必继续被直绘 feedback 层硬拦截
- `BmsRuleset.CreateStatisticsForScore()` 的 results gauge history 现改为走 `SkinnableBmsGaugeHistoryDisplay`，并补上 `BmsSkinTransformerTest` 覆盖 HUD 覆盖优先级、gauge bar fallback 与 gauge history fallback 路由
- 注意：以上改动服务于“皮肤无法正常加载时的 feedback/fallback 层”，不代表 OMS 默认内置皮肤的正式设计方向；真正的 OMS built-in skin 仍待 Phase 1.1 后续开发
- `dotnet test osu.Game.Rulesets.Bms.Tests --filter FullyQualifiedName~BmsSkinTransformerTest` 通过；`dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 通过；仅有既有 `AutoMapper` `NU1903` 告警

### 文档规划：Phase 1.1 皮肤系统专项

- 已在 `OMS_COPILOT.md` 中重写皮肤系统权威规范：明确 OMS 将继续使用 osu!lazer 既有 `ISkin` / `Skinnable` 架构，但最终产品默认皮肤必须迁移到 OMS 自有 built-in skin；默认皮肤应表现为一个集成 `Global + Mania + BMS` 的默认皮肤包，其中 mania 与 BMS 为各自独立的 ruleset 皮肤实现，并逐步移除 Argon / Triangles / Legacy / Retro 等上游原生默认皮肤的默认产品地位
- 已在 `DEVELOPMENT_PLAN.md` 中新增 **Phase 1.1 皮肤系统专项**（`1.1.1` ~ `1.1.12`），并将其改写为按默认皮肤包骨架、mania 组件批次、BMS 组件批次、用户皮肤兼容桥、上游默认皮肤移除、打包约束与测试矩阵推进的主线
- 已在 `DEVELOPMENT_STATUS.md`、`README.md`、`RELEASE.md` 中同步当前主执行焦点切换：后续主精力优先投入皮肤系统完善，并把“正式发行只附带 OMS 内置默认皮肤、不再以 osu!lazer 原生默认皮肤作为对外默认体验”写入状态与发行约束
- 本次为**文档 / 规划更新**，未改动运行时代码；现有最近一次 `dotnet build osu.Desktop` 通过与 `dotnet test osu.Game.Rulesets.Bms.Tests` **361/361** 通过的结论保持不变

### 信噪比优化：文档压缩与 Discord RPC 守卫

- **Discord Rich Presence 守卫**：`OsuGameDesktop.LoadComplete()` 现在只在 `OnlineFeaturesEnabled` 为 `true` 时才加载 `DiscordRichPresence` 组件，离线模式下不再向 Discord 泄露活动状态
- **联网约束表收口**："游戏内联网入口隐藏"与"上游静态资源 fallback 清理"均已升级为"已完成"，所有运行时可达的在线入口已经确认被 `OnlineFeaturesEnabled` / 空 endpoints / `LocalOfflineAPIAccess` 三重防线阻断
- **DEVELOPMENT_STATUS.md 大幅压缩**：从 ~400 行压缩到 ~160 行：Phase 1 矩阵每行从长段落压缩为 1-2 句关键事实；"已落地能力"改为引用矩阵的高层摘要（~5000 字符 → ~500）；"当前主线"改为表格；"遗留问题"去除重复描述；"下一次更新时应检查"从 11 项精简到 5 项
- **README.md 精简**："当前状态"从 ~14 条分项列表压缩为 6 项关键能力 + 1 句验证摘要
- **repo memory 精简**：`oms-project-summary.md` "当前主线与断点"从 ~30 行 verbose 流水账压缩为 5 行
- `dotnet build osu.Desktop` 退出码为 0；`dotnet test osu.Game.Rulesets.Bms.Tests` **361/361** 通过

### 文档重构：验证历史迁移与联网审计收口

- **新建 `CHANGELOG.md`**：将 DEVELOPMENT_STATUS.md 中 ~190 行的"最近一次验证"完整历史迁移至本文件，原文件仅保留最新快照与交叉引用，减少 DEVELOPMENT_STATUS.md 冗长度
- **联网入口全面审计收口**：确认 Toolbar、Song Select、所有 overlay、编辑器外链、Preview/LargeTextureStore/metadata cache/BundledBeatmapDownloader/SentryLogger/SignalR 均已被 `OnlineFeaturesEnabled` / 空 endpoints / `LocalOfflineAPIAccess` 三重防线阻断；Settings 的 Report Issue 按钮新增 `OnlineFeaturesEnabled` 守卫；`Medal.ImageUrl`、`OsuMarkdownImage` 中的硬编码 ppy.sh URL 因对应 overlay 已阻断而不可触发；Discord Rich Presence 本地 IPC 仍无条件活跃（不发网络请求），后续可按需调整
- `dotnet build osu.Desktop` 退出码为 0；`dotnet test osu.Game.Rulesets.Bms.Tests` **361/361** 通过；当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 便携发行基线与 cherry-pick 跟踪

- **新增 `RELEASE.md`**：文档化了 Portable.zip 构建命令、发行物内容、用户数据存储路径（`%APPDATA%/oms/`）、版本覆盖更新流程、冒烟测试与在线功能状态；确认程序文件覆盖不影响已导入谱面、成绩与设置
- **扩充 `UPSTREAM.md`**：完整列举了 osu.Game 中被 OMS 修改的 ~37 个文件（2 个新增 + ~35 个修改），按层级分类（离线 gate / Ruleset 扩展点 / RulesetData 持久化 / 自定义 Loader），并标注了 cherry-pick 高风险文件（`BeatmapCarousel`、`FilterControl`、`WorkingBeatmapCache`、`BeatmapManager`、`OsuGame`、`OsuGameBase`）
- **Portable 发行审计结论**：`Program.cs` 的 `setupVelopack()` 已完全早退、`OsuGameDesktop` 无安装路径假设、首次运行逻辑已绕过、用户数据与程序目录分离——无阻断项
- `dotnet build osu.Desktop` 退出码为 0；`dotnet test osu.Game.Rulesets.Bms.Tests` **361/361** 通过；当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 遗留问题噪声清理与代码清洁

- **DEVELOPMENT_STATUS 噪声清理**：移除了遗留问题与检查列表中 6 处已修复的删除线条目（results auto-jump、Directory.Build.props NuGet 元数据、slnf Templates 项目、nullable 告警、results auto-jump 实机验证检查项、测试覆盖缺口检查项），减少后续对话信息噪声
- **`BmsScoreProcessor` 诊断日志**：`[BMS] ApplyBeatmap`、`COMPLETED OK`、`COMPLETION STUCK` 三组诊断日志及关联计数器字段已包裹在 `#if DEBUG` 条件编译内，Release 构建不再无条件输出这些仅用于调试 results auto-jump 的运行时日志
- **`osu.nuspec` 元数据**：title / authors / owners / description / copyright 从上游 `ppy Pty Ltd` 更新为 OMS；移除了指向 `osu.ppy.sh` 的 projectUrl
- **在线 fallback 审计**：确认 `PreviewTrackManager`、`LocalCachedBeatmapMetadataSource`、`BundledBeatmapDownloader` 中的 `ppy.sh` URL 均已被现有离线模式守卫屏蔽，运行时不可达；`TrustedDomainOnlineStore` 仍为 `*.ppy.sh` 白名单，待 Phase 3 有 OMS 域名时再扩展；Velopack 已被 `IsInAppUpdateEnabled => false` 完全短路，相关死代码保留作为 Phase 3 技术预留
- `dotnet build osu.Desktop` 退出码为 0；`dotnet test osu.Game.Rulesets.Bms.Tests` **361/361** 通过；当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 遗留问题处理

- **测试覆盖缺口补齐**：新增 `BmsKeysoundSampleInfoTest`（20 个独立单测：构造器正规化、路径遍历拒绝、`LookupNames` 有/无扩展名、`Equals` / `GetHashCode` 大小写不敏感、`TryCreate` / `TryNormaliseFilename` 边界、`With()` 保持 filename）与 `TestSceneBmsLaneCover`（7 个 headless scene 回归：position 保留、覆盖率 0/50/clamp、focus 显/隐/zero-coverage 不显示）；`BmsOrderedHitPolicy` 已由 `BmsDrawableRulesetTest` 与 `TestSceneOmsScratchGameplayBridge` 间接覆盖，无需独立桩测试
- **`Directory.Build.props` NuGet 元数据**：Authors / Company / Copyright 从 `ppy Pty Ltd` 更新为 `OMS contributors`，PackageTags 追加 `bms`
- **nullability 告警**：构建确认仅剩 `AutoMapper` `NU1903`，无 OMS 引入的 nullable 告警
- **AutoMapper 升级评估**：14.0+ 有破坏性 API 变更（`c.Internal().ForAllMaps()` 等 internal API 不兼容），移除需手写 ~150 行替代深拷贝；当前 `MaxDepth(3)` 已缓解实际攻击面，暂不动，继续跟踪
- `dotnet build osu.Desktop` 退出码为 0；`dotnet test osu.Game.Rulesets.Bms.Tests` **361/361** 通过（较之前 326 新增 35 测试）

### 1.17 analog scratch pulse 语义、loaded gameplay bridge 与 scratch stream/hold 回归

- `OmsMouseAxisInputHandler` / `OmsHidAxisInputHandler` 不再把同向连续移动折叠成跨多帧/多轮询的一次长按，而是按帧/按轮询在边界自动 release。这样同向持续搓盘会重新产生成对的 press/release pulse，更接近 scratch 离散触发而不是"第一拍按下后一直按住"
- 同一帧/同一轮询内若同一 scratch action 从正向切到反向，axis handler 现会先 release 再 re-press，而不再被 shared-action 引用计数折叠成一次连续长按；这让快速换向也能继续产出新的 scratch edge
- `OmsHidDeviceHandlerTest` 现已补上 device polling 层 rapid-flip 回归：单次 `PollOnce()` 内若 turntable axis batch 从正向切到反向，`OmsHidDeviceHandler` 仍会排空全部 queued axis changes，并产出独立的 `+/-/+/-` scratch pulse，而不是在 device 层吞掉第二个 edge
- `BmsInputManager` 现只在 `OmsInputRouter` 的全局首个 press / 最终 release 时才把对应 `BmsAction` 转发给 `KeyBindingContainer`；keyboard、XInput、mouse-axis、HID axis 等多个 source 共享同一 scratch 时，不会再因为某个非最终 source 的 release 把 gameplay 侧动作提前放掉
- `OmsInputRouterTest` 现已额外锁定显式 `OmsMouseAxisInputHandler -> BmsInputManager -> KeyBindingContainer` 链路：mouse-axis pulse 会在帧尾释放 gameplay scratch 动作，而当 keyboard 已持有同一 scratch 时，mouse-axis pulse 的帧尾 release 不会把 gameplay 侧动作提前放掉
- 新增 `TestSceneOmsScratchGameplayBridge` loaded headless scene 回归：显式证明 `OmsMouseAxisInputHandler` / `OmsHidAxisInputHandler` / XInput scratch 经 `BmsInputManager -> DrawableBmsRuleset/BmsPlayfield` 的真实输入链都能结算 scratch stream；重复 scratch pulse / press 可连续命中，而较晚输入也会在 poor window 内强制把更早未判 note 记为 miss。新增 mixed-source scene 进一步锁定 keyboard 持有 scratch 时，HID pulse、mouse pulse 与 XInput press 都不会伪造新的 gameplay hit edge，scratch 只会在最后一个 source release 后才真正松开；若此时正在持有 scratch hold，mouse/HID pulse 的 `FinishFrame()` / `FinishPolling()` 边界不会打断 hold，XInput 也能在 keyboard 中途释放后继续接管 hold，tail 仍会沿 held path 正常结算。此前把这类 bridge 回归写在 detached plain NUnit harness 中会得到假阴性，因此现已统一迁到 loaded scene
- `BmsDrawableRulesetTest` 现已补上 gameplay-facing scratch stream 回归：重复 scratch press 可以连续命中 scratch stream，且在仍处于 poor window 内时，较晚的 scratch 命中会强制把更早未判定 note 记为 miss。为让这类 direct-drawable lane 单测与 runtime 共享同一 ordering 语义，`BmsOrderedHitPolicy` 现会优先读取 `AliveObjects`，若 detached/non-pooled harness 尚未物化 alive lifetime，则回退到当前 in-use `Objects`
- `dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter FullyQualifiedName~TestSceneOmsScratchGameplayBridge -verbosity minimal` 最近一次 **14/14** 通过，并已覆盖 mouse-axis + HID-axis + XInput 的 loaded gameplay bridge、keyboard-held mixed-source suppression、keyboard-held scratch hold 在 mouse/HID pulse 中途不被打断，以及 keyboard->XInput hold takeover 后 tail 仍正常结算的回归；全量 `dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore -verbosity minimal` 最近一次 **361/361** 通过；当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 1.17 cross-device shared-action 语义

- `OmsInputRouter` 不再只按布尔 pressed state 记录 `OmsAction`，而是改为按动作引用计数维护共享按压；这样 keyboard / mouse-axis / HID / XInput 等多个 handler 命中同一 scratch 或同一动作时，不会因为其中一个 source 先释放就把另一个仍处于激活态的 source 一并放掉
- `OmsInputRouterTest` 当前已覆盖 router 重复 press/release 计数语义、keyboard + mouse-axis 共享 scratch 状态、`BmsInputManager` 下 keyboard + XInput 共享 scratch 状态、mouse-axis 同帧反向换向 retrigger，以及 `KeyBindingContainer` 的 mixed-source shared-state 与 mouse-axis bridge 回归；而 keyboard-held + HID-axis / mouse-axis / XInput mixed-source 的 runtime 语义现已额外由 `TestSceneOmsScratchGameplayBridge` 在 loaded hierarchy 内锁定；上述修正均已包含在当前全量 BMS **361/361** 验证内

### 1.5 importer 入口与通知回归

- `BmsImportIntegrationTest` 已新增三类 importer 端到端回归：单个 `.bms` stream task 导入、archive 内重复谱面触发 skipped-file warning、以及无有效 beatmap 的 archive 触发失败 notification。这样 `BmsBeatmapImporter` 自己的入口与通知语义不再只靠 `BmsArchiveReader` / `BmsFolderImporter` 间接覆盖
- `dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter FullyQualifiedName~BmsImportIntegrationTest -verbosity minimal` 最近一次 **16/16** 通过；全量 `dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore -verbosity minimal` 最近一次 **361/361** 通过；当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 1.6 长条 tail release-window 校准

- `BmsJudgementSystem.SetLongNoteReleaseWindows()` 不再对所有 tail release 判定窗口统一乘一个 lenience；release `Perfect` / `Great` / `Good` / `Meh` 现与当前 judge mode 的普通命中窗口保持一致，只有 release `Miss` 仍保留轻微放宽。默认 `OD` 现走 `BmsHoldNote.DEFAULT_RELEASE_MISS_LENIENCE = 1.25`，`BEATORAJA` / `LR2` 则分别使用 `1.2` 的 release miss grace
- `BmsHoldNoteTailEvent.MaximumJudgementOffset`、tail miss-window 检查与 `BmsTimingWindows.WindowFor(..., isLongNoteRelease: true)` 现共用同一套 release-window 数据源；`BmsDrawableRulesetTest` 已补测 `OD` / `BEATORAJA` / `LR2` 三套模式下的 release-window 期望值、精确结果分段、`CN` / `HCN` 的 late-press miss-window 边界，以及 scratch stream repeated press / late-hit ordering 回归；`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter FullyQualifiedName~BmsDrawableRulesetTest -verbosity minimal` 最近一次 **46/46** 通过，`--filter FullyQualifiedName~TestSceneOmsScratchGameplayBridge -verbosity minimal` 最近一次 **14/14** 通过，全量 `dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore -verbosity minimal` 最近一次 **361/361** 通过；当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 1.6 CN/HCN gauge 分母修正

- `BmsGaugeProcessor` 现在会按 beatmap 当前的 nested long-note 结构统计 `TotalHittableObjects`：普通单键与 hold head 继续计入，`CN` / `HCN` 下 `BmsHoldNoteTailEvent` 若仍是 scored tail 也会进入 gauge `BaseRate` 分母，而 `HCN` gauge-only body tick 仍不进入分母；这避免了含大量 scored tail 的谱面在 gauge 变化量上被放大
- 已扩展 `BmsGaugeProcessorTest` 覆盖 LN / CN / HCN 三种模式下的 gauge scaling 与 body tick 回归；`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore -verbosity minimal` 最近一次 **361/361** 通过，当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 1.17 Windows Raw Input 键盘源

- `osu.Desktop` 已新增 `WindowsRawKeyboardSource`，在 Windows 桌面端按 gameplay `Playing` + 窗口激活态启停原生键盘捕获；当前会注册 Raw Input keyboard 设备、子类化游戏窗口接收 `WM_INPUT`，并把原生键位映射为 `InputKey` 后送入新的 `IOmsKeyboardEventSource -> IOmsKeyboardEventSink` 链
- `BmsInputManager` 现已实现 `IOmsKeyboardEventSink` 并在加载/释放时自动向桌面侧 keyboard source 注册/注销；raw keyboard 的 press/release 会复用现有 `OmsKeyboardInputHandler` 语义，disable 时还会通过 `ResetRawKeyboardState()` 主动清理残留按压，避免 gameplay 退出或失焦后卡键
- `dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 最近一次退出码为 0；`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore --verbosity minimal` 最近一次 **287/287** 通过，且 `OmsKeyboardInputHandlerTest` 已新增 raw keyboard sink 入口与 reset 回归；当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 1.17 keyboard gameplay 事件接线

- `BmsInputManager` 现已在 `OnKeyDown()` / `OnKeyUp()` 中优先把属于 OMS binding 的 framework keyboard events 交给 `OmsKeyboardInputHandler`，并在命中时直接消费事件，避免默认 `KeyBindingContainer` 对同一组键盘输入重复触发；现有 replay / 非 OMS 键路径保持不变
- 这让 `TriggerKeyPressed()` / `TriggerKeyReleased()` 不再只是测试或外部注入入口，而成为 live keyboard gameplay 的统一接收面；后续若补 Windows Raw Input，只需把新的键盘事件源送进同一条 OMS keyboard handler 链
- `dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 最近一次退出码为 0；`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore --verbosity minimal` 最近一次 **287/287** 通过，且定向执行 `OmsKeyboardInputHandlerTest` + `OmsInputBridgeTest` 回归最近一次 **24/24** 通过；当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 1.17 通用 keybinding 面板整合

- `osu.Game.Rulesets.Ruleset` 已新增通用 `CreateKeyBindingSections()` 扩展点，`RulesetBindingsSection` 现会在默认 variant keybinding rows 之后挂载 ruleset-specific keybinding panel sections；BMS 侧已通过 `BmsRuleset.CreateKeyBindingSections()` 把现有 `BmsSupplementalBindingSettingsSection` 直接接入 `Input -> Configure -> BMS` 区块，避免 supplemental trigger 只能从 ruleset settings 入口单独维护
- `BmsSupplementalBindingSettingsSection` 现已实现 `IFilterable`，可被 settings search 命中 `supplemental` / `hid` / `mouse` / `trigger` 等关键词；`BmsRulesetConfigurationTest` 也新增断言，确保 BMS ruleset 会稳定暴露该 supplemental keybinding section
- `dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 最近一次退出码为 0；`dotnet test osu.Game.Rulesets.Bms.Tests --no-restore --verbosity minimal` 最近一次 **285/285** 通过，当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 1.17 OMS supplemental trigger mouse-axis live capture

- `oms.Input` 已新增 `OmsMouseAxisCapture`，用于将设置页累计的鼠标位移解析为 `OmsMouseAxis + OmsAxisDirection`；当前采用 dominant-axis 判定并带最小位移阈值，避免点击 `Start capture` 后的轻微抖动被误识别为绑定输入
- `BmsSupplementalBindingSettingsSection` 现已把 live capture 从 HID-only 扩到统一入口：`MouseAxisBindingRow` 也新增了 per-row `Start capture` / `Cancel capture`，capture 期间会直接从包含该设置页的 `InputManager` 读取鼠标位置并累计 delta，成功后自动回填 mouse axis 与 direction，`Invert axis` 则重置为未勾选
- `dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 最近一次退出码为 0；`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter OmsMouseAxisInputHandlerTest` 最近一次 **7/7** 通过；全量 BMS 测试现为 **285/285** 通过，当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 1.17 OMS supplemental trigger HID live capture

- `oms.Input` 已新增 `Devices/OmsHidDeviceCaptureSession`，复用现有 `IOmsHidButtonDeviceProvider` / `IOmsHidButtonDevice` 与 `OmsHidDeviceChange` 结构，提供脱离 gameplay binding 语义的单设备 HID capture 会话；`OmsHidDeviceHandler` 也同步补出默认 provider 工厂，避免设置页重复实现 HidSharp 设备打开逻辑
- `BmsSupplementalBindingSettingsSection` 的 `HidButtonBindingRow` / `HidAxisBindingRow` 现已新增 per-row `Start capture` / `Cancel capture` 按钮：若当前只连接一台 HID 设备会自动回填 device identifier，否则仍可手动指定 device identifier 后开始 capture；成功后会自动回填 button index 或 axis index，并按 axis delta 符号推导方向
- `dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 与 `dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore` 最近一次均退出码为 0；BMS 全量测试现为 **284/284** 通过，当前仍仅剩既有 `AutoMapper` `NU1903` 告警

### 1.17 OMS supplemental trigger 编辑 UI

- `oms.Input` 已新增 `Devices/OmsHidDeviceDiscovery`，复用现有 HidSharp 编译别名与 `OmsHidDeviceIdentifier`，在不侵入 gameplay 输入链的前提下为设置页提供当前连接 HID 设备摘要
- `osu.Game.Rulesets.Bms` 已新增 `BmsSupplementalBindingSettingsSection`，并接入 `BmsSettingsSubsection`：当前可按 variant 手动编辑 `HidButton` / `HidAxis` / `MouseAxis` supplemental bindings，支持刷新设备列表、重载当前 variant、应用保存与清空当前 variant，运行时仍通过 `OmsBmsBindingSettingsStorage` + `OmsBmsBindingResolver` 合并回现有 OMS 输入链
- `dotnet build osu.Desktop -p:GenerateFullPaths=true -m -verbosity:m` 退出码为 0；新增编译告警已清零，当前仍仅剩既有 `AutoMapper` `NU1903`；针对输入桥接回归的 `OmsInputBridgeTest` 最近一次 **20/20** 通过

### 1.17 OMS supplemental trigger 持久化基础

- `OmsBmsBindingResolver` 现仅在当前 variant 没有任何持久化 `RealmKeyBinding` 时才回退到默认绑定；如果数据库里已有 BMS 绑定行但无一可转换为 OMS binding，则不再静默重新激活默认键位，避免已保存的非常规绑定被默认值重新带回
- 已新增 `OmsBmsBindingSettingsStorage`，把通用 `RealmKeyBinding` 无法表达的 `HidButton` / `HidAxis` / `MouseAxis` trigger 以 `RealmRulesetSetting` 的 ruleset+variant scoped JSON 持久化；`OmsBmsBindingResolver` 现会把这部分 supplemental OMS bindings 与标准 keyboard/joystick keybindings 合并返回
- 已扩展 `OmsInputBridgeTest` 覆盖"persisted but unconvertible 不再 fallback 默认值""unsupported trigger round-trip""standard + supplemental merge"三类回归；`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore` 退出码为 0，**277/277** 通过

### 1.17 XInput 默认绑定与持久化回环

- `OmsBindingStore` 现为 5K / 7K profile 补出默认 XInput button 绑定，并新增 `buttonIndex <-> InputKey.JoystickN` 转换辅助；`Devices/OmsXInputButtonInputHandler` 继续按 `buttonIndex` 解析 XInput/joystick button press/release，并保持共享 action 的引用计数 release 语义
- `BmsRuleset.GetDefaultKeyBindings()` 现会把默认 XInput button 一并导出为 ruleset keybindings；`OmsBmsBindingResolver` 也已能把持久化的 joystick-only `RealmKeyBinding` 还原为 OMS `XInputButton` trigger，因此现有 keybinding UI 记录到的 joystick button 可以回到 OMS 默认/持久化链路
- 已补 `BindingSettings` 搜索词 `joystick` / `gamepad` / `controller` / `xinput`，并确认当前通用 keybinding UI 复用 `KeyBindingRow.OnJoystickPress()` 路径承载 joystick button 的默认展示与录入，因此当前不再单列独立 XInput 绑定 UI 为剩余缺口
- 已扩展 `OmsXInputButtonInputHandlerTest`、`OmsInputBridgeTest` 与 `BmsRulesetModTest` 覆盖默认 XInput 绑定、持久化 joystick 解析与 ruleset 默认绑定计数；`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore` 退出码为 0，**277/277** 通过

## 2026-04-04

### 1.17 MouseAxis delta 链路接通

- `OmsBinding` 现已补出 `MouseAxisTriggers`，`oms.Input` 新增 `Devices/OmsMouseAxisInputHandler`：按 `axis + direction + inverted` 解析每帧 mouse delta，并沿用现有 axis handler 的共享 action 引用计数语义，把鼠标位移折叠为 pressed/released `OmsAction`
- `BmsInputManager` 现新增 `TriggerMouseAxisDelta()` 入口，并在 `OnMouseMove(MouseMoveEvent e)` 内把 X/Y 方向 delta 直接送入 OMS router；该链路不会修改默认键位，只为显式 mouse-axis 绑定提供最小 backend
- 已新增 `OmsMouseAxisInputHandlerTest`，覆盖方向命中、方向翻转不断链与 axisInverted 语义；`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore` 退出码为 0，**264/264** 通过

### 1.17 HidSharp 轴 delta 链路接通

- `OmsBinding` 现已补出 `HidAxisTriggers`，`oms.Input` 新增 `Devices/OmsHidAxisInputHandler`：按 `deviceIdentifier + axisIndex + direction + inverted` 解析每轮 polling 的 axis delta，并以 pressed/released 语义把轴运动折叠回现有 `OmsAction` 路由，不额外引入新的 gameplay 输入面
- `OmsHidDeviceHandler` 现将 HidSharp 轮询从"仅数字按钮"扩到统一 `OmsHidDeviceChange` 按钮/轴变化流：可读取 relative/absolute axis logical 值、为 absolute axis 计算 delta，并在设备断开时同时 release 仍处于活动态的 button / axis action；`BmsInputManager.Update()` 继续通过 `PollOnce()` 接入该链路
- 已新增 `OmsHidAxisInputHandlerTest`，并扩展 `OmsHidDeviceHandlerTest` 覆盖 axis 方向翻转、共享 action 保持按下、空闲自动 release、断开自动 release 与未绑定设备忽略；`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore` 退出码为 0，**261/261** 通过

### 1.17 HidSharp 数字按钮设备轮询链路接通

- `oms.Input` 现已新增 `Devices/OmsHidDeviceHandler`、`OmsHidDeviceIdentifier` 与可注入的 `IOmsHidButtonDeviceProvider` / `IOmsHidButtonDevice` 抽象：运行时会按绑定中的 `deviceIdentifier` 枚举 HidSharp 设备、解析数字按钮 input report，并在设备移除时自动释放仍处于按下状态的按钮，避免断开控制器后 action 卡住
- `oms.Input.csproj` 现为 `HidSharp` 增加编译别名，规避 `OpenTabletDriver` 链路引入的 `HidSharpCore` 同名类型冲突；`OmsHidButtonInputHandler` 也已统一标准化 `deviceIdentifier`，避免大小写/空白导致绑定不命中
- `osu.Game.Rulesets.Bms` 的 `BmsInputManager` 现统一通过 `applyBindings()` 初始化 keyboard / HID button / HidSharp device handlers，并在 `Update()` 内持续 `PollOnce()`；已新增 `OmsHidDeviceHandlerTest`，覆盖队列按钮变化、断开设备自动 release 与未绑定设备忽略，`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore` 退出码为 0，**256/256** 通过

### 1.17 HID 数字按钮链路接通

- `oms.Input` 现已新增 `Devices/OmsHidButtonInputHandler`，并在 `OmsBinding` 上补出 `HidButtonTriggers` 入口：同一 `OmsAction` 可绑定多个 HID 按钮，只有最后一个活动按钮释放时才会真正触发 release，避免 scratch / 面板多备用按钮提前松键
- `osu.Game.Rulesets.Bms` 的 `BmsInputManager` 现会同步初始化 keyboard + HID button handlers，并新增 `TriggerHidButtonPressed()` / `TriggerHidButtonReleased()` 注入入口；后续 HidSharp 设备枚举/轮询或外部硬件适配层已经可以直接走 `OmsAction -> BmsAction` bridge
- 已新增 `OmsHidButtonInputHandlerTest`，覆盖同 action 多按钮引用计数、跨设备按钮隔离，以及通过 `TriggerOmsActionPressed()` / `TriggerOmsActionReleased()` 驱动 router 的 HID 注入断言；`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore` 退出码为 0，**253/253** 通过

### 1.17 键盘后端与组合键语义收口

- `oms.Input` 现已把键盘触发模型提升为 `OmsBindingTrigger`：一个 `OmsBinding` 现在可携带完整键盘 `KeyCombination` 以及为后续 HID / Mouse Axis / XInput 预留的设备触发类型，不再把"多个键"强行解释成同一层级的备用单键
- `Devices/OmsKeyboardInputHandler` 现按完整 `KeyCombination` 解析 press/release：既保持 scratch 默认 `Q/A` 这类备用单键引用计数语义，也修正了 `Ctrl+Key` 这类组合键不会再被误当成两个可独立触发的单键
- `osu.Game.Rulesets.Bms` 的 `OmsBmsBindingResolver` 现会保留 `RealmKeyBinding.KeyCombination` 的完整组合语义，而不是把数据库里的组合键拆成多个备用键；`BmsRuleset.GetDefaultKeyBindings()` 也已改为按 `OmsBinding` 中的完整 keyboard combinations 回吐默认绑定
- 已扩展 `OmsKeyboardInputHandlerTest`，新增组合键必须完整按下才触发的断言；`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore` 退出码为 0，**250/250** 通过

### 1.17 最小正式输入链路接通

- `oms.Input` 已从纯空壳推进到最小正式链路：`OmsBindingStore` 现提供 5K / 7K / 9K / 14K 的默认 profile 绑定源，`OmsInputRouter` 现提供按 `OmsAction` 路由的 pressed/released 状态与事件骨架
- `osu.Game.Rulesets.Bms` 现新增 `OmsBmsActionMap`，将 `OmsAction` 与现有 `BmsAction` gameplay 面做 variant-aware 双向桥接；`BmsRuleset.GetDefaultKeyBindings()` 也已改为从 `OmsBindingStore` 生成默认键位，而不再把键位硬编码在 ruleset 内部
- `BmsInputManager` 现新增最小 router bridge：一方面会把现有 `BmsAction` 输入镜像到 `OmsInputRouter`，另一方面也提供 `TriggerOmsActionPressed()` / `TriggerOmsActionReleased()` 入口，允许后续 Raw Input / HID / XInput backend 直接走 `OmsAction -> BmsAction` 注入路径
- 已新增 `OmsInputBridgeTest` 覆盖 profile 绑定数、`OmsAction -> BmsAction` 映射、`LaneCoverFocus` 的 router 入口，以及 ruleset 默认 scratch 绑定保持不变；`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore` 退出码为 0，**246/246** 通过

### long-note release-window 语义收束

- 已将 long-note tail release-window 从 `DrawableBmsHoldNote` / `BmsHoldNoteTailEvent` 的外部 `RELEASE_WINDOW_LENIENCE` 推导，收束为 `BmsJudgementSystem.LongNoteReleaseWindows` / `BmsTimingWindows.WindowFor(..., isLongNoteRelease: true)` 的正式判定接口
- `OsuOdJudgementSystem`、`BeatorajaJudgementSystem`、`Lr2JudgementSystem` 现都会显式生成 long-note release windows；`DrawableBmsHoldNote` 的 miss-window 判断、tail release 判定与 `BmsHoldNoteTailEvent.MaximumJudgementOffset` 现统一走这套 release-window API，而不再各自重复乘除 lenience
- 已更新 `BmsDrawableRulesetTest`，把 tail release 相关断言改为直接校验 `BmsTimingWindows` 的 long-note release windows，并补 judge mode 下 tail release window 放宽断言
- `dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore` 退出码为 0，**246/246** 通过；构建期仍仅残留 `AutoMapper` `NU1903` 告警

### results auto-jump 最终修复

- `dotnet build osu.Desktop` 退出码为 0，编译器诊断保持 2 个（仅 `AutoMapper` `NU1903`）
- `dotnet test osu.Game.Rulesets.Bms.Tests` 退出码为 0，**235/235** 通过（较 04-03 新增 2 个 hold note completion 测试）
- **Results auto-jump 根因最终修复并经实机验证**：日志诊断发现 `JudgedHits=1952` 远超 `MaxHits=1192`，差值 760 恰好等于游玩过程中动态注入的 `BmsEmptyPoorHitObject` 数量。`BmsEmptyPoorHitObject` 继承自 `HitObject`（非 `BmsHitObject`），原先的黑名单过滤 `is not BmsEmptyPoorHitObject` 在运行时未能正确排除这些动态创建的对象，导致 `JudgedHits != MaxHits` → `HasCompleted` 永远为 false → 不跳结算。修复：将 `BmsScoreProcessor.CountsResultTowardsJudgedHits` 从黑名单改为白名单 `result.HitObject is BmsHitObject and not BmsHoldNote`，仅允许继承自 `BmsHitObject` 的对象（单键音符、长键头、长键尾）计入 `JudgedHits`，自动排除所有非 `BmsHitObject` 类型（`BmsEmptyPoorHitObject`、`BmsBgmEvent`、`BmsHoldNoteBodyTick`）
- 同步简化了 `BmsGaugeProcessor.CountsResultTowardsJudgedHits`，移除多余的 `base.CountsResultTowardsJudgedHits(result)` 调用
- 为 `BmsScoreProcessorTest` 新增 `TestHoldNoteCompletionReachesTrueInLnMode` 和 `TestMixedBeatmapCompletionReachesTrue` 两个 completion 验证测试
- 保留了 `BmsScoreProcessor` 中的诊断日志（`[BMS] ApplyBeatmap` / `[BMS] COMPLETED OK` / `[BMS] COMPLETION STUCK`），方便后续排查

## 2026-04-03

### 遗留问题修复 + 全局审计

- `dotnet build osu.Desktop` 退出码为 0，编译器诊断从 10 个降至 2 个（仅剩 `AutoMapper` `NU1903`）；`dotnet test osu.Game.Rulesets.Bms.Tests` `361/361` 通过
- **Results auto-jump 根因修复**：`BmsHoldNoteBodyTick` 导致 `JudgedHits < MaxHits` → 修复 `resolveTail` 强制 judge 剩余 body tick + `CountsResultTowardsJudgedHits` 排除 body tick / hold parent
- **代码质量修复**：`WorkingBeatmapCache` nullable（7 处 `string?`→`string`）、`RealmAccess` nullable（`.Select(path => path!)`)、slnf 15→7 项目、`AutoMapper` CVE `.MaxDepth(3)` 缓解
- **全局代码审计**：覆盖 BMS ~ 96 源文件、osu.Game ~25 被修改文件、oms.Input；无桩代码/TODO/硬编码凭据；`EndpointConfiguration` 已确认清空；上游 cherry-pick 高风险区：`BeatmapCarousel`/`FilterControl`/`WorkingBeatmapCache`/`BeatmapManager`

## 2026-04-02

### completion 收紧 + 通道修正 + smoke test

- `dotnet build osu.Desktop` 通过；`dotnet test osu.Game.Rulesets.Bms.Tests` `361/361` 通过
- 新增 `SmokeTestDesktop.ps1` 8 秒非交互启动验证
- 修正 BMS SP/BME 通道语义（`16` scratch、`17` free zone 跳过）、`SliderMultiplier=1` + `RelativeScaleBeatLengths=true`
- 收紧 completion 边界（`<=`）、`HasCompleted` 单调语义、`BmsBgmEvent` 排除出 judged-hit 统计
- 修正无音频 BMS 虚拟轨长度（`BeatmapInfo.Length = GetLastObjectTime()`）

## 2026-04-01

### 离线化 + gauge/clear lamp/Empty Poor 接通

- `dotnet build osu.Desktop` 通过；`dotnet test osu.Game.Rulesets.Bms.Tests` `176/176` 通过
- 默认离线模式全面接通：endpoint 清空、`LocalOfflineAPIAccess` 装配、主菜单/Toolbar/Song Select/OsuGame 在线入口按 `OnlineFeaturesEnabled` 隐藏、`LargeTextureStore`/`PreviewTrackManager`/metadata cache 离线退化、First-run Setup 下载禁用、profile 静态资源本地占位
- Empty Poor（`BmsEmptyPoorHitObject` + `ComboBreak`）+ gauge 伤害 + combo 断裂 + 结果页计数
- `BmsGaugeBar` HUD + `BmsModGaugeAutoShift` / `BmsGasGaugeProcessor` GAS 降级链
- `BmsGaugeHistoryGraph` 结果页 gauge history 重放（单层 + GAS 多层）
- 仓库清理：上游副本移除、`.github` 目录移除
