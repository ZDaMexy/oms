# P1-F 变动日志

## 2026-09-11

### BMS 预览修复版发行复验

- P1-A 修复真实 BMS 进入失败后，重新发布 `oms_20260911_preview-fix.zip` 与集中体验包；完整解压、只读原件、四轮首次便携/自定义位置/工作副本恢复/同包覆盖后正常启动、两测试根 BMS/mania 可用及 PS5 无 Git/SDK 组装均通过。三处既有根字节和属性前后不变，未打开原数据库；最新制品身份与证据见 [STATUS](DEVELOPMENT_STATUS.md)及 [C7 入口修复](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md#交付后-bms-预览入口修复)。先前 `_4` 的记录继续保留，原事故的事前快照缺失与全部人工未签事实不变。

### 最终真实多文件发行、四轮正常启动与无 SDK 验收组装

- 完成当前完整自包含多文件发布，实际 ZIP 为 `oms_20260911_4.zip`，SHA256 `bcf6aa8da7700f822db6613734dfc4af20d4bf57c5ff6d29f25cf5c4b2ce9ac8`；发行清单 SHA256 `d7ccb5bc66abedef7bf4f25ffca93b9b25b3605ddc976b89d991669a07899d57`。Windows Shell 从该真实 ZIP 解压，完整文件摘要匹配，两款 canonical 原件保持 ReadOnly/Archive，未由检查补写属性。
- `release-startup-final4/results.json`（UTC 09:08:41～09:10:58）实际通过首次便携、自定义位置、坏工作副本恢复和同包完整覆盖后启动。每轮均完成实际数据根和加载检查、八秒稳定运行、`Stopping/Stopped` 与退出码 0，无强制结束；原发行来源保持不变。坏工作副本按原字节保全，覆盖期间用户库、bootstrap 指针和原 portable 模式未变。本次是同一发行包覆盖，不冒充跨版本更新。
- 四轮共享两处隔离保存根。结束后仅打开额外复制件，以 Realm SDK Dynamic/IsReadOnly 读取 `Ruleset.ShortName/Available`，首次便携根及最终覆盖后的自定义根都确认 BMS、mania 可用，源与副本摘要不变；不声称取得四份独立时点数据库快照。中间两轮仍以各自日志、恢复和正常退出记录为证。
- 本次账户 bootstrap、事故根与原 G1 根的全文件字节和属性前后一致，未直接打开原数据库；该受保护复验不能追溯证明首次误入保存根事故无影响。首次事故、事后保全以及缺少事前快照的事实全部保留，不自动回滚或清理。
- 最终发行中的作者程序摘要与独立制作演练版本一致。以真实完整包在 PS5、PATH 无 Git/SDK 的环境组装 `release-repo/oms-skin-c7-acceptance-20260911-final`，`acceptance-final4-assembly.json` 为 Passed，来源字节和属性不变；这次已替代早期占位程序组装的当前证据。
- P1-F 状态与计划收敛到实际完成的发行/恢复能力，以及仍需独立环境、设备、画面和长期体验签收的事项；V-001～V-004 0/4、V-005 未签收，不宣布整个 Skin V1 或 release 完成。集中证据见 [C7 验证记录](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md)。以下本日小节保留故障定位时的历史状态，后续复验结果以上述最终包为准。

### 真实启动误入旧保存根与发行方式纠正

- 完整自解压候选包实际将 `AppContext.BaseDirectory` 指向 TEMP，忽略原安装旁的 `portable.ini`，进入当前账户既有自定义数据根并执行 Realm schema 57 迁移。本轮事后数据与指针已完整逐字节保全到私有 `startup-incident-preserved` 记录；没有事前该根快照，不能声称数据无损、恢复原状或完成回滚。公开文档不展开账户路径。
- `build-release.ps1` 改为 `PublishSingleFile=false`、self-contained 的完整多文件 ZIP，入口仍为 `osu!.exe`。仅去掉 `IncludeAllContentForSelfExtract` 会让规则集 DLL 留在 bundle 内，无法满足现有 `RulesetStore` 物理 DLL 发现，因此不采用不完整的 single-file 修补。
- `Program` 使用 `HostOptions.PortableInstallation = OsuGameDesktop.IsPortableMode`，令便携框架缓存位于程序旁 `cache/`，用户库仍为程序旁 `data/` 或其中 `storage.ini` 指向的自定义根。两处修正的真实发行启动、保存/缓存位置与正常退出尚待复验，不在此提前记录通过。
- 2026-05-09 的完整自解压“冷启动通过”原记录保留；其观察仅覆盖窗口/进程和 smoke，没有证明实际保存根，不能继续作为便携隔离或用户数据保护证据。新的逐轮验收要求读取本轮实际 `client.realm`、日志、canonical 工作副本和缓存位置，非便携运行只用独立账户/虚拟机。
- 作者工具已独立验证的自包含 single-file 发布与游戏路径问题无关，保持不变。当前仅同步发行合同与事故事实；完整发行退出门仍未闭合。

### C7 只读来源覆盖、真实中断现场与 ZIP 属性

- 独立合成复验先发现来源只读使 `File.Replace` 中途拒绝，随后真实 NTFS 硬链接揭示解除旧件只读后进程中断会改变作者原件属性。最终 canonical 只把旧件原样 move 到本次 old，再将已在私有暂存设只读的新件 move 到目标；旧件全程不写属性或内容，不增加无法保护新增链接窄窗的 link-count 包装。其它程序文件仍 Replace。真实硬链接、两次 move 间目标冲突及仅终止本次进程后的同包重试均实测，原中断现场、用户与作者数据保留；两次 move 间安装原件可暂缺，不声称单次原子替换。
- 发行 ZIP 显式记录两款原件 DOS ReadOnly/Archive；PS5 `Compress-Archive` 生成反斜杠条目名，按规范化路径定位唯一原件后写属性。实际 Windows Shell 解包保留只读，而 .NET/Expand-Archive 忽略；最终启动检查改为核验来源和副本，不主动补位。
- 移除交互验收入口对现有 canonical 原件补只读属性的遗留操作，防止经硬链接改到作者文件，也不掩盖解包差异；原件缺失或损坏仍可打开游戏修复界面，不增加额外启动门。
- 导入副本补齐工具改为仅新建缺失项，已有普通文件原样保留；消除 Force 覆写用户修改或硬链接作者文件的边界。PS5 实际完成首次补齐、已消费项补齐、重复运行及真实硬链接保护，说明同步要求重做现有项先改名保全。
- 作者工具改用每次唯一新发布目录，完整复制本次输出，既有作者 bin 不猜测清理或混入。合成检查和文件复核不代替最终发行启动、独立工具执行或人工验收，详见 [C7 验证记录](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md)。

## 2026-09-10

### C7 原验收输入固定与离开仓库后的组装

- 将本机原 V-001 good/broken、原观察谱、V-005 Momentum、原清单与原 V-001 摘要按原字节固定到 `skin-c7-acceptance/legacy/`，六个原文件合计 780,237 B；没有重生成或改变未签收事实。
- `build-release.ps1` 前置验证固定摘要，移除对未提交 `artifacts/` 的依赖。验收组装同样前置验证原输入、完整独立作者文件与安装原件/作者成品一致性，缺件在建立输出前报明。
- 在独立 TEMP 发行夹具中使用 PS5，子进程 PATH 不含 Git/SDK，实际组装双包、Aurora、第三方与原 V 输入、MP4、完整作者文件；来源全部字节及属性保持。程序与工具使用明确占位文件，证明组装独立性，不代替真实工具、游戏启动或人工验收。证据见 [C7 验证记录](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md)。
- 集中体验说明补入“维护 → 内部谱库 → 扫描内部谱库（增量）”，避免新副本有观察文件却没有选歌记录。

## 2026-09-09

### C7 随包制作工具与保模式覆盖更新

- `build-release.ps1` 串行发布自包含作者工具，随包加入双 canonical 原件、普通双包、源文件、完整作者套件、验收脚本及 `release-files.json`；递归清理前核对 publish 的绝对父目录、固定子名及整树无 reparse。
- 新 `Update-OMS.ps1` 按完整性清单在第一笔目标写前校验，保留原 portable marker、data/bootstrap storage.ini 与外部数据；同卷 replace 保留旧程序及 durable 中断说明，无法确认的旧内容不猜测删除。canonical ReadOnly 只在已验证的安装路径更新时暂时解除并恢复。
- 中英随包说明与 RELEASE 已补齐真实自定义保存位置和非便携模式流程；提供实际可执行更新命令，不只留下操作提醒。
- `Test-UpdateProtection.ps1` 已实际通过 portable/nonportable/custom、原件只读更新与坏新包拒绝；合成文件验证不代替真实发行物冷启动。证据见 [C7 验证记录](../../other/SKIN_SYSTEM_C7_VALIDATION_20260909.md)。

### 发行实现与证据时效复核

- 核对打包脚本、portable marker、OsuStorage重定向及禁用updater；将最近明确的publish/fresh extract/smoke证据标为2026-05-09，不把近期代码build当作新包验收。
- 记录随包中英更新说明缺storage.ini提醒、非便携覆盖全包引入portable.ini会切换基础数据根两项实际缺口；PLAN补对应动作，约束明确storage.ini位于基础数据根。代码未改，本线未publish或启动用户发行物；全仓运行结果由主线本次审查记录汇总。

## 2026-07-16

### 文档健康治理：发行 ownership 收敛为包体、数据根与覆盖更新

- PLAN 不再与 P1-G 重复拥有桌面拖放、Song Select 或通用 UI smoke；P1-F 只提供候选发行物、portable/custom root、覆盖更新和发行专属步骤，最终人工结果交 P1-G 汇总。
- STATUS 移除已建档和 VS Code/Python 终端处置细节；这些实现史仍保留在 2026-05-09 记录，当前页只保留发行能力、风险与 gate。
- 当前发行能力与产品 gate 未变；本次仅改文档，未改代码，未运行产品测试或 Release。

## 2026-05-09

### 内部 OMS 版号切换兼容护栏

- `ChangelogOverlay.ShowBuild(string)` 不再硬拆 `版本-流`，遇到纯 `oms_YYYYMMDD` 版号时会安全回退列表，不再因为缺少上游 `-stream` 后缀而错误解析构建信息。
- `OsuConfigManager.Migrate()` 现可同时解析旧上游日期版号与 `oms_YYYYMMDD`，避免未来切换内部 OMS 版号时迁移逻辑静默失效。
- 这条兼容护栏不改变当前离线便携发布链，只补齐未来版号切换前必须存在的最小保护。

### single-file 发行物补齐完整自解压并复核冷启动

- `build-release.ps1` 现已在 `PublishSingleFile=true` 之外显式保留 `IncludeAllContentForSelfExtract=true`，避免 fresh extract 的便携发行物首次运行只创建 `data/` 后就无窗退出。
- 这次修正不改变当前离线便携发布策略：正式发行物仍是 `oms_YYYYMMDD(.zip)`、`portable.ini -> data/` 与手工覆盖更新，只是把 single-file 冷启动合同补回到可发布状态。
- 验证：重新执行 ` .\build-release.ps1 ` 后，新解压的便携 zip 冷启动通过；`.\SmokeTestDesktop.ps1 -Configuration Release -WaitSeconds 8` 通过。

### 工作区关闭 Python 终端自动激活以稳定发行脚本运行

- 工作区级 `.vscode/settings.json` 现已加入 `python.terminal.activateEnvironment = false`，避免 VS Code 直接点 Run 执行 `build-release.ps1` 时，新 PowerShell 终端又被 `.venv` 自动激活命令打断。
- 这次修正不改变 OMS 正式发行链：仓库当前没有 Python 源文件、`pyproject.toml`、`requirements.txt` 或 Python 任务；根目录 `.venv/` 仅为本地工作区环境，不属于 OMS 正式构建 / 测试 / 发行链。
- 验证：工作区 `.vscode/settings.json` 已更新且无错误；仓库级 `.vscode/settings.json` / `.vscode/tasks.json` 未发现项目级 Python 依赖配置。

### 发行包新增中英双语 `how to update.txt`

- `build-release.ps1` 现会在发行根目录生成 `how to update.txt`，并随 `oms_YYYYMMDD(.zip)` 一起打包。
- 该文件同时提供中文与英文的手动覆盖更新步骤，并以更精炼的终端用户口径强调：先退出程序、覆盖整个压缩包内容，以及在便携模式下保留 `portable.ini` 与 `data/`。
- `../../other/RELEASE.md` 与本子线状态文档已同步到“发行根目录额外包含一份中英双语手动更新说明”的口径。
- 验证：PowerShell 语法解析通过；实际执行 ` .\build-release.ps1 ` 成功生成 `release-repo/oms_20260509_2.zip`，且 `publish/` 与 zip 根目录均已确认包含 `how to update.txt`。

### 离线覆盖更新审计与发行命名口径同步

- `P1-F` 当前正式打包入口已明确为 `build-release.ps1`，输出 `release-repo/oms_YYYYMMDD(.zip)`；发行根目录继续保留 `osu!.exe` + `portable.ini` + `lazer.ico` + `beatmap.ico`，不再把当前 release 误写成“严格只有一个 exe”。
- 手工覆盖更新链已重新审计：`portable.ini -> data/` 与 `storage.ini` 自定义数据根路径继续成立，覆盖后不会进入 Velopack / 安装器自更新；当前实际风险主要是程序未退出时覆盖文件，或误删 `portable.ini` / `storage.ini` 造成数据根切换。
- 同轮已把 `../../other/RELEASE.md`、`../../mainline/DEVELOPMENT_STATUS.md`、`../../mainline/CHANGELOG.md` 与根 `README.md` 同步到当前发行物命名、覆盖更新步骤和注意事项口径。
- 验证：`dotnet build .\osu.Game\osu.Game.csproj -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

## 2026-04-20

### 子线正式建档

- `P1-F` 已建立独立目录与四件套文档。
- 当前仅完成文档结构治理，未新增代码、构建或测试执行。
