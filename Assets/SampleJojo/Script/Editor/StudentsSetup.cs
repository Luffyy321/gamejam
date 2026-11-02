using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Élèves animés (dossier model/eleve) assis sur des chaises de la classe, qui jouent leur animation (idle) en boucle.
/// </summary>
public static class StudentsSetup
{
    const string Dir = "Assets/SampleJojo/model/eleve/";
    const string RootName = "Élèves";

    // ---------------- Scan (pour voir les chaises et les modèles) ----------------
    [MenuItem("Tools/Classe/1 - Scan élèves + chaises", false, 1)]
    static void Scan()
    {
        var sb = new StringBuilder();
        foreach (var path in Directory.GetFiles(Dir, "*.fbx"))
        {
            var p = path.Replace('\\', '/');
            var imp = AssetImporter.GetAtPath(p) as ModelImporter;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            var clips = AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            Bounds b = default; bool first = true;
            if (go != null) foreach (var r in go.GetComponentsInChildren<Renderer>()) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            sb.AppendLine($"MODELE {p} type={imp?.animationType} clips=[{string.Join(", ", clips.Select(c => $"{c.name} {c.length:F2}s"))}] bornes={b.center:F2} taille={b.size:F2} renderers={go?.GetComponentsInChildren<Renderer>().Length}");
        }
        var player = GameObject.Find("Départ partie (chaise)");
        if (player != null) sb.AppendLine($"JOUEUR chaise {player.transform.position:F2} avant {player.transform.forward:F2}");
        var gameChair = GameObject.Find("Chaise du jeu");
        if (gameChair != null) sb.AppendLine($"CHAISE DU JEU {gameChair.transform.position:F2} bornes {WB(gameChair).center:F2} {WB(gameChair).size:F2}");
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            string n = t.name.ToLowerInvariant();
            if (!(n.Contains("chaise") || n.Contains("chair") || n.Contains("table") || n.Contains("desk") || n.Contains("bureau"))) continue;
            if (t.parent != null && (t.parent.name.ToLowerInvariant().Contains("chaise") || t.parent.name.ToLowerInvariant().Contains("chair"))) continue;
            var bb = WB(t.gameObject);
            sb.AppendLine($"{PathOf(t)} | pos {t.position:F2} | bornes {bb.center:F2} taille {bb.size:F2} | enfants {t.childCount}");
        }
        Directory.CreateDirectory("Assets/SampleJojo/Preview~/");
        File.WriteAllText("Assets/SampleJojo/Preview~/scan_eleves.txt", sb.ToString());
        Debug.Log("[Élèves] scan écrit dans Preview~/scan_eleves.txt");
    }

    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

    static Bounds WB(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // ---------------- Élèves assis ----------------
    // Chaises choisies (les élèves sont devant et sur les côtés du joueur ; la sienne reste libre).
    // Dans l'ordre des fichiers : Eleve 1, Eleve 5, Eleve 6, Eleve 7, Sitting_éleve3.
    static readonly string[] Chairs = { "Desk/dupli (29)/Chaise du jeu (1)", "Desk/dupli (23)/Chaise du jeu (1)", "Desk/dupli (24)/Chaise du jeu (1)", "Desk/dupli (27)/Chaise du jeu (1)", "Desk/dupli (31)/Chaise du jeu (1)", "Desk/dupli (22)/Chaise du jeu (1)", "Desk/dupli (26)/Chaise du jeu (1)", "Desk/dupli (30)/Chaise du jeu (1)" };
    const string EventSuffix = " anim event";
    // Fichier qui remplace l'idle du modèle (ex. « Eleve 5 vrai.fbx »).
    static readonly string[] IdleSuffixes = { " vrai", " idle" };
    const float SeatedHeight = 1.25f;   // haut de la tête au-dessus du sol pour un élève assis (m)

    [MenuItem("Tools/Classe/2 - Élèves assis (animation en boucle)", false, 2)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Élèves] Quitte le mode Play avant."); return; }
        var files = Directory.GetFiles(Dir, "*.fbx").Select(f => f.Replace('\\', '/'))
            .Where(f => !Path.GetFileNameWithoutExtension(f).EndsWith(EventSuffix) && !IdleSuffixes.Any(x => Path.GetFileNameWithoutExtension(f).EndsWith(x))).OrderBy(f => f).ToArray();
        if (files.Length == 0) { Debug.LogError("[Élèves] aucun FBX dans " + Dir); return; }
        var player = GameObject.Find("Départ partie (chaise)");
        Vector3 fwd = player != null ? player.transform.forward : Vector3.right; fwd.y = 0f; fwd.Normalize();

        var old = GameObject.Find(RootName);
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Élèves");

        var log = new List<string>();
        for (int i = 0; i < files.Length && i < Chairs.Length; i++)
        {
            string path = files[i];
            var clip = Import(path);
            // Idle de remplacement si présent (ex. « Eleve 5 vrai.fbx »).
            foreach (var suf in IdleSuffixes)
            {
                string idlePath = Dir + Path.GetFileNameWithoutExtension(path) + suf + ".fbx";
                if (File.Exists(idlePath)) { var c2 = Import(idlePath, "Idle", true); if (c2 != null) { clip = c2; log.Add($"idle remplacé par {Path.GetFileName(idlePath)}"); } break; }
            }
            // Animation « event » (ex. « Eleve 5 anim event.fbx ») : jouée de temps en temps, au hasard.
            string evPath = Dir + Path.GetFileNameWithoutExtension(path) + EventSuffix + ".fbx";
            var evClip = File.Exists(evPath) ? Import(evPath, "Event", false) : null;
            var chair = GameObject.Find(Chairs[i]);
            if (clip == null || chair == null) { log.Add($"{Path.GetFileName(path)} : clip={clip != null} chaise={chair != null}"); continue; }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = "Élève " + (i + 1) + " (" + Path.GetFileNameWithoutExtension(path) + ")";
            go.transform.SetParent(root.transform, true);
            go.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            go.transform.localScale = Vector3.one;
            go.transform.position = Vector3.zero;

            // Les FBX peuvent contenir une caméra ou une lumière : on les enlève (sinon la vue du casque passe par elles).
            foreach (var c in go.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c.gameObject);
            foreach (var l in go.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
            foreach (var al in go.GetComponentsInChildren<AudioListener>(true)) Object.DestroyImmediate(al);
            var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            anim.runtimeAnimatorController = Controller(path, clip, evClip);
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = false;

            // Taille : on mesure la pose de l'animation (début du clip).
            Bounds b = PosedBounds(go, clip);
            float h = b.size.y;
            if (h > 1e-4f && (h < 1.0f || h > 1.5f)) { go.transform.localScale = Vector3.one * (SeatedHeight / h); b = PosedBounds(go, clip); }

            // Place : bassin au-dessus de l'assise, pieds au sol.
            var cb = WB(chair);
            float floorY = cb.min.y;
            var hips = go.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name.ToLowerInvariant().Contains("hips"));
            Vector3 pelvis = hips != null ? HipsPosed(go, clip, hips) : b.center;
            Vector3 seat = new Vector3(cb.center.x, 0f, cb.center.z) - fwd * 0.02f;
            go.transform.position += new Vector3(seat.x - pelvis.x, floorY - b.min.y, seat.z - pelvis.z);

            var idle = go.AddComponent<StudentIdle>();
            idle.stateName = "Idle";
            idle.eventState = evClip != null ? "Event" : "";
            log.Add($"{go.name} (caméras/lumières retirées) sur {chair.transform.parent.name}{(evClip != null ? $" + event {evClip.length:F1} s" : "")} : hauteur {PosedBounds(go, clip).size.y:F2} m, échelle {go.transform.localScale.x:F3}, clip {clip.length:F2} s");
        }
        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("[Élèves] " + string.Join(" | ", log));
        Photo();
    }

    static AnimationClip Import(string path) => Import(path, "Idle", true);

    static AnimationClip Import(string path, string clipName, bool loop)
    {
        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp == null) return null;
        bool changed = false;
        if (imp.animationType != ModelImporterAnimationType.Generic) { imp.animationType = ModelImporterAnimationType.Generic; changed = true; }
        if (imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; changed = true; }
        if (!imp.importAnimation) { imp.importAnimation = true; changed = true; }
        if (changed) imp.SaveAndReimport();
        // Textures intégrées au FBX (Mixamo) : on les sort dans un dossier pour que les matériaux les trouvent.
        string texDir = Dir + Path.GetFileNameWithoutExtension(path) + "_Textures";
        if (!AssetDatabase.IsValidFolder(texDir))
        {
            AssetDatabase.CreateFolder(Dir.TrimEnd('/'), Path.GetFileNameWithoutExtension(path) + "_Textures");
            bool ok = imp.ExtractTextures(texDir);
            AssetDatabase.Refresh();
            imp.SaveAndReimport();
            Debug.Log($"[Élèves] textures de {Path.GetFileName(path)} : {(ok ? "extraites" : "aucune")} ({AssetDatabase.FindAssets("t:Texture2D", new[] { texDir }).Length})");
        }
        var clips = imp.clipAnimations;
        if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
        if (clips != null && clips.Length > 0)
        {
            var c = clips[0];
            if (c.loopTime != loop || c.name != clipName)
            {
                c.name = clipName;
                c.loopTime = loop;
                c.loopPose = false;
                imp.clipAnimations = new[] { c };
                imp.SaveAndReimport();
            }
        }
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
    }

    static RuntimeAnimatorController Controller(string fbxPath, AnimationClip clip, AnimationClip evClip)
    {
        string path = Dir + Path.GetFileNameWithoutExtension(fbxPath).Replace(' ', '_') + ".controller";
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(path) != null) AssetDatabase.DeleteAsset(path);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(path);
        var st = ctrl.layers[0].stateMachine.AddState("Idle");
        st.motion = clip;
        ctrl.layers[0].stateMachine.defaultState = st;
        if (evClip != null)
        {
            var ev = ctrl.layers[0].stateMachine.AddState("Event");
            ev.motion = evClip;
            // À la fin de l'animation event, retour en douceur à l'idle.
            var back = ev.AddTransition(st);
            back.hasExitTime = true;
            back.exitTime = 0.92f;
            back.duration = 0.3f;
            back.hasFixedDuration = true;
        }
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    /// <summary>Bornes réelles de la pose (maillages « cuits » au début du clip).</summary>
    static Bounds PosedBounds(GameObject go, AnimationClip clip)
    {
        AnimationMode.StartAnimationMode();
        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(go, clip, 0f);
        AnimationMode.EndSampling();
        bool first = true; Bounds b = new Bounds(go.transform.position, Vector3.zero);
        foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var m = new Mesh();
            smr.BakeMesh(m, true);
            foreach (var v in m.vertices)
            {
                var p = smr.transform.TransformPoint(v);
                if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
            }
            Object.DestroyImmediate(m);
        }
        foreach (var mr in go.GetComponentsInChildren<MeshRenderer>()) { if (first) { b = mr.bounds; first = false; } else b.Encapsulate(mr.bounds); }
        AnimationMode.StopAnimationMode();
        return b;
    }

    static Vector3 HipsPosed(GameObject go, AnimationClip clip, Transform hips)
    {
        AnimationMode.StartAnimationMode();
        AnimationMode.BeginSampling();
        AnimationMode.SampleAnimationClip(go, clip, 0f);
        AnimationMode.EndSampling();
        Vector3 p = hips.position;
        AnimationMode.StopAnimationMode();
        return p;
    }

    // ---------------- Photo ----------------
    [MenuItem("Tools/Classe/3 - Photo élèves", false, 3)]
    static void Photo() => Photo(false);

    [MenuItem("Tools/Classe/4 - Photo élèves (animation event)", false, 4)]
    static void PhotoEvent() => Photo(true);

    static void Photo(bool ev)
    {
        var player = GameObject.Find("Départ partie (chaise)");
        var root = GameObject.Find(RootName);
        if (player == null || root == null) return;
        // On fige chaque élève dans sa pose (début de l'animation) le temps de la photo.
        AnimationMode.StartAnimationMode();
        AnimationMode.BeginSampling();
        foreach (var a in root.GetComponentsInChildren<Animator>())
        {
            var clipsA = a.runtimeAnimatorController != null ? a.runtimeAnimatorController.animationClips : new AnimationClip[0];
            var c = (ev ? clipsA.FirstOrDefault(x => x.name == "Event") : null) ?? clipsA.FirstOrDefault();
            if (c != null) AnimationMode.SampleAnimationClip(a.gameObject, c, c.length * 0.5f);
        }
        AnimationMode.EndSampling();
        Vector3 eye = player.transform.position + Vector3.up * 1.2f;
        Vector3 f = player.transform.forward;
        Shoot(eye, eye + f * 3f - Vector3.up * 0.4f, ev ? "eleves_joueur_event.png" : "eleves_joueur.png", 75f);
        var first = root.transform.childCount > 0 ? root.transform.GetChild(0) : null;
        foreach (Transform s in root.transform)
        {
            var b = WB(s.gameObject);
            Vector3 side = Vector3.Cross(Vector3.up, f).normalized;
            // Vue de 3/4 avant, en hauteur, côté allée centrale.
            float toCenter = Mathf.Sign(Vector3.Dot(player.transform.position - b.center, side));
            if (toCenter == 0f) toCenter = 1f;
            Shoot(b.center + f * 1.2f + side * toCenter * 0.9f + Vector3.up * 0.75f, b.center + Vector3.up * 0.1f, (ev ? "eleve_event_" : "eleve_") + s.GetSiblingIndex() + ".png", 55f);
        }
        AnimationMode.StopAnimationMode();
        Debug.Log("[Élèves] photos");
    }

    static void Shoot(Vector3 eye, Vector3 target, string file, float fov)
    {
        var go = new GameObject("PhotoCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov; cam.nearClipPlane = 0.02f;
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
