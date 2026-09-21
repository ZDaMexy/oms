# BMS TOTAL 规则取证（2026-09-22）

> 外部源码取证与本轮实施记录；软件验证见末节，不代表播放器原版实机或长条人工签收。
> 玩法归属 [P1-C](../subline/P1-C/DEVELOPMENT_STATUS.md)，解析归属 [P1-K](../subline/P1-K/DEVELOPMENT_STATUS.md)；当前进度与正式合同以所属子线为准。

## 范围与证据边界

本轮修正现有 Gauge 规则选择下的作者 TOTAL、缺省 TOTAL、非法输入、物量与结果重建。保留 Gauge Mod 选择、默认规则、GAS 降级合同和血条外观。Legacy 维持既有算法与缺省 200；IIDX 维持不使用 TOTAL 的算法。HCN 持续回血/扣血速率另案处理，本轮只校验其 TOTAL 输入与物量来源。

`#TOTAL` 表示普通血条正向恢复的预算，缺省行为并非 BMS 格式统一规定。格式背景见 [BMS command memo 的 TOTAL 条目](https://hitkey.bms.ms/cmds.htm#TOTAL) 与 [仓库格式参考](BMS_FORMAT_REFERENCE.md)。不得将某一播放器的缺省策略描述成所有 BMS 的强制标准。

本报告直接读取公开实现的源码。LR2 取证采用 LR2oraja 作者明确声称匹配 LR2 的兼容实现；没有运行 LR2 原版，也没有确证原版对非法、重复声明和所有特殊模式的逐项行为。

## 锁定的一级来源

| 来源 | 版本 | 直接证据 |
| --- | --- | --- |
| exch-bms2/beatoraja | `bda2b72f8607daed9e9f651f1e92fc445d42f114` | [BMSPlayerRule](https://github.com/exch-bms2/beatoraja/blob/bda2b72f8607daed9e9f651f1e92fc445d42f114/src/bms/player/beatoraja/play/BMSPlayerRule.java)：缺省公式；[GrooveGauge](https://github.com/exch-bms2/beatoraja/blob/bda2b72f8607daed9e9f651f1e92fc445d42f114/src/bms/player/beatoraja/play/GrooveGauge.java)：回血与 Hard 修正 |
| exch-bms2/beatoraja | 同上 | [PlayerResource](https://github.com/exch-bms2/beatoraja/blob/bda2b72f8607daed9e9f651f1e92fc445d42f114/src/bms/player/beatoraja/PlayerResource.java)、[BMSPlayer](https://github.com/exch-bms2/beatoraja/blob/bda2b72f8607daed9e9f651f1e92fc445d42f114/src/bms/player/beatoraja/play/BMSPlayer.java)、[AutoplayModifier](https://github.com/exch-bms2/beatoraja/blob/bda2b72f8607daed9e9f651f1e92fc445d42f114/src/bms/player/beatoraja/pattern/AutoplayModifier.java)：默认值、辅助变换与 Gauge 建立顺序 |
| exch-bms2/jbms-parser | `e19bcc21c8d4c85d7791ff8d31d73c1cb1ea5d27` | [BMSDecoder](https://github.com/exch-bms2/jbms-parser/blob/e19bcc21c8d4c85d7791ff8d31d73c1cb1ea5d27/src/bms/model/BMSDecoder.java)、[BMSModel](https://github.com/exch-bms2/jbms-parser/blob/e19bcc21c8d4c85d7791ff8d31d73c1cb1ea5d27/src/bms/model/BMSModel.java)、[TimeLine](https://github.com/exch-bms2/jbms-parser/blob/e19bcc21c8d4c85d7791ff8d31d73c1cb1ea5d27/src/bms/model/TimeLine.java)：输入处理与 LN 计数 |
| wcko87/lr2oraja，代码分支 `lr2oraja` | `3db78adff969b854fb3bcc68966449bd36cf7a5b` | [BMSPlayerRule](https://github.com/wcko87/lr2oraja/blob/3db78adff969b854fb3bcc68966449bd36cf7a5b/src/bms/player/beatoraja/play/BMSPlayerRule.java#L73-L89)：缺省 TOTAL 的实际公式 |
| wcko87/lr2oraja，说明分支 `readme` | `c40f03cc0ad3e9cf2065e99abdd791197c39096d` | [README](https://github.com/wcko87/lr2oraja/blob/c40f03cc0ad3e9cf2065e99abdd791197c39096d/README.md)：第 6 项明确说明 0.8.3+ 的公式变更用于匹配 LR2 |

解析器仓库与播放器仓库分别锁定当前公开源码；这不构成某个发布二进制所捆绑解析器版本的实测证明。

## 缺省公式与作者值

设 `N` 为选定长条模式下、辅助移除前的操作判定点数。

| Gauge 规则家族 | 未声明合法 TOTAL 的处理 |
| --- | --- |
| Beatoraja（5K/7K/10K/14K/PMS） | `max(260, 7.605 * N / (0.01 * N + 6.5))` |
| LR2（本轮采用 LR2oraja 兼容依据） | `160 + (N + clamp(N - 400, 0, 200)) * 0.16` |
| Legacy | 保持 OMS 历史缺省 `200` |
| IIDX | TOTAL 不参与该家族血量计算 |

Beatoraja 的 24K/48K 另有 `max(300, 7.605 * (N + 100) / (0.01 * N + 6.5))`，不应扩张成当前 OMS 支持范围。LR2oraja 的上述函数不按 keymode 分支，不能由此宣称 LR2 原版支持同样的全部模式。

作者明确填写的有限正数直接采用，包括小数和小于 260 的值。260 是 Beatoraja 缺省公式的下限，不是作者值下限。例如 `N=1000`：Beatoraja 缺省约 `460.9091`，LR2 兼容缺省 `352`，作者明确写 `200` 则仍是 `200`。

## 非法与重复声明

jbms-parser 的 TOTAL 处理只在数字解析成功且 `> 0` 时更新模型；因此非法后续声明不覆盖先前合法值。全无合法声明时，模型保留 `Total=100` 与 `TotalType=BMSON` 的初态，再在播放器验证中换成缺省 TOTAL 的 100%。这属于参考实现的内部表示，OMS 无须复制该表示。

OMS 本轮采用的边界合同：最后一条合法且处于有效解析分支中的 TOTAL 声明生效；零、负数、非数字、NaN、无穷值输出解析诊断并忽略；没有合法声明才取对应家族缺省。不得在读取时把未声明提前变成作者明确填写 200。

参考解析器仅检查 `> 0`，会接受正无穷；OMS 拒绝非有限值是明确的输入安全修正，不宣称与该漏洞兼容。原版 LR2 的非法及重复声明行为未在本轮得到独立验证。

## 两个物量的不同职责

Beatoraja 的调用顺序提供了区分两个物量的证据：`PlayerResource` 按 `lnmode` 解码并执行 `BMSPlayerRule.validate()`，之后 `BMSPlayer` 才执行谱面辅助变换，最后在 `create()` 建立 `GrooveGauge`。`AutoplayModifier` 把指定轨道移入背景，不重写 TOTAL；血量修正公式使用建立 Gauge 时的 `model.getTotalNotes()`。

- 缺省 TOTAL 的 `N`：长条模式已经确定，辅助音符尚未移除。
- 每次回血与 Hard 物量修正的分母/计数：辅助变换后实际参与手动判定的音符数。
- LN 只计头；CN/HCN 计头尾；背景、地雷、HCN 持续恢复事件不计入操作判定点。
- 全辅助后没有手动判定点时，OMS 必须避免除零，不用更小的手动物量回头改变谱面的缺省 TOTAL。

因此不能只保留一个排除辅助后的计数，同时用于缺省公式和每次回血。OMS 的具体捕获时机须结合自己的 Mod 生命周期验证，不照抄上游类结构。

## TOTAL 的消费边界

普通 Beatoraja/LR2 血条对正向恢复使用 `TOTAL / 手动判定点数`，失误基础扣血不一起乘该比值。Hard 则保留家族差别：Beatoraja 使用 TOTAL 限制回血，LR2 兼容规则根据 TOTAL 与物量修正扣血。不得用一个通用倍数替换这些已有家族行为。

本轮不改 Legacy 倍率、不重选默认 Gauge、不调整 HCN 持续速率。演奏、GAS 重建和结算曲线必须消费同一份有效 TOTAL 语义，谱面复制和 Mod 切换不能将缺省值回写为作者值。

## 成绩版本与实际实现

当前实现：

- 新局使用 BMS 成绩版本 v7，采用本次作者声明/家族缺省语义。
- v6 及更旧成绩，或没有版本数据的历史记录，使用旧 TOTAL 语义：未声明时仍为 200。
- 历史保存的过关灯和最终血量不批量重算；曲线及回放重建须根据成绩版本选择相应语义，避免旧灯与新曲线矛盾。
- 新手动录制通过 `BmsReplayRecorder` 初始化v7；新自动播放通过瞬时 `BmsGeneratedAutoplayReplay` 身份初始化，避免把没有数据的旧自动播放误认新局。`DrawableBmsRuleset.SetReplayScore` 将版本传给实时血量处理器；结算重建使用同一版本选择。`PopulateScore` 不覆盖该数据。
- LR2/5K ExHard低物量扣血的参考预算使用整数折半（如125→62），原代码用浮点折半；v7修正，旧版本保持原舍入。HCN只调整等价乘除顺序以避免中间溢出，不调整速率。
- 历史非法TOTAL谱不承诺重现先前无效数值传播；解析统一采用有限正数边界，已保存终值/灯仍保留。历史合法声明与未声明路径按版本保持。

## 验证状态

Debug focused 已完成：`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-restore --filter "FullyQualifiedName~BmsTotalRulesTest|FullyQualifiedName~TestSceneBmsTotalReplay|FullyQualifiedName~BmsGaugeProcessorTest|FullyQualifiedName~BmsGasGaugeProcessorTest|FullyQualifiedName~BmsClearLampProcessorTest|FullyQualifiedName~BmsBeatmapDecoderTest|FullyQualifiedName~BmsBeatmapConverterTest|FullyQualifiedName~BmsPlayableBeatmapCacheTest" --logger "trx;LogFileName=total-focused-final.trx"`：250通过，0失败/跳过。TRX在本地BMS测试工程TestResults，日志`%TEMP%/oms-total-focused-final.log`。其中真实headless Drawable覆盖旧无数据/v6/v7回放、Mod应用先后、新自动播放和新手动录制；不是只测工厂函数。

首次`--no-restore`因本机缺失Test SDK依赖空跑退出0，不作为验证；随后构建报具名MSB3030缺测试运行器文件，`dotnet restore ... --force --no-cache`恢复后重新编译测试。有效构建保留既有CS8600/CA2007两个警告。

BMS full：在上述成功编译后执行 `dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-build --no-restore --logger "trx;LogFileName=total-bms-full.trx"`，2300通过、29失败、16跳过，共2345，耗时9分47秒。没有把该结果标成全绿；BMS转mania及相关转换测试没有失败。日志`%TEMP%/oms-total-bms-full.log`，TRX在本地BMS测试工程TestResults。

Release：`dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m`成功，0错误、2个既有测试警告（上述CS8600/CA2007），日志`%TEMP%/oms-total-release.log`。

自动覆盖包括作者低值与缺省的区别、非法/重复声明、公式边界、LN/CN/HCN和辅助前后物量、普通/Hard增减血、家族切换、GAS、新旧演奏/回放/结算与零手动物量。没有LR2原版或Beatoraja实机对照，也不代签真实设备/特殊谱长条门。提交前`powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\CheckDocumentation.ps1`与`git diff --check`通过；检查日志`%TEMP%/oms-total-doc-check.log`。

### 完整回归失败的基线复现

在独立detached检出`F:/oms-total-baseline-20260922`构建未修改的`45d8613`，使用当前full的失败方法组成`FullyQualifiedName~方法名`的OR筛选，重新执行`dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --filter <筛选> --logger "trx;LogFileName=total-baseline-failures.trx"`。29项全部复现；逐项testName、错误信息相同，路径根标准化后堆栈也全部相同。筛选文本和日志分别为`%TEMP%/oms-total-baseline-filter.txt`、`%TEMP%/oms-total-baseline.log`，TRX已留存至当前工作区BMS测试工程的`TestResults/total-baseline-failures.trx`，临时检出随后清理。这是具名失败复现，不是旧版本全量通过声明。

其中28项在`BmsManagedFolderSelectionProductTest.FullSkinSettingsCallerHost.Workspace`第7186行因`Single()`空序列失败，另1项在同文件第1952行要求编辑器禁用而实际启用。历史提交`701893fd34ec13e28b1dad30ec3dcf9f73b25721`已从`SkinSection`删除`FolderSkinWorkspace`，并按产品要求调整编辑器准入；测试仍断言旧入口。对应皮肤生产/测试文件与本次改动无diff。本次不恢复已移除功能，也不把这些旧测试断言算作TOTAL退化。

逐项失败身份（参数实例各自核对）：

- `TestCurrentDeleteJournalCompletionDynamicallyRefreshesRedactedWorkspaceSupport`
- `TestCurrentExternalRealmFailureRollbackWaitsForHalfLoadedConsumerAndRestoresExactOldPair`
- `TestCurrentExternalWorkspaceUnregisterFinalRealmDriftRestoresExactRevisionAndRetries("record-field")`
- `TestCurrentExternalWorkspaceUnregisterFinalRealmDriftRestoresExactRevisionAndRetries("registry-declaration")`
- `TestCurrentExternalWorkspaceUnregisterFinalRealmDriftRestoresExactRevisionAndRetries("service-owner")`
- `TestCurrentExternalWorkspaceUnregisterRejectsRealVisualParticipantBeforeFallbackAndRetriesAfterDetach`
- `TestCurrentExternalWorkspaceUnregisterWaitsForExactOldDetachAndNeverTouchesSource`
- `TestCurrentManagedDeleteIsolatesThrowingSourceObserverAndRetiresAfterRealHolderDetaches`
- `TestCurrentManagedDeleteParticipantFailureDoesNotStartJournalOrPhysicalMutationAndRetries`
- `TestFolderWorkspaceManagedOpenRowRevalidatesDriftBeforeExternalLaunch("duplicate-path")`
- `TestFolderWorkspaceManagedOpenRowRevalidatesDriftBeforeExternalLaunch("freeze")`
- `TestFolderWorkspaceManagedOpenRowRevalidatesDriftBeforeExternalLaunch("owner")`
- `TestFolderWorkspaceManagedOpenRowRevalidatesDriftBeforeExternalLaunch("path")`
- `TestFolderWorkspaceManagedOpenRowRevalidatesDriftBeforeExternalLaunch("same-label-different-id")`
- `TestFolderWorkspaceManagedOpenRowRevalidatesDriftBeforeExternalLaunch("stale-source")`
- `TestFolderWorkspaceManagedRowDeleteDialogCancelIsSideEffectFree`
- `TestFolderWorkspaceManagedRowDeleteDoubleConfirmDisablesReentryAndShutdownJoins`
- `TestFolderWorkspaceManagedRowDialogRechecksCurrentToNonCurrentAsNotRequired`
- `TestFolderWorkspaceManagedRowDialogRechecksNonCurrentToCurrentThroughC2Fallback`
- `TestFolderWorkspaceManagedRowDialogRevalidatesDetachedRecordAtConfirmation("delete-pending")`
- `TestFolderWorkspaceManagedRowDialogRevalidatesDetachedRecordAtConfirmation("external-generation")`
- `TestFolderWorkspaceManagedRowDialogRevalidatesDetachedRecordAtConfirmation("hash")`
- `TestFolderWorkspaceManagedRowDialogRevalidatesDetachedRecordAtConfirmation("missing")`
- `TestFolderWorkspaceManagedRowDialogRevalidatesDetachedRecordAtConfirmation("owner")`
- `TestFolderWorkspaceManagedRowDialogRevalidatesDetachedRecordAtConfirmation("path")`
- `TestFolderWorkspaceManagedRowDialogRevalidatesDetachedRecordAtConfirmation("same-label-different-id")`
- `TestFolderWorkspaceManagedRowDialogUsesAuthoritativePairAfterBackingProjectionTamper`
- `TestFolderWorkspaceOperationFailureLogsOnlyStableGenericText`
- `TestRealmPackageSettingsDeleteKeepsLegacySoftDeleteAndDefaultSemantics`
