using System;
using System.Collections.Generic;

using MetaMystia.Network;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

using DEYU.AssetHandleUtility;
using GameData.Core.Collections.CharacterUtility;
using GameData.Core.Collections.NightSceneUtility;
using GameData.CoreLanguage;

using MetaMiku;
using MetaMystia.Patch;
using MetaMystia.ResourceEx.SpellCollection;

namespace MetaMystia;

public static partial class ResourceExManager
{
    public static void SpellTest()
    {
        const int spellId = 8999;
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

    // 所有已注册符卡的角色 ID（包含主角色和 ResourceEx 别名）
    private static readonly HashSet<int> _registeredSpellIds = new();

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
            Spell_Shinki.LoadFlagSprite();
            Spell_Shinki.LoadBuffIcon();
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
        langs[0] = new LanguageBase("魔神降临", "神绮开启无尽的魔界传送门，持续召唤魔界客人");
        langs[1] = new LanguageBase("绮符【环游魔界80天】", "神绮邀请所有客人前往魔界游玩");
        GameData.CoreLanguage.Collections.DataBaseLanguage.SpellLang[shinkiCharacterId] = langs;

        // 诊断：确认 C# 侧注册的符卡名称是否正确
        UnityEngine.Debug.Log($"[MetaMystia] SpellLang registered for id={shinkiCharacterId}: " +
            $"红卡='{langs[0].Name}', 黑卡='{langs[1].Name}'");

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
        _registeredSpellIds.Add(shinkiCharacterId);

        // === 注册自定义 BuffDescription 文本（供 RegisterTimedBuff 显示） ===
        NativeBuffHelper.RegisterCustomBuffDescription(
            NativeBuffHelper.BT.Null,
            title: "魔神降临",
            description: "每隔15秒从魔界传送门中随机召唤两位魔界人");

        IzakayaCloseAction.RegisterOnIzakayaClose(Spell_Shinki.CleanupPortal);

        _shinkiSpellRegistered = true;
        _shinkiRegisteredId = shinkiCharacterId;

        // === Also register for ResourceEx Shinki if it exists in SpecialGuest ===
        var rexConfig = TryFindCharacterConfigByName("神绮");
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
                         sid.Contains("神绮")))
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
                _registeredSpellIds.Add(rexId);
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

    public static void RegisterDaiyouseiSpell()
    {
        const int spellId = 9000;
        const string portraitUri = "rex://ResourceExample/assets/Character/9000/Portrait/0.png";

        ClassInjector.RegisterTypeInIl2Cpp<Spell_Daiyousei>();
        var spell = ScriptableObject.CreateInstance<Spell_Daiyousei>();

        Spell_Daiyousei.LoadBuffIcon();

        var spellHandle = new Common.SceneDirector.RuntimeHandle<SpellBase>(spell);
        DataBaseNight.SpecialGuestSpell[spellId] = spellHandle.Cast<IAssetHandle<SpellBase>>();

        var langs = new Il2CppReferenceArray<LanguageBase>(2);
        langs[0] = new LanguageBase("妖精的呼朋引伴", "大妖精从朋友中召唤一位稀客到场");
        langs[1] = new LanguageBase("雾符【妖精的薄雾】", "用餐区被神秘的薄雾笼罩，持续30秒");
        GameData.CoreLanguage.Collections.DataBaseLanguage.SpellLang[spellId] = langs;

        if (TryGetSprite(portraitUri, out var portraitSprite) && portraitSprite != null)
        {
            var spriteAssetHandle = new Common.SceneDirector.RuntimeHandle<Sprite>(portraitSprite)
                .Cast<IAssetHandle<Sprite>>();
            var tuple = new Il2CppSystem.ValueTuple<IAssetHandle<Sprite>, IAssetHandle<Sprite>>(
                spriteAssetHandle, spriteAssetHandle);
            DataBaseNight.SpecialGuestSpellPortrayal.ForceAddOrUpdateValueTuple(spellId, tuple);
        }
        else
        {
            Log.Warning($"RegisterDaiyouseiSpell: 加载立绘 sprite 失败 {portraitUri}");
        }

        DataBaseCharacter.CharacterHasSpell[spellId] = true;
        _registeredSpellIds.Add(spellId);
        _daiyouseiSpellRegistered = true;
        _daiyouseiRegisteredId = spellId;
        Log.LogInfo($"[MetaMystia] Daiyousei spell registered for id={spellId}");
    }

    private static bool _daiyouseiSpellRegistered;
    private static int _daiyouseiRegisteredId;

    public static bool IsDaiyouseiSpellRegistered() => _daiyouseiSpellRegistered;
    public static bool IsDaiyouseiCharacterId(int id) => _daiyouseiSpellRegistered && _daiyouseiRegisteredId == id;

    /// <summary>
    /// 统一检查：给定 ID 是否属于任何已注册符卡的角色（含 ResourceEx 别名）。
    /// </summary>
    public static bool IsCustomSpellCharacter(int id) => _registeredSpellIds.Contains(id);

    public static bool AutoRegisterShinkiSpell()
    {
        bool TryRegister(int id, string label)
        {
            RegisterShinkiSpell(id, label, ShinkiPositivePortraitUri, ShinkiNegativePortraitUri);
            return true;
        }

        // === 策略 1: stringId 匹配（原有逻辑） ===
        var specialGuests = DataBaseCharacter.SpecialGuest;
        if (specialGuests != null)
        {
            foreach (var kvp in specialGuests)
            {
                var sid = kvp.Value.stringId;
                if (!string.IsNullOrEmpty(sid) &&
                    (sid.IndexOf("Shinki", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     sid.Contains("神绮")))
                {
                    Log.Info($"AutoRegisterShinkiSpell: found via stringId, id={kvp.Key}, stringId={sid}");
                    return TryRegister(kvp.Key, sid);
                }
            }
        }

        // === 策略 2: DataBaseLanguage 显示名称匹配 ===
        var langDb = GameData.CoreLanguage.Collections.DataBaseLanguage.SpecialGuest;
        if (langDb != null)
        {
            foreach (var kvp in langDb)
            {
                try
                {
                    var displayName = kvp.Value.Item1;
                    if (!string.IsNullOrEmpty(displayName) && displayName.Contains("神绮"))
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

        // === 策略 3: DLC5 ID 范围兜底 ===
        if (specialGuests != null && langDb != null)
        {
            foreach (var kvp in specialGuests)
            {
                if (kvp.Key < 5000 || kvp.Key > 5015) continue;
                try
                {
                    var nameEntry = langDb[kvp.Key];
                    var name = nameEntry.Item1;
                    if (!string.IsNullOrEmpty(name) && name.Contains("神绮"))
                    {
                        var label = kvp.Value.stringId ?? $"Guest_{kvp.Key}";
                        Log.Info($"AutoRegisterShinkiSpell: found via DLC5 range, id={kvp.Key}, stringId={label}");
                        return TryRegister(kvp.Key, label);
                    }
                }
                catch (Exception) { /* key not found in language db, skip */ }
            }
        }

        // === 全部失败：dump 诊断信息到 Player.log ===
        Log.Warning("AutoRegisterShinkiSpell: ALL STRATEGIES FAILED — could not find Shinki");

        // ResourceEx config fallback
        var config = TryFindCharacterConfigByName("神绮");
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
