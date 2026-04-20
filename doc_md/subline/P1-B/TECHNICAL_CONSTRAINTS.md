# P1-B 技术约束：输入语义与硬件验收

1. 不得为了补验收而引入与当前主链无关的新输入后端。
2. Windows 默认 HID backend 维持 DirectInput；`HidSharp` 仅作为诊断或非 Windows 路径。
3. keyboard / Raw Input / XInput / MouseAxis / HID 的 shared-action 语义不得因局部修复回退。
4. 任何改变输入行为、硬件口径或验收结论的改动，都必须同步更新本目录四件套与受影响的 `../../mainline/` 文档。