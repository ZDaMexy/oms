# P1-A 当前计划：Skin V1、产品面与 release gate

> 最后更新：2026-07-16
> 主线顺序见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，硬约束见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)，逐切历史见 [CHANGELOG.md](CHANGELOG.md)。

## 子线目标

交付 Windows-only、离线优先的 Skin V1：同一公开外部皮肤路径同时支持 mania/BMS；引擎拥有 gameplay truth、布局、fallback、安全和资源预算，外部 package 拥有具体视觉、动画与对只读事件的表现响应。

完成下限与上限保持不变：

- `oms-simple.osk`：同时含 mania/BMS 的最小可玩包，承担只读 canonical 逐组件 fallback。
- `oms-complex.osk`：同时含 mania/BMS，只使用公开 API 证明接近 IIDX 复杂度的表达上限。
- `.osk`、根 `skin.ini`、mania 共同素材/帧命名、解包编辑和拖入导入继续遵循 osu 社区心智；BMS/scene/script 是版本化扩展，不要求作者编译 DLL。
- 程序化 `OmsSkin` 只在迁移期保留；`oms-simple` 达到 parity、完整性与恢复 gate 后退出产品渲染链。

不属于 V1：解析 LR2/beatoraja/IIDX 皮肤格式、捆绑商业素材、允许 package 修改输入/判定/计分/谱面/BGA timeline，或提前开放联网能力。

## 当前执行门

| 顺序 | 门 | 状态 | 通过条件 |
| --- | --- | --- | --- |
| 0 | `SV1-0` 恢复与数据安全 | 已完成 | 结果只在 STATUS/CHANGELOG 保留，不重开迁移或全局 cleanup |
| 1 | 文档与 memory 健康治理 | 已完成 | 当前事实、未来步骤、稳定合同和历史重新归位；无代码/gate 变化 |
| 2 | 首个编号帧动画用户实机 | **当前唯一活动门** | 用户确认真实 managed `.osk` 的动画、选择切换与 selected 坏包回落；产品另行决定是否扩入真实 beatmap-local 格式 |
| 3 | `SV1-1` 下一玩家可见组件 | 尚未选择 | 门 2 通过后由产品选定一个组件，再冻结最小切片与验证面 |
| 4 | `SV1-2`～`SV1-7` | 未完成 | 按以下依赖顺序分别过门，不并行宣称完成 |

在门 2 闭合前不启动新组件、不修改 G1、layout、shared codec、scene/script 或 canonical fallback authority。

beatmap-local 的相对 provider 顺序是已有自动合同，但当前真实 `WorkingBeatmap` 只产生不解析 `[Bms]` 的 `LegacyBeatmapSkin`；仓库也未定义 `.bme` 的逐谱侧车格式。因此现有注入式 fixture 只证明 provider-order，不证明 BMS 谱面本地素材已可用；若选择实现，必须作为独立作者格式/生产 adapter 纵切重新冻结。

## 未完成实施顺序

### SV1-1：共同合同与玩家可见纵切

当前只完成 BMS 普通短键的 selected-package `Provide/Inherit`、逐组件 fallback、精确 package authority 与编号帧动画纵切。后续按“一次一个玩家可见组件”推进：

1. 产品先选组件，明确它是 critical 或 optional、允许的 `Provide/Inherit/Suppress` 状态及最小可玩回落。
2. 只补该组件必需的 neutral slot/config/resource mapping，不借机扩完整 manifest、layout 或 event runtime。
3. 资源必须绑定 exact package revision，经过 containment、文件/帧/解码/预算验证，在后台准备完成后发布；失败保持旧视觉或逐组件 fallback。
4. beatmap-local → selected → ruleset resources → protected built-in 相对 authority 不变；不同 package 不得拼件。
5. 自动 gate 后必须补受影响 keymode、选择/切换/回落的用户实机确认；通过前不计下一项新增可见功能。

验收：该组件在真实 gameplay 进入用户选中的 managed package 链，损坏/缺失/越权/超预算均不破坏可玩性，且未实现的 slot/runtime 不被描述为已完成。

### SV1-2：G1 安全存储与原子重载

依赖：保持 `SV1-0` 数据处置结论与当前 `.osk` 路径稳定；不得从异常期存档整包恢复。

1. 分离 managed 与 external authority；external absolute root 使用 `NativeStorage` 且只读。
2. managed import/rename/delete 做 resolved-root containment、冲突拒绝及 reparse-point/symlink 风险处理。
3. scanner 只维护自身 authority，不删除 `.osk`、未知来源或其它 authority 的 Realm 记录。
4. `SkinManager` 从正确 authority 建立 folder store；非 folder 与 `.osk` 路径保持字节一致。
5. ini/manifest/script/素材的新 revision 完整验证后一次切换；失败只销毁 provisional revision。
6. UI 明确区分 managed 删除文件与 external 解除注册。

验收：真实选择链、重启、切换、rename/delete、缺件、原子替换和备份数据根均通过自动与人工验证。

### SV1-3：playfield/BGA layout descriptor

依赖：P1-K 先给出可信 keymode source/diagnostic/override 并修正 lane timeline 上界；P1-L 保持 BGA 内容 authority 不扩张。

1. 定义 neutral `GameplaySkinLayoutContext` 与唯一 `BmsGameplayLayoutSnapshot`，覆盖 side/style、playfield/stage/lane/judgement/cover/BGA/HUD rect 和 stable lane identity。
2. playfield、gauge、combo fallback、BGA、scene/script 全部消费同一 snapshot，禁止各自 `CreateDefault()` 推导几何。
3. skin geometry 做 finite、正值、范围、屏内与不重叠校验；非法字段逐项回落默认。
4. 矩阵覆盖 5K/7K P1/P2/CenterP1/CenterP2、9K BMS/PMS、14K 双 deck/S1/S2/centre gap。
5. BGA decode/timeline/seek/POOR 留在引擎；多个 viewport 只能镜像同一只读 content authority。

验收：各矩阵格的 lane order/bounds/scratch role/BGA/gauge/combo 无冲突，并完成常见宽高比、DPI、每轨输入/keysound 与 BGA 实机检查。

### SV1-4：mania-compatible ini 共同层

依赖：SV1-3 冻结 stable lane/layout context；现有 legacy decoder 生产行为先由 fixture 保护。

1. adapter-first 导出带 explicit presence 的 neutral snapshot，稳定后再抽 shared codec；不在第一刀切换 mania 生产 tokenizer。
2. mania/BMS 共同字段使用同一 codec/resolver；BMS 独有字段进入版本化 extension schema。
3. 统一 mania column、BMS lane token 与 stable lane ID；renderer 不再拼接 lane 字符串。
4. 未覆盖共同件按冻结 mapping 显式进入 compatibility fallback。
5. 未知键、非法值、缺素材和不支持 capability 产生结构化诊断，加载继续 fail-open。

验收：同一 fixture 在 mania/BMS 共同件上解析一致，旧 `.osk/[Mania]` 与 `.osk/[Bms]` reference 继续可用。

### SV1-5：声明式 scene、动画与事件 ABI

依赖：SV1-3 layout snapshot 与 SV1-4 shared config 可用；引擎事件 authority 已明确。

1. 以稳定 node type allowlist 提供 sprite/container/text/mask、受控 effect、clip、frame animation、tween、状态机、property binding 和 template。
2. 引擎 adapter 发布 lifecycle/layout/input/object/judgement/score/timing/BGA 只读事件；attach/reload/seek/retry 必须产生完整 Snapshot/Reset。
3. global、lane template、pooled note/LN 与 ephemeral effect 分层；scroll/LN clipping/instancing 仍由引擎 host 驱动。
4. package 只能锚定 descriptor slot 或自身 scene，不能遍历 `DrawableRuleset` 父树。
5. 新 gameplay provider 使用显式三态结果，不改写 nullable `ISkin`/`Drawable.Empty()` 旧语义。

验收：只用公开 scene/event host 实现代表性的 key press、hit、LN、judgement、combo/gauge 与 BGA 装饰，dense/14K 不产生 per-note script churn。

### SV1-6：可选沙箱脚本

依赖：SV1-5 declarative runtime 先覆盖不需要脚本的共同能力；脚本选型须先做隔离和性能 spike。

1. 脚本只读 snapshot/event，只能操作获准视觉节点。
2. capability 由 package request、host allowlist、当前支持与 per-skin authorization 共同决定；网络、任意文件、反射、进程、线程、原生库、Realm/config/gameplay mutation 永久禁止。
3. gameplay clock、确定性 seed、seek/retry/reload 状态重建必须固定。
4. VM 提供可抢占 instruction/heap quota；资源、scene/effect pool 和每帧预算有界。
5. 编译与 IO 不阻塞 update thread；异常/超限只熔断脚本/scene 层并 fallback。

验收：权限逃逸、无限循环、内存、异常、determinism、seek/retry、热重载和低端硬件预算通过。

### SV1-7：双包、作者套件与 release gate

依赖：SV1-2～SV1-6 的产品能力真实存在，不用目标包反向伪造未实现 runtime。

1. 制作同时含 mania/BMS 的 `oms-simple.osk` 与 `oms-complex.osk`，均走普通导入/导出链且保留可编辑源。
2. `oms-simple` 只保留最小可玩件并 suppress 可选视觉，随发行物只读携带，构建/启动校验并可原子恢复。
3. `oms-complex` 覆盖公开 slot/event 表达上限，不使用私有 C# provider、隐藏资源或内置专权。
4. 交付模板、schema/event/layout/预算参考、validator/diagnostics 与打包说明。
5. `oms-simple` 达到 mania/BMS parity 后，程序化主题渲染退出产品链。

验收：缺失/损坏用户包仍可玩；canonical 包完整性失败进入明确安装修复；双包、第三方包、启动/切换/reload、全 keymode、BGA、脚本性能与人工视觉全部过门。

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
- 旧 F/G 术语只作为 CHANGELOG/恢复审计索引；当前执行只看 `SV1-*` 与本页当前门。
