# OMS 账号与查榜界面复查（2026-10-07）

客户端软件来源 `6168791c2cd4da2386de467c3fa5a116b3bb26e7`；后续文档提交不是新软件证据。配套官网于04:25:04 CST实际发布d1 / e6，来源、缓存 / 对应源码、备份与两空恢复见[网站报告](../../../websites/oms-website/doc_md/other/oms-deai-verification-20261007.md)；两端真实体验仍待用户。

本轮落实用户的“深度去 AI 味”要求，继续原 lazer 账号和个人页，不增加第二套登录面板。玩家能先保存明确的 IR 连接，再登录；查看谱面时切换来源会回参考榜并重新选择条件，成绩详情在原位置展开。当前为源码与软件验证通过，日常窗口体验仍由用户通过 VS Code 非调试启动签收，没有 Windows 发行包。

## 采用的修订

| 实际问题 | 修订与保留边界 |
| --- | --- |
| 登录地址、启用开关与凭据混在一起；未保存新地址仍可能向旧连接登录 | 连接地址、启用和保存放在一起；设置与已保存连接不一致时明确提示并禁用登录、注册与密码回车。保存不发送凭据，不自动启用或填默认地址。 |
| 奖杯和查榜页重复解释整个开发方案 | 奖杯称“谱面排行榜”；查询、加载、空结果直接说明当前动作。原账号页保留真实待上传数量及其他账号归属。 |
| 长列表的详情跳到页面顶部，长 UUID 挤占每行 | 原位置展开、收起；完整 UUID、原记录、条件、未知灯与实际时间留在详情。 |
| 同条件下改来源仍沿用旧条件 | 换来源清空旧条件，回参考混榜、第一页并重新请求；全部取消保持空选择，不偷偷回全部。 |
| mania 被泛用的灯说明误述 | mania 只显示实际通过与最佳分；独立灯语义限 BMS。本人页去掉重复谱名，不改变服务数据。 |

默认空 endpoint、旧在线总开关 false、离线优先及按需请求保持。没有改 OmsIrService、保存 factory、旧待交 body、UUID、owner、外部最佳状态或 LR2IR 摘要语义；没有新增账号同名合并、PP、头像或在线状态。

## 实际软件证据

证据目录为 `F:/oms/artifacts/oms-deai-20261007/`。构建、测试由根执行者串行进行，每次新 shell 先执行开发存储入口。

- `client-build-r1.log`：新编 `osu.Game.Tests` Release，通过，0 warning / 0 error。
- `client-focused-r1.log` / TRX：17 例中 15 通过、2 失败。两失败 `TestNativeLoginRegistrationAndPrivateProfile` 和 `TestUnsavedConnectionCannotSendCredentials` 均为新增连接提示在异步 Dispose 线程直接修改 TextFlow；原件保留。
- 修复只把该刷新排到界面线程并跳过已释放表单；取消 token、清密码、保存后按钮更新和测试断言未绕过。
- `client-build-r2.log` 与 `client-focused-r2.log` / TRX：重新编译后原 17 例全部通过。集合为 `TestSceneOmsAccount`、`TestSceneOmsIrOverlay`、`OmsIrSubmissionTests`。
- `client-desktop-release-r3.log`：普通 `osu.Desktop.slnf` Release 编译成功；既有 BMS 测试 CS8600（TestSceneFilesystemBackedStoryboardFallback:151）与 CA2007（BmsRulesetStatisticsTest:555）保留。未 publish、打包或创建安装副本。

新增真实密码聚焦 / 回车场景证明未保存连接不能发送凭据、保存本身无 HTTP、保存后只发往新 origin。原查榜场景验证同条件下移除 OMS 后请求确实清除条件、重置页码并使用新来源；详情展开 / 收起、旧 ID / 未知灯、独立灯、私有 owner、取消与迟到响应均保留。

## 真人路径与剩余项

用户从 VS Code 非调试启动[当前客户端工作区](../../AGENTS.md#开发磁盘约束)：右上角原账号入口 → 填地址、启用、保存 → 登录。修改地址但不保存，登录、注册和密码回车应保持不可提交；保存后再登录。原个人页看本人记录，奖杯查同一 BMS 原谱，切单 / 多 / 全 / 空来源及同条件，展开一条历史详情后原位置收起；切账号应保留原账号待上传记录。

实际窗口尺寸、焦点、长名、缩放和两端真实数据仍待用户签收。完整公开历史、网页部署与维护分别取 Website / Backend 的本轮证据；这组软件测试不证明真实播放器交分、原生 UI、真人下载入库或 P/C 完整闭环。
