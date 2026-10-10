# OMS 发行说明

> 当前阶段（Phase 1–2）以 **`oms_YYYYMMDD(.zip)` 便携全量包 + 本地工具覆盖** 为唯一正式发行方式。
> 游戏内在线更新默认禁用，不依赖 `Setup.exe`、MSI 或增量包。

当前正式打包入口为仓库根目录的 `build-release.ps1`，输出位于 `release-repo/`，压缩包命名为 `oms_YYYYMMDD.zip`；同日多次构建会自动追加 `_2`、`_3` 等序号。

## 当前发行范围

静线为唯一内置、默认与保底，外观打磨按用户决定暂停；星轨已退役，不再是启动依赖或构建对象。历史作者文件保留参考，旧内置星轨选择迁回静线，普通用户导入皮肤不清除；不再要求星轨视觉签收。

2026-10-03 已形成绑定 clean `63f50c7` 的完整候选，ZIP 与 fresh publish 载荷一致，便携、自定义根、工作副本恢复及同包覆盖后隔离启动通过。随后按用户要求删除生成的 ZIP、publish 与两启动副本的程序/缓存，保留日志、结果、清单和恢复备份，见 [P1-F 状态](../subline/P1-F/DEVELOPMENT_STATUS.md#最近一次验证)及 [IR 发布记录](OMS_IR_CLIENT_RELEASE_20261003.md)。该结果不代签 Windows Shell 解包、跨版本、新版作者套件独立制作、真实下载或真人 IR 游玩；旧 9 月候选不代表当前源码全部行为，设备/长期及整体公开发行仍未完成。日常通过 VS Code 非调试启动验收，发行构建由用户自行执行，不要求再次打包。

## 此前人工验收包

以下发行物保留生成时快照；用户已放弃星轨，不能把它们的双包范围当成当前构建要求。静线整体画面、设备及长期体验仍未签收，V-001～V-004 仍为 0/4，V-005 未签收。当前验收范围与操作见[集中清单](SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md)和[验收指南](../../skin-c7-acceptance/README.md)。

- `oms_20260911_startup-fix-final.zip` 与同名集中验收目录：历史双包候选，在 PS5、无 Git/SDK 环境完成组装，验证便携、自定义根、损坏工作副本恢复、同包覆盖后的正常启动和退出；两玩法可用有额外只读检查证据。对旧 `preview-fix` 副本另验证跨版本更新保留用户文件、数据库、模式及两款旧只读原件备份。这些记录见 [C7 验证](SKIN_SYSTEM_C7_VALIDATION_20260909.md)，不证明当前版本已做同样的整包安装复验。
- `oms_20260912.zip`：历史双内置自动构建候选；`oms_20260912_2.zip`：其后修复已保存星轨配置的冷启动异常。构建、隔离桌面补验及限制见[构建记录](SKIN_BUILTIN_BUILD_20260912.md)。
- `oms_20260912_4.zip`：9 月单内置完整候选，退役迁移和四轮隔离启动结果见 [P1-F 状态](../subline/P1-F/DEVELOPMENT_STATUS.md)。这些旧制品及其随包空白清单和验证结果保留原身份。

## 构建发行包

在非系统盘 checkout 的仓库根目录打开 PowerShell；每个新进程先按[开发磁盘约束](../../AGENTS.md#开发磁盘约束)加载存储入口，首次切换后重新 restore 对应工程。

```powershell
. .\UseDevelopmentStorage.ps1
# 推荐：生成正式发行物
.\build-release.ps1

# 如需保留 PDB 供诊断使用
.\build-release.ps1 -KeepPdb
```

`build-release.ps1` 当前执行 self-contained、多文件 `dotnet publish`（`PublishSingleFile=false`），保留完整运行文件与玩法 DLL，补齐 `lazer.ico` / `beatmap.ico`、写入 `portable.ini` 后打包到 `release-repo/oms_YYYYMMDD(.zip)`。解压完整 ZIP 后直接运行 `osu!.exe`，无需另装 .NET。
2026-09-11 的实际启动揭示旧完整自解压方式会把程序基准目录移到 TEMP，未读到实际安装旁的便携标记并误入已有保存位置。因此游戏改为多文件发行；仅取消完整自解压、仍将玩法 DLL 留在 single-file 内也不能满足现有玩法发现方式。修正后的 `_4` 与 `preview-fix` 历史包已有隔离启动记录；历史 `oms_20260911_startup-fix-final.zip` 已完成 Windows 标准解压、便携与自定义保存、坏工作副本恢复、同包覆盖及上述跨版本更新后正常启动。两玩法可用及实际保存/缓存位置均有各自证据。这些自动安装结果不代表 Skin V1 或公开发行的人工门已经签收。
发布无需 SDK 的作者工具、静线源文件、完整制作手册、Aurora 练习与验收工具；`build-release.ps1` 递归复制 `skin-authoring/docs/`，其中包括基础教程、脚本指南及 First Scene / BGA Layout / Reference Study。候选包需在无源码仓库的目录中复查文档链接、素材与练习，不能用仓库内验证代替实际发行内容检查。complex 源和旧 dist 仅在仓库保留历史参考，不随当前发行构建；发行根的中英双语 `how to update.txt` 和 `Update-OMS.ps1` 已提供保留原便携模式与基础目录 `storage.ini` 的实际更新入口。

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
| `Skins/Canonical/oms-simple.osk` | 唯一随安装携带的内置原件与正式保底 |
| `skin-authoring/` | 静线普通可导入包、完整源文件、模板、完整离线手册/素材/例子和无需 SDK 的制作工具 |
| `skin-c7-acceptance/` | 集中人工验收说明、记录表、输入生成及隔离副本工具 |

游戏入口仍是 `osu!.exe`，但不能只复制这个文件；同级完整运行文件与玩法 DLL 都是发行物的一部分。压缩包内直接放这些内容，不把游戏构建目录或 `publish/` 目录名本身打入包。作者工具保持已独立验证的 self-contained single-file 方式，完整复制其实际发布输出；游戏发行方式的修正不要求改动作者工具。

作者工具每次发布到本次唯一新目录，再仅将此次完整输出复制为发行包的 `skin-authoring/bin/`。不把仓库旧 `bin/` 中的残留文件、手工内容或运行数据混入发行物，也不猜测清理旧目录。

## 内置皮肤发行约束

从 **Phase 1.1 皮肤系统专项** 开始，OMS 的公开发行物需要逐步满足以下约束：

1. gameplay 唯一内置和正式保底为只读 canonical `oms-simple.osk`，覆盖 mania/BMS；complex 不参与构建、安装完整性或启动，历史源仅作参考。旧 `OmsSkin` 继续仅为人工对照，不能回到产品回退链。
2. `Argon`、`Triangles`、`DefaultLegacy`、`Retro` 以及其他仅属于 osu!lazer 原生产品表面的内建默认皮肤，不再作为 OMS 的正式内建皮肤对外暴露。
3. mania 与 BMS 的规则集默认 fallback 必须统一逐组件回落到 `oms-simple.osk`，而不是上游原生默认皮肤或长期程序化主题层。
4. 用户皮肤缺少必要组件时按组件粒度补齐 canonical 内容，作者明确关闭的可选装饰不恢复；安装只读原件缺失或损坏时明确提示修复安装并阻止进入谱面，不以临时外观掩盖问题。
5. 当前内置交付名为 `oms-simple.osk`；旧 complex 配置迁回 simple，普通用户导入包不清除。
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

随包中英 `how to update.txt` 与本页一致。完整性清单校验文件内容，ReadOnly 属性只用于减少误改。当前发行 ZIP 为唯一静线原件写入 DOS 只读属性；历史候选的 Windows 资源管理器解包链曾实际保留该属性，每个新候选仍须复验；`Expand-Archive` / .NET 解包会忽略该属性，不能混称所有解包器都保留。发行验收使用 Windows 标准解包并核验原来源及副本属性，不在启动前补标来制造通过。游戏只读捕获并核对正式保底的固化摘要，保护不依赖 DOS 属性，游戏不为此增加原件属性写入。

更新工具只对本次私有暂存的新 canonical 文件设 ReadOnly；旧原件通过不覆盖的 move 原样移入 `old/`，再将新件 move 到目标，全程不写旧原件的内容或属性，已有或更新期间增加的硬链接也不因此改到作者原件。其它程序文件继续使用 `File.Replace` 保存旧件。canonical 两次 move 之间可能暂缺安装原件，故必须完全退出游戏；中断时保留原 `Applying` 记录、旧文件与新暂存，再次运行同一完整包完成覆盖，旧现场仍不清理。这里不宣称 canonical 安装覆盖为单次原子替换；游戏工作副本的原子恢复合同保持不变。

## 冒烟测试

开发环境可使用仓库自带脚本作有限启动观察：

```powershell
. .\UseDevelopmentStorage.ps1
.\SmokeTestDesktop.ps1        # 有限的 8 秒启动观察，不证明实际保存位置
```

正式发行使用 [启动、保存位置与退出核对](../../skin-c7-acceptance/STARTUP-CHECK.md)，从完整新发行目录建立隔离副本，分别验证首次便携、自定义保存、工作副本恢复和完整覆盖后启动。每轮核对实际用户库、日志、安装原件与工作副本、缓存位置及正常退出；非便携真实运行应在独立 Windows 账户或虚拟机完成。

2026-05-09 的 single-file 冷启动记录保留在 [P1-F 历史](../subline/P1-F/CHANGELOG.md)，但窗口/进程观察未证明实际保存根，不能再作便携隔离通过依据。2026-09-11 本轮误入当前账户既有自定义根并运行 Realm schema 57 迁移后，已私下完整保全事后数据与指针；没有该根事前快照，不能宣称无损或已回滚，本文不披露其路径。最终多文件包本次复验前后，账户 bootstrap、事故根与原 G1 根的全文件字节和属性保持相同；这不能倒推首次事故前后相同。以后每个候选包仍须重新记录实际结果，不能复用旧通过结论。

## 在线功能状态

- 游戏内更新：**已禁用**（`IsInAppUpdateEnabled => false`）
- Velopack 初始化：**已跳过**
- 旧 osu! API / OAuth / SignalR：**默认端点已清空**
- 原官网账号/API、官网谱面下载 / 聊天 / 多人：**已隐藏或禁用**
- 独立 OMSIR：**固定官方服务的按需试运行**，原账号窗口直接登录后允许保存的新局上传；未登录可主动读具体谱面公开榜，操作和真人门沿 [P3-IR](../subline/P3-IR/DEVELOPMENT_STATUS.md)。当前工作区能力不冒充已公开发行的客户端
- 远程静态资源 fallback：**已被离线模式屏蔽**

> 独立 IR 试运行不启用旧在线链或旧 API 端点，也不关闭 Phase 1.x 人工/发行门。多来源与 LR2 历史榜沿[已采纳专项](../subline/P3-IR/DEVELOPMENT_PLAN.md#多播放器与-lr2-历史榜实施)实施，目标宿主与真人门独立保留。公共 BMS 难度表 URL、Ginger Rush / 616 BMS 和 Sayobot mania 镜像下载仍是独立窄例外，能力及未签收实网/大包体验见 [P1-A 状态](../subline/P1-A/DEVELOPMENT_STATUS.md)，边界见 P1-A [BMS](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#第三方-bms-浏览下载) / [mania](../subline/P1-A/TECHNICAL_CONSTRAINTS.md#sayobot-mania-浏览下载)；剩余发行组合沿 [P1-F](../subline/P1-F/DEVELOPMENT_PLAN.md) / [P1-G](../subline/P1-G/DEVELOPMENT_PLAN.md)。
