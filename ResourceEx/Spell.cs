using System;

using MetaMystia.Network;
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
        ClassInjector.RegisterTypeInIl2Cpp<Spell_Test>();
        var spell = ScriptableObject.CreateInstance<Spell_Test>();

        var spellHandle = new Common.SceneDirector.RuntimeHandle<SpellBase>(spell);
        DataBaseNight.SpecialGuestSpell[spellId] = spellHandle.Cast<IAssetHandle<SpellBase>>();

        // 2. 注册符卡名称和描述。通常只有两个版本，秦心(额外含有喜怒哀乐等子符卡)等除外
        var langs = new Il2CppReferenceArray<LanguageBase>(2);
        langs[0] = new LanguageBase("大妖精 红卡", "测试符卡 - 红卡");
        langs[1] = new LanguageBase("大妖精 黑卡", "测试符卡 - 黑卡");
        GameData.CoreLanguage.Collections.DataBaseLanguage.SpellLang[spellId] = langs;

        // 3. 通过 rex 管线加载立绘并注册为符卡立绘。
        if (TryGetSprite(portraitUri, out var portraitSprite) && portraitSprite != null)
        {
            var spriteAssetHandle = new Common.SceneDirector.RuntimeHandle<Sprite>(portraitSprite)
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

    private static bool _shinkiSpellRegistered;
    private static bool _shinkiSpellInstanceCreated;
    private static int _shinkiRegisteredId = -1;
    private static int _shinkiResourceExId = -1;
    private static Spell_Shinki _shinkiSpellInstance;
    private static Common.SceneDirector.RuntimeHandle<SpellBase> _shinkiSpellHandle;

    public static void RegisterShinkiSpell(int shinkiCharacterId, string shinkiLabel, string portraitUri = null)
        => RegisterShinkiSpell(shinkiCharacterId, shinkiLabel, portraitUri, portraitUri);

    public static void RegisterShinkiSpell(int shinkiCharacterId, string shinkiLabel, string positivePortraitUri, string negativePortraitUri)
    {
        if (_shinkiSpellRegistered) return;

        if (!_shinkiSpellInstanceCreated)
        {
            ClassInjector.RegisterTypeInIl2Cpp<Spell_Shinki>();
            _shinkiSpellInstance = ScriptableObject.CreateInstance<Spell_Shinki>();
            _shinkiSpellHandle = new Common.SceneDirector.RuntimeHandle<SpellBase>(_shinkiSpellInstance);
            Spell_Shinki.SetShinkiLabel(shinkiLabel);
            Spell_Shinki.SetShinkiCharacterId(shinkiCharacterId);
            Spell_Shinki.ResolveCharacterIds();
            _shinkiSpellInstanceCreated = true;
        }

        if (DataBaseNight.SpecialGuestSpell == null)
        {
            Log.Warning("RegisterShinkiSpell: SpecialGuestSpell is still null at EventManager.Initialize");
            return;
        }

        CompleteShinkiSpellRegistration(shinkiCharacterId, positivePortraitUri, negativePortraitUri);
    }

    private static void CompleteShinkiSpellRegistration(int shinkiCharacterId, string positivePortraitUri, string negativePortraitUri)
    {
        if (_shinkiSpellRegistered) return;

        DataBaseNight.SpecialGuestSpell[shinkiCharacterId] = _shinkiSpellHandle.Cast<IAssetHandle<SpellBase>>();

        var langs = new Il2CppReferenceArray<LanguageBase>(2);
        langs[0] = new LanguageBase("\u9B54\u795E\u964D\u4E34", "\u795E\u7EE6\u5F00\u542F\u65E0\u5C3D\u7684\u9B54\u754C\u4F20\u9001\u95E8\uFF0C\u6301\u7EED\u53EC\u5524\u9B54\u754C\u5BA2\u4EBA");
        langs[1] = new LanguageBase("\u7EE6\u7B26\u300C\u73AF\u6E38\u9B54\u754C80\u5929\u300D", "\u795E\u7EE6\u9080\u8BF7\u6240\u6709\u5BA2\u4EBA\u524D\u5F80\u9B54\u754C\u6E38\u73A9");
        GameData.CoreLanguage.Collections.DataBaseLanguage.SpellLang[shinkiCharacterId] = langs;

        IAssetHandle<Sprite> LoadPortraitHandle(string uri, string label)
        {
            UnityEngine.Debug.Log($"[MetaMystia] LoadPortraitHandle({label}): uri='{uri}'");
            if (string.IsNullOrEmpty(uri))
            {
                UnityEngine.Debug.Log($"[MetaMystia] LoadPortraitHandle({label}): uri is null/empty");
                return null;
            }
            if (!TryGetSprite(uri, out var s))
            {
                UnityEngine.Debug.Log($"[MetaMystia] LoadPortraitHandle({label}): TryGetSprite returned false");
                return null;
            }
            if (s == null)
            {
                UnityEngine.Debug.Log($"[MetaMystia] LoadPortraitHandle({label}): sprite is null");
                return null;
            }
            UnityEngine.Debug.Log($"[MetaMystia] LoadPortraitHandle({label}): success, sprite={s.name}, size={s.rect.size}");
            return new Common.SceneDirector.RuntimeHandle<Sprite>(s).Cast<IAssetHandle<Sprite>>();
        }

        var positiveHandle = LoadPortraitHandle(positivePortraitUri, "positive");
        var negativeHandle = LoadPortraitHandle(negativePortraitUri, "negative");
        var fallback = positiveHandle ?? negativeHandle;
        if (fallback != null)
        {
            var tuple = new Il2CppSystem.ValueTuple<IAssetHandle<Sprite>, IAssetHandle<Sprite>>(
                positiveHandle ?? fallback, negativeHandle ?? fallback);
            DataBaseNight.SpecialGuestSpellPortrayal.ForceAddOrUpdateValueTuple(shinkiCharacterId, tuple);
            UnityEngine.Debug.Log($"[MetaMystia] SpellPortrayal registered for id={shinkiCharacterId}");
        }
        else
        {
            UnityEngine.Debug.Log($"[MetaMystia] RegisterShinkiSpell: no portrait loaded for id={shinkiCharacterId}");
        }


        DataBaseCharacter.CharacterHasSpell[shinkiCharacterId] = true;
        IzakayaCloseAction.RegisterOnIzakayaClose(Spell_Shinki.CleanupPortal);

        _shinkiSpellRegistered = true;
        _shinkiRegisteredId = shinkiCharacterId;

        // === Also register for ResourceEx Shinki if it exists in SpecialGuest ===
        var rexConfig = TryFindCharacterConfigByName("\u795e\u7ee6");
        var rexId = rexConfig?.id ?? -1;
        if (rexId == shinkiCharacterId || rexId == -1)
        {
            var sg = DataBaseCharacter.SpecialGuest;
            if (sg != null)
            {
                foreach (var kvp in sg)
                {
                    if (kvp.Key == shinkiCharacterId) continue;
                    var sid = kvp.Value?.stringId;
                    if (!string.IsNullOrEmpty(sid) &&
                        (sid.IndexOf("Shinki", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         sid.Contains("\u795e\u7ee6")))
                    {
                        rexId = kvp.Key;
                        break;
                    }
                }
            }
        }
        if (rexId > 0 && rexId != shinkiCharacterId)
        {
            _shinkiResourceExId = rexId;
            Spell_Shinki.SetShinkiResourceExId(rexId);
            try
            {
                DataBaseNight.SpecialGuestSpell[rexId] = _shinkiSpellHandle.Cast<IAssetHandle<SpellBase>>();
                DataBaseCharacter.CharacterHasSpell[rexId] = true;
                GameData.CoreLanguage.Collections.DataBaseLanguage.SpellLang[rexId] = langs;
                if (fallback != null)
                {
                    var rexTuple = new Il2CppSystem.ValueTuple<IAssetHandle<Sprite>, IAssetHandle<Sprite>>(
                        positiveHandle ?? fallback, negativeHandle ?? fallback);
                    DataBaseNight.SpecialGuestSpellPortrayal.ForceAddOrUpdateValueTuple(rexId, rexTuple);
                    UnityEngine.Debug.Log($"[MetaMystia] SpellPortrayal also registered for rexId={rexId}");
                }
            }
            catch (Exception ex)
            {
                Log.Warning($"RegisterShinkiSpell: ResourceEx alias registration failed: {ex.Message}");
            }
        }
    }

    public static bool IsShinkiSpellRegistered() => _shinkiSpellRegistered;
    public static bool IsShinkiCharacterId(int id) => _shinkiSpellRegistered && _shinkiRegisteredId == id;
    public static bool IsShinkiResourceExId(int id) => _shinkiSpellRegistered && _shinkiResourceExId == id;
    public static int GetShinkiResourceExId() => _shinkiResourceExId;
    public static void SetShinkiResourceExId(int id) => _shinkiResourceExId = id;
    public static SpellBase GetShinkiSpellInstance() => _shinkiSpellInstance;

    private const string ShinkiPositivePortraitUri = "rex://ResourceExample/assets/Character/9004/Portrait/0.png";
    private const string ShinkiNegativePortraitUri = "rex://ResourceExample/assets/Character/9004/Portrait/2.png";

    public static bool AutoRegisterShinkiSpell()
    {
        bool TryRegister(int id, string label)
        {
            RegisterShinkiSpell(id, label, ShinkiPositivePortraitUri, ShinkiNegativePortraitUri);
            return true;
        }

        // === \u7B56\u7565 1: stringId \u5339\u914D\uFF08\u539F\u6709\u903B\u8F91\uFF09 ===
        var specialGuests = DataBaseCharacter.SpecialGuest;
        if (specialGuests != null)
        {
            foreach (var kvp in specialGuests)
            {
                var sid = kvp.Value.stringId;
                if (!string.IsNullOrEmpty(sid) &&
                    (sid.IndexOf("Shinki", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     sid.Contains("\u795E\u7EE6")))
                {
                    Log.Info($"AutoRegisterShinkiSpell: found via stringId, id={kvp.Key}, stringId={sid}");
                    return TryRegister(kvp.Key, sid);
                }
            }
        }

        // === \u7B56\u7565 2: DataBaseLanguage \u663E\u793A\u540D\u79F0\u5339\u914D ===
        var langDb = GameData.CoreLanguage.Collections.DataBaseLanguage.SpecialGuest;
        if (langDb != null)
        {
            foreach (var kvp in langDb)
            {
                try
                {
                    var displayName = kvp.Value.Item1;
                    if (!string.IsNullOrEmpty(displayName) && displayName.Contains("\u795E\u7EE6"))
                    {
                        var id = kvp.Key;
                        var refGuest = DataBaseCharacter.RefSGuest(id);
                        var label = (refGuest != null && !string.IsNullOrEmpty(refGuest.stringId))
                            ? refGuest.stringId
                            : $"Guest_{id}";
                        Log.Info($"AutoRegisterShinkiSpell: found via display name, id={id}, label={label}");
                        return TryRegister(id, label);
                    }
                }
                catch (Exception) { /* skip entries that can't be read */ }
            }
        }

        // === \u7B56\u7565 3: DLC5 ID \u8303\u56F4\u515C\u5E95 ===
        if (specialGuests != null && langDb != null)
        {
            foreach (var kvp in specialGuests)
            {
                if (kvp.Key < 5000 || kvp.Key > 5015) continue;
                try
                {
                    var nameEntry = langDb[kvp.Key];
                    var name = nameEntry.Item1;
                    if (!string.IsNullOrEmpty(name) && name.Contains("\u795E\u7EE6"))
                    {
                        var label = kvp.Value.stringId ?? $"Guest_{kvp.Key}";
                        Log.Info($"AutoRegisterShinkiSpell: found via DLC5 range, id={kvp.Key}, stringId={label}");
                        return TryRegister(kvp.Key, label);
                    }
                }
                catch (Exception) { /* key not found in language db, skip */ }
            }
        }

        // === \u5168\u90E8\u5931\u8D25\uFF1Adump \u8BCA\u65AD\u4FE1\u606F\u5230 Player.log ===
        Log.Warning("AutoRegisterShinkiSpell: ALL STRATEGIES FAILED \u2014 could not find Shinki");

        // ResourceEx config fallback
        var config = TryFindCharacterConfigByName("\u795E\u7EE6");
        if (config != null)
        {
            var label = config.label;
            var refGuest = DataBaseCharacter.RefSGuest(config.id);
            if (refGuest != null && !string.IsNullOrEmpty(refGuest.stringId)) label = refGuest.stringId;
            return TryRegister(config.id, label);
        }

        return false;
    }
}