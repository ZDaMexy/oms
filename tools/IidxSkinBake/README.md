# IIDX recorded-animation 离线烘焙

此工具把已有 `oms-gameplay-recorded-animation.v1` 和其 manifest 资源导出为供另一宿主消费的 PNG atlas 与 `index.json`。原作者源只读；工具、原素材和导出产物分开保存，Standard 与 Lightning 各自运行、各自输出，不组成混合素材库。

## 使用

在 OMS 根目录加载开发存储入口，先确认当前 Release 运行库存在，再只编译本工具：

```powershell
. .\UseDevelopmentStorage.ps1
dotnet build tools\IidxSkinBake\IidxSkinBake.csproj -c Release
dotnet .dev-cache\temp\iidx-bake-dev\bin\net8.0\IidxSkinBake.dll `
  --runtime osu.Desktop\bin\Release\net8.0 `
  --author <独立作品的冻结作者目录> `
  --manifest <相对该目录的manifest.json> `
  --animation <manifest内animation-ID> `
  --frames 0,30,60-62 `
  --output .dev-cache\temp\iidx-bake-dev\<全新输出目录> `
  --verification-images false
```

`--frames all` 保留全部原帧。列表和闭区间表示**原 clip 的零基索引**，不会重新编号；FPS、触发类型、循环起点、held loop、release frame 与时长都取正式解码结果。`--page-size` 默认 2048，可用 64～8192 的二次幂，只控制装箱上限，不缩放图像。输出必须是本 checkout 的 `.dev-cache/temp/iidx-bake-dev/` 或 `artifacts/` 下的新目录。目录已存在、路径逃逸、源引用经过 reparse point、源文件在导出期间改变均拒绝；不覆盖失败试产目录。

较长 normal 段中逐图元 RGBA8 舍入与整体回存会累积差异；若完整导出因此未过像素门，可用 `--normal-run-pieces 4` 保持原顺序再细分连续 normal 段，增加少量目标绘制对象以降低量化误差，不提高验收门；细分后仍须通过验证。默认 4096 不额外细分；实际取值写入索引。additive 段保持原连续分组。

构建时可用 `-p:BakeRuntimeDirectory=<当前已验证OMS运行库>`。工具内的窄反射调用精确生产 API，并钉住构建时 `osu.Game`、`osu.Framework`、`osuTK`、ImageSharp 四个 DLL 的字节身份。`--runtime` 不匹配便拒绝；没有兼容旧签名或改换解析器的回退。工具不会启动完整 OMS Player、访问玩家库或运行原版 HDD。

## 绘制与像素合同

正式 manifest/animation Codec 负责校验，生产 `GameplaySkinRecordedAnimationProgram` 求解原相机、父子变换、UV、遮罩和颜色状态，生产 `GameplaySkinRecordedAnimationDrawable` 与原 shader 实际绘制。薄 GPU 宿主只选定源帧、保留有序图元并读取原画布大小的透明 framebuffer。它等待所有可见纹理上传完成；源动画不跟随宿主墙钟推进。图形后端由框架选择，实际身份保留在私有输出 `host-storage/logs/`；不能把 Direct3D 对照记作目标宿主的 OpenGL 验收。

每帧按**原图元顺序的连续 normal/additive 段**捕获，不能把两种混合合成一张黑底 normal 图。两种 PNG 编码不同：

- `normal`：RGB 按框架 framebuffer 的字节色彩空间保留，透明度记录 Porter-Duff 覆盖率，再转为 straight RGBA8；目标混合为 `SRC_ALPHA, ONE_MINUS_SRC_ALPHA`。框架原 Mixture 的 Alpha 是相加，分段透明捕获仅将 Alpha 方程改为覆盖率；RGB 方程与 shader 不变。
- `additive`：RGB 已包含原全部源 Alpha 的权重，非零贡献像素设 Alpha=255，零贡献清为 RGBA=0；目标混合为 `SRC_ALPHA, ONE`。不能再把其 Alpha 解释为原图透明度或重新乘一遍原权重。

保证范围是原整数像素与**不透明最终背景**；不承诺恢复 OMS 的目标 Alpha 累加值。没有附加 gamma 转换。透明分段与 RGBA8 回存存在量化误差，不能称 PNG 与直接源绘制逐字节相同。每个不同的生产端已求值帧均在黑、白、彩色三个不透明背景下，用同一生产渲染器比较原动画与烘焙段重组；只有全部 float 顶点、UV/Q、颜色参数、资源、混合与图元顺序逐字节相同的帧才复用已核结果，仍保留每个原帧及其时长。当前门为单通道最大差 ≤4/255、扣除每通道 1-LSB 正常量化后的平均超额 ≤0.05/255。原始平均差仍单独报告；这样小画布高覆盖的 1-LSB 舍入不会被错误判得比大画布稀疏画面更严重。实际差异写 `verification.json`，任一帧超门不发布 `index.json`。这是离线绘制验证，不能代签 beatoraja 实际导入、目标混合设置、播放计时或缩放质量。

裁剪只去掉无贡献边界，记录相对原画布的 offset；不重采样。相同尺寸且 RGBA 完全相同的段复用同一 atlas rect。图集使用一像素透明间隔、nearest 采样，页面裁到实际使用范围；目标端若缩放或改线性滤波须独立验收。每页和每个 rect 均有摘要供闭包验证；保存后重新读取所有 PNG，从每个 rect 逆向恢复并核对 RGBA。`pixelSha256` 为小端 uint32 宽、高后接逐行 RGBA8 的摘要，不是 PNG 文件摘要。

## 索引

`index.json` 的合同是 `oms-iidx-baked-animation.v1`：

- `canvas`、`registrationPoint=[0,0]`，坐标明确为左上原点、X 向右、Y 向下；目标宿主自行转换坐标。
- `sourceFrameCount` **包含空终止帧**；`durationMs` 不包含该零时长标记。`loopStartFrame` 等全部仍为原索引。
- `selectionComplete=false` 表示仅代表帧，不能当完整动画播放。`selectedFrames` 列出实际选择。
- `pages[]` 记录 PNG 文件、实际尺寸、采样方式和字节摘要。
- `frames[]` 记录 `sourceFrame/timeMs/durationMs/terminal`，`runs[]` 按数组顺序绘制；每段有 `order/blend/page/rect/offset/sourcePieceRange/pixelSha256`。`rect=[x,y,width,height]` 是图集像素，`offset=[x,y]` 是原画布像素。
- 无贡献帧保留条目，`runs=[]`；空终止帧不生成空 PNG。相邻相同帧仍保留原时长，像素页可去重。

`--verification-images true` 额外保留原背景对照及分段原始读回，适合少量代表帧；完整动画默认关闭，避免重复大图。源文件身份、具体本机路径与运行库身份仅落私有输出收据，不进公开源码或文档。默认 gate 通过后仍须由目标宿主真实绘制并确认混合顺序、原注册点和结束清理。

## 已烘焙像素的无损分块

完整画布动画可能 PNG 很小、解码显存却很大。可对已通过验证的原始 bake 索引分块，不重新渲染、不降帧、不缩图：

```powershell
dotnet .dev-cache\temp\iidx-bake-dev\bin\net8.0\IidxSkinBake.dll `
  --runtime osu.Desktop\bin\Release\net8.0 `
  --tile-index <已通过的原始index.json> `
  --tile-size 256 `
  --output .dev-cache\temp\iidx-bake-dev\<全新分块目录>
```

支持 128 / 256 像素网格，网格固定于原画布。每个原 run 独立切分，只有 RGBA 四通道都为零才算空；每个 `(原order, blend, 网格cell)` 使用整段动画的非零像素联合裁切框，使出现帧共用固定 offset/size。每帧 `order = 原order × 网格cell总数 + 行序cell编号`，保持严格全序，不因空格省略而漂移。

默认 `--tile-crop movie-union` 使用上述固定框。若透明 padding 仍过多，可使用 `--tile-crop aligned16`：对每帧每格的非零 RGBA 外接框向外对齐到画布的 16 像素网格，增加固定透明 padding，不裁像素、不缩图。它比全动画联合框省像素，但可能产生更多不同 offset/size 的 imageset 组；须据实际数据与目标性能选择。

**目标 imageset 分组必须包含 blend**，同一 order 在不同帧可能采用不同混合；建议键为 `(order, blend, offset, width, height)`，只显示当前帧存在的组。新的 `runs` 仍兼容原 page/rect/offset 接口；`sourceFrame`、时长及循环字段原样保留。原运行库的背景对照另存 `render-verification.json`，分块 `verification.json` 逐帧重建每个原 run 并要求 RGBA 摘要完全相同，再核对每个新 PNG rect。这一更强的分段逐像素等价连同未改变的 per-pixel 层序，证明整个帧的混合结果不变。

`tiling-summary.json` 记录原/新解码字节、PNG 字节、最大每帧 draw 数及真实 imageset 分组数，索引 `optimization.decodedPixels` 提供相同统计供目标导出器挑选完整、verified 的候选；选择网格时同时看占用与绘制对象数量。透明裁边和图集去重只改变存储，不能据压缩文件大小声称低显存，也不能由离线等价代签目标端性能。

## 冻结作者的串行工作清单

`batch_frames.py` 从内容目录和实际材料实例选取每个框体的稳态及完整 READY / Fail。稳态优先使用节点引用的纯 literal 数值时钟，保留它对应的原帧；不能把 loopStart 一律当作稳态。`plan_components.py` 枚举 Key Beam、Explosion、Full Combo 的实际挂载；`plan_feedback.py` 枚举实际 gauge / scoreCrossing 消费者；`plan_object_feedback.py` 核对 Long Note 与 Key Flash 的明确声明和贡献分支。不把 manifest 中未挂载的动画算成反馈，作者未声明也不证明原 HDD 没有相关美术。规划与执行分别进行：

```powershell
. .\UseDevelopmentStorage.ps1
python tools\IidxSkinBake\batch_frames.py `
  --standard <Standard冻结作者目录> --lightning <Lightning冻结作者目录> `
  --output .dev-cache\temp\iidx-bake-dev\<全新规划目录>
python tools\IidxSkinBake\batch_frames.py `
  --execute .dev-cache\temp\iidx-bake-dev\<规划目录>\worklist.json
```

Python 只组织原 C# 烘焙器，依赖仓库既有 IidxSkinAssets 与本地图片工具环境。复用必须在同一作品内证明原动画文件、资源声明（除存储路径）和全部资源字节一致；不同 manifest 的复用保留目标来源收据。partial 只能满足实际包含的原帧；完整录制才能供完整动画使用。页可在同卷 hardlink，共用的页保持只读。

每项开始前重新核对源场景与资源；已有输出拒绝覆盖。工作目录出现 `pause-between-jobs` 文件时，当前项完成后暂停，移除后继续。Full Combo 必须先在私有清单中逐项记录规模审核；`programSizeReviewed` 不是效果验收。

工作清单可设 `minimumFreeBytes` 保留空间，并以 `losslessTilesAboveBytes` 对超过实际解码量的结果生成 128 像素 movie-union 候选。默认仅在候选解码量更低时选择它；`selectLosslessTiles=false` 保留原结果，供目标适配器比较后再选。记录同时区分 imageset 键 `(order, blend, offset, size)` 和静态裁片键 `(order, page, rect, offset, blend)`；两者对象数量不能混用。`results.json` 保留每项原/选中索引、显存像素估算、对象组和可用空间；它仍不代替目标宿主的真实性能与画面验收。
