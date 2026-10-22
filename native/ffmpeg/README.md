# OMS 进程内视频依赖

此目录交付Windows x64游玩/故事板视频实际使用的四个共享FFmpeg库。版本、固定源码与公开签名以[`source.lock.json`](source.lock.json)为准，实际二进制以[`runtime-manifest.json`](runtimes/win-x64/native/runtime-manifest.json)为准；不能用外部转码器`ffmpeg.exe`的版本代替它们。

当前软件结果与剩余门只取[P1-L状态](../../doc_md/subline/P1-L/DEVELOPMENT_STATUS.md)，本轮候选的原configure与后续静态运行库重链历史见[日期记录](../../doc_md/subline/P1-L/CHANGELOG.md#2026-10-22进程内ffmpeg原生依赖升级)。该候选不是按最终配方从零重编或逐位可复现的证明。

## 文件职责

| 路径 | 用途 |
| --- | --- |
| [`source/`](source/) | 官方签名发布源码、公开key/签名、固定维护补丁的完整对应来源 |
| [`prepare.py`](prepare.py) | 核对摘要/签名、受限解包，并按顺序零fuzz应用补丁 |
| [`build.sh`](build.sh) | 使用全新work/output编译四库；不修改NuGet缓存 |
| [`abi-probe.c`](abi-probe.c)、[`binding-abi.json`](binding-abi.json) | 当前框架实际消费字段与AutoGen布局基线 |
| [`verify_native.py`](verify_native.py) | 检查四库身份、导出、输入协议、导入依赖及原生/托管布局 |
| [`runtimes/win-x64/native/`](runtimes/win-x64/native/) | 受版本控制的必要DLL、FFmpeg许可与二进制manifest |
| [`FFmpeg.Native.targets`](FFmpeg.Native.targets) | 替换SDK资产，保留NuGet/RID/目标路径并旁带对应源码与许可 |
| [`SOURCE-NOTICE.txt`](SOURCE-NOTICE.txt)、[`notices/`](notices/) | FFmpeg及实际编译器运行库的完整许可与来源SHA |

## 源码构建与核对

在F盘仓库内的Linux工具链运行；需要GNU coreutils的`realpath -m`、Python≥3.12、GnuPG、GNU patch、bash、make、nasm和MinGW-w64交叉GCC。当前所用工具链版本见[`notices/provenance.json`](notices/provenance.json)。`prepare.py`使用隔离公钥库，禁用个人options、agent自动启动与自动抓取密钥；子命令限30秒，失败保留返回码和有界stdout/stderr诊断。全新公钥导入及正/反签名检查已通过，完整从零原生重编仍未重新执行，不能将下列示例当作一次新构建已验收。

先把随仓源码复制到可写缓存，避免把keyring、解包内容或编译输出写入受版本控制的`source/`：

```bash
mkdir -p .dev-cache/native/ffmpeg-source-cache
cp native/ffmpeg/source/* .dev-cache/native/ffmpeg-source-cache/
bash native/ffmpeg/build.sh \
  .dev-cache/native/ffmpeg-source-cache \
  .dev-cache/temp/ffmpeg-rebuild-20261022 \
  .dev-cache/native/ffmpeg-rebuild-20261022/win-x64
```

work/output必须不存在；已有缓存仍会重新核摘要和签名。不要覆盖旧证据目录或通过删除现有目录强造“干净”。`-static -static-libgcc`仅静态链接编译器支持，`-l:lib%.dll.a`保持FFmpeg四库之间的共享依赖，不增加第五个运行库DLL。

在Windows x64 Python中验证新输出，使用同一编译头文件生成的探针：

```powershell
. .\UseDevelopmentStorage.ps1
python native/ffmpeg/verify_native.py `
  .dev-cache/native/ffmpeg-rebuild-20261022/win-x64 `
  --abi-probe .dev-cache/temp/ffmpeg-rebuild-20261022/abi-probe.exe `
  --write-manifest .dev-cache/native/ffmpeg-rebuild-20261022/win-x64/runtime-manifest.json
```

`--write-manifest`拒绝覆盖；核对现有输出时可用`--check-manifest`。源码相同不保证不同构建的文件摘要相同；审核新四件套、许可与manifest后整体替换，不能单换不同主ABI的DLL或放宽验证器迁就缺失依赖。

## 集成与验收

仓库[`Directory.Build.targets`](../../Directory.Build.targets)导入本目录targets。普通portable build与显式`win-x64`输出必须都只携带补丁四库；新输出同时检查实际DLL摘要与`.deps.json`的RID/相对路径。显式其它RID/x86/ARM与缺件失败，不回退到NuGet旧媒体库；BASS不由此配方升级。

运行`FFmpegNativeDependencySecurityTest`核实际框架加载路径/摘要与布局，再按P1-L运行共享BGA的播放、暂停、seek/reset、POOR、布局及释放回归；共享更新按主线矩阵补充验证。通过软件测试不代签逐谱保真、GPU帧时或实体设备体验。

所有包含此依赖的可执行输出都应保留`licenses/ffmpeg/`下的对应源码、构建配方、签名和完整运行库许可，single-file工具也旁带该目录。正式发行由既有打包入口另行验收；本目录和本次安全修复不授权生成Windows发行包。
