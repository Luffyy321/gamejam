using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Relevé de la salle du bas : grands éléments (murs, sol, portes) et objets près des poignées de porte.</summary>
public static class RoomScan
{
    [MenuItem("Tools/Jackpot/Rapport murs de la salle", false, 303)]
    static void Scan()
    {
        var sb = new StringBuilder();
        var all = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        var room = new Bounds(new Vector3(-2.5f, 3f, 4f), new Vector3(16f, 4f, 14f));
        sb.AppendLine("== Grands éléments dans la zone de la salle ==");
        foreach (var r in all)
        {
            var b = r.bounds;
            if (!room.Intersects(b)) continue;
            if (Mathf.Max(b.size.x, b.size.z) < 1.4f && b.size.y < 1.4f) continue;
            if (b.size.magnitude > 60f) continue;
            sb.AppendLine($"{r.name} centre={b.center:F2} taille={b.size:F2} parent={(r.transform.parent ? r.transform.parent.name : "-")}");
        }
        sb.AppendLine("== Près des poignées de porte ==");
        var knobs = all.Where(r => r.name.StartsWith("Door_Knob")).ToList();
        foreach (var k in knobs)
        {
            sb.AppendLine($"[poignée] {k.name} {k.bounds.center:F2}");
            foreach (var r in all)
            {
                if (r == k) continue;
                if (r.bounds.SqrDistance(k.bounds.center) < 0.25f * 0.25f)
                    sb.AppendLine($"   proche : {r.name} centre={r.bounds.center:F2} taille={r.bounds.size:F2}");
            }
        }
        File.WriteAllText("Assets/SampleJojo/Script/Editor/room_scan.txt", sb.ToString());
        Debug.Log("[Scan] écrit : room_scan.txt");
    }
}
