using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Menu Tools > Jackpot > 4 : la machine devient un objet qu'on peut lancer, rangé dans un sac posé
/// à côté de LA table de jeu. Sur cette table : un livre ouvert (pages tournables) et un emplacement
/// en pointillés au-dessus du livre, le seul endroit où l'on peut poser la machine.
/// </summary>
public static class JackpotInventorySetup
{
    const float TargetWidth = 0.6f;                 // largeur de la machine posée sur la table (m)
    const string TableName = "Table.035";           // la table de jeu (devant le joueur au départ)
    const string PrefabFolder = "Assets/SampleJojo/Prefabs";
    const string SpotPrefabPath = PrefabFolder + "/Emplacement Jackpot.prefab";
    const string ModelFolder = "Assets/SampleJojo/model/";
    const string DashMatPath = ModelFolder + "JackpotPointilles.mat";

    // Dimensions du livre ouvert (m)
    const float PageW = 0.2f, PageD = 0.28f;
    const float CoverT = 0.006f, BlockT = 0.018f;
    const float PageTop = CoverT + BlockT;          // dessus des pages
    const float BookHeight = PageTop + 0.004f;      // la machine se pose au-dessus

    [MenuItem("Tools/Jackpot/4 - Configurer l'inventaire (sac + table + livre)")]
    static void Setup()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[Jackpot] Quitte le mode Play avant de configurer.");
            return;
        }
        Transform root = null;
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.parent == null && t.name == "Jackpot" && FindDeep(t, "Cylinder.007") != null) { root = t; break; }
        if (root == null) { Debug.LogError("[Jackpot] Machine 'Jackpot' introuvable."); return; }

        Renderer table = null;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (r.name == TableName && r.transform.root.name != "Jackpot") { table = r; break; }
        if (table == null) { Debug.LogError("[Jackpot] Table '" + TableName + "' introuvable."); return; }

        var item = SetupItem(root, out float machineDepth);
        var spot = SetupSpot(table, machineDepth);
        var bag = SetupBag(item, table);

        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Selection.activeGameObject = spot;
        Debug.Log("[Jackpot] Inventaire OK : machine réduite à " + TargetWidth + " m et lançable, livre ouvert + emplacement sur '" +
                  TableName + "', sac posé à côté de la table. Les textes du livre se modifient sur 'Livre ouvert' (liste Pages).");
    }

    // ---------------- La machine : objet attrapable et lançable ----------------
    static JackpotItem SetupItem(Transform root, out float depth)
    {
        var mf = root.GetComponent<MeshFilter>();
        var meshBounds = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);

        // Taille : largeur ramenée à TargetWidth (calculée sur le mesh, indépendamment de la rotation).
        Undo.RecordObject(root, "Taille machine");
        float width = meshBounds.size.x * root.lossyScale.x;
        float factor = width > 0.01f ? TargetWidth / width : 1f;
        if (Mathf.Abs(factor - 1f) > 0.01f) root.localScale *= factor;
        depth = meshBounds.size.z * root.lossyScale.z;
        PrefabUtility.RecordPrefabInstancePropertyModifications(root);

        BoxCollider body = null;
        foreach (var bc in root.GetComponents<BoxCollider>()) body = bc;
        if (body == null) body = Undo.AddComponent<BoxCollider>(root.gameObject);
        Undo.RecordObject(body, "Collider machine");
        body.center = meshBounds.center;
        body.size = meshBounds.size;
        PrefabUtility.RecordPrefabInstancePropertyModifications(body);

        foreach (var mc in root.GetComponentsInChildren<MeshCollider>(true))
        {
            if (mc.convex || !mc.enabled) continue;
            Undo.RecordObject(mc, "Mesh Collider");
            mc.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(mc);
        }

        var rb = root.GetComponent<Rigidbody>();
        if (rb == null) rb = Undo.AddComponent<Rigidbody>(root.gameObject);
        Undo.RecordObject(rb, "Rigidbody machine");
        rb.mass = 1f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        PrefabUtility.RecordPrefabInstancePropertyModifications(rb);

        Transform attach = Child(root, "Accroche bas");
        Undo.RecordObject(attach, "Accroche");
        attach.localPosition = new Vector3(meshBounds.center.x, meshBounds.min.y, meshBounds.center.z);

        Transform handle = Child(root, "Poignee sac");
        Undo.RecordObject(handle, "Poignee");
        handle.localPosition = body.center;
        var sphere = handle.GetComponent<SphereCollider>();
        if (sphere == null) sphere = Undo.AddComponent<SphereCollider>(handle.gameObject);
        Undo.RecordObject(sphere, "Poignee");
        sphere.center = Vector3.zero;
        sphere.radius = 0.17f / Mathf.Max(1e-5f, root.lossyScale.x);
        sphere.enabled = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(sphere);

        var grab = root.GetComponent<XRGrabInteractable>();
        if (grab == null) grab = Undo.AddComponent<XRGrabInteractable>(root.gameObject);
        Undo.RecordObject(grab, "Grab machine");
        grab.colliders.Clear();
        grab.colliders.Add(body);
        grab.colliders.Add(sphere);
        grab.attachTransform = attach;
        grab.useDynamicAttach = true;
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.throwOnDetach = true;                 // on peut lancer la machine
        grab.throwVelocityScale = 2.2f;
        grab.throwAngularVelocityScale = 1f;
        grab.trackScale = false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(grab);

        var item = root.GetComponent<JackpotItem>();
        if (item == null) item = Undo.AddComponent<JackpotItem>(root.gameObject);
        Undo.RecordObject(item, "Item machine");
        item.body = body;
        item.bagHandle = sphere;
        PrefabUtility.RecordPrefabInstancePropertyModifications(item);
        return item;
    }

    static Transform Child(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t == null)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, name);
            t = go.transform;
            t.SetParent(parent, false);
        }
        Undo.RecordObject(t, name);
        t.localRotation = Quaternion.identity;
        t.localScale = Vector3.one;
        return t;
    }

    // ---------------- La table : livre ouvert + emplacement unique au-dessus ----------------
    static GameObject SetupSpot(Renderer table, float machineDepth)
    {
        var parent = GameObject.Find("Emplacements Jackpot");
        if (parent == null)
        {
            parent = new GameObject("Emplacements Jackpot");
            Undo.RegisterCreatedObjectUndo(parent, "Emplacements");
        }
        // Un seul emplacement : on enlève les anciens (dont celui de la table ronde).
        for (int i = parent.transform.childCount - 1; i >= 0; i--)
            Undo.DestroyObjectImmediate(parent.transform.GetChild(i).gameObject);

        var b = table.bounds;
        Vector3 pos = new Vector3(b.center.x, b.max.y + 0.002f, b.center.z);
        Quaternion rot = Quaternion.LookRotation(FacePlayer(pos), Vector3.up);

        var spot = CreateSpot("Emplacement Jackpot (livre)", machineDepth);
        Undo.RegisterCreatedObjectUndo(spot, "Emplacement");
        spot.transform.SetParent(parent.transform, false);
        spot.transform.SetPositionAndRotation(pos, rot);

        // Modèle réutilisable (livre + emplacement), au cas où.
        if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/SampleJojo", "Prefabs");
        var tmp = CreateSpot("Emplacement Jackpot", machineDepth);
        PrefabUtility.SaveAsPrefabAsset(tmp, SpotPrefabPath);
        Object.DestroyImmediate(tmp);
        return spot;
    }

    static Vector3 FacePlayer(Vector3 from)
    {
        var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        Vector3 player = origin != null ? origin.transform.position : from + Vector3.forward;
        Vector3 d = Vector3.ProjectOnPlane(player - from, Vector3.up);
        return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
    }

    static GameObject CreateSpot(string name, float machineDepth)
    {
        float w = Mathf.Max(TargetWidth, 2 * PageW) + 0.08f;
        float d = Mathf.Max(machineDepth, PageD) + 0.1f;

        var spot = new GameObject(name);
        var trig = spot.AddComponent<BoxCollider>();
        trig.isTrigger = true;
        trig.center = new Vector3(0f, 0.2f, 0f);
        trig.size = new Vector3(w + 0.1f, 0.4f, d + 0.15f);

        var outline = new GameObject("Pointillés");
        outline.transform.SetParent(spot.transform, false);
        outline.AddComponent<MeshFilter>().sharedMesh = DashMesh(w, d);
        var mr = outline.AddComponent<MeshRenderer>();
        mr.sharedMaterial = DashMaterial();
        NoShadows(mr);

        // La machine se pose AU-DESSUS du livre.
        var attach = new GameObject("Accroche machine").transform;
        attach.SetParent(spot.transform, false);
        attach.localPosition = new Vector3(0f, BookHeight + 0.002f, 0f);

        var socket = spot.AddComponent<JackpotSocket>();
        socket.outline = outline;
        socket.attachTransform = attach;
        socket.showInteractableHoverMeshes = true;

        var book = CreateBook(spot.transform);
        book.blockingSocket = socket;
        return spot;
    }

    // ---------------- Le livre ouvert ----------------
    static JackpotBook CreateBook(Transform parent)
    {
        var bookGo = new GameObject("Livre ouvert");
        var t = bookGo.transform;
        t.SetParent(parent, false);
        t.localRotation = Quaternion.Euler(0f, 180f, 0f);     // le haut des pages vers le fond, lisible depuis le joueur

        var cover = Mat("Livre_Couverture", new Color(0.42f, 0.08f, 0.1f), false, 0.35f);
        var edges = Mat("Livre_Tranche", new Color(0.93f, 0.89f, 0.8f), false, 0.1f);
        var paper = Mat("Livre_Papier", new Color(0.98f, 0.95f, 0.87f), false, 0.05f);

        float half = PageW / 2f + 0.0005f;
        Part(t, "Couverture gauche", PrimitiveType.Cube, new Vector3(-half - 0.006f, CoverT / 2, 0), new Vector3(PageW + 0.012f, CoverT, PageD + 0.016f), cover);
        Part(t, "Couverture droite", PrimitiveType.Cube, new Vector3(half + 0.006f, CoverT / 2, 0), new Vector3(PageW + 0.012f, CoverT, PageD + 0.016f), cover);
        Part(t, "Reliure", PrimitiveType.Cube, new Vector3(0, PageTop / 2, 0), new Vector3(0.012f, PageTop, PageD + 0.016f), cover);
        Part(t, "Pages gauche", PrimitiveType.Cube, new Vector3(-half, CoverT + BlockT / 2, 0), new Vector3(PageW - 0.004f, BlockT, PageD - 0.004f), edges);
        Part(t, "Pages droite", PrimitiveType.Cube, new Vector3(half, CoverT + BlockT / 2, 0), new Vector3(PageW - 0.004f, BlockT, PageD - 0.004f), edges);
        Part(t, "Page gauche", PrimitiveType.Cube, new Vector3(-half, PageTop + 0.0003f, 0), new Vector3(PageW - 0.006f, 0.0006f, PageD - 0.008f), paper);
        Part(t, "Page droite", PrimitiveType.Cube, new Vector3(half, PageTop + 0.0003f, 0), new Vector3(PageW - 0.006f, 0.0006f, PageD - 0.008f), paper);

        var leftText = Text(t, "Texte gauche", new Vector3(-half, PageTop + 0.0009f, 0), true);
        var rightText = Text(t, "Texte droite", new Vector3(half, PageTop + 0.0009f, 0), true);

        // Feuille qui tourne : pivot sur la reliure.
        var leaf = new GameObject("Feuille qui tourne").transform;
        leaf.SetParent(t, false);
        leaf.localPosition = new Vector3(0f, PageTop + 0.0012f, 0f);
        Part(leaf, "Papier", PrimitiveType.Cube, new Vector3(half, 0, 0), new Vector3(PageW - 0.006f, 0.0005f, PageD - 0.008f), paper);
        var front = Text(leaf, "Recto", new Vector3(half, 0.0005f, 0), true);
        var back = Text(leaf, "Verso", new Vector3(half, -0.0005f, 0), false);

        // Zones à pincer pour tourner les pages.
        var rightZone = Zone(t, "Zone page droite", new Vector3(half + 0.01f, PageTop + 0.03f, 0));
        var leftZone = Zone(t, "Zone page gauche", new Vector3(-half - 0.01f, PageTop + 0.03f, 0));

        var src = bookGo.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.minDistance = 0.5f;
        src.maxDistance = 8f;

        var book = bookGo.AddComponent<JackpotBook>();
        book.leftText = leftText;
        book.rightText = rightText;
        book.leaf = leaf;
        book.leafFrontText = front;
        book.leafBackText = back;
        book.rightZone = rightZone;
        book.leftZone = leftZone;
        book.audioSource = src;
        book.flipClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/page_flip.wav");
        leftText.text = book.pages.Length > 0 ? book.pages[0] : "";
        rightText.text = book.pages.Length > 1 ? book.pages[1] : "";
        leaf.gameObject.SetActive(false);
        return book;
    }

    static TextMeshPro Text(Transform parent, string name, Vector3 pos, bool facingUp)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        // Texte à plat, le haut du texte vers le haut de la page (+Z du livre).
        go.transform.localRotation = Quaternion.LookRotation(facingUp ? Vector3.down : Vector3.up, Vector3.forward);
        var tmp = go.AddComponent<TextMeshPro>();
        if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        tmp.rectTransform.sizeDelta = new Vector2(PageW - 0.03f, PageD - 0.035f);
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.05f;
        tmp.fontSizeMax = 0.28f;
        tmp.color = new Color(0.22f, 0.13f, 0.07f);
        tmp.alignment = TextAlignmentOptions.TopLeft;
        tmp.text = "";
        var r = go.GetComponent<MeshRenderer>();
        if (r != null) NoShadows(r);
        return tmp;
    }

    static XRSimpleInteractable Zone(Transform parent, string name, Vector3 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var col = go.AddComponent<BoxCollider>();
        col.size = new Vector3(PageW * 0.8f, 0.06f, PageD);
        return go.AddComponent<XRSimpleInteractable>();
    }

    // ---------------- Le sac, posé à côté de la table ----------------
    static JackpotBag SetupBag(JackpotItem item, Renderer table)
    {
        GameObject bagGo = GameObject.Find("Sac Jackpot");
        if (bagGo == null)
        {
            bagGo = new GameObject("Sac Jackpot");
            Undo.RegisterCreatedObjectUndo(bagGo, "Sac");
            BuildBagVisual(bagGo.transform);
        }
        foreach (var oldCol in bagGo.GetComponents<Collider>())
            Undo.DestroyObjectImmediate(oldCol);

        var src = bagGo.GetComponent<AudioSource>();
        if (src == null) src = Undo.AddComponent<AudioSource>(bagGo);
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.minDistance = 0.5f;
        src.maxDistance = 10f;

        var bag = bagGo.GetComponent<JackpotBag>();
        if (bag == null) bag = Undo.AddComponent<JackpotBag>(bagGo);
        Undo.RecordObject(bag, "Sac");
        bag.item = item;
        bag.startInBag = true;
        bag.followPlayer = false;
        bag.audioSource = src;
        bag.takeClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/reel_stop.wav");
        bag.storeClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/lever_pull.wav");
        var glow = bagGo.transform.Find("Visuel/Lueur");
        bag.glow = glow != null ? glow.GetComponent<Renderer>() : null;
        EditorUtility.SetDirty(bag);

        Undo.RecordObject(item, "Item machine");
        item.bag = bag;
        PrefabUtility.RecordPrefabInstancePropertyModifications(item);

        // Par terre, à droite de la table (vu depuis le joueur), vers l'avant de la table.
        var b = table.bounds;
        Vector3 toPlayer = FacePlayer(b.center);
        Vector3 right = Vector3.Cross(Vector3.up, -toPlayer);
        float sideExtent = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(right.x), 0, Mathf.Abs(right.z))));
        float frontExtent = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(toPlayer.x), 0, Mathf.Abs(toPlayer.z))));
        Vector3 pos = new Vector3(b.center.x, b.min.y, b.center.z) + right * (sideExtent + 0.28f) + toPlayer * (frontExtent * 0.55f);
        Undo.RecordObject(bagGo.transform, "Sac");
        bagGo.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(toPlayer, Vector3.up));
        return bag;
    }

    static void BuildBagVisual(Transform bag)
    {
        var visual = new GameObject("Visuel").transform;
        visual.SetParent(bag, false);
        Part(visual, "Corps", PrimitiveType.Cylinder, new Vector3(0f, 0.14f, 0f), new Vector3(0.34f, 0.14f, 0.34f), Mat("Sac_Cuir", new Color(0.45f, 0.27f, 0.13f), false, 0.25f));
        Part(visual, "Bord", PrimitiveType.Cylinder, new Vector3(0f, 0.285f, 0f), new Vector3(0.37f, 0.015f, 0.37f), Mat("Sac_CuirFonce", new Color(0.25f, 0.14f, 0.07f), false, 0.25f));
        Part(visual, "Ouverture", PrimitiveType.Cylinder, new Vector3(0f, 0.297f, 0f), new Vector3(0.29f, 0.004f, 0.29f), Mat("Sac_Interieur", new Color(0.05f, 0.04f, 0.03f), false, 0.25f));
        Part(visual, "Lueur", PrimitiveType.Cylinder, new Vector3(0f, 0.302f, 0f), new Vector3(0.33f, 0.002f, 0.33f), Mat("Sac_Lueur", new Color(1f, 0.75f, 0.2f), true, 0.6f));
        Part(visual, "Anse gauche", PrimitiveType.Cube, new Vector3(-0.15f, 0.34f, 0f), new Vector3(0.025f, 0.1f, 0.05f), Mat("Sac_CuirFonce", new Color(0.25f, 0.14f, 0.07f), false, 0.25f));
        Part(visual, "Anse droite", PrimitiveType.Cube, new Vector3(0.15f, 0.34f, 0f), new Vector3(0.025f, 0.1f, 0.05f), Mat("Sac_CuirFonce", new Color(0.25f, 0.14f, 0.07f), false, 0.25f));
    }

    // ---------------- Utilitaires ----------------
    static void Part(Transform parent, string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        NoShadows(r);
    }

    static void NoShadows(Renderer r)
    {
        r.shadowCastingMode = ShadowCastingMode.Off;      // petits objets : pas d'ombres (FPS)
        r.lightProbeUsage = LightProbeUsage.Off;
        r.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    static Material Mat(string name, Color color, bool emissive, float smooth)
    {
        string path = ModelFolder + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
        if (emissive)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * 1.2f);
        }
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>Mesh des pointillés : des petits rectangles plats posés sur la table (un seul rendu).</summary>
    static Mesh DashMesh(float w, float d)
    {
        string path = ModelFolder + "Pointilles_" + Mathf.RoundToInt(w * 1000) + "x" + Mathf.RoundToInt(d * 1000) + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;

        const float dash = 0.035f, gap = 0.03f, thick = 0.012f, y = 0.001f;
        var verts = new List<Vector3>();
        var tris = new List<int>();
        Vector3[] corners =
        {
            new Vector3(-w / 2, y, -d / 2), new Vector3(w / 2, y, -d / 2),
            new Vector3(w / 2, y, d / 2), new Vector3(-w / 2, y, d / 2)
        };
        for (int s = 0; s < 4; s++)
        {
            Vector3 a = corners[s], b = corners[(s + 1) % 4];
            Vector3 dir = b - a;
            float len = dir.magnitude;
            dir /= len;
            Vector3 side = Vector3.Cross(Vector3.up, dir) * (thick / 2f);
            int count = Mathf.Max(1, Mathf.FloorToInt((len + gap) / (dash + gap)));
            float start = (len - (count * dash + (count - 1) * gap)) / 2f;
            for (int i = 0; i < count; i++)
            {
                Vector3 p0 = a + dir * (start + i * (dash + gap));
                Vector3 p1 = p0 + dir * dash;
                int v = verts.Count;
                verts.Add(p0 - side); verts.Add(p0 + side); verts.Add(p1 + side); verts.Add(p1 - side);
                tris.Add(v); tris.Add(v + 2); tris.Add(v + 1);      // face visible vers le haut
                tris.Add(v); tris.Add(v + 3); tris.Add(v + 2);
            }
        }
        var mesh = new Mesh { name = "Pointillés" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        var normals = new Vector3[verts.Count];
        for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
        mesh.normals = normals;
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    static Material DashMaterial()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(DashMatPath);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            AssetDatabase.CreateAsset(mat, DashMatPath);
        }
        return mat;
    }

    // ---------------- Tests sans casque ----------------
    [MenuItem("Tools/Jackpot/5 - Test : poser la machine sur le livre (en Play) %#j")]
    static void TestPlace()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Jackpot] Lance d'abord le mode Play."); return; }
        var bag = Object.FindFirstObjectByType<JackpotBag>();
        var socket = Object.FindFirstObjectByType<JackpotSocket>();
        if (bag == null || socket == null) { Debug.LogError("[Jackpot] Sac ou emplacement introuvable."); return; }
        bag.TestPlaceOn(socket);
        Debug.Log("[Jackpot] Test : machine posée sur '" + socket.name + "'.");
    }

    [MenuItem("Tools/Jackpot/6 - Test : ranger la machine dans le sac (en Play) %#k")]
    static void TestStore()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Jackpot] Lance d'abord le mode Play."); return; }
        var bag = Object.FindFirstObjectByType<JackpotBag>();
        if (bag != null) { bag.TestStore(); Debug.Log("[Jackpot] Test : machine rangée dans le sac."); }
    }

    [MenuItem("Tools/Jackpot/7 - Test : page suivante (en Play) %#u")]
    static void TestNextPage() => TurnPage(1);

    [MenuItem("Tools/Jackpot/8 - Test : page précédente (en Play) %#y")]
    static void TestPrevPage() => TurnPage(-1);

    static void TurnPage(int dir)
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Jackpot] Lance d'abord le mode Play."); return; }
        var book = Object.FindFirstObjectByType<JackpotBook>();
        if (book != null) book.TestTurn(dir);
    }

    static Transform FindDeep(Transform r, string n)
    {
        if (r.name == n) return r;
        foreach (Transform c in r)
        {
            var f = FindDeep(c, n);
            if (f != null) return f;
        }
        return null;
    }
}
