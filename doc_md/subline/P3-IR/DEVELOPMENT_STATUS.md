# OMS IR 当前状态

## 玩家当前能做什么

客户端增加独立 IR 奖杯入口：玩家主动填入服务地址、启用并登录后，新局先完成最终结算和本地保存，再交分；可在端内读榜、查看本人记录与手动补交。关闭 IR 仍可离线游玩。公网试运行页面已发布到 `https://oms.zdamexy.work/ir/`，客户端填写的地址为 `https://oms.zdamexy.work`。用户通过 VS Code 非调试启动当前工作区验收，发行构建由用户自行执行；误生成的本轮 ZIP / publish 与程序副本已清理，既有验证日志和恢复数据保留，见[本轮记录](../../other/OMS_IR_CLIENT_RELEASE_20261003.md)。

客户端实际保存 UUID、原谱内容身份、BMS / PMS 键型、mania 实际列数和 BMS 最终点灯 / 血条已接入；待交绑定原服务与原账号，响应丢失重复不新增，401 等待原账号重登。来源和适用提交维护在外部 Client Bridge，跨端合同由 Dev Bridge IR 维护；静态来源、合成场景与实际玩家验收分别记录。

## 当前门

- IR0：当前源码来源与采用合同 v1 已建立，消费者沿桥文档登记。
- IR1：真实本地服务 / 网页、同局去重、会话隔离、重启及一致备份空目录恢复已验证；结果与限制以外部 Dev Bridge IR 支线的进展和报告为准。
- IR2 / IR3：保存后交分、Windows 凭据、持久待交、会话 / 账号隔离与端内查询已实现并有有效软件验证；真实 BMS / mania 完整游玩、断网重启和账号切换由用户通过 VS Code 非调试启动验收。
- IR4：真实共享主机十万条合成历史的 50 局集中交分 / 25 次查榜每秒通过，RSS 峰值约 131 MiB；两次新空目录恢复与未 checkpoint WAL 中会话撤销均通过。服务和网页已生产部署，TLS / cookie / Origin / 本人历史隔离通过，原两站仍 200；50 人同出口集中登录约 66 秒才能全部成功，注册现限每出口 20 人 / 小时。详细指标和维护回退归外部 Backend IR4 报告。

默认 endpoint、旧 OnlineFeaturesEnabled 仍保持为空 / false。Phase 1.x 原人工及发行门不随 IR 开工关闭，主线产品状态仍保留其未签收项。

## 2026-10-03 验证范围

客户端 core IR Release focused 43 通过，BMS IR / 点灯 / 计分 relevant 101 通过，Desktop Release 构建成功。真实 Player + ScoreManager / Realm 场景证明保存未完成时无交分、保存后使用实际 UUID、关闭 IR 仍保存和匿名开局不认领；HTTP 为合成服务，不代签设备。两份实际 C# Create 导出的合成 JSON 未改字段即被公网 API 接收，同局重传 200 / 不新增、榜单 / 本人历史保持一致、刷新旋转及退出撤销均通过。后端 60 行为检查通过，1 条 Starlette 弃用警告；NuGet 漏洞信息源不可达警告保留，不能宣称依赖审计通过。

本轮命令、失败修复与证据见[客户端发布记录](../../other/OMS_IR_CLIENT_RELEASE_20261003.md)，长期证据在 `artifacts/oms-ir-release-20261003/`；首轮本地历史保留在 `artifacts/oms-ir-start-20261003/`。真实手动游玩和此前 Phase 1.x 设备 / 皮肤 / 发行门没有被自动检查关闭。
