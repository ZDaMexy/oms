# Reference Study：换图、模板和击打历史

这里的三个文件都是**完整文件**，但这个目录不是独立皮肤：图片、`skin.ini` 与普通游玩部件来自静线模板。必须先复制模板，再替换三个同名文件，不能把本目录直接当完整包导入。

在制作套件目录运行，使用一个尚不存在的输出目录：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action new -Output .\work\reference-study -Name '我的场景练习'
Copy-Item -LiteralPath .\docs\examples\reference-study\gameplay-skin.json -Destination .\work\reference-study\gameplay-skin.json -Force
Copy-Item -LiteralPath .\docs\examples\reference-study\gameplay-skin.scene.json -Destination .\work\reference-study\gameplay-skin.scene.json -Force
Copy-Item -LiteralPath .\docs\examples\reference-study\gameplay-skin.script -Destination .\work\reference-study\gameplay-skin.script -Force
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action check -Source .\work\reference-study
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action import -Source .\work\reference-study -Output .\dist\reference-study.osk
```

替换后不要 `generate`，它会重写这三个练习文件。普通 BMS / mania 音符与长条继续使用模板的素材。

| 观察点 | 文件里的名字 | 机制与预期 |
| --- | --- | --- |
| 分数、准确率、连击、BPM、谱面进度 | `ref.bind.*` | 无需授权的实时绑定；进度条初始宽度为 0 |
| 旋转图形 | `ref.rotate` / `ref.icon` | 2000 ms 一周，暂停不推进 |
| 判定换图 | `ref.grade-image` | perfect/great 时选 warm，其它当前判定选 cool；不是读取图片文件名表达式 |
| READY → RUNNING | `ref.status-template` / `ref.status-instance` | 定义一棵模板树再生成实例，状态机仍按源节点 `ref.status` 控制；暂停冻结上一帧，恢复重建当前状态 |
| 图形随击打变大、逐渐回落 | `gameplay-skin.script` | 判定事件增加固定 heap 中的累计值（最多 8），每个 60 Hz tick 乘 0.9 衰减；再加当前血量权重 |

允许 snapshot、events、scene write 后，脚本缩放公式为 `1 + recent × 0.05 + gauge × 0.15`，范围为 1～1.55。拒绝或撤销时只撤掉额外缩放，信息、旋转、换图和状态仍在。这里的事件40是所有真实判定事件，不筛“成功命中”；当前脚本接口不提供判定等级，不能从偏差0推断Miss。

练习修改：先改旋转周期，再把 warm/cool 变体互换，最后改历史衰减系数。每一步都在 BMS 和 mania 各进入一次，暂停、恢复、重试；回退会清历史，不应继续带着上次累计值。例子没有可选目标或玩法过滤器；全局目标负责跨玩法共用，专用槽位应另用实际素材匹配模板。

此前逐文件工具检查记录见[记录](../../reference-verification.json)；这份说明不将工具检查等同于所有窗口和实谱视觉验收。完整写法见[字段参考](../../REFERENCE.md)。
