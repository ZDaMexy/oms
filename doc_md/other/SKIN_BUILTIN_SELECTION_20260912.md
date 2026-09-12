# 两款长期内置选择验证（2026-09-12）

用户明确 simple 与 complex 必须长期随游戏内置，不能要求先导入第三方包。本次让安装中的两个只读 `.osk` 都注册为受保护的固定选项；列表显示 `OMS 简洁` 与 `OMS 星轨`。首次默认和必要件保底继续由 simple 独占。两包作品内容没有再次修改，美术验收仍未完成。

## 玩家路径与身份

- 无导入的新保存目录能直接选择两款；循环切换可回到原实例，重启保留星轨选择。
- 两款原件不能删除或覆盖；导出保留完整包，编辑走普通可修改副本。
- 星轨使用独立 `ProtectedBuiltIn` 来源，不能获得 `ProtectedFallback` 权限；两个固定 ID 均不能登记成外部或受管目录。
- 两份工作副本各用自己的文件名，损坏副本先保全再恢复；缺失或损坏任一安装原件要求修复完整安装，不能借旧副本掩盖损坏。

## 验证记录

基线：当前 master `08cbcdf`，开始时工作区干净；fetch 成功，已记录远端落后当前分支两次提交，无分叉。本次验证使用测试临时目录，没有打开玩家保存根。

首轮 focused 编译发现新增测试缺少 `DummyRenderer` 命名空间；补齐后 Release focused **77/77** 通过，覆盖安装包、首次注册、选择/重启、恢复和来源身份。该失败不是有效运行结果，修正记录保留。

新增实际游玩检查首次因测试默认 mania 首音符在 1,000,000 ms、却等待 2,000 ms 而超时；改用现有四键测试谱面后 **1/1** 通过。检查直接选择内置 complex，真实 BMS 与 mania 输入产生得分，并验证两边作者信息区显示，不调用 Import。

core `FullyQualifiedName~Skin` 为 **1345/1350**；5 项剩余失败已逐项比对原名称、类别和完整消息一致。mania full 首次为 **857/867**：除原 4 项自动演奏问题，6 项旧检查仍写死单内置选择。更新注册资源、列表顺序、循环切换与随机选择的双内置合同；原自动演奏断言未动。

BMS Release full 为 **2212 Passed / 2 Failed / 15 Skipped**（未提供核验备份根）。两项失败分别是旧迁移与安装异常列表写死单内置数量；按双固定身份修正后，包含正常迁移、三种未知记录/未解恢复、异常安装和真实双玩法输入的 focused **6/6** 通过。中间一次测试投影交给 Realm 的 `Select` 不受支持，改为同一读取作用域内 `AsEnumerable` 后物化；该失败日志保留。全套后只修改测试断言，未再改变生产代码，不把聚焦通过伪写为第二次 full 全绿。

最终 mania full **863/867**，只剩原 4 项自动演奏帧数失败；与 core 5 项由 `Compare-FailureBaseline.ps1 -ResolvedCoreSampleFixture` 逐项核对名称、类别和完整消息，通过。Release 构建 **0 警告 / 0 错误**，输出中的双包 SHA-256 均与发行锚点一致；formatter whitespace 通过。BMS 测试编译仍有既有 CS8600/CA2007，未改写既有失败断言或屏蔽警告。

文档检查初次发现本报告缺索引，补齐后通过（162 Markdown、1464 相对链接、149 本地锚点、105 memory wiki 链）；保留既有公开校验值/通用路径提醒。`git diff --check` 通过。

主要命令（构建与测试串行）：

```powershell
dotnet test osu.Game.Tests/osu.Game.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~CanonicalSkinPackageTest|FullyQualifiedName~TestSceneBuiltInOmsSkins|FullyQualifiedName~TestSceneStartupSkinMigration|FullyQualifiedName~SkinFilesystemStorageResolverTest|FullyQualifiedName~GameplaySkinDocumentIdentity'
dotnet test osu.Game.Tests/osu.Game.Tests.csproj -c Release --no-build --no-restore --filter 'FullyQualifiedName~Skin'
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~TestBuiltInComplexPlaysBmsAndManiaWithoutImport'
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj -c Release --no-build --no-restore
dotnet test osu.Game.Rulesets.Bms.Tests/osu.Game.Rulesets.Bms.Tests.csproj -c Release --no-restore --filter 'FullyQualifiedName~TestEvidencedRetiredReferenceMetadataMigratesOnlyAfterSafeRecovery|FullyQualifiedName~TestFreshFailedCanonicalInstallationKeepsSkinRepairListUsableWithoutCreatingProtectedEvidence|FullyQualifiedName~TestBuiltInComplexPlaysBmsAndManiaWithoutImport'
dotnet test osu.Game.Rulesets.Mania.Tests/osu.Game.Rulesets.Mania.Tests.csproj -c Release --no-restore
dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m
```

原始日志和 TRX 归档于本地 `artifacts/skin-builtin-selection-20260912/`（忽略目录）。历史布局与原失败身份见[静线首轮报告](SKIN_SIMPLE_LAYOUT_20260912.md)，本轮不改写历史验收。
