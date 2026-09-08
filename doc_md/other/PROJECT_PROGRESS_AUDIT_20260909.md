# 2026-09-09 项目实际进度、文档与记忆审查

## 结论与基线

本轮覆盖主线、P1-A～P1-M、README/发行/皮肤作者说明及相关 memory，按生产 caller → consumer → 测试源码 → 本轮实测核对。只同步文档与记忆，没有修改产品代码、测试或用户数据。

- 本地基线：`master` 的 `2f8aedd`，开始时工作区干净；已记录的 `origin/master` 为 `163d38a`，本地 ahead 1。两次 fetch 均因 TLS 失败，未验证在线远端是否有新提交；结论只覆盖本地基线。
- Skin V1 仍为 **5/7 closed，C6 active**；C1～C5 生产接入有证据，C6 sandbox/最终整包 reload 与 C7 canonical 双包/Authoring Kit 未完成。`V-001`～`V-004` 仍 **0/4**。
- BMS/mania 主流程和软件输入链已存在；真实硬件、特殊谱、视觉与候选发行包验收未因本次自动测试获得签收。
- 本报告是本地审查与复验的日期快照；当前状态、执行顺序仍以[主线状态](../mainline/DEVELOPMENT_STATUS.md)和[子线路由](../subline/README.md)为准。没有新开或关闭 campaign。

## 各子线覆盖

| 子线 | 当前生产证据 | 测试证据与实际边界 | 未闭合门 |
| --- | --- | --- | --- |
| [P1-A](../subline/P1-A/DEVELOPMENT_STATUS.md) 皮肤 | `SkinSection → SkinManager` 工作区/三源 reload；BMS/mania preparer → 同一 layout/material/scene publication → 实际 slot hosts | Workspace/archive/current-revision、mania 三源 consumer、BMS 全 keymode/slot 矩阵已存在并包含在本轮相应宽测试；internal staged import 仅有测试 caller，不单独计玩家功能 | C6/C7、canonical 替代程序化 OmsSkin、集中视觉签收 |
| [P1-B](../subline/P1-B/DEVELOPMENT_STATUS.md) 输入 | `BmsInputManager → OmsInputRouter`；keyboard/XInput/Raw Input/DirectInput-HID | loaded scratch bridge 证明软件鼠标/HID/XInput 路径；不能证明真实跨厂商控制器覆盖 | analog scratch 跨设备语义与实机 |
| [P1-C](../subline/P1-C/DEVELOPMENT_STATUS.md) 判定 | `BmsRuleset → BmsScoreProcessor`；OD/Beatoraja/LR2/IIDX 与 gauge family | BMS full 覆盖 parity、Empty Poor、GAS 等测试；Empty Poor 不断 combo，GAS 按最终 active gauge 判灯 | 真实谱判定手感；不恢复已删除常驻反馈卡 |
| [P1-D](../subline/P1-D/DEVELOPMENT_STATUS.md) 校准 | Settings supplemental binding editor、捕获与持久化 | 现存 editor/bridge 测试；轴方向 pulse 和 inversion 不等于 velocity/calibration 产品 | deadzone、sensitivity、持续 diagnostics |
| [P1-E](../subline/P1-E/DEVELOPMENT_STATUS.md) gameplay | `DrawableBmsHoldNote` release/regrab、HCN body 和 gauge | drawable、score、gauge 自动测试；LN/CN/HCN 的实际谱组合仍需人工 | 长条/音频/设备组合 checklist |
| [P1-F](../subline/P1-F/DEVELOPMENT_STATUS.md) 发行 | `build-release.ps1` 带 portable.ini；`OsuGameDesktop → OsuStorage` 选择启动存储与重定向 | 本轮是 solution Release build，未 publish 候选包；历史发行冷启动证据不能替代本次 | 随包说明、非便携覆盖保持模式、候选包冷启动 |
| [P1-G](../subline/P1-G/DEVELOPMENT_STATUS.md) 人工门 | 汇总子线实机/视觉证据，无独立 runtime | 保留 2026-07-14 恢复签收及集中清单；本轮无人工签收 | V-001～004、输入/长条/选歌/BGA/发行矩阵 |
| [P1-H](../subline/P1-H/DEVELOPMENT_STATUS.md) 存储 | BMS/mania filesystem importer、`ExternalLibraryScanner`、基本 managed 删除与 hash 复用 | import integration 测试、core scanner 7/7；缺失 root 跳过/移除仅改配置，不等于完整失效治理 | root 移除、跨 root 同 hash、失效/恢复一致性 |
| [P1-I](../subline/P1-I/DEVELOPMENT_STATUS.md) 选歌 | persisted read-model、query、分组导航与三行双端 slider | BMS 现有 UI 用例验证三行原型；core shared scene TestSearch 被依赖缺失阻断 | 先实现既定单轨上限段，再补拖拽/shared visual/大库门 |
| [P1-J](../subline/P1-J/DEVELOPMENT_STATUS.md) 音频 | converter `GetLaneCount` timeline → player/autoplay shared store | C3 末端 lane/keymode/shared-store 真实发声 proof 已完成；不能继续列为前置缺口 | 转谱 LN 头共享池、长 one-shot 保位续播、50k profile/听感 |
| [P1-K](../subline/P1-K/DEVELOPMENT_STATUS.md) 解析 | decoder 单一 keymode/override → loader/converter；统一 event times | lane、LNOBJ、timing、typed STOP 等测试；RANDOM 固定值与 IF/ELSE/SWITCH 控制流已实现 | 用户纠正 UI、真正随机选择、少数实谱语义 |
| [P1-L](../subline/P1-L/DEVELOPMENT_STATUS.md) BGA | 唯一 layout viewport、C5 只读事件 → `DefaultBmsBgaPanelDisplay` 每 viewport 创建 player | layout/timing-epoch 与默认 14K 四 player 测试；共享转码任务不等于共享 decoder | 单 content/decoder 会话、设置旧中缝文案、Gimmick/反向滚动实谱 |
| [P1-M](../subline/P1-M/DEVELOPMENT_STATUS.md) 播放器 | 现有 MusicController/mini/playlist；BMS 仅显式有效 PREVIEW、PreviewTime=0 | 未发现 PlayQueue/SMTC 实现；旧 `detectFullMusicFile` 已不存在 | PlayQueue、repeat/source 状态、整曲语义、SMTC；保持后置 |

## 已纠正的实质漂移

1. **把目标当已完成**：P1-I 单轨筛选、P1-L 单内容源、P1-M 整曲识别，以及 canonical 默认包均恢复为真实边界。保留已有产品决定，未因旧测试或文档写过“已落”而迁就原型。
2. **把完成项留在待办**：P1-J 的末端 lane 过滤/真实发声前置、skin geometry/scene/event memory 的 C3～C5 旧缺口已消除。脚本权限协商的类型和 fixture 仍不计 C6 production。
3. **主约束伪实现**：修正 BMS lane/scratch channel、measure 累乘/STOP 伪算法、固定分支兼容、LN tail 音频、Empty Poor/combo、GAS lamp、结果重复应用 mods、判定/gauge 数表泛化和第二套 layout。保留行为合同，数值细节回归 owning 子线与生产测试。
4. **难度权威混写**：native BMS 作者 PLAYLEVEL/持久化 fallback、converted-mania rating、weighted note-distribution preview 分开；删除不存在的密度星模型。层级分组直接展开单谱面，排序下拉仍可用。
5. **发行使用说明**：官方 ZIP 自带 portable.ini；非便携覆盖必须保持 marker 缺席。storage.ini 位于 bootstrap storage，不在 exe 同级且不随迁移。RELEASE 已修；打包脚本内双语说明的欠账登记 P1-F。
6. **公开与内部能力混写**：README 三语收窄 HID 覆盖宣传、纠正 ffmpeg 设置实际名称与默认 14K 布局；SKINNING 明确 editor 禁用、当前 Supported/NotApplicable profile、legacy direct visual 与 public author ABI 边界。
7. **memory 进度漂移**：修正已删除函数/旧 prototype/旧 authority；索引减少复制当前燃尽。新增数据根 marker 和 SDK 诊断误判的召回线索，不复制本轮测试数字。

## 本轮实际验证

共享工程串行调度。先成功编译 `osu.Desktop.slnf` 的 Release 产物，再运行 BMS/mania 的 `--no-build`；core `osu.Game.Tests` 不在该 solution filter，先由首条 core test 命令编译，然后才对 core library 子集使用 `--no-build`。

| 门 | 本轮结果 | 范围与限制 |
| --- | --- | --- |
| Release build | 0 errors / 11 warnings | 9 次既有 MessagePack NU1902，BMS tests 的 CS8600/CA2007 各 1；没有压制告警 |
| BMS full | 1721/1721 | 无失败、跳过或 hang artifact |
| mania full | 860/864 | 四个既有 HoldNote frame-count 失败，详见下表 |
| core `~Skin` | 1218/1224 | 四个旧 archive fixture 和两个默认皮肤假设失败；不是全 core 测试 |
| core library/filter | 22/23 | BmsStarRatingResolver 14/14、ExternalLibraryScanner 7/7、shared FilterControl 1/2 |

mania 四项均属于 `TestSceneAutoGeneration`，本轮名称和 ErrorInfo.Message 与现存 `mania-c3-full-final.trx` 逐项一致：

| 测试 | 本轮帧数断言 |
| --- | --- |
| TestSingleHoldNote | Expected 0，actual 2 |
| TestHoldNoteChord | Expected 0，actual 2 |
| TestHoldNoteStair | Expected 0，actual 4 |
| TestHoldNoteWithReleasePress | Expected 2，actual 3 |

core 六项与 [C5 既有失败清单](SKIN_SYSTEM_C5_SCENE_EVENT_COMPLETION_HANDOFF_20260903.md)对应：

- `TestRetrieveAndLegacyExportJapaneseFilename`、`TestRetrieveAndNonLegacyExportJapaneseFilename`、`TestRetrieveOggAudio`、`TestRetrievalWithConflictingFilenames`：`No valid beatmap files found in the beatmap archive.`；四项本轮 ErrorInfo.Message 与现存 `c1-core-skins.trx` 的失败条目一致。
- `TestBackgroundCyclingOnDefaultSkin(True)`：`wait for beatmap background to be loaded` 超时。
- `TestSampleUpdatedBeforePlaybackWhenNotPresent`：期望 `Gameplay/Argon/normal-sliderslide`，实际 `normal-sliderslide`。

额外 shared `TestSceneBeatmapFilterControl.TestSearch` 因测试作用域缺少 `INotificationOverlay` 报 `DependencyNotRegisteredException`，未进入搜索断言；已写回 P1-I 修 fixture/re-run 待办，不据此推断产品搜索失败，也不把 shared gate 写绿。

命令（PowerShell；每条测试均写入独立 TRX，并使用 `--blame-hang --blame-hang-timeout 5m`）：

```powershell
dotnet build osu.Desktop.slnf --no-restore -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj --no-build --no-restore -c Release
dotnet test osu.Game.Rulesets.Mania.Tests/osu.Game.Rulesets.Mania.Tests.csproj --no-build --no-restore -c Release
dotnet test osu.Game.Tests/osu.Game.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~Skin'
dotnet test osu.Game.Tests/osu.Game.Tests.csproj --no-build --no-restore -c Release --filter 'FullyQualifiedName~ExternalLibraryScanner|FullyQualifiedName~BmsStarRatingResolver|FullyQualifiedName~TestSceneBeatmapFilterControl'
```

本机日志为 `%TEMP%/oms-progress-audit-20260909-{release,bms,mania,core-skin,core-library}.log`；TRX 为 `%TEMP%/oms-progress-audit-20260909-tests/{bms-full,mania-full,core-skin,core-library}.trx`。不提交环境日志、二进制或用户路径。

环境边界：当前 roll-forward 选中 SDK 10.0.204，目标 net8.0。`dotnet --info` 的 workload InstallerBase 初始化单独失败，但实际 build/test 均可运行；本轮没有改变 SDK、依赖或配置。未执行全 core suite、publish、冷启动、真实硬件、人工音频/视觉；不重签 2026-09-03 C5 完整 campaign 门。

## 治理与后续

本轮同步 owning 子线四件套、主线摘要、派生说明及 memory；历史 CHANGELOG 保留原始记录，最新审查条目指出已被纠正的结论。文档检查与最终 diff 结果记录在本轮主线 CHANGELOG。

后续仍按 C6 → C7 → 玩法/硬件/选歌/BGA收尾 → 发行门执行。文档中识别出的真实实现欠账进入所属 PLAN，未以假想 guard、catch、fallback 或新架构替代实际工作。
