---
name: reference-build-and-test
description: OMS 构建、formatter、测试宿主和环境误判的诊断召回
metadata:
  node_type: memory
  type: reference
---

# 构建与测试召回

执行入口见 [AGENTS](../../AGENTS.md#并行与验证协调)；范围按[主线验收矩阵](../../doc_md/mainline/DEVELOPMENT_PLAN.md#改动验收矩阵)和 owning 合同。当前结果从主线 STATUS 路由，历史按所属 CHANGELOG 查询。

## 测试宿主与产物

- `osu.Desktop.slnf` 包含 BMS/mania tests，core `osu.Game.Tests` 须单独编译；只有相同项目/配置的当前代码成功编译后才能用 `--no-build`。
- `osu.Game` 是 library，却因引用 NUnit test scene 被 C# Dev Kit 误认成 test project，导致缺 runtimeconfig、AutoMapper/测试平台程序集。使用真实 core/BMS/mania 测试工程，不为这个红节点复制依赖或改 library 身份。
- 第三方程序集可通过 deps/runtimeconfig 从 NuGet cache 加载，输出目录没有单独 DLL 不足以证明依赖缺失。

## formatter 与并发误判

- 本机 `dotnet format --include` 传绝对路径曾成功退出但 report 为 0，漏掉实际 `ENDOFLINE`；从仓库根传 `git diff/ls-files` 的相对路径后才真正命中。检查 report 与已知改动，不以退出码单独证明 include 范围已执行。
- 正常 format 修复过一次也不等于最终 `--verify-no-changes` 通过；C6 最终检查仍检出内部 static readonly 字段与新增 private overload 的 IDE1006。按实际符号修正并重新编译，保留 public overload 名称；不要加 suppression 或把首次 exit 0 当作最终格式证据。
- `dotnet format --include` 曾对新/未跟踪测试误报 `IDE0005`，删去实际使用的 `System.Collections.Generic` / `System.Reflection` 后，`HashSet<>` / `BindingFlags` 编译失败。先核对符号，再以 owning csproj 编译裁决，不能以 formatter 摘要替代编译器。
- solution-level whitespace verify 曾对新测试漏报 `ENDOFLINE`，也曾出现聚合噪声。新/未跟踪文件按 owning csproj 定点校验，修改后重新编译和暂存；`git diff --cached --check` 核对最终字节。
- 共享工程并发 build/test 会争用 `obj`/输出，触发 `CS2012`、`MSB3026`。统一调度或真正隔离输出；冲突那次不算 gate，不靠循环重试掩盖。

## 检查脚本与环境

- JavaScript 字符串调用 PowerShell regex 有两层转义；普通 template literal 可能吞掉 `\[`，`String.raw` 仍会插值 `${...}`。优先运行已审查脚本，不能把带错误的 `BROKEN 0` 当作结果。
- `rg --files` 默认不包含 hidden memory；枚举要包括 `.Codex` 并排除 `.git`。根目录 Markdown 的 parent 也须正确解析。链接以文件所在目录为准，不从仓库根再猜一次。
- `dotnet --info` 曾在 workload `InstallerBase` 初始化失败，但同环境实际 Release build/test 可运行。诊断命令失败不等于产品构建失败，不据此修改 SDK、安装 workload 或加 runtime fallback。
- 不恢复全局 `NoWarn` 隐藏依赖告警；失败按具体测试身份、消息和原因归类，不只对比数量。
