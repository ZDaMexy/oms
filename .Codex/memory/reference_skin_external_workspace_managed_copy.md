---
name: reference_skin_external_workspace_managed_copy
description: external Folder Skin Workspace、exact registry、只读选择/注销与single-v3 ManagedCopy地雷
metadata:
  node_type: memory
  type: reference
---

# external Workspace 与 ManagedCopy 地雷

权威状态、退出门与C2冻结边界只看[P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)、[PLAN](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md)和[CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)；C1冻结边界见[2026-08-13完成交接](../../doc_md/other/SKIN_SYSTEM_C1_COMPLETION_HANDOFF_20260813.md)。本页只保存最容易误改的稳定合同。

## authority与选择

- resolver只签发词法合格的external capture request；service-owner token只授权本服务管理Realm declaration。真正source authority来自仍持有的no-follow root/ancestry session及其fresh validation，digest/record/path本身都不是capability。
- register必须在同一次held capture取得exact capsule、logical manifest与physical proof，factory验证后才发布exact service-owned Realm记录；失败不留记录，不自动选择。active实例只读capsule，不重开external path。
- external selection把慢物理capture与最终publication拆开：`CaptureExactSetForSelection`不持有coordinator lease，也不授予publication authority；准备阶段必须一直持有managed-root authority、完整registry中每个external的physical proof和目标package session。这样较新的不同请求可立即推进generation、取消旧准备并按latest-wins提交，不被旧请求的I/O占住线性化边界。
- final completion必须取得fresh selection lease，并复验generation、generic mutation reservation epoch、managed root、目标package proof、完整registry declarations/generation/digest与全部held physical proofs；只有这些仍exact才在同一Realm线性化点提交。目标`Name`/`Creator`/`Hash`必须来自本次held package解析出的fresh metadata，不能把请求时的陈旧展示字段发布回Realm。
- 上一条只描述selection/reselection。same-ID current Reload虽复用held package/full registry proof，却只发布prepared in-memory owner/revision；registration的`Name`/`Creator`/`Hash` observation必须保持exact且成功Reload不得写Realm，否则既破坏observation语义也把可失败I/O塞入commit barrier。
- dropdown显式候选与random/next/previous隐式候选必须分离：external可显式选择和configured restart，但hotkey/random永不选中。same-value不冒充reload；current源变化后须在安全screen显式点击Settings的`Reload current skin`，或切走再选/configured restart做fresh capture，不存在watcher。
- Folder Skin Workspace row与确认框只复制committed `Guid`、immutable label、kind和capability hint；不得持有`Skin`、`Live<SkinInfo>`、path、manifest、journal或physical proof。Open/action全部由manager按ID fresh重读。
- Workspace records与journal support读取是manager-owned并发只读worker，不占mutation slot；manager shutdown必须先封门，再cancel、观察并同步join全部读取，UI关闭只取消自身refresh lifetime。managed Open在initial/final Realm view都要复核exact record与normalized path唯一，同路径重复声明时禁止调用host。

## registry与mutation

- exact registry snapshot是有界完整集合，绑定declaration digest、deterministic generation与持有中的全部physical sessions。selection与Rename/StagedImport/Delete/ManagedCopy都要把该snapshot保持到final Realm事务并复验同一集合；合法非重叠external不再触发旧global block，foreign/null/overlap/count/path预算或generation/identity drift仍fail-closed。
- noncurrent Unregister仍直接pure-Realm compare-remove exact service-owner记录，不得resolver/capture/open/write/delete source或dispose prior owner。current Unregister先由统一revision transaction发布protected fallback并等待old `ConsumersDetached`，再fresh compare fallback/current generation/full registry/exact record后pure-Realm remove；任一步失败借old-revision operation lease恢复exact A并保留record。source missing/unreadable/drift不妨碍其它proof exact时移除陈旧注册，但任何结果source零I/O、零变化。
- journal support snapshot必须inspect-only；读取UI状态不得调用会执行forward/rollback的recovery。只有fresh inspection证明唯一existing handler可安全动作时提供manager-owned Retry；状态、reason和bundle不得含绝对path、record/operation ID、physical identity、entry文本或native异常正文。

## single-intent ManagedCopy

- 唯一产品入口只接external record ID和用户明确的managed direct-child target；operation ID与staging path由manager生成。文件bytes只从fresh capsule读取，目录/空目录/name/kind只从同次capture的bounded immutable manifest读取，绝不重开external source。
- destination只经held root、handle-relative、no-follow/no-replace primitive重建；首个provisional root或byte前必须durable写入并exact reload同一canonical v3 intent。禁止第二个staged-import journal、任意path、merge、overwrite、auto suffix或目录年龄猜测cleanup。
- caller cancellation只在首字节前可精确rollback；首字节后由journal/recovery收口。Copying只在durable root identity证明exact empty root时可回滚；完整capsule/manifest/content证明才可forward，任何非空不完整或foreign/replacement保持Ambiguous/freeze。
- live writer在CaptureStaged建立新完整authority后必须释放旧writer-tree descendant/root handles再move；held session的Validate不得永久捕获首次调用token，后续使用传入token或`CancellationToken.None`。这些NTFS细节有回归，不可用放宽share或path reopen规避。

## C1关闭后的边界

- external Workspace、ManagedCopy与ordinary noncurrent Unregister已随C1关闭；C2 same-ID reload、current external unregister与全consumer publication/detach/retire也已签发，燃尽为`2/7 closed，C3 active`。watcher始终不属于该能力。
