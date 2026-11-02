using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu Tools > Jackpot : configure le levier de la machine à sous dans la scène ouverte.
/// </summary>
public static class JackpotSetup
{
    static readonly string[] ReelNames = { "Cylinder.005", "Cylinder.008", "Cylinder.004" };
    const string LeverName = "Cylinder.007";

    [MenuItem("Tools/Jackpot/1 - Configurer le levier")]
    static void Setup()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("[Jackpot] Quitte le mode Play avant de configurer.");
            return;
        }

        Transform jackpotRoot = FindJackpotRoot();
        if (jackpotRoot == null)
        {
            Debug.LogError("[Jackpot] Objet 'Jackpot' (contenant '" + LeverName + "') introuvable dans la scène.");
            return;
        }
        int cleaned = CleanupOutside(jackpotRoot);
        if (cleaned > 0) Debug.Log("[Jackpot] Nettoyage : " + cleaned + " élément(s) ajouté(s) par erreur hors du Jackpot ont été retirés.");

        Transform lever = FindDeep(jackpotRoot, LeverName);

        if (lever.GetComponent<Collider>() == null)
        {
            var bc = Undo.AddComponent<BoxCollider>(lever.gameObject);
            bc.size *= 1.3f;
        }

        var comp = lever.GetComponent<JackpotLever>();
        if (comp == null) comp = Undo.AddComponent<JackpotLever>(lever.gameObject);

        Undo.RecordObject(comp, "Configurer le levier");
        comp.lever = lever;
        var reels = new List<Transform>();
        foreach (var n in ReelNames)
        {
            var r = FindDeep(jackpotRoot, n);
            if (r != null) reels.Add(r);
            else Debug.LogWarning("[Jackpot] Rouleau '" + n + "' introuvable.");
        }
        comp.reels = reels.ToArray();
        comp.strips = new JackpotLever.ReelStrip[ReelNames.Length];
        for (int i = 0; i < ReelNames.Length; i++)
            comp.strips[i] = new JackpotLever.ReelStrip { symbols = (string[])ReelSymbols[i].Clone() };
        comp.symbolsPerReel = ReelSymbols[0].Length;
        comp.jackpotSymbol = "mmi";
        PrefabUtility.RecordPrefabInstancePropertyModifications(comp);

        // L'animation importée de Blender ne doit pas se jouer : c'est la main qui bouge le levier.
        int off = 0;
        foreach (var an in jackpotRoot.GetComponentsInChildren<Animator>(true))
        {
            if (!an.enabled) continue;
            Undo.RecordObject(an, "Désactiver Animator");
            an.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(an);
            off++;
        }
        foreach (var an in jackpotRoot.GetComponentsInChildren<Animation>(true))
        {
            if (!an.enabled) continue;
            Undo.RecordObject(an, "Désactiver Animation");
            an.enabled = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(an);
            off++;
        }

        int sym = ApplySymbols(reels);
        int bulbs = SetupFX(jackpotRoot, comp);

        EditorSceneManager.MarkSceneDirty(lever.gameObject.scene);
        Selection.activeGameObject = lever.gameObject;
        Debug.Log("[Jackpot] OK : levier configuré sur '" + lever.name + "', " + reels.Count +
                  " rouleaux, " + off + " animation(s) importée(s) désactivée(s), symboles sur " + sym +
                  " rouleau(x), " + bulbs + " lampes qui clignotent, sons branchés.");
    }

    static Transform FindJackpotRoot()
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.parent == null && t.name == "Jackpot" && FindDeep(t, LeverName) != null) return t;
        return null;
    }

    /// <summary>Retire ce que les scripts Jackpot auraient ajouté à d'autres objets que la machine.</summary>
    static int CleanupOutside(Transform root)
    {
        int n = 0;
        foreach (var lv in Object.FindObjectsByType<JackpotLever>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (lv.transform.IsChildOf(root)) continue;
            var go = lv.gameObject;
            Undo.DestroyObjectImmediate(lv);
            n++;
            var bc = go.GetComponent<BoxCollider>();
            if (bc != null && PrefabUtility.IsAddedComponentOverride(bc))
            {
                Undo.DestroyObjectImmediate(bc);
                n++;
            }
        }
        foreach (var fx in Object.FindObjectsByType<JackpotFX>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (fx.transform.IsChildOf(root)) continue;
            var go = fx.gameObject;
            Undo.DestroyObjectImmediate(fx);
            n++;
            foreach (var a in go.GetComponents<AudioSource>())
            {
                if (PrefabUtility.IsAddedComponentOverride(a))
                {
                    Undo.DestroyObjectImmediate(a);
                    n++;
                }
            }
        }
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (r.transform.IsChildOf(root)) continue;
            bool mine = false;
            foreach (var m in r.sharedMaterials)
                if (m != null && (m.name.StartsWith("Symboles_") || m.name == "JackpotBulb")) mine = true;
            if (!mine || !PrefabUtility.IsPartOfPrefabInstance(r)) continue;
            PrefabUtility.RevertObjectOverride(r, InteractionMode.UserAction);
            n++;
        }
        return n;
    }

    // Ordre des symboles sur chaque rouleau (cran 0, 1, 2...) : correspond aux images Symboles_*.png.
    static readonly string[][] ReelSymbols =
    {
        new[] { "izly", "claude", "mmi", "crous", "couteau" },     // Cylinder.005
        new[] { "claude", "crous", "couteau", "mmi", "izly" },     // Cylinder.008
        new[] { "couteau", "izly", "crous", "mmi", "claude" },     // Cylinder.004
    };

    const string AudioFolder = "Assets/SampleJojo/Audio";
    const string BulbMatPath = "Assets/SampleJojo/model/JackpotBulb.mat";

    static int SetupFX(Transform root, JackpotLever comp)
    {
        // Matériau lumineux partagé (garantit que l'émission est incluse dans le build).
        var bulbMat = AssetDatabase.LoadAssetAtPath<Material>(BulbMatPath);
        if (bulbMat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            bulbMat = new Material(shader);
            AssetDatabase.CreateAsset(bulbMat, BulbMatPath);
        }
        if (bulbMat.HasProperty("_BaseColor")) bulbMat.SetColor("_BaseColor", new Color(1f, 0.82f, 0.25f));
        if (bulbMat.HasProperty("_Smoothness")) bulbMat.SetFloat("_Smoothness", 0.8f);
        bulbMat.EnableKeyword("_EMISSION");
        bulbMat.SetColor("_EmissionColor", new Color(1f, 0.82f, 0.25f) * 1.5f);
        bulbMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;   // pas de GI temps réel (FPS)
        EditorUtility.SetDirty(bulbMat);

        // Lampes séparées (matériau "Lights") autour de la vitre, triées dans l'ordre du tour.
        var bulbs = new List<Renderer>();
        Renderer body = null;
        int bodySlot = -1;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            int slot = -1;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && (mats[i].name == "Lights" || mats[i] == bulbMat)) slot = i;
            if (slot < 0) continue;
            if (r.transform == root) { body = r; bodySlot = slot; }
            else if (mats.Length == 1) bulbs.Add(r);
            else continue;

            Undo.RecordObject(r, "Lampes");
            mats[slot] = bulbMat;
            r.sharedMaterials = mats;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
        }
        Vector3 c = Vector3.zero;
        foreach (var b in bulbs) c += root.InverseTransformPoint(b.bounds.center);
        if (bulbs.Count > 0) c /= bulbs.Count;
        bulbs.Sort((a, b) =>
        {
            Vector3 pa = root.InverseTransformPoint(a.bounds.center) - c;
            Vector3 pb = root.InverseTransformPoint(b.bounds.center) - c;
            return Mathf.Atan2(pa.y, pa.x).CompareTo(Mathf.Atan2(pb.y, pb.x));
        });

        // Sources audio 3D sur la machine.
        var sources = new List<AudioSource>(root.GetComponents<AudioSource>());
        while (sources.Count < 2) sources.Add(Undo.AddComponent<AudioSource>(root.gameObject));
        foreach (var s in sources)
        {
            Undo.RecordObject(s, "Audio");
            s.playOnAwake = false;
            s.spatialBlend = 1f;
            s.rolloffMode = AudioRolloffMode.Logarithmic;
            s.minDistance = 1f;
            s.maxDistance = 20f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(s);
        }
        sources[1].loop = true;
        sources[1].volume = 0.6f;

        var fx = root.GetComponent<JackpotFX>();
        if (fx == null) fx = Undo.AddComponent<JackpotFX>(root.gameObject);
        Undo.RecordObject(fx, "Effets");
        fx.lever = comp;
        fx.bulbs = bulbs.ToArray();
        fx.bodyRenderer = body;
        fx.bodyLightsSlot = bodySlot;
        fx.sfx = sources[0];
        fx.loopSource = sources[1];
        fx.leverClip = LoadClip("lever_pull");
        fx.spinLoopClip = LoadClip("reel_spin_loop");
        fx.reelStopClip = LoadClip("reel_stop");
        fx.winClip = LoadClip("jackpot_win");
        fx.winDuration = fx.winClip != null ? fx.winClip.length : 4f;   // les lampes flashent toute la musique
        PrefabUtility.RecordPrefabInstancePropertyModifications(fx);
        AssetDatabase.SaveAssets();
        return bulbs.Count + (body != null ? 1 : 0);
    }

    static AudioClip LoadClip(string name)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioFolder + "/" + name + ".wav");
        if (clip == null) Debug.LogWarning("[Jackpot] Son introuvable : " + name);
        return clip;
    }

    const string SymbolFolder = "Assets/SampleJojo/model/Symboles";

    static int ApplySymbols(List<Transform> reels)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        int n = 0;
        foreach (var r in reels)
        {
            string texPath = SymbolFolder + "/Symboles_" + r.name + ".png";
            var ti = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (ti == null)
            {
                Debug.LogWarning("[Jackpot] Texture introuvable : " + texPath);
                continue;
            }
            if (ti.maxTextureSize < 4096 || ti.wrapMode != TextureWrapMode.Repeat)
            {
                ti.maxTextureSize = 4096;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.SaveAndReimport();
            }
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

            string matPath = SymbolFolder + "/Symboles_" + r.name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.55f);
            EditorUtility.SetDirty(mat);

            var mr = r.GetComponent<MeshRenderer>();
            if (mr == null) continue;
            var mats = mr.sharedMaterials;
            int slot = -1;
            for (int i = 0; i < mats.Length; i++)
                if (mats[i] != null && mats[i].name.StartsWith("Symboles")) slot = i;
            if (slot < 0) slot = mats.Length - 1;
            Undo.RecordObject(mr, "Symboles des rouleaux");
            mats[slot] = mat;
            mr.sharedMaterials = mats;
            PrefabUtility.RecordPrefabInstancePropertyModifications(mr);
            n++;
        }
        AssetDatabase.SaveAssets();
        return n;
    }

    [MenuItem("Tools/Jackpot/2 - Tester le levier (en Play)")]
    static void Test()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Jackpot] Lance d'abord le mode Play.");
            return;
        }
        var c = Object.FindFirstObjectByType<JackpotLever>();
        if (c == null) Debug.LogError("[Jackpot] Aucun JackpotLever dans la scène.");
        else c.TestPull();
    }

    [MenuItem("Tools/Jackpot/3 - Tester un jackpot (en Play)")]
    static void TestWin()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Jackpot] Lance d'abord le mode Play.");
            return;
        }
        var c = Object.FindFirstObjectByType<JackpotLever>();
        if (c == null) Debug.LogError("[Jackpot] Aucun JackpotLever dans la scène.");
        else c.TestWin();
    }

    static Transform FindDeep(Transform r, string n)
    {
        if (r.name == n) return r;
        foreach (Transform c in r)
        {
            var f = FindDeep(c, n);
            if (f != null) return f;
        }
        return null;
    }
}
