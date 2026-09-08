---
name: reference_skin_windows_handle_capture
description: Windows fixed-handle/no-follow capture、8.3 alias、inventory 与非 transaction 边界
metadata:
  node_type: memory
  type: reference
---

# Windows handle capture 地雷

native capture 资格/预算与完整 proof 合同见 [P1-A CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)，当前能力读 [STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)。声明前置见 [[reference_skin_filesystem_authority_preflight]]。

## 名字通过不等于物理身份固定

- managed/external request 校验 private issuer，但 request 不是文件或 mutation capability。native adapter 将 QueryDosDevice 限为 exact \Device\HarddiskVolume<uint>，从 NT volume 逐段 handle-relative 打开；SUBST、remote/mapped drive、shadow/device alias 不进入该路径。
- OBJ_DONT_REPARSE + FILE_OPEN_REPARSE_POINT；FileIdExtdDirectoryInformation 枚举在 native 循环内先检查取消/entry budget，再增长 managed集合。
- 名称两侧 NFC 后按 Windows ordinal-ignore-case 匹配；拒绝未展开的8.3/alternate alias。resolver 可能已把存在的短 data-root 展开成长名，不能因此让 native层接受未展开 alias。
- 每节点 proof 含 volume serial + 128-bit file ID、kind、attributes/reparse、time/length/link/delete状态；文件单link，identity全包唯一。文件只 share read，writer/busy handle 触发拒绝。
- 只读 share能阻止冲突 write/file rename，不能封住目录 namespace。新 child 仍可能出现，须由 final inventory 拒绝；不能称为目录树冻结。

## Capture 与 CaptureHeld 的差别

- 读前 pin 全目录/文件，通过 non-owning handle stream拷 bytes，分块维持取消响应；完成后复核 pinned metadata、inventory、authority link/package root。
- Capture 关闭 handles后只返回 immutable capsule；CaptureHeld 返回 caller-owned session，proof必须持到 final validation/commit。不能把一次性Capture的提前关handle要求套到Reload/selection。
- typed失败、取消、意外异常乃至单handle Dispose失败，都要继续释放其余handle与provisional capsule；不返回半成品或隐藏live callback。
- final check后的源变化不影响已复制bytes，但这不是跨文件系统transaction snapshot；同长度读取是否稳定由native proof而非capsule长度保证。

## Move 不是 capture 的普通延长

- fixed staging 只来自既存held staging root下operation固定direct child；source需要DELETE|SYNCHRONIZE与share delete，两侧root/source同卷，首move前durable receipt。
- NTFS要求preflight后释放descendant handles，保留source/parent/root，再move并以CancellationToken.None完整target recapture；不能靠放宽share或path reopen绕过。
- DELETE access可合法推进provisional父目录项时间；父inventory比name/identity/kind，source pinned metadata仍exact。只放行明确rename-related root时间，不放宽descendant/content。
- move尝试后的取消、tree差异和恢复歧义统一去 [[reference_skin_managed_folder_mutation_foundation]]。

SUBST的历史证据只覆盖volume-target classifier/fake alias，不能宣称已跑真实SUBST命令integration。capture成功也不验证InstantiationInfo、选择资格或publication；后续走 [[reference_skin_package_revision_capsule]]、[[reference_skin_managed_folder_selection]]。
