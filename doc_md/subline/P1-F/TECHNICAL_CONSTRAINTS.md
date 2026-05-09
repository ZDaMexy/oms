# P1-F 技术约束：发行后置与离线发布验收

1. 在 Phase 3 前不得借发行验收之名重新打开在线更新、默认 endpoint 或终端联网入口。
2. 便携发布、覆盖更新与离线首发口径必须与 `../../other/RELEASE.md` 保持一致。
3. 任何改变发行方式、覆盖更新结论或公开 release gate 的改动，都必须同步更新本目录四件套与 `../../mainline/`、`../../other/RELEASE.md`。
4. 当前正式发行压缩包命名以 `build-release.ps1 -> release-repo/oms_YYYYMMDD(.zip)` 为准；不要继续把现状写成泛化的 `OMS-Portable.zip`。
5. 覆盖更新说明必须显式保留 `portable.ini`、便携模式下的 `data/`，以及任何自定义数据根使用的 `storage.ini`；也不要把当前布局误描述成“严格只有一个 exe”。
6. 若后续改变内部 `game.Version` 口径，发行线变更不得破坏 changelog 跳转或配置迁移对非上游 `版本-流` 字符串的兼容性。