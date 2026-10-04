# ResourceEx 模块结构

ResourceEx 资源包子系统按职责分层，目录与命名空间一一对应：

| 目录 | 命名空间 | 职责 |
|---|---|---|
| `ResourceEx/Core.cs` | `MetaMystia` | `ResourceExManager`：包加载、DLC 依赖检查、生命周期钩子、包查询 |
| `ResourceEx/Registries/` | `MetaMystia.ResourceEx.Registries` | 各内容领域注册器：SpecialGuest、Dialog、Gift、Ingredient、Food、Beverage、Recipe、Cloth、MissionNode、EventNode、Merchant、Spell、Buff、AssetBundle；以及 `SchedulerDataRecovery` |
| `ResourceEx/SpellCollection/` | `MetaMystia.ResourceEx.SpellCollection` | 符卡基类 `SpellBaseEx` 与各符卡的行为实现 |
| `ResourceEx/Vfx/` | `MetaMystia.ResourceEx.Vfx` | `VfxBundle`：AssetBundle 中特效的播放、全屏遮罩与结束 |
| `ResourceEx/Models/` | `MetaMystia.ResourceEx.Models` | ResourceEx.json 配置 DTO |
| `ResourceEx/AssetManagement/` | `MetaMystia.ResourceEx.AssetManagement` | ZIP 加载、ID 范围与签名校验、rex:// 资产注册表与资产查询 |

## 数据流

包扫描与解析（`ResourcePackageLoader`）→ ID 校验（`IdRangeValidator`）→ 资产注册（`RexAssetRegistry`，经框架 `IAssetLocator`）→ `ResourceExManager` 按包合并配置到各注册器（`*Registry.Merge`）→ 游戏初始化钩子按序调用各注册器的注册方法。

## 生命周期钩子

游戏数据库初始化由 Patch 调用 `ResourceExManager.OnDataBaseXxxInitialized()` 等钩子驱动，钩子内按固定顺序调用各注册器。注册顺序即依赖顺序：Dialog 先于 MissionNode、EventNode、Merchant、SpecialGuest 构建。

`GiftRegistry` 按加载的包保留礼物列表，在 `OnDataBaseDayInitialized()` 注册对话后校验 Item 与对话引用。`GiftMailboxManager` 负责菜单和对话结束后的入库；与 `StoryReplayManager` 共用 `UI/DaySceneSelectionMenu`，不持有领取存档。

`DataBaseCore` 初始化后，`AssetBundleRegistry` 先预加载已声明的包，`SpellRegistry` 再检查符卡配置及实现要求的依赖。手工维护的 `Implementations` 列表关联受 ISpellDependencies 约束的泛型工厂；检查通过后才注入类型、创建实例，并将 `vfxBundle` 对应的 `VfxBundle` 赋给实例。未声明符卡时不创建或注入，缺少依赖时只跳过对应符卡。后续语言、角色符卡标记和夜间实例登记只处理通过检查的项。

## 约定

`DayMapRegistry` 在白天数据库初始化时校验配置并从零创建地图模板，登记地图与语言数据。`InMemoryGameObjectProvider` 保留模板，原游戏切图负责实例的加载与销毁；`DayMapCommands` 提供单机指令进入和返回。配置见 [白天地图首版](resourceex-day-maps.md)。

- 注册器只持有本领域配置与产物；跨领域查询调用兄弟注册器，资产读取走 `RexAssetRegistry`。
- 新增内容类型：在 `Registries/` 新增注册器类，并在 `ResourceExManager.MergeResourcePackage` 与对应生命周期钩子中登记。
- 配置 DTO 与注册逻辑的同步见 [resourceex-package-contract.md](resourceex-package-contract.md)。
