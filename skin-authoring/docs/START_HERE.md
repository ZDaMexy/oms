# 从零做出第一款皮肤

目标：做出一份有自己名字和配色的完整 BMS / mania 皮肤，在游戏内使用，并能继续修改和分享。先从静线复制，音符、长条、键帽、舞台和声音都已配好；不用先学完整 JSON。

## 1. 准备一个自己的目录

解压完整制作套件到可写目录。在包含 `Author.ps1` 的目录打开 PowerShell。以下命令只对这一次进程放行脚本，不修改系统永久执行策略，也不需要管理员权限。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action new -Output .\work\my-skin -Name '我的第一款皮肤'
```

工具拒绝覆盖已存在的目录。已经做过时换一个新名字，或直接继续编辑原目录；不要删除作品来迁就示例。内置安装原件受保护，始终在自己的副本里修改。

## 2. 改名字和配色

用文本编辑器打开 `work/my-skin/author.json`。保留其它字段，改这些值：

```json
{
  "name": "我的第一款皮肤",
  "author": "你的署名",
  "complex": false,
  "compactLayout": true,
  "bmsAccent": "58d3ea",
  "maniaAccent": "b7a3ff",
  "highlight": "f3ca78",
  "background": "0b1019"
}
```

这是完整制作配方示例。颜色是**不含 `#` 的六位十六进制 RGB**。BMS 与 mania 可以配不同主色。再运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action generate -Source .\work\my-skin
```

预期：名字、两种玩法的设置、图片和演出文件按配方重生。`author.json` 是离线工具的配方，不是游戏必需文件；游戏实际读取 `skin.ini`、图片和场景。

**从现在起若开始手绘 PNG、手改 INI 或 JSON，不再运行 `generate`。** 它不是“保存”，会重新生成这些内容。手写文件直接检查和打包。

## 3. 替换第一张图片

打开 `bms/note-white.png`，在图片编辑器里改色，保留透明背景并另存为同名 PNG。它是静线白键短音符，原图为 128×24；这只是可直接沿用的起点，不是强制尺寸。可先只改颜色，下一次再调整厚度和透明边缘。

同色长条头、身、尾分别为 `head-white.png`、`body-white.png`、`tail-white.png`，它们是独立素材。修改短音符不会自动改变其它三张。mania 的素材在 `mania/` 中，不能只改 BMS 图片就期待两种玩法同时变化。具体文件和字段见[逐字段参考](REFERENCE.md)与模板 `skin.ini`。

## 4. 检查、打包、导入

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action check -Source .\work\my-skin
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action import -Source .\work\my-skin -Output .\dist\my-skin.osk
```

成功会显示检查通过、成品位置以及一个 `work/import-…/my-skin.osk` 导入副本。**把提示的副本拖入 OMS**；正式成品和源文件留在制作目录。可选参数 `-GameExecutable 'D:\OMS\osu!.exe'` 可将副本交给该游戏的普通导入入口。

在设置 → 皮肤分别选择 BMS 与 osu!mania 的皮肤。两个模式各自保存选择；只选择了 BMS，不会替换 mania 的当前作品。先用短键谱确认图片变化，再用长条谱查看头身尾。

`pack` 只生成普通 `.osk`；`import` 和 `update` 都先打包再准备导入副本。`update` 不覆盖游戏里原皮肤记录，导入后要选择新版本。已有成品替换时会留 `.previous`；遇到已有 `.pending`，先保留并检查未完成文件，不要直接清理原作品。

## 5. 更快地反复修改

1. 在游戏设置 → 皮肤点击“打开皮肤文件夹”。
2. 把**完整作品目录**放入打开的 `chartskin/` 下，例如 `chartskin/my-skin/skin.ini`；不能多套一层空目录。
3. 点击“刷新皮肤”发现作品并选择它。
4. 修改源文件，退出游玩和预览，再点“刷新皮肤”。成功后重新进入同一谱面比较。

刷新失败会保留此前已加载的画面。先看错误提示并修正文件，再刷新；不要把“画面没变”理解成编辑器没有保存。游戏不自动监视文件，不在一局中途替换皮肤。旧版登记的外部目录仍只读保留，但现在设置没有新的外部目录注册入口。

## 6. 下一步做什么

| 想要的效果 | 最小学习步骤 |
| --- | --- |
| 两帧闪烁音符 | 把基名改为连续 `name-0.png`、`name-1.png`，参照[素材与编号帧](SKINNING.md#4-图片编号帧与-skinini) |
| 更窄的黑键、更宽的转盘 | 改相应 `[Bms] Keymode` 块，参照[布局](REFERENCE.md#真实车道布局与-ini) |
| 实时分数、进度、旋转装饰 | 直接检查并导入[完整 First Scene 小包](examples/first-scene/README.md) |
| 根据判定换图、跨舞台复用模板 | 跟做[Reference Study](examples/reference-study/README.md) |
| 根据近期击打组合演出 | 在前一例基础上阅读[可选脚本](SCRIPTING.md) |

发布前分别检查目标键数、单双舞台、转盘位置、最窄窗口、暂停/重试和脚本拒绝状态。`check` 能找语法、缺素材和预算问题；游戏里的文字遮挡、实谱密度、听感和屏幕比例仍需要实际查看。
