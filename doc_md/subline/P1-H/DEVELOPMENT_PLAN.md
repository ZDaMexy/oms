# P1-H 当前计划：存储拓扑

> 最后更新：2026-09-29（新增自动回归完成，保留隔离根实机、真实大库与诊断门）
> 当前事实见 [DEVELOPMENT_STATUS.md](DEVELOPMENT_STATUS.md)，稳定合同见 [TECHNICAL_CONSTRAINTS.md](TECHNICAL_CONSTRAINTS.md)，批次及验证命令见 [CHANGELOG.md](CHANGELOG.md)。

## 子线职责

维护 `chartbms/`、`chartmania/`、portable/custom data root、managed/external 谱库及其 Realm/path authority。为导入、发行和 P1-A/G1 提供路径经验，不输出可直接复制的皮肤 scanner/delete 实现。

## 当前执行顺序

### 1. 隔离数据根及真实大库验收

1. 备份数据根覆盖存在→单谱删除→目录移走/改名→恢复→重扫，确认列表、磁盘、成绩和收藏一致。
2. 验证外部根离线只报错、解除只改可用性；遗留重叠根的剩余覆盖有效，最后解除才隐去。新根重复、父子重叠和链接拒绝应清楚提示。
3. 表开关/刷新保持当前选歌页面和选中歌曲，检查分组及谱卡文字；记录大库同步耗时、掉帧及输入响应。小库自动测试不能替代。
4. 对旧 `DeletePending` 遗留保留证据，不猜测自动恢复。数据根迁移和 held identity 不属于现有谱库索引能力。

验收区分临时 fixture、隔离库、用户真实库；没做的人工项明确保留，不用测试数量替代。

### 2. 现场只读诊断

1. 对 difficulty-table/MD5/source identity 不匹配提供脱敏、可定位的只读诊断。
2. 区分原始字节 MD5、共享字段覆盖、批量通知或谱卡更新遗漏、真实路径不可用。
3. 不在筛选路径逐谱加载 working beatmap、全库重算或写 Realm。

验收：解释未匹配/未刷新，不泄露用户绝对路径、不改变库状态，明确下一 owning 子线。

## 向 P1-A/G1 输出的边界

- 可复用：managed/external 分离、规范路径、索引失效与用户物删分离、批量持久化通知。
- 不可复制：字符串检查作为 held identity、历史清理作为 skin authority、外部目录写权限。
- G1 独立维护 package identity、skin selection、rename/delete 与 atomic reload。

## 明确不做

- 不恢复远端同步、backend 镜像或定时联网刷新。
- 不将难度表管理泛化为跨 ruleset 平台。
- 不以异步化或 busy UI 替代 correctness；大库性能结论须有测量。
