# 名词速查

同一行的中文说法视为同义词。以下按游戏源码中的类型整理，遇到游戏相关名词时必读，供快速定位和消歧，无需核查下面内容。

## 客人与收集品

| 名词／别名 | 对应类型（全名） |
| --- | --- |
| 稀客 | `GameData.Core.Collections.NightSceneUtility.SpecialGuest` |
| 普客、普通客人 | `GameData.Core.Collections.NightSceneUtility.NormalGuest` |
| 厨具 | `GameData.Core.Collections.Cooker` |
| 菜谱、食谱 | `GameData.Core.Collections.Recipe` |
| 料理 | `GameData.Core.Collections.Sellable` |
| 酒水 | `GameData.Core.Collections.Sellable` |
| 食材 | `GameData.Core.Collections.Ingredient` |
| 服装、衣服 | `GameData.Profile.ClothesProfile.Clothes`、`GameData.Core.Collections.Item` |
| 装饰物 | `GameData.Core.Collections.Decoration` |
| 唱片 | `GameData.Core.Collections.Record` |
| 物品、Item | `GameData.Core.Collections.Item` |

相关继承关系（节选）：

```text
GameData.Core.NonTradableObjectBase
├─ GameData.Core.Collections.Item
│  ├─ GameData.Core.Collections.Decoration
│  └─ GameData.Core.Collections.Record
├─ GameData.Core.Collections.Cooker
├─ GameData.Core.Collections.Recipe
└─ GameData.Core.TradableObjectBase
   ├─ GameData.Core.Collections.Ingredient
   └─ GameData.Core.Collections.Sellable
```

`GameData.Profile.ClothesProfile.Clothes` 不继承 `GameData.Core.Collections.Item`，两者通过 ID 关联。

## 营业与白天活动

| 名词／别名 | 对应类型（全名） |
| --- | --- |
| 食堂（泛指营业店铺） | `GameData.Core.Collections.Izakaya` |
| 伙伴 | `GameData.Profile.PartnerInfoBase` |
| 符卡 | `GameData.Core.Collections.NightSceneUtility.SpellBase` |
| 托盘 | `GameData.RunTime.NightSceneUtility.IzakayaTray` |
| 商人 | `GameData.Core.Collections.DaySceneUtility.Collections.Merchant` |
| 商品 | `GameData.Core.Collections.DaySceneUtility.Collections.Merchant.Merchandise` |
| 采集点 | `GameData.Core.Collections.DaySceneUtility.Collections.Collectable` |
| 任务节点 | `GameData.Profile.SchedulerNodeCollection.MissionNode` |
| 事件节点 | `GameData.Profile.SchedulerNodeCollection.EventNode` |
| 对话包 | `GameData.Profile.DialogPackage` |

| 名词 | 对应类型或枚举成员（全名） |
| --- | --- |
| 客群 | `NightScene.GuestManagementUtility.GuestGroupController` |
| 营业中的稀客 | `NightScene.GuestManagementUtility.SpecialGuestsController` |
| 营业中的普客 | `NightScene.GuestManagementUtility.NormalGuestsController` |
| 订单 | `NightScene.GuestManagementUtility.GuestsManager.OrderBase` |
| 普客订单 | `NightScene.GuestManagementUtility.GuestsManager.NormalOrder` |
| 稀客订单 | `NightScene.GuestManagementUtility.GuestsManager.SpecialOrder` |
| 营业中的厨具 | `NightScene.CookingUtility.CookController` |
| 厨师伙伴 | `GameData.Profile.PartnerBase.PartnerType.Cook` |
| 传菜伙伴 | `GameData.Profile.PartnerBase.PartnerType.Waitress` |
| 酒水伙伴 | `GameData.Profile.PartnerBase.PartnerType.Barmaid` |
| 最终试炼 | `NightScene.NightSceneDirector.ChallengeType.Story_Yuyuko` |
| 重修最终试炼 | `NightScene.NightSceneDirector.ChallengeType.Challenge_Yuyuko` |

| 名词 | 代码用词 |
| --- | --- |
| 羁绊 | `Kizuna`、`Bond` |
| 奖励符卡 | `Positive` |
| 惩罚符卡 | `Negative` |
| 热火朝天 | `Fever` |
| 夜雀之歌 | `QTE`（Quick Time Event） |
| 耐心 | `Patient`、`CurrentPatient` |
| 桌号 | `DeskCode` |
| 小费 | `Tip` |

## 符卡

以下同时为游戏内角色名对照：「中文名」为游戏内角色名，「类名」为符卡类（`Spell_*`），「持有者标识」为 `OnGettingSpellOwnerIdentifier()` 的返回值。

| 中文名 | 类名 | 持有者标识 | 备注 |
| --- | --- | --- | --- |
| — | `Spell_` | `—` | 基类占位（OnNegativeBuffExecute 抛 NotImplementedException） |
| 稗田阿求 | `Spell_Akyuu` | `Akyuu` | |
| 爱丽丝 | `Spell_Alice` | `Alice` | |
| 射命丸文 | `Spell_Aya` | `ShameimaruAya` | |
| 橙 | `Spell_Chen` | `Chen` | |
| 琪露诺 | `Spell_Cirno` | `Cirno` | |
| 爱莲 | `Spell_Ellen` | `Ellen` | |
| 芙兰朵露 | `Spell_Flandre_Intro` | `Flandre` | 开场/介绍用符卡 |
| 物部布都 | `Spell_Futo` | `MononobeFuto` | |
| 今泉影狼 | `Spell_Kagerou` | `ImaizumiKagerou` | |
| 蓬莱山辉夜 | `Spell_Kaguya` | `Kaguya` | |
| 茨木华扇 | `Spell_Kasen` | `Kasen` | |
| 上白泽慧音 | `Spell_Keine` | `Keine` | |
| 上白泽慧音 | `Spell_Keine_Intro` | `—` | 代码无持有者标识；editor 中 Keine_Intro 名为占位符 |
| 多多良小伞 | `Spell_Kogasa` | `TataraKogasa` | |
| 古明地恋 | `Spell_Koishi` | `Koishi` | |
| 秦心 | `Spell_Kokoro` | `TBSKokoro` | 4.4.0e 新增；owner 为 TBSKokoro |
| 露易兹 | `Spell_Luize` | `Luize` | |
| 二岩猯藏 | `Spell_Mamizou` | `HutatsuiwaMamizou` | |
| 雾雨魔理沙 | `Spell_Marisa` | `Marisa` | |
| 梅蒂欣 | `Spell_Medicine` | `Medicine` | |
| 红美铃 | `Spell_Meirin` | `Meirin` | |
| 魅魔 | `Spell_Mima` | `Mima` | |
| 藤原妹红 | `Spell_Mokou` | `Mokou` | |
| 犬走椛 | `Spell_Momizi` | `Momizi` | |
| 村纱水蜜 | `Spell_Murasa` | `MurasaMinamitu` | |
| 矢田寺成美 | `Spell_Narumi` | `Narumi` | |
| 河城荷取 | `Spell_Nitori` | `Nitori` | |
| 封兽鵺 | `Spell_Nue` | `HoujuuNue` | |
| 火焰猫燐 | `Spell_Orin` | `Orin` | |
| 水桥帕露西 | `Spell_Parsee` | `Parsee` | |
| 帕秋莉 | `Spell_Patchouli` | `Patchouli` | |
| 博丽灵梦 | `Spell_Reimu` | `Reimu` | |
| 铃仙 | `Spell_Reisen2` | `Reisen2` | |
| 蕾米莉亚 | `Spell_Remilia` | `DLC4_Remilia` | |
| 冴月麟 | `Spell_Rin` | `Rin` | |
| 森近霖之助 | `Spell_Rinnosuke` | `Rinnosuke` | |
| 露米娅 | `Spell_Rumia` | `Rumia` | |
| 东风谷早苗 | `Spell_Sanae` | `Sanae` | |
| 古明地觉 | `Spell_Satori` | `Satori` | |
| 霍青娥 | `Spell_Seiga` | `KakuSeiga` | |
| 鬼人正邪 | `Spell_Seija` | `KijinSeija` | |
| 少名针妙丸 | `Spell_Shinmyoumaru` | `SukunaShinmyoumaru` | |
| 伊吹萃香 | `Spell_Suika` | `Suika` | |
| 比那名居天子 | `Spell_Tenshi` | `Tenshi` | |
| 因幡帝 | `Spell_Tewi` | `Tewi` | |
| 饕餮尤魔 | `Spell_TotetsuYuuma` | `Totetsu` | editor 中拼作 Toutetsu |
| 绵月丰姬 | `Spell_Toyohime` | `Toyohime` | |
| 苏我屠自古 | `Spell_Toziko` | `SogaToziko` | |
| 立空汐 | `Spell_Ushio` | `Ushio` | |
| 灵乌路空 | `Spell_Utsuho` | `Utsuho` | |
| 莉格露 | `Spell_Wriggle` | `Wriggle` | |
| 黑谷山女 | `Spell_Yamame` | `Yamame` | |
| 绵月依姬 | `Spell_Yorihime` | `Yorihime` | |
| 魂魄妖梦 | `Spell_Youmu` | `Youmu_Free` | |
| 八云紫 | `Spell_Yukari` | `Yukari` | |
| 时焉侑 | `Spell_Yuu` | `Yuu` | |
| 星熊勇仪 | `Spell_Yuugi` | `Yuugi` | |
| 风见幽香 | `Spell_Yuuka` | `KazamiYuuka` | |
| 西行寺幽幽子 | `Spell_Yuyuko` | `Yuyuko_Free` | |

## 食堂等级

“食堂”也特指三级店铺。名称对应模组的 [等级映射](../../src/MetaMystia.Mod/Utils/MapLabel.cs) 与 [中文文本](../../src/MetaMystia.Mod/UI/Locales/zh-CN.json)。

| 名词 | 对应枚举成员（全名） |
| --- | --- |
| 推车（一级） | `Common.UI.IzakayaLevel.Level1` |
| 小屋（二级） | `Common.UI.IzakayaLevel.Level2` |
| 食堂（三级） | `Common.UI.IzakayaLevel.Level3` |

## 场景

| 名词／别名 | 对应类型（全名） | 场景枚举／代码用词 |
| --- | --- | --- |
| 启动场景 | `SplashScene.SceneManager` | `SplashScene` |
| 主菜单场景 | `MainScene.SceneManager` | `Common.UI.Scene.MainScene` |
| 加载场景 | `Common.LoadingSceneManager` | `Common.UI.Scene.LoadScene` |
| 白天场景 | `DayScene.SceneManager` | `Common.UI.Scene.DayScene` |
| 准备场景 | `PrepNightScene.SceneManager` | `Common.UI.Scene.IzakayaPrepScene` |
| 营业场景、工作场景、夜间场景 | `NightScene.SceneManager` | `Common.UI.Scene.WorkScene` |
| 结算场景 | `ResultScene.SceneManager` | `Common.UI.Scene.ResultScene` |

## 地图

以下为模组 [地图枚举](../../src/MetaMystia.Mod/Utils/MapLabel.cs) 已登记的地图，中文名取自 [中文文本](../../src/MetaMystia.Mod/UI/Locales/zh-CN.json)。枚举成员去掉 `MetaMystia.MapLabel.` 前缀即为游戏 `MapKey`，如 `Home`、`DLC1_MagicForest`。

| 地图／别名 | 对应模组枚举成员（全名） |
| --- | --- |
| 夜雀小屋 | `MetaMystia.MapLabel.Home` |
| 地下室 | `MetaMystia.MapLabel.Basement` |
| 妖怪兽道、兽道 | `MetaMystia.MapLabel.BeastForest` |
| 人间之里、人里 | `MetaMystia.MapLabel.HumanVillage` |
| 博丽神社 | `MetaMystia.MapLabel.HakureiShrine` |
| 红魔馆 | `MetaMystia.MapLabel.ScarletMansion` |
| 迷途竹林 | `MetaMystia.MapLabel.BambooForest` |
| 舞台 | `MetaMystia.MapLabel.PartyStage` |
| 白玉楼 | `MetaMystia.MapLabel.Hakugyokurou` |
| 魔法森林 | `MetaMystia.MapLabel.DLC1_MagicForest` |
| 妖怪之山、妖怪山 | `MetaMystia.MapLabel.DLC1_YoukaiMountain` |
| 旧地狱 | `MetaMystia.MapLabel.DLC2_FormerHell` |
| 地灵殿 | `MetaMystia.MapLabel.DLC2_EarthSpiritsPalace` |
| 命莲寺 | `MetaMystia.MapLabel.DLC3_MyourenTemple` |
| 神灵庙 | `MetaMystia.MapLabel.DLC3_DivineSpiritMausoleum` |
| 博丽大祭 | `MetaMystia.MapLabel.DLC3_HakureiFestival` |
| 太阳花田 | `MetaMystia.MapLabel.DLC4_GardenOfTheSun` |
| 辉针城 | `MetaMystia.MapLabel.DLC4_ShiningNeedleCastle` |
| 红魔馆地下室 | `MetaMystia.MapLabel.DLC4_ScarletMansionBasement` |
| 魔界 | `MetaMystia.MapLabel.DLC5_Makai` |
| 月之都、月都 | `MetaMystia.MapLabel.DLC5_LunarCapital` |
