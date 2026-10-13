# OMS 品牌资源

`oms-logo.svg` 是客户端圆形标识的唯一可编辑源稿。字形由路径组成，不依赖机器字体；白圈与透明留白对应现有粉饼控件，粉色底、三角形与频谱继续由客户端绘制。

`export-assets.mjs` 使用 Node.js 与 `sharp` 导出：

- `osu.Game/Resources/Textures/OMS/logo.png`：1024×1024 透明徽章，主体与节拍残影共用。
- 同目录 `logo-intro-background.png` / `logo-intro-highlight.png`：启动动画数据贴图；R 保存显现时间，G 保存覆盖率，不是普通彩色图片。
- `osu.Desktop/lazer.ico`：16～256 像素的九个 OMS 图标层；沿用文件名以保持现有窗口、文件关联和打包调用。
- `oms-badge.png`：同源完整徽章预览，粉色底与控件渐变一致，不作为游戏背景贴图。

安装了 `sharp` 的环境运行 `node assets/branding/export-assets.mjs`；也可通过 `--sharp-path <sharp模块路径>` 使用已有工具运行库。导出后提交源稿和生成物，普通 .NET 构建无需 Node.js 或 `sharp`。

当前资源接入和验证见 [P1-A 状态](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)。标识调整不改变 gameplay 皮肤包、数据根、协议或程序文件名。
