using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu : Tools > Jackpot > 28 - Calculatrice = pièces + post-it objectif.
/// - L'écran de la calculatrice affiche le total des pièces du sac (plus de compteur au-dessus du sac).
/// - La calculatrice est tournée vers la place du joueur.
/// - Un post-it collé derrière la calculatrice, face au joueur : « Objectif du jour » écrit au crayon.
/// </summary>
public static class CalculatorSetup
{
    const string PostItFbx = "Assets/SampleJojo/model/Props/Post-it/PostIt.fbx";
    const string PostItDir = "Assets/SampleJojo/model/Props/Post-it/";
    const string FontDir = "Assets/SampleJojo/Fonts";
    const int DailyGoal = 1000;
    const float PostItSize = 0.076f;

    [MenuItem("Tools/Jackpot/28 - Calculatrice = pièces + post-it objectif", false, 139)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Calculatrice] Quitte le mode Play avant."); return; }
        var calc = GameObject.Find("Calculatrice");
        if (calc == null) { Debug.LogError("[Calculatrice] « Calculatrice » introuvable (menu 15)."); return; }
        var screen = calc.GetComponentInChildren<TextMeshPro>(true);
        if (screen == null) { Debug.LogError("[Calculatrice] texte de l'écran introuvable."); return; }
        var bag = Object.FindFirstObjectByType<JackpotBag>();
        var oldPost = calc.transform.Find("Post-it objectif");
        if (oldPost != null) Undo.DestroyObjectImmediate(oldPost.gameObject);

        // ---- Tournée vers le joueur ----
        Vector3 player = PlayerSeat(calc.transform.position);
        Bounds cb = Bounds(calc);
        Vector3 toPlayer = player - cb.center; toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 1e-4f) toPlayer = Vector3.back;
        toPlayer.Normalize();
        Vector3 back = Flat(screen.transform.up);          // le haut du texte = côté de l'écran (vers le fond)
        Undo.RecordObject(calc.transform, "Calculatrice");
        Quaternion turn = Quaternion.FromToRotation(back, -toPlayer);
        turn = Quaternion.Euler(0f, turn.eulerAngles.y, 0f);
        calc.transform.rotation = turn * calc.transform.rotation;
        calc.transform.position = cb.center + turn * (calc.transform.position - cb.center);
        cb = Bounds(calc);

        // ---- L'écran affiche les pièces ----
        Undo.RecordObject(screen, "Écran");
        screen.text = "0";
        screen.gameObject.name = "Ecran pièces";
        var disp = screen.GetComponent<CoinCounterDisplay>();
        if (disp == null) disp = Undo.AddComponent<CoinCounterDisplay>(screen.gameObject);
        disp.bag = bag;
        disp.format = "{0}";

        // Plus de compteur au-dessus du sac.
        if (bag != null)
        {
            Undo.RecordObject(bag, "Sac");
            bag.coinText = null;
            EditorUtility.SetDirty(bag);
            var counter = bag.transform.Find("Compteur pièces");
            if (counter != null) { Undo.RecordObject(counter.gameObject, "Compteur"); counter.gameObject.SetActive(false); }
        }

        // ---- Post-it ----
        string postInfo = PostIt(calc, cb, toPlayer);

        EditorSceneManager.MarkSceneDirty(calc.scene);
        Debug.Log($"[Calculatrice] écran = pièces du sac, tournée vers le joueur | {postInfo}");
    }

    static Vector3 PlayerSeat(Vector3 near)
    {
        var chair = GameObject.Find("Chaise du jeu");
        if (chair != null) return Bounds(chair).center;
        var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        return origin != null ? origin.transform.position : near + Vector3.back;
    }

    static string PostIt(GameObject calc, Bounds cb, Vector3 toPlayer)
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(PostItFbx);
        if (fbx == null) return "post-it introuvable : " + PostItFbx;

        var holder = new GameObject("Post-it objectif");
        Undo.RegisterCreatedObjectUndo(holder, "Post-it");

        var inst = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        inst.transform.position = Vector3.zero;
        // Un seul post-it (le modèle en contient deux).
        var planes = inst.GetComponentsInChildren<MeshRenderer>(true);
        for (int i = 1; i < planes.Length; i++) Object.DestroyImmediate(planes[i].gameObject);
        var paper = PaperMaterial();
        var r = inst.GetComponentInChildren<MeshRenderer>();
        r.sharedMaterial = paper;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        foreach (var c in inst.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

        // Modèle à plat (normale +Y, le coin relevé vers le haut) : on le centre puis on le met à la bonne taille.
        Bounds b = r.bounds;
        inst.transform.position -= b.center;
        float s = PostItSize / Mathf.Max(b.size.x, b.size.z);
        inst.transform.localScale *= s;
        inst.transform.position *= s;
        b = r.bounds;
        float curl = b.size.y;
        inst.transform.SetParent(holder.transform, true);

        // Le texte, couché sur la partie plate du papier (dessous du relevé).
        var font = PencilFont();
        var txtGo = new GameObject("Objectif");
        txtGo.transform.SetParent(holder.transform, false);
        txtGo.transform.localPosition = new Vector3(0f, -curl * 0.5f + 0.0012f, 0f);
        txtGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var tmp = txtGo.AddComponent<TextMeshPro>();
        if (font != null) tmp.font = font;
        tmp.text = $"Objectif\ndu jour :\n<size=170%>{DailyGoal}</size>";
        tmp.color = new Color(0.2f, 0.2f, 0.23f, 0.9f);       // mine de crayon
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.01f;
        tmp.fontSizeMax = 1f;
        tmp.lineSpacing = -20f;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(PostItSize * 0.84f, PostItSize * 0.84f);
        tmp.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Debout, face au joueur : la normale (+Y du support) vers le joueur, le haut (+Z) vers le ciel,
        // un peu penché en arrière et de travers, comme un vrai post-it collé.
        Quaternion face = Quaternion.LookRotation(Vector3.up, toPlayer);
        Quaternion tilt = Quaternion.AngleAxis(-12f, Vector3.Cross(Vector3.up, toPlayer)) * Quaternion.AngleAxis(-5f, toPlayer);
        holder.transform.rotation = tilt * face;

        // Collé derrière la calculatrice (côté écran), le bas un peu caché derrière.
        Vector3 backEdge = cb.center - toPlayer * Mathf.Abs(Vector3.Dot(cb.extents, new Vector3(Mathf.Abs(toPlayer.x), 0f, Mathf.Abs(toPlayer.z))));
        Vector3 pos = new Vector3(backEdge.x, cb.max.y, backEdge.z) + Vector3.up * (PostItSize * 0.5f - 0.015f) - toPlayer * 0.004f;
        holder.transform.position = pos;
        holder.transform.SetParent(calc.transform, true);

        // Le texte est lisible depuis la place du joueur ?
        if (Vector3.Dot(txtGo.transform.forward, toPlayer) > 0f)
            txtGo.transform.Rotate(0f, 180f, 0f, Space.Self);
        if (Vector3.Dot(txtGo.transform.up, Vector3.up) < 0f)
            txtGo.transform.Rotate(0f, 0f, 180f, Space.Self);
        txtGo.transform.position += toPlayer * 0.0005f;
        txtGo.transform.localScale = Vector3.one / Mathf.Max(1e-6f, holder.transform.lossyScale.x);
        return $"post-it « Objectif du jour : {DailyGoal} »";
    }

    static Material PaperMaterial()
    {
        string path = PostItDir + "PostIt_Papier.mat";
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetColor("_BaseColor", new Color(1f, 0.9f, 0.32f));
        m.SetFloat("_Smoothness", 0.15f);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_Cull", 0f);                       // visible des deux côtés
        m.doubleSidedGI = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>Police manuscrite (Ink Free de Windows) pour l'écriture au crayon.</summary>
    static TMP_FontAsset PencilFont()
    {
        string assetPath = FontDir + "/Crayon SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;
        string[] candidates = { "C:/Windows/Fonts/Inkfree.ttf", "C:/Windows/Fonts/segoepr.ttf", "C:/Windows/Fonts/segoesc.ttf", "C:/Windows/Fonts/comic.ttf" };
        string src = candidates.FirstOrDefault(File.Exists);
        if (src == null) { Debug.LogWarning("[Calculatrice] Aucune police manuscrite trouvée : police par défaut."); return null; }
        if (!AssetDatabase.IsValidFolder(FontDir)) AssetDatabase.CreateFolder("Assets/SampleJojo", "Fonts");
        string ttf = FontDir + "/Crayon" + Path.GetExtension(src).ToLowerInvariant();
        if (!File.Exists(ttf)) File.Copy(src, ttf);
        AssetDatabase.ImportAsset(ttf);
        var f = AssetDatabase.LoadAssetAtPath<Font>(ttf);
        if (f == null) return null;
        var fa = TMP_FontAsset.CreateFontAsset(f);
        if (fa == null) return null;
        fa.name = "Crayon SDF";
        AssetDatabase.CreateAsset(fa, assetPath);
        if (fa.atlasTexture != null) { fa.atlasTexture.name = "Crayon SDF Atlas"; AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa); }
        if (fa.material != null) { fa.material.name = "Crayon SDF Material"; AssetDatabase.AddObjectToAsset(fa.material, fa); }
        fa.TryAddCharacters("Objectif du jour :0123456789 pièces!");
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        Debug.Log("[Calculatrice] Police crayon : " + Path.GetFileName(src));
        return fa;
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<MeshRenderer>().Where(x => x.GetComponent<TMP_Text>() == null).ToArray();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-6f ? v.normalized : Vector3.forward; }
}
