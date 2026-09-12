# OMS 发行说明

> 当前阶段（Phase 1–2）以 **`oms_YYYYMMDD(.zip)` 便携全量包 + 本地工具覆盖** 为唯一正式发行方式。
> 游戏内在线更新默认禁用，不依赖 `Setup.exe`、MSI 或增量包。

当前正式打包入口为仓库根目录的 `build-release.ps1`，输出位于 `release-repo/`，压缩包命名为 `oms_YYYYMMDD.zip`；同日多次构建会自动追加 `_2`、`_3` 等序号。

## 当前人工验收包

**2026-09-12 最新体验结论：星轨现版动画、美术安排和精细度不符合用户预期，总体不可用。** 本轮按要求暂时收尾，simple/complex 优化留待新对话；下述包保留为安装与作品对照，不代表可接受的最终美术或已通过发行签收。现有 ZIP 和已组装目录没有被重打包或改写，随包说明/空白清单仍是生成时快照；当前反馈与剩余修改见 [P1-A 状态](../subline/P1-A/DEVELOPMENT_STATUS.md)和[当前集中清单](SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)。

截至 2026-09-12，当前实际完整包为 `release-repo/oms_20260911_startup-fix-final.zip`，已组装成可直接运行的 `release-repo/oms-skin-c7-acceptance-20260911-startup-fix-final`。按其中 `README.md` 运行 `Start-Acceptance.ps1`，即可在独立副本中选择两款成品、导入观察输入和第三方皮肤，使用同一作者工具修改、检查、打包并更新导入副本；集中步骤见 [人工验收指南](../../skin-c7-acceptance/README.md)。本次真实包组装已在 PS5、无 Git/SDK 的环境完成，来源保持原样。先前 `_4`、`preview-fix` 包和验收结果保留为历史，当前使用入口以上述新包为准。

四轮实际启动与正常退出已通过；四轮结束后的首次便携根和共享自定义根额外只读副本确认 `bms`、`mania` 均为可用，未把两处最终状态写成四份时点快照。当前仍保留 V-001～V-004 0/4、V-005 未签收，以及画面、声音、设备和长期体验人工门。`oms-simple` 承担正式保底，`oms-complex` 是展示包和默认候选，未擅自设为首次默认选择。完整摘要和证据范围见 [P1-F 状态](../subline/P1-F/DEVELOPMENT_STATUS.md)与 [C7 验证记录](SKIN_SYSTEM_C7_VALIDATION_20260909.md)。

本轮还在旧 `preview-fix` 安装的独立副本上完成真实跨版本更新：更新工具保留用户文件、数据库和便携模式，两款旧只读原件留在备份；旧工作副本由游戏首次启动自动换成新版简洁款，随后正常退出，第三处测试根的额外只读副本确认两玩法可用。没有打开原用户数据库，也不把测试库在实际启动后的正常变化说成全程字节不变。最终制品独立复核已通过；上述结果不代替人工签收。

## 构建发行包

```powershell
# 推荐：生成正式发行物
.\build-release.ps1

# 如需保留 PDB 供诊断使用
.\build-release.ps1 -KeepPdb
```

`build-release.ps1` 当前执行 self-contained、多文件 `dotnet publish`（`PublishSingleFile=false`），保留完整运行文件与玩法 DLL，补齐 `lazer.ico` / `beatmap.ico`、写入 `portable.ini` 后打包到 `release-repo/oms_YYYYMMDD(.zip)`。解压完整 ZIP 后直接运行 `osu!.exe`，无需另装 .NET。
2026-09-11 的实际启动揭示旧完整自解压方式会把程序基准目录移到 TEMP，未读到实际安装旁的便携标记并误入已有保存位置。因此游戏改为多文件发行；仅取消完整自解压、仍将玩法 DLL 留在 single-file 内也不能满足现有玩法发现方式。修正后的 `_4` 与 `preview-fix` 历史包已有隔离启动记录；当前 `oms_20260911_startup-fix-final.zip` 重新完成 Windows 标准解压、便携与自定义保存、坏工作副本恢复、同包覆盖及上述跨版本更新后正常启动。两玩法可用及实际保存/缓存位置均有各自证据。这些自动安装结果不代表 Skin V1 或公开发行的人工门已经签收。
同时发布无需 SDK 的作者工具、双包源文件与验收工具；发行根的中英双语 `how to update.txt` 和 `Update-OMS.ps1` 已提供保留原便携模式与基础目录 `storage.ini` 的实际更新入口。

> `portable.ini` 是一个空标记文件；只要它存在于 `osu!.exe` 同级目录，游戏便以便携模式启动。

## 发行物内容

打包后的发行包应包含：

| 内容 | 说明 |
| --- | --- |
| `oms_YYYYMMDD(.zip)` | 外层发行压缩包命名；同日多次构建自动追加 `_2`、`_3` |
| `osu!.exe` | 主入口（DesktopGL，完整自包含多文件发行） |
| 同级 DLL、运行文件及运行资源目录 | 游戏和 BMS/mania 必需内容；必须随完整 ZIP 一起解压与覆盖 |
| `portable.ini` | 便携模式标记（空文件） |
| `lazer.ico` / `beatmap.ico` | Windows 文件关联图标 |
| `how to update.txt` | 中英双语手动覆盖更新说明 |
| `Update-OMS.ps1` / `release-files.json` | 保留原运行模式的离线更新工具与逐文件完整性清单 |
| `Skins/Canonical/oms-simple.osk` / `oms-complex.osk` | 随安装携带的原件；简洁款承担正式保底，复杂款仍为展示和默认候选 |
| `skin-authoring/` | 两款普通可导入包、完整源文件、模板、说明和无需 SDK 的制作工具 |
| `skin-c7-acceptance/` | 集中人工验收说明、记录表、输入生成及隔离副本工具 |

游戏入口仍是 `osu!.exe`，但不能只复制这个文件；同级完整运行文件与玩法 DLL 都是发行物的一部分。压缩包内直接放这些内容，不把游戏构建目录或 `publish/` 目录名本身打入包。作者工具保持已独立验证的 self-contained single-file 方式，完整复制其实际发布输出；游戏发行方式的修正不要求改动作者工具。

作者工具每次发布到本次唯一新目录，再仅将此次完整输出复制为发行包的 `skin-authoring/bin/`。不把仓库旧 `bin/` 中的残留文件、手工内容或运行数据混入发行物，也不猜测清理旧目录。

## 内置皮肤发行约束

从 **Phase 1.1 皮肤系统专项** 开始，OMS 的公开发行物需要逐步满足以下约束：

1. gameplay 正式保底由只读 canonical `oms-simple.osk` 覆盖 mania 与 BMS，`oms-complex.osk` 展示普通作者路径可制作的组合演出；程序化 `OmsSkin` 仅为历史对照保留到 parity、完整性、原子恢复与实机 gate 全满足后移除，不作为安装原件损坏时的替代外观。
2. `Argon`、`Triangles`、`DefaultLegacy`、`Retro` 以及其他仅属于 osu!lazer 原生产品表面的内建默认皮肤，不再作为 OMS 的正式内建皮肤对外暴露。
3. mania 与 BMS 的规则集默认 fallback 必须统一逐组件回落到 `oms-simple.osk`，而不是上游原生默认皮肤或长期程序化主题层。
4. 用户皮肤缺少必要组件时按组件粒度补齐 canonical 内容，作者明确关闭的可选装饰不恢复；安装只读原件缺失或损坏时明确提示修复安装并阻止进入谱面，不以临时外观掩盖问题。
5. 当前交付名为 `oms-simple.osk` 与 `oms-complex.osk`；旧候选 `SKIN/SimpleTou-Lazer` 不作为当前成品身份或回退版本，复杂款也不因展示完成而自动成为首次默认选择。
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
| `cache/` | 程序旁的便携运行缓存；不属于用户库，也不随 `storage.ini` 改位置 |

未重定向数据根时，整个安装目录（包含程序文件和 `data/`）可直接复制使用。已重定向时还须保全目标数据目录，并保证指针在新位置有效。

桌面入口把便携模式同时传给底层宿主，避免用户资料已在程序旁、缓存却写入当前账户默认位置。本次真实发行的用户库与缓存位置已分别核对；后续候选包仍须复验，不能只凭 `portable.ini` 存在判断通过。

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

1. 当前发行物除 `osu!.exe` 还包含完整运行文件、玩法 DLL、图标与便携标记；必须完整更新。`portable.ini` 是否存在决定启动存储，更新必须保持原模式。
2. 必须在程序完全退出后再覆盖文件；运行中替换可执行文件会遇到 Windows 文件锁。
3. 便携模式下如果误删 `portable.ini`，下次启动将不再继续使用同级 `data/` 作为数据根。
4. 若使用自定义数据根，保留启动存储中的 `storage.ini` 和目标数据。非便携安装新增 `portable.ini` 会让程序改读 `data/`，从而绕过原 `%APPDATA%/oms/storage.ini`；这可能表现为曲库消失，不能据此重建或删除旧数据。
5. 覆盖新包后不会触发 Velopack 或安装器自更新链；当前仅保留手工覆盖这一离线更新路径。

随包中英 `how to update.txt` 与本页一致。完整性清单校验文件内容，ReadOnly 属性只用于减少误改。发行 ZIP 显式保存两款原件的 DOS 只读属性，本机 Windows 资源管理器的解包链实际保留；`Expand-Archive` / .NET 解包会忽略该属性，不能混称所有解包器都保留。发行验收使用 Windows 标准解包并核验原来源及副本属性，不在启动前补标来制造通过。游戏只读捕获并核对正式保底的固化摘要，保护不依赖 DOS 属性，游戏不为此增加原件属性写入。

更新工具只对本次私有暂存的新 canonical 文件设 ReadOnly；旧原件通过不覆盖的 move 原样移入 `old/`，再将新件 move 到目标，全程不写旧原件的内容或属性，已有或更新期间增加的硬链接也不因此改到作者原件。其它程序文件继续使用 `File.Replace` 保存旧件。canonical 两次 move 之间可能暂缺安装原件，故必须完全退出游戏；中断时保留原 `Applying` 记录、旧文件与新暂存，再次运行同一完整包完成覆盖，旧现场仍不清理。这里不宣称 canonical 安装覆盖为单次原子替换；游戏工作副本的原子恢复合同保持不变。

## 冒烟测试

开发环境可使用仓库自带脚本作有限启动观察：

```powershell
.\SmokeTestDesktop.ps1        # 有限的 8 秒启动观察，不证明实际保存位置
```

正式发行使用 [启动、保存位置与退出核对](../../skin-c7-acceptance/STARTUP-CHECK.md)，从完整新发行目录建立隔离副本，分别验证首次便携、自定义保存、工作副本恢复和完整覆盖后启动。每轮核对实际用户库、日志、安装原件与工作副本、缓存位置及正常退出；非便携真实运行应在独立 Windows 账户或虚拟机完成。

2026-05-09 的 single-file 冷启动记录保留在 [P1-F 历史](../subline/P1-F/CHANGELOG.md)，但窗口/进程观察未证明实际保存根，不能再作便携隔离通过依据。2026-09-11 本轮误入当前账户既有自定义根并运行 Realm schema 57 迁移后，已私下完整保全事后数据与指针；没有该根事前快照，不能宣称无损或已回滚，本文不披露其路径。最终多文件包本次复验前后，账户 bootstrap、事故根与原 G1 根的全文件字节和属性保持相同；这不能倒推首次事故前后相同。以后每个候选包仍须重新记录实际结果，不能复用旧通过结论。

## 在线功能状态

- 游戏内更新：**已禁用**（`IsInAppUpdateEnabled => false`）
- Velopack 初始化：**已跳过**
- API / OAuth / SignalR：**默认端点已清空**
- 在线排行榜 / 谱面下载 / 聊天 / 多人：**已隐藏**
- 远程静态资源 fallback：**已被离线模式屏蔽**

> 联网功能将在 Phase 3 统一启用。
