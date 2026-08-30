---
name: reference_gameplay_skin_layout_snapshot
description: C3唯一immutable gameplay layout context/snapshot/publication、BMS solver、mania adapter、全consumer与C2 package-layout pair地雷
metadata:
  node_type: memory
  type: reference
---

# gameplay skin layout snapshot 召回

权威当前态见[P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)，稳定合同见[P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)，C3完整边界与验证见[C3完成交接](../../doc_md/other/SKIN_SYSTEM_C3_LAYOUT_COMPLETION_HANDOFF_20260830.md)。本页只保存长期实现地雷。

## 唯一 publication

- `GameplaySkinLayoutContext`是唯一ruleset-neutral输入帧：绑定exact ruleset/native context与keymode、既有lane topology、presentation style、screen/safe bounds、aspect/DPI、scroll direction、exact `GameplaySkinPackageRevision`、topology revision和layout revision。tokens与diagnostic均为稳定脱敏值；构造后不可变。
- `GameplaySkinLayoutSnapshot`是唯一neutral geometry结果，防御性复制并只读暴露group、lane、surface、BGA viewport和diagnostic。`GameplaySkinLayoutPublication`把该exact neutral snapshot与一个`IGameplaySkinLayoutAdapter`绑定；adapter必须引用同一snapshot。`GameplaySkinLayoutRevisionOwner.Current`只是`CurrentPublication.Snapshot`的派生view，不是第二交换点。
- package/current revision与layout revision是不可分割pair。production root只允许一个exact owner/current publication；consumer不得自行new profile/default geometry/fixed rect、按drawable size重算、缓存可替换snapshot或从topology-only revision拼装第二结果。
- exact one-shot必须由shared `GameplaySkinLayoutRevisionOwner`在自己的publication锁内执行，不能只放在BMS/mania helper；否则cached descendant可直接调用`Prepare/PreparePublication`造成旧child持A、late child读B。并发首次prepare仍由admission generation保持latest-wins，但一旦exact current存在，后续prepare必须在任何work lease/solve前拒绝。

## ruleset adapter 与 identity

- BMS keymode只来自parser-owned `BmsKeymodeResolution`；唯一`BmsGameplayLayoutSolver`覆盖5K/7K P1/P2/CenterP1/CenterP2、9K BMS/PMS、14K双deck/S1/S2/centre gap。`BmsPlayfieldLayoutProfile`只可作为solver内部已验证配置/isolated compatibility输入，不能成为第二production geometry authority。
- mania adapter只接受真实、防御性复制的single/dual stage-column vector。stage与column的global/group-local logical/visual index显式保存；special key按stage-local column判断，不能用total columns、global modulo或enum ordinal反推。
- `GameplaySkinLaneId`/`GameplaySkinLaneGroupId`继续来自既有topology，没有第二组stable ID。ID跨style、视觉重排、geometry和topology-preserving revision稳定；Mirror/Random/S-Random只改变对象post-mod目标lane，不改固定topology。对象、shared keysound store与skin lookup最终使用目标lane的同一LaneId。

## production consumer

- BMS playfield/stage/group/lane、Note/LN head/body/tail、barline、hit/judgement line/target/display、lane cover、pre-start preview、BGA最终viewport、gauge/combo/HUD都只读同一typed/neutral publication。
- mania playfield/stage/column/flow、note/hold、barline/hidden、hit target、judgement/adjustment/touch input、gameplay HUD与core ruleset/provider root只读同一publication。transformer hand-off、逐consumer `Apply`、local offset或raw profile metric不能形成第二geometry。
- BGA在C3只统一最终viewport/rect；内容、timeline、seek和gimmick播放仍归P1-L。menu/shell/background不是作者gameplay layout surface。

## geometry 与 fallback

- 每个可配置字段独立验证finite、正值、合法range、安全screen bounds与字段间non-overlap。单字段非法只对该字段使用确定程序化fallback并产生稳定脱敏diagnostic。
- solve无论正常或fallback都一次产生一个完整snapshot；禁止NaN/Infinity/负尺寸传播，也禁止部分新/部分旧geometry拼接。常见/极窄/极宽aspect、DPI与safe-area必须同时守住14K双field/scratch/gap、BGA及HUD。

## C2 lifecycle 扩展

- 可失败的native stage vector/topology、environment读取、skin geometry解析、solve与资源准备都在fresh work lease内的background prepare完成；update thread只提交prepared immutable publication引用。不要在进入owner solve callback前预先发布mania/BMS topology。
- prepare前后与commit锁内复核root、participant generation、current selection、exact source/content/package revision和layout revision。attach触发fresh barrier，commit前detach使carrier失效；成功后late attach只取得已提交pair与lease。失败保持exact旧package+layout pair。
- old owner必须等最后consumer/work/operation lease detach后在update thread exactly-once retire；跨revision holder不得提前释放。same-ID latest-wins、reentrant/cancel/scheduler fault/shutdown及current mutation继续沿C2失败原子性。
- live gameplay/gameplay preview仍在source prepare前拒绝；没有watcher，也不为layout测试开放live reload。

## fail-closed construction

- 完整gameplay host必须以显式layout intent创建provider，由enclosing exact dependency scope提供唯一owner，并在renderer child load前完成publication。没有publication的production construction不得退回compatibility或默认geometry。
- prepared carrier只属于签发它的exact owner；注入另一owner carrier、同root第二provider/二次prepare、compatibility→exact升级、consumer/transformer第二snapshot hand-off或adapter不引用neutral exact snapshot都必须fail-closed。
- compatibility入口仅用于明确isolated solver/visual test，并且同样一次构造完整graph；它不能在真实provider attach前可见，也不能在生产树中后补/变更为exact。

## C3未实现

C3没有实现shared codec/public catalog、完整`Provide/Inherit/Suppress` resolver与mania资源parity、beatmap-local作者格式终态、scene/animation/event、剩余optional slot、sandbox/script VM、canonical双包或Authoring Kit。程序化`OmsSkin`继续保留；最终ini/manifest/scene/script/全部素材整包门仍到C6关闭。
