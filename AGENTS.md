# AGENTS.md — OMS 协作入口

OMS 是基于 osu!lazer 的 Windows-only 音游客户端：只保留 osu!mania，新增第一类 BMS；离线优先，Phase 3 前 OMS 私有服务与默认 endpoint 冻结。

## 开始工作

1. 先看 `git status --short --branch`、HEAD 与跟踪分支差异，分清本地代码、已记录的远端与在线查询结果。接续开发时先 fetch；工作区干净且仅落后时可 `merge --ff-only`，有未提交改动或分叉时先保全并审查。网络失败须说明基线时效，不能把旧 STATUS 当最新进度。
2. 读 [当前状态](doc_md/mainline/DEVELOPMENT_STATUS.md)。
3. 读 [当前计划](doc_md/mainline/DEVELOPMENT_PLAN.md)。
4. 从 [子线路由](doc_md/subline/README.md) 进入所属子线，只读该线 `STATUS` 与任务相关 `CONSTRAINTS`。
5. 产品红线在 [OMS_COPILOT.md](doc_md/mainline/OMS_COPILOT.md) 按关键词定位；历史在对应 `CHANGELOG` 按日期/子线搜索。两者勿默认整篇加载。

皮肤任务额外先读 [2026-07-10 恢复审计](doc_md/other/SKIN_SYSTEM_RECOVERY_20260710.md)。

## 权威与文档

- `doc_md/mainline`：全局状态、编排、产品约束与历史。
- `doc_md/subline/P1-*`：专项四件套 `PLAN / STATUS / CHANGELOG / TECHNICAL_CONSTRAINTS`。
- `doc_md/other`：参考、审计和面向用户/制作者的派生说明。
- `.Codex/memory`：踩坑与诊断召回，不替代 `doc_md`。

冲突顺序：当前代码/测试/真机反馈 → mainline → subline → other → memory。完整低噪声规则见 [doc_md/README.md](doc_md/README.md)。

改动必须同次同步其改变的状态、计划、约束和验证结论；子线只有影响全局优先级、release gate 或硬约束时才向 mainline 回写一句摘要和链接。新踩坑写入对应 memory，并更新 [MEMORY.md](.Codex/memory/MEMORY.md)。

## 工作流

1. 先归线并确认现有设计合同。
2. 审查/取证；真机日志优先于推测。
3. 实现能闭合真实用户路径的最小切片，遵守下方实现取舍。
4. 按[改动验收矩阵](doc_md/mainline/DEVELOPMENT_PLAN.md#改动验收矩阵)运行 focused tests；按风险与所属子线合同补 full tests、Release 或人工门。通过后只有新改动、失败或未解疑点才扩大/重复验证。
5. 同步 doc + memory，运行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\CheckDocumentation.ps1` 与 `git diff --check`。
6. 在当前分支提交；不开 PR、不新建分支。`git push` 前必须取得用户确认。

语言使用中文。默认所有面向用户的沟通、进度汇报、交付说明和下阶段提示词使用产品语言：先说明玩家和作者能做什么、还有什么未完成、实际体验与验收结果；不使用技术术语，不以代码量、提交数、测试数或阶段比例代替产品进度。只有用户明确要求技术报告时才切换；仓库技术文档与验证记录仍保留准确细节。对依赖测试/契约的“看似问题”先用产品影响说明取舍，不盲改。

用户明确结束本轮或将后续开发留给新对话时，只完成已授权的核对、文档、提交与推送收尾，不自行进入下一开发阶段。

## 实现取舍

- 以当前需求、真实输入边界和可复现故障驱动实现。优先直接表达业务流程，删除本次改动涉及的无用分支；不为假想调用者、未来扩展或“以防万一”新增包装、接口、配置开关、重试和兼容层。合同约束行为与安全边界，不要求保留既有层数或抽象形状。
- 校验集中在不可信输入进入系统的边界；内部消费已经验证的类型和不变量，不逐层重复判空、范围检查或复制同一份状态。跨异步、revision 或 ownership 变化的复核须对应真实竞态，不能把已验证的旧状态当作新状态。
- 程序错误应在测试或明确失败中暴露，不用宽泛 `catch`、静默 `return`、默认值或自动 fallback 把违约伪装成成功。只捕获当前层能按合同处理的异常；取消、资源释放与已定义的可恢复失败保持原有语义。
- 外部文件/作者包、用户数据写入、权限与 sandbox 的必要验证、预算和原子恢复继续保留。判断一个保护是否需要，看它保护的具体边界或失败场景；精简要消除重复和掩错，不削弱真实安全合同。
- 测试证明用户行为、边界和已修复的故障，不围绕实现细节堆断言；为假想分支补出 fixture 本身不构成产品需求证据。审查新增抽象、guard、catch、fallback 时同样检查是否有上述依据。

## 并行与验证协调

- 能独立推进的检索、实现和审查按需委派；写任务明确文件负责人，禁止多个 agent 同时改同一文件。简单任务不为凑并行度拆分。
- 共享工作区的 build/test/formatter 由一个执行者串行调度；检查期间暂停相关源文件写入。只有输出目录真正隔离时才并行构建。主 agent 统一审查、暂存与提交。
- `--no-build` 仅用于对应项目、配置和当前改动已成功编译的产物；formatter 修改代码后也须重新编译。记录命令、配置和失败身份，不能只凭失败数量认定既有基线；锁冲突或旧产物的结果不算有效 gate。
- 已授权范围内持续完成实现、验证、文档与提交，不因普通实现选择反复请求确认。需要产品取舍或缺少不可推断的信息时简短说明；有规则要求暂停时指出具体来源。push 仍按上方约定取得确认。

## 常用工程与命令

主要工程：`osu.Game`、`osu.Game.Rulesets.Mania`、`osu.Game.Rulesets.Bms`、`oms.Input`、`osu.Desktop`。

```powershell
dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m
dotnet run --project osu.Desktop
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore
```

## 红线

- 不重新引入 Osu/Taiko/Catch。
- 不盲目同步上游；按 [UPSTREAM.md](doc_md/other/UPSTREAM.md) 选择性 cherry-pick。
- Phase 3 前 OMS 私有服务与默认 endpoint 为空，不把在线预留描述成当前能力；用户主动添加公共 BMS 难度表 URL 是既有窄例外，不得扩张成 OMS 在线产品面。
- BMS 直读 `chartbms/`，mania 直读 `chartmania/`；不转 `.osz`，不经通用 hash-backed `files/`。
- 发行物不以 osu!lazer 原生默认皮肤作为产品表面；程序化 `OmsSkin` 在 `oms-simple.osk` 通过 parity、完整性、原子恢复与实机 gate 前不得删除，最终产品渲染链必须由只读 canonical 包接管。
- 皮肤异常期归档只能定点取证，禁止整包 cherry-pick/apply。
