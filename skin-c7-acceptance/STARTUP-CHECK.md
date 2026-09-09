# 发行副本的启动、保存位置与退出核对

这些步骤验证完整发行副本能启动、使用预期保存目录并正常关闭。它们不替代两款皮肤的画面、输入设备、视频和长时体验签收。每次记录实际发行包、程序和 `release-files.json` 的校验，以及这一轮新产生的日志。

## 启动前

1. 将发行 ZIP 完整解压到新的普通目录；不要从开发构建目录复制少量文件。首次安装检查时必须还没有 `data/`。
2. 检查当前没有 `osu!`、`osu` 或 `OMS` 进程，也没有占用 `oms` 的命名管道；若有，先保留当前状态，暂不启动验收副本，不关闭别人的游戏。管道只枚举，不连接、不发消息。
3. 确认两款 `Skins/Canonical/*.osk` 与发行清单一致，设为只读。启动命令不传皮肤、谱面或协议参数。当前普通程序即使遇到第二实例竞态，零参数只会拒绝第二实例，不会把文件导入另一个正在使用的游戏。

```powershell
Get-Process -Name 'osu!', 'osu', 'OMS' -ErrorAction SilentlyContinue | Select-Object Id, ProcessName, Path
[IO.Directory]::GetFiles('\\.\pipe\') | Where-Object { [IO.Path]::GetFileName($_) -match '(^|[-_.])oms($|[-_.])' }

$install = [IO.Path]::GetFullPath('D:\OMS-C7-fresh')
$startedUtc = [DateTime]::UtcNow
$game = Start-Process -FilePath (Join-Path $install 'osu!.exe') -WorkingDirectory $install -WindowStyle Hidden -PassThru
```

前两条命令有匹配结果时不继续执行启动行。`Start-Process` 返回的进程必须持续存活；快速正常退出也可能只是被第二实例保护拒绝，不能视为启动通过。

## 读取本轮证据

等待本轮新日志出现并完成启动，再保留至少八秒稳定运行。冷启动首次加载较慢时继续等待实际加载完成，不以进程活着八秒替代完整加载。只读取创建时间不早于 `$startedUtc` 的本轮 `*.runtime.log`，避免混入复制副本中的旧日志。

现有日志中的可复查标记：

| 结果 | 现有标记或文件 |
| --- | --- |
| 使用独立便携根 | `Portable mode active. Data root: <安装目录>\data` |
| 图形设备已初始化 | `Renderer initialised!`，并保留同段实际设备/驱动信息 |
| 游戏主循环已运行 | `Host execution state changed to Running` |
| 实际打开预期用户库 | `Opened realm "<实际保存目录>\client.realm"` |
| 首次设置和设置页已加载 | `Loaded FirstRunSetupOverlay!`、`Loaded SettingsOverlay!` |
| 界面已进入启动页或主菜单 | `OsuScreenStack` 的 `entered Intro...` 或 `entered MainMenu...`；第一次向导未完成时如实记录停留位置 |
| 正式保底工作副本已准备 | 实际保存目录中的 `skin-canonical/oms-simple.osk` 与本轮安装原件校验一致 |

逐项审查新日志的 `[error]`、未处理异常、`Too many unhandled exceptions`、缺失原生库和第二实例拒绝信息。存在错误时记录具体消息并调查，不按进程存活判通过。

便携日志位于 `<安装目录>/data/logs/`。自定义保存副本由 `Create-CustomRootCopy.ps1` 建立：指针固定保留在 `app-custom/data/storage.ini`，实际用户库、日志、皮肤与谱面在 `custom-data/`。新日志还应有 `Storage successfully changed to <custom-data>`，没有 `Custom storage location could not be used`；核对实际 `client.realm` 路径，不只读取指针文字。保留启动前后指针及用户皮肤校验。

非便携完整冷启动在独立 Windows 账户或虚拟机进行，保持程序旁没有 `portable.ini`。Release 的基础根为 `%APPDATA%/oms/`；自定义指针若存在，也位于该基础根。不要更改当前账户的默认根来伪造隔离通过。

## 正常关闭

只向本轮 `Start-Process` 返回的进程请求关闭。当前退出流程第一次可能先回到 Intro；待界面返回后可以第二次请求关闭，再等待片尾结束。

```powershell
$game.Refresh()
if (-not $game.HasExited) { $game.CloseMainWindow() }
# 等待现有退出动画，再 Refresh；若尚未退出，可对同一 $game 再 CloseMainWindow 一次。
```

正常关闭证据为进程退出码 `0`，以及本轮日志中的 `Host execution state changed to Stopping`、`Host execution state changed to Stopped`。只有这一进程无法正常关闭时才结束它并保留现场；这样的运行可记录已完成启动，但不能填写正常退出通过。`SkinManager` 的恢复/作者资源工作会在正常退出时完成释放，不能用强制结束替代关闭验收。

退出后再进行自定义根复制、覆盖更新或安装损坏恢复。每次覆盖更新后重新重复启动、保存位置和正常关闭检查；确认原便携模式、指针、作者外部文件与用户皮肤仍保留。
