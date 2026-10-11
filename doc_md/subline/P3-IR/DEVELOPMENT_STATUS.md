# OMS IR 当前状态

## 原生在线服务

2026-10-12 现行谱面目录与下载已合为一个「谱面列表」入口，OMS / Ginger Rush / 616 / Sayobot 在页内切换，共用原生搜索、实际筛选与卡片显示。各来源保留条件和阅读位置，详情、榜与下载返回原页，慢读取及迟到挂载按当前导航归属处理。只请求当前来源，无启动预抓或新增轮询；后台下载与准确入库 / 打开沿原管理器。当前源码与软件取[统一浏览来源](../../../../oms-server/oms_client_bridge_md/doc_md/other/oms-unified-beatmap-browser-snapshot-20261012.md)，剩余实际体验取[接续计划](CLIENT_NATIVE_SERVICES_PLAN.md)。

新闻、帮助、排行、玩家、真实 MD5 详情与账号沿原生页面；社区继续已有列表 / 正文 / 回复与作者操作。旧统一服务面板已被用户否定并删除，聊天、好友和在线状态后续。Web / Backend 复用已部署接口，客户端页面纠正未改变服务代码或重新部署。

## 玩家当前能做什么

右上角原账号窗口直接注册 / 登录固定 OMSIR；支持的新局在最终本地保存成功后交分。本人个人页展示 UUID 新局、真实判定 / 灯和分页，原账号菜单提供退出与手动补交。未登录或退出后完整离线游玩，匿名局不因中途登录被认领。无待交的匿名启动与打开登录窗口不请求网络。

从所选原谱或本人记录进入公开榜，匿名可主动读取。BMS 支持全部、单个、多个和空来源，完整范围人数 / 名次 / 独立灯与主动同条件沿服务结果；mania 按真实计分条件查询。旧同名身份独立，未知字段如实显示；网页公开最佳和端内私人全历史分别保留。

## 实现来源与适用范围

| 范围 | 已绑定来源 |
| --- | --- |
| 单一谱面列表与来源恢复 | 2026-10-12 源码 589a9624e5605cd872a82bdaab10962c64aec471，[统一浏览快照](../../../../oms-server/oms_client_bridge_md/doc_md/other/oms-unified-beatmap-browser-snapshot-20261012.md) |
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

2026-10-12 Debug 导航 / 下载 / OMS目录与原生服务130项合计226/226；随后仅卡片尾部留白与显示控件回归更新，最终布局 / 下载 / 接线96/96，均0失败、0跳过，两轮有重叠不相加。Desktop.slnf Release / Debug均0错误、各2条原BMS.Tests告警；受改两项目格式和verify-no-changes通过。 准确命令、失败修正与原件取[本次快照](../../../../oms-server/oms_client_bridge_md/doc_md/other/oms-unified-beatmap-browser-snapshot-20261012.md#软件与失败修正)。旧315仅属[此前原生页面纠正来源](../../../../oms-server/oms_client_bridge_md/doc_md/other/oms-client-native-pages-snapshot-20261012.md)，不计为本轮重跑或真人签收。真实账号 / 原包 / 整曲、设备与发行门保持。

## 文档治理验证

2026-10-12 专项对照实现来源、各仓采用点与诊断记忆，收简计划中的已完成步骤，纠正旧入口、旧源码和历史证据的当前称谓。文档检查、来源保全与选择暂存结果只取[主线本轮日志](../../mainline/CHANGELOG.md#2026-10-12进度与文档记忆健康同步)；产品软件 / 生产 / 真人证据保留原日期。
