# OMS IR 当前状态

## 玩家当前能做什么

客户端已复用右上角原用户按钮、登录窗口与个人页：玩家主动填入服务地址、启用并登录后，可在原个人页查看本人 UUID 新局、判定 / 灯与分页，从记录进入同条件或参考榜；连接设置、退出与手动补交在原账号菜单。奖杯只负责查榜并跳转同一账号入口。新局仍先最终结算和本地保存，再交分；关闭 IR 可离线游玩。公网试运行页面为 `https://oms.zdamexy.work/ir/`，客户端填写 `https://oms.zdamexy.work`。2026-10-05 原界面软件门通过，当前工作区待用户 VS Code 非调试启动验收；发行由用户执行，不生成 Windows 发行包。原发布历史见[客户端发布记录](../../other/OMS_IR_CLIENT_RELEASE_20261003.md)。

客户端实际保存 UUID、原谱内容身份、BMS / PMS 键型、mania 实际列数和 BMS 最终点灯 / 血条已接入；待交绑定原服务与原账号，响应丢失重复不新增，401 等待原账号重登。来源和适用提交维护在外部 Client Bridge，跨端合同由 Dev Bridge IR 维护；静态来源、合成场景与实际玩家验收分别记录。

当前工作区可从所选BMS原谱、谱名/作者/MD5目录进入参考混榜，选一个/多个/全部/空来源，查看完整范围人数/名次/独立灯并主动收窄同条件。历史同名账号独立，缺字段/未知灯如实显示；mania及本人逐局记录沿原路径。2026-10-05 17:16:55（UTC+8）外部新版已发布 `b520bcb99015-5d0531c22423` / schema3，官网实际 osu-web 与客户端原登录 / 个人页衔接，最小网页身份只带 OMS ID。运行来源、全量 / 恢复 / 备份和浏览器工具限制只取[外部本次核验](F:/zdamexy-workspace/websites/oms-website/doc_md/other/osu-web-lazer-account-verification-20261005.md)。状态“已部署待验收”；本次原账号产品源码 6bc5272 软件门已过，真实网页 / OMS 同范围及目标宿主游玩待用户验收，b7d0f74d 为此前多来源来源。

## 当前客户端与外部候选来源

当前客户端运行源码为 `234a9ff39654cdc30f3d5661cb6a9bf69bb90db6`，含原登录 / 本人页与 mania 六判定；文档 HEAD `f05d` 和本次更新不构成新运行验证。默认空地址、旧在线总开关 false 及原 body / UUID / owner 不变；窗口 / 焦点 / 长名、实服分页、旧待交和网页同范围仍待用户 VS Code 非调试验收，无 Windows 发行包。

生产仍为 `b520bcb99015-5d0531c22423`；新完整玩家站候选 `25d32397c330-37f05e2ca369` 已准备但**未部署**。
r1第一次新空恢复的统计首次330.510ms超过300，原失败保留；两失败测试库已完整F保全后定点退役。
2026-10-06 新r2七查询分项后，首BMS个人454.771ms失败；原生 / 1,800秒 / 个人重叠及恢复未发生。排名首读修复与实际七日余量仍待。
旧3ab成功分项与原空间false保留来源，详细证据取[外部本次服务验证](F:/zdamexy-workspace/oms-server/oms-backend/doc_md/other/oms-player-site-verification-20261005.md)。增量边界见[本线计划](DEVELOPMENT_PLAN.md#多播放器与-lr2-历史榜实施)。

地力评级 / Walkure 后置；PP 为后续优先方向，规则与来源资格独立采用后实施，现有收录 / 通关 / mania 累计指标不标 PP。先导 P / 完整 C 和所有宿主真人格仍待验收。

## 当前门

- IR0：当前源码来源与采用合同 v1 已建立，消费者沿桥文档登记。
- IR1：真实本地服务 / 网页、同局去重、会话隔离、重启及一致备份空目录恢复已验证；结果与限制以外部 Dev Bridge IR 支线的进展和报告为准。
- IR2 / IR3：保存后交分、Windows 凭据、持久待交、会话 / 账号隔离与端内查询已实现并有有效软件验证；真实 BMS / mania 完整游玩、断网重启和账号切换由用户通过 VS Code 非调试启动验收。
- IR4：真实共享主机十万条合成历史的 50 局集中交分 / 25 次查榜每秒通过，RSS 峰值约 131 MiB；两次新空目录恢复与未 checkpoint WAL 中会话撤销均通过。服务和网页已生产部署，TLS / cookie / Origin / 本人历史隔离通过，原两站仍 200；50 人同出口集中登录约 66 秒才能全部成功，注册现限每出口 20 人 / 小时。详细指标和维护回退归外部 Backend IR4 报告。

默认 endpoint、旧 OnlineFeaturesEnabled 仍保持为空 / false。Phase 1.x 原人工及发行门不随 IR 开工关闭，主线产品状态仍保留其未签收项。

## 多来源扩展状态

2026-10-04 外部 Dev Bridge 修订 3 已完成 D01～D09 逐项审查，必要公开单谱历史、真实来源混榜和对应接入范围已采纳；内容 / 规则证明、全量主机预算及宿主真人门保留，入口见[执行投影](DEVELOPMENT_PLAN.md#多播放器与-lr2-历史榜实施)。先导 P 为 OMS + 全量公开历史 + ED 7K，完整 C 还需其余指定播放器 / 玩法逐格验收，客户端软件通过不关闭 P/C。

- M1：服务接收已存在的真实判定、血条家族、固定血条、长条和 GAS 选项，校验新规则 Mod 与保存后实际字段；客户端原待交 body、UUID 和账号归属保持。JD 与辅助视觉参数仍明确拒绝并保全待交。真实 C# 保存数据投射已通过软件验证，原账号实际补交和对应手动游玩待验收。
- M4：OMS 的来源选择、参考 / 同条件切换、本人完整范围名次、旧身份 / 缺字段 / 独立灯以及账号 / 选谱变化与迟到响应已通过合成行为场景，分页参数由 Service 验证；真实跨页体验仍归用户门。新来源 registry 明示未核验和待宿主验收，不把通道存在视为真人支持完成。
- 早先外部发布 ecca50eab82c-09d7ffdf4bbb / schema3 的完整性 / 实际网络、全量主机 / 两空恢复 / 补账及公开 / 双站 / 首备份保持原日期；当次公开浏览器来源 / 分页 / 深链 / 同条件和桌面 / 390px 补核不代签当前新版。当前来源与新 gate 取上方本次核验，旧十万条容量不代签全量；公开账号 / 密钥 / 本人、DPI、P/C、VS Code非调试OMS及全部宿主真人仍未闭环。

## 最近一次验证

2026-10-06 唯一排名索引与旧库原子升级软件门通过，SQL提速不代签HTTP。新主机 / 旧写 / 全量持续 / 两恢复及发布待，生产b520；取[执行记录](DEVELOPMENT_PLAN.md#多播放器与-lr2-历史榜实施)。客户端234未变，真人P/C未提升。

2026-10-06 OpenLR2 R4 在3ab源码 / 固定SDK / 专用软件Host上通过双架构非空消费；不证明正式DLL与真实EXE的STL、标准LN、UI、线程或断线。1,002身份、请求 / 统计 / 缓存推断和实际构建来源见[本日记录](CHANGELOG.md)。后续25d正式重编不改R4来源，P/C真人仍待。

旧25d / 37f的[host r7](../../../artifacts/oms-player-site-20261005/host-player-report-r7.json)两玩法1,000 / 100,000不同最佳通过，53账号、十次含首；个人≤300ms、榜≤1秒及500MiB / 150%、共享余量、schema3往返留证。仅适用旧来源与实际规模，不签新索引、近三万人口或完整持续门。

2026-10-05 后续已采用完整玩家网站范围；外部官网实施中，当前部署仍取上方实际版本。客户端本人记录修正 mania Ok 错名 / 漏 Meh，原六类计数与 BMS 判定分开；真实 factory focused Release 11/11 通过，命令与证据见[本次记录](../../other/OMS_PLAYER_SITE_20261005.md)。保存、UUID和原账号待交归属未改；VS Code 非调试 / 两端真人与 P/C 保留。

2026-10-05 原账号 / 个人页 Release 有效复编，64/64通过，含原lazer界面、取消 / 迟到响应、账号切换、UUID归属与分页。
个人页保留本人逐局记录，网页外链仅带真实OMS ID；不造旧APIUser、PP或头像。Desktop普通Release编译成功，原BMS两警告保留，未打包。
命令、失败身份、日志 / TRX与修复见[本线日志](CHANGELOG.md#2026-10-05原登录和个人页复用)。布局 / 焦点 / 长名、实服分页与网页同账号仍待真人，不签收P/C。

2026-10-04 集中 Release 复编后，`osu.Game.Tests` 的 Service / Overlay focused 32/32 通过（Service 30 项、场景构造与行为 2 项）；`BmsOmsIrSubmissionTest` 27/27 通过，导出 18 份合成 JSON，其中 17 份新增覆盖真实规则 Mod、LR2 / LR2G 五轴以及 GAS 默认、显式设置和下限钳制。投射经过原生录制初始化、最终结果准备及保存字段 clone，保留 UUID、原设置和实际规则轴；它们是软件契约证据，不是玩家游玩。BMS 编译输出保留其他源文件的 CS8600 / CA2007 警告，不宣称零警告或依赖审计通过。

来源范围、空选择、原 EX 排序、旧同名身份、独立灯、本人名次、mania 回归、选谱变化和迟到响应由场景验证；有效会话的 v2 Bearer、原 v1 刷新与不退匿名由 Service 验证。首次无输入目录读取曾因内部搜索 Bindable 默认 null，在进入 HTTP 前失败；初始化为 `string.Empty` 后原行为断言通过，临时诊断输出已删除。日志 / TRX 和新导出留 `artifacts/oms-ir-multisource-20261004/`，身份与命令配置归[本线日志](CHANGELOG.md#2026-10-04多来源正式采用与客户端查询)；早期失败证据保留。

随后同一 Release 编译产物的全部 core IR relevant 46/46 通过，涵盖提交、Player 保存链、Service 与面板；Desktop 普通 Release 编译成功。18份实际Create导出在隔离服务原样受理，再交保持同UUID和原规则字段，完整本人记录守恒；首轮探针错误使用不存在的score.user字段，修正验收工具后以本人历史核对，不是产品上传失败。证据为 `oms-all-relevant.log` / `oms-ir-all-relevant.trx`、`desktop-release.log`、`m1-client-backend-report.json`；没有写入本次生产测试账号或成绩。

2026-10-03 原 Player / Realm、公网两份 C# 合成受理、Desktop 与发行历史证据仍归[客户端发布记录](../../other/OMS_IR_CLIENT_RELEASE_20261003.md)和 `artifacts/oms-ir-release-20261003/`，首轮留 `artifacts/oms-ir-start-20261003/`。本次未生成 Windows 发行包、publish 或安装副本，也未签收真实手动游玩、目标宿主及此前 Phase 1.x 设备 / 皮肤 / 发行门。

## 文档治理验证

2026-10-06 根执行 CheckDocumentation.ps1 / git diff --check通过（199Markdown / 2017链接 / 310锚点 / 125记忆wiki链），workspace124文档 / 930链接通过；证据为 `client-docs-r9.log`。此次长度精简后提交前复检结果另存 `artifacts/oms-player-site-20261005/client-docs-r10.log`；原通用警告保留，仅同步文档，不刷新产品 / 真人日期。

2026-10-05，外部实际部署、公开匿名读榜、P/C剩余门、维护与收据故障已同步状态/计划/日志及记忆；统一执行者运行 CheckDocumentation.ps1 与 git diff --check 通过（198Markdown/2002相对链接/305锚点/125记忆wiki链）。原有公开checksum/通用路径提示保留；证据为 `artifacts/oms-ir-multisource-20261004/client-docs-deployment-r10-r3.log`，外部协作检查110文档/739链接及原来源/消费者边界独立核对。只同步文档，没有重跑未改客户端产品、生成Windows包或代签真人。

2026-10-04正式采用的软件结果、首轮RELEASE旧锚点修复和 `oms-documentation-r2.log`保留原日期；更早专项检查归[历史](CHANGELOG.md#2026-10-04专项进度文档与记忆健康同步)与 `artifacts/oms-ir-doc-sync-20261004/`，文档更新不刷新产品或真人日期。
