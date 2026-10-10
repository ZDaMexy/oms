# 文件系统谱库身份与失效召回

权威：[P1-H 状态](../../doc_md/subline/P1-H/DEVELOPMENT_STATUS.md) · [稳定合同](../../doc_md/subline/P1-H/TECHNICAL_CONSTRAINTS.md)。这里记录诊断地雷，不代替当前验收状态。

## 同 hash 不等于同目录

旧 importer 先取任意同 hash 记录再校验路径，不可复用则把旧记录标 `DeletePending`。Realm 启动清理会收集待删 managed 记录的 `FilesystemStoragePath` 并删除目录，因此“仅导入另一份相同谱面”能沿生产调用链变成旧目录删除。检查重复来源问题须同时看 importer 和 `cleanupPendingDeletions`，不能只看选歌列表有没有重复。

最小回归使用临时 storage，在 `chartbms/A` 与 external B（或 `chartbms/B`）放相同谱文件内容，分别注册、重开 Realm，断言两份路径和记录仍存在。set hash 由谱文件内容形成，绝对父目录不参与身份。

## 失效不是删除

`FilesystemUnavailable` 保留旧谱面身份、成绩关联与收藏；`DeletePending` 仍有清理/物删语义。不要为“从列表消失”复用删除开关，也不要把旧库全部待删条目猜成扫描失效而复活。

改动验证要检查源文件、旧 set/beatmap ID、ScoreInfo 链接、收藏 MD5，而不只断言 active 数量。目录改名和原目录恢复是不同路径问题；当前不会自动跨路径迁移成绩身份。

## 重建与复用陷阱

- 成功目录导入不代表整个根扫描完整。扫描器收到错误或取消后不能收敛缺失索引，已完成的单目录更新不整体回滚。不要将 importer 在仍有有效谱面时的跳过/警告误当扫描错误；先看返回结果和错误统计。整根离线不能当空根清理。
- 谱文件改名且仍命中同集合 hash 时，必须更新 `LocalFilePath`，否则加载仍指向旧文件名。set hash 由按文件名排序后的谱文件内容形成；多文件改名改变排序时可能改变集合 hash，不能把单谱改名回归扩张为所有改名都保留原 set ID。
- 同目录冲突查找应让 Realm 先筛路径和权限，不能每导入一个目录便将全库记录 `ToList()`；这会把大库重建变成反复拉取全部对象。
- 新准入拒绝父子 root 并不能消除旧配置重叠。解除任一旧根前，须看同类型剩余根是否仍覆盖该目录；祖先链接也须在扫描旧配置时检查，不能只保护 AddRoot。
- 曾有 `Task<FolderImportResult>` 降为 `Task` 后不检查 null 结果的链路，使全坏谱目录被计成功并允许缺失收敛。当前 Register 已改为无有效谱面时明确报错，普通 Import 的空结果通知另保留；回归此类问题还要检查分类器是否把文件占用异常吞成“非本模式”。
- 混合目录可同时含 BMS 与 mania，尤其旧父子根配置。相同路径和权限不足以确定可替换范围，须再限定 ruleset，避免一边重扫隐去另一边。

精确 fixture 与验证结果查 [P1-H CHANGELOG](../../doc_md/subline/P1-H/CHANGELOG.md) 2026-09-29。上述字符串路径检查不构成 held filesystem identity，皮肤写入 authority 继续按其独立合同处理。

## 大库扫描诊断

- `DirectoryArchiveReader.Filenames` 是实时递归枚举，不是已缓存列表。素材解析每引用一次就访问它，会随谱面、BGA 引用和文件数放大。单次目录导入的文件快照/字典须记录原枚举顺序，不能顺便改变多扩展候选偏好；不将缓存留到下一次扫描。原始 hash、可玩转换和解析警告仍由原链路处理。
- 增量 active 检查先在 Realm 筛路径/权限/可用性，再确认玩法与 BMS 注册根；先把全库 `ToList()` 再查一个目录是另一处放大器。同路径另一玩法的记录不能让当前模式跳过或阻止恢复。
- 累计临时分配不等于峰值内存；旧日志首末解析消息的间隔不等于完整扫描计时。Windows 文件读取资源不足需要保留具体异常，不能仅因优化降低分配就写成原因已确认或故障已修复。证据与取舍见 [扫描审查](../../doc_md/other/LIBRARY_SCAN_REVIEW_20261010.md)，当前结果只取 P1-H STATUS。
