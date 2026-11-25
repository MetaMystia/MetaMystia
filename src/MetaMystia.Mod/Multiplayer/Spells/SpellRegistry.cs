using System;
using System.Collections.Generic;
using System.Linq;

using Il2CppSystem.Linq;
using BepInEx.Unity.IL2CPP.Utils.Collections;

using GameData.Core.Collections.CharacterUtility;
using GameData.Core.Collections.NightSceneUtility;
using GameData.Core.Collections.NightSceneUtility.SkillCollection;
using GameData.RunTime.Common;
using NightScene.EventUtility;

using MetaMystia.Multiplayer.Guests;

namespace MetaMystia.Multiplayer.Spells;

/// <summary>当前场景正在执行的符卡，存于 <see cref="Scopes.Scene"/>。</summary>
public sealed class SpellRegistry
{
    private readonly Dictionary<int, SpellRun> runs = new();
    private int nextId = 1;

    public static SpellRegistry Current => Scopes.Scene.Get<SpellRegistry>();

    public void Add(SpellRun run) => runs[run.RunId] = run;

    public void Remove(SpellRun run) => runs.Remove(run.RunId);

    public SpellRun Find(int runId) => runs.GetValueOrDefault(runId);

    /// <summary>客机：该顾客的符卡尚未演完。</summary>
    public bool IsCasting(int runtimeId) => runs.Values.Any(run => run.RuntimeId == runtimeId);

    private SpellRun Find(SpellExecutionContext context)
    {
        foreach (var run in runs.Values)
            if (run.Context?.Pointer == context.Pointer) return run;
        return null;
    }

    /// <summary>主机新建执行，客机取回广播对应的执行；其余（单机、剧情与脚本顾客）保持原版。</summary>
    public void Wrap(SpellBase spell, SpellKind kind, SpellExecutionContext context,
        ref Il2CppSystem.Collections.IEnumerator execution)
    {
        var run = Find(context);
        if (run == null)
        {
            if (GuestRegistry.Mode(context.GuestsController) != GuestMode.Host) return;
            run = new SpellRun
            {
                RunId = nextId++,
                RuntimeId = GuestRegistry.Of(context.GuestsController).RuntimeId,
                SpellOwner = OwnerOf(spell, context.CharacterData),
                Kind = kind,
                RecordSpellNum = context.ShouldRecordSpellNum,
                Context = context,
            };
            Add(run);
        }
        execution = run.Execute(spell, execution).WrapToIl2Cpp();
    }

    private static int? OwnerOf(SpellBase spell, SpecialGuest caster)
    {
        if (DataBaseNight.WorkSceneGetSpell(caster.Id).Pointer == spell.Pointer) return null;
        foreach (var id in RunTimeAlbum.GetAllRecordedSpecialGuests().ToArray())
            if (DataBaseCharacter.RefSGuest(id).HasSpell && DataBaseNight.WorkSceneGetSpell(id).Pointer == spell.Pointer) return id;
        return null;
    }

    /// <summary>
    /// 客机只播宣言、正文由主机执行的卡：代放他人符卡（古明地觉），使用生成的顾客（二岩猯藏、露易兹红卡），
    /// 弹出选图面板、按稀客池放逐（绵月丰姬）。
    /// </summary>
    public static bool HostOnlyBody(SpellBase spell, SpellKind kind) =>
        spell.TryCast<Spell_Satori>() != null || spell.TryCast<Spell_Toyohime>() != null
        || kind == SpellKind.Positive && (spell.TryCast<Spell_Mamizou>() != null || spell.TryCast<Spell_Luize>() != null);

    /// <summary>在本地符卡队列中执行，排在已排队的符卡之后。</summary>
    public static void Defer(Action action)
    {
        Func<Il2CppSystem.Action, Il2CppSystem.Collections.IEnumerator> spell = onFinish =>
        {
            action();
            onFinish?.Invoke();
            return Nothing();
        };
        EventManager.Instance.ScheduleSpellExecution(spell);
    }

    public static Il2CppSystem.Collections.IEnumerator Nothing() => Empty().WrapToIl2Cpp();

    private static System.Collections.IEnumerator Empty()
    {
        yield break;
    }
}
