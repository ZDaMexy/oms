# beatoraja 原生皮肤适配诊断

当前作品、来源、安装和验收只读[适配记录](../../doc_md/other/BEATORAJA_IIDX_SKINS_20261107.md)及[P1-A状态](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md#iidx-作品与平台维护状态)。这里保留移植时容易产生假成功的原因，不保存推进账本。

Lua皮肤header与body分别执行；require缓存会保留返回表，被合并过的片段必须由factory每次返回新表。字符串对象ID需要整体命名空间，数值timer/ref/offset不能顺带改名。相同custom timer ID只能复用相同定义，否则后者会覆盖前者；参考[组件包](../../tools/IidxSkinAssets/beatoraja_package.py)。

官方Skin配置会递归扫描全部`.json`，未知字段被忽略不代表文件不会完整解析。大型审计清单应使用`package-receipt.json.txt`，不能把仅直接加载`.luaskin`成功当成菜单扫描通过；独立发现门见[官方扫描探针](../../tools/IidxSkinAssets/tests/SkinDiscoveryProbe.java)。历史旧名只读保留，正式安装不能继续投放旧JSON侧车。

5KEYS必须使用独立type1和六路NoteSet，不能套用LR2的7转5菜单兼容。作者P2五键位于原七键靠皿侧，按实际5K layout读取，不能取前六列；逻辑scratch的timer身份也不能硬写第八路。缓存同时保留keymode与presentation，避免相同框体/素材误用八路片段；诊断取[六路来源测试](../../tools/IidxSkinAssets/tests/test_beatoraja_five_keys.py)。

原生clip宽或高为0代表无clip，不能靠零面积隐藏整图。TimerProperty微秒origin转换毫秒时对负数向零截断，合成几何时钟必须与实际SkinObject规则核对；极低正值可被截成elapsed=0。P2的两次180°旋转必须沿祖先保留signed rect，看到叶子最终正向不能删掉中间镜像。回归取[Gauge原生探针](../../tools/IidxSkinAssets/tests/GaugeClipProbe.java)。

原坐标变换可能把整数glyph advance算成`28.99999999999997`；直接与`int()`比较会拒绝合法BMFont。仅在与最近整数的绝对差不超过`1e-9`时整化，负值和真实分数仍拒绝；不要改源字形或作者数据来迁就导出器。正负余差、原图字节及真实分数反例见[字体间距测试](../../tools/IidxSkinAssets/tests/test_beatoraja_hud_spacing.py)。

原生offset已按目标逻辑分辨率缩放；Gdx窗口高度不一定是Config逻辑高度，不能据窗口比例反推offset。offset同时移动clip，固定窗口内的读数需要保持clip独立。Standard旧框体可复用26软件的484读数跨度，但21的实际轨道高度473/top11不能被该常数覆盖；诊断取[读数适配器](../../tools/IidxSkinAssets/beatoraja_reading.py)。

Gauge增长与分数越线只能从实际公开状态观察；首个高分快照不提供越线时间，零PB也不代表存在一次0分记录。custom timer是render采样缓存，不能代替不可见的游戏事件队列。Judge外观OFF仍须保留原生状态owner，否则FAST/SLOW的region资格也会消失。接口依据与边界回链适配记录。

原图出现SUB/NO DATA不证明存在第二条比较柱，先核祖先门和同列锚点；来源实际可能是PB不可用时的替代。开场素材移动也不代表所有独立Gauge/转盘须延迟出现，先核作者可见门和源宿主准备期是否仍渲染稳态树，不能凭画面观感补全局隐藏规则。

DP同一开场模板可按`layout.group`选择左右动画；选了stage矩形并不等于已经提供舞台上下文。每个material binding须使用自己的`target.group`，随后恢复原上下文，不能全局固定deck-1或同时放行两支。BPL tradz的真实三模式来源回归见[轨道开场测试](../../tools/IidxSkinAssets/tests/test_beatoraja_lane_opening.py)。

NoteSet外层destination颜色不会传给音符；Judge外层颜色也不会替内部label/Combo染色。亮度须核到实际carrier，不能只看recipe有值。原小节线归NoteSet.group；它的纹理、几何与到达后退场行为分别核对。条件动画要把已解释的真实KEYON门传到最终atlas destination，不能只让sprite分支通过；b04曾因此整支漏接。

原生Replay可产生真实判定与Result，但不注册正常成绩；曲终后START/SELECT会触发结束而非调整挡板。取景工具应在曲中模拟公开输入并验证读数门、原自然结果与退出。framebuffer遥测在render后读取，判定线程仍可能变化；图片年龄和状态不是原子快照。工具入口取[原生取景](../../tools/BeatorajaSkinCapture/README.md)。

同一render的多个取景标签须合并readback与PNG编码。官方Replay的JudgeThread读render更新的timer，一次时间跳跃会把短按press/release一并处理；后台编码队列满后重复PNG在GL线程执行，仍可能造成真实MISS。修工具后保持原Replay和零失误门复验，不能把取景干扰签作皮肤失败或放宽结算门。

动画同名ID只有连同作品和manifest才是资源身份。完整原帧、原loopStart和终止帧与压缩方式分开核对；PNG小不代表解码占用小。静态裁片、ImageSet和规则grid的对象数/解码占用须分别计算，不能沿前一种载体的排序选后一种。最终选择需要目标播放器实绘及耗时证据，不能由烘焙器的像素等价直接签收。

原生Slider/HiddenCover/LiftCover只接单texture规则grid，不能把ImageSet/value直接塞入。保留这些原生载体才能保持挡板移动/裁切；普通Image加offset会连clip一起移动。规则grid按行排格、`cycle=全部格数×100`（包含padding）；只读TimerProperty返回`(floor(nowMicro/1000)-(cell*100+50))*1000`，整千微秒避免负origin向零截断。页中缺少当前帧时还必须`draw=false`，仅timer OFF仍会选第0格。用文件映射生成PNG后须显式`Image.close()`释放PIL引用，否则mmap仍被导出指针占用。诊断取[原生选格探针](../../tools/IidxSkinAssets/tests/CoverGridTimerProbe.java)及[重排测试](../../tools/IidxSkinAssets/tests/test_beatoraja_cover_atlas.py)，它们不代签GPU裁切或显存。

大动画索引的JSON字节数不是进程占用：解析树还受缓存持有链影响。`read_bakes`须在逐项选定身份后释放大树，保留小元数据和文件摘要，使用时只缓存当前项；切换前先释放，重读校验冻结字节。只给读取器限缓存还不够，Cover的对象缓存若强引用index仍会让全部树常驻。保留像素/帧结果缓存即可复用重排；membership、选择优先级、覆盖顺序、源校验与换项重载取[索引测试](../../tools/IidxSkinAssets/tests/test_beatoraja_bakes.py)。只读选择的峰值不代签完整导出或原生宿主。

安装器只需名称/族别时，先对整个大侧车`ConvertFrom-Json`会让PowerShell对象树与后续Python完整验包同时驻留。[独立安装器](../../tools/IidxSkinAssets/Install-BeatorajaOmni.ps1)让短命Python输出这两个字段，检查退出码后只解析小结果；原三次完整核验不省略。Python自身仍有一次完整JSON解析峰值，不能把子进程退出称为流式解析或内存上限。
