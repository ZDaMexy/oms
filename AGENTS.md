# AGENTS.md — OMS 协作入口

OMS 是基于 osu!lazer 的 Windows-only 音游客户端：只保留 osu!mania，新增第一类 BMS；离线优先，Phase 3 前 OMS 私有服务与默认 endpoint 冻结。

## 开始工作

1. 读 [当前状态](doc_md/mainline/DEVELOPMENT_STATUS.md)。
2. 读 [当前计划](doc_md/mainline/DEVELOPMENT_PLAN.md)。
3. 从 [子线路由](doc_md/subline/README.md) 进入所属子线，只读该线 `STATUS` 与任务相关 `CONSTRAINTS`。
4. 产品红线在 [OMS_COPILOT.md](doc_md/mainline/OMS_COPILOT.md) 按关键词定位；历史在对应 `CHANGELOG` 按日期/子线搜索。两者勿默认整篇加载。

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
3. 实现最小安全切片。
4. 运行 focused tests；按风险补 full tests 与 Release build。
5. 同步 doc + memory，运行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\CheckDocumentation.ps1` 与 `git diff --check`。
6. 在当前分支提交；不开 PR、不新建分支。`git push` 前必须取得用户确认。

语言使用中文。对依赖测试/契约的“看似问题”先说明取舍，不盲改。

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
