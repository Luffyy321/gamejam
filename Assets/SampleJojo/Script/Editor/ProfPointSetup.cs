using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Ajoute l'animation « pointer du doigt » à la prof (quand elle est devant nous).</summary>
public static class ProfPointSetup
{
    const string Dir = "Assets/SampleJojo/model/Prof";
    const string PointFbx = Dir + "/pointer du doigt.fbx";
    const string ControllerPath = Dir + "/Prof.controller";
    const float PointEnd = 1.6f;     // durée gardée du clip (s)

    [MenuItem("Tools/Boutique/30 - Prof : animation pointer du doigt", false, 30)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Prof] Quitte le mode Play d'abord."); return; }
        var imp = AssetImporter.GetAtPath(PointFbx) as ModelImporter;
        if (imp == null) { Debug.LogError("[Prof] Introuvable : " + PointFbx); return; }

        // Humanoid (comme les autres animations de la prof) pour qu'elle marche sur son modèle.
        if (imp.animationType != ModelImporterAnimationType.Human || !imp.importAnimation)
        {
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.importAnimation = true;
            imp.SaveAndReimport();
        }
        var clips = imp.clipAnimations;
        if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
        if (clips == null || clips.Length == 0) { Debug.LogError("[Prof] Aucune animation dans " + PointFbx); return; }
        var c = clips[0];
        c.name = "Point";
        // On garde seulement le début du clip : le moment où elle pointe du doigt.
        var src = AssetDatabase.LoadAllAssetsAtPath(PointFbx).OfType<AnimationClip>().FirstOrDefault(x => !x.name.StartsWith("__preview__"));
        float fps = src != null && src.frameRate > 0f ? src.frameRate : 30f;
        var defaults = imp.defaultClipAnimations;
        float first = defaults != null && defaults.Length > 0 ? defaults[0].firstFrame : 0f;
        c.firstFrame = first;
        c.lastFrame = first + PointEnd * fps;
        c.loopTime = false;                      // elle pointe une fois et garde la pose
        c.lockRootRotation = true; c.keepOriginalOrientation = true;
        c.lockRootHeightY = true; c.keepOriginalPositionY = true;
        c.lockRootPositionXZ = true; c.keepOriginalPositionXZ = true;
        imp.clipAnimations = new[] { c };
        imp.SaveAndReimport();

        var clip = AssetDatabase.LoadAllAssetsAtPath(PointFbx).OfType<AnimationClip>().FirstOrDefault(a => !a.name.StartsWith("__preview__"));
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (clip == null || ctrl == null) { Debug.LogError($"[Prof] clip={clip != null} controller={ctrl != null}"); return; }

        var sm = ctrl.layers[0].stateMachine;
        var st = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Point");
        if (st == null) st = sm.AddState("Point", new Vector3(300, 240, 0));
        st.motion = clip;
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();

        var prof = Object.FindFirstObjectByType<ProfAI>(FindObjectsInactive.Include);
        if (prof != null) { Undo.RecordObject(prof, "Point"); prof.pointState = "Point"; EditorUtility.SetDirty(prof); }
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[Prof] Animation « Point » ajoutée ({clip.length:F2} s).");
    }

    [MenuItem("Tools/Boutique/31 - Test : la prof pointe du doigt + photo (en Play)", false, 31)]
    static void TestPoint()
    {
        if (!Application.isPlaying) return;
        var prof = Object.FindFirstObjectByType<ProfAI>(FindObjectsInactive.Include);
        if (prof == null) { Debug.Log("[Prof] pas de prof"); return; }
        prof.gameObject.SetActive(true);
        prof.enabled = true;
        prof.TestPoint();
        t0 = EditorApplication.timeSinceStartup;
        shot = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static double t0;
    static int shot;
    static readonly float[] Times = { 0.3f, 0.6f, 0.9f, 1.3f, 1.8f, 2.5f };

    // Photos de la vue du joueur pendant l'animation.
    static void Tick()
    {
        if (!Application.isPlaying || shot >= Times.Length) { EditorApplication.update -= Tick; return; }
        if (EditorApplication.timeSinceStartup - t0 < Times[shot]) return;
        Shoot(shot);
        shot++;
    }

    static void Shoot(int n)
    {
        var cam = Camera.main;
        if (cam == null) return;
        var go = new GameObject("PhotoCam");
        var c = go.AddComponent<Camera>();
        c.fieldOfView = 75f; c.nearClipPlane = 0.02f;
        go.transform.SetPositionAndRotation(cam.transform.position, cam.transform.rotation);
        var rt = new RenderTexture(1280, 800, 24);
        c.targetTexture = rt; c.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); tex.Apply();
        RenderTexture.active = null;
        Directory.CreateDirectory("Assets/SampleJojo/Preview~/");
        File.WriteAllBytes($"Assets/SampleJojo/Preview~/prof_point_{n}.png", tex.EncodeToPNG());
        c.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
        Debug.Log("[Prof] photo pointer du doigt");
    }
    [MenuItem("Tools/Boutique/32 - Prof : planche de l'animation pointer (photos)", false, 32)]
    static void Sheet()
    {
        if (Application.isPlaying) return;
        var prof = Object.FindFirstObjectByType<ProfAI>(FindObjectsInactive.Include);
        var clip = AssetDatabase.LoadAllAssetsAtPath(PointFbx).OfType<AnimationClip>().FirstOrDefault(a => !a.name.StartsWith("__preview__"));
        if (prof == null || clip == null) { Debug.Log("[Prof] manque prof ou clip"); return; }
        var src = prof.GetComponentInChildren<Animator>(true);
        var copy = Object.Instantiate(src.gameObject);
        copy.SetActive(true);
        Vector3 basePos = new Vector3(0f, -50f, 0f);          // loin de la classe
        copy.transform.SetPositionAndRotation(basePos, Quaternion.identity);
        var anim = copy.GetComponent<Animator>();
        var light = new GameObject("PhotoLight").AddComponent<Light>();
        light.type = LightType.Directional; light.transform.rotation = Quaternion.Euler(30f, 200f, 0f); light.intensity = 1.2f;
        AnimationMode.StartAnimationMode();
        Directory.CreateDirectory("Assets/SampleJojo/Preview~/");
        int n = 0;
        for (float t = 0f; t <= clip.length + 0.01f; t += clip.length / 11f, n++)
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(copy, clip, t);
            AnimationMode.EndSampling();
            // Face (le joueur est devant elle, +Z du modèle) et côté.
            ShootAt(basePos + new Vector3(0f, 1.45f, 2.2f), basePos + Vector3.up * 1.2f, $"point_face_{n:00}.png");
            ShootAt(basePos + new Vector3(2.4f, 1.45f, 0.3f), basePos + Vector3.up * 1.1f + Vector3.forward * 0.3f, $"point_cote_{n:00}.png");
        }
        AnimationMode.StopAnimationMode();
        Object.DestroyImmediate(copy);
        Object.DestroyImmediate(light.gameObject);
        Debug.Log($"[Prof] planche : {n} poses, clip {clip.length:F2} s");
    }

    static void ShootAt(Vector3 eye, Vector3 target, string file) => ShootAt(eye, target, file, Vector3.up);

    static void ShootAt(Vector3 eye, Vector3 target, string file, Vector3 up)
    {
        var go = new GameObject("PhotoCam");
        var c = go.AddComponent<Camera>();
        c.fieldOfView = 60f; c.nearClipPlane = 0.02f;
        c.clearFlags = CameraClearFlags.SolidColor; c.backgroundColor = new Color(0.5f, 0.55f, 0.6f);
        go.transform.position = eye; go.transform.LookAt(target, up);
        var rt = new RenderTexture(320, 400, 24);
        c.targetTexture = rt; c.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(320, 400, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 320, 400), 0, 0); tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes("Assets/SampleJojo/Preview~/" + file, tex.EncodeToPNG());
        c.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
    }
}
