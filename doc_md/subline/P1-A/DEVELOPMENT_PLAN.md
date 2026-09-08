# P1-A 当前计划：Skin V1、产品面与 release gate

> 最后更新：2026-09-08（工作流治理，不改变campaign或产品门）
> 主线顺序见[主线计划](../../mainline/DEVELOPMENT_PLAN.md)，当前事实与唯一产品验证见[当前状态](DEVELOPMENT_STATUS.md)，稳定合同见[技术约束](TECHNICAL_CONSTRAINTS.md)，完成历史见[CHANGELOG](CHANGELOG.md)。

## 子线目标

交付 Windows-only、离线优先的 Skin V1：同一公开外部皮肤路径同时支持mania/BMS；引擎拥有gameplay truth、布局、fallback、安全和资源预算，外部package拥有具体视觉、动画与只读事件响应。

完成下限是同时覆盖mania/BMS的只读canonical `oms-simple.osk`；上限是同包覆盖两ruleset、只用公开API展示接近IIDX复杂度的`oms-complex.osk`。`.osk`、根`skin.ini`、mania素材/帧命名、解包编辑与拖入导入保持osu社区心智，作者无需编译DLL。程序化`OmsSkin`在canonical parity、完整性、原子恢复与实机gate通过前保留。

不属于V1：LR2/beatoraja/IIDX皮肤格式兼容、商业素材捆绑、package修改输入/判定/计分/谱面/BGA timeline，或联网能力。

## 当前执行门与冻结输入

当前为 **`5/7 closed，C6 active`**。C1～C5已闭合；它们在本计划中只作为冻结输入，不重开campaign，也不重复记载实现过程：

- C1作者工作区/archive：external永久只读；held proof、exact-set、single-v3 journal/recovery和ordinary `.osk` receipt保持，thin/arbitrary-path stager冻结。
- C2三源current revision：Settings唯一manual Reload，live gameplay/preview在source prepare前拒绝；participant/lease/detach/retire与current mutation先fallback+detach保持，无watcher/legacy editor/update-import旁路。
- C3唯一layout：P1-K parser/converter是keymode、lane count与keysound timeline唯一authority；BMS/mania/core/HUD/BGA viewport只读同一immutable publication。
- C4 public catalog/shared codec/三态material：28项公共目录、exact bytes唯一解析、stable target/index与critical不可Suppress保持；不新增beatmap-local public作者格式，现有只读legacy direct visual compatibility仍优先。
- C5 scene/event：versioned prepared graph、read-only Snapshot/Reset、全部适用public slot与预算/池化已接入同一package+layout+material+scene publication；BMS 28项有route（9K适用26项），mania 23 Supported、5 NotApplicable。

完整冻结合同只在[技术约束](TECHNICAL_CONSTRAINTS.md)维护，退出证据见[CHANGELOG](CHANGELOG.md)及[C5交接](../../other/SKIN_SYSTEM_C5_SCENE_EVENT_COMPLETION_HANDOFF_20260903.md)。`SV1-0`恢复/数据处置继续生效，迁移归档与无authority orphan blob保全；异常期代码不得整包恢复。

| 未完成门 | 下一动作 | 退出条件 |
| --- | --- | --- |
| C6 / `SV1-6` + `SV1-2`最终整包门 | 在同一campaign完成脚本选型、必要产品决定、真实作者入口/host与整包publication | 以下C6非人工产品结果和硬退出门全部成立 |
| C7 / `SV1-7` + Skin V1自动release | 交付canonical双包、作者套件、恢复与自动发行证明 | 以下C7硬退出门成立，只留集中人工签收 |
| `V-001`～`V-004`、最终视觉/实机 | 按[集中清单](../../other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)签收已导入`.osk`普通短键与LN head/body/tail、选择切换、坏包回落及新增视觉 | Skin V1/release完成声明前统一确认，不能复用2026-07-14静态恢复验收 |

视觉待签收不阻塞可自动验证的后续工作；仅当视觉结论决定后续设计或自动证据无法裁决异常时暂停取得反馈。已通过自动门只能称“实现/自动gate通过，视觉待验收”，不能据此称`SV1-1`、Skin V1或release完成。G1最终门到C6，canonical与程序化产品视觉退出到C7。

## 七个持久Campaign预算与剩余退出门

`SV1-0`～`SV1-7`表示能力与依赖层，不暗示协作轮数。自2026-08-09的campaign启动prompt起，已知Skin V1/P1-A范围及各campaign必须取得终态的产品路线、P1-K layout前置，在最多七个持久campaign内收口；第七个campaign退出时只允许保留集中视觉、真实设备、长时间体验等人工签收。该承诺是campaign prompt预算，不是日历或单提交工期：同一对话可有多次交互、上下文压缩、有意义提交与测试，未过退出门不得生成后续campaign prompt。当前文档治理不占用或推进campaign。

### `C6` 可选脚本与隔离及最终整包reload门

映射：`SV1-6`并最终复核`SV1-2`。

**必须闭合的非人工产品结果：**

同一campaign完成VM选型spike、所需产品确认与production实现，spike/决策不能作为终态。若选择in-tree bounded bytecode VM，必须同切交付无需DLL的package作者入口、版本化source/bytecode格式、compiler/verifier、malformed/untrusted bytecode验证、version reject/compat策略、source-mapped诊断与deterministic fixtures。无论选型均须闭合只读snapshot/event、授权scene node、四方capability协商、per-skin identity授权持久化/撤销/重协商、compiler/runtime版本失效与cache规则、永久hard-deny、instruction/heap/node/resource预算、deterministic clock/seed、seek/retry/reload、异步compile、熔断、profiler与授权UI；script host同切加入C2 revision lease/detach协议

**硬退出门：**

真实BMS/mania host运行complex候选脚本；无限循环、超限、异常、取消/shutdown不阻塞update thread且只熔断脚本/scene；ini/manifest/scene/script/素材全部参与同一publication/detach/owner矩阵，至此关闭最终整包reload与G1自动门。不得只交选型文档、catalog、mock consumer，也不得把语言ABI/工具链、授权持久化、profiler或异常回落推给`C7`

### `C7` canonical双包、Authoring Kit与自动release收口

映射：`SV1-7`并汇总`SV1-1`～`SV1-7`。

**必须闭合的非人工产品结果：**

交付可编辑、可复现构建的`oms-simple.osk`/`oms-complex.osk`、模板、完整schema/event/layout/capability/budget文档、validator/diagnostics与打包导入说明；发行物只读携带、完整性验证/原子恢复。canonical fallback接管必须覆盖`SkinManager`初始/current/config失败pair、ruleset providing containers、selection/reload失败回落、current managed delete/current external unregister、protected Realm record。升级时仍存在且具备完整现行证据的supported pre-C1 v2及C1以后journal，可由旧`OmsSkin`证据继续恢复或显式版本迁移；缺tombstone/fingerprint/manifest/disposition的pre-product legacy-v1/old-v2 Delete继续strict Invalid并进入安装修复，绝不猜测迁移。之后才让程序化`OmsSkin`退出产品authority；canonical缺失/损坏必须阻止进入gameplay并进入明确安装修复，不能重新生成程序化视觉。第三方包、portable/custom-root/update、性能及全套自动门收敛

**硬退出门：**

工程状态达到`SV1-1`～`SV1-7`“自动/合同/安全/release gate通过，人工待签收”；canonical切换前后的全部受支持journal/recovery、delete/unregister receipt与失败回落均可证明收口，invalid旧intent也有不扩大authority的安装修复路径；无程序化主题fallback、私有canonical特权、TODO validator/Authoring Kit或未归因自动失败，同时生成一键人工验收包，用户只需执行视觉/实机清单

### 共同执行规则

1. 只读审计、GO/NO-GO、路线冻结、红测、foundation、DTO、单个consumer、单个提交或文档同步都不能独占一个campaign，也不能推进编号；它们只能是当前campaign的前段或组成部分。
2. 每个campaign最低终态为`产品红测 → runtime/backend → 真实UI caller → 全部声明涉及的production consumer → 失败回退/owner边界 → focused/full/Release → docs/memory → 独立终审 → 有意义提交`。
3. 当前campaign未闭合就留在同一对话继续；若必须由用户改变产品语义，则在同一对话等待，不生成新的handoff prompt来消耗预算。若提前闭合，直接在同一对话进入下一个campaign也允许，因此七个prompt是上限而非配额。
4. 人工签收发现的新缺陷形成新证据后仍须修复，但不能预先虚构其不存在；七个campaign承诺覆盖2026-08-09已知P1-A范围、各campaign内须取得终态的产品路线及明确的P1-K layout前置，不把P1-B/D/E/G其它产品子线偷塞进Skin预算。


## 实施补充与最终验收

C6在C5声明式runtime基础上实现可选脚本，用于声明式能力无法合理表达的组合逻辑；脚本只读snapshot/event、操作获准节点，不能成为普通note/key/judgement显示的必要条件。VM须通过可抢占instruction与heap quota、license、Windows打包、性能/GC、调试诊断spike，并在同一campaign兑现生产能力。低端硬件、权限逃逸、无限循环、内存/节点/资源超限、异常、determinism、seek/retry/pause/reload与profiler都是验收项；编译/I/O在后台，异常只熔断脚本/scene层，保持gameplay。

C6最终整包门继续复验真实选择、重启、切换、rename/import/delete、缺件与原子替换，覆盖备份数据根和现有C1/C2失败回退/owner矩阵。C6新增consumer与脚本权限撤销都必须加入现有revision协议；不另造event、layout、material或publication authority，不改变P1-L BGA内容/timeline/seek。

C7双包均经普通导入/导出链，保留可编辑源和可复现构建；`oms-simple`只含最小可玩件、显式Suppress可选视觉，`oms-complex`证明公开slot/event/script表达上限，不使用私有C# provider、隐藏资源或内置特权。Authoring Kit含模板、schema/event/layout/capability/预算参考、validator/diagnostics与打包说明。最终验收覆盖双包和第三方包、缺失/损坏用户包仍可玩、canonical安装完整性失败的修复入口、启动/切换/reload、全部keymode、BGA、脚本性能、portable/custom-root/覆盖更新、人工视觉与真实设备/谱面。

## 跨线依赖

| 子线 | 向 P1-A 提供 | P1-A 不得越权 |
| --- | --- | --- |
| P1-B/P1-D | 只读输入状态、真实硬件结果 | 不修改输入 edge/hold/calibration authority |
| P1-C/P1-E | 判定、LN/CN/HCN 与反馈语义 | 不由皮肤解释规则结果 |
| P1-H | 路径/authority/重扫经验 | 不直接复制谱面 scanner 的删除 authority |
| P1-J/P1-K | lane keysound proof、keymode/topology truth | 不由 renderer 二次猜 lane/keymode |
| P1-L | BGA timeline/content truth | 不让皮肤创建第二套 player/clock |
| P1-G | 用户实机与 release checklist 汇总 | 不用自动测试替代视觉/硬件结论 |

## 验证矩阵

| 变更面 | 最低自动 gate | 人工 gate |
| --- | --- | --- |
| BMS ruleset 内单一皮肤组件 | BMS skin focused + relevant/full + Release | 受影响 keymode、选择/切换/回落与新增视觉 |
| shared skin/mania compatibility/fallback authority | core skin + mania relevant + BMS relevant + Release | 双 ruleset、选择、fallback 与恢复 |
| G1/Realm/storage | importer/scanner/containment/selection focused + Release | 备份数据根、重启、删改、external/managed |
| layout/BGA | topology/layout/BGA focused + BMS full + Release | keymode/style/宽高比/DPI/逐轨/BGA |
| scene/event/script | ABI/order/fallback/capability/budget + full/Release | 长时间游玩、seek/retry/reload 与 profiler |
| release 双包 | canonical integrity/recovery + core/mania/BMS/Release | `oms-simple`、`oms-complex`、第三方 `.osk` |

## 兼容与回退

- 当前 `.osk/[Mania]`、`.osk/[Bms]`、nullable `ISkin`、选择链与程序化迁移 fallback 在对应替代 gate 前保持不变。
- 任一新切片失败时只回退该切片，不恢复异常期 G1/F2/Lua/mania adapter/reference-default 整包。
- 旧 F/G 术语只作为 CHANGELOG/恢复审计索引；当前执行只看本页C6/C7门；`SV1-*`仅表示能力依赖。
