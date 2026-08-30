# OMS 文档入口

`doc_md/` 是项目治理权威；`.Codex/memory/` 只负责经验召回。目标是让协作者在几分钟内找到“现在做什么、为什么、受什么约束”，而不是通读历史。

## 三分钟阅读路径

1. [mainline/DEVELOPMENT_STATUS.md](mainline/DEVELOPMENT_STATUS.md)：当前阶段、活动主线、最新验证和阻塞。
2. [mainline/DEVELOPMENT_PLAN.md](mainline/DEVELOPMENT_PLAN.md)：只确认当前活动 gate、退出条件与后续依赖。
3. 从 [subline/README.md](subline/README.md) 进入所属子线，只读该线 `STATUS` 与任务相关的 `CONSTRAINTS` 小节。
4. 需要产品红线时，在 [mainline/OMS_COPILOT.md](mainline/OMS_COPILOT.md) 按关键词定位；需要历史时，在对应 `CHANGELOG.md` 按日期或子线编号搜索。

默认不要整篇加载 `OMS_COPILOT.md`、mainline `DEVELOPMENT_PLAN.md` 的历史版本或任何大型 `CHANGELOG.md`。

## Agent 与记忆入口

- [AGENTS.md](../AGENTS.md) 是唯一跨 Agent 协作规则源。
- [CLAUDE.md](../CLAUDE.md) 只是 Claude 兼容跳转，不复制规则和状态。
- [.Codex/memory/MEMORY.md](../.Codex/memory/MEMORY.md) 只路由到特定地雷/诊断；单次任务按需读少量文件，不整库加载。
- memory 采用“权威链接 → 稳定合同 → 地雷/诊断 → 未闭合项”，不保存可从 CHANGELOG 查询的逐日实现史或旧测试数字。

## 分层与唯一职责

| 层 | 内容 | 入口 | 是否权威 |
| --- | --- | --- | --- |
| `mainline/` | 全局状态、编排、产品硬约束、全局历史 | [mainline/README.md](mainline/README.md) | 是 |
| `subline/P1-*` | 单一专项的计划、状态、约束和历史 | [subline/README.md](subline/README.md) | 是，限该专项 |
| `other/` | 格式资料、外部审计、制作者/发行说明、恢复证据 | [other/README.md](other/README.md) | 参考；正式结论须回写主线/子线 |
| `mini/` | 与主线无关、可独立关闭的小事项 | [mini/README.md](mini/README.md) | 是，限该事项 |

## 四类文件的边界

| 文件 | 只写什么 | 禁止写什么 |
| --- | --- | --- |
| `DEVELOPMENT_STATUS.md` | 当前事实、当前风险、下一检查点、唯一一条最新验证 | 调查流水账、逐日实现史、多轮旧测试数字 |
| `DEVELOPMENT_PLAN.md` | 未完成工作、依赖顺序、验收条件、冻结项 | 已完成实现细节、提交日志、重复的当前测试数字 |
| `TECHNICAL_CONSTRAINTS.md` | 稳定合同、红线、必须重跑的验证面 | 临时进度、一次性命令输出 |
| `CHANGELOG.md` | 按日期倒序的已确认变化、验证命令与结果 | 当前优先级、仍会变化的状态叙事 |

同一事实只保留一个权威落点：当前状态进 `STATUS`，未来动作进 `PLAN`，不可破坏的语义进 `CONSTRAINTS`，过程与旧数字进 `CHANGELOG`。其它文件只链接，不复制长段落。

## 低噪声预算

- `STATUS` 建议不超过 120 行；开头只允许“最后更新 + 上级入口”，禁止把 changelog 塞进引用块。
- `README` 只做路由和一句话结论，不承载实现详情。
- `PLAN` 的已完成事项只保留一行结果或移出；实现日记必须进入 `CHANGELOG`。
- `CHANGELOG` 可以增长，但只通过 `rg -n "日期|P1-X|关键词"` 定点读取，不作为每次会话上下文。
- 测试数字只在对应 `STATUS` 的最新验证和本次 `CHANGELOG` 各出现一次；旧数字不反复同步。
- 带日期的结论只要不再影响当前决策，就从 `STATUS/PLAN` 删除，历史由 Git 与 `CHANGELOG` 保存。
- 单次执行 prompt、会话 handoff 话术和“下一轮入口”只在聊天交付，不单独落库，也不得成为 `STATUS/PLAN/README/memory` 的权威依赖；可持续复用的事实分别归入四件套，诊断地雷才进入 memory。
- Agent 适配文件不得复制 `AGENTS.md`；memory 单行建议不超过 800 字符，发现状态叙事时改为链接权威 STATUS。
- 仓库内 Markdown 链接必须按文件所在目录使用标准相对路径；不依赖“从仓库根再猜一次”的非标准回退。
- 生产/用户数据精确 hash、mtime、byte size、会话 ID 与可识别个人/机器的 home 路径不得进入文档；通用路径示例和公开制品 checksum 可以保留但须明确语境，其它本机取证路径改用脱敏占位符或仓库外 authority。

## 联动规则

1. 开工前先归属到一个主线、子线或 mini；跨线时指定一个主归属，其余只链接。
2. 改动改变状态、计划、约束或验证结论时，同次更新对应文件。
3. 子线变化影响全局优先级、release gate 或产品红线时，只向 mainline 回写一条摘要和链接。
4. `other/` 的结论升级为正式决策时，必须进入对应 `PLAN/STATUS/CONSTRAINTS`。
5. 新踩坑同步到 `.Codex/memory/`；memory 与文档冲突时以当前代码、测试和 `doc_md` 为准。
6. 完成前运行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\CheckDocumentation.ps1` 与 `git diff --check`；脚本兼容 Windows PowerShell 5.1，会检查标准相对链接、四件套/索引完整性、STATUS/README 预算、memory wiki 链、隐私残片和会话级 PLAN 污染；模糊数字/路径只告警复核，不强迫删除合法技术信息。

## 当前特殊入口

- 皮肤恢复权威：[other/SKIN_SYSTEM_RECOVERY_20260710.md](other/SKIN_SYSTEM_RECOVERY_20260710.md)
- 皮肤专项当前态：[subline/P1-A/DEVELOPMENT_STATUS.md](subline/P1-A/DEVELOPMENT_STATUS.md)
- 跨会话记忆索引：[../.Codex/memory/MEMORY.md](../.Codex/memory/MEMORY.md)
