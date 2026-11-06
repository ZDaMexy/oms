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

当前以已保留的Standard 26 Rootage与Lightning 33各自建立7K、1P游玩切片；不把配方推广为其它代的原软件事实。官方0.8.9、原安装保全、冻结来源与研究结果见[本轮记录](../../other/BEATORAJA_IIDX_SKINS_20261107.md)。继续闭合当前切片、P2/DP与原生配置，然后扩大各自主题和组件；不能只做研究、更新播放器或文档后停止本轮授权工作。

1. 逐项核对原图/UV、几何与动画，以及宿主实际提供的notes/LN、BGA、Gauge、判定、Combo、Graph与事件/计时能力；先标记可直接表达、需适配、缺宿主能力或缺原版证据。OMS作者JSON/Lua与OSK不作为可直接运行的beatoraja格式，不能把当前OMS生成器命名成通用双平台导出器。
2. 完成原生皮肤加载、实际输入与完整歌曲、必要读谱信息、长条状态、BGA/Gauge/判定、真实失败与自然结束，再核对已接入动画的触发和重开复位。透视、网格、遮罩及混合效果逐项验证，近似方案须记录适配差异；不以静态截图或模拟分数证明可玩。
3. 保留独立的组件差异表与同包证据，达到上述切片后扩P2/DP、主题和配置组合；Lightning的beatoraja已获准，OMS旧缺口仍不进入队列。beatoraja不套用OMS的mania矩阵，选曲与Results皮肤不列为本切片产出。
4. 有第二个真实消费者后再提取共用导出逻辑；优先复用来源与已核解析，不预建多引擎框架。正式交付按“作品/宿主/包版本/宿主版本/来源与工具版本/已验组合/适配差异/未验项”收口，完成一平台不改变另一平台状态。
