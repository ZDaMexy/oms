# OMS IR 当前状态

## 原生在线服务（2026-10-11）

2026-10-12 已按用户实测纠正统一面板：新闻、帮助、排行、公开玩家页、谱面目录 / 详情分别使用 lazer 原页面结构和独立工具栏入口。真实排行行 → 玩家 → 公开成绩 → 原谱 → 榜 / 下载 → 返回原位置已贯通；账号与社区草稿保持原归属。当前源码为 6381e12，精确身份和软件 / 真人范围取[新来源](../../../../oms-server/oms_client_bridge_md/doc_md/other/oms-client-native-pages-snapshot-20261012.md)，实施闭环取[计划](CLIENT_NATIVE_SERVICES_PLAN.md)。

旧 f2b495f4 的统一面板与旧软件只属历史，不能认定符合原生体验。网站 / Backend 复用已部署接口，本轮未改代码或重新部署；生产取[原配套发布](../../../../websites/oms-web/doc_md/client-native-services-20261011.md)。聊天、好友、在线状态后续；真实窗口 / IME / DPI、账号 / 下载 / 整曲、P/C 和 Windows 发行仍待签收。

## 2026-10-10 登录与网页接线来源

2026-10-10 当前源码固定连接 OMSIR，右上角直接注册 / 登录即允许支持的新局在本地保存后上传；已删除服务器地址、启用开关、保存连接和账号菜单的连接设置。顶部通用奖杯与端内目录搜索移除，谱面榜从选歌或本人记录的具体谱面进入，未登录可主动查看公开榜。选歌详情 / 复制、谱集逐难度、结算谱名、规范个人页和工具栏玩家排行继续使用真实 MD5 / OMS ID；mania 选择真实计分条件，网页保留来源 / 条件 / 页码。共同职责取[客户端与网页入口](../../../../oms-server/dev_bridge_md/doc_md/subline/oms-player-site/constraints.md#客户端与网页入口)。

网站保持已部署待验收，准确运行包与历次发布 / 失败只取[现行维护](../../../../websites/oms-web/doc_md/production-maintenance.md)。2026-10-11网络审查同步优化客户端待交等待 / 压缩、Web身份 / 下载和Backend元数据；生产图片gzip保持，网站 / Backend已清理后实际部署，三服务同步源 / 有限运行 / 同库往返及前后完整备份只取[本轮跨端审查](../../../../websites/oms-web/doc_md/network-service-review-20261011.md)，没有Windows发行物。旧 osu! API 地址空 / 在线 false 保持；旧 IR 设置文件不读取也不删除，只恢复官方 origin 凭据，其他 origin 的凭据 / 待交不转移或上传。原 UUID / body / owner 不改；客户端真实点击、原账号与待交、下载入库、两端同范围及 P/C 仍待用户非调试验收。

## 玩家当前能做什么

客户端复用右上角原用户按钮、登录窗口与个人页。登录后查看本人 UUID 新局、判定 / 灯与分页，从记录进入同条件或参考榜；退出与手动补交在原账号菜单。未登录或退出后仍可离线游玩，匿名局不因中途登录而认领。公网榜为 `https://oms.zdamexy.work/ir/`；客户端固定官方 origin，打开登录窗口和无待交的匿名启动不请求网络。发行由用户执行，当前工作区待 VS Code 非调试验收；原发布历史见[客户端发布记录](../../other/OMS_IR_CLIENT_RELEASE_20261003.md)。

客户端实际保存 UUID、原谱内容身份、BMS / PMS 键型、mania 实际列数和 BMS 最终点灯 / 血条已接入；待交绑定原服务与原账号，响应丢失重复不新增，401 等待原账号重登。来源和适用提交维护在外部 Client Bridge，跨端合同由 Dev Bridge IR 维护；静态来源、合成场景与实际玩家验收分别记录。

当前工作区从所选 BMS 原谱或本人记录进入参考混榜，选择一个 / 多个 / 全部 / 空来源，查看完整范围人数 / 名次 / 独立灯并主动收窄同条件；读取时关闭按钮可用，换谱关闭旧榜，迟到响应不回填。历史同名身份独立，缺字段 / 未知灯如实显示；mania 及本人逐局沿原路径。官网谱面查询与玩家排行不变，网页公开最佳不伪装本人 UUID 逐局历史。

## 当前客户端与外部候选来源

2026-10-11网络服务实现为 `f3b8a439f0354c2f9b20e8fc2a0ccd467db3dea0`，源码 / 软件与真实传输的适用范围取[新来源快照](../../../../oms-server/oms_client_bridge_md/doc_md/other/oms-ir-network-snapshot-20261011.md)，先经Client Bridge登记，再由Dev Bridge及Web / Backend采用。文档提交不冒充新代码、生产或真人签收。

当前账号 / 谱面榜源码为 `21daf78ddc97c4b801dc5fe5fd92dda48df95525`，来源与消费者沿[Client Bridge 事实登记](../../../../oms-server/oms_client_bridge_md/doc_md/mainline/facts.md)维护；此前接线 `239160cb`、账号 / 查榜 `6168791` 与 mania 六判定 `234a9ff` 保留各自日期和适用范围。窗口 / 焦点 / 长名、实服分页、旧待交和网页同范围仍待用户 VS Code 非调试验收，无 Windows 发行包。

当前网站运行与维护取上方现行维护记录；旧静态 D1/e6 仅为同库设计回退来源。旧版本、运行 / 恢复 / 回退、原 false 与核验范围按 [历史服务证据](../../../../oms-server/oms-backend/doc_md/other/oms-player-site-verification-20261005.md#counts22-候选的实际主机检查2026-10-06)及 [2026-10-07 日志](CHANGELOG.md#2026-10-07维护收尾与真人门)读取，不代签当前源码或真人。

地力黑星、komasan方向与Walkure后置，标准与客户端方案待讨论；PP为后续优先方向，规则 / 范围 / 来源资格须独立采用，当前指标不标PP。先导P / 完整C、30玩法矩阵与全部宿主真人格仍待验收。

## 当前门

- IR0：当前源码来源与采用合同 v1 已建立，消费者沿桥文档登记。
- IR1：真实本地服务 / 网页、同局去重、会话隔离、重启及一致备份空目录恢复已验证；结果与限制以外部 Dev Bridge IR 支线的进展和报告为准。
- IR2 / IR3：保存后交分、Windows 凭据、持久待交、会话 / 账号隔离与端内查询已实现并有有效软件验证；真实 BMS / mania 完整游玩、断网重启和账号切换由用户通过 VS Code 非调试启动验收。
- IR4：2026-10-03 v1 主机 / 恢复基线只取 [原 IR4 报告](../../../../oms-server/oms-backend/doc_md/other/ir4-host-verification-20261003.md)；当时合成规模不代签当前全量历史或原版官网资源门，最新适用运行来源取现行维护。

旧 osu! API 的 EndpointConfiguration 为空、旧 OnlineFeaturesEnabled 为 false；独立 OMSIR 使用固定官方 origin。Phase 1.x 原人工及发行门不随 IR 开工关闭，主线产品状态仍保留其未签收项。

## 多来源扩展状态

2026-10-04 外部 Dev Bridge 修订 3 已完成 D01～D09 逐项审查，必要公开单谱历史、真实来源混榜和对应接入范围已采纳；内容 / 规则证明、全量主机预算及宿主真人门保留，入口见[执行投影](DEVELOPMENT_PLAN.md#多播放器与-lr2-历史榜实施)。先导 P 为 OMS + 全量公开历史 + ED 7K，完整 C 还需其余指定播放器 / 玩法逐格验收，客户端软件通过不关闭 P/C。

- M1：服务接收已存在的真实判定、血条家族、固定血条、长条和 GAS 选项，校验新规则 Mod 与保存后实际字段；客户端原待交 body、UUID 和账号归属保持。JD 与辅助视觉参数仍明确拒绝并保全待交。真实 C# 保存数据投射已通过软件验证，原账号实际补交和对应手动游玩待验收。
- M4：OMS 的来源选择、参考 / 同条件切换、本人完整范围名次、旧身份 / 缺字段 / 独立灯以及账号 / 选谱变化与迟到响应已通过合成行为场景，分页参数由 Service 验证；真实跨页体验仍归用户门。新来源 registry 明示未核验和待宿主验收，不把通道存在视为真人支持完成。
- 早先外部发布 ecca50eab82c-09d7ffdf4bbb / schema3 的完整性 / 实际网络、全量主机 / 两空恢复 / 补账及公开 / 双站 / 首备份保持原日期；当次公开浏览器来源 / 分页 / 深链 / 同条件和桌面 / 390px 补核不代签当前新版。当前来源与新 gate 取上方本次核验，旧十万条容量不代签全量；公开账号 / 密钥 / 本人、DPI、P/C、VS Code非调试OMS及全部宿主真人仍未闭环。

## 最近一次验证

2026-10-12 格式后 Game.Tests Debug 原生页面与账号 / IR / 下载 / 内容必要回归 **315/315，0失败、0跳过**；Desktop.slnf Release / Debug 均0错误、各2条原 BMS.Tests 告警，受改两项目 whitespace 与 verify-no-changes 通过。完整点击、返回位置、切账号 / 迟到响应、草稿与延迟下载挂载均有当前检查。准确命令、原失败修正与原件取[来源快照](../../../../oms-server/oms_client_bridge_md/doc_md/other/oms-client-native-pages-snapshot-20261012.md#有效软件与失败修正)。

此前网络、登录 / 外跳及统一面板的各软件与独立基线留于[本线历史](CHANGELOG.md)，不算本轮重跑或真人签收；真实账号 / 原包 / 整曲、设备与发行门保持。

## 文档治理验证

2026-10-12 CheckDocumentation 与跨仓 verify-workspace 通过，原路径示例 / 公开 checksum 提示保留。选择暂存的全部来源 / 消费者定位及原未提交差异另核通过；当前源码冻结，原文档迁移和删除状态保全。原件为 artifacts/native-pages-correction-20261011/client-docs-final.txt、workspace-facts-final.txt、delivery-scope-audit.json；当前源码、软件与生产分开，不由文档门签真人或发行。此前治理只属[本线历史](CHANGELOG.md)。
