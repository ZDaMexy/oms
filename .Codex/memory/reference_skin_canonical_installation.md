---
name: reference_skin_canonical_installation
description: canonical 只读原件、工作副本恢复、旧记录与导出 authority 的排查边界
metadata:
  node_type: memory
  type: reference
---

# Canonical 简洁皮肤安装与旧数据排查

状态与签收只读 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)，完整合同见 [P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)。这里记录迁移时容易混淆的证据，不替代验收。

- 安装原件是程序目录 `Skins/Canonical/oms-simple.osk`，SHA-256 锚点嵌入 `osu.Game`，不是从旁边可一起改写的 manifest 取得信任。每次启动先验证原件，再处理当前数据根 `skin-canonical/oms-simple.osk`；原件缺失/损坏时，即使工作副本完整也不能绕过安装修复。
- 工作副本通过 held no-follow 父目录、完整临时 archive 的 `Flush(true)` 与 no-replace rename 发布。坏的既存副本先以 held file handle 改名到 `*.preserved-*` 保全；中断会留下完整旧件或完整临时件，重启重新从原件恢复，不按名称猜删遗留件。目录、reparse 或文件锁不能被当成缺失并清空。hardlink 的其它名字指向的内容不能被修改。
- 真正 canonical 是完整 archive admission、capsule capture、普通 `BmsLegacySkin` factory 与 shared author-package preparation 成功后的具体实例。`IsCanonicalSkin` 使用实例信任表；`Protected=true`、固定 ID、名字或某个类型都不是 canonical 视觉 authority。
- protected Realm 元数据 `Hash=oms.skin.canonical.simple.v1` 是跨发行稳定的记录标记，**不是**内容 hash。真实运行内容用 capsule revision，安装内容用发行 SHA-256。不要拿这个字段替代内容校验。
- 受支持旧 journal 先对 exact 旧 `OmsSkin` / 新 canonical protected record 恢复，然后才迁移旧元数据。未解 journal 时，原本没有 protected row 就必须仍然没有；先补新 row 再 Retry 会制造之前缺失的恢复证据。缺 fingerprint/manifest/tombstone/disposition 的旧 intent 仍然 Invalid，不补字段、不猜删。
- 旧固定 ID 也可能含不能确认归属的用户数据。只有完整 exact protected metadata 且无用户 files 的已知内置记录能更新/移除；未知同 ID 的 Name/type/hash/files 保全并要求修复。不要因 ID 看起来内置而覆盖或删除。
- 新导入旧 `OmsSkin` metadata 的 archive 改用普通 BMS/mania parser；已经存在的非 protected 旧 Oms record 只在内存中按 exact 原用户 files 构造同一普通 parser，不能改 Realm type/hash/refs 或补旧嵌入资源。选择和 reload 两条路径都须覆盖。
- protected canonical 导出按钮需显式允许已验证 instance 对应 exact metadata。普通 `.osk` 导出应保留整份作者包，不能因 protected record 没有 Realm files 而导出空包；未知固定 ID 带用户 files 的记录又必须导出其真实 files，不能按 ID 静默替换成简洁包。
- `EnsureMutableSkin` 的 canonical 副本也必须由完整普通 archive 导入，不能延用旧“只造同类型无文件记录”的内置皮肤方式。author-manifest 选择走已有异步 publication，创建副本返回不等于已切换；测试应等真实 current pair，legacy editor 仍按现有合同冻结。
- 核对恢复/UI时同时看 `IsGameplaySkinInstallationAvailable`、安装修复说明、journal 状态与真实 current revision。内存中有经过验证的简洁包，不表示未解的旧数据操作已经允许进入游玩；修复界面占位 skin 也绝不是可玩的备用主题。

人工观感与设备签收不能由上述安装检查代替。旧 `OmsSkin` 代码的保留用于旧证据与人工对照，不允许被新的普通导入、默认选择或安装故障回落重新接回产品链。

- 首次外部目录登记先要`chartskin`物理防重叠证明；旧测试预先mkdir曾遮蔽新安装无法登记的真实缺口。生产仅通过显式`OpenForFirstWorkspace`创建空自有根：coordinator lease、journal Missing、无任何旧filesystem声明、作者held proof排除祖先重叠、新根native no-follow/no-replace/identity复核。不要把创建能力加给scanner/recovery Open；旧记录但根缺失仍保全并提供恢复原目录指引。
- Windows PowerShell `-Command`后追加路径不等于脚本参数，路径含空格会被重新解释；native junction测试用独立`.ps1`与`-File`、`ArgumentList`逐个传参。ZipArchiveEntry读流不可seek，使用`ReadAllRemainingBytesToArray()`。

- C7 完整包不能只测槽位ready和原生owner隐藏：hud.text一次接管会同时遮掉准确率/进度，必须观察新的实际文字和状态；同一测试host同时挂两玩法时必须给各自事件子树缓存本玩法processor，并沿Player顺序接NewResult/RevertResult，否则可能只有判定事件而分数恒零。当前未验修改和继续顺序见[暂停检查点](../../doc_md/other/SKIN_SYSTEM_C7_RESUME_20260909.md)。
