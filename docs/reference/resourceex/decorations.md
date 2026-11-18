# 装饰

装饰是原版 `Decoration : Item`。名称、说明、图标和类型来自资源包 `decorations`；效果按 `implementation` 选择 Mod 中的实现，组织方式与符卡相同。

## 配置

```json
"decorations": [
  {
    "id": 8999,
    "name": "小鸽子挂坠",
    "description": "白天移动速度提高0.12；营业时移动速度、伙伴工作效率和伙伴移动速度提高20%。",
    "spritePath": "assets/Decoration/8999.png",
    "implementation": "DovePendant"
  }
]
```

| 字段 | 说明 |
|---|---|
| `id`、`name`、`description`、`spritePath` | 与物品共用；图标同时用于展示柜 |
| `implementation` | 必填，对应 `DecorationRegistry.Implementations` 的键 |
| `decorationType` | `Outdoor`（默认）、`Household`、`FishingRod`；原版普通营业只对 `Outdoor` 调用夜间效果 |
| `conflictDecorationIds` | 冲突装饰 ID，默认空；装备本件时原版移除冲突项的使用记录 |

实现不存在、图标缺失或依赖检查不通过时跳过该装饰，不注册物品。实现用到的 buff 与符卡一样在代码中以常量引用，并在 `buffs` 中声明。

## 新增实现

在 `ResourceEx/DecorationCollection/` 新建 `DecorationBaseEx` 子类并实现 `IDecorationDependencies`，在 `DecorationRegistry.Implementations` 登记一行。一件装饰的效果全部收在它自己的文件中，不新增专属 Patch。

- 夜间：重写原版 `DecorationBuffEnterNight`。`EventManager`、`CookSystemManager` 是夜间场景单例，在其上的登记和事件订阅随场景销毁，无需手动清理。限时效果用原版计时 buff，暂停、驱散和结束由原版处理。
- 白天：原版装饰没有白天效果。子类只声明加成（目前为 `DayMoveSpeedBonus`），`DecorationRegistry.ApplyDayEffects` 按当前装备集合汇总后直接赋值。新增其他白天效果时同样在基类加声明、在汇总处赋值，不做增量加减。

## 白天移速

原版在以下时点把 `MoveSpeedMultiplier` 写为等级值 `LevelProfile.MoveSpeedMultiplier`（源码推断）：

- `DayScenePlayerInputGenerator.UpdateCharacter` 初始化角色
- `RunTimePlayerData.MountAndOverridePlayerRuntimeData`（挑战存档挂载）

`ApplyDayEffects` 在上述方法及 `RunTimeAlbum` 装备增删、存档挂载之后重算，结果为等级值加上已装备装饰的加成。等级值每级 +0.12（1.00、1.12、1.24…，来自 `LevelProfile` 资源）。剧情移动期间 `SceneDirector` 临时改写并恢复该值，剧情中更换装备时恢复的是旧值（待验证）。

## 黑暗料理

原版黑暗料理（`Food` ID -1）的来源都在烹饪完成时经过 `CookSystemManager.OnResultCompleteCallback`（源码推断）：玩家 QTE 失败、厨具（如核能系列）、伙伴（如键山雏）、符卡强制覆写（帕秋莉、潮）。未发现烹饪以外的来源。

## 联机

每台机器按自己的存档装备调用效果，不同步，两端结果可能不一致。

## 示例实现

| 文件 | 效果 |
|---|---|
| `DovePendantDecoration.cs` | 白天移速 +0.12；营业时玩家移速、伙伴工作效率与移速 ×1.2 |
| `DarumaYukiyukiDecoration.cs` | 营业时做出黑暗料理后获得 30 秒 buff：返还食材概率 +20%（原版 `FreeCookRate`，与其他来源相加），烹饪时间 ×0.8；重复触发用 `TryOverrideTimedBuff` 刷新时长，不叠加 |

示例中的装饰 ID 8999、8998 与 buff ID 8997 为占位，位于游戏保留段，资源包 ID 校验不会通过，分配正式 ID 后替换。达摩的 buff 说明中 `$a` 为剩余秒数。以上行为未经游戏实测。
