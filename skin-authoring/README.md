# OMS 双皮肤制作套件

玩家可导入 **OMS Simple · 静线** 与 **OMS Complex · 星轨**。每个包都同时包含 BMS 与 mania：静线清楚克制；星轨使用独立机械舞台、青金与紫金配色、流动音符、集中显示分数/准确率/连击/速度/判定/能量、谱面进度与暂停状态的信息控制台，以及随击打强度变化的两侧演出。信息控制台不依赖额外脚本授权。两款均为普通 `.osk`，可解包编辑、导入、选择和导出。星轨是展示作品与默认候选，不决定首次默认选择。

**作者设计与自动验证不等于人工验收。** 真实字体、不同屏幕清晰度、低端设备、延迟和长时间体验仍按集中验收清单观察；现有 V-001～V-005 的未签收事实不改变。

## 直接使用

复制 [oms-simple.osk](dist/oms-simple.osk) 或 [oms-complex.osk](dist/oms-complex.osk)，把**副本**拖入 OMS；普通导入可能消耗输入文件，请保留 `dist/` 中的正式作品。在设置的皮肤列表选择 `OMS Simple · 静线` 或 `OMS Complex · 星轨`，先各玩一张 BMS 与 mania。也可运行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action import -Source ./sources/oms-complex`，工具会准备可消耗的导入副本。星轨的可选组合效果可以授权、拒绝或撤销；拒绝后自有音符、长条、判定线和必要信息照常显示。退出游玩与预览后才可重新载入。

整套目录可复制到任意普通可写目录。正式验收包附带 `bin/SkinAuthoring.exe` 与所需文件，无须安装 SDK 或阅读游戏源码。仓库开发者第一次准备工具可运行：

```powershell
dotnet build tools/SkinAuthoring -c Release
```

## 完成第一款自己的皮肤

打开 PowerShell 并进入本套件目录，依次运行。下列入口只允许本次工具进程执行随包脚本，不修改 Windows 的永久设置，也不需要管理员权限：

```powershell
# 1. 复制完整静线模板为新作品；不会覆盖已有目录。
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action new -Output ./work/my-skin -Name '我的第一款皮肤'
# 2. 用文本编辑器修改 work/my-skin/author.json 的 bmsAccent、maniaAccent 等六位颜色。
# 3. 由可编辑制作配方重新生成图片和双玩法设置。
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action generate -Source ./work/my-skin
# 4. 定位文件、行号和素材问题。
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action check -Source ./work/my-skin
# 5. 检查、打包并准备导入副本；只把工具提示的副本拖入 OMS。
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action import -Source ./work/my-skin -Output ./dist/my-skin.osk
# 在皮肤设置选择新作品，分别验证 BMS、mania。
# 6. 修改后更新；准备新的普通包，保留游戏内旧版本供对照。
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action update -Source ./work/my-skin -Output ./dist/my-skin.osk
```

若只修改 `skin.ini`、图片、场景或脚本，**直接 check → pack**；`generate` 会按 `author.json` 重新生成这些文件，不是普通保存按钮。可以用任意图片编辑器替换 PNG；注意透明边缘、文件名和尺寸。所有可见素材位于 `bms/`、`mania/` 与 `scene/`，根目录 WAV 是普通击打及长条持续音；均可直接替换，不存在只能官方调用的素材。

`-GameExecutable '你的 OMS 安装目录/osu!.exe'` 可把准备好的导入副本直接交给普通导入入口，正式 `.osk` 与作者文件保留。也可把作品目录登记为作者外部目录；OMS 只读这个目录，作者在自己的编辑器中修改。已登记且选中的目录可在退出游玩/预览后使用设置内“重新载入当前皮肤”。新受管目录需重启才会发现，不会自动切换。不要直接改游戏内部文件存储。

继续制作舞台、动画和组合演出时，按 [制作说明](docs/AUTHORING.md) 操作；具体字段、事件、模板、图片变体、权限和预算见 [普通作者参考](docs/REFERENCE.md)。完整练习文件可直接放入新的模板副本并检查，不需要从游戏源码寻找示例。

## 套件内容

| 内容 | 用途 |
| --- | --- |
| `sources/oms-simple/` | 完整静线作者文件，也是最小完整模板 |
| `sources/oms-complex/` | 完整星轨作者文件、场景与组合脚本 |
| `sources/aurora-study/` | 实际从模板改色、重新生成、检查、打包的第三方演练作品 |
| `dist/` | 普通成品包与 SHA256；同输入、同工具重复打包得到相同字节 |
| `Author.ps1` | 新建、生成、检查、打包、普通导入与更新入口 |
| `Build-Skins.ps1` | 从保留的作者文件重复打包两款成品；加 `-RebuildAssets` 才重新生成 |
| `Test-Authoring.ps1` | 正常制作、重复打包、错误拒绝与中断成品保护的可重复复核 |
| `docs/` | 制作约定、错误定位、验收步骤和完整公开目录 |

发行套件在 `tool-source/` 保留完整配方与工具源码（仓库位置为 `tools/SkinAuthoring/`）。`SkinRecipe.cs` 是作者可编辑的完整制作配方；它只离线生成普通文件，不由游戏加载，不需要作者编写或编译 DLL。日常制作直接运行附带工具；工具工程源码在完整游戏仓库中编译，套件副本用于审阅与保留可重复制作依据。游戏只消费包内公开设置、图片、场景与可选数值脚本。

## 交付与数据保护

打包先完整检查并捕获当前文件，再写旁边的 `.pending`，通过与游戏普通导入相同的整包检查后替换成品；上一个成品留为 `.previous`。已存在 `.pending` 时要求先保留检查，不猜测删除。新建不会覆盖原作品，链接目录不会被打包，外部作者目录不会被 OMS 修改。

普通导入产生新记录；本工具不假装提供不存在的“覆盖原导入记录”功能。日常快速修改建议登记作者目录并手动重新载入。分享作品使用 `.osk`，导出使用游戏普通皮肤导出入口。

许可：本套件原创几何素材、配方、场景与文档按仓库 MIT 许可发布；无远程素材或隐藏依赖。
