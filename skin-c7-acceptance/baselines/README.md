# 原 C6 失败消息存证

[c6-failures.json](c6-failures.json)来自提交 `de9eb3e39e84c2d0dffe49f313a37eae61149b80` 的隔离副本。2026-09-11 在原源码、原断言上重新运行指定项目，恢复 core 六项与 mania 四项原失败；两个 TRX 的来源摘要、实际命令和开始/结束时间均保留在 JSON 中。原临时记录已经丢失，因此这次恢复运行单独标明来源，不能冒充当时完整检查的原 TRX。

每项只保存名称、错误首行类别和完整原始消息，不包含 StackTrace、账户名或私有用户数据位置。原 TRX 留在本地开发证据目录中。JSON 中的命令用于说明实际来源，比较工具不会执行这些命令，也不依赖这些开发路径。

[Compare-FailureBaseline.ps1](../Compare-FailureBaseline.ps1)直接读取此随包文件，与本轮真实 core 和 mania TRX 逐项比较，只规范换行和首尾空白。不能修改消息、删失败或按数量抵消差异。默认必须保持原六项/四项身份和消息；唯一显式关闭选项 `-ResolvedCoreSampleFixture` 要求原 `TestSampleUpdatedBeforePlaybackWhenNotPresent` 在本轮有唯一 `Passed`，其它五项/四项仍完全一致。缺失、跳过、重复或继续失败均拒绝。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Compare-FailureBaseline.ps1 -ActualCoreTrx "本轮-core.trx" -ActualManiaTrx "本轮-mania.trx" -ResolvedCoreSampleFixture -OutputFile "本轮-逐项比较.json"
```

该命令在 `skin-c7-acceptance/` 目录执行；只在本轮确有上述唯一通过事实时加关闭选项。比较器运行成功不代替完整项目检查或人工签收。
