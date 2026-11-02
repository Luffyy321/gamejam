using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Menu 34 : nouveau casier de la boutique.
/// - Tous les objets côte à côte sur une étagère ; on vise un objet → il grossit, s'avance, et sa fiche s'affiche
///   (nom, ce qu'il fait, prix avec une pièce) — sans dire combien de dopamine.
/// - La calculatrice est posée dans le casier (étagère du haut).
/// - Un buzzer vert sur l'intérieur de la porte : on appuie → cours suivant.
/// </summary>
public static class ShopShelfSetup
{
    const string Dir = "Assets/SampleJojo/model/Props/boutique/";
    const string FontPath = "Assets/SampleJojo/Fonts/Cartoon SDF.asset";
    const float ShelfH = 0.65f, LowShelfH = 0.36f, TopShelfH = 0.95f, ButtonH = 1.05f;
    // Ordre sur les étagères : rangée du haut puis rangée du bas.
    static readonly string[] Order = { "coca", "seringue", "pilule", "cafe", "antiseche", "lunettes3d", "doflamingo" };

    static readonly (string id, string name, string desc)[] Infos =
    {
        ("coca", "Canette de sucre", "Un bon shot de sucre :\ngain de dopamine."),
        ("seringue", "Seringue", "Amplifie les gains ET les pertes\nde dopamine à la machine."),
        ("pilule", "Pilule", "Gros gain de dopamine...\nmais ta vision se resserre un moment."),
        ("antiseche", "Antisèche", "Une antisèche pour tricher\npendant l'examen."),
        ("lunettes3d", "Lunettes 3D", "Surligne les réponses du test\nsur les fiches de révision."),
        ("doflamingo", "Lunettes de Doflamingo", "Filtre rose.\nWoaaa c'est beau."),
        ("cafe", "Café gourmand", "Ralentit le temps...\nmais pas la machine à sous !"),
    };

    [MenuItem("Tools/Boutique/34 - Casier boutique : étagère + buzzer + fiches", false, 34)]
    static void Setup() => Run();

    internal static void Run()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Boutique] Quitte le mode Play avant."); return; }
        var root = GameObject.Find("Couloir boutique");
        var treasure = root != null ? root.transform.Find("Casier trésor") : null;
        var shop = treasure != null ? treasure.GetComponent<LockerShop>() : null;
        if (shop == null) { Debug.LogError("[Boutique] Casier trésor introuvable : lance d'abord Boutique > 1."); return; }
        var bodyR = treasure.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == "Корпус");
        var doorR = treasure.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == "Дверца");
        if (bodyR == null) { Debug.LogError("[Boutique] corps du casier introuvable"); return; }

        // Porte fermée pour mesurer.
        if (shop.door != null) shop.door.localRotation = Quaternion.identity;

        // ---------- Nettoyage de l'ancien agencement ----------
        foreach (Transform c in treasure.Cast<Transform>().ToArray())
            if (c.name == "Étagère" || c.name.StartsWith("Prix ") || c.name.StartsWith("Étagère boutique") || c.name == "Calculatrice (casier)")
                Undo.DestroyObjectImmediate(c.gameObject);
        foreach (var n in new[] { "Socle calculatrice", "Calculatrice (boutique)", "Bouton NIVEAU SUIVANT", "Fiche objet" })
        {
            var t = root.transform.Find(n);
            if (t != null) Undo.DestroyObjectImmediate(t.gameObject);
        }
        foreach (var ob in treasure.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Buzzer cours suivant").ToArray())
            Undo.DestroyObjectImmediate(ob.gameObject);

        // ---------- Repère du casier ----------
        Bounds bb = bodyR.bounds;
        Vector3 front = treasure.TransformDirection(Vector3.right); front.y = 0f; front.Normalize();   // vers le joueur
        Vector3 right = Vector3.Cross(Vector3.up, -front).normalized;                                  // droite du joueur
        float width = Mathf.Abs(Vector3.Dot(bb.size, right)) - 0.07f;
        float depth = Mathf.Abs(Vector3.Dot(bb.size, front)) - 0.05f;
        float floorY = bb.min.y;
        Vector3 c0 = new Vector3(bb.center.x, floorY, bb.center.z);

        var wood = Lit("Casier_Planche", new Color(0.55f, 0.36f, 0.2f), 0.3f);
        var items = shop.items.Where(i => i != null).OrderBy(i => { int k = System.Array.IndexOf(Order, i.itemId); return k < 0 ? 99 : k; }).ToArray();
        // Jusqu'à 4 objets : une étagère. Au-delà : deux rangées (étagère du milieu + étagère du bas).
        int perRow = items.Length > 4 ? Mathf.CeilToInt(items.Length / 2f) : Mathf.Max(1, items.Length);
        Plank(treasure, "Étagère boutique", c0 + Vector3.up * ShelfH, width, depth, front, wood);
        if (items.Length > perRow) Plank(treasure, "Étagère boutique (bas)", c0 + Vector3.up * LowShelfH, width, depth, front, wood);
        Plank(treasure, "Étagère boutique (haut)", c0 + Vector3.up * TopShelfH, width, depth, front, wood);

        // ---------- Objets côte à côte ----------
        for (int i = 0; i < items.Length; i++)
        {
            var it = items[i];
            int row = i / perRow, col = i % perRow;
            int inRow = row == 0 ? Mathf.Min(perRow, items.Length) : items.Length - perRow;
            float slot = width / Mathf.Max(1, inRow);
            float shelfTop = c0.y + (row == 0 ? ShelfH : LowShelfH) + 0.0075f;
            float maxH = row == 0 ? TopShelfH - ShelfH - 0.04f : ShelfH - LowShelfH - 0.04f;
            Undo.RecordObject(it.transform, "Étagère");
            Undo.RecordObject(it, "Étagère");
            var info = Infos.FirstOrDefault(x => x.id == it.itemId);
            if (info.id != null) { it.displayName = info.name; it.description = info.desc; }
            it.visual = it.transform.Find("Visuel");
            if (it.priceLabel != null) { Undo.DestroyObjectImmediate(it.priceLabel.gameObject); it.priceLabel = null; }
            it.transform.rotation = Quaternion.LookRotation(-front, Vector3.up);
            // Taille : tient dans sa case.
            var b = WB(it.gameObject);
            float maxSize = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
            float want = Mathf.Min(slot * 0.82f, 0.2f);
            if (maxSize > 1e-4f) it.transform.localScale *= want / maxSize;
            b = WB(it.gameObject);
            if (b.size.y > maxH) { it.transform.localScale *= maxH / b.size.y; b = WB(it.gameObject); }
            Vector3 target = c0 + right * (-width * 0.5f + slot * (col + 0.5f)) + front * 0.03f;
            it.transform.position += new Vector3(target.x - b.center.x, shelfTop + 0.005f - b.min.y, target.z - b.center.z);
            EditorUtility.SetDirty(it);
        }

        // ---------- Calculatrice sur l'étagère du haut ----------
        string calcInfo = Calculator(treasure, c0 + Vector3.up * (TopShelfH + 0.0075f), front);

        // ---------- Buzzer vert sur l'intérieur de la porte ----------
        var flow = Object.FindFirstObjectByType<LevelFlow>();
        string buzzInfo = doorR != null && flow != null ? Buzzer(doorR.transform, shop.door != null ? shop.door : doorR.transform.parent, front, floorY, flow) : "porte ou LevelFlow introuvable";

        // ---------- Fiche d'infos ----------
        Vector3 cardPos = c0 + front * (depth * 0.5f + 0.06f) + Vector3.up * 1.27f;    // juste devant le casier, au-dessus des objets, face à nous
        // Tournée vers le joueur debout devant le casier.
        Vector3 eye = c0 + front * (depth * 0.5f + 1.2f) + Vector3.up * 1.55f;
        Vector3 look = cardPos - eye; look.y = 0f;
        shop.infoCard = Card(root.transform, cardPos, Quaternion.LookRotation(look.normalized, Vector3.up));
        EditorUtility.SetDirty(shop);

        EditorSceneManager.MarkSceneDirty(root.scene);
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"[Boutique] Casier : {items.Length} objets sur {(items.Length > perRow ? 2 : 1)} étagère(s) (largeur {width:F2} m). {calcInfo} {buzzInfo}");
        Photo();
    }

    // ---------------- Étagère ----------------
    static GameObject Plank(Transform parent, string name, Vector3 center, float width, float depth, Vector3 front, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        Undo.RegisterCreatedObjectUndo(go, "Étagère");
        go.GetComponent<Renderer>().sharedMaterial = m;
        go.transform.SetParent(parent, true);
        go.transform.position = center;
        go.transform.rotation = Quaternion.LookRotation(front, Vector3.up);
        var ls = parent.lossyScale;
        go.transform.localScale = new Vector3(width / ls.x, 0.015f / ls.y, depth / ls.z);
        return go;
    }

    // ---------------- Calculatrice ----------------
    static string Calculator(Transform treasure, Vector3 shelfTopCenter, Vector3 front)
    {
        var src = GameObject.Find("Calculatrice");
        if (src == null) return "calculatrice introuvable.";
        var copy = Object.Instantiate(src);
        Undo.RegisterCreatedObjectUndo(copy, "Calculatrice");
        copy.name = "Calculatrice (casier)";
        copy.transform.SetParent(treasure, true);
        var screen = copy.GetComponentsInChildren<TextMeshPro>(true).FirstOrDefault(t => t.GetComponent<CoinCounterDisplay>() != null);
        if (screen != null)
        {
            // Écran au fond, touches vers le joueur.
            Vector3 back = screen.transform.up; back.y = 0f;
            if (back.sqrMagnitude > 1e-4f)
            {
                var turn = Quaternion.FromToRotation(back.normalized, -front);
                turn = Quaternion.Euler(0f, turn.eulerAngles.y, 0f);
                copy.transform.rotation = turn * copy.transform.rotation;
            }
            screen.GetComponent<CoinCounterDisplay>().bag = Object.FindFirstObjectByType<JackpotBag>();
        }
        // Un peu inclinée vers le joueur pour bien lire l'écran.
        copy.transform.rotation = Quaternion.AngleAxis(-18f, Vector3.Cross(Vector3.up, front)) * copy.transform.rotation;
        var b = WB(copy, true);
        copy.transform.position += new Vector3(shelfTopCenter.x - b.center.x, shelfTopCenter.y - b.min.y + 0.003f, shelfTopCenter.z - b.center.z);
        return "Calculatrice posée dans le casier.";
    }

    // ---------------- Buzzer ----------------
    static string Buzzer(Transform door, Transform hinge, Vector3 front, float floorY, LevelFlow flow)
    {
        // Face intérieure de la porte fermée = côté corps (le moins « avant »).
        var mf = door.GetComponent<MeshFilter>();
        var mb = mf.sharedMesh.bounds;
        float minF = float.MaxValue; Vector3 center = door.TransformPoint(mb.center);
        for (int i = 0; i < 8; i++)
        {
            var p = door.TransformPoint(mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1)));
            minF = Mathf.Min(minF, Vector3.Dot(p, front));
        }
        Vector3 pos = center + front * (minF - Vector3.Dot(center, front));
        pos.y = floorY + ButtonH;
        Vector3 normal = -front;     // vers l'intérieur du casier quand la porte est fermée

        var go = new GameObject("Buzzer cours suivant");
        Undo.RegisterCreatedObjectUndo(go, "Buzzer");
        // Enfant de la charnière (pas de la porte, qui a déjà son propre bouton « ouvrir ») : il tourne avec elle.
        go.transform.SetParent(hinge, true);
        go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(normal, Vector3.up));
        var ls = hinge.lossyScale;
        go.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);

        var dark = Lit("Buzzer_Socle", new Color(0.12f, 0.12f, 0.14f), 0.5f, 0.6f);
        var ring = Lit("Buzzer_Bague", new Color(0.95f, 0.78f, 0.25f), 0.6f, 0.8f);
        var green = Lit("Buzzer_Vert", new Color(0.2f, 0.9f, 0.3f), 0.85f);
        green.EnableKeyword("_EMISSION");
        green.SetColor("_EmissionColor", new Color(0.1f, 0.6f, 0.15f));
        green.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

        Part(go.transform, PrimitiveType.Cylinder, "Socle", new Vector3(0f, 0f, 0.012f), new Vector3(0.13f, 0.012f, 0.13f), dark);
        Part(go.transform, PrimitiveType.Cylinder, "Bague", new Vector3(0f, 0f, 0.026f), new Vector3(0.105f, 0.004f, 0.105f), ring);
        var cap = Part(go.transform, PrimitiveType.Sphere, "Bouton", new Vector3(0f, 0f, 0.03f), new Vector3(0.09f, 0.09f, 0.055f), green);
        cap.localRotation = Quaternion.identity;

        var label = new GameObject("Texte");
        label.transform.SetParent(go.transform, false);
        label.transform.localPosition = new Vector3(0f, 0.11f, 0.004f);
        label.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);     // lisible depuis l'extérieur de la porte ouverte
        var t = label.AddComponent<TextMeshPro>();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) t.font = font;
        t.text = "COURS\nSUIVANT";
        t.fontStyle = FontStyles.Bold;
        t.alignment = TextAlignmentOptions.Center;
        t.enableAutoSizing = true; t.fontSizeMin = 0.05f; t.fontSizeMax = 0.4f;
        t.color = new Color(0.2f, 0.95f, 0.35f);
        t.outlineWidth = 0.2f; t.outlineColor = new Color32(10, 40, 15, 255);
        t.rectTransform.sizeDelta = new Vector2(0.22f, 0.09f);

        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.02f, 0.03f);
        col.size = new Vector3(0.16f, 0.2f, 0.07f);
        go.AddComponent<XRSimpleInteractable>();
        var mbtn = go.AddComponent<MenuButton>();
        mbtn.visual = cap;
        mbtn.hoverScale = 1.08f;
        mbtn.hoverLift = 0.008f;         // il s'enfonce un peu quand on le vise
        mbtn.clickClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/pop.wav");
        if (mbtn.onClick == null) mbtn.onClick = new UnityEngine.Events.UnityEvent();
        UnityEventTools.AddPersistentListener(mbtn.onClick, flow.NextLevel);
        return "Buzzer vert sur la porte.";
    }

    static Transform Part(Transform parent, PrimitiveType type, string name, Vector3 localPos, Vector3 size, Material m)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        // Cylindre : axe Y → on le couche le long de Z (perpendiculaire à la porte).
        go.transform.localRotation = type == PrimitiveType.Cylinder ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
        go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = m;
        return go.transform;
    }

    // ---------------- Fiche ----------------
    static ShopInfoCard Card(Transform parent, Vector3 pos, Quaternion rot)
    {
        var go = new GameObject("Fiche objet");
        Undo.RegisterCreatedObjectUndo(go, "Fiche");
        go.transform.SetParent(parent, true);
        go.transform.SetPositionAndRotation(pos, rot);
        var card = go.AddComponent<ShopInfoCard>();
        var panel = new GameObject("Panneau").transform;
        panel.SetParent(go.transform, false);
        card.panel = panel;

        var border = Lit("Fiche_Bord", new Color(0.55f, 0.25f, 0.65f), 0.3f);
        border.EnableKeyword("_EMISSION"); border.SetColor("_EmissionColor", new Color(0.2f, 0.08f, 0.25f));
        var paper = Lit("Fiche_Papier", new Color(0.99f, 0.96f, 0.86f), 0.2f);
        paper.EnableKeyword("_EMISSION"); paper.SetColor("_EmissionColor", new Color(0.35f, 0.33f, 0.28f));
        var line = Lit("Fiche_Trait", new Color(0.25f, 0.2f, 0.3f), 0.2f);
        Part(panel, PrimitiveType.Cube, "Bord", new Vector3(0f, 0f, 0.004f), new Vector3(0.40f, 0.29f, 0.006f), border);
        Part(panel, PrimitiveType.Cube, "Papier", new Vector3(0f, 0f, 0f), new Vector3(0.38f, 0.27f, 0.006f), paper);
        Part(panel, PrimitiveType.Cube, "Trait", new Vector3(0f, 0.065f, -0.0035f), new Vector3(0.3f, 0.003f, 0.001f), line);

        var ink = new Color(0.15f, 0.12f, 0.22f);
        card.nameText = Txt(panel, "Nom", "Canette de sucre", new Vector3(0f, 0.095f, -0.004f), new Vector2(0.34f, 0.055f), 0.42f, new Color(0.45f, 0.15f, 0.55f), FontStyles.Bold);
        card.descText = Txt(panel, "Effet", "Un bon shot de sucre :\ngain de dopamine.", new Vector3(0f, 0.01f, -0.004f), new Vector2(0.34f, 0.09f), 0.22f, ink, FontStyles.Normal);
        card.priceText = Txt(panel, "Prix", "10", new Vector3(-0.03f, -0.085f, -0.004f), new Vector2(0.14f, 0.07f), 0.5f, ink, FontStyles.Bold);
        card.priceText.alignment = TextAlignmentOptions.Right;
        card.priceText.textWrappingMode = TextWrappingModes.NoWrap;

        // Pièce à côté du prix.
        var jar = Object.FindFirstObjectByType<CoinJar>(FindObjectsInactive.Include);
        var coinDef = jar != null ? jar.coinPrefabs.Where(c => c != null && c.prefab != null).OrderBy(c => c.value).FirstOrDefault() : null;
        if (coinDef != null)
        {
            var coin = (GameObject)PrefabUtility.InstantiatePrefab(coinDef.prefab);
            PrefabUtility.UnpackPrefabInstance(coin, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            coin.name = "Pièce";
            foreach (var comp in coin.GetComponentsInChildren<Component>(true).Reverse())
                if (!(comp is Transform) && !(comp is MeshFilter) && !(comp is MeshRenderer)) Object.DestroyImmediate(comp);
            coin.transform.SetParent(panel, false);
            // La face plate de la pièce vers le joueur : on essaie plusieurs rotations et on garde la plus « plate » en Z.
            Quaternion best = Quaternion.identity; float bestDepth = float.MaxValue;
            foreach (var q in new[] { Quaternion.identity, Quaternion.Euler(90f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f) })
            {
                coin.transform.localRotation = q;
                float zmin = float.MaxValue, zmax = float.MinValue;
                foreach (var f in coin.GetComponentsInChildren<MeshFilter>())
                {
                    if (f.sharedMesh == null) continue;
                    foreach (var v in f.sharedMesh.vertices)
                    {
                        float z = panel.InverseTransformPoint(f.transform.TransformPoint(v)).z;
                        zmin = Mathf.Min(zmin, z); zmax = Mathf.Max(zmax, z);
                    }
                }
                if (zmax - zmin < bestDepth) { bestDepth = zmax - zmin; best = q; }
            }
            coin.transform.localRotation = best;
            coin.transform.localScale = Vector3.one;
            // Taille : diamètre ~5,5 cm (mesuré dans le repère du panneau).
            float w = 0f;
            foreach (var f in coin.GetComponentsInChildren<MeshFilter>())
            {
                if (f.sharedMesh == null) continue;
                float xmin = float.MaxValue, xmax = float.MinValue;
                foreach (var v in f.sharedMesh.vertices) { float x = panel.InverseTransformPoint(f.transform.TransformPoint(v)).x; xmin = Mathf.Min(xmin, x); xmax = Mathf.Max(xmax, x); }
                w = Mathf.Max(w, xmax - xmin);
            }
            if (w > 1e-5f) coin.transform.localScale = Vector3.one * (0.055f / w);
            var cb = WB(coin);
            Vector3 want = panel.TransformPoint(new Vector3(0.075f, -0.085f, -0.008f));
            coin.transform.position += want - cb.center;
        }
        panel.gameObject.SetActive(false);
        return card;
    }

    static TextMeshPro Txt(Transform parent, string name, string text, Vector3 pos, Vector2 size, float fontSize, Color c, FontStyles style)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var t = go.AddComponent<TextMeshPro>();
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font != null) t.font = font;
        t.text = text;
        t.color = c;
        t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.enableAutoSizing = true; t.fontSizeMin = fontSize * 0.4f; t.fontSizeMax = fontSize;
        t.textWrappingMode = TextWrappingModes.Normal;
        t.rectTransform.sizeDelta = size;
        return t;
    }

    // ---------------- Outils ----------------
    static Material Lit(string name, Color c, float smooth, float metal = 0f)
    {
        string path = Dir + "Materiaux/" + name + ".mat";
        if (!AssetDatabase.IsValidFolder(Dir + "Materiaux")) AssetDatabase.CreateFolder(Dir.TrimEnd('/'), "Materiaux");
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metal);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Bounds WB(GameObject go, bool skipText = false)
    {
        var rs = go.GetComponentsInChildren<Renderer>().Where(r => !(r is ParticleSystemRenderer) && (!skipText || r.GetComponent<TMP_Text>() == null)).ToArray();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    // ---------------- Photos ----------------
    [MenuItem("Tools/Boutique/35 - Photo casier boutique", false, 35)]
    internal static void Photo()
    {
        var root = GameObject.Find("Couloir boutique");
        var treasure = root != null ? root.transform.Find("Casier trésor") : null;
        var shop = treasure != null ? treasure.GetComponent<LockerShop>() : null;
        if (shop == null) return;
        var bodyR = treasure.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == "Корпус");
        Bounds bb = bodyR.bounds;
        Vector3 front = treasure.TransformDirection(Vector3.right); front.y = 0f; front.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, -front).normalized;

        // Porte ouverte + fiche visible le temps des photos.
        Quaternion doorRot = shop.door != null ? shop.door.localRotation : Quaternion.identity;
        if (shop.door != null) shop.door.localRotation = Quaternion.AngleAxis(shop.openAngle, shop.hingeAxis) * doorRot;
        GameObject panel = shop.infoCard != null && shop.infoCard.panel != null ? shop.infoCard.panel.gameObject : null;
        Vector3 ps = panel != null ? panel.transform.localScale : Vector3.one;
        if (panel != null) { panel.SetActive(true); panel.transform.localScale = Vector3.one; }

        Vector3 look = new Vector3(bb.center.x, bb.min.y + 1.0f, bb.center.z);
        Shoot(look + front * 1.6f + Vector3.up * 0.6f, look, "boutique_casier.png", 60f);
        Shoot(look + front * 1.1f + Vector3.up * 0.3f - right * 0.25f, look - Vector3.up * 0.1f, "boutique_etagere.png", 55f);
        var buzz = treasure.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Buzzer cours suivant");
        if (buzz != null) Shoot(buzz.position + buzz.forward * 0.6f + Vector3.up * 0.15f, buzz.position, "boutique_buzzer.png", 50f);

        if (panel != null) { panel.transform.localScale = ps; panel.SetActive(false); }
        if (shop.door != null) shop.door.localRotation = doorRot;
        Debug.Log("[Boutique] photos casier");
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
        Directory.CreateDirectory("Assets/SampleJojo/Preview~/");
        File.WriteAllBytes("Assets/SampleJojo/Preview~/" + file, tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
    }
}
