# IIDX 本地素材与作品工具

读取用户提供的现有 HDD 美术目录，建立来源清单，并在独立非系统盘目录中处理 IFS 副本。原始图、原始 XML / AFP / 几何数据和按 UV 裁切的 PNG 分别保留；`index.html` 可离线搜索、筛选和预览。

## 使用

在 OMS 仓库根目录的新 PowerShell 中运行。Python 3.11 以上；依赖安装到本仓开发缓存，不安装进游戏目录或系统 Python。

```powershell
. .\UseDevelopmentStorage.ps1
python -B -m pip install --target .dev-cache/tools/iidx-assets --no-cache-dir --no-compile -r tools/IidxSkinAssets/requirements.txt
python -B tools/IidxSkinAssets/build_library.py --game-root 'X:\IIDX-HDD' --output 'Y:\IIDX-Art-Library' --ifstools-path .dev-cache/tools/iidx-assets
```

`--output` 必须尚不存在，父目录须已存在；输出不能与整个游戏根目录重叠，也不能落到系统盘。失败时保留已有输出供诊断，`run.json` 仅在整次成功后标记 `complete`，不覆盖或猜测续接。

`selection-33-hdd.json` 是当前 33 HDD 的首批取材清单：27～33 编号框体、00 号共用素材变体与当前界面共用件。它不是历代完整覆盖声明；`00` 等素材编号不能解释成代数。新增当代 HDD 后先核对实际目录，再提供对应选择文件，不猜旧版本的结构。

## 输出与验证

- `source-inventory.json`：只扫描 `contents/data/graphic` 与非 `_cache` 模组的 `graphic`，不读取账号、卡号、游戏配置、曲谱或声音。
- `source-copies/`：经过 SHA-256 对照的 IFS 副本，第三方解包器只接收这些副本。
- `packages/<原相对路径>/raw/`：完整解包文件，保留纹理外沿及动画关联清单。
- 同包 `sprites/`：由 ifstools 按原 `uvrect` 解出的有效纹理，不做缩放或重绘。
- `library.json`：每张图的包来源、原始/UV 矩形、精灵尺寸、图片摘要；相同内容只登记，不删除或跨代替换。
- `run.json`、`packages.json`：完成状态、工具版本与每包回执。成功前核对所有 PNG、有效尺寸、源包摘要及整个美术清单的文件名/大小/最后写入时间。

原版素材、精确源清单与生成库只留本地忽略目录或仓库外，不加入 Git、内置静线或公开发行。工具本身不启动游戏、不安装注入器、不重打包 IFS、不操作 OMS 玩家数据。

## 生成本地 27～33 游玩作者包

`build_skin.py` 消费已核对的独立素材库和 AFP 研究输出，保留原图字形与帧序，生成普通公开作者包及相邻 `.osk`。`--style` 支持 27 HEROIC VERSE、28 BISTROVER、29 CastHour、30 RESIDENT、31 EPOLIS、32 Pinky Crush；省略或指定 33 保持既有 Sparkle Shower 输出。研究输入须包含 `skin-reference-map.json`、`gameplay-assets.json`、`extra-note-library/library.json` 与过滤后的 `static-frames/`，布局包及原画布须与 style 一致；另以 `--fullcombo-research` 指向含 `fcombo00-author-data.json` 的全连研究目录。全连使用原纹理、源帧时间与透视四边形，不烘焙整屏序列。这些来源资料和美术同样只保留本地，不由脚本从游戏安装根读取。

```powershell
. .\UseDevelopmentStorage.ps1
python -B tools/IidxSkinAssets/build_skin.py --style 31 --library 'Y:\IIDX-Art-Library' --research 'Y:\IIDX31-AFP-Research' --fullcombo-research 'Y:\IIDX-FullCombo-Research' --output 'Y:\IIDX31-Author-Candidate' --image-tools-path .dev-cache/tools/iidx-assets --game-root 'X:\IIDX-HDD'
dotnet run --project tools/SkinAuthoring -- check 'Y:\IIDX31-Author-Candidate'
```

输出目录及 `.osk` 均须尚不存在，不能与输入、受保护的游戏根重叠，也不能落系统盘。资源在生成后检查 PNG、总像素和资源数；`generation.json` 只有整包写入成功后标记 complete。作者检查通过仍须正常导入 OMS 并观察真实 Player；`local-study.json` 和 `READ-ME.txt` 明确记录当前还原缺口，不能把生成成功当成完成作品。

27～29 还需 `--legacy-library 'Y:\IIDX-Legacy-Common-Library'`，指向经过独立副本核对的 `0/common` / `0/gameparts` 库；研究输入增加 `effective-bga-viewports.json` 和真实渲染的 `dp_frame_1p-static181.png` 固定符号层，29另需 `candidate-score-slots.json`。源图保持1280×720，生成器明确采用1.5倍作者投影，轨道438×726、白/黑/皿51/39/90、间隔及边距3、双侧间距132；未将当前共用HD音符和击打几何重复放大。小BPM保留28×18原PNG，在scene中按原19×16全UV几何显示。HS按各布局源位置取步进，并独立放置原始小数点；相同度量共用字体，保持最多8套字形预算。分数/BPM的多位排布是框内候选，单个源占位符不证明原版动态数字算法。

27保留原SCORE/MAXCOMBO面板，最高连击读取公开 `combo.maximum`（本局实际已达最高连击）；源181帧没有曲名/作者hook，不在分数面板上叠文字。SD视频使用实际框体透明开口内的安全矩形，将黑底置于原框体下方；窗口候选不证明原版的视频裁切、复制或适配规则，负坐标裸movie矩形不能直接盖在轨道上。1～26已找到真实版本标识，但已查来源未覆盖对应原版游玩框体、几何、动画及默认绑定；待用户提供26 HDD后继续逐代核对，不用标识图冒充整代皮肤。

SD源红线按原GEO在scene显示，原生必需判定线槽用透明材质 `Provide` 避免重复；该槽不允许 `Suppress`，不能为作者包放宽合同。28/29双打曲名/作者限制在可见左列，长文本显示省略号；原文字占位图的宽度延到轨道下方，不能直接当成有效可见文字区。这些排布候选仍需当代实机对照。

各代使用当前 HDD 保留的对应 `gmframe` 框体、数字和逐布局转盘坐标；只按实际世界矩阵决定2P镜像，30的转盘正X不沿用31/32负X。普通血槽来自该框体，Hard/EX-Hard采用当前共用包中明确命名的图，Hazard暂沿普通血槽外观。音符、CN/HCN、判定、击打与全连同样来自当前共用包，尚无各代历史默认组合的实机证明；中心平移、轨道背景变换、判定锚点、文字字体与装饰动画也须继续校准。31背景轨道的源矩阵底部约693px，红线和键灯仍在716/728px附近，仅据背景不修改原生演奏高度。不能将33的实机截图记为其它代验收。

使用包含本次作者能力的 OMS 工作区导入 `.osk`。包自身声明BMS5/7/14K和mania5/6/7/8/14/16K，默认1P；在皮肤设置的“皮肤配置项”选择2P，按皮肤和模式独立保存，下次进入游玩生效。5K保留近皿五普通槽；mania5/7/14留空皿位，6/8/16实际占用皿位。留空、白黑贴图、固定转盘中心和七位mania分数排版均由作者包处理；引擎不识别IIDX名字。未支持键数沿原canonical回落，选歌与结算仍是OMS。

`--customization-library`可提供独立核验的notes01..11、beam01..03与lane001..006来源；连同原00变体，包内生成12种音符、4种光柱和7种挡板选择。爆炸有原尺寸/紧凑及0.5..1.5缩放，判定有连击/仅文字，光柱可关闭；这些选项通过同一公开binding/variant使用原资源，不改变判定、输入或长条时序。没有该来源参数时不暴露对应选择。新增素材仍以独立selection交给build_library，保留原包与UV数据，不能给Builder指向游戏根。

上述仅为当前素材子集，不能视为完整Play Customization。FC、爆炸和判定字目前只消费00设计，连击显示开关不等于判定字体选择，紧凑/缩放不等于爆炸设计选择。完整来源、普通/CN头尾厚度及beam原档图对照、全部FC结构与辅助区缺口见[P1-A完整性记录](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-18iidx-custom完整性核对)；当前工具尚未提供这些完整选择，不修改旧包内generation状态冒充接入。

爆炸按BMS命中区与mania全列宿主分别定义作者模板，复用同一原始帧、时间和采样；mania通过包内静态坐标保持原帧宽高，不能改公共宿主来适配某套作品。固定转盘中心已归框体，过时的键区转盘模板不再生成。scene仍守512KiB上限，短效果ID用于节省重复引用；框体当前按向下落键设计，反向滚动尚未专门校准。

BPL使用同一生成器，通过`--frame-package`指定实际归档标识、`--identity`给稳定节点前缀、`--name`给本地显示名。研究输入必须逐队对应；同位置或同数字PNG不意味着可以混用战队框体。当前来源含S5七队及0目录SUPERNOVA高清框体；1目录同名旧SD研究另存，不能依据相似文件名或小PNG推定相同画布。若research有effective-bga-viewports.json，HD也使用逐框透明开口和原框下方黑底，避免裸movie矩形覆盖装饰；合成框体显式nearest。原动画仍冻结源181帧，未复刻完整原版界面。

## 通过隔离副本采集原版参照

原始可玩安装保持只读。运行参照使用另一处完整物理副本；启动前核对路径，独立保存配置、补丁选择、截图和日志，结束后复核源安装清单及关键文件摘要。副本清空卡号与自动插卡设置，不使用真实卡或账号；配置和 API 密码只存本地忽略目录，不进入 Git 或命令输出。

`reference_api.py` 只连接会话指定的 `127.0.0.1` Spice API，要求非空独立密码；提供截图、按钮、模拟轴、触控及投币，不提供卡、内存或电源操作，也不负责复制或启动游戏。Spice API 服务自身的监听范围须在启动配置中另行核对，本客户端的 localhost 限制不能证明服务只监听回环。

会话 JSON 包含 `host/port/password/captureRoot`。截图仅允许在 `captureRoot` 内新建，不覆盖已有图片；持续导航可用 `with ReferenceApi(session_path) as api` 复用连接，避免每次输入都弹出连接通知。

```powershell
. .\UseDevelopmentStorage.ps1
python -B tools/IidxSkinAssets/reference_api.py --session 'Y:\IIDX-Reference\api-session.json' screens
python -B tools/IidxSkinAssets/reference_api.py --session 'Y:\IIDX-Reference\api-session.json' capture --screen 0 --output 'Y:\IIDX-Reference\captures\reference-001.jpg'
```

截图用于对位，连续帧辅助观察动画；普通运行日志不保证列出每次纹理绘制。素材关联仍需对照原 XML、AFP / GEO 和实际画面。副本为适配参考环境启用的启动检查调整必须单独记录，不作为原安装、设备或原版刷新率的验收结果。

需要在内置浏览器预览时，可从素材库启动只监听本机的 HTTP 服务；该浏览器不允许 `file://`，不调整其安全策略。预览服务只指向生成库，不指向 HDD 或整个工作区。

```powershell
python -B -m http.server 8768 --bind 127.0.0.1 --directory 'Y:\IIDX-Art-Library'
```

当前授权、作品范围与下一步以 [P1-A 计划](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md)和[约束](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)为准；解包成功不代表动画、布局或游玩效果已验收。
