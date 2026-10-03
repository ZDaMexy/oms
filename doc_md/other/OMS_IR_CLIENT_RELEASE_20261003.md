# OMS IR 客户端与生产试运行记录

## 玩家入口与验收范围

用户于 2026-10-03 授权持续实现、commit / push 与直接部署，由用户在真实环境验收。客户端工具栏奖杯打开独立 IR 面板；服务地址填 `https://oms.zdamexy.work`，启用并登录后，正常完成且真正保存的新局才交分。网页入口为 `https://oms.zdamexy.work/ir/`；端内 / 网页同一账号读取相同服务记录。客户端默认地址空、IR 关闭，旧 `OnlineFeaturesEnabled=false` 不变。

首轮公开试验榜为 client-reported、未经回放核验；不上传回放或谱包。BMS 最佳分与最佳灯可来自不同局，辅助玩法分组。不支持的 Mod / 参数明确阻塞并保全待交；客户端不上传未实际保存的自动演示、回放、历史导入、失败 / 中退手动保存。正常完成但最终 FAILED 点灯的 BMS 可提交，IR passed 按灯投射，不更改本地完成状态。

## 客户端实现与有效验证

新局捕捉原谱 MD5 / SHA256 和实际键型；最终 ruleset 准备之后，本地 ImportScore 成功、返回实际 UUID / Hash 才排入待交。网络处理独立于本地保存，原服务 / 原账号 / UUID 不随账号切换改变。令牌只放绑定保存根与服务 origin 的 Windows 凭据库；设置 / 每局待交原子保存到当前用户根 `oms-ir/`，异常原件保留。

Release 验证均先 `. .\UseDevelopmentStorage.ps1`，开发缓存 / 临时 / 日志均在 F 盘。命令及当前有效结果：

```powershell
dotnet test osu.Game.Tests\osu.Game.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~OmsIr' --logger 'trx;LogFileName=client-focused-run6.trx' --results-directory artifacts\oms-ir-release-20261003
dotnet test osu.Game.Rulesets.Bms.Tests\osu.Game.Rulesets.Bms.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~BmsOmsIrSubmissionTest|FullyQualifiedName~BmsClearLampProcessor|FullyQualifiedName~BmsScoreProcessor' --logger 'trx;LogFileName=bms-focused-run2.trx' --results-directory artifacts\oms-ir-release-20261003
dotnet build osu.Desktop.slnf -c Release --no-restore -p:GenerateFullPaths=true -m -verbosity:m
```

core IR 43 通过、BMS relevant 101 通过、Desktop Release 编译成功。真实 Player / ScoreManager / Realm 合成 4K 场景证明保存前没有待交或 HTTP、保存后发送实际 UUID、关闭 IR 仍保存、中局登录不认领匿名成绩。面板行为覆盖按需查询、本人历史、退出清空和错误响应；服务覆盖真实 Windows 凭据 roundtrip、重启恢复、账号切换、响应 / 响应体丢失、刷新旋转、401 停止、永久拒绝保全与同局不同内容拒绝。BMS native recorder / 最终准备验证 v7、灯 / 血条、EMPTY POOR 和 keymode；两份 Create 合成导出不是真实玩家记录。

NU1900 保留：NuGet 漏洞信息源不可达，未完成最新依赖安全审计。BMS 编译保留既有 CS8600 / CA2007 具名警告。没有因 IR 改动重跑与本轮无关的整仓旧 fixture，也不宣称整仓全绿。

## 首轮失败与修复

core run1 为新 UI fixture 编译问题；run2 暴露 InvalidDataException 不属于 IOException 的错误恢复遗漏，明确停 IR 并保全异常设置 / 队列；run3 更新同 ID 不同内容的友好错误预期，run4 39 通过。新增真实 Player fixture 的 run5 错用不存在的 OsuSetting.OnlineFeaturesEnabled，删除该行后 run6 43 通过；没有修改硬 false 旧开关。

BMS 合成 payload 起初被服务拒绝：本地完成整曲的 Passed=true 与最终 FAILED 灯不同。按原生最终 clear_lamp >=2 投射 IR passed，保持本地状态；对应 native regression 和公网未修改导出均通过。保存上下文必须取原 BeatmapInfo，BMS converter 的新可玩 metadata 不能提供原始 hash。

## 公网与容量

生产 Backend `52d3174d1def15839acbe6126ad0bf56baee5027`、Website `d29ac644dc775eb20b5daaabc74cf6d62d927280`，发布包 SHA256 与本地制品一致，精确制品指纹留在长期验收证据。真实主机独立 Python 3.12.14 / SQLite 3.53.1，单 worker、loopback 8081、MemoryMax 500 MiB；只新增 OMS BT extension 的 IR include，原网站根、证书和个人主页未改。

十万条合成历史、50 账号、热门谱五万条，50 局 / 10 秒 +25 次读榜 / 秒：交分 p95 17.844 ms、读榜 p95 33.833 ms、RSS 峰值 130.89 MiB、无 swap，均真正确认。首轮慢查询失败保留，修复后有效结果和两个新空目录恢复见 Backend `doc_md/other/ir4-host-verification-20261003.md`。共出口登录配额为 60 次尝试 / 分钟，50 人集中登录按 Retry-After 全部成功约 66.29 秒；注册 20 人 / 小时 / 出口、读榜 600 次 / 分钟 / 出口，不把独立 IP 压测当作任意 NAT 均无限制。

2026-10-03 11:19 UTC 发布，私有健康通过、服务无重启、日备份 timer 已启动；Nginx 重载后首次公开检查 404，后续重新检查 IR / API 与两站均 200，不把首次失败记为成功。HTTP→HTTPS 301，TLS 验证开启。公网真实 HTTP 接收两份未改的 C# 导出，同局第二次 200 / 不新增、本人两条历史 / 他人 403、公共榜对应局、桌面刷新旋转 / 退出撤销、网页 Secure / HttpOnly / Strict cookie、错误 / 缺少 Origin 拒绝和 64 KiB JSON 413 均通过。公开两谱明确标为“合成契约样例（非玩家成绩）”；没有将容量库或测试账号迁入生产。

生产维护、备份、账号人工恢复、日志及回退只沿 Backend `deploy/README.md`，共享设施修改同步两站。备份离机与真实浏览器最终检查在实际执行后补录。

## 候选包与保留门

Windows 完整候选包及冷启动检查待当前源码提交后执行官方 `build-release.ps1`，结果后续补录；不向客户端填入默认地址。长期证据位于 `artifacts/oms-ir-release-20261003/`，包含 TRX、编译日志、合成 payload、公网 probe、容量失败 / 复测、恢复 / NAT 和生产配置操作；凭据不进入证据或 Git。

真实玩家仍需分别完整手动游玩 BMS / mania，核对本地、端内、网页同一局；断网完成、重启补交、原账号恢复、关闭 IR 和账号切换需实际验收。DPI / 长谱名、portable / 自定义根及覆盖更新保留。此前 Phase 1.x 皮肤、输入设备、听感和整体公开发行门没有被此候选或自动场景关闭。
