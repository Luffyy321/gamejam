using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Crée les rondes de la prof pour les cours 2 et 3 (la ronde du cours 1 = les « Point 1…8 » d'origine).
/// Les points sont placés dans la classe du cours 1, dans les allées entre les tables ; au cours 2 / 3,
/// ils déménagent avec le reste dans la nouvelle salle. Tu peux ensuite les déplacer à la main
/// (objets « Ronde cours 2 » / « Ronde cours 3 » sous « Ronde du prof », visibles en bleu et vert).
/// </summary>
public static class RondesSetup
{
    // Positions (x, z) dans la classe du cours 1. Notre chaise est en (-6.96, 3.31), on regarde vers +X (le tableau).
    // Cours 2 : allée du milieu jusqu'à notre table, puis le fond de la classe et le côté du bureau.
    static readonly Vector2[] Cours2 = {
        new Vector2( 1.20f, 4.45f), new Vector2(-2.30f, 4.45f), new Vector2(-5.10f, 4.45f),
        new Vector2(-3.60f, 4.60f), new Vector2(-3.60f, 7.15f), new Vector2(-6.40f, 7.15f),
        new Vector2(-3.60f, 7.20f), new Vector2(-0.50f, 7.45f), new Vector2( 1.78f, 7.60f), new Vector2( 1.78f, 5.10f) };
    // Cours 3 : elle tourne autour de nous : allée de devant jusqu'au coin de notre table, puis l'autre côté.
    static readonly Vector2[] Cours3 = {
        new Vector2( 1.00f, 0.95f), new Vector2( 1.00f, 2.27f), new Vector2(-3.60f, 2.27f),
        new Vector2(-5.75f, 2.44f), new Vector2(-3.60f, 2.30f), new Vector2(-3.60f, 4.45f),
        new Vector2(-5.10f, 4.45f), new Vector2(-3.60f, 4.50f), new Vector2( 1.00f, 4.45f) };

    [MenuItem("Tools/Classe/29 - Creer les rondes des cours 2 et 3", false, 29)]
    static void Create()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Ronde] Quitte le mode Play avant."); return; }
        var prof = Object.FindFirstObjectByType<ProfAI>(FindObjectsInactive.Include);
        var ronde = GameObject.Find("Ronde du prof");
        if (prof == null || ronde == null) { Debug.LogWarning("[Ronde] Prof ou « Ronde du prof » introuvable."); return; }
        var cr = Object.FindFirstObjectByType<ClassroomRooms>(FindObjectsInactive.Include);

        // On travaille dans la classe du cours 1 (si un aperçu de salle est affiché, on revient puis on le réaffiche).
        int preview = cr != null ? cr.editPreview : 0;
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName("Rondes des cours 2 et 3");
        if (preview != 0)
        {
            Undo.RecordObject(cr, "Rondes");
            foreach (var s in cr.editSnapshot) if (s != null && s.t != null) { Undo.RecordObject(s.t, "Rondes"); Undo.RecordObject(s.t.gameObject, "Rondes"); }
            cr.RestoreEditSnapshot();
        }

        float y = prof.waypoints != null && prof.waypoints.Length > 0 && prof.waypoints[0] != null ? prof.waypoints[0].position.y : prof.transform.position.y;
        Undo.RecordObject(prof, "Rondes");
        prof.rondeCours2 = Build(ronde.transform, "Ronde cours 2", Cours2, y);
        prof.rondeCours3 = Build(ronde.transform, "Ronde cours 3", Cours3, y);
        EditorUtility.SetDirty(prof);
        EditorSceneManager.MarkSceneDirty(prof.gameObject.scene);
        Debug.Log($"[Ronde] Rondes créées : cours 2 ({Cours2.Length} points, en bleu), cours 3 ({Cours3.Length} points, en vert).");

        if (preview != 0) RoomsPreview.Preview(preview);
    }

    static Transform[] Build(Transform parent, string name, Vector2[] pts, float y)
    {
        var old = parent.Find(name);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var g = new GameObject(name).transform;
        Undo.RegisterCreatedObjectUndo(g.gameObject, "Rondes");
        g.SetParent(parent, false);
        g.localPosition = Vector3.zero; g.localRotation = Quaternion.identity;
        var list = new List<Transform>();
        for (int i = 0; i < pts.Length; i++)
        {
            var t = new GameObject("Point " + (i + 1)).transform;
            t.SetParent(g, false);
            t.position = new Vector3(pts[i].x, y, pts[i].y);
            list.Add(t);
        }
        return list.ToArray();
    }
}
