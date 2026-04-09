# OMS 发行说明

> 当前阶段（Phase 1–2）以 **Portable.zip 全量包 + 手工文件覆盖** 为唯一正式发行方式。
> 游戏内在线更新默认禁用，不依赖 `Setup.exe`、MSI 或增量包。

## 构建 Portable.zip

```powershell
# Release 构建
dotnet publish osu.Desktop -c Release -r win-x64 --self-contained -o publish/

# 打包（排除调试符号）
Compress-Archive -Path publish/* -DestinationPath OMS-Portable.zip
```

> 如需保留 PDB 以便崩溃诊断，可在打包时不排除 `*.pdb`。

## 发行物内容

打包后的 Portable.zip 应包含：

| 内容 | 说明 |
| --- | --- |
| `osu!.exe` | 主入口（DesktopGL） |
| `*.dll` / `*.json` | 运行时依赖 |
| `bass*.dll` / `SDL*.dll` | 原生库 |
| `runtimes/` | .NET 自包含运行时 |

不应包含：`*.pdb`（可选保留）、`publish/` 目录名本身。

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

- 上述第 2 条尚未满足。BMS 默认层已完成七批 OMS-owned slice，但 mania 侧当前已完成第一批 Stage / Column / Key shell 的组件、layout、behaviour、shared asset、首批 shell colour 与首批 stage-local key asset 收口，以及第二批首个 stage-local note/hold asset slice、首个 explicit normal-note / hold-note-head / hold-note-tail / hold-note-body component slice、首个 shared judgement asset slice、首个 shared judgement-position slice、首个 shared bar-line config slice、首个 explicit judgement / bar-line / combo counter / hitburst component slice；当前这批 non-column shared preset 在 mixed-stage 路径下也已固定复用第一 stage preset，不再落回 total-columns legacy 默认值，`OmsNotePiece` / `OmsHoldNoteHeadPiece` / `OmsHoldNoteTailPiece` / `OmsHoldNoteBodyPiece` / `OmsManiaJudgementPiece` / `OmsHitExplosion` / `OmsManiaComboCounter` / `OmsBarLine` 也已升格为实际 OMS-owned 组件，但 note / hold / combo/HUD / bar-line 仍继续复用部分 legacy 语义，因此 mania 默认路径迁移仍未完成。

## 用户数据存储

| 路径 | 说明 |
| --- | --- |
| `%APPDATA%/oms/` | 默认用户数据目录（Release 构建） |
| `%APPDATA%/oms-development/` | Debug 构建隔离目录 |
| `songs/` | BMS 谱面目录（位于用户数据目录下） |

- 用户数据目录与程序文件夹**分离**，覆盖更新不会影响已导入的谱面、成绩、设置和难度表缓存
- 用户可通过游戏内设置迁移数据目录（写入 `storage.ini`）

## 版本更新流程

1. 下载新版本 `OMS-Portable.zip`
2. 解压覆盖到当前程序文件夹（覆盖所有文件）
3. 启动 `osu!.exe`

**无需重新导入** BMS 目录——用户数据保存在 `%APPDATA%/oms/`，不受程序文件覆盖影响。

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
