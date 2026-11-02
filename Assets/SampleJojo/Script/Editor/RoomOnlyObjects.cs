using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Menu Classe > 30 : « Chaise du jeu (2) » n'apparaît que dans la salle 2 (cours 2).</summary>
public static class RoomOnlyObjects
{
    [MenuItem("Tools/Classe/30 - Chaise du jeu (2) seulement dans sa salle", false, 30)]
    static void Setup()
    {
        if (Application.isPlaying) return;
        var cr = Object.FindFirstObjectByType<ClassroomRooms>(FindObjectsInactive.Include);
        var chair = Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g => g.name == "Chaise du jeu (2)" && g.scene.IsValid());
        if (cr == null || chair == null) { Debug.LogError("[Salles] « Salles des cours » ou « Chaise du jeu (2) » introuvable."); return; }
        // La salle où elle est posée (sinon celle du cours 2).
        var pos = chair.transform.position;
        var room = cr.rooms.FirstOrDefault(r => r != null && r.interior.Contains(pos + Vector3.up * 0.3f)) ?? cr.rooms.FirstOrDefault(r => r != null && r.course == 2);
        if (room == null) { Debug.LogError("[Salles] pas de salle trouvée."); return; }
        // Pendant un aperçu de salle : on l'enlève de la « photo » de la classe (elle reste là où elle est posée).
        if (cr.editSnapshot != null) cr.editSnapshot.RemoveAll(x => x != null && x.t == chair.transform);
        // Elle ne doit pas suivre le déménagement de la classe : on la sort du groupe « Desk ».
        if (chair.transform.parent != null && chair.transform.parent != cr.transform)
        {
            Undo.SetTransformParent(chair.transform, cr.transform, "Chaise salle");
            chair.transform.position = pos;
        }
        Undo.RecordObject(cr, "Chaise salle 2");
        // Elle ne doit pas déménager avec la classe (sinon elle partirait aussi dans les autres salles).
        bool wasMover = cr.movers != null && cr.movers.Contains(chair.transform);
        if (wasMover) cr.movers = cr.movers.Where(t => t != chair.transform).ToArray();
        if (cr.core != null && cr.core.Contains(chair.transform)) cr.core = cr.core.Where(t => t != chair.transform).ToArray();
        room.onlyHere = (room.onlyHere ?? new GameObject[0]).Where(g => g != null && g != chair).Append(chair).ToArray();
        foreach (var r in cr.rooms) if (r != null && r != room && r.onlyHere != null) r.onlyHere = r.onlyHere.Where(g => g != chair).ToArray();
        EditorUtility.SetDirty(cr);
        EditorSceneManager.MarkSceneDirty(cr.gameObject.scene);
        var p = chair.transform.position;
        Debug.Log($"[Salles] « Chaise du jeu (2) » ({p:F2}) : visible seulement dans « {room.name} » (cours {room.course})." + (wasMover ? " (retirée des objets qui déménagent)" : ""));
    }
}
