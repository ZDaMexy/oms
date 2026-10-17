# IIDX 本地素材与作品工具

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

## 生成本地 33 游玩作者包

`build_skin.py` 消费已核对的独立素材库和 AFP 研究输出，保留原图字形与帧序，生成普通公开作者包及相邻 `.osk`。研究输入须包含 `skin-reference-map.json`、`gameplay-assets.json`、`extra-note-library/library.json` 与过滤后的 `static-frames/`；另以 `--fullcombo-research` 指向含 `fcombo00-author-data.json` 的全连研究目录。全连使用原纹理、源帧时间与透视四边形，不烘焙整屏序列。这些来源资料和美术同样只保留本地，不由脚本从游戏安装根读取。

```powershell
. .\UseDevelopmentStorage.ps1
python -B tools/IidxSkinAssets/build_skin.py --library 'Y:\IIDX-Art-Library' --research 'Y:\IIDX-AFP-Research' --fullcombo-research 'Y:\IIDX-FullCombo-Research' --output 'Y:\IIDX33-Author-Candidate' --image-tools-path .dev-cache/tools/iidx-assets --game-root 'X:\IIDX-HDD'
dotnet run --project tools/SkinAuthoring -- check 'Y:\IIDX33-Author-Candidate'
```

输出目录及 `.osk` 均须尚不存在，不能与输入、受保护的游戏根重叠，也不能落系统盘。资源在生成后检查 PNG、总像素和资源数；`generation.json` 只有整包写入成功后标记 complete。作者检查通过仍须正常导入 OMS 并观察真实 Player；`local-study.json` 和 `READ-ME.txt` 明确记录当前还原缺口，不能把生成成功当成完成作品。

使用包含本次作者能力的 OMS 工作区导入 `.osk`，切到 BMS 并选择该皮肤。7K 在 BMS 设置“游玩区域样式”选择“1P（居左）”或“2P（居右）”，14K 使用双侧布局；默认居中的 7K 和其它键数保留 canonical 材料。生成器不会修改玩家的布局偏好。当前作品只覆盖游玩，选歌与结算页面仍沿原 OMS。

## 通过隔离副本采集原版参照

原始可玩安装保持只读。运行参照使用另一处完整物理副本；启动前核对路径，独立保存配置、补丁选择、截图和日志，结束后复核源安装清单及关键文件摘要。副本清空卡号与自动插卡设置，不使用真实卡或账号；配置和 API 密码只存本地忽略目录，不进入 Git 或命令输出。

`reference_api.py` 只连接会话指定的 `127.0.0.1` Spice API，要求非空独立密码；提供截图、按钮、模拟轴、触控及投币，不提供卡、内存或电源操作，也不负责复制或启动游戏。Spice API 服务自身的监听范围须在启动配置中另行核对，本客户端的 localhost 限制不能证明服务只监听回环。

会话 JSON 包含 `host/port/password/captureRoot`。截图仅允许在 `captureRoot` 内新建，不覆盖已有图片；持续导航可用 `with ReferenceApi(session_path) as api` 复用连接，避免每次输入都弹出连接通知。

```powershell
. .\UseDevelopmentStorage.ps1
python -B tools/IidxSkinAssets/reference_api.py --session 'Y:\IIDX-Reference\api-session.json' screens
python -B tools/IidxSkinAssets/reference_api.py --session 'Y:\IIDX-Reference\api-session.json' capture --screen 0 --output 'Y:\IIDX-Reference\captures\reference-001.jpg'
```

截图用于对位，连续帧辅助观察动画；普通运行日志不保证列出每次纹理绘制。素材关联仍需对照原 XML、AFP / GEO 和实际画面。副本为适配参考环境启用的启动检查调整必须单独记录，不作为原安装、设备或原版刷新率的验收结果。

需要在内置浏览器预览时，可从素材库启动只监听本机的 HTTP 服务；该浏览器不允许 `file://`，不调整其安全策略。预览服务只指向生成库，不指向 HDD 或整个工作区。

```powershell
python -B -m http.server 8768 --bind 127.0.0.1 --directory 'Y:\IIDX-Art-Library'
```

当前授权、作品范围与下一步以 [P1-A 计划](../../doc_md/subline/P1-A/DEVELOPMENT_PLAN.md)和[约束](../../doc_md/subline/P1-A/TECHNICAL_CONSTRAINTS.md)为准；解包成功不代表动画、布局或游玩效果已验收。
