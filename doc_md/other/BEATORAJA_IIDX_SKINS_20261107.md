# beatoraja IIDX 双作品适配记录

本页记录2026-11-07开始的独立宿主工作，当前为开发中；不提升OMS旧包、来源原软件或发行验收。调度取[P1-A维护计划](../subline/P1-A/IIDX_SKIN_MAINTENANCE.md)，状态取[STATUS](../subline/P1-A/DEVELOPMENT_STATUS.md#iidx-作品与平台维护状态)。

## 授权、名称与来源

用户本轮明确要求调研并制作Standard与Lightning的beatoraja皮肤，更新其实际播放器，保全玩家数据，推进可验证产物。基础名称仍为`Standard Model Skin Omni Until 26`与`Lightning Model Skin Omni Until 33`。Lightning在OMS的停止自动推进不变；本轮授权是新的beatoraja消费者，不能据此恢复OMS旧缺口队列。

两族各自只读消费冻结作者：Standard使用2026-11-07的`author-r8`；Lightning使用2026-10-29交付对应的`omni-full-v52-current-hispeed-padding-r1`，不是较早的`delivery`目录。同名素材不跨族替换，尺寸、框体和软件机能不强求相等。Standard来源范围沿[来源现状](STANDARD_MODEL_SKIN_20261105.md#本地来源现状)，Lightning沿[作品记录](OMNI_GAMEPLAY_COMPLETION_20261026.md)。

原始素材留在`E:/OMS-Development/sources/`各作品原位，新的原生试产在`OMS-Beatoraja-20261107/<family>/`区分；后续临时构建、逐帧烘焙在本仓`.dev-cache/temp/`，真实运行证据在`artifacts/beatoraja-capture/`。只有候选闭合后才复制版本化交付，历史OMS包不迁移或覆盖。精确私有清单、素材、玩家信息和截图不进Git。优先CLI/API与程序内真实framebuffer；本轮用户仅允许必要时以computer use解决操作阻塞，目前未使用。

## 官方0.8.9更新与数据保全

官方版本取[0.8.9发布](https://github.com/exch-bms2/beatoraja/releases/tag/0.8.9)及[官方分发页](https://www.mocha-repository.info/download.php)。研究源码固定标签提交`83574048ba489082eb46df7fe9806b464bf1f526`；执行验证使用实际官方发行JAR。源码标签与发行包的默认skin路径有差异，不能假定两者完全同一打包布局。

已将用户的`D:/beatoraja0.8.8-jre-win64`原目录同卷改名为`D:/beatoraja`，目录文件ID一致，没有复制完整安装或曲库。使用官方差分包的10个程序/IR/native文件；原启动器、JavaFX 21运行时、曲库、player、现有skin、表与其它工具保留。旧minir-600移至`ir_backup/`并仅调整Endless Dream启动器使其继续引用旧插件；新beatoraja引用minir-700。未改用户当前选择的skin，未启动真实玩家环境执行数据库升级。

升级前22项关键文件已逐个备份并核SHA256。路径迁移只改3个JSON中的7个值及`songdata.db`的一条根目录路径；8个数据库13张表逐表核对类型/行内容与schema，其它数据保持一致，原59974条谱面相对路径与成绩不改。3个开始菜单快捷方式已修复。失败回滚在合成目录验证后才执行实际迁移；备份和完整回执位于`E:/OMS-Development/evidence/beatoraja-upgrade-20261107/`，其中`upgrade-complete.json`是更新完成入口。下载SHA为本地完整性证据，不能冒充发布者签名。

## 宿主研究与实现边界

原生格式取官方[JsonSkin](https://github.com/exch-bms2/beatoraja/blob/0.8.9/src/bms/player/beatoraja/skin/json/JsonSkin.java)、[Lua状态API](https://github.com/exch-bms2/beatoraja/blob/0.8.9/docs/lua-main-state-api.md)与同标签loader/render源码。

- `.luaskin`的header和body分别执行；读取原生option并只加载本次所选模式/主题的资源。作者catalog的variant使用第一个匹配分支，DP不携带SP专用layout选项；不得把同名animation ID当跨manifest资源身份。
- notes、LN/CN/HCN、判定、Combo、Gauge、BGA与失败/FC时机由beatoraja实际状态驱动。SP2仍是第一玩家；DP才使用第二组timer。新长条字段明确映射，不沿用易混淆的旧active/damage命名。
- 两族普通note和长条首尾高度不同；原生共享lane高度，导出用同族透明顶补、底对齐保留原像素。IIDX没有mine素材，BMS补显眼的红白危险条并登记为宿主新增，不谎称街机原图。
- Gauge原作是整条图与裁切，不能猜成50格。Graph采用beatoraja当前Target与实际EX/PB；无法区分无PB与真实零分时保留原生零值，不伪造镜像成绩。宿主Fast/Slow统计与OMS语义不同，分别记录。
- 字体按原glyph矩形和advance转换；原生字号、裁切及采样差异逐项登记。bitmap字体强制线性过滤、原生truncate与OMS ellipsis等不宣称像素等价。
- AFP记录动画包含UV网格、camera、mask及colourMatrix，不能当普通PNG帧直接读。专用GPU宿主调用现有生产解码/evaluator/drawable/shader，按原层序保留normal与additive段，并在黑/白/彩底验证复合。该宿主实际后端是Veldrid/D3D11；beatoraja的OpenGL显示仍需独立取景。
- 烘焙atlas保留源帧索引、fps、loopStart、heldLoop/release及空终止帧，裁边/精确去重/分块不降帧、不缩图。选中结果逐个核来源文件摘要；事件类不接受partial静止采样，框体固定clock使用源声明的准确帧。PNG压缩大小不代表显存。

实现入口为[素材工具](../../tools/IidxSkinAssets/README.md)、[GPU烘焙工具](../../tools/IidxSkinBake/README.md)和[原生取景工具](../../tools/BeatorajaSkinCapture/README.md)。没有移植OMS运行时，也不改beatoraja判定、计分或用户的客户端JAR。

## 当前验证与未闭合项

实际安装的官方JAR/natives/JRE已在独立QA工作目录通过SP1正常REPLAY、SP2自然失败、DP HCN及SP2 CN AUTOPLAY；配置、合成谱、Replay、player和DB全在QA目录。工具核对实际skin路径/选项，捕捉fallback，要求自然状态与正常dispose，阻止HTTP/HTTPS及真实IR。AUTOPLAY只证明自然返回选曲，正常Result由原生Replay获得；不注入成绩、Gauge或判定计时器。

两套默认SP1已经真实加载并跑到Result。试产已接原生三色判定、Gauge、Graph、分数/BPM/Hi-Speed、判定统计面板、Full Combo、beam/explosion与三类挡板，并保存首次效果的0/50/100/250毫秒近帧。整合审查修正SP2的逻辑键/视觉索引混用，以及Lift下爆炸、Standard判定位置；Lift独立使用自身art和边距。这些修复仍须在后续同包矩阵验证，不能用更早截图签收。

Lightning默认FC24做过实际官方JAR的两种无损分块比较。固定联合框方案为467个imageset组、约1041.6MiB解码；16像素对齐方案为6448组、733.42MiB。前者Play/FC后的render调用median为0.544/0.685ms、p95为0.776/0.941ms；后者对应3.684/5.176ms、4.389/5.988ms，故选择前者。实测为本机HD、合成HCN Replay、限120FPS；含宿主等待，不是纯CPU、实际FPS或手感。两次取景在`artifacts/beatoraja-capture/lightning-r7/`与`lightning-r8-union/`，完整FC原始未分块约1478.22MiB；未据此宣称全部组件或低显存设备通过。

当前仍在整合Ready/失败/开场、各模式动画和完整可选组件，补同包SP2/DP与读谱效果验证后再定交付。透明留边、原生format与工具测试已做focused核对；输出中的`export-receipt.json`逐variant保留未映射项，不能把格式加载成功当作视觉完整。真实物理输入、长期手感、视频BGA、真实玩家保存/IR、所有配置排列与选曲/Results皮肤未签收。尚无beatoraja正式成品版本或部署验收。
