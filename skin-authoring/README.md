# OMS 皮肤制作套件

用户已放弃 complex，仅继续打磨 simple。静线为唯一内置、默认与保底；星轨不再是启动依赖或构建对象。历史作者文件保留参考，旧内置星轨选择迁回静线，普通用户导入皮肤不清除；不再要求星轨视觉签收。本套件提供静线作者源、普通 `.osk` 与制作工具；仓库保留的 complex 目录只作历史公开写法参考，不作为当前成品或制作验收依赖。

静线素材位置与提示词见[素材说明](docs/SIMPLE_ARTWORK.md)，实机参考见[索引](../doc_md/other/references/simple-1p-20260912/README.md)。静线尚未整体视觉签收。

## 直接使用

仓库开发时，正常 `dotnet run --project osu.Desktop`、build 与 publish 会从静线 `sources/oms-simple` 自动生成本次静线内置包及校验，不需要先运行 `Build-Skins.ps1`。普通构建不覆盖手工素材，也不改仓库 dist；发行套件的同名包来自本次游戏产物。显式跳过构建会沿用已有产物。构建回归可运行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File skin-authoring/Test-BuiltInSkins.ps1`（仓库根目录）。

安装支持当前改动的 OMS 后，静线 `OMS 简洁` 是唯一内置默认。使用它无需导入；要制作自己的作品，可复制作者目录或导出副本，再检查、打包并普通导入。旧内置星轨配置迁回静线，已经普通导入的用户作品继续保留。

整套目录可复制到任意普通可写目录。正式验收包附带 `bin/SkinAuthoring.exe` 与所需文件，无须安装 SDK 或阅读游戏源码。仓库开发者第一次准备工具可运行：

```powershell
dotnet build tools/SkinAuthoring -c Release
```

## 完成第一款自己的皮肤

内置原件受保护；制作自己的皮肤应复制作者目录或导出副本，修改副本后通过普通导入使用，不直接改安装原件。更新已有作品时保留旧包与源文件，再用当前工具检查、打包并导入新版本；不同转盘样式的写法见 [作者参考](docs/REFERENCE.md)。

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
| `sources/oms-complex/` | 历史星轨作者文件、场景与组合脚本，仅供参考 |
| `sources/aurora-study/` | 实际从模板改色、重新生成、检查、打包的第三方演练作品 |
| `dist/` | 当前普通包与 SHA256；同输入、同工具重复打包得到相同字节；星轨旧包只作历史参考 |
| `Author.ps1` | 新建、生成、检查、打包、普通导入与更新入口 |
| `Build-Skins.ps1` | 从静线作者文件重复打包当前成品；加 `-RebuildAssets` 才重新生成 |
| `Test-Authoring.ps1` | 正常制作、重复打包、错误拒绝与中断成品保护的可重复复核 |
| `docs/` | 制作约定、错误定位、验收步骤和完整公开目录 |

发行套件在 `tool-source/` 保留完整配方与工具源码（仓库位置为 `tools/SkinAuthoring/`）。`SkinRecipe.cs` 是作者可编辑的完整制作配方；它只离线生成普通文件，不由游戏加载，不需要作者编写或编译 DLL。日常制作直接运行附带工具；工具工程源码在完整游戏仓库中编译，套件副本用于审阅与保留可重复制作依据。游戏只消费包内公开设置、图片、场景与可选数值脚本。

## 交付与数据保护

打包先完整检查并捕获当前文件，再写旁边的 `.pending`，通过与游戏普通导入相同的整包检查后替换成品；上一个成品留为 `.previous`。已存在 `.pending` 时要求先保留检查，不猜测删除。新建不会覆盖原作品，链接目录不会被打包，外部作者目录不会被 OMS 修改。

普通导入产生新记录；本工具不假装提供不存在的“覆盖原导入记录”功能。日常快速修改建议登记作者目录并手动重新载入。分享作品使用 `.osk`，导出使用游戏普通皮肤导出入口。

许可：本套件原创几何素材、配方、场景与文档按仓库 MIT 许可发布；无远程素材或隐藏依赖。

当前发行作者套件只携带 `sources/oms-simple` 与 `aurora-study`；complex 源与旧 dist 仅留在仓库作历史参考，不随发行构建。`Test-Authoring.ps1` 验证静线与 Aurora；跨 PowerShell 5 构建器和 .NET 8 作者工具时，成品一致性按 ZIP 内逐文件摘要判断，压缩容器字节可以不同。
