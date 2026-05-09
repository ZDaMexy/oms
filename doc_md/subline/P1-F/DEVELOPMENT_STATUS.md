# P1-F 开发进度：发行后置与离线发布验收

> 最后更新：2026-05-09

## 当前阶段

- `P1-F` 已完成子线建档。
- `OsuGameDesktop.CreateStorage()` 已按 `portable.ini -> data/` 接通便携数据根；当前正式打包入口已明确为 `build-release.ps1 -> release-repo/oms_YYYYMMDD(.zip)`，发行根目录保持 `osu!.exe` + `portable.ini` + `lazer.ico` + `beatmap.ico`。
- 手工覆盖更新路径已重新审计：用户数据仍与程序文件分离，游戏内在线更新继续保持禁用；当前主要操作风险是运行中覆盖文件，以及误删 `portable.ini` / `storage.ini` 导致数据根切换。
- 游戏内在线更新仍保持禁用，当前重点不是恢复安装器/在线更新，而是继续收口公开发行物产品面与最终 release gate。

## 进度矩阵

| 事项 | 状态 | 备注 |
| --- | --- | --- |
| 子线建档 | 已完成 | 四件套已建立 |
| 便携发布基线 | 已完成 | `build-release.ps1 -> oms_YYYYMMDD(.zip)`、`portable.ini -> data/` 与覆盖更新路径结论已对齐 |
| 在线更新关闭基线 | 已完成 | 当前仍维持离线优先，不恢复在线更新；手工覆盖后不会进入 Velopack 自更新链 |
| 公开发行物产品面验收 | 进行中 | 依赖 `P1-A` 的默认皮肤与 release gate 收尾 |
| 发布口径同步 | 进行中 | 需持续联动 `../../other/RELEASE.md` |

## 验证记录

- 沿用既有验证结论：Release publish 后 `portable.ini` 会触发 `data/` 自动生成，当前正式压缩包命名为 `oms_YYYYMMDD(.zip)`。
- 本轮补充审计：覆盖更新继续按“退出程序 -> 解压覆盖 -> 再启动”执行，不会触发 Velopack 或安装器自更新；便携模式需保留 `portable.ini` 与 `data/`，自定义数据根场景需保留 `storage.ini`。
- 为未来可能的内部 OMS 版号切换补上了最小兼容护栏：`ChangelogOverlay.ShowBuild(string)` 与 `OsuConfigManager.Migrate()` 现已兼容不带上游 `-stream` 后缀的 `oms_YYYYMMDD` 版号。
- 验证：`dotnet build .\osu.Game\osu.Game.csproj -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。