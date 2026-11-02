using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Relevé de la ronde du prof et des obstacles au sol de la classe (pour dessiner d'autres rondes).</summary>
public static class RondesScan
{
    [MenuItem("Tools/Classe/28 - Scan ronde et obstacles", false, 28)]
    static void Scan()
    {
        var cr = Object.FindFirstObjectByType<ClassroomRooms>(FindObjectsInactive.Include);
        var prof = Object.FindFirstObjectByType<ProfAI>(FindObjectsInactive.Include);
        var sb = new StringBuilder();
        if (cr != null) sb.AppendLine($"source interior min={cr.sourceInterior.min:F2} max={cr.sourceInterior.max:F2} spot={cr.sourceSpot:F2} preview={cr.editPreview}");
        if (prof != null)
        {
            sb.AppendLine($"prof pos={prof.transform.position:F2} waypoints={prof.waypoints?.Length}");
            if (prof.waypoints != null) foreach (var w in prof.waypoints) if (w != null) { var o = Orig(cr, w).MultiplyPoint3x4(Vector3.zero); sb.AppendLine($"  WP {w.name} parent={(w.parent ? w.parent.name : "-")} pos={w.position:F2} classe={o:F2}"); }
        }
        var ronde = GameObject.Find("Ronde du prof");
        if (ronde != null) foreach (Transform c in ronde.transform) sb.AppendLine($"  enfant ronde {c.name} pos={c.position:F2} enfants={c.childCount}");
        var markers = new System.Collections.Generic.List<GameObject>();
        if (cr != null)
        {
            float floor = cr.sourceInterior.min.y;
            foreach (var t in cr.Affected())
            {
                if (t == null || !OrigActive(cr, t)) continue;
                var toOrig = Orig(cr, t) * t.localToWorldMatrix.inverse;
                bool first = true; Bounds b = default;
                foreach (var r in t.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer || !r.enabled) continue;
                    var rb = r.bounds;
                    for (int k = 0; k < 8; k++)
                    {
                        var c = toOrig.MultiplyPoint3x4(new Vector3((k & 1) == 0 ? rb.min.x : rb.max.x, (k & 2) == 0 ? rb.min.y : rb.max.y, (k & 4) == 0 ? rb.min.z : rb.max.z));
                        if (first) { b = new Bounds(c, Vector3.zero); first = false; } else b.Encapsulate(c);
                    }
                }
                if (first || b.min.y > floor + 0.5f || b.size.magnitude > 8f) continue;
                sb.AppendLine($"OBST {t.name} min=({b.min.x:F2},{b.min.z:F2}) max=({b.max.x:F2},{b.max.z:F2}) h={b.max.y - floor:F2}");
            }
        }
        if (prof != null && prof.waypoints != null)
            for (int i = 0; i < prof.waypoints.Length; i++)
            {
                var w = prof.waypoints[i]; if (w == null) continue;
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.transform.position = w.position + Vector3.up * 2.2f; s.transform.localScale = Vector3.one * (0.35f + 0.02f * i);
                var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); m.color = Color.Lerp(Color.red, Color.yellow, i / (float)prof.waypoints.Length);
                s.GetComponent<Renderer>().sharedMaterial = m;
                markers.Add(s);
            }
        if (cr != null)
        {
            var c = cr.sourceInterior.center;
            Shoot(new Vector3(c.x, cr.sourceInterior.max.y - 0.2f, c.z), "ronde_dessus.png", Mathf.Max(cr.sourceInterior.size.x, cr.sourceInterior.size.z) * 0.55f);
        }
        foreach (var m in markers) Object.DestroyImmediate(m);
        System.IO.File.WriteAllText("Assets/SampleJojo/Preview~/ronde.txt", sb.ToString());
        Debug.Log("[Ronde] scan écrit (Preview~/ronde.txt, ronde_dessus.png)");
    }

    static ClassroomRooms.EditSnap Snap(ClassroomRooms cr, Transform t)
    {
        if (cr == null || cr.editPreview == 0) return null;
        foreach (var s in cr.editSnapshot) if (s != null && s.t == t) return s;
        return null;
    }

    static bool OrigActive(ClassroomRooms cr, Transform t)
    {
        for (var x = t; x != null; x = x.parent) { var s = Snap(cr, x); if (!(s != null ? s.a : x.gameObject.activeSelf)) return false; }
        return true;
    }

    /// <summary>Matrice de l'objet tel qu'il est dans la classe du cours 1 (même pendant un aperçu de salle).</summary>
    static Matrix4x4 Orig(ClassroomRooms cr, Transform t)
    {
        if (t == null) return Matrix4x4.identity;
        var s = Snap(cr, t);
        var local = s != null ? Matrix4x4.TRS(s.p, s.r, t.localScale) : Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);
        return Orig(cr, t.parent) * local;
    }

    static void Shoot(Vector3 eye, string file, float size)
    {
        var go = new GameObject("PhotoCam");
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = size; cam.nearClipPlane = 0.01f; cam.farClipPlane = 20f;
        go.transform.position = eye; go.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        var rt = new RenderTexture(1280, 1280, 24);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 1280, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 1280), 0, 0); tex.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes("Assets/SampleJojo/Preview~/" + file, tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
    }
}
