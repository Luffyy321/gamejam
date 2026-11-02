using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Menu : Tools > Jackpot > 23 - Salle : sol texturé, lumières au casque, collisions des tables.
/// </summary>
public static class ClassroomFix
{
    const string TexDir = "Assets/SampleJojo/model/Props/SolTexture/";
    const float TileMeters = 2f;     // un motif tous les 2 m

    [MenuItem("Tools/Jackpot/23 - Salle : sol texturé, lumières casque, collisions tables", false, 134)]
    static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Salle] Quitte le mode Play avant."); return; }
        var log = new List<string>();
        log.Add(Floor());
        log.Add(LightsOnHeadset());
        log.Add(TableColliders());
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[Salle] " + string.Join(" | ", log));
    }

    // ---------- Sol ----------
    static string Floor()
    {
        var floor = GameObject.Find("Salle - objets du .blend/Sol");
        if (floor == null) return "sol : objet « Sol » introuvable (menu 20 d'abord)";

        var nrm = AssetImporter.GetAtPath(TexDir + "sand_02_normal_gl_1k.png") as TextureImporter;
        if (nrm != null && nrm.textureType != TextureImporterType.NormalMap) { nrm.textureType = TextureImporterType.NormalMap; nrm.SaveAndReimport(); }

        string path = TexDir + "Sol_texture.mat";
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "sand_02_color_1k.png"));
        m.SetColor("_BaseColor", Color.white);
        var n = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "sand_02_normal_gl_1k.png");
        if (n != null) { m.SetTexture("_BumpMap", n); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
        var ao = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "sand_02_ambient_occlusion_1k.png");
        if (ao != null) { m.SetTexture("_OcclusionMap", ao); m.EnableKeyword("_OCCLUSIONMAP"); }
        m.SetFloat("_Smoothness", 0.15f);
        m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);

        // UV en mètres : un motif tous les 2 m.
        var mf = floor.GetComponent<MeshFilter>();
        var mesh = mf.sharedMesh;
        var v = mesh.vertices;
        var uv = new Vector2[v.Length];
        for (int i = 0; i < v.Length; i++)
        {
            Vector3 w = floor.transform.TransformPoint(v[i]);
            uv[i] = new Vector2(w.x / TileMeters, w.z / TileMeters);
        }
        mesh.uv = uv;
        mesh.RecalculateTangents();
        EditorUtility.SetDirty(mesh);
        var r = floor.GetComponent<MeshRenderer>();
        Undo.RecordObject(r, "Sol");
        r.sharedMaterial = m;
        r.receiveShadows = true;
        AssetDatabase.SaveAssets();
        return "sol texturé (motif de " + TileMeters + " m)";
    }

    // ---------- Lumières visibles au casque ----------
    static string LightsOnHeadset()
    {
        // Les réglages URP du projet : sur Quest, « Performance » avait les lumières supplémentaires désactivées.
        int fixedCount = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(guid));
            if (asset == null) continue;
            var so = new SerializedObject(asset);
            var mode = so.FindProperty("m_AdditionalLightsRenderingMode");
            var limit = so.FindProperty("m_AdditionalLightsPerObjectLimit");
            bool changed = false;
            if (mode != null && mode.intValue != 1) { mode.intValue = 1; changed = true; }          // 1 = par pixel
            if (limit != null && limit.intValue < 4) { limit.intValue = 4; changed = true; }
            if (changed) { so.ApplyModifiedProperties(); EditorUtility.SetDirty(asset); fixedCount++; }
        }
        AssetDatabase.SaveAssets();

        // Allumées dès le début.
        var ctrl = Object.FindFirstObjectByType<ClassroomLights>();
        if (ctrl != null)
        {
            Undo.RecordObject(ctrl, "Lumières");
            ctrl.on = true;
            ctrl.emission = new Color(1f, 0.95f, 0.85f) * 3f;
            foreach (var l in ctrl.lights)
            {
                if (l == null) continue;
                Undo.RecordObject(l, "Lumière");
                l.enabled = true;
                l.intensity = 2.2f;
                l.range = 5f;
            }
            ctrl.Apply();
            EditorUtility.SetDirty(ctrl);
        }
        return "lumières : " + fixedCount + " réglage(s) URP corrigé(s), allumées au démarrage";
    }

    // ---------- Collisions des tables ----------
    static string TableColliders()
    {
        int n = 0;
        var desk = GameObject.Find("Desk");
        if (desk != null)
            foreach (Transform group in desk.transform)
                foreach (Transform t in group)
                {
                    if (t.name.StartsWith("Chaise")) continue;
                    if (AddBox(t.gameObject)) n++;
                }
        var jeu = GameObject.Find("Bureau du jeu");
        if (jeu != null && AddBox(jeu)) n++;
        return n + " tables avec collision";
    }

    /// <summary>Boîte de collision qui englobe tout le meuble (calculée dans son propre repère).</summary>
    static bool AddBox(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return false;
        bool first = true;
        Bounds local = default;
        foreach (var r in rs)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            Bounds mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = go.transform.InverseTransformPoint(r.transform.TransformPoint(c));
                if (first) { local = new Bounds(p, Vector3.zero); first = false; } else local.Encapsulate(p);
            }
        }
        if (first) return false;
        var box = go.GetComponent<BoxCollider>();
        if (box == null) box = Undo.AddComponent<BoxCollider>(go);
        Undo.RecordObject(box, "Collision table");
        box.center = local.center;
        box.size = local.size;
        box.isTrigger = false;
        return true;
    }
}
