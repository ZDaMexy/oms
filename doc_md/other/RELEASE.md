# OMS 发行说明

> 当前阶段（Phase 1–2）以 **Portable.zip 全量包 + 手工文件覆盖** 为唯一正式发行方式。
> 游戏内在线更新默认禁用，不依赖 `Setup.exe`、MSI 或增量包。

## 构建 Portable.zip

```powershell
# Release 构建（single-file，自包含）
dotnet publish osu.Desktop -c Release -r win-x64 --self-contained `
	-p:PublishSingleFile=true `
	-p:IncludeNativeLibrariesForSelfExtract=true `
	-p:GenerateDocumentationFile=false `
	-p:DebugSymbols=false `
	-p:DebugType=None `
	-o publish/

# 保留 Windows 文件关联图标（single-file 下需旁路文件）
Copy-Item osu.Desktop/lazer.ico publish/lazer.ico -Force
Copy-Item osu.Desktop/beatmap.ico publish/beatmap.ico -Force

# 写入便携标记，使程序启动时自动使用 data/ 子目录存储所有用户数据
New-Item -Path publish/portable.ini -ItemType File -Force | Out-Null

# 清理非运行时杂项
Get-ChildItem publish -Filter *.lib -Recurse | Remove-Item -Force
Get-ChildItem publish -Filter *.xml -Recurse | Remove-Item -Force

# 打包
Compress-Archive -Path publish/* -DestinationPath OMS-Portable.zip
```

> 如需保留 PDB 以便崩溃诊断，可在打包时不排除 `*.pdb`。
> `portable.ini` 是一个空标记文件；只要它存在于 `osu!.exe` 同级目录，游戏便以便携模式启动。

## 发行物内容

打包后的 Portable.zip 应包含：

| 内容 | 说明 |
| --- | --- |
| `osu!.exe` | 主入口（DesktopGL，自包含 single-file） |
| `portable.ini` | 便携模式标记（空文件） |
| `lazer.ico` / `beatmap.ico` | Windows 文件关联图标 |

不应包含：松散的 `*.dll` / `*.deps.json` / `*.runtimeconfig.json` / `*.xml` / `*.lib`、`publish/` 目录名本身。

## 内置皮肤发行约束

从 **Phase 1.1 皮肤系统专项** 开始，OMS 的公开发行物需要逐步满足以下约束：

1. 终端产品只保留 **OMS 内置皮肤** 作为唯一内建默认皮肤族，覆盖全局 UI、mania 与 BMS。
2. `Argon`、`Triangles`、`DefaultLegacy`、`Retro` 以及其他仅属于 osu!lazer 原生产品表面的内建默认皮肤，不再作为 OMS 的正式内建皮肤对外暴露。
3. mania 与 BMS 的规则集默认 fallback 必须统一回落到 OMS 内置皮肤，而不是回落到上游原生默认皮肤。
4. 用户自行安装的自定义皮肤仍可作为覆盖层存在，但缺失的组件必须按组件粒度回退到 OMS 内置皮肤，而不是出现空白或重新落回上游默认资源。
5. `SKIN/SimpleTou-Lazer` 或其后继候选包，在 mania 与 BMS 均完成 OMS-owned 默认路径前，只能被描述为“内置皮肤候选基线”，不得被对外宣称为“已完成的 OMS 默认皮肤”。
6. 在 Phase 1.1 完成前，仓库里即使仍保留上游默认皮肤实现或资源，也只视为过渡态，不构成公开发行标准。

公开发行前的皮肤验收至少应覆盖：

1. 设置页和运行时皮肤选择入口中不再出现 osu!lazer 原生默认皮肤作为 OMS 的默认推荐项。
2. mania 与 BMS 均能在无任何外部皮肤的情况下完整使用 OMS 内置皮肤游玩、结算和浏览 Song Select。
3. BMS 专属组件如 scratch lane、lane cover、gauge bar、clear lamp、note distribution 在缺少自定义资源时都能稳定回退到 OMS 内置实现。
4. BMS playfield 的默认几何、hit target / receptor 与 HUD 默认实现不再依赖临时 feedback 直绘层或硬编码 fallback 才能保持完整可玩。

当前仓库状态说明：

- 上述第 2 条尚未完全满足。BMS 默认层与 BMS fallback 已完成七批 OMS-owned slice 收口；mania 侧已完成 shell / preset 接线与 8 类 OMS-owned 组件升格，`OmsOwnedSkinComponentContractTest` + `TestSceneOmsBuiltInSkin` 已锁定主要 runtime 语义，但公开发行物产品面、少量 legacy config/asset lookup 兼容与最终 release gate 仍在收尾。详见 [../mainline/DEVELOPMENT_STATUS.md](../mainline/DEVELOPMENT_STATUS.md)。

## 用户数据存储

### 便携模式（推荐用于首发 release）

当 `portable.ini` 标记文件存在于 `osu!.exe` 同级目录时，所有用户数据存储在同级的 `data/` 子目录：

| 路径 | 说明 |
| --- | --- |
| `data/` | 便携模式数据根（自动创建） |
| `data/chartbms/` | BMS 谱面目录 |
| `data/chartmania/` | Mania 谱面目录 |
| `data/client.realm` | 主 Realm 数据库 |
| `data/files/` | 通用哈希文件仓库（成绩附件 / replay 等） |
| `data/bms-difficulty-tables/tables.db` | BMS 难度表 sqlite 缓存 |
| `data/storage.ini` | 可选的自定义数据根重定向配置（便携模式下一般不需要） |

便携模式下，整个安装目录（包含程序文件和 `data/`）可直接拷贝到 U 盘或其他位置使用。

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

- `OsuStorage` 支持通过游戏内迁移流程写入 `storage.ini`，把全部运行时数据迁移到单一自定义数据根

### 谱库扫描操作口径

- 无论是便携模式还是非便携模式，当前数据根下的 `chartbms/` 与 `chartmania/` 都属于 OMS 托管谱库目录。
- Settings -> Maintenance 现已拆成 `外部谱库` 与 `内部谱库` 两层。两边都提供 `重建` 与 `增量` 两种扫描模式。
- 如果你是手动把 BMS 或 mania 谱面目录复制、解压或移动到 `chartbms/` / `chartmania/` 里，需要进入 `内部谱库` 执行 `扫描内部谱库（重建）` 或 `扫描内部谱库（增量）` 来补扫。
- 如果谱面目录位于其他任意外部路径，需要先在 `外部谱库` 里添加对应的外部谱库文件夹，再执行 `扫描外部谱库（重建）` 或 `扫描外部谱库（增量）`；`内部谱库` 不负责任意外部路径。
- `增量` 模式只补导当前没有 active `FilesystemStoragePath` 记录的目录；若你希望对现有路径重新跑一遍注册/重建索引，应使用 `重建`。

## 版本更新流程

### 便携模式

1. 下载新版本 `OMS-Portable.zip`
2. 解压覆盖到当前程序文件夹（覆盖所有文件，保留 `data/` 目录不动）
3. 启动 `osu!.exe`

**无需重新导入** BMS/Mania 目录——用户数据保存在 `data/` 子目录中，不受程序文件覆盖影响。

### 非便携模式

1. 下载新版本 `OMS-Portable.zip`
2. 解压覆盖到当前程序文件夹（覆盖所有文件）
3. 启动 `osu!.exe`

**无需重新导入**——用户数据保存在 `%APPDATA%/oms/`；若已迁移，则继续保存在 `storage.ini` 指向的数据根中。

## 冒烟测试

构建后可使用仓库自带脚本验证启动：

```powershell
.\SmokeTestDesktop.ps1        # 8 秒非交互启动验证
```

## 在线功能状态

- 游戏内更新：**已禁用**（`IsInAppUpdateEnabled => false`）
- Velopack 初始化：**已跳过**
- API / OAuth / SignalR：**默认端点已清空**
- 在线排行榜 / 谱面下载 / 聊天 / 多人：**已隐藏**
- 远程静态资源 fallback：**已被离线模式屏蔽**

> 联网功能将在 Phase 3 统一启用。
