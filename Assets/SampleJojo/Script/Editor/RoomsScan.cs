using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Relevé de la scène pour trouver la/les pièce(s) vide(s) à côté de la classe.</summary>
public static class RoomsScan
{
    [MenuItem("Tools/Classe/10 - Scan des salles", false, 10)]
    static void Scan()
    {
        var sb = new StringBuilder();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        sb.AppendLine("== Objets racine ==");
        foreach (var go in scene.GetRootGameObjects())
        {
            var b = Bounds(go);
            sb.AppendLine($"{go.name} actif={go.activeSelf} pos={go.transform.position:F2} rot={go.transform.eulerAngles:F0} bornes={b.center:F2} taille={b.size:F2} enfants={go.transform.childCount} renderers={go.GetComponentsInChildren<Renderer>(true).Length}");
        }
        sb.AppendLine("== Enfants directs des racines (gros objets) ==");
        foreach (var go in scene.GetRootGameObjects())
            foreach (Transform c in go.transform)
            {
                var b = Bounds(c.gameObject);
                if (b.size.magnitude < 0.8f) continue;
                sb.AppendLine($"{go.name}/{c.name} bornes={b.center:F2} taille={b.size:F2} enfants={c.childCount}");
            }
        sb.AppendLine("== Grandes surfaces (sols, murs, plafonds) près de la classe ==");
        var zone = new Bounds(new Vector3(-3f, 3f, 3f), new Vector3(60f, 14f, 60f));
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var b = r.bounds;
            if (!zone.Intersects(b)) continue;
            bool big = (b.size.x > 2.5f && b.size.z > 2.5f) || (b.size.y > 2f && Mathf.Max(b.size.x, b.size.z) > 2.5f);
            if (!big || b.size.magnitude > 80f) continue;
            sb.AppendLine($"{Path(r.transform)} centre={b.center:F2} taille={b.size:F2} actif={r.gameObject.activeInHierarchy}");
        }
        Directory.CreateDirectory("Assets/SampleJojo/Preview~/");
        File.WriteAllText("Assets/SampleJojo/Preview~/scan_salles.txt", sb.ToString());
        Debug.Log("[Salles] scan écrit dans Preview~/scan_salles.txt");
        // Vue de dessus de la zone.
        Shoot(new Vector3(-3f, 6.5f, 3f), new Vector3(-3f, 0f, 3.01f), "salles_dessus.png", 60f, true, 20f);
        Shoot(new Vector3(-3f, 4.2f, 3f), new Vector3(-3f, 0f, 3.01f), "salles_dessus_bas.png", 60f, true, 20f);
    }

    [MenuItem("Tools/Classe/11 - Photos des pièces vides", false, 11)]
    static void RoomPhotos()
    {
        Persp(new Vector3(2.6f, 3.2f, 0.9f), new Vector3(9f, 1.6f, 7.5f), "piece_A1.png");
        Persp(new Vector3(9.0f, 3.2f, 7.6f), new Vector3(2.6f, 1.6f, 0.8f), "piece_A2.png");
        Persp(new Vector3(10f, 3.2f, 0.9f), new Vector3(14.5f, 1.6f, 7.5f), "piece_B1.png");
        Persp(new Vector3(14.2f, 3.2f, 7.6f), new Vector3(9.8f, 1.6f, 0.9f), "piece_B2.png");
        Persp(new Vector3(5.6f, 3.2f, 8.6f), new Vector3(13f, 1.6f, 12.4f), "piece_C1.png");
        Persp(new Vector3(-7f, 3.2f, 0.7f), new Vector3(2f, 1.6f, 7.8f), "piece_classe.png");
        Persp(new Vector3(-4.0f, 9.5f, 1.9f), new Vector3(4f, 8.0f, 7.4f), "etage_1.png");
        Persp(new Vector3(4.0f, 9.5f, 7.4f), new Vector3(-4f, 8.0f, 1.9f), "etage_2.png");
        Persp(new Vector3(0.2f, 9.6f, 4.6f), new Vector3(-4.4f, 8.6f, 4.6f), "etage_3.png");
        Persp(new Vector3(0.2f, 9.6f, 4.6f), new Vector3(4.4f, 8.6f, 4.6f), "etage_4.png");
        Debug.Log("[Salles] photos des pièces");
    }

    [MenuItem("Tools/Classe/12 - Inventaire pièces A-B", false, 12)]
    static void Inventory()
    {
        var sb = new StringBuilder();
        var boxes = new (string, Bounds)[] {
            ("A", new Bounds(new Vector3(5.8f, 2.6f, 4.2f), new Vector3(7.6f, 2.6f, 8.4f))),
            ("B", new Bounds(new Vector3(12.2f, 2.6f, 4.3f), new Vector3(6.0f, 2.6f, 8.2f))),
            ("C", new Bounds(new Vector3(9.3f, 2.6f, 10.5f), new Vector3(8.6f, 2.6f, 4.8f))),
        };
        foreach (var (name, box) in boxes)
        {
            sb.AppendLine("== Pièce " + name + " " + box.min.ToString("F1") + " → " + box.max.ToString("F1"));
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var b = r.bounds;
                if (!box.Intersects(b) || b.size.magnitude > 40f) continue;
                sb.AppendLine($"  {Path(r.transform)} centre={b.center:F2} taille={b.size:F2}");
            }
            foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c.GetComponent<Renderer>() != null) continue;
                var b = c.bounds;
                if (!box.Intersects(b) || b.size.magnitude > 40f) continue;
                sb.AppendLine($"  [collider seul] {Path(c.transform)} centre={b.center:F2} taille={b.size:F2}");
            }
        }
        File.WriteAllText("Assets/SampleJojo/Preview~/pieces.txt", sb.ToString());
        Debug.Log("[Salles] inventaire écrit");
    }

    [MenuItem("Tools/Classe/13 - Scan étage", false, 13)]
    static void ScanUpstairs()
    {
        var sb = new StringBuilder();
        foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var b = r.bounds;
            if (b.min.y < 3.3f || b.min.y > 8.5f || b.size.magnitude > 60f) continue;
            if (b.size.magnitude < 1.2f) continue;
            sb.AppendLine($"{Path(r.transform)} centre={b.center:F2} taille={b.size:F2}");
        }
        File.WriteAllText("Assets/SampleJojo/Preview~/etage.txt", sb.ToString());
        Shoot(new Vector3(-1f, 7.3f, 0f), new Vector3(-1f, 0f, 0.01f), "etage_dessus.png", 60f, true, 20f);
        Shoot(new Vector3(-1f, 3.55f, 0f), new Vector3(-1f, 0f, 0.01f), "rdc_dessus.png", 60f, true, 20f);
        Debug.Log("[Salles] étage scanné");
    }

    static void Persp(Vector3 eye, Vector3 target, string file)
    {
        var go = new GameObject("PhotoCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = 80f; cam.nearClipPlane = 0.05f; cam.farClipPlane = 200f;
        go.transform.position = eye; go.transform.LookAt(target, Vector3.up);
        var rt = new RenderTexture(1280, 800, 24);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes("Assets/SampleJojo/Preview~/" + file, tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
    }

    static string Path(Transform t) => t.parent == null ? t.name : Path(t.parent) + "/" + t.name;

    static Bounds Bounds(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
        if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
        var b = rs[0].bounds;
        foreach (var r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    public static void Shoot(Vector3 eye, Vector3 target, string file, float fov, bool ortho = false, float size = 15f)
    {
        var go = new GameObject("PhotoCam");
        var cam = go.AddComponent<Camera>();
        cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 200f;
        cam.orthographic = ortho; cam.orthographicSize = size;
        go.transform.position = eye; go.transform.LookAt(target, Vector3.forward);
        var rt = new RenderTexture(1280, 1280, 24);
        cam.targetTexture = rt; cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1280, 1280, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1280, 1280), 0, 0); tex.Apply();
        RenderTexture.active = null;
        File.WriteAllBytes("Assets/SampleJojo/Preview~/" + file, tex.EncodeToPNG());
        cam.targetTexture = null;
        Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(go);
    }
}
