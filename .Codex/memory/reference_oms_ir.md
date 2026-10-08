# OMS IR 接入诊断

## 权威入口与证据时效

当前能力读 [[../../doc_md/subline/P3-IR/DEVELOPMENT_STATUS.md]]，客户端合同读 [[../../doc_md/subline/P3-IR/TECHNICAL_CONSTRAINTS.md]]，多来源正式执行从 [[../../doc_md/subline/P3-IR/DEVELOPMENT_PLAN.md#多播放器与-lr2-历史榜实施]] 进入。当前采用源码绑定取外部 Client Bridge；旧 `63f50c7` 是早期发布来源，`a9928fe` 是字段 / 生命周期取证，均不能当当前 HEAD。这里保留排错线索，不定义阶段或替代正式合同。

- 2026-10-08 子验证 argv / cwd / cgroup 的合取失败须先保全预期与实际字段，没记录分项不能归因于启动竞态或资源耗尽。有界身份等待仍须精确原来源 / 同 PID 与 starttime / 同 cgroup / 存活，并计入从 Popen 前开始的原总时限；迟到匹配也失败。父资源观察覆盖子初始化、完整投影哈希与 wait，子报告成功不代签父资源或真实退出；所有后端生命周期保持父所有权。实际官网迁移状态只取 `F:\zdamexy-workspace\websites\oms-web\doc_md\production-deployment-20261007.md`，不从新工具或文档 HEAD 推导线上来源。

- 2026-10-05 原账号 / 实际 osu-web 发布：运行 manifest 绑定已提交源码、插件实际构建及对应源码下载，后续文档 HEAD 不能冒充 runtime。读取 profile / 来源本身会更新 SQLite rate_limits；回退指纹应完整核对玩家 / 会话 / 撤销 / 社区与 schema，单独记录该真实限流命中，不能谎报所有表全等或放宽限额。门户 no-cache 与 IR 既有 no-store 都要求重新请求，校验脚本不能因更严格 no-store 误报。浏览器 fullPage 导出 / viewport override 与实际 native 画面不一致时保留失败并明确窄屏待复核；公网 AX/DOM/reload 超时而截图可用，只签实际截图，不能以 HTTP 字节或旧截图代签普通刷新和点击往返。

- 2026-10-07 已部署站的收尾必须区别实际formal备份和观察器：短prune子PID漏采时原observer false不改；保留actual loaded Exit0 / 正峰 / PIDgone与原件，独立F full CRC / raw / 22表验证，只签这份完整对，不造未观察PID。早先同一固定helper backup/prune真实观察另留来源。源码新→旧→新用同一live，配额因GET可变，原账号UUID和全部非配额表逐表核对。
- `systemctl disable --now`可能回收inactive单元终态，停止catalog后先收loaded / 原PID / 缓存峰再disable；卸载后不能以ExecMainPID0 / 未设置峰改签正常资源门。`Path('/proc') / PID`必须str；最后收取失败仅只读补核原关闭角色 / 原件，不重复源码切换和API来制造新证明。
- journal的秒级`--until`会截掉同秒亚秒shutdown日志；先保留实际Code2 / Status15 / Resultsuccess和PIDgone，再捕获包含关闭边界的日志。工具误判在stop之后失败时按实际停点续接，不重停 / 不拿旧快照覆盖；本次两次停服影响和原失败见P3-IR日志。
- 临时观测文件逐件核完整inode / SHA和原effective设置；只撤销任务自己的90-*及本轮50-MemoryAccounting。默认Accounting本已yes时删除冗余runtime属性仍核实际yes，新维护pin保持。恢复原timer只在空间和固定源码门后，核enabled / active / waiting及实际NextElapse，不写死每日触发时刻。8对预算同时包含最大已获证gzip / sidecar、实际或批准最大raw / WAL及系统2GiB；现成daily snapshot大小也纳入MAX。

- 2026-10-07 页面发布的正式oneshot结束后可能移除cgroup，MemoryPeak真实为`[not set]`；int转换失败的r6原件保留且未切换。以`start --no-block`采实际同一正starttimestamp / ExecMainPID的100ms运行帧，另核硬Max128 / swap0 / CPU50与终态成功、PIDgone；缺终态峰记null，观察峰不冒充全生命周期峰。timer暂停 / 等原worker结束后再操作，超时只停止自己的worker，所有收尾错误分别留证；新前端sidecar恢复保留旧完整release绑定，以同一投影兼容新manifest，源码回退不覆盖live。

## 成绩身份与最终保存

- 旧 Player.prepareAndImportScoreAsync 中 SubmittingPlayer 的网络准备早于 BMS ruleset 最终结果准备。直接接旧入口会缺最终灯 / 血条；新 IR 消费最终保存成功并回写实际 UUID 的结果。
- 9K 的数字 CircleSize 不能判断 BMS / PMS；必须在可玩谱持有 KeymodeResolution 时捕捉。mania 转换 / dual stage 也使实际 TotalColumns 不总等于 CircleSize。
- BMS EMPTY POOR v6 起是 HitResult.Ok (`ok`)，不是 combo_break；最大 EX 来自 maximum_statistics，不能把整张字典求和当物件数。final_gauge 是 0..1，clear_lamp 原生是整数，邻近字段的 StringEnumConverter 不能外推到它。
- 总分版本 `30000016` 也可能来自历史回放导入 / 重算，不能用它证明新局。首接入不自动遍扫历史上传。
- BMS converter 可复制新的可玩 BeatmapInfo 且丢失 hash，交分上下文取 loader 已验证的原谱身份；实际列数 / 键型仍取可玩谱。
- BMS Normal 完整结束但未达到清条线时，本地 Passed 仍 true；IR passed 按最终 clear_lamp >=2 投射，不能修改本地“已完成”来迎合接口。

## 凭据与持久待交

- 原 lazer 的账号 UI 可直接消费独立 OmsIrService，不把服务身份塞入旧 APIUser / IAPIProvider。登录响应 await 后、写凭据和 State 前必须对当前操作取消复核；关闭窗口取消与 revision / Owner 变化是实际边界，迟到响应不能恢复账号或本人记录。
- Visual TestScene 的 SetUp 用 AddStep 延后替换字段实例时，`AddStep(..., overlay.Hide)` 会在注册时捕获旧实例；执行时须用 lambda 读取当前字段。2026-10-05 关闭登录场景 r4～r6 的失败因此定位，r7 在当前窗口 Visible→Hidden、HTTP 取消与迟到不出现的原断言全部通过。不能直接削弱安全断言或把隐藏窗口的操作当真人关闭门。

- InvalidDataException 不是 IOException 子类，异常 settings / pending 恢复须显式处理并停 IR / 保全原件；响应体读取 IOException 是网络丢失，仍以原 UUID 重试。
- 401 清除凭据失败不能保留内存会话继续重发：先使内存身份失效、标记需要登录，再尝试 Windows 删除。排队本地写入也不能等待正在登录的 HTTP 锁。

## 构建与服务诊断

- Git URL 专用 `http.https://github.com/.proxy` 覆盖普通 `http.proxy=`。2026-10-03 用每命令清空该项、schannel / HTTP1.1 成功绕行，不关闭 TLS 或永久改配置；下次先诊断当时网络，不把旧绕行写成永远有效。
- .NET / NuGet 会读取 Windows 系统代理，curl --noproxy 正常不代表 dotnet 可达。2026-10-03 对调用进程显式设置 HTTP_PROXY / HTTPS_PROXY 加官方域名 NO_PROXY 后绕行成功、官方完整包恢复；不能靠改源或反复 ignore-failed-sources 掩盖 TLS 故障，不改全局代理或跳过签名。
- 本机常规 Python 的旧 SQLite 存在 WAL reset 风险；2026-10-03 服务开发验证用外部项目 F 盘 venv 的 SQLite 3.53.1，实际 runtime 版本须在所属发布门核对，单 worker 不等于只有一个数据库连接。
- 十万局榜单避免全历史 payload 排序和按人逐次全表点灯扫描；正式 SQL 先取每人最佳 ID，再排名读 payload，灯走独立覆盖索引。容量探针计入调度到确认，不能把 429 或排队延迟剔除。
- 索引升级的全表记录不变与结构不变分开验：`logical_fingerprint` 同时含逐表行hash与 `schema_sha256`。2026-10-06 r6 因合法新增一个索引的结构hash变化误判，保留原失败；r7复核逐表相同并从结构中仅移除精确批准索引后与原结构相等。八→九列在真实 r4 合成库另验所有表与其他结构不变、再次初始化的 schema_version / rootpage 不变，不能跳过全部结构比较或用总行数代签。
- 全量主机探针和服务不可共用生产MemoryMax口径：2026-10-04 r1把JSON发起/校验与服务放同scope，混读超时且未到30分钟。后续用独立systemd服务约束实际server，采集MainPID/cgroup而非systemd-run包装进程；失败证据取外部Backend全量主机报告，不从暖SQL推导HTTP通过。WAL最后连接关闭会在exists/stat之间消失，只做一次stat并捕获真实FileNotFoundError，防采样线程中止后残余样本冒充资源门。
- 覆盖索引暖读不证明首次整榜：2026-10-04 r4暖页通过，但100k旧局的完整覆盖扫描仍约780ms。整tuple递归也未跳过重复条件，实际计划只用第一前缀范围；分拆最大EX、规则组、账号的严格前缀seek才把同50个完整候选降到约30ms。条件目录同样不能再扫描所有旧局；以实际HTTP/完整候选等价和新回归复核，不新增被同名索引忽略的“优化”。
- OpenLR2固定SDK须同MSVC19.44/MT/Release/架构；x86 cdecl的`.def`直接列未修饰GetMethodTable，手工别名可能双重修饰。F盘免安装工具入口必须从原工具目录加载，复制到构建目录的入口仅作hash证明；UTF8无BOM含中文构建脚本用当前PS7执行，PS5可能ANSI误读。独立检查程序须被固定宿主指纹拒绝，不绕过它伪造真实宿主HTTP/游玩证据。
- 同源码Open非空软件Host变体要单独绑定：2026-10-06 R4实际源码3ab、固定SDK及同MSVC /MT消费非空对象，但软件DLL只接受自己探针EXE的Host SHA。正式DLL /固定真实EXE的STL、UI、线程和断线仍待，不由后来25d正式重编译改写旧软件结果；SDK longnote只表presence，不签标准LN解析。真实汇总与软件build-record在F盘`oms-player-site-20261005/open-nonempty-native-gate/`，能力状态只取P3-IR。
- ACL子shell失败须先分清实际shell：2026-10-06 Open软件r1在app /账号创建前失败，`open-acl-diagnostic-r1.json`为WinPS5 `CouldNotAutoloadMatchingModule`；父pwsh的Core PSModulePath被子WinPS继承，不能据此说SDK /HTTP /产品失败。r2同一Core EXE以UTF16LE EncodedCommand执行ACL，30秒截止、stdout /stderr均零；保留真实timeout，未证明其具体根因。`open-acl-shell-diagnostic-r2.json`证明同一pwsh普通`-NoProfile -NonInteractive -Command`最短诊断EXIT0，诊断本身没运行ACL /app。后续使用明确Core EXE与Python list argv的plain Command，在新owned目录完成原保护ACL；Storage首行、原30秒截止、三ACE、shell /脚本SHA与原输出保留，不改全局PSModulePath、不放宽权限或猜成业务失败。
- 同灯的最高分观察不替换独立灯载体：原external.update仅在原灯严格更高时更新lamp_json。2026-10-06 Open软件r3在EX0/Failed1→EX61/Failed1后误期待灯载体也变成stage1，实际仍stage0；较低EX14/Hard4才更新灯载体。r4只修期望 `(best_stage,lamp_stage)=(1,0)`，不改C++ /业务或编译输入，保留原失败 /原stateID与两载体hash，不能把验收工具预期错误写成产品数据损坏。
- 单worker不等于单同步工作：r6默认AnyIO40槽形成41实际线程，首次/原生单读已通过，峰值后仍挤占共享余量并产生大量MemoryHigh事件；无OOM/swap、RSS低于500MiB不等于资源通过。按真实边界限制工作量，再用原峰值/持续速率复核，不以改小压测速率或暖SQL签收；短峰值已失败就先落资源证据并停止，不能再跑无效30分钟。
- 内存守门也不证明峰值可用：r7逐路径CPU乘实际速率已超主机1.5核；给全榜生成名次再排本页和目录COUNT/页两次扫描是重复工作。完整候选只物化一次，近端读页、页首EX按全榜更高人数修正并列，其余offset+页内RANK；末页反读仍按原record_key输出。须逐行核对首中末/跨页大并列/越界和全局本人，再实测原负载；SQL加速数字不代签HTTP。
- 一条SQL不一定更省：r8目录MATERIALIZED让首屏先整理全目录，实际HTTP文字搜索CPU由137升至207ms。各目录UNIQUE(md5)可按互斥的匹配成员计总数，再用原UNION有序页；此前来源不同标题未匹配时不能吞掉后续命中。混榜完整排序仅携带四个整数、页面/本人再hydrate原字段，种类加记录ID字典序必须等价原record_key；80排名/26目录暖SQL等价仍不代签原峰值。r8驱动排队仅2ms但HTTP约1秒，不能把线程槽排队或execute墙钟差值直接说成SQLite锁。
- 网络整榜先分层取证：r9服务器本地完整数组通过，固定Java SDK经SSH网络首次仍在`pending.get(10s)`失败，JSON/DTO转换尚未执行；独立HTTP成功和同源后续暖读不能代签首次SDK。标准JDK subscriber的对照要保留原对象，不用包装后冒充其内部TrustedSubscriber调度；记录头/完整body/解析/转换时间和实际协议，不由偏好HTTP2、request(1)或失败文字直接推断流控/解码问题。所有诊断不是正式真人门，不延长截止或截榜。
- 压缩整榜不能只检查JSON：r10实际WinHTTP自动解压接受坏CRC、29,204行仍与identity相同；编译/字段一致不能签完整性。按实际失败改固定官方zlib静态校验完整结束与尾部，网络/解压后各计限并沿原截止；依赖源码/许可/hash纳入干净编译与发布证明，不借宿主已有DLL猜版本。Java首次连续slow失败未复现时保留原日志，用实际异常类型核定，不能盲改成“超时”或放松原因断言。
- 恢复盘账须保留原失败：r9两闲置合成恢复库使原七日门false；先将完整gzip/sidecar及逻辑验证证据外取，解压复核SHA后才能精确定点清理可重建的合成DB/WAL/SHM，再另记七日/新空恢复/WAL/系统余量补账，不覆写原报告。Python sqlite3连接的`with`只管理事务，不自动close；ownership/fuser前须显式关闭自己的连接。本机远端fuser不支持`--`，非法选项不能当作文件仍被占用。
- Windows字节证明文件不能用默认文本fd：2026-10-05 r10外取工具用`os.open`/`os.write`后按UTF8字节数`ftruncate`，默认LF→CRLF展开使JSON结尾被截断，CLI显示通过也不构成可用receipt。写这种精确字节文件须用`O_BINARY`；保留原失败，重新完整解压核对两快照并独立解析完成JSON/核producer hash后，才向远端确认定点清理。F证据`offhost-r10-receipt-invalid.json`及`offhost-r10-verification-r2.json`不进Git。
- “真实环境验收”不授权客户端打包：2026-10-03 用户明确日常用 VS Code 非调试启动，发行构建自行执行。稳定约定在 AGENTS；不要由部署服务推导需要 ZIP / publish / 额外安装副本，既有发行门留到用户构建时验收。
- 首次未填搜索词时，`new Bindable<string>()` 默认 null；`query.Length` 在 HTTP 前失败，async finally 仍恢复按钮，界面可停在 loading。2026-10-04 trace 只有登录与 read-start、没有第二请求 / response-ready，滚动猜测不成立；应初始化业务不变量 `string.Empty`，不加 catch / fallback 掩错。临时诊断撤掉，原场景断言仍须通过。

## 多来源取证地雷

- 2026-10-05 本人详情消费 statistics 必须按真实 ruleset：mania Perfect / Great / Good / Ok / Meh / Miss 六类；BMS 原 Perfect / Great / Good / Miss / Ok 对应 EMPTY POOR。共用 BMS 表会把 mania Ok 错名并漏 Meh。真实 ManiaRuleset + Capture/Create + ScoreDetails 行为断言证明计数，不能仅比较新数组字面量；未改保存 / 上传 payload。网站当前公开最佳和游戏本人 UUID 全历史分开，mirror sid/bid 未提供 chart MD5 时不造同名关联。

以下取证地雷已进入正式审查 / 实施的排错路径；具体边界仍取正式合同。接口、合成导出、插件构建或旧十万局容量不能签收真实宿主 / 全量历史。

- archive 的 PB 是 (MD5, 原玩家 ID) 最佳摘要；缺逐次时间/SHA256/完整规则，不可塞成完整 OMS v7 或自动注册旧身份。用户给的 v3.db 路径实际是目录；只访问明确授权的目标，schema/汇总证据留 artifacts，不扫描其他 private-data。
- schema 的 privacy_level=full 是隐藏全部个人统计，不等于单谱 PB 私密；profile 缺失也不是私密证明。★FULLCOMBO、option、异常及停榜标记需要原站/解析器语义取证，不能由标签猜规范灯或删除整批公开成绩。
- Java IRScoreData 和 OpenLR2 IRScoreV1 没有稳定局 ID；宿主重复会 new 对象，秒级日期/每次生成 UUID 都不能证明同局。最佳状态幂等与逐局幂等分开；ED assist=0 和 FAILED 灯不能证明无辅助或整曲完成。
- Java RankingData 用全数组长度算人数、空 player 认本人，TopN/第一页会错榜；OpenLR2 才有 TopX/本人/总数。其原生 int ID 需持久唯一映射，不能直接与旧 LR2/OMS ID 混空间。RestoreCachedRank 不能发 HTTP。
- OMS Create 发送真实 APIMods，不能清空 Mod 伪装普通。真实规则 Mod 接收边界从正式合同取；GAS settings 的原枚举是整数，最终数据的枚举是字符串，默认 settings 为空，有效下限钳制不改原参数。JD 捕捉的原 header rank 尚不等于覆盖后的有效 rank，拒绝时保原待交。
- 灯规则不能只匹配 group hash：OMS family 还区分最大 EX，消费服务 `rule_label`；外部 SDK 缺精确 TOTAL / gauge_history 时相同 gauge 数字也不证明可合并。历史 ★FULLCOMBO 保原标签，不能换算为 OMS Perfect。
- 混榜错位先核对筛选位置：来源/公开资格/条件必须先约束最佳 EX、独立灯和参与身份，再计算人数、名次及分页。先取每源 TopN 再拼接，或沿 mandatory group 永远分开，均不满足自由参考混榜。

## 玩家网站与发布诊断

- 2026-10-08 原版 osu-web R14 已真实通过完整共享主机运行（最低可用内存 560.957 MiB，swap / OOM 零）；随后两实际空恢复、完整 F 保全及 81 窗口 / 29,724 原帧的八对 / 最大 raw / 额外 2 GiB 盘账也通过，原失败保持。正式 current 仍旧；固定正式备份成功，但首个 observer 启动身份误判失败，完整观察仍须补验。接续读 Web `doc_md/production-deployment-20261007.md`，不从新文档 HEAD 或运行分项推导部署 / P/C。停止 gzip 预测不是 SQL 一致备份或恢复门，字节继承证明如实记未执行本地 SQLite 重读。
- 2026-10-08 通用 journal 经明确用户授权才访问和清理：255.4 rotate / flush 自动 vacuum，`--sync --namespace=*` 不能证明两个 namespace 都已 sync，须逐一 literal sync。独立已 ready CONT 看门、短暂停写、closed44硬链接 + active2复制，恢复原 PID 后全部 journal verify；首轮 BrokenPipeError / F0B 必须保留失败，不能归因为未知 SSH idle。R2 用同一固定 cut 经有 keepalive / 有界 metadata 的 foreground `ssh -n cat FIFO` 全件导出 F，完整46件 / SHA / size / EOFCRC 后才解除准确 pins 和设置default480 + 既有OMS32 MiB，合计512 MiB。随后按原授权追加清理4+3件已关闭旧日志；最新3件执行源 `9e1bbc17…`、空间实测净增148500480 B。完整受保护 F 原件及两轮原帧保持，实际净增空间不代签两空恢复、source/cache/WAL峰值或生产。
- 冻结 harness 的 `properties()` 不包含 ExecStart，正式定义须独立 `systemctl show` 读取，勿访问不存在字段或修改冻结源。停止 main / WAL / SHM 的物理证明须先核存在集合，再逐件 SHA；`if is_file()` 后仅 `all()` 会吞掉缺失侧文件。checkpoint 前完整保全原组合和 22 表，SQLite 连接显式 close，再验证逻辑相等；预测文件不冒充正式 sidecar。

- 2026-10-08 原R14第二空恢复先撤销user0桌面会话，完整分页却仍用该token，实际返回401；不能据 `full_board_all_pages_300ms` 失败项名认定已证明超过300ms。工具 `133eb509` 改为显式传入有效身份，并在断言前保全逐请求HTTP；人数、全部页、本人行与原时限仍核验，原失败不改。
- 同日第二恢复补验r1 / r2分别因proctitle成员匹配、重复原sample名失败，F原件保留。r3的Nginx仍是同PID2868624、starttime / cwd相同，但启动后合法改成master title，`same_process` 因argv变化误拒，不能说PID被重用。99次正常续期（49桌面、50网页）已逐次原子写入独立0600凭据，旧refresh已旋转不可再用；后续r4须先实际首页200就绪，再构造BudgetUnit绑定稳定身份并复用最新凭据，不复活已撤销的user0桌面会话。
- 本次 `systemctl stop` 把已关闭的Nginx单位变为真实inactive / dead，并使ExecMainPID0、MemoryPeak `[not set]`；这与 `BudgetUnit.close` 对同PID发SIGTERM、保留loaded / exited终态的路径不同。保留原断言失败后独立读取已关闭现场，不再次stop，不补造原PID、原峰或旧成功。
- 固定备份observer须在sampling.join后，从已关闭完整原流按最终InvocationID / ExecMainPID / MainPID及真实可用cgroup逐行统计；新调用帧可先落盘、后登记身份，在线compact计数会漏帧。收取端独立核全流SHA / 数量、非零同PID存活帧及128 MiB / CPU50% / swap0 / OOM边界，不能只删计数对应断言。仅backup终态允许真实 `[not set]` 并记null，观察峰不称全生命周期峰；观察者自身仍核实际终态。本次 `c98a4917…` 备份观察工具及 `81c6e8b7…` 有限收尾工具已审接受但尚无实际门签收，不据此宣布r4补验、两恢复完整或部署。

- 完整原生榜 probe 的四次校验可在 JSON object_hook 逐行完整解析后释放未消费的 identity / lamp / conditions / native 图，保留原 ID / is_me / EX；顶层 / 灯汇总与默认解析不改，完整收包 / gzip / 全行 / 名次 / 字节 / 时限仍核验。2026-10-08 R13 父观察最低 510.145 MiB 的失败原件保留，新软件 24 项通过不代签主机。末次成功内容不能定位失败原因，须读 failures 与内外完整原帧。跨仓使用 Web 存储入口只创建 Web temp，pytest --basetemp 用其绝对 F 路径；Backend 相对父目录不存在的 setup errors 不算有效 gate。

- counts22 当前 `player_position` 返回完整 `rank` / `total_players` 字典；检查器不能与tuple比较或用`*dict`记录数值。2026-10-06 small R2因此真实失败，R3只修工具合同后通过全部逐人原始数学。服务正常关闭须主动SIGTERM own main并保留loaded单位，已退出后再`systemctl stop`会卸载transient；维护工具也受同一边界约束。全量R4的512MiB余量失败在guard前未落最小样本，不能以终态内存补填；重新实测先保存同窗口driver/main实际观察。关闭后的合成库仍可能有已提交WAL，先完整封存raw/WAL/SHM及组合逻辑指纹，再独立必要checkpoint；只读终态失败不改成成功。

- `tarfile.TarInfo` 默认mtime=0；实际BT Nginx的静态ETag依赖mtime和长度，内容变而长度不变会错误304，即使Cache-Control=no-cache也仍旧显示。2026-10-06实际独立探针复现并验证实际发布时间可修正；外层发布条目统一created_at秒，内容SHA仍独立绑定。内部源码归档固定时间不等于HTTP文件时间。
- 玩家统计先看真实EXPLAIN：GROUP BY可能使SQLite选scores_history回表，即使存在scores_lamp覆盖索引；全scores_reference再逐局JOIN还会反复计算条件JSON。请求内先筛真实eligible_groups，再沿实际覆盖范围聚合；新执行器/索引声明或小样本不能代签同规模p95。
- 测试driver和服务是不同进程：driver的MemoryHigh也会产生回收延迟；256MiB完整整榜解码执行器不能当作生产或维护预算，主服务500MiB、维护128MiB要分别实测。记录实际cgroup事件/peak，异常时保留旧失败再复核，不由推测消除时限失败。
- Windows上的sqlite3 Connection `with`只结束事务，不保证close；完整解压CRC/hash核验后删除F盘临时库须显式close或contextlib.closing。WinError32时保全源库和压缩证据，修正后从完整重新核验继续，不能跳过旧失败或删整目录。
- 增长报告每次HTTP成功整份重写会增加driver CPU / 回收干扰；2026-10-06恢复首次330.510ms仍失败，不能因只读函数约20ms就认定根因或删首请求。成功事件独占逐行记录，失败保全；十次最近秩p95=max，不用更多暖读稀释。实际采样首末跨度与外包围墙钟、原HTTP末条时长分开，分别记录来源。
- 失败保全按实际对象：上述r1只有主库 / restore1两raw，没有restore2；三raw pass-only工具不能套用。先完整F gzip EOF CRC / raw SHA / 全21表 / schema / sequence / FK核验，再按关闭PID、inode、字节定点退役，原false不改。zero WAL的残留SHM不是未提交状态，不为它自动写库或删除侧文件。
- r2十次首BMS454.771ms仍失败。独立只读分段中摘要26ms、排名约376ms；本人缓存摘要改法仍总387ms，不能只凭删raw枚举当修复。descriptor JSON不带source，读取实际scope列。原transient服务停止后可能LoadState=not-found / ExecMainPID0，结合原报告实际PID与/proc gone核对，不伪造终态PID；来源、函数实验与真正HTTP门分开。

- WITHOUT ROWID表的`NOT INDEXED`标签不能代签主键扫描；实际EQP为准。2026-10-06覆盖排名副本数学通过，正式唯一索引按精确完整旧/新结构升级；物理结构改变须重跑完整来源/写成本/恢复门，不能复用纯读取合同。只读backup保全旧schema，升级失败连索引回滚；25d/3ab严格拒绝新索引，指定b520回退仍须实测，新维护helper保持固定。seed.archive是公开信息字典，不是raw路径；保全失败原值保持，完整F核验后只定点退役实际raw。

- Windows `str(Path('/opt/...'))`会带反斜线；远端身份串行化用POSIX字符串或`as_posix()`。CPUQuota显示的精度依实际主机，真实150%可能为`1.500000s`，先取证再用等价数值核验；SQLite实际索引DDL可含换行，只做SQL空白正规化，完整schema指纹仍保留原字节。
- systemd `--collect`会丢真实终态，`RemainAfterExit=yes`与`--wait`会挂住；用独占文件、非阻塞启动、实际PID / starttime握手与loaded终态MemoryPeak。本轮已成功退出的Type=exec / RemainAfterExit=yes单位超过自身RuntimeMaxSec后仍保持success，原journal-budget / old-map长时观察已留证，不能概括它都会timeout。仍运行的staging / 前端及独立维护unit各按真实合同限时；父RuntimeMax不限制独立systemd子unit，维护worker须单独有限，失败时只收尾自己实际绑定的角色并保留真实终态。末次live内核峰不冒充终态，内外gzip与controller各自实测。

- 新索引存在、自动被用与HTTP预算是三个事实：2026-10-06 R3真实首HTTP622.113ms失败，后九次约29ms；六条件COUNT的后置EQP确用正式COVERING，独立函数24～38ms却read_bytes全0。系统缓存命中和不含quota写 / commit / close的函数诊断不解释首HTTP，不据此盲加INDEXED BY或删首请求；需独立合成副本的完整HTTP分段。driver累计memory.max历史事件已在前窗口存在，若本窗口不增不得归因本次尾延迟。

- 2026-10-06 counts22全量R6完成全部持续 / 恢复分项，独立driver实际内核和loaded峰值仍超Max256MiB 20,480B；原completed=true/pass=false完整14MB报告与终态均保全。上限是实际峰值门，不能减页缓存、换RSS、给容差或将成功分项升级整体。新观察进程可提前设置MemoryHigh以回收，MemoryMax、CPU、swap和原HTTP / 时长门保持，仍须真实新运行。
- 只读mode=ro的WAL库可能创建空WAL与锁SHM：本次三库保全审计R1因此真实Exit1。保留失败原件与侧文件；只有固定自有副本的全部角色关闭、fuser闲置、非空WAL / journal已拒绝后，才可保留COMMON immutable读取并证明所有物理侧文件首尾恒等，不用于活动生产库或母库。F上执行的starter仅有F真实来源，主机参考副本不等于主机执行；完整收取的report/log/events其inode / ctime等证据不应被后续chmod改写。
- Windows Python3.12的Path.stat把ctime报成birthtime，os.fstat可能报实际change time；跨API只比dev / ino / size / mtime / nlink / 明确birthtime，同API早晚继续核完整ctime / 属性和整件SHA，不把fd字段回填成原path证据。Windows只读本身可能改变atime，须保留真实before / after，不能由它断言源被写或替改证据；Linux显式O_NOATIME仍按实际字段核。Linux校验编译报告中的F路径用PureWindowsPath，实际Linux文件用Path；旧失败来源 / 输出保留。
- systemd关闭后的ControlGroup可能为空；不能拼出/sys/fs/cgroup把全局根峰值冒充该单位终态。实际loaded MemoryPeak独立保留，终态kernel不可用如实记录，末次live不代填。本次before实际报告通过而F终态观察因空group失败：原false原件保留，另用独立只读收取同一次已退出单位 / 实际PID / 原源码 / 全输入与输出，再签新观察；不重跑旧before或备份，不虚构timeout。新空目录被接受前不能写入执行日志，误写与拒绝证据保留。
- 发布的运行release.json（3370fbfbcb7d）与AGPL源码offer清单（c627b813eb36）用途不同：前者绑定实际部署字节和runtime，后者绑定公开构建源码 / 固定依赖 / 许可。HTML或source-only清单不能代替运行format3；后续文档HEAD也不更新运行来源。维护helper保持新counts22源码，但备份sidecar必须记录当时真实current来源，旧b520同库回退不把新helper伪装为旧运行版本。
- typed终态按实际共有字段逐键核对，保留两份完整叶子；18字段包装与21字段完整systemctl记录不要求整对象同形，也不能漏PID / unit / 来源 / 预算。先整件收F，再核同一单位实际loaded MainPID0 / ExecMainPID / Code / Status / Result / PIDgone / cached峰，内核可得性另记。正常uvicorn SIGTERM可为Code2 / Status15 / Resultsuccess，但须配实际同PID顺序关闭日志；维护worker仍要求真实Code1 / Status0，不伪造Exit0或以live替代终态。
- 维护报告用setup.evidence_directory的实际具体子目录定位：主机 production-backup-r7-ID/worker-report.json / observer-events.jsonl 与backup.stdout.log，不在证据根猜同名report；F签收在 production-post-r7-ID/backup-collection.json，完整核验在pair-verification.json。必须从本次完整stdout的准确发布行识别gzip / sidecar，不找latest；失败worker已有报告却没有pair时仍完整保全后来真实发布对，原flags不改。正式恢复用原对在两个新空目录整raw字节相等继承F全22表证明，不能说已独立主机逐表测量。

- 2026-10-07原登录表单的地址输入不等于已保存service origin；登录按钮与密码OnCommit都必须使用同一个已保存连接谓词。配置保存仅写本地，不发送凭据，异步完成不覆盖用户随后新输入。CancelAccountOperation可从AsyncDisposalQueue调用；新增TextFlow文字刷新必须Schedule且跳过已Dispose，不能在释放线程Clear子元素。初次两例真实线程失败保全，修复后原17例Release有效重编通过。来源切换须清旧condition回reference / page1，保留空sources，详情在原位置展开保留原ID / unknown / lamp，不用简化文案改记录语义。

- 2026-10-08 frozen g.properties 只返回 UNIT_PROPERTIES 声明的字段，不含 ControlPID；新增前置关闭核对须一次明确读取实际 ControlPID，不能把未读取当实际缺失或直接索引。补验r4因此在业务前真实KeyError / failed，F完整一流两帧保持；r5准备来源不代签运行或两恢复。

- 2026-10-08 fixed backup R4 的 protected ACL 子 shell 即使重定向 stream6，UseDevelopmentStorage 仍从 success stream1 输出提示，JSON 解析失败且尚未 SSH；明确同时抑制1和6，错误仍保留。R5严格源文件名绑定未同步而在 dispatch 前拒绝。R6新 InvocationID / ExecMainPID 出现时 bash argv / cwd 尚未通过，61 ms退出；七原帧均旧 invocation，不能充当新实例观察。实际 backup 本身随后成功，从该精确 invocation 完成行独立完整保全对至 F、核 gzip/raw/22表及真实当前 manifest，原 observer false 和正式观察门 false 保持；oneshot须有限等待真实身份、保存候选，不把 ControlPID 0 / MainPID 0 含义混用或用后来终态补帧。
