using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Écrit un rapport sur la scène (diagnostic, sans rien modifier).</summary>
public static class JackpotReport
{
    const string OutPath = "Assets/SampleJojo/Script/Editor/jackpot_report.txt";
    static readonly string[] TableWords = { "table", "bureau", "desk", "plateau", "tabl", "counter", "comptoir" };

    [MenuItem("Tools/Jackpot/Rapport (diagnostic) %#l")]
    static void Write()
    {
        var sb = new StringBuilder();
        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        sb.AppendLine("== Racines de la scène ==");
        foreach (var t in all)
            if (t.parent == null)
                sb.AppendLine(t.name + " pos=" + t.position.ToString("F3") + " rot=" + t.eulerAngles.ToString("F1") +
                              " scale=" + t.lossyScale.ToString("F4") + " enfants=" + t.childCount);

        sb.AppendLine("== Composants (Play=" + Application.isPlaying + ") ==");
        foreach (var t in all)
        {
            if (t.parent != null || (t.name != "Jackpot" && t.name != "Teleport Area Setup")) continue;
            sb.AppendLine("[" + t.name + "] actif=" + t.gameObject.activeInHierarchy);
            foreach (var c in t.GetComponents<Component>())
            {
                if (c == null) { sb.AppendLine("   (script manquant)"); continue; }
                string extra = "";
                if (c is Behaviour be) extra += " enabled=" + be.enabled;
                if (c is Collider co) extra += " enabled=" + co.enabled + " trigger=" + co.isTrigger;
                if (c is MeshCollider mc) extra += " convex=" + mc.convex + " mesh=" + (mc.sharedMesh ? mc.sharedMesh.name : "-");
                if (c is Rigidbody rb) extra += " kinematic=" + rb.isKinematic + " gravity=" + rb.useGravity;
                sb.AppendLine("   " + c.GetType().FullName + extra);
            }
        }
        sb.AppendLine("== Objets XR / joueur ==");
        foreach (var t in all)
        {
            string n = t.name.ToLower();
            if (n.Contains("xr origin") || n.Contains("main camera") || n.Contains("camera offset") || n.Contains("teleport"))
                sb.AppendLine(GetPath(t) + " pos=" + t.position.ToString("F3"));
        }

        sb.AppendLine("== Objets dont le nom ressemble à une table ==");
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string path = GetPath(r.transform).ToLower();
            bool match = false;
            foreach (var w in TableWords) if (path.Contains(w)) match = true;
            if (!match) continue;
            var b = r.bounds;
            sb.AppendLine(GetPath(r.transform) + " | centre=" + b.center.ToString("F3") + " taille=" + b.size.ToString("F3") +
                          " haut=" + b.max.y.ToString("F3") + " collider=" + (r.GetComponent<Collider>() != null) +
                          " mats=" + MatNames(r));
        }

        sb.AppendLine("== Grandes surfaces horizontales hors Jackpot (taille > 0.6m, épaisseur < 0.15m, hauteur 0.5-1.3m au-dessus du sol XR) ==");
        float floor = 0f;
        foreach (var t in all) if (t.parent == null && t.name.StartsWith("XR Origin")) floor = t.position.y;
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r.transform.root.name == "Jackpot") continue;
            var b = r.bounds;
            float h = b.max.y - floor;
            if (b.size.x > 0.6f && b.size.z > 0.4f && b.size.y < 0.15f && h > 0.5f && h < 1.3f)
                sb.AppendLine(GetPath(r.transform) + " | centre=" + b.center.ToString("F3") + " taille=" + b.size.ToString("F3") +
                              " haut=" + b.max.y.ToString("F3") + " collider=" + (r.GetComponent<Collider>() != null));
        }

        File.WriteAllText(OutPath, sb.ToString());
        AssetDatabase.ImportAsset(OutPath);
        Debug.Log("[Jackpot] Rapport écrit : " + OutPath);
    }

    static string MatNames(Renderer r)
    {
        var sb = new StringBuilder();
        foreach (var m in r.sharedMaterials) sb.Append(m ? m.name : "null").Append(",");
        return sb.ToString();
    }

    static string GetPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
        return p;
    }
}
