# OMS Momentum C6 候选

同一普通包支持 BMS/mania。右侧 Momentum 装饰由真实 judgement 事件维护最近八次判定间隔，并结合 gauge、gameplay time 和可选确定性随机数改变大小、透明度、旋转及进度条。这种跨事件的滚动历史组合由可编辑脚本完成。

1. 在仓库根运行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File doc_md/other/skin-c6-candidate/Build-Candidate.ps1`，得到 `artifacts/skin-c6/oms-complex-c6.osk`。
2. 将 `.osk` 拖入 OMS，Settings → Skin 选择 **OMS Momentum C6 [oms-complex-c6]**（现有 importer 会附加与皮肤名称不同的压缩包文件名；目录来源显示 OMS Momentum C6）。先保持未授权进入 BMS/mania，普通 note/key/judgement 应可玩，装饰维持静态。
3. 在 **可选皮肤脚本** 授权三个 required 能力；random 是 optional，可先拒绝。进入任意可玩 BMS/mania 谱面，实际命中后观察右侧组合效果。
4. 查看 profiler；撤销 scene 写入应恢复静态装饰，继续可玩。暂停后效果时间冻结，seek/retry 后历史重建。退出 gameplay 后才能手动 Reload。
5. 编辑 `gameplay-skin.script` 或场景、重新打包/Reload；新内容需要重新授权。相同内容重启应保留决定。可用相同五个作者文件建立 managed 目录或注册 external 目录验证；external 的任何原文件都不会被 OMS 改写。

`Build-Candidate.ps1` 以固定 ZIP 时间和确定性白色 tile 生成普通包，同时编译并验证 `gameplay-skin.bytecode`，供作者选择预编译分发。语言、权限、预算和诊断见[脚本作者说明](../SKIN_SCRIPT_V1_AUTHORING.md)。

这是 C6 可导入候选，继续使用现有普通基础视觉回退，不是 C7 canonical `oms-complex`，不接管默认皮肤。[V-005 集中验收项](../SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md#v-005c6-可选脚本与双规则集-momentum-候选)尚未签收，C6 自动证据也不代表 Skin V1/release 完成。
