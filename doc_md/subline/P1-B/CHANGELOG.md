# P1-B 变动日志

## 2026-05-09

### desktop public settings surface 收口：安全隐藏 upstream mouse/touch/tablet 分区

- `OsuGameDesktop` 现已 override `CreateSettingsSubsectionFor(InputHandler)`，在 desktop Settings -> 输入 中对 `ITabletHandler`、`TouchHandler` 与 `MouseHandler` 返回 `null`，因此上游通用的数位板 / 触屏点击 / 鼠标 subsection 不再继续暴露给最终产品面。
- 这次变更明确属于 **安全隐藏** 而不是 runtime 删除：mouse/touch/tablet handler 与 `MouseDisableButtons` / `MouseDisableWheel` / `ConfineMouseMode` / `TouchDisableGameplayTaps` 的消费链保持不变。
- 该裁剪故意保持在 `OsuGameDesktop` 层而不是下移到 `OsuGameBase`，避免同步改写 test scene / 非 desktop host 的输入设置装配。
- 验证：`dotnet build osu.Desktop -p:Configuration=Release -p:GenerateFullPaths=true -m -verbosity:m` 通过。

## 2026-04-20

### 子线正式建档

- `P1-B` 已建立独立目录与四件套文档。
- 当前仅完成文档结构治理，未新增代码、构建或测试执行。