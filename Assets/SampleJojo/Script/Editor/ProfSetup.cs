using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Met le prof dans la classe : modèle + animations (Idle / Walk / Run), points de ronde,
/// point d'exclamation et script ProfAI. Menu : Tools > Jackpot > 9 - Configurer le prof.
/// </summary>
public static class ProfSetup
{
    const string Dir = "Assets/SampleJojo/model/Prof";
    const string IdleFbx = Dir + "/Old Man Idle.fbx";
    const string WalkFbx = Dir + "/Old Man Walk.fbx";
    const string RunFbx = Dir + "/Fast Run.fbx";
    const string ControllerPath = Dir + "/Prof.controller";
    const string BodyTex = Dir + "/Textures/ScaryTeacher_BodyTexture_0.png";
    const string HeadTex = Dir + "/Textures/headTexture_1.png";
    const string AlertWav = "Assets/SampleJojo/Audio/prof_alert.wav";
    const float TargetHeight = 1.75f;

    // Ronde dans la classe du bas (x, z), entre les rangées de tables.
    static readonly Vector2[] DefaultRoute =
    {
        new Vector2(-4.2f, 2.0f),
        new Vector2(-2.5f, 2.1f),
        new Vector2(-0.8f, 2.2f),
        new Vector2(-0.8f, 4.3f),
        new Vector2(-0.9f, 6.3f),
        new Vector2(-3.0f, 6.3f),
        new Vector2(-5.5f, 6.4f),
        new Vector2(-4.3f, 4.4f),
    };

    [MenuItem("Tools/Jackpot/9 - Configurer le prof", false, 120)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Prof] Quitte le mode Play avant de configurer le prof."); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(IdleFbx) == null)
        {
            Debug.LogError("[Prof] Modèle introuvable : " + IdleFbx);
            return;
        }

        // 1) Import des FBX en Humanoid + clips en boucle, sur place.
        ConfigureImporter(IdleFbx, "Idle");
        ConfigureImporter(WalkFbx, "Walk");
        ConfigureImporter(RunFbx, "Run");
        ConfigureTexture(BodyTex);
        ConfigureTexture(HeadTex);

        var idle = LoadClip(IdleFbx);
        var walk = LoadClip(WalkFbx);
        var run = LoadClip(RunFbx);
        if (idle == null || walk == null || run == null)
        {
            Debug.LogError($"[Prof] Animations manquantes (Idle={idle != null}, Walk={walk != null}, Run={run != null}).");
            return;
        }

        // 2) Animator Controller.
        var ctrl = BuildController(idle, walk, run);

        // 3) Matériaux.
        var bodyMat = MakeMaterial("Prof_Corps", AssetDatabase.LoadAssetAtPath<Texture2D>(BodyTex));
        var headMat = MakeMaterial("Prof_Tete", AssetDatabase.LoadAssetAtPath<Texture2D>(HeadTex));
        var exMat = ExclamationMaterial();

        // 4) Le sol de la classe (même hauteur que le sac posé par terre).
        var bag = Object.FindFirstObjectByType<JackpotBag>();
        float floorY = bag != null ? bag.transform.position.y : 1.524f;

        // 5) Points de ronde (gardés s'ils existent déjà, pour garder tes réglages).
        var routeRoot = GameObject.Find("Ronde du prof");
        if (routeRoot == null)
        {
            routeRoot = new GameObject("Ronde du prof");
            Undo.RegisterCreatedObjectUndo(routeRoot, "Ronde du prof");
            for (int i = 0; i < DefaultRoute.Length; i++)
            {
                var p = new GameObject("Point " + (i + 1));
                p.transform.SetParent(routeRoot.transform, false);
                p.transform.position = new Vector3(DefaultRoute[i].x, floorY, DefaultRoute[i].y);
            }
        }
        var points = routeRoot.transform.Cast<Transform>().ToArray();

        // 6) Le prof.
        var old = GameObject.Find("Prof");
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject("Prof");
        Undo.RegisterCreatedObjectUndo(root, "Prof");
        Vector3 start = points.Length > 0 ? points[0].position : new Vector3(-3f, floorY, 4f);
        start.y = floorY;
        root.transform.position = start;
        if (points.Length > 1)
        {
            Vector3 dir = points[1].position - points[0].position; dir.y = 0f;
            if (dir.sqrMagnitude > 1e-4f) root.transform.rotation = Quaternion.LookRotation(dir);
        }

        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(IdleFbx));
        model.name = "Modele prof";
        model.transform.SetParent(root.transform, false);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        var anim = model.GetComponent<Animator>();
        if (anim == null) anim = model.AddComponent<Animator>();
        anim.runtimeAnimatorController = ctrl;
        anim.applyRootMotion = false;
        anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

        // Matériaux par nom (Head / body), ombres simples.
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                string n = mats[i] != null ? mats[i].name.ToLowerInvariant() : "";
                mats[i] = n.Contains("head") || n.Contains("face") || n.Contains("hair") ? headMat : bodyMat;
            }
            r.sharedMaterials = mats;
            if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
        }

        // Taille réaliste et pieds au sol.
        Bounds b = WorldBounds(model);
        if (b.size.y > 0.01f && (b.size.y < 1.4f || b.size.y > 2.2f))
        {
            model.transform.localScale *= TargetHeight / b.size.y;
            b = WorldBounds(model);
        }
        model.transform.position += Vector3.up * (floorY - b.min.y);
        b = WorldBounds(model);
        float height = b.size.y;

        // Point d'exclamation.
        var ex = GameObject.CreatePrimitive(PrimitiveType.Quad);
        ex.name = "Point exclamation";
        Object.DestroyImmediate(ex.GetComponent<Collider>());
        ex.transform.SetParent(root.transform, false);
        ex.transform.localPosition = Vector3.up * (height + 0.35f);
        ex.transform.localScale = new Vector3(0.2f, 0.4f, 1f);
        var exR = ex.GetComponent<MeshRenderer>();
        exR.sharedMaterial = exMat;
        exR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        exR.receiveShadows = false;
        exR.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        exR.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        exR.enabled = false;

        // Son.
        var src = root.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.minDistance = 2f;
        src.maxDistance = 25f;

        // Script.
        var ai = root.AddComponent<ProfAI>();
        ai.waypoints = points;
        ai.animator = anim;
        ai.exclamation = exR;
        ai.exclamationHeight = height + 0.35f;
        ai.eyeHeight = height * 0.92f;
        ai.audioSource = src;
        ai.alertClip = AssetDatabase.LoadAssetAtPath<AudioClip>(AlertWav);
        AssignExclamationMaterials(ai);

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[Prof] OK : prof de {height:0.00} m, {points.Length} points de ronde, sol à {floorY:0.000}. " +
                  "Déplace les objets « Ronde du prof / Point N » pour changer son parcours.");
    }

    // ---------------- Import ----------------
    static void ConfigureImporter(string path, string clipName)
    {
        var imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp == null) { Debug.LogError("[Prof] Import introuvable : " + path); return; }
        bool changed = false;
        if (imp.animationType != ModelImporterAnimationType.Human)
        {
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            changed = true;
        }
        if (!imp.importAnimation) { imp.importAnimation = true; changed = true; }
        if (changed) imp.SaveAndReimport();      // il faut l'avatar avant de régler les clips

        var clips = imp.clipAnimations;
        if (clips == null || clips.Length == 0) clips = imp.defaultClipAnimations;
        if (clips == null || clips.Length == 0) { Debug.LogError("[Prof] Aucune animation dans " + path); return; }
        var c = clips[0];
        bool need = c.name != clipName || !c.loopTime || !c.lockRootRotation || !c.lockRootHeightY || !c.lockRootPositionXZ;
        c.name = clipName;
        c.loopTime = true;
        c.loopPose = true;
        // Sur place : c'est le script qui fait avancer le prof.
        c.lockRootRotation = true;
        c.keepOriginalOrientation = true;
        c.lockRootHeightY = true;
        c.keepOriginalPositionY = true;
        c.lockRootPositionXZ = true;
        c.keepOriginalPositionXZ = true;
        if (need)
        {
            imp.clipAnimations = new[] { c };
            imp.SaveAndReimport();
        }
    }

    static void ConfigureTexture(string path)
    {
        var ti = AssetImporter.GetAtPath(path) as TextureImporter;
        if (ti == null) return;
        if (ti.maxTextureSize != 1024 || !ti.mipmapEnabled)
        {
            ti.maxTextureSize = 1024;
            ti.mipmapEnabled = true;
            ti.SaveAndReimport();
        }
    }

    static AnimationClip LoadClip(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
            .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

    static AnimatorController BuildController(AnimationClip idle, AnimationClip walk, AnimationClip run)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);
        var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var sm = ctrl.layers[0].stateMachine;
        var sIdle = sm.AddState("Idle", new Vector3(300, 0, 0));
        sIdle.motion = idle;
        var sWalk = sm.AddState("Walk", new Vector3(300, 80, 0));
        sWalk.motion = walk;
        var sRun = sm.AddState("Run", new Vector3(300, 160, 0));
        sRun.motion = run;
        sm.defaultState = sIdle;
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        return ctrl;
    }

    static Material MakeMaterial(string name, Texture2D tex)
    {
        string path = Dir + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        if (tex != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            mat.mainTexture = tex;
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.2f);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material ExclamationMaterial()
    {
        string path = Dir + "/Prof_Exclamation.mat";
        var shader = Shader.Find("SampleJojo/ProfExclamation");
        if (shader == null) { Debug.LogError("[Prof] Shader « SampleJojo/ProfExclamation » introuvable."); return null; }
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = shader;
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true);
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // ---------------- « ! » (matériaux Unlit, compatibles casque) ----------------
    static void AssignExclamationMaterials(ProfAI ai)
    {
        Undo.RecordObject(ai, "Matériaux du !");
        ai.exclamationDark = UnlitMat("Prof_Exclamation_Contour", new Color(0.12f, 0.03f, 0f), 4000);
        ai.exclamationOrange = UnlitMat("Prof_Exclamation_Orange", new Color(1f, 0.55f, 0.05f), 4001);
        ai.exclamationRed = UnlitMat("Prof_Exclamation_Rouge", new Color(0.95f, 0.06f, 0.04f), 4002);
        EditorUtility.SetDirty(ai);
    }

    static Material UnlitMat(string name, Color c, int queue)
    {
        string path = Dir + "/" + name + ".mat";
        // Dessiné par-dessus tout : le plafond de la classe est juste au-dessus de la tête du prof.
        var shader = Shader.Find("SampleJojo/OverlayUnlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetColor("_BaseColor", c);
        m.renderQueue = queue;
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    [MenuItem("Tools/Jackpot/9 bis - Mettre à jour le prof (sans le recréer)", false, 120)]
    static void UpdateProf()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Prof] Quitte le mode Play avant."); return; }
        var ai = Object.FindFirstObjectByType<ProfAI>();
        if (ai == null) { Debug.LogError("[Prof] Pas de prof dans la scène (menu 9)."); return; }
        AssignExclamationMaterials(ai);
        EditorSceneManager.MarkSceneDirty(ai.gameObject.scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[Prof] « ! » mis à jour (version compatible casque).");
    }

    // ---------------- Tests en Play ----------------
    [MenuItem("Tools/Jackpot/10 - Test : le prof me repère (en Play)", false, 121)]
    static void TestSpot()
    {
        var ai = Object.FindFirstObjectByType<ProfAI>();
        if (ai == null) { Debug.LogError("[Prof] Pas de prof dans la scène (menu 9)."); return; }
        ai.TestSpot();
        Debug.Log("[Prof] Test : elle te repère et court vers toi.");
    }

    [MenuItem("Tools/Jackpot/11 - Test : le prof reprend sa ronde (en Play)", false, 122)]
    static void TestReset()
    {
        var ai = Object.FindFirstObjectByType<ProfAI>();
        if (ai == null) return;
        ai.TestReset();
        Debug.Log("[Prof] Test : retour à la ronde.");
    }
}
