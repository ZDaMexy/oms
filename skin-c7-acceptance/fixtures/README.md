# 原创视频观察素材

`viewport.mp4` 是本仓库生成的白边、移动蓝带原创观察视频，适用仓库 MIT 许可。玩家和验收包组装直接使用它，无需下载或配置 ffmpeg；现有 BGA 播放器直接读取 MP4。它不改变播放器、背景区域规则或原 V-001～V-005 的未签收状态。

视频为 H.264 Constrained Baseline、yuv420p、96×54、10 fps、6 秒。实际解码得到 60 帧且每帧内容不同。成品 SHA256 为 `c2654af16841a1f01bcbcd9f33ce70de7d7c46af256c8ad32ce22bb1aaebdaee`。详情见 [实际制作证据](viewport-evidence.json)；这份证据不声称游戏 GPU 或实际设备已签收。

重做原创 AVI 源视频不需要额外工具。在 `skin-c7-acceptance/` 运行以下命令，输出目录必须不存在：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Generate-ViewportVideo.ps1 -OutputDirectory D:\video-source-new
```

需要重新编码 MP4 时，使用 [FFmpeg 官方下载页](https://ffmpeg.org/download.html)指向的 [Gyan Windows 构建](https://www.gyan.dev/ffmpeg/builds/)，按同页 SHA256 校验归档，再明确指定本地工具。工具只用于离线制作，不随验收包运行或分发。

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Generate-ViewportVideo.ps1 -OutputDirectory D:\video-remake-new -FfmpegPath D:\ffmpeg\bin\ffmpeg.exe
```

脚本保留完整原始像素生成与转码参数；同一工具版本的实际重复制作得到相同字节。更换工具版本可能改变 MP4 字节，更新随包素材前必须重新解码核对帧数、移动内容、格式与摘要，并同步生成器的固定素材校验。
