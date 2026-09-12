# 内置双皮肤随构建更新（2026-09-12）

simple／静线与 complex／星轨长期内置，玩家无需导入。两款的 `skin-authoring/sources/` 作者文件是开发构建输入；修改素材、配置或场景后，下一次正常开发启动、build 或 publish 自动生成当前包与配对校验，不再要求先手动更新 `dist/`。

## 构建边界

- `osu.Game.csproj` 调用 [Build-BuiltInSkins.ps1](../../skin-authoring/Build-BuiltInSkins.ps1)，中间产物位于 `osu.Game/obj/canonical/<Configuration>/`，包复制到应用的 `Skins/Canonical/`，对应摘要嵌入本次程序集。IDE 输入检查也纳入源文件。
- 每次 MSBuild 执行检查实际内容，覆盖新增、删除及时间戳未变的修改；内容未变时不重写包与摘要。普通构建不改仓库 `dist/` 或作者文件，也不运行素材配方覆盖手工美术。
- 构建打包不依赖引用游戏程序集的作者工具，避免 Game → 作者工具 → Game 的依赖循环。固定 ZIP 顺序与时间；文件系统链接、缺失 skin.ini、超过 128 MiB／4096 文件拒绝生成。完整皮肤语义仍由公共作者检查与游戏准入验证。
- `dotnet run --project osu.Desktop` 的默认构建会更新；显式 `--no-build`／IDE 强制跳过构建沿用已有产物，需要先构建。游戏运行中不热替换安装原件。
- `build-release.ps1` 从本次 publish 原件组装作者套件同名包与摘要，并用本次发布的作者工具检查两套源文件。历史验收输入、独立示例包与只读原件保护合同不变。
- 同一构建主机保证重复生成稳定；不同 .NET ZIP 实现不要求与历史手工 `dist` 压缩字节相等，要求解包文件一致、当前包与当前内嵌摘要严格配对。

## 验证记录

- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File skin-authoring/Test-BuiltInSkins.ps1`：双包生成、未变内容不重写、同大小同时间戳修改、删除、摘要配对、缺失设置拒绝全部通过。初次暴露的 Windows PowerShell 空路径转换问题已修复并重新通过。
- `dotnet build osu.Desktop.slnf -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m`：通过；BMS 测试工程保留既有 CS8600（TestSceneFilesystemBackedStoryboardFallback.cs:151）和 CA2007（BmsRulesetStatisticsTest.cs:555）两项警告。
- `dotnet build osu.Desktop/osu.Desktop.csproj -c Debug`：通过，零警告。进一步在两套真实源目录各新增本次唯一临时文件，正常 Debug 构建后在 Desktop 原件中找到；删除临时文件并再次正常构建后不再存在。最终两套包的全部文件逐字节等于当前源文件，临时输入已移除。
- 最终 target 仅在实际 `PrepareForBuild` 生成；调整后已再次通过上述真实双包增删验证及 `dotnet publish osu.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false`。另在源目录临时新增文件后执行 `publish --no-build`，确认不会单独刷新皮肤而脱离已编译摘要，临时文件已移除。最初该检查遇到 Debug restore 替换 assets 导致 NETSDK1047，完成对应 RID 的正常 publish 后再执行才计为有效通过。
- `dotnet test osu.Game.Tests/osu.Game.Tests.csproj -c Release --filter 'FullyQualifiedName~CanonicalSkinPackageTest|FullyQualifiedName~TestSceneBuiltInOmsSkins'`：21/21 通过，含内置选择、启动与恢复路径。
- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File build-release.ps1`：完整自包含游戏与独立作者工具 publish、作者语义检查和 ZIP 打包通过。作者工具 single-file publish 仍有既有 IL3000 提示，本轮未改其模式。
- 本次验证包 `release-repo/oms_20260912.zip`，约 335.1 MiB；simple 113 个文件、complex 494 个文件逐字节匹配源目录，游戏原件和发行作者副本摘要一致。Windows 构建包 SHA256：simple `ed1f8a669bdfed5fc16ce01016eb75b63a11d47bbd04536db6a7f4d99fda70c0`；complex `3a180bedb0f76171c767c5e34cfdd61c825daba961552f960edb0993140c3141`。
- `skin-c7-acceptance/Test-ReleaseStartup.ps1 -ReleaseDirectory F:\oms\publish -OutputDirectory F:\oms\artifacts\builtin-build-startup-20260912`：独立副本四轮正常启动、八秒稳定运行、正常关闭、portable/custom root、损坏工作副本保全恢复及同包覆盖更新通过。使用本次 publish 目录作为来源，未重新验证 Windows Shell ZIP 解压；原解压合同不变。没有接触玩家已有保存根，也不把隔离空库检查扩大为已有个人库的迁移签收。

原日志保存在 `artifacts/builtin-*.log`，启动详情在 `artifacts/builtin-build-startup-20260912/results.json`，开发源变动验证脚本在 `artifacts/Test-BuiltInBuildIntegration.ps1`；测试 TRX 在 `osu.Game.Tests/TestResults/builtin-build.trx`。这些本机输出不提交。美术观感与原人工设备签收仍未完成，本次构建更新不替代视觉验收。

文档检查与 `git diff --check` 通过；公开构建包摘要及仓库路径的提示已审阅，不含玩家个人保存位置。
