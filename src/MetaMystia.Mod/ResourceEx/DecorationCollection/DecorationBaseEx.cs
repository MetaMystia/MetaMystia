using Il2CppInterop.Runtime.Attributes;

using GameData.Profile;

namespace MetaMystia.ResourceEx.DecorationCollection;

/// <summary>
/// 自定义装饰基类：只负责效果。名称、说明、图标、类型由资源包 decorations 声明，
/// DecorationRegistry 按 implementation 创建实例并作为原版 Decoration.SpecialBuff。
///
/// 夜间：重写原版 DecorationBuffEnterNight，由原版开业流程调用（普通营业只调用 Outdoor）。
/// EventManager 与 CookSystemManager 随夜间场景销毁，在其上的登记和订阅无需手动清理。
///
/// 白天：原版装饰没有白天效果。这里只声明加成数值，由 DecorationRegistry.ApplyDayEffects
/// 按当前装备集合汇总并直接赋值，不做增量加减，原版重置数值后重算即可恢复。
///
/// 本类会被 ClassInjector 注入 il2cpp，新增成员必须标注 [HideFromIl2Cpp]，
/// 不得声明抽象成员（见 docs/reference/il2cppinterop-defects.md）。
/// </summary>
public abstract class DecorationBaseEx : DecorationBase
{
    /// <summary>白天玩家移速加成，与等级移速同单位（每级 0.12）。</summary>
    [HideFromIl2Cpp] public virtual float DayMoveSpeedBonus => 0f;
}
