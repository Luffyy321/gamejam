using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Relevé des nouveaux modèles (roulette, lunettes, café) pour les préparer.</summary>
public static class NewModelsScan
{
    const string Dir = "Assets/SampleJojo/model/Props/boutique/";
    static readonly string[] Paths = { "Roulette/rulet_8.blend", "Lunettes3D/3dGlass.obj", "Doflamingo/DondlamingoGlasses.blend", "Cafe/coffeecupp.fbx" };

    [MenuItem("Tools/Jeu/1 - Scan des nouveaux modeles", false, 1)]
    static void Scan()
    {
        var sb = new StringBuilder();
        foreach (var p in Paths)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(Dir + p);
            sb.AppendLine("==== " + p + (go == null ? " : INTROUVABLE" : ""));
            if (go == null) continue;
            var inst = (GameObject)Object.Instantiate(go);
            inst.transform.position = new Vector3(0f, -200f, 0f);
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                string path = t == inst.transform ? t.name : t.name;
                int depth = 0; for (var x = t; x != inst.transform; x = x.parent) depth++;
                var comps = string.Join(",", t.GetComponents<Component>().Where(c => !(c is Transform)).Select(c => c.GetType().Name));
                sb.Append(new string(' ', depth * 2)).Append(t.name).Append($" lp={t.localPosition:F3} lr={t.localEulerAngles:F0} ls={t.localScale:F3} [{comps}]");
                var r = t.GetComponent<Renderer>();
                if (r != null)
                {
                    sb.Append($" bounds c={(r.bounds.center - inst.transform.position):F3} s={r.bounds.size:F3}");
                    var mf = t.GetComponent<MeshFilter>();
                    if (mf != null && mf.sharedMesh != null) sb.Append($" verts={mf.sharedMesh.vertexCount} sub={mf.sharedMesh.subMeshCount}");
                    sb.Append(" mats=" + string.Join(" | ", r.sharedMaterials.Select(m => m == null ? "null" : $"{m.name}({(m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor").ToString("F2") : m.HasProperty("_Color") ? m.GetColor("_Color").ToString("F2") : "?")} tex={(m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null ? m.GetTexture("_BaseMap").name : m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null ? m.GetTexture("_MainTex").name : "-")} sh={m.shader.name})")));
                }
                sb.AppendLine();
            }
            // Roulette : où sont les cases rouges / noires / vertes (angle autour du centre).
            foreach (var mf in inst.GetComponentsInChildren<MeshFilter>())
            {
                var mesh = mf.sharedMesh; var r = mf.GetComponent<Renderer>();
                if (mesh == null || r == null) continue;
                for (int s = 0; s < mesh.subMeshCount && s < r.sharedMaterials.Length; s++)
                {
                    var m = r.sharedMaterials[s]; if (m == null) continue;
                    string n = m.name.ToLower();
                    if (!(n.Contains("red") || n.Contains("black") || n.Contains("green"))) continue;
                    var tris = mesh.GetTriangles(s); var v = mesh.vertices;
                    var b = new Bounds(v[tris[0]], Vector3.zero);
                    foreach (var i in tris) b.Encapsulate(v[i]);
                    sb.AppendLine($"  [{mf.name}] sous-maillage {s} {m.name} : {tris.Length / 3} triangles, bornes locales {b.center:F3} {b.size:F3}");
                }
            }
            Object.DestroyImmediate(inst);
        }
        System.IO.Directory.CreateDirectory("Assets/SampleJojo/Preview~/");
        System.IO.File.WriteAllText("Assets/SampleJojo/Preview~/nouveaux_modeles.txt", sb.ToString());
        Debug.Log("[Jeu] Scan des nouveaux modèles écrit (Preview~/nouveaux_modeles.txt).");
    }
}
