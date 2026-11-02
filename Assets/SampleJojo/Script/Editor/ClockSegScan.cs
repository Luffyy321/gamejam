using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Liste les segments (morceaux séparés) de l'écran de l'horloge digitale.</summary>
public static class ClockSegScan
{
    const string Path = "Assets/SampleJojo/model/Props/clasroom/Digital_Clock.blend";

    [MenuItem("Tools/Jackpot/Rapport segments horloge", false, 305)]
    static void Scan()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        var body = prefab.GetComponentsInChildren<MeshFilter>(true).First(f => f.name == "Digital.Clock");
        var mesh = body.sharedMesh;
        var mats = body.GetComponent<MeshRenderer>().sharedMaterials;
        var v = mesh.vertices;
        var sb = new StringBuilder();
        sb.AppendLine($"verts={v.Length} submeshes={mesh.subMeshCount} bounds={mesh.bounds}");
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            var idx = mesh.GetIndices(s);
            var comps = Components(idx, v);
            sb.AppendLine($"--- sub {s} mat={(s < mats.Length && mats[s] ? mats[s].name : "?")} tris={idx.Length / 3} comps={comps.Count}");
            if (comps.Count > 80) continue;
            foreach (var c in comps.OrderBy(c => c.center.x).ThenBy(c => c.center.y))
                sb.AppendLine($"  c={c.center:F3} s={c.size:F3}");
        }
        File.WriteAllText("Assets/SampleJojo/Script/Editor/clock_seg_scan.txt", sb.ToString());
        Debug.Log("[Horloge] rapport segments écrit");
    }

    public static List<Bounds> Components(int[] idx, Vector3[] v) => ComponentTris(idx, v).Select(t =>
    {
        var b = new Bounds(v[idx[t[0] * 3]], Vector3.zero);
        foreach (var tri in t) for (int k = 0; k < 3; k++) b.Encapsulate(v[idx[tri * 3 + k]]);
        return b;
    }).ToList();

    /// <summary>Regroupe les triangles qui se touchent (sommets à la même position).</summary>
    public static List<List<int>> ComponentTris(int[] idx, Vector3[] v)
    {
        int nt = idx.Length / 3;
        var key = new Dictionary<Vector3Int, int>();
        int[] vid = new int[idx.Length];
        for (int i = 0; i < idx.Length; i++)
        {
            var p = v[idx[i]];
            var k = new Vector3Int(Mathf.RoundToInt(p.x * 10000), Mathf.RoundToInt(p.y * 10000), Mathf.RoundToInt(p.z * 10000));
            if (!key.TryGetValue(k, out int id)) { id = key.Count; key[k] = id; }
            vid[i] = id;
        }
        int[] parent = Enumerable.Range(0, key.Count).ToArray();
        int Find(int a) { while (parent[a] != a) a = parent[a] = parent[parent[a]]; return a; }
        for (int t = 0; t < nt; t++)
        {
            int a = Find(vid[t * 3]);
            for (int k = 1; k < 3; k++) { int b = Find(vid[t * 3 + k]); if (a != b) parent[b] = a; }
        }
        var groups = new Dictionary<int, List<int>>();
        for (int t = 0; t < nt; t++)
        {
            int r = Find(vid[t * 3]);
            if (!groups.TryGetValue(r, out var l)) groups[r] = l = new List<int>();
            l.Add(t);
        }
        return groups.Values.ToList();
    }
}
