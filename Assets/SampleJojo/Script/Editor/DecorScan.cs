using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class DecorScan
{
    public const string Dir = "Assets/SampleJojo/model/Props/clasroom/decor/Modeles/";
    public static readonly string[] Models =
    {
        "Affiche_ABC/Affiche_ABC.fbx", "Affiche_Calendrier/Affiche_Calendrier.fbx", "Affiche_Colombe/Affiche_Colombe.fbx",
        "Affiche_Katakana/Affiche_Katakana.blend", "Agrafeuse/Agrafeuse.fbx", "Avion_papier/Avion_papier.fbx",
        "Extincteur/Extincteur.obj", "Lunettes/Lunettes.fbx", "Mug/Mug.fbx", "Ordinateur/Ordinateur.fbx",
        "Papiers/Papiers.obj", "Poubelle/Poubelle.fbx",
    };

    [MenuItem("Tools/Boutique/Scan décor", false, 40)]
    static void Scan()
    {
        var sb = new StringBuilder();
        foreach (var m in Models)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + m);
            if (asset == null) { sb.AppendLine("!! introuvable " + m); continue; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.transform.position = new Vector3(100, 100, 100);
            var rs = go.GetComponentsInChildren<Renderer>();
            Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds();
            foreach (var r in rs) b.Encapsulate(r.bounds);
            int tris = 0; Vector3 nsum = Vector3.zero;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh; if (mesh == null) continue;
                var v = mesh.vertices; var t = mesh.triangles; tris += t.Length / 3;
                for (int i = 0; i < t.Length; i += 3)
                {
                    Vector3 a = mf.transform.TransformPoint(v[t[i]]), c1 = mf.transform.TransformPoint(v[t[i + 1]]), c2 = mf.transform.TransformPoint(v[t[i + 2]]);
                    nsum += Vector3.Cross(c1 - a, c2 - a);
                }
            }
            sb.AppendLine($"== {m} taille={b.size:F3} centre-pos={(b.center - go.transform.position):F3} tris={tris} normale={nsum.normalized:F2} ({nsum.magnitude:F4})");
            foreach (var r in rs)
                sb.AppendLine($"   {r.name} [{r.GetType().Name}] taille={r.bounds.size:F3} mats={string.Join(",", r.sharedMaterials.Select(x => x == null ? "null" : x.name + "/" + x.shader.name + (x.mainTexture ? "/tex" : "")))}");
            Object.DestroyImmediate(go);
        }

        // ---- La salle ----
        var desk = GameObject.Find("Bureau du prof");
        if (desk != null)
        {
            foreach (Transform c in desk.transform)
            {
                var rr = c.GetComponentsInChildren<Renderer>();
                if (rr.Length == 0) continue;
                Bounds bb = rr[0].bounds; foreach (var r in rr) bb.Encapsulate(r.bounds);
                sb.AppendLine($"[bureau] {c.name} min={bb.min:F2} max={bb.max:F2}");
            }
        }
        foreach (var n in new[] { "Prof", "Chaise du jeu", "Sac Jackpot", "Jackpot" })
        {
            var g = GameObject.Find(n); if (g != null) sb.AppendLine($"[obj] {n} pos={g.transform.position:F2}");
        }
        Vector3 c0 = new Vector3(-3f, 2.6f, 4.3f);
        foreach (var d in new[] { Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
            foreach (float off in new[] { -3f, -1.5f, 0f, 1.5f, 3f })
            {
                Vector3 side = Vector3.Cross(Vector3.up, d) * off;
                foreach (float h in new[] { 2.4f, 3.1f })
                {
                    Vector3 o = new Vector3(c0.x, h, c0.z) + side;
                    if (Physics.Raycast(o, d, out var hit, 15f))
                        sb.AppendLine($"[mur] dir={d} depuis={o:F2} touche={hit.point:F2} n={hit.normal:F2} {hit.collider.name}");
                    else sb.AppendLine($"[mur] dir={d} depuis={o:F2} rien");
                }
            }
        for (float x = -7f; x <= 1.5f; x += 1.5f)
            for (float z = 1f; z <= 7.6f; z += 1.6f)
            {
                var o = new Vector3(x, 3.3f, z);
                if (Physics.Raycast(o, Vector3.down, out var hit, 5f))
                    sb.AppendLine($"[sol] ({x:F1},{z:F1}) y={hit.point.y:F2} {hit.collider.name}");
            }
        File.WriteAllText("Assets/SampleJojo/Script/Editor/decor_scan.txt", sb.ToString());
        Debug.Log("[Décor] scan écrit");
    }
}
