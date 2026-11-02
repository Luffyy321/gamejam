using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class PlayerDiag
{
    [MenuItem("Tools/Classe/9 - Diagnostic joueur", false, 9)]
    static void Diag()
    {
        var sb = new StringBuilder("[Diag] ");
        var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include);
        sb.Append(origin != null ? $"XROrigin {origin.name} actif={origin.gameObject.activeInHierarchy} pos={origin.transform.position:F2} rot={origin.transform.eulerAngles:F0} cam={(origin.Camera != null ? origin.Camera.transform.position.ToString("F2") : "?")} | " : "pas d'XROrigin | ");
        var cc = origin != null ? origin.GetComponent<CharacterController>() : null;
        if (cc != null) sb.Append($"CC enabled={cc.enabled} | ");
        foreach (var m in Object.FindObjectsByType<GameMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            sb.Append($"GameMenu {m.name} actif={m.isActiveAndEnabled} menuSpawn={(m.menuSpawn != null ? m.menuSpawn.position.ToString("F2") : "VIDE")} gameSpawn={(m.gameSpawn != null ? m.gameSpawn.position.ToString("F2") + " fwd " + m.gameSpawn.forward.ToString("F2") : "VIDE")} root={(m.root != null ? m.root.name : "?")} | ");
        sb.Append($"SkipNextMenu={GameMenu.SkipNextMenu} | ");
        var origins = Object.FindObjectsByType<Unity.XR.CoreUtils.XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        sb.Append($"nb XROrigin={origins.Length} | ");
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.name.Contains("Départ") || t.name.Contains("Spawn") || t.name.Contains("Menu")))
            sb.Append($"{t.name} {t.position:F2} actif={t.gameObject.activeInHierarchy}; ");
        var sp = GameObject.Find("Espace menu");
        if (sp != null)
        {
            sb.Append($"| Espace menu enfants={sp.transform.childCount}: ");
            foreach (Transform c in sp.transform)
            {
                var r = c.GetComponent<Renderer>();
                sb.Append($"{c.name} actif={c.gameObject.activeInHierarchy} rend={(r != null ? r.enabled + " mat=" + (r.sharedMaterial != null ? r.sharedMaterial.name + "/" + r.sharedMaterial.shader.name : "AUCUN") : "-")} col={c.GetComponent<Collider>() != null}; ");
            }
        }
        var cl = Object.FindFirstObjectByType<ClassBanner>();
        Debug.Log(sb.ToString());
        System.IO.File.WriteAllText("Assets/SampleJojo/Preview~/diag.txt", sb.ToString());
    }
}
