# OMS 跨会话记忆索引

> 按故障主题选少量叶子。当前进度读[主线 STATUS](../../doc_md/mainline/DEVELOPMENT_STATUS.md)，合同读所属子线；这里保留诊断线索，不复制当前状态。

## 项目与协作

- [项目总览](project_oms_overview.md) — 范围、独立 IR 与旧在线链边界、数据根与便携标记。
- [文档治理](project_oms_docs_governance.md) — 主约束/多语/发行例外漏同步、候选证据与清理范围、历史网络时效、不可达验收格、开发命令环境与检查器误判。
- [反馈工作流](feedback_workflow.md) — 默认产品语言、反过度防御、真机证据与本轮结束边界。
- [选歌展示与导航](project_oms_songselect_display_nav.md) — 状态分离、祖先可见性、谱卡重绑与大库诊断。
- [内置音乐播放器](project_oms_music_player.md) — 共用音轨/试听与队列接入、已定展开壳体和 core 依赖方向。
- [内置 BMS 下载](reference_bms_builtin_download.md) — 完整表与包身份、目录手动恢复、归档预算/CRC/释放语义、首次完成旧快照、任务收尾与精确选歌。
- [Sayobot mania 下载](reference_mania_sayobot_download.md) — 原游标/混合包、原sid/bid与实际MD5、镜像节点；两玩法共享封面、首次完成旧快照的回链与退出诊断。
- [OMS IR 接入](reference_oms_ir.md) — 权威与来源时效；保存/身份与凭据队列、原账号UI取消发布、场景方法组捕获旧窗、空搜索null；多来源统计隐私、无局ID、原生整榜/ID、Mod/GAS、独立灯、共享主机cgroup/WAL、峰值/全局分页/目录去重、首次SDK网络分层、压缩CRC真实边界、恢复外取/补账/连接关闭、Windows字节receipt及Open工具地雷。

## 皮肤恢复与存储

- [机台底板与跨拓扑 scene](reference_skin_cabinet_surfaces.md) — Global/Stage背景与作者乘色、轨宽/每侧键序、原谱难度表资料与转换丢失、共享字体预算与省略文字、固定血槽裁切及真实绘制限制。

- [2026-07-10 皮肤恢复](reference_skin_recovery_20260710.md) — 恢复锚点、归档与重新准入；**皮肤任务先读**。
- [2026-07-13 schema 56 皮肤清点](reference_skin_schema56_inventory_20260713.md) — 只读取证、失效类型与 Realm mtime 误判。
- [skin folder authority/path preflight](reference_skin_filesystem_authority_preflight.md) — 声明/path preflight 不等于安全打开或写入授权。
- [skin package immutable revision capsule](reference_skin_package_revision_capsule.md) — 内容身份、独占 bytes 与物理捕获的区别。
- [skin folder Windows handle capture](reference_skin_windows_handle_capture.md) — held no-follow、文件身份竞态与 handle 生命周期。
- [managed skin folder scanner](reference_skin_managed_folder_scanner.md) — Observed/Valid、启动/手动扫描与 reload 的区别。
- [managed skin folder factory/selection](reference_skin_managed_folder_selection.md) — 选择竞态、typed epoch、shutdown 与隐式提交前同值重选的 fixture 时序。
- [managed chartskin mutation / rename / staged import / delete](reference_skin_managed_folder_mutation_foundation.md) — startup 恢复子 lease、NTFS move、日志恢复及 uncertain failure。
- [external Workspace / exact registry / ManagedCopy](reference_skin_external_workspace_managed_copy.md) — 旧注册后端保留、external 只读与 ManagedCopy 复核。
- [managed skin atomic reload/detach](reference_skin_atomic_reload_detach.md) — 三源 publication、lease/retire 与调度竞态。
- [ordinary `.osk` archive import safety](reference_skin_osk_archive_import_safety.md) — archive 预检、same-hash receipt 与非对称回滚。
- [canonical 构建、安装与用户数据保护](reference_skin_canonical_installation.md) — 静线唯一保底、source→原件/摘要与解压字节核对、星轨退役迁移、缺行修复；便携误入、缓存隔离、冷启动线程、取消资源移交。
- [BMS 皮肤创作](project_oms_bms_skin_authoring.md) — 随包手册与示例闭合、作者边界、用户决定与诊断入口。
- [Skin V1 价值与工作预算](project_oms_skin_product_progress.md) — 区分效果能力、成品与创作便利度；星轨实际体验否定、有限反馈与本轮停止边界，当前状态读 P1-A。
- [按模式皮肤选择](reference_skin_mode_selection.md) — BMS/mania 独立配置、旧全局迁移、规则集切换回落与设置页空状态收简。

## 构建、存储与产品面参考

- [构建与测试](reference_build_and_test.md) — 开发磁盘与进程环境、测试宿主、formatter owning 路径、输出锁、未编译 fixture/空跑、完整基线归因与依赖审计误判。
- [大曲库选歌性能](reference_song_select_perf.md)
- [谱库路径身份与历史保全](reference_filesystem_library_identity.md) — 同内容不同目录、失效与物删分离、多文件改名及扫描错误边界。
- [谱面构成过滤](reference_bms_composition_filter.md) — 单轨上限、共享额度、零宽入口及无解条件。
- [难度表](reference_bms_difficulty_table.md)
- [选歌元数据显示](reference_bms_songselect_metadata_display.md) — 署名共用 resolver 与标题/难度名局部清理的边界。
- [在资源管理器中显示](reference_bms_songselect_reveal_in_explorer.md)
- [转谱星数持久化](reference_converted_star_persistence.md) — 转谱星、原生作者等级与密度预览。
- [转谱键数显示](reference_converted_mania_keycount_display.md) — BMS 键数误入 osu 启发式、转换与展示统一 CircleSize。

## BMS 解析、音频与游玩参考

- [BGA 链](reference_bms_bga_chain.md) — 游戏会话、作者窗口、共享画布与零窗、转码缓存及实际像素诊断。
- [bgm1 按键触发故障](reference_bms_bgm1_pause_keytrigger_bug.md)
- [游玩音轨静音合同](reference_bms_gameplay_track_mute.md)
- [键音链](reference_bms_keysound_chain.md) — BMS/转谱发声责任、暂停保位、池化与样本准备、长条/空击性能。
- [lane 键音 timeline 上界](reference_bms_lane_keysound_timeline_bounds.md) — key/lane count 误用、末端声音分层证明与 sparse API/UI 边界。
- [LNOBJ 解码](reference_bms_lnobj_decoding.md) — 单候选配对地雷及 P1-K 权威回链。
- [lane 重排](reference_bms_lane_rearrangement.md) — 重复应用的三次置换故障及 P1-K/P1-J 回链。
- [stop-motion 滚动旁路](reference_bms_stopmotion_bypass.md)
- [判定 parity](reference_bms_judgement_parity.md)
- [自动调整偏移](reference_bms_auto_offset.md) — 继承入口与迁移、Gimmick 独立开关、视觉寿命、提前加载候选与录制结束持久化。
- [TOTAL 与成绩版本](reference_bms_total_rules.md) — 作者值/缺省值、辅助前后两个物量、历史反序列化默认与新游玩版本、autoplay 身份。
- [mania autoplay HoldNote 地雷](reference_mania_autoplay_holdnote.md) — nested judgement 过滤与 P1-K 修复历史。

## 皮肤与视觉参考

- [BMS 默认皮肤几何](reference_bms_default_skin_geometry.md)
- [BMS 皮肤编辑器边界](reference_bms_skin_editor.md) — 原编辑器异步独立草稿、关闭保存/回收、预览资源与 CLR 反射构造地雷。
- [gameplay skin slot 三态合同](reference_gameplay_skin_slot_contract.md) — 三态、provider 优先级与候选生命周期。
- [gameplay skin shared codec/material](reference_gameplay_skin_codec_material.md) — Common/Bms 元素族、样式选择、导入说明误报与持久诊断范围。
- [gameplay skin lane identity/topology](reference_gameplay_skin_lane_identity.md) — stable lane ID 与 topology 投影。
- [gameplay skin topology publication/revision](reference_gameplay_skin_topology_revision.md) — owner-local revision 与 publication 区别。
- [gameplay skin唯一layout snapshot](reference_gameplay_skin_layout_snapshot.md) — 唯一 layout、选中包声明、HUD 坐标与作者 BGA 碰撞、分隔纹理缩放及重打包摘要。
- [gameplay skin config presence](reference_gameplay_skin_config_presence.md) — accepted presence、synthetic default 与 per-index mask。
- [gameplay skin lane-resource compatibility](reference_gameplay_skin_lane_resource_compatibility.md) — lane provenance、9K/14K 候选映射与资源退役。
- [gameplay skin event envelope](reference_gameplay_skin_event_envelope.md) — 事件顺序、seek 时钟收敛与完整 Reset、callback 拷贝及真实显示取证。
- [gameplay skin capability negotiation](reference_gameplay_skin_capability_negotiation.md) — closed allowlist、只读 token 与危险 handle。
- [gameplay skin 脚本诊断](reference_gameplay_skin_scripts.md) — 授权撤销、暂停冻结、数值钳制、固定 tick 与纹理上传 ownership。
