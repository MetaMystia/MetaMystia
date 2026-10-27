# ResourceEx 白天地图

资源包在 `ResourceEx.json` 的 `dayMaps` 数组声明地图，图片和音频放在同一 ZIP。Mod 从配置创建独立 `DaySceneMap`，通过内存 Addressables Provider 交给游戏原有切图流程实例化。

当前格式为画布地图（`formatVersion: 3`），不兼容旧版本：美术是整张大图，碰撞、坡度、出生点与相机单独描述。地图美术、碰撞与坡度的制作方法见 [`map-creation/`](map-creation/README.md)；在线编辑见 MetaMystia ResourceEx Editor 的“白天地图”页。

## 使用

安装 ZIP 后重启游戏，在单机白天自由活动时使用：

```text
/resourceex map list
/resourceex map goto 1073742000
/resourceex map goto 1073742000 East
/resourceex map back
```

`goto` 不扣行动点。首次进入时记录原地图的一个有效出生点，`back` 返回该点；不是恢复原来的精确坐标。返回记录仅在当前白天场景有效。地图不解锁到世界地图面板，不创建出口，也不挂靠原有区域。联机连接期间禁止这些指令。

地图 ID 参加现有资源包 ID 范围与签名校验。运行时标签为 `_ResourceEx_Map_{id}`，出生点为 `_ResourceEx_Map_{id}_{name}`。重复地图 ID 全部停用，避免由包扫描顺序决定地图归属。

## 与原版结构的对应

| 原版 | 画布地图 | Mod 生成 |
| --- | --- | --- |
| Tilemap 层（地面、前景） | `layers`：一张大图，带排序层、排序值与颜色 | 每个图块一个 `SpriteRenderer` |
| 带 `SpriteRenderer` 或 `SortingGroup` 的预制体 | `prefabs` 模板 + `objects` 实例 | 物件节点带 `SortingGroup`，按 Y 排序时加 `LayerSortingController`（`isStatic`），子节点为图块与碰撞 |
| `PolygonCollider2D`、`EdgeCollider2D`、`BoxCollider2D`、`CircleCollider2D`；`CompositeCollider2D` 轮廓 | `colliders`：`polygon`、`edge`、`box`、`circle` | 同名组件 |
| `HEIGHT_MAP` | `height.cells` | 隐藏 Tilemap，见“坡面高度” |
| `CameraCollider` | `camera.bounds` 多边形 | `PolygonCollider2D`，层 `Ignore Raycast` |
| `SpawnMarker` | `spawnMarkers` | 同名组件 |
| `mapBGM`、`hideClock` | `mapBGM`、`hideClock` | 同名字段 |

## 配置

| 字段 | 用途 |
| --- | --- |
| `id`、`formatVersion` | 整数地图 ID；格式版本为 `3` |
| `name`、`description` | 地图名称与描述 |
| `pixelsPerUnit` | 图层的像素密度，默认 `48`（原版地面同为 48）；世界坐标 = 像素 / `pixelsPerUnit` |
| `hideClock` | 可选，进入地图时隐藏时钟 |
| `layers` | 图层：`name`、`sortingLayer`、`sortingOrder`、可选 `color`、左下角像素坐标 `x/y`、`image` |
| `prefabs` | 物件模板：`key`、`image`、`pivot`、`pixelsPerUnit`、`sortingLayer`、`sortingOrder`、`sortByY`、`colliders` |
| `objects` | 物件：`prefab`、`x/y`、`scale`（负值翻转）、可选 `name` |
| `colliders` | 地图碰撞，世界坐标 |
| `height` | 可选坡度格，`cells` 中保存 `x/y` 和 `slope` |
| `spawnMarkers`、`defaultSpawnMarker` | 出生点：`name`、`x/y`、`rotation`（默认 `Down`）；不指定出生点时使用的名称 |
| `camera` | `shouldFollow`、相机中心范围多边形 `bounds`、固定相机 `position` |
| `mapBGM` | 包内音频 `intro`、`loop`，或原版 `LoopedBGMPackage` 的 Addressables 地址 `native`，二选一 |

所有资源路径均支持包内相对路径和 `rex://` URI。

### 图片

图层与物件模板共用切块图片结构：

```json
"image": { "width": 4529, "height": 4071, "dir": "dayMaps/1073742303/layers/1", "chunks": [[0, 0], [1, 0], [0, 1]] }
```

图片按 1024 像素切块，块 `[列,行]` 自图片左上角起算，文件为 `<dir>/<列>_<行>.png`；末列末行可以小于 1024。只列出非空块，全透明区域不占文件。图片边长上限 16384 像素。校验只读取 PNG 头部尺寸，不解码。

图层的 `x/y` 是图片左下角的像素坐标，整数。物件模板的 `pivot` 是左下原点的归一化锚点，可以超出 0..1；`pixelsPerUnit` 可与地图不同，原版部分光照与特效精灵为 100。

### 排序

图层与物件都使用原版排序层（`Default`、`Background`、`BelowCharacter`、`Character`、`Overlay`、`OnTop`、`EffectOverlay`、`UI`）。物件节点挂 `SortingGroup`，整组参与排序，与原版成组的预制体一致；`sortByY` 时由原版 `LayerSortingController` 在首帧写入 `(int)(-32×Y)` 后移除组件，`sortingOrder` 不生效，Y 坐标限制在 ±1023。

### 碰撞

| `shape` | 字段 | 说明 |
| --- | --- | --- |
| `polygon` | `paths` | 实心；多条路径时内层路径为洞（偶奇规则） |
| `edge` | `points`、可选 `closed` | 只有线段阻挡；`closed` 时首尾相连，内部仍可走 |
| `box` | `x/y`（中心）、`width/height`、可选 `rotation`（度） | 实心矩形 |
| `circle` | `x/y`、`radius` | 实心圆 |

原版地图的移动边界由 `CompositeCollider2D`（Outlines）合成，只有轮廓阻挡；用闭合 `edge` 表达等价。物件模板的碰撞使用模板局部坐标，作为物件的子节点创建，随物件缩放与翻转。

`camera.bounds` 是相机中心可移动范围，不是美术外框；`shouldFollow: false` 时使用 `position: [x,y,z]`。矩形范围可按 16:9、正交半高 7.5、相机 Y 偏移 +0.5 推算，其他画幅需要单独检查。

### 背景音乐

`mapBGM.native` 填原版 `LoopedBGMPackage` 的地址（如 `DLC3_HakureiFestival`），注册时检查地址存在，进入地图时加载、离开后释放；需要玩家拥有对应 DLC，资源包应在 `packInfo.dependencies` 声明。包内音频使用加载器支持的 WAV。

## 坡面高度

`height` 对应游戏 `DaySceneMap.height`，与显示图层、碰撞分开。省略、设为 `null` 或提供空 `cells` 均为平地。

`slope` 范围为 `[-1,1]`：正值表示向右上坡，负值表示向右下坡，`0` 或空格表示不修正移动。比如 `0.5` 表示仅向右移动时，横向每前进 1 单位，纵向约增加 0.5；向左则反向下降。每格内坡度恒定，最多 100000 格，不允许重复坐标、非有限数或越界坡度。

原游戏按格读取高度图精灵的像素：红色通道为坡度大小，绿色通道大于 0.5 为正。原版博丽大祭的 41 个坡度格每格颜色一致，按格保存不损失信息。Mod 创建不可见的 `HEIGHT_MAP` Tilemap 并赋给原生 `height`，把绝对坡度写入共用的 256×2 可读纹理，大小量化为 `round(abs(slope)×255)/255`，例如 `0.5` 实际约为 `0.501961`。

原游戏 `DayScenePlayerInputGenerator.UpdateCharacter` 在切图时重新绑定高度图，`HeightBlendedInputProcessorComponent` 根据脚下格子修正输入：`输出y = 输入y + 坡度×输入x`。剧情中和原输入 `y > 0.70700002` 时跳过修正。它不保存海拔、不创建三维地形，也不支持同一位置的桥上、桥下双层通行。

## 加载和校验

包加载时，`dayMaps` 引用的本包图片只登记字节，不预先解码。白天数据库初始化时先校验，再创建非激活、常驻的地图模板：渲染器、碰撞、出生点与高度图都已就位，但没有精灵。

地图模板通过 `InMemoryGameObjectProvider` 注册了加载与释放回调，与 Addressables 引用计数同步：游戏加载地图资产时解码全部图块（不可读纹理，Point 过滤，Clamp），创建精灵；地图实例销毁、句柄释放后销毁纹理与精灵，原版 BGM 句柄一并释放。跨包 `rex://` 图片由资源注册表解码，共享使用，不随地图释放。

校验覆盖：格式版本、排序层、坐标范围、图块编号与 PNG 尺寸、模板键与引用、碰撞形状、坡度格、出生点名称与是否落在实心碰撞内、相机多边形、音乐。它不能证明角色整个身体不会贴墙，也不能证明地图可达性，需要实际移动检查。

单地图限制：32 个图层、4096 个物件模板、8192 个物件、4096 个地图碰撞、每个碰撞 4096 个顶点与 64 条路径、100000 个坡度格、256 个出生点。校验失败的地图不进入数据库，其余资源仍可加载，错误见 BepInEx 日志。

## 测试包与原版镜像

在仓库根目录运行：

```text
python src/MetaMystia.AITest/create-day-map-package.py .tmp/day-map/IsolatedMapTest.zip
```

测试地图包含河流、木桥、前景栏杆、按 Y 排序的树（模板带圆形碰撞，实例放大 2 倍）、多边形与矩形河道、闭合折线外圈、两块坡面和四个出生点。地面宽 1728 像素，跨两个图块，用于检查接缝。用 `goto 1073742000 SlopeUp` / `SlopeDown` 进入正、负坡度区域。

原版地图可用 `tools/day-map-art/mirror` 从游戏 Addressables 转换为画布地图，见其 README。镜像包只供本地参考。

## 未覆盖

特殊材质（光照、阴影滤镜）、动画、交互物、采集点、NPC、出口与传送面板尚未加入。存档移除地图包后的恢复、联机和其他屏幕比例尚需验证。

## 实测记录

以下实测基于 formatVersion 1（2026-09），覆盖地图注册、原生切图、出生点、矩形碰撞、按 Y 排序与坡面输入；这些路径在画布地图中保持不变。画布地图的图块显示、接缝、多边形与折线碰撞、按需解码与释放、原版 BGM 尚未在游戏中实测。

### 2026-09-14 地图注册修复

修复 `MapNode` 写入 IL2CPP 字典时的值类型字段错位，详见 [Il2CppInterop 缺陷](il2cppinterop-defects.md)。独立地图不配置食堂，三级编号数组均为空。部署后两个扩展地图条目的类型和空数组长度正确，55 个原版食堂编号的等级查询全部通过。

### 2026-09-13 平地与坡面

环境：游戏 RELEASE 4.4.0e、Unity 2021.3.28f1、MetaMystia 0.27.0，单机，1280×720。

| 检查 | 运行时结果 |
| --- | --- |
| 进入 | 控制台 `goto` → `SwapMap` 完成，默认出生位置 `(-6,0)` |
| 河岸碰撞 | 从 `(6,4)` 向 `(0,4)` 移动，阻挡于约 `(3.29,4)`；人物横向半宽约 0.29 格 |
| 前后排序 | 树顺序 `-96`；玩家在树后为 `-127`，树前为 `-66`，截图符合遮挡关系 |
| 返回 | `back` 回到 `Home` 出生点；输入恢复，行动点不变 |
| 坡面 | `0.5` 配置采样为 `0.5019608`；水平向右 `Δx=3.1080`、`Δy=1.5601`，负坡与反向对称；切图后重新绑定 |

移动测试走 `UpdateInputDirection` 和游戏物理，不是修改坐标。脚本见 `src/MetaMystia.AITest/`。
