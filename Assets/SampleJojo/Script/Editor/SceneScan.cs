using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Relevé des objets de la scène (racines + objets intéressants) dans scene_scan.txt.</summary>
public static class SceneScan
{
    [MenuItem("Tools/Jackpot/Rapport scène (objets)", false, 300)]
    static void Scan()
    {
        var sb = new StringBuilder();
        sb.AppendLine("== Racines ==");
        foreach (var go in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
            sb.AppendLine(Line(go));
        string[] keys = { "untitled", "siege", "siège", "seat", "chair", "chaise", "bureau", "desk", "prof", "mafia", "fauteuil", "tableau", "board" };
        sb.AppendLine("== Objets ==");
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string n = t.name.ToLowerInvariant();
            bool hit = false;
            foreach (var k in keys) if (n.Contains(k)) { hit = true; break; }
            if (hit && (t.parent == null || t.parent.parent == null || t.GetComponent<Renderer>() != null)) sb.AppendLine(Path(t) + " | " + Line(t.gameObject));
        }
        File.WriteAllText("Assets/SampleJojo/Script/Editor/scene_scan.txt", sb.ToString());
        Debug.Log("[Scan] écrit : Assets/SampleJojo/Script/Editor/scene_scan.txt");
    }

    static string Line(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        string b = "pas de rendu";
        if (rs.Length > 0)
        {
            var bb = rs[0].bounds;
            foreach (var r in rs) bb.Encapsulate(r.bounds);
            b = $"centre={bb.center:F2} taille={bb.size:F2}";
        }
        return $"{go.name} actif={go.activeInHierarchy} pos={go.transform.position:F2} rot={go.transform.eulerAngles:F0} enfants={go.transform.childCount} {b}";
    }

    static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
}
