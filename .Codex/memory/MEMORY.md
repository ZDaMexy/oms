# OMS 跨会话记忆索引

> `doc_md/` 是权威治理源；本目录只保存稳定踩坑、诊断线索与工作流快速召回，不常驻当前测试数字或逐刀进度。按任务从索引选择少量文件，勿整库加载；任何“已修/已落”结论都要以当前代码、测试和主线状态复核。

## 项目与协作

- [项目总览](project_oms_overview.md) — OMS 范围、架构、阶段与红线。
- [文档治理](project_oms_docs_governance.md) — 低噪声读取路径、一个事实一个落点、STATUS/PLAN 预算、同次联动与持续防回潮规则。
- [反馈工作流](feedback_workflow.md) — 中文协作、真机反馈权威、修复→验证→文档/记忆→提交。
- [选歌展示与导航](project_oms_songselect_display_nav.md) — P1-I 展示层级、返回条、分组与转谱展示。
- [内置音乐播放器](project_oms_music_player.md) — P1-M 规划与边界。

## 皮肤恢复与存储

- [2026-07-10 皮肤恢复](reference_skin_recovery_20260710.md) — 分界点、恢复基线、归档 refs、保留/撤回面与重新准入门。**处理皮肤任务先读。**
- [2026-07-13 schema 56 皮肤清点](reference_skin_schema56_inventory_20260713.md) — 副本只读取证、定点迁移、失效 `InstantiationInfo`/`TrianglesSkin` 与 Realm mtime 不可靠地雷。
- [skin folder authority/path preflight](reference_skin_filesystem_authority_preflight.md) — schema 56-origin managed/external 声明分类、Windows 路径歧义、namespace overlap、TOCTOU/identity 与不可误作 mutation capability 的边界。
- [skin package immutable revision capsule](reference_skin_package_revision_capsule.md) — post-capture 内容身份、规范名/预算、defensive ownership、失败清理与不可误作 no-follow capture 的边界。
- [skin folder Windows handle capture](reference_skin_windows_handle_capture.md) — managed/external resolver与staging held authority、physical NT volume、handle-relative no-follow、identity/inventory竞态、external proof生命周期及非filesystem transaction边界。
- [managed skin folder scanner](reference_skin_managed_folder_scanner.md) — schema 57 exact owner、Observed/Valid、单次启动reconcile非watcher、manual Reload分界及统一shutdown地雷。
- [managed skin folder factory/selection](reference_skin_managed_folder_selection.md) — exact-capsule/guarded选择、typed startup双epoch、explicit current Reload、current Delete pre-physical fallback/detach与shutdown地雷。
- [managed chartskin mutation / rename / staged import / delete](reference_skin_managed_folder_mutation_foundation.md) — 专用资格/held identity、typed coordinator、NTFS handoff、`(version,kind,phase)`闭集、terminal compare-delete与Workspace managed动作地雷。
- [external Workspace / exact registry / ManagedCopy](reference_skin_external_workspace_managed_copy.md) — service owner不等于capability、held-to-final proof、manual Reload、current/noncurrent pure-Realm unregister与single-v3 copy/recovery地雷。
- [managed skin atomic reload/detach](reference_skin_atomic_reload_detach.md) — 三源C2与C3/C4 package+layout+material不可分割publication、Settings唯一manual Reload、live fail-closed、fresh barrier、participant/work lease、generation复核、current mutation与owner retire；C5～C6新增consumer继续接入。
- [ordinary `.osk` archive import safety](reference_skin_osk_archive_import_safety.md) — skin-scoped pre-open/CEN gate、same-hash receipt、record/blob asymmetric rollback，以及current reload/delete不放宽importer的边界。
- [BMS 皮肤创作](project_oms_bms_skin_authoring.md) — 作者面稳定决议、legacy editor禁用、三源reload完成边界、真实beatmap-local缺口与不可误推边界；实时能力只看P1-A。
- [Skin V1 产品进度与后续工作包](project_oms_skin_product_progress.md) — 按真实caller→consumer核算价值；当前`4/7 closed，C5 active`，C1～C4冻结，下一门为scene/animation/event与剩余optional slot production。

## 构建、存储与产品面参考

- [构建与测试](reference_build_and_test.md) — CLI gate、targeted formatter、并发构建、hidden-aware link checker 与 C# Dev Kit 误判地雷。
- [大曲库选歌性能](reference_song_select_perf.md)
- [谱面构成过滤](reference_bms_composition_filter.md)
- [难度表](reference_bms_difficulty_table.md)
- [选歌元数据显示](reference_bms_songselect_metadata_display.md)
- [在资源管理器中显示](reference_bms_songselect_reveal_in_explorer.md)
- [转谱星数持久化](reference_converted_star_persistence.md)
- [转谱键数显示](reference_converted_mania_keycount_display.md)

## BMS 解析、音频与游玩参考

- [BGA 链](reference_bms_bga_chain.md)
- [bgm1 按键触发故障](reference_bms_bgm1_pause_keytrigger_bug.md)
- [游玩音轨静音合同](reference_bms_gameplay_track_mute.md)
- [键音链](reference_bms_keysound_chain.md)
- [lane 键音 timeline 上界](reference_bms_lane_keysound_timeline_bounds.md) — C3已统一`GetLaneCount`，覆盖5K/7K末键、9K全lane、14K K14/S2及各对象族；parser-owned keymode与真实发声边界。
- [LNOBJ 解码](reference_bms_lnobj_decoding.md)
- [lane 重排](reference_bms_lane_rearrangement.md)
- [stop-motion 滚动旁路](reference_bms_stopmotion_bypass.md)
- [判定 parity](reference_bms_judgement_parity.md)
- [mania autoplay HoldNote 地雷](reference_mania_autoplay_holdnote.md)

## 皮肤与视觉参考

- [BMS 默认皮肤几何](reference_bms_default_skin_geometry.md)
- [BMS 皮肤编辑器边界](reference_bms_skin_editor.md) — legacy UI/backend稳定不可用，非Workspace/manual Reload或Skin V1 ABI；Activator只在未来重开时相关。
- [gameplay skin slot 三态合同](reference_gameplay_skin_slot_contract.md) — fail-open、semantic taxonomy、descriptor/context、provider precedence、诊断隐私与候选生命周期地雷。
- [gameplay skin shared codec/material](reference_gameplay_skin_codec_material.md) — C4 public catalog/shared tokenizer、三态resolver、exact target/material publication、diagnostic、beatmap-local排除与foundation分类地雷。
- [gameplay skin lane identity/topology](reference_gameplay_skin_lane_identity.md) — 强类型 stable ID、immutable topology snapshot、neutral transition validator 与 internal BMS/mania projection。
- [gameplay skin topology publication/revision](reference_gameplay_skin_topology_revision.md) — owner-local revision、BMS keymode/mania ordered-stage continuity、失败原子性与非 production `layoutRevision` 边界。
- [gameplay skin唯一layout snapshot](reference_gameplay_skin_layout_snapshot.md) — C3唯一neutral immutable context/publication、BMS solver与mania adapter、全consumer、字段fallback及C2 package+layout+material triple/lease地雷。
- [gameplay skin config presence](reference_gameplay_skin_config_presence.md) — bucket/legacy mania scalar/indexed-array/global/per-column-colour/native `[Bms]` exact 22 colour / 12 geometry/bucket-global/`NoteBodyStyle` accepted presence、semantic mapping、per-index mask、synthetic default与decoder authority地雷。
- [gameplay skin lane-resource compatibility](reference_gameplay_skin_lane_resource_compatibility.md) — BMS/mania逐lane provenance/candidate矩阵、production resolved material、exact preparation ref-counted borrow/retirement、9K/14K映射与future consumer纳入地雷。
- [gameplay skin event envelope](reference_gameplay_skin_event_envelope.md) — process-local envelope、canonical stream ordering、producer authority 与 mutable callback 地雷。
- [gameplay skin capability negotiation](reference_gameplay_skin_capability_negotiation.md) — closed allowlist、hard-deny classifier、只读 event token 与 authority-handle 地雷。
