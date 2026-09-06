using System.Collections.Generic;

using UnityEngine;

using DayScene.Interactables.Collections.BehaviourComponents;
using DayScene.Interactables.Collections.ConditionComponents;
using GameData.RunTime.Common;
using GameData.RunTime.DaySceneUtility;

public static class Payload
{
    public static object Execute()
    {
        var rows = new List<string> { $"map={DayScene.SceneManager.Instance.CurrentActiveMapLabel} AP={RunTimeDayScene.RemainActions} clock={DayScene.UI.UIManager.Instance.GetTimeCode(RunTimeDayScene.RemainActions)} fund={RunTimePlayerData.GetFund()} totalDonation={RunTimePlayerData.GetHakureiMoneyBoxDonateNum} nightBuffQueue={NightScene.SceneManager.AdditiveBuff.Count}" };
        var box = Object.FindObjectOfType<HakureiMoneyBoxBehaviourComponent>();
        if (box != null) rows.Add($"BOX used={StatusTracker.Instance.GetComponentNum(box.m_ModuleID)} collider={box.GetComponent<Collider2D>().enabled} finalAmount={box.finalDonateNum}");
        var exp = RunTimeAlbum.GetCharacterKizuna(7, out var max, out var level);
        rows.Add($"REIMU level={level} exp={exp}/{max}");
        foreach (var c in Object.FindObjectsOfType<CharacterConditionComponent>())
        {
            if (c.name != "比那名居天子") continue;
            var id = RunTimeAlbum.RefSpecialNPCId(c.CharacterLabel);
            exp = RunTimeAlbum.GetCharacterKizuna(id, out max, out level);
            rows.Add($"TENSHI label={c.CharacterLabel} id={id} level={level} exp={exp}/{max} attempted={StatusTracker.Instance.HasTemptInvited(c.CharacterLabel)} invited={StatusTracker.Instance.InvitedGuests.Contains(id)} position={c.transform.position}");
        }
        return string.Join("\n", rows);
    }
}
