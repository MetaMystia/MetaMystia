using System;
using System.Collections.Generic;

// 库存与 Interop 的离线替身；不加载 Unity，也不验证原生 Hook 安装。
namespace HarmonyLib
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    sealed class HarmonyPatch : Attribute
    {
        public HarmonyPatch(Type type) { }
        public HarmonyPatch(string name) { }
    }
    sealed class HarmonyPrefix : Attribute { }
    sealed class HarmonyPostfix : Attribute { }
}

namespace Il2CppSystem.Collections.Generic
{
    interface IEnumerable<T> : System.Collections.Generic.IEnumerable<T> { }
    sealed class List<T> : System.Collections.Generic.List<T>, IEnumerable<T>
    {
        public List() { }
        public List(IEnumerable<T> values) : base(values) { }
        public TCast Cast<TCast>() => (TCast)(object)this;
    }
    sealed class KeyValuePair<TKey, TValue>(TKey key, TValue value)
    {
        public TKey Key => key;
        public TValue Value => value;
    }
}

namespace Il2CppInterop.Runtime.InteropTypes.Arrays
{
    sealed class Il2CppReferenceArray<T>(T[] items)
    {
        public int Length => items.Length;
        public T this[int index] { get => items[index]; set => items[index] = value; }
    }
}

namespace GameData.Core.Collections
{
    sealed class Sellable(int id, bool sake = true)
    {
        public enum SellableType { Food, Beverage }
        public int Id => id;
        public SellableType Type { get; init; } = SellableType.Beverage;
        public int[] Tags { get; } = sake ? [6] : [0];
    }
    static class DataBaseCore
    {
        public static readonly Dictionary<int, Sellable> Beverages = new();
        public static Sellable RefBeverage(int id) => Beverages.GetValueOrDefault(id);
    }
}

namespace GameData.RunTime.Common
{
    using MetaMystia.Patch;
    using IntList = Il2CppSystem.Collections.Generic.List<int>;
    using IntEnumerable = Il2CppSystem.Collections.Generic.IEnumerable<int>;

    static class RunTimeStorage
    {
        public const int GREEN_TEA_ID = 0;
        public static readonly Dictionary<int, int> Stock = new();
        public static int CountBeverage(int id) => Stock.GetValueOrDefault(id);
        public static void GetAllBeverages() { }
        public static void BeverageOut(int beverageId)
        {
            if (beverageId != GREEN_TEA_ID && RunTimeStoragePatch.BeverageOut_Prefix(beverageId))
                Stock[beverageId] = CountBeverage(beverageId) - 1;
        }
        public static void BeverageOutRange(IntEnumerable beverageIds)
        {
            RunTimeStoragePatch.BeverageOutRange_Prefix(ref beverageIds);
            foreach (var id in beverageIds)
                Stock[id] = CountBeverage(id) - 1;
        }
        public static void BeverageInRange(IntEnumerable beverageIds)
        {
            RunTimeStoragePatch.BeverageInRange_Prefix(ref beverageIds);
            foreach (var id in beverageIds)
                Stock[id] = CountBeverage(id) + 1;
        }
        public static void Return(int id) => BeverageInRange(new IntList { id });
    }
}

namespace NightScene.EventUtility
{
    sealed class EventManager
    {
        public enum BuffType { }
        public static EventManager Instance = new();
        public bool HarvestActive;
        public bool CheckTimedBuffExists(BuffType type) => HarvestActive;
    }
}

namespace MetaMystia.ResourceEx.SpellCollection
{
    static class Spell_Minoriko
    {
        internal const NightScene.EventUtility.EventManager.BuffType HarvestBuff = (NightScene.EventUtility.EventManager.BuffType)10001;
    }
}

namespace MetaMystia.Patch
{
    sealed class AutoLogAttribute : Attribute { }
    static class WorkSceneStoragePannelPatch
    {
        public static Panel instanceRef;
        public sealed class Panel
        {
            public GameData.Core.Collections.Sellable.SellableType openType;
            public int Refreshes;
            public Group ActiveInStorageGroup { get; } = new();
            public void UpdateBevField() => Refreshes++;
        }
        public sealed class Group
        {
            public int Refreshes;
            public void UpdateElementsAndReselect() => Refreshes++;
        }
    }
}
