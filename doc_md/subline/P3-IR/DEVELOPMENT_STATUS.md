# OMS IR 当前状态

## 原生在线服务

2026-10-12 现行「谱面列表」只含 Ginger Rush / 616 / Sayobot 三个批准来源，撤出此前错误混入的 OMS 目录来源 / UI / 列表读取，具体原 MD5 详情与榜保留。按实际字段对标 lazer 的紧凑筛选、普通 / 大卡和悬停 / 展开；稳定标题、匹配谱数及真实收录 / 难度信息独立于下载状态，固定字体 / 窄窗换行。来源条件 / 阅读位置、慢恢复、当前路由与后台准确入库打开保持；没有启动预抓或新轮询。当前源码、软件和隔离桌面渲染取[三来源快照](../../../../oms-server/oms_client_bridge_md/doc_md/other/oms-beatmap-browser-lazer-snapshot-20261012.md)，剩余真人体验取[接续计划](CLIENT_NATIVE_SERVICES_PLAN.md)。

新闻、帮助、排行、玩家、真实 MD5 详情与账号沿原生页面；社区继续已有列表 / 正文 / 回复与作者操作。旧统一服务面板已被用户否定并删除，聊天、好友和在线状态后续。Web / Backend 复用已部署接口，客户端页面纠正未改变服务代码或重新部署。

## 玩家当前能做什么

右上角原账号窗口直接注册 / 登录固定 OMSIR；支持的新局在最终本地保存成功后交分。本人个人页展示 UUID 新局、真实判定 / 灯和分页，原账号菜单提供退出与手动补交。未登录或退出后完整离线游玩，匿名局不因中途登录被认领。无待交的匿名启动与打开登录窗口不请求网络。

从所选原谱或本人记录进入公开榜，匿名可主动读取。BMS 支持全部、单个、多个和空来源，完整范围人数 / 名次 / 独立灯与主动同条件沿服务结果；mania 按真实计分条件查询。旧同名身份独立，未知字段如实显示；网页公开最佳和端内私人全历史分别保留。

## 实现来源与适用范围

| 范围 | 已绑定来源 |
| --- | --- |
| 三个批准源 / 原生卡片 / 来源恢复 | 2026-10-12 源码 fff09a2431b9cbc80206bf0d83c69192963b5347，[三来源与 lazer 布局快照](../../../../oms-server/oms_client_bridge_md/doc_md/other/oms-beatmap-browser-lazer-snapshot-20261012.md) |
| 其余原生页面、社区归属与内容缓存 | 2026-10-12 源码 6381e120aea1ff63dfdd34ff0e9fe38ac05f1528，[原生页面纠正快照](../../../../oms-server/oms_client_bridge_md/doc_md/other/oms-client-native-pages-snapshot-20261012.md) |
| 原账号统一等待、持久期限、手动账号复核与 JSON 压缩 | 2026-10-11 源码 f3b8a439f0354c2f9b20e8fc2a0ccd467db3dea0，[网络快照](../../../../oms-server/oms_client_bridge_md/doc_md/other/oms-ir-network-snapshot-20261011.md) |
| 固定 OMSIR、直接登录与官方凭据 / 待交隔离 | 2026-10-10 源码 21daf78ddc97c4b801dc5fe5fd92dda48df95525，[账号快照](../../../../oms-server/oms_client_bridge_md/doc_md/other/oms-ir-account-login-snapshot-20261010.md) |

表中是各行为的来源提交，文档 HEAD 不构成新的实现或软件验证。来源 / 适用性以[事实登记](../../../../oms-server/oms_client_bridge_md/doc_md/mainline/facts.md)为准，共同职责取[原生合同](../../../../oms-server/dev_bridge_md/doc_md/mainline/client-native-services.md)与[客户端 / 网页入口](../../../../oms-server/dev_bridge_md/doc_md/subline/oms-player-site/constraints.md#客户端与网页入口)。旧目录删除、默认外跳与保存连接按[本线历史](CHANGELOG.md)读取，不再作为当前操作。

网站和 Backend 保持已部署待验收，准确运行、固定备份与同库回退只取[现行维护](../../../../websites/oms-web/doc_md/production-maintenance.md#当前来源与运行位置)。2026-10-11 服务配套发布取[原收据](../../../../websites/oms-web/doc_md/client-native-services-20261011.md#生产发布与闭环)；旧静态 D1/e6 仅保全 / 回退，旧软件、容量和恢复不签当前源码或真人。

## 当前门

- IR0：源码事实与采用合同已建立，消费者沿两桥登记；旧 osu! API EndpointConfiguration 为空、OnlineFeaturesEnabled 为 false。
- IR1：真实本地服务 / 网页、幂等、会话隔离、重启和空目录恢复已有原日期证据，适用范围沿[外部 IR 进展](../../../../oms-server/dev_bridge_md/doc_md/subline/oms-ir/dev-progress.md)。
- IR2 / IR3：保存后交分、Windows 凭据、持久待交、账号隔离及查询有软件证据；实际完整 BMS / mania、原账号补交、断网重启和两端同范围仍待用户非调试验收。
- IR4：旧小型 v1 基线沿[2026-10-03 报告](../../../../oms-server/oms-backend/doc_md/other/ir4-host-verification-20261003.md)，当前全量 / 网站运行沿现行维护，旧合成规模不代签全量容量。

真实窗口 / IME / DPI、账号 / 原包 / 整曲、P/C、Phase 1.x 设备 / 皮肤与发行均未签收。用户从当前工作区 VS Code 非调试启动，发行自行构建；本专项没有 Windows 发行包、publish 或安装副本。

## 多来源扩展状态

D01～D09 修订 3 已采纳，OMS M1 / M4 的实际保存 Mod 投射、来源选择与迟到取消有原来源软件证据。JD 与未开放辅助视觉参数继续明确拒绝并保全原待交 body / UUID / owner；接入通道与合成场景不等于真实宿主可用。完整版本、资格、M0～M8 / A01～A12 与旧失败只取[IR 专项矩阵](../../../../oms-server/dev_bridge_md/doc_md/subline/oms-ir/dev-progress.md#多来源-ir-实施)。

先导 P 仍为 OMS＋全量公开历史＋ED7K，完整 C 还需指定播放器、两 Open 架构与全部玩法逐格真人验收；动作见[执行投影](DEVELOPMENT_PLAN.md#多播放器与-lr2-历史榜实施)。地力 / komasan / Walkure 后置，PP 规则、范围和来源资格尚需正式采用，当前指标不标 PP。

## 最近一次验证

2026-10-12 当前相关回归244/244，0失败 / 跳过；含三来源、实际筛选 / 路由 / 恢复、下载 / 精确打开与原生卡片相关行为。Desktop Debug / Release均0错误，各2条原BMS.Tests告警；受改项目格式 verify-no-changes通过。26张隔离桌面截图已逐图核对宽 / 窄、普通 / 大卡、中文长名 / 末卡展开、无图 / 合成封面及下载 / 失败 / 重试 / 完成。另两项原生卡片扩测失败的身份 / 消息 / 堆栈与原HEAD基线一致，最终filter显式排除，不称全套全绿。准确命令、修正和原件取[本次快照](../../../../oms-server/oms_client_bridge_md/doc_md/other/oms-beatmap-browser-lazer-snapshot-20261012.md#软件与渲染证据)与[本线历史](CHANGELOG.md#2026-10-12三来源与-lazer-布局闭环)；旧软件 / 生产保留各自日期，真实账号 / 原包 / 整曲、设备与发行未签收。

## 文档治理验证

2026-10-12 专项对照实现来源、各仓采用点与诊断记忆，收简计划中的已完成步骤，纠正旧入口、旧源码和历史证据的当前称谓。文档检查、来源保全与选择暂存结果只取[主线本轮日志](../../mainline/CHANGELOG.md#2026-10-12进度与文档记忆健康同步)；产品软件 / 生产 / 真人证据保留原日期。
