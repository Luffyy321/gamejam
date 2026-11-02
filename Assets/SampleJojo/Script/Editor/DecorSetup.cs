using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Menu : Tools > Boutique > 9 - Décor de la classe.
/// Affiches aux murs, objets sur le bureau du prof (ordinateur, mug, papiers, agrafeuse, lunettes),
/// au sol (poubelle, extincteur, avions en papier qu'on peut ramasser et lancer).
/// </summary>
public static class DecorSetup
{
    const string Dir = DecorScan.Dir;
    const string MatDir = Dir + "Materiaux/";
    const string RootName = "Décor classe";
    const float FloorY = 1.52f;

    [MenuItem("Tools/Boutique/9 - Décor de la classe", false, 9)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Décor] Quitte le mode Play avant."); return; }
        var old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, RootName);
        var log = new List<string>();

        // ---------------- Affiches ----------------
        // Mur côté couloir (z = 0.55, face +Z) : à droite du joueur.
        Poster(root, "Affiche alphabet", "Affiche_ABC/Affiche_ABC.fbx", Tex("Affiche_ABC/Affiche_ABC_couleur.jpg"),
               new Vector3(-5.0f, FloorY + 1.05f, 1.5f), Vector3.back, 0.85f, Vector3.forward, log);
        // Mur des fenêtres (z = 8, face -Z) : à gauche du joueur, avant les fenêtres.
        Poster(root, "Affiche katakana", "Affiche_Katakana/Affiche_Katakana.blend", Tex("Affiche_Katakana/Affiche_Katakana_couleur.jpg"),
               new Vector3(-6.25f, FloorY + 1.15f, 7.0f), Vector3.forward, 1.0f, Vector3.left, log);
        Poster(root, "Affiche colombe", "Affiche_Colombe/Affiche_Colombe.fbx", Tex("Affiche_Colombe/Affiche_Colombe_couleur.jpg"),
               new Vector3(-4.0f, FloorY + 1.1f, 7.0f), Vector3.forward, 0.7f, Vector3.forward, log, true);
        // Mur du tableau, entre le coin et la porte (x = 1.95, face -X).
        Poster(root, "Calendrier", "Affiche_Calendrier/Affiche_Calendrier.fbx", Tex("Affiche_Calendrier/Affiche_Calendrier_couleur.jpg"),
               new Vector3(1.0f, FloorY + 1.1f, 1.2f), Vector3.right, 0.55f, Vector3.forward, log);

        // ---------------- Bureau du prof ----------------
        var deskRoot = GameObject.Find("Bureau du prof");
        var deskT = deskRoot != null ? deskRoot.transform.Find("Bureau") : null;
        var deskR = deskT != null ? deskT.gameObject : null;
        // Le bureau visible (celui de la salle) peut être un autre objet que celui du menu 17.
        var visibleDesk = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name.StartsWith("Bureau prof") && t.GetComponentsInChildren<Renderer>().Any(r => r.enabled));
        if (visibleDesk != null) deskR = visibleDesk.gameObject;
        log.Add("bureau utilisé : " + (deskR != null ? deskR.name : "aucun"));
        float topY = 2.20f, x0 = 0.1f, x1 = 0.9f, z0 = 5.4f, z1 = 7.0f;
        if (deskR != null) TopRect(deskR, out topY, out x0, out x1, out z0, out z1);
        log.Add((deskR == null ? "BUREAU INTROUVABLE " : "") + $"plateau x {x0:F2}→{x1:F2} z {z0:F2}→{z1:F2} y {topY:F2}");
        System.Func<float, float, Vector3> onDesk = (x, z) => new Vector3(Mathf.Clamp(x, x0 + 0.08f, x1 - 0.08f), topY, Mathf.Clamp(z, z0 + 0.08f, z1 - 0.08f));
        var desk = new GameObject("Sur le bureau du prof").transform;
        desk.SetParent(root.transform, false);

        var pc = Place(desk, "Ordinateur", "Ordinateur/Ordinateur.fbx", Size.Height, 0.38f, Quaternion.identity);
        if (pc != null)
        {
            foreach (var r in pc.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = r.sharedMaterials.Select(m => m == null ? null :
                    m.name.Contains("Screen") ? Mat("Ordinateur_Ecran_Vitre", new Color(0.08f, 0.12f, 0.16f), null, 0.9f) :
                    m.name.Contains("Monitor") ? Mat("Ordinateur_Plastique", Color.white, Tex("Ordinateur/Ordinateur_ecran.jpg"), 0.35f) :
                    Mat("Ordinateur_Tour", Color.white, Tex("Ordinateur/Ordinateur_tour.jpg"), 0.35f)).ToArray();
            FaceScreen(pc, Vector3.right);                    // l'écran regarde le prof (derrière le bureau)
            var pb = Bounds(pc);
            Settle(pc, onDesk(x1 - pb.extents.x - 0.01f, 6.3f));
        }
        var papers = Place(desk, "Copies", "Papiers/Papiers.obj", Size.Max, 0.24f, Quaternion.Euler(0f, 12f, 0f));
        if (papers != null) { var cm = Mat("Copies", Color.white, Tex("Papiers/Papiers_couleur.jpg"), 0.1f); cm.SetFloat("_Cull", 0f); Paint(papers, cm); Settle(papers, onDesk(x0 + 0.15f, 5.99f)); }
        var mug = Place(desk, "Mug", "Mug/Mug.fbx", Size.Height, 0.095f, Quaternion.Euler(0f, 40f, 0f));
        if (mug != null)
        {
            foreach (var r in mug.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = r.sharedMaterials.Select(m => m != null && m.name.Contains("Coffee")
                    ? Mat("Mug_Cafe", Color.white, Tex("Mug/Mug_couleur.jpg"), 0.8f)
                    : Mat("Mug_Ceramique", new Color(0.86f, 0.3f, 0.28f), null, 0.75f)).ToArray();
            var mb = Bounds(mug);
            var mp = onDesk(x0 + mb.extents.x + 0.01f, 5.47f);
            mp.z = z0 + Mathf.Max(mb.extents.z, 0.05f) + 0.005f;
            Settle(mug, mp);
        }
        var stapler = Place(desk, "Agrafeuse", "Agrafeuse/Agrafeuse.fbx", Size.Max, 0.15f, Quaternion.Euler(0f, -10f, 0f));
        if (stapler != null)
        {
            foreach (var r in stapler.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = r.sharedMaterials.Select(m => m != null && (m.name.Contains("Metal") || m.name.Contains("Spring"))
                    ? Mat("Agrafeuse_Metal", Color.white, Tex("Agrafeuse/Agrafeuse_metal.jpg"), 0.7f, 0.9f)
                    : Mat("Agrafeuse_Plastique", Color.white, Tex("Agrafeuse/Agrafeuse_plastique.jpg"), 0.45f)).ToArray();
            Settle(stapler, onDesk(x0 + 0.08f, 6.33f));
        }
        var glasses = Place(desk, "Lunettes", "Lunettes/Lunettes.fbx", Size.Max, 0.14f, Quaternion.Euler(0f, 100f, 0f));
        if (glasses != null)
        {
            foreach (var r in glasses.GetComponentsInChildren<Renderer>())
                r.sharedMaterials = r.sharedMaterials.Select(m => r.name.Contains("steklo")
                    ? Mat("Lunettes_Verre", new Color(0.75f, 0.85f, 0.9f), null, 0.95f)
                    : Mat("Lunettes_Monture", new Color(0.55f, 0.08f, 0.2f), null, 0.7f)).ToArray();
            // Posées sur les copies.
            var gp = onDesk(x0 + 0.16f, 6.0f);
            if (papers != null) gp.y = Bounds(papers).max.y;
            Settle(glasses, gp);
        }
        foreach (Transform t in desk) { var bb = Bounds(t.gameObject); log.Add($"{t.name} x {bb.min.x:F2}→{bb.max.x:F2} y {bb.min.y:F2}→{bb.max.y:F2} z {bb.min.z:F2}→{bb.max.z:F2}"); }

        // ---------------- Au sol ----------------
        var floor = new GameObject("Au sol").transform;
        floor.SetParent(root.transform, false);
        var bin = Place(floor, "Poubelle", "Poubelle/Poubelle.fbx", Size.Height, 0.36f, Quaternion.identity);
        if (bin != null) { Paint(bin, Mat("Poubelle", Color.white, Tex("Poubelle/Poubelle_couleur.jpg"), 0.3f)); Settle(bin, new Vector3(1.45f, FloorY, 4.85f)); Solid(bin); }
        var ext = Place(floor, "Extincteur", "Extincteur/Extincteur.obj", Size.Height, 0.55f, Quaternion.Euler(-90f, -90f, 0f));
        if (ext != null) { Paint(ext, Mat("Extincteur", Color.white, Tex("Extincteur/Extincteur_couleur.jpg"), 0.6f, 0.2f)); Settle(ext, new Vector3(1.72f, FloorY, 1.55f)); Solid(ext); }
        var planeMat = Mat("Avion_papier", Color.white, Tex("Avion_papier/Avion_papier_couleur.jpg"), 0.1f);
        int i = 0;
        foreach (var p in new[] { new Vector3(-2.6f, FloorY, 3.6f), new Vector3(-1.3f, FloorY, 6.9f), new Vector3(-5.6f, FloorY, 5.6f) })
        {
            var plane = Place(floor, "Avion en papier " + (++i), "Avion_papier/Avion_papier.fbx", Size.Max, 0.26f, Quaternion.Euler(0f, 37f * i + 20f, 0f));
            if (plane == null) continue;
            Paint(plane, planeMat);
            Settle(plane, p);
            Grabbable(plane);
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        File.WriteAllText("Assets/SampleJojo/Script/Editor/decor_log.txt", string.Join("\n", log));
        Debug.Log("[Décor] Classe décorée. " + string.Join(" | ", log));
    }

    // ================= Outils =================
    enum Size { Height, Max }

    static GameObject Place(Transform parent, string name, string path, Size mode, float target, Quaternion rot, bool keepRoot = false)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + path);
        if (asset == null) { Debug.LogWarning("[Décor] introuvable : " + path); return null; }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        go.name = name;
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        foreach (var l in go.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
        foreach (var c in go.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c.gameObject);
        foreach (var a in go.GetComponentsInChildren<Animator>(true)) Object.DestroyImmediate(a);
        go.transform.SetParent(parent, false);
        go.transform.rotation = keepRoot ? rot * go.transform.rotation : rot;   // keepRoot : garde la rotation d'origine du modèle
        var b = Bounds(go);
        float cur = mode == Size.Height ? b.size.y : Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (cur > 1e-5f) go.transform.localScale *= target / cur;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (r is SkinnedMeshRenderer sk) sk.updateWhenOffscreen = false;
        }
        return go;
    }

    /// <summary>Pose l'objet : dessous à la hauteur y, centré en x/z.</summary>
    static void Settle(GameObject go, Vector3 p)
    {
        var b = Bounds(go);
        go.transform.position += new Vector3(p.x - b.center.x, p.y - b.min.y + 0.001f, p.z - b.center.z);
    }

    /// <summary>
    /// Affiche collée au mur : on cherche le mur par un rayon depuis « from » dans la direction « toWall »,
    /// on tourne l'affiche pour que sa face (modelFront) regarde la salle, puis on la plaque contre le mur.
    /// </summary>
    static void Poster(GameObject root, string name, string path, Texture2D tex, Vector3 from, Vector3 toWall, float height, Vector3 modelFront, List<string> log, bool keepRoot = false)
    {
        if (!Physics.Raycast(from, toWall, out var hit, 8f)) { log.Add(name + " : mur introuvable"); return; }
        Vector3 n = hit.normal; n.y = 0f; n.Normalize();
        var rot = Quaternion.FromToRotation(modelFront, n);
        // On garde l'affiche droite (pas de roulis).
        rot = Quaternion.LookRotation(rot * Vector3.forward, Vector3.up);
        var go = Place(root.transform, name, path, Size.Height, height, rot, keepRoot);
        if (go == null) return;
        if (tex != null) { var pm = Mat(name.Replace(' ', '_'), Color.white, tex, 0.15f); pm.SetFloat("_Cull", 0f); pm.doubleSidedGI = true; Paint(go, pm); }
        var b = Bounds(go);
        // Arrière de l'affiche contre le mur (+3 mm), centré sur le point visé.
        float back = Vector3.Dot(b.center - hit.point, n) - Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(n.x), Mathf.Abs(n.y), Mathf.Abs(n.z))));
        Vector3 along = hit.point - b.center; along -= Vector3.Dot(along, n) * n;
        go.transform.position += along + n * (0.003f - back);
        go.transform.position += Vector3.up * (from.y - Bounds(go).center.y);
        var fb = Bounds(go);
        log.Add($"{name} sur « {hit.collider.name} » min {fb.min:F2} max {fb.max:F2} rends {go.GetComponentsInChildren<Renderer>().Length}");
    }

    /// <summary>Tourne l'ordinateur pour que l'écran (matériau « Screen ») regarde dans la direction voulue.</summary>
    static void FaceScreen(GameObject go, Vector3 dir)
    {
        Vector3 c = Bounds(go).center, s = Vector3.zero; int n = 0;
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            var r = mf.GetComponent<MeshRenderer>(); var mesh = mf.sharedMesh;
            if (r == null || mesh == null) continue;
            for (int sm = 0; sm < mesh.subMeshCount && sm < r.sharedMaterials.Length; sm++)
            {
                if (r.sharedMaterials[sm] == null || !r.sharedMaterials[sm].name.Contains("Ecran_Vitre")) continue;
                var v = mesh.vertices;
                foreach (int idx in mesh.GetTriangles(sm)) { s += mf.transform.TransformPoint(v[idx]); n++; }
            }
        }
        if (n == 0) return;
        Vector3 front = s / n - c; front.y = 0f;
        if (front.sqrMagnitude < 1e-8f) return;
        float yaw = Vector3.SignedAngle(front.normalized, dir, Vector3.up);
        go.transform.RotateAround(c, Vector3.up, yaw);
    }

    /// <summary>Le dessus du bureau, mesuré par des rayons vers le bas sur le modèle du bureau.</summary>
    static void TopRect(GameObject desk, out float y, out float x0, out float x1, out float z0, out float z1)
    {
        var temp = new List<MeshCollider>();
        foreach (var mf in desk.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null && mf.gameObject.activeInHierarchy && mf.GetComponent<MeshRenderer>() is MeshRenderer vr && vr.enabled) { var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; temp.Add(mc); }
        Physics.SyncTransforms();
        var vis = desk.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
        var b = vis.Length > 0 ? vis[0].bounds : Bounds(desk);
        foreach (var r in vis) b.Encapsulate(r.bounds);
        var hits = new List<Vector3>();
        for (float x = b.min.x; x <= b.max.x; x += 0.02f)
            for (float z = b.min.z; z <= b.max.z; z += 0.02f)
            {
                var all = Physics.RaycastAll(new Vector3(x, b.max.y + 1f, z), Vector3.down, 3f);
                float best = float.MinValue;
                // Seulement les faces visibles d'en haut (certaines faces du modèle sont à l'envers et invisibles).
                foreach (var h in all) if (h.collider is MeshCollider mc && temp.Contains(mc) && h.normal.y > 0.5f && h.point.y > best) best = h.point.y;
                if (best > float.MinValue) hits.Add(new Vector3(x, best, z));
            }
        foreach (var mc in temp) Object.DestroyImmediate(mc);
        Debug.Log($"[Décor] bureau : {temp.Count} maillages visibles, {hits.Count} points");
        if (hits.Count == 0) { y = b.max.y; x0 = b.min.x; x1 = b.max.x; z0 = b.min.z; z1 = b.max.z; return; }
        // Hauteur la plus fréquente = le plateau.
        float top = hits.GroupBy(h => Mathf.Round(h.y * 100f)).OrderByDescending(g => g.Count()).First().Key / 100f;
        var onTop = hits.Where(h => Mathf.Abs(h.y - top) < 0.015f).ToList();
        y = onTop.Max(h => h.y);
        x0 = onTop.Min(h => h.x); x1 = onTop.Max(h => h.x); z0 = onTop.Min(h => h.z); z1 = onTop.Max(h => h.z);
    }

    static void Paint(GameObject go, Material m)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>())
            r.sharedMaterials = Enumerable.Repeat(m, r.sharedMaterials.Length).ToArray();
    }

    static void Solid(GameObject go)
    {
        var b = Bounds(go);
        var col = go.AddComponent<BoxCollider>();
        col.center = go.transform.InverseTransformPoint(b.center);
        var ls = go.transform.lossyScale;
        Vector3 local = Quaternion.Inverse(go.transform.rotation) * b.size;
        col.size = new Vector3(Mathf.Abs(local.x) / ls.x, Mathf.Abs(local.y) / ls.y, Mathf.Abs(local.z) / ls.z);
    }

    static void Grabbable(GameObject go)
    {
        var b = Bounds(go);
        var root = new GameObject(go.name).transform;
        root.SetParent(go.transform.parent, false);
        root.position = b.center;
        go.transform.SetParent(root, true);
        go.name = "Visuel";
        var col = root.gameObject.AddComponent<BoxCollider>();
        col.size = new Vector3(Mathf.Max(b.size.x, 0.03f), Mathf.Max(b.size.y, 0.03f), Mathf.Max(b.size.z, 0.03f));
        var rb = root.gameObject.AddComponent<Rigidbody>();
        rb.mass = 0.02f; rb.linearDamping = 1.5f; rb.angularDamping = 1f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        var grab = root.gameObject.AddComponent<XRGrabInteractable>();
        grab.useDynamicAttach = true;
        grab.throwOnDetach = true;
    }

    static Texture2D Tex(string rel) => AssetDatabase.LoadAssetAtPath<Texture2D>(Dir + rel);

    static Material Mat(string name, Color c, Texture2D tex, float smooth, float metal = 0f)
    {
        Directory.CreateDirectory(MatDir);
        string path = MatDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.shader = Shader.Find("Universal Render Pipeline/Lit");
        m.SetColor("_BaseColor", c);
        m.SetTexture("_BaseMap", tex);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metal);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // ================= Photos de contrôle =================
    [MenuItem("Tools/Boutique/Photo décor", false, 41)]
    static void Photos()
    {
        Vector3 eye = new Vector3(-6.8f, FloorY + 1.15f, 3.3f);
        Shoot(eye, eye + Vector3.right, "decor_face.png", 70f);
        Shoot(eye, eye + Vector3.back, "decor_droite.png", 70f);
        Shoot(eye, eye + Vector3.forward, "decor_gauche.png", 70f);
        Shoot(new Vector3(-0.9f, 2.75f, 6.0f), new Vector3(0.35f, 2.2f, 6.0f), "decor_bureau.png", 55f);
        Shoot(new Vector3(-0.5f, 2.4f, 2.6f), new Vector3(1.6f, 1.8f, 1.6f), "decor_porte.png", 60f);
        Shoot(new Vector3(-3.5f, 2.6f, 2.5f), new Vector3(-2.6f, 1.52f, 3.6f), "decor_sol.png", 60f);
        Shoot(new Vector3(-5.0f, 2.6f, 4.0f), new Vector3(-5.0f, 2.6f, 8.0f), "decor_fenetres.png", 70f);
        var topCam = new Vector3(0.35f, 3.4f, 6.15f);
        ShootDown(topCam, "decor_bureau_dessus.png");
        Debug.Log("[Décor] photos");
    }

    static void ShootDown(Vector3 eye, string file)
    {
        var go = new GameObject("PhotoCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 60f; cam.nearClipPlane = 0.02f;
        go.transform.position = eye; go.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.right);
        var rt = new RenderTexture(1280, 800, 24);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes("Assets/SampleJojo/Preview~/" + file, tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
    }

    static void Shoot(Vector3 eye, Vector3 target, string file, float fov)
    {
        var go = new GameObject("PhotoCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov; cam.nearClipPlane = 0.02f;
        go.transform.position = eye; go.transform.LookAt(target, Vector3.up);
        var rt = new RenderTexture(1280, 800, 24);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); tex.Apply();
        RenderTexture.active = null;
        Directory.CreateDirectory("Assets/SampleJojo/Preview~/");
        File.WriteAllBytes("Assets/SampleJojo/Preview~/" + file, tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
    }
}
