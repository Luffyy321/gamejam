using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu : Tools > Boutique > 15 - Feuille de QCM (fin du 3e cours).
/// Une feuille A4 posée à la place du livre, 12 cases à cocher avec le crayon.
/// </summary>
public static class QcmSetup
{
    const string RootName = "Feuille QCM";
    const string MatDir = "Assets/SampleJojo/model/menu/Materiaux/";
    const float W = 0.21f, H = 0.297f, T = 0.0015f;

    [MenuItem("Tools/Boutique/15 - Feuille de QCM (3e cours)", false, 15)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[QCM] Quitte le mode Play avant."); return; }
        var old = GameObject.Find(RootName);
        if (old == null) old = Resources.FindObjectsOfTypeAll<QcmSheet>().Select(q => q.gameObject).FirstOrDefault(g => g.scene.IsValid());
        if (old != null) Undo.DestroyObjectImmediate(old);

        var book = Object.FindFirstObjectByType<JackpotBook>(FindObjectsInactive.Include);
        var chair = GameObject.Find("Chaise du jeu");
        Bounds bb = book != null ? Bounds(book.gameObject) : new Bounds(new Vector3(-6.4f, 2.3f, 3.6f), Vector3.one * 0.2f);
        Vector3 eye = chair != null ? chair.transform.position : bb.center - Vector3.right;
        Vector3 fwd = bb.center - eye; fwd.y = 0f; fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.right;

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, RootName);
        root.transform.position = new Vector3(bb.center.x, bb.min.y + 0.001f, bb.center.z);
        root.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);       // +Z = haut de la page (loin du joueur)
        root.transform.localScale = Vector3.one * 1.25f;                         // un peu plus grand qu'un A4 : plus lisible dans le casque

        var paper = GameObject.CreatePrimitive(PrimitiveType.Cube);
        paper.name = "Papier";
        Object.DestroyImmediate(paper.GetComponent<Collider>());
        paper.transform.SetParent(root.transform, false);
        paper.transform.localPosition = new Vector3(0f, T * 0.5f, 0f);
        paper.transform.localScale = new Vector3(W, T, H);
        paper.GetComponent<Renderer>().sharedMaterial = Mat("QCM_Papier", new Color(0.97f, 0.96f, 0.92f), 0.1f);

        float top = T + 0.0004f;
        var ink = new Color(0.12f, 0.12f, 0.16f);
        Text(root.transform, "Titre", "INTERRO SURPRISE", new Vector3(-0.03f, top, 0.132f), new Vector2(0.15f, 0.025f), 0.12f, ink, TextAlignmentOptions.Center, FontStyles.Bold);
        Text(root.transform, "Consigne", "Coche la bonne réponse à chaque question avec ton crayon.", new Vector3(0f, top, 0.116f), new Vector2(0.19f, 0.015f), 0.04f, ink, TextAlignmentOptions.Center, FontStyles.Italic);

        var lineMat = Mat("QCM_Encre", ink, 0.1f);
        var pencilMat = Mat("QCM_Mine", new Color(0.25f, 0.25f, 0.28f), 0.3f);
        var boxes = new List<Transform>(); var marks = new List<GameObject>(); var labels = new List<TMP_Text>(); var questions = new List<TMP_Text>();
        const float box = 0.013f, line = 0.001f;
        for (int qi = 0; qi < 4; qi++)
        {
            float z0 = 0.098f - qi * 0.061f;
            var qt = Text(root.transform, "Question " + (qi + 1), "<b>" + (qi + 1) + ". Question ?</b>", new Vector3(0f, top, z0), new Vector2(0.19f, 0.015f), 0.05f, ink, TextAlignmentOptions.Left, FontStyles.Normal);
            qt.textWrappingMode = TextWrappingModes.Normal;
            questions.Add(qt);
            for (int a = 0; a < 3; a++)
            {
                int i = qi * 3 + a;
                float x = -0.08f, z = z0 - 0.018f - a * 0.0165f;
                var c = new GameObject("Case " + (i + 1)).transform;
                c.SetParent(root.transform, false);
                c.localPosition = new Vector3(x, top, z);
                Bar(c, "Haut", new Vector3(0f, 0f, box * 0.5f), new Vector3(box + line, 0.0003f, line), lineMat);
                Bar(c, "Bas", new Vector3(0f, 0f, -box * 0.5f), new Vector3(box + line, 0.0003f, line), lineMat);
                Bar(c, "Gauche", new Vector3(-box * 0.5f, 0f, 0f), new Vector3(line, 0.0003f, box + line), lineMat);
                Bar(c, "Droite", new Vector3(box * 0.5f, 0f, 0f), new Vector3(line, 0.0003f, box + line), lineMat);
                var mark = new GameObject("Croix").transform;
                mark.SetParent(c, false);
                var b1 = Bar(mark, "Trait 1", new Vector3(0f, 0.0004f, 0f), new Vector3(box * 1.25f, 0.0003f, 0.002f), pencilMat);
                b1.localRotation = Quaternion.Euler(0f, 42f, 0f);
                var b2 = Bar(mark, "Trait 2", new Vector3(0f, 0.0005f, 0f), new Vector3(box * 1.2f, 0.0003f, 0.002f), pencilMat);
                b2.localRotation = Quaternion.Euler(0f, -47f, 0f);
                mark.gameObject.SetActive(false);
                // Réponse à droite de la case (texte rempli au hasard à chaque examen).
                var lab = Text(root.transform, "Réponse " + (i + 1), "réponse", new Vector3(x + 0.013f + 0.085f, top, z), new Vector2(0.17f, 0.015f), 0.046f, ink, TextAlignmentOptions.Left, FontStyles.Normal);
                boxes.Add(c); marks.Add(mark.gameObject); labels.Add(lab);
            }
        }

        var q = root.AddComponent<QcmSheet>();
        q.boxes = boxes.ToArray();
        q.marks = marks.ToArray();
        q.labels = labels.ToArray();
        q.questionTexts = questions.ToArray();
        q.answersPerQuestion = 3;
        // Note en rouge (écrite à la fin de l'examen), en haut à droite.
        var note = Text(root.transform, "Note", "0 / 4", new Vector3(0.078f, top + 0.0003f, 0.13f), new Vector2(0.05f, 0.03f), 0.12f, new Color(0.85f, 0.08f, 0.1f), TextAlignmentOptions.Center, FontStyles.Bold);
        var crayonFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/SampleJojo/Fonts/Crayon SDF.asset");
        if (crayonFont != null) note.font = crayonFont;
        note.transform.localRotation = Quaternion.Euler(90f, -12f, 0f);
        q.noteText = note;
        note.gameObject.SetActive(false);
        q.boxHalf = box * 0.5f;
        var pencil = GameObject.Find("Crayon");
        q.pencil = pencil != null ? pencil.transform : null;
        var src = root.AddComponent<AudioSource>();
        src.playOnAwake = false; src.spatialBlend = 1f;
        q.audioSource = src;
        q.scribbleClip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/SampleJojo/Audio/pencil_scribble.wav");
        root.SetActive(false);

        var flow = Object.FindFirstObjectByType<LevelFlow>();
        if (flow != null)
        {
            Undo.RecordObject(flow, "QCM");
            flow.qcm = q;
            flow.book = book;
            flow.examLevel = 3;
            EditorUtility.SetDirty(flow);
        }
        EditorSceneManager.MarkSceneDirty(root.scene);
        Debug.Log($"[QCM] Feuille créée à la place du livre ({root.transform.position:F2}), crayon {(pencil != null ? "trouvé" : "INTROUVABLE")}, " +
                  $"enchaînement {(flow != null ? "branché (fin du 3e cours)" : "LevelFlow introuvable")}.");
    }

    static Transform Bar(Transform parent, string name, Vector3 pos, Vector3 size, Material m)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = size;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }

    static TextMeshPro Text(Transform parent, string name, string text, Vector3 pos, Vector2 size, float fontSize, Color color, TextAlignmentOptions align, FontStyles style)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // à plat, lisible depuis la chaise
        var t = go.AddComponent<TextMeshPro>();
        t.text = text;
        t.fontSize = fontSize;
        t.color = color;
        t.alignment = align;
        t.fontStyle = style;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.rectTransform.sizeDelta = size;
        return t;
    }

    static Material Mat(string name, Color c, float smooth)
    {
        Directory.CreateDirectory(MatDir);
        string path = MatDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(m);
        return m;
    }

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true).Where(r => r.GetComponent<TMP_Text>() == null).ToArray();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds); return b;
    }

    // ---------------- Tests ----------------
    [MenuItem("Tools/Boutique/16 - Test : interro QCM (en Play)", false, 16)]
    static void TestExam() { var f = Object.FindFirstObjectByType<LevelFlow>(); if (Application.isPlaying && f != null) f.StartExam(); }

    [MenuItem("Tools/Boutique/17 - Test : cocher une case (en Play)", false, 17)]
    static void TestTick()
    {
        var q = Object.FindFirstObjectByType<QcmSheet>();
        if (Application.isPlaying && q != null) q.Toggle(Random.Range(0, q.boxes.Length));
    }

    [MenuItem("Tools/Boutique/18 - Photo QCM", false, 18)]
    static void Photo()
    {
        var q = Resources.FindObjectsOfTypeAll<QcmSheet>().FirstOrDefault(x => x.gameObject.scene.IsValid());
        if (q == null) return;
        bool was = q.gameObject.activeSelf;
        q.gameObject.SetActive(true);
        q.PickQuestions();
        var shown = new[] { 0, 4, 8, 10 };
        foreach (int i in shown) q.marks[i].SetActive(true);
        if (q.noteText != null) { q.noteText.text = "2 / 4"; q.noteText.gameObject.SetActive(true); }
        var book = Object.FindFirstObjectByType<JackpotBook>();
        bool bookWas = book != null && book.gameObject.activeSelf;
        if (book != null) book.gameObject.SetActive(false);
        var chair = GameObject.Find("Chaise du jeu");
        Vector3 eye = (chair != null ? chair.transform.position : q.transform.position - q.transform.forward) + Vector3.up * 1.15f;
        Shoot(eye, q.transform.position, "qcm.png", 50f);
        Shoot(q.transform.position + Vector3.up * 0.42f - q.transform.forward * 0.12f, q.transform.position, "qcm_pres.png", 50f);
        foreach (int i in shown) q.marks[i].SetActive(false);
        if (q.noteText != null) q.noteText.gameObject.SetActive(false);
        q.gameObject.SetActive(was);
        if (book != null) book.gameObject.SetActive(bookWas);
        Debug.Log("[QCM] photos");
    }

    static void Shoot(Vector3 eye, Vector3 target, string file, float fov)
    {
        var go = new GameObject("PhotoCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov; cam.nearClipPlane = 0.02f;
        go.transform.position = eye;
        Vector3 d = target - eye;
        go.transform.rotation = Quaternion.LookRotation(d, Mathf.Abs(Vector3.Dot(d.normalized, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up);
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
