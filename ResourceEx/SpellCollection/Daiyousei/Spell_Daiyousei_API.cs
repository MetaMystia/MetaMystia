using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using DEYU.AssetHandleUtility;
using GameData.Core.Collections.CharacterUtility;
using GameData.Core.Collections.NightSceneUtility;
using GameData.CoreLanguage;
using MetaMiku;
using MetaMystia.ResourceEx.SpellCollection.Daiyousei;
using MetaMystia.UI;

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

        // 1. 注册类型并向 il2cpp 域注入，随后创建托管侧实例（CreateInstance 依赖上一步注入）。
        ClassInjector.RegisterTypeInIl2Cpp<Spell_Daiyousei>();
        var spell = ScriptableObject.CreateInstance<Spell_Daiyousei>();

        // 2. 以稀客 id 为 key 登记符卡（key 即绑定关系：游戏按稀客 id 查得此符卡）。
        var spellHandle = new Common.SceneDirector.RuntimeHandle<SpellBase>(spell);
        DataBaseNight.SpecialGuestSpell[daiyouseiGuestId] = spellHandle.Cast<IAssetHandle<SpellBase>>();

        // 3. 注册符卡名称与描述，文本走 L10n（规则 32 i18n，不硬编码中文）。
        var langs = new Il2CppReferenceArray<LanguageBase>(spellLanguageVersionCount);
        langs[0] = new LanguageBase(TextId.Spell_Daiyousei_NameRed.Get(), TextId.Spell_Daiyousei_DescRed.Get());
        langs[1] = new LanguageBase(TextId.Spell_Daiyousei_NameBlack.Get(), TextId.Spell_Daiyousei_DescBlack.Get());
        GameData.CoreLanguage.Collections.DataBaseLanguage.SpellLang[daiyouseiGuestId] = langs;

        // 4. 通过 rex 管线加载示例立绘并注册（真实素材待 U6f 转正）。
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

        // 5. 标记该稀客拥有符卡，使夜场流程能正确识别并可被宣言。
        DataBaseCharacter.CharacterHasSpell[daiyouseiGuestId] = true;
    }
}
