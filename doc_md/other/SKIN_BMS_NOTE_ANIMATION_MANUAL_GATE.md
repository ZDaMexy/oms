# BMS 普通短键编号帧动画手工门

本页只用于复现当前已导入 `.osk` 的 BMS 普通短键编号帧动画、皮肤选择切换和 selected 坏包逐组件回落。素材全部由 OMS 测试侧代码生成，不读取 `SKIN/SimpleTou-Lazer` 或用户皮肤。

## 生成

在仓库根目录运行：

```powershell
.\GenerateBmsNoteAnimationManualGate.ps1
```

默认输出到 `artifacts/manual-gates/bms-note-animation/`：

- `bms-note-animation-manual-gate.osk`：7K lane 1 使用 60 张连续编号帧；深蓝音符内的白/品红亮带每秒横向循环一次。
- `bms-note-animation-manual-gate-broken.osk`：声明同一资源，但只有 frame 1，故缺少必需的 frame 0 和静态图。
- `chartbms/bms-note-animation-manual-gate/`：约 30 秒的静音 7K `.bme`；第一小节覆盖全部七条普通键道，后续持续在 lane 1 放置观察音符。
- `SHA256SUMS.txt`：本次确定性输出的校验值。
- `import-staging/`：供正常 UI 导入消费的一次性 `.osk` 副本；导入成功后被删除是预期行为，重跑脚本只覆盖两个已知副本并保留无关文件。staging 根或已知目标若是 reparse point/目录冲突会 fail-closed；该目录不进入 `SHA256SUMS.txt`。

生成器固定 PNG 像素、编码参数、ZIP entry 顺序、时间戳和压缩方式；focused test 会连续生成两次并逐文件比较 SHA-256。

## 隔离自动可视预检

需要集中验收时，可在仓库根目录由用户自行运行：

```powershell
.\RunBmsNoteAnimationVisualGate.ps1
```

脚本先构建 BMS 测试工程，再在一次性 host storage 与测试内部临时数据根中只打开 `TestSceneBmsManagedPackageNoteVisualGate`；该场景在普通 desktop TestBrowser 缺少 exact-isolation marker 时会在导入前 fail-closed。场景用真实 `SkinManager` 导入内存生成的 good/broken 包，经 `RulesetSkinProvidingContainer → BmsAsyncNoteDrawable` 自动循环 3 轮：good 阶段等待 60 帧视觉完全加载并驻留，broken 阶段等待可见默认回落。全部步骤完成时 PASS 保留 3 秒后自动关窗并返回 `0`；参数、启动、加载、步骤、exact host `LoadComplete` 后的 120 秒 watchdog 或 host 清理等任一受控失败会立即写日志、关窗并返回 `1`，用户提前关闭返回 `3`。watchdog 不冒充操作系统进程级超时。runner 会验证并删除自己创建的精确 AppData 子目录，场景内部数据使用临时 storage；不会读写 OMS 产品 Realm、`chartskin/` 或用户皮肤目录。

这只是隔离自动预检，不能替代下方真实 Song Select → Play、选择切换和用户观感签收。用户签收状态统一记录在[Skin V1 集中视觉验收清单](SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)的 `V-001`。

## 隔离实机步骤

1. 使用测试专用 portable 数据根，不要把 fixture 写入生产 Realm 或用户皮肤目录。
2. 将生成的 `chartbms/bms-note-animation-manual-gate/` 整个目录复制到该测试数据根的 `chartbms/`。
3. 启动对应 commit 的 OMS，分别通过正常导入 UI 导入 `import-staging/` 中的 good 与 broken 副本。不要直接导入输出根下的确定性原件；`SkinImporter` 会在成功后删除 `.osk` 来源，直接导入原件会使 SHA 清单立即失真。
4. 选择 good 包并游玩 `OMS BMS Note Animation Manual Gate`。预期 lane 1 的音符保持固定尺寸，内部亮带持续横向移动；其它 lane 仍使用外层 fallback，可作静态对照。
5. 切到 OMS 内建皮肤，再切回 good 包。预期选择变化立即体现在新进入 gameplay 的普通短键上，且不会跨包拼接旧帧。
6. 选择 broken 包并再次游玩。预期 lane 1 音符不会消失，而是沿既有外层链逐组件回落到可玩默认视觉。
7. 记录 commit/build、Windows 与显示环境、实际观感、截图/日志和通过或失败结论。

## 不覆盖的结论

本 fixture **不证明 beatmap-local BMS 普通键视觉优先**。原始 26 项产品测试中的 beatmap-local 用例是私有 `BeatmapNoteSkin` provider-contract fixture；真实 `WorkingBeatmap` 仍创建 `LegacyBeatmapSkin`，没有可由 `chartbms/` 目录表达的 `[Bms]` ordinary-note provider。因此不得用本次手工包把 beatmap-local 或完整 Skin V1 写成实机通过。

它也不覆盖 LN、key、mania compatibility、G1、scene/script、整包原子热重载、`oms-simple` 或 canonical 恢复。
