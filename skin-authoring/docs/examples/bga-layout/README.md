# BGA Layout：两窗与三种适配

这是完整最小INI皮肤，普通素材继承静线，不需要另附图片。只改变原生BMS的7K/14K窗口，不给mania或BMS转谱增加BGA。没有制作配方，不使用`generate`。

在套件目录运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action check -Source .\docs\examples\bga-layout
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Author.ps1 -Action import -Source .\docs\examples\bga-layout -Output .\dist\bga-layout.osk
```

7K先选择**1P样式**并使用有BGA的谱面：右侧上窗使用`fit`，下窗使用`fill`。14K例子把舞台宽度设为0.40，两窗分别放两侧。它们共享谱面的时间线与内容；在同一时刻看到的是同一合成画面的不同适配结果，不是两份独立播放器。

每组格式为 `x,y,width,height,fit|fill|stretch`，分号隔开；坐标相对安全区域而非裸窗口像素。`fit`完整显示并留边，`fill`中心裁切填满，`stretch`直接拉伸；有时间线时适配4:3合成画布，不逐资源重排谱面叠层。

先把第二组末尾`fill`改为`stretch`，检查拉伸效果；再把整行改为`BgaViewports: none`，确认关闭；最后删除这一行，确认恢复旧自动布局（14K默认四窗）。`none`和没有这一行含义不同。

一组最多16窗，x/y非负、宽高为正、右/下边不超过1。大小写、非法数字、越界和超量由工具拒绝。实际窗口撞上轨道、键区、血槽、HUD或预留信息区时整组不显示，游戏不会擅自挪动作者窗口。进入游戏后在日志目录的`runtime.log`搜索`bms.layout.bga-viewports-unavailable`；设置没有专属布局错误面板，离线check不代表实际布局已验证。这份7K例子不承诺适用于2P/窄屏，切换后应检查并调整自己的布局。

`BgaInformationHeight`独立于窗口开关：大于0时，即使`none`或无空间，曲名/判定/速度/玩家信息区域仍保留；等于0才不生成这些区域。同包的场景索引沿声明顺序从0开始；不能在两窗例子中引用index=2。完整布局解释见[作者参考](../../REFERENCE.md)。

找不到日志时，在设置中搜索“日志”或“logs”，点击“导出日志 / Export logs”，完成后点击通知打开生成的 `compressed-logs.zip`；解压其中的 `runtime.log`，搜索上述代码。这是现有通用日志出口，不需要注册外部目录或打开开发工具。
