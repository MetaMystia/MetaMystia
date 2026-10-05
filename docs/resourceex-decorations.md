# 装饰物

装饰是原版 `Decoration : Item`。数据、图标和文案来自资源包；效果按 `implementation` 选择 Mod 中的实现，组织方式与符卡一致。

服装的 `ClothesProfile.Clothes` 不继承 `Item`，而是通过相同 ID 与普通 Item 关联。因此 `ItemRegistry` 对服装创建普通 Item，对装饰则委托 `DecorationRegistry` 创建 Decoration，并将同一个对象写入 Items 和 Decorations；此处保留装饰分支，服装专属数据仍由 `ClothRegistry` 注册。

原版 `Item.IsClothes` 和 `Item.IsDecoration` 都按 ID 查询对应数据库，不依赖 C# 类型判断。注册时的对象类型差异与运行时的分类查询是两回事。

## 配置

在 `ResourceEx.json` 增加 `decorations`，达摩的计时提示另在 `buffs` 声明。以下 ID 和路径为示例，发布前须确认所属 ID 段并补齐图片。

```json
"decorations": [
  {
    "id": 9006,
    "name": "小鸽子挂坠",
    "description": "装备后，白天移动速度系数增加0.12；夜间营业时，移动速度、伙伴工作效率和伙伴移动速度提高20%。",
    "spritePath": "assets/Decoration/9006.png",
    "implementation": "DovePendant"
  },
  {
    "id": 9007,
    "name": "达摩雪雪",
    "description": "营业时，玩家或伙伴做出黑暗料理后，获得30秒「不倒翁七転八起」：烹饪有20%概率返还食材，烹饪时间减少20%。重复触发刷新持续时间，不叠加。",
    "spritePath": "assets/Decoration/9007.png",
    "implementation": "DarumaYukiyuki",
    "buffId": 11004
  }
],
"buffs": [
  {
    "id": 11004,
    "name": "不倒翁七転八起",
    "description": "烹饪有20%概率返还食材，烹饪时间减少20%。剩余$c秒。",
    "icon": "assets/Buff/11004.png"
  }
]
```

| 字段 | 说明 |
|---|---|
| `id`、`name`、`description`、`spritePath` | 与物品共用；图标同时用于展示柜 |
| `implementation` | 必填，当前支持 `DovePendant`、`DarumaYukiyuki` |
| `decorationType` | 原版类型，默认 `Outdoor`；也支持 `Household`、`FishingRod`，原版普通营业只调用 Outdoor 的夜间效果 |
| `conflictDecorationIds` | 原版冲突装饰 ID 数组，默认空；装备本件时移除这些装饰的使用记录 |
| `buffId` | 引用 `buffs[].id`；达摩必填，小鸽子不需要 |

缺少图标、实现不存在或依赖不全时跳过该装饰，不注册空壳物品；其他内容照常加载。未声明装饰时不注入效果类型。获取可使用已有礼物邮箱、商人或任务奖励，不新增专用领取指令。

## 行为

- 白天在角色就绪及装备变化后增减本件装饰的效果；重复切图不累加。装备冲突以原版最终使用记录为准。
- 夜间由原版开业流程调用效果。营业中更改使用记录不动态重算效果，未新增联机装备共享或同步规则；是否在特殊挑战生效也沿用原版入口。
- 小鸽子的夜间玩家移速接入原版额外速度乘数，伙伴加成使用原版接口，不覆盖玩家基础速度或预先移除其他来源的同值加成。
- 达摩监听 `CookSystemManager.OnResultCompleteCallback`：玩家、伙伴的新料理最终为黑暗料理（Food，ID -1）时触发；不监听仓库入库、取出或托盘搬运。
- 达摩使用原版 30 秒计时 buff，重复触发先结束旧效果再重建，不叠加；暂停、禁止 buff、驱散及结束清理由原版处理。减时用于之后开始的烹饪，不重算正在烹饪的进度。
- 概率返还沿用原版 `FreeCookRate` 与 PRD 判定。按逆向源码，玩家确认开做时命中会返还并触发入库通知；伙伴在烹饪结束时判定返还，关闭入库通知。它与 `IsFreeCook` 的完全免食材机制不同。

## 增加效果

新增一个 `DecorationBaseEx` 子类，实现原版 `DecorationBuffEnterNight`；需要白天效果时重写 `OnDayEquip` / `OnDayUnequip`。通过 `IDecorationDependencies` 检查所需资源，在 `DecorationRegistry.Implementations` 登记一条工厂映射即可。不要把具体效果写入 `ResourceExManager` 或新增装饰专属 Patch。

## 验证范围

上述原版行为来自对应逆向源码审计，编译以项目 Interop DLL 为准。游戏实测仍需覆盖展示柜勾选与冲突、连续切图、两次营业、达摩刷新和到期、其他同值加成共存，以及主客机差异；离线检查不能代替这些验证。
