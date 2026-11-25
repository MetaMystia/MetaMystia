using System.Collections.Generic;
using System.Linq;

using Il2CppSystem.Linq;
using UnityEngine;

using GameData.Core.Collections.CharacterUtility;
using GameData.Core.Collections.NightSceneUtility;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;

using MetaMystia.Multiplayer.Guests;
using MetaMystia.Multiplayer.Messages;
using SgrYuki.Utils;

using Random = UnityEngine.Random;

namespace MetaMystia.Multiplayer.Spells;

public enum SpellKind : byte { Positive, Negative, Leave }

/// <summary>
/// 一次符卡执行。主机推进原版协程，开始时广播，正文第一步之后广播随机种子与选中的桌子、厨具；
/// 客机按广播把同一张卡排入本地符卡队列，正文等到主机结果后执行。
/// </summary>
[AutoLog]
public sealed partial class SpellRun
{
    /// <summary>正在推进正文的符卡，只在正文的一步之内有值。</summary>
    public static SpellRun Current { get; private set; }

    public int RunId { get; init; }
    public int RuntimeId { get; init; }
    /// <summary>符卡所属稀客，施法者自己的卡为空。古明地觉会代放他人的卡。</summary>
    public int? SpellOwner { get; init; }
    public SpellKind Kind { get; init; }
    public bool RecordSpellNum { get; init; }
    /// <summary>客机按主机广播建立的执行。</summary>
    public bool Replica { get; init; }
    public SpellExecutionContext Context { get; internal set; }
    /// <summary>客机：正文比主机晚开始的秒数，登记限时 buff 时扣除。</summary>
    public int Lag { get; private set; }

    private readonly Scope scope = Scopes.Scene;
    /// <summary>主机：正文第一步的选择。客机：尚未取用的主机选择。</summary>
    private readonly List<int[]> choices = new();
    private bool planned;
    private bool seeded;
    private int seed;
    private int choiceCount;
    private float plannedAt;
    private Random.State random;

    /// <summary>客机：排入原版符卡队列，出队时建立上下文。</summary>
    public void Enqueue()
    {
        System.Func<Il2CppSystem.Action, Il2CppSystem.Collections.IEnumerator> start = Start;
        EventManager.Instance.ScheduleSpellExecution(start);
    }

    private Il2CppSystem.Collections.IEnumerator Start(Il2CppSystem.Action onFinish)
    {
        var controller = GuestRegistry.Current.Find(RuntimeId)?.Controller?.TryCast<SpecialGuestsController>();
        if (controller == null)
        {
            Log.Warning($"{this} skipped, guest missing locally");
            SpellRegistry.Current.Remove(this);
            onFinish?.Invoke();
            return SpellRegistry.Nothing();
        }
        var owner = SpellOwner is { } id ? DataBaseCharacter.RefSGuest(id) : controller.SpecialGuest;
        var spell = DataBaseNight.WorkSceneGetSpell(owner.Id);
        Context = new SpellExecutionContext(controller, controller.SpecialGuest, controller.guestInstances,
            owner.GetSpellLanguage(), onFinish, RecordSpellNum);
        // 代放的卡在施法者自己的卡上已计次数。
        if (SpellOwner != null) return Kind == SpellKind.Positive ? spell.ExecutePositiveBuff(Context) : spell.ExecuteNegativeBuff(Context);
        return Kind switch
        {
            SpellKind.Positive => spell.ExecutePositiveBuffAsync(Context),
            SpellKind.Negative => spell.ExecuteNegativeBuffAsync(Context),
            _ => spell.ExecuteOnLeaveBuffAsync(Context),
        };
    }

    /// <summary>客机：收到主机结果。</summary>
    public void Plan(int hostSeed, int[][] hostChoices)
    {
        choices.AddRange(hostChoices);
        Seed(hostSeed);
        plannedAt = Time.time;
        planned = true;
    }

    /// <summary>
    /// 推进原版执行协程（ExecutePositiveBuff 等）：施法者不在则结束；自动宣言；正文；结束事件与回调。
    /// 正文是单独的托管协程，其中抛出的异常只结束正文，不会中断符卡队列。
    /// </summary>
    public System.Collections.IEnumerator Execute(SpellBase spell, Il2CppSystem.Collections.IEnumerator execution)
    {
        KeepPosition();
        if (!execution.MoveNext())
        {
            SpellRegistry.Current.Remove(this);
            yield break;
        }
        if (!Replica) SpellBeginMessage.Send(this);
        if (Kind != SpellKind.Leave && spell.ShouldCallSpellDeclarationAuto(Kind == SpellKind.Positive))
        {
            yield return execution.Current;
            KeepPosition();
            execution.MoveNext();
        }
        if (!Replica || !SpellRegistry.HostOnlyBody(spell, Kind))
        {
            while (Replica && !planned && GameSession.HasRoomPeers && !scope.Ended) yield return null;
            if (Replica && planned) Lag = (int)(Time.time - plannedAt);
            yield return Body(execution.Current.Cast<Il2CppSystem.Collections.IEnumerator>());
        }
        SpellRegistry.Current.Remove(this);
        execution.MoveNext();
    }

    private System.Collections.IEnumerator Body(Il2CppSystem.Collections.IEnumerator body)
    {
        if (!Replica) Seed(Random.Range(int.MinValue, int.MaxValue));
        bool more;
        try
        {
            more = Step(body);
        }
        finally
        {
            if (!Replica)
            {
                planned = true;
                SpellPlanMessage.Send(RunId, seed, choices);
            }
        }
        while (more)
        {
            yield return Nested(body.Current);
            more = Step(body);
        }
    }

    /// <summary>正文等待的子协程同样逐步推进。</summary>
    private object Nested(Il2CppSystem.Object current) =>
        current?.TryCast<Il2CppSystem.Collections.IEnumerator>() is { } nested ? Drive(nested) : current;

    private System.Collections.IEnumerator Drive(Il2CppSystem.Collections.IEnumerator routine)
    {
        while (Step(routine)) yield return Nested(routine.Current);
    }

    /// <summary>
    /// 推进一步。两端使用同一条随机序列，不影响正文以外的随机数。
    /// 客机正文对顾客数值的改动以主机为准，立即还原。
    /// </summary>
    private bool Step(Il2CppSystem.Collections.IEnumerator routine)
    {
        KeepPosition();
        var outer = Current;
        var state = Random.state;
        Current = this;
        if (seeded) Random.state = random;
        try
        {
            return routine.MoveNext();
        }
        finally
        {
            if (seeded) random = Random.state;
            Random.state = state;
            Current = outer;
            if (Replica) GuestRegistry.Current.RestoreStats();
        }
    }

    private void Seed(int value)
    {
        seed = value;
        var state = Random.state;
        Random.InitState(seed);
        random = Random.state;
        Random.state = state;
        seeded = true;
    }

    /// <summary>
    /// 客机落后时施法者可能已离场销毁，位置为空，原版会跳过正文或取空值。
    /// 改为按不记录次数的执行处理，位置取玩家。
    /// </summary>
    private void KeepPosition()
    {
        if (Replica && Context.ShouldRecordSpellNum && Context.GuestCharacterInstances?.All(character => character != null) != true)
            Context._ShouldRecordSpellNum_k__BackingField = false;
    }

    /// <summary>客机：取主机在正文第一步选出的桌子或厨具。</summary>
    public bool TryReplayChoice(ref Il2CppSystem.Collections.Generic.IEnumerable<int> result)
    {
        if (!Replica || choices.Count == 0) return false;
        var list = new Il2CppSystem.Collections.Generic.List<int>();
        foreach (var value in choices[0]) list.Add(value);
        choices.RemoveAt(0);
        result = list.ToIEnumerable();
        return true;
    }

    /// <summary>主机记下正文第一步选出的桌子或厨具。选择消耗的随机数两端可能不同，之后重新对齐。</summary>
    public void AfterChoice(Il2CppSystem.Collections.Generic.IEnumerable<int> result)
    {
        if (!Replica && !planned) choices.Add(result.ToArray());
        Random.InitState(unchecked(seed + ++choiceCount));
    }

    public override string ToString() => $"Spell #{RunId} {Kind} by guest #{RuntimeId}{(SpellOwner is { } id ? $" (of {id})" : "")}";
}
