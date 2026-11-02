using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class CoinScan
{
    static readonly string[] Paths =
    {
        "Assets/SampleJojo/model/Props/Coin/MMIcoins.fbx",
        "Assets/SampleJojo/model/Props/sac/backpack.fbx",
        "Assets/SampleJojo/model/Props/Post-it/PostIt.fbx",
    };

    [MenuItem("Tools/Jackpot/Rapport pièces + sac", false, 306)]
    static void Scan()
    {
        var sb = new StringBuilder();
        foreach (var p in Paths)
        {
            sb.AppendLine("=== " + p);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (prefab == null) { sb.AppendLine("  introuvable"); continue; }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.position = Vector3.zero;
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                var r = t.GetComponent<Renderer>();
                var mf = t.GetComponent<MeshFilter>();
                Mesh m = mf ? mf.sharedMesh : (r is SkinnedMeshRenderer s ? s.sharedMesh : null);
                string mats = r ? string.Join(",", r.sharedMaterials.Select(x => x == null ? "null" : x.name + "{" + x.shader.name + (x.mainTexture ? " tex=" + x.mainTexture.name : "") + " col=" + (x.HasProperty("_BaseColor") ? x.GetColor("_BaseColor").ToString("F2") : "") + "}")) : "";
                sb.AppendLine($"  {Path(t, inst.transform)} lpos={t.localPosition:F3} lrot={t.localEulerAngles:F0} lsc={t.localScale:F3} rendu={(r != null)} centre={(r ? r.bounds.center : Vector3.zero):F4} taille={(r ? r.bounds.size : Vector3.zero):F4} verts={(m ? m.vertexCount : 0)} subs={(m ? m.subMeshCount : 0)} mats={mats}");
            }
            Object.DestroyImmediate(inst);
            var imp = AssetImporter.GetAtPath(p) as ModelImporter;
            if (imp != null) sb.AppendLine($"  importer: scale={imp.globalScale} useFileScale={imp.useFileScale} fileScale={imp.fileScale} materials={imp.materialImportMode} loc={imp.materialLocation}");
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(p)) if (o is Material || o is Texture) sb.AppendLine("  sous-asset: " + o.GetType().Name + " " + o.name);
        }
        File.WriteAllText("Assets/SampleJojo/Script/Editor/coin_scan.txt", sb.ToString());
        Debug.Log("[Pièces] rapport écrit");
    }

    static string Path(Transform t, Transform root) => t == root ? t.name : Path(t.parent, root) + "/" + t.name;
}
