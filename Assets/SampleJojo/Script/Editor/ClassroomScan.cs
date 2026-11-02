using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Liste les objets du .blend de la salle de classe et repère porte / murs / tables dans la scène.</summary>
public static class ClassroomScan
{
    const string Blend = "Assets/SampleJojo/model/Props/clasroom/Salle de Classe WALAD .blend";

    [MenuItem("Tools/Jackpot/Rapport salle de classe (.blend)", false, 301)]
    static void Scan()
    {
        var sb = new StringBuilder();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Blend);
        sb.AppendLine("== Objets du .blend (" + (prefab != null ? "ok" : "INTROUVABLE") + ") ==");
        if (prefab != null)
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.position = Vector3.zero;
            foreach (Transform t in inst.GetComponentsInChildren<Transform>(true))
            {
                if (t == inst.transform) continue;
                var r = t.GetComponent<Renderer>();
                var mats = r != null ? string.Join(",", r.sharedMaterials.Where(m => m != null).Select(m => m.name)) : "";
                sb.AppendLine($"{Path(t, inst.transform)} | rendu={(r != null)} centre={(r != null ? r.bounds.center : t.position):F2} taille={(r != null ? r.bounds.size : Vector3.zero):F2} rot={t.eulerAngles:F0} mats={mats}");
            }
            Object.DestroyImmediate(inst);
        }

        sb.AppendLine("== Scène : portes / murs / fenêtres / tableaux (salle du bas, y < 5) ==");
        string[] keys = { "porte", "door", "mur", "wall", "fenetre", "fenêtre", "window", "tableau", "armoire", "placard", "sol", "floor" };
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            string n = r.name.ToLowerInvariant();
            if (!keys.Any(k => n.Contains(k))) continue;
            if (r.bounds.min.y > 5f) continue;
            sb.AppendLine($"{r.name} centre={r.bounds.center:F2} taille={r.bounds.size:F2}");
        }

        sb.AppendLine("== Tables de l'utilisateur (objet Desk) ==");
        var desk = GameObject.Find("Desk");
        if (desk != null)
            foreach (var r in desk.GetComponentsInChildren<Renderer>())
                sb.AppendLine($"{Path(r.transform, desk.transform)} centre={r.bounds.center:F2} taille={r.bounds.size:F2} haut={r.bounds.max.y:F3}");

        File.WriteAllText("Assets/SampleJojo/Script/Editor/classroom_scan.txt", sb.ToString());
        Debug.Log("[Scan] écrit : classroom_scan.txt");
    }

    static string Path(Transform t, Transform root) => t == root || t.parent == null ? t.name : Path(t.parent, root) + "/" + t.name;
}
