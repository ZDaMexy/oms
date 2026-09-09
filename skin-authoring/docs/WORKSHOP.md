# 实际制作演练：Aurora Study

2026-09-09 在套件目录实际完成以下流程，作品源文件保留在 `sources/aurora-study/`，成品为 `dist/aurora-study.osk`。

```powershell
./Author.ps1 -Action new -Output ./sources/aurora-study -Name 'Aurora Study · 作者演练'
```

工具先检查模板，复制为不覆盖原件的新作品，再生成新的名字和完整双玩法文件。接着实际修改新作品的 `author.json`：作者为 `Authoring Kit workshop`，BMS 使用 `86e0a8`，mania 使用 `f4af89`，特殊键使用 `edf19b`。两种玩法可以保持不同配色，不需要拆包。

```powershell
./Author.ps1 -Action generate -Source ./sources/aurora-study
./Author.ps1 -Action check -Source ./sources/aurora-study
./Author.ps1 -Action pack -Source ./sources/aurora-study -Output ./dist/aurora-study.osk
```

上述操作已实际执行；产生自己的双玩法素材、完整目标设置和普通包，最终生产版本工具的公开设置、素材、场景解析与普通导入整包准入检查通过。当前成品的确切字节摘要见同目录 `.sha256`，重复打包字节一致，记录见 [authoring-tool-verification.json](authoring-tool-verification.json)。作品不携带可选脚本权限请求。

最后一段仍须由本轮真实产品验证确认：用 `Author.ps1 -Action import` 准备可消耗副本，通过与任意 `.osk` 相同的导入入口进入皮肤列表，选择 `Aurora Study · 作者演练`，分别观察 BMS 的薄荷音符与 mania 的杏色音符；从普通皮肤导出入口导出后重新导入。鼠标操作、画面清晰度和真正设备体验只能在实际运行中观察，不能由本页命令代签。

下面是在发行套件中重做的完整入口。上面的路径记录当时制作过程；随包的 `sources/aurora-study/` 已存在，因此重做必须使用新的作品目录。保留该目录作为原件，不删除它来迁就命令。在套件目录打开 PowerShell 后运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action new -Output .\work\aurora-remake -Name 'Aurora Remake · 我的演练'
# 用文本编辑器打开 work/aurora-remake/author.json，改成自己的作者名。
# bmsAccent 填 86e0a8，maniaAccent 填 f4af89，highlight 填 edf19b。
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action generate -Source .\work\aurora-remake
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action check -Source .\work\aurora-remake
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action import -Source .\work\aurora-remake -Output .\dist\aurora-remake.osk
# 把提示的可消耗副本拖入游戏，在设置选择 Aurora Remake，再游玩 BMS 和 mania。
```

再练一次错误定位：在新作品 `skin.ini` 中把任意一条已提供素材的文件名改为 `missing-note`，运行同一条 `check`，应给出具体文件与位置。用编辑器撤销这个错误并再次检查，通过后运行同一条 `import`。不调用 `generate` 来掩盖手工错误；确认自己修复的文件真正通过检查。若新作品目录也已存在，换一个新的目录名并在后续命令中保持一致。

也可以把随包的演练作品作为第二份模板。独立于游戏的制作工具只生成、检查和打包普通文件；未形成可视化编辑器项目。
