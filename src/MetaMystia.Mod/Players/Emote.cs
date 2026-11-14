using System.Collections;
using System.Collections.Generic;

using Common.CharacterUtility;
using GameData.Core.Collections.CharacterUtility;
using GameData.Profile;
using UnityEngine;

namespace MetaMystia;

public enum EmoteType : byte
{
    Fire = 1,
    Shake = 2,
}

/// <summary>在角色上播放表情效果。效果取自原版：着火为布都伙伴的灼烧特效，颤抖为剧情中的角色抖动。</summary>
public static class Emote
{
    public const float Duration = 3f;
    private static readonly Vector2 ShakeAmplitude = new(0.5f, 0.5f);
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
        EmoteType.Shake => true,
        _ => false
    };

    /// <summary>同一角色上的同种表情未结束时不重复播放，返回 false。</summary>
    public static bool Play(CharacterControllerUnit unit, EmoteType emote)
    {
        if (unit == null || !IsAvailable(emote) || !playing.Add((unit.GetInstanceID(), emote))) return false;
        PluginHost.Instance.StartManagedCoroutine(emote switch
        {
            EmoteType.Fire => Fire(unit),
            _ => Shake(unit)
        });
        return true;
    }

    private static IEnumerator Fire(CharacterControllerUnit unit)
    {
        var key = (unit.GetInstanceID(), EmoteType.Fire);
        Common.Audio.GlobalAudioManager.Instance.Play(Futo.playerBurntSFX);
        var fire = Object.Instantiate(Futo.firePlayer, unit.transform, false);
        fire.transform.localPosition = new Vector3(0f, 0.75f, 0f);
        yield return new WaitForSeconds(Duration);
        if (fire != null) Object.Destroy(fire);
        playing.Remove(key);
    }

    // 原版 SceneDirector.CharacterShake 会在结束时把角色放回起始位置；联机角色仍在移动，这里只叠加并撤销每帧的偏移。
    private static IEnumerator Shake(CharacterControllerUnit unit)
    {
        var key = (unit.GetInstanceID(), EmoteType.Shake);
        var offset = Vector3.zero;
        for (float t = 0f; t < Duration && unit != null; t += Time.deltaTime)
        {
            var next = new Vector3(
                (Random.value * -0.06f + Random.value * 0.07f + Random.value * 0.15f) * ShakeAmplitude.x,
                (Random.value * 0.12f + Random.value * 0.09f + Random.value * 0.14f) * ShakeAmplitude.y);
            unit.transform.position += next - offset;
            offset = next;
            yield return null;
        }
        if (unit != null) unit.transform.position -= offset;
        playing.Remove(key);
    }
}
