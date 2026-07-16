---
name: reference-build-and-test
description: OMS 构建入口、formatter、并发构建与 C# Dev Kit 误判地雷
metadata:
  node_type: memory
  type: reference
---

# 构建与测试召回

- build 入口：`osu.Desktop.slnf`；CLI `dotnet test <csproj> --no-build -c Release|Debug` 最可靠。
- 第三方程序集通过 deps/runtimeconfig 从 NuGet cache 解析，输出目录没有单独 DLL 不一定是错误。
- 真实测试工程：`osu.Game.Tests`、BMS.Tests、Mania.Tests。

## C# Dev Kit 地雷

`osu.Game` 是 library，但因引用 NUnit 抽象 test scene，Dev Kit 会误识别成测试容器并用缺 runtimeconfig 的 testhost 启动，随后报告 AutoMapper/测试平台程序集缺失。该红节点是已确认 benign：不要通过把 `osu.Game` 变成真实 test project、复制全依赖或移走上游测试基类来修。使用真实测试工程或 CLI。

## targeted formatter 地雷

`dotnet format --include` 对新/未跟踪 test 文件可能给出与真实编译不一致的 `IDE0005` unused-using 建议。不要只凭该 warning 删除 namespace；先看实际符号使用并立刻编译对应 test project。2026-07-14 曾按误报移除 `System.Collections.Generic`，随后 `HashSet<>` 以 `CS0246` 失败；改用已引用 LINQ 的 `ToHashSet()` 后 focused 与 verify 才同时通过。编译器结果高于 formatter 概括 warning。

同日 capability fixture 再次复现：formatter 删除实际用于 `BindingFlags` 的 `System.Reflection`，随后 owning test project 以 4 个 `CS0103` 失败；改用 `System.Reflection.BindingFlags` 全限定名后恢复。不要因为同一误报已见过一次就跳过编译复核。

solution-level `dotnet format osu.sln ... --include <untracked-test>` 还可能漏掉新 test 文件的 owning-project `end_of_line` 规则：2026-07-14 一次 solution verify 返回 0，但随后直接对 `osu.Game.Tests.csproj` verify 才报告同一文件 89 行 `ENDOFLINE`。新/未跟踪文件必须再按所属 csproj 执行 whitespace verify，并在规范化后重新暂存和跑 `git diff --cached --check`；不要把 solution-level 0 当成 staged bytes 已合规。

同日另一个多工程切片出现相反表现：solution-level verify 直接对 LF 新文件报告大量 `ENDOFLINE`，并同时提示一处 `IDE0032`；改为 auto-property、使用 owning project 的 whitespace formatter 规范化后才稳定。两种结果说明 solution 聚合输出既可能漏报也可能集中爆出跨项目噪声；最终 authority 仍是逐 owning csproj 的 targeted whitespace/style verify，再重新编译、检查 staged bytes。

## 并发构建地雷

多个 agent/命令同时 build/test 引用同一工程时会竞争共享 `obj`/输出，出现 `CS2012` 或 `MSB3026` 文件锁；这不是产品回归，但该次结果也不是有效 gate。最终验证必须串行（或使用真正隔离的输出目录）重跑，记录最初锁冲突和权威串行结果，不能靠重试成功掩盖。

## 内联检查脚本转义地雷

- 从 JavaScript host 字符串调用 PowerShell regex 时，普通 template literal 可能先吞掉 `\[` 等反斜杠；`String.raw` 也不会关闭 `${...}` 插值。优先运行仓库内已审查的 checker；必须内联时正确处理两层转义，并在脚本成功退出后才记录结论。
- 普通 `rg --files -g '*.md'` 会忽略 `.Codex` 等 hidden 文件；根目录 Markdown 的 parent 还可能是空串。检查必须包含 hidden 文件、排除 `.git`、把空 parent 规范化为 `.`，并让路径异常 fail-closed，不能输出带错误的伪 `BROKEN 0`。
- 仓库内链接统一写成以当前 Markdown 所在目录为基准的标准相对路径；根目录 checker 会拒绝仓库根链接，不再用回退逻辑掩盖非标准写法。标准入口是 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\CheckDocumentation.ps1`，必须兼容系统自带 Windows PowerShell 5.1。

## 告警纪律

不要恢复全局 `NoWarn` 隐藏依赖告警；安全升级单独治理。当前验证数字只看 mainline STATUS，恢复期历史数字查 [恢复审计](../../doc_md/other/SKIN_SYSTEM_RECOVERY_20260710.md) 或对应 CHANGELOG，不在 memory 重抄。
