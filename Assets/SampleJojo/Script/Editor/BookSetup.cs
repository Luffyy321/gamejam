using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Menu : Tools > Boutique > 23 - Livre du niveau 1 (bananes).
/// Met les 9 pages du chapitre « Les bananes » dans le livre (page 8 = image de banane).
/// </summary>
public static class BookSetup
{
    const string ImgPath = "Assets/SampleJojo/model/menu/Banane.png";
    const string MatPath = "Assets/SampleJojo/model/menu/Materiaux/Page_Banane.mat";

    [MenuItem("Tools/Boutique/23 - Livre du niveau 1 (bananes)", false, 23)]
    static void Setup()
    {
        if (Application.isPlaying) { Debug.LogWarning("[Livre] Quitte le mode Play avant."); return; }
        var book = Object.FindFirstObjectByType<JackpotBook>(FindObjectsInactive.Include);
        if (book == null) { Debug.LogError("[Livre] Livre introuvable."); return; }

        // Image : texture avec transparence.
        var imp = AssetImporter.GetAtPath(ImgPath) as TextureImporter;
        if (imp != null && (!imp.alphaIsTransparency || imp.wrapMode != TextureWrapMode.Clamp))
        {
            imp.alphaIsTransparency = true; imp.wrapMode = TextureWrapMode.Clamp; imp.mipmapEnabled = true;
            imp.SaveAndReimport();
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(ImgPath);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(mat, MatPath); }
        mat.shader = Shader.Find("Universal Render Pipeline/Unlit");
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_AlphaClip", 0f);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.renderQueue = -1;
        mat.SetFloat("_Cull", 0f);
        EditorUtility.SetDirty(mat);

        Undo.RecordObject(book, "Livre");
        book.pages = (string[])QcmBank.BookLevel1.Clone();
        book.pageImage = mat;
        EditorUtility.SetDirty(book);
        EditorSceneManager.MarkSceneDirty(book.gameObject.scene);
        Debug.Log($"[Livre] {book.pages.Length} pages « Les bananes » + image de banane ({(tex != null ? "OK" : "image introuvable")}).");
    }

    [MenuItem("Tools/Boutique/24 - Test : page suivante (en Play)", false, 24)]
    static void TestTurn() { var b = Object.FindFirstObjectByType<JackpotBook>(); if (Application.isPlaying && b != null) b.TestTurn(1); }

    [MenuItem("Tools/Boutique/25 - Photo livre", false, 25)]
    static void Photo()
    {
        var b = Object.FindFirstObjectByType<JackpotBook>();
        if (b == null) return;
        var rs = b.GetComponentsInChildren<Renderer>();
        var bb = rs[0].bounds; foreach (var r in rs) bb.Encapsulate(r.bounds);
        var chair = GameObject.Find("Chaise du jeu");
        Vector3 back = chair != null ? (chair.transform.position - bb.center) : Vector3.left; back.y = 0f; back.Normalize();
        var go = new GameObject("PhotoCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 50f; cam.nearClipPlane = 0.02f;
        go.transform.position = bb.center + Vector3.up * 0.45f + back * 0.25f;
        go.transform.LookAt(bb.center, Vector3.up);
        var rt = new RenderTexture(1280, 800, 24);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); tex.Apply();
        RenderTexture.active = null;
        System.IO.File.WriteAllBytes("Assets/SampleJojo/Preview~/livre.png", tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
        Debug.Log("[Livre] photo");
    }
}
