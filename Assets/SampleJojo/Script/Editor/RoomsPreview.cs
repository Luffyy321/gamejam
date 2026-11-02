using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Aperçu des salles des cours 2 et 3 en mode édition (sans lancer le jeu).
/// La classe est photographiée avant (positions, rotations, objets cachés) et « Revenir à la classe » remet tout.
/// Même si tu enregistres la scène en aperçu, le jeu remet la classe d'abord au lancement : rien ne casse.
/// </summary>
public static class RoomsPreview
{
    [MenuItem("Tools/Classe/25 - Apercu salle 2 (cours 2) en edition", false, 25)]
    static void Preview2() => Preview(2);

    [MenuItem("Tools/Classe/26 - Apercu salle 3 (cours 3) en edition", false, 26)]
    static void Preview3() => Preview(3);

    [MenuItem("Tools/Classe/27 - Revenir a la classe (cours 1)", false, 27)]
    static void Back()
    {
        var cr = Find(); if (cr == null) return;
        if (cr.editPreview == 0) { Debug.Log("[Salles] On est déjà dans la classe du cours 1."); return; }
        Record(cr, "Revenir à la classe");
        cr.RestoreEditSnapshot();
        EditorSceneManager.MarkSceneDirty(cr.gameObject.scene);
        FrameClass(cr);
        Debug.Log("[Salles] Retour à la classe du cours 1.");
    }

    [MenuItem("Tools/Classe/25 - Apercu salle 2 (cours 2) en edition", true)]
    [MenuItem("Tools/Classe/26 - Apercu salle 3 (cours 3) en edition", true)]
    [MenuItem("Tools/Classe/27 - Revenir a la classe (cours 1)", true)]
    static bool NotPlaying() => !Application.isPlaying;

    internal static void Preview(int course)
    {
        var cr = Find(); if (cr == null) return;
        var room = cr.RoomFor(course);
        if (room == null) { Debug.LogWarning($"[Salles] Pas de salle pour le cours {course}."); return; }
        Record(cr, $"Aperçu salle du cours {course}");
        if (cr.editPreview != 0) cr.RestoreEditSnapshot();   // on repart toujours de la classe
        cr.TakeEditSnapshot();
        cr.ApplyRoom(room);
        cr.editPreview = course;
        EditorUtility.SetDirty(cr);
        EditorSceneManager.MarkSceneDirty(cr.gameObject.scene);
        View(room.interior);
        Photo(room.interior, $"apercu_salle{course}");
        Debug.Log($"[Salles] Aperçu en édition : « {room.name} ». Tools > Classe > 27 pour revenir à la classe.");
    }

    static void Record(ClassroomRooms cr, string name)
    {
        Undo.IncrementCurrentGroup();
        Undo.SetCurrentGroupName(name);
        Undo.RecordObject(cr, name);
        foreach (var t in cr.Affected()) { Undo.RecordObject(t, name); Undo.RecordObject(t.gameObject, name); }
        foreach (var s in cr.editSnapshot) if (s != null && s.t != null) { Undo.RecordObject(s.t, name); Undo.RecordObject(s.t.gameObject, name); }
    }

    static void FrameClass(ClassroomRooms cr)
    {
        if (cr.sourceInterior.size.sqrMagnitude > 0f) View(cr.sourceInterior);
    }

    /// <summary>Place la vue Scene dans un coin haut de la salle, tournée vers le centre (on voit toute la salle de l'intérieur).</summary>
    static void View(Bounds r)
    {
        var sv = SceneView.lastActiveSceneView;
        if (sv == null) return;
        Vector3 eye = new Vector3(r.min.x + 0.3f, r.max.y - 0.3f, r.min.z + 0.3f);
        Vector3 pivot = new Vector3(r.center.x, r.min.y + 0.6f, r.center.z);
        Vector3 dir = pivot - eye;
        sv.orthographic = false;
        sv.LookAtDirect(pivot, Quaternion.LookRotation(dir), dir.magnitude * Mathf.Tan(30f * Mathf.Deg2Rad));
        sv.Repaint();
    }

    static void Photo(Bounds r, string name)
    {
        float y = r.max.y - 0.3f;
        Shoot(new Vector3(r.min.x + 0.3f, y, r.min.z + 0.3f), new Vector3(r.max.x, r.min.y, r.max.z), name + "_a.png");
        Shoot(new Vector3(r.max.x - 0.3f, y, r.max.z - 0.3f), new Vector3(r.min.x, r.min.y, r.min.z), name + "_b.png");
    }

    static void Shoot(Vector3 eye, Vector3 target, string file)
    {
        var go = new GameObject("PhotoCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 85f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 200f;
        go.transform.position = eye; go.transform.LookAt(target, Vector3.up);
        var rt = new RenderTexture(1280, 800, 24);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); tex.Apply();
        RenderTexture.active = null;
        System.IO.Directory.CreateDirectory("Assets/SampleJojo/Preview~/");
        System.IO.File.WriteAllBytes("Assets/SampleJojo/Preview~/" + file, tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
    }

    static ClassroomRooms Find()
    {
        var cr = Object.FindFirstObjectByType<ClassroomRooms>(FindObjectsInactive.Include);
        if (cr == null) Debug.LogWarning("[Salles] Pas de « Salles des cours » dans la scène (Tools > Classe > 20).");
        return cr;
    }
}
