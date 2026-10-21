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

- `ClockBackedTestWorkingBeatmap.TrackVirtualManual`挂在真实AudioManager时，即使Rate=0，音频Update仍会读改写`accumulated`。Update线程直接Seek可被该旧值覆写，使一次跳时后的整个主时钟停在0；`SeekAsync`也是同步包装。测试先捕获source/offset，再经公开`GameHost.AudioThread.Scheduler`串行跳时，保留真实`OnSeek`区别与业务断言；`IAudioChannel.EnqueueAction`是framework内部成员，不可外调。日志未含source快照时，不宣称已唯一重放历史交错。见[P1-A取证](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-20选曲与皮肤遗留软件问题收尾)。

- `osu.Desktop.slnf` 包含 BMS/mania tests，core `osu.Game.Tests` 须单独编译；只有相同项目/配置的当前代码成功编译后才能用 `--no-build`。
- `osu.Game` 是 library，却因引用 NUnit test scene 被 C# Dev Kit 误认成 test project，导致缺 runtimeconfig、AutoMapper/测试平台程序集。使用真实 core/BMS/mania 测试工程，不为这个红节点复制依赖或改 library 身份。
- 第三方程序集可通过 deps/runtimeconfig 从 NuGet cache 加载，输出目录没有单独 DLL 不足以证明依赖缺失。
- 缺Test SDK依赖时，`dotnet test --no-restore`可能未发现/执行测试却退出0；须确认实际执行摘要及TRX，修复对应工程依赖后重新编译。具名案例见[TOTAL报告](../../doc_md/other/BMS_TOTAL_RULES_AUDIT_20260922.md#验证状态)。
- 源文件存在也不代表 fixture 已编译。旧 `TestSceneGameplaySampleTriggerSource` 依赖已移除的 OsuRuleset，被工程排除；对它的过滤无匹配不算空击验证。2026-09-30 改用真实转谱 Player 和当前容器用例，具体覆盖与过滤器见[性能记录](../../doc_md/other/GAMEPLAY_PERFORMANCE_20260930.md)。
- `FullyQualifiedName~Skin`是子串匹配，会包含BackgroundDataStoreProcessorTests的旧skin处理方法。其全局LoadQuickOsz仅导入3张mania却仍断言12张，载入异常被headless容许后宿主可能持续空转；日志停止增长但CPU继续消耗不能当作仍在推进测试。先保存具体fixture/日志、核对进程身份后停止本次宿主，按真实改动合同选择明确范围；扩大检查记为中止，不恢复已移除玩法或将focused通过冒充全Core通过。见[P1-A本轮失败身份](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-22autoplay跳转与omni反馈修复)。
- 真实桌面 exact-test 的 game 隔离根不代表 framework host storage/cache 也跟随 TEMP；默认 host 仍用 AppData，改 APPDATA 环境变量不会改变已解析的 Windows special folder。受非系统盘开发约束时，可在已校验的 `.dev-cache/temp/` 独立输出中复用 ExactVisualTestGame，并用框架 PortableInstallation 使 host 根位于 AppContext.BaseDirectory；不能指向正式客户端输出或用户数据。保存证据后只清理已确认的探针目录。示例见 [性能验证记录](../../doc_md/other/GAMEPLAY_PERFORMANCE_20260930.md)。
- ExactVisualTestGame/TestOsuGame 不具备 OsuGameDesktop 缓存的 IHighPerformanceSessionManager。原生宿主会绘图、Player 能完整结束，不代表实际桌面的 GC 会话条件一致；应由真实 PlayerLoader 解析生产服务并记录 IsSessionActive/LatencyMode，不能手工切 GC 模式后冒充产品路径。Window.Raise 也不保证取得 Windows 前台；以逐帧 active 证据区分后台限帧与前台样本，始终后台的整曲只证明运行/结果，不证明前台流畅度。具名误判与保留原样本见[判定与性能深审](../../doc_md/other/GAMEPLAY_PERFORMANCE_JUDGEMENT_20261019.md#实机采样方法)。
- UntilStepButton有固定10秒上限，调大TestScene.TimePerAction不会放宽大型作者包导入期限。opt-in取景可在有120秒外层watchdog的准备step完成导入，再展示设置；不能超时后Dispose尚在写入的Realm。普通ImportTask成功会消费来源归档，取景须先复制可删除输入，不能直接交付作者OSK原件。来源摘要及失败/成功证据见[键数设置记录](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-19按键数皮肤设置与素材预览)。
- 临时目录日志会被清理；复核时分别标明可回读TRX、历史执行记录和新运行证据。比较既有失败需逐项核对名称、错误与堆栈，不能仅靠失败总数。
- 临时替换旧源码做 before 时，Copy-Item 会保留备份 mtime；若早于已有 DLL，增量 build 可继续复用 after 程序集。替换与恢复都显式更新时间或强制重编译；关键红绿可读取实际输出 DLL 的 PE type/field metadata 验证版本。一次 build exit 0 不证明替换的旧源码已编译，具名误判见[游玩审查](../../doc_md/other/GAMEPLAY_REVIEW_20261019.md)。
- `UseFreshStoragePerRun` 不表示每个测试方法都重建 Realm/目录：固定 framework 复用同一个 scene，依赖创建时才分配根。保存的组合键或未收尾的目录变更会污染后例；失败的视觉步骤又会跳过后置 `TearDownSteps`。随机选择还可能抽到前例故意损坏 owner／缺 ini 的记录，产品正确拒绝而测试无条件等提交；先核候选身份，再局部准备本例完整允许集合，不能因偶发通过就排除该分支。输入审查留有[确定两例红绿](../../doc_md/other/INPUT_SIGNAL_REVIEW_20261021.md#失败定位与有效修后证明)。需明确每例重置与真正兜底责任，不抹 journal 或放宽生产目录校验；其它探针边界见上述游玩审查。
- 固定framework的`UseTestSceneRunner.AfterTest`在NUnit单例`TearDown`之后才执行视觉步骤；单例收尾只能清前例，`RunAfterTest`也可能被失败跳过。末例应在派生fixture的`OneTimeTearDown`中、基类关闭host之前调度Update收尾，并有界等待/传播异常。`Clear(true)`异步Dispose，若join前必须释放holder/lease，使用现有`Remove(child, disposeImmediately:true)`逐个同步释放；不要为夹具改通用runner。具名来源与验证见上述游玩审查。
- framework未加载的Drawable会忽略普通失效传播；直接new Sprite后改四角再读缓存包围盒，不能证明实际裁切失效。几何/绘制边界回归需把组件真实加载到headless场景，先读取初帧再变更并更新，核对屏幕四角和裁切范围；原始失败及修正见[IIDX33记录](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-18iidx33-游玩皮肤与公共表现能力)。
- 先前 relevant 范围未覆盖的方法，不能只凭修改日期认定旧失败。`TestRegisterExternalDirectoryWithOnlyNonManiaBeatmapsReturnsNull` 的旧 null 预期与 InvalidDataException 合同冲突，2026-09-30 full 发现后，以恢复原生产 HEAD 并重新编译的具名单项确认基线，再逐字节恢复优化；过程见上述性能记录。该旧名称已退休，现行回归为 `ManiaImportIntegrationTest.TestRegisterExternalDirectoryWithOnlyNonManiaBeatmapsReportsFailure`，验证明确失败、零记录及源文件保全，来源见 [P1-H 扫描记录](../../doc_md/subline/P1-H/CHANGELOG.md#扫描性能与便携日志定位)。复制旧过滤器无匹配不能算通过。

- 隔离DesktopGameHost / ExactVisualTestGame的PNG属于测试资料渲染；设置overlay.RelativeSizeAxes / Width只改变覆盖层内容区，不改变宿主窗口 / DPI。先核尺寸作用对象，不能由“narrow”文件名或实际像素截图推定玩家缩窗和设计已验。正常OMS窗口的lazer体验仍须实际使用反馈，具名误称与范围见[来源勘误](../../../oms-server/oms_client_bridge_md/doc_md/other/oms-beatmap-browser-mode-snapshot-20261012.md#证据范围勘误)。

## formatter 与并发误判

- 本机 `dotnet format --include` 传绝对路径曾成功退出但 report 为 0，漏掉实际 `ENDOFLINE`；从仓库根传 `git diff/ls-files` 的相对路径后才真正命中。检查 report 与已知改动，不以退出码单独证明 include 范围已执行。
- 正常 format 修复过一次也不等于最终 `--verify-no-changes` 通过；C6 最终检查仍检出内部 static readonly 字段与新增 private overload 的 IDE1006。按实际符号修正并重新编译，保留 public overload 名称；不要加 suppression 或把首次 exit 0 当作最终格式证据。
- `dotnet format --include` 曾对新/未跟踪测试误报 `IDE0005`，删去实际使用的 `System.Collections.Generic` / `System.Reflection` 后，`HashSet<>` / `BindingFlags` 编译失败。先核对符号，再以 owning csproj 编译裁决，不能以 formatter 摘要替代编译器。
- solution-level whitespace verify 曾对新测试漏报 `ENDOFLINE`，也曾出现聚合噪声。新/未跟踪文件按 owning csproj 定点校验，修改后重新编译和暂存；`git diff --cached --check` 核对最终字节。
- 共享工程并发 build/test 会争用 `obj`/输出，触发 `CS2012`、`MSB3026`。统一调度或真正隔离输出；冲突那次不算 gate，不靠循环重试掩盖。

## 检查脚本与环境

- `osu.Game` 内新增 `Resources/Textures/` 资源时，`DllResourceStore` 只剥程序集名称，仍保留 `Resources/`。直接加 store 后按 `Textures/` 请求会返回 null；应通过 `NamespacedResourceStore` 接入该目录。控件 `IsLoaded` 和旧启动测试可在缺图时通过，须核实际绘制尺寸或截图。`LogoAnimation` 的 R/G 分别是时间/覆盖率，不能换成普通 RGBA 图；其高亮先于填充绘制，外侧描边才能保留。资源合同见 [品牌资源](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#客户端品牌资源)，失败与修正来源见 [品牌日志](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-13客户端品牌标识专项)。

- Pylance提示大量源文件时，先按目录枚举`.py/.pyi`，缓存 / 验证产物可能含成千依赖文件。工作区`python.analysis.exclude`排除目录扫描，打开或被实际源码导入的文件仍可分析；`ignore`只静音诊断，不能替代排除。区分目录数量、配置验证与重新加载后的实际编辑器结果；本机实例和采用范围取[2026-10-10日志](../../doc_md/mainline/CHANGELOG.md#2026-10-10)。
- 仓库在非系统盘不代表开发零占用系统盘：旧工作副本有独立 bin/obj，默认 NuGet/TEMP 与 Codex 聊天也分开存放。每个新 shell 按 [开发磁盘约束](../../AGENTS.md#开发磁盘约束)加载 `UseDevelopmentStorage.ps1`；进程环境不跨工具调用保留，旧 assets 的绝对 packageFolders 也不会随环境变量自动重写，切换后先 restore。运行数据和验收资料不能因位于 bin/artifacts/AppData 就认定为垃圾；清理被策略拦截时不能记为已释放。
- 清理构建输出前须查看真实内容：Desktop的`bin/.../data`可能含正在使用的Realm、谱库和配置，应保护整个运行目录；Game的`obj/canonical`还可能残留退役皮肤包，先按内容摘要确认独立保留副本，不能仅凭`bin/obj`名或忽略规则删除。清除`obj`后需正常restore / build；容量复核区分文件逻辑大小与磁盘可用空间差值。实例和边界取[清理日志](../../doc_md/mainline/CHANGELOG.md#确认后清理构建输出)。
- JavaScript 字符串调用 PowerShell regex 有两层转义；普通 template literal 可能吞掉 `\[`，`String.raw` 仍会插值 `${...}`。优先运行已审查脚本，不能把带错误的 `BROKEN 0` 当作结果。
- `rg --files` 默认不包含 hidden memory；枚举要包括 `.Codex` 并排除 `.git`。根目录 Markdown 的 parent 也须正确解析。链接以文件所在目录为准，不从仓库根再猜一次。
- `dotnet --info` 曾在 workload `InstallerBase` 初始化失败，但同环境实际 Release build/test 可运行。诊断命令失败不等于产品构建失败，不据此修改 SDK、安装 workload 或加 runtime fallback。
- 不恢复全局 `NoWarn` 隐藏依赖告警；失败按具体测试身份、消息和原因归类，不只对比数量。
- VS Code 的“无调试运行”不是构建配置名；以实际 launch/task 与用户构建输出区分 Debug/Release，不能由保存目录推断程序位置。NU1902 在 restore/build 重复显示不是多份独立漏洞；保留 advisory 身份，修补真实依赖，不屏蔽类别。
- restore 成功或不再发告警不等于完整依赖审计清洁。还需按范围核 `dotnet list ... package --vulnerable --include-transitive`，保留具名例外和传递依赖实情；实际修复版本、边界与兼容检查见 [C7 启动反馈](../../doc_md/other/SKIN_SYSTEM_C7_VALIDATION_20260909.md#交付后默认皮肤提示与构建警告修复)。
