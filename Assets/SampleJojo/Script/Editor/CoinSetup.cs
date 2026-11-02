using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Menu : Tools > Jackpot > 25 - Pièces de la machine + nouveau sac.
/// - 6 pièces (1, 5, 10, 25, 50, 100) faites avec MMIcoins.fbx : plus la valeur est grande, plus la pièce est grosse.
/// - La machine fait exploser des pièces quand on gagne (gain selon la combinaison).
/// - Le sac (remplacé par le sac à dos) récolte les pièces qu'on y lâche.
/// </summary>
public static class CoinSetup
{
    const string CoinFbx = "Assets/SampleJojo/model/Props/Coin/MMIcoins.fbx";
    const string CoinDir = "Assets/SampleJojo/model/Props/Coin/";
    const string BagFbx = "Assets/SampleJojo/model/Props/sac/backpack.fbx";
    const string BagDir = "Assets/SampleJojo/model/Props/sac/";
    const string AudioDir = "Assets/SampleJojo/Audio/";

    // Valeur, diamètre (m), matériau.
    static readonly (int value, float diameter, string metal)[] Coins =
    {
        (1,   0.050f, "Bronze"),
        (5,   0.060f, "Bronze"),
        (10,  0.070f, "Argent"),
        (25,  0.080f, "Argent"),
        (50,  0.095f, "Or"),
        (100, 0.110f, "Or"),
    };

    [MenuItem("Tools/Jackpot/25 - Pièces de la machine + nouveau sac", false, 136)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Pièces] Quitte le mode Play avant."); return; }
        var log = new List<string>();

        var prefabs = BuildCoinPrefabs(log);
        if (prefabs == null) return;

        // ---- Machine ----
        var item = Object.FindFirstObjectByType<JackpotItem>();
        var lever = Object.FindFirstObjectByType<JackpotLever>();
        if (item == null || lever == null) { Debug.LogError("[Pièces] Machine (JackpotItem / JackpotLever) introuvable."); return; }

        Undo.RecordObject(lever, "Gains");
        lever.winChance = 0.1f;
        lever.payTable = new[]
        {
            new JackpotLever.PayRule { symbol = "mmi",     triple = 500, pair = 25, weight = 1f },
            new JackpotLever.PayRule { symbol = "claude",  triple = 250, pair = 15, weight = 2f },
            new JackpotLever.PayRule { symbol = "izly",    triple = 150, pair = 10, weight = 3f },
            new JackpotLever.PayRule { symbol = "crous",   triple = 100, pair = 10, weight = 4f },
            new JackpotLever.PayRule { symbol = "couteau", triple = 75,  pair = 10, weight = 5f },
        };
        lever.singleJackpotSymbol = 5;
        PrefabUtility.RecordPrefabInstancePropertyModifications(lever);

        var spawner = item.GetComponent<JackpotCoins>();
        if (spawner == null) spawner = Undo.AddComponent<JackpotCoins>(item.gameObject);
        Undo.RecordObject(spawner, "Pièces");
        spawner.lever = lever;
        spawner.item = item;
        spawner.coins = Coins.Select((c, i) => new JackpotCoins.CoinType { value = c.value, prefab = prefabs[i] }).ToArray();
        var sndT = item.transform.Find("Sons pièces");
        if (sndT == null)
        {
            var g = new GameObject("Sons pièces");
            Undo.RegisterCreatedObjectUndo(g, "Sons pièces");
            g.transform.SetParent(item.transform, false);
            sndT = g.transform;
        }
        var src = sndT.GetComponent<AudioSource>();
        if (src == null) src = Undo.AddComponent<AudioSource>(sndT.gameObject);
        src.playOnAwake = false; src.spatialBlend = 1f; src.minDistance = 0.8f; src.maxDistance = 15f;
        spawner.audioSource = src;
        spawner.burstClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "coin_burst.wav");
        // Les pièces retombent uniquement sur la table de jeu.
        // (les pièces visent la table autour de la machine, mesurée en jeu)
        PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);
        log.Add("machine : gains + explosion de pièces");

        // ---- Sac ----
        log.Add(SetupBag());

        EditorSceneManager.MarkSceneDirty(item.gameObject.scene);
        Debug.Log("[Pièces] " + string.Join(" | ", log));
    }

    /// <summary>La table sous l'emplacement de la machine (le livre) : son BoxCollider.</summary>
    static BoxCollider FindGameTable()
    {
        Physics.SyncTransforms();
        var socket = Object.FindFirstObjectByType<JackpotSocket>();
        Vector3 from = socket != null ? socket.transform.position + Vector3.up * 0.3f : Vector3.zero;
        if (socket == null) return null;
        var hits = Physics.RaycastAll(from, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance);
        foreach (var h in hits)
            if (h.collider is BoxCollider bc && bc.GetComponentInParent<JackpotItem>() == null && bc.bounds.size.x > 0.4f && bc.bounds.size.z > 0.3f)
                return bc;
        return null;
    }

    // ---------------- Pièces ----------------
    static GameObject[] BuildCoinPrefabs(List<string> log)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CoinFbx);
        if (fbx == null) { Debug.LogError("[Pièces] introuvable : " + CoinFbx); return null; }

        var mats = new Dictionary<string, Material>
        {
            ["Bronze"] = Metal("Piece_Bronze", new Color(0.78f, 0.45f, 0.22f)),
            ["Argent"] = Metal("Piece_Argent", new Color(0.82f, 0.84f, 0.88f)),
            ["Or"] = Metal("Piece_Or", new Color(1f, 0.76f, 0.12f)),
        };

        var phys = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(CoinDir + "Piece_Physique.asset");
        if (phys == null)
        {
            phys = new PhysicsMaterial("Piece_Physique");
            AssetDatabase.CreateAsset(phys, CoinDir + "Piece_Physique.asset");
        }
        phys.bounciness = 0.05f;
        phys.dynamicFriction = 0.7f;
        phys.staticFriction = 0.8f;
        phys.bounceCombine = PhysicsMaterialCombine.Minimum;
        phys.frictionCombine = PhysicsMaterialCombine.Maximum;
        EditorUtility.SetDirty(phys);

        // Forme de collision : un cylindre simple (bien plus léger que le modèle).
        var colMesh = CylinderMesh(CoinDir + "Piece_Collision.asset");
        var clink = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "coin_clink.wav");

        var result = new GameObject[Coins.Length];
        for (int i = 0; i < Coins.Length; i++)
        {
            var (value, diameter, metal) = Coins[i];
            var root = new GameObject("Pièce " + value);
            var vis = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
            PrefabUtility.UnpackPrefabInstance(vis, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            vis.name = "Visuel";
            vis.transform.SetParent(root.transform, false);
            foreach (var l in vis.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
            foreach (var c in vis.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var r in vis.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials = Enumerable.Repeat(mats[metal], r.sharedMaterials.Length).ToArray();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            // Mise à la taille : le modèle fait ~2 unités de large, à plat.
            Bounds b = Bounds(vis);
            float modelD = Mathf.Max(b.size.x, b.size.z);
            float s = diameter / Mathf.Max(1e-5f, modelD);
            vis.transform.localScale *= s;
            b = Bounds(vis);
            vis.transform.localPosition -= b.center;
            float thick = Mathf.Max(b.size.y, diameter * 0.06f);

            // Collision : cylindre de 1 m mis à l'échelle de la pièce.
            var colGo = new GameObject("Collision");
            colGo.transform.SetParent(root.transform, false);
            colGo.transform.localScale = new Vector3(diameter, thick, diameter);
            var mc = colGo.AddComponent<MeshCollider>();
            mc.sharedMesh = colMesh;
            mc.convex = true;
            mc.sharedMaterial = phys;

            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 0.02f + 0.06f * (diameter / 0.11f);
            rb.linearDamping = 0.05f;
            rb.angularDamping = 1.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            var grab = root.AddComponent<XRGrabInteractable>();
            grab.useDynamicAttach = true;
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.throwOnDetach = true;
            grab.throwVelocityScale = 1.5f;
            grab.throwAngularVelocityScale = 1f;
            grab.colliders.Clear();
            grab.colliders.Add(mc);

            var coin = root.AddComponent<JackpotCoin>();
            coin.value = value;
            coin.clinkClip = clink;

            string path = CoinDir + "Piece_" + value + ".prefab";
            result[i] = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }
        AssetDatabase.SaveAssets();
        log.Add(result.Length + " pièces (prefabs dans Props/Coin)");
        return result;
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Material Metal(string name, Color c)
    {
        string path = CoinDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Metallic", 0.65f);
        m.SetFloat("_Smoothness", 0.6f);
        // Un peu de lumière propre pour qu'elles brillent même dans les coins sombres.
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c * 0.12f);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>Cylindre de 1 m de diamètre et 1 m de haut, centré (24 côtés).</summary>
    static Mesh CylinderMesh(string path)
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        bool create = mesh == null;
        if (create) mesh = new Mesh { name = "Piece_Collision" };
        const int n = 24;
        var v = new List<Vector3>();
        var t = new List<int>();
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            float x = Mathf.Cos(a) * 0.5f, z = Mathf.Sin(a) * 0.5f;
            v.Add(new Vector3(x, 0.5f, z));
            v.Add(new Vector3(x, -0.5f, z));
        }
        for (int i = 0; i < n; i++)
        {
            int a = i * 2, b = ((i + 1) % n) * 2;
            t.AddRange(new[] { a, b, a + 1, b, b + 1, a + 1 });
            if (i > 0 && i < n - 1)
            {
                t.AddRange(new[] { 0, b, a });
                t.AddRange(new[] { 1, a + 1, b + 1 });
            }
        }
        mesh.Clear();
        mesh.SetVertices(v);
        mesh.SetTriangles(t, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (create) AssetDatabase.CreateAsset(mesh, path);
        else EditorUtility.SetDirty(mesh);
        return mesh;
    }

    // ---------------- Sac à dos ----------------
    static string SetupBag()
    {
        var bag = Object.FindFirstObjectByType<JackpotBag>();
        if (bag == null) return "sac introuvable";
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(BagFbx);
        if (fbx == null) return "sac à dos introuvable : " + BagFbx;

        // Ancien sac (cylindres) : caché, sauf la lueur qui devient un halo au sol.
        var visual = bag.transform.Find("Visuel");
        if (visual != null)
            foreach (Transform c in visual)
            {
                if (c.name == "Lueur") continue;
                Undo.RecordObject(c.gameObject, "Sac");
                c.gameObject.SetActive(false);
            }
        var glow = visual != null ? visual.Find("Lueur") : null;
        if (glow != null)
        {
            Undo.RecordObject(glow, "Lueur");
            glow.localPosition = new Vector3(0f, 0.003f, 0f);
            glow.localRotation = Quaternion.identity;
            glow.localScale = new Vector3(0.62f, 0.001f, 0.62f);
        }

        var old = bag.transform.Find("Sac à dos");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        Undo.RegisterCreatedObjectUndo(inst, "Sac à dos");
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        inst.name = "Sac à dos";
        inst.transform.SetParent(bag.transform, false);
        inst.transform.localPosition = Vector3.zero;
        inst.transform.localRotation = Quaternion.Euler(0f, BagYaw, 0f);
        foreach (var an in inst.GetComponentsInChildren<Animator>(true)) an.enabled = false;
        foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

        var mat = BagMaterial();
        foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
        }

        // Posé au sol, centré sur le sac.
        Bounds b = Bounds(inst);
        Vector3 local = bag.transform.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z));
        inst.transform.localPosition -= local;
        b = Bounds(inst);
        float height = b.size.y;

        // Compteur au-dessus.
        var txtT = bag.transform.Find("Compteur pièces");
        if (txtT == null)
        {
            var g = new GameObject("Compteur pièces");
            Undo.RegisterCreatedObjectUndo(g, "Compteur");
            g.transform.SetParent(bag.transform, false);
            txtT = g.transform;
        }
        txtT.localPosition = new Vector3(0f, height + 0.15f, 0f);
        txtT.localScale = Vector3.one;
        var tmp = txtT.GetComponent<TextMeshPro>();
        if (tmp == null) tmp = Undo.AddComponent<TextMeshPro>(txtT.gameObject);
        tmp.text = "0 pièces";
        tmp.fontSize = 0.7f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = new Color(1f, 0.82f, 0.25f);
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(0.6f, 0.12f);

        Undo.RecordObject(bag, "Sac");
        bag.handleHeight = height * 0.55f;
        bag.collectRadius = Mathf.Max(0.28f, Mathf.Max(b.size.x, b.size.z) * 0.6f);
        bag.collectTop = height + 0.35f;
        bag.coinText = tmp;
        bag.coinClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + "coin_collect.wav");
        EditorUtility.SetDirty(bag);
        return $"sac à dos posé ({height:F2} m), récolte des pièces";
    }

    /// <summary>Rotation du modèle de sac à dos pour que sa face avant regarde le joueur.</summary>
    const float BagYaw = 0f;

    static Material BagMaterial()
    {
        string tex = BagDir + "textures/backpack_lp_BackPack_";
        var nrmImp = AssetImporter.GetAtPath(tex + "Normal_OpenGL.png") as TextureImporter;
        if (nrmImp != null && nrmImp.textureType != TextureImporterType.NormalMap) { nrmImp.textureType = TextureImporterType.NormalMap; nrmImp.SaveAndReimport(); }
        foreach (var lin in new[] { "Metallic", "Roughness", "AO" })
        {
            var imp = AssetImporter.GetAtPath(tex + lin + ".png") as TextureImporter;
            if (imp != null && imp.sRGBTexture) { imp.sRGBTexture = false; imp.SaveAndReimport(); }
        }
        string path = BagDir + "Sac_Backpack.mat";
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "BaseColor.png"));
        m.SetColor("_BaseColor", Color.white);
        var n = AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "Normal_OpenGL.png");
        if (n != null) { m.SetTexture("_BumpMap", n); m.SetFloat("_BumpScale", 1f); m.EnableKeyword("_NORMALMAP"); }
        var ao = AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "AO.png");
        if (ao != null) { m.SetTexture("_OcclusionMap", ao); m.EnableKeyword("_OCCLUSIONMAP"); }
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Smoothness", 0.25f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------------- Tests en Play ----------------
    [MenuItem("Tools/Jackpot/26 - Test : faire sortir 500 pièces (en Play)", false, 137)]
    static void TestBig()
    {
        var s = Object.FindFirstObjectByType<JackpotCoins>();
        if (Application.isPlaying && s != null) s.Payout(500);
    }

    [MenuItem("Tools/Jackpot/27 - Test : ranger les pièces dans le sac (en Play)", false, 138)]
    static void TestCollect()
    {
        var bag = JackpotBag.Active;
        if (!Application.isPlaying || bag == null) return;
        foreach (var c in Object.FindObjectsByType<JackpotCoin>(FindObjectsSortMode.None)) bag.Collect(c);
    }
}
