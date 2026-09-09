# OMS 发行说明

> 当前阶段（Phase 1–2）以 **`oms_YYYYMMDD(.zip)` 便携全量包 + 本地工具覆盖** 为唯一正式发行方式。
> 游戏内在线更新默认禁用，不依赖 `Setup.exe`、MSI 或增量包。

当前正式打包入口为仓库根目录的 `build-release.ps1`，输出位于 `release-repo/`，压缩包命名为 `oms_YYYYMMDD.zip`；同日多次构建会自动追加 `_2`、`_3` 等序号。

## 构建发行包

```powershell
# 推荐：生成正式发行物
.\build-release.ps1

# 如需保留 PDB 供诊断使用
.\build-release.ps1 -KeepPdb
```

`build-release.ps1` 内部仍执行 single-file self-contained `dotnet publish`，补齐 `lazer.ico` / `beatmap.ico`、写入 `portable.ini`，清理非运行时杂项后再打包到 `release-repo/oms_YYYYMMDD(.zip)`。
当前 single-file 发行参数必须同时保留 `IncludeNativeLibrariesForSelfExtract=true` 与 `IncludeAllContentForSelfExtract=true`；若回退成只抽原生库，fresh extract 的便携发行物可能会出现“首次运行先创建 `data/`，随后无窗退出”的冷启动失败。
同时发布无需 SDK 的作者工具、双包源文件与验收工具；发行根的中英双语 `how to update.txt` 和 `Update-OMS.ps1` 已提供保留原便携模式与基础目录 `storage.ini` 的实际更新入口。

> `portable.ini` 是一个空标记文件；只要它存在于 `osu!.exe` 同级目录，游戏便以便携模式启动。

## 发行物内容

打包后的发行包应包含：

| 内容 | 说明 |
| --- | --- |
| `oms_YYYYMMDD(.zip)` | 外层发行压缩包命名；同日多次构建自动追加 `_2`、`_3` |
| `osu!.exe` | 主入口（DesktopGL，自包含 single-file） |
| `portable.ini` | 便携模式标记（空文件） |
| `lazer.ico` / `beatmap.ico` | Windows 文件关联图标 |
| `how to update.txt` | 中英双语手动覆盖更新说明 |
| `Update-OMS.ps1` / `release-files.json` | 保留原运行模式的离线更新工具与逐文件完整性清单 |
| `Skins/Canonical/oms-simple.osk` / `oms-complex.osk` | 随安装携带的原件；简洁款承担正式保底，复杂款仍为展示和默认候选 |
| `skin-authoring/` | 两款普通可导入包、完整源文件、模板、说明和无需 SDK 的制作工具 |
| `skin-c7-acceptance/` | 集中人工验收说明、记录表、输入生成及隔离副本工具 |

游戏入口继续为 single-file，不应把游戏构建目录或 `publish/` 目录名本身打入包。作者工具目录保留其实际需要的全部运行文件，不按游戏入口的 single-file 假设误删。

## 内置皮肤发行约束

从 **Phase 1.1 皮肤系统专项** 开始，OMS 的公开发行物需要逐步满足以下约束：

1. 最终 gameplay 默认面由只读 canonical `oms-simple.osk` 覆盖 mania 与 BMS，并以 `oms-complex.osk` 证明公开作者 API 上限；程序化 `OmsSkin` 只保留到前者通过 parity、完整性、原子恢复与实机 gate。
2. `Argon`、`Triangles`、`DefaultLegacy`、`Retro` 以及其他仅属于 osu!lazer 原生产品表面的内建默认皮肤，不再作为 OMS 的正式内建皮肤对外暴露。
3. mania 与 BMS 的规则集默认 fallback 必须统一逐组件回落到 `oms-simple.osk`，而不是上游原生默认皮肤或长期程序化主题层。
4. 用户自行安装的自定义皮肤仍可作为覆盖层存在，但缺失的组件必须按组件粒度回退到 canonical 包，而不是出现空白或重新落回上游默认资源。
5. `SKIN/SimpleTou-Lazer` 或其后继候选包，在 mania 与 BMS 均完成 OMS-owned 默认路径前，只能被描述为“内置皮肤候选基线”，不得被对外宣称为“已完成的 OMS 默认皮肤”。
6. 在 Phase 1.1 完成前，仓库里即使仍保留上游默认皮肤实现或资源，也只视为过渡态，不构成公开发行标准。

公开发行前的皮肤验收至少应覆盖：

1. 设置页和运行时皮肤选择入口中不再出现 osu!lazer 原生默认皮肤作为 OMS 的默认推荐项。
2. mania 与 BMS 均能在无任何外部皮肤的情况下完整使用 OMS 内置皮肤游玩、结算和浏览 Song Select。
3. BMS 专属组件如 scratch lane、lane cover、gauge bar、clear lamp、note distribution 在缺少自定义资源时都能稳定回退到 OMS 内置实现。
4. BMS playfield 的默认几何、hit target / receptor 与 HUD 默认实现不再依赖临时 feedback 直绘层或硬编码 fallback 才能保持完整可玩。

本节只定义发行约束，不记录易过期的实现进度。当前是否满足这些 gate，以 [主线状态](../mainline/DEVELOPMENT_STATUS.md) 和 [P1-A 状态](../subline/P1-A/DEVELOPMENT_STATUS.md) 为准；恢复边界见 [SKIN_SYSTEM_RECOVERY_20260710.md](SKIN_SYSTEM_RECOVERY_20260710.md)。

## 用户数据存储

### 便携模式（推荐用于首发 release）

当 `portable.ini` 标记文件存在于 `osu!.exe` 同级目录时，默认从同级 `data/` 启动存储；若其中的 `storage.ini` 已指定自定义根，运行时数据位于该目标目录：

| 路径 | 说明 |
| --- | --- |
| `data/` | 便携模式数据根（自动创建） |
| `data/chartbms/` | BMS 谱面目录 |
| `data/chartmania/` | Mania 谱面目录 |
| `data/client.realm` | 主 Realm 数据库 |
| `data/files/` | 通用哈希文件仓库（成绩附件 / replay 等） |
| `data/bms-difficulty-tables/tables.db` | BMS 难度表 sqlite 缓存 |
| `data/storage.ini` | 可选的自定义数据根重定向配置（便携模式下一般不需要） |

未重定向数据根时，整个安装目录（包含程序文件和 `data/`）可直接复制使用。已重定向时还须保全目标数据目录，并保证指针在新位置有效。

### 非便携模式（传统布局）

当 `portable.ini` 不存在时，用户数据存储在系统用户目录：

| 路径 | 说明 |
| --- | --- |
| `%APPDATA%/oms/` | 默认用户数据目录（Release 构建） |
| `%APPDATA%/oms-development/` | Debug 构建隔离目录 |
| `chartbms/` | BMS 谱面目录（位于用户数据目录下） |
| `chartmania/` | Mania 谱面目录（位于用户数据目录下） |
| `client.realm` | 主 Realm 数据库（位于用户数据目录下） |
| `files/` | 通用哈希文件仓库（成绩附件 / replay 等） |
| `bms-difficulty-tables/tables.db` | BMS 难度表 sqlite 缓存 |
| `storage.ini` | 可选的单一自定义数据根重定向配置 |

- `OsuStorage` 通过游戏内迁移流程写入 `storage.ini`，切换到单一自定义数据根。该指针始终保留在启动存储：便携模式是程序旁 `data/storage.ini`，非便携 Release 是 `%APPDATA%/oms/storage.ini`；它不会随数据迁移，也不是放在 exe 同级。

### 游戏内更改数据目录位置

- `Settings -> 常规 -> 安装位置 -> 更改数据目录位置` 只会切换或迁移运行时数据根，不会移动 `osu!.exe` 或其他程序文件。
- 如果选择的是空目录，当前数据内容会直接迁入该目录。
- 如果选择的目录里已有无关文件，OMS 会改用其下的 `oms/` 子目录作为目标数据根，避免把现有文件和游戏数据混在同一层。
- 如果选择的目录本身已经是可用的 OMS 数据目录，OMS 不会重复复制文件，而是写入 `storage.ini` 并在重启后切换过去。
- 便携 build 也遵循同一规则；一旦切到新的数据根，原先同级的 `data/` 目录就不再是当前运行时数据位置。

### 谱库扫描操作口径

- 无论是便携模式还是非便携模式，当前数据根下的 `chartbms/` 与 `chartmania/` 都属于 OMS 托管谱库目录。
- Settings -> Maintenance 现已拆成 `外部谱库` 与 `内部谱库` 两层。两边都提供 `重建` 与 `增量` 两种扫描模式。
- 如果你是手动把 BMS 或 mania 谱面目录复制、解压或移动到 `chartbms/` / `chartmania/` 里，需要进入 `内部谱库` 执行 `扫描内部谱库（重建）` 或 `扫描内部谱库（增量）` 来补扫。
- 如果谱面目录位于其他任意外部路径，需要先在 `外部谱库` 里添加对应的外部谱库文件夹，再执行 `扫描外部谱库（重建）` 或 `扫描外部谱库（增量）`；`内部谱库` 不负责任意外部路径。
- `增量` 模式只补导当前没有 active `FilesystemStoragePath` 记录的目录；若你希望对现有路径重新跑一遍注册/重建索引，应使用 `重建`。

## 版本更新流程

### 便携模式

1. 完全退出 OMS
2. 下载新版本 `oms_YYYYMMDD(.zip)`
3. 完整解压到另一个普通本地目录，在新目录执行下方 `Update-OMS.ps1` 命令，目标指定原安装目录。
4. 工具完成后启动原安装中的 `osu!.exe`。

**无需重新导入** BMS/Mania 目录——保留 `data/`；若已重定向，同时保留 `data/storage.ini` 与目标目录。程序文件更新不会主动迁移这些数据。

### 非便携模式

1. 完全退出 OMS
2. 下载新版本 `oms_YYYYMMDD(.zip)`
3. 完整解压到另一目录并运行同一更新工具。它保持原目标没有 `portable.ini`，保留 `%APPDATA%/oms/` 及其中的 `storage.ini`；不要直接用新包 marker 覆盖目标。
4. 工具完成后启动原安装中的 `osu!.exe`。

**无需重新导入**——用户数据保存在 `%APPDATA%/oms/`；若已迁移，则继续保存在 `storage.ini` 指向的数据根中。

在已解压的新包目录中执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Update-OMS.ps1 -UpdateSourceDirectory . -TargetDirectory "D:\OMS-current"
```

工具先校验整个新包，拒绝链接、路径冲突和运行中覆盖，按文件备份旧程序；不会写入用户保存目录或作者外部目录。更新前文件及中断修复说明保留在目标 `.oms-update-backup-*`。发生中断时保留备份，用完整新包再次执行完成更新；不能猜测删除旧数据。

### 覆盖更新注意事项

1. 当前发行物除 `osu!.exe` 还包含图标与便携标记；`portable.ini` 是否存在决定启动存储，更新必须保持原模式。
2. 必须在程序完全退出后再覆盖文件；运行中替换可执行文件会遇到 Windows 文件锁。
3. 便携模式下如果误删 `portable.ini`，下次启动将不再继续使用同级 `data/` 作为数据根。
4. 若使用自定义数据根，保留启动存储中的 `storage.ini` 和目标数据。非便携安装新增 `portable.ini` 会让程序改读 `data/`，从而绕过原 `%APPDATA%/oms/storage.ini`；这可能表现为曲库消失，不能据此重建或删除旧数据。
5. 覆盖新包后不会触发 Velopack 或安装器自更新链；当前仅保留手工覆盖这一离线更新路径。

随包中英 `how to update.txt` 与本页一致。完整性清单校验文件内容，ReadOnly 属性只用于减少误改；两款安装原件在发行、验收启动和更新后设置只读，实际包完整性仍由程序检查。

## 冒烟测试

构建后可使用仓库自带脚本验证启动：

```powershell
.\SmokeTestDesktop.ps1        # 8 秒非交互启动验证
```

冒烟结果只对生成它的发行包有效；每次候选包都应重新记录冷启动结果，并把日期、commit 与结果写入 P1-F `CHANGELOG`，不要复用本文中的历史通过结论。

## 在线功能状态

- 游戏内更新：**已禁用**（`IsInAppUpdateEnabled => false`）
- Velopack 初始化：**已跳过**
- API / OAuth / SignalR：**默认端点已清空**
- 在线排行榜 / 谱面下载 / 聊天 / 多人：**已隐藏**
- 远程静态资源 fallback：**已被离线模式屏蔽**

> 联网功能将在 Phase 3 统一启用。
