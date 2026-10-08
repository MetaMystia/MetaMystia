# 秋穰子符卡

角色 ID `10001`，实现名 `Minoriko`。资源包沿用秋穰子的笑脸、生气立绘，新增 `assets/Spell/10001` 和 `assets/Buff/10001.png`。

## 设定与美术

- 秋穰子是掌管丰收的妹妹，秋静叶是掌管红叶的姐姐。穰子的特效只采用稻穗、谷粒与帽饰葡萄，不使用姐姐的红叶意象。
- 红卡名称对应原作丰符「オヲトシハーベスター」。黑卡沿用本次需求命名，不作为原作符卡的逐字翻译。
- 红卡以一束稻穗向上舒展起手；结界持续期间，稻穗在施放位置附近错落浮现、升起后消散，穿插少量葡萄与金光。无整屏贴图、边框或圆形法阵，与舞的屏幕覆盖表现区分。
- 黑卡以谷粒和葡萄短暂散开，接续料理投掷与饱腹光点。结界解除时停止发射，已有粒子自然消散。

资料：[秋穰子及《东方风神录》设定文档](https://thwiki.cc/秋穰子)、[《东方外来韦编》秋姐妹介绍](https://www.thwiki.cc/东方外来韦编/肆/幻想乡人妖名鉴)、[符卡名称考据](https://ewigleere.net/studynotes/fuujinroku_aki_sisters_old-20181005_2/)。设定文档与后续考据应分开看待。

## 行为

| 符卡 | 实现 |
|---|---|
| 丰符「大年收获者」 | 丰穣结界基础时长 30 秒，重复触发沿用原版叠加计时；结束时移除制作加成和持续特效 |
| 料理 | 沿用梅蒂欣的 `ExtraCookTags`：期间新制作的料理附加饱腹，成品保留 tag；已有料理不追溯修改 |
| 秒做 | 按料理原始 `tags` 是否有饱腹判定，不把结界附加的饱腹算进去；制作时间乘数为 0，游戏仍保留 0.01 秒下限及原有 QTE 流程 |
| 清酒 | 以酒水 tag `0` 判定。玩家及伙伴上酒时走原版免费酒水分支：返还取酒时扣除的 1 份，并免除额外耗酒。取出再放回不增加库存；与其他免费酒水效果不重复返还 |
| 秋符「无常秋日与少女之心」 | 立即解除结界。对施放时已生成的客群，各减少 1 次剩余点单额度及额外上限，最低保留 0 次；不撤销当前订单。每个角色各播放一次随机料理飞入动画，不真实上菜、不结算 |

黑卡投掷起点依次取：上下文中仍存在的触发角色、在场的符卡所属角色、玩家位置。所属角色按 `OwnerIdentifier` 匹配，不写死角色 ID。原版 `GuestPosition` 在不记录符卡次数的代触发场景直接返回玩家位置，因此这里读取实际角色实例。原版若在执行入口发现正常触发的客人已消失，会直接结束符卡；此修改不绕过该检查。

## 酒架无限清酒调研（#117，尚未实现）

当前仍是上酒返还。建议保留真实库存，另行记录免费取出的酒水实例；不要把存档库存改成 `-1`，也不要结束时恢复库存快照，否则可能覆盖期间正常获得的酒水。

- 范围建议限定为结界生效时库存中已有、带清酒 tag 的酒水，不凭空解锁未持有酒水。酒架数量显示为 `∞`，结界开始和结束时刷新已打开的面板。
- 玩家和酒水伙伴取酒时不扣真实库存，并记录取出的具体实例。只按酒水 ID 计数无法区分同种酒中哪些在结界前已扣库存。
- 免费实例在结界结束后仍可送出，但放回或打烊回收时不增加库存；正常实例照常返还。记录要保留到实例送出或回收，不能随结界计时一起清空。
- 免费实例上酒时免额外消耗，也不再走当前的返还 1 份逻辑；其他免费酒水效果同样不能重复返还。普通奖励入库不能被全局拦截。

已核对的原版入口：

| 环节 | 逆向代码入口（相对于 `src/Assembly-CSharp/`） |
|---|---|
| 库存与数量 | `GameData/RunTime/Common/RunTimeStorage.cs`：酒水没有食材的 `infiniteResolver`；普通酒水数量 `-1` 仍会被扣成 `-2` |
| 酒架显示、取出、放回 | `NightScene/UI/CookingUtility/WorkSceneStoragePannel.cs`：`UpdateBevField`、`Extract`、`ReturnTrayToStorage` |
| 酒水伙伴取酒、直接或投掷上酒 | `GameData/Profile/PartnerWaitressBehaviour.cs`：`OnInitialize` 中的回调、`ExecuteThrowDeliver` |
| 玩家上酒及返还 | `NightScene/UI/GuestManagementUtility/WorkSceneServePannel.cs`：`InvokeOrderUpdate` |
| 伙伴放回库存 | `GameData/Profile/PartnerBase.cs`：`EmployeeAddToInventoryCoroutine` |
| 面板退回及打烊回收 | `NightScene/UI/CookingUtility/WorkSceneBinPannel.cs`；`NightScene/GuestManagementUtility/GuestsManager.cs`：`DisposeTray`、`DisposeAllFoods` |

实现前还需核对实例在托盘、伙伴持有物、投掷中订单及联机消息之间的复制方式，确保免费标记不会丢失；上述路径中的 LINQ 逆向结果部分仅有模式还原，不能视为运行时验证。重点检查结界前取酒后退回、结界内取酒后过期退回、伙伴投掷途中结束、连续施放、其他免费效果叠加和打烊回收。

## 源码依据

以下路径相对于逆向仓库的 `src/Assembly-CSharp/`；本机位置见 `AGENTS.local.md`。

- `GameData/Core/Collections/NightSceneUtility/SkillCollection/Spell_Medicine.cs`：`SetExtraCookTag`、结束清理。
- `NightScene/UI/CookingUtility/WorkSceneCookingSelectionPannel.cs`：`MatchedCookCombo.GetResult` 写入额外 tag；`GameData/Profile/PartnerCookBehaviour.cs` 也调用此入口。
- `NightScene/CookingUtility/CookController.cs`：玩家 `SetCook` 与伙伴 `GetTrueCookingTime` 都经过烹饪乘数，并限制最短 0.01 秒。
- `NightScene/EventUtility/EventManager.cs`：`CookTimeAndOrderRateEditByTag` 的条件检查、计时清理；`IsFreeBevServe` 的免费酒水状态。
- `NightScene/UI/GuestManagementUtility/WorkSceneServePannel.cs`：`InvokeOrderUpdate` 中的清酒返还入口。
- `GameData/Profile/PartnerWaitressBehaviour.cs`：直接上酒回调与 `ExecuteThrowDeliver` 落地后的免费酒水分支。实际 Interop 目标分别为 `_OnInitialize_b__14_6` 和 `_ExecuteThrowDeliver_d__41.MoveNext`，作用域不跨协程等待。
- `NightScene/GuestManagementUtility/GuestsManager.cs`：`AllPresentedGuestGroupController` 包含已生成且尚未完全离场的客群。
- `NightScene/GuestManagementUtility/GuestGroupController.cs`：`AddExtraOrderCount(-1, false)` 同时改变剩余次数和额外上限。
- `NightScene/UI/UIManager.cs`：`ExecuteThrowDeliver` 在飞行结束后隐藏并归还动画对象。
- `GameData/Core/Collections/NightSceneUtility/SpellExecutionContext.cs`：`GuestPosition` 与实际触发角色实例的区别；`SpellBase.cs`：执行入口的离场检查。

联机客机只播放黑卡演出，点单额度由主机修改。未新增专用同步消息；客机额度显示及完整联机效果仍需实测。

## 构建与检查

- 贴图：`python tools/spell-vfx/scripts/minoriko/gen_textures.py`。
- 特效包：`pwsh tools/spell-vfx/scripts/build.ps1 -Spell Minoriko -Probe`，Unity 路径由 `UNITY_EXE` 提供。
- 美术预览：Unity 批处理运行 `MinorikoPreview.Run`，须启用图形设备；输出位于 `tools/spell-vfx/Build/Minoriko/`。
- 构建产物复制到资源包 `assets/Spell/10001`；生成的 `Build/Minoriko/10001.png` 复制到 `assets/Buff/10001.png`。

本次同时修正工具工程的空排序层写法，以及加色 shader 的混合因子与预乘颜色不匹配的问题。加色 shader 变化只在重新构建对应 AssetBundle 后生效，已有舞的包未重打。

验证范围：模组编译、Unity 构建、粒子分布、编辑器静态渲染与包内资源检查。未启动游戏；30 秒计时、QTE、重复施放、库存与联机效果尚未进行游戏内验证。
