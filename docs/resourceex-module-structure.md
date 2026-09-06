# ResourceEx 模块结构

ResourceEx 资源包子系统按职责分层，目录与命名空间一一对应：

| 目录 | 命名空间 | 职责 |
|---|---|---|
| `ResourceEx/Core.cs` | `MetaMystia` | `ResourceExManager`：包加载、DLC 依赖检查、生命周期钩子、包查询 |
| `ResourceEx/Registries/` | `MetaMystia.ResourceEx.Registries` | 各内容领域注册器：SpecialGuest、Dialog、Gift、Ingredient、Food、Beverage、Recipe、Cloth、MissionNode、EventNode、Merchant；以及 `PixelSpriteFactory`、`SchedulerDataRecovery` |
| `ResourceEx/Mappers/` | `MetaMystia.ResourceEx.Mappers` | config DTO → 游戏对象转换 |
| `ResourceEx/Models/` | `MetaMystia.ResourceEx.Models` | ResourceEx.json 配置 DTO |
| `ResourceEx/AssetManagement/` | `MetaMystia.ResourceEx.AssetManagement` | ZIP 加载、ID 范围与签名校验、rex:// 资产注册表与资产查询 |
| `ResourceEx/Addressables/` | `MetaMystia.ResourceEx.Addressables` | 内存资产注入 Unity Addressables 管线 |

## 数据流

包扫描与解析（`ResourcePackageLoader`）→ ID 校验（`IdRangeValidator`）→ 资产注册（`RexAssetRegistry` / `RuntimeAddressables`）→ `ResourceExManager` 按包合并配置到各注册器（`*Registry.Merge`）→ 游戏初始化钩子按序调用各注册器的注册方法。

## 生命周期钩子

游戏数据库初始化由 Patch 调用 `ResourceExManager.OnDataBaseXxxInitialized()` 等钩子驱动，钩子内按固定顺序调用各注册器。注册顺序即依赖顺序：Dialog 先于 MissionNode、EventNode、Merchant、SpecialGuest 构建。

`GiftRegistry` 按加载的包保留礼物列表，在 `OnDataBaseDayInitialized()` 注册对话后校验 Item 与对话引用。`GiftMailboxManager` 负责菜单和对话结束后的入库；与 `StoryReplayManager` 共用 `UI/DaySceneSelectionMenu`，不持有领取存档。

## 约定

- 注册器只持有本领域配置与产物；跨领域查询调用兄弟注册器，资产读取走 `RexAssetRegistry`。
- 新增内容类型：在 `Registries/` 新增注册器类，并在 `ResourceExManager.MergeResourcePackage` 与对应生命周期钩子中登记。
- 配置 DTO 与注册逻辑的同步见 [resourceex-package-contract.md](resourceex-package-contract.md)。
