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
    /// <summary>
    /// 通用符卡注册：CreateInstance → 注册到 DataBase → 注册立绘 → 标记。
    /// 所有符卡共用此方法，只需提供 ID、类型、语言数据和立绘 URI。
    ///
    /// 使用示例：
    ///   RegisterSpell&lt;Spell_Example&gt;(
    ///       spellId: 9002,
    ///       positiveName: "红卡名", positiveDesc: "红卡描述",
    ///       negativeName: "黑卡名", negativeDesc: "黑卡描述",
    ///       portraitUri: "rex://.../Portrait/0.png");
    /// </summary>
    private static void RegisterSpell<T>(
        int spellId,
        string positiveName,
        string positiveDesc,
        string negativeName,
        string negativeDesc,
        string portraitUri = null,
        float portraitContentScale = 1f) where T : SpellBase
    {

        // 1. 注册 Spell_Test 并新建实例
        //    - RegisterTypeInIl2Cpp 把这个托管类型登记到 il2cpp 域，让 il2cpp 认识它的 vtable，
        //      之后 ScriptableObject.CreateInstance<T>() 才能在 Unity native 侧真正造出 Spell_Test
        //      子类实例，OnPositiveBuffExecute 等 override 才能被游戏 native 调用命中。
        //    - 这一步【必须】保留：实测注释掉后 CreateInstance<T>() 会抛
        //      `MethodInfoStoreGeneric_CreateInstance_Public_Static_T_0\`1` 的 TypeInitializationException
        //      （内部 NullReferenceException），原因是 il2cpp 找不到对应的 Class。
        //    - 实例本身不需要托管侧静态字段保活：下面塞进 DataBaseNight.SpecialGuestSpell 的
        //      RuntimeHandle 已经在 il2cpp 侧持有强引用，Unity native 对象不会被回收。
        ClassInjector.RegisterTypeInIl2Cpp<T>();
        var spell = ScriptableObject.CreateInstance<T>();

        var spellHandle = new Common.SceneDirector.RuntimeHandle<SpellBase>(spell);
        DataBaseNight.SpecialGuestSpell[spellId] = spellHandle.Cast<IAssetHandle<SpellBase>>();

        // 2. 注册符卡名称和描述。通常只有两个版本，秦心(额外含有喜怒哀乐等子符卡)等除外
        var langs = new Il2CppReferenceArray<LanguageBase>(2);
        langs[0] = new LanguageBase(positiveName, positiveDesc);
        langs[1] = new LanguageBase(negativeName, negativeDesc);
        GameData.CoreLanguage.Collections.DataBaseLanguage.SpellLang[spellId] = langs;

        // 3. 注册立绘（可选，portraitUri 传 null 则跳过） ----
        if (portraitUri != null && TryGetSprite(portraitUri, out var portraitSprite) && portraitSprite != null)
        {
            // pivot=(0.5, 0.65) 把锚点抬高到约胸部位置，游戏 SpellDeclareCutinCharacter
            // 会自动将 Image 的 pivot 对齐到 sprite pivot，使得上半身居中、下半身被裁切，
            // 实现"符卡立绘"效果。
            var pivot = new Vector2(0.5f, 0.65f);
            var resizedSprite = CreateSpellPortraitSprite(portraitSprite, pivot, portraitContentScale);

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
        else if (portraitUri != null)
        {
            Log.Warning($"RegisterSpell<{typeof(T).Name}>: 加载立绘 sprite 失败 {portraitUri}");
        }

        // 4. 标记该角色拥有符卡，让夜场流程能正确识别。
        DataBaseCharacter.CharacterHasSpell[spellId] = true;
        Log.Info($"RegisterSpell<{typeof(T).Name}>: 已注册 {spellId} 号符卡");
    }

    private static Sprite CreateSpellPortraitSprite(Sprite source, Vector2 pivot, float contentScale)
    {
        const float pixelsPerUnit = 100f;
        if (Mathf.Approximately(contentScale, 1f))
        {
            return Sprite.Create(source.texture, source.rect, pivot, pixelsPerUnit);
        }

        var srcWidth = Mathf.RoundToInt(source.rect.width);
        var srcHeight = Mathf.RoundToInt(source.rect.height);
        var dstWidth = Mathf.CeilToInt(srcWidth / contentScale);
        var dstHeight = Mathf.CeilToInt(srcHeight / contentScale);
        var srcPixels = ReadSpritePixels(source, srcWidth, srcHeight);

        var dstTexture = new Texture2D(dstWidth, dstHeight, TextureFormat.RGBA32, false);
        dstTexture.name = $"{source.name}_SpellPortraitScaled";
        dstTexture.filterMode = source.texture.filterMode;
        dstTexture.wrapMode = TextureWrapMode.Clamp;
        dstTexture.hideFlags = HideFlags.HideAndDontSave;

        var dstPixels = new Color[dstWidth * dstHeight];
        for (var i = 0; i < dstPixels.Length; i++)
        {
            dstPixels[i] = Color.clear;
        }

        var offsetX = (dstWidth - srcWidth) / 2;
        var offsetY = (dstHeight - srcHeight) / 2;
        for (var y = 0; y < srcHeight; y++)
        {
            for (var x = 0; x < srcWidth; x++)
            {
                dstPixels[(offsetY + y) * dstWidth + offsetX + x] = srcPixels[y * srcWidth + x];
            }
        }

        dstTexture.SetPixels(dstPixels);
        dstTexture.Apply(false, false);

        var sprite = Sprite.Create(dstTexture, new Rect(0f, 0f, dstWidth, dstHeight), pivot, pixelsPerUnit);
        sprite.name = $"{source.name}_SpellPortraitScaled";
        sprite.hideFlags = HideFlags.HideAndDontSave;
        Log.Info($"CreateSpellPortraitSprite: {source.name} contentScale={contentScale:0.###}, source={srcWidth}x{srcHeight}, canvas={dstWidth}x{dstHeight}");
        return sprite;
    }

    private static Color[] ReadSpritePixels(Sprite sprite, int width, int height)
    {
        var texture = sprite.texture;
        var rect = sprite.rect;
        var x = Mathf.RoundToInt(rect.x);
        var y = Mathf.RoundToInt(rect.y);

        if (texture.isReadable)
        {
            return texture.GetPixels(x, y, width, height);
        }

        var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        var previous = RenderTexture.active;
        try
        {
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;

            var readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            readable.Apply();
            var pixels = readable.GetPixels(x, y, width, height);
            UnityEngine.Object.DestroyImmediate(readable);
            return pixels;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    // ===== 具体符卡注册 =====

    public static void SpellTest()
    {
        RegisterSpell<Spell_Daiyousei>(
            spellId: 9000,
            positiveName: "「妖精的呼朋引伴」",
            positiveDesc: "大妖精喊来了笨蛋们！",
            negativeName: "雾符「我也不知道这个符卡该叫什么名字！」",
            negativeDesc: "大妖精在食堂里释放了迷雾！顾客区视野受阻 30 秒",
            portraitUri: "rex://ResourceExample/assets/Character/9000/Portrait/0.png");
    }

    public static void SpellKoakuma()
    {
        RegisterSpell<Spell_Koakuma>(
            spellId: 9001,
            positiveName: "灵符「遗失典籍的回响」",
            positiveDesc: "小恶魔从图书馆搬来一本百科全书",
            negativeName: "幻符「献给巴瓦鲁的镇魂曲」",
            negativeDesc: "30 秒内料理面板里的食材顺序被打乱，酒水柜里的酒水顺序被打乱，过滤功能不可用，交互的厨具变成随机厨具",
            portraitUri: "rex://ResourceExample/assets/Character/9001/Portrait/0.png");
    }

    public static void SpellShinki()
    {
        RegisterSpell<Spell_Shinki>(
            spellId: 9004,
            positiveName: "「魔神降临」",
            positiveDesc: "神绮开启魔界传送门，每隔 15 秒召唤两位魔界人",
            negativeName: "绮符「环游魔界80天」",
            negativeDesc: "神绮邀请当前客人前往魔界游玩",
            portraitUri: "rex://ResourceExample/assets/Character/9004/Portrait/0.png",
            portraitContentScale: 0.70f);
    }
}
