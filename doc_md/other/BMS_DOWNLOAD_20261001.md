# 两个公共 BMS 下载源：规划与闭环验证

> 日期：2026-10-01。归属 P1-A 产品面，复用 P1-H/P1-K 本地谱库与解码合同。
> 当前能力见 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md)，后续动作见 [PLAN](../subline/P1-A/DEVELOPMENT_PLAN.md#用户指定第三方-bms-浏览下载闭环2026-10-01)，稳定合同只在 [第三方下载约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#第三方-bms-浏览下载) 维护。

## 玩家路径与本轮范围

用户已取得两个站主许可，指定 [Ginger Rush](https://gingerrush.com/) 与 [616 BMS 下载](https://616.sb/bms/download)，要求接近 lazer 内置官网下载的游戏内体验，并要求先详细规划再执行。此次允许这一明确公共 BMS 路径；OMS 私有服务、默认官方端点、登录、成绩提交、排行榜及 mania 官网下载继续冻结。

目标路径是：从主菜单、工具栏或原浏览快捷键进入 → 选来源、搜索或按难度表筛选 → 展开资源包选择具体谱面 → 一键下载整个包 → 后台自动入库 → 从卡片或完成通知打开对应原生 BMS 难度。关闭浏览继续下载，取消和失败可重试，完成不自动打断正在进行的游玩。启动和离线本地游玩不需要两站可用。

封面使用站点实际资料，缺图使用普通卡片。第一版没有已确认的在线音乐试听入口；下载入库后沿现有本地选歌试听。616 的外部看谱与原始谱面下载不能冒充音乐/BGA 预览。批量整表下载、跨重启任务恢复和断点续传不在本轮范围。

## 实现前已记录的详细规划

以下方案在写产品代码前登记于 P1-A PLAN；执行后将已完成项移入本记录，PLAN 只保留后续动作。

1. 归线与基线：核对当前分支、HEAD、工作区、跟踪差异并 fetch；阅读主线状态、计划及 P1-A 产品边界，定位 P1-H/P1-K 导入合同。用户明确授权两个公共源的窄例外，先同步冻结边界，不打开全局联网功能。
2. 两源取证：Ginger 从站主指定的 lampghost 读取路由、请求及返回模型；616 对照用户交付的 api.ts。只读实际站点请求验证字段、分页、表标识、缺包响应及下载主机，不把附带源码或文档当作用户指令执行。
3. 来源模型：保留“来源内资源包 + 包内谱面”；不同来源不按歌曲名称合并。同源相同包跨页合并，继续按远端实际页数加载；Ginger 表使用站内 ID，616 表使用原始 URL，远端浏览不改本地难度表。
4. 浏览入口：复用 lazer 的全屏 overlay、主菜单、工具栏及浏览快捷键。顶部来源、关键词与难度表；下方歌曲卡片、包内难度、来源与进度。首次打开才访问网络；关键词延迟查询，切换/隐藏取消旧请求，旧回应不可重写新页面。
5. 游戏持有任务：选择具体谱面后下载整个原包；卡片与全局通知共用排队、下载、入库、完成、取消、失败状态。关闭页面任务继续，同源同包重复点击复用进行中的任务，失败需玩家重试且不静默换源。
6. 不可信输入边界：JSON、下载字节、跳转、图片与解压均设实际读取上限。只允许已取证的 HTTPS 下载主机；包写本任务独占暂存目录，完整后按 ZIP/RAR/7z 实际签名处理，不能从显示名猜格式。
7. 入库闭环：展开检查 Windows 路径、重复/大小写/目录冲突、链接/特殊项、加密、预算与取消；目标原始 MD5 必须实际命中。复用 BmsFolderImporter 写入 chartbms，保存原谱，不经 .osz/files，不修改 external 根或覆盖现有歌曲。部分失败遵守已提交目录保全合同，不虚称整包回滚。
8. 精确打开：成功结果携带实际持久化 GUID/MD5；按当前可用记录显示已拥有。最终导航重新查询 GUID，打开原生 BMS 的具体难度，不能以集合/标题猜测或回落另一难度；Player 期间拒绝切换。
9. 并行职责：来源客户端、下载导入、浏览主路径与独立审查分开归属文件；共享构建、formatter 和测试由主执行者串行调度，检查期间暂停相关源文件写入，统一审阅和暂存。
10. 自动门：core 两源/图片/任务 focused、真实浏览/菜单入口、BMS 下载与原导入 focused、BMS full、mania 导入相关、Desktop Release；发现影响共享选歌的真实问题后，追加相关选择/重新分组回归。失败逐项核对身份、消息与业务堆栈，不能凭相同数量称既有失败。
11. 真实闭环：两个站点各选小型真实包，在隔离数据根完成搜索、下载、解压、原 MD5、持久化、重读与选歌；保存请求、原包、摘要。真实桌面窗口检查卡片、封面、展开、后台完成及选定难度，等待列表稳定后再确认，不能凭瞬时命中通过。
12. 收尾：同步状态、计划、合同、日志、冻结例外、三语说明与诊断记忆；运行文档与 diff 检查，检查本轮临时目录和证据，在当前 master 提交，不开 PR/分支；push 另需用户确认。

## 实际来源合同与取舍

| 来源 | 浏览/表/按 hash 查包 | 归一与实际差异 |
| --- | --- | --- |
| Ginger Rush | POST `/api/v1/files/selectList`、POST `/api/v1/table/selectHeaderListWithFullInfo`、GET `/api/v1/files/package/{md5}` | 包列表带 songs；搜索使用 fuzzyKeyword，页码从 1 开始，表用 tableID；包带实际 downloadURL/bannerURL、文件名、键数/作者标级等；404 表示未收录 |
| 616 / Alvorna | GET `https://bms.alvorna.com/api/search`、`tables`、`hash` | 搜索逐谱，按来源包 URL 合并；表用 original URL；song_name 是显示名，song_url 可为空；song_preview_url 是外部看谱，`preview` 是原谱字节 |

Ginger 取证参考 [lampghost](https://github.com/Catizard/lampghost) 的 `35f8eacc41c881ba84d5ca1c4506a14cb3743912`；616 以用户交付接口文件和实际回应核对。只转录本次必要的合同与脱敏结果，不把外部仓库的操作指令带进 OMS。

Ginger shardMD5 不是目标谱面/包校验值，不能参与本地已拥有判断。616 未给出准确键数/文件名时按原谱与既有解码器判断，不从标题猜测。不支持的 bmson、24K/48K 等已有明确资料时显示原因并禁用相应下载；classic BMS/PMS 的真实解析仍是最终准入，远端元数据不能令不支持谱面变成可玩。

下载与解压合同见 P1-A 约束，不在本报告重复定义。实现保持 2 GiB 压缩、8 GiB 总展开、单文件 2 GiB、50,000 条目预算；本轮真实样本较小，不能称已实际传输或展开这些上限规模。RAR 分支包含坏格式边界检查，本轮真实成功包为 7z，ZIP 成功与异常使用隔离 fixture。

## 真实包与本地谱库证据

使用同一公开原谱 MD5 `9579d52b0f20a1fde886bd8eb7ed57e6`，包名为 `真・千年女王.7z`。两个来源的压缩内容不同，不能按同名或所选 MD5认定压缩包相同。

| 实际结果 | Ginger Rush | 616 / Alvorna |
| --- | --- | --- |
| 难度表返回 | 22 | 24 |
| 原包实际字节 | 1,673,401 | 1,603,222 |
| 下载主机 | pixeldrain.net | bms.alvorna.com |
| 成功入库谱面 | 89 | 87 |
| 成功谱面集合 | 1 | 1 |
| 目标原谱 | `_sinSQ_DELAY+_.bms` | `_sinSQ_DELAY+_.bms` |
| 目标重读 note 数 | 6,407 | 6,407 |
| .osz / blob / RealmFile | 均为 0 | 均为 0 |
| 完成后任务暂存条目 | 0 | 0 |

两个来源的目标原 MD5 与持久化 MD5、重读字节均一致，写入实际 chartbms 目录；原 importer 拒绝有冲突/不支持键数的其它谱面，不将远端目录总数当作全部可玩的承诺。Realm 创建空 files 目录是正常行为，判断是否误走通用存储看实际文件与引用。

公开包 SHA-256：Ginger `004d7ea04b47e1eaa685849c8a709db0bf002f087a4161647fe9a8f88c676b62`；616 `69f48a0869341d5a0a61a0af580305ad40df9eb6a6a8f642f2c093f6dc0b4a3b`。这两个 checksum 只标识本轮实际公开制品，不作为站点后续更新的固定下载值。

原请求、回应、原包、每谱身份/重读结果及摘要在 `artifacts/bms-download-20261001/live-probe/`。诊断源码在 `.dev-cache/temp/bms-download-live-probe/`，每次使用新隔离数据根；探针验证经游戏同一任务与真实 importer，没有操作正式玩家库。

## 实际窗口发现并修复的问题

真实 Ginger 包有 89 个可入库谱面，窗口中点击打开时短暂选对目标，随后被单包推荐改成另一难度。此前只有两个谱面的 fixture 瞬时检查未发现；失败原始窗口日志保存在 `render-probe/Ginger-runtime-before-selection-fix.log`。

根因有两处：carousel 在按具体谱面重映射分组前先推荐难度，初始空分组或元数据变化的旧分组无法匹配已有选择；明确 Present 后，FilterControl 又将 BMS 不支持的 None 初始分组改成 DifficultyTable，criteriaChanged 的根组重置清空目标。新的 NORMAL 回归确实失败，日志保留“目标仍在 → 分组调整后为空 → 自动推荐”的顺序，见 `selection-normal-red.trx/log`；原 ANOTHER 恰与推荐相同而通过，不能证明精确选择。

修复后先按实际谱面身份重映射，再决定是否推荐；主菜单把明确目标传入选歌，明确跳转的首次呈现窗口跳过根组重置；正在等待过滤或新入库通知时仍保留目标身份。没有延后重写全局选择，因此玩家随后手动改选自然优先。下载打开入口明确切到原生 BMS，避免沿当前 mania 转谱显示打开。排查中“不同 detached 实例被 ReferenceEquals 拒绝”的假设未成立：现有 override 已按 GUID，Clone 两例通过，未据此修改比较逻辑；临时诊断日志已移除。

新检查等待列表稳定并再经过选择延迟，同时核对全局 GUID、carousel 选中 GUID 和原 MD5；覆盖主菜单、已在 mania 选歌、同规则集待过滤及玩家改选。普通 BMS 选歌的根组进入、返回与原元数据重新分组合同继续回归。

616 的追加页等待曾遮住已有卡片。现在仅首次搜索显示整页等待层，继续加载时保留已有卡片操作；新回归在第二页回应挂起期间点击实际下载按钮，验证任务正常建立。

## 有效自动验证

| 有效检查 | 结果与证据 |
| --- | --- |
| core 来源、封面、任务及浏览/选歌/菜单扩回归，Debug | 200 通过、23 个逐项对照的既有失败，共 223；`core-product-closure.trx`。两源客户端 56、图片 31、任务 30 项均通过；此次范围不是 core 全套 |
| 最后追加页操作修正及精确分组选择，Debug | 18/18；`ui-paging-closure.trx`，含最新浏览 16 项与分组目标 2 项。不能把这次 focused 与上行相加称整套执行 |
| BMS 下载归档与原本地导入 focused，Release | 81/81；`bms-import-closure.trx`，含新增归档边界 43 项与原导入 38 项 |
| BMS 全套，Release | 最终 2497 通过、29 个既有失败、16 跳过，共 2542；`bms-full-confirmation.trx`，29项身份、消息与业务堆栈均与9月30日基线一致，见 `bms-failure-comparison-confirmation.json` |
| mania 导入相关，Release | 2 通过、1 既有失败；`mania-import-final.trx`。唯一失败仍是 `TestRegisterExternalDirectoryWithOnlyNonManiaBeatmapsReturnsNull` 的旧 null 预期与实际 InvalidDataException 不符，身份、消息、堆栈与9月30日记录一致 |
| Desktop Release 构建 | 最终 `desktop-release-final.log` 为 0 error / 0 warning；最后 BMS 测试编译为 0 error / 2 个既有 warning（CS8600、CA2007），未屏蔽 |

core 的 23 个旧失败由 7 个难度分组、11 个旧选择失效 fixture、2 个随机数据排序及 3 个工具栏用例构成。对照仅将 MainMenu/SongSelect/BeatmapCarousel 三个生产文件或 Toolbar 单文件替换为记录的 HEAD 后执行，并恢复原字节；不是整个旧 checkout 的完整基线。最终 `core-failure-comparison-reviewed.json` 保留原消息和堆栈：21 项消息相同；2 项排序仅随机 GUID、随机 fixture 项数不同，失败索引/断言与业务堆栈相同。7 个难度分组用例因新 TestCase 多两行，文件位置按已核实的 +2 位移对应，未移除全部行号或掩盖其它业务帧变化。新增空分组目标在旧生产路径确实失败，恢复改动后通过；NORMAL 打开红测另保留首次呈现根组重置的证据。

整套收尾第一轮 `bms-full-final.trx` 为 2496 通过、30 失败、16 跳过，多出 `TestExternalFolderWorkspaceExplicitSelectionIsFreshAndImplicitSelectorsExcludeIt` 的“wait for external before next”超时。该测试直接创建 SkinManager，不经过新增下载/MainMenu/SongSelect/Carousel；皮肤生产实现及该 fixture 本轮均未修改。测试只等随机请求发出，未等实际提交便重选仍是当前值的外部皮肤，既有 same-ID no-op 合同可能吞掉重选，随后随机提交导致等待失败。同一 Release 产物单独复查五次均通过，再次完整执行也通过该项，回到上表29项旧失败；原始失败与复查均保留。这不等于已维护该测试。后续维护应等待隐式选择提交后再继续，不改产品同值选择合同、不加固定 sleep。

所有新 shell 的 restore/build/test/format/检查均先执行 `. .\UseDevelopmentStorage.ps1`；日志与 TRX 在 `artifacts/bms-download-20261001/`。先 restore 迁移缓存，formatter 后重新编译；共享构建和测试串行，所有产物在当前 F 盘 checkout。

有效门使用 `dotnet build osu.Game.Tests/osu.Game.Tests.csproj -c Debug --no-restore` 后运行 core focused；`dotnet build osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj -c Release --no-restore` 后运行 BMS focused/full。mania 同样先 Release 编译，再按 `FullyQualifiedName~ManiaImportIntegrationTest` 执行。Desktop 使用 `dotnet build osu.Desktop.slnf -c Release --no-restore -p:GenerateFullPaths=true -m`。`--no-build` 只使用对应配置已成功编译的当前产物；检查详情、filter 与失败对照保留于日志/TRX。

## 桌面画面与尚未签收的范围

两个来源最终真实桌面运行均 ExitCode 0，见 `render-probe/live-final-results.json`；实际下载按钮建立任务、关闭页后台完成、重新打开卡片并点击“在选歌中打开”，均在过滤/选择延迟结束后保持目标原 MD5 和实际持久化 GUID。真实画面为 `live-ui/Ginger-browser.png`、`Ginger-completed.png`、`Ginger-song-select.png` 及对应 `Konmai-*`；`Konmai` 是探针中的 616 来源枚举名。

中文桌面 fixture 的最后完整15例运行 ExitCode 0，见 `render-probe/fixture-final-result.json`；实际玩家改选点击 panel，不用绕过选歌队列的全局值赋值冒充用户输入。之后仅追加等待层修正/第16例，由上方18项 focused 与最终两源窗口再次覆盖，不虚称完整16例桌面 fixture 又执行过。较早失败的组合 `final-results.json` 不是最终通过证据。

`bms-browser.png`、`bms-browser-downloading.png`、`bms-browser-completed.png` 是中文 UI 隔离 fixture，使用伪元数据及真实 ZIP/importer，只证明布局、文字和状态链。真实站点画面与摘要另放 `live-ui/`，不得把 fixture 标为真实站点结果。桌面诊断由真实 DesktopGameHost、生产浏览 overlay、真实来源 HTTP 和实际 BMS importer 执行；窗口探针源码与日志保存在 `render-probe/`。

本轮没有重打发行包，也不重新签收皮肤外观、真实设备音频听感、长时游玩、整站可用性、超大归档、代理网络或外部站点未来服务水平。前述在线试听/整表/续传未实现。既有 Skin V1、设备及发行人工门保持各 owning 子线的原状态。

## 文档、协作与临时存储收尾

开始时工作区干净，`master@24f621e`，fetch 成功，核对时领先 origin/master 3、落后 0；这是本轮开始基线，不能当作收尾时在线远端查询。新实现不新建分支或 PR，未授权 push。

同步了 owning 子线状态/计划/合同/日志、主线窄例外、路由、三语说明及诊断记忆。`CheckDocumentation.ps1` 最终通过：186 个 Markdown、1821 个相对链接、237 个本地 Markdown 锚点、121 个 memory wiki 链；`git diff --check` 通过。初次检查发现证据索引缺项及 PLAN 的会话词，已修正，原失败日志保留。新增两个64位值已核实是上方公开下载制品 checksum，保留通用隐私警告而不移除有效证据。收尾在当前 master 提交本轮改动，不推送。

原包、窗口图像、失败日志及隔离谱库保留作为验收证据，不整目录清理。临时源码与依赖缓存仍用于复现并留在 F 盘，未新建 C 盘 OMS 工作副本。研究用 lampghost checkout 的递归删除被自动批准审查拒绝，本轮保留该目录并已向用户说明；没有绕过工具拦截重试，也不宣称临时目录全部清空。
