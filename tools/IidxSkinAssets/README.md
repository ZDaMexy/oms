# IIDX 本地素材与作品工具

读取用户提供的现有 HDD 美术目录，建立来源清单，并在独立非系统盘目录中处理 IFS 副本。原始图、原始 XML / AFP / 几何数据和按 UV 裁切的 PNG 分别保留；`index.html` 可离线搜索、筛选和预览。

## 统一 Lightning 时期作品

当前整合入口为 `build_omni_skin.py`，作者作品基础名称固定 **Lighting Model Skin Omni Until 33**，默认33；27～33和八支BPL进入同一Game Frame选项。各代及BPL的candidate09目录是整合器当前读取的Frame来源，重建时须保留；它们不再作为多套成品交付，已被替代的旧包和暂存可清理。Until34不在本轮范围。

Omni 是供作者使用及熟人间私下分享的独立 `.osk`，不内置到 OMS，也不随客户端公开发行或公开制作套件分发。客户端的通用皮肤能力和素材作品分开交付；默认与保底仍为静线。

最终Omni交付文件追加`.vyymmddxx`版本，`xx`为当日从`00`开始递增的两位序号，统一收存在[总工作区入口](../../../AGENTS.md)约定的`deliverables/skins/`中，每版附说明、SHA256与原验证记录；日期按Asia/Shanghai，旧版不改名或覆盖。仅为已验证包改文件名时，逐字节核对并保留原验收日期，不重打包或冒充新产品验收；作者源及研究输出仍留原位置。版本化私有产物不作为源码文档的内容依赖。

Omni所有可见开关默认开启，包含Graph Area与实时JUDGE；模式/键数的显式保存值继续按原合同优先，不把BMS设置隐式复制给mania。首排预告保留真实滚动退场，同时受准备阶段约束，与GET READY一起结束。`build_omni_skin.py --repackage-directory`从冻结作者闭包重建时也执行这两项修正，并保留原素材字节。

新增来源工具 `build_customization_catalog.py` 建完整原始UV/AFP/GEO库；`build_fullcombo_catalog.py` 与 `export_customization_animations.py` 输出中立记录数据；`customization_modules.py` 生成共用选项和贡献闭包。整合工具仅打开经过核对的独立库，不打开游戏安装。已有冻结模块可用 `--modules-directory` 重用，逐文件SHA一致后复制，避免重复提取。

```powershell
. .\UseDevelopmentStorage.ps1
$env:PYTHONPATH = (Resolve-Path '.dev-cache/tools/iidx-assets').Path
python -X utf8 tools/IidxSkinAssets/build_omni_skin.py --frames-root 'Y:\FrameCandidates' --custom-library 'Y:\OriginalCustomLibrary' --fc-catalog 'Y:\FullComboLibrary\runtime-v2-catalog.json' --modules-directory 'Y:\FrozenModules' --music-progress-reference 'Y:\MusicProgressReference\source-geometry.json' --output 'Y:\OmniAuthor'
dotnet run --project tools/SkinAuthoring -- check 'Y:\OmniAuthor'
```

重新导出模块时省略 `--modules-directory`，改为提供 `--bemani-root`（与来源收据一致的独立 bemaniutils checkout）、`--image-tools`（下方依赖目录）和 `--frame-maps`。后者是15个Frame ID到各自原始 `skin-reference-map.json` 的JSON映射，路径相对映射文件所在目录并须位于其下；例如 `{"iidx33":"iidx33/skin-reference-map.json",...}`。`customization_modules.py` 接受相同三项参数；单独导出动画也须显式提供前两项，不再依赖某天的研究缓存路径。AFP解析器版本取来源收据，不由脚本自动下载或更新；玩家导入成品只需要对应OMS版本。

实际选项包括15框体、27爆炸、46转盘、21判定、12音符、11套CN/HCN、15光柱、366挡板和25FC；Graph Area可选空白或366同源艺术，LIFT仅保留366款艺术、默认000，紧接Lane Cover。音符/CN尺寸、EXP缩放、光柱长度独立于设计。notes11缺少原尺寸档，复用同11原图做显式作者纵缩，不能换00冒充；HCN额外配色是同款CN原材质的作者选项，不声称已确认官方颜色编号。

`build_lighting_lift_default.py --source 'Y:\OldAuthor' --output 'Y:\FreshSibling'`仅修正冻结包的LIFT目录，不执行旧几何修复或重新导出素材；适用于已压缩本地ID的当前作者。它移除作者额外添加的`blank`纯黑款及其无引用文件，将默认改为`l000`，并把配置放在Lane Cover之后。新目录有1271款/539张预览；保留款式ID、贡献字节与选中上界不变，黑色内层衬底仍是艺术图的组成，不是可选款。普通源生成及旧冻结模块入口采用相同规则；后续亮度/轨底、HUD、高度和百分比工具接受旧1272/540及新1271/539两种父目录，并按实际输入登记保全数量，不改玩家保存值。

`note_geometry.py`在完整导出和旧包重建时分别归一BMS与mania的单点、长条头尾。BMS原22.5载体、模板及身体保持；mania按原00标记图尺寸和实际48参考宽度换算高度/偏移，以不同C4资源名选择专用模板，只复制九张原图为逐字节相同的标记别名。不改图片或公共host，不同时实例化两套音符；CN/HCN资源变体保留。真实游玩须另核原生AutoSize高度、替代Sprite矩形及1080p原图尺寸，codec或自动演奏计分通过不证明音符可见。

根options使用33框体、源00部件、normal尺寸；FC默认为已确认33主题的fc24，fc00标为通用款，全部25个旧选值保留。fcombo24原角色图明示33及果汁主题，原机菜单默认ID尚未实测；生成或重包不会读取、改写玩家已保存选值。Graph等适用开关默认开启。最新细节构建器接入真实当前/理论满分、符合当前用户/谱面/玩法/完整mods的有效本地个人最好，以及BMS EX的AAA/AA/A目标；无记录或非EX玩法不制造目标值，没有联网对手成绩。LIFT仅跟随BMS原生抬升。FC保留三布局终止空帧和原时长，19为3500ms、24为3000ms，其它2500ms；滤镜顺序和真实host设置仍待原版画面校准。

`build_lighting_details.py`在已验证的完整v14作者目录上增量制作开场版本，写入全新作者目录及相邻`.osk`。旧CUSTOM、FC资源和INI保留，只校准上述FC默认与两款标签；`lighting_lane_intro.py`消费15框体各自SP/DP的原始轨道开场记录，`lighting_ready.py`消费完整`1p_ready`父movie（含出现和退场），`lighting_panels.py`换用完整JUDGE原图并声明实际AUTO/有符号偏移与滑入。旧外框组件入场曲线保留，仅切到公开准备时钟；不向规则集加入IIDX布局或演出名字。

Graph开启时收窄SP的原生BGA窗口；DP使用左侧视频，右列留给图表。DP的JUDGE位于右下角，单独开启时限制右侧视频底端；旧27DP窗口跨入底部HUD保留区的问题在作者INI修正，不放宽公共避让检查。Graph/JUDGE、首排预告、外框入场、轨道开场和GET READY均默认开启，可分别切换。预告保留原`start_notes_s/w/b`三图，仅筛选全谱最早同排，并在原生音符进入可视区域前退出，最迟与准备阶段一起退出；空槽或之后各轨的首音不再另播。转盘00两侧顺时针已有实际参照；位置、蓝光波形、血条闪烁周期和转盘常转速度仍为作者适配，不能当作原机精确校准。

`lighting_motion.replace_gauge_front`可在冻结的v15贡献上迁移前沿，不重新解包或改写原图。每个唯一Frame布局调用一次，v15实物宽8px须显式传`gauge_dot_width_px=8`；当前完整fill及其mode分支沿完整UV裁出末端窄条，P2保留原补偿。中性明暗调制使前沿真正闪烁，NORMAL过线及Hard/EX HARD/Hazard跟随本体颜色，通用引擎不写IIDX颜色规则；旧dot声明仅在无引用时退休，物理文件由新闭包排除。迁移冲突不提交半改，重复执行不变；作者发布后仍须检查公开codec和原生绘制。

`lighting_persistent.py`先按原层次重绘并逐RGBA核对旧背景，再排除明确组件重绘；输出独立reference，输入作者和素材只读。已核范围为28的lane-base入场、33及七支S5队伍各自的持续星形，共9框体/27布局。BPL必须保留原safe-BGA开口与DP固定符号层，不套33的组合；原星PNG、UV、mask与源循环分别保全，不擦图或烘焙整屏序列。`upgrade_lighting_components.py --source <原作者> --reference <完整reference.json> --output <全新作者目录>`消费该证据，生成新闭包和相邻OSK；`lighting_local_ids.py`仅缩短CN/Judge的内部声明及类型明确的引用，资源/字形/动画/拓扑/路径/选项键保持，减少配置重复。生成后的预算、codec与实际Player仍须独立验收，详见[持续组件记录](../../doc_md/other/OMNI_OPENINGS_20261021.md#2026-10-24持续组件与原版参照复核)。

Start设置迁移分两步：`build_lighting_start_options.py`消费实际菜单的显式review JSON及原canonical小节线PNG，增加一个共用物件RGB亮度设置；`build_lighting_columns.py`另消费45布局的原轨底分层reference和独立review JSON，增加Column设置。两者均新建作者兄弟目录和相邻OSK，拒绝改旧作者；父目录款式、预览及旧选值保全。物件与轨底是两个设置，不能按五类物件再造五个独立旋钮，具体范围和历史1272/540证据取[Start记录](../../doc_md/other/OMNI_START_OPTIONS_20261024.md)。

`lighting_columns.py`按原网格、UV、mask、Alpha和采样拆开底色，先证实源图重合成一致；保留28开场的base/column交错次序、hold和sentinel，同时退出C4/C5的旧baked游戏纹理。`--mode probe/merge`只产研究reference，`--mode upgrade`必须有完整15框体×三布局及哈希固定的`--reviewed-column`；JSON精确字段和准入见工具模块说明。没有review输入时不推断范围、步长或原版内部混合。构建后仍须现行CLI、实际Player的默认/最低值像素及非column区域验证，离线图不代签GPU或前台性能。

`build_lighting_native_hud.py`在下一份全新作者中，仅为45份Frame manifest的225个既有profile增加公开`modsRect`，将同一个OMS原生ModDisplay放到柜体空位，避免遮住Graph分数。其余布局、scene字节、素材、选项、预览保持；这是OMS摆位适配，不生成IIDX Mod美术或改变输入/计分。

`build_lighting_judgement_position.py`消费独立review JSON中真实游玩−2/0/+1/+10位移和OFF证据，以及225个profile/42组SD-HD贡献的几何审查，新增整体高度配置与独立内层关闭门。位移由既有根与同高关闭门各分半，守单节点绝对位置4预算，不增加额外节点或放宽限制。三种布局沿既有宿主高度换算，原行排版、字体、RGB和生命周期保持；锚点间线性插值、P2/DP沿用位移均明确属于OMS作者适配，未以设置页缩图或其它侧别代签逐像素校准。未取得完整review时拒绝生成。

`build_lighting_gameplay_controls.py`从冻结父作者及独立完整原图库新建游玩Start面板、上下挡板/LIFT白绿字和SUB TARGET。它核原AFP/GEO、窗口/字形来源及白字着色与原绿字逐RGBA相等；上下白字分别读真实Sudden/Hidden单位与active，LIFT位置沿原生已应用offset，mania不显示BMS调速面板。读数在完整Stage坐标跟随真实边缘，横排叶子保持合法像素尺寸，不随挡板长度或透明度缩放。Normal栏位与滑块沿OMS原速度范围，原机档位到倍率整表未推定。SUB只在主目标缺数据时接替，两者缺数据显示NO DATA，不生成对手或全国成绩。

为守原预算，工具先证明15框体三布局的Graph声明归一后相同，再用实际`layout.presentation`和受限x/width变体共用；不依赖DP不适用的保存layout选项。366款LIFT的重复字体/读数改为同一原生边缘模板，FC只给原PNG建立更短的内容路径，原资源ID、像素、动画与选值保持。父作者和原包只读，新包必须另过当前CLI、真实Player和配置保存；闭包/CRC成功不代表引擎或画面验收。完整本轮边界取[游玩调速记录](../../doc_md/other/OMNI_GAMEPLAY_CONTROLS_20261025.md)。

`audit_lighting_asset_usage.py`将冻结作者闭包与独立原图库逐RGBA比较，展开压缩scene、C2选项/条件、INI、字形与atlas裁片，并核对显式派生/tint收据。输出来源引用JSONL、独立像素候选及联系表，区分直接/等像素引用、已确认派生、待核候选、范围外及证据不足；没有直接打包原PNG不等于未使用，候选不等于缺失功能。主库未解包的IFS仍属未查范围。输入保持只读，输出必须是独立新目录，完整清单和图片仅留本地。参数`--derivation-receipt / --tint-receipt / --module-receipt`可重复提供，只接受实际生成收据，不按名称推断派生。

`build_lighting_pacemaker_targets.py`将原graph-target以同ID迁入C2，保留六个旧值/默认并加PACEMAKER NEXT；只发布所选目标及必要SUB树以守原预算。`build_lighting_timing_display.py`在下一独立作者中接四张原FAST/SLOW图片，增加BMS专用A/B/C、Separate Display及OFF，默认A。它必须消费独立`--positions`审查收据，核对15框体/既有全部Judgement款的相同宿主，以及105分支的声明/引用ID；不凭菜单预览推断原生精确坐标。两步均拒绝覆盖父作者和旧OSK，不增加字体/预算；数据资格、真实原版证据与OMS适配边界见[本轮记录](../../doc_md/other/OMNI_GAMEPLAY_COMPLETION_20261026.md)。

```powershell
. .\UseDevelopmentStorage.ps1
python -B tools/IidxSkinAssets/audit_lighting_asset_usage.py --author 'Y:\FrozenAuthor' --library 'Y:\OriginalCustomLibrary' --fullcombo 'Y:\FullComboLibrary\v3' --localized 'Y:\LocalizedLibrary' --output 'Y:\FreshUsageAudit'
python -B tools/IidxSkinAssets/build_lighting_pacemaker_targets.py --source 'Y:\FrozenControlsAuthor' --output 'Y:\FreshNextAuthor'
python -B tools/IidxSkinAssets/build_lighting_timing_display.py --source 'Y:\FreshNextAuthor' --library 'Y:\OriginalCustomLibrary' --positions 'Y:\ReviewedTimingPositions.json' --output 'Y:\FreshTimingAuthor'
```

```powershell
. .\UseDevelopmentStorage.ps1
python -B tools/IidxSkinAssets/build_lighting_gameplay_controls.py --source 'Y:\PreviousOmniAuthor' --library 'Y:\OriginalCustomLibrary' --output 'Y:\NextOmniAuthor' --evidence 'Y:\GameplayControlsEvidence'
python -B tools/IidxSkinAssets/test_lighting_gameplay_controls.py
dotnet run --project tools/SkinAuthoring -- check 'Y:\NextOmniAuthor'
```

`build_lighting_percentage_target.py`在再下一份全新作者中追加百分比目标及0..100、步长1、默认50的数值配置。范围/默认取官方说明，review的`authorStep:1`单独记录作者整数步长，不冒称原版十键精度；原版50档实际选中的截图另行固定，旧五档目标保全。原柱使用已有非循环配置采样轴，原数字旁用普通文字显示百分号，不猜算原版EX整数取整、rotate或Ghost。两个工具均保全父作者/OSK及父目录的全部款式和预览，发布前仍须当前CLI与真实Player验收。

```powershell
. .\UseDevelopmentStorage.ps1
python -B tools/IidxSkinAssets/build_lighting_columns.py --mode upgrade --source 'Y:\SharedBrightnessOmniAuthor' --column-reference 'Y:\ColumnSource\reference.json' --reviewed-column 'Y:\ColumnReview\reviewed-column.json' --output 'Y:\ColumnOmniAuthor'
python -B tools/IidxSkinAssets/build_lighting_native_hud.py --source 'Y:\ColumnOmniAuthor' --output 'Y:\NextOmniAuthor'
```

`upgrade_lighting_components.py`同时调用`lighting_motion.retime_turntable_idle`，只将冻结转盘贡献的四条常转轨迹从4000ms改为1000ms一圈，其余几何、方向和设备counts/revolutions轨迹保持。此值是依据参照画面速度量级选择的作者近似，未证明原机固定周期，也不接BPM时钟；历史完整生成器的4000ms默认不追溯改写。冲突先完整校验再拒绝，已升级的输入可幂等复核。

原版新采样已直接证明默认j00的蓝/绿/粉三套标签和数字同步变化，但当前采图速度会影响观测驻留，尚未唯一校准源周期和命中原点。未标定的PG迁移原型只留本地诊断，不参与本入口或发布，不给所有21套套用猜定周期。

各款转盘只在加载期选择当前框体贡献，游玩不计算其它14框体的马达。设备相对counts与校准revolutions分开叠加；数字键/Auto/旧回放没有可证明的带符号运动时仅显示常转。新增纹理使用短内容标识、原PNG字节和原采样；入场声明仅缩短ID，保留全部时间与几何采样。46种转盘按所选清单共用15份相同几何，原图/滤波各自保留；相同INI按原字节去重。scene大小为数字归一化预留空间；整包配置仍守导入16MiB，C2捕获的64MiB结构预算不替代它。实际选中资源/像素/scene/manifest预算均需检查，普通包不声明这些能力时保持原流程。

```powershell
. .\UseDevelopmentStorage.ps1
$env:PYTHONPATH = (Resolve-Path '.dev-cache/tools/iidx-assets').Path
python -B -X utf8 tools/IidxSkinAssets/build_lighting_details.py --source 'Y:\ValidatedV14OmniAuthor' --library 'Y:\OriginalCustomLibrary' --lane-reference 'Y:\LaneOpeningResearch\reference.json' --ready-reference 'Y:\ReadyResearch\export.json' --output 'Y:\NextOmniAuthor'
```

`--lane-reference`必须完整覆盖15框体各自1P/2P/DP两侧的60条原记录；`lighting_lane_intro.py export`从独立来源库提取开局循环前、实际与轨道相交的原层次/UV/遮罩/混合，排除框外装饰，保留终止空帧。不得用33替代其它代；七支S5是否共用33及SUPERNOVA使用0目录高清来源须逐包证明。`--ready-reference`取完整父movie，裸字母movie缺少退场，不能替代。已有本地化三PNG和外框曲线直接继承v14，不重新读取安装目录。来源记录与生成命令可通过各工具`--help`查看；构建成功只证明引用闭包与离线预算，实际验证及边界取[P1-A状态](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)。

统一生成器为全部11类、1271款美术选项声明静态缩略图，共539张256×160 PNG、约12MiB编码字节。原六类132张保持；Frame取自身完整静态底图，普通挡板取原美术，动画挡板与25款FC取来源收据核对过的原CUSTOM选项图；Graph/LIFT与挡板的同源设计共用PNG，Graph原空白有明确图示。`choice_previews.py`只生成独立预览，不改游玩图片、动画或默认选值。首次生成动画挡板/FC缩略图时，冻结目录重打包须提供`--choice-preview-library 'Y:\OriginalCustomLibrary'`（含`original-previews.json`）；完整源构建默认用`--custom-library`。已捕获的规范PNG可直接随冻结包重建，无需再次读取源库。预览纳入ZIP引用闭包与独立32MiB预算；设置只解码可见内容、每页最多12款，翻页/收起释放旧图卡。作者检查与实际导入的证据、未完成范围取[P1-A状态](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md#最近一次验证)。此工具不把Omni写入玩家目录、canonical或公开制作套件。

2026-10-20按用户反馈，Omni仅提供主游玩画面：移除Lightning副屏、展开/专注/亮度三项配置、Premium背景/图标及专注背景。新导出与冻结模块/旧作者目录重打包都不再声明副屏，发布闭包排除其专用素材；原研究资料及旧版交付保留。Graph仍在主屏显示，不受此项移除影响，公共引擎及其它作者包的辅助屏能力保持。

歌曲进度使用原`playm_music_point.png`和15框体各自的1P/2P/DP几何，DP保留两枚游标，沿用原加色混合。`music_progress.py`只向选中Frame的信息模板添加游标与已有只读`timing.progress`绑定，未修改公共时钟、判定或其它皮肤。进度从首个可玩对象至最后对象结束（包含长条尾）计算，首前/末后夹至0/1，暂停和重试跟随Player时钟。原图22×30，SD框体按原画布投影，DP保留原横缩；左右对称的原像素共用一个PNG，不生成负缩放镜像。

`--music-progress-reference`消费独立来源核对的`oms-iidx-music-progress-reference.v1`文件及同目录原PNG，完整导出必须提供；旧作者目录可用`--repackage-directory 'Y:\PreviousOmniAuthor' --music-progress-reference 'Y:\MusicProgressReference\source-geometry.json' --output 'Y:\NextOmniAuthor'`接入。引用需完整覆盖15框体和三布局，保留每个源实例、端点依据和PNG摘要。已有原纵向端点的布局保留其几何；28～32及SUPERNOVA的DP缺少末端动画位置，以下方E标记顶边减游标高度对齐，属于作者适配。原几何不证明原游戏时间起止、插值或完整歌曲算法；这些仍待实机校准。重打包逐项限制场景/资源改动，其它素材、选项及INI保全；旧codec收据不得冒充改包后的检查。

整合输出只将引用闭包写入OSK，研究/来源收据留在作者目录，不能把generation或独立PNG可解码当作导入/游玩验收。内容目录能力、边界与可复跑CLI检查取[作者参考](../../skin-authoring/docs/REFERENCE.md#按选项组合内容)，本轮实际验证取[P1-A状态](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)。全部原版素材和作品仍仅留本地忽略目录或仓库外，不进入canonical/制作套件/公开发行。

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

原版素材、精确源清单与生成库只留本地忽略目录或仓库外，不加入 Git、内置静线或公开发行；独立作品按上方私下分享边界交付。工具本身不启动游戏、不安装注入器、不重打包 IFS、不操作 OMS 玩家数据。

## 旧分代生成器（27～33来源保留）

本节记录旧`build_skin.py`和已保留的分代候选，供统一包取材与复现历史使用；新交付从上方统一入口生成。

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

上述旧分代生成器仅消费素材子集：FC、爆炸和判定字只消费00设计，连击显示开关不等于判定字体选择，紧凑/缩放不等于爆炸设计选择。该轮来源审查见[P1-A完整性记录](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-18iidx-custom完整性核对)。统一入口已接入上方完整选项，不修改旧包内generation状态或原字节来刷新历史结论。

爆炸按BMS命中区与mania全列宿主分别定义作者模板，复用同一原始帧、时间和采样；mania通过包内静态坐标保持原帧宽高，不能改公共宿主来适配某套作品。固定转盘中心已归框体，过时的键区转盘模板不再生成。scene仍守512KiB上限，短效果ID用于节省重复引用；框体当前按向下落键设计，反向滚动尚未专门校准。

BPL使用同一生成器，通过`--frame-package`指定实际归档标识、`--identity`给稳定节点前缀、`--name`给本地显示名。研究输入必须逐队对应；同位置或同数字PNG不意味着可以混用战队框体。当前来源含S5七队及0目录SUPERNOVA高清框体；1目录同名旧SD研究另存，不能依据相似文件名或小PNG推定相同画布。若research有effective-bga-viewports.json，HD也使用逐框透明开口和原框下方黑底，避免裸movie矩形覆盖装饰；合成框体显式nearest。原动画仍冻结源181帧，未复刻完整原版界面。

## 通过隔离副本采集原版参照

原始可玩安装保持只读。运行参照使用另一处完整物理副本；启动前核对路径，独立保存配置、补丁选择、截图和日志，结束后复核源安装清单及关键文件摘要。副本清空卡号与自动插卡设置，不使用真实卡或账号；配置和 API 密码只存本地忽略目录，不进入 Git 或命令输出。

`reference_api.py` 只连接会话指定的 `127.0.0.1` Spice API，要求非空独立密码；提供截图、按钮、数字键盘导航、模拟轴、触控及投币，不提供卡、内存、进程或电源操作，也不负责复制或启动游戏。Spice API 服务自身的监听范围须在启动配置中另行核对，本客户端的 localhost 限制不能证明服务只监听回环。

会话 JSON 包含 `host/port/password/captureRoot`。截图仅允许在 `captureRoot` 内新建，不覆盖已有图片；持续导航可用 `with ReferenceApi(session_path) as api` 复用连接，避免每次输入都弹出连接通知。

截图的 `--quality` 范围为1～100，默认100；`--divide` 为本工具限制的1～8整数倍缩小，默认1。例如2使宽高各减半，减少编码、传输与本机保存成本，不改变游戏分辨率或刷新率。[官方capture实现](https://github.com/spice2x/spice2x.github.io/blob/c83496776e4dfa06f286e483d21fd269c331505c/src/spice2x/api/modules/capture.cpp)的第三参数是缩小倍数，不是帧率或等待时间。连续采样须排除旧图/旧时间戳；新时间戳也不是游戏时钟、Present序号或命中帧：该版本在RGB转换完成、入队时记录整数毫秒。D3D9读回占用Present线程，divide在完整读回后才缩小，减小输出图像不能证明捕获不影响原机节奏。来源和适用边界见[参照记录](../../doc_md/other/OMNI_OPENINGS_20261021.md#2026-10-24持续组件与原版参照复核)，没有宣称本地二进制与该源码提交完全匹配。

`keypad-write --keypad 0|1 --keys '2'`向P1/P2依次输入导航字符，仅接受非空的ASCII字符串`0-9/A/D`（A为00，D为小数键）；工具不开放数字键盘持续覆盖，不回显输入内容，不用于真实卡或PIN。[官方keypads实现](https://github.com/spice2x/spice2x.github.io/blob/main/src/spice2x/api/modules/keypads.cpp)每个字符先按下再松开，两段各约70ms；短导航按需发送，不用于逐帧高速输入。Python入口分别为`api.capture(path, screen=0, quality=100, divide=1)`及`api.keypad_write(0, "2")`。

```powershell
. .\UseDevelopmentStorage.ps1
python -B tools/IidxSkinAssets/reference_api.py --session 'Y:\IIDX-Reference\api-session.json' screens
python -B tools/IidxSkinAssets/reference_api.py --session 'Y:\IIDX-Reference\api-session.json' capture --screen 0 --output 'Y:\IIDX-Reference\captures\reference-001.jpg'
python -B tools/IidxSkinAssets/reference_api.py --session 'Y:\IIDX-Reference\api-session.json' capture --screen 0 --quality 70 --divide 2 --output 'Y:\IIDX-Reference\captures\reference-fast-001.jpg'
python -B tools/IidxSkinAssets/reference_api.py --session 'Y:\IIDX-Reference\api-session.json' keypad-write --keypad 0 --keys '2'
```

截图用于对位，连续帧辅助观察动画；普通运行日志不保证列出每次纹理绘制。素材关联仍需对照原 XML、AFP / GEO 和实际画面。副本为适配参考环境启用的启动检查调整必须单独记录，不作为原安装、设备或原版刷新率的验收结果。

需要在内置浏览器预览时，可从素材库启动只监听本机的 HTTP 服务；该浏览器不允许 `file://`，不调整其安全策略。预览服务只指向生成库，不指向 HDD 或整个工作区。

```powershell
python -B -m http.server 8768 --bind 127.0.0.1 --directory 'Y:\IIDX-Art-Library'
```

当前授权、作品范围与下一步以 [P1-A 计划](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md)和[约束](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)为准；解包成功不代表动画、布局或游玩效果已验收。

Omni光柱使用公开`playfield.lane-surface`，在原生Note/LN下方；Frame透明占位同步退出该槽。新导出及`--repackage-directory`均保留这种分工；光柱校正仅涉及已确认的模板/材料声明与Frame空占位，原图、录制动画和长度保持；副屏内容另按上方主屏范围剔除。光柱验证见[P1-A记录](../../doc_md/subline/P1-A/CHANGELOG.md#2026-10-19omni光柱层级修正)，通用引擎图层不变。
