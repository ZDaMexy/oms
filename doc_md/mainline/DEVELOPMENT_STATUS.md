# OMS 开发进度与遗留问题

> 最后更新：2026-06-29（**P1-A 皮肤创作生态 `F0` 立项**：BMS 素材 + `skin.ini` 皮肤创作/编辑生态正式立项，`F0` 组件契约 + ini schema 草案 + 必备/推荐/可选三档冻结已作为纯文档落地，权威源进 P1-A 四件套、`SKINNING.md` 降为派生视图；**`F1` 皮肤主面已成（2026-06-29）**：ini 解析三件套 + 配置源（`BmsLegacySkin`/`SkinImporter` 路由[改 core·fallback 保护]）+ **颜色 / 纹理 / 几何三轴全部皮肤化**（所有现存渲染件 + `BmsPlayfield.applySkinGeometry`）+ **reference 验收 capstone**（创作者模板 + `BmsReferenceSkinTest` 逐键 parity），BMS 全套 **1002/1002** + Release gate 绿；剩 stage 框架 / `KeyImage` 净新增件。**皮肤存储轨 `G1`（可视文件夹·revisit "复用 SkinManager hash" 决议）已启动·刀①（folder-backed 直读建块）落地**；详见 P1-A 四件套。见下「皮肤系统现状」末条。此前 2026-06-23 同日三件落地：**P1-K K12** 修复 `BMS→mania` 转谱星数被 BGM/scratch sample-only 对象灌高（难度入口 nested-aware 过滤 + `conversion_version` bump，pre-fix 反证 +113%）；**P1-J #12** 修复选歌试听音频泄漏进游玩开头（mute 方案 + 核心 `Ruleset.PlayBeatmapTrackDuringGameplay`，并收紧选歌试听为只 `#PREVIEW`、存量谱 backfill 回写）；**P1-L BGA Phase 5.2** 开局即播 + 会话级缓存 + ultrafast 转码 + 扫描线加载进度；BMS **961/961** + Release **0/0**、用户实机均已验收 / 暂未见异常。）当前真实状态见下「最新快照」，最新验证快照见「最近一次验证」，历史切片见 [CHANGELOG.md](CHANGELOG.md)——按本页规则「最近一次验证只保留最新一条；历史归 CHANGELOG」，抬头不再堆叠历史快照。
> 本文档只记录"仓库里已经真实存在的状态"，不重复规划全文。
> 详细分步规划见 [DEVELOPMENT_PLAN.md](DEVELOPMENT_PLAN.md)，权威技术约束见 [OMS_COPILOT.md](OMS_COPILOT.md)，外部 IIDX / BMS 方向校准见 [../other/IIDX_REFERENCE_AUDIT.md](../other/IIDX_REFERENCE_AUDIT.md)。

## 状态定义

| 状态 | 含义 |
| --- | --- |
| 已完成 | 代码已落地，且至少通过一次构建、测试或明确手动验证 |
| 进行中 | 已有实际实现，但功能尚未满足该步骤验收标准 |
| 仅骨架 | 项目、类或入口已创建，但核心逻辑尚未接入 |
| 未开始 | 仓库中尚无对应实现 |
| 阻塞 | 当前步骤依赖尚未满足，暂时不能有效推进 |

## 最新快照

- **当前阶段**：Phase 1.1 皮肤系统专项执行中（BMS 默认层已收口，mania OMS-owned 组件与 release-gate 回归已继续收口；当前主线仍以公开发行物产品面收尾与 1.17 输入硬件/语义验收为先，但外部 IIDX 审计导出的反馈闭环与判定 parity 缺口已抬升为下一优先补强项；与此同时，`P1-J` 已把转谱-mania 游玩期性能全面收口——帧抖动（同样本快路径）与 once-per-run 冻结（确诊为开局阻塞 gen2、prewarm 放开玩家模式修复）均于 2026-06-11 用户实测通过；50k 极端 dense 谱 profile 仍后置）
- **仓库定位**：Windows-only，保留 osu!mania + BMS，已移除 Osu/Taiko/Catch
- **主入口**：`osu.Desktop.slnf`（含 7 个项目）
- **BMS 规模**：约 167 个源文件；`oms.Input` 15 个源文件（含 Windows DirectInput backend）；58 个测试源文件（以上为 2026-04-25 本地文件计数，排除 `bin/obj`）
- **已落地主链**：BMS 解码 → 转换 → 导入 → 7K+1 gameplay → 四套判定 → 六种 gauge + GAS → EX-SCORE / CLEAR LAMP / DJ LEVEL → CN/HCN mode-aware 计分 → 本地 best/replay/排行榜按 judge mode + long-note mode 分桶 → BMS replay recording / playback / 本地归档 → 难度表来源管理 / 缓存 / MD5 匹配 / 表分组 → Song Select 分布图 → 谱面元数据摘要 → gameplay → results 自动跳转
- **BMS 元数据**：`#SUBTITLE` / `#SUBARTIST` / `#COMMENT` / `#PLAYLEVEL` / `#DIFFICULTY` 已解析，Song Select 可显示谱师、内部标级与表标签
- **BMS 选歌分组**：Song Select 当前已把 BMS 可见分组收窄为 `难度表`、`外部谱库`、`内部谱库`、`曲师`、`谱师`、`BPM`、`星数`、`最近游玩时间`、`谱面时长`、`成绩评级`、`标题`；`难度表` 现为默认分组，`未分组` 与若干上游通用分组只在非 BMS ruleset 保留。进入 BMS 选歌与切换任一 BMS 分组时，当前视图会停留在分组最外层，并以 keyboard selection 高亮当前歌曲/谱面所属的最外层分组（即便当前播放的是非 BMS/mania 谱面也只停在 root、不再误展开任意分组，2026-06-18 修）；`外部谱库` / `内部谱库` 当前也已走同一条 ruleset-specific hierarchical grouping 管线，不再依赖 `DifficultyTable` 专用特判。Song Select 现还提供 BMS-only「展示层级」下拉（歌曲↔谱面，强制扁平分组下锁定为谱面）、层级返回条（大库下快速上退一级、不必滚动找组头）与按 `group.Depth` 区分的分组视觉（表名层 vs 等级层）。该功能面已按主线收口，剩余仅为 `P1-G` 下的 Song Select UI 人工展开验收与后续测试回归。
- **BMS 选歌排序**：Song Select 当前已使用 ruleset-specific 8 项排序：`标题`、`曲师`、`BPM`、`时长`、`星数`、`点灯状态`、`达成率`、`miss 数`；其中本地成绩派生项的显示标签已明确改用 BMS 专用文案，不再回落到通用 `Clear Lamp` / `准度要求`，mania 不受影响。
- **P1-I 子线状态**：`I1` / `I2` / `I3` 均已完成落地。`BmsCompositionFilterControl` 已以 BMS-local 私有单轨控件形式落地：`RC / LN / SCR` 三段可独立启停、各自表示最大占比、尾段为空白容差；`BmsCompositionHandle` 拖拽句柄可在段间边界拖拽并显示当前数值；`BmsCompositionRowButton` / `BmsKeyCountToggleButton` 非激活态用 `ColourProvider.Background3/Background1`（hover 效果可见）；`SearchHintTooltip` 已接入并修复 DI 崩溃（构造函数注入，对齐 `ModTooltip` 模式）；颜色冻结 RC=蓝(94,190,255) / LN=黄(255,212,92) / SCR=橙(255,119,86)。**（2026-06-16）大曲库「谱面构成」过滤"失效"已修复并经用户实测确认**：真因＝backfill Phase 1 的 Realm `IQueryable` link-traversal 查询崩溃被静默 catch（缓存恒空 + Phase 2 跳过）；同轮把旧库首轮 Phase 2 补算收口为直读 .bms 旁路（避开 `GetWorkingBeatmap` 全局锁）+ 轻量计数解码（可证等价）+ 批量写回 + 一次性进度通知。**核心 API 变更**：`Ruleset.OnSongSelectSetup` 签名加 `Storage` + `INotificationOverlay`（OMS 自有方法）。详见 [P1-I 四件套](../subline/P1-I/) 与 [约束 #8–#16](../subline/P1-I/TECHNICAL_CONSTRAINTS.md)。`I4` focused regression 仍在进行中（单轨拖拽 headless regression 与 shared visual gate 待补强）。**`I5`–`I7`（BMS-only「展示层级」下拉 / 层级返回条 / 难度表分组解析缓存）已于 2026-06-16 落地、2026-06-18 经用户实机反馈再修两轮（层级展开态+缩进方向 #15、非 BMS 播放谱面进入 BMS 误展开分组 #16）；BMS 全套 918/918、人工视觉验收通过。** **（2026-06-22 选曲展示/筛选三连、用户实机验收通过）**：① 标准面板 `PanelBeatmapStandalone` 第 4 排在星级↔「展示全部难度」按钮间加**难度表归类标签**（如 `sl4` / `★8/sl4`，BMS 选曲与转谱-mania 都生效；osu.Game 经新增 `BmsPersistedMetadataResolver.GetDifficultyTableEntries` 只读 ExtensionData，约束 #19）；② **mania「显示转谱」由二态升三态**——单一 enum `ConvertedBeatmapsDisplay { Hidden, Shown, ConvertedOnly }`（`OsuSetting.ShowConvertedBeatmaps`(bool) 改名 `ConvertedBeatmapsDisplay`(enum)、过滤行为 `BeatmapCarouselFilterMatching` 一处 switch、mania 三态循环钮 + BMS 保留二态 + 设置面板改下拉、非 mania 把 `ConvertedOnly` 夹回 `Shown` 防清空，约束 #20）；③ **mania 选歌新增「难度表」分组**——复用 BMS 难度表分层但**只显示 BMS 转谱**（新 osu.Game 共享 `BmsConvertedDifficultyTableGrouping`、对非 BMS 谱面返回空 group → grouping 丢弃法零改 matching、`ManiaRuleset` override 4 分组虚方法、`NoResultsPlaceholder` 空结果指导转谱禁用→启用/已开但空→导入 BMS，约束 #21）。详见 [P1-I CHANGELOG](../subline/P1-I/CHANGELOG.md) 2026-06-22（其二/其三/其四）。
- **P1-J 子线状态**：shared keysound timing、lane/order 首轮 hot-path 收口、live channel non-destructive resize、pause/seek 生命周期回收、player-level 音频语义 proof、hold-note body-tick early-break、BMS replay frame 缓存化、full autoplay 的对象级 `AutoPlay` + direct-time replay 分流，以及 full autoplay keysound sample pool 预热都已落地。当前自动化基线已覆盖 full autoplay correctness、HUD/key-counter replay surface 与 keysound 邻接回归。**「once-per-run 致命卡顿」假设线已于 2026-06-11 确诊收口**：其转谱-mania 形态 = 开局段阻塞 gen2 全量 GC（玩家模式键音游玩中冷解码所致），随 keysound prewarm 放开玩家模式（BMS 原生与转谱对等）修复并经用户同谱实测（游玩中 stalls=0、阻塞 gen2=0）；同轮另确诊修复游玩期帧抖动（store 通道每播 sample-drawable 重建 → gen1 晋升风暴 → 通道同样本快路径）。50k 极端 dense 谱仍未 profile、后置。
- **P1-K 子线状态**：解析链路治理已阶段性收口——数字层级 `K1`~`K8` 整体落地（raw carrier 显式入口 / signed BPM / 同拍位 `BPM→STOP→object` / `LNTYPE 2` 最小 MGQ / BGA·invisible·mine·`SCROLLxx·SC` typed surface + consumer contract / `K4` parse-once→project-many 全 consumer reuse / `K5` source-bound modless playable cache / `K6` results·statistics 已带 mods playable contract / `K7`·`K8` summary·gauge-history proof）；`K9`（dedicated BMS→mania converter + sample-only scratch + autoplay ignore contract + persisted converted star + spread-display read-model）已落地，剩 public wording 与更宽 presentation/manual proof；`K11`（BMS→mania BGM/autoplay 音频补全 + LN 尾键音对齐）converter 侧已落地。**K12 修复（2026-06-23 落地）**：此前 `BMS→mania` 转谱星数被 BGM/scratch sample-only 对象灌高——这些对象虽不进 `TotalObjectCount` 计数，但仍留在 `HitObjects` 里被 `ManiaDifficultyCalculator` 零过滤计入 Strain/MaxCombo（mania 难度器不读计数字段、直接遍历 `HitObjects`）；键音型 BMS 灌水可观（pre-fix 反证 scratch 谱 +113%），影响选歌星数显示/排序/按星分组（**仅星数，游玩计分不受影响**）。修复＝`ManiaDifficultyCalculator.isDifficultyRelevant` 难度入口 nested-aware combo 过滤（对原生 mania 可证 no-op、不 bump mania `Version`）+ bump `conversion_version` `20260623`（仅失效重算 BMS 库，升级后首启一次「Reprocessing converted star rating」进度通知）。逐刀落地与各层 authority 落点详见 [P1-K 四件套](../subline/P1-K/)。
- **BMS→mania 转谱音频补全（K11 转谱对象 + J6 mania-runtime 播放，已闭合）**：此前 `BMS -> mania` 转谱只搬玩家可击打对象的键音、BGM（autoplay channel `0x01`）被静默丢弃 → 纯键音 BMS 在 mania 游玩（mania ruleset + `ShowConvertedBeatmaps`）丢掉鼓/贝斯/铺底/人声等背景层。`K11`：BGM 以 sample-only `BmsConvertedBgmSampleHitObject` autoplay 发声、不进 scorable/star，LN 尾 node sample 置空对齐 BMS「长条只头发声」。`J6`：转谱 BGM/scratch/tap-note 统一走复用的 `BmsKeysoundStore`（`DrawableManiaRuleset` 反射宿主 + 缓存 + 挂载游玩树，暂停/seek 统一停、缺席安全回退），已闭合 **E（暂停停 BGM）/ 长 BGM 通道偷断（store floor 128）/ tap-note→store per-WAV cut（生产默认）/ bgm1 按键触发（BGM/scratch `Samples` 置空、键音走 `KeysoundSample`；真因 = mania 按键音效反馈 `GameplaySampleTriggerSource`，2026-06-08 用户多谱实测 ✅，见 P1-J CONSTRAINTS #11）/ tap-note 池化（2026-06-10：经 mania 自有 `IManiaKeysoundStore`/`IHasManiaKeysound` 接口让池化 `DrawableNote` 路由 store）/ **游玩期帧抖动（2026-06-11：真因 = 每键音触发 sample-drawable 重建 churn → gen1 晋升风暴，修复 = 通道同样本快路径，用户实测密集区 maxFrame 15–30ms→5–10ms ✅）/ 开局 ~220ms gen2 冻结（2026-06-11：玩家模式键音冷解码所致，prewarm 放开到玩家模式、BMS 原生与转谱对等）**。仍后置：转谱键音重复的 **LN 部分**（须池化嵌套头）、50k 极端 dense（仍未 profile）、BGM/scratch 非池化每帧 + 按键反馈重扫（次级）、store 128 floor（选歌预览 Track 泄漏已于 2026-06-23 由 P1-J #12 mute 方案修复，见「最近一次验证」）。详见 [P1-J 四件套](../subline/P1-J/) 与 [P1-K CHANGELOG](../subline/P1-K/CHANGELOG.md)。
- **BMS 解析-导入-静态背景链**：本轮已补齐三处主链缺口：非 `.bme` 稀疏 `9K_Bms` 谱面现可通过 `channel 17` 进入 9 键路径，不再要求九个 lane 全部出现；decoder 的 non-fatal warning 现会在导入成功时汇总成单独通知并写入日志，而不是静默吞掉；静态背景链现统一为 `STAGEFILE > BACKBMP > BANNER`，导入期会把静态图引用规范化到实际存在的文件名，运行时也会对旧数据补做常见图片扩展名 fallback，默认 `BmsBackgroundLayer` 在有当前 `WorkingBeatmap` 时会优先尝试显示真实背景贴图。
- **BGA 链路（P1-L Phase 5，落地 2026-06-14、自动化通过、人工视觉验收待办）**：修复 native BMS 游玩此前无真正 BGA——解析层（P1-K）产出完整 BGA 事件/定义，但转换层只取一个静态 `metadata.BackgroundFile`、时间线被丢弃，显示层 `BmsBackgroundLayer` 静态占位件且被不透明 lane 背板完全遮挡。已落地：转换携带 `BmsBeatmap.BgaTimeline`（不进 `HitObjects`），运行时 `BmsBgaPlayer` 在皮肤可定制浮窗 `BgaPanel`（挂 `DrawableRuleset.Overlays`、不被遮挡）按时间线播放图序列 + 视频（FFmpeg `Video`+时钟同步，资源直读 `chartbms/`），POOR 层按 `#POORBGA` 在 miss 显示，默认镜像 playfield 布局（P1→右上/P2→左上/居中→右上/14K→四角各一个，2026-06-20 由原"中缝"改）+ letterbox，`ShowBga` 开关，无 BGA 回退静态图，仅 native 路径。**Phase 5.1（2026-06-15）**：框架打不开的老式 `.mpg/.wmv/.avi/.flv` 经 opt-in **外部 ffmpeg**（用户自备）后台转 `.mp4` 缓存后播放（`BgaVideoTranscode` 开关，无 ffmpeg＝静态图回退）。**Phase 5.2（2026-06-23）**：转码加载体验与缓存治理——开局即播（`BmsBgaVideoPreloader` 阻塞预热推迟 player push 到首帧就绪）、会话级一次性清缓存、libx264 `-preset ultrafast` 提速、仅 BMS 的扫描线加载进度（`ScanlineLoadingLayer` + `GameplayLoadProgress` 跨 DI 桥）。BMS 961/961 + Release 0 错。详见 [P1-L 四件套](../subline/P1-L/) Phase 5/5.1/5.2。
- **BMS 选歌 BPM 显示**：Song Select 左上 BPM 统计现已按 imported chart 的真实 timing data 显示；`BmsImportedBeatmapFactory` 会把首次转换得到的 `ControlPointInfo` / `HitObjects` / `Breaks` 复用回 raw wrapper，使 `BeatmapTitleWedge` 这类 raw working beatmap consumer 不再回退到默认 `60 BPM`。BPM 分组与排序仍继续使用 persisted `BeatmapInfo.BPM`，两条链当前已不再失配。
- **存储**：Release 默认 `%APPDATA%/oms/`；`storage.ini` 可迁移到单一自定义数据根；BMS 使用 `chartbms/` 目录、mania 使用 `chartmania/` 目录的文件系统直读存储；Settings → Maintenance 现已拆成 `外部谱库` 与 `内部谱库` 两个 subsection，并把谱库扫描扩展为四个显式入口：`扫描外部谱库（重建）`、`扫描外部谱库（增量）`、`扫描内部谱库（重建）`、`扫描内部谱库（增量）`。其中 `增量` 模式只补导当前没有 active `FilesystemStoragePath` 记录的目录，`重建` 模式则继续重走全部候选目录；当前 managed-root 子目录判定也已补齐 trailing-separator 归一化，避免合法内部目录被误判为“不在托管根下”。`BeatmapSetInfo` 现还会持久化 `ExternalLibraryRootPath`，把 external root snapshot 固定到 beatmap set 上，供 `外部谱库` 分组与后续 fallback 使用。Settings → 常规 → 安装位置 现已把入口明确为 `更改数据目录位置`：选择空目录时会把当前数据内容直接迁入所选目录；若所选目录已有无关文件，则会改用其下 `oms/` 子目录；若所选目录本身已是可用数据目录，则只写入 `storage.ini` 并在重启后切换。整个流程只改变运行时数据根，不移动程序文件。
- **BMS 难度表来源管理**：Settings → 游戏模式 → BMS → 难度表 当前统一支持本地目录、`index.html`、`header.json`、表体 json 与 `http/https` URL；seeded preset 会按 `source_name` / `display_name` 自动认领现有预置来源；移除已导入 preset 时会清空来源并恢复隐藏占位，而不是删除内置 preset；导入或刷新失败时，设置页与首次启动页都会显示中文分类原因。
- **BMS 难度表当前状态**：`manager-owned metadata sync`、`RefreshAll` 真实结果合同与 `wrapper/source identity fallback` 三批修补已落地；在此基础上，响应性后置已继续推进两刀：persisted metadata 回写已从“单次全量重写所有 BMS 谱面”收窄成“按受影响 MD5 集合分批写入”，`RefreshAll` 也已补上逐源进度合同和 settings 页持续反馈；同时，internal / external rebuild 命中旧 beatmap set 时也会重新套用当前难度表 metadata。当前这轮工程修补已可收尾；若后续现场仍见 `Unrated`，优先进入原始 `.bms` 字节 MD5 差异诊断，而不是继续怀疑 Song Select 分组消费面。
- **首次启动向导**：首次启动设置当前已收口为六步 OMS flow：欢迎、UI 缩放、获取谱面、导入、难度表设置、按键绑定。获取谱面页改为 mania / BMS 外部站点导流与内部谱库补扫提示；导入页直接复用 `ExternalLibrarySettings`；难度表页通过反射调用 BMS 难度表管理器按分组导入 zris 预设 URL，并在多项失败时显示中文摘要；最终页复用全局、mania 与 BMS 的按键绑定 subsection。
- **首次启动稳定性与本地化**：手动重新打开首次启动向导并切到旧“游戏表现”页导致的 blank panel / unhandled error 已修复；欢迎页、获取谱面页与导入页的可见文案现已切到 OMS-owned localisation namespace + `.resx`，确保简中界面不再继续显示上游翻译。该表面主归属 `P1-A`，导入页复用外部谱库设置只形成 `P1-H` 从属暴露，不新开子线。
- **输入**：键盘 / Raw Input / XInput / MouseAxis 主链可用；Windows 默认 HID 已切到 DirectInput；`HidSharp` 仅为 `OMS_ENABLE_HIDSHARP=1` 诊断后端。桌面端 Settings -> 输入 当前已主动隐藏上游通用的数位板 / 触屏点击 / 鼠标 subsection，保留 OMS 相关键位 / supplemental binding 表面，但不移除对应 runtime config 与 handler 链。
- **训练 Mod**：`BmsModMirror` 与 `BmsModRandom` 已落地；`RANDOM` / `R-RANDOM` / `S-RANDOM` + seed / custom pattern 已接通，14K 单组 pattern 可自动复制到双侧
- **辅助 Mod**：`BmsModAutoScratch`、`BmsModAutoNote` 与 `BmsModAutoplay` 已落地，均归 `DifficultyReduction`；`A-SCR` 会让 scratch 退出判定 / 计分 / gauge 池，并提供 mod 内可见性 / 染色配置；`A-NOT` 会对非 scratch note 做同样的 assist 处理，并提供独立的 note 可见性 / 染色 / 染色盘；`A-SCR` 与 `A-NOT` 当前互斥，且二者都继续与 `autoplay` 互斥。`autoplay` 已接通 BMS replay frame / replay input handler / replay recorder / auto generator
- **BMS mod 记忆**：BMS 现通过 `BmsRulesetSetting.PersistedModState` 以 ruleset-local JSON snapshot 持久化 mod 选中状态与非默认配置；完全重启或从 mania 切走再切回 BMS 时都可恢复，且不影响 mania。实现 `IPreserveSettingsWhenDisabled` 的 configurable BMS mod 现在关闭再开启也不会丢配置；`Sudden / Hidden / Lift` 还额外提供 `记忆游戏内变动` 开关，默认开启时会把局内调整回写到当前 BMS mod 配置并在回场 / 下次启动后延续。启动早期若 `RulesetConfigCache` 尚未完成加载，`OsuGameBase` 现在会延后 replay 当前 ruleset 到 cache ready 后再执行恢复，避免冷启动首轮漏恢复或把 ruleset 误标记失败；该路径已由 `BmsStartupModPersistenceIntegrationTest` 锁定。
- **BMS 速度语义**（完整实现/键位/测试明细见 [P1-C](../subline/P1-C/)）：lane cover 按 IIDX/LR2 语义拆为 `Sudden`（上遮挡）/ `Hidden`（下遮挡），`Lift` 为独立 mod、经 `ScrollLengthRatio` 间接影响 GN；设置页提供 `Normal / Floating / Classic` tri-mode Hi-Speed（`Classic` 锁官方 sample `HS 10 + WN 350 => GN 300`，`Floating` 仅 initial-BPM anchored、非完整 mid-song re-float），并显示「不启用 `Sudden/Hidden/Lift` 的基础下落时间」；`GreenNumber` 只在游戏内 HUD/toast 查看。`Playfield Scale` 固定 `1.0`、数值型 `Playfield Horizontal Offset` 已退出，single-play 改为四态 `Playfield Style`（`1P 居左`/`2P 居右`/`居中·左皿`/`居中·右皿`，9K 固定居中、14K 固定双侧；不改变可见时间语义）。runtime 调速反馈留在 gameplay 内（mode-aware `GN + WN + 模式和值 + 当前目标`，滚轮/中键循环目标）；`UI_PreStartHold` 收口为「前 5 秒阻止开始 + 全程调速修饰键」运行时合同，pre-start 1 号普通轨纯视觉流速预览已落地（不接判定/键音/replay 链）。
- **osu!mania 滚动速度设置**：`Settings -> 游戏模式 -> osu!mania -> 滚动速度` 当前已通过 hover 文案明确为“标准车道几何下的参考下落时间”；由于不同皮肤会改变车道尺寸、判定线位置与缩放，同一数值不保证跨皮肤体感一致。更换皮肤后应重新校准，且 mania 与 BMS 的下落时间当前不可互相参考。
- **BMS 键音通道设置**：`Settings -> 游戏模式 -> BMS -> 键音通道数` 当前已把 shared `BmsKeysoundStore` ceiling 公开为 `1..256` 滑条，默认值现已从 `16` 调整为 `32`。hover 提示会直接概括低值更容易截断 BGM / 键音 / 长按尾音、`32` 为常用折中、高值更适合极高密谱面或较强机器，以及“缺音时先升到 `48/64`、额外负载增加时再逐步下调”的调参路径。该设置仍继续作用于同一 shared pool；运行时调高会立即补充通道，调低则会在超额 channel 停播后逐步回收，不再直接切断当前正在播放的键音。
- **P1-A / P1-C 交叉专题**：现阶段这条交叉线已从“strict Classic 收口”推进到“tri-mode Hi-Speed control surface + 阻止谱面开始/ingame start operator surface”。`P1-A` 继续负责 settings / HUD 宿主 / fallback / skin boundary 与 operator overlay / toast 的产品边界，`P1-C` 继续负责 mode-aware speed metrics、`Sudden / Hidden / Lift` 联动、hold modifier 调速语义（同一 feedback family 下的常驻训练表达已于 2026-06-15 随速度反馈卡移除）；pre-start 1 号普通轨纯视觉流速预览现也沿这条 split 落地，宿主/fallback 归 `P1-A`，显示时序、lane 选择与“绝不接判定链”语义归 `P1-C`。aggregate scalar state contract 已随速度反馈卡移除，当前 `GN` / `WN` 已明确属于 OMS 的 tri-mode runtime surface，而非完整 `FHS`；除冷启动 BMS mod 恢复外，pre-start overlay owner contract、real-player host binding、preview gate 与 hold 期间 lane 输入转发抑制也已补 focused coverage，后续 backlog 主要转为 full Floating parity（mid-song re-float、soflan range、更加严格的 IIDX start sequencing）、更广的 real-input integration coverage 与后置人工验收。
- **文档治理基线**：文档目录现已固定为 `doc_md/mainline`、`doc_md/subline`、`doc_md/other`、`doc_md/mini`；任何后续开发必须同步更新对应目录文档，子线与 mini 的变化若影响全局，必须反向同步主线四件套
- **结果页反馈基线**：BMS results 的 expanded 主评价与 contracted badge 已按 `DJ LEVEL` 显示，主分数区已显式标为 `EX-SCORE`；`BmsClearLampProcessor` 的结果侧 final gauge / gauge history 重放现会复用运行时 long-note mode 与 caller 提供的已带 mods playable beatmap，不再在 helper 内重复应用 beatmap mods，`HCN` body-tick fail 也不会再把 failed score 误持久化成 `PERFECT` / `FULL COMBO`
- **回放基线**：BMS replay frame / replay input handler / replay recorder / auto generator 已接通；本地 replay 归档复用 core legacy replay encode/decode 的 custom-ruleset fallback，当前按 lane action 持久化
- **反馈/训练闭环缺口（常驻反馈卡已移除）**：**2026-06-15 按产品决定整体删除常驻速度反馈卡 `DefaultBmsSpeedFeedbackDisplay`**——BMS 最近判定 `FAST/SLOW`、compact judgement summary、live `DJ LEVEL + EX %`、`PERFECT / FC / FC LOST` 状态线、瞬时 judge display、compact visual timing-offset、fixed AAA EX pacemaker、常驻 GN 均退出 gameplay；判定**计数**改由全局 `JudgementCounterDisplay`（已修 COMBO BREAK 实时计数）承担，GN 仅留 toast / pre-start。pre-start 1 号普通轨纯视觉流速预览仍在。当前缺口：controller calibration / deadzone / sensitivity 可见入口；若未来重建 key-sounded BMS 训练闭环（FAST/SLOW、pacemaker、judge display）须另立专题。参考方向见 [../other/IIDX_REFERENCE_AUDIT.md](../other/IIDX_REFERENCE_AUDIT.md)
- **判定系统语义差距**：`OD` 主路径已稳定；`BEATORAJA` / `LR2` / `IIDX` judge mode 已显式接通，其中 `BEATORAJA` / `LR2` 的 judge-rank difficulty 已进入 runtime 与 score bucket。**2026-06-14 校正现状（parity 第 1–2 刀）**：parity 并非「全缺」——IIDX `16.67/33.33/116.67/250` 与 LR2 四档窗口已与外部审计逐项吻合，beatoraja 已有整数截断缩放 + early/late 非对称 BAD + scratch/release profile，excessive/empty poor 已按家族参数化（LR2 仅 note 前）。第 1 刀建 `BmsJudgementSystemParityTest`（29/29）锁成 parity 契约 + 统一跨家族边界；**第 2 刀从 beatoraja `JudgeProperty.SEVENKEYS` 溯源后发现并修复 BAD 早/晚写反（应早 280/晚 220，早窗更宽）（G3），并把 IIDX empty-poor `500/150` 与 CN release 结论性收口为 documented heuristic（IIDX 闭源、无权威单值，不宣称 parity）（G4）**。剩余第 3 刀：把 BAD-early/late、empty-poor vs note-poor 区分接进 gameplay judge display / counts（属性显示面已自动满足）
- **联网**：账号、在线排行榜、谱面下载、新闻/聊天、多人与观战入口及 Discord RPC 已按 `OnlineFeaturesEnabled` 守卫；默认 endpoint 已清空。BMS 难度表的公共 URL 导入/刷新为当前例外，不依赖 OMS backend。

### 皮肤系统现状

- **BMS 默认层**：七批 OMS-owned 切片已在 ruleset 侧收口（playfield / lane / note / hold / LaneCover / HUD / gauge / results / Song Select panels）
- **OmsSkin 基础设施**：`OmsSkin` host / provider / resource root、共享 `OmsSkinTransformer`、显式 `ManiaOmsSkinTransformer` 入口已落地
- **Global shell**：global HUD / SongSelect / Results / Playfield 缺省 shell 经 OMS preview 返回；`MainHUDComponents.json` / `SongSelect.json` / `Results.json` / `Playfield.json` layout metadata 由 regression 锁定；`ResultsScreen` global target 与 Skin Editor Results preview 已完成最小闭环
- **Mania 第一批**（Stage / Column / Key）：StageBackground / StageForeground / ColumnBackground / KeyArea / HitTarget 已切到 OMS shell 组件；10 类 stage-local / shared preset 已接通（layout、shell behaviour、shell asset、shell colour、key asset）
- **Mania 第二批**（Note / Hold / HitBurst / Judgement / HUD）：8 类 OMS-owned 组件已升格：`OmsNotePiece` / `OmsHoldNoteHeadPiece` / `OmsHoldNoteTailPiece` / `OmsHoldNoteBodyPiece` / `OmsManiaJudgementPiece` / `OmsHitExplosion` / `OmsManiaComboCounter` / `OmsBarLine`（均不再继承 legacy 类型），由 `OmsOwnedSkinComponentContractTest` + `TestSceneOmsBuiltInSkin` 锁定；note scrolling、combo 与 bar-line 的主要 runtime 语义已收口，当前剩余 gap 主要在 legacy config/asset lookup 兼容路径与公开发行物产品面收尾
- **Native-default removal**：`SetSkinFromConfiguration()` 已把 Argon / Triangles / DefaultLegacy / Retro 统一回退 OMS；`SkinManager` 只注册 `DefaultOmsSkin` 为受保护 built-in，启动时清理历史上游条目；legacy beatmap fallback 已切到 `DefaultOmsSkin`；`SkinManager.AllSources` 已去重
- **Partial override**：BMS 用户皮肤缺失 BMS 组件时返回 null 让后续 source 承接；mania legacy 用户皮肤缺失 note / hold / judgement / explosion / combo / bar-line 时回退 OMS 组件；mixed-layer 三类语义（mania-only / BMS-only / 双层皮肤）已有 runtime 证明
- **候选包语义**：`SimpleTou-Lazer` 仅为 mania 侧内置皮肤候选基线，不可对外称为"已完成默认皮肤"
- **BMS 素材 + ini 皮肤创作生态（P1-A `F`/`G` 系列，2026-06-27 立项；`F1` 主面 2026-06-29 已成）**：把当前纯代码型 BMS 皮肤（唯一入口是写 C# `ISkin.GetDrawableComponent()`、默认皮肤 100% 程序化零素材）升级成像 mania 那样「放文件夹 + `skin.ini` 即换皮」的产品。`F0`（组件契约 + ini schema 草案 + 必备 / 推荐 / 可选三档冻结，按 osu!mania/beatoraja/LR2 真实生态校准）已作为纯文档落地；权威源在 [../subline/P1-A/TECHNICAL_CONSTRAINTS.md](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)「皮肤创作生态」节 + [../subline/P1-A/DEVELOPMENT_PLAN.md](../subline/P1-A/DEVELOPMENT_PLAN.md) `F` 系列，制作者视图见 [../other/SKINNING.md](../other/SKINNING.md)。锁定决议：游玩界面 only / 自有 `[Mania]`-对齐 + `[Bms]` 扩展段 / 程序化兜底 + 参考素材皮肤·不烤 PNG / fail-open + 诊断 / 新 `BmsAssetSkin` 包在 `BmsSkinTransformer` 下零改 lookup。`F1`（实现）**2026-06-27 已动工并打通核心链路**：① ini 解析三件套（`BmsSkinDecoder`/`BmsSkinConfiguration`/`BmsSkinConfigurationLookup`，独立解析器不侵入核心 `LegacySkin`）；② 配置源——`BmsLegacySkin` 经 `ParseConfigurationStream` hook 解析 `[Bms]` 段 + `GetConfig` 应答，`SkinImporter` 路由导入皮肤实例化为 `BmsLegacySkin`（最小 core 改动 + `SkinnableSprite` 连带适配 + fallback 保护：BMS 不在场的非 OMS 环境零变化）；③ 渲染读 config——**颜色 / 纹理 / 几何三轴全部皮肤化**（所有现存渲染件：note 家族 / lane bg / divider / hit target / bar line / lane cover / backdrop / baseplate 均 ini 可配·贴图优先 Sprite / 颜色回退 Box·抽共享 `BmsSkinnableVisual`；`BmsPlayfield.applySkinGeometry` 读 11 几何键·`HitTargetVerticalOffset` 锁 0 守时序）+ **reference 验收 capstone**（创作者模板 [oms-bms-reference-skin/skin.ini](../other/oms-bms-reference-skin/skin.ini) + 自校验门 `BmsReferenceSkinTest` 逐键断言 == 真实默认）均落，BMS 全套 **1002/1002** + Release gate 绿；据代码更正 `SKINNING.md`。**剩 stage 框架 / `KeyImage` 净新增件**。**皮肤存储轨 `G1`（2026-06-29 立项·让皮肤像 chartbms 一样可视文件夹直读·revisit "复用 SkinManager hash 存储" 决议）已启动·刀①（folder-backed 直读建块）+ 刀②（`SkinInfo` realm 载体·schema 55→56）+ 刀③（**`SkinManager.GetSkin` folder 分支·D4 反射三参 ctor·守卫测试·非 folder 零变化·2026-07-04**）落地**，BMS 全套 **1003/1003**；`F2`（②类引擎驱动件）/ `F3`（③类 `[Bms]` 扩展段）/ `G2`（文件型默认）待续

### 1.17 输入切片现状

- `TestSceneOmsScratchGameplayBridge` 已覆盖：Scratch1 reverse-config / inverted suppression / reverse-config late-hit、14K Scratch2 全路径、second scratch mixed-source / inverted suppression、normal / inverted mouse/HID hold-survival、XInput takeover
- desktop product surface 当前已通过 `OsuGameDesktop.CreateSettingsSubsectionFor()` 安全隐藏 upstream `MouseSettings` / `TouchSettings` / `TabletSettings`；这属于 public settings surface 收口，不等于删除 mouse/touch/tablet runtime 语义
- 剩余：更广的 analog scratch cross-device 产品语义、终态输入链、controller calibration / deadzone / sensitivity / diagnostics UI，以及真实 HID 硬件验收

## 开发指标

| 指标 | 当前值 | 说明 |
| --- | --- | --- |
| Phase 1 完成率 | 70.6% (12/17) | 仅按标记"已完成"项计算 |
| Phase 1 加权进度 | 85.3% (14.5/17) | 已完成=1, 进行中=0.5, 仅骨架=0.25, 未开始/阻塞=0 |
| Phase 1.1 皮肤专项 | 进行中 | BMS 默认层已收口；mania OMS-owned 组件、runtime 语义与 release-gate 回归已继续收口；公开发行物产品面待收尾 |
| 桌面端构建 | 通过 | `dotnet build osu.Desktop.slnf -p:Configuration=Release` 0 错误（生产代码 0 警告）（2026-06-23） |
| BMS 全量测试 | **1003/1003** | 最近一次全量 `osu.Game.Rulesets.Bms.Tests`（2026-07-04 P1-A `G1` 刀③后重跑：+1 守卫测试 `TestFolderCtorReflectableForSkinManagerGetSkinPath`；含 BGA Phase 5/5.1/5.2 + 判定 parity 29 项。注：1 项 BGA 缓存 temp 清理测试间歇 flaky·`git stash` 干净树同样失败·与皮肤工作零因果·归 P1-L 跟踪） |
| Mania 全量测试 | **761/761** | 最近一次全量 `osu.Game.Rulesets.Mania.Tests`（2026-04-24） |
| BMS 聚焦回归 | **111/111** | `BmsStartupModPersistenceIntegrationTest` / `BmsModStatePersistenceTest` / `TestSceneBmsSoloPlayerPreStart` / `BmsSkinTransformerTest` / `TestSceneBmsUserSkinFallbackSemantics`（2026-04-25） |
| Mania 皮肤回归 | **92/92** | `OmsOwnedSkinComponentContractTest` + `TestSceneOmsBuiltInSkin`（2026-04-25） |
| Scratch bridge | **43/43** | `TestSceneOmsScratchGameplayBridge` 最近一次快照（2026-04-24） |
| osu.Game.Tests gate | **18/18** | `ExternalLibraryScannerTest` / `TestSceneFirstRunSetupOverlay` / `TestSceneFirstRunScreenImportFromStable` / `TestSettingsMigration`（2026-04-25） |
| K9 转谱聚焦回归 | **33/33** | mania convert/autoplay **14/14** + selector/resolver **19/19**（2026-05-26） |
| 编译器诊断残留 | 0 | 当前 Release 构建已清零；`CS1574`、本地化 OLOC、`AD0001` 兼容性问题均已处理，SharpCompress GHSA 通过 `NuGetAuditSuppress` 做定点抑制（2026-05-09） |

## 最近一次验证

> 严格只保留一条最新快照；详细命令与历史记录归档到 [CHANGELOG.md](CHANGELOG.md)。

### 2026-06-23（同日三件落地：P1-K K12 转谱星数修复 + P1-J #12 选歌试听泄漏修复 + P1-L BGA Phase 5.2，BMS 961/961、Release 0/0，用户实机均已验收 / 暂未见异常）

- **P1-K K12（转谱星数修正）**：确诊并修复 `BMS→mania` 转谱星数被 BGM/scratch sample-only 对象灌高——这些对象虽不进 `TotalObjectCount` 计数，但仍留在 `HitObjects` 被 `ManiaDifficultyCalculator` 零过滤计入 Strain/MaxCombo；修复＝`isDifficultyRelevant` 难度入口 nested-aware 过滤（对原生 mania 可证 no-op、不 bump mania `Version`）+ bump `conversion_version` `20260623`（仅失效重算 BMS 库，升级后首启一次性「Reprocessing converted star rating」进度通知）。pre-fix 反证 scratch-dense 谱星 +113%。**仅影响星数显示/排序/分组，游玩计分不变。**
- **P1-J #12（选歌试听泄漏 + 策略收紧 + 存量回写）**：BMS 游玩音频全由键音驱动，但 `BmsFolderImporter` 把 `Metadata.AudioFile` 设成选歌试听源、被 MGCC 从 0 驱动播放 → 开局叠在键音上（bms 原生/转谱-mania、autoplay/正常游玩四种组合全中招）。修复＝mute 方案（核心 `Ruleset.PlayBeatmapTrackDuringGameplay`，`BmsRuleset` override false；MGCC 仍以 `working.Track` 作时钟源、时序不变，仅 opt-out 时加 `Volume=0`；**虚拟轨/换源方案已显式禁止回退**）。同日收紧选歌试听为只 `#PREVIEW`、从头播、删 `detectFullMusicFile`；存量谱经 `BmsPreviewAudioBackfill`（一次性标记 + 进度通知）回写。
- **P1-L BGA Phase 5.2（转码加载体验与缓存治理）**：开局即播（`BmsBgaVideoPreloader` 阻塞预热推迟 player push 到首帧就绪）、会话级一次性清缓存、libx264 `-preset ultrafast` 提速、仅 BMS 的扫描线加载进度揭示（`ScanlineLoadingLayer` + `GameplayLoadProgress` 跨 DI 桥）。
- **测试 / 构建**：`osu.Game.Rulesets.Bms.Tests` 全量 **961/961**、`osu.Desktop.slnf` Release **0 错误 0 警告**；BGA 逐谱人工视觉验收仍交接人工。
- **更早快照**（均已归档 [CHANGELOG.md](CHANGELOG.md)）：2026-06-22 P1-I 选曲展示/筛选五连（难度表归类 / 「显示转谱」三态 / mania 难度表分组 / IIDX 难度等级胶囊 / 右键打开文件位置）+ P1-K LNOBJ 连续尾解码修复 + BGA 老式 `.mpg` 转码并发写坏缓存确诊修复；2026-06-15 P1-L BGA Phase 5/5.1（BMS 游玩 BGA 背景图·动画·视频链路落地）；2026-06-14 判定 parity 第 1–2 刀（`BmsJudgementSystemParityTest` 29/29 + 修复 beatoraja BAD 早/晚方向写反）。本状态页只保留最新一条快照。

## 联网约束

| 项目 | 状态 | 说明 |
| --- | --- | --- |
| 便携版发布基线 | 已落地 | `portable.ini` 标记 → `<exe>/data/` 自动生成；已实机 Release publish 验证 |
| 游戏内在线更新 | 已禁用 | Velopack 跳过，`CreateUpdateManager()` 切回基础实现；手工覆盖后不会进入游戏内自更新链 |
| 默认 endpoint | 已清空 | `LocalOfflineAPIAccess` 默认装配；hub connector 返回 null |
| 游戏内联网入口 | 已隐藏 | Toolbar / 主菜单 / Song Select / overlay / 编辑器外链 / First-run Setup 均按 `OnlineFeaturesEnabled` 收口 |
| 上游静态资源 fallback | 已离线化 | LargeTextureStore / PreviewTrackManager / metadata cache 在线源已关闭；profile 资源已补本地占位 |
| BMS 原样目录存储 | 已完成 | `chartbms/` 直读，`FilesystemStoragePath` / `LocalFilePath` 已记录 |
| Mania 目录存储 | 已完成 | `chartmania/` 直读，与 BMS `chartbms/` 同级的独立目录树；`ManiaFolderImporter` + `ManiaBeatmapImporter` 已落地 |
| 多谱库根扫描 | 已完成 | `ExternalLibraryConfig`（JSON）+ `ExternalLibraryScanner`（委托注入）已落地；Settings → Maintenance `ExternalLibrarySettings` 设置 UI 可添加/移除/扫描；BMS / mania 双类型根均可注册 |

## 已落地能力

- 上游裁剪与项目基础，主入口以桌面端为准
- BMS 解码 → 转换 → 导入 → 7K+1 gameplay → 四套判定 → 六种 gauge + GAS → EX-SCORE / CLEAR LAMP / DJ LEVEL
- LN / CN / HCN mode-aware 计分与分桶
- BMS 结果页反馈首轮收口：expanded 主环 / contracted badge 使用 DJ LEVEL，主分数区显式使用 EX-SCORE 文案，结果侧 gauge / lamp 重建已与 gameplay mod 链对齐
- 本地/在线难度表来源管理 / 缓存 / MD5 匹配 / 表分组 / Song Select 音符分布图
- BMS Song Select `外部谱库` / `内部谱库` 分组与 external root snapshot 持久化
- oms.Input 多源输入（键盘 / XInput / MouseAxis / Raw Input / DirectInput HID）
- gameplay → results 自动跳转
- BMS 皮肤链路：ruleset transformer + 全组件 lookup 接线

## Phase 1 进度矩阵

| 步骤 | 状态 | 差距 |
| --- | --- | --- |
| 1.1 上游清理 | 已完成 | — |
| 1.2 BMS 数据模型 | 已完成 | — |
| 1.3 BMS 解析器 | 已完成 | — |
| 1.4 谱面转换器 | 已完成 | — |
| 1.5 归档导入 | 进行中 | 仅剩桌面端拖放导入 UI 手工验收 |
| 1.6 键音系统 | 进行中 | 缺真实谱面长条边界人工验校 |
| 1.7 BMS 规则集入口 | 进行中 | 缺更完整 gameplay HUD 与真实谱面 gameplay 边角验校 |
| 1.8 7K+1 Playfield | 进行中 | 缺真实车道样式、皮肤化 drawable；并入 Phase 1.1 |
| 1.9 OD 判定系统 | 已完成 | — |
| 1.10 Normal Gauge | 已完成 | — |
| 1.11 EX-SCORE 与结算 | 已完成 | — |
| 1.12 密度星级 | 已完成 | — |
| 1.13 难度表来源管理 | 已完成 | — |
| 1.14 MD5 匹配 | 已完成 | — |
| 1.15 Song Select 表分组 | 已完成 | — |
| 1.16 音符分布图 | 已完成 | — |
| 1.17 输入绑定与 Lane Cover | 进行中 | analog scratch cross-device 产品语义与真实 HID 验收 |

## Phase 1.1 皮肤系统专项

| 步骤 | 状态 | 说明 |
| --- | --- | --- |
| 1.1.1 默认皮肤包分层 | 已澄清 | Global + Mania + BMS 三层独立 |
| 1.1.2 组件矩阵与 lookup | 已文档化 | 可直接驱动开发的映射矩阵 |
| 1.1.3 资源命名与配置桥 | 已文档化 | mania legacy 兼容 + BMS 自有命名 |
| 1.1.4 Global provider / shell | 进行中 | host / provider / resource root / shared transformer / layout metadata / results contract 已落地；当前维持 release gate 稳定 |
| 1.1.5 Mania 第一批 | 进行中 | 5 类 shell 组件 + 10 类 preset 已接通；仍主要消费 legacy-derived assets |
| 1.1.6 Mania 第二批 | 进行中 | 8 类 OMS-owned 组件已升格；主要 runtime 语义已收口，剩余 legacy config/asset lookup 兼容与公开发行物收尾 |
| 1.1.7 BMS 第一批 | 已完成 | playfield / lane / hit target / bar line / static BG 的 lookup 与 OMS 默认层 |
| 1.1.8 BMS 第二批 | 已完成 | note / hold / LaneCover / judgement / combo 的 lookup 与 OMS 默认层 |
| 1.1.9 BMS 第三批 | 已完成 | HUD / gauge / results / Song Select panels 的 lookup 与 OMS 默认层 |
| 1.1.10 Partial override | 进行中 | mixed-layer 三类语义已有 runtime 证明；legacy 用户皮肤 component-level fallback 已接通 |
| 1.1.11 Native-default removal | 进行中 | built-in realm 注册面已瘦身；settings / runtime fallback / source-chain 已收口；公开发行物剥离待收尾 |
| 1.1.12 测试矩阵与 release gate | 进行中 | Mania skin 92/92、BMS 聚焦 111/111、osu.Game.Tests 18/18 已复核；BMS 全量 **1002/1002** 已于 2026-06-29 复核（P1-A `F1` 皮肤主面完成 + reference 验收 + `G1` 刀①，见上「开发指标」；1 项间歇 BGA flaky 归 P1-L），mania 全量与 scratch bridge 继续沿用 2026-04-24 快照 |

执行优先顺序：维持 release gate 稳定 → 1.17 analog scratch cross-device edge/hold contract → 真实硬件验收。

## Phase 2 / Phase 3

| 阶段 | 状态 | 备注 |
| --- | --- | --- |
| Phase 2 | 阻塞 | 依赖 Phase 1 + Phase 1.1 先落地 |
| Phase 3 | 阻塞 | 依赖本地 BMS 主流程稳定；在线功能保持禁用 |

## 待人工操作验收

默认放在 Phase 1 阶段末尾统一执行，仅在构成阻塞时提前请求用户介入。

| 事项 | 状态 |
| --- | --- |
| 1.5 桌面端拖放导入 / Song Select UI 验收（含外部谱库 / 内部谱库分组展开与 fallback） | 待做 |
| 桌面端真实 UI smoke test | 已完成 |
| 便携发行物实际运行与覆盖更新验证 | 已完成 |

说明：Release publish 后 `portable.ini` 已验证会触发 `data/` 自动生成，目录结构正确；当前覆盖更新路径也已复核通过，但需要在程序完全退出后替换文件，并保留 `portable.ini`、便携模式下的 `data/` 以及任何自定义数据根使用的 `storage.ini`。

## 当前主线

以下主线全部归属于 **Phase 1.x 大主线**，仅用于执行编排；不表示项目已经正式进入 Phase 2。除阻塞修复外，Phase 2 / Phase 3 功能仍按冻结处理。

| 子主线 | 焦点 | 状态 |
| --- | --- | --- |
| P1-A 产品面与 release gate | Phase 1.1 皮肤专项 → 公开发行物皮肤收尾 | 进行中 |
| P1-I BMS 选歌筛选与搜索定制 | `I1` / `I2` / `I3` 已完成；BMS-only `谱面构成` / `键数` visual filter、custom search 与 persisted matching authority 已落地，公开搜索口径已统一为 `key/keys`、`rc/rice`、`ln`、`scr`（`regular` 仅保留兼容 alias），剩余单轨拖拽 headless regression 与 shared visual gate 收口 | 进行中（`I4`） |
| P1-B 输入语义与硬件验收 | analog scratch cross-device contract → 真实 HID 覆盖 | 进行中 |
| P1-C 判定语义与反馈闭环补强 | BEATORAJA / LR2 / IIDX parity / BMS 结果页反馈面 / 权威 GN 与调速反馈 / pre-start 1 号普通轨纯视觉流速预览（FAST/SLOW·judge display·visual timing-offset·EX pacemaker 曾归此线，已移除，见状态） | 部分回退（tri-mode/pre-start 链、results-side consumer proof 已落地；判定 parity 第 1–2 刀已收口，剩第 3 刀）；**2026-06-15 按产品决定整体移除常驻速度反馈卡**——FAST/SLOW·judge display·visual timing-offset·EX pacemaker·judgement summary·常驻 GN 全部退出 gameplay，judgement 计数改走全局 `JudgementCounterDisplay`（已修 COMBO BREAK），GN 仅留 toast/pre-start。明细见下「遗留问题」与 [P1-C](../subline/P1-C/) |
| P1-J BMS gameplay runtime 性能与音频时序治理 | shared keysound pool 时序 / dense-lane hot path / live channel resize 安全合同 / dense full autoplay replay 分流 | 进行中（`J1`/`J4` 完成、`J2`/`J3` 首刀、`J5` 自动化闭合；J6 转谱音频链路已闭合，once-per-run 开局 gen2 冻结与游玩帧抖动均 2026-06-11 用户实测修复 ✅；剩余转谱 LN 键音池化、50k 极端 dense 未 profile、选歌预览 Track 泄漏与人工验收。明细见上「最新快照」与 [P1-J](../subline/P1-J/)） |
| P1-K BMS 解析链路治理 | decoder / normalized chart model / converter 语义 / projection reuse / parse-side cache | 已阶段性收口（数字层级 `K1`–`K8` 整体落地；`K9` dedicated converter / public gate / sample-only scratch / persisted star / autoplay fix / spread-display read-model 已落地，剩 wording 与更广 presentation/manual proof。明细见上「最新快照」与 [P1-K](../subline/P1-K/)） |
| P1-D 控制器校准与诊断 | deadzone / sensitivity / scratch 模式说明 / live diagnostics | 下一优先级 |
| P1-E gameplay 与长条语义 | LN/CN/HCN 真实谱面验校 | 次优先级 |
| P1-F 首发离线发行基线 | portable.ini + data/ 便携模式已落地 | 已验证 |
| P1-G 人工验收后置 | 统一后置到 Phase 1 / 1.1 收口后 | 待做 |
| P1-H 存储拓扑支撑线 | chartmania/ 目录存储 + 外部/内部谱库重建与增量扫描 + portable.ini 便携模式；BMS 谱库分组与 external root snapshot 已接通，难度表一致性 / 刷新合同修补专题主链也已收口 | 已落地，剩余仅为后置诊断 / backlog |
| P1-M 内置音乐播放器 | 把「全局音轨 + 右上角 mini 浮窗 + song-select 试听」升级为真音乐播放器：分层 PlayQueue（真队列/重复·随机/曲库搜索排序/收藏歌单/可展开全屏复用 FullscreenOverlay/可视化/播放源 mania·bms·both）；红线：不改坏 song-select 试听与 gameplay 音轨控制、离线只用本地轨 | 规划已对齐（2026-06-15 建线），未开工；下一步 Phase 0 地基（PlayQueue + 协调契约 + 测试网） |

## 遗留问题

### 高优先级

- **训练向 lane rearrangement 已落地**：`BmsModMirror` 与 `BmsModRandom`（`RANDOM` / `R-RANDOM` / `S-RANDOM` + 自定义 pattern）现已接入 BMS ruleset；**2026-06-13 修复重排被重复应用**——`Mirror`/`Random` 既实现 `IApplicableToBeatmap`（`GetPlayableBeatmap` 应用 1 次）又被 `BmsBeatmapModApplicator` 在 `DrawableBmsRuleset` + `BmsScoreProcessor` 对同一 playable beatmap 再应用 2 次，lane 置换复合成 P³，导致自定义 pattern 失真且地雷不随重排；改为 applicator 不再重排（交 `IApplicableToBeatmap` 单次应用）、地雷经 `applyPermutation` 同步重排（S-RANDOM 例外）。同日并补自定义 pattern 输入体验：字符级过滤、按选中谱面真实键数（`TryGetKeyCount`/`CircleSize`）的实时校验+预览（**14K = 两段 1–7、7 位镜像到两侧，非 1–14**）、tooltip 各键数示例；且**非空但非法的 pattern 不再静默回退随机，改为保持谱面不变**（互斥提示用 placeholder/预览/tooltip，而非禁用 type/seed 控件——后者会让 `Mod.CopyFrom`/clone 经 `BindTo` 抛异常）。当前 Phase 2 冻结重点已转向 `1P/2P flip` / `dan` / `FHS` / BSS / MSS 等更大范围能力
- **Phase 1.1 剩余**：mania 侧仍有 legacy config/asset lookup 兼容路径与公开发行物产品面收尾；维持 release gate 稳定后继续转向 1.17 输入与真实硬件验收
- **判定系统 parity 缺口**：**2026-06-14 第 1–2 刀已收口大部**——early/late 非对称窗口、scratch / long-note release 特例与按家族参数化 excessive poor 已实现并经契约测试锁定（`BmsJudgementSystemParityTest` 29/29）+ 统一跨家族边界。第 2 刀从 beatoraja `JudgeProperty.SEVENKEYS` 溯源后**修复了一处真实 bug：BAD 早/晚非对称方向写反**（应早 280/晚 220、早窗更宽，OMS 原为早 220/晚 280），并把 IIDX empty-poor `500/150` 与 CN release 收口为 documented heuristic（IIDX 闭源、无权威单值）。剩余仅第 3 刀：把 BAD-early/late、empty-poor vs note-poor 的区分接进 gameplay judge display / counts（属性显示面已自动满足）。详见 [P1-C CONSTRAINTS #14–#17](../subline/P1-C/TECHNICAL_CONSTRAINTS.md)
- **反馈闭环缺口（常驻反馈卡已移除）**：results 页主评价 / 缩略徽章 / 主分数文案已切到 BMS 语义，结果反馈面完成第一轮收口；**gameplay 侧的常驻速度反馈卡（`DefaultBmsSpeedFeedbackDisplay`：最近判定 FAST/SLOW、瞬时 judge display、compact judgement summary、visual timing-offset、fixed AAA EX pacemaker、live `DJ LEVEL + EX %`）已于 2026-06-15 按产品决定整体删除**（玩家不用——按调整键调挡板时浮窗已显示速度信息）。judgement **计数**改由全局 `JudgementCounterDisplay`（右侧 7 计数器，已修 COMBO BREAK 实时计数）承担；若未来要重建 key-sounded BMS 训练闭环（FAST/SLOW、pacemaker、judge display）须另立专题
- **权威绿色数字现状（常驻 GN 已随卡移除）**：C2 的 target-state / cycle / `HOLD` 语义与 pre-start 纯视觉流速预览仍在；但 **C1/C3 的常驻 GN HUD 与反馈家族（FAST/SLOW、judge display 生命周期、judgement summary、visual timing-offset、EX pacemaker、live DJ%）已随速度反馈卡于 2026-06-15 整体移除**，GN 现仅在 `BmsSpeedMetricsToast`（调速时）与 pre-start overlay 查看，不再常驻 HUD
- **gameplay hot path / 音频时序缺口**：`P1-J` 已从首轮 hot-path 收口继续推进到 dense full autoplay 专项：shared `BmsKeysoundStore` 的 gameplay keysound 已不再无条件 `Schedule()` 到下一帧，`BmsLane.shouldTriggerEmptyPoor()` 与 `BmsOrderedHitPolicy.getParticipatingHitObjects()` 已去掉首批热路径对象物化，`DrawableBmsHitObject.PlaySamples()` 已收口到单样本 keysound 路径，`KeysoundConcurrentChannels` live 改值也已从 rebuild-all 改成 non-destructive resize，并补上 `config -> drawable ruleset -> playfield shared store` 的 direct binding coverage；其后又加上 pause/seek 生命周期回收、player-level 音频语义 proof、`BmsReplayFrame` 缓存化、BMS-only full autoplay replay 分流，以及 keysound sample pool 预热（2026-06-11 起玩家模式一律执行，BMS 原生与转谱对等）。**once-per-run 单次致命卡顿已于 2026-06-11 确诊收口**（开局阻塞 gen2 冻结 = 游玩中键音冷解码所致，prewarm 修复、用户实测 ✅）；同轮确诊修复转谱游玩期帧抖动（每播 sample-drawable 重建 → gen1 晋升风暴 → store 通道同样本快路径）。当前主风险收窄为：转谱 LN 键音走 store（须池化嵌套头）、50k 极端 dense 谱未 profile，以及 `P1-G` 下的人工 checklist 尚未完成。
- **解析链路治理缺口**：当前 parse chain 已先补上 raw carrier 的显式入口，并把 `SCROLLxx` 定义、unknown bag 与 `SC` 这类非十六进制 channel line 接进 no-loss 保留层；signed BPM、duplicate channel line compound、同拍位 `BPM -> STOP -> object` 顺序、`LNTYPE 2` 的最小 MGQ long-note expression、BGA / invisible / mine 的第一批 typed surface、`SCROLLxx/SC` 的 typed consumer contract、richer BGA-definition header family、unified visual-definition projection，以及 static background / Song Select note distribution / beatmap statistics / core-side metadata read-model 的 consumer reuse 也都已进入 parser/converter/import contract。当前剩余主缺口则收缩为更零散的 core/read-model 尾项，以及更广 special long-note parity。若不继续推进 `P1-K`，后续播放期优化、真实谱面验校与特效谱支持仍会建立在不完整的 parse projection 上。
- **控制器校准 / 诊断**：deadzone / sensitivity / scratch 模式说明 / live diagnostics 尚未落地；当前仅有 supplemental bindings 与 live capture，不足以覆盖 IIDX/BMS 控制器的一致性调校
- **难度表一致性 / 刷新合同**：manager-owned metadata sync、`RefreshAll` 真实结果合同、wrapper/source identity fallback、分批回写 / 进度反馈，以及 rebuild / reuse 命中旧 set 时的 metadata 自愈都已落地。2026-05-31 定位并修复**全 `Unrated`** 真根因：转谱星数（`BmsPersistedMetadataData`）与难度表（`BmsBeatmapMetadataData`）各自定义容器类却共用同一 `BeatmapMetadata.RulesetData` 列，`SetRulesetData<T>` 整体覆盖写互相抹掉对方字段（转谱星数重算冲掉难度表 entries → 全 Unrated；反向冲掉星数触发反复重算）；已用两侧 `[JsonExtensionData]` 往返保留修复（CONSTRAINTS #22）。同日早先误判为 carousel staleness 并加 per-set `DifficultyTableRevision` bump，因大库 UI 卡死已撤（中途开关表改为重启反映）。**判读顺序**：再现 `Unrated` 先确认**重启后是否仍 Unrated**——重启后正常属 carousel 中途未刷新（已知限制），重启后仍 Unrated 则查 RulesetData 字段是否被其它子系统覆盖、或原始 `.bms` 字节 MD5 与表项 MD5 不一致。
- **内置皮肤候选包**：`SimpleTou-Lazer` 仅为 mania 候选基线，不可提前对外描述为已完成
- **upstream 默认皮肤移除**：runtime fallback 已大部分收口到 OMS；剩余公开发行物剥离与 partial override 全路径收口
- **osu.Game.Tests 稳定性**：6/6 已恢复；后续扩大范围应沿 csproj exclusion 清单逐步清退
- **1.6 真实谱面长条验校**：Phase 1 最贴近玩法质量的剩余项
- **便携发行物实机验证**：portable.ini → data/ 与 single-file 冷启动已验证；剩余：内置皮肤发行门槛

### 中优先级

- **Windows HID 实机验收**：DirectInput backend 已接通，需真实 IIDX/BMS 控制器覆盖
- **存储拓扑**：portable.ini 便携模式已落地（data/ 子目录自包含）；chartmania/ 目录已落地；外部多目录谱库扫描与 Maintenance UI 已完成；剩余删除/失效语义、path identity dedup 与重扫策略
- **AutoMapper GHSA**：`NuGetAuditSuppress` + `NU1903 NoWarn` 已定点抑制，运行时 `MaxDepth(3)` 缓解攻击面；升级到 15.x 需 ~150 行 API 迁移 + Realm 操作全回归，暂维持现状
- **上游 cherry-pick 风险**：42 个 osu.Game 文件（40 修改 + 2 新增），其中 6 个属于高频改动区（详见 UPSTREAM.md）
- **密度星级标定**：已压到保守区间，需真实样本继续校准

### 低优先级

（当前无低优先级功能遗留；Release 构建已确认 `0 warning / 0 error`）

## 更新约定

- 优先更新"状态变化""遗留问题变化"和"一条最新验证快照"
- "最近一次验证"只保留最新一条；历史归 `CHANGELOG.md`
- Phase 1.1 执行顺序 / 门槛 / 候选包语义变化时必须与 `DEVELOPMENT_PLAN.md`、`README.md`、`SKINNING.md`、`RELEASE.md`、`OMS_COPILOT.md` 同步
