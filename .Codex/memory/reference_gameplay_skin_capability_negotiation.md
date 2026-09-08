---
name: reference_gameplay_skin_capability_negotiation
description: Skin V1 capability negotiation、hard-deny classifier、只读 event 命名与 authority handle 地雷
metadata:
  node_type: memory
  type: reference
---

# gameplay skin capability negotiation 召回

权威状态与硬约束见 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md) / [CONSTRAINTS](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)；本文件只保存实现地雷。

## 当前通用脚本权限合同（C6尚未接入production）

- grant 必须同时满足 explicit request、closed allowlist、host feature available、当前 skin policy authorization（若需要）且未命中 hard deny。request/support/authorization 任一单项都不产生权限；unknown 不动态注册。
- `GameplaySkinCapabilityNegotiation` 只是 immutable decision snapshot，不是 service/delegate/authority handle。future host API 仍须逐调用 gate；重新协商表达 authorization revocation/feature removal，但不证明旧 scene/script 已原子停用。
- hard-deny表和reserved classifier是closed allowlist后的第二屏障，不穷举任意同义词。通用脚本权限仍无production request/authorization、per-skin授权identity/持久化/UI、required/optional/explicit-deny、activation/version或sandbox runtime。C5已生效的`GameplaySkinRuntimeSupportProfile`、package identity与manifest/scene/event版本是不同合同，不在此未实现范围。
- ID只能是非敏感lowercase ASCII opaque token。此处通用脚本权限carrier/diagnostic/JSON不是manifest、持久化或script ABI；future parser须做ID length、request count与package budget，不能把包名、用户值或路径塞进ID。

## classifier 地雷

- 不能扫描 gameplay ID 的任意 action segment 来判断 mutation。`Reset` 已是 event envelope 的正式 delivery kind；`gameplay.lifecycle.reset.read`、`gameplay.event.seek.read`、`gameplay.event.score-update.read` 都可能是合法只读能力。
- 当前安全形态是：明确 deny root 的 exact/descendant 永远拒绝；其它 gameplay ID 只把 terminal mutation action 判为 hard deny。只读 fixture 用 terminal `.read` 区分，action 名可以出现在前序 segment。
- `gameplay.layout.write` 禁止的是 runtime gameplay authority，不禁止 schema-validated declarative geometry。`host.filesystem.arbitrary` 与 package-scoped resource read 也必须分开。
- aggregate 必须拒绝 hard-denied grant、同 ID grant+deny、duplicate denial 和 hard-deny code/ID 不一致；BMS 是 IVT friend，不能只相信“正常 negotiator 不会生成矛盾值”。

## 验证地雷

- public-surface fixture 要同时锁 request factory 仍为 internal，以及 definition/policy/negotiator/hard-deny catalog 不公开；只查 property/constructor 不够。
- `dotnet format --include` 曾把测试实际使用的 `System.Reflection` 判为 unused 并删除，随后出现 `CS0103 BindingFlags`。对新未跟踪 fixture 必须立即编译 owning test project；必要时使用全限定类型绕过误删。

精确验证数字与当前接线状态只看 P1-A STATUS/CHANGELOG，不在 memory 重抄。
