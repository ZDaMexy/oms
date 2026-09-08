---
name: reference_bms_songselect_reveal_in_explorer
description: 文件位置入口使用GameHost而非Storage，外部根只读、managed/external路径与面板DI地雷
metadata:
  node_type: memory
  type: reference
---

# Song Select 文件位置入口地雷

用户决定与入口范围见 [P1-I CONSTRAINTS](../../doc_md/subline/P1-I/TECHNICAL_CONSTRAINTS.md)，存储拓扑见 [P1-H CONSTRAINTS](../../doc_md/subline/P1-H/TECHNICAL_CONSTRAINTS.md)。唯一helper是 [FilesystemBeatmapLocation](../../osu.Game/Beatmaps/FilesystemBeatmapLocation.cs)。

- 用户选择“所有本地文件夹谱面”，不是BMS-only：FilesystemStoragePath非空的chartbms和直读chartmania可见菜单，hash-backed files库无真实folder就不提供。
- managed set目录由Storage.GetFullPath(relative)解析，external用其绝对FilesystemStoragePath；chart file再拼LocalFilePath与规范分隔符。P1-H改该约定时同时复核BmsBgaPlayer的路径解析。
- reveal必须走GameHost.PresentFileExternally(absolutePath)，Windows打开父目录并选中目标；Storage.PresentFileExternally会将external绝对路径当越出数据root的traversal而拒绝。不能为修这个入口放宽Storage containment。
- 目标缺失时可按既有行为打开父目录，否则no-op；external始终只读打开Explorer，不执行写/移动/删除。
- 调用面是PanelBeatmapSet歌曲菜单、SoloSongSelect forward action谱面菜单，以及兼作歌曲条的PanelBeatmapStandalone。后者不能漏歌曲入口；共享forward action还覆盖难度行/footer。
- 新增Resolved GameHost/Storage要由真实panel scope提供；helper纯单测不能证明scene依赖注入。回归定位FilesystemBeatmapLocationTest、TestScenePanelSet、TestScenePanelBeatmapStandalone，不在此复制旧通过数字。

当前人工定位签收读P1-I STATUS/CHANGELOG；历史“panel加载通过”不等于Explorer真机已确认。相关 [[project_oms_songselect_display_nav]]。
