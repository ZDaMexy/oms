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

## 联动要求

1. 任何开发必须先归线，再更新对应子线目录下的四件套。
2. 子线变化若改变全局优先级、主线状态或硬约束，必须同步回写 `../mainline/`。