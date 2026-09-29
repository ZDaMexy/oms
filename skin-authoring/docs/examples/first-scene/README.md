# First Scene：第一份可运行场景

这是**完整可打包的小皮肤**，不是 JSON 片段。目录含 `skin.ini`、清单、完整场景、可选脚本和 `scene/white.png`。普通音符、长条、键帽等未改部件继承 OMS 静线；这里只替换公共文字信息层，适用于 BMS 和 mania。没有 `author.json`，因此直接 `check / pack / import`，不用 `new / generate`。

在制作套件目录运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action check -Source .\docs\examples\first-scene
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action import -Source .\docs\examples\first-scene -Output .\dist\first-scene.osk
```

把工具提示的副本拖入游戏，在两种玩法各选择 `OMS First Scene`，进入一张非零时长谱面。这份练习使用上方全屏坐标，目标是看清编写机制；正式作品还应按自己的最小窗口调整位置。

| 节点 | 预期看到什么 | 写法 |
| --- | --- | --- |
| `lesson.score` | 左上方青色分数，随真实得分变化 | `score.value → text` 绑定 |
| `lesson.progress` | 青色横条，首个物件前为空，随谱面可玩进度增长 | `timing.progress → width`；父容器固定宽度 |
| `lesson.status` | READY → RUNNING | 运行状态机写文字；恢复由当前状态重建 |
| `lesson.accent` | 金色图形每 2 秒旋转一周 | `tween` 写 rotation，与游戏时间同步 |
| 同一金色图形 | 授权后按血量放大到 1～1.25 倍 | 脚本只写 scale-x / scale-y；不与旋转轨迹争写属性 |

先拒绝两项可选脚本权限：文字、进度和旋转仍工作，图形缩放为 1。再允许 `gameplay.snapshot.read` 与 `scene.numeric.write`；血量为 0.8 时图形缩放应为 `1 + 0.8 × 0.25 = 1.2`。撤销权限后回到缩放 1，分数和进度不消失。暂停时整个游玩场景冻结，文字、旋转与进度都保留上一帧；示例虽然声明了 PAUSED 状态，但不能用它制作暂停时即时更新的菜单。恢复时按当前运行状态重建，重试/回退按新时刻重建。

按下面顺序修改，每次 `check → import` 或在固定目录刷新：

1. 把 `lesson.turn.end.time` 从 `2000` 改为 `4000`，旋转变为每 4 秒一周。
2. 把 `lesson.score.properties.colour` 改为 `#ffffffff`，分数变白。
3. 把脚本 `mul size energy 0.25` 中的系数改为 `0.5`，满血缩放上限变为 1.5；新内容重新授权。

刻意检查一次错误：把绑定源 `score.value` 改成 `score.points`，`check` 应报 `OMS-SKIN-SCENE-018`（未知绑定）。撤销这次修改再继续；不要用生成工具覆盖它来掩盖错误。

三种机制分别负责不同属性：绑定读连续事实，状态机呈现当前状态，轨迹按游戏时钟播放；脚本才计算组合数值。声明式场景本身不需要脚本授权。完整字段与上限见[参考](../../REFERENCE.md)，进阶模板/换图/击打历史例子见[Reference Study](../reference-study/README.md)。
