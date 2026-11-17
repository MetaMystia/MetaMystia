using System;

using GameData.Core.Collections;
using GameData.RunTime.Common;
using NightScene.EventUtility;

using MetaMystia.Patch;
using MetaMystia.ResourceEx.SpellCollection;

using IntList = Il2CppSystem.Collections.Generic.List<int>;
using StockItem = Il2CppSystem.Collections.Generic.KeyValuePair<GameData.Core.Collections.Sellable, int>;

var checks = 0;
void Check(bool value, string message)
{
    if (!value) throw new Exception(message);
    checks++;
    Console.WriteLine("PASS " + message);
}

var sake = new Sellable(10);
var other = new Sellable(20, false);
DataBaseCore.Beverages[10] = sake;
DataBaseCore.Beverages[20] = other;
RunTimeStorage.Stock[10] = 2;
RunTimeStorage.Stock[20] = 2;
Check(!RunTimeStoragePatch.BeverageOut_Prefix(RunTimeStorage.GREEN_TEA_ID),
    "结界外取绿茶直接返回，不进入原生跳板");
RunTimeStorage.BeverageOut(10);
Check(RunTimeStorage.CountBeverage(10) == 1, "结界外取酒正常扣库存");
var panel = new WorkSceneStoragePannelPatch.Panel { openType = Sellable.SellableType.Beverage };
WorkSceneStoragePannelPatch.instanceRef = panel;
EventManager.Instance.HarvestActive = true;
MinorikoSake.SetActive(true);
Check(MinorikoSake.IsUnlimited(sake) && !MinorikoSake.IsUnlimited(other),
    "清酒标签 6 无限，单独低酒精标签 0 不无限");
Check(!RunTimeStoragePatch.BeverageOut_Prefix(RunTimeStorage.GREEN_TEA_ID),
    "结界内取绿茶直接返回，不进入原生跳板");
Check(panel.Refreshes == 1 && panel.ActiveInStorageGroup.Refreshes == 1, "结界开始刷新酒架");
var display = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<StockItem>([
    new(sake, 1), new(other, 2)]);
RunTimeStoragePatch.GetAllBeverages_Postfix(display);
Check(display[0].Value == -1 && display[1].Value == 2 && RunTimeStorage.CountBeverage(10) == 1,
    "仅清酒显示无限，真实库存不变");
Check(!MinorikoSake.IsUnlimited(new Sellable(0))
    && !MinorikoSake.IsUnlimited(new Sellable(10) { Type = Sellable.SellableType.Food }),
    "保留绿茶原逻辑，料理不受影响");
RunTimeStorage.BeverageOut(10);
RunTimeStorage.BeverageOutRange(new IntList { 10, 10, 20 });
Check(RunTimeStorage.CountBeverage(10) == 1 && RunTimeStorage.CountBeverage(20) == 1,
    "单份与批量消耗均不扣清酒，其他酒正常扣除");
RunTimeStorage.BeverageInRange(new IntList { 10, 10, 20 });
Check(RunTimeStorage.CountBeverage(10) == 1 && RunTimeStorage.CountBeverage(20) == 2,
    "所有清酒入库均忽略，其他酒正常入库");
EventManager.Instance.HarvestActive = false;
Check(!MinorikoSake.IsActive, "原版结界状态消失即停止无限供应");
MinorikoSake.SetActive(false);
Check(panel.Refreshes == 2, "结界结束刷新酒架");
RunTimeStorage.BeverageOut(10);
RunTimeStorage.Return(10);
Check(RunTimeStorage.CountBeverage(10) == 1, "结束后恢复普通取用和入库");
display[0] = new(sake, 1);
RunTimeStoragePatch.GetAllBeverages_Postfix(display);
Check(display[0].Value == 1, "结束后显示真实库存");
Console.WriteLine($"ALL PASS ({checks} assertions; game ports simulated)");
