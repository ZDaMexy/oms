---
name: reference-build-and-test
description: OMS 构建入口、当前恢复基线与 C# Dev Kit 误判地雷
metadata:
  node_type: memory
  type: reference
---

# 构建与测试召回

- build 入口：`osu.Desktop.slnf`；CLI `dotnet test <csproj> --no-build -c Release|Debug` 最可靠。
- 第三方程序集通过 deps/runtimeconfig 从 NuGet cache 解析，输出目录没有单独 DLL 不一定是错误。
- 真实测试工程：`osu.Game.Tests`、BMS.Tests、Mania.Tests。

## C# Dev Kit 地雷

`osu.Game` 是 library，但因引用 NUnit 抽象 test scene，Dev Kit 会误识别成测试容器并用缺 runtimeconfig 的 testhost 启动，随后报告 AutoMapper/测试平台程序集缺失。该红节点是已确认 benign：不要通过把 `osu.Game` 变成真实 test project、复制全依赖或移走上游测试基类来修。使用真实测试工程或 CLI。

## 2026-07-10 恢复基线

- BMS 1005/1005；mania 默认 OMS 资源 1/1。
- mania full 787/791：4 个既有 HoldNote auto-frame 期待失败。
- core skin 57/62：Argon/已删除 ruleset 的旧测试失配。
- Release 0 error / 20 warnings：9 个 MessagePack NU1902 在 restore/build 重复 + BMS test CS8600/CA2007。

不要恢复全局 `NoWarn` 隐藏依赖告警；安全升级单独治理。最新数字最终以 mainline STATUS 为准。
