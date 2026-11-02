using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Menu Classe/20 : prépare les salles des cours 2 (pièce d'à côté) et 3 (salle de l'étage).
/// Rien n'est copié : au chargement des cours 2 et 3, l'intérieur de la classe déménage dans la bonne salle (ClassroomRooms).
/// </summary>
public static class RoomsSetup
{
    static readonly string[] Shell = { "Murs", "Sol", "Porte", "Fenetre", "Plafond" };
    static readonly string[] Excluded = { "Archi_MMI", "Salle", "Menu principal", "Espace menu", "Couloir boutique", "Salles des cours" };
    static readonly string[] CoreNames = { "Jackpot", "Sac Jackpot", "Emplacements Jackpot", "Prof", "Chaise du jeu", "Props bureau", "Untitled (1)",
        "Départ partie (chaise)", "Objets achetés (table)", "Bocal à pièces", "Feuille QCM", "Ambiance classe", "Ronde du prof" };

    [MenuItem("Tools/Classe/20 - Salles des cours 2 et 3", false, 20)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Salles] Quitte le mode Play avant."); return; }
        var scene = SceneManager.GetActiveScene();
        var seat = GameObject.Find("Départ partie (chaise)");
        if (seat == null) { Debug.LogError("[Salles] « Départ partie (chaise) » introuvable."); return; }

        var host = GameObject.Find("Salles des cours");
        if (host == null) { host = new GameObject("Salles des cours"); Undo.RegisterCreatedObjectUndo(host, "Salles"); }
        var cr = host.GetComponent<ClassroomRooms>() ?? Undo.AddComponent<ClassroomRooms>(host);
        Undo.RecordObject(cr, "Salles");

        cr.sourceSpot = seat.transform.position;
        var src = new Bounds();
        src.SetMinMax(new Vector3(-7.43f, 1.525f, 0.55f), new Vector3(1.95f, 3.495f, 8.0f));
        cr.sourceInterior = src;
        var zone = src; zone.Expand(new Vector3(0.6f, 1f, 0.6f));

        // Ce qui déménage : objets racine dans la classe + éléments de « Salle » (sauf murs, sol, porte, fenêtre).
        var movers = new List<Transform>();
        foreach (var go in scene.GetRootGameObjects())
        {
            if (Excluded.Contains(go.name) || go.name.StartsWith("XR Origin")) continue;
            bool inside = TryBounds(go.transform, out var b) ? zone.Contains(b.center) : zone.Contains(go.transform.position);
            if (inside || go.name == "Ronde du prof") movers.Add(go.transform);
        }
        var salle = GameObject.Find("Salle");
        if (salle != null)
            foreach (Transform c in salle.transform)
                if (!Shell.Contains(c.name)) movers.Add(c);
        cr.movers = movers.ToArray();

        // Groupes : chaque enfant est traité à part.
        var groups = new List<Transform>();
        foreach (var n in new[] { "Desk", "Décor classe", "Élèves" }) { var g = GameObject.Find(n); if (g != null && g.transform.parent == null) groups.Add(g.transform); }
        if (salle != null) foreach (var n in new[] { "Horloges", "Lampes", "Livres" }) { var g = salle.transform.Find(n); if (g != null) groups.Add(g); }
        cr.groups = groups.ToArray();

        // Objets du jeu jamais cachés.
        var core = movers.Where(m => m.parent == null && CoreNames.Contains(m.name)).ToList();
        foreach (var g in groups)
            foreach (Transform c in g)
                if (c.GetComponentInChildren<JackpotBook>(true) != null || c.GetComponentInChildren<CoinCounterDisplay>(true) != null ||
                    c.GetComponentInChildren<QcmSheet>(true) != null || c.GetComponentInChildren<CoinJar>(true) != null ||
                    c.GetComponentInChildren<JackpotSocket>(true) != null)
                    core.Add(c);
        foreach (var m in movers)
            if (!core.Contains(m) && !groups.Contains(m) &&
                (m.GetComponentInChildren<JackpotBook>(true) != null || m.GetComponentInChildren<CoinCounterDisplay>(true) != null ||
                 m.GetComponentInChildren<JackpotItem>(true) != null || m.GetComponentInChildren<ProfAI>(true) != null))
                core.Add(m);
        cr.core = core.Distinct().ToArray();

        // Bureau de la prof (+ ce qui est dessus).
        var desk = new List<Transform>();
        var bp = GameObject.Find("Bureau du prof"); if (bp != null) desk.Add(bp.transform);
        if (salle != null) { var b2 = salle.transform.Find("Bureau prof (.blend)"); if (b2 != null) desk.Add(b2); }
        var deco = GameObject.Find("Décor classe");
        if (deco != null) { var on = deco.transform.Find("Sur le bureau du prof"); if (on != null) desk.Add(on); }
        if (desk.Count > 0 && TryBounds(desk[0], out var deskB))
        {
            var u = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Untitled");
            if (u != null && TryBounds(u.transform, out var ub) && Vector3.Distance(new Vector3(ub.center.x, 0, ub.center.z), new Vector3(deskB.center.x, 0, deskB.center.z)) < 2.2f) desk.Add(u.transform);
        }
        cr.profDesk = desk.ToArray();
        cr.classBoard = salle != null ? salle.transform.Find("Tableau") : null;
        var ronde = GameObject.Find("Ronde du prof");
        cr.waypoints = ronde != null ? ronde.transform : null;

        // Les salles.
        var roomA = new ClassroomRooms.Room
        {
            name = "Salle 2 (pièce d'à côté)", course = 2, yaw = 90f,
            playerSpot = new Vector3(5.76f, 1.525f, 7.0f),
            pillars = new[] { new Vector3(6.97f, 0.4f, 2.95f), new Vector3(6.97f, 0.4f, 8.06f) },
            profDeskSpot = new Vector3(8.3f, 1.525f, 1.05f),
            hasBoard = false,
        };
        var a = new Bounds(); a.SetMinMax(new Vector3(2.29f, 1.525f, 0.55f), new Vector3(9.39f, 3.495f, 8.0f)); roomA.interior = a;
        var room3 = new ClassroomRooms.Room
        {
            name = "Salle 3 (étage)", course = 3, yaw = 0f,
            playerSpot = new Vector3(-3.7f, 7.88f, 4.56f),
            pillars = new Vector3[0],
            profDeskSpot = new Vector3(3.75f, 7.88f, 6.3f),
            hasBoard = false,
        };
        var c3 = new Bounds(); c3.SetMinMax(new Vector3(-4.3f, 7.88f, 1.65f), new Vector3(4.15f, 10.36f, 7.6f)); room3.interior = c3;
        // Intérieurs vérifiés au rayon (si les murs ont des collisions).
        var log = new StringBuilder();
        Measure(roomA, log); Measure(room3, log);
        cr.rooms = new[] { roomA, room3 };
        // Les murs et sols de ces pièces n'ont pas de collisions : on en ajoute (invisibles).
        foreach (var r in cr.rooms) Colliders(host.transform, r);
        EditorUtility.SetDirty(cr);

        // Les objets qui déménagent ne doivent pas être « statiques » (sinon ils restent figés à leur place).
        int cleared = 0;
        foreach (var m in movers)
            foreach (var t in m.GetComponentsInChildren<Transform>(true))
            {
                var f = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
                var nf = f & ~(StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
                if (nf != f) { GameObjectUtility.SetStaticEditorFlags(t.gameObject, nf); cleared++; }
            }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveOpenScenes();
        string report = $"[Salles] {movers.Count} objets déménagent ({core.Count} du jeu, {groups.Count} groupes, bureau prof : {desk.Count} morceaux, statique retiré sur {cleared}). " +
            $"Déménagent : {string.Join(", ", movers.Select(m => m.name))}. {log}";
        Debug.Log(report);
        File.WriteAllText("Assets/SampleJojo/Preview~/salles_setup.txt", report);
    }

    static void Colliders(Transform host, ClassroomRooms.Room r)
    {
        var old = host.Find("Collisions " + r.name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var root = new GameObject("Collisions " + r.name).transform;
        root.SetParent(host, false);
        var i = r.interior;
        float h = i.size.y, t = 0.2f;
        Box(root, "Sol", new Vector3(i.center.x, i.min.y - t * 0.5f, i.center.z), new Vector3(i.size.x + 1f, t, i.size.z + 1f));
        Box(root, "Plafond", new Vector3(i.center.x, i.max.y + t * 0.5f, i.center.z), new Vector3(i.size.x + 1f, t, i.size.z + 1f));
        Box(root, "Mur -X", new Vector3(i.min.x - t * 0.5f, i.center.y, i.center.z), new Vector3(t, h, i.size.z + 1f));
        Box(root, "Mur +X", new Vector3(i.max.x + t * 0.5f, i.center.y, i.center.z), new Vector3(t, h, i.size.z + 1f));
        Box(root, "Mur -Z", new Vector3(i.center.x, i.center.y, i.min.z - t * 0.5f), new Vector3(i.size.x + 1f, h, t));
        Box(root, "Mur +Z", new Vector3(i.center.x, i.center.y, i.max.z + t * 0.5f), new Vector3(i.size.x + 1f, h, t));
        foreach (var p in r.pillars)
        {
            var go = new GameObject("Poteau");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(p.x, i.center.y, p.z);
            var cc = go.AddComponent<CapsuleCollider>();
            cc.radius = p.y; cc.height = h; cc.direction = 1;
        }
    }

    static void Box(Transform parent, string name, Vector3 center, Vector3 size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.AddComponent<BoxCollider>().size = size;
    }

    static void Measure(ClassroomRooms.Room r, StringBuilder log)
    {
        Vector3 c = r.interior.center; c.y = r.interior.min.y + 1.1f;
        log.Append($" | {r.name} :");
        foreach (var d in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
        {
            if (Physics.Raycast(c, d, out var hit, 15f, ~0, QueryTriggerInteraction.Ignore))
                log.Append($" {d}→{hit.distance:F2}m ({hit.collider.name})");
            else log.Append($" {d}→rien");
        }
        if (Physics.Raycast(c, Vector3.down, out var h2, 3f)) log.Append($" sol {h2.point.y:F2}");
        if (Physics.Raycast(c, Vector3.up, out var h3, 4f)) log.Append($" plafond {h3.point.y:F2}");
    }

    static bool TryBounds(Transform t, out Bounds b)
    {
        b = default; bool first = true;
        foreach (var r in t.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
        }
        return !first;
    }

    // ---------------- Tests ----------------
    [MenuItem("Tools/Classe/21 - Test : cours 2 (en Play)", false, 21)]
    static void Test2() => Jump(2);
    [MenuItem("Tools/Classe/22 - Test : cours 3 (en Play)", false, 22)]
    static void Test3() => Jump(3);
    [MenuItem("Tools/Classe/24 - Test : niveau 2, cours 1 (en Play)", false, 24)]
    static void TestHard() { if (!Application.isPlaying) return; GameProgress.StartChapter(2, GameProgress.Coins, 0); GameMenu.SkipNextMenu = true; SceneManager.LoadScene(SceneManager.GetActiveScene().name); }

    static void Jump(int level)
    {
        if (!Application.isPlaying) return;
        GameProgress.Level = level;
        GameMenu.SkipNextMenu = true;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    [MenuItem("Tools/Classe/23 - Photos de la salle actuelle (en Play)", false, 23)]
    static void Photos()
    {
        if (!Application.isPlaying) return;
        var inner = ClassroomRooms.ActiveInterior;
        var cam = Camera.main;
        if (cam != null) Shoot(cam.transform.position, cam.transform.position + cam.transform.forward * 3f, "salle_joueur.png", 80f);
        if (inner.HasValue)
        {
            var r = inner.Value;
            float y = r.min.y + 1.85f;
            Shoot(new Vector3(r.min.x + 0.3f, y, r.min.z + 0.3f), new Vector3(r.max.x, r.min.y, r.max.z), "salle_coin1.png", 85f);
            Shoot(new Vector3(r.max.x - 0.3f, y, r.max.z - 0.3f), new Vector3(r.min.x, r.min.y, r.min.z), "salle_coin2.png", 85f);
        }
        Debug.Log("[Salles] photos");
    }

    static void Shoot(Vector3 eye, Vector3 target, string file, float fov)
    {
        var go = new GameObject("PhotoCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov; cam.nearClipPlane = 0.03f;
        go.transform.position = eye; go.transform.LookAt(target, Vector3.up);
        var rt = new RenderTexture(1280, 800, 24);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes("Assets/SampleJojo/Preview~/" + file, tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
    }
}
