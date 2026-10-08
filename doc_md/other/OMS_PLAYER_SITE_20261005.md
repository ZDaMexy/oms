# 2026-10-05 玩家网站接续与客户端 mania 判定

## 已采用范围与当前状态

用户要求以 osu-web 的实际谱面、个人与排行页面为基础补齐 BMS / mania 玩家服务，而非只改配色或最小身份页。共同采用合同归 [外部玩家网站专项](../../../oms-server/dev_bridge_md/doc_md/subline/oms-player-site/constraints.md)，谱面查询 / 原包获取限既有批准的 Ginger / 616 / Sayobot 来源。地力评级按用户意见后置，BMS PP 的规则与优先级另议；不把积累指标叫 PP。新网站范围当前实施与验收中，既有生产版和 P/C 真人待验收状态保留。

游戏继续从原账号按钮登录、打开原个人页。官网可扩展当前公开最佳和分条件统计；游戏本人完整 UUID 历史不因此公开。官网跳原下载源不等于客户端自动入库，mania 原始 sid / bid 未有真实 MD5 关联时不能猜造。默认空地址、旧在线总开关 false 与保存后 UUID / 原账号待交归属不变。无 Windows 发行包、publish 或额外安装副本。

## 本次客户端修复

原 `OmsIrOverlay.ScoreDetails.FromJson()` 对所有玩法共用 BMS 判定表，导致 mania 的 Ok 错显示为 EMPTY POOR 并漏 Meh。现在按真实 ruleset 分支：BMS 原五类不变，mania 保留 Perfect / Great / Good / Ok / Meh / Miss 六类，未知 ruleset 明确拒绝。仅修展示，不变更保存字段、计分版本或交分 payload。

新增行为断言通过真实 ManiaRuleset、Capture / Create、HitResult 字典与详情读取证明六类计数守恒，且不出现 BMS EMPTY POOR / 灯。2026-10-05 根统一执行者先加载 `UseDevelopmentStorage.ps1`，运行 `dotnet test osu.Game.Tests/osu.Game.Tests.csproj --filter 'FullyQualifiedName~OmsIrSubmissionTests' -p:Configuration=Release --logger 'trx;LogFileName=mania-native-judgements-r1.trx' --results-directory F:/oms/artifacts/oms-player-site-20261005/client-results --verbosity minimal`：有效 restore / Release 编译，11/11 通过。证据 `artifacts/oms-player-site-20261005/client-judgements-r1.log` 与对应 TRX。此为软件验证，不代签真实 mania 游玩或客户端布局。

## 还需验收

同一根执行者随后运行 `dotnet build osu.Desktop.slnf -p:Configuration=Release --no-restore -p:GenerateFullPaths=true -m:1 --verbosity minimal`：普通 Desktop Release 成功，保留未改 BMS 测试的 CS8600 / CA2007 两项警告，日志 `artifacts/oms-player-site-20261005/desktop-release-r1.log`。文档初查发现新证据未入索引和 PLAN 混入结果，修复导航及计划职责后 r2 通过；历史 r1 保留。

新官网谱面获取、真实账号 / 公开最佳 / 玩家榜 / 原社区与两端一致性、共享主机预算和两次空目录恢复由外部专项归档后再部署试运行。用户通过[当前客户端工作区](../../AGENTS.md#开发磁盘约束)的 VS Code 非调试启动核对 mania 六类判定、原账号入口、旧待交 / UUID / owner 与网页本人页；ED 7K 先导和全部固定宿主 / 玩法 C、非空跨 DLL 容器及 Phase 1.x 原真人 / 发行门均未关闭。
