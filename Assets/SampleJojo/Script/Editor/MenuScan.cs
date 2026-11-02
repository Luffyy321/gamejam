using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Rapport + photos du carnet et des icônes (pour préparer le menu).</summary>
public static class MenuScan
{
    const string Dir = "Assets/SampleJojo/Preview~/";
    static readonly string[] Paths =
    {
        "Assets/SampleJojo/model/menu/Carnet/Carnet.blend",
        "Assets/SampleJojo/model/menu/Icones/ui_icon_set_1.fbx",
    };

    [MenuItem("Tools/Jackpot/Rapport menu (carnet + icônes)", false, 310)]
    static void Scan()
    {
        var sb = new StringBuilder();
        int k = 0;
        foreach (var p in Paths)
        {
            sb.AppendLine("=== " + p);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (prefab == null) { sb.AppendLine("  introuvable"); continue; }
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.position = new Vector3(500f + k * 50f, 500f, 500f);
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                var r = t.GetComponent<Renderer>();
                var mf = t.GetComponent<MeshFilter>();
                string mats = r ? string.Join(",", r.sharedMaterials.Select(x => x == null ? "null" : x.name + "{" + (x.mainTexture ? x.mainTexture.name : "") + (x.HasProperty("_BaseColor") ? " " + x.GetColor("_BaseColor").ToString("F2") : "") + "}")) : "";
                Vector3 c = r ? r.bounds.center - inst.transform.position : Vector3.zero;
                sb.AppendLine($"  {t.name} lrot={t.localEulerAngles:F0} lsc={t.localScale:F2} centre={c:F3} taille={(r ? r.bounds.size : Vector3.zero):F3} verts={(mf && mf.sharedMesh ? mf.sharedMesh.vertexCount : 0)} mats={mats}");
            }
            // Photos : de face (-Z), de dessus.
            var rs = inst.GetComponentsInChildren<Renderer>();
            if (rs.Length > 0)
            {
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                float d = b.extents.magnitude * 2.6f;
                Shoot(b.center + new Vector3(0, 0, -d), b.center, Vector3.up, $"preview_menu_{k}_face.png");
                Shoot(b.center + new Vector3(0, d, 0), b.center, Vector3.forward, $"preview_menu_{k}_dessus.png");
                Shoot(b.center + new Vector3(d * 0.6f, d * 0.5f, -d * 0.6f), b.center, Vector3.up, $"preview_menu_{k}_biais.png");
                // Une photo par objet pour les icônes.
                if (p.EndsWith(".fbx"))
                {
                    int i = 0;
                    foreach (var r in rs)
                    {
                        var bb = r.bounds;
                        float dd = Mathf.Max(0.001f, bb.extents.magnitude) * 2.8f;
                        Shoot(bb.center + new Vector3(0, 0, -dd), bb.center, Vector3.up, $"preview_icone_{i:00}_{r.name}.png", 256, r);
                        i++;
                    }
                }
            }
            Object.DestroyImmediate(inst);
            k++;
        }
        Directory.CreateDirectory(Dir);
        File.WriteAllText(Dir + "preview_menu.txt", sb.ToString());
        Debug.Log("[Menu] rapport + photos écrits à la racine du projet");
    }

    static void Shoot(Vector3 eye, Vector3 target, Vector3 up, string file, int size = 800, Renderer only = null)
    {
        var go = new GameObject("PhotoCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.001f;
        cam.farClipPlane = 5000f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.85f, 0.85f, 0.85f);
        go.transform.position = eye;
        go.transform.LookAt(target, up);
        var light = new GameObject("PhotoLight").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = go.transform.rotation;
        light.intensity = 1.2f;
        bool[] states = null;
        Renderer[] all = null;
        if (only != null)
        {
            all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            states = all.Select(r => r.enabled).ToArray();
            foreach (var r in all) r.enabled = r == only;
        }
        var rt = new RenderTexture(size, size, 24);
        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        Directory.CreateDirectory(Dir);
        File.WriteAllBytes(Dir + file, tex.EncodeToPNG());
        if (only != null) for (int i = 0; i < all.Length; i++) if (all[i] != null) all[i].enabled = states[i];
        cam.targetTexture = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(light.gameObject);
        Object.DestroyImmediate(go);
    }
}
