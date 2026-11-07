# beatoraja 原生皮肤适配诊断

当前作品、来源、安装和验收只读[适配记录](../../doc_md/other/BEATORAJA_IIDX_SKINS_20261107.md)及[P1-A状态](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md#iidx-作品与平台维护状态)。这里保留移植时容易产生假成功的原因，不保存推进账本。

Lua皮肤header与body分别执行；require缓存会保留返回表，被合并过的片段必须由factory每次返回新表。字符串对象ID需要整体命名空间，数值timer/ref/offset不能顺带改名。相同custom timer ID只能复用相同定义，否则后者会覆盖前者；参考[组件包](../../tools/IidxSkinAssets/beatoraja_package.py)。

原生clip宽或高为0代表无clip，不能靠零面积隐藏整图。TimerProperty微秒origin转换毫秒时对负数向零截断，合成几何时钟必须与实际SkinObject规则核对；极低正值可被截成elapsed=0。P2的两次180°旋转必须沿祖先保留signed rect，看到叶子最终正向不能删掉中间镜像。回归取[Gauge原生探针](../../tools/IidxSkinAssets/tests/GaugeClipProbe.java)。

原生offset已按目标逻辑分辨率缩放；Gdx窗口高度不一定是Config逻辑高度，不能据窗口比例反推offset。offset同时移动clip，固定窗口内的读数需要保持clip独立。Standard旧框体可复用26软件的484读数跨度，但21的实际轨道高度473/top11不能被该常数覆盖；诊断取[读数适配器](../../tools/IidxSkinAssets/beatoraja_reading.py)。

Gauge增长与分数越线只能从实际公开状态观察；首个高分快照不提供越线时间，零PB也不代表存在一次0分记录。custom timer是render采样缓存，不能代替不可见的游戏事件队列。Judge外观OFF仍须保留原生状态owner，否则FAST/SLOW的region资格也会消失。接口依据与边界回链适配记录。

原生Replay可产生真实判定与Result，但不注册正常成绩；曲终后START/SELECT会触发结束而非调整挡板。取景工具应在曲中模拟公开输入并验证读数门、原自然结果与退出。framebuffer遥测在render后读取，判定线程仍可能变化；图片年龄和状态不是原子快照。工具入口取[原生取景](../../tools/BeatorajaSkinCapture/README.md)。

动画同名ID只有连同作品和manifest才是资源身份。完整原帧、原loopStart和终止帧与压缩方式分开核对；PNG小不代表解码占用小，图片组数也不等于挡板静态裁片对象数。最终选择需要目标播放器实绘及耗时证据，不能由烘焙器的像素等价直接签收。
