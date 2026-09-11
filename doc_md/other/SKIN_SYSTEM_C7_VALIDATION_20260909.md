# C7 成品、作者体验与安装验证记录

**C7 交付后发现的 BMS 预览入口缺陷已修复，并补齐实际进入、成品样式及修复版安装验证，不建立新阶段。** 原七阶段不重计；Skin V1、公开发行及全部人工项目仍未签收。[此前暂停检查点](SKIN_SYSTEM_C7_RESUME_20260909.md)保留当时的历史状态。当前结论以本页末尾“交付后 BMS 预览入口修复”及 [P1-A 状态](../subline/P1-A/DEVELOPMENT_STATUS.md)为准；先前完成声明、候选失败与检查记录均保留历史，不能代替当前问题的修复证据。

本页属于原七阶段中的 C7，不建立新阶段。当前完成声明只由 [P1-A 状态](../subline/P1-A/DEVELOPMENT_STATUS.md)维护；本文记录可复查证据和仍须人工观察的边界。

## 人工签收事实

原 `V-001`～`V-004` 仍为 **0/4**，`V-005` 未签收，参见[原集中清单](SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)。C7 双包的整体观感、文字/GPU、真实设备和长时体验也没有自动签收。Momentum C6 是可导入的局部组合效果候选，不能充当 C7 完整复杂皮肤或替代 V-005 签收。

程序化 `OmsSkin` 的删除受 [恢复审计](SKIN_SYSTEM_RECOVERY_20260710.md)及 P1-A parity、完整性、原子恢复、实机 gate 约束。canonical 接管的自动证明不等于已取得实机签收；保留的旧证据适配不能被描述成新的产品 fallback。最终仍须明确区分“C7 非人工实现和验证已完成”与“Skin V1/release 已完成”。

## C6 实际失败基线

以下是此前实际运行的历史路径；本次恢复工作时已确认旧 TEMP 中的 TRX 被外部清理，不能再声称这些文件仍可在本机读取：

- `%TEMP%/oms-c6-tests/c6-core-final-format.trx`、`c6-mania-final-format.trx`、`c6-bms-final-format.trx`。
- 较早独立审查：`%TEMP%/oms-progress-audit-20260909-tests/core-skin.trx`、`mania-full.trx`、`bms-full.trx`。

本轮执行者改将新证据保存在仓库忽略的 `artifacts/skin-c7-evidence/`；从提交 `de9eb3` 的 `git archive` 新建隔离 `c6-source`，只重新运行原失败身份以恢复实际错误消息，不改当前工作副本，也不把历史数量当成本轮消息证据。实际 `tests/c6-core-recovered.trx` 完成 8 项，其中原 6 项失败、2 项通过；`tests/c6-mania-recovered.trx` 完成 4 项、原 4 项失败，均未修改原断言。两次开始 UTC 时间分别为 `2026-09-11T04:43:53.9893562+00:00` 与 `2026-09-11T04:46:24.8783046+00:00`；完整来源命令、结束时间、TRX 摘要及十条原始消息固定到随包 [c6-failures.json](../../skin-c7-acceptance/baselines/c6-failures.json)，不含 StackTrace 或私有用户路径。它是本次真实恢复证据，不冒充原完整宽测。

新的真实备份根保护基线来自当前开发数据根的完整只读字节复制；复制前后原根与副本逐文件摘要相同，复制没有打开原 Realm。先前 TEMP 用户数据基线同样已丢失，不延续为本轮依据；私有位置只记入本地 `private/paths.json`，本页不披露用户保存路径。最终通过事实须引用这次运行产生的新记录。

精确合同见 [P1-A 测试与发布约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#测试与发布约束)。C6 实际六项 core 与四项 mania 的历史说明见 [C6 报告](SKIN_SYSTEM_C6_VALIDATION_20260909.md#真实失败与关闭证据)。[Compare-FailureBaseline.ps1](../../skin-c7-acceptance/Compare-FailureBaseline.ps1)现直接读取上述随包固定 JSON 和本轮真实 TRX，逐项比对 `testName`、错误首行类别及完整 `ErrorInfo.Message`，仅规范换行和首尾空白；失败集合也必须完全相同。它不再依赖 TEMP 或 Git，也不删测试、不改预期、不压制告警，不会将新增失败按数量抵消。

默认比较仍要求原 core 六项、mania 四项全部失败身份与消息相同。唯一显式例外 `-ResolvedCoreSampleFixture` 用于原暂停/换源夹具固定实际原始声音后的复验：旧 `TestSampleUpdatedBeforePlaybackWhenNotPresent` 必须仍存在且本轮唯一结果为 `Passed`，其余 core 五项与 mania 四项仍完整一致。缺失、跳过、继续失败或重复结果均拒绝；报告保留原失败、要求的状态与实际 `Passed` 证据，不提供任意忽略名单。使用此开关或修改夹具本身都不代表该项已经关闭，仍须本轮真实结果。

| 套件 | 冻结失败身份 |
| --- | --- |
| core Skin | `TestRetrieveAndLegacyExportJapaneseFilename`、`TestRetrieveAndNonLegacyExportJapaneseFilename`、`TestRetrievalWithConflictingFilenames`、`TestRetrieveOggAudio`：AggregateException → ArgumentException，无有效 beatmap；`TestBackgroundCyclingOnDefaultSkin(True)`：背景加载超时；`TestSampleUpdatedBeforePlaybackWhenNotPresent`：Argon sample 名称假设超时 |
| mania | `TestHoldNoteChord`、`TestSingleHoldNote` expected 0 / actual 2；`TestHoldNoteStair` 0 / 4；`TestHoldNoteWithReleasePress` 2 / 3；均为既有 `Incorrect number of frames` assertion |

## 自动门与证据归属

以下是 C7 改动面要求，只有本轮实际执行记录才能填写通过，既有测试可复用但不能以旧运行结果代替本轮所需门。

| 面 | 必须覆盖的产品结果 | 已有可复用测试/入口 |
| --- | --- | --- |
| 核心和两玩法 | default/fallback、三态、公共资源、脚本/scene、所有适用键数、布局和兼容性 | core `~Skin`、`GameplaySkin`；BMS/mania relevant + full；FileStoreTests |
| 真正选择与游玩 | ordinary/managed/external → SkinManager → 同一 current package → actual BMS/mania renderer | `BmsManagedFolderSelectionProductTest`；`BmsCurrentRevisionPreparedPackageProductTest`；`BmsGameplayLayoutCurrentRevisionProductTest`；`BmsAllKeymodeSceneProductionMatrixProductTest` |
| 更新与失败保留 | live/preview 拒绝 reload；菜单 A→B；坏 B 保留 A；detach/取消/关闭 | `BmsCurrentRevisionPublicationProductTest`、`BmsCurrentRevisionMutationAtomicityProductTest`、`BmsCurrentRevisionShutdownOwnerProductTest` |
| 真实脚本与授权 | 允许/拒绝/撤销/暂停/重启/版本；两玩法基本视觉持续可用 | `BmsScriptCandidateUiProductTest`、`BmsScriptProductionProductTest`、`GameplaySkinScriptAuthorizationTest` |
| 旧数据和恢复 | supported journal 准确恢复；invalid legacy 严格保全；三源删改 receipt；备份根非空用户皮肤与 orphan 都保留 | C1/G1 product fixtures；`BmsCurrentRevisionBackupRootProductTest`；C7 canonical 专用验证 |
| 两款普通包 | 两款完整作品普通导入/导出与 third-party 对照；simple 必要部分完整且可选 suppress 不复活；complex 公开 API 组合演出 | C7 真实双包产品测试、Authoring Kit validator/pack 与作者练习 |
| 安装与发行 | canonical 原件/工作副本、完整恢复、坏安装阻止游玩；Release publish、fresh extract、冷启动、portable/custom-root/覆盖更新 | `build-release.ps1`；C7 canonical fixture；下列验收/更新工具 |
| 最终收尾 | owning formatter、精确失败比对、独立复审、文档/whitespace、当前分支提交 | `CheckDocumentation.ps1`、`git diff --check`；根执行者统一调度 |

## 成品与实际作者证据

两款成品以普通作者文件为唯一作品来源：[静线源文件](../../skin-authoring/sources/oms-simple/)、[星轨源文件](../../skin-authoring/sources/oms-complex/)和 [Aurora 作者练习](../../skin-authoring/sources/aurora-study/)均同时包含 BMS 与 mania。运行 [Author.ps1](../../skin-authoring/Author.ps1)可新建、重新生成、检查、打包、准备普通导入副本或更新。两款允许各玩法采用不同视觉，均没有仅供官方使用的资源声明或运行接口。`oms-simple` 承担正式保底；`oms-complex` 是完整展示包与默认候选，本轮没有替用户决定首次默认选择。

星轨拥有独立舞台与两种玩法配色，完整动态音符、长条和边框，集中呈现分数、连击、速度、判定、能量及暂停状态。其控制台通过公开只读状态绑定工作；可选脚本使用最近判定间隔和能量驱动两侧组合演出。未授权时保留普通文件素材与基本信息。静线和星轨均补入 mania 真正使用的普通击打与长条持续声音，根目录共 18 个 WAV；最终声音解码与实际使用结果由本轮游戏检查记录确认，不能以文件存在冒充可播放。

[实际制作演练](../../skin-authoring/docs/WORKSHOP.md)已在随发行包的独立工具上复做：从静线新建 Aurora，明确编辑 `author.json` 与保留的 `README.md`，生成完整双玩法文件，故意损坏图片并定位、修复，再检查、导入准备和更新。工具 SHA256 为 `dbdefcd3dcb906272804adebe56a651da84f69efcde775e355dfbd4312a1ca33`；Windows PowerShell 5 子进程 PATH 没有 Git 或 SDK。当前[作者工具检查记录](../../skin-authoring/docs/authoring-tool-verification.json)覆盖三包正常准入、重复打包及重新生成后再打包，结果逐字节等于当前成品；坏图片、未知字段/确切行号、目录预算、`.pending` 与已有作品保护均使用真实输入。先前 `05:43Z` 的开发工具记录已保全为历史，不再冒充独立发布验证。

| 当前成品 | 最新作者实测与当前文件 SHA256 |
| --- | --- |
| `oms-simple.osk` | `f32ae2b89c9c5cb5f2f5c67721800db686483164cb6d96566ebb0ceb7e99a980` |
| `oms-complex.osk` | `ca562158f3e1268e6f1ab640c9e207c3dc6a9183222da77363de109679ed2fbd` |
| `aurora-study.osk` | `ac596ab952da9244e57cf72a5193278e6af3961ac61a1dc168ec0165eba324e7` |

历史记录 `2026-09-09T05:00:48.1115982Z` 的三包摘要分别为 `fe268870…`、`e7c02b7e…`、`4cc5ec8f…`；`2026-09-09T17:16:19.5319027Z` 的摘要分别为 `ec278aee…`、`aa75fa80…`、`c9fe82ff…`。它们只对应当时作品，已不是当前成品依据；当时的制作和候选游戏检查也不自动覆盖后续成品。

当前[作者入口实际演练记录](../../skin-authoring/docs/authoring-workflow-verification.json)完成于 `2026-09-11T07:12:36.200448Z`，使用发行工具经 `Author.ps1` 实际执行 `new → 编辑资料与说明 → generate → check → import → update`。成品与上一版本均保留，两次独立可消耗副本均等于当前 Aurora 摘要。命令演练本身没有启动游戏；字节完全相同的三包由本轮 G1 及成品矩阵另证普通导入、选择、两玩法装载、普通导出/再次导入。先前 `2026-09-09T17:31:44.4911812Z` 记录保留在本地历史副本，不被覆盖冒充本次结果。

上述制作记录不冒充发行目录中独立工具的最终复验，也不替代游戏实际导入、选择、导出、再导入和两玩法消费。作品普通导入可消耗输入文件，因此 `Author.ps1 import/update` 实际交付独立副本，保留正式 `.osk` 与完整源文件。套件同时保存独立工具全部运行文件与四个配方/工具源码文件；作者无需 SDK，制作配方源码不由游戏执行。

## 本轮产品测试与独立复核范围

[CanonicalSkinProductsTest](../../osu.Game.Rulesets.Bms.Tests/Skinning/CanonicalSkinProductsTest.cs)通过普通导入选择两款成品和缺件第三方包，并与安装保底对照。它覆盖 BMS 全支持键数和样式、mania 单/双舞台各 1～10 轨形状及三种画面尺寸，检查同一当前发布版本实际到达音符、长条、场景、必要部分与背景区域，禁止静默回到程序化来源。缺件第三方明确关闭装饰，必要内容由包补齐而关闭保持。这里的内部双舞台形状不改变下述原生文件读取上限。

复核发现最初 mania 矩阵只挂玩法画面，尚未挂真正的共用信息显示；已补充当前玩法专属分数/生命数据与实际 `HUDOverlay`；mania 玩法、事件场景与共用信息取得同一组真实 processors，两种玩法按 Player 原顺序绑定真实判定与撤回到各自计分，退出时解除订阅，在每个 mania 成品矩阵项核对真实生命、分数、准确度、进度信息载体，精确舞台及全局控制关系、已准备的替代画面、旧信息和间隙残留均受同一版本约束。既有 C6 mania 原始 TRX 中 `TestDualStageGaugeAndGlobalTextSuppressHideOnlyTheirRealCoreHudOwners` 与 `TestDualStageCoreHudPartitionsRetainFullFallbackOrHideOnlyAuthoredFirstStage(-1/0)` 三项确为 Passed；它们证明已有共用信息路径可复用，但不充当本轮成品矩阵通过证据。

[CanonicalSkinBackupRootProductTest](../../osu.Game.Rulesets.Bms.Tests/Skinning/CanonicalSkinBackupRootProductTest.cs)复用 C6 真实备份根保护，要求实际 `OMS_C6_BACKUP_ROOT`，仅在其新副本操作。静线、星轨、Aurora 分别走普通包、受管目录、外部只读目录；普通包再经实际游戏导出与重新导入，双玩法选择、重启恢复、设置内重新载入及游玩拒绝删改，最后解除消费者再删除或取消登记回到正式保底。验证保留原备份根全部文件、现有用户记录与文件资源，以及外部目录全部字节。环境缺失导致 Ignore 必须报告为未执行，不能算保护门通过。

导出验证保留真实导入语义：普通导入可能按现有规则向 `skin.ini` 追加名称和作者信息，故原文件全部行必须作为完整前缀保留，新增尾部必须准确匹配真实导入记录的合法元数据；其它文件逐字节等于原包。导出所有条目再逐字节比对真正 Realm 记录中的文件，不能误将绘制实例持有的只含元数据快照当成文件清单。早期观察谱和产品路径失败及修复由最终复验确认，不以扫描成功冒充目录准入或实际选择成功。

独立只读审查覆盖以下产品边界，发现的问题均交由相应负责人修复并补入真实调用路径验证：

| 边界 | 复核结论与对应修复 |
| --- | --- |
| 缺失或损坏安装原件 | 原件必须先通过固化摘要与无链接只读捕获；即使工作副本有效也不能绕过坏原件。失败保留明确修复安装状态并由玩家入口阻止进入谱面。 |
| 工作副本恢复与中断 | 已知坏副本保全后从已验证原件恢复；未知旧记录、无效旧日志和不可确认现场不猜测删除。发现无效日志且缺 protected row 时构造器不应补写记录改变现场，已修并加真实恢复测试。 |
| 旧固定 ID 与用户文件 | 导出只能将精确匹配的旧/新无文件保底记录映射到 canonical；未知同 ID 用户包必须按普通路径导出真实文件。已修原误判并加真实 Realm 用户文件导出检查。 |
| 普通作者也能导出 | 已验证正式保底应可使用设置内实际导出按钮；原 protected 判定将其一并禁用，已收窄并补按钮触发后整包字节对照。未知 protected 同 ID 不获特殊能力。 |
| 公共三态与最终材料 | 新解析入口按普通 Provide / Inherit / Suppress 与公开目标选择工作；明确关闭的可选部分立即保留关闭，必要部分从经过验证的包补齐，不能暗中调用旧程序化外观掩盖缺失。 |
| 通用声音测试 | 暂停、停止和换源夹具需要固定实际原始声音，不能依赖每款默认外观恰好带有 Argon 名称。已改为直接取实际原始样本，保留全部原断言；真正 canonical 声音另行验证，不把历史皮肤重新放入产品路径。 |
| 目录和作者准入 | 真实目录导入曾暴露 `skin.ini` 超过 1 MiB。作者工具现检查生产目录准入；作品改用已有公开选择器合并重复声明，保留全部键数和双舞台映射，未提高预算或绕过权限。 |
| 第三方缺件与共用信息接管 | 无 manifest 第三方从 canonical 补齐的生命条已准备为 Semantic 替代，但 HUD plan 原只认 selected 声明、Scene 或 Suppress，未建立 Gauge 分区，旧生命条未被接管。实际诊断确认后，最小修复仅将已验证 CanonicalPackage 纳入现有路由资格；不改变作者权限或 Source 声明，不读取整份 canonical scene。独立复核已确认修复边界，真实回归见下节。 |

复核不是人工代签。候选运行曾通过两款与第三方的完整画面矩阵及真实观察谱，并暴露后续需修复的普通导出验证和目录入口问题；最终通过事实只采用最后编译对应的完整本轮结果。`c7-core-candidate.trx` 中原声音夹具身份实际 Passed 且新增安装等用例通过属于候选证据，最终 formatter 后重编译与复验仍须完成。

## 可直接使用的集中输入

[集中体验入口与步骤](../../skin-c7-acceptance/README.md)、[逐项记录表](../../skin-c7-acceptance/CHECKLIST.csv)和 [Build-Acceptance.ps1](../../skin-c7-acceptance/Build-Acceptance.ps1)将完整发行物、双包、完整作者套件、原 V-001/V-005 输入及新观察谱组装成独立便携目录。原 `.osk` 留在 `packages/`；正常导入只消费 `import-copies/`，可用 [Reset-ImportCopies.ps1](../../skin-c7-acceptance/Reset-ImportCopies.ps1)补齐缺失项。已有普通副本不写内容或属性，要重做时先改名保全；新件使用不覆盖的 `File.Copy(..., false)` 创建，再仅解除本次新副本的只读位。

该行为修复了原 `Copy-Item -Force` 可覆写用户已修改或硬链接副本的边界。PS5 实际隔离证据为 `artifacts/skin-c7-evidence/import-copy-verification-616261f7771340329b98ef002a1774be/results.json`，UTC `2026-09-11T06:02:23.9595337Z`：首次副本字节相同且可消费、已消费缺件重新补齐、重复运行保留现有修改、真实只读硬链接目标与外部作者原件内容/属性全不变；`packages/` 原件始终保持。此处未启动游戏，脚本 SHA256 为 `16da50b2714db4235576ffcfb294cc999c0cdc608714f96ba9d11e847bf03eb6`。

发行套件只交付完整 `sources/`、`docs/`、独立工具 `bin/`、配方及工具 `tool-source/`、作者入口和正式 `dist/` 成品，不收录制作过程的 `work/`、`.previous` 或 `.pending`。原 V-001/V-005 包、原观察谱和原清单以原字节保存在随包 `skin-c7-acceptance/legacy/`。验收组装默认使用发行套件，离开仓库也不需要 Git、SDK 或原开发目录；构建来源由发行 `release-files.json` 的构建时 HEAD/dirty 和文件校验携带，组装记录保留该清单哈希。Aurora 作者练习成品也进入 `packages/` 和可消费导入副本。

[Generate-Inputs.ps1](../../skin-c7-acceptance/Generate-Inputs.ps1)只写新建输出目录，生成原创静音 BMS 5K/7K/9K BMS/9K PMS/14K 每轨短键与 LN、mania 1～10K 和 12/14/16/18K、静态/连续帧/坏帧第三方包、随包 H.264 MP4 背景视频及固定边框图片。它不读取用户皮肤、归档包或用户数据。

实际运行已生成 `%TEMP%/oms-c7-acceptance-inputs-b`；PowerShell 5.1 可执行，脚本保留 UTF-8 BOM。新增 [SkinCanonicalAcceptanceInputsTest](../../osu.Game.Rulesets.Bms.Tests/Skinning/SkinCanonicalAcceptanceInputsTest.cs)从提交的脚本自行生成全新临时输入，经真实 BMS decoder/converter（无 keymode override）和 mania legacy decoder/converter验证每轨短键/LN、原生舞台与视频引用；运行结果由根串行验证补入本页。单纯视频引用通过不能充当 GPU 显示或视频实机签收。复核发现早期 AVI 在无外部转码工具时只会静态回落，现改为随包直接读取 MP4；原创 AVI 生成及离线转码配方仍保留在 Generate-ViewportVideo.ps1。fixtures/viewport-evidence.json 记录本轮实际 H.264 Constrained Baseline / yuv420p、96×54、10 fps、6 秒、60 帧全部解码且帧摘要各异；同工具重复制作字节一致。玩家和组装过程不依赖 ffmpeg。

原生 mania 的 `ManiaModDualStages` 明确不改原生 mania converter，故不把“对任意 1～5K 原生谱启用 DS”写成可达体验。`LegacyBeatmapDecoder.MAX_MANIA_KEY_COUNT = 18` 是现有公开 `.osu` 读取上限，首次真实输入检查发现 20K 文件被正确限制为 9+9、18 轨短键与长条；本轮据此移除错误标注的 20K 观察谱与测试输入，而不改既有玩法规则或将它宣称为 20K 通过。公开入口的单舞台 1～10K、原生双舞台 12/14/16/18K 独立列明，其它内部支持形状（包括 10+10）由相应真实 host 自动矩阵说明。

## 覆盖更新与数据保护工具

[Update-Installation.ps1](../../skin-c7-acceptance/Update-Installation.ps1)随发行物以 `Update-OMS.ps1` 提供。新包必须处于另一完整目录并带 `release-files.json`；工具在第一笔目标写入前校验全部声明文件、目标运行状态和路径冲突，拒绝 reparse 和嵌套来源，保留目标原便携模式，永不写入 `data/`、`storage.ini` 或外部保存位置。普通程序文件通过同卷 `File.Replace` 保存旧件；canonical 旧件原样 no-replace move 到本次 `old/`，再将已在私有暂存设只读的新件 no-replace move 到目标。旧原件始终不改内容或属性，两次移动间中断可暂缺安装原件；按 `Applying` 现场及修复说明使用同一完整新包重试，保留全部旧现场。这里不声称 canonical 安装覆盖为单次原子替换，游戏工作副本原子恢复合同不变，也不自动删除无法确认的旧数据。

[Test-UpdateProtection.ps1](../../skin-c7-acceptance/Test-UpdateProtection.ps1)此前实际运行通过 portable、nonportable、custom 三类文件覆盖、ReadOnly canonical 覆盖及坏新包拒绝：运行模式、用户皮肤字节、bootstrap `storage.ini` 与原程序备份均保持，损坏新包在任何目标写入前拒绝。历史证据路径为 `%TEMP%/oms-c7-update-proof-0bc00594639649b0b41eb06009d287cc/results.json`，本次恢复工作时临时文件已被外部清理，该记录只作历史事实，不能声称现在可重新读取。使用合成程序文件验证的是覆盖保护，不宣称本轮真实发行物启动通过。

2026-09-11 的较早复验补入“来源与目标原件均只读”，实际发现 `File.Copy` 把来源只读属性带到待安装副本，使 `File.Replace` 在部分程序已更新后拒绝。首次失败现场保留在 `artifacts/skin-c7-evidence/update-verification-9e20b88fb0794dae8d7d317bebc4793b/`。当时先通过解除私有暂存只读位取得 PS5 记录 `update-verification-73d9da2d2e4349419d868295d38f4cb0/results.json`，但其旧原件属性写入仍存在下述硬链接中断缺口，已被最终两次 move 替代；该历史记录不能代替最终更新方式的证据。

定点 NTFS 硬链接复核发现中断边界：`artifacts/skin-c7-evidence/update-hardlink-verification-20260911a/results.json` 使用真实硬链接，使两条隔离安装/作者路径共用文件。正常完成与真实替换被占用后回滚均保留作者内容和只读属性；但在实际更新脚本解除目标只读后、替换前通过调试断点仅终止本次更新进程，作者原件只读位也被解除并遗留，文件内容未改。取证时没有修改生产脚本或启动游戏。另一个实际句柄实验 `hardlink-handle-window-f0409ef3b369421e93f95bca1e7c114d/results.json` 表明 FileShare.Read（无 delete）甚至 FileShare.None 的只读句柄仍允许新增硬链接，因此没有以 link-count 双检冒充闭合竞态。最终通过完全移除旧原件属性写入，直接保留旧文件解决该问题。

最终 PS5 合成保护复验为 `artifacts/skin-c7-evidence/update-verification-9a39e0d34e614c599f64ff8310b2faf7/results.json`，UTC `2026-09-11T05:54:06.1043486Z`～`05:54:13.4387350Z`，更新脚本 SHA256 `996a15b34edf8a02d148583759c918b3c78f086d1329b0c04d92ede722802355`，11 项全部通过。包括便携/非便携/自定义根、来源及目标原件只读、损坏/缺件/坏清单写前拒绝、首程序已替换后二程序占用失败重试、两款原件真实硬链接正常更新；还在前两程序已更新及旧 canonical 已移动后，以调试断点加入另一作者硬链接占据目标，新 move 实际拒绝覆盖并保全现场，随后同包重试成功。另一真实进程用例仅终止本次更新子进程，精确留下目标缺失、old/new 同在、作者仍只读的两次 move 间现场，再同包重试完成；两种失败的原 `Applying` 目录、用户根、作者字节及属性均原样保留。所有程序文件均为明确合成输入，`GameStarted=false`；它证明文件保护和恢复操作，不替代真实发行启动或断电设备签收。

发行 ZIP 的只读保护另外实证：普通 `Compress-Archive` 不携带原只读属性，PS5 条目名称使用反斜杠。现按规范化名称找到唯一两原件，显式写入 DOS ReadOnly/Archive；`artifacts/skin-c7-evidence/zip-release-pipeline-f0e267317e1a48d79064bf741c4281a2/results.json` 记录 PS5 压包 → 属性写入 → Windows Shell 解包，两原件内容和只读属性均保持。同一 DOS 属性经 .NET 解包却被忽略，证据在 `zip-attributes-verification-16c8ab999a3a4b74b498442dc0a89341/`。最终发行验证须使用 Windows 标准解包，启动工具只核验原来源/副本属性，不能预先补位冒充保留。游戏继续只读打开并核对正式保底的固化摘要，不增加原件属性写入，完整性保护不依赖 DOS 属性。

末次独立复核还发现交互入口 `Start-Acceptance.ps1` 遗留了为现有原件补 ReadOnly 的操作；现已移除。该入口不写原件属性，也不因原件缺失或损坏增设启动阻断，仍交由游戏展示修复安装界面并限制进入谱面。发行取证工具与组装工具继续如实核验解包属性，两者职责不混淆；此次只做 PS5 语法及无启动静态确认。

作者工具发布改用本次唯一新目录，再把这次实际产生的全部文件复制到发行 `skin-authoring/bin/`；不使用可能混入旧文件或运行数据的旧 bin，也不猜测删除旧内容。本轮实际独立工具运行与真实发行启动仍由最终执行者保存新的结果。

[Create-CustomRootCopy.ps1](../../skin-c7-acceptance/Create-CustomRootCopy.ps1)只从已关闭的隔离验收副本创建 `app-custom/` 与 `custom-data/`；基础配置写在 `app-custom/data/storage.ini`。非便携人工冷启动要求专用 Windows 账户或虚拟机，不能为了测试打开现有用户默认根。

[发行冷启动取证步骤](../../skin-c7-acceptance/STARTUP-CHECK.md)限定全新隔离目录、启动前确认无现有实例、零导入参数、准确保存位置的当次日志，以及本次创建进程的正常退出。可复查实际日志应包含正确 Realm 路径、渲染初始化、设置和主菜单加载，结束时保留 Stopping / Stopped 与退出码。强制结束不能算正常关闭；无桌面显示的检查也不替代 GPU 与人工观感签收。

## 2026-09-10 独立交付续验

原 V-001 两包与观察谱、V-005 Momentum、原集中清单和原 V-001 摘要已按原字节固定到 [legacy](../../skin-c7-acceptance/legacy/README.md)，六个原文件合计 780,237 B。V-001 包与原摘要一致；Momentum 为 `859ace33f5bbbfd850871076329a67ab25ea04b3ef2a6c5d979c21dc05c2b2fb`。原文件与新路径对应关系、逐文件摘要均随包保留。发行和组装先检查这些文件，再开始建立输出；不依赖本机未提交 `artifacts/`，不重生成原验收项目，也不将原未签收改写为通过。

历史离仓组装证据路径为 `%TEMP%/oms-c7-offline-assembly-f359d3b78bbd4c578633bdf422ec9973/results.json`：把已知作者文件、完整制作配方、固定原输入和 MP4 复制到新 TEMP 发行夹具，从其随包 `Build-Acceptance.ps1` 在 PS5 运行，子进程 PATH 移除 Git/SDK。成功得到 11 个原 `.osk` 与 11 份可消费导入副本、5 份 MP4 和原 V 观察谱；全部来源文件字节及属性保持，构建证据随包传递。该夹具的游戏和作者可执行文件均是明确标记的普通占位文件，`GameStarted=false`；它只证明无需仓库、Git 或 SDK 的组装过程，不能证明独立工具可执行、真实游戏启动或人工体验。这些 TEMP 记录本次已被外部清理，最终实际发行组装须保存新的可复查记录。

原清单存证使用 `.md.snapshot` 明确区别于现役文档；其原相对开发链接与全部内容保持不变，现役原清单继续接受完整文档检查。最终路径复验记录在同目录 `results-snapshot.json`：再次在无 Git/SDK PATH 下组装并保持来源字节/属性，交付的 `V001-V005-原验收清单.md` 摘要仍为 `7d87f7e349aa6adb5e28f73168993015b0ad935a823db84360d2cdc58a37ee8f`，与原始文件完全相同。

集中体验说明补齐首次“维护 → 内部谱库 → 扫描内部谱库（增量）”，建立预置观察文件的选歌记录。原 V-001～V-005、两款成品的观感、真实设备和长时间使用仍按人工清单留待实际观察。

此前只读复核的 `%TEMP%/oms-c7-release-startup-proof-20260909.ps1` 已被外部清理，且从未实际启动游戏。现按相同隔离合同恢复为随包 [Test-ReleaseStartup.ps1](../../skin-c7-acceptance/Test-ReleaseStartup.ps1)：必须使用无 `data/` 的完整新发行源，检查现有 OMS 进程/管道，零参数 Hidden 启动新副本；依次核对便携、自定义保存位置、坏工作副本恢复，以及同一真实完整发行包覆盖后再次启动。每轮只收取新增日志、实际 Realm 路径、正式保底工作副本摘要、稳定运行与正常退出；强制结束只作用于创建的进程，不能算正常退出。更新前后在游戏关闭期间比对用户数据、基础保存根与便携标记，原发行来源所有字节和属性保持；同版本覆盖不冒充跨版本升级。当前仅做脚本审查与语法解析，本轮最终执行者仍须在实际发行物上运行，结果保存到新的独立目录。

## 本轮实际宽测、发行物与终审

2026-09-11 较早中间候选检查为 43 项、30 通过、13 失败；随后候选为 47 项、37 通过、10 失败，仍不能作为完成门。九项 G1 失败已定位为夹具误将本次导入新建、尚无引用的临时 INI 当成原有文件；真实已引用素材和原用户数据均保全，该轮时夹具按来源身份修正后待复验，复杂款停止前 BMS 消费者重建的失败也仍在定位。后续只以修复并重新编译后的完整最终记录判断通过，保留本次失败来源，不按数字抵消或降低预期。

首轮 BMS 完整检查随后出现确定的 HUD owner 断言失败和重复的第三方 ready 超时，执行者于 UTC `2026-09-11T06:10:50.9990802Z` 定点终止该轮，保留 `artifacts/skin-c7-evidence/wide-first-bms-interruption.json` 与 `wide-first-bms-interrupted.log`。这是未完成的诊断运行，不能作为完整检查门，也不能把尚未运行的项目写为通过。

本次先修正三处与真实输入不符的夹具：四项历史无纹理颜色检查使用隔离的无纹理来源，保留原 Box/RGB 断言；两项统计等待改为核对本次真实皮肤 ID 与 current owner，避免读取尚未切换的旧状态；C7 HUD 四类 owner 改为普通文件皮肤真实提供的 `LegacyHealthDisplay`、`LegacyScoreCounter`、`LegacyAccuracyCounter`、`LegacySongProgress`，仍逐项核对替代画面、对应旧 owner 隐藏与必要信息，不删除原行为检查。

`tests/c7-hud-repair-diagnostic.trx` 实际完成 31 项、30 通过、1 失败，时间为 `2026-09-11T14:13:55.9302715+08:00`～`14:14:15.6181077+08:00`。唯一失败是第三方单轨 mania；其 StdOut 明确为 `scene=True, hud=True, gauge=0, text=3, gauge-route=False, text-route=True`，且旧 `LegacyHealthDisplay` 仍留在原 owner 中。这确认了真实 canonical 必要补齐的 HUD 路由遗漏，而不是异步加载慢；文字因 stage Suppress 已进入路由，生命条的纯 Provide 来源则被排除。

`GameplaySkinPreparedHudPlan` 随后仅把 `CanonicalPackage` 加入既有 `isPackageOwned` 判断，继续受完整 stage/global 路由、同一发布版本与原预算约束。已验证的简洁款补齐内容现在与选中皮肤的公共替代一样接管对应旧信息；没有把 canonical 伪装为 selected declaration，没有扩大第三方权限或增加整 scene 回退。另一负责人的独立只读复核确认这项最小修复与上述边界一致。

修复后 `tests/c7-hud-repair.trx` 实际完成 **35/35，通过且无跳过**，时间为 `2026-09-11T14:15:55.4328224+08:00`～`14:16:08.1353151+08:00`。覆盖 canonical、静线、星轨、缺件第三方各自单轨 1280×720 与内部双 10+10 轨 1024×768、1.25 倍缩放，以及上述全部历史颜色和两项统计回归。第三方日志分别到达 Gauge/Text 分区 `1/3` 与 `2/6`，两类 route 均为 True；现有断言继续检查旧信息隐藏、实际替代与全局准确度等必要信息。内部双 10+10 形状仍不宣称为原生 20K 文件可达入口。

以上是定点故障关闭证据。修复后的源码已冻结并重新启动最终完整检查；本页尚未据此宣布 C7 闭门，最终全部检查、真实发行物与人工边界仍待下方本轮结果。

本节由最终执行者追加本轮实际命令、配置、结果、精确失败比较、发行物哈希、独立复核修复及人工边界。在证据尚未写入时，不依据本页的准备工作宣布 C7、Skin V1 或 release 完成。

## 2026-09-11 完整发行与收尾复验过程

### 真实启动恢复及取消的关闭证据

真实发行退出错误已由新增 OsuGame 启动回归复现：tests/c7-startup-recovery-red.trx 为 5/8，通过的仍是旧非 startup 恢复格；空根、普通作者目录和带 ProvisionalReady journal 的 startup 三格均以同一 InvalidOperationException 失败。完整栈为 OsuGame.performManagedSkinFolderStartup → PerformManagedSkinFolderMutationRecovery → SkinManager.RecoverManagedFolderMutations → Recovery.Recover → coordinator.EnterMutation，确认外层 StartupSequence 内的重复 mutation reservation 冲突；原生命周期夹具替换了实际恢复，不能证明这条真实链通过。

修复增加仅限当前线程、外层 StartupSequence 深度 1 的 EnterRecovery 子租约；真实恢复继续取得 MutationReservation authority，外层 startup owner、epoch 和 selection completion 保持到后续扫描完成。普通 mutation、短 scope、重复 recovery 仍拒绝重入，取消不消费或释放外层租约。tests/c7-startup-recovery-green.trx 在 owning formatter 和 Release 重新编译后取得 24/24、0 跳过；真实恢复、扫描、普通退出及原 join/Realm 生命周期全部纳入。

独立复核随后发现取消资源移交漏洞：生产 RecoveryAuthority 在最终 Session.Validate 前将 native 与 registry 局部置空，若此处取消，调用者尚未收到 Session，原 finally 也无可释放的引用。tests/c7-recovery-cancellation-red.trx 在真实 Windows native 根、真实外部目录捕获与最后校验处触发取消；原 token 正常向上传递，但外部 session 的 Dispose 为 0，实际 native 句柄仍可读取，精确复现未释放。该失败现场由测试仅清理自己创建的资源，不写作者内容。

最小修复将两个局部的移交放到 Validate 成功之后；false、取消或已定义拒绝仍由原 finally 释放，移除 false 分支提前 Dispose 防止重复释放。没有改变权限检查、异常语义或外部目录只读约束。修复后 owning formatter、Release 编译及相关完整恢复类、startup/lifecycle/coordinator 合计 tests/c7-recovery-cancellation-green.trx 41/41 通过、0 跳过，持续 25 秒。断言同时检查原取消 token、真实外部文件字节、资源只释放一次及实际关闭后查询失败，独立复核无剩余阻塞。最后全部当前源码检查正在 closure-* 记录中执行；真实发行四轮启动仍须使用新包另证，不能以这组定点通过替代。

最新补充：tests/c7-final-bms-full2.trx 已取得完整 BMS 2215/2215 通过、0 跳过，持续 12 分 21 秒。该结果在后续启动恢复故障修复前取得，共有代码变化仍须按影响范围追加当前验证。release-startup-portable-repair3 已取得准确独立 data/cache、完整初始化、正式工作副本及正常退出码 0；但当次日志的 Managed skin folder scan ended unexpectedly 错误使演练仍为失败。退出工具已按独立复核仅向自己创建且 PID、程序路径、类和标题一致的唯一隐藏窗口发送普通 WM_CLOSE，未把强制结束算作成功。启动恢复链的租约重入冲突正在补真实回归与修复，未忽略错误或删减失败门。本次账户 bootstrap、事故根和原 G1 根的全文件摘要及属性前后不变；这不能倒推初次事故前后不变。

本段记录恢复继续后的实际结果，不改变 C7 尚未完成或任何人工未签收事实。证据仍保存在忽略的 `artifacts/skin-c7-evidence/`，失败输出保留，修复后使用新的名称复验。

- 首轮完整发现运行：BMS 2179/2215、mania 818/867；core `~Skin` 1306/1311、FileStore 11/11；无跳过。原始 `final-*` 日志/TRX 另保全到 `discovery-20260911-1418/`。这是发现失败的运行，不能作为通过门。
- core 五项失败的身份和完整消息已与本轮恢复的 C6 原始基线相同；原 `TestSampleUpdatedBeforePlaybackWhenNotPresent` 本轮唯一结果为 Passed，证据 `discovery-core-exact-comparison.json`。未宣称 core 全套通过。
- BMS 原生旧绘制件的夹具已按正式包实际替代调整，并保留或加强素材、可见范围、舞台隔离和用户关闭检查。`c7-bms-repair.trx` 134/136 后，两处输入/空视频时间线设置修复的 `c7-bms-repair2.trx` 6/6；最终完整 BMS 仍须重新执行。
- mania 普通包回退夹具改走真实 `.osk` 选择和双玩法来源隔离，未放宽纹理区域比对；`c7-mania-diagnostic2.trx` 中完整 `TestSceneOmsBuiltInSkin` 91/91。历史 Argon 几何以明确的历史对照来源保留，`c7-mania-diagnostic3b.trx` 的两个 Argon 与不可拆分组件隔离通过。另两个核心 HUD 挂载及一个 ExactRoot 旧 barline 预期仍失败，继续修复。HUD 实证为目标在 Loading 状态被回收，不能归因于等待时间不足，也未跳过 readiness 门。
- `publish-initial-c7.log` 已完成真实 Windows Release 发布，生成 `release-repo/oms_20260911.zip`。`release-initial-extracted-extraction.json` 记录 Windows Shell 实际解压、完整清单和文件摘要比对；两个 canonical 原件的 ZIP 属性为 33，实际解压为 ReadOnly, Archive，检查未补写属性。
- 随发行包的独立作者工具已在 Windows PowerShell 5、PATH 无 Git/SDK 的环境完成新作品、明确编辑作者资料和 README、损坏图片定位、修复、导入/更新副本、原件与上一版保留。`standalone-author-workflow-exercise2.json` 及 `standalone-author-exercise2/docs/authoring-tool-verification.json` 已同步到作者套件；完整说明见 [WORKSHOP](../../skin-authoring/docs/WORKSHOP.md)。首次复做只差保留的作者 README，保留该失败记录；未修改生成工具覆盖作者文字。
- 第一次实际完整发行启动演练 **失败**：`release-startup-initial/results.json` 中 portable-first 自有进程运行 150 秒，但指定副本保存根没有日志、完整启动标记或 canonical 工作副本，因此未执行后续保存位置/恢复/覆盖演练。已按脚本结束本次进程，失败原因继续定位；不能以出现窗口当作启动通过，也不能把本次未完成的实际启动移到人工待签收来关闭 C7。

最终 owning formatter、必要重编译与完整套件、精确既有失败比对、当前发布后实际启动、原备份完整性、独立复审、文档检查和当前分支提交仍须取得真实结果。V-001～V-004 0/4、V-005 未签收，以及画面/GPU/声音/真实设备/长时体验的人工边界均保持。
### 首次发行误入保存根的后续取证与纠正

初次 `release-startup-initial` 并非没有完成游戏初始化：程序已在错误的既有保存根完成主菜单装载，并运行 Realm schema 57 forward migration。完整自解压改变实际运行基准目录，导致便携标记被忽略，再读取账户 bootstrap 的既有自定义位置。这是本轮发现的真实发行缺陷；早期“指定副本没有日志”不能解释成程序从未访问其它根。

现有目录事后已完整字节复制，bootstrap 指针单独保留；源与保全副本逐文件摘要一致，未自动覆盖或删除任何旧数据。本次根不同于 G1 的事前备份根，没有对应事前快照，不能宣称事故前后不变。只在第二份一次性副本用当前 Realm SDK `IsDynamic=true / IsReadOnly=true / SchemaVersion=57` 检查：主要用户内容可枚举，所查 blob、登记目录与谱面引用当前存在，第一保全源与检查副本前后摘要相同。该结果只证明事后可读，不证明未发生迁移或既有 pending 清理；没有本次 forward 迁移自动备份证据，也没有猜测使用旧 corrupt 备份回滚。完整原始记录与中文恢复说明仅在 `private/startup-incident-preserved/`、`private/post-incident-readability-faa6c4be02e24dc2a74cac148dc992ab/` 保留，不公开用户路径或资料。

正式修复使用完整 self-contained 多文件发行，保留双玩法物理 DLL 发现；`HostOptions.PortableInstallation` 与现有 `IsPortableMode` 一致，使框架缓存也留在本次程序目录。合同和历史纠错见 [P1-F](../subline/P1-F/DEVELOPMENT_STATUS.md)。第二份实际 ZIP `oms_20260911_2.zip` 已生成并完成 Windows Shell 解压。重试先发现修改 PS5 脚本丢 UTF-8 BOM 的解析失败（尚未启动游戏，已有根前后不变），已恢复 BOM。

`release-startup-portable-repair2` 已取得准确独立 data 根、Renderer/Running、Realm、FirstRunSetup、Settings、Intro/MainMenu、正式工作副本和八秒稳定证据；已有账户 bootstrap、事故根及原 G1 根在本次前后全文件摘要与属性完全相同。但是两次正常关闭请求未完成退出，该运行仍为失败，继续核对正常退出确认行为，不把自有进程强制结束算作成功。后续自定义根、恢复、覆盖和最终当前包启动仍须重新取得完整通过结果。

### mania 验收宿主的定点纠正（2026-09-11）

本节只记录已确认的宿主问题与定点修复，不替代最终完整 mania 检查。改动集中于 [TestSceneManiaGameplaySkinLayoutProduction.cs](../../osu.Game.Rulesets.Mania.Tests/Skinning/TestSceneManiaGameplaySkinLayoutProduction.cs)，没有为夹具放宽生产外观、权限、事件或快照保护。

核心 HUD 的长期等待源于真实依赖缺项：普通 mania HUD 同时创建排行榜，`DrawableGameplayLeaderboard` 必需 `IGameplayLeaderboardProvider`；实际 `SoloPlayer` 提供该依赖，而原宿主没有提供。现沿用既有核心 HUD 测试的 `EmptyGameplayLeaderboardProvider`，保留排行榜、全部 11 个显式作者信息组件、真实装载与显示断言。早期保留证据是目标 `Loading` 后被回收、后台任务已完成，未保存该次依赖异常原文，不能补写不存在的堆栈；消费声明、生产宿主对照与 `c7-mania-repair4.trx` 中相关 HUD 用例恢复共同确认这一缺项。该次整组实际 26/30，通过范围不包含其余四项失败。

历史 Argon 判定几何保持在明确的历史提供器内，仅使隐藏对照部件完成更新，不进入正式包回退路径。普通包继续核对实际文件纹理及精确纹理区域；新 canonical 小节线、连击和判定显示检查实际成品纹理、矩形、舞台归属和旧 owner 隐藏。三来源公共素材检查按 `LegacyStageBackground` 的实际逐列部件和 `LegacyColumnBackground` 的 KeyFlash 分别取 owner，保留相邻轨道的可见纹理、独立 owner、作者关闭项无替身以及全部材料来源与几何断言；`c7-mania-repair5.trx` 的普通包、托管目录、外部目录三项均通过，唯一 ExactRoot 仍失败。

ExactRoot 后续故障由真实错误确认。`c7-mania-repair9.trx` 实际 0/1，`mania-repair9.log` 保留 `An active mania drawable must retain its sole pre-registered gameplay-skin object ID.`，堆栈从 `DrawableManiaRuleset.createGameplaySkinActiveObjectSnapshot:617` 经 `CreateGameplaySkinActiveObjectSnapshot:585` 到 `GameplaySkinEventRuntimeHost.Update:494`。此前两个几何样本直接添加 `DrawableNote` / `DrawableHoldNote`；非池化添加不触发本轮 `HitObjectUsageBegan` 登记，却在跳转后参与活动对象快照。跳转监听已经收到 `Seek`，异常发生于清除 pending 后、发布新 epoch 前，所以不能将旧 epoch 当作“前跳无需重置”或删除重置等待。

修复将包括前段几何样本在内的受控音符统一通过 `Playfield.Add(HitObject)`，按同一个 HitObject 引用获取真实池对象。保留全部 authoritative publication、材料、几何、双舞台小节线独立生命周期、Seek 后新 epoch 与活动 ID、真实按键命中及实际判定可见和自然退场断言。`final-mania-format4.log` 格式验证及 `final-mania-build7.log` Release 构建通过；`c7-mania-repair10.trx` 的唯一 ExactRoot 实际 1/1，通过新的池化路径、严格跳转恢复和真实击打。它只是定点通过，最终完整 mania 仍须在共同生产改动冻结后复验。

诊断同时确认 `Logger.NewEntry` 的 `Message` 与 `Exception` 分字段：只打印 Message 会留下通用未处理错误文字，无法定位根因；本例现在保留两者，测试结束无论成功或失败均取消监听，未改变 `GameHost.ExceptionThrown`。大量临时私有状态探针已移除，只保留有界公共事件摘要与真实错误。上述修复均经过独立只读复审；V-001～V-004 仍为 0/4、V-005 未签收，画面、声音、真实设备与长时体验的人工观察不由这些结果代签。

## 最终源码与作者复验（2026-09-11）

生产代码及测试冻结后，由根执行者串行运行 `artifacts/skin-c7-evidence/Run-C7FinalClosure.ps1`。`closure-checks.json` 保存逐命令、配置、开始/结束 UTC、日志名与真实退出码；最后四个归属项目的 formatter 验证及 Desktop、BMS.Tests、Mania.Tests、Game.Tests 的 Release 构建全部成功。其它已验证项目此后没有代码改动。本节取代前文“最终宽测待运行”的当前状态，保留旧失败过程供定位。

| 最后实际产物 | 结果 | 证据边界 |
| --- | --- | --- |
| `tests/c7-closure-bms-full.trx` | 2215/2215 Passed，0 skipped | UTC 08:21:09～08:31:13；含双包/第三方、真实备份根三来源和完整信息、观察输入与组合演出矩阵 |
| `tests/c7-closure-mania-full.trx` | 863/867 Passed，原 4 项失败，0 skipped | UTC 08:31:13～08:33:46；新 HUD、实际池化 ExactRoot/Seek 等修正均 Passed |
| `tests/c7-closure-core-skin.trx` | 1314/1319 Passed，原 5 项失败，0 skipped | UTC 08:33:46～08:36:21；含真实启动 recovery/scan、取消释放及当前全部 Skin 相关检查，不冒充 full core |
| `tests/c7-closure-core-files.trx` | 11/11 Passed，0 skipped | UTC 08:36:21～08:36:25；实际 FileStore 保护 |
| `closure-failure-comparison.json` | 精确比较 Passed | 对上述 core/mania 使用 `-ResolvedCoreSampleFixture`；旧声音夹具唯一结果 Passed，剩余九项名称、类别与完整消息与恢复的 C6 基线逐项一致 |

独立执行者再次检查最终 BMS/mania TRX 的完整日志和错误：没有新增未处理异常或管理目录扫描故障，BMS stderr 为空；mania 只含上述四项既有帧数断言，未以数量相等代替身份比较。core 与 mania 的原始测试命令如实保留退出码 1，接受的是已归因失败完全一致这一合同，不宣称所有检查无失败。Release 仍有既有依赖安全告警和历史编译告警，没有压制告警、删除检查或修改预期凑通过。

最终独立作者工具经 Release/win-x64 自包含发布，SHA256 `c478dac99f66fc2cfacd74b3f03d1617047885f24d0b43616047f71cff513aaf`。新的完整套件 `standalone-author-final/` 在 PS5、PATH 不含 Git/SDK 的进程中完成模板创建、明确修改两玩法配置与作者说明、生成、检查、坏 PNG 精确定位、修复、打包、导入副本消费和更新保全。`standalone-author-final-workflow.json`（UTC `08:37:54.7568409Z`）记录完整实际步骤；其 `docs/authoring-tool-verification.json`（UTC `08:38:06`）另证实三包检查、重复打包及删除生成件后重新生成均逐字节一致，错误行号、超限 INI、损坏图片和已有/中断内容保护均通过。最终 Aurora 包为 `ac596ab952da9244e57cf72a5193278e6af3961ac61a1dc168ec0165eba324e7`。

当前 [WORKSHOP](../../skin-authoring/docs/WORKSHOP.md)、[作者流程原始结果](../../skin-authoring/docs/authoring-workflow-verification.json)及[作者工具原始结果](../../skin-authoring/docs/authoring-tool-verification.json)准确同步这次事实；旧三份公开文档原字节另保存在 `historical-author-doc-records-before-final/`。独立工具演练发生在最终 ZIP 之前，不能写成已从 ZIP 解包执行；随后最终发行实际携带的工具摘要与它完全一致。游戏内普通包导入、导出、重新导入及两玩法选择另由最终 BMS 三来源备份根检查证明，不以文件复制演练替代游戏使用。

## 最终实际发行与集中体验包（2026-09-11）

**当前交付为 `release-repo/oms_20260911_4.zip` 与 `release-repo/oms-skin-c7-acceptance-20260911-final/`，准确身份及最后执行记录见本节末尾“最终封装与提交前复核”。** 下文先保留 `_3` 的完整通过过程；末次提交检查发现新增作者参考示例的尾部空行后，已修正说明文件并再次完整封装/运行，不能将两个包的 manifest 混用。

根执行者使用 `build-release.ps1` 成功发布初次收尾包 `release-repo/oms_20260911_3.zip`（344,244,838 B），实际完整日志为 `artifacts/skin-c7-evidence/release-final-publish.log`。游戏为完整自包含多文件，作者工具继续独立单文件；无需玩家另装 .NET 或作者安装 SDK。构建时 HEAD 为暂停提交 `b2fbd03489c70e44a67a03791787d71085e5099d`、dirty=true，发行清单诚实保留这一构建来源，不伪称由后续收尾提交构建。生产源码、作品和工具此后未改；末次变化仅为下述三个作者说明文件。

| 初次收尾包 `_3` 的实际身份 | SHA256 |
| --- | --- |
| `oms_20260911_3.zip` | `b1db7de89a184291788bfe7794aa94a9f98dbb961d55eddae4a5e01014f6288a` |
| `release-files.json` | `a6ead206bec463272a4545012b073d1075b5c70bcf5fc5f951c16502fceb47c5` |
| `osu!.exe` | `84dcd74cfce1b7ca8e39c171357563c7e691292e0df8df81ce240a0c7fb867b6` |
| `skin-authoring/bin/SkinAuthoring.exe` | `c478dac99f66fc2cfacd74b3f03d1617047885f24d0b43616047f71cff513aaf` |

`release-final-extracted-extraction.json` 记录 UTC `08:48:07.8443013Z`～`08:48:21.5219215Z` 的实际 Windows Shell 解压：1163 个文件与完整发行清单相符，两款原件 ZIP DOS 属性 33，实际解压后均为 `ReadOnly, Archive` 且字节正确，整个检查没有补写只读位。包内中文更新说明完整可读，工具字节等于上节已实际执行的最终独立版本。

随后随包相同字节的 [Test-ReleaseStartup.ps1](../../skin-c7-acceptance/Test-ReleaseStartup.ps1)在全新持久目录执行；`release-startup-final/results.json` 记录 UTC `08:49:10`～`08:51:28` 四轮全部 Passed：

| 本轮真实启动 | 实际结果 |
| --- | --- |
| 首次便携 `portable-first` | 用户库/日志位于独立 `app/data`，缓存位于本程序 `cache`，正式保底工作副本与只读原件一致 |
| 自定义保存位置 `custom-root` | 用户库/日志位于新 `custom-data`，基础 `data/storage.ini` 保持；缓存仍在运行程序旁 |
| 损坏副本恢复 `damaged-cache-recovery` | 故意损坏的工作副本按原字节保全，从随包原件完整恢复；保存指针保持 |
| 完整覆盖后 `after-complete-overwrite` | 使用本次实际 `Update-OMS.ps1` 完整覆盖同一发行版本，旧程序备份与 Completed 回执保留，覆盖期间用户数据/基础根/便携标记不变，随后正确启动 |

每轮均取得 Renderer、Running、实际 Realm 路径、FirstRunSetup、Settings、Intro/MainMenu、八秒稳定运行及 Stopping/Stopped，退出码 0、NormalExit=true、ForcedTermination=false，无错误日志。隐藏自动检查只向本次创建、核验 PID 与 SDL 窗口身份的窗口发出正常关闭请求；不触碰其它游戏实例。主菜单/首次向导装载不代表已人工完成向导或实际游玩签收。完整发行来源全文件字节与属性不变。上述覆盖是同一版本的完整覆盖，不冒充未运行的跨版本升级或断电试验。

另在四轮结束后，从两个测试保存根各复制一份数据库，用**本次发行** Realm SDK `IsDynamic=true / IsReadOnly=true / schema57` 只打开新检查副本。`release-startup-final/evidence/rulesets-readonly-7409129c119442eab64d5dcbe8fe95f5/result.json` 与 `rulesets-readonly-5592548557ed43b0bbf71c88418aaa28/result.json` 均确认唯一 bms/mania 的持久 `Available=true`；此值由 `RealmRulesetStore` 实际构造玩法、校验 API 兼容后设置，不以 DLL 文件存在代替发现成功。源与检查副本字节、源属性不变。它们分别证明首次便携根和最终覆盖后自定义根的状态；中间两轮共用自定义根，**没有四份独立时点的数据库快照**，中间轮只按各自启动日志与正常退出判定。

完整发行包的公开 `Build-Acceptance.ps1` 在离仓 PS5 环境实际运行，PATH 中 Git/SDK 均不可用，UTC `08:52:33.3520897Z`～`08:52:39.5955308Z` 成功生成 **`release-repo/oms-skin-c7-acceptance-20260911/`**。集中包的 [使用说明源](../../skin-c7-acceptance/README.md)说明安装/选择/游玩、作者修改检查打包导入验证，以及自定义位置与覆盖；实际目录可直接运行 `Start-Acceptance.ps1`。`acceptance-final-assembly.json` 保留真实退出码、来源不变、最终 release manifest/exe 摘要和构建来源，不再是占位程序夹具。两款成品、Aurora、完整作者源文件/工具/说明、原 V-001/V-005 包与原观察谱/清单、集中 CSV 均在最终交付中。

本次四轮前后，当前账户 bootstrap、首次事故根和原 G1 根的全部文件字节与属性完全相同，证据仅存 `private/startup-root-guard-48ba984bd95040aeaa246a388e936de7/result.json`。最终另将原 G1 根及其保护副本各自完整比对本次事前清单，均一致，未用 Realm 打开原根；见 `private/final-data-preservation-corrected.json`。最初 PS5 取证包装把 JSON 数组再次包成单项嵌套数组而错误报不等，原报告保留；`final-data-preservation-corrected.log` 记录实际类型证据和修正后完整重比对，不是放宽字节预期。这些正确隔离检查不能追溯证明首次事故根从未变化，其事后可读/无事前快照边界仍适用。

两个失败候选 `oms_20260911.zip`、`oms_20260911_2.zip` 在 `_3` 通过后，从 `release-repo/` 原样移入 ignored `artifacts/skin-c7-evidence/failed-release-candidates/`，逐个与原解压记录核对 SHA、长度、属性后移动并复核；`failed-release-preservation.json` 保存旧/新位置与证明，无删除。此举避免误用失败包，同时完整保留事故与失败取证。

原七阶段的非人工工作到此闭合；原 `V-001`～`V-004` 仍 **0/4**，`V-005` 和 C7 观感/文字/GPU/真实输入设备、真实音频/视频、低端与长时体验均未签。非便携完整冷启动需独立 Windows 账户或虚拟机；不能再使用当前真实保存根冒充隔离。旧 `OmsSkin` 只供历史/人工对照，不在产品失败回退链中，物理删除仍须原实机 gate；复杂款仍为展示包和默认候选，未决定首次默认选择。以上均不宣称 Skin V1 或公开发行完成。

### 最终封装与提交前复核

第一次对新增文件执行 staged `git diff --check` 时，实际发现参考示例 `gameplay-skin.json` 与 `gameplay-skin.script` 末尾多余空行；此前未跟踪文件不在普通 `git diff` 内，不能将其遗漏当作已经通过。只删除两处多余空行，并为 `docs/examples` 的 JSON/script 固定 LF，保留全部示例内容；原文及旧验证记录保存在 `reference-final-normalized/`。同一最终独立工具于 UTC `09:05:36.853627Z` 再次检查完整静线副本与三例，退出码 0，当前公开 `reference-verification.json` 如实同步。没有删除检查或忽略空行错误。

为让交付资料与提交源文件一致，再次执行实际发布和 Windows Shell 解压。最终 `_4` ZIP 为 **344,244,910 B**，SHA256 **`bcf6aa8da7700f822db6613734dfc4af20d4bf57c5ff6d29f25cf5c4b2ce9ac8`**；最终 `release-files.json` SHA256 为 **`d7ccb5bc66abedef7bf4f25ffca93b9b25b3605ddc976b89d991669a07899d57`**。`release-final4-publish.log` 保留完整命令结果；因旧失败原名已移出目录，打包器先输出空闲原名，随后只改外层文件名为 `_4`，避免重用事故原名，内容未修改。构建来源仍如实为 `b2fbd034...`、dirty=true，创建 UTC `09:06:57.7038356Z`。

`release-final4-extracted-extraction.json`（UTC `09:07:29`～`09:07:43`）证明实际全部文件与清单相符，ZIP DOS 33 和实际两原件 `ReadOnly, Archive` 未经补标保留。`release-final4-difference.json` 精确确认 `_3` 与 `_4` 清单内只有上述两例和 `reference-verification.json` 三个说明文件变化；**全部游戏运行文件、三款成品、完整作者源文件、制作工具和验收/更新脚本逐字节相同**。故最终源码宽测与独立作者完整制作证据仍适用，没有因说明格式修改重复整个产品宽测。

对新 manifest 仍完整重跑发行路径：`release-startup-final4/results.json`（UTC `09:08:41`～`09:10:58`）四轮全部 Passed、NormalExit=true、退出码 0、无强制结束，完整加载/正确用户根/程序旁缓存/恢复保全/覆盖保护和来源不变全部通过；`release-final4-rulesets-portable.log` 与 `release-final4-rulesets-custom.log` 对应两根最新只读副本的 BMS/mania Available=true、源和检查副本字节不变。两根仍不是四时点快照。`private/startup-root-guard-eacd4a147ed6486ca6c5120027d2cb53/result.json` 证明本次三处既有根字节/属性保持；最后 `private/final-data-preservation-final4.json` 再次完整核对原 G1 根及其副本与事前清单一致，未打开原 Realm。

最终 **`release-repo/oms-skin-c7-acceptance-20260911-final/`** 由这次 `_4` 解包后的公开组装脚本实际生成，PS5、PATH 无 Git/SDK，UTC `09:11:08`～`09:11:14`，`acceptance-final4-assembly.json` 的 Outcome/ExitCode/SourceUnchanged 为 Passed/0/true，构建证据绑定上述新 manifest。其中 `README.md`、`Start-Acceptance.ps1`、两款 `.osk`、三款完整作者源与工具、11 份原包/可消费副本、原 V 输入、原观察谱及 MP4 均可直接使用；集中记录为 **27 项待验、原 V 对应 5 项未签收**，共 32 项，未作任何人工代签。

独立复核先逐项完成 `_3` 的真实发行、全部运行文件、三成品/作者源与工具、原 V 输入及未签 CSV 检查，证据为 `final-independent-delivery-review-99198574cd5d413e89dc107a3845e48b/review.json`。最终另作 `_4` 有界复核，`final4-independent-delivery-review.json`/`.md` 为 Passed：精确三份说明差异、全部运行/作品/源/工具字节不变，新四轮日志与脚本/发行身份一致，两根只读玩法结果绑定新启动记录，实际组装和最终目录匹配新清单，没有新增阻塞。复核没有修改公开文件，也没有将全体原有自动失败或人工待签改成全通过。

已通过但被 `_4` 取代的 `_3` 包与其未运行集中目录原样同卷移入 `artifacts/skin-c7-evidence/prior-verified-deliveries/`，保留映射及原包/构建身份摘要于 `prior-verified-delivery-preservation.json`，无删除。旧运行和解包证据仍在原位置。`release-repo/` 当前只保留 `_4` 与 `oms-skin-c7-acceptance-20260911-final/`，避免交付入口混淆。

最后执行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\CheckDocumentation.ps1`、`git diff --check` 及 `git diff --cached --check` 均通过；日志为 `documentation-final4.log`、`diff-final4-working.log`、`diff-final4-staged.log`。首次文档检查发现的 PLAN 会话交接语与 memory 重复状态长行已按归属精简，未修改检查器。公开制品摘要和通用路径例子保留其提示，私有用户资料未进入提交。最终收尾说明后再次运行同样文档/差异检查，当前分支提交保留产品、证据、状态、计划、约束与记忆；不创建新分支或 PR，不推送。

## 交付后 BMS 预览入口修复

用户提供的 `1789125771.runtime.log` 在两次 `SoloSongSelect.PlayerLoader` 进入时记录相同异常：`InvalidOperationException: BMS gameplay layout preparation requires the final ruleset configuration.` 堆栈为 `BmsRuleset.PrepareGameplaySkinLayout` → `RulesetSkinProvidingContainer` → `BeatmapSkinProvidingContainer.load` → `Player.load`。其它日志不支持将故障归因为设备初始化或联网。五份原日志仅按字节复制到 ignored 私有证据并核摘要，没有启动用户安装或打开原用户 Realm。

实际原因是 Player 先加载皮肤布局根，之后才加载 DrawableRuleset 子树；BMS preparer 直接查具体配置，但此配置只被缓存于后者的子依赖容器。原 `PlayerTestScene` 经 `OsuTestScene.CreateRuleset()` 提前注入 `DrawableRulesetDependencies`，C7 布局宿主也显式缓存具体配置，故它们没有证明真实上层配置可用性。之前四轮发行启动只证明到达主菜单，不能替代进入谱面的证据；先前“非人工已收口”声明不覆盖这次已证实的新故障。

修复仅将 BMS preparer 改为与 mania 一致的 `IRulesetConfigCache.GetConfigFor(this)`，取游戏保存的同一最终配置；缺少实际配置仍明确失败，没有临时默认值、额外配置实例或异常吞掉。新增 `TestSceneBmsPlayerSkinEntry` 使用真实 nested OsuGame、生产 RulesetConfigCache 和 PlayerLoader，明确断言父容器无具体配置。BMS P2、mania Up 分别经普通 Player 与 Ctrl+Enter 所用 ReplayPlayer 进入，核真实 renderer、必要信息与 BGA 的同一布局发布，真实重试形成新布局，退出回主菜单，游玩/演示时重新载入继续拒绝。该路径没有开启原本不可用的旧皮肤编辑器。

修复前 Release 编译成功；`artifacts/skin-preview-entry-20260911/tests/entry-red.trx` 首次只记录 BMS 两格等待失败，mania 两格和基础测试通过。把诊断监听移到真实游戏到达菜单后的步骤后，`entry-red-diagnostic.trx` 与 `.log` 在同样两个失败中取得与用户完全相同的异常及真实调用栈；两次红测各 2 failed/3 passed/0 skipped。保留两次原始结果，不把最初未捕获异常的等待超时冒称精确故障证据。

首次修复后实际入口为 **5/5 Passed**（四种实际进入场景及基础测试），见 `entry-green.trx`。随后独立复核发现旧 `ExactLayoutJourneyHost` 将 requested style 写进自建配置，renderer 却从正式 cache 读取另一份；旧 C7 部分样式格没有精确验证请求值，不能沿用其声明。将此宿主改为在 `CreateChildDependencies`、包括异步取消装载路径挂载 provider 前设置实际继承 cache，删除独立配置；正式成品矩阵增加 requested/applied style、父 cache 和 renderer 配置同一实例的检查，原场景、几何、素材及缺件断言全部保留。首次 full 尚未结束即为这项修正主动中止，`bms-full-first-cancelled.json` 明确为 Cancelled，不计作通过或既有失败。

首轮格式检查分别发现生产改动两行的行尾与新增测试一个未使用引用，均修正后重新检查和编译，原失败日志保留；没有关闭诊断规则。自动证据来自新临时游戏根和原已保护的 G1 副本，不打开原用户数据库。此修改只涉及 BMS 配置获取和 BMS 测试；未改 shared skin、mania runtime、fallback authority 或两款作品，因此按 P1-A 测试与发布约束执行 BMS relevant/full 与 Release，另有真实 mania 进入对照。未重复运行 mania full、core Skin/full 或 FileStore，原因是相应生产面未改；此前 core 五项、mania 四项既有失败及原 sample 已解决事实继续按历史记录保持，未重新归因或宣称本次全通过。

最终 `entry-and-matrix.trx` 为 **142/142 Passed**，包括真实进入对照、全部成品 BMS 键数/样式/屏幕格及组合效果实际组件；随后 `bms-full.trx` 为 **2220/2220 Passed**，0 skipped/error/aborted/timeout。完整 BMS 执行 UTC `11:46:25`～`11:59:18`，Release 桌面编译 UTC `11:59:18`～`11:59:39` 成功；命令、时间和退出码保留于 `checks.json`，没有用中止的首次 full 代替最终结果。

用户进一步确认本次来自 VS Code 的“无调试运行”。实际 `.vscode/launch.json` 提供 Debug/Release 两个入口，均先执行对应 build；另按 Debug 入口构建成功，`vscode-debug-build.json` 记录 UTC `11:51:03`～`11:51:22` 和当前 BMS 源摘要。该 Debug 输出与正在只读运行的已编译 Release 测试目录隔离；作者独立发布同样不写 BMS 测试运行目录，其余共享输出的编译/格式工作串行。没有把日志中的数据根猜成程序位置，也没有覆盖另一份旧安装或启动用户原游戏。

独立复核分开覆盖互未编写的源码：`entry-independent-review.json` 绑定生产修改与新增真实入口用例；`source-review.json` 绑定生产配置来源及另一复核者修正的旧宿主/成品矩阵。两者均无剩余可操作问题；后者原先发现的配置错配已经修复并纳入上述 full，没有只记问题不落实。

修复版构建身份变化使随包独立作者工具重新生成，不能声称仍是旧 `c478...` 字节。工具以同一公开发布方法独立发布到 `author-published/`，当前 SHA256 为 `62e1f0527e4b48deb6f83917dde6fd5ad9e7f81fc830969b9babaab47b730862`；在全新 `standalone-author/`、PS5 且 PATH 无 Git/SDK 的环境，再次完成完整 Aurora 修改、错误定位、修复、打包和导入/更新副本流程（UTC `11:56:38.9246996Z`），以及三款作品重复生成/打包和中断保护检查（UTC `11:56:56.5965688Z`）。两款成品和 Aurora 字节未变。两份新记录逐字节同步公开作者说明，旧 WORKSHOP 与两份记录保全于 `author-docs-before-fix/`；完整发行物必须与此次实际演练工具逐字节一致。

修复版实际交付为 **`release-repo/oms_20260911_preview-fix.zip`**（344,245,186 B，SHA256 `1c1e11f4f31b2c57308c75558fd403b2fa84aa4083698af02d6a126a82513fd3`）。`release-files.json` SHA256 为 `9df5370ac24b5fe8ddd4a368ae93f6d9675c4fdf39646202e03617c27eef3ed7`，游戏 EXE 为 `e5857b7142259f143f398c252025f5a32535fa86eff5dabe9d1f57c596c24cec`；构建来源如实为 `615872d60273f4823e5741415ac927778e8cff2a`、dirty=true、UTC `12:01:03.7136072Z`，不冒称来自尚未生成的后续修复提交。公开打包器先使用空闲原名，随后只改外层名称为 preview-fix，前后 SHA 相同。UTC `12:01:49`～`12:02:09` 通过真实 Windows Shell 解压，全部 1163 文件与清单相符，两款原件 ZIP DOS 属性 33、实际 ReadOnly/Archive，没有事后补标；新作者工具与刚完成的演练字节一致。

`artifacts/skin-c7-evidence/release-startup-preview-fix/results.json` 记录 UTC `12:02:24`～`12:04:57` 四轮 Passed：首次便携、自定义保存、损坏工作副本保全/完整恢复、同一完整修复包覆盖后重启，均正常退出、ExitCode 0、无强制结束，来源字节/属性不变。两处最终测试根另用实际发行 SDK 打开新复制的只读数据库，`rulesets-portable.log` 与 `rulesets-custom.log` 分别确认 BMS/mania Available=true、源和检查副本字节不变；仍不冒称中间两轮有独立数据库快照。运行前后，三处既有根（包括此次反馈的数据根）的文件字节及属性一致，未打开原 Realm，私有 guard 结果 UTC `12:05:07.2096764Z` 为通过；该结果不回溯抹去此前事故的无事前快照事实。

最终集中目录 **`release-repo/oms-skin-c7-acceptance-20260911-preview-fix/`** 由该修复包的公开入口于 UTC `12:05:10`～`12:05:19` 完成实际组装与来源复核，PS5、PATH 无 Git/SDK，`acceptance-assembly.json` 为 Passed/ExitCode 0/SourceUnchanged=true，并绑定本次 manifest。包中程序、三款作品、作者源、制作/更新入口和原验收输入均可直接使用，原 CSV 继续保留 27 项待验及原 V 对应 5 项未签收，没有代填人工结论。旧 `_4` 包、原集中目录及历史证据保留，当前修复入口以上述新目录为准。

最终 `delivery-review.json` 为 Passed、Issues/Pending 均空：独立核对实际 ZIP/解压/集中包的完整清单、作者源与三包各 entry、当前工具和两份新演练、四轮正常启动日志及两根玩法证据、原输入和未签 CSV，未发现剩余阻塞。新集中目录尚无用户数据库或保存位置指针。原七阶段不重新计数，此缺陷在原 C7 内闭合；原 V-001～V-005、C7 观感/真实设备/长期体验继续未签，Skin V1 和公开发行整体仍未完成。

所属状态/计划/约束/历史、发行交付指针及 layout 记忆同步完成，主线仅保留摘要和链接。文档检查、工作副本与 staged `git diff --check` 均通过，记录为 `documentation-ready.log` 及最终提交前核对；STATUS 中旧发行细节回链本页，避免重复历史超过预算。保留公开制品摘要提示，不提交用户原日志或私有数据路径。修复在当前分支提交，不新建分支、不开 PR、不推送。
