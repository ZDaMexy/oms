# OMS 文档入口

`doc_md/` 保存项目状态与合同，`.Codex/memory/` 保存诊断经验。日常只读当前入口和所属子线，历史按需查找。

## 三分钟阅读路径

先按 [AGENTS.md](../AGENTS.md#开始工作)核对 Git 基线，再读：

1. [主线 STATUS](mainline/DEVELOPMENT_STATUS.md)：当前阶段、阻塞和最新验证。
2. [主线 PLAN](mainline/DEVELOPMENT_PLAN.md)：当前 gate、顺序与验收条件。
3. [子线路由](subline/README.md)：进入所属 STATUS 和相关 CONSTRAINTS。
4. 按需检索 [OMS_COPILOT](mainline/OMS_COPILOT.md) 的产品红线、所属 CHANGELOG 的日期/关键词，或 [memory 索引](../.Codex/memory/MEMORY.md) 的诊断主题。

[AGENTS](../AGENTS.md) 是唯一协作规则源，[CLAUDE](../CLAUDE.md) 只作兼容跳转。不要默认加载整篇大合同、历史版本或 CHANGELOG。

## 分层与唯一职责

| 层 | 保存内容 | 入口 |
| --- | --- | --- |
| mainline | 全局编排、跨线风险、产品硬约束及历史 | [主线入口](mainline/README.md) |
| subline | 一个专项的事实、计划、合同与历史 | [P1-A～M](subline/README.md) |
| other | 作者/发行说明、格式参考、带日期的审查和恢复证据 | [参考索引](other/README.md) |
| mini | 没有现成归属、需独立跟踪的事项 | [mini 入口](mini/README.md) |
| memory | 难以从代码直接看出的踩坑、复现条件和诊断方法 | [记忆索引](../.Codex/memory/MEMORY.md) |

| 文件 | 唯一落点 | 不保存 |
| --- | --- | --- |
| STATUS | 当前事实、风险、下一门、最新验证 | 调查流水账和多轮测试数字 |
| PLAN | 未完成工作、依赖、验收、冻结项 | 已完成实现细节、提交日志 |
| CONSTRAINTS | 稳定行为合同和不可破坏的边界 | 临时进度、末尾覆盖前文的补丁条款 |
| CHANGELOG | 按日期倒序的已确认变化、命令与结果 | 活动优先级 |
| memory | 权威链接 + 独有地雷/诊断 | 合同全文、字段大全、当前燃尽和逐日实现史 |

同一事实只维护一个权威落点，其它层摘要并链接。当前状态以 owning STATUS 为准；带日期的报告说明当时验证了什么，不自动变成最新状态。外部参考形成正式决定时，回写 owning PLAN/CONSTRAINTS。

## 低噪声预算

- STATUS 最多 120 行，建议不超过 6000 字符；PLAN 建议不超过 180 行、10000 字符。两者单行超过 800 字符时复核是否塞入重复段落；不靠断行或合并长行凑预算。
- 产品验证只写在唯一 `## 最近一次验证`；纯文档结果可另设一个 `## 文档治理验证`。两章不设轮次子标题；没有产品验证时可省略产品章，治理不得刷新产品/实机日期。
- 测试数字集中于 owning STATUS 最新验证和当次 CHANGELOG；主线只摘要、链接。跨线审查报告可保存本次矩阵，避免向每条子线再复制整表。
- 治理 README 最多 80 行，只做路由；memory 单行最多 800 字符。记忆增长时先检查是否重复合同/其它叶子，不凭体积拆成更多文件。
- CHANGELOG 允许增长，通过 `rg -n "日期|P1-X|关键词"` 查找。旧决定和不再影响当前工作的日期化事实退出 STATUS/PLAN，不为减少体积抹除恢复证据。
- 合同就地更新，保留具体输入边界、失败条件和理由；不叠加“以后文为准”。标题改名或拆分时同步修正链接及片段锚点。
- 任务 prompt、会话话术和临时交接不入库。已有主线/子线归属的小修只更新相应文件，不另外生成审计报告或 mini 四件套。
- 仓库内 Markdown 链接使用相对当前文件的标准路径；含空格路径用 `<...>`。当前入口与记忆保留稳定文件名，合并节点须同步全部引用。
- 生产/用户数据精确 hash、mtime、byte size、会话 ID、个人 home 路径不入文档；保留脱敏结论和仓库外证据位置。通用路径示例和明确的公开制品 checksum 可保留。

## 联动规则

1. 先归属主线、子线或 mini；跨线指定一个主归属。只同步本次实际改变的状态、计划、合同和验证结论，不机械刷新全部文件日期。
2. 子线影响全局优先级、release gate 或产品红线时，向 mainline 回写一句摘要和链接；新增地雷写 owning memory，并更新索引描述。
3. 完成前运行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\CheckDocumentation.ps1` 和 `git diff --check`。脚本检查结构、文件/片段链接、索引和约定的隐私残片；本地锚点覆盖普通 ATX 标题、行内 code、重复标题和显式 `<a name/id>`，复杂标题只提示人工核对，不猜结果。通过不证明代码、文档语义或人工验收正确。
4. 修改检查脚本时另运行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\TestCheckDocumentation.ps1`，用临时小仓库验证正反案例；只改文档无需重复跑该脚本或产品测试。

当前入口：[皮肤恢复边界](other/SKIN_SYSTEM_RECOVERY_20260710.md) · [P1-A 状态](subline/P1-A/DEVELOPMENT_STATUS.md) · [构建与验证地雷](../.Codex/memory/reference_build_and_test.md)。
