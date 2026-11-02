using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Prend seulement certains objets du .blend « Salle de Classe WALAD » et les place dans la classe du bas :
/// - le tableau contre le mur du tableau (remplace l'ancien) ;
/// - la porte dans l'ouverture de la porte existante (remplace l'ancienne) ;
/// - une trousse sur chacune de tes tables (objet « Desk ») ;
/// - les armoires (haute + basse, avec le globe) au fond de la salle, contre le mur sans tables.
/// Menu : Tools > Jackpot > 18 - Salle : tableau, porte, trousses, armoires.
/// </summary>
public static class ClassroomSetup
{
    const string Blend = "Assets/SampleJojo/model/Props/clasroom/Salle de Classe WALAD .blend";
    const string RootName = "Salle - objets du .blend";
    const float FloorY = 1.525f;

    // Mur du tableau (avant de la classe) : face intérieure à x ≈ 1.955.
    const float FrontWallX = 1.955f;
    const float BoardCenterZ = 5.65f, BoardCenterY = 2.80f, BoardHeight = 1.26f;
    // Ouverture de la porte dans le mur avant.
    const float DoorZMin = 1.955f, DoorZMax = 3.29f, DoorHeight = 1.97f;
    // Mur du fond : face intérieure à x ≈ -7.435.
    const float BackWallX = -7.435f;
    const float CabinetsEndZ = 7.85f;
    const float PropScale = 0.40f;            // échelle du .blend pour les meubles (porte de 5 unités ≈ 2 m)

    [MenuItem("Tools/Jackpot/18 - Salle : tableau, porte, trousses, armoires", false, 129)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Salle] Quitte le mode Play avant."); return; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Blend);
        if (prefab == null) { Debug.LogError("[Salle] .blend introuvable : " + Blend); return; }

        var old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, RootName);

        // On pose tout le .blend, on garde seulement ce qu'il faut.
        var src = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(src, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var log = new List<string>();

        // ---- Tableau ----
        var board = Take(src, "Cube", root, "Tableau");
        if (board != null)
        {
            Yaw(board, 180f);                                         // face écrite vers la classe (-X)
            ScaleTo(board, BoardHeight / Size(board).y);
            Bounds b = B(board);
            Move(board, new Vector3(FrontWallX - b.extents.x - 0.005f, BoardCenterY, BoardCenterZ) - b.center);
            Hide("Tableau.003", "TableauContour.003");
            log.Add("tableau");
        }

        // ---- Porte ----
        var door = Take(src, "Cube.003", root, "Porte");
        if (door != null)
        {
            Yaw(door, 90f);                                           // dans le mur avant (largeur le long de Z)
            ScaleTo(door, DoorHeight / Size(door).y);
            float width = DoorZMax - DoorZMin + 0.04f;
            Vector3 s = Size(door);
            if (s.z > 1e-4f) StretchWorldZ(door, width / s.z);        // remplit l'ouverture
            Bounds b = B(door);
            Move(door, new Vector3(FrontWallX + 0.165f, FloorY + b.extents.y, (DoorZMin + DoorZMax) * 0.5f) - b.center);
            Hide("Cube.038");
            log.Add("porte");
        }

        // ---- Armoires au fond (haute + basse avec le globe) ----
        var tall = Take(src, "Cube.001", root, "Armoire haute");
        var low = Take(src, "Cube.002", root, "Armoire basse");
        var globe = Take(src, "Circle.001", root, "Globe");
        float z = CabinetsEndZ;
        foreach (var cab in new[] { tall, low })
        {
            if (cab == null) continue;
            Yaw(cab, 180f);                                           // portes vers la classe (+X)
            ScaleTo(cab, PropScale);
            Bounds b = B(cab);
            Move(cab, new Vector3(BackWallX + b.extents.x + 0.02f, FloorY + b.extents.y, z - b.extents.z) - b.center);
            z -= b.size.z + 0.08f;
        }
        if (globe != null && low != null)
        {
            ScaleTo(globe, PropScale);
            Bounds lb = B(low), gb = B(globe);
            Move(globe, new Vector3(lb.center.x, lb.max.y + gb.extents.y, lb.center.z) - gb.center);
        }
        if (tall != null || low != null) log.Add("armoires");

        // ---- Trousses sur tes tables ----
        var cases = new List<GameObject>();
        for (int i = 1; i <= 8; i++)
        {
            var c = Take(src, "Cylinder.00" + i, null, "Trousse " + i);
            if (c != null) { ScaleTo(c, PropScale); cases.Add(c); }
        }
        int placed = 0;
        var desks = UserDeskTops();
        var rnd = new System.Random(7);
        for (int i = 0; i < desks.Count && cases.Count > 0; i++)
        {
            var model = cases[i % cases.Count];
            var t = i < cases.Count ? model : Object.Instantiate(model);
            t.name = "Trousse (" + (i + 1) + ")";
            t.transform.SetParent(root.transform, true);
            Yaw(t, (float)rnd.NextDouble() * 360f);
            Bounds top = desks[i];
            Bounds b = B(t);
            // Un peu sur le côté de la table, pas au milieu.
            Vector3 pos = new Vector3(top.center.x, top.max.y + b.extents.y + 0.002f, top.center.z + top.extents.z * 0.45f);
            Move(t, pos - b.center);
            placed++;
        }
        foreach (var c in cases) if (c != null && c.transform.parent == null) Object.DestroyImmediate(c);   // trousses en trop
        log.Add(placed + " trousses");

        Object.DestroyImmediate(src);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[Salle] OK : " + string.Join(", ", log) + ".");
    }

    // ======================= Fenêtre, livres, bureau du prof =======================
    // Fenêtre de la salle : vitre « Plane.131 » sur le mur z ≈ 8.0, entre x ≈ -2.94 et 0.69.
    static readonly string[] OldWindow = { "Plane.131", "Venetian_slats.017", "Venetian_slats.020", "VenetianFrame.017", "VenetianFrame.020" };

    [MenuItem("Tools/Jackpot/19 - Salle : fenêtre, livres, bureau du prof", false, 130)]
    static void Setup2()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Salle] Quitte le mode Play avant."); return; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Blend);
        if (prefab == null) { Debug.LogError("[Salle] .blend introuvable : " + Blend); return; }
        var root = GameObject.Find(RootName);
        if (root == null) { root = new GameObject(RootName); Undo.RegisterCreatedObjectUndo(root, RootName); }
        foreach (var n in new[] { "Fenetre", "Livres", "Bureau prof (.blend)" })
        {
            var o = root.transform.Find(n);
            if (o != null) Undo.DestroyObjectImmediate(o.gameObject);
        }

        var src = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        PrefabUtility.UnpackPrefabInstance(src, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var log = new List<string>();

        // ---- Fenêtre : remplace la vitre et les stores, à la taille de l'ouverture ----
        var oldGlass = FindRenderer("Plane.131");
        var win = Take(src, "Fenêtre", root, "Fenetre");
        if (win != null && oldGlass != null)
        {
            Bounds gb = oldGlass.bounds;
            Yaw(win, 180f);                                            // côté intérieur vers la classe (-Z)
            Vector3 s = Size(win);
            Vector3 ls = win.transform.localScale;
            float fx = gb.size.x / Mathf.Max(1e-4f, s.x), fy = gb.size.y / Mathf.Max(1e-4f, s.y);
            // Le support n'est tourné qu'autour de Y (180°) : X/Y locaux = X/Y monde.
            win.transform.localScale = new Vector3(ls.x * fx, ls.y * fy, ls.z * fy);
            Bounds b = B(win);
            Move(win, new Vector3(gb.center.x, gb.center.y, gb.center.z - 0.03f) - b.center);
            Hide(OldWindow);
            log.Add("fenêtre");
        }
        else log.Add("fenêtre NON placée (vitre Plane.131 ou objet Fenêtre introuvable)");

        // ---- Livres ouverts sur les tables des élèves ----
        var books = new List<GameObject>();
        for (int i = 0; i <= 8; i++)
        {
            var b = Take(src, i == 0 ? "book_a_open" : "book_a_open.00" + i, null, "Livre");
            if (b != null) books.Add(b);
        }
        var holder = new GameObject("Livres");
        holder.transform.SetParent(root.transform, false);
        var desks = UserDeskTops();
        var rnd = new System.Random(11);
        int placed = 0;
        for (int i = 0; i < desks.Count && books.Count > 0; i++)
        {
            var model = books[i % books.Count];
            var bk = i < books.Count ? model : Object.Instantiate(model);
            bk.name = "Livre (" + (i + 1) + ")";
            bk.transform.SetParent(holder.transform, true);
            bk.transform.rotation = Quaternion.identity;
            bk.transform.localScale = Vector3.one;
            // Le livre ouvert fait environ 32 cm de large.
            Bounds b0 = B(bk);
            float w = Mathf.Max(b0.size.x, b0.size.z);
            if (w > 1e-4f) bk.transform.localScale *= 0.32f / w;
            Yaw(bk, (float)(rnd.NextDouble() * 24.0 - 12.0));
            Bounds top = desks[i];
            Bounds b = B(bk);
            Move(bk, new Vector3(top.center.x, top.max.y + b.extents.y + 0.001f, top.center.z - top.extents.z * 0.25f) - b.center);
            placed++;
        }
        foreach (var bk in books) if (bk != null && bk.transform.parent == null) Object.DestroyImmediate(bk);
        log.Add(placed + " livres");

        // ---- Bureau du prof : remplace le bureau actuel par celui du .blend ----
        var current = CurrentProfDesk(out string which);
        var newDesk = Take(src, "Bueau", root, "Bureau prof (.blend)");
        if (newDesk != null && current != null)
        {
            Bounds ob = B(current);
            Yaw(newDesk, 180f);                                        // côté prof vers le tableau (+X)
            ScaleTo(newDesk, PropScale);
            Bounds b = B(newDesk);
            Move(newDesk, new Vector3(ob.center.x, FloorY + b.extents.y, ob.center.z) - b.center);
            b = B(newDesk);
            foreach (var c in newDesk.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var box = newDesk.AddComponent<BoxCollider>();
            box.center = newDesk.transform.InverseTransformPoint(b.center);
            Vector3 ls = newDesk.transform.lossyScale;
            box.size = new Vector3(b.size.x / Mathf.Abs(ls.x), b.size.y / Mathf.Abs(ls.y), b.size.z / Mathf.Abs(ls.z));
            // Le plateau et le cacher : l'ancien bureau est désactivé (pas supprimé).
            Undo.RecordObject(current, "Ancien bureau");
            current.SetActive(false);
            // Le ventilateur passe sur le nouveau bureau.
            var fan = GameObject.Find("Ventilateur");
            if (fan != null)
            {
                Undo.RecordObject(fan.transform, "Ventilateur");
                Bounds fb = B(fan);
                Vector3 target = new Vector3(b.center.x, b.max.y + fb.extents.y + 0.001f, b.min.z + fb.extents.z + 0.1f);
                fan.transform.position += target - fb.center;
            }
            log.Add("bureau du prof remplacé (ancien : " + which + ", caché)");
        }
        else log.Add("bureau du prof NON remplacé (" + (current == null ? "bureau actuel introuvable" : "objet Bueau absent") + ")");

        Object.DestroyImmediate(src);
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log("[Salle] OK : " + string.Join(", ", log) + ".");
    }

    /// <summary>Le bureau du prof actuellement dans la salle (devant le tableau).</summary>
    static GameObject CurrentProfDesk(out string which)
    {
        which = "";
        // 1) Le bureau de l'utilisateur (avec « Desk_Top »), 2) sinon celui posé par le menu 17.
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (r.name != "Desk_Top" || r.bounds.min.y > 5f) continue;
            Transform t = r.transform;
            while (t.parent != null && t.parent.name != "Bureau du prof") t = t.parent;
            which = t.name; return t.gameObject;
        }
        var bp = GameObject.Find("Bureau du prof");
        var child = bp != null ? bp.transform.Find("Bureau") : null;
        if (child != null && child.gameObject.activeInHierarchy) { which = "Bureau du prof/Bureau"; return child.gameObject; }
        return null;
    }

    static Renderer FindRenderer(string name)
    {
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (r.name == name && r.bounds.min.y < 5f) return r;
        return null;
    }

    // ======================= Sol =======================
    // Intérieur de la classe du bas (entre les murs).
    const float RoomMinX = -7.435f, RoomMaxX = 1.955f, RoomMinZ = 0.55f, RoomMaxZ = 8.0f;

    [MenuItem("Tools/Jackpot/20 - Salle : sol du .blend", false, 131)]
    static void SetupFloor()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Salle] Quitte le mode Play avant."); return; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Blend);
        if (prefab == null) { Debug.LogError("[Salle] .blend introuvable : " + Blend); return; }
        var root = GameObject.Find(RootName);
        if (root == null) { root = new GameObject(RootName); Undo.RegisterCreatedObjectUndo(root, RootName); }
        var oldFloor = root.transform.Find("Sol");
        if (oldFloor != null) Undo.DestroyObjectImmediate(oldFloor.gameObject);

        var src = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        src.transform.position = Vector3.zero;
        // Le sol est la partie « Sol » de l'objet « Base ».
        MeshRenderer baseR = null; int sub = -1;
        foreach (var r in src.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].name.StartsWith("Sol")) { baseR = r; sub = i; break; }
            if (baseR != null) break;
        }
        if (baseR == null) { Object.DestroyImmediate(src); Debug.LogError("[Salle] Matériau « Sol » introuvable dans le .blend."); return; }
        var mf = baseR.GetComponent<MeshFilter>();
        var mesh = mf.sharedMesh;
        var mat = baseR.sharedMaterials[sub];

        // Taille des carreaux : combien d'UV par mètre sur le sol d'origine (à l'échelle des autres objets).
        var verts = mesh.vertices; var uvs = mesh.uv;
        var idx = mesh.GetIndices(sub).Distinct().ToArray();
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
        double cUX = 0, cUZ = 0;   // pour savoir si U suit X ou Z
        Vector3 c0 = Vector3.zero;
        foreach (var i in idx) c0 += mf.transform.TransformPoint(verts[i]);
        c0 /= Mathf.Max(1, idx.Length);
        foreach (var i in idx)
        {
            Vector3 w = mf.transform.TransformPoint(verts[i]);
            minX = Mathf.Min(minX, w.x); maxX = Mathf.Max(maxX, w.x); minZ = Mathf.Min(minZ, w.z); maxZ = Mathf.Max(maxZ, w.z);
            if (uvs.Length > i)
            {
                Vector2 uv = uvs[i];
                minU = Mathf.Min(minU, uv.x); maxU = Mathf.Max(maxU, uv.x); minV = Mathf.Min(minV, uv.y); maxV = Mathf.Max(maxV, uv.y);
                cUX += (w.x - c0.x) * uv.x; cUZ += (w.z - c0.z) * uv.x;
            }
        }
        bool uAlongX = System.Math.Abs(cUX) >= System.Math.Abs(cUZ);
        float sizeU = (uAlongX ? (maxX - minX) : (maxZ - minZ)) * PropScale;
        float sizeV = (uAlongX ? (maxZ - minZ) : (maxX - minX)) * PropScale;
        float uPerM = sizeU > 1e-4f ? (maxU - minU) / sizeU : 1f;
        float vPerM = sizeV > 1e-4f ? (maxV - minV) / sizeV : 1f;
        Object.DestroyImmediate(src);

        // Un grand rectangle sur tout le sol de la classe, posé juste au-dessus de l'ancien sol.
        var m = new Mesh { name = "Sol classe" };
        float y = FloorY + 0.004f;
        var p = new[] { new Vector3(RoomMinX, y, RoomMinZ), new Vector3(RoomMaxX, y, RoomMinZ), new Vector3(RoomMinX, y, RoomMaxZ), new Vector3(RoomMaxX, y, RoomMaxZ) };
        m.vertices = p;
        m.uv = p.Select(v => uAlongX ? new Vector2(v.x * uPerM, v.z * vPerM) : new Vector2(v.z * uPerM, v.x * vPerM)).ToArray();
        m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
        string meshPath = "Assets/SampleJojo/model/Props/clasroom/Sol classe.asset";
        AssetDatabase.DeleteAsset(meshPath);
        AssetDatabase.CreateAsset(m, meshPath);

        var floor = new GameObject("Sol");
        Undo.RegisterCreatedObjectUndo(floor, "Sol");
        floor.transform.SetParent(root.transform, false);
        floor.AddComponent<MeshFilter>().sharedMesh = m;
        var fr = floor.AddComponent<MeshRenderer>();
        fr.sharedMaterial = mat;
        fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[Salle] Sol posé ({RoomMaxX - RoomMinX:F1} x {RoomMaxZ - RoomMinZ:F1} m), matériau « {mat.name} », {uPerM:F2} x {vPerM:F2} UV/m.");
    }

    // ======================= Murs =======================
    const float CeilingY = 3.49f;

    [MenuItem("Tools/Jackpot/21 - Salle : murs du .blend", false, 132)]
    static void SetupWalls()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Salle] Quitte le mode Play avant."); return; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Blend);
        if (prefab == null) { Debug.LogError("[Salle] .blend introuvable : " + Blend); return; }
        var root = GameObject.Find(RootName);
        if (root == null) { root = new GameObject(RootName); Undo.RegisterCreatedObjectUndo(root, RootName); }
        var oldWalls = root.transform.Find("Murs");
        if (oldWalls != null) Undo.DestroyObjectImmediate(oldWalls.gameObject);

        // Matériau des murs : la partie « Mur » la plus grande de l'objet « Base ».
        var src = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        src.transform.position = Vector3.zero;
        Material wallMat = null; float bestArea = 0f; float uPerM = 0.5f, vPerM = 0.5f;
        foreach (var r in src.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var mesh = mf.sharedMesh; var verts = mesh.vertices; var uvs = mesh.uv;
            var mats = r.sharedMaterials;
            for (int sIdx = 0; sIdx < mats.Length && sIdx < mesh.subMeshCount; sIdx++)
            {
                if (mats[sIdx] == null || !mats[sIdx].name.StartsWith("Mur")) continue;
                var idx = mesh.GetIndices(sIdx).Distinct().ToArray();
                if (idx.Length == 0) continue;
                Bounds wb = new Bounds(mf.transform.TransformPoint(verts[idx[0]]), Vector3.zero);
                float minU = float.MaxValue, maxU = float.MinValue, minV = float.MaxValue, maxV = float.MinValue;
                foreach (var i in idx)
                {
                    wb.Encapsulate(mf.transform.TransformPoint(verts[i]));
                    if (uvs.Length > i) { minU = Mathf.Min(minU, uvs[i].x); maxU = Mathf.Max(maxU, uvs[i].x); minV = Mathf.Min(minV, uvs[i].y); maxV = Mathf.Max(maxV, uvs[i].y); }
                }
                float horiz = Mathf.Max(wb.size.x, wb.size.z), area = horiz * wb.size.y;
                if (area <= bestArea) continue;
                bestArea = area; wallMat = mats[sIdx];
                if (maxU > minU && horiz > 1e-4f) uPerM = (maxU - minU) / (horiz * PropScale);
                if (maxV > minV && wb.size.y > 1e-4f) vPerM = (maxV - minV) / (wb.size.y * PropScale);
            }
        }
        Object.DestroyImmediate(src);
        if (wallMat == null) { Debug.LogError("[Salle] Matériau de mur (« Mur… ») introuvable dans le .blend."); return; }

        var holder = new GameObject("Murs");
        Undo.RegisterCreatedObjectUndo(holder, "Murs");
        holder.transform.SetParent(root.transform, false);
        float h = CeilingY - FloorY;
        const float off = 0.003f;   // juste devant le vrai mur
        int n = 0;
        // Mur du fond (x = -7.435), face vers +X.
        n += Panel(holder, wallMat, uPerM, vPerM, "Mur fond", new Vector3(RoomMinX + off, FloorY, RoomMinZ), Vector3.forward, RoomMaxZ - RoomMinZ, h);
        // Mur du tableau (x = 1.955), face vers -X, sans l'ouverture de la porte.
        n += Panel(holder, wallMat, uPerM, vPerM, "Mur tableau (1)", new Vector3(RoomMaxX - off, FloorY, DoorZMin), Vector3.back, DoorZMin - RoomMinZ, h);
        n += Panel(holder, wallMat, uPerM, vPerM, "Mur tableau (2)", new Vector3(RoomMaxX - off, FloorY, RoomMaxZ), Vector3.back, RoomMaxZ - DoorZMax, h);
        // Au-dessus de la porte.
        n += Panel(holder, wallMat, uPerM, vPerM, "Mur tableau (dessus porte)", new Vector3(RoomMaxX - off, FloorY + DoorHeight, DoorZMax), Vector3.back, DoorZMax - DoorZMin, h - DoorHeight);
        // Mur des fenêtres (z = 8.0), face vers -Z, sans la fenêtre (x -2.94 → 0.69) ni la porte du coin (x 0.73 → 1.71).
        n += Panel(holder, wallMat, uPerM, vPerM, "Mur fenêtres (1)", new Vector3(RoomMinX, FloorY, RoomMaxZ - off), Vector3.right, -2.94f - RoomMinX, h);
        n += Panel(holder, wallMat, uPerM, vPerM, "Mur fenêtres (2)", new Vector3(1.71f, FloorY, RoomMaxZ - off), Vector3.right, RoomMaxX - 1.71f, h);
        // Mur d'en face (z = 0.55), face vers +Z.
        n += Panel(holder, wallMat, uPerM, vPerM, "Mur côté couloir", new Vector3(RoomMaxX, FloorY, RoomMinZ + off), Vector3.left, RoomMaxX - RoomMinX, h);

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[Salle] Murs posés : {n} panneaux, matériau « {wallMat.name} ».");
    }

    /// <summary>Panneau vertical : part de <paramref name="start"/> (en bas), s'étend de <paramref name="width"/> dans <paramref name="dir"/>.
    /// La face visible regarde vers (dir.z, 0, -dir.x) : dir est choisi pour qu'elle regarde la classe.</summary>
    static int Panel(GameObject parent, Material mat, float uPerM, float vPerM, string name, Vector3 start, Vector3 dir, float width, float height)
    {
        if (width <= 0.01f || height <= 0.01f) return 0;
        var m = new Mesh { name = name };
        Vector3 a = start, b = start + dir * width, c = a + Vector3.up * height, d = b + Vector3.up * height;
        m.vertices = new[] { a, b, c, d };
        float u0 = Vector3.Dot(a, dir) * uPerM;
        m.uv = new[]
        {
            new Vector2(u0, a.y * vPerM), new Vector2(u0 + width * uPerM, a.y * vPerM),
            new Vector2(u0, c.y * vPerM), new Vector2(u0 + width * uPerM, c.y * vPerM),
        };
        m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = m;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        string path = "Assets/SampleJojo/model/Props/clasroom/" + name.Replace("(", "").Replace(")", "") + ".asset";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
        return 1;
    }

    // ======================= Lampes =======================
    const string LampModel = "Assets/SampleJojo/model/Props/clasroom/model.dae";
    const string LampTex = "Assets/SampleJojo/model/Props/clasroom/textures/lamp_01_";

    [MenuItem("Tools/Jackpot/22 - Salle : lampes + interrupteur", false, 133)]
    static void SetupLamps()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Salle] Quitte le mode Play avant."); return; }
        var lampPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LampModel);
        if (lampPrefab == null) { Debug.LogError("[Salle] Lampe introuvable : " + LampModel); return; }
        var root = GameObject.Find(RootName);
        if (root == null) { root = new GameObject(RootName); Undo.RegisterCreatedObjectUndo(root, RootName); }
        var old = root.transform.Find("Lampes");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        // Matériau avec toutes les textures (normal map réglée comme telle).
        var nrm = AssetImporter.GetAtPath(LampTex + "normal.png") as TextureImporter;
        if (nrm != null && nrm.textureType != TextureImporterType.NormalMap) { nrm.textureType = TextureImporterType.NormalMap; nrm.SaveAndReimport(); }
        var mat = LampMaterial();

        var holder = new GameObject("Lampes");
        Undo.RegisterCreatedObjectUndo(holder, "Lampes");
        holder.transform.SetParent(root.transform, false);

        float[] xs = { -5.85f, -2.75f, 0.35f };
        float[] zs = { 2.4f, 6.1f };
        var lights = new List<Light>();
        var panels = new List<Renderer>();
        int n = 0;
        foreach (var x in xs)
            foreach (var z in zs)
            {
                n++;
                var lamp = (GameObject)PrefabUtility.InstantiatePrefab(lampPrefab);
                lamp.name = "Lampe " + n;
                lamp.transform.SetParent(holder.transform, false);
                lamp.transform.localScale = Vector3.one;
                Bounds b = B(lamp);
                float w = Mathf.Max(b.size.x, b.size.z);
                if (w > 1e-4f) lamp.transform.localScale *= 0.6f / w;      // panneau de 60 cm
                b = B(lamp);
                Move(lamp, new Vector3(x, CeilingY - b.extents.y - 0.005f, z) - b.center);
                foreach (var r in lamp.GetComponentsInChildren<Renderer>())
                {
                    r.sharedMaterial = mat;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    panels.Add(r);
                }
                foreach (var c in lamp.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

                // La vraie lumière, juste sous le panneau.
                var lg = new GameObject("Lumière");
                lg.transform.SetParent(lamp.transform, true);
                lg.transform.position = new Vector3(x, B(lamp).min.y - 0.08f, z);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 4.5f;
                l.intensity = 1.6f;
                l.color = new Color(1f, 0.95f, 0.86f);
                l.shadows = LightShadows.None;           // pas d'ombres : plus léger pour le casque
                l.renderMode = LightRenderMode.Auto;
                lights.Add(l);
            }

        // Interrupteur sur le mur du tableau, à côté de la porte (côté intérieur).
        var sw = new GameObject("Interrupteur");
        sw.transform.SetParent(holder.transform, false);
        sw.transform.position = new Vector3(FrontWallX - 0.015f, FloorY + 1.05f, DoorZMax + 0.25f);
        sw.transform.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);     // face vers la classe
        var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plate.name = "Plaque";
        plate.transform.SetParent(sw.transform, false);
        plate.transform.localScale = new Vector3(0.08f, 0.12f, 0.015f);
        plate.GetComponent<Renderer>().sharedMaterial = SimpleMat("Interrupteur_Plaque", new Color(0.93f, 0.92f, 0.88f));
        var toggle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        toggle.name = "Bascule";
        Object.DestroyImmediate(toggle.GetComponent<Collider>());
        var pivot = new GameObject("Pivot bascule").transform;
        pivot.SetParent(sw.transform, false);
        pivot.localPosition = new Vector3(0f, 0f, 0.012f);
        toggle.transform.SetParent(pivot, false);
        toggle.transform.localScale = new Vector3(0.03f, 0.05f, 0.015f);
        toggle.GetComponent<Renderer>().sharedMaterial = SimpleMat("Interrupteur_Bascule", new Color(0.85f, 0.84f, 0.8f));
        // Zone qu'on touche avec la main (un peu plus grande que la plaque).
        var zone = plate.GetComponent<BoxCollider>();
        zone.size = new Vector3(1.6f, 1.4f, 6f);
        var inter = sw.AddComponent<XRSimpleInteractable>();
        inter.colliders.Clear();
        inter.colliders.Add(zone);
        var audio = sw.AddComponent<AudioSource>();
        audio.playOnAwake = false; audio.spatialBlend = 1f;

        var ctrl = holder.AddComponent<ClassroomLights>();
        ctrl.lights = lights.ToArray();
        ctrl.panels = panels.ToArray();
        ctrl.switchButton = inter;
        ctrl.switchToggle = pivot;
        ctrl.audioSource = audio;
        ctrl.clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/reel_stop.wav");
        ctrl.on = true;

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[Salle] {n} lampes au plafond avec lumière, interrupteur à côté de la porte.");
    }

    static Material LampMaterial()
    {
        string path = "Assets/SampleJojo/model/Props/clasroom/Lampe.mat";
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        Texture2D T(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(LampTex + n);
        m.SetTexture("_BaseMap", T("albedo.jpg"));
        m.SetColor("_BaseColor", Color.white);
        var normal = T("normal.png");
        if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
        var ao = T("AO.jpg");
        if (ao != null) { m.SetTexture("_OcclusionMap", ao); m.EnableKeyword("_OCCLUSIONMAP"); }
        m.SetFloat("_Metallic", 0.3f);
        m.SetFloat("_Smoothness", 0.45f);
        var em = T("emissive.jpg");
        if (em != null) m.SetTexture("_EmissionMap", em);
        m.SetColor("_EmissionColor", new Color(1f, 0.95f, 0.85f) * 2.2f);
        m.EnableKeyword("_EMISSION");
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material SimpleMat(string name, Color c)
    {
        string path = "Assets/SampleJojo/model/Props/clasroom/" + name + ".mat";
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.4f);
        EditorUtility.SetDirty(m);
        return m;
    }

    // ---------- Tables de l'utilisateur : le plateau (le plus haut et le plus large) de chaque table ----------
    static List<Bounds> UserDeskTops()
    {
        var list = new List<Bounds>();
        var desk = GameObject.Find("Desk");
        if (desk == null) return list;
        foreach (Transform group in desk.transform)
        {
            foreach (Transform t in group)
            {
                if (t.name.StartsWith("Chaise")) continue;
                var rs = t.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) continue;
                // Plateau = pièce plate la plus haute.
                var top = rs.Where(r => r.bounds.size.y < 0.06f && r.bounds.size.x > 0.3f && r.bounds.size.z > 0.3f)
                            .OrderByDescending(r => r.bounds.max.y).FirstOrDefault();
                if (top != null) list.Add(top.bounds);
            }
        }
        return list;
    }

    // ---------- Outils ----------
    /// <summary>Sort un objet du .blend posé, le met dans un support propre (pivot au centre) sous <paramref name="parent"/>.</summary>
    static GameObject Take(GameObject src, string name, GameObject parent, string newName)
    {
        var t = src.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == name);
        if (t == null) { Debug.LogWarning("[Salle] Objet « " + name + " » absent du .blend."); return null; }
        var holder = new GameObject(newName);
        var rs = t.GetComponentsInChildren<Renderer>(true);
        Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(t.position, Vector3.zero);
        foreach (var r in rs) b.Encapsulate(r.bounds);
        holder.transform.position = b.center;
        t.SetParent(holder.transform, true);
        if (parent != null) holder.transform.SetParent(parent.transform, true);
        foreach (var r in rs) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return holder;
    }

    static void Yaw(GameObject go, float deg) => go.transform.Rotate(Vector3.up, deg, Space.World);
    static void ScaleTo(GameObject go, float factor) => go.transform.localScale *= factor;

    static void StretchWorldZ(GameObject go, float factor)
    {
        // Le support n'a qu'une rotation autour de Y : on trouve son axe local aligné sur Z monde.
        Vector3 ls = go.transform.localScale;
        Vector3 zLocal = go.transform.InverseTransformDirection(Vector3.forward);
        int ax = Mathf.Abs(zLocal.x) > Mathf.Abs(zLocal.z) ? 0 : 2;
        ls[ax] *= factor;
        go.transform.localScale = ls;
    }

    static void Move(GameObject go, Vector3 delta) => go.transform.position += delta;

    static void Hide(params string[] names)
    {
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!names.Contains(r.name)) continue;
            if (r.bounds.min.y > 5f) continue;                       // seulement la salle du bas
            Undo.RecordObject(r, "Cacher");
            r.enabled = false;
            foreach (var c in r.GetComponents<Collider>()) { Undo.RecordObject(c, "Cacher"); c.enabled = false; }
        }
    }

    static Bounds B(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Vector3 Size(GameObject go) => B(go).size;
}
