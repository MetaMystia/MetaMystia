using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

using DEYU.AssetHandleUtility;
using GameData.Core.Collections.CharacterUtility;
using GameData.Core.Collections.NightSceneUtility;
using GameData.CoreLanguage;

using MetaMiku;
using MetaMystia.ResourceEx.SpellCollection;

namespace MetaMystia;

public static partial class ResourceExManager
{
    public static void SpellTest()
    {
        const int spellId = 9000;
        const string portraitUri = "rex://ResourceExample/assets/Character/9000/Portrait/0.png";

        // 1. 注册 Spell_Test 并新建实例，作为 9000 号角色的符卡。
        //    - RegisterTypeInIl2Cpp 把这个托管类型登记到 il2cpp 域，让 il2cpp 认识它的 vtable，
        //      之后 ScriptableObject.CreateInstance<T>() 才能在 Unity native 侧真正造出 Spell_Test
        //      子类实例，OnPositiveBuffExecute 等 override 才能被游戏 native 调用命中。
        //    - 这一步【必须】保留：实测注释掉后 CreateInstance<T>() 会抛
        //      `MethodInfoStoreGeneric_CreateInstance_Public_Static_T_0\`1` 的 TypeInitializationException
        //      （内部 NullReferenceException），原因是 il2cpp 找不到对应的 Class。
        //    - 实例本身不需要托管侧静态字段保活：下面塞进 DataBaseNight.SpecialGuestSpell 的
        //      RuntimeHandle 已经在 il2cpp 侧持有强引用，Unity native 对象不会被回收。
        ClassInjector.RegisterTypeInIl2Cpp<Spell_Daiyousei>();
        var spell = ScriptableObject.CreateInstance<Spell_Daiyousei>();

        var spellHandle = new Common.SceneDirector.RuntimeHandle<SpellBase>(spell);
        DataBaseNight.SpecialGuestSpell[spellId] = spellHandle.Cast<IAssetHandle<SpellBase>>();

        // 2. 注册符卡名称和描述。通常只有两个版本，秦心(额外含有喜怒哀乐等子符卡)等除外
        var langs = new Il2CppReferenceArray<LanguageBase>(2);
        langs[0] = new LanguageBase("「妖精的呼朋引伴」", "大妖精喊来了笨蛋们！");
        langs[1] = new LanguageBase("雾符「我也不知道这个符卡该叫什么名字！」", "大妖精在食堂里释放了迷雾！顾客区视野受阻 30 秒");
        GameData.CoreLanguage.Collections.DataBaseLanguage.SpellLang[spellId] = langs;

        // 3. 通过 rex 管线加载立绘并注册为符卡立绘。
        if (TryGetSprite(portraitUri, out var portraitSprite) && portraitSprite != null)
        {
            // pivot=(0.5, 0.65) 把锚点抬高到约胸部位置，游戏 SpellDeclareCutinCharacter
            // 会自动将 Image 的 pivot 对齐到 sprite pivot，使得上半身居中、下半身被裁切，
            // 实现"符卡立绘"效果。
            var pivot = new Vector2(0.5f, 0.65f);
            var resizedSprite = Sprite.Create(portraitSprite.texture, portraitSprite.rect, pivot, 100f);

            var spriteAssetHandle = new Common.SceneDirector.RuntimeHandle<Sprite>(resizedSprite)
                .Cast<IAssetHandle<Sprite>>();

            // 注意：il2cppinterop 把 Il2CppSystem.ValueTuple 包装为带 16 字节对象头的引用类型，
            // 但底层的 Dictionary<int, ValueTuple<...>> 存的是无头部的纯结构体数据。
            // 直接 `dict[k] = tuple` 会把对象头当成字段数据写进去（Item1 变成会让解引用挂死的
            // 垃圾指针，Item2 变成 null）。改用 MetaMiku.Utils.ForceAddOrUpdateValueTuple，
            // 它会先 il2cpp_object_unbox 拿到真正的数据指针再调用 set_Item。
            var tuple = new Il2CppSystem.ValueTuple<IAssetHandle<Sprite>, IAssetHandle<Sprite>>(
                spriteAssetHandle, spriteAssetHandle);
            DataBaseNight.SpecialGuestSpellPortrayal.ForceAddOrUpdateValueTuple(spellId, tuple);
        }
        else
        {
            Log.Warning($"SpellTest: 加载立绘 sprite 失败 {portraitUri}");
        }

        // 4. 标记该角色拥有符卡，让夜场流程能正确识别。
        DataBaseCharacter.CharacterHasSpell[spellId] = true;
    }
}
