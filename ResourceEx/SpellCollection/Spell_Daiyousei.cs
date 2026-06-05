using System.Collections.Generic;
using System.Linq;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

using GameData.Core.Collections.NightSceneUtility;
using GameData.Core.Collections.NightSceneUtility.SkillCollection;
using NightScene.EventUtility;
using NightScene.GuestManagementUtility;
using MetaMystia.Patch;

namespace MetaMystia.ResourceEx.SpellCollection;

[AutoLog]
public partial class Spell_Daiyousei : SpellBase
{
    // ===================================================================
    //  手动三次贝塞尔曲线，用于模拟琪露诺的特效飞行轨迹。
    //  B(t) = (1-t)³·P0 + 3(1-t)²·t·P1 + 3(1-t)·t²·P2 + t³·P3
    // ===================================================================
    private static Vector3 CubicBezier(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        float u = 1f - t;
        float uu = u * u;
        float tt = t * t;
        return uu * u * p0 + 3 * uu * t * p1 + 3 * u * tt * p2 + tt * t * p3;
    }

    // ===================================================================
    //  符卡接口
    // ===================================================================
    public override string OnGettingSpellOwnerIdentifier() => "_ResourceExample_Daiyousei";

    public override Il2CppSystem.Collections.IEnumerator OnPositiveBuffExecute(SpellExecutionContext ctx)
        => PositiveBuffRoutine(ctx).WrapToIl2Cpp();
    public override Il2CppSystem.Collections.IEnumerator OnNegativeBuffExecute(SpellExecutionContext ctx)
        => NegativeBuffRoutine(ctx).WrapToIl2Cpp();

    // ===================================================================
    //  红卡 — 「妖精的呼朋引伴」
    // ===================================================================
    [HideFromIl2Cpp]
    /// 判断指定 ID 的稀客当前是否已在食堂中（已入座 or 排队中）
    private static bool IsSpecialGuestPresent(int guestId)
    {
        // 检查已入座的客人（遍历所有已入座客人组）
        var seated = GuestsManager.Instance.AllGuestInDeskController;
        if (seated != null)
        {
            int count = Il2CppSystem.Linq.Enumerable.Count<GuestGroupController>(seated);
            for (int i = 0; i < count; i++)
            {
                var group = Il2CppSystem.Linq.Enumerable.ElementAt<GuestGroupController>(seated, i);
                if (group.ControllType == GuestsManager.GuestType.Special)
                {
                    var sc = group.TryCast<SpecialGuestsController>();
                    if (sc != null && sc.SpecialGuest.Id == guestId)
                        return true;
                }
            }
        }

        // 检查排队的客人（List 直接用 .Count 和 [i] 遍历，无需 LINQ）
        var queued = GuestGroupController.QueuedGuestControllers;
        if (queued != null)
        {
            for (int i = 0; i < queued.Count; i++)
            {
                var group = queued[i];
                if (group.ControllType == GuestsManager.GuestType.Special)
                {
                    var sc = group.TryCast<SpecialGuestsController>();
                    if (sc != null && sc.SpecialGuest.Id == guestId)
                        return true;
                }
            }
        }

        return false;
    }

    private System.Collections.IEnumerator PositiveBuffRoutine(SpellExecutionContext ctx)
    {
        const int WRIGGLE = 0, RUMIA = 1, CIRNO = 28, KEINE = 4; // 硬编码角色 ID
        var bakaIds = new[] { WRIGGLE, RUMIA, CIRNO }; // 目前已经实装的三个笨蛋，以后可能还会有更多？

        // 检查哪些笨蛋当前在场
        var present = bakaIds.Where(id => IsSpecialGuestPresent(id)).ToArray();
        bool allBakaPresent = present.Length == bakaIds.Length;
        bool keinePresent = IsSpecialGuestPresent(KEINE);

        if (allBakaPresent && !keinePresent)
        {
            InviteGuest(KEINE, "上白泽慧音");
        }
        else if (allBakaPresent && keinePresent)
        {
            yield return GiveFruitsRoutine(ctx);
        }
        else
        {
            // 邀请一个当前不在场的妖精
            var absent = bakaIds.Where(id => !IsSpecialGuestPresent(id)).ToArray();
            if (absent.Length > 0)
            {
                var pick = absent[UnityEngine.Random.Range(0, absent.Length)];
                var names = new Dictionary<int, string> { { WRIGGLE, "莉格露" }, { RUMIA, "露米娅" }, { CIRNO, "琪露诺" } };
                InviteGuest(pick, names[pick]);
            }
            else
            {
                yield return GiveFruitsRoutine(ctx);
            }
        }
    }

    /// 邀请指定 ID 的稀客
    private static void InviteGuest(int guestId, string guestName)
    {
        if (guestId != 4) Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage($"大妖精邀请 {guestName} 来食堂了！");
        else
            Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage($"上白泽慧音来抓翘课的不听话的孩子们了！");
        Log.Info($"[Spell_Test] 邀请 GuestId={guestId} ({guestName})");

        GuestsManager.Instance.SpawnSpecialGuestGroup(
            guestId,
            SpecialGuestsController.GuestSpawnType.Normal,
            new Il2CppSystem.Nullable<Vector3>(),
            null,
            GuestGroupController.LeaveType.Move,
            false,
            -1,
            false,
            null,
            true
        );

        // 标记为已生成，防止游戏再自然刷新一个同款
        GameData.RunTime.NightSceneUtility.IzakayaConfigure.Instance.SetThisGuestHasSpawned(guestId);
    }

    /// 仿造琪露诺的红卡的送水果（或许可以把 ResourceEx 里面的椰子加进去？）
    /// 现阶段使用琪露诺的 prefab，后续可以考虑重绘一个大妖精版本的飞行道具和落地特效（目前直接复用琪露诺的雪花特效，虽然不太符合大妖精的风格但总比没有强）
    private System.Collections.IEnumerator GiveFruitsRoutine(SpellExecutionContext ctx)
    {
        // ---- 第 0 步：借资源 ----
        var cirno = SpellAssetBorrower.Borrow<Spell_Cirno>("Spell_Test");
        var chen = SpellAssetBorrower.Borrow<Spell_Chen>("Spell_Test");

        var front = NightScene.UI.UIManager.Instance.UiAnimationFront;

        // ---- 选物品（三种水果每种 2~5 个，动画仍是 3 件飞行） ----
        // SelectFromDatabase 的第三个参数是数量，直接指定随机个数
        int peachCount = UnityEngine.Random.Range(2, 6);
        int grapeCount = UnityEngine.Random.Range(2, 6);
        int lemonCount = UnityEngine.Random.Range(2, 6);

        var selections = new List<EventManager.SelectedValue>();
        selections.Add(this.Manager.SelectFromDatabase(EventManager.InventoryIOType.Ingredient, 21, peachCount));
        selections.Add(this.Manager.SelectFromDatabase(EventManager.InventoryIOType.Ingredient, 36, grapeCount));
        selections.Add(this.Manager.SelectFromDatabase(EventManager.InventoryIOType.Ingredient, 2001, lemonCount));
        Log.Info($"[Spell_Test] 送水果: 桃×{peachCount} 葡萄×{grapeCount} 柠檬×{lemonCount}");

        Vector3 origin = ctx.GuestPosition.HasValue
            ? ctx.GuestPosition.Value + new Vector3(0f, 0.5f, 0f)
            : SpellBase.GetPlayerPosition(center: true);
        Vector3 target = this.GetFoodStoragePosition();

        if (cirno?.iceInAirSFX != null) this.PlayAudio(cirno.iceInAirSFX);
        float flyDur = cirno?.iceInAirDuration ?? 1f;

        // ---- 飞行道具 ----
        var flightData = new List<(GameObject go, Vector3 cp1, Vector3 cp2)>();
        if (cirno != null) for (int i = 0; i < selections.Count; i++)
        {
            var sel = selections[i];
            var icon = Il2CppSystem.Linq.Enumerable.ElementAt(sel.Text, 0)?.Visual;

            var go = Object.Instantiate(cirno.giveIceItem, front);
            go.transform.position = origin;
            go.transform.localScale = Vector3.one * 0.7f;
            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && icon != null) sr.sprite = icon;

            float angle = i * 120 + UnityEngine.Random.Range(-20f, 20f);
            float dash = UnityEngine.Random.Range(cirno.itemMinDashDistance, cirno.itemMaxDashDistance);
            var cp1 = origin + Quaternion.AngleAxis(cirno.itemControlPoint1AngularOffset + angle, Vector3.forward) * Vector3.up * dash;
            var cp2 = origin + Quaternion.AngleAxis(cirno.itemControlPoint2AngularOffset + angle, Vector3.forward) * Vector3.up * dash;
            flightData.Add((go, cp1, cp2));
        }

        // ---- 同时飞行 ----
        float elapsed = 0f;
        while (elapsed < flyDur)
        {
            float t = elapsed / flyDur;
            foreach (var d in flightData)
                if (d.go != null) d.go.transform.position = CubicBezier(t, origin, d.cp1, d.cp2, target);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // ---- 落地 ----
        for (int i = 0; i < selections.Count; i++)
        {
            if (flightData.Count > i && flightData[i].go != null)
            {
                flightData[i].go.transform.position = target;
                Object.Destroy(flightData[i].go);
            }
            if (cirno?.endIceEffect != null)
            {
                Vector3 screenPos = Camera.main.WorldToScreenPoint(target);
                var eff = Object.Instantiate(cirno.endIceEffect, front);
                eff.transform.position = new Vector3(screenPos.x, screenPos.y, 0f);
                Object.Destroy(eff, 1f);
            }
            this.Manager.InventoryIn(selections[i]);
            yield return new WaitForSeconds(0.1f);
        }

        if (cirno?.itemGetSFX != null) this.PlayAudio(cirno.itemGetSFX);
        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage("全员集合！大妖精送上水果拼盘！");
        Log.Info("Spell_Test+: 送水果完成");
    }

    // ===================================================================
    //  黑卡 — 已实现？但是名字叫什么呢？
    // ===================================================================
    [HideFromIl2Cpp]
    private System.Collections.IEnumerator NegativeBuffRoutine(SpellExecutionContext ctx)
    {
        // ---- 注册 Buff ----
        var buffType = (EventManager.BuffType)9003;
        var existingVisual = GameData.CoreLanguage.Collections.DataBaseLanguage.BuffDescription[EventManager.BuffType.PhilosopherStone]?.Visual;
        var desc = new GameData.CoreLanguage.ObjectLanguageBase(
            "雾符「我也不知道这个符卡该叫什么名字！」",
            "大妖精在食堂里释放了迷雾！顾客区视野受阻 30 秒",
            existingVisual);
        GameData.CoreLanguage.Collections.DataBaseLanguage.BuffDescription[buffType] = desc;
        EventManager.Instance.RegisterCountedBuff(buffType, 1, EventManager.MathOperation.Add, null, null);

        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage("来自雾之湖的白色雾气笼罩食堂！30秒！");

        const int fogDuration = 30;
        const int count = 20;
        const float areaWidth = 50f;
        const float areaHeight = 30f;
        const float spriteScale = 5f;

        // 铺一个大范围雾区，确保不管在哪个位置都能覆盖屏幕
        var root = new GameObject("FogEffect");
        root.transform.position = Vector3.zero;

        var fogCopies = new GameObject[count];
        var velocities = new Vector3[count];

        // 20 块雾按 5×4 网格均匀排列 + 每格内微随机偏移
        int cols = 5;
        float cellW = areaWidth / cols;
        float cellH = areaHeight / cols;

        for (int i = 0; i < count; i++)
        {
            var fog = UnityEngine.Object.Instantiate(
                ResourceEx.AssetBundles.Test.TestObj, root.transform);

            int row = i / cols;
            int col = i % cols;

            // 网格中心 + 每格内 ±30% 随机偏移
            float baseX = -areaWidth / 2 + (col + 0.5f) * cellW;
            float baseY = -areaHeight / 2 + (row + 0.5f) * cellH;
            float offsetX = UnityEngine.Random.Range(-cellW * 0.3f, cellW * 0.3f);
            float offsetY = UnityEngine.Random.Range(-cellH * 0.3f, cellH * 0.3f);
            fog.transform.localPosition = new Vector3(baseX + offsetX, baseY + offsetY, 0f);

            // 随机缩放（稍微不同增加层次感）
            var scale = spriteScale * UnityEngine.Random.Range(0.8f, 1.2f);
            fog.transform.localScale = new Vector3(scale, scale, 1f);

            // 随机漂移速度（很慢，方向随机）
            velocities[i] = new Vector3(
                UnityEngine.Random.Range(-0.15f, 0.15f),
                UnityEngine.Random.Range(-0.08f, 0.08f),
                0f
            );

            // 设置 UI 层
            var r = fog.GetComponent<Renderer>();
            if (r != null)
            {
                r.sortingLayerName = "UI";
                r.sortingOrder = 0;
            }

            fogCopies[i] = fog;
        }

        // 持续 12 秒雾气弥漫
        for (var t = 0f; t < fogDuration; t += Time.deltaTime)
        {
            for (int i = 0; i < count; i++)
            {
                var pos = fogCopies[i].transform.localPosition;

                // 漂移
                pos += velocities[i] * Time.deltaTime;

                // 超出边界后从另一侧补回（无缝循环）
                if (pos.x > areaWidth / 2) pos.x -= areaWidth;
                if (pos.x < -areaWidth / 2) pos.x += areaWidth;
                if (pos.y > areaHeight / 2) pos.y -= areaHeight;
                if (pos.y < -areaHeight / 2) pos.y += areaHeight;

                fogCopies[i].transform.localPosition = pos;
            }

            yield return null;
        }

        UnityEngine.Object.Destroy(root);
    }
}
