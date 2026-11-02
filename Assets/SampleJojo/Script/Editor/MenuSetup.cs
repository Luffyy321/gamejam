using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Menu : Tools > Jackpot > 31 - Menu principal (carnet).
/// Un carnet à spirale avec JOUER / PARAMÈTRES / QUITTER, une fiche « Tutoriel » et une fiche « Paramètres ».
/// Repère du menu : +X à droite, +Y en haut, -Z vers le joueur (les textes se lisent depuis le joueur).
/// </summary>
public static class MenuSetup
{
    const string Dir = "Assets/SampleJojo/model/menu/";
    const string CarnetBlend = Dir + "Carnet/Carnet.blend";
    const string IconsFbx = Dir + "Icones/ui_icon_set_1.fbx";
    const string MeshAsset = Dir + "Menu_Formes.asset";
    const string PaperTex = Dir + "Papier_ligne.png";
    const string FontCartoon = "Assets/SampleJojo/Fonts/Cartoon SDF.asset";
    const string FontCrayon = "Assets/SampleJojo/Fonts/Crayon SDF.asset";

    // Couleurs (d'après la maquette).
    static readonly Color Navy = new Color(0.11f, 0.15f, 0.33f);
    static readonly Color Cream = new Color(0.99f, 0.96f, 0.9f);
    static readonly Color Yellow = new Color(1f, 0.79f, 0.33f), YellowDark = new Color(0.93f, 0.55f, 0.16f);
    static readonly Color Blue = new Color(0.47f, 0.68f, 0.97f), BlueDark = new Color(0.24f, 0.44f, 0.85f);
    static readonly Color Red = new Color(0.94f, 0.47f, 0.47f), RedDark = new Color(0.78f, 0.3f, 0.3f);
    static readonly Color LightBlue = new Color(0.76f, 0.86f, 0.98f), PaleBlue = new Color(0.88f, 0.93f, 0.99f);
    static readonly Color Green = new Color(0.33f, 0.78f, 0.62f);
    static readonly Color DeepBlue = new Color(0.17f, 0.42f, 0.86f);

    static Dictionary<string, Mesh> meshes;
    static Dictionary<string, Material> mats;
    static TMP_FontAsset cartoon, crayon;
    static GameObject iconSet;
    static AudioClip click;

    [MenuItem("Tools/Jackpot/31 - Menu principal (carnet)", false, 142)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Menu] Quitte le mode Play avant."); return; }
        meshes = new Dictionary<string, Mesh>();
        mats = new Dictionary<string, Material>();
        LoadMeshAsset();
        cartoon = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontCartoon);
        crayon = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontCrayon);
        click = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/page_flip.wav");
        var iconPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(IconsFbx);

        // Restes d'un essai précédent.
        foreach (var leftover in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                     .Where(t => t != null && t.parent == null && t.name == "ui_icon_set_1").Select(t => t.gameObject).ToList())
            Object.DestroyImmediate(leftover);
        var old = GameObject.Find("Menu principal");
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject("Menu principal");
        Undo.RegisterCreatedObjectUndo(root, "Menu");

        iconSet = iconPrefab != null ? (GameObject)PrefabUtility.InstantiatePrefab(iconPrefab) : null;
        if (iconSet != null) { iconSet.transform.position = Vector3.zero; iconSet.transform.rotation = Quaternion.identity; iconSet.transform.localScale = Vector3.one; }

        var menu = root.AddComponent<GameMenu>();
        menu.root = root.transform;

        // ---------------- Le carnet ----------------
        var notebook = new GameObject("Carnet").transform;
        notebook.SetParent(root.transform, false);
        Vector2 page = BuildNotebook(notebook);       // largeur, hauteur de la page
        float W = page.x, H = page.y;

        // Titre écrit au crayon en haut de la page.
        Text(notebook, "Titre", "Jackpot en classe", crayon ?? cartoon, Navy * 0.9f + Color.black * 0.1f, new Vector3(0.03f, H * 0.5f - 0.075f, -0.004f), new Vector2(W * 0.8f, 0.07f), 0.5f, FontStyles.Normal);

        // Les trois boutons.
        float bx = 0.03f, bw = 0.33f, bh = 0.085f;
        Button(notebook, "Bouton JOUER", "JOUER", Yellow, YellowDark, Navy, new Vector3(bx, 0.11f, 0f), new Vector2(bw, bh), "Object098", menu, nameof(GameMenu.Jouer));
        Button(notebook, "Bouton PARAMETRES", "PARAMÈTRES", Blue, BlueDark, Navy, new Vector3(bx, -0.03f, 0f), new Vector2(bw, bh), "Object083", menu, nameof(GameMenu.Parametres));
        Button(notebook, "Bouton QUITTER", "QUITTER", Red, RedDark, Navy, new Vector3(bx, -0.17f, 0f), new Vector2(bw, bh), "Object091", menu, nameof(GameMenu.Quitter));

        // ---------------- Fiche « Tutoriel » ----------------
        var tuto = Card(root.transform, "Fiche Tutoriel", "TUTORIEL", menu, W);
        var steps = new[]
        {
            "Sors la machine du sac et pose-la sur le livre.",
            "Tire le levier, puis range les pièces gagnées dans le sac.",
            "Évite la prof et atteins l'objectif avant la fin du chrono !",
        };
        Box(tuto, "Encadré", new Vector3(0f, -0.005f, -0.004f), new Vector3(0.43f, 0.215f, 0.004f), 0.025f, PaleBlue, true);
        for (int i = 0; i < 3; i++)
        {
            float y = 0.065f - i * 0.068f;
            Box(tuto, "Pastille " + (i + 1), new Vector3(-0.17f, y, -0.008f), new Vector3(0.042f, 0.042f, 0.006f), 0.021f, DeepBlue, true);
            Text(tuto, "Numéro " + (i + 1), (i + 1).ToString(), cartoon, Color.white, new Vector3(-0.17f, y, -0.0115f), new Vector2(0.04f, 0.04f), 0.3f, FontStyles.Bold);
            var t = Text(tuto, "Étape " + (i + 1), steps[i], cartoon, Navy, new Vector3(0.035f, y, -0.008f), new Vector2(0.33f, 0.064f), 0.3f, FontStyles.Normal);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.enableAutoSizing = false;
            t.fontSize = 0.235f;
        }
        Button(tuto, "Bouton C'EST PARTI", "C'EST PARTI !", DeepBlue, new Color(0.1f, 0.28f, 0.65f), Color.white, new Vector3(0f, -0.155f, 0f), new Vector2(0.24f, 0.06f), "Object079", menu, nameof(GameMenu.CestParti));
        menu.tutorialPanel = tuto.gameObject;

        // ---------------- Fiche « Paramètres » ----------------
        var param = Card(root.transform, "Fiche Paramètres", "PARAMÈTRES", menu, W);
        Box(param, "Encadré", new Vector3(0f, 0.01f, -0.004f), new Vector3(0.43f, 0.12f, 0.004f), 0.025f, PaleBlue, true);
        Icon(param, "Icône volume", "Object092", new Vector3(-0.17f, 0.01f, -0.01f), 0.045f, Navy);
        var vl = Text(param, "Libellé volume", "Volume", cartoon, Navy, new Vector3(-0.07f, 0.01f, -0.008f), new Vector2(0.13f, 0.05f), 0.3f, FontStyles.Bold);
        vl.alignment = TextAlignmentOptions.MidlineLeft;
        Button(param, "Bouton volume moins", "-", Blue, BlueDark, Navy, new Vector3(0.04f, 0.01f, 0f), new Vector2(0.055f, 0.055f), null, menu, nameof(GameMenu.VolumeMoins));
        menu.volumeText = Text(param, "Valeur volume", "100 %", cartoon, Navy, new Vector3(0.105f, 0.01f, -0.008f), new Vector2(0.07f, 0.045f), 0.3f, FontStyles.Bold);
        Button(param, "Bouton volume plus", "+", Blue, BlueDark, Navy, new Vector3(0.17f, 0.01f, 0f), new Vector2(0.055f, 0.055f), null, menu, nameof(GameMenu.VolumePlus));
        Button(param, "Bouton RETOUR", "RETOUR", DeepBlue, new Color(0.1f, 0.28f, 0.65f), Color.white, new Vector3(0f, -0.155f, 0f), new Vector2(0.24f, 0.06f), "Object090", menu, nameof(GameMenu.FermerPanneau));
        menu.settingsPanel = param.gameObject;

        if (iconSet != null) Object.DestroyImmediate(iconSet);

        // Placement pour l'éditeur : devant la position de départ du joueur (en jeu, il se place devant les yeux).
        var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        Vector3 basePos = origin != null ? origin.transform.position : Vector3.zero;
        Vector3 fwd = origin != null ? Vector3.ProjectOnPlane(origin.transform.forward, Vector3.up).normalized : Vector3.forward;
        root.transform.position = basePos + fwd * 0.8f + Vector3.up * 1.45f;
        root.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up) * Quaternion.Euler(8f, 0f, 0f);

        menu.countdown = Object.FindFirstObjectByType<ClassroomCountdown>();
        menu.prof = Object.FindFirstObjectByType<ProfAI>();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log("[Menu] Menu principal créé : carnet JOUER / PARAMÈTRES / QUITTER + fiches Tutoriel et Paramètres.");
    }

    // ---------------- Carnet ----------------
    static Vector2 BuildNotebook(Transform parent)
    {
        float scale = 2.2f;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CarnetBlend);
        GameObject model = null;
        if (prefab != null)
        {
            model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.name = "Carnet (modèle)";
            model.transform.SetParent(parent, false);
            // Page à plat (normale +Y), spirale le long de X : on la met debout, spirale à gauche.
            model.transform.localRotation = Quaternion.LookRotation(Vector3.right, Vector3.back);
            model.transform.localScale = Vector3.one * scale;
            var paper = LitMat("Menu_Papier", Cream, 0.1f, 0f);
            var spiral = LitMat("Menu_Spirale", new Color(0.18f, 0.27f, 0.62f), 0.6f, 0.7f);
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
            {
                r.sharedMaterial = r.name.Contains("spiral") ? spiral : paper;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        }

        // Page : centrée sur l'origine, la face avant en z = 0.
        Bounds pb = LocalBounds(parent, model != null ? model.GetComponentsInChildren<MeshRenderer>().Where(r => !r.name.Contains("spiral")) : null);
        float W = pb.size.x > 0.05f ? pb.size.x : 0.46f, H = pb.size.y > 0.05f ? pb.size.y : 0.65f;
        if (model != null) model.transform.localPosition -= new Vector3(pb.center.x, pb.center.y, pb.min.z);

        // Feuille lignée (texture) juste devant la première page.
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(PaperTex);
        var sheet = new GameObject("Feuille lignée");
        sheet.transform.SetParent(parent, false);
        sheet.transform.localPosition = new Vector3(0f, 0f, -0.0015f);
        sheet.AddComponent<MeshFilter>().sharedMesh = QuadMesh(W, H);
        var sr = sheet.AddComponent<MeshRenderer>();
        sr.sharedMaterial = TexMat("Menu_Feuille", tex);
        sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // Couverture bleue derrière, et deux onglets.
        Box(parent, "Couverture", new Vector3(0.006f, -0.006f, 0.014f), new Vector3(W + 0.035f, H + 0.035f, 0.012f), 0.02f, new Color(0.2f, 0.36f, 0.8f), false);
        Box(parent, "Onglet jaune", new Vector3(W * 0.5f + 0.02f, H * 0.28f, 0.006f), new Vector3(0.06f, 0.08f, 0.006f), 0.01f, Yellow, false);
        Box(parent, "Onglet vert", new Vector3(W * 0.5f + 0.02f, H * 0.12f, 0.006f), new Vector3(0.06f, 0.08f, 0.006f), 0.01f, Green, false);
        return new Vector2(W, H);
    }

    static Bounds LocalBounds(Transform space, IEnumerable<MeshRenderer> rs)
    {
        bool first = true; Bounds b = default;
        if (rs == null) return b;
        foreach (var r in rs)
        {
            var mf = r.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) continue;
            var mb = mf.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 p = space.InverseTransformPoint(r.transform.TransformPoint(c));
                if (first) { b = new Bounds(p, Vector3.zero); first = false; } else b.Encapsulate(p);
            }
        }
        return b;
    }

    // ---------------- Fiche (tutoriel / paramètres) ----------------
    static Transform Card(Transform root, string name, string title, GameMenu menu, float notebookW)
    {
        var card = new GameObject(name).transform;
        card.SetParent(root, false);
        card.localPosition = new Vector3(notebookW * 0.5f + 0.3f, 0.02f, -0.04f);
        card.localRotation = Quaternion.Euler(0f, 18f, -2.5f);       // tournée vers le joueur, un peu de travers
        Vector2 size = new Vector2(0.5f, 0.42f);
        Box(card, "Ombre", new Vector3(0.014f, -0.014f, 0.012f), new Vector3(size.x, size.y, 0.008f), 0.03f, BlueDark, true);
        Box(card, "Contour", new Vector3(0f, 0f, 0.006f), new Vector3(size.x + 0.01f, size.y + 0.01f, 0.006f), 0.034f, Navy, true);
        Box(card, "Fond", Vector3.zero, new Vector3(size.x, size.y, 0.006f), 0.03f, Cream, true);
        Box(card, "Bandeau titre", new Vector3(0f, 0.155f, -0.005f), new Vector3(0.25f, 0.06f, 0.004f), 0.022f, LightBlue, true);
        Text(card, "Titre", title, cartoon, Navy, new Vector3(0f, 0.155f, -0.008f), new Vector2(0.23f, 0.05f), 0.36f, FontStyles.Bold);
        // Croix pour fermer.
        Button(card, "Bouton fermer", "", Red, RedDark, Navy, new Vector3(0.205f, 0.165f, 0f), new Vector2(0.05f, 0.05f), "Object075", menu, nameof(GameMenu.FermerPanneau), 0.03f);
        return card;
    }

    // ---------------- Bouton ----------------
    static void Button(Transform parent, string name, string label, Color face, Color edge, Color textColor, Vector3 pos, Vector2 size,
        string icon, GameMenu menu, string method, float iconSize = 0f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var visual = new GameObject("Visuel").transform;
        visual.SetParent(go.transform, false);
        float d = 0.018f;
        Box(visual, "Bord", new Vector3(0f, -0.007f, 0.004f - d * 0.5f), new Vector3(size.x, size.y, d), Mathf.Min(size.x, size.y) * 0.3f, edge, true);
        Box(visual, "Face", new Vector3(0f, 0f, -d * 0.5f - 0.002f), new Vector3(size.x, size.y, d), Mathf.Min(size.x, size.y) * 0.3f, face, true);
        float front = -d - 0.0035f;
        bool hasLabel = !string.IsNullOrEmpty(label);
        if (!string.IsNullOrEmpty(icon))
        {
            float s = iconSize > 0f ? iconSize : size.y * 0.55f;
            float ix = hasLabel ? -size.x * 0.5f + size.y * 0.55f : 0f;
            Icon(visual, "Icône", icon, new Vector3(ix, 0f, front), s, textColor);
        }
        if (hasLabel)
        {
            float shift = !string.IsNullOrEmpty(icon) ? size.y * 0.3f : 0f;
            float tw = !string.IsNullOrEmpty(icon) ? size.x - size.y * 0.9f - shift : size.x * 0.85f;
            Text(visual, "Texte", label, cartoon, textColor, new Vector3(shift, 0f, front), new Vector2(tw, size.y * 0.75f), 0.5f, FontStyles.Bold);
        }
        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0f, -d * 0.5f);
        col.size = new Vector3(size.x, size.y, d + 0.01f);
        go.AddComponent<XRSimpleInteractable>();
        var b = go.AddComponent<MenuButton>();
        b.visual = visual;
        if (b.onClick == null) b.onClick = new UnityEngine.Events.UnityEvent();
        b.clickClip = click;
        var action = System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), menu, method) as UnityEngine.Events.UnityAction;
        UnityEventTools.AddPersistentListener(b.onClick, action);
    }

    // ---------------- Icône (tirée du lot d'icônes 3D) ----------------
    static void Icon(Transform parent, string name, string objectName, Vector3 pos, float size, Color color)
    {
        if (iconSet == null) return;
        var src = iconSet.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == objectName);
        if (src == null) { Debug.LogWarning("[Menu] icône introuvable : " + objectName); return; }
        var holder = new GameObject(name).transform;
        holder.SetParent(parent, false);
        holder.localPosition = pos;
        // Les icônes regardent +Z dans le lot : on les retourne vers le joueur (-Z).
        holder.localRotation = Quaternion.Euler(0f, 180f, 0f);
        var copy = new GameObject("Forme");
        copy.transform.SetParent(holder, false);
        copy.AddComponent<MeshFilter>().sharedMesh = src.GetComponent<MeshFilter>().sharedMesh;
        var r = copy.AddComponent<MeshRenderer>();
        r.sharedMaterial = UnlitMat(color);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        // Même orientation/échelle que dans le lot, puis centrée et mise à la taille voulue.
        copy.transform.localRotation = src.transform.rotation;
        copy.transform.localScale = src.transform.lossyScale;
        Bounds b = LocalBounds(holder, new[] { r });
        float s = size / Mathf.Max(1e-5f, Mathf.Max(b.size.x, b.size.y));
        copy.transform.localScale *= s;
        b = LocalBounds(holder, new[] { r });
        copy.transform.localPosition -= new Vector3(b.center.x, b.center.y, b.max.z);
    }

    // ---------------- Texte ----------------
    static TextMeshPro Text(Transform parent, string name, string text, TMP_FontAsset font, Color color, Vector3 pos, Vector2 size, float maxSize, FontStyles style)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var t = go.AddComponent<TextMeshPro>();
        if (font != null) t.font = font;
        t.text = text;
        t.color = color;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.enableAutoSizing = true;
        t.fontSizeMin = 0.05f;
        t.fontSizeMax = maxSize;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.rectTransform.sizeDelta = size;
        go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return t;
    }

    // ---------------- Formes arrondies ----------------
    static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, float radius, Color color, bool unlit)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.AddComponent<MeshFilter>().sharedMesh = RoundedBox(size.x, size.y, size.z, radius);
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = unlit ? UnlitMat(color) : LitMat("Menu_" + ColorUtility.ToHtmlStringRGB(color), color, 0.25f, 0f);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    static void LoadMeshAsset()
    {
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(MeshAsset))
            if (o is Mesh m) meshes[m.name] = m;
    }

    static void SaveMesh(Mesh m)
    {
        if (AssetDatabase.LoadMainAssetAtPath(MeshAsset) == null) AssetDatabase.CreateAsset(m, MeshAsset);
        else AssetDatabase.AddObjectToAsset(m, MeshAsset);
    }

    /// <summary>Pavé aux coins arrondis (vu de face), centré, face avant en -Z.</summary>
    static Mesh RoundedBox(float w, float h, float d, float r)
    {
        string key = $"Arrondi_{w:F3}x{h:F3}x{d:F3}_r{r:F3}";
        if (meshes.TryGetValue(key, out var cached)) return cached;
        r = Mathf.Clamp(r, 0.0005f, Mathf.Min(w, h) * 0.5f);
        const int seg = 8;
        var outline = new List<Vector2>();
        Vector2[] centers = { new Vector2(w / 2 - r, h / 2 - r), new Vector2(-w / 2 + r, h / 2 - r), new Vector2(-w / 2 + r, -h / 2 + r), new Vector2(w / 2 - r, -h / 2 + r) };
        for (int c = 0; c < 4; c++)
            for (int i = 0; i <= seg; i++)
            {
                float a = (c * 90f + i * 90f / seg) * Mathf.Deg2Rad;
                outline.Add(centers[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
        int n = outline.Count;
        var v = new List<Vector3>(); var nr = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
        float z0 = -d / 2, z1 = d / 2;
        // Face avant (-Z) et arrière (+Z) en éventail.
        int fc = v.Count; v.Add(new Vector3(0, 0, z0)); nr.Add(Vector3.back); uv.Add(new Vector2(0.5f, 0.5f));
        foreach (var p in outline) { v.Add(new Vector3(p.x, p.y, z0)); nr.Add(Vector3.back); uv.Add(new Vector2(p.x / w + 0.5f, p.y / h + 0.5f)); }
        for (int i = 0; i < n; i++) t.AddRange(new[] { fc, fc + 1 + (i + 1) % n, fc + 1 + i });
        int bc = v.Count; v.Add(new Vector3(0, 0, z1)); nr.Add(Vector3.forward); uv.Add(new Vector2(0.5f, 0.5f));
        foreach (var p in outline) { v.Add(new Vector3(p.x, p.y, z1)); nr.Add(Vector3.forward); uv.Add(new Vector2(p.x / w + 0.5f, p.y / h + 0.5f)); }
        for (int i = 0; i < n; i++) t.AddRange(new[] { bc, bc + 1 + i, bc + 1 + (i + 1) % n });
        // Tour.
        int sc = v.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 p = outline[i];
            Vector2 prev = outline[(i - 1 + n) % n], next = outline[(i + 1) % n];
            Vector2 tan = (next - prev).normalized;
            Vector3 normal = new Vector3(tan.y, -tan.x, 0f);
            v.Add(new Vector3(p.x, p.y, z0)); nr.Add(normal); uv.Add(new Vector2(i / (float)n, 0));
            v.Add(new Vector3(p.x, p.y, z1)); nr.Add(normal); uv.Add(new Vector2(i / (float)n, 1));
        }
        for (int i = 0; i < n; i++)
        {
            int a = sc + i * 2, b = sc + ((i + 1) % n) * 2;
            t.AddRange(new[] { a, b, a + 1, b, b + 1, a + 1 });
        }
        var m = new Mesh { name = key };
        m.SetVertices(v); m.SetNormals(nr); m.SetUVs(0, uv); m.SetTriangles(t, 0);
        m.RecalculateBounds();
        SaveMesh(m);
        meshes[key] = m;
        return m;
    }

    static Mesh QuadMesh(float w, float h)
    {
        string key = $"Feuille_{w:F3}x{h:F3}";
        if (meshes.TryGetValue(key, out var cached)) return cached;
        var m = new Mesh { name = key };
        m.vertices = new[] { new Vector3(-w / 2, -h / 2, 0), new Vector3(w / 2, -h / 2, 0), new Vector3(-w / 2, h / 2, 0), new Vector3(w / 2, h / 2, 0) };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        m.normals = Enumerable.Repeat(Vector3.back, 4).ToArray();
        m.triangles = new[] { 0, 2, 1, 1, 2, 3 };
        m.RecalculateBounds();
        SaveMesh(m);
        meshes[key] = m;
        return m;
    }

    // ---------------- Matériaux ----------------
    static Material UnlitMat(Color c)
    {
        string name = "Menu_Unie_" + ColorUtility.ToHtmlStringRGB(c);
        if (mats.TryGetValue(name, out var m)) return m;
        string path = Dir + "Materiaux/" + name + ".mat";
        EnsureFolder(Dir + "Materiaux");
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetColor("_BaseColor", c);
        EditorUtility.SetDirty(m);
        mats[name] = m;
        return m;
    }

    static Material LitMat(string name, Color c, float smooth, float metal)
    {
        if (mats.TryGetValue(name, out var m)) return m;
        string path = Dir + "Materiaux/" + name + ".mat";
        EnsureFolder(Dir + "Materiaux");
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metal);
        EditorUtility.SetDirty(m);
        mats[name] = m;
        return m;
    }

    static Material TexMat(string name, Texture2D tex)
    {
        string path = Dir + "Materiaux/" + name + ".mat";
        EnsureFolder(Dir + "Materiaux");
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int i = path.LastIndexOf('/');
        AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
    }

    // ---------------- Tests en Play ----------------
    [MenuItem("Tools/Jackpot/32 - Test menu : JOUER (en Play)", false, 143)]
    static void TestJouer() { var m = Object.FindFirstObjectByType<GameMenu>(); if (Application.isPlaying && m != null) m.Jouer(); }

    [MenuItem("Tools/Jackpot/33 - Test menu : C'EST PARTI (en Play)", false, 144)]
    static void TestGo() { var m = Object.FindFirstObjectByType<GameMenu>(); if (Application.isPlaying && m != null) m.CestParti(); }
}
