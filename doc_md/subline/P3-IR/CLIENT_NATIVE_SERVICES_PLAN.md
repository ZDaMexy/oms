# OMS 端内原生页面实施与闭环计划

## 目标与范围

用户实机否定统一「OMS 在线服务」面板，要求沿 lazer 原有新闻、wiki / 帮助、排行、个人页、谱面目录与详情结构接入真实 OMS 数据。已有能力纠正完成；聊天、好友和在线状态按用户决定留到后续。持续提交 / 推送授权见[所属计划](DEVELOPMENT_PLAN.md#授权与范围)。不生成 Windows 发行包。

本次基线：客户端 ac77d83、Web 56954f4b、Backend 8bd2dd57、Dev Bridge 277a26f、Client Bridge e27fca88。现有差异保全在 artifacts/native-pages-correction-20261011/before/；最终软件证据为 2026-10-12，源码与准确检查取[新快照](../../../../oms-server/oms_client_bridge_md/doc_md/other/oms-client-native-pages-snapshot-20261012.md)。其他仓已有修改保留，本轮选择暂存自身增量。

## 玩家路径与页面职责

| 路径 | 已实现行为 |
| --- | --- |
| 原工具栏新闻 / 帮助 / 排行 / 谱面目录 | 独立入口和原选中状态，各页保留原 header / banner / 正文布局，没有通用服务门户 |
| 新闻与帮助 | 日期卡片、年月索引、完整文章；帮助首页面板 / 段落侧栏；网站同一份纯文本内容和安全站内链接 |
| 排行 → 玩家 → 成绩 → 原谱 | 原排行表格、正数 int64 OMS 身份、公开统计与真实最佳 / 最近更新卡片、原 MD5 详情；返回恢复筛选 / 位置 |
| 谱面目录 / 选歌详情 / 结算 / 逐难度 | 原目录搜索 / 卡片 / 分页，原详情信息与操作区；v2 为已收录成绩谱面目录，下载源另走既有浏览器 |
| 原谱 → 榜 / 下载 → 返回 | 保留玩法 / 键型 / 来源，单谱再选真实条件；BMS 原 MD5 Resolve，mania 明确为歌名候选；沿原管理器入库 |
| 账号与本人历史 | 原注册 / 登录 / 退出、本人 UUID 私有全历史和待交；公开最佳 / 最近更新不冒充逐局历史 |
| 社区 | 独立原生页、真实列表 / 正文 / 回复、作者操作；原账号 / UUID / 已发送正文重试与有界草稿保持 |
| 网页查看与分享 | 紧凑页头动作和现有复制链接使用规范网站地址；浏览器与桌面分别登录 |

## 实现边界

1. 复用原 NewsOverlay、WikiOverlay、RankingsOverlay、UserProfileOverlay、BeatmapListingOverlay、BeatmapSetOverlay，不开启旧 ppy API / 在线总门。无 WebView、假用户 / 谱集 ID、PP / 国家 / 头像 / 等级 / 在线状态。
2. OmsPageNavigator 只负责非可视导航；至多 16 项历史，保留路由和阅读位置。跨页、隐藏、账号变化取消旧操作，迟到响应与延迟下载 / 私人页挂载核对 navigation revision；自动隐藏先清旧返回回调。
3. 请求只由打开、筛选、翻页或刷新触发；当前页 / tab、匿名内容共用 ETag 缓存、会话返回复用，无启动预抓、逐行资料请求和空闲轮询。预算与账号归属取[共同合同](../../../../oms-server/dev_bridge_md/doc_md/mainline/client-native-services.md)。
4. 纯文本转义后交原生正文排版，帮助 heading 在输入边界验证；OMS RootUrl / DocumentUrl 保持站内链接。未知字段与错误状态如实显示，不把 BMS PLAYLEVEL 冒充计算星数。
5. 本轮现有 Web / Backend 接口与内容足够，未改变服务源码、schema、运行配置或下载代理；不重复服务部署和旧容量 / 恢复门。

## 执行顺序与完成状态

| 步骤 | 实际完成情况 |
| --- | --- |
| 基线与评估 | 五仓 HEAD / 状态 / 差异保全；核原生禁用分支和实际 API，确认既有服务足够 |
| 页面纠正 | 删除统一面板及通用工具栏按钮，分别接入原页面与真实控件；无可视服务主页 |
| 整合导航 | 工具栏、公开成绩、原谱 / 榜 / 下载、本人账号、社区与正文内部跳转贯通，恢复范围 / 位置 |
| 软件检查 | 当前源码聚焦 / 必要回归、Desktop Release / Debug、受改源码格式通过；准确范围与原失败只取来源快照 |
| 来源与交付 | 新提交来源 → Client Bridge 三事实 → Dev Bridge 共同合同 → Web / Backend；文档 / locator 与选择暂存保全通过，准确交付收据留 artifacts/native-pages-correction-20261011/ |

旧 f2b495f4 统一面板的软件与 2026-10-11 服务配套保留于[历史来源](../../../../oms-server/oms_client_bridge_md/doc_md/other/oms-client-native-services-snapshot-20261011.md)和[原配套发布](../../../../websites/oms-web/doc_md/client-native-services-20261011.md)，不能签收纠正后的页面。旧服务器清理和用户 12 个 bin / obj 清理已收尾，继续只取[清理补记](CHANGELOG.md#2026-10-11用户清理基线编译输出复核)，不重做。

## 真人接续与退出条件

用户从 VS Code 非调试启动当前工作区，核新闻 / 帮助 / 排行 / 目录的原生画面与独立入口；实际点击排行玩家、公开成绩、原谱、榜与下载，再返回原位置。核快速切页、关闭、隐藏时退出账号后重开及真实长内容 / IME / DPI。

实际账号 / 社区写入、网页同谱同来源 / 条件、批准原包下载入库、完整手动游玩 / 保存 / 断网重启仍由用户签收。新源码软件闭环和真人门分别记录；P/C、Phase 1.x 设备 / 皮肤与发行门不提升。仅按实际反馈定点修复，已通过且无新变化的检查不重复。
