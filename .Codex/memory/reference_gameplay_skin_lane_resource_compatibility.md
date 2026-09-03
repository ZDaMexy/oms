---
name: reference_gameplay_skin_lane_resource_compatibility
description: Skin V1 BMS/mania lane-resource production candidate、resolved material、exact preparation borrow/retirement、source isolation/async reload与9K/14K编址地雷
metadata:
  node_type: memory
  type: reference
---

# gameplay skin lane-resource compatibility 召回

权威状态与硬约束见 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md) / [PLAN](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md) / [CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)；作者当前格式见 [SKINNING](../../doc_md/other/SKINNING.md)。本文件只保存实现地雷。

## lane-resource production边界

- legacy closed field catalog只含note、LN head/body/tail、key up/down六个逐lane资源字段；它现在是C4 production material resolver的compatibility adapter输入，不是第二张public slot表。declaration仍不等于已验证`Provide`。
- candidate必须绑定exact C3 topology与public target，防御性复制并按logical lane/catalog确定排序；缺项为`Absent`，显式空名仍为`Declared`。public duplicate由shared codec拒绝；安全字符串不展开资源名。
- 只读实际 `LegacyManiaSkinDecoder` / `BmsSkinDecoder` output。禁止从会合成缺失 mania bucket 的 `LegacySkin` production lookup 反推 presence。
- public legacy mania factory 只是跨 ruleset 程序集的 process-local CLR bridge，不是作者/plugin/package/manifest/script ABI。

## 候选矩阵

| 模式 | 固定顺序 |
| --- | --- |
| 5K | `[Bms] → Keys:6 full visual → Keys:5 key-only → canonical marker` |
| 7K | `[Bms] → Keys:8 full visual → Keys:7 key-only → marker` |
| 9K BMS/PMS | `[Bms] → Keys:9 → marker`；不得重复同一 Keys9 key-only candidate |
| 14K | `[Bms] → Keys:16 full visual → 同一 Keys:8 bucket 分别投影两个 deck → Keys:14 key-only → marker` |

- P2/CenterRightScratch full bucket 使用 global visual index，stable lane ID/action 不变。
- 14K deck bucket 使用 group-local visual index；同一个真实 Keys8 bucket 投影两次，因为 legacy decoder 不保留第二个 duplicate Keys8 section。Keys8 必须先于 Keys14，才能优先保留 scratch/deck-local presentation。
- candidate/provider已由`BmsGameplayResolvedNoteMaterialPreparer`用于production Note/LN resolution；marker仍只是尚未装载的canonical authority终点，不是`oms-simple` snapshot。不得把candidate生成本身冒充资源验证或C5 optional host。
- candidate lookup可读取exact snapshot中已经显式存储的logical/visual、global/group-local index，但不得从bucket序号、legacy token或候选顺序重建topology/geometry。stable identity与最终rect只能来自同一C3 layout publication；资源层不能形成第二solver。

## 9K raw token 地雷

legacy`BmsLegacySkin`对无scratch 9K继续使用raw`0..8`；public target只接受canonical`1..9`，两者由`bms-gameplay-skin-nine-key-index.v1`显式双向映射。5K/7K/14K legacy普通键仍是`1..`；stable LaneId不要与raw token混成ABI。

未知映射版本必须fail-closed；绝不能同时静默接受`0..8`与`1..9`，因为两套编号的`1..8`含义重叠。

## decoder-time provenance 地雷

- shared legacy mania 只接受区分大小写的 `NoteImage{0-based column}[H|L|T]` 与 `KeyImage{column}[D]`，column 必须是当前 `Keys` 范围内的 canonical ASCII 十进制；前导零、符号、Unicode lookalike、越界与其它后缀仍可按既有 parser 留在 compatibility dictionary，但不能进入 private sidecar。
- native `[Bms]` 只把 note、LN head/body/tail、key up/down 六个 exact prefix/suffix 组合写入 sidecar。raw lane token 服从既有 regex 的 `\d+`、`S`、`S2` 并且不做 normalization；因此 `01` 或 Unicode decimal token 即使被 decoder 接受也不会冒充 projection 查询的 ASCII `1`。`LaneBackground`、`LaneDivider` 与 stray suffix 等 regex-compatible key 继续只存在于 `ImageLookups`。
- 两侧 factory 均只读 decoder-time accepted sidecar。public dictionary 在 factory 前被 forge、overwrite、remove、clear、replace 或 late-add 都不能改变 provenance；显式空值与 valid duplicate-last 仍保留。invalid enum/composite/index/token/null 必须在任何 sidecar/compatibility 双写前原子拒绝。
- 这仍是 process-local declaration bridge，不是安全、文件或渲染 authority。文件存在性、containment、解码预算、materialization、component ownership、slot `Provide` 与 fallback winner 均属于后续层。

## resolution / revision-owner 地雷

- selected-package candidate factory保持冻结顺序并由唯一production resolver组合selected public/legacy → ruleset resources → protected/canonical → programmatic。legacy beatmap direct visual在resolver外预准备为更高compatibility authority；新beatmap-local public source不存在。factory绝不能制造名为`oms-simple`的provider。
- 缺 bucket/field 直接 `Inherit` 且不得调用 materializer；显式空字符串仍是 declaration，必须交给 materializer 做基础验证。ini declaration 永不产生 `Suppress`，取消异常必须传播。
- source-aware reference 至少区分 source、Keys、stable lane ID、field 与 raw resource name。同一 raw name 在 BMS/mania 或不同 bucket 下可以有不同结果，不能只按字符串名跨 authority 共用；resource name 不得进入稳定诊断、JSON 或安全字符串。
- materializer 返回前必须由一个 revision-scoped owner 取得 component 所有权并完成基础验证。winner 与被 outer validator reject/throw 的 component 都只是 resolver/consumer 借用，延迟到 owner dispose 回收；resolver/provider 不单独 dispose。
- BMS exact selected preparation按layout snapshot/generation复用，但资源寿命不等于cache/task寿命：`BmsLegacySkin`维护waiter/borrower计数，完成revision只经幂等`BmsManagedPackageNoteRevisionBorrow`进入material publication。borrow沿material preparer→publication→prepared carrier→layout owner移动，前任每次转移后必须清空持有；构造/prepare异常、取消、dispatch拒绝、commit guard失败与owner teardown都exactly-once退役。
- active/provisional owner不能交叉：失败reload只dispose新provisional owner，旧active owner继续存活；成功原子替换先detach superseded consumer，再dispose旧owner；teardown同样先detach后dispose。该规则已由C2`SkinCurrentRevision` participant/work lease落到production并签发，覆盖BMS async note/materializer与真实owner retirement；C3/C4/C5又把package、唯一layout、resolved material与prepared scene作为不可分quadruple加入同一barrier。C6新增consumer必须继续加入。
- 成功publication的borrow由`GameplaySkinLayoutRevisionOwner`持有；`RulesetSkinProvidingContainer.Dispose`必须先让base container完成renderer子树detach/dispose，之后才释放owner。`BmsLegacySkin.Dispose`可以封门、取消并joinworker，但有active borrower时只把generation标为退役，waiter/borrower未同时归零不得清理prepared revision/TextureStore。
- production resource consumer若需要layout，只能从enclosing exact owner的`CurrentPublication`取得neutral/typed同一引用；注入另一owner的carrier、建立第二provider、把isolated compatibility snapshot升级为exact或在transformer/consumer另存可替换snapshot都必须fail-closed。详见[[reference_gameplay_skin_layout_snapshot]]。

## production source-bound note/LN 纵切地雷

- ordinary note 是首个 production 纵切，之后同一路径扩到 `LongNoteHead`、`LongNoteBody` 与 `LongNoteTail`。不能分别向聚合 skin 查询 config 和 texture：声明可能来自 selected package、同名 texture 却来自 ruleset/内置 package，形成跨 provider 拼接。declaration、frame discovery 与 texture decode 必须绑定同一个精确 package revision。
- body 还要求 resolved `LongNoteBodyWidth` 与素材帧处于同一 source-bound material/revision；解析后 `skin.ini` identity 必须参与 revision authority，发布后 renderer 不得再读 aggregate width。缺失或非法宽度只在该 material 内用 typed reason 回到 `0.5775`，不得从低层裸 width 拼接。
- `GetTexture()` 之后才检查输入大小、尺寸/像素或累计预算已经太晚；先读受限元数据并做 pre-decode gate，解码后再核对实际值。runtime cap 也不等于 importer 的总解压字节、解压比或 zip-bomb gate。
- Realm 的 hash-backed package 要先冻结不可变文件名→内容身份快照，再由该 revision 独占 private resource cache；大小写重复、路径越界、身份冲突或缺 blob 均只让对应 slot `Inherit`，不能从另一 package 补齐。
- current reload的Realm/blob/held filesystem I/O、parser、texture decode与materialization都必须止于background prepare；update thread只做已准备且可逆的引用交换。BMS async note/materializer还须把generation、cancel、callback ownership与work lease保持到真实worker退出，不能只释放未采用Drawable。`BmsAsyncNoteDrawable`可能位于ancestor `!IsAlive`的hitobject下，首次Ready admission及source invalidation rebuild都要经GameHost scheduler；source event先同步进入work admission gate，推进generation并exact claim旧owner/CTS，再调度fresh rebuild。prepare install与finish publish比较captured generation；participant shutdown/dispose也先terminal并在同一gate推进generation/claim work，从合同上消除CTS double-dispose窄窗。Dispose不能退回不推进的local scheduler或留下晚到publication。
- C5已把shared document、catalog/resolver结果、prepared scene/read-only event与BMS/mania migrated consumer加入C2/C3同一package+layout+material+scene publication；C6 script与最终ini/manifest/scene/script/全部素材整包门仍须逐批加入，只有C6才关闭。
- preparation cache仍按exact `BmsLegacySkin` instance/revision使用；active instance不观察原位来源变化，Settings manual Reload构造new instance/revision。same-instance refresh或逐组件A→B不得绕过统一barrier。
- `Box` 继承 `Sprite`；测试若只用 `drawable is Sprite` 会把程序化 fallback 误判为用户贴图，必须验证 source-bound 类型/纹理身份或明确的宿主状态。

## production source-bound 边界

- native BMS 普通短键是首个 package-scoped production 窄纵切，随后已扩到长条头/身/尾；当前自动/实机 gate 和精确测试数字只看 P1-A STATUS/CHANGELOG，不在 memory 重抄。
- C5已实现shared codec、28项public catalog、显式三态、BMS/mania migrated resource parity、prepared scene/read-only event与逐slot capability；BMS 28项均有route，Mania 23项Supported + `object.mine`、`playfield.turntable`、`playfield.laser`、`bga.viewport`、`bga.frame` NotApplicable。C6 script/final package gate与`oms-simple` authority仍未成立；测试不得访问或写入生产数据。
