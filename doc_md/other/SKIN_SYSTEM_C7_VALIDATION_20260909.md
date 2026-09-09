# C7 成品、作者体验与安装验证记录

**当前因用户额度要求暂停，C7 未完成。** 准确续接和当前配方/成品不一致、新测试未验范围见 [暂停检查点](SKIN_SYSTEM_C7_RESUME_20260909.md)。下文较早候选证据不能代替新增信息与按下态的最终验证。

本页属于原七阶段中的 C7，不建立新阶段。当前完成声明只由 [P1-A 状态](../subline/P1-A/DEVELOPMENT_STATUS.md)维护；本文记录可复查证据和仍须人工观察的边界。

## 人工签收事实

原 `V-001`～`V-004` 仍为 **0/4**，`V-005` 未签收，参见[原集中清单](SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)。C7 双包的整体观感、文字/GPU、真实设备和长时体验也没有自动签收。Momentum C6 是可导入的局部组合效果候选，不能充当 C7 完整复杂皮肤或替代 V-005 签收。

程序化 `OmsSkin` 的删除受 [恢复审计](SKIN_SYSTEM_RECOVERY_20260710.md)及 P1-A parity、完整性、原子恢复、实机 gate 约束。canonical 接管的自动证明不等于已取得实机签收；保留的旧证据适配不能被描述成新的产品 fallback。最终仍须明确区分“C7 非人工实现和验证已完成”与“Skin V1/release 已完成”。

## C6 实际失败基线

本机已确认以下 TRX 仍存在：

- `%TEMP%/oms-c6-tests/c6-core-final-format.trx`、`c6-mania-final-format.trx`、`c6-bms-final-format.trx`。
- 较早独立审查：`%TEMP%/oms-progress-audit-20260909-tests/core-skin.trx`、`mania-full.trx`、`bms-full.trx`。

精确合同见 [P1-A 测试与发布约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#测试与发布约束)。C6 实际六项 core 与四项 mania 消息见 [C6 报告](SKIN_SYSTEM_C6_VALIDATION_20260909.md#真实失败与关闭证据)。新增 [Compare-FailureBaseline.ps1](../../skin-c7-acceptance/Compare-FailureBaseline.ps1)读取真实 TRX，逐项比对 `testName`、错误首行类别及完整 `ErrorInfo.Message`，仅规范换行和首尾空白；失败集合也必须完全相同。它不删测试、不改预期、不压制告警，也不会将新增失败按数量抵消。

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

[实际制作演练](../../skin-authoring/docs/WORKSHOP.md)记录从静线模板新建 Aurora，再修改 `author.json` 中 BMS、mania、特殊键配色，生成完整双玩法文件、检查并打包的真实路径；作品不申请脚本权限。[作者工具实际记录](../../skin-authoring/docs/authoring-tool-verification.json)在 `2026-09-09T05:00:48.1115982Z` 使用当时工具执行三包正常检查、重复打包、重新生成后再打包。每包三次结果字节一致；坏 PNG、未知公开字段和确切行号、超过目录准入预算的 `skin.ini`、未结束的 `.pending`、源目录内部输出和已存在作者目录均按真实输入拒绝，修复来源后通过。

| 成品 | 此次作者实测 SHA256 |
| --- | --- |
| `oms-simple.osk` | `fe2688701ab349772b0670cdda8f3b5faaed87fe4cf24ec0ab5981e9d56571f4` |
| `oms-complex.osk` | `e7c02b7e5d6d7d04b0e446bc002972a1f88a5c517193324bd810769e6fdf26ed` |
| `aurora-study.osk` | `4cc5ec8f36191ab3995677a87b46930eb46ae2ba95d0896c1b11dbdc160c9b93` |

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

复核不是人工代签。候选运行曾通过两款与第三方的完整画面矩阵及真实观察谱，并暴露后续需修复的普通导出验证和目录入口问题；最终通过事实只采用最后编译对应的完整本轮结果。`c7-core-candidate.trx` 中原声音夹具身份实际 Passed 且新增安装等用例通过属于候选证据，最终 formatter 后重编译与复验仍须完成。

## 可直接使用的集中输入

[集中体验入口与步骤](../../skin-c7-acceptance/README.md)、[逐项记录表](../../skin-c7-acceptance/CHECKLIST.csv)和 [Build-Acceptance.ps1](../../skin-c7-acceptance/Build-Acceptance.ps1)将完整发行物、双包、完整作者套件、原 V-001/V-005 输入及新观察谱组装成独立便携目录。原 `.osk` 留在 `packages/`；正常导入只消费 `import-copies/`，可用 [Reset-ImportCopies.ps1](../../skin-c7-acceptance/Reset-ImportCopies.ps1)补齐。

发行套件只交付完整 `sources/`、`docs/`、独立工具 `bin/`、配方及工具 `tool-source/`、作者入口和正式 `dist/` 成品，不收录制作过程的 `work/`、`.previous` 或 `.pending`。原 V-001/V-005 包、原观察谱和原清单以原字节保存在随包 `skin-c7-acceptance/legacy/`。验收组装默认使用发行套件，离开仓库也不需要 Git、SDK 或原开发目录；构建来源由发行 `release-files.json` 的构建时 HEAD/dirty 和文件校验携带，组装记录保留该清单哈希。Aurora 作者练习成品也进入 `packages/` 和可消费导入副本。

[Generate-Inputs.ps1](../../skin-c7-acceptance/Generate-Inputs.ps1)只写新建输出目录，生成原创静音 BMS 5K/7K/9K BMS/9K PMS/14K 每轨短键与 LN、mania 1～10K 和 12/14/16/18K、静态/连续帧/坏帧第三方包、随包 H.264 MP4 背景视频及固定边框图片。它不读取用户皮肤、归档包或用户数据。

实际运行已生成 `%TEMP%/oms-c7-acceptance-inputs-b`；PowerShell 5.1 可执行，脚本保留 UTF-8 BOM。新增 [SkinCanonicalAcceptanceInputsTest](../../osu.Game.Rulesets.Bms.Tests/Skinning/SkinCanonicalAcceptanceInputsTest.cs)从提交的脚本自行生成全新临时输入，经真实 BMS decoder/converter（无 keymode override）和 mania legacy decoder/converter验证每轨短键/LN、原生舞台与视频引用；运行结果由根串行验证补入本页。单纯视频引用通过不能充当 GPU 显示或视频实机签收。复核发现早期 AVI 在无外部转码工具时只会静态回落，现改为随包直接读取 MP4；原创 AVI 生成及离线转码配方仍保留在 Generate-ViewportVideo.ps1。fixtures/viewport-evidence.json 记录本轮实际 H.264 Constrained Baseline / yuv420p、96×54、10 fps、6 秒、60 帧全部解码且帧摘要各异；同工具重复制作字节一致。玩家和组装过程不依赖 ffmpeg。

原生 mania 的 `ManiaModDualStages` 明确不改原生 mania converter，故不把“对任意 1～5K 原生谱启用 DS”写成可达体验。`LegacyBeatmapDecoder.MAX_MANIA_KEY_COUNT = 18` 是现有公开 `.osu` 读取上限，首次真实输入检查发现 20K 文件被正确限制为 9+9、18 轨短键与长条；本轮据此移除错误标注的 20K 观察谱与测试输入，而不改既有玩法规则或将它宣称为 20K 通过。公开入口的单舞台 1～10K、原生双舞台 12/14/16/18K 独立列明，其它内部支持形状（包括 10+10）由相应真实 host 自动矩阵说明。

## 覆盖更新与数据保护工具

[Update-Installation.ps1](../../skin-c7-acceptance/Update-Installation.ps1)随发行物以 `Update-OMS.ps1` 提供。新包必须处于另一完整目录并带 `release-files.json`；工具在第一笔目标写入前校验全部声明文件、目标运行状态和路径冲突，拒绝链接和嵌套来源，保留目标原便携模式，永不写入 `data/`、`storage.ini` 或外部保存位置。每次替换通过同卷 `File.Replace` 保存旧程序文件；新增文件通过同卷 move 安装。操作开始前写明 old/new 与已有文件清单；中断保留准确现场与修复说明，不声称跨文件系统事务或自动删除无法确认的旧数据。

[Test-UpdateProtection.ps1](../../skin-c7-acceptance/Test-UpdateProtection.ps1)已实际运行通过 portable、nonportable、custom 三类文件覆盖、ReadOnly canonical 覆盖及坏新包拒绝：运行模式、用户皮肤字节、bootstrap `storage.ini` 与原程序备份均保持，损坏新包在任何目标写入前拒绝。实际本机证据保留在 `%TEMP%/oms-c7-update-proof-0bc00594639649b0b41eb06009d287cc/results.json`。使用合成程序文件验证的是覆盖保护，不宣称本轮真实发行物启动通过。

[Create-CustomRootCopy.ps1](../../skin-c7-acceptance/Create-CustomRootCopy.ps1)只从已关闭的隔离验收副本创建 `app-custom/` 与 `custom-data/`；基础配置写在 `app-custom/data/storage.ini`。非便携人工冷启动要求专用 Windows 账户或虚拟机，不能为了测试打开现有用户默认根。

[发行冷启动取证步骤](../../skin-c7-acceptance/STARTUP-CHECK.md)限定全新隔离目录、启动前确认无现有实例、零导入参数、准确保存位置的当次日志，以及本次创建进程的正常退出。可复查实际日志应包含正确 Realm 路径、渲染初始化、设置和主菜单加载，结束时保留 Stopping / Stopped 与退出码。强制结束不能算正常关闭；无桌面显示的检查也不替代 GPU 与人工观感签收。

## 本轮实际宽测、发行物与终审

本节由最终执行者追加本轮实际命令、配置、结果、精确失败比较、发行物哈希、独立复核修复及人工边界。在证据尚未写入时，不依据本页的准备工作宣布 C7、Skin V1 或 release 完成。
