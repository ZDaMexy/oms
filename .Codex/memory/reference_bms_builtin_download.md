---
name: reference_bms_builtin_download
description: P1-A 公共 BMS 下载源、浏览可见性、任务收尾及精确选歌接入地雷
metadata:
  node_type: memory
  type: reference
---

当前能力与剩余验收见 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)，唯一产品合同见 [第三方 BMS 浏览下载](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#第三方-bms-浏览下载)。这里保留接入时已复现的诊断线索，不把两源窄例外扩成全局联网许可。

## 浏览入口与 UI 生命周期

- Framework `ToggleVisibility()` 直接改变 `State`，不会经过 `Show()/Hide()` 重写。网络启动、隐藏取消与 revision 必须订阅 State，并在 LoadComplete replay 当前值；只重写 Show 会出现主菜单打开空页、没有来源请求。
- MainMenu 比异步下载 overlay 更早注入；可选 overlay 参数可能永远缓存 null。三个入口通过游戏当前实例的 `waitForReady`，不能捕获早期空值。
- `Button.Action` 赋值会更新 Enabled。不可下载按钮先赋 Action，最后设置 Enabled；顺序反过来会把缺包和 bmson/24K/48K 按钮重新启用。
- 全屏 Loading 层只用于清空结果的初次查询。追加页保留卡片操作，只禁用加载更多；单测在下一页回应挂起时点击实际下载按钮，避免已有结果仍被等待层吞掉输入。
- `OsuGameTestScene` 的步骤执行时才创建新 Game。`AddStep(..., overlay.Show)` 会在构造步骤时捕获上一实例；使用 `() => overlay.Show()`，否则看似依赖注入错误，实际访问旧 overlay。

## 来源与包格式

- 616 `song_name` 是显示名称，不带包扩展名；暂存固定名称，交给实际 ZIP/RAR/7z 签名识别。不能依据该名称选解压器或拼下载 URL。
- 616 `/preview` 返回原谱字节，`song_preview_url` 是外部看谱链接，都不是音乐试听。Ginger `shardMD5` 也不是目标谱面或压缩包校验值。
- Ginger 已知文件名/键数可提前提示不支持；616 缺资料时用实际原 MD5 与 classic 解码判断，不从标题猜键数。只有实际入库身份能显示可玩。

## 任务收尾与导航

- `InvalidDataException` 直接继承 SystemException，不属于 IOException；坏 JSON/归档的可恢复失败必须明确列入对应边界，否则任务/UI 异步方法会异常退出而没有失败状态。
- 在任务集合锁内登记 Completion，终态在释放流、独占暂存和队列资源后发布；退出先 cancel + join，再释放 Realm。否则可能漏等任务或让重试替换仍在收尾的所有者。
- Realm LINQ 不支持本链 `Ruleset.ShortName` 嵌套属性比较。沿既有字符串 RQL 查询；`BeatmapManager.QueryBeatmap` 已返回 detached BeatmapInfo，不能再当 Live 调用 PerformRead。
- 打开选中的原谱用实际持久化 GUID，导航最终回调重新查询可用记录；旧 PresentBeatmap(set, predicate) 会在不命中时回落其它难度，不满足精确选歌。实际 Player 期间只通知，不自动切歌或借完成点击退出游玩。
- 精确选歌必须等 carousel 初次呈现、过滤完成与选择 debounce 后再核对 GUID。真实多谱包暴露两条初始化竞态：先推荐再重映射 GroupedBeatmap 会因旧/null 分组换难度；明确 Present 后 FilterControl 又把 None 改为 DifficultyTable，criteriaChanged 根组重置会清空目标。推荐前按原 GUID 重映射，明确跳转的首次呈现窗口跳过根组 reset；正常进入/用户后续改组继续原合同。主菜单 LoadComplete 要向选歌传递明确目标；等待过滤或 Realm 新项通知时保留目标 GroupedBeatmap，不能后来覆盖玩家手动选择。
- 原 fixture 只选 ANOTHER，恰与自动推荐一致，会掩盖上述换歌。正反目标都要检查，并核对 carousel 与全局选择一致；单纯把对象复制成 Clone 不能复现此问题，CheckModelEquality 已有 BeatmapInfo GUID override。下载打开明确选原生 BMS，不能沿当前 mania 的转谱显示保持模式。
- RealmAccess 会创建空 `files/` 目录。直接 chartbms 验证应检查没有 blob、RealmFile 或 set.Files 引用，不能仅以目录存在判定误走通用存储。
