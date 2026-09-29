---
name: reference_bms_skin_editor
description: "原布局编辑器的独立草稿、预览资源与延迟应用边界；CLR 反射构造地雷"
metadata:
  node_type: memory
  type: reference
---

# 原皮肤编辑器恢复召回

当前事实见 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)，安全合同见 [G1](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#g1选择ui与startup协调)，作者边界见 [SKINNING](../../skin-authoring/docs/SKINNING.md#1-选择合适的制作方法)。

- 2026-09-14 用户要求还原原编辑功能，暂不扩展。入口恢复设置、菜单及 `Ctrl+Shift+S`；仅编辑 `SkinnableContainer` 内的 `ISerialisableDrawable`，不是完整 scene/script 或车道素材图形编辑器。BMS/mania 公开作者文件能力不受该范围限制。
- 不能只把 `LegacyEditorAvailable` 设为 true：旧 `EnsureMutableSkin` 会立即把副本选成 current，而 `Save/AddFile` 当前记录防护随即拒绝。应完整复制独立非 current Realm 草稿，原控件只序列化到草稿，保留 current 通用修改禁令。
- `RulesetSkinProvidingContainer` 把编辑器自建 `EndlessPlayer` 也登记为 live host；关闭编辑 UI 不等于 host 已退出。仅退出自己创建的预览，待参与者允许后正常选择保存副本；真实游戏不强退。后来选皮肤、换模式或重开编辑优先于旧待应用副本。
- `CreateEditorDraftAsync` 在 manager 跟踪的 worker 中复制完整包；控件出现前先等待 `DraftReady`，不能把整个包复制搬回 `LoadComplete`。关闭时取消准备，未安装或未保存草稿交由 manager 清理；正常无编辑开关不能切换或累积副本。极端退出期数据库已停止时仅释放资源，可能留一个未使用副本，不保证退出后仍可写库清理。
- “恢复默认”会异步重建目标组件，紧接着关闭必须等待 `CanFinishEditing` 再保存，不能因 `ComponentsLoaded=false` 跳过保存后销毁草稿。待应用使用 overlay 自身 scheduler，`IsPresent` 须覆盖待执行任务；仅关闭可见 UI 不能停止该收尾。
- 新导入图片必须从草稿资源预览；只把文件写入草稿、仍从当前皮肤读取图片，会出现“保存了但看不见”。退出与切换编辑目标时要移除临时资源覆盖并恢复屏幕组件。
- toolbox 和 layout reload 都依赖公共无参构造：全可选参数构造不等于无参构造。`GetAllAvailableDrawables` 的 `GetConstructor(Type.EmptyTypes)` 过滤要保留，避免 `Activator.CreateInstance` 抛出 `MissingMethodException`。
- 外部编辑 mount/update-import 仍禁用，原 layout JSON 不能当作 Skin V1 scene ABI；新 scene 使用稳定节点标识与 allowlist。

- exact package owner 的 SkinInfo.Files 有意不暴露 Realm 文件引用；验证导入图片持久化时重新 GetSkin 后读取实际资源 bytes，不能以 Files 为空判定丢文件。
