# OMS 皮肤制作套件

用普通图片、`skin.ini` 和 JSON 制作 BMS / osu!mania 游玩皮肤，再以 `.osk` 分享。无需编写 DLL；安装版套件附带制作工具，无需 .NET SDK。

## 从这里开始

| 你想做什么 | 阅读入口 |
| --- | --- |
| 第一次制作：改名字、配色、图片，装进游戏 | [从零做出第一款皮肤](docs/START_HERE.md) |
| 找音符、长条、键帽、判定、血槽、BGA 对应文件 | [图解与元素查表](docs/SKINNING.md#3-按画面元素查找文件) |
| 移动舞台、改变轨宽、制作动画和实时信息 | [完整制作手册](docs/SKINNING.md) |
| 跟做一个小而完整的跨玩法场景 | [First Scene](docs/examples/first-scene/README.md) |
| 练习换图、模板实例和击打历史组合 | [Reference Study](docs/examples/reference-study/README.md) |
| 查 JSON 字段、绑定、状态机、上限及错误码 | [逐字段参考](docs/REFERENCE.md) |
| 查所有公开部件与适用范围 | [部件目录](docs/CATALOG.md) |
| 编写可选的数值脚本 | [脚本指南](docs/SCRIPTING.md) |

## 五步开始

在**本套件目录**打开 PowerShell，使用尚不存在的作品目录名：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action new -Output .\work\my-skin -Name '我的第一款皮肤'
# 用文本编辑器修改 work/my-skin/author.json 中的 name、author、bmsAccent、maniaAccent。
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action generate -Source .\work\my-skin
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action check -Source .\work\my-skin
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action import -Source .\work\my-skin -Output .\dist\my-skin.osk
```

把工具提示的**导入副本**拖入 OMS，在设置 → 皮肤分别选择 BMS 与 osu!mania 的皮肤。只改图片、INI、场景或脚本时直接 `check → import`；`generate` 会按配方重新生成文件，会覆盖手写改动。

反复修改可使用设置中的“打开皮肤文件夹”，把作品放入 `chartskin/<作品名>/`，退出游玩/预览后点“刷新皮肤”。不需要注册外部目录，没有自动文件监视或游玩中重载。原组件编辑器可保存独立副本，完整场景与脚本仍在文件中编辑。

## 文件去哪里找

| 路径 | 内容 |
| --- | --- |
| `sources/oms-simple/` | 唯一内置静线的完整作者源；从它复制自己的作品 |
| `sources/aurora-study/` | 完整改色范例；过程见[演练](docs/WORKSHOP.md) |
| `docs/examples/` | 带操作步骤和预期效果的学习文件；按各自 README 使用 |
| `Author.ps1` | `new / generate / check / pack / import / update` |
| `bin/SkinAuthoring.exe` | 发行套件自带的工具 |
| `dist/` | 普通 `.osk` 成品；请保留作者源文件 |

仓库中的 `sources/oms-complex/` 和旧包仅保留历史参考，不随当前套件发行。静线素材与许可见[素材说明](docs/SIMPLE_ARTWORK.md)；原创素材、配方与文档按仓库 MIT 许可发布。

## 仓库开发者

只有从源码仓库使用且尚无工具时，在**仓库根目录**的新 PowerShell 中运行：

```powershell
. .\UseDevelopmentStorage.ps1
dotnet build tools/SkinAuthoring -c Release
```

此开发存储入口不属于发行套件。普通作者直接使用附带工具即可。游戏正常 build/run/publish 会从静线作者源准备内置包；不需要为了制作自己的作品覆盖安装原件或运行构建脚本。开发验证状态在源码仓库 `doc_md/subline/P1-A/` 维护，不是制作套件的使用前提。
