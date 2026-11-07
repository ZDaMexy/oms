# IIDX Omni 多作品与双平台维护计划

本页承接[P1-A计划](DEVELOPMENT_PLAN.md#多作品与双平台准备)，只维护跨作品调度、远期分组和平台原型的下一步。实际推进状态取[STATUS](DEVELOPMENT_STATUS.md#iidx-作品与平台维护状态)，独立来源/宿主/证据边界取[维护约束](TECHNICAL_CONSTRAINTS.md#多作品与双平台维护)，现有调用与回归范围取[工具说明](../../../tools/IidxSkinAssets/README.md#多作品维护入口)。2026-11-07用户已明确启动两族beatoraja制作；不据此恢复Lightning的OMS自动推进，也不启动无来源系列。

## 远期系列与待核边界

按街机正传估算四个主要系列，可能按实物差异拆成五至六个独立作品；名称和代数边界是规划假设，不是已取得来源、已实现覆盖或最终立项。CS/INFINITAS未纳入此估算，不为占位系列创建空工程。

| 系列暂称 | 待核范围 | 决定边界时需要的证据 |
| --- | --- | --- |
| Twinkle / Classic | 初代、substream～8th style | 各代原始资源、布局与真实调用，不能用ARC SCORE复刻代替 |
| Legacy PC / SD | 9th style～19 Lincle | 原画面及软件机能；9～13与14～19是否拆分由实际差异决定 |
| Standard | 20 tricoro起的传统框体路线 | 逐代来源与传统框体行为；现有Until26保持身份，后续扩代或另作需明确范围 |
| Lightning | 27 HEROIC VERSE起的Lightning路线 | Lightning自己的原机证据；现有Until33停止边界与范围保持 |

分期依据为[Bemanitools开发者的引擎研究](https://github.com/djhackersdev/bemanitools/blob/master/doc/dev/journal/2025-02-09-iidx-engine.md)，属于研究性分类；[27官方介绍](https://p.eagate.573.jp/game/2dx/27/lightning_model/index.html)、[31两类框体操作说明](https://p.eagate.573.jp/game/2dx/31/howto/play/tenkey.html)证明Lightning出现后传统框体仍并存。故27以后可能存在同代双框体表现，Until26不是传统框体历史终点。[30官方界面与Full HD更新](https://p.eagate.573.jp/game/2dx/30/howto/new/01.html)可作为系列内部差异调查，分辨率或主板变化不自动要求拆作品；BPL与ARC SCORE沿来源作为主题登记，不另计硬件时代。

## 调度与增量维护

先处理已交付作品的实际回归与阻断，再推进当前获准的来源补完或平台切片。各线可分别开发、验证、维护或暂停，不要求同速或同期发布；每次明确本次负责的作品、宿主和文件，build/test/打包按仓库协作规则串行执行。

共用工具修改只触发受影响线的必要回归，不能把暂停线的未完成功能纳入队列。新来源先完成版本/组件/保留美术/复刻/可选性分类，再决定归属；无真实来源的扩代继续等待，不占用当前原型完成条件。来源、帧主题与组件缺口继续维护在对应作品记录，避免另建重复进度账。

## beatoraja首个原型与扩展门

当前门是Standard与Lightning各自的完整7KEYS/14KEYS组件包；已有开发切片、P2/DP与控制实绘不能代签全部菜单或最终包。官方0.8.9、原安装保全、冻结来源和实际验证取[本轮记录](../../other/BEATORAJA_IIDX_SKINS_20261107.md)；不把作者配方推广为其它代的原软件事实。

1. 完成剩余FC、全部主题开场和动画挡板，逐项确认来源、完整帧、混合、循环与空终止帧；固定clock取样与完整影片分别登记，不用静止图补签可用动画。
2. 固定完整工具与来源清单后生成原生菜单，保留每族组件和支持的控制；不可表达的状态明确列缺口，Target外观不冒充原选目标算法。各主题/模式、全部片段及引用闭包经官方解析，再做最终同包实绘。
3. 同包验证覆盖两族主题、P1/P2/DP、长条状态、Timing、失败、自然结束、控制端点和较大动画负载；实际原生Replay/合成谱与物理输入、真实歌曲、长期手感分别记证，不能以静态截图或模拟分数证明可玩。其它键数按实际作者声明与宿主映射另核，不套用OMS的mania矩阵或LR2兼容规则；选曲/Results仍不列为本切片产出。
4. 验证后向真实播放器添加独立版本目录，复核原配置、成绩、谱库和旧skin，再形成版本化ZIP、许可、说明和校验。交付记录作品、宿主、包/工具/来源版本、已验组合与未验项；完成beatoraja不改变OMS状态，Lightning的OMS旧缺口仍不进入队列。
