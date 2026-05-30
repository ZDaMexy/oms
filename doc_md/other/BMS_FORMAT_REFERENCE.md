# BMS 格式权威参考

> 最后更新：2026-05-29
> 本文档把 BMS / bmson 生态的外部规范收敛成 OMS 解析链路可直接对照的事实基线，主要服务 [P1-K（BMS 解析链路治理）](../subline/P1-K/) 的审查与回归立项。
> 本文档是参考材料，不替代主线计划/约束，也不替代 [P1-K 四件套](../subline/P1-K/)。当实现与本文冲突时，按 [文档联动规则](../../CLAUDE.md) 先修正其一再继续开发。
> 本文件只做结构化归纳与交叉差异沉淀，不复制站点原文；遇到边界情况请回到「参考来源」逐条核对。

## 文档定位

- **目的**：把 BMS 格式中分散在多份外部资料里的稳定结论、跨实现差异与经典解析陷阱收敛成一份可执行对照表，避免每次审查解析器都重新调研。
- **使用方式**：审查 `BmsBeatmapDecoder` / `BmsDecodedChart` / `BmsBeatmapInfo` / `BmsBeatmapConverter` 时，先用本文校验"某 channel / header 的语义和编码基数是否被正确处理"，再决定是否回写 P1-K 约束或补回归。
- **边界**：本文聚焦"解析器必须知道的格式事实"。判定窗口、gauge、训练反馈等玩法语义归 [P1-C](../subline/P1-C/) 与 [IIDX_REFERENCE_AUDIT.md](IIDX_REFERENCE_AUDIT.md)，本文只在涉及解析字段（如 `#RANK` / `#TOTAL` / `#LNMODE`）时点到为止。

## 参考来源与权威分级

| 优先级 | 来源 | 定位 | 权威性 |
| --- | --- | --- | --- |
| 1（首选事实标准） | [hitkey BMS command memo](https://hitkey.bms.ms/cmds.htm) | 社区公认最全，逐条记录 LR2 / nanasi / ruvit / pomu2 / beatoraja / bemaniaDX 在边界情况上的差异 | 写解析器的首选参考；2011-09-21 定稿、2011-10-16 HTML 化，**早于** beatoraja 的 `#SCROLL/#SPEED` 扩展 |
| 1（镜像） | [SaxxonPike/bms-command-memo](https://github.com/SaxxonPike/bms-command-memo)（GitHub 镜像） | 上面 memo 的稳定镜像 | 内容同 hitkey，原站抽风时用 |
| 2（可执行回归基线） | [bemusic/bmspec](https://github.com/bemusic/bmspec) | 用 Gherkin/Cucumber 写的可执行规范，本身就是行为测试用例 | 自述"**不是**官方规范"，但可直接当解析器回归基准 |
| 3（历史起点） | [BM98 BMS format（Urao Yane, 1998）](http://bm98.yaneu.com/bm98/bmsformat.html) | 格式起点，只覆盖最基础 header + channel | 仅作历史参考；**不含** `#BPMxx` / `#STOP` / 长条 / `#SCROLL` |
| 旁系（非经典 BMS） | [bmson specification](https://bmson-spec.readthedocs.io/en/master/doc/) | JSON 化格式（Bemuse 用），同生态但模型不同 | OMS 当前不读 bmson；仅作设计对照，见末节 |

> **URL 迁移提醒**：hitkey 原 `hitkey.nekokan.dyndns.info` 域名已迁移/失效，权威地址现为 `https://hitkey.bms.ms/cmds.htm`（同源镜像 `https://bms.ms/~hitkey/`）。仓库内旧链接已统一指向新域名。

---

## 1. 文件与行结构

- BMS 是纯文本格式，逐行解释；**以 `#` 开头的行是命令行**，其余行一律视为注释/忽略（可放说明文字）。
- 命令行分两类：
  - **header 句**：`#COMMAND value`（如 `#TITLE foo`、`#WAV01 a.wav`）。
  - **channel 句（object 行）**：`#xxxCC:data`（measure + channel + 数据流）。
- 命令名**大小写不敏感**。
- BMS "运行时编译"：**行序自由**，header 可出现在任意位置，不必在 channel 句之前。
- 文本编码：事实默认 **Shift_JIS**（日系谱面）；`#CHARSET` 可声明 UTF-8 等；跨所有实现唯一安全的是 ASCII。文件名/目录名含多字节字符会让部分实现异常。
- 可能混用 CRLF / CR / LF 行尾，末行可能无换行。

### channel 句（object 行）格式

```
#xxxCC:data
 │  │   └── 数据流：见下方"编码基数"
 │  └────── CC = 两字符 channel token（如 11、08、SC）
 └───────── xxx = 小节号 [000-999]，需补前导零
```

- 对**对象类 channel**：`data` 是连续的 **两字符一组的对象**拼接；**组数 = `len(data)/2` = 该小节被等分的份数**；`00` 表示该位置为空（rest）。
  - 例：`#00111:00110022` → 4 组（`00 11 00 22`）→ 小节四等分，对象 `11` 落在 1/4 拍位、`22` 落在 3/4 拍位，其余为空。
- 小节号范围一般 `[000-999]`；个别实现更窄（BMSV `[000-511]`、bemaniaDX `[000-399]`）。

### 对象编码基数（base36 vs base16）

- **现代实现：base36** `[0-9A-Z]`（大小写不敏感）→ `00`–`ZZ` 共 **1296** 个槽位。
- **早期/遗留：base16** `[0-9A-F]` → `00`–`FF` 共 **256** 个槽位。
- `00` 恒为"空/rest"。
- ⚠️ **并非所有 channel 的数据都是 base36 对象**——见下一节的"编码陷阱"。

---

## 2. Channel 表（解析审查核心）

> **这是解析器最易翻车的地方**：channel `02` 是浮点、channel `03` 是十六进制字面量，二者都**不是** base36 对象对；channel `08`/`09`/`SC` 是 base36 **索引**（指向 header 定义），而非直接值。

### 2.1 经典编码陷阱（数据不是 base36 对象对）

| Channel | 含义 | 数据编码 | 陷阱说明 |
| --- | --- | --- | --- |
| `02` | 小节长度 / 拍号 | **十进制浮点**，冒号后直接写一个数 | **不是**对象对。`#00102:0.75` = 该小节 3/4 拍；`1.0`=4/4（隐式默认）；仅作用于该小节 |
| `03` | BPM 变化 | **两位十六进制字面量** `01`–`FF`（=1–255） | **不是**索引、**不是** base36。`00`=空。值就是 BPM 本身，上限 255 |
| `08` | 扩展 BPM 变化 | **base36 索引** → `#BPMxx` | 真正的 BPM 在 `#BPMxx` header 里，可 >255 / 小数 / 负数 |
| `09` | STOP（冻结滚动） | **base36 索引** → `#STOPxx` | 停顿时长在 `#STOPxx` 里，单位见 §4.2 |
| `SC` | 滚动速度（SCROLL） | **base36 索引** → `#SCROLLxx` | beatoraja/bemuse 扩展；hitkey 2011 版未收录 |
| `SP` | 音符间距（SPEED） | **base36 索引** → `#SPEEDxx` | beatoraja/bemuse 扩展 |

### 2.2 音频与可见/不可见/长条/地雷音符

| Channel | 含义 | 数据编码 |
| --- | --- | --- |
| `01` | BGM（自动播放 keysound） | base36 → `#WAVxx`；**同小节多行不合并**，各为一条并行 BGM 轨 |
| `11`–`19` | 1P 可见音符 | base36 → `#WAVxx`（键位映射见 §3） |
| `21`–`29` | 2P 可见音符 | base36 → `#WAVxx` |
| `31`–`39` | 1P 不可见对象（仅 keysound） | base36 → `#WAVxx`；**不显示、不判定、不计分**，只播音 |
| `41`–`49` | 2P 不可见对象 | base36 → `#WAVxx` |
| `51`–`59` | 1P 长条（LN） | base36 → `#WAVxx`；语义随 `#LNTYPE`，见 §5 |
| `61`–`69` | 2P 长条 | base36 → `#WAVxx` |
| `D1`–`D9` | 1P 地雷（landmine） | base36；触雷音效用 `#WAV00`（nanasi 扩展） |
| `E1`–`E9` | 2P 地雷 | base36 |

### 2.3 BGA、判定与杂项

| Channel | 含义 | 数据编码 |
| --- | --- | --- |
| `04` | BGA base（主动画层） | base36 → `#BMPxx` |
| `06` | BGA poor（miss 时显示） | base36 → `#BMPxx` |
| `07` | BGA layer（叠加层） | base36 → `#BMPxx` |
| `0A` | BGA layer2（第二叠加层） | base36 → `#BMPxx`（beatoraja） |
| `A0` | 判定窗口变化 | base36 → `#EXRANKxx`（nanasi） |
| `A1`–`A4` | BGA aRGB / 透明度（base/layer/layer2/poor 系列） | base36 → `#ARGBxx`；⚠️ A1–A4 与各层的精确对应在各实现间不完全一致，依赖时请直接核对 hitkey |
| `A5` | 按键绑定 BGA（key-bound BGA） | bemaniaDX；标准化程度低 |
| `A6` | 动态选项变化 | base36 → `#CHANGEOPTIONxx`；**同小节多行不合并** |
| `99` | 文本/歌词显示 | base36 → `#TEXTxx` / `#SONGxx`（pomu） |

> **现代实现忽略 `#PLAYER`**，改由"哪些 channel 上有对象"推断 SP/DP 与键数（见 §3.2）。

---

## 3. 键位 / lane 映射

### 3.1 1P / 2P 可见音符 channel → 键位

| Channel(1P / 2P) | 键位 | 说明 |
| --- | --- | --- |
| `11` / `21` | KEY1 | |
| `12` / `22` | KEY2 | |
| `13` / `23` | KEY3 | |
| `14` / `24` | KEY4 | |
| `15` / `25` | KEY5 | |
| `16` / `26` | **SCRATCH（碟）** | ⚠️ 注意是 16/26，不是某个"第 6 键" |
| `17` / `27` | FREE-ZONE / 脚踏（遗留 5-key） | 现代谱多不用 |
| `18` / `28` | **KEY6** | 7-key 才有 |
| `19` / `29` | **KEY7** | 7-key 才有 |

> ⚠️ **经典陷阱**：7-key 的第 6、7 键是 `18`/`19`，**不是** `16`/`17`——`16` 永远是 scratch。因此一张 7K 谱用 `11 12 13 14 15 18 19` 当 7 个键、`16` 当 scratch，`17` 通常空置。

### 3.2 由 channel 推断游玩模式（现代实现）

| 模式 | 携带对象的 channel |
| --- | --- |
| 5K + scratch | 1P `11`–`16` |
| 7K + scratch | 1P `11`–`19` |
| 10K（DP，5+5） | 1P `11`–`16` + 2P `21`–`26` |
| 14K（DP，7+7） | 1P `11`–`19` + 2P `21`–`29` |

### 3.3 PMS（pop'n music，9 键，无 scratch）

- PMS 专精 9 BUTTONS，**没有 scratch channel**。
- ⚠️ **存在多套约定，必须以本仓库 K9 的 canonical lane contract 为准，不可照搬任一外部表**：
  - 一种常见约定（hitkey 记载的 BME 兼容型）：button 1–5 = 1P `11`–`15`，button 6/7/8/9 = `18`/`19`/`16`/`17`。
  - 另有 beatoraja `.pms` 原生约定与之不同。
- 因此 PMS 9K 的列映射在审查时应被视为"需要被解析器/转换器显式钉死"的点，而不是"照抄某张表即可"。OMS 侧已在 [P1-K K9 约束](../subline/P1-K/TECHNICAL_CONSTRAINTS.md) 中要求 `9K_Pms` 走 canonical 顺序、不制造 fake scratch 列。

---

## 4. 时序语义

### 4.1 BPM

- **初始 BPM**：`#BPM <real>` header（BM98 缺省 130）。
- **channel 03**：两位十六进制 `01`–`FF`，直接表示 1–255 BPM。
- **channel 08 + `#BPMxx`**：可超 255、可小数、可负。各实现上限差异极大（nanasi ~1e6；LR2/beatoraja ~9e8）。
- **负 BPM**：行为分歧严重——"reverse（反向滚动）/ stop / ignore / ROARING（死循环）"四类都出现过。**必须显式钉死本实现的语义**。
  - OMS 现状：`#BPMxx` 允许带符号进入 typed model；timeline 推进按**绝对值**消费，sign 单独保留（见 P1-K K2-A / K3-A）。
- **03 与 08 同位并存**：处理顺序按文件行序，旧实现（BM98de）对先后敏感；DTXCreator 会把 03 全转成 08。

### 4.2 STOP

- `#STOPxx <integer>` + channel `09`（base36 索引）。
- **单位：1/192 个 4/4 小节**（即 1/48 拍 / 1/48 个四分音符）。所以 `#STOPxx 192` = 冻结一整个 4/4 小节，`48` = 冻结一拍。
- `#STP`（bemaniaDX）是另一套独立 STOP：`#STP <measure>.<position> <duration_ms>`，按毫秒、可重复声明（不受"同命令唯一"限制）。

### 4.3 小节长度 / 拍号

- channel `02`，十进制浮点直写（§2.1）。`1.0`=4/4，`0.75`=3/4，`0.015625`=1/64（BMSE 可编辑最小值）。
- **只作用于该小节**（与 DTX 的"一次声明持续生效"不同）。

### 4.4 SCROLL / SPEED（beatoraja/bemuse 扩展）

- `#SCROLLxx <float>` + channel `SC`：作为**滚动速度倍率**改变音符在屏上的移动速度，**不改变 timing/判定**。可跨小节持续（不受"逐小节命名"限制）。兼容性有限，非全实现支持。
- `#SPEEDxx <float>` + channel `SP`：改变音符间距（soflan 视觉），通常做插值过渡。
- OMS 现状：`#SCROLLxx`/`SC` 已升格为 `ScrollEvents` + converter 侧 `EffectControlPoint.ScrollSpeed`（P1-K K3-D）；`#SPEEDxx`/`SP` 是否覆盖需在审查中确认（疑似 gap）。

### 4.5 同拍位事件顺序

- bmson 明确规定同 pulse 处理序：**Notes/BGA → BpmEvent → StopEvent**；同位多个 BPM 取最后一个，多个 STOP 累加。
- 经典 BMS 无统一书面规定，但 de-facto 与上一致（先应用 tempo/stop 再结算对象时间）。
- OMS 现状：converter 已单独拥有并冻结同拍位 `BPM → STOP → object` 顺序（P1-K 时间轴约束 / K3-A）。审查时须确认此顺序与上述事实基线一致、且只此一处 authority。

---

## 5. 长条（LN / CN / HCN）

| 机制 | 来源/默认 | 编码方式 |
| --- | --- | --- |
| `#LNTYPE 1`（RDM 记法，默认） | RDM 1.7+ | 用 channel `51`–`59`/`61`–`69`；同 channel 上对象**成对**出现：第一个=头，下一个=尾 |
| `#LNTYPE 2`（MGQ 记法） | RDM | 同 LN channel 上，**连续非零对象**=长条主体，其后第一个 `00`=收口 |
| `#LNOBJ xx` | RDM 1.61+ | 长条写在**普通可见 channel**（`11`–`19`）上：index==`#LNOBJ` 的对象=**尾**，其前一个同 channel 可见对象=**头**；允许多个 `#LNOBJ` |
| `#LNMODE n` | beatoraja | 全谱声明长条类型：`1`=LN、`2`=CN（charge note）、`3`=HCN（hell charge note） |

- LN 尾端是否要求 keyup（松手判定）各实现不同（nanasi/HDX 要求 keyup）。
- OMS 现状：`#LNTYPE 2` 已建立最小 MGQ 状态机（显式 `00` 收口 + duplicate compound 遵循"`00` 不覆盖"），并端到端转成 `BmsHoldNote`（P1-K K3-B）。CN/HCN 与 `#LNMODE` 的真实谱验校归 [P1-E](../subline/P1-E/)。

---

## 6. 复合 / 覆盖规则（同小节同 channel 重复行）

> 这是 P1-K K1-A「source line order」与 K3「duplicate channel compound/overwrite 语义」约束的外部依据。

- **默认：加性叠加（additive overlay）**。把重复行按各自分母对齐到最小公倍数网格后逐槽合并：
  - **行号大者（更靠近 EOF）优先**；
  - 但 **`00` 永不覆盖**已有对象。
- **不参与叠加的例外**（按"最靠近 EOF 的行覆盖"或并行处理）：
  - `01`（BGM）——各行并行成多条 BGM 轨，不合并；
  - `02`（小节长度）——最后一行生效；
  - `A6`（`#CHANGEOPTION`）——不合并。
- ⚠️ hitkey 明言"约一半实现并未正确满足该叠加规范"，因此真实谱面可能依赖、也可能违反它。

### 已核对的叠加示例（hitkey 原例）

```
行 100: #00113:11111111            （4 等分：11 11 11 11）
行 200: #00113:0022332255224400    （8 等分：00 22 33 22 55 22 44 00）
行 300: #00113:0066                （2 等分：00 ……   66 ……）
────────────────────────────────────────────────
结果:   #00113:1122332266224400    （8 等分：11 22 33 22 66 22 44 00）
```

逐槽推演（8 格）：`00`不覆盖、大行号优先 → 槽0 `11`（200/300 的 `00` 不覆盖）、槽2 `11`→`33`、槽4 `11`→`55`→`66`、其余取 200 行值。结果自洽。

---

## 7. 控制流（`#RANDOM` / `#SWITCH`）

```
#RANDOM n            （或 #SETRANDOM n 强制固定值，便于复现）
  #IF v
    …commands / channel 行…
  #ELSEIF v
    …
  #ELSE
    …
  #ENDIF
#ENDRANDOM
```

```
#SWITCH n            （或 #SETSWITCH n）
  #CASE v
    …
  #SKIP              （跳到 #ENDSW；C 风格 fallthrough）
  #DEF               （默认分支）
    …
#ENDSW
```

- `#RANDOM n` 生成 `[1..n]` 随机整数，命中的 `#IF`/`#ELSEIF`/`#ELSE` 分支生效；**允许嵌套**。
- `#SETRANDOM`/`#SETSWITCH` 固定值，用于可复现（编辑器/测试）。
- 解析器健壮性陷阱：`#RONDAM`（typo）、`#END IF`（中间带空格）等常见错误写法。
- ⚠️ **审查重点**：解析器需要明确"在 parse 期解析 RANDOM 还是保留结构、如何 seed"。`#RANDOM`/`#SWITCH` 是真实谱面（尤其 insane/差分谱）的常见特性，若 OMS 解析链当前未处理，应作为显式 gap 立项，而不是默默丢弃分支内容。

---

## 8. header / 定义命令速查

> 完整逐条与跨实现差异以 hitkey 为准；下表是解析审查常用子集。索引类（`#XXxx`）默认 base36 索引。

### 8.1 元数据

| 命令 | 含义 / 取值 | 备注 |
| --- | --- | --- |
| `#TITLE` / `#SUBTITLE` | 主/副标题 | 部分实现在无 `#SUBTITLE` 时从 `#TITLE` 末尾隐式切出副标题 |
| `#ARTIST` / `#SUBARTIST` | 主/副曲师；`#SUBARTIST` 可多行 | OMS 的 BMS local artist/creator 展示已统一走 `BeatmapLocalMetadataDisplayResolver`（P1-K K4 系列） |
| `#GENRE` | 流派（`#GENLE` 为常见 typo） | |
| `#MAKER` / `#COMMENT` | 制作来源 / 选歌注释 | |
| `#STAGEFILE` / `#BANNER` / `#BACKBMP` | 载入图 / 选歌 banner / 游玩背景图 | OMS 静态背景已优先 `STAGEFILE/BACKBMP/BANNER`，缺失时回退 `#BGA/#@BGA`（P1-K K4-A） |
| `#PREVIEW` | 试听音频 | beatoraja 扩展，hitkey 2011 版未收录 |
| `#CHARFILE` | 角色文件（pop'n 风） | |
| `%URL` / `%EMAIL` | 作者联系信息 | BMSManager/BMSC 生成，少被解析 |

### 8.2 难度 / gauge（判定细节归 P1-C）

| 命令 | 含义 / 取值 | 备注 |
| --- | --- | --- |
| `#PLAYER` | `1`=SP、`2`=Couple、`3`=DP、`4`=Battle | 现代实现忽略，改由 channel 推断 |
| `#RANK` | 判定宽窄：`0`=VERY HARD、`1`=HARD、`2`=NORMAL、`3`=EASY（缺省）、`4`=VERY EASY(nanasi) | 社区常以 `2`(NORMAL) 为推荐基准；判定语义归 P1-C |
| `#DEFEXRANK` / `#EXRANK` | 百分比判定（`100`=`#RANK 2`），可小数 | nanasi 系；与 `#RANK` 并存时取最靠 EOF 的 |
| `#TOTAL` | gauge/生命总量 | 缺省行为各异，LR2 等按物量/难度估算 |
| `#PLAYLEVEL` | 难度显示（数字或符号串） | 非强标准 |
| `#DIFFICULTY` | `1`=BEGINNER、`2`=NORMAL、`3`=HYPER、`4`=ANOTHER、`5`=INSANE | nanasi 系，多作元数据/分组 |
| `#VOLWAV` | keysound 主音量 `0`–`100` | |

### 8.3 索引 / 资源定义（`#XXxx`，base36 索引）

| 命令 | 含义 | 关联 channel | 备注 |
| --- | --- | --- | --- |
| `#WAVxx` | keysound 文件 | `01`/`11`-`69`/`31`-`49` | 支持 wav/ogg "alternative search"；同小节同 channel 复合见 §6；`#WAV00`=地雷音效 |
| `#BMPxx` | 图片/视频 | `04`/`06`/`07`/`0A` | `#BMP00`=空/poor 暗化；视频需 `#VIDEOFILE/#VIDEOf,s/#VIDEODLY` |
| `#BPMxx` | 扩展 BPM（real，可负） | `08` | 索引 `01`-`ZZ`；同 header 重复取最靠 EOF |
| `#STOPxx` | STOP 时长（1/192 小节单位） | `09` | 见 §4.2 |
| `#SCROLLxx` | 滚动倍率 | `SC` | beatoraja/bemuse |
| `#SPEEDxx` | 音符间距 | `SP` | beatoraja/bemuse |
| `#EXRANKxx` | 分段判定窗口（ms） | `A0` | nanasi 系 |
| `#TEXTxx`/`#SONGxx` | 游玩中文本 | `99` | DBCS 编码易乱码 |
| `#CHANGEOPTIONxx` | 动态选项 | `A6` | 同小节多行不合并 |
| `#ARGBxx` | aRGB 颜色/透明 | `A1`-`A4` | |
| `#BGAxx` | BGA 裁剪：`#BGAxx <BMPidx> x1 y1 x2 y2 [dx dy]` | — | 与 `#@BGAxx`/`#SWBGAxx` 同族；OMS 已收入 richer BGA-definition typed surface（P1-K K3-C/K4-A） |
| `#LNOBJ` / `#LNTYPE` / `#LNMODE` | 长条 | `11`-`19` / `51`-`69` | 见 §5 |
| `#PATH_WAV` | wav 搜索目录 | — | 编辑器/试玩用 |
| `#CHARSET` | 文本编码声明 | — | 见 §1 |
| `#OPTION` | 强制游玩选项（bitmask） | — | 同命令可多行 |
| `#STP` | 毫秒 STOP | — | bemaniaDX；可多行 |

> 可多行声明的"例外命令"（不遵循"重复取最靠 EOF"）：`#STP`、`#LNOBJ`、`#WAVCMD`、`#OPTION`、`#CHANGEOPTION`（部分）；以及 channel 侧的 `01`/`A6`。

---

## 9. bmson（JSON 旁系，OMS 当前不读）

> bmson 是 Bemuse 生态的 JSON 化格式，与经典 BMS **不互通**。OMS 当前只读 `.bms/.bme/.bml/.pms`，故 bmson 仅作未来设计对照。

- **顶层**：`version`、`info`、`lines`（小节线 pulse 数组）、`bpm_events`、`stop_events`、`sound_channels`、`bga`。
- **`info`**：`title`/`subtitle`/`artist`/`subartists[]`/`genre`/`mode_hint`(默认 `"beat-7k"`)/`chart_name`/`level`/`init_bpm`(必填)/`judge_rank`(默认 100)/`total`(默认 100)/`back_image`/`eyecatch_image`/`banner_image`/`preview_music`/`resolution`(默认 240，每四分音符脉冲数)。
- **时序**：纯 **pulse** 模型，`resolution` = 每四分音符脉冲数；**无 measure/fraction**，小节线由 `lines` 显式给出。同 pulse 处理序 = Notes/BGA → BPM → STOP。
- **音符**：`sound_channels[] = {name, notes:[{x,y,l,c}]}`。
  - `x`=lane（`0`/`null`=BGM 自动播放，`1+`=可游玩）；`y`=pulse；`l`=长度（`0`=普通，`>0`=长条，跨 `y`→`y+l`）；`c`=continuation（`true`=接续不重起音、即切片延续）。
  - 切片：遇到 `c:false` 的音符就重起音频。
- **`bga`**：`{bga_header:[{id,name}], bga_events:[{y,id}], layer_events:[…], poor_events:[…]}`。
- **`mode_hint`**：`beat-5k`/`beat-7k`/`beat-10k`/`beat-14k`/`popn-5k`/`popn-9k`/`generic-nkeys`；beat 系里 scratch lane 为 `x=8`(1P)/`x=16`(2P)。
- **核心 1.0.0 未含** `mine_channels`/`key_channels`/`scroll_events`（扩展可能补）。
- **与 BMS 的根本差异**：JSON vs 文本；pulse vs measure/fraction；运行时动态切片 vs 预切 keysound；字段 snake_case。

---

## 10. bemusic/bmspec（可执行回归基线）

- 仓库以 **Gherkin/Cucumber** 写成（`features/` 目录下 `.feature` 文件，100% Gherkin），是可执行的行为规范。
- 覆盖类别：
  - **基础解析**：注释/header/channel 句、`#TITLE`/`#SUBTITLE`/`#MAKER`/`#GENRE`(`#GENLE`)、BGM 叠加（`01`）、拍号（`02`）。
  - **时序**：`03`/`08` 的 BPM、`09`/`#STP` 的 STOP、小节长度与拍位。
  - **进阶**：长条（`#LNTYPE 1`/`#LNOBJ`）、BGA（`#BMPxx`/`04`/`07`）。
- 自述"**不是**官方 BMS 规范"，但把行为形式化成可自动测试的用例。
- **对 OMS 的用法**：可把这些 `.feature` 场景翻成 `BmsBeatmapDecoderTest` / `BmsBeatmapConverterTest` 的具体用例（P1-K 已要求 decoder/converter/import 三层 focused 回归），作为语言无关的合规基准；尤其适合补 §6 叠加规则、§2 编码陷阱、§5 长条这类边界。

---

## 11. OMS 解析链路审查对照清单

> 审查 `BmsBeatmapDecoder → BmsDecodedChart / BmsBeatmapInfo → BmsBeatmapConverter` 时，逐项对照本文事实基线。括号内为相关 P1-K 切片/约束。

1. **channel 02/03 不可当 base36 对象解析**：`02` 是浮点小节长度、`03` 是十六进制 BPM 字面量。这是最易翻车点（§2.1）。
2. **`08` vs `03` 的"索引 vs 字面量"**、以及 **signed BPM** 经 `#BPMxx`/`08` 进入 typed model、timeline 按绝对值推进、sign 单独保留（§4.1；K2-A/K3-A）。
3. **STOP 单位 = 1/192 小节**，converter 时间换算需据此（§4.2；时间轴约束）。
4. **base36（1296）vs base16（256）** 对象解码与 `00`=rest（§1）。
5. **同小节同 channel 复合规则**：LCM 对齐、大行号优先、`00` 不覆盖、`01`/`02`/`A6` 例外；并保留 source line order（§6；K1-A/K3）。
6. **键位陷阱**：`16`/`26`=scratch、`18`/`19`=KEY6/7（≠16/17）；K9 lane flatten 必须走 canonical contract、PMS 走多约定澄清后的固定列（§3）。
7. **长条三套记法**：`#LNTYPE 1`/`#LNTYPE 2`(MGQ 显式 `00` 收口)/`#LNOBJ`；CN/HCN 与 `#LNMODE`（§5；K3-B，CN/HCN 验校归 P1-E）。
8. **`#RANDOM`/`#SWITCH` 控制流**：确认解析策略，若未处理须作为显式 gap 立项、不可静默丢分支（§7）。
9. **`SCROLLxx`/`SC` 已建 typed consumer contract**；`SPEEDxx`/`SP` 是否覆盖需确认（§4.4；K3-D）。
10. **地雷 `D1-D9`/`E1-E9` + `#WAV00`** 已进 `MineEvents`（§2.2；K3-C）。
11. **BGA `04/06/07/0A` + `#BGA/#@BGA/#ARGB/#SWBGA/#POORBGA`** 的 typed surface 与静态背景投影（§2.3/§8.3；K3-C/K4-A）。
12. **文本 channel `99` / `#TEXTxx`** 是否至少进 raw snapshot（§2.3；K1-A "未消费语义必须保留"）。
13. **编码**：Shift_JIS 默认 + `#CHARSET`；title/artist 解码错误会污染下游持久化与转谱星（§1）。
14. **未消费即保留**：unknown header / unknown channel / `#SPEED` 等当前未建模项，必须落 raw snapshot 或 typed placeholder，不得在 decode 阶段丢数据（K1-A/K1-B 约束）。

---

## 联动要求

1. 本文是参考材料，不直接替代 `mainline` / `subline` 的计划、状态与约束。
2. 一旦本文某条结论升级为正式约束或执行优先级（例如确认某 channel 语义为硬合同、或把 bmspec 用例正式纳入回归门槛），必须回写 [P1-K 四件套](../subline/P1-K/) 与必要时的 [mainline](../mainline/)。
3. 外部 URL 若再次迁移/失效，更新本文「参考来源」表，并同步 P1-K 中引用同一来源的链接。
