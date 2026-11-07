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

## beatoraja首版后的维护与扩展门

当前用户新提供的22 HDD已进入Standard双宿主增量；按[独立来源记录](../../other/STANDARD_MODEL_SKIN_20261105.md#2026-11-08-22-来源接入进行中)完成本轮来源资格、同包验收和新版本保全，不重建已交付Lightning。

Standard与Lightning完整组件首版已分别安装、保全与交付，覆盖原生5KEYS/7KEYS两侧、14KEYS DP。官方0.8.9、两族冻结来源、独立包身份和同包验证取[本轮记录](../../other/BEATORAJA_IIDX_SKINS_20261107.md)；不把作者配方推广为其它代原软件事实。首版构建/安装不再列入待办，后续沿以下实际输入维护，不自动扩代或恢复OMS停止线。

1. 按真实游玩反馈定位配置、谱面与设备；复现后修复受影响族并补同包回归。物理输入、真实歌曲/视频BGA、长期手感及真实玩家保存/IR仍未由合成Replay代签，不操作正式成绩来补工程证明。
2. Lightning大动画资源继续按实际载体计算。已有同选DP实绘及纹理清单不覆盖不同大动画混选或低显存设备；若需扩大该范围，先固定具体配置并观察运行和资源，不删帧、缩图或沿旧载体排序冒称无损优化。
3. 宿主或来源出现新证据时，再处理完整Start面板、首排提示、Separate Timing、Target算法等已明示差异；无真实可用状态时保持缺口，不伪造数据。没有10KEYS作者声明，不套用OMS的mania矩阵或LR2兼容规则；选曲/Results不列为本首版产出。
4. 新版本继续冻结各族工具/来源、官方解析、受影响同包实绘和必要旧族回归，再添加独立版本目录、复核玩家保全并交付ZIP。沿已有目录分宿主存储，保留原版及配套许可/证据；新HDD先分类资格，跨作品不借美术补缺。
