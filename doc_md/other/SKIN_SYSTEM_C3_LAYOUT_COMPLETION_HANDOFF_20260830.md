# Skin V1 C3 唯一 gameplay layout 完成交接（2026-08-30）

> 本文记录 `P1-A / Skin V1` 七个持久 campaign 中 `C3` 的完成边界与验证证据。当前燃尽与下一步始终以 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md) 和 [P1-A PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md) 为准；本文不是后续执行 prompt。

## 完成结论

`C3` 已关闭以下同一纵切，不能拆成相互独立的“基础设施完成”：

1. P1-K 的 Skin 前置由 parser/converter truth 闭合。lane keysound timeline 的逻辑上界统一为 `BmsRuleset.GetLaneCount()`；5K/7K 最右键、9K 全部 lane、14K 右 deck 末键与 `Scratch2` 不再因 key count 边界静默丢失。
2. sparse keymode 由 `BmsKeymodeResolution` 按可追溯 source/precedence、authoritative host/importer显式 override seam和稳定脱敏 diagnostic 决定；证据不足时 fail-closed。该seam不包含终端用户UI，普通导入入口当前仍传`null`；layout、skin 与 runtime 不重新读取 BMS，也不从最高出现 channel、枚举序号、总 lane 数或布局宽度猜 keymode。
3. core 只发布一个 ruleset-neutral、构造后防御性不可变的 `GameplaySkinLayoutSnapshot`；它与同 revision 的 adapter 组成一个 `GameplaySkinLayoutPublication`，由唯一 `GameplaySkinLayoutRevisionOwner` 持有。
4. BMS 只经 `BmsGameplayLayoutSolver` 产生最终 snapshot；mania 只经 `ManiaGameplaySkinLayoutPreparer` 适配真实 single/dual stage vector。production consumer 不再各自创建 profile、默认 geometry、固定 rect 或按 drawable 尺寸二次求解。
5. package/current revision 与 layout revision 作为不可分割 pair 进入 C2 background prepare、fresh barrier、update-thread commit、participant generation、lease/detach/retire 协议。失败保留 exact 旧 package+layout pair；成功 publication 才允许 late attach。

## Authority 与 identity

`GameplaySkinLayoutContext` 固定以下输入：exact ruleset/native context 与 keymode、lane topology、presentation style、safe bounds、aspect/DPI、package/current revision 和 layout revision。snapshot 内部集合在构造时复制并只读暴露，consumer 不能改写或再组装另一份结果。

- `LaneId` / `GroupId` 继续来自既有 lane identity/topology，只作为 solver 输入；C3 没有创建第二组稳定 ID。
- stable ID 跨 style、视觉重排、geometry 以及 topology-preserving revision 保持不变。
- logical/visual index、global/group-local index 均显式存储和使用；禁止借枚举位置、`RelativeStart`、enum ordinal 或 total lane count 反推。
- Mirror/Random/S-Random 只改变 mod 后对象目标 lane，不改变固定 playfield topology；对象、shared keysound store 与 skin lookup 最终解析到同一 `LaneId`。
- mania dual stage 的 special key 保持 stage-local 语义，不用全局 modulo 或总列数推导。

## 唯一 geometry 与 consumer

BMS 矩阵覆盖 5K/7K 的 P1、P2、CenterP1、CenterP2，9K BMS/PMS，以及 14K 双 deck、S1、S2 和 centre gap。mania adapter 覆盖真实 single/dual stage vector。

同一 exact snapshot 已由以下 production 面消费：

- BMS playfield、stage/group/lane、Note、LN head/body/tail、barline、hit target、judgement line/display、lane cover、pre-start preview；
- BMS 最终 BGA viewport/rect、gauge、combo 与 HUD safe placement；BGA 内容、timeline、seek 和 gimmick 播放仍归 P1-L；
- mania playfield、stage、column/flow、note、hold、barline、hidden、hit target、judgement、adjustment、touch input 与 gameplay HUD；
- core ruleset/provider 适配与真实 renderer root。

`BmsPlayfieldLayoutProfile` 只作为 solver 内部的配置/兼容输入存在；isolated compatibility 入口被显式标记且不能进入 exact production root。BMS transformer 不再转交第二份 snapshot，mania consumer 会校验 publication 必须与 enclosing owner 的 current publication 引用完全一致；注入另一 owner 的 snapshot、同一 root 二次 prepare 或无 publication 的 production construction都会 fail-closed。

## Geometry 安全

每个可配置字段独立验证 finite、正值、合法 range、安全 screen bounds 与字段间 non-overlap。单字段非法只选用该字段的确定程序化 fallback，并产生稳定脱敏 diagnostic；最终仍一次产生一份完整 snapshot，不允许 NaN/Infinity/负尺寸传播，也不允许部分新/部分旧 snapshot 拼接。

常见、极窄、极宽 aspect，DPI scaling 与 safe-area 矩阵均覆盖 14K 两个 field、双 scratch、centre gap、BGA 与 HUD。fallback 结果同样受完整 snapshot 不变量约束。

## C2 revision 协议扩展

- 可失败的 geometry 解析、求解和资源准备只在 background prepare 发生；update thread 只提交已准备的 immutable publication 引用。
- prepare 前后与 commit 时复核 participant generation、current selection、exact source/content revision、package revision 和 layout revision；任一漂移保留旧 pair。
- attach 前执行 fresh barrier；未准备的 attached layout consumer 不能进入 live root。commit 前安全 detach，late attach 只取得已提交 publication 与 lease。
- 旧 owner 在最后 consumer/work lease detach 后 exactly-once retire；跨 revision holder 不允许提前释放资源。
- live gameplay/gameplay preview 仍在 source prepare 前拒绝；没有 watcher，也没有为 layout 测试开放 live reload。
- same-ID 三源 latest-wins、reentrant、cancel、scheduler fault、shutdown、current external/managed/ordinary mutation的失败原子性继续沿用 C2 合同。

## P1-K 发声证明

末端 lane timeline 覆盖 visible note、LN head/tail armed entry、invisible object、mine 及相邻 armed timeline。真实 BMS 玩家与 autoplay、以及 BMS 转 mania 路径均进入同一 shared keysound store 并实际发声。C3 没有顺带修改 sample pool、判定或 binding；parser/converter 仍是 lane/keymode/timeline 唯一 authority。

## 最终验证

| 验证面 | 结果 |
| --- | --- |
| P1-K decoder/converter/import/cache/authority | **176/176** |
| BMS → mania projection | **24/24** |
| BMS native shared keysound 实际发声 | **14/14** |
| converted mania shared store 实际发声 | **2/2** |
| BMS C3 relevant/product matrix | **316/316** |
| mania solver/production root | **27/27** |
| core C2/layout/staged/provider focused | **56/56** |
| product concurrency/mutation | **17/17** |
| storyboard explicit keymode authority | **7/7** |
| formatter 后 core/BMS/mania 宽关键复验 | **47/47、235/235、51/51** |
| 最终 owner 审计红绿硬化后 core/mania/BMS critical | **48/48、51/51、37/37** |
| core canonical `~Skin` | **1164/1170**；6项与既有精确基线相同 |
| mania canonical `~Skin` | **209/209** |
| mania full | **854/858**；4项为既有 AutoGeneration replay frame-count 基线 |
| BMS canonical `~Skin` | **802/802** |
| BMS full | **1763/1763**；`--blame-hang 5m`全数完成且无 sequence |
| `osu.Desktop.slnf` Release | **0 error / 9 known MessagePack warnings** |

targeted formatter 覆盖六个归属工程的实际变更文件。formatter 曾把 `BmsPlayfield` 的 `[Cached]` 私有 store 机械改写成 DI source generator 不支持的 field-attributed auto-property；该改写已恢复为显式 backing field，随后 Release 与以上关键集复验通过。它不是 runtime 产品合同的放宽。

最终owner审计先以真实shared owner测试复现并跑红cached descendant可绕过ruleset helper直接二次发布的问题；one-shot约束随后下沉到`GameplaySkinLayoutRevisionOwner`的同一锁内。Mania stage vector/topology/environment也移入fresh work lease与participant generation之后的solve callback；BMS managed入口在任何config/skin/solve前对称拒绝compatibility token。explicit compatibility仍只是detached solver/visual fixture opt-in，不能进入exact production tree。

独立终审结论为 blocker/major/moderate/minor **0/0/0/0**：唯一 publication、production bypass、P1-K authority、participant/owner、并发和真实 consumer 均无未闭合问题。

闭门后的产品价值复核另区分出一项不属于C3安全/layout缺陷的P1-K后续可用性风险：目前没有终端用户可操作的keymode correction caller，模糊sparse `.bms/.bml`只能fail-closed。C3证明的是override authority可由host/importer显式传入且会贯穿真实converter/renderer，不宣称普通用户已经能在UI中设置它。

## 未在 C3 实现

C3 没有实现 shared codec/public catalog、完整 `Provide/Inherit/Suppress` resolver 与 mania parity、beatmap-local 作者格式终态、scene/animation/event、剩余 optional slot、sandbox/script VM、canonical 双包或 Authoring Kit。程序化 `OmsSkin` 继续保留；最终 ini/manifest/scene/script/全部素材的整包门仍只在 C6 关闭。BGA 内容/timeline/seek 与 gimmick 播放继续归 P1-L，menu/shell/background 不是作者 gameplay layout surface。
