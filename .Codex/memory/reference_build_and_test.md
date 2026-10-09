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
- 缺Test SDK依赖时，`dotnet test --no-restore`可能未发现/执行测试却退出0；须确认实际执行摘要及TRX，修复对应工程依赖后重新编译。具名案例见[TOTAL报告](../../doc_md/other/BMS_TOTAL_RULES_AUDIT_20260922.md#验证状态)。
- 源文件存在也不代表 fixture 已编译。旧 `TestSceneGameplaySampleTriggerSource` 依赖已移除的 OsuRuleset，被工程排除；对它的过滤无匹配不算空击验证。2026-09-30 改用真实转谱 Player 和当前容器用例，具体覆盖与过滤器见[性能记录](../../doc_md/other/GAMEPLAY_PERFORMANCE_20260930.md)。
- 真实桌面 exact-test 的 game 隔离根不代表 framework host storage/cache 也跟随 TEMP；默认 host 仍用 AppData，改 APPDATA 环境变量不会改变已解析的 Windows special folder。受非系统盘开发约束时，可在已校验的 `.dev-cache/temp/` 独立输出中复用 ExactVisualTestGame，并用框架 PortableInstallation 使 host 根位于 AppContext.BaseDirectory；不能指向正式客户端输出或用户数据。保存证据后只清理已确认的探针目录。示例见 [性能验证记录](../../doc_md/other/GAMEPLAY_PERFORMANCE_20260930.md)。
- 临时目录日志会被清理；复核时分别标明可回读TRX、历史执行记录和新运行证据。比较既有失败需逐项核对名称、错误与堆栈，不能仅靠失败总数。
- 先前 relevant 范围未覆盖的方法，不能只凭修改日期认定旧失败。`TestRegisterExternalDirectoryWithOnlyNonManiaBeatmapsReturnsNull` 的旧 null 预期与当前 InvalidDataException 合同冲突，2026-09-30 full 发现后，以保全优化文件、恢复原生产 HEAD 并重新编译的具名单项确认基线，再逐字节恢复优化；两轮范围差异与旧产物不能冒充基线证据，过程见上述性能记录。

## formatter 与并发误判

- 本机 `dotnet format --include` 传绝对路径曾成功退出但 report 为 0，漏掉实际 `ENDOFLINE`；从仓库根传 `git diff/ls-files` 的相对路径后才真正命中。检查 report 与已知改动，不以退出码单独证明 include 范围已执行。
- 正常 format 修复过一次也不等于最终 `--verify-no-changes` 通过；C6 最终检查仍检出内部 static readonly 字段与新增 private overload 的 IDE1006。按实际符号修正并重新编译，保留 public overload 名称；不要加 suppression 或把首次 exit 0 当作最终格式证据。
- `dotnet format --include` 曾对新/未跟踪测试误报 `IDE0005`，删去实际使用的 `System.Collections.Generic` / `System.Reflection` 后，`HashSet<>` / `BindingFlags` 编译失败。先核对符号，再以 owning csproj 编译裁决，不能以 formatter 摘要替代编译器。
- solution-level whitespace verify 曾对新测试漏报 `ENDOFLINE`，也曾出现聚合噪声。新/未跟踪文件按 owning csproj 定点校验，修改后重新编译和暂存；`git diff --cached --check` 核对最终字节。
- 共享工程并发 build/test 会争用 `obj`/输出，触发 `CS2012`、`MSB3026`。统一调度或真正隔离输出；冲突那次不算 gate，不靠循环重试掩盖。

## 检查脚本与环境

- Pylance提示大量源文件时，先按目录枚举`.py/.pyi`，缓存 / 验证产物可能含成千依赖文件。工作区`python.analysis.exclude`排除目录扫描，打开或被实际源码导入的文件仍可分析；`ignore`只静音诊断，不能替代排除。区分目录数量、配置验证与重新加载后的实际编辑器结果；本机实例和采用范围取[2026-10-10日志](../../doc_md/mainline/CHANGELOG.md#2026-10-10)。
- 仓库在非系统盘不代表开发零占用系统盘：旧工作副本有独立 bin/obj，默认 NuGet/TEMP 与 Codex 聊天也分开存放。每个新 shell 按 [开发磁盘约束](../../AGENTS.md#开发磁盘约束)加载 `UseDevelopmentStorage.ps1`；进程环境不跨工具调用保留，旧 assets 的绝对 packageFolders 也不会随环境变量自动重写，切换后先 restore。运行数据和验收资料不能因位于 bin/artifacts/AppData 就认定为垃圾；清理被策略拦截时不能记为已释放。
- 清理构建输出前须查看真实内容：Desktop的`bin/.../data`可能含正在使用的Realm、谱库和配置，应保护整个运行目录；Game的`obj/canonical`还可能残留退役皮肤包，先按内容摘要确认独立保留副本，不能仅凭`bin/obj`名或忽略规则删除。清除`obj`后需正常restore / build；容量复核区分文件逻辑大小与磁盘可用空间差值。实例和边界取[清理日志](../../doc_md/mainline/CHANGELOG.md#确认后清理构建输出)。
- JavaScript 字符串调用 PowerShell regex 有两层转义；普通 template literal 可能吞掉 `\[`，`String.raw` 仍会插值 `${...}`。优先运行已审查脚本，不能把带错误的 `BROKEN 0` 当作结果。
- `rg --files` 默认不包含 hidden memory；枚举要包括 `.Codex` 并排除 `.git`。根目录 Markdown 的 parent 也须正确解析。链接以文件所在目录为准，不从仓库根再猜一次。
- `dotnet --info` 曾在 workload `InstallerBase` 初始化失败，但同环境实际 Release build/test 可运行。诊断命令失败不等于产品构建失败，不据此修改 SDK、安装 workload 或加 runtime fallback。
- 不恢复全局 `NoWarn` 隐藏依赖告警；失败按具体测试身份、消息和原因归类，不只对比数量。
- VS Code 的“无调试运行”不是构建配置名；以实际 launch/task 与用户构建输出区分 Debug/Release，不能由保存目录推断程序位置。NU1902 在 restore/build 重复显示不是多份独立漏洞；保留 advisory 身份，修补真实依赖，不屏蔽类别。
- restore 成功或不再发告警不等于完整依赖审计清洁。还需按范围核 `dotnet list ... package --vulnerable --include-transitive`，保留具名例外和传递依赖实情；实际修复版本、边界与兼容检查见 [C7 启动反馈](../../doc_md/other/SKIN_SYSTEM_C7_VALIDATION_20260909.md#交付后默认皮肤提示与构建警告修复)。
