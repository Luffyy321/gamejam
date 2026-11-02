using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Bureau du prof devant le tableau de la classe du bas, ventilateur posé dessus (pales qui tournent)
/// et le fauteuil déjà présent dans la scène (« Untitled ») placé derrière le bureau, tourné vers la classe.
/// Menu : Tools > Jackpot > 17 - Bureau du prof + ventilo + fauteuil.
/// </summary>
public static class ProfDeskSetup
{
    const string DeskObj = "Assets/SampleJojo/model/Props/BureauProf/Desk.obj";
    const string FanFbx = "Assets/SampleJojo/model/Props/Ventilo/Ventilateur.fbx";
    const string RootName = "Bureau du prof";
    const string SeatName = "Untitled";              // le fauteuil déjà posé dans la scène

    // Classe du bas : tableau sur le mur x = 1.95, centre de la salle en z ≈ 4.3.
    const float BoardWallX = 1.95f;
    const float DeskCenterZ = 4.6f;
    const float DeskHeight = 0.78f;
    const float FanHeight = 0.38f;

    static readonly Vector3 ToClass = Vector3.left;   // le prof regarde la classe (vers -X)

    [MenuItem("Tools/Jackpot/17 - Bureau du prof + ventilo + fauteuil", false, 128)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[BureauProf] Quitte le mode Play avant."); return; }
        var deskPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DeskObj);
        if (deskPrefab == null) { Debug.LogError("[BureauProf] Bureau introuvable : " + DeskObj); return; }

        var prof = GameObject.Find("Prof");
        var bag = Object.FindFirstObjectByType<JackpotBag>();
        float floorY = prof != null ? prof.transform.position.y : bag != null ? bag.transform.position.y : 1.52f;

        var old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, RootName);

        // ---- Le fauteuil (déjà dans la scène) : on mesure sa profondeur pour laisser la place.
        var seat = FindRootSeat();
        float seatDepth = 0.9f;
        if (seat != null)
        {
            Undo.RecordObject(seat.transform, "Fauteuil");
            FaceDirection(seat, ToClass);
            seatDepth = Mathf.Abs(Vector3.Dot(Bounds(seat).size, Abs(ToClass)));
        }

        // ---- Le bureau : côté tiroirs (+Z du modèle) vers le prof, donc vers le tableau.
        var desk = (GameObject)PrefabUtility.InstantiatePrefab(deskPrefab);
        desk.name = "Bureau";
        desk.transform.SetParent(root.transform, false);
        desk.transform.rotation = Quaternion.LookRotation(-ToClass, Vector3.up);
        Bounds db = Bounds(desk);
        if (db.size.y > 1e-4f) desk.transform.localScale *= DeskHeight / db.size.y;
        db = Bounds(desk);
        float deskMaxX = BoardWallX - seatDepth - 0.1f;
        desk.transform.position += new Vector3(deskMaxX - db.max.x, floorY - db.min.y, DeskCenterZ - db.center.z);
        db = Bounds(desk);
        foreach (var c in desk.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
        var box = root.AddComponent<BoxCollider>();
        box.center = root.transform.InverseTransformPoint(new Vector3(db.center.x, db.min.y + db.size.y * 0.5f, db.center.z));
        box.size = new Vector3(db.size.x, db.size.y, db.size.z);
        float topY = db.max.y;

        // ---- Le fauteuil derrière le bureau, entre le bureau et le tableau.
        if (seat != null)
        {
            Bounds sb = Bounds(seat);
            Vector3 target = new Vector3(db.max.x + 0.05f + sb.extents.x, floorY, DeskCenterZ);
            seat.transform.position += new Vector3(target.x - sb.center.x, floorY - sb.min.y, target.z - sb.center.z);
        }

        // ---- Le ventilateur, au bout du bureau, tourné vers le prof.
        string fanInfo = "pas de ventilateur";
        var fanPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FanFbx);
        if (fanPrefab != null)
        {
            var fan = (GameObject)PrefabUtility.InstantiatePrefab(fanPrefab);
            PrefabUtility.UnpackPrefabInstance(fan, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            fan.name = "Ventilateur";
            fan.transform.SetParent(root.transform, false);
            fan.transform.rotation = Quaternion.identity;
            Bounds fb0 = Bounds(fan);
            if (fb0.size.y > 1e-4f) fan.transform.localScale *= FanHeight / fb0.size.y;
            FaceFan(fan, -ToClass);
            Bounds fb = Bounds(fan);
            Vector3 target = new Vector3(db.center.x, topY, db.min.z + fb.extents.z + 0.1f);
            fan.transform.position += new Vector3(target.x - fb.center.x, topY - fb.min.y + 0.001f, target.z - fb.center.z);
            fanInfo = AddSpinner(fan);
        }

        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[BureauProf] OK : bureau {db.size:F2} à x={db.center.x:F2} z={db.center.z:F2}, plateau à {topY - floorY:F2} m ; " +
                  (seat != null ? "fauteuil placé derrière" : "fauteuil « Untitled » introuvable") + " ; " + fanInfo);
    }

    // ---------- Fauteuil ----------
    static GameObject FindRootSeat()
    {
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == SeatName && go.GetComponentInChildren<Renderer>() != null) return go;
        return null;
    }

    /// <summary>Tourne un siège pour que son assise regarde <paramref name="dir"/> (le dossier = partie la plus haute).</summary>
    static void FaceDirection(GameObject seat, Vector3 dir)
    {
        Vector3 front = SeatFront(seat);
        float yaw = Vector3.SignedAngle(front, dir, Vector3.up);
        Bounds b = Bounds(seat);
        seat.transform.RotateAround(b.center, Vector3.up, yaw);
    }

    static Vector3 SeatFront(GameObject seat)
    {
        var pts = WorldVertices(seat);
        if (pts.Count == 0) return seat.transform.forward;
        float minY = pts.Min(p => p.y), maxY = pts.Max(p => p.y);
        Vector3 center = Vector3.zero; foreach (var p in pts) center += p; center /= pts.Count;
        var high = pts.Where(p => p.y > minY + 0.8f * (maxY - minY)).ToList();
        Vector3 hc = Vector3.zero; foreach (var p in high) hc += p; hc /= Mathf.Max(1, high.Count);
        Vector3 back = hc - center; back.y = 0f;
        return back.sqrMagnitude > 1e-6f ? -back.normalized : seat.transform.forward;
    }

    // ---------- Ventilateur ----------
    static List<Transform> Blades(GameObject fan) =>
        fan.GetComponentsInChildren<Transform>(true).Where(t => t.name.ToLowerInvariant().Contains("blade") && t.GetComponent<Renderer>() != null).ToList();

    /// <summary>Le devant du ventilateur = du pied vers les pales.</summary>
    static void FaceFan(GameObject fan, Vector3 dir)
    {
        var blades = Blades(fan);
        var bases = fan.GetComponentsInChildren<Renderer>(true).Where(r => r.name.ToLowerInvariant().Contains("base")).ToList();
        if (blades.Count == 0 || bases.Count == 0) { fan.transform.rotation = Quaternion.LookRotation(dir, Vector3.up); return; }
        Vector3 bc = Center(blades.Select(t => t.GetComponent<Renderer>()));
        Vector3 basec = Center(bases);
        Vector3 front = bc - basec; front.y = 0f;
        if (front.sqrMagnitude < 1e-8f) return;
        float yaw = Vector3.SignedAngle(front.normalized, dir, Vector3.up);
        fan.transform.RotateAround(Bounds(fan).center, Vector3.up, yaw);
    }

    static string AddSpinner(GameObject fan)
    {
        var blades = Blades(fan);
        if (blades.Count == 0) return "ventilateur posé (pales non trouvées)";
        var rs = blades.Select(t => t.GetComponent<Renderer>()).ToList();
        Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
        var pivot = new GameObject("Pales (rotation)").transform;
        pivot.SetParent(blades[0].parent, true);
        pivot.position = b.center;
        // Axe = direction la plus fine du disque des pales.
        Vector3 s = b.size;
        Vector3 axisW = s.x <= s.y && s.x <= s.z ? Vector3.right : s.y <= s.z ? Vector3.up : Vector3.forward;
        pivot.rotation = Quaternion.LookRotation(axisW, Mathf.Abs(Vector3.Dot(axisW, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up);
        foreach (var t in blades) t.SetParent(pivot, true);
        var spin = pivot.gameObject.AddComponent<FanSpin>();
        spin.localAxis = Vector3.forward;
        return "ventilateur posé, " + blades.Count + " pales qui tournent";
    }

    // ---------- Outils ----------
    static List<Vector3> WorldVertices(GameObject go)
    {
        var list = new List<Vector3>();
        foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.sharedMesh == null) continue;
            var v = mf.sharedMesh.vertices;
            int step = Mathf.Max(1, v.Length / 4000);
            for (int i = 0; i < v.Length; i += step) list.Add(mf.transform.TransformPoint(v[i]));
        }
        return list;
    }

    static Vector3 Center(IEnumerable<Renderer> rs)
    {
        Vector3 c = Vector3.zero; int n = 0;
        foreach (var r in rs) { c += r.bounds.center; n++; }
        return n > 0 ? c / n : Vector3.zero;
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
}
