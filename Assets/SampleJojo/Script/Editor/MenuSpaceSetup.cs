using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu : Tools > Jackpot > 34 - Espace gris du menu + apparition sur la chaise.
/// Le joueur commence dans un espace gris (loin sous la classe) avec le menu ; JOUER le fait apparaître sur la chaise du sac.
/// </summary>
public static class MenuSpaceSetup
{
    const string Dir = "Assets/SampleJojo/model/menu/";
    static readonly Vector3 SpaceCenter = new Vector3(0f, -60f, 0f);

    [MenuItem("Tools/Jackpot/34 - Espace gris du menu + apparition sur la chaise", false, 145)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Menu] Quitte le mode Play avant."); return; }
        var menu = Object.FindFirstObjectByType<GameMenu>();
        if (menu == null) { Debug.LogError("[Menu] Menu principal introuvable : lance d'abord le menu 31."); return; }

        // ---------------- Espace gris ----------------
        var old = GameObject.Find("Espace menu");
        if (old != null) Undo.DestroyObjectImmediate(old);
        var space = new GameObject("Espace menu");
        Undo.RegisterCreatedObjectUndo(space, "Espace menu");
        space.transform.position = SpaceCenter;

        var dome = new GameObject("Dôme gris");
        dome.transform.SetParent(space.transform, false);
        dome.transform.localScale = Vector3.one * 20f;          // 10 m de rayon
        dome.AddComponent<MeshFilter>().sharedMesh = InvertedSphere();
        var dr = dome.AddComponent<MeshRenderer>();
        dr.sharedMaterial = Unlit("Menu_Espace_Gris", new Color(0.6f, 0.6f, 0.62f));
        dr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        floor.name = "Sol gris";
        floor.transform.SetParent(space.transform, false);
        floor.transform.localPosition = new Vector3(0f, -0.01f, 0f);
        floor.transform.localScale = new Vector3(8f, 0.01f, 8f);
        Object.DestroyImmediate(floor.GetComponent<Collider>());
        floor.GetComponent<MeshRenderer>().sharedMaterial = Unlit("Menu_Sol_Gris", new Color(0.5f, 0.5f, 0.52f));
        floor.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Sol solide (sinon on tombe) et murs invisibles.
        var ground = new GameObject("Sol (collision)");
        ground.transform.SetParent(space.transform, false);
        ground.transform.localPosition = new Vector3(0f, -0.1f, 0f);
        ground.AddComponent<BoxCollider>().size = new Vector3(20f, 0.2f, 20f);
        for (int i = 0; i < 4; i++)
        {
            var wall = new GameObject("Mur invisible " + (i + 1));
            wall.transform.SetParent(space.transform, false);
            wall.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
            wall.transform.localPosition = wall.transform.localRotation * new Vector3(0f, 1.5f, 3.5f);
            wall.AddComponent<BoxCollider>().size = new Vector3(8f, 3f, 0.2f);
        }

        // Lumière douce pour les parties « éclairées » du carnet.
        var lightGo = new GameObject("Lumière menu");
        lightGo.transform.SetParent(space.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 2.6f, 0.6f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 8f;
        light.intensity = 2f;
        light.shadows = LightShadows.None;

        var menuSpawn = new GameObject("Départ menu");
        menuSpawn.transform.SetParent(space.transform, false);
        menuSpawn.transform.localPosition = Vector3.zero;
        menuSpawn.transform.localRotation = Quaternion.identity;

        // ---------------- Apparition sur la chaise ----------------
        var oldSpawn = GameObject.Find("Départ partie (chaise)");
        if (oldSpawn != null) Undo.DestroyObjectImmediate(oldSpawn);
        var gameSpawn = new GameObject("Départ partie (chaise)");
        Undo.RegisterCreatedObjectUndo(gameSpawn, "Départ");
        var chair = GameObject.Find("Chaise du jeu");
        var bag = Object.FindFirstObjectByType<JackpotBag>();
        var socket = Object.FindFirstObjectByType<JackpotSocket>();
        string info;
        if (chair != null)
        {
            Vector3 p = chair.transform.position;
            Vector3 look = socket != null ? socket.transform.position - p : chair.transform.forward;
            look.y = 0f;
            if (look.sqrMagnitude < 1e-4f) look = Vector3.forward;
            gameSpawn.transform.SetPositionAndRotation(p, Quaternion.LookRotation(look.normalized, Vector3.up));
            info = "chaise du jeu";
        }
        else if (bag != null)
        {
            gameSpawn.transform.SetPositionAndRotation(bag.transform.position + bag.transform.forward * 0.6f, Quaternion.LookRotation(-bag.transform.forward, Vector3.up));
            info = "à côté du sac (chaise introuvable)";
        }
        else info = "introuvable";

        Undo.RecordObject(menu, "Menu");
        menu.menuSpawn = menuSpawn.transform;
        menu.gameSpawn = gameSpawn.transform;
        menu.fader = Object.FindFirstObjectByType<GameOverRestart>();
        EditorUtility.SetDirty(menu);

        // Le joueur commence déjà dans l'espace gris (même avant le premier fondu).
        var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        if (origin != null)
        {
            Undo.RecordObject(origin.transform, "Joueur");
            origin.transform.SetPositionAndRotation(SpaceCenter, Quaternion.identity);
        }
        // Le carnet aussi, pour le voir dans l'éditeur.
        Undo.RecordObject(menu.transform, "Menu");
        menu.transform.SetPositionAndRotation(SpaceCenter + new Vector3(-0.22f, 1.45f, 0.8f), Quaternion.Euler(8f, 0f, 0f));

        EditorSceneManager.MarkSceneDirty(space.scene);
        Debug.Log($"[Menu] Espace gris créé ; JOUER → apparition {info}.");
    }

    static Mesh InvertedSphere()
    {
        string path = Dir + "Sphere_inversee.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) return existing;
        var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        var m = Object.Instantiate(tmp.GetComponent<MeshFilter>().sharedMesh);
        Object.DestroyImmediate(tmp);
        m.name = "Sphere_inversee";
        var t = m.triangles;
        for (int i = 0; i < t.Length; i += 3) { int a = t[i]; t[i] = t[i + 1]; t[i + 1] = a; }
        m.triangles = t;
        m.normals = m.normals.Select(n => -n).ToArray();
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    static Material Unlit(string name, Color c)
    {
        string path = Dir + "Materiaux/" + name + ".mat";
        if (!AssetDatabase.IsValidFolder(Dir + "Materiaux")) AssetDatabase.CreateFolder(Dir.TrimEnd('/'), "Materiaux");
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetColor("_BaseColor", c);
        EditorUtility.SetDirty(m);
        return m;
    }
}
