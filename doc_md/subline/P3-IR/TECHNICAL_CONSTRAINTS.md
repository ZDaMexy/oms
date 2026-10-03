# OMS IR 客户端接入约束

## 产品和授权

2026-10-03 用户授权独立按需 IR 持续开发、commit / push 与直接部署试运行，范围见[计划](DEVELOPMENT_PLAN.md)。软件与运行门须先通过，真实游玩由用户在部署环境验收。Phase 3 开发不自动启用官网谱包下载、聊天、presence、观战、多人、自动更新或所有旧 API。客户端默认服务地址仍为空；不能把预留接口当作当前能力。

## 身份、结算与保存

- 使用独立服务设置、账号会话和请求消费者，`OnlineFeaturesEnabled=false` 及空 EndpointConfiguration 不改；不借旧 SoloPlayer legacy / 正数 OnlineID gate。
- 本局选定的服务、账号与提交 UUID 在异步响应或重登时保持原所有权；切换账号不把旧待交重新绑定给新账号。
- 真实本地保存优先。BMS `PrepareScoreInfoForResults` 写入最终血条和灯后，`ImportScore` 成功并回写实际 UUID，才允许创建待交。旧网络钩子位于 BMS 最终准备之前，不能直接复用。
- 不从已有成绩版本号相同推导新游玩，也不自动领取匿名局 / 扫描历史回放上传。失败、中退与手动保存另立真实用户路径。
- 原谱 MD5 关联难度表，SHA256 守住内容声明；沿已验证的可玩谱来源，不在每层重读文件。差分谱父子关系不在首版。

## 玩法与结果

- BMS 采用 v7 最终数据，EX 由 perfect×2+great；最大 EX 同式读最大统计；EMPTY POOR 使用 ok；final_gauge 为 0..1；原生 clear_lamp 为整数。完整数据通过新 API 适配为对象，不假定旧 ForSubmission 字段足够。
- BMS 的 IR `passed` 表示最终点灯成功，按 `clear_lamp >= 2` 投射；本地 `ScoreInfo.Passed` 可以只表示完成整曲，Normal 未达最终清条线仍为 true。适配不修改本地完成状态；整曲保存的 FAILED 点灯可以上传，中途退出 / 失败手动保存和历史回放不进入首版新局路径。
- BMS keymode 从本局 BmsBeatmap.BmsInfo.KeymodeResolution 捕捉，9K BMS / PMS 明确区分。mania 从实际可玩谱 TotalColumns 捕捉，历史恢复要应用真实 score.Mods，不一概读 CircleSize。
- mania 计分版本独立于 BMS ruleset data 版本；客户端必须读实际保存的 TotalScoreVersion。`30000016` 只表示计算版本，不能证明成绩真实性或新游玩来源。
- 分组不复用当前 display bucket / Mod.Ranked；GAS 的开始、下限、家族和自动降档条件参与分组，最后活动血条是结果，最佳分和最佳灯可来自不同局。辅助单组，全自动仅本人历史。
- 首轮固定 client-reported、未经回放核验。输入校验、账号认证和幂等不构成反作弊证明。不支持的玩法明确失败并保全本地成绩与待交。

## 凭据与离线

桌面 Bearer / refresh 令牌放 Windows 凭据库，目标绑定保存根与服务 origin，不写明文 Realm、日志或配置。刷新旋转，401 停止补交、保全待交、重登再交；响应或响应体丢失仍使用原 UUID，关闭 IR 不阻断本地保存。每局待交原子写入当前保存根 `oms-ir/pending/`，本地落盘不等待账号网络请求；保留完整恢复文件与异常原件，无法恢复时明确停 IR，不伪造空队列。

只在存在当前服务 / 账号的有效待交时启动有限重试；普通网络失败按 5 / 15 / 45 / 120 秒等待，最多五次后保全并提示手动重试。422 / 409 保留失败原因，重试沿同一 UUID 与原 body；没有闲置轮询、presence 或常驻连接。端内请求回到更新线程，账号 / 服务变化后清空旧私人列表，迟到响应不能重新显示旧账号数据。

跨端正式合同在外部 `F:\zdamexy-workspace\oms-server\dev_bridge_md\doc_md\subline\oms-ir\constraints.md`；本文件只约束客户端消费，实施变更须同步事实桥、Dev Bridge、Backend 和 Website 的实际落点。
