---
name: reference_mania_sayobot_download
description: Sayobot mania 原游标、混合包假阳性、原sid/bid身份与实际下载端口的接入诊断
metadata:
  node_type: memory
  type: reference
---

当前能力见 [P1-A STATUS](../../doc_md/subline/P1-A/DEVELOPMENT_STATUS.md)，稳定边界见 [Sayobot合同](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md#sayobot-mania-浏览下载)，取证/验收见 [闭环记录](../../doc_md/other/MANIA_SAYOBOT_DOWNLOAD_20261001.md)。共享卡片、真鼠标及精确选歌地雷仍查 [BMS下载记忆](reference_bms_builtin_download.md)，不复制合同全文。

## 来源与筛选诊断

- 列表mania是mode位8，详情原玩法是mode3；两者不可混用。服务端列表按集合筛选，4K与低星可能分别命中不同难度，甚至低星来自其它玩法。须在详情的同一原生mania难度上同时匹配，不能只信列表命中。
- `endid`不是页码或结果数，是下一次offset；0可以配非空末页。`results`是关键词原总数，不等于筛后数。过滤后空页仍可能有下一游标，按空页提前结束会漏谱。
- 实站无匹配`status=-1`可能仅有status，也可能附空data/endid=0。搜索结果中删除的集合可跳过；玩家直接输入的数字sid若缺失应失败，不回落曲名。纯数字0/超Int32来自玩家文本，不能抛未接住的参数异常逃出async void；键数/枚举等内部违约仍明确暴露。
- 元数据/封面/包都须站方要求的OMS User-Agent与发布地址Referer。实站资源入口443先观察到`tc1.sayobot.cn:25225`，实际GET后来切到tc2同端口；只列一个观察节点仍会误拒合法路由，整个sayobot域通配则越界。精确列举已确认authority，不扩大未观察主机。声明CDN或不同server键不保证实际换节点，必须看真实Location/TLS，不能用参数名声称已修好线路。
- 已处理的读取/下载失败调用Logger.Error会由OsuGame.forwardGeneralLogToNotifications再弹英文诊断；只把target改Network仍会转发。用既有Logger.Log的Network/Verbose保留完整异常与业务上下文，并保留页面/中文通知；Debug默认可能不留诊断。UI断言只检查本来源重复提示，不把其它测试迟到的难度计算日志当成当前查询错误。
- ppy.osu.framework 2026.303.0的TextureStore.GetAsync只是Task.Run(Get)；同URL未完成时第二次取图会在后台走WaitSafely并报错。资源HTTP层合并或直接换LargeTextureStore不能修复。BeatmapDownloadCoverStore合并底层取图，每卡WaitAsync自己的token；底层任务未完成时，旧卡取消不能删pending或Dispose共享Texture。普通TextureStore返回共享Texture，LargeTextureStore才有独立引用计数wrapper。
- 图片owner释放先同锁停止新查询/快照，再锁外取消source读取，join底层取图后释放loader。TextureLoaderStore.Get的宽泛catch会把源OCE变null，因此await之后须针对真实owner退出复核，保持取消。慢图筛选重建与两个不同URL实际读取取消各有专项；不能只验证等待者Task结束就认为网络撤出。
- UI下递归查询任意Sprite.Texture会误命中文字的字形Sprite，不能证明歌曲封面已加载；应精确定位填满封面区域的Sprite，并看原生截图。SpriteIcon本身不是Sprite，两者不能用类型模式排除；同理不能把难度/操作图标的可见性当作封面占位图的可见性。

## 原难度与入库诊断

- 镜像详情不含原MD5，不能用标题或虚构hash决定目标。下载前保存sid/bid，发布前核对原.osu元数据及完整解码，然后核对实际持久化MD5与本地Guid。
- ManiaFolderImporter持久化beatmap OnlineID=原bid，但本地集合OnlineID仍-1；下载完成必须用返回的bid→Guid映射，不能按set OnlineID寻找源sid。混合.osz里其它模式可以保留原文件资源，但不索引/开放已删除玩法。
- 目标验证要先于既有目录Import；仅在入库后检查，会把缺失/坏目标包先发布其它难度，再显示失败。重复复用后还要看目标实际文件，不能仅凭旧Realm快照声称原谱仍可用。
- 测试中的Decoder与System.Text.Decoder同名，使用明确alias。visual fixture的Visibility位于Framework.Graphics.Containers，不能只导入Graphics。原窗口探针先等鼠标动作登记任务，再读GetTask；等过滤/选择延迟稳定后核对实际carousel与全局Guid。
