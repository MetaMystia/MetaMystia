using BepInEx.Unity.IL2CPP.Utils.Collections;
using Il2CppInterop.Runtime.Attributes;
using UnityEngine;

using GameData.Core.Collections.NightSceneUtility;

namespace MetaMystia.ResourceEx.SpellCollection;

[AutoLog]
public partial class Spell_Test : SpellBase
{
    public override string OnGettingSpellOwnerIdentifier()
    {
        return "_ResourceExample_Daiyousei";
    }

    public override Il2CppSystem.Collections.IEnumerator OnPositiveBuffExecute(SpellExecutionContext spellExecutionContext)
        => PositiveBuffRoutine(spellExecutionContext).WrapToIl2Cpp();

    public override Il2CppSystem.Collections.IEnumerator OnNegativeBuffExecute(SpellExecutionContext spellExecutionContext)
        => NegativeBuffRoutine(spellExecutionContext).WrapToIl2Cpp();

    [HideFromIl2Cpp]
    private System.Collections.IEnumerator PositiveBuffRoutine(SpellExecutionContext spellExecutionContext)
    {
        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage("大妖精测试符卡: 正向开始, 创建和操控 Unity 对象");

        CleanupTestObjects();

        var guestPosition = spellExecutionContext.GuestPosition;

        Transform playerTransform = SpellBase.GetPlayerTransform();
        Vector3 guestBase = guestPosition.HasValue ? guestPosition.Value : SpellBase.GetPlayerPosition(false);

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Spell_Test_PositiveCube";
        cube.transform.position = guestBase + new Vector3(0f, 1.15f, 0f);
        cube.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
        Renderer cubeRenderer = cube.GetComponent<Renderer>();
        if (cubeRenderer != null)
        {
            cubeRenderer.material.color = new Color(0.25f, 0.85f, 1f, 1f);
        }

        GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.name = "Spell_Test_PositiveSphere";
        sphere.transform.position = cube.transform.position + new Vector3(0.85f, 0.35f, 0f);
        sphere.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
        Renderer sphereRenderer = sphere.GetComponent<Renderer>();
        if (sphereRenderer != null)
        {
            sphereRenderer.material.color = new Color(1f, 0.85f, 0.2f, 1f);
        }

        sphere.transform.SetParent(cube.transform, true);
        SetRotation(ref spellExecutionContext, cube.transform, "Spell_Test_PositiveCubeRotation", 180f, Vector3.zero);

        yield return new WaitForSeconds(0.35f);

        Vector3 cubeTarget = playerTransform.position + new Vector3(1.0f, 1.4f, 0f);
        var targetFunc = () => cubeTarget;
        yield return LerpPosition(cube.transform, targetFunc, 1.1f);

        sphere.transform.SetParent(null, true);
        Vector3 sphereTarget = playerTransform.position + new Vector3(-0.9f, 1.25f, 0f);
        var sphereTargetFunc = () => sphereTarget;
        yield return LerpPosition(sphere.transform, sphereTargetFunc, 0.7f);

        for (int i = 0; i < 3; i++)
        {
            cube.transform.localScale *= 1.12f;
            yield return new WaitForSeconds(0.12f);
            cube.transform.localScale /= 1.12f;
            yield return new WaitForSeconds(0.12f);
        }

        StopRotation(ref spellExecutionContext, cube.transform, "Spell_Test_PositiveCubeRotation");
        if (cubeRenderer != null)
        {
            cubeRenderer.material.color = new Color(0.15f, 1f, 0.55f, 1f);
        }
        if (sphereRenderer != null)
        {
            sphereRenderer.material.color = new Color(1f, 0.45f, 0.15f, 1f);
        }

        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage("大妖精测试符卡: 正向对象演示完成");

        UnityEngine.Object.Destroy(cube, 0.5f);
        UnityEngine.Object.Destroy(sphere, 0.5f);
        yield return new WaitForSeconds(0.5f);
    }

    [HideFromIl2Cpp]
    private System.Collections.IEnumerator NegativeBuffRoutine(SpellExecutionContext spellExecutionContext)
    {
        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage("大妖精测试符卡: 反向开始, 测试父子关系和移动");

        CleanupTestObjects();

        var guestPosition = spellExecutionContext.GuestPosition;

        Transform playerTransform = SpellBase.GetPlayerTransform();
        Vector3 guestBase = guestPosition.HasValue ? guestPosition.Value : SpellBase.GetPlayerPosition(false);

        GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        capsule.name = "Spell_Test_NegativeCapsule";
        capsule.transform.position = playerTransform.position + new Vector3(-1.0f, 1.05f, 0f);
        capsule.transform.localScale = new Vector3(0.45f, 0.75f, 0.45f);
        Renderer capsuleRenderer = capsule.GetComponent<Renderer>();
        if (capsuleRenderer != null)
        {
            capsuleRenderer.material.color = new Color(1f, 0.35f, 0.35f, 1f);
        }

        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "Spell_Test_NegativeMarker";
        marker.transform.position = guestBase + new Vector3(0f, 1.35f, 0f);
        marker.transform.localScale = new Vector3(0.22f, 0.62f, 0.22f);
        Renderer markerRenderer = marker.GetComponent<Renderer>();
        if (markerRenderer != null)
        {
            markerRenderer.material.color = new Color(0.85f, 0.35f, 1f, 1f);
        }

        marker.transform.SetParent(capsule.transform, true);
        SetRotation(ref spellExecutionContext, capsule.transform, "Spell_Test_NegativeCapsuleRotation", -160f, Vector3.zero);

        yield return new WaitForSeconds(0.25f);

        Vector3 capsuleTarget = guestBase + new Vector3(0f, 1.25f, 0f);
        var capsuleTargetFunc = () => capsuleTarget;
        yield return LerpPosition(capsule.transform, capsuleTargetFunc, 0.9f);

        marker.transform.SetParent(null, true);
        Vector3 markerTarget = playerTransform.position + new Vector3(0f, 1.85f, 0f);
        var markerTargetFunc = () => markerTarget;
        yield return LerpPosition(marker.transform, markerTargetFunc, 0.8f);

        if (capsuleRenderer != null)
        {
            capsuleRenderer.material.color = new Color(0.6f, 0.15f, 1f, 1f);
        }
        if (markerRenderer != null)
        {
            markerRenderer.material.color = new Color(1f, 0.95f, 0.2f, 1f);
        }

        yield return new WaitForSeconds(0.35f);
        StopRotation(ref spellExecutionContext, capsule.transform, "Spell_Test_NegativeCapsuleRotation");

        Common.UI.ReceivedObjectDisplayerController.Instance.NotifyTextMessage("大妖精测试符卡: 反向对象演示完成");

        UnityEngine.Object.Destroy(capsule, 0.5f);
        UnityEngine.Object.Destroy(marker, 0.5f);
        yield return new WaitForSeconds(0.5f);
    }

    private static void CleanupTestObjects()
    {
        DestroyIfExists("Spell_Test_PositiveCube");
        DestroyIfExists("Spell_Test_PositiveSphere");
        DestroyIfExists("Spell_Test_NegativeCapsule");
        DestroyIfExists("Spell_Test_NegativeMarker");
    }

    private static void DestroyIfExists(string name)
    {
        GameObject target = GameObject.Find(name);
        if (target != null)
        {
            UnityEngine.Object.Destroy(target);
        }
    }
}
