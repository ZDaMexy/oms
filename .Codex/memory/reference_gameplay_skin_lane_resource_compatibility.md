---
name: reference_gameplay_skin_lane_resource_compatibility
description: legacy lane-resource 候选、9K/14K 编址、source-bound material 与 borrow 生命周期地雷
metadata:
  node_type: memory
  type: reference
---

# Lane-resource compatibility 地雷

现行候选、resolver 与 publication 合同见 [P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md) 的 ini 兼容章节；作者 ID/能力见 [public catalog](../../doc_md/other/GAMEPLAY_SKIN_PUBLIC_CATALOG_V1.md)，进度只读 [STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)。

## 查错贴图先看 source 与 index

- legacy closed field 是 note、LN head/body/tail、key up/down；它是 compatibility 输入，不是第二张 public slot 表。Declared 不等于已验证 Provide，CLR bridge 也不是作者/plugin/script ABI。
- BMS 顺序为 5K：Bms→Keys6→Keys5；7K：Bms→Keys8→Keys7；9K：Bms→Keys9 且不重复；14K：Bms→Keys16→同一 Keys8 分投两 deck→Keys14。真实 ruleset/protected/canonical/programmatic 层由 resolver 提供，旧伪 canonical marker/provider 已删除。
- P2/CenterRightScratch 的 full bucket 用 GlobalVisualIndex；14K deck 用 GroupLocalVisualIndex。同一个 Keys8 投影两次，因为 legacy decoder 不保留第二个 duplicate Keys8；不能拿候选序号重建 topology。
- 9K legacy raw 0..8 与 public canonical 1..9 只经 `bms-gameplay-skin-nine-key-index.v1` 双向映射。未知版本拒绝，不能同时静默接受两套 alias：重叠的 1..8 指向不同 lane。
- source-aware reference 要区分 source、Keys、stable lane、field 与 raw name。同名资源在不同 bucket/provider 可不同，不能跨 authority 仅按字符串缓存。

## Decoder 接受事实

- Mania exact `NoteImage{column}[H|L|T]` / `KeyImage{column}[D]` 使用范围内 0-based canonical ASCII column；前导零、符号、Unicode lookalike、越界/其它后缀不进入 accepted sidecar，即使 legacy dictionary 有值。
- Native BMS 的六类 exact prefix/suffix 才进 sidecar；raw token 沿原 regex 的 \d+、S、S2，不归一化。01/Unicode decimal 即使被 parser 接受，也不冒充 ASCII 1 查询；LaneBackground/Divider 或 stray suffix 不扩成 lane-resource provenance。
- factory 只读 decoder-time sidecar。decode 后 forge/overwrite/remove/clear/replace/late-add 不改变来源事实；显式空与 valid duplicate-last 保留。合成缺失 bucket 的 LegacySkin lookup 不能反推 presence。
- absent 不调用 materializer；显式空名须进入基础验证。legacy 声明不产生 Suppress，public 三态另由 codec/resolver解释；取消传播。更多 parser 地雷见 [[reference_gameplay_skin_config_presence]]。

## Source-bound material 与预算

- 不能分别向 aggregate skin 查声明、texture 和 body width；会把 selected 声明与另一 provider 同名纹理拼起来。frame discovery、decode、解析后 skin.ini identity、body width 都绑定同一 exact revision。
- body 宽非法只在该 material 内按 typed reason 使用合同 fallback，不从低层借裸宽度；替代也必须是下层自己的完整组件。
- GetTexture 之后再查输入大小、尺寸/像素/累计预算已太晚。先受限读 metadata/pre-decode，再校验实际值；runtime cap 不等于 archive 的总解压/ratio gate。
- hash-backed package 先冻结文件名→内容身份，再使用该 revision 的 private cache；不可拿另一包补同名缺件。缺 blob/路径/身份异常按该层合同诊断或拒绝，不把 source capture 失败与单 slot material fallback 混写。
- `Box` 继承 `Sprite`：测试只断言 drawable is Sprite 会把程序化 fallback 当用户贴图。检查 source-bound 类型/纹理身份或宿主状态；composite 的颜色查内层 visual。

## Borrow 不是 cache/task 寿命

- materializer 返回前由 revision owner 接管 component；winner 与被 outer validator 拒绝的 component 都是借用，resolver/consumer 不自行 dispose。
- BmsLegacySkin 的 waiter/borrower 与 cache/task 分开。幂等 `BmsManagedPackageNoteRevisionBorrow` 沿 preparer→publication→carrier→layout owner 转移，前任转移后清空持有；异常、取消、dispatch/commit 拒绝与 teardown 都 exactly-once 退役。
- RulesetSkinProvidingContainer 必须先 base dispose/detach renderer 子树，再释放 layout owner。BmsLegacySkin.Dispose 可封门/cancel/join，但 active borrower 存在时仅标 generation 退役；waiter/borrower 同时归零才清 resource owner。
- failed reload 只回收 provisional，旧 active 保留；same-instance refresh 不得绕过 manual Reload。异步 materializer 的 scheduler/generation/callback/work lease 细节见 [[reference_skin_atomic_reload_detach]]。
- 若资源 consumer 需要 layout，只取 enclosing exact owner 的 CurrentPublication；另一 owner carrier、第二 provider、compatibility 升级或 transformer 的可替换 snapshot 均不是合法替代。

lane identity 见 [[reference_gameplay_skin_lane_identity]]，公共 resolver 地雷见 [[reference_gameplay_skin_codec_material]]。
