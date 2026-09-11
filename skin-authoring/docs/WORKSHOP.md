# 实际制作演练：Aurora Study

2026-09-11 已用随默认皮肤提示修复版提供的制作工具 `bin/SkinAuthoring.exe`，在独立复制的套件目录里重新制作 Aurora Study。入口是 Windows PowerShell 5 下的 `Author.ps1`；演练期间 PATH 中没有 Git 或 .NET SDK，使用随包工具即可完成。完整作者文件保留在 `sources/aurora-study/`，成品为 `dist/aurora-study.osk`。

最终演练执行于 `2026-09-11T15:56:22.4589140Z`，工具 SHA-256 为 `be91924141e44c8b4422290430ba8a14998a2c345c1cb2daa242b1e22c8ef2fa`。工具先独立发布并完成本页演练，随后供最终游戏安装包使用；本次记录不声称从尚未生成的最终游戏 ZIP 中抽取工具执行。后续装包须保留同一工具字节，工具身份与实际输出均记在下方两份验证记录中。

```powershell
./Author.ps1 -Action new -Output ./work/standalone-aurora -Name 'Aurora Study · 作者演练'
```

工具先检查模板，复制为不覆盖原件的新作品，再生成新的名字和完整双玩法文件。演练实际使用新的 `work/standalone-aurora/` 目录，随后把 `author.json` 的作者改为 `Authoring Kit workshop`，BMS 使用 `86e0a8`，mania 使用 `f4af89`，特殊键使用 `edf19b`。两种玩法可以保持不同配色，不需要拆包。`README.md` 也作为单独的作者编辑步骤改为 Aurora Study 的作品说明、署名和使用说明；生成素材时会保留这份文字。

```powershell
./Author.ps1 -Action generate -Source ./work/standalone-aurora
./Author.ps1 -Action check -Source ./work/standalone-aurora
./Author.ps1 -Action import -Source ./work/standalone-aurora -Output ./work/standalone-aurora.osk
./Author.ps1 -Action update -Source ./work/standalone-aurora -Output ./work/standalone-aurora.osk
```

上述制作步骤已实际执行；新作品产生自己的双玩法素材、完整设置和普通包。演练还故意损坏一张音符图片，确认检查准确指出 `bms/note-white.png`，恢复该文件后重新检查通过。随后 `import` 和 `update` 各准备一份可消耗导入副本，正式成品与上一版本均保留。完整命令结果、修改前后的作品资料和发行工具摘要见 [authoring-workflow-verification.json](authoring-workflow-verification.json)。这次制作命令没有启动游戏，也没有执行鼠标拖入。

同一最终工具也实际检查了静线、星轨和 Aurora Study：源文件检查、重复打包、从作品资料重新生成后再打包，均与各自随包成品逐字节一致。这组检查执行于 `2026-09-11T15:56:37.8010969Z`。未知设置会给出文件行号，过大的目录说明文件和损坏图片会被拒绝；未完成的打包文件、已有作品目录及上一版本得到保留。完整结果见 [authoring-tool-verification.json](authoring-tool-verification.json)，成品确切摘要见 `dist/` 中对应的 `.sha256`。Aurora Study 不携带可选脚本权限请求。

首次独立复做只编辑作品资料，保留下来的静线 `README.md` 与 Aurora Study 原件不同，因此整包没有逐字节一致。第二次把作品说明也明确列为作者编辑步骤，结果与随包 Aurora Study 完全一致。这是生成工具保留作者文字的既定行为，两次演练的原始记录都保留在仓库验收证据目录；没有通过覆盖作者说明或改变比对要求来消除差异。

同一份 Aurora Study 成品已在本轮游戏检查中经过普通包导入、选择、双玩法装载、普通导出及再次导入；静线和星轨同样覆盖普通导入、游戏管理目录和作者外部目录三种来源。对应记录是 `CanonicalSkinBackupRootProductTest` 的 G1 用例与成品玩法矩阵。制作演练通过逐字节一致的成品与这条游戏路径相连。

预览反馈前的游戏自动复验记录为：BMS 全部通过，mania 与核心皮肤检查只保留逐条核对名称和完整消息均一致的既有失败，文件存储检查、规定格式检查和 Release 编译也已完成。准确记录为 BMS `2215/2215`、mania `863/867`（原有 4 项）、核心皮肤 `1314/1319`（原有 5 项）、文件存储 `11/11`；证据保留在仓库 `artifacts/skin-c7-evidence/tests/c7-closure-*.trx` 与 `artifacts/skin-c7-evidence/closure-failure-comparison.json`，解释与范围见仓库 `doc_md/other/SKIN_SYSTEM_C7_VALIDATION_20260909.md`。这些仓库开发证据不影响独立套件中的制作步骤。这证明制作成品与当时游戏自动路径相通，不代替最终安装包组装和实际发行启动检查。

本次默认皮肤提示修复后的新成品，已再次通过完整 BMS 使用组合和真实双玩法进入、预览、重试与退出。mania 与核心皮肤的既有失败逐项保持原身份，文件保存检查通过；首次作者目录选择未生效的失败及同一产物后续完整通过均保留，没有调整预期。当前开发证据见仓库 `artifacts/skin-startup-warning-20260911/` 及 C7 报告的“交付后默认皮肤提示与构建警告修复”小节。

鼠标安装与选择、画面清晰度、显卡表现和真实设备长时间体验仍须人工观察；V-001～V-004 仍为 `0/4` 签收，V-005 仍未签收，不能据此宣布整个 Skin V1 或发行版本完成。此次修复保留作品的画面、声音与制作步骤，更新了左右转盘的适用声明；新工具已按同一路径重新演练。更新本页及两份验证记录前，上一版文件已原样保全到仓库 `artifacts/skin-startup-warning-20260911/prior-author-assets/`；预览修复前的文件仍在 `artifacts/skin-preview-entry-20260911/author-docs-before-fix/`；更早文件仍在 `artifacts/skin-c7-evidence/historical-author-doc-records-before-final/`；首次只改作品资料、随后明确编辑 README 的两次历史演练也继续保留。

下面是在发行套件中制作自己版本的完整入口，另用新的 `work/aurora-remake/` 目录。随包的 `sources/aurora-study/` 已存在，保留它作为原件，不删除它来迁就命令。在套件目录打开 PowerShell 后运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action new -Output .\work\aurora-remake -Name 'Aurora Remake · 我的演练'
# 用文本编辑器打开 work/aurora-remake/author.json，改成自己的作者名。
# bmsAccent 填 86e0a8，maniaAccent 填 f4af89，highlight 填 edf19b。
# 同时编辑 work/aurora-remake/README.md，写上自己的作品说明和署名。
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action generate -Source .\work\aurora-remake
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action check -Source .\work\aurora-remake
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action import -Source .\work\aurora-remake -Output .\dist\aurora-remake.osk
# 把提示的可消耗副本拖入游戏，在设置选择 Aurora Remake，再游玩 BMS 和 mania。
```

再练一次错误定位：在新作品 `skin.ini` 中把任意一条已提供素材的文件名改为 `missing-note`，运行同一条 `check`，应给出具体文件与位置。用编辑器撤销这个错误并再次检查，通过后运行同一条 `import`。不调用 `generate` 来掩盖手工错误；确认自己修复的文件真正通过检查。若新作品目录也已存在，换一个新的目录名并在后续命令中保持一致。

上面的重做入口使用自己的作品名，生成的包当然可以与 Aurora Study 不同。若要重现随包原件，应将新目录中的 `author.json` 与 `README.md` 都编辑为 `sources/aurora-study/` 中的完整原文，再生成和打包；作品说明也是包的一部分，不能只比较图片。

也可以把随包的演练作品作为第二份模板。独立于游戏的制作工具只生成、检查和打包普通文件；未形成可视化编辑器项目。
