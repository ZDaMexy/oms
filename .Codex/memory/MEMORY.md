# OMS 跨会话记忆索引

> 按故障主题选少量叶子。当前进度读[主线 STATUS](../../doc_md/mainline/DEVELOPMENT_STATUS.md)，合同读所属子线；这里保留诊断线索，不复制当前状态。

## 项目与协作

- [项目总览](project_oms_overview.md) — 范围、数据根与便携标记。
- [文档治理](project_oms_docs_governance.md) — 文档失真、标题与检查器误判。
- [反馈工作流](feedback_workflow.md) — 默认产品语言、反过度防御、真机证据与本轮结束边界。
- [选歌展示与导航](project_oms_songselect_display_nav.md) — 展示层级、返回条与转谱显示。
- [内置音乐播放器](project_oms_music_player.md) — 播放器产品决定与预览音频边界。

## 皮肤恢复与存储

- [2026-07-10 皮肤恢复](reference_skin_recovery_20260710.md) — 恢复锚点、归档与重新准入；**皮肤任务先读**。
- [2026-07-13 schema 56 皮肤清点](reference_skin_schema56_inventory_20260713.md) — 只读取证、失效类型与 Realm mtime 误判。
- [skin folder authority/path preflight](reference_skin_filesystem_authority_preflight.md) — 声明/path preflight 不等于安全打开或写入授权。
- [skin package immutable revision capsule](reference_skin_package_revision_capsule.md) — 内容身份、独占 bytes 与物理捕获的区别。
- [skin folder Windows handle capture](reference_skin_windows_handle_capture.md) — held no-follow、文件身份竞态与 handle 生命周期。
- [managed skin folder scanner](reference_skin_managed_folder_scanner.md) — Observed/Valid、启动扫描与 reload 的区别。
- [managed skin folder factory/selection](reference_skin_managed_folder_selection.md) — 选择竞态、typed epoch 与 shutdown。
- [managed chartskin mutation / rename / staged import / delete](reference_skin_managed_folder_mutation_foundation.md) — NTFS move、日志恢复及 uncertain failure。
- [external Workspace / exact registry / ManagedCopy](reference_skin_external_workspace_managed_copy.md) — external 只读、注册与 ManagedCopy 复核。
- [managed skin atomic reload/detach](reference_skin_atomic_reload_detach.md) — 三源 publication、lease/retire 与调度竞态。
- [ordinary `.osk` archive import safety](reference_skin_osk_archive_import_safety.md) — archive 预检、same-hash receipt 与非对称回滚。
- [canonical 安装与用户数据保护](reference_skin_canonical_installation.md) — 缺行修复、旧数据、只读原件与更新；便携误入、缓存隔离、真实启动恢复与取消资源移交。
- [BMS 皮肤创作](project_oms_bms_skin_authoring.md) — 作者边界、可重复源文件换行、场景定位与零宽进度。
- [Skin V1 价值与工作预算](project_oms_skin_product_progress.md) — 区分效果能力、成品与创作便利度；预算和实时进度读 P1-A。

## 构建、存储与产品面参考

- [构建与测试](reference_build_and_test.md) — 测试宿主、formatter include 路径、输出锁与环境误判。
- [大曲库选歌性能](reference_song_select_perf.md)
- [谱面构成过滤](reference_bms_composition_filter.md) — read-model/query/实际控件与产品目标分离。
- [难度表](reference_bms_difficulty_table.md)
- [选歌元数据显示](reference_bms_songselect_metadata_display.md)
- [在资源管理器中显示](reference_bms_songselect_reveal_in_explorer.md)
- [转谱星数持久化](reference_converted_star_persistence.md) — 转谱星、原生作者等级与密度预览。
- [转谱键数显示](reference_converted_mania_keycount_display.md)

## BMS 解析、音频与游玩参考

- [BGA 链](reference_bms_bga_chain.md) — viewport/event、转码与内容播放的区别。
- [bgm1 按键触发故障](reference_bms_bgm1_pause_keytrigger_bug.md)
- [游玩音轨静音合同](reference_bms_gameplay_track_mute.md)
- [键音链](reference_bms_keysound_chain.md)
- [lane 键音 timeline 上界](reference_bms_lane_keysound_timeline_bounds.md) — lane-count 上界、parser keymode 与末端发声。
- [LNOBJ 解码](reference_bms_lnobj_decoding.md)
- [lane 重排](reference_bms_lane_rearrangement.md)
- [stop-motion 滚动旁路](reference_bms_stopmotion_bypass.md)
- [判定 parity](reference_bms_judgement_parity.md)
- [mania autoplay HoldNote 地雷](reference_mania_autoplay_holdnote.md)

## 皮肤与视觉参考

- [BMS 默认皮肤几何](reference_bms_default_skin_geometry.md)
- [BMS 皮肤编辑器边界](reference_bms_skin_editor.md) — legacy editor 禁用与 CLR 反射格式风险。
- [gameplay skin slot 三态合同](reference_gameplay_skin_slot_contract.md) — 三态、provider 优先级与候选生命周期。
- [gameplay skin shared codec/material](reference_gameplay_skin_codec_material.md) — shared codec/resolver/material 与诊断边界。
- [gameplay skin lane identity/topology](reference_gameplay_skin_lane_identity.md) — stable lane ID 与 topology 投影。
- [gameplay skin topology publication/revision](reference_gameplay_skin_topology_revision.md) — owner-local revision 与 publication 区别。
- [gameplay skin唯一layout snapshot](reference_gameplay_skin_layout_snapshot.md) — 唯一 layout、字段回退与共同 publication。
- [gameplay skin config presence](reference_gameplay_skin_config_presence.md) — accepted presence、synthetic default 与 per-index mask。
- [gameplay skin lane-resource compatibility](reference_gameplay_skin_lane_resource_compatibility.md) — lane provenance、9K/14K 候选映射与资源退役。
- [gameplay skin event envelope](reference_gameplay_skin_event_envelope.md) — 事件顺序、producer authority、准确率/进度、真实HUD挂层与池化音符/Seek错误取证。
- [gameplay skin capability negotiation](reference_gameplay_skin_capability_negotiation.md) — closed allowlist、只读 token 与危险 handle。
- [gameplay skin 脚本诊断](reference_gameplay_skin_scripts.md) — 菜单整包验证、撤销残留、固定 tick、持久化中断与 queued texture ownership。
