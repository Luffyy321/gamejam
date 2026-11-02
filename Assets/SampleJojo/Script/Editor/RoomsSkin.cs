using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu Jeu > 4 : les salles des cours 2 et 3 prennent la texture des murs et du sol de la classe.
/// On pose un « habillage » fin contre les murs et sur le sol de chaque salle :
/// les fenêtres, les portes, le tableau et les poteaux restent visibles (repérés au rayon).
/// </summary>
public static class RoomsSkin
{
    const float Cell = 0.1f;
    static readonly string[] OpeningNames = { "plane", "venetian", "tableau", "porte", "fenetre", "fenêtre", "window", "vitre", "cylinder", "circle", "door", "table", "chaise" };

    [MenuItem("Tools/Jeu/4 - Murs et sol des salles 2 et 3 (textures de la classe)", false, 4)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Salles] Quitte le mode Play avant."); return; }
        var cr = Object.FindFirstObjectByType<ClassroomRooms>(FindObjectsInactive.Include);
        if (cr == null) { Debug.LogError("[Salles] « Salles des cours » introuvable."); return; }
        if (cr.editPreview != 0) { Debug.LogWarning("[Salles] Reviens d'abord à la classe (Tools > Classe > 27)."); return; }
        var salle = GameObject.Find("Salle");
        var sol = salle != null ? salle.transform.Find("Sol") : null;
        var murs = salle != null ? salle.transform.Find("Murs") : null;
        if (sol == null || murs == null) { Debug.LogError("[Salles] Salle/Sol ou Salle/Murs introuvable."); return; }
        var floorR = sol.GetComponent<Renderer>();
        var wallR = murs.GetComponentsInChildren<Renderer>().OrderByDescending(r => r.bounds.size.x * r.bounds.size.y + r.bounds.size.z * r.bounds.size.y).FirstOrDefault();
        if (floorR == null || wallR == null) { Debug.LogError("[Salles] matériaux de la classe introuvables."); return; }
        Material floorMat = floorR.sharedMaterial, wallMat = wallR.sharedMaterial;
        Vector2 floorUV = MetersPerUV(floorR.GetComponent<MeshFilter>()), wallUV = MetersPerUV(wallR.GetComponent<MeshFilter>());

        var archi = GameObject.Find("Archi_MMI");
        var log = new List<string>();
        foreach (var room in cr.rooms)
        {
            if (room == null) continue;
            string name = "Habillage " + room.name;
            var old = cr.transform.Find(name);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var skin = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(skin, "Habillage");
            skin.transform.SetParent(cr.transform, false);
            skin.transform.position = Vector3.zero; skin.transform.rotation = Quaternion.identity;

            // Colliders provisoires sur le bâtiment autour de la salle (il n'en a pas).
            var zone = room.interior; zone.Expand(new Vector3(3f, 1f, 3f));
            var temp = new List<MeshCollider>();
            if (archi != null)
                foreach (var mf in archi.GetComponentsInChildren<MeshFilter>(true))
                {
                    var r = mf.GetComponent<Renderer>();
                    if (r == null || mf.sharedMesh == null || !zone.Intersects(r.bounds)) continue;
                    if (mf.GetComponent<Collider>() != null) continue;
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    temp.Add(mc);
                }
            Physics.SyncTransforms();
            var tempSet = new HashSet<Collider>(temp);

            int cells = 0;
            var b = room.interior;
            // Sol.
            Quad(skin.transform, "Sol", new[] {
                new Vector3(b.min.x, b.min.y + 0.004f, b.min.z), new Vector3(b.min.x, b.min.y + 0.004f, b.max.z),
                new Vector3(b.max.x, b.min.y + 0.004f, b.max.z), new Vector3(b.max.x, b.min.y + 0.004f, b.min.z) },
                v => new Vector2(v.x / floorUV.x, v.z / floorUV.y), floorMat);
            // 4 murs.
            var walls = new (string n, Vector3 normal)[] { ("Mur -X", Vector3.left), ("Mur +X", Vector3.right), ("Mur -Z", Vector3.back), ("Mur +Z", Vector3.forward) };
            foreach (var (wn, n) in walls)
                cells += Wall(skin.transform, wn, b, n, tempSet, wallMat, wallUV);

            foreach (var mc in temp) Object.DestroyImmediate(mc);
            foreach (var t in skin.GetComponentsInChildren<Transform>()) GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
            log.Add($"{room.name} : {cells} carreaux de mur");
        }
        EditorSceneManager.MarkSceneDirty(cr.gameObject.scene);
        Debug.Log("[Salles] Habillage posé (textures de la classe) : " + string.Join(" ; ", log) + ".");
    }

    static int Wall(Transform parent, string name, Bounds b, Vector3 n, HashSet<Collider> temp, Material mat, Vector2 uvScale)
    {
        // Repère du mur : t le long du mur, y en hauteur ; le plan intérieur est sur la face de la boîte.
        Vector3 along = Mathf.Abs(n.x) > 0.5f ? Vector3.forward : Vector3.right;
        float t0 = Vector3.Dot(b.min, along), t1 = Vector3.Dot(b.max, along);
        float y0 = b.min.y, y1 = b.max.y;
        Vector3 face = b.center + Vector3.Scale(b.extents, n);
        int nt = Mathf.CeilToInt((t1 - t0) / Cell), ny = Mathf.CeilToInt((y1 - y0) / Cell);
        var dist = new float[nt, ny]; var solid = new bool[nt, ny];
        var wallHits = new List<float>();
        for (int i = 0; i < nt; i++)
            for (int j = 0; j < ny; j++)
            {
                float t = Mathf.Min(t0 + (i + 0.5f) * Cell, t1), y = Mathf.Min(y0 + (j + 0.5f) * Cell, y1);
                Vector3 p = face + along * (t - Vector3.Dot(face, along)) + Vector3.up * (y - face.y) - n * 0.35f;
                dist[i, j] = -1f;
                float best = float.MaxValue; Collider hitC = null;
                foreach (var h in Physics.RaycastAll(p, n, 0.9f, ~0, QueryTriggerInteraction.Ignore))
                    if (temp.Contains(h.collider) && h.distance < best) { best = h.distance; hitC = h.collider; }
                if (hitC == null || IsOpening(hitC)) continue;
                dist[i, j] = best;
                wallHits.Add(best);
            }
        if (wallHits.Count == 0) return 0;
        wallHits.Sort();
        float med = wallHits[wallHits.Count / 2];
        for (int i = 0; i < nt; i++) for (int j = 0; j < ny; j++) solid[i, j] = dist[i, j] >= 0f && Mathf.Abs(dist[i, j] - med) < 0.08f;

        // Bandes horizontales de carreaux pleins → un maillage.
        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
        Vector3 plane = face + n * (med - 0.35f - 0.004f);
        int count = 0;
        for (int j = 0; j < ny; j++)
            for (int i = 0; i < nt; i++)
            {
                if (!solid[i, j]) continue;
                int k = i; while (k + 1 < nt && solid[k + 1, j]) k++;
                float ta = t0 + i * Cell, tb = Mathf.Min(t0 + (k + 1) * Cell, t1);
                float ya = y0 + j * Cell, yb = Mathf.Min(y0 + (j + 1) * Cell, y1);
                Vector3 Pt(float tt, float yy) => plane + along * (tt - Vector3.Dot(plane, along)) + Vector3.up * (yy - plane.y);
                int v0 = verts.Count;
                verts.Add(Pt(ta, ya)); verts.Add(Pt(ta, yb)); verts.Add(Pt(tb, yb)); verts.Add(Pt(tb, ya));
                foreach (var (ut, uy) in new[] { (ta, ya), (ta, yb), (tb, yb), (tb, ya) }) uvs.Add(new Vector2(ut / uvScale.x, uy / uvScale.y));
                // Face tournée vers l'intérieur de la salle (-n).
                bool flip = Vector3.Dot(Vector3.Cross(verts[v0 + 1] - verts[v0], verts[v0 + 2] - verts[v0]), -n) < 0f;
                if (!flip) tris.AddRange(new[] { v0, v0 + 1, v0 + 2, v0, v0 + 2, v0 + 3 });
                else tris.AddRange(new[] { v0, v0 + 2, v0 + 1, v0, v0 + 3, v0 + 2 });
                count += k - i + 1;
                i = k;
            }
        if (verts.Count == 0) return 0;
        var mesh = new Mesh { name = name };
        mesh.indexFormat = verts.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
        MakeRenderer(parent, name, mesh, mat);
        return count;
    }

    static bool IsOpening(Collider c)
    {
        string n = c.name.ToLower();
        if (OpeningNames.Any(o => n.Contains(o))) return true;
        // Petit pavé de la taille d'une porte : c'est une porte.
        var b = c.bounds;
        float hor = Mathf.Max(b.size.x, b.size.z), thin = Mathf.Min(b.size.x, b.size.z);
        return b.size.y < 2.25f && hor < 1.6f && thin < 0.5f;
    }

    static void Quad(Transform parent, string name, Vector3[] v, System.Func<Vector3, Vector2> uv, Material mat)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(v.ToList());
        mesh.SetUVs(0, v.Select(uv).ToList());
        // Face vers le haut.
        bool up = Vector3.Cross(v[1] - v[0], v[2] - v[0]).y > 0f;
        mesh.SetTriangles(up ? new[] { 0, 1, 2, 0, 2, 3 } : new[] { 0, 2, 1, 0, 3, 2 }, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
        MakeRenderer(parent, name, mesh, mat);
    }

    static void MakeRenderer(Transform parent, string name, Mesh mesh, Material mat)
    {
        // Le maillage est rangé dans un fichier (sinon il serait perdu à la sauvegarde de la scène).
        string dir = "Assets/SampleJojo/model/Habillage salles";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets/SampleJojo/model", "Habillage salles");
        string path = dir + "/" + parent.name + " - " + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) { EditorUtility.CopySerialized(mesh, existing); mesh = existing; }
        else AssetDatabase.CreateAsset(mesh, path);
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>Mètres par unité d'UV (u, v) du maillage de la classe, pour garder la même taille de motif.</summary>
    static Vector2 MetersPerUV(MeshFilter mf)
    {
        if (mf == null || mf.sharedMesh == null) return new Vector2(2f, 2f);
        var m = mf.sharedMesh; var v = m.vertices; var uv = m.uv; var tris = m.triangles;
        if (uv == null || uv.Length != v.Length) return new Vector2(2f, 2f);
        float su = 0f, sv = 0f; int n = 0;
        for (int i = 0; i + 2 < tris.Length && n < 200; i += 3)
        {
            Vector3 p0 = mf.transform.TransformPoint(v[tris[i]]), p1 = mf.transform.TransformPoint(v[tris[i + 1]]), p2 = mf.transform.TransformPoint(v[tris[i + 2]]);
            Vector2 a = uv[tris[i]], b2 = uv[tris[i + 1]], c = uv[tris[i + 2]];
            Vector2 d1 = b2 - a, d2 = c - a;
            float det = d1.x * d2.y - d1.y * d2.x;
            if (Mathf.Abs(det) < 1e-8f) continue;
            Vector3 e1 = p1 - p0, e2 = p2 - p0;
            Vector3 dPdu = (e1 * d2.y - e2 * d1.y) / det, dPdv = (e2 * d1.x - e1 * d2.x) / det;
            su += dPdu.magnitude; sv += dPdv.magnitude; n++;
        }
        if (n == 0) return new Vector2(2f, 2f);
        var r = new Vector2(su / n, sv / n);
        if (r.x < 0.01f || r.x > 500f) r.x = 2f;
        if (r.y < 0.01f || r.y > 500f) r.y = 2f;
        return r;
    }
}
