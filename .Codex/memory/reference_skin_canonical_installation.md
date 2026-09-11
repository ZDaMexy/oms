---
name: reference_skin_canonical_installation
description: canonical 只读原件、工作副本恢复、旧记录与导出 authority 的排查边界
metadata:
  node_type: memory
  type: reference
---

# Canonical 简洁皮肤安装与旧数据排查

状态与签收只读 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)，完整合同见 [P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)。这里记录迁移时容易混淆的证据，不替代验收。

- 安装原件是程序目录 `Skins/Canonical/oms-simple.osk`，SHA-256 锚点嵌入 `osu.Game`，不是从旁边可一起改写的 manifest 取得信任。每次启动先验证原件，再处理当前数据根 `skin-canonical/oms-simple.osk`；原件缺失/损坏时，即使工作副本完整也不能绕过安装修复。
- 工作副本通过 held no-follow 父目录、完整临时 archive 的 `Flush(true)` 与 no-replace rename 发布。坏的既存副本先以 held file handle 改名到 `*.preserved-*` 保全；中断会留下完整旧件或完整临时件，重启重新从原件恢复，不按名称猜删遗留件。目录、reparse 或文件锁不能被当成缺失并清空。hardlink 的其它名字指向的内容不能被修改。
- 真正 canonical 是完整 archive admission、capsule capture、普通 `BmsLegacySkin` factory 与 shared author-package preparation 成功后的具体实例。`IsCanonicalSkin` 使用实例信任表；`Protected=true`、固定 ID、名字或某个类型都不是 canonical 视觉 authority。
- protected Realm 元数据 `Hash=oms.skin.canonical.simple.v1` 是跨发行稳定的记录标记，**不是**内容 hash。真实运行内容用 capsule revision，安装内容用发行 SHA-256。不要拿这个字段替代内容校验。
- 受支持旧 journal 先对 exact 旧 `OmsSkin` / 新 canonical protected record 恢复，然后才迁移旧元数据。未解 journal 时，原本没有 protected row 就必须仍然没有；先补新 row 再 Retry 会制造之前缺失的恢复证据。缺 fingerprint/manifest/tombstone/disposition 的旧 intent 仍然 Invalid，不补字段、不猜删。
- 旧固定 ID 也可能含不能确认归属的用户数据。只有完整 exact protected metadata 且无用户 files 的已知内置记录能更新/移除；未知同 ID 的 Name/type/hash/files 保全并要求修复。不要因 ID 看起来内置而覆盖或删除。
- 新导入旧 `OmsSkin` metadata 的 archive 改用普通 BMS/mania parser；已经存在的非 protected 旧 Oms record 只在内存中按 exact 原用户 files 构造同一普通 parser，不能改 Realm type/hash/refs 或补旧嵌入资源。选择和 reload 两条路径都须覆盖。
- protected canonical 导出按钮需显式允许已验证 instance 对应 exact metadata。普通 `.osk` 导出应保留整份作者包，不能因 protected record 没有 Realm files 而导出空包；未知固定 ID 带用户 files 的记录又必须导出其真实 files，不能按 ID 静默替换成简洁包。
- `EnsureMutableSkin` 的 canonical 副本也必须由完整普通 archive 导入，不能延用旧“只造同类型无文件记录”的内置皮肤方式。author-manifest 选择走已有异步 publication，创建副本返回不等于已切换；测试应等真实 current pair，legacy editor 仍按现有合同冻结。
- 核对恢复/UI时同时看 `IsGameplaySkinInstallationAvailable`、安装修复说明、journal 状态与真实 current revision。内存中有经过验证的简洁包，不表示未解的旧数据操作已经允许进入游玩；修复界面占位 skin 也绝不是可玩的备用主题。
- protected row 故意缺失时，`GetAllUsableSkins` 与前后轮换的 implicit list 不能直接 `Find(...).ToLive(...)`：`RealmLive` 构造会解引用空记录，造成修复设置丢失用户列表或切换异常。两处列表仅使用既有 `DefaultOmsSkin.SkinInfo` 内存占位；不能补 Realm 行。安装或 journal 仍阻断时，exact protected 默认项的 `CanExport` 与 `ExportSkin` 同时拒绝，普通用户包不因此禁止导出。用真实旧用户包加缺行 invalid journal，以及独立新根未知 `skin-canonical` 文件阻止工作副本创建两路验证，不能改写真实安装原件来制造测试前提。
- 2026-09-11真实备份G1曾出现普通包游玩拒绝、managed扫描零新增、external登记false三个表现，实际是同一旧protected记录被阻断。仅在失败working Realm的再复制件以SDK`IsDynamic + IsReadOnly`读取，确认完整元数据精确等于归档`dirty-stash^3`中`BmsOmsReferenceSkin.CreateInfo`；同一归档manager确有写入路径。该无用户files记录可在journal已resolved后定点迁移到canonical；不能把旧reference类型重新载入，不能把它加入`IsExactProtectedFallbackRecord`扩张旧journal authority。未知字段、附用户file和未解journal分别保留阻断。归档只取单文件证据，未改原数据或私有baseline，G1不得先替换旧行来造通过前提。
- 缺行恢复测试应在执行菜单轮换前核对启动的canonical current；前后轮换按既有规则可能选中普通用户包，不能据此要求current仍是启动默认实例。轮换后要核对pair一致、缺行/journal未变以及游玩仍被拒绝，不能添加更宽的选择禁令来迎合错误测试前提。
- G1补充的“预存用户包”若archive文件名与`skin.ini`作者名称不同，普通导入会按既有合同追加archive名称并替换ini引用，产生本次新建的零引用旧ini；重启的`RealmAccess.cleanupPendingDeletions → RealmFileStore.Cleanup`合法清理它。2026-09-11九项最终保全断言曾把导入后的全部files都当成旧用户数据，缺失hash实际等于fixture初始ini，原baseline八个blob未丢。失败working副本的只读再复制取证确认重启前旧ini记录backlinks=0、真正保留皮肤仍有两条文件引用；重启后只有零引用临时项消失。fixture包名应匹配自己的INI名称，避免无意义重命名；全部原记录、原blob、实际用户引用和其它原内容保护断言继续保留，不修改FileStore让临时项永久积累。

人工观感与设备签收不能由上述安装检查代替。旧 `OmsSkin` 代码的保留用于旧证据与人工对照，不允许被新的普通导入、默认选择或安装故障回落重新接回产品链。

- 首次外部目录登记先要`chartskin`物理防重叠证明；旧测试预先mkdir曾遮蔽新安装无法登记的真实缺口。生产仅通过显式`OpenForFirstWorkspace`创建空自有根：coordinator lease、journal Missing、无任何旧filesystem声明、作者held proof排除祖先重叠、新根native no-follow/no-replace/identity复核。不要把创建能力加给scanner/recovery Open；旧记录但根缺失仍保全并提供恢复原目录指引。
- Windows PowerShell `-Command`后追加路径不等于脚本参数，路径含空格会被重新解释；native junction测试用独立`.ps1`与`-File`、`ArgumentList`逐个传参。ZipArchiveEntry读流不可seek，使用`ReadAllRemainingBytesToArray()`。

- C7 完整包不能只测槽位ready和原生owner隐藏：hud.text一次接管会同时遮掉准确率/进度，必须观察新的实际文字和状态；同一测试host同时挂两玩法时必须给各自事件子树缓存本玩法processor，并沿Player顺序接NewResult/RevertResult，否则可能只有判定事件而分数恒零。当前证据与剩余门从[P1-A状态](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)进入，暂停检查点只保留历史。
- PowerShell 5的`Compress-Archive`不会自动携带源文件DOS只读标记，条目名还可能使用反斜杠。发行脚本按规范化条目名定位唯一canonical原件并写`ExternalAttributes`；Windows自带ZIP解包实际保留标记，`Expand-Archive`/`.NET ZipFile.ExtractToDirectory`可能忽略。启动取证不得先补属性掩盖差异。游戏只读捕获与固化摘要验证独立于DOS标记，不能以标记替代完整性。
- `File.Copy`会把只读发行原件的属性带入更新暂存副本，`File.Replace`消费只读副本/目标时可能Access denied。不得通过临时清掉旧目标只读再恢复来修正：目标若与作者文件hardlink，属性写入也会改变作者原件；即使持有FileShare.None仍可添加新hardlink，链接数双检不能关掉该窗口。canonical安装覆盖只对本次自有new副本设只读，将旧目标no-replace Move到old，再将new no-replace Move到目标，全程不写旧件内容或属性。两次Move之间可能缺目标，不称单次原子替换；Applying收据、old/new与修复说明保留，同一完整更新包再次运行完成覆盖。工作副本的原子恢复合同不变；安装原件缺失时游戏阻止进入谱面。实测必须包含真实只读hardlink、两次Move间目标冲突与定点终止后重试，并核对作者字节/属性及中断现场始终保持。
- 独立作者工具发布使用每次全新输出目录，并只把该次输出放入发行套件；向既有bin反复publish会混入旧产物或未知文件，不能据“本次publish成功”认为整个旧目录可交付，也不能猜测删除旧bin内容。
- canonical也由普通`BmsLegacySkin`构造，产品夹具不能以`CurrentSkin is BmsLegacySkin`独自判定一次异步选择完成；它可能在默认外观尚未离开时已为true。应等待本次请求ID、实际实例SkinInfo.ID与current revision owner一致，再保存后续参与者归属；不能改释放次数或放宽等待来隐藏错误revision。
- 普通文件皮肤的main HUD由`LegacySkin`创建`LegacyHealthDisplay/LegacyScoreCounter/LegacyAccuracyCounter/LegacySongProgress`。完整成品替代检查应核当前这四个真实owner及原有stage/global控制键、原owner隐藏和替代信息可见，不能强求旧Oms的`Default*`派生类。旧无纹理程序化组件的颜色断言应隔离所给皮肤来源；默认`SkinProvidingContainer`会继续向父canonical查图，按既有“纹理拥有颜色”合同得到Sprite，Box查询失败不表示颜色parser失效。不能为了历史Box断言重新接回程序化产品fallback或给作者纹理强加颜色。
- 第三方无manifest而从canonical补齐HUD时，`PreparedScene.CreateEmpty`仍会建立Semantic替代，但`PreparedHudPlan`若只认selected declaration/Scene/Suppress，会漏掉纯CanonicalPackage的gauge/组合/判定：真实表现是scene与HUD已ready、Text分区存在、Gauge分区为零，原生命条与补齐内容可同时留在画面。路由应接受该实际包替代，仍受supported、完整global/stage、相同snapshot、rect与资源预算约束；不得伪造IsSelectedDocumentDeclaration、借载canonical整scene或恢复作者Suppress。检查必须继续核原owner隐藏及实际必要信息，不删分区要求。

- 2026-09-11 真实 Release 取证推翻旧 single-file 完整自解压“便携已通过”：IncludeAllContentForSelfExtract 开启 netcoreapp3 compat，AppContext.BaseDirectory落到TEMP/.net，portable.ini和canonical原件不在该根；窗口和MainMenu可以正常出现，却已读取当前账户bootstrap storage.ini并迁移既有用户库。不得只观察窗口、进程或Running，也不得在开始前创建data/就认为隔离成功。正式改为PublishSingleFile=false自包含多文件，保留RulesetStore现有物理DLL扫描；仅移除IncludeAll而仍单文件会重现旧玩法发现问题。Program用现有HostOptions.PortableInstallation=OsuGameDesktop.IsPortableMode同时隔离框架缓存（exe旁cache/），游戏数据仍data或其storage.ini目标。实际复验必须逐项匹配本次保存根、Realm日志、只读原件、工作副本、双玩法发现与正常退出，且已有默认bootstrap/用户根前后全文件hash和属性一致。
- 该启动事故原件未事前取该根快照（与G1备份根不同），事后完整字节副本与pointer已保全；只在第二副本以Realm SDK IsDynamic/IsReadOnly、schema57核可读及引用存在，不调用RealmAccess或SkinManager（会执行cleanup/migration）。可读不证明事前未变；0of0 blob cleanup也不覆盖全部DeletePending清理。无本次forward迁移自动备份证据，不拿旧corrupt备份猜测回滚。
- 修改含中文的Windows PowerShell 5入口必须保留UTF-8 BOM；用UTF8Encoding(false)覆盖原文件可令PS5将UTF-8当本机ANSI，轻则随包中文说明乱码，重则引号误解导致ParseError。先用实际powershell.exe Parser.ParseFile核对，再跑完整入口；仅pwsh解析成功不算PS5证据。
- 2026-09-11首次真正正常退出揭露启动扫描从未成功进入：`release-startup-portable-repair3` runtime在Stopping/Stopped后写“Managed skin folder scan ended unexpectedly.”，原隐私边界未记录异常message/stack，不能据退出时刻猜成取消。`c7-startup-recovery-red.trx`真实OsuGame worker（空根/已有普通作者目录）及有journal生产authority三格均给出同一`EnterMutation`重入异常：外层StartupSequence里再次申请mutation被拒绝，fault直到Dispose join才显露；旧lifecycle夹具覆写recovery/scan，未覆盖真实衔接。不能仅改为Enter短lease，因为恢复native/外部registry要求mutation authority，会把合法旧journal误判Ambiguous。专用EnterRecovery只在本线程StartupSequence depth=1时给予恢复子lease，保留外层owner、epoch、selection retry completion和后续扫描，其他路径继续使用不可重入mutation；补实际首启扫描/有journal恢复及取消、不提前释放检查。修复后结果应以当前focused和完整Release启动复验为准，不能把红测复现写成通过。
- `SkinManagedFolderMutationRecoveryAuthority.TryOpen` 曾在 `Session.Validate` 前清空 native/registry 局部，真实验证中取消后 caller 尚未获得 Session，finally 也已无资源可释放。必须 Validate 成功后才清空局部并移交；拒绝或取消由原 finally 释放，false 分支不再提前 Dispose，避免重复释放。回归使用真实 Windows managed-root 与 external registry，在底层 Validate 后取消，核原 OCE token、外部文件字节、Session/句柄各释放一次及关闭后查询失败；包装器只计数和定点取消，不伪造物理证明，失败夹具也须释放自身资源。不要吞掉 OCE 或扩大 catch。
