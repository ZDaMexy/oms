---
name: reference_gameplay_skin_scripts
description: Skin C6 菜单整包验证、脚本撤销、固定时钟、持久化中断和纹理上传诊断
metadata:
  node_type: memory
  type: reference
---

# Gameplay skin 脚本诊断召回

现行合同见 [P1-A TECH](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)，语言/工具见[作者说明](../../doc_md/other/SKIN_SCRIPT_V1_AUTHORING.md)，完成状态只读 [STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)。

- 菜单 Reload 的旧坏 scene 测试曾注册 synthetic layout participant 才拒绝 B；无 gameplay host 的真实 Settings 路径会发布坏 B。诊断应先检查 package stage 是否已解析同一 exact manifest/scene/script/resource，再看 layout-specific prepare；不能由测试 participant 的成功反推菜单已验证。
- `Texture.SetData(upload)` 排队到 draw thread，并由 framework 在上传完成后 Dispose。调用方 `using TextureUpload` 会提前释放 queued pixels，headless tests 不一定看得出来。成功交接后不 Dispose；只有未交接的异常路径由调用方释放 upload/texture。
- 撤销脚本不等于引擎 Snapshot/Reset；停止回调仍会留下旧 rotation/scale/alpha，调用 `resetStateMachines` 又可能丢掉 C5 当前状态。只撤去 script 触碰的内层 transform，重投影现有 state assignments/tracks/bindings/variants；native pooled owner、BoundObjectId 与 gameplay authority 继续归原 host。
- 暂停的 `FrameStabilityContainer` 不遍历 scene children；仅在 scene Update 比较授权会保留旧 VM/overlay。权限变化经 GameHost update scheduler 恢复，但必须以一次 volatile 读取的 effective epoch 去重：decision 先发布、Changed 后到时，active host 可能已经消费新权限和事件。迟到通知不能再次清空状态，撤销/再授权同 mask 的 ABA 则必须重建；operation 写序号不能替代此 epoch。
- 单 `set` 可以扩展成上千个 template/native-pool clone。VM set-count 不能代表实际绘制属性应用预算，必须在 fanout 后、写任何 node 前计算整帧实际数量。
- 以 renderer 每帧执行一次可变脚本会让 `state += 1` 或 PRNG 随 FPS 分歧。`FrameStableComponents` 的 scene host 先于 playfield 遍历，空 queue 仍可能稍后收到同 time judgement；tick 必须等 gameplay time 严格越过后封闭，先补旧 baseline 的 tick 再折叠新 edge。fractional renderer timing 不能倒灌先前 tick。
- 只禁止 `read event-kind` 不足以保护 events 权限：无 grant 时每条真实事件仍调用脚本，作者可用 state 计数观察事件数量和时间。真正的事件回调派发也必须 gate，内部 Snapshot/Reset 重建照常。
- 授权 `.pending` 文件或目录可能代表失败/中断的撤销替换；不能忽略它并从旧主文件恢复 grant。`File.Exists` 对目录为 false，需识别两种存在形式。磁盘写失败的 grant 也不能残留到 desired 状态、被下一次无关保存意外激活。
- `InvalidDataException` 在 .NET 中直接继承 `SystemException`，不是 `IOException`。存储 schema/预算的有意拒绝须包含这个窄异常；读取 `JToken.Value<string>` 前先查 Type，避免损坏授权文件触发 `InvalidCastException` 并让后台 Ready/Shutdown 失控。
- C1 mutation 要求 scanner 真实 metadata/hash。只造 `SkinInfo`、设置 scanner owner 却让 Hash 空的 fixture 可以选择，但不具备 Rename/Delete 的原生资格；应通过真实 discovery/scanner 创建记录，不放宽产品准入。
- ordinary Realm blob 的 hash 不保证当前文件大小。先 `.Get()` 再交 capsule 配额会在拒绝前分配超大 buffer；复用 capsule 的 stream length/逐块 cancel/预算，再比较 captured hash 与 Realm 声明，覆盖选择、Reload 和 current mutation 内容复核。
