# OMS IR 接入诊断

## 权威入口与证据时效

当前能力读 [P3-IR STATUS](../../doc_md/subline/P3-IR/DEVELOPMENT_STATUS.md)，行为读[客户端合同](../../doc_md/subline/P3-IR/TECHNICAL_CONSTRAINTS.md)，多来源执行从[计划](../../doc_md/subline/P3-IR/DEVELOPMENT_PLAN.md#多播放器与-lr2-历史榜实施)进入。各轮失败、报告身份和指标查[本线历史](../../doc_md/subline/P3-IR/CHANGELOG.md)；这里按故障保留原因和诊断方法，不维护中途待办或第二套进度。

- **文档、工具和运行来源不同**：运行 manifest 绑定实际已提交服务 / 网页、插件构建及相应源码下载；新文档或外置探针 HEAD 不更新 runtime。原版 Laravel / Blade / React 官网与早期静态参考站分开，实际部署与失败取[正式记录](../../../websites/oms-web/doc_md/production-deployment-20261007.md)，操作取[维护说明](../../../websites/oms-web/doc_md/production-maintenance.md)。
- **运行清单不等于源码 offer**：release.json 绑定运行字节；AGPL offer 绑定公开构建源码 / 依赖 / 许可，HTML 或 source-only 清单不能替代运行 format3。固定备份 helper 和当时 HTTP current 分别绑定；旧源码同库回退时 sidecar 仍记录真实 current，不把新 helper 冒称旧运行源码。历史 sidecar 恢复保留原完整 release 绑定，同一投影兼容新 manifest 不重写旧身份；源码回退保留 live，不覆盖当前库。
- **成功分项不补签失败整轮**：软件、实际服务 / 资源、恢复、浏览器和真人分别留来源；completed=true 仍可能 pass=false。旧失败原件不改，后续实际成功另记，不通过重停服务、重放请求或延长时限制造旧门的证明。

## 成绩身份与最终保存

- 旧 Player.prepareAndImportScoreAsync 中 SubmittingPlayer 网络准备早于 BMS 最终结果，直接接旧钩子会缺最终灯 / 血条；新 IR 消费最终保存成功并回写实际 UUID 的结果。
- 数字 CircleSize=9 不能区分 BMS / PMS；取可玩谱 KeymodeResolution。mania 转换 / dual stage 的实际 TotalColumns 也不总等于 CircleSize。
- BMS EMPTY POOR v6 起是 HitResult.Ok，非 combo_break；最大 EX 取 maximum_statistics，不能整字典求和当物件数。final_gauge 为 0..1，原生 clear_lamp 为整数，邻近 StringEnumConverter 不能外推。
- 总分版本 30000016 也来自历史回放导入 / 重算，不证明新局；首接入不遍扫历史上传。converter 可复制丢 hash 的可玩 BeatmapInfo，原谱身份应取 loader 已验证来源，实际列数 / 键型仍取可玩谱。
- BMS Normal 完整结束却未清条时，本地 Passed 仍 true；IR passed 按最终 clear_lamp>=2 投射，不修改本地“已完成”来迎合接口。
- mania 本人详情套 BMS 表会把 Ok 错叫 EMPTY POOR 并漏 Meh。核真实 ManiaRuleset + Capture/Create + ScoreDetails 六类计数，不能只比新数组；修展示不改保存 / 上传 payload。镜像 sid / bid 缺 chart MD5 时不能凭同名猜关联。

## 凭据、界面与持久待交

- 原可切换服务版本中只清榜单行而保留MD5 / condition / group，会由新origin构造旧服务范围的有效网页链接。诊断须同时检查当前视图 / 范围和网页按钮；地址变化清完整范围并回目录，异步状态处理前再核origin。实际故障与两玩法行为证据见[2026-10-10接线日志](../../doc_md/subline/P3-IR/CHANGELOG.md#2026-10-10客户端与网站入口恢复)。
- 原账号 UI 消费独立 OmsIrService，不把服务身份塞入旧 APIUser / IAPIProvider。await 登录后、写凭据 / State 前复核当前操作取消；窗口关闭与 revision / Owner 变化是真实边界，迟到响应不能恢复账号或本人记录。
- 旧保存连接谓词只适用于原地址输入界面；固定 OMSIR 后直接登录 / 注册 / 密码 OnCommit 走同一账号请求，开窗无凭据请求。旧 settings 文件保留但不消费，官方 origin 凭据目标的保存根 / hash 格式保持；外站凭据和待交不得重新归属。CancelAccountOperation 可从 AsyncDisposalQueue 调用；TextFlow 刷新须 Schedule 并跳过已 Dispose，释放线程直接 Clear 曾导致真实失败。
- 隐藏覆盖层的账号通知可能停留在 Scheduler；重新开榜时若先启动请求再处理旧通知，会取消新榜。ShowChartBoard 在新读取前应用当前账号，queued 通知随后不再改变 owner；用隐藏时退出、立即重开并加载公开榜的行为验证，不能只断言 Visible。实际修复回链[本轮日志](../../doc_md/subline/P3-IR/CHANGELOG.md#2026-10-10固定omsir登录与谱面榜入口简化)。
- Visual TestScene 的 SetUp 用 AddStep 延后换实例时，`AddStep(..., overlay.Hide)` 注册时已捕获旧实例；用 lambda 执行时读当前字段。原 Visible→Hidden、HTTP 取消与迟到不出现断言保留，隐藏窗口的测试操作不代签真人关闭。
- InvalidDataException 不继承 IOException；异常 pending 恢复须显式处理、停上传并保全原件；退休 settings 不再参与恢复。响应体 IOException 是网络丢失，沿原 UUID 重试。401 时先使内存身份失效、标记需要登录，再尝试删除 Windows 凭据；删除失败也不能继续重发。本地排队写入不等账号 HTTP 锁。
- 原目录搜索的 `new Bindable<string>()` 默认为 null，query.Length 可在 HTTP 前失败，finally 仍恢复按钮而画面留 loading；只有 read-start、无第二请求时先查业务不变量，初始化 string.Empty，不靠 catch / fallback 掩错。独立目录交互已退休，此条只留原故障原因。
- 换来源后的旧 condition / 页码与迟到结果须沿当前合同清理；空 sources、原 ID / unknown / lamp 保真。网站公开最佳与端内本人 UUID 全历史的边界读合同，不因简化文案改变记录语义。

## 多来源取证地雷

正式记录 / 来源规则取[查询合同](../../doc_md/subline/P3-IR/TECHNICAL_CONSTRAINTS.md#来源查询与记录展示)，以下只保留解析器和 SDK 的反直觉条件。

- archive PB 是 (MD5, 原玩家 ID) 最佳摘要，缺逐次时间 / SHA256 / 完整规则，不能填成 OMS v7 或自动注册旧身份。授权的 v3.db 路径曾实际是目录；先核目标类型，不扫描其它 private-data。
- privacy_level=full 隐藏个人统计，不等于单谱 PB 私密；缺 profile 也不证明私密。★FULLCOMBO、option、异常 / 停榜标记须按原站 / 解析器取证，不凭标签猜灯或删除整批公开成绩。
- Java IRScoreData / OpenLR2 IRScoreV1 无稳定局 ID；宿主会重复 new 对象，秒级日期或每次生成 UUID 不证明同局。最佳状态与逐局幂等分开；ED assist=0 / FAILED 也不证明无辅助或整曲完成。
- Java RankingData 用完整数组长度计人数、空 player 认本人，截 TopN / 首屏会错榜；OpenLR2 才有 TopX / 本人 / 总数。原生 int ID 须独立持久映射，不与 LR2 / OMS 混空间；RestoreCachedRank 不发 HTTP。
- GAS settings 枚举是整数而最终数据是字符串，默认 settings 为空；有效下限钳制不改原参数，真实 Create 保留 APIMods。JD 原 header rank 不等于覆盖后的有效 rank，接收 / 拒绝仍取合同。
- 灯不能只按 group hash 合并：OMS family 还区分最大 EX，消费服务 rule_label；外部 SDK 缺精确 TOTAL / gauge_history 时同 gauge 数字不证明同条件。历史 ★FULLCOMBO 保原标签，不换算 OMS Perfect。
- 最高分与独立灯载体可来自不同状态：external.update 只在原灯严格更高时换 lamp_json。同灯 EX0→EX61 时灯载体仍可保持旧局，较低分但更高灯才更新；验收工具不能误把这个合同判断成数据损坏。
- OpenLR2 固定 SDK 须同 MSVC19.44 / MT / Release / 架构；x86 cdecl 的 .def 直接列未修饰 GetMethodTable，手工别名可能双重修饰。非空软件 Host 变体只接自己的 EXE 指纹，R4 的 3ab 软件来源不能由后续 25d 正式重编改写；固定真实 EXE 的 STL / UI / 线程 / 断线与标准 LN 仍另验，longnote presence 不证明标准 LN 解析。来源与 build-record 从 STATUS 回链，不绕宿主指纹造真人证明。

## 构建与服务诊断

- Git 专用 `http.https://github.com/.proxy` 可覆盖普通 http.proxy=；按调用进程诊断 / 绕行，不永久改配置或关闭 TLS。Windows 系统代理也影响 .NET / NuGet，curl --noproxy 成功不证明 dotnet 可达；当次 HTTP_PROXY / HTTPS_PROXY / NO_PROXY 的绕行不写成永久方案，不靠换源或 ignore-failed-sources 掩盖故障。
- 服务开发曾用 F 盘 venv 的 SQLite 3.53.1 避开旧 runtime WAL reset 风险；发布时核实际版本。单 worker 既不等于一个数据库连接，也不等于单个同步工作；AnyIO 默认 40 槽曾形成 41 线程，RSS 小于预算且无 OOM / swap 仍可能挤占共享余量。
- ACL 子 shell 失败先核实际 shell：父 pwsh Core PSModulePath 被 WinPS5 继承可产生 CouldNotAutoloadMatchingModule；EncodedCommand 超时且空输出不证明 SDK / HTTP / ACL 业务失败。同一 Core EXE 的 plain Command 诊断成功仅证明 shell；真实 ACL 用明确 EXE / list argv，在新 owned 目录沿原截止和原三 ACE 验证，不改全局模块路径或放宽权限。免安装工具从原工具目录加载，复制入口仅作 hash；含中文无 BOM 脚本用 PS7。
- 子 shell 输出 JSON 时，UseDevelopmentStorage 的提示来自 success stream1，即使抑制 stream6 仍会污染解析；明确同时抑制1和6，错误保留。严格绑定工具源文件名却未同步时可在 dispatch 前拒绝，不算 SSH 或产品执行失败。
- frozen harness 的 properties() 只返回声明字段，可能没有 ExecStart / ControlPID；须显式 systemctl show 读取，不能索引不存在字段或把未读取当实际缺失，不为过门改冻结源。

## SQL、HTTP 与资源诊断

- 全历史 payload 排序和逐人全表点灯扫描曾放大成本；先取每人最佳 ID，再排名 / hydrate，灯走独立覆盖索引。覆盖索引暖读不证明首次整榜；整 tuple 递归可能只利用第一前缀，严格分拆最大 EX / 规则组 / 账号前缀 seek 才能跳过重复条件，条件目录也不能再扫全部旧局。
- logical_fingerprint 含逐表行 hash 和 schema_sha256，记录不变与结构不变要分开验。新增批准索引时只排除那一个精确差异再比完整结构；八→九列另验其它表 / 结构以及二次初始化 schema_version / rootpage，不用总行数代签。新索引升级须核写成本 / 完整来源 / 恢复并实测指定同库回退，失败回滚索引；旧严格来源可能拒绝新索引。
- 全榜排完再排本页、目录 COUNT / 页重复扫描可使 CPU 超预算。候选只物化一次，近端取页；页首并列用全榜更高人数修正，其余 offset+页内 RANK，末页反读仍按原 record_key；核首中末 / 跨页大并列 / 越界 / 全局本人，不以 SQL 速度代签 HTTP。
- 一条 MATERIALIZED SQL 曾让目录首屏先整理全部目录，比原分拆更慢。UNIQUE(md5) 目录可按互斥匹配成员计总数后 UNION 有序页，不能吞后来源不同标题的命中；混榜整数排序后按原记录 hydrate 仍须证明 record_key 等价。
- 玩家统计 GROUP BY 可让 SQLite 选 scores_history 回表；全 scores_reference 逐局 JOIN 会重复解 JSON。先筛真实 eligible_groups，再沿实际覆盖范围聚合。WITHOUT ROWID 的 NOT INDEXED 不证明主键扫描，索引存在 / 自动采用 / HTTP 过门是三个事实，以真实 EXPLAIN 和完整请求为准，不盲加 INDEXED BY。
- 首 HTTP 慢、后续暖读快时，read_bytes=0 或只读函数较快不解释 quota 写 / commit / close；先取完整 HTTP 分段。线程槽排队、execute 墙钟或旧累计 memory.max 事件不直接证明 SQLite 锁 / 本次回收；同窗口增量与实际进程要齐。
- player_position 返回 rank / total_players 字典，不能与 tuple 比或用 *dict 取值。请求 401 时 full_board_all_pages_300ms 项失败不证明延迟超300ms；断言前留逐请求状态 / 时长，使用当前有效身份，不复活已撤销桌面会话。十次最近秩 p95=max，首请求不删除，也不加暖读稀释。
- driver 与服务分别约束：JSON 发起 / 校验不放同一生产 scope；采真正 MainPID / cgroup，而非包装进程。容量计时包含调度至确认，不剔除429或排队延迟。driver MemoryHigh 自身会导致回收；完整原生 JSON 图跨来源未释放也会挤共享余量，可在逐行校验后释放未消费对象，保留完整收包 / gzip / 名次 / 原时限。object_hook 释放 identity / lamp / conditions / native 图时仍保留原 ID / is_me / EX，顶层和灯汇总不改。
- 短峰已失败先完整落资源证据，不再跑无效持续门；真实 kernel / loaded peak 超上限约20KiB仍是失败，不能减页缓存、换 RSS 或给容差。主 / 维护 / driver 预算不同，来源相同的后续新观察仍须沿原 HTTP / 时长 / CPU / swap 门实测。
- 每次成功 HTTP 整份重写报告会增加 driver CPU / 回收干扰；成功事件独占逐行写、失败完整保全。采样首末跨度、外包围墙钟和末条 HTTP 时长分别登记；独立函数较快不定位恢复首请求的真实超时。

## 原生整榜网络诊断

- 固定 Java SDK 首次 pending.get(10s) 失败且 JSON / DTO 尚未执行时，服务器本地数组或独立 HTTP 成功不能代签 SDK 首次。分层记录头 / 完整 body / 解析 / 转换 / 实际协议，标准 JDK subscriber 对照保留原对象，不据 HTTP2 偏好、request(1) 或错误文字猜流控 / 解码。
- WinHTTP 自动解压曾接受坏 gzip CRC，完整行仍与 identity 相同；字段等价不证明完整性。固定官方 zlib 静态校验完整结束和尾部，网络 / 解压分别计限且保持原总截止；源码 / 许可 / hash 随实际构建发布，不借宿主已有 DLL。Java slow 失败未复现时留原异常类型，不盲改成“超时”或放松原因断言。
- 正式完整门不截榜或延长截止；对象释放的工具修订、软件结果与主机资源分别绑定。跨仓 pytest --basetemp 使用所属存储入口创建的绝对 F 路径，相对父目录不存在的 setup errors 不算有效 gate。

## systemd 身份、采样与终态

- oneshot 新 InvocationID / ExecMainPID 可先于 Bash argv / cwd 就绪。有限保存所有候选，只有同 invocation / PID / starttime 且真实属于 cgroup 的 MainPID 或 ControlPID 才登记；旧 invocation 原帧不能补新观察，ControlPID0 与 MainPID0 含义不同。子身份等待计入从 Popen 前开始的总截止，迟到匹配也失败。
- argv / cwd / cgroup 合取失败先保全预期和实际分项，缺分项不能归因竞态 / 耗尽。Nginx 启动后合法改为 master proctitle 时，同 PID / starttime / cwd 仍可稳定；不能把 argv 变化直接当 PID 重用。父观察覆盖子初始化、完整投影 hash 和 wait，子报告成功不证明父资源或真实退出，生命周期仍归父。
- backup observer 在 sampling.join 后从关闭的完整原流按最终 invocation / PID / 可用 cgroup 统计；在线 compact 可漏掉先落盘后登记的帧。收取端核完整 SHA / 数量 / 非零同 PID 存活帧与资源边界，不能仅删计数断言。短 prune PID 漏采保持 observer false；实际备份成功和精确完整对另验，不造未采帧。
- oneshot 关闭可移除 cgroup，使 MemoryPeak=[not set]；backup 终态峰实际不可得时记 null，观察峰不冒称生命周期峰，observer 自身仍核实际终态。ControlGroup 空时不能拼全局 /sys/fs/cgroup 取根峰值；终态 kernel 不可得如实记，末次 live 不回填。
- disable --now / stop 可卸载已退出单元，ExecMainPID0 与峰缺失不证明资源通过。先保留 loaded / exited 和原 PID；uvicorn 自有 SIGTERM 可真实 Code2 / Status15 / Resultsuccess，但配同 PID 顺序关闭日志，维护 worker 仍要求 Code1 / Status0。typed 终态只比实际共有字段，18字段包装不必与21字段原件同形，PID / unit / 来源 / 预算不得漏。
- --collect 会丢终态，RemainAfterExit=yes 配 --wait 会挂住；用独占输出、非阻塞启动、实际 PID / starttime 握手和 loaded 终态。已成功退出 Type=exec / RemainAfterExit 单元超过 RuntimeMaxSec 未必失败，父 RuntimeMax 也不约束独立 systemd 子 unit；各 worker 独立有限，失败只收尾绑定的自有角色。
- journal 秒级 --until 会漏同秒亚秒 shutdown；先保留实际退出与 PIDgone，再取包含边界的日志。stop 后工具失败按真实停点只读补核，不重停或用旧快照覆盖。新空恢复目录验收前不能写执行日志，误写与拒绝证据保留。

## 备份、恢复与证据文件

- gzip 大小预测不等于 SQL 一致备份或空恢复；恢复 raw 整件字节相等可继承完整 F 指纹，仍如实说明没有主机独立 SQLite 逐表重读。报告按 setup.evidence_directory 实际子目录找，准确 gzip / sidecar 从该 invocation 的完整 stdout 发布行识别，不找 latest。
- worker 失败且早报告无 pair 时，后来真正发布的完整对仍须保全，原 flags 不改。暂停 timer 并等原 worker 关闭后操作，超时只停自己的 worker；真实 refresh 轮转后旧 token 不复用，新的独立0600凭据原子保存，恢复核实际首页就绪和当前有效身份。
- 恢复盘账包含最大已获证 gzip / sidecar、实际或批准最大 raw / WAL、八对及系统2GiB，daily snapshot 也取 MAX。闲置合成恢复库造成旧门失败时，先完整 F 外取 / 解压核 SHA 与逻辑证据，再按身份定点退役可重建 raw / WAL / SHM；按实际存在对象保全，不能把三 raw 工具套在只有两 raw 的失败轮，或把 seed.archive 信息字典当 raw 路径。
- checkpoint 前核 main / WAL / SHM 的实际存在集合并逐件 SHA，`if is_file()` 后仅 all() 会漏缺失侧件。完整封存已提交 WAL 和组合逻辑后显式 close，必要 checkpoint 后核原组合逻辑等价；零 WAL 的残留 SHM 不等于未提交状态，不为它自动写库或删侧件。采样 WAL 最后连接关闭时可能在 exists / stat 间消失，一次 stat 捕获真实 FileNotFoundError，防采样线程中止后残余样本冒充完整门。
- SQLite Connection 的 with 只结束事务，不自动 close；删除临时恢复库或核 fuser 前显式 close / contextlib.closing。WinError32 留源库与压缩证据，不删整目录；远端 fuser 不支持 -- 时，选项失败不表示仍占用。
- mode=ro 读 WAL 库仍可能创建空 WAL / 锁 SHM。只有固定自有副本的所有角色关闭、fuser 空闲、非空 WAL / journal 已拒绝后才可 immutable 读，并核所有物理侧件前后恒等；不用于活动生产或母库。F 执行来源不等于主机参考副本执行，完整 report / log / events 的 inode / ctime 证据也不能被后续 chmod 改写。
- Windows 默认文本 fd 的 LF→CRLF 可使按 UTF8 字节数 ftruncate 的 JSON 尾部截坏；精确字节证明用 O_BINARY。CLI exit0 不是有效 receipt，完整解压核两快照并独立解析 JSON / producer hash 后才确认清理；旧 invalid receipt 保全。
- Windows Python3.12 Path.stat 的 ctime 可为 birthtime，os.fstat 可报 change time；跨 API 只比 dev / ino / size / mtime / nlink / 明确 birthtime，同 API 前后仍核完整 ctime / 属性 / SHA。只读可改变 atime，保留实值，不据此断言源被写或替改证据；Linux O_NOATIME 也沿真实字段核。
- 远端身份序列化用 POSIX 字符串 / as_posix()，Windows Path('/opt/...') 可能产反斜线；Linux 解析 F 路径用 PureWindowsPath，真实 Linux 文件用 Path，/proc 子路径 PID 转 str。CPUQuota 的 1.500000s 可等价150%；索引 DDL 换行仅作 SQL 空白正规化，完整 schema 指纹保原字节。

## 玩家网站与发布诊断

- 私有 owner 的0077 umask 会让公开 cache 父目录不可遍历；父层明确0755，private 子目录仍0700。config / view cache 在 FPM 同一正式 /app 命名空间生成，不能用其它目录生成的成功结果代签实际运行。
- Nginx reload exit0 后旧 worker 仍可接请求；宝塔 init reload 后有界核真实 HTTPS，保留每次尝试。停主 IR 属发布变动，自动恢复先停候选 IR / PHP 再作兼容守卫；旧 schema 全保留且只容许本次批准的两普通目录索引，不泛化为任意版本回退。
- 原 Blade 静态页无 json-oms-page，账号 PHP 空 data 可以是[]；核真实 controller / action / section、DOM / 正文 / 导航 / 资源，不给产品填假 JSON。首页 href 空 root path 与 / 等价，其它 path / query / fragment 保留；真实502和后来200分开留日期，不借重试 / 替代源冒充原成功。
- TarInfo 默认 mtime=0，而实际 BT Nginx 静态 ETag 依赖 mtime / 长度；内容变、长度不变可错304，no-cache 仍旧显示。HTTP发布条目用实际 created_at 秒，内部固定源码归档时间另算，内容 SHA 独立绑定。no-store 比 no-cache 更严，检查器不能因此误报。
- GET profile / 来源会改 rate_limits；同库回退核账号 / UUID / 会话 / 撤销 / 社区 / schema，单列真实配额变化，不谎报所有表相同或放宽限额。来源 / 输入 / 规模未变时不反复操作来刷新旧证据。
- fullPage / viewport override 可与 native 画面不同；AX / DOM / reload 超时而截图可用时只签截图，HTTP字节和旧截图不代签普通点击 / 刷新。
- journal255.4 rotate / flush 可自动 vacuum，--sync --namespace=* 不证明各 namespace 已同步，逐一 literal sync。已授权保全时用 ready 的独立 CONT 看门覆盖短暂停写，closed 硬链接 / active 复制后恢复原 PID，再全件 journal verify；BrokenPipeError 不猜 SSH idle。同一固定 cut 以 keepalive / 有界 metadata 的 foreground ssh -n cat FIFO 完整外取，核 SHA / size / EOFCRC 才解准确 pins，净增空间不代签生产资源。
- 撤销临时观察配置前逐件核 inode / SHA / effective 值，只撤自有文件；原 Accounting=yes 不因删冗余属性消失，新维护 pin 保留。timer 仅在空间 / 固定来源门后恢复原状态，核 enabled / active / waiting 和实际 NextElapse，不写死每日触发秒数。
