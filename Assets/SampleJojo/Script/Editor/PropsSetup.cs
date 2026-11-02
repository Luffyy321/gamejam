using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Props du bureau (Table.035) :
/// - Calculatrice fixée au fond à gauche du bureau (vu du joueur), « 1000 » écrit sur l'écran en police cartoon.
/// - Crayon à droite du livre : on peut le prendre et le lancer.
/// Menu : Tools > Jackpot > 15 - Calculatrice + crayon.
/// </summary>
public static class PropsSetup
{
    const string PropsDir = "Assets/SampleJojo/model/Props";
    const string CalcFbx = PropsDir + "/Calculator/Calculator.FBX";
    const string PencilFbx = PropsDir + "/Crayon/PENCIL.fbx";
    const string PencilTex = PropsDir + "/Crayon/Textures/PENCIL.jpg";
    const string FontDir = "Assets/SampleJojo/Fonts";
    const string TableName = "Table.035";
    public const string DeskName = "Bureau du jeu";
    const string CalcText = "1000";

    [MenuItem("Tools/Jackpot/15 - Calculatrice + crayon", false, 126)]
    public static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Props] Quitte le mode Play avant."); return; }
        var spot = GameObject.Find("Emplacement Jackpot (livre)");

        // Le nouveau bureau s'il existe, sinon l'ancienne table de la salle.
        Bounds tb;
        var desk = GameObject.Find(DeskName);
        if (desk != null) tb = WorldBounds(desk);
        else
        {
            var table = FindRenderer(TableName);
            if (table == null) { Debug.LogError("[Props] Table « " + TableName + " » introuvable."); return; }
            tb = table.bounds;
        }
        float topY = tb.max.y;
        Vector3 toPlayer = spot != null ? Flat(spot.transform.forward) : FacePlayer(tb.center);
        Vector3 back = -toPlayer;                                     // le fond du bureau
        Vector3 right = Vector3.Cross(Vector3.up, back).normalized;  // la droite du joueur
        Vector3 left = -right;
        Vector3 bookCenter = spot != null ? spot.transform.position : new Vector3(tb.center.x, topY, tb.center.z);
        float halfW = Mathf.Abs(Vector3.Dot(tb.extents, Abs(right)));
        float halfD = Mathf.Abs(Vector3.Dot(tb.extents, Abs(back)));

        var old = GameObject.Find("Props bureau");
        if (old != null) Undo.DestroyObjectImmediate(old);
        var root = new GameObject("Props bureau");
        Undo.RegisterCreatedObjectUndo(root, "Props bureau");

        // Plateau du bureau : la table n'a pas de collision, sinon le crayon passerait au travers.
        var top = new GameObject("Collision plateau");
        top.transform.SetParent(root.transform, false);
        top.transform.position = new Vector3(tb.center.x, topY - 0.015f, tb.center.z);
        var tc = top.AddComponent<BoxCollider>();
        tc.size = new Vector3(tb.size.x, 0.03f, tb.size.z);

        var font = CartoonFont();
        string calcInfo = SetupCalculator(root.transform, tb, topY, back, left, halfW, halfD, font);
        string pencilInfo = SetupPencil(root.transform, topY, bookCenter, back, right);

        EditorSceneManager.MarkSceneDirty(root.scene);
        var calcGo = root.transform.Find("Calculatrice");
        var penGo = root.transform.Find("Crayon");
        Debug.Log("[Props] OK. " + calcInfo + " " + pencilInfo +
                  $"\nTable centre={tb.center:F2} taille={tb.size:F2} | livre={bookCenter:F2} | vers joueur={toPlayer:F2} droite={right:F2}" +
                  (calcGo != null ? $" | calculatrice={WorldBounds(calcGo.gameObject).center:F2}" : "") +
                  (penGo != null ? $" | crayon={WorldBounds(penGo.gameObject).center:F2}" : ""));
    }

    // ---------------- Calculatrice ----------------
    static string SetupCalculator(Transform parent, Bounds tb, float topY, Vector3 back, Vector3 left, float halfW, float halfD, TMP_FontAsset font)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CalcFbx);
        if (prefab == null) return "Calculatrice introuvable (" + CalcFbx + ").";
        var calc = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        calc.name = "Calculatrice";
        calc.transform.SetParent(parent, false);
        calc.transform.localPosition = Vector3.zero;
        calc.transform.rotation = Quaternion.identity;
        calc.transform.localScale = Vector3.one;

        var mf = calc.GetComponentInChildren<MeshFilter>();
        var mr = mf != null ? mf.GetComponent<MeshRenderer>() : null;
        if (mf == null || mr == null) return "Calculatrice sans mesh.";
        var mesh = mf.sharedMesh;
        Bounds mb = mesh.bounds;

        // Axes du modèle : épaisseur (le plus petit) = haut, longueur (le plus grand) = vers le fond.
        int tAxis = MinAxis(mb.size), lAxis = MaxAxis(mb.size);
        Vector3 tDir = Axis(tAxis), lDir = Axis(lAxis);

        // L'écran : partie sans texture, plate, qui n'est pas le corps.
        int screenSub = FindScreen(mesh, mr.sharedMaterials, tAxis, out Bounds screenB);
        var sb = new StringBuilder("Calculatrice : ");
        if (screenSub >= 0)
        {
            float sT = Mathf.Sign(screenB.center[tAxis] - mb.center[tAxis]);
            float sL = Mathf.Sign(screenB.center[lAxis] - mb.center[lAxis]);
            if (sT != 0) tDir *= sT;      // face avec l'écran = dessus
            if (sL != 0) lDir *= sL;      // l'écran vers le fond (les touches vers le joueur)
            sb.Append("écran = sous-mesh ").Append(screenSub).Append(" (").Append(mr.sharedMaterials[screenSub].name).Append(") ");
        }
        else sb.Append("écran non trouvé ");

        Quaternion qm = mf.transform.rotation;
        Quaternion modelFrame = Quaternion.LookRotation(qm * lDir, qm * tDir);
        calc.transform.rotation = Quaternion.LookRotation(back, Vector3.up) * Quaternion.Inverse(modelFrame);

        // Taille : 16 cm de long.
        Bounds wb = WorldBounds(calc);
        float len = Mathf.Max(wb.size.x, wb.size.z);
        if (len > 1e-4f) calc.transform.localScale *= 0.16f / len;
        wb = WorldBounds(calc);
        float cHalfW = Mathf.Abs(Vector3.Dot(wb.extents, Abs(left)));
        float cHalfD = Mathf.Abs(Vector3.Dot(wb.extents, Abs(back)));

        // Au fond à gauche du bureau.
        Vector3 target = new Vector3(tb.center.x, topY, tb.center.z) + left * (halfW - cHalfW - 0.04f) + back * (halfD - cHalfD - 0.05f);
        calc.transform.position += new Vector3(target.x - wb.center.x, topY - wb.min.y + 0.001f, target.z - wb.center.z);

        // Fixée : collider statique, pas de rigidbody.
        foreach (var c in calc.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        var bc = mf.gameObject.AddComponent<BoxCollider>();
        bc.center = mb.center; bc.size = mb.size;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        // « 1000 » sur l'écran.
        Transform mt = mf.transform;
        Bounds sLocal = screenSub >= 0 ? screenB : new Bounds(mb.center + Vector3.Scale(tDir, mb.extents) * 0.99f - lDir * 0, new Vector3(mb.size.x * 0.7f, mb.size.y * 0.25f, mb.size.z * 0.7f));
        Vector3 screenTopLocal = sLocal.center;
        screenTopLocal[tAxis] = tDir[tAxis] > 0 ? sLocal.max[tAxis] : sLocal.min[tAxis];
        Vector3 screenWorld = mt.TransformPoint(screenTopLocal) + Vector3.up * 0.0012f;
        // Taille de l'écran dans le monde (largeur = côté gauche/droite, hauteur = sens fond).
        float sw = sLocal.size[3 - tAxis - lAxis] * mt.lossyScale[3 - tAxis - lAxis];
        float sh = sLocal.size[lAxis] * mt.lossyScale[lAxis];
        sw = Mathf.Abs(sw); sh = Mathf.Abs(sh);

        var txtGo = new GameObject("Ecran 1000");
        txtGo.transform.SetParent(calc.transform, true);
        var tmp = txtGo.AddComponent<TextMeshPro>();
        tmp.text = CalcText;
        if (font != null) tmp.font = font;
        tmp.color = new Color(0.08f, 0.1f, 0.08f);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.01f;
        tmp.fontSizeMax = 2f;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.rectTransform.sizeDelta = new Vector2(Mathf.Max(0.02f, sw * 0.85f), Mathf.Max(0.01f, sh * 0.75f));
        txtGo.transform.position = screenWorld;
        // Couché sur l'écran, lisible depuis la place du joueur (haut du texte vers le fond).
        txtGo.transform.rotation = Quaternion.LookRotation(Vector3.down, Flat(mt.TransformDirection(lDir)));
        // Échelle réelle de 1 (la calculatrice est très réduite : 0,005).
        txtGo.transform.localScale = Vector3.one / Mathf.Max(1e-6f, Mathf.Abs(calc.transform.lossyScale.x));
        var tr = tmp.GetComponent<MeshRenderer>();
        tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        sb.Append("taille écran ").Append((sw * 100f).ToString("0.0")).Append("x").Append((sh * 100f).ToString("0.0")).Append(" cm.");
        return sb.ToString();
    }

    /// <summary>Cherche la plaque de l'écran : sous-mesh sans texture, la plus grande en surface, hors corps.</summary>
    static int FindScreen(Mesh mesh, Material[] mats, int tAxis, out Bounds best)
    {
        best = default;
        int bestI = -1; float bestScore = 0f;
        var v = mesh.vertices;
        float bodyArea = 0f; int body = -1;
        var bounds = new Bounds[mesh.subMeshCount];
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            var idx = mesh.GetIndices(s);
            if (idx.Length == 0) continue;
            var b = new Bounds(v[idx[0]], Vector3.zero);
            foreach (var i in idx) b.Encapsulate(v[i]);
            bounds[s] = b;
            float area = Area(b, tAxis);
            if (area > bodyArea) { bodyArea = area; body = s; }
        }
        var report = new StringBuilder("[Props] Sous-meshes de la calculatrice :\n");
        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            var m = s < mats.Length ? mats[s] : null;
            bool textured = m != null && m.mainTexture != null;
            var b = bounds[s];
            report.Append(s).Append(" ").Append(m != null ? m.name : "-").Append(textured ? " [tex " + m.mainTexture.name + "]" : "").Append(" c=").Append(b.center.ToString("F2")).Append(" s=").Append(b.size.ToString("F2")).Append('\n');
            if (s == body || textured || b.size == Vector3.zero) continue;
            // Plat dans l'axe de l'épaisseur.
            float thick = b.size[tAxis];
            float area = Area(b, tAxis);
            if (thick > 0.5f * Mathf.Sqrt(area)) continue;
            if (area > bestScore) { bestScore = area; bestI = s; best = b; }
        }
        Debug.Log(report.ToString());
        return bestI;
    }

    static float Area(Bounds b, int tAxis)
    {
        float a = 1f;
        for (int k = 0; k < 3; k++) if (k != tAxis) a *= b.size[k];
        return a;
    }

    // ---------------- Crayon ----------------
    static string SetupPencil(Transform parent, float topY, Vector3 bookCenter, Vector3 back, Vector3 right)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PencilFbx);
        if (prefab == null) return "Crayon introuvable (" + PencilFbx + ").";
        var pencil = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        pencil.name = "Crayon";
        pencil.transform.SetParent(parent, false);
        pencil.transform.rotation = Quaternion.identity;
        pencil.transform.localScale = Vector3.one;

        var mf = pencil.GetComponentInChildren<MeshFilter>();
        if (mf == null) return "Crayon sans mesh.";
        Bounds mb = mf.sharedMesh.bounds;
        int lAxis = MaxAxis(mb.size);
        Vector3 lDir = Axis(lAxis), uDir = Axis((lAxis + 1) % 3);
        Quaternion qm = mf.transform.rotation;
        // Couché, pointe vers le fond du bureau.
        pencil.transform.rotation = Quaternion.LookRotation(back, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(qm * lDir, qm * uDir));

        // Matériau texturé.
        var mat = LitMat("Crayon", AssetDatabase.LoadAssetAtPath<Texture2D>(PencilTex));
        foreach (var r in pencil.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            r.sharedMaterials = mats;
        }

        // 17,5 cm de long.
        Bounds wb = WorldBounds(pencil);
        float len = Mathf.Max(wb.size.x, wb.size.z);
        if (len > 1e-4f) pencil.transform.localScale *= 0.175f / len;
        wb = WorldBounds(pencil);

        // À droite du livre (vu du joueur), posé sur le bureau.
        const float bookHalfWidth = 0.2f;
        Vector3 target = bookCenter + right * (bookHalfWidth + 0.06f);
        pencil.transform.position += new Vector3(target.x - wb.center.x, topY - wb.min.y + 0.002f, target.z - wb.center.z);

        // Physique + prise en main.
        foreach (var c in pencil.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
        var bc = mf.gameObject.AddComponent<BoxCollider>();
        bc.center = mb.center; bc.size = mb.size;
        var rb = pencil.AddComponent<Rigidbody>();
        rb.mass = 0.03f;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.3f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        var grab = pencil.AddComponent<XRGrabInteractable>();
        grab.colliders.Clear();
        grab.colliders.Add(bc);
        grab.useDynamicAttach = true;
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.throwOnDetach = true;
        grab.throwVelocityScale = 1.5f;
        grab.throwAngularVelocityScale = 1f;
        grab.trackScale = false;
        pencil.AddComponent<PropRespawn>();
        return "Crayon : " + (wb.size.magnitude * 100f).ToString("0") + " cm, à droite du livre.";
    }

    // ---------------- Police cartoon ----------------
    /// <summary>Copie Comic Sans MS (police cartoon de Windows) dans le projet et en fait une police TextMeshPro.</summary>
    static TMP_FontAsset CartoonFont()
    {
        string assetPath = FontDir + "/Cartoon SDF.asset";
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
        if (existing != null) return existing;

        string[] candidates =
        {
            "C:/Windows/Fonts/comicbd.ttf", "C:/Windows/Fonts/comic.ttf",
            "C:/Windows/Fonts/Inkfree.ttf", "C:/Windows/Fonts/segoepr.ttf",
        };
        string src = candidates.FirstOrDefault(File.Exists);
        if (src == null) { Debug.LogWarning("[Props] Aucune police cartoon trouvée dans Windows : police par défaut."); return null; }

        if (!AssetDatabase.IsValidFolder(FontDir)) AssetDatabase.CreateFolder("Assets/SampleJojo", "Fonts");
        string ttf = FontDir + "/Cartoon" + Path.GetExtension(src).ToLowerInvariant();
        if (!File.Exists(ttf)) File.Copy(src, ttf);
        AssetDatabase.ImportAsset(ttf);
        var f = AssetDatabase.LoadAssetAtPath<Font>(ttf);
        if (f == null) return null;

        var fa = TMP_FontAsset.CreateFontAsset(f);
        if (fa == null) return null;
        fa.name = "Cartoon SDF";
        AssetDatabase.CreateAsset(fa, assetPath);
        if (fa.atlasTexture != null) { fa.atlasTexture.name = "Cartoon SDF Atlas"; AssetDatabase.AddObjectToAsset(fa.atlasTexture, fa); }
        if (fa.material != null) { fa.material.name = "Cartoon SDF Material"; AssetDatabase.AddObjectToAsset(fa.material, fa); }
        fa.TryAddCharacters("0123456789.,+-=x/ ");
        EditorUtility.SetDirty(fa);
        AssetDatabase.SaveAssets();
        Debug.Log("[Props] Police cartoon : " + Path.GetFileName(src));
        return fa;
    }

    // ---------------- Outils ----------------
    static Material LitMat(string name, Texture2D tex)
    {
        string path = PropsDir + "/" + name + ".mat";
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        if (tex != null) { if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex); m.mainTexture = tex; }
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.3f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Renderer FindRenderer(string name)
    {
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            if (r.name == name) return r;
        return null;
    }

    static Vector3 FacePlayer(Vector3 from)
    {
        var origin = Object.FindFirstObjectByType<Unity.XR.CoreUtils.XROrigin>();
        Vector3 player = origin != null ? origin.transform.position : from + Vector3.forward;
        Vector3 d = Flat(player - from);
        return d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
    }

    public static Bounds WorldBounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true).Where(r => r.GetComponent<TMP_Text>() == null).ToArray();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.forward; }
    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
    static int MinAxis(Vector3 s) => s.x <= s.y && s.x <= s.z ? 0 : s.y <= s.z ? 1 : 2;
    static int MaxAxis(Vector3 s) => s.x >= s.y && s.x >= s.z ? 0 : s.y >= s.z ? 1 : 2;
}
