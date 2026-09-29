# Skin V1 数值脚本指南入口

完整语言说明已迁至制作套件内的[可选数值脚本指南](../../skin-authoring/docs/SCRIPTING.md)。它包含可直接打包的完整例子、权限、语法、事件编号、数值输入、历史组合、预算、排错及可选预编译命令；不要求作者阅读游戏源码。

- 入门：[First Scene](../../skin-authoring/docs/examples/first-scene/README.md)，用血量控制装饰缩放。
- 进阶：[Reference Study](../../skin-authoring/docs/examples/reference-study/README.md)，用有限击打历史组合演出。
- 当前实现和验证状态：[P1-A状态](../subline/P1-A/DEVELOPMENT_STATUS.md)。
- 维护合同：[P1-A技术约束](../subline/P1-A/TECHNICAL_CONSTRAINTS.md)；二进制格式由[生产编译器](../../osu.Game/Skinning/Gameplay/Scripting/GameplaySkinScriptCompiler.cs)定义。

普通作者通过制作套件的check编译源码，再在游戏里允许或拒绝可选效果。历史Momentum/星轨材料继续保留参考，不作为当前制作起点。
