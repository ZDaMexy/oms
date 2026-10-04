# OMS IR 接入诊断

## 权威入口与证据时效

当前能力读 [[../../doc_md/subline/P3-IR/DEVELOPMENT_STATUS.md]]，客户端合同读 [[../../doc_md/subline/P3-IR/TECHNICAL_CONSTRAINTS.md]]，多来源正式执行从 [[../../doc_md/subline/P3-IR/DEVELOPMENT_PLAN.md#多播放器与-lr2-历史榜实施]] 进入。当前采用源码绑定取外部 Client Bridge；旧 `63f50c7` 是早期发布来源，`a9928fe` 是字段 / 生命周期取证，均不能当当前 HEAD。这里保留排错线索，不定义阶段或替代正式合同。

## 成绩身份与最终保存

- 旧 Player.prepareAndImportScoreAsync 中 SubmittingPlayer 的网络准备早于 BMS ruleset 最终结果准备。直接接旧入口会缺最终灯 / 血条；新 IR 消费最终保存成功并回写实际 UUID 的结果。
- 9K 的数字 CircleSize 不能判断 BMS / PMS；必须在可玩谱持有 KeymodeResolution 时捕捉。mania 转换 / dual stage 也使实际 TotalColumns 不总等于 CircleSize。
- BMS EMPTY POOR v6 起是 HitResult.Ok (`ok`)，不是 combo_break；最大 EX 来自 maximum_statistics，不能把整张字典求和当物件数。final_gauge 是 0..1，clear_lamp 原生是整数，邻近字段的 StringEnumConverter 不能外推到它。
- 总分版本 `30000016` 也可能来自历史回放导入 / 重算，不能用它证明新局。首接入不自动遍扫历史上传。
- BMS converter 可复制新的可玩 BeatmapInfo 且丢失 hash，交分上下文取 loader 已验证的原谱身份；实际列数 / 键型仍取可玩谱。
- BMS Normal 完整结束但未达到清条线时，本地 Passed 仍 true；IR passed 按最终 clear_lamp >=2 投射，不能修改本地“已完成”来迎合接口。

## 凭据与持久待交

- InvalidDataException 不是 IOException 子类，异常 settings / pending 恢复须显式处理并停 IR / 保全原件；响应体读取 IOException 是网络丢失，仍以原 UUID 重试。
- 401 清除凭据失败不能保留内存会话继续重发：先使内存身份失效、标记需要登录，再尝试 Windows 删除。排队本地写入也不能等待正在登录的 HTTP 锁。

## 构建与服务诊断

- Git URL 专用 `http.https://github.com/.proxy` 覆盖普通 `http.proxy=`。2026-10-03 用每命令清空该项、schannel / HTTP1.1 成功绕行，不关闭 TLS 或永久改配置；下次先诊断当时网络，不把旧绕行写成永远有效。
- .NET / NuGet 会读取 Windows 系统代理，curl --noproxy 正常不代表 dotnet 可达。2026-10-03 对调用进程显式设置 HTTP_PROXY / HTTPS_PROXY 加官方域名 NO_PROXY 后绕行成功、官方完整包恢复；不能靠改源或反复 ignore-failed-sources 掩盖 TLS 故障，不改全局代理或跳过签名。
- 本机常规 Python 的旧 SQLite 存在 WAL reset 风险；2026-10-03 服务开发验证用外部项目 F 盘 venv 的 SQLite 3.53.1，实际 runtime 版本须在所属发布门核对，单 worker 不等于只有一个数据库连接。
- 十万局榜单避免全历史 payload 排序和按人逐次全表点灯扫描；正式 SQL 先取每人最佳 ID，再排名读 payload，灯走独立覆盖索引。容量探针计入调度到确认，不能把 429 或排队延迟剔除。
- 全量主机探针和服务不可共用生产MemoryMax口径：2026-10-04 r1把JSON发起/校验与服务放同scope，混读超时且未到30分钟。后续用独立systemd服务约束实际server，采集MainPID/cgroup而非systemd-run包装进程；失败证据取外部Backend全量主机报告，不从暖SQL推导HTTP通过。WAL最后连接关闭会在exists/stat之间消失，只做一次stat并捕获真实FileNotFoundError，防采样线程中止后残余样本冒充资源门。
- 覆盖索引暖读不证明首次整榜：2026-10-04 r4暖页通过，但100k旧局的完整覆盖扫描仍约780ms。整tuple递归也未跳过重复条件，实际计划只用第一前缀范围；分拆最大EX、规则组、账号的严格前缀seek才把同50个完整候选降到约30ms。条件目录同样不能再扫描所有旧局；以实际HTTP/完整候选等价和新回归复核，不新增被同名索引忽略的“优化”。
- OpenLR2固定SDK须同MSVC19.44/MT/Release/架构；x86 cdecl的`.def`直接列未修饰GetMethodTable，手工别名可能双重修饰。F盘免安装工具入口必须从原工具目录加载，复制到构建目录的入口仅作hash证明；UTF8无BOM含中文构建脚本用当前PS7执行，PS5可能ANSI误读。独立检查程序须被固定宿主指纹拒绝，不绕过它伪造真实宿主HTTP/游玩证据。
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

以下取证地雷已进入正式审查 / 实施的排错路径；具体边界仍取正式合同。接口、合成导出、插件构建或旧十万局容量不能签收真实宿主 / 全量历史。

- archive 的 PB 是 (MD5, 原玩家 ID) 最佳摘要；缺逐次时间/SHA256/完整规则，不可塞成完整 OMS v7 或自动注册旧身份。用户给的 v3.db 路径实际是目录；只访问明确授权的目标，schema/汇总证据留 artifacts，不扫描其他 private-data。
- schema 的 privacy_level=full 是隐藏全部个人统计，不等于单谱 PB 私密；profile 缺失也不是私密证明。★FULLCOMBO、option、异常及停榜标记需要原站/解析器语义取证，不能由标签猜规范灯或删除整批公开成绩。
- Java IRScoreData 和 OpenLR2 IRScoreV1 没有稳定局 ID；宿主重复会 new 对象，秒级日期/每次生成 UUID 都不能证明同局。最佳状态幂等与逐局幂等分开；ED assist=0 和 FAILED 灯不能证明无辅助或整曲完成。
- Java RankingData 用全数组长度算人数、空 player 认本人，TopN/第一页会错榜；OpenLR2 才有 TopX/本人/总数。其原生 int ID 需持久唯一映射，不能直接与旧 LR2/OMS ID 混空间。RestoreCachedRank 不能发 HTTP。
- OMS Create 发送真实 APIMods，不能清空 Mod 伪装普通。真实规则 Mod 接收边界从正式合同取；GAS settings 的原枚举是整数，最终数据的枚举是字符串，默认 settings 为空，有效下限钳制不改原参数。JD 捕捉的原 header rank 尚不等于覆盖后的有效 rank，拒绝时保原待交。
- 灯规则不能只匹配 group hash：OMS family 还区分最大 EX，消费服务 `rule_label`；外部 SDK 缺精确 TOTAL / gauge_history 时相同 gauge 数字也不证明可合并。历史 ★FULLCOMBO 保原标签，不能换算为 OMS Perfect。
- 混榜错位先核对筛选位置：来源/公开资格/条件必须先约束最佳 EX、独立灯和参与身份，再计算人数、名次及分页。先取每源 TopN 再拼接，或沿 mandatory group 永远分开，均不满足自由参考混榜。
