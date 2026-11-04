# Standard 皮肤实际 Player 取景

此工具只在隔离存储中通过普通 OSK 导入、模式选择、真实 ReplayPlayer 自动演奏和普通 Results 获取证据，不读取正式玩家数据。合成谱证明此次包的实际绘制与计分路径，不代替原版游戏、完整真实歌曲或实体设备验收。工具不携带原版素材，也不使用 Lightning 成品。

先从当前源码准备合格的 OMS runtime。每次新 shell 先执行仓库根的 `UseDevelopmentStorage.ps1`；再构建本工具，`CaptureRuntimeDirectory` 指向该 runtime，`OutputPath`、`BaseIntermediateOutputPath` 明确指向非系统盘。宿主运行目录须包含其引用的程序集、`runtimes/win-x64/native` 中的原生依赖和 `Skins/Canonical`，不得从现有程序目录复制 `data`、`cache`、日志或玩家配置。宿主启动时检查当前架构的原生目录和 SDL2，仅将该目录加入自身进程的 PATH，不修改用户或系统 PATH。全部测试共用一份程序依赖即可，不为每个组合复制整套程序。

```powershell
. .\UseDevelopmentStorage.ps1
dotnet build tools/StandardSkinCapture/StandardSkinCapture.csproj -c Release `
  -p:CaptureRuntimeDirectory='E:/OMS-Development/evidence/standard-model-20261105/player/runtime' `
  -p:OutputPath='E:/OMS-Development/evidence/standard-model-20261105/player/runtime/' `
  -p:BaseIntermediateOutputPath='E:/OMS-Development/evidence/standard-model-20261105/player/host-build/' `
  -p:MSBuildProjectExtensionsPath='E:/OMS-Development/evidence/standard-model-20261105/player/host-build/' `
  -p:AppendTargetFrameworkToOutputPath=false `
  -p:AppendRuntimeIdentifierToOutputPath=false
```

工程显式包含三份本地 C# 文件和公共 ExactVisualTestRunner，不扫描历史 `bin/obj` 中的源码；切换中间输出目录无需删除旧目录。

运行参数均通过 CLI 提供，未知参数、重复值、错误模式和不匹配的 OSK SHA256 会失败。`--output` 必须尚不存在；`--temp-root` 缺省为该目录下的 `temporary`，进程会在创建宿主前将自身 TEMP/TMP 设到这里。运行程序、临时存储及输出都拒绝系统盘和 reparse 路径。皮肤布局以 `--presentation` 指定，不能同时在 `--options` 内另写 `layout`。

```powershell
& 'E:/OMS-Development/evidence/standard-model-20261105/player/runtime/StandardSkinCapture.exe' `
  --package 'E:/Path/Standard Model Skin Omni Until 26.osk' `
  --sha256 '<完整成品SHA256>' `
  --output 'E:/OMS-Development/evidence/standard-model-20261105/player/bms7-p1' `
  --ruleset bms --keys 7 --presentation p1 --long-note-mode cn `
  --options '{"frame":"<包内实际选项值>"}'
```

BMS 支持 5/7/14K，mania 支持 5/6/7/8/14/16K；单舞台使用 `p1/p2`，14/16K 使用 `dp`。BMS 长条为 `ln/cn/hcn`，缺省 `cn`。包实际未声明的选项不会被猜测或静默替代。

每局输出包身份及运行程序集 SHA256、选择与精确 publication、全部材料来源、选项/轨道几何/绑定/时钟声明、四张自然游玩帧及取景前采样的真实状态和节点/纹理观察、普通 Results 帧与真实成绩，最后以 `completed.json` 和 `host-exit.json` 闭合。运行失败或缺少完成回执均返回非零；图片文件写出不等于目视验收。宿主使用真实渲染 API 取景，不使用 computer use。

完整计分后还会确认真实 full combo 与保留的 `gameplay.complete` 事件，沿自然时钟等待至少 300 毫秒及实际已应用动画帧中可见的纹理片段，然后输出 `full-combo-outro.png`、对应游玩状态与 `full-combo-observation.json`。取景不 seek、不暂停时钟、不修改 alpha；采图后继续自然进入 Results。

`chart.json` 的 `rawWrapperObjects` 是转换前包装对象数，BMS 解码包装在此阶段为零；实际 Player 转换后的 `convertedHitObjects` 单独写入 `player-chart.json` 并要求大于零。该回执另记实际 Mods、曲目元数据、原生 BMS 长条模式及每条长条的模式；捕获要求原生模式符合请求、没有 NoFail，并在普通 Results 确认真正通过。异常日志保留异常链中键名明确的有限数值 `Data`，便于核对引擎给出的场景预算计数，不改变引擎限制。

BMS 每局在自己的 `bga-fixture/` 生成一张 640×480 四色棋盘测试图，含白边、四角编号及 `OMS BGA TEST` 字样；它只用于视口验收，不含原版图像。实际合成 BMS 文件通过 `#BMP01` 和 `#00104:01` 引用它，原生 BGA 播放器经 `WorkingBeatmap.GetStream` 读取该 PNG 的只读文件流。`bga-fixture.json` 保存谱面和图片 SHA256；每张游玩截图的 JSON 记录原生 base layer 实际选中的 image ID、文件、纹理尺寸、读取次数、BGA state/revision、各视口与实际显示窗口的矩形。截图前要求原生纹理确已加载，不能以默认背景或事件声明替代 BGA 图像证据。mania 继续使用原本的合成谱，不注入 BMS BGA。

既有内层测试存储依框架合同在退出时回收；输出证据与共享 runtime 字形缓存保持。工具没有清理旧任务产物的入口。
