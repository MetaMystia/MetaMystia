using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using DEYU.AssetHandleUtility;
using GameData.Core.Collections.CharacterUtility;
using GameData.Core.Collections.NightSceneUtility;
using GameData.CoreLanguage;
using MetaMiku;
using MetaMystia.ResourceEx.SpellCollection;
using MetaMystia.ResourceEx.SpellCollection.Daiyousei;
using MetaMystia.UI;
using NightScene.EventUtility;

namespace MetaMystia;

public static partial class ResourceExManager
{
    /// <summary>
    /// 注册大妖精符卡，使其可被夜场流程宣言（U6a）。
    /// 对标 SpellTest 四步：类型注入 → SpellLang 名称/描述（L10n）→ 立绘 Portrayal → CharacterHasSpell 标记。
    /// 以稀客 id（9000）为 key 写入 SpecialGuestSpell / CharacterHasSpell，即完成「符卡绑定该稀客」——游戏流程按稀客 id 查符卡（见 DataBaseNight.WorkSceneGetSpell）。
    /// 仅由 ResourceExManager.Initialize 在数据库就绪后调用一次，非每帧调用。
    /// </summary>
    public static void RegisterDaiyouseiSpell()
    {
        const int daiyouseiGuestId = 9000;
        const int spellLanguageVersionCount = 2;
        const string portraitUri = "rex://ResourceExample/assets/Character/9000/Portrait/0.png";

        ClassInjector.RegisterTypeInIl2Cpp<Spell_Daiyousei>();
        var spell = ScriptableObject.CreateInstance<Spell_Daiyousei>();

        var spellHandle = new Common.SceneDirector.RuntimeHandle<SpellBase>(spell);
        DataBaseNight.SpecialGuestSpell[daiyouseiGuestId] = spellHandle.Cast<IAssetHandle<SpellBase>>();

        var langs = new Il2CppReferenceArray<LanguageBase>(spellLanguageVersionCount);
        langs[0] = new LanguageBase(TextId.Spell_Daiyousei_NameRed.Get(), TextId.Spell_Daiyousei_DescRed.Get());
        langs[1] = new LanguageBase(TextId.Spell_Daiyousei_NameBlack.Get(), TextId.Spell_Daiyousei_DescBlack.Get());
        GameData.CoreLanguage.Collections.DataBaseLanguage.SpellLang[daiyouseiGuestId] = langs;

        if (TryGetSprite(portraitUri, out var portraitSprite) && portraitSprite != null)
        {
            var spriteAssetHandle = new Common.SceneDirector.RuntimeHandle<Sprite>(portraitSprite)
                .Cast<IAssetHandle<Sprite>>();
            var tuple = new Il2CppSystem.ValueTuple<IAssetHandle<Sprite>, IAssetHandle<Sprite>>(
                spriteAssetHandle, spriteAssetHandle);
            DataBaseNight.SpecialGuestSpellPortrayal.ForceAddOrUpdateValueTuple(daiyouseiGuestId, tuple);
        }
        else
        {
            Log.Warning($"[Daiyousei] 加载立绘 sprite 失败：{portraitUri}");
        }

        DataBaseCharacter.CharacterHasSpell[daiyouseiGuestId] = true;
    }

    /// <summary>
    /// 注册大妖精符卡的自定义 Buff 描述与图标（U6c），供右下角 Buff 栏显示。
    /// 须与 RegisterDaiyouseiSpell 同调用点（EventManager.Initialize Postfix）调用一次，非每帧。
    /// </summary>
    public static void RegisterDaiyouseiBuff()
    {
        const string buffIconUri = "rex://ResourceExample/assets/Buff/9000_1.png";

        TryGetSprite(buffIconUri, out var buffIcon);
        if (buffIcon == null)
        {
            Log.Warning($"[Daiyousei] 加载 Buff 图标失败：{buffIconUri}");
        }

        SpellHelper.RegisterBuffDescription(
            (EventManager.BuffType)Spell_Daiyousei.DaiyouseiFogBuffType,
            TextId.Spell_Daiyousei_BuffName.Get(),
            TextId.Spell_Daiyousei_BuffDesc.Get(),
            buffIcon);
    }
}
