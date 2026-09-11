# 发行副本的启动、保存位置与退出核对

这些步骤验证完整发行副本能启动、使用预期保存目录并正常关闭。它们不替代两款皮肤的画面、输入设备、视频和长时体验签收。每次记录实际发行包、程序和 `release-files.json` 的校验，以及这一轮新产生的日志。

**结果须对应实际检查的发行包。** 每次以该包的 `release-files.json` 校验和检查输出的 `results.json` 为准，不能借用另一候选包的通过记录。2026-09-11 的旧完整自解压候选包没有使用安装旁的便携标记，曾进入当前账户已有自定义保存根并运行 Realm schema 57 迁移。事后数据与指针已完整逐字节保全到私有事故记录；没有事前该根快照，不能声称无损或已经回滚，也不在本清单披露账户路径。旧窗口/进程 smoke 不能证明保存位置正确。以下流程只适用于修正后的完整自包含多文件包。

## 可重复执行的隔离检查

随包 [Test-ReleaseStartup.ps1](Test-ReleaseStartup.ps1)从完整新发行目录另建副本，依次检查便携首次启动、自定义保存位置、损坏工作副本恢复，以及同一完整发行包覆盖后的再次启动。来源目录保持原样；每轮保存独立日志与结果。覆盖项证明实际完整覆盖恢复，不冒充跨版本升级。脚本不处理非便携真实账户根，也不代签画面、声音或设备体验。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Test-ReleaseStartup.ps1 -ReleaseDirectory "D:\OMS-C7-fresh" -OutputDirectory "D:\OMS-C7-startup-proof-new"
```

`ReleaseDirectory` 必须是尚无 `data/` 的完整新发行目录，不能选已经使用的人工验收 `app/`，也不能使用旧完整自解压包。`OutputDirectory` 必须尚不存在，且与来源互不包含。启动前如发现已有游戏进程或 OMS 管道，脚本停止；不会关闭其它游戏。成功结果要求全部加载标记、八秒稳定运行、实际用户库与日志使用预期保存根、准确工作副本、便携缓存留在安装旁，以及最多两次正常关闭请求后退出码为零和完整退出日志。若脚本尚未提供某项证据，须按下文补齐，不能用其它项代替。若必须结束本次创建的进程，该轮明确失败，现场保留在输出的 `evidence/`，总结果见 `results.json`。无效输出路径在建立目录前拒绝，不能覆盖旧记录。

下文保留逐步人工复核方法，用于审阅脚本结果或记录实际设备差异。

## 启动前

1. 使用 Windows 资源管理器将修正后的完整自包含多文件 ZIP 解压到新的普通目录。确认 `osu!.exe` 旁保留清单中的全部运行文件、BMS/mania DLL、`portable.ini` 和原件；不要只复制 exe 或从开发构建目录挑少量文件。首次安装检查时必须还没有 `data/` 和 `cache/`。
2. 检查当前没有 `osu!`、`osu` 或 `OMS` 进程，也没有占用 `oms` 的命名管道；若有，先保留当前状态，暂不启动验收副本，不关闭别人的游戏。管道只枚举，不连接、不发消息。
3. 确认两款 `Skins/Canonical/*.osk` 与发行清单一致，并实际保留只读属性；记录来源和副本属性，不在检查前手动补属性。启动命令不传皮肤、谱面或协议参数。当前普通程序即使遇到第二实例竞态，零参数只会拒绝第二实例，不会把文件导入另一个正在使用的游戏。
4. 保留启动前的隔离副本内容与指针记录，并明确写下预期用户库与缓存位置。旧完整自解压会将程序基准目录移到 TEMP；只取消完整自解压仍会影响物理玩法 DLL 发现。两类旧包都不能拿当前账户的已有资料尝试验证。非便携检查使用独立 Windows 账户或虚拟机。

发行 ZIP 明确保存两款原件的 DOS 只读属性，本机实际 Windows Shell 解包会保留；`Expand-Archive` 和 .NET 解包会忽略该属性，因此不能用它们的结果声称 Windows 标准解包已验证，也不能把解包器差异说成所有工具都支持。游戏始终只读捕获并核对固化内容摘要，完整性保护不依赖 DOS 属性；本检查则必须如实验证发行属性，拒绝预先补标后冒充通过。

```powershell
Get-Process -Name 'osu!', 'osu', 'OMS' -ErrorAction SilentlyContinue | Select-Object Id, ProcessName, Path
[IO.Directory]::GetFiles('\\.\pipe\') | Where-Object { [IO.Path]::GetFileName($_) -match '(^|[-_.])oms($|[-_.])' }

$install = [IO.Path]::GetFullPath('D:\OMS-C7-fresh')
$startedUtc = [DateTime]::UtcNow
$game = Start-Process -FilePath (Join-Path $install 'osu!.exe') -WorkingDirectory $install -WindowStyle Normal -PassThru
```

前两条命令有匹配结果时不继续执行启动行。此人工观察示例显示游戏窗口，供观察者操作；上文自动检查则保持隐藏并只向它自己创建、核对身份的窗口发送正常关闭请求。`Start-Process` 返回的进程必须持续存活；快速正常退出也可能只是被第二实例保护拒绝，不能视为启动通过。

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
| 两种玩法可发现 | 安装旁 BMS/mania 玩法 DLL 与清单一致，并从本轮加载记录或实际页面确认两种玩法可用；仅文件存在不算运行成功 |
| 便携缓存使用独立位置 | 本轮缓存实际建立于 `<安装目录>/cache/`；记录完整隔离副本中的相对路径和新文件，不将其当作用户库 |

逐项审查新日志的 `[error]`、未处理异常、`Too many unhandled exceptions`、缺失原生库和第二实例拒绝信息。存在错误时记录具体消息并调查，不按进程存活判通过。

便携日志位于 `<安装目录>/data/logs/`。自定义保存副本由 `Create-CustomRootCopy.ps1` 建立：指针固定保留在 `app-custom/data/storage.ini`，实际用户库、日志、皮肤与谱面在 `custom-data/`。新日志还应有 `Storage successfully changed to <custom-data>`，没有 `Custom storage location could not be used`；核对实际 `client.realm` 路径，不只读取指针文字。保留启动前后指针及用户皮肤校验。

便携缓存则由桌面宿主的便携选项控制，始终位于运行副本程序旁 `cache/`；自定义用户库不会把它移到 `custom-data/`。用户库、日志、皮肤工作副本和缓存要分别核对。若日志显示打开了预设副本以外的数据库，立即将该轮记为失败，停止后续覆盖或恢复步骤，保全实际现场；不得以“窗口已打开”继续填写通过，也不得猜测删除或降级该数据库。事后备份只能证明保全了事后状态。

非便携完整冷启动在独立 Windows 账户或虚拟机进行，保持程序旁没有 `portable.ini`。Release 的基础根为 `%APPDATA%/oms/`；自定义指针若存在，也位于该基础根。不要更改当前账户的默认根来伪造隔离通过。

## 正常关闭

在本轮新打开的游戏窗口中正常关闭游戏，并完成界面上的退出确认。当前退出流程第一次可能先回到 Intro；待界面返回后可以第二次请求关闭，再等待片尾结束。不要操作另一份游戏或用任务管理器结束进程代替正常关闭。下列命令仅观察上文保存的本轮进程是否退出：

```powershell
$game.Refresh()
if ($game.HasExited) { $game.ExitCode } else { '游戏尚未退出，请在本轮窗口中完成正常关闭并等待退出动画。' }
```

正常关闭证据为进程退出码 `0`，以及本轮日志中的 `Host execution state changed to Stopping`、`Host execution state changed to Stopped`。只有这一进程无法正常关闭时才结束它并保留现场；这样的运行可记录已完成启动，但不能填写正常退出通过。`SkinManager` 的恢复/作者资源工作会在正常退出时完成释放，不能用强制结束替代关闭验收。

退出后再进行自定义根复制、覆盖更新或安装损坏恢复。每次覆盖更新后重新重复启动、保存位置和正常关闭检查；确认原便携模式、指针、作者外部文件与用户皮肤仍保留。
