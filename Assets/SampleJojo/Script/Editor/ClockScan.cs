using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ClockScan
{
    const string Path = "Assets/SampleJojo/model/Props/clasroom/Digital_Clock.blend";

    [MenuItem("Tools/Jackpot/Rapport horloge (.blend)", false, 304)]
    static void Scan()
    {
        var sb = new StringBuilder();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path);
        if (prefab == null) { Debug.LogError("[Horloge] introuvable"); return; }
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        inst.transform.position = Vector3.zero;
        foreach (var t in inst.GetComponentsInChildren<Transform>(true))
        {
            var r = t.GetComponent<Renderer>();
            var mf = t.GetComponent<MeshFilter>();
            string mats = r != null ? string.Join(",", r.sharedMaterials.Where(m => m != null).Select(m => m.name + (m.mainTexture ? "[" + m.mainTexture.name + "]" : ""))) : "";
            sb.AppendLine($"{t.name} parent={(t.parent ? t.parent.name : "-")} rendu={(r != null)} centre={(r ? r.bounds.center : t.position):F3} taille={(r ? r.bounds.size : Vector3.zero):F3} rot={t.eulerAngles:F0} sous-meshes={(mf && mf.sharedMesh ? mf.sharedMesh.subMeshCount : 0)} mats={mats}");
        }
        Object.DestroyImmediate(inst);
        File.WriteAllText("Assets/SampleJojo/Script/Editor/clock_scan.txt", sb.ToString());
        Debug.Log("[Horloge] rapport écrit");
    }
}
