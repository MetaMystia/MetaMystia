using System;
using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

using GameData.Core.Collections.NightSceneUtility;
using GameData.Core.Collections.NightSceneUtility.SkillCollection;
using NightScene.EventUtility;

namespace MetaMystia.ResourceEx.SpellCollection;

[AutoLog]
public partial class Spell_Test : SpellBase
{
    private static Spell_Cirno cirnoSpell;

    public override string OnGettingSpellOwnerIdentifier()
    {
        return "_ResourceExample_Daiyousei";
    }

    public override Il2CppSystem.Collections.IEnumerator OnPositiveBuffExecute(SpellExecutionContext spellExecutionContext)
        => PositiveBuffRoutine(spellExecutionContext).WrapToIl2Cpp();

    public override Il2CppSystem.Collections.IEnumerator OnNegativeBuffExecute(SpellExecutionContext spellExecutionContext)
        => NegativeBuffRoutine(spellExecutionContext).WrapToIl2Cpp();

    [HideFromIl2Cpp]
    private IEnumerator PositiveBuffRoutine(SpellExecutionContext spellExecutionContext)
    {
        Log.Warning("[Spell_Test] PositiveBuffRoutine start");
        Spell_Cirno cirno = GetCirnoSpell();
        var guestPosition = spellExecutionContext.GuestPosition;
        Vector3 guestPositionValue = guestPosition.HasValue ? guestPosition.Value : SpellBase.GetPlayerPosition(false);
        Vector3 characterPosition = new Vector3(guestPositionValue.x, guestPositionValue.y + 0.5f, guestPositionValue.z);

        Vector3 bevTargetPosition = GetBeverageStoragePosition() + new Vector3(0f, 0.75f, 0f);
        Vector3 iceTargetPosition = GetFoodStoragePosition() + new Vector3(0f, 0.75f, 0f);
        float bevStartAngle = UnityEngine.Random.Range(0f, 360f);
        float iceStartAngle = UnityEngine.Random.Range(135f, 225f) + bevStartAngle;
        float intervalGameObjectTime = cirno != null && cirno.intervalGameObjectTime > 0f ? cirno.intervalGameObjectTime : 0.12f;
        float iceInAirDuration = cirno != null && cirno.iceInAirDuration > 0f ? cirno.iceInAirDuration : 0.85f;
        float itemMinDashDistance = cirno != null && cirno.itemMinDashDistance > 0f ? cirno.itemMinDashDistance : 1.2f;
        float itemMaxDashDistance = cirno != null && cirno.itemMaxDashDistance > 0f ? cirno.itemMaxDashDistance : 2.0f;
        float cp1Offset = cirno != null ? cirno.itemControlPoint1AngularOffset : 45f;
        float cp2Offset = cirno != null ? cirno.itemControlPoint2AngularOffset : 135f;
        int giveBevNum = 1;
        int giveIceNum = 1;
        Log.Warning($"[Spell_Test] guest={guestPositionValue} character={characterPosition} bevTarget={bevTargetPosition} iceTarget={iceTargetPosition}");

        EventCoroutineDelegation.Schedule(SpawnBevItem().WrapToIl2Cpp());
        EventCoroutineDelegation.Schedule(SpawnIceItem().WrapToIl2Cpp());
        Log.Warning("[Spell_Test] scheduled spawn coroutines");

        yield return new WaitForSeconds(iceInAirDuration + 0.4f);
        Log.Warning("[Spell_Test] PositiveBuffRoutine end");
        yield break;

        IEnumerator SpawnBevItem()
        {
            Log.Warning("[Spell_Test] SpawnBevItem start");
            for (int i = 0; i < giveBevNum; i++)
            {
                Log.Warning($"[Spell_Test] SpawnBevItem idx={i}");
                EventManager.SelectedValue giveData = Manager.SelectFromDatabase(
                    EventManager.InventoryIOType.Beverage,
                    1,
                    cirno != null ? cirno.iceAvailableBevTagId : -900,
                    -900,
                    0,
                    cirno != null ? cirno.bevMaxPrice : -1,
                    false);
                GameObject item = CreateSpellItem("Spell_Test_PositiveBev_" + i, Color.cyan);
                item.transform.position = characterPosition;
                Log.Warning($"[Spell_Test] Bev created pos={item.transform.position}");
                float dashDistance = UnityEngine.Random.Range(itemMinDashDistance, itemMaxDashDistance);
                Vector3 controlPoint1 = characterPosition + Quaternion.AngleAxis(cp1Offset + bevStartAngle, Vector3.forward) * Vector3.up * dashDistance;
                Vector3 controlPoint2 = characterPosition + Quaternion.AngleAxis(cp2Offset + bevStartAngle, Vector3.forward) * Vector3.up * dashDistance;
                Func<Vector3> cp1Func = () => controlPoint1;
                Func<Vector3> cp2Func = () => controlPoint2;
                Func<Vector3> targetFunc = () => bevTargetPosition;
                bevStartAngle += UnityEngine.Random.Range(360f / giveBevNum * 0.9f, 360f / giveBevNum * 1.1f);
                Log.Warning($"[Spell_Test] Bev lerp cp1={controlPoint1} cp2={controlPoint2} target={targetFunc()}");
                yield return LerpBezierCubic(item.transform, cp1Func, cp2Func, targetFunc, iceInAirDuration, false);
                Log.Warning($"[Spell_Test] Bev arrived pos={item.transform.position}");
                SpawnEndVfx(item.transform.position, Color.cyan);
                Manager.InventoryIn(giveData);
                Log.Warning("[Spell_Test] Bev InventoryIn");
                UnityEngine.Object.Destroy(item);
                yield return new WaitForSeconds(intervalGameObjectTime);
            }
            Log.Warning("[Spell_Test] SpawnBevItem end");
        }

        IEnumerator SpawnIceItem()
        {
            Log.Warning("[Spell_Test] SpawnIceItem start");
            for (int i = 0; i < giveIceNum; i++)
            {
                Log.Warning($"[Spell_Test] SpawnIceItem idx={i}");
                EventManager.SelectedValue giveData = Manager.SelectFromDatabase(
                    EventManager.InventoryIOType.Ingredient,
                    cirno != null ? cirno.iceItemId : 1,
                    1);
                GameObject item = CreateSpellItem("Spell_Test_PositiveIce_" + i, new Color(1f, 0.85f, 0.2f, 1f));
                item.transform.position = characterPosition;
                Log.Warning($"[Spell_Test] Ice created pos={item.transform.position}");
                float dashDistance = UnityEngine.Random.Range(itemMinDashDistance, itemMaxDashDistance);
                Vector3 controlPoint1 = characterPosition + Quaternion.AngleAxis(cp1Offset + iceStartAngle, Vector3.forward) * Vector3.up * dashDistance;
                Vector3 controlPoint2 = characterPosition + Quaternion.AngleAxis(cp2Offset + iceStartAngle, Vector3.forward) * Vector3.up * dashDistance;
                Func<Vector3> cp1Func = () => controlPoint1;
                Func<Vector3> cp2Func = () => controlPoint2;
                Func<Vector3> targetFunc = () => iceTargetPosition;
                iceStartAngle += UnityEngine.Random.Range(360f / giveIceNum * 0.9f, 360f / giveIceNum * 1.1f);
                Log.Warning($"[Spell_Test] Ice lerp cp1={controlPoint1} cp2={controlPoint2} target={targetFunc()}");
                yield return LerpBezierCubic(item.transform, cp1Func, cp2Func, targetFunc, iceInAirDuration, false);
                Log.Warning($"[Spell_Test] Ice arrived pos={item.transform.position}");
                SpawnEndVfx(item.transform.position, new Color(1f, 0.85f, 0.2f, 1f));
                Manager.InventoryIn(giveData);
                Log.Warning("[Spell_Test] Ice InventoryIn");
                UnityEngine.Object.Destroy(item);
                yield return new WaitForSeconds(intervalGameObjectTime);
            }
            Log.Warning("[Spell_Test] SpawnIceItem end");
        }
    }

    [HideFromIl2Cpp]
    private IEnumerator NegativeBuffRoutine(SpellExecutionContext spellExecutionContext)
    {
        Log.Warning("[Spell_Test] NegativeBuffRoutine start");

        Vector3 guestPositionValue = spellExecutionContext.GuestPosition.HasValue
            ? spellExecutionContext.GuestPosition.Value
            : SpellBase.GetPlayerPosition(false);
        Vector3 characterPosition = new Vector3(guestPositionValue.x, guestPositionValue.y + 1.0f, guestPositionValue.z);
        Vector3 targetPosition = SpellBase.GetPlayerPosition(false) + new Vector3(0f, 2.0f, 0f);
        float startAngle = UnityEngine.Random.Range(180f, 360f);
        Log.Warning($"[Spell_Test] negative guest={guestPositionValue} character={characterPosition} target={targetPosition} startAngle={startAngle}");

        EventCoroutineDelegation.Schedule(SetCameraShake(1.5f, 0.2f, 0.2f));

        for (int i = 0; i < 2; i++)
        {
            Log.Warning($"[Spell_Test] Negative item idx={i}");
            GameObject item = CreateSpellItem("Spell_Test_Negative_" + i, new Color(0.35f, 0.65f, 1f, 1f));
            item.transform.position = characterPosition;
            item.transform.localScale = new Vector3(1f, 1f, 1f);
            Vector3 controlPoint1 = characterPosition + Quaternion.AngleAxis(40f + startAngle, Vector3.forward) * Vector3.up * UnityEngine.Random.Range(1.3f, 2.4f);
            Vector3 controlPoint2 = characterPosition + Quaternion.AngleAxis(140f + startAngle, Vector3.forward) * Vector3.up * UnityEngine.Random.Range(1.3f, 2.4f);
            Func<Vector3> cp1Func = () => controlPoint1;
            Func<Vector3> cp2Func = () => controlPoint2;
            Func<Vector3> targetFunc = () => targetPosition + new Vector3(i == 0 ? -0.2f : 0.2f, 0f, 0f);
            Log.Warning($"[Spell_Test] Negative lerp cp1={controlPoint1} cp2={controlPoint2} target={targetFunc()}");
            yield return LerpBezierCubic(item.transform, cp1Func, cp2Func, targetFunc, 0.75f, false);
            Log.Warning($"[Spell_Test] Negative arrived pos={item.transform.position}");
            SpawnEndVfx(item.transform.position, new Color(0.55f, 0.8f, 1f, 1f));
            UnityEngine.Object.Destroy(item);
            yield return new WaitForSeconds(0.1f);
            startAngle += UnityEngine.Random.Range(60f, 100f);
        }

        Log.Warning("[Spell_Test] NegativeBuffRoutine end");
    }

    private static GameObject CreateSpellItem(string name, Color color)
    {
        Spell_Cirno cirno = GetCirnoSpell();
        if (cirno != null && cirno.giveIceItem != null)
        {
            GameObject item = UnityEngine.Object.Instantiate(cirno.giveIceItem);
            item.name = name;
            var spriteRenderer = item.GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.color = color;
            }
            Log.Warning($"[Spell_Test] CreateSpellItem {name} instantiate Cirno giveIceItem");
            return item;
        }

        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        root.name = name;
        Log.Warning($"[Spell_Test] CreateSpellItem {name} create primitive cube");
        var renderer = root.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }
        root.transform.localScale = new Vector3(0.42f, 0.42f, 0.42f);

        GameObject child = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        child.name = name + "_Core";
        child.transform.SetParent(root.transform, false);
        child.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
        child.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        var childRenderer = child.GetComponent<Renderer>();
        if (childRenderer != null)
        {
            childRenderer.material.color = new Color(color.r, color.g, color.b, 0.9f);
        }

        GameObject particleObject = new GameObject(name + "_Particle");
        particleObject.transform.SetParent(root.transform, false);
        particleObject.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        particleObject.transform.localScale = new Vector3(1.5f, 1.5f, 1.5f);
        var particle = particleObject.AddComponent<ParticleSystem>();
        var particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
        var main = particle.main;
        main.startColor = color;
        main.startLifetime = 0.35f;
        main.startSpeedMultiplier = 1.0f;
        main.startSizeMultiplier = 0.25f;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        if (particleRenderer != null)
        {
            var particleShader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
            if (particleShader != null)
            {
                particleRenderer.material = new Material(particleShader);
                particleRenderer.material.color = color;
            }
            particleRenderer.sortingOrder = 40;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        }
        particle.Emit(28);
        particle.Play();
        Log.Warning($"[Spell_Test] CreateSpellItem {name} particle attached");

        Log.Warning($"[Spell_Test] CreateSpellItem {name} stay world-space");

        return root;
    }

    private static void SpawnEndVfx(Vector3 position, Color color)
    {
        Spell_Cirno cirno = GetCirnoSpell();
        if (cirno != null && cirno.endIceEffect != null)
        {
            GameObject cirnoVfx = UnityEngine.Object.Instantiate(cirno.endIceEffect);
            cirnoVfx.name = "Spell_Test_EndVfx";
            cirnoVfx.transform.position = position;
            UnityEngine.Object.Destroy(cirnoVfx, cirno.endIceDuration > 0f ? cirno.endIceDuration : 0.5f);
            Log.Warning($"[Spell_Test] SpawnEndVfx instantiate Cirno endIceEffect pos={position}");
            return;
        }

        GameObject vfx = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        vfx.name = "Spell_Test_EndVfx";
        Log.Warning($"[Spell_Test] SpawnEndVfx pos={position} color={color}");
        vfx.transform.position = position + new Vector3(0f, 0.15f, 0f);
        vfx.transform.localScale = new Vector3(0.12f, 0.28f, 0.12f);
        var renderer = vfx.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = color;
        }
        GameObject particleObject = new GameObject("Spell_Test_EndVfx_Particle");
        particleObject.transform.SetParent(vfx.transform, false);
        particleObject.transform.localPosition = new Vector3(0f, 0.18f, 0f);
        particleObject.transform.localScale = new Vector3(1.8f, 1.8f, 1.8f);
        var particle = particleObject.AddComponent<ParticleSystem>();
        var particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
        var main = particle.main;
        main.startColor = color;
        main.startLifetime = 0.45f;
        main.startSpeedMultiplier = 1.2f;
        main.startSizeMultiplier = 0.32f;
        main.loop = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        if (particleRenderer != null)
        {
            var particleShader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
            if (particleShader != null)
            {
                particleRenderer.material = new Material(particleShader);
                particleRenderer.material.color = color;
            }
            particleRenderer.sortingOrder = 60;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        }
        particle.Emit(32);
        particle.Play();
        UnityEngine.Object.Destroy(vfx, 0.35f);
    }

    private static Spell_Cirno GetCirnoSpell()
    {
        if (cirnoSpell != null)
        {
            return cirnoSpell;
        }

        try
        {
            SpellBase spell = DataBaseNight.WorkSceneGetSpell(28);
            cirnoSpell = spell == null ? null : spell.TryCast<Spell_Cirno>();
            if (cirnoSpell != null)
            {
                Log.Warning($"[Spell_Test] Found Cirno spell id=28, giveIceItem={cirnoSpell.giveIceItem != null}, endIceEffect={cirnoSpell.endIceEffect != null}");
                return cirnoSpell;
            }
        }
        catch (Exception ex)
        {
            Log.Warning($"[Spell_Test] Failed to get Cirno spell id=28: {ex.Message}");
        }

        Log.Warning("[Spell_Test] Cirno spell not found, fallback primitive object");
        return null;
    }
}
