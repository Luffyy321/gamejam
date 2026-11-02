using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Menu Jeu > 2 : les 4 nouveaux objets de la boutique (antisèche, lunettes 3D, lunettes de Doflamingo, café gourmand).
/// - un prefab « Objet_… » (posé sur notre table au cours suivant : on le prend et on l'approche de la bouche / des yeux) ;
/// - un exemplaire « À vendre » dans le casier, puis le casier est réarrangé sur deux étagères.
/// </summary>
public static class NewItemsSetup
{
    const string Dir = "Assets/SampleJojo/model/Props/boutique/";

    class Def { public string id, name, path; public int price; public float size; }

    static readonly Def[] Defs =
    {
        new Def { id = "antiseche",  name = "Antisèche",              path = null,                                     price = 20, size = 0.09f },
        new Def { id = "lunettes3d", name = "Lunettes 3D",            path = Dir + "Lunettes3D/3dGlass.obj",           price = 40, size = 0.15f },
        new Def { id = "doflamingo", name = "Lunettes de Doflamingo", path = Dir + "Doflamingo/DondlamingoGlasses.blend", price = 35, size = 0.15f },
        new Def { id = "cafe",       name = "Café gourmand",          path = Dir + "Cafe/coffeecupp.fbx",              price = 65, size = 0.10f },
    };

    [MenuItem("Tools/Jeu/2 - Nouveaux objets de la boutique", false, 2)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Boutique] Quitte le mode Play avant."); return; }
        var root = GameObject.Find("Couloir boutique");
        var treasure = root != null ? root.transform.Find("Casier trésor") : null;
        var shop = treasure != null ? treasure.GetComponent<LockerShop>() : null;
        var flow = Object.FindFirstObjectByType<LevelFlow>(FindObjectsInactive.Include);
        if (shop == null || flow == null) { Debug.LogError("[Boutique] Casier trésor ou LevelFlow introuvable."); return; }

        var items = shop.items.Where(i => i != null && !Defs.Any(d => d.id == i.itemId)).ToList();
        foreach (var old in treasure.GetComponentsInChildren<ShopItem>(true).Where(i => Defs.Any(d => d.id == i.itemId)).ToArray())
            Undo.DestroyObjectImmediate(old.gameObject);
        var prefabs = (flow.itemPrefabs ?? new LevelFlow.ItemPrefab[0]).Where(p => p != null && !Defs.Any(d => d.id == p.itemId)).ToList();
        var log = new List<string>();

        foreach (var d in Defs)
        {
            var prefab = BuildPrefab(d);
            if (prefab == null) { log.Add(d.id + " : modèle introuvable"); continue; }
            prefabs.Add(new LevelFlow.ItemPrefab { itemId = d.id, prefab = prefab });

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            Undo.RegisterCreatedObjectUndo(inst, "Objets");
            inst.name = "À vendre : " + d.name;
            inst.transform.SetParent(treasure, true);
            Object.DestroyImmediate(inst.GetComponent<Consumable>());
            Object.DestroyImmediate(inst.GetComponent<XRGrabInteractable>());
            Object.DestroyImmediate(inst.GetComponent<Rigidbody>());
            inst.AddComponent<XRSimpleInteractable>();
            var si = inst.AddComponent<ShopItem>();
            si.itemId = d.id; si.displayName = d.name; si.price = d.price; si.shop = shop;
            items.Add(si);
            log.Add($"{d.name} ({d.price})");
        }
        Undo.RecordObject(shop, "Objets"); shop.items = items.ToArray(); EditorUtility.SetDirty(shop);
        Undo.RecordObject(flow, "Objets"); flow.itemPrefabs = prefabs.ToArray(); EditorUtility.SetDirty(flow);
        EditorSceneManager.MarkSceneDirty(shop.gameObject.scene);
        Debug.Log("[Boutique] Nouveaux objets : " + string.Join(", ", log) + ". Rangement du casier…");
        ShopShelfSetup.Run();
    }

    // ---------------- Prefab (version « sur la table ») ----------------
    static GameObject BuildPrefab(Def d)
    {
        var root = new GameObject("Objet " + d.name);
        GameObject vis;
        if (d.path == null) vis = CheatSheet();
        else
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(d.path);
            if (model == null) { Object.DestroyImmediate(root); return null; }
            vis = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(vis, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        }
        vis.name = "Visuel";
        vis.transform.SetParent(root.transform, false);
        foreach (var c in vis.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var l in vis.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
        foreach (var c in vis.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c.gameObject);
        Materials(d.id, vis);
        // Les lunettes de Doflamingo arrivent de profil : on les tourne pour voir les verres de face.
        if (d.id == "doflamingo") vis.transform.rotation = Quaternion.Euler(0f, 90f, 0f) * vis.transform.rotation;

        // Taille, centré sur l'origine.
        Bounds b = WB(vis);
        float s = d.size / Mathf.Max(1e-5f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
        vis.transform.localScale *= s;
        b = WB(vis);
        vis.transform.position -= b.center;
        b = WB(vis);
        var col = root.AddComponent<BoxCollider>();
        col.center = b.center; col.size = Vector3.Max(b.size, Vector3.one * 0.02f);
        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 0.15f; rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var grab = root.AddComponent<XRGrabInteractable>();
        grab.useDynamicAttach = true;
        grab.throwOnDetach = true;
        var cons = root.AddComponent<Consumable>();
        cons.itemId = d.id;
        cons.wearOnFace = d.id != "cafe";
        if (d.id == "cafe") cons.eatClip = AssetDatabase.LoadAssetAtPath<AudioClip>(FindClip("manger"));
        string path = Dir + "Objet_" + d.id + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static string FindClip(string part)
    {
        var g = AssetDatabase.FindAssets("t:AudioClip " + part, new[] { "Assets/SampleJojo/Audio" }).FirstOrDefault();
        return g != null ? AssetDatabase.GUIDToAssetPath(g) : "";
    }

    /// <summary>Antisèche : petit papier plié avec des lignes griffonnées.</summary>
    static GameObject CheatSheet()
    {
        var go = new GameObject("Antisèche");
        var paper = Lit("Antiseche_Papier", new Color(0.97f, 0.95f, 0.86f), 0.1f);
        var ink = Lit("Antiseche_Encre", new Color(0.15f, 0.25f, 0.65f), 0.1f);
        // Deux moitiés un peu pliées.
        for (int k = 0; k < 2; k++)
        {
            var half = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(half.GetComponent<Collider>());
            half.name = "Papier " + (k + 1);
            half.transform.SetParent(go.transform, false);
            half.transform.localScale = new Vector3(0.045f, 0.0012f, 0.06f);
            half.transform.localPosition = new Vector3(k == 0 ? -0.0222f : 0.0222f, 0.0025f, 0f);
            half.transform.localRotation = Quaternion.Euler(0f, 0f, k == 0 ? 7f : -7f);
            half.GetComponent<Renderer>().sharedMaterial = paper;
            // Lignes écrites tout petit.
            for (int i = 0; i < 7; i++)
            {
                var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(line.GetComponent<Collider>());
                line.name = "Ligne";
                line.transform.SetParent(half.transform, false);
                float w = 0.55f + 0.3f * Mathf.PerlinNoise(i * 0.7f, k * 3.1f);
                line.transform.localScale = new Vector3(w * 0.8f, 0.3f, 0.035f);
                line.transform.localPosition = new Vector3(-0.08f + (1f - w) * -0.3f, 0.65f, 0.4f - i * 0.12f);
                line.GetComponent<Renderer>().sharedMaterial = ink;
            }
        }
        return go;
    }

    static void Materials(string id, GameObject vis)
    {
        var rs = vis.GetComponentsInChildren<Renderer>();
        if (id == "doflamingo")
        {
            // Monture dorée, verres rose-rouge (comme les lunettes du pirate).
            var frame = Lit("Doflamingo_Monture", new Color(0.95f, 0.75f, 0.25f), 0.8f, 0.8f);
            var lens = Lit("Doflamingo_Verres", new Color(0.95f, 0.2f, 0.45f), 0.95f, 0.1f);
            lens.SetFloat("_Cull", 0f);                     // verres visibles des deux côtés
            lens.EnableKeyword("_EMISSION"); lens.SetColor("_EmissionColor", new Color(0.45f, 0.05f, 0.2f));
            lens.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            frame.SetFloat("_Cull", 0f);
            foreach (var r in rs)
            {
                var mf = r.GetComponent<MeshFilter>();
                var mats = r.sharedMaterials.ToArray();
                // Le sous-maillage le plus petit = les verres.
                int lensIdx = 0, best = int.MaxValue;
                if (mf != null && mf.sharedMesh != null)
                    for (int i = 0; i < mf.sharedMesh.subMeshCount; i++) { int n = mf.sharedMesh.GetIndexCount(i) > int.MaxValue ? int.MaxValue : (int)mf.sharedMesh.GetIndexCount(i); if (n < best) { best = n; lensIdx = i; } }
                for (int i = 0; i < mats.Length; i++) mats[i] = i == lensIdx ? lens : frame;
                r.sharedMaterials = mats;
            }
        }
        else if (id == "lunettes3d")
        {
            var white = Lit("Lunettes3D_Monture", new Color(0.97f, 0.97f, 0.97f), 0.4f);
            var red = Lit("Lunettes3D_Rouge", new Color(0.9f, 0.1f, 0.1f), 0.9f);
            var blue = Lit("Lunettes3D_Bleu", new Color(0.1f, 0.4f, 0.95f), 0.9f);
            foreach (var r in rs)
            {
                var m = r.name.Contains("Redlen") ? red : r.name.Contains("Bluelen") ? blue : white;
                r.sharedMaterials = Enumerable.Repeat(m, r.sharedMaterials.Length).ToArray();
            }
        }
        else if (id == "cafe")
        {
            var cup = Lit("Cafe_Tasse", new Color(0.96f, 0.95f, 0.92f), 0.7f);
            var plate = Lit("Cafe_Soucoupe", new Color(0.9f, 0.88f, 0.84f), 0.7f);
            var coffee = Lit("Cafe_Cafe", new Color(0.3f, 0.17f, 0.08f), 0.85f);
            foreach (var r in rs)
            {
                var m = r.name == "Cylinder.002" ? coffee : r.name == "Cylinder.001" ? plate : cup;
                r.sharedMaterials = Enumerable.Repeat(m, r.sharedMaterials.Length).ToArray();
            }
        }
    }

    static Material Lit(string name, Color c, float smooth, float metal = 0f)
    {
        string path = Dir + "Materiaux/" + name + ".mat";
        if (!AssetDatabase.IsValidFolder(Dir + "Materiaux")) AssetDatabase.CreateFolder(Dir.TrimEnd('/'), "Materiaux");
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metal);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Bounds WB(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer) && r.GetComponent<TMP_Text>() == null).ToArray();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }
}
