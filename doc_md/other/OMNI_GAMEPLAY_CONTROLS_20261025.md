# Omni 游玩调速与挡板复核（2026-10-25～26）

本轮按用户提供的七张参照图补齐游玩Start操作、挡板白绿字、甩盘调速与SUB TARGET。开始基线为干净`master@fec44d57`，fetch后与origin一致；当前交付、验证和未完成门取[P1-A状态](../subline/P1-A/DEVELOPMENT_STATUS.md)。

## 原版实际观察

原HDD只读；操作只在既有完整独立副本，通过本机Spice API投币、按键、转盘与截图。沿已保存的两项启动调整，不增加菜单或歌曲计时冻结，不使用真实卡、账号或正式OMS玩家库。截图/操作收据保存在ignored的`artifacts/omni-gameplay-controls-20261025/`；截图新时间戳不作为游戏tick、精确毫秒或硬件时序证明。

| 场景 | 本轮实际所见 |
| --- | --- |
| LIFT | Start按住时在抬起的判定线下方显示白字125、绿字701；双击Start保留LIFT125并出现上挡板125，绿字变601 |
| LIFT与SUD，普通模式 | 按住Start、正向转盘令SUD125→148，LIFT125不变，HS0.97不变，绿字601→583 |
| 模式切换 | 同一次按住Start时按EFFECT切入Floating，HS0.97和绿字583保持；正向转盘令SUD148→177、HS0.97→0.93，绿字583保持 |
| LIFT与SUD，Floating | 双击关闭SUD后LIFT125、HS0.93保持，绿字730；再正向转盘令LIFT125→100，HS0.93保持，绿字751；重开SUD自动令其177→202，恢复绿字583 |
| HIDDEN，Floating | 正向转盘令Hidden125→96、HS1.25→1.29，绿字583保持；双击关闭后绿字644，重开恢复Hidden96/绿字583，没有添加SUD |
| SUD与HID，普通模式 | 两边白字125/绿字601/HS1.08；正向转盘后两边白字同步147/绿字566，HS1.08保持；双击同时关闭后绿字800，再开保留147/绿字566 |
| 双击第二下继续按住 | 第二次按下后保持700ms，已经切换挡板并继续显示读数；不等待第二次松开才切换 |

以上为对应游客、谱面和当时设置的运行样例，不是OMS公式常量或跨账号默认值。正方向均指本轮API的`P1 TT+`；硬件方向仍由实际输入校准决定。

后半夜原参照遇到本地EA默认维护，组合挡板取景另起自有进程，仅在独立runtime增加`eamaint=0`以继续游客流程；不改系统时钟。该设置依据Spice公开[参数定义](https://raw.githubusercontent.com/spice2x/spice2x.github.io/main/src/spice2x/launcher/options.cpp)与[实际处理](https://raw.githubusercontent.com/spice2x/spice2x.github.io/main/src/spice2x/launcher/launcher.cpp)，本地最终日志已确认生效。前后两个配置、进程收据和截图分别保留，两个启动调整不变，不把服务维护设置解释为游玩速度或时序校准。

SUB TARGET的[官方说明](https://p.eagate.573.jp/game/2dx/33/howto/play/score_graph.html)定义为主目标无数据时的备用目标，重复十键2取消、在其它目标上设置则替换；备用也无数据时为NO DATA。本轮游客已选AAA/AA并发送十键2，但截图没有可辨SUB标记，操作意图文件名不计原版设置成功；OMS的保存、补位和真实显示另行验证，不把它做成第三根并列柱。

[官方调速说明](https://p.eagate.573.jp/game/2dx/33/howto/play/option_speed.html)与[挡板/LIFT说明](https://p.eagate.573.jp/game/2dx/33/howto/play/option.html)用于交叉核对Start、白键降/黑键升、Floating挡板保持可见时间和双击；实际读数、方向与组合行为以本轮原版操作和各自截图为依据。

## OMS 接入边界

原生BMS持有按键、速度、挡板与LIFT；皮肤通过同一Timing快照只读显示。`scroll.available/adjusting/mode/green-number`、上下挡板保留值/active及LIFT原有实际位移见[作者参考](../../skin-authoring/docs/REFERENCE.md#动画状态机绑定变体模板)。白字分别取所属的上下挡板长度或LIFT单位，不用一个Sudden数值冒充其它行；mania不伪造BMS调速状态。

Start仍承担开场阻塞；短按双击、模式组合和轴运动不能攻击音符。第二次按下可继续长按，长按/用于调整的手势不参与后续双击；失焦、暂停来源和时钟epoch变化清理手势与轴余量。轴用既有解释器输出的带符号counts或圈数，保留亚单位反向抵消；不按帧数或回调次数调速。正常键、长皿持续状态、回放和成绩权威保持原链。

Floating按当前真实BPM/可见几何调速；挡板转动保持目标可见时间，SUD关闭后改LIFT、重开恢复目标。保留OMS现有Normal/Floating/Classic范围与基础映射，原机20档到实际倍率的整表、所有变速谱及设备手感未因此宣称完全等价。普通按键/转盘调节分别沿自己的实际步长，不用界面模式名称替代算法验证。

原版美术与作者目录仍仅保存在本地，作品独立私下分享；不并入canonical、客户端发行或公开制作套件。旧作者与OSK不覆盖，最终版本及校验从成品说明读取。

## 本轮验证

证据根为ignored的`artifacts/omni-gameplay-controls-20261025/`，源码基线为`fec44d57`加本轮增量。当前明确通过：显式Core.Tests构建后的皮肤/HUD范围1371通过、3项opt-in跳过；BMS完整3050通过、19项opt-in跳过。Desktop Release、Core.Tests、mania.Tests与作者CLI的Release构建成功，保留既有ImageSharp索引告警及未修改文件的nullable/ConfigureAwait警告。

BMS首轮仅零分配断言出现3288bytes，原程序集单项复跑为0；第二轮出现另一1552bytes断言及一个异步旧结果夹具失败、后续LiveGameplayActive拒绝。相关118项原断言不变，独立复跑通过；独占运行的第三轮完整3050项无失败。未放宽分配预算、删除断言或据此修改判定/设备热路径。保留全部原TRX与日志，不将偶发小量分配武断归因为JIT。

最初取景把较旧Core.Tests输出覆盖到当前BMS依赖，导致新variant在选择时拒绝018；改为显式构建拥有工程、逐个关键DLL按来源封存摘要。早期`core-skin-hud-r1/r2`及两次失败Player不能代签当前代码。候选r1的非法像素属性和r2两处同节点Alpha冲突均未发布；修到合法horizontal叶子与独立父子门，r3静态统一Alpha writer检查无重复，不放宽scene/manifest预算。

mania完整995通过、2项opt-in跳过。随后只补runtime拒绝的数值分项与opt-in取景的独立准备等待，Core皮肤/HUD1371通过、3跳过再验；没有扩大生产时限或预算。

r3通过目录分支检查仍在真C3超textGlyphs：7K9073、14K14321，限8192。真实实例为830/1427节点和12002/23894视觉，其他预留均未超限。原因是新Stage读数Provide向Group及Lane继承，单/双舞台各展开10/20份；同节点的可见门不裁剪准备储备。r4只在相应Group截断该继承，保持真正Stage一份读数，另移除普通/Classic面板中与底框重复的倍率行、修SUB百分号前缀；不增加引擎预算，不添加没有必要的目录模块。

r5的实图另修小数点：原8×8点逐像素放入8×20透明行画布，保持字距、字号和行高；两个FHS底板只清除各自原固定小数点的9×9区域，每张实改68像素，区域外RGBA不变。上下限使用原10.0/0.5标签，CURRENT中心取原四位数字联合边界，修正DP行位。45份Frame scene/INI、所有binding/variant/track/instance和字体度量相对r4保持；原图及旧作者不写改。

最终同一r5包12组原生Player全部退出0：BMS7无挡板、14双挡板、7K右侧Hidden、5K/28框体LIFT、7K/27框体LIFT与SUD、右侧Classic、透明挡板且关闭LIFT读数、右侧SUB AAA、14K主目标优先、无数据，以及mania7实际SUB下拉保存/重建/恢复和mania16双舞台。BMS每组8个真实输入/原生状态/显示样本，共80个；自动核对每行白绿字与各自active、实际原生矩形边缘63处，并查看最终画面。SP真实C3预留4322字形、DP4962，限8192；无运行时故障，未放宽scene/manifest或atlas限制。

控制取景使用正常`BmsSoloPlayer`，移除Autoplay后通过已有动作入口注入，显式模拟`host.IsActive`并记录原观测为false；不修改生产输入所有权或门。真实帧缓冲由原生宿主截图，截图本身不是前台性能或物理设备验收。控制步骤后在等待谓词中按真实游玩时钟命中一个音符，保留正EX、连击和自然结算断言；不是完整Autoplay/FC或真实歌曲通关。早期将ReplayPlayer当真人入口、后台失焦和跨步骤命中晚50ms的失败均留存，修正取景方法后重跑；没有用放宽输入门或删除成绩断言掩盖失败。

源包SHA256为`b2e9a58f59f4748f67bfd56c73ddb847dda436f1ddd7d6f04b1021fe5f0b208a`，323,316,715字节、7043份闭包、14,412,200字节配置；11模块、31选项、10字体，原1271款与539张预览保持。保守选中scene上界518525/524288字节、manifest64448/65536字节。Python增量26项和原百分比13项检查均通过；最终CLI退出0，11类所有款式与条件分支、13436组不同代表组合通过，其余取默认值，不展开全部排列。该CLI不代替上方真实C3/Player检查。最终ZIP另逐条解压验证CRC并与作者内容摘要核对，7043份全部一致。

原安装14221份元数据及7项关键摘要保持，独立副本卡文件为空，自有参照进程均已退出。旧10月24日完整目录和Player证据不计为新包验证。当前包须先重编/重启OMS再导入；原机档位整表、所有变速谱、正常窗口帧时间与实体控制器手感仍待验收。

本地交付为`.v26102600`，附操作说明、SHA256、验证记录与8张原生截图；不是客户端发行包。文档导航、阶段投影及事实登记检查通过，另外核对本轮新增的26处本地文档链接，无失效目标。完成日期按Asia/Shanghai记录，不提升旧证据适用性。

取景源与运行依赖元数据已留档；删除本轮临时`oms/.dev-cache/temp/omni-limit-render-host`的整个命令在执行前被自动审批以`blocked by policy`拒绝，未给出更具体原因。未通过其它命令重试，目录继续保留；回执为同证据根`native-host-cleanup-rejected.json`。原作者/旧版、截图、测试证据、工程输出与共享依赖保留。
