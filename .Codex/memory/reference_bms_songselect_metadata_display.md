---
name: reference-bms-songselect-metadata-display
description: Song Select 元数据显示：源ruleset与当前模式、Revive命名优先级及只读展示边界
metadata:
  node_type: memory
  type: reference
---

# BMS Song Select metadata 地雷

字段与展示合同见 [P1-K CONSTRAINTS](../../doc_md/subline/P1-K/TECHNICAL_CONSTRAINTS.md) 的元数据章节；选歌当前面见 [P1-I STATUS](../../doc_md/subline/P1-I/DEVELOPMENT_STATUS.md)。入口是 [BeatmapLocalMetadataDisplayResolver](../../osu.Game/Beatmaps/BeatmapLocalMetadataDisplayResolver.cs)。

- resolver按item本身Ruleset.ShortName=bms，而非当前游玩ruleset；BMS转mania后源仍bms，所以title/difficulty/artist/creator一致，star与key badge另有模式逻辑。
- native作者PLAYLEVEL、converted-mania star、note-distribution density是三个量。不要从图表推native星数，难度表分组也只用persisted table/level。
- 真实回归例：Dead Soul [Revive]且DIFFICULTY=5，必须显示Revive，不让Insane盖掉谱师显式命名。优先Subtitle/title tag，再HeaderDifficulty类别，纯数字存储难度名可为空。
- 只有Subtitle缺失时才剥Title末尾配对括号或完整对称-X-/~X~；Re-Loaded这类普通连字符不能误删。artist中的obj后缀用于提取署名，creator从导入后的Author取，无值显示占位。
- 这是display-only：不改存储Title/DifficultyName，不改变MD5、原始search/sort/group；旧库无需重导。
- core不能引用BMS ruleset，用BmsPersistedMetadataResolver读同一持久JSON；不能因显示需求引入另一套可写schema或重开.bms。
- Song Select panel/TitleWedge与全局now-playing/results的BeatmapInfoExtensions不是同一范围；后者当时有意未改，不把局部显示统一写成全局能力。

回归定位：BmsLocalMetadataDisplayResolverTest的TestTitleTagBeatsHeaderDifficultyLabel。key-count见 [[reference_converted_mania_keycount_display]]，table标签见 [[reference_bms_difficulty_table]]。
