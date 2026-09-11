# 原 V-001～V-005 固定验收输入

这里保存 2026-09-09 已存在的原验收文件，2026-09-10 按原字节复制纳入仓库；没有重新制作、修改原清单或签收任何项目。

| 文件 | 原用途 |
| --- | --- |
| `bms-note-animation-manual-gate.osk` | V-001 原 60 帧普通短键包 |
| `bms-note-animation-manual-gate-broken.osk` | V-001 原缺第零帧坏包 |
| `bms-note-animation-manual-gate/bms-note-animation-manual-gate.bme` | V-001 原 BMS 观察谱 |
| `oms-complex-c6.osk` | V-005 原 Momentum C6 候选；仍借用基础视觉，不是 C7 星轨成品 |
| `SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md.snapshot` | 原集中清单的原字节快照；V-001～V-004 0/4、V-005 未签收 |
| `original-v001-SHA256SUMS.txt` | 原 V-001 生成目录的原始摘要表；其中路径沿用原目录布局 |
| `SHA256SUMS.txt` | 本目录固定文件的逐文件摘要，供发行与组装校验 |

原文件分别来自仓库工作区的 `artifacts/manual-gates/bms-note-animation/`、`artifacts/skin-c6/oms-complex-c6.osk` 和 `doc_md/other/SKIN_V1_VISUAL_ACCEPTANCE_CHECKLIST.md`。原制作入口和作者源文件仍保留在仓库；发行物与验收包使用本目录，重建时不再依赖未提交的开发产物。

请保留本目录原件，仅导入验收包 `import-copies/` 中的副本。离开仓库后的操作入口见验收包 `README.md` 和 `CHECKLIST.csv`；原清单中指向开发源码、历史或生成命令的链接继续保留原样，不因此改变原验收语义。

原清单以 `.md.snapshot` 标明不可变的原始证据；内容字节不变，不作为新位置的现役文档维护。组装后仍以 `V001-V005-原验收清单.md` 提供阅读，现役仓库清单继续位于原 `doc_md/other/` 路径并接受完整文档检查。
