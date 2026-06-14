# 主线子方向文档索引

这里收口从主线可直接追踪的开发方向。每条 `P1-*` 子线都必须固定维护：

1. `DEVELOPMENT_PLAN.md`
2. `DEVELOPMENT_STATUS.md`
3. `CHANGELOG.md`
4. `TECHNICAL_CONSTRAINTS.md`

## 当前子线入口

- [P1-A/README.md](P1-A/README.md)：产品面与 release gate，含皮肤边界冻结。
- [P1-B/DEVELOPMENT_PLAN.md](P1-B/DEVELOPMENT_PLAN.md)：输入语义与硬件验收。
- [P1-C/README.md](P1-C/README.md)：判定语义与反馈闭环。
- [P1-D/DEVELOPMENT_PLAN.md](P1-D/DEVELOPMENT_PLAN.md)：控制器校准与诊断。
- [P1-E/DEVELOPMENT_PLAN.md](P1-E/DEVELOPMENT_PLAN.md)：gameplay 与长条真实谱面验校。
- [P1-F/DEVELOPMENT_PLAN.md](P1-F/DEVELOPMENT_PLAN.md)：发行后置与首发离线发布验收。
- [P1-G/DEVELOPMENT_PLAN.md](P1-G/DEVELOPMENT_PLAN.md)：人工验收后置。
- [P1-H/DEVELOPMENT_PLAN.md](P1-H/DEVELOPMENT_PLAN.md)：存储拓扑支撑线。
- [P1-I/DEVELOPMENT_PLAN.md](P1-I/DEVELOPMENT_PLAN.md)：BMS 选歌筛选与搜索定制。
- [P1-J/DEVELOPMENT_PLAN.md](P1-J/DEVELOPMENT_PLAN.md)：BMS gameplay runtime 性能与音频时序治理。
- [P1-K/DEVELOPMENT_PLAN.md](P1-K/DEVELOPMENT_PLAN.md)：BMS 解析链路治理。
- [P1-L/DEVELOPMENT_PLAN.md](P1-L/DEVELOPMENT_PLAN.md)：BMS 演出/Gimmick 谱视觉复刻（隔离旁路渲染：地雷 / 专用滚动 / BGA 背景图·动画；红线：不改坏正常游玩链路）。
- [P1-M/DEVELOPMENT_PLAN.md](P1-M/DEVELOPMENT_PLAN.md)：内置音乐播放器（分层 PlayQueue：真队列/重复·随机/曲库搜索排序/收藏歌单/可展开全屏/可视化/播放源 mania·bms·both；红线：不改坏 song-select 试听与 gameplay 音轨控制、离线只用本地轨）。

## 联动要求

完整联动更新规则见 [doc_md 总索引 · 联动更新规则](../README.md#联动更新规则)。子线要点：① 先归线，再改对应子线目录下的四件套；② 子线变化若影响全局优先级 / 主线状态 / 硬约束，必须反向回写 `../mainline/` 四件套。
