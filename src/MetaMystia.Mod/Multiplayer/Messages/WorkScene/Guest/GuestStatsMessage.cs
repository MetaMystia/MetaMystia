using System;
using System.Collections.Generic;

using MemoryPack;

using NightScene.EventUtility;

using MetaMystia.Multiplayer.Guests;

namespace MetaMystia.Multiplayer.Messages;

[MemoryPackable]
public partial class GuestStats
{
    public int RuntimeId { get; set; }
    public int Mood { get; set; }
    public int Fund { get; set; }
    public int RemainOrderCount { get; set; }
    public int Patient { get; set; }
    public int MaxPatient { get; set; }
}

/// <summary>
/// 主机：顾客数值（心情、资金、剩余点单数、耐心）与房间连击，变化时每帧合并下发，客机直接覆盖。
/// 客机照常本地倒计时，离场仍由主机判定。
/// </summary>
[MemoryPackable]
[AutoLog]
public partial class GuestStatsMessage : MultiplayerMessage
{
    public GuestStats[] Guests { get; set; }
    public bool HasCombo { get; set; }
    public int Combo { get; set; }
    public int LoseComboTimes { get; set; }
    public int LoseComboTimeForPassion { get; set; }
    public int LoseComboGuestSetNum { get; set; }

    protected override BepInEx.Logging.LogLevel OnReceiveLogLevel => BepInEx.Logging.LogLevel.Debug;
    protected override BepInEx.Logging.LogLevel OnSendLogLevel => BepInEx.Logging.LogLevel.Debug;

    [RequireHostSender]
    [ClientOnlyReceive]
    [CheckScene(Common.UI.Scene.WorkScene)]
    public override void OnReceivedDerived()
    {
        var registry = GuestRegistry.Current;
        foreach (var stats in Guests)
        {
            var controller = registry.Find(stats.RuntimeId)?.Controller;
            if (controller == null || !controller.HaveNotLeft()) continue;
            if (controller.Mood != stats.Mood) controller.Mood = stats.Mood;
            controller.GetFund = stats.Fund;
            controller.remainOrderCount = stats.RemainOrderCount;
            controller.MaxPatient = stats.MaxPatient;
            if (controller.CurrentPatient != stats.Patient) controller.SetPatient(stats.Patient);
        }
        if (!HasCombo) return;
        var events = EventManager.Instance;
        events.CurrentCombo = Combo;
        events.LoseComboTimes = LoseComboTimes;
        events.LoseComboTimeForPassion = LoseComboTimeForPassion;
        events.LoseComboGuestSetNum = LoseComboGuestSetNum;
        events.CallExternOnComboUpdate(Combo);
        events.CallExternOnMusicIndexUpdate(events.CurrentMusicLevelHandle.Invoke());
    }

    /// <summary>主机每帧调用：收集与上次下发不同的数值，有变化时发送一条。</summary>
    public static void Flush(GuestRegistry registry)
    {
        List<GuestStats> changed = null;
        foreach (var guest in registry.All)
        {
            var controller = guest.Controller;
            if (guest.Track != GuestTrack.Replicated || guest.IsLeft || controller == null) continue;
            int mood = controller.Mood, fund = controller.GetFund, remain = controller.remainOrderCount;
            int patient = controller.CurrentPatient, maxPatient = controller.MaxPatient;
            if (mood == guest.SentMood && fund == guest.SentFund && remain == guest.SentRemain
                && patient == guest.SentPatient && maxPatient == guest.SentMaxPatient) continue;
            guest.SentMood = mood;
            guest.SentFund = fund;
            guest.SentRemain = remain;
            guest.SentPatient = patient;
            guest.SentMaxPatient = maxPatient;
            (changed ??= new()).Add(new GuestStats
            {
                RuntimeId = guest.RuntimeId,
                Mood = mood,
                Fund = fund,
                RemainOrderCount = remain,
                Patient = patient,
                MaxPatient = maxPatient,
            });
        }
        var events = EventManager.Instance;
        var sent = registry.SentCombo;
        int[] combo = [events.CurrentCombo, events.LoseComboTimes, events.LoseComboTimeForPassion, events.LoseComboGuestSetNum];
        bool comboChanged = !combo.AsSpan().SequenceEqual(sent);
        if (changed == null && !comboChanged) return;
        var message = new GuestStatsMessage { Guests = changed?.ToArray() ?? [] };
        if (comboChanged)
        {
            combo.CopyTo(sent, 0);
            message.HasCombo = true;
            message.Combo = combo[0];
            message.LoseComboTimes = combo[1];
            message.LoseComboTimeForPassion = combo[2];
            message.LoseComboGuestSetNum = combo[3];
        }
        message.Enqueue();
    }
}
