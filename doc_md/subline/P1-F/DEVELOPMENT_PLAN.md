# P1-F 开发计划：发行后置与离线发布验收

> 最后更新：2026-05-09
> 主线总规划见 [../../mainline/DEVELOPMENT_PLAN.md](../../mainline/DEVELOPMENT_PLAN.md)。

## 子线目标

- 统一承接拖放导入、桌面 UI smoke、覆盖更新与便携发布验收。
- 在不提前恢复在线能力的前提下完成离线发行基线收口。

## 当前执行顺序

1. 维持 `build-release.ps1 -> oms_YYYYMMDD(.zip)` 的 portable 发布与覆盖更新基线稳定，并保持 `portable.ini` / `data/` / `storage.ini` 注意事项口径一致。
2. 在 `P1-A` 收口后继续执行公开发行物产品面验收。
3. 将可对外声明的发行口径同步到 `../../other/RELEASE.md`。
4. 若后续把内部版本串切到 OMS 版号，继续维持 changelog / migration 对非上游 `版本-流` 字符串的兼容。