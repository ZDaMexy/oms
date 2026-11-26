# beatoraja 原生皮肤适配诊断

当前作品、来源、安装和验收只读[适配记录](../../doc_md/other/BEATORAJA_IIDX_SKINS_20261107.md)及[P1-A状态](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md#iidx-作品与平台维护状态)。这里保留移植时容易产生假成功的原因，不保存推进账本。

启动黑屏且日志最后是 BGM Set 统计时，先在活体抓主进程与 MinIR 子进程线程栈。若 `LWJGL Application` 在 `BarManager.init → getTableDatas → destroyProcess → BufferedReader.close` 等锁，对照 IR 读线程是否在 `readLine` 持有同一锁。确认子进程仍在读取难度表再单独结束它，并观察原进程是否进入选曲绘制。真实超时与恢复见[现场记录](../../doc_md/other/BEATORAJA_IIDX_SKINS_20261107.md#2026-11-26-minir-启动黑屏)。

Lua皮肤header与body分别执行；require缓存会保留返回表，被合并过的片段必须由factory每次返回新表。字符串对象ID需要整体命名空间，数值timer/ref/offset不能顺带改名。相同custom timer ID只能复用相同定义，否则后者会覆盖前者；参考[组件包](../../tools/IidxSkinAssets/beatoraja_package.py)。

内容寻址的工厂改名不直接证明画面变化。仅对实际Lua及官方converter解出的静态CN片段，在image ID非空唯一、完整对象相同、Note/source有序引用闭合且全局namespace无碰撞时，可按唯一ID映射比较image声明；不能重排destination、Note列、source或其它数组。原始树另核字段存在性及native keys枚举原序，避免导出器替数组排序后伪造等价。保留严格字节门失败，报告明确byteExact=false及窄范围，不能扩大成所有Lua的顺序例外。实际受限证明见[20来源增量](../../doc_md/other/BEATORAJA_IIDX_SKINS_20261107.md#20-来源-standard-增量)。

观察本组件包最终Lua时，裸作者ID的整串正则会漏掉带合法工厂namespace的实际对象。分别留存actual id、sourceNodeId与namespace，验证目标角色唯一、同一单层namespace且属于已核shard，不把实际ID改成预想值。Java JSON探针将整数Number写成double时，crop的23.0须在本地像素索引前经有限、排除bool和精确整数校验后转int；保留原selection字节，不能用floor接纳真正分数或改写几何。实际Graph取证仍须独立运行与原图核对，静态角色齐全不代签视觉。

Graph rank线的颜色取证须先排除实际柱图可能遮挡的几何区域。20的首轮实际矩阵中，目标柱覆盖AA/A的部分固定取样点，使正确源色被误判；应从最终Lua和官方装配取得current/best/target三柱的完整可能矩形，仅在rank线剩余可见区域均匀取样，保留最小样本数、原颜色与误差门。不能按画面颜色搜点、挑通过帧或放宽阈值。官方探针对未设置angle/clip输出的MIN sentinel须结合原始字段存在性判定，不能把默认占位当真实旋转/裁切。保留旧失败并用新冻结准备重跑同包矩阵，证据回链[20宿主记录](../../doc_md/other/BEATORAJA_IIDX_SKINS_20261107.md#20-来源-standard-增量)。

官方Skin配置会递归扫描全部`.json`，未知字段被忽略不代表文件不会完整解析。大型审计清单应使用`package-receipt.json.txt`，不能把仅直接加载`.luaskin`成功当成菜单扫描通过；独立发现门见[官方扫描探针](../../tools/IidxSkinAssets/tests/SkinDiscoveryProbe.java)。历史旧名只读保留，正式安装不能继续投放旧JSON侧车。

5KEYS必须使用独立type1和六路NoteSet，不能套用LR2的7转5菜单兼容。作者P2五键位于原七键靠皿侧，按实际5K layout读取，不能取前六列；逻辑scratch的timer身份也不能硬写第八路。缓存同时保留keymode与presentation，避免相同框体/素材误用八路片段；诊断取[六路来源测试](../../tools/IidxSkinAssets/tests/test_beatoraja_five_keys.py)。

原生clip宽或高为0代表无clip，不能靠零面积隐藏整图。TimerProperty微秒origin转换毫秒时对负数向零截断，合成几何时钟必须与实际SkinObject规则核对；极低正值可被截成elapsed=0。P2的两次180°旋转必须沿祖先保留signed rect，看到叶子最终正向不能删掉中间镜像。回归取[Gauge原生探针](../../tools/IidxSkinAssets/tests/GaugeClipProbe.java)。

原坐标变换可能把整数glyph advance算成`28.99999999999997`；直接与`int()`比较会拒绝合法BMFont。仅在与最近整数的绝对差不超过`1e-9`时整化，负值和真实分数仍拒绝；不要改源字形或作者数据来迁就导出器。正负余差、原图字节及真实分数反例见[字体间距测试](../../tools/IidxSkinAssets/tests/test_beatoraja_hud_spacing.py)。

原生offset已按目标逻辑分辨率缩放；Gdx窗口高度不一定是Config逻辑高度，不能据窗口比例反推offset。offset同时移动clip，固定窗口内的读数需要保持clip独立。Standard旧框体可复用26软件的484读数跨度，但26 ARC SCORE SPADA（iidx21）的作者轨道高度473/top11不能被该常数覆盖；这里的iidx21不是21原程序或source21-gmframe0；诊断取[读数适配器](../../tools/IidxSkinAssets/beatoraja_reading.py)。

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

播放器保全的范围选择与全树枚举成本要分开。大型曲库中，对每个不属于保护范围的歌曲文件反复构造Path，会使只读基线耗时显著增加；可以每目录判一次skin归属，按文件名先筛数据库/配置后再构造路径。优化仍须遍历未知子树、拒绝reparse并保持原hash/stat/mtime、目录身份、进程空闲与前后比较语义，不能为提速跳过可能的新数据库。先用实际文件树证明选中集合与旧实现一致，并核非inventory代码未变；不可沿用过期玩家基线或用恢复旧数据抹去期间变化。实际安装和保全取[宿主记录](../../doc_md/other/BEATORAJA_IIDX_SKINS_20261107.md#20-来源-standard-增量)。

交付消费者释放大审计树、仅保留精简身份时，仍须保留后续真实验证器需要的`contract`。原21交付准备曾漏掉它，实际`bind_cases`因此拒绝合法Standard包；只用替身绑定器的夹具未覆盖这条调用。修订保留字段后，实际候选的15场绑定须与冻结计划一致，缺字段及错误族仍拒绝；包和游玩证据不因消费者修订而重建。具体边界取[适配记录](../../doc_md/other/BEATORAJA_IIDX_SKINS_20261107.md)。

源动画闭包齐全不证明导出显示段已装入Lua。`blocking_gaps`也控制真正的selection路径；已证明的20 BPM整数与progress intro适配曾被当成阻断项，HUD/decorations虽生成却没有装配。应核实际coverage的available、shard及最终selection引用，不只看末尾blocking-review。仅按具名适配、明确informational布尔值及已资格来源/阶段登记，未知或缺flag仍阻断；失败输出须新建完整包复验，不能删报告或手改available补签。来源与宿主边界取[20增量](../../doc_md/other/BEATORAJA_IIDX_SKINS_20261107.md#20-来源-standard-增量)。
