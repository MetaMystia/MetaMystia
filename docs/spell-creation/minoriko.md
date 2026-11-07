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
| 丰符「大年收获者」 | 丰穣结界基础时长 20 秒，保留原版奖励时长加成与重复触发计时；结束时移除制作加成和持续特效 |
| 料理 | 沿用梅蒂欣的 `ExtraCookTags`：期间新制作的料理附加饱腹，成品保留 tag；已有料理不追溯修改 |
| 秒做 | 按料理原始 `tags` 是否有饱腹判定，不把结界附加的饱腹算进去；制作时间乘数为 0，游戏仍保留 0.01 秒下限及原有 QTE 流程 |
| 清酒 | 以酒水 tag `6` 判定（`0` 为低酒精）。结界期间酒水架显示 `∞`，清酒库存不增不减；结束后恢复普通处理 |
| 秋符「无常秋日与少女之心」 | 立即解除结界。对施放时已生成的客群，各减少 1 次剩余点单额度及额外上限，最低保留 0 次；不撤销当前订单。每个角色各播放一次随机料理飞入动画，不真实上菜、不结算 |

黑卡投掷起点依次取：上下文中仍存在的触发角色、在场的符卡所属角色、玩家位置。所属角色按 `OwnerIdentifier` 匹配，不写死角色 ID。原版 `GuestPosition` 在不记录符卡次数的代触发场景直接返回玩家位置，因此这里读取实际角色实例。原版若在执行入口发现正常触发的客人已消失，会直接结束符卡；此修改不绕过该检查。

## 酒水架无限清酒（#117）

仅按当前结界状态处理，不区分普通酒和免费酒，也不记录单瓶来源。真实库存不改成 `-1`，不使用库存快照。

- 酒水架沿用原版库存列表，不新增未持有的酒水。结界开始和结束时刷新已打开的酒水架；绿茶沿用原版逻辑。
- 结界期间，清酒的单份扣除、批量扣除及所有入库均被过滤。退回、上酒返还、打烊回收和奖励获得统一处理，期间获得的清酒也不累计。
- 结束后立即恢复普通处理。因此，结界内取出、结束后退回会增加库存；结界前取出、期间退回不会补回数量。此边界不作额外追踪。
- 联机消息和冲突回滚暂不改动，完整联机行为待验证。

只 Hook `RunTimeStorage` 的四个方法：`GetAllBeverages` 修改返回的显示数量，`BeverageOut` 阻止单份扣除，`BeverageOutRange` 和 `BeverageInRange` 过滤批量增减。批量扣除不经过单份扣除入口，因此两者分别处理。结界状态读取原版计时 buff，开关时直接刷新酒水架，不另加生命周期 Hook。

## 源码依据

以下路径相对于逆向仓库的 `src/Assembly-CSharp/`；本机位置见 `AGENTS.local.md`。

- `GameData/Core/Collections/NightSceneUtility/SkillCollection/Spell_Medicine.cs`：`SetExtraCookTag`、结束清理。
- `NightScene/UI/CookingUtility/WorkSceneCookingSelectionPannel.cs`：`MatchedCookCombo.GetResult` 写入额外 tag；`GameData/Profile/PartnerCookBehaviour.cs` 也调用此入口。
- `NightScene/CookingUtility/CookController.cs`：玩家 `SetCook` 与伙伴 `GetTrueCookingTime` 都经过烹饪乘数，并限制最短 0.01 秒。
- `NightScene/EventUtility/EventManager.cs`：`CookTimeAndOrderRateEditByTag` 的条件检查、计时清理；`IsFreeBevServe` 的免费酒水状态。
- `NightScene/UI/GuestManagementUtility/WorkSceneServePannel.cs`：`InvokeOrderUpdate` 中的清酒返还入口。
- `GameData/Profile/PartnerWaitressBehaviour.cs`：取酒、直接上酒和投掷上酒均通过 `RunTimeStorage` 增减库存。
- `GameData/RunTime/Common/RunTimeStorage.cs`：单份扣除、批量增减及酒水架数量读取。
- `NightScene/GuestManagementUtility/GuestsManager.cs`：`AllPresentedGuestGroupController` 包含已生成且尚未完全离场的客群。
- `NightScene/GuestManagementUtility/GuestGroupController.cs`：`AddExtraOrderCount(-1, false)` 同时改变剩余次数和额外上限。
- `NightScene/UI/UIManager.cs`：`ExecuteThrowDeliver` 在飞行结束后隐藏并归还动画对象。
- `GameData/Core/Collections/NightSceneUtility/SpellExecutionContext.cs`：`GuestPosition` 与实际触发角色实例的区别；`SpellBase.cs`：执行入口的离场检查。

联机客机只播放黑卡演出，点单额度由主机修改。未新增专用同步消息；客机额度显示及完整联机效果仍需实测。

## 构建与检查

- 库存离线检查：`dotnet run --project src/MetaMystia.Spell.Tests/MetaMystia.Spell.Tests.csproj`。直接编译库存实现与 Hook，使用游戏接口替身覆盖 13 项检查；不验证原生 Hook 安装或游戏表现。
- 贴图：`python tools/spell-vfx/scripts/minoriko/gen_textures.py`。
- 特效包：`pwsh tools/spell-vfx/scripts/build.ps1 -Spell Minoriko -Probe`，Unity 路径由 `UNITY_EXE` 提供。
- 美术预览：Unity 批处理运行 `MinorikoPreview.Run`，须启用图形设备；输出位于 `tools/spell-vfx/Build/Minoriko/`。
- 构建产物复制到资源包 `assets/Spell/10001`；生成的 `Build/Minoriko/10001.png` 复制到 `assets/Buff/10001.png`。

本次同时修正工具工程的空排序层写法，以及加色 shader 的混合因子与预乘颜色不匹配的问题。加色 shader 变化只在重新构建对应 AssetBundle 后生效，已有舞的包未重打。

验证范围：模组编译、Unity 构建、粒子分布、编辑器静态渲染与包内资源检查。用户已确认绿茶跳板修复后不再崩溃；标签 ID 已纠正。持续时间、QTE、重复施放、库存边界与联机效果尚未完成游戏内验证。
