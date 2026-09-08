# C6 可选脚本与最终整包验证

本文记录现有 C6 campaign 的实现和验证证据；当前闭门结论以 [P1-A STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md) 为准。C7 canonical 接管、程序化 `OmsSkin` 删除和人工签收不属于本次。

## 基线与选型

本次开始为干净 `master@c4b42d9219d05c1655e268d5e4347e3703a53861`，领先已记录 `origin/master` 三个提交。`git fetch --prune` 因 TLS unexpected EOF 失败；未把旧跟踪引用当在线最新事实，也未回退、合并或推送。

对可抢占性、隔离、Windows 部署、license、GC 与诊断作定点比较后，采用仓库内 MIT 许可的有界数值 bytecode VM。它只处理固定数值 state/heap、引擎 snapshot/event、获准 scene 属性和确定性随机数；不继承旧归档 Lua 或 `.luaskin` 兼容承诺。

| 路线 | 具体取舍 |
| --- | --- |
| MoonSharp | 纯托管、BSD-3，但 auto-yield 对 CLR 重入有约束；独立保留 heap 上限和收窄库仍须自行建立。见 [license](https://github.com/moonsharp-devs/moonsharp/blob/master/LICENSE)、[coroutines](https://www.moonsharp.org/coroutines.html)、[sandbox](https://www.moonsharp.org/sandbox.html)。 |
| Jint | BSD-2、托管 JS、具备执行约束；memory constraint 计算分配量，不能直接等同固定 retained heap，完整 JS 对本次数值 API 成本较高。见 [项目说明](https://github.com/sebastienros/jint)、[memory constraint](https://github.com/sebastienros/jint/blob/main/Jint/Constraints/MemoryLimitConstraint.cs)。 |
| Lua native | allocator/count hook 可实现限额，但带来 native ABI、Windows binary 与封装审计成本。见 [Lua 手册](https://www.lua.org/manual/5.5/manual.html)、[MIT license](https://www.lua.org/license.html)。 |
| 本次数值 VM | 每指令配额/取消、固定 heap、closed host API，Windows 复用现有 .NET 发布，不增加 native/runtime 包。代价是必须在 C6 同时维护版本化语言、compiler/verifier、source map 与 CLI；这些均在本次实现。 |

选型 spike 使用生产源码独立编译运行，环境 Windows 11 build 26200、i9-13900HX（24 核/32 线程）、SDK 8.0.424 / .NET 8.0.30。10 万次 state/event/math/PRNG/heap/scene callback：p50 0.700 μs，p99 1.000 μs，max 124.600 μs；热循环 0 B allocation、Gen0/1/2 均 0。1 万次无限循环在第 4096 条指令熔断：p50 17.700 μs，p99 36.000 μs，max 3.841 ms（包括桌面调度）。2000 个 bytecode 定点变异中 655 个被 typed verifier 拒绝，其余经验证并有界执行。这是 VM 测量，不能代替 renderer、GPU 或低端实机性能结论。

## 产品路径与作者产物

[Momentum 作者源文件与构建步骤](skin-c6-candidate/README.md)生成同包 BMS/mania `.osk`；[完整脚本语言与授权说明](SKIN_SCRIPT_V1_AUTHORING.md)提供无需 DLL 的公开工具链。候选根据最近八次真实 judgement 间隔、gauge 和 gameplay time 组合出旋转/能量衰减效果，基础 note/key/judgement 不依赖脚本。

ordinary import / managed scanner / registered external → `SkinManager` exact revision → shared codec/prepared package → Settings 显式授权 → 双规则集真实 producer/scene host → 数值 overlay → 查询/拒绝/撤销 → 故障恢复，均沿用 C2 owner/participant/work lease/detach/retire。没有测试 publisher 或新的 publication authority。

首次产品红测直接使用 Settings Reload 且没有 gameplay participant：三源坏 scene 均错误提交 B（0/3），暴露原先合成 coherent consumer 掩盖的菜单路径缺口。修复后 menu prepare 同时验证 ini/manifest/scene/script/资源，失败保 exact A；完整证据由下表记录。

## 自动结果

本轮 TRX 与构建日志位于机器 TEMP 的 `oms-c6-tests` 与 `oms-c6-*.log`。失败比较使用既有 `oms-progress-audit-20260909-tests` 的实际 TRX，不按数量归因。

| 验证面 | 本次结果 |
| --- | --- |
| 候选 compiler → encode → verifier | V1/V1/compiler V1/runtime V1；54 instructions、19 state、8 heap、5 targets |
| core GameplaySkin focused | 486/486；最终命名修正后的 core 合并组同一子集亦 486/486，无 skip |
| 三源备份根 G1 | source/CLI bytecode × 三源 6/6，包含在最终 BMS relevant 中，无 skip |
| BMS relevant | 初跑414/414及复审追加atomic-save 2/2；最终full中同一筛选子集416/416，无skip |
| BMS full | 最终命名修正后重新编译，1776/1776，无 skip/hang artifact |
| mania relevant/full | relevant 69/69；full 860/864，无 skip；四项名称、分类与精确消息均与实际基线全同 |
| core Skin/存储并发 | 最终合并 `~Skin|FileStoreTests` 1286/1292（Skin 1275/1281、存储11/11），六项既有失败逐名/分类/精确消息全等，无skip |
| Windows Release | 最终0 error / 18条NU1902：同九条既有MessagePack告警在restore/build重复；BMS重新编译仍仅有既有CS8600/CA2007 |
| 独立终审、formatter、文档与 whitespace | 产品/安全/lifecycle/作者文档独立复审GO；四个owning工程verify-no-changes通过，末次UI文案同样通过；CheckDocumentation通过（143 Markdown、1259相对链接、122本地锚点、105 memory链接），工作区/暂存diff检查通过 |

上述 focused/relevant 运行均编译对应 owning project 的当前源码；最后三处内部命名修正后，core Skin/存储、mania与BMS full均再次编译并实际执行，其focused/relevant子集也全部通过。全量后仅修正授权保存失败的一句提示，避免在旧grant仍有效时误称撤销成功；权限逻辑未改，并重新编译通过实际三源Settings/双host UI测试3/3及Release。未在旧产物上认定结果。共享 build/test/formatter 全由一个执行者串行调度，源码在检查期间冻结。

真实双 host 候选运行另记录 300 次引擎/scene frame 的 update 测量：ordinary 14.163 ms、managed 14.312 ms、external 12.424 ms；每格当前线程 allocation 79,544 B，双方 VM 固定 heap 各 216 B，累计指令 8751/8450。运行环境同上述 Windows 机器，64 位、32 logical CPU。这是生产 producer/host 的 headless CPU 路径，包含场景更新分配，不能将孤立 VM 的 0 B 结论套用到整个 renderer。

## 真实失败与关闭证据

| 故障或合同 | 自动证据与修复边界 |
| --- | --- |
| 无 gameplay participant 时坏 B 被提交 | `BmsCurrentRevisionPreparedPackageProductTest.TestSettingsReloadWithoutGameplayHostRejectsMalformedWholePackageAndKeepsExactA` 经实际 Settings caller 覆盖三源坏 manifest/缺 scene/坏 source/坏 bytecode/坏资源的15格，prepare 失败保 exact A；成功 package receipt 才进入同 C2 publication。ini 同 revision 消费另由选择及 atomic-save 矩阵证明。 |
| 普通 blob 先分配再限额、最终 lease 内较旧选择提交 | 同 fixture 的 `TestOrdinaryAuthorSelectionAndReloadRejectOversizedBlobBeforeAllocatingIt` 与 `TestNewerOrdinaryAuthorRequestAtFinalCoordinatorBoundaryCannotPublishStalePreparedOwner`；前者红测曾在拒绝前分配 67,361,864 B，后者实际提交了旧 generation。改为 bounded stream capture 与 final coordinator lease 内 fresh generation 复核。 |
| 真实作者包与 UI 可达性 | `BmsScriptCandidateUiProductTest` 三源实际导入/选择作者文件，真实 Settings 授权驱动双方 judgement producer 和 renderer；`BmsScriptProductionProductTest` 覆盖拒绝、撤销、fault 与双 host 时钟。不存在 test publisher 或私有 provider 的产品替身。 |
| 未授权仍可通过回调计数读取事件/时钟 | `BmsScriptEventAdmissionProductTest` 红测观察到无 events 权限的 13 次 callback、无 snapshot 权限的 6 次 tick；现在在派发边界分别 gate，scene 写权限不能创建读取通道。 |
| 同时刻 judgement 丢到 tick 之后、暂停无法撤销、迟到 Changed 重置新历史 | 真实 FrameStability/双方 host 测试证明 tick 严格等引擎 high-water 越过时间才封闭；GameHost scheduler 处理暂停撤销，以 atomic effective epoch 去重，ABA 重建而已消费通知不重复重建。 |
| 克隆放大写入、撤销破坏 C5 状态、过早释放 queued texture pixels | `BmsScriptCloneProductTest` 限制 actual fanout 并保留 native pool ownership/C5 当前状态；`GameplaySkinPreparedTextureOwnershipTest` 使用真实 GLTexture 上传队列证明成功交接后的 CPU pixels 生命周期。未以无 GL 的通过声称 GPU 观感通过。 |
| 无限循环、heap/node/resource 超限、异常、取消与 shutdown | `GameplaySkinScriptVmTest` 在 instruction/API 边界抢占、验证 malformed source/bytecode/版本与确定性；真实双 host fault 测试继续玩法并恢复必要视觉。`BmsCurrentRevisionPreparedPackageProductTest` 覆盖六格 compile cancel/shutdown、晚提交与 provisional retire；资源/节点复用 C5 package budgets。 |
| durable grant、即时 revoke 与并发失败 | `GameplaySkinScriptAuthorizationTest` 使用真实文件锁/原子替换失败证明失败 grant 不激活、成功 grant 不被后续失败保存丢弃、排队 revoke 不被早先 grant 短暂复活、pending 文件/目录 restart fail-closed，以及 shutdown 失权并 join。 |
| identity/version/cache | 真实三源重启/Reload、managed rename 与 package/script 内容变化；record ID+整包/VM 指纹绑定权限，source/bytecode/runtime 输入版本严格验证，当前 compiler 版本参与指纹，不存在可跨版本复用的磁盘编译 cache。 |
| 作者原子保存与整包消费 | `TestAuthorAtomicFileReplacementKeepsActiveAUntilSettingsPublishesB` 的 managed/external 两格以真实 sibling temp + `File.Replace` 更新五文件；active 双 host 仍执行 A（15→30），detach 后 Settings Reload 发布同 ID 新 B，ini/manifest/scene/script/资源 bytes/digest 同属 B；未授权基线 7，新授权后双方真实输入产生 37。这是每个文件的作者 atomic save，不声称五文件 filesystem transaction；external 写入仅由测试中的作者动作执行。 |

mania full 四项均为原 `Incorrect number of frames` NUnit assertion：`TestHoldNoteChord`、`TestSingleHoldNote` expected 0 / actual 2，`TestHoldNoteStair` 0 / 4，`TestHoldNoteWithReleasePress` 2 / 3。比较只规范 CRLF/LF 并 trim 首尾，消息内容逐项全等；未删测试、改预期或添加 NoWarn。

core Skin 六项同样与基线逐名/分类/精确消息全等，机器可复核结果保存在 `c6-exact-baseline-comparison.json`：

- `TestRetrieveAndLegacyExportJapaneseFilename`、`TestRetrieveAndNonLegacyExportJapaneseFilename`、`TestRetrievalWithConflictingFilenames`、`TestRetrieveOggAudio`：均为 `AggregateException → ArgumentException`，完整消息为 `System.AggregateException : One or more errors occurred. (No valid beatmap files found in the beatmap archive.)`，下一行 `  ----> System.ArgumentException : No valid beatmap files found in the beatmap archive.`。
- `TestBackgroundCyclingOnDefaultSkin(True)`：`"wait for beatmap background to be loaded" timed out`。
- `TestSampleUpdatedBeforePlaybackWhenNotPresent`：`"ensure sample loaded" timed out: Expected string length 33 but was 18. Strings differ at index 0.`；后续三行依次为 `  Expected: "Gameplay/Argon/normal-sliderslide"`、`  But was:  "normal-sliderslide"`、`  -----------^`。

后两项分别仍为 default-skin background 与 Argon sample 假设导致的 Step timeout；四项 archive fixture 仍没有有效 beatmap。没有新增失败，也没有把失效测试数量当作通过依据。

最终 publication/lifecycle 还复用并重跑既有真实 consumer 矩阵：`BmsCurrentRevisionPublicationProductTest` 的 live prepare 前拒绝、latest-wins、uncooperative worker、observer/cancel/shutdown 与 `BmsCurrentRevisionMutationAtomicityProductTest` 的 fallback/Realm 失败保 A；`BmsGameplayLayoutCurrentRevisionProductTest` 保留 all-keymode、14K opaque/partial stage、custom fallback，mania 真实 lane-cover/shell 等矩阵仍走相同 scene。三源脚本 package 准备加入这些既有 owner/lease，不建立额外协议。

## 复现命令与审查

以下命令从仓库根串行执行。先按[候选步骤](skin-c6-candidate/README.md)构建产物；备份根测试须显式设置 `OMS_C6_BACKUP_ROOT` 为经过只读 hash 验证的 baseline 副本，`OMS_C6_CANDIDATE_OSK` 与 `OMS_C6_CANDIDATE_BYTECODE` 为实际 CLI 构建文件的绝对路径，不可直接指向原用户根。

```powershell
dotnet test osu.Game.Tests/osu.Game.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~GameplaySkin'
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~GameplaySkin|FullyQualifiedName~BmsManagedFolderSelectionProductTest'
dotnet test osu.Game.Rulesets.Mania.Tests/osu.Game.Rulesets.Mania.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~GameplaySkin'
dotnet test osu.Game.Rulesets.Mania.Tests/osu.Game.Rulesets.Mania.Tests.csproj --no-restore --no-build -c Release
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore --no-build -c Release --blame-hang --blame-hang-timeout 5m
dotnet test osu.Game.Tests/osu.Game.Tests.csproj --no-restore --no-build -c Release --filter 'FullyQualifiedName~Skin'
dotnet test osu.Game.Tests/osu.Game.Tests.csproj --no-restore --no-build -c Release --filter 'FullyQualifiedName~FileStoreTests'
dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m
```

实际运行另外保存了独立 TRX/log；最终命名修正后的文件为 `c6-core-final-format.trx`、`c6-mania-final-format.trx`、`c6-bms-final-format.trx`。复审追加两格atomic-save后也曾重新编译执行，再以最终full覆盖。formatter 的 `--include` 使用仓库相对路径；绝对路径曾静默匹配零文件，不能把该次 exit 0 计为格式门。首次format后仍检出三处IDE1006，修正后四个owning工程的`--verify-no-changes`全部通过。

独立复审分别覆盖 VM/授权存储及安全、公开作者文档/API与真实 host、全包 publication/G1/lifecycle。发现的回调权限、并发授权、暂停撤销、迟到通知、纹理交接、原子保存证据及版本文案问题均在同一 C6 修正；源码范围最终 GO，未留下需要作者语言兼容或授权产品策略决策的未决项。没有从旧归档恢复 Lua 实现。

最终使用仓库原七campaign预算关闭C6；当前分支提交代码、作者源文件、文档与诊断记忆，不新建分支/PR，也不push。C7的canonical接管和程序化`OmsSkin`删除未实施。

## 数据根与人工边界

实际用户根通过字节复制建立只读 baseline，25 文件、175,303,768 B，包含 `client.realm`、既存 `corrupt.realm`、ini/json/marker、空 `chartskin` 及全部 18 个 blob（含无记录 orphan）。原文件 before/after 与 backup SHA256 逐项一致；未用 Realm SDK 打开原根。私有路径和逐文件证据仅保留在 TEMP，不进入仓库文档。

G1 测试从 baseline 再复制独立 working root，只在副本打开 Realm/manager，验证真实候选导入/三源选择、授权、双 host、重启、Reload、managed rename/delete、external unregister，以及原 metadata/blob 和 baseline 不变。source 与 CLI 实际生成的 bytecode 各走三源，共六格均实际通过；live scripted host 附着时 mutation 拒绝，逐 host detach 后才能 fallback/delete/unregister。该备份的原非 protected SkinInfo 为 **0**，不能声称实测保留了非空既存用户皮肤记录；18 个未知 blob 均实际保留。缺少显式备份路径时测试会 skip，因此只有实际提供备份并通过的运行才计此门。

最终只读复核原根及 immutable baseline 的 25 文件长度/SHA 与最初 before 全等，175,303,768 B，原根零 Realm SDK 打开、零写句柄。私有证据已追加 FinalCampaignVerification；未把原路径或文件指纹放入仓库。

`V-001`～`V-004` 原有 0/4 未签收事实保持；新增可见结果须进入[集中清单](SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)。GPU 观感、低端 Windows 机器、真实设备/谱面及长时间体验仍需用户签收。C6 自动闭门不等于 Skin V1 或 release 完成。
