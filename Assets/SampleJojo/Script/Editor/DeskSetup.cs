using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Remplace la table du jeu (Table.035) par le bureau donné (andys_desk.glb) et ajoute la chaise (Chair.glb).
/// Le livre (et l'emplacement de la machine), le sac, la calculatrice et le crayon sont replacés sur le nouveau bureau.
/// Menu : Tools > Jackpot > 16 - Nouveau bureau + chaise.
/// </summary>
public static class DeskSetup
{
    const string DeskGlb = "Assets/SampleJojo/model/Props/Bureau/Bureau.gltf";
    const string ChairGlb = "Assets/SampleJojo/model/Props/Chaise/Chair.glb";
    const string OldTable = "Table.035";
    const string ChairName = "Chaise du jeu";
    const float DeskHeight = 0.76f;     // hauteur d'un vrai bureau
    const float ChairHeight = 0.92f;    // dossier compris

    [MenuItem("Tools/Jackpot/16 - Nouveau bureau + chaise", false, 127)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Bureau] Quitte le mode Play avant."); return; }
        var deskPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeskGlb);
        var chairPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ChairGlb);
        if (deskPrefab == null) { Debug.LogError("[Bureau] Bureau introuvable ou pas importé (paquet glTFast ?) : " + DeskGlb); return; }

        // 1) L'ancienne table : on mémorise sa place et on la cache.
        Bounds ob = default; bool found = false;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r.name != OldTable) continue;
            if (!found) { ob = r.bounds; found = true; } else ob.Encapsulate(r.bounds);
            if (r.enabled) { Undo.RecordObject(r, "Cacher la table"); r.enabled = false; }
            foreach (var c in r.GetComponents<Collider>()) { Undo.RecordObject(c, "Cacher la table"); c.enabled = false; }
        }
        var oldDesk = GameObject.Find(PropsSetup.DeskName);
        if (!found && oldDesk != null) { ob = PropsSetup.WorldBounds(oldDesk); found = true; }
        if (!found) { Debug.LogError("[Bureau] Table « " + OldTable + " » introuvable."); return; }
        float floorY = ob.min.y;

        if (oldDesk != null) Undo.DestroyObjectImmediate(oldDesk);
        bool keepChair = GameObject.Find(ChairName) != null;     // chaise déjà placée : on n'y touche pas

        // Côté joueur = côté d'où on lit le livre.
        var spot = GameObject.Find("Emplacement Jackpot (livre)");
        Vector3 toPlayer = spot != null ? Flat(spot.transform.forward) : Vector3.back;
        Vector3 back = -toPlayer;
        Vector3 right = Vector3.Cross(Vector3.up, back).normalized;

        // 2) Le bureau : tiroirs (face avant du modèle, -Z) tournés vers le joueur.
        var desk = (GameObject)PrefabUtility.InstantiatePrefab(deskPrefab);
        Undo.RegisterCreatedObjectUndo(desk, "Bureau");
        desk.name = PropsSetup.DeskName;
        desk.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        desk.transform.localScale = Vector3.one;
        desk.transform.rotation = UprightFacing(desk, toPlayer, out string how);
        Bounds db = PropsSetup.WorldBounds(desk);
        if (db.size.y > 1e-4f) desk.transform.localScale *= DeskHeight / db.size.y;
        db = PropsSetup.WorldBounds(desk);
        desk.transform.position += new Vector3(ob.center.x - db.center.x, floorY - db.min.y, ob.center.z - db.center.z);
        db = PropsSetup.WorldBounds(desk);
        foreach (var r in desk.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        float topY = db.max.y;
        float halfW = Mathf.Abs(Vector3.Dot(db.extents, Abs(right)));
        float halfD = Mathf.Abs(Vector3.Dot(db.extents, Abs(back)));

        // 3) Le livre + l'emplacement de la machine : au milieu du plateau.
        if (spot != null)
        {
            Undo.RecordObject(spot.transform, "Livre");
            spot.transform.position = new Vector3(db.center.x, topY + 0.002f, db.center.z);
        }

        // 4) Le sac : par terre, à droite du bureau (vu du joueur).
        var bag = Object.FindFirstObjectByType<JackpotBag>();
        if (bag != null)
        {
            Undo.RecordObject(bag.transform, "Sac");
            bag.transform.SetPositionAndRotation(
                new Vector3(db.center.x, floorY, db.center.z) + right * (halfW + 0.3f) + toPlayer * (halfD * 0.4f),
                Quaternion.LookRotation(toPlayer, Vector3.up));
        }

        // 5) La chaise, côté joueur, tournée vers le bureau.
        string chairInfo = keepChair ? "chaise existante gardée" : "pas de chaise";
        if (chairPrefab != null && !keepChair)
        {
            var chair = (GameObject)PrefabUtility.InstantiatePrefab(chairPrefab);
            Undo.RegisterCreatedObjectUndo(chair, "Chaise");
            chair.name = ChairName;
            chair.transform.SetPositionAndRotation(Vector3.zero, Quaternion.LookRotation(back, Vector3.up));
            chair.transform.localScale = Vector3.one;
            Bounds cb = PropsSetup.WorldBounds(chair);
            if (cb.size.y > 1e-4f) chair.transform.localScale *= ChairHeight / cb.size.y;
            cb = PropsSetup.WorldBounds(chair);
            float chairHalfD = Mathf.Abs(Vector3.Dot(cb.extents, Abs(back)));
            Vector3 target = new Vector3(db.center.x, floorY, db.center.z) + toPlayer * (halfD + chairHalfD * 0.6f);
            chair.transform.position += new Vector3(target.x - cb.center.x, floorY - cb.min.y, target.z - cb.center.z);
            chairInfo = "chaise de " + PropsSetup.WorldBounds(chair).size.ToString("F2");
        }

        // 6) Calculatrice, crayon et plateau sur le nouveau bureau.
        PropsSetup.Setup();

        EditorSceneManager.MarkSceneDirty(desk.scene);
        Debug.Log($"[Bureau] OK ({how}) : bureau {db.size:F2} (plateau à {topY - floorY:F2} m du sol), {chairInfo}. L'ancienne table « {OldTable} » est cachée.");
    }

    /// <summary>
    /// Le modèle est livré couché : on redresse le bureau pour que le plateau (la plus grande pièce plate)
    /// soit horizontal en haut, puis on tourne les poignées des tiroirs (les plus petites pièces) vers le joueur.
    /// </summary>
    static Quaternion UprightFacing(GameObject desk, Vector3 toPlayer, out string how)
    {
        var rs = desk.GetComponentsInChildren<Renderer>();
        Bounds all = PropsSetup.WorldBounds(desk);
        Renderer top = null; float bestArea = 0f; int thinAxis = 1;
        foreach (var r in rs)
        {
            Vector3 s = r.bounds.size;
            int a = s.x <= s.y && s.x <= s.z ? 0 : s.y <= s.z ? 1 : 2;
            float area = 1f; float maxSide = 0f;
            for (int k = 0; k < 3; k++) if (k != a) { area *= s[k]; maxSide = Mathf.Max(maxSide, s[k]); }
            if (s[a] > 0.2f * maxSide) continue;          // pas plat
            if (area > bestArea) { bestArea = area; top = r; thinAxis = a; }
        }
        Quaternion q = Quaternion.identity;
        how = "orientation d'origine";
        if (top != null)
        {
            Vector3 axis = thinAxis == 0 ? Vector3.right : thinAxis == 1 ? Vector3.up : Vector3.forward;
            float sign = Mathf.Sign(top.bounds.center[thinAxis] - all.center[thinAxis]);
            if (sign == 0f) sign = 1f;
            q = Quaternion.FromToRotation(axis * sign, Vector3.up);
            how = "plateau = " + top.name;
        }
        desk.transform.rotation = q;

        // Poignées : les 3 pièces avec le moins de sommets.
        var mfs = desk.GetComponentsInChildren<MeshFilter>();
        System.Array.Sort(mfs, (a, b) => a.sharedMesh.vertexCount.CompareTo(b.sharedMesh.vertexCount));
        all = PropsSetup.WorldBounds(desk);
        Vector3 knobs = Vector3.zero; int n = 0;
        for (int i = 0; i < mfs.Length && n < 3; i++)
        {
            var r = mfs[i].GetComponent<Renderer>();
            if (r == null) continue;
            knobs += r.bounds.center; n++;
        }
        if (n > 0)
        {
            Vector3 knobDir = Flat(knobs / n - all.center);
            // La face avant est perpendiculaire au grand côté du plateau : on prend l'axe court (X ou Z) du bureau,
            // dans le sens des poignées, pour que le bureau reste bien droit.
            Vector3 front = all.size.x < all.size.z ? Vector3.right : Vector3.forward;
            if (Vector3.Dot(front, knobDir) < 0f) front = -front;
            float yaw = Vector3.SignedAngle(front, toPlayer, Vector3.up);
            q = Quaternion.AngleAxis(yaw, Vector3.up) * desk.transform.rotation;
            how += ", tiroirs vers le joueur";
        }
        return q;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.forward; }
    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
}
