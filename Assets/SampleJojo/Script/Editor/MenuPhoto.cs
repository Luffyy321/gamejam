using System.IO;
using UnityEditor;
using UnityEngine;

public static class MenuPhoto
{
    const string Dir = "Assets/SampleJojo/Preview~/";

    [MenuItem("Tools/Jackpot/Photo du menu (face)", false, 311)]
    static void Photo()
    {
        var root = GameObject.Find("Menu principal");
        if (root == null) { Debug.LogError("[Photo] menu introuvable"); return; }
        var t = root.transform;
        var tuto = t.Find("Fiche Tutoriel");
        var param = t.Find("Fiche Paramètres");
        bool a = tuto && tuto.gameObject.activeSelf, b = param && param.gameObject.activeSelf;
        Vector3 c = t.position + t.right * 0.3f;
        if (tuto) tuto.gameObject.SetActive(true);
        if (param) param.gameObject.SetActive(false);
        Shoot(c - t.forward * 1.3f, c, t.up, "menu_face_tuto.png");
        if (tuto) tuto.gameObject.SetActive(false);
        if (param) param.gameObject.SetActive(true);
        Shoot(c - t.forward * 1.3f, c, t.up, "menu_face_param.png");
        Shoot(t.position - t.forward * 0.6f, t.position, t.up, "menu_carnet_proche.png");
        if (tuto) tuto.gameObject.SetActive(a);
        if (param) param.gameObject.SetActive(b);
        Debug.Log("[Photo] menu");
    }

    static void Shoot(Vector3 eye, Vector3 target, Vector3 up, string file)
    {
        var go = new GameObject("PhotoCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 45f;
        cam.nearClipPlane = 0.01f;
        go.transform.position = eye;
        go.transform.LookAt(target, up);
        var rt = new RenderTexture(1280, 800, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        Directory.CreateDirectory(Dir);
        File.WriteAllBytes(Dir + file, tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(go);
    }
}
