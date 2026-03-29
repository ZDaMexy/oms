# OMS 开发规划

> 本文档是 OMS 项目的详细开发规划，将 `OMS_COPILOT.md` 中的三个阶段拆解为可执行的开发步骤。  
> 每个步骤标注前置依赖、产出文件和验收标准。

---

## 总览

| 阶段 | 目标 | 步骤数 |
|---|---|---|
| **Phase 1** | 核心 BMS — 能导入并游玩 7K+1 BMS 谱面 | 17 步 |
| **Phase 2** | BMS 功能完善 — 全键模式、全 Mod、全 Gauge | 12 步 |
| **Phase 3** | 私服集成 — 在线账号、排行榜、谱面下载 | 6 步 |

---

## Phase 1 — 核心 BMS

### 1.1 上游清理与项目脚手架

**目标：** 移除三个无用规则集，确认项目可编译运行。

**步骤：**

1. 删除 `osu.Game.Rulesets.Osu`、`osu.Game.Rulesets.Taiko`、`osu.Game.Rulesets.Catch` 项目及所有引用
2. 在解决方案中清理对应的 `.csproj` 引用和反射注册代码
3. 移除 osu.Game 中对已删除模式的硬编码引用（测试场景、默认规则集列表等）
4. 创建 `osu.Game.Rulesets.Bms` 项目骨架（.csproj + BmsRuleset.cs 入口）
5. 创建 `oms.Input` 项目骨架（.csproj + OmsAction 枚举 + OmsInputRouter 空壳）
6. 创建 `oms.Desktop` 入口项目（如尚不存在）
7. 添加 NuGet 依赖：`SharpCompress`、`Ude.NetStandard`、`HidSharp`

**前置依赖：** 无  
**验收：** `dotnet build` 通过，启动后进入 osu!mania Song Select（无 BMS 内容）。

---

### 1.2 BMS 数据模型

**目标：** 定义 BMS 规则集的核心数据结构。

**产出文件：**
- `BmsBeatmapInfo.cs` — Keymode、MeasureLengthControlPoints、#TOTAL、#RANK 等 BMS 专属元数据
- `BmsHitObject.cs` — 单键音符（LaneIndex, KeysoundId, IsScratch, AutoPlay）
- `BmsHoldNote.cs` — 长条音符（StartTime, Duration, HeadKeysoundId, TailKeysoundId）
- `BmsBgmEvent.cs` — 非可判定 BGM 事件
- `BmsKeymode.cs` — 枚举：Key5K, Key7K, Key9K_Bms, Key9K_Pms, Key14K
- `BmsMeasureLengthControlPoint.cs` — record(int MeasureIndex, double Multiplier)

**前置依赖：** 1.1  
**验收：** 所有数据模型编译通过，无运行时使用。

---

### 1.3 BMS 文件解析器 (`BmsBeatmapDecoder`)

**目标：** 将 `.bms`/`.bme`/`.bml`/`.pms` 解析为内存中间表示。

**实现要点：**
1. 编码检测（Ude）→ Shift-JIS / UTF-8 自动切换
2. 头字段解析（#TITLE, #SUBTITLE, #ARTIST, #GENRE, #BPM, #PLAYLEVEL, #DIFFICULTY, #RANK, #TOTAL, #STAGEFILE, #BANNER, #BACKBMP）
3. 索引表解析（#WAV##, #BMP##, #BPM##, #STOP##）
4. LN 声明解析（#LNOBJ, #LNTYPE）
5. #RANDOM / #IF / #ENDIF：解析所有分支，执行 `#IF 1` 块，记录警告
6. 通道解析（#MMMCC:data）— base-36 对象切分
7. Channel 02 特殊处理（十进制浮点数，非 base-36）
8. Channel 03/08 BPM 变化解析
9. Channel 09 STOP 解析
10. Channel 01 BGM、11–19/21–29 可打击、51–59/61–69 LN 通道
11. LNOBJ 逆向绑定（尾判标记前一个音符为 LN 头）
12. LNTYPE 1 通道对解析（5x/6x 头尾配对）
13. 键模式自动检测（6 条有序规则）
14. 输出：填充 BmsBeatmapInfo + 原始通道事件列表

**前置依赖：** 1.2  
**验收：** 单元测试覆盖——至少包含以下测试用例：
- 基础 7K 谱面完整解析
- Shift-JIS 编码文件正确读取
- Channel 02 浮点值 0.75 解析
- #LNOBJ 长条配对
- #LNTYPE 1 长条配对
- BPM 变速 (Channel 03 + 08)
- STOP (Channel 09)
- 键模式检测（5K、7K、14K、PMS 分别验证）
- #RANDOM 块跳过并输出警告

---

### 1.4 谱面转换器 (`BmsBeatmapConverter`)

**目标：** 将解析后的 BMS 原始数据转换为 osu-framework 可执行的 `IBeatmap`。

**实现要点：**
1. 绝对时间计算引擎——按小节累加 `measure_start_ms`，处理 Channel 02 倍率和 BPM 变化
2. ControlPointInfo 填充——Initial BPM → Channel 03/08 BPM → Channel 09 STOP（合成极大 BPM 冻结 + 恢复）
3. MeasureLengthControlPoints 填充
4. HitObject 生成——单键 → BmsHitObject，LN → BmsHoldNote，BGM → BmsBgmEvent
5. 排序保证——HitObjects 按 StartTime 升序，ControlPointInfo 按时间升序无重叠
6. 输出契约校验

**前置依赖：** 1.3  
**验收：** 单元测试——验证已知 BMS 文件转换后的 HitObject 时间精度（±1ms 容差）、ControlPoint 数量、STOP 时间窗口正确性。

---

### 1.5 归档导入 (`BmsArchiveReader`)

**目标：** 支持 .zip/.rar/.7z 归档拖放导入，在 OMS songs 目录下直接保留 BMS 文件结构。

**实现要点：**
1. SharpCompress 解压到临时目录
2. 扫描 .bms/.bme/.bml/.pms 文件
3. 每个文件调用 BmsBeatmapDecoder，收集成功/失败
4. 同文件夹文件组为一个 BeatmapSet（不按键模式拆分）
5. 计算每个文件 MD5 哈希，写入 BeatmapInfo.Hash
6. 解析失败处理（部分成功警告/全部失败错误通知）
7. 移动到 songs 目录，清理临时文件
8. 注册到 osu! BeatmapManager

**前置依赖：** 1.3, 1.4  
**验收：** 手动测试——拖放一个 7K BMS zip 包，Song Select 中出现对应谱面集，每个难度显示键模式标签。

---

### 1.6 键音系统 (`BmsKeysoundStore`)

**目标：** 按 base-36 索引管理每张谱面最多 1295 个音频采样，支持并发播放。

**实现要点：**
1. #WAV## 索引构建（base-36 → 音频文件路径）
2. 格式降级查找（精确文件名 → .wav → .ogg → .mp3，替代时记录警告）
3. ManagedBass 懒加载 + 会话内缓存
4. 并发通道上限由 `BmsRulesetConfigManager.KeysoundConcurrentChannels` 控制
5. 缺失键音：记录警告，播放静音
6. BGM 通道（Channel 01）事件队列：按时间自动触发
7. 可打击音符键音：由 gameplay 层在判定时调用触发

**前置依赖：** 1.2, 1.5（需要导入后的文件结构）  
**验收：** 导入一个有键音的 BMS 包 → 在测试场景播放 BGM 通道 → 听到正确音频序列。缺失文件不崩溃。

---

### 1.7 BMS 规则集入口 (`BmsRuleset`)

**目标：** 将 BMS 注册为 osu-framework 可发现的规则集，串联解析→转换→gameplay 管线。

**实现要点：**
1. BmsRuleset 继承 Ruleset，注册 RulesetInfo
2. CreateBeatmapConverter → BmsBeatmapConverter
3. CreateDifficultyCalculator → BmsDifficultyCalculator（先返回 stub 值，1.12 完善）
4. GetModsFor → 返回 Phase 1 的 Mod 列表（Lane Cover Top/Bottom）
5. BmsRulesetConfigManager 初始化

**前置依赖：** 1.4  
**验收：** 启动 OMS → 模式选择器中出现 BMS 模式图标 → 可切换到 BMS。

---

### 1.8 7K+1 Playfield (`BmsPlayfield` + `BmsLaneLayout`)

**目标：** 渲染 7K+1 (1P) 布局的 BMS 游玩界面。

**实现要点：**
1. BmsLaneLayout — 8 车道（1 scratch + 7 key），宽度/颜色/位置定义
2. BmsPlayfield — 继承 ScrollingPlayfield，注册车道
3. BmsScratchLane — scratch 车道渲染（加宽）
4. 小节线渲染——读取 MeasureLengthControlPoints 计算位置
5. BmsBackgroundLayer — 预留渲染槽 + 静态 #STAGEFILE 显示
6. 音符 Drawable 绑定——BmsHitObject/BmsHoldNote → 车道内可视元素
7. 滚动速度——继承 mania 的 scroll speed 系统

**前置依赖：** 1.7, 1.6  
**验收：** 选择已导入的 7K BMS 谱面 → 进入 gameplay → 音符从上/下滚动，scratch 车道在最左（1P），BGM 键音自动播放。

---

### 1.9 OD 判定系统 (`OsuOdJudgementSystem`)

**目标：** 实现默认判定——基于 #RANK → OD 的 osu!mania 时间窗口。

**实现要点：**
1. BmsJudgementSystem 抽象基类（Evaluate + Windows）
2. OsuOdJudgementSystem 具体实现——#RANK → OD 映射 → 时间窗口
3. BmsTimingWindows — 存储 PGREAT/GREAT/GOOD/BAD/POOR 窗口值
4. Gameplay 集成——HitObject 判定回调接入 BmsJudgementSystem.Evaluate
5. 键音触发规则接入——PGREAT/GREAT/GOOD/BAD 播放键音，POOR 不播放

**前置依赖：** 1.8  
**验收：** 单元测试——#RANK 2 时各窗口值与 osu!mania OD 7 一致。Gameplay 中按键得到判定文字反馈。

---

### 1.10 Normal Gauge (`BmsGaugeProcessor`)

**目标：** 实现 NORMAL Gauge（默认），包含 #TOTAL 驱动的回复/伤害率。

**实现要点：**
1. BmsGaugeProcessor 基类架构（当前值、base_rate 计算、Apply 方法）
2. Normal Gauge 具体参数——起始 20%，回复 base_rate×0.8，伤害 base_rate×8.0
3. 全判定回复/伤害倍率实现（PGREAT 1.0×回复、GREAT 1.0×、GOOD 0.5× 回复 + 0 伤害、BAD 1.0×伤害、POOR 1.5×、Empty POOR 1.0×）
4. 2% 生存底线
5. 结算条件——≥80% 为 NORMAL CLEAR
6. Gauge 条 UI 渲染

**前置依赖：** 1.9  
**验收：** 单元测试——给定 #TOTAL=200、1000 音符，验证 base_rate 和各判定的精确 gauge 变化。Gameplay 中 gauge 条实时变化。

---

### 1.11 EX-SCORE 与结算 (`BmsScoreProcessor` + `BmsClearLampProcessor` + `BmsDjLevelCalculator`)

**目标：** 完成 BMS 计分管线和结算画面。

**实现要点：**
1. BmsScoreProcessor — PGREAT×2 + GREAT×1 EX-SCORE；追踪各判定计数和 MAX COMBO
2. Combo 规则——PGREAT/GREAT/GOOD 续连，BAD/POOR/Empty POOR 断连
3. BmsClearLampProcessor — 灯级层次（NO PLAY → ... → PERFECT），仅升不降
4. BmsDjLevelCalculator — EX% → AAA/AA/.../F
5. 结算画面集成——显示 EX-SCORE、判定分布、Clear Lamp、DJ Level、gauge 图表

**前置依赖：** 1.10  
**验收：** 打完一首谱面 → 结算画面正确显示所有数据。单元测试覆盖 DJ Level 边界值和 Clear Lamp 升级逻辑。

---

### 1.12 密度星级 (`BmsNoteDensityAnalyzer` + `BmsDifficultyCalculator`)

**目标：** 基于加权音符密度计算 0–20 星级。

**实现要点：**
1. BmsNoteDensityAnalyzer — 滑动窗口分析（1000ms 窗口，500ms 步进）
2. 权重规则——基础 1.0，和弦 +0.3/额外音符（≤1ms 容差），scratch +0.5，LN +0.1/100ms
3. GetPercentileDensity — 95th 百分位
4. BmsDifficultyCalculator — 调用 Analyzer，7K 标准化常数，映射到 0–20 星
5. BmsDifficultyAttributes — StarRating, TotalNoteCount, ScratchNoteCount, LnNoteCount, PeakDensityNps, PeakDensityMs
6. 替换 1.7 中的 stub 返回值

**前置依赖：** 1.4  
**验收：** 单元测试——已知音符分布的合成谱面，验证 bucket 计数、百分位值和最终星级在预期范围内。

---

### 1.13 难度表预置与订阅 (`BmsDifficultyTableManager`)

**目标：** 实现社区难度表的订阅管理和数据拉取。

**实现要点：**
1. `bms_table_presets.json` 资源文件——Satellite、Stella 等预置 URL
2. SQLite 订阅表——URL, display_name, is_preset, enabled, last_fetched
3. BMSTable 两步拉取——HTML → header.json → body.json
4. 直接 JSON URL 的降级处理
5. RefreshAllTables / RefreshTable 异步方法
6. TableDataChanged 事件
7. 订阅管理 UI（启用/禁用切换、手动刷新、添加自定义 URL）

**前置依赖：** 1.1  
**验收：** 启用 Satellite 预置 → 拉取成功 → 本地缓存写入 → 重启后无需网络即可读取缓存。

---

### 1.14 MD5 匹配管线 (`BmsTableMd5Index`)

**目标：** 导入时自动匹配难度表等级，表刷新时批量更新。

**实现要点：**
1. 导入时——BmsArchiveReader 计算 MD5 → 查询内存索引 → 写入 BmsTableLevel
2. 表刷新时——批量 SQL IN 查询 → 更新 DB → 重建内存索引 → 发出事件
3. 内存结构 `Dictionary<string, List<BmsDifficultyTableEntry>>`
4. 启动时从 DB 缓存重建（不需要网络）

**前置依赖：** 1.5, 1.13  
**验收：** 导入一个在 Satellite 表中存在的 BMS 包 → 谱面元数据自动标记表等级。手动刷新表 → 新匹配自动生效。

---

### 1.15 Song Select 表分组 (`BmsTableGroupMode`)

**目标：** BMS 模式下 Song Select 支持按难度表→等级的层级分组。

**实现要点：**
1. BmsTableGroupMode 实现 GroupDefinition
2. 分组层级：表名 → 等级 → BeatmapSet → 难度
3. "Unrated" 分组放在最后
4. 多表谱面在每个表的分组下独立出现
5. 分组激活时禁用排序下拉，内部固定按密度星级升序
6. 注册到 BMS 规则集的 Song Select 配置

**前置依赖：** 1.14, 1.12  
**验收：** BMS 模式 Song Select 中选择表分组 → 看到 Satellite/Stella 等分组 → 展开看到 ★1/★2 等级 → 内部谱面按星级排列。

---

### 1.16 音符分布图 (`BmsNoteDistributionGraph`)

**目标：** Song Select 右侧面板显示谱面音符密度预览。

**实现要点：**
1. 调用 BmsNoteDensityAnalyzer（windowMs=1000, stepMs=1000）
2. 读取 BmsDifficultyAttributes 中的统计数据
3. 白色（普通）/ 红色（scratch）/ 蓝色（LN）堆叠柱状图
4. 统计文字：总音符、scratch 占比、LN 占比、峰值密度
5. 后台任务计算 → Schedule() 推送到 UI 线程
6. BmsNoteDistribution SkinComponentLookup 注册
7. 选中谱面缓存，切换清除

**前置依赖：** 1.12, 1.8  
**验收：** BMS Song Select 选中谱面 → 右侧出现分布图，数据与实际音符一致。

---

### 1.17 基础输入绑定与 Lane Cover

**目标：** 键盘和 HID 输入绑定可用，Lane Cover Mod 可用。

**实现要点：**

*输入：*
1. OmsAction 枚举完整定义（1P/2P 7K+1, 9K, UI 动作）
2. OmsBindingStore — 键盘按键 → OmsAction 的持久化映射
3. RawKeyboardHandler — Windows Raw Input 键盘捕获
4. HidDeviceHandler — HidSharp 设备枚举、按钮读取、映射
5. OmsInputRouter — 路由信号到 gameplay
6. 绑定 UI — 按键监听录入、信号类型图标显示、7K 绑定档案

*Lane Cover：*
7. BmsLaneCover — 不透明遮挡层 Drawable
8. BmsModLaneCoverTop — 顶部遮挡，CoverPercent (0–100%)
9. BmsModLaneCoverBottom — 底部遮挡，CoverPercent (0–100%)
10. 游戏内滚轮调节（默认调 Top；按住 UI_LaneCoverFocus 调 Bottom）

**前置依赖：** 1.8  
**验收：** 键盘绑定后能正常打 7K BMS 谱面。HID 控制器接入后按钮动作正确。Lane Cover 显示并可滚轮调节。

---

### Phase 1 完成里程碑

**达成条件：**
- [x] 可导入 .zip/.rar/.7z BMS 归档
- [x] 7K+1 谱面可完整游玩（音符、键音、判定、gauge、计分）
- [x] 结算画面显示 EX-SCORE、Clear Lamp、DJ Level
- [x] 难度表订阅/MD5 匹配/表分组可用
- [x] 音符分布图在 Song Select 显示
- [x] 键盘 + HID 基础输入可用
- [x] Lane Cover 可用

---

## Phase 2 — BMS 功能完善

### 2.1 beatoraja + LR2 判定 Mod

**实现：** `BeatorajaJudgementSystem` + `Lr2JudgementSystem`；`BmsModJudgeBeatoraja` + `BmsModJudgeLr2` Mod 类。beatoraja 按 #RANK 缩放窗口，LR2 固定窗口。

**前置依赖：** Phase 1 完成  
**验收：** 单元测试覆盖全部判定系统的窗口值。切换 Mod 后 gameplay 使用对应窗口。

---

### 2.2 全 Gauge 类型

**实现：**
- BmsModGaugeAssistEasy / BmsModGaugeEasy（非生存型，20% 起始，2% 底线）
- BmsModGaugeHard / BmsModGaugeExHard（生存型，100% 起始，0% 失败）
- BmsModGaugeHazard（100% 起始，BAD/POOR 瞬间归零，GOOD 不触发失败）
- 各 Gauge Mod 互斥

**前置依赖：** Phase 1 完成  
**验收：** 单元测试——每种 gauge 的回复/伤害/结算/失败条件全覆盖。HAZARD gauge 下 GOOD 不扣血。

---

### 2.3 GAS (Gauge Auto Shift)

**实现：** `BmsModGaugeAutoShift`（StartingGauge / FloorGauge Bindable），降级链 HAZARD→EX-HARD→HARD→NORMAL→EASY→ASSIST EASY，非生存 gauge 不再降级，结算取最佳灯。

**前置依赖：** 2.2  
**验收：** 测试场景——从 EX-HARD 开始，触发降级到 NORMAL，通过结算 → 最佳灯为 NORMAL CLEAR。结算画面显示各层 gauge 图表。

---

### 2.4 A-SCR (Auto Scratch)

**实现：** `BmsModAutoScratch`——scratch 音符标记为 AutoPlay，排除出计分/gauge/combo/MaxExScore 池，键音照常播放。`AscScratchVisibility` 设置。14K DP 双侧 scratch 同时处理。

**前置依赖：** Phase 1 完成  
**验收：** A-SCR 激活后 scratch 自动触发键音，不计入 EX-SCORE。MaxExScore 正确减少。可见性设置切换有效。

---

### 2.5 Empty Poor 判定

**实现：** `BmsPoorJudgement`——每车道维护活跃音符窗口状态，按键时无可打击音符 → 触发 Empty Poor → gauge 伤害 = BAD 1.0×、断 combo、不影响 EX-SCORE。

**前置依赖：** Phase 1 完成  
**验收：** 空打确实触发 gauge 伤害和 combo 断裂；结算画面 Empty Poor 计数正确。

---

### 2.6 5K / 9K / 14K DP 布局

**实现：**
- BmsLaneLayout 扩展——5K(5+1) / 9K(BMS) / 9K(PMS Pop'n 排列) / 14K(双侧 7+1)
- 14K DP 单一绑定档案（1P + 2P 统一界面）
- 密度星级校准常数——每种键模式独立的标准化参数

**前置依赖：** Phase 1 完成  
**验收：** 分别导入 5K / 9K / 14K 谱面 → 布局正确渲染，绑定功能正常，星级值合理。

---

### 2.7 1P/2P 翻转 Mod

**实现：** `BmsModMirror1P2P`——水平镜像车道数组，scratch 左右互换，绑定跟随翻转，皮肤元素响应 CurrentSide bindable。

**前置依赖：** 2.6  
**验收：** 7K 1P 侧翻转为 2P → scratch 从左移到右。14K DP 下无效果或双侧对调。

---

### 2.8 模拟轴输入

**实现：**
- HidDeviceHandler 扩展——旋转编码器轴值读取，delta 转换为 scratch 方向 + 速度
- MouseAxisHandler——鼠标原始 delta 转 scratch 输入，灵敏度设置
- AxisInverted 标志——每绑定独立极性翻转
- scratch 车道同时接受数字键 + 模拟轴 + 鼠标三种信号

**前置依赖：** 1.17  
**验收：** HID 旋转编码器产生 scratch 输入；鼠标横向移动触发 scratch；反转标志生效。

---

### 2.9 LNTYPE 2 (MGQ) 长条

**实现：** BmsBeatmapDecoder 中追加 LNTYPE 2 解析逻辑。MGQ 格式的 LN 头/尾配对规则。

**前置依赖：** 1.3  
**验收：** LNTYPE 2 格式的测试 BMS 文件正确解析为 BmsHoldNote。

---

### 2.10 BGA 视频播放

**实现：**
- #BMP## 索引解析（BmsBeatmapDecoder 中已预留）
- Channel 04/06/07 BGA 事件时间轴
- BmsBackgroundLayer 扩展——ffmpeg.autogen 解码视频帧，纹理更新
- POOR 层（Channel 06）在 POOR 判定时显示

**前置依赖：** 1.8（BmsBackgroundLayer 已预留槽位）  
**验收：** 含 BGA 视频的 BMS 谱面播放时正确显示视频，POOR 层在误判时切换。

---

### 2.11 完整皮肤覆盖

**实现：** 全部 SkinComponentLookup 注册——BmsPlayfield 背景、BmsLane、BmsScratchLane、BmsLaneCover、BmsJudgementText、BmsComboCounter、BmsGaugeBar、BmsClearLamp、BmsNoteDistribution。全部有内建默认回退。

**前置依赖：** Phase 1 完成  
**验收：** 无自定义皮肤时全部元素正常渲染。加载自定义皮肤后对应元素替换。

---

### 2.12 BmsRulesetConfigManager 设置画面

**实现：** BMS 模式设置画面集成所有持久化设置——AutoScratchNoteVisibility、KeysoundConcurrentChannels、LeaderboardGaugeFilter、LeaderboardAscrFilter、LeaderboardJudgeFilter。

**前置依赖：** 2.4  
**验收：** 设置画面各项可修改 → 重启后保持。

---

### Phase 2 完成里程碑

**达成条件：**
- [x] 三套判定系统全部可用
- [x] 六种 Gauge + GAS + A-SCR 全部可用
- [x] 5K / 9K / 14K DP 布局完整
- [x] 1P/2P 翻转、Empty Poor 功能正常
- [x] HID 旋转编码器和鼠标 scratch 输入可用
- [x] BGA 视频播放（含 POOR 层）
- [x] 皮肤系统全元素覆盖

---

## Phase 3 — 私服集成

### 3.1 API 客户端基础 (`OmsApiClient`)

**实现：** HttpClient 封装、可配置 Base URL、Bearer Token 存储（Windows 凭据管理器）、Refresh Token 流程、请求/响应序列化、网络错误处理、离线回退逻辑。

**前置依赖：** Phase 2 完成  
**验收：** 可连接测试服务器完成登录/刷新流程。网络断开时 OMS 自动进入离线模式无崩溃。

---

### 3.2 账号认证

**实现：**
- AuthEndpoint — POST /auth/login, POST /auth/refresh
- 登录 UI（用户名/密码输入、记住登录状态）
- GET /user/me → OmsUser 模型，主界面显示用户信息
- 退出登录 / 切换账号

**前置依赖：** 3.1  
**验收：** 完整登录→显示用户名→退出→重新登录流程。

---

### 3.3 成绩提交

**实现：**
- POST /scores/submit — OmsScore 完整载荷（含 Mod 标签、gauge_mode、judge_mode、EmptyPoorCount 等）
- 结算画面增加上传按钮/自动上传
- 离线成绩本地暂存 → 联网后批量补传
- 客户端 replay hash（防篡改基础措施）

**前置依赖：** 3.2  
**验收：** 打完一首谱面 → 成绩上传 → 服务端可查。离线打完 → 联网后自动补传。

---

### 3.4 在线排行榜

**实现：**
- LeaderboardEndpoint — GET /scores/chart/{hash}（含 gauge/ascr/judge 筛选参数）
- Song Select 排行榜面板——显示 Top N 成绩
- 筛选 UI（gauge / A-SCR / judge 独立下拉）
- 筛选状态持久化到 BmsRulesetConfigManager

**前置依赖：** 3.3  
**验收：** Song Select 选中谱面 → 排行榜面板加载成绩列表 → 筛选条件生效 → 切换谱面后筛选保持。

---

### 3.5 谱面搜索与下载

**实现：**
- BeatmapDownloadEndpoint — GET /beatmaps/search, GET /beatmaps/{id}/download
- 搜索 UI（关键词、分页）
- 下载进度指示 → 自动调用 BmsArchiveReader 导入
- 已拥有谱面标记

**前置依赖：** 3.1, 1.5  
**验收：** 搜索 → 选择 → 下载 → 自动导入 → Song Select 中出现。

---

### 3.6 服务端难度表镜像

**实现：**
- GET /difficulty-tables, GET /difficulty-tables/{id}
- BmsDifficultyTableManager 增加服务端源——拉取镜像表数据作为社区表的补充/替代
- 当社区 URL 不可达时自动回退到服务端镜像

**前置依赖：** 3.1, 1.13  
**验收：** 社区表 URL 模拟不可达 → 自动从服务端镜像拉取 → 数据一致。

---

### Phase 3 完成里程碑

**达成条件：**
- [x] 账号登录/登出/刷新完整可用
- [x] 成绩自动上传（包含所有 Mod 标签）
- [x] 在线排行榜（带复合筛选）
- [x] 谱面搜索与下载
- [x] 难度表服务端镜像
- [x] 离线模式优雅降级

---

## 步骤依赖关系图

```
Phase 1:
  1.1 ──┬── 1.2 ── 1.3 ── 1.4 ──┬── 1.5 ──┬── 1.6
        │                        │          │
        │                        │          └── 1.14 ── 1.15
        │                        │
        │                        ├── 1.7 ── 1.8 ──┬── 1.9 ── 1.10 ── 1.11
        │                        │                 │
        │                        │                 ├── 1.16
        │                        │                 │
        │                        │                 └── 1.17
        │                        │
        │                        └── 1.12
        │
        └── 1.13

Phase 2 (全部依赖 Phase 1 完成):
  2.1
  2.2 ── 2.3
  2.4
  2.5
  2.6 ── 2.7
  2.8
  2.9
  2.10
  2.11
  2.12

Phase 3 (全部依赖 Phase 2 完成):
  3.1 ──┬── 3.2 ── 3.3 ── 3.4
        ├── 3.5
        └── 3.6
```

---

## 单元测试优先级

以下组件**必须**在实现同步编写测试，不可推迟：

| 优先级 | 组件 | 测试重点 |
|---|---|---|
| **P0** | BmsBeatmapDecoder | 头字段、通道解析、LN 配对、键模式检测、编码检测、#RANDOM 跳过 |
| **P0** | BmsBeatmapConverter | 绝对时间精度、ControlPoint 生成、STOP 时间窗口、MeasureLengthControlPoints |
| **P0** | BmsTimingWindows | 三套判定系统的全部窗口值 |
| **P0** | BmsScoreProcessor | EX-SCORE 计算、Combo 规则、各判定计数 |
| **P0** | BmsGaugeProcessor | 全部 gauge 类型的回复/伤害/底线/结算/失败 |
| **P1** | BmsDifficultyCalculator | 合成谱面的星级计算、百分位值 |
| **P1** | BmsNoteDensityAnalyzer | bucket 计数、加权规则、边界条件 |
| **P1** | BmsTableMd5Index | 导入匹配、表刷新批量更新、内存索引重建 |
| **P1** | BmsDifficultyTableManager | 两步拉取、缓存持久化、禁用表排除 |
| **P2** | BmsClearLampProcessor | 灯级升级逻辑、A-SCR 下的灯判定 |
| **P2** | BmsDjLevelCalculator | EX% 边界值（8/9、7/9 等分数精度） |
