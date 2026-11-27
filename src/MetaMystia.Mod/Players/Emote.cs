using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Timeline;

using Common.CharacterUtility;
using Common.TimelineExtestion;
using GameData.Core.Collections.CharacterUtility;
using GameData.Profile;

using MetaMystia.UI;

namespace MetaMystia;

public enum EmoteType : byte
{
    Fire = 1,
    Shake = 2,
    Jump = 3,
    Henshin = 4,
    Sparkle = 5,
    Smoke = 6,
    ExGood = 7,
    Good = 8,
    Normal = 9,
    Bad = 10,
    ExBad = 11,
    Heart = 12,
    Heartbreak = 13,
    Exclaim = 14,
}

/// <summary>
/// 在角色上播放表情。只改变显示：位移类表情移动贴图子节点，不动角色根节点与碰撞体；特效类表情挂在角色下并按时销毁；
/// 图标表情显示在头顶。
/// </summary>
public static class Emote
{
    public const float Duration = 3f;
    private static readonly Vector2 ShakeAmplitude = new(0.5f, 0.5f);
    private const float JumpHeight = 0.3f, JumpGravity = -9f;
    private const int JumpCount = 2;

    /// <summary>剧情特效：所在 Timeline 地址与 COM_WorldOverlay_Set 引用的 prefab 名。</summary>
    private static readonly Dictionary<EmoteType, (string Timeline, string Prefab)> storyEffects = new()
    {
        [EmoteType.Henshin] = ("AfterStory_TalkToKeine", "Keine_Henshin"),
        [EmoteType.Sparkle] = ("Main_0_Intro_000_Timeline", "Yuyuko_Flash"),
        [EmoteType.Smoke] = ("DLC3_Challenge_Cooking_Win_Timeline", "Smoke"),
    };
    private static readonly Dictionary<EmoteType, GameObject> storyPrefabs = [];

    /// <summary>图标表情：原版图集地址与精灵名（夜间顾客评价、白天羁绊变化）。</summary>
    private static readonly Dictionary<EmoteType, (string Atlas, string Sprite)> icons = new()
    {
        [EmoteType.ExGood] = ("Common_NpcConvIcons_Atlas", "UI_night_addup_23"),
        [EmoteType.Good] = ("Common_NpcConvIcons_Atlas", "UI_night_addup_24"),
        [EmoteType.Normal] = ("Common_NpcConvIcons_Atlas", "UI_night_addup_25"),
        [EmoteType.Bad] = ("Common_NpcConvIcons_Atlas", "UI_night_addup_26"),
        [EmoteType.ExBad] = ("Common_NpcConvIcons_Atlas", "UI_night_addup_27"),
        [EmoteType.Heart] = ("Day_Atlas", "HeartsUp_32x_0"),
        [EmoteType.Heartbreak] = ("Day_Atlas", "HeartsDown_32x_0"),
        [EmoteType.Exclaim] = ("Work_Atlas", "UI_night_addup_0"),
    };
    private static readonly Dictionary<EmoteType, Sprite> iconSprites = [];
    private static readonly Dictionary<int, GameObject> bubbles = [];
    private const float BubbleGap = 0.1f, BubbleRise = 0.1f, BubblePop = 0.15f, BubbleFade = 0.4f;
    private static readonly HashSet<(int, EmoteType)> playing = [];
    private static PartnerBossInfoFuto futo;

    /// <summary>布都的伙伴资料，属于 DLC3；未加载该 DLC 时为 null。</summary>
    private static PartnerBossInfoFuto Futo
    {
        get
        {
            if (futo != null) return futo;
            foreach (var info in DataBaseCharacter.PartnerInfo.Values)
            {
                futo = info.TryCast<PartnerBossInfoFuto>();
                if (futo != null) break;
            }
            return futo;
        }
    }

    public static bool IsAvailable(EmoteType emote) => emote switch
    {
        EmoteType.Fire => Futo?.firePlayer != null,
        EmoteType.Shake or EmoteType.Jump => true,
        EmoteType.Henshin or EmoteType.Sparkle => GetStoryPrefab(emote) != null,
        EmoteType.Smoke => Futo != null && GetStoryPrefab(emote) != null,
        _ => GetIcon(emote) != null
    };

    /// <summary>同一角色上的同种表情未结束时不重复播放，返回 false。</summary>
    public static bool Play(CharacterControllerUnit unit, EmoteType emote)
    {
        if (unit == null || !IsAvailable(emote) || !playing.Add((unit.GetInstanceID(), emote))) return false;
        PluginHost.Instance.StartManagedCoroutine(emote switch
        {
            EmoteType.Fire => Attach(unit, emote, Futo.firePlayer, new Vector3(0f, 0.75f, 0f)),
            EmoteType.Shake => Shake(unit),
            EmoteType.Jump => Jump(unit),
            EmoteType.Henshin or EmoteType.Sparkle or EmoteType.Smoke => Attach(unit, emote, GetStoryPrefab(emote), Vector3.zero),
            _ => Bubble(unit, emote, GetIcon(emote))
        });
        if (emote == EmoteType.Fire) Common.Audio.GlobalAudioManager.Instance.Play(Futo.playerBurntSFX);
        return true;
    }

    /// <summary>首次使用时加载剧情 Timeline（只读取资源，不播放），取出其中叠加层引用的 prefab。</summary>
    private static GameObject GetStoryPrefab(EmoteType emote)
    {
        if (storyPrefabs.TryGetValue(emote, out var cached)) return cached;
        var (key, name) = storyEffects[emote];
        GameObject prefab = null;
        var timeline = UnityEngine.AddressableAssets.Addressables
            .LoadAssetAsync<TimelineAsset>((Il2CppSystem.Object)(Il2CppSystem.String)key).WaitForCompletion();
        for (int i = 0; timeline != null && prefab == null && i < timeline.outputTrackCount; i++)
        {
            foreach (var clip in timeline.GetOutputTrack(i).clips)
            {
                var overlay = clip.asset?.TryCast<COM_WorldOverlay_Set>();
                if (overlay?.spriteGameObject != null && overlay.spriteGameObject.name == name)
                {
                    prefab = overlay.spriteGameObject;
                    break;
                }
            }
        }
        if (prefab == null) Log.Warning($"Emote prefab {name} not found in {key}");
        storyPrefabs[emote] = prefab;
        return prefab;
    }

    public static Sprite GetIcon(EmoteType emote)
    {
        if (iconSprites.TryGetValue(emote, out var cached)) return cached;
        var sprite = icons.TryGetValue(emote, out var icon) ? OverlayUi.AtlasSprite(icon.Atlas, icon.Sprite) : null;
        iconSprites[emote] = sprite;
        return sprite;
    }

    private static IEnumerator Attach(CharacterControllerUnit unit, EmoteType emote, GameObject prefab, Vector3 localPosition)
    {
        var key = (unit.GetInstanceID(), emote);
        var effect = Object.Instantiate(prefab, unit.transform, false);
        effect.transform.localPosition = localPosition;
        yield return new WaitForSeconds(Duration);
        if (effect != null) Object.Destroy(effect);
        playing.Remove(key);
    }

    // 角色根节点带 SortingGroup，子节点只在组内排序；气泡放在场景根下逐帧跟随，排在 OnTop 层。
    // 高度取角色下可见渲染器的最高处，避开玩家名标签。同一角色同时只显示一个气泡，新气泡替换旧的。
    private static IEnumerator Bubble(CharacterControllerUnit unit, EmoteType emote, Sprite sprite)
    {
        int id = unit.GetInstanceID();
        if (bubbles.TryGetValue(id, out var old) && old != null) Object.Destroy(old);
        var bubble = new GameObject($"Emote_{emote}");
        bubbles[id] = bubble;
        var renderer = bubble.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = "OnTop";
        float top = unit.mainRenderer.bounds.max.y;
        foreach (var child in unit.GetComponentsInChildren<Renderer>())
            top = Mathf.Max(top, child.bounds.max.y);
        float height = top - unit.transform.position.y + sprite.bounds.extents.y + BubbleGap;
        for (float t = 0f; t < Duration && bubble != null && unit != null; t += Time.deltaTime)
        {
            float pop = Mathf.Clamp01(t / BubblePop) - 1f;
            bubble.transform.localScale = Vector3.one * (1f + 2.70158f * pop * pop * pop + 1.70158f * pop * pop);
            bubble.transform.position = unit.transform.position + new Vector3(0f, height + BubbleRise * t / Duration);
            renderer.color = new Color(1f, 1f, 1f, Mathf.Clamp01((Duration - t) / BubbleFade));
            yield return null;
        }
        if (bubble != null) Object.Destroy(bubble);
        if (bubbles.TryGetValue(id, out var current) && current == bubble) bubbles.Remove(id);
        playing.Remove((id, emote));
    }

    /// <summary>平移角色根节点下除阴影外的子节点。</summary>
    private static void OffsetVisual(CharacterControllerUnit unit, Vector3 delta)
    {
        var root = unit.transform;
        var shadow = unit.shadowRenderer != null ? unit.shadowRenderer.transform : null;
        for (int i = 0; i < root.childCount; i++)
        {
            var child = root.GetChild(i);
            if (child != shadow) child.localPosition += delta;
        }
    }

    // 原版 SceneDirector.CharacterShake 直接改根节点位置并在结束时复位；这里只叠加并撤销每帧的偏移。
    private static IEnumerator Shake(CharacterControllerUnit unit)
    {
        var key = (unit.GetInstanceID(), EmoteType.Shake);
        var offset = Vector3.zero;
        for (float t = 0f; t < Duration && unit != null; t += Time.deltaTime)
        {
            var next = new Vector3(
                (Random.value * -0.06f + Random.value * 0.07f + Random.value * 0.15f) * ShakeAmplitude.x,
                (Random.value * 0.12f + Random.value * 0.09f + Random.value * 0.14f) * ShakeAmplitude.y);
            OffsetVisual(unit, next - offset);
            offset = next;
            yield return null;
        }
        if (unit != null) OffsetVisual(unit, -offset);
        playing.Remove(key);
    }

    // 参照 SceneDirector.CharacterJump 的重力跳跃。
    private static IEnumerator Jump(CharacterControllerUnit unit)
    {
        var key = (unit.GetInstanceID(), EmoteType.Jump);
        for (int i = 0; i < JumpCount && unit != null; i++)
        {
            float height = 0f, velocity = Mathf.Sqrt(2f * -JumpGravity * JumpHeight);
            do
            {
                velocity += JumpGravity * Time.deltaTime;
                float next = Mathf.Max(0f, height + velocity * Time.deltaTime);
                OffsetVisual(unit, new Vector3(0f, next - height));
                height = next;
                yield return null;
            } while (height > 0f && unit != null);
        }
        playing.Remove(key);
    }
}
