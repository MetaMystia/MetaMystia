# 原版白天地图结构

调研环境：游戏 RELEASE 4.4.0e，单机。当时从零构造的 `DaySceneMap` 经 GameObject Provider 接入原生加载，完成行走、桥面通行、河岸阻挡、前后遮挡、顶层覆盖和多次进出；该方案已实现为画布地图，见 [ResourceEx 白天地图](../reference/resourceex/day-maps.md)。本文保留原版结构，以及出口、NPC、存档等尚未实现部分的调研结论。

逆向路径相对逆向仓库。标注 mimo 或只有空方法体的代码不能单独作为结论。

## 原图组成

以妖怪兽道 `BeastForest` 为例：

| 层 | 运行时情况 | 含义 |
|---|---|---|
| `BeastForest_Tilemap_Binary` | 7386 格，`Background/-2000`，Chunk | 主要地面；常见 Sprite 48×48、PPU 48、pivot `(0,0)` |
| `Shadow_Floor` | 77 格，`Background/-1300` | 地面阴影，专用材质 |
| `Daylight` | 17 格，`Overlay/700` | 光效；样本 PPU 100，格变换含 5 倍缩放 |
| `TopDecoration` | 13 格，`Overlay/5` | 固定盖在角色上方的装饰 |
| `HEIGHT_MAP` | 20 格，Renderer 关闭，纹理可读 | 斜坡输入数据 |
| `MoveColliders` | 0 格，无 TilemapRenderer | 实际阻挡由其多边形组件承担 |

Grid 为 Rectangle、XYZ，`cellSize=(1,1,0)`。各层的 anchor、pivot、PPU 和材质不完全相同。`BinaryTilemapDecompressor` 在 Awake 时按 Sprite 索引、图层索引、格坐标和矩阵创建 Tile，再批量 `SetTiles`。

### 碰撞独立于贴图

- `MoveColliders` 的 PolygonCollider2D（1 条路径、366 点）参与 `Map` 上的 CompositeCollider2D（`Outlines`，5 条路径），只有轮廓阻挡。
- 建筑和装饰有独立的 Polygon、Box Collider；采集点和出口是 Trigger。
- `CameraCollider` 是单独的 PolygonCollider2D，位于 `Ignore Raycast` 层，供相机使用。
- 视觉 Tile 的 `colliderType=Sprite` 不表示该层产生移动碰撞。桥面通道宽度要留出玩家碰撞半径（横向半宽约 0.29）。

### 排序

`DEYU.Utils.LayerSortingController` 按脚底世界 Y 设置 `sortingOrder=(int)(-32*y)`，玩家位于 `Character` 层。需要与角色前后交错的物体各自使用 SpriteRenderer 或 SortingGroup，以脚底为排序原点；合成进同一张 Chunk Tilemap 的物体不能逐个与角色交错。

### HEIGHT_MAP

`HeightBlendedInputProcessorComponent` 采样当前格像素：红通道为斜率大小，绿通道决定正负，输入改为 `(x, y + slope*x)`；剧情中和部分向上输入例外。它不是楼层系统，不能实现桥上、桥下同坐标分别通行。`height=null` 的地图可正常行走。

### 相机

`UpdateCharacter` 将 `map.boundingShape` 交给 CinemachineConfiner，`m_ConfineScreenEdges=false`，约束的是相机中心；另有 `CinemachineCameraOffset=(0,0.5,-10)` 和像素取整组件。直接用美术外框作范围会露出黑边，需按视口半宽高（正交半高 7.5、16:9 时半宽 13.333）收缩并扣除 Y+0.5 偏移。复杂边界和比视口小的地图需单独处理。

### 加载

- `SpawnMapReferenceAsync` 先加载 GameObject 资产句柄，再 `Instantiate(loaded.Component)`。
- 原生 `SwapMapAsync` 在请求目标资产前已销毁旧地图；模板和依赖必须在开放入口前准备好。
- 原生加载还会处理目标地图和 `DEFAULT` 的 additive 对象。
- 地图登记项：`DataBaseDay.mapData`、`mapReference`、`allCollectablesLabels`、`allSpawnMarkerLabels`、`DaySceneLanguage.MapLanguageData`。独立区域的 `parent` 为空字符串。
- `GetMapLabelFromSpawnMarker` 全局查找第一个含有该 marker 的地图，marker 重名会导致错误归属。

## 出口（未实现）

原生出口组合：`Collider2D (Trigger)` + `InteractableArea` + `MapTransitionData` + `MapTransitionConditionComponent` + `MapTransitionBehaviourComponent` + `VisualEntity`。

- 目标由 `targetSceneLabel`、`targetSceneSpawnMarker` 指定；`shouldCostAction` 决定是否扣 1 行动点；`isInteractTransition` 区分走入与互动。Behaviour 最终调用原生 `SwapMap`。
- 组件有 Awake/Start 依赖，需在未激活对象上组装完成；`VisualEntity`、`InteractableArea` 的数组须显式为空或有效。
- 原图实例每次初始化都需按唯一出口 ID 注入入口，避免重复；进图出生点放在出口触发区之外。
- 原生条件检查 `GetMapOpenStatus`，按 `MapNode.parent` 或自身 mapName 查询解锁状态。直接 `SwapMap` 成功不等于出口可用；`UnlockMap` 还涉及通知与配方。

## NPC

资源包角色通过 `characters[].spawnMarker` 指向地图，实现见 [模块结构](../reference/resourceex/module-structure.md)。`SpecialGuestRegistry.RegisterNPC` 中有注释掉的旧实现，NPC 创建链路需连同 `RegisterAllSpecialGuests`、原生运行时 NPC 数据、`RefreshAllDayNpcs` 和定位 Patch 一起看。

## 存档与联机

- 地图模板属于包资产，随包加载重建。原生存档的 `trackedMaps`、`trackedNPCs` 记录解锁与 NPC 状态；不要为登记地图写 NPC 的持久化 destination。
- 包缺失时，若恢复流程会引用该地图，需在切图前转到原版落点，避免原生切图销毁旧图后失败。旧存档首次装包、升级包、移除包的完整链路未验证。
- 联机资料以整数 `MapLabel` 同步地图，协议允许 9000 以上的自定义地图 ID；两端需加载相同的地图包。

## 逆向索引

- `src/Assembly-CSharp/GameData/Core/Collections/DaySceneUtility/DataBaseDay.cs`：地图索引、资产加载、marker 查找。
- `src/Assembly-CSharp/DayScene/SceneManager.cs`、`DaySceneMap.cs`：切图、实例初始化、NPC、句柄。
- `src/Assembly-CSharp/DayScene/Input/DayScenePlayerInputGenerator.cs`：落点、相机与高度输入绑定。
- `src/Assembly-CSharp/Common/CharacterUtility/HeightBlendedInputProcessorComponent.cs`：斜坡输入。
- `src/Assembly-CSharp-firstpass/DEYU/Utils/LayerSortingController.cs`：按 Y 排序。
- `src/DEYU.BinaryTilemap/BinaryTilemapDecompressor.cs`：原版瓦片重建。
- `src/Assembly-CSharp/DayScene/Interactables/`：`MapTransitionData`、`InteractableArea`、`VisualEntity` 及相关 Condition、Behaviour。
- `src/Assembly-CSharp/GameData/RunTime/DaySceneUtility/RunTimeDayScene.cs`：地图解锁、NPC 与存档数据。
