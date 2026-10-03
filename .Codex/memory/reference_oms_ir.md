# OMS IR 接入诊断

2026-10-03 静态来源 HEAD `a9928fe6da158469aa94b510c744426afdb86acb`，实际合同与状态读 [[../../doc_md/subline/P3-IR/TECHNICAL_CONSTRAINTS.md]]、[[../../doc_md/subline/P3-IR/DEVELOPMENT_STATUS.md]]。外部 Client Bridge 保存提交绑定快照，不用记忆替代来源。

- 旧 Player.prepareAndImportScoreAsync 中 SubmittingPlayer 的网络准备早于 BMS ruleset 最终结果准备。直接接旧入口会缺最终灯 / 血条；新 IR 消费最终保存成功并回写实际 UUID 的结果。
- 9K 的数字 CircleSize 不能判断 BMS / PMS；必须在可玩谱持有 KeymodeResolution 时捕捉。mania 转换 / dual stage 也使实际 TotalColumns 不总等于 CircleSize。
- BMS EMPTY POOR v6 起是 HitResult.Ok (`ok`)，不是 combo_break；最大 EX 来自 maximum_statistics，不能把整张字典求和当物件数。final_gauge 是 0..1，clear_lamp 原生是整数，邻近字段的 StringEnumConverter 不能外推到它。
- 总分版本 `30000016` 也可能来自历史回放导入 / 重算，不能用它证明新局。首接入不自动遍扫历史上传。
- 本机常规 Python 的旧 SQLite 存在 WAL reset 风险；服务开发用外部项目 F 盘 venv 的 SQLite 3.53.1，实际环境 gate 另验，单 worker 不等于只有一个数据库连接。
