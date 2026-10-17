# IIDX 本地素材索引

读取用户提供的现有 HDD 美术目录，建立来源清单，并在独立非系统盘目录中处理 IFS 副本。原始图、原始 XML / AFP / 几何数据和按 UV 裁切的 PNG 分别保留；`index.html` 可离线搜索、筛选和预览。

## 使用

在 OMS 仓库根目录的新 PowerShell 中运行。Python 3.11 以上；依赖安装到本仓开发缓存，不安装进游戏目录或系统 Python。

```powershell
. .\UseDevelopmentStorage.ps1
python -B -m pip install --target .dev-cache/tools/iidx-assets --no-cache-dir --no-compile -r tools/IidxSkinAssets/requirements.txt
python -B tools/IidxSkinAssets/build_library.py --game-root 'X:\IIDX-HDD' --output 'Y:\IIDX-Art-Library' --ifstools-path .dev-cache/tools/iidx-assets
```

`--output` 必须尚不存在，父目录须已存在；输出不能与整个游戏根目录重叠，也不能落到系统盘。失败时保留已有输出供诊断，`run.json` 仅在整次成功后标记 `complete`，不覆盖或猜测续接。

`selection-33-hdd.json` 是当前 33 HDD 的首批取材清单：27～33 编号框体、00 号共用素材变体与当前界面共用件。它不是历代完整覆盖声明；`00` 等素材编号不能解释成代数。新增当代 HDD 后先核对实际目录，再提供对应选择文件，不猜旧版本的结构。

## 输出与验证

- `source-inventory.json`：只扫描 `contents/data/graphic` 与非 `_cache` 模组的 `graphic`，不读取账号、卡号、游戏配置、曲谱或声音。
- `source-copies/`：经过 SHA-256 对照的 IFS 副本，第三方解包器只接收这些副本。
- `packages/<原相对路径>/raw/`：完整解包文件，保留纹理外沿及动画关联清单。
- 同包 `sprites/`：由 ifstools 按原 `uvrect` 解出的有效纹理，不做缩放或重绘。
- `library.json`：每张图的包来源、原始/UV 矩形、精灵尺寸、图片摘要；相同内容只登记，不删除或跨代替换。
- `run.json`、`packages.json`：完成状态、工具版本与每包回执。成功前核对所有 PNG、有效尺寸、源包摘要及整个美术清单的文件名/大小/最后写入时间。

原版素材、精确源清单与生成库只留本地忽略目录或仓库外，不加入 Git、内置静线或公开发行。工具本身不启动游戏、不安装注入器、不重打包 IFS、不操作 OMS 玩家数据。

需要在内置浏览器预览时，可从素材库启动只监听本机的 HTTP 服务；该浏览器不允许 `file://`，不调整其安全策略。预览服务只指向生成库，不指向 HDD 或整个工作区。

```powershell
python -B -m http.server 8768 --bind 127.0.0.1 --directory 'Y:\IIDX-Art-Library'
```

当前授权、作品范围与下一步以 [P1-A 计划](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md)和[约束](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)为准；解包成功不代表动画、布局或游玩效果已验收。
