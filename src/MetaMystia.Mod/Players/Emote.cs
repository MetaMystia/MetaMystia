using System.Collections;
using System.Collections.Generic;

using Common.CharacterUtility;
using Common.TimelineExtestion;
using GameData.Core.Collections.CharacterUtility;
using GameData.Profile;
using UnityEngine;
using UnityEngine.Timeline;

namespace MetaMystia;

public enum EmoteType : byte
{
    Fire = 1,
    Shake = 2,
    Jump = 3,
    Henshin = 4,
    Sparkle = 5,
    Smoke = 6,
}

/// <summary>
/// 在角色上播放表情。只改变显示：位移类表情移动贴图子节点，不动角色根节点与碰撞体；特效类表情挂在角色下并按时销毁。
/// </summary>
[AutoLog]
public static partial class Emote
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
        _ => false
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
            _ => Attach(unit, emote, GetStoryPrefab(emote), Vector3.zero)
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
        if (prefab == null) Log.LogWarning($"Emote prefab {name} not found in {key}");
        storyPrefabs[emote] = prefab;
        return prefab;
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
