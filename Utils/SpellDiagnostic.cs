using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

using GameData.Core.Collections;
using GameData.CoreLanguage;

namespace MetaMystia;

public static class SpellDiagnostic
{
    // ================================================================================
    // 角色数据库转储（F7）
    // ================================================================================

    public static void DumpGuestDatabase()
    {
        Debug.Log("[Diag] ===== SPECIAL GUEST DATABASE =====");
        try
        {
            var sgLang = GameData.CoreLanguage.Collections.DataBaseLanguage.SpecialGuest;
            if (sgLang != null)
            {
                foreach (var kvp in sgLang)
                    Debug.Log($"  [SG] id={kvp.Key,4}  name={kvp.Value.Item1}");
            }
            else { Debug.Log("[Diag] SpecialGuest dict is null"); }
        }
        catch (Exception ex) { Debug.Log($"[Diag] SpecialGuest dump error: {ex.Message}"); }

        Debug.Log("[Diag] ===== NORMAL GUEST DATABASE =====");
        try
        {
            var ngLang = GameData.CoreLanguage.Collections.DataBaseLanguage.NormalGuest;
            if (ngLang != null)
            {
                foreach (var kvp in ngLang)
                    Debug.Log($"  [NG] id={kvp.Key,4}  name={kvp.Value.Name}");
            }
            else { Debug.Log("[Diag] NormalGuest dict is null"); }
        }
        catch (Exception ex) { Debug.Log($"[Diag] NormalGuest dump error: {ex.Message}"); }
    }

    // ================================================================================
    // 活跃精灵/粒子/着色器转储（F6）
    // ================================================================================

    public static void DumpAllActiveSprites()
    {
        var seen = new HashSet<string>();

        // --- Sprites ---
        Debug.Log("[Diag] ===== ALL ACTIVE SPRITES IN SCENE =====");
        try
        {
            foreach (var sr in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>())
            {
                if (sr == null || sr.sprite == null) continue;
                if (seen.Add(sr.sprite.name))
                {
                    var path = GetHierarchyPath(sr.gameObject);
                    Debug.Log($"  [SR] sprite={sr.sprite.name}  tex={sr.sprite.texture?.name}  rect={sr.sprite.rect}  obj={path}");
                }
            }
        }
        catch (Exception ex) { Debug.Log($"[Diag] SpriteRenderer scan error: {ex.Message}"); }

        try
        {
            foreach (var img in UnityEngine.Object.FindObjectsOfType<UnityEngine.UI.Image>())
            {
                if (img == null || img.sprite == null) continue;
                if (seen.Add(img.sprite.name))
                {
                    var path = GetHierarchyPath(img.gameObject);
                    Debug.Log($"  [IMG] sprite={img.sprite.name}  tex={img.sprite.texture?.name}  rect={img.sprite.rect}  obj={path}");
                }
            }
        }
        catch (Exception ex) { Debug.Log($"[Diag] Image scan error: {ex.Message}"); }

        Debug.Log($"[Diag] Total unique sprites: {seen.Count}");

        // --- Particle Systems ---
        Debug.Log("[Diag] ===== PARTICLE SYSTEMS IN SCENE =====");
        try
        {
            var psFound = 0;
            foreach (var ps in UnityEngine.Object.FindObjectsOfType<ParticleSystem>())
            {
                if (ps == null) continue;
                psFound++;
                var path = GetHierarchyPath(ps.gameObject);
                var main = ps.main;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();

                Debug.Log($"  [PS] obj={path}");
                Debug.Log($"       playing={ps.isPlaying}  paused={ps.isPaused}  particleCount={ps.particleCount}");
                Debug.Log($"       duration={main.duration:F2}s  maxParticles={main.maxParticles}");

                if (renderer != null)
                {
                    var mat = renderer.material;
                    var trailMat = renderer.trailMaterial;
                    Debug.Log($"       renderMode={renderer.renderMode}  mat={mat?.name}  shader={mat?.shader?.name}");
                    if (mat != null && mat.mainTexture != null)
                        Debug.Log($"       mainTex={mat.mainTexture.name}  size={mat.mainTexture.width}x{mat.mainTexture.height}");
                    if (trailMat != null)
                        Debug.Log($"       trailMat={trailMat.name}  shader={trailMat.shader?.name}");
                }
            }
            if (psFound == 0) Debug.Log("  (none found)");
            Debug.Log($"[Diag] Total particle systems: {psFound}");
        }
        catch (Exception ex) { Debug.Log($"[Diag] ParticleSystem scan error: {ex.Message}"); }

        // --- Shaders & Materials on scene objects ---
        Debug.Log("[Diag] ===== UNIQUE SHADERS IN SCENE =====");
        try
        {
            var shaderSet = new HashSet<string>();
            var matSet = new HashSet<string>();

            foreach (var sr in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>())
            {
                if (sr == null) continue;
                DumpMaterial(sr.material, shaderSet, matSet, "SR", GetHierarchyPath(sr.gameObject));
            }

            foreach (var ps in UnityEngine.Object.FindObjectsOfType<ParticleSystem>())
            {
                if (ps == null) continue;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    DumpMaterial(renderer.material, shaderSet, matSet, "PS", GetHierarchyPath(ps.gameObject));
                    if (renderer.trailMaterial != null)
                        DumpMaterial(renderer.trailMaterial, shaderSet, matSet, "PS-Trail", GetHierarchyPath(ps.gameObject));
                }
            }

            foreach (var mr in UnityEngine.Object.FindObjectsOfType<MeshRenderer>())
            {
                if (mr == null) continue;
                DumpMaterial(mr.material, shaderSet, matSet, "Mesh", GetHierarchyPath(mr.gameObject));
            }

            Debug.Log($"[Diag] Total unique shaders: {shaderSet.Count}");
            Debug.Log($"[Diag] Total unique materials: {matSet.Count}");
        }
        catch (Exception ex) { Debug.Log($"[Diag] Shader scan error: {ex.Message}"); }
    }

    private static void DumpMaterial(Material mat, HashSet<string> shaderSet, HashSet<string> matSet, string tag, string objPath)
    {
        if (mat == null) return;
        var shader = mat.shader;
        if (shader == null) return;

        bool newShader = shaderSet.Add(shader.name);
        bool newMat = matSet.Add(mat.name);

        if (newShader || newMat)
        {
            Debug.Log($"  [{tag}] shader={shader.name}  mat={mat.name}  obj={objPath}");
            if (mat.mainTexture != null)
                Debug.Log($"         mainTex={mat.mainTexture.name}  size={mat.mainTexture.width}x{mat.mainTexture.height}");
        }
    }

    private static string GetHierarchyPath(GameObject go)
    {
        var path = go.name;
        var t = go.transform.parent;
        while (t != null) { path = t.name + "/" + path; t = t.parent; }
        return path;
    }

    // ================================================================================
    // 桌子与可交互物品位置转储（F9）
    // ================================================================================

    public static void DumpDeskAndInteractablePositions()
    {
        var cam = Camera.main;
        Debug.Log("[Diag] ===== DESK & INTERACTABLE POSITIONS =====");

        // --- DeskUnit（桌子） ---
        try
        {
            var desks = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
            int deskCount = 0;
            foreach (var mb in desks)
            {
                if (mb == null) continue;
                var typeName = mb.GetIl2CppType().Name;
                if (typeName != "DeskUnit") continue;

                deskCount++;
                var go = mb.gameObject;
                var path = GetHierarchyPath(go);
                var worldPos = go.transform.position;
                var screenPos = cam != null ? cam.WorldToScreenPoint(worldPos) : Vector3.zero;

                Debug.Log($"  [Desk] name={go.name}  obj={path}");
                Debug.Log($"         world=({worldPos.x:F2}, {worldPos.y:F2}, {worldPos.z:F2})  screen=({screenPos.x:F0}, {screenPos.y:F0})");
            }
            if (deskCount == 0) Debug.Log("  (no DeskUnit found)");
            Debug.Log($"[Diag] Total desks: {deskCount}");
        }
        catch (Exception ex) { Debug.Log($"[Diag] DeskUnit scan error: {ex.Message}"); }

        // --- CookerModule（厨具） ---
        try
        {
            int cookerCount = 0;
            foreach (var mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null) continue;
                var typeName = mb.GetIl2CppType().Name;
                if (typeName != "CookerModule") continue;

                cookerCount++;
                var go = mb.gameObject;
                var path = GetHierarchyPath(go);
                var worldPos = go.transform.position;
                var screenPos = cam != null ? cam.WorldToScreenPoint(worldPos) : Vector3.zero;

                Debug.Log($"  [Cooker] name={go.name}  obj={path}");
                Debug.Log($"           world=({worldPos.x:F2}, {worldPos.y:F2}, {worldPos.z:F2})  screen=({screenPos.x:F0}, {screenPos.y:F0})");
            }
            if (cookerCount == 0) Debug.Log("  (no CookerModule found)");
            Debug.Log($"[Diag] Total cookers: {cookerCount}");
        }
        catch (Exception ex) { Debug.Log($"[Diag] CookerModule scan error: {ex.Message}"); }

        // --- Gifts（可交互物品/装饰） ---
        try
        {
            int giftCount = 0;
            foreach (var sr in UnityEngine.Object.FindObjectsOfType<SpriteRenderer>())
            {
                if (sr == null) continue;
                var parent = sr.transform.parent;
                if (parent == null || parent.name != "Gifts") continue;

                giftCount++;
                var go = sr.gameObject;
                var worldPos = go.transform.position;
                var screenPos = cam != null ? cam.WorldToScreenPoint(worldPos) : Vector3.zero;

                Debug.Log($"  [Gift] name={go.name}  world=({worldPos.x:F2}, {worldPos.y:F2}, {worldPos.z:F2})  screen=({screenPos.x:F0}, {screenPos.y:F0})");
            }
            Debug.Log($"[Diag] Total gifts: {giftCount}");
        }
        catch (Exception ex) { Debug.Log($"[Diag] Gift scan error: {ex.Message}"); }

        // --- Storage 仓库面板位置（UI） ---
        try
        {
            foreach (var mb in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null) continue;
                var typeName = mb.GetIl2CppType().Name;
                if (typeName != "WorkSceneStoragePannel") continue;

                var rt = mb.GetComponent<RectTransform>();
                if (rt == null) continue;

                var anchored = rt.anchoredPosition;
                var size = rt.sizeDelta;
                var corners = new Vector3[4];
                rt.GetWorldCorners(corners);

                Debug.Log($"  [Storage] anchored=({anchored.x:F0}, {anchored.y:F0})  size=({size.x:F0}, {size.y:F0})");
                Debug.Log($"            corners: BL=({corners[0].x:F0},{corners[0].y:F0}) TL=({corners[1].x:F0},{corners[1].y:F0}) TR=({corners[2].x:F0},{corners[2].y:F0}) BR=({corners[3].x:F0},{corners[3].y:F0})");
            }
        }
        catch (Exception ex) { Debug.Log($"[Diag] Storage scan error: {ex.Message}"); }
    }

    // ================================================================================
    // 食材标签转储（F8）—— 用于查找水果标签 ID
    // ================================================================================

    public static void DumpFoodTagsAndIngredients()
    {
        Debug.Log("[Diag] ===== FOOD TAGS =====");
        try
        {
            var tags = GameData.CoreLanguage.Collections.DataBaseLanguage.FoodTags;
            if (tags != null)
            {
                foreach (var kvp in tags)
                    Debug.Log($"  [Tag] id={kvp.Key}  name={kvp.Value}");
            }
            else { Debug.Log("[Diag] FoodTags dict is null"); }
        }
        catch (Exception ex) { Debug.Log($"[Diag] FoodTags dump error: {ex.Message}"); }

        Debug.Log("[Diag] ===== BEVERAGE TAGS =====");
        try
        {
            var tags = GameData.CoreLanguage.Collections.DataBaseLanguage.BeverageTags;
            if (tags != null)
            {
                foreach (var kvp in tags)
                    Debug.Log($"  [Tag] id={kvp.Key}  name={kvp.Value}");
            }
        }
        catch (Exception ex) { Debug.Log($"[Diag] BeverageTags dump error: {ex.Message}"); }

        Debug.Log("[Diag] ===== INGREDIENTS (first 50) =====");
        try
        {
            var ingredients = DataBaseCore.Ingredients;
            if (ingredients != null)
            {
                int count = 0;
                foreach (var kvp in ingredients)
                {
                    var ing = kvp.Value;
                    var name = TryGetIngredientName(kvp.Key);
                    var tagList = ing.Tags != null ? string.Join(",", ing.Tags) : "[]";
                    Debug.Log($"  [Ing] id={kvp.Key}  name={name}  tags=[{tagList}]");
                    if (++count >= 50) { Debug.Log($"  ... ({ingredients.Count} total, showing first 50)"); break; }
                }
            }
        }
        catch (Exception ex) { Debug.Log($"[Diag] Ingredients dump error: {ex.Message}"); }
    }

    private static string TryGetIngredientName(int id)
    {
        try
        {
            var lang = GameData.CoreLanguage.Collections.DataBaseLanguage.Ingredients;
            if (lang != null)
            {
                var entry = lang[id];
                return entry?.ToString() ?? $"Unknown({id})";
            }
        }
        catch { }
        return $"Unknown({id})";
    }
}
