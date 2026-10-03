# OMS IR 接入诊断

2026-10-03 静态来源 HEAD `a9928fe6da158469aa94b510c744426afdb86acb`，实际合同与状态读 [[../../doc_md/subline/P3-IR/TECHNICAL_CONSTRAINTS.md]]、[[../../doc_md/subline/P3-IR/DEVELOPMENT_STATUS.md]]。外部 Client Bridge 保存提交绑定快照，不用记忆替代来源。

- 旧 Player.prepareAndImportScoreAsync 中 SubmittingPlayer 的网络准备早于 BMS ruleset 最终结果准备。直接接旧入口会缺最终灯 / 血条；新 IR 消费最终保存成功并回写实际 UUID 的结果。
- 9K 的数字 CircleSize 不能判断 BMS / PMS；必须在可玩谱持有 KeymodeResolution 时捕捉。mania 转换 / dual stage 也使实际 TotalColumns 不总等于 CircleSize。
- BMS EMPTY POOR v6 起是 HitResult.Ok (`ok`)，不是 combo_break；最大 EX 来自 maximum_statistics，不能把整张字典求和当物件数。final_gauge 是 0..1，clear_lamp 原生是整数，邻近字段的 StringEnumConverter 不能外推到它。
- 总分版本 `30000016` 也可能来自历史回放导入 / 重算，不能用它证明新局。首接入不自动遍扫历史上传。
- 本机常规 Python 的旧 SQLite 存在 WAL reset 风险；服务开发用外部项目 F 盘 venv 的 SQLite 3.53.1，实际环境 gate 另验，单 worker 不等于只有一个数据库连接。

本轮实际接入新增诊断：

- BMS converter 可复制新的可玩 BeatmapInfo 且丢失 hash，交分上下文取 loader 已验证的原谱身份；实际列数 / 键型仍取可玩谱。
- BMS Normal 完整结束但未达到清条线时，本地 Passed 仍 true；IR passed 按最终 clear_lamp >=2 投射，不能修改本地“已完成”来迎合接口。
- InvalidDataException 不是 IOException 子类，异常 settings / pending 恢复须显式处理并停 IR / 保全原件；响应体读取 IOException 是网络丢失，仍以原 UUID 重试。
- 401 清除凭据失败不能保留内存会话继续重发：先使内存身份失效、标记需要登录，再尝试 Windows 删除。排队本地写入也不能等待正在登录的 HTTP 锁。
- Git URL 专用 `http.https://github.com/.proxy` 覆盖普通 `http.proxy=`。本轮成功用每命令清空该项、schannel / HTTP1.1，不关闭 TLS 或永久改配置。
- .NET / NuGet 会读取 Windows 系统代理，curl --noproxy 正常不代表 dotnet 可达。当前进程显式 HTTP_PROXY / HTTPS_PROXY 加官方域名 NO_PROXY 后实际绕行成功、官方完整包恢复；不能靠改源或反复 ignore-failed-sources 掩盖 TLS 故障，不改全局代理或跳过签名。
- 十万局榜单避免全历史 payload 排序和按人逐次全表点灯扫描；正式 SQL 先取每人最佳 ID，再排名读 payload，灯走独立覆盖索引。容量探针计入调度到确认，不能把 429 或排队延迟剔除。
- “真实环境验收”不授权客户端打包：2026-10-03 用户明确日常用 VS Code 非调试启动，发行构建自行执行。稳定约定在 AGENTS；不要由部署服务推导需要 ZIP / publish / 额外安装副本，既有发行门留到用户构建时验收。

多来源取证召回（2026-10-04；正式决定仍读 [[../../doc_md/subline/P3-IR/DEVELOPMENT_PLAN.md#多播放器与-lr2-历史榜待审查规划]]）：

- archive 的 PB 是 (MD5, 原玩家 ID) 最佳摘要；缺逐次时间/SHA256/完整规则，不可塞成完整 OMS v7 或自动注册旧身份。用户给的 v3.db 路径实际是目录；只访问明确授权的目标，schema/汇总证据留 artifacts，不扫描其他 private-data。
- schema 的 privacy_level=full 是隐藏全部个人统计，不等于单谱 PB 私密；profile 缺失也不是私密证明。★FULLCOMBO、option、异常及停榜标记需要原站/解析器语义取证，不能由标签猜规范灯或删除整批公开成绩。
- Java IRScoreData 和 OpenLR2 IRScoreV1 没有稳定局 ID；宿主重复会 new 对象，秒级日期/每次生成 UUID 都不能证明同局。最佳状态幂等与逐局幂等分开；ED assist=0 和 FAILED 灯不能证明无辅助或整曲完成。
- Java RankingData 用全数组长度算人数、空 player 认本人，TopN/第一页会错榜；OpenLR2 才有 TopX/本人/总数。其原生 int ID 需持久唯一映射，不能直接与旧 LR2/OMS ID 混空间。RestoreCachedRank 不能发 HTTP。
- 现行 OMS Create 发送全部 APIMods，但服务仅允许 ASCR/ANOT/AT、最多两项；LR2/LR2G 是已有玩法的真实接收缺口，不能清空 Mod 来伪装支持。来源筛选必须先约束参与者/最佳 EX/独立灯/人数，再排名分页；参考混榜不能沿 mandatory group 后实际永远分开。
